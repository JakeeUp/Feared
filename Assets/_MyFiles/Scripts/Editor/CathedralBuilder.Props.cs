using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Brawler.Editor
{
    public static partial class CathedralBuilder
    {
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

        private static void Stone(Ctx ctx, GameObject go, string material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ctx.Stone[material];
            renderer.shadowCastingMode = go.transform.position.z > 10f ? ShadowCastingMode.Off : ShadowCastingMode.On;
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
