# NUMBERS v2: the curve on base classes, the raids re-run, and the items by hull category

## Owner summary

1. **Scope: a new table.** `Par.cs` is the reference row: the DESTROYER base class with no chips, behind the level walls (abilities at L1/L3/L6). It wears 4 power lines and draws its tiers from Loot's drop row. **L1 anchors: Lancer 3456, Drake 3183** (was 3820/3520, which counted 3 kit chips). Boss hull ×2.22 and damage ×3.13 at L40, then flat (×2.34 / ×3.26 at L80).
2. **Par kills in 60.0 s at every level** (the Drake 55.3 s, the kept 700/760 row ratio). **Time to die holds at 31.0 s against the Lancer and 42.0 s against the Drake at every level.** To get there without chips, every boss row except the two supers is cut: Lancer ×0.744 (guns 3.6→2.68, trident 15→11.2, wave 45→33.5, ram 40→29.8), Drake ×0.787 (gun 6→4.72, scrap 18.75→14.76). The burn (250, 50 a tick) and the rock (250) are unchanged: your supers ruling names those two.
3. **Chips are upside.** Every open slot filled (3 Combat + 3 Armour, no chip ladder) kills ×1.10 faster at L3, ×1.19 at L10 and ×1.24 at L40 (48 s). From L10 it survives ×1.14-1.18 longer (TTD 36-37 s).
4. **Raids keep pace by construction.** Raiders take a level, so at every level an add dies in the same time (webifier 0.62 s, gunship 2.5 s) and deals the same share of hull. The beam's windup is max(6 s / 1.01^(L-1), strip time + 0.6 s), with the strip priced on the slowest class (par's strip ×1.2: the BB and TE fly at 84% of par). The squad-1 floor binds from L23 at 4.82 s. A web that lands mid-windup stretches it (to 5.8-7.5 s). **No full beam lands on any class at any level**, except once on the Carrier at L1, behind its walls.
5. **Raids, with adds against the boss alone:** x1.06 (Rusty L1-5) rising to x1.39 (Rusty L39) and x2.01 (Drake L40). Fights run 4-39% longer, pinned at most 20% of the time, and the adds pay +3-24% EXP.
6. **Items: 42 lines** (capital 11, freighter 10, heavy 11, light 10) **plus 6 generic chips**. Tiers go ×1.10 each: a +25% T1 is +59% at T10. Rate, cooldown and duration lines start at +20%, which puts them at ×0.98-0.99 of a damage line (at most ×1.12 with full chips). The two rider lines (Magazine Core, Swarm Rack) add a count to the full lean, so on a boss they are ×1.06-1.17 of the reference line (D11). Scrap is 100×1.25^(t-1).
7. **L40:** gear is 68% of Par's hull (parts 38%, salvage 30%) and 46% of its damage. With no salvage Par takes 75 s to kill and dies in 21 s. The best possible build (T10, 100% of the gate, full chips) is ×1.36-1.68 of the class's own par: 42 s for the DD, 41 s for the Warden and Carrier on their riders, 35 s at the fastest (Echo). **No build reaches half of par**, as long as gear top speed never prices the Dart's damage (D6). If it does, a Dart on Engine chips reaches about 29 s.
8. **Your calls (defaults stand):** D1 cut every row but the burn and the rock (not everything ×0.86/0.92); D2 no chip ladder; D3 no boss-hull trim for adds; D4 the escape floor priced on the slowest class. Priced on par instead, the 7 classes slower than par take a full beam on a quarter to a half of the Rusty's beams. §6 has all eleven.
9. **The parallel kits draft (kits v3.1) disagrees** on the anchor (3091 there, 3456 here), the size of the cut, three class rows and the chip budget. §8 lists them; the two must agree before either is built.

Read-only design. Nothing in the repo was edited, built or run. The numbers are from `numbers_v2.py` in this folder (`python numbers_v2.py [all|curve|classes|chips|raids|items]`; the full output is `run.txt`). It imports `../curve.py`, `../player_model.py` and `../raids_v2_model.py` and changes nothing in them. **Three rows here differ from the script as it stands** (the ram is not a super, the raider missile is 42, the escape floor is priced on the slowest class). §7 has the three edits that make it print this file's §1.2 and §2.

---

## 0 · WHAT THIS RESTS ON

**The rulings this model applies.**
- Base classes are the reference, with no chips.
- The walls: abilities at 1/3/6; chip slots at 2/4/8/10/12/14, 6 on every hull, at most 3 combat and 3 utility; walls read the highest level reached.
- Kit chips are not baked into base stats.
- Heavies' twin laser deals 2.58, and heavies never web.
- The Rusty Bucket's escorts become squad wave 1 from L1; any web starts the beam; the 0.6 s escape floor, which this file holds for every class (D4).
- Adds from L6 go H, L, L, L every 3 levels, up to 3H+9L; a wiped squad refills after 30 s and a refill pays no EXP.
- Supers stay at 250/250: the Lancer's burn and the Drake's rock. The code also flags the ram (Lancer.cs:64) and the scrap shotgun (Drake.cs:42) `Super`; neither is 250, and both take the cut like every other row.
- The DD's rip deals 1% of the target's total hull + 10.
- Freighter F is Time on target, hitscan.
- Items are split by hull category: 8-12 lines each, 10 tiers at +10% compounding, and saves are disregarded.
- Salvage levels live on the slot, per pilot.
- Raiders take a level.

