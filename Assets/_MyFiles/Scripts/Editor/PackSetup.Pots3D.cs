using System.IO;
using System.Linq;
using Brawler.Presentation;
using Brawler.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Editor
{
    /// <summary>
    /// 3D pots from FabinhoSC's CC0 "Stylized Vases And Pots" (opengameart.org/content/stylized-vases-and-pots).
    /// Every model ships with a pre-fractured twin whose cells become the shards, so pots break apart in the world
    /// instead of popping into flat sprites.
    /// </summary>
    public static partial class PackSetup
    {
        private const string Pots3DRoot = ThirdParty + "/Pots3D/Stylized Vases And Pots";
        private const string Pots3DModels = Pots3DRoot + "/Models/FBX";
        private const string Pots3DGenerated = GeneratedDir + "/Pots3D";

        /// <summary>Model and height for each sprite pot shape (small, jug, urn), so placements keep their meaning.</summary>
        private static readonly (string Model, float Height)[] Pot3DShapes =
        {
            ("StylizedPot02{0}(Small)", 0.6f),
            ("StylizedVase01{0}(Little)", 0.8f),
            ("StylizedVase03{0}(Big)", 1.1f),
        };

        /// <summary>Texture per sprite colour slot; each is a palette swap on the same UVs. Tinted down for the cathedral.</summary>
        private static readonly (int Texture, Color Tint)[] Pot3DColours =
        {
            (4, new Color(0.55f, 0.6f, 0.5f)), (1, new Color(0.8f, 0.72f, 0.68f)),
            (2, new Color(0.85f, 0.8f, 0.78f)), (3, new Color(0.6f, 0.68f, 0.7f)),
        };

        private static bool Pots3DAvailable() => Directory.Exists(Pots3DModels);

        private static void BuildPot3D(Transform root, int index, Vector3 position, int colour, int shape, int hits,
                                       int hurtboxLayer, ScoreWallet wallet, VfxPlayer vfx, SpriteClip puff)
        {
            // Alternate the two sculpt variations so neighbouring pots differ.
            bool second = index % 2 == 1;
            string variation = second ? "Variation02" : "Variation01";
            string suffix = second ? "(Variation02)" : string.Empty;
            string model = string.Format(Pot3DShapes[shape].Model, suffix);
            string broken = string.Format(Pot3DShapes[shape].Model, "(Parts)" + suffix);
            GameObject intactModel = LoadPotModel($"{Pots3DModels}/{variation}/{model}.fbx");
            GameObject brokenModel = LoadPotModel($"{Pots3DModels}/Broken/{variation}/{broken}.fbx");
            if (intactModel == null || brokenModel == null)
            {
                return;
            }

            var go = new GameObject($"Pot_{index}");
            go.transform.SetParent(root, false);
            go.transform.position = position;

            // Visual and pieces share one parent, so scaling and grounding it lines the fragments up with the pot.
            var model3D = new GameObject("Model");
            model3D.transform.SetParent(go.transform, false);
            model3D.transform.localRotation = Quaternion.Euler(0f, (index * 137.5f) % 360f, 0f);
            GameObject visual = Object.Instantiate(intactModel, model3D.transform, false);
            visual.name = "Visual";
            GameObject pieces = Object.Instantiate(brokenModel, model3D.transform, false);
            pieces.name = "Pieces";

            Material material = PotMaterial(colour);
            foreach (Renderer r in model3D.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            Renderer body = visual.GetComponentInChildren<Renderer>();
            float scale = Pot3DShapes[shape].Height / Mathf.Max(0.01f, body.bounds.size.y);
            model3D.transform.localScale = Vector3.one * scale;
            Bounds bounds = body.bounds;
            model3D.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            bounds = body.bounds;

            var solid = go.AddComponent<CapsuleCollider>();
            solid.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f;
            solid.height = Mathf.Max(bounds.size.y, solid.radius * 2f);
            solid.center = new Vector3(0f, solid.height * 0.5f, 0f);

            var prop = go.AddComponent<BreakableProp>();
            BoxCollider trigger = AddPropHurtbox(go, prop, hurtboxLayer, new Vector3(bounds.size.x, bounds.size.y, 0.6f));
            Transform[] fragments = pieces.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform).ToArray();

            EditorUtil.Set(prop, so =>
            {
                so.FindProperty("_visual").objectReferenceValue = body;
                EditorUtil.SetArray(so.FindProperty("_colliders"), solid, trigger);
                EditorUtil.SetArray(so.FindProperty("_pieces"), fragments.Cast<Object>().ToArray());
                so.FindProperty("_hitsToBreak").intValue = hits;
                so.FindProperty("_scoreReward").intValue = hits > 1 ? 50 : 25;
                so.FindProperty("_wallet").objectReferenceValue = wallet;
                so.FindProperty("_vfx").objectReferenceValue = vfx;
                so.FindProperty("_breakClip").objectReferenceValue = puff;
                so.FindProperty("_shardLifetime").floatValue = 1.6f;
            });

            foreach (Transform fragment in fragments)
            {
                fragment.gameObject.SetActive(false);
            }
        }

        private static GameObject LoadPotModel(string path)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter importer
                && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Material PotMaterial(int colour)
        {
            (int texture, Color tint) = Pot3DColours[colour % Pot3DColours.Length];
            string path = $"{Pots3DGenerated}/M_Pot_{texture}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Pots3DGenerated);
                material = new Material(Shader.Find(LitShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            // Point-filtered like the sprites, so the pots read as part of the same pixel world.
            material.SetTexture("_BaseMap", ImportPointTexture($"{Pots3DRoot}/Textures/TextureVaseAndPots0{texture}.png"));
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ImportPointTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
