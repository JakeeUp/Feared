using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Running on the X/Z plane. Accelerates toward input * MoveSpeed and updates facing from input X.
    /// Transitions (Tick, §4.1 priority order): 1. consume Dash -&gt; Dash; 2. consume Attack -&gt; Attack (ComboTracker.Begin);
    /// 3. !HasMoveInput -&gt; Idle. Hitstun is entered externally (priority 0).
    /// </summary>
    public sealed class MoveState : IState<PlayerContext>
    {
        // ============ Constants ============

        /// <summary>Minimum |input X| that turns the player, so near-pure depth input keeps the current facing.</summary>
        private const float FacingThreshold = 0.1f;

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // Velocity is kept so momentum from a dash carries into the run.
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

            if (!context.HasMoveInput())
            {
                context.Machine.ChangeState(context.States.Idle);
                return;
            }

            Vector3 input = context.GetMoveInputWorld();
            if (Mathf.Abs(input.x) > FacingThreshold)
            {
                context.FacingSign = input.x > 0f ? 1 : -1;
            }

            // Deceleration when slowing down or turning reads heavier than accelerating through a reversal.
            PlayerMovementSettings movement = context.Movement;
            Vector3 target = input * movement.MoveSpeed;
            bool slowing = target.sqrMagnitude < context.Velocity.sqrMagnitude || Vector3.Dot(target, context.Velocity) < 0f;
            float rate = slowing ? movement.Deceleration : movement.Acceleration;

            context.Velocity = Vector3.MoveTowards(context.Velocity, target, rate * deltaTime);
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
