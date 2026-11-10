var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Play Mode required");
g.enabled = false;
var stage = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Stage>();
string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../tmp"));
System.IO.Directory.CreateDirectory(dir);
var state = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "ticket07-state.txt")).Trim();
float dt = 1f / 60;
var idle = new TNDA.Prototype.ArcadeInput();
var log = new System.Text.StringBuilder();
int failed = 0, passed = 0;
System.Action<string, bool> check = (name, ok) => { log.AppendLine((ok ? "PASS " : "FAIL ") + name); if (ok) passed++; else failed++; };
System.Action<float> run = seconds => { for (int f = 0; f < seconds * 60; f++) g.Advance(dt, idle, idle); };
System.Action<int> start = humans => { UnityEngine.Random.InitState(17 + humans); g.StartRun(humans); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, idle, idle); };
if (state.StartsWith("start"))
{
    int humans = state == "start2" ? 2 : 1;
    start(humans);
    check("8 arcade seats", g.Contenders == 8);
    check("4 pool slots, 5 props, 4 crates", g.rockets.Length == 4 && g.props.Length == 5 && g.pickups.Length == 4);
    for (int f = 0; f < 20 * 60; f++)
    {
        var p1 = new TNDA.Prototype.ArcadeInput { throttle = 1, steer = UnityEngine.Mathf.Sin(f * .021f), actionPressed = f % 240 == 120 };
        var p2 = new TNDA.Prototype.ArcadeInput { throttle = 1, steer = UnityEngine.Mathf.Sin(f * .021f + 1.7f), actionPressed = f % 240 == 120 };
        g.Advance(dt, p1, p2);
    }
    check("playing after 20s", g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.Elapsed > 19);
    check("board names all eight", System.Linq.Enumerable.All(g.cars, c => g.leaderboard.text.Contains(c.DriverName + " ")));
    check("four visible score lines", g.leaderboard.text.Split('\n').Length == 4);
    check("eight unique seat names", System.Linq.Enumerable.Select(g.cars, c => c.DriverName).Distinct().Count() == 8);
    check("correct view count", ((TNDA.Prototype.ICabinetProjection)stage).Views == humans);
}
else if (state == "finish")
{
    // Keep the human players in the top two to inspect every elimination and the final standings. AI still drives,
    // collides, fires and scores normally; this fixture controls standings, not the production game.
    while (g.Elapsed < 39.9f) { for (int i = 0; i < g.PlayerCount; i++) g.cars[i].score = 10000 - i * 1000; run(dt); }
    run(.2f);
    check("40s leaves four", g.Contenders == 4);
    check("four eliminated with places and retained scores", System.Linq.Enumerable.Count(g.cars, c => c.eliminated && c.FinalPlace >= 5) == 4);
    while (g.Elapsed < 79.9f) { for (int i = 0; i < g.PlayerCount; i++) g.cars[i].score = 10000 - i * 1000; run(dt); }
    run(.2f);
    check("80s leaves two", g.Contenders == 2);
    while (g.Elapsed < 120 && g.Phase == TNDA.Prototype.ArcadePhase.Playing && !g.victoryScreen.activeSelf)
    { for (int i = 0; i < g.PlayerCount; i++) g.cars[i].score = 10000 - i * 1000; run(dt); }
    check("120s champion and victory", g.Champion == 0 && g.victoryScreen.activeSelf);
    run(3.2f);
    check("results", g.Phase == TNDA.Prototype.ArcadePhase.Results);
    check("all eight result places", System.Linq.Enumerable.All(g.cars, c => g.overlayBody.text.Contains(c.FinalPlace + ". " + c.DriverName)));
    check("result rules show pyramid", g.overlayBody.text.Contains("40 秒淘汰 4 台") && g.overlayBody.text.Contains("80 秒淘汰 2 台") && g.overlayBody.text.Contains("120 秒決賽"));
}
else if (state == "challenge")
{
    g.TryStartChallenge(TNDA.Prototype.Challenges.RocketOnly);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, idle, idle);
    run(.1f);
    check("rocket challenge still has four contenders", g.Contenders == 4);
    check("extra cars hidden", System.Linq.Enumerable.All(System.Linq.Enumerable.Skip(g.cars, 4), c => !c.gameObject.activeSelf));
    check("extra seats absent from board", !g.leaderboard.text.Contains("P5") && !g.leaderboard.text.Contains("P8"));
    check("challenge still ends at 120", g.NextElimination == 120);
    start(1);
    check("arcade restores eight and 40s", g.Contenders == 8 && g.NextElimination == 40 && System.Linq.Enumerable.All(g.cars, c => c.gameObject.activeSelf));
}
else if (state == "attract")
{
    g.PreparePreview();
    run(dt);
    check("eight AI in attract", g.Phase == TNDA.Prototype.ArcadePhase.Attract && g.Contenders == 8 && System.Linq.Enumerable.All(g.cars, c => c.computer));
    run(21);
    check("attract demonstrates four eliminations", g.Contenders == 4 && g.Phase == TNDA.Prototype.ArcadePhase.Attract);
    run(10);
    check("demo loop restores eight", g.Contenders == 8);
}
else throw new System.InvalidOperationException("Unknown state " + state);
check("screen text has no missing glyphs", System.Linq.Enumerable.All(new[] { g.leaderboard, g.hud, g.overlayBody }, t => System.Linq.Enumerable.All(t.text.Where(c => !char.IsControl(c)), c => t.font.HasCharacter(c))));
System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ticket07-" + state + "-" + g.PlayerCount + ".txt"), log.ToString());
return new { state, passed, failed, g.Phase, g.Elapsed, g.Contenders, g.Champion, frame = UnityEngine.Time.frameCount, log = log.ToString() };
