// CLI regression (Play Mode): eval_file tmp/g3hunter.cs. The softened 追王 (2026-10-16): chase 6 s / scatter 4 s,
// a chase goes for first place about 60% of the time (else second), ramming the quarry ends the chase, and the rammed
// car is spared next chase. P1 leads with 1500 points; measured over 100 s after the grace period.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled; g.enabled = false;
try
{
    g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    var p1 = g.cars[0]; var hunter = g.cars[2];
    int frames = 0, onP1 = 0, scatterFrames = 0, scatterWithTarget = 0, chases = 0, chaseFirst = 0, rams = 0, sparedOk = 0, sparedChecks = 0;
    bool wasScattering = true; TNDA.Prototype.ArcadeVehicle lastTarget = null, rammedCar = null; int lastCollisions = hunter.Collisions;
    float chaseStart = 0, longestChase = 0;
    for (int f = 0; f < 60 * 110 && g.Phase == TNDA.Prototype.ArcadePhase.Playing; f++)
    {
        // Fixed standings, nobody wrecked or eliminated: P1 first, 碰碰 second, 迷糊 third.
        foreach (var c in g.cars) { c.health = 1; c.score = c == p1 ? 1500 : c.index == 3 ? 900 : c.index == 1 ? 300 : 100; }
        g.Advance(dt, none, none);
        if (g.Elapsed < 10) continue;
        frames++;
        bool sc = hunter.Ai.Scattering;
        var t = hunter.Ai.Target;
        if (sc) { scatterFrames++; if (t != null && !hunter.HasCherry) scatterWithTarget++; }
        if (t == p1) onP1++;
        if (!sc && wasScattering) { chases++; chaseStart = g.Elapsed; if (rammedCar != null) { sparedChecks++; } }
        if (!sc && t != null && lastTarget == null && chases > 0)
        {
            if (t == p1) chaseFirst++;
            if (rammedCar != null) { if (t != rammedCar) sparedOk++; rammedCar = null; }
        }
        if (sc && !wasScattering)
        {
            longestChase = UnityEngine.Mathf.Max(longestChase, g.Elapsed - chaseStart);
            if (lastTarget != null && UnityEngine.Vector2.Distance(lastTarget.position, hunter.position) < 1.2f) { rams++; rammedCar = lastTarget; }
        }
        wasScattering = sc;
        lastTarget = sc ? null : t;
    }
    checks["share of time 追王 targets leading P1"] = (onP1 / (float)frames).ToString("0.00");
    checks["share of time scattering (~0.4)"] = (scatterFrames / (float)frames).ToString("0.00");
    checks["scatter never targets a car"] = scatterWithTarget == 0;
    checks["chases started / aimed at P1 first"] = chases + " / " + chaseFirst;
    checks["longest chase ≤ 6.1 s"] = longestChase.ToString("0.0");
    checks["rams ended chases / next chase spared the rammed car"] = rams + " / " + sparedOk + " of " + sparedChecks;
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.enabled = wasEnabled; g.PreparePreview();
return checks;
