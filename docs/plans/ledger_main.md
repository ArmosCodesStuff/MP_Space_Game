# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE COMPLETE 2026-09-25: every planned lane is merged into version-l at 1ff39a3; nothing has run in the engine)
- fable-critique / task w2f27eojd: 3 Fable critics + a synthesis -> scratchpad/fable/REPORT.md, CLAUDE.md, process/*.md, MAP.md. When it
  lands: report to the owner (SendUserFile REPORT.md), apply the "NOW" items (CLAUDE.md swap, test-phase.js edits), THEN launch the test phase.
- slots proof (background PowerShell): two chains quick,solo at once from WarShips_wt_t0 / _t1 (tags tpslots_0/1). Both green = slots work;
  a red is a rungs.ps1 / harness -Slot bug to fix before the test phase.

## Next
1. Launch Workflow({scriptPath: workflows/scripts/test-phase.js}) once the critique's NOW items are applied, and arm the watchdog
   (agents.ps1 -Loop in the background). Its rows: audit -> rounds -> extras -> bar -> release. A stop: read the row, fix, resume with
   args {attempt: 2}.
2. After the release: frames to the owner (its framesForOwner), the owed one-machine / two-machine network checks (netOwed), then the
   critique's "after this release" items.
3. Delete each lane's worktree once merged (t0..t4 are the test phase's).
Done: BUILD PHASE. Kits lane A slices 1-6 (6a d524c33, 6b bcab9c3, 6d 667be46, 6c ac2817d), items 669c5a3 + reconcile 1ff39a3,
  follow-ups d9f2e82 (fingerprint tables, retarget lead, warp cooldown), drives, fields, wings, curve, raids, net2 (R2-R5), art, walls,
  slots, net R1. Defaults the lanes took are in each ledger's "defaults" lines (6a: rip on a dummy = 10, no rip sound; 6d: Wraith numbers
  derived from the power rows; items: 4 unpriced pairs). Backup: origin/backup/unverified.

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
