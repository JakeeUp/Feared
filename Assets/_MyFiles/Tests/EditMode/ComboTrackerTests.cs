using Brawler.Combat;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="ComboTracker"/> (chain index bookkeeping), plus how it composes with
    /// <see cref="AttackFrames"/> cancel windows the way <c>AttackState</c> uses them (ARCHITECTURE.md §4.1):
    /// advance only inside the cancel window, reset when the chain ends or the attack finishes without a follow-up.
    /// </summary>
    public sealed class ComboTrackerTests
    {
        // ============ Fixture ============

        private const int BasicComboLength = 3; // Jab1 -> Jab2 -> Finisher

        // Attack_Jab1 frame data (§7.4): 4/3/10, cancel [7,17).
        private static readonly AttackFrames Jab1 = new AttackFrames(4, 3, 10, 7, 17);

        // Attack_Jab2 frame data (§7.4): 5/3/12, cancel [8,20).
        private static readonly AttackFrames Jab2 = new AttackFrames(5, 3, 12, 8, 20);

        // Attack_Finisher frame data (§7.4): 8/4/22, cancel [20,34).
        private static readonly AttackFrames Finisher = new AttackFrames(8, 4, 22, 20, 34);

        private ComboTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new ComboTracker(BasicComboLength);
        }

        /// <summary>
        /// Mirrors AttackState's "Attack → Attack (next)" rule using only public APIs:
        /// IsInCancelWindow(frame) &amp;&amp; HasNext → TryAdvance. (The real state also needs a consumed Attack press.)
        /// </summary>
        private static bool TryChainAtFrame(ComboTracker tracker, AttackFrames frames, int frame)
        {
            if (!frames.IsInCancelWindow(frame) || !tracker.HasNext)
            {
                return false;
            }

            return tracker.TryAdvance(out _);
        }

        // ============ Construction ============

        [Test]
        public void Constructor_StoresLength_AndStartsOutsideCombo()
        {
            Assert.AreEqual(BasicComboLength, _tracker.ComboLength);
            Assert.AreEqual(-1, _tracker.CurrentIndex);
            Assert.IsFalse(_tracker.IsInCombo);
            Assert.IsFalse(_tracker.HasNext);
            Assert.IsFalse(_tracker.IsFinisher);
        }

        // ============ Begin ============

        [Test]
        public void Begin_StartsAtIndexZero()
        {
            int index = _tracker.Begin();

            Assert.AreEqual(0, index);
            Assert.AreEqual(0, _tracker.CurrentIndex);
            Assert.IsTrue(_tracker.IsInCombo);
            Assert.IsTrue(_tracker.HasNext);
            Assert.IsFalse(_tracker.IsFinisher);
        }

        [Test]
        public void Begin_WhileInCombo_RestartsAtZero()
        {
            _tracker.Begin();
            _tracker.TryAdvance(out _);

            int index = _tracker.Begin();

            Assert.AreEqual(0, index);
            Assert.AreEqual(0, _tracker.CurrentIndex);
        }

        [Test]
        public void Begin_WithZeroLength_ReturnsMinusOne_AndStaysOutsideCombo()
        {
            var empty = new ComboTracker(0);

            Assert.AreEqual(-1, empty.Begin());
            Assert.AreEqual(-1, empty.CurrentIndex);
            Assert.IsFalse(empty.IsInCombo);
            Assert.IsFalse(empty.HasNext);
            Assert.IsFalse(empty.IsFinisher);
        }

        [Test]
        public void Begin_WithLengthOne_IsImmediatelyTheFinisher()
        {
            var single = new ComboTracker(1);

            single.Begin();

            Assert.IsTrue(single.IsFinisher);
            Assert.IsFalse(single.HasNext);
            Assert.IsFalse(single.TryAdvance(out int next));
            Assert.AreEqual(0, next);
        }

        // ============ TryAdvance ============

        [Test]
        public void TryAdvance_FromFirstAttack_MovesToSecond()
        {
            _tracker.Begin();

            bool advanced = _tracker.TryAdvance(out int next);

            Assert.IsTrue(advanced);
            Assert.AreEqual(1, next);
            Assert.AreEqual(1, _tracker.CurrentIndex);
        }

        [Test]
        public void TryAdvance_ThroughWholeChain_StopsAtFinisher()
        {
            _tracker.Begin();

            Assert.IsTrue(_tracker.TryAdvance(out int second));
            Assert.AreEqual(1, second);
            Assert.IsTrue(_tracker.TryAdvance(out int third));
            Assert.AreEqual(2, third);

            Assert.IsTrue(_tracker.IsFinisher);
            Assert.IsFalse(_tracker.HasNext);
        }

        [Test]
        public void TryAdvance_AtFinisher_ReturnsFalse_AndDoesNotChangeIndex()
        {
            _tracker.Begin();
            _tracker.TryAdvance(out _);
            _tracker.TryAdvance(out _);

            bool advanced = _tracker.TryAdvance(out int next);

            Assert.IsFalse(advanced);
            Assert.AreEqual(2, next, "nextIndex reports CurrentIndex when no advance happens.");
            Assert.AreEqual(2, _tracker.CurrentIndex);
        }

        [Test]
        public void TryAdvance_WhenNotInCombo_ReturnsFalse()
        {
            bool advanced = _tracker.TryAdvance(out int next);

            Assert.IsFalse(advanced);
            Assert.AreEqual(-1, next);
            Assert.AreEqual(-1, _tracker.CurrentIndex);
        }

        [TestCase(0, true, false)]
        [TestCase(1, true, false)]
        [TestCase(2, false, true)]
        public void HasNextAndIsFinisher_AtEachIndex(int index, bool expectedHasNext, bool expectedIsFinisher)
        {
            _tracker.Begin();
            for (int i = 0; i < index; i++)
            {
                _tracker.TryAdvance(out _);
            }

            Assert.AreEqual(index, _tracker.CurrentIndex);
            Assert.AreEqual(expectedHasNext, _tracker.HasNext);
            Assert.AreEqual(expectedIsFinisher, _tracker.IsFinisher);
        }

        // ============ Reset ============

        [Test]
        public void Reset_LeavesCombo()
        {
            _tracker.Begin();
            _tracker.TryAdvance(out _);

            _tracker.Reset();

            Assert.AreEqual(-1, _tracker.CurrentIndex);
            Assert.IsFalse(_tracker.IsInCombo);
            Assert.IsFalse(_tracker.HasNext);
            Assert.IsFalse(_tracker.IsFinisher);
        }

        [Test]
        public void Reset_ThenTryAdvance_ReturnsFalse()
        {
            _tracker.Begin();
            _tracker.Reset();

            Assert.IsFalse(_tracker.TryAdvance(out _));
        }

        [Test]
        public void Reset_ThenBegin_StartsFreshChain()
        {
            _tracker.Begin();
            _tracker.TryAdvance(out _);
            _tracker.Reset();

            Assert.AreEqual(0, _tracker.Begin());
            Assert.IsTrue(_tracker.HasNext);
        }

        // ============ With cancel windows (AttackState rules) ============

        [Test]
        public void Chain_AdvancesOnFirstFrameOfCancelWindow()
        {
            _tracker.Begin();

            Assert.IsTrue(TryChainAtFrame(_tracker, Jab1, 7));
            Assert.AreEqual(1, _tracker.CurrentIndex);
        }

        [Test]
        public void Chain_AdvancesOnLastFrameOfCancelWindow()
        {
            _tracker.Begin();

            Assert.IsTrue(TryChainAtFrame(_tracker, Jab1, 16));
            Assert.AreEqual(1, _tracker.CurrentIndex);
        }

        [TestCase(0)]
        [TestCase(4)]  // active frame
        [TestCase(6)]  // one frame before window
        [TestCase(17)] // window end (exclusive) == attack finished
        [TestCase(25)]
        public void Chain_DoesNotAdvanceOutsideCancelWindow(int frame)
        {
            _tracker.Begin();

            Assert.IsFalse(TryChainAtFrame(_tracker, Jab1, frame));
            Assert.AreEqual(0, _tracker.CurrentIndex);
        }

        [Test]
        public void Chain_FullBasicCombo_ReachesFinisher_ThenCannotAdvance()
        {
            _tracker.Begin();                                         // Jab1
            Assert.IsTrue(TryChainAtFrame(_tracker, Jab1, 10));       // → Jab2
            Assert.IsTrue(TryChainAtFrame(_tracker, Jab2, 9));        // → Finisher
            Assert.IsTrue(_tracker.IsFinisher);

            // Finisher's cancel window is dash-cancel only: no HasNext, so no further chain.
            Assert.IsFalse(TryChainAtFrame(_tracker, Finisher, 25));
            Assert.AreEqual(2, _tracker.CurrentIndex);
        }

        [Test]
        public void Chain_ResetsWhenAttackFinishesWithoutFollowUp()
        {
            // Simulates AttackState: walk the timeline, no Attack press; on IsFinished → Reset (combo timed out).
            _tracker.Begin();
            for (int frame = 0; frame <= Jab1.TotalFrames; frame++)
            {
                if (Jab1.IsFinished(frame))
                {
                    _tracker.Reset();
                    break;
                }
            }

            Assert.IsFalse(_tracker.IsInCombo);
            Assert.AreEqual(-1, _tracker.CurrentIndex);
        }

        [Test]
        public void Chain_ResetsAfterFinisherCompletes_AndNextAttackStartsAtZero()
        {
            _tracker.Begin();
            _tracker.TryAdvance(out _);
            _tracker.TryAdvance(out _);
            Assert.IsTrue(Finisher.IsFinished(Finisher.TotalFrames));

            _tracker.Reset();          // AttackState: Finished → Reset
            int index = _tracker.Begin(); // next Attack press from Idle/Move

            Assert.AreEqual(0, index);
        }

        // ============ With ComboData (as PlayerContext builds it) ============

        [Test]
        public void Tracker_BuiltFromEmptyComboData_HasZeroLength()
        {
            var combo = ScriptableObject.CreateInstance<ComboData>();
            try
            {
                var tracker = new ComboTracker(combo.Count);

                Assert.AreEqual(0, tracker.ComboLength);
                Assert.AreEqual(-1, tracker.Begin());
                Assert.IsNull(combo.GetAttack(0));
            }
            finally
            {
                Object.DestroyImmediate(combo);
            }
        }
    }
}
