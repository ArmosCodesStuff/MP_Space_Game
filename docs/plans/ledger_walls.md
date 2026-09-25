# Ledger: lane C (chips and level walls), worktree `WarShips_wt_walls`, branch `wt/walls`

Spec: `level_walls.md` as amended by `kits_v31.md` §3.6 / §6 F15 / §8 lane C and the README rulings
(6 chip slots on every hull, kind caps 3+3, chip_basic deleted, no baking, walls read `Peak`).
No engine runs in this lane: compile rungs only (`typecheck.ps1`, `verify.ps1 -Quick`).

## Decisions (the spec is silent; the plan's default taken)

- D1 Every hull has 6 chip slots: `Equipment.ChipSlots` 5 -> 6, no `ClassDef.Chips` (rulings override level_walls §3.3 item 1).
- D2 `chip_basic` deleted, `Equipment.Default` fits no chips (kits §3.6, decision 7). Not baked into Base.
- D3 Chip kinds from numbers §3.3's table: Combat Chip = Combat; Armour, Engine, Targeting = Utility. `ItemDef.Kind`.
- D4 `AbilityDef.Weapon` (lane A's Abilities.cs: one field + the flag on today's weapon rows): Guns, FireMode, Pd,
  Reload (the missile's R), Attack, Recall, Deploy, Collect. Every other row of a class is walled in list order.
  Today every class carries exactly ONE walled ability (open at L1), so no real press is refused until lane A's
  slice 6 writes three per class in learn order.
- D5 Ability-wall checks on real presses (level_walls checks 4, 5, 13, 14, 16, 17) iterate every flyable class's
  walled abilities generically. Until a class has a 2nd one they print `PEND walls ...` (not PASS); the index ->
  level rule is proved now on lists of real rows in a fixed order (`Unlocks.AbilityWall(kit, def)`, the one path).
- D6 `Character.Peak`: `[progress] peak`, loads as max(peak, level); raised by AddExp, never lowered by Refit.
- D7 `Game.Version` 2 -> 3 (F15 changes what a save holds). Harness files written by hand move to version 3; a
  format-2 file is refused like format 1.
- D8 Wire: `NetIdentity` gains `peak` after `level`; `p.Peak = Progression.Claim(max(peak, level))`, the same
  1..100 cap `Afford` uses; `ApplyIdentity` -> `SetProgress(bought, peak)`.
- D9 Harness roles set `Character.Peak = Unlocks.Top` (Level stays 1, so EXP-by-level checks keep their meaning);
  wall checks set lower peaks themselves. The menu's demo ship reads Top.

## Jobs

1. `Unlocks.cs` table (pilot rows) + `Character.Peak` + `Game.Version` 3 + Peak on the wire.
2. F15: 6 chip slots, kind caps, chip_basic deleted, `Sanitize(c, ids, peak)`, Load's displaced list, EquipmentWindow rows/locks.
3. Ability walls: `AbilityDef.Weapon`, `IGated`, `LockedAt`, `DoAbility`'s wall line, `UseAbility`'s `LOCKED · L3`,
   `SlotState.Locked` on the bar, K window line, unlock cards (`Hints` rows), Pilot window NEXT line.
4. Fold Auto-sell (`Economy.NeedsBoss`) and raids (`Lanes.NeedsBoss`) into `Unlocks.All` (Measure.Boss).

---

## Job 1 PRE
- intent: Unlocks.cs (Measure, Opens, Unlock, All pilot rows, At, Count, Top); Character.Peak saved/loaded/raised;
  Progression.Claim; Game.Version 3; Peak on the identity (Hub, Net.PlayerInfo, PlayerShip.SetProgress/Peak);
  harness: table check, peak save/refit check, version literals 2 -> 3, role setup Peak = Top; Shots role setup.
- start: aa1e4f9285579087ac94f56cb9c02a9dea29700a
- files (hash-object at start):
  - scripts/Character.cs fbe2cc81e905f8e3d2746f55b2a40b3efa1b7efb
  - scripts/Progression.cs 59cec95ddc297b1d7cc49db597056107b7a33dba
  - scripts/Game.cs e1ad46624d1417e4bc549f84499d036c40064726
  - scripts/Hub.cs b1e90e72868d7669eb5d64a11531a4068c4e1061
  - scripts/Net.cs 15e0d9a02d21b7016d720356385d2887a0b1d89d
  - scripts/PlayerShip.cs 6feada710ee6390670383fb070a7da80ce7c72b8
  - tools/smoketest/SmokeTest.cs.txt 00e6f1badf3c7f5922b1134e0a9f9cb1bce834e9
  - tools/screens/Shots.cs.txt 809555bf44e0f93aec0fd407feefb7c1f299f5c8
  - docs/CHANGES.md 3631a5f66e39f19cadbab0fe47a8297093fa8f87
  - scripts/Unlocks.cs (new)

## Job 1 POST
- verdict: typecheck 0 errors; `verify.ps1 -Quick` ALL CHECKS PASSED (UNUSED 0, 0 warnings). Rungs 3/5 owed.
- files: scripts/Unlocks.cs (new), Character.cs, Progression.cs, Game.cs, Hub.cs (identity lines), Net.cs
  (PlayerInfo.Peak), PlayerShip.cs (SetProgress/Peak only), SmokeTest.cs.txt, Shots.cs.txt, CHANGES.md, this ledger.
- rung 3 owed: `WallChecks` (table, peak through level-up/refit/reload x3, peak files x3); SaveCoverage round trip +
  inventory (`Peak`), build stamp 3, format-2 refusal.
- rung 5 owed: Mp host "a guest's highest level reached ... peak 14 at level 1"; handshake literal 3.
- commit: 21712ec "Walls job 1: the unlock table, the pilot's peak, save format 3".
- next: job 2 (F15).

## STOP (context past ~150k): jobs 2-4 go to a FRESH agent that reads this ledger, not the old transcript

### Job 2 plan (F15) -- not started, no PRE written, tree clean at the commit after 21712ec
API (Equipment.cs):
- `ChipSlots = 6`; `public enum ChipKind { Combat, Utility }`; `ItemDef.Kind`; `const int KindCap = 3`.
- `Line(...)` gains an optional `ChipKind kind` (chip_combat = Combat; chip_armour/engine/target = Utility, D3).
- delete `chip_basic` (Build() lines 179-182) and its header lines 11-12; `Default` = 5 core + 6 "" (no chips).
- `Sanitize(c, ids, int peak)`: chip j (0..5) emptied when j >= `Unlocks.Count(Opens.ChipSlot, peak)` or it is the
  4th of its Kind; `Bonuses/Adds(c, loadout, int peak, levels)` -> `Sum` sanitises with peak.
- `ChipFit(string[] l, int peak, ItemDef chip) -> (int slot, string why)`: first OPEN empty slot, else why:
  "at most 3 Combat chips" / "next chip slot opens at level 10" / "every chip slot is full".
Callers to move (scripts): Character.cs:84 (Default), :289-291 Load -> `clean = Sanitize(sc, ids, Peak)`, displaced
when `ById(ids[k]) is {Kit:false} part && clean[k] != part.Id` (covers unfit core, locked slots, kind caps);
CharacterCreator.cs:131-132 (+Character.Peak); EquipmentWindow.cs:83 (6 rows; locked row
`CHIP {k+1}  ·  LOCKED · L{at}` with no button), :106/:110 (EQUIP disabled + TooltipText = why), :180 FitChip via
ChipFit; PlayerShip.cs:322/325 (Bonuses/Adds with Peak), :410 (Sanitize with Peak).
Harness literals that assume 5 kit chips (x1.25) -- rewrite each to the stock number, same edit:
- 706/766/768 chip_basic in hold fixtures -> chip_combat_1 (a real drop); 7073 ReceiveLoot "chip_basic" -> "std_drive"
  (a kit part refused as loot); 846/852 veteran: drop the chip_basic literals (slots 5-6 empty); 887 v1 file text only.
- 2200-2203 creator card bsHull 375 -> 300; 3044 268.5 -> 214.8 (3x4x17.9); 3076 22.375 -> 17.9;
  3237-3239 refit fraction: 375->300, Bulwark III 615->540, 123->108, Glass III 300->225, 60->45, back 75/375->60/300;
  3259 message "312.5 with the five chips" -> stock; 3316 183.75 -> 147 (3x49); 3665 171.875 -> 137.5;
  4108-4114 Worst(): chips count KindCap per kind -> literals -0.47/-0.55/-0.70 become -0.37/-0.45/-0.60
  (message figures 1.575 / 9.845 / 150 and 100); 4133 Describe(chip_basic) -> chip_combat_1;
  4200-4275 rewrite the flow: stock hull `full`; an Armour Chip I from the hold (EQUIP) = full x1.08; UNEQUIP = full;
  other class has no chips; level block: Balanced Array fitted = full x1.28, levelled x1.29 (shares add, chip on);
  hold-is-the-pilot's with the armour chip; 4220 "five parts and six chip slots";
  4529-4531 +6.25 -> +5 (no chips); 4537/4045/4057-4060/2200 add the peak argument;
  8173 175 -> 140; 8179/8451 500 -> 400; 8189 7.5 -> 6; ~8880 Mp guest `gl[8]=gl[9]=""` and "the carrier still had five".
  After editing, grep `chip_basic|x1.25 by|the chips|five chips|22.375|1.25 for` to find stragglers.
New checks (own method `ChipChecks`, rung 3): level_walls 6 (open slots at peaks 1,2,4,8,14 = 0,1,2,3,6 for
Battleship, Warden, Echo, through Sanitize + the sheet), 6b (EQUIP greyed with no open slot, tooltip "next chip slot
opens at level 10"; a 4th Combat chip refused with "at most 3"), 7 (a version-3 file at level 3 with 5 Combat chips
loads 1 fitted + 4 held, owned count equal; files at levels 1/3/14 x capital/heavy/light, plus one with no `peak`);
the kind cap on the host (3 Combat + 3 Utility fit, a 4th Combat emptied). Rung 5: level_walls 15 (a guest with 5
chips at claimed peak 3: host and other guest apply 1). Rung 4: a Shots frame of the window with locked chip rows
(set Character.Peak = 4 before opening I; name it e.g. "eq_chip_walls").

### Job 3 plan (ability walls) -- see level_walls.md §3.3 items 3, 4, 7 and D4/D5 above
`AbilityDef.Weapon` on Guns, FireMode, Pd, Reload, Attack, Recall, Deploy, Collect (Abilities.cs, lane A's file:
field + flags only); `IGated { int Peak; ShipClass Class; }` on PlayerShip (Demo reads `Unlocks.Top`);
`Unlocks.AbilityWall(IReadOnlyList<AbilityDef> kit, AbilityDef def)` = At(Ability, 1-based index among non-Weapon,
non-Open rows) or null; `LockedAt(IGated, def)` = AbilityWall(Classes.Of(g.Class).Abilities, def) when > Peak.
`DoAbility`: `|| Unlocks.LockedAt(this, def) != null` return; `UseAbility`: Fail(id, $"LOCKED · L{n}") before
Refuse; `SlotState.Locked` + `AbilityBar.StateOf` first; bar draws the `_open` box dimmed with `LOCKED · L3`;
StatsWindow K row "opens at level 3"; Hints rows `unlock_<n>` + `Unlocks.Crossed(from, to)` in AddExp; PilotWindow
NEXT line. Checks: table-of-order proof on real rows (D5), live-now "ability 1 and every Weapon row open at level 1"
for 3 classes pressed through DoAbility; the generic refusal/host-gate checks print `PEND walls` until a class has a
2nd walled ability (lane A slice 6).

### Job 4 plan (fold the boss gates)
`Measure.Boss`, `Opens.Economy`, `Opens.Raids`; rows {Boss, 3, Economy, "hauler_autosell"} and {Boss, 1, Raids};
delete `Economy.Upgrade.NeedsBoss` (Economy.cs:82, :39, :119) and `Lanes.NeedsBoss` (Lanes.cs:103); Yard.cs:263,
BasePanel.cs:120-129, Raids.cs:53 ask Unlocks. Grep the harness for NeedsBoss and rewrite each to the table.

## Job 2 PRE (fresh agent, 2026-09-25)
- intent: F15 as planned above: ChipSlots 6, ChipKind + KindCap 3, chip_basic deleted (Default fits no chips),
  Sanitize/Bonuses/Adds take the peak, ChipFit, Load's displaced list, EquipmentWindow 6 rows + locked rows,
  PlayerShip re-sanitises on a peak change; harness old-truth numbers rewritten; ChipChecks; Shots frame eq_chip_walls.
- start: a56424581d8d29ba1a04a0ca605b78925fa772aa
- files (hash-object at start):
  - scripts/Equipment.cs a2c659c3cbd0a126dbfd09f3cbc751587703b298
  - scripts/Character.cs a3d5a3cca069c202e706df7eb704448a71423ad5
  - scripts/CharacterCreator.cs 735d70540bd9efade999021169d5fb485389dbd8
  - scripts/EquipmentWindow.cs 26c89e9ae2ca6093209f4d05343073561d49c434
  - scripts/PlayerShip.cs 0942c026545bf8deb01f6cb164c16b08e427ceb2
  - tools/smoketest/SmokeTest.cs.txt 984bac06b2d2a6f9c90a09baa29e8e0d29be4d9a
  - tools/screens/Shots.cs.txt 199b283e6bec1f46364523af3f97dcde21705507
  - docs/CHANGES.md 6e05268b16ce178f177bc1cc3e62cc7449bd60ac

## Job 2 POST
- verdict: typecheck 0 errors; `verify.ps1 -Quick` ALL CHECKS PASSED. Rungs 3/4/5 owed.
- files: Equipment.cs (ChipSlots 6, ChipKind/ItemDef.Kind/KindCap, chip_basic gone, Default no chips,
  Sanitize(c, ids, peak), ChipFit, Bonuses/Adds(.., peak, ..)), Character.cs (Load: displaced = whatever
  Sanitize with the peak empties), EquipmentWindow.cs (6 rows, locked rows, EQUIP via ChipFit + tooltip),
  PlayerShip.cs (`_fitted` kept whole, `Loadout` = Sanitize at Peak, so a peak rise applies a chip already
  fitted), CharacterCreator.cs, Ships.cs (comments), SmokeTest.cs.txt, Shots.cs.txt, CHANGES.md.
- D10: the plan's Worst() message figures "150 and 100" were wrong arithmetic: -60% leaves 120 of a
  battleship's 300 and 80 of a carrier's 200; those are written.
- D11: the unclaimed-loot fixture (:768) gets `std_drive` (a kit part, refused), not chip_combat_1 (a real
  drop would be kept and fail "no kit").
- D12: level_walls check 15 (rung 5) is the THIRD player (guest2, a battleship): it claims peak 3 with five
  chips (armour, combat, combat, armour, engine) -> 324 hull on the host, itself and Guesty (318 if all five).
  Guesty's gear is now four Armour Chips I (the host holds it to 3: 248, not 264); the arena guest's
  destroyer two Armour Chips I (290).
- rung 3 owed: `ChipChecks` (3 classes x: open slots/sheet, the Utility cap, ChipFit, 3 save files; +1 no-peak
  file); Solo equipment block (EQUIP/UNEQUIP Armour Chip I = full x1.08; Balanced x1.28 / x1.29 levelled; hold
  is the pilot's; save round trip with the chip; EQUIP greyed at peak 8 "at most 3 Combat chips" / "next chip
  slot opens at level 10", `CHIP 4  ·  LOCKED · L10`); every rewritten number (CHANGES entry lists them).
- rung 4 owed: frame `58b_eq_chip_walls` (read by eye once: two chips, four LOCKED rows, greyed EQUIP); the
  sweep now draws 109 frames (the Handoff's "108" is the main session's to update).
- rung 5 owed: host "a guest at peak 3 with five chips fitted flies one: 324"; third player's own 324; Guesty's
  view of the third player 324; Guesty "hull replicated ... 248"; arena host 140 / 400 / deploy 6 / guest
  destroyer 290; arena guest 400.
- note: the -Quick run began in a gap between the main session's engine runs; a solo smoke run started while
  it was going (separate folders; nothing shared).
- commit: (this commit) "Walls job 2 (F15): six chip slots, the kind caps, no starting chips".
- next: job 4 (small, independent of 3), then job 3 for a fresh agent.

## Job 4 PRE (done before job 3: independent and small, D13)
- intent: Measure.Boss, Opens.Economy/Raids, rows {Boss,3,Economy,hauler_autosell} {Boss,1,Raids}, Unlocks.Boss(); delete
  Economy.AutoSellBoss/Upgrade.NeedsBoss and Lanes.NeedsBoss; Yard/BasePanel/Raids ask Unlocks; WallChecks table + lane-clock check.
- start: 9057fd1dff386461014549302815458b9951ccc7
- files (hash-object at start):
  - scripts/Unlocks.cs 01c5b300e4706e10f2677cb11c38630fe1dc46f9
  - scripts/Economy.cs ad48e53beee3f329f4d66b3ecb0627f5efa23ef0
  - scripts/Lanes.cs 0125e73ad6c5b2cb4f54c20446d154ef5727135e
  - scripts/Raids.cs 042302daf0f32cfb712a1338d94a433098339871
  - scripts/Yard.cs 399f4aeaa11c7587a439e659ad5a219f2b60958b
  - scripts/BasePanel.cs 50f2f886daf0a3432e628e76378a87d046347105
  - tools/smoketest/SmokeTest.cs.txt d36a9260194bbb8f6499ef57a46918d7287c5d84
  - docs/CHANGES.md 4685f69782457cf61e729ba876f536ae1a51eb1d

## Job 4 POST
- verdict: typecheck 0 errors; `verify.ps1 -Quick` ALL CHECKS PASSED. Rung 3 owed.
- files: Unlocks.cs (Measure.Boss, Opens.Economy/Raids, Unlock.Id, two rows, `Unlocks.Boss(what, id)`),
  Economy.cs (AutoSellBoss and Upgrade.NeedsBoss deleted), Lanes.cs (NeedsBoss deleted), Raids.cs :53,
  Yard.cs :263, BasePanel.cs :120, SmokeTest.cs.txt (WallChecks), CHANGES.md, this ledger.
- rung 3 owed: WallChecks "the base's walls are rows of the same table" (3, 1, 0) and "the lanes' blockade
  clock" x3 (bounty boss 0 waits at 5.0; 1 and 4 run to 4.5); the existing Auto-sell checks unchanged
  (:2632-2660 LOCKED until boss 3; rung 5 :9380 the owner's record).
- D13: job 4 was done before job 3 (independent, small; job 3 needs a fresh context).
- next: job 3 (fresh agent). This agent stopped at the job boundary (context past ~150k).

### Job 3 map (read by the job-2/4 agent; line numbers at the job-4 commit)
- `AbilityDef` scripts/Abilities.cs:33 (lane A's file: add `public bool Weapon;` only, and the flag on the rows
  Guns :82, FireMode :89, Pd :117, Reload :148, Attack :159, Recall :173, Deploy :203, Collect :219).
  `SlotState` is Abilities.cs:31 {Line, Lit, Fail, Busy}: add `Locked`.
- `Abilities.For(c)` (:376) = `ClassDef.Abilities` + six `Open` rows (Open = true): AbilityWall skips Open AND
  Weapon rows. Class rows: Ships.cs :179 :204 :230 :270 :306 :343 :375 :407 :448 :479 :509 :543; today each has
  exactly ONE walled row (Broadside, Bombers, Missile, Bubble, Overdrive, Shockwave, Railgun, Rush, Hunters,
  Roll, Echo, Stealth) -> index 1 -> L1, so nothing real is refused yet (D5's `PEND walls`).
- PlayerShip: `Peak` :376 (SetProgress :377), UseAbility :455 (Refuse -> Fail at :464), RequestAbility :470,
  DoAbility :486, Fail :822, `Demo` :33 (reads Unlocks.Top for IGated).
- AbilityBar.StateOf :30: keep FailNote first (a refused press flashes `LOCKED · L3`), then return the Locked
  state before def.State.
- Hints: Progression.AddExp :160-173 (`Hub.I?.Hints?.Meet("pilot")`), for `Unlocks.Crossed(from, to)` cards.
- The Solo role's pilot flies at Peak 14 (D9); the third player (guest2) now claims peak 3 (job 2, D12): it
  presses only F (broadside, ability 1) today; if lane A gives the battleship a 2nd/3rd walled row, its
  presses stay legal at 3 only for abilities 1-2.

## Job 2b PRE (the coordinator's rung-3 red at 4e7f0b0, seed 11400714819323464263: 5 old-truth FAILs job 2 missed)
- intent: rewrite to the no-chip truth with literals, never loosened: the K window's battleship DPS lines
  (35.80 / 8.95 a barrel, broadside 214.8 every 15.0 s = 14.32 DPS, total 35.80 + 14.32 + 1.00 = 51.12), the
  close-in missile burst (183.75 -> 147 = 3 x 49), the kit count (23 -> 22: chip_basic gone). Then grep for
  other x1.25 literals (done: 6787/7345 are the Drake's scrap, 2540/3836/5840/9017/9482 economy or parts).
- start: 4e7f0b0386a408e0c2c3e9985db586f3ce734621 (job 3 not started: no edits in the tree)
- files (hash-object at start):
  - tools/smoketest/SmokeTest.cs.txt 0a3c6a4e0dd7d5a78eb3c280c6fadc6733280a96
  - docs/CHANGES.md a47cc48dbd9c03072d287cd3e1c42428014f73a6

## Job 2b POST
- verdict: `verify.ps1 -Quick` ALL CHECKS PASSED (0 errors, 0 warnings, UNUSED 0). Rung 3 owed on the five below.
- files: tools/smoketest/SmokeTest.cs.txt (:3008-3015, :3443-3445, :4077-4080), docs/CHANGES.md, this ledger.
- rung 3 owed: "stats tab: the stock main guns ... 35.80 DPS, 4 barrels of 8.95"; "stats tab: the broadside, 3 volleys
  of 4 shells x 17.9 = 214.8"; "and the total adds up ... 35.80 + 14.32 + 1.00 = 51.12"; "a target 150 u off the nose
  ... 3 x 49 = 147"; "the gear: 246 drops ... a 22-part kit". Rungs 4/5 not yet run at all (may hold more).
- commit: see git log ("Walls job 2b: five old-truth checks rewritten to the stock ship").
- next: job 3 (no PRE yet).

## Job 3 PRE (the ability walls; fresh agent, after job 2b)
- intent: AbilityDef.Weapon (+ flags on Guns FireMode Pd Reload Attack Recall Deploy Collect) and SlotState.Locked; Unlocks: Walled,
  AbilityAt(kit, def), LockedAt(class, peak, def), Locked(at), Nth, Crossed, Card, Next, Unlock.Hint; PlayerShip UseAbility
  (LOCKED · L3 before Refuse and Local) + DoAbility's wall line; AbilityBar lock; K row "opens at level N"; Hints.Card
  (unlock cards from the table); AddExp meets Crossed; Pilot window NEXT; chip row via Unlocks.Locked; the menu ship at Top.
  Harness: WallChecks table proofs + WallChecksLive (Solo) + guest2 rung-5 presses (PEND until a 2nd walled ability);
  Shots 6c_k_keys_walls, 57b_walls_next_card. D14: no IGated (see POST).
- start: 932e7f9d8105a97f949b05a85359537022a9d7a8
- files (hash-object at start):
  - scripts/Abilities.cs 8807eee5b4bf1e263b8762db6cd76a68efacb427
  - scripts/Unlocks.cs ccf62ed1b7042f33810f8a10d91c877e5f2481c0
  - scripts/PlayerShip.cs 13af950a3d56f05fc6bc100a4e4d7355221b74d4
  - scripts/AbilityBar.cs 6707b57c493ef01e85843ae1b1fda3d21761b793
  - scripts/StatsWindow.cs c5834debd8b3c60d9fd0a456a7c0a6e8be412e50
  - scripts/Hints.cs b689a3f8cad27fbd62746631722874ed96bdfe4a
  - scripts/Progression.cs 780f3c9b67133bc48136fedd4aa578d628f45103
  - scripts/PilotWindow.cs 81b69f416bcbdb552883755ce134c788c5e0c1e4
  - scripts/EquipmentWindow.cs f3479e0522d832cb12541b39ab787734f102b70b
  - scripts/MainMenu.cs aa3be5beae23e91642bf131068c3c9e0ce7cb636
  - tools/smoketest/SmokeTest.cs.txt 294d3ec4db4979835f2f614348a21001e06e5602
  - tools/screens/Shots.cs.txt 6a6ff2f47dc066e8c707e6074b1d39d37250cd0b
  - docs/CHANGES.md 6aacd7b0319d3657d997860635c4f1ac875fe1aa
  - docs/DESIGN.md 8d15c0e26f08e71e8b96c10151871587e72eea15

## Job 3 POST
- verdict: typecheck 0 errors; `verify.ps1 -Quick` ALL CHECKS PASSED (0 warnings, 0 analyser findings, UNUSED 0). Rungs 3/4/5 owed.
- files: Abilities.cs (`AbilityDef.Weapon` + the flag on the 8 weapon rows, `SlotState.Locked`: lane A's file, nothing
  else), Unlocks.cs (Walled, AbilityAt, LockedAt, Locked, Nth, Crossed, Card, Next, Unlock.Hint), PlayerShip.cs (UseAbility's
  wall line before Local/Refuse, DoAbility's wall line, one comment: nothing else), AbilityBar.cs (StateOf, the locked
  slot), StatsWindow.cs (K row "opens at level N"), Hints.cs (`Hints.Card`, two sentences, a duplicated comment removed),
  Progression.cs (AddExp meets `Crossed`), PilotWindow.cs (NEXT beside the points), EquipmentWindow.cs (chip row via
  `Unlocks.Locked`), MainMenu.cs (the demo at peak 14), SmokeTest.cs.txt, Shots.cs.txt, CHANGES.md, DESIGN.md, this ledger.
- D14: no `IGated`. Two of the four readers are the pilot (static `Character`: the Pilot window, the cards), not a ship,
  and PlayerShip's class line is lane A's; `Unlocks.LockedAt(ShipClass, int peak, AbilityDef)` serves all four.
- D15: an unlock card is no row of `Hints.All`: `Hints.Card(id)` reads a fixed row or `Unlocks.Card(id, class)`, so the
  words name the class flown (its key, its ability) and the table stays the one list. Ids `unlock_ability_2`,
  `unlock_chipslot_1` (`Unlock.Hint`: stable under reordering, which a row index is not). A crossed row the class has
  nothing behind (an ability 2 today) queues no card.
- D16: the rung-5 wall checks are the THIRD player's (level 1, peak 3, battleship): 13 = ability 3 sent past its own
  refusal (the spec's "level 1, ability 2", one wall up); 14 + 17 = ability 2 acts at level 1 because the host reads
  the peak (17's refit is the same claim: level below the wall, peak above it); 16 = AddExp to level 6 and ability 3
  pressed in the same frame, at the end of its session (identity and request share channel 0, reliable). Each is
  judged on the pressing guest's own copy (the host's report); the other guest's view is not checked separately.
  They sit inside the third's existing 3 s after joining (PEND spends nothing, so its timeline is unchanged today).
- D17: check 3's gunship is 300-600 u off AND inside 0.9 x the gun's reach (the Echo reaches 500).
- rung 3 owed (NEW): WallChecks "never walled: the guns, fire mode ..."; "walls by place in the list" x3; "all 12
  classes at level 1 ..."; "every pilot row has a card of its own"; "<class>: the card for chip slot 2 ..." x3
  (battleship, warden, echo); PEND line for check 3. WallChecksLive (Solo, after "2600 EXP from level 1"): "<class>:
  2600 EXP from level 1 reaches level 3 and queues the chip slot 1 card first" x3 (destroyer, warrior, carrier) +
  PEND x3; "the PILOT window names the next wall"; "<class> at level 1: nothing on the bar locked ..., its guns land
  ..., ability 1 acts through the host's gate" x3 (battleship, hauler, echo); PEND x6 (checks 4, 5). MenuDiorama:
  "...nobody's pilot, so it waits for no level: every wall open, peak 14". Two seeds.
- rung 4 owed: frames `6c_k_keys_walls` (today = 5_k_keys at level 1: nothing locked yet) and `57b_walls_next_card`
  (PILOT window with "NEXT  LEVEL 4 · CHIP SLOT 2" beside the points; card "LEVEL 2 · CHIP SLOT 1"); read by eye once;
  LINT 0 (the NEXT label must not widen the PILOT window into anything). Sweep: 111 frames.
- rung 5 owed: the third player prints "PEND walls" twice (GuestWalls, GuestLevelsAndPresses); nothing else changes.
- owed at lane A's slice 6 (a class with abilities 2 and 3): every PEND above goes live -- run rung 3 then rung 5, and
  the two mutants: drop DoAbility's wall line (check 5 at level 5 and check 13 must FAIL); drop the `displaced` line
  in Character.Load (ChipChecks' files must FAIL).
- commit: see git log ("Walls job 3: the ability walls")
- next: lane C's jobs are done; the rungs above are the main session's.

---

## Adversarial review, 2026-09-25 -- 7 findings confirmed (findings_walls.json), fixed one job each

### Step 0: merge version-l (8 docs-only commits since aa1e4f9) into wt/walls
- `git merge version-l --no-edit` at 58d7d1a: clean, no conflicts (CLAUDE.md, docs/CHANGES.md,
  docs/DESIGN.md, docs/plans/README.md all auto-merged). Commit f4bb52d "Merge branch 'version-l'
  into wt/walls".

### Fix 1 PRE (finding 1, major -- Unlocks.All never reached Net.Fingerprint)
- model: sonnet
- intent: make `Unlock` a type `Net.Plain` already hashes without touching scripts/Net.cs: a record
  class (its compiler-made `<Clone>$`), not the readonly struct. Extend "THE HANDSHAKE SEES THE
  TABLES" to prove a wall level and a boss gate literal are in what is hashed.
- start: f4bb52d5edc52eb195f50c3f13d3a259a58ae88e
- files (hash-object at start):
  - scripts/Unlocks.cs 25c1c88a682f0aa1505f366fb1a4959b4f168c1a
  - tools/smoketest/SmokeTest.cs.txt f3481bcb8ae223869583a9c8a9f89382a3de2e81

### Fix 1 POST
- verdict: typecheck 0 errors. `Unlock` is now `public sealed record class Unlock(Measure By, int
  At, Opens What, int Nth, string Id = null)` with an explicit `ToString() => "{By}/{At}/{What}/{Nth}/{Id}"`
  (deterministic text for the hash, not the compiler's default record format) -- routes through
  `Plain()`'s `<Clone>$` check, not `Table()` (no parameterless ctor), same as before for every
  external `u.By`/`u.At`/... read (now auto-properties, same names). No caller outside Unlocks.cs
  constructs an `Unlock` directly (grepped clean).
- files: scripts/Unlocks.cs (struct -> record class), tools/smoketest/SmokeTest.cs.txt (handshake
  check now also asserts `Unlocks.All=` contains "Pilot/3/Ability/2" (ability 2 at level 3) and
  "Boss/3/Economy/0/hauler_autosell" (Auto-sell after boss 3), both literals from level_walls.md).
- rung 3 owed: the rewritten "the build fingerprint covers ... and the unlock table" check.
- rung 5 owed: none new (no wire shape changed; the fingerprint's own value changing is what rung 5
  a moment ago (job 1) already proves the build-mismatch refusal with).
- commit: (next) "Walls fix 1: Unlocks.All reaches the build fingerprint".
- next: fix 2 (finding 2, major -- guest2's restart fixture).
