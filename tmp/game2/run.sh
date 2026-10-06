U=/c/Users/a0900/AppData/Local/Unity/bin/unity.exe; F="--format json --no-banner --caller plugin --skill unity-cli --result-only"
until $U command editor_status $F 2>/dev/null | grep -q '"playMode": *"stopped"'; do sleep 5; done
$U command eval_file $F --timeout 180 "tmp/game2/$1.cs" | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{const j=JSON.parse(s);console.log(j.result||JSON.stringify(j,null,1))})'
