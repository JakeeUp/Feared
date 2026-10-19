using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Global hitstop: briefly sets <see cref="Time.timeScale"/> to <see cref="FeedbackSettings.HitStopTimeScale"/>
    /// (default 0 = full freeze) when a hit connects, then restores it. A parry also gets a short slow-motion tail after
    /// the freeze (<see cref="FeedbackSettings.ParrySlowSeconds"/>) that eases back to full speed. Counts down with
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
        [Tooltip("Shared event bus asset (Data/Core/EventBus.asset). Listens for HitEvent and ParryEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Hitstop time scale, max duration and the parry slow-motion tail.")]
        [SerializeField] private FeedbackSettings _settings;

        // ============ State ============

        /// <summary>Unscaled seconds of freeze left. &gt; 0 while frozen.</summary>
        private float _remaining;

        /// <summary>Length of the slow-motion tail that follows the current freeze (unscaled seconds, 0 = none).</summary>
        private float _slowSeconds;

        /// <summary>Unscaled seconds elapsed in the slow-motion tail.</summary>
        private float _slowElapsed;

        /// <summary>Time scale captured on the FIRST trigger of a freeze; restored when it ends.</summary>
        private float _restoreTimeScale = 1f;

        /// <summary>True between the first trigger and the restore (freeze and slow tail).</summary>
        private bool _isActive;

        /// <summary>Time.frameCount of the last trigger: that frame's delta elapsed before the hit, so it isn't counted.</summary>
        private int _triggerFrame = -1;

        /// <summary>Cached delegate so subscribe/unsubscribe don't allocate and match by reference.</summary>
        private Action<HitEvent> _onHit;
        private Action<ParryEvent> _onParry;

        // ============ Properties ============

        /// <summary>True while time is frozen/slowed by this component.</summary>
        public bool IsActive => _isActive;

        /// <summary>True during the freeze itself (false in the slow-motion tail).</summary>
        public bool IsFrozen => _isActive && _remaining > 0f;

        /// <summary>Unscaled seconds of freeze remaining (0 when not frozen).</summary>
        public float Remaining => _remaining;

        /// <summary>Time scale that will be restored when the current freeze ends.</summary>
        public float RestoreTimeScale => _restoreTimeScale;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;
            _onParry = OnParry;
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
                _eventBus.Subscribe(_onParry);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
                _eventBus.Unsubscribe(_onParry);
            }

            // Otherwise a disable mid-freeze leaves the whole game stuck at timeScale 0.
            if (_isActive)
            {
                Restore();
            }
        }

        private void Update()
        {
            if (!_isActive || Time.frameCount == _triggerFrame)
            {
                return;
            }

            // Unscaled: deltaTime is 0 during the freeze, so it would never end.
            float dt = Time.unscaledDeltaTime;
            if (_remaining > 0f)
            {
                _remaining -= dt;
                if (_remaining > 0f)
                {
                    return;
                }

                if (_slowSeconds <= 0f || _settings == null)
                {
                    Restore();
                    return;
                }

                dt = -_remaining; // carry the overshoot into the tail
                _remaining = 0f;
            }

            // Slow-motion tail: starts at ParrySlowTimeScale and eases (quadratic) back to the restore scale.
            _slowElapsed += dt;
            float t = _slowElapsed / _slowSeconds;
            if (t >= 1f)
            {
                Restore();
                return;
            }

            Time.timeScale = Mathf.Lerp(_settings.ParrySlowTimeScale, _restoreTimeScale, t * t);
        }

        // ============ API ============

        /// <summary>
        /// Starts or extends a hitstop. Overlapping calls take the MAX remaining (never the sum), capped by
        /// <see cref="FeedbackSettings.MaxHitStopSeconds"/>.
        /// </summary>
        /// <param name="seconds">Real (unscaled) seconds to freeze. &lt;= 0 is ignored.</param>
        /// <param name="slowSeconds">Real seconds of slow motion after the freeze (0 = snap back to full speed).</param>
        public void Trigger(float seconds, float slowSeconds = 0f)
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
                _slowSeconds = 0f;
            }

            // Max, not sum: one swing hitting three targets shouldn't triple the freeze.
            _remaining = Mathf.Max(_remaining, Mathf.Min(seconds, _settings.MaxHitStopSeconds));

            // A hit landing inside a slow tail re-freezes, then finishes whatever is left of the longer tail.
            float tailLeft = _slowSeconds - _slowElapsed;
            _slowSeconds = Mathf.Max(tailLeft, slowSeconds);
            _slowElapsed = 0f;

            _triggerFrame = Time.frameCount;
            Time.timeScale = _settings.HitStopTimeScale;
        }

        // ============ Event Handlers ============

        /// <summary>HitEvent listener: freeze for the attack's hitstop frames.</summary>
        /// <param name="evt">The hit.</param>
        private void OnHit(HitEvent evt)
        {
            Trigger(FrameTime.ToSeconds(evt.HitStopFrames));
        }

        /// <summary>ParryEvent listener: a longer freeze, then a brief slow-motion tail.</summary>
        private void OnParry(ParryEvent evt)
        {
            Trigger(FrameTime.ToSeconds(evt.HitStopFrames), _settings != null ? _settings.ParrySlowSeconds : 0f);
        }

        // ============ Helpers ============

        private void Restore()
        {
            Time.timeScale = _restoreTimeScale;
            _remaining = 0f;
            _slowSeconds = 0f;
            _slowElapsed = 0f;
            _isActive = false;
        }
    }
}
