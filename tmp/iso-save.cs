var s = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.IsoStage>();
s.yaw = 30; s.tilt = 50; s.orthoSize = 6.2f;
UnityEditor.EditorUtility.SetDirty(s);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s.gameObject.scene);
return "saved " + s.gameObject.scene.path;
