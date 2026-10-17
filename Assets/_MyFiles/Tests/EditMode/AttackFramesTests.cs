using Brawler.Combat;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="AttackFrames"/> queries. Uses Attack_Jab1 values from ARCHITECTURE.md §7.4:
    /// startup 4, active 3, recovery 10 (total 17), cancel window [7, 17).
    /// Phases: Startup [0,4), Active [4,7), Recovery [7,17), Finished &gt;= 17.
    /// </summary>
    public sealed class AttackFramesTests
    {
        // ============ Fixture ============

        private const int Startup = 4;
        private const int Active = 3;
        private const int Recovery = 10;
        private const int CancelStart = 7;
        private const int CancelEnd = 17;
        private const int Total = Startup + Active + Recovery;

        private AttackFrames _jab;

        [SetUp]
        public void SetUp()
        {
            _jab = new AttackFrames(Startup, Active, Recovery, CancelStart, CancelEnd);
        }

        // ============ Construction ============

        [Test]
        public void Constructor_StoresAllValues()
        {
            Assert.AreEqual(Startup, _jab.Startup);
            Assert.AreEqual(Active, _jab.Active);
            Assert.AreEqual(Recovery, _jab.Recovery);
            Assert.AreEqual(CancelStart, _jab.CancelWindowStart);
            Assert.AreEqual(CancelEnd, _jab.CancelWindowEnd);
        }

        [Test]
        public void TotalFrames_IsStartupPlusActivePlusRecovery()
        {
            Assert.AreEqual(17, _jab.TotalFrames);
        }

        [TestCase(8, 4, 22, 34)]
        [TestCase(0, 1, 0, 1)]
        [TestCase(5, 3, 12, 20)]
        public void TotalFrames_MatchesSumForOtherAttacks(int startup, int active, int recovery, int expected)
        {
            var frames = new AttackFrames(startup, active, recovery, 0, 0);

            Assert.AreEqual(expected, frames.TotalFrames);
        }

        // ============ GetPhase ============

        [TestCase(-5, AttackPhase.Startup)]
        [TestCase(-1, AttackPhase.Startup)]
        [TestCase(0, AttackPhase.Startup)]
        [TestCase(3, AttackPhase.Startup)]   // last startup frame
        [TestCase(4, AttackPhase.Active)]    // first active frame (== Startup)
        [TestCase(6, AttackPhase.Active)]    // last active frame
        [TestCase(7, AttackPhase.Recovery)]  // first recovery frame (== Startup + Active)
        [TestCase(16, AttackPhase.Recovery)] // last recovery frame (== Total - 1)
        [TestCase(17, AttackPhase.Finished)] // == Total
        [TestCase(100, AttackPhase.Finished)]
        public void GetPhase_ReturnsExpectedPhase_AtBoundaryFrames(int frame, AttackPhase expected)
        {
            Assert.AreEqual(expected, _jab.GetPhase(frame));
        }

        [Test]
        public void GetPhase_WithZeroStartup_FrameZeroIsActive()
        {
            var frames = new AttackFrames(0, 2, 3, 0, 0);

            Assert.AreEqual(AttackPhase.Active, frames.GetPhase(0));
            Assert.AreEqual(AttackPhase.Active, frames.GetPhase(1));
            Assert.AreEqual(AttackPhase.Recovery, frames.GetPhase(2));
        }

        [Test]
        public void GetPhase_WithZeroRecovery_GoesStraightFromActiveToFinished()
        {
            var frames = new AttackFrames(2, 2, 0, 0, 0);

            Assert.AreEqual(AttackPhase.Active, frames.GetPhase(3));
            Assert.AreEqual(AttackPhase.Finished, frames.GetPhase(4));
        }

        [Test]
        public void GetPhase_WithSingleActiveFrame_HasExactlyOneActiveFrame()
        {
            var frames = new AttackFrames(3, 1, 3, 0, 0);

            Assert.AreEqual(AttackPhase.Startup, frames.GetPhase(2));
            Assert.AreEqual(AttackPhase.Active, frames.GetPhase(3));
            Assert.AreEqual(AttackPhase.Recovery, frames.GetPhase(4));
        }

        // ============ IsActive ============

        [TestCase(-1, false)]
        [TestCase(3, false)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        [TestCase(6, true)]
        [TestCase(7, false)]
        [TestCase(17, false)]
        public void IsActive_IsTrueOnlyDuringActivePhase(int frame, bool expected)
        {
            Assert.AreEqual(expected, _jab.IsActive(frame));
        }

        [Test]
        public void IsActive_CountOverWholeTimeline_EqualsActiveFrames()
        {
            int activeCount = 0;
            for (int frame = 0; frame < Total + 5; frame++)
            {
                if (_jab.IsActive(frame))
                {
                    activeCount++;
                }
            }

            Assert.AreEqual(Active, activeCount);
        }

        // ============ IsFinished ============

        [TestCase(0, false)]
        [TestCase(16, false)]
        [TestCase(17, true)]
        [TestCase(18, true)]
        public void IsFinished_IsTrueFromTotalFramesOnward(int frame, bool expected)
        {
            Assert.AreEqual(expected, _jab.IsFinished(frame));
        }

        // ============ IsInCancelWindow ============

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(6, false)]  // one before start
        [TestCase(7, true)]   // start (inclusive)
        [TestCase(12, true)]
        [TestCase(16, true)]  // last frame inside
        [TestCase(17, false)] // end (exclusive)
        [TestCase(30, false)]
        public void IsInCancelWindow_UsesHalfOpenRange(int frame, bool expected)
        {
            Assert.AreEqual(expected, _jab.IsInCancelWindow(frame));
        }

        [Test]
        public void IsInCancelWindow_WhenStartEqualsEnd_IsNeverTrue()
        {
            var frames = new AttackFrames(4, 3, 10, 7, 7);

            for (int frame = -1; frame <= 20; frame++)
            {
                Assert.IsFalse(frames.IsInCancelWindow(frame), "frame " + frame);
            }
        }

        [Test]
        public void IsInCancelWindow_WhenStartAfterEnd_IsNeverTrue()
        {
            var frames = new AttackFrames(4, 3, 10, 12, 8);

            for (int frame = -1; frame <= 20; frame++)
            {
                Assert.IsFalse(frames.IsInCancelWindow(frame), "frame " + frame);
            }
        }

        [Test]
        public void IsInCancelWindow_IsNotClampedToTotalFrames()
        {
            // Authoring validation is AttackData's job; the query reports the raw window.
            var frames = new AttackFrames(2, 2, 2, 5, 10); // total 6

            Assert.IsTrue(frames.IsInCancelWindow(8));
        }

        [Test]
        public void DefaultStruct_HasNoCancelWindow_AndIsFinishedImmediately()
        {
            var frames = default(AttackFrames);

            Assert.AreEqual(0, frames.TotalFrames);
            Assert.IsFalse(frames.IsInCancelWindow(0));
            Assert.IsTrue(frames.IsFinished(0));
        }
    }
}
