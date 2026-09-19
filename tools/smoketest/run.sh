#!/bin/sh
# Headless runtime check: boots the REAL engine, drives real input, and runs a host
# and TWO guests (three players) against each other over localhost. Catches what a typecheck cannot:
# null nodes, bad RPC paths, stale peer ids, scene-change ordering.
#
#   sh tools/smoketest/run.sh /path/to/Godot_v4.7.2-stable_mono_linux.x86_64
#
# Needs the .NET 8 SDK. Works offline: the Godot.NET.Sdk packages come from the
# nupkgs folder that ships beside the editor binary.
set -e
G="$1"; [ -x "$G" ] || { echo "usage: run.sh <godot mono binary>"; exit 2; }
NUPKGS="$(dirname "$G")/GodotSharp/Tools/nupkgs"
SRC="$(cd "$(dirname "$0")/../.." && pwd)"
W=/tmp/warships_smoke
rm -rf "$W"; mkdir -p "$W"; cp -r "$SRC"/. "$W"/; rm -rf "$W/.godot"
cp "$SRC/tools/smoketest/SmokeTest.cs.txt" "$W/scripts/_Test.cs"
sed -i 's#^Net="\*res://scripts/Net.cs"#&\n_Test="*res://scripts/_Test.cs"#' "$W/project.godot"
# Its own user:// folder: the test creates and DELETES characters, and must never
# touch real ones. Wiped each run so migration and first-run paths are exercised.
sed -i 's#^config/name="Warships"#config/name="WarshipsSmoke"#' "$W/project.godot"
grep -q 'config/name="WarshipsSmoke"' "$W/project.godot" || { echo "could not isolate user data; refusing to run"; exit 2; }
rm -rf "$HOME/.local/share/godot/app_userdata/WarshipsSmoke"
cat > "$W/nuget.config" <<X
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /><add key="godot" value="$NUPKGS" /></packageSources></configuration>
X
cd "$W"
# a test that does not compile tests nothing: stop here, loudly
if ! dotnet build -v q -nologo > build.log 2>&1 || grep -q " error " build.log; then
  grep -E " error " build.log | sed 's#.*/scripts/##' | sort -u | head -20
  echo "SMOKE TEST FAILED (the build failed)"; exit 1
fi
grep -E "Warning\(s\)|Error\(s\)" build.log
timeout 180 "$G" --headless --import --path . >/dev/null 2>&1 || true

F='PASS|FAIL|DONE|Exception|   at |ERROR: [^B]|^  [a-z]'
N='RID alloc|PagedAlloc'
set +e
# fixed 60 fps: identical frame timing every run, so the DPS checks are exact
timeout 1200 "$G" --headless --fixed-fps 60 --path . -- solo 2>&1 | grep --line-buffered -E "$F" | grep --line-buffered -vE "$N" | sed -u 's/^/[solo]  /' > solo.log
(timeout 60 "$G" --headless --path . -- host 2>&1 | grep --line-buffered -E "$F" | grep --line-buffered -vE "$N" | sed -u 's/^/[host]  /' > host.log) &
sleep 0.5
(timeout 60 "$G" --headless --path . -- guest2 2>&1 | grep --line-buffered -E "$F" | grep --line-buffered -vE "$N" | sed -u 's/^/[third] /' > guest2.log) &
timeout 60 "$G" --headless --path . -- guest 2>&1 | grep --line-buffered -E "$F" | grep --line-buffered -vE "$N" | sed -u 's/^/[guest] /' > guest.log
wait
cat solo.log host.log guest.log guest2.log
BAD=$(cat solo.log host.log guest.log guest2.log | grep -cE "FAIL|Exception|ERROR")
DONE=$(cat solo.log host.log guest.log guest2.log | grep -c "DONE")
if [ "$BAD" -gt 0 ] || [ "$DONE" -ne 4 ]; then echo "SMOKE TEST FAILED ($BAD problems, $DONE/4 runs finished)"; exit 1; fi
echo "SMOKE TEST PASSED"
