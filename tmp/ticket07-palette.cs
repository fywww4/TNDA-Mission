var stage = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Stage>();
var game = stage.game;
game.StartRun(2); game.enabled = false;
var root = new UnityEngine.GameObject("QA seat palette");
var camera = root.AddComponent<UnityEngine.Camera>();
camera.orthographic = true; camera.orthographicSize = 2; camera.cullingMask = 1 << 29;
camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor; camera.backgroundColor = new UnityEngine.Color32(40, 64, 152, 255);
camera.transform.position = new UnityEngine.Vector3(0, 0, -10);
var target = new UnityEngine.RenderTexture(512, 128, 24); camera.targetTexture = target;
var texture = new UnityEngine.Texture2D(512, 128, UnityEngine.TextureFormat.RGB24, false);
var previous = UnityEngine.RenderTexture.active;
try {
    for (int i = 0; i < 8; i++) {
        var car = game.cars[i];
        var block = new UnityEngine.MaterialPropertyBlock(); car.body.GetPropertyBlock(block);
        foreach (bool map in new[] { false, true }) {
            var go = new UnityEngine.GameObject("P" + (i + 1)); go.layer = 29; go.transform.SetParent(root.transform, false);
            go.transform.position = new UnityEngine.Vector3((i - 3.5f) * 2, map ? -1.25f : -.2f, 0);
            var renderer = go.AddComponent<UnityEngine.SpriteRenderer>();
            var blip = car.transform.Find("Minimap blip").GetComponent<UnityEngine.SpriteRenderer>();
            renderer.sprite = map ? blip.sprite : stage.carFrames[0];
            renderer.sharedMaterial = map ? blip.sharedMaterial : stage.carSheetMaterial;
            renderer.color = car.body.color; renderer.SetPropertyBlock(block);
            if (map) go.transform.localScale = UnityEngine.Vector3.one * .5f;
        }
    }
    camera.Render(); UnityEngine.RenderTexture.active = target;
    texture.ReadPixels(new UnityEngine.Rect(0, 0, 512, 128), 0, 0); texture.Apply();
    string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../tmp/ticket07-palettes.png"));
    System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); return path;
} finally {
    UnityEngine.RenderTexture.active = previous; camera.targetTexture = null; target.Release();
    UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(root);
}
