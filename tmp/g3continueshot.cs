// Captures the continue countdown (P1 knocked out at 60 s, 8 left) and, in the next call, the attract prompt with 2 coins.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
g.enabled = false;
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
var p1 = g.cars[0];
while (!p1.eliminated) { foreach (var c in g.cars) { c.health = 1; c.score = c == p1 ? 0 : 500 + c.index; } g.Advance(dt, none, none); }
for (int f = 0; f < 60 * 2 + 10; f++) g.Advance(dt, none, none);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3continue.png");
return "captured " + g.continueCount.text;
