var source = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
var shot = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
UnityEngine.ImageConversion.LoadImage(source, System.IO.File.ReadAllBytes("Assets/Prototype/Game3/Art/Crown.png"));
UnityEngine.ImageConversion.LoadImage(shot, System.IO.File.ReadAllBytes("tmp/g3crown-current.png"));
// g3crown-display.cs stages the crown at this cabinet pixel; do not read a later Play/Edit Mode position.
var centre = new UnityEngine.Vector3(128, 186, 0);
int bestX = 0, bestY = 0, bestError = int.MaxValue;
for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
{
    int x0 = UnityEngine.Mathf.FloorToInt(centre.x - source.width * .5f) + dx;
    int y0 = UnityEngine.Mathf.FloorToInt(centre.y - source.height * .5f) + dy;
    int error = 0;
    for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
    {
        UnityEngine.Color32 expected = source.GetPixel(x, y);
        UnityEngine.Color32 actual = shot.GetPixel(x0 + x, y0 + y);
        if (expected.a < 128) expected = new UnityEngine.Color32(0, 0, 0, 255);
        if (System.Math.Abs(actual.r - expected.r) + System.Math.Abs(actual.g - expected.g) + System.Math.Abs(actual.b - expected.b) > 8) error++;
    }
    if (error < bestError) { bestError = error; bestX = x0; bestY = y0; }
}
var enlarged = new UnityEngine.Texture2D(304, 208, UnityEngine.TextureFormat.RGBA32, false);
for (int y = 0; y < enlarged.height; y++) for (int x = 0; x < enlarged.width; x++)
    enlarged.SetPixel(x, y, shot.GetPixel(bestX - 2 + x / 16, bestY - 3 + y / 16));
enlarged.Apply();
System.IO.File.WriteAllBytes("tmp/g3crown-render-zoom.png", enlarged.EncodeToPNG());
var sourceRows = new System.Collections.Generic.List<string>();
var renderedRows = new System.Collections.Generic.List<string>();
for (int y = source.height - 1; y >= 0; y--)
{
    string a = "", b = "";
    for (int x = 0; x < source.width; x++)
    {
        UnityEngine.Color32 c = source.GetPixel(x, y), d = shot.GetPixel(bestX + x, bestY + y);
        a += c.a < 128 || c.r < 100 ? '.' : c.b > 200 ? 'W' : 'Y';
        b += d.r < 100 ? '.' : d.b > 200 ? 'W' : 'Y';
    }
    sourceRows.Add(a); renderedRows.Add(b);
}
UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(shot); UnityEngine.Object.DestroyImmediate(enlarged);
return new { mismatch = bestError, x = bestX, y = bestY, expected = sourceRows, actual = renderedRows };
