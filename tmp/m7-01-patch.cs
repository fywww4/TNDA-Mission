// Ticket 01: lay out the GAME3_Mode7 HUD for the 512x448 cabinet and save the scene.
var sc = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath("Assets/Scenes/GAME3_Mode7.unity");
if (sc.isLoaded && sc.isDirty) return "scene has unsaved changes; not patching";
return TNDA.Prototype.Editor.Mode7Layout.PatchScene();
