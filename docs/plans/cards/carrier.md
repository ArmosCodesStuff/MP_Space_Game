# CARRIER

## Identity
Capital. Hull 425. Top 99 u/s (`ShipStats.CarrierTop`; thrust half of it, turn radius 140, turn rate 0.9).
Never strafes (strafe_speed 0). Role: "fights only through its craft" (kits_v31 §2). No main gun (`Fit.Wing
| Fit.Pd`). Drive: **WARP** (capital-only, README/kits_v31 §3.4): 1.0 s spool, range grows 800 u/s
(warp_rate), safe to 2400 u (warp_safe, 4.0 s hold), overshoot +900 u, disabled 2 s / 300 u over, cooldown
20 s (warp_cooldown). Craft already out fight on through a jump's disable; the patrol is leashed to the
carrier and flies back after a jump (~7 s from 2400 u); the gunships' 3000 u leash is checked at launch
only (kits_v31 §3.4 table).

## Primary — Fighters: attack (Space)
3 fighters (`fighter_count`), 3.5 dmg every 0.35 s each = 10.0 DPS/craft, 25.0 DPS sheet total, 300 u
weapon range, 352 u/s, firing 15 s then resting 3 s docked. Sent at the selected target within 1500 u
control range (`control_range`); R **recalls** them home. Source: kits6a A6a-4 (fighter_damage 3.5,
replacing v1's 2.5).

## Abilities (learn order F L1 → E L3 → Q L6 — note the order: E before Q, level_walls.md line 13/52)

**F — Bomber strike.** Key F, cooldown = rearm 6 s (`bomber_rearm`) once all 2 bombers (`bomber_count`) are
spent. 2 bombers run straight at the target and launch 4 torpedoes each (`bomber_ammo`) 0.5 s apart
(`torpedo_interval`), 60 dmg each (`torpedo_damage`), 300 u/s, 2600 u run, launching inside 1900 u
(`launch_range`); no tracking. Source: kits_v31 §3.8 ("unchanged"), numbers kits6a A6a-4 (torpedo_damage
60, replacing v1's 137.5). What it does: an alpha-strike run that must close to launch range first.

**E — Warp gunships.** Key E, cooldown 25.0 s (`gunship_cooldown`) from the press. Up to 3 picked targets
(else the selected one), each within 3000 u (`gunship_leash`); 2 gunships (`gunship_count`) warp onto each
and circle it at 240 u (`gunship_orbit`) for 12 s (`gunship_time`), 2.5 dmg every 0.25 s = 10 DPS each.
Refused NO TARGET / OUT OF RANGE (a pick past leash) / COOLING. Source: kits_v2 (unchanged through v3.1),
numbers kits6a A6a-5. What it does: splits fire onto up to 3 separate targets at once, each pinned by two
small craft rather than the wing's main attack.

**Q — Supercarrier.** Key Q, cooldown 30.0 s from the END (`super_cooldown`, `CoolAfter`), never refused
but RUNNING / COOLING. A second, automated wing (the fighter's own numbers) circles the carrier at 300 u
(`patrol_orbit`) for 20 s (`patrol_time`), engaging **whatever `Turret.Rank` puts first inside 600 u**
(`patrol_range`) of it — **missiles included** (the Lancer's trident seekers, the siege cruise missile),
then lights/fighters, then heavies/bosses/structures, a dummy only as fallback. Source: README ("Carrier Q
= SUPERCARRIER: a second, automated wing... engaging anything within ~600 u... missiles included"),
kits_v31 §3.2 (this is the section's one change from v3: v3 excluded missiles), numbers kits6a A6a-6. What
it does: a timed second wing that guards the carrier itself rather than attacking on command.

## Interactions (kits_v31 §7)
- **CV patrol vs BB CIWS vs FR sentries vs PD**: accepted, split by carrier/reach/time — the patrol is
  craft flying a 600 u ring, 20 s of every 50; all four rank by one `Turret.Rank`.
- **CV patrol vs CV gunships**: as v3, split by anchor (the patrol rings the carrier; gunships go to picks).
- **Capitals' warp vs web-breakers**: a jump drops any web on the carrier itself (latch within 12 u); craft
  already launched are unaffected.

## Rulings touching this class (docs/plans/README.md)
- "hulls: BB 500 / CV 425 / DD 395."
- "Carrier Q = SUPERCARRIER: a second, automated wing patrolling the carrier, engaging anything within
  ~600 u (range moddable) -- missiles included."
- "WARP... is CAPITAL-ONLY (BB, CV, DD). Capitals are slower overall and rely on it."
- Level walls: F Bomber strike L1, **E** Warp gunships L3, **Q** Supercarrier L6 (the Carrier and the
  Warrior are the two classes whose keys don't read F/Q/E in that order; bar stays F E Q by default,
  level_walls.md "keep your keys").
- "The Carrier is the one real cost [of the walls]: only 28% of its damage (its bombers) is open at L1"
  (kills in 86 s at L1 vs ~59 s from L6, kits_v31 §3.6).

## BUILT
`scripts/Ships.cs` `ShipClass.Carrier`: `Drive = Drives.Warp`; `Nums` hull 425, thrust CarrierTop/2,
max_speed CarrierTop (99), turn_radius 140, turn_rate 0.9, pd_count 2; `Rows` gunship_cooldown 25,
super_cooldown 30; `Abilities = { Ab.Attack, Ab.Recall, Ab.Bombers, Ab.Gunships, Ab.Supercarrier }` (learn
order F E Q, matches the walls). Fighter/bomber/gunship/patrol numbers are class-shared defaults in
`scripts/Stats.cs` under `Fit.Wing` (fighter_damage 3.5, torpedo_damage 60, gunship_orbit 240, gunship_time
12, gunship_leash 3000, patrol_range 600, patrol_orbit 300, patrol_time 20) — the Carrier is the sole
`Fit.Wing` hull, so these are one truth, not a per-class override.

Ability ids: `bombers`, `gunships`, `super` (`scripts/Abilities.cs`; note "super", not "supercarrier").

Check methods (all present in `tools/smoketest/SmokeTest.cs.txt`, wired into the solo list ~18614/rung-5
lists ~22693): `LaneA6aCarrierRowChecks`, `LaneA6aGunshipChecks`, `LaneA6aGunshipFireChecks`,
`LaneA6aSuperChecks`, `LaneA6aSuperMissileChecks`, `LaneA6aSuperWingsChecks`,
`LaneA6aGunshipGuestChecks` (rung 5). Frame (`tools/screens/Shots.cs.txt`): `85c_cv_supercarrier`.
Warp/strafe, shared with BB/DD: `LaneBWarpChecks`, `LaneBStrafeChecks`.

## OPEN
- None found. The Carrier's numbers, ability ids, learn order and the Supercarrier's missile inclusion all
  match the newest spec (kits_v31 §3.2, README) exactly against the built code and its named checks.
