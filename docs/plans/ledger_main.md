# Coordinator state -- last event 22:09: round 3 = solo 117 + 127, six 160 + 161, screens 5 exc; 0 thrown lanes, 0 exceptions (round 1: 41 + 206, ~240 x2, 3 lanes, 994 exc); triage a3 running.
# Baseline reds: solo 41 fails + 1 throw at slot 0, 206 + 3 thrown lanes at slot 1, six ~240 + 3 lanes, screens 5 exc / LINT 2.
Under 25 lines; Edit tool only; commit FIRST after every event; the hook re-injects it after a compact and cross-checks each `task <id>`.

## Running (one row per workflow; the form `task <id> (<run id>)` is what tools/lanes.ps1 parses)
- test-phase / task w84ws46yg (wf_8c62491c-3b2, attempt 4 resumed 22:27: done = all a1 + a2 ids, preTasks a3 = round3_tasks.json (8 tasks from
  triage a3; 22:21's run stranded its 2-task check group on the old prompt cap and had the old merge gate), round 3 fixes run live): rounds (5 chains, triage, pooled fixes) -> seed sweep -> extras -> bar
  -> release. lands: 3 lines to the owner + frames; a stop -> ledger_test.md + journal, fix, relaunch per Next 1. ONE Monitor (journal + watchdog every 15 min).

- thrust / one Opus writer (background 22:37; owner: idle plume half-disc or off, the point only under thrust; tiny cosmetic side thrusters on
  turns/strafe): wt_thrust from version-l, checks + 3 frames, proves quick,solo,screens. lands: one Opus gate, merge before the bar.


## Landed (verdict first; delete the row once its action is done)
- 23:00 retro 36af923 (fable_retro_tokens.md): 97% of input = context re-reads; 74 agents past 100 turns = 76% of 4,913M (coordinator 1,510M).
  Applied 23:05-23:30: 100-call cap, one engine proof per round, Opus check rewrites, merge_lane.ps1, prompts never sliced, the `fixer` agent
  type (file + shell tools), 2 fixes per agent, fails_diff.py for triage; connectors Claude Docs + visualize off. Owner: /compact past 150k.
  action: stop + resume with the new script when round 3's merges land (before round 4's chains); delete this row then.
- 21:40 knife-edges group merged 5e1d86d. Owed to round 3 (kind check): the ramp check's tolerance went 1e-3 -> 3e-3 with the off-boundary
  sampling; restore 1e-3 and prove at 3 seeds (the sampling alone should hold; a widened tolerance is a loosening).

## Next (1 is the exact next call, copy-pasteable)
1. Wait for task w84ws46yg: round 3 = 7 groups over 4 slots, merges; round 4 chains + triage (a3 deferred: stale tables, melee arc, blade, flak, rewind);
   sweep, extras, bar, release. A stop -> relaunch args {attempt: 4, review: [], done: [every a1/a2 id + a3 ids merged], preTasks: {a2: [a stub id in done], a3: round3_tasks.json}}.
   After the release (owner 17:20, README next 8-9): two-machine test row; scenarios lane; PlayerShip/Hub split; fable_report lists.

## Notes (merge risks, promised follow-ups; nothing done, nothing historical). Owner questions open: none (today's rulings: README, Agents)
- Retro: fix prompt ~2,000 chars; six ~8 min; ledger_test row 011d7fe names only a1-rip-dummy4 (fold adds shots-stale-cam, warp-2frames, heavy-target).
