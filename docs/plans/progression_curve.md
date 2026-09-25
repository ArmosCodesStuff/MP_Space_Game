# CURVE: boss scaling and rewards fitted to the reference pilot

This is read-only design work. I edited nothing in the repo and ran no engine, harness or build. Every number
comes from `curve.py` in this scratchpad (run `python curve.py today 500 1.10` or `python curve.py capped 500 1.10`).
Salvage income comes from `player_model.py`. Class numbers are placeholders from the v1 sign-off and the v2 drafts.
Everything is parameterised by a class's base hull **B**, bare realistic DPS against a boss **D**, chip count **N**
and primary share **f_p**, so the revised kits can be plugged in (§2.6). Appendix B lists what I re-read in source and
what I took from the other agents.

---

## 0 · Verdict

1. **Boss scale becomes two curves read off one reference pilot.** The two curves are
   `HullScale(L) = Par DPS(L) / Par DPS(1)` and `DamageScale(L) = Par hull(L) / Par hull(1)`. They replace
   `Missions.LevelStep = 1.025`. By construction, a pilot on par kills the level's boss in **60 s at every level**. A
   pilot who stops dodging dies in the same number of seconds at every level: **31 s against the Lancer and 42 s against
   the Drake** for the median hull, within ±1 s from L1 to L40. The item pass then moves the bosses with no retune.
   Scope: **a new table** (`Par.cs`, the reference-pilot row).
2. **Today's 1.025 is wrong in shape, not only in level.** With today's items, Par's DPS grows 3.8-7% a level to L25,
   then 1.0-1.3%, and flattens past L40 (x3.38 at L40, x3.66 at L80). 1.025 stays behind it by up to 35% at L25 and then
   runs away after L40 (x7.0 at L80). With the capped items the item pass plans, Par is x2.48 at L40, close to
   1.025's x2.62, but the shape is still front-loaded. Past L40 the new curve plateaus by construction.
3. **The anchor is the main number.** Lancer hull L1 goes **760 → 3820**, which is 60 s x the median class's L1 par
   DPS of 63.7. The Drake goes **700 → 3520**, keeping today's 700/760 ratio.
4. **"65% of the salvage ladder" only means something if the ladder grows with the boss ladder.** Two changes:
   - **Gate:** a part's level can be bought up to `min(40, highest boss level cleared + 1)`, on either ladder.
   - **Re-priced ladder:** level n costs `500 x 1.10^n` (was `100 x 1.25^n`).

   The result: a pilot on boss and siege rewards alone sits at **60-85% of the open ladder** at every level. A pilot who
   also idles in the hub sits at 83-100%. Nobody can exceed 100%. The reference is **65%**. On the old ladder, 65% came
   at about L45, salvage growth was logarithmic, and hub idling had no ceiling.
5. **Salvage-levelled hull is now a visible share of hull.** It is **35%** of the reference's L40 hull with today's
   items and **28%** with capped items. A pilot who skips salvage at L40 takes **103 s** to kill the boss and dies in
   **19 s**, against the reference's 60 s and 31 s. A pilot at 100% of the gate wearing two hull parts takes **49 s**
   and dies in **63 s**.
6. **Four things the curve needs that are not numbers** (§5):
   - Gear levels keyed by **line**, not part id.
   - Gear levels **carried on the identity**. This is a live bug: the host builds every guest's sheet from the *host's*
     `Character.GearLevel`, so a guest's salvage does nothing in multiplayer.
   - Raiders take a **level** rather than a multiplier.
   - Sieges pay **2x credits and 2x crates**.
7. **Level skipping:** keep one boss per level (no rungs). Let the TIO select up to **2 levels past the newest unlocked**.
   The curve is gentle enough that +3 levels is a 62-68 s fight, not a wall.

---

## 1 · THE REFERENCE PILOT

**Who it is.** The reference is the **median class**, taken as the DESTROYER: B 395, D 54, N 3, f_p 0.73. It is the
closest class to the fleet median on both axes after kit chips: DPS 62.1 against a median of 61.7, and hull 454 against a
median of 415. The pilot flies the owner's pace: **4 bounty clears a level**. After the parity fix, 2 sieges pay the same.
It is measured after the 4th clear, so pilot level PL comes from today's EXP rules and runs 2, 8, 15, 30, 45, 59 at
L = 1, 5, 10, 20, 30, 40. It spends its points on Weapons and Hull, cheaper point first, which gives 7 and 7 at L40.
Cooling and Gunnery are left out because they are inverted today. It wears:
- **One hull part in the Shield slot:** Bulwark, +40% hull x rarity.
- **Damage parts that lift all its boss damage:** Rapid-pattern parts in the Weapon slot (the primary) and the Utility
  slot (the abilities). Each gives rate +60% x rarity, with a fixed −30% damage.
- **The kit frame and N kit chips.**

Its parts arrive over L1-5, when commons drop. The rarity scale of what it wears is 1.0 to L5, rising 0.05 a level to
2.0 at L25. Its gear stands at **65% of the gate**, g = 0.65 x min(40, L): **0.65, 3.25, 6.5, 13, 19.5, 26**. Every one
of these is a row of data (`Par`). None is code.

