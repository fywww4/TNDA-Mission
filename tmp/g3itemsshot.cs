// Captures the held-item icons (P1 shield, 迷糊 rocket, 追王 cherry held) and a striped cherry-power car (碰碰).
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
g.enabled = false;
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
foreach (var c in g.cars) c.computer = false; // humans with no input sit still, so nobody is wrecked before the shot
for (int f = 0; f < 240; f++) g.Advance(dt, none, none);
UnityEngine.Vector2[] spots = { new UnityEngine.Vector2(-2.6f, -2.4f), new UnityEngine.Vector2(-.6f, -.4f), new UnityEngine.Vector2(1.6f, -2.4f), new UnityEngine.Vector2(2.8f, -.2f) };
float[] headings = { 90, 0, 135, 200 };
for (int i = 0; i < 4; i++) { var c = g.cars[i]; c.computer = false; c.position = spots[i]; c.heading = headings[i]; c.speed = 0; }
g.cars[0].item = TNDA.Prototype.ArenaItem.Shield; g.cars[1].item = TNDA.Prototype.ArenaItem.Rocket; g.cars[2].item = TNDA.Prototype.ArenaItem.Cherry;
g.cars[3].GiveCherry(8);
foreach (var p in g.pickups) { p.available = false; p.respawn = 1000; }
g.Advance(dt, none, none);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3items.png");
return "captured " + string.Join(",", System.Linq.Enumerable.Select(g.itemViews, v => v.gameObject.activeSelf + ":" + (v.sprite != null ? v.sprite.name : "-")));
