if (UnityEditor.EditorApplication.isPlaying) return new { skipped = true, reason = "Preserve the active play session." };
const string scenePath = "Assets/Scenes/GAME3.unity";
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
bool openedHere = !scene.isLoaded;
if (openedHere) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
var g = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TNDA.Prototype.DemolitionGame>(true)).Single();
var canvas = g.GetComponentInChildren<UnityEngine.Canvas>();
var camera = g.sceneCamera;
var pixels = camera.GetComponent<UnityEngine.U2D.PixelPerfectCamera>();
var mode = canvas.renderMode;
var canvasCamera = canvas.worldCamera;
float distance = canvas.planeDistance, duration = g.roundSeconds;
bool pixelEnabled = pixels.enabled;
var originalTarget = camera.targetTexture;
var originalActive = UnityEngine.RenderTexture.active;
int originalCulling = camera.cullingMask;
var objects = g.GetComponentsInChildren<UnityEngine.Transform>(true);
var layers = objects.Select(t => t.gameObject.layer).ToArray();
bool hadBest = UnityEngine.PlayerPrefs.HasKey("WreckBest");
int best = UnityEngine.PlayerPrefs.GetInt("WreckBest", 0);
var rt = new UnityEngine.RenderTexture(320, 320, 24);
var texture = new UnityEngine.Texture2D(320, 320, UnityEngine.TextureFormat.RGB24, false);
var idle = new TNDA.Prototype.ArcadeInput();
var report = new System.Collections.Generic.Dictionary<string, object>();
System.IO.Directory.CreateDirectory("Docs/Game3");
System.Action<string> capture = file => {
    UnityEngine.Canvas.ForceUpdateCanvases();
    camera.targetTexture = rt;
    camera.Render();
    UnityEngine.RenderTexture.active = rt;
    texture.ReadPixels(new UnityEngine.Rect(0, 0, 320, 320), 0, 0);
    texture.Apply();
    System.IO.File.WriteAllBytes(file, texture.EncodeToPNG());
};
System.Action isolateRivals = () => {
    for (int i = 1; i < g.cars.Length; i++) {
        g.cars[i].alive = false;
        g.cars[i].respawn = 1000;
        g.cars[i].gameObject.SetActive(false);
    }
};
System.Func<float, float, float> impact = (victimHeading, speed) => {
    g.StartRun(1);
    isolateRivals();
    var a = g.cars[0]; var b = g.cars[1];
    a.position = new UnityEngine.Vector2(-.3f, 0); a.heading = 0; a.speed = speed;
    b.alive = true; b.gameObject.SetActive(true); b.position = new UnityEngine.Vector2(.3f, 0);
    b.heading = victimHeading; b.speed = 0; b.stun = 1;
    g.Advance(.02f, new TNDA.Prototype.ArcadeInput { throttle = 1 }, idle);
    return a.score;
};
try {
    for (int i = 0; i < objects.Length; i++) objects[i].gameObject.layer = 31;
    camera.cullingMask = 1 << 31;
    pixels.enabled = false;
    canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = camera;
    canvas.planeDistance = 1;
    g.PreparePreview();
    capture("Docs/Game3/title.png");
    g.StartRun(1);
    report["singlePlayerRivals"] = g.cars.Count(c => c.computer);
    g.StartRun(2);
    report["twoPlayerRivals"] = g.cars.Count(c => c.computer);
    for (int i = 0; i < 100; i++) g.Advance(.02f,
        new TNDA.Prototype.ArcadeInput { throttle = 1, action = true },
        new TNDA.Prototype.ArcadeInput { throttle = 1, steer = -.35f, action = true });
    report["ramActivated"] = g.cars[0].cooldown > 0;
    capture("Docs/Game3/gameplay.png");

    g.StartRun(1); isolateRivals();
    g.cars[0].position = g.props[0].point - UnityEngine.Vector2.right * .62f;
    g.cars[0].heading = 0; g.cars[0].speed = g.driveSpeed;
    g.Advance(.02f, new TNDA.Prototype.ArcadeInput { throttle = 1 }, idle);
    report["propDestroyed"] = g.props[0].broken && !g.props[0].view.gameObject.activeSelf;
    report["propPoints"] = g.cars[0].score;
    float normal = impact(180, 2.6f), side = impact(90, 2.6f), rear = impact(0, 2.6f), fast = impact(180, 4.29f);
    report["headOnPoints"] = normal; report["sidePoints"] = side;
    report["rearPoints"] = rear; report["fastPoints"] = fast;
    g.StartRun(1); isolateRivals();
    var attacker = g.cars[0]; var victim = g.cars[1];
    attacker.position = new UnityEngine.Vector2(-.3f, 0); attacker.heading = 0; attacker.speed = 2.6f;
    victim.alive = true; victim.gameObject.SetActive(true); victim.position = new UnityEngine.Vector2(.3f, 0);
    victim.heading = 180; victim.health = .05f; victim.stun = 1;
    g.Advance(.02f, new TNDA.Prototype.ArcadeInput { throttle = 1 }, idle);
    report["wreckAwarded"] = !victim.alive && attacker.score >= 500 && g.Wrecks == 1;
    victim.respawn = .01f;
    g.Advance(.02f, idle, idle);
    report["enemyRespawned"] = victim.alive && victim.health > 0;

    g.StartRun(1); g.DamageVehicle(g.cars[0], 1); g.Advance(.02f, idle, idle);
    report["singleDeathEndsRound"] = g.Phase == TNDA.Prototype.ArcadePhase.Results && !g.Survived;
    capture("Docs/Game3/gameover.png");
    g.StartRun(2); g.DamageVehicle(g.cars[0], 1); g.Advance(.02f, idle, idle);
    report["onePlayerCanContinue"] = g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.LivePlayers == 1;
    g.DamageVehicle(g.cars[1], 1); g.Advance(.02f, idle, idle);
    report["bothDeathsEndRound"] = g.Phase == TNDA.Prototype.ArcadePhase.Results;
    g.StartRun(2); g.roundSeconds = .1f;
    for (int i = 0; i < 6; i++) g.Advance(.02f, idle, idle);
    report["timerEndsWithSurvival"] = g.Phase == TNDA.Prototype.ArcadePhase.Results && g.Survived;
    var frozen = g.cars[0].position;
    g.Advance(.2f, idle, idle);
    report["resultsFreezeSimulation"] = g.cars[0].position == frozen;
    g.Advance(g.presentationSeconds + .1f, idle, idle);
    report["returnsToTitle"] = g.Phase == TNDA.Prototype.ArcadePhase.Attract;
    report["activityCap"] = g.cars.Length + g.props.Length;
}
finally {
    g.roundSeconds = duration;
    for (int i = 0; i < objects.Length; i++) objects[i].gameObject.layer = layers[i];
    camera.cullingMask = originalCulling;
    canvas.renderMode = mode;
    canvas.worldCamera = canvasCamera;
    canvas.planeDistance = distance;
    pixels.enabled = pixelEnabled;
    camera.targetTexture = originalTarget;
    UnityEngine.RenderTexture.active = originalActive;
    rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
    if (hadBest) UnityEngine.PlayerPrefs.SetInt("WreckBest", best); else UnityEngine.PlayerPrefs.DeleteKey("WreckBest");
    g.PreparePreview();
    UnityEditor.EditorUtility.SetDirty(g);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    if (openedHere) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
}
return report;
