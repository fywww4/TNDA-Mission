if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode before removing old games.");
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GAME3.unity");
if (!scene.isLoaded) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GAME3.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
foreach (var path in new[] { "Assets/Scenes/GAME1.unity", "Assets/Scenes/GAME2.unity" }) {
    var old = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
    if (old.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(old, true);
}
const string sources = "Assets/Prototype/Game2/Art/Source";
const string keptSources = "Assets/Prototype/Game3/Art/Source";
if (UnityEditor.AssetDatabase.IsValidFolder(sources) && !UnityEditor.AssetDatabase.IsValidFolder(keptSources))
    if (!UnityEditor.AssetDatabase.CopyAsset(sources, keptSources)) throw new System.InvalidOperationException("Could not preserve original supplied car images.");
var deleted = new System.Collections.Generic.List<string>();
var paths = new[] {
    "Assets/Scenes/GAME1.unity", "Assets/Scenes/GAME2.unity",
    "Assets/Prototype/Game1", "Assets/Prototype/Game2",
    "Assets/Prototype/Runtime/BossClimbGame.cs", "Assets/Prototype/Runtime/EliminationRaceGame.cs",
    "Assets/Prototype/Editor/Game1SceneBuilder.cs", "Assets/Prototype/Editor/Game2SceneBuilder.cs"
};
UnityEditor.AssetDatabase.StartAssetEditing();
try {
    foreach (string path in paths) {
        if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(path))) continue;
        if (!UnityEditor.AssetDatabase.DeleteAsset(path)) throw new System.InvalidOperationException("Could not delete " + path);
        deleted.Add(path);
    }
} finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
UnityEditor.EditorBuildSettings.scenes = new[] { new UnityEditor.EditorBuildSettingsScene("Assets/Scenes/GAME3.unity", true) };
UnityEditor.AssetDatabase.SaveAssets();
return new { deleted, preservedSources = keptSources, buildScene = "Assets/Scenes/GAME3.unity" };
