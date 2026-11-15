using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    public static class Mode7ObstacleArt
    {
        private const string Art = "Assets/Prototype/Game3/Art/";

        [MenuItem("TNDA/Mode 7/Apply fixed obstacles")]
        public static void Apply()
        {
            var stage = Object.FindAnyObjectByType<Mode7Stage>();
            if (stage == null || stage.gameObject.scene.path != "Assets/Scenes/GAME3_Mode7.unity")
                throw new System.InvalidOperationException("Open GAME3_Mode7 before applying fixed obstacles.");
            Normalize("ObstacleFence"); Normalize("ObstaclePipe");
            string[] names = { "ObstacleFence", "ObstaclePipe", "ObstacleWall", "ObstacleRamp" };
            var materials = new Material[4];
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Art + "Mode7Obstacle.shader");
            for (int i = 0; i < names.Length; i++)
            {
                string path = Art + names[i] + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true; importer.SaveAndReimport();
                string materialPath = Art + names[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                material.shader = shader; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                EditorUtility.SetDirty(material); materials[i] = material;
            }
            var terrain = stage.GetComponent<Mode7Obstacles>();
            if (terrain == null) terrain = stage.gameObject.AddComponent<Mode7Obstacles>();
            terrain.stage = stage; terrain.materials = materials;
            if (stage.game.obstacles == null || stage.game.obstacles.Length == 0)
                stage.game.obstacles = Layout((stage.game.arenaMin + stage.game.arenaMax) / 2);
            EditorUtility.SetDirty(terrain); EditorUtility.SetDirty(stage.game);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        }

        // Symmetric about the floor centre, leaving both start rows and all four authored crate spawns open.
        public static ArenaObstacle[] Layout(Vector2 centre)
        {
            ArenaObstacle At(ObstacleKind kind, Vector2 offset, Vector2 size, float angle, float height) =>
                new ArenaObstacle { kind = kind, point = centre + offset, size = size, angle = angle, height = height };
            return new[] {
                At(ObstacleKind.Wall, new Vector2(-3.1f, 0), new Vector2(2.4f, .5f), 90, .8f),
                At(ObstacleKind.Wall, new Vector2(3.1f, 0), new Vector2(2.4f, .5f), 90, .8f),
                At(ObstacleKind.Pipe, new Vector2(-5.9f, -2.6f), new Vector2(2.3f, .65f), 0, .8f),
                At(ObstacleKind.Pipe, new Vector2(5.9f, 2.6f), new Vector2(2.3f, .65f), 180, .8f),
                At(ObstacleKind.Fence, new Vector2(0, -2), new Vector2(1.8f, .2f), 0, .75f),
                At(ObstacleKind.Fence, new Vector2(0, 2), new Vector2(1.8f, .2f), 180, .75f),
                At(ObstacleKind.Ramp, new Vector2(-6, .1f), new Vector2(1.3f, 1.5f), 0, .45f),
                At(ObstacleKind.Ramp, new Vector2(6, -.1f), new Vector2(1.3f, 1.5f), 180, .45f)
            };
        }

        private static void Normalize(string name)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(Art + "Source/" + name + "Source.png"));
            var tile = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            var palette = new[] { new Color32(24,24,24,255), new Color32(184,184,184,255), new Color32(150,158,160,255),
                new Color32(88,88,97,255), new Color32(64,64,64,255), new Color32(40,37,45,255),
                new Color32(138,90,43,255), new Color32(92,58,26,255), new Color32(245,245,245,255) };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
            {
                Color32 c = source.GetPixel(Mathf.FloorToInt((x + .5f) * source.width / 64), Mathf.FloorToInt((y + .5f) * source.height / 32));
                Color32 closest = c.a < 128 ? new Color32(0,0,0,0) : palette.OrderBy(p =>
                    (p.r-c.r)*(p.r-c.r) + (p.g-c.g)*(p.g-c.g) + (p.b-c.b)*(p.b-c.b)).First();
                tile.SetPixel(x, y, closest);
            }
            string path = Art + name + ".png";
            byte[] bytes = tile.EncodeToPNG();
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(source); Object.DestroyImmediate(tile);
        }
    }
}
