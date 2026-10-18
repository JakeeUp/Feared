using System.Text;
using Brawler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling: menu <c>Brawler/Apply 2.5D Look</c>. Gives the open scene the HD-2D treatment: a global
    /// post-processing volume (bokeh depth of field for the miniature/tilt-shift feel, bloom, vignette, ACES grading),
    /// sprites casting real directional shadows, distance fog, a steeper camera and a 60 Hz fixed timestep.
    /// </summary>
    /// <remarks>
    /// Idempotent: the volume profile is created only if missing (so hand-tuned values survive), and re-running just
    /// re-wires the scene. Undo is recorded for every scene change.
    /// </remarks>
    public static class LookSetup
    {
        // ============ Paths ============

        private const string Root = "Assets/_MyFiles";
        private const string ProfilePath = Root + "/Data/Presentation/PostFX_2_5D.asset";
        private const string CameraSettingsPath = Root + "/Data/Presentation/CameraRigSettings.asset";
        private const string SpriteShaderName = "Brawler/SpriteAlphaClip";
        private const string VolumeName = "Global Volume (2.5D)";

        // ============ Tuning ============

        /// <summary>Steeper than the 25° starter so the floor (and depth) reads.</summary>
        private const float CameraPitch = 32f;

        /// <summary>Bokeh lens: long focal length + wide aperture = shallow focus band around the player.</summary>
        private const float FocalLengthMm = 120f;
        private const float Aperture = 5.6f;

        private const float FogStart = 18f;
        private const float FogEnd = 60f;
        private static readonly Color FogColor = new Color(0.62f, 0.68f, 0.78f);

        // ============ Menu ============

        [MenuItem("Brawler/Apply 2.5D Look")]
        public static void Apply()
        {
            var log = new StringBuilder("[Brawler] Apply 2.5D Look\n");

            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig == null)
            {
                EditorUtility.DisplayDialog("Apply 2.5D Look", "No CameraRig in the open scene. Open TestArena (or run Brawler > Build Test Scene) first.", "OK");
                return;
            }

            float focusDistance = ConfigureCameraSettings(rig, log);
            VolumeProfile profile = EnsureProfile(focusDistance, log);
            ConfigureVolume(profile, log);
            ConfigureCamera(rig.Camera, log);
            ConfigureSpriteShadows(log);
            ConfigureFog(log);
            ConfigureFixedTimestep(log);

            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ============ Steps ============

        /// <summary>Steeper pitch on the rig settings asset; returns the camera distance (= DoF focus distance).</summary>
        private static float ConfigureCameraSettings(CameraRig rig, StringBuilder log)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(CameraSettingsPath);
            if (settings == null)
            {
                log.AppendLine("  ! CameraRigSettings asset not found, camera angle unchanged");
                return 14f;
            }

            var so = new SerializedObject(settings);
            so.FindProperty("_pitch").floatValue = CameraPitch;
            so.ApplyModifiedProperties();
            rig.ApplySettings();
            log.AppendLine($"  * camera pitch {CameraPitch}°");
            return settings.Distance;
        }

        private static VolumeProfile EnsureProfile(float focusDistance, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null)
            {
                log.AppendLine("  * post-processing profile exists, keeping its values");
                return existing;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(focusDistance);
            dof.focalLength.Override(FocalLengthMm);
            dof.aperture.Override(Aperture);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(12f);

            EditorUtility.SetDirty(profile);
            log.AppendLine($"  * created {ProfilePath} (bokeh DoF focused at {focusDistance}, bloom, vignette, ACES, grading)");
            return profile;
        }

        private static void ConfigureVolume(VolumeProfile profile, StringBuilder log)
        {
            Volume volume = null;
            foreach (Volume candidate in Object.FindObjectsByType<Volume>())
            {
                if (candidate.isGlobal)
                {
                    volume = candidate;
                    break;
                }
            }

            if (volume == null)
            {
                var go = new GameObject(VolumeName);
                Undo.RegisterCreatedObjectUndo(go, "Create Global Volume");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                log.AppendLine("  * created global volume");
            }

            Undo.RecordObject(volume, "Assign Volume Profile");
            volume.sharedProfile = profile;
            volume.priority = 0f;
            log.AppendLine($"  * volume '{volume.name}' uses {profile.name}");
        }

        private static void ConfigureCamera(Camera camera, StringBuilder log)
        {
            if (camera == null)
            {
                log.AppendLine("  ! CameraRig has no camera wired, post-processing not enabled");
                return;
            }

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObject(data, "Enable Post Processing");
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            log.AppendLine("  * camera: post-processing on, SMAA");
        }

        private static void ConfigureSpriteShadows(StringBuilder log)
        {
            int count = 0;
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>())
            {
                Material material = sr.sharedMaterial;
                if (material == null || material.shader == null || material.shader.name != SpriteShaderName)
                {
                    continue;
                }

                Undo.RecordObject(sr, "Sprite Shadows");
                sr.shadowCastingMode = ShadowCastingMode.On;
                count++;
            }

            log.AppendLine($"  * {count} sprite(s) now cast directional shadows");
        }

        private static void ConfigureFog(StringBuilder log)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.fogColor = FogColor;
            log.AppendLine($"  * linear fog {FogStart}-{FogEnd}");
        }

        private static void ConfigureFixedTimestep(StringBuilder log)
        {
            // One FixedUpdate == one frame of frame data, so attacks/dash/hitstun tick evenly.
            Time.fixedDeltaTime = 1f / 60f;
            log.AppendLine("  * fixed timestep 1/60");
        }

        // ============ Helpers ============

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
