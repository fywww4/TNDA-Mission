var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
try
{
    g.enabled = false;
    for (int trial = 0; trial < 3; trial++)
    {
        g.StartRun(1);
        while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
        foreach (var car in g.cars) { car.computer = false; car.spawnProtection = 0; }
        var victim = g.cars[3];
        if (trial == 0) victim.GiveCherry(8);
        if (trial == 2) victim.shieldSeconds = 6;
        victim.position = new UnityEngine.Vector2(1, -1); victim.speed = 0;
        g.rockets[0].Spawn(new UnityEngine.Vector2(-.5f, -1), new UnityEngine.Vector2(6, 0), 0);
        for (int f = 0; f < 20 && g.rockets[0].active; f++) g.Advance(dt, none, none);
        checks[trial + " rocket hit"] = !g.rockets[0].active && g.explosions[0].gameObject.activeSelf;
        checks[trial + " damage"] = trial == 0 ? !victim.alive && victim.IsSpinningOut && !victim.HasCherry && g.cars[0].score == 420
            : trial == 1 ? victim.alive && UnityEngine.Mathf.Abs(victim.health - .62f) < .001f && g.cars[0].score == 120
            : victim.alive && victim.health == 1 && g.cars[0].score == 0;
        if (trial == 0) checks["lethal rocket keeps wreck state clean"] = victim.stun == 0 && victim.cooldown == 0;
    }
}
finally { g.PreparePreview(); g.enabled = wasEnabled; }
return checks;
