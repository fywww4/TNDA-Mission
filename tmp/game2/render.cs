var path = "Assets/Scenes/GAME2.unity";
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
TNDA.Prototype.EliminationRaceGame game = null;
foreach (var g in scene.GetRootGameObjects()) if (g.name == "GAME2 Prototype") game = g.GetComponent<TNDA.Prototype.EliminationRaceGame>();
var cam = game.sceneCamera;
var rt = new UnityEngine.RenderTexture(320, 320, 24); rt.filterMode = UnityEngine.FilterMode.Point;
cam.scene = scene; cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; cam.scene = default(UnityEngine.SceneManagement.Scene);
UnityEngine.RenderTexture.active = rt; var tex = new UnityEngine.Texture2D(320, 320, UnityEngine.TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0,0,320,320),0,0); tex.Apply(); UnityEngine.RenderTexture.active = null;
var big = new UnityEngine.Texture2D(960, 960, UnityEngine.TextureFormat.RGB24, false);
for (int y=0;y<960;y++) for(int x=0;x<960;x++) big.SetPixel(x,y,tex.GetPixel(x/3,y/3));
big.Apply();
System.IO.File.WriteAllBytes("tmp/game2/world.png", big.EncodeToPNG());
foreach (var name in new[]{"CarBlue","CarYellow","CarWhite"}) {
  var src = new UnityEngine.Texture2D(2,2); UnityEngine.ImageConversion.LoadImage(src, System.IO.File.ReadAllBytes("Assets/Prototype/Game2/Art/"+name+".png"));
  var up = new UnityEngine.Texture2D(src.width*12, src.height*12, UnityEngine.TextureFormat.RGBA32, false);
  for (int y=0;y<up.height;y++) for(int x=0;x<up.width;x++){ var c=src.GetPixel(x/12,y/12); up.SetPixel(x,y, c.a<.5f? new UnityEngine.Color(.5f,.2f,.5f): c);} up.Apply();
  System.IO.File.WriteAllBytes("tmp/game2/"+name+"_x12.png", up.EncodeToPNG());
}
UnityEngine.Object.DestroyImmediate(rt);
if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
return "ok";
