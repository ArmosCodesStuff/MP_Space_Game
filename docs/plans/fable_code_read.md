# Fable code read (2026-09-25): rewrite the scripts, or test and fix them?

Read whole: every game file under `scripts/` (104 files, 26,401 lines), DESIGN.md and plans README headings,
TRAPS.md, the harness's shape (25,259 lines, 1,767 `Check(`, 193 `Lane(`, 171 `*Checks` methods), and the two
round-1 fails files (solo at seed 11400714819323333590; six). Judged against CLAUDE.md's rules and invariants A-D.

## 1 The map

| subsystem | files (lines) |
|---|---|
| the ship | PlayerShip 2500, Abilities 1032, Ships (12 class rows) 758, Stats 440, Drives 359, HelmMoves 233, ActiveReload 187, Charge 73, Bores 76, Melee 121, Lines 116, Prism 145, Doses 66, Trails 47 |
| the fight | Combat 162, Targeting 134, Tags 49, Dealt 43, Statuses 164, Shots 474, Missiles 192, Turrets 356, Deployed 121, Zones 217, Decoys 129, Towing 120, Mend 55, Fx 667, Beam 87, Popups 130 |
| hostiles | Boss 676, Lancer 78, Drake 80, ThrownRock 98, Raider 373, Enemies 146, Squads 328, SquadSight 70, Raids 243, Waves 380, Emplacements 286, Missions 274, Par 87, TargetDummy 157, MenuFoe 226 |
| the world | Hub 1985, HubNodes 165, Landmarks 208, Docks 136, Lanes 328, Spawned 174, Session 123, Votes 111, Sky 135, Ids 46, Assets 45 |
| the economy | Yard 666, Hauler 450, Gatherer 327, UtilityShip 93, Economy 154, BaseDefense 51, Loot 130 |
| the pilot | Character 393, Progression 263, Unlocks 144, Equipment 239, Items 410, Hints 121, Tour 110 |
| the wire | Net 1246, Rendezvous 765, Link 254, Adapters 72 |
| screens | Ui 241, MainMenu 341, CharacterCreator 223, CharacterSelect 184, StatsWindow 208, SessionMenu 208, EquipmentWindow 203, BasePanel 159, PilotWindow 98, TioWindow 105, RecyclerPanel 114, Radar 171, AbilityBar 84, QuarryBar 95, ReloadBar 126, EscMenu 74, ReturnButton 27, Music 80, Sfx 116, Settings 87, Game 91, Txt/Aim/Plume/HealthBar/Sprites/Shield/EscapePod/Autopilot 330 |

The 12 heaviest edges (call sites / files): `Ui.*` 465/26 · `Net.Sim|IsHost|IsOnline|I|AskHost|FromPlayer|ToHeard` 297/34 ·
`Stats["id"]` 250/15 · `Character.*` 235/24 · `Combat.*` 164/32 · `Missions.*` 151/18 · `Sl("id")` 98/9 · `Yard.*` 89/12 ·
`Targeting.*` 86/21 · `Equipment.*` 79/15 · `Economy.*` 67/11 · `Shots.*` 60/18. Two doors to the world coexist:
`Hub.I` (13 sites/8 files) and `GetParent() as Hub` (14/8), beside `Combat.World`.

## 2 The god files

PlayerShip.cs (2500) mixes six jobs: identity+sheet 23-98, 394-555 · lifts, doors, conditions 132-282 · slots 283-306 ·
turret/wing hosting 308-590 · the HOST side of ~30 abilities 591-1358 (freighters 700-839, timed rows 841-913, Dart
915-1011, Tender 1013-1084, Bastion 1086-1153, heavies 1155-1207, lights 1209-1348) · drive/helm/hook 1360-1470 ·
statuses, web, paint 1491-1536 · the damage door 1581-1685 · the frame + every ability tick 1689-1806 · guns, lob, lance,
reload latch, bores, melee, spend, zones, stance, dash 1808-2147 · helm physics 2149-2323 · net follow + 3 wire codecs
2325-2447 · draw 2469-2499.
Hub.cs (1985) mixes layout tables 44-120 · selection/scope 139-208 · world build 281-488 · session, ships, identity,
held places 490-904 · missions, votes, sectors, arena 906-1195 · spawns, raids, missiles, decoys, world state 1197-1459 ·
clears and pay 1461-1506 · camera, hints, input, HUD line 1519-1887 · 28 `[Rpc]` handlers spread over all of it · side
windows 1948-1984.
Net.cs (1246) mixes the protocol fingerprint 174-324 (reflection over the build) · welcome/beat/watchdog 339-433 ·
session lifecycle 435-579 · the host desk 581-713 and guest desk 715-832 (WebRTC glue) · the copy-paste report 834-892 ·
joining 894-1004 · ~40 player-facing `Say()` strings · NetChannels and NetPose 1160-1246.

