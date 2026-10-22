using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Brawler.Editor
{
    internal static class StoneMaterials
    {
        private const string ChurchFolder = "Assets/_MyFiles/Art/Materials/Church";
        private const string WorldFolder = ChurchFolder + "/World";
        private const string ShaderPath = "Assets/_MyFiles/Art/Shaders/WorldStone.shader";

        internal static Material Get(string topName, string sideName, Material top, Material side)
        {
            Directory.CreateDirectory(WorldFolder);
            string path = $"{WorldFolder}/M_Stone_{topName}_{sideName}.mat";
            Shader shader = Shader.Find("Brawler/WorldStone") ?? AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"Missing stone shader: {ShaderPath}");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            SetSurface(material, "_TopMap", top);
            SetSurface(material, "_SideMap", side);
            material.SetColor("_TopColor", top.GetColor("_BaseColor"));
            material.SetColor("_SideColor", side.GetColor("_BaseColor"));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetSurface(Material material, string property, Material source)
        {
            Vector2 tile = PackSetup.TileWorldSize(source);
            material.SetTexture(property, source.GetTexture("_BaseMap"));
            material.SetTextureScale(property, new Vector2(1f / tile.x, 1f / tile.y));
            material.SetTextureOffset(property, Vector2.zero);
        }

        internal static void SyncTints()
        {
            var sources = new Dictionary<string, Material>();
            foreach (string name in new[] { "floor", "floor_dark", "block", "brick" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{ChurchFolder}/M_Church_{name}.mat");
                if (material != null)
                {
                    sources[name] = material;
                    OccluderTools.SyncExisting(material);
                }
            }

            foreach (var top in sources)
            {
                foreach (var side in sources)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>($"{WorldFolder}/M_Stone_{top.Key}_{side.Key}.mat");
                    if (material == null)
                    {
                        continue;
                    }

                    material.SetColor("_TopColor", top.Value.GetColor("_BaseColor"));
                    material.SetColor("_SideColor", side.Value.GetColor("_BaseColor"));
                    EditorUtility.SetDirty(material);
                    OccluderTools.SyncExisting(material);
                }
            }
        }
    }
}
