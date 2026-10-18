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
        [SerializeField, Min(0f)] private float _acceleration = 80f;

        [Tooltip("Deceleration to rest when input is released, in units/s^2.")]
        [SerializeField, Min(0f)] private float _deceleration = 100f;

        [Tooltip("Stick/keyboard magnitude below which input counts as neutral.")]
        [SerializeField, Range(0f, 0.9f)] private float _inputDeadzone = 0.15f;

        [Tooltip("Gravity in units/s^2 (negative = down). Keeps the CharacterController grounded.")]
        [SerializeField] private float _gravity = -30f;

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

        [Header("Jump")]
        [Tooltip("Upward launch speed in units/s. Apex height = v^2 / (2 * |Gravity|).")]
        [SerializeField, Min(0f)] private float _jumpVelocity = 12f;

        [Tooltip("Gravity multiplier while falling. > 1 = snappier, less floaty arcs.")]
        [SerializeField, Min(1f)] private float _fallGravityMultiplier = 1.7f;

        [Tooltip("Upward velocity is multiplied by this when Jump is released while rising (variable jump height).")]
        [SerializeField, Range(0f, 1f)] private float _jumpCutMultiplier = 0.45f;

        [Tooltip("Terminal fall speed in units/s.")]
        [SerializeField, Min(0f)] private float _maxFallSpeed = 22f;

        [Tooltip("Planar acceleration toward input while airborne, units/s^2.")]
        [SerializeField, Min(0f)] private float _airAcceleration = 45f;

        [Tooltip("Seconds after walking off a ledge during which Jump still works.")]
        [SerializeField, Min(0f)] private float _coyoteTime = 0.1f;

        [Tooltip("Dashes allowed per airtime (refilled on landing or touching a wall).")]
        [SerializeField, Min(0)] private int _airDashes = 1;

        [Tooltip("Extra jumps allowed in the air (1 = double jump). Refilled on landing or touching a wall.")]
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
        [SerializeField, Min(0)] private int _wallJumpLockFrames = 10;

        [Header("Hit Reaction")]
        [Tooltip("Horizontal knockback deceleration during hitstun, in units/s^2.")]
        [SerializeField, Min(0f)] private float _knockbackDeceleration = 30f;

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

        /// <summary>Jump launch speed (units/s).</summary>
        public float JumpVelocity => _jumpVelocity;

        /// <summary>Gravity multiplier while falling.</summary>
        public float FallGravityMultiplier => _fallGravityMultiplier;

        /// <summary>Upward velocity multiplier when Jump is released early.</summary>
        public float JumpCutMultiplier => _jumpCutMultiplier;

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

        // ============ Validation ============

        private void OnValidate()
        {
            _dashIFrameStart = Mathf.Clamp(_dashIFrameStart, 0, _dashFrames);
            _dashIFrameEnd = Mathf.Clamp(_dashIFrameEnd, _dashIFrameStart, _dashFrames);
            _rollIFrameStart = Mathf.Clamp(_rollIFrameStart, 0, _rollFrames);
            _rollIFrameEnd = Mathf.Clamp(_rollIFrameEnd, _rollIFrameStart, _rollFrames);
        }
    }
}
