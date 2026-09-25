# WARRIOR — spec card

## Identity
- Heavy, hull **300**, top speed **190 u/s** (`thrust` 130, `reverse_thrust` 60, `reverse_speed` 70, `turn_radius` 55, `turn_rate` 2.2).
- Drive: **boost** (the nine's Surge, not warp). `strafe_speed` 95, `strafe_thrust` 380. Shift+A/D strafes; V boosts (+50% top/strafe, 3 s, 15 s cooldown; never breaks a web). *kits_v31.md §2, §3.4; `Drives.cs` row `Boost`.*
- No passive (kits_v31 §2 table: passive column empty).
- Role: fast and close; blade cuts everything in its arc, prism splits light. *kits_v31 §2.*

## Primary — Blade (Space, hold)
26 dmg to every body in the arc, every **0.40 s** = **65.0 DPS**; reach **160 u**, arc **±55°**. *kits_v2.md §3 WARRIOR card.* Counterplay: kiting; every boss ring reaches it.

## Abilities, in LEARN order (walls: kits_v31.md §3.6 table)

### 1. Lunge — key **E**, learned L1, cooldown **7 s**
420 u dash along the nose in **0.3 s**; 40 dmg to each body on the way; **x0.5** damage taken while it runs. *kits_v2.md §3 WARRIOR card; kits_v31.md §3.4 unchanged.* Closes the melee gap and cuts incoming damage in half for the dash's duration; ends a prism stance.

### 2. Whirlwind — key **Q**, learned L3, cooldown **14 s**
2 s spin, 10 dmg every **0.25 s** to everything within **210 u** = 40 DPS; throws off every web on the hull and the Warrior cannot be latched while it spins. *kits_v2.md §3 WARRIOR card.*

### 3. Prism stance — key **F**, learned L6, cooldown **12 s from the stance's end**
2.0 s; the guard **g** faces the cursor, clamped ±90° off the nose. Light within **±60°** of g is resolved by θ (angle between guard and the line back to the source): **SQUARE** (θ≤15°) sends 75% back along g as a friendly beam, 25% continues as the enemy's; **SLANT** (15–60°) mirrors 50% off the blade (`r = u − 2(u·g)g`) as friendly, bends the other 50% by θ/2 to the other side, still the enemy's; **OPEN** (>60°) is not caught. The Warrior takes **0** of a caught blow. Children: 36 u wide, a friendly child reaches 1500 u (900 u for rays) to the first hostile body, a through child reaches the parent's remaining length (400 u for rays) and hits every pilot on it; children never split again; at most **3 splits a stance**, every **0.75 s**. Projectiles are reflected (not split): SQUARE sends them back along g at x1.5 speed, SLANT along r at x1.0, both friendly and unguided; the siege missile is never caught. In stance: speed **x0.5**, no blade swings; Q or E ends it. *kits_v2.md §3 WARRIOR card ("Prism stance"); kits_v31.md §6 F11 (children are `Lines` rows, `FirstBody`, 36 u).*

## Interactions (kits_v31.md §7)
- **Strafe vs Dart Slingshot vs Warrior Lunge vs Wraith Shadow step** — accepted: strafe is a slide at half top, the others are a snap/dash/blink.
- **The capitals' warp vs the web-breakers** — the Warrior has no warp; its own web answers are Whirlwind and Lunge.
- Prism vs BB CIWS/FR sentries/CV patrol/PD: not listed as an overlap (Prism only catches beams/rays/projectiles it faces, not turret fire in general).
- Boost in Prism stance: the stance's x0.5 is a share after the sum (F1), so boosted in stance is 1.5 x 0.5 = x0.75, never x1.0. Lunge is a fixed 420 u in 0.3 s, never priced from speed (kits_v31 §3.4 drive table).

## Rulings touching this class
- README.md: "Approved as written: … Warrior … with the changes below" — no owner change fell on the Warrior; its v2 card stands unedited through v3 and v3.1.
- kits_v31.md §3.6: learn order is **E, Q, F** (Lunge, Whirlwind, Prism), not the F/Q/E every other class uses — Prism is last because it is "the hardest mechanic in the game." `level_walls.md` line 13, 57.
- kits_v31.md §9 risk 1: strafe raises land rates for short-range classes (Warrior named) — unmeasured until flown.

## BUILT (scripts/Ships.cs, scripts/Abilities.cs)
- `ClassDef` row: `ShipClass.HeavyWarrior`, `Ready = true`, `Fit = Fit.None`, `Drive = Drives.Boost`.
- `ClassDef.Abilities = { Ab.Blade, Ab.Lunge, Ab.Whirlwind, Ab.PrismStance }` (learn order after the primary).
- Ability ids: `blade` (Space, Hold, Weapon), `lunge` (E), `whirlwind` (Q), `prism` (F).
- Rows (`ClassDef.Rows`): `blade_damage` 26, `blade_interval` 0.40, `blade_reach` 160; `lunge_reach` 420, `lunge_time` 0.3, `lunge_damage` 40, `lunge_guard` 0.5, `lunge_cooldown` 7; `whirl_damage` 10, `whirl_interval` 0.25, `whirl_reach` 210, `whirl_time` 2, `whirl_cooldown` 14; `prism_time` 2, `prism_split` 0.75, `prism_splits` 3, `prism_cooldown` 12. All values match sourced specs exactly.
- `Prism.cs` (new table): `Prism.Bands`, contract `IPrism` (`Prismatic`, `GuardAngle`, `Split()`), `Prism.Resolve`.
- **Check methods that exist** (`tools/smoketest/SmokeTest.cs.txt`, grep exact names):
  `LaneAMeleeArcChecks` (generic arc-pick geometry the blade/whirl/lunge/prism all use), `LaneAPrismBandChecks`, `LaneAPrismResolveChecks` (band math, foundation-level), `LaneA6cBladeChecks`, `LaneA6cLungeChecks`, `LaneA6cWhirlChecks`, `LaneA6cLungeHostChecks`, `LaneA6cLungeGuestChecks`, `LaneA6cPrismStanceChecks`, `LaneA6cPrismHostChecks`, `LaneA6cPrismGuestChecks`.
- None of these have run in the engine yet (`ledger_main.md`: "BUILD PHASE COMPLETE … nothing has run in the engine"); they exist as source only.

## OPEN
- None found. Every row, key and cooldown in `Ships.cs`/`Abilities.cs` matches the newest applicable source (kits_v2.md for the untouched v2 card, kits_v31.md §3.6 for the learn order) with no numeric or behavioral gap.
