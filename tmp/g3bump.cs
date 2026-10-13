var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
try
{
    g.enabled = false;
    g.StartRun(2);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) { car.computer = false; car.spawnProtection = car.shieldSeconds = 0; }
    foreach (var prop in g.props) { prop.broken = true; prop.respawn = 100; prop.view.gameObject.SetActive(false); }
    var p1 = g.cars[0]; var rammer = g.cars[1];
    p1.position = new UnityEngine.Vector2(0, -1.5f); p1.heading = 90; p1.speed = 0;
    rammer.position = new UnityEngine.Vector2(-.6f, -1.5f); rammer.heading = 0; rammer.speed = 2.4f;
    var start = p1.position;
    g.Advance(dt, none, new TNDA.Prototype.ArcadeInput { throttle = 1 });
    checks["side ram starts slide"] = p1.SlideVelocity.x > 5 && p1.stun == .12f && p1.health < 1;
    checks["rammer rebounds"] = rammer.SlideVelocity.x < 0;
    checks["nose knocked aside"] = UnityEngine.Mathf.Abs(p1.heading - 90) >= 15 && UnityEngine.Mathf.Abs(p1.heading - 90) <= 30;
    for (int f = 0; f < 59; f++) g.Advance(dt, none, none);
    checks["cars separate within one second"] = (p1.position - start).magnitude > 1
        && UnityEngine.Vector2.Distance(p1.position, rammer.position) > 1;
    checks["slide decays"] = p1.SlideVelocity.magnitude < .1f;
}
finally { g.PreparePreview(); g.enabled = wasEnabled; }
return checks;
