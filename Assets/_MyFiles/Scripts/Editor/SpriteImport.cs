using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    internal static class SpriteImport
    {
        private const string ClipRoot = "Assets/_MyFiles/Data/Animation";

        internal static Color32[] LoadPixels(string path, out int width, out int height)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            Color32[] pixels = tex.GetPixels32();
            width = tex.width;
            height = tex.height;
            Object.DestroyImmediate(tex);
            return pixels;
        }

        internal static Color32[] Crop(Color32[] pixels, int width, int height, RectInt topLeftRect)
        {
            int w = topLeftRect.width, h = topLeftRect.height;
            int bottom = height - topLeftRect.y - h;
            var block = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                System.Array.Copy(pixels, (bottom + row) * width + topLeftRect.x, block, row * w, w);
            }

            return block;
        }

        internal static RectInt Bounds(Color32[] pixels, int w, int h, byte solidAlpha)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a < solidAlpha)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            return maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        internal static void SavePng(string path, Color32[] pixels, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        internal static bool CropToPng(string source, RectInt topLeftRect, string destination)
        {
            if (!File.Exists(source))
            {
                return false;
            }

            Color32[] all = LoadPixels(source, out int w, out int h);
            SavePng(destination, Crop(all, w, h, topLeftRect), topLeftRect.width, topLeftRect.height);
            return true;
        }

        internal static List<string> FramePaths(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return new List<string>();
            }

            return Directory.GetFiles(dir, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(FrameNumber)
                .ToList();
        }

        internal static int FrameNumber(string path)
        {
            Match m = Regex.Match(Path.GetFileNameWithoutExtension(path), @"(\d+)$");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        internal static Sprite ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, Vector4 border = default)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ApplySprite(importer, pivot, pixelsPerUnit, FilterMode.Point, border);
            return LoadSprite(path);
        }

        internal static void ApplySprite(TextureImporter importer, Vector2 pivot, float pixelsPerUnit, FilterMode filter,
                                         Vector4? border = null)
        {
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
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
            if (border.HasValue)
            {
                settings.spriteBorder = border.Value;
            }
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        internal static Sprite[] ImportFrames(List<string> paths, float pixelsPerUnit, Vector2 pivot)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string path in paths)
                {
                    ApplySprite((TextureImporter)AssetImporter.GetAtPath(path), pivot, pixelsPerUnit, FilterMode.Point);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return paths.Select(LoadSprite).Where(s => s != null).ToArray();
        }

        internal static Texture2D ImportTiledTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static string ClipDir(string owner)
        {
            return $"{ClipRoot}/{owner}";
        }

        internal static SpriteClip SaveClip(string owner, string role, Sprite[] frames, float fps, bool loop, bool mirrored)
        {
            string dir = ClipDir(owner);
            Directory.CreateDirectory(dir);
            string path = $"{dir}/Clip_{owner}_{role}.asset";
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
    }
}
