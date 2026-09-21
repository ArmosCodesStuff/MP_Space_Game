# Warships — working rules for Claude Code

Godot 4.7.2 (.NET / C#) multiplayer space game. The HOST owns the world; each PLAYER owns their
ship. Single player is a host with no peers. The repo is the only source of truth — never rebuild
code from memory.

**This file assumes you have no memory of this project.** It is written for a fresh instance that
has just been handed the folder. Follow it in order and you will be where the last session left off.

---

## 1 · Start of every session

1. **Check you are in the right folder.** The git root is the folder containing `verify.ps1`,
   `scripts/` and `.git`. A re-extracted zip nests it one level down (`<wrapper>/Warships/`);
   opening the wrapper gives a session with no git and none of this. `git log --oneline -1` must
   print a commit.
2. Read the **Handoff** at the top of `docs/CHANGES.md`, then `docs/DESIGN.md` (decisions, standing
   rules, and the traps that have already cost time), `docs/README.md` and `docs/REVIEW.md`.
3. **Integrity check before touching anything:**
   `git status` · `git log <last commit starting "VERIFIED:">..HEAD` ·
   `powershell -ExecutionPolicy Bypass -File verify.ps1 -Quick`
   Anything you did not report yourself is an UNVERIFIED FINDING: read the diff and decide ADOPT
   (serves a current request; review and test it), REMOVE (contradicts the direction, duplicates,
   or broken; revert with git and say why) or ASK. **Report every finding.**

---

## 2 · Plan — the whole batch, before any of it

The player usually asks for several things at once. Plan them **together**, then build them
**in the order given**, unless one depends on another — say so when reordering.

For each request, before writing anything: what will be observably true; how it will be tested;
which systems it touches (search the code for every use); what it could break (multiplayer
authority, UI, balance, saves).

**FOUNDATIONS FIRST.** If a request needs something that does not exist yet — a field, a hook, a
public accessor, a shared helper, a new node type, a save-file field — **build that first, as its
own step, before the feature that uses it.** Do not bolt a feature onto a foundation that is not
ready and repair it afterwards. This is a rule about ORDER, not about scope: the foundation is part
of the request, not a separate one to ask about.

Ask all clarifying questions **in one batch**, each with the default you will use if unanswered.
Never ask what the docs already answer.

---

## 3 · Build — every request in the batch, then the bar once

The old rule was "one request, then the full bar". That ran ~12 minutes of engine checks for a
one-line change and ran them again for the next one. Now:

**While building each request:**

- Implement it, including its foundations.
- **Write the checks for it as you go**, in `tools/smoketest/SmokeTest.cs.txt`.
- **Prove each new check can fail**: put a mutant in a scratch copy and run it. One mutant at a
  time when they could interact. A mutant must reproduce the **real old code**, not an
  approximation. Build the scratch copy from `git ls-files` and copy `typecheck/GodotSharp.dll`
  into it; a sound copy reproduces the baseline pass count exactly, so any other number means the
  copy is wrong, not the code.
- **Prove the check is not vacuous**: set up the situation so the OLD behaviour would visibly
  differ. A boss that would not have moved anyway cannot demonstrate a lock; a hull already
  pointing the right way cannot demonstrate tracking. This has caught a blind check three times.
- **Compare with the spec's numbers as literals**, never with the code's own constants — a check
  that reads the constant cannot catch the constant being wrong.
- **Run only what can see the change** (the three gears are in §6). A rename: `-Quick`. New
  behaviour: `-Fast`. Something visual: the sweep, and **look at the frames yourself** with the
  Read tool. *A check on behaviour is not a check on being drawn.*
- Commit a checkpoint per request, so a later failure has somewhere to go back to. A checkpoint
  message does **not** start with `VERIFIED:`.

**When every request in the batch is done**, run the bar once — §5.

---

## 4 · The four standing invariants

Every change is checked against these, and each one has checks behind it.

**A · Lifetime.** Everything instantiated, duplicated or subscribed is released at the end of its
existence. Nodes freed; handlers on autoloads and static events removed in `_ExitTree`; timers and
tweens stopped; `Combat`'s hooks dropped by whatever world set them (each is a lambda holding that
world). The smoke test asserts orphan nodes and object count do not grow across leaving and
re-entering a world. *A C# `Resource` handle alive at shutdown is reported as `resources still in
use at exit` — see `Music._ExitTree`, and `Known broken` in `docs/CHANGES.md`.*

**B · Authority.** All world and combat state changes only under `Net.Sim` / `Net.IsHost`. Guests
request by RPC and the host checks the sender. Every visible state reaches guests. Home-only nodes
send via `Hub.RpcHome`. The smoke test runs a host with two guests and a two-player arena precisely
to catch this; a solo run cannot.

**C · Replacement, not accumulation.** When a mechanic changes, the previous code is **removed**,
not left beside the new path. Search for every caller of what you changed and update them all; a
field that now means something else is renamed so an unconverted caller fails to compile. No
comment may describe old behaviour. `verify.ps1` runs a cross-reference that must print
`UNUSED ANYWHERE: 0` — that catches the dead member, but only **you** catch the old path still
running.

**D · Correctness.** 0 compiler warnings, 0 analyser findings, 0 unused members.

---

## 5 · Finish the batch: verify, record, commit

```
powershell -ExecutionPolicy Bypass -File verify.ps1 -Update
```

`-Update` regenerates `version\MAP.md`, then `version\CODE_SNAPSHOT.txt`, then
`version\MANIFEST.sha256` **before** the integrity step, in that order, so integrity checks this
change rather than the last one. It must print `ALL CHECKS PASSED`.

Then record — in the same change as the code, never after:

- `docs/CHANGES.md` — the Handoff, plus every change under Unreleased. `Known broken` is mandatory
  and honest.
- `docs/DESIGN.md` — durable reasoning, and any new trap.
- `docs/README.md` — sizes and commands.
- `docs/REVIEW.md` — review progress.

Then commit with a message starting **`VERIFIED:`** carrying the verdict. That commit is the
baseline for the next session's integrity check. **Only a green `verify.ps1` earns the word.**

---

## 6 · Commands

Everything is PowerShell and needs nothing passed in: the Godot binary is found by
`tools\find-godot.ps1`, and the gitignored `typecheck/GodotSharp.dll` is fetched from the NuGet
cache when missing.

**The three gears — use the smallest one that can see your change:**

```bash
powershell -ExecutionPolicy Bypass -File verify.ps1 -Quick    # ~1 min   typecheck, build, analysers, xref
powershell -ExecutionPolicy Bypass -File verify.ps1 -Fast     # ~3 min   + ONE solo smoke run + the sweep
powershell -ExecutionPolicy Bypass -File verify.ps1 -Update   # ~12 min  + three FULL smoke runs, snapshot, manifest
```

`-Fast` runs one engine instead of six. It covers only the solo scenario's checks — but **it cannot see a host
and a guest disagreeing**, which is what this harness exists for, so it is never the last word.
Its verdict says `SMOKE TEST (SOLO ONLY)` so it can never be mistaken for the bar.

The individual steps, when you want one on its own:

```bash
powershell -ExecutionPolicy Bypass -File typecheck\typecheck.ps1        # 0 errors.  (and "REAL GodotSharp.dll")
dotnet build                                                            # 0 Warning(s), 0 Error(s)
powershell -ExecutionPolicy Bypass -File tools\analyse\run.ps1          # ANALYSERS: 0 findings
python tools\analyse\xref.py                                            # UNUSED ANYWHERE: 0
powershell -ExecutionPolicy Bypass -File tools\smoketest\run.ps1        # the full six-process run
powershell -ExecutionPolicy Bypass -File tools\smoketest\run.ps1 -Solo  # just the solo scenario
powershell -ExecutionPolicy Bypass -File tools\smoketest\run.ps1 -Wan   # multiplayer over a simulated internet
python tools\map.py                                                     # rebuild version\MAP.md
powershell -ExecutionPolicy Bypass -File tools\screens\run.ps1          # SWEEP DONE, LINT: 0
powershell -ExecutionPolicy Bypass -File tools\snapshot.ps1             # rebuild version\CODE_SNAPSHOT.txt
powershell -ExecutionPolicy Bypass -File tools\manifest.ps1             # rebuild version\MANIFEST.sha256
```

Sweep frames land in `%TEMP%\shots`.

**Run one engine harness at a time.** The smoke and sweep runners each own a scratch folder under
`%TEMP%`; a second run deletes the first's files and the victim prints a wall of
`Cannot open file 'res://scripts/...'` that reads like a broken project. The runners refuse rather
than failing that way — including against a stray Godot you left in the background.

**The smoke test builds its own network** (`NoRouterNoInternet`, fake routers in
`tools\smoketest\fakeigd.py`): every check means the same on every machine, and a test run never
touches the real router. A network change gets a scenario there, and a multiplayer change is worth a
`-Wan` run (90 ms each way, jitter, 2% loss) as well as the plain one.

**Version and integrity control:** `version/MAP.md` is the map -- every script, member, RPC, spawn
site, event hookup and asset, and who uses each; `version/CODE_SNAPSHOT.txt` is every code file
inline, for pasting into a chat without the repo; `version/MANIFEST.sha256` is one SHA-256 per
tracked file, checked with `sha256sum -c`, for verifying an unzipped copy. All three are
regenerated by `-Update`, in that order, **after the last edit**.

---

## 7 · Reply

Short. For each request: what was done and how it was verified; integrity findings and decisions;
anything still unverified; open questions. **Never say something is done when it is not**, and
never call something verified on anything less than a green `verify.ps1`.
