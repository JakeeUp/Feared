using Brawler.Core;
using Brawler.Player;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Wiring contracts of the player object graph that are implemented on Day One (should already pass):
    /// <see cref="PlayerContext"/> constructor defaults and <see cref="PlayerStates"/> instance ownership.
    /// Pure C# — no scene, MonoBehaviours passed as null.
    /// </summary>
    public sealed class PlayerWiringTests
    {
        /// <summary>The context starts facing right (IFacingSource contract: never 0) and outside a combo.</summary>
        [Test]
        public void PlayerContext_Constructor_FacesRight_AndBuildsEmptyComboTracker_WhenComboIsNull()
        {
            var buffer = new InputBuffer(0.2f, 8);
            var context = new PlayerContext(null, null, null, buffer, null, null, null);

            Assert.AreEqual(1, context.FacingSign);
            Assert.AreSame(buffer, context.Buffer);
            Assert.IsNotNull(context.ComboTracker);
            Assert.AreEqual(0, context.ComboTracker.ComboLength);
            Assert.AreEqual(-1, context.ComboTracker.CurrentIndex);
            Assert.IsFalse(context.IsInvulnerable);
        }

        /// <summary>Each state exists exactly once and the same instance is returned every time (no per-transition allocation).</summary>
        [Test]
        public void PlayerStates_CreatesOneDistinctInstancePerState()
        {
            var states = new PlayerStates();

            Assert.IsNotNull(states.Idle);
            Assert.IsNotNull(states.Move);
            Assert.IsNotNull(states.Attack);
            Assert.IsNotNull(states.Dash);
            Assert.IsNotNull(states.Hitstun);
            Assert.AreSame(states.Idle, states.Idle);
            Assert.AreNotSame(new PlayerStates().Idle, states.Idle);
        }

        /// <summary>States plug into the generic machine: ChangeState(Idle) makes Idle current (needs StateMachine implemented).</summary>
        [Test]
        public void StateMachine_WithPlayerStates_EntersIdle()
        {
            var context = new PlayerContext(null, null, null, new InputBuffer(0.2f, 8), null, null, null);
            context.States = new PlayerStates();
            context.Machine = new StateMachine<PlayerContext>(context);

            context.Machine.ChangeState(context.States.Idle);

            Assert.IsTrue(context.Machine.IsInState(context.States.Idle));
        }
    }
}
