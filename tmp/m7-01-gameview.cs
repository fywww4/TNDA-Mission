// Ticket 01: Game view fixed at 512x448 (adds the custom size once).
var asm = typeof(UnityEditor.EditorWindow).Assembly;
var sizesType = asm.GetType("UnityEditor.GameViewSizes");
var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
var groupType = group.GetType(); var sizeType = asm.GetType("UnityEditor.GameViewSize");
var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
int found = -1;
for (int i = 0; i < total; i++) {
  var s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[]{ i });
  int w = (int)sizeType.GetProperty("width").GetValue(s), h = (int)sizeType.GetProperty("height").GetValue(s);
  if (w == 512 && h == 448 && sizeType.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") { found = i; break; }
}
if (found < 0) {
  var ctor = sizeType.GetConstructor(new[]{ sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
  groupType.GetMethod("AddCustomSize").Invoke(group, new[]{ ctor.Invoke(new object[]{ System.Enum.Parse(sizeTypeEnum, "FixedResolution"), 512, 448, "Mode 7 512x448" }) });
  found = total;
}
var gvType = asm.GetType("UnityEditor.GameView");
var gv = UnityEditor.EditorWindow.GetWindow(gvType, false, null, false);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
gvType.GetMethod("SizeSelectionCallback", flags).Invoke(gv, new object[]{ found, null });
var lowRes = gvType.GetProperty("lowResolutionForAspectRatios", flags);
if (lowRes != null && lowRes.CanWrite) lowRes.SetValue(gv, false);
gv.Repaint();
return "selected " + found + " now=" + UnityEditor.Handles.GetMainGameViewSize();
