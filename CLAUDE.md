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

### Sequence only what depends -- and fan out only what pays (§2b)

Two pieces of work are sequential only when one needs the other's OUTPUT -- a foundation before
the feature that uses it, an edit before the check that proves it. Everything else starts at once.

- **Independent tool calls go in one message**, never one per turn.
- **Read-only work MAY go to a second agent, on a DIFFERENT ANGLE** (a review, an audit, a sweep of a
  subsystem you have not read) when §2b says it pays: give it a scope no other one has, so the
  findings ADD UP. Duplicates of one job only cost tokens, and most work is cheaper as one agent's
  BATCH (§2b rule 3).
- **Take results as they land.** Stop the stragglers once the map is complete rather than waiting
  for the slowest.
- **An engine rung and read-only agents run together fine** -- but every agent's prompt must say
  not to build and not to start the engine: the rung 3-6 harnesses share one scratch folder, and a
  second one wipes the first (§4).
- **Agents that WRITE never run in parallel** unless each has its own worktree. Two edits to one
  file is a lost edit.
- Sequential is a decision that needs a reason. If you cannot name what the second step takes from
  the first, they were concurrent and you were slow.

## 2b · Tokens: spend them where they buy accuracy

Measured on the 2026-09-24 session (1.52 billion tokens, 97% of it re-reading cached context): the
conversation's own size, re-read on every turn, is the cost -- not output. Every rule below is
explicit and binding.

1. **Compact aggressively; never every turn.** Write a 10-20 line state note first (what runs,
   what is next, open questions with defaults -- in the repo's docs/CHANGES.md Handoff or
   docs/plans/, never only in the conversation), then compact when ANY of these is true: the
   context passes ~200k tokens; a slice is committed; a design or decision document has landed and
   its outcome is written down; the work turns to a different subject; a big log or report has
   been read and its conclusion written down; **several agents or a workflow are about to be
   launched** (owner, 2026-09-25) -- write each agent's prompt to a file in the scratchpad first,
   tell the owner "compact now", and launch after the compact, so every one of their wake-ups is
   paid at the small context's price, not the large one's. Do not compact every turn: it throws away the prompt
   cache (a cached re-read costs about a tenth of fresh input) and the detail that then has to be
   re-read. Hold off only while waiting on a job whose purpose is not in the state note yet.
   Claude cannot run `/compact` itself: at a trigger it writes the note and tells the owner
   "compact now" in one line.
2. **Fewer wake-ups.** Every background job's completion is a full turn at the full context's
   price (37% of that session's turns were wake-ups). Chain engine rungs into ONE background
   command that stops at the first red; start agents so they finish together; take a finished job
   in silence when nothing is actionable, and report once, when there is something the owner must
   see or decide. **A batch wakes the main conversation ONCE (owner, 2026-09-25):** launch it as
   one workflow that holds the whole chain -- build, the writer's own engine rungs, a fresh agent
   from the ledger when one stops at ~150k, one escalation on a red, the merge gate -- with its
   rules for a red written into the script, so the main conversation hears only "green" or
   "stuck". Engine runs from agents go through one runner that waits for the engine to be free,
   never a harness started directly.
3. **One or two agents at a time, each on a BATCH (the default way to delegate).**
   - At most two agents run at once, never two writers in one checkout. A wider fan-out (a
     workflow) is only for large design, audit or research with genuinely different angles --
     name what each extra agent sees that the others do not; cap it at 5; no critic stage for a
     mechanical plan; one restart per agent, after reading why the first attempt failed.
   - A batch is several related jobs, in order, chosen so they SHARE files: the big files are read
     once per batch, not once per job (SmokeTest.cs.txt was opened 378 times in one session).
   - Every agent keeps a LEDGER on disk (scratchpad `ledger/<batch>.md`, or `docs/plans/` when it
     must outlive the session). The ledger is the record; the agent's reply is the ledger's path
     plus at most 10 lines. Each job writes TWO entries:
     - **PRE, before it touches anything:** job id, intent, the files it will touch, the commit it
       starts from (`git rev-parse HEAD`) and each of those files' hash (`git hash-object`).
     - **POST, when it is finished:** verdict, files changed, the checkpoint commit (a writer
       commits after every finished job), what comes next.
   - A PRE with no POST is an INTERRUPTED job, and the next agent redoes it before anything else:
     it compares those files with their recorded hashes, reverts that job's half-made edits (or
     keeps them only where they match the plan exactly), then runs the job again. Jobs are written
     to be repeatable: exact-match edits skip what is already applied, so a redo never doubles an
     edit, and an interruption costs at most the one job in flight.
   - An agent stops at a job boundary with its ledger current once its own context passes ~150k;
     the rest of the batch goes to a FRESH agent that reads the ledger, not the old transcript.
   - The one writer may run the engine rungs for its own batch (build, rung, fix, re-run) while no
     other engine run is going: the fix loop then happens in its small context, not the main one.
   - The main conversation holds only ledger paths and verdicts, so compacting it loses nothing and
     nothing is multiplied by the number of agents.
