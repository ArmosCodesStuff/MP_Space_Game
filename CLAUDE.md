# Warships — working rules for Claude Code

Godot 4.7.2 (.NET / C#) multiplayer space game. The HOST owns the world; each PLAYER owns their
ship. Single player is a host with no peers. The repo is the only source of truth — never rebuild
code from memory.

**This file assumes you have no memory of this project.** Everything below is written for a fresh
instance that has just been handed the folder. Follow it in order and you will be where the last
session left off.

## Start of every session

1. **Check you are in the right folder.** The git root is the folder containing `verify.ps1`,
   `scripts/` and `.git`. A re-extracted zip nests it one level down (`<wrapper>/Warships/`);
   opening the wrapper gives you a session with no git and none of this. `git log --oneline -1`
   must print a commit.
2. Read the **Handoff** at the top of `docs/CHANGES.md`, then `docs/DESIGN.md` (the player's
   decisions, the standing rules and the traps that have already cost time), `docs/README.md`
   (how to run everything) and `docs/REVIEW.md` (the code-review ledger).
3. **Integrity check before touching anything:**
   `git status` · `git log <last commit starting "VERIFIED:">..HEAD` ·
   `sha256sum -c version/MANIFEST.sha256`.
   Anything you did not report yourself is an UNVERIFIED FINDING: read the diff and decide
   ADOPT (serves a current request; review and test it), REMOVE (contradicts the direction,
   duplicates, or broken; revert with git and say why) or ASK. Report every finding.

## Plan

- Keep the player's requests in the order given, unless one depends on another (say so).
- For each: what will be observably true, how it will be tested, which systems it touches
  (search the code for every use), what it could break (multiplayer authority, UI, balance, saves).
- Ask all clarifying questions together, each with the default you will use. Never ask what the
  docs already answer.

## Build — one request at a time

- Implement it. Typecheck. Run the smoke test. Add checks for the new behaviour.
- **Prove each new check can fail**: put a mutant in a scratch copy and run it. One mutant at a
  time when they could interact. A mutant must reproduce the real old code, not an approximation.
  Build the scratch copy from `git ls-files` and copy `typecheck/GodotSharp.dll` into it; a sound
  copy reproduces the baseline pass count exactly, so any other number means the copy is wrong,
  not the code.
- **Prove the check is not vacuous**: set up the situation so the OLD behaviour would visibly
  differ. A boss that would not have moved anyway cannot demonstrate a lock; a hull already
  pointing the right way cannot demonstrate tracking. This has caught a blind check twice.
- **Compare with the spec's numbers as literals**, never with the code's own constants (a check
  that reads the constant cannot catch the constant being wrong).
- Visual change: run the screenshot sweep and **look at the frames yourself** with the Read tool.
- Commit a checkpoint per request. If something breaks, revert to the last checkpoint.

## Audit, clean-up, verify, record

- Leaks (nodes freed; handlers on autoloads/static events removed in `_ExitTree`; timers/tweens
  stop), assets (nothing missing; unused art in `art_unused/`), multiplayer (all world and combat
  state changed only under `Net.Sim` / `Net.IsHost`; guests request by RPC and the host checks the
  sender; every visible state reaches guests; home-only nodes send via `Hub.RpcHome`), correctness
  (0 compiler warnings, 0 analyser findings, 0 unused members; no comment describes old behaviour).
- Verify: typecheck; smoke test **three runs in a row**; sweep complete with 0 UI-lint. Only claim
  "tested" or "looked at" with a log or frame newer than the last code change.
- Record: `docs/CHANGES.md` (Handoff + every change under Unreleased), `docs/DESIGN.md` (durable
  reasoning), `docs/README.md` (sizes), `docs/REVIEW.md` (review progress).
- Regenerate `version/CODE_SNAPSHOT.txt` (`tools\snapshot.ps1`) and then `version/MANIFEST.sha256`,
  **in that order and after the last edit** — a manifest generated before a doc edit fails its own
  integrity check next session.
- Then commit with a message starting **"VERIFIED:"** and the results — the baseline for the next
  integrity check.

## Commands

**Windows (this developer's machine) — one command runs the whole bar:**

```
powershell -ExecutionPolicy Bypass -File verify.ps1
```

typecheck → build → analysers → cross-reference → smoke test ×3 → screenshot sweep → integrity,
then one verdict. It must print `ALL CHECKS PASSED`. `-Quick` stops after the static checks
(about a minute). Nothing has to be passed in: it finds the Godot binary itself and fetches the
gitignored `typecheck/GodotSharp.dll` from the NuGet cache.

The individual steps, if you need one on its own:

```
powershell -ExecutionPolicy Bypass -File typecheck\typecheck.ps1     # 0 errors.  (and "REAL GodotSharp.dll")
dotnet build                                                         # 0 Warning(s), 0 Error(s)
powershell -ExecutionPolicy Bypass -File tools\analyse\run.ps1       # ANALYSERS: 0 findings
python tools\analyse\xref.py                                         # UNUSED ANYWHERE: 0
powershell -ExecutionPolicy Bypass -File tools\smoketest\run.ps1     # 435 pass, 6/6 runs
powershell -ExecutionPolicy Bypass -File tools\screens\run.ps1       # SWEEP DONE, LINT: 0
powershell -ExecutionPolicy Bypass -File tools\snapshot.ps1          # rebuild version\CODE_SNAPSHOT.txt
```

Sweep frames land in `%TEMP%\shots`. **Run one smoke test at a time** — both runners share
`%TEMP%\warships_smoke`, and a second run deletes the first's files, which reads as a broken
project rather than as a collision.

Two smoke checks assert a sandbox with no router and no internet and **cannot pass on a real
machine**; `verify.ps1` reports them separately, so 435 real passes and 2 environmental failures
is a clean run. Do not "fix" them.

**Linux / WSL** (the POSIX originals, same checks): `sh typecheck/typecheck.sh`,
`sh tools/smoketest/run.sh <godot>`, `sh tools/screens/run.sh <godot>`, `sh tools/analyse/run.sh`,
`python3 tools/analyse/xref.py`. These need the Godot binary passed in.

## Reply

Short. For each request: what was done and how it was verified; integrity findings and decisions;
anything still unverified; open questions. Never say something is done when it is not.
