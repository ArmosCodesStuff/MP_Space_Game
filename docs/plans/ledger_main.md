# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

## Running (TEST PHASE since 2026-09-25 12:51; process = lean CLAUDE.md bccdadd + docs/process/*.md; Fable coordinates)
- DONE test-prep: harness Lane() helper 5e688ac (178 calls wrapped; a thrown lane prints "FAIL LANE <name> threw" and the role goes on);
  12 spec cards docs/plans/cards 306a531. Cards' OPEN lines for the review/triage: BB fire-mode key (spec R, built G, checks assert G);
  Tender numbers have no source file (kits_v1 missing: the code is the source); DD `longlance` id (renamed off the Tender's lance);
  grapnel_rip sound never built.
- watchdog: a 30-min Monitor polls tools/agents.ps1 every 10 min while Sonnet/Haiku agents run; re-arm it at each expiry.
- fable-critique-2 / task w8qdfovp6 (wf_f703a3a3-c3e): testing, hand-offs + compaction, verbiage -> scratchpad/fable2/REPORT2.md,
  CLAUDE.md v2 + CLAUDE.diff.md, process/, tools/. When it lands: REPORT2 to the owner; apply at the next batch boundary; report in 3 lines.

- DONE fable-review: 46 findings in docs/plans/review_tasks.json, 37 kept (severity >= 2; kept=true), 9 dropped (owner: speed). Top:
  Warden PD 10 DPS/mount vs the ruling's 1 (hv-R6c-1); a guest DD's warp mid-grapple rips 5 s late on the host (cap-R1); a rejoin
  without the token is not refused (net-RV1); Dealt.Deal credits blows on a dead target (net-RV3). test-phase.js applies them per scope
  (args.review: one Opus fix agent per scope, one proving chain each) before round 1. The blanket 3+ audit is OFF (args.audit unset).

## Next
1. When test-prep, critique-2 and fable-review have all landed: apply critique-2's test-phase.js / tester.md changes (owner: overhaul
   permitted), then Workflow({scriptPath: workflows/scripts/test-phase.js, args: {tasks: <review tasks>}}): review fixes -> rounds a ->
   audit merge after the first green -> rounds c -> extras -> bar -> release. A stop: read the row, fix, relaunch with resumeFromRunId
   and args {attempt: 2, tasks: []}.
2. After the release: frames to the owner (framesForOwner), the owed one-machine / two-machine network checks (netOwed), then the
   critique's "after this release" list (REPORT.md in scratchpad/fable; copy it to docs/plans/process_after_release.md first).
3. Delete each lane's worktree once merged (t0..t4 belong to the test phase).
Slots proof tpslots_0/1 at ae8ae99: two chains at once on slots 0 and 1, quick green on both, solo ran to the end on both: 42 / 45 problems,
  the same first 12 at two seeds = deterministic reds (every warp +27 u and its overshoot pricing, broadside 343.75 of 375, the fingerprint
  coverage check, the BB keys tab, a squad tracker on jumps, "never walled"). Tooling proven; the reds are round 1's triage.
Build phase complete: every lane merged at 1ff39a3 (defaults in each lane ledger); backup origin/backup/unverified.

## Owner questions
- none open. Rulings today (README, Agents): Fable orchestrates and picks models; Fable's CLAUDE.md approved (applied bccdadd); Fable keeps
  iterating the process after every phase toward an EXTREMELY EFFICIENT WORKFLOW; cut reply/prompt text; a method to compact at any moment.

## Notes
- Unverified work is backed up to origin/backup/unverified (owner: "push soon"); version-l/main move only on a green bar.
- A wait agent cannot wait long (the harness forces an early answer): waits are promises inside ONE workflow.
- Lane defaults to report with the release: items D-J8a web hold round 2 s; raids adds scaling HullShare/DamageShare; 6a rip on a dummy = 10,
  no rip sound; 6d Wraith numbers derived from the power rows; items: 4 unpriced pairs.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
