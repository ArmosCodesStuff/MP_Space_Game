# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE: no engine anywhere until every planned lane is merged -- owner; all agents Opus 5.5)
- wf_604b8361-142 / task wthoe9tj1, "lane-kits-fix" (no engine). Kits gate 1 (wydvp93f5, HEAD 809313c: K1+K2 done, net merged in) failed on
  7: knife-edge DPS windows; screens frame 49 + a twin-barrel frame; outpost GunDamage 42 -> derive from the gunship row = 35 (COORDINATOR
  DECISION); Ramp stepped on every peer (guest-flown Ramjet broken) -> owner-stepped; BaseDefense/Shots bypass Dealt.Deal; D17's echo check
  unwritten; stale comments. Fix, gate 2, then merge (it merges version-l into the lane first if art got there first). merge=false: by hand.
- wf_b2cacf8b-53f / task w3yjvxmzy, "lane-art-fix2" (no engine). Art A4 (1f3a6ae) fixed gate 1's six (ram once per dash via Slot.Struck; Find
  from the hull; ring/approach/spawn checks); gate 2 failed on 3 false comments only. COORDINATOR DECISION: the Drake's throw standoff also
  measured from the hull (nose 1300 u off) so the target is 200 u outside gun reach again (it was 10 u inside after Find moved). Fix A5,
  gate 3, merge (syncs version-l first). Tell the owner the throw decision.
- wf_b63808ec-4ad / task wojzfsouf, "wave-1-build": six lanes in parallel, build only, each from base f168508 (= kits 809313c + version-l;
  branch wt/wave): drives (B: F21/F22/F24), fields (D: F9), wings (E: F13), curve (F: Par, boss rows, ladder), raids (G: Squads), net2
  (R2-R5 code; S2 first if missing). Worktrees WarShips_wt_<key>, ledgers ledger_<key>.md (net2: ledger_webrtc.md). Each: build, opus gate,
  one fix + gate 2, then SERIALIZED merge (merge version-l into the lane, quick, merge --no-ff; waits up to 5 min for a clean MAIN).
  Returns one row per lane. A lane with no merge: read its row, launch its fix or merge it by hand. Keep MAIN commits quick.

## Next
1. When kits (wthoe9tj1) merges: launch lane A slices 3-6 in WarShips_wt_kits, build only (kits_v31 section 8: slice 3 F5, F23 Lines, F6, F7;
   4 F8, F10; 5 F12, F11, F14, F19; 6 the 12 classes by tier 6a-6d, each ClassDef.Abilities in learn order). Then lane I (items) once curve merges.
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
