var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput();
g.enabled = false;
try {
  g.StartRun(1);
  while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
  string last = null;
  for (int f = 0; f < 60 * 5; f++) {
    g.Advance(dt, none, none);
    var txt = g.countdownLabel.text + "(" + g.countdownLabel.fontSize + ")";
    if (txt != last) { sb.Append("  t=" + g.Elapsed.ToString("0.00") + " label: " + txt + "\n"); last = txt; }
  }
  // Bumper test after the grace period: AI rams a parked P1 from the side.
  typeof(TNDA.Prototype.ArcadeGame).GetProperty("Elapsed").SetValue(g, 15f);
  var p1 = g.cars[0]; var ai = g.cars[2];
  foreach (var c in g.cars) { c.spawnProtection = 0; c.shieldSeconds = 0; if (c != p1 && c != ai) c.position = new UnityEngine.Vector2(3.5f, 1.2f); }
  p1.position = new UnityEngine.Vector2(0, -1.5f); p1.heading = 90; p1.speed = 0; p1.health = 1;
  ai.position = new UnityEngine.Vector2(-.7f, -1.5f); ai.heading = 0; ai.speed = 2.4f;
  var start = p1.position;
  for (int f = 1; f <= 60; f++) {
    ai.position = f < 3 ? ai.position : ai.position; g.Advance(dt, none, none);
    if (f == 1 || f % 15 == 0) sb.Append("  bump +" + (f/60f).ToString("0.00") + "s P1 moved " + (p1.position - start).magnitude.ToString("0.00") + " gap=" + UnityEngine.Vector2.Distance(p1.position, ai.position).ToString("0.00") + " P1 heading=" + p1.heading.ToString("0") + " hp=" + (p1.health*100).ToString("0") + "\n");
  }
} catch (System.Exception e) { sb.Append("EXCEPTION " + e); }
g.enabled = true; g.PreparePreview(); return sb.ToString();
