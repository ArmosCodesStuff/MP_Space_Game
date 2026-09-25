# SNIPER — spec card

## Identity
- Heavy, hull **240**, top speed **190 u/s** (`thrust` 130, `reverse_thrust` 60, `reverse_speed` 70, `turn_radius` 55, `turn_rate` 2.2).
- Drive: **boost** (the nine's Surge, not warp). `strafe_speed` 95, `strafe_thrust` 380. *kits_v31.md §2, §3.4; `Drives.cs` row `Boost`.*
- Passive: **Active reload** (replaces Overcharge — see Rulings). *sniper_active_reload.md §0.*
- Role: plants, charges, pierces, flares missiles; no Overcharge is built. *kits_v31.md §2; README.md ruling.*

## Primary — Railgun (Space: hold to charge, release to fire)
One round in the chamber. Held, the bar fills blue over **0.8 s** (`rail_charge`) to full; released early it still fires the full 2500×14 u line, ramped **40%** at 0 s to 100% at full (`rail_tap` 40%). After the shot the chamber **reloads itself in 3.0 s** (`rail_reload`); the v1 tap band ("8, first body") is deleted. Full-charge damage **120**; a perfectly-timed reload press enhances the *next* round **x1.5 → 180**. Reach 2500 u, width 14 u. Source: `sniper_active_reload.md` §1.3–1.6, §2.1; replaces v1/v3's 1.6 s charge and Overcharge.

**The timing press** (also Space): while reloading, the *first* press is judged against the sweet spot (**40–60% of the reload**, 1.2–1.8 s at base, scales with rate lifts); later presses that reload do nothing. Perfect: round enhanced, fires x1.5 whenever loosed. Miss: seats plain, box greys for the rest of that reload. A held Space across the reload's end never starts a charge — the two strokes never overlap. Source: `sniper_active_reload.md` §1.1–1.4, §2.2.

## Abilities, in LEARN order (walls: kits_v31.md §3.6 table)

### 1. Anchor — key **F**, learned L1, cooldown **12 s from release**
Plants the ship up to **8 s** (`anchor_time`), immobile (a Lifts share of x0, refuses the boost — ANCHORED); every railgun interval (charge AND reload, through `Cadence`) runs at **x2.5** (`anchor_rate`); reach **x1.4 → 3500 u** (`anchor_reach`); F again weighs it in **0.3 s** (`anchor_release`). Anchored, the reload is 1.2 s and the sweet spot 0.48–0.72 s. Source: kits_v3.md §3.4 ("v1 Anchor exactly"); numbers confirmed unchanged by `sniper_active_reload.md` §0, §1.5.

### 2. Tether mine — key **Q**, learned L3, cooldown **12 s a charge, 2 charges, 2 out at most**
Drops a mine astern, live 0.5 s later; the first raiding craft within **170 u** sets it off, and every raider within 170 u is held **3 s**. Never a boss. Source: kits_v2.md §3 SNIPER card ("v1 Tether mine"); zone geometry unchanged per kits_v31.md §3.8.

### 3. Flares — key **E**, learned L6, cooldown **16 s, never refused**
Six flares (`flare_count`) in a ring **180 u** out (0.6 s to coast), the first dead astern, burning where they stop for **5 s**. Guided missiles within **500 u** of a burning flare turn onto it (sticky) and burst harmlessly; a predicted-missile landing mark within **300 u** with ≥1.0 s of flight left slides onto it; a fighter (light/heavy, never boss) within **150 u** is DAZZLED until 4 s after leaving — cannot latch anew, throws no missile, but an existing web/latch is untouched (only the Echo's EMP cures that). Source: kits_v2.md §3 SNIPER card ("Flares").

## Interactions (kits_v31.md §7)
- **FR Time on target vs SN railgun** — accepted, share `Lines`: the railgun is one charged line along the nose, TOT up to 4 converging on a paint.
- **Capitals' warp vs the web-breakers** — Sniper has no warp; its web answer is **Flares** (prevents new latches).
- **SN Fracture (if chosen) vs WR Venom** — moot: Fracture never built (one of three v3.1 candidates for "the new piece"; active reload replaced all three).

## Rulings touching this class
- README.md: "the new piece is the ACTIVE RELOAD… Everything on Space… REPLACES Overcharge: no Overcharge rows (`rail_over_*`) are built."
- README.md OPEN (default stands): "should a perfect press also finish the reload at once? Default **no**." Code (`ActiveReload.Seat`) implements no — a perfect press only sets the multiplier; the 3.0 s reload always runs.
- kits_v31.md §10 decision 9 marked **CLOSED by the ruling**, pointing at `sniper_active_reload.md`.

## BUILT (scripts/Ships.cs, scripts/Abilities.cs, scripts/ActiveReload.cs)
- `ClassDef` row: `ShipClass.HeavySniper`, `Ready = true`, `Fit = Fit.None`, `Drive = Drives.Boost`.
- `ClassDef.Abilities = { Ab.Railgun, Ab.Anchor, Ab.Tether, Ab.Flares }` — no `Ab.FireMode`/cannon; the v1 light-cannon-on-Space plus railgun-on-F from live code is gone, matching the kit.
- Ability ids: `railgun` (Space, Hold, Weapon, `Reload = ActiveReload.Rail`), `anchor` (F), `tether` (Q), `flares` (E).
- Rows: `rail_damage` 120, `rail_charge` 0.8, `rail_tap` 40%, `rail_reload` 3.0, `rail_spot_at` 40%, `rail_spot` 20%, `rail_perfect` 1.5x, `rail_range` 2500, `rail_width` 14; `anchor_time` 8, `anchor_rate` 2.5, `anchor_reach` 1.4, `anchor_release` 0.3, `anchor_cooldown` 12; `tether_charges` 2, `tether_recharge` 12, `tether_hold` 3, `tether_most` 2; `flare_cooldown` 16, `flare_count` 6. `rail_cooldown` deleted. All match `sniper_active_reload.md` §2.1.
- `ActiveReload.cs`: `ReloadSpec`, `enum Chamber`, `enum Verdict`, doors `Spent`/`Seat`/`Take`/`Judge`/`Press`, struct `View`.
- **Check methods** (`tools/smoketest/SmokeTest.cs.txt`, exact names): `LaneA6cReloadJudgeChecks`, `LaneA6cReloadChecks`, `LaneA6cReloadHostChecks`, `LaneA6cReloadGuestChecks`, `LaneA6cReloadBarChecks`, `LaneA6cAnchorChecks`, `LaneA6cAnchorGuestChecks`, `LaneA6cZoneChecks` (tether+curtain geometry, shared with Warden), `LaneA6cTetherChecks`, `LaneA6cFlaresChecks`, `LaneA6cFlaresGuestChecks`. None have run in the engine yet (`ledger_main.md`: build complete, nothing run).

## OPEN
- kits_v31.md §4's own power table still lists SNIPER realistic 51.0 ("Overcharge") — stale next to `sniper_active_reload.md` §1.6's **51.7** (flawless 63.4, sheet 75.4), which the code implements. Not a code/spec gap; a reader of kits_v31.md §4 alone sees the wrong number.
