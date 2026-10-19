using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Evasive burst along input direction (or facing when neutral). Two flavours chosen at Enter:
    /// on the ground it is a <b>dodge roll</b> (RollFrames long, eases out, i-frames in [RollIFrameStart, RollIFrameEnd));
    /// in the air (or off a wall) it is a <b>dash</b> (DashFrames long, constant speed, holds altitude, i-frames in
    /// [DashIFrameStart, DashIFrameEnd)). Timeline on a private <see cref="FrameClock"/>.
    /// </summary>
    /// <remarks>
    /// Transitions (FixedTick, after advancing — §4.1): when the timeline ends: 1. consume Attack -&gt; Attack
    /// (ComboTracker.Begin; dash-attack buffering); 2. else Air / Move / Idle.
    /// Tick: a roll from <c>RollCancelFrame</c> on -&gt; buffered Jump (StartJump) or Attack.
    /// Hitstun: external, only reachable outside i-frames (Hitbox skips invulnerable hurtboxes).
    /// </remarks>
    public sealed class DashState : IState<PlayerContext>
    {
        // ============ Constants ============

        /// <summary>Minimum |input X| that turns the player (matches MoveState).</summary>
        private const float FacingThreshold = 0.1f;

        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private Vector3 _direction; // unit planar direction, locked at Enter

        // ============ Properties (read by presentation) ============

        /// <summary>True when this activation is a ground roll rather than an air/wall dash.</summary>
        public bool IsRoll { get; private set; }

        /// <summary>Timeline progress, 0 at Enter to 1 at the end (drives the roll animation).</summary>
        public float Progress { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            bool fromWall = ReferenceEquals(context.Machine.PreviousState, context.States.WallSlide);
            Vector3 input = context.GetMoveInputWorld();
            if (fromWall)
            {
                // Off a wall the stick is held into it, so always dash away.
                _direction = new Vector3(-context.WallSide, 0f, 0f);
                context.FacingSign = -context.WallSide;
            }
            else if (input.sqrMagnitude > 0f)
            {
                // Normalized after depth scaling: every direction moves at full speed.
                _direction = input.normalized;
                if (Mathf.Abs(input.x) > FacingThreshold)
                {
                    context.FacingSign = input.x > 0f ? 1 : -1;
                }
            }
            else
            {
                _direction = new Vector3(context.FacingSign, 0f, 0f);
            }

            IsRoll = context.IsGrounded && !fromWall;
            Progress = 0f;
            _clock.Reset();
            ProcessFrame(context, 0);
            context.Velocity = _direction * CurrentSpeed(context);

            // Air dashes hold altitude for their whole length.
            if (!IsRoll)
            {
                context.VerticalVelocity = 0f;
            }
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            // A roll's recovery (from RollCancelFrame) is cancellable by a buffered jump or attack; dashes are committed.
            if (IsRoll && _clock.Frame >= context.Movement.RollCancelFrame && TryCancelRoll(context))
            {
                return;
            }

            context.Velocity = _direction * CurrentSpeed(context);
            context.Move(context.Velocity, deltaTime, IsRoll ? -1f : 0f);
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            int steps = _clock.Advance(fixedDeltaTime);
            for (int frame = _clock.Frame - steps + 1; frame <= _clock.Frame; frame++)
            {
                ProcessFrame(context, frame);
            }

            int length = Length(context);
            Progress = Mathf.Clamp01(_clock.Frame / (float)length);
            if (_clock.Frame < length)
            {
                return;
            }

            // Leave at run speed so the next state doesn't start at dash speed.
            context.Velocity = Vector3.ClampMagnitude(context.Velocity, context.Movement.MoveSpeed);

            if (context.ComboTracker.ComboLength > 0 && context.Buffer.TryConsume(BufferedCommand.Attack, context.Time))
            {
                context.ComboTracker.Begin();
                context.Machine.ChangeState(context.States.Attack);
                return;
            }

            context.ChangeToLocomotion();
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // An interrupted dash must never leave the player invulnerable.
            context.IsInvulnerable = false;
        }

        // ============ Helpers ============

        /// <summary>Jump or attack out of the roll recovery. Dash isn't offered so rolls can't chain their i-frames.</summary>
        private static bool TryCancelRoll(PlayerContext context)
        {
            if (context.CanGroundJump && context.Buffer.TryConsume(BufferedCommand.Jump, context.Time))
            {
                context.Velocity = Vector3.ClampMagnitude(context.Velocity, context.Movement.MoveSpeed);
                context.StartJump();
                return true;
            }

            return context.TryStartAttack();
        }

        private int Length(PlayerContext context)
        {
            return Mathf.Max(1, IsRoll ? context.Movement.RollFrames : context.Movement.DashFrames);
        }

        /// <summary>Dash: constant. Roll: full speed, then eases down over the last part of the roll.</summary>
        private float CurrentSpeed(PlayerContext context)
        {
            PlayerMovementSettings movement = context.Movement;
            if (!IsRoll)
            {
                return movement.DashSpeed;
            }

            float easeOut = Mathf.InverseLerp(movement.RollEaseStart, 1f, Progress);
            return movement.RollSpeed * Mathf.Lerp(1f, movement.RollEndSpeedFraction, easeOut);
        }

        private void ProcessFrame(PlayerContext context, int frame)
        {
            PlayerMovementSettings movement = context.Movement;
            context.IsInvulnerable = IsRoll
                ? frame >= movement.RollIFrameStart && frame < movement.RollIFrameEnd
                : frame >= movement.DashIFrameStart && frame < movement.DashIFrameEnd;
        }
    }
}
