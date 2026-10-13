// Ticket 10: records the whole cabinet flow frame by frame (attract demo, coin, countdown, match, results 20 s back
// to title, R restart, Esc to title, 2P start) so the trace can be compared before and after a refactor.
// Usage: eval_file tmp/g3flow.cs, with the output path in UnityEngine.PlayerPrefs string "g3flowOut".
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
string outPath = UnityEngine.PlayerPrefs.GetString("g3flowOut", "");
var sb = new System.Text.StringBuilder();
float dt = 1f / 60;
var none = new TNDA.Prototype.ArcadeInput();
var gas = new TNDA.Prototype.ArcadeInput { throttle = 1, steer = .3f };
bool wasEnabled = g.enabled;
int savedBest = UnityEngine.PlayerPrefs.GetInt("WreckBest", 0);
int frame = 0;
System.Action<string> mark = label => sb.Append("## ").Append(label).Append('\n');
System.Action<TNDA.Prototype.ArcadeInput> step = p1 =>
{
    g.Advance(dt, p1, none);
    frame++;
    sb.Append(frame).Append(' ').Append(g.Phase).Append(" t=").Append(g.Elapsed.ToString("R"))
      .Append(" cd=").Append(g.CountdownLeft.ToString("R")).Append(" pc=").Append(g.PlayerCount)
      .Append(" n=[").Append(g.notice.text).Append('|').Append(g.notice.color).Append(']')
      .Append(" c=[").Append(g.countdownLabel == null ? "-" : g.countdownLabel.text + "@" + g.countdownLabel.fontSize).Append(']')
      .Append(" ov=").Append(g.overlay.activeSelf).Append(" at=").Append(g.attractScreen != null && g.attractScreen.activeSelf)
      .Append(" h=[").Append(g.overlayHint.text).Append("] ot=[").Append(g.overlayTitle.text).Append(']')
      .Append(" ob=[").Append(g.overlayBody.text.Replace("\n", "/")).Append(']')
      .Append(" ab=[").Append(g.arenaBoard.text).Append("] hud=[").Append(g.hud.text.Replace("\n", "/")).Append(']')
      .Append(" lb=[").Append(g.leaderboard.text.Replace("\n", "/")).Append(']');
    foreach (var c in g.cars) sb.Append(" | ").Append(c.position.ToString("R")).Append(' ').Append(c.score).Append(c.alive ? "" : " dead").Append(c.eliminated ? " out" : "").Append(c.gameObject.activeSelf ? "" : " hid");
    sb.Append('\n');
};
try
{
    g.enabled = false;
    UnityEngine.PlayerPrefs.SetInt("WreckBest", 0);
    // Untraced prelude: finish a countdown and clear the notice timer, so leftovers from earlier runs cannot leak in.
    UnityEngine.Random.InitState(1234);
    g.StartRun(1);
    for (int f = 0; f < 60 * 8; f++) g.Advance(dt, none, none);
    typeof(TNDA.Prototype.ArcadeGame).GetField("noticeAge", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, 0f);
    g.notice.text = "";
    UnityEngine.Random.InitState(1234);
    mark("preview + attract demo past elimination and loop");
    g.PreparePreview();
    for (int f = 0; f < 60 * 32; f++) step(none);
    mark("coin 1P, countdown, full match to results");
    g.StartRun(1);
    while (g.Phase != TNDA.Prototype.ArcadePhase.Results && frame < 60 * 200) step(gas);
    mark("results back to title after 20 s");
    for (int f = 0; f < 60 * 21; f++) step(none);
    mark("2P start then R restart mid-match");
    g.StartRun(2);
    for (int f = 0; f < 60 * 8; f++) step(gas);
    g.StartRun(g.PlayerCount); // R
    for (int f = 0; f < 60 * 5; f++) step(gas);
    mark("Esc to title mid-match");
    g.ShowAttract(); // Esc
    for (int f = 0; f < 60 * 3; f++) step(none);
    mark("Esc during countdown");
    g.StartRun(1);
    for (int f = 0; f < 60; f++) step(none);
    g.ShowAttract();
    for (int f = 0; f < 60 * 2; f++) step(none);
}
finally
{
    g.PreparePreview();
    g.enabled = wasEnabled;
    UnityEngine.PlayerPrefs.SetInt("WreckBest", savedBest);
}
string trace = sb.ToString();
if (outPath != "") System.IO.File.WriteAllText(outPath, trace);
var sha = System.Security.Cryptography.SHA256.Create();
string hash = System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(trace))).Replace("-", "").Substring(0, 16);
return new { frames = frame, hash, results = trace.Contains(" Results "), outPath };
