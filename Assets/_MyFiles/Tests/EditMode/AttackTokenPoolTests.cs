using Brawler.Enemy;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="AttackTokenPool"/>: at most Capacity holders, idempotent acquire/release,
    /// and a released token is immediately available to someone else.
    /// </summary>
    public sealed class AttackTokenPoolTests
    {
        // ============ Fixture ============

        private AttackTokenPool _pool;
        private readonly object _a = new object();
        private readonly object _b = new object();
        private readonly object _c = new object();

        [SetUp]
        public void SetUp()
        {
            _pool = new AttackTokenPool(2);
        }

        // ============ Tests ============

        [Test]
        public void Constructor_ClampsCapacityToAtLeastOne()
        {
            Assert.AreEqual(1, new AttackTokenPool(0).Capacity);
        }

        [Test]
        public void TryAcquire_UpToCapacity_Succeeds_ThenFails()
        {
            Assert.IsTrue(_pool.TryAcquire(_a));
            Assert.IsTrue(_pool.TryAcquire(_b));
            Assert.IsFalse(_pool.TryAcquire(_c), "Third attacker must wait.");
            Assert.AreEqual(2, _pool.Count);
        }

        [Test]
        public void TryAcquire_Twice_ByTheSameHolder_UsesOneToken()
        {
            Assert.IsTrue(_pool.TryAcquire(_a));
            Assert.IsTrue(_pool.TryAcquire(_a));
            Assert.AreEqual(1, _pool.Count);
        }

        [Test]
        public void TryAcquire_Null_Fails()
        {
            Assert.IsFalse(_pool.TryAcquire(null));
            Assert.AreEqual(0, _pool.Count);
        }

        [Test]
        public void Release_FreesTheTokenForSomeoneElse()
        {
            _pool.TryAcquire(_a);
            _pool.TryAcquire(_b);

            _pool.Release(_a);

            Assert.IsFalse(_pool.Holds(_a));
            Assert.IsTrue(_pool.Holds(_b), "Releasing one holder must not disturb the others.");
            Assert.IsTrue(_pool.TryAcquire(_c));
        }

        [Test]
        public void Release_WhenNotHolding_IsANoOp()
        {
            _pool.TryAcquire(_a);

            _pool.Release(_b);
            _pool.Release(null);

            Assert.AreEqual(1, _pool.Count);
            Assert.IsTrue(_pool.Holds(_a));
        }

        [Test]
        public void CanAcquire_IsTrueForHolders_EvenWhenFull()
        {
            _pool.TryAcquire(_a);
            _pool.TryAcquire(_b);

            Assert.IsTrue(_pool.CanAcquire(_a));
            Assert.IsFalse(_pool.CanAcquire(_c));
        }

        [Test]
        public void Clear_ReturnsEveryToken()
        {
            _pool.TryAcquire(_a);
            _pool.TryAcquire(_b);

            _pool.Clear();

            Assert.AreEqual(0, _pool.Count);
            Assert.IsTrue(_pool.TryAcquire(_c));
        }
    }
}
