# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (all Opus 5.5 since the owner's ruling; the sonnet runs wq9tnfn98, wp5moexdp, w7j1zzr40 were stopped and relaunched)
- wf_6344aaed-93f / task w7kg87hb8, "lane-kits-k". K1 done (da21442: J6+J7 with NOTE 2's check fix). Now K2 merge version-l, chain
  quick,solo,solo,six,screens, gate, merge. Returns {kits, gate, merge}. merge=false: merge by hand. gate fail or red: one fix batch.
- wf_8b775b3f-dcc / task w8nx1q179, "lane-slots". S1 (985d4d8) + S2 (df86959) done; NOTE 1 = owner OK for many engines, default -Slots 4. Now S3 docs, the 3-chain proof (it HOLDS slot 0's lock
  meanwhile, so kits/art chains wait), gate, merge. Returns {slots{slots=proven default}, gate, merge}. merge=false: merge by hand.
- wf_6a56d393-8cf / task wcbd6tt6b, "lane-art-j45". J4 (PRE, no POST; j4_1 green) + J5 (started WITHOUT a PRE by the stopped writer; 41 dirty
  paths) settled, merge version-l, chain quick,solo,solo,six,screens, gate (camera ruling), merge. Returns {art{frames}, gate, merge}.
  On return: send the owner the frames (copied to %TEMP%\warships_frames_art); merge=false: merge by hand.

## Next
1. Merge slots, kits, art into version-l (net merged: eb5b2cb, after the coordinator fixed gate 3's two comment defects in fa20b7f).
2. The bar once, then push version-l and main.
3. Then, ALL AT ONCE (owner: run many engines; one worktree + engine slot each), per kits_v31.md section 8: lane A slice 3 (F5, F23 Lines,
   F6, F7), lane B (drives/helm/strafe: F21, F22, F24), D (fields: F9), E (wings: F13), F (curve: Par.cs, numbers section 8), G (raids:
   Squads.cs), and WebRTC R2. Merge points as section 8 (B before slice 4; G before slice 5; C/D/E before slice 5 ends; F before 6a).
   Then slices 4-6 (6 = the 12 classes' kits by tier), rung 4 once, the bar, release; then lane I (items).
4. Delete worktrees WarShips_wt_walls, WarShips_wt_net (merged) and WarShips_wt_test (old net test copy) after the merges.

## Owner questions
- none open

## Notes
- Keep the version-l tree CLEAN while w3gvr44ua runs: its merge step refuses a dirty tree. Commit ledger edits at once.
- Merge risks: ClassArt.PdRing removed (kits) vs art rows; Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
