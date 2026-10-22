using UnityEngine;

namespace Brawler.Editor
{
    /// <summary>
    /// The Cathedral's set dressing. Every room is enclosed by <see cref="Interior"/>: from the low camera the back wall,
    /// pilasters and vault fill the frame, so each room reads as the inside of a building. Pure decoration: nothing here
    /// touches the lane's collision except a solid end wall.
    /// </summary>
    public static partial class CathedralBuilder
    {
        private static readonly string[] HallPanels = { "window", "pillar", "torch", "window", "gargoyle", "pillar" };
        private static readonly string[] CorridorPanels = { "torch", "window", "pillar", "window" };

        // ============ Rooms ============

        private static void DressNave(Ctx ctx)
        {
            Interior(ctx, "Nave_Room", -0.5f, 44f, 0f, 9.5f, Wide, HallPanels, bay: 7f, gaps: new[] { (14f, 17f) });
            EndWall(ctx, "Nave_Wall_West", -0.5f, -1f, 10.5f, float.NaN, Wide);
            Pews(ctx, "Nave_Pews_A", 1f, 13f, Wide);
            Pews(ctx, "Nave_Pews_B", 18f, 32f, Wide);
            Rubble(ctx, "Nave_Rubble_Pit", 13.6f, 0f, Wide, 11);
            FgChain(ctx, "Nave_FG_Chain", 24.5f, Wide.Front - 2.6f, 10f, 4f, 1);
            FgPillar(ctx, "Nave_FG_Pillar_B", 31.5f, Wide.Front - 3.2f, 0f, 10.5f);
            FgStatue(ctx, "Nave_FG_Saint", 39f, Wide.Front - 3f, 0f, 2.8f);
            PointLight(ctx, "Nave_Fill", new Vector3(22f, 6f, 0f), new Color(0.55f, 0.62f, 0.9f), 26f, 1.2f);
        }

        private static void DressGallery(Ctx ctx)
        {
            Interior(ctx, "Gallery_Room", 43.5f, 78f, GalleryY, GalleryY + 6.5f, Narrow, CorridorPanels, bay: 5f,
                gaps: new[] { (56f, 61.5f) }, wallBottom: GalleryY - 9f);
            EndWall(ctx, "Gallery_Wall_West", 43.5f, -1f, GalleryY + 6.8f, GalleryY, Narrow);

            // Through the collapse: the nave aisle far below.
            Deco(ctx, "Gallery_Below", 55f, 62.5f, GalleryY - 10f, GalleryY - 9f, Narrow.Front - 3f, Narrow.Back + 0.4f, "floor_dark", "block");
            Rubble(ctx, "Gallery_Rubble", 57.5f, GalleryY - 9f, Narrow, 21);
            FgPillar(ctx, "Gallery_FG_Stump_A", 55.4f, Narrow.Front - 2.4f, GalleryY - 9f, GalleryY - 0.4f, broken: true);
            FgPillar(ctx, "Gallery_FG_Stump_B", 62.1f, Narrow.Front - 2.4f, GalleryY - 9f, GalleryY - 1.2f, broken: true);
            FgRail(ctx, "Gallery_FG_Rail", 45f, 54.5f, Narrow.Front - 1.6f, GalleryY, 52);
            FgRail(ctx, "Gallery_FG_Rail_B", 63f, 76f, Narrow.Front - 1.6f, GalleryY, 53);
        }

        private static void DressStairTower(Ctx ctx, float west, float east)
        {
            const float ceiling = TopY + 6.5f;
            Interior(ctx, "Tower_Room", west, east, GalleryY, ceiling, Shaft, CorridorPanels, bay: 8f);
            EndWall(ctx, "Tower_Wall_West", west, GalleryY - 1f, ceiling + 1f, GalleryY, Shaft);

            // Lancets and torches up the shaft wall, one row per storey.
            for (int storey = 1; storey <= 5; storey++)
            {
                float y = GalleryY + TowerStep * storey;
                PanelsAt(ctx, $"Tower_Panels_{storey}", west + 2.5f, east - 2.5f, y - 0.5f, Shaft.Back + 0.38f, storey % 2 == 0 ? CorridorPanels : HallPanels);
            }

            Bell(ctx, "Tower_Bell", new Vector3((west + east) * 0.5f, ceiling - 2f, 0.6f), 1.4f);
            FgPillar(ctx, "Tower_FG_Corner_W", west + 0.6f, Shaft.Front - 2.6f, GalleryY, ceiling);
            FgPillar(ctx, "Tower_FG_Corner_E", east - 0.6f, Shaft.Front - 2.6f, GalleryY, ceiling);
            PointLight(ctx, "Tower_Moon", new Vector3((west + east) * 0.5f, TopY - 2f, Shaft.Back), new Color(0.6f, 0.7f, 1f), 18f, 1.8f);
        }

