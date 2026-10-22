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

        /// <summary>How far below the feet ground can be and still be snapped to when walking down stairs.</summary>
        private const float GroundSnapDistance = 0.45f;

        /// <summary>Deadzone used when no movement settings are assigned.</summary>
        private const float DefaultDeadzone = 0.15f;

        /// <summary>A surface counts as a wall when |normal.x| is at least this (rejects slopes and crate tops).</summary>
        private const float MinWallNormalX = 0.7f;

        /// <summary>Frames a jump always rises at full speed before a released button can cut it (minimum hop).</summary>
        /// <remarks>Without it a buffered press released before landing is cut on its very first frame into a tiny hop.</remarks>
        private const int MinJumpCutFrames = 4;

        /// <summary>Stick Y (up or down) that turns Attack into a directional slash.</summary>
        private const float DirectionalAttackThreshold = 0.7f;

        /// <summary>Rope origin height above the capsule centre (roughly the hand).</summary>
        private const float RopeOriginOffset = 0.3f;

        // ============ Private Motor State ============

        private float _verticalVelocity; // Y velocity owned by Move()'s gravity step (units/s)
        private float _timeSinceGrounded; // scaled seconds since the controller last reported grounded (coyote time)
        private bool _jumpCutPending;     // true from a jump until Jump is released (variable jump height)
        private bool _jumpReleased;       // Jump let go while rising: heavier gravity until the apex
        private float _jumpStartTime;     // context Time of the last jump launch (minimum hop before a cut)
        private int _wallJumpBlockedSide; // wall side last kicked off: unusable until landing or grabbing the other wall
        private int _lastGrabbedWallSide; // wall side last grabbed this airtime: re-grabbing it does not refill air moves

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

        /// <summary>True while a parry would deflect an incoming hit. Written by ParryState.</summary>
        public bool IsParryActive { get; set; }

        /// <summary>The most recent hit taken. Written by PlayerController.TryApplyDamage; read by HitstunState.Enter.</summary>
        public DamageInfo LastHit { get; set; }

        /// <summary>Y velocity in units/s (positive = up). Integrated with gravity inside <see cref="Move"/>.</summary>
        public float VerticalVelocity
        {
            get => _verticalVelocity;
            set => _verticalVelocity = value;
        }

        /// <summary>Air dashes left before landing (or grabbing a new wall) refills them.</summary>
        public int AirDashesLeft { get; set; }

        /// <summary>Air (double) jumps left before landing (or grabbing a new wall) refills them.</summary>
        public int AirJumpsLeft { get; set; }

        /// <summary>True from a double jump until its rise ends (drives the flip animation).</summary>
        public bool IsDoubleJumping { get; set; }

        /// <summary>Side of the wall the player is sliding on: +1 right, -1 left. Written by AirState before WallSlide.</summary>
        public int WallSide { get; set; }

        /// <summary>Scaled seconds of reduced air control left after a wall jump (keeps the kick-off from being undone).</summary>
        public float WallJumpLockTimer { get; set; }

        /// <summary>
        /// Wall side (+1 / -1) the player last wall-jumped off, or 0. That side can't be grabbed or kicked off again
        /// until the player lands or grabs the opposite wall, so one wall can't be climbed forever (chimneys still work).
        /// </summary>
        public int WallJumpBlockedSide => _wallJumpBlockedSide;

        /// <summary>Overhead slash (Attack + up). Null = none.</summary>
        public AttackData UpAttack { get; set; }

        /// <summary>Downward air slash that pogos (Attack + down in the air). Null = none.</summary>
        public AttackData DownAttack { get; set; }

        /// <summary>Directional attack picked by <see cref="TryStartAttack"/> (null = the combo), read by AttackState.Enter.</summary>
        public AttackData PendingDirectionalAttack { get; set; }

        /// <summary>Time (same clock as <see cref="Time"/>) before which a fresh attack can't start. Set by AttackState.Exit.</summary>
        public float NextAttackTime { get; set; }

        /// <summary>Time before which the chain can't be thrown again.</summary>
        public float NextChainTime { get; set; }

        /// <summary>The chain while it's flying (throwing it is not a state).</summary>
        public Chain Chain { get; } = new Chain();

        /// <summary>Ring the chain just hooked, read by GrappleState.Enter.</summary>
        public GrappleAnchor PendingAnchor { get; set; }

        /// <summary>Heavy target the chain just hooked, read by ZipState.Enter.</summary>
        public IChainTarget PendingZipTarget { get; set; }

        /// <summary>World point the grapple rope starts from (about hand height).</summary>
        public Vector3 RopeOrigin => Controller.transform.position + Vector3.up * (Controller.center.y + RopeOriginOffset);

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

            bool wasGrounded = Controller.isGrounded;
            if (wasGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = GroundStickVelocity;
            }
            else
            {
                // Heavier on the way down (snappier than a symmetric parabola), heavier while rising after an early
                // release (short hops without a sudden stop), and lighter at the apex while Jump is held (hang time).
                float gravity = Movement != null ? Movement.Gravity : -30f;
                if (Movement != null)
                {
                    if (_verticalVelocity < 0f)
                    {
                        gravity *= Movement.FallGravityMultiplier;
                    }
                    else if (_jumpReleased)
                    {
                        gravity *= Movement.ReleaseGravityMultiplier;
                    }

                    if (Mathf.Abs(_verticalVelocity) < Movement.ApexHangThreshold && Input != null && Input.JumpHeld && !_jumpReleased)
                    {
                        gravity *= Movement.ApexGravityMultiplier;
                    }
                }

                _verticalVelocity += gravity * deltaTime;

                float cap = maxFallSpeed >= 0f ? maxFallSpeed : (Movement != null ? Movement.MaxFallSpeed : 25f);
                _verticalVelocity = Mathf.Max(_verticalVelocity, -cap);
            }

            Controller.Move(new Vector3(planarVelocity.x, _verticalVelocity, planarVelocity.z) * deltaTime);
            if (wasGrounded && !Controller.isGrounded && _verticalVelocity <= 0f)
            {
                SnapToGround();
            }

            if (Controller.isGrounded)
            {
                _timeSinceGrounded = 0f;
                _jumpReleased = false;
                RefillAirMoves();
                ResetWallUse();
                IsDoubleJumping = false;
            }
            else
            {
                _timeSinceGrounded += deltaTime;
            }
        }

        /// <summary>
        /// Walking down stairs or a slope outruns the ground-stick velocity, so the controller would leave the ground
        /// on every step. If there is floor just under the feet, move down onto it; past a real ledge, do nothing.
        /// </summary>
        private void SnapToGround()
        {
            Transform t = Controller.transform;
            float radius = Controller.radius * 0.9f;
            Vector3 center = t.TransformPoint(Controller.center);
            float toFeet = Controller.height * 0.5f - radius;
            if (Physics.SphereCast(center, radius, Vector3.down, out RaycastHit hit, toFeet + GroundSnapDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.transform != t && Vector3.Angle(hit.normal, Vector3.up) <= Controller.slopeLimit)
            {
                Controller.Move(Vector3.down * (hit.distance - toFeet + Controller.skinWidth));
            }
        }

        /// <summary>
        /// Moves the CharacterController by an exact displacement with no gravity step (the grapple swing integrates
        /// its own). Keeps the grounded bookkeeping (coyote time, air-move refills) the same as <see cref="Move"/>.
        /// </summary>
        /// <param name="displacement">World-space displacement for this tick.</param>
        /// <param name="deltaTime">Scaled delta time (Update).</param>
        /// <returns>The controller's collision flags, or None when it didn't move.</returns>
        public CollisionFlags MoveDirect(Vector3 displacement, float deltaTime)
        {
            if (deltaTime <= 0f || Controller == null || !Controller.enabled)
            {
                return CollisionFlags.None;
            }

            CollisionFlags flags = Controller.Move(displacement);
            if (Controller.isGrounded)
            {
                _timeSinceGrounded = 0f;
                RefillAirMoves();
                ResetWallUse();
                IsDoubleJumping = false;
            }
            else
            {
                _timeSinceGrounded += deltaTime;
            }

            return flags;
        }

        /// <summary>
        /// Throws the chain if Chain is buffered and something is in reach. This never changes state: the hook flies
        /// while the player keeps doing whatever they were doing (PlayerController resolves it when it lands). With
        /// nothing in reach the press stays buffered, so pressing a moment early still throws.
        /// </summary>
        /// <returns>True if the chain was thrown.</returns>
        public bool TryFireChain()
        {
            if (Chain.IsFlying || Time < NextChainTime || !Buffer.Peek(BufferedCommand.Grapple, Time))
            {
                return false;
            }

            if (!ChainTargeting.FindBest(this, out GrappleAnchor anchor, out IChainTarget enemy))
            {
                return false;
            }

            Buffer.TryConsume(BufferedCommand.Grapple, Time);
            NextChainTime = Time + Movement.ChainCooldown;
            Chain.Fire(RopeOrigin, anchor, enemy);
            return true;
        }

        /// <summary>Launches a jump and enters the air state. Consumes coyote time.</summary>
        public void StartJump()
        {
            _verticalVelocity = Movement.JumpVelocity;
            _timeSinceGrounded = float.MaxValue;
            ArmJumpCut();
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
            ArmJumpCut();
            IsDoubleJumping = true;
            WallJumpLockTimer = 0f;

            Vector3 input = GetMoveInputWorld();
            if (input.sqrMagnitude > 0f)
            {
                Velocity = input * Movement.MoveSpeed;
            }
        }

        /// <summary>Refills air dashes and air jumps (on landing or grabbing a new wall).</summary>
        public void RefillAirMoves()
        {
            AirDashesLeft = Movement != null ? Movement.AirDashes : 0;
            AirJumpsLeft = Movement != null ? Movement.AirJumps : 0;
        }

        /// <summary>
        /// Called when the player grabs the wall on <paramref name="side"/>. Refills air moves only for a different wall
        /// than the last one grabbed this airtime (no drop-off, double jump, re-grab climbing), and grabbing the
        /// opposite wall lifts the wall-jump block (chimney climbing).
        /// </summary>
        /// <param name="side">+1 right, -1 left.</param>
        public void OnWallGrabbed(int side)
        {
            if (side != _lastGrabbedWallSide)
            {
                RefillAirMoves();
            }

            _lastGrabbedWallSide = side;
            if (side != _wallJumpBlockedSide)
            {
                _wallJumpBlockedSide = 0;
            }
        }

        /// <summary>True when the wall on <paramref name="side"/> may be grabbed or kicked off (see <see cref="WallJumpBlockedSide"/>).</summary>
        /// <param name="side">+1 right, -1 left. 0 is never usable.</param>
        /// <returns>False for 0 and for the blocked side.</returns>
        public bool CanUseWall(int side)
        {
            return side != 0 && side != _wallJumpBlockedSide;
        }

        /// <summary>Clears the wall-jump block and the last grabbed wall. Called on landing.</summary>
        public void ResetWallUse()
        {
            _wallJumpBlockedSide = 0;
            _lastGrabbedWallSide = 0;
        }

        /// <summary>Kicks off the wall on <see cref="WallSide"/>: up and away, facing away, brief air-control lock.</summary>
        /// <remarks>Blocks <see cref="WallSide"/> until landing or grabbing the opposite wall (no single-wall climbing).</remarks>
        public void StartWallJump()
        {
            int away = -WallSide;
            _wallJumpBlockedSide = WallSide;
            _verticalVelocity = Movement.WallJumpVelocity;
            Velocity = new Vector3(away * Movement.WallJumpPush, 0f, 0f);
            FacingSign = away;
            WallJumpLockTimer = FrameTime.ToSeconds(Movement.WallJumpLockFrames);
            ArmJumpCut();
            IsDoubleJumping = false;
            Machine.ChangeState(States.Air);
        }

        /// <summary>Variable jump height: releasing Jump while rising switches to heavier gravity until the apex.</summary>
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

            // A minimum hop first, so a buffered press released before the jump started still clears low platforms.
            if (Input != null && !Input.JumpHeld && Time - _jumpStartTime >= FrameTime.ToSeconds(MinJumpCutFrames))
            {
                _jumpReleased = true;
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

        /// <summary>Consumes a buffered Parry, Dash, Jump or Attack (in that priority) and switches state. Shared by Idle and Move.</summary>
        /// <returns>True if a transition happened (the caller should return).</returns>
        public bool TryStartAction()
        {
            if (Buffer.TryConsume(BufferedCommand.Parry, Time))
            {
                Machine.ChangeState(States.Parry);
                return true;
            }

            if (TryStartDash())
            {
                return true;
            }

            if (CanGroundJump && Buffer.TryConsume(BufferedCommand.Jump, Time))
            {
                StartJump();
                return true;
            }

            return TryStartAttack();
        }

        /// <summary>
        /// Starts a dash if Dash is buffered: a roll on the ground, an air dash (spends <see cref="AirDashesLeft"/>) in the
        /// air. With no air dashes left the press stays buffered so it rolls on landing.
        /// </summary>
        /// <returns>True if the dash started.</returns>
        public bool TryStartDash()
        {
            bool grounded = IsGrounded;
            if (!grounded && AirDashesLeft <= 0)
            {
                return false;
            }

            if (!Buffer.TryConsume(BufferedCommand.Dash, Time))
            {
                return false;
            }

            if (!grounded)
            {
                AirDashesLeft--;
            }

            Machine.ChangeState(States.Dash);
            return true;
        }

        /// <summary>
        /// Air jump if Jump is buffered and <see cref="AirJumpsLeft"/> allows, switching to the air state (cancels out of
        /// an air attack). Stays buffered otherwise.
        /// </summary>
        /// <returns>True if the double jump started.</returns>
        public bool TryStartAirJump()
        {
            if (AirJumpsLeft <= 0 || !Buffer.TryConsume(BufferedCommand.Jump, Time))
            {
                return false;
            }

            StartDoubleJump();
            Machine.ChangeState(States.Air);
            return true;
        }

        /// <summary>Starts a fresh combo if Attack is buffered. Works on the ground and in the air.</summary>
        /// <returns>True if the attack started.</returns>
        public bool TryStartAttack()
        {
            // Too soon after the last swing: the press stays buffered and fires when the recovery ends.
            if (Time < NextAttackTime)
            {
                return false;
            }

            // Directional slashes first: up = overhead (anti-air), down in the air = pogo slash.
            AttackData directional = PickDirectionalAttack();
            if (directional != null && Buffer.TryConsume(BufferedCommand.Attack, Time))
            {
                ComboTracker.Reset();
                PendingDirectionalAttack = directional;
                Machine.ChangeState(States.Attack);
                return true;
            }

            // Only consume Attack when there is a combo to start, so the press isn't silently eaten.
            if (ComboTracker.ComboLength > 0 && Buffer.TryConsume(BufferedCommand.Attack, Time))
            {
                ComboTracker.Begin();
                Machine.ChangeState(States.Attack);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The directional attack the stick asks for, or null. The stick's Y is also depth movement, so only a clear
        /// push (mostly vertical) counts: diagonals keep walking in depth and swing the normal combo.
        /// </summary>
        public AttackData PickDirectionalAttack()
        {
            if (Input == null)
            {
                return null;
            }

            Vector2 stick = Input.Move;
            bool vertical = Mathf.Abs(stick.y) >= DirectionalAttackThreshold && Mathf.Abs(stick.x) < Mathf.Abs(stick.y) * 0.75f;
            if (!vertical)
            {
                return null;
            }

            if (stick.y > 0f)
            {
                return UpAttack;
            }

            return IsGrounded ? null : DownAttack;
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

        /// <summary>Arms the one-shot jump cut for a jump launched now (see <see cref="ApplyJumpCut"/>).</summary>
        private void ArmJumpCut()
        {
            _jumpCutPending = true;
            _jumpReleased = false;
            _jumpStartTime = Time;
        }
    }
}
