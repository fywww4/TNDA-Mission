// CLI regression (Play Mode): eval_file tmp/g3lavatrap.cs. The 2026-10-20 report: Zhima stuck between the left wall
// and a lava pool 1.3 from it. Recreates that pocket (lava forced in by reflection) with Zhima inside and Player1 below
// right, then measures how long Zhima sits still and whether it drives into the lava; also forms 300 pools the normal
// way and reports how close any came to a wall.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled; g.enabled = false;
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
try
{
    for (int trial = 0; trial < 6; trial++)
    {
        g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
        for (int f = 0; f < 60 * 11; f++) g.Advance(dt, none, none); // past the grace period
        var match = typeof(TNDA.Prototype.DemolitionGame).GetField("match", flags).GetValue(g);
        var lava = new UnityEngine.Vector2(g.arenaMin.x + 1.3f + .5f, -1.0f);
        match.GetType().GetProperty("LavaPoint").SetValue(match, lava);
        match.GetType().GetProperty("LavaActive").SetValue(match, true);
        var zhima = g.cars[3]; var p1 = g.cars[0];
        zhima.position = new UnityEngine.Vector2(g.arenaMin.x + .3f, -.6f); zhima.heading = -90; zhima.speed = 0;
        foreach (var c in g.cars) if (c != zhima && c != p1) c.position = new UnityEngine.Vector2(3.6f, c.index == 1 ? 1.2f : -3.4f);
        float still = 0, gotOut = -1; bool burned = false;
        var history = new System.Collections.Generic.Queue<UnityEngine.Vector2>();
        for (int f = 0; f < 60 * 15; f++)
        {
            p1.position = new UnityEngine.Vector2(-1f, -3.4f); p1.speed = 0; p1.health = 1; p1.score = 0;
            bool aliveBefore = zhima.alive;
            g.Advance(dt, none, none);
            // Only a wreck on the pool counts (a wreck from ramming elsewhere is not lava).
            if (aliveBefore && !zhima.alive && UnityEngine.Mathf.Abs(zhima.position.x - lava.x) < .8f && UnityEngine.Mathf.Abs(zhima.position.y - lava.y) < .8f) burned = true;
            if (!zhima.alive) { history.Clear(); continue; }
            history.Enqueue(zhima.position); if (history.Count > 120) history.Dequeue();
            if (history.Count == 120 && UnityEngine.Vector2.Distance(history.Peek(), zhima.position) < .3f) still += dt;
            if (gotOut < 0 && zhima.position.x > lava.x + .9f) gotOut = f / 60f;
        }
        checks["trial " + trial + ": still seconds / left the pocket at / drove into lava"] = still.ToString("0.0") + " / " + (gotOut < 0 ? "never" : gotOut.ToString("0.0") + " s") + " / " + burned;
    }

    // Normal pool placement: closest any pool came to a wall over 300 matches' worth of pools.
    var m = typeof(TNDA.Prototype.DemolitionGame).GetField("match", flags).GetValue(g);
    var form = m.GetType().GetMethod("FormLava", flags);
    float closest = 99;
    for (int k = 0; k < 300; k++)
    {
        m.GetType().GetProperty("LavaActive").SetValue(m, false);
        form.Invoke(m, null);
        var p = (UnityEngine.Vector2)m.GetType().GetProperty("LavaPoint").GetValue(m);
        closest = UnityEngine.Mathf.Min(closest, p.x - g.arenaMin.x, g.arenaMax.x - p.x, p.y - g.arenaMin.y, g.arenaMax.y - p.y);
    }
    checks["pool centre to nearest wall, minimum over 300 (≥ 2.1)"] = closest.ToString("0.00");
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.enabled = wasEnabled; g.PreparePreview();
return checks;
