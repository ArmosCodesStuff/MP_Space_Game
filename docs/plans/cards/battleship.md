# BATTLESHIP

## Identity
Capital. Hull 500. Top 88 u/s (thrust 47, reverse 20/30, turn radius 107, turn rate 1.08). Never strafes
(strafe_speed 0). Role: "turns its side, outguns everything" (kits_v31 §2).
Drive: **WARP** (capital-only, kits_v31 §3.4/README): 1.0 s spool, then range grows 800 u/s (warp_rate),
safe to 2400 u (warp_safe, a 4.0 s hold), overshoot to +900 u, disabled 2 s per 300 u over, cooldown 20 s
(warp_cooldown) from the jump. A running CIWS stops under an overshoot's Disabled (it's a gun); PD and the
guard through Brace keep running (decision 13, kits_v31 §3.4 table).

## Primary — Main battery (Space, Hold)
4 turrets, cursor-aimed, 12.5 dmg every 2.0 s each = **25.0 DPS** sheet, 1000 u range, shells 650 u/s.
**Arcs** (kits_v31 §0/kits6a A6a-1): the fore pair (mounts at y −74.28) never fires within 30° of the
stern (bears 0–150° off the bow); the aft pair (y −36.27) never within 30° of the bow (bears 30–180°),
so all 4 bear abeam, 2 fire bow/stern-on. R toggles fire mode SALVO/STAGGERED (same rate; source:
kits_v31 §2 table, R column "salvo / stagger" — see OPEN, the code binds this to G, not R).

## Abilities (learn order F L1 → Q L3 → E L6)

**F — Broadside.** Key F, cooldown 12.0 s (`broadside_cooldown`). 0.5 s wind-up (turrets swing onto the
cursor) then 6 volleys (`broadside_volleys`) of all 4 guns, 0.25 s apart (`broadside_gap`), each shell
×1.25 (`broadside_mult`) = 375 dmg side-on / 187.5 bow-on (arcs apply). 27.3 DPS sheet, 13.75 s cycle.
Source: kits_v31 §3 table + README ("hulls: BB 500... Rate buffs ADD"); numbers from kits6a A6a-1. Fires
mid-warp-charge (the charge and jump are unaffected by the drive, kits_v31 §3.4 table). What it does: the
BB turns broadside-on and unloads every gun at once for a burst well above its sustained rate.

**Q — Brace.** Key Q, cooldown 25.0 s (`brace_cooldown`) from the press. For 3 s (`brace_time`) the hull
takes ×0.35 of every blow (`brace_share`, a Hardened share, F1's share rule), at half top speed (Hold 0.5,
a share on `max_speed`/`strafe_speed`). Never refused but COOLING; the guns and Broadside keep firing
under it; the guard survives a warp jump. Source: kits_v31 §3.8 ("unchanged from v3"), README, numbers
kits6a A6a-2. What it does: cuts incoming damage sharply while slowing, to soak a beam or a boss's slug.

**E — CIWS.** Key E, cooldown 20.0 s (`ciws_cooldown`). For 6 s (`ciws_time`) both PD mounts fire ×8 rate
(`ciws_rate`, scoped to `pd_interval`) and ×3 damage (`ciws_damage`, scoped to `pd_damage`) at PD's own
460 u range (`pd_range`) and prey (missiles, fighters, light craft; never a heavy, boss or the siege cruise
missile) = 24 DPS a mount, 48 total. Stops under an overshoot's Disabled (`While = !s.Disabled`; a gun)
while the plain 1 DPS/mount PD keeps firing. Source: kits_v31 §3.8, README, numbers kits6a A6a-3. What it
does: a short, heavy point-defence burst against missile and fighter swarms.

## Interactions (kits_v31 §7)
- **CV patrol vs BB CIWS vs FR sentries vs PD**: accepted, split by carrier/reach/time; CIWS is the BB's
  own 460 u ring, 6 s of every 20 s (the cooldown runs from the press); all four rank by one `Turret.Rank`.
- **FR Time on target vs BB Broadside**: accepted — converging scattered guns (TOT) against a turned side.
- **Capitals' warp vs web-breakers**: a jump leaves the webber's latch (within 12 u), so the web drops —
  the BB's own way of clearing a web (the nine only boost, and never break one).
- **WD Taunt's guard vs BB Brace**: a design split, not a stack on one hull; x0.35 (Brace) wins over x0.67
  (Taunt) while both run, x0.67 after Brace's 3 s (kits6a J2).

## Rulings touching this class (docs/plans/README.md)
- "1 primary weapon + 3 abilities per class, each useful to a pilot flying alone."
- "hulls: BB 500 / CV 425 / DD 395... Approved as written: Battleship... with the changes below" (only
  drive/helm/walls sections of kits_v31 touch it; weapon and all 3 abilities unchanged from the original).
- "WARP... is CAPITAL-ONLY (BB, CV, DD). Capitals are slower overall and rely on it."
- "Rate buffs ADD (two x2 = x3). Point defence passive, 1 DPS per mount, 2 on every capital."
- Level walls: F Broadside L1, Q Brace L3, E CIWS L6; chip slots 1-6 at L2/4/8/10/12/14 (every hull).

## BUILT
`scripts/Ships.cs` `ShipClass.Battleship`: `Drive = Drives.Warp`; `Nums` hull 500, thrust 47, max_speed 88,
turn_radius 107, turn_rate 1.08, main_count 4, main_damage 12.5, main_interval 2.0, main_range 1000,
shell_speed 650, pd_count 2; `Rows` brace_time/share/cooldown (3/0.35/25), ciws_time/rate/damage/cooldown
(6/8/3/20); `Abilities = { Ab.Guns, Ab.FireMode, Ab.Broadside, Ab.Brace, Ab.Ciws }` (the learn order the
walls read). Broadside's own rows (volleys 6, mult 1.25, windup 0.5, gap 0.25, cooldown 12) are
class-shared defaults in `scripts/Stats.cs` under `Fit.Broadside`. `Drives.cs` `Warp`: warp_safe 2400,
warp_rate 800, warp_cooldown 20. Ability ids: `broadside`, `brace`, `ciws` (`scripts/Abilities.cs`).

Check methods (all present in `tools/smoketest/SmokeTest.cs.txt`, wired into the solo list ~18614):
`LaneA6aArcChecks`, `LaneA6aBroadsideChecks`, `LaneA6aBraceChecks`, `LaneA6aBraceWarpChecks`,
`LaneA6aBraceSplitChecks`, `LaneA6aCiwsChecks`, `LaneA6aCiwsDisableChecks`, `LaneA6aCiwsPreyChecks`.
Frames (`tools/screens/Shots.cs.txt`): `85a_bb_brace`, `85b_bb_ciws`. Warp/strafe, shared with CV/DD:
`LaneBWarpChecks`, `LaneBStrafeChecks` (capitals never strafe).

## OPEN
- **R key for fire mode.** kits_v31 §2's table gives the Battleship's R column as "salvo / stagger" (the
  fire-mode toggle), and §2's Keys note says "X, G, T and C stay unused." The built `Ab.FireMode`
  (`scripts/Abilities.cs`) has `Default = Key.G`, and the harness itself asserts this on purpose
  (SmokeTest.cs.txt ~16056: "reset restores defaults (fire mode on G)"). Spec says R; code and its own
  checks say G, and G is a key the spec calls unused. Not resolved here.
