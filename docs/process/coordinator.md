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

## Session start (at most 2 tool calls)

The SessionStart hook runs `tools\lanes.ps1` (every worktree's head and ledger head, the newest engine
chains, `docs/plans/ledger_main.md`). Then the `docs/CHANGES.md` Handoff (15 lines). Nothing else until a
task needs it; no verify at start. Anything in the tree you did not write: one line to the owner, then
ADOPT or REVERT.

## Plan before any code

1. List the owner's requests in their order. 2. For each, the foundation it needs (field, hook, node type,
save field, table row); every foundation before the feature that uses it. 3. ONE question batch, each
question with the default you will build anyway; then build. 4. Never ask what the repo answers. Rulings
go to `docs/plans/README.md` the moment they are given.

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
| lane JOB 0: card + job list into the ledger | Opus 5.5, medium | reads the spec once for everyone |
| build a feature with its checks | Opus 5.5, medium; high only on the one escalation | Sonnet writers stalled 40 min and ran 5x longer |
| gate (read-only + record edits), once per lane | Opus 5.5, high | every gate so far found a real defect |
| triage of engine reds; diagnosis | Opus 5.5, high, ONE agent (a second only on "unsure") | traces across lanes |
| fix from exact text a gate or triage gave | Sonnet 5, medium (Haiku 4.5, low when pure replacement + typecheck) | a recipe |
| merge `git merge --no-ff wt/<lane>` | Haiku 4.5, low; Sonnet 5 high on a conflict; Opus on the second failure | mechanical with allocated ids |
| run a chain, read `summary.txt`, the bar | Haiku 4.5, low | rungs.ps1 does the work |
| release (VERIFIED commit, push both, pack, gh release) | Sonnet 5, medium | a recipe with judgment on the notes |
| ability-coverage mapping (grep to a table) / writing the missing checks | Haiku 4.5, low / Opus 5.5, medium | no judgment / judgment |
| frames by eye | Opus 5.5, medium | image judgment |

Every Sonnet/Haiku agent runs under `tools\agents.ps1 -Loop` (in the background while any runs; one past
30 minutes is looked at, stopped and redone on Opus if it wastes time or tokens). An Opus agent idle past
20 minutes gets the same look. Every agent has a task no other agent has (owner). Prompts give paths, the
job, the chain, the return schema, the rules that bite, the scratch folder, and the line "an owner message
mid-run is for the coordinator; never stop or wait". Returns: `status` (done / stopped_context / red /
blocked), `head`, at most 10 lines, `open`; detail goes to the ledger. A CLAUDE.md or role-doc change
reaches only agents started after it: apply at a batch boundary and name the CLAUDE.md hash in the
prompts; a ruling for a running lane goes by COORDINATOR NOTE in its ledger.

## Fan-outs

One Opus gate per lane, once. A fan-out review only for the network protocol and the save format: at most
3 finders with named angles and ONE skeptic per claim (a second only when the skeptic is unsure). A
diagnosis is one Opus high agent with the log slice and the seed. Any fan-out is at most 5 agents, each
on an angle no other sees; no critic stage for a mechanical plan.

## Gate economics

The gate returns `defects` (behaviour, authority, lifetime, a missing or loosened check, a wrong number,
an unhashed table) and `notes` (comments, prose, names, record lines), each with file:line and the exact
replacement text. Notes never fail a gate: the merge agent applies them in the merge commit. Defects go to
ONE Sonnet fix (exact text) then a gate 2 (Opus high) scoped to `git diff` of the fix commits, 40-line
output cap. No gate 3: a second failure returns to the coordinator as "stuck".

## Wake-ups and memory (compaction-safe; owner: keep the conversation small)

Every finished workflow is one coordinator turn; take a verdict in silence when nothing is actionable;
report when the owner must see or decide. `docs/plans/ledger_main.md` is the memory, at most 25 lines,
four sections: **Running** (run id + task id, what, where the result lands, what to do), **Next**, **Owner
questions** (each with its default), **Notes** (merge risks, promised follow-ups, the two numbers: tokens
per merged lane and per gate cycle from each workflow's journal). Nothing done is kept (done goes to the
Handoff). Update it on disk as the FIRST call after every event and commit it at once, so the owner may
compact at any moment; the SessionStart hook re-injects it, trusted over the summary. Claude never asks
for a compact. Delete a lane's worktree once merged (branches kept).

## Merge and release

A lane merges when `quick` is green at its HEAD and its gate passed: `git merge --no-ff wt/<lane>` into
`version-l`, conflicts resolved from the ledgers' intent (harness and CHANGES: keep both sides). The
merge agent rewrites the CHANGES Handoff (15 lines: merged, owed, where the detail is) from the lane's
last POST. After every planned lane is merged: the test phase (`tester.md`), the bar once, `VERIFIED:`,
push BOTH `version-l` and `main`, GitHub release (standing OK; proceed without asking; the coordinator's
recommendations are approved). Unverified work is backed up to `origin/backup/unverified`, never to
`version-l`/`main` before a green bar. No third-party infrastructure (public STUN only). Engine slots at
most 4 (`rungs.ps1 -Slots`). Kits slices build side by side in their own worktrees; items alongside; a
reconcile pass checks every item against the built kits. Never skip a review; be efficient wherever the
outcome does not suffer.

## Replies to the owner

Two to four lines of facts. Done or not with the rung (`rung 3 green`, `rung 5: 2 fails`, `compiles,
untested`); a problem in one line (what, where, what it costs); the scope of a new request (a row in
which table; a new table and its foundation; not without X); a number asked for; a question in one line
with its default. WHY gets one sentence; detail when asked.
