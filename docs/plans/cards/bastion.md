# BASTION — spec card

## Identity
- Hull 420, category **freighter** (kits_v31.md §4). Drive **boost** (not a capital: no warp).
- Top speed 120 u/s (up from 85; kits_v31.md §3.5, decision 2 default; numbers §8 "freighter top 120: agreed"), same as the other two freighters.
  Thrust 63.5, reverse thrust 28.2, reverse top 42.4, turn rate 0.85, turn radius 150.
- Strafe (Shift+A/D): 60 u/s, full slide in 0.5 s at thrust 120 (kits_v31.md §3.4 "Strafe").
- Boost (V): +50% top/thrust/strafe for 3 s, cooldown 15 s from the press; refused ANCHORED; does not break a web
  (kits_v31.md §3.4, decision 3).
- PD x2, passive.
- Realistic DPS 47.0 (sheet 55.0); kill from L6 66 s; kill L1 walled 64 s (kits_v31.md §4 / kits_v3.md §4).

## Primary — Siege mortar (Space, Lob onto the cursor)
Lands 150-1100 u out (mortar_min / main_range), 1.4 s flight (mortar_flight), 110 u blast radius (mortar_blast):
58.75 dmg every 2.35 s = 25.0 DPS on one target (main_damage/main_interval). Source: table row "Siege mortar"
(kits_v31.md §2), numbers confirmed against Ships.cs comment "the siege mortar: 58.75 in 110 u every 2.35 s (25
DPS on one), 150-1100 u, 1.4 s in the air". Never walled.

## Abilities, in LEARN order
| # | key | wall | cooldown | ability |
|---|---|---|---|---|
| 1 | F | L1 | 12 s | Bunker buster |
| 2 | Q | L3 | 30 s | Shockwave |
| 3 | E | L6 | 22 s | Gravity well |
(The mortar and PD are never walled — kits_v31.md §3.6.)

**1. Bunker buster (F).** One heavy round from the main barrel toward the cursor, **380 u/s**, **1400 u** range,
stopping on the first body: **180** dmg, **x2 (360)** on a boss or structure, **1/4 through a pylon's shield**
(buster_damage/speed/range; `Shots.Buster`: `Versus = Boss|Structure, VersusMult = 2, Through = 0.25`, confirmed
in scripts/Shots.cs). Cooldown **12 s**. Source: kits_v2.md §2 Missiles ("BA Buster: a bunker-buster; it stops
on the first body and deals x2 to a boss or structure"); only the 1/4 through a pylon's shield is code-built.

**2. Shockwave (Q).** Throws everything within **1000 u** (wave_range) **1000 u** clear (wave_push); what cannot
be thrown — a boss, a structure — is **held 3 s** (wave_disable) instead. Cooldown **30 s**. Source: kits_v2.md
table ("Shockwave"); the numbers are code-built (see OPEN). Named in kits_v31.md §7 as one of the nine's own
answers to a web ("Shockwave (Bastion, throws)").

**3. Gravity well (E).** A well at the cursor, up to 900 u out, **280 u** reach (Zones.cs "well" row), **6 s**
life: drags loose raiding craft to its centre, lights at **200 u/s** (well_light), heavies at **100 u/s**
(well_heavy). Bosses, structures and anything latched stay put. Cooldown **22 s**. Source: kits_v2.md table
("Gravity well") for the name/key; the numbers are code-built (see OPEN).

## Interactions (kits_v31.md §7)
- **DD rip vs Bunker buster** — accepted, flagged: both add boss/structure damage. The rip (1% of the target's
  max hull + 10) is the only damage in the game read from the target's own hull; it is about 3% of every kill and
  grows with the party. The lever if flying finds it too strong together is `grapnel_rip_share` (the DD's row,
  not the Bastion's).
- **Boost vs Dart's Ramjet/sprint, Wraith's Veil** — accepted, additive speed lifts; only the Dart prices speed
  into damage.
- **Capitals' warp vs the web-breakers** — the Bastion's **own answer to a web is its Shockwave** (it throws the
  webber off, per §7's list), unlike the Freighter/Tender/Dart which have none.

## Rulings that touch this class
- "Approved as written: ...Bastion..." (README) — no owner change beyond the shared systems below.
- Warp is capital-only; V is the +50%/3s/15s-cooldown boost (README).
- 6 chip slots (≤3 combat, ≤3 utility), walled 2/4/8/10/12/14; none fitted at start (README, kits_v31.md §3.6).
- Item pass: freighter hull category, 8-12 item lines, +10% compounding a tier (README).
- Level walls: Buster L1, chip1 L2, Shockwave L3, chip2 L4, Well L6, chips 3-6 at L8/10/12/14 (kits_v31.md §3.6).

## BUILT (scripts/, as of ae8ae99)
- `ShipClass.FreightBastion` (Ships.cs ~382), `Name = "BASTION"`, `Fit = Fit.Guns | Fit.Pd` (no `Fit.Deploy` —
  ledger_kits6b.md D40), `Primary = Primary.Lob, LobSide = Missiles.Mortar`, `Drive = Drives.Boost`.
- `Abilities = { Ab.Guns, Ab.Buster, Ab.Shockwave, Ab.Well }` (Ships.cs). Ability ids: `buster`, `shockwave`,
  `well` (Abilities.cs `Ab.Buster` / `Ab.Shockwave` / `Ab.Well`); the well is a `Zones.All` row id `well`
  (Zones.cs, `Reach 280, Life 6, Prey Pullable`), not its own stat-only ability.
- Rows (Ships.cs FreightBastion.Rows): `mortar_min/flight/blast`, `buster_damage/speed/range/cooldown`,
  `wave_range/push/disable/cooldown`, `well_light/heavy/cooldown` (well_range/radius/time were deleted at the
  kits6b gate fix J11 — the Zones row's own literals replace them, per ledger_kits6b.md).
- Check methods (SmokeTest.cs.txt, grep exact): `LaneA6bMortarChecks`, `LaneA6bBusterChecks`,
  `LaneA6bBusterBossChecks`, `LaneA6bSiegeChecks`, `LaneA6bWellChecks`; shared: `LaneBHelmChecks`,
  `LaneBStrafeChecks`, `LaneBBoostChecks`.
- Per ledger_kits6b.md (J1-J6, gate-fix J11 applied): compiles, typecheck 0 errors, verify -Quick ALL CHECKS
  PASSED. Engine rungs (solo x2, six x2, screens) owed in the test phase, not yet run.

## OPEN
- **No plan file gives the Bastion's own numbers**, same gap as the Tender: kits_v2/v3/v31.md list Bastion as
  "unchanged; see v1 §3", and no `kits_v1.md` exists in this repo. The buster's quarter-through, the
  shockwave's 1000/1000/3s/30s, and the well's 280u/6s/200/100/22s all come from the CODE and ledger_kits6b.md,
  not a readable spec. Not a disagreement — nothing to disagree with — but unverifiable against a spec.
- ledger_kits6b.md (~L202) notes an old-truths sweep for "the Bastion's hull 400" replaced by 420 — an earlier,
  wrong figure now corrected in the code (420 stands, matches kits_v31.md §4). Not a live disagreement.
