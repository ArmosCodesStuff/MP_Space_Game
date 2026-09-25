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
- commit: see `git log` (subject "Walls job 1: ...").
- next: job 2 (F15).
