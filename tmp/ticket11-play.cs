// Ticket 11: public cabinet flow in the isolated QA product (never use against the user's product save).
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (!UnityEngine.Application.isPlaying || UnityEngine.Application.productName != "TNDA Ticket11 QA 20261110")
    throw new System.InvalidOperationException("Use the isolated ticket11 QA product in Play Mode.");
g.enabled = false;
var idle = new TNDA.Prototype.ArcadeInput();
float dt = .25f;
var challenge = TNDA.Prototype.Challenges.NinetySecondScore;
var log = new System.Text.StringBuilder(); int failed = 0, checks = 0;
System.Action<string, bool> check = (name, ok) => { checks++; if (!ok) failed++; log.AppendLine((ok ? "PASS " : "FAIL ") + name); };
System.Action countdown = () => { for (int i = 0; g.Phase == TNDA.Prototype.ArcadePhase.Countdown && i < 40; i++) g.Advance(dt, idle, idle); };
// Keep the P1 idle and protected, outscored by every AI, so the actual final gives P1 last place.
System.Action<float> until = seconds => {
    for (int i = 0; g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.Elapsed < seconds && i < 600; i++) {
        g.cars[0].spawnProtection = 999;
        g.Advance(dt, idle, idle);
    }
};
System.Action<int> finish = score => {
    until(89.75f);
    for (int i = 0; i < g.cars.Length; i++) {
        g.cars[i].health = 1; g.cars[i].spawnProtection = 999;
        g.cars[i].score = i == 0 ? score : i * 10000;
    }
    g.Advance(dt, idle, idle);
};
g.PreparePreview(); g.SelectMode(TNDA.Prototype.CabinetMode.Challenge);
while (g.SelectedChallenge != challenge) g.MoveChallengeCursor(1);
g.Advance(dt, idle, idle);
check("locked list offers 150 gold", g.attractPrompt.text.Contains("150") && g.attractPrompt.text.Contains("解鎖"));
check("cannot start locked", !g.TryStartChallenge(challenge));
check("149 gold cannot unlock", g.Gold == 149 && !g.TryUnlockChallenge(challenge) && g.Gold == 149 && !g.Records.IsUnlocked(challenge));
check("short-gold notice", g.notice.text.Contains("金幣不足"));
// Earn the extra gold through the existing free rocket challenge; no private wallet injection.
check("free rocket challenge starts", g.TryStartChallenge(TNDA.Prototype.Challenges.RocketOnly));
countdown();
for (int tick = 0; g.Phase == TNDA.Prototype.ArcadePhase.Playing && tick < 600; tick++) {
    g.cars[0].score = 0; g.cars[0].spawnProtection = 999;
    for (int i = 1; i < 4; i++) g.cars[i].score = i * 10000;
    g.Advance(dt, idle, idle);
}
check("rocket fourth-place reward funds unlock", g.Phase == TNDA.Prototype.ArcadePhase.Results && g.RoundGold == 30 && g.Gold == 179);
g.ShowAttract();
check("unlock pays 150", g.TryUnlockChallenge(challenge) && g.Gold == 29);
check("unlock persists", new TNDA.Prototype.ChallengeRecords().IsUnlocked(challenge));
check("repeated unlock is free", g.TryUnlockChallenge(challenge) && g.Gold == 29);
int credits = g.Credits;
check("90s challenge starts free", g.TryStartChallenge(challenge) && g.Credits == credits && g.PlayerCount == 1);
check("eight cars visible and seven AI", g.Contenders == 8 && System.Linq.Enumerable.Count(g.cars, c => c.gameObject.activeSelf) == 8 && System.Linq.Enumerable.Count(g.cars, c => c.computer) == 7);
countdown();
check("countdown consumes none of the 90s", g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.Elapsed == 0);
until(12);
g.cars[0].score = 1000; g.cars[0].spawnProtection = 0; g.cars[0].shieldSeconds = 0;
g.DamageVehicle(g.cars[0], 2);
check("wreck costs 250 without ending challenge", !g.cars[0].alive && g.cars[0].score == 750 && g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.RoundGold == 0);
for (int i = 0; i < 13; i++) g.Advance(dt, idle, idle);
check("wrecked player respawns", g.cars[0].alive && g.Contenders == 8);
until(89.75f);
check("all eight remain before 90", g.Contenders == 8 && g.Elapsed == 89.75f && g.Phase == TNDA.Prototype.ArcadePhase.Playing);
finish(1234);
check("results at exactly 90s", g.Elapsed == 90 && g.Phase == TNDA.Prototype.ArcadePhase.Results);
check("AI wins and P1 ranks eighth", g.Champion == 7 && g.cars[0].FinalPlace == 8);
check("P1 record and new best shown", g.overlayBody.text.Contains("本次成績 01234") && g.overlayBody.text.Contains("最佳紀錄 01234") && g.overlayBody.text.Contains("新紀錄"));
check("P1 gets 10 place gold plus 12 score gold", g.RoundGold == 22 && g.Gold == 51 && g.overlayBody.text.Contains("22") && g.overlayBody.text.Contains("51"));
check("best survives reopening records", new TNDA.Prototype.ChallengeRecords().Best(challenge) == 1234);
for (int i = 0; i < 8; i++) g.Advance(dt, idle, idle);
check("results pay gold only once", g.Gold == 51 && g.RoundGold == 22);
check("retry free with same challenge", g.TryStartChallenge(g.ActiveChallenge) && g.ActiveChallenge == challenge && g.Credits == credits);
countdown(); finish(1000);
check("lower retry keeps best", g.Phase == TNDA.Prototype.ArcadePhase.Results && g.overlayBody.text.Contains("本次成績 01000") && g.overlayBody.text.Contains("最佳紀錄 01234") && !g.overlayBody.text.Contains("新紀錄"));
check("second run pays its own 20 gold", g.RoundGold == 20 && g.Gold == 71);
for (int i = 0; i < 81; i++) g.Advance(dt, idle, idle);
check("20s timeout returns to challenge list", g.Phase == TNDA.Prototype.ArcadePhase.Attract && g.Mode == TNDA.Prototype.CabinetMode.Challenge && g.ActiveChallenge == null);
check("list shows saved record", g.attractChallenges.text.Contains("90 秒最高分　已解鎖　最佳 01234"));
g.StartRun(1, TNDA.Prototype.Challenges.NoWreck); countdown();
check("no-wreck remains four cars with three hunters", g.Contenders == 4 && System.Linq.Enumerable.All(System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(g.cars, 1), 3), c => c.Ai.Personality == TNDA.Prototype.AiPersonality.Hunter));
g.StartRun(1); countdown();
for (int i = 0; i < 160; i++) { g.cars[0].spawnProtection = 999; g.cars[0].score = 999999; g.Advance(dt, idle, idle); }
check("arcade restores eight-car 40s elimination", g.ActiveChallenge == null && g.Contenders == 4 && g.Elapsed == 40);
g.PreparePreview(); g.SelectMode(TNDA.Prototype.CabinetMode.Challenge);
while (g.SelectedChallenge != challenge) g.MoveChallengeCursor(1);
g.Advance(dt, idle, idle);
log.AppendLine(checks + " checks; " + failed + " failures");
System.IO.File.WriteAllText("tmp/ticket11-play-results.txt", log.ToString());
return log.ToString();
