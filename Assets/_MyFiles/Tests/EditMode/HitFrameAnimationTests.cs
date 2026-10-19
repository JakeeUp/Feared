using Brawler.Combat;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="AttackFrames.GetAnimationFrame(int, int, int)"/> for clips that animate their own
    /// wind-up (enemy casters, the boss): frames before the hit frame spread over startup, the hit frame held while
    /// active, the rest spread over recovery.
    /// </summary>
    public sealed class HitFrameAnimationTests
    {
        // 30 startup, 2 active, 18 recovery; a 10-frame clip whose shot leaves on frame 6 (the Church Wizard).
        private static readonly AttackFrames Frames = new AttackFrames(30, 2, 18, 0, 0);
        private const int ClipFrames = 10;
        private const int HitFrame = 6;

        [Test]
        public void Startup_PlaysWindUpFrames_InOrder_WithoutReachingTheHit()
        {
            int previous = 0;
            for (int frame = 0; frame < 30; frame++)
            {
                int shown = Frames.GetAnimationFrame(frame, ClipFrames, HitFrame);
                Assert.That(shown, Is.InRange(0, HitFrame - 1), "frame " + frame);
                Assert.That(shown, Is.GreaterThanOrEqualTo(previous), "frame " + frame);
                previous = shown;
            }

            Assert.AreEqual(0, Frames.GetAnimationFrame(0, ClipFrames, HitFrame));
            Assert.AreEqual(HitFrame - 1, Frames.GetAnimationFrame(29, ClipFrames, HitFrame));
        }

        [Test]
        public void ActiveFrames_HoldTheHitFrame()
        {
            Assert.AreEqual(HitFrame, Frames.GetAnimationFrame(30, ClipFrames, HitFrame));
            Assert.AreEqual(HitFrame, Frames.GetAnimationFrame(31, ClipFrames, HitFrame));
        }

        [Test]
        public void Recovery_PlaysTheRest_AndEndsOnTheLastFrame()
        {
            Assert.AreEqual(HitFrame + 1, Frames.GetAnimationFrame(32, ClipFrames, HitFrame));
            Assert.AreEqual(ClipFrames - 1, Frames.GetAnimationFrame(49, ClipFrames, HitFrame));
            Assert.AreEqual(ClipFrames - 1, Frames.GetAnimationFrame(500, ClipFrames, HitFrame));
        }

        [Test]
        public void HitFrameZero_UsesTheDefaultMapping()
        {
            for (int frame = 0; frame < 50; frame++)
            {
                Assert.AreEqual(Frames.GetAnimationFrame(frame, ClipFrames), Frames.GetAnimationFrame(frame, ClipFrames, 0), "frame " + frame);
            }
        }

        [Test]
        public void HitFrameOnLastFrame_HoldsItThroughRecovery()
        {
            Assert.AreEqual(ClipFrames - 1, Frames.GetAnimationFrame(31, ClipFrames, ClipFrames - 1));
            Assert.AreEqual(ClipFrames - 1, Frames.GetAnimationFrame(45, ClipFrames, ClipFrames + 5));
        }
    }
}
