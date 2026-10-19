using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Launched by a heavy hit: tumbles through the air (still hittable, so juggles work), lands and lies down for
    /// <c>DownFrames</c>, then gets up over <c>GetUpFrames</c>. Invulnerable from landing until standing.
    /// </summary>
    public sealed class EnemyKnockdownState : IState<EnemyContext>
    {
        // ============ Types ============

        /// <summary>Where the knockdown is.</summary>
        public enum KnockdownPhase
        {
            /// <summary>In the air after the launch (hittable).</summary>
            Airborne,

            /// <summary>Lying on the ground (invulnerable).</summary>
            Down,

            /// <summary>Standing back up (invulnerable).</summary>
            GettingUp,
        }

        // ============ Constants ============

        /// <summary>Frames after Enter before landing can end the airborne phase (the launch needs a moment to lift off).</summary>
        private const int MinAirFrames = 3;

        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();

        // ============ Properties (read by presentation) ============

        /// <summary>Current phase.</summary>
        public KnockdownPhase Phase { get; private set; }

        /// <summary>Progress through the current phase, 0..1 (Airborne reports 0).</summary>
        public float PhaseProgress { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
            Phase = KnockdownPhase.Airborne;
            PhaseProgress = 0f;
            context.IsInvulnerable = false;
            context.Velocity = Vector3.zero; // the launch knockback is applied right after this
            _clock.Reset();
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            context.Decelerate(context.Settings.KnockbackDeceleration * (Phase == KnockdownPhase.Airborne ? 0.3f : 2f), deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            EnemySettings settings = context.Settings;

            switch (Phase)
            {
                case KnockdownPhase.Airborne:
                    if (_clock.Frame >= MinAirFrames && context.IsGrounded && context.VerticalVelocity <= 0f)
                    {
                        SetPhase(context, KnockdownPhase.Down);
                    }

                    break;

                case KnockdownPhase.Down:
                    PhaseProgress = Mathf.Clamp01(_clock.Frame / (float)Mathf.Max(1, settings.DownFrames));
                    if (_clock.Frame >= settings.DownFrames)
                    {
                        SetPhase(context, KnockdownPhase.GettingUp);
                    }

                    break;

                case KnockdownPhase.GettingUp:
                    PhaseProgress = Mathf.Clamp01(_clock.Frame / (float)Mathf.Max(1, settings.GetUpFrames));
                    if (_clock.Frame >= settings.GetUpFrames)
                    {
                        context.Machine.ChangeState(context.States.Chase);
                    }

                    break;
            }
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
            context.IsInvulnerable = false;
        }

        // ============ Helpers ============

        private void SetPhase(EnemyContext context, KnockdownPhase phase)
        {
            Phase = phase;
            PhaseProgress = 0f;
            _clock.Reset();
            context.IsInvulnerable = phase != KnockdownPhase.Airborne;
        }
    }
}
