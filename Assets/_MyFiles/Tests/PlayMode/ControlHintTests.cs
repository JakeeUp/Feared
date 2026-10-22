using Brawler.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Brawler.Tests
{
    public sealed class ControlHintTests : InputTestFixture
    {
        private InputActionAsset _asset;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = _asset.AddActionMap("Gameplay");
            InputAction jump = map.AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
            jump.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
            jump.Enable();
            ControlHints.Register(_asset);
        }

        public override void TearDown()
        {
            ControlHints.Unregister(_asset);
            Object.DestroyImmediate(_asset);
            base.TearDown();
        }

        [Test]
        public void FormatUsesTheLastPressedDevice()
        {
            PressAndRelease(_keyboard.spaceKey);
            string keyboardHint = ControlHints.Format("Jump: {Jump}");
            Assert.That(ControlHints.Format("{Unmapped}"), Is.EqualTo("{Unmapped}"));

            PressAndRelease(_gamepad.buttonSouth);
            string gamepadHint = ControlHints.Format("Jump: {Jump}");

            PressAndRelease(_keyboard.spaceKey);
            string keyboardAgain = ControlHints.Format("Jump: {Jump}");

            Assert.AreNotEqual(keyboardHint, gamepadHint);
            Assert.AreEqual(keyboardHint, keyboardAgain);
            Assert.That(keyboardHint, Does.Not.Contain("{Jump}"));
            Assert.That(gamepadHint, Does.Not.Contain("{Jump}"));
        }
    }
}
