// CLI regression (Play Mode): eval_file tmp/g3cues.cs. Ticket 07: every notice and sound now comes from a match event
// through the scene adapter. Each path is triggered in the real scene and its notice text, colour, time on screen and
// sound are compared with what the rules showed directly before the change. The attract demo must stay silent.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, bool>();
var seen = new System.Collections.Generic.List<string>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
var use = new TNDA.Prototype.ArcadeInput { action = true, actionPressed = true };
UnityEngine.Color cyan = TNDA.Prototype.ArcadeGame.Cyan, yellow = TNDA.Prototype.ArcadeGame.Yellow;
var originalSpeaker = g.speaker;
var probe = new UnityEngine.GameObject("Cue probe").AddComponent<UnityEngine.AudioSource>();
probe.volume = 0;
var pickupItems = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(g.pickups, p => p.item));
bool wasEnabled = g.enabled;
var p1 = g.cars[0];

System.Action setup = () =>
{
    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    UnityEngine.Vector2[] spots = { new UnityEngine.Vector2(-3, 1), new UnityEngine.Vector2(3, 1), new UnityEngine.Vector2(3, -3.2f), new UnityEngine.Vector2(-3, -3.2f) };
    for (int i = 0; i < g.cars.Length; i++)
    {
        var c = g.cars[i];
        c.computer = false; c.spawnProtection = c.shieldSeconds = 0; c.speed = 0; c.heading = 90; c.position = spots[i];
    }
    foreach (var prop in g.props) { prop.broken = true; prop.respawn = 1000; }
    foreach (var pickup in g.pickups) { pickup.available = false; pickup.respawn = 1000; }
    g.Advance(dt, none, none);
    g.notice.text = "";
    probe.Stop();
};
// Steps until the notice clears and reports what was shown: text, colour and roughly how long.
System.Func<string, string, UnityEngine.Color, float, bool, bool> expect = (label, text, colour, seconds, sound) =>
{
    string shown = g.notice.text; var shownColour = g.notice.color; bool played = probe.isPlaying;
    int frames = 0;
    while (g.notice.text != "" && frames < 600) { g.Advance(dt, none, none); frames++; }
    float lasted = frames * dt;
    bool ok = shown == text && (text == "" || shownColour == colour) && UnityEngine.Mathf.Abs(lasted - seconds) <= 2 * dt && played == sound;
    seen.Add(label + ": \"" + shown + "\" #" + UnityEngine.ColorUtility.ToHtmlStringRGB(shownColour) + " " + lasted.ToString("0.00") + "s sound=" + played);
    checks[label] = ok;
    return ok;
};

