# Warships — how to work on this project

Godot 4.7.2 (.NET / C#). Host owns the world; each player owns their ship; single player is a host with
no peers. The repo is the only source of truth. Written for an instance with no memory of it.

**Build everything, THEN test (owner, 2026-09-25). Two phases, never mixed.** BUILD: every planned lane is
written and merged with NO engine run anywhere (not per job, not per batch, not per lane): per job
`typecheck` + `verify -Quick` and a read of its own diff; per lane one opus code gate, then the merge. Every
change still carries its check (section 6), written now and run later. TEST: only once everything planned
is merged, the engine chains run (section 8), several at once, reds fixed at the lowest rung, then the bar.
A build-phase script that starts the engine is a bug. Save tokens wherever it costs no speed. This file is loaded into every agent and every turn: keep it short,
never paste it into a prompt.

## 1 · Roles

- **Coordinator** = the main conversation. It plans, launches batches, reads verdicts, merges, and talks
  to the owner. It does not build or debug; it holds ledger paths and verdicts, nothing more.
- **Lane** = one branch `wt/<lane>` in its own worktree BESIDE the project folder
  (`..\WarShips_wt_<lane>`; a worktree inside it is copied by every runner). One writer per worktree.
  Lanes whose files do not overlap run at the same time.
- **Batch** = a lane's next jobs, picked so they share files (read once, not once per job), launched as
  ONE workflow holding the whole chain: step 0 `git merge version-l` into the lane; each job (ledger PRE,
  edit with its checks, `quick`, POST, commit); after the LAST job, `quick` at HEAD and NO engine (section
  8); a fresh agent from the ledger when one stops; one escalation on a red; on the lane's final batch,
  its code gate and its merge. The coordinator wakes once per batch, on "done" or "stuck", so the rules for a red live in the workflow script, not in a later turn.

## 2 · Session start (≤ 2 tool calls)

Automatic: the SessionStart hook runs `tools\lanes.ps1` (every worktree's head and ledger, the newest
engine chains, and `docs/plans/ledger_main.md`). Then the `docs/CHANGES.md` Handoff only. `docs/DESIGN.md`, `docs/REVIEW.md`, `docs/plans/`: grep the section a task
touches. No verify at session start.
Anything in the tree you did not write: one line to the owner, then ADOPT or REVERT.

## 3 · Plan the batch before any code

1. List the owner's requests in their order. 2. For each, the foundation it needs (field, hook, node
type, save field, table row); **every foundation before the feature that uses it**. 3. ONE question
batch, each question with the default you will build anyway; then build. 4. Never ask what the repo
answers. Owner rulings go to `docs/plans/README.md` (rulings, newest wins) the moment they are given.

**Concurrency.** Steps are sequential only when one needs the other's OUTPUT (a foundation, an edit before
its check); otherwise they start together. Independent tool calls go in one message. Lanes run in
parallel; jobs inside a lane run in order. Take results as they land; stop stragglers.

## 4 · Agents and tokens

The cost is the context re-read on every turn and by every agent, not the output.

