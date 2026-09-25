# SPRITES PLAN: the 34-sprite pack onto every ship in the game (for sign-off)

Merges `sprite_orientation.md`, `sprite_refs.md` and `sprite_mapping.md`. Read-only: no repo edit, no engine,
no compile. I re-checked every load-bearing claim against the tree, re-measured all 34 sprites the way the tool
will trim them, and measured today's art the same way (`sprites/plan_measure.ps1` → `plan_measure.txt`,
`sprites/plan_tint.ps1` → `plan_tint.txt`).

**Scope: rows, plus 2 foundations.** 29 sprites become game textures, 5 are spares. Every entity is a row edit on a
table that already exists. First, two foundations:
- **`HullArt`**, one struct shared by 7 hull tables and 2 consts (texture, length, half-width, tint, nozzles).
- **`EmplacementDef.Mounts`**.

Art enters through a finished-art table added to `tools/make_ships.ps1`.

Line numbers are as of this writing. **Another session is editing the tree right now** (Sky, Drake, SmokeTest,
Shots, docs, verify.ps1), and SmokeTest lines moved by 15 while I worked. Each check is also given with a
grep anchor.

---

## 0 · Corrections to the three drafts (verified)

| # | draft said | true | evidence |
|---|---|---|---|
| 1 | "Every hull comes out beamier" (BB 1.8x) | **False.** The draft compared the new sprite's half-SPAN with today's `HalfWidth` MARK. Comparing drawn span with drawn span: BB 164 → 163 u, the other capitals up to 23% wider, freighters 10-31% narrower, player heavies and lights −23% to +26%, enemy heavies 34-59% narrower, enemy lights 30-94% wider. If `HalfWidth` is kept (Q1), **no collider changes** | `plan_measure.txt` |
| 2 | a nose-left sprite needs a mirror first | **Rotate only:** 3 × `TurnLeft`. Mirroring flips the lettering and the asymmetric hulls (flagship, unit_d) | make_ships.ps1:112-123 |
| 3 | `Trim` keeps the keel centred | It centres the IMAGE (`m = Min(x0, W-1-x1)`). The keel lands off the texture's centre column by **2.0 u on carrier_b** (its runway and landing point), 0.9 u on the flagship and 0.8 u on unit_d. Needs a keel mark | make_ships.ps1:360-368 |
| 4 | pirate base "Length ≈ 444 keeps today's 330" | That figure used the untrimmed height and compared against `HalfWidth`. Trimmed, **L 483** gives today's drawn width of 746 u; L 444 would draw only 686 u. **Pylon:** at L 220 it draws 222 u across, so HW 150 would overhang the art by 40 u. **L 300** fits HW 150 | measured |
| 5 | BB: "keel turrets for the fore and aft pairs" | The art has only **2** keel twins, and both are forward of centre. The only seats that come in two pairs are the **4 flanking twins** (Q3) | grid_battleship_bb05.png |
| 6 | "five places repeat Length / GetHeight", counting the wing shadow | The wing shadow reads the sprite's own scale. The copies are Sprites.cs:11, PlayerShip.cs:276, Hauler.cs:301 and :319, and CharacterCreator.cs:201 | grep |
| 7 | (not raised) | **No screenshot frame shows the siege** (base or pylon). No frame shows a courier at a readable size either: in 44 it is about 2 px. `LINT` walks `Control`s only | Shots.cs.txt:27-41 |
| 8 | (not raised) | **The siege is the only under-resolved art.** At L 483 the base has 0.75 texture px per world unit, the pylon (L 300) 0.83. The deepest play zoom is 1.35 screen px per unit (Hub.cs:231), so both get upscaled 1.6-1.8x (today's base: 1.2x). Every other row has at least 1.68 px/u | measured |

Confirmed as the drafts had them:
- the orientation of all 34 (checked on the contact sheet);
- the 27 textures in 25 files;
- the 6 consumers that never tint;
- 9 single-flame plume call sites, plus the Hauler's 3 inline nozzles;
- the no-argument trap in make_ships (its lines 512-553 rebuild 5 hulls from `art_source/`);
- `-Single` running Paper → Levels → CutOut;
- `Cut()` reading lightness from RGB even under alpha 0, which ships the flagship's watermark unless that lightness is zeroed;
- the Drake literal at Drake.cs:70 (310 = HW 90 + 180 + 40);
- the stray form feed at README.md:195;
- `art_4x/`: 13 MB, unreferenced, and imported by Godot because it has no `.gdignore`.

---

## 1 · THE MAPPING TABLE

Rotation means the turn that points the nose up in the file. **Drawn width** is the full drawn width in world
units: today's art at today's Length, then the new art at its final length.

**The owner's picks, 2026-09-25** (binding; they replace this table's first draft on six rows): Carrier
carrier_a, Wraith fighter_unit_a, Pod drone_sensor (the pylon's art too; the row tints tell them apart),
Miner drone_salvager (the salvager's art too: one file, `gatherer.png`), wing fighter fighter_delta, wing
bomber fighter_g. carrier_b is the pirate-carrier boss spare. ≈ widths on those rows are the untrimmed art's.

### Player classes (tinted by the pilot's Main)

| entity | sprite | rotation | final L | drawn width (u) | why |
|---|---|---|---|---|---|
| Battleship | battleship_bb05 | 90° CW | 378 | 164 → 163 | Named for it. 6 painted twins; 4 of them seat the 4 mains, and two aft domes seat PD x2 |
| Carrier | carrier_a | 90° CCW (medium confidence: checked against the art in its slice) | 283.5 | 113 → ≈ 138 | The owner's pick |
| Destroyer | destroyer_dd22 | 90° CCW | 212.625 | 82 → 101 | Named for it. 2 keel mounts = the Director battery; VLS cells and racks aft = Long Lance and depth charges |
| Freighter | cargo_2 | 90° CW | 230 | 176 → 125 | 4 detachable hex pods read as its sentries |
| Tender | cargo_3 | 90° CW | 230 | 169 → 116 | The claw at the bow is where the mending lance comes from |
| Bastion | frigate_c | 90° CCW | 230 | 145 → 130 | An armoured box hull with 2 heavy twins: a gun-block, not cargo |
| Sniper | fighter_unit_c | 90° CCW | 120 | 88 → 68 | The long needle prow is the rail barrel |
| Warrior | fighter_unit_b | 90° CW | 120 | 93 → 80 | Two prongs reach past the nose: the blade |
| Warden | fighter_e | 90° CW | 120 | 84 → 88 | A 2×5 grid of cell hatches for the hunters. Also the art for the v2 decoy |
| Dart | fighter_unit_d | 90° CW | 70 | 49 → 40 | A spike ahead of a small, fast body: the rod |
| Echo | fighter_f | 90° CW | 70 | 43 → 55 | Paired guns on both wings: "fires twice" |
| Wraith | fighter_unit_a | 90° CCW | 70 | 44 → ≈ 54 | The owner's pick |

### Enemies (row tint)

| entity | sprite | rotation | final L | drawn width (u) | why |
|---|---|---|---|---|---|
| Webifier (also the target dummy, the title-screen lights and the Rusty escorts) | fighter_swept | none | 34 | 19 → 38 | The plainest small raider; its wingtip prongs read as web emitters |
| Talon | fighter_tri_a | none | 40 | 23 → 31 | Three forward spikes |
| Pod | drone_sensor | none | 52 | 35 → 52 | The owner's pick: a round drone, one stern vent (its one bell) |
| Gunship (also the title-screen heavies) | frigate_b | 90° CW | 136 | 96 → 63 | One painted twin plus a missile rack: its kit |
| Cross | gunship_h | 90° CW | 120 | 104 → 63 | frigate_b's hull (IoU 0.80) with the rack stripped |
| Lancerkin | frigate_d | 90° CW | 150 | 100 → 41 | The slim missile boat, 8 tubes on its spine |
| Title-screen Web | crescent_a | 180° | 34 (the webifier's) | ≈17 visible → 60 | Reads apart from the webifiers by its shape (Q10) |

### Bosses (need a tint)

| entity | sprite | rotation | final L | drawn width (u) | why |
|---|---|---|---|---|---|
| Rusty Bucket | frigate_a | 90° CCW | 360 | 174 → 190 | A rounded "bucket" bow, with turrets bolted on beside cargo |
| Drake Bastion | flagship | 90° CCW | 420 | 191 → 217 | The largest and most detailed sprite |

### Siege (tinted `Main`)

| entity | sprite | rotation | final L | drawn width (u) | why |
|---|---|---|---|---|---|
| Pirate base | crescent_b | none | **560 → 483** | 746 → 746 | The owner's rule: "the crescent hull is the pirate base". Its horns point forward |
| Shield pylon | drone_sensor | none | **220 → 300** | 306 → 303 | A round generator; HW 150 is its radius |

### Fleet and wing

| entity | sprite | rotation | final L | drawn width (u) | why |
|---|---|---|---|---|---|
| Hauler | cargo_4 | 90° CW | 200 | 71 → 93 | Exactly 6 cargo frames, 3 a side, for the code's 6 pods |
| Miner | drone_salvager | 90° CCW | 40 | 27 → 23 | The owner's pick: the salvager's drone in the miner's tint, its beam from the claws |
| Salvager | drone_salvager | 90° CCW | 40 | 27 → 23 | A grab-claw bow |
| Lane couriers (all 4 rows) | drone_economy | 90° CW | **22 → 30** (Q6) | 15 → 12 | A train of containers. It gets its own file; today's rows borrow the miner and salvager art |
| Wing fighter | fighter_delta | none | 17 | 11 → 19 | The owner's pick: two bells |
| Wing bomber | fighter_g | 90° CCW | 28.125 | 28 → 18 | The owner's pick: its torpedoes leave from its wingtip rails |

### Keep their current art

| entity | art | why |
|---|---|---|
| Escape pod | escape_pod.png, untouched, 24 u | The pack has no pod |
| Freighter sentries | turret_deploy.png | Not a hull |
| Every turret | turret_main.png, turret_pd.png | The pack has no turrets |

### Spares (go into `art_source/`, not into the game)

| sprite | rotation | note |
|---|---|---|
| heavy_fighter_hf01 | 90° CCW | The v2 carrier warp gunships, once they exist |
| carrier_b | 90° CCW | A future pirate-carrier boss (the owner's picks) |
| cargo_1 | 90° CW | Reads as the hauler (IoU 0.84) |
| fighter_tri_b | none | A duplicate of tri_a (IoU 0.975) |
| drone_mining, interceptor_a, interceptor_b | 90° CW | Unused since the owner's picks |

**Count:** 27 distinct sprites used (drone_sensor and drone_salvager each on two rows), plus 7 unused = **34**.

---

## 2 · WHAT EACH ENTITY NEEDS RE-PLACED

Every row, whatever the entity, needs:
- a **keel** mark (the hull's centre line, in source px);
- its **nozzle list** (centre and bell size per nozzle);
- `HalfWidth`, kept as today's number by default (Q1).

Every other mark is placed in SOURCE pixels on the tool's row, and the tool prints it in world units. The ≈
figures below come from the orientation report's ±5 px reads, converted to world units; the tool gives the exact
values.

| entity | marks to place | nozzles | waits for kit sign-off? |
|---|---|---|---|
| **Battleship** | 4 mains on the flanking twins, ≈ (±24, −74) and (±24, −36) (today: 4 on the spine). **Arcs** (F7). PD x2 on the aft domes ≈ (±59, +72) (today ±21, 157). k ≈ 1.9 from the twin housing (today 2.5). Paint out the 6 painted twins' barrels (Q4) | 4 | **yes**: the seats and arcs |
| **Carrier** | (≈ figures read off carrier_b; carrier_a's are measured in its slice) PD x2 on the flank stacks ≈ (±30, 0) (today 3). **Deck:** runway on x = 0 (needs the keel mark), 6 bays (the gear allows 5 bombers) two abreast at ≈ ±12.5 u, 3 rows about 30 u apart, keeping the keel lane (±6 u) clear for the lift. `RunwayBow` ≈ 110-125. Landing point (0,0) is on the runway. `EngineInset` goes | 2 | yes, as a capital (PD 2 is already binding, F16) |
| **Destroyer** | 2 mains on the keel mounts ≈ (0, −14) and (0, +18) (today −57 / +25). PD x2 on the flank domes ≈ (±31, +6). k from the twin housing | 3 | yes |
| **Freighter** | 1 main? (spotter cannon) and PD x2. Where the sentries leave the hull | 3 | **yes**: the main count |
| **Tender** | the lance's origin at the claw. PD x2 | 2 | yes |
| **Bastion** | the mortar mount(s): the art has 2 twins ≈ (±27, +53); 1 mount or 2 depends on the kit. PD x2 | 2 | yes |
| **Sniper / Warrior / Warden** | Sniper: the rail mount on the prow. Warrior: **no mains**, and the blade root at the prongs if the arc is drawn from them. Warden: flak mount and PD x1 | 3 / 2 / 2 | yes |
| **Dart / Echo / Wraith** | Dart: a fixed nose mount. Echo and Wraith: 1 main each (kit) | 2 / 2 / 2 | yes |
| **Gunship** | `TurretAft` ≈ +0.12 (today 0.143) and `TurretWidth` from its painted twin | 2 | no |
| **Cross / Lancerkin** | `TurretAft` and `TurretWidth` on the clean deck (their art has no painted gun there) | 2 / 2 | no |
| **Webifier / Talon / Pod** | none (`HitShare` unchanged) | 2 / 2 / 1 (built) | no |
| **Title Web** | none. Its row tint now applies (the own-art-keeps-its-colours branch goes) | 2 nacelles | no |
| **Rusty Bucket / Drake** | a **tint** (Q5). HW 70 and 90 kept, so the 310 literal stands. The nose stays at L/2 | 2 / 5 (listed; bosses draw no plume today) | no |
| **Pirate base** | `Mounts[4]` on the painted guns: horn roots ≈ (±332, −154) and canopy twins ≈ (±104, 0) (today a ring of r 210). L 483, HW 330 | 4 | no |
| **Pylon** | `Mounts[1]` = (0,0) on the lens. L 300, HW 150 | 0 | no |
| **Hauler** | 6 `PodCentre` plus `PodSize` on cargo_4's frames. PD (0, 8) re-seated. `Extent` share 0.12 → measured (≈ 0.2) | 2 (today 3, drawn inline) | no |
| **Miner / Salvager** | one drone: the beam emitter (today 0.45 L) moves to the claws' mouth. `Extent` (20, 12) → measured | 3 / 3 (built) | no |
| **Couriers** | none | count at build | no |
| **Wing fighter / bomber** | the bomber's torpedo point moves to its wingtip rails' front (today 0.45 L). The parked footprint is checked against the carrier's deck | 2 / 2 (built) | no (the art); the deck waits for the carrier |

Hit shapes:
- the capsule, the shield ellipse, `HitRadius` and the raid `Extent` all follow `HalfWidth` (kept by default);
- the raider circle `HitShare` is kept;
- the hauler and gatherer `Extent`s are re-measured.

The lancerkin's hit circle (r 45) will overhang its new 41-u-wide hull. It overhung the old one too, because a
circle cannot fit a 150-u hull.

---

## 3 · THE PIPELINE (one command, rerunnable)

**Sources**
- `art_source/pack/<owner's name>.png`: all 34 files as delivered, 15 MB. `art_source/.gdignore` keeps Godot out.
- The old drawings move to `retired/art/`: battleship, carrier, destroyer, heavy_fighter, light_fighter,
  pirate_base and pirate_pylon. `turret.png` stays.

**Tool: extend `tools/make_ships.ps1`; no new script.** Add one table, `$Finished`, with one row per game texture
(29 rows).

```
@{ Src = 'battleship_bb05.png'; Nose = 'Left'; Out = 'battleship_hull.png'; Length = 378; Keel = 165
   Marks = [ordered]@{ 'main±0' = (230,118); 'main±1' = (305,118); 'pd±' = (517,49); 'nozzle0' = (...); ... }
   Patches = @( <dst rect, clean src rect> ) }
```

Each row goes through these steps, in this order:

| step | what | existing code |
|---|---|---|
| 1 | load with its own alpha; **lightness = 0 where alpha = 0**, which drops the hidden background and the flagship's watermark | `Sheet.Cut` + 1 line |
| 2 | paint out painted guns under moving turrets | `PatchColumns`, or a rectangle copy |
| 3 | turn nose-up: Up 0, Right 1, Down 2, Left 3 quarter-turns; **never mirrored**; the marks are carried | `TurnLeft` |
| 4 | crop tight, **keel as the centre column**, 1-px pad (texture height = hull length + at most 1.3%) | `Trim`, extended to take the keel mark; the tool prints the keel's offset, which must be 0 |
| 5 | write, then print each mark as a pasteable `new(x, y)`. A `±` name prints the mirrored pair | `Out-Sheet`, `U()` |

**Never** runs on these:
- Paper, Resize, Sharpen, Levels, CutOut, Mirror or Half (the art is finished and shaded);
- `finish_ships.ps1`, which would shade it a second time.

**Deleted in the same edits (invariant C):**
- the old hull blocks, which are the trap. The raider blocks go with the enemies slice, and the carrier,
  battleship and destroyer blocks go with the capitals slice;
- `-Single`, `-Turn`, `-KeepRight`, `-FinalH` and `-Out`;
- the helpers only those used: `Hull()`, `CutOut`, `Seed`, `Dilate`, `Specks` and `Blank`.

The two turret blocks and the helpers they use stay.

**`-Preview <png>` is rewritten** to draw every `$Finished` row at world scale, tinted, with:
- turrets on its mount marks;
- nozzle dots, pods, deck bays and parked bombers.

It is the first look at every mark, before any engine run.

**Run:** `powershell -ExecutionPolicy Bypass -File tools\make_ships.ps1 [-Preview <png>]`. It rebuilds all
29 textures and both turrets. It has no randomness and no resampling, so a rerun gives the same pixels.

**Output names: keep today's** (Q2):
- every `res://` path, `DESIGN.md`'s class table and the `EndsWith` checks stay valid;
- one new file, `courier.png`;
- the rows name the art, so changing which sprite a role wears later is one row, with no code change.

**Godot:** nothing to do. `.import` files are not tracked, and the defaults (mipmaps off, `fix_alpha_border` on)
are right.

---

## 4 · ORDER

Sequenced because each slice reads the one before it. Every slice is committed on its own.

| # | slice | needs | proved by |
|---|---|---|---|
| 0 | **Pipeline:** the pack into `art_source/pack/`, `$Finished` + Cut/Trim/Preview changes, `-Single` deleted. No game file changes | none | the preview, read by eye |
| 1 | **Foundations, behaviour-neutral on the OLD art** (details below) | 0 | rung 2, then rung 3 green **before any art changes** |
| 2 | **Enemies:** 6 raiders + Web (the dummy and title screen follow). Old raider blocks deleted. Raider tints retuned if the brighter art reads too light | 1 | rung 3; rung 4 frames 0, 48, 49, 50, 53, 80 |
| 3 | **Bosses:** 2 rows + tints | 1 | rung 3; rung 4 frames 38, 39, 42, 62-66 |
| 4 | **Fleet + wing:** hauler (pods, PD, nozzles), miner, salvager, couriers, fighter, bomber | 1 | rung 3; rung 4 frames 7, 8, 12-16, 27, 28-28e, 46, 59, 59b, plus a courier frame |
| 5 | **Siege:** base + pylon, `Mounts` | 1 | rung 3 (the siege check near "a siege builds a pirate base"); rung 4 **new frame** |
| | *kit sign-off* | | |
| 6 | **Capitals BB / CV / DD**, with the capitals kit: deck, bays, PD 2, BB arcs. Old capital blocks deleted | 1 + kit F7/F16 | rung 3; rung 4 frames 0, 2, 3, 4, 9, 9d, 10, 10b-d, 22, 22b, 26, 30, 54 |
| 7 | **Freighters**, with their kit | 6 | 70-72, 79 |
| 8 | **Heavies**, with their kit | 6 | 73-75 |
| 9 | **Lights**, with their kit | 6 | 76-78 |
| 10 | **Rung 6, once** | all | the bar |

Slice 1, the foundations:
- `HullArt` in `Sprites.cs`, read by ClassArt, EnemyDef, BossType, WingDef, GathererDef, LaneDef, EmplacementDef
  and the 2 consts (`Hauler`, `EscapePod`); each table's own colour field (`EnemyDef.Tint`, `LaneDef.Tint`,
  `EmplacementDef.Main`) becomes `HullArt.Tint`, and `Gatherer.Length` moves onto the row;
- `Sprites.Fit(HullArt)` replaces the 4 copies of `Length / GetHeight`;
- `Plume` draws the nozzle list, which deletes `EngineInset` and the Hauler's inline array;
- `EmplacementDef.Mounts` replaces `Guns` and `GunRing`;
- the Hauler's pods, PD and Extent, and the Gatherer's beam point and Extent, move to rows;
- `TargetDummy` reads the webifier row;
- `MenuFoe` always tints.

The capitals' counts are already binding (BB 4+2, CV 0+2, DD 2+2). They stay after sign-off only because the
BB's seats and arcs are part of its kit.

Slice 1 touches `ClassArt`, which the kit foundations F7 and F16 also edit. **It lands before kit work starts,
or in sequence with it, never in parallel.** Art is local to each peer (every peer reads the same rows), so no
slice needs rung 5 on its own.

---

## 5 · WHAT CHANGES

### Files

| area | files |
|---|---|
| art | `art_source/pack/` (+34), `retired/art/` (+7 drawings), 28 PNGs at the repo root overwritten plus `courier.png`, `art_4x/` deleted (Q9) |
| tool | `tools/make_ships.ps1` (the table; old blocks and `-Single` gone), `tools/finish_ships.ps1` deleted (Q9) |
| code | `Sprites.cs` (`HullArt`, owner of the contract; header says what it replaced and what a row fills), `Plume.cs`, `Ships.cs`, `Enemies.cs`, `Missions.cs` (BossType: Hull + Tint), `ShipClasses.cs` (WingDef, torpedo point), `Gatherer.cs`, `Lanes.cs`, `Emplacements.cs`, `Hauler.cs`, `EscapePod.cs`, `TargetDummy.cs`, `MenuFoe.cs`, `PlayerShip.cs`, `CharacterCreator.cs`, `Boss.cs`, `Raider.cs` |
| docs | `DESIGN.md` "Art" (its line-art, mirror and paper-cut description stops being true), `README.md` commands (and the form feed at :195), `CLAUDE.md` §7 (the finish_ships line), `CHANGES.md` |

### Rows

- `Classes.All[].Art` ×12
- `Enemies.All` ×6
- `MenuFoe` Web
- `Missions.Bosses` ×2
- `Emplacements.All` ×2
- `Gathering.All` ×2
- `Lanes.All` ×4
- `Wings.All` ×2
- the Hauler's row
- the EscapePod's row (art unchanged)

### Smoke checks that change (by default)

| anchor (≈ line now) | today | after |
|---|---|---|
| `EndsWith("battleship_hull.png")` (1766) | 378, HW 43.875, the "line-art hull" message | message only (the name, 378 and HW are all kept) |
| `x.Offset.Y > 150f` (≈1773) | PD at the stern; mains on the spine (`Offset.X == 0`) | PD on the aft domes (y ≈ 72), mains on the flanking twins (\|x\| ≈ 24) |
| `14.7f ... 36f` (2874) | a parked bomber on the old deck's white strip | the new deck's literals (≈ 6.4 to 19.5 u) |
| `-110.06f` (7095) | the lift path's RunwayBow | the new RunwayBow |
| `Gatherer.Length * 0.45f` (1883) | the beam from 0.45 L | the beam from the row's emitter mark |

**Stay green unchanged** if Q1 and Q2 take their defaults:
- destroyer 2653 (212.625, HW 27.8, 2+2);
- carrier 2842;
- bomber and fighter sizes (≈1855, 3015);
- the pod at 24 (4046);
- the hauler clearing the station (2041);
- the menu hit radii (686) and gunship = 4 × webifier (3848);
- the boss rows (5414-5415);
- the Drake throw's 310 (5470-5471);
- the Drake sprite (5866);
- the rock's flank (≈5975);
- every class's mount count matching its sheet (4255).

### New checks (written in the same edit as the thing they prove)

1. **Every `HullArt` row** (one loop): the texture's `GetUsedRect()` covers at least 98.5% of its height and reaches
   one side edge within 2 px. This proves the trim for all 29. The keel is proven in the tool, which prints its
   offset; a used-rect centre cannot prove it, because unit_d and the flagship are not mirror images.
2. **Nozzle counts are literals from the art:** BB 4, CV 2, DD 3, Drake 5, hauler 2, etc. One plume per nozzle
   while thrusting.
3. **Tinted rows:** boss and gatherer `Modulate` equals the row's Tint, and not white.
4. **Siege:** the base has 4 turrets on its `Mounts`, the pylon 1.

Separately, the unused `bomb0` local at ≈1854 goes.

### Screens

Add:
- 1 frame for the siege (base and 4 pylons, in the arena section);
- 1 courier close-up.

Rung 4's `LINT: 0` says nothing about sprites. **Each slice's frames (§4) are read by eye**, looking for:
- turrets on their painted seats;
- flames on the bells;
- the keel on the runway;
- tint brightness.

### Rungs

| slice | rungs |
|---|---|
| 0 | the tool preview (rung 0) |
| every code slice | 1 → 2 → 3 |
| slices with art | also 4, read by eye |
| the batch | 6, once, at the end |

Nothing here crosses peers, so rung 5 runs only inside rung 6.

---

## 6 · QUESTIONS FOR THE OWNER (the default stands if unanswered)

| # | question | default |
|---|---|---|
| 1 | **HalfWidth** (collider, shield, hit radius): keep today's numbers, or re-mark each hull side on the new art? Re-marking moves the Tender 57 → ≈24 u and the Sniper 30 → ≈13 (measured amidships) | **keep today's** (gameplay unchanged) |
| 2 | Output file names: keep today's, or name them after the pack? | **keep today's** |
| 3 | Battleship mains: on the 4 flanking twins (keel twins painted out), or on the 2 keel twins plus 2 flankers? | **the 4 flanking twins** |
| 4 | Painted guns under moving turrets: paint them out? | **yes on player hulls**; leave them on raiders (too small to see) and bosses (no moving turrets) |
| 5 | Tints for grey art that was coloured | **the old sprite's average colour:** Rusty (0.33, 0.25, 0.26), Drake (0.52, 0.35, 0.29), miner (0.63, 0.46, 0.31), salvager (0.61, 0.35, 0.11). Wing and hauler stay untinted (white), as today. The Rusty's skull is lost |
| 6 | Courier Length 22 → 30? (At 22 the new art is 8.6 u wide) | **30** |
| 7 | Sniper and Dart are needle-nosed cousins (IoU 0.82) | **keep**, told apart by size and engine count; the other option is Dart ↔ Wraith art |
| 8 | Clipped nozzles (carrier_b, frigate_d) and the low-resolution siege (crescent_b, drone_sensor): re-export those four from the source, at 2x with a margin? | **accept as delivered** |
| 9 | `finish_ships.ps1` and `art_4x/` (13 MB, unreferenced) have no use after this | **delete both** |
| 10 | Title-screen Web: crescent_a, tinted, at the webifier's 34 u (60 u across)? | **yes**; the other option is the webifier's own art |
