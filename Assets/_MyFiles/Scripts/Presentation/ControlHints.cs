using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Brawler.Presentation
{
    public static class ControlHints
    {
        private const string KeyboardGroup = "Keyboard&Mouse";
        private const string GamepadGroup = "Gamepad";
        private static readonly List<InputAction> RegisteredActions = new List<InputAction>();
        private static InputActionAsset _asset;
        private static int _registrations;
        private static string _group = KeyboardGroup;

        public static event Action Changed;

        public static void Register(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (_asset == asset)
            {
                _registrations++;
                return;
            }

            Clear();
            _asset = asset;
            _registrations = 1;
            InputActionMap map = asset.FindActionMap("Gameplay", false);
            if (map == null)
            {
                return;
            }

            foreach (InputAction action in map.actions)
            {
                action.performed += OnActionPerformed;
                RegisteredActions.Add(action);
            }

            Changed?.Invoke();
        }

        public static void Unregister(InputActionAsset asset)
        {
            if (_asset != asset || _registrations == 0)
            {
                return;
            }

            if (--_registrations == 0)
            {
                Clear();
            }
        }

        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || _asset == null)
            {
                return text;
            }

            string result = text;
            foreach (InputAction action in RegisteredActions)
            {
                string token = "{" + action.name + "}";
                if (result.Contains(token))
                {
                    string display = action.GetBindingDisplayString(group: _group);
                    if (!string.IsNullOrEmpty(display))
                    {
                        result = result.Replace(token, display);
                    }
                }
            }

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
            Changed = null;
            _group = KeyboardGroup;
        }

        private static void OnActionPerformed(InputAction.CallbackContext context)
        {
            if (context.action.name == "Move" && context.ReadValue<Vector2>().sqrMagnitude < 0.04f)
            {
                return;
            }

            string group = context.control.device is Gamepad ? GamepadGroup : KeyboardGroup;
            if (_group == group)
            {
                return;
            }

            _group = group;
            Changed?.Invoke();
        }

        private static void Clear()
        {
            foreach (InputAction action in RegisteredActions)
            {
                action.performed -= OnActionPerformed;
            }

            RegisteredActions.Clear();
            _asset = null;
            _registrations = 0;
        }
    }
}
