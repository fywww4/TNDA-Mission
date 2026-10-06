U=/c/Users/a0900/AppData/Local/Unity/bin/unity.exe; F="--format json --no-banner --caller plugin --skill unity-cli --result-only"
until $U command editor_status $F 2>/dev/null | grep -q '"playMode": *"stopped"'; do sleep 5; done
$U command editor_play $F
for i in $(seq 1 30); do $U command editor_status $F 2>/dev/null | grep -q '"playMode": *"playing"' && break; sleep 1; done; sleep 1
$U command eval $F 'UnityEngine.Application.runInBackground = true; UnityEngine.Time.timeScale = .05f; UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.ArcadeGame>().StartRun(1); return "start";' | grep result
sleep 1
$U command capture_game_view $F --width 640 --height 640 --save_path Screenshots/$1-countdown.png | grep -iE 'savedPath|message'
$U command eval $F 'var g=UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.ArcadeGame>(); return g.Phase+" label="+g.countdownLabel.text;' | grep result
$U command editor_stop $F
