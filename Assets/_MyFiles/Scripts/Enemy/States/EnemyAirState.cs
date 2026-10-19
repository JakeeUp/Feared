using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Mid-jump (started by <see cref="EnemyContext.StartJump"/>) or falling off a ledge. Drifts toward the target at
    /// <c>AirSpeed</c> and returns to the chase on landing. A hit in the air becomes a knockdown (juggle).
    /// </summary>
    public sealed class EnemyAirState : IState<EnemyContext>
    {
        // ============ Constants ============

        /// <summary>Frames before landing can count, so the take-off frame doesn't immediately "land".</summary>
        private const int MinAirFrames = 3;

        /// <summary>Air steering acceleration (units/s^2).</summary>
        private const float AirSteer = 8f;

        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
            _clock.Reset();
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            Vector3 toTarget = context.ToTarget();
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Vector3 desired = toTarget.normalized * context.Settings.AirSpeed;
                context.Velocity = Vector3.MoveTowards(context.Velocity, desired, AirSteer * deltaTime);
            }

            context.FaceTarget();
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            if (_clock.Frame >= MinAirFrames && context.IsGrounded && context.VerticalVelocity <= 0f)
            {
                context.Machine.ChangeState(context.States.Chase);
            }
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
        }
    }
}
