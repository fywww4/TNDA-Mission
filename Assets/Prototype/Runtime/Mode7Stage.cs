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
    [DefaultExecutionOrder(100)]
    public sealed class Mode7Stage : MonoBehaviour
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

        [Header("Minimap (the original top-down camera), in 320px cabinet pixels from the bottom left")]
        public bool minimap = true;
        public RectInt minimapRect = new RectInt(4, 24, 96, 96);

        [Header("Standing sprites: half height lifted off the floor, in world units")]
        public float carLift = .45f, propLift = .32f, pickupLift = .3f, itemLift = .3f, crownLift = 1.1f, explosionLift = .35f;

        private readonly Camera[] chase = new Camera[2];
        private readonly float[] chaseYaw = new float[2];
        private readonly bool[] chaseReady = new bool[2];
        private Camera map;
        private Rect mapRectBefore;
        private GameObject ground, floor;
        private readonly List<Standing> standing = new List<Standing>();
        private Vector2 crownShifted = new Vector2(1e6f, 1e6f);

        private struct Standing { public Transform t; public float lift, roll; public bool car; }

        // Score pop-ups and car name tags ask for a point this far north of the car (see DemolitionGame).
        private const float TagOffset = .88f;

        private void OnEnable()
        {
            map = game.sceneCamera;
            foreach (var behaviour in map.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("PixelPerfect")) behaviour.enabled = false;
            mapRectBefore = map.rect;
            for (int i = 0; i < chase.Length; i++) chase[i] = MakeChaseCamera(i);
            ground = MakeGround();
            floor = MakeArenaFloor();
            DemolitionGame.CabinetProjector = ToCabinet;
            Camera.onPreCull += OnAnyPreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnAnyPreCull;
            if (DemolitionGame.CabinetProjector == (System.Func<Vector2, Vector2>)ToCabinet) DemolitionGame.CabinetProjector = null;
            for (int i = 0; i < chase.Length; i++) if (chase[i] != null) Destroy(chase[i].gameObject);
            if (ground != null) Destroy(ground);
            if (floor != null) Destroy(floor);
            if (map != null) { map.rect = mapRectBefore; map.enabled = true; }
            LayFlat();
        }

        private void LateUpdate()
        {
            bool two = game.PlayerCount == 2 && game.Phase != ArcadePhase.Attract;
            for (int i = 0; i < chase.Length; i++)
            {
                bool on = i == 0 || two;
                chase[i].enabled = on;
                if (!on) { chaseReady[i] = false; continue; }
                chase[i].rect = two ? new Rect(0, i == 0 ? .5f : 0, 1, .5f) : new Rect(0, 0, 1, 1);
                Follow(i, Target(i));
            }
            map.enabled = minimap;
            map.cullingMask &= ~(1 << chaseOnlyLayer);
            map.rect = new Rect(minimapRect.x / 320f, minimapRect.y / 320f, minimapRect.width / 320f, minimapRect.height / 320f);
            map.depth = 10;
            CollectStanding();
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
                if (car != null) standing.Add(new Standing { t = car.transform, lift = carLift, car = true });
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
                // A car's body keeps its own heading roll (heading - 90); turn the root so the nose points up on screen
                // when the car drives straight away from this camera.
                s.t.rotation = s.car ? rot * Quaternion.Euler(0, 0, 90 - chaseYaw[seat]) : rot;
            }
        }

        private void LayFlat()
        {
            foreach (var s in standing)
            {
                if (s.t == null) continue;
                var p = s.t.position;
                s.t.SetPositionAndRotation(new Vector3(p.x, p.y, 0), Quaternion.identity);
            }
        }

        // Name tags and score pop-ups: the arena asks for a point TagOffset north of the car. Here the tag floats
        // above the standing car in P1's view instead. Points behind the camera go off screen.
        private Vector2 ToCabinet(Vector2 world)
        {
            var cam = chase[0];
            var foot = world - new Vector2(0, TagOffset);
            Vector3 s = cam.WorldToScreenPoint(new Vector3(foot.x, foot.y, -(carLift * 2 + .15f)));
            if (s.z <= 0 || !cam.pixelRect.Contains(new Vector2(s.x, s.y))) return new Vector2(9999, 9999);
            float scale = 320f / Screen.height;
            return new Vector2((s.x - Screen.width * .5f) * scale, (s.y - Screen.height * .5f) * scale);
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
            cam.cullingMask = map.cullingMask | 1 << chaseOnlyLayer;
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
