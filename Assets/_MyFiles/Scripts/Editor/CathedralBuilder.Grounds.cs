using System.IO;
using Brawler.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>
    /// The Cathedral Grounds: the outdoor approach, built as an HD-2D diorama (Octopath Traveler) rather than a strip.
    /// The player walks freely in X and Z through a graveyard: a path winds into the depth, past fenced plots, crypts
    /// and pines, up a broad stair that climbs away from the camera to the west front, and in through the doors (a
    /// faded cut to the nave). A high, narrow-lens camera from far back flattens it into a miniature.
    /// </summary>
    /// <remarks>
    /// Props are Kenney's CC0 Graveyard Kit (kenney.nl/assets/graveyard-kit) under one tinted material; the facade
    /// is the same stone as the interior. Everything the player could walk into gets a collider, so the depth matters.
    /// </remarks>
    public static partial class CathedralBuilder
    {
        private const string GraveyardKit = "Assets/_ThirdParty/Kenney_graveyard-kit/FBX";
        private const string GraveyardMaterialPath = "Assets/_ThirdParty/_Generated/Graveyard/M_Graveyard.mat";

        /// <summary>Kit units to world units: the hero is 1 tall, a headstone should reach about his chest.</summary>
        private const float KitScale = 2f;

        // The grounds sit well west of the nave; the doors cut between them.
        private const float GroundsWest = -116f, GroundsEast = -36f, GroundsFront = -18f, GroundsBack = 13f;
        private const float FacadeX = -60f, TerraceY = 2f, TerraceZ = 10f, FacadeZ = 13.5f;

        private static readonly Vector3 GroundsStart = new Vector3(-112f, 0.05f, -8f);

        /// <summary>
        /// The walked lines of the grounds (x, z), shared by the ground painter and the dressing: the main path from the
        /// west gate to the stair, and the side paths that branch off to the crypts and the chests.
        /// </summary>
        private static readonly (Vector2[] Points, float HalfWidth)[] GroundsPaths =
        {
            (new[] { V(-118f, -8f), V(-102f, -8f), V(-94f, -3f), V(-84f, -3f), V(-78f, -10f), V(-68f, -10f), V(-63f, -5f), V(-63f, 0f), V(-60f, 1.5f), V(-60f, 6f) }, 1.6f),
            (new[] { V(-94f, -3f), V(-97f, 3f), V(-98f, 7.5f) }, 1.2f),       // north, to the crypt clearing
            (new[] { V(-60f, 1.5f), V(-47f, 1f), V(-41f, 4f) }, 1.2f),         // east, to the great crypt
            (new[] { V(-73f, -10f), V(-76f, -14.5f) }, 0.9f),                 // a worn track into the pines
            (new[] { V(-102f, -8f), V(-103f, -3f), V(-108f, 10.5f) }, 0.8f), // a goat track round the old plot
        };

        /// <summary>Fenced grave plots (x0, z0, width, depth); the painter sinks them to dark soil.</summary>
        private static readonly Rect[] GroundsPlots =
        {
            new Rect(-114f, 0f, 9f, 8f), new Rect(-88f, 2f, 14f, 7f), new Rect(-100f, -16f, 11f, 5.5f),
            new Rect(-60f, -9f, 12f, 6f), new Rect(-53f, 3.5f, 8f, 5f),
        };

        /// <summary>Treasure chests (built by PackSetup): each sits at the end of a side path, worth the detour.</summary>
        internal static readonly (Vector3 Position, float Yaw, int Xp)[] ChestPlacements =
        {
            (new Vector3(-100.5f, 0f, 9.6f), 15f, 60),  // crypt clearing, behind the watch
            (new Vector3(-77f, 0f, -15.8f), -10f, 40),  // in the pines, off the worn track
            (new Vector3(-38.8f, 0f, 4.4f), -40f, 80),  // beside the great crypt
            (new Vector3(-109.5f, 0f, 11.3f), 0f, 50),  // behind the old plot, by the wall
        };

        private static Material _graveyardMaterial;

        private static Vector2 V(float x, float z) => new Vector2(x, z);

        // ============ The Grounds ============

        private static void BuildGrounds(Ctx ctx)
        {
            // Ground and bounds: free movement inside the walls. The box is only the floor's collider; what shows is
            // the painted ground laid over it, wide enough to fill the frame past the walls.
            const float pad = 10f;
            GameObject ground = Box(ctx, "Grounds_Ground", GroundsWest - pad, GroundsEast + pad * 2f, -1f, 0f, GroundsFront - pad, GroundsBack + pad, "floor_dark", "block");
            ground.GetComponent<Renderer>().enabled = false;
            PaintGround(ctx, GroundsWest - pad, GroundsEast + pad * 2f, GroundsFront - pad, GroundsBack + pad, GroundsPaths, GroundsPlots);
            Blocker(ctx, "Grounds_Bound_W", GroundsWest - 1f, GroundsWest, 0f, 6f, GroundsFront, GroundsBack);
            Blocker(ctx, "Grounds_Bound_E", GroundsEast, GroundsEast + 1f, 0f, 6f, GroundsFront, GroundsBack);
            Blocker(ctx, "Grounds_Bound_Front", GroundsWest, GroundsEast, 0f, 6f, GroundsFront - 1f, GroundsFront);
            Blocker(ctx, "Grounds_Bound_Back", GroundsWest, GroundsEast, 0f, 12f, GroundsBack, GroundsBack + 1f);

            // The broad stair up to the terrace climbs away from the camera, the terrace runs along the front.
            const float stairWest = FacadeX - 5f, stairEast = FacadeX + 5f, stairFront = TerraceZ - 4.5f;
            const int steps = 9;
            for (int i = 0; i < steps; i++)
            {
                float z = stairFront + (TerraceZ - stairFront) * i / steps;
                Box(ctx, $"Grounds_Stair_{i}", stairWest, stairEast, 0f, TerraceY * (i + 1) / steps, z, TerraceZ, "floor", "block");
            }

            Box(ctx, "Grounds_Terrace", FacadeX - 11f, FacadeX + 11f, 0f, TerraceY, TerraceZ, FacadeZ + 0.5f, "floor", "block");
            Box(ctx, "Grounds_Terrace_Wall_W", FacadeX - 11f, stairWest, TerraceY, TerraceY + 0.7f, TerraceZ - 0.4f, TerraceZ, "floor", "brick");
            Box(ctx, "Grounds_Terrace_Wall_E", stairEast, FacadeX + 11f, TerraceY, TerraceY + 0.7f, TerraceZ - 0.4f, TerraceZ, "floor", "brick");

            // Gameplay: the walk in, a check, a fight at each turn of the path, chests down the side paths, the doors.
            Checkpoint(ctx, "CP_Grounds", GroundsStart.x, 0f, GroundsStart.z, 4f);
            Title(ctx, "Title_Grounds", GroundsStart.x + 2.5f, 0f, "THE CATHEDRAL GROUNDS", "Walk {Move} - the doors are at the top of the stair", z: GroundsStart.z, depth: 8f);
            Title(ctx, "Hint_Depth_Grounds", -103f, 0f, "Side paths hide treasure - strike a chest to open it", string.Empty, toast: true, z: -6f, depth: 8f);

            Fight(ctx, "Grounds_Risen", new Vector3(-90f, 1.5f, -4f), new Vector3(6f, 3f, 8f), new[]
            {
                new[] { ("Ghoul", -86f, 0f, 0f), ("Shade", -92f, 0f, -7f), ("Ghoul", -84f, 0f, -6.5f) },
            }, startToast: "The dead stir");

            Fight(ctx, "Grounds_CryptWatch", new Vector3(-97.5f, 1.5f, 5.5f), new Vector3(6f, 3f, 5f), new[]
            {
                new[] { ("Ghoul", -100f, 0f, 8f), ("Wizard", -95f, 0f, 10f), ("Shade", -101f, 0f, 4.5f) },
            }, startToast: "The crypt has keepers");

            Fight(ctx, "Grounds_Approach", new Vector3(-66f, 1.5f, -6f), new Vector3(6f, 3f, 9f), new[]
            {
                new[] { ("Ghoul", -58f, 0f, -11f), ("Shade", -74f, 0f, -6f), ("Ghoul", -68f, 0f, -2f) },
                new[] { ("Wizard", -56f, 0f, -1.5f), ("Ghoul", -67f, 0f, 3f), ("Shade", -72f, 0f, -13f) },
            }, startToast: "They rise to bar the stair");

            Fight(ctx, "Grounds_GreatCrypt", new Vector3(-45f, 1.5f, 2f), new Vector3(5f, 3f, 6f), new[]
            {
                new[] { ("Ghoul", -40f, 0f, 1.5f), ("Ghoul", -46f, 0f, -1.5f), ("Wizard", -41f, 0f, 7f) },
            }, startToast: "Something guards the great crypt");

            // In front of the portal on the terrace (the grounds' back bound stops the player short of the wall).
            var door = Volume<Doorway>(ctx, "Grounds_Doors", new Vector3(FacadeX, TerraceY + 1.5f, GroundsBack - 0.8f), new Vector3(3f, 3f, 1.2f));
            Transform arrival = Child(ctx.Volumes, "Nave_Arrival");
            arrival.position = Start;
            EnemySetup.Set(door, so =>
            {
                so.FindProperty("_destination").objectReferenceValue = arrival;
                so.FindProperty("_fade").objectReferenceValue = ctx.ScreenFade;
                so.FindProperty("_rig").objectReferenceValue = Object.FindAnyObjectByType<Brawler.Presentation.CameraRig>();
            });

            BuildFacade(ctx);
            DressGrounds(ctx);
        }

        // ============ The West Front ============

        /// <summary>
        /// The cathedral's west front facing the camera: a wall with a deep pointed portal, a rose window, twin towers
        /// with stepped spires, and buttresses. Seen whole from the far end of the grounds.
        /// </summary>
        private static void BuildFacade(Ctx ctx)
        {
            Transform root = Child(ctx.Decor, "Facade");
            if (CathedralKit.Available)
            {
                KitFacade(ctx, root);
                return;
            }

            const float half = 9f, back = FacadeZ + 2.5f, top = 17f;
            float x0 = FacadeX - half, x1 = FacadeX + half;

            // Wall, with the portal cut out of it.
            const float portalHalf = 1.8f, portalTop = TerraceY + 5f;
            Deco(ctx, "Facade_Wall_W", x0, FacadeX - portalHalf, 0f, top, FacadeZ, back, "floor", "brick", root);
            Deco(ctx, "Facade_Wall_E", FacadeX + portalHalf, x1, 0f, top, FacadeZ, back, "floor", "brick", root);
            Deco(ctx, "Facade_Wall_Above", FacadeX - portalHalf, FacadeX + portalHalf, portalTop, top, FacadeZ, back, "floor", "brick", root);
            Deco(ctx, "Facade_Doors", FacadeX - portalHalf, FacadeX + portalHalf, TerraceY, portalTop, FacadeZ + 1.2f, back, "floor_dark", "block", root);
            Blocker(ctx, "Facade_Solid_W", x0, FacadeX - portalHalf, 0f, top, FacadeZ, back);
            Blocker(ctx, "Facade_Solid_E", FacadeX + portalHalf, x1, 0f, top, FacadeZ, back);

            // Portal: receding orders of jambs and a stepped pointed arch.
            for (int order = 0; order < 3; order++)
            {
                float w = portalHalf + 0.5f + order * 0.5f;
                float z = FacadeZ - 0.5f + order * 0.5f;
                Deco(ctx, $"Facade_Jamb_W_{order}", FacadeX - w, FacadeX - w + 0.5f, TerraceY, portalTop, z, z + 0.5f, "floor", "block", root);
                Deco(ctx, $"Facade_Jamb_E_{order}", FacadeX + w - 0.5f, FacadeX + w, TerraceY, portalTop, z, z + 0.5f, "floor", "block", root);
                for (int k = 0; k < 4; k++)
                {
                    float y = portalTop + k * 0.55f;
                    float inset = w * (1f - Mathf.Cos(k / 4f * Mathf.PI * 0.5f)) + k * 0.12f;
                    Deco(ctx, $"Facade_Arch_{order}_{k}_W", FacadeX - w + inset, FacadeX - inset * 0.2f, y, y + 0.55f, z, z + 0.5f, "floor", "block", root);
                    Deco(ctx, $"Facade_Arch_{order}_{k}_E", FacadeX + inset * 0.2f, FacadeX + w - inset, y, y + 0.55f, z, z + 0.5f, "floor", "block", root);
                }
            }

            RoseWindow(ctx, "Facade_Rose", FacadeX, top - 6.5f, 2.6f, FacadeZ - 0.4f, top - 9.1f, top - 9.1f);
            Deco(ctx, "Facade_Gable", FacadeX - 6f, FacadeX + 6f, top, top + 3f, FacadeZ + 0.3f, back, "floor", "brick", root);
            DecoAt(ctx, "Facade_Gable_Peak", new Vector3(FacadeX, top + 3f, FacadeZ + 1.4f), new Vector3(6f, 6f, 2.2f), Quaternion.Euler(0f, 0f, 45f), "floor", "brick", root);

            // Twin towers with buttresses and stepped spires.
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = FacadeX + side * (half + 1.5f);
                string s = side < 0f ? "W" : "E";
                Deco(ctx, $"Facade_Tower_{s}", cx - 2.5f, cx + 2.5f, 0f, top + 6f, FacadeZ - 0.5f, back + 1f, "floor", "brick", root);
                Blocker(ctx, $"Facade_Tower_{s}_Solid", cx - 2.5f, cx + 2.5f, 0f, top, FacadeZ - 0.5f, back + 1f);
                Deco(ctx, $"Facade_Tower_{s}_Buttress_A", cx - 2.9f, cx - 2.1f, 0f, top - 2f, FacadeZ - 1.4f, FacadeZ - 0.5f, "floor", "block", root);
                Deco(ctx, $"Facade_Tower_{s}_Buttress_B", cx + 2.1f, cx + 2.9f, 0f, top - 2f, FacadeZ - 1.4f, FacadeZ - 0.5f, "floor", "block", root);
                PanelsAt(ctx, $"Facade_Tower_{s}_Window", cx - 1.2f, cx + 1.2f, top - 7f, FacadeZ - 0.52f, "window");
                for (int tier = 0; tier < 5; tier++)
                {
                    float h = 2.4f - tier * 0.45f;
                    DecoAt(ctx, $"Facade_Tower_{s}_Spire_{tier}", new Vector3(cx, top + 7.5f + tier * 2.2f, FacadeZ + 1.5f), new Vector3(h * 2f, 2.2f, h * 2f),
                        Quaternion.Euler(0f, tier % 2 == 0 ? 0f : 45f, 0f), "floor", "brick", root);
                }
            }

            PointLight(ctx, "Facade_Portal_Glow", new Vector3(FacadeX, TerraceY + 2.5f, FacadeZ - 1.2f), new Color(1f, 0.62f, 0.3f), 7f, 2.6f);
            PointLight(ctx, "Facade_Rose_Glow", new Vector3(FacadeX, top - 6.5f, FacadeZ - 1.5f), new Color(0.75f, 0.55f, 1f), 6f, 1.6f);
        }

        /// <summary>
        /// The west front from the Modular Cathedral Kit: stained-glass walls either side of the grand doorway (with
        /// its doors), a rose window above, and a bell tower with a spired cap on each side.
        /// </summary>
        private static void KitFacade(Ctx ctx, Transform root)
        {
            const float module = CathedralKit.Module;
            const float storey = 16f * CathedralKit.Scale;
            float left = FacadeX - module * 1.5f;
            string[] lower = { "wall_window_large_01", "doorway_01", "wall_window_large_01" };
            string[] upper = { "wall_01", "wall_rose_window_01", "wall_01" };
            for (int i = 0; i < 3; i++)
            {
                CathedralKit.PlaceAt(root, lower[i], new Vector3(left + module * i, TerraceY, FacadeZ));
                CathedralKit.PlaceAt(root, upper[i], new Vector3(left + module * i, TerraceY + storey, FacadeZ));
            }

            foreach (float side in new[] { -1f, 1f })
            {
                CathedralKit.PlaceCentered(root, "door_01", new Vector3(FacadeX + side * module * 0.25f, TerraceY, FacadeZ + 0.45f));
                float towerX = side < 0f ? left - 3f : left + module * 3f + 3f;
                GameObject tower = CathedralKit.PlaceCentered(root, "tower_base_01", new Vector3(towerX, 0f, FacadeZ + 2.5f));
                Bounds b = CathedralKit.BoundsOf(tower);
                CathedralKit.PlaceCentered(root, "tower_cap_01", new Vector3(b.center.x, b.max.y, b.center.z));
                Blocker(ctx, $"Facade_Tower_{side:0}_Solid", b.min.x, b.max.x, 0f, b.max.y, b.min.z, b.max.z);
            }

            float portal = module * 0.5f;
            Blocker(ctx, "Facade_Solid_W", left, FacadeX - portal, 0f, TerraceY + storey * 2f, FacadeZ, FacadeZ + 2f);
            Blocker(ctx, "Facade_Solid_E", FacadeX + portal, left + module * 3f, 0f, TerraceY + storey * 2f, FacadeZ, FacadeZ + 2f);
            ctx.Count += 12;

            PointLight(ctx, "Facade_Portal_Glow", new Vector3(FacadeX, TerraceY + 2.5f, FacadeZ - 1.2f), new Color(1f, 0.62f, 0.3f), 7f, 2.6f);
            PointLight(ctx, "Facade_Rose_Glow", new Vector3(FacadeX, TerraceY + storey * 1.5f, FacadeZ - 1.5f), new Color(0.75f, 0.55f, 1f), 7f, 1.8f);
        }

        // ============ Dressing ============

        private static void DressGrounds(Ctx ctx)
        {
            // The path itself is painted into the ground (PaintGround). Fenced plots of graves on both sides of it:
            // walk around them, behind them, between them.
            for (int i = 0; i < GroundsPlots.Length; i++)
            {
                Rect r = GroundsPlots[i];
                Plot(ctx, $"Plot_{i}", r.xMin, r.xMax, r.yMin, r.yMax, i + 1);
            }

            // Crypts anchor the side paths; candles, lanterns and urns mark where the chests are.
            Prop(ctx, "crypt-large", new Vector3(-42.5f, 0f, 9.8f), 180f, solid: true);
            Prop(ctx, "crypt-large-roof", new Vector3(-42.5f, 0f, 9.8f), 180f);
            Prop(ctx, "crypt-small", new Vector3(-97f, 0f, 11.2f), 180f, solid: true);
            Prop(ctx, "crypt-small-roof", new Vector3(-97f, 0f, 11.2f), 180f);
            Prop(ctx, "crypt", new Vector3(-82f, 0f, 11.2f), 180f, solid: true);
            Prop(ctx, "candle-multiple", new Vector3(-95.2f, 0f, 9.4f), 0f);
            Prop(ctx, "candle-multiple", new Vector3(-45.5f, 0f, 7.6f), 30f);
            Prop(ctx, "lantern-candle", new Vector3(-75.8f, 0f, -16.6f), 0f);
            Prop(ctx, "lantern-candle", new Vector3(-108.2f, 0f, 11.8f), 0f);
            Prop(ctx, "urn-round", new Vector3(-99f, 0f, 11.3f), 0f, solid: true);
            Prop(ctx, "urn-square", new Vector3(-45.5f, 0f, 11f), 0f, solid: true);
            PointLight(ctx, "Grounds_Crypt_Candles_W", new Vector3(-95.2f, 1f, 9.2f), new Color(1f, 0.6f, 0.3f), 4.5f, 1.8f);
            PointLight(ctx, "Grounds_Crypt_Candles_E", new Vector3(-45.5f, 1f, 7.4f), new Color(1f, 0.6f, 0.3f), 4.5f, 1.8f);
            PointLight(ctx, "Grounds_Pine_Lantern", new Vector3(-75.8f, 0.8f, -16.4f), new Color(1f, 0.62f, 0.3f), 3.5f, 1.4f);
            PointLight(ctx, "Grounds_Wall_Lantern", new Vector3(-108.2f, 0.8f, 11.6f), new Color(1f, 0.62f, 0.3f), 3.5f, 1.4f);

            // An open grave by the south-west plot: someone was digging.
            Prop(ctx, "grave", new Vector3(-86f, 0f, -13.5f), 0f, solid: true);
            Prop(ctx, "shovel-dirt", new Vector3(-84.4f, 0f, -12.6f), 30f);
            Prop(ctx, "coffin-old", new Vector3(-87.6f, 0f, -11.4f), 80f, solid: true);

            // Lamps along the path, warm against the night.
            foreach (Vector3 p in new[] { new Vector3(-108f, 0f, -10.2f), new Vector3(-90f, 0f, -0.8f), new Vector3(-69f, 0f, -12.4f),
                                         new Vector3(-65.4f, 0f, -2.5f), new Vector3(-52f, 0f, -1.3f) })
            {
                Prop(ctx, "lightpost-single", p, 0f, 1.3f, solid: true);
                PointLight(ctx, $"Grounds_Lamp_{p.x:0}", p + new Vector3(0f, 2.6f, 0f), new Color(1f, 0.66f, 0.35f), 6f, 2.2f);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                var basket = new Vector3(FacadeX + side * 6f, TerraceY, TerraceZ + 0.6f);
                Prop(ctx, "fire-basket", basket, 0f, solid: true);
                PointLight(ctx, $"Grounds_Brazier_{side:0}", basket + Vector3.up * 1.4f, new Color(1f, 0.55f, 0.25f), 7f, 2.6f);
            }

            // Pines: the frame of the diorama, plus clumps inside that the path bends around and hides behind.
            float[,] pines =
            {
                { -115f, 12f }, { -115.5f, 3f }, { -115f, -14.5f }, { -104f, 12.2f }, { -90f, 12f }, { -73f, 12f },
                { -38f, 12f }, { -37.5f, -2f }, { -38f, -14.5f },
                { -108f, -16f }, { -94f, -16.5f }, { -66f, -16.5f }, { -56f, -15.8f }, { -46f, -15.5f },
                { -80.5f, -14.2f }, { -72.5f, -15.2f }, { -102f, 1.5f }, { -70.5f, 4.5f }, { -54f, -12.8f }, { -88f, -7.5f },
            };
            for (int i = 0; i < pines.GetLength(0); i++)
            {
                string model = i % 3 == 0 ? "pine-crooked" : "pine";
                Prop(ctx, model, new Vector3(pines[i, 0], 0f, pines[i, 1]), i * 57f, 1.3f + (i % 4) * 0.15f);
                Blocker(ctx, $"Grounds_Pine_{i}_Trunk", pines[i, 0] - 0.5f, pines[i, 0] + 0.5f, 0f, 3f, pines[i, 1] - 0.5f, pines[i, 1] + 0.5f);
            }

            // A low wall along the front edge (between camera and play) and the stone wall behind.
            for (float x = GroundsWest; x < GroundsEast; x += KitScale)
            {
                Prop(ctx, "iron-fence", new Vector3(x + 1.1f, 0f, GroundsFront - 0.4f), 0f);
            }

            for (float x = GroundsWest; x < FacadeX - 13f; x += 2.6f)
            {
                Prop(ctx, (int)x % 3 == 0 ? "stone-wall-damaged" : "stone-wall", new Vector3(x + 1.3f, 0f, GroundsBack + 0.2f), 0f);
            }

            Prop(ctx, "iron-fence-border-gate", new Vector3(GroundsWest + 0.5f, 0f, GroundsStart.z), 90f);

            // A dark treeline past the walls fills the horizon, so the diorama never ends in void.
            var backdrop = new System.Random(11);
            for (float x = GroundsWest - 8f; x < GroundsEast + 18f; x += 3.2f)
            {
                if (x > FacadeX - 16f && x < FacadeX + 16f)
                {
                    continue; // the cathedral stands here
                }

                float z = GroundsBack + 3f + (float)backdrop.NextDouble() * 4f;
                Prop(ctx, backdrop.Next(3) == 0 ? "pine-crooked" : "pine", new Vector3(x, 0f, z), backdrop.Next(360), 1.6f + (float)backdrop.NextDouble() * 0.6f);
            }

            for (float z = GroundsFront - 2f; z < GroundsBack + 2f; z += 3.4f)
            {
                foreach (float x in new[] { GroundsWest - 4f, GroundsEast + 4f })
                {
                    Prop(ctx, backdrop.Next(3) == 0 ? "pine-crooked" : "pine", new Vector3(x + (float)backdrop.NextDouble() * 3f - 1.5f, 0f, z), backdrop.Next(360),
                         1.5f + (float)backdrop.NextDouble() * 0.6f);
                }
            }

            // Clutter at the path's edges, never on it.
            Prop(ctx, "debris", new Vector3(-58.5f, 0f, -1.6f), 20f);
            Prop(ctx, "rocks", new Vector3(-48f, 0f, -9.6f), 0f, solid: true);
            Prop(ctx, "rocks-tall", new Vector3(-111f, 0f, -14f), 40f, solid: true);
            Prop(ctx, "bench-damaged", new Vector3(-80f, 0f, -0.3f), 0f, solid: true);
            Prop(ctx, "bench", new Vector3(-104f, 0f, -11f), 0f, solid: true);
            Prop(ctx, "trunk-long", new Vector3(-62f, 0f, -14.5f), 70f, solid: true);
            Prop(ctx, "pine-fall", new Vector3(-103f, 0f, -16f), 100f);
            Prop(ctx, "gravestone-debris", new Vector3(-70f, 0f, -6.5f), 0f);
            PointLight(ctx, "Grounds_Moon", new Vector3(-76f, 18f, -6f), new Color(0.55f, 0.65f, 1f), 60f, 1.4f);
        }

        /// <summary>A fenced plot of headstones in rows, with a gap in the fence on the path side.</summary>
        private static void Plot(Ctx ctx, string name, float x0, float x1, float z0, float z1, int seed)
        {
            string[] stones = { "gravestone-cross", "gravestone-round", "gravestone-bevel", "gravestone-broken", "gravestone-decorative", "gravestone-wide", "cross-column" };
            var random = new System.Random(seed);
            for (float z = z0 + 1.2f; z < z1 - 0.6f; z += 2.2f)
            {
                for (float x = x0 + 1.2f; x < x1 - 0.6f; x += 2f)
                {
                    float jitter = (float)random.NextDouble() * 0.5f - 0.25f;
                    Prop(ctx, stones[random.Next(stones.Length)], new Vector3(x + jitter, 0f, z), (float)random.NextDouble() * 16f - 8f, solid: true);
                }
            }

            for (float x = x0; x < x1 - 0.5f; x += KitScale)
            {
                if (x > (x0 + x1) * 0.5f - 1.5f && x < (x0 + x1) * 0.5f + 0.5f)
                {
                    continue; // the gap in the front fence
                }

                Prop(ctx, random.Next(4) == 0 ? "iron-fence-damaged" : "iron-fence", new Vector3(x + 1.1f, 0f, z0), 0f, solid: true);
            }

            Prop(ctx, "iron-fence-border-column", new Vector3(x0, 0f, z0), 0f, solid: true);
            Prop(ctx, "iron-fence-border-column", new Vector3(x1, 0f, z0), 0f, solid: true);
        }

        /// <summary>
        /// Places a Graveyard Kit model under the shared tinted material. <paramref name="solid"/> gives it a box
        /// collider from its bounds, so the player and enemies walk around it.
        /// </summary>
        private static GameObject Prop(Ctx ctx, string model, Vector3 position, float yaw, float scale = 1f, bool solid = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{GraveyardKit}/{model}.fbx");
            if (asset == null)
            {
                return null;
            }

            GameObject go = Object.Instantiate(asset, ctx.Decor, false);
            go.name = $"Prop_{model}";
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * (KitScale * scale);
            Material material = GraveyardMaterial();
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                bounds = any ? Encapsulate(bounds, r.bounds) : r.bounds;
                any = true;
            }

            foreach (Transform t in go.GetComponentsInChildren<Transform>())
            {
                t.gameObject.isStatic = true;
            }

            if (solid && any)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                Vector3 size = go.transform.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }

            ctx.Count++;
            return go;
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        /// <summary>The kit's colour atlas, point-filtered and pulled toward cold, desaturated night tones.</summary>
        private static Material GraveyardMaterial()
        {
            if (_graveyardMaterial != null)
            {
                return _graveyardMaterial;
            }

            string texturePath = $"{GraveyardKit}/Textures/colormap.png";
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            _graveyardMaterial = AssetDatabase.LoadAssetAtPath<Material>(GraveyardMaterialPath);
            if (_graveyardMaterial == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GraveyardMaterialPath));
                _graveyardMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(_graveyardMaterial, GraveyardMaterialPath);
            }

            _graveyardMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            _graveyardMaterial.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.7f));
            // Matte: specular sheen from the moon behind the camera washed the small palette colours out to grey.
            _graveyardMaterial.SetFloat("_Smoothness", 0f);
            _graveyardMaterial.SetFloat("_SpecularHighlights", 0f);
            _graveyardMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            _graveyardMaterial.SetFloat("_EnvironmentReflections", 0f);
            _graveyardMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(_graveyardMaterial);
            return _graveyardMaterial;
        }
    }
}
