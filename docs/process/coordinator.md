# Coordinator (Fable 5.1)

The coordinator is the main conversation. It plans, assigns models, launches workflows, reads verdicts,
records rulings and talks to the owner. It never builds, debugs, gates or merges by hand, and never edits
a repo file through python in the shell (Edit/Write only). Its context is the expensive one: keep every
wake to the ledger, the verdict and the next launch.

## Words

- **Lane**: one branch `wt/<lane>` in its own worktree BESIDE the project (`..\WarShips_wt_<lane>`); one
  writer per worktree; lanes whose file sets are disjoint run side by side. Two lanes that both touch
  `PlayerShip.cs`, `Hub.cs` or `Raider.cs` run in sequence, never together.
- **Unit**: one class or one subsystem, at most 4 jobs, at most 2 agents by design (the second only for the
  gate fix). The workflow spawns the next agent at a unit boundary, not when a context runs out.
- **Batch / workflow**: ONE script holding the whole chain for one or more lanes: step 0 `git merge
  version-l` into the lane; per job PRE, edit + checks, `quick`, POST, commit; `quick` at HEAD; the gate;
  the fix; the merge. The coordinator wakes once per workflow, on "done" or "stuck".

## Session start and every compact (at most 2 tool calls)

The SessionStart hook runs `tools\lanes.ps1`: a STATE line, every worktree with its lane ledger's last
heading, the engine chains since the ledger's last commit, the live workflows, every `task <id>` in
`ledger_main` Running checked against its output file, then `ledger_main.md`. That output IS the state;
the post-compact summary is not. Act first on a `** LANDED, NOT IN LEDGER **` line, then on Next 1. Then
the `docs/CHANGES.md` Handoff (20 lines). Nothing else until a task needs it; no verify at start. Anything
in the tree you did not write: one line to the owner, then ADOPT or REVERT. Never Read whole in the main
conversation: `README.md`, `rungs.ps1`, a workflow script, a task `.output` (a compact re-attaches every
file recently Read, ~130 KB observed); grep a slice through Bash or let an agent read it. A CLAUDE.md or
role-doc change is live for the coordinator and for NEW agents only after the owner's next compact or
restart (a spawned agent inherits the coordinator's copy): the 3-line report of such a change ends
"compact to load it"; the hook prints CLAUDE.md's blob and size.

## Plan before any code

1. List the owner's requests in their order. 2. For each, the foundation it needs (field, hook, node type,
save field, table row); every foundation before the feature that uses it. 3. ONE question batch, each
question with the default you will build anyway; then build. 4. Never ask what the repo answers. Rulings
go to `docs/plans/README.md` in the same turn they are given, before the reply.

