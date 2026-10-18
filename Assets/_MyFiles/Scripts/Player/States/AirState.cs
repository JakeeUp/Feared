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
    /// touching a wall -&gt; wall jump, air jumps left -&gt; double jump (left buffered otherwise, so it fires on
    /// landing); 3. consume Attack -&gt; Attack
    /// (air combo, keeps falling); 4. falling + pushing into a wall -&gt; WallSlide; 5. after moving, grounded -&gt; Move/Idle.
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

            if (context.AirDashesLeft > 0 && context.Buffer.TryConsume(BufferedCommand.Dash, context.Time))
            {
                context.AirDashesLeft--;
                context.Machine.ChangeState(context.States.Dash);
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
            if (context.VerticalVelocity <= 0f && inputSide != 0 && context.IsTouchingWall(inputSide))
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

            if (context.WallJumpLockTimer > 0f)
            {
                context.WallJumpLockTimer -= deltaTime;
            }
            else
            {
                if (inputSide != 0)
                {
                    context.FacingSign = inputSide;
                }

                Vector3 target = input * movement.MoveSpeed;
                float rate = input.sqrMagnitude > 0f ? movement.AirAcceleration : movement.AirAcceleration * AirDragFactor;
                context.Velocity = Vector3.MoveTowards(context.Velocity, target, rate * deltaTime);
            }

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

            // Prefer the wall the stick points at, then the other side.
            int side = inputSide != 0 && context.IsTouchingWall(inputSide) ? inputSide
                : context.IsTouchingWall(1) ? 1
                : context.IsTouchingWall(-1) ? -1
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
    }
}
