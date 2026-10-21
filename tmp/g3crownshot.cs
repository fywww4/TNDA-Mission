// Captures 1P gameplay 15 s in: the leader's crown, P1-P4 names, P2 yellow, P3 cyan/white and P4 black/white stripes.
// Reports who wears the crown and whether that is the top score.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
g.enabled = false;
g.StartRun(1); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
bool zeroHidden = !g.crownView.gameObject.activeSelf;
for (int f = 0; f < 60 * 15; f++) g.Advance(dt, none, none);
var leader = System.Linq.Enumerable.First(System.Linq.Enumerable.OrderByDescending(System.Linq.Enumerable.Where(g.cars, c => !c.eliminated), c => c.score));
float off = UnityEngine.Vector2.Distance((UnityEngine.Vector2)g.crownView.transform.position, leader.position + UnityEngine.Vector2.up * g.crownHeight);
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/g3crown.png");
return "crown hidden at 0 points=" + zeroHidden + " | crown on " + leader.DriverName + " (score " + leader.score + ") shown=" + g.crownView.gameObject.activeSelf + " offset=" + off.ToString("0.00") + " | names " + string.Join(",", System.Linq.Enumerable.Select(g.cars, c => c.DriverName));
