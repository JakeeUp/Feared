using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    /// <summary>
    /// The Cathedral's 3D blockout kit: mock props built from primitives in church stone, so the level reads as a space
    /// rather than a strip. Columns and buttresses give the back wall relief, arches frame the walkway, rafters close the
    /// space overhead, low foreground pews/rubble sit between the camera and the lane, chandeliers carry the grapple
    /// rings. Stairs and galleries add routes that climb into depth. Decor that could snag the player has no collider.
    /// </summary>
    public static partial class CathedralBuilder
    {
        // ============ Pieces ============

        /// <summary>A round column with a square base and capital. Solid (it stands at the back wall).</summary>
        private static void Column(Ctx ctx, string name, float x, float z, float y0, float height, float radius = 0.45f)
        {
            Transform root = Child(ctx.Decor, name);
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(root, false);
            shaft.transform.position = new Vector3(x, y0 + height * 0.5f, z);
            shaft.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            Stone(ctx, shaft, "block");

            Box(ctx, $"{name}_Base", x - radius - 0.15f, x + radius + 0.15f, y0, y0 + 0.35f, z - radius - 0.15f, z + radius + 0.15f, "floor", "block", root);
            Box(ctx, $"{name}_Capital", x - radius - 0.2f, x + radius + 0.2f, y0 + height - 0.4f, y0 + height, z - radius - 0.2f, z + radius + 0.2f, "floor", "block", root);
            ctx.Count++;
        }

        /// <summary>A row of columns along the back wall, every <paramref name="spacing"/> units.</summary>
        private static void Colonnade(Ctx ctx, string name, float x0, float x1, float y0, float height, Lane lane, float spacing = 8f)
        {
            int i = 0;
            for (float x = x0 + spacing * 0.5f; x < x1; x += spacing)
            {
                Column(ctx, $"{name}_{i++:00}", x, lane.Back - 0.35f, y0, height);
            }
        }

        /// <summary>An arch over the walkway: a pillar in front of the lane, one at the back, a lintel across.</summary>
        private static void Arch(Ctx ctx, string name, float x, float y0, float height, Lane lane)
        {
            Transform root = Child(ctx.Decor, name);
            const float half = 0.35f;
            NoCollider(Box(ctx, $"{name}_Front", x - half, x + half, y0, y0 + height, lane.Front - 0.9f, lane.Front - 0.2f, "floor", "block", root));
            Box(ctx, $"{name}_Back", x - half, x + half, y0, y0 + height, lane.Back - 0.6f, lane.Back, "floor", "block", root);
            NoCollider(Box(ctx, $"{name}_Lintel", x - half - 0.1f, x + half + 0.1f, y0 + height, y0 + height + 0.6f, lane.Front - 0.9f, lane.Back, "floor", "block", root));
        }

        /// <summary>Roof beams across the lane every few units (decor, above the action).</summary>
        private static void Rafters(Ctx ctx, string name, float x0, float x1, float y, Lane lane, float spacing = 5f)
        {
            Transform root = Child(ctx.Decor, name);
            int i = 0;
            for (float x = x0 + spacing * 0.5f; x < x1; x += spacing)
            {
                NoCollider(Box(ctx, $"{name}_{i++:00}", x - 0.2f, x + 0.2f, y, y + 0.5f, lane.Front - 2f, lane.Back, "brick", "brick", root));
            }

            NoCollider(Box(ctx, $"{name}_Ridge", x0, x1, y + 0.5f, y + 0.8f, (lane.Front + lane.Back) * 0.5f - 0.2f, (lane.Front + lane.Back) * 0.5f + 0.2f, "brick", "brick", root));
        }

        /// <summary>Low pews in front of the lane (between camera and player): depth without hiding anything.</summary>
        private static void Pews(Ctx ctx, string name, float x0, float x1, Lane lane, float y = 0f)
        {
            Transform root = Child(ctx.Decor, name);
            int i = 0;
            for (float x = x0; x + 1.6f < x1; x += 2.4f)
            {
                NoCollider(Box(ctx, $"{name}_{i}_Seat", x, x + 1.6f, y, y + 0.35f, lane.Front - 1.9f, lane.Front - 1.4f, "floor", "block", root));
                NoCollider(Box(ctx, $"{name}_{i}_Back", x, x + 1.6f, y, y + 0.7f, lane.Front - 2.05f, lane.Front - 1.9f, "floor", "block", root));
                i++;
            }
        }

        /// <summary>Tumbled stone blocks in front of the lane.</summary>
        private static void Rubble(Ctx ctx, string name, float x, float y, Lane lane, int seed)
        {
            Transform root = Child(ctx.Decor, name);
            var random = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float s = 0.25f + (float)random.NextDouble() * 0.35f;
                float px = x + (float)random.NextDouble() * 1.6f;
                float pz = lane.Front - 1.2f - (float)random.NextDouble() * 1.2f;
                GameObject block = Box(ctx, $"{name}_{i}", px - s, px + s, y, y + s * 1.4f, pz - s, pz + s, "block", "block", root);
                NoCollider(block);
                block.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 70f, (float)random.NextDouble() * 20f - 10f);
            }
        }

        /// <summary>
        /// Stairs climbing into depth: <paramref name="steps"/> steps from the front of the lane toward the back, each a
        /// walkable rise (under the controller's step offset), ending at <paramref name="topY"/>.
        /// </summary>
        private static void StairsIntoDepth(Ctx ctx, string name, float x0, float x1, float z0, float z1, float baseY, float topY)
        {
            Transform root = Child(ctx.Geometry, name);
            const float rise = 0.22f;
            int steps = Mathf.Max(1, Mathf.CeilToInt((topY - baseY) / rise));
            float depth = (z1 - z0) / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = Mathf.Min(topY, baseY + rise * (i + 1));
                Box(ctx, $"{name}_{i:00}", x0, x1, baseY, top, z0 + depth * i, z1, "floor", "block", root);
            }
        }

        /// <summary>Stairs along X from <paramref name="topY"/> at x0 down to <paramref name="baseY"/> at x1 (either direction).</summary>
        private static void StairsAlongX(Ctx ctx, string name, float x0, float x1, float z0, float z1, float baseY, float topY)
        {
            Transform root = Child(ctx.Geometry, name);
            const float rise = 0.22f;
            int steps = Mathf.Max(1, Mathf.CeilToInt((topY - baseY) / rise));
            float run = (x1 - x0) / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = topY - rise * (i + 1);
                if (top <= baseY + 0.01f)
                {
                    break;
                }

                float a = x0 + run * i;
                float b = x0 + run * (i + 1);
                Box(ctx, $"{name}_{i:00}", Mathf.Min(a, b), Mathf.Max(a, b), baseY, top, z0, z1, "floor", "block", root);
            }
        }

        /// <summary>A hanging iron ring of candles: the 3D mount a grapple ring hangs from.</summary>
        private static void Chandelier(Ctx ctx, Transform parent, Vector3 ringPosition)
        {
            Vector3 top = ringPosition + Vector3.up * 1.2f;
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Chandelier";
            disc.transform.SetParent(parent, false);
            disc.transform.position = top;
            disc.transform.localScale = new Vector3(1.3f, 0.05f, 1.3f);
            Stone(ctx, disc, "brick");
            NoCollider(disc);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                var p = top + new Vector3(Mathf.Cos(angle) * 0.6f, 0.15f, Mathf.Sin(angle) * 0.6f);
                GameObject candle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                candle.name = "Candle";
                candle.transform.SetParent(disc.transform.parent, false);
                candle.transform.position = p;
                candle.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
                Stone(ctx, candle, "floor");
                NoCollider(candle);
            }

            NoCollider(Box(ctx, "Chain_Up", top.x - 0.03f, top.x + 0.03f, top.y, top.y + 6f, top.z - 0.03f, top.z + 0.03f, "brick", "brick", parent));
            NoCollider(Box(ctx, "Chain_Ring", ringPosition.x - 0.03f, ringPosition.x + 0.03f, ringPosition.y + 0.25f, top.y, ringPosition.z - 0.03f, ringPosition.z + 0.03f, "brick", "brick", parent));

            var light = new GameObject("CandleLight").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = top + Vector3.up * 0.3f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.4f);
            light.range = 4.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>A great bronze-dark bell hanging from a beam (decor).</summary>
        private static void Bell(Ctx ctx, string name, Vector3 position, float size)
        {
            Transform root = Child(ctx.Decor, name);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.position = position;
            body.transform.localScale = new Vector3(size, size * 0.55f, size);
            Stone(ctx, body, "brick");
            NoCollider(body);

            GameObject lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lip.name = "Lip";
            lip.transform.SetParent(root, false);
            lip.transform.position = position - Vector3.up * size * 0.55f;
            lip.transform.localScale = new Vector3(size * 1.25f, 0.06f, size * 1.25f);
            Stone(ctx, lip, "brick");
            NoCollider(lip);

            NoCollider(Box(ctx, $"{name}_Yoke", position.x - size, position.x + size, position.y + size * 0.55f, position.y + size * 0.55f + 0.3f,
                position.z - 0.15f, position.z + 0.15f, "block", "block", root));
        }

        // ============ Helpers ============

        private static void Stone(Ctx ctx, GameObject go, string material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ctx.Stone[material];
            renderer.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
        }

        private static GameObject NoCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return go;
        }
    }
}
