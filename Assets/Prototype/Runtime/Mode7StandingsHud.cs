using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace TNDA.Prototype
{
    // Presentation only. Each seat sees its own place/score, or the full table while holding SELECT.
    public sealed class Mode7StandingsHud : MonoBehaviour
    {
        [Serializable]
        public sealed class View
        {
            public RectTransform root;
            public GameObject compact, table;
            public TMP_Text place, score, rows, time, elimination;
        }

        [Tooltip("Show both players' tables automatically just before the next timed elimination.")]
        public bool autoShowBeforeElimination = true;
        [Min(0)] public float autoShowSeconds = 5;
        public View[] views = new View[2];
        private readonly bool[] held = new bool[2];

        public void ReadInput(ArcadeInput p1, ArcadeInput p2)
        {
            held[0] = p1.selectHeld;
            held[1] = p2.selectHeld;
        }

        public void Render(DemolitionGame game, ArcadeVehicle[] order, bool unobstructed)
        {
            // The original shared HUD is retained for the reference scenes and scene tooling.
            foreach (var label in new[] { game.header, game.arenaBoard, game.hud, game.leaderboard })
                if (label != null) label.gameObject.SetActive(false);
            bool racing = game.Phase == ArcadePhase.Playing;
            bool visible = unobstructed && (racing || game.Phase == ArcadePhase.Countdown);
            float untilElimination = game.NextElimination - game.Elapsed;
            bool automatic = racing && autoShowBeforeElimination && autoShowSeconds > 0
                && untilElimination > 0 && untilElimination <= autoShowSeconds;
            int remaining = order.Count(c => !c.eliminated);
            var lines = order.Select((car, rank) =>
            {
                string row = string.Format(CabinetText.RaceStanding, car.eliminated ? car.FinalPlace : rank + 1,
                    car.DriverName, Mathf.RoundToInt(car.score), Mathf.CeilToInt(car.health * 100));
                if (car.eliminated) row = string.Format(CabinetText.RaceStandingEliminated, row);
                Color color = car.eliminated ? new Color32(160, 160, 160, 255) : car.body.color;
                return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + row + "</color>";
            }).ToArray();
            int rows = (lines.Length + 1) / 2;
            string table = string.Join("\n", Enumerable.Range(0, rows).Select(i => lines[i]
                + (i + rows < lines.Length ? "<pos=250>" + lines[i + rows] : "")));
            for (int seat = 0; seat < views.Length; seat++)
            {
                var view = views[seat];
                if (view == null || view.root == null) continue;
                bool on = visible && seat < game.PlayerCount;
                view.root.gameObject.SetActive(on);
                if (!on) continue;
                bool split = game.PlayerCount == 2;
                float height = split ? HybridResolution.CabinetHeight / 2 : HybridResolution.CabinetHeight;
                view.root.anchoredPosition = new Vector2(0, split && seat == 0 ? height : 0);
                view.root.sizeDelta = new Vector2(HybridResolution.CabinetWidth, height);
                bool expanded = racing && (held[seat] || automatic);
                view.table.SetActive(expanded);
                view.compact.SetActive(!expanded);
                view.rows.text = table;
                var car = game.cars[seat];
                view.place.text = car.eliminated ? string.Format(CabinetText.RaceEliminatedPlace, car.FinalPlace)
                    : string.Format(CabinetText.RacePlace, Array.IndexOf(order, car) + 1, remaining);
                view.score.text = string.Format(CabinetText.RaceScore, seat + 1, Mathf.RoundToInt(car.score));
                view.time.text = string.Format(CabinetText.RaceTime, seat + 1, Mathf.CeilToInt(game.Remaining), game.Wrecks);
                view.elimination.text = game.arenaBoard.text;
            }
        }
    }
}
