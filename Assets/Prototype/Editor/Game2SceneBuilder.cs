using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace TNDA.Prototype.Editor
{
    // Invoked through Unity CLI eval. Builds only GAME2; never replaces other scenes.
    public static class Game2SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GAME2.unity";
        private const string ArtPath = "Assets/Prototype/Game2/Art";
        private const string RootName = "GAME2 Prototype";
        private const int CarPixelHeight = 28;
        private static Sprite square, circle;
        private static TMP_FontAsset font;

        public static object Build(bool replaceExisting = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building GAME2.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            square = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Circle.png");
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();
            if (square == null || circle == null || font == null) throw new InvalidOperationException("Missing built-in sprites or TMP font resources.");

            var carSprites = new[]
            {
                MakeCarSprite("CarBlue", ArcadeGame.Cyan),
                MakeCarSprite("CarYellow", ArcadeGame.Yellow),
                MakeCarSprite("CarWhite", ArcadeGame.White),
            };

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool newlyOpened = !scene.isLoaded;
            if (newlyOpened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (existing != null)
            {
                if (!replaceExisting) throw new InvalidOperationException(RootName + " already exists; pass replaceExisting to rebuild it.");
                UnityEngine.Object.DestroyImmediate(existing);
            }
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (camera == null) throw new InvalidOperationException("GAME2 has no prepared camera.");
            ConfigureCamera(camera);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var game = root.AddComponent<EliminationRaceGame>();
            game.sceneCamera = camera;
            game.cabinetTitle = "ELIMINATION\nCIRCUIT";
            game.instructions = "FORWARD DISTANCE = POINTS\nREVERSING OR SPINNING EARNS NOTHING\n\nLOWEST SCORE OUT AT 60s AND 90s\nBEST OF THE LAST TWO WINS AT 120s\n\nW/S GAS & BRAKE  A/D STEER  SPACE ITEM\nYELLOW BOX NITRO  //  CYAN BOX BOLT";
            game.presentationSeconds = 20;
            game.speaker = root.AddComponent<AudioSource>();
            game.speaker.playOnAwake = false;
            game.speaker.spatialBlend = 0;

            BuildCircuit(Child(root.transform, "Circuit (static)"), game.trackHalfWidth);

            var carRoot = Child(root.transform, "Cars (4)");
            game.cars = new ArcadeVehicle[4];
            game.carLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                var car = new GameObject("Car " + i).AddComponent<ArcadeVehicle>();
                car.transform.SetParent(carRoot, false);
                car.index = i;
                car.body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                car.body.transform.SetParent(car.transform, false);
                car.body.sprite = carSprites[Mathf.Min(i, 2)];
                car.body.sortingOrder = 10;
                var label = new GameObject("Driver tag", typeof(RectTransform)).AddComponent<TextMeshPro>();
                label.transform.SetParent(car.transform, false);
                label.rectTransform.sizeDelta = new Vector2(1.6f, .3f);
                label.transform.localPosition = new Vector3(0, .5f, 0);
                label.font = font;
                label.fontSize = 2.2f;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.sortingOrder = 20;
                game.carLabels[i] = label;
                game.cars[i] = car;
            }

            var boxRoot = Child(root.transform, "Item Boxes (2 fixed spawns)");
            game.boxes = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                // Box 0 grants Nitro, box 1 grants Bolt (EliminationRaceGame uses item = box index + 1).
                Vector2 spot = EliminationRaceGame.TrackPoint(i == 0 ? Mathf.PI / 2 : Mathf.PI);
                var box = SpriteObject(boxRoot, i == 0 ? "Nitro box" : "Bolt box", square, spot, new Vector2(.34f, .34f),
                    i == 0 ? ArcadeGame.Yellow : ArcadeGame.Cyan, 6);
                SpriteObject(box.transform, "Inset", square, Vector2.zero, new Vector2(.62f, .62f), Color.black, 7);
                SpriteObject(box.transform, "Core", square, Vector2.zero, new Vector2(.3f, .3f), i == 0 ? ArcadeGame.Yellow : ArcadeGame.Cyan, 8);
                game.boxes[i] = box;
            }

            var pool = Child(root.transform, "Bolt Pool (2)");
            game.bolts = new ArcadeProjectile[2];
            for (int i = 0; i < 2; i++)
            {
                var bolt = SpriteObject(pool, "Bolt " + i, circle, Vector2.zero, new Vector2(.22f, .22f), ArcadeGame.Cyan, 15);
                SpriteObject(bolt.transform, "Spark", circle, Vector2.zero, new Vector2(.5f, .5f), ArcadeGame.White, 16);
                game.bolts[i] = new ArcadeProjectile { view = bolt };
            }

            BuildInterface(root.transform, game);
            ArcadeSoundSetup.Apply(game);
            game.PreparePreview();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            var summary = new
            {
                scene = ScenePath,
                spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true).Length,
                carSprites = carSprites.Select(s => s.name + " " + s.rect.width + "x" + s.rect.height).ToArray(),
                activityCap = 8,
                saved = !scene.isDirty
            };
            if (newlyOpened) EditorSceneManager.CloseScene(scene, true);
            return summary;
        }

        // Reduces a supplied car illustration to a small four-colour pixel sprite at 32 PPU.
        private static Sprite MakeCarSprite(string name, Color bodyColor)
        {
            string source = ArtPath + "/Source/" + name + "Source.png";
            string output = ArtPath + "/" + name + ".png";
            if (!File.Exists(source)) throw new InvalidOperationException("Missing car image " + source);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(source));
            int w = texture.width, h = texture.height;
            Color32[] pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);

            // Flood fill from the border through transparent or near-white pixels: that is the backdrop.
            var background = new bool[pixels.Length];
            var queue = new Queue<int>();
            bool Backdrop(int p) => pixels[p].a < 128 || Mathf.Min(pixels[p].r, Mathf.Min(pixels[p].g, pixels[p].b)) > 200;
            void Seed(int p) { if (!background[p] && Backdrop(p)) { background[p] = true; queue.Enqueue(p); } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(), x = p % w, y = p / w;
                if (x > 0) Seed(p - 1);
                if (x < w - 1) Seed(p + 1);
                if (y > 0) Seed(p - w);
                if (y < h - 1) Seed(p + w);
            }
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int p = 0; p < pixels.Length; p++)
            {
                if (background[p]) continue;
                int x = p % w, y = p / w;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            if (maxX < 0) throw new InvalidOperationException(name + " has no visible car pixels.");
            int boxW = maxX - minX + 1, boxH = maxY - minY + 1;
            int outH = CarPixelHeight, outW = Mathf.Max(4, Mathf.RoundToInt(outH * boxW / (float)boxH));

            var result = new Color32[outW * outH];
            for (int oy = 0; oy < outH; oy++)
                for (int ox = 0; ox < outW; ox++)
                {
                    int x0 = minX + ox * boxW / outW, x1 = minX + (ox + 1) * boxW / outW;
                    int y0 = minY + oy * boxH / outH, y1 = minY + (oy + 1) * boxH / outH;
                    float clear = 0, dark = 0, light = 0, body = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            int p = y * w + x;
                            if (background[p]) { clear++; continue; }
                            Color32 c = pixels[p];
                            int hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), lo = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                            if (hi < 90) dark++;
                            else if (lo > 190) light++;
                            else body++;
                        }
                    float total = clear + dark + light + body;
                    Color32 pick;
                    if (clear >= total * .5f) pick = new Color32(0, 0, 0, 0);
                    else if (dark * .8f >= light && dark * .8f >= body) pick = new Color32(0, 0, 0, 255);
                    else if (light >= body) pick = ArcadeGame.White;
                    else pick = bodyColor;
                    result[oy * outW + ox] = pick;
                }

            var small = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
            small.SetPixels32(result);
            small.Apply();
            File.WriteAllBytes(output, small.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(small);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(output);
        }

        private static void BuildCircuit(Transform parent, float halfWidth)
        {
            // Walls are layered ellipses: white rim, black fill, white rim, black infield (2px lines).
            const float a = 3.65f, b = 2.75f, cy = -.35f, line = .07f;
            float outer = halfWidth + .22f, inner = halfWidth + .22f;
            SpriteObject(parent, "Outer wall", circle, new Vector2(0, cy), new Vector2(2 * (a + outer + line), 2 * (b + outer + line)), ArcadeGame.White, 0);
            SpriteObject(parent, "Track surface", circle, new Vector2(0, cy), new Vector2(2 * (a + outer), 2 * (b + outer)), Color.black, 1);
            SpriteObject(parent, "Inner wall", circle, new Vector2(0, cy), new Vector2(2 * (a - inner), 2 * (b - inner)), ArcadeGame.White, 2);
            SpriteObject(parent, "Infield", circle, new Vector2(0, cy), new Vector2(2 * (a - inner - line), 2 * (b - inner - line)), Color.black, 3);

            var dashes = Child(parent, "Centre dashes");
            for (int i = 0; i < 28; i++)
            {
                float t = i * Mathf.PI * 2 / 28;
                Vector2 tangent = new Vector2(-a * Mathf.Sin(t), b * Mathf.Cos(t));
                var dash = SpriteObject(dashes, "Dash", square, EliminationRaceGame.TrackPoint(t), new Vector2(.16f, .035f), ArcadeGame.White, 4);
                dash.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
            }

            // Checkered start line just ahead of P1's grid slot at the bottom of the loop.
            var start = Child(parent, "Start line");
            Vector2 centre = EliminationRaceGame.TrackPoint(-Mathf.PI / 2 + .16f);
            for (int row = 0; row < 2; row++)
                for (int cell = 0; cell < 8; cell++)
                    if ((row + cell) % 2 == 0)
                        SpriteObject(start, "Check", square, centre + new Vector2(row * .1f, (cell - 3.5f) * (2 * outer / 8)),
                            new Vector2(.1f, 2 * outer / 8), ArcadeGame.White, 4);
        }

        private static void ConfigureCamera(Camera camera)
        {
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false;
            var pixels = camera.GetComponent<PixelPerfectCamera>();
            if (pixels == null) pixels = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixels.assetsPPU = 32;
            pixels.refResolutionX = pixels.refResolutionY = 320;
            pixels.cropFrameX = pixels.cropFrameY = true;
            pixels.upscaleRT = true;
            pixels.pixelSnapping = false;
            EditorUtility.SetDirty(camera);
        }

        private static void BuildInterface(Transform parent, EliminationRaceGame game)
        {
            var canvasObject = new GameObject("Arcade Interface", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320, 320);
            scaler.matchWidthOrHeight = 1;
            var safe = new GameObject("320px Cabinet", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvasObject.transform, false);
            safe.anchorMin = safe.anchorMax = safe.pivot = new Vector2(.5f, .5f);
            safe.sizeDelta = new Vector2(320, 320);

            Image(safe, "Header backing", 0, 279, 320, 41, Color.black);
            Image(safe, "Header rule", 12, 278, 296, 1, ArcadeGame.Cyan);
            game.header = Text(safe, "Title", 16, 303, 288, 14, 12, ArcadeGame.White);
            game.hud = Text(safe, "Scores", 16, 290, 288, 12, 8, ArcadeGame.White);
            game.notice = Text(safe, "Feedback", 16, 280, 288, 10, 8, ArcadeGame.Yellow);
            game.notice.alignment = TextAlignmentOptions.Center;
            game.footer = Text(safe, "Controls", 12, 5, 296, 11, 8, ArcadeGame.White);
            game.footer.alignment = TextAlignmentOptions.Center;
            // The infield shows the elimination countdown and held items.
            game.circuitBoard = Text(safe, "Elimination board", 90, 110, 140, 78, 9, ArcadeGame.Yellow);
            game.circuitBoard.alignment = TextAlignmentOptions.Center;

            var panel = Image(safe, "Coin and result panel", 22, 30, 276, 244, ArcadeGame.Cyan).rectTransform;
            Image(panel, "Black inset", 2, 2, 272, 240, Color.black);
            game.overlay = panel.gameObject;
            game.overlayTitle = Text(panel, "Heading", 12, 178, 252, 58, 24, ArcadeGame.Yellow);
            game.overlayTitle.alignment = TextAlignmentOptions.Center;
            game.overlayTitle.fontStyle = FontStyles.Bold;
            game.overlayBody = Text(panel, "Instructions", 10, 62, 256, 114, 8, ArcadeGame.White);
            game.overlayBody.alignment = TextAlignmentOptions.Center;
            game.overlayHint = Text(panel, "Coin prompt", 10, 5, 256, 22, 8, ArcadeGame.White);
            game.overlayHint.alignment = TextAlignmentOptions.Center;
            game.onePlayerButton = Button(panel, "Insert Coin 1P", 20, 32, 112, 22, "1P  /  COIN");
            game.twoPlayerButton = Button(panel, "Insert Coin 2P", 144, 32, 112, 22, "2P  /  PRESS 2");
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(parent, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static SpriteRenderer SpriteObject(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order = 1)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = new Vector3(position.x, position.y, 0);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            return renderer;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var r = Rect(parent, name, x, y, w, h);
            var image = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h, float size, Color color)
        {
            var r = Rect(parent, name, x, y, w, h);
            var text = r.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name, float x, float y, float w, float h, string caption)
        {
            var image = Image(parent, name, x, y, w, h, ArcadeGame.Cyan);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.pressedColor = Color.white;
            button.colors = colors;
            var label = Text(image.transform, "Label", 2, 1, w - 4, h - 2, 10, Color.black);
            label.text = caption;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
