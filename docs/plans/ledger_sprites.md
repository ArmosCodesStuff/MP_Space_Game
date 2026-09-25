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

## STOPPED after J2 (context past ~150k). J3-J5 go to a FRESH agent that reads this file.

### How to continue (for the next agent; no transcript needed)
- Tool: add rows to `$Finished` in tools/make_ships.ps1 (marks and nozzles in the NOSE-UP frame, D4),
  run `powershell -ExecutionPolicy Bypass -File tools\make_ships.ps1 -Preview %TEMP%\p.png`, then
  `git checkout -- battleship_hull.png carrier_player.png destroyer_hull.png turret_main.png turret_pd.png`
  (the line-drawing trap) until J5 replaces those blocks. The finished rows rebuild byte-identical.
- Nozzle candidates: a scratch stern-lobe finder (turn nose-up, per column the lowest alpha>0.5 px,
  runs within ~6% of the tail) found every bell so far; confirm the count by eye on a stern crop
  (centre lobes are often a spine or tail, not a bell: D5).
- A table moves onto `HullArt` by deriving from it (D3): delete its own Texture/Length/Tint fields,
  keep the row syntax, `Sprites.Fit(row)` tints, `row.DrawPlumes(ci, at, k, col, throttle, active, reach)`.
- Harness helper `Trimmed(texture)` (SmokeTest top) is the trim test for any HullArt row.
- J3 fleet per sprites.md §1/§2: hauler cargo_4 (Right? -- sprites.md: 90° CW, i.e. nose Left; L 200;
  6 PodCentre + PodSize on its frames, PD (0, 8) re-seated, Extent re-measured, its 3 inline nozzles
  -> 2 bells via DrawPlumes; Hauler.cs:301/:319 copies of Length/GetHeight go), miner drone_mining
  (nose Left, L 40, tint 0.63/0.46/0.31, beam emitter off 0.45 L onto the scoop; harness anchor
  `Gatherer.Length * 0.45f`), salvager drone_salvager (nose Right, L 40, tint 0.61/0.35/0.11),
  couriers drone_economy (nose Left, NEW file courier.png, L 22 -> 30 on all 4 Lanes rows, Q6),
  wing fighter interceptor_a (nose Left, L 17), bomber interceptor_b (nose Left, L 28.125, torpedo
  point off 0.45 L onto the pods' front). Frames 7, 8, 12-16, 27, 28-28e, 46, 59, 59b + a NEW courier
  close-up frame. Retire nothing from art_source (these had no drawings there).
- J4 siege: crescent_b (none, L 560 -> 483, HW 330) and drone_sensor (none, L 220 -> 300, HW 150);
  `EmplacementDef.Mounts` replaces `Guns`/`GunRing` (base 4 on the painted guns, pylon 1 at (0,0));
  retire art_source/pirate_base.png + pirate_pylon.png; NEW siege frame; check "the base has 4
  turrets on its Mounts, the pylon 1".
- J5 player ships (lane H): 12 ClassArt rows per sprites.md §1 table, hit sizes (HalfWidth) unchanged,
  BB mains on the 4 flanking twins (paint out the 6 painted twins' barrels: needs a Patch helper),
  PD on the aft domes; delete the carrier/battleship/destroyer line blocks + Hull()/CutOut/Seed/
  Dilate/Specks/Blank/Paper/Load if unused, retire their drawings, delete tools/finish_ships.ps1 and
  art_unused/art_4x (Q9), edit CLAUDE.md §7 + docs/README.md:197-198 in the same commit. Harness
  anchors: `EndsWith("battleship_hull.png")`, `x.Offset.Y > 150f`, `14.7f ... 36f`, `-110.06f`.
  CAUTION: the kits lane (WarShips_wt_kits) edits ClassArt mounts (F7 arcs, F16 PD 2): touch only
  Texture/Length-art/marks/nozzle lines, and say in the POST which mount literals moved.

### QUEUED (owner ruling via the coordinator, 2026-09-25): J3b boss red, right after J3
- The bosses are RED AND BLACK: the owner's swatch, mean RGB 172, 7, 2 = tint (0.67, 0.03, 0.01),
  R 160-180, "I like this and black probably". Replaces sprites.md Q5's boss tints (D9) on the Rusty
  Bucket and the Drake. If a pure multiply leaves the hull unreadable on space (art L ~0.57 -> reds
  ~0.38), keep hue and black and lift the value only as far as the frame needs; record the choice as a
  decision; the swatch literal stays in the check asserting the tint row. Rewrite every harness check /
  Shots frame asserting the old boss tints in the same commit. One commit, own PRE/POST; rung 4 owes
  frames 38, 42, 62-66. Then J4, J5.

### J3 fleet -- PRE
- Intent: 6 `$Finished` rows (cargo_4 hauler, drone_mining miner, drone_salvager salvager,
  drone_economy courier.png NEW, interceptor_a wing fighter, interceptor_b bomber); GathererDef,
  LaneDef, WingDef derive from HullArt; the Hauler's const/sprite/inline nozzles become a HullArt
  row; pods, PD seat, Extents, the gatherer emitter and the bomber's launch point re-measured onto
  the art; couriers L 22 -> 30 (Q6); gatherer tints (Q5). Turrets and outposts keep their art
  (sprites.md "Keep their current art"; the outpost is not in the mapping).
- Start: 274b8c52cc1050ec777b0615a2a8e7e7d8939aea
- Files: tools/make_ships.ps1 9016f81d1e1d · scripts/Hauler.cs bbc72c9e989c · scripts/Gatherer.cs
  353cb0d9493c · scripts/Lanes.cs 0125e73ad6c5 · scripts/ShipClasses.cs cee7908cf855 · hauler.png
  60afbc3808f5 · miner.png 021073f18ae9 · salvager.png d252d80d523a · wing_fighter.png 0f19fee70330 ·
  wing_bomber.png d0dd0bc86114 · courier.png (new) · tools/smoketest/SmokeTest.cs.txt f41305f68b4a ·
  tools/screens/Shots.cs.txt 21ca86be138b · docs/DESIGN.md 998180e8cf2c · docs/CHANGES.md 3dc0c6bc85c0

### J3 fleet -- POST
- Verdict: typecheck 0 errors; `-Quick` ALL CHECKS PASSED. Rungs 3/4 owed (below).
- Files: make_ships.ps1 (6 rows; the preview crosses every non-turret mark), Hauler.cs (`Hauler.Art`
  row, const Length gone, pods/PD/HalfWidth off the art, DrawPlumes, `_fit`), Gatherer.cs
  (GathererDef : HullArt + Emitter + HalfWidth; const Length gone), Lanes.cs (LaneDef : HullArt,
  courier.png, L 30, CourierBells), ShipClasses.cs (WingDef : HullArt + Launch; torpedo from the
  pods, alternating), hauler/miner/salvager/wing_fighter/wing_bomber.png, courier.png (new),
  SmokeTest (2 new checks, 3 rewritten readers, 1 message, 1 comment), Shots (16b reads the row),
  DESIGN.md (fleet art; the stale Fighter/Bomber bullets gone), CHANGES.md.
- Commit: the J3 commit (hash = J3b's Start).
- **D10** No new courier frame: 16b/16c (added after sprites.md was written) show a courier at zoom 3.
- **D11** Gatherer/courier bells are their three stern thruster blocks (the same drawing in all three
  drones); the miner's legs reach aft of them, so the fleet check asserts "aft of centre", not the
  raiders' "aft of 0.4 L". Fighter 5 bells (sprites.md allowed 1; counted off the art, D5).
- **D12** Hauler PD seated on the bow dome (0, -85.94): the only round turret-like seat on cargo_4;
  its Extent half-width is the cargo frames' outer rails (40.94), not the engine wings (46.3).
- **D13** Bomber `Launch` is the port pod's front (-5.30, -4.39); rounds alternate pods by Ammo parity.
- **D14** Gatherer emitters: miner between its two scoops (0, -17.14), salvager in its claws' mouth
  (0, -13.36); the unloading load leaves from the same point.
- Lane overlap: ShipClasses.cs is lane E's (wings) -- touched: the WingDef header (derives from
  HullArt, +Launch), the two rows (+Nozzles, +Launch lines, the comment), `Sprites.Fit(Def)` in Init,
  the torpedo launch (2 lines), `_Draw`'s plume (1 line, `len` local gone). No seat of a class moved.
- Rung 3 owed: new "the fleet wears the pack ..." and "a torpedo leaves from a pod's front ..."
  (two seeds each); rewritten "two craft may share one pad" (Def.Length; the miner's Extent 12 ->
  14.2 must stay under the courier's 16.9 u along the face), "salvager's beam is a narrow scan"
  (Emitter), "turning on the way down, it clears the station" (Art.Length), "fighters 17 u, bombers
  28.1 u", "a parked bomber sits at 65% on the white deck" (12 u now, was 18).
- Rung 4 owed: 7, 8, 12-16, 16b, 16c, 27, 28-28e, 46, 59, 59b -- pods on the frames, PD on the bow
  dome, flames on the bells (hauler 2, drones 3, fighter 5, bomber 2), gatherer tints, courier size.
- Next: J3b boss red.

### J3b boss red -- PRE
- Intent: both boss rows wear the owner's swatch red (0.67, 0.03, 0.01) as one named colour in
  Missions.cs; the two harness checks asserting the old tints rewritten to the swatch literal; DESIGN
  and CHANGES (J2's reversed tint text and its Known broken alternative) brought to what is true.
- Start: daa98516eef31182a14c54e0f0476e702358e67e
- Files: scripts/Missions.cs 7f87c9aac007 · tools/smoketest/SmokeTest.cs.txt 665da2c3cfa1 ·
  docs/DESIGN.md e31b47441acc · docs/CHANGES.md 5f4f47e9da77

### J3b boss red -- POST
- Verdict: `-Quick` ALL CHECKS PASSED. Rungs 3/4 owed.
- Files: Missions.cs (private `BossRed` (0.67, 0.03, 0.01) on both rows), SmokeTest (2 checks
  rewritten to the swatch literal, their messages "the owner's red"), DESIGN.md (Art bosses; the
  Rusty line in the balance list), CHANGES.md (J3b entry; J2's reversed tint text and its 0.65x
  Known-broken alternative deleted).
- Commit: the J3b commit.
- **D15** Pure multiply, no lift. Rendered on the game's space colour (11, 15, 24) with a scratch
  tint tool: both hulls read, hull mean RGB 111, 5, 2, highlights near the swatch (R ~160-170) and
  shadows black -- the "red and black" asked for. The lift that keeps the hue, (1.0, 0.045, 0.015),
  puts the hull's MEAN at the swatch (166, 7, 2) but loses black; it is CHANGES' Known broken
  fallback, one line (BossRed).
- Rung 3 owed: "a boss wears the owner's red on the pack's grey art, trimmed, its bells listed";
  "level 2 is the Drake Bastion ... in the owner's red" (two seeds each).
- Rung 4 owed: 38, 42, 62-66 by eye -- is the red hull readable on space and under the arena's effects?

## STOPPED after J3b (coordinator: the owner is re-picking the sprite mapping). J4 and J5 NOT started.
- The J1-J3 mappings stay as built; the main session re-maps from the owner's picks (each is one
  `$Finished` row + the row's Texture in its table; marks and nozzles re-measured by the tool).
- J4/J5 notes above ("How to continue") still hold, with one correction for J4: the tree moved --
  `EmplacementDef` now has ONE `Gun` on a mount at its centre (the base's cruise launcher), no
  `Guns`/`GunRing`, and the base "carries NO GUNS" by design, so sprites.md's `Mounts[4]` is obsolete;
  J4 is a re-art (Sprite -> HullArt Texture, Main -> Tint, L 560 -> 483 / 220 -> 300). Check
  `Combat.KeelCovers` first: with L <= 2 HW both hulls are circles of HW, so the length change should
  not move a hit shape. Lane F (curve) edits the siege rows' numbers in Emplacements.cs.

## RESUMED 2026-09-25: the owner re-chose the mapping (binding). Carrier carrier_a (pirate-carrier
spare carrier_b), Wraith fighter_unit_a, Pod drone_sensor (the pylon too), Miner drone_salvager (the
salvager too), wing fighter fighter_delta, wing bomber fighter_g. Now unused: drone_mining,
interceptor_a/_b (+ the old spares cargo_1, fighter_tri_b). Jobs: J3c re-map, J4 siege, J5 players.

### QUEUED (coordinator, owner ruling 2026-09-25): J3d bosses TWICE AS BIG, right after J3c
- Default 2x (owner said "2 or 3x"): Rusty Bucket L 360 -> 720, Drake 420 -> 840, the multiplier ONE
  data value on the boss rows (3x = one number). HIT SIZE scales too (HW 70 -> 140, 90 -> 180): the
  "hit sizes unchanged" rule no longer applies to bosses. Everything reading a boss's length or
  half-width follows the row (nose beam/burn origin L/2, the Drake's throw/tractor/rock points, escort
  places, arena spawn distance and stand-offs, boss bar, camera/frames). Rewrite every check/frame
  asserting old boss sizes (the "310" literal: find what it measures, rewrite from the 2x literals,
  never loosen). Arena too small for an 840 u Drake -> a decision + report, not a shrunk ruling.
  Owes frames 38, 39, 42, 62-66 and the rung-3 boss checks. Then J4, J5.

### J3c re-map -- PRE
- Intent: the Pod row onto drone_sensor (Up), the miner onto the salvager's art (one `$Finished` row,
  one file both gatherer rows load; miner.png/salvager.png deleted), wing fighter fighter_delta (Up),
  bomber fighter_g (Right) with its torpedoes on fighter_g's own weapon points; bells counted off the
  art; every check/frame asserting the old rows rewritten; sprites.md mapping tables to the picks.
- Start: a73d26e5f15572b0c3a4e492b5ad17b779bdecdc
- Files: tools/make_ships.ps1 09c21642629b · scripts/Enemies.cs 6fe7b4fe523e · scripts/Gatherer.cs
  fb719ec94158 · scripts/ShipClasses.cs a8e655389a75 · enemy_pod_hull.png 3d1e12eeac69 · miner.png
  34781fb6ffb1 · salvager.png cffc7ca41279 · wing_fighter.png 3add16dc0d2e · wing_bomber.png
  68879b26df28 · gatherer.png (new) · tools/smoketest/SmokeTest.cs.txt 6b232c654031 ·
  tools/screens/Shots.cs.txt c1bda594db98 · docs/plans/sprites.md 8fe86d01207e · docs/DESIGN.md
  d2350aeb60b7 · docs/CHANGES.md 0fe396740a87

### J3c re-map -- POST
- Verdict: typecheck 0 errors; `-Quick` ALL CHECKS PASSED. Rungs 3/4 owed.
- Files: make_ships.ps1 (Pod row drone_sensor; ONE gatherer row -> gatherer.png; wing rows
  fighter_delta / fighter_g), Enemies.cs (Pod 1 bell), Gatherer.cs (Drone/DroneBells/ClawMouth/
  DroneSpan shared by both rows), ShipClasses.cs (wing bells, Launch on the rails, `pod` -> `rail`,
  the art comment), enemy_pod_hull/wing_fighter/wing_bomber.png, gatherer.png (new), miner.png +
  salvager.png deleted, SmokeTest (3 checks rewritten), sprites.md (§1 picks + §2 rows), DESIGN.md,
  CHANGES.md (J3c entry; J1/J3 entries brought to what is true now).
- Commit: the J3c commit (hash = J3d's Start).
- **D16** Pod bells: ONE, the stern vent box (x 111.5-148.5, aft rim y 241 of drone_sensor): the round
  drone's eight pods are sensor lenses, not bells. The raider check asserts bells {2,2,2,1,2,2}.
- **D17** Bomber torpedo points = fighter_g's WINGTIP RAILS' front (port (16.5, 232) nose-up px ->
  (-8.48, -0.12) u), the art's longest, symmetric weapon points; the inner guns (+-40 px, 2.5 u) are
  the alternative. Check bound 7.2-9.8 u (8.5 +- the old 1.3 u tolerance), not on an edge.
- **D18** One pack file on two rows is ONE game file named for what both rows are (`gatherer.png`),
  its marks shared by name; the pack's unused files (drone_mining, interceptor_a/_b, ...) stay in
  art_source/pack (the owner's delivery, D1) -- "art nothing loads" meant the game's miner/salvager.png.
- Parked bomber: fighter_g is 298 x 456 px -> 11.95 u across at 65% (was 12): the deck check holds.
- Rung 3 owed: rewritten "the fleet wears the pack ..." (fighter 2 bells; one drone, two tints),
  "a torpedo leaves from a wingtip rail's front, not the nose" (7.2-9.8 u), "every raider's art ...
  flames from its own bells" (pod 1); unchanged but re-read: "two craft may share one pad" (miner
  Extent 14.2 -> 11.2), "salvager's beam is a narrow scan", "a parked bomber sits at 65% ...".
- Rung 4 owed: 0 (title foes), 12, 13, 16b, 16c, 27, 28-28e, 46, 48, 59, 59b, 80 -- the pod's one
  flame at its vent, both gatherers as the claw drone in their tints, the fighter's 2 and the
  bomber's 2 flames, torpedoes off the wingtips.
- Next: J3d bosses 2x.

### J3d bosses 2x -- PRE
- Intent: `Missions.BossSize = 2` (the one number) on both boss rows: Length 720 / 840, HalfWidth
  140 / 180, bells `Nozzle.Scaled`. Every hull-relative figure follows the row at use (Boss.cs): the
  ram's lane = the hull's own beam (Width 0 -> 2 HW); the rock held HW + Radius + Offset (Offset the
  40 u gap, was the whole 310); EscortOut and WarpRing in HALF-WIDTHS (13/7 and 1.3: 130 and 117 at
  1x); stand-offs (HoldOff, a warp's Standoff) measured from the NOSE (+ L/2 at use; rows 470 / 440,
  390, 1090 keep the 1x fight); the boss spawns with its nose on ArenaCentre. Shockwave 340 u NOT
  scaled (a reach, not a place): flagged. Frames 38/39/42 and 63-66 place the ship off the nose and
  zoom (ZoomLevel) to fit the hull. Harness: every boss-size literal rewritten to the 2x truth.
- Start: e7f5a1c337f46a767086821bb61854515f1b2012
- Files: scripts/Missions.cs e65a4d72251b · scripts/Sprites.cs 41b1b7ff5e0c · scripts/Boss.cs
  fd5a56a1713d · scripts/Lancer.cs 08ac04023b42 · scripts/Drake.cs 15db8b36bfee · scripts/Hub.cs
  b1e90e72868d · tools/make_ships.ps1 684959f04012 · tools/smoketest/SmokeTest.cs.txt aa9a7d98ad22 ·
  tools/screens/Shots.cs.txt c1bda594db98 · docs/plans/sprites.md 948f934fb777 · docs/DESIGN.md
  a66ea4812a9f · docs/CHANGES.md a071703d8ebc

### J3d bosses 2x -- POST
- Verdict: typecheck 0 errors; `-Quick` ALL CHECKS PASSED. Rungs 3/4 owed.
- Files: Missions.cs (private const BossSize 2; rows x it; HoldOff from the nose: default 470, Drake
  440), Sprites.cs (`Nozzle.Scaled`), Boss.cs (Approach/warp stand-offs + L/2; warp ring and escorts x
  HalfWidth; ram lane 2 HW; `FlankHold`; Scaled no longer scales Offset), Lancer.cs (EscortOut 13/7,
  ram Width gone), Drake.cs (Standoff 390 / 1090, WarpRing 1.3, Offset 40), Hub.cs (the boss's nose on
  ArenaCentre), make_ships.ps1 (comment), SmokeTest (row checks, 2 ram-lane finders, escorts 260 u,
  2 lock pushes, level-2 840, live shotgun/throw, guest rock hold), Shots (FitBoss zoom + ship off the
  nose for 38/39/42 and 63-66), DESIGN.md, sprites.md, CHANGES.md.
- Commit: the J3d commit (hash = J4's Start).
- **D19** Stand-offs are measured from the NOSE (gap + L/2 at use), each row's gap chosen so the 1x
  fight is unchanged (650 from the centre -> 470 / 440; 600 -> 390; 1300 -> 1090). Centre-measured,
  the doubled Drake's shotgun would land its nose 180 u from the pilot and its 10-degree fan lines
  ~31 u apart there (68 u at 1x): an undodgeable fan. The guns' Find (900 / 1100) still reach the
  hold-off (830 / 860 from the centre).
- **D20** Formation places scale with the hull (EscortOut 13/7 and WarpRing 1.3 HALF-WIDTHS: the row
  comments said "HalfWidth + 60" and "HalfWidth x 1.3"; "+60" at 2x would put the 45-degree escorts
  1.4 u off the flank, so the share keeps the formation's shape instead). The ram lane is the hull's
  beam (Width 0, like the throw's body lane). The rock's hold is HW + body + a 40 u gap (FlankHold).
- **D21** NOT scaled (reaches, not places): the beam's 70 u width, the shockwave's 340 u ring (now
  under the 720 u hull's bow and stern: Known broken), ranges, bodies. The arena is open space (no
  bounds), so there is room to dodge; but a pilot's widest zoom (0.588, ~918 u toward the boss) does
  not show the Drake's whole hull at its hold-off -- the zoom-out limit is the owner's call.
- **D22** The boss spawns with its NOSE on ArenaCentre (centre + L/2 north), so a bigger boss starts
  no nearer the party (1179 u nose to the first ship's spawn).
- Rung 3 owed (two seeds): the boss row checks (hull/art/approach 720/140 & 840/180, bells at the
  stern, beam/ram/shotgun/throw rows, the mixed row 720), "two escorts ... 260 u out", the two lock
  pushes, the ram-lane timing (280 u), "level 2 is the Drake Bastion ... 840 u", the live shotgun
  (nose standoff 394, ring 234) and throw (hold 180 + 181.8 + 40), the guest's rock hold; and every
  other boss-fight check (the capsule doubled: a shot that grazed past may now hit).
- Rung 4 owed: 38, 39, 42, 62-66 -- the whole hull in frame (zoom ~0.47 / ~0.38), telegraphs.
- Next: J4 siege.

## STOPPED after J3d (context past ~150k). J4 and J5 go to a FRESH agent that reads this file.
- Every "How to continue" note above still holds, with the owner's picks: J4's pylon is
  `drone_sensor`, the SAME pack file as the Pod (J3c) -> ONE game file for both rows (D18): rename
  `enemy_pod_hull.png` in the same edit to a name true of both (the art: e.g. `drone_sensor.png`),
  one `$Finished` row, Enemies.cs Pod row + Emplacements.cs pylon row load it; `pirate_pylon.png`
  and `pirate_base.png` deleted when nothing loads them. The Pod's bell is (-0.21, 23.27) at 52 u; the
  pylon (L 300) lists its bell off the same art x 300/52 if emplacements draw none (check first).
- J4 correction (above) stands: `EmplacementDef` has ONE `Gun` at its centre; no `Mounts`.
- J5: Carrier `carrier_a` (90 deg CCW, MEDIUM confidence: look at the turned art before building),
  Wraith `fighter_unit_a` (90 deg CCW); sprites.md §2's carrier figures were read off carrier_b and
  must be re-measured. Kits lane: `ClassArt.PdRing` is gone on its branch -- do not use it; touch
  only Texture/Length-art/marks/nozzle lines; list every seat moved in the POST.
- Boss size is now `Missions.BossSize` (J3d): nothing in J4/J5 should read boss lengths.

### OWED to the main session (nothing here has run above rung 2)
- Rung 3 (`tools\smoketest\run.ps1 -Solo`, two seeds for the new checks):
  - new "the title screen's foes wear their raider rows' tints, the Web on its own art"
  - new "every raider's art is trimmed to its drawing and flames from its own two bells at the
    stern; the gunship is drawn 136 u"
  - new "a boss wears its tint on the pack's grey art, trimmed, its bells listed"
  - rewritten "a boss's hull, art and approach are its row's" and "level 2 is the Drake Bastion"
    (+ tint); the hand-made "post" and "mixed" boss rows (Texture)
  - every existing raider/boss check (sizes, HitShare 0.4/0.3, gunship = 4 x webifier) should stay
    green untouched: a red one there means the art or a row is wrong, not the check.
- Rung 4 (`tools\screens\run.ps1`), read by eye: 0_main_menu (title foes + the Web's red crescent),
  48_raiders_pinning, 49_heavy_waiting_missile, 50_base_defence, 53_raid_incoming,
  80_every_enemy_hull (renamed from 80_new_enemies: all six rows; turrets on seats, a flame per
  bell), 38/39 arena telegraphs, 42_boss_bar_chunk, 62_arena_loot, 63-66 Drake frames. Look for:
  turrets on their painted seats, flames on the bells, tint brightness (raiders read BRIGHTER, D7;
  bosses DARKER, D9).
- J3 and J3b: the rung-3 checks and rung-4 frames each owes are listed in its POST above.
