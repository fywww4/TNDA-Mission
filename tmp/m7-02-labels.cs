// Ticket 02 (Play Mode, GAME3_Mode7): name tags, score pop-ups and crown in every chase view. eval_file body.
// tmp/m7-02-state.txt holds the step: "setup 1|2|attract" puts the game in that state (call, then wait a frame so the
// chase cameras catch up); "check" verifies every car's tag and pop-up against each chase camera and captures
// tmp/m7-02-<players>.png. Prints PASS/FAIL lines.
var step = System.IO.File.ReadAllText("tmp/m7-02-state.txt").Trim().Split(' ');
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var stage = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Stage>();
if (g == null || stage == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3_Mode7 open.");
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
var sb = new System.Text.StringBuilder();
if (step[0] == "setup")
{
    g.enabled = false;
    if (step[1] == "attract") { g.PreparePreview(); for (int f = 0; f < 60 * 5; f++) g.Advance(dt, none, none); return "attract phase=" + g.Phase; }
    int players = int.Parse(step[1]);
    g.StartRun(players);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    for (int f = 0; f < 60 * 10; f++) g.Advance(dt, none, none);
    // The second car leads, so the crown rides on P2 (2P) / the first AI (1P).
    foreach (var c in g.cars) { c.health = 1; c.score = c.index == 1 ? 3000 : 100 * (c.index + 1); }
    return "setup " + players + "P phase=" + g.Phase;
}
int fails = 0;
System.Action<bool, string> check = (ok, what) => { if (!ok) fails++; sb.AppendLine((ok ? "PASS " : "FAIL ") + what); };
bool attract = g.Phase == TNDA.Prototype.ArcadePhase.Attract;
if (!attract)
{
    g.Advance(dt, none, none);
    // A pop-up on every car at once.
    var pop = typeof(TNDA.Prototype.DemolitionGame).GetMethod("PopScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    foreach (var c in g.cars) pop.Invoke(g, new object[] { new TNDA.Prototype.ScoreChanged(c, 100, c.position) });
    g.Advance(dt, none, none);
}
bool two = g.PlayerCount == 2 && !attract;
var cams = (UnityEngine.Camera[])typeof(TNDA.Prototype.Mode7Stage).GetField("chase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(stage);
System.Func<TMPro.TMP_Text, int, TMPro.TMP_Text> inView = (own, v) =>
{
    if (v == 0) return own;
    var t = own.transform.parent.Find(own.name + " (view " + (v + 1) + ")");
    return t == null ? null : t.GetComponent<TMPro.TMP_Text>();
};
int[] shown = new int[2], hidden = new int[2];
for (int v = 0; v < 2; v++)
{
    var cam = cams[v];
    bool viewInUse = v == 0 || two;
    for (int i = 0; i < g.cars.Length; i++)
    {
        var car = g.cars[i];
        var p = cam.WorldToScreenPoint(new UnityEngine.Vector3(car.position.x, car.position.y, -(stage.carLift * 2 + .15f)));
        bool onView = viewInUse && !attract && car.alive && p.z > 0 && cam.pixelRect.Contains(new UnityEngine.Vector2(p.x, p.y));
        if (onView) shown[v]++; else if (viewInUse && !attract) hidden[v]++;
        var expect = TNDA.Prototype.HybridResolution.ArtToCabinet(new UnityEngine.Vector2(p.x, p.y));
        foreach (var (kind, own, lift) in new[] { ("tag", g.carLabels[i], 0f), ("pop-up", g.scorePopups[i], 6f) })
        {
            var text = inView(own, v);
            string what = "view " + (v + 1) + " " + car.DriverName + " " + kind;
            if (text == null) { check(!viewInUse || attract, what + " has no copy" + (viewInUse ? "" : " (view not in use)")); continue; }
            bool active = text.gameObject.activeInHierarchy;
            check(active == onView, what + (onView ? " shown" : " hidden") + " (active=" + active + ", z=" + p.z.ToString("0.0") + ")");
            if (!active || !onView) continue;
            var at = text.rectTransform.anchoredPosition;
            // Tag sits on the projected point; a pop-up starts 6px above it and floats up to 16px (or stacks a line up).
            bool near = UnityEngine.Mathf.Abs(at.x - expect.x) <= (lift > 0 ? 4 : 1) && at.y - expect.y >= lift - 1 && at.y - expect.y <= lift + 16 + 13 * 3 + 1;
            check(near, what + " follows the car: at " + at + " expect " + expect);
            bool rightHalf = !two || (v == 0 ? expect.y >= 0 : expect.y < 0);
            check(rightHalf, what + " stays in its own half (y=" + expect.y + ")");
            check(text.text == own.text && text.color == own.color, what + " text/colour match (" + text.text + ")");
        }
    }
}
// The crown is a world sprite every chase camera draws: on the leader, on a layer both views render.
var crown = g.crownView;
if (!attract)
{
    var leader = g.cars[1];
    check(crown.gameObject.activeInHierarchy, "crown shown");
    var cp = (UnityEngine.Vector2)crown.transform.position;
    check(UnityEngine.Mathf.Min((cp - leader.position).magnitude, (cp - leader.position - UnityEngine.Vector2.up * g.crownHeight).magnitude) < .05f, "crown on the leader " + leader.DriverName);
    for (int v = 0; v < (two ? 2 : 1); v++)
        check((cams[v].cullingMask & (1 << crown.gameObject.layer)) != 0, "view " + (v + 1) + " draws the crown layer");
}
sb.Insert(0, (fails == 0 ? "ALL PASS" : fails + " FAIL") + "  players=" + g.PlayerCount + " phase=" + g.Phase
    + " shown=" + shown[0] + "/" + shown[1] + " hidden=" + hidden[0] + "/" + hidden[1] + "\n");
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/m7-02-" + (attract ? "attract" : g.PlayerCount + "p") + ".png");
return sb.ToString();
