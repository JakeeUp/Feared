using Brawler.Enemy;
using Brawler.Level;
using Brawler.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// The Cathedral's layout. Units: the hero is 1 tall, runs 6/s, jumps ~1.9 high and ~3 across, ~4.5 with the double
    /// jump, ~6 with jump + air dash. Gaps and heights below are sized against those numbers: a gap that needs a new
    /// skill is always first met where falling in only costs a little health and the checkpoint is a few steps back.
    /// </summary>
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

        /// <summary>Platforming: shallow, so depth never decides whether a jump lands.</summary>
        private static readonly Lane Narrow = new Lane(-1.5f, 1.5f);

        /// <summary>Brawling: room to circle enemies.</summary>
        private static readonly Lane Wide = new Lane(-3f, 3f);

        private static readonly Lane AltarLane = new Lane(-3.5f, 3.5f);

        private static readonly Vector3 Start = new Vector3(1f, 0.05f, 0f);

        /// <summary>Breakable pots along the level: (position, colour 0-3, shape 0 small / 1 jug / 2 urn).</summary>
        internal static readonly (Vector3 Position, int Colour, int Shape)[] PotPlacements =
        {
            (new Vector3(23.2f, 1.2f, 2.4f), 3, 1), (new Vector3(24f, 1.2f, 2.6f), 1, 2), (new Vector3(24.8f, 1.2f, 2.3f), 1, 0),
            (new Vector3(52f, 5f, 2.2f), 2, 2), (new Vector3(52.8f, 5f, 2.5f), 0, 1),
            (new Vector3(100.5f, -7f, 1f), 3, 2), (new Vector3(101.3f, -7f, 1.1f), 0, 0),
            (new Vector3(125.5f, -7f, 1f), 1, 1),
            (new Vector3(182.5f, 0f, 2.5f), 1, 1), (new Vector3(207.5f, 0f, -2.4f), 2, 2), (new Vector3(208.2f, 0f, 2.5f), 0, 0),
            (new Vector3(213.5f, 0f, 2.8f), 3, 1),
        };

        // ============ 1. The Nave (x -6..58) ============

        /// <summary>
        /// Teach the basics with no pressure: a step, a 3-wide pit (just a jump), a first fight on open ground, then a
        /// wall-jump chimney up to a ledge held by a fire worm, where the player learns to parry fireballs.
        /// </summary>
        private static void BuildNave(Ctx ctx)
        {
            LaneWalls(ctx, "Nave", -6f, 58f, -4f, 14f, Wide);
            Box(ctx, "Nave_Wall_Start", -6f, -4f, -4f, 14f, Wide.Front - 1f, Wide.Back, "brick", "brick");

            Floor(ctx, "Nave_Floor_A", -4f, 14f, 0f, Wide);
            Box(ctx, "Nave_Step", 8f, 11f, 0f, 1f, Wide.Front, Wide.Back);
            KillPit(ctx, "Nave_Pit", 14f, 17f, -4f, Wide, 10);
            Floor(ctx, "Nave_Floor_B", 17f, 41f, 0f, Wide);

            // Chimney: a hanging pillar (walk under it) facing the ledge wall 2.5 away. Wall jump between them.
            Box(ctx, "Nave_Chimney", 37.5f, 38.5f, 1.4f, 7.5f, Wide.Front, Wide.Back, "floor", "brick");
            Floor(ctx, "Nave_Ledge", 41f, 58f, 5f, Wide, -4f);

            Checkpoint(ctx, "CP_0_Start", Start.x, 0f);
            Title(ctx, "Title_Nave", 3f, 0f, "THE NAVE", "Where the faithful fell");
            Title(ctx, "Hint_Jump", 12.5f, 0f, "Jump (Space) - hold for height", string.Empty, toast: true);
            Title(ctx, "Hint_Wall", 35.5f, 0f, "Slide down a wall, then jump off it - climb the chimney", string.Empty, toast: true);

            // A raised gallery along the back wall, up steps that climb away from the camera: an optional high route
            // (pots up there, and a Shade to knock off it).
            Box(ctx, "Nave_Gallery", 19.5f, 36f, 0f, 1.2f, 1.5f, Wide.Back);
            StairsIntoDepth(ctx, "Nave_Gallery_Stairs", 17.5f, 19.5f, -0.3f, Wide.Back, 0f, 1.2f);

            Fight(ctx, "Nave_Fight", new Vector3(21.5f, 2f, 0f), new Vector3(2f, 6f, 8f), new[]
            {
                new[] { ("Shade", 28f, 0f, 0.6f), ("Shade", 31f, 1.2f, 2.2f), ("Ghoul", 34f, 0f, -0.5f) },
            });
            Fight(ctx, "Nave_Worm", new Vector3(44f, 7f, 0f), new Vector3(2f, 4f, 8f), new[]
            {
                new[] { ("FireWorm", 55f, 5f, 0f) },
            }, startToast: "Parry (L) a fireball to send it back");

            Panels(ctx, "Nave_Wall", -4f, 58f, 0f, Wide, "torch", "window", "pillar", "window", "gargoyle", "window", "pillar");

            // 3D set dressing.
            Colonnade(ctx, "Nave_Columns_A", -4f, 17f, 0f, 9.5f, Wide);
            Colonnade(ctx, "Nave_Columns_B", 19.5f, 36f, 1.2f, 8.3f, Wide);
            Colonnade(ctx, "Nave_Columns_C", 41f, 58f, 5f, 4.5f, Wide);
            Arch(ctx, "Nave_Arch_Entry", 6f, 0f, 3.6f, Wide);
            Arch(ctx, "Nave_Arch_Pit", 15.5f, 0f, 3.6f, Wide);
            Arch(ctx, "Nave_Arch_Ledge", 47f, 5f, 3.2f, Wide);
            Rafters(ctx, "Nave_Rafters", -4f, 58f, 9.5f, Wide);
            Pews(ctx, "Nave_Pews_A", 0f, 13f, Wide);
            Pews(ctx, "Nave_Pews_B", 18f, 36f, Wide);
            Rubble(ctx, "Nave_Rubble_Pit", 13.6f, 0f, Wide, 11);
            Rubble(ctx, "Nave_Rubble_Chimney", 38.6f, 0f, Wide, 12);
        }

        // ============ 2. The Crypt (x 58..114) ============

        /// <summary>
        /// Develop movement under pressure: a descent of platforms over a long pit. The first gap needs the air dash;
        /// casters on the far platforms open fire once the player commits. A brawl waits at the bottom.
        /// </summary>
        private static void BuildCrypt(Ctx ctx)
        {
            LaneWalls(ctx, "Crypt", 58f, 114f, -14f, 12f, Narrow);

            Floor(ctx, "Crypt_Top", 58f, 66f, 0f, Narrow);
            Floor(ctx, "Crypt_Step_1", 71.5f, 77f, -1.5f, Narrow, -3.5f);
            Floor(ctx, "Crypt_Step_2", 80f, 86f, -3.5f, Narrow, -5.5f);
            Floor(ctx, "Crypt_Step_3", 89.5f, 95f, -5f, Narrow, -7f);
            Floor(ctx, "Crypt_Bottom", 98f, 114f, -7f, Narrow, -14f);
            KillPit(ctx, "Crypt_Pit", 66f, 98f, -12f, Narrow);

            Checkpoint(ctx, "CP_1_Crypt", 61f, 0f);
            Title(ctx, "Title_Crypt", 62.5f, 0f, "THE CRYPT", "Down into the dark");
            Title(ctx, "Hint_Dash", 64.5f, 0f, "Too far to jump? Dash in the air (Shift)", string.Empty, toast: true);

            Fight(ctx, "Crypt_Casters", new Vector3(74f, 0.5f, 0f), new Vector3(5f, 4f, 4f), new[]
            {
                new[] { ("FireWorm", 84.5f, -3.5f, 0f), ("Wizard", 93.5f, -5f, 0f) },
            });
            Fight(ctx, "Crypt_Brawl", new Vector3(99.5f, -5f, 0f), new Vector3(2f, 4f, 4f), new[]
            {
                new[] { ("Ghoul", 106f, -7f, 0.5f), ("Ghoul", 109f, -7f, -0.5f), ("Shade", 111f, -7f, 0f) },
            });

            Panels(ctx, "Crypt_Upper", 58f, 72f, 0f, Narrow, "torch", "gargoyle");
            Panels(ctx, "Crypt_Lower", 66f, 114f, -7f, Narrow, "window", "torch", "pillar", "gargoyle", "torch", "window");

            Arch(ctx, "Crypt_Arch_Entry", 60f, 0f, 3.2f, Narrow);
            Arch(ctx, "Crypt_Arch_Bottom", 101.5f, -7f, 3.2f, Narrow);
            Rubble(ctx, "Crypt_Rubble_1", 72f, -1.5f, Narrow, 21);
            Rubble(ctx, "Crypt_Rubble_2", 90.5f, -5f, Narrow, 22);
            Rubble(ctx, "Crypt_Rubble_3", 105f, -7f, Narrow, 23);
        }

        // ============ 3. The Bell Tower (x 114..179) ============

        /// <summary>
        /// The grapple, kishotenketsu style: one ring over a pit (teach), two rings in a row (develop), then a swinging
        /// climb with angels diving at the player mid-air (twist). The climb ends on the belfry floor.
        /// </summary>
        private static void BuildTower(Ctx ctx)
        {
            LaneWalls(ctx, "Tower", 114f, 179f, -14f, 14f, Narrow);

            // Teach: one ring, one pit.
            Anchor(ctx, "Ring_1", 119f, -3.2f);
            Floor(ctx, "Tower_Landing_1", 124f, 130f, -7f, Narrow, -14f);
            KillPit(ctx, "Tower_Pit_1", 114f, 124f, -12f, Narrow, 10);

            // Develop: two rings, swing to swing.
            Anchor(ctx, "Ring_2", 134.5f, -3.2f);
            Anchor(ctx, "Ring_3", 141.5f, -3.2f);
            Floor(ctx, "Tower_Landing_2", 147f, 151f, -6f, Narrow, -14f);
            KillPit(ctx, "Tower_Pit_2", 130f, 147f, -12f, Narrow);

            // Twist: swing upward, with angels diving in.
            Anchor(ctx, "Ring_4", 154f, -2.2f);
            Floor(ctx, "Tower_Perch", 156.5f, 158.5f, -3.5f, Narrow, -5f);
            Anchor(ctx, "Ring_5", 161.5f, 0.6f);
            Anchor(ctx, "Ring_6", 165f, 3.4f); // high enough that the arc clears the belfry edge
            Floor(ctx, "Tower_Belfry", 168f, 179f, 0f, Narrow, -14f);
            KillPit(ctx, "Tower_Pit_3", 151f, 168f, -12f, Narrow);

            Checkpoint(ctx, "CP_2_Tower", 112.5f, -7f);
            Title(ctx, "Title_Tower", 113.2f, -7f, "THE BELL TOWER", "Swing, or fall");
            Title(ctx, "Hint_Grapple", 113.8f, -7f, "Grapple (E) the glowing ring - hold a direction to swing, jump to let go", string.Empty, toast: true);
            Checkpoint(ctx, "CP_3_Belfry", 170.5f, 0f);

            Fight(ctx, "Tower_Angels", new Vector3(149f, -4f, 0f), new Vector3(3f, 4f, 4f), new[]
            {
                new[] { ("Angel", 157f, -0.5f, 0f), ("Angel", 163f, 2.5f, 0f) },
            });

            Panels(ctx, "Tower_Wall", 114f, 168f, -7f, Narrow, "window", "pillar", "torch", "window", "gargoyle");
            Panels(ctx, "Tower_Belfry", 168f, 179f, 0f, Narrow, "torch", "window");

            Arch(ctx, "Tower_Arch_Entry", 111.5f, -7f, 3.2f, Narrow);
            Bell(ctx, "Tower_Bell_Pit", new Vector3(127f, 1.5f, 1f), 0.9f);
            Bell(ctx, "Tower_Bell_Belfry", new Vector3(173.5f, 4f, 0.6f), 1.2f);
            Rafters(ctx, "Tower_Belfry_Rafters", 168f, 179f, 6.5f, Narrow, 3.5f);
            Rubble(ctx, "Tower_Rubble", 125f, -7f, Narrow, 31);
        }

        // ============ 4. The Choir (x 179..211) ============

        /// <summary>The gates slam shut: three waves mixing everything met so far, ending with the elite.</summary>
        private static void BuildChoir(Ctx ctx)
        {
            LaneWalls(ctx, "Choir", 179f, 211f, -4f, 14f, Wide);
            Floor(ctx, "Choir_Floor", 179f, 211f, 0f, Wide);
            Box(ctx, "Choir_Dais", 193f, 197f, 0f, 0.9f, -1f, 1f);

            GameObject west = Gate(ctx, "Choir_Gate_West", 180f, 0f, Wide);
            GameObject east = Gate(ctx, "Choir_Gate_East", 210f, 0f, Wide);
            Title(ctx, "Title_Choir", 181.5f, 0f, "THE CHOIR", "None leave until the hymn ends");

            // Balconies along the back wall, stairs down at their inner ends: the casters shoot from up there, so the
            // fight has a high ground to take (or a chain to yank them off it).
            Box(ctx, "Choir_Balcony_West", 183.5f, 190f, 0f, 2.4f, 1.8f, Wide.Back);
            StairsAlongX(ctx, "Choir_Stairs_West", 190f, 192.6f, 1.8f, Wide.Back, 0f, 2.4f);
            Box(ctx, "Choir_Balcony_East", 200f, 206.5f, 0f, 2.4f, 1.8f, Wide.Back);
            StairsAlongX(ctx, "Choir_Stairs_East", 200f, 197.4f, 1.8f, Wide.Back, 0f, 2.4f);

            Fight(ctx, "Choir_Gauntlet", new Vector3(184f, 2f, 0f), new Vector3(2f, 4f, 6.5f), new[]
            {
                new[] { ("Shade", 192f, 0f, 1f), ("Shade", 198f, 0f, -1.5f), ("Ghoul", 204f, 0f, 0f) },
                new[] { ("FireWorm", 203.5f, 2.4f, 2.4f), ("Wizard", 186.5f, 2.4f, 2.4f), ("Ghoul", 196f, 0f, -1f) },
                new[] { ("EvilWizard", 195f, 0f, -0.5f), ("Angel", 190f, 3f, -1f) },
            }, new[] { west, east }, "The doors are sealed!", "The hymn ends.");

            if (ctx.MothsClip != null)
            {
                foreach (Vector3 p in new[] { new Vector3(186f, 3f, 2.4f), new Vector3(202f, 3.5f, 2.6f) })
                {
                    var moths = new GameObject("Choir_Moths");
                    moths.transform.SetParent(ctx.Decor, false);
                    moths.transform.position = p;
                    AnimatedSprite(moths, ctx.MothsClip, ctx.GlowMaterial);
                }
            }

            Panels(ctx, "Choir_Wall", 179f, 211f, 0f, Wide, "window", "pillar", "window", "torch", "window", "pillar");

            Column(ctx, "Choir_Column_0", 181.5f, Wide.Back - 0.35f, 0f, 9f);
            Column(ctx, "Choir_Column_1", 187f, Wide.Back - 0.35f, 2.4f, 6.6f);
            Column(ctx, "Choir_Column_2", 195f, Wide.Back - 0.35f, 0f, 9f);
            Column(ctx, "Choir_Column_3", 203.5f, Wide.Back - 0.35f, 2.4f, 6.6f);
            Column(ctx, "Choir_Column_4", 208.5f, Wide.Back - 0.35f, 0f, 9f);
            Rafters(ctx, "Choir_Rafters", 179f, 211f, 9f, Wide);
            Pews(ctx, "Choir_Pews", 182f, 209f, Wide);
        }

        // ============ 5. The Altar (x 211..250) ============

        /// <summary>The boss: a wide room, rings overhead to swing clear of the shadow dash, the exit behind it.</summary>
        private static void BuildAltar(Ctx ctx)
        {
            LaneWalls(ctx, "Altar", 211f, 250f, -4f, 16f, AltarLane);
            Floor(ctx, "Altar_Floor", 211f, 250f, 0f, AltarLane);
            Box(ctx, "Altar_Wall_End", 248f, 250f, 0f, 16f, AltarLane.Front - 1f, AltarLane.Back, "brick", "brick");

            Checkpoint(ctx, "CP_4_Altar", 213f, 0f);
            GameObject entrance = Gate(ctx, "Altar_Gate_West", 215f, 0f, AltarLane, 6f);
            GameObject exit = Gate(ctx, "Altar_Gate_East", 242f, 0f, AltarLane, 6f);

            // High: the bottom of the swing stays above the shadow dash (1.05 tall) and off the floor.
            Anchor(ctx, "Ring_Altar_1", 221f, 5.6f);
            Anchor(ctx, "Ring_Altar_2", 228.5f, 6f);
            Anchor(ctx, "Ring_Altar_3", 236f, 5.6f);

            BossController boss = PlaceBoss(ctx, new Vector3(236f, 0.15f, 0f));
            var arena = Volume<BossArena>(ctx, "Altar_BossArena", new Vector3(218f, 2f, 0f), new Vector3(2f, 4f, AltarLane.Depth + 0.5f));
            EnemySetup.Set(arena, so =>
            {
                so.FindProperty("_boss").objectReferenceValue = boss;
                so.FindProperty("_bar").objectReferenceValue = Object.FindAnyObjectByType<BossHealthBar>(FindObjectsInactive.Include);
                so.FindProperty("_minX").floatValue = 216.5f;
                so.FindProperty("_maxX").floatValue = 240.5f;
                HeroSetup.SetArray(so.FindProperty("_gates"), entrance);
                HeroSetup.SetArray(so.FindProperty("_exitBlockers"), exit);
            });

            // A stepped dais behind the arena, under the altar panel.
            Box(ctx, "Altar_Dais_Step", 223.6f, 233.4f, 0f, 0.22f, 0.8f, AltarLane.Back);
            Box(ctx, "Altar_Dais", 224f, 233f, 0f, 0.44f, 1.2f, AltarLane.Back);
            Colonnade(ctx, "Altar_Columns", 211f, 248f, 0f, 10.5f, AltarLane, 6f);
            Rafters(ctx, "Altar_Rafters", 211f, 248f, 10.5f, AltarLane, 4f);
            Arch(ctx, "Altar_Arch_Entry", 213.8f, 0f, 4.2f, AltarLane);
            Rubble(ctx, "Altar_Rubble", 240f, 0f, AltarLane, 41);

            Title(ctx, "Title_End", 245f, 0f, "THE END", "Thanks for playing Feared");
            PointLight(ctx, "Altar_Light", new Vector3(228.5f, 5f, 2.5f), new Color(1f, 0.25f, 0.2f), 14f, 3f);
            Panels(ctx, "Altar_Wall", 211f, 248f, 0f, AltarLane, "torch", "gargoyle", "window", "pillar", "altar", "pillar", "window", "gargoyle", "torch");
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
    }
}
