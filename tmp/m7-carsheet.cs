// GAME3_Mode7: import CarSheet.png as 16 car frames (32 PPU, point, uncompressed, pivot on the ground line) and wire
// them plus CarSheetPalette.mat into Mode7Stage. Safe to re-run.
const string sheetPath = "Assets/Prototype/Game3/Art/CarSheet.png", matPath = "Assets/Prototype/Game3/Art/CarSheetPalette.mat";
UnityEditor.AssetDatabase.Refresh();
var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(sheetPath);
imp.textureType = UnityEditor.TextureImporterType.Sprite;
imp.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
imp.spritePixelsPerUnit = 32;
imp.filterMode = UnityEngine.FilterMode.Point;
imp.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
imp.mipmapEnabled = false;
imp.alphaIsTransparency = true;
var metas = new UnityEditor.SpriteMetaData[16];
for (int i = 0; i < 16; i++)
  metas[i] = new UnityEditor.SpriteMetaData { name = "CarSheet_" + i.ToString("00"), rect = new UnityEngine.Rect(i * 48, 0, 48, 48),
    alignment = (int)UnityEngine.SpriteAlignment.Custom, pivot = new UnityEngine.Vector2(.5f, 7f / 48) };
#pragma warning disable 618
imp.spritesheet = metas;
#pragma warning restore 618
imp.SaveAndReimport();
var frames = new UnityEngine.Sprite[16];
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath))
  if (o is UnityEngine.Sprite sp) frames[int.Parse(sp.name.Substring(9))] = sp;
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(matPath);
if (mat == null) {
  mat = new UnityEngine.Material(UnityEngine.Shader.Find("TNDA/Game3 Car Sheet Palette"));
  UnityEditor.AssetDatabase.CreateAsset(mat, matPath);
}
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GAME3_Mode7.unity");
var s = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.Mode7Stage>();
s.carFrames = frames; s.carSheetMaterial = mat;
UnityEditor.EditorUtility.SetDirty(s);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
return "frames=" + System.Linq.Enumerable.Count(frames, f => f != null) + " shader=" + mat.shader.name + " scene=" + scene.path;
