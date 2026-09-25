# FREIGHTER — spec card

## Identity
- Hull 450, category **freighter** (kits_v31.md §4). Drive **boost** (not a capital: no warp).
- Top speed 120 u/s (up from 85; kits_v31.md §3.5, decision 2 default; numbers §8 "freighter top 120: agreed"). Thrust 63.5, reverse thrust 28.2,
  reverse top 42.4, turn rate 0.85, turn radius 150.
- Strafe (Shift+A/D): 60 u/s, full slide in 0.5 s at thrust 120 (kits_v31.md §3.4 "Strafe").
- Boost (V): +50% top/thrust/strafe for 3 s, cooldown 15 s from the press; refused ANCHORED; does not break a web
  (kits_v31.md §3.4, decision 3).
- PD x2, passive.
- Realistic DPS 56.0 (sheet 65.0); kill from L6 55 s; kill L1 walled 54 s (kits_v31.md §4).

## Primary — Spotter cannon (Space)
800 u range, 31.25 dmg every 1.25 s = 25.0 DPS, shell 560 u/s. A hit paints the target for 5 s (paint_time), one
target at a time. Source: kits_v2.md FREIGHTER card (unchanged since). Never walled.

## Abilities, in LEARN order
| # | key | wall | cooldown | ability |
|---|---|---|---|---|
| 1 | F | L1 | 16 s | Time on target |
| 2 | Q | L3 | 25 s | Bubble |
| 3 | E | L6 | 20 s | Redeploy |
(R Sentry throw/recall and PD are never walled — kits_v31.md §3.6.)

**1. Time on target (F).** Needs a paint under 5 s old. Every gun within **1500 u** of the painted target
(tot_reach) — the spotter plus each landed sentry — fires one **hitscan** rail line at it, **14 u** wide
(tot_width), all in the same host tick. **40** dmg (tot_damage) to everything hostile on the line, painted
target included: 40 with no sentries, 160 with three. Allies never hit. Cooldown **16 s** (tot_cooldown) from
the press. Refused: NO PAINT, COOLING. Realistic 8.0 (sheet 10.0). Source: README ruling ("Freighter F = TIME ON
TARGET... hitscan rail lines like the Sniper's, all landing at once") > kits_v31.md §3.1 > numbers §8 R5 keeps
kits_v31's 8.0 realistic over the numbers model's own figure (a 0.7 DPS gap inside the estimate).

**2. Bubble (Q).** Pool 400 (bubble_pool), radius 260 u, up 8 s, cooldown 25 s. A shield over you and every
friendly hull near, soaking damage until the pool is spent or the time runs out. Source: kits_v2.md FREIGHTER card
(unchanged; "Approved as written" per README).

**3. Redeploy (E).** Every sentry out folds and lands in a 150 u ring round you 1.0 s later (redeploy_flight),
each keeping the hull it had. Cooldown 20 s. Source: kits_v2.md FREIGHTER card (unchanged).

## Sentries (R, weapon's second action, never walled)
Thrown to the cursor up to 600 u (deploy_reach), lands in 0.8 s (deploy_flight); R within 60 u of one of yours
recalls it (0.8 s flight back). 3 out max, 6 s between throws (a recall is free), 120 hull, 650 u reach, 10 DPS
each. Targets **the painted target first** whenever it is within 650 u; otherwise **anything hostile** ranked by
`Turret.Rank` (missiles/hulled rounds, then lights/fighters, then heavies/bosses/structures, a dummy last).
Source: kits_v3.md §3.3 (throw/recall numbers, v1 numbers kept) + README ruling ("sentries PREFER painted
targets, else anything hostile, bosses included") which supersedes kits_v3 decision 14's paint-gate default.

## Interactions (kits_v31.md §7)
- **TOT vs Sniper's railgun** — accepted, they share the `Lines` table: the railgun is one charged line along the
  nose; TOT is up to 4 lines from scattered guns converging on the paint every 16 s.
- **TOT vs Battleship's Broadside** — accepted (v2): converging scattered guns against a turned side.
- **Boost vs Dart's Ramjet/sprint, Wraith's Veil** — accepted, all additive speed lifts; only the Dart prices
  speed into damage.
- **Capitals' warp vs the web-breakers** — the Freighter has **no web-break of its own**, "none but hull, the
  Bubble and the sentries" (decision 3). It cannot clear a web the way Echo/Warrior/Wraith/Sniper/Warden/Bastion do.

## Rulings that touch this class
- F = Time on target, hitscan, all landing at once; sentries launch to cursor (600 u/0.8 s, R recalls); sentries
  prefer the paint, else anything hostile, bosses included (README, binding).
- Warp is capital-only; V is the +50%/3s/15s-cooldown boost (README).
- 6 chip slots (≤3 combat, ≤3 utility), walled 2/4/8/10/12/14; none fitted at start (README, kits_v31.md §3.6).
- Item pass: freighter hull category, 8-12 item lines, +10% compounding a tier (README).
- Level walls: Tot L1, chip1 L2, Bubble L3, chip2 L4, Redeploy L6, chips 3-6 at L8/10/12/14 (kits_v31.md §3.6).

## BUILT (scripts/, as of ae8ae99)
- `ShipClass.FreightHauler` (Ships.cs ~295), `Name = "FREIGHTER"`, `Fit = Fit.Guns | Fit.Pd | Fit.Deploy`,
  `Shot = Shots.Spotter`, `Drive = Drives.Boost`.
- `Abilities = { Ab.Guns, Ab.Deploy, Ab.Tot, Ab.Bubble, Ab.Redeploy }` (Ships.cs). Ability ids: `guns`, `deploy`,
  `tot`, `bubble`, `redeploy` (Abilities.cs `Ab.Tot` / `Ab.Bubble` / `Ab.Redeploy` / `Ab.Deploy`).
- Rows (Ships.cs FreightHauler.Rows): `deploy_damage/interval/range/hull/max/cooldown/reach/flight`,
  `recall_pick`, `paint_time`, `tot_damage/width/reach/cooldown`, `bubble_pool/radius/time/cooldown`,
  `redeploy_ring/flight/cooldown` — all match the numbers above.
- Check methods (SmokeTest.cs.txt, grep exact): `LaneA6bSpotterChecks`, `LaneA6bTotChecks`,
  `LaneASentryPreferChecks`, `LaneASentryThrowChecks`, `LaneA6bBubbleCoverChecks`, `LaneA6bRedeployChecks`,
  `FieldsBubbleChecks`, `FieldsContractChecks`, `FieldsGuestBubbleChecks`; rung 5: the paint wire, inline in
  the arena host/guest blocks (comment labels LaneA6bPaintWireHost / LaneA6bPaintWireGuest,
  SmokeTest.cs.txt ~22400 / ~22716, not methods), `LaneA6bTotHost`, `LaneA6bTotGuest`; shared:
  `LaneBHelmChecks`, `LaneBStrafeChecks`, `LaneBBoostChecks`.
- Per ledger_kits6b.md (J1-J11, gate fix applied): compiles, typecheck 0 errors, verify -Quick ALL CHECKS PASSED.
  Engine rungs (solo x2, six x2, screens) are owed in the test phase, not yet run.

## OPEN
- None. Every number in kits_v31/v3/v2.md and numbers_curve_raids_items.md §8 matches the built rows exactly
  (TOT 40/14/1500/16, sentry 600/0.8/3/6/120/650/10, bubble 400/260/8/25, redeploy 150/1.0/20, spotter
  31.25/1.25/800/560, hull 450, top 120, strafe 60/120).
