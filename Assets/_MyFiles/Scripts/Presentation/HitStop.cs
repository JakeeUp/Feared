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

            // Otherwise a disable mid-freeze leaves the whole game stuck at timeScale 0.
            if (_isActive)
            {
                Restore();
            }
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            // Unscaled: deltaTime is 0 during the freeze, so it would never end.
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                Restore();
            }
        }

        // ============ API ============

        /// <summary>
        /// Starts or extends a hitstop. Overlapping calls take the MAX remaining (never the sum), capped by
        /// <see cref="FeedbackSettings.MaxHitStopSeconds"/>.
        /// </summary>
        /// <param name="seconds">Real (unscaled) seconds to freeze. &lt;= 0 is ignored.</param>
        public void Trigger(float seconds)
        {
            if (seconds <= 0f || _settings == null)
            {
                return;
            }

            // Capture only on the first trigger; a mid-freeze capture would read 0 and restore to 0 forever.
            if (!_isActive)
            {
                _restoreTimeScale = Time.timeScale;
                _isActive = true;
            }

            // Max, not sum: one swing hitting three targets shouldn't triple the freeze.
            _remaining = Mathf.Max(_remaining, Mathf.Min(seconds, _settings.MaxHitStopSeconds));
            Time.timeScale = _settings.HitStopTimeScale;
        }

        // ============ Event Handlers ============

        /// <summary>HitEvent listener: freeze for the attack's hitstop frames.</summary>
        /// <param name="evt">The hit.</param>
        private void OnHit(HitEvent evt)
        {
            Trigger(FrameTime.ToSeconds(evt.HitStopFrames));
        }

        // ============ Helpers ============

        private void Restore()
        {
            Time.timeScale = _restoreTimeScale;
            _remaining = 0f;
            _isActive = false;
        }
    }
}
