// Play Mode, GAME3_Mode7, after `tmp/m7-02-labels.cs` "setup 2" or "setup 1" (the second car leads, so it wears
// the crown). For each chase view in use, checks the crown's bottom edge clears the top of the leader's name tag
// (art pixels), then captures tmp/m7-crown-tag.png. eval_file body.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var stage = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Stage>();
if (g == null || stage == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3_Mode7 open.");
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var cams = (UnityEngine.Camera[])typeof(TNDA.Prototype.Mode7Stage).GetField("chase", flags).GetValue(stage);
var crownLift = typeof(TNDA.Prototype.Mode7Stage).GetMethod("CrownLift", flags);
g.Advance(1f / 60, new TNDA.Prototype.ArcadeInput(), new TNDA.Prototype.ArcadeInput());
var leader = g.cars[1];
var crown = g.crownView;
float half = crown.sprite.bounds.extents.y * crown.transform.lossyScale.y;
float tagLift = stage.carLift * 2 + .15f;
var sb = new System.Text.StringBuilder("players=" + g.PlayerCount + " crown shown=" + crown.gameObject.activeInHierarchy + "\n");
int fails = 0;
for (int v = 0; v < (g.PlayerCount == 2 ? 2 : 1); v++)
{
    var cam = cams[v];
    float lift = (float)crownLift.Invoke(stage, new object[] { cam, leader.position });
    var tag = cam.WorldToScreenPoint(new UnityEngine.Vector3(leader.position.x, leader.position.y, -tagLift));
    var bottom = cam.WorldToScreenPoint(new UnityEngine.Vector3(leader.position.x, leader.position.y, -(lift - half)));
    float gap = bottom.y - (tag.y + 3.25f);
    bool ok = tag.z <= 0 || gap >= stage.crownGap - .3f;
    if (!ok) fails++;
    sb.AppendLine((ok ? "PASS" : "FAIL") + " view " + (v + 1) + " depth=" + tag.z.ToString("0.0") + " lift=" + lift.ToString("0.00")
        + " tag top=" + (tag.y + 3.25f).ToString("0.0") + " crown bottom=" + bottom.y.ToString("0.0") + " gap=" + gap.ToString("0.0") + " art px");
}
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/m7-crown-tag-" + g.PlayerCount + "p.png");
return (fails == 0 ? "ALL PASS\n" : fails + " FAIL\n") + sb;
