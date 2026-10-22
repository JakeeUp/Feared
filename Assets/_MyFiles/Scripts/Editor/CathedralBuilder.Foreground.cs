using UnityEngine;

namespace Brawler.Editor
{
    /// <summary>
    /// The Cathedral's foreground layer: silhouettes 1.5-4 units in front of the lane, between the camera and the
    /// player (pillars, railings, hanging chains, statues, fallen masonry). They frame the shot and sell the depth.
    /// Each piece is handed to <see cref="OccluderTools.MakeOccluder"/>, which dithers it out when it covers the hero
    /// and strips any collider. Tall pieces only stand where nothing important happens.
    /// </summary>
    public static partial class CathedralBuilder
    {
        // ============ Foreground Pieces ============

        /// <summary>A foreground column from <paramref name="y0"/> to <paramref name="y1"/>; broken ones end in a jagged stump.</summary>
        private static void FgPillar(Ctx ctx, string name, float x, float z, float y0, float y1, bool broken = false)
        {
            Transform root = Child(ctx.Foreground, name);
            const float radius = 0.55f;
            Deco(ctx, $"{name}_Plinth", x - 0.8f, x + 0.8f, y0, y0 + 0.7f, z - 0.8f, z + 0.8f, "floor", "block", root);
            float top = broken ? y1 : y1 - 0.6f;
            Cyl(ctx, $"{name}_Shaft", new Vector3(x, (y0 + 0.7f + top) * 0.5f, z), radius, top - y0 - 0.7f, "block", root);
            if (broken)
            {
                DecoAt(ctx, $"{name}_Break_A", new Vector3(x - 0.15f, y1 + 0.2f, z), new Vector3(0.6f, 0.7f, 0.7f), Quaternion.Euler(10f, 25f, 18f), "block", "block", root);
                DecoAt(ctx, $"{name}_Break_B", new Vector3(x + 0.25f, y1 + 0.05f, z + 0.1f), new Vector3(0.4f, 0.45f, 0.5f), Quaternion.Euler(-8f, 60f, -12f), "block", "block", root);
            }
            else
            {
                Deco(ctx, $"{name}_Capital", x - 0.85f, x + 0.85f, y1 - 0.6f, y1, z - 0.85f, z + 0.85f, "floor", "block", root);
            }

            OccluderTools.MakeOccluder(root.gameObject);
        }

        /// <summary>
        /// A chain hanging from the vault (two crossed strands read as links at any angle), ending in a weight
        /// (<paramref name="end"/> 1), a gibbet cage (2) or nothing (0).
        /// </summary>
        private static void FgChain(Ctx ctx, string name, float x, float z, float yTop, float yBottom, int end = 1)
        {
            Transform root = Child(ctx.Foreground, name);
            float length = yTop - yBottom;
            var mid = new Vector3(x, (yTop + yBottom) * 0.5f, z);
            DecoAt(ctx, $"{name}_Strand_A", mid, new Vector3(0.1f, length, 0.04f), Quaternion.Euler(0f, 45f, 0f), "brick", "brick", root);
            DecoAt(ctx, $"{name}_Strand_B", mid, new Vector3(0.1f, length, 0.04f), Quaternion.Euler(0f, -45f, 0f), "brick", "brick", root);

            if (end == 1)
            {
                Deco(ctx, $"{name}_Weight", x - 0.3f, x + 0.3f, yBottom - 0.7f, yBottom, z - 0.3f, z + 0.3f, "block", "brick", root);
            }
            else if (end == 2)
            {
                const float r = 0.55f;
                const float h = 1.5f;
                Cyl(ctx, $"{name}_Cage_Top", new Vector3(x, yBottom, z), r, 0.08f, "brick", root);
                Cyl(ctx, $"{name}_Cage_Bottom", new Vector3(x, yBottom - h, z), r, 0.08f, "brick", root);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    float bx = x + Mathf.Cos(a) * r * 0.92f;
                    float bz = z + Mathf.Sin(a) * r * 0.92f;
                    Deco(ctx, $"{name}_Cage_Bar_{i}", bx - 0.03f, bx + 0.03f, yBottom - h, yBottom, bz - 0.03f, bz + 0.03f, "brick", "brick", root);
                }
            }

            OccluderTools.MakeOccluder(root.gameObject);
        }

