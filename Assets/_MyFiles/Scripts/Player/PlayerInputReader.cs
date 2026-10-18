
using System;
using Brawler.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Brawler.Player
{
    /// <summary>
    /// Thin adapter from the Input System to gameplay: exposes the raw move vector and raises
    /// <see cref="CommandPressed"/> for button presses. Uses serialized <see cref="InputActionReference"/>s
    /// (no generated wrapper class, no string lookups). Knows nothing about states or buffering —
    /// <c>PlayerController</c> forwards commands into the <see cref="InputBuffer"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Script execution order: before <c>PlayerController</c> (default 0) so states see this frame's input.</summary>
        public const int ExecutionOrder = -50;

        // ============ Serialized Fields ============

        [Header("Actions (BrawlerControls / Gameplay)")]
        [Tooltip("Gameplay/Move — Value Vector2 (WASD/arrows composite + left stick).")]
        [SerializeField] private InputActionReference _moveAction;

        [Tooltip("Gameplay/Attack — Button (J, mouse left, gamepad west). Raises CommandPressed(Attack) on performed.")]
        [SerializeField] private InputActionReference _attackAction;

        [Tooltip("Gameplay/Dash — Button (Left Shift, K, gamepad east). Raises CommandPressed(Dash) on performed.")]
        [SerializeField] private InputActionReference _dashAction;

        [Tooltip("Gameplay/Jump — Button (Space, gamepad south). Raises CommandPressed(Jump) on performed; held state drives variable jump height.")]
        [SerializeField] private InputActionReference _jumpAction;

        // ============ Cached Delegates (no per-subscribe allocation) ============

        private Action<InputAction.CallbackContext> _onAttackPerformed;
        private Action<InputAction.CallbackContext> _onDashPerformed;
        private Action<InputAction.CallbackContext> _onJumpPerformed;

        // ============ Runtime State ============

        private Vector2 _move;
        private bool _jumpHeld;

        // ============ Events & Properties ============

        /// <summary>Raised once per button press (from <c>InputAction.performed</c>) with the matching command.</summary>
        public event Action<BufferedCommand> CommandPressed;

        /// <summary>Raw move input (-1..1 per axis, no deadzone), sampled each Update. X = right, Y = up/into screen.</summary>
        public Vector2 Move => _move;

        /// <summary>True while the Jump button is held (sampled each Update). Releasing early shortens the jump.</summary>
        public bool JumpHeld => _jumpHeld;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: cache method-group delegates once so += / -= never allocate.
            _onAttackPerformed = OnAttackPerformed;
            _onDashPerformed = OnDashPerformed;
            _onJumpPerformed = OnJumpPerformed;
        }

        private void OnEnable()
        {
            // Wiring: enable actions and subscribe cached handlers.
            if (_moveAction != null && _moveAction.action != null)
            {
                _moveAction.action.Enable();
            }

            if (_attackAction != null && _attackAction.action != null)
            {
                _attackAction.action.performed += _onAttackPerformed;
                _attackAction.action.Enable();
            }

            if (_dashAction != null && _dashAction.action != null)
            {
                _dashAction.action.performed += _onDashPerformed;
                _dashAction.action.Enable();
            }

            if (_jumpAction != null && _jumpAction.action != null)
            {
                _jumpAction.action.performed += _onJumpPerformed;
                _jumpAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            // Wiring: reverse of OnEnable. Clear move so a disabled reader never reports stale input.
            if (_moveAction != null && _moveAction.action != null)
            {
                _moveAction.action.Disable();
            }

            if (_attackAction != null && _attackAction.action != null)
            {
                _attackAction.action.performed -= _onAttackPerformed;
                _attackAction.action.Disable();
            }

            if (_dashAction != null && _dashAction.action != null)
            {
                _dashAction.action.performed -= _onDashPerformed;
                _dashAction.action.Disable();
            }

            if (_jumpAction != null && _jumpAction.action != null)
            {
                _jumpAction.action.performed -= _onJumpPerformed;
                _jumpAction.action.Disable();
            }

            _move = Vector2.zero;
            _jumpHeld = false;
        }

        private void Update()
        {
            // Raw on purpose: deadzone and depth scaling live in PlayerContext so they come from settings.
            _move = _moveAction != null && _moveAction.action != null
                ? _moveAction.action.ReadValue<Vector2>()
                : Vector2.zero;
            _jumpHeld = _jumpAction != null && _jumpAction.action != null && _jumpAction.action.IsPressed();
        }

        // ============ Input Callbacks ============

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            CommandPressed?.Invoke(BufferedCommand.Attack);
        }

        private void OnDashPerformed(InputAction.CallbackContext context)
        {
            CommandPressed?.Invoke(BufferedCommand.Dash);
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            CommandPressed?.Invoke(BufferedCommand.Jump);
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_moveAction == null || _attackAction == null || _dashAction == null || _jumpAction == null)
            {
                Debug.LogWarning($"{nameof(PlayerInputReader)} on '{name}': assign Move/Attack/Dash/Jump InputActionReferences (BrawlerControls › Gameplay).", this);
            }
        }
    }
}