        private static void DressBelfry(Ctx ctx)
        {
            Interior(ctx, "Belfry_Room", 94f, 136f, TopY, TopY + 8f, Narrow, HallPanels, bay: 7f,
                gaps: new[] { (99f, 106f), (110f, 124f), (127f, 133f) }, wallBottom: TopY - 10f);
            EndWall(ctx, "Belfry_Wall_West", 94f, GalleryY, TopY + 9f, TopY, Narrow);

            Bell(ctx, "Belfry_Bell_Fallen", new Vector3(116f, TopY - 7.5f, 0.8f), 2.2f);
            Bell(ctx, "Belfry_Bell_Hung", new Vector3(128.5f, TopY + 6f, 1f), 1.2f);
            Deco(ctx, "Belfry_Pit_Floor", 98f, 134f, TopY - 10f, TopY - 9f, Narrow.Front - 5f, Narrow.Back + 0.4f, "floor_dark", "block");
            Rubble(ctx, "Belfry_Rubble", 103f, TopY - 9f, Narrow, 31);
            FgChain(ctx, "Belfry_FG_Rope_A", 112f, Narrow.Front - 2.2f, TopY + 8f, TopY - 3f, 0);
            FgPillar(ctx, "Belfry_FG_Stump", 125.5f, Narrow.Front - 2.4f, TopY - 9f, TopY - 1f, broken: true);
            FgChain(ctx, "Belfry_FG_Rope_B", 133.5f, Narrow.Front - 2.2f, TopY + 8f, TopY + 2f, 1);
        }

        private static void DressChoir(Ctx ctx)
        {
            Interior(ctx, "Choir_Room", 136f, 168f, TopY, TopY + 8.5f, Wide, HallPanels, bay: 6.4f);
            EndWall(ctx, "Choir_Wall_West", 136f, TopY - 1f, TopY + 9.5f, TopY, Wide);
            RoseWindow(ctx, "Choir_Rose", 152f, TopY + 6f, 1.8f, Wide.Back - 0.3f, TopY + 4.2f, TopY + 4.2f);
            Pews(ctx, "Choir_Pews", 139f, 166f, Wide, TopY);
            FgRail(ctx, "Choir_FG_Screen_W", 140f, 146.5f, Wide.Front - 2.4f, TopY, 55);
            FgChain(ctx, "Choir_FG_Censer", 152f, Wide.Front - 2.8f, TopY + 8.5f, TopY + 4.5f, 1);
            FgRail(ctx, "Choir_FG_Screen_E", 157.5f, 164f, Wide.Front - 2.4f, TopY, 56);
            PointLight(ctx, "Choir_Rose_Light", new Vector3(152f, TopY + 5f, 0f), new Color(1f, 0.75f, 0.45f), 12f, 1.8f);
        }

        private static void DressSanctum(Ctx ctx)
        {
            const float ceiling = TopY + 13f;
            Interior(ctx, "Sanctum_Room", 168f, 206f, TopY, ceiling, AltarLane, HallPanels, bay: 6f);
            EndWall(ctx, "Sanctum_Wall_West", 168f, TopY - 1f, ceiling + 1f, TopY, AltarLane);
            RoseWindow(ctx, "Sanctum_Rose", 186f, TopY + 8.5f, 2.6f, AltarLane.Back - 0.3f, TopY + 5.9f, TopY + 5.9f);
            Deco(ctx, "Sanctum_Dais_Step", 181f, 191f, TopY, TopY + 0.22f, AltarLane.Back - 0.8f, AltarLane.Back + 0.4f, "floor", "block");
            Deco(ctx, "Sanctum_Altar", 184.5f, 187.5f, TopY + 0.22f, TopY + 1.4f, AltarLane.Back - 0.5f, AltarLane.Back + 0.3f, "floor", "brick");
            FgPillar(ctx, "Sanctum_FG_Pillar_W", 171.5f, AltarLane.Front - 3.4f, TopY, ceiling);
            FgChain(ctx, "Sanctum_FG_Chain_A", 181f, AltarLane.Front - 3f, ceiling, TopY + 8.5f, 2);
            FgBoulder(ctx, "Sanctum_FG_Fallen", 188f, AltarLane.Front - 3f, TopY, 57);
            FgChain(ctx, "Sanctum_FG_Chain_B", 191.5f, AltarLane.Front - 2.6f, ceiling, TopY + 9.5f, 1);
            FgPillar(ctx, "Sanctum_FG_Pillar_E", 202.5f, AltarLane.Front - 3.4f, TopY, ceiling);
            PointLight(ctx, "Sanctum_Altar_Light", new Vector3(186f, TopY + 4f, AltarLane.Back - 1f), new Color(1f, 0.25f, 0.2f), 16f, 3f);
        }

