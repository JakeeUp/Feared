using UnityEngine;

namespace Brawler.Player
{
    /// <summary>Locomotion, dash and hit-reaction tuning for the player.</summary>
    [CreateAssetMenu(menuName = "Brawler/Player/Movement Settings", fileName = "PlayerMovementSettings")]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Locomotion (X/Z plane)")]
        [Tooltip("Top run speed in units/s.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;

        [Tooltip("Multiplier on Z (depth) speed. Beat 'em ups usually move slower into/out of the screen.")]
        [SerializeField, Range(0f, 1f)] private float _depthSpeedMultiplier = 0.75f;

        [Tooltip("Acceleration toward target velocity in units/s^2. High = snappy.")]
        [SerializeField, Min(0f)] private float _acceleration = 120f;

        [Tooltip("Deceleration to rest when input is released, in units/s^2.")]
        [SerializeField, Min(0f)] private float _deceleration = 140f;

        [Tooltip("Stick/keyboard magnitude below which input counts as neutral.")]
        [SerializeField, Range(0f, 0.9f)] private float _inputDeadzone = 0.15f;

        [Tooltip("Gravity in units/s^2 (negative = down). Keeps the CharacterController grounded.")]
        [SerializeField] private float _gravity = -38f;

        [Header("Dash")]
        [Tooltip("Air/wall dash speed in units/s along input direction (or facing if neutral). Ground presses roll instead.")]
        [SerializeField, Min(0f)] private float _dashSpeed = 16f;

        [Tooltip("Total dash duration in 60 Hz frames.")]
        [SerializeField, Min(1)] private int _dashFrames = 12;

        [Tooltip("First dash frame (inclusive) with invulnerability.")]
        [SerializeField, Min(0)] private int _dashIFrameStart = 0;

        [Tooltip("Dash frame (exclusive) where invulnerability ends. Must be <= Dash Frames.")]
        [SerializeField, Min(0)] private int _dashIFrameEnd = 8;

        [Header("Roll (ground dodge)")]
        [Tooltip("Roll top speed in units/s along input direction (or facing if neutral).")]
        [SerializeField, Min(0f)] private float _rollSpeed = 9f;

        [Tooltip("Total roll duration in 60 Hz frames (the roll animation is stretched over this).")]
        [SerializeField, Min(1)] private int _rollFrames = 26;

        [Tooltip("Roll progress (0..1) where the speed starts easing down.")]
        [SerializeField, Range(0f, 1f)] private float _rollEaseStart = 0.6f;

        [Tooltip("Fraction of RollSpeed left at the very end of the roll.")]
        [SerializeField, Range(0f, 1f)] private float _rollEndSpeedFraction = 0.2f;

        [Tooltip("First roll frame (inclusive) with invulnerability.")]
        [SerializeField, Min(0)] private int _rollIFrameStart = 2;

        [Tooltip("Roll frame (exclusive) where invulnerability ends. Must be <= Roll Frames.")]
        [SerializeField, Min(0)] private int _rollIFrameEnd = 16;

        [Tooltip("Roll frame from which a buffered Jump or Attack cancels the rest of the roll (its recovery). Must be <= Roll Frames.")]
        [SerializeField, Min(0)] private int _rollCancelFrame = 16;

        [Header("Jump")]
        [Tooltip("Upward launch speed in units/s. Apex height = v^2 / (2 * |Gravity|).")]
        [SerializeField, Min(0f)] private float _jumpVelocity = 12f;

        [Tooltip("Gravity multiplier while falling. > 1 = snappier, less floaty arcs.")]
        [SerializeField, Min(1f)] private float _fallGravityMultiplier = 2f;
        [Tooltip("Gravity multiplier while still rising after Jump is released (variable height without a sudden stop).")]
        [SerializeField, Min(1f)] private float _releaseGravityMultiplier = 2.4f;

        [Tooltip("Below this vertical speed (units/s, up or down) the jump is at its apex: gravity eases while Jump is held.")]
        [SerializeField, Min(0f)] private float _apexHangThreshold = 2.5f;

        [Tooltip("Gravity multiplier at the apex while Jump is held: the Hollow Knight float that gives time to steer.")]
        [SerializeField, Range(0.1f, 1f)] private float _apexGravityMultiplier = 0.5f;

        [Tooltip("Terminal fall speed in units/s.")]
        [SerializeField, Min(0f)] private float _maxFallSpeed = 22f;

        [Tooltip("Planar acceleration toward input while airborne, units/s^2.")]
        [SerializeField, Min(0f)] private float _airAcceleration = 60f;

        [Tooltip("Seconds after walking off a ledge during which Jump still works.")]
        [SerializeField, Min(0f)] private float _coyoteTime = 0.1f;

        [Tooltip("Dashes allowed per airtime (refilled on landing or grabbing a new wall).")]
        [SerializeField, Min(0)] private int _airDashes = 1;

        [Tooltip("Extra jumps allowed in the air (1 = double jump). Refilled on landing or grabbing a new wall.")]
        [SerializeField, Min(0)] private int _airJumps = 1;

        [Tooltip("Upward speed of an air (double) jump, units/s. A bit below JumpVelocity so it extends rather than doubles height.")]
        [SerializeField, Min(0f)] private float _doubleJumpVelocity = 8f;

        [Header("Wall")]
        [Tooltip("Layers that count as climbable walls. Triggers and other characters are always ignored.")]
        [SerializeField] private LayerMask _wallLayers = ~0;

        [Tooltip("Extra reach (units) beyond the capsule edge when probing for a wall.")]
        [SerializeField, Min(0f)] private float _wallCheckDistance = 0.12f;

        [Tooltip("Frames the player hangs still on first touching a wall (the grab), before sliding.")]
        [SerializeField, Min(0)] private int _wallGrabFrames = 12;

        [Tooltip("Max slide-down speed on a wall, units/s.")]
        [SerializeField, Min(0f)] private float _wallSlideSpeed = 2.5f;

        [Tooltip("Upward speed of a wall jump, units/s.")]
        [SerializeField, Min(0f)] private float _wallJumpVelocity = 11f;

        [Tooltip("Horizontal kick away from the wall on a wall jump, units/s.")]
        [SerializeField, Min(0f)] private float _wallJumpPush = 7f;

        [Tooltip("Frames of locked air control after a wall jump so the kick-off isn't instantly undone.")]
        [SerializeField, Min(0)] private int _wallJumpLockFrames = 8;

        [Tooltip("Fraction of air control kept during those frames (0 = locked, 1 = free). Some control never feels like a fight.")]
        [SerializeField, Range(0f, 1f)] private float _wallJumpControl = 0.35f;

        [Header("Grapple")]
        [Tooltip("How far away (units) a grapple anchor can be latched onto.")]
        [SerializeField, Min(0f)] private float _grappleRange = 8.5f;

        [Tooltip("Seconds the hook takes to fly out before the rope goes taut (the player keeps falling meanwhile).")]
        [SerializeField, Min(0f)] private float _grappleThrowTime = 0.07f;

        [Tooltip("Shortest and longest rope (units). Latching from further out than the max pulls the player in.")]
        [SerializeField, Min(0.5f)] private float _grappleMinLength = 1.6f;

        [SerializeField, Min(0.5f)] private float _grappleMaxLength = 5f;

        [Tooltip("The rope reels in toward this length while swinging (units). Shorter = faster, tighter swings.")]
        [SerializeField, Min(0.5f)] private float _grapplePreferredLength = 3.6f;

        [Tooltip("Reel-in speed toward the preferred length (units/s).")]
        [SerializeField, Min(0f)] private float _grappleReelSpeed = 9f;

        [Tooltip("Tangential push from holding left/right while swinging (units/s^2). This is how the player pumps the swing.")]
        [SerializeField, Min(0f)] private float _swingPumpAcceleration = 24f;

        [Tooltip("Swing speed cap (units/s). Keeps the pendulum from gaining runaway energy.")]
        [SerializeField, Min(0f)] private float _swingMaxSpeed = 17f;

        [Tooltip("Fraction of swing speed lost per second (air drag). Small values keep momentum.")]
        [SerializeField, Range(0f, 2f)] private float _swingDamping = 0.05f;

        [Tooltip("Upward speed added when releasing the swing with Jump (units/s).")]
        [SerializeField, Min(0f)] private float _swingReleaseJumpBoost = 6f;

        [Tooltip("Multiplier on the horizontal swing speed carried out of a release.")]
        [SerializeField, Min(0f)] private float _swingReleaseCarry = 1.1f;

        [Tooltip("Seconds before the anchor just released can be grabbed again (no hanging off one anchor forever).")]
        [SerializeField, Min(0f)] private float _grappleRegrabDelay = 0.4f;

        [Header("Chain")]
        [Tooltip("Hook flight speed (units/s). The player keeps full control while it flies.")]
        [SerializeField, Min(1f)] private float _chainSpeed = 55f;

        [Tooltip("Damage of the hook itself on an enemy.")]
        [SerializeField, Min(0)] private int _chainDamage = 4;

        [Tooltip("Hitstun frames on a yanked enemy: the window to start a combo as it arrives.")]
        [SerializeField, Min(0)] private int _chainStunFrames = 45;

        [Tooltip("How far in front of the player a yanked enemy lands (units).")]
        [SerializeField, Min(0.5f)] private float _chainYankDistance = 1.1f;

        [Tooltip("Hitstop on a hook landing in an enemy (frames).")]
        [SerializeField, Min(0)] private int _chainHitStopFrames = 4;

        [Tooltip("Speed of a zip to a heavy target (units/s).")]
        [SerializeField, Min(1f)] private float _zipSpeed = 24f;

        [Tooltip("A zip stops this far from the target's hook point (units).")]
        [SerializeField, Min(0.2f)] private float _zipArriveDistance = 1f;

        [Tooltip("A zip never lasts longer than this (seconds).")]
        [SerializeField, Min(0.05f)] private float _zipMaxSeconds = 0.5f;

        [Header("Attack Mobility")]
        [Tooltip("Fraction of run speed you can steer with while swinging on the ground. Attacks never root you.")]
        [SerializeField, Range(0f, 1f)] private float _attackMoveFraction = 0.45f;

        [Tooltip("Frames after the active frames before moving cancels the recovery on the ground (in the air: none).")]
        [SerializeField, Min(0)] private int _attackMoveCancelFrames = 4;

        [Tooltip("Upward speed from a down slash that hits something (units/s): the pogo.")]
        [SerializeField, Min(0f)] private float _pogoVelocity = 11f;

        [Header("Hit Reaction")]
        [Tooltip("Horizontal knockback deceleration during hitstun, in units/s^2.")]
        [SerializeField, Min(0f)] private float _knockbackDeceleration = 30f;

        [Header("Parry")]
        [Tooltip("Frames after pressing Parry during which an incoming hit is deflected.")]
        [SerializeField, Min(1)] private int _parryActiveFrames = 10;

        [Tooltip("Frames stuck after a parry that caught nothing (the risk).")]
        [SerializeField, Min(0)] private int _parryWhiffRecoveryFrames = 18;

        [Tooltip("Frames the parried attacker is stunned.")]
        [SerializeField, Min(0)] private int _parryStaggerFrames = 45;

        [Tooltip("Frames of invulnerability after a successful parry; any action cancels them (the counter).")]
        [SerializeField, Min(0)] private int _parrySuccessFrames = 24;

        [Tooltip("Hitstop on a successful parry, in frames.")]
        [SerializeField, Min(0)] private int _parryHitStopFrames = 12;

        [Tooltip("Screen shake trauma on a successful parry (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _parryScreenShake = 0.4f;

        // ============ Properties ============

        /// <summary>Top run speed (units/s).</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Z speed multiplier.</summary>
        public float DepthSpeedMultiplier => _depthSpeedMultiplier;

        /// <summary>Acceleration (units/s^2).</summary>
        public float Acceleration => _acceleration;

        /// <summary>Deceleration (units/s^2).</summary>
        public float Deceleration => _deceleration;

        /// <summary>Input deadzone (magnitude).</summary>
        public float InputDeadzone => _inputDeadzone;

        /// <summary>Gravity (units/s^2).</summary>
        public float Gravity => _gravity;

        /// <summary>Dash speed (units/s).</summary>
        public float DashSpeed => _dashSpeed;

        /// <summary>Dash duration (frames).</summary>
        public int DashFrames => _dashFrames;

        /// <summary>I-frame start (inclusive frame).</summary>
        public int DashIFrameStart => _dashIFrameStart;

        /// <summary>I-frame end (exclusive frame).</summary>
        public int DashIFrameEnd => _dashIFrameEnd;

        /// <summary>Knockback deceleration during hitstun (units/s^2).</summary>
        public float KnockbackDeceleration => _knockbackDeceleration;

        /// <summary>Parry deflect window (frames).</summary>
        public int ParryActiveFrames => _parryActiveFrames;

        /// <summary>Recovery after a whiffed parry (frames).</summary>
        public int ParryWhiffRecoveryFrames => _parryWhiffRecoveryFrames;

        /// <summary>Attacker stun on a parry (frames).</summary>
        public int ParryStaggerFrames => _parryStaggerFrames;

        /// <summary>Invulnerable counter window after a parry (frames).</summary>
        public int ParrySuccessFrames => _parrySuccessFrames;

        /// <summary>Parry hitstop (frames).</summary>
        public int ParryHitStopFrames => _parryHitStopFrames;

        /// <summary>Parry screen shake (0..1).</summary>
        public float ParryScreenShake => _parryScreenShake;

        /// <summary>Roll top speed (units/s).</summary>
        public float RollSpeed => _rollSpeed;

        /// <summary>Roll duration (frames).</summary>
        public int RollFrames => _rollFrames;

        /// <summary>Roll progress where easing starts (0..1).</summary>
        public float RollEaseStart => _rollEaseStart;

        /// <summary>Speed fraction at the end of the roll.</summary>
        public float RollEndSpeedFraction => _rollEndSpeedFraction;

        /// <summary>Roll i-frame start (inclusive frame).</summary>
        public int RollIFrameStart => _rollIFrameStart;

        /// <summary>Roll i-frame end (exclusive frame).</summary>
        public int RollIFrameEnd => _rollIFrameEnd;

        /// <summary>Roll frame from which Jump/Attack can cancel the roll.</summary>
        public int RollCancelFrame => _rollCancelFrame;

        /// <summary>Jump launch speed (units/s).</summary>
        public float JumpVelocity => _jumpVelocity;

        /// <summary>Gravity multiplier while falling.</summary>
        public float FallGravityMultiplier => _fallGravityMultiplier;
        /// <summary>Gravity multiplier while rising after Jump is released.</summary>
        public float ReleaseGravityMultiplier => _releaseGravityMultiplier;

        /// <summary>|Vertical speed| under which the jump counts as at its apex.</summary>
        public float ApexHangThreshold => _apexHangThreshold;

        /// <summary>Gravity multiplier at the apex while Jump is held.</summary>
        public float ApexGravityMultiplier => _apexGravityMultiplier;

        /// <summary>Terminal fall speed (units/s).</summary>
        public float MaxFallSpeed => _maxFallSpeed;

        /// <summary>Air acceleration (units/s^2).</summary>
        public float AirAcceleration => _airAcceleration;

        /// <summary>Coyote time (seconds).</summary>
        public float CoyoteTime => _coyoteTime;

        /// <summary>Air dashes per airtime.</summary>
        public int AirDashes => _airDashes;

        /// <summary>Air jumps per airtime (1 = double jump).</summary>
        public int AirJumps => _airJumps;

        /// <summary>Double jump launch speed (units/s).</summary>
        public float DoubleJumpVelocity => _doubleJumpVelocity;

        /// <summary>Climbable wall layers.</summary>
        public LayerMask WallLayers => _wallLayers;

        /// <summary>Wall probe reach beyond the capsule (units).</summary>
        public float WallCheckDistance => _wallCheckDistance;

        /// <summary>Hang frames before sliding.</summary>
        public int WallGrabFrames => _wallGrabFrames;

        /// <summary>Wall slide speed (units/s).</summary>
        public float WallSlideSpeed => _wallSlideSpeed;

        /// <summary>Wall jump upward speed (units/s).</summary>
        public float WallJumpVelocity => _wallJumpVelocity;

        /// <summary>Wall jump horizontal push (units/s).</summary>
        public float WallJumpPush => _wallJumpPush;

        /// <summary>Air-control lock after a wall jump (frames).</summary>
        public int WallJumpLockFrames => _wallJumpLockFrames;

        /// <summary>Air control fraction kept right after a wall jump.</summary>
        public float WallJumpControl => _wallJumpControl;

        // ============ Validation ============


        /// <summary>Hook flight speed (units/s).</summary>
        public float ChainSpeed => _chainSpeed;

        /// <summary>Damage of the hook on an enemy.</summary>
        public int ChainDamage => _chainDamage;

        /// <summary>Hitstun frames on a yanked enemy.</summary>
        public int ChainStunFrames => _chainStunFrames;

        /// <summary>Distance in front of the player a yanked enemy lands.</summary>
        public float ChainYankDistance => _chainYankDistance;

        /// <summary>Hitstop on a hook landing in an enemy (frames).</summary>
        public int ChainHitStopFrames => _chainHitStopFrames;

        /// <summary>Zip speed (units/s).</summary>
        public float ZipSpeed => _zipSpeed;

        /// <summary>Zip stop distance from the target (units).</summary>
        public float ZipArriveDistance => _zipArriveDistance;

        /// <summary>Longest zip (seconds).</summary>
        public float ZipMaxSeconds => _zipMaxSeconds;

        /// <summary>Fraction of run speed available for steering during a ground attack.</summary>
        public float AttackMoveFraction => _attackMoveFraction;

        /// <summary>Frames after the active frames before moving cancels a ground attack's recovery.</summary>
        public int AttackMoveCancelFrames => _attackMoveCancelFrames;

        /// <summary>Bounce speed off a down slash hit (units/s).</summary>
        public float PogoVelocity => _pogoVelocity;

        /// <summary>Max distance to a grapple anchor (units).</summary>
        public float GrappleRange => _grappleRange;

        /// <summary>Hook flight time before the rope goes taut (seconds).</summary>
        public float GrappleThrowTime => _grappleThrowTime;

        /// <summary>Shortest rope (units).</summary>
        public float GrappleMinLength => _grappleMinLength;

        /// <summary>Longest rope (units).</summary>
        public float GrappleMaxLength => _grappleMaxLength;

        /// <summary>Rope length reeled toward while swinging (units).</summary>
        public float GrapplePreferredLength => _grapplePreferredLength;

        /// <summary>Reel speed (units/s).</summary>
        public float GrappleReelSpeed => _grappleReelSpeed;

        /// <summary>Tangential pump acceleration from stick input (units/s^2).</summary>
        public float SwingPumpAcceleration => _swingPumpAcceleration;

        /// <summary>Swing speed cap (units/s).</summary>
        public float SwingMaxSpeed => _swingMaxSpeed;

        /// <summary>Fraction of swing speed lost per second.</summary>
        public float SwingDamping => _swingDamping;

        /// <summary>Upward speed added on a jump release (units/s).</summary>
        public float SwingReleaseJumpBoost => _swingReleaseJumpBoost;

        /// <summary>Multiplier on horizontal speed carried out of a release.</summary>
        public float SwingReleaseCarry => _swingReleaseCarry;

        /// <summary>Seconds before a released anchor can be grabbed again.</summary>
        public float GrappleRegrabDelay => _grappleRegrabDelay;

        private void OnValidate()
        {
            _dashIFrameStart = Mathf.Clamp(_dashIFrameStart, 0, _dashFrames);
            _dashIFrameEnd = Mathf.Clamp(_dashIFrameEnd, _dashIFrameStart, _dashFrames);
            _rollIFrameStart = Mathf.Clamp(_rollIFrameStart, 0, _rollFrames);
            _rollIFrameEnd = Mathf.Clamp(_rollIFrameEnd, _rollIFrameStart, _rollFrames);
            _rollCancelFrame = Mathf.Clamp(_rollCancelFrame, 0, _rollFrames);
            _grappleMaxLength = Mathf.Max(_grappleMaxLength, _grappleMinLength);
            _grapplePreferredLength = Mathf.Clamp(_grapplePreferredLength, _grappleMinLength, _grappleMaxLength);
        }
    }
}
