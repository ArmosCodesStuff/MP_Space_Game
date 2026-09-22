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
- **BUILD EVERY NEW SYSTEM GLOBALLY, AND SAY WHERE IT LIVES.** Anything new — a class, an enemy,
  an ability, a weapon, a status, an effect, a screen, an upgrade — is built so that the NEXT one
  is a row, not a copy. Five requirements, all of them:
  1. **A table of rows.** The thing's numbers, art and text live in data (`Classes.All`,
     `Enemies.All`, `Ab.*`, `Economy.All`, `Missions`), never in the code that uses them.
  2. **A named public contract.** A small interface or struct anything else can implement or read
     (`ITurretHost`, `ITagged`, `IStatused`, `IRaidTarget`, `TurretSpec`, `StatRow`). If only one
     class can ever use what you wrote, it is not a system yet.
  3. **One file that owns it, named for the thing** — `Ships.cs`, `Enemies.cs`, `Abilities.cs`,
     `Turrets.cs`, `Statuses.cs`, `Targeting.cs`, `Ids.cs`, `Aim.cs` — with a header saying what it
     replaced and what a new row must fill in.
  4. **Reached by id or tag, never by type.** `Tag`, a row index, an ability id, a stat id. A
     `is Raider` / `is Torpedo` / `HitRadius < 20f` in new code is a bug in the design, not a shortcut.
  5. **The generic path is the ONLY path.** Delete the specific one in the same edit. Two ways to
     do a thing is how the next instance picks the wrong one.
- **Extend a table before inventing one.** These already exist — add to them:
  `Classes.All` (a class) · `Ab.*` + `ClassDef.Abilities` (an ability) · `ClassDef.Rows` (a stat
  only one class has) · `Enemies.All` (an enemy) · `Economy.All` (an upgrade) · `Missions` (a boss
  level) · `Equipment` (a part) · `Tag` (a kind of thing) · `Status` (a thing done to something) ·
  `NetIds` (an id space) · `TargetFilter` (who may be shot) · `TurretSpec`/`ITurretHost` (a gun and
  what carries it) · `Aim`/`Motion` (pointing and arriving) · `PlayerShip.Slot` (per-ability state,
  which is also how it reaches the wire) · `PlayerShip.Incoming`/`Guarded` (all damage, and every
  defence against it) · `PlayerShip.NoteDealt` (all damage this ship deals).
- **Before writing the second of anything, make the first one a row.** The second boss, the second
  freighter, the second always-on gun: that is the moment the table is cheap and the copy is not.
- Delete what a change replaces in the same edit. `UNUSED ANYWHERE: 0` is enforced.
- No new tool script unless the same job will recur or it cannot be done inline; extend an existing
  tool first. Scratch files go in the scratchpad, never in the repo.
- Commit a checkpoint per slice. Only a green full run earns `VERIFIED:`.

## 4 · The ladder — always the cheapest rung that can SEE the problem

Every check in this project sits on a rung. **Run the lowest rung that can see the thing you
changed. Never run a higher rung to prove something a lower rung proves.**

| # | Rung | Cost | What it can SEE | What it is BLIND to |
|---|---|---|---|---|
| 0 | Read the code, and the log you already have | free | anything you can reason about | nothing it was not given |
| 1 | `typecheck\typecheck.ps1` | ~20 s | every rename, signature, missing caller — in `scripts/` **and in the harness** (`SmokeTest.cs.txt`, `Shots.cs.txt`) | behaviour, numbers, reflection by string |
| 2 | `verify.ps1 -Quick` | ~1 min | rung 1 + 0 warnings, 0 analyser findings, `UNUSED ANYWHERE: 0` | behaviour, numbers, anything drawn |
| 3 | `tools\smoketest\run.ps1 -Solo` | ~1.5 min | the whole single-player narrative: behaviour, every number, ability state, UI state | a host and a guest disagreeing |
| 4 | `tools\screens\run.ps1` | ~1.5 min | what is DRAWN: 97 frames, `LINT: 0` for off-screen, clipped and overlapping | behaviour |
| 5 | `tools\smoketest\run.ps1` (all six) | ~4 min | authority, replication, the protocol: host + two guests + the two-player arena | nothing the game does; it is the last word on correctness |
| 6 | `verify.ps1 -Update` | ~13 min | **the bar**: rungs 1-5, ×3 runs, + map, snapshot, manifest, integrity | nothing — it is the release gate, not a debugging tool |

**Choosing a rung.** A rename or a signature: 1. A number, a behaviour, an ability, an economy
row: 3. Anything visual: 4. Anything that crosses peers — an RPC, a field on the wire, a
host-decided state a guest must see: 5. Anything else: the lowest rung on this list that names it.

**Escalating.** Go up ONE rung, and only for one of two reasons:

1. **The rung is blind to it.** Authority is invisible below 5; a layout is invisible below 4. Do
   not run a rung that cannot see the problem "to check" — that is a wasted run, and its green
   verdict is worse than nothing.
2. **The same rung has failed to resolve it twice.** Two attempts at a rung without the answer
   means the rung is the wrong instrument, not that you need more of it.

**Coming back down is mandatory.** When rung 5 or 6 fails, read WHICH role and WHICH check failed,
then drop to the lowest rung that covers that check, fix it there, and re-prove it there. A check
that failed in the solo role is re-proved by rung 3 in 1.5 minutes — never by re-running the bar.

**Rung 6 runs ONCE, at the end of a batch**, and its output is a verdict, not a debugging tool. If
it fails: read, drop, fix, re-prove low, then run it once more. Two bars in a row for the same
fix means the ladder was skipped.

**Standing rules on the engine rungs (3-6):**

- Never start an engine rung while a compile rung is red. Green `-Quick` first, always.
- Never run two engine harnesses at once (they share one scratch folder; the second wipes the first).
- Prove numbers with **literals from the request**, never with the code's own constants. Where a
  behaviour check must read a table, prove that table against literals in one place and let the
  behaviour check read the row.
- Mutants only for authority, save-format and removed-path invariants, and only when a check's
  wiring is in doubt. Otherwise seeing a new check pass once is enough.
- A flaky check is a broken check. Fix its geometry at rung 3 until it passes three runs with
  different seeds; never re-run a higher rung hoping for a different draw.

### Varied, not repeated

Checks must not be tied to one placement. Use `Vary`, `VaryAngle` and `VaryNear` (SmokeTest) for
positions, angles and distances: they draw from a per-run seed the run prints as `SEED n`, so each
run covers different geometry and any failure comes back with `run.ps1 -Seed <n>`, which puts the
same numbers back. Never vary a figure the check ASSERTS — only where a thing is and which way it
faces. A check that only holds at one spot is a check that hides a bug.

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
tools\smoketest\run.ps1 [-Solo|-Wan|-Seed <n>]
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
