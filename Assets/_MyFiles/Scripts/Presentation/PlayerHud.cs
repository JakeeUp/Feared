using Brawler.Combat;
using Brawler.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Presentation
{
    /// <summary>
    /// Top-left HUD: health bar with a trailing "chip" that drains after a hit (so you can see how much you lost),
    /// XP bar, level badge, and an upgrade-point prompt. Reads <see cref="Health"/> and <see cref="PlayerProgression"/>
    /// each frame (a couple of floats, cheaper than wiring events). Runs on unscaled time so it animates during hitstop
    /// and while the upgrade menu has the game paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHud : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("Sources")]
        [SerializeField] private Health _health;
        [SerializeField] private PlayerProgression _progression;

        [Header("Health")]
        [Tooltip("Filled image showing current health.")]
        [SerializeField] private Image _healthFill;

        [Tooltip("Filled image behind the health fill that lags, then drains to it after damage.")]
        [SerializeField] private Image _healthChip;

        [Tooltip("Optional '75 / 100' readout.")]
        [SerializeField] private Text _healthText;

        [Tooltip("Seconds the chip waits before draining.")]
        [SerializeField, Min(0f)] private float _chipDelay = 0.45f;

        [Tooltip("Chip drain speed (fraction of the bar per second).")]
        [SerializeField, Min(0.01f)] private float _chipDrainSpeed = 0.8f;

        [Header("Experience")]
        [SerializeField] private Image _xpFill;
        [SerializeField] private Text _levelText;

        [Tooltip("Shown while upgrade points are unspent.")]
        [SerializeField] private Text _pointsText;

        [Tooltip("Key/button hint inserted in the points prompt.")]
        [SerializeField] private string _menuHint = "TAB";

        // ============ State ============

        private float _shownHealth = -1f;
        private float _chipWaitUntil;
        private float _shownXp;
        private int _shownLevel = -1;
        private int _shownPoints = -1;
        private int _shownCurrent = -1;
        private int _shownMax = -1;

        // ============ Lifecycle ============

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            UpdateHealth(now, dt);
            UpdateExperience(dt);
        }

        // ============ Helpers ============

        private void UpdateHealth(float now, float dt)
        {
            if (_health == null)
            {
                return;
            }

            float target = _health.Normalized;
            if (_healthFill != null)
            {
                _healthFill.fillAmount = target;
            }

            if (_healthChip != null)
            {
                if (target < _shownHealth)
                {
                    _chipWaitUntil = now + _chipDelay; // new damage restarts the wait
                }

                float chip = _healthChip.fillAmount;
                if (target >= chip)
                {
                    chip = target; // heals and max-health upgrades snap the chip up
                }
                else if (now >= _chipWaitUntil)
                {
                    chip = Mathf.MoveTowards(chip, target, _chipDrainSpeed * dt);
                }

                _healthChip.fillAmount = chip;
            }

            _shownHealth = target;

            if (_healthText != null && (_health.Current != _shownCurrent || _health.Max != _shownMax))
            {
                _shownCurrent = _health.Current;
                _shownMax = _health.Max;
                _healthText.text = $"{_shownCurrent} / {_shownMax}";
            }
        }

        private void UpdateExperience(float dt)
        {
            if (_progression == null)
            {
                return;
            }

            if (_progression.Level != _shownLevel)
            {
                _shownLevel = _progression.Level;
                _shownXp = 0f; // the bar refills from empty on level-up
                if (_levelText != null)
                {
                    _levelText.text = $"LV {_shownLevel}";
                }
            }

            _shownXp = Mathf.MoveTowards(_shownXp, _progression.XpNormalized, 2f * dt);
            if (_xpFill != null)
            {
                _xpFill.fillAmount = _shownXp;
            }

            if (_pointsText != null && _progression.Points != _shownPoints)
            {
                _shownPoints = _progression.Points;
                _pointsText.enabled = _shownPoints > 0;
                _pointsText.text = $"+{_shownPoints} UPGRADE POINT{(_shownPoints == 1 ? "" : "S")}   [{_menuHint}]";
            }
        }
    }
}
