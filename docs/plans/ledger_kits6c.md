# Ledger: kits lane A, slice 6c (the heavies: Warrior, Sniper, Warden)

Writer: one agent at a time in worktree `WarShips_wt_kits6c`, branch `wt/kits6c`, started at a8a5e81 (version-l).
Spec (newest wins): kits_v2 §3 cards (WARRIOR / SNIPER / WARDEN) + §5 → kits_v3 §3.4 / §3.5 + §5 → kits_v31 §2,
§3.4 (the drive vs every move), §3.6 (walls), §6, §7, §8; `sniper_active_reload.md` (the Sniper's piece);
numbers_curve_raids_items.md §8 (owns no heavy-kit number). Rulings: version-l's docs/plans/README.md.
Conventions and D1-D37: ledger_kits.md (D1-D27), ledger_kits5.md (D28-D37). BUILD PHASE: no engine run; per job
typecheck + verify -Quick + a read of the own diff; every check written now, run in the final test phase.
A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits, redo it first.
Checks go in NAMED methods `LaneA6c<Thing>Checks` in SmokeTest.cs.txt (solo) and `LaneA6c<Thing>Frames` in
Shots.cs.txt; rung-5 pairs `LaneA6c<Thing>HostChecks` / `...GuestChecks`. SmokeTest.cs.txt is LF: write it in binary.
6.7: every ability >= 3 distinct checks (effect with the spec's literals; each kits_v31 §7 interaction; guest
role or named frame), each from 3 varied situations (Vary / VaryAngle / VaryNear).

## Decisions taken where the spec is silent or superseded (D38 on)

- **D38 Overcharge is NOT built** (README ruling; ledger_kits D27): the Sniper's piece is the ACTIVE RELOAD. The
  task prompt's "Overcharge" is superseded. No `rail_over_*` rows.
- **D39 The open question's default:** a perfect press does NOT finish the reload (it only enhances the round, x1.5).
- **D40 Hulls:** Warrior 300, Sniper 240, Warden 270 (kits_v2 cards; README "heavies 240-300"). Today all three
  read 140. Only these three rows move; any harness check asserting the heavies' 140 is rewritten (6.3).
- **D41 Learn order (ClassDef.Abilities, the walls read it):** Weapon rows first (never walled), then
  Warrior {Lunge E, Whirlwind Q, Prism stance F}; Sniper {Anchor F, Tether mine Q, Flares E};
  Warden {Hunters F, Taunt Q, Flak curtain E} (kits_v31 §3.6 table).
- **D42 Replaced in the same edits (invariant C):** Warrior's twin cannons + FireMode + Rush (rush_* and emp_*
  rows, StartRush / RushEmp, Dps.Emp, Ab.Rush, kit parts warrior_main_guns / heavy_rush_drive); Sniper's light
  cannon + FireMode + the F railgun and its lock (rail_cooldown, Ab.Railgun on F, ChargeRail); Warden's light gun
  + FireMode (the proximity flak replaces it). HeavyCannon kit part goes when no class carries it.
  Items.cs @output / @area / @duration lists name rush_time / emp_* (lines ~100-104): remove those ids there,
  add the new ability rows' ids to the same lists (blade_damage, whirl_*, lunge_*, taunt_time, curtain_*,
  tether_*, anchor_time, flare_*) so the items' category doors still reach the heavies. `Dealt.Emp` / Fx "emp"
  stay only if something still reads them (6d's EMP is the Echo's; the analyser decides).
- **D43 The Warden's primary, Proximity flak (Space, Weapon):** its main gun keeps the turret path but fires the
  F5 `Flak` shot row (D24: lands with its class): bursts when within `flak_fuse` 70 u of a hostile (or at
  `main_range` 700 u), damage in the fuse radius, x0.75 on a Boss tag (a row field, not a type test), 45 DPS
  at the sheet (main_damage / main_interval). PD x1 unchanged.
