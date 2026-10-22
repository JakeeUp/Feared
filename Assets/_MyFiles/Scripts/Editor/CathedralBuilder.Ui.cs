using System.Text;
using Brawler.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// The level's own overlay canvas ("LevelUI", kept apart from the HUD so re-running Setup HUD never deletes it):
    /// the section title banner, the toast line and the boss bar (the Androx boss bar art, drained from the right).
    /// </summary>
    public static partial class CathedralBuilder
    {
        // ============ Look ============

        private const string LevelUiName = "LevelUI";
        private const string BossBarSheet = "Assets/_ThirdParty/boss_counters/boss_bar1-Sheet.png";

        /// <summary>The last (fully grown) row of the boss bar's appear animation.</summary>
        private static readonly RectInt BossBarRect = new RectInt(0, 416, 384, 32);

        /// <summary>The fillable inside of the bar art, in its pixels (left, bottom, right, top insets).</summary>
        private static readonly Vector4 BossBarInset = new Vector4(10f, 10f, 10f, 10f);

        private const float BossBarScale = 2f;

        private static readonly Color TitleColor = new Color(1f, 0.86f, 0.55f, 1f);
        private static readonly Color SubtitleColor = new Color(0.85f, 0.82f, 0.9f, 1f);

        // ============ Build ============

        private static void BuildUi(Ctx ctx, StringBuilder log)
        {
            GameObject old = GameObject.Find(LevelUiName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            var canvasGo = new GameObject(LevelUiName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Level UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite white = HudSetup.EnsureWhiteSprite();
            var root = (RectTransform)canvasGo.transform;

            BuildBanner(root, font);
            BuildBossBar(root, font, white);
            ctx.ScreenFade = BuildScreenFade(root, white);
            log.AppendLine("  * UI: section banner, toasts, boss bar and door fade on the LevelUI canvas");
        }

        /// <summary>Full-screen black overlay, transparent until a <see cref="Doorway"/> fades through it.</summary>
        private static CanvasGroup BuildScreenFade(RectTransform root, Sprite white)
        {
            RectTransform rect = Rect(root, "ScreenFade", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = white;
            image.color = Color.black;
            image.raycastTarget = false;
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static void BuildBanner(RectTransform root, Font font)
        {
            RectTransform banner = Rect(root, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1400f, 200f));
            var titleGroup = banner.gameObject.AddComponent<CanvasGroup>();
            titleGroup.interactable = false;
            titleGroup.blocksRaycasts = false;
            Text title = Label(Rect(banner, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 100f)), font, 76, TitleColor);
            Text subtitle = Label(Rect(banner, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(1400f, 50f)), font, 30, SubtitleColor);
            subtitle.fontStyle = FontStyle.Italic;

            RectTransform toastRect = Rect(root, "Toast", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1400f, 60f));
            var toastGroup = toastRect.gameObject.AddComponent<CanvasGroup>();
            toastGroup.interactable = false;
            toastGroup.blocksRaycasts = false;
            Text toast = Label(Rect(toastRect, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 60f)), font, 30, Color.white);

            var component = root.gameObject.AddComponent<LevelBanner>();
            EnemySetup.Set(component, so =>
            {
                so.FindProperty("_titleGroup").objectReferenceValue = titleGroup;
                so.FindProperty("_title").objectReferenceValue = title;
                so.FindProperty("_subtitle").objectReferenceValue = subtitle;
                so.FindProperty("_toastGroup").objectReferenceValue = toastGroup;
                so.FindProperty("_toast").objectReferenceValue = toast;
            });
        }

        private static void BuildBossBar(RectTransform root, Font font, Sprite white)
        {
            Sprite barSprite = null;
            string path = $"{LevelGenerated}/boss_bar.png";
            if (PackSetup.CropToPng(BossBarSheet, BossBarRect, path))
            {
                barSprite = PackSetup.ImportSprite(path, 100f, new Vector2(0.5f, 0.5f));
            }

            Vector2 size = new Vector2(BossBarRect.width, BossBarRect.height) * BossBarScale;
            RectTransform bar = Rect(root, "BossBar", new Vector2(0.5f, 0f), new Vector2(0f, 80f), size);
            var group = bar.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image frame = bar.gameObject.AddComponent<Image>();
            frame.sprite = barSprite != null ? barSprite : white;
            frame.color = barSprite != null ? Color.white : new Color(0.75f, 0.1f, 0.15f, 1f);
            frame.raycastTarget = false;

            // Overlays live in the bar's inside and grow leftward from its right edge.
            RectTransform inner = Rect(bar, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(BossBarInset.x, BossBarInset.y) * BossBarScale;
            inner.offsetMax = -new Vector2(BossBarInset.z, BossBarInset.w) * BossBarScale;
            float innerWidth = size.x - (BossBarInset.x + BossBarInset.z) * BossBarScale;

            RectTransform chip = Overlay(inner, "Chip", white, new Color(1f, 0.92f, 0.75f, 0.9f));
            RectTransform drain = Overlay(inner, "Drain", white, new Color(0.04f, 0.02f, 0.05f, 0.88f));

            Text name = Label(Rect(bar, "Name", new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(900f, 40f)), font, 30, TitleColor);

            var component = bar.gameObject.AddComponent<BossHealthBar>();
            EnemySetup.Set(component, so =>
            {
                so.FindProperty("_group").objectReferenceValue = group;
                so.FindProperty("_drain").objectReferenceValue = drain;
                so.FindProperty("_chip").objectReferenceValue = chip;
                so.FindProperty("_innerWidth").floatValue = innerWidth;
                so.FindProperty("_name").objectReferenceValue = name;
            });
        }

        // ============ Helpers ============

        private static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Overlay(RectTransform inner, string name, Sprite white, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(inner, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 0f);
            Image image = go.AddComponent<Image>();
            image.sprite = white;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text Label(RectTransform rect, Font font, int size, Color color)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }
    }
}
