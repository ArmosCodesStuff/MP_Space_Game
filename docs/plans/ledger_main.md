# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (the owner compacts at will; the SessionStart hook re-injects this)

Engine outputs for the old-way batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(their summary.txt is UTF-16: read with powershell Get-Content). New batches write to %TEMP%\warships_rungs.

## Running (BUILD PHASE: no engine anywhere until every planned lane is merged -- owner; all agents Opus 5.5)
- wf_05b8e6fa-3d3 / task wwls6zsfr, "lane-kits-parallel" (relaunch; script lane-kits-parallel-wf_777978ea-8f6.js, no waits): slice 4 (new
  wt_kits4) and slice 5 (wt_kits5, fresh) now; items: gate 2's two record lines, then merge; 6a after 4+5, 6b/6c after 5, 6d after 4 (own
  worktrees); then the items RECONCILE after all. Each: build, gate, one fix + gate 2, serialized merge. Returns 8 rows; a stopped unit gets a
  fix batch, and units it skipped are relaunched after it.

## Next
1. When lane-kits-parallel ends: every unit merged -> wait for wave 1 + net2, then the TEST PHASE (item 2). Else fix the row's unit.
2. FOLLOW-UPS batch once every lane is merged (before the test phase): the fingerprint's List/Dict tables; the heavy's zero lead
   after a retarget; the host checks a guest's warp COOLDOWN too (drives: it prices distance only); raider cruise 100 vs BB 88 / CV 99 (note only).
3. TEST PHASE, once everything is merged (owner 2026-09-25: 3+ engine checks per class ability / drive row, CLAUDE.md 6.7; first an
   audit agent maps every ClassDef.Abilities and drive row to its checks and writes the missing ones): the slots proof, then quick,solo,solo,six,screens (+ six,six) across slots, fix low, then the bar
   once, VERIFIED:, push version-l and main, release; frames to the owner (the art lane's Drake, Rusty, siege, player ships).
4. Delete each lane's worktree once it is merged (walls, net, slots, art, test removed 2026-09-25; branches kept).
Done: WAVE 1 ALL MERGED: wings 9b05fd3, net2 23d5b29 (R2-R5), raids f2f398b (gate 3 passed); worktrees removed.
  KITS SLICE 3 MERGED 25e27d0 (gate 1 failed, gate 2 passed; D24 shot rows land with their classes in 6a-6d, D27 no
  Overcharge -- README ruling added). Wave: drives 8dcc845, fields, curve merged; wings, raids, net2 still running. Worktrees kits,
  drives, fields, curve removed. KITS slices 1-2 MERGED a515479 (K4 9742789, K-merge b57862d with art + wave). ART MERGED (A5 7717267 passed gate 3's code; coordinator applied its one comment fix A6 761a2ad; quick art_mergeq green;
  engine-unproven; Drake throw now nose 1300 u off = 200 u past gun reach, tell the owner if asked). Slots merged 25aefc1 (gate 2 passed at a22c187; its 2 notes applied in e104a5a). Engine-unproven: its proof opens the test phase.

## Owner questions
- none open (owner: slices side by side, items alongside slice 6 with a reconcile pass, never skip a review)

## Notes
- A wait agent cannot wait long (the harness forces an early answer): express waits as promises inside ONE workflow.
- Items defaults to report: D-J8a web hold round 2 s (Web Breaker cuts each round's hold); raids' adds scaling HullShare/DamageShare.
- Keep the version-l tree CLEAN while wthoe9tj1 / wojzfsouf run: their merge steps refuse a dirty tree. Commit ledger edits at once.
- Merge risks: whichever of kits/art merges second resolves ClassArt.PdRing (kits removed it) and Ships.cs mount literals (list: ledger_sprites J5 POST); Ships.cs art rows vs kits rows; kits/art vs net's SmokeTest.cs.txt and Net.cs.
- Follow-up from net gate 2 (not blocking): these constant tables are Lists or Dictionaries, so the fingerprint never hashes them:
  NetIds.Widths, Spawns.All (the wire kind index), Hints.All, Sfx._gap and Abilities.Reserved. The fix is to make them arrays or row tables.
- Follow-up (kits NOTE 2): a heavy throws with zero lead right after a retarget (game side, latent); not fixed.
- Net fingerprint: a delegate field is hashed only as its type name, so a changed WaveCrew.Count rule is not compared (accepted, documented).
