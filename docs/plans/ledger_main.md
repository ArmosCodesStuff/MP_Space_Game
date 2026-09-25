# Coordinator state -- last event 16:52: round 1 (attempt 4, 68ad42c) ran all 5 chains, triage running; a Fable code-read agent started.
# Baseline reds: solo 41 fails + 1 throw at slot 0, 206 + 3 thrown lanes at slot 1, six ~240 + 3 lanes, screens 5 exc / LINT 2.
Under 25 lines; Edit tool only; commit FIRST after every event; the hook re-injects it after a compact and cross-checks each `task <id>`.

## Running (one row per workflow; the form `task <id> (<run id>)` is what tools/lanes.ps1 parses)
- test-phase / task wu5xd3g1m (wf_8c62491c-3b2, attempt 4 at 16:33, review [] = all 5 scopes merged, last lt 68ad42c): rounds (5 chains, triage, pooled fixes) -> seed sweep ->
  extras -> bar -> release. lands: 3 lines to the owner + the frames (framesForOwner, netOwed); a stop -> docs/plans/ledger_test.md
  and the journal, fix, relaunch: Workflow({scriptPath: <scripts>\test-phase.js, resumeFromRunId: "wf_8c62491c-3b2", args: {attempt: 2, review: []}}).
  Watchdog Monitor (agents.ps1 -Minutes 30 every 10 min, 30-min timeout) re-armed at each expiry while it runs.

## Landed (verdict first; delete the row once its action is done)
- code-read 17:25 (17b94df, fable_code_read.md): do NOT rewrite (fix 2 wk vs 8-12). 4 root causes: NaN cascade Math.Sign at PlayerShip.cs:2299,
  Turrets.cs:345, Hauler.cs:283, Prism.cs:105 (~48 sigs; triage a1 has only a1-turret-nan, deferred); CIWS DamageOf unwired PlayerShip.cs:343;
  14 stale literals; 6 bugs; 9 traps. action: round-2 triage reads §4-5 first (script pointer for r>=2 + a message when it starts; Monitor b38t0d23z).

## Next (1 is the exact next call, copy-pasteable)
1. Wait for task wu5xd3g1m; take its return as above (slots proved 16:40: random seeds on slot 3, box ports bound).
2. After the release, in order (owner 17:20, README next 8-9): two-machine test row; scenarios lane (row-by-row conversion); PlayerShip/Hub split; fable_report lists.

## Owner questions (one line each, with its default): none open (today's rulings: README, Agents).

## Notes (merge risks, promised follow-ups; nothing done, nothing historical)
- Cards' OPEN lines for triage: BB fire-mode key (spec R, built G); Tender numbers have no spec file; DD `longlance`; no grapnel_rip sound.
- Unverified work: origin/backup/unverified; version-l/main move only on a green bar. Lane defaults for the release notes: each lane ledger.
