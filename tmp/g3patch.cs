// CLI regression: eval_file tmp/g3patch.cs. Uses the public GAME3 patch entry point.
// The scene snapshot is restored and reloaded even when the regression fails.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
    throw new System.InvalidOperationException("Stop Play Mode before verifying scene patching.");
const string path = "Assets/Scenes/GAME3.unity";
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
bool openedHere = !scene.isLoaded;
if (openedHere) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
if (scene.isDirty) throw new System.InvalidOperationException("Save GAME3 before running the regression.");
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var buildScenes = UnityEditor.EditorBuildSettings.scenes;
string snapshot = System.IO.Path.GetFullPath(".utmp/issue08/scene-" + System.Guid.NewGuid().ToString("N") + ".unity");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(snapshot));
System.IO.File.Copy(path, snapshot);
var report = new System.Collections.Generic.Dictionary<string, object>();
var consoleIssues = new System.Collections.Generic.List<string>();
UnityEngine.Application.LogCallback onLog = (message, stack, type) =>
{
    if (type == UnityEngine.LogType.Warning || type == UnityEngine.LogType.Error || type == UnityEngine.LogType.Exception)
        consoleIssues.Add(message);
};
UnityEngine.Application.logMessageReceived += onLog;
try
{
    var game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TNDA.Prototype.DemolitionGame>(true)).Single();
    int originalObjects = game.GetComponentsInChildren<UnityEngine.Transform>(true).Length;
    var pickupViews = game.pickups.Select(p => p.view).ToArray();
    // Inspector values deliberately differ from the production defaults, including valid zero durations.
    game.firstElimination = 42;
    game.secondElimination = 77;
    game.roundSeconds = 105;
    game.presentationSeconds = 0;
    game.countdownSeconds = 0;
    game.driveSpeed = 3.25f;
    game.enemyRespawnSeconds = 4.5f;
    game.wreckPenalty = 175;
    game.wreckBonus = 425;
    game.shieldDuration = 9;
    game.pickupRespawnSeconds = 12;
    game.arenaMax = new UnityEngine.Vector2(4.1f, 1.4f);
    game.props[0].point = new UnityEngine.Vector2(-1.8f, -.65f);
    game.pickups[0].point = new UnityEngine.Vector2(-.75f, -2.25f);
    TNDA.Prototype.Editor.Game3SceneBuilder.IntegrateExisting();
    var mismatches = new System.Collections.Generic.List<string>();
    void Check(string field, bool matches) { if (!matches) mismatches.Add(field); }
    Check("firstElimination", game.firstElimination == 42);
    Check("secondElimination", game.secondElimination == 77);
    Check("roundSeconds", game.roundSeconds == 105);
    Check("presentationSeconds", game.presentationSeconds == 0);
    Check("countdownSeconds", game.countdownSeconds == 0);
    Check("driveSpeed", game.driveSpeed == 3.25f);
    Check("enemyRespawnSeconds", game.enemyRespawnSeconds == 4.5f);
    Check("wreckPenalty", game.wreckPenalty == 175);
    Check("wreckBonus", game.wreckBonus == 425);
    Check("shieldDuration", game.shieldDuration == 9);
    Check("pickupRespawnSeconds", game.pickupRespawnSeconds == 12);
    Check("arenaMax", game.arenaMax == new UnityEngine.Vector2(4.1f, 1.4f));
    Check("prop spawn", game.props[0].point == new UnityEngine.Vector2(-1.8f, -.65f));
    Check("pickup spawn", game.pickups[0].point == new UnityEngine.Vector2(-.75f, -2.25f));
    report["tuningPreserved"] = mismatches.Count == 0;
    report["resetFields"] = mismatches.ToArray();
    TNDA.Prototype.Editor.Game3SceneBuilder.IntegrateExisting();
    int patchedObjects = game.GetComponentsInChildren<UnityEngine.Transform>(true).Length;
    int itemObjects = game.transform.Find("Item spawns (2)").childCount;
    int rocketObjects = game.transform.Find("Rocket pool (2)").childCount;
    bool samePickups = game.pickups.Select(p => p.view).SequenceEqual(pickupViews);
    report["patchIsIdempotent"] = originalObjects == patchedObjects && itemObjects == 2 && rocketObjects == 2 && samePickups;
    report["inventory"] = new { originalObjects, patchedObjects, itemObjects, rocketObjects, samePickups };

    // Exercise Build against an empty prepared scene, with the original scene still backed up.
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
    var camera = new UnityEngine.GameObject("Main Camera", typeof(UnityEngine.Camera), typeof(UnityEngine.AudioListener));
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera, scene);
    TNDA.Prototype.Editor.Game3SceneBuilder.Build();
    var fresh = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TNDA.Prototype.DemolitionGame>(true)).Single();
    report["freshDefaults"] = fresh.firstElimination == 60 && fresh.secondElimination == 90 && fresh.roundSeconds == 120
        && fresh.presentationSeconds == 20 && fresh.countdownSeconds == 3 && fresh.driveSpeed == 2.8f
        && fresh.enemyRespawnSeconds == 3 && fresh.wreckPenalty == 250 && fresh.wreckBonus == 300
        && fresh.shieldDuration == 6 && fresh.pickupRespawnSeconds == 8
        && fresh.arenaMax == new UnityEngine.Vector2(4.35f, 1.65f)
        && fresh.props[0].points == 100 && fresh.props[1].points == 75
        && fresh.props[0].point == new UnityEngine.Vector2(-2, -.8f) && fresh.props[1].point == new UnityEngine.Vector2(2, -.8f)
        && fresh.pickups[0].point == new UnityEngine.Vector2(-.9f, -2.5f) && fresh.pickups[1].point == new UnityEngine.Vector2(.9f, .3f)
        && fresh.cars.Length == 4 && fresh.props.Length == 2 && fresh.pickups.Length == 2 && fresh.rockets.Length == 2;

    TNDA.Prototype.Editor.Game3SceneBuilder.IntegrateExisting();
    int freshPatchedObjects = fresh.GetComponentsInChildren<UnityEngine.Transform>(true).Length;
    TNDA.Prototype.Editor.Game3SceneBuilder.IntegrateExisting();
    report["freshPatchIsIdempotent"] = fresh.GetComponentsInChildren<UnityEngine.Transform>(true).Length == freshPatchedObjects
        && fresh.transform.Find("Item spawns (2)").childCount == 2 && fresh.transform.Find("Rocket pool (2)").childCount == 2;
}
finally
{
    UnityEngine.Application.logMessageReceived -= onLog;
    // Let EditorSceneManager restore the snapshot; the asset may still be memory mapped on Windows.
    var holding = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    string recovery = "Assets/Scenes/__Issue08Restore-" + System.Guid.NewGuid().ToString("N") + ".unity";
    System.IO.File.Copy(snapshot, recovery);
    UnityEditor.AssetDatabase.ImportAsset(recovery, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(recovery, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path))
        throw new System.InvalidOperationException("Scene snapshot remains at " + snapshot);
    UnityEditor.AssetDatabase.DeleteAsset(recovery);
    if (openedHere) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    if (active.path == path && !openedHere) UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    else if (active.IsValid() && active.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(holding, true);
    UnityEditor.EditorBuildSettings.scenes = buildScenes;
    report["sceneRestoredExactly"] = System.IO.File.ReadAllBytes(snapshot).SequenceEqual(System.IO.File.ReadAllBytes(path));
    System.IO.File.Delete(snapshot);
}
report["consoleIssues"] = consoleIssues.Distinct().ToArray();
return report;
