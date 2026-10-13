var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
var use = new TNDA.Prototype.ArcadeInput { action = true, actionPressed = true };
var gas = new TNDA.Prototype.ArcadeInput { throttle = 1 };
bool wasEnabled = g.enabled;
var randomState = UnityEngine.Random.state;
System.Action<int> start = players => {
    g.StartRun(players);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) { car.computer = false; car.spawnProtection = car.shieldSeconds = 0; }
    foreach (var prop in g.props) { prop.broken = true; prop.respawn = 1000; prop.view.gameObject.SetActive(false); }
    foreach (var pickup in g.pickups) { pickup.available = false; pickup.respawn = 1000; pickup.view.gameObject.SetActive(false); }
};
try
{
    g.enabled = false;
    UnityEngine.Random.InitState(20261013);
    // Roll through actual crate collection, with no reflection into the item-selection implementation.
    start(1);
    for (int i = 0; i < g.cars.Length; i++) g.cars[i].score = i * 100;
    var crate = g.pickups[0];
    int lastCherry = 0, leaderCherry = 0;
    foreach (int index in new[] { 0, 3 })
    {
        var collector = g.cars[index];
        var parked = collector.position;
        for (int trial = 0; trial < 2000; trial++)
        {
            collector.item = TNDA.Prototype.ArenaItem.None;
            collector.position = crate.point;
            crate.available = true; crate.view.gameObject.SetActive(true);
            g.Advance(.001f, none, none);
            if (collector.item == TNDA.Prototype.ArenaItem.Cherry) { if (index == 0) lastCherry++; else leaderCherry++; }
        }
        collector.position = parked;
    }
    checks["last-place cherry odds"] = lastCherry > 850 && lastCherry < 1150;
    checks["leader cannot roll cherry"] = leaderCherry == 0;

    start(1);
    var p1 = g.cars[0];
    p1.item = TNDA.Prototype.ArenaItem.Cherry;
    g.Advance(dt, use, none);
    var block = new UnityEngine.MaterialPropertyBlock(); p1.body.GetPropertyBlock(block);
    checks["use cherry consumes item and shows yellow car"] = p1.HasCherry && p1.item == TNDA.Prototype.ArenaItem.None
        && g.cherryViews[0].gameObject.activeSelf && block.GetFloat("_Solid") == 1;
    p1.position = new UnityEngine.Vector2(-4.1f, -.5f); p1.heading = 0;
    for (int f = 0; f < 180; f++) g.Advance(dt, gas, none);
    checks["cherry increases top speed"] = UnityEngine.Mathf.Abs(p1.speed - 3.78f) < .01f;

    start(1);
    p1 = g.cars[0]; var ai = g.cars[2];
    p1.GiveCherry(8);
    p1.position = new UnityEngine.Vector2(0, -1); p1.heading = 0; p1.speed = .5f;
    ai.position = new UnityEngine.Vector2(.55f, -1); ai.heading = 90; ai.speed = 0;
    g.Advance(dt, none, none);
    checks["cherry touch wrecks opponent"] = !ai.alive && p1.alive && p1.score == 300 && ai.IsSpinningOut;

    start(2);
    p1 = g.cars[0]; var p2 = g.cars[1];
    p1.GiveCherry(8);
    p1.position = new UnityEngine.Vector2(0, -1); p1.heading = 90;
    p2.position = new UnityEngine.Vector2(-.6f, -1); p2.heading = 0; p2.speed = 2.6f;
    g.Advance(dt, none, gas);
    checks["ramming cherry car wrecks it"] = !p1.alive && p2.alive && !p1.HasCherry;

    start(1);
    p1 = g.cars[0]; ai = g.cars[2]; ai.computer = true;
    ai.GiveCherry(8);
    ai.position = new UnityEngine.Vector2(0, -1); ai.heading = 0; ai.speed = 3;
    p1.position = new UnityEngine.Vector2(.55f, -1);
    g.Advance(dt, none, none);
    checks["AI cherry respects opening grace"] = p1.alive && p1.health == 1;

    start(1);
    p1 = g.cars[0]; p1.GiveCherry(8);
    for (int f = 0; f < 360; f++) g.Advance(dt, none, none);
    bool shown = g.cherryViews[0].gameObject.activeSelf;
    g.Advance(.13f, none, none);
    checks["cherry warning blinks"] = p1.HasCherry && shown != g.cherryViews[0].gameObject.activeSelf;
    for (int f = 0; f < 120; f++) g.Advance(dt, none, none);
    p1.body.GetPropertyBlock(block);
    checks["cherry expiry restores car"] = !p1.HasCherry && p1.CherrySeconds == 0
        && !g.cherryViews[0].gameObject.activeSelf && block.GetFloat("_Solid") == 0;

    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    for (int f = 0; f < 1800; f++) g.Advance(dt, none, none);
    checks["thirty-second AI soak"] = g.Phase == TNDA.Prototype.ArcadePhase.Playing && g.Contenders == 4;
}
finally { UnityEngine.Random.state = randomState; g.PreparePreview(); g.enabled = wasEnabled; }
return checks;
