using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// The chain hooked something too heavy to move (an elite, the boss): the player is pulled to it. Fast, straight,
    /// invulnerable, no gravity. Arrives just short of the target with a bit of momentum, straight into a buffered attack.
    /// Jump mid-zip launches out of it with the zip's speed (zip-jump), Dash breaks off into an air dash.
    /// </summary>
    public sealed class ZipState : IState<PlayerContext>
    {
        // ============ Constants ============

        /// <summary>Fraction of zip speed kept on a zip-jump.</summary>
        private const float JumpCarry = 0.6f;

        /// <summary>Small upward pop on arrival so the player doesn't stop dead on the ground.</summary>
        private const float ArrivePop = 3f;

        // ============ State ============

        private IChainTarget _target;
        private Vector3 _direction;
        private float _time;

        // ============ Properties ============

        /// <summary>What the player is zipping to (null outside this state).</summary>
        public IChainTarget Target => _target;

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            _target = context.PendingZipTarget;
            context.PendingZipTarget = null;
            _time = 0f;
            if (_target == null)
            {
                context.ChangeToLocomotion();
                return;
            }

            context.IsInvulnerable = true;
            context.VerticalVelocity = 0f;
            _direction = (_target.ChainPoint - context.RopeOrigin).normalized;
            if (Mathf.Abs(_direction.x) > 0.05f)
            {
                context.FacingSign = _direction.x > 0f ? 1 : -1;
            }
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            if (_target == null || deltaTime <= 0f)
            {
                return;
            }

            PlayerMovementSettings movement = context.Movement;
            if (context.Buffer.TryConsume(BufferedCommand.Jump, context.Time))
            {
                context.Velocity = new Vector3(_direction.x, 0f, _direction.z) * (movement.ZipSpeed * JumpCarry);
                context.StartJump();
                return;
            }

            if (context.TryStartDash())
            {
                return;
            }

            _time += deltaTime;
            Vector3 toTarget = _target.ChainPoint - context.RopeOrigin;
            float remaining = toTarget.magnitude - movement.ZipArriveDistance;
            if (!_target.CanBeChained || remaining <= 0f || _time >= movement.ZipMaxSeconds)
            {
                Arrive(context);
                return;
            }

            _direction = toTarget / toTarget.magnitude;
            float step = Mathf.Min(remaining, movement.ZipSpeed * deltaTime);
            CollisionFlags flags = context.MoveDirect(_direction * step, deltaTime);
            if ((flags & CollisionFlags.Sides) != 0 && step < movement.ZipSpeed * deltaTime * 0.5f)
            {
                Arrive(context);
            }
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            context.IsInvulnerable = false;
            context.RefillAirMoves();
            _target = null;
        }

        // ============ Helpers ============

        private void Arrive(PlayerContext context)
        {
            PlayerMovementSettings movement = context.Movement;
            context.Velocity = new Vector3(_direction.x, 0f, _direction.z) * movement.MoveSpeed;
            context.VerticalVelocity = ArrivePop;
            if (!context.TryStartAttack())
            {
                context.ChangeToLocomotion();
            }
        }
    }
}