try
{
    g.enabled = false;
    g.speaker = probe;

    setup(); // side ram, victim survives
    p1.position = new UnityEngine.Vector2(0, -1); p1.heading = 0; p1.speed = 2.8f;
    g.cars[3].position = new UnityEngine.Vector2(.55f, -1); g.cars[3].heading = 90;
    g.Advance(dt, none, none);
    expect("ram", "P1 撞擊 x2.0", cyan, 1, true);

    setup(); // ram smashes a prop
    var prop0 = g.props[0]; prop0.broken = false;
    p1.position = prop0.point + new UnityEngine.Vector2(-.6f, 0); p1.heading = 0; p1.speed = 2.8f;
    g.Advance(dt, none, none);
    expect("prop smashed", "P1 撞碎 +" + prop0.points, cyan, 1.2f, true);

    setup(); // rocket smashes a prop: sound only
    prop0.broken = false;
    g.rockets[0].Spawn(prop0.point + new UnityEngine.Vector2(-1.2f, 0), new UnityEngine.Vector2(6, 0), 0);
    for (int f = 0; f < 20 && g.rockets[0].active; f++) g.Advance(dt, none, none);
    expect("rocket smashes prop", "", cyan, 0, true);

    setup(); // crate pickup by P1, then by a computer car (sound only)
    var crate = g.pickups[0]; crate.item = TNDA.Prototype.ArenaItem.Shield; crate.available = true;
    p1.position = crate.point;
    g.Advance(dt, none, none);
    expect("crate pickup", "P1 拿到防護罩，按道具鍵使用", cyan, 2, true);
    setup();
    crate.available = true; g.cars[2].computer = true; g.cars[2].position = crate.point;
    g.Advance(dt, none, none);
    g.cars[2].computer = false;
    checks["computer pickup has no notice"] = g.notice.text == "" && probe.isPlaying;

    setup();
    p1.item = TNDA.Prototype.ArenaItem.Shield; g.Advance(dt, use, none);
    expect("shield used", "P1 防護罩 6 秒", cyan, 2, true);
    setup();
    p1.item = TNDA.Prototype.ArenaItem.Rocket; g.Advance(dt, use, none);
    expect("rocket used", "P1 發射火箭！", yellow, 1, true);
    setup();
    g.rockets[0].Spawn(new UnityEngine.Vector2(-1, 0), new UnityEngine.Vector2(0, -6), 1);
    g.rockets[1].Spawn(new UnityEngine.Vector2(1, 0), new UnityEngine.Vector2(0, -6), 1);
    p1.item = TNDA.Prototype.ArenaItem.Rocket; g.Advance(dt, use, none);
    expect("rockets busy", "火箭還在飛，道具先保留", yellow, 1, false);
    setup();
    p1.item = TNDA.Prototype.ArenaItem.Cherry; g.Advance(dt, use, none);
    expect("cherry eaten", "P1 吃下櫻桃！8 秒內碰到就撞毀", cyan, 2.5f, true);

    setup(); // cherry P1 touches computer 3
    p1.GiveCherry(8); p1.position = new UnityEngine.Vector2(0, -1);
    g.cars[3].position = new UnityEngine.Vector2(.55f, -1);
    g.Advance(dt, none, none);
    expect("cherry strike", "P1 一碰撞毀 電腦3！", cyan, 2, true);

    setup(); // fast into the right wall: sound only
    p1.position = new UnityEngine.Vector2(g.arenaMax.x - .01f, -1); p1.heading = 0; p1.speed = 2.8f;
    g.Advance(dt, none, none);
    expect("wall hit", "", cyan, 0, true);

    setup(); // rocket hit: explosion and sound, no notice for a non-lethal hit
    g.cars[2].position = new UnityEngine.Vector2(1.5f, -1.2f);
    g.rockets[0].Spawn(new UnityEngine.Vector2(.6f, -1.2f), new UnityEngine.Vector2(6, 0), 0);
    for (int f = 0; f < 20 && g.rockets[0].active; f++) g.Advance(dt, none, none);
    checks["rocket hit explosion"] = g.explosions[0].gameObject.activeSelf;
    expect("rocket hit", "", cyan, 0, true);

    setup(); // wreck without an attacker: only the wreck notice
    g.DamageVehicle(g.cars[2], 1);
    expect("wreck", "電腦2 被撞毀 -0，3 秒後重生", yellow, 2.5f, false);

    // Attract demo: 21 seconds including the demo elimination, no notice and no sound at any frame.
    g.PreparePreview();
    probe.Stop(); g.notice.text = "";
    bool silent = true, quiet = true;
    for (int f = 0; f < 60 * 21; f++) { g.Advance(dt, none, none); silent &= !probe.isPlaying; quiet &= g.notice.text == ""; }
    checks["attract demo eliminated a car"] = System.Linq.Enumerable.Count(g.cars, c => c.eliminated) == 1;
    checks["attract demo silent"] = silent;
    checks["attract demo shows no notice"] = quiet;
}
finally
{
    for (int i = 0; i < g.pickups.Length; i++) g.pickups[i].item = pickupItems[i];
    g.speaker = originalSpeaker;
    UnityEngine.Object.Destroy(probe.gameObject);
    g.PreparePreview();
    g.enabled = wasEnabled;
}
var result = new System.Collections.Generic.Dictionary<string, object>();
foreach (var kv in checks) result[kv.Key] = kv.Value;
result["~seen"] = string.Join("\n", seen);
return result;
