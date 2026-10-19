using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Airborne: rising from a jump, falling off a ledge, or after a wall jump. Gravity and the fall-speed cap live in
    /// <see cref="PlayerContext.Move"/>; this state adds air control, variable jump height and the air transitions.
    /// </summary>
    /// <remarks>
    /// Transitions (Tick, priority order): 1. consume Dash (air dashes left) -&gt; Dash; 2. Jump: coyote -&gt; ground jump,
    /// touching a usable wall -&gt; wall jump, air jumps left -&gt; double jump (left buffered otherwise, so it fires on
    /// landing); 3. consume Attack -&gt; Attack
    /// (air combo, keeps falling); 4. falling + pushing into a usable wall (not the one
    /// last kicked off, see <see cref="PlayerContext.WallJumpBlockedSide"/>) -&gt; WallSlide; 5. after moving, grounded -&gt; Move/Idle.
    /// </remarks>
    public sealed class AirState : IState<PlayerContext>
    {
        // ============ Constants ============

        /// <summary>Minimum |input X| that turns the player (matches MoveState).</summary>
        private const float FacingThreshold = 0.1f;

        /// <summary>Fraction of AirAcceleration used to bleed off speed when there is no input.</summary>
        private const float AirDragFactor = 0.35f;

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            // Velocity and vertical velocity are kept: jumps, ledge walk-offs and wall kicks all carry momentum in.
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            PlayerMovementSettings movement = context.Movement;
            Vector3 input = context.GetMoveInputWorld();
            int inputSide = Mathf.Abs(input.x) > FacingThreshold ? (input.x > 0f ? 1 : -1) : 0;

            if (context.TryStartDash())
            {
                return;
            }

            if (TryJump(context, inputSide))
            {
                return;
            }

            if (context.TryStartAttack())
            {
                return;
            }

            // Grab a wall only when falling and actively pushing into it, like most wall-slide platformers.
            // The wall just kicked off is off limits until landing or grabbing the opposite wall (no single-wall climb).
            if (context.VerticalVelocity <= 0f && context.CanUseWall(inputSide) && context.IsTouchingWall(inputSide))
            {
                context.WallSide = inputSide;
                context.Machine.ChangeState(context.States.WallSlide);
                return;
            }

            context.ApplyJumpCut();
            if (context.IsDoubleJumping && context.VerticalVelocity <= 0f)
            {
                context.IsDoubleJumping = false;
            }

            // Right after a wall jump only part of the air control applies, so the kick-off carries without locking.
            float control = 1f;
            if (context.WallJumpLockTimer > 0f)
            {
                context.WallJumpLockTimer -= deltaTime;
                control = movement.WallJumpControl;
            }
            else if (inputSide != 0)
            {
                context.FacingSign = inputSide;
            }

            Vector3 target = input * movement.MoveSpeed;

            // Keep over-speed momentum (swing releases, wall kicks) while the stick agrees with it; letting go bleeds it off.
            if (Mathf.Abs(context.Velocity.x) > movement.MoveSpeed && input.x * context.Velocity.x > 0f)
            {
                target.x = context.Velocity.x;
            }

            float rate = (input.sqrMagnitude > 0f ? movement.AirAcceleration : movement.AirAcceleration * AirDragFactor) * control;
            context.Velocity = Vector3.MoveTowards(context.Velocity, target, rate * deltaTime);

            context.Move(context.Velocity, deltaTime);

            if (context.IsGrounded && context.VerticalVelocity <= 0f)
            {
                context.ChangeToLocomotion();
            }
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            context.WallJumpLockTimer = 0f;
        }

        // ============ Helpers ============

        /// <summary>Coyote jump, wall jump off an adjacent wall, or double jump. Leaves Jump buffered when none applies.</summary>
        private static bool TryJump(PlayerContext context, int inputSide)
        {
            if (!context.Buffer.Peek(BufferedCommand.Jump, context.Time))
            {
                return false;
            }

            if (context.CanGroundJump)
            {
                context.Buffer.TryConsume(BufferedCommand.Jump, context.Time);
                context.StartJump();
                return true;
            }

            // Prefer the wall the stick points at, then the other side. The wall just kicked off doesn't count.
            int side = IsUsableWall(context, inputSide) ? inputSide
                : IsUsableWall(context, 1) ? 1
                : IsUsableWall(context, -1) ? -1
                : 0;

            if (side != 0)
            {
                context.Buffer.TryConsume(BufferedCommand.Jump, context.Time);
                context.WallSide = side;
                context.StartWallJump();
                return true;
            }

            if (context.AirJumpsLeft > 0)
            {
                context.Buffer.TryConsume(BufferedCommand.Jump, context.Time);
                context.StartDoubleJump();
                return false; // still airborne: keep running this tick (air control, move)
            }

            return false;
        }

        /// <summary>True when a wall on <paramref name="side"/> is touched and not blocked by the last wall jump.</summary>
        private static bool IsUsableWall(PlayerContext context, int side)
        {
            return context.CanUseWall(side) && context.IsTouchingWall(side);
        }
    }
}
