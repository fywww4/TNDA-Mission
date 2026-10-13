U=/c/Users/a0900/AppData/Local/Unity/bin/unity.exe; F="--format json --no-banner --caller plugin --skill unity-cli --result-only"
Q='var g=UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>(); var s=""; foreach (var c in g.cars) s+=c.DriverName+(c.eliminated?"[OUT]":"")+" active="+c.gameObject.activeSelf+" pos="+c.position+" | "; return g.Phase+" t="+g.Elapsed.ToString("0.0")+" "+s;'
bash tmp/game2/compile.sh
$U command editor_play $F
for i in $(seq 1 30); do $U command editor_status $F 2>/dev/null | grep -q '"playMode": *"playing"' && break; sleep 1; done; sleep 2
$U command eval $F 'UnityEngine.Application.runInBackground = true; var g=UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>(); g.StartRun(1); return "ok";' | grep result
sleep 5
$U command eval $F 'var g=UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.DemolitionGame>(); typeof(TNDA.Prototype.ArcadeGame).GetProperty("Elapsed").SetValue(g, 59.8f); g.cars[0].score = 999; UnityEngine.Time.timeScale = .15f; return "jump";' | grep result
sleep 2
$U command capture_game_view $F --width 320 --height 320 --save_path Screenshots/Game3-spinout.png | grep -iE 'savedPath|message'
$U command eval $F "$Q" | grep result
$U command eval $F 'UnityEngine.Time.timeScale = 1; return "normal";' | grep result
sleep 3
$U command eval $F "$Q" | grep result
$U command editor_stop $F; $U command console_status $F | grep -E 'compilationFailed|consoleErrors|consoleWarnings'
$U command eval_file $F tmp/zoom1.cs | grep result
