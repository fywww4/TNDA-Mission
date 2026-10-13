// Ticket 07: the attract demo shows no notice text, while a real match still shows its notices.
// (Sound is not observable in edit mode; the adapter gates notices and sounds at the same place.)
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
try
{
    g.enabled = false;
    g.PreparePreview();
    g.Advance(dt, none, none); // starts the demo
    checks["in attract"] = g.Phase == TNDA.Prototype.ArcadePhase.Attract;
    var victim = g.cars[2];
    victim.spawnProtection = victim.shieldSeconds = 0;
    g.notice.text = ""; // a notice left over from earlier tooling may still be timing out
    g.DamageVehicle(victim, 1);
    checks["demo wreck happened"] = !victim.alive;
    checks["demo wreck shows no notice"] = g.notice.text == "";
    for (int f = 0; f < 60 * 21; f++) g.Advance(dt, none, none);
    checks["demo elimination happened"] = System.Linq.Enumerable.Count(g.cars, c => c.eliminated) == 1;
    checks["demo elimination shows no notice"] = g.notice.text == "";

    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    var target = g.cars[3];
    target.spawnProtection = target.shieldSeconds = 0;
    g.DamageVehicle(target, 1);
    checks["match wreck shows its notice"] = g.notice.text.StartsWith(target.DriverName);
}
finally { g.PreparePreview(); g.enabled = wasEnabled; }
return checks;
