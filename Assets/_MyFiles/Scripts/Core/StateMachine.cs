namespace Brawler.Core
{
    /// <summary>
    /// Minimal, allocation-free finite state machine. Holds one current <see cref="IState{TContext}"/>
    /// and forwards Update/FixedUpdate ticks to it. States are plain C# objects that receive
    /// <see cref="Context"/> on every call, so a single state instance can be reused for the machine's lifetime.
    /// </summary>
    /// <typeparam name="TContext">Shared data/services object passed to every state call (e.g. <c>PlayerContext</c>).</typeparam>
    /// <remarks>
    /// Pure C# (no MonoBehaviour) so it is EditMode-testable. The owner (e.g. <c>PlayerController</c>)
    /// calls <see cref="Tick"/> from Update and <see cref="FixedTick"/> from FixedUpdate.
    /// </remarks>
    public sealed class StateMachine<TContext>
    {
        // ============ State ============

        private readonly TContext _context;
        private IState<TContext> _currentState;
        private IState<TContext> _previousState;

        // ============ Construction ============

        /// <summary>Creates a machine bound to <paramref name="context"/>. No state is entered until the first <see cref="ChangeState"/>.</summary>
        /// <param name="context">Context passed to every state callback. Stored as-is (may be a class or struct).</param>
        public StateMachine(TContext context)
        {
            _context = context;
        }

        // ============ Properties ============

        /// <summary>The context passed to every state callback.</summary>
        public TContext Context => _context;

        /// <summary>The active state, or null until the first <see cref="ChangeState"/>.</summary>
        public IState<TContext> CurrentState => _currentState;

        /// <summary>The state that was current before the most recent swap, or null if there has been at most one swap.</summary>
        public IState<TContext> PreviousState => _previousState;

        // ============ API ============

        /// <summary>
        /// Exits the current state (if any) and enters <paramref name="next"/>. Passing the SAME instance as the
        /// current state re-enters it (Exit, then Enter) — <c>HitstunState</c> relies on this to refresh on re-hit.
        /// </summary>
        /// <param name="next">State to enter. Null is ignored (no Exit, no swap).</param>
        public void ChangeState(IState<TContext> next)
        {
            if (next == null)
            {
                return;
            }

            // Same instance is deliberately not short-circuited: hitstun refresh and attack chaining re-enter.
            _currentState?.Exit(_context);
            _previousState = _currentState;
            _currentState = next;
            _currentState.Enter(_context);
        }

        /// <summary>Forwards a render-frame update to the current state (no-op when there is none).</summary>
        /// <param name="deltaTime">Scaled frame delta in seconds (<c>Time.deltaTime</c>).</param>
        public void Tick(float deltaTime)
        {
            _currentState?.Tick(_context, deltaTime);
        }

        /// <summary>Forwards a fixed-step update to the current state (no-op when there is none).</summary>
        /// <param name="fixedDeltaTime">Scaled fixed delta in seconds (<c>Time.fixedDeltaTime</c>).</param>
        public void FixedTick(float fixedDeltaTime)
        {
            _currentState?.FixedTick(_context, fixedDeltaTime);
        }

        /// <summary>True when <paramref name="state"/> is the current state (reference equality).</summary>
        /// <param name="state">State instance to compare.</param>
        /// <returns>True if <paramref name="state"/> is the very same instance as <see cref="CurrentState"/>.</returns>
        public bool IsInState(IState<TContext> state)
        {
            return ReferenceEquals(_currentState, state);
        }
    }
}
