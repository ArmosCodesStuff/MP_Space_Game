# docs

## Picking this up cold

If you are a fresh session with no history of this project, read in this order:

1. **`CHANGES.md`** — start with its **Handoff** section: current state, controls, how to verify,
   the developer's standing preferences, open questions and what is next. The `Known broken` list is
   the honest state; trust it over anything else.
2. **`DESIGN.md`** — why it is built this way. Standing rules, the authority model, and the traps
   that have already cost real time.
3. This file for the mechanics of working on it.

**You also need the code.** These documents describe the project; they do not replace it. Whoever
starts the session should attach the project zip (~6 MB, ~27 MB with its git history; 53 scripts;
80 code files, 16778 lines in `version/CODE_SNAPSHOT.txt`).

| File | For |
|---|---|
| `CHANGES.md` | current state, pruned every update — never an archive |
| `DESIGN.md` | reasoning, standing rules, known traps |
| `README.md` | this: how to work on the project |

---

## The stack

- **Godot 4.7.2 (.NET/Mono)**, C# 12, `net8.0`, `Godot.NET.Sdk/4.7.2`
- Scenes: `MainMenu.tscn` -> `CharacterSelect.tscn` -> `Hub.tscn`
- `Net.cs` is an **autoload**, registered in `project.godot`. It must exist before any world does.
- All game code is in `scripts/`. Nothing else compiles — the csproj excludes `tools/` and
  `typecheck/`.

## Playing it

    powershell -ExecutionPolicy Bypass -File play.ps1          one window
    powershell -ExecutionPolicy Bypass -File play.ps1 -Two     two windows, for multiplayer
    powershell -ExecutionPolicy Bypass -File play.ps1 -Editor  open it in the Godot editor

**There is no standalone build.** A Godot .NET project needs the engine to run it: `play.ps1`
finds the engine (same resolver the harnesses use), compiles the C#, and launches the project.
Anyone you send the folder to needs **Godot 4.7.2 .NET (mono)** too, and the **same zip** — the
build handshake refuses a peer on a different build, deliberately.

`-Two` gives you a host and a joiner on one machine. They share one `user://`, so **give each
window its own character before hosting**: two peers writing one character file is the collision
that made a day of save bugs look real.

To make a real `.exe`: open the editor, install the export templates it offers, then
*Project → Export → Windows Desktop*. Not scripted here — nothing in this repo has been exported
or tested that way yet.

## Checking your work

**On Windows, one command does everything:**

    powershell -ExecutionPolicy Bypass -File verify.ps1

typecheck → build → analysers → cross-reference → smoke test **×3** → screenshot sweep →
integrity, then one verdict. `-Quick` stops after the static checks (about a minute).

Nothing has to be passed in, and that is the point — the two things that used to need a human are
handled:

- **`typecheck/GodotSharp.dll`** is gitignored (a build dependency, not game content), so every
  fresh clone or unzipped copy arrives without it and the typecheck quietly drops to the weak
  hand-written stub. `typecheck.ps1` now **copies it from the NuGet cache** when it is missing and
  says so.
- **The Godot binary** is found by `tools\find-godot.ps1`: an explicit `-Godot`, else
  `$env:WARSHIPS_GODOT`, else `local.config.ps1` at the repo root, else a search of the usual
  places. To pin it and skip the search, create `local.config.ps1` (gitignored — it is about this
  computer, not the game):

      $Godot = 'C:\path\to\Godot_v4.7.2-stable_mono_win64.exe'

The smoke test builds its own network (no router, no internet, or fake routers), so every check
means the same on every machine -- there are no "environmental" failures to discount.

### The individual checks

**On Windows**, every harness has a PowerShell port that needs no WSL. They are the same checks,
validated against the numbers the Linux originals produce:

    powershell -ExecutionPolicy Bypass -File typecheck\typecheck.ps1          # 0 errors.
    dotnet build                                                              # 0 Warning(s), 0 Error(s)
    powershell -ExecutionPolicy Bypass -File tools\analyse\run.ps1            # ANALYSERS: 0 findings
    python tools\analyse\xref.py                                              # UNUSED ANYWHERE: 0
    powershell -ExecutionPolicy Bypass -File tools\smoketest\run.ps1     # every check, 6/6 runs
    powershell -ExecutionPolicy Bypass -File tools\screens\run.ps1
    powershell -ExecutionPolicy Bypass -File tools\snapshot.ps1              # rebuild version\CODE_SNAPSHOT.txt