        /// <summary>A low stone balustrade along X standing on <paramref name="y"/>, with a gap where a baluster fell.</summary>
        private static void FgRail(Ctx ctx, string name, float x0, float x1, float z, float y, int seed)
        {
            Transform root = Child(ctx.Foreground, name);
            var random = new System.Random(seed);
            Deco(ctx, $"{name}_Newel_W", x0 - 0.2f, x0 + 0.2f, y, y + 1.2f, z - 0.2f, z + 0.2f, "floor", "block", root);
            Deco(ctx, $"{name}_Newel_E", x1 - 0.2f, x1 + 0.2f, y, y + 1.2f, z - 0.2f, z + 0.2f, "floor", "block", root);
            Deco(ctx, $"{name}_Base", x0, x1, y, y + 0.2f, z - 0.15f, z + 0.15f, "floor", "block", root);
            Deco(ctx, $"{name}_Rail", x0, x1, y + 0.9f, y + 1.05f, z - 0.17f, z + 0.17f, "floor", "block", root);

            int count = Mathf.Max(1, Mathf.FloorToInt((x1 - x0) / 0.7f) - 1);
            int missing = random.Next(count);
            for (int i = 0; i < count; i++)
            {
                if (i == missing)
                {
                    continue;
                }

                float bx = x0 + (x1 - x0) * (i + 1f) / (count + 1f);
                Deco(ctx, $"{name}_Baluster_{i:00}", bx - 0.08f, bx + 0.08f, y + 0.2f, y + 0.9f, z - 0.08f, z + 0.08f, "floor", "block", root);
            }

            OccluderTools.MakeOccluder(root.gameObject);
        }

        /// <summary>A winged stone saint on a plinth, <paramref name="height"/> tall overall.</summary>
        private static void FgStatue(Ctx ctx, string name, float x, float z, float y, float height = 3f)
        {
            Transform root = Child(ctx.Foreground, name);
            float plinth = height * 0.3f;
            Deco(ctx, $"{name}_Plinth", x - 0.6f, x + 0.6f, y, y + plinth, z - 0.6f, z + 0.6f, "floor", "block", root);

            float body = height * 0.55f;
            GameObject robe = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            robe.name = $"{name}_Robe";
            robe.transform.SetParent(root, false);
            robe.transform.position = new Vector3(x, y + plinth + body * 0.5f, z);
            robe.transform.localScale = new Vector3(0.6f, body * 0.5f, 0.5f);
            Stone(ctx, robe, "block");
            NoCollider(robe);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = $"{name}_Head";
            head.transform.SetParent(root, false);
            head.transform.position = new Vector3(x, y + plinth + body + height * 0.07f, z);
            head.transform.localScale = Vector3.one * height * 0.13f;
            Stone(ctx, head, "block");
            NoCollider(head);

            // Folded wings behind the shoulders, angled back and out.
            float shoulder = y + plinth + body * 0.75f;
            DecoAt(ctx, $"{name}_Wing_L", new Vector3(x - 0.35f, shoulder, z + 0.3f), new Vector3(0.12f, body * 0.9f, 0.55f), Quaternion.Euler(0f, 30f, 12f), "block", "block", root);
            DecoAt(ctx, $"{name}_Wing_R", new Vector3(x + 0.35f, shoulder, z + 0.3f), new Vector3(0.12f, body * 0.9f, 0.55f), Quaternion.Euler(0f, -30f, -12f), "block", "block", root);
            ctx.Count += 2;

            OccluderTools.MakeOccluder(root.gameObject);
        }

        /// <summary>Fallen masonry: a few big tumbled blocks and a broken column drum.</summary>
        private static void FgBoulder(Ctx ctx, string name, float x, float z, float y, int seed)
        {
            Transform root = Child(ctx.Foreground, name);
            var random = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float s = 0.5f + (float)random.NextDouble() * 0.6f;
                var p = new Vector3(x + ((float)random.NextDouble() - 0.5f) * 2.4f, y + s * 0.45f, z + ((float)random.NextDouble() - 0.5f) * 1.2f);
                Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 20f - 10f, (float)random.NextDouble() * 90f, (float)random.NextDouble() * 24f - 12f);
                DecoAt(ctx, $"{name}_{i}", p, new Vector3(s * 1.3f, s, s), rotation, "block", "block", root);
            }

            GameObject drum = Cyl(ctx, $"{name}_Drum", new Vector3(x + 0.9f, y + 0.5f, z - 0.4f), 0.5f, 1.1f, "block", root);
            drum.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 90f);

            OccluderTools.MakeOccluder(root.gameObject);
        }
    }
}
