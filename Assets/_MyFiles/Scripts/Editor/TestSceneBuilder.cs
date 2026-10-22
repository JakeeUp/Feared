using System;
using System.IO;
using System.Text;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling (plumbing, fully implemented): menu <c>Brawler/Build Test Scene</c>.
    /// Creates — only if missing — placeholder sprites, materials, every data asset from ARCHITECTURE.md §7.3
    /// (starter values §7.1/§7.4), the damage-number prefab, and <c>Scenes/TestArena.unity</c> (§7.5) with all
    /// Inspector references wired through <see cref="SerializedObject"/> using the FINAL §7 field names.
    /// </summary>
    /// <remarks>
    /// Idempotent: existing assets are never overwritten (so hand-tuned values survive). If the scene already exists,
    /// a dialog asks whether to rebuild it; data assets are reused either way. Components are added by TYPE.
    /// </remarks>
    public static class TestSceneBuilder
    {
        // ============ Menu ============

        private const string MenuPath = "Brawler/Build Test Scene";

        // ============ Asset Paths (ARCHITECTURE.md §7.3) ============

        private const string Root = "Assets/_MyFiles";

        private const string ShaderSpriteAlphaClipPath = Root + "/Art/Shaders/SpriteAlphaClip.shader";
        private const string ShaderBlobShadowPath = Root + "/Art/Shaders/BlobShadow.shader";
        private const string UrpLitShaderGuid = "933532a4fcc9baf4fa0491de14d08ed7";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit"; // fallback only (editor tooling)

        private const string MatSpritePath = Root + "/Art/Materials/M_SpriteAlphaClip.mat";
        private const string MatBlobShadowPath = Root + "/Art/Materials/M_BlobShadow.mat";
        private const string MatGroundPath = Root + "/Art/Materials/M_Ground.mat";
        private const string MatPropPath = Root + "/Art/Materials/M_Prop.mat";

        private const string SpritePlayerPath = Root + "/Art/Sprites/T_Player_Placeholder.png";
        private const string SpriteDummyPath = Root + "/Art/Sprites/T_Dummy_Placeholder.png";

        private const string PrefabDamageNumberPath = Root + "/Art/Prefabs/P_DamageNumber.prefab";

        private const string AssetEventBusPath = Root + "/Data/Core/EventBus.asset";
        private const string AssetInputBufferPath = Root + "/Data/Core/InputBufferSettings.asset";
        private const string AssetCombatSettingsPath = Root + "/Data/Combat/CombatSettings.asset";
        private const string AssetDummySettingsPath = Root + "/Data/Combat/TrainingDummySettings.asset";
        private const string AssetMovementPath = Root + "/Data/Player/PlayerMovementSettings.asset";
        private const string AssetJab1Path = Root + "/Data/Player/Attack_Jab1.asset";
        private const string AssetJab2Path = Root + "/Data/Player/Attack_Jab2.asset";
        private const string AssetFinisherPath = Root + "/Data/Player/Attack_Finisher.asset";
        private const string AssetComboPath = Root + "/Data/Player/Combo_Basic.asset";
        private const string AssetFeedbackPath = Root + "/Data/Presentation/FeedbackSettings.asset";
        private const string AssetCameraRigPath = Root + "/Data/Presentation/CameraRigSettings.asset";

        private const string InputActionsPath = Root + "/Data/Input/BrawlerControls.inputactions";
        private const string InputMapName = "Gameplay";
        private const string InputMoveName = "Move";
        private const string InputAttackName = "Attack";
        private const string InputDashName = "Dash";

        private const string ScenePath = Root + "/Scenes/TestArena.unity";

        private static readonly string[] RequiredFolders =
        {
            Root + "/Art/Shaders", Root + "/Art/Materials", Root + "/Art/Sprites", Root + "/Art/Prefabs",
            Root + "/Data/Core", Root + "/Data/Combat", Root + "/Data/Player", Root + "/Data/Presentation",
            Root + "/Data/Input", Root + "/Scenes",
        };

        // ============ Serialized Property Names (ARCHITECTURE.md §7 — FINAL) ============

        private static class InputBufferSettingsProps
        {
            public const string BufferWindowSeconds = "_bufferWindowSeconds";
            public const string Capacity = "_capacity";
        }

        private static class ComboDataProps
        {
            public const string Attacks = "_attacks";
        }

        private static class CombatSettingsProps
        {
            public const string HurtboxLayers = "_hurtboxLayers";
            public const string MaxOverlapResults = "_maxOverlapResults";
            public const string DepthTolerance = "_depthTolerance";
        }

        private static class TrainingDummySettingsProps
        {
            public const string MaxHealth = "_maxHealth";
            public const string ResetDelaySeconds = "_resetDelaySeconds";
            public const string KnockbackScale = "_knockbackScale";
            public const string KnockbackDeceleration = "_knockbackDeceleration";
            public const string Gravity = "_gravity";
        }

        private static class MovementSettingsProps
        {
            public const string MoveSpeed = "_moveSpeed";
            public const string DepthSpeedMultiplier = "_depthSpeedMultiplier";
            public const string Acceleration = "_acceleration";
            public const string Deceleration = "_deceleration";
            public const string InputDeadzone = "_inputDeadzone";
            public const string Gravity = "_gravity";
            public const string DashSpeed = "_dashSpeed";
            public const string DashFrames = "_dashFrames";
            public const string DashIFrameStart = "_dashIFrameStart";
            public const string DashIFrameEnd = "_dashIFrameEnd";
            public const string KnockbackDeceleration = "_knockbackDeceleration";
        }

        private static class FeedbackSettingsProps
        {
            public const string HitStopTimeScale = "_hitStopTimeScale";
            public const string MaxHitStopSeconds = "_maxHitStopSeconds";
            public const string ShakeMaxOffset = "_shakeMaxOffset";
            public const string ShakeFrequency = "_shakeFrequency";
            public const string TraumaDecayPerSecond = "_traumaDecayPerSecond";
            public const string DamageNumberLifetime = "_damageNumberLifetime";
            public const string DamageNumberRise = "_damageNumberRise";
            public const string DamageNumberSpawnOffset = "_damageNumberSpawnOffset";
            public const string DamageNumberSpread = "_damageNumberSpread";
            public const string DamageNumberPoolCapacity = "_damageNumberPoolCapacity";
            public const string DamageNumberPoolMaxSize = "_damageNumberPoolMaxSize";
        }

        private static class CameraRigSettingsProps
        {
            public const string Pitch = "_pitch";
            public const string Yaw = "_yaw";
            public const string Distance = "_distance";
            public const string FieldOfView = "_fieldOfView";
            public const string TargetOffset = "_targetOffset";
            public const string FollowSmoothTime = "_followSmoothTime";
        }

        private static class HitboxProps
        {
            public const string CombatSettings = "_combatSettings";
            public const string EventBus = "_eventBus";
            public const string Owner = "_owner";
            public const string OwnerHurtbox = "_ownerHurtbox";
            public const string DrawGizmos = "_drawGizmos";
        }

        private static class HurtboxProps
        {
            public const string Collider = "_collider";
            public const string Owner = "_owner";
        }

        private static class TrainingDummyProps
        {
            public const string Settings = "_settings";
            public const string CharacterController = "_characterController";
        }

        private static class PlayerInputReaderProps
        {
            public const string MoveAction = "_moveAction";
            public const string AttackAction = "_attackAction";
            public const string DashAction = "_dashAction";
        }

        private static class PlayerControllerProps
        {
            public const string CharacterController = "_characterController";
            public const string InputReader = "_inputReader";
            public const string MovementSettings = "_movementSettings";
            public const string InputBufferSettings = "_inputBufferSettings";
            public const string ComboData = "_comboData";
            public const string Hitbox = "_hitbox";
        }

        private static class BillboardSpriteProps
        {
            public const string CameraTransform = "_cameraTransform";
            public const string SpriteRenderer = "_spriteRenderer";
            public const string FacingSource = "_facingSource";
            public const string MatchCameraPitch = "_matchCameraPitch";
        }

        private static class BlobShadowProps
        {
            public const string ShadowTransform = "_shadowTransform";
            public const string BaseScale = "_baseScale";
        }

        private static class CameraRigProps
        {
            public const string Target = "_target";
            public const string Settings = "_settings";
            public const string Pivot = "_pivot";
            public const string Camera = "_camera";
        }

        private static class FeedbackListenerProps
        {
            public const string EventBus = "_eventBus";
            public const string Settings = "_settings";
            public const string Prefab = "_prefab";                 // DamageNumberSpawner
            public const string CameraTransform = "_cameraTransform"; // DamageNumberSpawner
        }

        private static class DamageNumberProps
        {
            public const string Text = "_text";
        }

        // ============ Shader Property Names ============

        private const string ShaderPropColor = "_Color";
        private const string ShaderPropCutoff = "_Cutoff";
        private const string ShaderPropBaseColor = "_BaseColor"; // URP Lit
        private const string ShaderPropSmoothness = "_Smoothness"; // URP Lit

        // ============ Layout / Look ============

        private const int SpritePixelsPerUnit = 32;
        private const int SpriteWidth = 32;
        private const int SpriteHeight = 64;

        private static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 DummyAPosition = new Vector3(3f, 0f, 0f);
        private static readonly Vector3 DummyBPosition = new Vector3(5f, 0f, 1.5f);
        private static readonly Vector3 ArenaSize = new Vector3(24f, 1f, 12f);

        private const float ActorHeight = 2f;
        private const float ActorRadius = 0.35f;
        private static readonly Vector3 HurtboxCenter = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 HurtboxSize = new Vector3(0.8f, 1.8f, 0.5f);

        private static readonly Color PlayerBodyColor = new Color32(70, 130, 220, 255);
        private static readonly Color PlayerAccentColor = new Color32(250, 210, 80, 255);
        private static readonly Color DummyBodyColor = new Color32(200, 150, 90, 255);
        private static readonly Color DummyAccentColor = new Color32(210, 50, 50, 255);
        private static readonly Color GroundColor = new Color32(96, 120, 90, 255);
        private static readonly Color PropColor = new Color32(150, 110, 80, 255);
        private static readonly Color BackgroundColor = new Color32(120, 150, 185, 255);
        private static readonly Color DamageNumberColor = new Color32(255, 235, 90, 255);

        // ============ Entry Point ============

        /// <summary>Builds (or completes) the Day One test setup. Safe to run repeatedly.</summary>
        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("[Brawler] Build Test Scene summary:");

            try
            {
                AssetDatabase.StartAssetEditing();
                EnsureFolders(log);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // --- Art ---
            Sprite playerSprite = EnsureSprite(SpritePlayerPath, PlayerBodyColor, PlayerAccentColor, false, log);
            Sprite dummySprite = EnsureSprite(SpriteDummyPath, DummyBodyColor, DummyAccentColor, true, log);

            Material spriteMat = EnsureMaterial(MatSpritePath, LoadShader(ShaderSpriteAlphaClipPath), log, m =>
            {
                SetMaterialColor(m, ShaderPropColor, Color.white);
                SetMaterialFloat(m, ShaderPropCutoff, 0.5f);
            });
            Material shadowMat = EnsureMaterial(MatBlobShadowPath, LoadShader(ShaderBlobShadowPath), log, m =>
                SetMaterialColor(m, ShaderPropColor, new Color(0f, 0f, 0f, 0.5f)));
            Shader lit = LoadUrpLit();
            Material groundMat = EnsureMaterial(MatGroundPath, lit, log, m =>
            {
                SetMaterialColor(m, ShaderPropBaseColor, GroundColor);
                SetMaterialFloat(m, ShaderPropSmoothness, 0.1f);
            });
            Material propMat = EnsureMaterial(MatPropPath, lit, log, m =>
            {
                SetMaterialColor(m, ShaderPropBaseColor, PropColor);
                SetMaterialFloat(m, ShaderPropSmoothness, 0.2f);
            });

            // --- Data ---
            EventBusAsset eventBus = EnsureAsset<EventBusAsset>(AssetEventBusPath, log, null);
            InputBufferSettings bufferSettings = EnsureAsset<InputBufferSettings>(AssetInputBufferPath, log, so =>
            {
                SetFloat(so, InputBufferSettingsProps.BufferWindowSeconds, 0.2f);
                SetInt(so, InputBufferSettingsProps.Capacity, 8);
            });
            CombatSettings combatSettings = EnsureAsset<CombatSettings>(AssetCombatSettingsPath, log, so =>
            {
                SetInt(so, CombatSettingsProps.HurtboxLayers, ~0);
                SetInt(so, CombatSettingsProps.MaxOverlapResults, 16);
                SetFloat(so, CombatSettingsProps.DepthTolerance, 0.35f);
            });
            TrainingDummySettings dummySettings = EnsureAsset<TrainingDummySettings>(AssetDummySettingsPath, log, so =>
            {
                SetInt(so, TrainingDummySettingsProps.MaxHealth, 100);
                SetFloat(so, TrainingDummySettingsProps.ResetDelaySeconds, 2f);
                SetFloat(so, TrainingDummySettingsProps.KnockbackScale, 1f);
                SetFloat(so, TrainingDummySettingsProps.KnockbackDeceleration, 25f);
                SetFloat(so, TrainingDummySettingsProps.Gravity, -30f);
            });
            PlayerMovementSettings movementSettings = EnsureAsset<PlayerMovementSettings>(AssetMovementPath, log, so =>
            {
                SetFloat(so, MovementSettingsProps.MoveSpeed, 6f);
                SetFloat(so, MovementSettingsProps.DepthSpeedMultiplier, 0.75f);
                SetFloat(so, MovementSettingsProps.Acceleration, 80f);
                SetFloat(so, MovementSettingsProps.Deceleration, 100f);
                SetFloat(so, MovementSettingsProps.InputDeadzone, 0.15f);
                SetFloat(so, MovementSettingsProps.Gravity, -30f);
                SetFloat(so, MovementSettingsProps.DashSpeed, 16f);
                SetInt(so, MovementSettingsProps.DashFrames, 12);
                SetInt(so, MovementSettingsProps.DashIFrameStart, 0);
                SetInt(so, MovementSettingsProps.DashIFrameEnd, 8);
                SetFloat(so, MovementSettingsProps.KnockbackDeceleration, 30f);
            });

            // §7.4 starter attack values.
            AttackData jab1 = EnsureAttack(AssetJab1Path, log, 8, new Vector2(2f, 0f), 14, 4, 3, 10, 7, 17,
                new Vector2(1.1f, 0.8f), new Vector2(0.7f, 1.0f), 1.5f, 3, 0.10f);
            AttackData jab2 = EnsureAttack(AssetJab2Path, log, 10, new Vector2(2.5f, 0f), 16, 5, 3, 12, 8, 20,
                new Vector2(1.2f, 0.8f), new Vector2(0.75f, 1.0f), 2.0f, 4, 0.15f);
            AttackData finisher = EnsureAttack(AssetFinisherPath, log, 18, new Vector2(9f, 2f), 28, 8, 4, 22, 20, 34,
                new Vector2(1.6f, 1.2f), new Vector2(0.9f, 1.0f), 4.0f, 8, 0.45f);

            ComboData combo = EnsureAsset<ComboData>(AssetComboPath, log, so =>
            {
                SerializedProperty attacks = Prop(so, ComboDataProps.Attacks);
                if (attacks == null)
                {
                    return;
                }

                attacks.arraySize = 3;
                attacks.GetArrayElementAtIndex(0).objectReferenceValue = jab1;
                attacks.GetArrayElementAtIndex(1).objectReferenceValue = jab2;
                attacks.GetArrayElementAtIndex(2).objectReferenceValue = finisher;
            });

            FeedbackSettings feedbackSettings = EnsureAsset<FeedbackSettings>(AssetFeedbackPath, log, so =>
            {
                SetFloat(so, FeedbackSettingsProps.HitStopTimeScale, 0f);
                SetFloat(so, FeedbackSettingsProps.MaxHitStopSeconds, 0.25f);
                SetFloat(so, FeedbackSettingsProps.ShakeMaxOffset, 0.3f);
                SetFloat(so, FeedbackSettingsProps.ShakeFrequency, 25f);
                SetFloat(so, FeedbackSettingsProps.TraumaDecayPerSecond, 2f);
                SetFloat(so, FeedbackSettingsProps.DamageNumberLifetime, 0.7f);
                SetFloat(so, FeedbackSettingsProps.DamageNumberRise, 1.2f);
                SetVector3(so, FeedbackSettingsProps.DamageNumberSpawnOffset, new Vector3(0f, 0.5f, 0f));
                SetFloat(so, FeedbackSettingsProps.DamageNumberSpread, 0.25f);
                SetInt(so, FeedbackSettingsProps.DamageNumberPoolCapacity, 16);
                SetInt(so, FeedbackSettingsProps.DamageNumberPoolMaxSize, 64);
            });
            CameraRigSettings cameraSettings = EnsureAsset<CameraRigSettings>(AssetCameraRigPath, log, so =>
            {
                SetFloat(so, CameraRigSettingsProps.Pitch, 25f);
                SetFloat(so, CameraRigSettingsProps.Yaw, 0f);
                SetFloat(so, CameraRigSettingsProps.Distance, 14f);
                SetFloat(so, CameraRigSettingsProps.FieldOfView, 30f);
                SetVector3(so, CameraRigSettingsProps.TargetOffset, new Vector3(0f, 1f, 0f));
                SetFloat(so, CameraRigSettingsProps.FollowSmoothTime, 0.12f);
            });

            DamageNumber damageNumberPrefab = EnsureDamageNumberPrefab(log);

            AssetDatabase.SaveAssets();

            // --- Scene ---
            var ctx = new SceneContext
            {
                SpriteMaterial = spriteMat,
                ShadowMaterial = shadowMat,
                GroundMaterial = groundMat,
                PropMaterial = propMat,
                PlayerSprite = playerSprite,
                DummySprite = dummySprite,
                EventBus = eventBus,
                InputBufferSettings = bufferSettings,
                CombatSettings = combatSettings,
                DummySettings = dummySettings,
                MovementSettings = movementSettings,
                Combo = combo,
                FeedbackSettings = feedbackSettings,
                CameraSettings = cameraSettings,
                DamageNumberPrefab = damageNumberPrefab,
            };
            BuildSceneIfWanted(ctx, log);

            EnsureInBuildSettings(ScenePath, log);

            Debug.Log(log.ToString());
        }

        // ============ Folders ============

        private static void EnsureFolders(StringBuilder log)
        {
            foreach (string folder in RequiredFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string leaf = Path.GetFileName(folder);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    string grandParent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                    AssetDatabase.CreateFolder(grandParent, Path.GetFileName(parent));
                }

                AssetDatabase.CreateFolder(parent, leaf);
                log.AppendLine($"  + folder {folder}");
            }
        }

        // ============ Placeholder Sprites ============

        private static Sprite EnsureSprite(string assetPath, Color body, Color accent, bool isDummy, StringBuilder log)
        {
            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                Texture2D tex = DrawHumanoid(body, accent, isDummy);
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                log.AppendLine($"  + sprite {assetPath}");
            }
            else
            {
                log.AppendLine($"  = sprite {assetPath} (exists)");
            }

            // Configure the importer only if it isn't a sprite yet (fresh PNG); never re-touch hand-tuned settings.
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                ConfigureSpriteImporter(importer);
            }

            Sprite sprite = SpriteImport.LoadSprite(assetPath);
            if (sprite == null)
            {
                Debug.LogError($"[Brawler] Could not load a Sprite from {assetPath}.");
            }

            return sprite;
        }

        private static void ConfigureSpriteImporter(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpritePixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Draws a 32x64 chunky humanoid silhouette (head, torso, arms, legs, dark outline) on a transparent
        /// background. The eye sits on the right (+X) so sprite flipping is visible. Dummies get a target mark.
        /// </summary>
        private static Texture2D DrawHumanoid(Color body, Color accent, bool isDummy)
        {
            var mask = new byte[SpriteWidth * SpriteHeight]; // 0 empty, 1 body, 2 accent, 3 eye, 4 dark
            Color dark = Color.Lerp(body, Color.black, 0.45f);

            // Legs.
            FillRect(mask, 10, 0, 14, 20, 4);
            FillRect(mask, 18, 0, 22, 20, 4);
            // Torso.
            FillRect(mask, 9, 19, 23, 42, 1);
            // Belt / accent.
            FillRect(mask, 9, 19, 23, 22, 2);
            // Arms.
            FillRect(mask, 5, 24, 9, 40, 1);
            FillRect(mask, 23, 24, 27, 40, 1);
            // Fists.
            FillRect(mask, 5, 22, 9, 25, 2);
            FillRect(mask, 23, 22, 27, 25, 2);
            // Head.
            FillCircle(mask, 16, 51, 8, 1);
            // Facing indicator: eye + headband tail on the right side.
            FillRect(mask, 19, 51, 21, 54, 3);
            FillRect(mask, 9, 55, 24, 57, 2);

            if (isDummy)
            {
                // Target rings on the chest.
                FillCircle(mask, 16, 31, 5, 2);
                FillCircle(mask, 16, 31, 3, 1);
                FillCircle(mask, 16, 31, 1, 2);
            }

            var pixels = new Color32[SpriteWidth * SpriteHeight];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 outline = new Color32(20, 20, 28, 255);
            for (int y = 0; y < SpriteHeight; y++)
            {
                for (int x = 0; x < SpriteWidth; x++)
                {
                    int i = y * SpriteWidth + x;
                    switch (mask[i])
                    {
                        case 1: pixels[i] = body; break;
                        case 2: pixels[i] = accent; break;
                        case 3: pixels[i] = Color.white; break;
                        case 4: pixels[i] = dark; break;
                        default: pixels[i] = IsEdge(mask, x, y) ? outline : clear; break;
                    }
                }
            }

            var tex = new Texture2D(SpriteWidth, SpriteHeight, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void FillRect(byte[] mask, int x0, int y0, int x1, int y1, byte value)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(SpriteHeight, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(SpriteWidth, x1); x++)
                {
                    mask[y * SpriteWidth + x] = value;
                }
            }
        }

        private static void FillCircle(byte[] mask, int cx, int cy, int r, byte value)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= SpriteWidth || y >= SpriteHeight)
                    {
                        continue;
                    }

                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r * r + r)
                    {
                        mask[y * SpriteWidth + x] = value;
                    }
                }
            }
        }

        private static bool IsEdge(byte[] mask, int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = x + ox;
                    int ny = y + oy;
                    if (nx >= 0 && ny >= 0 && nx < SpriteWidth && ny < SpriteHeight && mask[ny * SpriteWidth + nx] != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ============ Shaders & Materials ============

        private static Shader LoadShader(string assetPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader == null)
            {
                Debug.LogError($"[Brawler] Shader not found at {assetPath}. Has Unity imported it (check the Console for shader errors)?");
            }

            return shader;
        }

        private static Shader LoadUrpLit()
        {
            string path = AssetDatabase.GUIDToAssetPath(UrpLitShaderGuid);
            Shader shader = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                shader = Shader.Find(UrpLitShaderName);
            }

            if (shader == null)
            {
                Debug.LogError("[Brawler] URP Lit shader not found. Is URP installed and active?");
            }

            return shader;
        }

        private static Material EnsureMaterial(string assetPath, Shader shader, StringBuilder log, Action<Material> init)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                log.AppendLine($"  = material {assetPath} (exists)");
                return existing;
            }

            if (shader == null)
            {
                log.AppendLine($"  ! material {assetPath} SKIPPED (shader missing)");
                return null;
            }

            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(assetPath) };
            init?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, assetPath);
            log.AppendLine($"  + material {assetPath}");
            return mat;
        }

        private static void SetMaterialColor(Material m, string property, Color value)
        {
            if (m.HasProperty(property))
            {
                m.SetColor(property, value);
            }
        }

        private static void SetMaterialFloat(Material m, string property, float value)
        {
            if (m.HasProperty(property))
            {
                m.SetFloat(property, value);
            }
        }

        // ============ Data Assets ============

        private static T EnsureAsset<T>(string assetPath, StringBuilder log, Action<SerializedObject> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                log.AppendLine($"  = asset {assetPath} (exists, untouched)");
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);

            if (init != null)
            {
                var so = new SerializedObject(asset);
                init(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            log.AppendLine($"  + asset {assetPath}");
            return asset;
        }

        private static AttackData EnsureAttack(string assetPath, StringBuilder log, int damage, Vector2 knockback,
            int hitstun, int startup, int active, int recovery, int cancelStart, int cancelEnd,
            Vector2 hitboxSize, Vector2 hitboxOffset, float lunge, int hitStopFrames, float shake)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<AttackData>(assetPath) != null;
            AttackData attack = AttackAssets.Ensure(assetPath, new AttackAssets.Spec
            {
                Damage = damage,
                Knockback = knockback,
                Hitstun = hitstun,
                Startup = startup,
                Active = active,
                Recovery = recovery,
                CancelStart = cancelStart,
                CancelEnd = cancelEnd,
                HitboxSize = hitboxSize,
                HitboxOffset = hitboxOffset,
                Lunge = lunge,
                HitStop = hitStopFrames,
                Shake = shake,
            });
            log.AppendLine(exists ? $"  = asset {assetPath} (exists, untouched)" : $"  + asset {assetPath}");
            return attack;
        }

        // ============ Damage Number Prefab ============

        private static DamageNumber EnsureDamageNumberPrefab(StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDamageNumberPath);
            if (existing != null)
            {
                log.AppendLine($"  = prefab {PrefabDamageNumberPath} (exists)");
                return existing.GetComponent<DamageNumber>();
            }

            var go = new GameObject(Path.GetFileNameWithoutExtension(PrefabDamageNumberPath));
            try
            {
                var renderer = GetOrAdd<MeshRenderer>(go);
                var text = GetOrAdd<TextMesh>(go);
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.font = font;
                text.text = "0";
                text.fontSize = 64;
                text.characterSize = 0.05f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontStyle = FontStyle.Bold;
                text.color = DamageNumberColor;
                if (font != null)
                {
                    renderer.sharedMaterial = font.material;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var number = go.AddComponent<DamageNumber>();
                var so = new SerializedObject(number);
                SetObject(so, DamageNumberProps.Text, text);
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDamageNumberPath);
                log.AppendLine($"  + prefab {PrefabDamageNumberPath}");
                return prefab != null ? prefab.GetComponent<DamageNumber>() : null;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ============ Scene ============

        /// <summary>Everything the scene needs, gathered once.</summary>
        private sealed class SceneContext
        {
            public Material SpriteMaterial;
            public Material ShadowMaterial;
            public Material GroundMaterial;
            public Material PropMaterial;
            public Sprite PlayerSprite;
            public Sprite DummySprite;
            public EventBusAsset EventBus;
            public InputBufferSettings InputBufferSettings;
            public CombatSettings CombatSettings;
            public TrainingDummySettings DummySettings;
            public PlayerMovementSettings MovementSettings;
            public ComboData Combo;
            public FeedbackSettings FeedbackSettings;
            public CameraRigSettings CameraSettings;
            public DamageNumber DamageNumberPrefab;
            public Transform CameraTransform;
            public Quaternion BillboardRotation;
        }

        private static void ReloadAssets(SceneContext ctx)
        {
            ctx.SpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatSpritePath);
            ctx.ShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatBlobShadowPath);
            ctx.GroundMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatGroundPath);
            ctx.PropMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPropPath);
            ctx.PlayerSprite = SpriteImport.LoadSprite(SpritePlayerPath);
            ctx.DummySprite = SpriteImport.LoadSprite(SpriteDummyPath);
            ctx.EventBus = AssetDatabase.LoadAssetAtPath<EventBusAsset>(AssetEventBusPath);
            ctx.InputBufferSettings = AssetDatabase.LoadAssetAtPath<InputBufferSettings>(AssetInputBufferPath);
            ctx.CombatSettings = AssetDatabase.LoadAssetAtPath<CombatSettings>(AssetCombatSettingsPath);
            ctx.DummySettings = AssetDatabase.LoadAssetAtPath<TrainingDummySettings>(AssetDummySettingsPath);
            ctx.MovementSettings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(AssetMovementPath);
            ctx.Combo = AssetDatabase.LoadAssetAtPath<ComboData>(AssetComboPath);
            ctx.FeedbackSettings = AssetDatabase.LoadAssetAtPath<FeedbackSettings>(AssetFeedbackPath);
            ctx.CameraSettings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(AssetCameraRigPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDamageNumberPath);
            ctx.DamageNumberPrefab = prefab != null ? prefab.GetComponent<DamageNumber>() : null;
        }

        private static void BuildSceneIfWanted(SceneContext ctx, StringBuilder log)
        {
            if (File.Exists(ToFullPath(ScenePath)))
            {
                bool rebuild = EditorUtility.DisplayDialog(
                    "Brawler: Build Test Scene",
                    $"{ScenePath} already exists.\n\nRebuild it from scratch? (Scene edits are lost; data assets, materials, sprites and the prefab are kept.)",
                    "Rebuild Scene",
                    "Keep & Open");
                if (!rebuild)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    log.AppendLine($"  = scene {ScenePath} (exists, opened)");
                    return;
                }
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) runs an unused-asset unload that destroys the in-memory ScriptableObjects/prefab
            // created above (their C# wrappers become fake-null), so re-load every reference from disk.
            ReloadAssets(ctx);

            ConfigureEnvironment(log);
            Light sun = CreateLight();
            RenderSettings.sun = sun;
            CreateArena(ctx);

            CameraRig rig = CreateCameraRig(ctx, out Transform shakeRoot);
            Transform player = CreatePlayer(ctx, log);
            CreateDummy(ctx, "Dummy_A", DummyAPosition);
            CreateDummy(ctx, "Dummy_B", DummyBPosition);
            CreateFeedback(ctx);

            // Rig target needs the player, which needs the camera transform → wire after both exist.
            var rigSo = new SerializedObject(rig);
            SetObject(rigSo, CameraRigProps.Target, player);
            rigSo.ApplyModifiedPropertiesWithoutUndo();
            rig.ApplySettings();
            rig.SnapToTarget();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            log.AppendLine(saved ? $"  + scene {ScenePath}" : $"  ! scene {ScenePath} FAILED to save");
            Selection.activeTransform = player;
        }

        private static void ConfigureEnvironment(StringBuilder log)
        {
            // Trilight ambient feeds the SH probe immediately (no lighting bake needed), which SpriteAlphaClip samples.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.25f);
            RenderSettings.fog = false;
            log.AppendLine("  * environment: trilight ambient, no fog");
        }

        private static Light CreateLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static void CreateArena(SceneContext ctx)
        {
            GameObject arena = GameObject.CreatePrimitive(PrimitiveType.Cube); // includes BoxCollider
            arena.name = "Arena";
            arena.transform.position = new Vector3(0f, -ArenaSize.y * 0.5f, 0f); // top face at y = 0
            arena.transform.localScale = ArenaSize;
            arena.isStatic = true;
            SetMaterial(arena, ctx.GroundMaterial);

            // A few props at different depths to show sprites sorting by depth against 3D geometry.
            CreateProp(arena.transform, PrimitiveType.Cube, "Prop_Crate_Back", new Vector3(-4f, 0.5f, 2.5f), Vector3.one, ctx.PropMaterial);
            CreateProp(arena.transform, PrimitiveType.Cube, "Prop_Crate_Front", new Vector3(-2f, 0.4f, -2.5f), new Vector3(0.8f, 0.8f, 0.8f), ctx.PropMaterial);
            CreateProp(arena.transform, PrimitiveType.Cylinder, "Prop_Pillar", new Vector3(1.5f, 1.5f, 3f), new Vector3(0.8f, 1.5f, 0.8f), ctx.PropMaterial);
            CreateProp(arena.transform, PrimitiveType.Cube, "Prop_Wall_Back", new Vector3(0f, 1f, 5.5f), new Vector3(24f, 2f, 1f), ctx.PropMaterial);
        }

        private static void CreateProp(Transform arena, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject prop = GameObject.CreatePrimitive(type);
            prop.name = name;
            // Not parented under Arena (its non-uniform scale would skew children); kept at root, grouped by name.
            prop.transform.position = position;
            prop.transform.localScale = scale;
            prop.isStatic = true;
            SetMaterial(prop, material);
        }

        private static CameraRig CreateCameraRig(SceneContext ctx, out Transform shakeRoot)
        {
            CameraRigSettings s = ctx.CameraSettings;
            float pitch = s != null ? s.Pitch : 25f;
            float yaw = s != null ? s.Yaw : 0f;
            float distance = s != null ? s.Distance : 14f;
            float fov = s != null ? s.FieldOfView : 30f;

            var rigGo = new GameObject("CameraRig");
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(rigGo.transform, false);
            // Pre-apply the angle so the scene looks right before CameraRig.ApplySettings is implemented.
            pivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            shakeRoot = new GameObject("ShakeRoot").transform;
            shakeRoot.SetParent(pivot, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera"; // engine/URP conventions only; our code never looks it up.
            camGo.transform.SetParent(shakeRoot, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -distance);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            camGo.AddComponent<AudioListener>();
            // UniversalAdditionalCameraData is added automatically by URP the first time the camera is used/inspected.

            var rig = rigGo.AddComponent<CameraRig>();
            var so = new SerializedObject(rig);
            SetObject(so, CameraRigProps.Settings, ctx.CameraSettings);
            SetObject(so, CameraRigProps.Pivot, pivot);
            SetObject(so, CameraRigProps.Camera, cam);
            so.ApplyModifiedPropertiesWithoutUndo();

            ctx.CameraTransform = camGo.transform;
            ctx.BillboardRotation = pivot.rotation;
            return rig;
        }

        private static Transform CreatePlayer(SceneContext ctx, StringBuilder log)
        {
            var root = new GameObject("Player");
            root.transform.position = PlayerPosition;

            // PlayerController [RequireComponent]s CharacterController + PlayerInputReader: add it first, then fetch.
            var controller = root.AddComponent<PlayerController>();
            CharacterController cc = AddCharacterController(root);
            PlayerInputReader input = GetOrAdd<PlayerInputReader>(root);
            // Execution order comes from [DefaultExecutionOrder(PlayerInputReader.ExecutionOrder)] — no .meta edits here.

            // Input actions (sub-asset InputActionReferences generated by the .inputactions importer).
            var inputSo = new SerializedObject(input);
            SetObject(inputSo, PlayerInputReaderProps.MoveAction, FindActionReference(InputMoveName));
            SetObject(inputSo, PlayerInputReaderProps.AttackAction, FindActionReference(InputAttackName));
            SetObject(inputSo, PlayerInputReaderProps.DashAction, FindActionReference(InputDashName));
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            CreateVisual(ctx, root.transform, ctx.PlayerSprite, controller);
            Transform shadow = CreateShadow(ctx, root.transform, 1f);
            AddBlobShadow(root, shadow, 1f);
            Hurtbox hurtbox = CreateHurtbox(root.transform, controller);

            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(root.transform, false); // at the feet; AttackData offsets are from here
            var hitbox = hitboxGo.AddComponent<Hitbox>();
            var hitboxSo = new SerializedObject(hitbox);
            SetObject(hitboxSo, HitboxProps.CombatSettings, ctx.CombatSettings);
            SetObject(hitboxSo, HitboxProps.EventBus, ctx.EventBus);
            SetObject(hitboxSo, HitboxProps.Owner, root.transform);
            SetObject(hitboxSo, HitboxProps.OwnerHurtbox, hurtbox);
            SetBool(hitboxSo, HitboxProps.DrawGizmos, true);
            hitboxSo.ApplyModifiedPropertiesWithoutUndo();

            var pcSo = new SerializedObject(controller);
            SetObject(pcSo, PlayerControllerProps.CharacterController, cc);
            SetObject(pcSo, PlayerControllerProps.InputReader, input);
            SetObject(pcSo, PlayerControllerProps.MovementSettings, ctx.MovementSettings);
            SetObject(pcSo, PlayerControllerProps.InputBufferSettings, ctx.InputBufferSettings);
            SetObject(pcSo, PlayerControllerProps.ComboData, ctx.Combo);
            SetObject(pcSo, PlayerControllerProps.Hitbox, hitbox);
            pcSo.ApplyModifiedPropertiesWithoutUndo();

            return root.transform;
        }

        private static void CreateDummy(SceneContext ctx, string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            // TrainingDummy [RequireComponent]s CharacterController: add it first, then fetch/configure.
            var dummy = root.AddComponent<TrainingDummy>();
            CharacterController cc = AddCharacterController(root);
            var so = new SerializedObject(dummy);
            SetObject(so, TrainingDummyProps.Settings, ctx.DummySettings);
            SetObject(so, TrainingDummyProps.CharacterController, cc);
            so.ApplyModifiedPropertiesWithoutUndo();

            CreateVisual(ctx, root.transform, ctx.DummySprite, null);
            Transform shadow = CreateShadow(ctx, root.transform, 1.1f);
            AddBlobShadow(root, shadow, 1.1f);
            CreateHurtbox(root.transform, dummy);
        }

        private static void CreateFeedback(SceneContext ctx)
        {
            var go = new GameObject("Feedback");

            var hitStop = go.AddComponent<HitStop>();
            var hsSo = new SerializedObject(hitStop);
            SetObject(hsSo, FeedbackListenerProps.EventBus, ctx.EventBus);
            SetObject(hsSo, FeedbackListenerProps.Settings, ctx.FeedbackSettings);
            hsSo.ApplyModifiedPropertiesWithoutUndo();

            var shake = go.AddComponent<ScreenShake>();
            var shSo = new SerializedObject(shake);
            SetObject(shSo, FeedbackListenerProps.EventBus, ctx.EventBus);
            SetObject(shSo, FeedbackListenerProps.Settings, ctx.FeedbackSettings);
            shSo.ApplyModifiedPropertiesWithoutUndo();

            var spawner = go.AddComponent<DamageNumberSpawner>();
            var spSo = new SerializedObject(spawner);
            SetObject(spSo, FeedbackListenerProps.EventBus, ctx.EventBus);
            SetObject(spSo, FeedbackListenerProps.Settings, ctx.FeedbackSettings);
            SetObject(spSo, FeedbackListenerProps.Prefab, ctx.DamageNumberPrefab);
            SetObject(spSo, FeedbackListenerProps.CameraTransform, ctx.CameraTransform);
            spSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============ Actor Parts ============

        private static CharacterController AddCharacterController(GameObject root)
        {
            CharacterController cc = GetOrAdd<CharacterController>(root);
            cc.height = ActorHeight;
            cc.radius = ActorRadius;
            cc.center = new Vector3(0f, ActorHeight * 0.5f, 0f); // pivot at the feet
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.05f;
            cc.slopeLimit = 45f;
            return cc;
        }

        private static void CreateVisual(SceneContext ctx, Transform parent, Sprite sprite, MonoBehaviour facingSource)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            // Pre-rotate to the camera so edit mode looks right before BillboardSprite.LateUpdate is implemented.
            go.transform.rotation = ctx.BillboardRotation;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (ctx.SpriteMaterial != null)
            {
                sr.sharedMaterial = ctx.SpriteMaterial;
            }

            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;

            var billboard = go.AddComponent<BillboardSprite>();
            var so = new SerializedObject(billboard);
            SetObject(so, BillboardSpriteProps.CameraTransform, ctx.CameraTransform);
            SetObject(so, BillboardSpriteProps.SpriteRenderer, sr);
            SetObject(so, BillboardSpriteProps.FacingSource, facingSource);
            SetBool(so, BillboardSpriteProps.MatchCameraPitch, true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateShadow(SceneContext ctx, Transform parent, float scale)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Shadow";
            // No collider: BlobShadow's raycast must never hit its own shadow.
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, face up
            quad.transform.localScale = new Vector3(scale, scale, scale);

            var mr = quad.GetComponent<MeshRenderer>();
            if (ctx.ShadowMaterial != null)
            {
                mr.sharedMaterial = ctx.ShadowMaterial;
            }

            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return quad.transform;
        }

        private static void AddBlobShadow(GameObject root, Transform shadow, float baseScale)
        {
            var blob = root.AddComponent<BlobShadow>();
            var so = new SerializedObject(blob);
            SetObject(so, BlobShadowProps.ShadowTransform, shadow);
            SetFloat(so, BlobShadowProps.BaseScale, baseScale);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Hurtbox CreateHurtbox(Transform parent, MonoBehaviour owner)
        {
            var go = new GameObject("Hurtbox");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = HurtboxCenter;
            box.size = HurtboxSize;

            var hurtbox = go.AddComponent<Hurtbox>();
            var so = new SerializedObject(hurtbox);
            SetObject(so, HurtboxProps.Collider, box);
            SetObject(so, HurtboxProps.Owner, owner);
            so.ApplyModifiedPropertiesWithoutUndo();
            return hurtbox;
        }

        // ============ Input ============

        private static InputActionReference FindActionReference(string actionName)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath);
            foreach (Object obj in subAssets)
            {
                if (obj is InputActionReference reference && reference.action != null
                    && reference.action.name == actionName
                    && reference.action.actionMap != null && reference.action.actionMap.name == InputMapName)
                {
                    return reference;
                }
            }

            Debug.LogError($"[Brawler] InputActionReference '{InputMapName}/{actionName}' not found in {InputActionsPath}. " +
                           "Is the .inputactions asset imported? Wire it by hand on Player > PlayerInputReader.");
            return null;
        }

        // ============ Build Settings ============

        private static void EnsureInBuildSettings(string scenePath, StringBuilder log)
        {
            if (!File.Exists(ToFullPath(scenePath)))
            {
                return;
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == scenePath)
                {
                    log.AppendLine($"  = build settings already contain {scenePath}");
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            Array.Copy(scenes, updated, scenes.Length);
            updated[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
            log.AppendLine($"  + build settings: added {scenePath}");
        }

        // ============ SerializedObject Helpers ============

        /// <summary>Finds a property by its §7 name; logs a contract mismatch instead of throwing.</summary>
        private static SerializedProperty Prop(SerializedObject so, string path)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogError($"[Brawler] Contract mismatch: '{so.targetObject.GetType().Name}' has no serialized field '{path}' (ARCHITECTURE.md §7).", so.targetObject);
            }

            return p;
        }

        private static void SetObject(SerializedObject so, string path, Object value)
        {
            SerializedProperty p = Prop(so, path);
            if (p != null)
            {
                p.objectReferenceValue = value;
            }
        }

        private static void SetFloat(SerializedObject so, string path, float value)
        {
            SerializedProperty p = Prop(so, path);
            if (p != null)
            {
                p.floatValue = value;
            }
        }

        private static void SetInt(SerializedObject so, string path, int value)
        {
            SerializedProperty p = Prop(so, path);
            if (p != null)
            {
                p.intValue = value;
            }
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            SerializedProperty p = Prop(so, path);
            if (p != null)
            {
                p.boolValue = value;
            }
        }

        private static void SetVector2(SerializedObject so, string path, Vector2 value)
        {
            SerializedProperty p = Prop(so, path);
            if (p != null)
            {
                p.vector2Value = value;
            }
        }

        private static void SetVector3(SerializedObject so, string path, Vector3 value)
        {
            SerializedProperty p = Prop(so, path);
            if (p != null)
            {
                p.vector3Value = value;
            }
        }

        // ============ Misc Helpers ============

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            if (material != null && go.TryGetComponent(out MeshRenderer mr))
            {
                mr.sharedMaterial = material;
            }
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, assetPath);
        }
    }
}
