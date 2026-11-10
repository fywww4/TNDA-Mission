// Render the actual HUD and its Mode7 art texture in the batch QA Editor, which has no Game View target.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var canvas = g.header.canvas;
var mode = canvas.renderMode; var worldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
var scaleMode = scaler.uiScaleMode; float factor = scaler.scaleFactor;
var cabinet = (UnityEngine.RectTransform)g.header.transform.parent; var offset = cabinet.anchoredPosition;
var transforms = canvas.GetComponentsInChildren<UnityEngine.Transform>(true);
var layers = System.Linq.Enumerable.Select(transforms, t => t.gameObject.layer).ToArray();
var go = new UnityEngine.GameObject("QA HUD capture");
var camera = go.AddComponent<UnityEngine.Camera>();
camera.orthographic = true; camera.orthographicSize = 224; camera.cullingMask = 1 << 29;
camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor; camera.backgroundColor = UnityEngine.Color.black;
camera.transform.position = new UnityEngine.Vector3(0, 0, -100);
var target = new UnityEngine.RenderTexture(512, 448, 24); camera.targetTexture = target;
var previous = UnityEngine.RenderTexture.active;
var texture = new UnityEngine.Texture2D(512, 448, UnityEngine.TextureFormat.RGB24, false);
string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../tmp"));
string state = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "ticket07-state.txt")).Trim();
string path = System.IO.Path.Combine(dir, "ticket07-" + state + "-" + g.PlayerCount + ".png");
try {
    for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = 29;
    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1;
    cabinet.anchoredPosition = UnityEngine.Vector2.zero;
    canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
    UnityEngine.Canvas.ForceUpdateCanvases(); camera.Render();
    UnityEngine.RenderTexture.active = target;
    texture.ReadPixels(new UnityEngine.Rect(0, 0, 512, 448), 0, 0); texture.Apply();
    System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
} finally {
    canvas.renderMode = mode; canvas.worldCamera = worldCamera; canvas.planeDistance = distance;
    scaler.uiScaleMode = scaleMode; scaler.scaleFactor = factor; cabinet.anchoredPosition = offset;
    for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
    UnityEngine.RenderTexture.active = previous;
    UnityEngine.Object.Destroy(texture); camera.targetTexture = null; target.Release();
    UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(go);
}
return path;
