# WRAITH — spec card

## Identity
- Light, hull **220**, top speed **260 u/s** (`thrust` 190, `reverse_thrust` 90, `reverse_speed` 95, `turn_radius` 35, `turn_rate` 3.0). *Ships.cs:691-693.*
- Drive: **boost** (the nine's Surge, not warp). `strafe_speed` 130, `strafe_thrust` 520. Shift+A/D strafes; V boosts (+50% top/thrust/strafe, 3 s, 15 s cooldown; never breaks a web). *kits_v31.md §2, §3.4; `Drives.cs` row `Boost`.*
- Passive: **Backstab** — `backstab_mult` **x1.5** on a blow landed within `backstab_arc` **60°** of a heading target's tail. *Ships.cs Backstab rows; kits_v31.md §3.4 line 284.*
- `Fit = Fit.Guns`, `Shot = Shots.Pellet` — one main mount, cursor-aimed, fires the pellet round. *Ships.cs:685-689.*
- Role: "unseen poisoned shotgun from behind." *kits_v31.md §2.*

## Primary — Ambush Scattergun (Space, mouse aims)
`main_count` 1, `main_damage` **7/pellet** every `main_interval` **0.75 s** to `main_range` **320 u**, `shell_speed` **620 u/s**; `scatter_pellets` **7** fanned `scatter_spread` **±10°** a volley = **65.33 DPS** point-blank (49 dmg/volley ÷ 0.75 s). Row `pellet`: not guided, one stop. *Ships.cs Nums/Rows; `Shots.Of("pellet")`; kits_v31.md §2 table.*

## Abilities, in LEARN order (`ClassDef.Abilities[2..4]`; Guns/FireMode are the shared gun-primary abilities, unwalled)

### 1. Veil — key **F**, cooldown **18 s** (`veil_cooldown`)
Press: **5 s** (`veil_time`) nothing hostile can pick it (`Status.Untargetable`; it can still be hit) at `veil_speed` **x1.35** top speed. The next volley fired hits `veil_break` **x3**; firing (`OnFire`) drops the veil early. *Ships.cs Veil rows; `Ab.Veil` (Abilities.cs:877).*

### 2. Venom — key **Q**, cooldown **22 s** (`venom_cooldown`)
Press: coats the scattergun for `venom_time` **6 s**. Each landed pellet applies a poison dose (`DoseRows.Venom`, tick every 0.5 s), up to `venom_cap` **10** doses on one hull, each eating `venom_dps` **1.25/s**, lasting `venom_last` **5 s** after the last dose lands. *Ships.cs Venom rows; `Ab.Venom` (Abilities.cs:851).*

### 3. Shadow Step — key **E**, cooldown **14 s** (`step_cooldown`)
Press: blinks `step_behind` **140 u** behind the selected hostile, up to `step_reach` **900 u** off, nose on it, speed kept; any web on the Wraith lets go. *Ships.cs Shadow step rows; `Ab.Step` (Abilities.cs:863).*

## Interactions (kits_v31.md §3.4, §7)
- **Veil's x1.35 top + boost's x1.5 add: `1 + 0.35 + 0.5 = x1.85 = 481 u/s`.** *§3.4:283.*
- **Backstab**: strafe holds a rear arc on prey while the nose stays aimed at it — accepted, no special case. *§3.4:284.*
- **The capitals' warp vs the web-breakers** — the nine lose the warp's web break; the Wraith's own answer is **Shadow step**. *§7:467.*
- Strafe vs Dart Slingshot vs Warrior Lunge vs Wraith Shadow step: accepted as four distinct mechanisms (slide/snap/dash/blink). *§7:466.*
- **SN Fracture (if chosen) vs WR Venom** — flagged, unresolved in the source itself ("as v3 (flagged)"); moot as built: the Sniper's shipped piece is the ACTIVE RELOAD (README ruling; sniper_active_reload.md), and Overcharge, Fracture and Deadeye are never built (decision 9, closed). *§7:471.*
- No class slows anything — flagged in the source as a standing note, not specific to the Wraith. *§7:472.*

## Rulings touching this class
- README.md: "Approved as written: Battleship, Bastion, Tender, Warrior, Echo, Wraith … with the changes below" — no owner edit fell on the Wraith's numbers.
- kits_v31.md decision 1 (strafe, default stands): Shift+A/D is what "holds a rear arc" for Backstab, rather than the nose following the cursor.
- kits_v31.md decision 3 (default stands): the boost never breaks a web, so Shadow step is the Wraith's only web answer.

## BUILT (scripts/Ships.cs, scripts/Abilities.cs)
- `ClassDef` row: `ShipClass.LightWraith`, `Ready = true`, `Fit = Fit.Guns`, `Shot = Shots.Pellet`, `Drive = Drives.Boost`.
- `ClassDef.Abilities = { Ab.Guns, Ab.FireMode, Ab.Veil, Ab.Venom, Ab.Step }` (Ships.cs:733) — ids `guns, firemode, veil, venom, step`. `Unlocks.Walled` excludes `Guns`/`FireMode` (both `Weapon = true`), so the level-walled 1-2-3 are Veil (L1), Venom (L3), Step (L6) — confirmed by `Unlocks.Nth(ShipClass.LightWraith, 1/2/3)` in the row checks below.
- **Check methods that exist** (`tools/smoketest/SmokeTest.cs.txt`, grep exact names):
  `LaneA6dScatterRowChecks`, `LaneA6dScatterChecks`, `LaneA6dBackstabChecks`, `LaneA6dVeilRowChecks`, `LaneA6dVeilChecks`, `LaneA6dVenomRowChecks`, `LaneA6dVenomChecks`, `LaneA6dStepRowChecks`, `LaneA6dStepChecks`. None run in the engine yet (ledger_main.md: build phase complete, test phase waits).

## OPEN
- None found for the Wraith's own rows, keys or ability order — every literal in `Ships.cs`/`Abilities.cs` matches its sourced check in `SmokeTest.cs.txt` (`LaneA6dScatterRowChecks` confirms hull 220, 7 pellets of 7 at 0.75 s to 320 u = 65.33 DPS, backstab x1.5/60°; `LaneA6dStepRowChecks` confirms the full 5-id learn order) and the newest applicable design doc.
- `Ab.FireMode` is built on G for this class; kits_v31 §2 lists no fire-mode action for it and names G unused (the same R/G gap as the Battleship card's OPEN). Not resolved here.
