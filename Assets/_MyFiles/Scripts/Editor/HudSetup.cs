using System.IO;
using System.Text;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Player;
using Brawler.Presentation;
using Brawler.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// Editor tooling: menu <c>Brawler/Setup HUD &amp; Progression</c>. Adds XP/levels/upgrades, the score wallet and
    /// the abilities (see HudSetup.Abilities.cs) to the player, an XP orb spawner, and builds the screen UI from code:
    /// top-left health/XP/level HUD, bottom-left ability slots, top-right score, middle-right hit streak, and the Tab
    /// menu (upgrades + shop, with an EventSystem for mouse/gamepad). Also an "everything" menu that runs
    /// Setup Hero, Setup Enemy and this in order.
    /// </summary>
    /// <remarks>Safe to re-run: the HUD canvas is rebuilt; settings/ability assets are created once and kept.</remarks>
    public static partial class HudSetup
    {
        // ============ Paths ============

        private const string Root = "Assets/_MyFiles";
        private const string ProgressionSettingsPath = Root + "/Data/Progression/ProgressionSettings.asset";
        private const string EventBusPath = Root + "/Data/Core/EventBus.asset";
        private const string OrbMaterialPath = Root + "/Art/Materials/M_XpOrb.mat";
        private const string WhiteSpritePath = Root + "/Art/UI/T_UIWhite.png";
        private const string HudName = "HUD";

        // ============ Look ============

        private static readonly Color FrameColor = new Color(0.04f, 0.04f, 0.06f, 0.9f);
        private static readonly Color HealthColor = new Color(0.86f, 0.16f, 0.22f, 1f);
        private static readonly Color ChipColor = new Color(1f, 0.82f, 0.45f, 1f);
        private static readonly Color XpColor = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.35f, 1f);
        private static readonly Color OrbColor = new Color(0.5f, 2.4f, 1.8f, 1f); // HDR: glows through bloom
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ============ Menu ============

        [MenuItem("Brawler/Setup Everything (Hero, Enemy, HUD, Packs)")]
        public static void RunAll()
        {
            HeroSetup.Run();
            EnemySetup.Run();
            Run();
            PackSetup.Run(); // after the HUD: it wires effects into the ability caster built there
        }

        [MenuItem("Brawler/Setup HUD & Progression")]
        public static void Run()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Health health = player != null ? player.GetComponent<Health>() : null;
            if (player == null || health == null)
            {
                EditorUtility.DisplayDialog("Setup HUD", "Needs the player with Health in the open scene. Run Setup Hero and Setup Enemy first.", "OK");
                return;
            }

            var log = new StringBuilder("[Brawler] Setup HUD & Progression\n");
            var eventBus = AssetDatabase.LoadAssetAtPath<EventBusAsset>(EventBusPath);
            ProgressionSettings settings = EnsureAsset<ProgressionSettings>(ProgressionSettingsPath, log);
            PlayerProgression progression = WireProgression(player, health, settings, log);
            BuildOrbSpawner(player, progression, settings, eventBus, log);
            AbilityLoadout loadout = SetupAbilities(player, health, eventBus, log);

            Sprite white = EnsureWhiteSprite();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UiSkin skin = PackSetup.EnsureUiSkin(log);
            BuildCanvas(player, health, progression, loadout, eventBus, white, font, skin, log);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ============ Gameplay ============

        private static PlayerProgression WireProgression(PlayerController player, Health health, ProgressionSettings settings, StringBuilder log)
        {
            var progression = HeroSetup.GetOrAdd<PlayerProgression>(player.gameObject);
            var so = new SerializedObject(progression);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_health").objectReferenceValue = health;
            so.FindProperty("_hitbox").objectReferenceValue = player.GetComponentInChildren<Hitbox>(true);
            so.ApplyModifiedProperties();
            log.AppendLine("  * player: PlayerProgression (XP, levels, Vitality/Strength upgrades)");
            return progression;
        }

        private static void BuildOrbSpawner(PlayerController player, PlayerProgression progression, ProgressionSettings settings, EventBusAsset eventBus, StringBuilder log)
        {
            Material material = EnemySetup.EnsureUnlitMaterial(OrbMaterialPath, OrbColor);
            GameObject go = GameObject.Find("XpOrbs") ?? HeroSetup.CreateRoot("XpOrbs");
            var spawner = HeroSetup.GetOrAdd<XpOrbSpawner>(go);
            var so = new SerializedObject(spawner);
            so.FindProperty("_eventBus").objectReferenceValue = eventBus;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_collector").objectReferenceValue = progression;
            so.FindProperty("_target").objectReferenceValue = player.transform;
            so.FindProperty("_material").objectReferenceValue = material;
            so.ApplyModifiedProperties();
            log.AppendLine("  * scene: XP orb spawner (glowing orbs home in on the player)");
        }

        // ============ UI ============

        private static void BuildCanvas(PlayerController player, Health health, PlayerProgression progression, AbilityLoadout loadout,
                                        EventBusAsset eventBus, Sprite white, Font font, UiSkin skin, StringBuilder log)
        {
            GameObject old = GameObject.Find(HudName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            var canvasGo = new GameObject(HudName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            EnsureEventSystem();
            if (skin != null)
            {
                BuildSkinnedPlayerHud(root, health, progression, skin, font);
            }
            else
            {
                BuildPlayerHud(root, health, progression, white, font);
            }

            BuildComboCounter(root, player.transform, loadout.Wallet, eventBus, font);
            BuildAbilityBar(root, loadout, white, font, skin);
            BuildUpgradeMenu(root, player, progression, loadout, white, font, skin);
            log.AppendLine("  * UI: health/XP/level top-left, abilities bottom-left, score top-right, hit streak middle-right, [Tab] upgrades + shop");
        }

        private static void BuildPlayerHud(RectTransform canvas, Health health, PlayerProgression progression, Sprite white, Font font)
        {
            RectTransform hud = Rect("PlayerHud", canvas, new Vector2(0f, 1f), new Vector2(36f, -32f), new Vector2(540f, 120f));

            // Level badge
            RectTransform badge = Rect("LevelBadge", hud, new Vector2(0f, 1f), Vector2.zero, new Vector2(96f, 96f));
            Img(badge, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), FrameColor);
            RectTransform badgeRing = Rect("Ring", badge, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(84f, 84f));
            Img(badgeRing, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), new Color(0.12f, 0.12f, 0.16f, 1f));
            Text level = Label(Rect("Level", badge, new Vector2(0f, 1f), Vector2.zero, new Vector2(96f, 96f)), font, 28, Color.white, TextAnchor.MiddleCenter, "LV 1");

            // Health
            RectTransform frame = Rect("HealthFrame", hud, new Vector2(0f, 1f), new Vector2(110f, -14f), new Vector2(410f, 32f));
            Img(frame, white, FrameColor);
            Image chip = Fill(Rect("Chip", frame, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(402f, 24f)), white, ChipColor);
            Image fill = Fill(Rect("Fill", frame, new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(402f, 24f)), white, HealthColor);
            Text healthText = Label(Rect("Value", frame, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(398f, 32f)), font, 18, Color.white, TextAnchor.MiddleRight, "100 / 100");

            // XP
            RectTransform xpFrame = Rect("XpFrame", hud, new Vector2(0f, 1f), new Vector2(110f, -52f), new Vector2(410f, 14f));
            Img(xpFrame, white, FrameColor);
            Image xp = Fill(Rect("Fill", xpFrame, new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(404f, 8f)), white, XpColor);
            xp.fillAmount = 0f;

            Text points = Label(Rect("Points", hud, new Vector2(0f, 1f), new Vector2(110f, -70f), new Vector2(430f, 28f)), font, 18, AccentColor, TextAnchor.MiddleLeft, "");
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

        private static void BuildComboCounter(RectTransform canvas, Transform player, ScoreWallet wallet, EventBusAsset eventBus, Font font)
        {
            // Full-screen container: the score sits top-right, the hit streak middle-right so they never overlap.
            RectTransform root = Rect("ComboCounter", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(root);
            Text score = Label(Rect("Score", root, new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(460f, 40f)), font, 30, Color.white, TextAnchor.MiddleRight, "SCORE  0");

            RectTransform streak = Rect("Streak", root, new Vector2(1f, 0.5f), new Vector2(-36f, 80f), new Vector2(460f, 100f));
            var group = streak.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            RectTransform hitsRect = Rect("Hits", streak, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(460f, 100f));
            hitsRect.pivot = new Vector2(1f, 0.5f); // punch scales from the right edge
            Text hits = Label(hitsRect, font, 72, AccentColor, TextAnchor.MiddleRight, "0 HITS");
            hits.fontStyle = FontStyle.Bold;

            var component = root.gameObject.AddComponent<ComboCounter>();
            var so = new SerializedObject(component);
            so.FindProperty("_eventBus").objectReferenceValue = eventBus;
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_wallet").objectReferenceValue = wallet;
            so.FindProperty("_hitsText").objectReferenceValue = hits;
            so.FindProperty("_scoreText").objectReferenceValue = score;
            so.FindProperty("_streakGroup").objectReferenceValue = group;
            so.ApplyModifiedProperties();
        }

        private static void BuildUpgradeMenu(RectTransform canvas, PlayerController player, PlayerProgression progression, AbilityLoadout loadout,
                                             Sprite white, Font font, UiSkin skin)
        {
            Sprite button = skin != null ? skin.Slot : white;
            RectTransform menuRoot = Rect("UpgradeMenu", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(menuRoot);

            RectTransform panel = Rect("Panel", menuRoot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(panel);
            Img(panel, white, new Color(0f, 0f, 0f, 0.6f));

            RectTransform window = Rect("Window", panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 520f));
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            if (skin != null)
            {
                Sliced(Img(window, skin.Window, Color.white));
            }
            else
            {
                Img(window, white, new Color(0.06f, 0.06f, 0.09f, 0.97f));
            }

            // Left column: level-up points.
            Label(Rect("Title", window, new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(520f, 50f)), font, 40, Color.white, TextAnchor.MiddleCenter, "UPGRADES")
                .fontStyle = FontStyle.Bold;
            Text points = Label(Rect("Points", window, new Vector2(0f, 1f), new Vector2(40f, -70f), new Vector2(520f, 36f)), font, 26, AccentColor, TextAnchor.MiddleCenter, "POINTS  0");

            Button vitality = MenuButton(window, "Vitality", new Vector2(40f, -124f), new Vector2(520f, 96f), button, font, out Text vitalityText);
            Button strength = MenuButton(window, "Strength", new Vector2(40f, -232f), new Vector2(520f, 96f), button, font, out Text strengthText);

            // Right column: the score shop.
            BuildShop(window, loadout, button, font);

            Button close = MenuButton(window, "Close", new Vector2(490f, -444f), new Vector2(200f, 56f), button, font, out Text closeText);
            closeText.text = "CLOSE  [TAB]";

            var component = menuRoot.gameObject.AddComponent<UpgradeMenu>();
            var so = new SerializedObject(component);
            so.FindProperty("_progression").objectReferenceValue = progression;
            so.FindProperty("_toggleAction").objectReferenceValue = HeroSetup.FindAction("Menu");
            HeroSetup.SetArray(so.FindProperty("_disableWhileOpen"), player.GetComponent<PlayerInputReader>());
            so.FindProperty("_panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("_pointsText").objectReferenceValue = points;
            so.FindProperty("_vitalityButton").objectReferenceValue = vitality;
            so.FindProperty("_vitalityText").objectReferenceValue = vitalityText;
            so.FindProperty("_strengthButton").objectReferenceValue = strength;
            so.FindProperty("_strengthText").objectReferenceValue = strengthText;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.ApplyModifiedProperties();
            panel.gameObject.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        // ============ UI Helpers ============

        /// <summary>Child RectTransform anchored and pivoted at <paramref name="anchor"/>.</summary>
        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Img(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Pixel-art 9-slice with borders drawn at <see cref="SkinScale"/>x.</summary>
        private static Image Sliced(Image image)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / SkinScale;
            return image;
        }

        private static Image Fill(RectTransform rect, Sprite sprite, Color color)
        {
            Image image = Img(rect, sprite, color);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            return image;
        }

        private static Text Label(RectTransform rect, Font font, int size, Color color, TextAnchor alignment, string text)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        private static void BuildShop(RectTransform window, AbilityLoadout loadout, Sprite button, Font font)
        {
            RectTransform shop = Rect("Shop", window, new Vector2(0f, 1f), new Vector2(620f, 0f), new Vector2(520f, 440f));
            Label(Rect("Title", shop, new Vector2(0f, 1f), new Vector2(0f, -18f), new Vector2(520f, 50f)), font, 40, Color.white, TextAnchor.MiddleCenter, "SHOP")
                .fontStyle = FontStyle.Bold;
            Text balance = Label(Rect("Balance", shop, new Vector2(0f, 1f), new Vector2(0f, -70f), new Vector2(520f, 36f)), font, 26, AccentColor, TextAnchor.MiddleCenter, "SCORE  0");

            Button ring = MenuButton(shop, "BladeRing", new Vector2(0f, -124f), new Vector2(520f, 92f), button, font, out Text ringText);
            Button shield = MenuButton(shop, "BubbleShield", new Vector2(0f, -224f), new Vector2(520f, 92f), button, font, out Text shieldText);
            Button mend = MenuButton(shop, "Mend", new Vector2(0f, -324f), new Vector2(520f, 92f), button, font, out Text mendText);
            foreach (Text text in new[] { ringText, shieldText, mendText })
            {
                text.fontSize = 22;
            }

            var component = shop.gameObject.AddComponent<AbilityShop>();
            var so = new SerializedObject(component);
            so.FindProperty("_loadout").objectReferenceValue = loadout;
            so.FindProperty("_balanceText").objectReferenceValue = balance;
            SerializedProperty entries = so.FindProperty("_entries");
            entries.arraySize = 3;
            SetEntry(entries.GetArrayElementAtIndex(0), AbilityType.BladeRing, ring, ringText);
            SetEntry(entries.GetArrayElementAtIndex(1), AbilityType.BubbleShield, shield, shieldText);
            SetEntry(entries.GetArrayElementAtIndex(2), AbilityType.Mend, mend, mendText);
            so.ApplyModifiedProperties();
        }

        private static void SetEntry(SerializedProperty entry, AbilityType type, Button button, Text label)
        {
            entry.FindPropertyRelative("Type").enumValueIndex = (int)type;
            entry.FindPropertyRelative("Button").objectReferenceValue = button;
            entry.FindPropertyRelative("Label").objectReferenceValue = label;
        }

        /// <summary>A button; a 9-sliced (skinned) sprite gets light tints, the plain white sprite dark ones.</summary>
        private static Button MenuButton(RectTransform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Font font, out Text label)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), position, size);
            Image image = Img(rect, sprite, Color.white);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                Sliced(image);
                colors.normalColor = new Color(0.78f, 0.78f, 0.84f, 1f);
                colors.highlightedColor = Color.white;
                colors.selectedColor = new Color(1f, 0.92f, 0.74f, 1f);
                colors.pressedColor = new Color(1f, 0.78f, 0.42f, 1f);
                colors.disabledColor = new Color(0.42f, 0.42f, 0.48f, 0.8f);
            }
            else
            {
                colors.normalColor = new Color(0.14f, 0.14f, 0.19f, 1f);
                colors.highlightedColor = new Color(0.24f, 0.24f, 0.33f, 1f);
                colors.selectedColor = new Color(0.3f, 0.28f, 0.42f, 1f);
                colors.pressedColor = new Color(0.4f, 0.36f, 0.2f, 1f);
                colors.disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.6f);
            }

            button.colors = colors;

            RectTransform textRect = Rect("Label", rect, new Vector2(0f, 1f), Vector2.zero, size);
            label = Label(textRect, font, 26, Color.white, TextAnchor.MiddleCenter, name.ToUpperInvariant());
            return button;
        }

        // ============ Assets ============

        /// <summary>A plain white sprite (the built-in UI sprites have rounded corners that smear when Filled).</summary>
        internal static Sprite EnsureWhiteSprite()
        {
            if (!File.Exists(WhiteSpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(WhiteSpritePath));
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                tex.SetPixels32(pixels);
                File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(WhiteSpritePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        private static T EnsureAsset<T>(string path, StringBuilder log) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            log.AppendLine($"  * created {path}");
            return asset;
        }
    }
}
