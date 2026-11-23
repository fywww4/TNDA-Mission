using System.Collections.Generic;
using UnityEngine;

namespace TNDA.Prototype
{
    // Fixed world meshes shared by both chase cameras, separate floor footprints for the minimap. No billboards.
    [DefaultExecutionOrder(120)]
    public sealed class Mode7Obstacles : MonoBehaviour
    {
        public Mode7Stage stage;
        // ObstacleKind order: fence, pipe, wall, ramp, then the night derby's loader and hulk (ticket 25; until those two
        // are wired the wall's and pipe's stand in). Scene references keep shaders/textures in builds.
        public Material[] materials;
        private GameObject root, standardTerrain, derbyTerrain;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<(ArenaObstacle obstacle, MeshRenderer view, MeshRenderer map)> views = new List<(ArenaObstacle, MeshRenderer, MeshRenderer)>();
        private readonly List<(ArcadeVehicle car, Transform shadow)> shadows = new List<(ArcadeVehicle, Transform)>();
        private readonly List<Fragment> fragments = new List<Fragment>();
        private Material flatMaterial;
        private MaterialPropertyBlock block;
        private float lastElapsed;

        private sealed class Fragment
        {
            public Transform view;
            public Vector3 velocity;
            public float left;
        }

        private void OnEnable()
        {
            if (stage == null || materials == null || materials.Length < 4) return;
            block = new MaterialPropertyBlock();
            root = new GameObject("Mode7 fixed terrain") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            flatMaterial = new Material(materials[0]) { name = "Terrain solid colours", hideFlags = HideFlags.DontSave };
            flatMaterial.mainTexture = Texture2D.whiteTexture;
            // The usual terrain, and the night derby's (ticket 25); LateUpdate shows the set the match is using.
            standardTerrain = Group("Standard terrain");
            derbyTerrain = Group("Derby terrain");
            foreach (var obstacle in stage.game.obstacles) AddObstacle(obstacle, standardTerrain.transform);
            foreach (var obstacle in stage.game.derbyObstacles) AddObstacle(obstacle, derbyTerrain.transform);
            foreach (var car in stage.game.cars)
            {
                var shadow = MakeRenderer("Jump shadow " + car.index, Disc(), flatMaterial, stage.chaseOnlyLayer);
                Tint(shadow, new Color32(55, 55, 55, 255));
                shadows.Add((car, shadow.transform));
            }
            for (int i = 0; i < 24; i++)
            {
                var piece = MakeRenderer("Fence wood fragment", Rectangle(new Vector2(.12f, .23f), 0), flatMaterial, stage.chaseOnlyLayer);
                Tint(piece, i % 2 == 0 ? new Color32(138, 90, 43, 255) : new Color32(184, 130, 75, 255));
                piece.gameObject.SetActive(false);
                fragments.Add(new Fragment { view = piece.transform });
            }
            stage.game.FencePresented += Scatter;
        }

        private void OnDisable()
        {
            if (stage != null && stage.game != null) stage.game.FencePresented -= Scatter;
            if (root != null) Destroy(root);
            foreach (var mesh in meshes) Destroy(mesh);
            if (flatMaterial != null) Destroy(flatMaterial);
            meshes.Clear(); views.Clear(); shadows.Clear(); fragments.Clear();
        }

        private GameObject Group(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root.transform, false);
            return go;
        }

        private void AddObstacle(ArenaObstacle obstacle, Transform parent)
        {
            MeshRenderer view = MakeObstacle(obstacle, parent);
            Color tint = obstacle.kind == ObstacleKind.Fence ? new Color32(138, 90, 43, 255)
                : obstacle.kind == ObstacleKind.Ramp ? new Color32(255, 212, 72, 255)
                : obstacle.kind == ObstacleKind.Loader ? new Color32(248, 192, 0, 255)
                : obstacle.kind == ObstacleKind.Hulk ? new Color32(80, 80, 80, 255)
                : obstacle.kind == ObstacleKind.Pipe ? new Color32(184, 184, 184, 255) : new Color32(150, 158, 160, 255);
            var map = MakeRenderer(obstacle.kind + " map footprint", Rectangle(obstacle.size, -.02f), flatMaterial, stage.minimapOnlyLayer, parent);
            Place(map.transform, obstacle);
            Tint(map, tint);
            views.Add((obstacle, view, map));
        }

        // The loader's and hulk's own material, or the wall's and pipe's until they are wired.
        private Material MaterialFor(ObstacleKind kind)
        {
            int i = (int)kind;
            if (i < materials.Length && materials[i] != null) return materials[i];
            return materials[kind == ObstacleKind.Loader ? (int)ObstacleKind.Wall : kind == ObstacleKind.Hulk ? (int)ObstacleKind.Pipe : i];
        }

