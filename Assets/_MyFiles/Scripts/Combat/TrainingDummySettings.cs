using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>Tuning for <c>TrainingDummy</c>.</summary>
    [CreateAssetMenu(menuName = "Brawler/Combat/Training Dummy Settings", fileName = "TrainingDummySettings")]
    public sealed class TrainingDummySettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Health")]
        [Tooltip("Health the dummy resets to. The dummy never dies; at 0 it clamps and waits for reset.")]
        [SerializeField, Min(1)] private int _maxHealth = 100;

        [Tooltip("Seconds without being hit before health resets to max.")]
        [SerializeField, Min(0f)] private float _resetDelaySeconds = 2f;

        [Header("Knockback")]
        [Tooltip("Multiplier applied to incoming knockback velocity (0 = immovable).")]
        [SerializeField, Min(0f)] private float _knockbackScale = 1f;

        [Tooltip("Horizontal knockback deceleration in units/s^2.")]
        [SerializeField, Min(0f)] private float _knockbackDeceleration = 25f;

        [Tooltip("Gravity in units/s^2 (negative = down).")]
        [SerializeField] private float _gravity = -30f;

        // ============ Properties ============

        /// <summary>Max health.</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>Seconds before health resets.</summary>
        public float ResetDelaySeconds => _resetDelaySeconds;

        /// <summary>Knockback multiplier.</summary>
        public float KnockbackScale => _knockbackScale;

        /// <summary>Knockback deceleration.</summary>
        public float KnockbackDeceleration => _knockbackDeceleration;

        /// <summary>Gravity.</summary>
        public float Gravity => _gravity;
    }
}
