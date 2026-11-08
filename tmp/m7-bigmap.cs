// GAME3_Mode7: arena twice the size around its centre, 5 breakables (was 2), 4 item crates (was 2). Run once.
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GAME3_Mode7.unity");
var g = UnityEngine.Object.FindFirstObjectByType<TNDA.Prototype.DemolitionGame>();
if (g.props.Length != 2 || g.pickups.Length != 2) return "already patched: props=" + g.props.Length + " pickups=" + g.pickups.Length;
UnityEngine.Vector2 c = (g.arenaMin + g.arenaMax) / 2;
System.Func<UnityEngine.Vector2, UnityEngine.Vector2> grow = p => c + (p - c) * 2;
g.arenaMin = grow(g.arenaMin); g.arenaMax = grow(g.arenaMax);
var root = g.transform;
var yard = root.Find("Scrapyard (static)");
yard.localScale = new UnityEngine.Vector3(2, 2, 1);
yard.localPosition = new UnityEngine.Vector3(-c.x, -c.y, 0);
var cam = g.sceneCamera;
cam.transform.position = new UnityEngine.Vector3(c.x, c.y, -10);
cam.orthographicSize = 10;
// Item crates: the two old ones move out with the arena, two more in the empty side lanes.
var pickupList = new System.Collections.Generic.List<TNDA.Prototype.ArenaPickup>();
foreach (var p in g.pickups) { p.point = grow(p.point); p.view.position = TNDA.Prototype.ArcadeGame.PixelPosition(p.point); pickupList.Add(p); }
UnityEngine.Vector2[] extraPickups = { new UnityEngine.Vector2(-5.5f, .6f), new UnityEngine.Vector2(5.5f, -3.4f) };
foreach (var at in extraPickups) {
  var copy = UnityEngine.Object.Instantiate(g.pickups[0].view.gameObject, g.pickups[0].view.parent);
  copy.name = "Item crate";
  copy.transform.position = TNDA.Prototype.ArcadeGame.PixelPosition(at);
  pickupList.Add(new TNDA.Prototype.ArenaPickup { view = copy.transform, point = at, item = g.pickups[0].item });
}
g.pickups = pickupList.ToArray();
g.pickups[0].view.parent.name = "Item spawns (4)";
// Breakables: crate, barrel + crate, barrel, crate. Spots are re-rolled at random each match; these are fallbacks.
var propList = new System.Collections.Generic.List<TNDA.Prototype.BreakableProp>();
foreach (var p in g.props) { p.point = grow(p.point); p.view.position = TNDA.Prototype.ArcadeGame.PixelPosition(p.point); propList.Add(p); }
UnityEngine.Vector2[] extraProps = { new UnityEngine.Vector2(-5f, -4.5f), new UnityEngine.Vector2(4.5f, 1.8f), new UnityEngine.Vector2(0f, 1.5f) };
for (int i = 0; i < extraProps.Length; i++) {
  var src = g.props[i % 2];
  var copy = UnityEngine.Object.Instantiate(src.view.gameObject, src.view.parent);
  copy.name = src.view.name;
  copy.transform.position = TNDA.Prototype.ArcadeGame.PixelPosition(extraProps[i]);
  propList.Add(new TNDA.Prototype.BreakableProp { view = copy.transform, point = extraProps[i], points = src.points });
}
g.props = propList.ToArray();
g.props[0].view.parent.name = "Breakables (5)";
UnityEditor.EditorUtility.SetDirty(g);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "arena " + g.arenaMin + " " + g.arenaMax + " props=" + g.props.Length + " pickups=" + g.pickups.Length;
