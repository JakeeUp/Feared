using Brawler.Combat;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>How an enemy type fights.</summary>
    public enum EnemyBehaviour
    {
        /// <summary>Walks up and swings (the default beat 'em up grunt).</summary>
        Melee = 0,

        /// <summary>Keeps its distance and fires projectiles (needs a ProjectileLauncher).</summary>
        Ranged = 1,

        /// <summary>Hovers above the ground and dives at the target. No gravity until it dies.</summary>
        Flyer = 2,
    }

    /// <summary>Tuning for one enemy type: health, movement, spacing, its attack, and hit reactions.</summary>
    [CreateAssetMenu(menuName = "Brawler/Enemy/Enemy Settings", fileName = "EnemySettings")]
    public sealed class EnemySettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Behaviour")]
        [Tooltip("Melee walks up and swings; Ranged keeps its distance and shoots; Flyer hovers and dives.")]
        [SerializeField] private EnemyBehaviour _behaviour = EnemyBehaviour.Melee;

        [Tooltip("Ranged: X distance it tries to keep from the target while shooting.")]
        [SerializeField, Min(0f)] private float _preferredRange = 5f;

        [Tooltip("Ranged: only shoots when the target is within this height difference (no shooting at ledges far above).")]
        [SerializeField, Min(0f)] private float _maxShotHeightDifference = 1.5f;

        [Tooltip("Flyer: height above the target it hovers at while lining up a dive.")]
        [SerializeField, Min(0f)] private float _hoverHeight = 1.8f;

        [Tooltip("Flyer: vertical acceleration toward its hover height (units/s^2).")]
        [SerializeField, Min(0f)] private float _hoverAcceleration = 18f;

        [Tooltip("Light: the player's chain yanks it in. Heavy: the chain pulls the player to it instead.")]
        [SerializeField] private ChainWeight _chainWeight = ChainWeight.Light;

        [Header("Health")]
        [SerializeField, Min(1)] private int _maxHealth = 60;

        [Header("Movement")]
        [Tooltip("Walk speed (units/s).")]
        [SerializeField, Min(0f)] private float _moveSpeed = 2.4f;

        [Tooltip("Acceleration toward the walk velocity (units/s^2).")]
        [SerializeField, Min(0f)] private float _acceleration = 25f;

        [Tooltip("Z speed multiplier, like the player's (beat 'em up depth movement is slower).")]
        [SerializeField, Range(0f, 1f)] private float _depthSpeedMultiplier = 0.8f;

        [Tooltip("Gravity (units/s^2, negative = down).")]
        [SerializeField] private float _gravity = -30f;

        [Header("Jumping")]
        [Tooltip("Upward speed of a jump (units/s). ~10 clears about 1.7 units.")]
        [SerializeField, Min(0f)] private float _jumpVelocity = 10f;

        [Tooltip("Jumps up when the target stands at least this much higher.")]
        [SerializeField, Min(0f)] private float _jumpUpThreshold = 0.45f;

        [Tooltip("...and is within this horizontal distance.")]
        [SerializeField, Min(0f)] private float _jumpUpRange = 3f;

        [Tooltip("Jumps over an obstacle after being blocked for this long (seconds).")]
        [SerializeField, Min(0f)] private float _blockedJumpDelay = 0.25f;

        [Tooltip("Horizontal speed while airborne (units/s).")]
        [SerializeField, Min(0f)] private float _airSpeed = 3.2f;

        [Tooltip("Seconds between jumps.")]
        [SerializeField, Min(0f)] private float _jumpCooldown = 0.8f;

        [Header("Awareness & Spacing")]
        [Tooltip("Starts chasing when the target is closer than this.")]
        [SerializeField, Min(0f)] private float _aggroRange = 9f;

        [Tooltip("Gives up and idles when the target is further than this.")]
        [SerializeField, Min(0f)] private float _loseRange = 14f;

        [Tooltip("Attacks when within this X distance of the target (and lined up in depth).")]
        [SerializeField, Min(0f)] private float _attackRange = 1.2f;

        [Tooltip("Max Z difference to count as lined up in the target's lane.")]
        [SerializeField, Min(0f)] private float _laneTolerance = 0.3f;

        [Tooltip("X distance kept while waiting for an attack token (others are attacking).")]
        [SerializeField, Min(0f)] private float _waitDistance = 2.6f;

        [Header("Attack")]
        [Tooltip("The swing (frame data, hitbox, damage). Long startup = readable telegraph.")]
        [SerializeField] private AttackData _attack;

        [Tooltip("Seconds between this enemy's attacks.")]
        [SerializeField, Min(0f)] private float _attackCooldown = 1f;

        [Tooltip("Random extra cooldown (0..this) so groups don't attack in sync.")]
        [SerializeField, Min(0f)] private float _cooldownJitter = 0.6f;

        [Header("Hit Reactions")]
        [Tooltip("Multiplier on incoming knockback.")]
        [SerializeField, Min(0f)] private float _knockbackScale = 1f;

        [Tooltip("Knockback slide deceleration (units/s^2).")]
        [SerializeField, Min(0f)] private float _knockbackDeceleration = 18f;

        [Tooltip("Hits with upward knockback at or above this launch into a knockdown (the finisher does).")]
        [SerializeField, Min(0f)] private float _launchThreshold = 1.5f;

        [Tooltip("Upward pop (units/s) when hit while already airborne, so juggles keep them up.")]
        [SerializeField, Min(0f)] private float _jugglePop = 3.5f;

        [Tooltip("Backward slide speed (units/s) when the enemy's swing is parried, so the stagger reads at a glance.")]
        [SerializeField, Min(0f)] private float _staggerPushback = 4f;

        [Tooltip("Frames lying on the ground after a knockdown (invulnerable).")]
        [SerializeField, Min(0)] private int _downFrames = 40;

        [Tooltip("Frames getting back up (invulnerable).")]
        [SerializeField, Min(0)] private int _getUpFrames = 20;

        [Header("Rewards")]
        [Tooltip("Experience dropped as orbs on death.")]
        [SerializeField, Min(0)] private int _xpReward = 15;

        [Header("Death")]
        [Tooltip("Upward pop on the killing blow (units/s).")]
        [SerializeField, Min(0f)] private float _deathPop = 4f;

        [Tooltip("Seconds the body stays (blinking) after landing before it despawns.")]
        [SerializeField, Min(0f)] private float _deathLingerSeconds = 1.2f;

        // ============ Properties ============

        /// <summary>Yanked in, or zipped to.</summary>
        public ChainWeight ChainWeight => _chainWeight;

        /// <summary>How this enemy type fights.</summary>
        public EnemyBehaviour Behaviour => _behaviour;

        /// <summary>Ranged: X distance kept while shooting.</summary>
        public float PreferredRange => _preferredRange;

        /// <summary>Ranged: max height difference to shoot at.</summary>
        public float MaxShotHeightDifference => _maxShotHeightDifference;

        /// <summary>Flyer: hover height above the target.</summary>
        public float HoverHeight => _hoverHeight;

        /// <summary>Flyer: vertical acceleration toward the hover height.</summary>
        public float HoverAcceleration => _hoverAcceleration;

        /// <summary>Jump launch speed (units/s).</summary>
        public float JumpVelocity => _jumpVelocity;

        /// <summary>Target height difference that triggers a jump up.</summary>
        public float JumpUpThreshold => _jumpUpThreshold;

        /// <summary>Horizontal distance within which the enemy jumps up to the target.</summary>
        public float JumpUpRange => _jumpUpRange;

        /// <summary>Seconds blocked before jumping over the obstacle.</summary>
        public float BlockedJumpDelay => _blockedJumpDelay;

        /// <summary>Air speed (units/s).</summary>
        public float AirSpeed => _airSpeed;

        /// <summary>Seconds between jumps.</summary>
        public float JumpCooldown => _jumpCooldown;

        /// <summary>Experience dropped on death.</summary>
        public int XpReward => _xpReward;

        /// <summary>Hit points at full health.</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>Walk speed (units/s).</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Walk acceleration (units/s^2).</summary>
        public float Acceleration => _acceleration;

        /// <summary>Z speed multiplier.</summary>
        public float DepthSpeedMultiplier => _depthSpeedMultiplier;

        /// <summary>Gravity (units/s^2).</summary>
        public float Gravity => _gravity;

        /// <summary>Chase start distance.</summary>
        public float AggroRange => _aggroRange;

        /// <summary>Give-up distance.</summary>
        public float LoseRange => _loseRange;

        /// <summary>X distance that allows an attack.</summary>
        public float AttackRange => _attackRange;

        /// <summary>Max Z difference to count as lined up.</summary>
        public float LaneTolerance => _laneTolerance;

        /// <summary>X distance kept while waiting for a token.</summary>
        public float WaitDistance => _waitDistance;

        /// <summary>The enemy's attack.</summary>
        public AttackData Attack => _attack;

        /// <summary>Seconds between attacks.</summary>
        public float AttackCooldown => _attackCooldown;

        /// <summary>Random extra cooldown (max).</summary>
        public float CooldownJitter => _cooldownJitter;

        /// <summary>Incoming knockback multiplier.</summary>
        public float KnockbackScale => _knockbackScale;

        /// <summary>Knockback slide deceleration (units/s^2).</summary>
        public float KnockbackDeceleration => _knockbackDeceleration;

        /// <summary>Upward knockback that triggers a knockdown.</summary>
        public float LaunchThreshold => _launchThreshold;

        /// <summary>Backward slide speed when parried (units/s).</summary>
        public float StaggerPushback => _staggerPushback;

        /// <summary>Upward pop when hit airborne (units/s).</summary>
        public float JugglePop => _jugglePop;

        /// <summary>Frames lying down.</summary>
        public int DownFrames => _downFrames;

        /// <summary>Frames getting up.</summary>
        public int GetUpFrames => _getUpFrames;

        /// <summary>Upward pop on death (units/s).</summary>
        public float DeathPop => _deathPop;

        /// <summary>Seconds the body lingers before despawning.</summary>
        public float DeathLingerSeconds => _deathLingerSeconds;

        // ============ Validation ============

        private void OnValidate()
        {
            _loseRange = Mathf.Max(_loseRange, _aggroRange);
            _waitDistance = Mathf.Max(_waitDistance, _attackRange);
        }
    }
}
