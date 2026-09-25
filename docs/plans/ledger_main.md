# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE: no engine anywhere until every planned lane is merged -- owner; all agents Opus 5.5)
- wf_b2ed579d-80a / task wydvp93f5, "lane-kits-k" (no engine). K1 done (da21442); K2 = merge version-l, IN PROGRESS in wt_kits; quick at HEAD,
  opus code gate, merge. Returns {kits, gate, merge}. merge=false: merge by hand. gate fail: one fix batch from its problems.
- wf_cd730b78-323 / task wzrk1yb4f, "lane-art-fix" (no engine). Art gate 1 (w3j9do0p7) failed e72fe19 on 6: the 2x Rusty rams twice (80 not
  40; check hid it) -> once per dash; ring-hit reach and approach/spawn had no failing check; boss Find measured from the centre (COORDINATOR
  DECISION: from the hull, m.Find + L/2, per the owner's "what is placed around a boss scales with it"); stale comments; a false Known broken.
  Camera ruling PASSED (Hub.BossFramed slides, ZoomOutMax 1.53 asserted). Fix A4, quick, gate 2, merge. Frames are read in the test phase.
## Next
1. When kits merges: launch the next wave ALL AT ONCE, build only (kits_v31.md section 8): lane A slice 3 (F5, F23 Lines, F6, F7), then
   slices 4-6 (6 = the 12 classes' kits by tier) in the same lane; lanes B (F21, F22, F24), D (F9), E (F13), F (Par.cs, numbers section 8),
   G (Squads.cs); WebRTC R2-R5 (network_webrtc.md); then lane I (items). Each: quick per job, opus code gate, merge in section 8's order.
2. TEST PHASE, once everything is merged: the slots proof, then quick,solo,solo,six,screens (+ six,six) across slots, fix low, then the bar
   once, VERIFIED:, push version-l and main, release; frames to the owner (the art lane's Drake, Rusty, siege, player ships).
3. Delete worktrees WarShips_wt_walls, WarShips_wt_net, WarShips_wt_slots, WarShips_wt_test after the merges.
Done: slots merged 25aefc1 (gate 2 passed at a22c187; its 2 notes applied in e104a5a). Engine-unproven: its proof opens the test phase.

## Owner questions
- none open

## Notes
- Keep the version-l tree CLEAN while w3gvr44ua runs: its merge step refuses a dirty tree. Commit ledger edits at once.
- Merge risks: whichever of kits/art merges second resolves ClassArt.PdRing (kits removed it) and Ships.cs mount literals (list: ledger_sprites J5 POST); Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
