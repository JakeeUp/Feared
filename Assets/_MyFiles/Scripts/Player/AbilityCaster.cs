using System;
using Brawler.Combat;
using Brawler.Presentation;
using Brawler.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Brawler.Player
{
    /// <summary>
    /// Fires the player's special abilities (bought in the shop, see <see cref="AbilityLoadout"/>). They're buffs, not
    /// states, so they work while running, attacking or airborne; only hitstun blocks them.
    /// <list type="bullet">
    /// <item><b>Blade Ring</b>: swords orbit the player for its duration; a radial hitbox re-arms every
    /// <c>RearmInterval</c> so each enemy can be hit again, knocking them away.</item>
    /// <item><b>Bubble Shield</b>: makes the player invulnerable for its duration (shown as a bubble).</item>
    /// <item><b>Mend</b>: heals a share of max health instantly.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityCaster : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private AbilityLoadout _loadout;
        [SerializeField] private Health _health;

        [Tooltip("Flashes for Mend.")]
        [SerializeField] private HitFlash _flash;

        [Tooltip("Blade sprites and spark facing.")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Input")]
        [SerializeField] private InputActionReference _bladeRingAction;
        [SerializeField] private InputActionReference _shieldAction;
        [SerializeField] private InputActionReference _mendAction;

        [Header("Blade Ring")]
        [Tooltip("Radial hitbox around the player (Hitbox with Radial Knockback ticked).")]
        [SerializeField] private Hitbox _ringHitbox;

        [Tooltip("Damage, knockback and box size of the ring.")]
        [SerializeField] private AttackData _ringAttack;

        [Tooltip("Orbiting blade visuals (children, hidden while inactive).")]
        [SerializeField] private Transform[] _blades = new Transform[0];

        [SerializeField, Min(0f)] private float _ringRadius = 0.95f;
        [SerializeField] private float _ringHeight = 0.55f;

        [Tooltip("Orbit speed (degrees per second).")]
        [SerializeField] private float _ringSpin = 420f;

        [Tooltip("Seconds between re-arms; each re-arm lets the ring hit the same enemy again.")]
        [SerializeField, Min(0.05f)] private float _rearmInterval = 0.35f;

        [Header("Bubble Shield")]
        [Tooltip("Bubble visual (child, hidden while inactive).")]
        [SerializeField] private Transform _bubble;

        [Header("Mend")]
        [SerializeField] private Color _mendFlashColor = new Color(0.4f, 1f, 0.5f, 1f);

        [Header("Effects (optional)")]
        [SerializeField] private VfxPlayer _vfx;
        [SerializeField] private SpriteClip _ringVfx;
        [SerializeField] private SpriteClip _shieldVfx;
        [SerializeField] private SpriteClip _mendVfx;

        // ============ State ============

        private Action<InputAction.CallbackContext> _onBladeRing;
        private Action<InputAction.CallbackContext> _onShield;
        private Action<InputAction.CallbackContext> _onMend;
        private bool _ringRunning;
        private float _nextRearm;
        private float _ringAngle;
        private Vector3 _bubbleScale = Vector3.one;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onBladeRing = _ => TryCast(AbilityType.BladeRing);
            _onShield = _ => TryCast(AbilityType.BubbleShield);
            _onMend = _ => TryCast(AbilityType.Mend);
            if (_bubble != null)
            {
                _bubbleScale = _bubble.localScale;
            }

            SetBladesVisible(false);
            SetBubbleVisible(false);
        }

        private void OnEnable()
        {
            Bind(_bladeRingAction, _onBladeRing, true);
            Bind(_shieldAction, _onShield, true);
            Bind(_mendAction, _onMend, true);
        }

        private void OnDisable()
        {
            Bind(_bladeRingAction, _onBladeRing, false);
            Bind(_shieldAction, _onShield, false);
            Bind(_mendAction, _onMend, false);
            StopRing();
            if (_player != null)
            {
                _player.ExternalInvulnerable = false;
            }

            SetBubbleVisible(false);
        }

        private void FixedUpdate()
        {
            if (!_ringRunning || _ringHitbox == null)
            {
                return;
            }

            if (_loadout == null || !_loadout.IsActive(AbilityType.BladeRing, Time.time))
            {
                StopRing();
                return;
            }

            if (Time.time >= _nextRearm)
            {
                _nextRearm = Time.time + _rearmInterval;
                _ringHitbox.BeginAttack(_ringAttack, 1); // fresh hit set: everyone can be hit again
            }

            _ringHitbox.Sweep();
        }

        private void LateUpdate()
        {
            float now = Time.time;
            UpdateBlades(Time.deltaTime);

            bool shielded = _loadout != null && _loadout.IsActive(AbilityType.BubbleShield, now);
            if (_player != null)
            {
                _player.ExternalInvulnerable = shielded;
            }

            SetBubbleVisible(shielded);
            if (shielded && _bubble != null)
            {
                // Gentle pulse so the bubble reads as alive; unscaled so it keeps breathing through hitstop.
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.04f;
                _bubble.localScale = _bubbleScale * pulse;
            }
        }

        // ============ Helpers ============

        private void TryCast(AbilityType type)
        {
            // Not while stunned, and not while the game is paused (upgrade menu).
            if (_loadout == null || _player == null || Time.timeScale <= 0f
                || ReferenceEquals(_player.CurrentState, _player.Context?.States.Hitstun))
            {
                return;
            }

            if (!_loadout.TryActivate(type, Time.time))
            {
                return;
            }

            switch (type)
            {
                case AbilityType.BladeRing:
                    StartRing();
                    PlayVfx(_ringVfx);
                    break;
                case AbilityType.BubbleShield:
                    PlayVfx(_shieldVfx);
                    break;
                case AbilityType.Mend:
                    Mend();
                    PlayVfx(_mendVfx);
                    break;
            }
        }

        private void StartRing()
        {
            if (_ringHitbox == null || _ringAttack == null)
            {
                return;
            }

            _ringRunning = true;
            _nextRearm = 0f;
            SetBladesVisible(true);
        }

        private void StopRing()
        {
            if (!_ringRunning)
            {
                return;
            }

            _ringRunning = false;
            if (_ringHitbox != null)
            {
                _ringHitbox.EndAttack();
            }

            SetBladesVisible(false);
        }

        private void Mend()
        {
            AbilityDefinition mend = _loadout.Get(AbilityType.Mend);
            if (_health == null || mend == null)
            {
                return;
            }

            _health.Heal(Mathf.Max(1, Mathf.RoundToInt(_health.Max * mend.Value)));
            if (_flash != null)
            {
                _flash.Flash(_mendFlashColor, 0.5f);
            }
        }

        private void PlayVfx(SpriteClip clip)
        {
            if (_vfx != null)
            {
                _vfx.Play(clip, transform.position + Vector3.up * _ringHeight);
            }
        }

        /// <summary>Blades circle the player on the ground plane (so they pass in front and behind), facing the screen.</summary>
        private void UpdateBlades(float deltaTime)
        {
            if (!_ringRunning || _blades.Length == 0)
            {
                return;
            }

            _ringAngle += _ringSpin * deltaTime;
            Quaternion face = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;
            Vector3 centre = transform.position + Vector3.up * _ringHeight;
            for (int i = 0; i < _blades.Length; i++)
            {
                float angle = (_ringAngle + 360f * i / _blades.Length) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _ringRadius;
                // Spin each sprite about the view axis too, so the swords look like they're slashing around.
                _blades[i].SetPositionAndRotation(centre + offset, face * Quaternion.Euler(0f, 0f, -_ringAngle * 2f + 90f * i));
            }
        }

        private void SetBladesVisible(bool visible)
        {
            foreach (Transform blade in _blades)
            {
                if (blade != null && blade.gameObject.activeSelf != visible)
                {
                    blade.gameObject.SetActive(visible);
                }
            }
        }

        private void SetBubbleVisible(bool visible)
        {
            if (_bubble != null && _bubble.gameObject.activeSelf != visible)
            {
                _bubble.gameObject.SetActive(visible);
            }
        }

        private static void Bind(InputActionReference reference, Action<InputAction.CallbackContext> handler, bool subscribe)
        {
            if (reference == null || reference.action == null)
            {
                return;
            }

            if (subscribe)
            {
                reference.action.performed += handler;
                reference.action.Enable();
            }
            else
            {
                reference.action.performed -= handler;
            }
        }
    }
}
