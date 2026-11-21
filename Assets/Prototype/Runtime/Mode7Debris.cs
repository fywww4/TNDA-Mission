using UnityEngine;
using UnityEngine.UI;

namespace TNDA.Prototype
{
    // Two things from the one Debris.png sheet (2026-11-21, replacing the on-screen debris of ticket 04):
    // - 油: when a player's car spins out on an oil slick, one big splat of oil smears that player's own chase view,
    //   then slides down and is gone within splashSeconds. In a 2P split it stays inside that player's half; a new
    //   smear replaces the one still there. Never in the attract demo.
    // - 零件: the scrap parts DemolitionMatch scatters on the floor, one view per slot (game.partViews). Mode7Stage
    //   stands them up in the chase views; body panels take the victim's seat colour through the car sheet palette.
    // Runs before Mode7Stage so the parts are in place when it collects the standing sprites.
    [DefaultExecutionOrder(90)]
    public sealed class Mode7Debris : MonoBehaviour
    {
        public Mode7Stage stage;
        [Tooltip("Four 64x64 cells in a row: body panel (its 4 key reds become the seat colour), glass, oil, parts.")]
        public Texture2D sheet;
        public float splashSeconds = 3;
        [Tooltip("Optional dedicated big oil splat (OilSmear.png). Empty = the sheet's oil cell, enlarged.")]
        public Texture2D oilSmear;
        [Tooltip("The splat's height as a share of the player's view (rounded to whole art-pixel steps).")]
        [Range(.3f, 1f)] public float smearHeight = .7f;
        [Tooltip("The splat sticks this long before it starts to slide, and blinks out over the last fadeSeconds.")]
        public float holdSeconds = .6f, fadeSeconds = .5f;
        [Header("Scrap parts on the floor")]
        [Tooltip("How wide a part stands in world units (a car is about 0.9).")]
        public float partSize = .42f;
        [Tooltip("A part nobody takes blinks for this long before it is gone.")]
        public float partBlinkSeconds = 1.5f;

        private const int Cells = 4, CellPixels = 64, OilCell = 2;
        // How far (in view heights) the oil drops by the end.
        private const float DropViews = .9f;

        private sealed class View
        {
            public RectTransform layer;
            public RawImage smear;
            public Vector2 start;
            public float age = -1;
        }

        private readonly View[] views = new View[2];
        private readonly Sprite[] partSprites = new Sprite[Cells];
        private SpriteRenderer[] parts = new SpriteRenderer[0];
        private DemolitionGame game;

        private void OnEnable()
        {
            if (stage == null || stage.game == null || sheet == null) return;
            game = stage.game;
            var cabinet = (RectTransform)game.header.transform.parent;
            for (int i = 0; i < views.Length; i++) views[i] = MakeView(cabinet, i);
            MakeParts();
            game.OilPresented += Splash;
        }

