using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Keeps the supplied originals, and reproducibly prepares the small, transparent game tiles.
    public static class Mode7AudienceArt
    {
        private const string Art = "Assets/Prototype/Game3/Art/";

        [MenuItem("TNDA/Mode 7/Apply crowd and fence")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GAME3_Mode7.unity")
                throw new System.InvalidOperationException("Open GAME3_Mode7 before applying audience art.");
            var stage = Object.FindAnyObjectByType<Mode7Stage>();
            if (stage == null) throw new System.InvalidOperationException("The scene needs Mode7Stage.");

            MakeFence();
            MakeCrowd();
            var audience = stage.GetComponent<Mode7Audience>();
            if (audience == null) audience = stage.gameObject.AddComponent<Mode7Audience>();
            audience.stage = stage;
            audience.fenceMaterial = Material("Mode7Fence", 1);
            audience.crowdMaterial = Material("Mode7Crowd", 3);
            EditorUtility.SetDirty(audience);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static Texture2D Source(string name)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Art + "Source/" + name + "Source.png"));
            return texture;
        }

        private static void MakeFence()
        {
            var source = Source("Mode7Fence");
            var tile = new Texture2D(32, 48, TextureFormat.RGBA32, false);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 32; x++)
                {
                    int topY = 47 - y;
                    bool frame = x < 4 || y < 2 || y >= 45;
                    // Trace the supplied tile's six-pixel diamonds. Its original grey backdrop is opaque;
                    // discard it rather than mistaking the darker diagonal wires for background.
                    bool lightWire = (x + topY) % 6 == 3;
                    bool darkWire = (x - topY + 60) % 6 == 4;
                    Color color = frame ? source.GetPixelBilinear((x + .5f) / 32, (y + .5f) / 48)
                        : lightWire ? new Color32(150, 151, 158, 255)
                        : darkWire ? new Color32(88, 89, 97, 255) : Color.clear;
                    tile.SetPixel(x, y, color);
                }
            Write("Mode7Fence", tile);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(tile);
        }

        private static void MakeCrowd()
        {
            var source = Source("Mode7Crowd");
            int width = source.width, height = source.height;
            var pixels = source.GetPixels32();
            var outside = new bool[pixels.Length];
            var queue = new Queue<int>();
            // Flood only white connected to an image edge, preserving the spectators' white shirts.
            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int i = y * width + x;
                var c = pixels[i];
                if (outside[i] || c.r < 218 || c.g < 218 || c.b < 218) return;
                outside[i] = true;
                queue.Enqueue(i);
            }
            for (int x = 0; x < width; x++) { Visit(x, 0); Visit(x, height - 1); }
            for (int y = 0; y < height; y++) { Visit(0, y); Visit(width - 1, y); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % width, y = i / width;
                pixels[i].a = 0;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
            }
            source.SetPixels32(pixels);
            var tile = new Texture2D(384, 64, TextureFormat.RGBA32, false);
            // All three poses share the same crop and baseline; the upper white margin is not game art.
            for (int frame = 0; frame < 3; frame++)
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        int sx = frame * (width / 3) + Mathf.FloorToInt((x + .5f) * (width / 3f) / 128);
                        int sy = height - 652 + Mathf.FloorToInt((y + .5f) * 456 / 64);
                        tile.SetPixel(frame * 128 + x, y, source.GetPixel(sx, sy));
                    }
            Write("Mode7Crowd", tile);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(tile);
        }

        private static void Write(string name, Texture2D tile)
        {
            string path = Art + name + ".png";
            byte[] bytes = tile.EncodeToPNG();
            // Identical input does not rewrite an asset that may be open in the Inspector.
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes))
                File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static Material Material(string name, int frames)
        {
            string path = Art + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("TNDA/Mode7 Boundary"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + name + ".png");
            material.SetFloat("_FrameCount", frames);
            material.SetFloat("_Frame", 0);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
