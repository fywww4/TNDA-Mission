var scenes = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i)).ToArray();
var g3 = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GAME3.unity");
if (!g3.isLoaded) g3 = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GAME3.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
var game = g3.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TNDA.Prototype.DemolitionGame>(true)).Single();
return new {
    loaded = scenes.Select(s => new { s.path, s.isDirty, roots = s.GetRootGameObjects().Select(r => r.name).ToArray() }).ToArray(),
    build = EditorBuildSettings.scenes.Select(s => new { s.path, s.enabled }).ToArray(),
    sampleDependencies = AssetDatabase.GetDependencies("Assets/Scenes/SampleScene.unity", true).Where(p => p.StartsWith("Assets/Prototype")).ToArray(),
    game3 = new { game.roundSeconds, game.presentationSeconds, cars = game.cars.Length, props = game.props.Length,
        game.cabinetTitle, game.instructions, fonts = game.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => new { t.name, t.fontSize, text = t.text }).ToArray() }
};
