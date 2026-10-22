using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>
    /// Church half of Apply Asset Packs: re-skins the grey level boxes with Gothicvania Church stone and adds the
    /// church back wall and columns.
    /// </summary>
    /// <remarks>
    /// Boxes keep their transforms and colliders; only the mesh and materials change. Each box gets its own mesh
    /// whose UVs are scaled by the box's size, so the stone tiles at the same pixel density on every surface (a
    /// plain cube would stretch one tile over the whole face). Top faces use the floor stone, sides the block or
    /// brick texture.
    /// </remarks>
    public static partial class PackSetup
    {
        // ============ Paths ============

        private const string ChurchEnv = ThirdParty + "/gothicvania church files/Assets/ENVIRONMENT";
        private const string ChurchTileset = ChurchEnv + "/tileset.png";
        private const string ChurchBackgrounds = ChurchEnv + "/backgrounds.png";
        private const string ChurchColumn = ChurchEnv + "/column.png";
        private const string ChurchGenerated = GeneratedDir + "/Church";
        private const string ChurchMaterialDir = Root + "/Art/Materials/Church";
        private const string ChurchMeshDir = Root + "/Art/Meshes/Church";
        private const string SpriteMaterialPath = Root + "/Art/Materials/M_SpriteAlphaClip.mat";
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        // ============ Look ============

        /// <summary>Church art is drawn at 16 px per tile; at 16 px/unit it sits at the hero's pixel density.</summary>
        private const float ChurchPixelsPerUnit = 16f;

        /// <summary>Back wall panels are scaled to this height (the end walls are 7 tall).</summary>
        private const float BackWallHeight = 7f;

        /// <summary>Back edge of the 24 x 12 arena.</summary>
        private const float BackWallZ = 6.05f;


        /// <summary>Tiles cut from tileset.png (top-left image coordinates).</summary>
        private static readonly (string Name, RectInt Rect)[] ChurchTiles =
        {
            ("floor", new RectInt(64, 168, 48, 16)), // paving: the brick top of the ground strip
            ("block", new RectInt(16, 112, 32, 32)), // big cracked stone block
            ("brick", new RectInt(160, 16, 16, 48)), // dark blue brickwork
        };

        /// <summary>Panels in backgrounds.png (top-left image coordinates), separated by transparent gaps.</summary>
        private static readonly (string Name, RectInt Rect)[] ChurchPanels =
        {
            ("window", new RectInt(0, 0, 160, 192)),
            ("pillar", new RectInt(176, 0, 128, 192)),
            ("altar", new RectInt(320, 0, 128, 192)),
            ("gargoyle", new RectInt(464, 0, 80, 192)),
            ("torch", new RectInt(560, 0, 64, 192)),
        };

        /// <summary>Back wall, left to right, centred on the altar. Runs past the end walls so no edge shows.</summary>
        private static readonly string[] BackWallOrder = { "torch", "gargoyle", "window", "pillar", "altar", "pillar", "window", "gargoyle", "torch" };

        /// <summary>Which texture each level object uses on its top and sides.</summary>
        private static readonly (string Name, string Top, string Sides)[] ChurchSurfaces =
        {
            ("Arena", "floor", "block"),
            ("Platform_Low", "floor", "block"),
            ("Platform_High", "floor", "block"),
            ("Pillar_Chimney_A", "floor", "brick"),
            ("Pillar_Chimney_B", "floor", "brick"),
            ("Ledge_High", "floor", "block"),
            ("Wall_End_Left", "floor", "brick"),
            ("Wall_End_Right", "floor", "brick"),
        };

        private static readonly Vector3[] ColumnPositions = { new Vector3(-10.4f, 0f, 5.2f), new Vector3(10.4f, 0f, 5.2f) };

        // ============ Build ============

        private static void BuildChurch(StringBuilder log)
        {
            if (!File.Exists(ChurchTileset))
            {
                log.AppendLine($"  ! church skipped: {ChurchTileset} not found");
                return;
            }

            Dictionary<string, Material> materials = BuildChurchMaterials();
            int skinned = 0;
            foreach (var surface in ChurchSurfaces)
            {
                GameObject box = GameObject.Find(surface.Name);
                if (box == null || box.GetComponent<MeshFilter>() == null)
                {
                    continue;
                }

                Vector3 size = box.transform.lossyScale;
                Vector2 topTile = TileWorldSize(materials[surface.Top]);
                Vector2 sideTile = TileWorldSize(materials[surface.Sides]);
                box.GetComponent<MeshFilter>().sharedMesh = SaveBoxMesh(surface.Name, size, topTile, sideTile);

                Material top = materials[surface.Top];
                if (surface.Name == "Arena")
                {
                    top = materials["floor_dark"];
                }

                box.GetComponent<MeshRenderer>().sharedMaterials = new[] { top, materials[surface.Sides] };
                skinned++;
            }

            log.AppendLine($"  * church stone on {skinned} floors/platforms/walls");
            BuildBackWall(log);
        }

        /// <summary>Cool grey for the desaturated tiles, close to the Modular Cathedral Kit's stone.</summary>
        private static readonly Color StoneTint = new Color(0.7f, 0.7f, 0.76f);

        internal static Dictionary<string, Material> BuildChurchMaterials()
        {
            Directory.CreateDirectory(ChurchMaterialDir);
            var materials = new Dictionary<string, Material>();
            foreach (var tile in ChurchTiles)
            {
                string path = $"{ChurchGenerated}/church_{tile.Name}.png";
                CropToPng(ChurchTileset, tile.Rect, path);
                Desaturate(path);
                Texture2D texture = ImportTiledTexture(path);
                materials[tile.Name] = EnsureLitMaterial($"{ChurchMaterialDir}/M_Church_{tile.Name}.mat", texture, StoneTint);
            }

            materials["floor_dark"] = EnsureLitMaterial($"{ChurchMaterialDir}/M_Church_floor_dark.mat", materials["floor"].GetTexture("_BaseMap") as Texture2D, new Color(0.38f, 0.37f, 0.42f));
            return materials;
        }

        /// <summary>
        /// The tileset is drawn in pink-violet, which no tint can turn into the kit's grey stone; keep only its light
        /// and let the material colour (and the lights) set the hue.
        /// </summary>
        private static void Desaturate(string path)
        {
            Color32[] pixels = LoadPixels(path, out int width, out int height);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                byte l = (byte)Mathf.Clamp(p.r * 0.3f + p.g * 0.59f + p.b * 0.11f, 0f, 255f);
                pixels[i] = new Color32(l, l, l, p.a);
            }

            SavePng(path, pixels, width, height);
        }

        private static Material EnsureLitMaterial(string path, Texture2D texture, Color tint)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(LitShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>World size of one repeat of the material's texture.</summary>
        internal static Vector2 TileWorldSize(Material material)
        {
            Texture texture = material.GetTexture("_BaseMap");
            return texture != null
                ? new Vector2(texture.width / ChurchPixelsPerUnit, texture.height / ChurchPixelsPerUnit)
                : Vector2.one;
        }

        // ============ Box Mesh ============

        /// <summary>
        /// Unit cube (the object's scale makes it the box) with UVs in world tiles: submesh 0 is the top face,
        /// submesh 1 the other five. Side textures run upward from the bottom edge so their lit top lines up.
        /// </summary>
        internal static Mesh SaveBoxMesh(string name, Vector3 size, Vector2 topTile, Vector2 sideTile)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var sides = new List<int>();

            // Each face: normal, the local axes spanning it (u, v), and the world sizes along them.
            AddFace(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z, topTile, vertices, normals, uvs, top);
            AddFace(Vector3.down, Vector3.right, Vector3.back, size.x, size.z, sideTile, vertices, normals, uvs, sides);
            AddFace(Vector3.back, Vector3.right, Vector3.up, size.x, size.y, sideTile, vertices, normals, uvs, sides);
            AddFace(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y, sideTile, vertices, normals, uvs, sides);
            AddFace(Vector3.left, Vector3.back, Vector3.up, size.z, size.y, sideTile, vertices, normals, uvs, sides);
            AddFace(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y, sideTile, vertices, normals, uvs, sides);

            Directory.CreateDirectory(ChurchMeshDir);
            string path = $"{ChurchMeshDir}/Box_{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.name = $"Box_{name}";
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(sides, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddFace(Vector3 normal, Vector3 uAxis, Vector3 vAxis, float uSize, float vSize, Vector2 tile,
                                    List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            int start = vertices.Count;
            Vector3 center = normal * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float u = (i == 1 || i == 2) ? 1f : 0f;
                float v = i >= 2 ? 1f : 0f;
                vertices.Add(center + uAxis * (u - 0.5f) + vAxis * (v - 0.5f));
                normals.Add(normal);
                uvs.Add(new Vector2(u * uSize / tile.x, v * vSize / tile.y));
            }

            // Wind clockwise as seen from outside (Unity's front face). Unity is left-handed: u -> v runs clockwise
            // from outside when u x v points outward, so reverse the order when it points inward.
            bool flip = Vector3.Dot(Vector3.Cross(uAxis, vAxis), normal) < 0f;
            if (flip)
            {
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            else
            {
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        // ============ Back Wall ============

        private static void BuildBackWall(StringBuilder log)
        {
            if (!File.Exists(ChurchBackgrounds))
            {
                return;
            }

            float ppu = ChurchPanels[0].Rect.height / BackWallHeight;
            var sprites = new Dictionary<string, Sprite>();
            foreach (var panel in ChurchPanels)
            {
                string path = $"{ChurchGenerated}/panel_{panel.Name}.png";
                CropToPng(ChurchBackgrounds, panel.Rect, path);
                sprites[panel.Name] = ImportSprite(path, ppu, new Vector2(0.5f, 0f));
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            GameObject root = GameObject.Find("Church") ?? EditorUtil.CreateRoot("Church");

            // Lay the panels out edge to edge, centred on the middle one.
            float total = 0f;
            foreach (string name in BackWallOrder)
            {
                total += sprites[name].bounds.size.x;
            }

            float x = -total * 0.5f;
            for (int i = 0; i < BackWallOrder.Length; i++)
            {
                Sprite sprite = sprites[BackWallOrder[i]];
                float width = sprite.bounds.size.x;
                Transform panel = FindOrCreateChild(root.transform, $"Wall_{i:00}_{BackWallOrder[i]}");
                ConfigureStaticSprite(panel.gameObject, sprite, material);
                panel.position = new Vector3(x + width * 0.5f, 0f, BackWallZ);
                panel.rotation = Quaternion.identity;
                x += width;
            }

            int columns = BuildColumns(root.transform, ppu, material);
            log.AppendLine($"  * church back wall: {BackWallOrder.Length} panels at z {BackWallZ}, {columns} columns");
        }

        private static int BuildColumns(Transform root, float ppu, Material material)
        {
            if (!File.Exists(ChurchColumn))
            {
                return 0;
            }

            string path = $"{ChurchGenerated}/column.png";
            File.Copy(ChurchColumn, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Sprite sprite = ImportSprite(path, ppu, new Vector2(0.5f, 0f));

            for (int i = 0; i < ColumnPositions.Length; i++)
            {
                Transform column = FindOrCreateChild(root, $"Column_{i}");
                ConfigureStaticSprite(column.gameObject, sprite, material);
                column.position = ColumnPositions[i];
                column.rotation = Quaternion.identity;

                // Solid trunk so nothing walks through it.
                var box = EditorUtil.GetOrAdd<BoxCollider>(column.gameObject);
                box.center = new Vector3(0f, BackWallHeight * 0.5f, 0f);
                box.size = new Vector3(sprite.bounds.size.x * 0.45f, BackWallHeight, 0.8f);
            }

            return ColumnPositions.Length;
        }

        internal static void ConfigureStaticSprite(GameObject go, Sprite sprite, Material material)
        {
            var sr = EditorUtil.GetOrAdd<SpriteRenderer>(go);
            sr.sprite = sprite;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            if (material != null)
            {
                sr.sharedMaterial = material;
            }

            go.isStatic = true;
        }
    }
}