**Its multipliers.** They are normalised to its own L1 state, which is stock plus kit chips plus 1 Weapons point. These
ARE the boss scales.

| L | 1 | 5 | 10 | 20 | 30 | 40 | 60 | 80 |
|---|---|---|---|---|---|---|---|---|
| pilot DPS (= HullScale), today's items | 1.000 | 1.309 | 1.588 | 2.316 | 2.942 | 3.378 | 3.569 | 3.664 |
| pilot hull (= DamageScale), today's items | 1.000 | 1.440 | 1.636 | 2.106 | 2.554 | 2.830 | 2.863 | 2.929 |
| pilot DPS, capped items (+25% commons) | 1.000 | 1.323 | 1.486 | 1.897 | 2.240 | 2.478 | 2.587 | 2.642 |
| pilot hull, capped items | 1.000 | 1.284 | 1.412 | 1.710 | 2.000 | 2.177 | 2.203 | 2.253 |
| today's single step 1.025^(L−1) | 1.000 | 1.104 | 1.249 | 1.599 | 2.046 | 2.620 | 4.292 | 7.034 |

Local growth with today's items:

| levels | DPS per level | hull per level | what drives it |
|---|---|---|---|
| L1-5 | +7.0% | +9.5% | the first parts arrive |
| L5-25 | +3.8% | +2.6% | rarity climbs and gear levels open |
| L25-40 | +1.3% | +1.1% | gear levels only |
| past L40 | about 0.2% | about 0.2% | points only |

Every per-level row from L1 to L40 is in Appendix A.

---

## 2 · THE FORMULAS

### 2.1 Par (the new table)
For the median class (B, D, N), at a real-valued level L. Real values matter because raiders use fractional threat levels.

```
g(L)   = 0.65 · min(40, L)                         gear level on each worn part (the gate makes this the pace)
ρ(L)   = 1 + 0.05 · clamp(L − 5, 0, 20)            rarity scale of worn parts
p(L)   = clamp((L − 1) / 4, 0, 1)                  share of the reference parts owned
(w, h) = PL(L) − 1 points, cheaper-next between Weapons (+3% all damage) and Hull (+5 flat)
         PL(L) from 4 clears a level under Missions' EXP rules (300·L/PL, +250 first, +100, 1000 a level, floor L ≥ PL/2)
H_par(L) = (B + 5h) · (1 + 0.05N + p · a_sh · ρ · (1 + 0.05g))
D_par(L) = D · (1 + 0.05N + 0.03w − p · d_dmg) · (1 + p · u_dmg · ρ · (1 + 0.05g))
HullScale(L)   = D_par(L) / D_par(1)
DamageScale(L) = H_par(L) / H_par(1)
```
- Today's items: a_sh = 0.40 (Bulwark), u_dmg = 0.60 (the Rapid pattern's rate), d_dmg = 0.30.
- Capped items: a_sh = 0.25, u_dmg = 0.25, d_dmg = 0.

In code, Par does not copy these numbers. It builds the median class's own `ShipStats` from its reference line ids, at
fractional rarity ρ and level g, and reads `hull` and the sustained DPS total. The K window's sheet is enough, because
only ratios are used. Par is deterministic from static tables, so every peer computes the same values.

**If the owner wants constants instead of a table**, three segments fit Par to within 4% (TTK) and 1% (TTD):

| items | HullScale per level: L1-5 / L5-25 / L25-40 | DamageScale per level: same ranges | past L40 |
|---|---|---|---|
| today's | 1.0697 / 1.0382 / 1.0133 | 1.0954 / 1.0260 / 1.0109 | flat |
| capped | 1.0726 / 1.0245 / 1.0096 | 1.0646 / 1.0198 / 1.0091 | flat |

The segments must be re-fitted by hand after the item pass. Par would not need that.

### 2.2 Boss hull and damage (P = party size)
```
BossHull(L, P)  = Row.Hull · HullScale(L) · (1 + 0.6(P−1))
Row.Hull        = T · D_par(1) · r          T = 60 s;  D_par(1) = D_med · (1 + 0.05·N_med + 0.03)
                = 60 · 54 · 1.18 = 3823 → Lancer 3820;  Drake 3520 (r = 700/760, kept)
BossDamage(L,P) = move.Damage · DamageScale(L) · (1 + 0.2(P−1))    (move rows unchanged)
```
- Time to kill for any class: `TTK_c = 60 · D_par,med(1) / D_par,c(1)`. It is the same at every level (§3.2), as long
  as damage parts lift every class's whole damage (§6).
- Time to die for any class that stops dodging: `TTD_c = H_c(L) / (sheet · DamageScale(L) − 0.005 · H_c(L))`. Sheet is
  17.05 for the Lancer and 13.23 for the Drake, per unit of scale.

### 2.3 Siege, decoupled from the boss row
```
Base(L, P)  = 3820 · HullScale(L) · party     (1.0 x the Lancer's row — its OWN row, not 2 · ForLevel(L).Hull)
Pylon(L, P) =  480 · HullScale(L) · party     (0.125 x; was a literal 400)
Guns        = row damage · DamageScale(L)
```
- **Why decouple.** Today the base reads `ForLevel(L)`, which alternates Lancer and Drake by level parity, so its hull
  swings 8% between odd and even levels. It also does not care what the boss's hull is anchored to.
