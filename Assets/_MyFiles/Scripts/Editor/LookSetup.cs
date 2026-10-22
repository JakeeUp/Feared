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
    public static class LookSetup
    {

        private const string Root = "Assets/_MyFiles";
        private const string ProfilePath = Root + "/Data/Presentation/PostFX_2_5D.asset";
        private const string CameraSettingsPath = Root + "/Data/Presentation/CameraRigSettings.asset";
        private const string SpriteShaderName = "Brawler/SpriteAlphaClip";
        private const string VolumeName = "Global Volume (2.5D)";


        // A low, side-on camera (Bloodstained, Lost Crown): the back wall, pillars and vault fill the frame, so a room
        // reads as the inside of a building rather than a floor seen from above.
        private const float CameraPitch = 13f;
        internal const float CameraDistance = 12.5f;
        private const float CameraFieldOfView = 34f;
        private static readonly Vector3 CameraTargetOffset = new Vector3(0f, 1.6f, 0f);

        private const float FocalLengthMm = 120f;
        private const float Aperture = 5.6f;

        // Night: dark blue haze that only starts past the far outdoor camera (~30 units), so it sinks distant
        // things into the dark instead of washing the scene out.
        private const float FogStart = 30f;
        private const float FogEnd = 85f;
        private static readonly Color FogColor = new Color(0.07f, 0.08f, 0.14f);
        private static readonly Color NightSky = new Color(0.03f, 0.035f, 0.07f);


        private const string ChurchMaterials = Root + "/Art/Materials/Church";

        private const float Hd2dFocalLengthMm = 60f;
        private const float Hd2dAperture = 4f;

        private const float Hd2dBloomThreshold = 1f;
        private const float Hd2dBloomIntensity = 0.4f;
        private const float Hd2dVignette = 0.3f;

        private const float Hd2dFogStart = 24f;
        private const float Hd2dFogEnd = 75f;
        private static readonly Color Hd2dFogColor = new Color(0.42f, 0.44f, 0.62f);

        private static readonly Color FloorTint = new Color(1f, 0.96f, 0.88f, 1f);
        private static readonly Color WallTint = new Color(0.8f, 0.82f, 0.9f, 1f);


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

        [MenuItem("Brawler/Apply HD-2D Look")]
        public static void ApplyHd2dLookMenu()
        {
            var log = new StringBuilder("[Brawler] Apply HD-2D Look\n");
            ApplyHd2dLook(log);
            Debug.Log(log.ToString());
        }

        internal static void ApplyHd2dLook(StringBuilder log)
        {
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig == null)
            {
                log.AppendLine("  ! HD-2D: no CameraRig in the open scene, skipped");
                return;
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = EnsureProfile(14f, log);
            }

            ConfigureHd2dProfile(profile, log);
            Volume volume = ConfigureVolume(profile, log);
            ConfigureCamera(rig.Camera, log);
            ConfigureFocus(rig.Camera, volume, log);
            ConfigureHd2dFog(log);
            ConfigureMoon(log);
            ConfigureSurfaceTints(log);

            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            AssetDatabase.SaveAssets();
        }


        private static float ConfigureCameraSettings(CameraRig rig, StringBuilder log)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(CameraSettingsPath);
            if (settings == null)
            {
                log.AppendLine("  ! CameraRigSettings asset not found, camera angle unchanged");
                return 14f;
            }

            EditorUtil.Set(settings, so =>
            {
                so.FindProperty("_pitch").floatValue = CameraPitch;
                so.FindProperty("_distance").floatValue = CameraDistance;
                so.FindProperty("_fieldOfView").floatValue = CameraFieldOfView;
                so.FindProperty("_targetOffset").vector3Value = CameraTargetOffset;
            });
            rig.ApplySettings();
            log.AppendLine($"  * camera pitch {CameraPitch}°, distance {CameraDistance}, fov {CameraFieldOfView}");
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

        private static Volume ConfigureVolume(VolumeProfile profile, StringBuilder log)
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
            return volume;
        }

        /// <summary>
        /// The scene's sun becomes a dim, cold moon behind the camera's shoulder: lit faces turn toward the viewer and
        /// shadows fall away into the scene. Warm point lights (lamps, torches, braziers) carry the colour.
        /// </summary>
        private static void ConfigureMoon(StringBuilder log)
        {
            foreach (Light light in Object.FindObjectsByType<Light>())
            {
                if (light.type != LightType.Directional)
                {
                    continue;
                }

                Undo.RecordObject(light, "Moonlight");
                Undo.RecordObject(light.transform, "Moonlight");
                light.color = new Color(0.55f, 0.62f, 0.9f);
                light.intensity = 0.45f;
                light.transform.rotation = Quaternion.Euler(42f, 24f, 0f);
                log.AppendLine("  * moonlight: cold, dim, from behind the camera");
            }
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
            Undo.RecordObject(camera, "Night Sky");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            log.AppendLine("  * camera: post-processing on, SMAA, night sky");
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
            Time.fixedDeltaTime = 1f / 60f;
            log.AppendLine("  * fixed timestep 1/60");
        }


        private static void ConfigureHd2dProfile(VolumeProfile profile, StringBuilder log)
        {
            if (!profile.TryGet(out DepthOfField dof))
            {
                dof = Add<DepthOfField>(profile);
            }

            dof.active = true;
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focalLength.Override(Hd2dFocalLengthMm);
            dof.aperture.Override(Hd2dAperture);
            if (!dof.focusDistance.overrideState)
            {
                dof.focusDistance.Override(14f);
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = Add<Bloom>(profile);
                bloom.scatter.Override(0.7f);
            }

            bloom.active = true;
            bloom.threshold.Override(Hd2dBloomThreshold);
            bloom.intensity.Override(Hd2dBloomIntensity);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = Add<Vignette>(profile);
                vignette.smoothness.Override(0.45f);
            }

            vignette.active = true;
            vignette.intensity.Override(Hd2dVignette);

            EditorUtility.SetDirty(dof);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(profile);
            log.AppendLine($"  * profile: bokeh DoF {Hd2dFocalLengthMm}mm f/{Hd2dAperture}, bloom {Hd2dBloomThreshold}/{Hd2dBloomIntensity}, vignette {Hd2dVignette}");
        }

        private static void ConfigureFocus(Camera camera, Volume volume, StringBuilder log)
        {
            if (camera == null)
            {
                log.AppendLine("  ! CameraRig has no camera wired, DepthOfFieldFocus not added");
                return;
            }

            var focus = EditorUtil.GetOrAdd<DepthOfFieldFocus>(camera.gameObject);
            var player = Object.FindAnyObjectByType<Brawler.Player.PlayerController>();
            EditorUtil.Set(focus, so =>
            {
                so.FindProperty("_volume").objectReferenceValue = volume;
                so.FindProperty("_camera").objectReferenceValue = camera;
                so.FindProperty("_target").objectReferenceValue = player != null ? player.transform : null;
                so.FindProperty("_focalLength").floatValue = Hd2dFocalLengthMm;
                so.FindProperty("_aperture").floatValue = Hd2dAperture;
            });
            log.AppendLine(player != null
                ? $"  * DepthOfFieldFocus on '{camera.name}' tracks '{player.name}'"
                : $"  ! DepthOfFieldFocus on '{camera.name}' has no target (no PlayerController in scene)");
        }

        private static void ConfigureHd2dFog(StringBuilder log)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Hd2dFogStart;
            RenderSettings.fogEndDistance = Hd2dFogEnd;
            RenderSettings.fogColor = Hd2dFogColor;
            log.AppendLine($"  * blue-violet linear fog {Hd2dFogStart}-{Hd2dFogEnd}");
        }

        private static void ConfigureSurfaceTints(StringBuilder log)
        {
            SetBaseColor(ChurchMaterials + "/M_Church_floor.mat", FloorTint, log);
            SetBaseColor(ChurchMaterials + "/M_Church_block.mat", WallTint, log);
            SetBaseColor(ChurchMaterials + "/M_Church_brick.mat", WallTint, log);
            StoneMaterials.SyncTints();
        }

        private static void SetBaseColor(string path, Color color, StringBuilder log)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty("_BaseColor"))
            {
                log.AppendLine($"  ! {path} missing (or no _BaseColor), tint skipped");
                return;
            }

            if (material.GetColor("_BaseColor") == color)
            {
                return;
            }

            Undo.RecordObject(material, "HD-2D Surface Tint");
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            log.AppendLine($"  * {material.name} tint {color}");
        }


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
