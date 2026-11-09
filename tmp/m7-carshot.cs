// Play Mode: 1P gameplay a few seconds in, to check the 16-direction car sheet and seat colours in the chase view.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
g.enabled = false;
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
for (int f = 0; f < 60 * int.Parse(System.Environment.GetEnvironmentVariable("M7SEC") ?? "12"); f++) g.Advance(dt, none, none);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/m7car.png");
var sb = new System.Text.StringBuilder();
foreach (var c in g.cars) sb.Append(c.DriverName + " " + c.body.sprite.name + " | ");
return sb.ToString();
