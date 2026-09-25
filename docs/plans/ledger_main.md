# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE: no engine anywhere until every planned lane is merged -- owner; all agents Opus 5.5)
- wf_b2ed579d-80a / task wydvp93f5, "lane-kits-k" (no engine). K1 done (da21442); K2 = merge version-l, IN PROGRESS in wt_kits; quick at HEAD,
  opus code gate, merge. Returns {kits, gate, merge}. merge=false: merge by hand. gate fail: one fix batch from its problems.
- wf_ee2e95db-7a6 / task w3j9do0p7, "lane-art-j45" (no engine). J4+J5 POSTed; A3 = merge version-l, IN PROGRESS in wt_art; quick, opus code
  gate (camera ruling in code + its written check), merge. Frames are read in the test phase.
- wf_532652e7-46e / task wecrtf9yf, "lane-slots-fix" (no engine). Gate 1 (wva52tsb5) failed 39cd05c on 4: the sweep's P(27015) = the next slot's
  host 27115 (move to P(27140)); -Slots 3 -> 4 + clamp at 4 (slot 4's fakeigd 19480 = slot 0's box); slot 0 took the lock before quick (take it
  lazily); an untrue NuGet comment. Fix S4, quick, gate 2, merge. merge=false (CLAUDE.md s8/s12 hunk): resolve by hand, keeping both.

## Next
1. When kits merges: launch the next wave ALL AT ONCE, build only (kits_v31.md section 8): lane A slice 3 (F5, F23 Lines, F6, F7), then
   slices 4-6 (6 = the 12 classes' kits by tier) in the same lane; lanes B (F21, F22, F24), D (F9), E (F13), F (Par.cs, numbers section 8),
   G (Squads.cs); WebRTC R2-R5 (network_webrtc.md); then lane I (items). Each: quick per job, opus code gate, merge in section 8's order.
2. TEST PHASE, once everything is merged: the slots proof, then quick,solo,solo,six,screens (+ six,six) across slots, fix low, then the bar
   once, VERIFIED:, push version-l and main, release; frames to the owner (the art lane's Drake, Rusty, siege, player ships).
3. Delete worktrees WarShips_wt_walls, WarShips_wt_net, WarShips_wt_test after the merges.

## Owner questions
- none open

## Notes
- Keep the version-l tree CLEAN while w3gvr44ua runs: its merge step refuses a dirty tree. Commit ledger edits at once.
- Merge risks: ClassArt.PdRing removed (kits) vs art rows; Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
