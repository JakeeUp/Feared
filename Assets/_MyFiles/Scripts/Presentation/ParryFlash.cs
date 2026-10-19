using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// The parry effect: at the contact point of every <see cref="ParryEvent"/>, a white four-point star (with a smaller
    /// diagonal glint) pops and vanishes in ~0.12 s while a thin gold ring snaps outward and fades over ~0.22 s.
    /// Both sprites are generated procedurally in Awake (no art), live in a small fixed ring of slots (no per-parry
    /// allocation) and run on <b>unscaled</b> time, so the whole effect plays inside the parry's hitstop freeze.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParryFlash : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Star texture size (pixels, square).</summary>
        private const int StarTextureSize = 64;

        /// <summary>Ring texture size (pixels, square). Large so the line stays thin at full size.</summary>
        private const int RingTextureSize = 128;

        /// <summary>Ring line half-thickness in ring-texture pixels.</summary>
        private const float RingHalfThicknessPx = 1.6f;

        /// <summary>Glint size relative to the star.</summary>
        private const float GlintScale = 0.55f;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus. Listens for ParryEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Camera transform; the effect faces the screen.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Transparent sprite material (same as the hit sparks).")]
        [SerializeField] private Material _material;

        [Tooltip("Parries that can overlap on screen.")]
        [SerializeField, Min(1)] private int _capacity = 3;

        [Tooltip("Pulls the effect toward the camera (units). Keep >= 0.6: character sprites pull their depth up to 0.5 toward the camera.")]
        [SerializeField, Min(0f)] private float _towardCamera = 0.7f;

        [Header("Star")]
        [Tooltip("Star colour (the core flash).")]
        [SerializeField] private Color _starColor = Color.white;

        [Tooltip("Star width in units at its peak.")]
        [SerializeField, Min(0.01f)] private float _starSize = 1.1f;

        [Tooltip("Real seconds the star is visible.")]
        [SerializeField, Min(0.01f)] private float _starSeconds = 0.12f;

        [Header("Ring")]
        [Tooltip("Ring colour.")]
        [SerializeField] private Color _ringColor = new Color(1f, 0.84f, 0.4f, 1f);

        [Tooltip("Ring diameter (units) when it appears.")]
        [SerializeField, Min(0f)] private float _ringStartSize = 0.2f;

        [Tooltip("Ring diameter (units) when it has faded.")]
        [SerializeField, Min(0.01f)] private float _ringEndSize = 1.6f;

        [Tooltip("Real seconds the ring takes to expand and fade.")]
        [SerializeField, Min(0.01f)] private float _ringSeconds = 0.22f;

        // ============ Types ============

        private sealed class Slot
        {
            public SpriteRenderer Star;
            public SpriteRenderer Glint;
            public SpriteRenderer Ring;
            public float Elapsed;
            public float Angle;
            public int StartFrame;
            public bool Active;
        }

        // ============ State ============

        private Slot[] _slots;
        private int _next;
        private float _angleSign = 1f;
        private Texture2D _starTexture;
        private Texture2D _ringTexture;
        private Sprite _starSprite;
        private Sprite _ringSprite;
        private Action<ParryEvent> _onParry;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onParry = OnParry;
            _starTexture = BuildStarTexture();
            _ringTexture = BuildRingTexture();
            _starSprite = Sprite.Create(_starTexture, new Rect(0f, 0f, StarTextureSize, StarTextureSize), new Vector2(0.5f, 0.5f), StarTextureSize);
            _ringSprite = Sprite.Create(_ringTexture, new Rect(0f, 0f, RingTextureSize, RingTextureSize), new Vector2(0.5f, 0.5f), RingTextureSize);

            _slots = new Slot[_capacity];
            for (int i = 0; i < _capacity; i++)
            {
                var root = new GameObject($"ParryFlash_{i}");
                root.transform.SetParent(transform, false);
                _slots[i] = new Slot
                {
                    Ring = CreateRenderer(root.transform, "Ring", _ringSprite, 0),
                    Star = CreateRenderer(root.transform, "Star", _starSprite, 1),
                    Glint = CreateRenderer(root.transform, "Glint", _starSprite, 2),
                };
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onParry);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onParry);
            }

            if (_slots == null)
            {
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                Hide(_slots[i]);
            }
        }

        private void OnDestroy()
        {
            Destroy(_starSprite);
            Destroy(_ringSprite);
            Destroy(_starTexture);
            Destroy(_ringTexture);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Quaternion cameraRotation = _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (!slot.Active)
                {
                    continue;
                }

                // The spawn frame's delta elapsed before the parry; skipping it keeps frame 0 at full size.
                if (Time.frameCount != slot.StartFrame)
                {
                    slot.Elapsed += dt;
                }

                if (slot.Elapsed >= _ringSeconds && slot.Elapsed >= _starSeconds)
                {
                    Hide(slot);
                    continue;
                }

                Animate(slot, cameraRotation);
            }
        }

        // ============ Event Handlers ============

        private void OnParry(ParryEvent evt)
        {
            Slot slot = _slots[_next];
            _next = (_next + 1) % _slots.Length;

            Vector3 toCamera = _cameraTransform != null ? -_cameraTransform.forward : Vector3.back;
            slot.Star.transform.parent.position = evt.Point + toCamera * _towardCamera;

            // A slight, alternating tilt so back-to-back parries don't stamp the identical star.
            _angleSign = -_angleSign;
            slot.Angle = _angleSign * UnityEngine.Random.Range(8f, 18f);
            slot.Elapsed = 0f;
            slot.StartFrame = Time.frameCount;
            slot.Active = true;
            slot.Star.enabled = slot.Glint.enabled = slot.Ring.enabled = true;
            Animate(slot, _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity);
        }

        // ============ Helpers ============

        private void Animate(Slot slot, Quaternion cameraRotation)
        {
            slot.Star.transform.parent.rotation = cameraRotation;

            // Star: overshoots on frame 0, settles, then thins and fades fast (alpha squared).
            float s = Mathf.Clamp01(slot.Elapsed / _starSeconds);
            float starScale = s < 0.25f ? Mathf.Lerp(1.3f, 1f, s / 0.25f) : Mathf.Lerp(1f, 0.55f, (s - 0.25f) / 0.75f);
            float fade = s < 0.35f ? 1f : 1f - (s - 0.35f) / 0.65f;
            float starAlpha = fade * fade;
            Color star = Color.Lerp(_starColor, _ringColor, s);
            star.a *= starAlpha;

            slot.Star.transform.localRotation = Quaternion.Euler(0f, 0f, slot.Angle);
            slot.Star.transform.localScale = Vector3.one * (_starSize * starScale);
            slot.Star.color = star;
            slot.Glint.transform.localRotation = Quaternion.Euler(0f, 0f, slot.Angle + 45f);
            slot.Glint.transform.localScale = Vector3.one * (_starSize * starScale * GlintScale);
            slot.Glint.color = star;

            // Ring: ease-out cubic expansion, linear fade.
            float r = Mathf.Clamp01(slot.Elapsed / _ringSeconds);
            float eased = 1f - (1f - r) * (1f - r) * (1f - r);
            Color ring = _ringColor;
            ring.a *= 1f - r;
            slot.Ring.transform.localScale = Vector3.one * Mathf.Lerp(_ringStartSize, _ringEndSize, eased);
            slot.Ring.color = ring;
        }

        private static void Hide(Slot slot)
        {
            slot.Active = false;
            slot.Star.enabled = slot.Glint.enabled = slot.Ring.enabled = false;
        }

        private SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            if (_material != null)
            {
                sr.sharedMaterial = _material;
            }

            sr.enabled = false;
            return sr;
        }

        /// <summary>Four-point star: two long tapered rays (horizontal + vertical) over a small soft core.</summary>
        private static Texture2D BuildStarTexture()
        {
            var pixels = new Color32[StarTextureSize * StarTextureSize];
            float half = (StarTextureSize - 1) * 0.5f;
            for (int y = 0; y < StarTextureSize; y++)
            {
                for (int x = 0; x < StarTextureSize; x++)
                {
                    float u = (x - half) / half;
                    float v = (y - half) / half;
                    float core = Mathf.Exp(-(u * u + v * v) * 28f);
                    float alpha = Mathf.Clamp01(core + Ray(u, v) + Ray(v, u));
                    pixels[y * StarTextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return BuildTexture("ParryStar", StarTextureSize, pixels);
        }

        /// <summary>A ray along <paramref name="along"/>: tapers to a point at the edge, sharp falloff across.</summary>
        private static float Ray(float along, float across)
        {
            float length = 1f - Mathf.Abs(along);
            if (length <= 0f)
            {
                return 0f;
            }

            float width = 0.07f * length + 0.005f;
            float t = across / width;
            return Mathf.Exp(-t * t) * length;
        }

        /// <summary>Thin anti-aliased circle touching the texture's edge.</summary>
        private static Texture2D BuildRingTexture()
        {
            var pixels = new Color32[RingTextureSize * RingTextureSize];
            float half = (RingTextureSize - 1) * 0.5f;
            float radius = half - RingHalfThicknessPx * 2f;
            for (int y = 0; y < RingTextureSize; y++)
            {
                for (int x = 0; x < RingTextureSize; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float d = (Mathf.Sqrt(dx * dx + dy * dy) - radius) / RingHalfThicknessPx;
                    float alpha = Mathf.Exp(-d * d);
                    pixels[y * RingTextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return BuildTexture("ParryRing", RingTextureSize, pixels);
        }

        private static Texture2D BuildTexture(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
