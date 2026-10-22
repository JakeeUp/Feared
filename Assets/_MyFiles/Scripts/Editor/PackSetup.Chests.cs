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
    /// <summary>
    /// Treasure chests from Kenney's CC0 Mini Dungeon kit (kenney.nl/assets/mini-dungeon), placed where
    /// <see cref="CathedralBuilder.ChestPlacements"/> says: off the main path, in nooks worth exploring. The kit's
    /// chest has a separate lid, which is re-parented onto a hinge on its back edge so it swings open.
    /// </summary>
    public static partial class PackSetup
    {
        private const string ChestModel = ThirdParty + "/Kenney_mini-dungeon/FBX/chest.fbx";
        private const string ChestTexture = ThirdParty + "/Kenney_mini-dungeon/FBX/Textures/colormap.png";
        private const string ChestMaterialPath = GeneratedDir + "/Chests/M_Chest.mat";
        private const string ChestRootName = "Chests";
        private const float ChestHeight = 0.75f;

        private static void BuildChests(PlayerController player, VfxPlayer vfx, StringBuilder log)
        {
            GameObject old = GameObject.Find(ChestRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            if (!CathedralBuilder.IsCathedralScene() || CathedralBuilder.ChestPlacements.Length == 0)
            {
                return;
            }

            GameObject model = LoadPotModel(ChestModel);
            if (model == null)
            {
                log.AppendLine($"  ! chests skipped: {ChestModel} not found");
                return;
            }

            var wallet = player.GetComponent<ScoreWallet>();
            var orbs = Object.FindAnyObjectByType<XpOrbSpawner>();
            SpriteClip puff = AssetDatabase.LoadAssetAtPath<SpriteClip>($"{VfxClipDir}/Clip_Vfx_Puff.asset");
            int hurtboxLayer = player.GetComponentInChildren<Hurtbox>(true)?.gameObject.layer ?? 0;
            AudioClip[] sounds = new[] { "doorOpen_1", "doorOpen_2", "handleCoins" }
                .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{ThirdParty}/Audio/Audio/{n}.ogg"))
                .Where(c => c != null).ToArray();
            Material material = ChestMaterial();

            GameObject root = EditorUtil.CreateRoot(ChestRootName);
            for (int i = 0; i < CathedralBuilder.ChestPlacements.Length; i++)
            {
                var place = CathedralBuilder.ChestPlacements[i];
                BuildChest(root.transform, i, model, material, place.Position, place.Yaw, place.Xp,
                           hurtboxLayer, wallet, orbs, vfx, puff, sounds);
            }

            log.AppendLine($"  * {CathedralBuilder.ChestPlacements.Length} treasure chests (strike to open: XP and score)");
        }

        private static void BuildChest(Transform root, int index, GameObject asset, Material material, Vector3 position, float yaw, int xp,
                                       int hurtboxLayer, ScoreWallet wallet, XpOrbSpawner orbs, VfxPlayer vfx, SpriteClip puff, AudioClip[] sounds)
        {
            // Built facing the camera (-Z) at the origin of its root, turned to its yaw at the end.
            var go = new GameObject($"Chest_{index}");
            go.transform.SetParent(root, false);
            go.transform.position = position;

            GameObject visual = Object.Instantiate(asset, go.transform, false);
            visual.name = "Model";
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the kit's front is +Z
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            Bounds bounds = CathedralKit.BoundsOf(visual);
            visual.transform.localScale = Vector3.one * (ChestHeight / Mathf.Max(0.01f, bounds.size.y));
            bounds = CathedralKit.BoundsOf(visual);
            visual.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            bounds = CathedralKit.BoundsOf(visual);

            // Hinge on the lid's back-bottom edge; the lid swings up and back about it.
            Transform lid = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLowerInvariant().Contains("lid"));
            Transform hinge = null;
            if (lid != null && lid.TryGetComponent(out Renderer lidRenderer))
            {
                Bounds lb = lidRenderer.bounds;
                hinge = new GameObject("Hinge").transform;
                hinge.SetParent(go.transform, false);
                hinge.position = new Vector3(lb.center.x, lb.min.y, lb.max.z);
                lid.SetParent(hinge, true);
            }

            var solid = go.AddComponent<BoxCollider>();
            solid.center = go.transform.InverseTransformPoint(bounds.center);
            solid.size = bounds.size;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, ChestHeight + 0.3f, -0.2f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.78f, 0.4f);
            glow.range = 3.5f;
            glow.shadows = LightShadows.None;

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = 0.8f;

            var chest = go.AddComponent<TreasureChest>();
            BoxCollider trigger = AddPropHurtbox(go, chest, hurtboxLayer, new Vector3(bounds.size.x + 0.2f, bounds.size.y + 0.2f, 0.8f));
            EditorUtil.Set(chest, so =>
            {
                so.FindProperty("_hinge").objectReferenceValue = hinge;
                so.FindProperty("_body").objectReferenceValue = visual.transform;
                so.FindProperty("_hurtbox").objectReferenceValue = trigger;
                so.FindProperty("_glow").objectReferenceValue = glow;
                so.FindProperty("_xp").intValue = xp;
                so.FindProperty("_scoreReward").intValue = xp * 4;
                so.FindProperty("_orbs").objectReferenceValue = orbs;
                so.FindProperty("_wallet").objectReferenceValue = wallet;
                so.FindProperty("_vfx").objectReferenceValue = vfx;
                so.FindProperty("_openClip").objectReferenceValue = puff;
                so.FindProperty("_audio").objectReferenceValue = audio;
                EditorUtil.SetArray(so.FindProperty("_openSounds"), sounds.Cast<Object>().ToArray());
            });

            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>The kit's colour atlas, point-filtered and matte, a little darker for the night.</summary>
        private static Material ChestMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ChestMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ChestMaterialPath));
                material = new Material(Shader.Find(LitShaderName));
                AssetDatabase.CreateAsset(material, ChestMaterialPath);
            }

            material.SetTexture("_BaseMap", ImportPointTexture(ChestTexture));
            material.SetColor("_BaseColor", new Color(0.66f, 0.6f, 0.62f));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
