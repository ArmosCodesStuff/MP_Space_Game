# WARDEN — spec card

## Identity
- Heavy, hull **270**, top speed **190 u/s** (`thrust` 130, `reverse_thrust` 60, `reverse_speed` 70, `turn_radius` 55, `turn_rate` 2.2).
- Drive: **boost** (the nine's Surge, not warp). `strafe_speed` 95, `strafe_thrust` 380. *kits_v31.md §2, §3.4; `Drives.cs` row `Boost`.*
- Passive: **PD x1** — one point-defence mount (the capitals and freighters carry two; no other heavy or light has PD): `pd_count` 1, the sheet's `pd_damage` 0.5 every 0.5 s = 1 DPS (README ruling "Point defence passive, 1 DPS per mount"), `pd_range` 420. *kits_v31.md §2 ("PD x1"); docs/plans/README.md.*
- `Fit = Fit.Guns | Fit.Pd`. Role: draws raiders in, shreds them. *kits_v31.md §2.*

## Primary — Proximity flak (Space, mouse-aimed)
22.5 dmg every **0.5 s** = **45 DPS** (`main_damage`/`main_interval`); bursts **70 u** off a hostile hull or at max range and hits everything within 70 u of the burst; range **700 u** (`main_range`); **x0.75** on a boss (`Resists = Tag.Boss, ResistShare = 0.75`, `scripts/Shots.cs`). Source: kits_v2.md §3 WARDEN card.

## Abilities, in LEARN order (walls: kits_v31.md §3.6 table)

### 1. Hunters — key **F**, learned L1, cooldown **14 s**
6 missiles (`hunter_count`), 45 dmg each (`hunter_damage`), 260 u/s, 2.5 rad/s guidance, reach **1200 u** (`hunter_range`). Prey order: a raider latched on a friendly hull (the Warden included) first, then nearest; a dummy only as fallback. Source: kits_v2.md §3 WARDEN card ("v1 Hunters"); prey order confirmed unchanged in kits_v31.md §3.8.

### 2. Taunt — key **Q**, learned L3, cooldown **20 s**
For **6 s** (`taunt_time`), every raider within **1000 u** (`taunt_reach`), or hunting something within 1000 u, takes the Warden as its target: latched webbers let go and post on the Warden, standoff heavies boost in at once instead of waiting at range, hostile emplacement guns in reach prefer the Warden. Called craft take **x1.5** (`taunt_mult`) from everything the Warden deals. **The Warden itself takes 33% less from everything for the same 6 s** (`taunt_guard` 0.67 — every blow x0.67: bosses, raiders, missiles, rings). Never pulls a boss. Source: `kits_v3.md` §3.5 (renamed from v2's "Beacon" and given the 33% guard); the guard itself is the owner's ruling (README.md).

### 3. Flak curtain — key **E**, learned L6, cooldown **18 s**
5 flak shells burst in a **500×80 u** line at the cursor (clamped 150–700 u), perpendicular to the aim, live **0.5 s** after the press, lasting **6 s**. A hostile craft (light/heavy, not boss/structure) touching it takes **20** on contact (`curtain_first`) then **10 every 0.5 s** while inside (`curtain_tick`/`curtain_every`). No slow. Source: kits_v2.md §3 WARDEN card ("Flak curtain").

## Interactions (kits_v31.md §7)
- **CV patrol vs BB CIWS vs FR sentries vs PD** — the Warden's passive PD is one of the four turrets split by carrier/reach/time, ranked by one `Turret.Rank`.
- **The capitals' warp vs the web-breakers** — the Warden has no warp; its own web answer is **Taunt** (pulls webbers onto itself).
- **WD Taunt's guard vs BB Brace** — "as v3": both are damage-taken multipliers on different classes, split by depth (Brace x0.35/3 s/half-speed, deep and short; Taunt x0.67/6 s/no hold, shallow and long); neither class carries the other's row.

## Rulings touching this class
- README.md: "Warden Beacon → TAUNT: the pull plus 33% damage reduction for 6 s, shown on screen." This is the defining owner change from v2's plain pull-only Beacon.
- README.md: "Approved as written: … Warden … with the changes below" (the Taunt rename/guard is that change; Hunters, Flak curtain and PD x1 are untouched from v2).

## BUILT (scripts/Ships.cs, scripts/Abilities.cs)
- `ClassDef` row: `ShipClass.HeavyWarden`, `Ready = true`, `Fit = Fit.Guns | Fit.Pd`, `Drive = Drives.Boost`, `Shot = Shots.Flak`.
- `ClassDef.Abilities = { Ab.Guns, Ab.FireMode, Ab.Hunters, Ab.Taunt, Ab.Curtain }` (primary + fire-mode toggle, then the 3 learned abilities).
- Ability ids: `hunters` (F), `taunt` (Q, `Draws = true`), `curtain` (E).
- Rows (`ClassDef.Rows`): `hunter_count` 6, `hunter_damage` 45, `hunter_speed` 260, `hunter_turn` 2.5, `hunter_range` 1200, `hunter_cooldown` 14; `taunt_time` 6, `taunt_reach` 1000, `taunt_mult` 1.5, `taunt_guard` 0.67, `taunt_cooldown` 20; `curtain_first` 20, `curtain_tick` 10, `curtain_every` 0.5, `curtain_cooldown` 18. Primary in `Nums`: `main_count` 1, `main_damage` 22.5, `main_interval` 0.5, `main_range` 700, `shell_speed` 600; `pd_count` 1, `pd_range` 420 (`pd_damage` 0.5 from the sheet). All values match sourced specs exactly, including the flak's 70 u fuse and x0.75 boss resist share (`scripts/Shots.cs` id `"flak"`).
- **Check methods that exist** (`tools/smoketest/SmokeTest.cs.txt`, grep exact names):
  `LaneA6cFlakChecks`, `LaneA6cHunterPreyChecks`, `LaneA6cTauntChecks`, `LaneA6cTauntLatchChecks`, `LaneA6cTauntEmplacementChecks`, `LaneA6cTauntGuestChecks`, `LaneA6cCurtainChecks`, `LaneA6cCurtainGuestChecks`, `LaneA6cZoneChecks` (curtain zone geometry, shared with Sniper's tether). Also `LaneA6aBraceSplitChecks` (the BB slice's check), which proves Taunt's guard and Brace's guard split by depth and never leak onto the other's class.
- None of these have run in the engine yet (`ledger_main.md`: build phase complete, nothing run).

## OPEN
- PD x1: built at 1 DPS (the sheet's pd_damage 0.5 every 0.5 s), per the README ruling (review hv-R6c-1).
- `Ab.FireMode` is built on G for this class; kits_v31 §2 lists no fire-mode action for it and names G unused (the same R/G gap as the Battleship card's OPEN). Not resolved here.
