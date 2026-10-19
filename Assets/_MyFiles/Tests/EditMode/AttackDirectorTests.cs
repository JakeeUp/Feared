using Brawler.Enemy;
using Brawler.Progression;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="AttackDirector"/>: queue order decides who may close in and attack, attacks are
    /// spaced by the group interval, and attackers rejoin at the back so everyone takes turns.
    /// </summary>
    public sealed class AttackDirectorTests
    {
        // ============ Fixture ============

        private const float Interval = 1f;

        private AttackDirector _director;
        private readonly object _a = new object();
        private readonly object _b = new object();
        private readonly object _c = new object();

        [SetUp]
        public void SetUp()
        {
            _director = new AttackDirector(1, Interval);
        }

        // ============ Tests ============

        [Test]
        public void OnlyTheFrontOfTheQueue_IsFront()
        {
            _director.Enlist(_a);
            _director.Enlist(_b);

            Assert.IsTrue(_director.IsFront(_a));
            Assert.IsFalse(_director.IsFront(_b));
            Assert.IsFalse(_director.IsFront(_c), "Not enlisted -> not front.");
        }

        [Test]
        public void TryBeginAttack_ByTheFront_Succeeds_AndLeavesTheQueue()
        {
            _director.Enlist(_a);
            _director.Enlist(_b);

            Assert.IsTrue(_director.TryBeginAttack(_a, 0f));
            Assert.AreEqual(-1, _director.QueueIndex(_a));
            Assert.AreEqual(0, _director.QueueIndex(_b), "Next in line moves up.");
            Assert.IsTrue(_director.IsFront(_b), "The next enemy may already close in while the first attacks.");
        }

        [Test]
        public void TryBeginAttack_FromBehindTheFront_Fails()
        {
            _director.Enlist(_a);
            _director.Enlist(_b);

            Assert.IsFalse(_director.TryBeginAttack(_b, 0f));
        }

        [Test]
        public void SecondAttack_WaitsForTheTokenAndTheInterval()
        {
            _director.Enlist(_a);
            _director.Enlist(_b);
            _director.TryBeginAttack(_a, 0f);

            Assert.IsFalse(_director.TryBeginAttack(_b, 0.5f), "Token still held by A.");

            _director.EndAttack(_a);
            Assert.IsFalse(_director.TryBeginAttack(_b, 0.5f), "Token free, but the group interval hasn't passed.");
            Assert.IsTrue(_director.TryBeginAttack(_b, Interval));
        }

        [Test]
        public void Attackers_RejoinAtTheBack()
        {
            _director.Enlist(_a);
            _director.Enlist(_b);
            _director.TryBeginAttack(_a, 0f);
            _director.EndAttack(_a);

            _director.Enlist(_a);

            Assert.AreEqual(0, _director.QueueIndex(_b));
            Assert.AreEqual(1, _director.QueueIndex(_a));
        }

        [Test]
        public void Withdraw_RemovesFromQueue_AndReturnsTheToken()
        {
            _director.Enlist(_a);
            _director.TryBeginAttack(_a, 0f);
            _director.Enlist(_b);

            _director.Withdraw(_a);

            Assert.AreEqual(0, _director.AttackingCount);
            Assert.IsTrue(_director.TryBeginAttack(_b, Interval));
        }

        [Test]
        public void Enlist_IsIdempotent_AndIgnoresAttackers()
        {
            _director.Enlist(_a);
            _director.Enlist(_a);
            Assert.AreEqual(1, _director.QueueLength);

            _director.TryBeginAttack(_a, 0f);
            _director.Enlist(_a);
            Assert.AreEqual(0, _director.QueueLength, "An attacker can't queue for another turn until its attack ends.");
        }

        [Test]
        public void TwoAttackers_FrontIsTwoWide()
        {
            var director = new AttackDirector(2, 0f);
            director.Enlist(_a);
            director.Enlist(_b);
            director.Enlist(_c);

            Assert.IsTrue(director.IsFront(_a));
            Assert.IsTrue(director.IsFront(_b));
            Assert.IsFalse(director.IsFront(_c));
        }

        // ============ LevelCurve ============

        [Test]
        public void LevelCurve_GrowsGeometrically_AndClampsInputs()
        {
            Assert.AreEqual(40, LevelCurve.XpToNext(1, 40, 1.5f));
            Assert.AreEqual(60, LevelCurve.XpToNext(2, 40, 1.5f));
            Assert.AreEqual(90, LevelCurve.XpToNext(3, 40, 1.5f));
            Assert.AreEqual(40, LevelCurve.XpToNext(0, 40, 1.5f), "Level below 1 counts as 1.");
            Assert.AreEqual(1, LevelCurve.XpToNext(1, 0, 0f), "Never less than 1 XP.");
        }
    }
}
