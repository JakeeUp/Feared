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
        // ============ Constants ============

        /// <summary>Downward velocity held while grounded so CharacterController.isGrounded stays stable.</summary>
        private const float GroundStickVelocity = -2f;

        /// <summary>Deadzone used when no movement settings are assigned.</summary>
        private const float DefaultDeadzone = 0.15f;

        /// <summary>A surface counts as a wall when |normal.x| is at least this (rejects slopes and crate tops).</summary>
        private const float MinWallNormalX = 0.7f;

        // ============ Private Motor State ============

        private float _verticalVelocity; // Y velocity owned by Move()'s gravity step (units/s)
        private float _timeSinceGrounded; // scaled seconds since the controller last reported grounded (coyote time)
        private bool _jumpCutPending;     // true from a jump until Jump is released (variable jump height)

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

        /// <summary>Y velocity in units/s (positive = up). Integrated with gravity inside <see cref="Move"/>.</summary>
        public float VerticalVelocity
        {
            get => _verticalVelocity;
            set => _verticalVelocity = value;
        }

        /// <summary>Air dashes left before landing (or touching a wall) refills them.</summary>
        public int AirDashesLeft { get; set; }

        /// <summary>Air (double) jumps left before landing (or touching a wall) refills them.</summary>
        public int AirJumpsLeft { get; set; }

        /// <summary>True from a double jump until its rise ends (drives the flip animation).</summary>
        public bool IsDoubleJumping { get; set; }

        /// <summary>Side of the wall the player is sliding on: +1 right, -1 left. Written by AirState before WallSlide.</summary>
        public int WallSide { get; set; }

        /// <summary>Scaled seconds of reduced air control left after a wall jump (keeps the kick-off from being undone).</summary>
        public float WallJumpLockTimer { get; set; }

        /// <summary>True while the CharacterController reports ground contact (updated by Move).</summary>
        public bool IsGrounded => Controller != null && Controller.isGrounded;

        /// <summary>Grounded, or left the ground less than CoyoteTime ago (jumping is still allowed).</summary>
        public bool CanGroundJump => IsGrounded || (Movement != null && _timeSinceGrounded <= Movement.CoyoteTime);

        // ============ Helpers ============

        /// <summary>Move input converted to a world-space planar direction.</summary>
        /// <returns>(x, 0, y * DepthSpeedMultiplier) with deadzone applied; <see cref="Vector3.zero"/> when neutral.</returns>
        public Vector3 GetMoveInputWorld()
        {
            if (!HasMoveInput())
            {
                return Vector3.zero;
            }

            Vector2 raw = Vector2.ClampMagnitude(Input.Move, 1f);
            float deadzone = Deadzone;
            float magnitude = raw.magnitude;

            // Rescale from the deadzone edge so speed ramps from 0 instead of jumping.
            Vector2 stick = raw * (Mathf.InverseLerp(deadzone, 1f, magnitude) / magnitude);
            float depth = Movement != null ? Movement.DepthSpeedMultiplier : 1f;
            return new Vector3(stick.x, 0f, stick.y * depth);
        }

        /// <summary>True when move input is outside the deadzone.</summary>
        /// <returns>True if the player is pushing a direction.</returns>
        public bool HasMoveInput()
        {
            if (Input == null)
            {
                return false;
            }

            float deadzone = Deadzone;
            return Input.Move.sqrMagnitude > deadzone * deadzone;
        }

        /// <summary>Moves the CharacterController by a planar velocity plus gravity.</summary>
        /// <param name="planarVelocity">X/Z velocity in units/s (Y component is ignored).</param>
        /// <param name="deltaTime">Scaled delta time (Update).</param>
        /// <param name="maxFallSpeed">Fall speed cap in units/s (positive). Negative = use MaxFallSpeed. 0 = hang in place.</param>
        public void Move(Vector3 planarVelocity, float deltaTime, float maxFallSpeed = -1f)
        {
            if (deltaTime <= 0f || Controller == null || !Controller.enabled)
            {
                return;
            }

            if (Controller.isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = GroundStickVelocity;
            }
            else
            {
                // Heavier on the way down: snappier arcs than a symmetric parabola.
                float gravity = Movement != null ? Movement.Gravity : -30f;
                if (_verticalVelocity < 0f && Movement != null)
                {
                    gravity *= Movement.FallGravityMultiplier;
                }

                _verticalVelocity += gravity * deltaTime;

                float cap = maxFallSpeed >= 0f ? maxFallSpeed : (Movement != null ? Movement.MaxFallSpeed : 25f);
                _verticalVelocity = Mathf.Max(_verticalVelocity, -cap);
            }

            Controller.Move(new Vector3(planarVelocity.x, _verticalVelocity, planarVelocity.z) * deltaTime);

            if (Controller.isGrounded)
            {
                _timeSinceGrounded = 0f;
                RefillAirMoves();
                IsDoubleJumping = false;
            }
            else
            {
                _timeSinceGrounded += deltaTime;
            }
        }

        /// <summary>Launches a jump and enters the air state. Consumes coyote time.</summary>
        public void StartJump()
        {
            _verticalVelocity = Movement.JumpVelocity;
            _timeSinceGrounded = float.MaxValue;
            _jumpCutPending = true;
            IsDoubleJumping = false;
            Machine.ChangeState(States.Air);
        }

        /// <summary>
        /// Air jump: resets the fall into a fresh (slightly shorter) rise and lets the stick redirect the drift.
        /// Stays in the air state. Caller checks <see cref="AirJumpsLeft"/>.
        /// </summary>
        public void StartDoubleJump()
        {
            AirJumpsLeft--;
            _verticalVelocity = Movement.DoubleJumpVelocity;
            _jumpCutPending = true;
            IsDoubleJumping = true;
            WallJumpLockTimer = 0f;

            Vector3 input = GetMoveInputWorld();
            if (input.sqrMagnitude > 0f)
            {
                Velocity = input * Movement.MoveSpeed;
            }
        }

        /// <summary>Refills air dashes and air jumps (on landing or touching a wall).</summary>
        public void RefillAirMoves()
        {
            AirDashesLeft = Movement != null ? Movement.AirDashes : 0;
            AirJumpsLeft = Movement != null ? Movement.AirJumps : 0;
        }

        /// <summary>Kicks off the wall on <see cref="WallSide"/>: up and away, facing away, brief air-control lock.</summary>
        public void StartWallJump()
        {
            int away = -WallSide;
            _verticalVelocity = Movement.WallJumpVelocity;
            Velocity = new Vector3(away * Movement.WallJumpPush, 0f, 0f);
            FacingSign = away;
            WallJumpLockTimer = FrameTime.ToSeconds(Movement.WallJumpLockFrames);
            _jumpCutPending = true;
            IsDoubleJumping = false;
            Machine.ChangeState(States.Air);
        }

        /// <summary>Variable jump height: releasing Jump while rising cuts the upward velocity once.</summary>
        public void ApplyJumpCut()
        {
            if (!_jumpCutPending)
            {
                return;
            }

            if (_verticalVelocity <= 0f)
            {
                _jumpCutPending = false;
                return;
            }

            if (Input != null && !Input.JumpHeld)
            {
                _verticalVelocity *= Movement.JumpCutMultiplier;
                _jumpCutPending = false;
            }
        }

        /// <summary>True when a solid, mostly vertical wall is right beside the player on <paramref name="side"/>.</summary>
        /// <param name="side">+1 to check right (+X), -1 to check left.</param>
        /// <remarks>
        /// One ray from the capsule's waist along X, at the player's own depth, so a crate a lane behind never counts.
        /// Other characters' CharacterControllers are ignored, as are triggers (hurtboxes).
        /// </remarks>
        public bool IsTouchingWall(int side)
        {
            if (side == 0 || Controller == null || Movement == null)
            {
                return false;
            }

            Transform t = Controller.transform;
            Vector3 origin = t.position + Vector3.up * (Controller.center.y);
            float distance = Controller.radius + Controller.skinWidth + Movement.WallCheckDistance;
            if (!Physics.Raycast(origin, new Vector3(side, 0f, 0f), out RaycastHit hit, distance, Movement.WallLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return !(hit.collider is CharacterController) && Mathf.Abs(hit.normal.x) >= MinWallNormalX;
        }

        /// <summary>Consumes a buffered Dash, Jump or Attack (in that priority) and switches state. Shared by Idle and Move.</summary>
        /// <returns>True if a transition happened (the caller should return).</returns>
        public bool TryStartAction()
        {
            if (Buffer.TryConsume(BufferedCommand.Dash, Time))
            {
                Machine.ChangeState(States.Dash);
                return true;
            }

            if (CanGroundJump && Buffer.TryConsume(BufferedCommand.Jump, Time))
            {
                StartJump();
                return true;
            }

            return TryStartAttack();
        }

        /// <summary>Starts a fresh combo if Attack is buffered. Works on the ground and in the air.</summary>
        /// <returns>True if the attack started.</returns>
        public bool TryStartAttack()
        {
            // Only consume Attack when there is a combo to start, so the press isn't silently eaten.
            if (ComboTracker.ComboLength > 0 && Buffer.TryConsume(BufferedCommand.Attack, Time))
            {
                ComboTracker.Begin();
                Machine.ChangeState(States.Attack);
                return true;
            }

            return false;
        }

        /// <summary>Exits a timeline state to Air when airborne, otherwise Move or Idle depending on input.</summary>
        public void ChangeToLocomotion()
        {
            if (!IsGrounded)
            {
                Machine.ChangeState(States.Air);
                return;
            }

            Machine.ChangeState(HasMoveInput() ? (IState<PlayerContext>)States.Move : States.Idle);
        }

        /// <summary>True when the player walked off a ledge (no ground and coyote time is over).</summary>
        public bool HasLeftGround()
        {
            return !CanGroundJump;
        }

        private float Deadzone => Movement != null ? Movement.InputDeadzone : DefaultDeadzone;
    }
}
