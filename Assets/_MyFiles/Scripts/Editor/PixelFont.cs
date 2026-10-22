using System.IO;
using UnityEditor;
using UnityEngine;

namespace Brawler.Editor
{
    /// <summary>
    /// Builds a Unity custom font from the 16x16 pixel font sheet (A-Z, a-z, 0-9, ten glyphs per row).
    /// Custom fonts ignore Text.fontSize, so each requested size gets its own font asset whose glyphs are scaled
    /// by a whole number, which keeps the pixels crisp.
    /// </summary>
    internal static class PixelFont
    {
        private const string SheetPath = "Assets/_ThirdParty/pxfntfree13-16x16.png";
        private const string FontDir = "Assets/_ThirdParty/_Generated/Fonts";
        private const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const int Cell = 16;
        private const int Columns = 10;

        /// <summary>The pixel font closest to <paramref name="size"/> points, or null when the sheet is missing.</summary>
        internal static Font Get(int size)
        {
            int scale = Mathf.Max(1, Mathf.RoundToInt(size / (float)Cell));
            string path = $"{FontDir}/F_Pixel_x{scale}.fontsettings";
            var existing = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (existing != null)
            {
                return existing;
            }

            Texture2D sheet = ImportSheet();
            if (sheet == null)
            {
                return null;
            }

            Directory.CreateDirectory(FontDir);
            var material = new Material(Shader.Find("GUI/Text Shader")) { mainTexture = sheet };
            AssetDatabase.CreateAsset(material, $"{FontDir}/M_Pixel_x{scale}.mat");

            var font = new Font($"F_Pixel_x{scale}") { material = material };
            AssetDatabase.CreateAsset(font, path);
            int baseline = BuildGlyphs(font, sheet, scale);
            var so = new SerializedObject(font);
            so.FindProperty("m_LineSpacing").floatValue = (Cell + 2) * scale;
            so.FindProperty("m_FontSize").floatValue = Cell * scale;
            so.FindProperty("m_Ascent").floatValue = (Cell - baseline) * scale;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        private static Texture2D ImportSheet()
        {
            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        }

        /// <summary>Measures every glyph's ink width and writes the character table. Returns the baseline row.</summary>
        private static int BuildGlyphs(Font font, Texture2D sheet, int scale)
        {
            Color32[] pixels = SpriteImport.LoadPixels(SheetPath, out int width, out int height);
            int baseline = CellBounds(pixels, width, height, 0).y; // the bottom of 'A' sits on the baseline
            var infos = new CharacterInfo[Glyphs.Length + 1];
            infos[0] = new CharacterInfo { index = ' ', advance = 6 * scale };

            for (int i = 0; i < Glyphs.Length; i++)
            {
                RectInt ink = CellBounds(pixels, width, height, i);
                int column = i % Columns;
                float cellBottom = height - (i / Columns + 1) * Cell;
                float u0 = (column * Cell + ink.x) / (float)width;
                float u1 = (column * Cell + ink.x + ink.width) / (float)width;
                float v0 = cellBottom / height;
                float v1 = (cellBottom + Cell) / height;

                var info = new CharacterInfo
                {
                    index = Glyphs[i],
                    uvBottomLeft = new Vector2(u0, v0),
                    uvBottomRight = new Vector2(u1, v0),
                    uvTopLeft = new Vector2(u0, v1),
                    uvTopRight = new Vector2(u1, v1),
                    advance = (ink.width + 1) * scale,
                };
                info.minX = 0;
                info.maxX = ink.width * scale;
                info.maxY = (Cell - baseline) * scale;
                info.minY = -baseline * scale;
                infos[i + 1] = info;
            }

            font.characterInfo = infos;
            return baseline;
        }

        /// <summary>Ink bounds of glyph <paramref name="index"/> inside its cell (bottom-up rows).</summary>
        private static RectInt CellBounds(Color32[] pixels, int width, int height, int index)
        {
            var cell = new RectInt(index % Columns * Cell, index / Columns * Cell, Cell, Cell);
            Color32[] block = SpriteImport.Crop(pixels, width, height, cell);
            RectInt ink = SpriteImport.Bounds(block, Cell, Cell, 128);
            return ink.width > 0 ? ink : new RectInt(0, 0, Cell / 2, Cell);
        }
    }
}
