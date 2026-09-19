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
starts the session should attach the project zip (~6 MB with history, 35 scripts, ~5700 lines).

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

## Checking your work

    cd typecheck && sh typecheck.sh

It prints its mode on every run. **`REAL GodotSharp.dll` is the only one worth trusting.** If it
says *hand-written stub*, the DLL is missing — ask for it:

    C:\Users\<you>\.nuget\packages\godotsharp\4.7.2\lib\net8.0\GodotSharp.dll

Drop it in `typecheck/`. It is gitignored on purpose: a build dependency, not game content.

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
