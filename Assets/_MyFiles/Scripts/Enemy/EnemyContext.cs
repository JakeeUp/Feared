using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Shared blackboard + services for enemy states (the enemy-side twin of PlayerContext). Built once by
    /// <see cref="EnemyController"/>; states are plain objects that read and write here.
    /// </summary>
    public sealed class EnemyContext
    {
        // ============ Constants ============

        /// <summary>Downward velocity held while grounded so CharacterController.isGrounded stays stable.</summary>
        private const float GroundStickVelocity = -2f;

        /// <summary>Below this |dx| the enemy doesn't turn (prevents flip-flopping when standing on the target's X).</summary>
        private const float TurnThreshold = 0.05f;

        /// <summary>Height of the target's chest above its feet (aim point).</summary>
        private const float TargetChestHeight = 0.9f;

        /// <summary>Deepest drop an enemy will walk off on its own (pits are deeper). Knockback can still throw it in.</summary>
        private const float MaxSafeDrop = 3f;

        /// <summary>How far past the capsule edge the ledge probe looks.</summary>
        private const float LedgeLookAhead = 0.25f;

        // ============ Construction ============

        /// <summary>Creates the context. References are stored as-is; the controller validates them.</summary>
        public EnemyContext(EnemyController owner, CharacterController controller, EnemySettings settings, Hitbox hitbox, Health health,
                            ProjectileLauncher launcher = null)
        {
            Owner = owner;
            Controller = controller;
            Settings = settings;
            Hitbox = hitbox;
            Health = health;
            Launcher = launcher;
            FacingSign = -1; // spawned enemies usually face back toward the player's side
        }

        // ============ Services ============

        /// <summary>Owning controller.</summary>
        public EnemyController Owner { get; }

        /// <summary>Motor.</summary>
        public CharacterController Controller { get; }

        /// <summary>Tuning.</summary>
        public EnemySettings Settings { get; }

        /// <summary>Attack hitbox.</summary>
        public Hitbox Hitbox { get; }

        /// <summary>Hit points.</summary>
        public Health Health { get; }

        /// <summary>Projectile launcher for Ranged enemies (null for the rest).</summary>
        public ProjectileLauncher Launcher { get; }

        /// <summary>True for a living Flyer: no gravity, hovers. A dead flyer falls like everyone else.</summary>
        public bool IsFlying => Settings != null && Settings.Behaviour == EnemyBehaviour.Flyer && !FlightSuppressed && (Health == null || !Health.IsDead);

        /// <summary>True while a chain yank has pulled a flyer out of the sky: it falls until the stun ends.</summary>
        public bool FlightSuppressed { get; set; }

        /// <summary>The target's chest (aim point for shots and dives); the enemy's own position without a target.</summary>
        public Vector3 TargetChest => Target != null ? Target.position + Vector3.up * TargetChestHeight : Position;

        /// <summary>Scene brain (target, camera, attack tokens). Injected by the spawner; may be null in a bare scene.</summary>
        public EnemyCoordinator Coordinator { get; set; }

        /// <summary>All state instances.</summary>
        public EnemyStates States { get; set; }

        /// <summary>The enemy's state machine.</summary>
        public StateMachine<EnemyContext> Machine { get; set; }

        // ============ Mutable Runtime Data ============

        /// <summary>Scaled game time, written by the controller before each tick.</summary>
        public float Time { get; set; }

        /// <summary>Planar (X/Z) velocity in units/s.</summary>
        public Vector3 Velocity { get; set; }

        /// <summary>Y velocity in units/s (positive = up).</summary>
        public float VerticalVelocity { get; set; }

        /// <summary>+1 right, -1 left. Never 0.</summary>
        public int FacingSign { get; set; }

        /// <summary>True while hits must be ignored (on the ground after a knockdown, getting up, dead).</summary>
        public bool IsInvulnerable { get; set; }

        /// <summary>The most recent hit taken.</summary>
        public DamageInfo LastHit { get; set; }

        /// <summary>Earliest time the next attack may start (cooldown).</summary>
        public float NextAttackTime { get; set; }

        /// <summary>Earliest time the next jump may start.</summary>
        public float NextJumpTime { get; set; }

        /// <summary>Collision flags from the last <see cref="Move"/> (sides = blocked by a wall or crate).</summary>
        public CollisionFlags LastCollision { get; private set; }

        // ============ Queries ============

        /// <summary>The target, or null.</summary>
        public Transform Target => Coordinator != null ? Coordinator.Target : null;

        /// <summary>Enemy root position.</summary>
        public Vector3 Position => Owner.transform.position;

        /// <summary>True while the motor reports ground contact.</summary>
        public bool IsGrounded => Controller != null && Controller.isGrounded;

        /// <summary>Planar offset from this enemy to the target (Y zeroed); zero when there is no target.</summary>
        public Vector3 ToTarget()
        {
            if (Target == null)
            {
                return Vector3.zero;
            }

            Vector3 delta = Target.position - Position;
            delta.y = 0f;
            return delta;
        }

        // ============ Motor ============

        /// <summary>Moves by a planar velocity plus gravity (one CharacterController.Move per frame).</summary>
        public void Move(Vector3 planarVelocity, float deltaTime)
        {
            if (deltaTime <= 0f || Controller == null || !Controller.enabled)
            {
                LastCollision = CollisionFlags.None;
                return;
            }

            if (IsFlying)
            {
                // No gravity: states steer VerticalVelocity; anything else (knockback lift) drifts back to a hover.
                VerticalVelocity = Mathf.MoveTowards(VerticalVelocity, 0f, Settings.HoverAcceleration * 0.5f * deltaTime);
            }
            else if (Controller.isGrounded && VerticalVelocity <= 0f)
            {
                VerticalVelocity = GroundStickVelocity;
            }
            else
            {
                VerticalVelocity += Settings.Gravity * deltaTime;
            }

            LastCollision = Controller.Move(new Vector3(planarVelocity.x, VerticalVelocity, planarVelocity.z) * deltaTime);
        }

        /// <summary>
        /// True when moving along <paramref name="planarVelocity"/> keeps solid ground within <see cref="MaxSafeDrop"/>
        /// just ahead (always true while airborne, flying or standing still). Stops enemies walking into pits.
        /// </summary>
        public bool IsSafeStep(Vector3 planarVelocity)
        {
            planarVelocity.y = 0f;
            if (IsFlying || !IsGrounded || planarVelocity.sqrMagnitude < 0.01f || Controller == null)
            {
                return true;
            }

            Vector3 ahead = Position + planarVelocity.normalized * (Controller.radius + LedgeLookAhead) + Vector3.up * 0.5f;
            return Physics.Raycast(ahead, Vector3.down, MaxSafeDrop + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Jumps toward <paramref name="direction"/> (planar) and enters the air state.</summary>
        public void StartJump(Vector3 direction)
        {
            direction.y = 0f;
            Velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * Settings.AirSpeed : Vector3.zero;
            VerticalVelocity = Settings.JumpVelocity;
            NextJumpTime = Time + Settings.JumpCooldown;
            Machine.ChangeState(States.Air);
        }

        /// <summary>Bleeds planar velocity toward zero (knockback slides, stopping).</summary>
        public void Decelerate(float rate, float deltaTime)
        {
            Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, rate * deltaTime);
        }

        /// <summary>Turns toward the target (unless standing almost on its X).</summary>
        public void FaceTarget()
        {
            float dx = ToTarget().x;
            if (Mathf.Abs(dx) > TurnThreshold)
            {
                FacingSign = dx > 0f ? 1 : -1;
            }
        }

        // ============ Turn-taking ============

        /// <summary>Queues for an attack turn (no-op without a coordinator, or if already queued).</summary>
        public void EnlistForAttack()
        {
            if (Coordinator != null)
            {
                Coordinator.Director.Enlist(Owner);
            }
        }

        /// <summary>True if this enemy may close in (front of the queue). Always true without a coordinator.</summary>
        public bool IsFrontOfQueue => Coordinator == null || Coordinator.Director.IsFront(Owner);

        /// <summary>Starts an attack turn if it's ours (always succeeds without a coordinator).</summary>
        public bool TryBeginAttackTurn()
        {
            return Coordinator == null || Coordinator.Director.TryBeginAttack(Owner, Time);
        }

        /// <summary>Ends the attack turn (returns the token). Safe to call any time.</summary>
        public void EndAttackTurn()
        {
            if (Coordinator != null)
            {
                Coordinator.Director.EndAttack(Owner);
            }
        }

        /// <summary>Leaves the queue and returns any token (hit, killed, despawned).</summary>
        public void WithdrawFromQueue()
        {
            if (Coordinator != null)
            {
                Coordinator.Director.Withdraw(Owner);
            }
        }

        /// <summary>Depth offset of this enemy's waiting spot.</summary>
        public float WaitLaneOffset => Coordinator != null ? Coordinator.WaitLaneOffset(Owner) : 0f;

        /// <summary>Starts the post-attack cooldown, jittered so groups desync.</summary>
        public void StartCooldown()
        {
            NextAttackTime = Time + Settings.AttackCooldown + Random.Range(0f, Settings.CooldownJitter);
        }
    }
}
