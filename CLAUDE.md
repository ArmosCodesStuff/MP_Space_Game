# Warships — working rules for Claude Code

Godot 4.7.2 (.NET / C#) multiplayer space game. The HOST owns the world; each PLAYER owns their
ship. Single player is a host with no peers. The repo is the only source of truth — never rebuild
code from memory.

## Start of every session

1. Read the **Handoff** at the top of `docs/CHANGES.md`, then `docs/DESIGN.md` (the player's decisions
   and open questions), `docs/README.md`, and `docs/REVIEW.md` (the code-review checklist).
2. **Integrity check** before touching anything:
   `git status` · `git log <last commit starting "VERIFIED:">..HEAD` · optionally
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
- **Compare with the spec's numbers as literals**, never with the code's own constants (a check
  that reads the constant cannot catch the constant being wrong).
- Visual change: run the screenshot sweep and look at the frames yourself.
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
- Then commit with a message starting **"VERIFIED:"** and the results — the baseline for the next
  integrity check. (Optional: regenerate `version/CODE_SNAPSHOT.txt` and `version/MANIFEST.sha256`.)

## Commands (the Godot executable is passed in; the scripts are POSIX shell — use WSL on Windows)

- Typecheck: `cd typecheck && sh typecheck.sh` → must print `0 errors.`
  (needs `typecheck/GodotSharp.dll`, not in the repo: copy it from your Godot .NET folder,
  `GodotSharp/Api/Release/GodotSharp.dll`)
- Smoke test: `sh tools/smoketest/run.sh <godot>` → solo, host + 2 guests, then a 2-player arena run.
- Screenshot sweep: `sh tools/screens/run.sh <godot>` → frames in `/tmp/shots`; must end
  `SWEEP DONE` with 0 `LINT` (it uses a virtual display, Xvfb, on Linux).
- Analysers: `sh tools/analyse/run.sh` (after a smoke run) → `ANALYSERS: 0 findings`.
- Cross-reference: `python3 tools/analyse/xref.py` → `UNUSED ANYWHERE: 0`.

## Reply

Short. For each request: what was done and how it was verified; integrity findings and decisions;
anything still unverified; open questions. Never say something is done when it is not.
