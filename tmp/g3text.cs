// CLI regression (Play Mode): eval_file tmp/g3text.cs. Ticket 09: one text source for the cabinet.
// Checks that the in-match footer shows the same controls text as the title screen, that the pixel font
// has a glyph for every character the text table and scene can print, and that sizes stay on the 11px grid.
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
if (g == null) throw new System.InvalidOperationException("Enter Play Mode with GAME3 open.");
var report = new System.Collections.Generic.Dictionary<string, object>();
float dt = 1f / 60; var none = new TNDA.Prototype.ArcadeInput();
var attract = g.attractScreen.transform;
string p1Title = attract.Find("P1 controls").GetComponent<TMPro.TMP_Text>().text;
string p2Title = attract.Find("P2 controls").GetComponent<TMPro.TMP_Text>().text;
g.enabled = false;
try
{
    g.StartRun(2);
    while (g.Phase == TNDA.Prototype.ArcadePhase.Countdown) g.Advance(dt, none, none);
    g.Advance(dt, none, none);
    string footer = g.footer.text;
    // The 2P footer alternates every 3 real seconds; whichever player is showing must match the title screen.
    bool showsP1 = footer.StartsWith("P1"), matches = showsP1 ? footer.StartsWith(p1Title) : footer == p2Title;
    report["footerMatchesTitle"] = matches;
    report["footer"] = footer;
    report["titleControls"] = new[] { p1Title, p2Title };

    var font = g.footer.font;
    var text = new System.Text.StringBuilder(string.Concat(TNDA.Prototype.CabinetText.All));
    foreach (var label in g.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.Append(label.text);
    foreach (var s in g.startMessages) text.Append(s);
    // Rich-text tags and format placeholders are ASCII; every printable character must have a glyph.
    var missing = text.ToString().Where(c => c >= 32 && !font.HasCharacter(c)).Distinct().ToArray();
    report["missingGlyphs"] = new string(missing);

    var offGrid = g.GetComponentsInChildren<TMPro.TMP_Text>(true)
        .Where(t => t.fontSize % TNDA.Prototype.Editor.ArcadeFontSetup.PixelSize != 0 || t.font != font)
        .Select(t => t.name + " " + t.fontSize).ToArray();
    report["offGridText"] = offGrid;
    report["pass"] = matches && missing.Length == 0 && offGrid.Length == 0;
}
finally
{
    g.ShowAttract();
    g.enabled = true;
}
return report;
