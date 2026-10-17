using Brawler.Core;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="FrameClock"/>: converts arbitrary deltas into whole 60 Hz frames,
    /// carries sub-frame remainders, ignores non-positive deltas and resets cleanly.
    /// </summary>
    public sealed class FrameClockTests
    {
        // ============ Constants ============

        private const float OneFrame = FrameTime.SecondsPerFrame;

        // ============ Fixture ============

        private FrameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new FrameClock();
        }

        // ============ Initial state ============

        [Test]
        public void NewClock_FrameIsZero()
        {
            Assert.AreEqual(0, _clock.Frame);
        }

        // ============ Advance ============

        [Test]
        public void Advance_ByExactlyOneFrame_StepsOneFrame()
        {
            int steps = _clock.Advance(OneFrame);

            Assert.AreEqual(1, steps);
            Assert.AreEqual(1, _clock.Frame);
        }

        [Test]
        public void Advance_ByLessThanOneFrame_StepsNothing()
        {
            int steps = _clock.Advance(OneFrame * 0.5f);

            Assert.AreEqual(0, steps);
            Assert.AreEqual(0, _clock.Frame);
        }

        [Test]
        public void Advance_AccumulatesPartialDeltas()
        {
            Assert.AreEqual(0, _clock.Advance(OneFrame * 0.6f));
            Assert.AreEqual(1, _clock.Advance(OneFrame * 0.6f), "0.6 + 0.6 = 1.2 frames → one step, 0.2 carried.");
            Assert.AreEqual(1, _clock.Frame);
        }

        [Test]
        public void Advance_ByMultipleFramesAtOnce_ReturnsStepCount()
        {
            int steps = _clock.Advance(OneFrame * 3f);

            Assert.AreEqual(3, steps);
            Assert.AreEqual(3, _clock.Frame);
        }

        [Test]
        public void Advance_RemainderCarriesIntoNextCall()
        {
            _clock.Advance(OneFrame * 2.5f); // 2 frames, 0.5 carried

            int steps = _clock.Advance(OneFrame * 0.5f);

            Assert.AreEqual(1, steps);
            Assert.AreEqual(3, _clock.Frame);
        }

        [Test]
        public void Advance_SixtyOneFrameDeltas_ReachesFrameSixty()
        {
            // 1/60 is not exactly representable; repeated single-frame deltas must not drift to 59.
            for (int i = 0; i < 60; i++)
            {
                Assert.AreEqual(1, _clock.Advance(OneFrame), "call " + i);
            }

            Assert.AreEqual(60, _clock.Frame);
        }

        [Test]
        public void Advance_OneSecond_IsSixtyFrames()
        {
            int steps = _clock.Advance(1f);

            Assert.AreEqual(FrameTime.FramesPerSecond, steps);
            Assert.AreEqual(60, _clock.Frame);
        }

        [Test]
        public void Advance_WithFiftyHertzFixedStep_ConvertsToSixtyHertzFrames()
        {
            // Unity's default Time.fixedDeltaTime is 0.02 (50 Hz). 3 steps = 0.06 s = 3.6 frames → 3 whole frames.
            _clock.Advance(0.02f);
            _clock.Advance(0.02f);
            _clock.Advance(0.02f);

            Assert.AreEqual(3, _clock.Frame);
        }

        [Test]
        public void Advance_WithZeroDelta_StepsNothing()
        {
            Assert.AreEqual(0, _clock.Advance(0f));
            Assert.AreEqual(0, _clock.Frame);
        }

        [Test]
        public void Advance_WithNegativeDelta_StepsNothing_AndDoesNotRewind()
        {
            _clock.Advance(OneFrame * 2f);

            Assert.AreEqual(0, _clock.Advance(-1f));
            Assert.AreEqual(2, _clock.Frame);
        }

        [Test]
        public void Advance_WithNegativeDelta_DoesNotEatAccumulatedRemainder()
        {
            _clock.Advance(OneFrame * 0.5f);
            _clock.Advance(-OneFrame);

            Assert.AreEqual(1, _clock.Advance(OneFrame * 0.5f), "The 0.5 frame carried before the negative delta still counts.");
        }

        [Test]
        public void Advance_NewlyEnteredFrameRange_CoversEveryFrame()
        {
            // Usage contract: after `steps = Advance(dt)`, process frames Frame-steps+1 .. Frame.
            _clock.Advance(OneFrame); // frame 1

            int steps = _clock.Advance(OneFrame * 3f);
            int first = _clock.Frame - steps + 1;

            Assert.AreEqual(2, first);
            Assert.AreEqual(4, _clock.Frame);
        }

        // ============ Reset ============

        [Test]
        public void Reset_SetsFrameToZero()
        {
            _clock.Advance(OneFrame * 5f);

            _clock.Reset();

            Assert.AreEqual(0, _clock.Frame);
        }

        [Test]
        public void Reset_DiscardsSubFrameRemainder()
        {
            _clock.Advance(OneFrame * 1.75f);

            _clock.Reset();

            Assert.AreEqual(0, _clock.Advance(OneFrame * 0.5f), "Leftover 0.75 must not survive a reset.");
            Assert.AreEqual(0, _clock.Frame);
        }

        [Test]
        public void Advance_AfterReset_CountsFromZeroAgain()
        {
            _clock.Advance(OneFrame * 10f);
            _clock.Reset();

            _clock.Advance(OneFrame * 2f);

            Assert.AreEqual(2, _clock.Frame);
        }
    }
}
