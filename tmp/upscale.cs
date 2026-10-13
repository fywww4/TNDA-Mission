foreach (var name in new[]{"ItemCrate","Cherry","Explosion"}) {
  var src = new UnityEngine.Texture2D(2,2); UnityEngine.ImageConversion.LoadImage(src, System.IO.File.ReadAllBytes("Assets/Prototype/Game3/Art/"+name+".png"));
  var up = new UnityEngine.Texture2D(src.width*16, src.height*16, UnityEngine.TextureFormat.RGBA32, false);
  for (int y=0;y<up.height;y++) for(int x=0;x<up.width;x++){ var c=src.GetPixel(x/16,y/16); up.SetPixel(x,y, c.a<.5f? new UnityEngine.Color(.45f,.15f,.45f): c);} up.Apply();
  System.IO.File.WriteAllBytes("tmp/"+name+"_x16.png", up.EncodeToPNG());
}
return "ok";
