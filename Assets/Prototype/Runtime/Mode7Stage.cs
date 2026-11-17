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
    // Hybrid resolution (Docs/adr/0001): chase views share a 256x224 art texture; the minimap has its own texture
    // above debris. Both show at 2x2 coarse pixels under the native 512x448 cabinet HUD.
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
        [Tooltip("Scene-referenced ground shader, included in Windows and Web builds. Runtime textures are applied to copies.")]
        public Material groundMaterial;
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
        [Tooltip("Size of each car's arrow on the minimap, in world units.")]
        public float blipSize = 1.6f;
        [Tooltip("Side of each spike ball's white dot on the minimap, in minimap pixels.")]
        public int spikeDotPixels = 2;

        [Header("HUD lines that move with the split, in 512x448 cabinet pixels")]
        [Tooltip("The feedback line: under the top HUD in 1P; in P2's sky in 2P (P1's sky holds the scores).")]
        public Vector2Int noticeAt1P = new Vector2Int(6, 360), noticeAt2P = new Vector2Int(6, 206);

        [Header("Standing sprites: half height lifted off the floor, in world units")]
        public float carLift = .45f, propLift = .32f, pickupLift = .3f, itemLift = .3f, crownLift = 1.1f, explosionLift = .35f;
        [Tooltip("Art pixels of clear space between the leader's name tag and the crown above it (crownLift is the lowest it goes).")]
        public float crownGap = 1;

        [Header("Cars in the chase view: 16 directions, frame 0 seen from behind, then clockwise")]
        [Tooltip("CarSheet.png sliced into 16 frames, pivot on the wheels' ground line. Empty = stand the top-down sprite up.")]
        public Sprite[] carFrames;
        [Tooltip("Swaps the sheet's four key reds for the seat colour (CarSheetPalette.shader).")]
        public Material carSheetMaterial;
        [Tooltip("Ground line height off the floor for the sheet frames (their pivot is on the wheels).")]
        public float carFrameLift = 0;
        [Tooltip("Ticket 16: the four pattern sheets (DecalTwinStripes, DecalFlames, DecalLightning, DecalChecker), 16 frames each "
            + "in carFrames' order, drawn unpaletted over a P1/P2 car (DemolitionGame.ShownDecal).")]
        public Sprite[] decalFrames;

        private readonly Camera[] chase = new Camera[2];
        private readonly float[] chaseYaw = new float[2];
        private readonly bool[] chaseReady = new bool[2];
        private readonly ScreenShake[] shakes = { new ScreenShake(), new ScreenShake() };
        private Camera map, backdrop;
        private Rect mapRectBefore;
        private float mapSizeBefore;
        private RenderTexture mapTargetBefore;
        private int mapMaskBefore;
        private float mapDepthBefore;
        private RenderTexture art, mapArt;
        private UnityEngine.UI.RawImage artImage, mapImage;
        private UnityEngine.UI.CanvasScaler scaler;
        private (UnityEngine.UI.CanvasScaler.ScaleMode mode, float factor) scalerBefore;
        private readonly List<(SpriteRenderer view, ArcadeVehicle car)> blips = new List<(SpriteRenderer, ArcadeVehicle)>();
        private readonly Sprite[] blipFrames = new Sprite[16];
        // Ticket 21: a white dot per spike ball, drawn only by the minimap (the ball sprite itself is chase-only).
        private readonly List<SpriteRenderer> spikeDots = new List<SpriteRenderer>();
        private Sprite spikeDot;
        private MaterialPropertyBlock blipPalette;
        private GameObject ground, floor;
        private readonly List<Standing> standing = new List<Standing>();
        private readonly Dictionary<Transform, UnityEngine.Rendering.SortingGroup> standingGroups =
            new Dictionary<Transform, UnityEngine.Rendering.SortingGroup>();
        private Vector2 crownShifted = new Vector2(1e6f, 1e6f);

        private struct Standing { public Transform t; public Vector2 position; public float lift; public ArcadeVehicle car; }
        // Each car's own top-down sprite and material, put back for the minimap.
        private readonly Dictionary<ArcadeVehicle, (Sprite sprite, Material material)> topDown =
            new Dictionary<ArcadeVehicle, (Sprite, Material)>();

        // One pattern overlay per car, a child of its body; only the chase cameras ever see it switched on.
        private readonly Dictionary<ArcadeVehicle, SpriteRenderer> decalViews = new Dictionary<ArcadeVehicle, SpriteRenderer>();

        private bool UseFrames => carFrames != null && carFrames.Length == 16 && carSheetMaterial != null;

        // Score pop-ups and car name tags ask for a point this far north of the car (see DemolitionGame).
        private const float TagOffset = .88f;
        // Name tags float this high above the floor in the chase view; a tag is 13 cabinet pixels tall (6.5 art).
        private float TagLift => carLift * 2 + .15f;
        private const float TagHalfHeight = 3.25f;

        private void OnEnable()
        {
            blipPalette = new MaterialPropertyBlock();
            map = game.sceneCamera;
            foreach (var behaviour in map.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("PixelPerfect")) behaviour.enabled = false;
            mapRectBefore = map.rect;
            mapSizeBefore = map.orthographicSize;
            mapTargetBefore = map.targetTexture;
            mapMaskBefore = map.cullingMask;
            mapDepthBefore = map.depth;
            art = new RenderTexture(HybridResolution.ArtWidth, HybridResolution.ArtHeight, 24)
                { name = "Mode7 art 256x224", filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            mapArt = new RenderTexture(minimapRect.width, minimapRect.height, 24)
                { name = "Mode7 minimap", filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            map.targetTexture = mapArt;
            for (int i = 0; i < chase.Length; i++) chase[i] = MakeChaseCamera(i);
            backdrop = MakeBackdrop();
            ShowArtUnderHud();
            ShowMinimapOverDebris();
            MakeBlips();
            ground = MakeGround();
            floor = MakeArenaFloor();
            DemolitionGame.CabinetProjection = this;
            Camera.onPreCull += OnAnyPreCull;
            game.ShakePresented += Shake;
        }

        private void OnDisable()
        {
            game.ShakePresented -= Shake;
            Camera.onPreCull -= OnAnyPreCull;
            if (ReferenceEquals(DemolitionGame.CabinetProjection, this)) DemolitionGame.CabinetProjection = null;
            for (int i = 0; i < chase.Length; i++) if (chase[i] != null) Destroy(chase[i].gameObject);
            if (backdrop != null) Destroy(backdrop.gameObject);
            DestroyGround(ground);
            DestroyGround(floor);
            foreach (var blip in blips) if (blip.view != null) Destroy(blip.view.gameObject);
            blips.Clear();
            foreach (var view in decalViews.Values) if (view != null) Destroy(view.gameObject);
            decalViews.Clear();
            foreach (var sprite in blipFrames)
                if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
            foreach (var dot in spikeDots) if (dot != null) Destroy(dot.gameObject);
            spikeDots.Clear();
            if (spikeDot != null) { Destroy(spikeDot.texture); Destroy(spikeDot); }
            if (artImage != null) Destroy(artImage.gameObject);
            if (mapImage != null) Destroy(mapImage.gameObject);
            if (scaler != null)
            {
                scaler.uiScaleMode = scalerBefore.mode;
                scaler.scaleFactor = scalerBefore.factor;
                ((RectTransform)game.header.transform.parent).anchoredPosition = Vector2.zero;
            }
            if (map != null)
            {
                map.targetTexture = mapTargetBefore;
                map.rect = mapRectBefore;
                map.orthographicSize = mapSizeBefore;
                map.cullingMask = mapMaskBefore;
                map.depth = mapDepthBefore;
                map.enabled = true;
            }
            if (art != null) { art.Release(); Destroy(art); }
            if (mapArt != null) { mapArt.Release(); Destroy(mapArt); }
            LayFlat();
            foreach (var group in standingGroups.Values) if (group != null) Destroy(group);
            standingGroups.Clear();
        }

        private void LateUpdate()
        {
            bool two = TwoViews;
            for (int i = 0; i < chase.Length; i++)
            {
                bool on = i == 0 || two;
                chase[i].enabled = on;
                if (!on) { chaseReady[i] = false; shakes[i].Stop(); continue; }
                // The attract demo never shakes; a shake left from the last match ends with it.
                if (game.Phase == ArcadePhase.Attract) shakes[i].Stop();
                chase[i].rect = HybridResolution.ViewRect(i, two);
                Follow(i, Target(i));
                ApplyShake(i);
            }
            map.enabled = minimap;
            mapImage.gameObject.SetActive(minimap);
            map.cullingMask = (map.cullingMask & ~(1 << chaseOnlyLayer)) | 1 << minimapOnlyLayer;
            map.rect = new Rect(0, 0, 1, 1);
            // The whole arena out to the fence, fitted to the minimap's shape.
            Vector2 fenced = game.arenaMax - game.arenaMin + Vector2.one * arenaMargin * 2;
            map.orthographicSize = HybridResolution.FitOrthographicSize(fenced, (float)minimapRect.width / minimapRect.height);
            map.transform.position = new Vector3((game.arenaMin.x + game.arenaMax.x) / 2, (game.arenaMin.y + game.arenaMax.y) / 2, map.transform.position.z);
            map.depth = 10;
            KeepWholeScale();
            if (game.notice != null)
                game.notice.rectTransform.anchoredPosition = two ? (Vector2)noticeAt2P : (Vector2)noticeAt1P;
            PlaceContinuePanels(two);
            if (game.cars.Length > 4 && game.hud != null)
                game.hud.rectTransform.anchoredPosition = new Vector2(6, two ? 210 : 362);
            foreach (var blip in blips)
            {
                blip.view.color = blip.car.body.color;
                blip.car.body.GetPropertyBlock(blipPalette);
                blip.view.SetPropertyBlock(blipPalette);
            }
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
            scaler = cabinet.GetComponentInParent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scalerBefore = (scaler.uiScaleMode, scaler.scaleFactor);
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            }
        }

        private void ShowMinimapOverDebris()
        {
            var cabinet = game.header.transform.parent;
            var go = new GameObject("Mode7 minimap (2x)", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var rect = (RectTransform)go.transform;
            rect.SetParent(cabinet, false);
            // Debris inserts immediately after the art. Insert before the HUD, including when re-enabled while
            // the debris layers already exist, so the order remains art / debris / map / HUD.
            rect.SetSiblingIndex(game.header.transform.GetSiblingIndex());
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = (Vector2)minimapRect.position * HybridResolution.ArtScale;
            rect.sizeDelta = (Vector2)minimapRect.size * HybridResolution.ArtScale;
            mapImage = go.AddComponent<UnityEngine.UI.RawImage>();
            mapImage.texture = mapArt;
            mapImage.raycastTarget = false;
            // A white frame one art pixel wide round the minimap, so it reads apart from the black arena floor.
            {
                var m = minimapRect;
                const int k = HybridResolution.ArtScale;
                Edge(-1, -1, m.width + 2, 1);
                Edge(-1, m.height, m.width + 2, 1);
                Edge(-1, 0, 1, m.height);
                Edge(m.width, 0, 1, m.height);
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

        // Bake the sixteen arrow directions into texels instead of rotating a square sprite on the pixel grid.
        private void MakeBlips()
        {
            const int side = 7;
            for (int direction = 0; direction < blipFrames.Length; direction++)
            {
                var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                float angle = direction * 22.5f * Mathf.Deg2Rad;
                var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
                {
                    var pixel = new Vector2(x - 3, y - 3);
                    float along = Vector2.Dot(pixel, forward), across = Mathf.Abs(pixel.x * forward.y - pixel.y * forward.x);
                    bool head = along >= -1 && across <= (3.5f - along) * .78f;
                    bool shaft = along < -1 && along >= -3.5f && across <= 1;
                    // Red is tinted by the car palette, including P3/P4's three-texel stripes.
                    texture.SetPixel(x, y, head || shaft ? Color.red : Color.clear);
                }
                texture.Apply();
                blipFrames[direction] = Sprite.Create(texture, new Rect(0, 0, side, side), Vector2.one * .5f, side);
                blipFrames[direction].hideFlags = HideFlags.DontSave;
            }
            foreach (var car in game.cars)
            {
                if (car == null) continue;
                var go = new GameObject("Minimap blip") { hideFlags = HideFlags.DontSave, layer = minimapOnlyLayer };
                go.transform.SetParent(car.transform, false);
                go.transform.localScale = Vector3.one * blipSize;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = blipFrames[0];
                renderer.sharedMaterial = car.body.sharedMaterial;
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
                if (!car.eliminated && car.gameObject.activeInHierarchy && (best.eliminated || car.score > best.score)) best = car;
            return best;
        }

        // Ticket 18: a player's own ram, or being rammed, shakes only that player's view.
        private void Shake(ShakeKick kick)
        {
            int seat = kick.Car.index;
            if (seat < shakes.Length && (seat == 0 || TwoViews)) shakes[seat].Kick(kick.Pixels, kick.Seconds);
        }

        // Shifts the view's picture by whole art pixels through the projection, so the art stays pixel-sharp and the
        // edges show more of the scene instead of a gap; name tags projected through the camera move with it.
        private void ApplyShake(int i)
        {
            var cam = chase[i];
            Vector2Int offset = shakes[i].Advance(Time.unscaledDeltaTime);
            cam.ResetProjectionMatrix();
            if (offset == Vector2Int.zero) return;
            var m = cam.projectionMatrix;
            m.m02 += 2f * offset.x / Mathf.Max(1, cam.pixelWidth);
            m.m12 += 2f * offset.y / Mathf.Max(1, cam.pixelHeight);
            cam.projectionMatrix = m;
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
                    standing.Add(new Standing { t = car.transform, lift = (UseFrames ? carFrameLift : carLift) + VehicleLift(car), car = car });
                }
            foreach (var prop in game.props) if (prop.view != null) standing.Add(new Standing { t = prop.view, lift = propLift });
            foreach (var pickup in game.pickups) if (pickup.view != null) standing.Add(new Standing { t = pickup.view, lift = pickupLift });
            if (game.itemViews != null)
                foreach (var item in game.itemViews) if (item != null) standing.Add(new Standing { t = item.transform, lift = itemLift });
            if (game.explosions != null)
                foreach (var boom in game.explosions) if (boom != null) standing.Add(new Standing { t = boom.transform, lift = explosionLift });
            foreach (var spark in game.sparks) if (spark != null) standing.Add(new Standing { t = spark.transform, lift = explosionLift });
            foreach (var ball in game.spikeBallViews)
                if (ball != null)
                {
                    // The ball stands on the floor (pivot at its bottom); the minimap shows its white dot instead.
                    ball.gameObject.layer = chaseOnlyLayer;
                    standing.Add(new Standing { t = ball.transform, lift = 0 });
                }
            KeepSpikeDots();
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
            for (int i = 0; i < standing.Count; i++)
            {
                var s = standing[i];
                // A held item is a car child: capture its floor position before turning the parent for either
                // camera. Otherwise the parent's billboard rotation moves it differently in P1, P2 and the map.
                s.position = s.t.position;
                standing[i] = s;
                if (!standingGroups.ContainsKey(s.t))
                {
                    // Keep each object's own sprite layers (body, shield, crate details) together. All objects
                    // share the outer layer/order, so Perspective sorting uses this camera's distance to them.
                    var group = s.t.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    group.hideFlags = HideFlags.DontSave;
                    // Above flat floor markings (1) and oil slicks (3), while all standing groups still share one
                    // outer order and sort against one another by camera distance.
                    group.sortingOrder = 10;
                    // Held items are children of cars, but have their own position and must sort independently.
                    group.sortAtRoot = true;
                    group.enabled = false;
                    standingGroups.Add(s.t, group);
                }
            }
        }

        // Before each camera draws: stand sprites up facing a chase camera, or lay them flat for the minimap.
        private void OnAnyPreCull(Camera cam)
        {
            if (cam == map) { LayFlat(); return; }
            int seat = cam == chase[0] ? 0 : cam == chase[1] ? 1 : -1;
            if (seat < 0) return;
            foreach (var group in standingGroups.Values) group.enabled = true;
            var rot = cam.transform.rotation;
            foreach (var s in standing)
            {
                if (s.t == null) continue;
                var p = s.position;
                float lift = game.crownView != null && s.t == game.crownView.transform ? CrownLift(cam, p) : s.lift;
                s.t.position = new Vector3(p.x, p.y, -lift);
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
                    ShowDecal(s.car, frame);
                }
                else
                    // A car's body keeps its own heading roll (heading - 90); turn the root so the nose points up on
                    // screen when the car drives straight away from this camera.
                    s.t.rotation = s.car != null ? rot * Quaternion.Euler(0, 0, 90 - chaseYaw[seat]) : rot;
            }
            // The rolling frames roll to the right; flip them for a ball rolling leftwards across this view.
            for (int i = 0; i < game.spikeBallViews.Length && i < game.spikeBalls.Length; i++)
                if (game.spikeBallViews[i] != null)
                    game.spikeBallViews[i].flipX = Vector2.Dot(game.spikeBalls[i].velocity, cam.transform.right) < 0;
            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
        }

        // The minimap keeps plain seat colours.
        private void HideDecal(ArcadeVehicle car)
        {
            if (decalViews.TryGetValue(car, out var view) && view != null) view.enabled = false;
        }

        // The car's pattern (if it shows one) on the given sheet frame.
        private void ShowDecal(ArcadeVehicle car, int frame)
        {
            var sprite = car.body.enabled ? CarDecals.Frame(decalFrames, game.ShownDecal(car), frame) : null;
            bool on = sprite != null;
            if (!decalViews.TryGetValue(car, out var view) || view == null)
            {
                if (!on) return;
                var go = new GameObject("Decal") { hideFlags = HideFlags.DontSave, layer = car.body.gameObject.layer };
                go.transform.SetParent(car.body.transform, false);
                view = go.AddComponent<SpriteRenderer>();
                decalViews[car] = view;
            }
            view.enabled = on;
            if (!on) return;
            view.sprite = sprite;
            view.color = car.body.color.a < 1 ? new Color(1, 1, 1, car.body.color.a) : Color.white;
            view.sortingLayerID = car.body.sortingLayerID;
            view.sortingOrder = car.body.sortingOrder + 1;
        }

        // One white minimap dot per spike ball.
        private void KeepSpikeDots()
        {
            if (spikeDot == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                spikeDot = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                spikeDot.hideFlags = HideFlags.DontSave;
            }
            while (spikeDots.Count < game.spikeBalls.Length)
            {
                var go = new GameObject("Minimap spike ball") { hideFlags = HideFlags.DontSave, layer = minimapOnlyLayer };
                go.transform.SetParent(transform, false);
                var dot = go.AddComponent<SpriteRenderer>();
                dot.sprite = spikeDot;
                dot.color = ArcadeGame.White;
                dot.sortingOrder = 999; // under the car arrows
                spikeDots.Add(dot);
            }
            for (int i = 0; i < spikeDots.Count; i++) spikeDots[i].gameObject.SetActive(i < game.spikeBalls.Length);
        }

        // The crown floats clear above the leader's name tag. The tag is cabinet text, the same height in pixels at
        // any distance, so the crown's lift is worked out in this view's art pixels: tag point, half the tag, a gap,
        // then half the crown. Never lower than crownLift.
        private float CrownLift(Camera cam, Vector2 at)
        {
            var tag = new Vector3(at.x, at.y, -TagLift);
            float depth = Vector3.Dot(tag - cam.transform.position, cam.transform.forward);
            if (depth <= 0) return crownLift;
            float perPixel = HybridResolution.WorldPerArtPixel(depth, cam.fieldOfView, cam.pixelHeight);
            var crown = game.crownView;
            float half = crown.sprite != null ? crown.sprite.bounds.extents.y * crown.transform.lossyScale.y : 0;
            return Mathf.Max(crownLift, TagLift + (TagHalfHeight + crownGap) * perPixel + half);
        }

        private void LayFlat()
        {
            foreach (var group in standingGroups.Values) if (group != null) group.enabled = false;
            foreach (var s in standing)
            {
                if (s.t == null) continue;
                var p = s.position;
                s.t.SetPositionAndRotation(new Vector3(p.x, p.y, 0), Quaternion.identity);
                if (s.car != null && topDown.TryGetValue(s.car, out var top))
                {
                    // The minimap keeps the top-down car.
                    s.car.body.sprite = top.sprite;
                    s.car.body.sharedMaterial = top.material;
                    s.car.body.transform.localRotation = Quaternion.Euler(0, 0, s.car.heading - 90);
                }
                if (s.car != null) HideDecal(s.car);
            }
            foreach (var blip in blips)
            {
                blip.view.sprite = blipFrames[Mathf.RoundToInt(Mathf.Repeat(blip.car.heading, 360) / 22.5f) % 16];
                // Whole texels and pixel-aligned edges keep every baked direction and stripe intact on the map.
                float pixel = map.orthographicSize * 2 / map.pixelHeight;
                int pixels = 7 * Mathf.Max(1, Mathf.RoundToInt(blipSize / (7 * pixel)));
                float halfPixel = pixels % 2 * .5f;
                var at = map.WorldToViewportPoint(blip.car.position);
                at.x = (Mathf.Round(at.x * map.pixelWidth - halfPixel) + halfPixel) / map.pixelWidth;
                at.y = (Mathf.Round(at.y * map.pixelHeight - halfPixel) + halfPixel) / map.pixelHeight;
                blip.view.transform.SetPositionAndRotation(map.ViewportToWorldPoint(at), Quaternion.identity);
                var parentScale = blip.car.transform.lossyScale;
                blip.view.transform.localScale = new Vector3(pixels * pixel / parentScale.x, pixels * pixel / parentScale.y, 1);
            }
            for (int i = 0; i < spikeDots.Count && i < game.spikeBalls.Length; i++)
            {
                // Whole minimap pixels, edges on the pixel grid.
                float pixel = map.orthographicSize * 2 / map.pixelHeight;
                float halfPixel = spikeDotPixels % 2 * .5f;
                var at = map.WorldToViewportPoint(game.spikeBalls[i].position);
                at.x = (Mathf.Round(at.x * map.pixelWidth - halfPixel) + halfPixel) / map.pixelWidth;
                at.y = (Mathf.Round(at.y * map.pixelHeight - halfPixel) + halfPixel) / map.pixelHeight;
                var t = spikeDots[i].transform;
                t.SetPositionAndRotation(map.ViewportToWorldPoint(at), Quaternion.identity);
                var parentScale = transform.lossyScale;
                t.localScale = new Vector3(spikeDotPixels * pixel / parentScale.x, spikeDotPixels * pixel / parentScale.y, 1);
            }
            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
        }

        private float VehicleLift(ArcadeVehicle car)
        {
            if (car.Airborne) return car.AirHeight;
            if (!car.alive) return 0;
            foreach (var obstacle in game.obstacles)
                if (obstacle.kind == ObstacleKind.Ramp && obstacle.Contains(car.position))
                    return obstacle.height * Mathf.Clamp01(obstacle.ToLocal(car.position).y / obstacle.size.y + .5f);
            return 0;
        }

        // Each player's continue panel sits on that player's own road; GAME OVER (first panel) is centred on the screen.
        private void PlaceContinuePanels(bool two)
        {
            if (game.continuePanels == null) return;
            for (int seat = 0; seat < game.continuePanels.Length; seat++)
            {
                var view = game.continuePanels[seat];
                if (view == null || view.panel == null) continue;
                var rect = (RectTransform)view.panel.transform;
                rect.anchoredPosition = HybridResolution.ContinueCentre(seat, two && !game.ContinueShowsGameOver) - rect.sizeDelta / 2;
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
            float air = 0;
            foreach (var car in game.cars) if ((car.position - foot).sqrMagnitude < .01f) air = VehicleLift(car);
            Vector3 s = cam.WorldToScreenPoint(new Vector3(foot.x, foot.y, -TagLift - air));
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
            // CreatePrimitive also tries to add a MeshCollider. This is decorative geometry; WebGL strips the
            // unused collider class, so even destroying that collider immediately would log a player error.
            var mesh = new Mesh { name = name + " mesh", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.hideFlags = HideFlags.DontSave;
            go.layer = chaseOnlyLayer;
            go.transform.SetPositionAndRotation(new Vector3(centre.x, centre.y, depth), Quaternion.identity);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(groundMaterial) { mainTexture = tex, hideFlags = HideFlags.DontSave };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private void DestroyGround(GameObject quad)
        {
            if (quad == null) return;
            var material = quad.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(quad.GetComponent<MeshFilter>().sharedMesh);
            Destroy(material.mainTexture);
            Destroy(material);
            Destroy(quad);
        }
    }
}
