using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Defeated: invulnerable, pops up from the killing blow, lands, lingers for <c>DeathLingerSeconds</c>
    /// (presentation blinks it), then despawns so the spawner can recycle it.
    /// </summary>
    public sealed class EnemyDeadState : IState<EnemyContext>
    {
        // ============ Constants ============

        /// <summary>Frames before landing can count (the death pop needs to lift off first).</summary>
        private const int MinAirFrames = 3;

        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private float _landedTime = -1f;

        // ============ Properties (read by presentation) ============

        /// <summary>True once the body has landed.</summary>
        public bool HasLanded => _landedTime >= 0f;

        /// <summary>Linger progress after landing, 0..1.</summary>
        public float LingerProgress { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
            context.IsInvulnerable = true;
            context.Velocity = Vector3.zero; // the killing blow's knockback is applied right after this
            _clock.Reset();
            _landedTime = -1f;
            LingerProgress = 0f;
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            context.Decelerate(context.Settings.KnockbackDeceleration * (HasLanded ? 3f : 0.3f), deltaTime);
            context.Move(context.Velocity, deltaTime);

            if (!HasLanded)
            {
                return;
            }

            float linger = Mathf.Max(0.01f, context.Settings.DeathLingerSeconds);
            LingerProgress = Mathf.Clamp01((context.Time - _landedTime) / linger);
            if (LingerProgress >= 1f)
            {
                context.Owner.Despawn();
            }
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
            _clock.Advance(fixedDeltaTime);
            if (!HasLanded && _clock.Frame >= MinAirFrames && context.IsGrounded && context.VerticalVelocity <= 0f)
            {
                _landedTime = context.Time;
            }
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
            context.IsInvulnerable = false;
        }
    }
}
