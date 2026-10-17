// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414, CS0067

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

        [Tooltip("Gameplay/Dash — Button (K, Space, gamepad south). Raises CommandPressed(Dash) on performed.")]
        [SerializeField] private InputActionReference _dashAction;

        // ============ Cached Delegates (no per-subscribe allocation) ============

        private Action<InputAction.CallbackContext> _onAttackPerformed;
        private Action<InputAction.CallbackContext> _onDashPerformed;

        // ============ Runtime State ============

        private Vector2 _move;

        // ============ Events & Properties ============

        /// <summary>Raised once per button press (from <c>InputAction.performed</c>) with the matching command.</summary>
        public event Action<BufferedCommand> CommandPressed;

        /// <summary>Raw move input (-1..1 per axis, no deadzone), sampled each Update. X = right, Y = up/into screen.</summary>
        public Vector2 Move => _move;

        // ============ Lifecycle ============

        private void Awake()
        {
            // Wiring: cache method-group delegates once so += / -= never allocate.
            _onAttackPerformed = OnAttackPerformed;
            _onDashPerformed = OnDashPerformed;
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

            _move = Vector2.zero;
        }

        private void Update()
        {
            // TODO: cache the raw Move vector (null-guard the reference). Keep it raw - deadzone and depth scaling are
            // PlayerContext's job so they can come from settings.
            // Execution order is already handled by [DefaultExecutionOrder], so this runs before PlayerController reads it.
        }

        // ============ Input Callbacks ============

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            // TODO: raise CommandPressed(Attack). 'performed' fires once per press for a plain Button action.
            // No timestamp here, PlayerController stamps it when it goes into the buffer.
        }

        private void OnDashPerformed(InputAction.CallbackContext context)
        {
            // TODO: raise CommandPressed(Dash).
        }

        // ============ Validation ============

        private void OnValidate()
        {
            if (_moveAction == null || _attackAction == null || _dashAction == null)
            {
                Debug.LogWarning($"{nameof(PlayerInputReader)} on '{name}': assign Move/Attack/Dash InputActionReferences (BrawlerControls › Gameplay).", this);
            }
        }
    }
}