        private void LateUpdate()
        {
            if (root == null) return;
            // The campaign's training ground has no terrain (the match leaves it out too).
            root.SetActive(!stage.game.TrainingGround);
            standardTerrain.SetActive(stage.game.Arena == CampaignArena.Standard);
            derbyTerrain.SetActive(stage.game.Arena == CampaignArena.Derby);
            bool reset = stage.game.Elapsed < lastElapsed;
            lastElapsed = stage.game.Elapsed;
            foreach (var entry in views)
            {
                block.Clear(); block.SetFloat("_Frame", entry.obstacle.broken ? 1 : 0);
                entry.view.SetPropertyBlock(block);
                entry.map.enabled = !entry.obstacle.broken;
            }
            foreach (var shadow in shadows)
            {
                shadow.shadow.gameObject.SetActive(shadow.car.gameObject.activeInHierarchy && shadow.car.Airborne);
                shadow.shadow.position = new Vector3(shadow.car.position.x, shadow.car.position.y, -.025f);
                shadow.shadow.rotation = Quaternion.Euler(0, 0, shadow.car.heading - 90);
            }
            foreach (var fragment in fragments)
            {
                if (reset) fragment.left = 0;
                fragment.left -= Time.deltaTime;
                fragment.view.gameObject.SetActive(fragment.left > 0);
                if (fragment.left <= 0) continue;
                fragment.velocity += Vector3.forward * (5 * Time.deltaTime);
                fragment.view.position += fragment.velocity * Time.deltaTime;
                fragment.view.Rotate(150 * Time.deltaTime, 90 * Time.deltaTime, 330 * Time.deltaTime);
            }
        }

        private void Scatter(ArenaObstacle obstacle)
        {
            if (stage.game.Elapsed < lastElapsed)
                foreach (var old in fragments) old.left = 0;
            lastElapsed = stage.game.Elapsed;
            int spawned = 0;
            foreach (var fragment in fragments)
            {
                if (fragment.left > 0) continue;
                float along = (spawned / 7f - .5f) * obstacle.size.x;
                Vector2 p = obstacle.ToWorld(new Vector2(along, 0));
                fragment.view.position = new Vector3(p.x, p.y, -obstacle.height * .6f);
                fragment.view.rotation = Quaternion.Euler(90, 0, spawned * 40);
                // Deterministic scatter doesn't consume the match's random stream.
                float angle = (spawned * 137 + 25) * Mathf.Deg2Rad;
                fragment.velocity = new Vector3(Mathf.Cos(angle) * 1.7f, Mathf.Sin(angle) * 1.7f, -1.2f - spawned * .08f);
                fragment.left = .65f;
                if (++spawned == 8) break;
            }
        }

