// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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

            // TODO: zero trauma and reset the shake transform so a disabled shaker never leaves the camera offset.
        }

        private void LateUpdate()
        {
            // TODO: trauma-based shake.
            // - Unscaled dt (hitstop sets timeScale 0 and we still want the shake during it).
            // - shake = trauma^2, so small hits barely move and big hits really kick.
            // - Offset X/Y from Perlin noise (remap to -1..1, different row per axis, avoid integer coordinates - Perlin is ~0.5
            //   at lattice points so it won't move). Scale by ShakeMaxOffset. No Z, it'd look like a zoom pulse.
            // - Decay trauma linearly over time.
            // - Separate ShakeRoot because CameraRig's follow writes the root position every frame; shaking there means the
            //   follow fights it.
            // - A tiny roll can add punch, but BillboardSprite copies camera rotation so sprites would roll too. Keep it small.
            // Ref: Squirrel Eiserloh, GDC 2016 "Math for Game Programmers: Juicing Your Cameras With Math" - where the
            // trauma^2 + Perlin approach comes from.
        }

        // ============ API ============

        /// <summary>Adds trauma, clamped to 0..1. Larger hits add more; trauma decays over unscaled time.</summary>
        /// <param name="amount">Trauma to add (0..1). Negative values are ignored.</param>
        public void AddTrauma(float amount)
        {
            // TODO: add trauma, clamped to 0..1. Ignore <= 0.
        }

        // ============ Event Handlers ============

        /// <summary>HitEvent listener: shake by the attack's ScreenShake value.</summary>
        /// <param name="evt">The hit.</param>
        private void OnHit(HitEvent evt)
        {
            // TODO: add the hit's shake as trauma. One swing hitting several targets publishes several events, so trauma
            // stacks (clamped). If that's too much, keep the max per frame instead of summing.
        }
    }
}
