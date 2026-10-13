var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var sb = new System.Text.StringBuilder(); float dt = 1f/60; var none = new TNDA.Prototype.ArcadeInput();
g.enabled = false;
try {
  g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
  for (int f = 0; f < 150; f++) g.Advance(dt, none, none);
  var ai = g.cars[2]; ai.spawnProtection = 0; ai.shieldSeconds = 0; float h0 = ai.heading;
  g.DamageVehicle(ai, 1);
  sb.Append("wrecked " + ai.DriverName + " alive=" + ai.alive + " visible=" + ai.gameObject.activeSelf + " at " + ai.position + "\n");
  float gone = -1, back = -1;
  for (int f = 1; f <= 300; f++) {
    g.Advance(dt, none, none);
    if (f % 15 == 0 && gone < 0) sb.Append("  +" + (f/60f).ToString("0.00") + "s pos=" + ai.position + " spun=" + ((ai.heading - h0)/360).ToString("0.0") + " turns\n");
    if (gone < 0 && !ai.gameObject.activeSelf) gone = f/60f;
    if (back < 0 && ai.alive) { back = f/60f; sb.Append("  respawned at " + back.ToString("0.00") + "s pos=" + ai.position + " visible=" + ai.gameObject.activeSelf + " AI=" + ai.computer + "\n"); break; }
  }
  sb.Append("  left screen after " + gone.ToString("0.00") + "s\n");
  // Demo: P1's car wrecked during attract must come back AI-driven.
  g.PreparePreview();
  for (int f = 0; f < 120; f++) g.Advance(dt, none, none);
  var c0 = g.cars[0]; c0.spawnProtection = 0; c0.shieldSeconds = 0; g.DamageVehicle(c0, 1);
  for (int f = 0; f < 240 && !c0.alive; f++) g.Advance(dt, none, none);
  sb.Append("demo: car0 respawned alive=" + c0.alive + " AI=" + c0.computer + " phase=" + g.Phase);
} catch (System.Exception e) { sb.Append("EXCEPTION " + e); }
g.enabled = true; g.PreparePreview(); return sb.ToString();
