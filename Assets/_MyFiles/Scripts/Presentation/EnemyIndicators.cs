using System.Collections.Generic;
using Brawler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Presentation
{
    /// <summary>
    /// Red chevrons on the screen edge pointing at enemies that are off-screen (or hidden behind the camera), so the
    /// player always knows where the fight is. Reads <see cref="Hurtbox.Enemies"/>; enemies that are invulnerable
    /// (a dormant boss, the dead) fade out, and arrows ease in and out so brief i-frames don't flicker.
    /// </summary>
    /// <remarks>
    /// Lives on a full-screen RectTransform under the HUD canvas (Screen Space Overlay). The chevron sprite is
    /// generated in Awake, so nothing needs wiring beyond the camera and player.
    /// </remarks>
    [DefaultExecutionOrder(CameraRig.ExecutionOrder + 30)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class EnemyIndicators : MonoBehaviour
    {
        // ============ Types ============

        private sealed class Arrow
        {
            public Image Image;
            public float Alpha;
            public bool Seen;
        }

        // ============ Serialized Fields ============

        [Tooltip("Camera the world is viewed through. Null = the occluders' shared camera, then Camera.main.")]
        [SerializeField] private Camera _camera;

        [Tooltip("The player. Enemies farther than Max Distance from it get no arrow.")]
        [SerializeField] private Transform _player;

        [SerializeField] private Color _color = new Color(1f, 0.12f, 0.1f, 1f);

        [Tooltip("Arrow size (canvas units).")]
        [SerializeField, Min(4f)] private float _size = 34f;

        [Tooltip("Inset from the screen edge (canvas units).")]
        [SerializeField, Min(0f)] private float _margin = 46f;

        [Tooltip("Only enemies within this distance of the player (units) are pointed at.")]
        [SerializeField, Min(1f)] private float _maxDistance = 28f;

        [Tooltip("Height above the enemy's pivot to aim at (units).")]
        [SerializeField] private float _aimHeight = 0.8f;

        [Tooltip("Fade speed (1/s).")]
        [SerializeField, Min(0.1f)] private float _fadeRate = 10f;

        // ============ State ============

        private readonly Dictionary<Hurtbox, Arrow> _arrows = new Dictionary<Hurtbox, Arrow>();
        private readonly Stack<Image> _pool = new Stack<Image>();
        private readonly List<Hurtbox> _gone = new List<Hurtbox>();
        private RectTransform _rect;
        private Sprite _sprite;

        // ============ Lifecycle ============

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _sprite = CreateChevron();
        }

        private void OnDestroy()
        {
            if (_sprite != null)
            {
                Destroy(_sprite.texture);
                Destroy(_sprite);
            }
        }

        private void LateUpdate()
        {
            Camera cam = _camera != null ? _camera : ForegroundOccluder.ViewCamera != null ? ForegroundOccluder.ViewCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            foreach (Arrow arrow in _arrows.Values)
            {
                arrow.Seen = false;
            }

            Rect area = _rect.rect;
            Vector2 half = area.size * 0.5f - Vector2.one * _margin;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            IReadOnlyList<Hurtbox> enemies = Hurtbox.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Hurtbox enemy = enemies[i];
                Transform root = enemy.OwnerRoot;
                if (root == null || enemy.IsInvulnerable)
                {
                    continue;
                }

                if (_player != null && (root.position - _player.position).sqrMagnitude > _maxDistance * _maxDistance)
                {
                    continue;
                }

                Vector3 viewport = cam.WorldToViewportPoint(root.position + Vector3.up * _aimHeight);
                bool behind = viewport.z < 0f;
                bool onScreen = !behind && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
                if (onScreen)
                {
                    continue;
                }

                // Direction from the screen centre; behind the camera the projection is mirrored.
                Vector2 dir = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                if (behind)
                {
                    dir = -dir;
                }

                if (dir.sqrMagnitude < 1e-6f)
                {
                    dir = Vector2.down;
                }

                // Push out to the inset screen rectangle along that direction.
                float scale = Mathf.Min(
                    Mathf.Abs(dir.x) > 1e-6f ? half.x / Mathf.Abs(dir.x * area.width) : float.MaxValue,
                    Mathf.Abs(dir.y) > 1e-6f ? half.y / Mathf.Abs(dir.y * area.height) : float.MaxValue);
                Vector2 position = new Vector2(dir.x * area.width, dir.y * area.height) * scale;

                if (!_arrows.TryGetValue(enemy, out Arrow arrow))
                {
                    arrow = new Arrow { Image = Rent() };
                    _arrows.Add(enemy, arrow);
                }

                arrow.Seen = true;
                RectTransform t = arrow.Image.rectTransform;
                t.anchoredPosition = position;
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                arrow.Image.color = new Color(_color.r, _color.g, _color.b, _color.a * arrow.Alpha * pulse);
            }

            float t01 = 1f - Mathf.Exp(-_fadeRate * Time.unscaledDeltaTime);
            foreach (KeyValuePair<Hurtbox, Arrow> pair in _arrows)
            {
                Arrow arrow = pair.Value;
                arrow.Alpha = Mathf.Lerp(arrow.Alpha, arrow.Seen ? 1f : 0f, t01);
                if (!arrow.Seen)
                {
                    Color c = arrow.Image.color;
                    c.a = _color.a * arrow.Alpha;
                    arrow.Image.color = c;
                    if (arrow.Alpha < 0.02f || pair.Key == null)
                    {
                        _gone.Add(pair.Key);
                    }
                }
            }

            foreach (Hurtbox key in _gone)
            {
                Return(_arrows[key].Image);
                _arrows.Remove(key);
            }

            _gone.Clear();
        }

        // ============ Helpers ============

        private Image Rent()
        {
            Image image;
            if (_pool.Count > 0)
            {
                image = _pool.Pop();
                image.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("EnemyArrow", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                image = go.GetComponent<Image>();
                image.sprite = _sprite;
                image.raycastTarget = false;
                RectTransform t = image.rectTransform;
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
                t.sizeDelta = new Vector2(_size, _size);
            }

            image.color = Color.clear;
            return image;
        }

        private void Return(Image image)
        {
            image.gameObject.SetActive(false);
            _pool.Push(image);
        }

        /// <summary>A soft-edged chevron pointing +X (right), white so the Image color tints it.</summary>
        private static Sprite CreateChevron()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "EnemyChevron" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside a "V" made of two thick strokes meeting at the tip (right edge).
                    float px = (x + 0.5f) / size;
                    float py = (y + 0.5f) / size - 0.5f;
                    float tipX = 0.88f;
                    float along = tipX - px;
                    float stroke = Mathf.Abs(Mathf.Abs(py) - along * 0.9f);
                    float inside = along >= 0f && along <= 0.62f ? 1f - Mathf.Clamp01((stroke - 0.09f) / 0.04f) : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
