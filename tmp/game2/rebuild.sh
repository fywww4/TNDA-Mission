U=/c/Users/a0900/AppData/Local/Unity/bin/unity.exe; F="--format json --no-banner --caller plugin --skill unity-cli --result-only"
until $U command editor_status $F 2>/dev/null | grep -q '"playMode": *"stopped"'; do sleep 5; done
$U command recompile $F >/dev/null
for i in $(seq 1 60); do $U command recompile_status $F 2>/dev/null | grep -qE '"status": *"(completed|up_to_date)"' && break; sleep 3; done
until $U command editor_status $F 2>/dev/null | grep -q '"compiling": false'; do sleep 2; done
$U command console_status $F | grep -E 'compilationFailed|consoleErrors'
until $U command editor_status $F 2>/dev/null | grep -q '"playMode": *"stopped"'; do sleep 5; done
$U command eval $F --timeout 120 'return TNDA.Prototype.Editor.Game2SceneBuilder.Build(true);'
$U command eval_file $F --timeout 120 "tmp/game2/render.cs"
