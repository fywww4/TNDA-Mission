var g = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var none = new TNDA.Prototype.ArcadeInput();
var checks = new System.Collections.Generic.Dictionary<string, bool>();
var leader = g.cars[2];
var label = g.carLabels[2];
label.ForceMeshUpdate();
var top = label.transform.TransformPoint(new UnityEngine.Vector3(label.textBounds.center.x, label.textBounds.max.y, 0));
float topY = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, top).y;
float crownBottom = g.sceneCamera.WorldToScreenPoint(g.crownView.bounds.min).y;
var pivot = g.crownView.sprite.pivot;
checks["whole pixel pivot"] = pivot.x == UnityEngine.Mathf.Round(pivot.x) && pivot.y == UnityEngine.Mathf.Round(pivot.y);
checks["clear of name"] = crownBottom - topY >= 3;
checks["leader crown shown"] = g.crownView.gameObject.activeSelf;
foreach (var c in g.cars) c.score = 0;
g.Advance(.001f, none, none);
checks["zero scores hide crown"] = !g.crownView.gameObject.activeSelf;
g.cars[1].score = 900;
g.Advance(.001f, none, none);
checks["crown follows new leader"] = g.crownView.gameObject.activeSelf
    && UnityEngine.Vector2.Distance(g.crownView.transform.position,
        g.cars[1].position + UnityEngine.Vector2.up * g.crownHeight) < .03f;
g.cars[1].alive = false;
g.cars[1].respawn = 3;
g.Advance(.001f, none, none);
checks["wrecked leader hides crown"] = !g.crownView.gameObject.activeSelf;
g.cars[1].alive = true;
g.cars[1].respawn = 0;
g.cars[1].score = 0;
leader.score = 800;
g.Advance(.001f, none, none);
var shot = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
UnityEngine.ImageConversion.LoadImage(shot, System.IO.File.ReadAllBytes("tmp/g3crown-current.png"));
var crop = new UnityEngine.Texture2D(192, 360, UnityEngine.TextureFormat.RGBA32, false);
for (int y = 0; y < crop.height; y++) for (int x = 0; x < crop.width; x++)
    crop.SetPixel(x, y, shot.GetPixel(104 + x / 4, 108 + y / 4));
crop.Apply(); System.IO.File.WriteAllBytes("tmp/g3crown-fixed.png", crop.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(shot); UnityEngine.Object.DestroyImmediate(crop);
return new { checks, gapPixels = crownBottom - topY, frame = UnityEngine.Time.frameCount };
