UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GAME3_Mode7.unity");
UnityEditor.PlayerSettings.productName = "TNDA Ticket11 QA 20261110";
UnityEngine.PlayerPrefs.DeleteAll();
UnityEngine.PlayerPrefs.SetInt("TNDA.Gold", 149);
UnityEngine.PlayerPrefs.Save();
return new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, UnityEditor.PlayerSettings.productName };
