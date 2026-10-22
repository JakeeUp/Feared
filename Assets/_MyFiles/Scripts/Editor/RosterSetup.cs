using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Enemy;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling: menu <c>Brawler/Build Enemy Roster</c>. Turns the enemy packs into ready-to-place prefabs:
    /// Burning Ghoul (rusher), Fire Worm and Church Wizard (casters), Angel (diving flyer), Evil Wizard (elite), the
    /// Executioner's minion (flyer) and the Undead Executioner boss.
    /// For each one it cuts the frames out of the sheets (strips, grids or frame folders), scales the art to a set
    /// height relative to the 1-unit hero and anchors every clip on the feet, then builds the clips, the attack and
    /// settings assets (created once, then left for tuning), the projectile prefabs and the enemy prefab.
    /// </summary>
    /// <remarks>Run Setup Everything first (the player, materials and shared assets come from there). Safe to re-run.</remarks>
    public static class RosterSetup
    {
        // ============ Paths ============

        private const string ThirdParty = "Assets/_ThirdParty";
        private const string Church = ThirdParty + "/gothicvania church files/Assets/SPRITES";
        private const string Exec = ThirdParty + "/Undead executioner puppet/png";
        private const string Generated = ThirdParty + "/_Generated/Enemies";
        private const string Root = "Assets/_MyFiles";
        private const string DataDir = Root + "/Data/Enemy";
        private const string PrefabDir = Root + "/Prefabs";
        private const string CombatSettingsPath = Root + "/Data/Combat/CombatSettings.asset";
        private const string EventBusPath = Root + "/Data/Core/EventBus.asset";
        private const string SpriteMaterialPath = Root + "/Art/Materials/M_SpriteAlphaClip.mat";
        private const string ShadowMaterialPath = Root + "/Art/Materials/M_BlobShadow.mat";
        private const string BarBackMaterialPath = Root + "/Art/Materials/M_HealthBar_Back.mat";
        private const string BarFillMaterialPath = Root + "/Art/Materials/M_HealthBar_Fill.mat";

        /// <summary>Alpha at or above which a pixel counts as part of the art.</summary>
        private const byte SolidAlpha = 40;

        // ============ Definitions ============

        /// <summary>Where a clip's frames come from: cells of a sheet (row-major), or every PNG in a folder.</summary>
        private sealed class FrameSource
        {
            public string Path;
            public int CellWidth;
            public int CellHeight;
            public int First;
            public int Count; // 0 = every non-empty cell from First
            public bool IsFolder;

            public static FrameSource Strip(string path, int cell, int first = 0, int count = 0) =>
                new FrameSource { Path = path, CellWidth = cell, CellHeight = cell, First = first, Count = count };

            public static FrameSource Grid(string path, int cellWidth, int cellHeight, int first = 0, int count = 0) =>
                new FrameSource { Path = path, CellWidth = cellWidth, CellHeight = cellHeight, First = first, Count = count };

            public static FrameSource Folder(string path, int first = 0, int count = 0) =>
                new FrameSource { Path = path, IsFolder = true, First = first, Count = count };
        }

        private sealed class ClipDef
        {
            public string Role;
            public FrameSource Source;
            public float Fps = 10f;
            public bool Loop = true;

            public ClipDef(string role, FrameSource source, float fps = 10f, bool loop = true)
            {
                Role = role;
                Source = source;
                Fps = fps;
                Loop = loop;
            }
        }

        private sealed class AttackDef
        {
            public int Damage;
            public Vector2 Knockback;
            public int Startup;
            public int Active;
            public int Recovery;
            public float Lunge;
            public Vector2 HitboxSize;
            public Vector2 HitboxOffset;
            public int Hitstun = 16;
            public int HitStop = 4;
            public float Shake = 0.15f;
        }

        private sealed class ProjectileDef
        {
            public FrameSource Frames;
            public float Fps = 14f;
            public float WorldWidth = 0.5f;
            public float Speed = 7f;
            public AttackDef Attack;
        }

        private sealed class EnemyDef
        {
            public string Name;
            public EnemyBehaviour Behaviour;
            public float Height;    // world height of the idle art (the hero is 1)
            public bool Mirrored;   // art faces left
            public ClipDef[] Clips;
            public int AttackHitFrame;
            public AttackDef Attack;
            public ProjectileDef Projectile;
            public int Health = 40;
            public float MoveSpeed = 2.4f;
            public float AttackRange = 1.2f;
            public float LaneTolerance = 0.3f;
            public float WaitDistance = 2.6f;
            public float PreferredRange = 5f;
            public float HoverHeight = 1.6f;
            public float AggroRange = 9f;
            public float AttackCooldown = 1.2f;
            public float KnockbackScale = 1f;
            public int Xp = 15;
            public ChainWeight ChainWeight = ChainWeight.Light;
        }

        private static readonly EnemyDef[] Roster =
        {
            new EnemyDef
            {
                Name = "Ghoul", Behaviour = EnemyBehaviour.Melee, Height = 0.95f, Mirrored = true,
                Clips = new[]
                {
                    new ClipDef("Idle", FrameSource.Folder(Church + "/burning-ghoul/run 1/sprites", 0, 2), 4f),
                    new ClipDef("Run", FrameSource.Folder(Church + "/burning-ghoul/run 1/sprites"), 14f),
                    new ClipDef("Attack", FrameSource.Folder(Church + "/burning-ghoul/run 2/sprites"), 16f, false),
                },
                // The rusher: a long, fast charge after a short tell. Low health, so it dies to a punish.
                Attack = new AttackDef
                {
                    Damage = 7, Knockback = new Vector2(4.5f, 0f), Startup = 16, Active = 14, Recovery = 20, Lunge = 9f,
                    HitboxSize = new Vector2(0.7f, 0.8f), HitboxOffset = new Vector2(0.35f, 0.5f), HitStop = 4, Shake = 0.2f,
                },
                Health = 30, MoveSpeed = 4.2f, AttackRange = 3f, WaitDistance = 4f, AttackCooldown = 1.6f, Xp = 12,
            },
            new EnemyDef
            {
                Name = "FireWorm", Behaviour = EnemyBehaviour.Ranged, Height = 0.75f,
                Clips = new[]
                {
                    new ClipDef("Idle", FrameSource.Strip(ThirdParty + "/Fire Worm/Sprites/Worm/Idle.png", 90), 9f),
                    new ClipDef("Run", FrameSource.Strip(ThirdParty + "/Fire Worm/Sprites/Worm/Walk.png", 90), 9f),
                    new ClipDef("Attack", FrameSource.Strip(ThirdParty + "/Fire Worm/Sprites/Worm/Attack.png", 90), 16f, false),
                    new ClipDef("Hurt", FrameSource.Strip(ThirdParty + "/Fire Worm/Sprites/Worm/Get Hit.png", 90), 12f, false),
                    new ClipDef("Death", FrameSource.Strip(ThirdParty + "/Fire Worm/Sprites/Worm/Death.png", 90), 10f, false),
                },
                AttackHitFrame = 11,
                Attack = new AttackDef { Startup = 34, Active = 2, Recovery = 16 },
                Projectile = new ProjectileDef
                {
                    Frames = FrameSource.Strip(ThirdParty + "/Fire Worm/Sprites/Fire Ball/Move.png", 46), WorldWidth = 0.55f, Speed = 6.5f,
                    Attack = new AttackDef { Damage = 9, Knockback = new Vector2(3f, 0f), Hitstun = 16, HitStop = 4, Shake = 0.15f },
                },
                Health = 40, MoveSpeed = 1.6f, AttackRange = 8f, LaneTolerance = 0.9f, PreferredRange = 5.5f, WaitDistance = 6.5f,
                AttackCooldown = 2.2f, Xp = 15,
            },
            new EnemyDef
            {
                Name = "Wizard", Behaviour = EnemyBehaviour.Ranged, Height = 1.05f, Mirrored = true,
                Clips = new[]
                {
                    new ClipDef("Idle", FrameSource.Folder(Church + "/wizard/Idle/sprites"), 8f),
                    new ClipDef("Attack", FrameSource.Folder(Church + "/wizard/Fire/sprites"), 10f, false),
                },
                AttackHitFrame = 6,
                Attack = new AttackDef { Startup = 30, Active = 2, Recovery = 18 },
                Projectile = new ProjectileDef
                {
                    Frames = FrameSource.Folder(Church + "/fx/fireball/fireball-sprites"), WorldWidth = 0.45f, Speed = 7.5f,
                    Attack = new AttackDef { Damage = 10, Knockback = new Vector2(3.5f, 0f), Hitstun = 18, HitStop = 5, Shake = 0.2f },
                },
                Health = 45, MoveSpeed = 1.2f, AttackRange = 9f, LaneTolerance = 1f, PreferredRange = 6.5f, WaitDistance = 7.5f,
                AttackCooldown = 2.6f, Xp = 18,
            },
            new EnemyDef
            {
                Name = "Angel", Behaviour = EnemyBehaviour.Flyer, Height = 1.2f,
                Clips = new[]
                {
                    new ClipDef("Idle", FrameSource.Folder(Church + "/angel/idle/sprites"), 10f),
                    new ClipDef("Attack", FrameSource.Folder(Church + "/angel/attack/sprites"), 10f, false),
                },
                // Dives at where the player stood when the wings flared: keep moving and it misses.
                Attack = new AttackDef
                {
                    Damage = 8, Knockback = new Vector2(3.5f, 0f), Startup = 22, Active = 16, Recovery = 26, Lunge = 9f,
                    HitboxSize = new Vector2(0.7f, 0.8f), HitboxOffset = new Vector2(0.15f, 0.45f), HitStop = 4, Shake = 0.2f,
                },
                Health = 30, MoveSpeed = 3f, AttackRange = 3.4f, LaneTolerance = 0.5f, HoverHeight = 1.6f, WaitDistance = 4.5f,
                AttackCooldown = 2f, Xp = 16,
            },
            new EnemyDef
            {
                Name = "EvilWizard", Behaviour = EnemyBehaviour.Melee, Height = 1.4f,
                Clips = new[]
                {
                    new ClipDef("Idle", FrameSource.Strip(ThirdParty + "/Evil Wizard/Sprites/Idle.png", 150), 10f),
                    new ClipDef("Run", FrameSource.Strip(ThirdParty + "/Evil Wizard/Sprites/Move.png", 150), 10f),
                    new ClipDef("Attack", FrameSource.Strip(ThirdParty + "/Evil Wizard/Sprites/Attack.png", 150), 14f, false),
                    new ClipDef("Hurt", FrameSource.Strip(ThirdParty + "/Evil Wizard/Sprites/Take Hit.png", 150), 12f, false),
                    new ClipDef("Death", FrameSource.Strip(ThirdParty + "/Evil Wizard/Sprites/Death.png", 150), 8f, false),
                },
                // The elite: a flamethrower that covers a long stretch in front. Shrugs off knockback.
                AttackHitFrame = 2,
                Attack = new AttackDef
                {
                    Damage = 14, Knockback = new Vector2(5f, 0.5f), Startup = 24, Active = 14, Recovery = 28, Lunge = 0.5f,
                    HitboxSize = new Vector2(2.1f, 0.6f), HitboxOffset = new Vector2(1.25f, 0.65f), Hitstun = 20, HitStop = 6, Shake = 0.3f,
                },
                Health = 160, MoveSpeed = 1.8f, AttackRange = 2.4f, LaneTolerance = 0.45f, WaitDistance = 3.5f,
                AttackCooldown = 1.8f, KnockbackScale = 0.3f, Xp = 60, ChainWeight = ChainWeight.Heavy,
            },
            new EnemyDef
            {
                Name = "ExecMinion", Behaviour = EnemyBehaviour.Flyer, Height = 0.55f,
                Clips = new[]
                {
                    new ClipDef("Idle", FrameSource.Strip(Exec + "/summonIdle.png", 50), 8f),
                    new ClipDef("Attack", FrameSource.Strip(Exec + "/summonIdle.png", 50), 12f, false),
                    new ClipDef("Death", FrameSource.Grid(Exec + "/summonDeath.png", 50, 50), 12f, false),
                },
                Attack = new AttackDef
                {
                    Damage = 6, Knockback = new Vector2(3f, 0f), Startup = 26, Active = 12, Recovery = 22, Lunge = 7f,
                    HitboxSize = new Vector2(0.45f, 0.45f), HitboxOffset = new Vector2(0.1f, 0.25f), HitStop = 3, Shake = 0.1f,
                },
                Health = 12, MoveSpeed = 2.6f, AttackRange = 2.4f, LaneTolerance = 0.5f, HoverHeight = 0.9f, WaitDistance = 3.5f,
                AttackCooldown = 2.4f, Xp = 4,
            },
        };

        // Boss art (100 px cells).
        private static readonly ClipDef[] BossClips =
        {
            new ClipDef("Idle", FrameSource.Grid(Exec + "/idle2.png", 100, 100), 8f),
            new ClipDef("Swipe", FrameSource.Grid(Exec + "/attacking.png", 100, 100), 12f, false),
            new ClipDef("Dash", FrameSource.Grid(Exec + "/skill1.png", 100, 100), 12f, false),
            new ClipDef("Summon", FrameSource.Grid(Exec + "/summon.png", 100, 100), 8f, false),
            new ClipDef("Death", FrameSource.Grid(Exec + "/death.png", 100, 100), 10f, false),
        };

        private const float BossHeight = 3.2f;

        // Hit volumes were tuned at 1.8 units tall; everything spatial scales with the art.
        private const float BossScale = BossHeight / 1.8f;

        // ============ Menu ============

        [MenuItem("Brawler/Build Enemy Roster (Ghoul, Worm, Wizard, Angel, Elite, Boss)")]
        public static void Run()
        {
            var log = new StringBuilder("[Brawler] Build Enemy Roster\n");
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(PrefabDir);

            var player = Object.FindAnyObjectByType<PlayerController>();
            int hurtboxLayer = player != null ? player.GetComponentInChildren<Hurtbox>(true)?.gameObject.layer ?? 0 : 0;

            foreach (EnemyDef def in Roster)
            {
                if (!SourcesExist(def.Clips))
                {
                    log.AppendLine($"  ! {def.Name}: art not found, skipped");
                    continue;
                }

                BuildEnemy(def, hurtboxLayer, log);
            }

            if (SourcesExist(BossClips))
            {
                BuildBoss(hurtboxLayer, log);
            }
            else
            {
                log.AppendLine("  ! boss: Undead Executioner art not found, skipped");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        /// <summary>The roster prefab for <paramref name="name"/> (e.g. "Ghoul"), or null if it hasn't been built.</summary>
        internal static EnemyController LoadEnemy(string name)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy_{name}.prefab");
            return go != null ? go.GetComponent<EnemyController>() : null;
        }

        /// <summary>The boss prefab, or null if it hasn't been built.</summary>
        internal static BossController LoadBoss()
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Boss_Executioner.prefab");
            return go != null ? go.GetComponent<BossController>() : null;
        }

        // ============ Enemies ============

        private static void BuildEnemy(EnemyDef def, int hurtboxLayer, StringBuilder log)
        {
            Dictionary<string, SpriteClip> clips = BuildClips(def.Name, def.Clips, def.Height, def.Mirrored, out float ppu);
            AttackData attack = EnsureAttack($"{DataDir}/Attack_{def.Name}.asset", def.Attack);
            EnemySettings settings = EnsureSettings(def, attack);

            Projectile projectile = null;
            AttackData projectileAttack = null;
            if (def.Projectile != null)
            {
                projectileAttack = EnsureAttack($"{DataDir}/Attack_{def.Name}_Shot.asset", def.Projectile.Attack);
                projectile = BuildProjectile(def, log);
            }

            BuildEnemyPrefab(def, settings, clips, hurtboxLayer, projectile, projectileAttack);
            log.AppendLine($"  * {def.Name}: {clips.Count} clips at {ppu:0.#} px/unit, {def.Behaviour}, prefab Enemy_{def.Name}");
        }

        private static void BuildEnemyPrefab(EnemyDef def, EnemySettings settings, Dictionary<string, SpriteClip> clips,
                                             int hurtboxLayer, Projectile projectile, AttackData projectileAttack)
        {
            var root = new GameObject($"Enemy_{def.Name}");
            try
            {
                float height = Mathf.Max(0.5f, def.Height * 0.9f);
                var cc = root.AddComponent<CharacterController>();
                cc.height = height;
                cc.radius = Mathf.Clamp(0.22f * def.Height, 0.16f, 0.4f);
                cc.center = new Vector3(0f, height * 0.5f, 0f);
                cc.stepOffset = Mathf.Min(height * 0.25f, cc.height * 0.4f);
                cc.slopeLimit = 45f;

                var health = root.AddComponent<Health>();
                var enemy = root.AddComponent<EnemyController>();
                Hurtbox hurtbox = AddHurtbox(root.transform, enemy, hurtboxLayer, new Vector3(0.5f * def.Height, 0.95f * def.Height, 0.5f), def.Height * 0.5f);
                Hitbox hitbox = AddHitbox(root.transform, hurtbox);

                ProjectileLauncher launcher = null;
                if (projectile != null)
                {
                    launcher = root.AddComponent<ProjectileLauncher>();
                    EditorUtil.Set(launcher, so =>
                    {
                        so.FindProperty("_prefab").objectReferenceValue = projectile;
                        so.FindProperty("_attack").objectReferenceValue = projectileAttack;
                        so.FindProperty("_muzzleOffset").vector3Value = new Vector3(0.4f * def.Height, 0.5f * def.Height, 0f);
                        so.FindProperty("_speed").floatValue = def.Projectile.Speed;
                        so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Enemy;
                        so.FindProperty("_ownerHurtbox").objectReferenceValue = hurtbox;
                    });
                }

                EditorUtil.Set(enemy, so =>
                {
                    so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
                    so.FindProperty("_settings").objectReferenceValue = settings;
                    so.FindProperty("_characterController").objectReferenceValue = cc;
                    so.FindProperty("_health").objectReferenceValue = health;
                    so.FindProperty("_hitbox").objectReferenceValue = hitbox;
                    so.FindProperty("_launcher").objectReferenceValue = launcher;
                });

                AddShadow(root, def.Height * 0.5f);
                GameObject visual = AddVisual(root.transform, clips["Idle"].GetFrame(0), enemy, out SpriteAnimator animator, out BillboardSprite billboard, out HitFlash flash);

                var driver = visual.AddComponent<EnemyAnimationDriver>();
                EditorUtil.Set(driver, so =>
                {
                    so.FindProperty("_enemy").objectReferenceValue = enemy;
                    so.FindProperty("_animator").objectReferenceValue = animator;
                    so.FindProperty("_billboard").objectReferenceValue = billboard;
                    so.FindProperty("_flash").objectReferenceValue = flash;
                    so.FindProperty("_idle").objectReferenceValue = clips["Idle"];
                    so.FindProperty("_run").objectReferenceValue = Clip(clips, "Run") ?? clips["Idle"];
                    so.FindProperty("_attack").objectReferenceValue = Clip(clips, "Attack");
                    so.FindProperty("_attackHitFrame").intValue = def.AttackHitFrame;
                    so.FindProperty("_hurt").objectReferenceValue = Clip(clips, "Hurt") ?? clips["Idle"];
                    so.FindProperty("_death").objectReferenceValue = Clip(clips, "Death");
                });

                GameObject bar = EnemySetup.AddHealthBar(root.transform, health, null,
                    AssetDatabase.LoadAssetAtPath<Material>(BarBackMaterialPath),
                    AssetDatabase.LoadAssetAtPath<Material>(BarFillMaterialPath), hideWhenFull: true);
                bar.transform.localPosition = new Vector3(0f, def.Height * 1.15f + 0.1f, 0f);

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Enemy_{def.Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static EnemySettings EnsureSettings(EnemyDef def, AttackData attack)
        {
            string path = $"{DataDir}/EnemySettings_{def.Name}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<EnemySettings>(path);
            bool created = settings == null;
            if (created)
            {
                settings = ScriptableObject.CreateInstance<EnemySettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            EditorUtil.Set(settings, so =>
            {
                so.FindProperty("_attack").objectReferenceValue = attack;
                so.FindProperty("_behaviour").enumValueIndex = (int)def.Behaviour;
                so.FindProperty("_chainWeight").enumValueIndex = (int)def.ChainWeight;
                if (!created)
                {
                    return;
                }

                so.FindProperty("_maxHealth").intValue = def.Health;
                so.FindProperty("_moveSpeed").floatValue = def.MoveSpeed;
                so.FindProperty("_attackRange").floatValue = def.AttackRange;
                so.FindProperty("_laneTolerance").floatValue = def.LaneTolerance;
                so.FindProperty("_waitDistance").floatValue = Mathf.Max(def.WaitDistance, def.AttackRange);
                so.FindProperty("_preferredRange").floatValue = def.PreferredRange;
                so.FindProperty("_maxShotHeightDifference").floatValue = 4f; // shots aim at the chest, across platforms
                so.FindProperty("_hoverHeight").floatValue = def.HoverHeight;
                so.FindProperty("_aggroRange").floatValue = def.AggroRange;
                so.FindProperty("_loseRange").floatValue = def.AggroRange + 6f;
                so.FindProperty("_attackCooldown").floatValue = def.AttackCooldown;
                so.FindProperty("_knockbackScale").floatValue = def.KnockbackScale;
                so.FindProperty("_xpReward").intValue = def.Xp;
            });
            return settings;
        }

        // ============ Projectiles ============

        private static Projectile BuildProjectile(EnemyDef def, StringBuilder log)
        {
            ProjectileDef p = def.Projectile;
            List<Frame> frames = LoadFrames(p.Frames);
            if (frames.Count == 0)
            {
                log.AppendLine($"  ! {def.Name}: projectile art missing");
                return null;
            }

            float ppu = frames[0].Width / p.WorldWidth;
            string dir = $"{Generated}/{def.Name}";
            List<string> paths = WriteFrames(dir, "Shot", frames);
            Sprite[] sprites = SpriteImport.ImportFrames(paths, ppu, new Vector2(0.5f, 0.5f));
            SpriteClip clip = SpriteImport.SaveClip(def.Name, "Shot", sprites, p.Fps, true, false);

            var root = new GameObject($"Projectile_{def.Name}");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
                sr.sprite = sprites[0];
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
                var animator = visual.AddComponent<SpriteAnimator>();
                EditorUtil.Set(animator, so =>
                {
                    so.FindProperty("_renderer").objectReferenceValue = sr;
                    so.FindProperty("_defaultClip").objectReferenceValue = clip;
                });

                var projectile = root.AddComponent<Projectile>();
                EditorUtil.Set(projectile, so =>
                {
                    so.FindProperty("_combatSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatSettings>(CombatSettingsPath);
                    so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
                    so.FindProperty("_radius").floatValue = p.WorldWidth * 0.45f;
                    so.FindProperty("_sprite").objectReferenceValue = sr;
                });

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Projectile_{def.Name}.prefab");
                return saved.GetComponent<Projectile>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ============ Boss ============

        private static void BuildBoss(int hurtboxLayer, StringBuilder log)
        {
            Dictionary<string, SpriteClip> clips = BuildClips("Executioner", BossClips, BossHeight, false, out float ppu);
            AttackData swipe = EnsureAttack($"{DataDir}/Attack_Boss_Swipe.asset", new AttackDef
            {
                Damage = 16, Knockback = new Vector2(6f, 1f), Startup = 24, Active = 6, Recovery = 26,
                HitboxSize = new Vector2(2.2f, 1.4f) * BossScale, HitboxOffset = new Vector2(1f, 0.8f) * BossScale, Hitstun = 22, HitStop = 7, Shake = 0.35f,
            });
            AttackData dash = EnsureAttack($"{DataDir}/Attack_Boss_Dash.asset", new AttackDef
            {
                Damage = 18, Knockback = new Vector2(8f, 0.5f), Startup = 1, Active = 1, Recovery = 1,
                HitboxSize = new Vector2(1.3f, 1.05f) * BossScale, HitboxOffset = new Vector2(0.3f, 0.52f) * BossScale, Hitstun = 24, HitStop = 6, Shake = 0.5f,
            });
            EnemyController minion = LoadEnemy("ExecMinion");

            var root = new GameObject("Boss_Executioner");
            try
            {
                var health = root.AddComponent<Health>();
                var boss = root.AddComponent<BossController>();
                Hurtbox hurtbox = AddHurtbox(root.transform, boss, hurtboxLayer, new Vector3(0.8f * BossScale, 1.6f * BossScale, 0.6f), 0.85f * BossScale);
                Hitbox hitbox = AddHitbox(root.transform, hurtbox);

                EditorUtil.Set(boss, so =>
                {
                    so.FindProperty("_health").objectReferenceValue = health;
                    so.FindProperty("_hitbox").objectReferenceValue = hitbox;
                    so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
                    so.FindProperty("_swipe").objectReferenceValue = swipe;
                    so.FindProperty("_dash").objectReferenceValue = dash;
                    so.FindProperty("_minionPrefab").objectReferenceValue = minion;
                    so.FindProperty("_swipeRange").floatValue = 2.2f * BossScale;
                });

                AddShadow(root, BossScale);
                GameObject visual = AddVisual(root.transform, clips["Idle"].GetFrame(0), boss, out SpriteAnimator animator, out BillboardSprite billboard, out HitFlash flash);
                var driver = visual.AddComponent<BossAnimationDriver>();
                EditorUtil.Set(driver, so =>
                {
                    so.FindProperty("_boss").objectReferenceValue = boss;
                    so.FindProperty("_animator").objectReferenceValue = animator;
                    so.FindProperty("_billboard").objectReferenceValue = billboard;
                    so.FindProperty("_flash").objectReferenceValue = flash;
                    so.FindProperty("_idle").objectReferenceValue = clips["Idle"];
                    so.FindProperty("_swipe").objectReferenceValue = clips["Swipe"];
                    so.FindProperty("_dash").objectReferenceValue = clips["Dash"];
                    so.FindProperty("_summon").objectReferenceValue = clips["Summon"];
                    so.FindProperty("_death").objectReferenceValue = clips["Death"];
                });

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Boss_Executioner.prefab");
                log.AppendLine($"  * boss: Undead Executioner at {ppu:0.#} px/unit ({clips["Death"].FrameCount}-frame death), prefab Boss_Executioner"
                               + (minion == null ? " (no minion prefab!)" : string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ============ Prefab Parts ============

        private static Hurtbox AddHurtbox(Transform root, MonoBehaviour owner, int layer, Vector3 size, float centerY)
        {
            var go = new GameObject("Hurtbox") { layer = layer };
            go.transform.SetParent(root, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, centerY, 0f);
            var hurtbox = go.AddComponent<Hurtbox>();
            EditorUtil.Set(hurtbox, so =>
            {
                so.FindProperty("_collider").objectReferenceValue = box;
                so.FindProperty("_owner").objectReferenceValue = owner;
                so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Enemy;
            });
            return hurtbox;
        }

        private static Hitbox AddHitbox(Transform root, Hurtbox ownHurtbox)
        {
            var go = new GameObject("Hitbox");
            go.transform.SetParent(root, false);
            var hitbox = go.AddComponent<Hitbox>();
            EditorUtil.Set(hitbox, so =>
            {
                so.FindProperty("_combatSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatSettings>(CombatSettingsPath);
                so.FindProperty("_eventBus").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
                so.FindProperty("_owner").objectReferenceValue = root;
                so.FindProperty("_ownerHurtbox").objectReferenceValue = ownHurtbox;
                so.FindProperty("_team").enumValueIndex = (int)CombatTeam.Enemy;
            });
            return hitbox;
        }

        private static void AddShadow(GameObject root, float scale)
        {
            GameObject shadow = EnemySetup.Quad("Shadow", root.transform, AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath));
            shadow.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = Vector3.one * scale;
            var blob = root.AddComponent<BlobShadow>();
            EditorUtil.Set(blob, so =>
            {
                so.FindProperty("_shadowTransform").objectReferenceValue = shadow.transform;
                so.FindProperty("_baseScale").floatValue = scale;
            });
        }

        private static GameObject AddVisual(Transform root, Sprite first, MonoBehaviour facingSource,
                                            out SpriteAnimator animator, out BillboardSprite billboard, out HitFlash flash)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            sr.sprite = first;
            sr.shadowCastingMode = ShadowCastingMode.On;
            sr.receiveShadows = false;
            animator = visual.AddComponent<SpriteAnimator>();
            billboard = visual.AddComponent<BillboardSprite>();
            BillboardSprite b = billboard;
            EditorUtil.Set(b, so =>
            {
                so.FindProperty("_spriteRenderer").objectReferenceValue = sr;
                so.FindProperty("_facingSource").objectReferenceValue = facingSource;
            });
            HeroSetup.AddFlash(visual, root);
            flash = visual.GetComponent<HitFlash>();
            return visual;
        }

        // ============ Assets ============

        private static AttackData EnsureAttack(string path, AttackDef def)
        {
            return AttackAssets.Ensure(path, new AttackAssets.Spec
            {
                Damage = def.Damage,
                Knockback = def.Knockback,
                Hitstun = def.Hitstun,
                Startup = Mathf.Max(1, def.Startup),
                Active = Mathf.Max(1, def.Active),
                Recovery = Mathf.Max(1, def.Recovery),
                Lunge = def.Lunge,
                HitboxSize = def.HitboxSize,
                HitboxOffset = def.HitboxOffset,
                HitStop = def.HitStop,
                Shake = def.Shake,
            });
        }

        private static SpriteClip Clip(Dictionary<string, SpriteClip> clips, string role)
        {
            return clips.TryGetValue(role, out SpriteClip clip) ? clip : null;
        }

        // ============ Frames ============

        private struct Frame
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;
        }

        /// <summary>
        /// Cuts every clip's frames, scales the art so the idle pose is <paramref name="height"/> units tall, anchors
        /// each clip on the feet (clips drawn on the idle's canvas size share its anchor so the body never jumps), and
        /// saves the clips.
        /// </summary>
        private static Dictionary<string, SpriteClip> BuildClips(string name, ClipDef[] defs, float height, bool mirrored, out float ppu)
        {
            var frames = new Dictionary<string, List<Frame>>();
            foreach (ClipDef def in defs)
            {
                frames[def.Role] = LoadFrames(def.Source);
            }

            List<Frame> idle = frames["Idle"];
            RectInt idleBounds = Bounds(idle[0]);
            ppu = Mathf.Max(1f, idleBounds.height / height);
            Vector2Int idlePivot = ClipPivot(idle);

            var clips = new Dictionary<string, SpriteClip>();
            string dir = $"{Generated}/{name}";
            foreach (ClipDef def in defs)
            {
                List<Frame> clipFrames = frames[def.Role];
                if (clipFrames.Count == 0)
                {
                    continue;
                }

                bool sameCanvas = clipFrames[0].Width == idle[0].Width && clipFrames[0].Height == idle[0].Height;
                Vector2Int pivot = sameCanvas ? idlePivot : ClipPivot(clipFrames);
                Vector2 normalized = new Vector2(pivot.x / (float)clipFrames[0].Width, pivot.y / (float)clipFrames[0].Height);

                List<string> paths = WriteFrames(dir, def.Role, clipFrames);
                Sprite[] sprites = SpriteImport.ImportFrames(paths, ppu, normalized);
                clips[def.Role] = SpriteImport.SaveClip(name, def.Role, sprites, def.Fps, def.Loop, mirrored);
            }

            return clips;
        }

        private static bool SourcesExist(ClipDef[] defs)
        {
            return defs.All(d => d.Source.IsFolder ? Directory.Exists(d.Source.Path) : File.Exists(d.Source.Path));
        }

        private static List<Frame> LoadFrames(FrameSource source)
        {
            var frames = new List<Frame>();
            if (source.IsFolder)
            {
                if (!Directory.Exists(source.Path))
                {
                    return frames;
                }

                IEnumerable<string> files = Directory.GetFiles(source.Path, "*.png")
                    .Select(p => p.Replace('\\', '/'))
                    .OrderBy(p => int.Parse(Regex.Match(Path.GetFileNameWithoutExtension(p), @"(\d+)$").Value))
                    .Skip(source.First);
                if (source.Count > 0)
                {
                    files = files.Take(source.Count);
                }

                foreach (string file in files)
                {
                    Color32[] px = SpriteImport.LoadPixels(file, out int w, out int h);
                    frames.Add(new Frame { Pixels = px, Width = w, Height = h });
                }

                return frames;
            }

            if (!File.Exists(source.Path))
            {
                return frames;
            }

            Color32[] sheet = SpriteImport.LoadPixels(source.Path, out int sw, out int sh);
            int columns = Mathf.Max(1, sw / source.CellWidth);
            int rows = Mathf.Max(1, sh / source.CellHeight);
            int cells = columns * rows;
            int last = source.Count > 0 ? Mathf.Min(cells, source.First + source.Count) : cells;
            for (int i = source.First; i < last; i++)
            {
                var rect = new RectInt(i % columns * source.CellWidth, i / columns * source.CellHeight, source.CellWidth, source.CellHeight);
                Color32[] px = SpriteImport.Crop(sheet, sw, sh, rect);
                var frame = new Frame { Pixels = px, Width = rect.width, Height = rect.height };
                if (source.Count > 0 || Bounds(frame).width > 0)
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        private static List<string> WriteFrames(string dir, string role, List<Frame> frames)
        {
            Directory.CreateDirectory(dir);
            var paths = new List<string>();
            for (int i = 0; i < frames.Count; i++)
            {
                string path = $"{dir}/{role}_{i:00}.png";
                SpriteImport.SavePng(path, frames[i].Pixels, frames[i].Width, frames[i].Height);
                paths.Add(path);
            }

            return paths;
        }

        /// <summary>Median horizontal centre of the art across the clip, and its lowest solid row (feet). Pixels, bottom-up.</summary>
        private static Vector2Int ClipPivot(List<Frame> frames)
        {
            var centres = new List<float>();
            int feet = int.MaxValue;
            foreach (Frame frame in frames)
            {
                RectInt b = Bounds(frame);
                if (b.width == 0)
                {
                    continue;
                }

                centres.Add(b.x + b.width * 0.5f);
                feet = Mathf.Min(feet, b.y);
            }

            if (centres.Count == 0)
            {
                return new Vector2Int(frames[0].Width / 2, 0);
            }

            centres.Sort();
            return new Vector2Int(Mathf.RoundToInt(centres[centres.Count / 2]), feet);
        }

        private static RectInt Bounds(Frame frame)
        {
            return SpriteImport.Bounds(frame.Pixels, frame.Width, frame.Height, SolidAlpha);
        }
    }
}
