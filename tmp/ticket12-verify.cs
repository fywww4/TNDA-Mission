// Ticket 12 (Play Mode, GAME3_Mode7): unlocking 不被撞毀 with gold and its pass/fail results, through the public cabinet.
// Saves (gold, unlock, best) are restored afterwards.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3_Mode7 open.");
UnityEngine.Application.runInBackground = true;
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
var log = new System.Text.StringBuilder(); int failed = 0;
System.Action<string, bool> check = (name, ok) => { if (!ok) failed++; log.AppendLine((ok ? "PASS " : "FAIL ") + name); };
var noWreck = TNDA.Prototype.Challenges.NoWreck;
string bestKey = "TNDA.Challenge.no-wreck.Best", unlockKey = "TNDA.Challenge.no-wreck.Unlocked";
bool hadBest = UnityEngine.PlayerPrefs.HasKey(bestKey); int oldBest = UnityEngine.PlayerPrefs.GetInt(bestKey, 0);
bool hadUnlock = UnityEngine.PlayerPrefs.HasKey(unlockKey); int oldUnlock = UnityEngine.PlayerPrefs.GetInt(unlockKey, 0);
int oldGold = g.Gold;
var wallet = typeof(TNDA.Prototype.ArcadeGame).GetField("wallet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
System.Action<int> setGold = gold => { UnityEngine.PlayerPrefs.SetInt("TNDA.Gold", gold); wallet.SetValue(g, null); }; // reload the wallet from the save
UnityEngine.PlayerPrefs.DeleteKey(bestKey); UnityEngine.PlayerPrefs.DeleteKey(unlockKey);
try
{
    g.PreparePreview();
    g.SelectMode(TNDA.Prototype.CabinetMode.Challenge);
    while (g.SelectedChallenge != noWreck) g.MoveChallengeCursor(1);
    setGold(149);
    g.Advance(dt, none, none);
    check("locked prompt offers the 150 gold unlock", g.attractPrompt.text.Contains("150") && g.attractPrompt.text.Contains("解鎖"));
    check("149 gold cannot unlock", !g.TryUnlockChallenge(noWreck) && !g.Records.IsUnlocked(noWreck) && g.Gold == 149);
    check("short-gold notice", g.notice.text.Contains("金幣不足"));
    check("locked will not start", !g.TryStartChallenge(noWreck));

    // All three challenges are now playable; ticket11-play.cs covers the 90-second unlock and rules.
    setGold(200);

    check("200 gold unlocks", g.TryUnlockChallenge(noWreck) && g.Records.IsUnlocked(noWreck) && g.Gold == 50);
    check("unlock saved", UnityEngine.PlayerPrefs.GetInt(unlockKey, 0) == 1 && new TNDA.Prototype.ChallengeRecords().IsUnlocked(noWreck));
    g.Advance(dt, none, none);
    check("list shows it unlocked with no pass yet", g.attractChallenges.text.Contains("不被撞毀　已解鎖　最佳 未過關"));

    // A run that lasts: the player is kept unhurt below 1 and outscored, and still passes at 120 s.
    check("starts", g.TryStartChallenge(noWreck));
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    var ais = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(g.cars, c => c.computer && c.gameObject.activeSelf));
    check("three computer cars, all 追王", ais.Length == 3 && System.Linq.Enumerable.All(ais, c => c.Ai.Personality == TNDA.Prototype.AiPersonality.Hunter));
    bool chasedPlayer = false, chasedOther = false;
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing)
    {
        g.cars[0].health = .64f; g.cars[0].spawnProtection = 1;
        foreach (var ai in ais) { ai.score = 5000; var t = ai.Ai.Target; if (t == g.cars[0]) chasedPlayer = true; else if (t != null) { chasedOther = true; log.AppendLine("  " + ai.name + " -> " + t.name + " at " + g.Elapsed.ToString("0.0") + (ai.HasCherry ? " cherry" : "")); } }
        g.Advance(dt, none, none);
    }
    check("the 追王 chased the player", chasedPlayer);
    check("and nobody else", !chasedOther);
    check("results after 120 s", g.Phase == TNDA.Prototype.ArcadePhase.Results && g.Elapsed >= 119.9f);
    check("results show durability left and new record", g.overlayBody.text.Contains("本次成績 耐久 64%") && g.overlayBody.text.Contains("最佳紀錄 耐久 64%") && g.overlayBody.text.Contains("新紀錄"));
    check("first-place gold paid", g.RoundGold >= 100 && g.Gold == 50 + g.RoundGold);
    check("best saved as 64", UnityEngine.PlayerPrefs.GetInt(bestKey, 0) == 64);

    // A retry that wrecks the player fails at once and keeps the record.
    check("retry", g.TryStartChallenge(g.ActiveChallenge));
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    for (int i = 0; i < 60 * 15; i++) { g.cars[0].spawnProtection = 1; g.Advance(dt, none, none); }
    g.cars[0].spawnProtection = 0; g.cars[0].shieldSeconds = 0;
    int gold = g.Gold;
    g.DamageVehicle(g.cars[0], 2);
    check("wreck ends the run at once", g.Phase == TNDA.Prototype.ArcadePhase.Results && g.Elapsed < 16);
    check("failed results", g.overlayBody.text.Contains("本次成績 未過關") && g.overlayBody.text.Contains("最佳紀錄 耐久 64%") && !g.overlayBody.text.Contains("新紀錄"));
    check("last-place gold paid", g.RoundGold >= 30 && g.RoundGold < 100 && g.Gold == gold + g.RoundGold);
    check("hint offers retry", g.overlayHint.text.Contains("重試"));

    g.ShowAttract();
    g.Advance(dt, none, none);
    check("list shows the record", g.attractChallenges.text.Contains("最佳 耐久 64%"));
}
finally
{
    if (hadBest) UnityEngine.PlayerPrefs.SetInt(bestKey, oldBest); else UnityEngine.PlayerPrefs.DeleteKey(bestKey);
    if (hadUnlock) UnityEngine.PlayerPrefs.SetInt(unlockKey, oldUnlock); else UnityEngine.PlayerPrefs.DeleteKey(unlockKey);
    setGold(oldGold);
    UnityEngine.PlayerPrefs.Save();
}
log.AppendLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
return log.ToString();
