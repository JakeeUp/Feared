using Brawler.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Brawler.Editor
{
    /// <summary>
    /// Play-mode input in the editor. By default the Input System only feeds the keyboard to the game while the Game
    /// view has focus, so a click in the Scene view or Hierarchy silently "breaks" movement. This project sends all
    /// device input to the game during play mode instead. Also logs a once-a-second <c>[MoveDiag]</c> line while
    /// playing (keys held, move vector, state, position, time scale) so a stuck hero can be diagnosed from the log.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeInput
    {
        private const string SettingsPath = "Assets/_MyFiles/Data/Core/InputSystemSettings.asset";
        private const string ConfigKey = "com.unity.input.settings";

        private static double _nextLog;
        private static int _lines;

        static PlayModeInput()
        {
            EditorApplication.delayCall += EnsureSettings;
            EditorApplication.playModeStateChanged += state => _lines = 0;
            EditorApplication.update += LogWhilePlaying;
        }

        private static void EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<InputSettings>(SettingsPath);
            if (settings == null)
            {
                settings = Object.Instantiate(InputSystem.settings);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            if (settings.editorInputBehaviorInPlayMode != InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView)
            {
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            EditorBuildSettings.AddConfigObject(ConfigKey, settings, true);
            if (InputSystem.settings != settings)
            {
                InputSystem.settings = settings;
            }
        }

        private static void LogWhilePlaying()
        {
            // A short burst per play session is enough to diagnose, without flooding the console.
            if (!EditorApplication.isPlaying || _lines >= 20 || EditorApplication.timeSinceStartup < _nextLog)
            {
                return;
            }

            _nextLog = EditorApplication.timeSinceStartup + 1.0;
            _lines++;
            var player = Object.FindAnyObjectByType<PlayerController>();
            var reader = player != null ? player.GetComponent<PlayerInputReader>() : null;
            Keyboard keyboard = Keyboard.current;
            string keys = keyboard == null ? "no keyboard" :
                $"WASD={(keyboard.wKey.isPressed ? "W" : "")}{(keyboard.aKey.isPressed ? "A" : "")}{(keyboard.sKey.isPressed ? "S" : "")}{(keyboard.dKey.isPressed ? "D" : "")} arrows={(keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed)}";
            Debug.Log($"[MoveDiag] {keys} gamepads={Gamepad.all.Count} move={(reader != null ? reader.Move.ToString() : "no reader")} " +
                      $"state={(player != null && player.CurrentState != null ? player.CurrentState.GetType().Name : "none")} " +
                      $"pos={(player != null ? player.transform.position.ToString() : "-")} timeScale={Time.timeScale} focus={(EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.titleContent.text : "none")} " +
                      $"playerActive={(player != null && player.isActiveAndEnabled)} readerEnabled={(reader != null && reader.isActiveAndEnabled)}");
        }
    }
}
