var asm = typeof(UnityEditor.EditorWindow).Assembly;
var sizesType = asm.GetType("UnityEditor.GameViewSizes");
var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
var groupType = group.GetType(); var sizeType = asm.GetType("UnityEditor.GameViewSize");
int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
int target = -1, custom = -1;
for (int i = 0; i < total; i++) {
  var s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[]{ i });
  int w = (int)sizeType.GetProperty("width").GetValue(s), h = (int)sizeType.GetProperty("height").GetValue(s);
  if (w == 320 && h == 320 && target < 0) target = i;
  if (w == 960 && h == 960) custom = i;
}
var gvType = asm.GetType("UnityEditor.GameView");
var gv = UnityEditor.EditorWindow.GetWindow(gvType, false, null, false);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
gvType.GetMethod("SizeSelectionCallback", flags).Invoke(gv, new object[]{ target, null });
if (custom >= 0) {
  int builtin = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);
  groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[]{ custom - builtin });
}
gv.Repaint();
return "selected " + target + " removed960=" + (custom >= 0) + " now=" + UnityEditor.Handles.GetMainGameViewSize();
