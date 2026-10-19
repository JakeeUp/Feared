using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Flinch from a normal hit: no control for <c>LastHit.HitstunFrames</c> while the knockback slides out.
    /// A re-hit re-enters (refreshing the stun), which is what lets the player's combo connect.
    /// </summary>
    public sealed class EnemyHitstunState : IState<EnemyContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ Properties (read by presentation) ============

        /// <summary>Increments on every Enter (each hit re-enters), so presentation can restart the flinch.</summary>
        public int EntryCount { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
            // Knockback is applied right after the damage call, so zeroing here is safe.
            context.Velocity = Vector3.zero;
            context.FaceTarget();
            _clock.Reset();
            EntryCount++;
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            context.Decelerate(context.Settings.KnockbackDeceleration, deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            if (_clock.Frame >= context.LastHit.HitstunFrames && (context.IsGrounded || context.IsFlying))
            {
                context.Machine.ChangeState(context.States.Chase);
            }
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
            // A yanked flyer takes off again once the stun is over.
            context.FlightSuppressed = false;
        }
    }
}
