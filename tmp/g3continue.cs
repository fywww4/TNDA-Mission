// CLI regression (Play Mode): eval_file tmp/g3continue.cs. Coins (5) and the paid continue: 1P costs 1 coin, 2P 2;
// the attract prompt follows the coins; a player eliminated at 60 s sees "P1 投幣繼續？" counting 10 → 1, the item
// button without a coin does nothing, with a coin it rejoins tied with the lowest score; letting it run out with no
// player left shows GAME OVER and returns to the title; in 2P the other player keeps playing; the final elimination
// gives no continue.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
var press = new TNDA.Prototype.ArcadeInput { action = true, actionPressed = true };
bool wasEnabled = g.enabled;
g.enabled = false;
var credits = typeof(TNDA.Prototype.ArcadeGame).GetProperty("Credits");
System.Action<int> setCredits = n => credits.SetValue(g, n);
System.Action skip = () => { while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none); };
// Runs until the 60 s elimination with P1 kept last (and unwrecked), then one more frame.
System.Action<TNDA.Prototype.ArcadeVehicle> runToFirstElimination = loser =>
{
    while (!loser.eliminated && g.Elapsed < 70)
    {
        foreach (var c in g.cars) { c.health = 1; c.score = c == loser ? 0 : 500 + c.index; }
        g.Advance(dt, none, none);
    }
};
try
{
    // Coins and the attract prompt.
    g.PreparePreview(); setCredits(0); g.Advance(dt, none, none);
    checks["prompt 0 coins"] = g.attractPrompt.text;
    g.InsertCoin(); g.Advance(dt, none, none);
    checks["prompt 1 coin"] = g.attractPrompt.text;
    checks["2P refused with 1 coin"] = !g.TryStartRun(2) && g.Phase == TNDA.Prototype.ArcadePhase.Attract;
    g.InsertCoin(); g.Advance(dt, none, none);
    checks["prompt 2 coins"] = g.attractPrompt.text;
    checks["2P start spends 2"] = g.TryStartRun(2) && g.Credits == 0 && g.PlayerCount == 2;
    g.PreparePreview();
    checks["1P refused with 0 coins"] = !g.TryStartRun(1);
    g.InsertCoin();
    checks["1P start spends 1"] = g.TryStartRun(1) && g.Credits == 0;

    // 1P: knocked out at 60 s, continue countdown, no coin, then pay and rejoin.
    skip();
    var p1 = g.cars[0];
    runToFirstElimination(p1);
    checks["P1 eliminated at ~60s"] = p1.eliminated + " t=" + g.Elapsed.ToString("0.0");
    checks["panel"] = g.continuePanel.activeSelf + " \"" + g.continueTitle.text + "\" \"" + g.continueCount.text + "\" \"" + g.continueHint.text + "\"";
    for (int f = 0; f < 61; f++) g.Advance(dt, none, none);
    checks["after 1s count"] = g.continueCount.text;
    g.Advance(dt, press, none);
    checks["button without coin keeps P1 out"] = p1.eliminated + " notice=\"" + g.notice.text + "\"";
    g.InsertCoin(); g.Advance(dt, none, none);
    checks["hint with coin"] = g.continueHint.text;
    float lowest = System.Linq.Enumerable.Min(System.Linq.Enumerable.Where(g.cars, c => !c.eliminated && c != p1), c => c.score);
    g.Advance(dt, press, none);
    checks["paid continue rejoins"] = (!p1.eliminated && p1.alive) + " score=" + p1.score + " lowest=" + lowest + " credits=" + g.Credits
        + " panel=" + g.continuePanel.activeSelf + " notice=\"" + g.notice.text + "\"";

    // 1P: let the countdown run out → GAME OVER → title.
    setCredits(1); g.TryStartRun(1); skip();
    runToFirstElimination(p1);
    for (int f = 0; f < 60 * 10 + 5; f++) g.Advance(dt, none, none);
    checks["timeout shows GAME OVER"] = g.continuePanel.activeSelf + " \"" + g.continueTitle.text + "\" phase=" + g.Phase;
    for (int f = 0; f < 60 * 2 + 5; f++) g.Advance(dt, none, none);
    checks["then title demo"] = g.Phase.ToString();

    // 2P: P1 out and not continuing, P2 still in → keep playing, no GAME OVER.
    setCredits(2); g.TryStartRun(2); skip();
    runToFirstElimination(p1);
    for (int f = 0; f < 60 * 13; f++) { g.cars[1].health = 1; g.Advance(dt, none, none); }
    checks["2P: P2 plays on"] = g.Phase + " panel=" + g.continuePanel.activeSelf + " P2 out=" + g.cars[1].eliminated;

    // Final elimination: no continue, results.
    setCredits(1); g.TryStartRun(1); skip();
    for (int guard = 0; guard < 60 * 130 && g.Phase == TNDA.Prototype.ArcadePhase.Playing; guard++)
    {
        foreach (var c in g.cars) { c.health = 1; c.score = c.index == 0 ? 600 : c.index == 3 ? 900 : 100 + c.index; }
        g.Advance(dt, none, none);
    }
    checks["final: P1 second, results, no panel"] = g.Phase + " P1 place=" + p1.FinalPlace + " panel=" + g.continuePanel.activeSelf;
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
setCredits(0);
g.enabled = wasEnabled;
g.PreparePreview();
return checks;
