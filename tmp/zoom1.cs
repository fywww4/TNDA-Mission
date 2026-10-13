var path = "Assets/Screenshots/Game3-fx.png";
var src = new UnityEngine.Texture2D(2,2); UnityEngine.ImageConversion.LoadImage(src, System.IO.File.ReadAllBytes(path));
int k = 3; var up = new UnityEngine.Texture2D(src.width*k, src.height*k, UnityEngine.TextureFormat.RGB24, false);
var px = src.GetPixels32(); var o = new UnityEngine.Color32[up.width*up.height];
for (int y=0;y<up.height;y++) for(int x=0;x<up.width;x++) o[y*up.width+x]=px[(y/k)*src.width+x/k];
up.SetPixels32(o); up.Apply(); System.IO.File.WriteAllBytes("tmp/Game3-fx-x3.png", up.EncodeToPNG()); return "ok";
