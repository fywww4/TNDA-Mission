// CLI regression (Play Mode): eval_file tmp/g3personality.cs. The three fixed computer personalities:
// seats and names, 追王 chases first place (second when it leads), 碰碰 rams the nearest car and moves on after a bump,
// 迷糊 never targets a car and is slowest, the late-match speed-up, cherry hunting for every personality, the 迷糊's
// random item timing, and the grace period still sparing players.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
g.enabled = false;
System.Action<int> start = players =>
{
    g.StartRun(players);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
};
System.Func<string> names = () => string.Join(",", System.Linq.Enumerable.Select(g.cars, c => c.DriverName));
try
{
    start(1);
    checks["1P names"] = names();
    start(2);
    checks["2P names"] = names();
    g.PreparePreview();
    checks["demo names"] = names();

    // 追王 during the grace period: a high-scoring P1 is spared, the top computer car is chased.
    start(1);
    var p1 = g.cars[0]; var wanderer = g.cars[1]; var hunter = g.cars[2]; var bumper = g.cars[3];
    p1.score = 1500; wanderer.score = 400; bumper.score = 100;
    for (int f = 0; f < 90; f++) g.Advance(dt, none, none);
    // Since 2026-10-16 a chase goes for first or (40%) second place among cars it may hit.
    checks["grace: 追王 chases a top computer (迷糊/碰碰), or scatters"] = hunter.Ai.Target == wanderer || hunter.Ai.Target == bumper || hunter.Ai.Scattering;
    checks["grace: nobody targets P1"] = System.Linq.Enumerable.All(g.cars, c => c.Ai.Target != p1 || !c.computer);
    // After the grace period the leading P1 is the 追王's target.
    for (int f = 0; f < 60 * 10; f++) { g.Advance(dt, none, none); p1.score = 1500; if (!p1.alive) break; }
    bool huntedP1 = false;
    for (int f = 0; f < 60 * 25 && !huntedP1; f++) { g.Advance(dt, none, none); p1.score = 1500; huntedP1 = hunter.Ai.Target == p1; }
    checks["after grace: 追王 chases leading P1 at some point"] = huntedP1;
    // A leading 追王 chases second place.
    start(1);
    hunter.score = 900; bumper.score = 500; wanderer.score = 100;
    for (int f = 0; f < 120; f++) g.Advance(dt, none, none);
    checks["leading 追王 never chases itself: second or third"] = hunter.Ai.Target == bumper || hunter.Ai.Target == wanderer || hunter.Ai.Scattering;

    // 碰碰 goes for the nearest car, then another one after bumping it.
    start(1);
    for (int f = 0; f < 30; f++) g.Advance(dt, none, none);
    bumper.position = new UnityEngine.Vector2(2, -1); hunter.position = new UnityEngine.Vector2(3, -1); wanderer.position = new UnityEngine.Vector2(-3, 1);
    for (int f = 0; f < 70; f++) g.Advance(dt, none, none);
    var first = bumper.Ai.Target;
    checks["碰碰 targets nearest computer"] = first != null ? first.DriverName : "none";
    var switched = false;
    for (int f = 0; f < 60 * 8 && !switched; f++) { g.Advance(dt, none, none); switched = bumper.Ai.Target != null && bumper.Ai.Target != first; }
    checks["碰碰 moves on to another car"] = switched;

    // 迷糊 never targets a car without a cherry; top speeds by personality before and after the first elimination.
    start(1);
    bool wandererTargeted = false;
    float wMax = 0, hMax = 0, bMax = 0, wLate = 0, hLate = 0;
    for (int f = 0; f < 60 * 75; f++)
    {
        g.Advance(dt, none, none);
        if (g.Phase != TNDA.Prototype.ArcadePhase.Playing) break;
        p1.health = 1; // keep P1 in the match (scores drive eliminations)
        wandererTargeted |= wanderer.Ai.Target != null && !wanderer.HasCherry;
        bool late = g.Elapsed >= g.firstElimination;
        if (wanderer.alive && !wanderer.HasCherry) { if (late) wLate = UnityEngine.Mathf.Max(wLate, wanderer.speed); else wMax = UnityEngine.Mathf.Max(wMax, wanderer.speed); }
        if (hunter.alive && !hunter.HasCherry) { if (late) hLate = UnityEngine.Mathf.Max(hLate, hunter.speed); else hMax = UnityEngine.Mathf.Max(hMax, hunter.speed); }
        if (bumper.alive && !bumper.HasCherry && !late) bMax = UnityEngine.Mathf.Max(bMax, bumper.speed);
    }
    checks["迷糊 never targets a car"] = !wandererTargeted;
    checks["top speed early 迷糊/追王/碰碰 (2.07/2.44/2.44)"] = wMax.ToString("0.00") + "/" + hMax.ToString("0.00") + "/" + bMax.ToString("0.00");
    checks["top speed late 迷糊/追王 (2.26/2.66; 0 = eliminated)"] = wLate.ToString("0.00") + "/" + hLate.ToString("0.00");

    // Cherry: even the 迷糊 hunts.
    start(1);
    for (int f = 0; f < 60 * 11; f++) g.Advance(dt, none, none);
    wanderer.GiveCherry(8);
    for (int f = 0; f < 10; f++) g.Advance(dt, none, none);
    checks["cherry 迷糊 hunts"] = wanderer.Ai.Target != null;

    // 迷糊 uses a held item 2 to 4 seconds after getting it.
    start(1);
    for (int f = 0; f < 60 * 11; f++) g.Advance(dt, none, none);
    p1.position = new UnityEngine.Vector2(-4, 1.4f);
    wanderer.item = TNDA.Prototype.ArenaItem.Shield; wanderer.shieldSeconds = 0;
    int used = -1;
    for (int f = 0; f < 60 * 6; f++) { g.Advance(dt, none, none); if (wanderer.item == TNDA.Prototype.ArenaItem.None) { used = f; break; } }
    checks["迷糊 item used after seconds"] = (used / 60f).ToString("0.0");

    // 30 s soak in 2P with idle players: no exceptions, players unhurt during the grace period.
    start(2);
    bool spared = true;
    for (int f = 0; f < 60 * 30; f++) { g.Advance(dt, none, none); if (g.Elapsed < 10) spared &= g.cars[0].health == 1 && g.cars[1].health == 1; }
    checks["2P soak ok, players spared in grace"] = spared;
    checks["2P scores"] = string.Join(",", System.Linq.Enumerable.Select(g.cars, c => c.DriverName + ":" + c.score));
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.enabled = wasEnabled;
g.PreparePreview();
return checks;
