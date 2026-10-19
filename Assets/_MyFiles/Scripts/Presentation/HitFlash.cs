using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Flashes a sprite when its owner is hit (<see cref="HitEvent"/> with Target == owner) or parried
    /// (<see cref="ParryEvent"/> with Attacker == owner), and on demand through <see cref="Flash"/> (attack telegraphs).
    /// A hit holds the flash at full strength for the hitstop freeze, then fades, and jitters the sprite sideways
    /// while frozen so the victim visibly takes the impact.
    /// Writes <c>_FlashColor</c> (rgb + amount in a) through a MaterialPropertyBlock, read by Brawler/SpriteAlphaClip.
    /// Runs on unscaled time so the flash is visible during the hitstop freeze, which is when it matters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        // ============ Constants ============

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        // ============ Serialized Fields ============

        [Tooltip("Shared event bus asset. Listens for HitEvent and ParryEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Root transform that HitEvent.Target / ParryEvent.Attacker is compared against (the character root).")]
        [SerializeField] private Transform _owner;

        [Tooltip("Renderer to flash. Defaults to the SpriteRenderer on this GameObject.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Flash colour.")]
        [SerializeField] private Color _color = Color.white;

        [Tooltip("Fade-out length in real seconds, after the hold that matches the hit's freeze.")]
        [SerializeField, Min(0.01f)] private float _duration = 0.09f;

        [Header("Impact Jitter")]
        [Tooltip("Sideways jitter (units) of the sprite while a hit's freeze holds. 0 = off.")]
        [SerializeField, Min(0f)] private float _jitter = 0.035f;

        [Header("Parried")]
        [Tooltip("Flash colour when the owner's attack is parried.")]
        [SerializeField] private Color _parriedColor = new Color(1f, 0.93f, 0.7f, 1f);

        // ============ State ============

        private MaterialPropertyBlock _block;
        private Action<HitEvent> _onHit;
        private Action<ParryEvent> _onParry;
        private float _hold;
        private float _remaining;
        private float _currentDuration;
        private Color _currentColor;
        private bool _jittering;
        private int _jitterSign = 1;
        private int _startFrame = -1;
        private Vector3 _basePosition;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;
            _onParry = OnParry;
            _block = new MaterialPropertyBlock();
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_owner == null)
            {
                _owner = transform.root;
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
                _eventBus.Subscribe(_onParry);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
                _eventBus.Unsubscribe(_onParry);
            }

            _hold = 0f;
            _remaining = 0f;
            StopJitter();
            Write(0f);
        }

        private void LateUpdate()
        {
            if (_hold <= 0f && _remaining <= 0f)
            {
                return;
            }

            // The trigger frame's delta elapsed before the hit; counting it would cut the first frame of flash.
            float dt = Time.frameCount == _startFrame ? 0f : Time.unscaledDeltaTime;
            if (_hold > 0f)
            {
                _hold -= dt;
                if (_jittering)
                {
                    _jitterSign = -_jitterSign;
                    transform.localPosition = _basePosition + new Vector3(_jitter * _jitterSign, 0f, 0f);
                }

                if (_hold > 0f)
                {
                    Write(1f);
                    return;
                }

                StopJitter();
                _remaining += _hold; // carry the overshoot into the fade
                _hold = 0f;
            }

            _remaining -= dt;
            Write(Mathf.Clamp01(_remaining / _currentDuration));
        }

        // ============ API ============

        /// <summary>Flashes <paramref name="color"/>, fading out over <paramref name="duration"/> real seconds (e.g. attack telegraphs).</summary>
        public void Flash(Color color, float duration)
        {
            Flash(color, duration, 0f);
        }

        /// <summary>Flashes <paramref name="color"/> at full strength for <paramref name="hold"/> real seconds, then fades over <paramref name="duration"/>.</summary>
        public void Flash(Color color, float duration, float hold)
        {
            _currentColor = color;
            _currentDuration = Mathf.Max(0.01f, duration);
            _remaining = _currentDuration;
            _hold = Mathf.Max(0f, hold);
            _startFrame = Time.frameCount;
            Write(1f);
        }

        // ============ Helpers ============

        private void OnHit(HitEvent evt)
        {
            if (evt.Target != _owner)
            {
                return;
            }

            float hold = FrameTime.ToSeconds(evt.HitStopFrames);
            Flash(_color, _duration, hold);
            if (_jitter > 0f && hold > 0f && !_jittering)
            {
                _basePosition = transform.localPosition;
                _jittering = true;
            }
        }

        private void OnParry(ParryEvent evt)
        {
            if (evt.Attacker != _owner)
            {
                return;
            }

            Flash(_parriedColor, _duration * 2f, FrameTime.ToSeconds(evt.HitStopFrames));
        }

        private void StopJitter()
        {
            if (_jittering)
            {
                transform.localPosition = _basePosition;
                _jittering = false;
            }
        }

        private void Write(float amount)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(FlashColorId, new Color(_currentColor.r, _currentColor.g, _currentColor.b, amount));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
