using Brawler.Combat;
using Brawler.Progression;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// A treasure chest tucked off the main path. Strike it and the lid swings open on its hinge, a warm light
    /// flares, XP orbs burst out and the wallet gets a score bonus. Opens once and stays open, so an explored
    /// nook reads as done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreasureChest : MonoBehaviour, IDamageable
    {
        // ============ Serialized Fields ============

        [Header("Parts")]
        [Tooltip("Pivot on the lid's back edge; rotated about X to open.")]
        [SerializeField] private Transform _hinge;

        [Tooltip("The chest body, shaken when struck.")]
        [SerializeField] private Transform _body;

        [Tooltip("Hurtbox turned off once opened.")]
        [SerializeField] private Collider _hurtbox;

        [Tooltip("Flares when the lid opens, then settles to a faint glow.")]
        [SerializeField] private Light _glow;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int _xp = 60;
        [SerializeField, Min(0)] private int _scoreReward = 250;
        [SerializeField] private XpOrbSpawner _orbs;
        [SerializeField] private ScoreWallet _wallet;

        [Header("Effects")]
        [SerializeField] private VfxPlayer _vfx;
        [SerializeField] private SpriteClip _openClip;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip[] _openSounds = new AudioClip[0];

        [Tooltip("Lid angle when fully open (degrees).")]
        [SerializeField] private float _openAngle = 105f;

        // ============ Constants ============

        private const float OpenTime = 0.35f;
        private const float ShakeTime = 0.2f;
        private const float FlareTime = 0.6f;
        private const float FlareIntensity = 6f;
        private const float RestIntensity = 0.8f;

        // ============ State ============

        private bool _opened;
        private float _openedAt;
        private Vector3 _bodyPosition;

        // ============ Properties ============

        /// <summary>True once looted.</summary>
        public bool IsOpen => _opened;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_body != null)
            {
                _bodyPosition = _body.localPosition;
            }

            if (_glow != null)
            {
                _glow.enabled = false;
            }
        }

        private void Update()
        {
            if (!_opened)
            {
                return;
            }

            float t = Time.time - _openedAt;
            if (_body != null)
            {
                float shake = t < ShakeTime ? Mathf.Sin(t * 80f) * 0.03f * (1f - t / ShakeTime) : 0f;
                _body.localPosition = _bodyPosition + new Vector3(shake, 0f, 0f);
            }

            if (_hinge != null)
            {
                // Ease out with a small overshoot, like a lid flung back.
                float k = Mathf.Clamp01(t / OpenTime);
                float eased = 1f + 1.6f * Mathf.Pow(k - 1f, 3f) + 0.6f * Mathf.Pow(k - 1f, 2f);
                _hinge.localRotation = Quaternion.Euler(_openAngle * eased, 0f, 0f);
            }

            if (_glow != null)
            {
                _glow.intensity = Mathf.Lerp(FlareIntensity, RestIntensity, Mathf.Clamp01(t / FlareTime));
            }

            if (t > Mathf.Max(OpenTime, ShakeTime, FlareTime))
            {
                enabled = false; // settled; nothing left to animate
            }
        }

        // ============ IDamageable ============

        /// <inheritdoc/>
        public bool TryApplyDamage(in DamageInfo info)
        {
            if (_opened)
            {
                return false;
            }

            Open();
            return true;
        }

        // ============ Helpers ============

        private void Open()
        {
            _opened = true;
            _openedAt = Time.time;
            if (_hurtbox != null)
            {
                _hurtbox.enabled = false;
            }

            if (_glow != null)
            {
                _glow.enabled = true;
                _glow.intensity = FlareIntensity;
            }

            Vector3 mouth = transform.position + Vector3.up * 0.4f;
            if (_orbs != null)
            {
                _orbs.Burst(transform.position, _xp);
            }

            if (_wallet != null && _scoreReward > 0)
            {
                _wallet.Add(_scoreReward);
            }

            if (_vfx != null)
            {
                _vfx.Play(_openClip, mouth);
            }

            if (_audio != null && _openSounds.Length > 0)
            {
                _audio.pitch = Random.Range(0.92f, 1.05f);
                _audio.PlayOneShot(_openSounds[Random.Range(0, _openSounds.Length)]);
            }
        }
    }
}
