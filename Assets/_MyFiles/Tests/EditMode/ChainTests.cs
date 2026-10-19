using Brawler.Combat;
using Brawler.Player;
using NUnit.Framework;
using UnityEngine;

namespace Brawler.Tests
{
    /// <summary>Expected behavior of the flying <see cref="Chain"/>: flight, arrival, and dropping the hook.</summary>
    public sealed class ChainTests
    {
        private sealed class FakeTarget : IChainTarget
        {
            public ChainWeight ChainWeight { get; set; } = ChainWeight.Light;
            public Vector3 ChainPoint { get; set; }
            public bool CanBeChained { get; set; } = true;

            public bool Yank(Vector3 destination, int stunFrames, int damage, Transform source)
            {
                return true;
            }
        }

        private const float Speed = 50f;
        private const float Range = 8f;

        [Test]
        public void Fire_AtEnemy_FliesFromTheHand()
        {
            var chain = new Chain();
            var target = new FakeTarget { ChainPoint = new Vector3(5f, 0f, 0f) };
            chain.Fire(Vector3.zero, null, target);

            Assert.IsTrue(chain.IsFlying);
            Assert.AreSame(target, chain.Enemy);
            Assert.AreEqual(Vector3.zero, chain.Tip);
        }

        [Test]
        public void Advance_ArrivesOnlyWhenTheTipReachesTheTarget()
        {
            var chain = new Chain();
            var target = new FakeTarget { ChainPoint = new Vector3(5f, 0f, 0f) };
            chain.Fire(Vector3.zero, null, target);

            Assert.IsFalse(chain.Advance(Vector3.zero, Speed, 0.05f, Range)); // 2.5 of 5 units
            Assert.AreEqual(2.5f, chain.Tip.x, 0.001f);
            Assert.IsTrue(chain.Advance(Vector3.zero, Speed, 0.05f, Range));
        }

        [Test]
        public void Advance_FollowsAMovingTarget()
        {
            var chain = new Chain();
            var target = new FakeTarget { ChainPoint = new Vector3(5f, 0f, 0f) };
            chain.Fire(Vector3.zero, null, target);
            chain.Advance(Vector3.zero, Speed, 0.05f, Range);

            target.ChainPoint = new Vector3(2.5f, 3f, 0f);
            Assert.IsFalse(chain.Advance(Vector3.zero, Speed, 0.01f, Range));
            Assert.Greater(chain.Tip.y, 0f);
        }

        [Test]
        public void Advance_TargetNoLongerHookable_DropsTheHook()
        {
            var chain = new Chain();
            var target = new FakeTarget { ChainPoint = new Vector3(5f, 0f, 0f) };
            chain.Fire(Vector3.zero, null, target);

            target.CanBeChained = false;
            Assert.IsFalse(chain.Advance(Vector3.zero, Speed, 0.05f, Range));
            Assert.IsFalse(chain.IsFlying);
            Assert.IsNull(chain.Enemy);
        }

        [Test]
        public void Advance_PlayerFarBeyondRange_DropsTheHook()
        {
            var chain = new Chain();
            var target = new FakeTarget { ChainPoint = new Vector3(5f, 0f, 0f) };
            chain.Fire(Vector3.zero, null, target);

            Assert.IsFalse(chain.Advance(new Vector3(-20f, 0f, 0f), Speed, 0.01f, Range));
            Assert.IsFalse(chain.IsFlying);
        }

        [Test]
        public void Fire_WithNothing_DoesNotFly()
        {
            var chain = new Chain();
            chain.Fire(Vector3.zero, null, null);
            Assert.IsFalse(chain.IsFlying);
            Assert.IsFalse(chain.Advance(Vector3.zero, Speed, 0.05f, Range));
        }
    }
}
