using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>The upgrades the player can spend points on.</summary>
    public enum UpgradeType
    {
        /// <summary>+Max health.</summary>
        Vitality,

        /// <summary>+Damage multiplier.</summary>
        Strength,
    }

    /// <summary>
    /// Player experience, level and upgrades. Collects XP (from orbs), levels up along the
    /// <see cref="ProgressionSettings"/> curve granting points, and applies spent points to the player's
    /// <see cref="Health"/> (Vitality) and attack <see cref="Hitbox"/> (Strength). The HUD and upgrade menu read it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerProgression : MonoBehaviour, IXpCollector
    {
        // ============ Serialized Fields ============

        [SerializeField] private ProgressionSettings _settings;

        [Tooltip("Player health (Vitality raises its max).")]
        [SerializeField] private Health _health;

        [Tooltip("Player attack hitbox (Strength raises its damage multiplier).")]
        [SerializeField] private Hitbox _hitbox;

        // ============ State ============

        private int _level = 1;
        private int _xp;
        private int _points;
        private int _vitalityRank;
        private int _strengthRank;

        // ============ Events & Properties ============

        /// <summary>Raised whenever XP, level, points or ranks change.</summary>
        public event Action Changed;

        /// <summary>Raised on each level gained.</summary>
        public event Action<int> LeveledUp;

        /// <summary>Current level (starts at 1).</summary>
        public int Level => _level;

        /// <summary>XP into the current level.</summary>
        public int Xp => _xp;

        /// <summary>XP needed for the next level.</summary>
        public int XpToNext => _settings != null ? _settings.XpToNext(_level) : 1;

        /// <summary>Progress to the next level, 0..1.</summary>
        public float XpNormalized => Mathf.Clamp01(_xp / (float)XpToNext);

        /// <summary>Unspent upgrade points.</summary>
        public int Points => _points;

        /// <summary>Tuning.</summary>
        public ProgressionSettings Settings => _settings;

        // ============ Lifecycle ============

        private void Start()
        {
            ApplyStrength();
        }

        // ============ IXpCollector ============

        /// <inheritdoc />
        public void AddXp(int amount)
        {
            if (amount <= 0 || _settings == null)
            {
                return;
            }

            _xp += amount;
            while (_xp >= XpToNext)
            {
                _xp -= XpToNext;
                _level++;
                _points += _settings.PointsPerLevel;
                LeveledUp?.Invoke(_level);
            }

            Changed?.Invoke();
        }

        // ============ Upgrades ============

        /// <summary>Current rank of an upgrade.</summary>
        public int Rank(UpgradeType type)
        {
            return type == UpgradeType.Vitality ? _vitalityRank : _strengthRank;
        }

        /// <summary>True if a point is available and the upgrade isn't maxed.</summary>
        public bool CanUpgrade(UpgradeType type)
        {
            return _settings != null && _points > 0 && Rank(type) < _settings.MaxRank;
        }

        /// <summary>Spends a point on <paramref name="type"/>.</summary>
        /// <returns>True if the upgrade was applied.</returns>
        public bool TryUpgrade(UpgradeType type)
        {
            if (!CanUpgrade(type))
            {
                return false;
            }

            _points--;
            if (type == UpgradeType.Vitality)
            {
                _vitalityRank++;
                if (_health != null)
                {
                    _health.AddMax(_settings.HealthPerRank); // also heals by the same amount
                }
            }
            else
            {
                _strengthRank++;
                ApplyStrength();
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Total damage multiplier from Strength.</summary>
        public float DamageMultiplier => 1f + _strengthRank * (_settings != null ? _settings.DamagePerRank : 0f);

        // ============ Helpers ============

        private void ApplyStrength()
        {
            if (_hitbox != null)
            {
                _hitbox.DamageMultiplier = DamageMultiplier;
            }
        }
    }
}