- **D44 The Flak curtain is a ZONE row** (new `Zones.cs`, named for the mechanism; v2 §5 F9 "Zones gain a Bar
  shape"): ZoneDef {Id, Length, Width, Time, Arm, First, Tick, Every, Prey(TargetFilter), Weapon}; host tick
  deals First on entry then Tick every Every to Prey inside the capsule, through Dealt (credit `curtain`);
  every peer draws it from ONE raise on an appended Spawns row (the decoy's pattern, NetIds space appended at
  the END). Row `curtain`: 500 x 80 u, 6 s, live 0.5 s after the press, 20 then 10 / 0.5 s, Light|Heavy only
  (no boss, structure, missile, ally), cursor clamped 150-700 u, perpendicular to the aim, cooldown 18 s.
- **D45 The Tether mine (v1, numbers only in kits_v2: "2 charges · 170 u · holds raiding craft 3 s"):** Q drops
  a mine at the stern (default; nothing says the cursor); it arms 0.5 s later; the first raider (Light|Heavy,
  never a boss) within 170 u sets it off and every raider within 170 u is held (Status.Disabled) 3 s; 2
  charges, each recharging 12 s (default: no v1 cooldown survives in the repo); at most 2 mines out, the oldest
  goes. A mine is a Zones row with a trigger (`tether`), so it rides the same raise and wire (D44).
- **D46 Lunge:** a fixed 420 u along the nose in 0.3 s (kits_v31 §3.4: never priced from speed), owner-side
  motion like the F8 payload; the host sweeps the path and strikes each body once for 40 (credit `lunge`),
  and the Warrior is Hardened x0.5 for the 0.3 s. Cooldown 7 s. It ends a Prism stance (kits_v2 card).
- **D47 Whirlwind:** 2 s, Melee row `whirl` (210 u, ±180°, Stops 0) ticking 10 every 0.25 s (40 DPS);
  on the press every web on the Warrior drops and no web can take it for the 2 s ("clears and blocks webs":
  a host-only Status appended at the END of the enum, read where Pinned is applied); cooldown 14 s; no blade
  swings while spinning; it ends a Prism stance.
- **D48 Blade (Space, Weapon, Hold):** Melee row `blade` (160 u, ±55°, Stops 0), 26 every 0.40 s (blade_damage,
  blade_interval through Cadence), host-struck while Trigger is held; no swings in stance or spin.
- **D49 Prism stance (F):** 2.0 s (prism_time), Status.Parrying for the run, Hold 0.5 (share after the sum:
  boosted 0.75), press F again to drop, Q or E ends it, cooldown 12 s from its END (prism_cooldown), the
  split tick 0.75 s and at most 3 splits a stance (Prism.cs has the bands / split / walk; the tick and the
  cap are this job's). Fx row `warn_beam` for the clip, appended.
- **D50 Anchor (F):** up to 8 s (anchor_time), Hold 0 (so Drives' ANCHORED refusal already applies to the
  boost: kits_v31 §3.4 is met by the generic Held rule, Drives.cs:154-159 -- the check is still written),
  RateStat anchor_rate 2.5 (runs the charge AND the reload through Cadence), reach x1.4 (anchor_reach: the
  rail's reach reads it while anchored), 0.3 s release (anchor_release), press F again to release, cooldown
  12 s from the release.
- **D51 Flares (E):** calls the existing `Hub.Flares(at, heading)` (Decoys.cs row `flares`), cooldown 16 s
  (flare_cooldown), never refused.
- **D52 Taunt (Q):** taunt_time 6, taunt_reach 1000, taunt_mult 1.5, taunt_cooldown 20, taunt_guard 0.67.
  Press: every raider squad whose member stands within 1000 u or hunts a target within 1000 u is Called
  (Squad.Call, kits5-J6) for 6 s; the Warden is Hardened(6, 0.67); Fx.TauntRing raised at the reach; the
  field row `taunt` (already in Fx.cs, keyed on slot "taunt") draws the shimmer and "−33%" from the slot.
  x1.5: `PlayerShip.Outgoing` adds a row -- a target whose CalledBy is this ship takes x taunt_mult (a stat on
  the dealer's sheet, 1 on every other class: reached by the stat, not by class). Emplacement guns prefer a
  Warden in range while its taunt runs (the Prefer filter), if the emplacement chooser has a Prefer hook;
  otherwise recorded as owed.

## Jobs (foundations before their users; each: PRE, edit + checks, typecheck + quick, POST, commit)

- **kits6c-J1 Warrior row + Blade** (D40-D42, D48): ClassDef row (hull 300, Fit none of Guns, Weapons Dps.Blade),
  Melee.All rows blade + whirl, Ab.Blade (Space), the host blade tick; Rush / cannons / emp deleted with every
  caller (Abilities, PlayerShip ~756-775, Ships, Stats ~370, Statuses:53, Items ~100-104, SmokeTest ~19 lines,
  Shots.cs.txt). Checks LaneA6cBladeChecks (26 per 0.40 s in the arc at 3 VaryAngle / VaryNear spots, nothing
  behind or past 160 u; 65 DPS on the sheet), the walls' order check rewritten (Warrior lunge, whirl, prism).
- **kits6c-J2 Lunge** (D46). Checks LaneA6cLungeChecks (420 ± 5 u in 0.3 s at 3 headings; 40 to each of 2-3
  bodies on the path once; half damage taken inside the dash; 7.0 s cooldown; ends a stance; boosted it is
  still 420 u -- the drive interaction), rung-5 guest lunge within 20 u of the host.
- **kits6c-J3 Whirlwind** (D47). Checks LaneA6cWhirlChecks (40 DPS ± 1 to bodies all round inside 210 u, 0 past;
  a latched webber lets go and cannot re-latch for 2 s; cooldown 14; no blade while spinning; ends a stance).
- **kits6c-J4 Prism stance** (D49; kits_v2 Proves: bands, SQUARE 37.5 / 12.5, SLANT at 40°, no re-split, tick
  at 1.9 s caught / 2.1 s lands 50, drop -> 12.0 s cd, x0.5 speed and boost x0.75, no blade, projectiles,
  rays, cursor at 120° clamps to 90°). Checks LaneA6cPrismStanceChecks, frames (wedge, SQUARE clip, SLANT fan)
  LaneA6cPrismFrames, rung-5 LaneA6cPrismHost/GuestChecks (the guest draws, the host decides).
- **kits6c-J5 ActiveReload foundation** (sniper_active_reload.md §2.2, §3): Net.Leeway, PlayerShip.SendInterval
  public, ActiveReload.cs (ReloadSpec, Chamber, Verdict, Spent / Seat / Take / Judge / Press / View),
  AbilityDef.Reload, RequestReloadPress RPC, the owner's one-meaning-per-stroke rule, the host charge latch.
  Checks: pure Judge (claims clamped, NaN / 7.0, one press a reload) LaneA6cReloadJudgeChecks.
- **kits6c-J6 Sniper row + railgun on Space** (§2.1 rows: rail_damage 120, rail_charge 0.8, rail_tap 40,
  rail_reload 3.0, rail_spot_at 40, rail_spot 20, rail_perfect 1.5; rail_cooldown deleted, rlg_wide
  re-pointed; Dps.Railgun 47.4; Damage 6.0; Cycle rail_reload), hull 240, cannon / FireMode / F railgun
  deleted; Beam + Fx `rail_enhanced` appended; Sfx.Special -> Sfx.ByName; make_sounds.py rows rail_perfect /
  rail_miss (a python tool, not the engine) + _gap; the muzzle glint; Hint + Hints row `reload`.
  Checks S1-S9 (LaneA6cReloadChecks), rung-5 G1-G5 (LaneA6cReloadHost/Guest/Guest2Checks).
- **kits6c-J7 ReloadBar** (§4): HUD control following the ship, the lint overlap list, frame 73c_sniper_reload_spot.
- **kits6c-J8 Anchor** (D50). Checks LaneA6cAnchorChecks (0 u moved in 2 s with W and Shift+D; reload 1.2 s and
  spot 0.48-0.72 anchored; reach 3500 hits a dummy at 3400 and not unanchored; 12.0 s cooldown from a release
  at Vary 1-8 s; 8 s cap; V refused ANCHORED; a reload started before the Anchor keeps its length).
- **kits6c-J9 Zones foundation + Tether mine** (D44, D45): Zones.cs, the Spawns row + NetIds space appended, the
  draw on every peer; row `tether`. Checks LaneA6cZoneChecks (pure capsule / trigger), LaneA6cTetherChecks
  (a raider at 169 u held 3 s, 171 u not; boss ignored; 2 charges then refused; recharge), rung 5 guest sees it.
- **kits6c-J10 Flares ability** (D51). Checks LaneA6cFlaresChecks (E pops the salvo at the hull, 16.0 s cooldown,
  seeker lured / mark moved / webber dazzled -- kits_v2 Proves, through the key), frame LaneA6cFlaresFrames,
  rung 5 (a guest's E: flares at the host's spots).
- **kits6c-J11 Warden row + Proximity flak** (D40-D43): hull 270, Flak shot row, Hunters prey order (latched on
  a friendly hull first). Checks LaneA6cFlakChecks (fuse at 69 / 71 u, x0.75 on the boss, 45 DPS sheet,
  700 u), LaneA6cHunterPreyChecks.
- **kits6c-J12 Taunt** (D52; v2 Beacon Proves under the new name + v3 guard: 30 -> 20.1 at 0.5 / 5.9 s, 30 at
  6.1 s; beam tick 50 -> 33.5; 999 / 1001 u; hunting-within-reach switch; latched lets go in 0.25 s; edge
  heavy boosts in; back at 6.1 s; boss ignores; flak 33.75 vs 22.5). Checks LaneA6cTauntChecks, frame (the
  existing 82_taunt_shimmer re-posed on a real press) LaneA6cTauntFrames, rung 5 guest sees Hardened + swing.
- **kits6c-J13 Flak curtain** (D44 row `curtain`). Checks LaneA6cCurtainChecks (clamp 100 / 900 -> 150 / 700;
  perpendicular at VaryAngle; (40 + r − 1) u takes 20 then 10 / 0.5 s, (40 + r + 1) u takes 0; boss /
  structure / seeker 0; gone at 6.1 s; credit in NoteDealt; the Taunt x1.5 on a called craft), frame
  LaneA6cCurtainFrames, rung 5 guest draws it.
- **kits6c-J14 Record**: CHANGES.md Unreleased + Handoff, DESIGN.md slice-6c section, final POST (what the test
  phase owes: checks, rungs, frames).

## JOB 0 · POST
- Worktree created from version-l a8a5e81. Spec read; job list above. No code touched. Next: kits6c-J1.

## Handover 1: the first agent stops after JOB 0 (context spent reading the spec), at a job boundary
Pointers for J1 on (a8a5e81 tree): Ships.cs heavies rows 370-475 (Sniper 370, Warrior 401, Warden 434; the
HeavyCannon kit part 150); Abilities.cs Ab.Railgun 290, Ab.Rush 304, Ab.Hunters 315, Timed() 367, AbilityDef
fields Hold / RateStat / SpeedStat / While / OnDealt 51-101; PlayerShip.cs Lifts / Held 139-192, Cadence 204,
Outgoing 216 (the Taunt x1.5 row goes here), Slot 248, FitClass 352, DoAbility 589, ChargeRail / FireRail
739-754 (Charges.At + Lines.Strike), StartRush / RushEmp 756-775, SeekerPrey / LaunchHunters 782-811,
Disabled 887, Prismatic / GuardAngle 903-904, ApplyStatus 905, Incoming 1007, TickAbilities 1166, LocalFlight
1246, Steer 1322, SendHostState 1447, _Draw 1533. Melee.cs (Pick / Guard / Nose / Strike, no rows yet),
Prism.cs (GuardMax, Bands, Resolve, Catch, Walk), Decoys.cs + Hub.Flares, Squads.cs Call / CalledBy (146, 247),
Statuses.cs enum 16-30 (Hardened 8 takes the applier's share), Fx.cs Fields rows 492-503 (the taunt field on slot
"taunt", the boost plume) and Fx.TauntRing = 10, Drives.cs:154-159 (ANCHORED = Held <= 0). Old-kit callers to
delete in J1/J6/J11 (grep rush_|emp_damage|emp_range|emp_stun|Ab\.Rush|Ab\.Railgun|rail_cooldown|Dps\.Emp|Dps\.Railgun|heavy_main_gun|heavy_rush_drive|warrior_main_guns|heavy_railgun|HeavyCannon):
Abilities 3, Items 3 (~100-104), PlayerShip 7, Ships 20, Stats 4 (~364-371), Statuses 1 (:53 rush_guard
comment), SmokeTest.cs.txt 19 (~6459, 6899, 9483, 10768-10783, 11074, 11276 ...). Commit messages: write the file
with [IO.File]::WriteAllText (PowerShell's utf8 adds a BOM to the subject).
## kits6c-J1 · PRE (agent 2)
- tier opus. Intent: Warrior row (hull 300, no guns) + Blade (Space, Melee row blade 160 u ±55°, 26 / 0.40 s) + the trigger generalised to the class's Weapon Hold row + Stills; Rush / twin cannons / EMP deleted with every caller (D40-D42, D48). HEAD 669af1e86831ae88c769e176d0666a164de33579
- scripts/Ships.cs 5415309685219dbbec36724dfee2c526ed106413
- scripts/Abilities.cs fdd4a423b516cbf1e187e372ec963810e197aea6
- scripts/PlayerShip.cs d532201f0baf49377bba9c8b96018b6ddcf6eb88
- scripts/Stats.cs 106a2e3fc29510501789cfdac653fac0a667557a
- scripts/Statuses.cs e77870f6ce301940cf665789cca2ea2740c03586
- scripts/Items.cs c35ab118e76ad338218307e8c446a0b7204cf3ad
- scripts/Melee.cs 5f80c66ccb9e5369bbf5208d155288753a5da983
- tools/smoketest/SmokeTest.cs.txt 59e7c40100d0ffc791c20a1e5a65601e740bb610
## kits6c-J1 · POST
- Verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- Built: Warrior row (Fit.None, hull 300, rows blade_damage 26 / blade_interval 0.40 / blade_reach 160, Dps.Blade 65,
  kit warrior_blade + warrior_prism (utility; names blade_damage until J4 re-points it at prism_time)); Melee.Blade row
  (+ MeleeDef.Damage / Every), Melee.Draw (the wedge + sweep on every peer), Melee.Running; AbilityDef.Swing + Stills,
  PlayerShip.Swings (host) / Stilled; Abilities.TriggerOf (Trigger = the class's Weapon Hold row, not Fit.Guns + "guns");
  Items roles: blade ids in @primary / @primary_rate / @primary_range, "blade" in PrimaryShots; rush_* / emp_* out.
- Deleted: Ab.Rush, StartRush / RushEmp, Dps.Emp, twin cannons + FireMode + Guns on the Warrior, rush_/emp_ rows.
- Kept (merge risk): Fx.Emp and Dealt.Emp now have no game raiser; the rung-5 wire check raises Fx.Emp by hand. 6d's
  EMP (Echo) should use them; if it does not, delete both at the merge.
- Checks: LaneA6cBladeChecks (new: sheet, 3 varied swings 78 in 1.0 s / 0 behind / past / off-arc, released + DISABLED);
  rewritten: weapon-rows list (+blade), walls kit (Rush -> Hunters), Weapons-level rise (blade_damage), EveryDamageStat,
  Warrior SustainedDps 65, rush speed-lift push -> the V boost's x1.5 push on the Warrior, EMP on a cruise missile ->
  the blade on it, sweep witness ["blade"], reach case blade_reach (+pairs 31), rush_guard -> taunt_guard, arena host
  Warrior (hull 300, own rows + 7, host-applied HARDENED + Fx.Emp ring); frame 74b_warrior_blade_swing (LaneA6cBladeFrames).
- Checkpoint: the commit below. Next: kits6c-J2 Lunge.

## kits6c-J2 · PRE
- tier opus. Intent: Lunge (E, ability 1): DashSpec row on AbilityDef (owner-side carry along the nose, 420 u in 0.3 s, never priced from speed; host sweeps the start line and strikes each body once for 40, credit lunge; Hardened 0.5 for the 0.3 s; cooldown 7 s). HEAD a2fee51ae0d6194a40fc532b85a37f907038a8d1
- scripts/Abilities.cs 0e8055639c30e84e210e20a18dbeca64b1f01bfb
- scripts/PlayerShip.cs 0401394ec675414dc9ce76baef8e4e97a7deee82
- scripts/Ships.cs 417f2f1a452223cea8c17aa0b2623b4c3b4c5b1b
- scripts/Items.cs 7a4df5f01bbdc0680705d29937984a1ec73e8e39
- tools/smoketest/SmokeTest.cs.txt 96ad32b633a57609c7d648e06dd707d2761fdf47
## kits6c-J2 · POST
- Verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- Built: DashSpec (AbilityDef.Dash: Reach / Time / Damage / Guard / Cooldown stat ids); PlayerShip.StartDash (host press:
  Left, cooldown, At + Own = the start, Hardened at the Guard share), DashSweep (host: the start line, each body once,
  credit = the row id; every frame + at 1 by Expire), DashCarry (owner: the whole Time from the first frame it sees Left,
  a stale packet told from a fresh press by the cooldown jumping back up > 1 s; replaces Steer while it runs). Ab.Lunge
  (E), Warrior rows lunge_reach 420 / lunge_time 0.3 / lunge_damage 40 / lunge_guard 0.5 / lunge_cooldown 7; Damage
  lunge_damage 2; Items @output + lunge_damage. Warrior Abilities {Blade, Lunge}.
- Checks: LaneA6cLungeChecks (new: rows; 3 runs rest / top speed / boosted: 420 +- 5 u in 0.3 s, 2-3 bodies 40 once,
  off-line 0, DealtBy, 50 in / 100 after, cooldown 7.0, second press COOLING); rung 5 LaneA6cLungeHostWatch /
  LaneA6cLungeHostChecks / LaneA6cLungeGuestChecks (new); rewritten: EveryDamageStat (+lunge_damage, nine), sweep witness
  ["lunge"].
- Next: kits6c-J3 Whirlwind.

## kits6c-J3 · PRE
- tier opus. Intent: Whirlwind (Q, ability 2): Melee.Whirl row (210 u, all round, 10 every 0.25 s = 40 DPS, 2 s), Stills the blade, the press drops the web and Status.Unwebbed (512, host-only, appended) refuses a new pin for the 2 s; cooldown 14 s (D47). HEAD b4aeea25233ee4e5545c489aa4b05410a053c323
- scripts/Abilities.cs a385a4d979d99295db8ccdb1f86f0f93c875c0b0
- scripts/PlayerShip.cs 00f6a41114b1f12dac792b1eb3ca6ead40366b61
- scripts/Ships.cs 0a43f48530a2d1bc71aa4cacce71f97cc926aa2e
- scripts/Items.cs 607be96fb1f8c636aaea157834c32a0bfadbe265
- scripts/Melee.cs bb15cebc11cd4e8f9810b6882f0816768836f2d6
- scripts/Statuses.cs dd5ed0fbe603d3b0f0baedd7841c804419a35dc8
- tools/smoketest/SmokeTest.cs.txt 531bc4e18ff6d66f6f0e353726ef3c873e69198f
- tools/screens/Shots.cs.txt 8e3eeecbdd9de6ace7a1d3558359212547185923
## kits6c-J3 · POST
- Verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- Built: Melee.Whirl (whirl_reach 210, 180 deg, whirl_damage 10 / whirl_interval 0.25), Ab.Whirlwind (Q, Swing + Stills),
  PlayerShip.Whirl (host press: Left whirl_time 2, cooldown whirl_cooldown 14, the web's pin and ask dropped,
  Status.Unwebbed 512 appended (host-only) for the spin; ApplyStatus refuses a pin while it runs). Melee.Running: a
  Press row runs while Left > 1e-6 (no ninth blow off a rounding hair). Items: whirl_damage @output, whirl_reach @area,
  whirl_time @duration; Damage whirl_damage 0.5. Warrior Abilities {Blade, Lunge, Whirlwind}.
- Checks: LaneA6cWhirlChecks (new: rows; 3 runs: 4 bodies all round lose 80, 280-340 u 0, cooldown 14, SPINNING;
  a latched webifier freed in 1-2 frames, free for the spin, re-takes after 2 s; the blade held through the spin
  swings 0, then swings); frame 74c_warrior_whirlwind (LaneA6cBladeFrames); rewritten: EveryDamageStat (+whirl, ten),
  sweep witness ["whirlwind"].
- Next: kits6c-J4 Prism stance.

## Handover 2: agent 2 stops after J3 (context), at a job boundary. J4 is next; no PRE written for it.
Conventions this agent used (keep them): checks are written in the scratchpad and spliced into SmokeTest.cs.txt with a
python exact-match edit (assert count == 1), each LaneA6c method inserted after the previous LaneA6c method; solo calls
go after `await LaneA6cWhirlChecks(yonder);` (the WARRIOR block, ~11200); the sweep's witness rows sit after
["whirlwind"]; frames go in LaneA6cBladeFrames (Shots.cs.txt ~1019). All harness roles run at Peak = Unlocks.Top.
Generic machinery built so far, for J4 on: AbilityDef.Swing (MeleeDef) + Stills (PlayerShip.Stilled holds the trigger's
weapon while such a row's Left runs), AbilityDef.Dash (DashSpec; PlayerShip.StartDash / DashSweep / DashCarry),
Abilities.TriggerOf, Melee.Draw / Running, Status.Unwebbed 512 (host-only).
J4 pointers (Prism stance, D49): nothing draws the prism wedge yet (grep Prismatic: only PlayerShip:882 and Prism.cs).
Prism.Catch (Prism.cs:116) splits every caught ray / beam with no cadence; D49's split tick (0.75 s, at most 3 a stance)
belongs to the stance row: suggested shape -- IPrism gains `bool Split()` (PlayerShip: the running row with the stance's
split stats counts N / the last split's clock in its slot; no running stance row = no cadence, so the slice-5
LaneAPrism*Checks that ApplyStatus(Parrying) directly keep passing), and Catch strikes the children only when Split()
says so (the catcher still takes 0). The stance row: Id "prism", Key.F, Hold = 0.5 (Held: share after the sum, boosted
0.75), Stills = true (no blade), Press toggles (F again drops: Left = 0 and the cooldown 12 s from the END -- set Cool
in the drop and in Elapsed/Expire, not at the press), Status.Parrying applied for prism_time 2.0 and cleared on the drop;
StartDash("lunge") and Whirl() must end a running stance (read the stance by a row flag, e.g. AbilityDef.EndsOnPress /
"Stance", never by id). Rows: prism_time 2, prism_cooldown 12, prism_hold 0.5 (or Hold literal), prism_split 0.75,
prism_splits 3. Re-point the kit part warrior_prism (Ships.cs Warrior Kit) from blade_damage to prism_time. Warrior
Abilities in learn order {Blade, Lunge, Whirlwind, Prism}; then the walls check at SmokeTest ~6922 (Learns lunge,
whirlwind, prism) goes live once every class has 3. Fx `warn_beam` (appended at the END of Fx's ids) for the clip.
Checks owed (ledger job list J4): LaneA6cPrismStanceChecks, LaneA6cPrismFrames (wedge, SQUARE clip, SLANT fan),
rung 5 LaneA6cPrismHostChecks / LaneA6cPrismGuestChecks; sweep witness ["prism"]; the Stills witness.

## kits6c-J4 · PRE (agent 3)
- tier opus. Intent: Prism stance (F, ability 3, D49): AbilityDef.Stance (StanceSpec: Time / Cooldown / Every / Splits stat ids + the Status it holds), PlayerShip.Stance / EndStance, any other ability 1-3 press ends a running stance, IPrism.Split (the stance's split tick 0.75 s, at most 3 a stance; no running stance row = every catch splits), Hold 0.5, Stills; Field row prism (FieldLook.Wedge) drawn on every peer; kit part warrior_prism -> prism_time. HEAD 0f19074d1188a66af716bac8ac1690e297a38bd8
- scripts/Abilities.cs 26a59cdcf242bd39a1d91a6b94fc00c6f3b0e648
- scripts/PlayerShip.cs a640e05f711cdadbc0213ce839db6540ceb53bb2
- scripts/Ships.cs 29ab7bdbd3aa4f6620029fb4f7f009c2053f41c1
- scripts/Prism.cs 858fa2c677b534b11e58f6a21f5f97eff52d0bf5
- scripts/Fx.cs a44e9429b3c6e0609b246a43fa48afc061f27f27
- scripts/Items.cs 9401325a16e5aebe715f4cf89daee1ccb10e94e6
- tools/smoketest/SmokeTest.cs.txt 77513606092ca43d127322e5a1d52e925397ff63
- tools/screens/Shots.cs.txt 705e8d6693817b55be7f325829a4955bc1391e4c
## kits6c-J4 · POST
- Verdict: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final test phase.
- Built: StanceSpec (AbilityDef.Stance: Time / Cooldown / Every / Splits stat ids, Holds a Status); Ab.PrismStance (F,
  Hold 0.5, Stills, Press toggles, Elapsed = EndStance on every peer); PlayerShip.Stance / EndStance (cooldown from the
  end) / Split (IPrism.Split: at most prism_splits a stance, prism_split apart, 0.05 s early allowed; no stance running =
  every catch splits); DoAbility: any other of the class's abilities (not Weapon, not the drive) ends a running stance;
  Prism.Catch strikes children only when Split says so (the catcher still takes 0). FieldLook.Wedge + Fields row
  "prism" (the wedge on every peer from the slot). Rows prism_time 2 / prism_split 0.75 / prism_splits 3 /
  prism_cooldown 12; kit part warrior_prism -> prism_time; Items @duration + prism_time. Warrior Abilities in learn order
  {Blade, Lunge, Whirlwind, PrismStance}.
- Defaults (D53): no warn_beam Fx row -- the clip and the fan are the children's own Lines lines (slice 5) and the
  wedge; nothing would read the row. The slot's N counts splits (Own = when the last fell), not the band: every peer
  draws the band from the children's lines.
- Checks: LaneA6cPrismStanceChecks (new: rows; 3 runs: every-frame SQUARE ticks -> 0 taken, 112.5 on the guard;
  1.85-1.9 s caught, 2.1-2.2 s lands 50; cooldown 12.0 at the end; held 0.5 / 0.75 boosted; blade 0; F / E / Q at
  0.3-1.7 s ends it, cooldown 12, COOLING); rung 5 LaneA6cPrismHostWatch / HostChecks / GuestChecks (new); frames
  74d_warrior_prism_wedge, 74e_warrior_prism_square, 74f_warrior_prism_slant (LaneA6cPrismFrames); rewritten: sweep
  witness ["prism"] + a stance's second press drops it (cooldown = its row), Parrying cleared after the sweeps.
- Next: kits6c-J5 ActiveReload foundation.

## kits6c-J5 · PRE
- tier opus. Intent: the ActiveReload foundation (sniper_active_reload.md §2.2, §3): ActiveReload.cs (new: ReloadSpec incl. Charge, Chamber, Verdict, Spent / Seat / Take / Judged / Give / Judge / Press / Shot / Step / View), Net.Leeway, AbilityDef.Reload + Loose, PlayerShip.RequestReloadPress + AskReloadPress, the owner's stroke rule, the host's charge latch, a fitted chamber seated; Sfx.Special renamed Sfx.ByName (Press plays through it). No row uses it yet (J6). Checks: LaneA6cReloadJudgeChecks (pure). HEAD c6c67808764fa0329d148c1f19321632b3595712
- scripts/ActiveReload.cs new
- scripts/Abilities.cs 0ed703fad647696f5b8fc44f80b2d16a3d373644
- scripts/PlayerShip.cs f9da5898c6fd5d163cd8368dfea98de71c02b7b7
- scripts/Net.cs f424f5a20037bb4fe6aeb298886ee58de1d1b04d
- scripts/Sfx.cs 01157ae1939d553ea78014a3926c7ad6d44664b6
- scripts/Boss.cs 873f332ec2392263d6d977c5f9dabb46a43365c9
- scripts/Fx.cs 54e6c935b131221c1ee07d0a15226d26de6c8475
- scripts/Hub.cs ab952edc87ccfb72862347360be24402bedeb011
- scripts/Shots.cs 05c56a1f0517457bda6938d963d7330c049989ef
- scripts/ThrownRock.cs c398efc801266348f7db2d56049911dd7fa64f67
- tools/smoketest/SmokeTest.cs.txt a1b003390d24ecd2588fb34a5eb2f962b7113f78
