// Follow-up checks for tmp/g3personality.cs: demo seat names after a demo tick, 碰碰 nearest pick with fresh plans,
// and the 迷糊's item delay (shield must actually come on).
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled; g.enabled = false;
try
{
    g.PreparePreview(); g.Advance(dt, none, none);
    checks["demo names"] = string.Join(",", System.Linq.Enumerable.Select(g.cars, c => c.DriverName));
    var picks = new System.Collections.Generic.List<string>();
    for (int trial = 0; trial < 5; trial++)
    {
        g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
        g.cars[3].position = new UnityEngine.Vector2(2, -1); g.cars[2].position = new UnityEngine.Vector2(3, -1); g.cars[1].position = new UnityEngine.Vector2(-3.5f, 1);
        g.cars[3].Ai.BeginMatch();
        for (int f = 0; f < 50; f++) g.Advance(dt, none, none);
        picks.Add(g.cars[3].Ai.Target == null ? "none" : g.cars[3].Ai.Target.DriverName);
    }
    checks["碰碰 first pick x5 (追王 nearest)"] = string.Join(",", picks);
    var delays = new System.Collections.Generic.List<string>();
    for (int trial = 0; trial < 5; trial++)
    {
        g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
        for (int f = 0; f < 60 * 11; f++) g.Advance(dt, none, none);
        var w = g.cars[1]; g.cars[0].position = new UnityEngine.Vector2(-4, 1.4f);
        w.spawnProtection = 3; w.item = TNDA.Prototype.ArenaItem.Shield; w.shieldSeconds = 0;
        int used = -1;
        for (int f = 0; f < 60 * 6; f++) { g.Advance(dt, none, none); w.spawnProtection = 3; if (w.item == TNDA.Prototype.ArenaItem.None) { used = f; break; } }
        delays.Add((used / 60f).ToString("0.0") + (w.shieldSeconds > 0 ? "s shield" : "s lost"));
    }
    checks["迷糊 item delays x5"] = string.Join(",", delays);
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.enabled = wasEnabled; g.PreparePreview();
return checks;
