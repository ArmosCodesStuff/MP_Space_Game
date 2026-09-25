# DART — spec card

## Identity
- Light, hull **200**, top speed **260 u/s** (`thrust` 190, `reverse_thrust` 90, `reverse_speed` 95, `turn_radius` 35, `turn_rate` 3.0). *Ships.cs:586-588.*
- Drive: **boost** (the nine's Surge, not warp). `strafe_speed` 130, `strafe_thrust` 520. Shift+A/D strafes; V boosts (+50% top/thrust/strafe, 3 s, 15 s cooldown; never breaks a web). *kits_v31.md §2, §3.4; `Drives.cs` row `Boost`.*
- Passive: **Slipstream** — `slip_guard` **x0.7** damage taken at `slip_speed` **325 u/s** or more (actual speed). Host clamps reported speed to `hypot(top, strafe) x 1.1`. *Ships.cs Slipstream rows; kits_v2.md DART card; kits_v31.md §3.4 F24 note.*
- `Fit = Fit.None` — no turret mount at all; the primary fires through the Bore path, not a gun. *Ships.cs:581.*
- Role: "its top speed is damage" — every ability prices off the sheet's current top. *kits_v31.md §2.*

## Primary — Pepperbox (Space, hold)
Two nose rails alternate, **6 darts/s** (`pepper_interval` 1/6 s). Each dart leaves at `pepper_speed` **520 u/s + ship velocity**, steers up to `pepper_turn` **6 rad/s** onto the owner's live cursor, reaches `pepper_range` **750 u**, hits the first hostile body (never a missile). Damage `pepper_damage` **7.5**, speed-priced: `7.5 x clamp(top / price_top(260), 1.0, pepper_cap(1.5))`, capped at **11.25/dart** from 390 u/s. Stock DPS 45.0. *Ships.cs Pepperbox rows; `Ab.Pepperbox` (Abilities.cs:753); kits_v2.md DART card; kits_v31.md §3.4 pricing table.*

## Abilities, in LEARN order (`ClassDef.Abilities[1..3]`; Pepperbox is `[0]`, the primary)

### 1. Rod from God — key **F**, cooldown **12 s** (`rod_cooldown`)
Press: **3.0 s** forced sprint — `sprint_thrust` **x3**, `sprint_add` **+100 u/s** flat, throttle forced open, rudder free. At 3.0 s a rod leaves the nose at `rod_speed` **300 u/s + ship velocity**, pierces every hostile body once over `rod_range` **1400 u** (never a missile). Damage `rod_damage` **180**, priced `180 x clamp(top/260, 1.0, rod_cap(2.0))`; recoil leaves `rod_recoil` **30%** of speed. *Ships.cs "Rod from God" rows; `Ab.Rod` (Abilities.cs:767); kits_v2.md DART card.*

### 2. Ramjet — key **Q**, cooldown **20 s** (`ramjet_cooldown`)
Lit **8 s** (`ramjet_time`). Flying flat-out within 5% of current top, top speed builds `ramjet_build` **+10%/s** to `ramjet_cap` **+50%**; a full turn at full yaw bleeds `ramjet_bleed` **-20%/s** (net -10%/s at full throttle+full rudder); drains out over 1.0 s after it goes out. **Replaces Jetwash** (owner ruling). *Ships.cs Ramjet rows; `Ab.Ramjet` (Abilities.cs:784); kits_v3.md §3.6; README.md ruling "Dart keeps a speed button, RAMJET … instead of Jetwash."*

### 3. Slingshot — key **E**, cooldown **6 s** (`sling_cooldown`)
Snaps heading AND the whole velocity (strafe included) onto the cursor bearing, up to 180°, keeping its size — a snap, not a turn, so it never bleeds a lit Ramjet. *Ships.cs Slingshot row; `Ab.Slingshot` (Abilities.cs:799); kits_v2.md DART card; kits_v31.md §3.4 row.*

## Interactions (kits_v31.md §3.4, §7)
- **V boost / Ramjet / sprint / Wraith Veil are the four speed lifts and they add.** Only the Dart turns speed into damage (decision 14). *§7:465.*
- Boost+Ramjet: top = `260 x (1 + ramjet + 0.5)`, up to **x2.0 = 520 u/s**; the build pauses while climbing to the new (boosted) top. *§3.4:277.*
- Boost+sprint: thrust 1 + 2.0 (sprint) + 0.5 (boost) = x3.5 (lifts add); top = `(260 x 1.5) + 100 = 490`, so the rod prices at **339.2**, capped **360** with the Ramjet also built. *§3.4:278.*
- Boost+Pepperbox: any boost puts pricing at its **x1.5 cap** (11.25/dart) for the 3 s it runs. *§3.4:279.*
- Slingshot snaps the whole velocity (strafe included) onto the cursor; unaffected by the boost or a lit Ramjet. *§3.4:280.*
- Boost pushes a Dart above 325 u/s (Slipstream) within 0.4 s; the host's clamp uses `hypot(top, strafe) x 1.1`. *§3.4:281.*
- **The Dart has no web-breaker at all — only hull.** The nine's web answers (Rewind/EMP, Whirlwind/Lunge, Shadow step, …) skip the Dart and the Tender; boost never breaks a web (decision 3). *§7:467.*
- Strafe vs Dart Slingshot vs Warrior Lunge vs Wraith Shadow step: accepted as four distinct mechanisms (slide/snap/dash/blink). *§7:466.*

## Rulings touching this class
- README.md: "Dart keeps a speed button, RAMJET (+10% top a second at full throttle, up to +50%; turning bleeds it), instead of Jetwash."
- README.md (reconciled 2026-09-25) and numbers §8 R6: realistic DPS 72.0; every top-speed lift prices the Pepperbox and the rod, gear included (kits_v31 decision 14's default kept; the numbers file's D6 reversed); kits_v31.md's own table (line 387) still prints 71.9.
- kits_v31.md §2: the nine (Dart included) lose warp; V is boost, +50% top/strafe 3 s, 15 s cooldown.

## BUILT (scripts/Ships.cs, scripts/Abilities.cs)
- `ClassDef` row: `ShipClass.LightDart`, `Ready = true`, `Fit = Fit.None`, `Drive = Drives.Boost`, `Shot` left at the default (unused — Pepperbox/Rod fire their own `Shots.Pepper`/`Shots.Rod` rows via `BoreSpec`).
- `ClassDef.Abilities = { Ab.Pepperbox, Ab.Rod, Ab.Ramjet, Ab.Slingshot }` (Ships.cs:635).
- Ability ids: `pepperbox` (Space, Hold, Weapon), `rod` (F), `ramjet` (Q), `slingshot` (E, TakesPoint).
- **Check methods that exist** (`tools/smoketest/SmokeTest.cs.txt`, grep exact names):
  `LaneA6dPepperRowChecks`, `LaneA6dPepperChecks`, `LaneA6dOneDart` (helper), `LaneA6dRodRowChecks`, `LaneA6dRodChecks`, `LaneA6dRodRecoilChecks`, `LaneA6dRamjetRowChecks`, `LaneA6dRamjetChecks`, `LaneA6dRamjetHostWatch`/`LaneA6dRamjetHostChecks`/`LaneA6dRamjetGuestChecks` (`LaneA6dRamjetSeen`), `LaneA6dSlingChecks`, `LaneA6dSlipChecks`.
- Row literals in `LaneA6dPepperRowChecks`/`RodRowChecks`/`RamjetRowChecks` match `Ships.cs` exactly. None run in the engine yet (ledger_main.md: build phase complete, test phase waits).

## OPEN
- None found for the Dart's own rows, keys or ability order — every literal in `Ships.cs`/`Abilities.cs` matches its sourced check in `SmokeTest.cs.txt` and the newest applicable design doc.