## 3 Rule breaks (file:line)

- Type tests: 0 `is Raider` / `is Boss`; 12 `is/as PlayerShip|Shot` remain, all at the damage side split because
  `Combat.Players` is `List<IHittable>`: Shots.cs:310, Lines.cs:105, ThrownRock.cs:64, PlayerShip.cs:836, :1036,
  Prism.cs:128, :130, Missiles.cs:105, Boss.cs:262-263 (`OfType<PlayerShip>`), Hub.cs:1055 and :1119 walk
  `GetChildren().OfType<Shot>()` for missiles instead of a list. Fix: `Tag.Player` filter + an `IPilot` door (~40 lines).
- Special cases by class/boss: 0 `== ShipClass.X` outside the tables. Hidden ones: the paint rides the `guns` slot
  (PlayerShip.cs:1533), the patrol ring is keyed on the id "super" (Fx.cs:537), a Ramp is read by `def.Ramp != null`
  in five places (PlayerShip.cs:186, 963-970, 1769-1777, 2416) instead of one door.
- Duplicated paths: six copies of "if Cool>0 return; Left=time; Cool=Cooling(cd)" that `Run(id,time,cool)` (908)
  already generalises: PlayerShip.cs:1013, 1072, 1213, 1265, 1285, 1300 (~40 lines); FitClass 395-442 and Restat
  516-524 repeat the hull-fraction refit; two "send to peers" doors (Hub.cs:579-590 vs Net.cs:378-383); two tables of
  the four outposts (Hub.cs:72 tuples, Landmarks.cs:83); three arrive-steer movers (Hauler.cs:264-286, Gatherer.cs:249,
  ShipClasses.cs:684) sharing only `Motion.Arrive`.
- Lists/Dictionaries as constant tables: Spawned.cs:117 `List<SpawnKind> All`; Hints.cs:18 `Dictionary All`;
  Sfx.cs:25 `_gap`; Ids.cs:18 `Widths`; Abilities.cs:931 `HashSet Reserved`; the class rows carry 48 per-class
  `Dictionary<string,double>` (Ships.cs:106-124 Nums/Damage/Reach/Cycle). All hashed, so no drift, but not arrays.
- Comments describing old behaviour (invariant C): 186 lines in 40 files match used to / no longer / REPLACED:
  Hub.cs 22 (23-40 is a 17-line ledger of what it "no longer owns"), PlayerShip 10, Net 8, Shots 7, Abilities 7,
  MainMenu 6, Fx 6, Character 6, Boss 6.
- Authority: every `[Rpc]` handler checks its sender or is Authority-mode (28 in Hub, 6 in Net, 2 in PlayerShip, 1 Boss,
  2 Yard); guests never simulate. Gaps: PlayerShip.cs:1128-1137 the shockwave moves a raider's Position while
  Raider.cs:222 flies it back to its squad slot next frame (a throw the squad undoes); DeployedTurret.cs:64 applies a
  status without `StatusSet.Reaches`.
- Invariant D: `PlayerShip.DamageOf` (152) has 0 callers, and `Spec(pd)` reads `Stats["pd_damage"]` (343) -- so the
  CIWS's x3 damage (Ab.Ciws DamageOn) never reaches the mounts. The round-1 red "CIWS run: takes 15.5 (48)" is this.

## 4 How the checks couple to the code: the round-1 reds

