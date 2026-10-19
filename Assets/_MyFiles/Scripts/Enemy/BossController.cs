using System;
using System.Collections.Generic;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Brawler.Enemy
{
    /// <summary>
    /// The Undead Executioner: a floating boss with a readable three-move kit, gated by phase.
    /// <list type="bullet">
    /// <item>Scythe swipe (parryable). Phase 2 chains a second swipe.</item>
    /// <item>Shadow dash: a long telegraph, then a ground-level charge across the arena. Jump, double jump or grapple over it.</item>
    /// <item>Summon (phase 2): calls flying minions while the boss keeps its distance.</item>
    /// </list>
    /// Super armour: hits don't interrupt it, but enough damage in a short time (poise) or a parry staggers it, which
    /// is the opening for big combos. Dormant until <see cref="Activate"/>; publishes <see cref="EnemyDefeatedEvent"/>
    /// and raises <see cref="Defeated"/> on death.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class BossController : MonoBehaviour, IDamageable, IKnockbackable, IInvulnerable, IFacingSource, IStaggerable, IChainTarget
    {
        // ============ Types ============

        /// <summary>What the boss is doing (read by <see cref="BossAnimationDriver"/>).</summary>
        public enum BossState
        {
            Dormant,
            Intro,
            Approach,
            Swipe,
            DashWindup,
            Dash,
            DashRecover,
            Summon,
            Staggered,
            Dead,
        }

        // ============ Serialized Fields ============

        [Header("Components")]
        [SerializeField] private Health _health;

        [SerializeField] private Hitbox _hitbox;

        [SerializeField] private EventBusAsset _eventBus;

        [Header("Stats")]
        [SerializeField, Min(1)] private int _maxHealth = 700;

        [SerializeField, Min(0)] private int _xpReward = 200;

        [Tooltip("Health fraction at which phase 2 (double swipe, summons, faster) begins.")]
        [SerializeField, Range(0f, 1f)] private float _phaseTwoAt = 0.5f;

        [Tooltip("Damage taken within PoiseWindow seconds that staggers the boss.")]
        [SerializeField, Min(1)] private int _poise = 70;

        [SerializeField, Min(0.1f)] private float _poiseWindow = 3f;

        [SerializeField, Min(0f)] private float _staggerSeconds = 0.9f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _moveSpeed = 2.2f;

        [Tooltip("Height the boss floats above the arena floor.")]
        [SerializeField, Min(0f)] private float _hoverHeight = 0.15f;

        [Tooltip("Distance it likes to keep before swiping.")]
        [SerializeField, Min(0f)] private float _swipeRange = 2.2f;

        [Header("Swipe")]
        [SerializeField] private AttackData _swipe;

        [Header("Shadow Dash")]
        [SerializeField] private AttackData _dash;

        [SerializeField, Min(0f)] private float _dashWindup = 0.75f;

        [SerializeField, Min(0f)] private float _dashSpeed = 13f;

        [SerializeField, Min(0f)] private float _dashRecover = 0.6f;

        [Header("Summon")]
        [SerializeField] private EnemyController _minionPrefab;

        [SerializeField] private EnemyCoordinator _coordinator;

        [SerializeField, Min(0)] private int _minionsPerSummon = 2;

        [SerializeField, Min(0)] private int _maxMinions = 3;

        [SerializeField, Min(0f)] private float _summonCast = 0.9f;

        [SerializeField, Min(0f)] private float _summonCooldown = 9f;

        [Header("Pacing")]
        [Tooltip("Seconds between decisions in phase 1 (phase 2 is 35% faster).")]
        [SerializeField, Min(0.1f)] private float _thinkInterval = 1.1f;

        [SerializeField, Min(0f)] private float _introSeconds = 1.6f;

        [SerializeField, Min(0f)] private float _deathLinger = 2.5f;

        // ============ Runtime ============

        private readonly FrameClock _clock = new FrameClock();
        private readonly List<EnemyController> _minions = new List<EnemyController>();
        private Action<EnemyController> _onMinionDespawned;

        private Transform _target;
        private Vector3 _lastChainPoint;
        private float _arenaMinX;
        private float _arenaMaxX;
        private float _floorY;
        private float _stateTime;
        private float _nextThink;
        private float _nextSummon;
        private int _dashDirection;
        private int _swipesLeft;
        private int _recentDamage;
        private float _recentDamageStart;

        // ============ Events & Properties ============

        /// <summary>Raised once when the boss dies.</summary>
        public event Action Defeated;

        /// <summary>Current behaviour.</summary>
        public BossState State { get; private set; } = BossState.Dormant;

        /// <summary>Seconds spent in the current state (scaled).</summary>
        public float StateTime => _stateTime;

        /// <summary>The current attack's simulation frame (Swipe only).</summary>
        public int AttackFrame => _clock.Frame;

        /// <summary>The swipe attack (frame data for the animation).</summary>
        public AttackData SwipeAttack => _swipe;

        /// <summary>Shadow dash wind-up length (seconds).</summary>
        public float DashWindup => _dashWindup;

        /// <summary>Summon cast length (seconds).</summary>
        public float SummonCast => _summonCast;

        /// <summary>Intro length (seconds).</summary>
        public float IntroSeconds => _introSeconds;

        /// <summary>True below the phase-two health fraction.</summary>
        public bool IsPhaseTwo => _health != null && _health.Normalized <= _phaseTwoAt;

        /// <summary>Counts every new swipe/dash/summon so presentation can flash a telegraph once per move.</summary>
        public int MoveCount { get; private set; }

        /// <summary>Hit points.</summary>
        public Health Health => _health;

        /// <inheritdoc />
        public int FacingSign { get; private set; } = -1;

        /// <inheritdoc />
        public bool IsInvulnerable => State == BossState.Dormant || State == BossState.Intro || State == BossState.Dead;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            _onMinionDespawned = OnMinionDespawned;
            _health.Initialize(_maxHealth);
            _floorY = transform.position.y - _hoverHeight;
            _arenaMinX = transform.position.x - 8f;
            _arenaMaxX = transform.position.x + 8f;
        }

        private void OnEnable()
        {
            ChainTargets.Register(this);
        }

        private void OnDisable()
        {
            ChainTargets.Unregister(this);
            if (_hitbox != null)
            {
                _hitbox.EndAttack();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || State == BossState.Dormant)
            {
                return;
            }

            _stateTime += dt;
            switch (State)
            {
                case BossState.Intro:
                    if (_stateTime >= _introSeconds)
                    {
                        SetState(BossState.Approach);
                    }

                    break;
                case BossState.Approach:
                    TickApproach(dt);
                    break;
                case BossState.DashWindup:
                    FaceTarget();
                    if (_stateTime >= _dashWindup)
                    {
                        BeginDash();
                    }

                    break;
                case BossState.Dash:
                    TickDash(dt);
                    break;
                case BossState.DashRecover:
                    if (_stateTime >= _dashRecover)
                    {
                        SetState(BossState.Approach);
                    }

                    break;
                case BossState.Summon:
                    if (_stateTime >= _summonCast)
                    {
                        SpawnMinions();
                        SetState(BossState.Approach);
                    }

                    break;
                case BossState.Staggered:
                    if (_stateTime >= _staggerSeconds)
                    {
                        SetState(BossState.Approach);
                    }

                    break;
                case BossState.Dead:
                    if (_stateTime >= _deathLinger)
                    {
                        gameObject.SetActive(false);
                    }

                    break;
            }
        }

        private void FixedUpdate()
        {
            if (State == BossState.Swipe)
            {
                TickSwipe(Time.fixedDeltaTime);
            }
            else if (State == BossState.Dash && _hitbox != null)
            {
                _hitbox.Sweep();
            }
        }

        // ============ API ============

        /// <summary>Wakes the boss: it fights <paramref name="target"/> inside [minX, maxX] on the floor at its current height.</summary>
        public void Activate(Transform target, float arenaMinX, float arenaMaxX)
        {
            _target = target;
            _arenaMinX = arenaMinX;
            _arenaMaxX = arenaMaxX;
            _nextSummon = Time.time + _summonCooldown * 0.5f;
            SetState(BossState.Intro);
        }

        /// <summary>Back to dormant at <paramref name="position"/> with full health (the player died during the fight).</summary>
        public void ResetBoss(Vector3 position)
        {
            DespawnMinions();
            if (_hitbox != null)
            {
                _hitbox.EndAttack();
            }

            gameObject.SetActive(true);
            transform.position = position;
            _health.ResetToMax();
            _recentDamage = 0;
            State = BossState.Dormant;
            _stateTime = 0f;
        }

        // ============ IChainTarget ============

        /// <inheritdoc />
        public ChainWeight ChainWeight => ChainWeight.Heavy;

        /// <inheritdoc />
        public Vector3 ChainPoint
        {
            get
            {
                if (this != null)
                {
                    _lastChainPoint = transform.position + Vector3.up * 1f;
                }

                return _lastChainPoint;
            }
        }

        /// <inheritdoc />
        public bool CanBeChained => this != null && isActiveAndEnabled && !IsInvulnerable;

        /// <inheritdoc />
        public bool Yank(Vector3 destination, int stunFrames, int damage, Transform source)
        {
            return false; // far too heavy: the chain pulls the player in instead
        }

        // ============ IDamageable / IKnockbackable / IStaggerable ============

        /// <inheritdoc />
        public bool TryApplyDamage(in DamageInfo info)
        {
            if (IsInvulnerable)
            {
                return false;
            }

            if (_health.TakeDamage(info.Amount))
            {
                Die();
                return true;
            }

            // Poise: enough damage in a short window staggers (only when it's not mid-dash: the charge always lands).
            if (Time.time - _recentDamageStart > _poiseWindow)
            {
                _recentDamage = 0;
                _recentDamageStart = Time.time;
            }

            _recentDamage += info.Amount;
            if (_recentDamage >= _poise && State != BossState.Dash)
            {
                _recentDamage = 0;
                SetState(BossState.Staggered);
            }

            return true;
        }

        /// <inheritdoc />
        public void ApplyKnockback(Vector3 velocity)
        {
            // Super armour: no knockback.
        }

        /// <inheritdoc />
        public void Stagger(int frames)
        {
            if (IsInvulnerable)
            {
                return;
            }

            SetState(BossState.Staggered);
            _stateTime = _staggerSeconds - Mathf.Max(_staggerSeconds, FrameTime.ToSeconds(frames)); // a parry stuns at least this long
        }

        // ============ Behaviour ============

        private void TickApproach(float dt)
        {
            if (_target == null)
            {
                return;
            }

            FaceTarget();
            Vector3 position = transform.position;
            float dx = _target.position.x - position.x;
            float distance = Mathf.Abs(dx);

            // Drift to swipe range and onto the player's lane.
            float desiredX = distance > _swipeRange * 0.8f ? Mathf.Sign(dx) * _moveSpeed : 0f;
            position.x += desiredX * dt;
            position.z = Mathf.MoveTowards(position.z, _target.position.z, _moveSpeed * 0.5f * dt);
            position.y = _floorY + _hoverHeight + Mathf.Sin(Time.time * 2f) * 0.08f;
            position.x = Mathf.Clamp(position.x, _arenaMinX, _arenaMaxX);
            transform.position = position;

            if (Time.time < _nextThink)
            {
                return;
            }

            _nextThink = Time.time + _thinkInterval * (IsPhaseTwo ? 0.65f : 1f);
            bool close = distance <= _swipeRange && Mathf.Abs(_target.position.z - position.z) < 0.6f;
            float roll = Random.value;

            if (IsPhaseTwo && Time.time >= _nextSummon && LiveMinions() < _maxMinions && roll < 0.35f && _minionPrefab != null)
            {
                _nextSummon = Time.time + _summonCooldown;
                SetState(BossState.Summon);
            }
            else if (close)
            {
                _swipesLeft = IsPhaseTwo ? 2 : 1;
                BeginSwipe();
            }
            else if (roll < (IsPhaseTwo ? 0.6f : 0.4f) && _dash != null)
            {
                SetState(BossState.DashWindup);
            }
        }

        private void BeginSwipe()
        {
            if (_swipe == null)
            {
                SetState(BossState.Approach);
                return;
            }

            FaceTarget();
            SetState(BossState.Swipe);
            _clock.Reset();
            if (_hitbox != null)
            {
                _hitbox.BeginAttack(_swipe, FacingSign);
            }
        }

        private void TickSwipe(float fixedDt)
        {
            int steps = _clock.Advance(fixedDt);
            for (int frame = _clock.Frame - steps + 1; frame <= _clock.Frame; frame++)
            {
                if (_swipe.Frames.IsActive(frame) && _hitbox != null)
                {
                    _hitbox.Sweep();
                }
            }

            if (!_swipe.Frames.IsFinished(_clock.Frame))
            {
                return;
            }

            if (_hitbox != null)
            {
                _hitbox.EndAttack();
            }

            _swipesLeft--;
            if (_swipesLeft > 0)
            {
                BeginSwipe();
            }
            else
            {
                SetState(BossState.Approach);
            }
        }

        private void BeginDash()
        {
            // Charge toward the player and on to the far wall.
            _dashDirection = FacingSign;
            SetState(BossState.Dash);
            if (_hitbox != null)
            {
                _hitbox.BeginAttack(_dash, _dashDirection);
            }
        }

        private void TickDash(float dt)
        {
            Vector3 position = transform.position;
            position.x += _dashDirection * _dashSpeed * dt;
            bool atWall = position.x <= _arenaMinX || position.x >= _arenaMaxX;
            position.x = Mathf.Clamp(position.x, _arenaMinX, _arenaMaxX);
            transform.position = position;

            if (atWall)
            {
                if (_hitbox != null)
                {
                    _hitbox.EndAttack();
                }

                SetState(BossState.DashRecover);
            }
        }

        private void SpawnMinions()
        {
            for (int i = 0; i < _minionsPerSummon && LiveMinions() < _maxMinions; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 position = transform.position + new Vector3(side * 1.6f, 1.6f, 0f);
                position.x = Mathf.Clamp(position.x, _arenaMinX, _arenaMaxX);

                EnemyController minion = Instantiate(_minionPrefab);
                minion.Despawned += _onMinionDespawned;
                minion.Spawn(position, _coordinator);
                _minions.Add(minion);
            }
        }

        private void Die()
        {
            if (_hitbox != null)
            {
                _hitbox.EndAttack();
            }

            DespawnMinions();
            SetState(BossState.Dead);
            if (_eventBus != null)
            {
                _eventBus.Publish(new EnemyDefeatedEvent(transform.position, _xpReward));
            }

            Defeated?.Invoke();
        }

        // ============ Helpers ============

        private void SetState(BossState state)
        {
            if (state == BossState.Staggered && _hitbox != null)
            {
                _hitbox.EndAttack();
            }

            State = state;
            _stateTime = 0f;
            if (state == BossState.Swipe || state == BossState.DashWindup || state == BossState.Summon)
            {
                MoveCount++;
            }
        }

        private void FaceTarget()
        {
            if (_target == null)
            {
                return;
            }

            float dx = _target.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f)
            {
                FacingSign = dx > 0f ? 1 : -1;
            }
        }

        private int LiveMinions()
        {
            _minions.RemoveAll(m => m == null || !m.isActiveAndEnabled);
            return _minions.Count;
        }

        private void OnMinionDespawned(EnemyController minion)
        {
            minion.Despawned -= _onMinionDespawned;
            _minions.Remove(minion);
            Destroy(minion.gameObject);
        }

        private void DespawnMinions()
        {
            for (int i = _minions.Count - 1; i >= 0; i--)
            {
                if (_minions[i] != null)
                {
                    _minions[i].Despawned -= _onMinionDespawned;
                    Destroy(_minions[i].gameObject);
                }
            }

            _minions.Clear();
        }

        // ============ Debug ============

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Vector3 p = transform.position;
            Gizmos.DrawLine(new Vector3(_arenaMinX, p.y, p.z), new Vector3(_arenaMaxX, p.y, p.z));
        }
    }
}
