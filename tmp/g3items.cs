// Ticket 03: the item module through its public seams (crate roll, crate → use in a match, HUD text).
var checks = new System.Collections.Generic.Dictionary<string, bool>();
var Shield = TNDA.Prototype.ArenaItem.Shield; var Rocket = TNDA.Prototype.ArenaItem.Rocket; var Cherry = TNDA.Prototype.ArenaItem.Cherry;

// Crate roll: a pure draw from two random numbers in [0, 1).
System.Func<float[], System.Func<float>> fixedRolls = values => { int n = 0; return () => values[n++]; };
checks["roll: trailing low roll is cherry"] = TNDA.Prototype.ArenaItems.Roll(true, .5f, fixedRolls(new[] { .49f, .9f })) == Cherry;
checks["roll: trailing miss splits shield"] = TNDA.Prototype.ArenaItems.Roll(true, .5f, fixedRolls(new[] { .5f, .2f })) == Shield;
checks["roll: trailing miss splits rocket"] = TNDA.Prototype.ArenaItems.Roll(true, .5f, fixedRolls(new[] { .5f, .7f })) == Rocket;
checks["roll: leader never cherry"] = TNDA.Prototype.ArenaItems.Roll(false, .5f, fixedRolls(new[] { 0f, .2f })) == Shield;
var random = new System.Random(20261013);
System.Func<float> sample = () => (float)random.NextDouble();
int[] last = new int[4], leader = new int[4];
for (int i = 0; i < 4000; i++) { last[(int)TNDA.Prototype.ArenaItems.Roll(true, .5f, sample)]++; leader[(int)TNDA.Prototype.ArenaItems.Roll(false, .5f, sample)]++; }
checks["roll: last place about 50% cherry"] = last[3] > 1800 && last[3] < 2200 && last[1] > 800 && last[2] > 800 && last[0] == 0;
checks["roll: leader 0% cherry, shield/rocket halves"] = leader[3] == 0 && leader[1] > 1800 && leader[2] > 1800 && leader[0] == 0;

// Match seam (Play Mode): crate pickup and using each item, with the same on-screen messages as before ticket 03.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
var use = new TNDA.Prototype.ArcadeInput { action = true, actionPressed = true };
bool wasEnabled = g.enabled;
var randomState = UnityEngine.Random.state;
System.Func<TNDA.Prototype.ArcadeVehicle> start = () => {
    g.StartRun(1);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var car in g.cars) { car.computer = false; car.spawnProtection = car.shieldSeconds = 0; }
    foreach (var prop in g.props) { prop.broken = true; prop.respawn = 1000; prop.view.gameObject.SetActive(false); }
    foreach (var pickup in g.pickups) { pickup.available = false; pickup.respawn = 1000; pickup.view.gameObject.SetActive(false); }
    return g.cars[0];
};
try
{
    g.enabled = false;
    UnityEngine.Random.InitState(20261013);

    var p1 = start();
    var crate = g.pickups[0];
    p1.position = crate.point; crate.available = true; crate.view.gameObject.SetActive(true);
    g.Advance(dt, none, none);
    string rolled = p1.item == Shield ? "防護罩" : p1.item == Rocket ? "火箭" : p1.item == Cherry ? "櫻桃" : "?";
    checks["crate gives an item and hides"] = p1.item != TNDA.Prototype.ArenaItem.None && !crate.available && !crate.view.gameObject.activeSelf;
    checks["crate message unchanged"] = g.notice.text == "P1 拿到" + rolled + "，按道具鍵使用";

    p1 = start(); p1.item = Shield;
    g.Advance(dt, use, none);
    checks["shield: consumed, 6s shield"] = p1.item == TNDA.Prototype.ArenaItem.None && p1.shieldSeconds > 5.9f && p1.shieldSeconds <= 6;
    checks["shield: message unchanged"] = g.notice.text == "P1 防護罩 6 秒";

    p1 = start(); p1.item = Rocket;
    g.Advance(dt, use, none);
    checks["rocket: consumed, one rocket flying"] = p1.item == TNDA.Prototype.ArenaItem.None
        && System.Linq.Enumerable.Count(g.rockets, r => r.active && r.owner == 0) == 1;
    checks["rocket: message unchanged"] = g.notice.text == "P1 發射火箭！";

    p1 = start(); p1.item = Rocket;
    foreach (var r in g.rockets) r.Spawn(new UnityEngine.Vector2(0, -1), new UnityEngine.Vector2(.01f, 0), 3);
    g.Advance(dt, use, none);
    checks["rocket: full pool keeps item"] = p1.item == Rocket && System.Linq.Enumerable.All(g.rockets, r => r.owner == 3);
    checks["rocket: busy message unchanged"] = g.notice.text == "火箭還在飛，道具先保留";

    p1 = start(); p1.item = Cherry;
    g.Advance(dt, use, none);
    checks["cherry: consumed, 8s power"] = p1.item == TNDA.Prototype.ArenaItem.None && p1.HasCherry && p1.CherrySeconds > 7.9f;
    checks["cherry: message unchanged"] = g.notice.text == "P1 吃下櫻桃！8 秒內碰到就撞毀";
}
finally { UnityEngine.Random.state = randomState; g.PreparePreview(); g.enabled = wasEnabled; }

return checks;
