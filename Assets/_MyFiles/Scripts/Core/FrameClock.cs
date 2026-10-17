// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

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
                // TODO: return _frame.
                return 0;
            }
        }

        // ============ API ============

        /// <summary>Accumulates <paramref name="deltaSeconds"/> and steps one frame per whole <see cref="FrameTime.SecondsPerFrame"/>.</summary>
        /// <param name="deltaSeconds">Scaled delta in seconds (usually <c>Time.fixedDeltaTime</c>). Zero/negative steps nothing.</param>
        /// <returns>Number of frames stepped by this call (0..n).</returns>
        public int Advance(float deltaSeconds)
        {
            // TODO: standard fixed-step accumulator - add dt, peel off whole frames, keep the remainder.
            // - dt <= 0 steps nothing. That's also what makes hitstop free: timeScale 0 -> dt 0 -> timelines freeze.
            // - 1/60 isn't exact in float, so sixty Advance(1/60f) calls can land on 59. Small epsilon in the compare so a
            //   one-frame delta always steps exactly one frame (there's a test for this).
            // - Probably cap steps per call (~10) so an editor pause doesn't dump an entire attack into one tick.
            // Ref: Glenn Fiedler, "Fix Your Timestep!" (Gaffer On Games) - same idea, counting frames instead of stepping physics.
            return 0;
        }

        /// <summary>Restarts the timeline: <see cref="Frame"/> = 0 and the sub-frame remainder is discarded.</summary>
        public void Reset()
        {
            // TODO: zero frame + accumulator. Called on every state Enter, including re-entering the same state
            // (hitstun refresh, next attack in the chain).
        }
    }
}
