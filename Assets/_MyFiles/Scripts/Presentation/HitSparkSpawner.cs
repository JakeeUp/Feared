using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Plays a one-shot impact effect (e.g. SlashFX) at the contact point of every <see cref="HitEvent"/>.
    /// Keeps a small fixed ring of sprite objects created in Awake, so a hit never allocates; the oldest spark is
    /// reused when they are all busy. Sparks face the screen and are mirrored by the knockback direction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitSparkSpawner : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus asset. Listens for HitEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Camera transform; sparks copy its rotation so they face the screen.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Material for the spark sprites (an unlit/transparent sprite material).")]
        [SerializeField] private Material _material;

        [Header("Effect")]
        [Tooltip("Spark clips; each hit picks the next one in turn so consecutive hits vary.")]
        [SerializeField] private SpriteClip[] _clips = new SpriteClip[0];

        [Tooltip("How many sparks can be on screen at once.")]
        [SerializeField, Min(1)] private int _capacity = 8;

        [Tooltip("Pulls the spark toward the camera (units) so it draws over the target it hit.")]
        [SerializeField, Min(0f)] private float _towardCamera = 0.2f;

        // ============ State ============

        private SpriteAnimator[] _sparks;
        private int _next;
        private int _clipIndex;
        private Action<HitEvent> _onHit;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;
            _sparks = new SpriteAnimator[_capacity];
            for (int i = 0; i < _capacity; i++)
            {
                var go = new GameObject($"Spark_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                if (_material != null)
                {
                    sr.sharedMaterial = _material;
                }

                sr.enabled = false;
                _sparks[i] = go.AddComponent<SpriteAnimator>();
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
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                SpriteAnimator spark = _sparks[i];
                if (spark.Renderer.enabled && spark.IsFinished)
                {
                    spark.Renderer.enabled = false;
                }
            }
        }

        // ============ Helpers ============

        private void OnHit(HitEvent evt)
        {
            if (_clips.Length == 0)
            {
                return;
            }

            SpriteClip clip = _clips[_clipIndex];
            _clipIndex = (_clipIndex + 1) % _clips.Length;
            if (clip == null)
            {
                return;
            }

            SpriteAnimator spark = _sparks[_next];
            _next = (_next + 1) % _sparks.Length;

            Quaternion rotation = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;
            Vector3 toCamera = _cameraTransform != null ? -_cameraTransform.forward : Vector3.back;
            spark.transform.SetPositionAndRotation(evt.Damage.HitPoint + toCamera * _towardCamera, rotation);

            // Sweep in the direction the target is knocked.
            spark.Renderer.flipX = (evt.Damage.Knockback.x < 0f) != clip.Mirrored;
            spark.Renderer.enabled = true;
            spark.Play(clip, true);
        }
    }
}
