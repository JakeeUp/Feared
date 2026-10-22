using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    public static partial class CathedralBuilder
    {
        private static GameObject Deco(Ctx ctx, string name, float x0, float x1, float y0, float y1, float z0, float z1,
                                       string top = "floor", string sides = "brick", Transform parent = null)
        {
            var center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            return DecoAt(ctx, name, center, size, Quaternion.identity, top, sides, parent);
        }

        private static GameObject DecoAt(Ctx ctx, string name, Vector3 center, Vector3 size, Quaternion rotation,
                                         string top = "floor", string sides = "brick", Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : ctx.Decor, false);
            go.transform.SetPositionAndRotation(center, rotation);
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = StoneMaterial(ctx, top, sides);
            renderer.shadowCastingMode = center.z > 10f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            NoCollider(go);
            go.isStatic = true;
            ctx.Count++;
            return go;
        }

        private static GameObject Cyl(Ctx ctx, string name, Vector3 center, float radius, float height, string material, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            Stone(ctx, go, material);
            NoCollider(go);
            ctx.Count++;
            return go;
        }

        private static Transform WallStair(Ctx ctx, string name, float x0, float x1, float z0, float z1, float yFrom, float yTo, float run = 1.25f)
        {
            Transform root = Child(ctx.Decor, name);
            int steps = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(x1 - x0) / run));
            float dx = (x1 - x0) / steps;
            float dy = (yTo - yFrom) / steps;
            for (int i = 0; i < steps; i++)
            {
                float a = x0 + dx * i;
                float top = yFrom + dy * (i + 1);
                Deco(ctx, $"{name}_{i:00}", Mathf.Min(a, a + dx), Mathf.Max(a, a + dx), top - 0.6f, top, z0, z1, "floor", "block", root);
            }

            var from = new Vector3(x0, yFrom - 0.9f, (z0 + z1) * 0.5f);
            var to = new Vector3(x1, yTo - 0.9f, (z0 + z1) * 0.5f);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            DecoAt(ctx, $"{name}_Stringer", (from + to) * 0.5f, new Vector3(Vector3.Distance(from, to), 0.5f, (z1 - z0) * 0.8f),
                Quaternion.Euler(0f, 0f, angle), "block", "block", root);
            return root;
        }

        private static void RoseWindow(Ctx ctx, string name, float cx, float cy, float radius, float z, float yBottom, float yTop)
        {
            Transform root = Child(ctx.Decor, name);
            float r = radius;
            Deco(ctx, $"{name}_Wall_Below", cx - r, cx + r, yBottom, cy - r, z, z + 1f, "floor", "brick", root);
            Deco(ctx, $"{name}_Wall_Above", cx - r, cx + r, cy + r, yTop, z, z + 1f, "floor", "brick", root);

            float zMid = z + 0.5f;
            const int ring = 16;
            float segment = 2f * Mathf.PI * r / ring * 1.1f;
            for (int k = 0; k < ring; k++)
            {
                float a = k * 2f * Mathf.PI / ring;
                var p = new Vector3(cx + Mathf.Cos(a) * (r - 0.2f), cy + Mathf.Sin(a) * (r - 0.2f), zMid);
                DecoAt(ctx, $"{name}_Ring_{k:00}", p, new Vector3(segment, 0.4f, 0.5f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), "block", "block", root);
            }

            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                float length = r - 0.4f;
                var p = new Vector3(cx + Mathf.Cos(a) * length * 0.5f, cy + Mathf.Sin(a) * length * 0.5f, zMid);
                DecoAt(ctx, $"{name}_Spoke_{k}", p, new Vector3(length, 0.16f, 0.35f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), "block", "block", root);
            }

            GameObject hub = Cyl(ctx, $"{name}_Hub", new Vector3(cx, cy, zMid), r * 0.18f, 0.5f, "block", root);
            hub.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (ctx.Panels.TryGetValue("window", out Sprite glass))
            {
                float scale = 2f * r / glass.bounds.size.x;
                var go = new GameObject($"{name}_Glass");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(cx, cy - glass.bounds.size.y * scale * 0.5f, z + 1.05f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                PackSetup.ConfigureStaticSprite(go, glass, ctx.SpriteMaterial);
            }
        }

    }
}
