using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Level
{
    /// <summary>
    /// Screen-space banner for section titles ("THE NAVE"), checkpoint toasts and the victory card. Fades on real time
    /// so hitstop never freezes it. One banner per scene; level scripts find it through <see cref="Instance"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelBanner : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [SerializeField] private CanvasGroup _titleGroup;

        [SerializeField] private Text _title;

        [SerializeField] private Text _subtitle;

        [SerializeField] private CanvasGroup _toastGroup;

        [SerializeField] private Text _toast;

        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.4f;

        // ============ Runtime ============

        private float _titleUntil;
        private float _toastUntil;

        // ============ Properties ============

        /// <summary>The scene's banner (null when the scene has none).</summary>
        public static LevelBanner Instance { get; private set; }

        // ============ Lifecycle ============

        /// <summary>Clears the instance on entering Play Mode (domain reload is disabled in this project).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void OnEnable()
        {
            Instance = this;
            SetAlpha(_titleGroup, 0f);
            SetAlpha(_toastGroup, 0f);
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            Fade(_titleGroup, now < _titleUntil);
            Fade(_toastGroup, now < _toastUntil);
        }

        // ============ API ============

        /// <summary>Shows a big title (and optional subtitle) for <paramref name="seconds"/>.</summary>
        public void ShowTitle(string title, string subtitle, float seconds = 3f)
        {
            if (_title != null)
            {
                _title.text = title;
            }

            if (_subtitle != null)
            {
                _subtitle.text = subtitle ?? string.Empty;
            }

            _titleUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Shows a small line near the bottom of the screen for <paramref name="seconds"/>.</summary>
        public void ShowToast(string text, float seconds = 2f)
        {
            if (_toast != null)
            {
                _toast.text = text;
            }

            _toastUntil = Time.unscaledTime + seconds;
        }

        // ============ Helpers ============

        private void Fade(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            float target = visible ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / _fadeSeconds);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }
    }
}
