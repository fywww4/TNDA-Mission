using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Ticket 25: campaign stage 3's night derby and its cars, wired into GAME3_Mode7.
    // - CarSheetBoss.png (1280x80) and CarSheetMinion.png (768x48): sliced into 16 frames like CarSheet, pivot on the
    //   ground line. The boss sheet's pixels per unit make an 80-pixel cell as wide as a 48-pixel CarSheet cell, so only
    //   the boss's own scale (its footprint, ArcadeVehicle.size) makes it bigger.
    // - DerbyDirt.png (Mode7DerbyArena; empty = a code-drawn floor), DerbyWall.png and DerbyCrowd.png (Mode7Audience),
    //   DerbyLoader.png and DerbyHulk.png (Mode7Obstacles' materials 4 and 5).
    // Missing wall, crowd, loader and hulk PNGs get placeholders (a code-drawn barrier, the crowd darkened, the wall's and
    // pipe's tiles recoloured). Overwrite a PNG with the real art and re-run; the derby terrain layout is only written
    // when DemolitionGame.derbyObstacles is empty.
    public static class Mode7DerbyArt
    {
        private const string Art = "Assets/Prototype/Game3/Art/";

        [MenuItem("TNDA/Mode 7/Apply boss arena")]
        public static void Apply()
        {
            var stage = Object.FindAnyObjectByType<Mode7Stage>();
            if (stage == null || stage.gameObject.scene.path != "Assets/Scenes/GAME3_Mode7.unity")
                throw new System.InvalidOperationException("Open GAME3_Mode7 before applying the boss arena.");
            var game = stage.game;

            if (File.Exists(Art + "CarSheetBoss.png")) stage.bossFrames = Slice("CarSheetBoss", 80, 4, 32f * 80 / 48);
            if (File.Exists(Art + "CarSheetMinion.png")) stage.minionFrames = Slice("CarSheetMinion", 48, 7, 32);
            EditorUtility.SetDirty(stage);

            if (!File.Exists(Art + "DerbyWall.png")) Write("DerbyWall", PlaceholderWall());
            if (!File.Exists(Art + "DerbyCrowd.png")) Write("DerbyCrowd", Darkened(Load(Art + "Mode7Crowd.png"), .45f));
            if (!File.Exists(Art + "DerbyLoader.png")) Write("DerbyLoader", Recoloured(Load(Art + "ObstacleWall.png"), new Color(1, .78f, .1f), 64, 30));
            if (!File.Exists(Art + "DerbyHulk.png")) Write("DerbyHulk", Recoloured(Load(Art + "ObstaclePipe.png"), new Color(.35f, .35f, .38f), 32, 18));
            AssetDatabase.Refresh();

            var audience = stage.GetComponent<Mode7Audience>();
            audience.derbyWall = Tile("DerbyWall", TextureWrapMode.Repeat);
            audience.derbyCrowd = Tile("DerbyCrowd", TextureWrapMode.Repeat);
            EditorUtility.SetDirty(audience);

            var arena = stage.GetComponent<Mode7DerbyArena>();
            if (arena == null) arena = stage.gameObject.AddComponent<Mode7DerbyArena>();
            arena.stage = stage;
            arena.dirtTile = File.Exists(Art + "DerbyDirt.png") ? Tile("DerbyDirt", TextureWrapMode.Repeat) : null;
            EditorUtility.SetDirty(arena);

            var terrain = stage.GetComponent<Mode7Obstacles>();
            var materials = terrain.materials.Take(4).ToList();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Art + "Mode7Obstacle.shader");
            foreach (var name in new[] { "DerbyLoader", "DerbyHulk" })
            {
                var texture = Tile(name, TextureWrapMode.Clamp);
                string path = Art + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.mainTexture = texture;
                EditorUtility.SetDirty(material);
                materials.Add(material);
            }
            terrain.materials = materials.ToArray();
            EditorUtility.SetDirty(terrain);

            if (game.derbyObstacles == null || game.derbyObstacles.Length == 0)
            {
                game.derbyObstacles = DerbyLayout.Obstacles(game.arenaMin, game.arenaMax);
                EditorUtility.SetDirty(game);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        }

        // Like CarSheet: point, uncompressed, 16 square frames in a row, pivot on the wheels' ground line.
        private static Sprite[] Slice(string name, int cell, int groundFromBottom, float pixelsPerUnit)
        {
            string path = Art + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var metas = new SpriteMetaData[16];
            for (int i = 0; i < 16; i++)
                metas[i] = new SpriteMetaData { name = name + "_" + i.ToString("00"), rect = new Rect(i * cell, 0, cell, cell),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, groundFromBottom / (float)cell) };
            importer.textureType = TextureImporterType.Sprite;
#pragma warning disable 618
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = metas;
#pragma warning restore 618
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var frames = new Sprite[16];
            foreach (var sprite in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>())
                frames[int.Parse(sprite.name.Substring(name.Length + 1))] = sprite;
            return frames;
        }

        private static Texture2D Tile(string name, TextureWrapMode wrap)
        {
            string path = Art + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = wrap;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        private static void Write(string name, Texture2D texture)
        {
            File.WriteAllBytes(Art + name + ".png", texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        // ---- Placeholders until the GPT art arrives ----

        private static Color32 Eight(Color c) => new Color32((byte)(Mathf.RoundToInt(c.r * 31) * 8), (byte)(Mathf.RoundToInt(c.g * 31) * 8),
            (byte)(Mathf.RoundToInt(c.b * 31) * 8), (byte)(c.a < .5f ? 0 : 255));

        // The crowd under night lights.
        private static Texture2D Darkened(Texture2D source, float light)
        {
            var pixels = source.GetPixels().Select(c => (Color)Eight(new Color(c.r * light, c.g * light, c.b * light, c.a))).ToArray();
            source.SetPixels(pixels);
            source.Apply();
            return source;
        }

        // Two 64-wide cells of the real sheet's height, each with the source's matching tile stretched across its
        // bottom `bodyHeight` rows in the given body colour (brightness kept), the rest clear.
        private static Texture2D Recoloured(Texture2D source, Color body, int cellHeight, int bodyHeight)
        {
            int tileW = source.width / 2, tileH = source.height;
            var result = new Texture2D(128, cellHeight, TextureFormat.RGBA32, false);
            result.SetPixels32(new Color32[result.width * result.height]);
            for (int y = 0; y < bodyHeight; y++)
                for (int x = 0; x < 128; x++)
                {
                    var c = source.GetPixel(x / 64 * tileW + x % 64 * tileW / 64, y * tileH / bodyHeight);
                    float v = Mathf.Max(c.r, c.g, c.b);
                    result.SetPixel(x, y, Eight(new Color(body.r * v * 1.2f, body.g * v * 1.2f, body.b * v * 1.2f, c.a)));
                }
            result.Apply();
            Object.DestroyImmediate(source);
            return result;
        }

        // 32x48: a grey concrete barrier with a light top edge (bottom 22 rows), an open railing above, clear at the top.
        private static Texture2D PlaceholderWall()
        {
            var tex = new Texture2D(32, 48, TextureFormat.RGBA32, false);
            Color32 clear = new Color32(0, 0, 0, 0), outline = new Color32(24, 24, 24, 255), dark = new Color32(56, 56, 56, 255),
                mid = new Color32(88, 88, 88, 255), light = new Color32(120, 120, 120, 255), top = new Color32(160, 160, 160, 255),
                mud = new Color32(72, 48, 24, 255), rail = new Color32(128, 136, 144, 255);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color32 c = clear;
                    if (y < 20) c = x == 0 ? outline : y < 4 && (x * 7 + y * 3) % 5 < 2 ? mud : y < 8 ? dark : y < 14 ? mid : light;
                    else if (y < 22) c = top;
                    else if (y < 44 && (x == 0 || x == 1 || x == 16 || x == 17 || y == 29 || y == 36 || y == 43)) c = rail;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }
    }
}
