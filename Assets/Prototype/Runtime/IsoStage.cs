using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // Rock N' Roll Racing style presentation for GAME3. The match still plays on the flat top-down arena (x, y); this
    // only changes how it is drawn: the orthographic camera is tilted and turned so the floor reads as a diagonal
    // slab, tall things (crates, barrels, held items, crown, explosions) stand up facing the camera, and flat ground
    // shadows tie everything to the floor. Remove the component and the scene is top-down again.
    [DefaultExecutionOrder(100)]
    public sealed class IsoStage : MonoBehaviour, ICabinetProjection
    {
        public DemolitionGame game;
        public Camera view;

        [Header("Camera")]
        [Tooltip("Turn of the arena around the vertical axis. 45 is the classic diagonal; 0 is straight on.")]
        [Range(-90, 90)] public float yaw = 30;
        [Tooltip("Tilt from straight down. 90 = top-down, ~55 = SNES racer look, lower = flatter, more horizon.")]
        [Range(20, 90)] public float tilt = 50;
        [Tooltip("Orthographic half height. The turned arena is wider than the flat one, so it needs more room.")]
        public float orthoSize = 6.2f;
        [Tooltip("The arena point the camera looks at.")]
        public Vector2 focus = new Vector2(0, -1.1f);
        public float cameraDistance = 20;

        [Header("Standing sprites (lift along the screen's up direction, in world units)")]
        public float propLift = .32f, pickupLift = .3f, itemLift = .3f, crownLift = .85f, explosionLift = .3f;

        [Header("Ground shadows")]
        public Vector2 carShadow = new Vector2(.95f, .55f);
        public Vector2 standShadow = new Vector2(.6f, .3f);

        private struct Written { public Vector3 raw, after; }
        private readonly Dictionary<Transform, Written> written = new Dictionary<Transform, Written>();
        private readonly List<Shadow> shadows = new List<Shadow>();
        private Sprite shadowSprite;

        private sealed class Shadow
        {
            public SpriteRenderer renderer;
            public Transform follow;
            public SpriteRenderer reference;
            public Vector2 size;
        }

        private void OnEnable()
        {
            if (view == null) view = Camera.main;
            // The built-in pixel snap would fight a tilted camera.
            foreach (var behaviour in view.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("PixelPerfect")) behaviour.enabled = false;
            view.orthographic = true;
            DemolitionGame.CabinetProjection = this;
            BuildShadows();
            Place();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(DemolitionGame.CabinetProjection, this)) DemolitionGame.CabinetProjection = null;
            foreach (var shadow in shadows) if (shadow.renderer != null) Destroy(shadow.renderer.gameObject);
            shadows.Clear();
            written.Clear();
        }

        private void LateUpdate()
        {
            Place();
            foreach (var prop in game.props)
                if (prop.view != null && prop.view.gameObject.activeSelf) Stand(prop.view, propLift, 0);
            foreach (var pickup in game.pickups)
                if (pickup.view != null && pickup.view.gameObject.activeSelf) Stand(pickup.view, pickupLift, 0);
            if (game.itemViews != null)
                foreach (var item in game.itemViews)
                    if (item != null && item.gameObject.activeSelf) Stand(item.transform, itemLift, 0);
            if (game.explosions != null)
                foreach (var boom in game.explosions)
                    if (boom != null && boom.gameObject.activeSelf) Stand(boom.transform, explosionLift, 0);
            if (game.crownView != null && game.crownView.gameObject.activeSelf)
                Stand(game.crownView.transform, crownLift, game.crownHeight);
            UpdateShadows();
        }

        // Camera: look at the arena floor from a tilted, turned angle.
        private void Place()
        {
            var turn = Quaternion.Euler(0, 0, yaw) * Quaternion.Euler(-(90 - tilt), 0, 0);
            var target = new Vector3(focus.x, focus.y, 0);
            view.transform.SetPositionAndRotation(target - turn * Vector3.forward * cameraDistance, turn);
            view.orthographicSize = orthoSize;
            view.nearClipPlane = .1f;
            view.farClipPlane = cameraDistance * 3;
        }

        // One view, the 320px cabinet (centre origin, 1 unit = 1 screen pixel).
        int ICabinetProjection.Views => 1;

        bool ICabinetProjection.ToCabinet(Vector2 world, int viewIndex, out Vector2 cabinet)
        {
            Vector3 v = view.WorldToViewportPoint(new Vector3(world.x, world.y, 0));
            cabinet = new Vector2(v.x - .5f, v.y - .5f) * 320;
            return true;
        }

        // Stand a sprite up to face the camera with its foot on the floor point it was given. The arena code rewrites
        // some positions only now and then, so the lift is undone before it is applied again.
        private void Stand(Transform t, float lift, float groundOffsetY)
        {
            Vector3 raw = t.position;
            if (written.TryGetValue(t, out var last) && (last.after - raw).sqrMagnitude < 1e-8f) raw = last.raw;
            Vector3 ground = raw - new Vector3(0, groundOffsetY, 0);
            Vector3 after = ground + view.transform.up * lift;
            t.SetPositionAndRotation(after, view.transform.rotation);
            written[t] = new Written { raw = raw, after = after };
        }

        private void BuildShadows()
        {
            if (shadowSprite == null) shadowSprite = MakeEllipse();
            foreach (var car in game.cars) AddShadow(car.transform, car.body, carShadow);
            foreach (var prop in game.props) AddShadow(prop.view, prop.view.GetComponentInChildren<SpriteRenderer>(), standShadow);
            foreach (var pickup in game.pickups) AddShadow(pickup.view, pickup.view.GetComponentInChildren<SpriteRenderer>(), standShadow);
        }

        private void AddShadow(Transform follow, SpriteRenderer reference, Vector2 size)
        {
            if (follow == null) return;
            var go = new GameObject("Iso shadow") { hideFlags = HideFlags.DontSave };
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = shadowSprite;
            renderer.color = Color.black;
            if (reference != null)
            {
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder - 1;
            }
            shadows.Add(new Shadow { renderer = renderer, follow = follow, reference = reference, size = size });
        }

        private void UpdateShadows()
        {
            foreach (var shadow in shadows)
            {
                var follow = shadow.follow;
                bool show = follow != null && follow.gameObject.activeInHierarchy;
                shadow.renderer.enabled = show;
                if (!show) continue;
                // A standing sprite's ground point is the position before its lift.
                Vector3 ground = follow.position;
                if (written.TryGetValue(follow, out var last) && (last.after - ground).sqrMagnitude < 1e-8f) ground = last.raw;
                var t = shadow.renderer.transform;
                t.position = new Vector3(ground.x, ground.y, 0);
                t.rotation = Quaternion.identity;
                t.localScale = new Vector3(shadow.size.x * 2, shadow.size.y * 2, 1);
            }
        }

        // A 16x8 hard-edged black ellipse (one pixel = 1/32 unit, so it scales to the sizes above).
        private static Sprite MakeEllipse()
        {
            var tex = new Texture2D(16, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x + .5f - 8) / 8, dy = (y + .5f - 4) / 4;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= 1 ? Color.white : Color.clear);
                }
            tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            return Sprite.Create(tex, new Rect(0, 0, 16, 8), new Vector2(.5f, .5f), 16);
        }
    }
}
