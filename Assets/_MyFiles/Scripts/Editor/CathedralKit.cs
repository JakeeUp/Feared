using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>
    /// SigilsVault's CC0 Modular Cathedral Kit (sigilsvault.itch.io/modular-cathedral-kit), unzipped into
    /// Assets/_ThirdParty. Pieces are built in real metres on an 8 m grid; <see cref="Scale"/> brings a person to the
    /// hero's height. The FBX files carry material names only, so their slots are remapped onto materials made here.
    /// </summary>
    internal static class CathedralKit
    {
        private const string KitRoot = "Assets/_ThirdParty/Modular Cathedral Kit";
        private const string PieceDir = KitRoot + "/Modular Cathedral Pieces/FBX Format";
        private const string PropDir = KitRoot + "/Props/FBX Format";
        private const string MaterialDir = "Assets/_ThirdParty/_Generated/CathedralKit";

        /// <summary>Kit metres to world units: a 1.8 m person is the 1-unit hero, a kit door (6.7 m) a 3.7-unit doorway.</summary>
        internal const float Scale = 0.55f;

        /// <summary>One wall module: 8 kit metres.</summary>
        internal const float Module = 8f * Scale;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        internal static bool Available => Directory.Exists(PieceDir);

        /// <summary>
        /// Places <paramref name="piece"/> turned by <paramref name="yaw"/>, then moves it so its bounds start at
        /// <paramref name="min"/> on each axis (x, bottom, z). Orientation-agnostic: callers say where the piece goes,
        /// not where its pivot is.
        /// </summary>
        internal static GameObject PlaceAt(Transform parent, string piece, Vector3 min, float yaw = 0f, float scale = 1f)
        {
            GameObject go = Instantiate(parent, piece, yaw, scale);
            if (go == null)
            {
                return null;
            }

            Bounds bounds = BoundsOf(go);
            go.transform.position += min - bounds.min;
            return go;
        }

        /// <summary>Places <paramref name="piece"/> with its bounds centred on x/z of <paramref name="center"/> and its bottom on center.y.</summary>
        internal static GameObject PlaceCentered(Transform parent, string piece, Vector3 center, float yaw = 0f, float scale = 1f)
        {
            GameObject go = Instantiate(parent, piece, yaw, scale);
            if (go == null)
            {
                return null;
            }

            Bounds bounds = BoundsOf(go);
            go.transform.position += new Vector3(center.x - bounds.center.x, center.y - bounds.min.y, center.z - bounds.center.z);
            return go;
        }

        internal static Bounds BoundsOf(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            return bounds;
        }

        private static GameObject Instantiate(Transform parent, string piece, float yaw, float scale)
        {
            GameObject asset = Load(piece);
            if (asset == null)
            {
                return null;
            }

            GameObject go = Object.Instantiate(asset, parent, false);
            go.name = $"Kit_{piece}";
            go.transform.localScale = Vector3.one * (Scale * scale);
            // Blender is Z-up: stand the piece up, then turn it.
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
            {
                t.gameObject.isStatic = true;
            }

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            return go;
        }

        /// <summary>Loads a piece (or prop), first setting its importer: metres, kit materials remapped.</summary>
        private static GameObject Load(string piece)
        {
            string path = $"{PieceDir}/{piece}.fbx";
            if (!File.Exists(path))
            {
                path = $"{PropDir}/{piece}.fbx";
            }

            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                return null;
            }

            // The FBX declares centimetres (UnitScaleFactor 1) but the vertices are metres: ignore the file's unit.
            bool dirty = importer.bakeAxisConversion || importer.importAnimation || importer.useFileScale || !Mathf.Approximately(importer.globalScale, 1f)
                         || importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.useFileScale = false;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (string slot in new[] { "trim sheet", "window atlas", "chandelier 2", "door" })
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                Material material = MaterialFor(slot);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out Object current) || current != material)
                {
                    importer.AddRemap(id, material);
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Material MaterialFor(string slot)
        {
            if (Materials.TryGetValue(slot, out Material cached) && cached != null)
            {
                return cached;
            }

            string file = slot.Replace(' ', '_');
            string path = $"{MaterialDir}/M_Kit_{file}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialDir);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            (string diffuse, string emission) = slot switch
            {
                "window atlas" => ("Modular Cathedral Pieces/Textures/1024x1024/window_diffuse_1024x1024.png", "Modular Cathedral Pieces/Textures/1024x1024/window_emission_1024x1024.png"),
                "chandelier 2" => ("Props/Textures/1024x1024/chandelier_diffuse_1024x1024.png", null),
                "door" => ("Props/Textures/1024x1024/door_diffuse_1024x1024.png", null),
                _ => ("Modular Cathedral Pieces/Textures/1024x1024/trim_sheet_1024x1024.png", null),
            };

            material.SetTexture("_BaseMap", Texture($"{KitRoot}/{diffuse}"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.08f);
            if (emission != null)
            {
                // Stained glass glows on its own, so windows read at night.
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetTexture("_EmissionMap", Texture($"{KitRoot}/{emission}"));
                material.SetColor("_EmissionColor", new Color(1.6f, 1.4f, 1.2f));
            }

            EditorUtility.SetDirty(material);
            Materials[slot] = material;
            return material;
        }

        private static Texture2D Texture(string path)
        {
            // Smooth filtering with mipmaps, as authored: some pieces (the towers) stretch a small part of the trim
            // sheet over a big face, and point filtering turns each magnified texel into a flat coloured block.
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && (importer.filterMode != FilterMode.Bilinear || !importer.mipmapEnabled))
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
