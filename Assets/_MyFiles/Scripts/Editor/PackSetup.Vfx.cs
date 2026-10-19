using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Brawler.Core;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>
    /// VFX half of Apply Asset Packs: slices the chosen PVFX Foundry effects (96 px grid sheets with a JSON manifest
    /// each) and the church enemy-death burst into clips, builds the scene <see cref="VfxPlayer"/>, and wires it to
    /// deaths, the abilities and the player's movement dust (parries use the procedural <see cref="ParryFlash"/>).
    /// </summary>
    public static partial class PackSetup
    {
        // ============ Paths ============

        private const string PvfxRoot = ThirdParty + "/effects";
        private const string ChurchDeathFrames = ThirdParty + "/gothicvania church files/Assets/SPRITES/fx/enemy-death/enemy-death-sprites";
        private const string VfxGenerated = GeneratedDir + "/Vfx";
        private const string VfxClipDir = Root + "/Data/Animation/Vfx";

        // ============ PVFX Format (see the pack's IMPORTING.md) ============

        private const int PvfxCell = 96;
        private const int PvfxColumns = 5;
        private const float PvfxFps = 20f; // every v0.10 frame lasts 50 ms

        // ============ Effect Table ============

        /// <summary>Clip name, PVFX effect id, and how wide the 96 px canvas is in world units.</summary>
        private static readonly (string Clip, string Effect, float Width)[] PvfxEffects =
        {
            ("Heal", "radiant-heal", 1.8f),
            ("Shield", "hush-lock", 1.9f),
            ("Ring", "crescent-slash", 2f),
            ("Land", "landing-dust", 1.5f),
            ("Puff", "smoke-puff", 1.1f),
        };

        /// <summary>The church death burst is 81 px wide; shown about 1.4 units wide.</summary>
        private const float DeathWidth = 1.4f;

        // ============ Manifest ============

#pragma warning disable CS0649 // filled by JsonUtility
        [System.Serializable]
        private sealed class PvfxManifest
        {
            public int[] pivot;
            public PvfxFrame[] frames;
        }

        [System.Serializable]
        private sealed class PvfxFrame
        {
            public int frame;
        }
#pragma warning restore CS0649

        // ============ Build ============

        private static VfxPlayer BuildVfx(PlayerController player, EventBusAsset eventBus, Transform cameraTransform, StringBuilder log)
        {
            Directory.CreateDirectory(VfxClipDir);
            var clips = new Dictionary<string, SpriteClip>();
            foreach (var effect in PvfxEffects)
            {
                SpriteClip clip = BuildPvfxClip(effect.Clip, effect.Effect, effect.Width, log);
                if (clip != null)
                {
                    clips[effect.Clip] = clip;
                }
            }

            SpriteClip death = BuildChurchDeathClip(log);
            if (death != null)
            {
                clips["Death"] = death;
            }

            if (clips.Count == 0)
            {
                log.AppendLine("  ! vfx skipped: no effect packs found");
                return null;
            }

            GameObject go = GameObject.Find("Vfx") ?? HeroSetup.CreateRoot("Vfx");
            var vfx = HeroSetup.GetOrAdd<VfxPlayer>(go);
            var so = new SerializedObject(vfx);
            so.FindProperty("_eventBus").objectReferenceValue = eventBus;
            so.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            so.FindProperty("_material").objectReferenceValue =
                GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.default2DMaterial : null;
            so.FindProperty("_towardCamera").floatValue = HeroSetup.EffectTowardCamera;
            so.FindProperty("_defeatClip").objectReferenceValue = Get(clips, "Death");
            so.ApplyModifiedProperties();

            WireAbilityVfx(player, vfx, clips);
            WirePlayerFx(player, vfx, clips);
            log.AppendLine($"  * vfx: {clips.Count} clips (enemy death, ability casts, dash puff, landing dust)");
            return vfx;
        }

        private static void WireAbilityVfx(PlayerController player, VfxPlayer vfx, Dictionary<string, SpriteClip> clips)
        {
            var caster = player.GetComponent<AbilityCaster>();
            if (caster == null)
            {
                return; // Setup HUD adds the caster; re-run this afterwards (Setup Everything does)
            }

            var so = new SerializedObject(caster);
            so.FindProperty("_vfx").objectReferenceValue = vfx;
            so.FindProperty("_ringVfx").objectReferenceValue = Get(clips, "Ring");
            so.FindProperty("_shieldVfx").objectReferenceValue = Get(clips, "Shield");
            so.FindProperty("_mendVfx").objectReferenceValue = Get(clips, "Heal");
            so.ApplyModifiedProperties();
        }

        private static void WirePlayerFx(PlayerController player, VfxPlayer vfx, Dictionary<string, SpriteClip> clips)
        {
            var fx = HeroSetup.GetOrAdd<PlayerFx>(player.gameObject);
            var so = new SerializedObject(fx);
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_vfx").objectReferenceValue = vfx;
            so.FindProperty("_dashClip").objectReferenceValue = Get(clips, "Puff");
            so.FindProperty("_landClip").objectReferenceValue = Get(clips, "Land");
            so.FindProperty("_doubleJumpClip").objectReferenceValue = Get(clips, "Puff");
            so.ApplyModifiedProperties();
        }

        /// <summary>Cuts an effect's grid sheet into frames (stopping at the manifest's frame count) and builds a clip.</summary>
        internal static SpriteClip BuildPvfxClip(string clipName, string effectId, float worldWidth, StringBuilder log)
        {
            string sheet = $"{PvfxRoot}/{effectId}/grid/sprite-sheet.png";
            string manifestPath = $"{PvfxRoot}/{effectId}/grid/manifest.json";
            if (!File.Exists(sheet) || !File.Exists(manifestPath))
            {
                log.AppendLine($"  ! missing PVFX effect {effectId}");
                return null;
            }

            var manifest = JsonUtility.FromJson<PvfxManifest>(File.ReadAllText(manifestPath));
            int count = manifest?.frames?.Length ?? 0;
            if (count == 0)
            {
                return null;
            }

            // Manifest pivots are in canvas pixels from the top-left; sprite pivots are normalised from the bottom-left.
            Vector2 pivot = manifest.pivot != null && manifest.pivot.Length == 2
                ? new Vector2(manifest.pivot[0] / (float)PvfxCell, 1f - manifest.pivot[1] / (float)PvfxCell)
                : new Vector2(0.5f, 0.5f);
            float ppu = PvfxCell / worldWidth;

            Color32[] all = LoadPixels(sheet, out int w, out int h);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var cell = new RectInt(i % PvfxColumns * PvfxCell, i / PvfxColumns * PvfxCell, PvfxCell, PvfxCell);
                string path = $"{VfxGenerated}/{effectId}/{effectId}_{i:00}.png";
                SavePng(path, Crop(all, w, h, cell), PvfxCell, PvfxCell);
                frames[i] = ImportSprite(path, ppu, pivot);
            }

            return SaveVfxClip(clipName, frames, PvfxFps);
        }

        private static SpriteClip BuildChurchDeathClip(StringBuilder log)
        {
            if (!Directory.Exists(ChurchDeathFrames))
            {
                log.AppendLine("  ! missing church enemy-death frames");
                return null;
            }

            string[] paths = Directory.GetFiles(ChurchDeathFrames, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => int.Parse(Regex.Match(Path.GetFileNameWithoutExtension(p), @"(\d+)$").Value))
                .ToArray();
            if (paths.Length == 0)
            {
                return null;
            }

            LoadPixels(paths[0], out int w, out _);
            Sprite[] frames = paths.Select(p => ImportSprite(p, w / DeathWidth, new Vector2(0.5f, 0.5f))).ToArray();
            return SaveVfxClip("Death", frames, 18f);
        }

        private static SpriteClip SaveVfxClip(string name, Sprite[] frames, float fps)
        {
            string path = $"{VfxClipDir}/Clip_Vfx_{name}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<SpriteClip>(path);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteClip>();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.EditorSet(frames, fps, false, false);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static SpriteClip Get(Dictionary<string, SpriteClip> clips, string name)
        {
            return clips.TryGetValue(name, out SpriteClip clip) ? clip : null;
        }
    }
}
