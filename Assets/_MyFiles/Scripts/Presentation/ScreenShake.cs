using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Trauma-based screen shake (Squirrel Eiserloh, "Juicing Your Cameras With Math"). Hits add trauma (0..1);
    /// the offset is Perlin noise × trauma² × <see cref="FeedbackSettings.ShakeMaxOffset"/>, applied to
    /// <see cref="_shakeTransform"/>'s localPosition (ShakeRoot, between the rig pivot and the camera).
    /// Runs on <b>unscaled</b> time so it keeps shaking through hitstop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenShake : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Perlin rows per axis (non-integer so samples never sit on lattice points).</summary>
        private const float NoiseRowX = 0.37f;
        private const float NoiseRowY = 7.71f;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus asset (Data/Core/EventBus.asset). Listens for HitEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Shake amplitude, frequency and decay.")]
        [SerializeField] private FeedbackSettings _settings;

        [Tooltip("Transform whose localPosition is offset (CameraRig/Pivot/ShakeRoot). Must NOT be the rig root (CameraRig moves that) or the camera (CameraRig sets its distance).")]
        [SerializeField] private Transform _shakeTransform;

        // ============ State ============

        /// <summary>Current trauma, 0..1.</summary>
        private float _trauma;

        /// <summary>Accumulated unscaled time × frequency used to sample the noise (advance in LateUpdate).</summary>
        private float _noiseTime;

        /// <summary>Cached delegate so subscribe/unsubscribe don't allocate and match by reference.</summary>
        private Action<HitEvent> _onHit;

        // ============ Properties ============

        /// <summary>Current trauma (0..1).</summary>
        public float Trauma => _trauma;

        /// <summary>Current noise sample position (debug/inspection).</summary>
        public float NoiseTime => _noiseTime;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
            }

            _trauma = 0f;
            if (_shakeTransform != null)
            {
                _shakeTransform.localPosition = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (_shakeTransform == null || _settings == null)
            {
                return;
            }

            if (_trauma <= 0f)
            {
                return;
            }

            // Unscaled so the shake keeps going through hitstop.
            float dt = Time.unscaledDeltaTime;
            _noiseTime += dt * _settings.ShakeFrequency;

            // trauma^2: small hits barely move, big hits kick. Offset rows keep Perlin off its lattice points.
            float shake = _trauma * _trauma * _settings.ShakeMaxOffset;
            float x = (Mathf.PerlinNoise(_noiseTime, NoiseRowX) * 2f - 1f) * shake;
            float y = (Mathf.PerlinNoise(_noiseTime, NoiseRowY) * 2f - 1f) * shake;
            _shakeTransform.localPosition = new Vector3(x, y, 0f);

            _trauma = Mathf.Max(0f, _trauma - _settings.TraumaDecayPerSecond * dt);
            if (_trauma <= 0f)
            {
                _shakeTransform.localPosition = Vector3.zero;
            }
        }

        // ============ API ============

        /// <summary>Adds trauma, clamped to 0..1. Larger hits add more; trauma decays over unscaled time.</summary>
        /// <param name="amount">Trauma to add (0..1). Negative values are ignored.</param>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        // ============ Event Handlers ============

        /// <summary>HitEvent listener: shake by the attack's ScreenShake value.</summary>
        /// <param name="evt">The hit.</param>
        private void OnHit(HitEvent evt)
        {
            AddTrauma(evt.ScreenShake);
        }
    }
}
