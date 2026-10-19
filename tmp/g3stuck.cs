// CLI regression (Play Mode): eval_file tmp/g3stuck.cs. AI getting unstuck (2026-10-16): a computer car jammed into a
// corner drives out; a slow bump into the prop it is heading for leads to a run-up and a smash; over three 1P matches,
// how long computer cars sit still (moved < 0.3 in the last 2 s while alive and not slipping on oil).
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled; g.enabled = false;
System.Action start = () => { g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none); };
try
{
    // Corner: 追王 nose-first into the top-right corner.
    start();
    var hunter = g.cars[2];
    for (int f = 0; f < 5; f++) g.Advance(dt, none, none);
    var corner = new UnityEngine.Vector2(g.arenaMax.x - .05f, g.arenaMax.y - .05f);
    hunter.position = corner; hunter.heading = 45; hunter.speed = 0;
    for (int f = 0; f < 60 * 3; f++) g.Advance(dt, none, none);
    checks["corner: 追王 drove out (distance after 3 s)"] = UnityEngine.Vector2.Distance(hunter.position, corner).ToString("0.0");

    // Slow bump: 迷糊 0.9 from a prop, facing it, from a standstill.
    start();
    var wanderer = g.cars[1]; var prop = g.props[0];
    for (int f = 0; f < 5; f++) g.Advance(dt, none, none);
    prop.broken = false; prop.point = new UnityEngine.Vector2(0, -1.2f);
    foreach (var c in g.cars) if (c != wanderer) c.position = new UnityEngine.Vector2(c.index == 0 ? -3.8f : 3.8f, c.index == 3 ? -3.4f : 1.2f);
    wanderer.position = new UnityEngine.Vector2(-.9f, -1.2f); wanderer.heading = 0; wanderer.speed = 0;
    float smashedAt = -1;
    for (int f = 0; f < 60 * 8 && smashedAt < 0; f++) { g.Advance(dt, none, none); if (prop.broken) smashedAt = f / 60f; }
    checks["slow bump: prop smashed after run-up (s)"] = smashedAt.ToString("0.0");

    // Three matches: seconds computer cars sit still.
    float stillSeconds = 0, aliveSeconds = 0;
    for (int m = 0; m < 3; m++)
    {
        start();
        var history = new System.Collections.Generic.Queue<UnityEngine.Vector2>[4];
        for (int i = 0; i < 4; i++) history[i] = new System.Collections.Generic.Queue<UnityEngine.Vector2>();
        for (int f = 0; f < 60 * 110 && g.Phase == TNDA.Prototype.ArcadePhase.Playing; f++)
        {
            g.cars[0].score = 5000; g.cars[0].health = 1; // keep P1 in, parked
            g.Advance(dt, none, none);
            for (int i = 1; i < 4; i++)
            {
                var c = g.cars[i];
                if (!c.alive || c.IsSlipping) { history[i].Clear(); continue; }
                history[i].Enqueue(c.position);
                if (history[i].Count > 120) history[i].Dequeue();
                aliveSeconds += dt;
                if (history[i].Count == 120 && UnityEngine.Vector2.Distance(history[i].Peek(), c.position) < .3f) stillSeconds += dt;
            }
        }
    }
    checks["computer cars sitting still: seconds / alive seconds"] = stillSeconds.ToString("0.0") + " / " + aliveSeconds.ToString("0");
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.enabled = wasEnabled; g.PreparePreview();
return checks;
