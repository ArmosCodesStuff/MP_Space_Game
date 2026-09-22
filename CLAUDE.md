# Warships — how to work on this project

Godot 4.7.2 (.NET / C#). Host owns the world; each player owns their ship; single player is a host
with no peers. The repo is the only source of truth. Written for an instance with no memory of it.

**Accuracy outranks speed. Speed outranks everything else.**

---

## 1 · Session start (≤ 2 tool calls)

```
git -C <repo> log --oneline -3 ; git -C <repo> status --short
```
Read `docs/CHANGES.md` Handoff only. Read `DESIGN.md`/`REVIEW.md` sections only when the task touches
them (grep, do not read whole files). Do not run verify at session start.
Anything in the tree you did not write: report it in one line, then ADOPT or REVERT.

## 2 · Plan the whole batch before writing code

1. List the player's requests **in their order**.
2. For each: the foundation it needs (field, hook, accessor, node type, save field, data table).
   **Build every foundation before the feature that uses it**, even if that reorders the work.
3. One clarifying question batch, each with the default you will use anyway. Then build.
4. Never ask what the repo answers.

## 3 · Build

- Batch edits. No engine run during development, ever. Compile checks (`typecheck.ps1`,
  `dotnet build`) are free — use them; they are not tests.
- **The harness is source.** `tools/smoketest/SmokeTest.cs.txt` and `tools/screens/Shots.cs.txt`
  are compiled INTO the game by their runners. A rename or a deletion makes them callers you must
  fix in the same edit. `typecheck.ps1` compiles them with `scripts/`, so it costs a minute;
  before it did, it cost an engine run three minutes in, after a full copy and import.
- **Never start an engine run while a compile check is red.** Green `-Quick` first, always.
- Write the smoke checks for a feature as you write the feature, in the same edit pass.
- **Generalise, never special-case.** A new boss, class, enemy, ability or upgrade must be a row of
  data plus parameters, not a new `if`. If a request forces a special case, the system is wrong:
  fix the system.
- Delete what a change replaces in the same edit. `UNUSED ANYWHERE: 0` is enforced.
- No new tool script unless the same job will recur or it cannot be done inline; extend an existing
  tool first. Scratch files go in the scratchpad, never in the repo.
- Commit a checkpoint per slice. Only a green full run earns `VERIFIED:`.

## 4 · Test once, at the end

- Development, then planning-complete, then code, then **one** run. If a run fails, fix and re-run
  only what the fix can affect (§5), not the whole bar.
- Never run two engine harnesses at once.
- Prove numbers with **literals from the request**, never with the code's own constants.
- Mutants only for authority, save-format and removed-path invariants, and only when a check's
  wiring is in doubt. Otherwise seeing a new check pass once is enough.

### Selective verification

```
verify.ps1 -Quick                 # typecheck, build, analysers, xref          ~1 min
verify.ps1 -Scope <tags>          # the above + only the smoke scenarios those tags cover
verify.ps1 -Update                # everything + map, snapshot, manifest        (release bar)
```
`-Scope` takes the smoke test's section tags (`ship`, `wing`, `boss`, `raid`, `econ`, `net`, `ui`,
`art`). Run the tags your diff touches. `-Update` is for the end of a batch or a release only.

### Varied, not repeated

Checks must not be tied to one placement. Use `Vary` (SmokeTest) for positions, angles, distances
and party sizes: it draws from a per-run seed printed in the log, so a run covers a different
geometry each time while any failure is reproducible with `-Seed <n>`. A check that only holds at one
spot is a check that hides a bug.

## 5 · Standing invariants

- **A · Lifetime.** Everything created, subscribed or hooked is released in `_ExitTree`. Orphan and
  object counts must not grow across leaving and re-entering a world.
- **B · Authority.** World and combat state changes only under `Net.Sim` / `Net.IsHost`. Guests
  request by RPC; the host checks the sender. Every visible state reaches guests.
- **C · Replacement.** The old path is deleted, not left beside the new one. No comment describes
  old behaviour.
- **D · Correctness.** 0 warnings, 0 analyser findings, 0 unused members.

## 6 · Replies to the player

- Default: **≤ 4 lines.** One line per request: what is true now.
- State failures and unverified work plainly. Never call something verified without a green run.
- No narration of what you are about to do, no restating their request, no progress commentary
  unless a run is minutes long (then one line).
- Numbers and file paths, not adjectives.

## 7 · Commands

```
typecheck\typecheck.ps1 · dotnet build · tools\analyse\run.ps1 · python tools\analyse\xref.py
tools\smoketest\run.ps1 [-Solo|-Wan|-Scope <tags>|-Seed <n>]
tools\screens\run.ps1            # sweep; trust LINT: 0 for layout, read a frame only for new art
tools\make_ships.ps1             # ship sprites from art_source\
tools\finish_ships.ps1           # shade, upscale, detail a sprite
python tools\make_sounds.py      # boss/ability sounds
python tools\map.py · tools\snapshot.ps1 · tools\manifest.ps1
```
Godot is found by `tools\find-godot.ps1`. `-Update` regenerates map, snapshot and manifest in that
order, then checks integrity. **Integrity is asked only in `-Update`**: anywhere else the
manifest is still the last change's, so it could only ever say FAILED — and a red line in the
verdict of every mid-session run is how a real failure gets waved through.

## 8 · Record (same commit as the code)

`docs/CHANGES.md` Handoff + Unreleased entry + honest `Known broken`; `DESIGN.md` for durable
reasoning and new traps; `README.md` for sizes and commands; `REVIEW.md` for review passes.
Keep each entry to what is true now — delete what a change reversed.