        // ============ Kit ============

        /// <summary>
        /// Encloses a room: a back wall carrying the church panels, pilasters with transverse ribs under a vault, and dark
        /// paving running toward the camera (broken where the floor has fallen in, see <paramref name="gaps"/>).
        /// End walls are separate (<see cref="EndWall"/>) because neighbouring rooms share them.
        /// </summary>
        private static void Interior(Ctx ctx, string name, float x0, float x1, float floorY, float ceilingY, Lane lane, string[] panels,
                                     float bay = 6f, (float X0, float X1)[] gaps = null, float wallBottom = float.NaN)
        {
            Transform root = Child(ctx.Decor, name);
            float wallZ = lane.Back + 0.4f;
            float bottom = float.IsNaN(wallBottom) ? floorY - 1f : wallBottom;
            if (CathedralKit.Available)
            {
                KitInterior(ctx, root, x0, x1, floorY, ceilingY, lane, wallZ, bottom, bay, gaps);
                return;
            }

            Deco(ctx, $"{name}_BackWall", x0, x1, bottom, ceilingY + 1f, wallZ, wallZ + 1f, "floor", "brick", root);
            PanelsAt(ctx, $"{name}_Panel", x0 + 0.6f, x1 - 0.6f, floorY, wallZ - 0.02f, panels);
            // The vault stops short of the camera, so from below it reads as a ceiling, not a slab across the shot.
            float vaultFront = lane.Front - 1f;
            Deco(ctx, $"{name}_Vault", x0, x1, ceilingY, ceilingY + 1f, vaultFront, wallZ + 1f, "brick", "block", root);

            int i = 0;
            for (float x = x0 + bay * 0.5f; x < x1; x += bay, i++)
            {
                Deco(ctx, $"{name}_Pier_{i:00}", x - 0.45f, x + 0.45f, floorY, ceilingY, wallZ - 0.5f, wallZ, "floor", "block", root);
                Deco(ctx, $"{name}_Pier_{i:00}_Base", x - 0.6f, x + 0.6f, floorY, floorY + 0.5f, wallZ - 0.7f, wallZ, "floor", "block", root);
                Deco(ctx, $"{name}_Rib_{i:00}", x - 0.2f, x + 0.2f, ceilingY - 0.35f, ceilingY, vaultFront, wallZ, "floor", "block", root);
            }

            float cursor = x0;
            int segment = 0;
            foreach (var gap in gaps ?? System.Array.Empty<(float X0, float X1)>())
            {
                Apron(ctx, $"{name}_Apron_{segment++}", cursor, gap.X0, floorY, lane);
                cursor = gap.X1;
            }

            Apron(ctx, $"{name}_Apron_{segment}", cursor, x1, floorY, lane);
        }

