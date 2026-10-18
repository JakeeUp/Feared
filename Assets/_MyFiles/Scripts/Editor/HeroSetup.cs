using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling: menu <c>Brawler/Setup Hero (Thomas Lean + SlashFX)</c>. Turns the third-party packs in
    /// <c>Assets/_ThirdParty</c> into a playable character in the open scene:
    /// imports frames as crisp pixel sprites anchored on the character's body, builds <see cref="SpriteClip"/> assets,
    /// measures each attack's hitbox from its swing art, scales collision/movement/level to the character, wires
    /// <see cref="PlayerAnimationDriver"/> / <see cref="HitFlash"/> / the Jump input, turns SlashFX into hit sparks,
    /// and adds platforms, climbable walls and a parallax backdrop.
    /// </summary>
    /// <remarks>
    /// Safe to re-run: import settings, clips and tuning are regenerated from the constants below (hand edits to the
    /// tuned fields are overwritten, so change them here), scene objects are reused by name. The third-party folder
    /// is git-ignored; the generated clips only reference it.
    /// </remarks>
    public static class HeroSetup
    {
        // ============ Paths ============

        private const string ThirdParty = "Assets/_ThirdParty";
        private const string HeroRoot = ThirdParty + "/AssetPack/Light";
        private const string EnvRoot = ThirdParty + "/AssetPack/Environment";
        private const string SlashSheetFormat = ThirdParty + "/SlashFX Combo{0} sheet.png";
        private const string SlashFramesDir = ThirdParty + "/SlashFX_Frames";

        private const string Root = "Assets/_MyFiles";
        private const string ClipDir = Root + "/Data/Animation/Hero";
        private const string EventBusPath = Root + "/Data/Core/EventBus.asset";
        private const string InputActionsPath = Root + "/Data/Input/BrawlerControls.inputactions";
        private const string SpriteMaterialPath = Root + "/Art/Materials/M_SpriteAlphaClip.mat";
        private const string PropMaterialPath = Root + "/Art/Materials/M_Prop.mat";
        private const string CameraSettingsPath = Root + "/Data/Presentation/CameraRigSettings.asset";
        private const string PostFxProfilePath = Root + "/Data/Presentation/PostFX_2_5D.asset";

        private static readonly string[] AttackPaths =
        {
            Root + "/Data/Player/Attack_Jab1.asset",
            Root + "/Data/Player/Attack_Jab2.asset",
            Root + "/Data/Player/Attack_Finisher.asset",
        };

        // ============ Scale (the one knob) ============

        /// <summary>Standing height of the character in world units. Sprite size, collision and tuning derive from it.</summary>
        private const float CharacterHeight = 1f;

        /// <summary>Camera distance that keeps a <see cref="CharacterHeight"/>-tall character readable.</summary>
        private const float CameraDistance = 11f;

        private const float CapsuleRadius = 0.22f;
        private static readonly Vector3 HurtboxSize = new Vector3(0.45f, 0.95f, 0.5f);
        private const float PlayerShadowScale = 0.5f;

        /// <summary>Placeholder dummies are 2 units tall; this brings them to the character's height.</summary>
        private const float DummyScale = CharacterHeight / 2f;

        /// <summary>Forward lunge per combo link (units/s).</summary>
        private static readonly float[] Lunges = { 1f, 1.3f, 2.5f };

        /// <summary>Movement tuned for a 1-unit character: (field, value). Jump apex ~1.4 units.</summary>
        private static readonly (string Field, float Value)[] MovementTuning =
        {
            ("_moveSpeed", 4f),
            ("_dashSpeed", 10f),
            ("_rollSpeed", 7.5f),
            ("_jumpVelocity", 9.2f),
            ("_doubleJumpVelocity", 7.5f), // second rise ~0.9 units
            ("_wallJumpVelocity", 8f),
            ("_wallJumpPush", 4.5f),
            ("_wallSlideSpeed", 1.8f),
        };

        // ============ Sprite Analysis ============

        /// <summary>Alpha above which a pixel counts as art.</summary>
        private const byte SolidAlpha = 128;

        /// <summary>Hero frame used as the size reference (its art height maps to <see cref="CharacterHeight"/>).</summary>
        private const string ReferenceFrame = HeroRoot + "/idle.png";

        /// <summary>Index of the swing frame in each attack clip (frame 0 is the wind-up).</summary>
        private const int SwingFrame = 1;

        /// <summary>Art closer than this (px) to the body centre is the body itself, not the swing.</summary>
        private const int SwingBodyMarginPx = 2;

        // ============ SlashFX (hit sparks) ============

        private const int SlashCell = 128;

        /// <summary>Width of a hit spark in units (about the character's height).</summary>
        private const float SparkWidth = 1f;

        // ============ Clip Table ============

        private struct ClipSpec
        {
            public string Name;   // asset name: Clip_Hero_<Name>
            public string Folder; // frame folder under HeroRoot
            public float Fps;
            public bool Loop;
            public bool Mirrored; // art faces the other way (wall grab faces the wall)
            public int First;     // first frame used from the folder
            public int Count;     // frames used (0 = all from First)

            public ClipSpec(string name, string folder, float fps, bool loop, bool mirrored = false, int first = 0, int count = 0)
            {
                Name = name; Folder = folder; Fps = fps; Loop = loop; Mirrored = mirrored; First = first; Count = count;
            }
        }

        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec("Run", "run", 14f, true),
            new ClipSpec("Dash", "dash", 16f, true),
            new ClipSpec("Roll", "roll", 20f, false),
            new ClipSpec("DoubleJump", "roll", 24f, false, first: 2, count: 7), // the roll's tucked spin as a somersault
            new ClipSpec("Jump", "jump", 12f, false),
            new ClipSpec("Fall", "fall", 10f, true),
            new ClipSpec("WallGrab", "wall_grab", 24f, false, mirrored: true),
            new ClipSpec("WallSlide", "wall_slide", 10f, true),
            new ClipSpec("Attack1", "attack1", 12f, false),
            new ClipSpec("Attack2", "attack2", 12f, false),
            new ClipSpec("Attack3", "attack3", 12f, false),
        };

        // ============ Level Layout ============

        private struct BoxSpec
        {
            public string Name;
            public Vector3 Center;
            public Vector3 Size;

            public BoxSpec(string name, Vector3 center, Vector3 size)
            {
                Name = name; Center = center; Size = size;
            }
        }

        /// <summary>
        /// Stepped platforms (left), a two-pillar wall-jump chimney with a ledge beyond it (right), tall end walls.
        /// Arena is 24 x 12. Heights fit the ~1.4-unit jump: Low is an easy hop, High needs Low as a step.
        /// </summary>
        private static readonly BoxSpec[] LevelBoxes =
        {
            new BoxSpec("Platform_Low", new Vector3(-7.5f, 0.3f, -0.5f), new Vector3(3f, 0.6f, 4f)),
            new BoxSpec("Platform_High", new Vector3(-10.5f, 0.6f, -0.5f), new Vector3(3f, 1.2f, 4f)),
            new BoxSpec("Pillar_Chimney_A", new Vector3(7.4f, 1.75f, -1f), new Vector3(0.8f, 3.5f, 2.5f)),
            new BoxSpec("Pillar_Chimney_B", new Vector3(9.2f, 1.75f, -1f), new Vector3(0.8f, 3.5f, 2.5f)),
            new BoxSpec("Ledge_High", new Vector3(10.6f, 3.3f, -1f), new Vector3(2f, 0.4f, 2.5f)),
            new BoxSpec("Wall_End_Left", new Vector3(-12.5f, 3.5f, 0f), new Vector3(1f, 7f, 12f)),
            new BoxSpec("Wall_End_Right", new Vector3(12.5f, 3.5f, 0f), new Vector3(1f, 7f, 12f)),
        };

        /// <summary>Backdrop layers far -&gt; near: file, depth (z), height in units, tiled with mirrored side copies.</summary>
        private static readonly (string File, float Z, float Height, bool Tile, bool AlphaClip)[] Backdrop =
        {
            ("Background", 26f, 22f, true, true),
            ("Background_Fog", 22f, 18f, true, false),
            ("Background_Arches", 17f, 14f, true, true),
            ("Background_Pillars", 12f, 12f, true, true),
            ("Background_Wall", 8f, 9f, false, true),
        };

        // ============ Menu ============

        [MenuItem("Brawler/Setup Hero (Thomas Lean + SlashFX)")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(HeroRoot))
            {
                EditorUtility.DisplayDialog("Setup Hero", $"Couldn't find {HeroRoot}. Unzip the Thomas Lean pack into Assets/_ThirdParty/ first.", "OK");
                return;
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Setup Hero", "No PlayerController in the open scene. Open TestArena first.", "OK");
                return;
            }

            var log = new StringBuilder("[Brawler] Setup Hero\n");
            float ppu = ArtHeight(ReferenceFrame) / CharacterHeight;
            var pivots = new Dictionary<string, Vector2Int>();
            try
            {
                AssetDatabase.StartAssetEditing();
                ImportHeroFrames(ppu, pivots, log);
                ImportEnvironment(log);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            List<string> sparkFrames = SliceSlashSheets(log);
            Dictionary<string, SpriteClip> clips = BuildClips(sparkFrames, log);

            ApplyCharacterScale(player, log);
            TuneAttacks(ppu, pivots, log);
            WirePlayer(player, clips, log);
            WireDummyFlashes(log);
            WireHitSparks(player, clips, log);
            WireJumpInput(player, log);
            ApplyCamera(log);
            BuildLevel(log);
            BuildBackdrop(log);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ============ Import ============

        /// <summary>
        /// Every frame of a clip shares one anchor: the median body centre across the clip (so the artist's in-clip
        /// motion is kept) and the lowest non-trail pixel (feet). Body-based anchoring keeps clips consistent with each
        /// other even though the frames are different sizes and the attacks carry huge sword trails.
        /// </summary>
        private static void ImportHeroFrames(float ppu, Dictionary<string, Vector2Int> pivots, StringBuilder log)
        {
            int count = 0;
            var specs = Clips.Select(c => c.Folder).Distinct().Concat(new[] { "idle_blink" });
            foreach (string folder in specs)
            {
                List<string> frames = FramePaths(folder);
                if (frames.Count == 0)
                {
                    log.AppendLine($"  ! no frames in {HeroRoot}/{folder}");
                    continue;
                }

                Vector2Int pivot = ClipPivot(frames);
                pivots[folder] = pivot;
                foreach (string path in frames)
                {
                    ConfigurePixelSprite(path, pivot, ppu);
                    count++;
                }
            }

            // The still idle frame shares idle_blink's anchor so the blink never shifts the sprite.
            Vector2Int idlePivot = pivots.TryGetValue("idle_blink", out Vector2Int p) ? p : ClipPivot(new List<string> { ReferenceFrame });
            ConfigurePixelSprite(ReferenceFrame, idlePivot, ppu);

            log.AppendLine($"  * {count + 1} hero frames: point filter, {ppu:0.#} px/unit ({CharacterHeight} unit tall), anchored on the body");
        }

        private static void ImportEnvironment(StringBuilder log)
        {
            foreach (var layer in Backdrop)
            {
                string path = $"{EnvRoot}/{layer.File}.png";
                if (!File.Exists(path))
                {
                    continue;
                }

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                LoadPixels(path, out _, out int h);
                ApplySprite(importer, new Vector2(0.5f, 0f), h / layer.Height, FilterMode.Point);
            }

            log.AppendLine("  * environment layers imported");
        }

        /// <summary>Slices the SlashFX sheets into frames anchored on the centre of their art, sized to <see cref="SparkWidth"/>.</summary>
        private static List<string> SliceSlashSheets(StringBuilder log)
        {
            var result = new List<string>();
            Directory.CreateDirectory(SlashFramesDir);
            for (int combo = 1; combo <= 3; combo++)
            {
                string sheet = string.Format(SlashSheetFormat, combo);
                if (!File.Exists(sheet))
                {
                    log.AppendLine($"  ! missing {sheet}");
                    continue;
                }

                Color32[] all = LoadPixels(sheet, out int sheetW, out int sheetH);
                int cellH = Mathf.Min(SlashCell, sheetH);
                var comboFrames = new List<string>();
                RectInt union = default;
                bool hasUnion = false;
                for (int i = 0; i < sheetW / SlashCell; i++)
                {
                    Color32[] pixels = GetBlock(all, sheetW, i * SlashCell, 0, SlashCell, cellH);
                    if (!TryGetBounds(pixels, SlashCell, cellH, out RectInt bounds))
                    {
                        continue; // trailing empty cells
                    }

                    union = hasUnion ? Encapsulate(union, bounds) : bounds;
                    hasUnion = true;

                    var frame = new Texture2D(SlashCell, cellH, TextureFormat.RGBA32, false);
                    frame.SetPixels32(pixels);
                    string path = $"{SlashFramesDir}/Slash{combo}_{comboFrames.Count}.png";
                    File.WriteAllBytes(path, frame.EncodeToPNG());
                    Object.DestroyImmediate(frame);
                    comboFrames.Add(path);
                }

                if (!hasUnion)
                {
                    continue;
                }

                AssetDatabase.Refresh();
                var pivot = new Vector2(union.center.x / SlashCell, union.center.y / cellH);
                float ppu = union.width / SparkWidth;
                foreach (string path in comboFrames)
                {
                    ApplySprite((TextureImporter)AssetImporter.GetAtPath(path), pivot, ppu, FilterMode.Bilinear);
                }

                result.AddRange(comboFrames);
            }

            log.AppendLine($"  * sliced {result.Count} SlashFX frames into hit sparks ({SparkWidth} unit wide)");
            return result;
        }

        // ============ Clips ============

        private static Dictionary<string, SpriteClip> BuildClips(List<string> sparkFrames, StringBuilder log)
        {
            Directory.CreateDirectory(ClipDir);
            var clips = new Dictionary<string, SpriteClip>();

            foreach (ClipSpec spec in Clips)
            {
                IEnumerable<string> paths = FramePaths(spec.Folder).Skip(spec.First);
                if (spec.Count > 0)
                {
                    paths = paths.Take(spec.Count);
                }

                Sprite[] frames = paths.Select(LoadSprite).Where(s => s != null).ToArray();
                clips[spec.Name] = SaveClip(spec.Name, frames, spec.Fps, spec.Loop, spec.Mirrored);
            }

            // Idle: hold the still frame, then blink (~every 3 s at 8 fps).
            Sprite idle = LoadSprite(ReferenceFrame);
            var idleFrames = Enumerable.Repeat(idle, 24).Concat(FramePaths("idle_blink").Select(LoadSprite)).Where(s => s != null).ToArray();
            clips["Idle"] = SaveClip("Idle", idleFrames, 8f, true, false);

            for (int combo = 1; combo <= 3; combo++)
            {
                string prefix = $"{SlashFramesDir}/Slash{combo}_";
                Sprite[] frames = sparkFrames.Where(p => p.StartsWith(prefix))
                    .OrderBy(FrameNumber).Select(LoadSprite).Where(s => s != null).ToArray();
                clips[$"Spark{combo}"] = SaveClip($"Spark{combo}", frames, 24f, false, false);
            }

            log.AppendLine($"  * {clips.Count} clips in {ClipDir}");
            return clips;
        }

        private static SpriteClip SaveClip(string name, Sprite[] frames, float fps, bool loop, bool mirrored)
        {
            string path = $"{ClipDir}/Clip_Hero_{name}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<SpriteClip>(path);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteClip>();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.EditorSet(frames, fps, loop, mirrored);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ============ Tuning ============

        /// <summary>Shrinks the player's collision/hurtbox/shadow and the dummies, and retunes movement for the size.</summary>
        private static void ApplyCharacterScale(PlayerController player, StringBuilder log)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                Undo.RecordObject(cc, "Scale Player Capsule");
                cc.height = CharacterHeight;
                cc.radius = CapsuleRadius;
                cc.center = new Vector3(0f, CharacterHeight * 0.5f, 0f); // pivot at the feet
                cc.stepOffset = Mathf.Min(cc.stepOffset, CharacterHeight * 0.25f);
            }

            Hurtbox hurtbox = player.GetComponentInChildren<Hurtbox>(true);
            if (hurtbox != null && hurtbox.TryGetComponent(out BoxCollider box))
            {
                Undo.RecordObject(box, "Scale Player Hurtbox");
                box.size = HurtboxSize;
                box.center = new Vector3(0f, CharacterHeight * 0.5f, 0f) - hurtbox.transform.localPosition;
            }

            var shadow = player.GetComponent<BlobShadow>();
            if (shadow != null)
            {
                var shadowSo = new SerializedObject(shadow);
                shadowSo.FindProperty("_baseScale").floatValue = PlayerShadowScale;
                shadowSo.ApplyModifiedProperties();
            }

            foreach (TrainingDummy dummy in Object.FindObjectsByType<TrainingDummy>())
            {
                Undo.RecordObject(dummy.transform, "Scale Dummy");
                dummy.transform.localScale = Vector3.one * DummyScale;
            }

            var movement = new SerializedObject(player).FindProperty("_movementSettings").objectReferenceValue as PlayerMovementSettings;
            if (movement != null)
            {
                var movementSo = new SerializedObject(movement);
                foreach (var tuning in MovementTuning)
                {
                    movementSo.FindProperty(tuning.Field).floatValue = tuning.Value;
                }

                movementSo.ApplyModifiedProperties();
            }

            log.AppendLine($"  * scale: capsule {CharacterHeight} tall, dummies x{DummyScale}, movement retuned");
        }

        /// <summary>
        /// Hitbox of each combo link = the swing frame's art in front of the body (where the blade actually reaches),
        /// so what you see is what hits.
        /// </summary>
        private static void TuneAttacks(float ppu, Dictionary<string, Vector2Int> pivots, StringBuilder log)
        {
            for (int i = 0; i < AttackPaths.Length; i++)
            {
                string folder = $"attack{i + 1}";
                var attack = AssetDatabase.LoadAssetAtPath<AttackData>(AttackPaths[i]);
                List<string> frames = FramePaths(folder);
                if (attack == null || frames.Count <= SwingFrame || !pivots.TryGetValue(folder, out Vector2Int pivot))
                {
                    continue;
                }

                Color32[] pixels = LoadPixels(frames[SwingFrame], out int w, out int h);
                int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    for (int x = pivot.x + SwingBodyMarginPx; x < w; x++)
                    {
                        if (pixels[y * w + x].a < SolidAlpha)
                        {
                            continue;
                        }

                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                if (maxX < 0)
                {
                    continue;
                }

                // From the body centre to the blade tip; pixel rows are bottom-up, pivot.y is the feet row.
                float x0 = pivot.x, x1 = maxX + 1, y0 = minY, y1 = maxY + 1;
                var size = new Vector2((x1 - x0) / ppu, (y1 - y0) / ppu);
                var offset = new Vector2(((x0 + x1) * 0.5f - pivot.x) / ppu, ((y0 + y1) * 0.5f - pivot.y) / ppu);

                var so = new SerializedObject(attack);
                so.FindProperty("_hitboxSize").vector2Value = size;
                so.FindProperty("_hitboxOffset").vector2Value = offset;
                so.FindProperty("_lungeSpeed").floatValue = Lunges[Mathf.Min(i, Lunges.Length - 1)];
                so.ApplyModifiedProperties();
                log.AppendLine($"  * {attack.name}: hitbox {size.x:0.00} x {size.y:0.00} at ({offset.x:0.00}, {offset.y:0.00}) from the swing art");
            }
        }

        private static void ApplyCamera(StringBuilder log)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(CameraSettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("_distance").floatValue = CameraDistance;
                so.ApplyModifiedProperties();
                Object.FindAnyObjectByType<CameraRig>()?.ApplySettings();
            }

            // Keep the depth-of-field focus on the player at the new distance.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxProfilePath);
            if (profile != null && profile.TryGet(out DepthOfField dof))
            {
                dof.focusDistance.Override(CameraDistance);
                EditorUtility.SetDirty(profile);
            }

            log.AppendLine($"  * camera distance {CameraDistance} (DoF refocused)");
        }

        // ============ Scene ============

        private static void WirePlayer(PlayerController player, Dictionary<string, SpriteClip> clips, StringBuilder log)
        {
            BillboardSprite billboard = player.GetComponentsInChildren<BillboardSprite>(true)
                .FirstOrDefault(b => b.GetComponent<SpriteRenderer>() != null && b.transform.parent == player.transform);
            if (billboard == null)
            {
                log.AppendLine("  ! player has no Visual (BillboardSprite + SpriteRenderer) child; skipped wiring");
                return;
            }

            GameObject visual = billboard.gameObject;
            var renderer = visual.GetComponent<SpriteRenderer>();
            Undo.RecordObject(renderer, "Hero Sprite");
            renderer.sprite = clips["Idle"].GetFrame(0);
            renderer.color = Color.white;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            Undo.RecordObject(visual.transform, "Hero Scale");
            visual.transform.localScale = Vector3.one;
            visual.transform.localPosition = Vector3.zero;

            SpriteAnimator animator = GetOrAdd<SpriteAnimator>(visual);

            // The old on-character slash is replaced by hit sparks.
            Transform oldSlash = player.transform.Find("SlashFX");
            if (oldSlash != null)
            {
                Undo.DestroyObjectImmediate(oldSlash.gameObject);
            }

            var driver = GetOrAdd<PlayerAnimationDriver>(visual);
            var so = new SerializedObject(driver);
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_animator").objectReferenceValue = animator;
            so.FindProperty("_billboard").objectReferenceValue = billboard;
            so.FindProperty("_idle").objectReferenceValue = clips["Idle"];
            so.FindProperty("_run").objectReferenceValue = clips["Run"];
            so.FindProperty("_dash").objectReferenceValue = clips["Dash"];
            so.FindProperty("_roll").objectReferenceValue = clips["Roll"];
            so.FindProperty("_jump").objectReferenceValue = clips["Jump"];
            so.FindProperty("_doubleJump").objectReferenceValue = clips["DoubleJump"];
            so.FindProperty("_fall").objectReferenceValue = clips["Fall"];
            so.FindProperty("_wallGrab").objectReferenceValue = clips["WallGrab"];
            so.FindProperty("_wallSlide").objectReferenceValue = clips["WallSlide"];
            SetArray(so.FindProperty("_attacks"), clips["Attack1"], clips["Attack2"], clips["Attack3"]);
            so.ApplyModifiedProperties();

            AddFlash(visual, player.transform);
            log.AppendLine("  * player: hero sprite, animation driver, hit flash");
        }

        private static void WireHitSparks(PlayerController player, Dictionary<string, SpriteClip> clips, StringBuilder log)
        {
            GameObject go = GameObject.Find("HitSparks") ?? CreateRoot("HitSparks");
            var spawner = GetOrAdd<HitSparkSpawner>(go);

            BillboardSprite billboard = player.GetComponentInChildren<BillboardSprite>(true);
            Transform cameraTransform = billboard != null
                ? new SerializedObject(billboard).FindProperty("_cameraTransform").objectReferenceValue as Transform
                : null;

            var so = new SerializedObject(spawner);
            so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
            so.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            so.FindProperty("_material").objectReferenceValue =
                GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.default2DMaterial : null;
            SetArray(so.FindProperty("_clips"), clips["Spark1"], clips["Spark2"], clips["Spark3"]);
            so.ApplyModifiedProperties();
            log.AppendLine("  * SlashFX now plays as hit sparks at the contact point");
        }

        private static void WireDummyFlashes(StringBuilder log)
        {
            int count = 0;
            foreach (TrainingDummy dummy in Object.FindObjectsByType<TrainingDummy>())
            {
                SpriteRenderer sr = dummy.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();
                if (sr != null)
                {
                    AddFlash(sr.gameObject, dummy.transform);
                    count++;
                }
            }

            log.AppendLine($"  * hit flash on {count} dummies");
        }

        private static void AddFlash(GameObject target, Transform owner)
        {
            var flash = GetOrAdd<HitFlash>(target);
            var so = new SerializedObject(flash);
            so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
            so.FindProperty("_owner").objectReferenceValue = owner;
            so.FindProperty("_renderer").objectReferenceValue = target.GetComponent<SpriteRenderer>();
            so.ApplyModifiedProperties();
        }

        private static void WireJumpInput(PlayerController player, StringBuilder log)
        {
            InputActionReference jump = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.name == "Jump");
            var reader = player.GetComponent<PlayerInputReader>();
            if (jump == null || reader == null)
            {
                log.AppendLine("  ! couldn't find the Jump action reference (reimport BrawlerControls.inputactions)");
                return;
            }

            var so = new SerializedObject(reader);
            so.FindProperty("_jumpAction").objectReferenceValue = jump;
            so.ApplyModifiedProperties();
            log.AppendLine("  * Jump input wired (Space / gamepad South); Dash/Roll is Shift / K / gamepad East");
        }

        private static void BuildLevel(StringBuilder log)
        {
            GameObject root = GameObject.Find("Level_Vertical") ?? CreateRoot("Level_Vertical");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);
            foreach (BoxSpec box in LevelBoxes)
            {
                Transform existing = root.transform.Find(box.Name);
                GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (existing == null)
                {
                    Undo.RegisterCreatedObjectUndo(go, "Create Level Box");
                    go.name = box.Name;
                    go.transform.SetParent(root.transform, false);
                }

                go.transform.position = box.Center;
                go.transform.localScale = box.Size;
                go.isStatic = true;
                if (material != null)
                {
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
            }

            // Boxes from earlier layouts that are no longer in the table.
            foreach (Transform child in root.transform.Cast<Transform>().ToList())
            {
                if (LevelBoxes.All(b => b.Name != child.name))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            log.AppendLine($"  * level: {LevelBoxes.Length} platforms / walls (two heights, wall-jump chimney, end walls)");
        }

        private static void BuildBackdrop(StringBuilder log)
        {
            GameObject root = GameObject.Find("Backdrop") ?? CreateRoot("Backdrop");
            var alphaClip = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            Material soft = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.default2DMaterial : null;
            int placed = 0;

            foreach (var layer in Backdrop)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{EnvRoot}/{layer.File}.png");
                if (sprite == null)
                {
                    continue;
                }

                float width = sprite.bounds.size.x;
                int copies = layer.Tile ? 3 : 1;
                for (int i = 0; i < copies; i++)
                {
                    int offset = layer.Tile ? i - 1 : 0;
                    string name = $"{layer.File}_{i}";
                    Transform existing = root.transform.Find(name);
                    GameObject go = existing != null ? existing.gameObject : new GameObject(name);
                    if (existing == null)
                    {
                        Undo.RegisterCreatedObjectUndo(go, "Create Backdrop Layer");
                        go.transform.SetParent(root.transform, false);
                    }

                    var sr = GetOrAdd<SpriteRenderer>(go);
                    sr.sprite = sprite;
                    sr.flipX = offset != 0; // mirrored neighbours hide the seams
                    sr.shadowCastingMode = ShadowCastingMode.Off;
                    sr.receiveShadows = false;
                    Material material = layer.AlphaClip ? alphaClip : soft;
                    if (material != null)
                    {
                        sr.sharedMaterial = material;
                    }

                    // Layers face the camera's yaw (0) and stand on the floor; real depth gives real parallax.
                    go.transform.position = new Vector3(offset * width, 0f, layer.Z);
                    go.transform.rotation = Quaternion.identity;
                    placed++;
                }
            }

            log.AppendLine($"  * backdrop: {placed} parallax sprites from z 8 to 26");
        }

        // ============ Sprite Analysis ============

        /// <summary>Near-white sword-trail pixels; ignored when finding the feet so trails never move the anchor.</summary>
        private static bool IsTrail(Color32 c)
        {
            return c.r > 225 && c.g > 215 && c.b > 190;
        }

        /// <summary>The character's dark clothing; its centroid is a stable body centre in every pose.</summary>
        private static bool IsBody(Color32 c)
        {
            return c.r < 45 && c.g < 45 && c.b < 45;
        }

        /// <summary>Median body-centre X across the frames, and the lowest non-trail row (feet). Pixels, bottom-up.</summary>
        private static Vector2Int ClipPivot(List<string> frames)
        {
            var centres = new List<float>();
            int feet = int.MaxValue;
            foreach (string path in frames)
            {
                Color32[] pixels = LoadPixels(path, out int w, out int h);
                float sum = 0f;
                int n = 0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = pixels[y * w + x];
                        if (c.a < SolidAlpha)
                        {
                            continue;
                        }

                        if (IsBody(c))
                        {
                            sum += x + 0.5f;
                            n++;
                        }

                        if (!IsTrail(c))
                        {
                            feet = Mathf.Min(feet, y);
                        }
                    }
                }

                centres.Add(n > 0 ? sum / n : w * 0.5f);
            }

            centres.Sort();
            float median = centres[centres.Count / 2];
            return new Vector2Int(Mathf.RoundToInt(median), feet == int.MaxValue ? 0 : feet);
        }

        /// <summary>Height in pixels of the solid art in a frame.</summary>
        private static int ArtHeight(string path)
        {
            Color32[] pixels = LoadPixels(path, out int w, out int h);
            return TryGetBounds(pixels, w, h, out RectInt b) ? b.height : h;
        }

        // ============ Helpers ============

        private static Color32[] LoadPixels(string path, out int width, out int height)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            Color32[] pixels = tex.GetPixels32();
            width = tex.width;
            height = tex.height;
            Object.DestroyImmediate(tex);
            return pixels;
        }

        private static List<string> FramePaths(string folder)
        {
            string dir = $"{HeroRoot}/{folder}";
            if (!Directory.Exists(dir))
            {
                return new List<string>();
            }

            return Directory.GetFiles(dir, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(FrameNumber)
                .ToList();
        }

        private static int FrameNumber(string path)
        {
            Match m = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        private static void ConfigurePixelSprite(string path, Vector2Int pivotPx, float ppu)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return;
            }

            LoadPixels(path, out int w, out int h);
            ApplySprite(importer, new Vector2(pivotPx.x / (float)w, pivotPx.y / (float)h), ppu, FilterMode.Point);
        }

        private static void ApplySprite(TextureImporter importer, Vector2 pivot, float ppu, FilterMode filter)
        {
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = filter;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>Bounding box of non-transparent pixels (bottom-left origin). False if the block is empty.</summary>
        private static bool TryGetBounds(Color32[] pixels, int w, int h, out RectInt bounds)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a < SolidAlpha)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            bounds = maxX < 0 ? default : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return maxX >= 0;
        }

        private static RectInt Encapsulate(RectInt a, RectInt b)
        {
            int xMin = Mathf.Min(a.xMin, b.xMin), yMin = Mathf.Min(a.yMin, b.yMin);
            int xMax = Mathf.Max(a.xMax, b.xMax), yMax = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Color32[] GetBlock(Color32[] all, int sheetWidth, int x, int y, int w, int h)
        {
            var block = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                System.Array.Copy(all, (y + row) * sheetWidth + x, block, row * w, w);
            }

            return block;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static GameObject CreateRoot(string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }
    }
}
