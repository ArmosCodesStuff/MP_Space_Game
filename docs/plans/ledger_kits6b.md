# Ledger: kits lane A, slice 6b (the freighters: FREIGHTER, TENDER, BASTION)

Writer: one agent at a time in worktree `WarShips_wt_kits6b`, branch `wt/kits6b`, started at a8a5e81 (version-l).
BUILD PHASE: no engine; per job typecheck + verify -Quick + a read of the own diff; every check written now, run in
the final test phase. A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits, redo it.
Conventions and D1-D32: `ledger_kits.md`, `ledger_kits5.md`. Other tiers of slice 6 build side by side: touch only
the three freighter classes, their rows, and the foundations below (appended rows only; new wire ids at the END).

## Spec as read (newest wins)

- v1 cards (the base the v2 file points at as "see v1 §3"; never committed): the session scratchpad
  `...\3495b155-...\scratchpad\signoff.md` §3 FREIGHTER / TENDER / BASTION and §4 F7/F9/F10. README: "Approved as
  written: Bastion, Tender"; Freighter with the ruling (TOT hitscan, throw 600 u / 0.8 s, sentries prefer the paint).
- **FREIGHTER** hull 450, top 120 (built). Space **Spotter cannon**: 800 u, 31.25 every 1.25 s (25 DPS), 560 u/s, a
  hit PAINTS the target 5 s (one target, the latest hit). R **sentry** (built slice 5: throw 600 u / 0.8 s, recall 60 u,
  3 out, 6 s, 120 hull) with v1's **650 u, 5 every 0.5 s (10 DPS)**. F **Time on target** (kits_v31 §3.1): paint < 5 s
  old; the spotter (hull muzzle) + each LANDED sentry within 1500 u of the paint fire one hitscan line each, same host
  tick, 14 u wide, stops at the target, 40 to every hostile on it (allies never), cd 16, refused NO PAINT / COOLING;
  credited `tot`; Beam row `tot`, drawn by Fx.Line in the spotter's colour. Q **Bubble** (built: 400 pool, 260 u, 8 s,
  cd 25) now covers every friendly hull inside: allies, sentries, fleet craft. E **Redeploy**: every sentry folds and
  lands 1.0 s later on a 150 u ring round the hull, 120° apart, each with its hull; cd 20; refused NONE OUT. PD x2.
  Leaves: fire mode, the 12 DPS cargo gun.
- **TENDER** hull 380, one 500 u radius for its three fields. Space **Mending lance**: a beam from the main barrel
  locking onto the FIRST body it touches, 650 u; an enemy takes 4 per 0.1 s (40 DPS), an ally is repaired 0.8 per
  tick (8 HP/s), never both; it follows the main turret at 90°/s; missiles ignored. F **Overdrive field**: 8 s, you and
  every friendly within 500 u fire x1.5 (sentries included), cd 24. Q **Repair field**: 500 u, 2.0% of MAX hull a
  second for 8 s to every friendly hull, never a wreck, cd 30. E **Resupply**: instant, every friendly within 500 u,
  YOU INCLUDED (v2 fix), has 8 s cut from each ability still cooling (ClassDef.Abilities only: never the drive,
  kits_v31 §3.4), except Resupply; refused and free when nothing is cooling; cd 30. PD x2. Leaves: cargo gun, fire
  mode, deploy/recall.
- **BASTION** hull 420. Space **Siege mortar**: lobbed onto the cursor, clamped 150-1100 u, fixed 1.4 s flight under a
  friendly landing circle, 58.75 in 110 u every 2.35 s (25 DPS on one). F **Bunker buster**: one slow round, stops on
  the first body, 180 (x2 = 360 on a boss or structure), 25% (90) through a pylon shield, 380 u/s, 1400 u, cd 12.
  Q **Shockwave** (built: 1000 u, throws 1000 u, cd 30) with v1's fix: a boss, structure or dummy is HELD 3 s, never
  thrown. E **Gravity well**: at the cursor up to 900 u, r 280, 6 s, pulls lights 200 u/s and heavies 100 u/s, only
  UNLATCHED craft; bosses, structures, missiles never move; cd 22. PD x2. Leaves: cargo gun, fire mode, deploy/recall.
