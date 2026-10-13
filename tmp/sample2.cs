var t = new UnityEngine.Texture2D(2,2); UnityEngine.ImageConversion.LoadImage(t, System.IO.File.ReadAllBytes("Assets/Prototype/Game3/Art/Source/ExplosionSource.png"));
var s = new System.Text.StringBuilder();
int[][] pts = { new[]{100,500}, new[]{180,230}, new[]{600,200}, new[]{300,300}, new[]{280,420}, new[]{330,380}, new[]{410,380}, new[]{360,330}, new[]{250,300}, new[]{560,330}, new[]{400,480}, new[]{230,450}, new[]{470,340}};
foreach (var p in pts) { var c = t.GetPixel(p[0], t.height - 1 - p[1]); float h, sa, v; UnityEngine.Color.RGBToHSV(c, out h, out sa, out v); s.Append("("+p[0]+","+p[1]+") rgb="+(int)(c.r*255)+","+(int)(c.g*255)+","+(int)(c.b*255)+" h="+(int)(h*360)+" s="+sa.ToString("0.00")+" v="+v.ToString("0.00")+"\n"); }
return s.ToString();
