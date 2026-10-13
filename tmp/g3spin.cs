var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput();
g.enabled = false;
try {
  g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
  for (int f = 0; f < 120; f++) g.Advance(dt, none, none);
  g.cars[0].score = 999;
  typeof(TNDA.Prototype.ArcadeGame).GetProperty("Elapsed").SetValue(g, 59.99f);
  g.Advance(dt, none, none);
  var loser = System.Linq.Enumerable.First(g.cars, c => c.eliminated);
  sb.Append("eliminated " + loser.DriverName + " at " + loser.position + " active=" + loser.gameObject.activeSelf + "\n");
  float h0 = loser.heading;
  for (int f = 1; f <= 90; f++) {
    g.Advance(dt, none, none);
    if (f % 10 == 0) sb.Append("  +" + (f/60f).ToString("0.00") + "s pos=" + loser.position + " spun=" + ((loser.heading - h0)/360).ToString("0.0") + " turns active=" + loser.gameObject.activeSelf + "\n");
    if (!loser.gameObject.activeSelf) { sb.Append("  gone off screen after " + (f/60f).ToString("0.00") + "s\n"); break; }
  }
  // capture a mid-spin frame for a picture: restart and stop at 0.25s into the spin
  g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
  for (int f = 0; f < 120; f++) g.Advance(dt, none, none);
  g.cars[0].score = 999; typeof(TNDA.Prototype.ArcadeGame).GetProperty("Elapsed").SetValue(g, 59.99f);
  for (int f = 0; f < 16; f++) g.Advance(dt, none, none);
  sb.Append("frozen mid-spin for screenshot");
} catch (System.Exception e) { sb.Append("EXCEPTION " + e); }
return sb.ToString();