Solo: 206 FAIL lines = 108 distinct signatures (runs x3 collapse), 3 lanes threw (FieldsRipYard NRE, WingsGunship and
ItemsDoor ObjectDisposed), one harness abort (`Sequence contains no elements`), 994 `ArithmeticException` (Math.Sign of
NaN: only 4 sites -- PlayerShip.cs:2299 Steer, Turrets.cs:345 Swing, Hauler.cs:283, Prism.cs:105). Six: the identical 206
lines in its solo role plus 40 role lines (guest 15, host 8, third 3, aguest 9, ahost 2). Two seeds, the same 206: 0 flakes.
A throw inside PlayerShip._Process aborts the rest of that ship's frame (abilities, guns, wings, the 10 Hz host report --
the guest role's "host stream arrives at 0.4 a second" and "Space order never reached the host" are downstream of it).
- Thrown-lane / frame cascade, ~48 signatures, ~110 lines: every zero-damage, zero-motion red of the heavies and
  lights -- "the railgun ... DealtBy grew 0.0, the target lost 0.0 (x3)"; "the whirlwind ... the four all round lose
  0 / 0 / 0 / 0"; "the lunge at top speed ... 56.1 u along the nose (420)" (= top speed x 0.3 s: dead reckoning, no
  carry); "a dart fired sliding 112 u/s: it leaves at 0.0 u/s" (no shot at all). One root, one guard, one re-run.
- Stale literal (a later lane changed the number per spec), 14 signatures, ~26 lines: "keys tab lists ... 5 abilities
  + 6 open slots" asserts 11 rows, the drive row (lane B, Drives.cs) makes 12; "a class card prints ... (500" asserts
  300 (Ships.cs:176 says 500); "carrier PD: three turrets" -- pd_count is 2 (Ships.cs:218); "the same source lands
  only once inside 0.35 s" asserts -5 within 0.2 while 0.5%/s regeneration (PlayerShip.cs:65) gives 0.9 back;
  "brace + warp ... a reach of -1" holds V under the 1.0 s spool (Drives.cs:56); "three wings ... a gunship 1970 u
  off -- 0 fighters" places it past control_range 1500 (Stats.cs:186); "every shell lands ... 343.75" fires the
  broadside across the 6a arcs (Ships.cs:206) that hold two barrels.
- Real bug, 10 signatures, ~30 lines: the CIWS damage above (3 x 3 runs); "no two lines of a category share a slot
  and a lean (Utility:@output0.25 x2)" -- salvo_core and magazine_core (Items.cs:165, :168) have the same lean; "more
  bombers ... arrive rearming, not armed (2 -> 5)"; "Supercarrier ... 3 patrol craft out 0.03/0.03/0.03 (0.83 apart)";
  "Point defence ... takes a light at 358 u and lets it go at 755 u" against `Range * 1.15f` (Turrets.cs:190);
  "F8 helm: the anchor warped 2248 u mid-pull: the move ends that frame (None, on True)" (HelmMoves.cs:176).
- Check trap, 9 signatures, ~20 lines: "V held 2.0 s: 827 u (want 800 +-20)" and every "+173/+627 past the ring"
  are +27 u of hull coast counted from the key press (timed from the input); "at level 3: Suppressing fire acts
  (LOCKED . L3 ...)" reads the bar 0.05 s after a refused press while FailShow holds the note 1.5 s (PlayerShip.cs:1539:
  stale pick); "a ramp row ... +0.148 (0.150)" and "mortar ... 1.38 s (1.4)" are one frame (knife edge).