        /// <summary>
        /// <see cref="Interior"/> from the Modular Cathedral Kit: wall modules (stained-glass bays between plain ones)
        /// stacked from the lowest visible point to the vault, gothic columns at each bay, ribbed vaults over the lane,
        /// and stone floor tiles from the back wall to the camera, broken over <paramref name="gaps"/>.
        /// </summary>
        private static void KitInterior(Ctx ctx, Transform root, float x0, float x1, float floorY, float ceilingY, Lane lane,
                                        float wallZ, float bottom, float bay, (float X0, float X1)[] gaps)
        {
            const float module = CathedralKit.Module;
            const float wallHeight = 16f * CathedralKit.Scale;
            string[] bays = { "wall_window_01", "wall_01", "wall_window_large_01", "wall_01" };
            int column = 0;
            for (float x = x0; x < x1 - 0.5f; x += module, column++)
            {
                for (float y = bottom; y < ceilingY + 1f; y += wallHeight)
                {
                    bool windowRow = y <= floorY + 0.5f && y + wallHeight > floorY + 2f;
                    string piece = windowRow ? bays[column % bays.Length] : "wall_01";
                    CathedralKit.PlaceAt(root, piece, new Vector3(x, y, wallZ));
                    ctx.Count++;
                }
            }

            float columnScale = (ceilingY - floorY) / (23f * CathedralKit.Scale);
            int i = 0;
            for (float x = x0 + bay * 0.5f; x < x1; x += bay, i++)
            {
                CathedralKit.PlaceCentered(root, "column_01", new Vector3(x, floorY, wallZ - 0.5f), 0f, columnScale);
                ctx.Count++;
            }

            for (float x = x0; x < x1 - 0.5f; x += 24f * CathedralKit.Scale)
            {
                CathedralKit.PlaceAt(root, "vault_large_01", new Vector3(x, ceilingY - 1.6f, lane.Front - 0.2f));
                ctx.Count++;
            }

            string[] tiles = { "floor_01", "floor_02", "floor_03" };
            int t = 0;
            for (float x = x0; x < x1 - 0.1f; x += module)
            {
                for (float z = wallZ - module; z > lane.Front - 6f; z -= module, t++)
                {
                    float tileEnd = Mathf.Min(x + module, x1);
                    bool overGap = false;
                    foreach (var gap in gaps ?? System.Array.Empty<(float X0, float X1)>())
                    {
                        overGap |= x < gap.X1 && tileEnd > gap.X0;
                    }

                    if (!overGap)
                    {
                        CathedralKit.PlaceAt(root, tiles[(t * 7 + 3) % tiles.Length], new Vector3(x, floorY - module * 0.125f + 0.02f, z));
                        ctx.Count++;
                    }
                }
            }
        }

        /// <summary>
        /// A wall across the room at <paramref name="x"/>, from <paramref name="y0"/> to <paramref name="y1"/>. With a
        /// <paramref name="doorY"/> it has a doorway 3.6 tall standing on that height; the jamb in front of the lane
        /// dithers out over the hero. Without one it is solid, and blocks the lane.
        /// </summary>
        private static void EndWall(Ctx ctx, string name, float x, float y0, float y1, float doorY, Lane lane)
        {
            Transform root = Child(ctx.Decor, name);
            float front = lane.Front - 5f;
            float back = lane.Back + 1.4f;
            const float half = 0.5f;
            if (float.IsNaN(doorY))
            {
                Deco(ctx, name, x - half, x + half, y0, y1, front, back, "floor", "brick", root);
                Blocker(ctx, $"{name}_Solid", x - half, x + half, y0, y1, lane.Front, lane.Back);
                return;
            }

            const float doorHeight = 3.6f;
            if (doorY > y0 + 0.01f)
            {
                Deco(ctx, $"{name}_Below", x - half, x + half, y0, doorY, front, back, "floor", "brick", root);
            }

            Deco(ctx, $"{name}_Lintel", x - half, x + half, doorY + doorHeight, y1, front, back, "floor", "brick", root);
            Deco(ctx, $"{name}_Jamb_Back", x - half, x + half, doorY, doorY + doorHeight, lane.Back, back, "floor", "brick", root);
            Transform jamb = Child(root, $"{name}_Jamb_Front");
            Deco(ctx, $"{name}_Jamb_Front", x - half, x + half, doorY, doorY + doorHeight, front, lane.Front, "floor", "brick", jamb);
            OccluderTools.MakeOccluder(jamb.gameObject);
        }

        /// <summary>
        /// Dark paving in front of the lane, so the floor runs on toward the camera instead of ending at the lane's
        /// edge. No collider: the lane's invisible front wall still bounds play.
        /// </summary>
        private static void Apron(Ctx ctx, string name, float x0, float x1, float y, Lane lane)
        {
            if (x1 - x0 > 0.05f)
            {
                Deco(ctx, name, x0, x1, y - 4f, y, lane.Front - 5f, lane.Front, "floor_dark", "block");
            }
        }
    }
}
