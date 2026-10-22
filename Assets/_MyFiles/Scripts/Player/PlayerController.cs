using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Player composition root. Builds the <see cref="InputBuffer"/>, <see cref="PlayerContext"/>, <see cref="PlayerStates"/>
    /// and <see cref="StateMachine{TContext}"/> in Awake, forwards input commands into the buffer, and drives the machine
    /// from Update/FixedUpdate. Also the damage/knockback/i-frame endpoint for the player's <see cref="Hurtbox"/>, and the
    /// <see cref="IFacingSource"/> for the billboard sprite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour, IFacingSource, IDamageable, IKnockbackable, IInvulnerable
    {
        // ============ Serialized Fields ============

        [Header("Components")]
        [Tooltip("Motor. Defaults to the CharacterController on this GameObject.")]
        [SerializeField] private CharacterController _characterController;

        [Tooltip("Input source. Defaults to the PlayerInputReader on this GameObject.")]
        [SerializeField] private PlayerInputReader _inputReader;

        [Tooltip("Player hitbox (child 'Hitbox'); driven by AttackState.")]
        [SerializeField] private Hitbox _hitbox;

        [Tooltip("Hit points. Optional: without it the player can't be worn down. At 0 the player respawns where they started.")]
        [SerializeField] private Health _health;

        [Tooltip("Shared event bus. ParryEvent is published here on a successful parry.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Header("Data")]
        [Tooltip("Locomotion, dash and hit-reaction tuning.")]
        [SerializeField] private PlayerMovementSettings _movementSettings;

        [Tooltip("Input buffer window/capacity. The buffer is built once in Awake (capacity is fixed afterwards).")]
        [SerializeField] private InputBufferSettings _inputBufferSettings;

        [Tooltip("Combo chain played by consecutive Attack presses.")]
        [SerializeField] private ComboData _comboData;

        [Tooltip("Attack while holding up: an overhead slash (anti-air). Optional.")]
        [SerializeField] private AttackData _upAttack;

        [Tooltip("Attack while holding down in the air: a downward slash that pogos off whatever it hits. Optional.")]
        [SerializeField] private AttackData _downAttack;

        // ============ Runtime ============

        private PlayerContext _context;
        private StateMachine<PlayerContext> _machine;
        private Action<BufferedCommand> _onCommandPressed;
        private Vector3 _spawnPosition;
        private bool _respawnPending;

        // ============ Properties ============

        /// <summary>Facing sign (+1 right, -1 left). Never 0.</summary>
        public int FacingSign => _context != null ? _context.FacingSign : 1;

        /// <summary>True during dash/roll i-frames, the parry counter window, or an external effect (bubble shield).</summary>
        public bool IsInvulnerable => (_context != null && _context.IsInvulnerable) || ExternalInvulnerable;

        /// <summary>Invulnerability granted from outside the state machine (e.g. the Bubble Shield ability).</summary>
        public bool ExternalInvulnerable { get; set; }

        /// <summary>Current state (debugging / gizmos / animation). Null before Awake.</summary>
        public IState<PlayerContext> CurrentState => _machine?.CurrentState;

        /// <summary>Where the player comes back after dying (the start position until a checkpoint moves it).</summary>
        public Vector3 RespawnPoint
        {
            get => _spawnPosition;
            set => _spawnPosition = value;
        }

        /// <summary>Raised after the player respawns from death (level scripts reset their encounters).</summary>
        public event Action Respawned;

        /// <summary>The player's blackboard (read-only use by presentation, e.g. PlayerAnimationDriver). Null before Awake.</summary>
        public PlayerContext Context => _context;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: cache components, build the object graph, enter Idle.
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_inputReader == null)
            {
                _inputReader = GetComponent<PlayerInputReader>();
            }

            float window = _inputBufferSettings != null ? _inputBufferSettings.BufferWindowSeconds : 0.2f;
            int capacity = _inputBufferSettings != null ? Mathf.Max(1, _inputBufferSettings.Capacity) : 8;
            var buffer = new InputBuffer(window, capacity);

            _context = new PlayerContext(this, _characterController, _inputReader, buffer, _movementSettings, _comboData, _hitbox);
            _context.States = new PlayerStates();
            _context.UpAttack = _upAttack;
            _context.DownAttack = _downAttack;
            _machine = new StateMachine<PlayerContext>(_context);
            _context.Machine = _machine;

            _onCommandPressed = OnCommandPressed;
            _spawnPosition = transform.position;

            _machine.ChangeState(_context.States.Idle);
        }

        private void OnEnable()
        {
            if (_inputReader != null)
            {
                _inputReader.CommandPressed += _onCommandPressed;
            }
        }

        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.CommandPressed -= _onCommandPressed;
            }

            if (_context == null)
            {
                return;
            }

            // Back to Idle so re-enabling never resumes a stale dash (i-frames) or attack (live hitbox).
            _context.ComboTracker.Reset();
            _machine.ChangeState(_context.States.Idle);
            _context.IsInvulnerable = false;
            _context.Buffer.Clear();

            if (_hitbox != null)
            {
                _hitbox.EndAttack();
            }
        }

        private void Update()
        {
            if (_respawnPending)
            {
                Respawn();
            }

            // Wiring: publish scaled time to the context, then tick. dt == 0 during hitstop -> states freeze.
            _context.Time = Time.time;
            UpdateChain(Time.deltaTime);
            _machine.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            // Wiring: frame timelines (attack/dash/hitstun) advance here via each state's FrameClock.
            _context.Time = Time.time;
            _machine.FixedTick(Time.fixedDeltaTime);
        }

        // ============ Input ============

        private void OnCommandPressed(BufferedCommand command)
        {
            // Scaled time: it stops during hitstop, so presses made in the freeze are still fresh afterwards.
            _context.Buffer.EnqueueLatest(command, Time.time);
        }

        // ============ IDamageable ============

        /// <summary>Receives a hit. Rejected while invulnerable; otherwise records it and enters (or re-enters) Hitstun.</summary>
        /// <param name="info">Incoming hit.</param>
        /// <returns>True if the hit connected.</returns>
        public bool TryApplyDamage(in DamageInfo info)
        {
            // Already dead and waiting to respawn: no more hitstun, hitstop or shake.
            if (_health != null && _health.IsDead)
            {
                return false;
            }

            if (IsInvulnerable)
            {
                return false;
            }

            if (_context.IsParryActive)
            {
                Parry(info);
                return false; // deflected: no damage, knockback or HitEvent
            }

            // Hitstun -> Hitstun re-enters, which refreshes the stun. Getting hit drops a flying chain.
            _context.Chain.Cancel();
            _context.LastHit = info;
            _machine.ChangeState(_context.States.Hitstun);

            // Respawn next Update, after the hitbox has finished applying knockback for this hit.
            if (_health != null && _health.TakeDamage(info.Amount))
            {
                _respawnPending = true;
            }

            return true;
        }

        // ============ Chain ============

        /// <summary>
        /// Throws the chain from any free state (not while stunned, swinging or zipping), flies it, and when it lands
        /// decides what happens: a ring starts a swing, a light enemy is yanked in front of the player, a heavy one pulls
        /// the player to it.
        /// </summary>
        private void UpdateChain(float deltaTime)
        {
            IState<PlayerContext> state = _machine.CurrentState;
            PlayerStates states = _context.States;
            bool busy = ReferenceEquals(state, states.Hitstun) || ReferenceEquals(state, states.Grapple) || ReferenceEquals(state, states.Zip);
            if (busy)
            {
                _context.Chain.Cancel();
                return;
            }

            _context.TryFireChain();
            Chain chain = _context.Chain;
            if (deltaTime <= 0f || !chain.Advance(_context.RopeOrigin, _movementSettings.ChainSpeed, deltaTime, _movementSettings.GrappleRange))
            {
                return;
            }

            GrappleAnchor anchor = chain.Anchor;
            IChainTarget enemy = chain.Enemy;
            chain.Cancel();

            if (anchor != null)
            {
                _context.PendingAnchor = anchor;
                _machine.ChangeState(states.Grapple);
                return;
            }

            if (enemy == null)
            {
                return;
            }

            if (enemy.ChainWeight == ChainWeight.Heavy)
            {
                _context.PendingZipTarget = enemy;
                _machine.ChangeState(states.Zip);
                return;
            }

            Vector3 toEnemy = enemy.ChainPoint - transform.position;
            int side = toEnemy.x >= 0f ? 1 : -1;
            _context.FacingSign = side;
            Vector3 destination = transform.position + new Vector3(side * _movementSettings.ChainYankDistance, 0f, 0f);
            if (enemy.Yank(destination, _movementSettings.ChainStunFrames, _movementSettings.ChainDamage, transform) && _eventBus != null)
            {
                var info = new DamageInfo(_movementSettings.ChainDamage, Vector3.zero, _movementSettings.ChainStunFrames, enemy.ChainPoint, transform);
                Transform target = enemy is Component component ? component.transform : null;
                _eventBus.Publish(new HitEvent(info, target, _movementSettings.ChainHitStopFrames, 0.15f));
            }
        }

        // ============ Parry ============

        /// <summary>Deflects <paramref name="info"/>: staggers the attacker, opens the counter window, publishes ParryEvent.</summary>
        private void Parry(in DamageInfo info)
        {
            _context.States.Parry.OnParried(_context);

            Transform attacker = info.Source;
            if (attacker != null)
            {
                float dx = attacker.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.01f)
                {
                    _context.FacingSign = dx > 0f ? 1 : -1;
                }

                // Rare event, so a component lookup here is fine.
                if (attacker.TryGetComponent(out IStaggerable staggerable))
                {
                    staggerable.Stagger(_movementSettings.ParryStaggerFrames);
                }
            }

            if (_eventBus != null)
            {
                _eventBus.Publish(new ParryEvent(info.HitPoint, transform, attacker,
                    _movementSettings.ParryHitStopFrames, _movementSettings.ParryScreenShake));
            }
        }

        // ============ Respawn ============

        /// <summary>Back to the respawn point with full health, standing idle.</summary>
        private void Respawn()
        {
            _respawnPending = false;
            _characterController.enabled = false; // CharacterController overrides transform writes while enabled
            transform.position = _spawnPosition;
            _characterController.enabled = true;

            _health.ResetToMax();
            _context.Velocity = Vector3.zero;
            _context.VerticalVelocity = 0f;
            _context.ComboTracker.Reset();
            _context.Buffer.Clear();
            _machine.ChangeState(_context.States.Idle);
            Respawned?.Invoke();
        }

        /// <summary>
        /// Moves the player to <paramref name="position"/> standing idle, keeping health (pit falls). Clears velocity,
        /// combo and buffer so nothing carries over from the fall.
        /// </summary>
        public void Teleport(Vector3 position)
        {
            _characterController.enabled = false; // CharacterController overrides transform writes while enabled
            transform.position = position;
            _characterController.enabled = true;

            _context.Velocity = Vector3.zero;
            _context.VerticalVelocity = 0f;
            _context.ComboTracker.Reset();
            _context.Buffer.Clear();
            _machine.ChangeState(_context.States.Idle);
        }

        /// <summary>Deals <paramref name="amount"/> damage without a hit reaction (pits, hazards). Dying respawns as usual.</summary>
        /// <returns>True if this killed the player.</returns>
        public bool TakeEnvironmentDamage(int amount)
        {
            if (_health == null || !_health.TakeDamage(amount))
            {
                return false;
            }

            _respawnPending = true;
            return true;
        }

        // ============ IKnockbackable ============

        /// <summary>Sets the player's planar knockback velocity (X/Z; Y is dropped — no juggles on Day One).</summary>
        /// <param name="velocity">World-space knockback velocity (units/s).</param>
        public void ApplyKnockback(Vector3 velocity)
        {
            _context.Velocity = new Vector3(velocity.x, 0f, velocity.z);
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_inputReader == null)
            {
                _inputReader = GetComponent<PlayerInputReader>();
            }

            if (_movementSettings == null || _inputBufferSettings == null || _comboData == null || _hitbox == null)
            {
                Debug.LogWarning($"{nameof(PlayerController)} on '{name}': assign Movement Settings, Input Buffer Settings, Combo Data and Hitbox.", this);
            }
        }

        // ============ Debug ============

        private void OnDrawGizmosSelected()
        {
            if (_context == null)
            {
                return;
            }

            // Facing arrow (blue while invulnerable).
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Gizmos.color = _context.IsInvulnerable ? Color.cyan : Color.yellow;
            Gizmos.DrawLine(origin, origin + Vector3.right * (0.75f * _context.FacingSign));
        }
    }
}
