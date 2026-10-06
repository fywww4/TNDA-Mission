var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.BossClimbGame>();
g.enabled = false;
g.StartRun(1);
var idle = new TNDA.Prototype.ArcadeInput();
int revivedWhilePlaying = 0;
System.Action<float, float, int> move = (horizontal, vertical, frames) => {
    for (int i = 0; i < frames; i++) {
        if (g.Phase == TNDA.Prototype.ArcadePhase.Continue) { g.ContinueRun(); revivedWhilePlaying++; }
        g.Advance(.02f, new TNDA.Prototype.ArcadeInput { steer = horizontal, throttle = vertical }, idle);
    }
};
g.Advance(.02f, new TNDA.Prototype.ArcadeInput { throttle = 1, jumpPressed = true }, idle);
bool jumped = g.PlayerPosition.y > g.floors[0] + .4f;
move(0, 0, 60);
move(1, 0, 10);
move(0, 1, 65);
move(0, 0, 25);
var firstFloor = g.PlayerPosition;
move(1, 0, 120);
move(0, 1, 65);
move(0, 0, 25);
var secondFloor = g.PlayerPosition;
move(-1, 0, 120);
move(0, 1, 55);
move(0, 0, 25);
var thirdFloor = g.PlayerPosition;
int hpBeforeShooting = g.BossHealth;
int maxActors = 0;
for (int i = 0; i < 1200 && g.Kills == 0; i++) {
    if (g.Phase == TNDA.Prototype.ArcadePhase.Continue) { g.ContinueRun(); revivedWhilePlaying++; }
    float aim = UnityEngine.Mathf.Clamp((g.boss.transform.position.x - g.PlayerPosition.x) / (.02f * g.moveSpeed), -1, 1);
    g.Advance(.02f, new TNDA.Prototype.ArcadeInput { steer = aim, action = true }, idle);
    int actors = 2;
    foreach (var p in g.shots) if (p.active) actors++;
    foreach (var p in g.hazards) if (p.active) actors++;
    maxActors = System.Math.Max(maxActors, actors);
}
int shotsAfterKill = 0, hazardsAfterKill = 0;
foreach (var p in g.shots) if (p.active) shotsAfterKill++;
foreach (var p in g.hazards) if (p.active) hazardsAfterKill++;
bool bossHiddenAfterKill = !g.boss.gameObject.activeSelf;
move(0, 0, 65);
bool nextBossSpawned = g.boss.gameObject.activeSelf && g.BossHealth == g.BossMaxHealth && g.BossMaxHealth > hpBeforeShooting;
g.boss.transform.position = new UnityEngine.Vector3(g.PlayerPosition.x, 3.23f, 0);
for (int i = 0; i < 35; i++) g.Advance(.02f, new TNDA.Prototype.ArcadeInput { action = true }, idle);
for (int hit = 0; hit < 10 && g.Phase == TNDA.Prototype.ArcadePhase.Playing; hit++) {
    move(0, 0, 70);
    g.DamagePlayer();
}
int retainedBossHealth = g.BossHealth, retainedKills = g.Kills;
var retainedPosition = g.PlayerPosition;
float elapsedAtDeath = g.Elapsed;
float hazardAge = g.hazards[0].age;
g.Advance(1, idle, idle);
bool frozeOnDeath = g.Elapsed == elapsedAtDeath && g.hazards[0].age == hazardAge;
bool countdownStarted = g.Phase == TNDA.Prototype.ArcadePhase.Continue && g.ContinueSeconds <= 9.01f;
g.ContinueRun();
bool retainedProgress = g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.PlayerHealth == 3
    && g.BossHealth == retainedBossHealth && g.Kills == retainedKills && g.PlayerPosition == retainedPosition && g.Invulnerability == 2;
for (int hit = 0; hit < 10 && g.Phase == TNDA.Prototype.ArcadePhase.Playing; hit++) {
    move(0, 0, 105);
    g.DamagePlayer();
}
for (int i = 0; i < 501 && g.Phase == TNDA.Prototype.ArcadePhase.Continue; i++) g.Advance(.02f, idle, idle);
bool timedOutToResults = g.Phase == TNDA.Prototype.ArcadePhase.Results;
for (int i = 0; i < 155; i++) g.Advance(.02f, idle, idle);
bool returnedToTitle = g.Phase == TNDA.Prototype.ArcadePhase.Attract;
return new {
    jumped, firstFloor = firstFloor.ToString(), secondFloor = secondFloor.ToString(), thirdFloor = thirdFloor.ToString(),
    bossDefeatedByShots = retainedKills > 0, bossHiddenAfterKill, shotsAfterKill, hazardsAfterKill, nextBossSpawned,
    maxActors, revivedWhilePlaying, frozeOnDeath, countdownStarted, retainedProgress, timedOutToResults, returnedToTitle
};