- Walls read `ClassDef.Abilities` in learn order (non-Weapon rows = abilities 1/2/3 at L1/L3/L6): FR F TOT, Q Bubble,
  E Redeploy; TE F Overdrive, Q Repair field, E Resupply; BA F Buster, Q Shockwave, E Gravity well (kits_v31 §3.6).
- Rung-5 (second run) items for 6b: TOT lines (a guest draws every line from its own sentry copies to the host's target
  within 20 u), the throw. Rung-4 frames: the TOT lines (+ this tier's fields, lance, mortar circle, well).

## Decisions (D33 on; D1-D32 are ledger_kits*.md's)

- **D33** THE PAINT RIDES A SLOT (closes D32): `PlayerShip.PaintOn/Painted` keep their signatures but store the paint
  in the `guns` slot (Left = seconds left, N = the target's NetId). Slots are on the wire (host report), so a guest's
  `Painted`, its sentry copies' `Prefer` and its TOT refusal read the host's paint with no new field or RPC.
- **D34** THE SPOTTER: `ShotDef.Paint` (a stat id: seconds a hit paints for) and the row `Shots.Spotter` APPENDED
  (index 8). `ClassDef.MainShot` (a Shots row, default Shell) sets the main mounts' `TurretSpec.Kind`; Shot.Strike on a
  host hit of a Paint row calls `Source.PaintOn(target, Stats[Paint])`.
- **D35** PRIMARIES AS A FIELD: `ClassDef.Primary` enum {Guns (default), Lob, Beam}; FireControl dispatches on it
  (Guns = today's turret path). A new primary kind is a member plus its one fire method; a class is a field value.
- **D36** TOT LINES: `Lines.Tot` row APPENDED (Width `tot_width` 14, Reach `tot_reach` 1500, Stops 0, AtTarget true,
  Fx row `tot`, Beam row `tot`, both appended). Credit id `Dealt.Tot` = "tot". "Landed" = in `Hub.Deployed` (a throw in
  flight is in `_throws`, never a gun). Muzzle = the main turret's barrel tip toward the target (the hull's spotter).
- **D37** The Tender's heal goes through slice 4's `Mend.Give` (F10). If version-l has not merged kits4 when the
  Tender job starts, that job merges version-l again first; if still absent, the Tender jobs wait at the end and the
  return says so (never a second heal door).
- **D38** AURAS: the Overdrive field lifts other ships: `AbilityDef.Aura` (a radius stat id). `PlayerShip.Lifts(Rate)`
  also adds, for every OTHER live pilot within that pilot's Aura radius whose row runs, that row's RateStat share
  (x1.5 = +0.5; adds, never multiplies). Sentries read their owner's `Cadence` today, so an ally's sentries are lifted
  through their owner being in reach (the v1 "sentries included" for the Tender's own; noted, not special-cased).
- **D39** THE WELL: a host-side list on the Hub (`Wells`: at, radius, left, lightPull, heavyPull) ticked in the
  Hub's process; every peer draws it from one `Fx.Warn` raise (a new Fx row `well`, Time = 6 s, Size = 280). Pull
  reads `Targeting.Immovable` (slice 5) for bosses / structures, never a missile (Tag.Missile / Hulled shots are not
  in the pool), and skips a latched raider (`Raider.Latched` or the equivalent host flag).
- **D40** Tender and Bastion lose `Fit.Deploy` and every deploy_* / recall_pick row (v1 "Leaves"); Freighter keeps
  them with deploy_damage 5, deploy_range 650. `FireMode` leaves all three rows' `Abilities`.

## Jobs (foundations before their users; each: PRE, edit + its checks, typecheck + quick, POST, commit)

Each ability's checks (CLAUDE.md 6.7): effect literals + each kits_v31 §7 interaction + a guest-role check (rung 5) or a
named frame (Shots.cs.txt), each from 3 varied situations. Named methods `LaneA6b*` in SmokeTest.cs.txt / Shots.cs.txt.
Before every job: grep the harness for checks asserting the OLD truth of what it changes and rewrite them (6.3).

- **kits6b-J1 FR spotter + paint on the wire** (D33, D34, D35's field only). Shots.Spotter + ShotDef.Paint,
  ClassDef.MainShot, paint in the guns slot, Freighter row (hull 450, spotter numbers, paint_time 5, sentry 5 / 0.5 /
  650, FireMode out, Abilities {Guns, Deploy, Bubble} until J2/J3 add theirs). Checks `LaneA6bSpotterChecks` (3 ranges:
  a hit paints for 5.0 s, a second target moves it, 31.25 a round credited), `LaneA6bPaintWireHost/Guest` (rung 5).
- **kits6b-J2 FR Time on target** (D36). Lines.Tot, Fx/Beam rows, Dealt.Tot, Ab.Tot (F) first in learn order, tot_*
  rows. Checks `LaneA6bTotChecks` ({0,1,3} sentries at 1499 u -> {1,2,4} lines; 1501 u silent; one in flight silent;
  one DealtBy frame; 40 per line on a dummy moving at {0,130,260}; a light across a line takes 40 and the paint still
  40; paint age 4.9 fires / 5.1 NO PAINT; cd 16.0), `LaneA6bTotGuest` (rung 5, lines within 20 u), frame `tot_lines`.
- **kits6b-J3 FR Bubble on every friendly hull + Redeploy**. Bubble covers DeployedTurret and UtilityShip too (Q);
  Ab.Redeploy (E). Checks `LaneA6bBubbleCoverChecks` ({ally, sentry, craft} at {259, 261}), `LaneA6bRedeployChecks`
  ({1,3} out keep hull, ring 150 u 120° apart at 1.0 s, NONE OUT, cd 20), frame `redeploy_ring`.
- **kits6b-J4 BA mortar + Lob primary** (D35, D40). Missiles.All row `mortar` (friendly, appended), FireControl Lob;
  Bastion row (hull 420, mortar rows, deploy out). Checks `LaneA6bMortarChecks` (cursor 100->150, 1100, 1300->1100;
  lands at 1.4 s; 58.75 in 110 u, three bunched take 58.75 each; bow vs centre by Covers), frame `mortar_circle`.
- **kits6b-J5 BA Bunker buster + Shockwave holds structures**. Shots.Buster (appended; ShotDef.Versus-style x2 on
  Tag.Boss|Structure, shield share 0.25), Ab.Buster (F); Shockwave to Q, held not thrown for boss/structure/dummy.
  Checks `LaneA6bBusterChecks`, `LaneA6bShockHoldChecks` (+ rung-5 host/guest: an immovable at the same place).
- **kits6b-J6 BA Gravity well** (D39). Ab.Well (E), Hub wells, Fx row `well`. Checks `LaneA6bWellChecks` (cruising /
  boosting / latched raider x edge 279/281; light 200 vs heavy 100 u/s; boss, pylon, seeker unmoved), frame `well`.
- **kits6b-J7 TE Mending lance + Beam primary** (D37). Tender row (hull 380, lance rows, deploy out). Checks
  `LaneA6bLanceChecks` (first body enemy / ally / sentry, missile ignored, 649/651 u, 40 DPS vs 8 HP/s, 90°/s follow).
- **kits6b-J8 TE Overdrive field, Repair field, Resupply** (D38). Checks `LaneA6bOverdriveFieldChecks` (ally 499/501,
  leaving mid-buff, adds with another lift), `LaneA6bRepairFieldChecks` (2% of MAX/s, wreck skipped, caps at max),
  `LaneA6bResupplyChecks` (slots cooling / running / ready, you included, the drive untouched, NOTHING COOLING free).
  Frames `overdrive_field`, `repair_field`.
- **kits6b-J9** record: CHANGES Unreleased + Handoff, DESIGN section, final POST (what the test phase owes).

### kits6b-J1 · PRE · FR spotter, the paint on the wire (D33, D34) -- tier opus
- Intent: Shots.Spotter (appended, 8) + ShotDef.Paint; ClassDef.MainShot -> PlayerShip.Spec(false).Kind; Shot.Strike
  paints on a host hit; PaintOn / Painted on the guns slot (Left, N = NetId); Freighter row: hull 450, main 31.25 /
  1.25 s / 800 u / 560 u/s, paint_time 5, deploy_damage 5, deploy_range 650, FireMode out. Old-truth callers rewritten
  (deploy_damage 6, SustainedDps 50, MaxHp 400, For(...).Length 11). Checks LaneA6bSpotterChecks, LaneA6bPaintWireHost/Guest.
- Files: scripts/Shots.cs, scripts/Ships.cs, scripts/PlayerShip.cs, scripts/Abilities.cs, tools/smoketest/SmokeTest.cs.txt, this ledger.
- HEAD fd5dacddc4585901bcafbfaf485084d13f2caadf · Shots.cs 05c56a1f0517457bda6938d963d7330c049989ef · Ships.cs 5415309685219dbbec36724dfee2c526ed106413 · PlayerShip.cs d532201f0baf49377bba9c8b96018b6ddcf6eb88 · Abilities.cs fdd4a423b516cbf1e187e372ec963810e197aea6 · SmokeTest.cs.txt 59e7c40100d0ffc791c20a1e5a65601e740bb610
### kits6b-J1 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Shots.Spotter = 8 (appended, wire index) with ShotDef.Paint = "paint_time"; ClassDef.MainShot sets the
  mains' TurretSpec.Kind; Shot.Strike paints on a host hit; the paint lives in the `guns` slot (PlayerShip.PaintSlot:
  Left, N = NetId) so it rides the host report (D33, D32 closed). Freighter: hull 450, spotter 31.25 / 1.25 s / 800 u /
  560 u/s, paint_time 5, sentries 5 a shot / 650 u, FireMode out, Abilities {Guns, Deploy, Bubble}. Items.PrimaryShots
  gains "spotter" (Spin-up Feed ramps the freighter's primary).
- Old truths rewritten: fitRows (fire mode is no fitting's; never on a hull without guns, never on the freighter),
  deploy_damage 6 -> 5 (+ range 650), freighter SustainedDps 50 -> 25 + 30 + 2, arena host/guest 400 hull -> 450,
  For(freighter).Length 11 -> 10, Spin-up Feed's freighter blows "shell" -> "spotter", LaneASentryPreferChecks now
  flies a freighter (the paint needs the spotter's slot).
- Files: Shots.cs, Ships.cs, PlayerShip.cs, Items.cs (one id), SmokeTest.cs.txt (LaneA6bSpotterChecks after
  LaneASentryThrowChecks in solo; LaneA6bPaintWireHost in the arena host after the freighter parks; LaneA6bPaintWireGuest
  in the arena guest after its 450-hull check). Test phase owes: solo x2 (LaneA6bSpotterChecks, the rewritten five),
  six x2 (the paint wire pair).
- Next: kits6b-J2 (TOT). Bubble moves F -> Q in J2 (TOT takes F): rewrite `KeyFor(... "bubble")` / "F" comments then.

## Handover 1: the first agent stops after kits6b-J1 (context), at a job boundary

Done: JOB 0, J1. Next is kits6b-J2; no PRE written for it. What the next agent needs (saves re-reading):
- **Where things are.** Lines.cs (whole, 111 lines: rows Rail/PrismOut/PrismThrough, `Strike(row, by, from, to, damage)`
  returns the drawn end; an AtTarget row ends at `to`). Fx row consts: Fx.cs ~line 86 (`Rail = 6` ...), Fields table
  Fx.cs ~490 (`FieldDef` rows: bubble, patrol, taunt, boost; a new field is a row). Beam rows: Beam.cs. Dealt ids:
  Dealt.cs. Sentries: `Hub.Deployed` (landed, `DeployedTurret.OwnerId`), throws in flight: PlayerShip `_throws`
  (~line 619). The spotter muzzle: `_mains[0]` (PlayerShip's main Turret list; `Turret.S.Barrel`, GlobalPosition).
  Abilities: Ab rows in Abilities.cs (Bubble / Overdrive / Shockwave rows all Default Key.F today; `Timed(...)` helper
  for Show). Ships.cs rows: FreightHauler ~246, FreightTender ~290, FreightBastion ~330.
- **Harness.** SmokeTest.cs.txt is LF. Helpers: `SetClass`, `Wait`, `Vary/VaryAngle/VaryNear` (~1136), `KeyDown/KeyUp`
  (~67), `AimWorld` (~81), `SentryAt(ship, cursor)` (~2000), `H.Drop(owner, at, hull)`, `H.SpawnRaider(at, Enemies.X)`,
  `Combat.Fire(Shots.Seeker, ...)`. Solo order: the LaneA* list ~line 11040 (LaneA6bSpotterChecks after
  LaneASentryThrowChecks). Rung-5 anchors: arena host after "the guest refitted to a FREIGHTER and parked" (~14684,
  LaneA6bPaintWireHost), arena guest after "flying a FREIGHTER in a session" (~14975, LaneA6bPaintWireGuest).
  Tool quirk: a long python edit in a bash heredoc failed to parse once; write the python to the scratchpad and run it.
- **Old-truth sites J2-J8 will meet** (grep before each job): bubble pressed as F (`fr.UseAbility("bubble"` ~5218,
  ~10620, arena guest ~15030 "// F"); the Tender's overdrive x2 (~10694-10721, `KeyFor(FreightTender, "guns")`);
  the Bastion's shockwave (~10680, ~10985); `Abilities.For(FreightHauler).Length == 10` (arena guest) grows with each
  row added; the weaponRows / level-wall tables (~6895-6901) and the every-ability witness sweep (~11446: every row on
  a bar needs a witness entry there); passive PD table (~2476) keeps 2 mounts on all three.

## Agent 2 resumes at kits6b-J2 (3da5808; J1 POSTed, nothing interrupted)

### kits6b-J2 · PRE · FR Time on target (D36) -- tier opus
- Intent: Lines.Tot (3, appended), Fx.Tot row (15, appended), Beam.Tot (9, appended), Dealt.Tot "tot"; Ab.Tot (F) first
  in the freighter's learn order (Guns, Deploy, Tot, Bubble); rows tot_damage 40, tot_width 14, tot_reach 1500,
  tot_cooldown 16; PlayerShip.TimeOnTarget (host): paint alive, the spotter muzzle + each LANDED own sentry within
  tot_reach of the paint, one Lines.Strike each, same tick. Bubble -> Q. Old truths: witness sweep (+ "tot"; every
  press is given the mark painted), live wall check 3 (the freighter's ability 1 now needs the paint: the mark is
  sunk after the press), arena guest For(freighter).Length 10 -> 11. Checks LaneA6bTotChecks (solo),
  LaneA6bTotHost/Guest (rung 5), frame tot_lines (Shots.cs.txt).
- Files: scripts/Lines.cs, Fx.cs, Beam.cs, Dealt.cs, Abilities.cs, Ships.cs, PlayerShip.cs, SmokeTest.cs.txt, Shots.cs.txt.
- HEAD 3da5808dc39ffed6fad455e8b7456565016378da · Lines d418369 · Fx a44e942 · Beam 459b969 · Dealt 055f743 · Abilities
  fdd4a42 · Ships 1ee522d · PlayerShip b6d72a2 · SmokeTest a0c6c99 · Shots.cs.txt 95eeb17
### kits6b-J2 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Lines.Tot = 3, Fx.Tot = 15, Beam.Tot = 9 (appended; laser_boss @0.80, between bolt 0.90 and rail 0.71),
  Dealt.Tot; Ab.Tot on F (ability 1), Bubble on Q; PlayerShip.TimeOnTarget; rows tot_damage/width/reach/cooldown
  (kept out of Damage/Reach: no Weapons/REACH points lift it -- a default, noted for the gate).
- Old truths rewritten: witness sweep (+ "tot"; the sweep paints its mark before every press), live wall check 3 (the
  mark sunk AFTER ability 1, which needs the spotter's paint), the beam pin table (+ tot), Fx.All.Length 10 -> 16
  (it was already stale at 15 rows before this lane: two checks rewritten), arena host deploy_damage 6 -> 5 (J1 missed
  it), arena guest For(freighter).Length 10 -> 11, the guest's bubble comment F -> Q.
- Checks written: LaneA6bTotChecks (solo, after LaneA6bSpotterChecks), LaneA6bTotHost/LaneA6bTotGuest (arena, after
  the decoy pair), frame 79c_tot_lines (Shots.cs.txt LaneA6bTotFrame). Test phase owes: solo x2, six x2, screens.
- Next: kits6b-J3 (Bubble on every friendly hull + Redeploy).

### kits6b-J3 · PRE · FR Bubble over every friendly hull + Redeploy -- tier opus
- Intent: PlayerShip.ThroughBubbles(at, d) public, by position: a pilot's Guarded, DeployedTurret.TakeDamage and
  UtilityShip.TakeDamage all spend the bubbles over them first. Ab.Redeploy (E, ability 3): every LANDED own sentry
  folds (DeployedTaken) and rides _throws for redeploy_flight 1.0 s to a ring redeploy_ring 150 u round the hull,
  2 pi / deploy_max apart (120 deg) from the bow, each with its hull; cd redeploy_cooldown 20; refused NONE OUT / COOLING.
  D41: the ring is taken round the hull at the press (a throw's landing point is fixed at its throw).
  Old truths: "F raises a bubble" message -> Q. Checks LaneA6bBubbleCoverChecks, LaneA6bRedeployChecks, frame redeploy_ring.
- Files: Abilities.cs, PlayerShip.cs, Deployed.cs, UtilityShip.cs, Ships.cs, SmokeTest.cs.txt, Shots.cs.txt.
- HEAD a3265f7a3d2876b8fce0b945f85c14cfb9f05d05 · 7146e4f c438f09 41fe484 4d92dd4 a250e89 640022b 9433a13 
### kits6b-J3 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. ThroughBubbles(at, d) is by place and public: the pilot's Guarded, DeployedTurret.TakeDamage and
  UtilityShip.TakeDamage all pay the bubbles over them first. Ab.Redeploy on E (ability 3; the freighter's list is
  Guns, Deploy, Tot, Bubble, Redeploy); PlayerShip.OwnLanded/Redeploy (D41: the ring is taken at the press).
- Old truths: the solo bubble message F -> Q; arena guest For(freighter).Length 11 -> 12; the witness sweep (+
  "redeploy"; a Fit.Deploy hull is given one sentry standing 200-300 u off its cursor before each press).
- Checks written: LaneA6bBubbleCoverChecks, LaneA6bRedeployChecks (solo, after LaneA6bTotChecks), frames
  79d_redeploy_marks / 79e_redeploy_ring (Shots.cs.txt LaneA6bRedeployFrame). The ally case of the bubble is the arena
  host's existing check (the host's hull inside the guest's bubble). Test phase owes: solo x2, screens.
- Next: kits6b-J4 (Bastion mortar + the Lob primary).

### kits6b-J4 · PRE · BA Siege mortar + the Lob primary (D35, D40) -- tier opus
- Intent: ClassDef.Primary {Guns, Lob, Beam} (Beam's fire method lands in J7); FireControl's one shot dispatches on
  it; PlayerShip.LobPoint(cursor) (clamped mortar_min 150 .. main_range 1100 on the cursor's bearing) and a Lob
  throws Hub.ThrowMissile(Missiles.Mortar, appended index 2: friendly AimZone mark, the hostiles, Attackable,
  credited Dealt.Mortar "mortar" to the pilot); MissileSide.Land gains the thrower's id. Bastion: hull 420, 58.75 /
  2.35 s / 1100 u, mortar_min 150, mortar_flight 1.4, mortar_blast 110; FireMode, Fit.Deploy and every deploy_* /
  recall_pick row out (D40); Abilities {Guns, Shockwave} until J5/J6. Items.PrimaryShots + "mortar".
  Old truths: grep the harness for the Bastion's hull 400, its deploy rows, its firemode, its 12-damage gun.
  Checks LaneA6bMortarChecks, frame mortar_circle.
- Files: Ships.cs, PlayerShip.cs, Missiles.cs, Hub.cs, Dealt.cs, Items.cs, SmokeTest.cs.txt, Shots.cs.txt.
- HEAD 43b09537c92861708c13879ca19b4fc0fb6f4cfe · Ships Player Missiles Hub Lanes Dealt Items Smoke Shots: 8336fbd 31355e4 c3a26d7 ab952ed 01e9085 768ab57 9059689 97ef5d7 0244884 
### kits6b-J4 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. enum Primary {Guns, Lob, Beam} + ClassDef.Primary / LobSide; FireControl -> FireOnce (the one switch
  on the kind) -> Lob (PlayerShip.LobPoint clamps mortar_min..main_range); Missiles.Mortar = 2 (appended; Land now
  takes the thrower's id, credited via Combat.PlayerById); Dealt.Mortar; Items.PrimaryShots + mortar. Bastion: hull
  420, 58.75 / 2.35 / 1100, mortar_min 150 / flight 1.4 / blast 110; Fit.Deploy, FireMode, deploy_* rows, shell_speed
  and Dps.Deployed out; Abilities {Guns, Shockwave} until J5/J6. Kept by default: the weapon-slot own item
  CargoGun ("freight_main_gun") stays on all three (renaming a saved item id is a save-format change).
- Old truths: the reach sweep's main_range gets a Lob arm (a Lob's reach is its clamp, not a shell's flight).
- Checks written: LaneA6bMortarChecks (solo, after LaneA6bRedeployChecks), frame 79f_mortar_circle (LaneA6bMortarFrame).
  Test phase owes: solo x2, screens.
- Next: kits6b-J5 (Bunker buster + Shockwave holds structures, Shockwave -> Q).

## Handover 2: agent 2 stops after kits6b-J4 (context), at a job boundary

Done: J2, J3, J4 (a3265f7, 43b0953, 19cfffa). Next is kits6b-J5; no PRE written. For the next agent:
- **Edit tooling.** Files are LF: a python edit with default newline on Windows wrote CRLF once (fixed with sed).
  Use the scratchpad helper pattern: a file of EDITS=[(old,new)] applied with open(..., newline='') and a
  count==1 assert; write long edit files with the Write tool (a bash heredoc with quotes inside failed to parse).
  verify is `.\verify.ps1 -Quick` at the tree root (not tools\).
- **Where the new pieces are.** PlayerShip: TimeOnTarget / OwnLanded / Redeploy beside StartOverdrive and the
  throw code; FireOnce / LobPoint / Lob after FireControl (Primary.Beam falls to the guns path until J7 writes its
  fire method there). Ships.cs: enum Primary above ClassDef; the Bastion row now {Guns, Shockwave} -- J5 makes it
  {Guns, Buster, Shockwave} with Shockwave on Q, J6 appends Well on E. Missiles.Land takes (n, d, at, src, from).
- **Harness.** New solo methods run after LaneA6bSpotterChecks in this order: Tot, BubbleCover, Redeploy, Mortar.
  Rung-5 pair LaneA6bTotHost/Guest sit after the decoy pair in the arena host/guest. Shots.cs.txt: LaneA6bTotFrame,
  LaneA6bRedeployFrame, LaneA6bMortarFrame after frame 79 (the freighter block); Shots' Class()/Me() are local
  functions of its big method, so a new frame method uses Character.Class + P(hub,"ApplyLocalIdentity") itself.
- **Old truths J5 meets:** the bastion shockwave checks (solo ~11000 "a 1000 u shockwave throws nothing in flight",
  ~11300 "BASTION: everything within 1000 u thrown", ~14228 the dummy/structure throw check) press "shockwave" by id
  (keys unaffected) but assert Tag.Boss -> Disabled only; J5's "boss, structure or dummy HELD 3 s" rewrites the
  structure/dummy case (Targeting.Throwable already refuses to move them: make them held too). The witness sweep
  needs rows for "buster" (and "well" in J6); sweep B (reach) needs an arm for any Reach id J5/J6 add (or keep
  them out of ClassDef.Reach). Arena guest `Abilities.For(...).Length` asserts only the freighter's.
