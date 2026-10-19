using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>Game-feel tuning shared by HitStop, ScreenShake and DamageNumberSpawner.</summary>
    [CreateAssetMenu(menuName = "Brawler/Presentation/Feedback Settings", fileName = "FeedbackSettings")]
    public sealed class FeedbackSettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Hit Stop")]
        [Tooltip("Time.timeScale while hitstop is active. 0 = full freeze (FixedUpdate stops, so frame timelines pause too).")]
        [SerializeField, Range(0f, 1f)] private float _hitStopTimeScale = 0f;

        [Tooltip("Upper bound on a single hitstop, in real (unscaled) seconds. Overlapping hitstops take the max remaining, never sum past this.")]
        [SerializeField, Min(0f)] private float _maxHitStopSeconds = 0.25f;

        [Tooltip("Real seconds of slow motion after a parry's freeze, easing back to full speed. 0 = snap straight back.")]
        [SerializeField, Min(0f)] private float _parrySlowSeconds = 0.2f;

        [Tooltip("Time.timeScale at the start of the parry slow-motion tail.")]
        [SerializeField, Range(0.05f, 1f)] private float _parrySlowTimeScale = 0.3f;

        [Header("Screen Shake")]
        [Tooltip("Max positional offset in units at trauma 1.")]
        [SerializeField, Min(0f)] private float _shakeMaxOffset = 0.35f;

        [Tooltip("Perlin noise frequency (Hz-ish). Higher = more jittery.")]
        [SerializeField, Min(0f)] private float _shakeFrequency = 30f;

        [Tooltip("Trauma lost per real second. Shake runs on unscaled time so it plays through hitstop.")]
        [SerializeField, Min(0f)] private float _traumaDecayPerSecond = 3f;

        [Header("Damage Numbers")]
        [Tooltip("Seconds a damage number is visible before returning to the pool (unscaled).")]
        [SerializeField, Min(0.05f)] private float _damageNumberLifetime = 0.7f;

        [Tooltip("Units a damage number rises over its lifetime.")]
        [SerializeField] private float _damageNumberRise = 1.2f;

        [Tooltip("Offset from the hit point where numbers spawn.")]
        [SerializeField] private Vector3 _damageNumberSpawnOffset = new Vector3(0f, 0.5f, 0f);

        [Tooltip("Random horizontal spread (units) so stacked hits don't overlap exactly.")]
        [SerializeField, Min(0f)] private float _damageNumberSpread = 0.25f;

        [Tooltip("Max random vertical offset (units) so consecutive numbers don't stack on one line.")]
        [SerializeField, Min(0f)] private float _damageNumberHeightSpread = 0.25f;

        [Tooltip("Size multiplier on the damage-number prefab.")]
        [SerializeField, Min(0.05f)] private float _damageNumberScale = 0.55f;

        [Tooltip("ObjectPool default capacity (prewarm size).")]
        [SerializeField, Min(1)] private int _damageNumberPoolCapacity = 16;

        [Tooltip("ObjectPool max size; extra released numbers are destroyed.")]
        [SerializeField, Min(1)] private int _damageNumberPoolMaxSize = 64;

        // ============ Properties ============

        /// <summary>Time scale during hitstop.</summary>
        public float HitStopTimeScale => _hitStopTimeScale;

        /// <summary>Max single hitstop (unscaled seconds).</summary>
        public float MaxHitStopSeconds => _maxHitStopSeconds;

        /// <summary>Max shake offset at trauma 1.</summary>
        public float ShakeMaxOffset => _shakeMaxOffset;

        /// <summary>Parry slow-motion tail length (unscaled seconds).</summary>
        public float ParrySlowSeconds => _parrySlowSeconds;

        /// <summary>Time scale at the start of the parry slow-motion tail.</summary>
        public float ParrySlowTimeScale => _parrySlowTimeScale;

        /// <summary>Shake noise frequency.</summary>
        public float ShakeFrequency => _shakeFrequency;

        /// <summary>Trauma decay per unscaled second.</summary>
        public float TraumaDecayPerSecond => _traumaDecayPerSecond;

        /// <summary>Damage number lifetime (unscaled seconds).</summary>
        public float DamageNumberLifetime => _damageNumberLifetime;

        /// <summary>Damage number rise distance.</summary>
        public float DamageNumberRise => _damageNumberRise;

        /// <summary>Damage number spawn offset.</summary>
        public Vector3 DamageNumberSpawnOffset => _damageNumberSpawnOffset;

        /// <summary>Damage number random spread.</summary>
        public float DamageNumberSpread => _damageNumberSpread;

        /// <summary>Max random vertical offset for damage numbers (units).</summary>
        public float DamageNumberHeightSpread => _damageNumberHeightSpread;

        /// <summary>Damage-number size multiplier.</summary>
        public float DamageNumberScale => _damageNumberScale;

        /// <summary>Damage number pool default capacity.</summary>
        public int DamageNumberPoolCapacity => _damageNumberPoolCapacity;

        /// <summary>Damage number pool max size.</summary>
        public int DamageNumberPoolMaxSize => _damageNumberPoolMaxSize;
    }
}
