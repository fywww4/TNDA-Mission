var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
try
{
    g.enabled = false;
    // Both a player and an AI use the same three-second wreck/respawn lifecycle.
    foreach (int index in new[] { 0, 2 })
    {
        g.StartRun(1);
        while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
        var car = g.cars[index];
        car.spawnProtection = car.shieldSeconds = 0;
        car.score = 1000;
        car.GiveCherry(8);
        car.ApplySlide(UnityEngine.Vector2.up * 2);
        g.DamageVehicle(car, 1);
        checks[index + " wreck"] = !car.alive && car.IsSpinningOut && car.gameObject.activeSelf && car.score == 750
            && !car.HasCherry && car.SlideVelocity == UnityEngine.Vector2.zero && g.Wrecks == 1;
        float heading = car.heading;
        g.Advance(.25f, none, none);
        checks[index + " spins four turns per second"] = UnityEngine.Mathf.Abs(car.heading - heading - 360) < .01f;
        for (int f = 0; f < 105; f++) g.Advance(dt, none, none);
        checks[index + " hides before respawn"] = !car.alive && !car.gameObject.activeSelf && !car.IsSpinningOut;
        for (int f = 0; f < 59; f++) g.Advance(dt, none, none);
        checks[index + " stays dead before three seconds"] = !car.alive;
        g.Advance(.025f, none, none);
        checks[index + " respawns with retained score"] = car.alive && car.gameObject.activeSelf && car.health == 1
            && car.score == 750 && car.spawnProtection == 2 && car.computer == (index == 2);
        g.Advance(.13f, none, none);
        bool shown = g.shieldViews[index].gameObject.activeSelf;
        g.Advance(.125f, none, none);
        checks[index + " protection blinks"] = shown != g.shieldViews[index].gameObject.activeSelf;
        g.DamageVehicle(car, 1);
        checks[index + " respawn protection blocks damage"] = car.alive && car.health == 1;
    }
    g.PreparePreview();
    g.Advance(dt, none, none);
    var demoCar = g.cars[0];
    demoCar.spawnProtection = demoCar.shieldSeconds = 0;
    g.DamageVehicle(demoCar, 1);
    for (int f = 0; f < 190; f++) g.Advance(dt, none, none);
    checks["demo respawn keeps AI"] = demoCar.alive && demoCar.computer && g.Phase == TNDA.Prototype.ArcadePhase.Attract;
}
finally { g.PreparePreview(); g.enabled = wasEnabled; }
return checks;
