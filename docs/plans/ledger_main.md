# Coordinator state (CLAUDE.md section 4.6) -- updated 2026-09-25 (compact threshold now ~200k)

Engine outputs for the running batches: S = C:\Users\logan\AppData\Local\Temp\claude\C--Users-logan-Downloads-WarShips-Version-L\31bcb796-5d47-41e0-ad68-ba3af2cf375b\scratchpad
(tools\lanes.ps1 -Runs S). New batches write to %TEMP%\warships_rungs.

## Running (the old way, rungs per job: let them finish)
- wf_e2b4eda1-d12 / task wq9tnfn98, "lanes-walls-art". The result is {walls, gate, merge, art}.
  - walls: DONE, merged into version-l as 29b154e; `Unlocks.All` added to CLAUDE.md section 7.
  - art: the shockwave ring and camera repositioning, then J4 and J5. The camera must NEVER zoom past the player's max
    (COORDINATOR NOTE at the end of wt_art's ledger_sprites.md). Check for a zoom-out and replace it if found. Send the owner
    the frames it returns.
- wf_ab65d36a-eb3 / task w79l6jmmg, "lane-kits-slice2". The result is {kits, gate}. J5, then J6, J7 and the gate. Does not merge.
- wf_2778a53a-b3b / task wdwwml9cf, "lane-net-r1-prove". The result is {net, gate}. The first gate found a StructRow problem (fix round R1Q).
  Does not merge.

## Next (the new way: speed first, test at the end)
1. Merge net, kits and art into version-l, each once its gate passes.
2. Engine slots, one sonnet batch, so 2-4 engine runs can go at once:
   - per-slot TEMP and APPDATA;
   - a port base through tools\smoketest\run.ps1 into SmokeTest.cs.txt (27115/27125/27123/27124) and fakeigd.py's argv
     (19000/19080/19351);
   - rungs.ps1 slots: slot 0 is the legacy defaults, slots 1+ only for trees whose run.ps1 takes -PortBase.
3. The bar once, then push version-l and main.
4. Then docs/plans/README.md "Next": R2-R5, the rest of the class batch, curve/raids numbers, items.

## Owner questions
- none open

## Notes
- Merge risks: Hub.NetIdentity peak (walls) vs net; ClassArt.PdRing removed (kits) vs art rows; Ships.cs art rows vs kits rows.
- Do not commit to version-l files that the lanes edit while wq9tnfn98 can still merge. CLAUDE.md, tools\ and ledger_main.md are safe.
