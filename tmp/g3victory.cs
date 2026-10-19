// CLI regression (Play Mode): eval_file tmp/g3victory.cs. Victory picture (2026-10-19): a player champion sees the
// picture without a title (Player2 with the yellow-car copy) for 3 s while the match stands still, then the results; a computer
// champion goes straight to the results. Captures tmp/g3victory.png during a Player1 win.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled; g.enabled = false;
UnityEngine.Application.runInBackground = true;
// Plays a whole match with fixed standings (the given seat leads) until the match is decided.
System.Action<int, int> play = (players, leader) =>
{
    g.StartRun(players); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing && !g.victoryScreen.activeSelf)
    {
        foreach (var c in g.cars) { c.health = 1; c.score = c.index == leader ? 5000 : c.index == 0 ? 4000 : 100 * (c.index + 1); }
        g.Advance(dt, none, none);
    }
};
try
{
    play(1, 0);
    checks["P1 win: picture, no title"] = g.victoryScreen.activeSelf + " \"" + g.victoryScore.text + "\" sprite=" + g.victoryImage.sprite.name + " phase=" + g.Phase;
    var before = g.cars[2].position;
    for (int f = 0; f < 60; f++) g.Advance(dt, none, none);
    checks["match stands still behind it"] = UnityEngine.Vector2.Distance(before, g.cars[2].position) < .001f;
    for (int f = 0; f < 60 * 2 + 5; f++) g.Advance(dt, none, none);
    checks["after 3 s: results, picture gone"] = g.Phase + " picture=" + g.victoryScreen.activeSelf + " overlay=" + g.overlay.activeSelf + " \"" + g.overlayTitle.text + "\"";

    play(2, 1);
    checks["P2 win: yellow-car picture"] = g.victoryScreen.activeSelf + " sprite=" + g.victoryImage.sprite.name;

    play(1, 3);
    checks["computer win: straight to results"] = g.Phase + " picture=" + g.victoryScreen.activeSelf + " \"" + g.overlayTitle.text + "\"";

    play(1, 0);
    for (int f = 0; f < 30; f++) g.Advance(dt, none, none);
    UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3victory.png");
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
return checks;
