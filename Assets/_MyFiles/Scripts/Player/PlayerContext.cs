// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    /// <summary>
    /// Shared blackboard + services for player states. Constructed once by <see cref="PlayerController"/> in Awake
    /// (constructor injection, no MonoBehaviour). States are stateless with respect to the player and read/write here.
    /// </summary>
    public sealed class PlayerContext
    {
        // ============ Private Motor State ============

        private float _verticalVelocity; // Y velocity owned by Move()'s gravity step (units/s)

        // ============ Construction ============

        /// <summary>Creates the context. All references are stored as-is; null checks belong to the caller (PlayerController.OnValidate/Awake).</summary>
        /// <param name="owner">Owning controller (MonoBehaviour root).</param>
        /// <param name="controller">CharacterController motor.</param>
        /// <param name="input">Input reader (raw move + command events).</param>
        /// <param name="buffer">Command buffer (Attack/Dash).</param>
        /// <param name="movement">Locomotion/dash/hit-reaction tuning.</param>
        /// <param name="combo">Combo chain; its Count sizes <see cref="ComboTracker"/>.</param>
        /// <param name="hitbox">Player hitbox driven by AttackState.</param>
        public PlayerContext(PlayerController owner, CharacterController controller, PlayerInputReader input,
                             InputBuffer buffer, PlayerMovementSettings movement, ComboData combo, Hitbox hitbox)
        {
            Owner = owner;
            Controller = controller;
            Input = input;
            Buffer = buffer;
            Movement = movement;
            Combo = combo;
            Hitbox = hitbox;
            ComboTracker = new ComboTracker(combo != null ? combo.Count : 0);
            FacingSign = 1; // IFacingSource contract: never 0. Start facing right (+X).
        }

        // ============ Services ============

        /// <summary>Owning controller.</summary>
        public PlayerController Owner { get; }

        /// <summary>CharacterController motor.</summary>
        public CharacterController Controller { get; }

        /// <summary>Input reader.</summary>
        public PlayerInputReader Input { get; }

        /// <summary>Command buffer. Consume with <c>Buffer.TryConsume(cmd, Time)</c>.</summary>
        public InputBuffer Buffer { get; }

        /// <summary>Movement tuning.</summary>
        public PlayerMovementSettings Movement { get; }

        /// <summary>Combo chain data.</summary>
        public ComboData Combo { get; }

        /// <summary>Combo index tracker (length = <c>Combo.Count</c>).</summary>
        public ComboTracker ComboTracker { get; }

        /// <summary>Player hitbox.</summary>
        public Hitbox Hitbox { get; }

        /// <summary>All state instances. Set once by PlayerController right after construction.</summary>
        public PlayerStates States { get; set; }

        /// <summary>The player's state machine. Set once by PlayerController right after construction.</summary>
        public StateMachine<PlayerContext> Machine { get; set; }

        // ============ Mutable Runtime Data ============

        /// <summary>Scaled game time (<c>Time.time</c>), written by PlayerController before each Tick/FixedTick. Use for buffer consumption.</summary>
        public float Time { get; set; }

        /// <summary>Planar (X/Z) velocity in units/s. Y is ignored here; gravity is handled inside <see cref="Move"/>.</summary>
        public Vector3 Velocity { get; set; }

        /// <summary>Facing sign: +1 right (+X), -1 left. Never 0.</summary>
        public int FacingSign { get; set; }

        /// <summary>True during dash i-frames. Written by DashState each processed frame; cleared on Dash exit.</summary>
        public bool IsInvulnerable { get; set; }

        /// <summary>The most recent hit taken. Written by PlayerController.TryApplyDamage; read by HitstunState.Enter.</summary>
        public DamageInfo LastHit { get; set; }

        // ============ Helpers ============

        /// <summary>Move input converted to a world-space planar direction.</summary>
        /// <returns>(x, 0, y * DepthSpeedMultiplier) with deadzone applied; <see cref="Vector3.zero"/> when neutral.</returns>
        public Vector3 GetMoveInputWorld()
        {
            // TODO: raw stick -> world direction on X/Z.
            // - Radial deadzone (compare squared magnitude, no sqrt), clamp to length 1.
            // - Optional: rescale from the deadzone edge so speed ramps smoothly instead of jumping.
            // - Stick Y maps to world Z, scaled by DepthSpeedMultiplier - walking into/out of the screen slower than sideways
            //   is the standard beat 'em up feel (Streets of Rage, River City Girls etc.).
            // - No camera-relative remap needed while the camera yaw is fixed at 0.
            return default;
        }

        /// <summary>True when move input is outside the deadzone.</summary>
        /// <returns>True if the player is pushing a direction.</returns>
        public bool HasMoveInput()
        {
            // TODO: same deadzone test as GetMoveInputWorld. If the thresholds differ, Idle <-> Move flickers at the edge.
            return false;
        }

        /// <summary>Moves the CharacterController by a planar velocity plus gravity.</summary>
        /// <param name="planarVelocity">X/Z velocity in units/s (Y component is ignored).</param>
        /// <param name="deltaTime">Scaled delta time (Update).</param>
        public void Move(Vector3 planarVelocity, float deltaTime)
        {
            // TODO: the motor step - gravity + CharacterController.Move.
            // - Skip when dt <= 0 (hitstop) or the controller is disabled.
            // - Small constant down force while grounded, otherwise accumulate gravity.
            // - This runs from Update on purpose: CharacterController doesn't interpolate, so moving it in FixedUpdate stutters.
            // - Don't write Velocity here. States own planar velocity, this just applies it.
        }
    }
}
