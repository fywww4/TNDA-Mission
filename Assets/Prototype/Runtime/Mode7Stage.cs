using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // Mode 7 chase view for GAME3 (Super Mario Kart / F-Zero style). The match still plays on the flat arena (x, y);
    // this only changes how it is drawn:
    // - a low perspective camera rides behind each human's car and turns with it (1P full screen, 2P split top/bottom);
    // - the arena floor is seen in perspective down to a horizon, over an endless checker ground;
    // - cars, crates, barrels, held items, crown and explosions stand up facing whichever camera is drawing;
    // - the original top-down camera becomes a corner minimap where everything lies flat again.
    // World "up" for the standing sprites is -Z (the side the top-down camera looks from).
    // Hybrid resolution (Docs/adr/0001): every scene camera draws into one 256x224 art texture, shown at 2x2 coarse
    // pixels under the 512x448 cabinet HUD, so the art looks like SFC 256x224 while text stays native and sharp.
    // The cabinet is shown at a whole-number scale (2x = 1024x896 when the screen fits it, otherwise 1x).
    [DefaultExecutionOrder(100)]
    public sealed class Mode7Stage : MonoBehaviour, ICabinetProjection
    {
        public DemolitionGame game;

        [Header("Chase camera")]
        [Tooltip("How far behind the car the camera rides, and how high.")]
        public float behind = 3.2f, height = 1.3f;
        [Tooltip("The camera looks at this point ahead of the car, this high off the floor.")]
        public float lookAhead = 2.2f, lookHeight = .25f;
        public float fieldOfView = 60;
        [Tooltip("How quickly the camera swings round to the car's heading (higher = stiffer).")]
        public float turnFollow = 5;
        public Color sky = new Color32(0x28, 0x40, 0x98, 0xFF);

        [Header("Ground beyond the arena")]
        public Color groundA = new Color32(0x30, 0x30, 0x30, 0xFF);
        public Color groundB = new Color32(0x48, 0x48, 0x48, 0xFF);
        [Tooltip("World units per checker square.")]
        public float checkerSize = 1;
        [Tooltip("The arena floor inside the fence (the top-down game draws it as the black camera background).")]
        public Color arenaFloor = Color.black;
        [Tooltip("How far the arena floor reaches past the drivable area, out to the fence.")]
        public float arenaMargin = .55f;
        [Tooltip("A layer only the chase cameras draw (the ground and arena floor), so the minimap stays as it was.")]
        [Range(8, 31)] public int chaseOnlyLayer = 31;

        [Header("Minimap (the original top-down camera), in 256x224 art pixels from the bottom left")]
        public bool minimap = true;
        public RectInt minimapRect = DefaultMinimapRect;
        public static readonly RectInt DefaultMinimapRect = new RectInt(4, 18, 72, 48);
        [Tooltip("A layer only the minimap draws (the car blips).")]
        [Range(8, 31)] public int minimapOnlyLayer = 30;
        [Tooltip("Size of each car's square blip on the minimap, in world units (bigger than a crate, so the white P3/P4 read as cars).")]
        public float blipSize = 1.6f;

        [Header("HUD lines that move with the split, in 512x448 cabinet pixels")]
        [Tooltip("The feedback line: under the top HUD in 1P; in P2's sky in 2P (P1's sky holds the scores).")]
        public Vector2Int noticeAt1P = new Vector2Int(6, 360), noticeAt2P = new Vector2Int(6, 206);

        [Header("Standing sprites: half height lifted off the floor, in world units")]
        public float carLift = .45f, propLift = .32f, pickupLift = .3f, itemLift = .3f, crownLift = 1.1f, explosionLift = .35f;

        [Header("Cars in the chase view: 16 directions, frame 0 seen from behind, then clockwise")]
        [Tooltip("CarSheet.png sliced into 16 frames, pivot on the wheels' ground line. Empty = stand the top-down sprite up.")]
        public Sprite[] carFrames;
        [Tooltip("Swaps the sheet's four key reds for the seat colour (CarSheetPalette.shader).")]
        public Material carSheetMaterial;
        [Tooltip("Ground line height off the floor for the sheet frames (their pivot is on the wheels).")]
        public float carFrameLift = 0;

        private readonly Camera[] chase = new Camera[2];
        private readonly float[] chaseYaw = new float[2];
        private readonly bool[] chaseReady = new bool[2];
        private Camera map, backdrop;
        private Rect mapRectBefore;
        private float mapSizeBefore;
        private RenderTexture art;
        private UnityEngine.UI.RawImage artImage;
        private UnityEngine.UI.CanvasScaler scaler;
        private (UnityEngine.UI.CanvasScaler.ScaleMode mode, float factor) scalerBefore;
        private readonly List<(SpriteRenderer view, ArcadeVehicle car)> blips = new List<(SpriteRenderer, ArcadeVehicle)>();
        private Texture2D blipTexture;
        private GameObject ground, floor;
        private readonly List<Standing> standing = new List<Standing>();
        private Vector2 crownShifted = new Vector2(1e6f, 1e6f);

        private struct Standing { public Transform t; public float lift; public ArcadeVehicle car; }
        // Each car's own top-down sprite and material, put back for the minimap.
        private readonly Dictionary<ArcadeVehicle, (Sprite sprite, Material material)> topDown =
            new Dictionary<ArcadeVehicle, (Sprite, Material)>();

        private bool UseFrames => carFrames != null && carFrames.Length == 16 && carSheetMaterial != null;

        // Score pop-ups and car name tags ask for a point this far north of the car (see DemolitionGame).
        private const float TagOffset = .88f;

        private void OnEnable()
        {
            map = game.sceneCamera;
            foreach (var behaviour in map.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("PixelPerfect")) behaviour.enabled = false;
            mapRectBefore = map.rect;
            mapSizeBefore = map.orthographicSize;
            art = new RenderTexture(HybridResolution.ArtWidth, HybridResolution.ArtHeight, 24)
                { name = "Mode7 art 256x224", filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            map.targetTexture = art;
            for (int i = 0; i < chase.Length; i++) chase[i] = MakeChaseCamera(i);
            backdrop = MakeBackdrop();
            ShowArtUnderHud();
            MakeBlips();
            ground = MakeGround();
            floor = MakeArenaFloor();
            DemolitionGame.CabinetProjection = this;
            Camera.onPreCull += OnAnyPreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnAnyPreCull;
            if (ReferenceEquals(DemolitionGame.CabinetProjection, this)) DemolitionGame.CabinetProjection = null;
            for (int i = 0; i < chase.Length; i++) if (chase[i] != null) Destroy(chase[i].gameObject);
            if (backdrop != null) Destroy(backdrop.gameObject);
            if (ground != null) Destroy(ground);
            if (floor != null) Destroy(floor);
            foreach (var blip in blips) if (blip.view != null) Destroy(blip.view.gameObject);
            blips.Clear();
            if (blipTexture != null) Destroy(blipTexture);
            if (artImage != null) Destroy(artImage.gameObject);
            if (scaler != null)
            {
                scaler.uiScaleMode = scalerBefore.mode;
                scaler.scaleFactor = scalerBefore.factor;
                ((RectTransform)game.header.transform.parent).anchoredPosition = Vector2.zero;
            }
            if (map != null)
            {
                map.targetTexture = null;
                map.rect = mapRectBefore;
                map.orthographicSize = mapSizeBefore;
                map.enabled = true;
            }
            if (art != null) { art.Release(); Destroy(art); }
            LayFlat();
        }

        private void LateUpdate()
        {
            bool two = TwoViews;
            for (int i = 0; i < chase.Length; i++)
            {
                bool on = i == 0 || two;
                chase[i].enabled = on;
                if (!on) { chaseReady[i] = false; continue; }
                chase[i].rect = HybridResolution.ViewRect(i, two);
                Follow(i, Target(i));
            }
            map.enabled = minimap;
            map.cullingMask = (map.cullingMask & ~(1 << chaseOnlyLayer)) | 1 << minimapOnlyLayer;
            map.rect = new Rect((float)minimapRect.x / HybridResolution.ArtWidth, (float)minimapRect.y / HybridResolution.ArtHeight,
                (float)minimapRect.width / HybridResolution.ArtWidth, (float)minimapRect.height / HybridResolution.ArtHeight);
            // The whole arena out to the fence, fitted to the minimap's shape.
            Vector2 fenced = game.arenaMax - game.arenaMin + Vector2.one * arenaMargin * 2;
            map.orthographicSize = HybridResolution.FitOrthographicSize(fenced, (float)minimapRect.width / minimapRect.height);
            map.transform.position = new Vector3((game.arenaMin.x + game.arenaMax.x) / 2, (game.arenaMin.y + game.arenaMax.y) / 2, map.transform.position.z);
            map.depth = 10;
            KeepWholeScale();
            if (game.notice != null)
                game.notice.rectTransform.anchoredPosition = two ? (Vector2)noticeAt2P : (Vector2)noticeAt1P;
            foreach (var blip in blips) blip.view.color = blip.car.body.color;
            CollectStanding();
        }

        // Shows the art texture at 2x as the bottom layer of the 512x448 cabinet, and keeps the cabinet at a whole-number scale.
        private void ShowArtUnderHud()
        {
            var cabinet = (RectTransform)game.header.transform.parent;
            var go = new GameObject("Mode7 art (256x224 at 2x)", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var rect = (RectTransform)go.transform;
            rect.SetParent(cabinet, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(HybridResolution.CabinetWidth, HybridResolution.CabinetHeight);
            artImage = go.AddComponent<UnityEngine.UI.RawImage>();
            artImage.texture = art;
            artImage.raycastTarget = false;
            // A white frame one art pixel wide round the minimap, so it reads apart from the black arena floor.
            if (minimap)
            {
                var m = minimapRect;
                const int k = HybridResolution.ArtScale;
                Edge(m.x - 1, m.y - 1, m.width + 2, 1);
                Edge(m.x - 1, m.yMax, m.width + 2, 1);
                Edge(m.x - 1, m.y, 1, m.height);
                Edge(m.xMax, m.y, 1, m.height);
                void Edge(int x, int y, int w, int h)
                {
                    var edge = new GameObject("Minimap frame", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
                    var r = (RectTransform)edge.transform;
                    r.SetParent(rect, false);
                    r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
                    r.anchoredPosition = new Vector2(x * k, y * k);
                    r.sizeDelta = new Vector2(w * k, h * k);
                    var image = edge.AddComponent<UnityEngine.UI.Image>();
                    image.color = ArcadeGame.White;
                    image.raycastTarget = false;
                }
            }
            scaler = cabinet.GetComponentInParent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scalerBefore = (scaler.uiScaleMode, scaler.scaleFactor);
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            }
        }

        // The canvas at the largest whole multiple of 512x448 that fits. When the border left round the cabinet is
        // an odd number of screen pixels, centring would put the cabinet on a half pixel, so it moves half a pixel back.
        private void KeepWholeScale()
        {
            if (scaler == null) return;
            int k = HybridResolution.DisplayScale(Screen.width, Screen.height);
            scaler.scaleFactor = k;
            var cabinet = (RectTransform)game.header.transform.parent;
            float Snap(int screen, int size) => (screen - size * k) % 2 == 0 ? 0 : -.5f / k;
            cabinet.anchoredPosition = new Vector2(Snap(Screen.width, HybridResolution.CabinetWidth), Snap(Screen.height, HybridResolution.CabinetHeight));
        }

        // Every scene camera now draws into the art texture, so one camera clears the real screen (the border round
        // the cabinet when the window is not an exact multiple of 512x448).
        private Camera MakeBackdrop()
        {
            var cam = new GameObject("Mode7 backdrop") { hideFlags = HideFlags.DontSave }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.depth = -100;
            return cam;
        }

        // A square in each car's colour that only the minimap draws, so every car reads at minimap size.
        private void MakeBlips()
        {
            blipTexture = new Texture2D(1, 1) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            blipTexture.SetPixel(0, 0, Color.white);
            blipTexture.Apply();
            var sprite = Sprite.Create(blipTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            sprite.hideFlags = HideFlags.DontSave;
            foreach (var car in game.cars)
            {
                if (car == null) continue;
                var go = new GameObject("Minimap blip") { hideFlags = HideFlags.DontSave, layer = minimapOnlyLayer };
                go.transform.SetParent(car.transform, false);
                go.transform.localScale = Vector3.one * blipSize;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 1000;
                blips.Add((renderer, car));
            }
        }

        // The car a view follows: the seat's own car, or the leader once that car is out for good.
        private ArcadeVehicle Target(int seat)
        {
            var own = game.cars[Mathf.Min(seat, game.cars.Length - 1)];
            if (!own.eliminated) return own;
            ArcadeVehicle best = own;
            foreach (var car in game.cars)
                if (!car.eliminated && (best.eliminated || car.score > best.score)) best = car;
            return best;
        }

        private void Follow(int i, ArcadeVehicle car)
        {
            float dt = Time.unscaledDeltaTime;
            if (!chaseReady[i]) { chaseYaw[i] = car.heading; chaseReady[i] = true; }
            chaseYaw[i] = Mathf.LerpAngle(chaseYaw[i], car.heading, 1 - Mathf.Exp(-turnFollow * dt));
            var dir = new Vector2(Mathf.Cos(chaseYaw[i] * Mathf.Deg2Rad), Mathf.Sin(chaseYaw[i] * Mathf.Deg2Rad));
            Vector2 eye = car.position - dir * behind, look = car.position + dir * lookAhead;
            var from = new Vector3(eye.x, eye.y, -height);
            var to = new Vector3(look.x, look.y, -lookHeight);
            chase[i].transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from, Vector3.back));
        }

        private void CollectStanding()
        {
            standing.Clear();
            foreach (var car in game.cars)
                if (car != null)
                {
                    if (!topDown.ContainsKey(car)) topDown[car] = (car.body.sprite, car.body.sharedMaterial);
                    standing.Add(new Standing { t = car.transform, lift = UseFrames ? carFrameLift : carLift, car = car });
                }
            foreach (var prop in game.props) if (prop.view != null) standing.Add(new Standing { t = prop.view, lift = propLift });
            foreach (var pickup in game.pickups) if (pickup.view != null) standing.Add(new Standing { t = pickup.view, lift = pickupLift });
            if (game.itemViews != null)
                foreach (var item in game.itemViews) if (item != null) standing.Add(new Standing { t = item.transform, lift = itemLift });
            if (game.explosions != null)
                foreach (var boom in game.explosions) if (boom != null) standing.Add(new Standing { t = boom.transform, lift = explosionLift });
            if (game.crownView != null)
            {
                // The arena puts the crown crownHeight north of the leader; in the chase view it floats straight above.
                var crown = game.crownView.transform;
                // The arena may not rewrite it every frame, so only shift a position it has newly written.
                Vector2 at = crown.position;
                if ((at - crownShifted).sqrMagnitude > 1e-8f)
                {
                    crownShifted = at - new Vector2(0, game.crownHeight);
                    crown.position = new Vector3(crownShifted.x, crownShifted.y, crown.position.z);
                }
                standing.Add(new Standing { t = crown, lift = crownLift });
            }
        }

        // Before each camera draws: stand sprites up facing a chase camera, or lay them flat for the minimap.
        private void OnAnyPreCull(Camera cam)
        {
            if (cam == map) { LayFlat(); return; }
            int seat = cam == chase[0] ? 0 : cam == chase[1] ? 1 : -1;
            if (seat < 0) return;
            var rot = cam.transform.rotation;
            foreach (var s in standing)
            {
                if (s.t == null) continue;
                var p = s.t.position;
                s.t.position = new Vector3(p.x, p.y, -s.lift);
                if (s.car != null && UseFrames)
                {
                    // Pick the sheet frame for the angle this camera sees the car from: its bearing from the camera
                    // minus the car's heading (0 = driving straight away, 90 = nose to the right, clockwise).
                    Vector2 from = (Vector2)s.t.position - (Vector2)cam.transform.position;
                    float bearing = from.sqrMagnitude > 1e-6f ? Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg : chaseYaw[seat];
                    int frame = Mathf.RoundToInt(Mathf.Repeat(bearing - s.car.heading, 360) / 22.5f) % 16;
                    s.car.body.sprite = carFrames[frame];
                    s.car.body.sharedMaterial = carSheetMaterial;
                    s.car.body.transform.localRotation = Quaternion.identity;
                    s.t.rotation = rot;
                }
                else
                    // A car's body keeps its own heading roll (heading - 90); turn the root so the nose points up on
                    // screen when the car drives straight away from this camera.
                    s.t.rotation = s.car != null ? rot * Quaternion.Euler(0, 0, 90 - chaseYaw[seat]) : rot;
            }
        }

        private void LayFlat()
        {
            foreach (var s in standing)
            {
                if (s.t == null) continue;
                var p = s.t.position;
                s.t.SetPositionAndRotation(new Vector3(p.x, p.y, 0), Quaternion.identity);
                if (s.car != null && topDown.TryGetValue(s.car, out var top))
                {
                    // The minimap keeps the top-down car.
                    s.car.body.sprite = top.sprite;
                    s.car.body.sharedMaterial = top.material;
                    s.car.body.transform.localRotation = Quaternion.Euler(0, 0, s.car.heading - 90);
                }
            }
        }

        // Name tags and score pop-ups get a set per chase view: one in 1P and attract, two in a 2P split.
        private bool TwoViews => game.PlayerCount == 2 && game.Phase != ArcadePhase.Attract;

        int ICabinetProjection.Views => TwoViews ? 2 : 1;

        // The arena asks for a point TagOffset north of the car. Here the tag floats above the standing car as that
        // view's camera sees it; behind the camera or outside the view's half it is not shown. The cameras draw into
        // the 256x224 art texture, so their screen points are art pixels; the cabinet is twice that.
        bool ICabinetProjection.ToCabinet(Vector2 world, int view, out Vector2 cabinet)
        {
            cabinet = default;
            if (view < 0 || view >= chase.Length || chase[view] == null) return false;
            var cam = chase[view];
            var foot = world - new Vector2(0, TagOffset);
            Vector3 s = cam.WorldToScreenPoint(new Vector3(foot.x, foot.y, -(carLift * 2 + .15f)));
            return HybridResolution.TagOnView(s, cam.pixelRect, out cabinet);
        }

        private Camera MakeChaseCamera(int seat)
        {
            var go = new GameObject("Mode7 chase P" + (seat + 1)) { hideFlags = HideFlags.DontSave };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 60;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            cam.cullingMask = (map.cullingMask | 1 << chaseOnlyLayer) & ~(1 << minimapOnlyLayer);
            cam.targetTexture = art;
            cam.depth = map.depth - 1 - seat;
            cam.transparencySortMode = TransparencySortMode.Perspective;
            return cam;
        }

        // An endless-looking checker floor a hair below the arena, so the view reaches a horizon.
        private GameObject MakeGround()
        {
            float size = 200;
            var go = MakeQuad("Mode7 ground", new[] { groundA, groundB, groundB, groundA }, 2, Vector2.zero, new Vector2(size, size), .02f);
            go.GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale = Vector2.one * (size / checkerSize / 2);
            return go;
        }

        // The arena's own floor, so the drivable area reads apart from the ground outside the fence.
        private GameObject MakeArenaFloor()
        {
            Vector2 min = game.arenaMin - Vector2.one * arenaMargin, max = game.arenaMax + Vector2.one * arenaMargin;
            return MakeQuad("Mode7 arena floor", new[] { arenaFloor }, 1, (min + max) / 2, max - min, .01f);
        }

        private GameObject MakeQuad(string name, Color[] pixels, int side, Vector2 centre, Vector2 size, float depth)
        {
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            tex.SetPixels(pixels);
            tex.Apply();
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            go.layer = chaseOnlyLayer;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(new Vector3(centre.x, centre.y, depth), Quaternion.identity);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Unlit/Texture")) { mainTexture = tex, hideFlags = HideFlags.DontSave };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
