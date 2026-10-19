using Brawler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Level
{
    /// <summary>
    /// Bottom-of-screen boss bar: the framed bar art, a light chip overlay covering everything lost (drawn first), and a
    /// dark drain overlay on top that catches up after a short hold, so a fresh hit shows as a bright strip that fades
    /// to dark. Both grow from the right. Hidden until <see cref="Show"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHealthBar : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [SerializeField] private CanvasGroup _group;

        [Tooltip("Dark overlay over health lost a while ago (anchored right, drawn above the chip; width driven).")]
        [SerializeField] private RectTransform _drain;

        [Tooltip("Light overlay over all lost health (anchored right, drawn below the drain): the fresh part shows as the chip.")]
        [SerializeField] private RectTransform _chip;

        [Tooltip("Width of the bar's inner (fillable) area, in the drain's parent space.")]
        [SerializeField, Min(1f)] private float _innerWidth = 600f;

        [SerializeField] private Text _name;

        [Tooltip("Chip catch-up speed (fraction of the bar per second) after a short hold.")]
        [SerializeField, Min(0.01f)] private float _chipSpeed = 0.6f;

        [SerializeField, Min(0f)] private float _chipHold = 0.4f;

        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.5f;

        // ============ Runtime ============

        private Health _health;
        private bool _visible;
        private float _shown = 1f;
        private float _chipValue = 1f;
        private float _holdUntil;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        private void Update()
        {
            if (_group != null)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, _visible ? 1f : 0f, Time.unscaledDeltaTime / _fadeSeconds);
            }

            if (_health == null)
            {
                return;
            }

            float value = _health.Normalized;
            if (value < _shown)
            {
                _holdUntil = Time.unscaledTime + _chipHold;
            }

            _shown = value;
            if (_chipValue < value || Time.unscaledTime >= _holdUntil)
            {
                _chipValue = _chipValue < value ? value : Mathf.MoveTowards(_chipValue, value, _chipSpeed * Time.unscaledDeltaTime);
            }

            SetWidth(_chip, 1f - _shown);
            SetWidth(_drain, 1f - _chipValue);
        }

        // ============ API ============

        /// <summary>Shows the bar tracking <paramref name="health"/>.</summary>
        public void Show(Health health, string bossName)
        {
            _health = health;
            _shown = _chipValue = health != null ? health.Normalized : 1f;
            if (_name != null)
            {
                _name.text = bossName;
            }

            _visible = true;
        }

        /// <summary>Fades the bar out.</summary>
        public void Hide()
        {
            _visible = false;
        }

        // ============ Helpers ============

        private void SetWidth(RectTransform rect, float fraction)
        {
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp01(fraction) * _innerWidth);
            }
        }
    }
}
