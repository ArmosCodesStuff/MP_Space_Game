# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE: no engine anywhere until every planned lane is merged -- owner; all agents Opus 5.5)
- kits: gate 2 (wthoe9tj1) passed K3 97f767e but for ONE comment (SmokeTest.cs.txt ~7268 "an outpost's 42 ... used to sit" -> gate's wording).
  COORDINATOR DECISION: Dealt.Landed (static event on the damage door, for blows with no ship behind them) accepted. Next: a K4 comment fix,
  merge version-l in (art conflicts: ClassArt.PdRing, Ships.cs mounts), quick, merge into version-l -- workflow launching.
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
3. Delete each lane's worktree once it is merged (walls, net, slots, art, test removed 2026-09-25; branches kept).
Done: ART MERGED (A5 7717267 passed gate 3's code; coordinator applied its one comment fix A6 761a2ad; quick art_mergeq green;
  engine-unproven; Drake throw now nose 1300 u off = 200 u past gun reach, tell the owner if asked). Slots merged 25aefc1 (gate 2 passed at a22c187; its 2 notes applied in e104a5a). Engine-unproven: its proof opens the test phase.

## Owner questions
- none open

## Notes
- Keep the version-l tree CLEAN while wthoe9tj1 / wojzfsouf run: their merge steps refuse a dirty tree. Commit ledger edits at once.
- Merge risks: whichever of kits/art merges second resolves ClassArt.PdRing (kits removed it) and Ships.cs mount literals (list: ledger_sprites J5 POST); Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
