using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Brawler.Combat;
using Brawler.Enemy;
using Brawler.Level;
using Brawler.Player;
using Brawler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    public static partial class CathedralBuilder
    {
        private const string Root = "Assets/_MyFiles";
        private const string TestArenaPath = Root + "/Scenes/TestArena.unity";
        private const string ScenePath = Root + "/Scenes/Cathedral.unity";
        private const string LevelGenerated = "Assets/_ThirdParty/_Generated/Level";
        private const string SpriteMaterialPath = Root + "/Art/Materials/M_SpriteAlphaClip.mat";
        private const string RootName = "Cathedral";

        private static readonly string[] TestArenaOnly =
        {
            "Arena", "Backdrop", "Church", "Dummy_A", "Dummy_B", "Level_Vertical", "Pots",
            "Prop_Crate_Back", "Prop_Crate_Front", "Prop_Pillar", "Prop_Wall_Back",
        };

        private sealed class Ctx
        {
            public Transform Root;
            public Transform Geometry;
            public Transform Volumes;
            public Transform Decor;
            public Transform Foreground;
            public Transform Anchors;
            public Transform Lights;
            public Dictionary<string, Material> Stone;
            public Material SpriteMaterial;
            public Material GlowMaterial;
            public Sprite AnchorRing;
            public SpriteClip CheckpointClip;
            public SpriteClip MothsClip;
            public Dictionary<string, Sprite> Panels;
            public EnemyCoordinator Coordinator;
            public Transform CameraTransform;
            public CanvasGroup ScreenFade;
            public PlayerController Player;
            public readonly Dictionary<string, EnemyController> Enemies = new Dictionary<string, EnemyController>();
            public readonly Dictionary<string, Material> StonePairs = new Dictionary<string, Material>();
            public int Count;
        }

        [MenuItem("Brawler/Build Cathedral Level")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!File.Exists(ScenePath))
            {
                if (!File.Exists(TestArenaPath) || !AssetDatabase.CopyAsset(TestArenaPath, ScenePath))
                {
                    EditorUtility.DisplayDialog("Build Cathedral", $"Couldn't copy {TestArenaPath}. Open it and run Setup Everything first.", "OK");
                    return;
                }
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Build Cathedral", "The Cathedral scene has no player. Run Setup Everything in TestArena, delete Cathedral.unity and try again.", "OK");
                return;
            }

            var log = new StringBuilder("[Brawler] Build Cathedral Level\n");
            if (RosterSetup.LoadEnemy("Ghoul") == null || RosterSetup.LoadBoss() == null)
            {
                RosterSetup.Run();
            }

            StripTestArena(log);
            HeroSetup.WireInput(player, log);
            HeroSetup.WireDirectionalAttacks(player, log);
            Ctx ctx = Prepare(player, log);

            BuildGrounds(ctx);
            BuildNave(ctx);
            BuildGallery(ctx);
            BuildStairTower(ctx);
            BuildBelfry(ctx);
            BuildChoir(ctx);
            BuildSanctum(ctx);
            BuildCameraZones(ctx);
            log.AppendLine($"  * level: {ctx.Count} pieces, six rooms climbing from the nave (y 0) to the sanctum (y {TopY})");

            PlacePlayer(player, GroundsStart);
            PackSetup.Run();
            AddToBuildSettings();
            LookSetup.ApplyHd2dLook(log);
            ReadabilitySetup.Apply(log);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            log.AppendLine($"  * saved {ScenePath} (first in Build Settings). Press Play.");
            Debug.Log(log.ToString());
        }

        internal static bool IsCathedralScene()
        {
            return GameObject.Find(RootName) != null;
        }

        private static void StripTestArena(StringBuilder log)
        {
            int removed = 0;
            foreach (string name in TestArenaOnly)
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }

            GameObject enemies = GameObject.Find("Enemies");
            if (enemies != null)
            {
                foreach (EnemySpawner spawner in enemies.GetComponents<EnemySpawner>())
                {
                    Undo.DestroyObjectImmediate(spawner);
                }

                foreach (Transform child in enemies.transform.Cast<Transform>().ToList())
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            log.AppendLine($"  * removed {removed} test-arena objects and the endless spawner");
        }

        private static Ctx Prepare(PlayerController player, StringBuilder log)
        {
            var ctx = new Ctx
            {
                Player = player,
                Root = HeroSetup.CreateRoot(RootName).transform,
                Stone = PackSetup.BuildChurchMaterials(),
                SpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath),
                GlowMaterial = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.default2DMaterial : null,
                CameraTransform = PackSetup.PlayerCamera(player),
            };

            ctx.Geometry = Child(ctx.Root, "Geometry");
            ctx.Volumes = Child(ctx.Root, "Volumes");
            ctx.Decor = Child(ctx.Root, "Decor");
            ctx.Foreground = Child(ctx.Root, "Foreground");
            ctx.Anchors = Child(ctx.Root, "GrappleAnchors");
            ctx.Lights = Child(ctx.Root, "Lights");

            GameObject enemies = GameObject.Find("Enemies") ?? HeroSetup.CreateRoot("Enemies");
            ctx.Coordinator = HeroSetup.GetOrAdd<EnemyCoordinator>(enemies);
            EnemySetup.Set(ctx.Coordinator, so =>
            {
                so.FindProperty("_target").objectReferenceValue = player.transform;
                so.FindProperty("_cameraTransform").objectReferenceValue = ctx.CameraTransform;
            });

            foreach (string name in new[] { "Shade", "Ghoul", "FireWorm", "Wizard", "Angel", "EvilWizard" })
            {
                EnemyController prefab = RosterSetup.LoadEnemy(name);
                if (prefab != null)
                {
                    ctx.Enemies[name] = prefab;
                }
            }

            var shade = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Enemy_Shade.prefab");
            if (shade != null)
            {
                ctx.Enemies["Shade"] = shade.GetComponent<EnemyController>();
            }

            ctx.Panels = LoadPanels();
            ctx.AnchorRing = EnsureRingSprite();
            ctx.CheckpointClip = LoopClip("Checkpoint", "rift-portal", 1.1f, log);
            ctx.MothsClip = LoopClip("Moths", "choir-moths", 2.4f, log);
            BuildUi(ctx, log);
            return ctx;
        }

        private static void PlacePlayer(PlayerController player, Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            Undo.RecordObject(player.transform, "Place Player");
            player.transform.position = position;
            if (cc != null)
            {
                cc.enabled = true;
            }
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static GameObject Box(Ctx ctx, string name, float x0, float x1, float y0, float y1, float z0, float z1,
                                      string top = "floor", string sides = "block", Transform parent = null)
        {
            var size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : ctx.Geometry, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            go.transform.localScale = size;
            go.isStatic = true;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = StoneMaterial(ctx, top, sides);
            renderer.shadowCastingMode = go.transform.position.z > 10f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            ctx.Count++;
            return go;
        }

        private static Material StoneMaterial(Ctx ctx, string top, string sides)
        {
            string key = top + "_" + sides;
            if (!ctx.StonePairs.TryGetValue(key, out Material material))
            {
                material = StoneMaterials.Get(top, sides, ctx.Stone[top], ctx.Stone[sides]);
                ctx.StonePairs[key] = material;
            }

            return material;
        }

        private static GameObject Floor(Ctx ctx, string name, float x0, float x1, float top, Lane lane, float bottom = float.NaN)
        {
            float y0 = float.IsNaN(bottom) ? top - 4f : bottom;
            return Box(ctx, name, x0, x1, y0, top, lane.Front, lane.Back);
        }

        private static void Blocker(Ctx ctx, string name, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Geometry, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            go.isStatic = true;
        }

        private static void LaneWalls(Ctx ctx, string name, float x0, float x1, float y0, float y1, Lane lane)
        {
            Blocker(ctx, $"{name}_Front", x0, x1, y0, y1, lane.Front - 1f, lane.Front);
            Blocker(ctx, $"{name}_Back", x0, x1, y0, y1, lane.Back, lane.Back + 1f);
        }

        private static T Volume<T>(Ctx ctx, string name, Vector3 center, Vector3 size) where T : PlayerTrigger
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(ctx.Volumes, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            ctx.Count++;
            return go.AddComponent<T>();
        }

        private static void KillPit(Ctx ctx, string name, float x0, float x1, float y, Lane lane, int damage = 15)
        {
            var zone = Volume<KillZone>(ctx, name, new Vector3((x0 + x1) * 0.5f, y, (lane.Front + lane.Back) * 0.5f),
                new Vector3(x1 - x0 + 2f, 2f, lane.Depth + 2f));
            EnemySetup.Set(zone, so => so.FindProperty("_damage").intValue = damage);
        }

        private static void Title(Ctx ctx, string name, float x, float y, string title, string subtitle, bool toast = false, float z = 0f, float depth = 12f)
        {
            var zone = Volume<ZoneTitle>(ctx, name, new Vector3(x, y + 2f, z), new Vector3(1.5f, 6f, depth));
            EnemySetup.Set(zone, so =>
            {
                so.FindProperty("_title").stringValue = title;
                so.FindProperty("_subtitle").stringValue = subtitle;
                so.FindProperty("_toastOnly").boolValue = toast;
            });
        }

        private static void Checkpoint(Ctx ctx, string name, float x, float y, float z = 0f, float depth = 12f)
        {
            var checkpoint = Volume<Brawler.Level.Checkpoint>(ctx, name, new Vector3(x, y + 1.5f, z), new Vector3(1.2f, 3f, depth));
            Transform t = checkpoint.transform;

            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(t, false);
            spawn.position = new Vector3(x, y + 0.05f, z);

            Box(ctx, $"{name}_Pedestal", x - 0.3f, x + 0.3f, y, y + 0.45f, z + 0.6f, z + 1.2f, "floor", "brick", t);

            var lit = new GameObject("Lit");
            lit.transform.SetParent(t, false);
            lit.transform.position = new Vector3(x, y + 1.05f, z + 0.9f);
            if (ctx.CheckpointClip != null)
            {
                AnimatedSprite(lit, ctx.CheckpointClip, ctx.GlowMaterial);
            }

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lit.transform, false);
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.75f, 1f);
            light.range = 5f;
            light.intensity = 2.5f;

            EnemySetup.Set(checkpoint, so =>
            {
                so.FindProperty("_spawnPoint").objectReferenceValue = spawn;
                so.FindProperty("_litVisual").objectReferenceValue = lit;
            });
        }

        private static void Anchor(Ctx ctx, string name, float x, float y, float z = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Anchors, false);
            go.transform.position = new Vector3(x, y, z);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            var sr = glow.AddComponent<SpriteRenderer>();
            sr.sprite = ctx.AnchorRing;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            if (ctx.GlowMaterial != null)
            {
                sr.sharedMaterial = ctx.GlowMaterial;
            }

            var anchor = go.AddComponent<GrappleAnchor>();
            EnemySetup.Set(anchor, so => so.FindProperty("_glow").objectReferenceValue = sr);

            Chandelier(ctx, go.transform, go.transform.position);
            ctx.Count++;
        }

        private static Encounter Fight(Ctx ctx, string name, Vector3 center, Vector3 size, (string Enemy, float X, float Y, float Z)[][] waves,
                                       GameObject[] gates = null, string startToast = "", string clearToast = "")
        {
            var encounter = Volume<Encounter>(ctx, name, center, size);
            Transform t = encounter.transform;
            EnemySetup.Set(encounter, so =>
            {
                so.FindProperty("_coordinator").objectReferenceValue = ctx.Coordinator;
                so.FindProperty("_startToast").stringValue = startToast;
                so.FindProperty("_clearToast").stringValue = clearToast;
                HeroSetup.SetArray(so.FindProperty("_gates"), (gates ?? new GameObject[0]).Cast<Object>().ToArray());

                SerializedProperty wavesProp = so.FindProperty("_waves");
                wavesProp.arraySize = waves.Length;
                for (int w = 0; w < waves.Length; w++)
                {
                    SerializedProperty wave = wavesProp.GetArrayElementAtIndex(w);
                    wave.FindPropertyRelative("Delay").floatValue = w == 0 ? 0.2f : 1f;
                    SerializedProperty spawns = wave.FindPropertyRelative("Spawns");
                    var valid = waves[w].Where(s => ctx.Enemies.ContainsKey(s.Enemy)).ToArray();
                    spawns.arraySize = valid.Length;
                    for (int i = 0; i < valid.Length; i++)
                    {
                        var point = new GameObject($"Spawn_{w + 1}_{i + 1}_{valid[i].Enemy}").transform;
                        point.SetParent(t, false);
                        point.position = new Vector3(valid[i].X, valid[i].Y + 0.05f, valid[i].Z);
                        SerializedProperty entry = spawns.GetArrayElementAtIndex(i);
                        entry.FindPropertyRelative("Prefab").objectReferenceValue = ctx.Enemies[valid[i].Enemy];
                        entry.FindPropertyRelative("Point").objectReferenceValue = point;
                    }
                }
            });
            return encounter;
        }

        private static GameObject Gate(Ctx ctx, string name, float x, float y, Lane lane, float height = 5f)
        {
            return Box(ctx, name, x - 0.4f, x + 0.4f, y, y + height, lane.Front - 0.5f, lane.Back, "brick", "brick");
        }

        private static void PointLight(Ctx ctx, string name, Vector3 position, Color color, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(ctx.Lights, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private static void BuildCameraZones(Ctx ctx)
        {
            Transform root = Child(ctx.Root, "CameraZones");
            // Small yaws turn the camera a little into the room's depth; tall rooms pull back to show the vault.
            // Outdoors, Octopath-style: high, far back and a long lens, so the grounds read as a miniature diorama.
            CameraZone(root, "Cam_Grounds", GroundsWest, GroundsEast, -2f, 22f, 17.5f, new Vector3(0f, 1f, 2.5f), 0f,
                pitch: 34f, fieldOfView: 24f, z0: GroundsFront - 2f, z1: GroundsBack + 2f);
            CameraZone(root, "Cam_Nave", -1f, 43f, -3f, 11f, 2f, new Vector3(1f, 0.6f, 0f), 6f);
            CameraZone(root, "Cam_Gallery", 43f, 78f, GalleryY - 9f, GalleryY + 7f, 0.5f, new Vector3(1.5f, 0.3f, 0f), -5f);
            CameraZone(root, "Cam_Tower", 78f, 94f, GalleryY - 1f, TopY + 7f, 2.5f, new Vector3(0f, 0.8f, 0f), 0f);
            CameraZone(root, "Cam_Belfry", 94f, 136f, TopY - 10f, TopY + 9f, 3.5f, new Vector3(2f, 0.8f, 0f), 5f);
            CameraZone(root, "Cam_Choir", 136f, 168f, TopY - 2f, TopY + 10f, 2f, new Vector3(0f, 0.8f, 0f), 0f);
            CameraZone(root, "Cam_Sanctum", 168f, 206f, TopY - 2f, TopY + 14f, 5f, new Vector3(0f, 2.4f, 0f), 0f);

            var rig = Object.FindAnyObjectByType<CameraRig>();
            var director = ctx.Root.gameObject.AddComponent<CameraZoneDirector>();
            EnemySetup.Set(director, so =>
            {
                so.FindProperty("_rig").objectReferenceValue = rig;
                so.FindProperty("_player").objectReferenceValue = ctx.Player.transform;
            });
        }

        private static void CameraZone(Transform root, string name, float x0, float x1, float y0, float y1, float distance, Vector3 offset, float yaw,
                                       float pitch = 0f, float fieldOfView = 0f, float z0 = -6f, float z1 = 6f)
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            var zone = go.AddComponent<Brawler.Level.CameraZone>();
            EnemySetup.Set(zone, so =>
            {
                so.FindProperty("_extraDistance").floatValue = distance;
                so.FindProperty("_offset").vector3Value = offset;
                so.FindProperty("_yaw").floatValue = yaw;
                so.FindProperty("_pitch").floatValue = pitch;
                so.FindProperty("_fieldOfView").floatValue = fieldOfView;
            });
        }

        private static void PanelsAt(Ctx ctx, string name, float x0, float x1, float y, float z, params string[] order)
        {
            if (ctx.Panels.Count == 0 || order.Length == 0)
            {
                return;
            }

            float x = x0;
            int i = 0;
            while (x < x1)
            {
                string key = order[i % order.Length];
                if (!ctx.Panels.TryGetValue(key, out Sprite sprite))
                {
                    i++;
                    continue;
                }

                float width = sprite.bounds.size.x;
                if (x + width > x1 + 0.5f)
                {
                    break;
                }

                var go = new GameObject($"{name}_{i:00}_{key}");
                go.transform.SetParent(ctx.Decor, false);
                go.transform.position = new Vector3(x + width * 0.5f, y, z);
                PackSetup.ConfigureStaticSprite(go, sprite, ctx.SpriteMaterial);

                if (key == "torch")
                {
                    PointLight(ctx, $"{name}_TorchLight_{i:00}", new Vector3(x + width * 0.5f, y + 4.2f, z - 0.8f),
                        new Color(1f, 0.62f, 0.3f), 6f, 2.2f);
                }

                x += width;
                i++;
            }
        }

        private static Dictionary<string, Sprite> LoadPanels()
        {
            var panels = new Dictionary<string, Sprite>();
            foreach (string name in new[] { "window", "pillar", "altar", "gargoyle", "torch" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_ThirdParty/_Generated/Church/panel_{name}.png");
                if (sprite != null)
                {
                    panels[name] = sprite;
                }
            }

            return panels;
        }

        private static void AnimatedSprite(GameObject go, SpriteClip clip, Material material)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = clip.GetFrame(0);
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            if (material != null)
            {
                sr.sharedMaterial = material;
            }

            var animator = go.AddComponent<SpriteAnimator>();
            EnemySetup.Set(animator, so =>
            {
                so.FindProperty("_renderer").objectReferenceValue = sr;
                so.FindProperty("_defaultClip").objectReferenceValue = clip;
            });
        }

        private static SpriteClip LoopClip(string name, string effect, float width, StringBuilder log)
        {
            SpriteClip source = PackSetup.BuildPvfxClip($"{name}_Source", effect, width, log);
            if (source == null)
            {
                return null;
            }

            Sprite[] frames = Enumerable.Range(0, source.FrameCount).Select(source.GetFrame).ToArray();
            string path = $"{Root}/Data/Animation/Vfx/Clip_Vfx_{name}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<SpriteClip>(path);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteClip>();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.EditorSet(frames, 20f, true, false);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        internal static Sprite EnsureRingSprite()
        {
            const int size = 32;
            string path = $"{LevelGenerated}/anchor_ring.png";
            var pixels = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 11f) / 2.2f);
                    float core = Mathf.Clamp01(1f - d / 5f) * 0.8f;
                    byte a = (byte)(Mathf.Max(ring, core) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            PackSetup.SavePng(path, pixels, size, size);
            return PackSetup.ImportSprite(path, size / 0.7f, new Vector2(0.5f, 0.5f));
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
