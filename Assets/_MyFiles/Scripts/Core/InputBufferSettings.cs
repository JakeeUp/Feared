using UnityEngine;

namespace Brawler.Core
{
    /// <summary>Tuning for <c>InputBuffer</c>: how long a pressed command stays consumable.</summary>
    [CreateAssetMenu(menuName = "Brawler/Core/Input Buffer Settings", fileName = "InputBufferSettings")]
    public sealed class InputBufferSettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Buffer")]
        [Tooltip("Seconds a buffered command stays live after being pressed. ~0.2s (12 frames) feels responsive without misfires. Uses scaled time, so inputs pressed during hitstop survive the freeze.")]
        [SerializeField, Min(0f)] private float _bufferWindowSeconds = 0.2f;

        [Tooltip("Maximum number of stored commands. When full, the oldest entry is overwritten. Fixed at construction (no runtime allocation).")]
        [SerializeField, Min(1)] private int _capacity = 8;

        // ============ Properties ============

        /// <summary>Seconds a buffered command stays live.</summary>
        public float BufferWindowSeconds => _bufferWindowSeconds;

        /// <summary>Fixed ring-buffer capacity.</summary>
        public int Capacity => _capacity;
    }
}
