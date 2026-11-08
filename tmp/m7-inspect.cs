var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GAME3_Mode7.unity");
var sb = new System.Text.StringBuilder();
foreach (var r in scene.GetRootGameObjects()) {
  sb.AppendLine("ROOT " + r.name + " pos=" + r.transform.position + " scale=" + r.transform.localScale);
  foreach (UnityEngine.Transform c in r.transform) sb.AppendLine("   " + c.name + " pos=" + c.position + " scale=" + c.localScale + " kids=" + c.childCount);
}
var g = UnityEngine.Object.FindFirstObjectByType<TNDA.Prototype.DemolitionGame>();
sb.AppendLine("cam " + g.sceneCamera.transform.position + " size=" + g.sceneCamera.orthographicSize);
foreach (var p in g.props) sb.AppendLine("prop " + p.view.name + " " + p.point + " parent=" + p.view.parent.name + " pts=" + p.points);
foreach (var p in g.pickups) sb.AppendLine("pickup " + p.view.name + " " + p.point + " parent=" + p.view.parent.name);
sb.AppendLine("arena " + g.arenaMin + " " + g.arenaMax);
return sb.ToString();
