using System;
using System.Collections.Generic;
using Brawler.Core;
using NUnit.Framework;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="StateMachine{TContext}"/>: Enter/Exit ordering, same-state re-entry,
    /// null handling, Tick/FixedTick forwarding and <see cref="StateMachine{TContext}.IsInState"/>.
    /// </summary>
    public sealed class StateMachineTests
    {
        // ============ Fakes ============

        /// <summary>Minimal context so tests can verify the machine passes it through.</summary>
        private sealed class TestContext
        {
        }

        /// <summary>Hand-written fake that records every callback into a shared, ordered log.</summary>
        private sealed class FakeState : IState<TestContext>
        {
            private readonly string _name;
            private readonly List<string> _log;

            public FakeState(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public TestContext LastContext { get; private set; }
            public float LastDeltaTime { get; private set; }
            public float LastFixedDeltaTime { get; private set; }
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public int TickCount { get; private set; }
            public int FixedTickCount { get; private set; }

            /// <summary>Optional hook run at the end of Enter (used for re-entrancy tests).</summary>
            public Action OnEnter { get; set; }

            public void Enter(TestContext context)
            {
                LastContext = context;
                EnterCount++;
                _log.Add(_name + ".Enter");
                OnEnter?.Invoke();
            }

            public void Tick(TestContext context, float deltaTime)
            {
                LastContext = context;
                LastDeltaTime = deltaTime;
                TickCount++;
                _log.Add(_name + ".Tick");
            }

            public void FixedTick(TestContext context, float fixedDeltaTime)
            {
                LastContext = context;
                LastFixedDeltaTime = fixedDeltaTime;
                FixedTickCount++;
                _log.Add(_name + ".FixedTick");
            }

            public void Exit(TestContext context)
            {
                LastContext = context;
                ExitCount++;
                _log.Add(_name + ".Exit");
            }
        }

        // ============ Fixture ============

        private List<string> _log;
        private TestContext _context;
        private StateMachine<TestContext> _machine;
        private FakeState _a;
        private FakeState _b;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _context = new TestContext();
            _machine = new StateMachine<TestContext>(_context);
            _a = new FakeState("A", _log);
            _b = new FakeState("B", _log);
        }

        // ============ Construction ============

        [Test]
        public void Constructor_StoresContext()
        {
            Assert.AreSame(_context, _machine.Context);
        }

        [Test]
        public void CurrentState_IsNull_BeforeFirstChangeState()
        {
            Assert.IsNull(_machine.CurrentState);
            Assert.IsNull(_machine.PreviousState);
        }

        // ============ ChangeState ============

        [Test]
        public void ChangeState_FirstTime_EntersNewStateOnly()
        {
            _machine.ChangeState(_a);

            CollectionAssert.AreEqual(new[] { "A.Enter" }, _log);
            Assert.AreSame(_a, _machine.CurrentState);
            Assert.IsNull(_machine.PreviousState, "No previous state after the very first change.");
        }

        [Test]
        public void ChangeState_ExitsOldStateBeforeEnteringNewState()
        {
            _machine.ChangeState(_a);
            _log.Clear();

            _machine.ChangeState(_b);

            CollectionAssert.AreEqual(new[] { "A.Exit", "B.Enter" }, _log);
        }

        [Test]
        public void ChangeState_UpdatesCurrentAndPreviousState()
        {
            _machine.ChangeState(_a);
            _machine.ChangeState(_b);

            Assert.AreSame(_b, _machine.CurrentState);
            Assert.AreSame(_a, _machine.PreviousState);
        }

        [Test]
        public void ChangeState_PassesMachineContextToEnterAndExit()
        {
            _machine.ChangeState(_a);
            Assert.AreSame(_context, _a.LastContext, "Enter received the context.");

            _machine.ChangeState(_b);
            Assert.AreSame(_context, _a.LastContext, "Exit received the context.");
            Assert.AreSame(_context, _b.LastContext);
        }

        [Test]
        public void ChangeState_WithNull_IsIgnored()
        {
            _machine.ChangeState(_a);
            _log.Clear();

            _machine.ChangeState(null);

            Assert.AreSame(_a, _machine.CurrentState, "Current state must be unchanged.");
            Assert.IsNull(_machine.PreviousState, "PreviousState must not change on an ignored call.");
            CollectionAssert.IsEmpty(_log, "Null must not Exit the current state.");
        }

        [Test]
        public void ChangeState_WithNull_BeforeAnyState_DoesNothing()
        {
            Assert.DoesNotThrow(() => _machine.ChangeState(null));
            Assert.IsNull(_machine.CurrentState);
        }

        [Test]
        public void ChangeState_ToSameState_ExitsThenReEnters()
        {
            _machine.ChangeState(_a);
            _log.Clear();

            _machine.ChangeState(_a);

            CollectionAssert.AreEqual(new[] { "A.Exit", "A.Enter" }, _log, "HitstunState relies on same-state re-entry.");
            Assert.AreEqual(2, _a.EnterCount);
            Assert.AreEqual(1, _a.ExitCount);
        }

        [Test]
        public void ChangeState_ToSameState_PreviousStateIsThatState()
        {
            _machine.ChangeState(_a);
            _machine.ChangeState(_a);

            Assert.AreSame(_a, _machine.CurrentState);
            Assert.AreSame(_a, _machine.PreviousState);
        }

        [Test]
        public void ChangeState_LongChain_LogsEveryTransitionInOrder()
        {
            _machine.ChangeState(_a);
            _machine.ChangeState(_b);
            _machine.ChangeState(_a);

            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Exit", "B.Enter", "B.Exit", "A.Enter" }, _log);
            Assert.AreSame(_b, _machine.PreviousState);
        }

        [Test]
        public void ChangeState_CalledFromInsideEnter_LatestStateWins()
        {
            // Documented behavior: allowed; the most recently requested state ends up current.
            _a.OnEnter = () => _machine.ChangeState(_b);

            _machine.ChangeState(_a);

            Assert.AreSame(_b, _machine.CurrentState);
            Assert.AreSame(_a, _machine.PreviousState);
            CollectionAssert.AreEqual(new[] { "A.Enter", "A.Exit", "B.Enter" }, _log);
        }

        // ============ Tick / FixedTick ============

        [Test]
        public void Tick_ForwardsContextAndDeltaToCurrentState()
        {
            _machine.ChangeState(_a);

            _machine.Tick(0.016f);

            Assert.AreEqual(1, _a.TickCount);
            Assert.AreEqual(0.016f, _a.LastDeltaTime);
            Assert.AreSame(_context, _a.LastContext);
        }

        [Test]
        public void FixedTick_ForwardsContextAndDeltaToCurrentState()
        {
            _machine.ChangeState(_a);

            _machine.FixedTick(0.02f);

            Assert.AreEqual(1, _a.FixedTickCount);
            Assert.AreEqual(0.02f, _a.LastFixedDeltaTime);
            Assert.AreSame(_context, _a.LastContext);
        }

        [Test]
        public void Tick_DoesNotCallFixedTick_AndViceVersa()
        {
            _machine.ChangeState(_a);

            _machine.Tick(0.016f);
            Assert.AreEqual(0, _a.FixedTickCount);

            _machine.FixedTick(0.02f);
            Assert.AreEqual(1, _a.TickCount);
            Assert.AreEqual(1, _a.FixedTickCount);
        }

        [Test]
        public void Tick_WithZeroDelta_IsStillForwarded()
        {
            // During hitstop Time.deltaTime is 0; states still get ticked (and decide to do nothing).
            _machine.ChangeState(_a);

            _machine.Tick(0f);

            Assert.AreEqual(1, _a.TickCount);
            Assert.AreEqual(0f, _a.LastDeltaTime);
        }

        [Test]
        public void Tick_WithNoState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _machine.Tick(0.016f));
        }

        [Test]
        public void FixedTick_WithNoState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _machine.FixedTick(0.02f));
        }

        [Test]
        public void Tick_AfterChangeState_OnlyTicksNewState()
        {
            _machine.ChangeState(_a);
            _machine.ChangeState(_b);

            _machine.Tick(0.016f);
            _machine.FixedTick(0.02f);

            Assert.AreEqual(0, _a.TickCount);
            Assert.AreEqual(0, _a.FixedTickCount);
            Assert.AreEqual(1, _b.TickCount);
            Assert.AreEqual(1, _b.FixedTickCount);
        }

        // ============ IsInState ============

        [Test]
        public void IsInState_ReturnsTrue_ForCurrentState()
        {
            _machine.ChangeState(_a);

            Assert.IsTrue(_machine.IsInState(_a));
        }

        [Test]
        public void IsInState_ReturnsFalse_ForOtherState()
        {
            _machine.ChangeState(_a);

            Assert.IsFalse(_machine.IsInState(_b));
        }

        [Test]
        public void IsInState_ReturnsFalse_ForPreviousStateAfterChange()
        {
            _machine.ChangeState(_a);
            _machine.ChangeState(_b);

            Assert.IsFalse(_machine.IsInState(_a));
            Assert.IsTrue(_machine.IsInState(_b));
        }

        [Test]
        public void IsInState_UsesReferenceEquality_NotType()
        {
            var anotherA = new FakeState("A", _log);
            _machine.ChangeState(_a);

            Assert.IsFalse(_machine.IsInState(anotherA), "Two instances of the same state class are different states.");
        }

        [Test]
        public void IsInState_ReturnsFalse_ForNull_OnceAStateIsActive()
        {
            _machine.ChangeState(_a);

            Assert.IsFalse(_machine.IsInState(null));
        }
    }
}
