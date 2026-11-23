using UnityEngine;

namespace TNDA.Prototype
{
    // Campaign stage 3's night derby floor in the chase views (ticket 25): tiled dirt over the black arena floor, shown
    // only while DemolitionGame.Arena is the derby. The rest of the derby look lives with its owners: the night sky and
    // darker outside checker in Mode7Stage, the concrete barrier and night crowd in Mode7Audience, the loader and hulks
    // in Mode7Obstacles. Chase cameras only.
    [DefaultExecutionOrder(115)]
    public sealed class Mode7DerbyArena : MonoBehaviour
    {
        public Mode7Stage stage;
        [Tooltip("DerbyDirt.png: 32x32, seamless, one tile per world unit. Empty = code-drawn dirt.")]
        public Texture2D dirtTile;

        private GameObject floor;
        private Mesh mesh;
        private Material material;
        private Texture2D placeholder;

        private void OnEnable()
        {
            if (stage == null || stage.game == null || stage.groundMaterial == null) return;
            var game = stage.game;
            Vector2 min = game.arenaMin - Vector2.one * stage.arenaMargin, max = game.arenaMax + Vector2.one * stage.arenaMargin;
            Vector2 size = max - min;
            mesh = new Mesh { name = "Derby dirt", hideFlags = HideFlags.DontSave };
            // Just above the black arena floor (.01), like the training ground's.
            mesh.vertices = new[] { new Vector3(min.x, min.y, .005f), new Vector3(max.x, min.y, .005f), new Vector3(max.x, max.y, .005f), new Vector3(min.x, max.y, .005f) };
            mesh.uv = new[] { Vector2.zero, new Vector2(size.x, 0), size, new Vector2(0, size.y) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            if (dirtTile == null) placeholder = PlaceholderDirt();
            material = new Material(stage.groundMaterial) { mainTexture = dirtTile != null ? dirtTile : placeholder, hideFlags = HideFlags.DontSave };
            floor = new GameObject("Mode7 derby dirt", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave, layer = stage.chaseOnlyLayer };
            floor.transform.SetParent(transform, false);
            floor.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            floor.SetActive(false);
        }

        private void OnDisable()
        {
            if (floor != null) Destroy(floor);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (placeholder != null) Destroy(placeholder);
        }

        private void LateUpdate()
        {
            if (floor != null) floor.SetActive(stage.game.Arena == CampaignArena.Derby);
        }

        // Until DerbyDirt.png arrives: dark brown with scattered clods and dry specks (a fixed hash, so it tiles).
        private static Texture2D PlaceholderDirt()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            Color main = new Color32(96, 64, 40, 255), dark = new Color32(72, 48, 24, 255), dry = new Color32(120, 80, 48, 255);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    int h = (x * 73 + y * 151 + x * y * 7) % 23;
                    tex.SetPixel(x, y, h < 4 ? dark : h == 11 ? dry : main);
                }
            tex.Apply();
            return tex;
        }
    }
}
