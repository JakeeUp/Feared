using Brawler.Combat;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="AttackFrames.GetAnimationFrame"/>: the wind-up frame through startup, the swing
    /// frame exactly while the hitbox is active, and recovery spread over the remaining frames.
    /// </summary>
    public sealed class AttackAnimationFrameTests
    {
        // 4 startup, 3 active, 10 recovery; a 10-frame clip (wind-up, swing, 8 recovery frames).
        private static readonly AttackFrames Frames = new AttackFrames(4, 3, 10, 7, 17);
        private const int ClipFrames = 10;

        [Test]
        public void Startup_ShowsWindUpFrame()
        {
            for (int frame = 0; frame < 4; frame++)
            {
                Assert.AreEqual(0, Frames.GetAnimationFrame(frame, ClipFrames), "frame " + frame);
            }
        }

        [Test]
        public void ActiveFrames_ShowSwingFrame()
        {
            for (int frame = 4; frame < 7; frame++)
            {
                Assert.AreEqual(1, Frames.GetAnimationFrame(frame, ClipFrames), "frame " + frame);
                Assert.IsTrue(Frames.IsActive(frame));
            }
        }

        [Test]
        public void Recovery_StartsAtFrameTwo_AndEndsOnTheLastFrame()
        {
            Assert.AreEqual(2, Frames.GetAnimationFrame(7, ClipFrames));
            Assert.AreEqual(ClipFrames - 1, Frames.GetAnimationFrame(16, ClipFrames));
            Assert.AreEqual(ClipFrames - 1, Frames.GetAnimationFrame(99, ClipFrames), "Past the end clamps to the last frame.");
        }

        [Test]
        public void Recovery_NeverGoesBackwards()
        {
            int previous = 0;
            for (int frame = 0; frame < Frames.TotalFrames; frame++)
            {
                int current = Frames.GetAnimationFrame(frame, ClipFrames);
                Assert.GreaterOrEqual(current, previous, "frame " + frame);
                previous = current;
            }
        }

        [Test]
        public void ShortClip_PlaysLinearly_AndEmptyClipReturnsZero()
        {
            Assert.AreEqual(0, Frames.GetAnimationFrame(0, 2));
            Assert.AreEqual(1, Frames.GetAnimationFrame(16, 2));
            Assert.AreEqual(0, Frames.GetAnimationFrame(5, 0));
        }
    }
}
