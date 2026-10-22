using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brawler.Tests
{
    public sealed class WorldStoneProjectionTests
    {
        [TestCase("Brawler/WorldStone", 4)]
        [TestCase("Brawler/OccluderDither", 3)]
        public void StoneShaders_HaveRequiredPassesAndNoImportErrors(string name, int passes)
        {
            Shader shader = Shader.Find(name);
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
            var material = new Material(shader);
            try
            {
                Assert.AreEqual(passes, material.passCount);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WorldProjection_SelectsSixFacesAndRepeatsAtSixteenPixelsPerUnit()
        {
            Assume.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            Shader shader = Shader.Find("Hidden/Brawler/WorldStoneProjectionTest");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
            var material = new Material(shader);
            var top = Checker(new[] { Color.red, Color.green, Color.blue, Color.white });
            var side = Checker(new[] { Color.yellow, Color.cyan, Color.magenta, Color.black });
            var target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                material.SetTexture("_TopMap", top);
                material.SetTexture("_SideMap", side);
                material.SetVector("_TopMap_ST", new Vector4(0.5f, 0.5f, 0f, 0f));
                material.SetVector("_SideMap_ST", new Vector4(0.5f, 0.5f, 0f, 0f));
                material.SetColor("_TopColor", Color.white);
                material.SetColor("_SideColor", Color.white);
                var position = new Vector3(0.02f, 0.02f, 0.02f);
                Check(Vector3.up, position, Color.red);
                Check(Vector3.down, position, Color.magenta);
                Check(Vector3.back, position, Color.yellow);
                Check(Vector3.forward, position, Color.cyan);
                Check(Vector3.left, position, Color.cyan);
                Check(Vector3.right, position, Color.yellow);
                Check(Vector3.up, position + Vector3.right / 16f, Color.green);
                Check(Vector3.up, position + Vector3.forward / 16f, Color.blue);
                Check(Vector3.up, position + Vector3.right * 2f, Color.red);
                Check(Vector3.back, position + Vector3.up / 16f, Color.magenta);
                Check(Vector3.back, position + Vector3.up * 2f, Color.yellow);

                void Check(Vector3 normal, Vector3 point, Color expected)
                {
                    material.SetVector("_Normal", normal);
                    material.SetVector("_Position", point);
                    Graphics.Blit(null, target, material);
                    RenderTexture.active = target;
                    pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    Color actual = pixel.GetPixel(0, 0);
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), normal + " at " + point);
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), normal + " at " + point);
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), normal + " at " + point);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixel);
                Object.DestroyImmediate(top);
                Object.DestroyImmediate(side);
                Object.DestroyImmediate(material);
            }
        }

        private static Texture2D Checker(Color[] colors)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    pixels[y * 32 + x] = colors[(x & 1) + (y & 1) * 2];
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
