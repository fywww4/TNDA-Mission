var s = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Stage>();
s.behind = 3.2f; s.height = 1.3f;
UnityEditor.EditorUtility.SetDirty(s);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s.gameObject.scene);
return "saved " + s.gameObject.scene.path;
