using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    public static class Game3Garage
    {
        [MenuItem("TNDA/Mode 7/Apply garage")]
        public static void PatchScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before patching the garage.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Mode7Layout.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Mode7Layout.ScenePath, OpenSceneMode.Additive);
            var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Mode7Stage>(true)).Single();
            ArcadeFontSetup.GetOrCreate(stage.game);
            Apply(stage);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        // Reuse named objects on a second patch, without rebuilding the cabinet or changing its tuning.
        public static void Apply(Mode7Stage stage)
        {
            var game = stage.game;
            var cabinet = (RectTransform)game.header.transform.parent;
            var root = Rect(cabinet, "Garage screen", 0, 0, 512, 448);
            Undo.RecordObject(game, "Garage HUD");
            var view = root.GetComponent<GarageScreen>();
            if (view == null) view = Undo.AddComponent<GarageScreen>(root.gameObject);
            Undo.RecordObject(view, "Garage HUD");
            Image(root, "Background", 0, 0, 512, 448, new Color32(12, 16, 32, 255));
            Image(root, "Top backing", 0, 390, 512, 58, new Color32(30, 44, 77, 255));
            Image(root, "Header rule", 0, 388, 512, 2, ArcadeGame.Yellow);
            Label(root, "Title", CabinetText.GarageTitle, 18, 407, 100, 26, ArcadeGame.Yellow, 22);
            view.gold = Label(root, "Gold", "", 206, 412, 288, 13, ArcadeGame.Yellow);
            view.gold.alignment = TextAlignmentOptions.Right;
            Label(root, "Ability tab", CabinetText.GarageAbilityTab, 18, 362, 264, 13, ArcadeGame.Yellow);
            Label(root, "Shared", CabinetText.GarageShared, 292, 362, 202, 13, ArcadeGame.Cyan).alignment = TextAlignmentOptions.Center;
            view.rows = new UnityEngine.UI.Image[4];
            view.bars = new UnityEngine.UI.Image[16];
            view.names = new TMP_Text[4]; view.bonuses = new TMP_Text[4]; view.prices = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                var row = Rect(root, "Ability " + i, 18, 284 - i * 58, 264, 54);
                view.rows[i] = Image(row, "Border", 0, 0, 264, 54, ArcadeGame.White);
                Image(row, "Inset", 2, 2, 260, 50, new Color32(18, 25, 44, 255));
                view.names[i] = Label(row, "Name", "", 10, 33, 150, 13, ArcadeGame.White);
                view.bonuses[i] = Label(row, "Bonus", "", 190, 33, 64, 13, ArcadeGame.Cyan);
                view.bonuses[i].alignment = TextAlignmentOptions.Right;
                for (int j = 0; j < 4; j++) view.bars[i * 4 + j] = Image(row, "Level " + j, 10 + j * 36, 12, 32, 10, ArcadeGame.Cyan);
                view.prices[i] = Label(row, "Price", "", 158, 10, 96, 13, ArcadeGame.Yellow);
                view.prices[i].alignment = TextAlignmentOptions.Right;
            }
            // Two-pixel steps and a solid shadow form a small SFC-style turntable. All art stays on the coarse grid.
            var platform = Rect(root, "Display platform", 292, 126, 202, 212);
            Image(platform, "Frame", 0, 0, 202, 212, new Color32(53, 73, 114, 255));
            Image(platform, "Inset", 2, 2, 198, 208, new Color32(18, 25, 44, 255));
            Image(platform, "Pedestal shadow", 20, 22, 164, 20, Color.black);
            Image(platform, "Pedestal base", 12, 32, 178, 18, new Color32(53, 73, 114, 255));
            Image(platform, "Pedestal top", 20, 50, 162, 4, ArcadeGame.Cyan);
            view.car = Image(platform, "Rotating car", 4, 26, 192, 192, ArcadeGame.Cyan);
            view.car.material = stage.carSheetMaterial;
            view.carFrames = stage.carFrames;
            if (view.carFrames != null && view.carFrames.Length == 16) view.car.sprite = view.carFrames[0];
            view.car.preserveAspect = true;
            Label(root, "Step rule", CabinetText.GarageStep, 292, 102, 202, 13, ArcadeGame.White).alignment = TextAlignmentOptions.Center;
            Label(root, "Strength rule", CabinetText.GarageStrengthStep, 18, 82, 476, 13, ArcadeGame.White);
            view.message = Label(root, "Message", "", 18, 57, 476, 13, ArcadeGame.Yellow);
            Label(root, "Controls", CabinetText.GarageControls, 18, 18, 476, 13, ArcadeGame.White).alignment = TextAlignmentOptions.Center;
            var entry = Label((RectTransform)game.attractScreen.transform, "Garage option", CabinetText.GarageEntry,
                0, 46, 512, 13, ArcadeGame.Yellow);
            entry.alignment = TextAlignmentOptions.Center;
            game.garage = view;
            root.SetAsLastSibling();
            root.gameObject.SetActive(false);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(game);

            TMP_Text Label(RectTransform parent, string name, string text, int x, int y, int w, int h, Color color, int size = 11)
            {
                var rect = Rect(parent, name, x, y, w, h);
                var label = rect.GetComponent<TextMeshProUGUI>();
                if (label == null) label = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
                Undo.RecordObject(label, "Garage HUD");
                label.font = game.header.font;
                label.fontSharedMaterial = game.header.fontSharedMaterial;
                label.fontSize = size;
                label.color = color;
                label.alignment = TextAlignmentOptions.Left;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.enableAutoSizing = false;
                label.raycastTarget = false;
                label.text = text;
                EditorUtility.SetDirty(label);
                return label;
            }
        }

        private static UnityEngine.UI.Image Image(RectTransform parent, string name, int x, int y, int w, int h, Color color)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = Undo.AddComponent<UnityEngine.UI.Image>(rect.gameObject);
            Undo.RecordObject(image, "Garage HUD");
            image.color = color;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
            return image;
        }

        private static RectTransform Rect(RectTransform parent, string name, int x, int y, int w, int h)
        {
            var rect = (RectTransform)parent.Find(name);
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Garage HUD");
                rect.SetParent(parent, false);
            }
            Undo.RecordObject(rect, "Garage HUD");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            EditorUtility.SetDirty(rect);
            return rect;
        }
    }
}
