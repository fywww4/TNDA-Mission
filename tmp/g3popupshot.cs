// Freezes a ram that wrecks a 400-point car (+ pop-up over P1, -250 over the victim) and captures the Game view.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
g.enabled = false;
g.StartRun(1);
while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
foreach (var c in g.cars) { c.computer = false; c.spawnProtection = 0; c.speed = 0; }
foreach (var pickup in g.pickups) { pickup.available = false; pickup.respawn = 1000; }
for (int f = 0; f < 300; f++) g.Advance(dt, none, none);
var p1 = g.cars[0]; var victim = g.cars[3];
p1.position = new UnityEngine.Vector2(-.6f, -1.2f); p1.heading = 0; p1.speed = 2.8f;
victim.position = new UnityEngine.Vector2(-.05f, -1.2f); victim.heading = 90; victim.health = .01f; victim.score = 400;
g.Advance(dt, none, none);
for (int f = 0; f < 12; f++) g.Advance(dt, none, none);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3popup.png");
return "captured " + g.scorePopups[0].text + " / " + g.scorePopups[3].text;
