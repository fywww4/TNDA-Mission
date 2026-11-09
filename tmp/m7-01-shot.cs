// Ticket 01 (Play Mode, GAME3_Mode7): puts the game in the state named in tmp/m7-01-state.txt and captures
// tmp/m7-01-<state>.png at the 512x448 Game view. Run it twice: the first call sets the state up, the second
// (a frame later, once the chase cameras have caught up) advances one frame and captures. States: attract, countdown, play1, play2, continue, victory, results.
var state = System.IO.File.ReadAllText("tmp/m7-01-state.txt").Trim();
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3_Mode7 open.");
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
System.Action<int> start = n => { g.StartRun(n); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none); };
System.Action<float> run = s => { for (int f = 0; f < 60 * s; f++) g.Advance(dt, none, none); };
// Whole match with fixed standings until decided (the given seat leads).
System.Action<int, int> decide = (players, leader) =>
{
    start(players);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Playing && !g.victoryScreen.activeSelf)
    {
        foreach (var c in g.cars) { c.health = 1; c.score = c.index == leader ? 5000 : c.index == 0 ? 4000 : 100 * (c.index + 1); }
        g.Advance(dt, none, none);
    }
};
bool capture = state.StartsWith("capture ");
if (capture) state = state.Substring(8);
if (!capture) g.enabled = state == "attract";
if (capture) { if (!g.enabled) g.Advance(dt, none, none); }
else switch (state)
{
    case "attract": break;
    case "countdown": g.StartRun(1); for (int f = 0; f < 70; f++) g.Advance(dt, none, none); break;
    case "play1": start(1); run(12); break;
    case "play2": start(2); run(12); break;
    case "continue":
        start(1);
        var p1 = g.cars[0];
        while (!p1.eliminated) { foreach (var c in g.cars) { c.health = 1; c.score = c == p1 ? 0 : 500 + c.index; } g.Advance(dt, none, none); }
        run(2);
        break;
    case "victory": decide(1, 0); run(.5f); break;
    case "results": decide(1, 3); run(.5f); break;
}
if (capture) UnityEngine.ScreenCapture.CaptureScreenshot("tmp/m7-01-" + state + ".png");
var sb = new System.Text.StringBuilder(state + " phase=" + g.Phase + " screen=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
var scaler = g.header.canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
sb.Append(" scaler=" + scaler.uiScaleMode + "@" + scaler.scaleFactor);
foreach (var c in g.cars) sb.Append(" | " + c.DriverName + (c.eliminated ? " out" : ""));
return sb.ToString();
