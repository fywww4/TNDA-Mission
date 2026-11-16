using TMPro;
using UnityEditor;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Targeted, repeatable additions to the existing Mode 7 cabinet. All coordinates are native HUD pixels.
    public static class Mode7StandingsLayout
    {
        public static void Apply(Mode7Stage stage)
        {
            var game = stage.game;
            var cabinet = (RectTransform)game.header.transform.parent;
            var hud = game.GetComponent<Mode7StandingsHud>();
            if (hud == null) hud = Undo.AddComponent<Mode7StandingsHud>(game.gameObject);
            Undo.RecordObject(game, "SELECT standings");
            Undo.RecordObject(hud, "SELECT standings");
            game.standingsHud = hud;
            hud.views = new Mode7StandingsHud.View[2];
            for (int seat = 0; seat < 2; seat++)
            {
                Color color = seat == 0 ? ArcadeGame.Cyan : ArcadeGame.Yellow;
                var root = Rect(cabinet, "P" + (seat + 1) + " standings", 0, 0, 512, 448);
                root.SetSiblingIndex(game.overlay.transform.GetSiblingIndex());
                var compact = Rect(root, "Place and score", 6, -70, 500, 54, top: true);
                var place = Label(compact, "Place", 0, 13, 200, 39, color);
                place.fontSize = ArcadeFontSetup.PixelSize * 3;
                var score = Label(compact, "Score", 0, 0, 240, 13, color);
                var hint = Label(compact, "SELECT hint", 250, 26, 250, 13, ArcadeGame.White);
                hint.text = seat == 0 ? CabinetText.P1StandingsHint : CabinetText.P2StandingsHint;
                var table = Rect(root, "Full standings", 4, -70, 504, 54, top: true);
                var backing = table.GetComponent<UnityEngine.UI.Image>();
                if (backing == null) backing = Undo.AddComponent<UnityEngine.UI.Image>(table.gameObject);
                Undo.RecordObject(backing, "SELECT standings");
                backing.color = Color.black;
                backing.raycastTarget = false;
                var rows = Label(table, "Rows", 2, 0, 500, 54, ArcadeGame.White);
                var time = Label(root, "Time", 6, -16, 240, 13, color, top: true);
                var elimination = Label(root, "Elimination", 246, -16, 260, 13, ArcadeGame.Yellow, top: true);
                elimination.alignment = TextAlignmentOptions.TopRight;
                hud.views[seat] = new Mode7StandingsHud.View {
                    root = root, compact = compact.gameObject, table = table.gameObject,
                    place = place, score = score, rows = rows, time = time, elimination = elimination
                };
                table.gameObject.SetActive(false);
                root.gameObject.SetActive(false);
                EditorUtility.SetDirty(place);
                EditorUtility.SetDirty(elimination);
                EditorUtility.SetDirty(backing);
            }
            // Keep the feedback below the table in each view, clear of the timer and of the full eight rows.
            Undo.RecordObject(stage, "SELECT standings");
            if (stage.noticeAt2P == new Vector2Int(6, 206)) stage.noticeAt2P = new Vector2Int(6, 136);
            EditorUtility.SetDirty(stage);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(game);

            TMP_Text Label(RectTransform parent, string name, int x, int y, int w, int h, Color color, bool top = false)
            {
                var rect = Rect(parent, name, x, y, w, h, top);
                var label = rect.GetComponent<TextMeshProUGUI>();
                if (label == null) label = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
                Undo.RecordObject(label, "SELECT standings");
                label.font = game.header.font;
                label.fontSharedMaterial = game.header.fontSharedMaterial;
                label.fontSize = ArcadeFontSetup.PixelSize;
                label.color = color;
                label.alignment = TextAlignmentOptions.TopLeft;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.raycastTarget = false;
                EditorUtility.SetDirty(label);
                return label;
            }
        }

        private static RectTransform Rect(RectTransform parent, string name, int x, int y, int w, int h, bool top = false)
        {
            var rect = (RectTransform)parent.Find(name);
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                Undo.RegisterCreatedObjectUndo(rect.gameObject, "SELECT standings");
                rect.SetParent(parent, false);
            }
            Undo.RecordObject(rect, "SELECT standings");
            rect.anchorMin = rect.anchorMax = top ? Vector2.up : Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            EditorUtility.SetDirty(rect);
            return rect;
        }
    }
}
