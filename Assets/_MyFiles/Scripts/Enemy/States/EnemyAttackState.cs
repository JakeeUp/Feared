using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// One telegraphed swing from <c>EnemySettings.Attack</c>: facing locked at Enter, hitbox swept on every stepped
    /// active frame, small lunge during the active frames. Ends its attack turn and starts the cooldown when done or
    /// interrupted.
    /// </summary>
    public sealed class EnemyAttackState : IState<EnemyContext>
    {
        // ============ State ============

        private readonly FrameClock _clock = new FrameClock();
        private AttackData _attack;
        private Vector3 _diveTarget;
        private bool _fired;

        // ============ Properties (read by presentation) ============

        /// <summary>The attack playing, or null outside the state.</summary>
        public AttackData CurrentAttack => _attack;

        /// <summary>Simulation frame of the current attack.</summary>
        public int CurrentFrame => _clock.Frame;

        /// <summary>Increments on every Enter so presentation can react once per swing (telegraph).</summary>
        public int ActivationCount { get; private set; }

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
            _attack = context.Settings.Attack;
            if (_attack == null)
            {
                context.EndAttackTurn();
                context.Machine.ChangeState(context.States.Chase);
                return;
            }

            context.FaceTarget();
            context.Velocity = Vector3.zero;
            _diveTarget = context.TargetChest; // flyers dive at where the target was when the telegraph started
            _fired = false;
            _clock.Reset();
            ActivationCount++;
            if (context.Hitbox != null && context.Launcher == null)
            {
                context.Hitbox.BeginAttack(_attack, context.FacingSign);
            }

            ProcessFrame(context, 0);
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            if (_attack == null)
            {
                return;
            }

            AttackPhase phase = _attack.Frames.GetPhase(_clock.Frame);
            if (phase == AttackPhase.Active && context.IsFlying)
            {
                // Dive: straight at the telegraphed spot, in 3D.
                Vector3 dive = (_diveTarget - context.Position).normalized * _attack.LungeSpeed;
                context.Velocity = new Vector3(dive.x, 0f, dive.z);
                context.VerticalVelocity = dive.y;
            }
            else if (phase == AttackPhase.Active && context.Launcher == null)
            {
                // Lunge, but stop dead at a ledge rather than charging into a pit.
                var lunge = new Vector3(context.FacingSign * _attack.LungeSpeed, 0f, 0f);
                context.Velocity = context.IsSafeStep(lunge) ? lunge : Vector3.zero;
            }
            else
            {
                context.Decelerate(context.Settings.Acceleration, deltaTime);
            }

            context.Move(context.Velocity, deltaTime);
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
            if (_attack == null)
            {
                return;
            }

            int steps = _clock.Advance(fixedDeltaTime);
            for (int frame = _clock.Frame - steps + 1; frame <= _clock.Frame; frame++)
            {
                ProcessFrame(context, frame);
            }

            if (_attack.Frames.IsFinished(_clock.Frame))
            {
                context.Machine.ChangeState(context.States.Chase);
            }
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
            // Every exit (finished, interrupted by a hit, killed) ends the turn and starts the cooldown; the enemy
            // re-enlists at the back of the queue once the cooldown is over.
            if (context.Hitbox != null)
            {
                context.Hitbox.EndAttack();
            }

            context.EndAttackTurn();
            context.StartCooldown();
            _attack = null;
        }

        // ============ Helpers ============

        private void ProcessFrame(EnemyContext context, int frame)
        {
            if (_attack == null || !_attack.Frames.IsActive(frame))
            {
                return;
            }

            // Casters release one shot on the first active frame instead of swinging.
            if (context.Launcher != null)
            {
                if (!_fired)
                {
                    _fired = true;
                    context.Launcher.Fire(context.FacingSign, context.TargetChest, aimVertical: true); // across ledges too
                }

                return;
            }

            if (context.Hitbox != null)
            {
                context.Hitbox.Sweep();
            }
        }
    }
}
