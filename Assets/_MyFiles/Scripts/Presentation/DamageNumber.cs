// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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
        }

        private void Update()
        {
            // TODO: animate on unscaled time (numbers should still pop during hitstop), then release once.
            // Ease-out rise, fade over the last ~40%, maybe a quick scale punch at the start.
            // TextMesh.color writes vertex colors so there's no material instance per number.
        }

        private void OnDisable()
        {
            // TODO: if we get disabled mid-animation (scene unload etc.) the pool is probably being torn down, so don't Release
            // here - just flag as released so the next Play starts clean.
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
            // TODO: set text, position/rotation, reset timer + color, clear the released flag.
            // amount.ToString() allocates per hit - fine for now since it's per hit, not per frame. If it ever shows up in the
            // profiler, cache a string table for 0..999.
        }

        // ============ Helpers ============

        /// <summary>Returns this instance to its pool, at most once per play.</summary>
        private void ReleaseToPool()
        {
            // TODO: release to the pool exactly once (flag first - Release disables us, which runs OnDisable inside it).
            // No pool (prefab dropped in a scene by hand) -> just Destroy.
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
