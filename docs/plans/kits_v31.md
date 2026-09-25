# SIGN-OFF v3.1: 12 class kits, every ruling applied

## Owner summary

1. **Scope.** Most of this is rows on the v3 foundations. Three things are new tables: `Drives.cs` (what V does), `Lines.cs` (hitscan lines: the railgun and Time on target) and a strafe helm. The strafe helm is needed because **no ship can strafe today**, so the boost's "+50% strafe" has nothing to raise.
2. **Strafe default:** on the 9 non-capitals, hold Shift and A/D slide the ship sideways at half its top speed. The nose, the cursor and every approved card work as they do now.
3. **Power, base classes, no chips.** The median is 51.5 realistic DPS and the spread is 46-72 (1.57x). Lights stay on top: the lowest light is 64, the highest non-light is the DD at 56.6 with its 1% + 10 rip. The Dart rises to 71.9, because the boost's +50% top speed also raises the damage of its speed-priced guns.
4. **Curve, chips and adds: RECONCILED on 2026-09-25** with `numbers_curve_raids_items.md` §8, which now owns every curve, boss, raid, chip and item number. What this file proposed and what stands: the median-class anchor stands (Lancer **3222** / Drake **2968** on the numbers model, not 3091 / 2847); the burn and the rock stay 250 and every other move is cut, but by Lancer **×0.744** / Drake **×0.787**, not ×0.64; no starting chips and `chip_basic` deleted stand; the Dart at 72 stands (every speed lift prices it); chip slots take no salvage levels. Reversed: decision 15 (the boss hull is NOT trimmed for adds) and the "≤ +10% a chip" cap (the numbers file's chip rows stay within ×1.24).
7. **Warp is capitals only.** BB 88, CV 99 and DD 117 u/s in the water; with warp, 183-211 u/s over distance (today 136-161). Freighters lose warp, so by default their top speed goes 85 → **120**. That makes every non-capital faster than every capital in the water, and freighters still cover distance at 129 u/s (today 118).
8. **Freighter F is Time on target again**, now as hitscan lines (8.0 realistic), and Unmask is deleted. Sentries shoot the painted target first and anything hostile otherwise; this resolves v3 decision 14 as you ruled. The Supercarrier engages anything, missiles included (I found no reason to exclude them). The 24 s respawn is already in the working tree.
9. **Build.** 9 lanes; the kits cost about 140 S-units (v3: 133). Rung 5 runs twice and rung 6 once for the kits, then once each for items. **14 decisions are open in §10** (decision 5 is closed by your sentry ruling; 15 is new). **Say nothing and the defaults are what gets built.**

---

This file starts from `signoff_v3.md` and applies every ruling that v3 did not yet reflect. Where v3 is unchanged, this file says "v3 §x" and does not copy it.

**Design only.** Nothing in the repo was edited, built or run.
- The numbers come from `power_v31.py` in this folder (`python power_v31.py`). It is `power_v3.py`, copied and edited. It imports `../curve.py` read-only.
- The source facts were re-read in the working tree: `8fddb84` plus the uncommitted hunks, including `PlayerShip.StasisTime = 24`, `Targeting.Sentry`, the helm in `PlayerShip.Steer` and the warp block.

---

## 0 · THE RULINGS, AND WHERE EACH ONE LANDS

| ruling | what v3.1 does | § |
|---|---|---|
| 6 chip slots on every hull; at most 3 combat and at most 3 utility | Slots 3/4/5/6 are gone: every hull has 6. F15 becomes one constant plus the two kind caps | 3.6 |
| Walls: ability 1 at L1, chip 1 at L2, ability 2 at L3, chip 2 at L4, ability 3 at L6, chips 3-6 at L8/10/12/14. Keys keep their layout; walls read the highest level reached; Auto-sell and raids fold into the wall table | `Unlocks.cs` as `level_gates.md` §3, with 6 chip rows on every hull and no per-class chip count | 3.6 |
| Kit chips are NOT baked into base stats | `chip_basic` leaves the game, and `Equipment.Default` fits no chips (decision 7) | 3.6 |
| Balance on base classes; the reference pilot carries no chips; hold 60 s and ~31 / ~42 s; chips are upside; bosses and raids must not fall far behind | The power table is stock with no chips. The curve, the cut and the chips: `numbers_curve_raids_items.md` §8 (3222 / 2968; Lancer ×0.744, Drake ×0.787) | 4, 5 |
| Warp is capital-only. The other nine get V = +50% top speed and +50% strafe for 3 s, cooldown 15 s | `Drives.cs`: warp on BB/CV/DD, the boost on the nine. The strafe helm (F24) exists so the boost has a strafe to raise. Every class's interactions are redone | 3.4 |
| Freighter F = Time on target, hitscan like the railgun, all lines landing at once. Sentries thrown to the cursor. Sentries prefer the paint, otherwise anything (8fddb84 kept) | TOT card, Unmask deleted, `ITurretHost.Prefer` added, and the turret's hold rule lets go for a new paint. v3 decision 14 closes this way | 3.1 |
| DD Grapnel pulls the DD in; the torn chunk deals 1% of the target's total hull + 10 | The rip is a real hit through the hostile damage door | 3.3 |
| Carrier Q = Supercarrier; it engages "anything" | Missiles included, ranked as a sentry ranks | 3.2 |
| Warden Taunt 33% for 6 s; Dart keeps Ramjet; Sniper: v1 Anchor + a new piece | v3 §3.5 / §3.6 / §3.4 unchanged. The Sniper's piece is still open (decision 9) | — |
| Heavies' twin laser 2.58; heavies never own CC | v3 §3.7 unchanged | — |
| Supers unchanged (250 / 250) | The burn and the rock stay 250. The curve's cut (Lancer ×0.744, Drake ×0.787) takes every other move, including the ram and the scrap shotgun, which the code flags `Super` | 5 |
| Player respawn 24 s; the whole party down at once fails the mission | **Already in the working tree** (uncommitted, with its rung-3 check). The kits add nothing | 3.7 |
| Raids: beam escorts become squad wave 1 from L1; any add's web may start the beam; 0.6 s escape floor; H,L,L,L growth; 30 s refills with no EXP | The raids lane (G), unchanged from `raids_v2.md` plus these rulings. With no chips the fight with adds runs longer; decision 15 holds the 60 s | 5, 8 |
| Curve defaults; salvage levels live on the slot, per pilot | The curve lane (F) | 8 |
| Items by hull category, 8-12 lines each; +10% compounding a tier, 10 tiers; saves disregarded | The items lane (I), last. `Game.Version` goes 2 → 3 with the kits release and 3 → 4 with the items release, since each changes what a save holds | 8 |
| Drake silent during its throw | Already built (242b1aa). No work | — |
| Sprites: hit sizes unchanged; BB mains on the 4 flanking twins | The sprite lane (H). **No Unmask seats** any more | 8 |
| Network: webrtc-native, invite codes, public STUN rows only | The network lane merges first (step 0). It also deletes today's other outside contact, the api.ipify.org lookup (`Net.PublicIpService`), and UPnP | 8 |

---

## 1 · WHAT CHANGED FROM v3

"Scope" uses v3's three words. **Row** means a table that already exists. **New table** means a foundation comes first. **Foundation** means a small addition to one.

| class | slot | v3 | v3.1 | scope |
|---|---|---|---|---|
| FREIGHTER | F | Unmask (2 hidden mounts) | **Time on target**: up to 4 hitscan lines, all in one host tick | new table (`Lines.cs`), then rows |
| FREIGHTER | sentries | the paint gate put back (decision 14) | **the paint first, then anything hostile** (8fddb84's `Targeting.Sentry` kept) | foundation (`ITurretHost.Prefer`) |
| CARRIER | Q | the patrol never fires on a missile (decision 13) | **engages anything, missiles first** (`Turret.Rank`) | row |
| DESTROYER | E | the rip is visual only | **rip = 1% of the anchor's max hull + 10**, a real hit | row |
| the nine non-capitals | V | hold-to-warp | **boost**: +50% top, thrust and strafe for 3 s, cooldown 15 s from the press | new table (`Drives.cs`) |
| the nine | helm | A/D are the rudder only | **Shift + A/D strafe** at half the top speed | new table (F24) |
| FREIGHTER, TENDER, BASTION | helm | top 85, and capital warp (decision 9) | **top 120**, thrust scaled to match, no warp (decision 2) | row |
| BB, CV, DD | V | warp | warp, unchanged from v3 §3.9, now on the capitals alone | row on `Drives.All` |
| every class | chips | 3/4/5/6 slots, kind caps | **6 slots**, kind caps, walled at L2/4/8/10/12/14, none fitted at start | row |
| curve | L1 boss | 3820 / 3520 (3 kit chips) | **3222 / 2968**, every move but the two 250s at **×0.744 / ×0.787** (numbers §8) | rows (curve lane) |

**Gone:**
- Unmask: `TurretSpec.While`, the `ClassArt.Hidden` seats, the unmask_* rows, and the sprite batch's flank seats.
- The warp on nine hulls.
- The warp's ANCHORED refusal. It moves to the boost.
- The Rewind and Shadow step exemptions from warp pricing. No capital has either.
- The rows tot_flight and collect_range.
- `chip_basic`.
- v3 decisions 6, 9, 10, 11, 13 and 14, which your rulings answered or made moot.

---

## 2 · THE TABLE

| class | role | Space | F | Q | E | R | passive | V | hull | top u/s |
|---|---|---|---|---|---|---|---|---|---|---|
| BATTLESHIP | turns its side, outguns everything | Main battery | Broadside | Brace | CIWS | salvo / stagger | PD x2 | **warp** | 500 | 88 |
| CARRIER | fights only through its craft | Fighter wing | Bomber strike | Supercarrier | Warp gunships | recall | PD x2 | **warp** | 425 | 99 |
| DESTROYER | hooks, hauls in, circles, torpedoes it | Director battery | Long Lance | Suppressing fire | Grapnel (pull, swing, **rip**) | | PD x2 | **warp** | 395 | 117 |
| FREIGHTER | paints targets; its guns converge | Spotter cannon | **Time on target** | Bubble | Redeploy | sentry throw / recall | PD x2 | boost | 450 | **120** |
| TENDER | one beam, heal or harm | Mending lance | Overdrive field | Repair field | Resupply | | PD x2 | boost | 380 | **120** |
| BASTION | pulls them in, shells them | Siege mortar | Bunker buster | Shockwave | Gravity well | | PD x2 | boost | 420 | **120** |
| WARRIOR | blade cuts; prism splits light | Blade | Prism stance | Whirlwind | Lunge | | none | boost | 300 | 190 |
| SNIPER | plants, charges, pierces, flares missiles | Charge railgun | Anchor (v1) | Tether mine | Flares | | Overcharge | boost | 240 | 190 |
| WARDEN | draws raiders in, shreds them | Proximity flak | Hunters | Taunt | Flak curtain | | PD x1 | boost | 270 | 190 |
| DART | its top speed is damage | Pepperbox | Rod from God | Ramjet | Slingshot | | Slipstream | boost | 200 | 260 |
| ECHO | every shot and pulse repeats | Echo repeater | Reverb | Rewind | EMP | | none | boost | 180 | 260 |
| WRAITH | unseen poisoned shotgun from behind | Ambush scattergun | Veil | Venom | Shadow step | | Backstab | boost | 220 | 260 |

**Keys.**
- V is the class's drive: the capitals hold it to warp, the nine tap it to boost.
- **Shift is new, and fixed like V.** On the nine, holding Shift turns A/D into strafe.
- R is only a weapon's second action.
- X, G, T and C stay unused.

---

## 3 · CARDS: only what changed

Every "Proves" line is rung 3 unless it says otherwise. Positions come from Vary / VaryAngle / VaryNear, and the figures the checks assert are literals.

### 3.1 FREIGHTER · Time on target (F), hitscan; the sentries

| piece | numbers | counterplay |
|---|---|---|
| **Time on target** (F) | Needs a paint under 5 s old. Every gun the freighter owns that is within **1500 u** of the painted target fires **one hitscan line** at it, all in the **same host tick**: the spotter from the hull, plus each sentry that has landed (not one still in flight). <br>• Each line runs from the gun's muzzle to the painted target's centre, **14 u wide** like the railgun, and stops at the target. <br>• **40** to everything hostile on the line (the railgun's rule, decision 4), the painted target included: 40 with no sentries out, 160 with three. <br>• Allies are never hit. **Cooldown 16 s** from the press. <br>• **Refused:** NO PAINT, COOLING. <br>**Power:** 10.0 sheet. Realistic **8.0** (paint up 90%, sentries in reach 85%); v2's 1.2 s flight gave 7.0 | the paint lapses 5 s after the last spotter hit · a sentry more than 1500 u from the target is silent · 16 s |
| **Sentry** (R) | v3 §3.3 unchanged: thrown to the cursor (up to 600 u, 0.8 s), recalled with R within 60 u, 3 out, 6 s between throws, 120 hull, 650 u, 10 DPS each | raiders go for sentries · 0.8 s in flight |
| **Sentry targets** | **The painted target first** whenever it is inside the sentry's 650 u. Otherwise **anything hostile**, ranked by `Turret.Rank`: missiles and hulled rounds, then lights and fighters, then heavies, bosses and structures. A practice dummy only as a fallback. This is 8fddb84's `Targeting.Sentry`, kept as it is | a target outside 650 u · the paint outranks a seeker (your ruling); the freighter's own PD x2 still takes missiles at the hull |

**On screen.** Rail lines in the spotter's colour leave every gun at once and converge on the target. Each line is heard as its own `Beam` row, `tot`. Every peer draws them through `Fx.Line`; the host alone deals the damage.

**Engine.**
- **F23 `Lines.cs` (new table).** Today the railgun's segment strike is written out inside `PlayerShip.FireRail` (the `DistToSegment` loop). It becomes `Lines.All` rows {Width, Reach, Stops: Never | AtTarget | FirstBody, Fx, Beam} and one door, `Lines.Strike(row, from, to, damage, credit)`.
  - The railgun is row `rail` (14 u, 2500 u, never stops).
  - Time on target is row `tot` (14 u, 1500 u, stops at the target).
  - F11's prism children read the same table (`FirstBody`, 36 u).
  - The header names what it replaced and what a new row must fill in.
- **The paint preference:** `ITurretHost` gains `IHittable Prefer`. A sentry's host returns its owner's live paint. `Turret.Acquire` ranks the preferred target above rank 0.
  - **The hold rule changes with it.** Today a turret keeps its target until that target dies, leaves the world or goes out of reach (the guard at Turrets.cs:152). Acquire alone would leave a sentry on a seeker when a paint appears, and on the painted gunship after the paint lapses.
  - So the guard also re-acquires when `Host.Prefer` is not the turret's target: a new paint in reach, or the paint it was following lapsing. A null `Prefer` changes nothing, so PD, CIWS and the base's guns behave as today.
- **Rows:** tot_damage 40, tot_reach 1500, tot_cooldown 16. F4 credits each line as `tot`.
- **Deleted:** tot_flight, the per-call shells, `Missiles.Predict`'s use here, and the `Fx.AimZone` raise. Unmask and every piece of it goes too.

**Proves.**
- With {0, 1, 3} sentries out at 1499 u from the paint, TOT gives {1, 2, 4} lines. One sentry at 1501 u gives no line. The distance is the figure the check asserts, so only the bearing varies (VaryAngle). VaryNear would draw 40-100% of it. A sentry thrown 0.3 s before the press gives no line.
- Every line lands in the same host tick (one DealtBy frame), from VaryAngle bearings.
- On a dummy moving straight at {0, 130, 260} u/s: 40 per line, 160 with three sentries.
- A light standing across one line takes 40, and the painted target still takes 40 from that line.
- Paint age 4.9 s fires; 5.1 s is refused NO PAINT. The cooldown is 16.0 s.
- **Sentries:**
  - A painted gunship at 600 u beats an unpainted seeker at 300 u.
  - A sentry already firing on the seeker turns to a paint raised on the gunship within one tick.
  - When the paint lapses, the seeker is taken, although the gunship is still in reach.
  - An unpainted boss is taken when nothing else is in reach.
  - A dummy is taken only when nothing else is.
- **The railgun on `Lines`:** 45 to each of 3 dummies on a VaryAngle line. This is today's check, now through the new door.
- **Rung 5:** a guest draws every line from its own copy of the sentries to the host's target, within 20 u.

### 3.2 CARRIER · Supercarrier engages anything

v3 §3.1 stands with one change. **The patrol wing takes whatever `Turret.Rank` puts first inside its 600 u ring**: a missile or a hulled round, then lights and fighters, then heavies, bosses and structures, with a dummy only as a fallback. That is the order every gun that picks for itself uses, so there is no special case.

**Why missiles are included.** You said "anything", and nothing argues against it.
- The only hostile missiles a gun can shoot are the Lancer's trident seekers (135 u/s) and the siege cruise missile (120 u/s, hulled). A raider's missile is a telegraphed blast (Missiles.cs), not a body. Fighters at 352 u/s run both down.
- The v3 reason against was the overlap with PD and CIWS. It is weak: the three are split by carrier, reach and time (§7).
- Missiles come **first** because that is `Turret.Rank`. v3's alternative put them last, which would be a second ranking for one wing.

**What it costs, what it saves.**
- Against a lone Lancer, about 8% of the patrol's time goes on seekers: 3 every 15 s, one 1.2 s pass each.
- The patrol is **11.0 sheet / 5.6 realistic** (v3: 12.0 / 6.1). The Carrier is **52.1** (v3: 52.6).
- It stops about 0.6 DPS of trident sheet aimed at the carrier at today's rows, 0.45 after the ×0.744 cut (numbers §8).

**Engine.**
- The `Patrol` TargetFilter row forbids nothing and takes a Dummy only as a fallback (`WingPrey` today forbids Missile and Dummy).
- **No shot change.** A fighter's shot is hitscan on the target it chose (`TakeDamage` and a flash, in the wing's Burst state, ShipClasses.cs). So the filter row alone lets a patrol craft take a seeker.

**Proves.**
- A seeker at VaryNear 590 u from the carrier is engaged first and brought down before it lands. One at 610 u is not engaged.
- The v3 checks, with the seeker line inverted.
- **Rung 5:** a guest sees the patrol down a seeker, and the seeker disappears on the guest too.

### 3.3 DESTROYER · the torn chunk is a real hit

v3 §3.2 stands: the pull, the swing, the tow and hurl, and the rip's look (chunk, sparks, smoke, scar, sound). **One change: the rip deals damage.**

- **At cast-off, the anchor takes 1% of its own maximum hull, plus 10.**
  - Maximum hull is the level- and party-scaled `MaxHp`.
  - On the L1 Lancer (3222) that is **42.2**; at L40 (7141) it is 81.4.
  - It is a hit through the hostile damage door (F4), credited to the DD as `grapnel`.
- **Where it applies:** bosses, structures and dummies. Never a craft; the tow and hurl keep their own 60 / 120.
- **When there is no rip:** the anchor died or warped. A second E, the 5 s limit, a web and the DD's own warp all rip.
- **Power:** one rip per 21 s cycle (5 s swing + 16 s cooldown) is **+1.95 sheet / +1.56 realistic**, which puts the DD at **56.6**.
  - Because it is a share of the boss's hull, it is about 3% of every kill at every level, and it scales with the party.
  - **Flagged in §7:** it is the only damage in the game read from the target's hull rather than the pilot's gear.
- **Engine:** the row grapnel_rip_share 0.01 and grapnel_rip_flat 10. The same NetFx raise carries the look. No new RPC.
- **Proves:**
  - On {Lancer, Drake, pylon, base} at VaryAngle, the anchor's hull drops by exactly 0.01 × MaxHp + 10 at cast-off (40.9 on the L1 Lancer).
  - Nothing on a craft. Nothing when the anchor dies first.
  - In a party of 2, the rip is 1% of the party-scaled hull.
  - This replaces v3's "hull the same before and after".

### 3.4 V · the drive: warp on the capitals, a boost on the nine

**`Drives.cs` (new table), named for the mechanism.**
- `Drives.All` holds two rows, and every hull names one: `ClassDef.Drive`.
  - **warp**, kind Jump: the three capitals.
  - **boost**, kind Surge: the other nine.
- The V key presses the class's drive. The drive is never walled.
- **The drive's slot.** A ship's slots are built from `Abilities.For(Class)` (PlayerShip.FitClass): today that is `ClassDef.Abilities` plus the six open keys. `For` appends the class's drive row the way it appends the open keys. The drive then has a slot on the wire, while Resupply, which walks `ClassDef.Abilities`, never reaches it.
- **The header:**
  - says it replaced PlayerShip's warp block and `Hints`' single "warp" row;
  - says the Drake's warp opener is a Boss move row and not this;
  - says what a new drive row must fill in.
- **The numbers are stat rows on the sheet**, so gear can move them later:
  - warp_safe, warp_rate, warp_cooldown on the three capitals;
  - boost_time, boost_cooldown, boost_share on the nine.

#### Warp (BB, CV, DD)

v3 §3.9 unchanged, on the capitals alone:
- 1.0 s spool, then the range grows at **800 u/s**;
- safe to **2400 u** (a 4.0 s hold), overshoot up to +900 u;
- **disabled 2 s per 300 u over** (proportional, decision 12), cooldown **20 s** from the jump;
- the aimed landing and the range ring, ghost and readout as v3.

**What gets simpler:**
- The host prices a jump only on a hull whose drive is warp. No capital has a Rewind or a Shadow step, so there is no exemption list and no 0.1 s spacing rule.
- **One new rule:** the host prices a snap only between two reports of the same live ship in the same world. Entering a world or re-boarding is never a jump (a rung-5 check).

**Long haul, with the speed lost on each jump** (every jump sets speed to 0):

| hull | top (today) | thrust (today) | today: warp 1200 / 30 s | v3.1: warp 2400 / 20 s | with a full overshoot |
|---|---|---|---|---|---|
| BATTLESHIP | 88 (104) | 47 (56) | 136 u/s | **183 u/s** | 194 u/s |
| CARRIER | 99 (116.48) | 49.5 (58.24) | 148 u/s | **193 u/s** | 201 u/s |
| DESTROYER | 117 (130) | 63 (70) | 161 u/s | **211 u/s** | 215 u/s |

#### Boost (the nine)

| piece | numbers | counterplay |
|---|---|---|
| **Boost** (V, tap) | For **3 s**: **+50%** on top speed, thrust, strafe speed and strafe thrust. It is one F1 lift row on four stat ids, additive with every other lift. **Cooldown 15 s from the press.** It uses the ability path: the owner flies at once, the host's slot is the truth, and the slot's `Left` puts the plume on every peer. <br>**Refused:** ANCHORED (the Sniper), and in stasis. <br>**Webbed:** allowed. The web's 20% of top is a share taken after the sum, so a boosted pinned hull moves at 30% of its sheet top. Strafe stays at 0 under a web, like the rudder. **The boost does not break a web** (decision 3). <br>**Reaching the new top:** light 260 → 390 u/s in 0.76 s; heavy 190 → 285 u/s in 0.86 s; freighter 120 → 180 u/s in 1.43 s | 3 s of every 15 · the new top takes most of a second to reach · a web holds it |

**Long haul without warp:** freighters 129 u/s (today 118 with warp; 92 if they stayed at 85), heavies 206 (today 221), lights 283 (today 290). The nine lose little; the capitals gain about a third.

**On screen:**
- a triple-length engine plume, the raiders' boost look;
- the slot reads `BOOST 2.1 s`, then `BOOST 9 s` while cooling;
- the hull bar's warp readout becomes the drive's readout.

#### Strafe (F24, new table; decision 1)

- **Hold Shift, and A/D slide the hull sideways** instead of turning it.
  - W/S still drive along the keel.
  - The nose holds its heading, so the guns, the cursor, the rod, the Pepperbox, the blade, the prism guard and the railgun line keep their meaning.
  - Release Shift and A/D are the rudder again.
- **Rows** (Helm group, on the sheet):
  - `strafe_speed` = half the top speed: lights 130, heavies 95, freighters 60;
  - `strafe_thrust`: lights 520 and heavies 380 reach full slide in 0.25 s; freighters take 0.5 s at 120 (the owner's earlier "half strafe acceleration").
  - Boosted: 195 / 142.5 / 90 u/s.
- **Capitals carry `strafe_speed` 0, so they never strafe.** That is read from the stat, never from the class. The capitals' check "A/D pivot slowly and never strafe" stays as it is.
- **The physics, in `PlayerShip.Steer`:**
  - while strafing, the across speed moves toward ±strafe_speed × lift at strafe_thrust × lift;
  - otherwise the keel's grip damps it, as today;
  - a web or Disabled sets strafe to 0.
- **Holds reach the slide.** Every hold share lands on `strafe_speed` as it lands on `max_speed` (F1): the Anchor's x0, the railgun lock's x0 until 6c deletes it, and the Prism's x0.5. Otherwise a planted Sniper would slide at 95 u/s.
- **Shift + click still adds a target** (Hub.cs:1675). Only the capitals hold more than one target (`Targets = 3` in Ships.cs), and they never strafe.
- **Authority:** strafe is owner-side flight. Position and velocity already go to the host at 20 Hz, so there is no new RPC.
  - **Slipstream's speed clamp changes:** the host clamps the speed it reads to hypot(top, strafe_speed) × 1.1, not top × 1.1, or a legal slide reads as a cheat.
- **Shift** joins the Reserved keys. The K window lists it, and a `Hints` row "strafe" goes to the nine.

**Proves (the boost and strafe; literals from your ruling, +50% for 3 s, 15 s).**
- On a light, a heavy and a freighter at VaryAngle headings, V raises the top to {390, 285, 180} and it is back to {260, 190, 120} at 3.1 s. A second V is refused until 15.0 s after the press.
- Shift + D from rest: the slide reaches {130, 95, 60} ± 3 u/s and the heading moves under 0.5°. Boosted, it reaches {195, 142.5, 90}.
- A capital with Shift + A/D pivots and never slides. This is today's check.
- Anchored, V is refused ANCHORED and Shift + A/D moves the Sniper 0 u in 2 s. Webbed, a boosted hull's top is 30% of its sheet top, and its slide is 0.
- A Resupply leaves the drive's cooldown where it was.
- **Rung 5:** a guest's boost and slide put its ship within 20 u of its own report on the host, and the host's speed clamp never fires on a boosted slide.

#### The drive against every class's moves

| class | move | what the drive does to it | rule |
|---|---|---|---|
| BB | Brace (x0.35, half speed) | The charge and the jump are unaffected, and the guard keeps running through a jump | none |
| BB | Broadside, CIWS | A broadside fires mid-charge. A running CIWS **stops** under an overshoot's disable (it is a gun) | decision 13 |
| CV | wings, gunships, patrol | Craft already out fight on through a disable. The patrol is leashed to the carrier and flies back after a jump (about 7 s from 2400 u). The gunships' 3000 u leash is checked at launch only | none |
| DD | Grapnel | A DD warp casts off **with** a rip. An anchor that warps casts off with none. The pull is 22 u a report, never a snap | none |
| all three capitals | webs | A jump leaves the webber's post (latched only within 12 u, Raider.cs), so the web drops | today's behaviour |
| SNIPER | Anchor | **The boost is refused ANCHORED.** The Anchor's root is a share taken after the sum (F1), so no lift moves a planted Sniper | refusal |
| SNIPER | railgun charge | Strafe slides a charging line sideways with the hull. Moving the line onto a dodging target is skill | accepted |
| WARRIOR | Prism stance (x0.5 speed) | The stance's x0.5 is a share after the sum: boosted in stance is 1.5 × 0.5 = **x0.75**, never x1.0 | F1 share rule |
| WARRIOR | Lunge | A fixed 420 u in 0.3 s, never priced from speed | none |
| WARDEN | Taunt | The boost lets it drag called raiders across its own Curtain | accepted |
| DART | Ramjet | Additive: the top is 260 × (1 + ramjet + 0.5), up to x2.0 = 520. The build pauses while the Dart climbs to the boosted top (within 5% of current top, along the keel). Strafe is not yaw, so it never bleeds the build | none |
| DART | sprint + rod | Sprint thrust +200% and boost +50% add (x3.5). Top is (260 × 1.5) + 100 = 490, so the rod prices at **339.2**, and at 360 (its cap) with the Ramjet built | counts (decision 14) |
| DART | Pepperbox | Any boost puts it at its x1.5 cap (11.25 a missile) for 3 s | counts (decision 14) |
| DART | Slingshot | Snaps the whole velocity, strafe part included, onto the cursor bearing, keeping its size | none |
| DART | Slipstream (x0.7 at ≥ 325 u/s) | A boost puts a Dart above 325 within 0.4 s. The host's clamp uses hypot(top, strafe) × 1.1 from its own lifts | clamp rule |
| ECHO | Rewind | Position, heading and velocity go back **8 s** (owner, 2026-09-24). The boost's time left is not rewound, and the helm clamps a rewound speed to the current top | none |
| WRAITH | Veil (x1.35 top) | Additive: 1 + 0.35 + 0.5 = x1.85 = 481 u/s | none |
| WRAITH | Backstab | Strafe holds a rear arc while the nose stays on the target | accepted |
| TENDER | Resupply ("every ability still cooling") | **Never touches V.** The drive is not in `ClassDef.Abilities`, so the capitals' warp is not cut either | by list, not by `if` |
| TENDER | Overdrive field | Fire rate only, so none | none |
| FREIGHTERS | throw, TOT, Bubble, Redeploy, well, Shockwave | None. The boost moves the hull; its own pieces are placed or hull-centred | none |
| the nine | a web | The boost does not break it. The nine lose the web break the warp gave them (§7, decision 3) | decision 3 |

### 3.5 HELM: the capital cut, and the freighters

- **Capitals, as v3:** BB 88, CV 99 (`CarrierTop` 116.48 → 99 moves all four of the carrier's rows), DD 117. Thrust is cut in step, so each reaches its top in the same time. Turn rates are unchanged.
- **Freighters (default, decision 2):** top **85 → 120**.
  - Thrust 45 → 63.5, reverse thrust 20 → 28.2, reverse top 30 → 42.4. That is x1.41 on each, so they reach top in the same time.
  - Turn rate 0.85 and turn radius 150 are unchanged (yaw at top is 0.8 rad/s, under the 0.85 cap).
  - **Why:** your rule "capitals are slower overall and rely on warp". At 85 with no warp, a freighter would be slower than every capital both in the water and over distance (92 against 183-211 u/s).
  - At 120, the order in the water is capitals 88-117 < freighters 120 < heavies 190 < lights 260. Over distance, the capitals' warp puts them level with the heavies.
- **Scope:** F22, rows on Ships.cs.
- **Proves:**
  - Tops BB 88, CV 99, DD 117, FR/TE/BA 120, with DD > CV > BB.
  - The carrier's thrust is 49.5; the freighters' is 63.5.
  - Time from rest to 95% of top matches today's within 0.05 s on every changed hull.
- **The DD's swing is unchanged by any of this.** At 117 u/s and 320 u it outruns the Lancer's bow until L21 (v3 risk 6).

### 3.6 CHIPS AND THE WALLS

**Chips (F15, reduced).**
- `Equipment.ChipSlots` 5 → **6** on every hull. There is no per-class count and no `ClassDef.Chips`.
- `ChipKind` {Combat, Utility}: at most 3 of each.
- The host's `Sanitize` moves anything over a cap to the hold, and the window refuses a 4th of a kind.
- `Equipment.Default` fits **no chips**, and **`chip_basic` is deleted** (decision 7). Its callers move in the same edit: 16 in SmokeTest.cs.txt and 2 in Equipment.cs.
- **Saves:** `Game.Version` 2 → 3 with the kits release, and 3 → 4 with the items release (§8 step 9), because each changes what a save holds. `Character.Playable` already refuses an older file, so nothing is migrated and nothing is refunded, as you ruled.

**The walls (`Unlocks.cs`, `level_gates.md` §3, with these changes):**

| class | ability 1 (L1) | ability 2 (L3) | ability 3 (L6) | never walled |
|---|---|---|---|---|
| BATTLESHIP | F Broadside | Q Brace | E CIWS | the main battery and its R, PD, V warp |
| CARRIER | F Bomber strike | **E** Warp gunships | **Q** Supercarrier | the wing and R recall, PD, V warp |
| DESTROYER | F Long Lance | Q Suppressing fire | E Grapnel | the director, PD, V warp |
| FREIGHTER | F **Time on target** | Q Bubble | E Redeploy | the spotter and the R sentry, PD, V boost |
| TENDER | F Overdrive | Q Repair field | E Resupply | the lance, PD, V boost |
| BASTION | F Bunker buster | Q Shockwave | E Gravity well | the mortar, PD, V boost |
| WARRIOR | **E** Lunge | Q Whirlwind | **F** Prism stance | the blade, V boost |
| SNIPER | F Anchor | Q Tether mine | E **Flares** | the railgun and **Overcharge** (a passive), V boost |
| WARDEN | F Hunters | Q Taunt | E Flak curtain | the flak, PD, V boost |
| DART | F Rod from God | Q Ramjet | E Slingshot | the Pepperbox, Slipstream, V boost |
| ECHO | F Reverb | Q Rewind | E EMP | the repeater, V boost |
| WRAITH | F Veil | Q Venom | E Shadow step | the scattergun, Backstab, V boost |

- **Chip slots: 1 at L2, 2 at L4, 3 at L8, 4 at L10, 5 at L12, 6 at L14, on every hull.** The reference pilot reaches L14 at boss L9, fight 36.
- Strafe (Shift) and the drive (V) are never walled.
- Everything else stands as written: `level_gates.md` §3.3 items 2-10, `Character.Peak` from its item 1, and its checks 1-17.
  - `ChipSlots(IGated)` is now `Count(ChipSlot, Peak)`.
  - Check 6's literals become "every hull: 0, 1, 2, 3, 6 at levels {1, 2, 4, 8, 14}".
  - Check 11's literals become HullScale(2) = 1.098 and anchor 3222 (numbers §1.3). Its 1.109 / 3779 assumed the baked chips.
  - The "bake" edits and `ClassDef.Chips` (§3.3 item 1, check 10) are dropped.
- **What the walls cost** (Lancer kill in seconds, from `power_v31.py`):

| class | L1 | L2 | L3 | L4 | L5 | L6+ |
|---|---|---|---|---|---|---|
| CARRIER | **86** | **73** | **70** | 61 | 59 | 59 |
| DART | 48 | 43 | 42 | 42 | 43 | 43 |
| WRAITH | 51 | 48 | 47 | 47 | 48 | 48 |
| the other nine | within ±3 s of their L6+ figure at every level |

The Carrier is the one real cost: only 28% of its damage (its bombers) is open at L1. Nothing else changes from `level_gates.md` §2.3.

### 3.7 RESPAWN: 24 s (already built)

- The working tree already has `PlayerShip.StasisTime = 24`, together with the rule "the whole party in stasis at once fails the mission" (Hub.cs) and the stasis readout.
- Its rung-3 check is already in place: "re-boards at 24 s and not before, while its partner flies on".
- It commits with the in-flight work in step 0. The kits change nothing about it.

### 3.8 Unchanged from v3

Everything else in these sections of v3 §3 stands unchanged:
- **Grapnel:** pull, swing, tow (§3.2), except the rip's damage above.
- **Sniper:** the v1 Anchor and Overcharge (§3.4). Decision 9 is still open.
- **Warden:** Taunt (§3.5).
- **Dart:** Ramjet (§3.6).
- **Enemy heavies:** 2.58 per heavy, with the raids re-run (§3.7).
- **Supercarrier:** its timing, ring and wire (§3.1).

---

## 4 · POWER: base classes, no chips, one target, a lone boss

The method is v2's. **Stock with no chips** is what the realistic column always was. What is new is the columns around it:
- **Kill L1** is the class's own walls (ability 1 only) against the new L1 Lancer.
- **Kill from L6** has every ability open. It holds at every level after L6, because the boss and the pilot grow by the same factor.
- **TTD** is a pilot who stops dodging, against the L1 boss with every move but the burn and the rock at kits v3.1's ×0.64, and those two at 250 (the reconciled cut is ×0.744 / ×0.787 on a four-fight hull, which holds 31 / 42 s; numbers §1.2).
- **Hull lost** is the realistic share of hull lost over the class's own L6+ kill, with 0.5%/s regen.

| class | tier | hull | top | sheet | realistic (v3) | kill L1 (walled) | kill from L6 | TTD Lancer / Drake | hull lost Lancer / Drake |
|---|---|---|---|---|---|---|---|---|---|
| BATTLESHIP | capital | 500 | 88 | 52.3 | 46.0 | 65 s | 67 s | 40 / 56 s | 0.09 / 0.10 |
| TENDER | freighter | 380 | 120 | 48.4 | 46.0 | 68 s | 67 s | 29 / 40 s | 0.60 / 0.24 |
| BASTION | freighter | 420 | 120 | 55.0 | 47.0 | 64 s | 66 s | 33 / 45 s | 0.17 / 0.18 |
| WARDEN | heavy | 270 | 190 | 53.0 | 49.0 | 61 s | 63 s | 20 / 27 s | 0.80 / 0.37 |
| WARRIOR | heavy | 300 | 190 | 74.7 | 50.0 | 63 s | 62 s | 22 / 30 s | 0.96 / 0.36 |
| SNIPER | heavy | 240 | 190 | 56.7 | 51.0 | 59 s | 61 s | 17 / 23 s | 0.26 / 0.32 |
| **CARRIER** | capital | 425 | 99 | 63.1 | **52.1** (52.6) | 86 s | 59 s | 33 / 45 s | 0.22 / 0.07 |
| **FREIGHTER** | freighter | 450 | 120 | 65.0 | **56.0** (56.1) | 54 s | 55 s | 35 / 49 s | 0.38 / 0.12 |
| **DESTROYER** | capital | 395 | 117 | 63.6 | **56.6** (55.0) | 56 s | 55 s | 30 / 42 s | 0.49 / 0.18 |
| WRAITH | light | 220 | 260 | 77.1 | 64.0 | 51 s | 48 s | 16 / 21 s | 1.11 / 0.47 |
| **DART** | light | 200 | 260 | 87.8 | **71.9** (66.8) | 48 s | 43 s | 14 / 19 s | 0.83 / 0.42 |
| ECHO | light | 180 | 260 | 77.4 | 72.0 | 42 s | 43 s | 13 / 17 s | 1.05 / 0.56 |

**Summary.**
- **Median 51.5** (v3 51.8), mean 55.1. The spread is 46-72, **1.57x** (v3 1.57x). The median class kills in 60 s from L6 (Sniper 61, Carrier 59).
- **Lights stay on top.** The lowest light is the Wraith at 64.0; the highest non-light is the DD at 56.6 (v3: the Freighter at 56.1).
- **What moved:**
  - DD +1.6, from the rip.
  - Freighter −0.1: TOT's hitscan 8.0 against Unmask's 8.1.
  - Carrier −0.5: the patrol's seeker passes.
  - **Dart +5.1: the boost reaches its speed-priced guns.**
    - Pepperbox: 3.4 → 6.0.
    - Rod: 2.3 → 4.9, because 80% of its 12 s rods can be timed into a 15 s boost.
    - That is the generic path ("everything priced from top speed reads it"). Leaving the boost out would be a special case. Decision 14 is the lever.
- **Nothing else moves,** because the boost and strafe only move the hull. They will lift land rates for the short-range classes (Warrior, Wraith) and lower the boss's land rate on the nine. Only flying can see that.
- **Assumptions behind the moved rows:**
  - the patrol loses 8% of its time to seekers;
  - TOT's paint is up 90% and its sentries are in reach 85%;
  - on the sheet, the Dart prices every boost and fires 80% of its rods boosted; realistically it spends 80% of its boosts on pricing and fires half its rods boosted;
  - the DD rips once per 21 s cycle, 80% of the time.
  - The rung 3 landed-DPS probe (H) replaces all of them.

---

## 5 · THE CURVE WITHOUT CHIPS

**Moved.** The curve, the boss cut, the chip budget and the adds share live in `numbers_curve_raids_items.md` (§1-§3), reconciled with this file in its §8 on 2026-09-25. This file's own §5 ran on today's items and is replaced, not kept beside it. In short: Lancer 3222 / Drake 2968 at L1 (the fleet's walled L1 median); every move but the burn and the rock ×0.744 / ×0.787; no starting chips, no salvage on chip slots, chips ×1.24 at L40; the boss hull is not trimmed for adds (fights +2% to +42%).

---

## 6 · FOUNDATIONS: changes to v3 §5

| id | foundation | v3.1 change | cost change |
|---|---|---|---|
| B | Burn clock | unchanged | |
| F1 | Lifts | **+ the share rule:** buffs add; holds and slows multiply after the sum, as the web's `PinSpeed` does today (PlayerShip.cs, the `along` clamp). This covers the Anchor root (x0), the Prism stance (x0.5) and Brace (x0.5). A hold lands on `strafe_speed` as well as `max_speed`. The boost is one row on four stat ids | + XS |
| F2 | Status registry | Disabled on a pilot is the capitals' overshoot only | ± 0 |
| F4 | Damage door + credit | + `grapnel` rip and `tot` lines credited · − Unmask's mounts | ± 0 |
| F5 | Shot.Strike | unchanged: a fighter's shot is hitscan on its chosen target, so the `Patrol` filter (F13) alone lets the patrol take a missile | ± 0 |
| F7 | Primaries | + charge bands (Overcharge) · **− `TurretSpec.While`, − `ClassArt.Hidden`**. The railgun fires through `Lines` | − S |
| F8 | Helm, wards, payload | + the pull, + the point payload | ± 0 |
| F9 | Fields, zones, marks | + Debris and the rip rows, + the patrol ring, + the taunt shimmer, + the boost plume (from the drive's slot) · − the Unmask panels | ± 0 |
| F11 | Blow kind + Prism | the prism's children are `Lines` rows (`FirstBody`, 36 u) | ± 0 |
| F13 | Wing orbit | widened as v3; the `Patrol` filter takes missiles, ranked by `Turret.Rank` | ± 0 |
| F14 | Owned bodies | throw and recall as v3 · **+ `ITurretHost.Prefer`** (the paint), and the turret's hold guard (Turrets.cs:152) re-acquires when `Prefer` changes · `Targeting.Sentry` kept, with no paint gate | + XS |
| F15 | Chips | **6 slots on every hull**, the kind caps, `Default` with no chips, `chip_basic` deleted, `Game.Version` 3 | − XS |
| F17 | Outgoing door | unchanged; the curve's DamageScale goes inside `Boss.Out` | |
| F20 | Enemy rows | unchanged (2.58) | |
| **F21** | **Drives** (was Warp) | `Drives.cs`: warp (capitals, v3 §3.9) and boost (the nine). `ClassDef.Drive` · `Abilities.For` appends the drive's row, so it has a slot on the wire · V reads the class's drive · the host prices snaps on warp hulls only · deletes PlayerShip's warp block, `WarpWarmup`, `WarpRange`, `WarpHop`, `WarpEvery` | M (was M) |
| **F22** | Helm rows | capitals as v3, + freighters to 120 | XS |
| **F23** | **Lines** (NEW) | `Lines.cs`: `Lines.All` {Width, Reach, Stops, Fx, Beam} and `Lines.Strike`. The railgun moves onto it, then TOT and the prism's children | S |
| **F24** | **Strafe helm** (NEW) | `strafe_speed` / `strafe_thrust` Helm rows, Shift in Reserved, the across law in `Steer`, holds on `strafe_speed` (F1), the Slipstream clamp by hypot | L |
| H | Harness | + rows for TOT lines, the rip, the patrol's missiles, the boost, strafe and the walls · − Unmask, − the Rewind / Shadow step warp exemptions | ± 0 |

**Callers to fix in F21's edit.** Find them by name, since other jobs are editing these files: `StartWarp|WarpWarmup|WarpStandoff|WarpHop|WarpEvery|_warpCd|WarpCooldownLeft|WarpWarmupLeft|Warping|WarpAim`.
- SmokeTest.cs.txt: 20 lines.
- PlayerShip: 23 lines (30 hits; the tree has moved since the first count).
- MainMenu: 6.
- Hub: 3.
- HubNodes: 2.
- Shots.cs.txt: 2.
- `Hints` row "warp" (Hints.cs:27) becomes the two drive rows.

**Total:** about **140 S-units** (v3: about 133).
- The strafe helm is +5 (L).
- `Lines` is +1, TOT on it +1, the boost +1.
- The rip, Prefer with its hold rule, and the patrol's missiles (a filter row) are small.
- Unmask's pieces and the warp exemptions come out (about −2).

---

## 7 · OVERLAPS (redone)

| pair | verdict |
|---|---|
| CV patrol vs BB CIWS vs FR sentries vs PD | **Accepted, split by carrier, reach and time.** All four now take missiles. <br>• PD is passive, on the hull. <br>• CIWS is the BB's ring: 460 u, 6 s. <br>• The patrol is craft flying a 600 u ring round the carrier, 20 s of every 50. <br>• The sentries are planted and follow the paint first. <br>All four rank by one `Turret.Rank` |
| FR Time on target vs SN railgun | **Accepted, and they share `Lines`.** The railgun is one charged line along the nose. TOT is up to 4 lines from scattered guns, converging on a painted target, every 16 s |
| FR Time on target vs BB Broadside | as v2: converging scattered guns against a turned side |
| V boost vs DA Ramjet vs DA sprint vs WR Veil speed | **Accepted.** All four are speed lifts and they add. <br>• The boost is the universal 3 s surge. <br>• The Ramjet builds from flying straight. <br>• The sprint is the rod's telegraph. <br>• The Veil's speed comes from hiding. <br>Only the Dart turns speed into damage (decision 14) |
| Strafe vs DA Slingshot vs WA Lunge vs WR Shadow step | **Accepted.** Strafe is a slide at half top. The others are a snap, a dash and a blink |
| the capitals' warp vs the web-breakers | **Accepted, capitals only now.** The nine lose the warp's web break. Their own answers: Rewind and EMP (Echo), Whirlwind and Lunge (Warrior), Shadow step (Wraith), Flares (Sniper, prevents), Shockwave (Bastion, throws), Taunt (Warden, pulls the webbers onto itself). The Freighter, Tender and Dart have none but hull, the Bubble and the sentries. Decision 3 |
| DD rip vs BA Bunker buster | **Accepted, flagged.** Both add boss and structure damage. The rip is **the only damage in the game read from the target's own hull** (1% + 10). It is about 3% of every kill at every level and grows with the party. If flying finds it too strong in a party, the lever is grapnel_rip_share |
| WD Taunt's guard vs BB Brace | as v3 |
| CV patrol vs CV gunships | as v3 (split by anchor) |
| SN Fracture (if chosen) vs WR Venom | as v3 (flagged) |
| **No class slows anything** | as v3 (flagged) |
| *removed* | FR Unmask vs BB CIWS; CV patrol vs FR Unmask (Unmask is gone) |

**Solo.** Every changed piece works for a pilot alone: TOT follows the freighter's own paint, the patrol guards the carrier, the rip needs only a boss, and the boost and strafe are the pilot's own.

---

## 8 · BUILD ORDER: everything signed off

**Standing rules.**
- A writer works in its own git worktree.
- **Compile rungs (1-2) run in every worktree at once.**
- **Engine rungs (3-6) run one at a time across all worktrees.** Every harness shares `%TEMP%\warships_smoke`, so a second run wipes the first.
- Each slice is committed with its own checks. Never start an engine rung while a compile rung is red.
- Kits before items (your ruling).

### Step 0: before any lane

1. **The in-flight work commits.** That is everything `git status` shows:
   - the siege's cruise missile, the equipment base, the beam notes and the docks;
   - **the 24 s respawn**, and both harness files;
   - `Docks.cs` and `Landmarks.cs`.

   Its own author's rungs decide it.
2. **The network lane merges** (`net_tree`): webrtc-native, invite codes, the public STUN rows (Google, Cloudflare), a heartbeat with `DisconnectPeer`, the 256 KiB reliable-RPC guard, the missing-DLL check, and the licences in the export.
   - **The only outside contact left is those two STUN rows.** The lane deletes today's api.ipify.org lookup (`Net.PublicIpService`, Net.cs, still in `net_tree` as of this review) and the UPnP / NAT-PMP code (`rtc_design_v2.md` R2). Until it does, the ruling is not met.
   - **Friends need nothing extra:** the plugin DLL ships in the runtime part PLAY.bat already downloads, and it imports only Windows system DLLs.
   - It proves itself at rung 5 and `-Wan`.
   - **It goes first because it changes the transport under every later rung-5 run.** A kit proved on the old transport would have to be proved again.
3. **Lane A's first commit copies the signed kits into `docs/DESIGN.md`:** the v2 cards, the v3 changes and this file's changes. The scratchpad is session-scoped, and the kits are the spec for the batch.

### The lanes

| lane | what | starts after | files it owns | its rungs |
|---|---|---|---|---|
| **A · combat core** (the spine; one writer) | slices 1-6 below | step 0 | Boss, Combat, Shots, Missiles, Turrets, Abilities, `Lines.cs`, PlayerShip outside the helm and drive, Raider (in the order below) | 2 and 3 per slice; 5 twice |
| **B · drives, helm, strafe** | F21, F22, F24 | slice 1 (F2's Disabled; the railgun lock off Disabled) | **`Drives.cs`**, PlayerShip's `Steer` / `LocalFlight` and the drive block, Hub's V and Shift, the `Reserved` and `For` lines in Abilities.cs (lane A's file, two lines), HubNodes' readout, MainMenu, Hints' drive and strafe rows, the helm and drive rows in Ships.cs / Stats.cs, the harness's warp, boost and strafe sections | 2 · 3 · 5 at A's first run · frames in the one rung-4 sweep |
| **C · chips and walls** | F15 (6 slots), `Unlocks.cs`, `Peak`, `Game.Version` 3, the folded Auto-sell and raid gates | step 0 | Equipment, EquipmentWindow, Character, `Unlocks.cs`, Progression, the AbilityBar lock, `DoAbility`'s wall line, Yard / BasePanel / Raids gates | 2 · 3 · 5 at A's first run (+ the two mutants in `level_gates.md` checks 7 and 13) |
| **D · fields and effects** | F9 | slice 1 | Fx.cs, FxNode, the slot-drawn fields in `PlayerShip._Draw` | 2 · 3 |
| **E · wings** | F13 (gunships, patrol with missiles) | slice 2 (F4 credits wing hits) | ShipClasses.cs (Wing), the fighter and patrol rows in Stats | 2 · 3 · 5 at A's first run |
| **F · curve** | `Par.cs`, boss rows 3222 / 2968, every move but the two 250s at ×0.744 / ×0.787 (numbers §8), no hull trim for adds, siege rows, the salvage ladder 500 × 1.10 capped at highest boss cleared + 1, **salvage levels on the slot, per pilot**, levels on the identity, skip +2, raider Strength as a level | slice 2 for Par, Missions and the boss rows (DamageScale goes in `Boss.Out`); its Equipment / Character part (ladder, slot levels, identity) waits for lane C's merge (one writer on those two files) | Par.cs, Missions, Lancer / Drake move rows, Emplacements, Waves, TioWindow, the Equipment ladder, Character's slot levels | 2 · 3 · 5 at A's second run |
| **G · raids** | `Squads.cs` and raids_v2, plus the rulings: beam escorts become squad wave 1 from L1; any add's web may start the beam; the 0.6 s escape floor; H,L,L,L growth; 30 s refills that pay no EXP | slice 2 (F17 + F20) | Squads.cs, Raids, Waves rows, Raider (its turn in the order below), Boss's beam start (the `s.Target.Pinned` test, Boss.cs:413 today) | 2 · 3 · 5 at A's first run |
| **H · sprites** | the 12 player ships: hit sizes unchanged, BB mains on the 4 flanking twins; `finish_ships.ps1` and old art deleted if unused (CLAUDE.md §7's command list edited in the same commit) | step 0 | art, `ClassArt` in Ships.cs, tools/make_ships | its frames ride the one rung-4 sweep |
| **I · items** | hull-category lines (8-12 each), +10% compounding over 10 tiers, **the chip budget (decision 6)** | the kits' rung 6 is green and lane F has merged (Par reads item rows) | Equipment rows, Loot, the item generator | 3 per slice · 4 once · 5 once · **6 once** |

**Merge points.**
- **B** merges before slice 4, because F8 also edits the input path and Hub.
- **G** merges before slice 5, because of Raider.cs.
- **C, D and E** merge before the end of slice 5.
- **F** merges before slice 6a.
- **H** merges before step 7.
- Each lane rebases on A before every engine run.

**Files more than one lane touches.** Each writer keeps to its own region.
- **Raider.cs has one writer at a time, in this order:**
  1. slice 1's stale comments;
  2. slice 2's F17 + F20;
  3. G (Squads);
  4. slice 5's F14 `Call`, F19 Towed, and the Dazzled / Jammed gates;
  5. F's Strength-as-a-level.
- **Equipment.cs and Character.cs:** C, then F, then I.
- **PlayerShip.cs:**
  - A outside the helm and drive;
  - B `Steer`, `LocalFlight` and the drive block (the ring's draw lives in `Drives.cs`, with one call in `_Draw`);
  - C `DoAbility`'s wall line and `SetProgress`;
  - D the slot-drawn fields in `_Draw`.
- **Ships.cs:** B's helm and drive rows, H's `ClassArt`, C's (none: no per-class chip count), and A's kits in slice 6.
- **Boss.cs:** A (the burn clock, `Boss.Out`, the Burn walk), F (DamageScale inside `Boss.Out`), and G (the beam's start, the `s.Target.Pinned` test). They are line-disjoint; whichever merges second rebases.
- **SmokeTest.cs.txt:** each lane writes its checks inside its own named method.
- **CHANGES.md:** a lane writes its Handoff only at merge.

### Lane A's slices

1. **Slice 1:**
   - B (Boss.cs:509, `s.Next += m.Tick`);
   - F2, with the railgun's charge lock moved to a Lifts share;
   - F16;
   - the stale comments (Raider.cs:17-20, :48; Waves.cs:174).

   Rung 2, then rung 3. Re-baseline the sheet.
2. **Slice 2:** F4 + F17 + F18; F1 (+ Add, + Ramp, **+ the share rule**); F20 (2.58, one edit with the raids' EnemyDef rows). Rung 3. *Lanes E, F and G may start.*
3. **Slice 3:** F5, then **F23 `Lines`** (the railgun moves onto it, same literals), then F6, then F7 (charge bands). Rung 3.
4. **Slice 4:** F8 (+ the pull, + the point payload), F10. **Merge B.** Rung 3.
5. **Slice 5:** F12, F11 (the prism's children on `Lines`), F14 (throw, recall, `Prefer` and the hold rule), F19. **Merge G, then C, D and E.** Rung 3.
   - **Rung 5, first run.** It covers everything that crosses peers so far:
     - predicted-missile NetIds, the F8 payload, the prism band, NetDecoy;
     - B's host-priced overshoot, a guest's boost and a guest's strafe;
     - C's host wall gate, chip sanitising and `Peak` on the wire;
     - E's 6 craft and a patrol kill of a seeker;
     - G's squad RPC and a beam started by an add's web.
6. **Slice 6, the kits by tier.** **Merge F first.** Each tier's witness rows go in the same edit, at rung 3. Each class's `ClassDef.Abilities` is written in learn order, so the walls read it.
   - **6a capitals:** BB (arcs, Brace, CIWS); CV (gunships on E, Supercarrier with missiles); DD (Suppress; Grapnel pull, swing, **rip damage**, tow).
   - **6b freighters:** FR (**TOT on `Lines`**, the sentry throw and recall, `Prefer`); TE; BA.
   - **6c heavies:** Warrior (Prism); Sniper (v1 Anchor, Overcharge, Tether, Flares, the boost refused ANCHORED); Warden (Taunt, Curtain).
   - **6d lights:** Dart (Pepperbox, sprint and rod, Ramjet, Slingshot, the boost in its pricing); Echo (EMP); Wraith.
   - **Rung 5, second run**, after 6d:
     - Suppress, the Grapnel rip (host damage, the chunk on a guest within 20 u), TOT lines, the throw, Supercarrier, Prism, Flares, Taunt;
     - the Pepperbox, the sprint, the host's Ramjet and boost pricing (within 1%), EMP;
     - F's raider Strength and slot levels on the identity.
7. **Rung 4, once**, for every visual in the batch:
   - the prism wedge, clip and fan; the Curtain; the flares;
   - the rip and scar; the patrol ring; the taunt shimmer;
   - **the TOT lines**; the warp ring, band, ghost and readout; **the boost plume and readout**;
   - the 3-ability bar with its locks; **6 chip rows** with their locks; the unlock card;
   - the 12 new sprites (lane H).
8. **Rung 6, once.** If it fails: read which role and which check failed, drop to the lowest rung that covers it, fix, re-prove there, then run rung 6 once more. **A green rung 6 is a release** (your standing OK).
9. **Lane I (items)**, with `Game.Version` 3 → 4, then its own rung 4 once, rung 5 once, rung 6 once, and a release.

**Engine time for the kits batch**, before any fix:
- slices: about 10 × rung 3;
- lanes: about 12 × rung 3;
- rung 5 twice, rung 4 once, rung 6 once.

That is roughly **60 minutes** of engine, run one at a time.

---

## 9 · RISKS

1. **Strafe changes how nine hulls fight** (F24, L).
   - Circle-strafing with the nose on a target raises land rates for the short-range classes and lowers the boss's land rate on the nine. Neither is in the table.
   - **Mitigation:** the DealtBy probe plus `Fly()` at rung 3 measure both. The levers are rows: `strafe_speed`, `strafe_thrust`, and the boost's share.
2. **Warp across peers** (capitals only now). v3 risk 1 stands, minus the Rewind and Shadow step cases.
   - **New:** a host-caused relocation (entering a world, re-boarding) must never read as a jump. Rung 5 checks it.
3. **Pricing parity.** The host prices the Dart's rod and Pepperbox from its own copy of the Ramjet and the boost, both built from 20 Hz state and the boost's slot.
   - **Mitigation:** the host's figure is the only one that counts. Rung 5 asserts the two agree within 1%.
4. **The anchor rests on estimated realistic DPS.**
   - The fleet's walled L1 median sets 3222 one-for-one (numbers §8 R1).
   - The ×0.744 / ×0.787 cut rests on the Lancer and Drake sheets from boss_model.md, with the 5-tick burn.
   - **Mitigation:** the curve lane sets `Bosses[].Hull` from the probe's median at L1, not from this table.
5. **The rip in a party.** 1% of a party-scaled hull is the one damage that grows with the party rather than with the pilot. Watch it at 4 players; the lever is grapnel_rip_share.
6. **Merge pressure.** Raider.cs, Ships.cs, PlayerShip.cs, Boss.cs, Equipment.cs, Character.cs and SmokeTest.cs.txt are shared.
   - **Mitigation:** the one-writer orders in §8, named check methods, and a rebase before every engine run.
7. **The DD's swing past L20:** v3 risk 6, unchanged.
8. **A held warp:** v3 risk 5, unchanged, on capitals only.
9. **The adds' share rests on the old raids model.** It uses the 1.025 step and fit.py's par, so only its v3 → v3.1 change is trusted here (§5).
   - **Mitigation:** the curve lane's rung-3 probe measures the fight with its squads at L6, L18 and L39, and the adds share is set from that (decision 15).

---

## 10 · DECISIONS STILL OPEN (say nothing and the default stands)

| # | fork | default | alternative |
|---|---|---|---|
| 1 | **Strafe:** there is none today, so "+50% strafe" needs one. What slides the hull? | **Shift + A/D on the nine.** Strafe top = half the hull's top (lights 130, heavies 95, freighters 60). Lights and heavies reach it in 0.25 s, freighters in 0.5 s. Nose and cursor unchanged | the nose follows the cursor and A/D strafe (this reopens the Slingshot, rod, Pepperbox, blade and prism cards) · or a speed-only boost now, and no strafe |
| 2 | Freighters with no warp | **top 85 → 120** (long haul 129 u/s; every non-capital faster than every capital in the water) | keep 85 (long haul 92 u/s: the slowest hull in the game, slower than all three capitals) |
| 3 | Does the boost break a web? | **no**: only the capitals' warp still does | a boost clears Pinned as a jump does (the nine get back the web break the warp gave them) |
| 4 | What Time on target's lines hit | **the railgun's rule**: every hostile on a line takes 40, and the line stops at the painted target | only the painted target takes them |
| 5 | *(closed by your ruling)* Sentries: the paint against a missile | **the paint first**, then anything hostile; the freighter's PD takes missiles at the hull | none: "prefer painted targets" answers it |
| 6 | **The chip budget** (SETTLED, numbers §8 R7: no salvage on chip slots; the numbers file's chip rows, ×1.24 at L40, replace the +10% cap) | **chip slots never take salvage levels; one chip ≤ +10% at T10** (4.2% at T1). The median pilot with 6 chips never kills faster than 45 s, a boss is never more than x1.33 behind it, and raids follow | chip slots level like core slots (29 s kills at L40; the boss x2.07 behind) |
| 7 | Starting chips | **none**; `chip_basic` is deleted; chips come from crates | the L2 unlock card gives one common Combat Chip |
| 8 | The curve's DPS anchor (SETTLED, numbers §8 R1: 3222) | **the fleet's median walled DPS** (3091: the median class 60 s, the DD 55 s) | the DD's own walled DPS (about 3330: the DD 60 s, the median class about 65 s) |
| 9 | The Sniper's new piece (CLOSED by the ruling: the active reload, `sniper_active_reload.md`) | **Overcharge, a passive on the railgun** (51.0) | Fracture (51.1; stacks like Venom) · Deadeye (51.4; a ragged window on the wire) · or a key, replacing the Tether or the Flares |
| 10 | Supercarrier (v3 decision 3) | **timed: 20 s, cooldown 30 s from the end** (52.1) | a toggle (about 61: above every non-light, just under the lowest light) |
| 11 | Grapnel on a boss (v3 decisions 4-5) | **pull, then the v2 swing; the rip at cast-off** | pull only · the rip on arrival |
| 12 | Overshoot penalty (v3 decision 7) | **proportional**: 2 s per 300 u | whole steps: 2 / 4 / 6 s |
| 13 | What a capital's overshoot disables (v3 decision 8) | **helm, guns (CIWS included), abilities and warp**; PD and craft already out keep going | the helm only |
| 14 | Speed lifts in the Dart's pricing (SETTLED, numbers §8 R6: they count, gear included) | **they count**: the Ramjet and the boost (Dart 71.9, level with the Echo) | flight only (the Dart at 61) · or the boost excluded (66.8, but that is a special case in the pricing) |
| 15 | **The adds and the 60 s** (SETTLED the other way by the owner, 2026-09-24: NO trim; numbers §8 R8) (new, §5): with no chips, a boss fight with its squads runs +5% at L6 to +51% at L39-40 | **hold 60 s with the adds**: Par carries the adds share per level (from the rung-3 probe), and the boss's hull is divided by (1 + share) | leave the boss's hull alone: fights with squads run 63-90 s, and EXP per minute falls with them |
