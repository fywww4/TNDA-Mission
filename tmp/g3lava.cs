// CLI regression (Play Mode): eval_file tmp/g3lava.cs. Lava and oil (2026-10-16): the pool forms at 80 s (15 s in the
// title demo) with its notice, one prop slot is used up, AI avoids it unless it has a cherry; oil shows as a trailing
// icon, drops a slick view behind the car and spins the next car. Ends by capturing tmp/g3lava.png.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
var use = new TNDA.Prototype.ArcadeInput { action = true, actionPressed = true };
bool wasEnabled = g.enabled; g.enabled = false;
UnityEngine.Application.runInBackground = true;
try
{
    // Title demo: lava by 15 s.
    g.PreparePreview();
    for (int f = 0; f < 60 * 16; f++) g.Advance(dt, none, none);
    checks["demo lava by 15 s"] = g.lavaView.gameObject.activeSelf;

    // A real 1P match: P1 kept first so it is never eliminated.
    g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    var p1 = g.cars[0];
    bool before = false;
    while (g.Elapsed < 80.05f) { p1.score = 3000; p1.health = 1; before |= g.lavaView.gameObject.activeSelf && g.Elapsed < 79.9f; g.Advance(dt, none, none); }
    checks["no lava before 80 s"] = !before;
    checks["lava at 80 s + notice"] = g.lavaView.gameObject.activeSelf + " \"" + g.notice.text + "\"";
    checks["one prop slot used up"] = System.Linq.Enumerable.Count(g.props, p => p.molten);
    var lava = (UnityEngine.Vector2)g.lavaView.transform.position;
    System.Func<TNDA.Prototype.ArcadeVehicle, bool> inLava = c => UnityEngine.Mathf.Abs(c.position.x - lava.x) < .75f && UnityEngine.Mathf.Abs(c.position.y - lava.y) < .75f;

    // 30 s of AI driving after the lava: count lava wrecks with and without a cherry.
    int plain = 0, cherry = 0;
    var alive = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(g.cars, c => c.alive));
    var hadCherry = new bool[4];
    for (int f = 0; f < 60 * 30 && g.Phase == TNDA.Prototype.ArcadePhase.Playing; f++)
    {
        p1.score = 3000; p1.health = 1; p1.position = new UnityEngine.Vector2(-3.8f, 1.2f);
        for (int i = 0; i < 4; i++) hadCherry[i] = g.cars[i].HasCherry;
        g.Advance(dt, none, none);
        for (int i = 1; i < 4; i++)
        {
            var c = g.cars[i];
            if (alive[i] && !c.alive && inLava(c)) { if (hadCherry[i]) cherry++; else plain++; }
            alive[i] = c.alive;
        }
    }
    checks["AI lava wrecks in 30 s: plain / cherry"] = plain + " / " + cherry;

    // Oil: icon while held, slick view behind the car after use, next car spins.
    g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var c in g.cars) c.computer = false;
    p1.position = new UnityEngine.Vector2(-1, -1.5f); p1.heading = 0; p1.item = TNDA.Prototype.ArenaItem.Oil;
    g.Advance(dt, none, none);
    checks["oil icon trails P1"] = g.itemViews[0].gameObject.activeSelf && g.itemViews[0].sprite == g.itemSprites[3];
    g.Advance(dt, use, none);
    var slot = System.Array.Find(g.rockets, r => r.active && r.slick);
    checks["slick view on the floor"] = slot != null && slot.slickView.gameObject.activeSelf && !slot.view.gameObject.activeSelf;
    var victim = g.cars[3];
    victim.position = slot.position; victim.speed = 2;
    g.Advance(dt, none, none);
    checks["next car spins, no damage, dropper +50"] = victim.IsSlipping + " hp=" + victim.health + " P1=" + p1.score + " slick=" + slot.slickView.gameObject.activeSelf;

    // Screenshot: lava pool and an oil slick on the floor.
    g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var c in g.cars) c.computer = false;
    g.lavaSeconds = 4;
    for (int f = 0; f < 60 * 5; f++) g.Advance(dt, none, none);
    p1.position = new UnityEngine.Vector2(-2.6f, -2.6f); p1.heading = 90; p1.item = TNDA.Prototype.ArenaItem.Oil;
    g.Advance(dt, use, none);
    p1.position = new UnityEngine.Vector2(-2.6f, -1.6f);
    g.cars[2].item = TNDA.Prototype.ArenaItem.Oil;
    g.Advance(dt, none, none);
    UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3lava.png");
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.lavaSeconds = 80;
return checks;