`-Godot <path>` is optional on both runners; without it they resolve the engine themselves. If you
do pass one, pass the plain `Godot_v4.7.2-stable_mono_win64.exe` -- they swap themselves to the
`_console.exe` beside it, because the GUI binary prints nothing.

**Run them one at a time.** Both copy the project to a single fixed scratch folder, so a second
run deletes the first's files mid-flight; `run.ps1` refuses to start rather than fail obscurely.

The smoke test's bar is **every check passing, 6/6 runs** (the count is in the last VERIFIED commit;
`-Wan` runs the multiplayer half over a simulated internet). The sweep's bar is **SWEEP DONE, 0
LINT**, into `%TEMP%\shots`.

The Windows analyser does *not* need the smoke test run first: NuGet is reachable here, so it
restores and builds in place rather than reusing the smoke test's offline build folder.

**On Linux** (the sandbox the project is developed in), the originals:

    cd typecheck && sh typecheck.sh

It prints its mode on every run. **`REAL GodotSharp.dll` is the only one worth trusting.** If it
says *hand-written stub*, the DLL is missing — ask for it:

    C:\Users\<you>\.nuget\packages\godotsharp\4.7.2\lib\net8.0\GodotSharp.dll

Drop it in `typecheck/`. It is gitignored on purpose: a build dependency, not game content.
(The Windows `typecheck.ps1` fetches it from that cache by itself; only the Linux `typecheck.sh`
still needs it handed over, because a sandbox has no such cache.)

It exits **2** if no .NET 8 SDK is installed (`apt-get install dotnet-sdk-8.0` from the Ubuntu
archive works in a sandbox). Treat anything but "0 errors." as a failure.

**A clean typecheck is not a working game.** It proves the code compiles. It cannot catch a null
node, a scene wired wrong, or an RPC that never arrives. The smoke test catches most of that:

    sh tools/smoketest/run.sh /path/to/Godot_v4.7.2-stable_mono_linux.x86_64

Anything visual also gets **looked at**, since the smoke test cannot see:

    sh tools/screens/run.sh /path/to/Godot_v4.7.2-stable_mono_linux.x86_64

renders real frames into `/tmp/shots/` on a virtual display (needs `xvfb` and Mesa; see the
CHANGES.md Handoff for the packages).

The Linux mono build downloads from GitHub releases (`godotengine/godot`, tag `4.7.2-stable`) and
builds offline from its bundled `GodotSharp/Tools/nupkgs`. It is headless, so how things look still
needs a real launch.

## The loop

The project is edited here and run on the developer's machine. So:

1. Make the change, run the typecheck.
2. Hand back **individual files** for a small change — the developer edits locally, and a whole-zip
   replace can clobber their work. Send the zip only when many files moved.
3. The developer runs it in Godot and pastes any build failure. The **error lines** are enough; the
   300-line MSBuild command dump adds nothing.
4. Record the change in `CHANGES.md` before calling it done.

Godot compiles C# itself: save, switch to Godot, press **F5**. No reimport for `.cs`. New scripts
are picked up automatically (the SDK globs the folder) but must be **built once** before the editor
can see the type. Editing `project.godot` externally needs Project -> Reload Current Project. The
zips ship without `.godot/`, so the first open after extracting does a full asset import.

## Testing multiplayer

Default port **27015**, ENet, peer-to-peer — one peer hosts, the rest join by address. Run two
instances (Godot's Debug -> Run Multiple Instances, or a second exported copy) and join
`127.0.0.1`. Offline is a host with zero peers: the same code path, not a separate mode.

## Working style that has held up

- Verify claims with a command rather than asserting them. Several "obviously fine" things in this
  project were not.
- State what was actually tested versus what was only compiled. Those are different claims.
- When a decision could go two ways and the wrong one is expensive, ask before building.
