using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TNDA.Prototype
{
    // 碎片噴濺: when a player wrecks a car, or rams one at prop-smashing speed (DebrisSplashed), pieces of that car splat
    // onto the player's own chase view, about a third of it, then slide down and are gone within splashSeconds. In a
    // 2P split they stay inside the rammer's half. Body-panel pieces take the rammed car's seat colour. A new splash on
    // a view replaces the one still there. Presentation only; DemolitionGame never reports one in the attract demo.
    [DefaultExecutionOrder(110)]
    public sealed class Mode7Debris : MonoBehaviour
    {
        public Mode7Stage stage;
        [Tooltip("Four 64x64 cells in a row: body panel (its 4 key reds become the seat colour), glass, oil, parts. Read/Write enabled.")]
        public Texture2D sheet;
        public float splashSeconds = 3;
        [Tooltip("Share of the rammer's view the pieces themselves cover; sets how many pieces fly.")]
        [Range(.1f, .6f)] public float coverage = .25f;
        [Tooltip("Share of the view the pieces are scattered across (wider = sparser, less clumped).")]
        [Range(.3f, .9f)] public float scatter = .75f;
        [Tooltip("Pieces keep at least this many piece widths apart from each other where the view has room.")]
        [Range(0, 1.2f)] public float minGap = .85f;
        [Tooltip("The pieces stick this long before they start to slide, and blink out over the last fadeSeconds.")]
        public float holdSeconds = .35f, fadeSeconds = .5f;

        private const int Cells = 4, CellPixels = 64, BodyCell = 0, MaxPieces = 8;
        private const float CellSize = CellPixels * HybridResolution.ArtScale;
        // The scatter area is an ellipse this much wider than tall; tries per piece to find a spot clear of the others.
        private const float SplashAspect = 1.4f;
        private const int SpotTries = 24;
        // Share of pieces after the first that are body panels; how far (in view heights) the pieces drop by the end.
        private const float BodyShare = .4f, DropViews = .9f;

        private sealed class Piece { public RawImage image; public Vector2 start; public float fall; }

        private sealed class View
        {
            public RectTransform layer;
            public readonly Piece[] pieces = new Piece[MaxPieces];
            public int count;
            public float age = -1;
        }

        private readonly View[] views = new View[2];
        private readonly Dictionary<Color32, Texture2D> bodies = new Dictionary<Color32, Texture2D>();
        private DemolitionGame game;

        private void OnEnable()
        {
            if (stage == null || stage.game == null || sheet == null) return;
            game = stage.game;
            var cabinet = (RectTransform)game.header.transform.parent;
            for (int i = 0; i < views.Length; i++) views[i] = MakeView(cabinet, i);
            game.DebrisPresented += Splash;
        }

        private void OnDisable()
        {
            if (game != null) game.DebrisPresented -= Splash;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i]?.layer != null) Destroy(views[i].layer.gameObject);
                views[i] = null;
            }
            foreach (var texture in bodies.Values) Destroy(texture);
            bodies.Clear();
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
            var view = new View { layer = layer };
            for (int i = 0; i < MaxPieces; i++)
            {
                var piece = new GameObject("Piece", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
                var rect = (RectTransform)piece.transform;
                rect.SetParent(layer, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = Vector2.one * CellSize;
                var image = piece.AddComponent<RawImage>();
                image.raycastTarget = false;
                piece.SetActive(false);
                view.pieces[i] = new Piece { image = image };
            }
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

        private void Splash(DebrisSplashed debris)
        {
            int seat = debris.Rammer.index;
            if (seat >= views.Length || views[seat] == null || (seat == 1 && !TwoViews)) return;
            var view = views[seat];
            var area = ViewPixels(seat);
            // Enough pieces to cover `coverage` of the view, scattered across a wider ellipse (`scatter` of the view)
            // and kept apart, so the splash reads as separate shards rather than one clump.
            float viewArea = area.width * area.height;
            view.count = Mathf.Clamp(Mathf.RoundToInt(viewArea * coverage / (CellSize * CellSize)), 2, MaxPieces);
            float ry = Mathf.Sqrt(viewArea * scatter / (Mathf.PI * SplashAspect)), rx = ry * SplashAspect;
            // Keep a piece's centre a third of a piece inside the view so most of it shows.
            float inset = CellSize / 3;
            rx = Mathf.Min(rx, area.width / 2 - inset);
            ry = Mathf.Min(ry, area.height / 2 - inset);
            var centre = new Vector2(Random.Range(.45f, .55f) * area.width, Random.Range(.4f, .5f) * area.height);
            Texture2D body = BodyTexture(debris.Victim.body != null ? debris.Victim.body.color : Color.white);
            for (int i = 0; i < MaxPieces; i++)
            {
                var piece = view.pieces[i];
                bool on = i < view.count;
                piece.image.gameObject.SetActive(on);
                if (!on) continue;
                // At least one body panel, so the splash always reads as that car.
                int cell = i == 0 || Random.value < BodyShare ? BodyCell : Random.Range(1, Cells);
                piece.image.texture = cell == BodyCell ? body : sheet;
                piece.image.uvRect = new Rect((float)cell / Cells, 0, 1f / Cells, 1);
                // Quarter turns and mirroring keep the pixel grid.
                var rect = piece.image.rectTransform;
                rect.localRotation = Quaternion.Euler(0, 0, 90 * Random.Range(0, 4));
                rect.localScale = new Vector3(Random.value < .5f ? -1 : 1, 1, 1);
                piece.start = FreeSpot(view, i, centre, rx, ry);
                piece.fall = Random.Range(.8f, 1.2f);
            }
            view.age = 0;
            Place(view, seat);
        }

        // A random spot in the scatter ellipse at least minGap piece widths from the pieces already placed; when the
        // view is too crowded for that, the farthest of the tries.
        private Vector2 FreeSpot(View view, int placed, Vector2 centre, float rx, float ry)
        {
            float gap = minGap * CellSize;
            Vector2 best = centre;
            float bestClear = -1;
            for (int t = 0; t < SpotTries; t++)
            {
                var offset = Random.insideUnitCircle;
                var spot = centre + new Vector2(offset.x * rx, offset.y * ry);
                float clear = float.MaxValue;
                for (int j = 0; j < placed; j++) clear = Mathf.Min(clear, Vector2.Distance(spot, view.pieces[j].start));
                if (clear >= gap) return spot;
                if (clear > bestClear) { bestClear = clear; best = spot; }
            }
            return best;
        }

        private void LateUpdate()
        {
            if (game == null) return;
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
            // Stuck for a moment, then sliding faster and faster until the pieces have dropped about a view height.
            float slide = Mathf.Max(0, view.age - holdSeconds), slideTime = Mathf.Max(.01f, splashSeconds - holdSeconds);
            float drop = DropViews * (area.height + CellSize) * slide * slide / (slideTime * slideTime);
            // Four alpha steps rather than a smooth fade, in keeping with the coarse pixels.
            float alpha = Mathf.Ceil(Mathf.Clamp01((splashSeconds - view.age) / fadeSeconds) * 4) / 4;
            const float k = HybridResolution.ArtScale;
            for (int i = 0; i < view.count; i++)
            {
                var piece = view.pieces[i];
                var at = piece.start - new Vector2(0, drop * piece.fall);
                // Whole art pixels keep the pieces on the same grid as the scene.
                piece.image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(at.x / k) * k, Mathf.Round(at.y / k) * k);
                piece.image.color = new Color(1, 1, 1, alpha);
            }
        }

        private static void Clear(View view)
        {
            if (view == null || view.age < 0) return;
            view.age = -1;
            view.layer.gameObject.SetActive(false);
        }

        private Texture2D BodyTexture(Color seat)
        {
            var key = (Color32)seat;
            if (bodies.TryGetValue(key, out var texture)) return texture;
            if (!sheet.isReadable) return sheet;
            var pixels = sheet.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = SeatRecolour(pixels[i], seat);
            texture = new Texture2D(sheet.width, sheet.height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            bodies[key] = texture;
            return texture;
        }

        // Key reds, lightest first: #F87858 #E04020 #B01800 #600000, and how each one's shade is built from the seat
        // colour. The same ramp as CarSheetPalette.shader, so a car and its debris match.
        private static readonly Color32[] Keys =
            { new Color32(0xF8, 0x78, 0x58, 0), new Color32(0xE0, 0x40, 0x20, 0), new Color32(0xB0, 0x18, 0x00, 0), new Color32(0x60, 0x00, 0x00, 0) };
        private static readonly float[] Lift = { .45f, 0, 0, 0 }, Shade = { 1, 1, .68f, .38f };

        // One sheet pixel: a key red becomes its shade of the seat colour (sRGB), anything else is kept as drawn.
        public static Color32 SeatRecolour(Color32 pixel, Color seat)
        {
            for (int k = 0; k < Keys.Length; k++)
            {
                if (Mathf.Abs(pixel.r - Keys[k].r) > 5 || Mathf.Abs(pixel.g - Keys[k].g) > 5 || Mathf.Abs(pixel.b - Keys[k].b) > 5) continue;
                var c = Color.Lerp(seat, Color.white, Lift[k]) * Shade[k];
                c.a = pixel.a / 255f;
                return c;
            }
            return pixel;
        }
    }
}
