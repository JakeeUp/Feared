using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Plays the combo link at <c>ComboTracker.CurrentIndex</c>. Re-entered (same-state ChangeState) for each next link.
    /// Frame timeline runs on a private <see cref="FrameClock"/> in FixedTick; hitbox sweeps on Active frames.
    /// </summary>
    /// <remarks>
    /// Transitions (§4.1): Tick — 1. cancel window &amp;&amp; consume Dash (air: an air dash left) -&gt; Dash (ComboTracker.Reset);
    /// then Parry, then Jump (ground jump, or double jump in the air);
    /// 2. cancel window &amp;&amp; HasNext &amp;&amp; consume Attack -&gt; TryAdvance, re-enter Attack.
    /// FixedTick — 3. IsFinished -&gt; ComboTracker.Reset; Move if input else Idle. Hitstun external (priority 0).
    /// Facing is LOCKED for the whole attack.
    /// </remarks>
    public sealed class AttackState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private AttackData _attack;
        private bool _directional;
        private bool _pogoed;

        // ============ Properties (read by presentation) ============

        /// <summary>The attack playing, or null outside the state.</summary>
        public AttackData CurrentAttack => _attack;

        /// <summary>Simulation frame of the current attack (0 at Enter).</summary>
        public int CurrentFrame => _clock.Frame;

        /// <summary>Increments on every Enter, so presentation can tell a re-entered (chained) attack from the same one.</summary>
        public int ActivationCount { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(PlayerContext context)
        {
            _directional = context.PendingDirectionalAttack != null;
            _attack = _directional
                ? context.PendingDirectionalAttack
                : context.Combo != null ? context.Combo.GetAttack(context.ComboTracker.CurrentIndex) : null;
            context.PendingDirectionalAttack = null;
            _pogoed = false;
            if (_attack == null)
            {
                context.ComboTracker.Reset();
                context.Machine.ChangeState(context.States.Idle);
                return;
            }

            _clock.Reset();
            ActivationCount++;

            // Facing is locked for the whole swing.
            if (context.Hitbox != null)
            {
                context.Hitbox.BeginAttack(_attack, context.FacingSign);
            }

            // Frame 0 right away so 0-startup attacks hit on frame 0.
            ProcessFrame(context, 0);
        }

        /// <inheritdoc />
        public void Tick(PlayerContext context, float deltaTime)
        {
            if (_attack == null)
            {
                return;
            }

            AttackFrames frames = _attack.Frames;
            int frame = _clock.Frame;

            if (frames.IsInCancelWindow(frame))
            {
                // Dash cancel. In the air it spends an air dash (no endless dash -> attack -> dash flight).
                if (CanDash(context) && context.Buffer.Peek(BufferedCommand.Dash, context.Time))
                {
                    context.ComboTracker.Reset();
                    context.TryStartDash();
                    return;
                }

                // Parry cancel: answer an enemy's swing from inside your own recovery.
                if (context.Buffer.TryConsume(BufferedCommand.Parry, context.Time))
                {
                    context.ComboTracker.Reset();
                    context.Machine.ChangeState(context.States.Parry);
                    return;
                }

                // Jump cancel launches straight out of the swing (coyote counts); in the air it spends a double jump.
                if (context.CanGroundJump && context.Buffer.TryConsume(BufferedCommand.Jump, context.Time))
                {
                    context.ComboTracker.Reset();
                    context.StartJump();
                    return;
                }

                if (!context.CanGroundJump && context.AirJumpsLeft > 0 && context.Buffer.Peek(BufferedCommand.Jump, context.Time))
                {
                    context.ComboTracker.Reset();
                    context.TryStartAirJump();
                    return;
                }

                // A directional slash (or a held direction) breaks out into a fresh attack of its own.
                if (context.PickDirectionalAttack() != null && context.TryStartAttack())
                {
                    return;
                }

                // HasNext before consuming, so a press during the finisher stays buffered instead of being eaten.
                if (!_directional && context.ComboTracker.HasNext && context.Buffer.TryConsume(BufferedCommand.Attack, context.Time))
                {
                    context.ComboTracker.TryAdvance(out _);
                    context.Machine.ChangeState(context.States.Attack);
                    return;
                }
            }

            if (TryMoveCancel(context, frames, frame))
            {
                return;
            }

            Steer(context, frames.GetPhase(frame), deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(PlayerContext context, float fixedDeltaTime)
        {
            if (_attack == null)
            {
                return;
            }

            // Process every stepped-over frame so a 1-frame active window is never skipped.
            int steps = _clock.Advance(fixedDeltaTime);
            for (int frame = _clock.Frame - steps + 1; frame <= _clock.Frame; frame++)
            {
                ProcessFrame(context, frame);
            }

            if (_attack.Frames.IsFinished(_clock.Frame))
            {
                context.ComboTracker.Reset();
                context.ChangeToLocomotion();
            }
        }

        /// <inheritdoc />
        public void Exit(PlayerContext context)
        {
            // Combo reset is left to whichever transition ends the combo; chaining re-enters this state.
            if (context.Hitbox != null)
            {
                context.Hitbox.EndAttack();
            }

            _attack = null;
        }

        // ============ Helpers ============

        /// <summary>Grounded, or airborne with an air dash left (matches <see cref="PlayerContext.TryStartDash"/>).</summary>
        private static bool CanDash(PlayerContext context)
        {
            return context.IsGrounded || context.AirDashesLeft > 0;
        }

        /// <summary>
        /// Attacks never root the player. On the ground the stick steers at a fraction of run speed (plus the swing's
        /// lunge while it's out); in the air the swing keeps full air control and gravity, so an air attack never stalls
        /// a jump.
        /// </summary>
        private void Steer(PlayerContext context, AttackPhase phase, float deltaTime)
        {
            PlayerMovementSettings movement = context.Movement;
            Vector3 input = context.GetMoveInputWorld();

            if (!context.IsGrounded)
            {
                Vector3 airTarget = input * movement.MoveSpeed;
                if (Mathf.Abs(context.Velocity.x) > movement.MoveSpeed && input.x * context.Velocity.x > 0f)
                {
                    airTarget.x = context.Velocity.x; // keep over-speed momentum (swing releases, dashes)
                }

                context.Velocity = Vector3.MoveTowards(context.Velocity, airTarget, movement.AirAcceleration * deltaTime);
                context.Move(context.Velocity, deltaTime);
                return;
            }

            Vector3 target = input * (movement.MoveSpeed * movement.AttackMoveFraction);
            if (!_directional && (phase == AttackPhase.Startup || phase == AttackPhase.Active))
            {
                target.x += context.FacingSign * _attack.LungeSpeed;
            }

            context.Velocity = Vector3.MoveTowards(context.Velocity, target, movement.Deceleration * deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <summary>
        /// Recovery is not a prison: shortly after the active frames, moving (on the ground) or anything at all (in the
        /// air) hands control straight back, unless another attack is already buffered (the combo keeps priority).
        /// </summary>
        private bool TryMoveCancel(PlayerContext context, AttackFrames frames, int frame)
        {
            int activeEnd = frames.Startup + frames.Active;
            bool grounded = context.IsGrounded;
            int earliest = activeEnd + (grounded ? context.Movement.AttackMoveCancelFrames : 0);
            if (frame < earliest || context.Buffer.Peek(BufferedCommand.Attack, context.Time))
            {
                return false;
            }

            if (grounded && !context.HasMoveInput())
            {
                return false;
            }

            context.ComboTracker.Reset();
            context.ChangeToLocomotion();
            return true;
        }

        private void ProcessFrame(PlayerContext context, int frame)
        {
            // Sweep dedupes per activation, so a target walking in late is still hit exactly once.
            if (_attack == null || !_attack.Frames.IsActive(frame) || context.Hitbox == null)
            {
                return;
            }

            int hits = context.Hitbox.Sweep();

            // Pogo: a down slash that connects bounces the player up and refreshes the air moves (Hollow Knight).
            if (hits > 0 && !_pogoed && ReferenceEquals(_attack, context.DownAttack))
            {
                _pogoed = true;
                context.VerticalVelocity = context.Movement.PogoVelocity;
                context.RefillAirMoves();
                context.IsDoubleJumping = false;
            }
        }
    }
}
