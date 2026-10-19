// CLI regression (Play Mode): eval_file tmp/g3popups.cs. Score pop-ups replace the score notices: every score change
// pops up over the car in its colour (double size, floats up, blinks out), a ram that wrecks adds up in one pop-up,
// the victim shows its loss, the feedback line stays empty for scoring, and the silent attract demo shows pop-ups.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var sb = new System.Text.StringBuilder();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
bool wasEnabled = g.enabled;
g.enabled = false;
System.Func<int, string> show = i => g.scorePopups[i].gameObject.activeSelf
    ? "\"" + g.scorePopups[i].text + "\" size " + g.scorePopups[i].fontSize + " y " + g.scorePopups[i].rectTransform.anchoredPosition.y : "hidden";
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
    g.Advance(dt * 200, none, none); // let the start notice expire
};
try
{
    // 1. Smash a prop.
    setup();
    var p1 = g.cars[0];
    var prop0 = g.props[0];
    prop0.broken = false; prop0.respawn = 0; prop0.point = new UnityEngine.Vector2(0, -1);
    p1.position = new UnityEngine.Vector2(-.5f, -1); p1.heading = 0; p1.speed = 2.8f;
    g.Advance(dt, none, none);
    sb.Append("Smash: popup0 " + show(0) + " colour cyan=" + (g.scorePopups[0].color == TNDA.Prototype.ArcadeGame.Cyan)
        + " notice=\"" + g.notice.text + "\"\n");
    float y0 = g.scorePopups[0].rectTransform.anchoredPosition.y;
    for (int f = 0; f < 18; f++) g.Advance(dt, none, none);
    sb.Append("  after 0.3s: " + show(0) + " rose=" + (g.scorePopups[0].rectTransform.anchoredPosition.y > y0) + "\n");
    for (int f = 0; f < 45; f++) g.Advance(dt, none, none);
    sb.Append("  after 1.05s: " + show(0) + "\n");

    // 2. Ram a nearly wrecked car that has 400 points.
    setup();
    p1 = g.cars[0]; var victim = g.cars[3];
    p1.position = new UnityEngine.Vector2(0, -1); p1.heading = 0; p1.speed = 2.8f;
    victim.position = new UnityEngine.Vector2(.55f, -1); victim.heading = 90; victim.health = .01f; victim.score = 400;
    float before = p1.score;
    g.Advance(dt, none, none);
    sb.Append("Ram wreck: P1 +" + (p1.score - before) + " popup0 " + show(0) + " | victim popup3 " + show(3)
        + " colour white=" + (g.scorePopups[3].color == TNDA.Prototype.ArcadeGame.White) + " notice=\"" + g.notice.text + "\"\n");

    // 3. Attract demo: silent, no notices, but pop-ups appear.
    g.ShowAttract();
    g.PreparePreview();
    int frames = 0, popped = 0;
    for (int f = 0; f < 60 * 20; f++)
    {
        g.Advance(dt, none, none);
        frames++;
        foreach (var p in g.scorePopups) if (p.gameObject.activeSelf) { popped++; break; }
    }
    sb.Append("Attract 20s: frames with a pop-up " + popped + "/" + frames + " notice=\"" + g.notice.text + "\" phase=" + g.Phase + "\n");
}
catch (System.Exception e) { sb.Append("EXCEPTION " + e); }
g.enabled = wasEnabled;
g.PreparePreview();
return sb.ToString();
