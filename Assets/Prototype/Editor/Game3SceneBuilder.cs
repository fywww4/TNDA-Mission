using System;
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
    public static class Game3SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GAME3.unity";
        private const string ArtPath = "Assets/Prototype/Game3/Art";
        private static Sprite square, circle;
        private static TMP_FontAsset font;

        public static object Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building GAME3.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            square = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Circle.png");
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (square == null || circle == null || font == null)
                throw new InvalidOperationException("Missing built-in sprites or TMP Essentials.");
            Sprite[] sprites = { ImportCar("CarBlue"), ImportCar("CarYellow"), ImportCar("CarWhite") };
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/CarPalette.mat");
            if (material == null)
            {
                var shader = Shader.Find("TNDA/Game3 Car Palette");
                if (shader == null) throw new InvalidOperationException("GAME3 car shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ArtPath + "/CarPalette.mat");
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.GetRootGameObjects().Any(g => g.name == "GAME3 Prototype"))
                    throw new InvalidOperationException("GAME3 already exists; update its existing objects instead of rebuilding.");
                var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
                if (camera == null) throw new InvalidOperationException("GAME3 has no prepared camera.");
                ConfigureCamera(camera);
                var root = new GameObject("GAME3 Prototype");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Build GAME3 prototype");
                var game = root.AddComponent<DemolitionGame>();
                game.sceneCamera = camera;
                game.cabinetTitle = "SCRAPYARD\nSMASH";
                game.instructions = "SMASH PROPS. RAM CARS.\nSURVIVE 90s. SCORE BIG.\n\nW/S GAS & BRAKE  /  A/D STEER\nSPACE RAM  /  HIT SIDES & REAR\n\n2P: ARROWS + RIGHT CTRL";
                game.roundSeconds = 90;
                game.presentationSeconds = 6;
                game.speaker = root.AddComponent<AudioSource>();
                game.speaker.playOnAwake = false;
                game.speaker.spatialBlend = 0;

                BuildArena(Child(root.transform, "Scrapyard (static)"));
                var cars = Child(root.transform, "Cars (4)");
                game.cars = new ArcadeVehicle[4];
                game.carLabels = new TMP_Text[4];
                game.healthBars = new Transform[4];
                game.ramTrails = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    var car = new GameObject("Car " + i).AddComponent<ArcadeVehicle>();
                    car.transform.SetParent(cars, false);
                    car.index = i;
                    var sprite = sprites[Mathf.Min(i, 2)];
                    car.body = SpriteObject(car.transform, "Body", sprite, Vector2.zero, sprite.bounds.size, Color.white, 10);
                    car.body.sharedMaterial = material;
                    SpriteObject(car.transform, "Health frame", square, new Vector2(0, .55f), new Vector2(.6875f, .09375f), ArcadeGame.White, 20);
                    SpriteObject(car.transform, "Health backing", square, new Vector2(0, .55f), new Vector2(.625f, .03125f), Color.black, 21);
                    var fill = Child(car.transform, "Health fill");
                    fill.localPosition = new Vector3(-.3125f, .55f, 0);
                    SpriteObject(fill, "Fill", square, new Vector2(.3125f, 0), new Vector2(.625f, .03125f),
                        i == 0 ? ArcadeGame.Cyan : i == 1 ? ArcadeGame.Yellow : ArcadeGame.White, 22);
                    game.healthBars[i] = fill;
                    var label = new GameObject("Driver tag", typeof(RectTransform)).AddComponent<TextMeshPro>();
                    label.transform.SetParent(car.transform, false);
                    label.font = font;
                    label.fontSize = 2.2f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.rectTransform.sizeDelta = new Vector2(1.5f, .25f);
                    label.sortingOrder = 23;
                    game.carLabels[i] = label;
                    game.ramTrails[i] = SpriteObject(car.transform, "Ram exhaust", square, Vector2.zero,
                        new Vector2(.15625f, .375f), ArcadeGame.Yellow, 8);
                    game.cars[i] = car;
                }

                var props = Child(root.transform, "Breakables (4 fixed spawns)");
                game.props = new BreakableProp[4];
                game.props[0] = Prop(props, "CRATE +100", new Vector2(-2, -.6f), 100, 0);
                game.props[1] = Prop(props, "OIL BARREL +75", new Vector2(2, -.6f), 75, 1);
                game.props[2] = Prop(props, "FENCE +150", new Vector2(0, 1.65f), 150, 2);
                game.props[3] = Prop(props, "ROADBLOCK +200", new Vector2(0, -2.6f), 200, 3);
                BuildInterface(root.transform, game);
                ArcadeSoundSetup.Apply(game);
                game.PreparePreview();
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                var buildScenes = EditorBuildSettings.scenes.ToList();
                int index = buildScenes.FindIndex(s => s.path == ScenePath);
                if (index < 0) buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                else buildScenes[index].enabled = true;
                EditorBuildSettings.scenes = buildScenes.ToArray();
                AssetDatabase.SaveAssets();
                return new { scene = ScenePath, saved = !scene.isDirty, cars = 4, breakables = 4,
                    activityCap = 8, resolution = "320x320", spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true).Length };
            }
            finally
            {
                if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Sprite ImportCar(string name)
        {
            string path = ArtPath + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing supplied car sprite: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureCamera(Camera camera)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform }, "Configure GAME3 camera");
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false;
            var pixels = camera.GetComponent<PixelPerfectCamera>();
            if (pixels == null) pixels = Undo.AddComponent<PixelPerfectCamera>(camera.gameObject);
            pixels.assetsPPU = 32;
            pixels.refResolutionX = pixels.refResolutionY = 320;
            pixels.cropFrameX = pixels.cropFrameY = true;
            pixels.upscaleRT = true;
            pixels.pixelSnapping = false;
            EditorUtility.SetDirty(camera);
        }

        private static void BuildArena(Transform parent)
        {
            SpriteObject(parent, "Top fence", square, new Vector2(0, 3.55f), new Vector2(9.4f, .0625f), ArcadeGame.White);
            SpriteObject(parent, "Bottom fence", square, new Vector2(0, -4.4f), new Vector2(9.4f, .0625f), ArcadeGame.White);
            for (int side = -1; side <= 1; side += 2)
            {
                SpriteObject(parent, "Side fence", square, new Vector2(side * 4.7f, -.425f), new Vector2(.0625f, 8), ArcadeGame.White);
                for (float y = -4.1f; y < 3.5f; y += .5f)
                    SpriteObject(parent, "Fence post", square, new Vector2(side * 4.7f, y), new Vector2(.1875f, .0625f), ArcadeGame.White);
                SpriteObject(parent, "Top corner warning", square, new Vector2(side * 4.4f, 3.25f), new Vector2(.45f, .0625f), ArcadeGame.Yellow);
                SpriteObject(parent, "Bottom corner warning", square, new Vector2(side * 4.4f, -4.1f), new Vector2(.45f, .0625f), ArcadeGame.Yellow);
            }
            for (int x = -3; x <= 3; x += 2) for (int y = -3; y <= 2; y += 2)
                SpriteObject(parent, "Floor marking", square, new Vector2(x, y), new Vector2(.125f, .03125f), ArcadeGame.White);
        }

        private static BreakableProp Prop(Transform parent, string name, Vector2 position, int points, int style)
        {
            var view = Child(parent, name);
            view.position = ArcadeGame.PixelPosition(position);
            if (style == 0)
            {
                SpriteObject(view, "Frame", square, Vector2.zero, new Vector2(.56f, .56f), ArcadeGame.Yellow, 3);
                SpriteObject(view, "Inset", square, Vector2.zero, new Vector2(.44f, .44f), Color.black, 4);
                foreach (int sign in new[] { -1, 1 })
                {
                    var brace = SpriteObject(view, "Crate brace", square, Vector2.zero, new Vector2(.5f, .0625f), ArcadeGame.Yellow, 5);
                    brace.transform.localRotation = Quaternion.Euler(0, 0, sign * 45);
                }
            }
            else if (style == 1)
            {
                SpriteObject(view, "Barrel", circle, Vector2.zero, new Vector2(.5f, .5f), ArcadeGame.Yellow, 3);
                SpriteObject(view, "Band", square, Vector2.zero, new Vector2(.46f, .0625f), Color.black, 4);
                SpriteObject(view, "Cap", circle, new Vector2(0, .12f), new Vector2(.125f, .125f), Color.black, 4);
            }
            else if (style == 2)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    SpriteObject(view, "Post", square, new Vector2(sign * .44f, 0), new Vector2(.0625f, .44f), ArcadeGame.Cyan, 3);
                    SpriteObject(view, "Rail", square, new Vector2(0, sign * .12f), new Vector2(.9375f, .0625f), ArcadeGame.Cyan, 3);
                }
            }
            else
            {
                SpriteObject(view, "Roadblock", square, Vector2.zero, new Vector2(.9375f, .3125f), ArcadeGame.Yellow, 3);
                for (int stripe = -1; stripe <= 1; stripe++)
                {
                    var mark = SpriteObject(view, "Warning stripe", square, new Vector2(stripe * .25f, 0), new Vector2(.0625f, .28f), Color.black, 4);
                    mark.transform.localRotation = Quaternion.Euler(0, 0, -25);
                }
            }
            return new BreakableProp { view = view, point = position, points = points };
        }

        private static void BuildInterface(Transform parent, DemolitionGame game)
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
            game.hud = Text(safe, "Durability and scores", 16, 290, 288, 12, 9, ArcadeGame.Cyan);
            game.arenaBoard = Text(safe, "Time and wrecks", 182, 303, 122, 14, 8, ArcadeGame.Yellow);
            game.arenaBoard.alignment = TextAlignmentOptions.Right;
            game.notice = Text(safe, "Impact feedback", 16, 280, 288, 10, 8, ArcadeGame.Yellow);
            game.notice.alignment = TextAlignmentOptions.Center;
            game.footer = Text(safe, "Controls", 12, 2, 296, 18, 7.5f, ArcadeGame.White);
            game.footer.alignment = TextAlignmentOptions.Center;
            var panel = Image(safe, "Coin and result panel", 22, 52, 276, 222, ArcadeGame.Cyan).rectTransform;
            Image(panel, "Black inset", 2, 2, 272, 218, Color.black);
            game.overlay = panel.gameObject;
            game.overlayTitle = Text(panel, "Heading", 12, 154, 252, 58, 25, ArcadeGame.Yellow);
            game.overlayTitle.alignment = TextAlignmentOptions.Center;
            game.overlayTitle.fontStyle = FontStyles.Bold;
            game.overlayBody = Text(panel, "Instructions and results", 14, 62, 248, 88, 9, ArcadeGame.White);
            game.overlayBody.alignment = TextAlignmentOptions.Center;
            game.overlayHint = Text(panel, "Coin prompt", 10, 5, 256, 22, 8, ArcadeGame.White);
            game.overlayHint.alignment = TextAlignmentOptions.Center;
            game.onePlayerButton = Button(panel, "1P Coin", 15, 32, 116, 23, "1P / 3 RIVALS", ArcadeGame.Cyan);
            game.twoPlayerButton = Button(panel, "2P Coin", 145, 32, 116, 23, "2P / 2 RIVALS", ArcadeGame.Yellow);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(parent, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Transform Child(Transform parent, string name)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            return result;
        }

        private static SpriteRenderer SpriteObject(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order = 1)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = new Vector3(position.x, position.y, 0);
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
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
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h, float size, Color color)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name, float x, float y, float w, float h, string caption, Color color)
        {
            var image = Image(parent, name, x, y, w, h, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            var label = Text(image.transform, "Label", 2, 1, w - 4, h - 2, 9, Color.black);
            label.text = caption;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
