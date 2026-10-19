using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Beat 'em up approach with turn-taking. Off cooldown, the enemy queues with the coordinator's
    /// <see cref="AttackDirector"/>. Enemies at the front of the queue close in on their side of the target, line up in
    /// its depth lane and attack when the director gives them the turn; everyone else waits at <c>WaitDistance</c> on
    /// their own lane so the group fans out around the target. Jumps up to a target standing higher, and over
    /// obstacles that block the path.
    /// </summary>
    public sealed class EnemyChaseState : IState<EnemyContext>
    {
        // ============ Constants ============

        /// <summary>Stand-off is this fraction of AttackRange, so the enemy stops a little inside its reach.</summary>
        private const float ApproachFraction = 0.8f;

        /// <summary>Distance (units) inside which the enemy eases off instead of overshooting its spot.</summary>
        private const float ArriveRadius = 0.3f;

        /// <summary>Below this desired speed the enemy isn't really trying to move, so contact doesn't count as blocked.</summary>
        private const float BlockedMinSpeed = 0.5f;

        /// <summary>Vertical speed (units/s) below which an ungrounded enemy counts as falling rather than stepping down.</summary>
        private const float FallingSpeed = -3f;

        // ============ State ============

        private float _blockedTime;

        // ============ IState ============

        /// <inheritdoc />
        public void Enter(EnemyContext context)
        {
            _blockedTime = 0f;
        }

        /// <inheritdoc />
        public void Tick(EnemyContext context, float deltaTime)
        {
            EnemySettings settings = context.Settings;
            Vector3 toTarget = context.ToTarget();
            if (context.Target == null || toTarget.magnitude > settings.LoseRange)
            {
                context.WithdrawFromQueue();
                context.Machine.ChangeState(context.States.Idle);
                return;
            }

            if (context.IsFlying)
            {
                TickFlyer(context, toTarget, deltaTime);
                return;
            }

            // Walked off a ledge.
            if (!context.IsGrounded && context.VerticalVelocity < FallingSpeed)
            {
                context.Machine.ChangeState(context.States.Air);
                return;
            }

            context.FaceTarget();

            bool offCooldown = context.Time >= context.NextAttackTime;
            if (offCooldown)
            {
                context.EnlistForAttack();
            }

            bool ranged = settings.Behaviour == EnemyBehaviour.Ranged;
            bool front = offCooldown && context.IsFrontOfQueue;
            bool lined = Mathf.Abs(toTarget.z) <= settings.LaneTolerance;
            bool inReach = Mathf.Abs(toTarget.x) <= settings.AttackRange;
            float rise = Mathf.Abs(context.Target.position.y - context.Position.y);
            bool level = rise < (ranged ? settings.MaxShotHeightDifference : settings.JumpUpThreshold);
            if (front && lined && inReach && level && context.TryBeginAttackTurn())
            {
                context.Machine.ChangeState(context.States.Attack);
                return;
            }

            // Casters don't chase up ledges: they hold their ground and wait for a shot.
            if (!ranged && TryJump(context, toTarget, deltaTime))
            {
                return;
            }

            // Front of the queue closes in on its lane-0 spot; the rest wait further out on their own lanes.
            // Casters hold at their preferred range instead of walking into melee.
            float side = toTarget.x > 0f ? -1f : 1f; // enemy's side relative to the target
            float approach = ranged ? settings.PreferredRange : settings.AttackRange * ApproachFraction;
            Vector3 offset = front
                ? new Vector3(side * approach, 0f, 0f)
                : new Vector3(side * Mathf.Max(settings.WaitDistance, approach), 0f, context.WaitLaneOffset);
            Vector3 toGoal = context.Target.position + offset - context.Position;
            toGoal.y = 0f;

            // Arrive: full speed far away, easing to a stop on the spot.
            float distance = toGoal.magnitude;
            Vector3 desired = distance > 0.001f
                ? toGoal / distance * settings.MoveSpeed * Mathf.Clamp01(distance / ArriveRadius)
                : Vector3.zero;
            desired.z *= settings.DepthSpeedMultiplier;

            // Never walk off into a pit: hold at the edge (the target has to come to us).
            if (!context.IsSafeStep(desired))
            {
                desired = Vector3.zero;
                context.Velocity = Vector3.zero;
            }

            context.Velocity = Vector3.MoveTowards(context.Velocity, desired, settings.Acceleration * deltaTime);
            context.Move(context.Velocity, deltaTime);

            bool pushing = desired.magnitude > BlockedMinSpeed && (context.LastCollision & CollisionFlags.Sides) != 0;
            _blockedTime = pushing ? _blockedTime + deltaTime : 0f;
        }

        /// <inheritdoc />
        public void FixedTick(EnemyContext context, float fixedDeltaTime)
        {
        }

        /// <inheritdoc />
        public void Exit(EnemyContext context)
        {
        }

        // ============ Helpers ============

        /// <summary>
        /// Flyer: hovers beside and above the target (the front of the queue closer in), then dives when lined up.
        /// Steers in 3D: planar velocity toward the hover spot, vertical velocity toward its height.
        /// </summary>
        private static void TickFlyer(EnemyContext context, Vector3 toTarget, float deltaTime)
        {
            EnemySettings settings = context.Settings;
            context.FaceTarget();

            bool offCooldown = context.Time >= context.NextAttackTime;
            if (offCooldown)
            {
                context.EnlistForAttack();
            }

            bool front = offCooldown && context.IsFrontOfQueue;
            bool lined = Mathf.Abs(toTarget.z) <= settings.LaneTolerance;
            bool inReach = Mathf.Abs(toTarget.x) <= settings.AttackRange;
            if (front && lined && inReach && context.TryBeginAttackTurn())
            {
                context.Machine.ChangeState(context.States.Attack);
                return;
            }

            float side = toTarget.x > 0f ? -1f : 1f;
            Vector3 offset = front
                ? new Vector3(side * settings.AttackRange * ApproachFraction, settings.HoverHeight, 0f)
                : new Vector3(side * settings.WaitDistance, settings.HoverHeight + 0.6f, context.WaitLaneOffset);
            Vector3 toGoal = context.Target.position + offset - context.Position;

            Vector3 planar = new Vector3(toGoal.x, 0f, toGoal.z);
            float distance = planar.magnitude;
            Vector3 desired = distance > 0.001f
                ? planar / distance * settings.MoveSpeed * Mathf.Clamp01(distance / ArriveRadius)
                : Vector3.zero;
            desired.z *= settings.DepthSpeedMultiplier;
            context.Velocity = Vector3.MoveTowards(context.Velocity, desired, settings.Acceleration * deltaTime);

            float climb = Mathf.Clamp(toGoal.y * 3f, -settings.MoveSpeed, settings.MoveSpeed);
            context.VerticalVelocity = Mathf.MoveTowards(context.VerticalVelocity, climb, settings.HoverAcceleration * deltaTime);
            context.Move(context.Velocity, deltaTime);
        }

        /// <summary>Jumps up to a higher target nearby, or over whatever has been blocking the path.</summary>
        private bool TryJump(EnemyContext context, Vector3 toTarget, float deltaTime)
        {
            EnemySettings settings = context.Settings;
            if (!context.IsGrounded || context.Time < context.NextJumpTime)
            {
                return false;
            }

            float rise = context.Target.position.y - context.Position.y;
            bool targetAbove = rise >= settings.JumpUpThreshold && toTarget.magnitude <= settings.JumpUpRange;
            bool blocked = _blockedTime >= settings.BlockedJumpDelay;
            if (!targetAbove && !blocked)
            {
                return false;
            }

            _blockedTime = 0f;
            context.StartJump(toTarget);
            return true;
        }
    }
}
