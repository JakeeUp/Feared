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

        [Header("Data")]
        [Tooltip("Locomotion, dash and hit-reaction tuning.")]
        [SerializeField] private PlayerMovementSettings _movementSettings;

        [Tooltip("Input buffer window/capacity. The buffer is built once in Awake (capacity is fixed afterwards).")]
        [SerializeField] private InputBufferSettings _inputBufferSettings;

        [Tooltip("Combo chain played by consecutive Attack presses.")]
        [SerializeField] private ComboData _comboData;

        // ============ Runtime ============

        private PlayerContext _context;
        private StateMachine<PlayerContext> _machine;
        private Action<BufferedCommand> _onCommandPressed;

        // ============ Properties ============

        /// <summary>Facing sign (+1 right, -1 left). Never 0.</summary>
        public int FacingSign => _context != null ? _context.FacingSign : 1;

        /// <summary>True during dash i-frames.</summary>
        public bool IsInvulnerable => _context != null && _context.IsInvulnerable;

        /// <summary>Current state (debugging / gizmos / animation). Null before Awake.</summary>
        public IState<PlayerContext> CurrentState => _machine?.CurrentState;

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
            _machine = new StateMachine<PlayerContext>(_context);
            _context.Machine = _machine;

            _onCommandPressed = OnCommandPressed;

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
            // Wiring: publish scaled time to the context, then tick. dt == 0 during hitstop -> states freeze.
            _context.Time = Time.time;
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
            _context.Buffer.Enqueue(command, Time.time);
        }

        // ============ IDamageable ============

        /// <summary>Receives a hit. Rejected while invulnerable; otherwise records it and enters (or re-enters) Hitstun.</summary>
        /// <param name="info">Incoming hit.</param>
        /// <returns>True if the hit connected.</returns>
        public bool TryApplyDamage(in DamageInfo info)
        {
            if (IsInvulnerable)
            {
                return false;
            }

            // Hitstun -> Hitstun re-enters, which refreshes the stun.
            _context.LastHit = info;
            _machine.ChangeState(_context.States.Hitstun);
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