        private void OnDisable()
        {
            if (game != null) game.OilPresented -= Splash;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i]?.layer != null) Destroy(views[i].layer.gameObject);
                views[i] = null;
            }
            foreach (var part in parts) if (part != null) Destroy(part.gameObject);
            if (game != null) game.partViews = new SpriteRenderer[0];
            parts = new SpriteRenderer[0];
            for (int i = 0; i < partSprites.Length; i++) { if (partSprites[i] != null) Destroy(partSprites[i]); partSprites[i] = null; }
            game = null;
        }

        // One clipped layer per chase view, just above the art image and under the HUD text.
        private View MakeView(RectTransform cabinet, int seat)
        {
            var go = new GameObject("Mode7 debris P" + (seat + 1), typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var layer = (RectTransform)go.transform;
            layer.SetParent(cabinet, false);
            // Index 0 is Mode7Stage's art image (it puts itself first); the HUD text follows.
            layer.SetSiblingIndex(1);
            layer.anchorMin = layer.anchorMax = layer.pivot = Vector2.zero;
            go.AddComponent<RectMask2D>();
            var smear = new GameObject("Oil smear", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var rect = (RectTransform)smear.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            var image = smear.AddComponent<RawImage>();
            image.raycastTarget = false;
            var view = new View { layer = layer, smear = image };
            go.SetActive(false);
            return view;
        }

        private bool TwoViews => game.PlayerCount == 2 && game.Phase != ArcadePhase.Attract;

        // The view's rectangle in cabinet pixels (bottom-left origin).
        private Rect ViewPixels(int seat)
        {
            var r = HybridResolution.ViewRect(seat, TwoViews);
            return new Rect(r.x * HybridResolution.CabinetWidth, r.y * HybridResolution.CabinetHeight,
                r.width * HybridResolution.CabinetWidth, r.height * HybridResolution.CabinetHeight);
        }

        private void Splash(ArcadeVehicle car)
        {
            int seat = car.index;
            if (seat >= views.Length || views[seat] == null || (seat == 1 && !TwoViews)) return;
            var view = views[seat];
            var area = ViewPixels(seat);
            var image = view.smear;
            Vector2 texels;
            if (oilSmear != null) { image.texture = oilSmear; image.uvRect = new Rect(0, 0, 1, 1); texels = new Vector2(oilSmear.width, oilSmear.height); }
            else { image.texture = sheet; image.uvRect = new Rect((float)OilCell / Cells, 0, 1f / Cells, 1); texels = Vector2.one * CellPixels; }
            // Whole-number enlargement of the art pixels, so the splat stays on the scene's pixel grid.
            const float k = HybridResolution.ArtScale;
            float scale = Mathf.Max(1, Mathf.Round(area.height * smearHeight / (texels.y * k))) * k;
            var rect = image.rectTransform;
            rect.sizeDelta = texels * scale;
            // Mirroring keeps the pixel grid and varies the look.
            rect.localScale = new Vector3(Random.value < .5f ? -1 : 1, 1, 1);
            view.start = new Vector2(Random.Range(.4f, .6f) * area.width, Random.Range(.45f, .6f) * area.height);
            view.age = 0;
            Place(view, seat);
        }

        private void LateUpdate()
        {
            if (game == null) return;
            PlaceParts();
            // Every new match starts with a countdown; it and the title demo begin with a clean screen.
            if (game.Phase == ArcadePhase.Countdown || game.Phase == ArcadePhase.Attract) foreach (var v in views) Clear(v);
            for (int seat = 0; seat < views.Length; seat++)
            {
                var view = views[seat];
                if (view == null || view.age < 0) continue;
                if (seat == 1 && !TwoViews) { Clear(view); continue; }
                view.age += Time.deltaTime;
                if (view.age >= splashSeconds) { Clear(view); continue; }
                Place(view, seat);
            }
        }

        private void Place(View view, int seat)
        {
            var area = ViewPixels(seat);
            view.layer.anchoredPosition = area.position;
            view.layer.sizeDelta = area.size;
            view.layer.gameObject.SetActive(true);
            // Stuck for a moment, then sliding faster and faster until it has dropped about a view height.
            float slide = Mathf.Max(0, view.age - holdSeconds), slideTime = Mathf.Max(.01f, splashSeconds - holdSeconds);
            float drop = DropViews * (area.height + view.smear.rectTransform.sizeDelta.y / 2) * slide * slide / (slideTime * slideTime);
            // Four alpha steps rather than a smooth fade, in keeping with the coarse pixels.
            float alpha = Mathf.Ceil(Mathf.Clamp01((splashSeconds - view.age) / fadeSeconds) * 4) / 4;
            const float k = HybridResolution.ArtScale;
            var at = view.start - new Vector2(0, drop);
            view.smear.rectTransform.anchoredPosition = new Vector2(Mathf.Round(at.x / k) * k, Mathf.Round(at.y / k) * k);
            view.smear.color = new Color(1, 1, 1, alpha);
        }

        private static void Clear(View view)
        {
            if (view == null || view.age < 0) return;
            view.age = -1;
            view.layer.gameObject.SetActive(false);
        }

        // One sprite per cell (pivot on its bottom edge, so a part stands on the floor) and one view per part slot, on
        // the explosions' parent, sorting and chase-only layer like the spike balls.
        private void MakeParts()
        {
            float ppu = CellPixels / Mathf.Max(.05f, partSize);
            for (int c = 0; c < Cells; c++)
            {
                partSprites[c] = Sprite.Create(sheet, new Rect(c * CellPixels, 0, CellPixels, CellPixels), new Vector2(.5f, 0), ppu);
                partSprites[c].hideFlags = HideFlags.DontSave;
            }
            var template = game.explosions != null && game.explosions.Length > 0 ? game.explosions[0] : null;
            parts = new SpriteRenderer[DemolitionMatch.MaxParts];
            for (int i = 0; i < parts.Length; i++)
            {
                var view = new GameObject("Scrap part " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                view.gameObject.hideFlags = HideFlags.DontSave;
                if (template != null)
                {
                    view.transform.SetParent(template.transform.parent, false);
                    view.sortingLayerID = template.sortingLayerID;
                    view.sortingOrder = template.sortingOrder;
                }
                view.gameObject.layer = stage.chaseOnlyLayer;
                // The car sheet palette swaps the body panel's key reds for the victim's seat colour; the other cells
                // have none of those reds, so they keep their own colours.
                if (stage.carSheetMaterial != null) view.sharedMaterial = stage.carSheetMaterial;
                view.gameObject.SetActive(false);
                parts[i] = view;
            }
            game.partViews = parts;
        }

        private void PlaceParts()
        {
            var state = game.Parts;
            for (int i = 0; i < parts.Length && i < state.Count; i++)
            {
                var part = state[i];
                var view = parts[i];
                if (view == null) continue;
                bool on = part.active && game.Phase != ArcadePhase.Attract;
                if (view.gameObject.activeSelf != on) view.gameObject.SetActive(on);
                if (!on) continue;
                view.sprite = partSprites[Mathf.Clamp(part.kind, 0, Cells - 1)];
                view.color = part.Source != null && part.Source.body != null ? part.Source.body.color : Color.white;
                var at = ArcadeGame.PixelPosition(part.position);
                view.transform.position = new Vector3(at.x, at.y, view.transform.position.z);
                // About to be gone: blink (no alpha, like the explosions).
                float left = game.PartLifeSeconds - part.age;
                view.enabled = part.Magnet != null || left > partBlinkSeconds || Mathf.FloorToInt(left * 10) % 2 == 0;
            }
        }
    }
}
