using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // GAME3_Mode7 HUD on the 512x448 cabinet (Docs/adr/0001). Runs the shared GAME3 text patch (which lays out the
    // 320x320 top-down cabinet), then moves everything to 512x448. Safe to run again.
    // The chase view's sky (above the horizon, about the top third of each view) holds the HUD, so the road ahead
    // stays clear: in 2P the scores sit in P1's sky and the feedback line in P2's (Mode7Stage moves that line).
    public static class Mode7Layout
    {
        public const string ScenePath = "Assets/Scenes/GAME3_Mode7.unity";
        private const int W = HybridResolution.CabinetWidth, H = HybridResolution.CabinetHeight;

        public static object PatchScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before patching the Mode 7 HUD.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool newlyOpened = !scene.isLoaded;
            if (newlyOpened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Game3Text.PatchScene(ScenePath);
            var stage = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Mode7Stage>(true)).First(s => s != null);
            // The minimap rect is now in 256x224 art pixels (scenes saved before were in 320px cabinet pixels).
            Undo.RecordObject(stage, "Mode 7 HUD");
            stage.minimapRect = Mode7Stage.DefaultMinimapRect;
            EditorUtility.SetDirty(stage);
            Apply(stage);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var result = new { scene = ScenePath, saved = !scene.isDirty };
            if (newlyOpened) EditorSceneManager.CloseScene(scene, true);
            return result;
        }

        public static void Apply(Mode7Stage stage)
        {
            var game = stage.game;
            const int px = ArcadeFontSetup.PixelSize, line = 13;
            var cabinet = (RectTransform)game.header.transform.parent;
            Undo.RecordObject(cabinet.gameObject, "Mode 7 HUD");
            cabinet.gameObject.name = "512x448 Cabinet";
            // The cabinet stays centred on the canvas; Mode7Stage keeps the canvas at a whole-number scale.
            Undo.RecordObject(cabinet, "Mode 7 HUD");
            cabinet.sizeDelta = new Vector2(W, H);
            // Sky behind the HUD text instead of a black bar.
            foreach (var name in new[] { "Header backing", "Header rule" })
            {
                var backing = cabinet.Find(name);
                if (backing != null) { Undo.RecordObject(backing.gameObject, "Mode 7 HUD"); backing.gameObject.SetActive(false); }
            }
            // Top: title and elimination timer, then four-car scores (left) and player status (right).
            int top = H - 3 - line;                                    // 432
            Place(game.header.rectTransform, 6, top, 240, line);
            Place(game.arenaBoard.rectTransform, W - 6 - 260, top, 260, line);
            Place(game.leaderboard.rectTransform, 6, top - 2 - line * 4, 300, line * 4);   // 378..430
            Place(game.hud.rectTransform, W - 6 - 190, top - 2 - line * 4, 190, line * 4);
            // Feedback line (Mode7Stage moves it between 1P and 2P) and one-line controls at the bottom edge.
            Place(game.notice.rectTransform, stage.noticeAt1P.x, stage.noticeAt1P.y, W - 12, line);
            Place(game.footer.rectTransform, 6, 3, W - 12, line);
            // Middle of the cabinet: countdown, coin/result panel, continue panel.
            Centre(game.countdownLabel.rectTransform);
            Centre((RectTransform)game.overlay.transform);
            Centre((RectTransform)game.continuePanel.transform);
            LayOutAttract((RectTransform)game.attractScreen.transform);
            LayOutVictory((RectTransform)game.victoryScreen.transform);
            EditorUtility.SetDirty(game);

            void Centre(RectTransform rect) => Place(rect, (W - (int)rect.sizeDelta.x) / 2, (H - (int)rect.sizeDelta.y) / 2, rect.sizeDelta.x, rect.sizeDelta.y);

            void LayOutAttract(RectTransform root)
            {
                Place(root, 0, 0, W, H);
                Place(Child(root, "Top backing"), 0, H - 108, W, 108);
                Place(Child(root, "Mode tabs"), 0, H - 98, W, line);
                var list = Child(root, "Challenge list");
                Place(list, 0, H - 288, W, 112);
                Place(Child(list, "Backing"), 0, 0, W, 112);
                Place(Child(list, "Entries"), 0, 3, W, 106);
                Place(Child(root, "Title"), 0, H - 36, W, 26);
                Place(Child(root, "Coin rule"), 0, H - 62, W, line);
                Place(Child(root, "Prompt backing"), (W - 220) / 2, H - 131, 220, 19);
                Place(Child(root, "Coin prompt"), 0, H - 128, W, line);
                Place(Child(root, "Gold backing"), (W - 320) / 2, H - 168, 320, 32);
                Place(Child(root, "Gold total"), 0, H - 150, W, line);
                Place(Child(root, "Reset save hint"), 0, H - 165, W, line);
                Place(Child(root, "Controls backing"), 0, 0, W, 34);
                Place(Child(root, "P1 controls"), 0, 18, W, line);
                Place(Child(root, "P2 controls"), 0, 3, W, line);
            }

            void LayOutVictory(RectTransform root)
            {
                Place(root, 0, 0, W, H);
                // The 128px picture at exactly 2x in the middle, the score under it, 你活下來了 top-left.
                Place(Child(root, "Picture"), (W - 256) / 2, (H - 256) / 2, 256, 256);
                Place(game.victoryScore.rectTransform, 0, (H - 256) / 2 - 22, W, line);
                Place(game.victoryBanner.rectTransform, 24, H - 18 - 26, 200, 26);
                Undo.RecordObject(game.victoryBanner, "Mode 7 HUD");
                game.victoryBanner.fontSize = px * 2;
            }
        }

        private static RectTransform Child(RectTransform root, string name) =>
            (RectTransform)root.Find(name) ?? throw new InvalidOperationException(root.name + " has no " + name);

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            Undo.RecordObject(rect, "Mode 7 HUD");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            EditorUtility.SetDirty(rect);
        }
    }
}
