using System;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Enemy composition root: builds the <see cref="EnemyContext"/> and state machine, drives it from Update/FixedUpdate,
    /// and is the damage/knockback/i-frame endpoint for the enemy's <see cref="Hurtbox"/>. Spawned and recycled by
    /// <see cref="EnemySpawner"/> through <see cref="Spawn"/> / <see cref="Despawn"/>.
    /// </summary>
    /// <remarks>
    /// Hit routing: killing blow -&gt; Dead; upward knockback &gt;= LaunchThreshold, or any hit while already launched
    /// -&gt; Knockdown (juggle); otherwise Hitstun. Mid-attack hits interrupt (no super armour).
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyController : MonoBehaviour, IDamageable, IKnockbackable, IInvulnerable, IFacingSource, IStaggerable, IChainTarget
    {
        // ============ Serialized Fields ============

        [Header("Data")]
        [Tooltip("Health, movement, spacing, attack and hit-reaction tuning.")]
        [SerializeField] private EnemySettings _settings;

        [Header("Components")]
        [Tooltip("Motor. Defaults to the CharacterController on this GameObject.")]
        [SerializeField] private CharacterController _characterController;

        [Tooltip("Hit points. Defaults to the Health on this GameObject.")]
        [SerializeField] private Health _health;

        [Tooltip("The enemy's attack hitbox (child).")]
        [SerializeField] private Hitbox _hitbox;

        [Tooltip("Ranged enemies: fires the projectiles. Leave empty for melee and flyers.")]
        [SerializeField] private ProjectileLauncher _launcher;

        [Tooltip("Shared event bus. EnemyDefeatedEvent is published here on death (XP orbs listen).")]
        [SerializeField] private EventBusAsset _eventBus;

        [Header("Scene (optional)")]
        [Tooltip("Used when the enemy is placed in a scene by hand. Spawned enemies get it from the spawner.")]
        [SerializeField] private EnemyCoordinator _coordinator;

        // ============ Runtime ============

        private EnemyContext _context;
        private Vector3 _lastChainPoint;
        private StateMachine<EnemyContext> _machine;

        // ============ Events & Properties ============

        /// <summary>Raised when the enemy removes itself (after its death linger).</summary>
        public event Action<EnemyController> Despawned;

        /// <summary>+1 right, -1 left.</summary>
        public int FacingSign => _context != null ? _context.FacingSign : -1;

        /// <summary>True on the ground after a knockdown, while getting up, and when dead.</summary>
        public bool IsInvulnerable => _context != null && _context.IsInvulnerable;

        /// <summary>Current state (animation, debugging).</summary>
        public IState<EnemyContext> CurrentState => _machine?.CurrentState;

        /// <summary>Blackboard (read-only use by presentation).</summary>
        public EnemyContext Context => _context;

        /// <summary>Hit points.</summary>
        public Health Health => _health;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_settings != null)
            {
                _health.Initialize(_settings.MaxHealth);
            }

            _context = new EnemyContext(this, _characterController, _settings, _hitbox, _health, _launcher)
            {
                Coordinator = _coordinator,
                States = new EnemyStates(),
            };
            _machine = new StateMachine<EnemyContext>(_context);
            _context.Machine = _machine;
            ApplyCoordinator();
            _machine.ChangeState(_context.States.Idle);
        }

        private void OnEnable()
        {
            ChainTargets.Register(this);
        }

        private void OnDisable()
        {
            ChainTargets.Unregister(this);
            if (_context == null)
            {
                return;
            }

            _context.WithdrawFromQueue();
            if (_coordinator != null)
            {
                _coordinator.Unregister(this);
            }

            if (_hitbox != null)
            {
                _hitbox.EndAttack();
            }
        }

        private void Update()
        {
            _context.Time = Time.time;
            _machine.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _context.Time = Time.time;
            _machine.FixedTick(Time.fixedDeltaTime);
        }

        // ============ API ============

        /// <summary>(Re)activates the enemy at <paramref name="position"/> with full health, idle, linked to <paramref name="coordinator"/>.</summary>
        public void Spawn(Vector3 position, EnemyCoordinator coordinator)
        {
            if (coordinator != null)
            {
                _coordinator = coordinator;
            }

            // CharacterController overrides transform writes while enabled.
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
            gameObject.SetActive(true);

            _context.Coordinator = _coordinator;
            ApplyCoordinator();
            if (_coordinator != null)
            {
                _coordinator.Register(this);
            }

            _health.ResetToMax();
            _context.Velocity = Vector3.zero;
            _context.VerticalVelocity = 0f;
            _context.IsInvulnerable = false;
            _context.NextAttackTime = Time.time + (_settings != null ? _settings.AttackCooldown : 0f);
            _machine.ChangeState(_context.States.Idle);
        }

        /// <summary>Removes the enemy from play (the spawner may recycle it).</summary>
        public void Despawn()
        {
            gameObject.SetActive(false);
            Despawned?.Invoke(this);
        }

        // ============ IDamageable ============

        /// <inheritdoc />
        public bool TryApplyDamage(in DamageInfo info)
        {
            if (_context == null || IsInvulnerable || _health.IsDead)
            {
                return false;
            }

            _context.LastHit = info;
            _context.WithdrawFromQueue(); // getting hit costs the place in line
            if (_health.TakeDamage(info.Amount))
            {
                _machine.ChangeState(_context.States.Dead);
                if (_eventBus != null && _settings != null)
                {
                    _eventBus.Publish(new EnemyDefeatedEvent(transform.position, _settings.XpReward));
                }

                return true;
            }

            // Flyers flinch in the air instead of being knocked down (they have no floor to land on).
            bool launched = _settings != null && info.Knockback.y >= _settings.LaunchThreshold;
            bool juggled = ReferenceEquals(_machine.CurrentState, _context.States.Knockdown) || !_context.IsGrounded;
            _machine.ChangeState((launched || juggled) && !_context.IsFlying ? (IState<EnemyContext>)_context.States.Knockdown : _context.States.Hitstun);
            return true;
        }

        // ============ IChainTarget ============

        /// <inheritdoc />
        public ChainWeight ChainWeight => _settings != null ? _settings.ChainWeight : ChainWeight.Light;

        /// <inheritdoc />
        public Vector3 ChainPoint
        {
            get
            {
                // A destroyed enemy answers with where it last was, so a chain or reticle still pointing at it never throws.
                if (this != null)
                {
                    _lastChainPoint = transform.position + Vector3.up * (_characterController != null ? _characterController.height * 0.6f : 0.5f);
                }

                return _lastChainPoint;
            }
        }

        /// <inheritdoc />
        public bool CanBeChained => this != null && _context != null && isActiveAndEnabled && !_health.IsDead && !IsInvulnerable;

        /// <summary>
        /// Chain yank: the hook's damage, a stun, and a drag that stops at <paramref name="destination"/> (the speed is
        /// worked out from the knockback deceleration). A flyer loses flight until the stun ends: pulled out of the sky.
        /// </summary>
        public bool Yank(Vector3 destination, int stunFrames, int damage, Transform source)
        {
            if (!CanBeChained || _settings == null)
            {
                return false;
            }

            bool flyer = _settings.Behaviour == EnemyBehaviour.Flyer;
            var info = new DamageInfo(damage, Vector3.zero, stunFrames, ChainPoint, source);
            if (!TryApplyDamage(info) || _health.IsDead)
            {
                return true; // the hook itself was the killing blow
            }

            // Hitstun even when airborne (a juggle knockdown would ignore the pull).
            _machine.ChangeState(_context.States.Hitstun);
            _context.FlightSuppressed = flyer;

            Vector3 delta = destination - transform.position;
            Vector3 planar = new Vector3(delta.x, 0f, delta.z);
            float distance = planar.magnitude;
            float speed = Mathf.Clamp(Mathf.Sqrt(2f * _settings.KnockbackDeceleration * distance), 4f, 20f);
            _context.Velocity = distance > 0.01f ? planar / distance * speed : Vector3.zero;

            // Arrive at the destination's height: time of flight from the horizontal speed, then a ballistic launch.
            float time = Mathf.Max(0.15f, distance / speed);
            float gravity = _settings.Gravity;
            _context.VerticalVelocity = flyer || delta.y > 0f
                ? delta.y / time - 0.5f * gravity * time
                : 2f; // grounded targets hop a little so they slide over small bumps
            return true;
        }

        // ============ IStaggerable ============

        /// <summary>Parried: drop the swing, get shoved back away from the target and reel for <paramref name="frames"/>.</summary>
        public void Stagger(int frames)
        {
            if (_context == null || _health.IsDead)
            {
                return;
            }

            _context.WithdrawFromQueue();
            _context.LastHit = new DamageInfo(0, Vector3.zero, frames, transform.position, null);
            _machine.ChangeState(_context.States.Hitstun);

            // Hitstun Enter faced the target and zeroed velocity; slide back from it (decelerates like knockback).
            if (_settings != null)
            {
                _context.Velocity = new Vector3(-_context.FacingSign * _settings.StaggerPushback, 0f, 0f);
            }
        }

        // ============ IKnockbackable ============

        /// <inheritdoc />
        public void ApplyKnockback(Vector3 velocity)
        {
            if (_context == null || _settings == null)
            {
                return;
            }

            float scale = _settings.KnockbackScale;
            _context.Velocity = new Vector3(velocity.x, 0f, velocity.z) * scale;

            IState<EnemyContext> state = _machine.CurrentState;
            float lift = velocity.y * scale;
            if (ReferenceEquals(state, _context.States.Dead))
            {
                lift = Mathf.Max(lift, _settings.DeathPop);
            }
            else if (ReferenceEquals(state, _context.States.Knockdown))
            {
                // Juggle: even flat hits keep a launched enemy in the air.
                lift = Mathf.Max(lift, _settings.JugglePop);
            }

            if (lift > 0f)
            {
                _context.VerticalVelocity = lift;
            }
        }

        // ============ Helpers ============

        /// <summary>Hands the coordinator's camera to every billboard and health bar under this enemy (prefabs can't reference it).</summary>
        private void ApplyCoordinator()
        {
            Transform cameraTransform = _coordinator != null ? _coordinator.CameraTransform : null;
            if (cameraTransform == null)
            {
                return;
            }

            foreach (BillboardSprite billboard in GetComponentsInChildren<BillboardSprite>(true))
            {
                billboard.CameraTransform = cameraTransform;
            }

            foreach (WorldHealthBar bar in GetComponentsInChildren<WorldHealthBar>(true))
            {
                bar.CameraTransform = cameraTransform;
            }
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_settings == null || _hitbox == null)
            {
                Debug.LogWarning($"{nameof(EnemyController)} on '{name}': assign Settings and Hitbox.", this);
            }
        }
    }
}
