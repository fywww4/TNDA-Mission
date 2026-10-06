using System;
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
    // Invoked through Unity CLI eval. Builds only GAME1; never replaces other scenes.
    public static class Game1SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GAME1.unity";
        private const string ArtPath = "Assets/Prototype/Game1/Art";
        private static Sprite square, circle;
        private static TMP_FontAsset font;

        public static object Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop the running test before building GAME1.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            square = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Circle.png");
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();
            if (square == null || circle == null || font == null) throw new InvalidOperationException("Missing built-in sprites or TMP font resources.");

            string bossPath = ArtPath + "/Boss.png";
            var importer = AssetImporter.GetAtPath(bossPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Boss image must be copied into " + bossPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bossPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/BossPalette.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("TNDA/Arcade Palette"));
                AssetDatabase.CreateAsset(material, ArtPath + "/BossPalette.mat");
            }
            material.SetFloat("_WhiteKey", 1);
            EditorUtility.SetDirty(material);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool newlyOpened = !scene.isLoaded;
            if (newlyOpened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GAME1 Prototype");
            if (existing != null) throw new InvalidOperationException("GAME1 Prototype already exists; apply targeted updates instead of rebuilding.");
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (camera == null) throw new InvalidOperationException("GAME1 has no prepared camera.");
            ConfigureCamera(camera);

            var root = new GameObject("GAME1 Prototype");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Build GAME1 prototype");
            var game = root.AddComponent<BossClimbGame>();
            game.sceneCamera = camera;
            game.cabinetTitle = "BOSS\nCLIMB";
            game.instructions = "CLIMB. DODGE. KEEP FIRING.\n\nWASD MOVE  /  W JUMP OR CLIMB\nS CLIMB DOWN  /  SPACE FIRE\n\nBOSS DOWN = A FASTER NEXT WAVE\n10s TO INSERT A COIN AND REVIVE";
            game.presentationSeconds = 3;
            game.speaker = root.AddComponent<AudioSource>();
            game.speaker.playOnAwake = false;
            game.speaker.spatialBlend = 0;

            var world = Child(root.transform, "Platforms and Ladders");
            SpriteObject(world, "Left cabinet rail", square, new Vector2(-4.85f, -.35f), new Vector2(.06f, 8.4f), ArcadeGame.White);
            SpriteObject(world, "Right cabinet rail", square, new Vector2(4.85f, -.35f), new Vector2(.06f, 8.4f), ArcadeGame.White);
            for (int level = 0; level < game.floors.Length; level++)
            {
                float start = level == 0 || level % 2 == 1 ? -4.7f : -3.8f;
                float end = level == 0 || level % 2 == 0 ? 4.7f : 3.8f;
                SpriteObject(world, "Platform " + level, square, new Vector2((start + end) / 2, game.floors[level] - .05f),
                    new Vector2(end - start, .1f), ArcadeGame.White);
                SpriteObject(world, "Drop edge " + level, square, new Vector2(level % 2 == 1 ? end : start, game.floors[level] - .14f),
                    new Vector2(.13f, .3f), ArcadeGame.Yellow);
                for (int brace = 0; brace < 6; brace++)
                {
                    var beam = SpriteObject(world, "Platform brace", square, new Vector2(start + .45f + brace * 1.3f, game.floors[level] - .19f),
                        new Vector2(.4f, .035f), ArcadeGame.White);
                    beam.transform.rotation = Quaternion.Euler(0, 0, brace % 2 == 0 ? 28 : -28);
                }
            }
            for (int level = 0; level < game.ladderX.Length; level++)
            {
                float low = game.floors[level], high = game.floors[level + 1];
                var ladder = Child(world, "Ladder " + (level + 1));
                for (int side = -1; side <= 1; side += 2)
                    SpriteObject(ladder, "Rail", square, new Vector2(game.ladderX[level] + side * .17f, (low + high) / 2),
                        new Vector2(.045f, high - low), ArcadeGame.Cyan);
                for (float y = low + .13f; y < high; y += .25f)
                    SpriteObject(ladder, "Rung", square, new Vector2(game.ladderX[level], y), new Vector2(.38f, .035f), ArcadeGame.Cyan);
            }

            game.playerVisual = Child(root.transform, "Player 1");
            SpriteObject(game.playerVisual, "Suit", square, Vector2.zero, new Vector2(.32f, .42f), ArcadeGame.Cyan, 10);
            SpriteObject(game.playerVisual, "Helmet", circle, new Vector2(0, .29f), new Vector2(.3f, .3f), ArcadeGame.White, 11);
            SpriteObject(game.playerVisual, "Boots", square, new Vector2(0, -.27f), new Vector2(.35f, .09f), ArcadeGame.Yellow, 11);
            SpriteObject(game.playerVisual, "Eyes", square, new Vector2(0, .3f), new Vector2(.17f, .055f), Color.black, 12);
            SpriteObject(game.playerVisual, "Blaster", square, new Vector2(.21f, .22f), new Vector2(.08f, .24f), ArcadeGame.Cyan, 12);

            game.boss = SpriteObject(root.transform, "Boss (reused every wave)", bossSprite, Vector2.zero,
                new Vector2(1.15f, 1.15f), ArcadeGame.Yellow, 9);
            game.boss.sharedMaterial = material;
            var pool = Child(root.transform, "Projectile Pools (2 shots + 4 hazards)");
            game.shots = new ArcadeProjectile[2];
            game.hazards = new ArcadeProjectile[4];
            for (int i = 0; i < game.shots.Length; i++) game.shots[i] = new ArcadeProjectile
            { view = SpriteObject(pool, "Player shot " + i, square, Vector2.zero, new Vector2(.065f, .23f), ArcadeGame.Cyan, 15) };
            for (int i = 0; i < game.hazards.Length; i++)
            {
                var hazard = SpriteObject(pool, "Boss barrel " + i, circle, Vector2.zero, new Vector2(.28f, .28f), ArcadeGame.Yellow, 14);
                SpriteObject(hazard.transform, "Barrel stripe", square, Vector2.zero, new Vector2(.045f, .16f), Color.black, 15);
                game.hazards[i] = new ArcadeProjectile { view = hazard };
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
            var summary = new { scene = ScenePath, spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true).Length,
                activityCap = 8, cameraPPU = 32, resolution = "320x320", saved = !scene.isDirty };
            if (newlyOpened) EditorSceneManager.CloseScene(scene, true);
            return summary;
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

        private static void BuildInterface(Transform parent, BossClimbGame game)
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
            game.hud = Text(safe, "Health and kills", 16, 290, 288, 12, 9, ArcadeGame.Cyan);
            game.notice = Text(safe, "Feedback", 16, 280, 288, 10, 8, ArcadeGame.Yellow);
            game.notice.alignment = TextAlignmentOptions.Center;
            game.footer = Text(safe, "Controls", 12, 5, 296, 11, 8, ArcadeGame.White);
            game.footer.alignment = TextAlignmentOptions.Center;

            var panel = Image(safe, "Coin and result panel", 22, 52, 276, 222, ArcadeGame.Cyan).rectTransform;
            Image(panel, "Black inset", 2, 2, 272, 218, Color.black);
            game.overlay = panel.gameObject;
            game.overlayTitle = Text(panel, "Heading", 12, 154, 252, 58, 27, ArcadeGame.Yellow);
            game.overlayTitle.alignment = TextAlignmentOptions.Center;
            game.overlayTitle.fontStyle = FontStyles.Bold;
            game.overlayBody = Text(panel, "Instructions", 14, 59, 248, 90, 9, ArcadeGame.White);
            game.overlayBody.alignment = TextAlignmentOptions.Center;
            game.overlayHint = Text(panel, "Coin prompt", 10, 5, 256, 24, 8, ArcadeGame.White);
            game.overlayHint.alignment = TextAlignmentOptions.Center;
            game.onePlayerButton = Button(panel, "Insert Coin 1P", 34, 33, 208, 22, "1P  /  INSERT COIN");
            game.twoPlayerButton = Button(panel, "Unused 2P", 34, 33, 208, 22, "2P");
            game.twoPlayerButton.gameObject.SetActive(false);
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