- Not yet classified (the log's tail was cut at 230 chars): 27 signatures, ~20 lines, mostly items/catalogue counts.

## 5 The verdict

A rewrite replaces 26,401 lines of which ~1,300 numbers already sit in ~40 row tables (Classes.All 12 rows x ~25 stat
rows, Ab.* 47, Items 48 lines + 9 conditions, Enemies 6, Waves 11, Unlocks 11, Par 80, Fx 19, Shots 15, Beam 11 ...) and
~260 loose consts. It must re-prove 1,767 checks that read the code's own names (211 `Stats[]`, 98 `Sl(`, 312 table
reads, 4,432 hand-typed literals): every renamed member is a harness edit. At the build phase's pace (a slice a lane,
gated blind) that is 8-12 weeks, and its FIRST engine run would show the same class of reds, because nothing here has
run before round 1: the code is not wrong-shaped, it is untested.
The alternative: (a) one guard at the 4 Sign sites + the ObjectDisposed in two lanes: ~48 signatures; (b) DamageOf
wired at PlayerShip.cs:343: 9 lines; (c) 14 stale literals rewritten to the row (`sh["pd_count"]`, `Drives.Spool`,
`Stats["control_range"]`); (d) 9 traps (a Wait past FailShow, a Vary that never starts on the key frame); (e) the 6 real
bugs above. Splits (section 6) move ~1,700 lines and cross no call edge. Total: ~2 weeks to a green solo, then six.
Recommendation: do not rewrite. Fix the cascade first (one edit, one solo@seed), then the literals, then the 6 bugs;
split PlayerShip and Hub into partials on the way; leave Net, Boss, Yard, Abilities, Rendezvous whole; delete the 186
old-behaviour comment lines in the same commits. Rewriting would buy nothing the 12-class row shape does not already give.

## 6 Globalize, or split into small scripts? (the owner's second question)

1. Literals a check must know vs rows. Rows: ~1,300 numbers in ~40 tables (above). Loose: ~260 `const`/`static readonly`
   numerics (Hub 27, Economy 23, Net 19, PlayerShip 16, Drives 11, Lanes 10, MainMenu 9, Link 8, Hauler 8, Missions 7)
   plus ~1,200 drawing literals (Fx 217, Shots 156, Hub 156, Hauler 139) that no check reads. 83% of what a check can
   assert is already a row. The harness is the other way round: 4,432 typed literals against 523 row reads (11%). Worst
   five loose ones: PlayerShip.cs:72 `HitGap 0.52` (the check says 0.35); PlayerShip.cs:65 regeneration (untabled, breaks
   every "lost exactly N" check); Drives.cs:56-64 Spool/OverCap/PerStep/SnapAt (checks hold V for typed seconds);
   Turrets.cs:190 `Range * 1.15f` (an unnamed hold ratio the PD sweep asserts); Hub.cs:112-118 + TargetDummy.cs:22 the
   practice range's positions and 150 u ring, which ~30 checks stand foes beside by hand.
2. A scenario table (class, peak, foe row, range, bearing, press, wait, expect = a stat id or literal; `solo@scenario=<id>`)
   replaces the set-up of the ~60 `LaneA6*` methods (harness lines 19405-19535, ~9,000 of 25,259 lines: 1,657 `Vary`
   and 277 `new Vector2` place foes by hand) and ~90 of the 108 red signatures' checks; it does not replace the wire
   (six) lanes, the windows, or the economy. One scenario runs in ~20 s against today's 338 s solo.
3. Worth splitting (as `partial class`, 0 call edges cross a partial): PlayerShip.cs 2500 -> PlayerShip 900 (sheet,
   slots, door, frame) + ShipAbilities 660 (700-1358) + ShipHelm 330 (2149-2323, DashCarry, hook glue) + ShipWire 200
   (2325-2447 + SendHostState); Hub.cs 1985 -> Hub 1100 + HubSession 415 (490-904) + HubInput 370 (1519-1887);
   Net.cs: Fingerprint 174-324 (150) to Protocol.cs, Report 834-892 (60) to NetReport.cs. Not worth it: Boss (one
   scheduler), Yard (one economy), Abilities (a table), Rendezvous (a codec), Fx (rows + one node), Ships (rows).
4. Tokens to localize one ability red today: PlayerShip 2,500 + Abilities 1,032 + the lane method ~150 + the class row
   ~60 = ~3,700 lines (~55k tokens) and a 338 s run. After the partial split: ~660 + 30 + 150 + 60 = ~900 lines (~14k).
   After the scenario table: ~700 lines (~11k) and a 20 s run; a stale literal becomes a one-cell diff.
Ranked by tokens saved per week of work: (1) fix the 4 root causes and the 14 literals, 0.5 wk, ends the 206-line
re-reads; (2) the partial-class splits, 1 day, -75% lines per triage; (3) the scenario table + `solo@scenario`, 1.5 wk,
-95% run time per red and -50% of the ability lanes' harness lines; (4) name the 5 loose literals as rows, 1 day;
(5) a rewrite: negative -- it re-spends the 1,767 checks before saving a token.
