# Coordinator state -- read after a compact or at session start (CLAUDE.md section 2)

2026-09-25 ~02:30. version-l = docs and tools only since aa1e4f9 (VERIFIED, pushed).

## Running (the old way: rungs per job) -- let them finish
- walls (wt/walls): the 7 review findings are fixed, and the chain is green at 3ebce14. Now the opus gate, then the workflow merges
  wt/walls into version-l itself (haiku, aborts on a conflict). After it merges, add `Unlocks.All` to CLAUDE.md section 7's table list.
- art (wt/art): J3c/J3d proven green. Now building the shockwave ring scaling with BossSize, and the camera repositioning. The camera must
  NEVER zoom past the player's max; the owner's correction is COORDINATOR NOTE 1 at the end of the lane's ledger_sprites.md. Check the
  result for a zoom-out and replace one if found. Then J4 siege and J5 player ships.
- kits (wt/kits): J4 and job 1c proven. Now J5 (the hostile damage path, fixing 3 reds), then J6, J7 and the opus gate. Does not merge.
- net (wt/net): R1 all green at c4a7e01 (quick, 2 solo, six); now the opus gate. Does not merge.

## Next, in order (the NEW way: the owner's "speed first, test at the end", after the running work finishes)
1. Merge net, kits and art into version-l once their gates pass. Merge risks:
   - Hub.NetIdentity peak (walls) vs net
   - ClassArt.PdRing removed (kits) vs art rows
   - Ships.cs art rows vs kits rows
2. Engine slots: parallel engine runs. The scope is done (the engine-slots-scope workflow):
   - per-slot TEMP and APPDATA fix the scratch folders, user:// and the screens folders;
   - a port base must be threaded through tools\smoketest\run.ps1 into SmokeTest.cs.txt, for ports 27115, 27125, 27123 and 27124, and
     into fakeigd.py's argv (19000, 19080, 19351);
   - then tools\rungs.ps1 gets slots: slot 0 is the legacy defaults, slots 1+ only for trees whose run.ps1 takes -PortBase.
   Sonnet, one batch; proven by two chains running at once.
3. The bar once, then push to version-l and main.
4. Remaining plan items (docs/plans/README.md "Next"): R2-R5, the rest of the class batch, curve/raids numbers, items.