- **Quarry.** 1.5 boss hulls (5735 at L1) takes 90 s at par. Add 49 s of hops and about 30 s of garrison, and a siege is
  about 230 s. That equals two bounties at 120 s each, which is the time half of "2 sieges = 4 kills".
- **Recommended row edit: the base's guns.** Today they are 4 mounts x 18 every 2.2 s, which is 32.7 per unit of scale
  and fires through the shield. Cut them to about **8.4**, for example 2 mounts every 4.3 s. The siege's sheet then
  equals the Lancer's 17.1, and the median TTD is **31 s** instead of **11.6 s**. The Echo's TTD becomes 14.7 s instead
  of 5.8 s.

### 2.4 Reward(L)
```
EXP     = Kind.Exp · (300·L/PL + 250 first clear + 100)      unchanged (bounty 1, siege 2)
credits = 2000 · Kind.Pay · HullScale(L) · (1 + 0.5(P−1)) / P  S(L) → HullScale(L); siege Pay 1 → 2
crates  = Loot.CratesFor(L) · Kind.Crates                      NEW share: bounty 1, siege 2
salvage = crates scrapped (Economy.ScrapValue 100 / 250 / 600), unchanged
```
- Credits follow the boss's hull scale, so a bigger boss pays more: x1.31 / 1.59 / 2.32 / 2.94 / 3.38 at
  L5 / 10 / 20 / 30 / 40, against 1.10 / 1.25 / 1.60 / 2.05 / 2.62 today.
- **No new salvage reward is needed.** Once the ladder and gate are re-priced, the recycler's flat 3,520 salvage a level
  (from L11, on 4 clears) is exactly what keeps a rewards-only pilot at 60-85% of the gate.

### 2.5 The ladder
```
cost of level n → n+1 = round(500 · 1.10^n)        was round(100 · 1.25^n)
cap                  = min(40, highest boss level cleared on either ladder + 1)   NEW, checked at purchase only
+5% of the part's ups per level, 40 levels, drawbacks never scale                  unchanged
```
Level costs, 1st to 40th: 500, 550, 605 … 1,297 (11th) … 3,364 (21st) … 5,417 (26th) … 20,572 (40th). Reaching level
26 costs 54,591 per line, and the whole ladder costs 221,296 (was 3,008,864).

**The knob is `LevelCost`.** At 100, a rewards-only pilot sits at 100% of the gate until L30, and "65%" means nothing.
At 500 it sits at 60-85%.

Reference salvage per level, rewards only, on 3 levelled lines:

| L | income | the reference's next steps |
|---|---|---|
| 5 | about 800 a level | 0.65 x 3 steps of about 700 each |
| 20 | about 4k a level | 0.65 x 3 steps of about 1.8k each |
| 40 | 3.5k + 0.44k a level | 0.65 x 3 steps of about 5.4k each |

It runs slightly behind the reference from L35 on, and pilots who idle in the hub make up the difference.

### 2.6 Plugging in the v2 kits
- Set `D_med`, `N_med` and `B_med` to the median class after the kits land. For a kit set, `Row.Hull = 60 · D_med ·
  (1 + 0.05·N_med + 0.03)`.
- Each class's TTK and TTD then follow from its own B, D and N through §2.2. `curve.py` takes them from the `CLASSES`
  dict.
- The class spread of TTK is fixed by D, and the spread of TTD by B and N. Neither widens with level, provided §6.1 holds.

---

## 3 · THE TABLE

