using UnityEngine;
using UnityEngine.Pool;

namespace Brawler.Presentation
{
    /// <summary>
    /// One pooled floating damage number. Uses legacy <see cref="TextMesh"/> (no TMP essentials dependency;
    /// WebGL-safe). Rises and fades on unscaled time, then releases itself to its pool exactly once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumber : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Start-of-life scale multiplier for the pop-in punch.</summary>
        private const float PunchScale = 1.4f;

        /// <summary>Fraction of the lifetime the punch takes to settle.</summary>
        private const float PunchPortion = 0.15f;

        /// <summary>Fraction of the lifetime (at the end) spent fading out.</summary>
        private const float FadePortion = 0.4f;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("TextMesh that displays the number (on this GameObject). Its MeshRenderer uses the font's material.")]
        [SerializeField] private TextMesh _text;

        // ============ State ============

        /// <summary>Owning pool; injected once by <see cref="Initialize"/>.</summary>
        private IObjectPool<DamageNumber> _pool;

        /// <summary>Lifetime/rise tuning; injected once by <see cref="Initialize"/>.</summary>
        private FeedbackSettings _settings;

        /// <summary>Unscaled seconds since <see cref="Play"/>.</summary>
        private float _elapsed;

        /// <summary>World position at <see cref="Play"/>; the rise is applied on top of it.</summary>
        private Vector3 _startPosition;

        /// <summary>Text colour at full opacity (captured from the prefab in Awake).</summary>
        private Color _baseColor = Color.white;

        /// <summary>Guard: true once released for the current play, so Release is called exactly once.</summary>
        private bool _isReleased = true;

        /// <summary>Prefab scale, captured in Awake; the punch multiplies it.</summary>
        private Vector3 _baseScale = Vector3.one;

        // ============ Properties ============

        /// <summary>True while the number is animating (between <see cref="Play"/> and its release).</summary>
        public bool IsPlaying => !_isReleased;

        /// <summary>True once <see cref="Initialize"/> has injected a pool and settings.</summary>
        public bool IsInitialized => _pool != null && _settings != null;

        /// <summary>Unscaled seconds since the current play started.</summary>
        public float Elapsed => _elapsed;

        /// <summary>World position the current play started from.</summary>
        public Vector3 StartPosition => _startPosition;

        /// <summary>Full-opacity text colour.</summary>
        public Color BaseColor => _baseColor;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_text != null)
            {
                _baseColor = _text.color;
            }

            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (_isReleased || _settings == null)
            {
                return;
            }

            // Unscaled so numbers still pop during hitstop.
            _elapsed += Time.unscaledDeltaTime;
            float lifetime = Mathf.Max(0.01f, _settings.DamageNumberLifetime);
            float t = Mathf.Clamp01(_elapsed / lifetime);

            float easeOut = 1f - (1f - t) * (1f - t);
            transform.position = _startPosition + Vector3.up * (_settings.DamageNumberRise * easeOut);

            // Quick scale punch over the first 15%.
            float punch = t < PunchPortion ? Mathf.Lerp(PunchScale, 1f, t / PunchPortion) : 1f;
            transform.localScale = _baseScale * punch;

            // Fade over the last 40%. TextMesh.color writes vertex colours, so no material instances.
            if (_text != null)
            {
                Color color = _baseColor;
                color.a *= 1f - Mathf.InverseLerp(1f - FadePortion, 1f, t);
                _text.color = color;
            }

            if (t >= 1f)
            {
                ReleaseToPool();
            }
        }

        private void OnDisable()
        {
            // The pool is probably being torn down; never Release from here.
            _isReleased = true;
        }

        // ============ API ============

        /// <summary>Injects the owning pool and settings. Called once by <see cref="DamageNumberSpawner"/> on create.</summary>
        /// <param name="pool">Pool to release into when done.</param>
        /// <param name="settings">Lifetime / rise tuning.</param>
        public void Initialize(IObjectPool<DamageNumber> pool, FeedbackSettings settings)
        {
            // Wiring: constructor-style dependency injection.
            _pool = pool;
            _settings = settings;
        }

        /// <summary>Starts showing <paramref name="amount"/> at <paramref name="position"/> facing <paramref name="rotation"/>.</summary>
        /// <param name="amount">Damage to display.</param>
        /// <param name="position">World spawn position (offset/spread already applied by the spawner).</param>
        /// <param name="rotation">World rotation (the camera's rotation, so the text faces the screen).</param>
        public void Play(int amount, Vector3 position, Quaternion rotation)
        {
            if (_text != null)
            {
                _text.text = amount.ToString();
                _text.color = _baseColor;
            }

            _startPosition = position;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = _baseScale * PunchScale;
            _elapsed = 0f;
            _isReleased = false;
        }

        // ============ Helpers ============

        /// <summary>Returns this instance to its pool, at most once per play.</summary>
        private void ReleaseToPool()
        {
            if (_isReleased)
            {
                return;
            }

            // Flag first: Release disables us, which runs OnDisable inside it.
            _isReleased = true;
            if (_pool != null)
            {
                _pool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_text == null)
            {
                _text = GetComponent<TextMesh>();
            }
        }
    }
}
