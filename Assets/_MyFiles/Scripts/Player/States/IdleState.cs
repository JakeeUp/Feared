using Brawler.Core;

namespace Brawler.Player
{
    /// <summary>
    /// Standing still. Decelerates residual velocity to zero and waits for input.
    /// Transitions (Tick, §4.1 priority order — stop at the first that fires so only one command is consumed):
    /// 1. consume Dash -&gt; Dash; 2. consume Attack -&gt; Attack (ComboTracker.Begin); 3. HasMoveInput -&gt; Move.
    /// Hitstun is entered externally by PlayerController.TryApplyDamage (priority 0).
    /// </summary>
    public sealed class IdleState : IState<PlayerContext>
    {
        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // TODO: nothing required. Don't zero velocity here - letting Tick decelerate it makes stopping after a dash or a
            // knockback slide look smooth instead of snapping.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // TODO: transitions first, in priority order, returning right after any ChangeState:
            // dash (buffered) -> attack (buffered, start the combo) -> move input.
            // Use TryConsume directly rather than Peek-then-consume so one check can't eat two commands.
            // Then decelerate and always call Move, even at zero velocity - that's what applies gravity/grounding.
            // Facing doesn't change in Idle.
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            // TODO: nothing - Idle has no frame timeline.
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // TODO: nothing.
        }
    }
}
