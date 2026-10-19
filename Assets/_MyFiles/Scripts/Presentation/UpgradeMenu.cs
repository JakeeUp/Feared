using System;
using Brawler.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Brawler.Presentation
{
    /// <summary>
    /// Pause menu for spending upgrade points (Vitality = max health, Strength = damage). Toggled by the Menu action;
    /// pauses the game (timeScale 0) and disables gameplay input while open. Works with mouse and gamepad (the first
    /// button is selected on open).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeMenu : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("Sources")]
        [SerializeField] private PlayerProgression _progression;

        [Tooltip("Gameplay/Menu (Tab, gamepad Start).")]
        [SerializeField] private InputActionReference _toggleAction;

        [Tooltip("Components disabled while the menu is open (e.g. the PlayerInputReader) so clicks don't become attacks.")]
        [SerializeField] private Behaviour[] _disableWhileOpen = new Behaviour[0];

        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _pointsText;
        [SerializeField] private Button _vitalityButton;
        [SerializeField] private Text _vitalityText;
        [SerializeField] private Button _strengthButton;
        [SerializeField] private Text _strengthText;
        [SerializeField] private Button _closeButton;

        // ============ State ============

        private Action<InputAction.CallbackContext> _onToggle;
        private Action _onChanged;
        private bool _open;

        // ============ Properties ============

        /// <summary>True while the menu is showing (and the game paused).</summary>
        public bool IsOpen => _open;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onToggle = OnToggle;
            _onChanged = Refresh;
            if (_vitalityButton != null)
            {
                _vitalityButton.onClick.AddListener(UpgradeVitality);
            }

            if (_strengthButton != null)
            {
                _strengthButton.onClick.AddListener(UpgradeStrength);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_toggleAction != null && _toggleAction.action != null)
            {
                _toggleAction.action.performed += _onToggle;
                _toggleAction.action.Enable();
            }

            if (_progression != null)
            {
                _progression.Changed += _onChanged;
            }
        }

        private void OnDisable()
        {
            if (_toggleAction != null && _toggleAction.action != null)
            {
                _toggleAction.action.performed -= _onToggle;
            }

            if (_progression != null)
            {
                _progression.Changed -= _onChanged;
            }

            if (_open)
            {
                Close(); // never leave the game paused
            }
        }

        // ============ API ============

        /// <summary>Opens the menu and pauses the game.</summary>
        public void Open()
        {
            // A hitstop owns timeScale for a few frames; opening mid-freeze would fight it.
            if (_open || _panel == null || Time.timeScale < 1f)
            {
                return;
            }

            _open = true;
            _panel.SetActive(true);
            Time.timeScale = 0f;
            SetGameplayEnabled(false);
            Refresh();

            if (EventSystem.current != null && _vitalityButton != null)
            {
                EventSystem.current.SetSelectedGameObject(_vitalityButton.gameObject);
            }
        }

        /// <summary>Closes the menu and resumes the game.</summary>
        public void Close()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            Time.timeScale = 1f;
            SetGameplayEnabled(true);
        }

        // ============ Helpers ============

        private void OnToggle(InputAction.CallbackContext context)
        {
            if (_open)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void UpgradeVitality()
        {
            _progression?.TryUpgrade(UpgradeType.Vitality);
        }

        private void UpgradeStrength()
        {
            _progression?.TryUpgrade(UpgradeType.Strength);
        }

        private void Refresh()
        {
            if (_progression == null || _progression.Settings == null)
            {
                return;
            }

            ProgressionSettings settings = _progression.Settings;
            int max = settings.MaxRank;
            if (_pointsText != null)
            {
                _pointsText.text = $"POINTS  {_progression.Points}";
            }

            if (_vitalityText != null)
            {
                _vitalityText.text = $"VITALITY   {_progression.Rank(UpgradeType.Vitality)}/{max}\n+{settings.HealthPerRank} max health";
            }

            if (_strengthText != null)
            {
                _strengthText.text = $"STRENGTH   {_progression.Rank(UpgradeType.Strength)}/{max}\n+{Mathf.RoundToInt(settings.DamagePerRank * 100f)}% damage";
            }

            if (_vitalityButton != null)
            {
                _vitalityButton.interactable = _progression.CanUpgrade(UpgradeType.Vitality);
            }

            if (_strengthButton != null)
            {
                _strengthButton.interactable = _progression.CanUpgrade(UpgradeType.Strength);
            }
        }

        private void SetGameplayEnabled(bool enabled)
        {
            foreach (Behaviour behaviour in _disableWhileOpen)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = enabled;
                }
            }
        }
    }
}
