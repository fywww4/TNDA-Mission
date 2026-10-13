// Public lifecycle seam: a wreck clears transient driving/item state in one call.
var game = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
bool wasEnabled = game.enabled;
var checks = new System.Collections.Generic.Dictionary<string, bool>();
try
{
    game.enabled = false;
    game.StartRun(1);
    var car = game.cars[0];
    car.spawnProtection = car.shieldSeconds = 0;
    car.score = 1000;
    car.item = TNDA.Prototype.ArenaItem.Rocket;
    car.stun = .5f;
    car.cooldown = .7f;
    game.DamageVehicle(car, 1);
    checks["wreck keeps deducted score"] = !car.alive && car.score == 750;
    checks["wreck clears transient state"] = car.item == TNDA.Prototype.ArenaItem.None && car.stun == 0 && car.cooldown == 0;

    var rules = new TNDA.Prototype.VehicleLifecycleRules {
        wreckPenalty = 250, respawnSeconds = 3, spinStartSpeed = 4,
        spinAcceleration = 14, spinTurnsPerSecond = 4, arenaCentre = UnityEngine.Vector2.zero
    };
    car.ResetVehicle(UnityEngine.Vector2.right, 0, 1);
    car.GiveCherry(8);
    car.ApplySlide(new UnityEngine.Vector2(2, 1));
    car.AdvanceTimers(.5f);
    checks["cherry timers and protection"] = car.HasCherry && car.CherrySeconds == 7.5f && car.spawnProtection == 1.5f;
    checks["protected damage ignored"] = !car.TakeDamage(1, rules) && car.alive && car.health == 1;
    car.AdvanceTimers(1.5f);
    car.score = 1000;
    checks["lethal damage applied"] = car.TakeDamage(1, rules);
    checks["wreck starts spin and clears powers"] = !car.alive && car.IsSpinningOut && car.gameObject.activeSelf
        && car.score == 750 && car.respawn == 3 && !car.HasCherry && car.CherrySeconds == 0
        && car.SlideVelocity == UnityEngine.Vector2.zero && car.cooldown == 0 && car.spawnProtection == 0;
    car.ApplySlide(UnityEngine.Vector2.up);
    car.GiveCherry(8);
    checks["dead cars reject power and slide"] = car.CherrySeconds == 0 && car.SlideVelocity == UnityEngine.Vector2.zero;
    car.AdvanceSpinOut(.25f, rules, UnityEngine.Vector2.zero, 6);
    checks["spin motion unchanged"] = UnityEngine.Vector2.Distance(car.position, new UnityEngine.Vector2(2.875f, 0)) < .001f
        && UnityEngine.Mathf.Abs(car.heading - 360) < .001f;
    for (int f = 0; f < 120; f++) car.AdvanceSpinOut(1f / 60, rules, UnityEngine.Vector2.zero, 6);
    checks["spin hides off screen"] = !car.IsSpinningOut && !car.gameObject.activeSelf;
    car.AdvanceTimers(2.99f);
    checks["no early respawn"] = !car.ReadyToRespawn;
    car.AdvanceTimers(.02f);
    checks["respawn due after three seconds"] = car.ReadyToRespawn;
    car.Respawn(new UnityEngine.Vector2(-3.5f, 1.1f), 0);
    checks["respawn restores protected car and score"] = car.alive && car.gameObject.activeSelf && car.health == 1
        && car.spawnProtection == 2 && car.score == 750 && !car.IsSpinningOut && car.respawn == 0;
    // Respawn can interrupt a fly-out when tuned to occur before it reaches the edge.
    car.spawnProtection = 0;
    car.TakeDamage(1, rules);
    car.Respawn(UnityEngine.Vector2.left, 0);
    car.AdvanceSpinOut(1, rules, UnityEngine.Vector2.zero, 6);
    checks["respawn cancels residual spin"] = car.alive && !car.IsSpinningOut && car.position == UnityEngine.Vector2.left;

    car.GiveCherry(8);
    car.ApplySlide(UnityEngine.Vector2.up);
    float retainedScore = car.score;
    car.Eliminate(4, rules);
    car.AdvanceTimers(100);
    car.Respawn(UnityEngine.Vector2.zero, 0);
    checks["elimination is permanent"] = car.eliminated && !car.alive && !car.ReadyToRespawn
        && car.FinalPlace == 4 && car.score == retainedScore && car.CherrySeconds == 0
        && car.SlideVelocity == UnityEngine.Vector2.zero && car.respawn == 0;
    car.ResetVehicle(UnityEngine.Vector2.zero, 90, 0);
    checks["new demo resets elimination and driver"] = !car.eliminated && car.alive && car.FinalPlace == 0 && car.computer && car.score == 0;
    car.spawnProtection = 0;
    car.score = 100;
    car.TakeDamage(1, rules);
    checks["penalty floors score at zero"] = car.score == 0;
    car.Respawn(UnityEngine.Vector2.zero, 90);
    checks["demo respawn preserves AI driver"] = car.computer && car.alive;

    car.ApplySlide(new UnityEngine.Vector2(2, 0));
    car.AdvanceSlide(.25f, 4.5f);
    checks["slide moves and decays"] = car.position == new UnityEngine.Vector2(.5f, 0)
        && car.SlideVelocity.x > .64f && car.SlideVelocity.x < .66f;
    car.ReboundSlide(true, false);
    checks["wall soft rebound"] = car.SlideVelocity.x > -.20f && car.SlideVelocity.x < -.19f;
}
finally
{
    game.PreparePreview();
    game.enabled = wasEnabled;
}
return checks;
