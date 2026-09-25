# Coordinator state -- last event 16:52: round 1 (attempt 4, 68ad42c) ran all 5 chains, triage running; a Fable code-read agent started.
# Baseline reds: solo 41 fails + 1 throw at slot 0, 206 + 3 thrown lanes at slot 1, six ~240 + 3 lanes, screens 5 exc / LINT 2.
Under 25 lines; Edit tool only; commit FIRST after every event; the hook re-injects it after a compact and cross-checks each `task <id>`.

## Running (one row per workflow; the form `task <id> (<run id>)` is what tools/lanes.ps1 parses)
- test-phase / task wu5xd3g1m (wf_8c62491c-3b2, attempt 4 at 16:33, review [] = all 5 scopes merged, last lt 68ad42c): rounds (5 chains, triage, pooled fixes) -> seed sweep ->
  extras -> bar -> release. lands: 3 lines to the owner + the frames (framesForOwner, netOwed); a stop -> docs/plans/ledger_test.md
  and the journal, fix, relaunch: Workflow({scriptPath: <scripts>\test-phase.js, resumeFromRunId: "wf_8c62491c-3b2", args: {attempt: 2, review: []}}).
  Watchdog Monitor (agents.ps1 -Minutes 30 every 10 min, 30-min timeout) re-armed at each expiry while it runs.
- code-read / one Fable 5.1 agent (background, 16:52; owner: "read all of the code, is a rewrite easier than testing?"): reads all
  105 game files, writes + commits docs/plans/fable_code_read.md (map, god files, rule breaks, red sorting, verdict with numbers;
  17:10 owner's 2nd question sent to the same agent: what to globalize / un-hard-code / split for cheaper testing, 30-line section).
  lands: 4 lines to the owner from its 20-line return; the file is the durable answer (no task id: an Agent, not a workflow).

## Landed (verdict first; delete the row once its action is done): none

## Next (1 is the exact next call, copy-pasteable)
1. Wait for task wu5xd3g1m; take its return as above (slots proved 16:40: random seeds on slot 3, box ports bound).
2. After the release, in order (owner 17:20): two-machine test row; scenarios lane (table, scenarios.py, method-by-method conversion,
   each row proved against its method before deletion; fable_code_read.md lists the methods); PlayerShip/Hub split proved by the rows; rest of fable_report lists.

## Owner questions (one line each, with its default): none open (today's rulings: README, Agents).

## Notes (merge risks, promised follow-ups; nothing done, nothing historical)
- Cards' OPEN lines for triage: BB fire-mode key (spec R, built G); Tender numbers have no spec file; DD `longlance`; no grapnel_rip sound.
- Unverified work: origin/backup/unverified; version-l/main move only on a green bar. Lane defaults for the release notes: each lane ledger.
