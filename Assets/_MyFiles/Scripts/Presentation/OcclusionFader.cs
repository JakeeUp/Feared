using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Fades any level geometry that blocks the camera's view of the player, not just the hand-picked
    /// <see cref="ForegroundOccluder"/>s. Each frame it tests the cached bounds of every opaque scene mesh against the
    /// sight line from the camera to the player's chest; a blocker gets its materials swapped for
    /// <c>Brawler/OccluderDither</c> twins and its <c>_Fade</c> eased down, then swapped back once it is clear.
    /// </summary>
    /// <remarks>
    /// Candidates are gathered once in Start (the level is static), so spawned objects are not considered. Things
    /// shorter than <see cref="_minHeight"/> above the player's feet (floors, steps, low rubble) and things that
    /// contain the player (whole rooms) are ignored. The camera and target come from the statics published by
    /// <see cref="DepthOfFieldFocus"/>, like <see cref="ForegroundOccluder"/>.
    /// </remarks>
    [DefaultExecutionOrder(CameraRig.ExecutionOrder + 20)]
    [DisallowMultipleComponent]
    public sealed class OcclusionFader : MonoBehaviour
    {
        // ============ Types ============

        private struct Candidate
        {
            public Renderer Renderer;
            public Bounds Bounds;
        }

        private sealed class Faded
        {
            public Material[] Original;
            public float Fade = 1f;
            public bool Blocking;
        }

        // ============ Serialized Fields ============

        [Tooltip("Brawler/OccluderDither. Falls back to Shader.Find when empty.")]
        [SerializeField] private Shader _ditherShader;

        [Header("Fade")]
        [Tooltip("_Fade while blocking the player (0 = invisible, 1 = solid).")]
        [SerializeField, Range(0f, 1f)] private float _blockingFade = 0.3f;

        [Tooltip("How fast the fade eases toward its goal (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0.1f)] private float _fadeRate = 10f;

        [Header("Test")]
        [Tooltip("Height above the player's pivot the sight line aims at (units).")]
        [SerializeField] private float _targetHeight = 0.6f;

        [Tooltip("Bounds grow by this (units) so the test acts like a capsule around the sight line.")]
        [SerializeField, Min(0f)] private float _padding = 0.35f;

        [Tooltip("Ignore geometry whose top is lower than this above the player's feet (floors, steps).")]
        [SerializeField, Min(0f)] private float _minHeight = 1.1f;

        // ============ State ============

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TopMapId = Shader.PropertyToID("_TopMap");
        private static readonly int SideMapId = Shader.PropertyToID("_SideMap");
        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private static readonly int SideColorId = Shader.PropertyToID("_SideColor");
        private static readonly int WorldProjectionId = Shader.PropertyToID("_WorldProjection");

        private readonly List<Candidate> _candidates = new List<Candidate>(1024);
        private readonly Dictionary<Renderer, Faded> _faded = new Dictionary<Renderer, Faded>();
        private readonly List<Renderer> _settled = new List<Renderer>();
        private readonly Dictionary<Material, Material> _variants = new Dictionary<Material, Material>();
        private MaterialPropertyBlock _block;

        // ============ Lifecycle ============

        private void Start()
        {
            if (_ditherShader == null)
            {
                _ditherShader = Shader.Find("Brawler/OccluderDither");
            }

            if (_ditherShader == null)
            {
                Debug.LogWarning($"{nameof(OcclusionFader)}: Brawler/OccluderDither not found; occlusion fading is off.", this);
                enabled = false;
                return;
            }

            _block = new MaterialPropertyBlock();
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>())
            {
                if (IsCandidate(r))
                {
                    _candidates.Add(new Candidate { Renderer = r, Bounds = r.bounds });
                }
            }
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<Renderer, Faded> pair in _faded)
            {
                Restore(pair.Key, pair.Value);
            }

            _faded.Clear();
        }

        private void OnDestroy()
        {
            foreach (Material variant in _variants.Values)
            {
                Destroy(variant);
            }
        }

        private void LateUpdate()
        {
            foreach (Faded faded in _faded.Values)
            {
                faded.Blocking = false;
            }

            MarkBlockers();

            float t = 1f - Mathf.Exp(-_fadeRate * Time.unscaledDeltaTime);
            foreach (KeyValuePair<Renderer, Faded> pair in _faded)
            {
                Renderer r = pair.Key;
                Faded faded = pair.Value;
                if (r == null)
                {
                    _settled.Add(r);
                    continue;
                }

                float goal = faded.Blocking ? _blockingFade : 1f;
                faded.Fade = Mathf.Abs(goal - faded.Fade) < 0.01f ? goal : Mathf.Lerp(faded.Fade, goal, t);
                if (!faded.Blocking && faded.Fade >= 1f)
                {
                    _settled.Add(r);
                    continue;
                }

                _block.SetFloat(FadeId, faded.Fade);
                r.SetPropertyBlock(_block);
            }

            foreach (Renderer r in _settled)
            {
                if (r != null)
                {
                    Restore(r, _faded[r]);
                }

                _faded.Remove(r);
            }

            _settled.Clear();
        }

        // ============ Helpers ============

        private void MarkBlockers()
        {
            Transform target = ForegroundOccluder.Target;
            Camera cam = ForegroundOccluder.ViewCamera != null ? ForegroundOccluder.ViewCamera : Camera.main;
            if (target == null || cam == null)
            {
                return;
            }

            Vector3 feet = target.position;
            Vector3 from = cam.transform.position;
            Vector3 to = feet + Vector3.up * _targetHeight;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f)
            {
                return;
            }

            var ray = new Ray(from, delta / length);
            var segment = new Bounds(from, Vector3.zero);
            segment.Encapsulate(to);
            segment.Expand(_padding * 2f);
            Vector3 grow = Vector3.one * (_padding * 2f);
            float minTop = feet.y + _minHeight;

            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];
                Bounds bounds = c.Bounds;
                if (bounds.max.y < minTop || !bounds.Intersects(segment) || bounds.Contains(to))
                {
                    continue;
                }

                bounds.size += grow;
                if (!bounds.IntersectRay(ray, out float hit) || hit >= length - _padding)
                {
                    continue;
                }

                Renderer r = c.Renderer;
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!_faded.TryGetValue(r, out Faded faded))
                {
                    faded = new Faded { Original = r.sharedMaterials };
                    r.sharedMaterials = GetVariants(faded.Original);
                    _faded.Add(r, faded);
                }

                faded.Blocking = true;
            }
        }

        private bool IsCandidate(MeshRenderer r)
        {
            if (!r.enabled || r.GetComponentInParent<ForegroundOccluder>() != null)
            {
                return false;
            }

            foreach (Material m in r.sharedMaterials)
            {
                // Opaque only: cutout/transparent things (and anything already dithered) are left alone.
                if (m == null || m.shader == _ditherShader || m.renderQueue >= 2450)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Restore(Renderer r, Faded faded)
        {
            r.sharedMaterials = faded.Original;
            r.SetPropertyBlock(null);
        }

        private Material[] GetVariants(Material[] source)
        {
            var result = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (!_variants.TryGetValue(source[i], out Material variant))
                {
                    variant = CreateVariant(source[i]);
                    _variants.Add(source[i], variant);
                }

                result[i] = variant;
            }

            return result;
        }

        /// <summary>Runtime twin of a level material on the dither shader (mirrors the editor's OccluderTools.Sync).</summary>
        private Material CreateVariant(Material source)
        {
            var variant = new Material(_ditherShader) { name = source.name + " (Occluded)" };
            bool worldProjection = source.HasProperty(TopMapId) && source.HasProperty(SideMapId);
            variant.SetFloat(WorldProjectionId, worldProjection ? 1f : 0f);
            if (worldProjection)
            {
                foreach (int id in new[] { TopMapId, SideMapId })
                {
                    variant.SetTexture(id, source.GetTexture(id));
                    variant.SetTextureScale(id, source.GetTextureScale(id));
                    variant.SetTextureOffset(id, source.GetTextureOffset(id));
                }

                variant.SetColor(TopColorId, source.GetColor(TopColorId));
                variant.SetColor(SideColorId, source.GetColor(SideColorId));
                return variant;
            }

            int texId = source.HasProperty(BaseMapId) ? BaseMapId : source.HasProperty(MainTexId) ? MainTexId : -1;
            if (texId != -1)
            {
                variant.SetTexture(BaseMapId, source.GetTexture(texId));
                variant.SetTextureScale(BaseMapId, source.GetTextureScale(texId));
                variant.SetTextureOffset(BaseMapId, source.GetTextureOffset(texId));
            }

            int colorId = source.HasProperty(BaseColorId) ? BaseColorId : source.HasProperty(ColorId) ? ColorId : -1;
            if (colorId != -1)
            {
                variant.SetColor(BaseColorId, source.GetColor(colorId));
            }

            return variant;
        }
    }
}