1. **Every agent runs Opus 5.5 (`model: 'opus'`), `effort` set on every call** (owner, 2026-09-25, "just to
   be safe"; it replaces "lowest tier"). Effort: low for git-only steps (a merge, a grep), medium for builds
   and sweeps, high for a merge gate, a diagnosis or a second attempt after a red (the one escalation).
2. **Prompts** give paths, the job, the chain, the return schema, and only the rules that bite here, plus one
   line: an owner message that reaches the agent mid-run is for the coordinator; never stop or wait for it.
   **Returns** are a verdict and a pointer: `status` (done / stopped_context / red / blocked), `head`,
   ≤10 lines of summary, `open`. Detail goes to the ledger.
3. **Fan-outs.** Every lane gets ONE merge-gate reviewer (opus, read-only), once. A fan-out review (≤ 5
   finders, each finding verified by ≤ 3 skeptics) only for authority, wire or save-format lanes. Any
   fan-out (review, design, audit, research) is ≤ 5 agents, each on an angle you can name that no other
   one sees; duplicates only cost tokens. No critic stage for a mechanical plan.
4. **Never paste a log.** Redirect it, grep the verdict and the failing lines. **Read slices**: grep, then
   an offset. `SmokeTest.cs.txt`, `Hub.cs`, `PlayerShip.cs`, `CHANGES.md`, `DESIGN.md` are never read
   whole; `version/MAP.md` answers "what exists".
5. **Wake-ups.** Every finished background job is a full coordinator turn. Chain work so each batch wakes
   it once; take a verdict in silence when nothing is actionable; report when the owner must see or decide.
6. **Compaction-safe at every moment** (owner, 2026-09-25: keep the conversation small).
   `docs/plans/ledger_main.md` is the coordinator's memory, under 40 lines, in four sections: **Running**
   (each batch: workflow run id + task id, what it does, where its result lands, what to do when it
   lands), **Next**, **Owner questions** (each with its default), **Notes** (merge risks, promised
   follow-ups). Update it on disk at EVERY event (a launch, a verdict, a ruling, a merge, a question)
   as the first call after it, BEFORE anything else, and delete what is done; commit it at once. So the
   owner may compact at ANY moment and nothing breaks: running batches are untouched by a compact, and
   the SessionStart hook (`.claude/settings.json`) re-injects `tools\lanes.ps1`'s output, ledger_main.md
   included, trusted over the summary; nothing else is re-read until a task needs it. Claude never asks
   for a compact (owner, 2026-09-25).
7. **Agent context.** An agent stops at a job boundary with its ledger current past ~150k; a fresh agent
   reads the ledger, never the old transcript. Resume the same agent only while it is under ~150k.

## 5 · Ledgers

Every writer keeps `docs/plans/ledger_<lane>.md` in its lane; it is the record, the reply only points at it.
- **PRE**, before touching anything: job id, tier, intent, files, `git rev-parse HEAD`, `git hash-object` of
  each file. **POST**: verdict, files, checkpoint commit, next. Commit after every job.
- A PRE with no POST is an interrupted job: compare the hashes, revert its half-made edits (keep only what
  matches the plan exactly), redo it first. Edits are exact-match, so a redo never doubles one.
- **Rulings mid-batch**: running workflow agents cannot be messaged. The coordinator appends a numbered
  `## COORDINATOR NOTE n` to the end of the LANE's copy (`..\WarShips_wt_<lane>\docs\plans\ledger_<lane>.md`,
  never version-l's); a writer re-reads its ledger's tail before every PRE, and the PRE that acts on a
  note says "applies NOTE n".

## 6 · Build — every change carries its check

Edit in batches. Compile checks (`typecheck.ps1`, `verify.ps1 -Quick`) run after every job; they are cheap
and are not tests. The engine never runs in the build phase: not to explore, not to prove a job, not at a
batch's or a lane's end. It runs only in the test phase, never while a compile check is red.

**The harness is source.** `tools/smoketest/SmokeTest.cs.txt` and `tools/screens/Shots.cs.txt` compile
INTO the game; a rename or deletion makes them callers to fix in the same edit (`typecheck.ps1` sees them).

Before the first line of a change, name its checks; they land in the same commit:
1. **New** (mechanic, ability, row, weapon, enemy, status, screen, rule): a NEW check that fails without
   it, asserting a literal from the request, from 3 varied situations (range, angle, front/behind,
   moving/still).
2. **Interaction** (two things meeting): its own check per pair. "Each works alone" is not a check.
3. **Changed** (a number, rule, behaviour): grep the harness for every check asserting the old truth
   (literal, row id, message) and rewrite it. Never delete or loosen a check; an obsolete one is replaced
   in the same edit.
4. **Fixed bug**: a check that fails on the old code first.
5. **Visible**: a named frame in `Shots.cs.txt`, read by eye once.
6. **Wire** (RPC, field, host-decided state a guest sees): a check in a guest role (rung 5).

The commit message ends with a `Checks:` line naming every check added or rewritten. `verify.ps1` refuses a
`scripts/` change with no harness change since the last `VERIFIED:` commit.

**A check is code; a flake is a bug in it.** Rule out the three traps before committing one:
- **Knife edge**: nothing exactly ON a boundary (dead astern, a range limit, an accumulated clock); vary
  around the edge.
- **Stale pick**: anything chosen before a wait (nearest foe, a position, a heading) is re-taken after it.
- **Timed from the input**: start a clock on the first frame the effect shows, never on the key press.

Positions and angles come from `Vary`/`VaryAngle`/`VaryNear` (a per-run `SEED n`; the steps `solo@<n>` and
`six@<n>` replay it). Never vary a figure the check asserts. A new or rewritten check passes on **two seeds**
at its rung in the test phase (its chain runs it twice); a flaky one is fixed at rung 3 until
three seeds pass. Never re-run a higher
rung hoping for a different draw. Prove numbers with literals from the request; where a check must read a
table, prove the table against literals in one place. Mutants only for authority, save-format and
removed-path invariants, and only when a check's wiring is in doubt.

## 7 · Systems — the next one is a row

- **Generalise, never special-case.** A new boss, class, enemy, ability or upgrade is a row plus
  parameters. A forced special case means the system is wrong: fix the system.
- **Every new system**: a table of rows (numbers, art, text in data); a named public contract (`ITurretHost`,
  `ITagged`, `IStatused`, `IRaidTarget`, `TurretSpec`, `StatRow`); one file named for the thing, with a
  header saying what it replaced and what a row must fill in; reached by id or tag, never by type (`is
  Raider`, `HitRadius < 20f` are design bugs); the generic path is the ONLY path.
- **Name it for the mechanism, not its first use**; the rename happens in the same edit as the second use.
- **Extend before inventing**: `Classes.All` · `Ab.*` + `ClassDef.Abilities` · `ClassDef.Rows` ·
  `Enemies.All` · `Economy.All` · `Unlocks.All` · `Missions` · `Equipment` · `Tag` · `Status` · `NetIds` ·
  `TargetFilter` · `TurretSpec`/`ITurretHost` · `Aim`/`Motion` · `PlayerShip.Slot` ·
  `PlayerShip.Incoming`/`Guarded` · `PlayerShip.NoteDealt`. Before the second of anything, make the first
  a row.
- Delete what a change replaces in the same edit (`UNUSED ANYWHERE: 0`). No new tool script unless the job
  recurs; scratch files go in the scratchpad.

## 8 · The ladder — the cheapest rung that can SEE it

| # | Rung | Cost | Sees | Blind to |
|---|---|---|---|---|
| 0 | read the code and the logs you have | free | what you can reason about | what you were not given |
| 1 | `typecheck\typecheck.ps1` | ~20 s | renames, signatures, callers, harness included | behaviour, reflection by string |
| 2 | `verify.ps1 -Quick` | ~1 min | 1 + 0 warnings/findings, `UNUSED ANYWHERE: 0`, control chars, check-with-change | behaviour, drawing |
| 3 | `run.ps1 -Solo` | ~2 min | the single-player narrative: numbers, abilities, UI state | host and guest disagreeing |
| 4 | `tools\screens\run.ps1` | ~3 min | what is drawn: ~110 frames, `LINT: 0` | behaviour |
| 5 | `run.ps1` (six roles) | ~4 min | authority, replication, protocol | nothing the game does |
| 6 | `verify.ps1 -Update` | ~13 min | the bar: 1-5 ×3, map, snapshot, manifest, integrity | nothing; a gate, not a debugger |

**Every engine run goes through `tools\rungs.ps1`** (`powershell -NoProfile -ExecutionPolicy Bypass -File
tools\rungs.ps1 -Tree <checkout> -Tag <new tag> -Steps <chain>`; `-Tree` is required, so a lane is never
tested as version-l by mistake): it runs the chain in order, stops at the
first red, takes the first free of `-Slots` engine slots (slot 0 for bar, wan and trees without slots;
one chain per tree at a time), and writes `%TEMP%\warships_rungs\<tag>\summary.txt` + one log
per step. Read the summary, grep a log; never start a harness directly.

**Build phase: `quick` only, per job and at a lane's HEAD. No engine.** **Test phase** (everything planned is
merged into `version-l`): the engine-slots proof first, then chains covering everything built, several at
once on engine slots:

| what was built | its chain in the test phase |
|---|---|
| renames, signatures only | `quick` |
| a rename of anything reached by string (RPC, NodePath, `Call("x")`) | `quick,solo,six` |
| numbers, behaviour, abilities, rows | `quick,solo,solo` |
| anything drawn | add `screens` |
| anything a guest sees, an RPC, authority | add `six,six` |
| all of it, before the bar | `quick,solo,solo,six,screens` (`six,six` with new guest checks) |
| every test-phase chain green | `bar`, once |

A red in the test phase is traced through the ledgers' job lists (which job's files the failing check touches;
`git bisect` over the job commits if unclear), fixed, and the chain re-run once.

Other steps: `solo@<n>`, `six@<n>` (replay a seed), `wan`. A tag is used once: the runner clears its folder.

**Escalate** one rung only when the lower rung is blind to it, or has failed to resolve it twice. **Come
back down** always: on a red at 5 or 6, read which role and check, fix and re-prove at the lowest rung that
covers it. Two bars in a row for one fix means the ladder was skipped.

## 9 · Standing invariants

- **A · Lifetime.** Everything created, subscribed or hooked is released in `_ExitTree`; orphan and object
  counts do not grow across leaving and re-entering a world.
- **B · Authority.** World and combat state change only under `Net.Sim` / `Net.IsHost`; guests request by
  RPC and the host checks the sender; every visible state reaches guests.
