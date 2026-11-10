using TMPro;
using UnityEditor;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Targeted additions to the existing cabinet: title option and one gear readout per player view.
    public static class Game3Transmission
    {
        public static void Apply(DemolitionGame game, int width, int height)
        {
            var cabinet = (RectTransform)game.header.transform.parent;
            var title = (RectTransform)game.attractScreen.transform;
            Undo.RecordObject(game, "Transmission HUD");
            game.attractTransmission = Label(title, "Transmission option", 0, height - 79, width, 13, ArcadeGame.Yellow);
            game.attractControls = new[] {
                title.Find("P1 controls").GetComponent<TMP_Text>(),
                title.Find("P2 controls").GetComponent<TMP_Text>()
            };
            game.gearLabels = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                var box = Rect(cabinet, "P" + (i + 1) + " gear", width - 96, 20 + (i == 0 ? height / 2 : 0), 90, 17);
                var backing = box.GetComponent<UnityEngine.UI.Image>();
                if (backing == null) backing = Undo.AddComponent<UnityEngine.UI.Image>(box.gameObject);
                backing.color = Color.black;
                backing.raycastTarget = false;
                game.gearLabels[i] = Label(box, "Text", 0, 2, 90, 13, i == 0 ? ArcadeGame.Cyan : ArcadeGame.Yellow);
                // Behind every title/countdown/continue/result/victory panel, above the racing picture.
                box.SetSiblingIndex(game.overlay.transform.GetSiblingIndex());
                box.gameObject.SetActive(false);
                EditorUtility.SetDirty(backing);
            }
            EditorUtility.SetDirty(game);

            TMP_Text Label(RectTransform parent, string name, int x, int y, int w, int h, Color color)
            {
                var rect = Rect(parent, name, x, y, w, h);
                var label = rect.GetComponent<TextMeshProUGUI>();
                if (label == null) label = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
                Undo.RecordObject(label, "Transmission HUD");
                label.font = game.header.font;
                label.fontSharedMaterial = game.header.fontSharedMaterial;
                label.fontSize = ArcadeFontSetup.PixelSize;
                label.color = color;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.raycastTarget = false;
                EditorUtility.SetDirty(label);
                return label;
            }
        }

        private static RectTransform Rect(RectTransform parent, string name, int x, int y, int w, int h)
        {
            var rect = (RectTransform)parent.Find(name);
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "Transmission HUD");
                rect.SetParent(parent, false);
            }
            Undo.RecordObject(rect, "Transmission HUD");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            EditorUtility.SetDirty(rect);
            return rect;
        }
    }
}
