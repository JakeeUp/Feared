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
        [Tooltip("Dash speed in units/s along input direction (or facing if neutral).")]
        [SerializeField, Min(0f)] private float _dashSpeed = 16f;

        [Tooltip("Total dash duration in 60 Hz frames.")]
        [SerializeField, Min(1)] private int _dashFrames = 12;

        [Tooltip("First dash frame (inclusive) with invulnerability.")]
        [SerializeField, Min(0)] private int _dashIFrameStart = 0;

        [Tooltip("Dash frame (exclusive) where invulnerability ends. Must be <= Dash Frames.")]
        [SerializeField, Min(0)] private int _dashIFrameEnd = 8;

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

        // ============ Validation ============

        private void OnValidate()
        {
            // TODO: clamp the i-frame end into [start, dash length] so invulnerability can't outlast the dash.
        }
    }
}
