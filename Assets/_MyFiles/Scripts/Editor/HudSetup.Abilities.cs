using System.IO;
using System.Text;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Player;
using Brawler.Presentation;
using Brawler.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>Ability half of Setup HUD: wallet, loadout, ability assets, the caster and its visuals, and the HUD slots.</summary>
    public static partial class HudSetup
    {
        // ============ Paths ============

        private const string AbilityDir = Root + "/Data/Progression/Abilities";
        private const string RingAttackPath = Root + "/Data/Player/Attack_BladeRing.asset";
        private const string CombatSettingsPath = Root + "/Data/Combat/CombatSettings.asset";
        private const string BubbleMaterialPath = Root + "/Art/Materials/M_Bubble.mat";
        private const string SwordSpritePath = "Assets/_ThirdParty/AssetPack/Bonus/sword.png";

        // ============ Look ============

        /// <summary>The 18 px pack sword at this size is ~0.75 units, about the character's arm span.</summary>
        private const float SwordPixelsPerUnit = 24f;
        private const int BladeCount = 4;
        private static readonly Color BubbleColor = new Color(0.45f, 0.85f, 1f, 0.22f);
        private const float BubbleSize = 1.5f;

        // ============ Setup ============

        private static AbilityLoadout SetupAbilities(PlayerController player, Health health, EventBusAsset eventBus, StringBuilder log)
        {
            Directory.CreateDirectory(AbilityDir);
            AbilityDefinition ring = EnsureAbility(AbilityType.BladeRing, "Blade Ring", "Swords orbit you, cutting and knocking back.", 400, 12f, 5f, 0f, log);
            AbilityDefinition shield = EnsureAbility(AbilityType.BubbleShield, "Bubble Shield", "Blocks all damage for a few seconds.", 300, 15f, 4f, 0f, log);
            AbilityDefinition mend = EnsureAbility(AbilityType.Mend, "Mend", "Heal 35% of your max health.", 250, 20f, 0f, 0.35f, log);

            var wallet = HeroSetup.GetOrAdd<ScoreWallet>(player.gameObject);
            var loadout = HeroSetup.GetOrAdd<AbilityLoadout>(player.gameObject);
            var loadoutSo = new SerializedObject(loadout);
            HeroSetup.SetArray(loadoutSo.FindProperty("_abilities"), ring, shield, mend);
            loadoutSo.FindProperty("_wallet").objectReferenceValue = wallet;
            loadoutSo.ApplyModifiedProperties();

            BuildCaster(player, health, loadout, eventBus, log);
            return loadout;
        }

        private static void BuildCaster(PlayerController player, Health health, AbilityLoadout loadout, EventBusAsset eventBus, StringBuilder log)
        {
            Transform root = player.transform;
            BillboardSprite playerBillboard = player.GetComponentInChildren<BillboardSprite>(true);
            Transform cameraTransform = playerBillboard != null ? playerBillboard.CameraTransform : null;

            // Blade ring hitbox: radial knockback around the player, same team so it never hits the player.
            Transform ringTransform = root.Find("BladeRingHitbox") ?? NewChild(root, "BladeRingHitbox");
            var ringHitbox = HeroSetup.GetOrAdd<Hitbox>(ringTransform.gameObject);
            var ringSo = new SerializedObject(ringHitbox);
            ringSo.FindProperty("_combatSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatSettings>(CombatSettingsPath);
            ringSo.FindProperty("_eventBus").objectReferenceValue = eventBus;
            ringSo.FindProperty("_owner").objectReferenceValue = root;
            ringSo.FindProperty("_ownerHurtbox").objectReferenceValue = player.GetComponentInChildren<Hurtbox>(true);
            ringSo.FindProperty("_team").enumValueIndex = (int)CombatTeam.Player;
            ringSo.FindProperty("_radialKnockback").boolValue = true;
            ringSo.ApplyModifiedProperties();

            // Blades
            Sprite sword = EnsureSwordSprite();
            Transform bladesRoot = root.Find("Blades") ?? NewChild(root, "Blades");
            var blades = new Object[BladeCount];
            Material spriteDefault = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.default2DMaterial : null;
            for (int i = 0; i < BladeCount; i++)
            {
                Transform blade = bladesRoot.Find($"Blade_{i}") ?? NewChild(bladesRoot, $"Blade_{i}");
                var sr = HeroSetup.GetOrAdd<SpriteRenderer>(blade.gameObject);
                sr.sprite = sword;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
                if (spriteDefault != null)
                {
                    sr.sharedMaterial = spriteDefault;
                }

                blades[i] = blade;
            }

            // Bubble
            Transform bubble = root.Find("Bubble");
            if (bubble == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Undo.RegisterCreatedObjectUndo(sphere, "Create Bubble");
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.name = "Bubble";
                bubble = sphere.transform;
                bubble.SetParent(root, false);
            }

            bubble.localPosition = new Vector3(0f, 0.55f, 0f);
            bubble.localScale = Vector3.one * BubbleSize;
            var bubbleRenderer = bubble.GetComponent<MeshRenderer>();
            bubbleRenderer.sharedMaterial = EnsureTransparentMaterial(BubbleMaterialPath, BubbleColor);
            bubbleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bubbleRenderer.receiveShadows = false;

            var caster = HeroSetup.GetOrAdd<AbilityCaster>(player.gameObject);
            var so = new SerializedObject(caster);
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_loadout").objectReferenceValue = loadout;
            so.FindProperty("_health").objectReferenceValue = health;
            so.FindProperty("_flash").objectReferenceValue = player.GetComponentInChildren<HitFlash>(true);
            so.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            so.FindProperty("_bladeRingAction").objectReferenceValue = HeroSetup.FindAction("BladeRing");
            so.FindProperty("_shieldAction").objectReferenceValue = HeroSetup.FindAction("BubbleShield");
            so.FindProperty("_mendAction").objectReferenceValue = HeroSetup.FindAction("Mend");
            so.FindProperty("_ringHitbox").objectReferenceValue = ringHitbox;
            so.FindProperty("_ringAttack").objectReferenceValue = EnsureRingAttack(log);
            HeroSetup.SetArray(so.FindProperty("_blades"), blades);
            so.FindProperty("_bubble").objectReferenceValue = bubble;
            so.ApplyModifiedProperties();

            log.AppendLine("  * abilities: Blade Ring [1/Y], Bubble Shield [2/LB], Mend [3/LT]; bought in the Tab shop with score");
        }

        // ============ HUD ============

        private static void BuildAbilityBar(RectTransform canvas, AbilityLoadout loadout, Sprite white, Font font, UiSkin skin)
        {
            RectTransform bar = Rect("AbilityBar", canvas, new Vector2(0f, 0f), new Vector2(36f, 32f), new Vector2(300f, 96f));
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var slots = new (AbilityType Type, Sprite Icon, Color Tint, string Key, string Glyph)[]
            {
                (AbilityType.BladeRing, EnsureSwordSprite(), Color.white, "1", ""),
                (AbilityType.BubbleShield, knob, new Color(0.45f, 0.85f, 1f, 1f), "2", ""),
                (AbilityType.Mend, knob, new Color(0.45f, 1f, 0.55f, 1f), "3", "+"),
            };

            var component = bar.gameObject.AddComponent<AbilityBar>();
            var so = new SerializedObject(component);
            SerializedProperty slotsProperty = so.FindProperty("_slots");
            slotsProperty.arraySize = slots.Length;

            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform frame = Rect(slots[i].Type.ToString(), bar, new Vector2(0f, 0f), new Vector2(i * 100f, 0f), new Vector2(88f, 88f));
                Image frameImage = skin != null ? Sliced(Img(frame, skin.Slot, SlotColor)) : Img(frame, white, FrameColor);

                RectTransform iconRect = Rect("Icon", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                Image icon = Img(iconRect, slots[i].Icon, slots[i].Tint);
                icon.preserveAspect = true;
                if (slots[i].Glyph.Length > 0)
                {
                    Label(Rect("Glyph", iconRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f)), font, 44, Color.white, TextAnchor.MiddleCenter, slots[i].Glyph);
                }

                RectTransform cooldownRect = Rect("Cooldown", frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
                Image cooldown = Img(cooldownRect, white, new Color(0f, 0f, 0f, 0.7f));
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = (int)Image.Origin360.Top;
                cooldown.fillClockwise = false;
                cooldown.fillAmount = 0f;

                Label(Rect("Key", frame, new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(30f, 26f)), font, 20, AccentColor, TextAnchor.LowerRight, slots[i].Key);

                SerializedProperty slot = slotsProperty.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("Type").enumValueIndex = (int)slots[i].Type;
                slot.FindPropertyRelative("Icon").objectReferenceValue = icon;
                slot.FindPropertyRelative("Cooldown").objectReferenceValue = cooldown;
                slot.FindPropertyRelative("Frame").objectReferenceValue = frameImage;
            }

            so.FindProperty("_loadout").objectReferenceValue = loadout;
            so.FindProperty("_frameColor").colorValue = skin != null ? SlotColor : FrameColor;
            so.ApplyModifiedProperties();
        }

        // ============ Assets ============

        private static AbilityDefinition EnsureAbility(AbilityType type, string displayName, string description,
                                                       int cost, float cooldown, float duration, float value, StringBuilder log)
        {
            string path = $"{AbilityDir}/Ability_{type}.asset";
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability != null)
            {
                return ability; // keep hand-tuned prices and timings
            }

            ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.EditorSet(type, displayName, description, cost, cooldown, duration, value);
            AssetDatabase.CreateAsset(ability, path);
            log.AppendLine($"  * created {path}");
            return ability;
        }

        /// <summary>The ring's hit: light damage, strong outward push, small hitstop so a crowd doesn't freeze the game.</summary>
        private static AttackData EnsureRingAttack(StringBuilder log)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(RingAttackPath);
            if (attack != null)
            {
                return attack;
            }

            attack = ScriptableObject.CreateInstance<AttackData>();
            AssetDatabase.CreateAsset(attack, RingAttackPath);
            var so = new SerializedObject(attack);
            so.FindProperty("_damage").intValue = 5;
            so.FindProperty("_knockback").vector2Value = new Vector2(5f, 0f);
            so.FindProperty("_hitstunFrames").intValue = 14;
            SerializedProperty frames = so.FindProperty("_frames");
            frames.FindPropertyRelative("_startup").intValue = 0;
            frames.FindPropertyRelative("_active").intValue = 1;
            frames.FindPropertyRelative("_recovery").intValue = 0;
            frames.FindPropertyRelative("_cancelWindowStart").intValue = 0;
            frames.FindPropertyRelative("_cancelWindowEnd").intValue = 0;
            so.FindProperty("_hitboxSize").vector2Value = new Vector2(2.4f, 1.1f);
            so.FindProperty("_hitboxOffset").vector2Value = new Vector2(0f, 0.55f);
            so.FindProperty("_lungeSpeed").floatValue = 0f;
            so.FindProperty("_hitStopFrames").intValue = 1;
            so.FindProperty("_screenShake").floatValue = 0.05f;
            so.ApplyModifiedProperties();
            log.AppendLine($"  * created {RingAttackPath}");
            return attack;
        }

        private static Sprite EnsureSwordSprite()
        {
            var importer = AssetImporter.GetAtPath(SwordSpritePath) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, SwordPixelsPerUnit))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = SwordPixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SwordSpritePath);
        }

        /// <summary>URP Unlit set to transparent alpha blending (the bubble).</summary>
        private static Material EnsureTransparentMaterial(string path, Color color)
        {
            Material material = EnemySetup.EnsureUnlitMaterial(path, color);
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f);   // alpha
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
