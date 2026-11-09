// Lists the current Unity Console entries (errors and warnings) with their first line. eval_file body.
var t = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
var e = System.Type.GetType("UnityEditor.LogEntry, UnityEditor");
int n = (int)t.GetMethod("StartGettingEntries").Invoke(null, null);
var entry = System.Activator.CreateInstance(e);
var sb = new System.Text.StringBuilder();
for (int i = 0; i < n; i++)
{
    t.GetMethod("GetEntryInternal").Invoke(null, new object[] { i, entry });
    int mode = (int)e.GetField("mode").GetValue(entry);
    string msg = (string)e.GetField("message").GetValue(entry);
    sb.AppendLine(mode + " | " + msg.Split('\n')[0]);
}
t.GetMethod("EndGettingEntries").Invoke(null, null);
return n + " entries\n" + sb;
