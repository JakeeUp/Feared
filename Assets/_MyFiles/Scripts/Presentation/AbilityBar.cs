using System;
using Brawler.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Presentation
{
    /// <summary>
    /// Ability slots on the HUD: each shows its icon, the key that fires it, a radial cooldown sweep, a glow while the
    /// ability is running, and a dimmed lock until it's bought.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityBar : MonoBehaviour
    {
        // ============ Types ============

#pragma warning disable CS0649 // fields assigned by Unity serialization
        /// <summary>One slot.</summary>
        [Serializable]
        private struct Slot
        {
            public AbilityType Type;
            public Image Icon;

            [Tooltip("Radial Filled image over the icon; its fill is the remaining cooldown.")]
            public Image Cooldown;

            [Tooltip("Frame tinted while the ability is active.")]
            public Image Frame;
        }
#pragma warning restore CS0649

        // ============ Serialized Fields ============

        [SerializeField] private AbilityLoadout _loadout;
        [SerializeField] private Slot[] _slots = new Slot[0];
        [SerializeField] private Color _lockedTint = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Color _frameColor = new Color(0.04f, 0.04f, 0.06f, 0.9f);
        [SerializeField] private Color _activeFrameColor = new Color(1f, 0.85f, 0.35f, 1f);

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            if (_loadout == null)
            {
                return;
            }

            float now = Time.time;
            foreach (Slot slot in _slots)
            {
                bool unlocked = _loadout.IsUnlocked(slot.Type);
                if (slot.Icon != null)
                {
                    slot.Icon.color = unlocked ? Color.white : _lockedTint;
                }

                if (slot.Cooldown != null)
                {
                    slot.Cooldown.fillAmount = unlocked ? _loadout.CooldownRemaining01(slot.Type, now) : 0f;
                }

                if (slot.Frame != null)
                {
                    slot.Frame.color = _loadout.IsActive(slot.Type, now) ? _activeFrameColor : _frameColor;
                }
            }
        }
    }
}