        private MeshRenderer MakeObstacle(ArenaObstacle o, Transform parent)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float w = o.size.x / 2, d = o.size.y / 2, h = o.height;
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 e, int frame = 0, float top = 1)
            {
                int at = vertices.Count;
                vertices.AddRange(new[] { a, b, c, e });
                float left = frame * .5f + .5f / 64, right = (frame + 1) * .5f - .5f / 64;
                uv.AddRange(new[] { new Vector2(left, .5f / 32), new Vector2(right, .5f / 32),
                    new Vector2(right, top - .5f / 32), new Vector2(left, top - .5f / 32) });
                triangles.AddRange(new[] { at, at + 1, at + 2, at, at + 2, at + 3 });
            }
            if (o.kind == ObstacleKind.Loader || o.kind == ObstacleKind.Hulk)
            {
                // Two cells: the side view (nose to the left, at local -x) on both long sides, the front view on both
                // ends. Every face shows the same pixels per world unit: the quads are as tall as a cell is for the
                // obstacle's length, and the ends show only the middle of the front cell. The art's clear part is cut out.
                var texture = MaterialFor(o.kind).mainTexture;
                float tall = texture != null ? o.size.x * texture.height / (texture.width * .5f) : h;
                float crop = Mathf.Clamp01(o.size.y / o.size.x) * .25f;
                void Quad(Vector3 a, Vector3 b, float u0, float u1)
                {
                    int at = vertices.Count;
                    vertices.AddRange(new[] { a, b, new Vector3(b.x, b.y, -tall), new Vector3(a.x, a.y, -tall) });
                    uv.AddRange(new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1) });
                    triangles.AddRange(new[] { at, at + 1, at + 2, at, at + 2, at + 3 });
                }
                Quad(new Vector3(-w, -d, 0), new Vector3(w, -d, 0), 0, .5f);
                Quad(new Vector3(-w, d, 0), new Vector3(w, d, 0), 0, .5f);
                Quad(new Vector3(-w, d, 0), new Vector3(-w, -d, 0), .75f - crop, .75f + crop);
                Quad(new Vector3(w, -d, 0), new Vector3(w, d, 0), .75f - crop, .75f + crop);
            }
            else if (o.kind == ObstacleKind.Ramp)
            {
                Face(new Vector3(-w, -d, -.01f), new Vector3(w, -d, -.01f), new Vector3(w, d, -h), new Vector3(-w, d, -h));
                // The supplied right-hand tile is a triangular truss, rising from left to right.
                Face(new Vector3(-w, -d, 0), new Vector3(-w, d, 0), new Vector3(-w, d, -h * 1.6f), new Vector3(-w, -d, -h * 1.6f), 1);
                Face(new Vector3(w, -d, 0), new Vector3(w, d, 0), new Vector3(w, d, -h * 1.6f), new Vector3(w, -d, -h * 1.6f), 1);
                Face(new Vector3(w, d, 0), new Vector3(-w, d, 0), new Vector3(-w, d, -h), new Vector3(w, d, -h));
            }
            else if (o.kind == ObstacleKind.Pipe)
            {
                // Twelve sides make a low-poly tube; end-face tiles retain the dark open pipe mouth.
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
                    var p = new Vector2(Mathf.Cos(a) * d, -(1 + Mathf.Sin(a)) * h / 2);
                    var q = new Vector2(Mathf.Cos(b) * d, -(1 + Mathf.Sin(b)) * h / 2);
                    int at = vertices.Count;
                    Face(new Vector3(-w, p.x, p.y), new Vector3(w, p.x, p.y), new Vector3(w, q.x, q.y), new Vector3(-w, q.x, q.y));
                    for (int j = 0; j < 4; j++) uv[at + j] = new Vector2(uv[at + j].x, .02f - (j < 2 ? p.y : q.y) / h * .68f);
                }
                Face(new Vector3(-w, -d, 0), new Vector3(-w, d, 0), new Vector3(-w, d, -h), new Vector3(-w, -d, -h), 1);
                Face(new Vector3(w, -d, 0), new Vector3(w, d, 0), new Vector3(w, d, -h), new Vector3(w, -d, -h), 1);
            }
            else
            {
                int repeats = Mathf.Max(1, Mathf.RoundToInt(o.size.x / h));
                for (int i = 0; i < repeats; i++)
                {
                    float a = Mathf.Lerp(-w, w, i / (float)repeats), b = Mathf.Lerp(-w, w, (i + 1f) / repeats);
                    Face(new Vector3(a, -d, 0), new Vector3(b, -d, 0), new Vector3(b, -d, -h), new Vector3(a, -d, -h));
                    Face(new Vector3(b, d, 0), new Vector3(a, d, 0), new Vector3(a, d, -h), new Vector3(b, d, -h));
                }
                if (o.kind == ObstacleKind.Wall)
                {
                    Face(new Vector3(-w, -d, 0), new Vector3(-w, d, 0), new Vector3(-w, d, -h), new Vector3(-w, -d, -h));
                    Face(new Vector3(w, -d, 0), new Vector3(w, d, 0), new Vector3(w, d, -h), new Vector3(w, -d, -h));
                    Face(new Vector3(-w, -d, -h), new Vector3(w, -d, -h), new Vector3(w, d, -h), new Vector3(-w, d, -h), 1);
                }
            }
            var mesh = new Mesh { name = o.kind + " fixed mesh", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var renderer = MakeRenderer(o.kind + " fixed surface", mesh, MaterialFor(o.kind), stage.chaseOnlyLayer, parent);
            Place(renderer.transform, o);
            return renderer;
        }

        private static void Place(Transform t, ArenaObstacle o) =>
            t.SetPositionAndRotation(o.point, Quaternion.Euler(0, 0, o.angle));

        private void Tint(MeshRenderer renderer, Color tint)
        {
            block.Clear(); block.SetColor("_Color", tint); renderer.SetPropertyBlock(block);
        }

        private MeshRenderer MakeRenderer(string name, Mesh mesh, Material material, int layer, Transform parent = null)
        {
            meshes.Add(mesh);
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent != null ? parent : root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Mesh Rectangle(Vector2 size, float z)
        {
            Vector2 h = size / 2;
            return new Mesh { hideFlags = HideFlags.DontSave, vertices = new[] { new Vector3(-h.x, -h.y, z), new Vector3(h.x, -h.y, z),
                new Vector3(h.x, h.y, z), new Vector3(-h.x, h.y, z) }, uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 1, 2, 0, 2, 3 } };
        }

        private static Mesh Disc()
        {
            var vertices = new Vector3[17]; var triangles = new int[48];
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * .28f, Mathf.Sin(a) * .45f, 0);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 16 + 1;
            }
            return new Mesh { hideFlags = HideFlags.DontSave, vertices = vertices, uv = new Vector2[17], triangles = triangles };
        }
    }
}
