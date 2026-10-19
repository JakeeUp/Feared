using System.IO;
using System.Text;
using UnityEngine;

namespace Brawler.Editor
{
    /// <summary>Sprites cut from the Androx fantasy GUI for the HUD (see <see cref="PackSetup.EnsureUiSkin"/>).</summary>
    internal sealed class UiSkin
    {
        /// <summary>Sword health/mana frame with both bars emptied out.</summary>
        public Sprite HealthFrame;

        /// <summary>The red health bar alone, cropped to <see cref="HealthRect"/>.</summary>
        public Sprite HealthFill;

        /// <summary>The health bar's shape in white, for the trailing damage chip.</summary>
        public Sprite HealthChip;

        /// <summary>The green bar alone (used for XP), cropped to <see cref="XpRect"/>.</summary>
        public Sprite XpFill;

        /// <summary>The hero's face, shown in the frame's round socket.</summary>
        public Sprite Portrait;

        /// <summary>9-sliced menu window.</summary>
        public Sprite Window;

        /// <summary>9-sliced slot, used for buttons and ability slots.</summary>
        public Sprite Slot;

        /// <summary>Frame size in pixels.</summary>
        public Vector2Int FrameSize;

        /// <summary>Where the bars sit in the frame (pixels, top-left origin).</summary>
        public RectInt HealthRect;
        public RectInt XpRect;

        /// <summary>Centre (pixels, top-left origin) and diameter of the round portrait socket.</summary>
        public Vector2 PortraitCenter;
        public float PortraitDiameter;
    }

    /// <summary>UI half of Apply Asset Packs: builds the <see cref="UiSkin"/> that Setup HUD dresses the HUD with.</summary>
    public static partial class PackSetup
    {
        // ============ Paths ============

        private const string HealthFrameSource = ThirdParty + "/HP_counters/sword_UI_HP+mana.png";
        private const string InventorySource = ThirdParty + "/inventory/inventory_pt1.png";
        private const string FaceSource = ThirdParty + "/AssetPack/Bonus/face1.png";
        private const string UiGenerated = GeneratedDir + "/UI";

        // ============ Sword Frame Layout (pixels, top-left origin) ============

        /// <summary>Rows/first column of each bar; only the bar's own colours inside are treated as fill.</summary>
        private static readonly RectInt HealthBarArea = new RectInt(44, 19, 164, 7);
        private static readonly RectInt XpBarArea = new RectInt(54, 29, 154, 5);

        private static readonly Color32[] HealthBarColours =
        {
            new Color32(252, 203, 131, 255), new Color32(205, 104, 61, 255),
            new Color32(177, 55, 61, 255), new Color32(139, 41, 68, 255),
        };

        private static readonly Color32[] XpBarColours =
        {
            new Color32(242, 236, 139, 255), new Color32(181, 232, 136, 255),
            new Color32(106, 190, 48, 255), new Color32(75, 105, 47, 255),
        };

        /// <summary>What an emptied bar shows (dark troughs).</summary>
        private static readonly Color32 HealthEmpty = new Color32(38, 20, 30, 255);
        private static readonly Color32 XpEmpty = new Color32(22, 30, 24, 255);

        private static readonly Vector2 SocketCenter = new Vector2(29.5f, 24f);
        private const float SocketDiameter = 22f;

        /// <summary>The face's art inside face1.png.</summary>
        private static readonly RectInt FaceArt = new RectInt(15, 15, 39, 30);

        // ============ Inventory Layout ============

        /// <summary>Inside of the inventory frame (the slot grid), flattened to a plain panel.</summary>
        private static readonly RectInt WindowInterior = new RectInt(17, 17, 158, 126);
        private const int WindowBorder = 22;
        private static readonly Color32 WindowFill = new Color32(34, 27, 38, 255);

        /// <summary>One slot of the grid.</summary>
        private static readonly RectInt SlotArt = new RectInt(17, 17, 30, 30);
        private const int SlotBorder = 7;

        /// <summary>UI sprites use the canvas default; Setup HUD scales them with pixelsPerUnitMultiplier.</summary>
        private const float UiPixelsPerUnit = 100f;

        // ============ API ============

        /// <summary>Cuts the HUD sprites out of the GUI pack. Null when the pack isn't in the project.</summary>
        internal static UiSkin EnsureUiSkin(StringBuilder log)
        {
            if (!File.Exists(HealthFrameSource) || !File.Exists(InventorySource))
            {
                log.AppendLine("  ! GUI pack not found: plain HUD");
                return null;
            }

            var skin = new UiSkin();
            BuildHealthFrame(skin);
            BuildPanels(skin);
            if (File.Exists(FaceSource))
            {
                string path = $"{UiGenerated}/portrait.png";
                CropToPng(FaceSource, FaceArt, path);
                skin.Portrait = ImportSprite(path, UiPixelsPerUnit, new Vector2(0.5f, 0.5f));
            }

            log.AppendLine("  * HUD skin: sword health frame, portrait, menu window and slots from the GUI pack");
            return skin;
        }

