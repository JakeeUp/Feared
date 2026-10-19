using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>Levelling curve, upgrade sizes and XP orb behaviour.</summary>
    [CreateAssetMenu(menuName = "Brawler/Progression/Progression Settings", fileName = "ProgressionSettings")]
    public sealed class ProgressionSettings : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Levelling")]
        [Tooltip("XP from level 1 to 2.")]
        [SerializeField, Min(1)] private int _baseXpPerLevel = 40;

        [Tooltip("Each level needs this much more XP than the last (1.35 = +35%).")]
        [SerializeField, Min(1f)] private float _xpGrowth = 1.35f;

        [Tooltip("Upgrade points granted per level.")]
        [SerializeField, Min(0)] private int _pointsPerLevel = 1;

        [Header("Upgrades")]
        [Tooltip("Max health added per Vitality rank.")]
        [SerializeField, Min(0)] private int _healthPerRank = 20;

        [Tooltip("Damage multiplier added per Strength rank (0.15 = +15%).")]
        [SerializeField, Min(0f)] private float _damagePerRank = 0.15f;

        [Tooltip("Highest rank of each upgrade.")]
        [SerializeField, Min(1)] private int _maxRank = 10;

        [Header("XP Orbs")]
        [Tooltip("XP per orb; a drop splits the enemy's XP into orbs of this size.")]
        [SerializeField, Min(1)] private int _xpPerOrb = 5;

        [Tooltip("Most orbs a single drop can make (excess XP goes into the last orbs).")]
        [SerializeField, Min(1)] private int _maxOrbsPerDrop = 6;

        [Tooltip("Seconds an orb tumbles before it can be collected.")]
        [SerializeField, Min(0f)] private float _collectDelay = 0.4f;

        [Tooltip("Orbs fly to the player when within this distance (units).")]
        [SerializeField, Min(0f)] private float _magnetRadius = 2.5f;

        [Tooltip("Orbs fly to the player regardless of distance after this many seconds.")]
        [SerializeField, Min(0f)] private float _autoCollectAfter = 3f;

        [Tooltip("Top homing speed (units/s).")]
        [SerializeField, Min(0f)] private float _orbSpeed = 10f;

        [Tooltip("Initial burst speed when dropped (units/s).")]
        [SerializeField, Min(0f)] private float _orbPopSpeed = 3f;

        // ============ Properties ============

        /// <summary>XP from level 1 to 2.</summary>
        public int BaseXpPerLevel => _baseXpPerLevel;

        /// <summary>XP growth per level.</summary>
        public float XpGrowth => _xpGrowth;

        /// <summary>Points per level.</summary>
        public int PointsPerLevel => _pointsPerLevel;

        /// <summary>Max health per Vitality rank.</summary>
        public int HealthPerRank => _healthPerRank;

        /// <summary>Damage multiplier per Strength rank.</summary>
        public float DamagePerRank => _damagePerRank;

        /// <summary>Max rank per upgrade.</summary>
        public int MaxRank => _maxRank;

        /// <summary>XP per orb.</summary>
        public int XpPerOrb => _xpPerOrb;

        /// <summary>Max orbs per drop.</summary>
        public int MaxOrbsPerDrop => _maxOrbsPerDrop;

        /// <summary>Seconds before an orb can be collected.</summary>
        public float CollectDelay => _collectDelay;

        /// <summary>Magnet radius (units).</summary>
        public float MagnetRadius => _magnetRadius;

        /// <summary>Seconds after which orbs home in from anywhere.</summary>
        public float AutoCollectAfter => _autoCollectAfter;

        /// <summary>Homing speed (units/s).</summary>
        public float OrbSpeed => _orbSpeed;

        /// <summary>Drop burst speed (units/s).</summary>
        public float OrbPopSpeed => _orbPopSpeed;

        /// <summary>XP needed to advance from <paramref name="level"/>.</summary>
        public int XpToNext(int level)
        {
            return LevelCurve.XpToNext(level, _baseXpPerLevel, _xpGrowth);
        }
    }
}
