// CLI regression (Play Mode): eval_file tmp/g3oil.cs. Oil (2026-10-16; lava removed 2026-10-22): the held oil shows as a
// trailing icon, using it drops a slick view behind the car, and the next car spins without damage while the dropper
// scores 50. Also checks no lava object is left in the scene.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var checks = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
var use = new TNDA.Prototype.ArcadeInput { action = true, actionPressed = true };
bool wasEnabled = g.enabled; g.enabled = false;
try
{
    checks["no lava object in the scene"] = g.transform.Find("Effects/Lava pool") == null;
    g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    foreach (var c in g.cars) c.computer = false;
    var p1 = g.cars[0];
    p1.position = new UnityEngine.Vector2(-1, -1.5f); p1.heading = 0; p1.item = TNDA.Prototype.ArenaItem.Oil;
    g.Advance(dt, none, none);
    checks["oil icon trails P1"] = g.itemViews[0].gameObject.activeSelf && g.itemViews[0].sprite == g.itemSprites[3];
    g.Advance(dt, use, none);
    var slot = System.Array.Find(g.rockets, r => r.active && r.slick);
    checks["slick view on the floor"] = slot != null && slot.slickView.gameObject.activeSelf && !slot.view.gameObject.activeSelf;
    var victim = g.cars[3];
    victim.position = slot.position; victim.speed = 2;
    g.Advance(dt, none, none);
    checks["next car spins, no damage, dropper +50"] = victim.IsSlipping + " hp=" + victim.health + " P1=" + p1.score + " slick=" + slot.slickView.gameObject.activeSelf;
}
catch (System.Exception e) { checks["EXCEPTION"] = e.ToString(); }
g.enabled = wasEnabled; g.PreparePreview();
return checks;
