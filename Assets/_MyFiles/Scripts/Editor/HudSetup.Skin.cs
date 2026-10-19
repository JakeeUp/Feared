using Brawler.Combat;
using Brawler.Presentation;
using Brawler.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Editor
{
    /// <summary>Skinned half of Setup HUD: the top-left HUD dressed with the GUI pack (see <see cref="UiSkin"/>).</summary>
    public static partial class HudSetup
    {
        // ============ Look ============

        /// <summary>Pack pixels per canvas pixel (1080p reference): crisp 3x pixel art.</summary>
        private const float SkinScale = 3f;

        /// <summary>Resting tint of skinned ability slots (gold while the ability runs).</summary>
        private static readonly Color SlotColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        // ============ Build ============

        /// <summary>
        /// Sword frame with the hero's face in its socket; health drains over the emptied red trough with a white
        /// chip trailing behind, XP fills the green one. Same <see cref="PlayerHud"/> component as the plain HUD.
        /// </summary>
        private static void BuildSkinnedPlayerHud(RectTransform canvas, Health health, PlayerProgression progression, UiSkin skin, Font font)
        {
            Vector2 frameSize = (Vector2)skin.FrameSize * SkinScale;
            RectTransform hud = Rect("PlayerHud", canvas, new Vector2(0f, 1f), new Vector2(24f, -16f), frameSize);
            Img(Rect("Frame", hud, new Vector2(0f, 1f), Vector2.zero, frameSize), skin.HealthFrame, Color.white);

            // Bars, laid over their troughs.
            Image chip = Fill(PixelRect("Chip", hud, skin.HealthRect), skin.HealthChip, ChipColor);
            Image fill = Fill(PixelRect("Fill", hud, skin.HealthRect), skin.HealthFill, Color.white);
            Image xp = Fill(PixelRect("Xp", hud, skin.XpRect), skin.XpFill, Color.white);
            xp.fillAmount = 0f;

            RectTransform valueRect = PixelRect("Value", hud, skin.HealthRect);
            Text healthText = Label(valueRect, font, 18, Color.white, TextAnchor.MiddleRight, "100 / 100");
            healthText.rectTransform.offsetMax += new Vector2(-14f, 0f); // clear of the slanted bar end

            // Portrait: the hero's face, clipped to the round socket.
            if (skin.Portrait != null)
            {
                float diameter = skin.PortraitDiameter * SkinScale;
                Vector2 center = new Vector2(skin.PortraitCenter.x, -skin.PortraitCenter.y) * SkinScale;
                RectTransform socket = Rect("Portrait", hud, new Vector2(0f, 1f), center - new Vector2(diameter, -diameter) * 0.5f, Vector2.one * diameter);
                Img(socket, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), new Color(0.12f, 0.1f, 0.14f, 1f));
                socket.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                RectTransform face = Rect("Face", socket, new Vector2(0.5f, 0.5f), new Vector2(0f, -diameter * 0.08f), new Vector2(diameter * 1.3f, diameter));
                Img(face, skin.Portrait, Color.white).preserveAspect = true;
            }

            // Level under the socket, upgrade prompt under the bars.
            float socketBottom = (skin.PortraitCenter.y + skin.PortraitDiameter * 0.5f) * SkinScale;
            Text level = Label(Rect("Level", hud, new Vector2(0f, 1f), new Vector2(skin.PortraitCenter.x * SkinScale - 60f, -socketBottom - 2f), new Vector2(120f, 30f)),
                               font, 22, AccentColor, TextAnchor.MiddleCenter, "LV 1");
            level.fontStyle = FontStyle.Bold;
            Text points = Label(Rect("Points", hud, new Vector2(0f, 1f), new Vector2(skin.XpRect.x * SkinScale, -(skin.XpRect.yMax + 4) * SkinScale), new Vector2(460f, 28f)),
                                font, 18, AccentColor, TextAnchor.MiddleLeft, "");
            points.enabled = false;

            var component = hud.gameObject.AddComponent<PlayerHud>();
            var so = new SerializedObject(component);
            so.FindProperty("_health").objectReferenceValue = health;
            so.FindProperty("_progression").objectReferenceValue = progression;
            so.FindProperty("_healthFill").objectReferenceValue = fill;
            so.FindProperty("_healthChip").objectReferenceValue = chip;
            so.FindProperty("_healthText").objectReferenceValue = healthText;
            so.FindProperty("_xpFill").objectReferenceValue = xp;
            so.FindProperty("_levelText").objectReferenceValue = level;
            so.FindProperty("_pointsText").objectReferenceValue = points;
            so.ApplyModifiedProperties();
        }

        /// <summary>Child rect covering <paramref name="pixels"/> of the skin art (top-left origin), scaled.</summary>
        private static RectTransform PixelRect(string name, RectTransform parent, RectInt pixels)
        {
            return Rect(name, parent, new Vector2(0f, 1f), new Vector2(pixels.x, -pixels.y) * SkinScale, new Vector2(pixels.width, pixels.height) * SkinScale);
        }
    }
}
