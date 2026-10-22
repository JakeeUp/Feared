using System.Text;
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
    /// <summary>
    /// Wires the readability layer into the open scene: the camera-side <see cref="OcclusionFader"/>, the HUD's
    /// <see cref="EnemyIndicators"/>, and red <see cref="SealBarrier"/>s on every encounter / boss-arena gate.
    /// Idempotent; the Cathedral builder runs it too.
    /// </summary>
    internal static class ReadabilitySetup
    {
        private const string SealShaderPath = "Assets/_MyFiles/Art/Shaders/RedSeal.shader";
        private const string SealMaterialPath = "Assets/_MyFiles/Art/Materials/M_RedSeal.mat";
        private const string DitherShaderPath = "Assets/_MyFiles/Art/Shaders/OccluderDither.shader";
        private const string IndicatorsName = "EnemyIndicators";
        private const string SealLightName = "SealGlow";

        private static readonly Color SealLightColor = new Color(1f, 0.15f, 0.08f);

        [MenuItem("Brawler/Setup Readability (Occlusion Fade, Enemy Arrows, Red Seals)")]
        public static void Run()
        {
            var log = new StringBuilder("[Brawler] Setup Readability\n");
            Apply(log);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        internal static void Apply(StringBuilder log)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            Camera camera = rig != null ? rig.Camera : null;

            if (camera != null)
            {
                var fader = EditorUtil.GetOrAdd<OcclusionFader>(camera.gameObject);
                EditorUtil.Set(fader, so =>
                    so.FindProperty("_ditherShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(DitherShaderPath));
                log.AppendLine($"  * OcclusionFader on '{camera.name}'");
            }
            else
            {
                log.AppendLine("  ! no CameraRig camera: occlusion fade skipped");
            }

            SetupIndicators(player, camera, log);

            Material seal = EnsureSealMaterial();
            int seals = 0;
            foreach (Encounter encounter in Object.FindObjectsByType<Encounter>(FindObjectsInactive.Include))
            {
                seals += SealGates(new SerializedObject(encounter).FindProperty("_gates"), seal);
            }

            foreach (BossArena arena in Object.FindObjectsByType<BossArena>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(arena);
                seals += SealGates(so.FindProperty("_gates"), seal);
                seals += SealGates(so.FindProperty("_exitBlockers"), seal);
            }

            log.AppendLine($"  * {seals} gate(s) are red seals");
        }

        // ============ Enemy arrows ============

        internal static void SetupIndicators(PlayerController player, Camera camera, StringBuilder log)
        {
            var hud = Object.FindAnyObjectByType<PlayerHud>(FindObjectsInactive.Include);
            Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null)
            {
                log.AppendLine("  ! no HUD canvas: enemy arrows skipped (run Setup HUD first)");
                return;
            }

            Transform child = canvas.transform.Find(IndicatorsName);
            if (child == null)
            {
                var go = new GameObject(IndicatorsName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + IndicatorsName);
                child = go.transform;
                child.SetParent(canvas.transform, false);
            }

            var rect = (RectTransform)child;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            child.SetAsFirstSibling(); // under the rest of the HUD

            var indicators = EditorUtil.GetOrAdd<EnemyIndicators>(child.gameObject);
            EditorUtil.Set(indicators, so =>
            {
                so.FindProperty("_camera").objectReferenceValue = camera;
                so.FindProperty("_player").objectReferenceValue = player != null ? player.transform : null;
            });
            log.AppendLine($"  * EnemyIndicators under '{canvas.name}'");
        }

        // ============ Seals ============

        private static int SealGates(SerializedProperty gates, Material material)
        {
            int count = 0;
            for (int i = 0; i < gates.arraySize; i++)
            {
                if (gates.GetArrayElementAtIndex(i).objectReferenceValue is GameObject gate)
                {
                    MakeSeal(gate, material);
                    count++;
                }
            }

            return count;
        }

        /// <summary>Turns a gate box into a red seal: seal material, no shadows, a red glow and a <see cref="SealBarrier"/>.</summary>
        internal static void MakeSeal(GameObject gate, Material material)
        {
            // Static batching would bake the box into a combined mesh; the seal animates per renderer.
            GameObjectUtility.SetStaticEditorFlags(gate, 0);
            var renderer = gate.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Transform glow = gate.transform.Find(SealLightName);
            if (glow == null)
            {
                glow = new GameObject(SealLightName).transform;
                glow.SetParent(gate.transform, false);
            }

            glow.localPosition = new Vector3(0f, -0.2f, -0.6f); // low and toward the camera, washing the floor
            var light = EditorUtil.GetOrAdd<Light>(glow.gameObject);
            light.type = LightType.Point;
            light.color = SealLightColor;
            light.range = 6f;
            light.intensity = 0f; // SealBarrier drives it
            light.shadows = LightShadows.None;

            var seal = EditorUtil.GetOrAdd<SealBarrier>(gate);
            EditorUtil.Set(seal, so =>
            {
                so.FindProperty("_renderer").objectReferenceValue = renderer;
                so.FindProperty("_collider").objectReferenceValue = gate.GetComponent<Collider>();
                so.FindProperty("_light").objectReferenceValue = light;
            });
        }

        private static Material EnsureSealMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SealMaterialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(SealShaderPath)) { name = "M_RedSeal" };
                AssetDatabase.CreateAsset(material, SealMaterialPath);
            }

            return material;
        }
    }
}
