UnityEditor.AssetDatabase.ReleaseCachedFileHandles();
var sprite = TNDA.Prototype.Editor.Game3ItemArt.CherrySprite();
var src = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
UnityEngine.ImageConversion.LoadImage(src, System.IO.File.ReadAllBytes("Assets/Prototype/Game3/Art/Cherry.png"));
int scale = 16, padding = 16;
var up = new UnityEngine.Texture2D(src.width * scale + padding * 2, src.height * scale + padding * 2, UnityEngine.TextureFormat.RGBA32, false);
for (int y = 0; y < up.height; y++)
    for (int x = 0; x < up.width; x++)
    {
        var color = new UnityEngine.Color32(32, 32, 32, 255);
        if (x >= padding && y >= padding && x < up.width - padding && y < up.height - padding)
        {
            var pixel = src.GetPixel((x - padding) / scale, (y - padding) / scale);
            if (pixel.a > .5f) color = pixel;
        }
        up.SetPixel(x, y, color);
    }
up.Apply();
System.IO.File.WriteAllBytes("tmp/cherry-redrawn.png", up.EncodeToPNG());
var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(sprite));
var game = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>();
var result = new { width = src.width, height = src.height, ppu = sprite.pixelsPerUnit,
    pointFilter = importer.filterMode == UnityEngine.FilterMode.Point, noMipmaps = !importer.mipmapEnabled,
    uncompressed = importer.textureCompression == UnityEditor.TextureImporterCompression.Uncompressed,
    wired = game != null && game.itemSprites[2] == sprite,
    sceneDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };
UnityEngine.Object.DestroyImmediate(src);
UnityEngine.Object.DestroyImmediate(up);
return result;
