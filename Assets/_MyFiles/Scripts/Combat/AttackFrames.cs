using System;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Frame data for one attack, in 60 Hz simulation frames. Frame 0 is the first startup frame.
    /// Pure value type with pure queries so it is EditMode-testable without assets or a scene.
    /// </summary>
    /// <remarks>
    /// Ranges are half-open: Startup [0, S), Active [S, S+A), Recovery [S+A, S+A+R), Finished &gt;= S+A+R.
    /// Cancel window is [CancelWindowStart, CancelWindowEnd). During the cancel window the attack may be
    /// cancelled into the next combo attack or into Dash.
    /// </remarks>
    [Serializable]
    public struct AttackFrames
    {
        // ============ Serialized Fields ============

        [Tooltip("Wind-up frames before the hitbox turns on.")]
        [SerializeField, Min(0)] private int _startup;

        [Tooltip("Frames the hitbox is live (swept every simulation frame). Must be >= 1.")]
        [SerializeField, Min(1)] private int _active;

        [Tooltip("Frames after the hitbox turns off before the attack finishes.")]
        [SerializeField, Min(0)] private int _recovery;

        [Tooltip("First frame (inclusive, from attack start) where the attack can be cancelled into the next combo attack or Dash. Usually Startup + Active.")]
        [SerializeField, Min(0)] private int _cancelWindowStart;

        [Tooltip("Frame (exclusive) where the cancel window closes. Usually TotalFrames. Equal to start = no cancel window.")]
        [SerializeField, Min(0)] private int _cancelWindowEnd;

        // ============ Construction ============

        /// <summary>Creates frame data (used by tests and editor tooling).</summary>
        /// <param name="startup">Startup frames (&gt;= 0).</param>
        /// <param name="active">Active frames (&gt;= 1).</param>
        /// <param name="recovery">Recovery frames (&gt;= 0).</param>
        /// <param name="cancelWindowStart">Cancel window start frame, inclusive.</param>
        /// <param name="cancelWindowEnd">Cancel window end frame, exclusive.</param>
        public AttackFrames(int startup, int active, int recovery, int cancelWindowStart, int cancelWindowEnd)
        {
            _startup = startup;
            _active = active;
            _recovery = recovery;
            _cancelWindowStart = cancelWindowStart;
            _cancelWindowEnd = cancelWindowEnd;
        }

        // ============ Properties ============

        /// <summary>Startup frame count.</summary>
        public int Startup => _startup;

        /// <summary>Active frame count.</summary>
        public int Active => _active;

        /// <summary>Recovery frame count.</summary>
        public int Recovery => _recovery;

        /// <summary>Cancel window start frame (inclusive).</summary>
        public int CancelWindowStart => _cancelWindowStart;

        /// <summary>Cancel window end frame (exclusive).</summary>
        public int CancelWindowEnd => _cancelWindowEnd;

        /// <summary>Startup + Active + Recovery.</summary>
        public int TotalFrames
        {
            get
            {
                return _startup + _active + _recovery;
            }
        }

        // ============ Queries ============

        /// <summary>Returns the phase the attack is in at <paramref name="frame"/>.</summary>
        /// <param name="frame">Frame index since attack start (0-based).</param>
        /// <returns>The phase; <see cref="AttackPhase.Finished"/> once frame &gt;= TotalFrames.</returns>
        public AttackPhase GetPhase(int frame)
        {
            if (frame < _startup)
            {
                return AttackPhase.Startup;
            }

            if (frame < _startup + _active)
            {
                return AttackPhase.Active;
            }

            return frame < TotalFrames ? AttackPhase.Recovery : AttackPhase.Finished;
        }

        /// <summary>True while the hitbox should be live.</summary>
        /// <param name="frame">Frame index since attack start.</param>
        /// <returns>True when <see cref="GetPhase"/> is Active.</returns>
        public bool IsActive(int frame)
        {
            return GetPhase(frame) == AttackPhase.Active;
        }

        /// <summary>True while the attack may be cancelled into the next combo attack or Dash.</summary>
        /// <param name="frame">Frame index since attack start.</param>
        /// <returns>True when CancelWindowStart &lt;= frame &lt; CancelWindowEnd.</returns>
        public bool IsInCancelWindow(int frame)
        {
            return frame >= _cancelWindowStart && frame < _cancelWindowEnd;
        }

        /// <summary>
        /// Animation frame for clips whose art animates its own wind-up: frames [0, hitFrame) play across startup, the
        /// hit frame is held through the active frames, and the rest play across recovery.
        /// </summary>
        /// <param name="frame">Simulation frame since the attack started.</param>
        /// <param name="clipFrames">Frames in the clip.</param>
        /// <param name="hitFrame">Clip frame where the blow lands (or the shot leaves). 0 or less uses the default mapping.</param>
        /// <returns>Clip frame index in [0, clipFrames).</returns>
        public int GetAnimationFrame(int frame, int clipFrames, int hitFrame)
        {
            if (hitFrame <= 0 || clipFrames < 3)
            {
                return GetAnimationFrame(frame, clipFrames);
            }

            hitFrame = Mathf.Min(hitFrame, clipFrames - 1);
            if (frame < _startup)
            {
                return Mathf.Clamp(Mathf.FloorToInt(frame / (float)Mathf.Max(1, _startup) * hitFrame), 0, hitFrame - 1);
            }

            int activeEnd = _startup + _active;
            if (frame < activeEnd)
            {
                return hitFrame;
            }

            int rest = clipFrames - hitFrame - 1;
            if (rest <= 0)
            {
                return hitFrame;
            }

            float t = (frame - activeEnd) / (float)Mathf.Max(1, _recovery);
            return Mathf.Clamp(hitFrame + 1 + Mathf.FloorToInt(t * rest), hitFrame + 1, clipFrames - 1);
        }

        /// <summary>
        /// Picks the sprite frame for a wind-up / swing / recovery animation: startup shows frame 0, the active frames
        /// show frame 1, and recovery spreads the remaining frames evenly. Clips under 3 frames play linearly.
        /// </summary>
        /// <param name="frame">Frame index since attack start.</param>
        /// <param name="clipFrames">Frames in the animation.</param>
        /// <returns>Animation frame index in [0, clipFrames).</returns>
        public int GetAnimationFrame(int frame, int clipFrames)
        {
            if (clipFrames <= 0)
            {
                return 0;
            }

            if (clipFrames < 3)
            {
                int total = TotalFrames > 0 ? TotalFrames : 1;
                return Mathf.Clamp(frame * clipFrames / total, 0, clipFrames - 1);
            }

            if (frame < _startup)
            {
                return 0;
            }

            int activeEnd = _startup + _active;
            if (frame < activeEnd)
            {
                return 1;
            }

            float t = (frame - activeEnd) / (float)Mathf.Max(1, _recovery);
            return Mathf.Clamp(2 + Mathf.FloorToInt(t * (clipFrames - 2)), 2, clipFrames - 1);
        }

        /// <summary>True once the attack's timeline has fully elapsed.</summary>
        /// <param name="frame">Frame index since attack start.</param>
        /// <returns>True when frame &gt;= TotalFrames.</returns>
        public bool IsFinished(int frame)
        {
            return frame >= TotalFrames;
        }
    }
}
