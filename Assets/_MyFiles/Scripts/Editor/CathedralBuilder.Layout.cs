using Brawler.Enemy;
using Brawler.Level;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// The Cathedral's layout: "The Ascent", a climb through the inside of the building instead of a walk along it.
    /// Ground-floor nave, a grand stair up to the triforium gallery, a switchback stair tower, the belfry, the choir loft
    /// and the sanctum at the top. Every room is enclosed (see <c>Interior</c>), and the route keeps going up.
    /// </summary>
    /// <remarks>
    /// Units: the hero is 1 tall, runs 6/s, jumps ~1.9 high and ~3 across, ~4.5 with the double jump, ~6 with jump + air
    /// dash. Stairs are visible steps over a smooth ramp collider. In the tower the flights alternate between the back
    /// and the front half of the shaft, like a real stairwell, so a flight passes over the one below without blocking it.
    /// </remarks>
    public static partial class CathedralBuilder
    {
        // ============ Lanes ============

        /// <summary>The playable depth band of a stretch (world Z).</summary>
        private readonly struct Lane
        {
            public readonly float Front;
            public readonly float Back;

            public Lane(float front, float back)
            {
                Front = front;
                Back = back;
            }

            public float Depth => Back - Front;
        }

        /// <summary>Halls: room to circle enemies, shallow enough to read from a low camera.</summary>
        private static readonly Lane Wide = new Lane(-2.2f, 2.2f);

        /// <summary>Corridors and the gallery.</summary>
        private static readonly Lane Narrow = new Lane(-1.4f, 1.4f);

        /// <summary>The stair tower: back flights use z 0.1..1.6, front flights -1.6..-0.1.</summary>
        private static readonly Lane Shaft = new Lane(-1.6f, 1.6f);

        private static readonly Lane AltarLane = new Lane(-2.8f, 2.8f);

        private static readonly Vector3 Start = new Vector3(2f, 0.05f, 0f);

        // Floor heights: the climb.
        private const float GalleryY = 4.5f;
        private const float TowerStep = 4f;
        private const float TopY = GalleryY + TowerStep * 5f; // 24.5: belfry, choir loft and sanctum

        /// <summary>Breakable pots along the level: (position, colour 0-3, shape 0 small / 1 jug / 2 urn).</summary>
        internal static readonly (Vector3 Position, int Colour, int Shape)[] PotPlacements =
        {
            (new Vector3(-96.4f, 0f, 10.2f), 0, 2), (new Vector3(-101.8f, 0f, 10.4f), 3, 0), (new Vector3(-78.2f, 0f, -16.2f), 1, 1),
            (new Vector3(-40f, 0f, 6.6f), 2, 2), (new Vector3(-37.8f, 0f, 3f), 0, 0), (new Vector3(-106f, 0f, -10.6f), 3, 1),
            (new Vector3(21.5f, 0f, 1.7f), 3, 1), (new Vector3(22.3f, 0f, 1.9f), 1, 2), (new Vector3(23.1f, 0f, 1.6f), 1, 0),
            (new Vector3(48.5f, GalleryY, 1.0f), 2, 2), (new Vector3(49.3f, GalleryY, 1.1f), 0, 1),
            (new Vector3(92.6f, GalleryY + TowerStep, 1.2f), 3, 0), (new Vector3(79.4f, GalleryY + TowerStep * 2f, 1.1f), 0, 1),
            (new Vector3(95.6f, TopY, 1.2f), 1, 2),
            (new Vector3(139.5f, TopY, 1.6f), 2, 1), (new Vector3(165f, TopY, -1.6f), 1, 0),
            (new Vector3(171f, TopY, 2.2f), 3, 2), (new Vector3(171.8f, TopY, 2.4f), 0, 0),
        };

        // ============ 1. The Nave (x 0..44, ground floor) ============

        /// <summary>
        /// Teach the basics with no pressure: a step and pit, an optional chain ring, a first fight. The grand stair at
        /// the far end is the first climb.
        /// </summary>
        private static void BuildNave(Ctx ctx)
        {
            LaneWalls(ctx, "Nave", 0f, 44f, -2f, 10f, Wide);
            Floor(ctx, "Nave_Floor_A", -1f, 14f, 0f, Wide);
            Box(ctx, "Nave_Step", 8f, 11f, 0f, 1f, Wide.Front, Wide.Back);
            KillPit(ctx, "Nave_Pit", 14f, 17f, -3f, Wide, 10);
            Floor(ctx, "Nave_Floor_B", 17f, 45f, 0f, Wide);
            Anchor(ctx, "Ring_Nave_Pit", 15.5f, 3.2f);

            // The grand stair: up the last third of the nave onto the gallery.
            Flight(ctx, "Nave_Grand_Stair", 33f, 43f, 0f, GalleryY, Wide.Front, Wide.Back, front: false);
            Box(ctx, "Nave_Stair_Mass", 33f, 43f, -1f, 0f, Wide.Front, Wide.Back);

            Checkpoint(ctx, "CP_0_Start", Start.x, 0f);
            Title(ctx, "Title_Nave", 3.5f, 0f, "THE NAVE", "Attack {Attack} - press again to chain strikes");
            Title(ctx, "Hint_Jump", 6.5f, 0f, "Jump {Jump} over the step and gap", string.Empty, toast: true);
            Title(ctx, "Hint_Grapple_Nave", 12.5f, 0f, "Optional: {Grapple} the glowing ring; hold {Move} to swing, {Jump} to release", string.Empty, toast: true);
            Title(ctx, "Hint_Attack", 18.5f, 0f, "Attack {Attack} three times to finish a combo", string.Empty, toast: true);
            Title(ctx, "Hint_Yank", 24f, 0f, "{Grapple} an enemy: pull light foes close, zip to heavy foes", string.Empty, toast: true);

            // Red seals behind the pit and before the grand stair: clear the room to move on.
            GameObject naveWest = Gate(ctx, "Nave_Seal_West", 19.4f, 0f, Wide);
            GameObject naveEast = Gate(ctx, "Nave_Seal_East", 32.4f, 0f, Wide);
            Fight(ctx, "Nave_Fight", new Vector3(21.5f, 2f, 0f), new Vector3(2f, 6f, 8f), new[]
            {
                new[] { ("Shade", 28f, 0f, 0.6f), ("Ghoul", 31f, 0f, -0.5f) },
            }, new[] { naveWest, naveEast }, "The way is sealed!", "The seal breaks.");

            DressNave(ctx);
        }

        // ============ 2. The Gallery (x 43..78, first floor) ============

        /// <summary>
        /// A low corridor over the nave aisle. The floor has fallen in halfway: too far to jump, so the air dash is
        /// taught over a drop back to the ground. A fire worm holds the far end (parry lesson).
        /// </summary>
        private static void BuildGallery(Ctx ctx)
        {
            LaneWalls(ctx, "Gallery", 43f, 78f, GalleryY - 8f, GalleryY + 6f, Narrow);
            Floor(ctx, "Gallery_Floor_A", 43f, 56f, GalleryY, Narrow, GalleryY - 1f);
            Floor(ctx, "Gallery_Floor_B", 61.5f, 79f, GalleryY, Narrow, GalleryY - 1f);
            KillPit(ctx, "Gallery_Collapse", 56f, 61.5f, GalleryY - 6f, Narrow, 10);

            Checkpoint(ctx, "CP_1_Gallery", 46f, GalleryY);
            Title(ctx, "Title_Gallery", 46.8f, GalleryY, "THE GALLERY", "Above the nave");
            Title(ctx, "Hint_Dash", 52.5f, GalleryY, "Too far to jump? Dash {Dash} in the air", string.Empty, toast: true);

            GameObject galleryWest = Gate(ctx, "Gallery_Seal_West", 62.2f, GalleryY, Narrow, 4.5f);
            GameObject galleryEast = Gate(ctx, "Gallery_Seal_East", 77.2f, GalleryY, Narrow, 4.5f);
            Fight(ctx, "Gallery_Worm", new Vector3(64f, GalleryY + 2f, 0f), new Vector3(2f, 4f, 6f), new[]
            {
                new[] { ("FireWorm", 74.5f, GalleryY, 0f) },
            }, new[] { galleryWest, galleryEast }, "Parry {Parry} a fireball to send it back", "The seal breaks.");

            DressGallery(ctx);
        }

        // ============ 3. The Stair Tower (x 78..94, five flights) ============

        /// <summary>
        /// Five flights switching back and forth up a square tower, back half then front half. The third flight has
        /// collapsed in the middle (jump it; a miss drops you a flight, not to your death). Angels dive at the climber.
        /// </summary>
        private static void BuildStairTower(Ctx ctx)
        {
            const float west = 78f, east = 94f, runWest = 81f, runEast = 91f;
            LaneWalls(ctx, "Tower", west, east, GalleryY - 1f, TopY + 8f, Shaft);
            Floor(ctx, "Tower_Floor", west, east, GalleryY, Shaft, GalleryY - 1f);

            for (int i = 0; i < 5; i++)
            {
                float from = GalleryY + TowerStep * i;
                float to = from + TowerStep;
                bool back = i % 2 == 0;
                bool eastward = back;
                float z0 = back ? 0.1f : Shaft.Front;
                float z1 = back ? Shaft.Back : -0.1f;
                string name = $"Tower_Flight_{i + 1}";

                if (i == 2)
                {
                    // The collapsed flight: steps gone between x 84.5 and 87.5.
                    float lerp(float x) => Mathf.Lerp(from, to, (x - runWest) / (runEast - runWest));
                    Flight(ctx, $"{name}_Lower", runWest, 84.5f, from, lerp(84.5f), z0, z1, front: !back);
                    Flight(ctx, $"{name}_Upper", 87.5f, runEast, lerp(87.5f), to, z0, z1, front: !back);
                    Rubble(ctx, $"{name}_Rubble", 85f, GalleryY, Shaft, 33);
                }
                else
                {
                    Flight(ctx, name, eastward ? runWest : runEast, eastward ? runEast : runWest, from, to, z0, z1, front: !back);
                }

                // Landing at the top of the flight, full depth so the next flight (other half) starts from it.
                float landing0 = eastward ? runEast : west;
                float landing1 = eastward ? east : runWest;
                Box(ctx, $"Tower_Landing_{i + 1}", landing0, landing1, to - 0.5f, to, Shaft.Front, Shaft.Back);
            }

            Checkpoint(ctx, "CP_2_Tower", 79.5f, GalleryY);
            Title(ctx, "Title_Tower", 80.2f, GalleryY, "THE STAIR TOWER", "Up, and up");
            Title(ctx, "Hint_Depth", 91.6f, GalleryY + TowerStep, "The stair turns: step toward the camera with {Move}", string.Empty, toast: true);

            Fight(ctx, "Tower_Angels", new Vector3(80f, GalleryY + TowerStep * 2f + 1.5f, 0f), new Vector3(3f, 3f, 4f), new[]
            {
                new[] { ("Angel", 88f, GalleryY + TowerStep * 2f + 3f, 0f), ("Angel", 84f, GalleryY + TowerStep * 3f + 3f, 0f) },
            });

            DressStairTower(ctx, west, east);
        }

        // ============ 4. The Belfry (x 94..136, top floor) ============

        /// <summary>
        /// The bells fell through the floor: the grapple, kishotenketsu style. One ring over a hole (teach), two rings
        /// in a row (develop), then a long swing to the far side while the room keeps you guessing (twist).
        /// </summary>
        private static void BuildBelfry(Ctx ctx)
        {
            LaneWalls(ctx, "Belfry", 94f, 136f, TopY - 10f, TopY + 9f, Narrow);
            Floor(ctx, "Belfry_Floor_A", 93f, 99f, TopY, Narrow, TopY - 1f);
            Anchor(ctx, "Ring_1", 102.5f, TopY + 3.6f);
            Floor(ctx, "Belfry_Beam_1", 106f, 110f, TopY, Narrow, TopY - 1f);
            Anchor(ctx, "Ring_2", 114f, TopY + 3.6f);
            Anchor(ctx, "Ring_3", 120.5f, TopY + 3.6f);
            Floor(ctx, "Belfry_Beam_2", 124f, 127f, TopY, Narrow, TopY - 1f);
            Anchor(ctx, "Ring_4", 130f, TopY + 4.2f);
            Floor(ctx, "Belfry_Floor_B", 133f, 137f, TopY, Narrow, TopY - 1f);
            KillPit(ctx, "Belfry_Fall", 99f, 133f, TopY - 8f, Narrow);

            Checkpoint(ctx, "CP_3_Belfry", 96f, TopY);
            Title(ctx, "Title_Belfry", 96.8f, TopY, "THE BELFRY", "Swing, or fall");
            Title(ctx, "Hint_Grapple", 97.6f, TopY, "{Grapple} the glowing ring - hold {Move} to swing, {Jump} to let go", string.Empty, toast: true);

            Fight(ctx, "Belfry_Angels", new Vector3(125.5f, TopY + 2f, 0f), new Vector3(2f, 4f, 4f), new[]
            {
                new[] { ("Angel", 131f, TopY + 3.5f, 0f) },
            });

            DressBelfry(ctx);
        }

        // ============ 5. The Choir Loft (x 136..168) ============

        /// <summary>The gates slam shut: three waves mixing everything met so far, ending with the elite.</summary>
        private static void BuildChoir(Ctx ctx)
        {
            LaneWalls(ctx, "Choir", 136f, 168f, TopY - 2f, TopY + 9f, Wide);
            Floor(ctx, "Choir_Floor", 136f, 168f, TopY, Wide, TopY - 1f);
            Box(ctx, "Choir_Dais", 150f, 154f, TopY, TopY + 0.9f, -1f, 1f);

            GameObject west = Gate(ctx, "Choir_Gate_West", 137f, TopY, Wide);
            GameObject east = Gate(ctx, "Choir_Gate_East", 167f, TopY, Wide);
            Title(ctx, "Title_Choir", 138.5f, TopY, "THE CHOIR LOFT", "None leave until the hymn ends");

            // Balconies along the back wall with stairs down at their inner ends: the casters shoot from up there.
            Box(ctx, "Choir_Balcony_West", 140.5f, 147f, TopY, TopY + 2.4f, 1f, Wide.Back);
            Flight(ctx, "Choir_Stairs_West", 147f, 149.6f, TopY + 2.4f, TopY, 1f, Wide.Back, front: false);
            Box(ctx, "Choir_Balcony_East", 157f, 163.5f, TopY, TopY + 2.4f, 1f, Wide.Back);
            Flight(ctx, "Choir_Stairs_East", 154.4f, 157f, TopY, TopY + 2.4f, 1f, Wide.Back, front: false);

            Fight(ctx, "Choir_Gauntlet", new Vector3(141f, TopY + 2f, 0f), new Vector3(2f, 4f, 5f), new[]
            {
                new[] { ("Shade", 149f, TopY, 0.5f), ("Shade", 155f, TopY, -1.2f), ("Ghoul", 161f, TopY, 0f) },
                new[] { ("FireWorm", 160.5f, TopY + 2.4f, 1.6f), ("Wizard", 143.5f, TopY + 2.4f, 1.6f), ("Ghoul", 153f, TopY, -1f) },
                new[] { ("EvilWizard", 152f, TopY, -0.5f), ("Angel", 147f, TopY + 3f, -1f) },
            }, new[] { west, east }, "The doors are sealed!", "The hymn ends.");

            if (ctx.MothsClip != null)
            {
                foreach (Vector3 p in new[] { new Vector3(143f, TopY + 3.5f, 1.8f), new Vector3(159f, TopY + 4f, 1.9f) })
                {
                    var moths = new GameObject("Choir_Moths");
                    moths.transform.SetParent(ctx.Decor, false);
                    moths.transform.position = p;
                    AnimatedSprite(moths, ctx.MothsClip, ctx.GlowMaterial);
                }
            }

            DressChoir(ctx);
        }

        // ============ 6. The Sanctum (x 168..206) ============

        /// <summary>The boss: the tallest room, rings overhead to swing clear of the shadow dash, the exit behind it.</summary>
        private static void BuildSanctum(Ctx ctx)
        {
            LaneWalls(ctx, "Sanctum", 168f, 206f, TopY - 2f, TopY + 14f, AltarLane);
            Floor(ctx, "Sanctum_Floor", 168f, 206f, TopY, AltarLane, TopY - 1f);
            Box(ctx, "Sanctum_Wall_End", 205f, 207f, TopY, TopY + 14f, AltarLane.Front - 1f, AltarLane.Back, "brick", "brick");

            Checkpoint(ctx, "CP_4_Sanctum", 170f, TopY);
            GameObject entrance = Gate(ctx, "Sanctum_Gate_West", 173f, TopY, AltarLane, 6f);
            GameObject exit = Gate(ctx, "Sanctum_Gate_East", 199f, TopY, AltarLane, 6f);

            // High: the bottom of the swing stays above the shadow dash and off the floor.
            Anchor(ctx, "Ring_Sanctum_1", 179f, TopY + 6.4f);
            Anchor(ctx, "Ring_Sanctum_2", 186f, TopY + 6.8f);
            Anchor(ctx, "Ring_Sanctum_3", 193f, TopY + 6.4f);

            BossController boss = PlaceBoss(ctx, new Vector3(193f, TopY + 0.15f, 0f));
            var arena = Volume<BossArena>(ctx, "Sanctum_BossArena", new Vector3(176f, TopY + 2f, 0f), new Vector3(2f, 4f, AltarLane.Depth + 0.5f));
            EnemySetup.Set(arena, so =>
            {
                so.FindProperty("_boss").objectReferenceValue = boss;
                so.FindProperty("_bar").objectReferenceValue = Object.FindAnyObjectByType<BossHealthBar>(FindObjectsInactive.Include);
                so.FindProperty("_minX").floatValue = 174.5f;
                so.FindProperty("_maxX").floatValue = 197.5f;
                HeroSetup.SetArray(so.FindProperty("_gates"), entrance);
                HeroSetup.SetArray(so.FindProperty("_exitBlockers"), exit);
            });

            Title(ctx, "Title_Sanctum", 170.8f, TopY, "THE SANCTUM", "It has waited for you");
            Title(ctx, "Title_End", 202f, TopY, "THE END", "Thanks for playing Feared");
            DressSanctum(ctx);
        }

        private static BossController PlaceBoss(Ctx ctx, Vector3 position)
        {
            BossController prefab = RosterSetup.LoadBoss();
            if (prefab == null)
            {
                return null;
            }

            var boss = (BossController)PrefabUtility.InstantiatePrefab(prefab, ctx.Root);
            boss.transform.position = position;
            EnemySetup.Set(boss, so => so.FindProperty("_coordinator").objectReferenceValue = ctx.Coordinator);
            foreach (BillboardSprite billboard in boss.GetComponentsInChildren<BillboardSprite>(true))
            {
                EnemySetup.Set(billboard, so => so.FindProperty("_cameraTransform").objectReferenceValue = ctx.CameraTransform);
            }

            return boss;
        }

        // ============ Stairs ============

        /// <summary>
        /// A flight of stairs from (x0, yFrom) to (x1, yTo) across z0..z1: visible stone steps on a stringer, and a smooth
        /// ramp collider under them so walking up and down never stutters. A front flight (between the camera and a back
        /// flight) dithers out when it covers the hero.
        /// </summary>
        private static void Flight(Ctx ctx, string name, float x0, float x1, float yFrom, float yTo, float z0, float z1, bool front)
        {
            const float run = 0.55f;
            if (yTo < yFrom)
            {
                (x0, x1, yFrom, yTo) = (x1, x0, yTo, yFrom);
            }

            Transform visual = WallStair(ctx, name, x0, x1, z0, z1, yFrom, yTo, run);
            if (front)
            {
                OccluderTools.MakeOccluder(visual.gameObject);
            }

            // The ramp rides half a step above the step corners, so feet land on the treads rather than in them.
            int steps = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(x1 - x0) / run));
            float half = (yTo - yFrom) / steps * 0.5f;
            var from = new Vector3(x0, yFrom + half, (z0 + z1) * 0.5f);
            var to = new Vector3(x1, yTo + half, (z0 + z1) * 0.5f);
            if (to.x < from.x)
            {
                (from, to) = (to, from);
            }

            Vector3 along = to - from;
            const float thickness = 0.4f;
            Quaternion tilt = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);

            var ramp = new GameObject($"{name}_Ramp");
            ramp.transform.SetParent(ctx.Geometry, false);
            ramp.transform.SetPositionAndRotation((from + to) * 0.5f + tilt * Vector3.down * (thickness * 0.5f), tilt);
            ramp.AddComponent<BoxCollider>().size = new Vector3(along.magnitude, thickness, z1 - z0);
            ramp.isStatic = true;
        }
    }
}
