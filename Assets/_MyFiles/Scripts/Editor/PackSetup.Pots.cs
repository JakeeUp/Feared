using System.IO;
using System.Linq;
using System.Text;
using Brawler.Combat;
using Brawler.Player;
using Brawler.Presentation;
using Brawler.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>Pots half of Apply Asset Packs: cuts the pots and their shards out of the sheet and scatters breakable pots.</summary>
    public static partial class PackSetup
    {
        // ============ Paths ============

        private const string PotSheet = ThirdParty + "/breakable pots.png";
        private const string PotGenerated = GeneratedDir + "/Pots";
        private const string PotPrefabPath = Root + "/Prefabs/Pot.prefab";

        // ============ Sheet Layout (top-left image coordinates) ============

        /// <summary>Each colour (green, red, white, yellow) owns a 32 px column.</summary>
        private const int PotColumn = 32;

        /// <summary>Pot shapes within a colour column.</summary>
        private static readonly (string Name, RectInt Rect, int Hits)[] PotShapes =
        {
            ("small", new RectInt(3, 1, 11, 14), 1),
            ("jug", new RectInt(1, 16, 14, 15), 1),
            ("urn", new RectInt(16, 9, 16, 22), 2),
        };

        /// <summary>Shards: six 16 px cells per colour row, rows start at y 32.</summary>
        private const int ShardCell = 16;
        private const int ShardCount = 6;

        private static readonly string[] PotColours = { "green", "red", "white", "yellow" };

        /// <summary>Pots are drawn small; 30 px/unit makes the urn ~0.7 units next to the 1-unit hero.</summary>
        private const float PotPixelsPerUnit = 30f;

        /// <summary>Where pots stand: (x, z, colour index, shape index).</summary>
        private static readonly (float X, float Z, int Colour, int Shape)[] PotPlacements =
        {
            (-4.5f, 3.6f, 1, 2), (-3.7f, 4.2f, 1, 0), (-5.2f, 4.4f, 3, 1),
            (3.8f, 4f, 2, 2), (4.6f, 3.4f, 0, 1),
            (-2f, -4.4f, 3, 2), (-1.2f, -4.8f, 0, 0),
            (5.5f, -4.2f, 1, 1), (6.2f, -4.6f, 2, 0),
        };

        private const string PotRootName = "Pots";

        // ============ Build ============

        private static void BuildPots(PlayerController player, VfxPlayer vfx, Transform cameraTransform, StringBuilder log)
        {
            if (!File.Exists(PotSheet))
            {
                log.AppendLine($"  ! pots skipped: {PotSheet} not found");
                return;
            }

            Color32[] all = LoadPixels(PotSheet, out int w, out int h);
            var bodies = new Sprite[PotColours.Length, PotShapes.Length];
            var shards = new Sprite[PotColours.Length][];
            for (int c = 0; c < PotColours.Length; c++)
            {
                for (int s = 0; s < PotShapes.Length; s++)
                {
                    RectInt rect = PotShapes[s].Rect;
                    rect.x += c * PotColumn;
                    string path = $"{PotGenerated}/pot_{PotColours[c]}_{PotShapes[s].Name}.png";
                    SavePng(path, Crop(all, w, h, rect), rect.width, rect.height);
                    bodies[c, s] = ImportSprite(path, PotPixelsPerUnit, new Vector2(0.5f, 0f));
                }

                shards[c] = new Sprite[ShardCount];
                for (int i = 0; i < ShardCount; i++)
                {
                    var rect = new RectInt(i * ShardCell, PotColumn + c * ShardCell, ShardCell, ShardCell);
                    string path = $"{PotGenerated}/shard_{PotColours[c]}_{i}.png";
                    SavePng(path, Crop(all, w, h, rect), ShardCell, ShardCell);
                    shards[c][i] = ImportSprite(path, PotPixelsPerUnit, new Vector2(0.5f, 0.5f));
                }
            }

            var wallet = player.GetComponent<ScoreWallet>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            SpriteClip puff = AssetDatabase.LoadAssetAtPath<SpriteClip>($"{VfxClipDir}/Clip_Vfx_Puff.asset");
            int hurtboxLayer = player.GetComponentInChildren<Hurtbox>(true)?.gameObject.layer ?? 0;

            // The Cathedral places its own pots along the level (CathedralBuilder) instead of the arena set.
            (Vector3 Position, int Colour, int Shape)[] placements = CathedralBuilder.IsCathedralScene()
                ? CathedralBuilder.PotPlacements
                : PotPlacements.Select(p => (new Vector3(p.X, 0f, p.Z), p.Colour, p.Shape)).ToArray();

            GameObject old = GameObject.Find(PotRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            GameObject root = HeroSetup.CreateRoot(PotRootName);
            for (int i = 0; i < placements.Length; i++)
            {
                var place = placements[i];
                Sprite body = bodies[place.Colour, place.Shape];
                BuildPot(root.transform, i, place.Position, body, shards[place.Colour], PotShapes[place.Shape].Hits,
                         material, cameraTransform, hurtboxLayer, wallet, vfx, puff);
            }

            log.AppendLine($"  * {placements.Length} breakable pots (score when smashed, back after 20 s)");
        }

        private static void BuildPot(Transform root, int index, Vector3 position, Sprite body, Sprite[] shards, int hits,
                                     Material material, Transform cameraTransform, int hurtboxLayer,
                                     ScoreWallet wallet, VfxPlayer vfx, SpriteClip puff)
        {
            var go = new GameObject($"Pot_{index}");
            go.transform.SetParent(root, false);
            go.transform.position = position;

            Vector2 size = body.bounds.size;
            var solid = go.AddComponent<CapsuleCollider>();
            solid.radius = size.x * 0.4f;
            solid.height = Mathf.Max(size.y, solid.radius * 2f);
            solid.center = new Vector3(0f, solid.height * 0.5f, 0f);

            // Visual: billboarded like the characters.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.shadowCastingMode = ShadowCastingMode.On;
            if (material != null)
            {
                sr.sharedMaterial = material;
            }

            var billboard = visual.AddComponent<BillboardSprite>();
            var billboardSo = new SerializedObject(billboard);
            billboardSo.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            billboardSo.FindProperty("_spriteRenderer").objectReferenceValue = sr;
            billboardSo.ApplyModifiedProperties();

            var prop = go.AddComponent<BreakableProp>();

            // Hurtbox: neutral team, so the player and enemies can both smash it.
            var hurtGo = new GameObject("Hurtbox");
            hurtGo.layer = hurtboxLayer;
            hurtGo.transform.SetParent(go.transform, false);
            var trigger = hurtGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, size.y, 0.6f);
            trigger.center = new Vector3(0f, size.y * 0.5f, 0f);
            var hurtbox = hurtGo.AddComponent<Hurtbox>();
            var hurtSo = new SerializedObject(hurtbox);
            hurtSo.FindProperty("_collider").objectReferenceValue = trigger;
            hurtSo.FindProperty("_owner").objectReferenceValue = prop;
            hurtSo.FindProperty("_team").enumValueIndex = (int)CombatTeam.Neutral;
            hurtSo.ApplyModifiedProperties();

            var so = new SerializedObject(prop);
            so.FindProperty("_visual").objectReferenceValue = sr;
            HeroSetup.SetArray(so.FindProperty("_colliders"), solid, trigger);
            HeroSetup.SetArray(so.FindProperty("_shardSprites"), shards.Cast<Object>().ToArray());
            so.FindProperty("_shardMaterial").objectReferenceValue = material;
            so.FindProperty("_hitsToBreak").intValue = hits;
            so.FindProperty("_scoreReward").intValue = hits > 1 ? 50 : 25;
            so.FindProperty("_wallet").objectReferenceValue = wallet;
            so.FindProperty("_vfx").objectReferenceValue = vfx;
            so.FindProperty("_breakClip").objectReferenceValue = puff;
            so.ApplyModifiedProperties();
        }
    }
}
