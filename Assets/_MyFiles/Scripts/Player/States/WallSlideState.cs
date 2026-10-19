using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// On a wall at <see cref="PlayerContext.WallSide"/>: hangs still for <c>WallGrabFrames</c> (the grab), then slides
    /// down at <c>WallSlideSpeed</c>. Faces away from the wall so a wall jump or attack goes the natural way.
    /// </summary>
    /// <remarks>
    /// Transitions (Tick): 1. consume Jump -&gt; wall jump (Air); 2. consume Dash -&gt; Dash (away from the wall unless
    /// steered); 3. grounded -&gt; Move/Idle; 4. stick released / pushed away, or the wall ends -&gt; Air.
    /// Grabbing a different wall than the last one this airtime refills air dashes and air jumps.
    /// </remarks>
    public sealed class WallSlideState : IState<PlayerContext>
    {
        // ============ Constants ============

        /// <summary>Small push into the wall (units/s) so the capsule keeps contact while sliding.</summary>
        private const float StickSpeed = 1f;

        /// <summary>Minimum |input X| that counts as holding toward the wall.</summary>
        private const float HoldThreshold = 0.1f;

        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ Properties (read by presentation) ============

        /// <summary>True during the initial hang, before the slide starts.</summary>
        public bool IsGrabbing { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            _clock.Reset();
            IsGrabbing = context.Movement.WallGrabFrames > 0;
            context.Velocity = Vector3.zero;
            context.VerticalVelocity = 0f;
            context.FacingSign = -context.WallSide;
            context.OnWallGrabbed(context.WallSide); // refills only for a new wall; lifts the block on the other side
            context.IsDoubleJumping = false;
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            if (context.Buffer.TryConsume(BufferedCommand.Jump, context.Time))
            {
                context.StartWallJump();
                return;
            }

            if (context.Buffer.TryConsume(BufferedCommand.Dash, context.Time))
            {
                context.Machine.ChangeState(context.States.Dash);
                return;
            }

            if (context.IsGrounded)
            {
                context.ChangeToLocomotion();
                return;
            }

            float inputX = context.HasMoveInput() ? context.Input.Move.x : 0f;
            bool holding = inputX * context.WallSide > HoldThreshold;
            if (!holding || !context.IsTouchingWall(context.WallSide))
            {
                context.Machine.ChangeState(context.States.Air);
                return;
            }

            context.Velocity = new Vector3(context.WallSide * StickSpeed, 0f, 0f);
            float fallCap = IsGrabbing ? 0f : context.Movement.WallSlideSpeed;
            context.Move(context.Velocity, deltaTime, fallCap);
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            IsGrabbing = _clock.Frame < context.Movement.WallGrabFrames;
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            IsGrabbing = false;
        }
    }
}
