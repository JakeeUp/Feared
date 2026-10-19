using System;
using Brawler.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Presentation
{
    /// <summary>
    /// Shop column of the pause menu: one button per ability, bought with score from the <see cref="ScoreWallet"/>.
    /// Buttons show name, description and price, then OWNED; they're disabled when you can't afford them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityShop : MonoBehaviour
    {
        // ============ Types ============

#pragma warning disable CS0649 // fields assigned by Unity serialization
        /// <summary>One shop row.</summary>
        [Serializable]
        private struct Entry
        {
            public AbilityType Type;
            public Button Button;
            public Text Label;
        }
#pragma warning restore CS0649

        // ============ Serialized Fields ============

        [SerializeField] private AbilityLoadout _loadout;
        [SerializeField] private Text _balanceText;
        [SerializeField] private Entry[] _entries = new Entry[0];

        // ============ State ============

        private Action _onChanged;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onChanged = Refresh;
            foreach (Entry entry in _entries)
            {
                if (entry.Button != null)
                {
                    AbilityType type = entry.Type; // captured once here, not per click
                    entry.Button.onClick.AddListener(() => Buy(type));
                }
            }
        }

        private void OnEnable()
        {
            if (_loadout != null)
            {
                _loadout.Changed += _onChanged;
                if (_loadout.Wallet != null)
                {
                    _loadout.Wallet.Changed += _onChanged;
                }
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_loadout != null)
            {
                _loadout.Changed -= _onChanged;
                if (_loadout.Wallet != null)
                {
                    _loadout.Wallet.Changed -= _onChanged;
                }
            }
        }

        // ============ Helpers ============

        private void Buy(AbilityType type)
        {
            _loadout?.TryBuy(type);
        }

        private void Refresh()
        {
            if (_loadout == null)
            {
                return;
            }

            if (_balanceText != null && _loadout.Wallet != null)
            {
                _balanceText.text = $"SCORE  {_loadout.Wallet.Balance:N0}";
            }

            foreach (Entry entry in _entries)
            {
                AbilityDefinition ability = _loadout.Get(entry.Type);
                if (ability == null)
                {
                    continue;
                }

                bool owned = _loadout.IsUnlocked(entry.Type);
                if (entry.Label != null)
                {
                    string price = owned ? "OWNED" : $"{ability.Cost:N0}";
                    entry.Label.text = $"{ability.DisplayName.ToUpperInvariant()}   {price}\n{ability.Description}";
                }

                if (entry.Button != null)
                {
                    entry.Button.interactable = _loadout.CanBuy(entry.Type);
                }
            }
        }
    }
}
