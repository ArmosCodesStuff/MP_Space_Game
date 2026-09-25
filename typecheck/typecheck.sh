#!/bin/sh
# Type-checks every script with the real Roslyn compiler.
#
# PREFERRED: if GodotSharp.dll is present in this folder, it is referenced directly
# and the hand-written stub is ignored entirely. That makes this check identical to
# what Godot itself compiles -- same reference assembly, same compiler -- and removes
# the whole class of "the stub invented an API" and "the stub was incomplete" bugs.
#
# FALLBACK: GodotStub.cs, which only proves shape. Errors in the stub are FATAL,
# because Roslyn binds declarations before method bodies: one bad declaration in the
# stub means no method body is ever checked, and every script error vanishes.
# THE PROJECT'S OWN TARGET, read from the .csproj rather than written here as well, so this rung
# always checks the game against the framework it is actually built for.
TFM=$(sed -n 's/.*<TargetFramework>\(net[0-9]*\.[0-9]*\)<.*/\1/p' ../Warships.csproj | head -1)
MAJOR=$(echo "$TFM" | sed 's/^net//; s/\..*$//')
CSC=$(find /usr/lib/dotnet -name csc.dll -path "*Roslyn*" | head -1)
REF=$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/"$MAJOR"*/ref/"$TFM"/ | head -1)

# No compiler means no check. Without this guard the harness found nothing, ran
# nothing, and the error grep below turned "no compiler" into "0 errors".
if [ -z "$CSC" ] || [ -z "$REF" ]; then
  echo "NO .NET $MAJOR SDK FOUND -- nothing was checked. Install dotnet-sdk-$MAJOR.0." >&2
  exit 2
fi
R=""; for d in "$REF"*.dll; do R="$R -r:$d"; done
rm -f /tmp/typecheck.dll

# THE HARNESS IS SOURCE TOO (as typecheck.ps1): SmokeTest.cs.txt and Shots.cs.txt are compiled into
# the game by their runners, so they are copied to .cs and handed to the same compiler.
HARNESS=""
for h in ../tools/smoketest/SmokeTest.cs.txt ../tools/screens/Shots.cs.txt; do
  [ -f "$h" ] || continue
  dst="/tmp/typecheck_$(basename "$h" .txt)"
  cp "$h" "$dst"; HARNESS="$HARNESS $dst"
done

if [ -f GodotSharp.dll ]; then
  EXTRA="-r:GodotSharp.dll"
  [ -f GodotSharpEditor.dll ] && EXTRA="$EXTRA -r:GodotSharpEditor.dll"
  SRC="../scripts/*.cs $HARNESS"
  MODE="REAL GodotSharp.dll"
else
  EXTRA=""
  SRC="../scripts/*.cs $HARNESS GodotStub.cs"
  MODE="hand-written stub (weaker -- drop GodotSharp.dll here to fix)"
fi

OUT=$(dotnet "$CSC" -target:library -nologo -langversion:12.0 -nostdlib+ \
      -define:GODOT -out:/tmp/typecheck.dll $SRC $EXTRA $R 2>&1 | grep -E "error CS")

if [ -z "$GODOT_QUIET" ]; then echo "checking against: $MODE" >&2; fi

if [ ! -f GodotSharp.dll ]; then
  STUB=$(printf '%s\n' "$OUT" | grep -c "GodotStub")
  if [ "$STUB" -gt 0 ]; then
    echo "STUB IS BROKEN -- script checking did NOT run. Fix these first:"
    printf '%s\n' "$OUT" | grep "GodotStub"
    exit 1
  fi
fi
if [ -n "$OUT" ]; then
  printf '%s\n' "$OUT" | grep -v '^$'
  exit 1
fi
# Zero errors only counts if the compiler actually produced its output.
if [ ! -f /tmp/typecheck.dll ]; then
  echo "COMPILER PRODUCED NOTHING -- result is not trustworthy." >&2
  exit 2
fi
echo "0 errors." >&2
