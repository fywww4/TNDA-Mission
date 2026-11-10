// Ticket 10 (Play Mode, GAME3_Mode7): challenge mode flow through the public cabinet interface.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3_Mode7 open.");
UnityEngine.Application.runInBackground = true;
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
var log = new System.Text.StringBuilder(); int failed = 0;
System.Action<string, bool> check = (name, ok) => { if (!ok) failed++; log.AppendLine((ok ? "PASS " : "FAIL ") + name); };
var rocketOnly = TNDA.Prototype.Challenges.RocketOnly;
string bestKey = "TNDA.Challenge.rocket-only.Best";
bool hadBest = UnityEngine.PlayerPrefs.HasKey(bestKey); int oldBest = UnityEngine.PlayerPrefs.GetInt(bestKey, 0);
int oldGold = g.Gold; int oldCredits = g.Credits;
UnityEngine.PlayerPrefs.DeleteKey(bestKey);
try
{
    g.PreparePreview();
    g.Advance(dt, none, none);
    check("title starts on arcade mode", g.Mode == TNDA.Prototype.CabinetMode.Arcade && g.attractChallengePanel != null && !g.attractChallengePanel.activeSelf);
    check("arcade rule line is the coin rule", g.attractRule.text == TNDA.Prototype.CabinetText.CoinRule);

    g.SelectMode(TNDA.Prototype.CabinetMode.Challenge);
    g.Advance(dt, none, none);
    check("challenge mode shows the list", g.attractChallengePanel.activeSelf);
    check("list names all three", g.attractChallenges.text.Contains("只能用火箭") && g.attractChallenges.text.Contains("90 秒最高分") && g.attractChallenges.text.Contains("不被撞毀"));
    check("rocket-only is unlocked, others show the 150 price", g.attractChallenges.text.Contains("已解鎖") && System.Text.RegularExpressions.Regex.Matches(g.attractChallenges.text, "未解鎖 150 金幣").Count == 2);
    check("rule line says no coin needed", g.attractRule.text == TNDA.Prototype.CabinetText.ChallengeRule);

    // Locked challenges cannot start; coins unchanged.
    g.MoveChallengeCursor(2); // 不被撞毀: the 90 s challenge has no rules yet and says so instead
    g.Advance(dt, none, none);
    check("locked prompt shows the price", g.attractPrompt.text.Contains("150"));
    check("a locked challenge will not start", !g.TryStartChallenge(g.SelectedChallenge) && g.Phase == TNDA.Prototype.ArcadePhase.Attract);
    g.MoveChallengeCursor(-2);

    // Start with no coins.
    check("no coins inserted", g.Credits == oldCredits);
    check("rocket-only starts free", g.TryStartChallenge(g.SelectedChallenge));
    check("single player, challenge active", g.PlayerCount == 1 && g.ActiveChallenge == rocketOnly);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    check("coins untouched by a challenge start", g.Credits == oldCredits);
    g.Advance(dt, none, none);
    check("HUD board names the challenge", g.arenaBoard.text.Contains("只能用火箭"));
    check("only the final elimination is scheduled", g.RoundSeconds == 120 && g.NextElimination == 120);

    // Play to the end with a fixed score so the result is deterministic. Nothing is eliminated before the final.
    int eliminatedBeforeEnd = 0;
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing)
    {
        g.cars[0].health = 1; g.cars[0].score = 1234;
        g.Advance(dt, none, none);
        if (g.Elapsed < 119 && System.Linq.Enumerable.Any(g.cars, c => c.eliminated)) eliminatedBeforeEnd++;
    }
    check("nobody eliminated before 120 s", eliminatedBeforeEnd == 0);
    check("results phase reached", g.Phase == TNDA.Prototype.ArcadePhase.Results);
    check("no victory picture for a challenge", !g.victoryScreen.activeSelf);
    string body = g.overlayBody.text;
    check("results show the score", body.Contains("本次成績 01234"));
    check("results show the best with new-best mark", body.Contains("最佳紀錄 01234") && body.Contains("新紀錄"));
    check("results show earned and total gold", body.Contains("金幣") && g.RoundGold > 0 && g.Gold == oldGold + g.RoundGold);
    check("hint offers retry and list", g.overlayHint.text.Contains("重試") && g.overlayHint.text.Contains("Esc"));
    check("best record saved", UnityEngine.PlayerPrefs.GetInt(bestKey, 0) == 1234);

    // Retry runs the same challenge again; a lower score keeps the record.
    check("retry starts the same challenge", g.TryStartChallenge(g.ActiveChallenge) && g.ActiveChallenge == rocketOnly);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing) { g.cars[0].health = 1; g.cars[0].score = 500; g.Advance(dt, none, none); }
    check("lower score keeps the best", UnityEngine.PlayerPrefs.GetInt(bestKey, 0) == 1234 && !g.overlayBody.text.Contains("新紀錄") && g.overlayBody.text.Contains("本次成績 00500"));

    // Back to the list: the demo is an arcade match again.
    g.ShowAttract();
    g.Advance(dt, none, none);
    check("back at the title in challenge mode with no active challenge", g.Phase == TNDA.Prototype.ArcadePhase.Attract && g.Mode == TNDA.Prototype.CabinetMode.Challenge && g.ActiveChallenge == null);
    check("list shows the saved best", g.attractChallenges.text.Contains("最佳 01234"));

    // An arcade start afterwards has normal rules: three scheduled eliminations, crates can roll cherries.
    g.SelectMode(TNDA.Prototype.CabinetMode.Arcade);
    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    check("arcade rules restored", g.ActiveChallenge == null && g.NextElimination == 45 && g.RoundSeconds == 120);
    g.ShowAttract();
}
finally
{
    if (hadBest) UnityEngine.PlayerPrefs.SetInt(bestKey, oldBest); else UnityEngine.PlayerPrefs.DeleteKey(bestKey);
    UnityEngine.PlayerPrefs.SetInt("TNDA.Gold", oldGold);
    UnityEngine.PlayerPrefs.Save();
}
log.AppendLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
return log.ToString();
