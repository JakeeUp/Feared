using Brawler.Core;

namespace Brawler.Player
{
    /// <summary>
    /// Running on the X/Z plane. Accelerates toward input * MoveSpeed and updates facing from input X.
    /// Transitions (Tick, §4.1 priority order): 1. consume Dash -&gt; Dash; 2. consume Attack -&gt; Attack (ComboTracker.Begin);
    /// 3. !HasMoveInput -&gt; Idle. Hitstun is entered externally (priority 0).
    /// </summary>
    public sealed class MoveState : IState<PlayerContext>
    {
        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // TODO: nothing required. Keep current velocity so momentum from a dash carries into the run.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // TODO: same transitions as Idle (dash -> attack -> back to Idle when the stick is neutral), then:
            // - facing from input X, only when X is non-zero (pure depth input keeps the current facing, never 0)
            // - accelerate toward input * MoveSpeed with MoveTowards; using Deceleration when slowing down/turning reads better
            // - Move.
            // Accel/decel numbers are where most of the "weight" comes from - Steve Swink's Game Feel is a good read on this.
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            // TODO: nothing - locomotion lives in Tick (CharacterController doesn't interpolate).
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // TODO: nothing. Don't zero velocity - attack lunge and dash overwrite it anyway, and Idle eases it out.
        }
    }
}
