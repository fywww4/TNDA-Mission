UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var sb = new System.Text.StringBuilder();
foreach (var n in new[]{"Shooting","Countdown","Hit","Speed"}) {
  var path = "Assets/Prototype/Audio/"+n+".mp3";
  var imp = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(path);
  var s = imp.defaultSampleSettings; s.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad; imp.defaultSampleSettings = s; imp.forceToMono = true; imp.SaveAndReimport();
  var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(path);
  var data = new float[clip.samples * clip.channels]; clip.GetData(data, 0);
  sb.Append(n+": len="+clip.length.ToString("0.00")+"s hz="+clip.frequency+" ch="+clip.channels+"\n  env(0.1s): ");
  int step = clip.frequency/10 * clip.channels;
  for (int i=0;i<data.Length;i+=step){ float m=0; for(int j=i;j<System.Math.Min(i+step,data.Length);j++) m=System.Math.Max(m,System.Math.Abs(data[j])); sb.Append((int)(m*9.99f)); }
  sb.Append("\n");
}
return sb.ToString();
