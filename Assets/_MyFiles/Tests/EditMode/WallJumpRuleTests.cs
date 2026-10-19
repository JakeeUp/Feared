using Brawler.Core;
using Brawler.Player;
using NUnit.Framework;
using UnityEngine;

namespace Brawler.Tests
{
    /// <summary>
    /// Wall-climb limits on <see cref="PlayerContext"/>: a wall jump blocks that side until landing or grabbing the opposite
    /// wall, and re-grabbing the same wall doesn't refill air moves. Pure C#: no scene, controller and input are null.
    /// </summary>
    public sealed class WallJumpRuleTests
    {
        // ============ Fixture ============

        private PlayerMovementSettings _movement;
        private PlayerContext _context;

        [SetUp]
        public void SetUp()
        {
            _movement = ScriptableObject.CreateInstance<PlayerMovementSettings>();
            _context = new PlayerContext(null, null, null, new InputBuffer(0.2f, 8), _movement, null, null);
            _context.States = new PlayerStates();
            _context.Machine = new StateMachine<PlayerContext>(_context);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_movement);
        }

        // ============ Tests ============

        /// <summary>After kicking off a wall, that side can't be used again; the other side can.</summary>
        [Test]
        public void StartWallJump_BlocksThatSide_OnlyThatSide()
        {
            _context.WallSide = 1;
            _context.StartWallJump();

            Assert.AreEqual(1, _context.WallJumpBlockedSide);
            Assert.IsFalse(_context.CanUseWall(1));
            Assert.IsTrue(_context.CanUseWall(-1));
            Assert.IsFalse(_context.CanUseWall(0));
        }

        /// <summary>Chimney: grabbing the opposite wall lifts the block, and kicking off it blocks it instead.</summary>
        [Test]
        public void GrabbingOppositeWall_LiftsBlock_ChimneyAlternates()
        {
            _context.WallSide = 1;
            _context.StartWallJump();

            _context.OnWallGrabbed(-1);
            Assert.AreEqual(0, _context.WallJumpBlockedSide);

            _context.WallSide = -1;
            _context.StartWallJump();
            Assert.IsTrue(_context.CanUseWall(1));
            Assert.IsFalse(_context.CanUseWall(-1));
        }

        /// <summary>Landing clears the block.</summary>
        [Test]
        public void ResetWallUse_ClearsBlock()
        {
            _context.WallSide = -1;
            _context.StartWallJump();

            _context.ResetWallUse();

            Assert.IsTrue(_context.CanUseWall(-1));
        }

        /// <summary>Re-grabbing the same wall in one airtime doesn't refill (no drop, double jump, re-grab climbing).</summary>
        [Test]
        public void OnWallGrabbed_SameWallTwice_RefillsOnlyOnce()
        {
            _context.OnWallGrabbed(1);
            Assert.AreEqual(_movement.AirJumps, _context.AirJumpsLeft);

            _context.AirJumpsLeft = 0;
            _context.AirDashesLeft = 0;
            _context.OnWallGrabbed(1);

            Assert.AreEqual(0, _context.AirJumpsLeft);
            Assert.AreEqual(0, _context.AirDashesLeft);

            _context.OnWallGrabbed(-1);
            Assert.AreEqual(_movement.AirJumps, _context.AirJumpsLeft);
            Assert.AreEqual(_movement.AirDashes, _context.AirDashesLeft);
        }
    }
}
