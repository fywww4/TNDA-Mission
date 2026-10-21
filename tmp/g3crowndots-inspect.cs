var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var shot = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
UnityEngine.ImageConversion.LoadImage(shot, System.IO.File.ReadAllBytes("tmp/g3crown-current.png"));
var rows = new System.Collections.Generic.List<string>();
for (int y = 197; y >= 180; y--)
{
    string row = y + ": ";
    for (int x = 119; x < 138; x++)
    {
        UnityEngine.Color32 c = shot.GetPixel(x, y);
        row += c.r < 100 ? '.' : c.b > 200 ? 'W' : 'Y';
    }
    rows.Add(row);
}
UnityEngine.Object.DestroyImmediate(shot);
var sprites = new System.Collections.Generic.List<object>();
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsSortMode.None))
{
    if (!r.enabled) continue;
    var min = g.sceneCamera.WorldToScreenPoint(r.bounds.min);
    var max = g.sceneCamera.WorldToScreenPoint(r.bounds.max);
    if (min.x < 138 && max.x > 119 && min.y < 198 && max.y > 180)
        sprites.Add(new { name = r.name, parent = r.transform.parent?.name, min = min.ToString(), max = max.ToString(), sprite = r.sprite?.name });
}
var texts = new System.Collections.Generic.List<object>();
foreach (var t in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(UnityEngine.FindObjectsSortMode.None))
{
    if (!t.enabled) continue;
    t.ForceMeshUpdate();
    var min = t.transform.TransformPoint(t.textBounds.min);
    var max = t.transform.TransformPoint(t.textBounds.max);
    if (min.x < 138 && max.x > 119 && min.y < 198 && max.y > 180)
        texts.Add(new { name = t.name, text = t.text, min = min.ToString(), max = max.ToString() });
}
return new { rows, sprites, texts };
