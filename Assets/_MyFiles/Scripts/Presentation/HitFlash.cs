using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Flashes a sprite white when its owner is hit (listens for <see cref="HitEvent"/> with Target == owner).
    /// Writes <c>_FlashColor</c> (rgb + amount in a) through a MaterialPropertyBlock, read by Brawler/SpriteAlphaClip.
    /// Runs on unscaled time so the flash is visible during the hitstop freeze, which is when it matters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        // ============ Constants ============

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        // ============ Serialized Fields ============

        [Tooltip("Shared event bus asset. Listens for HitEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Root transform that HitEvent.Target is compared against (the character root).")]
        [SerializeField] private Transform _owner;

        [Tooltip("Renderer to flash. Defaults to the SpriteRenderer on this GameObject.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Flash colour.")]
        [SerializeField] private Color _color = Color.white;

        [Tooltip("Flash length in real seconds.")]
        [SerializeField, Min(0.01f)] private float _duration = 0.12f;

        // ============ State ============

        private MaterialPropertyBlock _block;
        private Action<HitEvent> _onHit;
        private float _remaining;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;
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
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
            }

            _remaining = 0f;
            Write(0f);
        }

        private void LateUpdate()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            Write(Mathf.Clamp01(_remaining / _duration));
        }

        // ============ Helpers ============

        private void OnHit(HitEvent evt)
        {
            if (evt.Target != _owner)
            {
                return;
            }

            _remaining = _duration;
            Write(1f);
        }

        private void Write(float amount)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(FlashColorId, new Color(_color.r, _color.g, _color.b, amount));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
