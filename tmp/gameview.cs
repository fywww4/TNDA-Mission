var asm = typeof(UnityEditor.EditorWindow).Assembly;
var sizesType = asm.GetType("UnityEditor.GameViewSizes");
var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
var sizes = singleton.GetProperty("instance").GetValue(null);
var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
var groupType = group.GetType();
int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
var sizeType = asm.GetType("UnityEditor.GameViewSize");
var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
int found = -1; var names = new System.Collections.Generic.List<string>();
for (int i = 0; i < total; i++) {
  var s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[]{ i });
  int w = (int)sizeType.GetProperty("width").GetValue(s), h = (int)sizeType.GetProperty("height").GetValue(s);
  string label = (string)sizeType.GetProperty("baseText").GetValue(s);
  names.Add(i + ":" + label + " " + w + "x" + h);
  if (w == 960 && h == 960 && sizeType.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") found = i;
}
if (found < 0) {
  var ctor = sizeType.GetConstructor(new[]{ sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
  var newSize = ctor.Invoke(new object[]{ System.Enum.Parse(sizeTypeEnum, "FixedResolution"), 960, 960, "TNDA 320x3" });
  groupType.GetMethod("AddCustomSize").Invoke(group, new[]{ newSize });
  found = total;
}
var gvType = asm.GetType("UnityEditor.GameView");
var gv = UnityEditor.EditorWindow.GetWindow(gvType, false, null, false);
gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(gv, new object[]{ found, null });
var lowRes = gvType.GetProperty("lowResolutionForAspectRatios", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
if (lowRes != null && lowRes.CanWrite) lowRes.SetValue(gv, false);
gv.Repaint();
return "selected " + found + " now=" + UnityEditor.Handles.GetMainGameViewSize() + " | " + string.Join(", ", names);
