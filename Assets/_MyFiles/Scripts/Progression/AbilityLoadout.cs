using System;
using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>
    /// Which abilities the player owns and when each was last used. The shop unlocks them (paying from the
    /// <see cref="ScoreWallet"/>), the player's AbilityCaster asks <see cref="TryActivate"/>, and the HUD reads
    /// cooldown/active progress. Times are scaled game time (cooldowns pause with the game).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityLoadout : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Number of <see cref="AbilityType"/> values (array sizes below).</summary>
        private const int AbilityTypeCount = 3;

        // ============ Serialized Fields ============

        [Tooltip("Every ability the shop offers, one per AbilityType.")]
        [SerializeField] private AbilityDefinition[] _abilities = new AbilityDefinition[0];

        [Tooltip("Pays for unlocks.")]
        [SerializeField] private ScoreWallet _wallet;

        // ============ State ============

        private readonly bool[] _unlocked = new bool[AbilityTypeCount];
        private readonly float[] _lastUsed = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

        // ============ Events & Properties ============

        /// <summary>Raised when an ability is unlocked.</summary>
        public event Action Changed;

        /// <summary>The wallet that pays for unlocks.</summary>
        public ScoreWallet Wallet => _wallet;

        // ============ Queries ============

        /// <summary>The definition for <paramref name="type"/>, or null if none is assigned.</summary>
        public AbilityDefinition Get(AbilityType type)
        {
            foreach (AbilityDefinition ability in _abilities)
            {
                if (ability != null && ability.Type == type)
                {
                    return ability;
                }
            }

            return null;
        }

        /// <summary>True once bought.</summary>
        public bool IsUnlocked(AbilityType type)
        {
            return _unlocked[(int)type];
        }

        /// <summary>True while a duration ability (ring, shield) is running.</summary>
        public bool IsActive(AbilityType type, float now)
        {
            AbilityDefinition ability = Get(type);
            return ability != null && now < _lastUsed[(int)type] + ability.Duration;
        }

        /// <summary>Remaining cooldown as 1 (just used) .. 0 (ready).</summary>
        public float CooldownRemaining01(AbilityType type, float now)
        {
            AbilityDefinition ability = Get(type);
            if (ability == null || ability.Cooldown <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((_lastUsed[(int)type] + ability.Cooldown - now) / ability.Cooldown);
        }

        /// <summary>True if owned and off cooldown.</summary>
        public bool IsReady(AbilityType type, float now)
        {
            return IsUnlocked(type) && CooldownRemaining01(type, now) <= 0f;
        }

        // ============ API ============

        /// <summary>True if the ability exists, isn't owned yet, and the wallet can pay.</summary>
        public bool CanBuy(AbilityType type)
        {
            AbilityDefinition ability = Get(type);
            return ability != null && !IsUnlocked(type) && _wallet != null && _wallet.CanAfford(ability.Cost);
        }

        /// <summary>Buys the ability.</summary>
        /// <returns>True if it was unlocked.</returns>
        public bool TryBuy(AbilityType type)
        {
            if (!CanBuy(type) || !_wallet.TrySpend(Get(type).Cost))
            {
                return false;
            }

            _unlocked[(int)type] = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Starts the ability's cooldown (and duration) if it is ready.</summary>
        /// <returns>True if the caller should apply the effect now.</returns>
        public bool TryActivate(AbilityType type, float now)
        {
            if (!IsReady(type, now))
            {
                return false;
            }

            _lastUsed[(int)type] = now;
            return true;
        }
    }
}
