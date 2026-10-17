namespace Brawler.Core
{
    /// <summary>
    /// A single state driven by <c>StateMachine&lt;TContext&gt;</c>. States are plain C# objects
    /// (not MonoBehaviours) and receive their shared data through <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">Shared data/services object the states operate on (e.g. <c>PlayerContext</c>).</typeparam>
    public interface IState<in TContext>
    {
        /// <summary>Called once when the state becomes current (also on same-state re-entry).</summary>
        /// <param name="context">The machine's context.</param>
        void Enter(TContext context);

        /// <summary>Per-render-frame update (driven from <c>Update</c>). Input reading, transitions, CharacterController movement.</summary>
        /// <param name="context">The machine's context.</param>
        /// <param name="deltaTime">Scaled frame delta in seconds (<c>Time.deltaTime</c>).</param>
        void Tick(TContext context, float deltaTime);

        /// <summary>Fixed-step update (driven from <c>FixedUpdate</c>). Frame-data timelines, hitbox sweeps, i-frame/hitstun counters.</summary>
        /// <param name="context">The machine's context.</param>
        /// <param name="fixedDeltaTime">Scaled fixed delta in seconds (<c>Time.fixedDeltaTime</c>).</param>
        void FixedTick(TContext context, float fixedDeltaTime);

        /// <summary>Called once when the state stops being current (also on same-state re-entry, before Enter).</summary>
        /// <param name="context">The machine's context.</param>
        void Exit(TContext context);
    }
}
