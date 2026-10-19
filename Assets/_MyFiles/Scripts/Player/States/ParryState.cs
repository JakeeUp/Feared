using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Parry: for <c>ParryActiveFrames</c> any hit is deflected (PlayerController calls <see cref="OnParried"/>), the
    /// attacker is staggered and the player gets <c>ParrySuccessFrames</c> of invulnerability that any action cancels
    /// (the counter). A parry that catches nothing leaves the player stuck for <c>ParryWhiffRecoveryFrames</c>.
    /// </summary>
    public sealed class ParryState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ Properties (read by presentation) ============

        /// <summary>True after a hit was deflected during this activation.</summary>
        public bool Succeeded { get; private set; }

        /// <summary>Increments on every successful parry so presentation can react once.</summary>
        public int SuccessCount { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            _clock.Reset();
            Succeeded = false;
            context.IsParryActive = true;
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // After a deflect, anything goes: the counter attack, a roll, a jump.
            if (Succeeded && context.TryStartAction())
            {
                return;
            }

            context.Velocity = Vector3.MoveTowards(context.Velocity, Vector3.zero, context.Movement.Deceleration * deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            PlayerMovementSettings movement = context.Movement;

            if (Succeeded)
            {
                if (_clock.Frame >= movement.ParrySuccessFrames)
                {
                    context.ChangeToLocomotion();
                }

                return;
            }

            context.IsParryActive = _clock.Frame < movement.ParryActiveFrames;
            if (_clock.Frame >= movement.ParryActiveFrames + movement.ParryWhiffRecoveryFrames)
            {
                context.ChangeToLocomotion();
            }
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            context.IsParryActive = false;
            context.IsInvulnerable = false;
        }

        // ============ API ============

        /// <summary>Called by PlayerController when a hit lands inside the window: switch to the invulnerable counter phase.</summary>
        public void OnParried(PlayerContext context)
        {
            Succeeded = true;
            SuccessCount++;
            context.IsParryActive = false;
            context.IsInvulnerable = true;
            context.Velocity = Vector3.zero;
            _clock.Reset();
        }
    }
}
