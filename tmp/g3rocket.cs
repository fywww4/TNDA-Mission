var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput(); g.enabled = false; string r = "";
try {
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
foreach (var c in g.cars) c.spawnProtection = 0;
var cs = typeof(TNDA.Prototype.DemolitionGame).GetField("cherrySeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var ai = g.cars[3];
for (int trial = 0; trial < 2; trial++) {
  ((float[])cs.GetValue(g))[3] = trial == 0 ? 8 : 0;
  ai.health = 1; ai.position = new UnityEngine.Vector2(1, -1); ai.speed = 0;
  g.rockets[0].Spawn(new UnityEngine.Vector2(-.5f, -1), new UnityEngine.Vector2(6, 0), 0);
  for (int f = 0; f < 20 && g.rockets[0].active; f++) { ai.position = new UnityEngine.Vector2(1, -1); ai.speed = 0; g.Advance(dt, none, none); }
  r += (trial == 0 ? "with cherry" : "no cherry") + ": alive=" + ai.alive + " hp=" + ai.health.ToString("0.00") + " | ";
  if (!ai.alive) { ai.alive = true; ai.gameObject.SetActive(true); }
}
} catch (System.Exception e) { r += e; }
g.enabled = true; g.PreparePreview(); return r;
