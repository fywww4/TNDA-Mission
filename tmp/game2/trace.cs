var path = "Assets/Scenes/GAME2.unity";
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
TNDA.Prototype.EliminationRaceGame g = null;
foreach (var r in scene.GetRootGameObjects()) if (r.name == "GAME2 Prototype") g = r.GetComponent<TNDA.Prototype.EliminationRaceGame>();
typeof(TNDA.Prototype.ArcadeGame).GetField("sounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, null);
var log = new System.Text.StringBuilder(); float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput();
try {
g.StartRun(1);
for (int f=0; f<60*12; f++) {
  g.Advance(dt, none, none);
  if (f % 60 == 0) { log.Append("t="+g.Elapsed.ToString("0")+" "); foreach (var c in g.cars) log.Append(c.DriverName+"("+c.position.x.ToString("0.0")+","+c.position.y.ToString("0.0")+" h"+c.heading.ToString("0")+" v"+c.speed.ToString("0.0")+" s"+c.score+" st"+c.stun.ToString("0.0")+") "); log.Append("\n"); }
}
} catch (System.Exception e) { log.Append(e.ToString()); }
g.PreparePreview();
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
return log.ToString();
