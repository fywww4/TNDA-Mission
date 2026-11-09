// Ticket 04 (Play Mode, GAME3_Mode7): debris splash. State in tmp/m7-04-state.txt: p1 (1P rams car 3), p2 (2P, P2 rams
// car 3), slow (1P slow dent: no splash), attract. Prefix "capture " on a second call to step one frame and save
// (the setup call itself saves tmp/m7-04-<state>.png at the end of its frame); prefix "after " only reports, so call it 3+ real
// seconds later (the splash ages in real frames) to see it gone.
var state = System.IO.File.ReadAllText("tmp/m7-04-state.txt").Trim();
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null || !UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Enter Play Mode with GAME3_Mode7 open.");
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
UnityEngine.Application.runInBackground = true;
bool capture = state.StartsWith("capture "), after = state.StartsWith("after ");
if (capture) state = state.Substring(8);
if (after) state = state.Substring(6);
System.Action<int> start = n => { g.StartRun(n); while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none); };
System.Action<int, float> ram = (seat, speed) =>
{
    foreach (var c in g.cars) { c.spawnProtection = 0; c.speed = 0; }
    var r = g.cars[seat]; var v = g.cars[3];
    r.position = new UnityEngine.Vector2(0, -1); r.heading = 0; r.speed = speed;
    v.position = new UnityEngine.Vector2(.55f, -1); v.heading = 90; v.health = 1;
    g.Advance(dt, none, none);
};
if (capture) g.Advance(dt, none, none);
else if (after) { }
else
{
    g.enabled = state == "attract";
    switch (state)
    {
        case "p1": start(1); g.enabled = false; ram(0, 2.8f); break;
        case "p2": start(2); g.enabled = false; ram(1, 2.8f); break;
        case "slow": start(1); g.enabled = false; ram(0, 1.6f); break;
        case "attract": break;
        // A splash, then a new match whose countdown runs in real frames (call "after restart" a moment later).
        case "restart": start(1); g.enabled = false; ram(0, 2.8f); g.StartRun(1); g.enabled = true; break;
    }
}
if (!after && state != "restart") UnityEngine.ScreenCapture.CaptureScreenshot("tmp/m7-04-" + state + ".png");
var sb = new System.Text.StringBuilder(state + (after ? " after" : "") + " phase=" + g.Phase + " players=" + g.PlayerCount);
foreach (var layer in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.UI.RectMask2D>())
{
    if (!layer.name.StartsWith("Mode7 debris")) continue;
    var rt = (UnityEngine.RectTransform)layer.transform;
    sb.Append(" | " + layer.name + " active=" + layer.gameObject.activeInHierarchy + " sibling=" + rt.GetSiblingIndex()
        + " rect=" + rt.anchoredPosition + "+" + rt.sizeDelta);
    if (!layer.gameObject.activeInHierarchy) continue;
    var sheet = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Debris>().sheet;
    var src = sheet.GetPixels32();
    int key = System.Array.FindIndex(src, p => p.a == 255 && p.r == 0xE0 && p.g == 0x40 && p.b == 0x20);
    foreach (var img in layer.GetComponentsInChildren<UnityEngine.UI.RawImage>())
        sb.Append(" [cell" + (int)(img.uvRect.x * 4) + (img.texture != sheet && img.texture is UnityEngine.Texture2D t
            ? " body=" + t.GetPixels32()[key] : "") + " @" + img.rectTransform.anchoredPosition + " a=" + img.color.a + "]");
}
sb.Append(" | car3 colour=" + (UnityEngine.Color32)g.cars[3].body.color);
return sb.ToString();
