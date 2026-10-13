using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // Applies the CabinetText table to the GAME3 scene (title, instructions, coin buttons, attract screen).
    public static class Game3Text
    {
        // Applies the text to the saved GAME3 scene without rebuilding it.
        public static object PatchScene(string scenePath = "Assets/Scenes/GAME3.unity")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before patching GAME3 text.");
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool newlyOpened = !scene.isLoaded;
            if (newlyOpened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var game = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<DemolitionGame>(true)).First(g => g != null);
            Undo.RecordObject(game, "Chinese GAME3 text");
            game.cabinetTitle = CabinetText.Title;
            game.instructions = CabinetText.Instructions;
            SetCaption(game.onePlayerButton, CabinetText.OnePlayerButton);
            SetCaption(game.twoPlayerButton, CabinetText.TwoPlayerButton);
            UseScreenTags(game);
            ArcadeFontSetup.Apply(game);
            PixelLayout(game);
            CreateAttractScreen(game);
            game.PreparePreview();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var result = new { scene = scenePath, game.cabinetTitle, saved = !scene.isDirty };
            if (newlyOpened) EditorSceneManager.CloseScene(scene, true);
            return result;
        }

        // World-space car tags render through the 320px pixel camera (~7 real px, unreadable for Chinese).
        // Replace them with screen-space labels on the cabinet canvas that DemolitionGame moves each frame.
        public static void UseScreenTags(DemolitionGame game)
        {
            if (game.carLabels == null || game.carLabels.All(l => l is TextMeshProUGUI)) return;
            var cabinet = game.header.transform.parent;
            int below = game.overlay.transform.GetSiblingIndex(); // keep tags under the title/result panel
            Undo.RecordObject(game, "Screen-space car tags");
            for (int i = 0; i < game.carLabels.Length; i++)
            {
                var old = game.carLabels[i];
                if (old is TextMeshProUGUI) continue;
                var rect = new UnityEngine.GameObject("Car tag " + i, typeof(UnityEngine.RectTransform)).GetComponent<UnityEngine.RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Screen-space car tags");
                rect.SetParent(cabinet, false);
                rect.SetSiblingIndex(below++);
                rect.anchorMin = rect.anchorMax = rect.pivot = new UnityEngine.Vector2(.5f, .5f);
                rect.sizeDelta = new UnityEngine.Vector2(80, 12);
                var tag = rect.gameObject.AddComponent<TextMeshProUGUI>();
                tag.font = game.header.font;
                tag.fontSharedMaterial = game.header.fontSharedMaterial;
                tag.fontSize = 8;
                tag.alignment = TextAlignmentOptions.Center;
                tag.textWrappingMode = TextWrappingModes.NoWrap;
                tag.raycastTarget = false;
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                game.carLabels[i] = tag;
            }
            EditorUtility.SetDirty(game);
        }

        // Layout for the 11px pixel font on the true 320x320 screen (all values are whole pixels, origin bottom-left).
        public static void PixelLayout(DemolitionGame game)
        {
            var cabinet = game.header.transform.parent;
            void Place(UnityEngine.RectTransform rect, float x, float y, float w, float h)
            {
                Undo.RecordObject(rect, "GAME3 pixel layout");
                rect.anchoredPosition = new UnityEngine.Vector2(x, y);
                rect.sizeDelta = new UnityEngine.Vector2(w, h);
            }
            void Style(TMP_Text text, int size)
            {
                Undo.RecordObject(text, "GAME3 pixel layout");
                text.fontSize = size;
                text.lineSpacing = 0;
                text.overflowMode = TextOverflowModes.Overflow;
                EditorUtility.SetDirty(text);
            }
            const int px = ArcadeFontSetup.PixelSize;
            // Snap all UI vertices to screen pixels so centred text never lands on a half pixel.
            var canvas = cabinet.GetComponentInParent<UnityEngine.Canvas>();
            Undo.RecordObject(canvas, "GAME3 pixel layout");
            canvas.pixelPerfect = true;
            // Top bar: title + elimination timer, four-car leaderboard (left), player status (right).
            Place((UnityEngine.RectTransform)cabinet.Find("Header backing"), 0, 244, 320, 76);
            Place((UnityEngine.RectTransform)cabinet.Find("Header rule"), 4, 243, 312, 1);
            Place(game.header.rectTransform, 4, 306, 160, 13); Style(game.header, px);
            Place(game.arenaBoard.rectTransform, 156, 306, 160, 13); Style(game.arenaBoard, px);
            Place(game.leaderboard.rectTransform, 4, 245, 200, 60); Style(game.leaderboard, px);
            Place(game.hud.rectTransform, 180, 245, 136, 60); Style(game.hud, px);
            // Feedback line just inside the arena's top fence; one-line controls under the bottom fence.
            Place(game.notice.rectTransform, 4, 220, 312, 13); Style(game.notice, px);
            Place(game.footer.rectTransform, 4, 3, 312, 13); Style(game.footer, px);
            // Title / result panel.
            Place(game.overlayTitle.rectTransform, 10, 164, 256, 28); Style(game.overlayTitle, px * 2);
            Place(game.overlayBody.rectTransform, 4, 60, 268, 102); Style(game.overlayBody, px);
            Place(game.overlayHint.rectTransform, 6, 6, 264, 13); Style(game.overlayHint, px);
            foreach (var button in new[] { game.onePlayerButton, game.twoPlayerButton })
                Style(button.GetComponentInChildren<TMP_Text>(true), px);
            Style(game.countdownLabel, px * 4);
            foreach (var tag in game.carLabels) { Style(tag, px); Place(tag.rectTransform, 0, 0, 80, 13); }
        }

        // Text layer shown over the AI demo on the title screen (rebuilt each time so text changes apply).
        public static void CreateAttractScreen(DemolitionGame game)
        {
            var cabinet = game.header.transform.parent;
            var old = cabinet.Find("Attract screen");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new UnityEngine.GameObject("Attract screen", typeof(UnityEngine.RectTransform)).GetComponent<UnityEngine.RectTransform>();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "GAME3 attract screen");
            root.SetParent(cabinet, false);
            root.SetSiblingIndex(game.overlay.transform.GetSiblingIndex());
            root.anchorMin = root.anchorMax = root.pivot = UnityEngine.Vector2.zero;
            root.sizeDelta = new UnityEngine.Vector2(320, 320);
            const int px = ArcadeFontSetup.PixelSize;
            Backing(root, "Top backing", 0, 244, 320, 76);
            Label(root, "Title", CabinetText.Title, 0, 284, 320, 26, px * 2, ArcadeGame.Yellow);
            Label(root, "Coin rule", CabinetText.CoinRule, 0, 258, 320, 13, px, ArcadeGame.White);
            Backing(root, "Prompt backing", 70, 193, 180, 19);
            var prompt = Label(root, "Coin prompt", CabinetText.CoinPrompt, 0, 196, 320, 13, px, ArcadeGame.Yellow);
            Backing(root, "Controls backing", 0, 0, 320, 34);
            Label(root, "P1 controls", CabinetText.P1Controls, 0, 18, 320, 13, px, ArcadeGame.Cyan);
            Label(root, "P2 controls", CabinetText.P2Controls, 0, 3, 320, 13, px, ArcadeGame.Yellow);
            Undo.RecordObject(game, "GAME3 attract screen");
            game.attractScreen = root.gameObject;
            game.attractPrompt = prompt;
            EditorUtility.SetDirty(game);

            void Backing(UnityEngine.RectTransform parent, string name, float x, float y, float w, float h)
            {
                var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = UnityEngine.Color.black;
                image.raycastTarget = false;
            }
            TextMeshProUGUI Label(UnityEngine.RectTransform parent, string name, string text, float x, float y, float w, float h, int size, UnityEngine.Color color)
            {
                var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
                label.font = game.header.font;
                label.fontSharedMaterial = game.header.fontSharedMaterial;
                label.fontSize = size;
                label.color = color;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.text = text;
                return label;
            }
            UnityEngine.RectTransform Rect(UnityEngine.RectTransform parent, string name, float x, float y, float w, float h)
            {
                var rect = new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform)).GetComponent<UnityEngine.RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = UnityEngine.Vector2.zero;
                rect.anchoredPosition = new UnityEngine.Vector2(x, y);
                rect.sizeDelta = new UnityEngine.Vector2(w, h);
                return rect;
            }
        }

        private static void SetCaption(UnityEngine.UI.Button button, string caption)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            Undo.RecordObject(label, "Chinese GAME3 text");
            label.text = caption;
            EditorUtility.SetDirty(label);
        }
    }
}
