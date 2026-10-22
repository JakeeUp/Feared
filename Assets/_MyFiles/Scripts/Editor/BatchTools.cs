using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Brawler.Level;
using Brawler.Player;
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
    /// One-call rebuild and screenshot tools, usable from the menu or headless:
    /// <c>Unity.exe -batchmode -projectPath . -executeMethod Brawler.Editor.BatchTools.RebuildAll</c>, and
    /// <c>-executeMethod Brawler.Editor.BatchTools.CaptureShots -shot nave:4,0,0 -shot tower:86,12,0</c>
    /// (images go to <c>Logs/Shots</c>; add <c>-nopost</c> to render without post-processing). Both exit the editor
    /// when run in batch mode.
    /// </summary>
    public static class BatchTools
    {
        private const string ScenePath = "Assets/_MyFiles/Scenes/Cathedral.unity";
        private const string ShotDir = "Logs/Shots";

        [MenuItem("Brawler/Rebuild Everything (Roster, HUD, Cathedral)")]
        public static void RebuildAll()
        {
            Batch(() =>
            {
                EditorSceneManager.OpenScene(ScenePath);
                RosterSetup.Run();
                HudSetup.Run();
                EditorSceneManager.SaveOpenScenes();
                CathedralBuilder.Run();
            });
        }

        public static void CaptureShots()
        {
            Batch(() =>
            {
                EditorSceneManager.OpenScene(ScenePath);
                var player = Object.FindAnyObjectByType<PlayerController>();
                var rig = Object.FindAnyObjectByType<CameraRig>();
                rig.ApplySettings();
                if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "-nopost") >= 0)
                {
                    rig.Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                }

                foreach (CameraZone zone in Object.FindObjectsByType<CameraZone>())
                {
                    Invoke(zone, "Awake");
                    Invoke(zone, "OnEnable");
                }

                var director = Object.FindAnyObjectByType<CameraZoneDirector>();
                if (director != null)
                {
                    Invoke(director, "OnEnable");
                }

                Directory.CreateDirectory(ShotDir);
                foreach ((string name, Vector3 position) in ShotArgs())
                {
                    player.transform.position = position;
                    rig.SnapToTarget();
                    foreach (BillboardSprite billboard in Object.FindObjectsByType<BillboardSprite>())
                    {
                        Invoke(billboard, "LateUpdate");
                    }

                    Render(rig.Camera, $"{ShotDir}/{name}.png");
                    Probe(rig.Camera, name);
                }
            });
        }

        /// <summary>
        /// With <c>-probe x,y</c> (fractions of the frame, 0,0 bottom-left), logs what is under that point in each shot:
        /// every renderer the ray passes through, nearest first, with its materials. For "what is that?" in a capture.
        /// </summary>
        private static void Probe(Camera camera, string shot)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-probe");
            if (i < 0 || i + 1 >= args.Length)
            {
                return;
            }

            float[] xy = Array.ConvertAll(args[i + 1].Split(','), s => float.Parse(s, CultureInfo.InvariantCulture));
            Ray ray = camera.ViewportPointToRay(new Vector3(xy[0], xy[1], 0f));
            var hits = new List<(float, string)>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>())
            {
                if (r.bounds.IntersectRay(ray, out float distance))
                {
                    var names = new List<string>();
                    foreach (Material m in r.sharedMaterials)
                    {
                        names.Add(m != null ? $"{m.name}({(m.mainTexture != null ? m.mainTexture.name : "-")})" : "null");
                    }

                    hits.Add((distance, $"{r.name} [{r.GetType().Name}] parent={(r.transform.parent != null ? r.transform.parent.name : "-")} mats={string.Join(",", names)} lightmap={r.lightmapIndex} staticBatch={r.isPartOfStaticBatch} probes={r.lightProbeUsage} mesh={(r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null ? f.sharedMesh.name + "/uv" + f.sharedMesh.uv.Length : "-")}"));
                }
            }

            hits.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            var log = new System.Text.StringBuilder().AppendLine($"[Probe] {shot} at {args[i + 1]}:");
            foreach ((float d, string text) in hits.GetRange(0, Mathf.Min(8, hits.Count)))
            {
                log.AppendLine($"  {d:0.0}  {text}");
            }

            Debug.Log(log.ToString());
        }

        private static void Render(Camera camera, string path)
        {
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.aspect = width / (float)height;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Parses <c>-shot name:x,y,z</c> pairs from the command line.</summary>
        private static IEnumerable<(string, Vector3)> ShotArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-shot")
                {
                    continue;
                }

                string[] parts = args[i + 1].Split(':');
                float[] xyz = Array.ConvertAll(parts[1].Split(','), s => float.Parse(s, CultureInfo.InvariantCulture));
                yield return (parts[0], new Vector3(xyz[0], xyz[1], xyz[2]));
            }
        }

        private static void Batch(Action action)
        {
            try
            {
                action();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        }
    }
}
