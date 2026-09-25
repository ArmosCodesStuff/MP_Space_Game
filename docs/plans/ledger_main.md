# Coordinator state -- last event 14:10: critique-2 landed; its edits installed; the test phase launches next
Under 25 lines. Edit tool only; commit as the FIRST call after every event. The hook re-injects this after a compact and
cross-checks every `task <id>` below against its output file. Facts go to README / CHANGES, not here.

## Running (one row per workflow; the form `task <id> (<run id>)` is what tools/lanes.ps1 parses)
- (none: the test phase launches with Next 1)

## Landed (verdict first; delete the row once its action is done)
- (none)

## Next (1 is the exact next call, copy-pasteable)
1. Workflow({scriptPath: "C:\Users\logan\.claude\projects\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\workflows\scripts\test-phase.js",
   args: {review: [{scope:"cap",ids:["cap-R1","cap-R2","cap-R3","cap-R4","cap-R5","cap-R6"],six:true},
   {scope:"fr",ids:["fr-R6b-1","fr-R6b-2","fr-R6b-3","fr-R6b-4","fr-R6b-5","fr-R6b-6","fr-R6b-7","fr-R6b-8"],six:true},
   {scope:"hv",ids:["hv-R6c-1","hv-R6c-2","hv-R6c-3","hv-R6c-4","hv-R6c-5","hv-R6c-6","hv-R6c-7"],six:true},
   {scope:"lt",ids:["lt-R6d-1","lt-R6d-2","lt-R6d-3","lt-R6d-4","lt-R6d-5","lt-R6d-6"],six:true},
   {scope:"net",ids:["net-RV1","net-RV2","net-RV3","net-RV4","net-RV5","net-RV6","net-RV7","net-RV8","net-RV9","net-RV10"],six:true}]}})
   then a Monitor (agents.ps1 -Minutes 30 every 10 min, 30-min timeout, re-armed at expiry) and this file's Running row.
2. When it lands (return under 20 lines): 3 lines to the owner + the frames (framesForOwner); a stop -> docs/plans/ledger_test.md and the
   journal, fix, relaunch with resumeFromRunId and args {attempt: 2, review: []}.
3. After the release: the two-machine test (NOTES.txt script) as a ledger row; docs/process/fable_report_1.md + _2.md "after the release"
   lists (harness split, enums, CHANGES fold, scenarios lane, PlayerShip/Hub split), each as a lane with a retrospective after.

## Owner questions (one line each, with its default)
- none open (today's rulings: README, Agents).

## Notes (merge risks, promised follow-ups; nothing done, nothing historical)
- Slots proof tpslots_0/1 at ae8ae99: tooling works; solo 42/44 reds, 32 shared, 7+9 seed-only (the old summary capped at 12 lines).
- review_tasks.json: 37 kept findings (severity >= 2) applied per scope before round 1; 9 dropped (severity 1). Cards' OPEN lines for
  triage: BB fire-mode key (spec R, built G); Tender numbers have no spec file; DD `longlance`; no grapnel_rip sound.
- Unverified work: origin/backup/unverified; version-l/main move only on a green bar. Lane defaults for the release notes: each lane ledger.
