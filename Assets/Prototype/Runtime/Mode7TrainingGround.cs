using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // Campaign stage 1's black-box training ground in the chase views (ticket 15): a gridded black floor and four dark
    // walls instead of the audience, shown only while DemolitionGame.TrainingGround (the match leaves out the terrain
    // and spike balls; Mode7Audience, Mode7Obstacles and Mode7Stage hide their own parts). Chase cameras only.
    [DefaultExecutionOrder(115)]
    public sealed class Mode7TrainingGround : MonoBehaviour
    {
        public Mode7Stage stage;
        [Tooltip("TrainingFloor.png: 32x32, seamless, one tile per world unit. Empty = a code-drawn grid.")]
        public Texture2D floorTile;
        [Tooltip("TrainingWall.png: 32x48, seamless left to right. Empty = code-drawn panels.")]
        public Texture2D wallTile;
        [Tooltip("TrainingWall.mat (TNDA/Mode7 Training Wall): see-through walls that fade near the camera. Empty = opaque walls in the ground material.")]
        public Material wallMaterial;
        public float wallHeight = 1.4f;

        private GameObject root;
        private readonly List<Object> owned = new List<Object>();

        private void OnEnable()
        {
            if (stage == null || stage.game == null || stage.groundMaterial == null) return;
            var game = stage.game;
            root = new GameObject("Mode7 training ground") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            Vector2 min = game.arenaMin - Vector2.one * stage.arenaMargin, max = game.arenaMax + Vector2.one * stage.arenaMargin;
            var floor = floorTile != null ? floorTile : Own(PlaceholderFloor());
            Vector2 size = max - min;
            var floorMesh = Own(new Mesh { name = "Training floor", hideFlags = HideFlags.DontSave });
            floorMesh.vertices = new[] { new Vector3(min.x, min.y, .005f), new Vector3(max.x, min.y, .005f), new Vector3(max.x, max.y, .005f), new Vector3(min.x, max.y, .005f) };
            floorMesh.uv = new[] { Vector2.zero, new Vector2(size.x, 0), size, new Vector2(0, size.y) };
            floorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            floorMesh.RecalculateBounds();
            Show("Training floor", floorMesh, floor, stage.groundMaterial);
            var wall = wallTile != null ? wallTile : Own(PlaceholderWall());
            float tileWidth = wallHeight * wall.width / wall.height;
            Show("Training walls", Walls(min, max, tileWidth), wall, wallMaterial != null ? wallMaterial : stage.groundMaterial);
            root.SetActive(false);
        }

        private void OnDisable()
        {
            if (root != null) Destroy(root);
            foreach (var thing in owned) if (thing != null) Destroy(thing);
            owned.Clear();
        }

        private void LateUpdate()
        {
            if (root != null) root.SetActive(stage.game.TrainingGround);
        }

        private T Own<T>(T thing) where T : Object { owned.Add(thing); return thing; }

        private void Show(string name, Mesh mesh, Texture2D texture, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave, layer = stage.chaseOnlyLayer };
            go.transform.SetParent(root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Own(new Material(material) { mainTexture = texture, hideFlags = HideFlags.DontSave });
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Four upright walls round the arena facing inwards only (a chase camera backed up outside one sees through it),
        // with whole repeats of the tile on each side.
        private Mesh Walls(Vector2 min, Vector2 max, float tileWidth)
        {
            var corners = new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[(side + 1) % 4];
                int v = vertices.Count;
                float repeats = Mathf.Max(1, Mathf.Round(Vector2.Distance(a, b) / tileWidth));
                vertices.Add(new Vector3(a.x, a.y, 0)); vertices.Add(new Vector3(b.x, b.y, 0));
                vertices.Add(new Vector3(b.x, b.y, -wallHeight)); vertices.Add(new Vector3(a.x, a.y, -wallHeight));
                uv.Add(Vector2.zero); uv.Add(new Vector2(repeats, 0)); uv.Add(new Vector2(repeats, 1)); uv.Add(Vector2.up);
                triangles.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
            }
            var mesh = Own(new Mesh { name = "Training walls", hideFlags = HideFlags.DontSave });
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D NewTexture(int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };

        // Until TrainingFloor.png arrives: black with a dark grey grid line on two edges and a small cross in the middle.
        private static Texture2D PlaceholderFloor()
        {
            var tex = NewTexture(32, 32);
            Color grid = new Color32(40, 40, 48, 255), mark = new Color32(64, 64, 80, 255);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool line = x == 0 || y == 0;
                    bool cross = (x == 16 && y >= 15 && y <= 17) || (y == 16 && x >= 15 && x <= 17);
                    tex.SetPixel(x, y, line ? grid : cross ? mark : Color.black);
                }
            tex.Apply();
            return tex;
        }

        // Until TrainingWall.png arrives: dark grey panels with seams, rivets, a lighter top edge and a yellow-black
        // hazard stripe along the bottom (row 0 is the bottom).
        private static Texture2D PlaceholderWall()
        {
            var tex = NewTexture(32, 48);
            Color seam = Color.black, panel = new Color32(48, 48, 56, 255), dark = new Color32(32, 32, 40, 255);
            Color rivet = new Color32(120, 120, 128, 255), top = new Color32(72, 72, 80, 255), yellow = new Color32(248, 192, 0, 255);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color c;
                    if (y < 6) c = ((x + y) / 4) % 2 == 0 ? yellow : seam;
                    else if (y >= 46) c = top;
                    else if (y == 6 || y == 26 || x == 0) c = seam;
                    else if ((x == 2 || x == 30) && (y == 8 || y == 24 || y == 28 || y == 44)) c = rivet;
                    else c = y < 26 ? dark : panel;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }
    }
}
