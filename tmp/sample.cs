var t = new UnityEngine.Texture2D(2,2); UnityEngine.ImageConversion.LoadImage(t, System.IO.File.ReadAllBytes("Assets/Prototype/Game3/Art/Source/ItemCrateSource.png"));
var s = new System.Text.StringBuilder();
// image coords given top-down; convert to texture y
int[][] pts = { new[]{35,200}, new[]{30,130}, new[]{100,125}, new[]{100,290}, new[]{60,200}, new[]{90,200}, new[]{120,200}, new[]{150,200}, new[]{180,200}, new[]{175,140}, new[]{75,160}, new[]{250,200}, new[]{200,95}};
foreach (var p in pts) { var c = t.GetPixel(p[0], t.height - 1 - p[1]); float h, sa, v; UnityEngine.Color.RGBToHSV(c, out h, out sa, out v); s.Append("("+p[0]+","+p[1]+") rgb="+(int)(c.r*255)+","+(int)(c.g*255)+","+(int)(c.b*255)+" a="+c.a.ToString("0.0")+" h="+(int)(h*360)+" s="+sa.ToString("0.00")+" v="+v.ToString("0.00")+"\n"); }
return s.ToString();