**Rulings that change no number here.**
- Warp is capital-only, and the non-capital V becomes a boost: mobility, with no boss DPS except through the Dart's speed-priced guns (D6, risk 4).
- Capitals are slower overall: the kits put the freighters' top speed above every capital's (kits v3.1's default is 120 u/s; today 85). The freighter lines in §3.3 are written on that.
- Respawn is 24 s: solo, a death still fails the mission.
- Sentries prefer the paint and otherwise shoot anything hostile, bosses and heavies included (8fddb84's `Targeting.Sentry` is kept). This file proposes that as the resolution of kits v3 decision 14. Solo, the spotter paints the boss anyway.
- The Supercarrier engages "anything": vs a boss, nothing changes.
- The Warden's Taunt guard is not hull, so TTD does not see it.

**Class inputs** (realistic DPS against a lone boss, stock, no chips). They come from the kits v3 power table (`kits3/signoff_v3.md` §4), with two changes:
- **Freighter 56.7**: Time on target goes hitscan (10.0 sheet × 0.9 painted × 0.97 landed = 8.7 realistic, against v2's 7.0), plus 1.5 from thrown sentries.
- **DD rip**: 1% of the boss's total hull + 10 per cast-off, one every 21 s, used 80% of the time. That is 2.3% of a boss per fight. It opens at PL6 with the Grapnel.
- **The Dart is v3's 66.8**, which does not price the V boost. D6's default does price it (the kit's boost reaches the Pepperbox and the rod). With it the Dart is 72.0 (kits v3.1: 71.9): 47 s at par instead of 51, and 35 s at its ceiling instead of 37. Nothing else in this file moves.

Each class splits its realistic DPS into weapon + abilities 1-3 in learn order (the `CLASSES` rows).

**The reference.**
- It is still the DESTROYER (curve.md's median row): realistic 55.0 and hull 395.
- The fleet's median realistic is 51.8. A median-DPS class kills in 63.7 s at L1 and **65.7 s from L10**, once the boss counts the DD's rip (decision D7).

**Boss rows.**
- Lancer per unit of scale: guns 3.0, trident 3.0, wave 2.65, **beam 8.33** (5 ticks = 250 after fix B), ram 1.33. The escorts' 0.4 has left the Lancer's sheet: they are now squad wave 1.
- Drake: gun 2.4, scrap 2.5, rock 8.33.
- Land rates: boss_model.md §1.3.

---

## 1 · THE CURVE

### 1.1 Par v3: the reference pilot, as rows

| row | value |
|---|---|
| class | DESTROYER base: hull 395, weapon 40.15 + Lance 14.2 + Suppress 0 + Grapnel 0.65 (+ rip) realistic |
| chips | **none** (the ruling). The model's chipped pilot is §1.5 |
| walls | abilities at PL 1/3/6. The DD learns F Lance, Q Suppress, E Grapnel (the rip arrives with the Grapnel at PL6) |
| pace | 4 clears a level. The first fill of the adds pays EXP (+3-24%). PL entering boss L1/5/10/20/40 = 1/7/14/30/62 |
| points | Weapons (+3% all damage) and Hull (+5 flat), the cheaper first: 7 and 7 at L40 |
| gear | 4 power lines: Weapon (primary damage), Utility (ability output), Shield (hull), Hull frame (hull), all +25% at T1 ×1.10 a tier. The worn tier comes from a drop sim of Loot's row (§3.2): it owns a hull part 78% of the time at L5 and 96% at L8, and wears T5.1 at L20 and T9.9 at L40 |
| salvage | g = 0.65 × min(40, L) on each worn slot (the gate is L at boss L). Each level is +3% of the part's ups |
| versus | Par reads the reference's DPS **by target tag**: against a boss it includes the rip, against craft it does not. Bosses use `HullScale`, raiders `CraftScale` |

Formulas, per fight k of level L, averaged over the level's 4 fights:
```
H_par   = (395 + 5h) · (1 + 0.25·G·(P_Sh + P_Fr))                       G = 1 + 0.03 g,  P = E[1.1^(t-1)] worn
D_craft = weapon·(1 + 0.03w + 0.25·G·P_W) + Σ open abilities·(1 + 0.03w + 0.25·G·P_U)
BossHull_Lancer(L) = 60 · D_boss / (1 − 60 r)                            r = 0.8 · 1% / 21 s once the Grapnel is open
HullScale = BossHull(L)/BossHull(1) · CraftScale = D_craft(L)/D_craft(1) · DamageScale = H_par(L)/H_par(1)
```

### 1.2 The L1 anchors

- **Hull:** Lancer **3456** = 60 s × 57.6, which is par's average DPS over L1's four fights (54.35 walled on the first fight, 60.7 by the fourth, as its first parts and point arrive). Drake **3183** (× 700/760).
  - The approved 3820 is 1.105 × this. The difference is the three kit chips and the Weapons point that curve.md counted.
- **Damage (the time-to-die anchor):** par's L1 hull averages 423 (395 on the first fight, 449 by the fourth). Holding 31.0 / 42.0 s needs a sheet of 15.76 (Lancer) and 12.19 (Drake).
  - The two supers stay at their rows, so every other row carries the cut.

| boss | row | today | new | |
|---|---|---|---|---|
| Lancer | guns (bolt) | 3.6 every 1.2 s | **2.68** | ×0.744 on every row but the burn |
| | trident (each) | 15 | **11.2** | |
| | shockwave | 45 | **33.5** | |
| | ram | 40 | **29.8** | flagged `Super` in code (Lancer.cs:64), but not one of the ruling's 250s |
| | beam tick / burn | 50 / 250 | 50 / 250 | a super: unchanged |
| Drake | main gun | 6 | **4.72** | ×0.787 on every row but the rock |
| | scrap (each) | 18.75 | **14.76** | flagged `Super` in code (Drake.cs:42), cut like the ram |
| | rock | 250 | 250 | a super: unchanged |

- New sheets: Lancer 15.76, Drake 12.19.
- New realistic rates: Lancer 5.68 (long band) / 6.67 (short band), Drake 3.52 (were 6.70 / 8.02 / 3.91).
- The supers are now 53% of the Lancer's sheet and 68% of the Drake's, so a pilot who dodges the telegraphs takes a lot less.
- The alternatives are D1 in §6.

### 1.3 The scale, level by level

Lancer and Drake are the absolute hulls. TTK is par's kill time, averaged over the level's 4 fights. TTD is against the sheet ("stops dodging").

| L | PL | worn E[P] W/U/Sh/Fr | g | HullScale | DamageScale | CraftScale | Lancer | Drake | par TTK L / D | TTD L / D |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1-2 | 0 / 0 / 0 / 0 | 0.7 | 1.000 | 1.000 | 1.000 | **3456** | **3183** | 60.1 / 55.4 | 31.0 / 42.0 |
| 2 | 2-3 | .37 .56 .35 .34 | 1.3 | 1.098 | 1.151 | 1.098 | 3794 | 3494 | 60.0 / 55.3 | 31.0 / 42.0 |
| 3 | 4-5 | .58 .82 .53 .58 | 2.0 | 1.158 | 1.259 | 1.158 | 4004 | 3688 | 60.0 / 55.3 | 31.0 / 42.0 |
| 4 | 5-6 | .70 .94 .71 .77 | 2.6 | 1.242 | 1.334 | 1.214 | 4294 | 3955 | 60.0 / 55.3 | 31.0 / 42.0 |
| 5 | 7-8 | .79 1.00 .79 .81 | 3.2 | 1.282 | 1.403 | 1.244 | 4430 | 4081 | 60.0 / 55.3 | 31.0 / 42.0 |
| 6 | 8-9 | .88 1.07 .91 .92 | 3.9 | 1.310 | 1.463 | 1.271 | 4529 | 4172 | 60.0 / 55.3 | 31.0 / 42.0 |
| 7 | 10-11 | 1.01 1.11 1.00 1.00 | 4.5 | 1.366 | 1.510 | 1.325 | 4721 | 4348 | 60.0 / 55.3 | 31.0 / 42.0 |
| 8 | 11-12 | 1.06 1.13 1.05 1.04 | 5.2 | 1.382 | 1.543 | 1.341 | 4777 | 4400 | 60.0 | 31.0 / 42.0 |
| 9 | 13-14 | 1.10 1.14 1.08 1.08 | 5.9 | 1.400 | 1.595 | 1.358 | 4838 | 4456 | 60.0 | 31.0 / 42.0 |
| 10 | 14-15 | 1.15 1.20 1.14 1.13 | 6.5 | 1.418 | 1.636 | 1.376 | 4901 | 4514 | 60.0 | 31.0 / 42.0 |
| 11 | 16-17 | | 7.2 | 1.447 | 1.667 | 1.404 | 5000 | 4605 | 60.0 | 31.0 / 42.0 |
| 12 | 17-18 | | 7.8 | 1.473 | 1.695 | 1.429 | 5091 | 4689 | 60.0 | 31.0 / 42.0 |
| 13 | 19-20 | | 8.5 | 1.490 | 1.727 | 1.446 | 5151 | 4744 | 60.0 | 31.0 / 42.0 |
| 14 | 20-22 | | 9.1 | 1.512 | 1.785 | 1.467 | 5226 | 4813 | 60.0 | 31.0 / 42.0 |
| 15 | 22-23 | 1.31 1.36 1.31 1.32 | 9.8 | 1.527 | 1.823 | 1.482 | 5278 | 4861 | 60.0 | 31.0 / 42.0 |
| 16 | 23-25 | | 10.4 | 1.540 | 1.849 | 1.495 | 5324 | 4904 | 60.0 | 31.0 / 42.0 |
| 17 | 25-26 | | 11.1 | 1.581 | 1.883 | 1.534 | 5464 | 5032 | 60.0 | 31.0 / 42.0 |
| 18 | 27-28 | | 11.7 | 1.612 | 1.930 | 1.564 | 5572 | 5132 | 60.0 | 31.0 / 42.0 |
| 19 | 28-29 | | 12.3 | 1.628 | 1.965 | 1.580 | 5627 | 5182 | 60.0 | 31.0 / 42.0 |
| 20 | 30-31 | 1.47 1.52 1.48 1.47 | 13.0 | 1.642 | 2.004 | 1.593 | 5674 | 5226 | 60.0 | 31.0 / 42.0 |
| 21 | 31-33 | | 13.7 | 1.665 | 2.057 | 1.615 | 5753 | 5299 | 60.0 | 31.0 / 42.0 |
| 22 | 33-34 | | 14.3 | 1.694 | 2.112 | 1.644 | 5855 | 5392 | 60.0 | 31.0 / 42.0 |
| 23 | 34-36 | | 15.0 | 1.711 | 2.151 | 1.660 | 5914 | 5447 | 60.0 | 31.0 / 42.0 |
| 24 | 36-37 | | 15.6 | 1.747 | 2.184 | 1.696 | 6039 | 5563 | 60.0 | 31.0 / 42.0 |
| 25 | 38-39 | 1.65 1.67 1.64 1.65 | 16.2 | 1.778 | 2.229 | 1.726 | 6146 | 5661 | 60.0 | 31.0 / 42.0 |
| 26 | 39-41 | | 16.9 | 1.808 | 2.291 | 1.755 | 6250 | 5756 | 60.0 | 31.0 / 42.0 |
| 27 | 41-42 | | 17.6 | 1.830 | 2.335 | 1.776 | 6326 | 5827 | 60.0 | 31.0 / 42.0 |
| 28 | 42-44 | | 18.2 | 1.847 | 2.391 | 1.793 | 6385 | 5881 | 60.0 | 31.0 / 42.0 |
| 29 | 44-45 | | 18.9 | 1.874 | 2.453 | 1.818 | 6477 | 5965 | 60.0 | 31.0 / 42.0 |
| 30 | 46-47 | 1.88 1.95 1.89 1.90 | 19.5 | 1.909 | 2.529 | 1.853 | 6599 | 6078 | 60.0 | 31.0 / 42.0 |
| 31 | 47-49 | | 20.2 | 1.935 | 2.581 | 1.877 | 6686 | 6158 | 60.0 | 31.0 / 42.0 |
| 32 | 49-50 | | 20.8 | 1.977 | 2.621 | 1.918 | 6833 | 6293 | 60.0 | 31.0 / 42.0 |
| 33 | 51-52 | | 21.4 | 2.015 | 2.680 | 1.955 | 6963 | 6414 | 60.0 | 31.0 / 42.0 |
| 34 | 52-54 | | 22.1 | 2.056 | 2.759 | 1.995 | 7106 | 6545 | 60.0 | 31.0 / 42.0 |
| 35 | 54-55 | 2.13 2.18 2.12 2.14 | 22.8 | 2.083 | 2.816 | 2.021 | 7198 | 6630 | 60.0 | 31.0 / 42.0 |
| 36 | 56-57 | | 23.4 | 2.106 | 2.880 | 2.044 | 7279 | 6704 | 60.0 | 31.0 / 42.0 |
| 37 | 57-59 | | 24.1 | 2.137 | 2.959 | 2.074 | 7387 | 6803 | 60.0 | 31.0 / 42.0 |
| 38 | 59-60 | | 24.7 | 2.177 | 3.035 | 2.112 | 7524 | 6930 | 60.0 | 31.0 / 42.0 |
| 39 | 61-62 | | 25.4 | 2.200 | 3.090 | 2.135 | 7603 | 7002 | 60.0 | 31.0 / 42.0 |
| **40** | 62-64 | 2.33 2.36 2.34 2.33 | 26.0 | **2.216** | **3.131** | **2.151** | **7661** | **7056** | 60.0 | 31.0 / 42.0 |
| 45 | 71-72 | 2.36 (T10) | 26.0 | 2.252 | 3.149 | 2.186 | 7785 | 7170 | 60.0 | 31.0 / 42.0 |
| 50 | 79-80 | | 26.0 | 2.252 | 3.186 | 2.186 | 7785 | 7170 | 60.0 | 31.0 / 42.0 |
| 60 | 96-97 | | 26.0 | 2.282 | 3.222 | 2.214 | 7887 | 7264 | 60.0 | 31.0 / 42.0 |
| 80 | 129-130 | | 26.0 | 2.341 | 3.259 | 2.272 | 8091 | 7452 | 60.0 | 31.0 / 42.0 |

- **Shape.** Growth is fast while the first parts arrive: +9.8% hull and +15% damage from L1 to L2. It settles to about 1.6% and 2.3% a level from L5 to L40. It is flat past L40, because tiers stop at T10 (L37) and the gate at g26: HullScale(80) / HullScale(40) = 1.056.
- **Damage outgrows hull** (×3.13 against ×2.22 at L40). Par wears two hull slots against one damage lift per system; that is items_design's F1 (the frame counts), kept.
- **Bosses against raiders.** `HullScale` runs above `CraftScale` by 2.3% from L4, because the rip hits bosses and never craft.
- **Within a level** the 4 fights spread because walls and drops land mid-level:

  | L | fight 1-4 TTK | fight 1-4 TTD (Lancer) |
  |---|---|---|
  | 1 | 63.6 / 61.7 / 58.2 / 57.0 | 28.6 / 30.4 / 31.8 / 33.2 |
  | 2 | 61.3 / 60.3 / 59.6 / 58.8 | 29.4 / 30.7 / 31.5 / 32.4 |
  | 4 | 62.4 / 59.5 / 59.2 / 59.0 | 30.6 / 30.9 / 31.1 / 31.4 |
  | 10 | 60.1 / 60.0 / 60.0 / 59.9 | 30.8 / 30.9 / 31.1 / 31.2 |
  | 40 | 60.0 all four | 31.0 all four |

- **The walls, at this pace:**
  - Ability 2 opens at boss L2 fight 2, and ability 3 at L4 fight 2.
  - Chip slots 1-6 open at L1 fight 3, L3 fight 1, L5 fight 3, L7 fight 1, L8 fight 2 and L9 fight 3.
  - This matches `level_gates.md` to within a fight: the adds' EXP moves chips 3 and 6 one fight earlier.

### 1.4 Every class against the par boss (no chips, its own category's drops)

TTK s / TTD s (Lancer sheet):

| class | realistic | L1 | L2 | L3 | L4 | L6 | L10 | L20 | L40 |
|---|---|---|---|---|---|---|---|---|---|
| BATTLESHIP | 46.0 | 70/41 | 69/41 | 70/40 | 72/40 | 73/40 | 74/41 | 74/40 | 74/40 |
| CARRIER | 52.6 | **85**/34 | **72**/34 | 69/34 | 65/34 | 64/33 | 64/34 | 64/34 | 65/34 |
| **DESTROYER** | 55.0 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 |
| FREIGHTER | 56.7 | 57/36 | 56/36 | 55/36 | 58/36 | 59/36 | 60/36 | 60/36 | 60/36 |
| TENDER | 46.0 | 73/29 | 71/29 | 71/29 | 72/30 | 73/30 | 74/30 | 73/30 | 74/30 |
| BASTION | 47.0 | 69/33 | 69/33 | 69/33 | 71/33 | 72/33 | 72/33 | 72/33 | 72/33 |
| WARRIOR | 50.0 | 70/22 | 69/22 | 69/22 | 67/23 | 67/23 | 68/23 | 68/23 | 68/23 |
| SNIPER | 51.0 | 63/18 | 62/18 | 62/18 | 64/18 | 65/18 | 66/18 | 66/18 | 67/19 |
| WARDEN | 49.0 | 66/20 | 65/20 | 64/20 | 67/20 | 68/20 | 69/21 | 69/21 | 69/21 |
| DART | 66.8 | 54/15 | 51/15 | 49/15 | 51/15 | 51/15 | 51/15 | 51/15 | 51/16 |
| ECHO | 72.0 | 46/13 | 46/13 | 46/13 | 47/13 | 47/13 | 47/14 | 47/14 | 47/14 |
| WRAITH | 64.0 | 56/16 | 53/16 | 52/16 | 53/16 | 53/17 | 53/17 | 53/17 | 53/17 |

- **Every class holds its own time within ±2 s from L4 to L40.** The spread (47-74 s) is the kits' ×1.57 power spread, not the curve.
- **Before L4 the walls move it.** The Carrier is the one class they hurt: 85 s at L1, because its ability shares are walled until L3 and L6 (as in `level_gates.md`). The Warrior is at 70 s against its usual 68.
- **Every other class is 3-6% slower from L6 than at L1:** BB 70 → 74, Sniper 63 → 67. The rip is 2.3% of it: a boss sized by Par counts the DD's rip once the Grapnel opens (L4, fight 2). The rest is the DD's own L1 (its walled Grapnel and first parts), which the L1 anchor averages.
- **Lights and heavies lost their kit-chip hull.** They die in 13-23 s against the Lancer's sheet (curve.md had 15-25 s). Chips give it back (§1.5).

### 1.5 The same pilot with every open chip slot filled

The order is Combat, Combat, Combat, Armour, Armour, Armour, in slot order. A kit Basic chip (+5% damage, +5% hull) sits in a slot until a found chip beats it. Chip tiers come from the drop sim. There is no chip ladder (D2).

| L | chips open | TTK par → chipped | ×DPS | TTD par → chipped | ×hull | with a chip ladder (g = 0.65 L) |
|---|---|---|---|---|---|---|
| 1 | 0-1 | 60.1 → 58.9 | ×1.02 | 31.0 → 31.8 | ×1.02 | 58.9 |
| 2 | 1 | 60.0 → 57.3 | ×1.05 | 31.0 → 31.4 | ×1.01 | 57.2 |
| 3 | 2 | 60.0 → 54.7 | ×1.10 | 31.0 → 32.4 | ×1.04 | 54.5 |
| 5 | 2-3 | 60.0 → 53.0 | ×1.13 | 31.0 | ×1.00 | 52.4 |
| 6 | 3 | 60.0 → 51.5 | ×1.17 | 31.0 | ×1.00 | 50.6 |
| 8 | 4-5 | 60.0 → 50.9 | ×1.18 | 31.0 → 34.0 | ×1.08 | 49.7 |
| 10 | 6 | 60.0 → 50.5 | ×1.19 | 31.0 → 36.2 | ×1.14 | 49.0 |
| 20 | 6 | 60.0 → 49.6 | ×1.21 | 31.0 → 37.2 | ×1.17 | 46.5 |
| 30 | 6 | 60.0 → 48.8 | ×1.23 | 31.0 → 37.4 | ×1.17 | 44.0 |
| **40** | 6 | 60.0 → **48.2** | **×1.24** | 31.0 → **37.5** | ×1.18 | 41.8 |
| 60 | 6 | 60.0 → 48.4 | ×1.24 | 31.0 → 37.7 | ×1.18 | 42.1 |

- **The upside.** A pilot who fills its chips is 20-24% faster from L10, and lives 14-18% longer. A 3 Combat + 3 Armour loadout is the strongest; any other mix trades one for the other.
- **The level walls hand out chips across L1-L9.** A new pilot's first 36 fights ramp the upside from ×1.02 to ×1.19.
- **A chip ladder** (one ladder for the 6 chip slots) takes the L40 chipped pilot to ×1.44 (41.8 s) and keeps growing with salvage. Without one, chips grow only by tier and hold at ×1.24. Hence D2.

### 1.6 Level skipping and the siege

The level-L par against the boss k levels up, TTK s / TTD s (Lancer):

| L | +1 | +2 | +3 |
|---|---|---|---|
| 5 | 61.3 / 29.6 | 63.8 / 28.5 | 64.6 / 27.8 |
| 10 | 61.2 / 30.3 | 62.3 / 29.8 | 63.0 / 29.1 |
| 20 | 60.8 / 30.1 | 61.9 / 29.2 | 62.5 / 28.6 |
| 40 | 60.7 / 30.9 | 60.9 / 30.9 | 60.9 / 30.8 |

- Skipping +2 (curve.md's approved TIO limit) costs 1-4 s. A chipped pilot takes +3 inside 55 s from L10 (53 s); at L5 it takes 57 s.
- **The siege follows the Lancer row, not the boss hull.** The base is 1.0× that row and a pylon 0.125×, so **3456 / 432** at L1 (curve.md had 3820 / 480), each × HullScale(L).
- **The siege guns are cut** (the approved curve default, in the working tree, uncommitted): the base's damage is `prep/siege_missile.md`'s cruise missile, 126 = the curve's 8.4 a second over 15 s, × DamageScale. That sheet (8.4 + 5.23 from the waves = 13.6) is below the new Lancer's 15.76, so par lives about 37 s in a siege rather than 31. Re-literal the 126 here if the siege should match the Lancer.
- **A home raid wave** (3 pinners + 1 standoff, 229.7 row hull) takes **5.7 s of par fire to clear at any level**: raiders take a level. See §2.4.

---

## 2 · RAIDS: raids_v2 re-run on the rulings

**What changed in the model** (`fight()` in numbers_v2.py, on raids_v2_model's squad geometry and enemy rows):
- **Scaling.** Add hull × `CraftScale(L)` and every damage × `DamageScale(L)`. The par pilot deals `Par.Dps(L, craft)` to adds and `Par.Dps(L, boss)` to the boss. The raider missile stays flat at its row's **42** (`EnemyDef.MissileDamage`, Enemies.cs:50), thrown unscaled as Raider.cs:339-344 does today. raids_v2_model's 35 is stale.
- **Heavy laser:** 2.58 (2 barrels at 1.29). Heavies never web.
- **The Rusty Bucket's squad wave 1 from L1:** at least 2 webifiers, at row hull, spawned at t = 0, formed up for 10 s, engaged about 13 s in. From L6 the schedule fills it: [G+2W] until L14, then [G+3W].
  - The beam row's own escorts, `EscortStep`, `EscortMax` and `EscortsAt` are deleted.
  - `EscortHull 3` goes with them: the escorts were 3 hull; webifiers are 25.
- **The beam, modelled explicitly.** It arms every 30 s from 6 s.
  - It charges on the first web by **any** add, or 5 s after arming with no web (the old `ArmMax`).
  - It lands its full 250 × DamageScale if the pilot is still pinned 0.6 s before the first tick. A free pilot takes 25% of it (the stated line-dodge rate).
- **The pilot strips the web first**: every pinner, in slot order, then the heavies, then the boss.

### 2.1 The escape floor

```
windup = max(6 s / 1.01^(L-1),  2 s react + 1.2 × (hull of every pinner on you) / (0.7 × Par.Dps(L, craft)) + 0.6 s)
         applied at charge start AND again whenever a new pinner latches during the wind-up (the live floor)
```

- **The 1.2 is the ruling's 0.6 s for every class** (D4). It is 55/46: the strip priced on the slowest classes, the BB and the TE, which fly at 84% of par's DPS. Priced on par (1.0), par keeps exactly 0.6 s and every class slower than par keeps less, down to 0.24 s. Because the first beam is always the live-floor case (below), the 7 classes slower than par then take a **full** beam on 25-30 of the Rusty's ~55 beams across L1-39, from L3-5 on (the Carrier on 15 of 54, from L1).
- Raiders take a level, so the hull / DPS ratio does not move, and **the strip time is the same number at every level**:

  | pinners | par's strip | floor | par's margin | the slowest class's margin |
  |---|---|---|---|---|
  | 2 webifiers (Rusty squad 1 to L14) | 3.24 s | **4.08 s** | 0.84 s | 0.60 s |
  | 3 webifiers (squad 1 from L15) | 3.86 s | **4.82 s** | 0.96 s | 0.60 s |
  | 3 talons (squad 2) | 3.34 s | 4.20 s | 0.86 s | 0.60 s |
  | 3 pods (squad 3 at L39) | 6.46 s | 7.94 s | 1.47 s | 0.61 s |
  | all 9 lights at once (L39's worst) | 9.66 s | 11.76 s | 2.10 s | 0.62 s |

- **Per level** (Rusty levels). "Flown" is what the fight sim's wind-ups actually were. The margins are over the squad-1 strip:

  | L | squads | 6/Q(L) | squad-1 floor | windup | par's margin | slowest class's margin | wind-ups flown | full beams on par |
  |---|---|---|---|---|---|---|---|---|
  | 1 | [W W] | 6.00 | 4.08 | 6.00 | 2.76 | 2.52 | 6.03, 6.00 | 0 |
  | 5 | [W W] | 5.77 | 4.08 | 5.77 | 2.53 | 2.29 | 6.03, 5.77 | 0 |
  | 9 | [G W W] | 5.54 | 4.08 | 5.54 | 2.30 | 2.06 | 6.03, 5.54 | 0 |
  | 15 | [G+3W] | 5.22 | 4.82 | 5.22 | 1.36 | 1.00 | **6.77**, 5.22 | 0 |
  | 21 | [G+3W][X+T] | 4.92 | 4.82 | 4.92 | 1.06 | 0.70 | 6.77, 5.78, 4.92 | 0 |
  | **23** | same | 4.82 | 4.82 | **4.82** | 0.96 | **0.60** | 6.77, 5.78, 4.82 | 0 |
  | 27 | [G+3W][X+3T] | 4.63 | 4.82 | 4.82 | 0.96 | 0.60 | 6.77, 6.85, 4.63 | 0 |
  | 31 | + [K] | 4.45 | 4.82 | 4.82 | 0.96 | 0.60 | 6.77, 4.45, 4.45 | 0 |
  | 35 | + [K+P] | 4.28 | 4.82 | 4.82 | 0.96 | 0.60 | 6.77, 4.28, 4.28 | 0 |
  | 39 | + [K+3P] | 4.11 | 4.82 | 4.82 | 0.96 | 0.60 | 6.77, 4.20, **7.53** | 0 |
  | 51 | same | 3.65 | 4.82 | 4.82 | 0.96 | 0.60 | 6.77, 4.20, 7.53 | 0 |

- **The squad-1 floor binds from L23.** Raids_v2's R1 margin, 0.1 s at L39, becomes 0.6 s for the slowest class and 0.96 s for par.
- A flown windup under the floor (4.45 at L31, 4.20 at L39) is a beam that charged with nobody pinned: the floor has nothing to strip, and the pilot is free to leave the line.
- **Every class, flown:** run at each class's own DPS against this floor, no class takes a full beam at any Rusty level, except the Carrier once at L1 (its walls leave it 70% of par there).
- **The live floor is not optional.** The first beam arms at 6 s, gives up waiting at 11 s and charges unpinned, and then squad 1 latches at about 13 s, mid-windup. Priced only at charge start, that beam landed in full on par at every Rusty level from L15. The live floor stretches it to 6.77 s and it misses. At L37-39 the pods arriving mid-windup stretch the third beam to 7.5 s.
- The Lancer's `First = 6` could instead move to 16 s, so the first beam arms after squad 1 is in. That is not taken: the live floor already covers every late web, and the wait was never a rule.

### 2.2 What the adds do to a boss fight (solo, par pilot)

x boss = all DPS taken / the boss alone. Fight +% is measured against the boss alone (Rusty 60 s, Drake 55.3 s). EXP +% is the first fill's EXP against a repeat clear at par PL.

| L | boss | squads | x boss | pinned | fight +% | adds' own DPS / DamageScale | full beams / fired | EXP +% |
|---|---|---|---|---|---|---|---|---|
| 1-5 | RUSTY | [W W] | **x1.06** | 10% | +4% | 0.19 | 0/6 | +3% |
| 1-5 | DRAKE | none | x1.00 | 0% | 0 | 0 | | 0 |
| 6-14 | RUSTY | [G W W] | x1.11 | 9% | +12% | 0.66 | 0/8 | +7% |
| 6-8 | DRAKE | [G] | x1.06 | 0% | +5% | 0.20 | | +4% |
| 9-11 | DRAKE | [G W] | x1.20 | 4% | +6% | 0.33 | | +5% |
| 12-14 | DRAKE | [G W W] | x1.25 | 5% | +7% | 0.41 | | +7% |
| 15-17 | RUSTY / DRAKE | [G+3W] | x1.13 / x1.30 | 11% / 6% | +14% / +8% | 0.80 / 0.49 | 0/4 | +8% |
| 18-20 | R / D | + [X] | x1.27 / x1.39 | 11% / 7% | +20% / +14% | 0.95 / 0.74 | 0/3 | +12% |
| 21-23 | R / D | + [X+T] | x1.29 / x1.53 | 14% / 12% | +21% / +15% | 1.05 / 0.84 | 0/6 | +13% |
| 24-26 | R / D | + [X+2T] | x1.30 / x1.59 | 14% / 13% | +21% / +15% | 1.10 / 0.95 | 0/3 | +15% |
| 27-29 | R / D | + [X+3T] | x1.31 / x1.74 | 15% / 16% | +22% / +24% | 1.17 / 1.21 | 0/6 | +16% |
| 30-32 | R / D | + [K] | x1.33 / x1.82 | 14% / 15% | +28% / +31% | 1.45 / 1.57 | 0/3 | +20% |
| 33-35 | R / D | + [K+P] | x1.35 / x1.94 | 16% / 19% | +31% / +33% | 1.58 / 1.62 | 0/6 | +21% |
| 36-38 | R / D | + [K+2P] | x1.37 / x1.95 | 18% / 19% | +33% / +36% | 1.70 / 1.68 | 0/3 | +22% |
| **39-40** | R / D | **3H+9L** | **x1.39 / x2.01** | 19% / 20% | **+36% / +39%** | 1.83 / 1.80 | 0/3 | +24% |

- **Against kits v3's table** (Rusty x1.51, Drake x2.07 at L39-40): the Rusty drops, because the floor means par never eats a full beam; the Drake is about the same.
- **The Rusty's L1-5 levels now have adds:** the two escort webifiers are a real squad, x1.06.
- **The web is still the danger, not the guns.** Pinned 0-20%. The adds' own guns reach 1.8 per unit of DamageScale at L39.
- **Fight length.** Par's L39 Rusty fight is 81 s and the L40 Drake 77 s. A chipped pilot (×1.24) flies them in about 65 s. The boss hull is not trimmed for adds (D3).
- **Ignore the adds** (the worst case): x2.51 at L1, x3.05 at L9, x4.58 at L20, x3.58 at L39, pinned 78%. Such a pilot eats full beams: 2 at L1, L9 and L39. The floor protects a pilot who strips, not one who ignores the web.

### 2.3 Time to die, dodging (realistic), boss alone → with adds

Par gear on each class's base hull; no chips.

| L (fight) | Echo | Warden | Destroyer | Battleship |
|---|---|---|---|---|
| 1 RUSTY (63 s) | 192: 34 → 31 s | 288: 55 → 51 | 423: 93 → 86 | 535: 134 → 122 |
| 6 DRAKE (58 s) | 291: 79 → 73 | 426: 141 → 129 | 619: 301 → 264 | 774: 607 → 494 |
| 9 RUSTY (67 s) | 322: 36 → **32** | 470: 57 → **50** | 675: 93 → 80 | 847: 132 → 113 |
| 20 DRAKE (63 s) | 411: 82 → **53** | 595: 146 → 87 | 848: 301 → 153 | 1062: 609 → 237 |
| 30 DRAKE (72 s) | 528: 84 → **39** | 754: 147 → **61** | 1070: 301 → 99 | 1334: 597 → 140 |
| 39 RUSTY (81 s) | 655: 38 → **26** | 926: 58 → **39** | 1308: 93 → **59** | 1626: 130 → 79 |
| 40 DRAKE (77 s) | 663: 86 → **35** | 939: 148 → **54** | 1325: 301 → 85 | 1647: 592 → 118 |

- **Bold** means it dies before the fight ends if it never mitigates.
- **Solo, that is a failed mission** (the respawn ruling).
- **Lights against the Rusty** are short at every level, with or without adds: the Echo lives 26-38 s dodging in a 60-81 s fight. This is the kits' light hull against an undodgeable bolt, cut here by 26% (risk 2).
- **The capitals are safe** until L39.

### 2.4 Keeping pace

Raiders take a level, from the same source as bosses.

| L | par s to kill: webifier / gunship / lancerkin | a webifier's DPS, % of par hull per s | a gunship laser, % per s | old multiplier 1.025^(L-1) against CraftScale |
|---|---|---|---|---|
| 1 | 0.62 / 2.48 / 3.72 | 0.236 | 0.610 | 1.00 against 1.00 |
| 10 | 0.62 / 2.48 / 3.72 | 0.236 | 0.610 | 1.25 against 1.38 (raiders were 10% soft) |
| 20 | same | same | same | 1.60 against 1.59 |
| 40 | same | same | same | 2.62 against 2.15 (22% hard) |
| 60 | same | same | same | 4.29 against 2.21 (94% hard: the runaway) |

- The add fights, the home raids (a wave clears in 5.7 s of par fire) and the siege garrison stay the same size relative to par at every level.
- **What grows is the count** (the roster) and the boss's `Quicken`, not the ratio.
- **Scope:** `Raider.Strength` becomes a level, as curve.md §5.1 row 10 says. The new detail is that raiders read `Par.Scale(L, craft)`, not the boss's `HullScale`.

---

## 3 · ITEMS: by hull category, +10% a tier

### 3.1 The law (`Tiers` row)

| t | P(t) | power lean (T1 +25%) | multiplier lean: rate, cooldown, duration (T1 +20%) | reach / area (T1 18%) | top / accel (T1 22%) | chip (T1 8%) | +n on a count of 4 / 6+ | price | scrap | first drops | main band |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1.000 | +25.0% | +20.0% | +18.0% | +22.0% | +8.0% | +1 / +1 | fixed | 100 | L1 | L1-4 |
| 2 | 1.100 | +27.5% | +22.0% | +19.8% | +24.2% | +8.8% | +1 / +1 | fixed | 125 | L1 | L5-8 |
| 3 | 1.210 | +30.3% | +24.2% | +21.8% | +26.6% | +9.7% | +1 / +1 | fixed | 156 | L5 | L9-12 |
| 4 | 1.331 | +33.3% | +26.6% | +24.0% | +29.3% | +10.6% | +1 / +1 | fixed | 195 | L9 | L13-16 |
| 5 | 1.464 | +36.6% | +29.3% | +26.4% | +32.2% | +11.7% | +1 / +1 | fixed | 244 | L13 | L17-20 |
| 6 | 1.611 | +40.3% | +32.2% | +29.0% | +35.4% | +12.9% | +1 / **+2** | fixed | 305 | L17 | L21-24 |
| 7 | 1.772 | +44.3% | +35.4% | +31.9% | +39.0% | +14.2% | +1 / +2 | fixed | 381 | L21 | L25-28 |
| 8 | 1.949 | +48.7% | +39.0% | +35.1% | +42.9% | +15.6% | +1 / +2 | fixed | 477 | L25 | L29-32 |
| 9 | 2.144 | +53.6% | +42.9% | +38.6% | +47.2% | +17.1% | **+2** / +2 | fixed | 596 | L29 | L33-36 |
| 10 | 2.358 | +58.9% | +47.2% | +42.4% | +51.9% | +18.9% | +2 / +2 | fixed | 745 | L33 | L37+ |

**The rules every line obeys.** They are rows. The generator asserts only the line counts, the four reference axes, what each class can wear and exact-duplicate (slot, lean) text. The rest below were checked by reading.
- **One growth law.** Every headline is ×1.10 a tier. Salvage lifts every up in the slot by (1 + 0.03 g), never the price. T10 at g26 is ×4.20 the T1 headline; at g40, ×5.19.
- **The T1 caps are the owner's.**
  - One up is at most **+25%** (smaller for geometry: 18% reach/area, 22% top/accel).
  - A price is at most **−50%**. Every price here is "−15% of power", weighted by the stat: damage or hull −15%, top −24%, turn/range/area −28%, tracking/guidance/accel −33%, astern −42%. **A price never grows with the tier.**
  - A count moves **±1**.
- **Multiplier leans start at +20%, not +25%.**
  - Rate, cooldown and duration multiply every damage share under them: points, damage parts and chips.
  - At +20% they sit at **×0.98-0.99 of a damage line** on Par's sheet at every tier, and at most **×1.12** in a fully chipped loadout (§3.5).
  - At +25% they would be ×1.07 on Par and ×1.19 chipped at L40 (the Sniper's Weapon slot).
  - This replaces items_design's √-growth rule: the ruling says +10% a tier, for every line.
- **R1: a price never lands on a DPS factor of what the line lifts.** No "rate up, damage down".
- **Conditional lines are unpriced.** "Only against craft", "under 35% hull", "after a boost", "on a kill", "small hits", "while webbed" and "under 50% hull" take the full lean with no price. **Par never wears one.**
- **An unconditional line with no price is half the lean** (R7): Buffer Array, +12.5%.
- **The rider (R8, "+25% and +1").** A full power lean plus +1 on a count row of base 4 or more, paid by a second fixed price (0.5·ln((b+1)/b) of power: top −17% on base 4, −12% on base 6). The +1 becomes +2 from T6 on bases of 6+, and from T9 on base 4 (never over 25% × P(t) × base).
  - **A helm price does not slow a boss kill**, so on a boss a rider is more DPS than its category's reference line: ×1.06-1.10 of it at T1-T5 and ×1.12-1.17 at T10 (BB, CV, Warden). That lifts three ceilings in §3.5. D11 is the choice.
- **No signature duplicates.**
  - Within a category no two lines share a (slot, lean). The generator checks exact duplicates only, and all four categories pass.
  - No line grants another class's signature: no cloak, no warp off a capital, no heal, no pull, no rewind, no paint, no speed-priced damage.
  - Across categories only the 4 reference axes (a damage line and a hull line) repeat, because Par needs them in every category. Each has its own price and name.
- **What a line fits.** It fits every hull of its category, except a rider on a hull with no count of 4+. The DD cannot fit Magazine Core, and the Warrior cannot fit Swarm Rack. Every class wears at least 10 of its category's lines.

### 3.2 Drops by level (`Loot` row)

- **Crates** 2 / 3 / 4 (L1-5 / 6-10 / 11+), unchanged.
- **Each crate:** 70% from the pilot's own **hull category** (its core lines plus the 6 generic chips), 30% from anything that drops. So about 77% can be worn.
- **Tier:** base tier = 1 + ⌊(L−1)/4⌋. Roll 20% one lower, 70% on it, 10% one higher.
  - Tier t first drops at L 4t − 7 (T2 at L1, T10 at L33).
  - Tier t is the main drop over L 4t − 3 to 4t (T10 from L37).
  - The TIO reads this row, not a second copy.
- **What Par wears** (the drop sim, 300 runs, own category):

  | boss L | 2 | 3 | 5 | 8 | 12 | 16 | 20 | 24 | 28 | 32 | 36 | 40 |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|
  | base tier | T1 | T1 | T2 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | T9 | T10 |
  | owns a hull part | 35% | 53% | 78% | 96% | 100% | | | | | | | |
  | mean tier worn (Shield) | T1.1 | T1.1 | T1.2 | T2.0 | T3.0 | T4.1 | T5.1 | T6.1 | T7.1 | T8.1 | T9.1 | T9.9 |

### 3.3 The lines

"T1 → T10" is the headline at tier 1 and at tier 10 (×2.36), before salvage. **Par** marks the reference line Par wears; **=** marks a line with the same power as Par's line, which Par can wear in its place; **+** marks a rider, which is more boss DPS than Par's line (×1.06-1.17, paid in helm).

#### CAPITAL: Battleship, Carrier, Destroyer (11 lines)

The playstyles: the line of battle, standoff gunnery, the alpha strike, tempo, the fortress, the escort screen, warp assault and the broadside dance.

| # | line | slot | T1 → T10 | trade-off (fixed) | Par | why it fits |
|---|---|---|---|---|---|---|
| 1 | **Heavy Battery** | Weapon | primary damage +25% → +59% | primary tracking −33% (turrets; the CV's fighters) | **Par** | The line of battle: fewer, heavier hits from the BB's twins, the DD's director, the CV's fighters |
| 2 | Director Suite | Weapon | primary tracking +25%, shot/craft speed +25%, damage +5% → +59%, +59%, +12% | primary range −28% | | Hits what jinks: raiders, a Drake after its warp, a talon on its burn; the DD leading from its Grapnel orbit |
| 3 | Escort Hunter | Weapon | against craft only: damage +25%, tracking +25% → +59% | none (conditional) | | The raid guard: shreds the squads that web the party. Nothing on a boss |
| 4 | **Salvo Core** | Utility | every ability output +25% → +59% | ability reach/area −28% | **Par** | The alpha strike: Broadside, Bomber strike and the Long Lance hit harder, from closer |
| 5 | Magazine Core | Utility | ability output +25% **and +1**: Broadside volleys 6 → 7, torpedoes a bomber 4 → 5 → +59% and +2 (volleys from T6, torpedoes from T9) | reach/area −28%, and the rider's top −17% | + | **RIDER.** The broadside and the strike wing. BB and CV only: the Lance is a system of one |
| 6 | Tempo Core | Utility | every ability cooldown recovers +20% faster → +47% | top speed −24% | = | Tempo: more Broadsides, Lances and Supercarriers, each the same size |
| 7 | **Bulwark Belt** | Shield | hull +25% → +59% | top speed −24% | **Par** | The fortress that soaks the supers |
| 8 | Aegis Array | Shield | point-defence damage +25%, PD range +18% → +59%, +42% | acceleration −33% | | The escort screen: PD kills tridents, raider missiles and fighters over the party. Passive PD only, never the BB's CIWS burst |
| 9 | **Armoured Citadel** | Hull | hull +25% → +59% | turn rate −28% | **Par** | The second hull source; the tank's pick |
| 10 | Warp Spine | Hull | warp safe limit +18% (2400 → 2832 u; T10 3418 u), warp charge rate +25% → +59% | top speed −24% | | **Capital only** (only capitals warp): slower on the helm, further and sooner on the jump. The overshoot penalty is unchanged, counted from the new limit |
| 11 | Helm Drive | Engines | turn rate +25%, turning radius 25% tighter → +59% | top speed −24% | | The broadside dance: brings the BB's side round and keeps the DD's bow on the Lancer |

#### FREIGHTER: Freighter, Tender, Bastion (10 lines)

The playstyles: the heavy hauler, artillery, the long grind, field engineering and the convoy. They have no warp. Under the capitals-slower ruling their top speed sits above every capital's (kits v3.1's default: 120 u/s, from 85 today).

| # | line | slot | T1 → T10 | trade-off | Par | why it fits |
|---|---|---|---|---|---|---|
| 1 | **Heavy Ordnance** | Weapon | weapon damage +25% → +59%: the spotter and its sentries, the lance's harm, the mortar | primary tracking −33% | **Par** | The heavy hauler's guns |
| 2 | Long Arc | Weapon | primary range +18%, shell speed +25%, damage +5% → +42%, +59%, +12% | primary tracking −33% | | Artillery: the spotter paints from 944 u, the mortar lobs to 1298 u, the lance reaches 767 u |
| 3 | Spin-up Feed | Weapon | primary damage ramps +5% a second on one target, to +25% after 5 s (→ +59%), dropping back 1 s after the gun stops | primary range −28% | = | The long grind: the Tender's beam, the spotter holding paint, the mortar's rhythm. It reuses F1's Ramp lift (the Ramjet's), so it needs no new mechanism |
| 4 | **Field Core** | Utility | every ability output +25% → +59%: Time on target, Overdrive, the Buster, and field strength where a field deals no damage | ability reach/area −28% | **Par** | Harder convergence, overdrive and buster |
| 5 | Field Emitter | Utility | ability area +18%, ability duration +20% → +42%, +47%: Bubble, the Repair and Overdrive fields, Gravity well, Shockwave | top speed −24% | | The field engineer: bigger, longer zones for the party |
| 6 | **Cargo Plating** | Shield | hull +25% → +59% | acceleration −33% | **Par** | The armoured hauler |
| 7 | Buffer Array | Shield | hull +12.5% → +29% | none (half the lean, R7) | | The no-compromise hauler: less hull and no helm price, for a hull that already crawls |
| 8 | **Cargo Spine** | Hull | hull +25% → +59% | turn rate −28% | **Par** | The second hull source |
| 9 | Convoy Rig | Hull | V boost lasts +20% (3 → 3.6 s) and strafes +25% harder (+50% → +62.5%) → +47%, +59% | top speed −24% | | Freighters lost warp. This is how a hull with no warp steps out of a beam lane (the strafe needs the kits' strafe helm, F24) |
| 10 | Hauler Drive | Engines | top speed +22%, acceleration +22% → +52% (on kits v3.1's 120 u/s: 146 at T1, 182 at T10) | turn rate −28% | | The freighters' reach over distance with no warp: toward the heavies' 190 |

#### HEAVY: Warrior, Sniper, Warden (11 lines)

The playstyles: the duellist, the marksman, the swarm, the finisher, the stance-holder and the raid survivor.

| # | line | slot | T1 → T10 | trade-off | Par | why it fits |
|---|---|---|---|---|---|---|
| 1 | **Heavy Barrel** | Weapon | weapon damage +25% → +59%: blade, railgun, flak | primary tracking −33% (the blade's sweep) | **Par** | Hit harder |
| 2 | Rapid Action | Weapon | primary rate +20% → +47%: swings, rail charge rate (Overcharge reads it), flak bursts | primary range −28% | = | Tempo: faster swings and charges, more flak |
| 3 | Executioner | Weapon | against a target under 35% hull: damage +25% → +59% | none (conditional) | | The finisher: raiders mid-fight, a boss's last third (about +9% on a boss) |
| 4 | **Tactical Core** | Utility | every ability output +25% → +59% | ability reach/area −28% | **Par** | Harder Hunters, Anchor lines and Lunge |
| 5 | Endurance Core | Utility | every ability duration +20% → +47%: Anchor 8 → 9.6 s, Taunt 6 → 7.2 s, Prism and Whirlwind 2 → 2.4 s | top speed −24% | | Hold the stance longer: plant, taunt and spin for longer |
| 6 | Swarm Rack | Utility | ability output +25% **and +1**: Hunters 6 → 7, Flares 6 → 7 → +59% and +2 from T6 | reach/area −28%, and the rider's top −12% | + (Warden; = on the Sniper, whose Flares deal no boss damage) | **RIDER.** The Warden's swarm and the Sniper's flare ring. The Warrior has no count of 4+ |
| 7 | **Heavy Plating** | Shield | hull +25% → +59% | astern speed −42% | **Par** | Stand and trade |
| 8 | Web Breaker | Shield | a web on you holds 25% shorter and slows you 25% less → +59% (capped at the Hold ceiling, 60%) | none (conditional) | | The raid survivor. Webs are the danger in every add fight. Passive, never a class's web-break button |
| 9 | **Bulkhead Frame** | Hull | hull +25% → +59% | turn rate −28% | **Par** | The second hull source |
| 10 | Quick-Boost Frame | Hull | V boost recharges +20% faster (15 → 12.5 s) → +47% (10.2 s) | top speed −24% | | More boosts: a Warrior closing, a Sniper leaving a lane, a Warden re-posting under Taunt |
| 11 | Vector Drive | Engines | strafe thrust +25%, astern speed +25% → +59% | top speed −24% | | The side-step: circle the target with the nose on it |

#### LIGHT: Dart, Echo, Wraith (10 lines)

The playstyles: the glass cannon, hit-and-run, the raid dancer, swarm armour and the dogfighter.

| # | line | slot | T1 → T10 | trade-off | Par | why it fits |
|---|---|---|---|---|---|---|
| 1 | **Hot Barrel** | Weapon | weapon damage +25% → +59% | shot speed −33% | **Par** | Hit harder. The Pepperbox and rod caps are unchanged |
| 2 | Redline | Weapon | while your hull is under 50%: damage +25% → +59% | none (conditional) | | The glass cannon's gamble: strongest exactly when it should run |
| 3 | Burst Feed | Weapon | for 4 s after a V boost ends: primary rate +20% → +47% | none (conditional) | | Hit and run: boost in, unload, boost out (about 27% uptime) |
| 4 | **Ace Core** | Utility | every ability output +25% → +59% | ability reach/area −28% | **Par** | A harder Rod, Reverb and Venom |
| 5 | Reset Core | Utility | a kill takes 25% off every ability cooldown left → 59% | none (conditional) | | The raid dancer: chains kills into abilities. Nothing against a lone boss |
| 6 | **Reactive Plating** | Shield | hull +25% → +59% | acceleration −33% | **Par** | A light that can take a beam tick |
| 7 | Ablative Skin | Shield | hits under 10% of your hull deal 20% less → 40% (its ceiling, reached at T9 before salvage, about T5 at par's salvage) | none (conditional) | | Swarm armour: raider lasers, the Lancer's bolts and tridents. Never a super |
| 8 | **Stressed Skin** | Hull | hull +25% → +59% | turn rate −28% | **Par** | The second hull source |
| 9 | Featherweight Frame | Hull | acceleration +22%, turn rate +25% → +52%, +59% | hull −15% | | The dogfighter: gives hull for the tightest, snappiest helm |
| 10 | Burner Drive | Engines | V boost top speed +25% (+50% → +62.5%) → +59% (+79.5%) | turn rate −28% | | The straight-line burst. Gear top speed never prices the Dart's damage (D6) |

**The light category has no rider.** Only the Wraith has a count of 4+ (7 pellets). The Dart's Pepperbox is a rate, and the Echo repeats once.

#### CHIPS: generic, 6 lines

- Every hull has 6 chip slots behind the walls, with at most 3 combat and 3 utility.
- No chip ladder (D2).
- The kit is 3 Basic Combat Chips (+5% damage and +5% hull, no tier). They are not baked into base stats.

| line | kind | T1 → T10 | trade-off | model |
|---|---|---|---|---|
| Combat Chip | combat | every weapon's damage +8% → +18.9% | top speed −4% | the chipped pilot's combat chip |
| Gunner Chip | combat | primary damage +10% → +23.6% | turn rate −4% | even with Combat at a weapon share of 0.8 |
| Hunter Chip | combat | against craft only: damage +8% → +18.9% | none (conditional) | raids only. The full chip lean with no price, as every conditional line (§3.1); +16% was a double lean |
| Armour Chip | utility | hull +8% → +18.9% | turn rate −4% | the chipped pilot's utility chip |
| Engine Chip | utility | top speed +5%, turn rate +6% → +11.8%, +14.1% | none | never prices the Dart (D6) |
| Targeting Chip | utility | every reach +6% → +14.1% (the Supercarrier ring included) | none | |

- **Chips add damage or hull or helm, never rate.** A rate chip multiplies every damage line. With three rate chips (the old Overclock), the L40 ceiling was ×1.12 higher. Gunner replaces it.

### 3.4 Salvage (the ladders, per pilot)

- **5 ladders**, one per core slot (Weapon, Utility, Shield, Hull, Engines), shared by every class the pilot flies. Level n → n+1 costs 500 × 1.10^n, gated at min(40, highest boss cleared + 1) (curve.md, approved). **No chip ladder.**
- **A level is +3% of every up in the slot.** At L40, g26 makes salvage 30% of Par's hull; step 0.035 gives 33% and 0.05 gives 41%. 0.03 is the one inside curve.md's 28-35% band.
- **Scrap is 100 × 1.25^(t−1)**: 100 → 745. It grows faster than power on purpose, because scrap is what buys the ladder. A pilot on rewards alone (no idling in the hub) keeps 4 ladders at this share of the gate:

  | scrap law | L5 | L10 | L20 | L30 | L40 |
  |---|---|---|---|---|---|
  | 1.10^(t−1) (the power law) | 49% | 55% | 58% | 54% | 50% |
  | 1.20^(t−1) | 50% | 58% | 64% | 62% | 60% |
  | **1.25^(t−1)** | 50% | 60% | **68%** | **67%** | **65%** |
  | 1.28^(t−1) (items_design) | 51% | 61% | 70% | 70% | 68% |

  Par's 65% is reached by L20 (60% at L10). A pilot who idles in the hub sits above it; the gate stops anyone passing 100%.

### 3.5 Against the curve at L40

**Par's L40 hull is 1325:**
- base 30%;
- points 3%;
- parts (tier) 38%;
- **salvage (levels) 30%**.

That makes **gear 68% of the hull**: items_design had 85% at ×1.20 tiers. Gear is **46% of its damage** (weapon share: base 1, points 0.21, parts 0.58, salvage 0.46).

**Builds, DESTROYER on capital lines, against the L40 Lancer (7661 hull):**

| build | hull | TTK | TTD Lancer / Drake |
|---|---|---|---|
| **Par** (4 reference lines at worn tier, g26, no chips) | 1325 | **60.0** | **31.0 / 42.0** |
| Par + 6 chips (3 Combat + 3 Armour) | 1559 | 48.2 | 37.5 / 51.3 |
| Par, no salvage (g0) | 933 | 74.8 | 20.9 / 27.8 |
| Par, no hull lines (Shield and Hull slots on other lines) | 430 | 60.0 | **9.1** / 11.9 |
| stock (no parts, no chips) | 430 | 109.5 | 9.1 / 11.9 |
| T10 reference lines, g26, no chips | 1332 | 59.8 | 31.2 / 42.3 |
| T10 reference lines, g40 (100% of the gate), no chips | 1545 | 54.0 | 37.1 / 50.8 |
| **CEILING:** T10, g40, 3 Combat + 3 Armour | 1789 | **44.3** | 44.3 / 61.2 |
| the same with Tempo Core in Utility (a multiplier line) | 1789 | 42.3 | 44.3 / 61.2 |
| the fastest glass: T10, g40, damage lines, no hull lines, 3 Combat | 430 | 44.3 | 9.1 / 11.9 |

**Every class's ceiling** (T10, g40, full chips, its category's multiplier line where one exists, and its rider in Utility where it can fit one) against its own par time:

| class | par TTK → ceiling | × | TTD par → ceiling |
|---|---|---|---|
| BATTLESHIP | 73.9 → **46.2** (Magazine Core; 49.6 on Tempo Core) | **×1.60** | 40.1 → 58.2 |
| CARRIER | 64.5 → **41.3** (Magazine Core; 43.0 on Tempo Core) | ×1.56 | 33.5 → 48.1 |
| DESTROYER | 60.0 → 42.3 | ×1.42 | 31.0 → 44.3 |
| FREIGHTER | 59.8 → 44.0 | ×1.36 | 35.7 → 51.4 |
| TENDER | 73.8 → 54.2 | ×1.36 | 29.8 → 42.4 |
| BASTION | 72.3 → 53.0 | ×1.36 | 33.1 → 47.4 |
| WARRIOR | 67.9 → 43.7 | ×1.55 | 23.4 → 32.9 |
| SNIPER | 66.5 → 43.7 | ×1.52 | 18.8 → 26.2 |
| WARDEN | 69.2 → **41.2** (Swarm Rack; 45.7 on Tactical Core) | **×1.68** | 21.0 → 29.5 |
| DART | 51.0 → 37.3 (47.3 → 34.6 with the boost priced, D6) | ×1.37 | 15.8 → 22.0 |
| ECHO | 47.2 → **34.6** | ×1.36 | 14.4 → 19.9 |
| WRAITH | 53.2 → 39.0 | ×1.37 | 17.3 → 24.1 |

**Why no build breaks the 60 s boss:**
- Damage lives in two core slots (Weapon, Utility) and three combat chips. Shield and Hull carry hull, PD and helm, and **no Shield or Hull line adds damage** (the old Glass Array is gone).
- So the only way to trade hull for damage is to give it up. The glass build is no faster than the ceiling (44.3 s) and dies in 9 s.
- The fastest kill in the game at L40 is the Echo's 34.6 s at T10, 100% of the gate and full chips (the Dart matches it once its boost is priced). The largest gain over a class's own par is the Warden's ×1.68, from Swarm Rack's two extra Hunters.
- **This holds only under D6's default.** If gear top speed priced the Dart's guns, 3 T10 Engine chips (+20% DPS, risk 4) take the Dart to about 29 s, under half of par, and Burner Drive would take it lower.
- The heavies' and capitals' ceilings run up to 24% higher than the rest (the DD 4%, the Warden 24%). Their categories have a multiplier line (Rapid Action, Tempo Core) that multiplies with points and chips, and the BB, CV and Warden have a rider. The multiplier line is ×0.99 of a damage line on Par and ×1.08-1.12 fully chipped:

  | L, tier, g | W multiplier / damage (Sniper) | + full chips | U multiplier / damage (Battleship) | + full chips |
  |---|---|---|---|---|
  | L1, T1, g0 | ×0.98 | ×0.98 | ×0.98 | ×0.98 |
  | L10, T3, g6.5 | ×0.98 | ×1.01 | ×0.98 | ×1.01 |
  | L20, T5, g13 | ×0.98 | ×1.03 | ×0.99 | ×1.03 |
  | L30, T8, g19.5 | ×0.99 | ×1.07 | ×0.99 | ×1.05 |
  | L40, T10, g26 | ×0.99 | ×1.11 | ×0.99 | ×1.08 |
  | L40, T10, g40 | ×0.99 | ×1.12 | ×0.99 | ×1.09 |

### 3.6 What this replaces in items_design.md

| items_design | now | why |
|---|---|---|
| 20 generic lines, plus up to 10 per class after the kits (about 1,400 ids) | **42 category lines + 6 generic chips = 480 ids** (48 lines × 10 tiers) | ruling: by hull category, 8-12 each |
| compound ×1.20 (T10 +129%) | **×1.10 (T10 +59%)** | ruling |
| rate, cooldown and duration grow by √ | **every line ×1.10; multiplier leans start at +20%** | the ruling's single law, and the same balance (§3.5) |
| a chip bank per ship, kit chips baked into Base (K3) | **6 real chips, at most 3 combat and 3 utility, behind walls; not baked; not in Par** | rulings |
| 6 slot ladders | **5** (no chip ladder) | D2 |
| scrap 100·1.28^(t−1) | **100·1.25^(t−1)** | keeps 65% of the gate at the new power law |
| Loot: 70% own class | **70% own hull category** | ruling |
| Glass Array (damage from the Shield slot) | **gone**: no Shield or Hull line adds damage | the ceiling (§3.5) |
| saves: 82 heirs, the `[gear_level]` collapse and refunds, the `Was` table, the stable-id checks | **all deleted** | ruling: existing saves are disregarded, with no migrations and no refunds |
| Par: W, U, Sh, Fr plus a chip bank; L40 ×4.63 / ×7.25 | **Par: W, U, Sh, Fr, no chips; L40 ×2.22 / ×3.13** | rulings |
| gear 85% of the L40 hull | **68%** | the smaller law |
| unchanged | roles, the budget rules R1-R8, the fit rule, ids `{stem}_t{n}`, the tier colour replacing rarity, the recycler queue fix (N4), the guest's levels on the wire (N3) | |

---

## 4 · WHAT CHANGES (rows and constants, old → new)

| # | where | old | new | rung that proves it |
|---|---|---|---|---|
| 1 | `Par.cs` (new; with curve.md's `Missions.S` / `LevelStep` deletion) | — | the reference row (DESTROYER, no chips, `Unlocks` walls, 4 power lines, Loot's drop row, g = 0.65·gate), queried **by target tag**: `Par.Scale(L, boss)`, `Par.Scale(L, craft)`, `Par.DamageScale(L)` | 3 (literals from §1.3 at L1/10/20/40) |
| 2 | `Missions.Bosses[].Hull` | 760 / 700 (approved 3820 / 3520) | **3456 / 3183** | 3 |
| 3 | Lancer rows (Lancer.cs) | guns 3.6, trident 15, wave 45, ram 40 | **2.68, 11.2, 33.5, 29.8**; the beam unchanged | 3 |
| 4 | Drake rows (Drake.cs) | gun 6, scrap 18.75 | **4.72, 14.76**; rock unchanged | 3 |
| 5 | Lancer beam row | `Escorts 2`, `EscortHull 3`; `EscortStep` / `EscortMax` / `EscortsAt` (Boss.cs:552-555) | deleted: the escorts are squad wave 1, one path (raids batch `Squads.cs`) | 3 |
| 6 | the beam's charge (Boss.cs:363-371) | waits on its own escorts' pin | waits on **any** pin of its target, fallback 5 s (`ArmMax`) | 3 |
| 7 | the beam's windup (Boss.cs `Scaled`, :158-172) | `Windup / Quicken(L)` | `max(Windup / Quicken(L), React + StripShare · pinner hull / (0.7 · Par.Dps(L, craft)) + 0.6)`, re-applied when a new pinner latches; `Beam.Escape = 0.6`, `React = 2`, `StripShare = 1.2` (55/46, the slowest class) rows | 3; 5 for a guest's pin |
| 8 | `Raider.Strength` | multiplier S(L) | **a level**: hull × `Par.Scale(L, craft)`, damage × `Par.DamageScale(L)`; the missile stays its row's flat 42 | **5** |
| 9 | heavy rows (F20) | 1.25 | 2 barrels × 1.29 = **2.58** | 3 |
| 10 | `Tiers` (Items.cs, item pass) | rarity 1 / 1.5 / 2 | ×1.10 a tier, 10 tiers; power T1 25%, multiplier 20%, reach 18%, top 22%; scrap 100·1.25^(t−1) | 3 |
| 11 | `Equipment.LevelStep`, ladders | 0.05 per id | **0.03 per slot level, 5 core ladders**, 500·1.10^n, gated (D10: curve.md approved 0.05) | 3 |
| 12 | `Loot` | 70% own class; rarity bands | 70% own hull category; base tier + 20/70/10 | 3 |
| 13 | chips (F15 + walls) | 5 slots, `chip_basic` × 5 | 6 slots walled at PL 2/4/8/10/12/14, at most 3 combat and 3 utility, kit = 3 Basic | 3; 5 (the host sanitises) |

**Order.**
- The curve (1-4) lands after lane A's slice 2 (F17's `Boss.Out` carries DamageScale). Its literals are this model's, on the new tier law.
- If Par lands before the item pass, it reads the `Tiers` and `Loot` rows, which are small. Land them first, or re-run this model on today's items and re-literal at the item pass.
- 5-7 go with the raids batch (`Squads.cs`), after slice 2.
- 10-13 are the item pass, after the kits.

---

## 5 · RISKS

1. **The anchor rests on estimated DPS.** The DD's 55.0 is the v3 table's. Freighter hitscan (8.7) and the rip use (80%) are this file's. TTK scales 1:1 with the error. The rung-3 DealtBy probe on the DD at L1 replaces 3456.
2. **Lights against the Rusty.** The par Echo dodging lives 26-38 s in a 60-81 s fight, and with no chips a full burn (250) is more than every light's and the Sniper's L1 hull (180-240). Solo, a death fails the mission.
   - The cut rows help: the Lancer's undodgeable bolt is 26% smaller. So do Rewind, EMP and 3 Armour chips (+18%).
   - The lever is the burn, which you ruled unchanged.
3. **The escape floor is priced on the slowest class** (D4), so every class keeps 0.6 s and par keeps 0.96 s against squad 1. Where the floor binds, the windups run 0.2-0.9 s longer than a floor priced on par (the first beam 6.77 s against 6.41; L39's third 7.53 against 6.72).
   - Priced on par instead, the 7 classes slower than par (BB, TE, Bastion, Warden, Warrior, Sniper, Carrier) keep 0.24-0.52 s against squad 1, and the BB, TE and Bastion are 0.16-0.26 s late against 3 pods. Flown, six of them take a full beam on about half the Rusty's beams, and the Carrier on a quarter.
   - The Carrier still takes one full beam at L1, where its walls leave it 70% of par.
4. **The Dart's damage reads top speed.**
   - If gear priced it, 3 T10 Engine chips (+35% top) would add about 20% DPS through the Pepperbox, outside the combat-chip cap: the Dart's ceiling falls to about 29 s, under half of par. D6 prices it from the kit only: the Ramjet, the sprint and the V boost.
   - **D6's default is a special case.** A Pepperbox that reads the kit's speed but not the gear's needs a second, gear-blind speed read, which CLAUDE.md §3 forbids ("the generic path is the only path"). Kits v3.1 takes the generic path for the boost (the Dart at 71.9). The system fix, if D6 stays, belongs in the item pass: no line or chip a Dart can fit lifts the speed its guns read.
   - The V boost itself (+50% top for 3 s in 15) adds about 7% to the Dart if it prices the Pepperbox (72.0 against 66.8). D6's default counts it; the `CLASSES` row does not yet (§0).
5. **The rip is 2.3% of every boss from L4.** Par counts it (it is the DD's real kit), so every other class takes 2.3% longer. Raiders read the craft scale, which has no rip.
6. **Early levels are averages.** L1's first fight runs 63.6 s and its fourth 57.0 s, as parts arrive. The Carrier's L1 is 85 s (the walls). A pilot with no hull part at L5 (22% of them) dies in 20 s, not 31, until one drops (18 s at L8).
7. **Salvage early.** A pilot on rewards alone sits at 50% of the gate at L5 and 60% at L10, against Par's 65%: about 1% slower at L5-10. That is small, because early levels are worth little.
8. **Past L40 the curve is flat** (×1.056 by L80), but `Quicken` keeps shortening the windups. The squad-1 floor holds the beam at 4.82 s from L23. The ram and ring windups are not floored.
9. **Adds lengthen every fight:** +4% at L1 Rusty, +39% at L40. The 60 s promise is for the boss's own hull (D3).

---

## 6 · DECISIONS (the default stands if unanswered)

| # | fork | default | alternative |
|---|---|---|---|
| D1 | how 31 / 42 s holds without chips | **cut every row but the two supers**: Lancer ×0.744 (the ram 40 → 29.8 with the rest), Drake ×0.787; the burn (50 a tick) and the rock unchanged (the supers ruling, 250/250) | every row ×0.860 / ×0.921 (burn 215, rock 230 at L1) · keep every code-`Super` move too: Lancer ×0.705 on guns, trident and wave with the ram at 40, and the Drake's gun alone ×0.57 (6 → 3.39) with the scrap at 18.75 · or change nothing: TTD 26.1 / 38.1 s |
| D2 | chip ladder | **none**: chips grow by tier only, ×1.24 at L40 | one shared chip ladder: ×1.44 at L40 (41.8 s), more to buy with salvage |
| D3 | adds and the boss's hull | **no trim**: 60 s is the boss's own hull; adds add 4-39% and pay 3-24% EXP | trim the hull by 1/(1 + fight%) so the whole fight is 60 s at par (kits v3.1's decision 15 makes this its default) |
| D4 | the escape floor's DPS | **the slowest class** (`StripShare` 1.2 = 55/46): every class keeps 0.6 s; windups run 0.2-0.9 s longer where the floor binds | par (1.0): par keeps 0.6 s, the 7 slower classes 0.24-0.52 s, and flown they take a full beam on a quarter to a half of the Rusty's beams |
| D5 | multiplier leans | **T1 +20%, ×1.10 a tier** | T1 +25% with √ growth (items_design F2): equal at T1, ×0.87 of a damage line at L40 unchipped (×0.97 chipped) · or T1 +25% at ×1.10: ×1.07 / ×1.19 |
| D6 | what prices the Dart's damage | **its kit only** (Ramjet, sprint, V boost). Needs a gear-blind speed read, a special case under CLAUDE.md §3 (risk 4) | all top speed, gear and chips included, the generic path (+20% DPS with Engine chips; the Dart's ceiling about 29 s, under half of par) |
| D7 | the reference class | **DESTROYER** (curve.md's row; a median-DPS class takes 63.7 s at L1 and 65.7 s from L10) | anchor on the fleet's walled L1 median (50.0 against the DD's 54.35): Lancer 3179, Drake 2928; the median class then kills in about 60 s from L10 and the DD in 55 s (kits v3.1 takes this, at 3091 / 2847) |
| D8 | the loot split | **70% own hull category, 30% anything** | 100% own category (Par's parts arrive sooner and the curve re-derives from the row; a class switch gets nothing until it flies) |
| D9 | the Lancer's first beam | **arms at 6 s, and the live floor covers squad 1 latching mid-windup** | `First` 6 → 16 s, so it arms after squad 1 engages |
| D10 | the salvage step (changes an approved curve.md value) | **+3% a level** (salvage 30% of Par's L40 hull, gear 68%): curve.md's 28-35% band on the ×1.10 tier law | curve.md's approved +5% (salvage 41%, gear 73%). The 60 s holds either way, because the boss reads Par |
| D11 | the riders (Magazine Core, Swarm Rack) | **+25% and +1, as R8**: the strongest boss line in their slot (×1.06-1.17 of the reference line); BB, CV and Warden ceilings ×1.56-1.68 | +12.5% and +1 (R7's half lean): ×0.97-1.09 of the reference line at every tier; ceilings back to ×1.49-1.54 (the BB and CV return to Tempo Core) |

---

## 7 · FILES (this folder)

- `numbers_v2.py`: the model. It imports `../curve.py`, `../player_model.py` and `../raids_v2_model.py` read-only.
- `run.txt`: its full output (`python numbers_v2.py all`), from the script **before** the review below.
- `numbers_v2.md`: this file.

**The review's three row edits** (the script was not touched). With them, `numbers_v2.py` prints this file's §1.2 and §2; nothing in §1.1, §1.3-§1.6 or §3 moves:
1. `LANCER['ram'] = (40 / 30, 0.25, False)`: only the burn and the rock are supers.
2. `MISSILE = 42.0`: the row's `EnemyDef.MissileDamage` (Enemies.cs:50), not raids_v2_model's 35.
3. `STRIP_SHARE = 55 / 46`, multiplying the hull term of `strip_time()`, which `fight()` uses only for the floor. The pilot still strips at its own DPS. §2.1's per-class check runs `fight()` with the pilot's DPS at each class's own ratio to par and the floor left on par's.

The §3.5 rider rows (BB, CV, Warden) and the Dart-with-boost figures are one-off runs of `pilot()` with the counted ability's share × (base + n) / base, and the Dart's weapon and rod each +2.6.

---

## 8 · AGAINST KITS v3.1 (the parallel kits draft, `kits3/signoff_v31.md`)

Both drafts apply the same rulings and reach different rows. Each row below must be settled once, in one place, before either lands.

| row | this file | kits v3.1 | why they differ |
|---|---|---|---|
| L1 anchor | Lancer 3456 / Drake 3183 (the DD's walled L1, with its first parts) | 3091 / 2847 (the fleet's walled median 50.0 × 1.03) | D7: which class is the reference, and whether L1's first parts count |
| the cut | Lancer ×0.744, Drake ×0.787, burn and rock kept | every move but the burn and the rock ×0.64 | the same rule on different anchors and hulls |
| DD | 55.0 + the rip as a share of the boss (2.3%) | 56.6 with the rip folded in | the same rip, counted two ways |
| Freighter Time on target | 8.7 realistic | 8.0 | the paint and landing assumptions |
| Dart | 66.8 (§0: 72.0 once D6's boost is counted) | 71.9 | D6 |
| chips | T1 +8% → T10 +18.9%; the kit is 3 Basic chips | at most +10% at T10 (4.2% at T1); `chip_basic` deleted | the chip budget; both put no salvage on chip slots |
| adds and 60 s | D3: no trim | decision 15: the boss hull ÷ (1 + the adds' share) | the same fork, opposite defaults |
| freighter top | the lines here assume the kits' 120 | 120 (its decision 2) | agreed |
