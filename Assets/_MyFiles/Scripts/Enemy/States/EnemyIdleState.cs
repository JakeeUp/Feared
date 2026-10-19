using Brawler.Core;

namespace Brawler.Enemy
{
    /// <summary>Stands still until the target comes within <c>AggroRange</c>, then chases.</summary>
    public sealed class EnemyIdleState : IState<EnemyContext>
    {
        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            if (context.Target != null && context.ToTarget().magnitude <= context.Settings.AggroRange)
            {
                context.Machine.ChangeState(context.States.Chase);
                return;
            }

            context.Decelerate(context.Settings.Acceleration, deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
        }
    }
}
