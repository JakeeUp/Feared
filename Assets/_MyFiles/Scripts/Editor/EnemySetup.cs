using System.Collections.Generic;
using System.IO;
using System.Text;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Enemy;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling: menu <c>Brawler/Setup Enemy (Shade)</c>. Builds the "Shade" enemy from the Dark variant of the
    /// Thomas Lean pack and puts a respawning group of them into the open scene:
    /// imports and anchors the Dark frames (same pipeline as the hero), creates the enemy's attack and settings assets
    /// (created once, then left for you to tune; the hitbox is re-measured from the art every run), rebuilds the
    /// <c>Enemy_Shade</c> prefab, gives the player Health / a health bar / teams, and adds a coordinator + spawner.
    /// </summary>
    /// <remarks>Run Setup Hero first. Safe to re-run: the prefab is rebuilt, scene objects are reused by name.</remarks>
    public static class EnemySetup
    {
        // ============ Paths ============

        private const string DarkRoot = "Assets/_ThirdParty/AssetPack/Dark";
        private const string Root = "Assets/_MyFiles";
        private const string EnemyDataDir = Root + "/Data/Enemy";
        private const string AttackPath = EnemyDataDir + "/Attack_Shade_Slash.asset";
        private const string SettingsPath = EnemyDataDir + "/EnemySettings_Shade.asset";
        private const string PrefabDir = Root + "/Prefabs";
        private const string PrefabPath = PrefabDir + "/Enemy_Shade.prefab";
        private const string CombatSettingsPath = Root + "/Data/Combat/CombatSettings.asset";
        private const string EventBusPath = Root + "/Data/Core/EventBus.asset";
        private const string SpriteMaterialPath = Root + "/Art/Materials/M_SpriteAlphaClip.mat";
        private const string ShadowMaterialPath = Root + "/Art/Materials/M_BlobShadow.mat";
        private const string BarBackMaterialPath = Root + "/Art/Materials/M_HealthBar_Back.mat";
        private const string BarFillMaterialPath = Root + "/Art/Materials/M_HealthBar_Fill.mat";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        // ============ Look & Layout ============

        private static readonly Color BarBackColor = new Color(0.08f, 0.08f, 0.1f, 1f);
        private static readonly Color EnemyBarColor = new Color(0.85f, 0.15f, 0.2f, 1f);
        private const float BarHeight = 1.3f;
        private static readonly Vector2 BarSize = new Vector2(0.8f, 0.09f);
        private const float BarBorder = 0.02f;
        private const float ShadowScale = 0.5f;
        private const int PlayerMaxHealth = 100;

        private static readonly Vector3[] SpawnPoints =
        {
            new Vector3(5f, 0f, 3f),
            new Vector3(-5f, 0f, 3f),
            new Vector3(3f, 0f, -3f),
        };

        // ============ Menu ============

        [MenuItem("Brawler/Setup Enemy (Shade)")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(DarkRoot))
            {
                EditorUtility.DisplayDialog("Setup Enemy", $"Couldn't find {DarkRoot}. Unzip the Thomas Lean pack into Assets/_ThirdParty/ first.", "OK");
                return;
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Setup Enemy", "No PlayerController in the open scene. Open TestArena (and run Setup Hero) first.", "OK");
                return;
            }

            var log = new StringBuilder("[Brawler] Setup Enemy (Shade)\n");
            float ppu = HeroSetup.CharacterPpu(DarkRoot);
            Dictionary<string, Vector2Int> pivots;
            try
            {
                AssetDatabase.StartAssetEditing();
                pivots = HeroSetup.ImportCharacterFrames(DarkRoot, ppu, log);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Dictionary<string, SpriteClip> clips = HeroSetup.BuildCharacterClips(DarkRoot, "Shade", log);
            Directory.CreateDirectory(EnemyDataDir);
            AttackData attack = EnsureAttack(ppu, pivots, log);
            EnemySettings settings = EnsureSettings(attack, log);
            Material barBack = EnsureUnlitMaterial(BarBackMaterialPath, BarBackColor);
            Material barFill = EnsureUnlitMaterial(BarFillMaterialPath, EnemyBarColor);

            Transform cameraTransform = PlayerCamera(player);
            int hurtboxLayer = player.GetComponentInChildren<Hurtbox>(true)?.gameObject.layer ?? 0;
            EnemyController prefab = BuildPrefab(settings, clips, barBack, barFill, hurtboxLayer, log);

            PreparePlayer(player, log);
            if (!CathedralBuilder.IsCathedralScene())
            {
                BuildSpawner(player, cameraTransform, prefab, log); // the Cathedral spawns through its encounters
            }

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ============ Assets ============

        /// <summary>Slow, readable swing. Created once with starter values; the hitbox is re-measured from the art every run.</summary>
        private static AttackData EnsureAttack(float ppu, Dictionary<string, Vector2Int> pivots, StringBuilder log)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(AttackPath);
            var created = attack == null;
            if (created)
            {
                attack = ScriptableObject.CreateInstance<AttackData>();
                AssetDatabase.CreateAsset(attack, AttackPath);
            }

            var so = new SerializedObject(attack);
            if (created)
            {
                so.FindProperty("_damage").intValue = 8;
                so.FindProperty("_knockback").vector2Value = new Vector2(3.5f, 0f);
                so.FindProperty("_hitstunFrames").intValue = 18;
                SerializedProperty frames = so.FindProperty("_frames");
                frames.FindPropertyRelative("_startup").intValue = 18; // the telegraph: long enough to react to
                frames.FindPropertyRelative("_active").intValue = 3;
                frames.FindPropertyRelative("_recovery").intValue = 24;
                frames.FindPropertyRelative("_cancelWindowStart").intValue = 0;
                frames.FindPropertyRelative("_cancelWindowEnd").intValue = 0;
                so.FindProperty("_lungeSpeed").floatValue = 2f;
                so.FindProperty("_hitStopFrames").intValue = 5;
                so.FindProperty("_screenShake").floatValue = 0.25f;
            }

            if (pivots.TryGetValue("attack1", out Vector2Int pivot)
                && HeroSetup.MeasureSwing(DarkRoot, "attack1", ppu, pivot, out Vector2 size, out Vector2 offset))
            {
                so.FindProperty("_hitboxSize").vector2Value = size;
                so.FindProperty("_hitboxOffset").vector2Value = offset;
            }

            so.ApplyModifiedProperties();
            log.AppendLine($"  * {(created ? "created" : "kept")} {AttackPath} (hitbox re-measured from the swing)");
            return attack;
        }

        private static EnemySettings EnsureSettings(AttackData attack, StringBuilder log)
        {
            var settings = AssetDatabase.LoadAssetAtPath<EnemySettings>(SettingsPath);
            bool created = settings == null;
            if (created)
            {
                settings = ScriptableObject.CreateInstance<EnemySettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var so = new SerializedObject(settings);
            so.FindProperty("_attack").objectReferenceValue = attack;
            so.ApplyModifiedProperties();
            log.AppendLine($"  * {(created ? "created" : "kept")} {SettingsPath}");
            return settings;
        }

        internal static Material EnsureUnlitMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(UnlitShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ============ Prefab ============

        private static EnemyController BuildPrefab(EnemySettings settings, Dictionary<string, SpriteClip> clips,
                                                   Material barBack, Material barFill, int hurtboxLayer, StringBuilder log)
        {
            Directory.CreateDirectory(PrefabDir);
            var root = new GameObject("Enemy_Shade");
            try
            {
                float height = HeroSetup.CharacterHeight;
                var cc = root.AddComponent<CharacterController>();
                cc.height = height;
                cc.radius = HeroSetup.CapsuleRadius;
                cc.center = new Vector3(0f, height * 0.5f, 0f);
                cc.stepOffset = height * 0.25f;
                cc.slopeLimit = 45f;

                var health = root.AddComponent<Health>();
                var enemy = root.AddComponent<EnemyController>();

                // Hurtbox
                var hurtboxGo = new GameObject("Hurtbox") { layer = hurtboxLayer };
                hurtboxGo.transform.SetParent(root.transform, false);
                var box = hurtboxGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = HeroSetup.HurtboxSize;
                box.center = new Vector3(0f, height * 0.5f, 0f);
                var hurtbox = hurtboxGo.AddComponent<Hurtbox>();
                Set(hurtbox, so =>
                {
                    so.FindProperty("_collider").objectReferenceValue = box;
                    so.FindProperty("_owner").objectReferenceValue = enemy;
                    so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Enemy;
                });

                // Hitbox
                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.transform.SetParent(root.transform, false);
                var hitbox = hitboxGo.AddComponent<Hitbox>();
                Set(hitbox, so =>
                {
                    so.FindProperty("_combatSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatSettings>(CombatSettingsPath);
                    so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
                    so.FindProperty("_owner").objectReferenceValue = root.transform;
                    so.FindProperty("_ownerHurtbox").objectReferenceValue = hurtbox;
                    so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Enemy;
                });

                Set(enemy, so =>
                {
                    so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
                    so.FindProperty("_settings").objectReferenceValue = settings;
                    so.FindProperty("_characterController").objectReferenceValue = cc;
                    so.FindProperty("_health").objectReferenceValue = health;
                    so.FindProperty("_hitbox").objectReferenceValue = hitbox;
                });

                // Blob shadow
                GameObject shadow = Quad("Shadow", root.transform, AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath));
                shadow.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shadow.transform.localScale = Vector3.one * ShadowScale;
                var blob = root.AddComponent<BlobShadow>();
                Set(blob, so =>
                {
                    so.FindProperty("_shadowTransform").objectReferenceValue = shadow.transform;
                    so.FindProperty("_baseScale").floatValue = ShadowScale;
                });

                // Visual
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
                sr.sprite = clips["Idle"].GetFrame(0);
                sr.shadowCastingMode = ShadowCastingMode.On;
                sr.receiveShadows = false;
                var animator = visual.AddComponent<SpriteAnimator>();
                var billboard = visual.AddComponent<BillboardSprite>();
                Set(billboard, so =>
                {
                    so.FindProperty("_spriteRenderer").objectReferenceValue = sr;
                    so.FindProperty("_facingSource").objectReferenceValue = enemy;
                });
                HeroSetup.AddFlash(visual, root.transform);
                var flash = visual.GetComponent<HitFlash>();
                var driver = visual.AddComponent<EnemyAnimationDriver>();
                Set(driver, so =>
                {
                    so.FindProperty("_enemy").objectReferenceValue = enemy;
                    so.FindProperty("_animator").objectReferenceValue = animator;
                    so.FindProperty("_billboard").objectReferenceValue = billboard;
                    so.FindProperty("_flash").objectReferenceValue = flash;
                    so.FindProperty("_idle").objectReferenceValue = clips["Idle"];
                    so.FindProperty("_run").objectReferenceValue = clips["Run"];
                    so.FindProperty("_jump").objectReferenceValue = clips["Jump"];
                    so.FindProperty("_fall").objectReferenceValue = clips["Fall"];
                    so.FindProperty("_attack").objectReferenceValue = clips["Attack1"];
                    so.FindProperty("_hurt").objectReferenceValue = clips["Hurt"];
                    so.FindProperty("_tumble").objectReferenceValue = clips["Tumble"];
                    so.FindProperty("_down").objectReferenceValue = clips["Down"];
                    so.FindProperty("_getUp").objectReferenceValue = clips["GetUp"];
                });

                AddHealthBar(root.transform, health, null, barBack, barFill, hideWhenFull: true);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                log.AppendLine($"  * rebuilt {PrefabPath}");
                return saved.GetComponent<EnemyController>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ============ Scene ============

        /// <summary>Gives the player Health and the Player team so enemies can hurt it (and not each other).</summary>
        private static void PreparePlayer(PlayerController player, StringBuilder log)
        {
            Health health = HeroSetup.GetOrAdd<Health>(player.gameObject);
            Set(health, so => so.FindProperty("_maxHealth").intValue = PlayerMaxHealth);
            Set(player, so => so.FindProperty("_health").objectReferenceValue = health);

            foreach (Hurtbox hurtbox in player.GetComponentsInChildren<Hurtbox>(true))
            {
                Set(hurtbox, so => so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Player);
            }

            foreach (Hitbox hitbox in player.GetComponentsInChildren<Hitbox>(true))
            {
                Set(hitbox, so => so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Player);
            }

            // The player's health lives on the screen HUD (Setup HUD), not over their head.
            Transform oldBar = player.transform.Find("HealthBar");
            if (oldBar != null)
            {
                Undo.DestroyObjectImmediate(oldBar.gameObject);
            }

            log.AppendLine($"  * player: Health {PlayerMaxHealth}, Player team on hurtbox/hitbox");
        }

        private static void BuildSpawner(PlayerController player, Transform cameraTransform, EnemyController prefab, StringBuilder log)
        {
            GameObject root = GameObject.Find("Enemies") ?? HeroSetup.CreateRoot("Enemies");
            var coordinator = HeroSetup.GetOrAdd<EnemyCoordinator>(root);
            Set(coordinator, so =>
            {
                so.FindProperty("_target").objectReferenceValue = player.transform;
                so.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            });

            var points = new List<Object>();
            for (int i = 0; i < SpawnPoints.Length; i++)
            {
                string name = $"SpawnPoint_{i + 1}";
                Transform point = root.transform.Find(name);
                if (point == null)
                {
                    point = new GameObject(name).transform;
                    Undo.RegisterCreatedObjectUndo(point.gameObject, "Create Spawn Point");
                    point.SetParent(root.transform, false);
                    point.position = SpawnPoints[i];
                }

                points.Add(point);
            }

            var spawner = HeroSetup.GetOrAdd<EnemySpawner>(root);
            Set(spawner, so =>
            {
                so.FindProperty("_prefab").objectReferenceValue = prefab;
                so.FindProperty("_coordinator").objectReferenceValue = coordinator;
                HeroSetup.SetArray(so.FindProperty("_spawnPoints"), points.ToArray());
            });

            log.AppendLine($"  * scene: 'Enemies' with coordinator (max 2 attackers) and a spawner keeping {SpawnPoints.Length} Shades alive");
        }

        // ============ Helpers ============

        /// <summary>Background + fill quads under a "HealthBar" child, driven by <see cref="WorldHealthBar"/>.</summary>
        internal static GameObject AddHealthBar(Transform parent, Health health, Transform cameraTransform, Material back, Material fill, bool hideWhenFull)
        {
            var bar = new GameObject("HealthBar");
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, BarHeight, 0f);

            GameObject backQuad = Quad("Back", bar.transform, back);
            backQuad.transform.localScale = new Vector3(BarSize.x + BarBorder * 2f, BarSize.y + BarBorder * 2f, 1f);
            GameObject fillQuad = Quad("Fill", bar.transform, fill);
            fillQuad.transform.localScale = new Vector3(BarSize.x, BarSize.y, 1f);
            fillQuad.transform.localPosition = new Vector3(0f, 0f, -0.01f); // in front of the background (quads face -Z)

            var component = bar.AddComponent<WorldHealthBar>();
            Set(component, so =>
            {
                so.FindProperty("_health").objectReferenceValue = health;
                so.FindProperty("_fill").objectReferenceValue = fillQuad.transform;
                so.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
                so.FindProperty("_hideWhenFull").boolValue = hideWhenFull;
            });
            return bar;
        }


        internal static GameObject Quad(string name, Transform parent, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>()); // never hit by raycasts or overlaps
            quad.transform.SetParent(parent, false);
            var mr = quad.GetComponent<MeshRenderer>();
            if (material != null)
            {
                mr.sharedMaterial = material;
            }

            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return quad;
        }

        private static Transform PlayerCamera(PlayerController player)
        {
            BillboardSprite billboard = player.GetComponentInChildren<BillboardSprite>(true);
            return billboard != null ? billboard.CameraTransform : null;
        }

        internal static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedProperties();
        }
    }
}
