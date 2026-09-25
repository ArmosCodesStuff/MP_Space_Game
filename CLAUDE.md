# Warships — how to work on this project

Godot 4.7.2 (.NET / C#). The host owns the world; each player owns their ship; single player is a host
with no peers. The repo is the only source of truth. Owner rulings: `docs/plans/README.md` (binding,
newest wins). The process per role: `docs/process/coordinator.md` (Fable: plans, picks models, launches
workflows), `writer.md` (builds a lane), `gate.md` (reviews a lane), `tester.md` (engine runs, triage,
release). Read your role's file first, then only what the task touches.

## The two phases (owner, 2026-09-25)

BUILD: every planned lane is written, gated and merged with NO engine run anywhere (a build-phase script
that starts the engine is a bug); per job `typecheck\typecheck.ps1` + `verify.ps1 -Quick` and a read of
your own diff. TEST: only once everything planned is merged, the engine chains run through
`tools\rungs.ps1`, several at once, reds fixed at the lowest rung, then the bar once, a `VERIFIED:`
commit, push BOTH `version-l` and `main`, release (standing OK). Never call multiplayer "working": real
two-machine play has never worked; only the owner and a friend playing can say so.

An owner message that reaches an agent mid-run is for the coordinator: never stop or wait for it.

## Read slices, write little

Grep, then an offset. Never read whole: `SmokeTest.cs.txt`, `Hub.cs`, `PlayerShip.cs`, `CHANGES.md`,
`DESIGN.md`; `version/MAP.md` answers "what exists". Never paste a log: grep the verdict and the failing
lines. Edit with the Edit tool or a python file in the scratchpad (never C# in a shell heredoc); LF; commit
messages from a BOM-free file; scratch under `<scratchpad>\<lane>\`. Replies: 2-4 lines of facts (done or
not, with the rung; a problem; scope; a number; a question with its default); "verified" only after a
green run; never narrate or praise.

## Every change carries its check (same commit)

The harness is source: `tools/smoketest/SmokeTest.cs.txt` and `tools/screens/Shots.cs.txt` compile into
the game and `typecheck.ps1` sees them. Name the checks before the first line of a change:
1. **New** thing: a check that fails without it, asserting a literal from the request, from 3 varied situations.
2. **Interaction**: its own check per pair; "each works alone" is not a check.
3. **Changed** number or rule: rewrite every check asserting the old truth. Never delete or loosen a check.
4. **Fixed bug**: a check that fails on the old code first.
5. **Visible**: a named frame in `Shots.cs.txt`.
6. **Wire** (RPC, field, host-decided state a guest sees): a check in a guest role.
7. **Class ability or drive row** (owner): at least THREE distinct checks (the effect's literals, each
   named interaction, a guest-role check or a frame); the test phase audits every row for three.

The commit message ends `Checks:` naming each. Positions and angles come from `Vary`/`VaryAngle`/`VaryNear`
(a per-run `SEED n`; never vary a figure the check asserts). A flake is a bug in the check; rule out the
three traps before committing: **knife edge** (nothing exactly on a boundary), **stale pick** (re-take
anything chosen before a wait), **timed from the input** (a clock starts on the first frame the effect
shows, never on the key press).

## Systems: the next one is a row

A new boss, class, enemy, ability, item or upgrade is a row plus parameters; a forced special case means
the system is wrong. Reach by id or tag, never by type (`is Raider`, `HitRadius < 20f` are design bugs).
Extend before inventing: `Classes.All` · `Ab.*` + `ClassDef.Abilities` · `ClassDef.Rows` · `Enemies.All` ·
`Economy.All` · `Unlocks.All` · `Missions` · `Equipment` · `Tag` · `Status` · `NetIds` · `TargetFilter` ·
`TurretSpec`/`ITurretHost` · `Aim`/`Motion` · `PlayerShip.Slot` · `Incoming`/`Guarded` · `NoteDealt`.
Name a thing for its mechanism. Delete what a change replaces in the same edit (`UNUSED ANYWHERE: 0`).
New wire ids, enum values and row indexes go at the END of their table, inside the range the plan gave
the lane. Constant tables are arrays or row tables (the net fingerprint hashes them), never Lists or
Dictionaries.

## Invariants

- **A Lifetime**: everything created, subscribed or hooked is released in `_ExitTree`; orphan and object
  counts do not grow across leaving and re-entering a world.
- **B Authority**: world and combat state change only under `Net.Sim` / `Net.IsHost`; guests request by
  RPC and the host checks the sender; every visible state reaches guests.
- **C Replacement**: the old path is deleted; no comment describes old behaviour.
- **D Correctness**: 0 warnings, 0 analyser findings, 0 unused members.

## The ladder: the cheapest rung that can SEE it

| # | rung | cost | sees |
|---|---|---|---|
| 1 | `typecheck\typecheck.ps1` | 20 s | renames, signatures, callers, the harness |
| 2 | `verify.ps1 -Quick` (`quick`) | 1 min | + warnings, findings, unused, control chars, check-with-change |
| 3 | `solo` | 2 min | single-player behaviour, numbers, UI state |
| 4 | `screens` | 3 min | what is drawn (~110 frames, `LINT: 0`) |
| 5 | `six` | 4 min | authority, replication, protocol (six roles, loopback) |
| 6 | `bar` (`verify.ps1 -Update`) | 13 min | 1-5 x3, map, snapshot, manifest, integrity: a gate, not a debugger |

Every engine run: `tools\rungs.ps1 -Tree <checkout> -Tag <new tag> -Steps <chain>` (steps `quick`, `solo`,
`solo@<n>` replaying a seed, `six`, `screens`, `bar`, `wan`); it takes a free engine slot (at most 4), stops
at the first red and writes `%TEMP%\warships_rungs\<tag>\summary.txt` plus one log per step. Escalate one
rung only when the lower one is blind to it; always come back down to fix and re-prove. Two bars for one
fix means the ladder was skipped.

## Models (the coordinator assigns; `model` and `effort` on every call)

Fable 5.1 coordinates only. Opus 5.5 builds a feature with its checks (medium; high on the one escalation),
gates, triages and diagnoses (high, ONE agent). Sonnet 5 / Haiku 4.5 run a recipe (a merge, a chain and its
`summary.txt`, exact text a gate gave, a push, a pack) under the 30-minute watchdog `tools\agents.ps1 -Loop`.
The table: `docs/process/coordinator.md`.

## Commands

```
typecheck\typecheck.ps1 · verify.ps1 -Quick · tools\analyse\run.ps1 · python tools\analyse\xref.py
tools\lanes.ps1 (state in one call) · tools\agents.ps1 [-Loop] (watchdog) · tools\rungs.ps1 (every engine run)
tools\make_ships.ps1 · python tools\make_sounds.py · python tools\map.py · tools\snapshot.ps1 · tools\manifest.ps1
```
Godot is found by `tools\find-godot.ps1`. Integrity is asked only by `verify.ps1 -Update`.
