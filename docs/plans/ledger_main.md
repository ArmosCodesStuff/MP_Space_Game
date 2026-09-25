# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running
- wf_e2b4eda1-d12 / task wq9tnfn98, "lanes-walls-art" (old way). Returns {walls, gate, merge, art}; walls already merged (29b154e).
  art: J4 siege, J5 player ships. On return: check the camera never zooms past the player's max (moved only), send the owner the frames.
  COORDINATOR NOTE 2 (UTF-16 summaries) appended to wt_art's ledger_sprites.md.
- wf_1bf3d281-818 / task wp5moexdp, "lane-kits-k" (new way). K1: NOTE 2's check fix (diagnosis wx9xf98c3, 5/5: the J7 CHECK was wrong --
  first-tick zero lead + pinned pilot under forced thrust drifting off the blast; hvL's stray blast passed "flight 10 s"), commit J6+J7, J7 POST
  corrected; K2: merge version-l (net R1); chain quick,solo,solo,six,screens; opus gate; haiku merge. Returns {kits, gate, merge}.
  merge=false: merge by hand. gate fail or red: one fix batch from its problems. (w79l6jmmg was stopped by the owner at ~2 h.)
- wf_d333d7cd-747 / task w7j1zzr40, "lane-slots" (relaunch; worktree WarShips_wt_slots). S1 done at 985d4d8 (rungs.ps1 -Slot/-Slots,
  per-tree lock, UTF-8 summary). Now: S2 harness --port-shift / P(), S3 docs, 3-chain proof, opus gate, haiku merge (net is in, so it may
  merge itself). Returns {slots{slots=proven default}, gate, merge}. merge=false: merge by hand.

## Next
1. Merge slots, kits, art into version-l (net merged: eb5b2cb, after the coordinator fixed gate 3's two comment defects in fa20b7f).
2. The bar once, then push version-l and main.
3. docs/plans/README.md "Next": WebRTC R2-R5, the rest of the class batch, curve/raids numbers, items.
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
