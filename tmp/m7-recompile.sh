# Recompile GAME3 scripts, wait for the domain reload, print compile result and Console ground truth.
# Usage: bash tmp/m7-recompile.sh [test-filter]  (runs EditMode tests after, filtered if given)
U='/c/Users/a0900/AppData/Local/Unity/bin/unity.exe'; P='C:\Users\a0900\Desktop\TNDA Mission'
"$U" command recompile --project-path "$P" --result-only >/dev/null
sleep 5
for i in $(seq 1 40); do
  e=$("$U" command editor_status --project-path "$P" --result-only 2>&1)
  echo "$e" | grep -q '"compiling": false' && echo "$e" | grep -q '"domainReloadInProgress": false' && break
  sleep 4
done
"$U" command recompile_status --project-path "$P" --result-only | grep -E '"status"|failed|error CS|warning CS'
"$U" command console_status --project-path "$P" --result-only | grep -E 'compilationFailed|consoleErrors|consoleWarnings'
if [ "$1" != "" ]; then
  if [ "$1" = "all" ]; then f=""; else f="--filter $1"; fi
  "$U" command run_tests --mode editor $f --project-path "$P" --result-only | grep -E '"(Total|Passed|Failed)"|"Status": "Failed"' -A0 | head -12
fi
