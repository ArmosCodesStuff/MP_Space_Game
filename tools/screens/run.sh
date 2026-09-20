#!/bin/sh
# Renders real screenshots of the game (select screen, creator, both classes in the
# hub, the K window, a bomber strike) into /tmp/shots/, so layout and art can be
# LOOKED AT rather than assumed. The smoke test is headless and cannot see.
#
#   sh tools/screens/run.sh /path/to/Godot_v4.7.2-stable_mono_linux.x86_64
#
# Needs: .NET 8 SDK, and a virtual display with software OpenGL:
#   apt-get install -y xvfb libgl1-mesa-dri libglx-mesa0 libgl1 libxcursor1 libxinerama1 libxrandr2 libxi6
set -e
G="$1"; [ -x "$G" ] || { echo "usage: run.sh <godot mono binary>"; exit 2; }
NUPKGS="$(dirname "$G")/GodotSharp/Tools/nupkgs"
SRC="$(cd "$(dirname "$0")/../.." && pwd)"
W=/tmp/warships_shots
rm -rf "$W"; mkdir -p "$W" /tmp/shots; cp -r "$SRC"/. "$W"/; rm -rf "$W/.godot" /tmp/shots/*
cp "$SRC/tools/screens/Shots.cs.txt" "$W/scripts/_Shots.cs"
sed -i 's#^Net="\*res://scripts/Net.cs"#&\n_Shots="*res://scripts/_Shots.cs"#' "$W/project.godot"
# its own user:// -- it creates characters
sed -i 's#^config/name="Warships"#config/name="WarshipsShots"#' "$W/project.godot"
rm -rf "$HOME/.local/share/godot/app_userdata/WarshipsShots"
cat > "$W/nuget.config" <<X
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /><add key="godot" value="$NUPKGS" /></packageSources></configuration>
X
cd "$W"
dotnet build -v q -nologo | grep -E "error|Error\(s\)"
timeout 180 "$G" --headless --import --path . >/dev/null 2>&1 || true
# A restart can leave a stale display lock behind, and then Xvfb will not start and
# Godot silently draws nothing. Clear it, start the display, and refuse to go on
# without one.
if ! pgrep Xvfb >/dev/null; then
  rm -f /tmp/.X99-lock /tmp/.X11-unix/X99
  Xvfb :99 -screen 0 1600x900x24 >/dev/null 2>&1 &
  sleep 1.5
fi
pgrep Xvfb >/dev/null || { echo "NO VIRTUAL DISPLAY -- nothing was rendered"; exit 2; }
# Forward+ needs Vulkan; the compatibility renderer runs on Mesa's software GL
DISPLAY=:99 timeout 400 "$G" --path . --rendering-driver opengl3 --rendering-method gl_compatibility \
    --windowed --resolution 1600x900 2>&1 | grep --line-buffered -E "^shot|^LINT|Exception|^SWEEP" | tee /tmp/sweep_out.log
# a sweep cut off part-way (a timeout) must never pass as clean
grep -q "^SWEEP DONE" /tmp/sweep_out.log || echo "SWEEP INCOMPLETE -- the run was cut off; later frames were not rendered"
ls /tmp/shots
