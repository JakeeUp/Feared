using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Hit reaction: no control for <c>LastHit.HitstunFrames</c>, sliding with knockback that decelerates.
    /// Entered from any state by <c>PlayerController.TryApplyDamage</c>; a re-hit re-enters this same state
    /// (StateMachine performs Exit then Enter), which refreshes the timeline with the new hit's hitstun.
    /// </summary>
    /// <remarks>Transition (FixedTick, §4.1): frame &gt;= LastHit.HitstunFrames -&gt; Move if input, else Idle.</remarks>
    public sealed class HitstunState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // No mashing out. Velocity zeroing is safe: Hitbox applies knockback right after the damage call.
            context.Buffer.Clear();
            if (context.Hitbox != null)
            {
                context.Hitbox.EndAttack();
            }

            context.ComboTracker.Reset();
            context.IsInvulnerable = false;
            context.Velocity = Vector3.zero;
            _clock.Reset();

            // Turn to face the attacker.
            Transform source = context.LastHit.Source;
            if (source != null && context.Owner != null)
            {
                float dx = source.position.x - context.Owner.transform.position.x;
                if (Mathf.Abs(dx) > 0.01f)
                {
                    context.FacingSign = dx > 0f ? 1 : -1;
                }
            }
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            context.Velocity = Vector3.MoveTowards(context.Velocity, Vector3.zero, context.Movement.KnockbackDeceleration * deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            if (_clock.Frame >= context.LastHit.HitstunFrames)
            {
                context.ChangeToLocomotion();
            }
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // Residual knockback is kept; Idle/Move ease out of it.
        }
    }
}
