using Brawler.Core;
using UnityEngine;

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
            // Velocity is kept so Tick eases out of a dash or knockback slide instead of snapping.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            if (context.TryStartAction())
            {
                return;
            }

            if (context.HasLeftGround())
            {
                context.Machine.ChangeState(context.States.Air);
                return;
            }

            if (context.HasMoveInput())
            {
                context.Machine.ChangeState(context.States.Move);
                return;
            }

            // Always Move, even at rest: that's what applies gravity/grounding.
            context.Velocity = Vector3.MoveTowards(context.Velocity, Vector3.zero, context.Movement.Deceleration * deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
        }
    }
}
