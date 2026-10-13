// Ticket 10: the sound and font setup tools still apply to GAME3 (in memory; the scene is not saved).
var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
System.Func<string> snap = () =>
{
    var s = new System.Text.StringBuilder();
    s.Append(g.countdownClip?.name).Append('|').Append(g.hitClip?.name).Append('|').Append(g.engineClip?.name).Append('|').Append(g.countdownLabel?.name).Append('|').Append(g.countdownSeconds);
    foreach (var t in g.GetComponentsInChildren<TMPro.TMP_Text>(true)) s.Append('|').Append(t.name).Append(':').Append(t.font?.name).Append('@').Append(t.fontSize).Append(t.fontStyle);
    return s.ToString();
};
bool dirtyBefore = g.gameObject.scene.isDirty;
string before = snap();
TNDA.Prototype.Editor.ArcadeSoundSetup.Apply(g);
TNDA.Prototype.Editor.ArcadeFontSetup.Apply(g);
string after = snap();
return new { sameAfterApply = before == after, clips = g.countdownClip != null && g.hitClip != null && g.engineClip != null, label = g.countdownLabel != null,
    texts = g.GetComponentsInChildren<TMPro.TMP_Text>(true).Length, dirtyBefore };
