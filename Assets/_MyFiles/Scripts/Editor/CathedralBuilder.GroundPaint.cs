using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>
    /// Paints the grounds' floor as one pixel-art texture, the way HD-2D fields are dressed: a single continuous
    /// surface where night grass, packed earth and worn cobbles blend into each other, instead of tiles laid in a
    /// grid. Paths are drawn along the same polylines the layout walks, with ragged, dithered edges; grave plots
    /// sink to darker soil. Deterministic, so a rebuild paints the same ground.
    /// </summary>
    public static partial class CathedralBuilder
    {
        private const string GroundPaintPath = "Assets/_ThirdParty/_Generated/Graveyard/T_GroundsPaint.png";
        private const string GroundPaintMaterialPath = "Assets/_ThirdParty/_Generated/Graveyard/M_GroundsPaint.mat";

        /// <summary>Texels per world unit: chunky enough to read as pixel art next to the 1-unit hero.</summary>
        private const int PaintDensity = 10;

        // Night palette: cold grass, grey-brown earth, blue-grey cobbles.
        private static readonly Color32[] GrassShades = { C(28, 38, 36), C(33, 45, 40), C(39, 52, 44), C(46, 60, 49) };
        private static readonly Color32[] EarthShades = { C(47, 42, 44), C(55, 49, 49), C(63, 56, 54) };
        private static readonly Color32[] StoneShades = { C(74, 74, 84), C(84, 83, 92), C(95, 93, 100), C(66, 66, 76) };
        private static readonly Color32 Mortar = C(38, 35, 40);
        private static readonly Color32 SoilDark = C(32, 29, 31);

        private static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        /// <summary>Lays the painted floor over the rectangle as an unlit-collision quad just above y 0.</summary>
        private static void PaintGround(Ctx ctx, float x0, float x1, float z0, float z1, (Vector2[] Points, float HalfWidth)[] paths, Rect[] plots)
        {
            int w = Mathf.CeilToInt((x1 - x0) * PaintDensity), h = Mathf.CeilToInt((z1 - z0) * PaintDensity);
            var pixels = new Color32[w * h];
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    float x = x0 + (px + 0.5f) / PaintDensity, z = z0 + (py + 0.5f) / PaintDensity;
                    pixels[py * w + px] = GroundTexel(x, z, px, py, paths, plots);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(GroundPaintPath));
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(GroundPaintPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(GroundPaintPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(GroundPaintPath) is TextureImporter importer)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false; // always magnified; mip streaming showed blurry low mips on the first frame
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundPaintMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, GroundPaintMaterialPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPaintPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Grounds_Paint";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(ctx.Decor, false);
            quad.transform.SetPositionAndRotation(new Vector3((x0 + x1) * 0.5f, 0.004f, (z0 + z1) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(x1 - x0, z1 - z0, 1f);
            quad.isStatic = true;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ctx.Count++;
        }

        private static Color32 GroundTexel(float x, float z, int px, int py, (Vector2[] Points, float HalfWidth)[] paths, Rect[] plots)
        {
            // Grass: large soft patches, broken up by per-texel dither and the odd lighter tuft.
            float patch = Fbm(x * 0.18f, z * 0.18f);
            float grain = Hash(px, py);
            int shade = Mathf.Clamp((int)(patch * 4.2f + (grain - 0.5f) * 1.1f), 0, GrassShades.Length - 1);
            Color32 colour = GrassShades[shade];
            if (grain > 0.985f)
            {
                colour = C(58, 72, 58); // tuft
            }

            // Plots: sunken dark soil with a ragged rim.
            foreach (Rect plot in plots)
            {
                float inside = Mathf.Min(Mathf.Min(x - plot.xMin, plot.xMax - x), Mathf.Min(z - plot.yMin, plot.yMax - z));
                float rim = inside + (Fbm(x * 1.3f, z * 1.3f) - 0.5f) * 0.8f;
                if (rim > 0.15f || (rim > -0.2f && grain > 0.55f))
                {
                    colour = Lerp(SoilDark, GrassShades[0], Mathf.Clamp01(Fbm(x * 0.6f + 9f, z * 0.6f) - 0.25f));
                }
            }

            // Paths: packed earth with ragged dithered edges, cobbles laid down the worn middle.
            float best = float.MaxValue, halfWidth = 0f;
            foreach (var path in paths)
            {
                for (int i = 0; i < path.Points.Length - 1; i++)
                {
                    float d = SegmentDistance(new Vector2(x, z), path.Points[i], path.Points[i + 1]) - path.HalfWidth;
                    if (d < best)
                    {
                        best = d;
                        halfWidth = path.HalfWidth;
                    }
                }
            }

            float edge = best + (Fbm(x * 0.9f + 31f, z * 0.9f) - 0.5f) * 0.9f;
            if (edge > 0.25f)
            {
                return colour;
            }

            if (edge > -0.15f && grain < (edge + 0.15f) / 0.4f)
            {
                return colour; // the edge frays: grass texels poking into the dirt
            }

            Color32 earth = EarthShades[Mathf.Clamp((int)(Fbm(x * 0.7f, z * 0.7f + 50f) * 3.2f + (grain - 0.5f) * 0.8f), 0, EarthShades.Length - 1)];
            float wear = -best / Mathf.Max(0.01f, halfWidth); // 0 at the edge, 1 on the centre line
            if (wear < 0.35f + (Fbm(x * 0.5f + 70f, z * 0.5f) - 0.5f) * 0.4f)
            {
                return earth;
            }

            // Cobbles: jittered cells, each its own shade, with dark mortar where cells meet; a few are missing.
            Cobble(x * 1.15f, z * 1.4f, out float border, out int cell);
            if (Hash(cell, 7) < 0.12f)
            {
                return earth;
            }

            if (border < 0.07f)
            {
                return Mortar;
            }

            Color32 stone = StoneShades[(int)(Hash(cell, 3) * StoneShades.Length) % StoneShades.Length];
            return border < 0.12f || grain > 0.95f ? Lerp(stone, Mortar, 0.35f) : stone;
        }

        /// <summary>Worley-style cells: distance to the cell border (second-nearest minus nearest) and the cell id.</summary>
        private static void Cobble(float x, float z, out float border, out int cell)
        {
            int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
            float first = float.MaxValue, second = float.MaxValue;
            cell = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ix = cx + dx, iz = cz + dz;
                    float fx = ix + 0.15f + Hash(ix, iz) * 0.7f, fz = iz + 0.15f + Hash(iz + 101, ix) * 0.7f;
                    float d = Mathf.Sqrt((x - fx) * (x - fx) + (z - fz) * (z - fz));
                    if (d < first)
                    {
                        second = first;
                        first = d;
                        cell = ix * 7919 + iz;
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }

            border = second - first;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        /// <summary>Two octaves of smooth value noise in 0..1.</summary>
        private static float Fbm(float x, float z)
        {
            return ValueNoise(x, z) * 0.65f + ValueNoise(x * 2.3f + 17f, z * 2.3f) * 0.35f;
        }

        private static float ValueNoise(float x, float z)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float a = Mathf.Lerp(Hash(ix, iz), Hash(ix + 1, iz), fx);
            float b = Mathf.Lerp(Hash(ix, iz + 1), Hash(ix + 1, iz + 1), fx);
            return Mathf.Lerp(a, b, fz);
        }

        /// <summary>Integer hash to 0..1.</summary>
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
