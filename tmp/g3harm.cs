// Public match seam: all attack types obey the same opening grace rule.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
var none = new TNDA.Prototype.ArcadeInput();
float dt = 1f / 60;
bool wasEnabled = g.enabled;
var randomState = UnityEngine.Random.state;
System.Action<float> start = elapsed => {
    g.StartRun(2);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) { car.computer = false; car.spawnProtection = car.shieldSeconds = 0; }
    foreach (var prop in g.props) { prop.broken = true; prop.respawn = 1000; prop.view.gameObject.SetActive(false); }
    foreach (var pickup in g.pickups) { pickup.available = false; pickup.respawn = 1000; pickup.view.gameObject.SetActive(false); }
    if (elapsed > 0) g.Advance(elapsed, none, none);
};
try
{
    g.enabled = false;
    UnityEngine.Random.InitState(20261013);
    foreach (bool late in new[] { false, true })
    foreach (var pair in new[] { (2, 0), (2, 3), (0, 2), (0, 1) })
    foreach (string attack in new[] { "ram", "cherry", "rocket" })
    {
        start(late ? 10 : 0);
        var attacker = g.cars[pair.Item1]; var victim = g.cars[pair.Item2];
        attacker.computer = pair.Item1 == 2;
        victim.computer = pair.Item2 >= 2;
        attacker.position = new UnityEngine.Vector2(0, -1); attacker.heading = 0;
        victim.position = new UnityEngine.Vector2(.55f, -1); victim.heading = 90;
        bool allowed = late || pair.Item1 == 0 || pair.Item2 == 3;
        if (attack == "ram") attacker.speed = 2.8f;
        if (attack == "cherry") attacker.GiveCherry(8);
        if (attack == "rocket")
        {
            attacker.position = new UnityEngine.Vector2(-2, -1);
            victim.position = new UnityEngine.Vector2(1, -1);
            g.rockets[0].Spawn(new UnityEngine.Vector2(.5f, -1), new UnityEngine.Vector2(6, 0), attacker.index);
            for (int f = 0; f < 10; f++) g.Advance(dt, none, none);
        }
        else g.Advance(.001f, none, none);
        string label = (late ? "after grace " : "during grace ") + pair.Item1 + "->" + pair.Item2 + " " + attack;
        checks[label + " damage"] = allowed ? victim.health < 1 : victim.alive && victim.health == 1;
        checks[label + " score"] = allowed ? attacker.score > 0 : attacker.score == 0;
        if (attack == "cherry") checks[label + " wreck"] = victim.alive != allowed;
        if (attack == "rocket" && allowed) checks[label + " impact"] = !g.rockets[0].active && g.explosions[0].gameObject.activeSelf;
    }

    // A real AI match must spare the stationary leader until 10s, then hunt it.
    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    var p1 = g.cars[0]; p1.score = 1500;
    bool earlySafe = true, lateTarget = false;
    for (int f = 0; f < 60 * 25; f++)
    {
        p1.score = System.Math.Max(1500, p1.score);
        g.Advance(dt, none, none);
        foreach (var ai in g.cars)
            if (ai.computer && ai.Ai.Target == p1) { if (g.Elapsed < 10) earlySafe = false; else lateTarget = true; }
        if (g.Elapsed < 10 && (!p1.alive || p1.health != 1)) earlySafe = false;
    }
    checks["leader protected and never targeted during first ten seconds"] = earlySafe;
    checks["leader hunted between ten and twenty-five seconds"] = lateTarget;

    // The exact GO-time boundary releases protection for every attack, independent of held cherry state.
    start(9.998f);
    var owner = g.cars[2]; var target = g.cars[0]; owner.computer = true;

    target.position = new UnityEngine.Vector2(1, -1);
    g.rockets[0].Spawn(target.position, UnityEngine.Vector2.zero, owner.index);
    g.Advance(.001f, none, none);
    checks["rocket ignored immediately before grace boundary"] = target.health == 1 && g.rockets[0].active;
    g.Advance(.001f, none, none);
    checks["rocket damages at grace boundary"] = UnityEngine.Mathf.Abs(target.health - .62f) < .001f && !g.rockets[0].active;

    // A rocket already in flight remains dangerous after its owner is wrecked.
    start(10);
    owner = g.cars[2]; target = g.cars[0]; owner.computer = true;
    target.position = new UnityEngine.Vector2(1, -1);
    g.rockets[0].Spawn(target.position, UnityEngine.Vector2.zero, owner.index);
    g.DamageVehicle(owner, 1);
    g.Advance(.001f, none, none);
    checks["wrecked owner rocket still hits"] = !owner.alive && target.health < 1 && owner.score == 120;
}
finally { UnityEngine.Random.state = randomState; g.PreparePreview(); g.enabled = wasEnabled; }
return checks;