Today's items. Bounty pace. Solo. The boss's DPS is shown as sheet / realistic (Lancer, long band). The boss hull shown
is the Lancer; the Drake is x0.921. Salvage is rewards-only / with 60 s in the hub per mission. Gear affordable is the
level on each of 3 lines under the new ladder and the gate (g_ref is the reference's 65%).

| class | L | pilot hull | pilot DPS | boss hull | boss DPS | **TTK** | **TTD** Lancer; Drake | hull lost in the fight (realistic) | salvage by then | gear affordable (cap) | g_ref |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **DESTROYER** (median) B 395 D 54 N 3 | 1 | 454 | 63.7 | 3823 | 17.1 / 6.7 | **60.0** | **30.7; 41.5** | 58% | 941 / 2,635 | 0.6 / 1.0 (1) | 0.65 |
| | 5 | 654 | 83.4 | 5005 | 24.6 / 9.6 | 60.0 | 30.7; 41.5 | 58% | 5,241 / 20,136 | 3.1 / 5.0 (5) | 3.25 |
| | 10 | 743 | 101.2 | 6072 | 27.9 / 11.0 | 60.0 | 30.7; 41.5 | 58% | 15,816 / 53,211 | 7.5 / 10.0 (10) | 6.5 |
| | 20 | 957 | 147.6 | 8853 | 35.9 / 14.1 | 60.0 | 30.7; 41.5 | 58% | 55,385 / 145,205 | 16.2 / 20.0 (20) | 13 |
| | 30 | 1160 | 187.4 | 11246 | 43.5 / 17.1 | 60.0 | 30.7; 41.5 | 58% | 95,430 / 243,387 | 20.9 / 29.9 (30) | 19.5 |
| | 40 | 1286 | 215.2 | 12913 | 48.3 / 19.0 | 60.0 | 30.7; 41.5 | 58% | 135,808 / 345,899 | 24.2 / 33.4 (40) | 26 |
| **ECHO** (fastest) B 180 D 72 N 6 | 1 | 234 | 95.8 | 3823 | 17.1 / 6.7 | **39.9** | **14.7; 19.4** | 94% | same | same | |
| | 5 | 335 | 129.6 | 5005 | 24.6 / 9.6 | 38.6 | 14.7; 19.3 | 92% | | | |
| | 10 | 383 | 156.5 | 6072 | 27.9 / 11.0 | 38.8 | 14.7; 19.4 | 92% | | | |
| | 20 | 491 | 226.3 | 8853 | 35.9 / 14.1 | 39.1 | 14.7; 19.3 | 93% | | | |
| | 30 | 605 | 286.3 | 11246 | 43.5 / 17.1 | 39.3 | 14.9; 19.7 | 92% | | | |
| | 40 | 675 | 327.6 | 12913 | 48.3 / 19.0 | 39.4 | 15.0; 19.8 | 91% | | | |
| **SNIPER** (slowest) B 240 D 43 N 5 | 1 | 300 | 55.0 | 3823 | 17.1 / 6.7 | **69.5** | **19.3; 25.6** | 120% | same | same | |
| | 5 | 429 | 73.7 | 5005 | 24.6 / 9.6 | 67.9 | 19.1; 25.4 | 119% | | | |
| | 10 | 488 | 89.2 | 6072 | 27.9 / 11.0 | 68.1 | 19.2; 25.4 | 119% | | | |
| | 20 | 625 | 129.2 | 8853 | 35.9 / 14.1 | 68.5 | 19.1; 25.3 | 120% | | | |
| | 30 | 764 | 163.7 | 11246 | 43.5 / 17.1 | 68.7 | 19.2; 25.5 | 119% | | | |
| | 40 | 850 | 187.5 | 12913 | 48.3 / 19.0 | 68.9 | 19.3; 25.6 | 119% | | | |

Salvage and gear affordable do not depend on class. With capped items the median's boss hull is 3823, 5060, 5681, 7253,
8565 and 9474, its sheet DPS 17.1, 21.9, 24.1, 29.2, 34.1 and 37.1, and TTK and TTD are the same 60 s and 30.7 s.

**How to read "hull lost".** It uses one stated land rate (39% of the Lancer's sheet, long band) for every class. The
Sniper at 2500 u and the lights' agility should land less, so the 90-120% figures are a warning about the **kits'** hull
against today's **level-1** move damage (§7.3), not about the curve. The curve's promise is that the figure is the same
at L40 as at L1.

### 3.1 Every class, constant across levels
TTK s / TTD s against the Lancer's sheet:

| class | if damage parts lift **all** damage: L1 / L20 / L40 | if they lift **only the primary** (today's weapon slot): L40 |
|---|---|---|
| BATTLESHIP | 70/41 · 70/40 · 70/40 | **107**/40 |
| CARRIER | 65/33 · 65/33 · 65/33 | **102**/33 |
| DESTROYER | 60/31 · 60/31 · 60/31 | 73/31 |
| FREIGHTER | 58/38 · 58/36 · 58/36 | 91/36 |
| TENDER | 68/31 · 67/30 · 67/30 | 76/30 |
| BASTION | 66/35 · 66/34 · 66/33 | **104**/33 |
| SNIPER | 69/19 · 68/19 · 69/19 | 98/19 |
| WARRIOR | 60/25 · 59/24 · 59/24 | 68/24 |
| WARDEN | 61/22 · 60/22 · 60/22 | 79/22 |
| DART | 47/17 · 46/16 · 47/17 | 61/17 |
| ECHO | 40/15 · 39/15 · 39/15 | 44/15 |
| WRAITH | 45/18 · 44/18 · 44/18 | 49/18 |

### 3.2 Below and above the reference (median class)
TTK s / TTD s:

| pilot | L5 | L10 | L20 | L40 |
|---|---|---|---|---|
| **reference** (65% of the gate, 1 hull part) | 60/31 | 60/31 | 60/31 | 60/31 |
| skips salvage (g = 0) | 64/29 | 68/28 | **80/24** | **103/19** |
| no hull part (a Glass or empty shield slot) | 60/21 | 60/18 | 60/14 | 60/**11** |
| stock (kit only, rarity 1, no levels) | 77/21 | 91/18 | 126/14 | **176/11** |
| rewards only (60-85% of the gate) | 60/31 | 59/31 | 57/32 | 62/30 |
| + 60 s in the hub per mission | 58/32 | 56/32 | 53/35 | 54/34 |
| 100% of the gate | 58/32 | 56/32 | 53/35 | 49/38 |
| 2 hull parts (+ Braced frame) | 60/39 | 60/41 | 60/45 | 60/48 |
| **100% of the gate + 2 hull parts** | 58/40 | 56/44 | **53/53** | **49/63** |

- **Skipping salvage is felt from L10** and hurts by L20.
- **The gate caps the upside.** An idler is 10-20% faster, not 3x.
- **Salvage-levelled hull is survival:** at L40, levels account for 1286 − 838 = 448 of the reference's hull.

---

## 4 · LEVEL SKIPPING

**Yes, as the pilot's choice. No, as a ladder change. The cadence stays one boss per level.** Add
`Missions.SkipAhead = 2`, so the TIO's ▶ stops at `Unlocked + 2` (the highest cleared + 3).

**Why a pilot may skip.** Par grows 1-4% a level. The L-reference against the boss k levels up (TTK s / TTD s):

| L | +1 | +2 | +3 | +5 | +10 |
|---|---|---|---|---|---|
| 10 | 64/30 | 66/29 | **68/28** | 73/26 | 87/23 |
| 20 | 62/29 | 64/29 | **66/28** | 72/26 | 76/25 |
| 30 | 61/30 | 61/30 | **62/30** | 65/29 | 69/27 |

- **+3 is a stretch, not a wall.** It is the reward for being above par. A pilot at 100% of the gate (53-56 s) takes +3
  at about 60 s.
- **The system balances itself:**
  - Kill EXP of 300·L/PL pays more above your level.
  - The gear gate reads the highest cleared level, so a skipper's gear cap moves with them.
  - Skipped levels stay uncleared, and their first-clear bonus is still there to go back for. No save change is needed.

**Why no rungs (a boss every k levels):**
- **It is a relabelling.** Per fight nothing changes.
- **It breaks `ForLevel`.** For an even k, every rung is the same boss.
- **It starves the reference.** The reference's salvage is per clear (crates), so a rung of k gives 1/k of the gear per
  level and needs rewards x k.
- **Thresholds arrive early.** Loot's thresholds, escorts and Quicken, all counted in levels, would arrive k times sooner.

---

## 5 · WHAT CHANGES

### 5.1 Constants and tables, old → new

| # | where | old | new | rung that proves it |
|---|---|---|---|---|
| 1 | `Missions.LevelStep`, `Missions.S(L)` (Missions.cs:181-182) | 1.025^(L−1) | **deleted**; `Par.HullScale(L)` and `Par.DamageScale(L)` (new `Par.cs`, header naming what it replaced) | 3 |
| 2 | `Missions.HullMult` / `DamageMult` (:221-222) | S(L) · party | HullScale(L) · party / DamageScale(L) · party | 3 |
| 3 | `Missions.BountyEach` (:226) | S(L) | HullScale(L) | 3; 5 for the guest's share |
| 4 | `Missions.Bosses[].Hull` | Lancer 760, Drake 700 | **3820, 3520** | 3 |
| 5 | `MissionKind.Pay` (siege) | 1 | **2** | 3 |
| 6 | `MissionKind.Crates` | none (a siege drops 1x) | **new share: bounty 1, siege 2**, used for the host's roll count | 3 |
| 7 | `Emplacements.All` base `Hull` (Emplacements.cs:65) | `l => 2 * ForLevel(l).Hull` | `_ => 3820` | 3 |
| 8 | `Emplacements.All` pylon `Hull` (:76) | `_ => 400` | `_ => 480` | 3 |
| 9 | `Emplacements` base `Gun` (:69) | 4 mounts x 18 every 2.2 s (32.7 sheet) | about 8.4 sheet, e.g. `Guns = 2`, `Interval = 4.3` (recommended) | 3 |
| 10 | `Raider.Strength` meaning, and its callers `Boss.cs:539, :541`, `Waves.cs:180, 237, 244, 255, 263`, `Waves.ThreatStrength` (:129), `Waves.Standing` (:148) | a multiplier: `Missions.S(level)`, `LevelStep^(threat−1)`, `Log(ratio)/Log(LevelStep)` | **a level**: hull x HullScale(level), damage x DamageScale(level); `Strength = b => b.Level`; ThreatStrength deleted, since the threat IS a level; Standing = `Par.LevelOfHull(ratio)`. The same double goes on the wire with a new meaning, so the fingerprint changes | **5** |
| 11 | `TioWindow.cs:64` | `×{Missions.S(lv)}` | `×{Par.HullScale(lv)}` | 4 (text) |
| 12 | `Equipment.LevelCost` / `LevelGrowth` (Equipment.cs:438) | 100 / 1.25 | **500 / 1.10** | 3 |
| 13 | gear-level gate (new: `Equipment.Cap`, checked in `Yard.BuyGearLevel` and the EquipmentWindow button) | none | `min(MaxLevel, highest cleared on either ladder + 1)`, at purchase; saves keep what they bought | 3 |
| 14 | `Equipment.LevelOf` key (:439), `Character` `[gear_level]` load (Character.cs:314) | part id (`sh_bulwark_2`) | **line** (`sh_bulwark`); load migrates by taking the max over `_1/_2/_3` | 3 |
| 15 | gear levels on the wire: `Hub.SendIdentity` / `NetIdentity` (Hub.cs:762-768), `PlayerShip.BuildSheet` → `Equipment.Bonuses` → `LevelOf` | **not sent**: the host lifts a guest's parts by the host's own levels | the identity carries the worn lines' levels, clamped to MaxLevel and to the claimed level; `Bonuses` takes a level source | **5** |
| 16 | `Missions.SkipAhead` (new), `TioWindow.cs:62, :66` (`top`, `_up`), `Hub.cs:1018, :1030` (the SelectLevel clamps) | ▶ stops at Unlocked | Unlocked + 2 | 3 |
| 17 | `Progression.cs:54` (Gunnery) and `:82` (Cooling) | −0.005 on an inverse stat, so the ship gets **worse** | +0.005 | 3 |
| 18 | `Yard.TakeStock` (the hauler) | loads the deepest pile, salvage included | never loads salvage, so savings for a 5-20k level are not sold at 1 credit | 3 |
| 19 | stale text: Missions.cs header (S(L), "200 x boss/pilot"), :163 ("10%"), Waves.cs:121, :174, Raider.cs:48 ("1.1^(L-1)"), `Economy.ScrapValue` comment ("a common is one early level") | | rewrite to Par / "a fifth of the first level" | 1 |

**Unchanged:** the EXP rules, `ForLevel`, the party multipliers, `Quicken` (see §7.6), `CratesFor`, `RollRarity`,
`ScrapValue`, `MaxLevel` 40 and `LevelStep` 0.05.

### 5.2 Smoke checks that assert a moved value (`tools/smoketest/SmokeTest.cs.txt`)

| lines | asserts today | becomes |
|---|---|---|
| **3636-3639** | S(1)=1, S(5)=1.025^4, LevelStep=1.025, S(41)=2.685 | **the one literal proof of Par**: HullScale / DamageScale at L = 1, 10, 20, 40 against Appendix A's literals, plus the properties below |
| 3640 | party x2.2 / x1.4 at L1 | unchanged (every scale is 1 at L1) |
| 3647-3650 | `BountyEach(Siege,1,1) == 2000` | **4000** |
| 5399-5403 | Lancer 760 / Drake 700 rows | 3820 / 3520 |
| 5483 | `boss.MaxHp == 760` at L1 | 3820 |
| 5551 | escort hull ≤ 3 x S(Level) | 3 x HullScale(Level) |
| 5825 | S(2) = LevelStep, S(3) = LevelStep² | HullScale(3) > HullScale(2) > 1 (monotone), read from Par |
| 5850 | Drake MaxHp = 700 x S(2) | 3520 x HullScale(2) |
| 5883-5884, 5924-5926, 5978 | Drake gun 6, scrap 18.75, rock 250 x S(2) | x DamageScale(2) |
| 6000-6007 | raid Strength = LevelStep^(L−1), MaxHull = hull x sL | Strength == failed level; MaxHull = hull x HullScale(level) |
| 6058-6062 | base = 2 x bossAt3; pylons = 400 x S(3) | base = 3820 x HullScale(3); pylons = 480 x HullScale(3) |
| 6073-6075 | siege wave Strength = LevelStep² | Strength == 3; MaxHull = hull x HullScale(3) |
| 6104-6106 | base drops `CratesFor(3)` | 2 x CratesFor(3) |
| 2148, 2171, 4174 | escort hunters MaxHull = 0.5 x hull x Strength | 0.5 x hull x HullScale(Strength) |
| 2155-2163 | ThreatStrength(4.75) = LevelStep^3.75; toughness via Log(LevelStep); hunters' Strength = LevelStep^(threat−1) | Strength == threat; toughness via Par.LevelOfHull |
| 1363-1371 | ladder 100 / 125 / 156, whole 3,008,864 | **500 / 550 / 605, whole 221,296**, plus the gate refusing a level at the cap |
| 1390-1396 | "a level belongs to the part id": `LevelOf("bs_rapid_2") == 0` | inverted: the line's level shows on `_2`; plus a migration check (a file with `_1` = 7 and `_2` = 3 loads as 7) |
| 402 | save fixture `GearLevel["bs_rapid_1"]=7`, `["cv_elite_3"]=40` | line keys |
| 5830, 6123 | ▶ disabled past the newest unlocked; `SelectLevel(99)` clamps to the top | past newest + 2 |
| 4492-4500 | Cooling / Gunnery per-point literal −0.005 | +0.005, and measure an interval and a cooldown **shortening** after a purchase |
| **6710-6716** (two-player guest) | 1537.5 = 1500 x 1.025 | 1500 x HullScale(2) — **rung 5** |
| 6425, 6462, 6651, 7182 | party L1 figures | unchanged |

**New checks, in the same edits:**
- At rung 3 unless noted.
- **TTK band.** Par's median pilot at a varied L in [1, 60]: `Bosses[0].Hull · HullScale(L) / Par.Dps(L)` lies in
  [55, 65] s after it is converted to realistic DPS once. Better: the Fly() DealtBy probe for the median class at L = 1
  lands 60 ± 6 s.
- **TTD band.** `Par.Hull(L) / (sheet · DamageScale(L) − 0.005 · Par.Hull(L))` is within ±5% of its L1 value at a
  varied L.
- **Plateau.** `HullScale(80) / HullScale(40) ≤ 1.1`.
- **Siege parity.** Pay, Crates and Exp of the siege are each 2x the bounty's.
- **The gate.** A level is refused at `highest + 1`.
- **A guest's gear level.** It lifts the guest's hull **on the host** (rung 5).
- **Escort raiders.** At L40 an escort has hull x HullScale(40) and deals damage x DamageScale(40) on host and guest
  (rung 5).

---

## 6 · WHAT THE ITEM PASS MUST DELIVER (hull first)

The curve reads the item table through Par, so it stays true for any numbers. What the table must deliver is set by the
owner's goals, not by the curve:

1. **Damage parts must lift a class's whole boss damage.** The Weapon part lifts the primary and the Utility part the
   abilities, each with a levelled up of similar size. If only the primary lifts (today), the TTK spread at L40 widens
   from 39-70 s to **44-107 s** (§3.1): the Battleship, Carrier and Bastion fall to 100+ s, Echo stays at 44 s.
   **Needed:** at L20 and L40, every class's best Weapon+Utility pair gives a net DPS lift within ±10% of the median
   class's.
2. **Every class needs a hull line in the Shield slot, drawbacks on mobility only.** The curve assumes **one** hull part
   per pilot. At the capped +25% common (x2 at the top rarity):
   - Salvage levels give **+65% of base hull at L40, 28% of the reference's hull**. A no-salvage pilot's TTD is 21 s
     against 31 s.
   - That is visible, but the minimum. For salvage to be **a third** of the pilot's hull, the top-rarity hull line needs
     **+72%** (a +36% common). That breaks the +25% cap.
   - **Two ways to keep the cap and reach a third:** (a) a steeper rarity scale for hull ups (x1 / x2 / x3), or (b) a
     hull up on the kit Shield (`basic_deflector` +10%, levellable, about +23% at g = 26). (b) also gives a pilot with no
     drop a way to turn salvage into hull from L1.
3. **Keep a second hull source as the "tank" choice.** Braced or Armoured frame, or Armour chips. Par does not count it.
   Two hull parts are TTD **+30-55%** at L10-40 (§3.2). Its price must stay on mobility, not on hull or damage.
4. **Chip stacks level as one line.** Levels are per id, and soon per line. Six Armour chips on a light level together:
   +8% x 2 x 2.3 x 6 = **+221% hull** at L40 for the cost of one line. Either chips do not take `Ups`, or Par has to
   count a chip stack. Decision 3 (at most 3 Combat chips) bounds damage, not hull.
5. **Rarity.** Par's ramp (ρ 1.0 at L5 → 2.0 at L25) assumes the top tier is x2 and is worn by about L25. A new tier or
   scale changes the ramp row, not code.
6. **No hull or damage drawback on the reference lines.** Today's Bulwark costs speed and the Rapid costs 30% damage.
   Par reads the rows, so this is a warning, not a blocker: a reference line whose drawback eats its own axis flattens
   the boss curve with it.

---

## 7 · RISKS (where this is most likely wrong without flying it)

1. **The anchor rests on a guessed realistic DPS.** The median class's landed DPS is the v2 draft's estimate (54 bare).
   TTK scales 1:1 with the error.
   - Mitigation: the DealtBy probe plus Fly() at rung 3 for the median class at L1. Set `Bosses[].Hull` from the
     measurement, not from this table.
   - The Drake's warps (the throw warps back to 1300 u, the scrap shot to 600 u) cut uptime, so 3520 may fly longer than
     55 s.
2. **Hub time is the biggest unknown in salvage.**
   - A pilot who idles 60 s in the hub per mission is at the gate (100%) from L1 to L30, so fights run 53-56 s. The gate
     is what stops it being 30 s.
   - If most pilots idle, "par" is effectively 100% of the gate. Move `Par.share` from 0.65 to about 0.85 and every boss
     follows.
3. **Lights and the Sniper against today's level-1 damage.**
   - Constant across levels, but not comfortable: TTD 15-19 s, and at the stated land rates they lose 90-120% of hull in
     their fight.
   - The Lancer's undodgeable bolt (3.0 of its 17.05 sheet) alone is about 50% of an Echo's hull over a 40 s fight.
   - This is the kits' hull and the moves' damage, not the curve. Options: halve the bolt or make it dodgeable, or scale
     the L1 damage anchor to x0.8 (median TTD 38 / 52 s).
4. **Drops are random, and Par is the median.**
   - A pilot with no hull shield by L5 is at TTD 21 s instead of 31 s.
   - One with no epic by L25 is 10-15% below Par until it drops.
   - Levels keyed by line soften this. The skip-ahead helps the lucky, not the unlucky. Consider a Loot "pity" floor if
     flying shows it.
5. **The class spread depends on §6.1.** If the item pass leaves Utility parts unable to lift abilities, low-f_p classes
   (Battleship, Carrier, Bastion, Freighter) drift to 90-107 s at L40, whatever the boss curve is.
6. **Past L40 the curve plateaus, but `Quicken` (1.01 a level) does not.** At L80, wind-ups are ÷2.2 and land rates rise,
   so realistic damage creeps up while the sheet stays flat. Cap Quicken at L40 too, or accept it as the endgame's teeth.
   Raising `MaxLevel` later extends Par automatically.
7. **Raiders' Strength changes meaning** (a multiplier becomes a level). Every caller moves in one edit, and it is a
   rung-5 change: host and guest must agree on hull.
   - The identity carrying gear levels is also a wire change.
   - Today a guest's salvage is invisible to the host that resolves its hull. **This is the largest hidden gap between
     "salvage gives HP" and what a party actually gets.**
8. **Par's own assumptions:**
   - 4 clears a level sets points, but points are only 7-10% of power.
   - The parts ramp (owned by L5) is conservative: about half of pilots have a hull shield after the first level's 8
     crates.
   - The rarity ramp is for "some hull line", not the best line.
   Each is one row. Changing it moves every boss by a few percent.
9. **Existing saves.** Levels bought on the old ladder stay, because the gate applies only at purchase. The cost change
   is not refunded. Line-keyed levels take the max over rarities, so no save loses power.

---

## Appendix A · Par per level, today's items (HullScale, DamageScale)

| L | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| H | 1.000 | 1.086 | 1.194 | 1.259 | 1.309 | 1.396 | 1.442 | 1.489 | 1.538 | 1.588 |
| D | 1.000 | 1.106 | 1.206 | 1.328 | 1.440 | 1.473 | 1.507 | 1.542 | 1.598 | 1.636 |

| L | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 |
|---|---|---|---|---|---|---|---|---|---|---|
| H | 1.692 | 1.748 | 1.804 | 1.863 | 1.923 | 1.984 | 2.111 | 2.177 | 2.246 | 2.316 |
| D | 1.675 | 1.715 | 1.757 | 1.822 | 1.866 | 1.912 | 1.958 | 2.006 | 2.055 | 2.106 |

| L | 21 | 22 | 23 | 24 | 25 | 26 | 27 | 28 | 29 | 30 |
|---|---|---|---|---|---|---|---|---|---|---|
| H | 2.387 | 2.461 | 2.536 | 2.612 | 2.771 | 2.805 | 2.839 | 2.874 | 2.908 | 2.942 |
| D | 2.183 | 2.237 | 2.291 | 2.347 | 2.404 | 2.428 | 2.452 | 2.476 | 2.530 | 2.554 |

| L | 31 | 32 | 33 | 34 | 35 | 36 | 37 | 38 | 39 | 40 |
|---|---|---|---|---|---|---|---|---|---|---|
| H | 2.976 | 3.010 | 3.044 | 3.167 | 3.202 | 3.237 | 3.273 | 3.308 | 3.343 | 3.378 |
| D | 2.579 | 2.603 | 2.627 | 2.652 | 2.676 | 2.700 | 2.724 | 2.781 | 2.806 | 2.830 |

The small steps (L11, 17, 25, 34) are Weapons points landing. Capped items: `python curve.py capped`.

## Appendix B · Where each number comes from

**Re-read in source for this document:**

| number | where |
|---|---|
| `LevelStep` 1.025, `S`, `HullMult` / `DamageMult` party terms, `BountyBase` 2000, `KillExp` 300 / 250 / 100, `ExpFloorShare` 0.5, siege `Exp = 2` and `Pay` default 1, `ForLevel` parity | Missions.cs |
| Lancer 760, Drake 700 and every move row | Missions.cs, Lancer.cs, Drake.cs (Drake as in the working tree) |
| base `2 * ForLevel(l).Hull`, guns 4 x 18 / 2.2 s; pylon 400, gun 9 / 2.6 s; both x `HullMult` in `Raise` | Emplacements.cs |
| `CratesFor` 2 / 3 / 4; rarity 55 / 30 / 15 from L11 | Loot.cs |
| `ScrapValue` 100 / 250 / 600 | Economy.cs |
| ladder 100 x 1.25^n, 40 levels, +5% of `Ups`, levels per id, rarity scale 1 / 1.5 / 2 on ups only; Bulwark 0.40, Braced 0.30, Rapid 0.60 / −0.30, `chip_basic` +5% / +5% | Equipment.cs |
| Weapons +3%, Hull +5 flat, cost n, Cooling / Gunnery −0.005 on inverse stats | Progression.cs |
| `(Base+Flat) x (1+ΣBonus)`, inverse divides | Stats.cs |
| `AwayShare` 1/20 | Yard.cs:170 |
| regen 0.5% / s in combat | PlayerShip.cs:55 |
| gear levels absent from `NetIdentity`; `BuildSheet` → `Bonuses` → `LevelOf` → local `Character.GearLevel` | Hub.cs:762-768, PlayerShip.cs:289-296, Equipment.cs:439-445 |
| every smoke line in §5.2 | SmokeTest.cs.txt |

**Taken from the other agents, not re-derived:**

| number | source |
|---|---|
| fleet income (trip distances, salvager buying) | player_model.py |
| realistic land rates (Lancer 6.70 long band, Drake 3.91) and sheets (17.05 / 13.23) | boss_model.md §1.3; I checked the sheet arithmetic against the move rows. The beam may land 5 ticks, not 4, which would make the sheet 18.7 |
| class B / D / N / f_p | v1 signoff and kits2_* drafts |

## Appendix C · Files
- `curve.py`: the model. Arguments: item set, LevelCost, LevelGrowth.
- `player_model.py`: income, imported.
- This file.
