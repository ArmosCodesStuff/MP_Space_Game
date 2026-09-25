# TENDER — spec card

## Identity
- Hull 380, category **freighter** (kits_v31.md §4). Drive **boost** (not a capital: no warp).
- Top speed 120 u/s (up from 85; kits_v31.md §3.5, decision 2 default; numbers §8 "freighter top 120: agreed"), same as the other two freighters.
  Thrust 63.5, reverse thrust 28.2, reverse top 42.4, turn rate 0.85, turn radius 150.
- Strafe (Shift+A/D): 60 u/s, full slide in 0.5 s at thrust 120 (kits_v31.md §3.4 "Strafe").
- Boost (V): +50% top/thrust/strafe for 3 s, cooldown 15 s from the press; refused ANCHORED; does not break a web
  (kits_v31.md §3.4, decision 3). **Resupply never touches V** — the drive is not in `ClassDef.Abilities`
  (kits_v31.md §3.4 "the drive against every class's moves").
- PD x2, passive.
- Realistic DPS 46.0 (sheet 48.4); kill from L6 67 s; kill L1 walled 68 s (kits_v31.md §4 / kits_v3.md §4).

## Primary — Mending lance (Space, held)
Beam out of the main barrel onto the **first body it touches**: 650 u range, 4 dmg every 0.1 s = 40 DPS to a
hostile (main_damage/main_interval), turret turn Tau/4 rad/s (~90°/s). The same beam **mends 0.8 a tick = 8/s**
(lance_heal) to the first friend it touches — a pilot, a sentry, the fleet. Source: table row "Mending lance"
(kits_v31.md §2 / kits_v2.md §2, "one beam, heal or harm") — the DPS/heal split is code only; no spec file gives
the split numbers (see OPEN). Never walled.

## Abilities, in LEARN order
| # | key | wall | cooldown | ability |
|---|---|---|---|---|
| 1 | F | L1 | 24 s | Overdrive field |
| 2 | Q | L3 | 30 s | Repair field |
| 3 | E | L6 | 30 s | Resupply |
(The lance and PD are never walled — kits_v31.md §3.6.)

**1. Overdrive field (F).** For **8 s** (overdrive_time), you and every friendly pilot within **500 u**
(field_radius, an Aura row) fire **x1.5** as fast (overdrive_mult) — guns, PD, sentries and craft. Cooldown
**24 s**. Source: kits_v2.md TENDER table row ("Overdrive field") for the name/key; numbers (8 s / x1.5 / 500 u /
24 s cd) are code-built, not spelled out in any plan file (see OPEN).

**2. Repair field (Q).** For **8 s** (repair_time), every friendly hull within **500 u** — you included — is
mended **2% of its full hull a second** (repair_share). Cooldown **30 s**. Source: kits_v2.md's text fix
("Resupply reads 'every friendly within 500 u, you included'" — the same 500 u aura also governs Repair per the
built `field_radius` row); the 2%/8s/30s figures are code-built (see OPEN).

**3. Resupply (E).** Cuts **8 s** off every ability still cooling — yours and every friendly pilot's within 500 u,
you included — never the drive, never Resupply itself. Cooldown **30 s**. Source: kits_v2.md §3 text fix
("every friendly within 500 u, you included"; cuts 8 s off Overdrive and Repair); only the 30 s cooldown is
code-built.

## Interactions (kits_v31.md §7)
- **Boost vs Dart's Ramjet/sprint, Wraith's Veil** — accepted, additive speed lifts; only the Dart prices speed
  into damage.
- **Capitals' warp vs the web-breakers** — the Tender has **no web-break of its own**, "none but hull, the
  Bubble and the sentries" (decision 3's list names Freighter/Tender/Dart together — the Tender has no Bubble
  or sentries either; its only defence against a web is its hull. Flagged, not a disagreement — the sentence as
  written groups the three freight/light classes' hull-only defence together).
- No other pair in §7 names the Tender.

## Rulings that touch this class
- "Approved as written: ...Tender..." (README) — no owner change beyond the shared systems below.
- Warp is capital-only; V is the +50%/3s/15s-cooldown boost (README).
- 6 chip slots (≤3 combat, ≤3 utility), walled 2/4/8/10/12/14; none fitted at start (README, kits_v31.md §3.6).
- Item pass: freighter hull category, 8-12 item lines, +10% compounding a tier (README).
- Level walls: Overdrive L1, chip1 L2, Repair L3, chip2 L4, Resupply L6, chips 3-6 at L8/10/12/14 (kits_v31.md §3.6).

## BUILT (scripts/, as of ae8ae99)
- `ShipClass.FreightTender` (Ships.cs ~344), `Name = "TENDER"`, `Fit = Fit.Guns | Fit.Pd` (no `Fit.Deploy` —
  ledger_kits6b.md D40), `Primary = Primary.Beam`, `Drive = Drives.Boost`.
- `Abilities = { Ab.Lance, Ab.Overdrive, Ab.Repair, Ab.Resupply }` (Ships.cs). Ability ids: `lance`
  (`PlayerShip.LanceSlot`), `overdrive`, `repair`, `resupply` (Abilities.cs).
- Rows (Ships.cs FreightTender.Rows): `lance_heal` 0.8, `field_radius` 500, `overdrive_mult` 1.5 / `_time` 8 /
  `_cooldown` 24, `repair_share` 0.02 / `_time` 8 / `_cooldown` 30, `resupply_cut` 8 / `_cooldown` 30.
- Check methods (SmokeTest.cs.txt, grep exact): `LaneA6bLanceChecks`, `LaneA6bLanceTriggerChecks`,
  `LaneA6bOverdriveFieldChecks`, `LaneA6bRepairFieldChecks`, `LaneA6bResupplyChecks`; rung 5:
  `LaneA6bFieldsHost/Guest`; shared: `LaneBHelmChecks`, `LaneBStrafeChecks`, `LaneBBoostChecks`.
- Per ledger_kits6b.md (J7/J8, resumed after D37's Mend.Give dependency landed; gate-fix J11 applied): compiles,
  typecheck 0 errors, verify -Quick ALL CHECKS PASSED. Engine rungs (solo x2, six x2, screens) owed in the test
  phase, not yet run.

## OPEN
- **No plan file gives the Tender's own numbers.** kits_v2.md/kits_v3.md/kits_v31.md list Tender as "unchanged;
  see v1 §3" for its card, but no `kits_v1.md` exists in this repo (docs/plans/ has v2, v3, v3.1 only). Every
  number on this card (lance 4/0.1s/650u/0.8 heal, overdrive x1.5/8s/500u/24s, repair 2%/8s/30s, resupply's 30 s
  cooldown (its 8 s and 500 u are in kits_v2 §3))
  comes from the CODE and ledger_kits6b.md, not from a readable spec file. Not a disagreement — there is nothing
  to disagree with — but a fix agent cannot check these numbers against a spec, only against the code and the
  ledger's own account of building them.
- The lance's beam DPS (40) and heal rate (8/s) split is not stated as a ratio anywhere in the plans; both read
  straight off the code.
