// Captures 1P gameplay 15 s in, to check the English driver names on the leaderboard, car tags and HUD, then (second
// call) the continue panel with the longest name.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
g.enabled = false;
g.StartRun(2); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
for (int f = 0; f < 60 * 15; f++) g.Advance(dt, none, none);
g.cars[0].item = TNDA.Prototype.ArenaItem.Shield;
g.Advance(dt, none, none);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3names.png");
return "captured " + string.Join(",", System.Linq.Enumerable.Select(g.cars, c => c.DriverName)) + " | hud=" + g.hud.text.Replace("\n", " / ");
