var path = "Assets/Scenes/GAME2.unity";
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
bool wasDirty = scene.isDirty;
TNDA.Prototype.EliminationRaceGame g = null;
foreach (var r in scene.GetRootGameObjects()) if (r.name == "GAME2 Prototype") g = r.GetComponent<TNDA.Prototype.EliminationRaceGame>();
typeof(TNDA.Prototype.ArcadeGame).GetField("sounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, null);
var log = new System.Text.StringBuilder();
float dt = 1f/60;
System.Func<TNDA.Prototype.ArcadeVehicle, float, TNDA.Prototype.ArcadeInput> pilot = (car, look) => {
  var inp = new TNDA.Prototype.ArcadeInput();
  float a = TNDA.Prototype.EliminationRaceGame.TrackAngle(car.position);
  var goal = TNDA.Prototype.EliminationRaceGame.TrackPoint(a + look) - car.position;
  inp.steer = -UnityEngine.Mathf.Clamp(UnityEngine.Mathf.DeltaAngle(car.heading, UnityEngine.Mathf.Atan2(goal.y, goal.x) * UnityEngine.Mathf.Rad2Deg) / 35, -1, 1);
  inp.throttle = 1; return inp; };
var none = new TNDA.Prototype.ArcadeInput();

// A: full race, P1 idle. Check eliminations, monotonic scores, bolt cap, results timing.
g.StartRun(1); for (int k=0;k<400 && g.Phase==TNDA.Prototype.ArcadePhase.Countdown;k++) g.Advance(dt, none, none);
int maxBolts = 0, maxActive = 0; bool scoreDropped = false; var last = new float[4]; int prevElim = 0;
while (g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.Elapsed < 130) {
  g.Advance(dt, none, none);
  maxBolts = System.Math.Max(maxBolts, g.ActiveBolts);
  int act = g.ActiveCars + g.ActiveBolts; foreach (var b in g.boxes) if (b.gameObject.activeSelf) act++; maxActive = System.Math.Max(maxActive, act);
  for (int i=0;i<4;i++){ if (g.cars[i].score < last[i]) scoreDropped = true; last[i]=g.cars[i].score; }
  if (g.Eliminations != prevElim) { prevElim = g.Eliminations; log.Append("elim#"+prevElim+" at "+g.Elapsed.ToString("0.00")+"s alive="+g.ActiveCars+" | "); }
}
log.Append("A: phase="+g.Phase+" winner="+g.Winner+" elapsed="+g.Elapsed.ToString("0.0")+" maxBolts="+maxBolts+" maxActive="+maxActive+" scoreDropped="+scoreDropped+" scores="+string.Join(",", System.Linq.Enumerable.Select(g.cars, c=>c.DriverName+":"+c.score+(c.alive?"":"x")))+"\n");
log.Append("   title="+g.overlayTitle.text.Replace("\n"," ")+"\n");
float t0 = 0; while (g.Phase == TNDA.Prototype.ArcadePhase.Results && t0 < 25) { g.Advance(dt, none, none); t0 += dt; }
log.Append("   results shown for "+t0.ToString("0.0")+"s then phase="+g.Phase+"\n");

// B: P1 drives well with pilot. Lap time and score.
g.StartRun(1); for (int k=0;k<400 && g.Phase==TNDA.Prototype.ArcadePhase.Countdown;k++) g.Advance(dt, none, none);
for (int i=0;i<600;i++) g.Advance(dt, pilot(g.cars[0], .3f), none);
log.Append("B: pilot 10s P1 score="+g.cars[0].score+" cpu scores="+g.cars[1].score+","+g.cars[2].score+","+g.cars[3].score+" maxSpeed="+g.maxSpeed+"\n");

// C: forward 3s, reverse 3s, forward 3s -> no farming
g.StartRun(1); for (int k=0;k<400 && g.Phase==TNDA.Prototype.ArcadePhase.Countdown;k++) g.Advance(dt, none, none);
for (int i=0;i<180;i++) g.Advance(dt, pilot(g.cars[0], .3f), none);
float s1 = g.cars[0].score;
var rev = new TNDA.Prototype.ArcadeInput(); rev.throttle = -1;
for (int i=0;i<180;i++) g.Advance(dt, rev, none);
float s2 = g.cars[0].score; float prog2 = g.cars[0].progress;
for (int i=0;i<60;i++) g.Advance(dt, pilot(g.cars[0], .3f), none);
log.Append("C: fwd="+s1+" afterReverse="+s2+" (progress "+prog2.ToString("0.00")+") after1sFwd="+g.cars[0].score+"\n");

// D: spin in place 10s
g.StartRun(1); for (int k=0;k<400 && g.Phase==TNDA.Prototype.ArcadePhase.Countdown;k++) g.Advance(dt, none, none);
var spin = new TNDA.Prototype.ArcadeInput(); spin.throttle = 1; spin.steer = 1;
for (int i=0;i<600;i++) g.Advance(dt, spin, none);
log.Append("D: circling 10s score="+g.cars[0].score+"\n");

// E: two players
g.StartRun(2); for (int k=0;k<400 && g.Phase==TNDA.Prototype.ArcadePhase.Countdown;k++) g.Advance(dt, none, none);
log.Append("E: players="+g.PlayerCount+" names="+string.Join(",", System.Linq.Enumerable.Select(g.cars, c=>c.DriverName+(c.computer?"(AI)":"")))+" active="+g.ActiveCars+"\n");
for (int i=0;i<300;i++) g.Advance(dt, pilot(g.cars[0], .3f), pilot(g.cars[1], .3f));
log.Append("   5s scores="+string.Join(",", System.Linq.Enumerable.Select(g.cars, c=>c.score.ToString()))+" hud="+g.hud.text+"\n");

// F: bolt hit stuns but does not reduce score
g.StartRun(1); for (int k=0;k<400 && g.Phase==TNDA.Prototype.ArcadePhase.Countdown;k++) g.Advance(dt, none, none);
for (int i=0;i<120;i++) g.Advance(dt, pilot(g.cars[0], .3f), none);
var victim = g.cars[0]; float before = victim.score; float hb = victim.heading;
g.bolts[0].Spawn(victim.position, UnityEngine.Vector2.zero, 3);
g.Advance(dt, none, none);
log.Append("F: hit stun="+victim.stun.ToString("0.00")+" scoreBefore="+before+" after="+victim.score);
for (int i=0;i<60;i++) g.Advance(dt, none, none);
log.Append(" headingRestored="+(UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(victim.heading, hb))<20)+"\n");

g.PreparePreview();
if (!wasDirty) { UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, opened ? UnityEditor.SceneManagement.OpenSceneMode.Additive : UnityEditor.SceneManagement.OpenSceneMode.Single); }
if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path), true);
return log.ToString();