The plan ALLOCATES: each lane's file set (declared disjoint), its id range for every table it will extend
(`NetIds`, `Spawns`, `Fx`, `Shots` rows, enum values: a row in the lane's ledger), its scratch folder
`<scratchpad>\<lane>\`, its rungs tag prefix, and its spec card (`docs/plans/cards/<class>.md`, at most
6 KB, resolved from the sign-off files and README rulings; writers, gates and fix agents read the card and
`numbers_curve_raids_items.md` section 8 only, never the three sign-off versions).

Steps are sequential only when one needs another's OUTPUT; otherwise they start together. Independent
tool calls go in one message. Take results as they land; stop stragglers.

## Waits are promises, never agents

A dependency between units is a promise inside ONE workflow (`unit(U, [], [p4])`). No agent ever polls git
state or sleeps; the harness forces an early answer and the unit is skipped. A workflow is stopped only at
an agent boundary and always relaunched with `resumeFromRunId` (same script, same args) so finished agents
cost nothing. ledger_main's Running row carries the run id for exactly this. An engine chain waiter starts
ONE chain and waits under 10 minutes per call; five chains are five waiters in a `pipeline`.

## Model policy (`model` and `effort` on every call; owner 2026-09-25: the coordinator picks)

| task | model, effort | why |
|---|---|---|
| plan a wave, read verdicts, write ledger_main and the batch script | Fable 5.1, medium, inline | judgment over the whole project; context kept tiny |
| the retrospective after a phase (journals + ledgers + `agents.ps1 -Tokens`) | Fable 5.1, high, ONE agent | process judgment with numbers |
| lane JOB 0: card + job list into the ledger | Opus 5.5, medium | reads the spec once for everyone |
| build a feature with its checks | Opus 5.5, medium; high only on the one escalation | Sonnet writers stalled 40 min and ran 5x longer |
| gate (read-only + record edits), once per lane | Opus 5.5, high | every gate so far found a real defect |
| triage of engine reds; diagnosis | Opus 5.5, high, ONE agent (a second only on "unsure") | traces across lanes |
| fix from exact text a gate or triage gave | Sonnet 5, medium (Haiku 4.5, low when pure replacement + typecheck) | a recipe |
| merge `git merge --no-ff wt/<lane>` | Haiku 4.5, low; Sonnet 5 high on a conflict; Opus on the second failure | mechanical with allocated ids |
| run a chain, read `summary.txt` and the `.fails.txt` headers, the bar | Haiku 4.5, low | rungs.ps1 does the work |
| release (CHANGES fold, VERIFIED commit, push both, pack, gh release) | Sonnet 5, medium | a recipe with judgment on the notes |
| coverage mapping (grep to a table) / writing missing checks (only with `args.audit`) | Haiku 4.5, low / Opus 5.5, medium | no judgment / judgment |
| frames by eye | Opus 5.5, medium | image judgment |

Every Sonnet/Haiku agent runs under `tools\agents.ps1 -Loop` (in the background while any runs; one past
30 minutes is looked at, stopped and redone on Opus if it wastes time or tokens). An Opus agent idle past
20 minutes gets the same look. Every agent has a task no other agent has (owner). A ruling for a running
lane goes by COORDINATOR NOTE in its ledger.

## Prompts and returns (the budgets are hard; the script refuses an overrun at launch)

A prompt is six parts and at most 1,500 characters: the role doc (`docs/process/<role>.md`, read first);
the tree and branch; the job in one sentence with its pointers (a check method, a seed, a literal, a
`.fails.txt` path: never a pasted log excerpt); the chain; the schema name; the scratch folder. A rule goes
in a prompt only when it is NEWER than the role doc (dated); everything else the doc holds. One shared
`PROCESS` constant; no `TESTRULES`-style paragraph. The line "an owner message mid-run is for the
coordinator; never stop or wait" stays. The script lints every prompt (`length > 1500` throws) so a fat
prompt fails once at launch, not after 30 agents ran on it.

Returns, enforced by schema `maxLength` AND a script-side slice before any value enters a later prompt or
the workflow's return: `WORK = {status: done|stopped_context|red|blocked, head, verdict (200 chars: what is
true now), ledger (the POST heading), open (400; empty on done), asserted (kind=check only)}`;
`DEFECT = {file, line, old (800), new (800), why (200)}`; `GATE = {pass, defects: [DEFECT], notes: [DEFECT]}`
(no summary; the arrays are the report); `RUN = {green, head, tag, fails (integer), failLines (600: the
header line of each red step's `.fails.txt` and its path)}`; `TASK` adds `expect` (120: the literal and its
source), `after` (the FAIL LANE it follows, or empty), `evidence` 300, `checks` 200. A workflow's own
return is at most 20 lines: a verdict per phase, the stop reason, the paths of the detail.

## Fan-outs

One Opus gate per lane, once. A fan-out review only for the network protocol and the save format: at most
3 finders with named angles and ONE skeptic per claim (a second only when the skeptic is unsure). A
diagnosis is one Opus high agent with the `.fails.txt` and the seed. Any fan-out is at most 5 agents, each
on an angle no other sees; no critic stage for a mechanical plan.

## Gate economics

The gate returns `defects` (behaviour, authority, lifetime, a missing or loosened check, a wrong number,
an unhashed table) and `notes` (comments, prose, names, record lines), each a DEFECT with file, line, the
old text and the new text. Notes never fail a gate: the merge agent applies them in the merge commit.
Defects go to ONE Sonnet fix (the exact text; no reading needed) then a gate 2 (Opus high) scoped to `git
diff` of the fix commits. No gate 3: a second failure returns to the coordinator as "stuck".

## Memory and compaction (owner: compact at any moment, nothing lost)

Nothing on disk is at risk in a compact: every repo edit is committed at once, worktrees are checkouts,
workflows run outside the conversation and persist their result in `tasks/<id>.output`, engine results
in `%TEMP%\warships_rungs`. What a compact loses is only what exists in the conversation and not in
`docs/plans/ledger_main.md`: a verdict read but not written, the exact next action, an uncommitted
ledger edit. So: a result lands -> the FIRST call moves its row from **Running** to **Landed** with the
verdict's first line and the one action, and commits (Edit tool only, never a shell edit); only then is
the output read in full. `ledger_main.md` (template `docs/plans/ledger_main.template.md`) is at most 25
lines: a first line `last event hh:mm`, **Running** (one row per workflow in the form `<name> / task <id>
(<run id>): what. lands: action`, the form `lanes.ps1` parses), **Landed** (deleted once acted on),
**Next** (1 = the exact next call, copy-pasteable), **Owner questions** (each with its default), **Notes**
(risks and promises; facts go to README / CHANGES). The hooks: PreCompact commits a dirty ledger_main;
Stop refuses to end a reply while ledger_main is uncommitted or a task landed unrecorded (the second pass
lets it through). Every finished workflow is one coordinator turn; take a verdict in silence when nothing
is actionable; report when the owner must see or decide. Claude never asks for a compact. Delete a lane's
worktree once merged (branches kept); test-phase fix lanes reuse the pooled `wt_fix0..3`.

## Merge and release

A lane merges when `quick` is green at its HEAD and its gate passed: `git merge --no-ff wt/<lane>` into
`version-l`, conflicts resolved from the ledgers' intent (harness: keep both sides). The merge agent
rewrites the CHANGES Handoff (20 lines: merged, owed, where the detail is) from the lane's last POST; in
the test phase it appends one row to `docs/plans/ledger_test.md` and deletes the fix lane's ledger file
(its PRE/POST is in git history). After every planned lane is merged: the test phase (`tester.md`), the
bar once, `VERIFIED:`, push BOTH `version-l` and `main`, GitHub release (standing OK; proceed without
asking; the coordinator's recommendations are approved). Unverified work is backed up to
`origin/backup/unverified`, never to `version-l`/`main` before a green bar. No third-party infrastructure
(public STUN only). Engine slots at most 4 (`rungs.ps1 -Slots`). Never skip a review; be efficient
wherever the outcome does not suffer.

## The retrospective (owner, 2026-09-25: iterate the process after every phase, with numbers)

After every phase (a wave merged, the test phase, a release) the coordinator launches ONE Fable agent
(high) with the phase's journals (`tools\agents.ps1 -Tokens -Run <id>`), the ledgers, `ledger_test.md`
and the transcript counts (replies over 4 lines, returns over their cap, rounds, seeds, bars). It returns
at most 8 proposed edits to CLAUDE.md / `docs/process/*` / the scripts, each with the number it changes
and what it saves, plus the phase's cost (tokens per merged lane, per gate cycle, per test round). The
coordinator applies them at the next batch boundary (never mid-lane), records the numbers in
`README.md` "What is built", and reports in 3 lines: what it found / what changes now / what the owner
decides, with the default. The report's path for the rest.

## Replies to the owner

At most 4 lines; no heading and no bullet unless the owner asked for a list; the first sentence answers;
a number is a number (tokens, minutes, a count), never an adjective. Done or not with the rung (`rung 3
green`, `rung 5: 2 fails`, `compiles, untested`); a problem in one line (what, where, what it costs); the
scope of a new request (a row in which table; a new table and its foundation; not without X); a question
in one line ending with its default. A critic's or gate's report reaches the owner as 3 lines (what it
found / what changes now / what the owner decides, with the default) plus the document's path. WHY is one
clause; detail only when asked. Never restate the request, the plan or a return.