4. **The lowest tier you can trust (owner, 2026-09-25).** Every agent gets the cheapest model that
   can be trusted to do its job reasonably, and the `model` is set explicitly on every call --
   never left to inherit the main conversation's. The default is the LOW tier; a higher one needs a
   reason you can name.
   - **haiku, low effort:** grepping, copying, reading a log for its verdict, re-running a check,
     applying exact edits someone else wrote, a merge whose resolution is written down.
   - **sonnet:** building from a written plan or ledger handoff with its checks (a slice, a sprite
     re-map, a data row, a named failing check to fix), a read-only sweep or audit of a subsystem.
   - **opus (the default model):** design, diagnosing a failure nobody has explained yet, authority
     and wire work, the reviewer or skeptic whose verdict gates a merge -- where a wrong answer
     costs more than the tokens.
   - **Escalate one tier after one failed attempt**, having read why it failed; never start high
     "to be safe". Record each job's tier in its ledger PRE.
5. **Sequential beats parallel when jobs share files; resume while small.** Several agents reading
   the same files each pay for them; one agent doing the jobs in turn reads them once. For a
   follow-up on the same scope, RESUME the agent that did the work while its transcript is under
   ~150k; past that, a fresh agent reading its ledger is cheaper.
6. **Agents return a verdict and a pointer, not a report.** Detail goes to a file; the reply is at
   most ~40 lines: pass/fail, what changed (file: one line), the number that matters, the file to
   read for more. (Returned reports reached 89 KB and all of it entered the caller's context.)
7. **Never paste a log.** Redirect engine and build output to a file and grep the verdict and the
   failing lines back. A whole log in the conversation is paid for on every later turn.
8. **Read slices, never whole big files.** SmokeTest.cs.txt, Hub.cs, PlayerShip.cs, CHANGES.md and
   DESIGN.md: grep for the name, then read the lines around it with an offset. version/MAP.md (the
   generated map of every script, member and RPC) answers "what exists" without opening the code.

## 3 · Build

- Batch edits. No engine run during development, ever. Compile checks (`typecheck.ps1`,
  `dotnet build`) are free — use them; they are not tests.
- **The harness is source.** `tools/smoketest/SmokeTest.cs.txt` and `tools/screens/Shots.cs.txt`
  are compiled INTO the game by their runners. A rename or a deletion makes them callers you must
  fix in the same edit. `typecheck.ps1` compiles them with `scripts/`, so it costs a minute;
  before it did, it cost an engine run three minutes in, after a full copy and import.
- **Never start an engine run while a compile check is red.** Green `-Quick` first, always.
- **EVERY CHANGE CARRIES ITS CHECK — no exceptions, same edit pass.** Before the first line of a
  feature is written, name the checks that will prove it; they land in the same commit.
  1. **New** (a mechanic, feature, ability, row, weapon, enemy, status, screen, rule): at least one
     NEW check that fails without it and passes with it — asserting a literal from the request,
     from **3 varied situations** (range, angle, in front / behind, moving / still — `Vary`).
  2. **Interaction** (two things meeting: a weapon on a heavy, a status on a boss, a light's web
     under a heavy's missile, an ability against a platform): its OWN check, per pair touched. "Each
     side works alone" is not a check of the pair.
  3. **Changed** (a number, a rule, a behaviour): grep the harness for every check that asserts the
     old truth — its literal, its row id, its message — and REWRITE each to the new truth in the same
     edit. A check is never deleted or loosened to make a change pass; if it is truly obsolete, its
     replacement lands in the same edit.
  4. **Fixed bug**: a check that reproduces the bug first (it must fail on the old code), then passes.
  5. **Visible**: a named frame in `Shots.cs.txt`, read by eye once.
  6. **Wire** (an RPC, a field, a host-decided state a guest sees): a check in a guest role (rung 5).
  The commit message ends with a `Checks:` line naming every check added or rewritten. `verify.ps1`
  refuses a code change under `scripts/` with no harness change since the last `VERIFIED:` commit.
- **A CHECK IS CODE, AND FLAKES ARE BUGS IN IT.** Before a new check is committed, rule out the
  three traps that have cost this project engine runs — each of them passed once and failed later:
  - **A knife edge.** A spot exactly ON a boundary, dead astern (π), exactly at a range limit,
    a float compare on an accumulated clock: vary AROUND the edge, never onto it.
  - **A stale pick.** Anything chosen or measured before a wait (the nearest foe, a position, a
    heading) is re-taken after it — the world moved.
  - **Timed from the input, not the effect.** Start a clock on the first frame the effect shows (the
    hull moves, the shot exists), never on the key press or the call.
  A new or rewritten check must pass on **two different seeds** at its rung before its commit.
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
- **NAME IT FOR THE MECHANISM, NEVER FOR ITS FIRST USE.** `DamageNumbers` was right until
  something else wanted a floating readout; `ShipClasses.cs` stopped being true the day the class
  table moved to `Ships.cs`. A name that describes one use is a name that becomes a lie, and the
  next instance either believes it or copies the file rather than extending it. When a second use
  arrives, the rename happens in the SAME edit as the second use -- never "later".
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
| 2 | `verify.ps1 -Quick` | ~1 min | rung 1 + 0 warnings, 0 analyser findings, `UNUSED ANYWHERE: 0`, no control characters, and no code change without a check change | behaviour, numbers, anything drawn |
| 3 | `tools\smoketest\run.ps1 -Solo` | ~1.5 min | the whole single-player narrative: behaviour, every number, ability state, UI state | a host and a guest disagreeing |
| 4 | `tools\screens\run.ps1` | ~1.5 min | what is DRAWN: 108 frames, `LINT: 0` for off-screen, clipped and overlapping | behaviour |
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
  wiring is in doubt. Otherwise a new check passing on two different seeds is enough (§3).
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

**Two to four lines. State facts, not process.** The player reads these to decide what to do next,
not to learn what you did.

Say only what they cannot see for themselves:

- **Done, or not**, with the rung that proved it: `rung 3 green` · `rung 5: 2 fails` ·
  `compiles, untested`. Never "verified" without a green run.
- **A problem**, in one line: what broke, where, and what it costs.
- **Scope**, whenever they ask for something new -- this is the one thing they always want and
  cannot get anywhere else. Three answers, in these words:
  - **a row** -- it fits a table that exists; say which.
  - **a new table** -- it needs a foundation first; say what the foundation is, in one line.
  - **not without X** -- it cannot be done as asked; say what X is.
- **A number they asked for**, on its own.
- **A question you need answered**, in one line, with the default you will use if they do not answer.

Never:

- narrate what you are about to do, or what you just read
- restate their request back to them
- re-explain a design they have already approved
- paste a table, a log or a diff the tool already printed -- point at it
- use adjectives about your own work

If they ask WHY, answer in one sentence. If they ask for detail, give it -- brevity is the default,
not a rule against answering.

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
