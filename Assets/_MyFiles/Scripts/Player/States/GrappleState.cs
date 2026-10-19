using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Swinging from a ring the chain has hooked. The throw itself happens outside any state (see <see cref="Chain"/>),
    /// so this starts the moment the hook lands, with whatever momentum the player had: no reel-in, no forced facing.
    /// A pendulum in the X/Y plane with hand-written position-based maths, not a physics joint: integrate gravity, stick
    /// pump and drag, step, then project back onto the rope circle (slack inside it). Speed is capped so the swing never
    /// gains runaway energy, and the controller's collisions still apply.
    /// </summary>
    /// <remarks>
    /// Transitions (Tick, priority order): 1. Jump -&gt; release with an upward boost (Air); 2. Chain again -&gt; let go
    /// (Air); 3. Dash -&gt; air dash out; 4. Attack -&gt; air attack out; 5. landed -&gt; locomotion. Exit always hands the swing
    /// velocity to the context, so every way out (including Hitstun) carries the momentum.
    /// Latching refills air jumps/dashes and lifts the wall-jump block, so swing -&gt; double jump -&gt; swing chains work.
    /// </remarks>
    public sealed class GrappleState : IState<PlayerContext>
    {
        // ============ Constants ============

        /// <summary>Minimum |swing X speed| that turns the player to face the swing direction.</summary>
        private const float FacingSpeed = 0.5f;

        /// <summary>How fast the player is drawn onto the ring's depth lane (units/s).</summary>
        private const float DepthAlignSpeed = 8f;

        /// <summary>Seconds after latching before touching the ground lets go (so latching from a standstill can lift off).</summary>
        private const float GroundReleaseGrace = 0.3f;

        // ============ State ============

        private GrappleAnchor _anchor;
        private float _ropeLength;
        private float _attachedTime;
        private Vector3 _swingVelocity;

        // ============ Properties ============

        /// <summary>Ring being swung from (null outside this state).</summary>
        public GrappleAnchor Anchor => _anchor;

        /// <summary>Current swing velocity (units/s). Drives the swing pose.</summary>
        public Vector3 SwingVelocity => _swingVelocity;

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            _anchor = context.PendingAnchor;
            context.PendingAnchor = null;
            if (_anchor == null)
            {
                context.ChangeToLocomotion();
                return;
            }

            PlayerMovementSettings movement = context.Movement;
            _anchor.IsTargeted = true;
            _attachedTime = 0f;
            _swingVelocity = new Vector3(context.Velocity.x, context.VerticalVelocity, 0f);

            Vector3 offset = context.RopeOrigin - _anchor.Position;
            offset.z = 0f;
            // The rope starts exactly as long as the throw. Snapping it shorter would drag the player across in one frame
            // (a huge fake velocity: the fling); a long latch reels in smoothly instead (see Swing).
            _ropeLength = Mathf.Max(offset.magnitude, movement.GrappleMinLength);

            // The rope catches: drop only the part of the velocity pulling away from the ring, keep the rest (momentum in).
            Vector3 radial = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.down;
            float outward = Vector3.Dot(_swingVelocity, radial);
            if (outward > 0f)
            {
                _swingVelocity -= radial * outward;
            }

            context.RefillAirMoves();
            context.ResetWallUse();
            context.IsDoubleJumping = false;
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            if (_anchor == null || deltaTime <= 0f)
            {
                return;
            }

            if (TryExit(context))
            {
                return;
            }

            _attachedTime += deltaTime;
            Swing(context, deltaTime);

            if (_attachedTime > GroundReleaseGrace && context.IsGrounded && _swingVelocity.y <= 0f)
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
            context.Velocity = new Vector3(_swingVelocity.x, 0f, 0f);
            context.VerticalVelocity = _swingVelocity.y;

            if (_anchor != null)
            {
                _anchor.IsTargeted = false;
                _anchor.BlockFor(context.Movement.GrappleRegrabDelay);
            }

            _anchor = null;
        }

        // ============ Swing ============

        /// <summary>One pendulum step: forces, cap, step, project onto the rope, collide.</summary>
        private void Swing(PlayerContext context, float deltaTime)
        {
            PlayerMovementSettings movement = context.Movement;
            Vector3 anchor = _anchor.Position;
            Vector3 origin = context.RopeOrigin;

            Vector3 radial = origin - anchor;
            radial.z = 0f;
            Vector3 radialDir = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.down;
            Vector3 tangent = new Vector3(-radialDir.y, radialDir.x, 0f);

            // Gravity, then pump: push along the tangent in the direction the stick points.
            _swingVelocity.y += movement.Gravity * deltaTime;
            float stick = context.GetMoveInputWorld().x;
            _swingVelocity += tangent * (Vector3.Dot(tangent, Vector3.right) * stick * movement.SwingPumpAcceleration * deltaTime);
            _swingVelocity *= Mathf.Max(0f, 1f - movement.SwingDamping * deltaTime);
            _swingVelocity = Vector3.ClampMagnitude(_swingVelocity, movement.SwingMaxSpeed);

            // Reel a long latch in toward the max length at a steady speed.
            if (_ropeLength > movement.GrappleMaxLength)
            {
                _ropeLength = Mathf.Max(movement.GrappleMaxLength, _ropeLength - movement.GrappleReelSpeed * deltaTime);
            }

            // Step, then project back onto the circle: the rope is rigid while taut, slack inside it.
            Vector3 next = origin + _swingVelocity * deltaTime;
            Vector3 fromAnchor = next - anchor;
            fromAnchor.z = 0f;
            if (fromAnchor.magnitude > _ropeLength)
            {
                fromAnchor = fromAnchor.normalized * _ropeLength;
            }

            Vector3 desired = new Vector3(anchor.x + fromAnchor.x, anchor.y + fromAnchor.y, origin.z);
            desired.z = Mathf.MoveTowards(origin.z, anchor.z, DepthAlignSpeed * deltaTime);

            Vector3 displacement = desired - origin;
            Vector3 before = context.Controller.transform.position;
            CollisionFlags flags = context.MoveDirect(displacement, deltaTime);
            Vector3 moved = context.Controller.transform.position - before;

            // The velocity is whatever actually happened: the projection removes the radial part, walls kill the rest.
            // Capped again: whatever the projection did, it never becomes a fling.
            _swingVelocity = flags == CollisionFlags.None
                ? new Vector3(displacement.x, displacement.y, 0f) / deltaTime
                : new Vector3(moved.x, moved.y, 0f) / deltaTime;
            _swingVelocity = Vector3.ClampMagnitude(_swingVelocity, movement.SwingMaxSpeed);

            if (Mathf.Abs(_swingVelocity.x) > FacingSpeed)
            {
                context.FacingSign = _swingVelocity.x > 0f ? 1 : -1;
            }
        }

        // ============ Transitions ============

        private bool TryExit(PlayerContext context)
        {
            InputBuffer buffer = context.Buffer;
            float now = context.Time;

            if (buffer.TryConsume(BufferedCommand.Jump, now))
            {
                PlayerMovementSettings movement = context.Movement;
                _swingVelocity.x *= movement.SwingReleaseCarry;
                _swingVelocity.y = Mathf.Max(_swingVelocity.y, 0f) + movement.SwingReleaseJumpBoost;
                context.Machine.ChangeState(context.States.Air);
                return true;
            }

            // Chain again: let go and leave the press buffered, so the chain flies at the next ring (this one is blocked
            // for a moment) on the same press. Ring to ring is one fluid input; with nothing in reach it just lets go.
            if (buffer.Peek(BufferedCommand.Grapple, now))
            {
                context.Machine.ChangeState(context.States.Air);
                return true;
            }

            return context.TryStartDash() || context.TryStartAttack();
        }
    }
}
