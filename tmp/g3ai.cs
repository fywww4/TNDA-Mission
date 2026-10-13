var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput();
var targetField = typeof(TNDA.Prototype.DemolitionGame).GetField("aiTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
g.enabled = false;
try {
for (int scenario = 0; scenario < 2; scenario++) {
  g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
  var p1 = g.cars[0];
  if (scenario == 0) p1.score = 1500; else { g.cars[1].score = 1500; }
  int[] earlyT = new int[4], lateT = new int[4]; float earlyMinHp = 1, lateMinHp = 1; int lateWrecks = 0; bool wasAlive = true;
  for (int f = 0; f < 60 * 25; f++) {
    if (scenario == 0) p1.score = System.Math.Max(p1.score, 1500); else g.cars[1].score = System.Math.Max(g.cars[1].score, 1500);
    g.Advance(dt, none, none);
    var t = (int[])targetField.GetValue(g);
    for (int i = 1; i < 4; i++) if (t[i] >= 0) { if (g.Elapsed < 10) earlyT[t[i]]++; else lateT[t[i]]++; }
    if (g.Elapsed < 10) earlyMinHp = System.Math.Min(earlyMinHp, p1.alive ? p1.health : 0);
    else { lateMinHp = System.Math.Min(lateMinHp, p1.alive ? p1.health : 0); if (wasAlive && !p1.alive) lateWrecks++; }
    wasAlive = p1.alive;
  }
  sb.Append(scenario == 0 ? "[P1 leads 1500]\n" : "[電腦1 leads 1500, P1 0]\n");
  sb.Append("  0-10s AI target frames  P1:" + earlyT[0] + " 電腦1:" + earlyT[1] + " 電腦2:" + earlyT[2] + " 電腦3:" + earlyT[3] + " | P1 min HP " + (earlyMinHp*100).ToString("0") + "%\n");
  sb.Append("  10-25s AI target frames P1:" + lateT[0] + " 電腦1:" + lateT[1] + " 電腦2:" + lateT[2] + " 電腦3:" + lateT[3] + " | P1 min HP " + (lateMinHp*100).ToString("0") + "% wrecked " + lateWrecks + "x\n");
}
} catch (System.Exception e) { sb.Append("EXCEPTION " + e); }
g.enabled = true; g.PreparePreview();
return sb.ToString();
