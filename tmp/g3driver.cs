// Ticket 05: the AI driver module through its public seams. Situations are staged in a real match
// (StartRun/Advance); the AI's choice is read from car.Ai (Target, Destination). Play Mode.
var checks = new System.Collections.Generic.Dictionary<string, bool>();
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
var randomState = UnityEngine.Random.state;
float grace = g.aiGraceSeconds, firstOut = g.firstElimination;
System.Action<float> run = seconds => { for (float t = 0; t < seconds - 1e-4f; t += dt) g.Advance(dt, none, none); };

// Car 2 is the AI under test. P1, car 1 and car 3 are parked (no input) unless a check says otherwise.
// Only prop[1] (2, -0.8) stays; crates are hidden. Car 2 makes its first plan 0.5s after GO.
System.Func<float, float, TNDA.Prototype.ArcadeVehicle> stage = (graceSeconds, leaderScore) => {
    g.aiGraceSeconds = graceSeconds;
    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) { car.computer = false; car.spawnProtection = car.shieldSeconds = 0; car.speed = 0; }
    g.props[0].broken = true; g.props[0].respawn = 1000; g.props[0].view.gameObject.SetActive(false);
    foreach (var pickup in g.pickups) { pickup.available = false; pickup.respawn = 1000; pickup.view.gameObject.SetActive(false); }
    var ai = g.cars[2];
    ai.computer = true;
    ai.position = new UnityEngine.Vector2(0, -2); ai.heading = 0;
    g.cars[0].position = new UnityEngine.Vector2(-2, -2); g.cars[0].score = leaderScore;
    g.cars[1].position = new UnityEngine.Vector2(2, -3.3f);
    g.cars[3].position = new UnityEngine.Vector2(-3, 1);
    foreach (var car in g.cars) car.Sync();
    return ai;
};

try
{
    g.enabled = false;
    UnityEngine.Random.InitState(20261013);
    var p1 = g.cars[0];

    // --- Seam 1: target choice in staged situations.
    var ai = stage(0, 1500);
    run(.55f);
    checks["leader 300+ ahead is hunted over the prop strategy"] = ai.Ai.Target == p1;

    ai = stage(0, 200);
    run(.55f);
    checks["leader under the margin: strategy goes for the prop"] = ai.Ai.Target == null
        && (ai.Ai.Destination - g.props[1].point).magnitude < .01f;

    ai = stage(10, 1500);
    g.cars[3].computer = true; g.cars[3].score = 1500;
    run(.55f);
    checks["grace period: hunts the leading AI, not the nearer leading player"] = ai.Ai.Target == g.cars[3];

    ai = stage(10, 0);
    var cherry = g.cars[3];
    cherry.position = ai.position + new UnityEngine.Vector2(1.2f, 0); cherry.Sync();
    cherry.GiveCherry(8);
    p1.position = new UnityEngine.Vector2(0, 1); p1.Sync(); // out of the escape path
    run(1.5f);
    checks["flees a nearby cherry car"] = ai.alive && UnityEngine.Vector2.Distance(ai.position, cherry.position) > 1.5f;

    // --- Seam 2: the driver notices collisions, respawns and eliminations by itself and replans next frame.
    // At 0.5s it plans for the prop (P1 only 200 ahead). P1 then jumps to 1500: only a replan reveals the hunt.
    System.Func<TNDA.Prototype.ArcadeVehicle> committed = () => {
        var car = stage(0, 200);
        run(.6f);
        p1.score = 1500;
        car.position = new UnityEngine.Vector2(0, -2); car.speed = 0; car.Sync();
        return car;
    };
    ai = committed();
    run(2 * dt);
    checks["control: no event, plan kept"] = ai.Ai.Target == null;

    ai = committed();
    ai.position = new UnityEngine.Vector2(g.arenaMax.x + .1f, -2); ai.heading = 0; ai.speed = 2;
    run(2 * dt);
    checks["replans after a wall hit"] = ai.Ai.Target == p1;

    // Props: the plan heads for prop[1], so hit prop[0] instead (reaching the destination replans anyway).
    System.Action reveal = () => { g.props[0].broken = false; g.props[0].view.gameObject.SetActive(true); };
    ai = committed(); reveal();
    ai.position = g.props[0].point + new UnityEngine.Vector2(.3f, 0); ai.speed = 0;
    run(2 * dt);
    checks["replans after bumping a prop"] = ai.Ai.Target == p1;

    ai = committed(); reveal();
    ai.position = g.props[0].point + new UnityEngine.Vector2(.3f, 0); ai.heading = 180; ai.speed = 2.5f;
    run(2 * dt);
    checks["replans after smashing a prop"] = g.props[0].broken && ai.Ai.Target == p1;

    ai = committed();
    g.cars[1].position = ai.position + new UnityEngine.Vector2(.3f, 0); g.cars[1].Sync();
    run(2 * dt);
    checks["replans after touching a car"] = ai.Ai.Target == p1;

    ai = committed();
    g.firstElimination = g.Elapsed + dt / 2;
    run(2 * dt);
    checks["replans after an elimination"] = g.cars[3].eliminated && ai.Ai.Target == p1;
    g.firstElimination = firstOut;

    ai = committed();
    g.DamageVehicle(ai, 1);
    bool wreckedHuntsNobody = !ai.alive && ai.Ai.Target == null;
    for (int f = 0; f < 600 && !ai.alive; f++) g.Advance(dt, none, none);
    run(2 * dt);
    checks["a wreck hunts nobody; replans after respawn"] = wreckedHuntsNobody && ai.alive && ai.Ai.Target == p1;

    // --- Seam 3: whole matches. 30s of four AI cars, and the four-AI title demo past its elimination and loop.
    g.aiGraceSeconds = grace;
    string error = null;
    try
    {
        g.StartRun(1);
        while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
        g.cars[0].computer = true;
        bool targeted = false;
        for (int f = 0; f < 60 * 30; f++) { g.Advance(dt, none, none); targeted |= System.Linq.Enumerable.Any(g.cars, c => c.Ai.Target != null); }
        checks["30s AI match: AI hunts and scores"] = targeted && System.Linq.Enumerable.Max(g.cars, c => c.score) > 0;

        g.PreparePreview();
        bool allAi = true, sawOut = false, looped = false, demoTargets = false; float last = 0;
        for (int f = 0; f < 60 * 35; f++)
        {
            g.Advance(dt, none, none);
            allAi &= System.Linq.Enumerable.All(g.cars, c => c.computer);
            sawOut |= System.Linq.Enumerable.Any(g.cars, c => c.eliminated);
            demoTargets |= System.Linq.Enumerable.Any(g.cars, c => c.Ai.Target != null);
            looped |= g.Elapsed < last; last = g.Elapsed;
        }
        checks["title demo: four AI hunt, eliminate at 20s, loop at 30s"] = g.Phase == TNDA.Prototype.ArcadePhase.Attract
            && allAi && demoTargets && sawOut && looped;
    }
    catch (System.Exception e) { error = e.ToString(); }
    checks["whole matches run without exceptions" + (error == null ? "" : ": " + error)] = error == null;
}
finally
{
    g.aiGraceSeconds = grace; g.firstElimination = firstOut;
    UnityEngine.Random.state = randomState; g.PreparePreview(); g.enabled = wasEnabled;
}
return checks;
