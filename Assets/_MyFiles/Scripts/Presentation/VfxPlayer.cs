using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Plays one-shot sprite effects (sliced VFX sheets) in the world: on its own for
    /// <see cref="EnemyDefeatedEvent"/>, and on request through <see cref="Play"/> (abilities, landing dust, props).
    /// Keeps a fixed ring of sprite objects created in Awake; the oldest effect is reused when all are busy.
    /// Effects face the screen like the characters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxPlayer : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus. Listens for EnemyDefeatedEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Camera transform; effects copy its rotation so they face the screen.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Material for the effect sprites (a transparent sprite material).")]
        [SerializeField] private Material _material;

        [Tooltip("How many effects can be on screen at once.")]
        [SerializeField, Min(1)] private int _capacity = 16;

        [Tooltip("Pulls effects toward the camera (units) so they draw over whatever they sit on. Keep >= 0.6: character sprites pull their depth up to 0.5 toward the camera.")]
        [SerializeField, Min(0f)] private float _towardCamera = 0.65f;

        [Header("Event Effects")]
        [Tooltip("Played where an enemy dies.")]
        [SerializeField] private SpriteClip _defeatClip;

        [Tooltip("Height above the enemy's feet for the death effect.")]
        [SerializeField] private float _defeatHeight = 0.45f;

        // ============ State ============

        private SpriteAnimator[] _effects;
        private int _next;
        private Action<EnemyDefeatedEvent> _onDefeated;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onDefeated = OnDefeated;
            _effects = new SpriteAnimator[_capacity];
            for (int i = 0; i < _capacity; i++)
            {
                var go = new GameObject($"Vfx_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                if (_material != null)
                {
                    sr.sharedMaterial = _material;
                }

                sr.enabled = false;
                _effects[i] = go.AddComponent<SpriteAnimator>();
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onDefeated);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onDefeated);
            }
        }

        private void LateUpdate()
        {
            Quaternion rotation = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;
            for (int i = 0; i < _effects.Length; i++)
            {
                SpriteAnimator effect = _effects[i];
                if (!effect.Renderer.enabled)
                {
                    continue;
                }

                if (effect.IsFinished)
                {
                    effect.Renderer.enabled = false;
                    continue;
                }

                effect.transform.rotation = rotation; // the camera may move while an effect plays
            }
        }

        // ============ API ============

        /// <summary>Plays <paramref name="clip"/> once at <paramref name="position"/>.</summary>
        /// <param name="clip">Effect to play (non-looping clips end on their own; looping ones play one pass).</param>
        /// <param name="position">World position of the clip's pivot.</param>
        /// <param name="scale">Uniform size multiplier.</param>
        /// <param name="flip">Mirror horizontally.</param>
        public void Play(SpriteClip clip, Vector3 position, float scale = 1f, bool flip = false)
        {
            Play(clip, position, Color.white, scale, flip);
        }

        /// <summary>Plays <paramref name="clip"/> once at <paramref name="position"/>, tinted.</summary>
        public void Play(SpriteClip clip, Vector3 position, Color tint, float scale = 1f, bool flip = false)
        {
            if (clip == null || _effects == null)
            {
                return;
            }

            SpriteAnimator effect = _effects[_next];
            _next = (_next + 1) % _effects.Length;

            Quaternion rotation = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;
            Vector3 toCamera = _cameraTransform != null ? -_cameraTransform.forward : Vector3.back;
            effect.transform.SetPositionAndRotation(position + toCamera * _towardCamera, rotation);
            effect.transform.localScale = Vector3.one * scale;

            effect.Renderer.flipX = flip;
            effect.Renderer.color = tint;
            effect.Renderer.enabled = true;
            effect.Play(clip, true);
        }

        // ============ Helpers ============

        private void OnDefeated(EnemyDefeatedEvent evt)
        {
            Play(_defeatClip, evt.Position + Vector3.up * _defeatHeight);
        }
    }
}
