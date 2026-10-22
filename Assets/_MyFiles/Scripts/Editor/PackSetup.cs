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
            BuildChests(player, vfx, log);
            BuildAudio(player, eventBus, log);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ============ Image Helpers ============

        internal static Color32[] LoadPixels(string path, out int width, out int height)
        {
            return SpriteImport.LoadPixels(path, out width, out height);
        }

        internal static Color32[] Crop(Color32[] pixels, int width, int height, RectInt topLeftRect)
        {
            return SpriteImport.Crop(pixels, width, height, topLeftRect);
        }

        internal static void SavePng(string path, Color32[] pixels, int width, int height)
        {
            SpriteImport.SavePng(path, pixels, width, height);
        }

        internal static bool CropToPng(string source, RectInt topLeftRect, string destination)
        {
            return SpriteImport.CropToPng(source, topLeftRect, destination);
        }

        internal static Sprite ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, Vector4 border = default)
        {
            return SpriteImport.ImportSprite(path, pixelsPerUnit, pivot, border);
        }

        internal static Texture2D ImportTiledTexture(string path)
        {
            return SpriteImport.ImportTiledTexture(path);
        }

        // ============ Scene Helpers ============

        internal static Transform PlayerCamera(PlayerController player)
        {
            return EditorUtil.PlayerCamera(player);
        }

        internal static Transform FindOrCreateChild(Transform parent, string name)
        {
            return EditorUtil.FindOrCreateChild(parent, name);
        }
    }
}
