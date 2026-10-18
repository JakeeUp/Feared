namespace Brawler.Core
{
    /// <summary>
    /// Turns arbitrary scaled delta times into whole 60 Hz simulation frames (see <see cref="FrameTime"/>), so frame
    /// data (startup/active/recovery, dash, hitstun) is correct regardless of <c>Time.fixedDeltaTime</c>.
    /// </summary>
    /// <remarks>
    /// Usage contract: on state Enter call <see cref="Reset"/> and process frame 0; each FixedTick call
    /// <c>int steps = clock.Advance(dt)</c>, then process every newly entered frame
    /// <c>Frame - steps + 1 .. Frame</c> so a 1-frame active window is never skipped when a long delta spans
    /// several frames. Pure C#, no allocation.
    /// </remarks>
    public sealed class FrameClock
    {
        // ============ Constants ============

        /// <summary>Float slack so a delta of exactly one (inexact) 1/60 always steps one frame.</summary>
        private const float Epsilon = 1e-5f;

        /// <summary>Most frames a single <see cref="Advance"/> call may step (one second; tests advance 1 s at once).</summary>
        private const int MaxStepsPerAdvance = FrameTime.FramesPerSecond;

        // ============ State ============

        /// <summary>Whole frames elapsed since the last <see cref="Reset"/>.</summary>
        private int _frame;

        /// <summary>Leftover seconds not yet amounting to a whole frame (always in [0, SecondsPerFrame)).</summary>
        private float _accumulator;

        // ============ Properties ============

        /// <summary>Current frame index since the last <see cref="Reset"/> (0 right after a reset or construction).</summary>
        public int Frame
        {
            get
            {
                return _frame;
            }
        }

        // ============ API ============

        /// <summary>Accumulates <paramref name="deltaSeconds"/> and steps one frame per whole <see cref="FrameTime.SecondsPerFrame"/>.</summary>
        /// <param name="deltaSeconds">Scaled delta in seconds (usually <c>Time.fixedDeltaTime</c>). Zero/negative steps nothing.</param>
        /// <returns>Number of frames stepped by this call (0..n).</returns>
        public int Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return 0;
            }

            _accumulator += deltaSeconds;

            int steps = 0;
            while (_accumulator + Epsilon >= FrameTime.SecondsPerFrame)
            {
                _accumulator -= FrameTime.SecondsPerFrame;
                steps++;

                // An editor pause or hitch shouldn't dump a whole attack into one tick.
                if (steps >= MaxStepsPerAdvance)
                {
                    _accumulator = 0f;
                    break;
                }
            }

            // The epsilon can leave a tiny negative remainder; keep the documented [0, SecondsPerFrame) range.
            if (_accumulator < 0f)
            {
                _accumulator = 0f;
            }

            _frame += steps;
            return steps;
        }

        /// <summary>Restarts the timeline: <see cref="Frame"/> = 0 and the sub-frame remainder is discarded.</summary>
        public void Reset()
        {
            _frame = 0;
            _accumulator = 0f;
        }
    }
}
