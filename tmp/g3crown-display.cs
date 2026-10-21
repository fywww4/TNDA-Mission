var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
g.enabled = false;
var none = new TNDA.Prototype.ArcadeInput();
g.StartRun(1);
while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(1f / 60, none, none);
foreach (var c in g.cars) { c.computer = false; c.spawnProtection = c.shieldSeconds = 0; }
for (int i = 0; i < 240; i++) g.Advance(1f / 60, none, none);
var spots = new[] { new UnityEngine.Vector2(-2.7f, -2.6f), new UnityEngine.Vector2(2.7f, -2.6f),
    new UnityEngine.Vector2(-1, -.7f), new UnityEngine.Vector2(2.7f, .6f) };
for (int i = 0; i < g.cars.Length; i++)
{
    var c = g.cars[i]; c.position = spots[i]; c.heading = 125; c.speed = 0; c.score = i == 2 ? 800 : 0;
}
foreach (var p in g.props) { p.broken = true; p.respawn = 1000; }
foreach (var p in g.pickups) { p.available = false; p.respawn = 1000; }
g.Advance(1f / 60, none, none);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3crown-current.png");
var s = g.crownView.sprite;
return new { frame = UnityEngine.Time.frameCount, pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
    ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName,
    screen = UnityEngine.Screen.width + "x" + UnityEngine.Screen.height,
    crownScreen = g.sceneCamera.WorldToScreenPoint(g.crownView.transform.position).ToString(),
    bounds = s.bounds.ToString(), vertices = System.Linq.Enumerable.Select(s.vertices, v => v.ToString()).ToArray(),
    uv = System.Linq.Enumerable.Select(s.uv, v => v.ToString()).ToArray(),
    height = g.crownHeight, filter = s.texture.filterMode.ToString() };
