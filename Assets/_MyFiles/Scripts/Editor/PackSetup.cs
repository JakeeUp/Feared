using System.IO;
using System.Text;
using Brawler.Core;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling: menu <c>Brawler/Apply Asset Packs (Church, VFX, Pots)</c>. Applies the third-party packs in
    /// <c>Assets/_ThirdParty</c> to the open scene:
    /// <list type="bullet">
    /// <item>Gothicvania Church: stone/brick textures on the floor, platforms and walls, a back wall of church
    /// panels (window, pillar, altar, gargoyle, torches) and two standing columns (PackSetup.Church.cs).</item>
    /// <item>PVFX Foundry + church FX: ability, dust and enemy-death effects through a <see cref="VfxPlayer"/>
    /// (PackSetup.Vfx.cs).</item>
    /// <item>Breakable Pots: pots around the arena that shatter for score (PackSetup.Pots.cs).</item>
    /// </list>
    /// The Androx GUI skin for the HUD is generated here too (PackSetup.Ui.cs) but applied by Setup HUD.
    /// </summary>
    /// <remarks>
    /// Safe to re-run. Images cut out of the packs are written to <c>Assets/_ThirdParty/_Generated</c>, which is
    /// git-ignored with the packs; materials, meshes and clips live in <c>_MyFiles</c> and only reference them.
    /// Missing packs are skipped with a note in the log.
    /// </remarks>
    public static partial class PackSetup
    {
        // ============ Paths ============

        private const string ThirdParty = "Assets/_ThirdParty";
        private const string GeneratedDir = ThirdParty + "/_Generated";
        private const string Root = "Assets/_MyFiles";
        private const string EventBusPath = Root + "/Data/Core/EventBus.asset";

        // ============ Menu ============

        [MenuItem("Brawler/Apply Asset Packs (Church, VFX, Pots)")]
        public static void Run()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Apply Asset Packs", "No PlayerController in the open scene. Run Setup Hero first.", "OK");
                return;
            }

            var log = new StringBuilder("[Brawler] Apply Asset Packs\n");
            var eventBus = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
            Transform cameraTransform = PlayerCamera(player);

            if (!CathedralBuilder.IsCathedralScene())
            {
                BuildChurch(log); // the Cathedral skins its own geometry
            }
            VfxPlayer vfx = BuildVfx(player, eventBus, cameraTransform, log);
            BuildPots(player, vfx, cameraTransform, log);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ============ Image Helpers ============

        /// <summary>Reads a PNG's pixels straight from disk (bottom-up rows, like Texture2D).</summary>
        internal static Color32[] LoadPixels(string path, out int width, out int height)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            Color32[] pixels = tex.GetPixels32();
            width = tex.width;
            height = tex.height;
            Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>Cuts a rectangle given in image coordinates (top-left origin, as in an image editor).</summary>
        internal static Color32[] Crop(Color32[] pixels, int width, int height, RectInt topLeftRect)
        {
            int w = topLeftRect.width, h = topLeftRect.height;
            int bottom = height - topLeftRect.y - h;
            var block = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                System.Array.Copy(pixels, (bottom + row) * width + topLeftRect.x, block, row * w, w);
            }

            return block;
        }

        /// <summary>Writes pixels to a PNG under the project and imports it.</summary>
        internal static void SavePng(string path, Color32[] pixels, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Crops a rectangle (top-left image coordinates) out of a pack image into its own PNG.</summary>
        internal static bool CropToPng(string source, RectInt topLeftRect, string destination)
        {
            if (!File.Exists(source))
            {
                return false;
            }

            Color32[] all = LoadPixels(source, out int w, out int h);
            SavePng(destination, Crop(all, w, h, topLeftRect), topLeftRect.width, topLeftRect.height);
            return true;
        }

        /// <summary>Imports an image as a crisp single sprite.</summary>
        /// <param name="path">Asset path.</param>
        /// <param name="pixelsPerUnit">World scale.</param>
        /// <param name="pivot">Normalised pivot (0,0 = bottom-left).</param>
        /// <param name="border">9-slice border in pixels (left, bottom, right, top); zero for none.</param>
        internal static Sprite ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, Vector4 border = default)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Imports an image as a repeating pixel-art texture (for tiled 3D surfaces).</summary>
        internal static Texture2D ImportTiledTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ============ Scene Helpers ============

        internal static Transform PlayerCamera(PlayerController player)
        {
            BillboardSprite billboard = player.GetComponentInChildren<BillboardSprite>(true);
            return billboard != null ? billboard.CameraTransform : null;
        }

        internal static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