- **C · Replacement.** The old path is deleted; no comment describes old behaviour.
- **D · Correctness.** 0 warnings, 0 analyser findings, 0 unused members.

## 10 · Merge and release

- A lane merges (build phase) when `quick` is green at its HEAD and its opus code gate passed:
  `git merge --no-ff wt/<lane>` into `version-l`. Conflicts are resolved from the ledgers' intent.
- After EVERY planned lane is merged: the test phase (section 8), then the bar once. Red: the lane its failing check traces to gets a fix batch (a new lane
  from `version-l` if it traces to the combination), proved low, then the bar once more. Green: a
  `VERIFIED:` commit, push to BOTH `version-l` and `main`. The owner
  has a standing OK for GitHub releases. Real two-machine play has never worked: never call multiplayer
  "working" until the owner and a friend have played.

## 11 · Replies to the owner

Two to four lines of facts, not process. Say only what they cannot see:
- **Done or not**, with the rung: `rung 3 green` · `rung 5: 2 fails` · `compiles, untested`. Never
  "verified" without a green run.
- **A problem** in one line: what broke, where, what it costs.
- **Scope** of every new request: **a row** (which table) · **a new table** (its foundation) · **not
  without X**.
- **A number** they asked for; **a question** in one line with its default.

Never narrate, restate the request, re-explain an approved design, paste what a tool printed, or praise
your own work. WHY gets one sentence; detail when asked.

## 12 · Commands

```
typecheck\typecheck.ps1 · dotnet build · tools\analyse\run.ps1 · python tools\analyse\xref.py
tools\lanes.ps1                                          # state in one call: lanes, chains, ledger_main
tools\rungs.ps1 -Tree <checkout> -Tag <t> -Steps <chain> [-Slot n] [-Slots n]   # every engine run; lanes run side by side
tools\smoketest\run.ps1 · tools\screens\run.ps1   # only through rungs.ps1; trust LINT: 0 for layout,
                                                  # read a frame only for new art
tools\make_ships.ps1 · python tools\make_sounds.py
python tools\map.py · tools\snapshot.ps1 · tools\manifest.ps1
```
Godot is found by `tools\find-godot.ps1`. `-Update` regenerates map, snapshot and manifest, then checks
integrity; integrity is asked only there (anywhere else the manifest is the last change's and would read
FAILED, which teaches everyone to ignore a red line).

## 13 · Record (same commit as the code)

`docs/CHANGES.md` Handoff + Unreleased + honest `Known broken`; `docs/DESIGN.md` for durable reasoning and
new traps; `docs/README.md` for sizes and commands; `docs/REVIEW.md` for review passes. Keep each entry true
now: delete what a change reversed.