        // ============ Helpers ============

        /// <summary>
        /// Splits the sword frame into an empty frame plus separate bar sprites, so the bars can drain over it.
        /// </summary>
        private static void BuildHealthFrame(UiSkin skin)
        {
            Color32[] frame = LoadPixels(HealthFrameSource, out int w, out int h);
            var health = new Color32[frame.Length];
            var chip = new Color32[frame.Length];
            var xp = new Color32[frame.Length];

            RectInt healthBounds = ExtractBar(frame, w, h, HealthBarArea, HealthBarColours, HealthEmpty, health, chip);
            RectInt xpBounds = ExtractBar(frame, w, h, XpBarArea, XpBarColours, XpEmpty, xp, null);

            var center = new Vector2(0.5f, 0.5f);
            SavePng($"{UiGenerated}/health_frame.png", frame, w, h);
            skin.HealthFrame = ImportSprite($"{UiGenerated}/health_frame.png", UiPixelsPerUnit, center);
            skin.HealthFill = SaveCropped("health_fill", health, w, h, healthBounds);
            skin.HealthChip = SaveCropped("health_chip", chip, w, h, healthBounds);
            skin.XpFill = SaveCropped("xp_fill", xp, w, h, xpBounds);

            skin.FrameSize = new Vector2Int(w, h);
            skin.HealthRect = healthBounds;
            skin.XpRect = xpBounds;
            skin.PortraitCenter = SocketCenter;
            skin.PortraitDiameter = SocketDiameter;
        }

        /// <summary>
        /// Moves the bar pixels in <paramref name="area"/> out of <paramref name="frame"/> into <paramref name="fill"/>
        /// (and a white copy into <paramref name="white"/>), leaving the trough colour behind.
        /// </summary>
        /// <returns>Bounds of the bar (top-left origin).</returns>
        private static RectInt ExtractBar(Color32[] frame, int w, int h, RectInt area, Color32[] colours, Color32 empty,
                                          Color32[] fill, Color32[] white)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = area.yMin; y < area.yMax; y++)
            {
                for (int x = area.xMin; x < Mathf.Min(area.xMax, w); x++)
                {
                    int i = (h - 1 - y) * w + x; // pixel arrays are bottom-up
                    if (!IsOneOf(frame[i], colours))
                    {
                        continue;
                    }

                    fill[i] = frame[i];
                    if (white != null)
                    {
                        white[i] = new Color32(255, 255, 255, 255);
                    }

                    frame[i] = empty;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            return maxX < 0 ? area : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static bool IsOneOf(Color32 c, Color32[] colours)
        {
            if (c.a == 0)
            {
                return false;
            }

            foreach (Color32 k in colours)
            {
                if (c.r == k.r && c.g == k.g && c.b == k.b)
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite SaveCropped(string name, Color32[] pixels, int w, int h, RectInt rect)
        {
            string path = $"{UiGenerated}/{name}.png";
            SavePng(path, Crop(pixels, w, h, rect), rect.width, rect.height);
            return ImportSprite(path, UiPixelsPerUnit, new Vector2(0.5f, 0.5f));
        }

        private static void BuildPanels(UiSkin skin)
        {
            Color32[] inventory = LoadPixels(InventorySource, out int w, out int h);

            string slotPath = $"{UiGenerated}/slot.png";
            SavePng(slotPath, Crop(inventory, w, h, SlotArt), SlotArt.width, SlotArt.height);
            skin.Slot = ImportSprite(slotPath, UiPixelsPerUnit, new Vector2(0.5f, 0.5f), Vector4.one * SlotBorder);

            // Flatten the slot grid into a plain panel so the frame can hold text.
            for (int y = WindowInterior.yMin; y < WindowInterior.yMax; y++)
            {
                for (int x = WindowInterior.xMin; x < WindowInterior.xMax; x++)
                {
                    inventory[(h - 1 - y) * w + x] = WindowFill;
                }
            }

            string windowPath = $"{UiGenerated}/window.png";
            SavePng(windowPath, inventory, w, h);
            skin.Window = ImportSprite(windowPath, UiPixelsPerUnit, new Vector2(0.5f, 0.5f), Vector4.one * WindowBorder);
        }
    }
}
