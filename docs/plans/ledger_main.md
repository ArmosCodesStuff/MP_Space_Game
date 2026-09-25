# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (TEST PHASE since 2026-09-25 12:51; process = lean CLAUDE.md bccdadd + docs/process/*.md; Fable coordinates)
- test-prep / task w1t537izg (wf_f152cf63-f20): the harness Lane() helper (wt_prep, Opus) and 12 spec cards docs/plans/cards (wt_cards:
  4 Sonnet writers, an Opus review, a Sonnet fix), merged by the Haiku->Sonnet->Opus chain. Both rows merged: launch test-phase.js (Next 1).
  A row not merged: read it, one fix agent or a merge by hand, then launch.
- watchdog: a 30-min Monitor polls tools/agents.ps1 every 10 min while Sonnet/Haiku agents run; re-arm it at each expiry.

## Next
1. Workflow({scriptPath: workflows/scripts/test-phase.js}): rounds a (5 chains, Haiku runners, Opus triage, fixes) -> audit merge after
   the first green -> rounds c -> extras (frames, wan, pack -Dirty) -> bar -> release. A stop: read the row, fix, relaunch with
   resumeFromRunId and args {attempt: 2}.
2. After the release: frames to the owner (framesForOwner), the owed one-machine / two-machine network checks (netOwed), then the
   critique's "after this release" list (REPORT.md in scratchpad/fable; copy it to docs/plans/process_after_release.md first).
3. Delete each lane's worktree once merged (t0..t4 belong to the test phase).
Slots proof tpslots_0/1 at ae8ae99: two chains at once on slots 0 and 1, quick green on both, solo ran to the end on both: 42 / 45 problems,
  the same first 12 at two seeds = deterministic reds (every warp +27 u and its overshoot pricing, broadside 343.75 of 375, the fingerprint
  coverage check, the BB keys tab, a squad tracker on jumps, "never walled"). Tooling proven; the reds are round 1's triage.
Build phase complete: every lane merged at 1ff39a3 (defaults in each lane ledger); backup origin/backup/unverified.

## Owner questions
- none open. Owner 2026-09-25: Fable orchestrates everything (they switch the model menu); a Fable critique + lean CLAUDE.md proposal is
  running (workflow "fable-critique"; its report + proposal land in the scratchpad's fable folder): report to the owner, then apply the
  CLAUDE.md at a batch boundary (standing OK for recommendations).

## Notes
- WATCHDOG: while any Sonnet/Haiku agent runs, keep `powershell -File tools/agents.ps1 -Loop` running in the background (Bash
  run_in_background); when it exits it prints the flagged agents: look, stop/redo on Opus if wasteful, re-arm it.
- Owner 2026-09-25 "push soon, you have a lot": unverified work is backed up to origin/backup/unverified (never version-l/main before a green bar).
- A wait agent cannot wait long (the harness forces an early answer): express waits as promises inside ONE workflow.
- Items defaults to report: D-J8a web hold round 2 s (Web Breaker cuts each round's hold); raids' adds scaling HullShare/DamageShare.
- Keep the version-l tree CLEAN while wthoe9tj1 / wojzfsouf run: their merge steps refuse a dirty tree. Commit ledger edits at once.
- Merge risks: whichever of kits/art merges second resolves ClassArt.PdRing (kits removed it) and Ships.cs mount literals (list: ledger_sprites J5 POST); Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
