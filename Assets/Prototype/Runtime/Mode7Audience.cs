using UnityEngine;

namespace TNDA.Prototype
{
    // Fixed upright walls, rather than camera-facing billboards: both split-screen views see the same stands.
    // Only the chase cameras draw this layer. There are no colliders or match entities here.
    [DefaultExecutionOrder(110)]
    public sealed class Mode7Audience : MonoBehaviour
    {
        public Mode7Stage stage;
        public Material fenceMaterial, crowdMaterial;
        public float fenceHeight = 1.2f, crowdHeight = 2;
        public float crowdSetback = .45f;
        public float cheerSeconds = 1;

        private GameObject boundary;
        private Material animatedCrowd;
        private Mesh fenceMesh, crowdMesh;
        private float cheerLeft, animationAge, lastElapsed;
        private DemolitionGame game;

        private void OnEnable()
        {
            if (stage == null || stage.game == null || fenceMaterial == null || crowdMaterial == null) return;
            game = stage.game;
            animatedCrowd = new Material(crowdMaterial) { hideFlags = HideFlags.DontSave };
            boundary = new GameObject("Mode7 audience boundary") { hideFlags = HideFlags.DontSave };
            boundary.transform.SetParent(transform, false);
            Vector2 min = game.arenaMin - Vector2.one * stage.arenaMargin;
            Vector2 max = game.arenaMax + Vector2.one * stage.arenaMargin;
            fenceMesh = MakeRing("Fence", min, max, fenceHeight, fenceHeight * 32 / 48, fenceMaterial);
            crowdMesh = MakeRing("Crowd", min - Vector2.one * crowdSetback, max + Vector2.one * crowdSetback,
                crowdHeight, crowdHeight * 2, animatedCrowd);
            game.WreckPresented += Cheer;
        }

        private void OnDisable()
        {
            if (game != null) game.WreckPresented -= Cheer;
            if (boundary != null) Destroy(boundary);
            if (animatedCrowd != null) Destroy(animatedCrowd);
            if (fenceMesh != null) Destroy(fenceMesh);
            if (crowdMesh != null) Destroy(crowdMesh);
            game = null;
            cheerLeft = animationAge = 0;
        }

        private void Cheer() { cheerLeft = cheerSeconds; animationAge = 0; }

        private void LateUpdate()
        {
            if (animatedCrowd == null) return;
            if (game.Elapsed < lastElapsed) cheerLeft = animationAge = 0;
            lastElapsed = game.Elapsed;
            animationAge += Time.unscaledDeltaTime;
            // Normal: seated <-> a few arms up. Wreck: the whole crowd repeatedly raises its arms.
            bool cheering = cheerLeft > 0;
            int beat = (int)(animationAge * 10) % 4;
            int frame = cheering ? (beat == 0 || beat == 3 ? 1 : 2) : (int)(animationAge * 3) % 2;
            animatedCrowd.SetFloat("_Frame", frame);
            animatedCrowd.SetFloat("_Bob", cheering ? (beat == 1 || beat == 2 ? 2 : 0) : frame);
            cheerLeft = Mathf.Max(0, cheerLeft - Time.unscaledDeltaTime);
        }

        private Mesh MakeRing(string name, Vector2 min, Vector2 max, float height, float tileWidth, Material material)
        {
            var corners = new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
            var vertices = new Vector3[16];
            var uv = new Vector2[16];
            var triangles = new int[24];
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[(side + 1) % 4];
                int v = side * 4, t = side * 6;
                vertices[v] = a; vertices[v + 1] = b;
                vertices[v + 2] = new Vector3(b.x, b.y, -height);
                vertices[v + 3] = new Vector3(a.x, a.y, -height);
                // Whole repeats at each corner keep the posts and guardrails joined.
                float repeats = Mathf.Max(1, Mathf.Round(Vector2.Distance(a, b) / tileWidth));
                uv[v] = Vector2.zero; uv[v + 1] = new Vector2(repeats, 0);
                uv[v + 2] = new Vector2(repeats, 1); uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Mode7 " + name, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            var go = new GameObject(name) { layer = stage.chaseOnlyLayer };
            go.transform.SetParent(boundary.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }
    }
}
