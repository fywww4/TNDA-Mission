var game = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Capture title in Edit Mode.");
var camera = game.sceneCamera;
var canvas = game.GetComponentInChildren<UnityEngine.Canvas>();
var pixels = camera.GetComponent<UnityEngine.U2D.PixelPerfectCamera>();
var canvasMode = canvas.renderMode;
var canvasCamera = canvas.worldCamera;
float canvasDistance = canvas.planeDistance;
bool pixelsEnabled = pixels.enabled;
var previousTarget = camera.targetTexture;
var previousActive = UnityEngine.RenderTexture.active;
int previousMask = camera.cullingMask;
var transforms = game.GetComponentsInChildren<UnityEngine.Transform>(true);
var layers = transforms.Select(t => t.gameObject.layer).ToArray();
var rt = new UnityEngine.RenderTexture(320, 320, 24);
var texture = new UnityEngine.Texture2D(320, 320, UnityEngine.TextureFormat.RGB24, false);
try {
    for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = 31;
    camera.cullingMask = 1 << 31;
    pixels.enabled = false;
    canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = camera;
    canvas.planeDistance = 1;
    camera.targetTexture = rt;
    game.PreparePreview();
    UnityEngine.Canvas.ForceUpdateCanvases();
    camera.Render();
    UnityEngine.RenderTexture.active = rt;
    texture.ReadPixels(new UnityEngine.Rect(0, 0, 320, 320), 0, 0);
    texture.Apply();
    System.IO.File.WriteAllBytes("Docs/Game3/integrated-title.png", texture.EncodeToPNG());
} finally {
    camera.targetTexture = previousTarget;
    UnityEngine.RenderTexture.active = previousActive;
    canvas.renderMode = canvasMode;
    canvas.worldCamera = canvasCamera;
    canvas.planeDistance = canvasDistance;
    pixels.enabled = pixelsEnabled;
    camera.cullingMask = previousMask;
    for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
    UnityEngine.Object.DestroyImmediate(texture);
    rt.Release();
    UnityEngine.Object.DestroyImmediate(rt);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(game.gameObject.scene);
}
return new { path = "Docs/Game3/integrated-title.png", sceneSaved = !game.gameObject.scene.isDirty,
    missingScripts = game.GetComponentsInChildren<UnityEngine.Transform>(true).Sum(t => UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
    references = new { cars = game.cars.All(c => c.body != null), props = game.props.All(p => p.view != null),
        pickups = game.pickups.All(p => p.view != null), rockets = game.rockets.All(p => p.view != null),
        shields = game.shieldViews.All(v => v != null), leaderboard = game.leaderboard != null },
    leaderboardBounds = new { game.leaderboard.rectTransform.anchoredPosition, game.leaderboard.rectTransform.sizeDelta }
};
