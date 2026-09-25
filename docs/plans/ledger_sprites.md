# Ledger: the SPRITES lane (worktree WarShips_wt_art, branch wt/art)

Spec: `sprites.md` (defaults approved), `README.md` rulings, `kits_v31.md` §8 lane H. Jobs in order:
1 enemies · 2 bosses · 3 fleet · 4 siege · 5 the 12 player ships (hull art only). Compile rungs only
in this lane; the main session owes rungs 3 and 4 (listed at the end).

## Decisions (where the spec is silent or the tree moved)

- **D1** The pack stays where the owner put it, `art_source/pack_2026-09-24/` (README.md names that
  path; sprites.md's `art_source/pack/` was a placeholder). `art_source/.gdignore` already covers it.
- **D2** sprites.md's slices 0 (pipeline) and 1 (foundations) are not separate commits: each
  foundation lands with the first job that uses it (the pipeline and `HullArt` with the enemies, and
  each table moves onto `HullArt` in the job that re-arts it). A contract with no user would fail
  `UNUSED ANYWHERE: 0`, and no engine run can prove a behaviour-neutral slice here anyway.
- **D3** `HullArt` is a BASE CLASS the art-carrying rows derive from (EnemyDef, ...), holding
  Texture, Length, Tint and Nozzles. The rows keep their `Texture = / Length = / Tint =` syntax and
  every `Def.Length` reader is untouched, so the lanes editing Raider/Boss/Ships.cs in parallel meet
  the fewest changed lines. Hit sizes stay each table's own rule (`HitShare`, `HalfWidth`, ...), so
  `HalfWidth` is not on `HullArt` (hit sizes unchanged, README ruling).

## Jobs

### J1 enemies -- PRE
- Intent: pipeline (`$Finished` rows in make_ships.ps1: finished art, turned, keel-trimmed; `-Single`
  and the raider blocks deleted; preview of every finished row), `HullArt` + nozzle plumes, the 6
  raider rows + the title Web re-arted (fighter_swept, frigate_b, fighter_tri_a, fighter_delta,
  gunship_h, frigate_d, crescent_a), MenuFoe always tints, TargetDummy reads the webifier row.
- Start: aa1e4f9285579087ac94f56cb9c02a9dea29700a
- Files (hash-object, 12): tools/make_ships.ps1 956db8137ac5 · scripts/Sprites.cs f46ae01e9188 ·
  scripts/Enemies.cs 9412635ea118 · scripts/Raider.cs 11393eb2aa45 · scripts/MenuFoe.cs 7e5b57f31db6 ·
  scripts/TargetDummy.cs b3f71152fb37 · scripts/Plume.cs bb0295cb5eaf · enemy_light_fighter.png
  f9eef8922055 · enemy_heavy_hull.png d1bb9f38d538 · enemy_talon_hull.png 21041294eefc ·
  enemy_pod_hull.png 03829ddc6607 · enemy_cross_hull.png a68996ef3579 · enemy_lancerkin_hull.png
  6f4272646697 · enemy_light_tier_2.png 3c36de7f947c · art_source/light_fighter.png d3390ac21a3a ·
  art_source/heavy_fighter.png b7e1c6e6b321 · tools/smoketest/SmokeTest.cs.txt 00e6f1badf3c ·
  tools/screens/Shots.cs.txt 809555bf44e0 · docs/DESIGN.md 8d15c0e26f08 · docs/CHANGES.md 3631a5f66e39

### J1 enemies -- POST
- Verdict: typecheck 0 errors; `-Quick` ALL CHECKS PASSED. Rungs 3/4 owed (below).
- Files: tools/make_ships.ps1 ($Finished rows + Finished/CoverAxis/TrimOn; -Single & co and the
  raider blocks deleted; preview draws finished rows with turrets and nozzle bars), scripts/Sprites.cs
  (HullArt, Nozzle, Fit(HullArt)), Plume.cs (Width), Enemies.cs (EnemyDef : HullArt; nozzles;
  per-row turret figures), Raider.cs (Fit(Def), DrawPlumes), MenuFoe.cs (always tints),
  TargetDummy.cs (webifier row), 7 enemy PNGs, 2 drawings -> retired/art/, SmokeTest (2 checks),
  Shots (frame 80 -> 80_every_enemy_hull), DESIGN.md Art, CHANGES.md.
- Commit: the J1 commit (hash = J2's Start).
- **D4** marks are placed in the NOSE-UP frame (px of the turned file, before the trim), not the
  source frame: x is across and y along the hull, readable against the turned art.
- **D5** Talon has 2 bells, not sprites.md's 3: the art (fighter_tri_a) shows two bells and a
  centre spike; swept 2 and delta 2 (their centre lobes are a spine and a tail). Literals in the check.
- **D6** Each Turret row names TurretAft/TurretWidth (the gunship's old figures stopped being
  every row's default); the tool prints them from a `turret` mark and a `housing` edge mark.
- **D7** Raider tints unchanged: the pack is LIGHTER than the old line art (mean L 0.57 vs 0.31
  webifier), so reds read brighter; retune only if frames 48/80 say so.
- Trap found: a run of make_ships rewrites the capitals' and turrets' PNGs with different bytes;
  `git checkout` them after a run unless their block changed (written into DESIGN.md Art).
- Next: J2 bosses.

### J2 bosses -- PRE
- Intent: BossType derives from HullArt (Sprite -> Texture, + Tint, + Nozzles listed); Rusty
  Bucket frigate_a (nose Right, L 360, tint 0.33/0.25/0.26), Drake flagship (nose Right, L 420,
  tint 0.52/0.35/0.29); HW 70 / 90 kept (the Drake's 310 stands); nozzles 2 / 5 listed, not drawn.
- Start: e00ba7a80178ef40106c20cf520a9ed8b0ea2e58 (= J1's commit)
- Files: tools/make_ships.ps1 5404844429ca · scripts/Missions.cs e1afdde80028 · scripts/Boss.cs
  28201da9b48b · boss_raider.png d01762233989 · boss_drake.png a0a3e4624364 ·
  tools/smoketest/SmokeTest.cs.txt 143c458d16de · tools/screens/Shots.cs.txt 21ca86be138b ·
  docs/DESIGN.md 80fc0d91e800 · docs/CHANGES.md 9d9a30a43907

### J2 bosses -- POST
- Verdict: `-Quick` ALL CHECKS PASSED. Rungs 3/4 owed.
- Files: make_ships.ps1 (2 rows), Missions.cs (BossType : HullArt; tints; nozzles), Boss.cs
  (Fit(Type)), boss_raider.png, boss_drake.png, SmokeTest (Trimmed helper; 1 new check, 2 rewritten,
  2 hand-made rows renamed), DESIGN.md, CHANGES.md.
- Commit: the J2 commit (hash = J3's Start).
- **D8** A boss's bells are listed on its row (2 / 5, the count check) but not drawn: bosses drew no
  flame before and the spec does not ask for one.
- **D9** Boss tints are sprites.md Q5's literals. On the new grey (mean L 0.65) they draw at ~0.65x
  the old average colour, i.e. darker; the brighter alternative is in CHANGES Known broken.
- Next: J3 fleet.
