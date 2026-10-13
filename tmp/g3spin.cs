var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
float first = g.firstElimination, second = g.secondElimination, round = g.roundSeconds, stay = g.presentationSeconds;
bool hadBest = UnityEngine.PlayerPrefs.HasKey("WreckBest");
int best = UnityEngine.PlayerPrefs.GetInt("WreckBest");
try
{
    g.enabled = false;
    g.firstElimination = 1; g.secondElimination = 6; g.roundSeconds = 10; g.presentationSeconds = 20;
    g.StartRun(2);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) car.computer = false; // Park all cars to exercise deterministic tie-breaking.
    while (g.Elapsed < 1) g.Advance(dt, none, none);
    var loser = g.cars[3];
    checks["index tie-break and fourth place"] = loser.eliminated && loser.FinalPlace == 4 && g.Contenders == 3;
    checks["elimination begins visible spin"] = !loser.alive && loser.IsSpinningOut && loser.gameObject.activeSelf;
    g.Advance(.25f, none, none);
    checks["leaderboard retains eliminated rank"] = g.leaderboard.text.Contains("淘汰 第4名");
    for (int f = 0; f < 240; f++) g.Advance(dt, none, none);
    checks["eliminated car never respawns"] = loser.eliminated && !loser.alive && !loser.gameObject.activeSelf
        && !loser.ReadyToRespawn && loser.FinalPlace == 4;
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing) g.Advance(dt, none, none);
    checks["final standings"] = g.Champion == 0 && g.cars[0].FinalPlace == 1 && g.cars[1].FinalPlace == 2
        && g.cars[2].FinalPlace == 3 && g.cars[3].FinalPlace == 4;
    checks["results show champion"] = g.overlayBody.text.Contains("1. P1") && g.Phase == TNDA.Prototype.ArcadePhase.Results;
    g.Advance(20.1f, none, none);
    checks["returns to title"] = g.Phase == TNDA.Prototype.ArcadePhase.Attract;

    // Durability precedes index when scores tie, including a car already waiting to respawn.
    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) { car.computer = false; car.spawnProtection = 0; }
    g.DamageVehicle(g.cars[1], 1);
    while (g.Elapsed < 1) g.Advance(dt, none, none);
    checks["wrecked low-health car can be eliminated"] = g.cars[1].eliminated && g.cars[1].FinalPlace == 4;
    while (g.Elapsed < 4.5f) g.Advance(dt, none, none);
    checks["elimination cancels pending respawn"] = !g.cars[1].alive && !g.cars[1].ReadyToRespawn;
    g.StartRun(1);
    checks["restart resets all ranks"] = System.Linq.Enumerable.All(g.cars, c => c.FinalPlace == 0 && !c.eliminated && c.alive);
}
finally
{
    g.firstElimination = first; g.secondElimination = second; g.roundSeconds = round; g.presentationSeconds = stay;
    if (hadBest) UnityEngine.PlayerPrefs.SetInt("WreckBest", best); else UnityEngine.PlayerPrefs.DeleteKey("WreckBest");
    g.PreparePreview(); g.enabled = wasEnabled;
}
return checks;
