# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE: no engine anywhere until every planned lane is merged -- owner; all agents Opus 5.5)
- wf_d703a267-73b / task w2aq1g0re, "lane-kits-a3to6": NOW SLICE 3 ONLY (WarShips_wt_kits; NOTE 4 there makes its slice-4 agent return
  blocked). Slice 3 build (J8 F5 done, J9 Lines running) -> gate -> fix -> gate 2 -> merge "lane A slice 3". Its end row: expect slice 3
  merged + a slice-4 "blocked (NOTE 4)" or "waiting" row: both fine. A slice 3 stop: fix it by hand/new batch.
- (launching) "lane-kits-parallel": slices 4 (after drives), 5 (after slice 3 + raids, fields, wings), 6a (after 4+5+curve), 6b/6c (after
  5+curve), 6d (after 3+4+curve), items (after curve), then items RECONCILE (after 6a-6d + items). Own worktrees wt_kits4.. / wt_items.
- wf_b63808ec-4ad / task wojzfsouf, "wave-1-build": six lanes in parallel, build only, each from base f168508 (= kits 809313c + version-l;
  branch wt/wave): drives (B: F21/F22/F24), fields (D: F9), wings (E: F13), curve (F: Par, boss rows, ladder), raids (G: Squads), net2
  (R2-R5 code; S2 first if missing). Worktrees WarShips_wt_<key>, ledgers ledger_<key>.md (net2: ledger_webrtc.md). Each: build, opus gate,
  one fix + gate 2, then SERIALIZED merge (merge version-l into the lane, quick, merge --no-ff; waits up to 5 min for a clean MAIN).
  Returns one row per lane. A lane with no merge: read its row, launch its fix or merge it by hand. Keep MAIN commits quick.

## Next
1. When lane-kits-parallel ends: every unit merged -> wait for wave 1 + net2, then the TEST PHASE (item 2). Else fix the row's unit.
2. TEST PHASE, once everything is merged (owner 2026-09-25: 3+ engine checks per class ability / drive row, CLAUDE.md 6.7; first an
   audit agent maps every ClassDef.Abilities and drive row to its checks and writes the missing ones): the slots proof, then quick,solo,solo,six,screens (+ six,six) across slots, fix low, then the bar
   once, VERIFIED:, push version-l and main, release; frames to the owner (the art lane's Drake, Rusty, siege, player ships).
3. Delete each lane's worktree once it is merged (walls, net, slots, art, test removed 2026-09-25; branches kept).
Done: KITS slices 1-2 MERGED a515479 (K4 9742789, K-merge b57862d with art + wave). ART MERGED (A5 7717267 passed gate 3's code; coordinator applied its one comment fix A6 761a2ad; quick art_mergeq green;
  engine-unproven; Drake throw now nose 1300 u off = 200 u past gun reach, tell the owner if asked). Slots merged 25aefc1 (gate 2 passed at a22c187; its 2 notes applied in e104a5a). Engine-unproven: its proof opens the test phase.

## Owner questions
- none open (owner: slices side by side, items alongside slice 6 with a reconcile pass, never skip a review)

## Notes
- Keep the version-l tree CLEAN while wthoe9tj1 / wojzfsouf run: their merge steps refuse a dirty tree. Commit ledger edits at once.
- Merge risks: whichever of kits/art merges second resolves ClassArt.PdRing (kits removed it) and Ships.cs mount literals (list: ledger_sprites J5 POST); Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
