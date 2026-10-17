// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Global hitstop: briefly sets <see cref="Time.timeScale"/> to <see cref="FeedbackSettings.HitStopTimeScale"/>
    /// (default 0 = full freeze) when a hit connects, then restores it. Counts down with
    /// <see cref="Time.unscaledDeltaTime"/> because scaled time is frozen while it runs.
    /// </summary>
    /// <remarks>
    /// At timeScale 0 FixedUpdate stops and deltaTime is 0, so combat timelines, movement and knockback all pause
    /// for free. The InputBuffer uses scaled time, so presses during the freeze stay buffered.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HitStop : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus asset (Data/Core/EventBus.asset). Listens for HitEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Hitstop time scale and max duration.")]
        [SerializeField] private FeedbackSettings _settings;

        // ============ State ============

        /// <summary>Unscaled seconds of hitstop left. &gt; 0 while active.</summary>
        private float _remaining;

        /// <summary>Time scale captured on the FIRST trigger of a freeze; restored when it ends.</summary>
        private float _restoreTimeScale = 1f;

        /// <summary>True between the first trigger and the restore.</summary>
        private bool _isActive;

        /// <summary>Cached delegate so subscribe/unsubscribe don't allocate and match by reference.</summary>
        private Action<HitEvent> _onHit;

        // ============ Properties ============

        /// <summary>True while time is frozen/slowed by this component.</summary>
        public bool IsActive => _isActive;

        /// <summary>Unscaled seconds of hitstop remaining (0 when inactive).</summary>
        public float Remaining => _remaining;

        /// <summary>Time scale that will be restored when the current freeze ends.</summary>
        public float RestoreTimeScale => _restoreTimeScale;

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

            // TODO: if a freeze is active, restore timeScale and clear state. Important - otherwise getting disabled mid-freeze
            // (scene change, leaving play mode with domain reload off) leaves the whole game stuck at timeScale 0.
        }

        private void Update()
        {
            // TODO: count down on unscaled time and restore timeScale when it runs out.
            // Never Time.deltaTime here - it's 0 during the freeze, so the freeze would never end. unscaledDeltaTime can spike
            // (breakpoint, tabbing away in WebGL); that just ends the hitstop early, which is fine.
        }

        // ============ API ============

        /// <summary>
        /// Starts or extends a hitstop. Overlapping calls take the MAX remaining (never the sum), capped by
        /// <see cref="FeedbackSettings.MaxHitStopSeconds"/>.
        /// </summary>
        /// <param name="seconds">Real (unscaled) seconds to freeze. &lt;= 0 is ignored.</param>
        public void Trigger(float seconds)
        {
            // TODO: start or extend the freeze.
            // - Clamp to MaxHitStopSeconds.
            // - Capture the timeScale to restore ONLY on the first trigger - a second hit mid-freeze would capture 0 and
            //   "restore" to 0 forever.
            // - Take the max of remaining and new, not the sum. Three targets in one swing shouldn't triple the freeze.
            // - Don't touch fixedDeltaTime, timeScale 0 already stops FixedUpdate.
            // Later, if a pause menu also writes timeScale, everything should go through one place.
            // Ref: Masahiro Sakurai's "Creating Games" video on hitstop is the best short explanation of why this matters;
            // Vlambeer's "The Art of Screenshake" talk covers it too (they call it sleep).
        }

        // ============ Event Handlers ============

        /// <summary>HitEvent listener: freeze for the attack's hitstop frames.</summary>
        /// <param name="evt">The hit.</param>
        private void OnHit(HitEvent evt)
        {
            // TODO: trigger with the event's hitstop frames converted via FrameTime (60 Hz frame-data units, not fixedDeltaTime).
        }
    }
}
