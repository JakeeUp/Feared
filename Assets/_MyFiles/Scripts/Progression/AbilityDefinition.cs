using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>The player's special abilities.</summary>
    public enum AbilityType
    {
        /// <summary>Swords orbit the player, hitting and knocking back anything they touch.</summary>
        BladeRing,

        /// <summary>A bubble that blocks all damage while it lasts.</summary>
        BubbleShield,

        /// <summary>Instantly restores a share of max health.</summary>
        Mend,
    }

    /// <summary>Shop + tuning data for one ability: name, price, cooldown, duration and its main number.</summary>
    [CreateAssetMenu(menuName = "Brawler/Progression/Ability Definition", fileName = "Ability_New")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        // ============ Serialized Fields ============

        [SerializeField] private AbilityType _type;

        [SerializeField] private string _displayName = "Ability";

        [Tooltip("One line for the shop.")]
        [SerializeField] private string _description = "";

        [Tooltip("Score needed to unlock it in the shop.")]
        [SerializeField, Min(0)] private int _cost = 300;

        [Tooltip("Seconds before it can be used again (counted from activation).")]
        [SerializeField, Min(0f)] private float _cooldown = 12f;

        [Tooltip("Seconds it stays active (0 = instant).")]
        [SerializeField, Min(0f)] private float _duration;

        [Tooltip("Main effect number: Mend = fraction of max health healed (0.35 = 35%). Unused by the others.")]
        [SerializeField, Min(0f)] private float _value;

        // ============ Properties ============

        /// <summary>Which ability this is.</summary>
        public AbilityType Type => _type;

        /// <summary>Shop name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Shop description.</summary>
        public string Description => _description;

        /// <summary>Unlock price (score).</summary>
        public int Cost => _cost;

        /// <summary>Cooldown (seconds).</summary>
        public float Cooldown => _cooldown;

        /// <summary>Active time (seconds, 0 = instant).</summary>
        public float Duration => _duration;

        /// <summary>Main effect number (see tooltip).</summary>
        public float Value => _value;

#if UNITY_EDITOR
        /// <summary>Editor tooling: fills the definition in one call.</summary>
        public void EditorSet(AbilityType type, string displayName, string description, int cost, float cooldown, float duration, float value)
        {
            _type = type;
            _displayName = displayName;
            _description = description;
            _cost = cost;
            _cooldown = cooldown;
            _duration = duration;
            _value = value;
        }
#endif
    }
}
