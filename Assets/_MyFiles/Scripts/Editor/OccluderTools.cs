using System.Collections.Generic;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    internal static class OccluderTools
    {
        private const string ShaderName = "Brawler/OccluderDither";
        private const string ShaderPath = "Assets/_MyFiles/Art/Shaders/OccluderDither.shader";
        private const string MaterialFolder = "Assets/_MyFiles/Art/Materials/Occluder";
        private const string MaterialPrefix = "M_Occluder_";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Dictionary<Material, Material> Variants = new Dictionary<Material, Material>();

        internal static void MakeOccluder(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Shader shader = FindShader();
            if (shader == null)
            {
                Debug.LogError($"[OccluderTools] Shader '{ShaderName}' not found ({ShaderPath}).");
                return;
            }

            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader == shader)
                    {
                        continue;
                    }

                    materials[i] = GetVariant(source, shader);
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            if (go.GetComponent<ForegroundOccluder>() == null)
            {
                go.AddComponent<ForegroundOccluder>();
            }
        }

        private static Shader FindShader()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader != null)
            {
                return shader;
            }

            AssetDatabase.ImportAsset(ShaderPath);
            return AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        }

        private static Material GetVariant(Material source, Shader shader)
        {
            if (!Variants.TryGetValue(source, out Material variant) || variant == null)
            {
                EnsureFolder();
                string path = $"{MaterialFolder}/{MaterialPrefix}{Sanitize(source.name)}.mat";
                variant = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (variant == null)
                {
                    variant = new Material(shader) { name = MaterialPrefix + source.name };
                    AssetDatabase.CreateAsset(variant, path);
                }

                Variants[source] = variant;
            }

            if (variant.shader != shader)
            {
                variant.shader = shader;
            }

            Sync(source, variant);
            return variant;
        }

        private static void Sync(Material source, Material variant)
        {
            bool worldProjection = source.HasProperty("_TopMap") && source.HasProperty("_SideMap");
            variant.SetFloat("_WorldProjection", worldProjection ? 1f : 0f);
            if (worldProjection)
            {
                foreach (string property in new[] { "_TopMap", "_SideMap" })
                {
                    variant.SetTexture(property, source.GetTexture(property));
                    variant.SetTextureScale(property, source.GetTextureScale(property));
                    variant.SetTextureOffset(property, source.GetTextureOffset(property));
                }

                variant.SetColor("_TopColor", source.GetColor("_TopColor"));
                variant.SetColor("_SideColor", source.GetColor("_SideColor"));
                EditorUtility.SetDirty(variant);
                return;
            }

            int texId = source.HasProperty(BaseMapId) ? BaseMapId : source.HasProperty(MainTexId) ? MainTexId : -1;
            int colorId = source.HasProperty(BaseColorId) ? BaseColorId : source.HasProperty(ColorId) ? ColorId : -1;
            bool dirty = false;

            if (texId != -1)
            {
                Texture tex = source.GetTexture(texId);
                Vector2 scale = source.GetTextureScale(texId);
                Vector2 offset = source.GetTextureOffset(texId);
                if (variant.GetTexture(BaseMapId) != tex || variant.GetTextureScale(BaseMapId) != scale || variant.GetTextureOffset(BaseMapId) != offset)
                {
                    variant.SetTexture(BaseMapId, tex);
                    variant.SetTextureScale(BaseMapId, scale);
                    variant.SetTextureOffset(BaseMapId, offset);
                    dirty = true;
                }
            }

            if (colorId != -1)
            {
                Color color = source.GetColor(colorId);
                if (variant.GetColor(BaseColorId) != color)
                {
                    variant.SetColor(BaseColorId, color);
                    dirty = true;
                }
            }

            if (dirty)
            {
                EditorUtility.SetDirty(variant);
            }
        }

        internal static void SyncExisting(Material source)
        {
            string path = $"{MaterialFolder}/{MaterialPrefix}{Sanitize(source.name)}.mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (variant != null)
            {
                Sync(source, variant);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/_MyFiles/Art/Materials", "Occluder");
            }
        }

        private static string Sanitize(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Replace(" (Instance)", string.Empty);
        }
    }
}
