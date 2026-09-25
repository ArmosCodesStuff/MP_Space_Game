# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running
- wf_e2b4eda1-d12 / task wq9tnfn98, "lanes-walls-art" (old way). Returns {walls, gate, merge, art}; walls already merged (29b154e).
  art: J4 siege, J5 player ships. On return: check the camera never zooms past the player's max (moved only), send the owner the frames.
  COORDINATOR NOTE 2 (UTF-16 summaries) appended to wt_art's ledger_sprites.md.
- wf_ab65d36a-eb3 / task w79l6jmmg, "lane-kits-slice2" (old way). Returns {kits, gate}; does not merge. J6+J7 red in solo twice
  (kits_j67a/b: "...and it lands 35 (0)", the heavy missile); its ledger POST claims green: VERIFY against S\kits_j67*\summary.txt on return.
  COORDINATOR NOTE 1 (UTF-16) appended to wt_kits' ledger_kits.md. On gate pass: a merge batch (step 0 merge version-l, merge chain, merge).
- wf_2b58e76d-117 / task wx9xf98c3, "kits-heavy-missile-diagnose" (owner asked for 5 agents): 5 opus read-only angles (check, missile, door,
  launcher, logs) on "lands 35 (0)", then a synthesis appends COORDINATOR NOTE 2 (cause + exact fix) to wt_kits' ledger, or reports already_fixed.
  On return: tell the owner the cause in one line. The kits writer picks the note up at its next PRE.
- wf_d333d7cd-747 / task w7j1zzr40, "lane-slots" (relaunch; worktree WarShips_wt_slots). S1 done at 985d4d8 (rungs.ps1 -Slot/-Slots,
  per-tree lock, UTF-8 summary). First run: agent 1 stopped_context, agent 2 took the owner's "2 hours?" as its instruction and stopped;
  the prompt now says owner messages are for the coordinator. Now: S2 harness --port-shift / P(), S3 docs, 3-chain proof, opus gate, haiku merge. Returns {slots{slots=proven default}, gate, merge}. merge=false: merge by hand.

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
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
