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
    /// Transitions (§4.1): Tick — 1. cancel window &amp;&amp; consume Dash -&gt; Dash (ComboTracker.Reset);
    /// 2. cancel window &amp;&amp; HasNext &amp;&amp; consume Attack -&gt; TryAdvance, re-enter Attack.
    /// FixedTick — 3. IsFinished -&gt; ComboTracker.Reset; Move if input else Idle. Hitstun external (priority 0).
    /// Facing is LOCKED for the whole attack.
    /// </remarks>
    public sealed class AttackState : IState<PlayerContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private AttackData _attack;

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
            _attack = context.Combo != null ? context.Combo.GetAttack(context.ComboTracker.CurrentIndex) : null;
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
                if (context.Buffer.TryConsume(BufferedCommand.Dash, context.Time))
                {
                    context.ComboTracker.Reset();
                    context.Machine.ChangeState(context.States.Dash);
                    return;
                }

                // Jump cancel (grounded only) launches straight out of the swing.
                if (context.IsGrounded && context.Buffer.TryConsume(BufferedCommand.Jump, context.Time))
                {
                    context.ComboTracker.Reset();
                    context.StartJump();
                    return;
                }

                // HasNext before consuming, so a press during the finisher stays buffered instead of being eaten.
                if (context.ComboTracker.HasNext && context.Buffer.TryConsume(BufferedCommand.Attack, context.Time))
                {
                    context.ComboTracker.TryAdvance(out _);
                    context.Machine.ChangeState(context.States.Attack);
                    return;
                }
            }

            AttackPhase phase = frames.GetPhase(frame);
            if (phase == AttackPhase.Startup || phase == AttackPhase.Active)
            {
                context.Velocity = new Vector3(context.FacingSign * _attack.LungeSpeed, 0f, 0f);
            }
            else
            {
                context.Velocity = Vector3.MoveTowards(context.Velocity, Vector3.zero, context.Movement.Deceleration * deltaTime);
            }

            context.Move(context.Velocity, deltaTime);
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

        private void ProcessFrame(PlayerContext context, int frame)
        {
            // Sweep dedupes per activation, so a target walking in late is still hit exactly once.
            if (_attack != null && _attack.Frames.IsActive(frame) && context.Hitbox != null)
            {
                context.Hitbox.Sweep();
            }
        }
    }
}
