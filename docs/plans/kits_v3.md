> Copied unchanged on 2026-09-24 from the sign-off session's scratchpad (`kits3/signoff_v3.md`, now this file). Its `signoff_v2.md` is `kits_v2.md` and its `raids_v2.md` is `raids_squads_adds.md`; `kits_v31.md` supersedes it where it says so.

# SIGN-OFF v3: 12 class kits, your changes applied

The v2 kits (`signoff_v2.md`) are signed off, with your changes. This file applies those changes and designs what they left open. It also gives the new power table and the build order for the whole class batch.

**Where your answers come from.** The sign-off page's saved answers: 23 answers, and the `kits` and `curve` sections marked accepted. Where this file and the page differ, the page wins.

Design only. Nothing in the repo was edited, built or run. The numbers come from two scripts in this folder: `power_v3.py` and `raids_rerun.py`.

A balance, overlap, warp and build-order review has been applied in place: every card was checked against the source and against the other eleven kits.

---

## 0 · YOUR ANSWERS, AS READ

| item | your answer | what v3 does |
|---|---|---|
| Battleship, Bastion, Tender, Warrior, Echo, Wraith | approved | Unchanged from v2. The capital speed cut still applies to the Battleship |
| CARRIER | Q becomes a supercarrier mode that fields a second, automated fighter set that engages "anything" within about 600 u | **Supercarrier** replaces Scramble (§3.1). Whether "anything" includes missiles is decision 13 |
| DESTROYER | the grapnel pulls the DD to the boss and fake-rips a chunk out of it | The pull comes first, then the v2 swing. The rip is visual only (§3.2) |
| WARDEN | the Beacon also cuts damage taken; call it Taunt; show 33% for 6 s | **Taunt** (§3.5) |
| DART | kits-3: "Keep a speed button: Ramjet" | **Ramjet** replaces Jetwash on Q (§3.6) |
| SNIPER | kits-4 note: "Neither? Make something else to replace designate and leave anchor alone" | The v1 Anchor, unchanged, and a new piece (§3.4). **One conflict, see below** |
| FREIGHTER | kits-6: launch the sentry to the cursor · kits-7: Unmask | R throws a sentry and recalls it. F is **Unmask** (§3.3) |
| enemy heavies | kits-2: each barrel at 1.25x the light mean, 2.58 DPS per heavy | The raids model is re-run (§3.7) |
| supers | kits-5: unchanged | Burn 250, rock 250 |
| chips | kits-1: keep 3/4/5/6, at most 3 Combat Chips, plus "3 combat 3 utility chips on any ship" | Read as two caps (§3.8). The other reading is there too |
| capitals and warp | kits-1 note: slower capitals, hold-to-warp with a range display, disabled 2 s per 300 u over the limit, at most 900 u over | Warp drive (§3.9) |
| L1 boss hull | curve-2: Lancer 3820 / Drake 3520 | The kill-time column uses it (§4) |

**The Sniper conflict.** Designate was on E in v1. In v2, E holds the Flares, and you approved the Sniper card with Flares on it. So a new *key* would push out an approved piece: the Flares, or the Tether mine.
- **Default:** the new piece is a **passive on the railgun**, with no key. The Sniper's passive slot is empty today. This keeps the rule of 1 weapon + 3 abilities and leaves F / Q / E as you approved them.
- If you meant a key, say which piece it replaces.

**Not answered on the page, so the defaults stand:** every `raids-*` question, and the `items`, `sprites` and `home` sections. From the raids batch this file uses only the ruling it already applied: a heavy never webs.

---

## 1 · WHAT CHANGED FROM v2

"Scope" uses three words. **Row** means it fits a table that already exists. **New table** means it needs a foundation first. **Foundation** means a small addition to a v2 foundation.

| class | slot | v2 | v3 | scope |
|---|---|---|---|---|
| CARRIER | Q | Scramble | **Supercarrier**: 20 s of a second wing that patrols 600 u round the carrier | row (`Wings.All` `patrol`) on F13, widened to orbit an anchor |
| DESTROYER | E | Grapnel swing, fixed line | **Pull** to 250 u off the hull at 450 u/s, then the swing; at cast-off the hook **rips** a chunk (visual) | rows on F8, plus a new Fx shape `Debris` |
| DESTROYER | helm | top 130 | **117** | row |
| FREIGHTER | F | Time on target | **Unmask**: 2 hidden mounts, 15 DPS each, 8 s, cooldown 24 s | row, plus `TurretSpec.While` |
| FREIGHTER | R | drop under the hull / pick up within 120 u | **thrown** to the cursor, up to 600 u in 0.8 s; R over one of yours **recalls** it | row on F14 (launch and recall) |
| SNIPER | F | Anchor x1.25 damage, cooldown 8 s | **v1 Anchor**: x2.5 charge, 3500 u, cooldown 12 s | row (the two v2 lifts are deleted) |
| SNIPER | passive | none | **Overcharge**: hold past full, 45 → 99 over 0.8 s | row on F7 (charge bands) |
| WARDEN | Q | Beacon | **Taunt**: Beacon + x0.67 damage taken for 6 s, shown on every screen | row (F2 Hardened share) |
| DART | Q | Jetwash (slow wake) | **Ramjet**: +10% top a second at full speed, up to +50%; turning bleeds it | row, plus a Ramp lift on F1 |
| BATTLESHIP | helm | top 104 | **88** | row |
| CARRIER | helm | top 116.48 | **99** | row |
| every class | V | tap: 3 s warm-up, 1200 u, cooldown 30 s | **hold to charge, release to jump**, a range ring, overshoot disables | **new table**: `Warp.cs` |
| every class | chips | at most 3 Combat Chips | at most 3 Combat **and** at most 3 utility | row on F15 (chip kind) |
| enemy heavies | laser | 1.25 DPS per heavy | **2.58** (each barrel 1.29) | row (F20) |
| gone | | Scramble, Time on target, Jetwash (and with it the only slow a class had), the Anchor's x1.25 and 8 s cooldown, the Beacon's name, the tap warp | | |

---

## 2 · THE TABLE

| class | role | Space | F | Q | E | R | passive | hull | top u/s |
|---|---|---|---|---|---|---|---|---|---|
| BATTLESHIP | turns its side, outguns everything | Main battery | Broadside | Brace | CIWS | salvo / stagger | PD x2 | 500 | **88** |
| CARRIER | fights only through its craft | Fighter wing | Bomber strike | **Supercarrier** | Warp gunships | recall | PD x2 | 425 | **99** |
| DESTROYER | hooks, hauls in, circles, torpedoes it | Director battery | Long Lance | Suppressing fire | **Grapnel** (pull, swing, rip / tow) | | PD x2 | 395 | **117** |
| FREIGHTER | paints targets; its guns converge | Spotter cannon | **Unmask** | Bubble | Redeploy | **sentry throw / recall** | PD x2 | 450 | 85 |
| TENDER | one beam, heal or harm | Mending lance | Overdrive field | Repair field | Resupply | | PD x2 | 380 | 85 |
| BASTION | pulls them in, shells them | Siege mortar | Bunker buster | Shockwave | Gravity well | | PD x2 | 420 | 85 |
| WARRIOR | blade cuts; prism splits light | Blade | Prism stance | Whirlwind | Lunge | | none | 300 | 190 |
| SNIPER | plants, charges, pierces, flares missiles | Charge railgun | Anchor (v1) | Tether mine | Flares | | **Overcharge** | 240 | 190 |
| WARDEN | draws raiders in, shreds them | Proximity flak | Hunters | **Taunt** | Flak curtain | | PD x1 | 270 | 190 |
| DART | its top speed is damage | Pepperbox | Rod from God | **Ramjet** | Slingshot | | Slipstream | 200 | 260 |
| ECHO | every shot and pulse repeats | Echo repeater | Reverb | Rewind | EMP | | none | 180 | 260 |
| WRAITH | unseen poisoned shotgun from behind | Ambush scattergun | Veil | Venom | Shadow step | | Backstab | 220 | 260 |

**Keys.**
- V is warp. You now **hold** it and release to jump.
- R is only a weapon's second action.
- X, G, T and C stay unused.

---

## 3 · CARDS: only what changed

Every "Proves" line is rung 3 unless it says otherwise. Positions come from Vary / VaryAngle / VaryNear. The figures the checks assert are literals.

### 3.1 CARRIER · Supercarrier (Q), replacing Scramble

| piece | numbers | counterplay |
|---|---|---|
| **Supercarrier** (Q) | For **20 s** the carrier flies a second wing, the **patrol wing**, beside its own. <br>**The craft.** `fighter_count` fighters (3 stock) leave the hangar 0.83 s apart, the fighter's own launch spacing. Every number is the fighter's: 3.5 a shot, 0.35 s reload, 3 shots a pass, 352 u/s, 3.5 rad/s, 300 u gun reach. Anything that lifts a fighter lifts these: Hangars parts, chips, pilot points, the Tender's Overdrive. They do not rest while the mode is up. <br>**The patrol.** They circle the carrier 300 u out. They engage any hostile body whose hull comes within **600 u** of the carrier's centre: first a raider webbing the carrier, then the nearest. That means craft, bosses, pylons and bases. By default never a missile: your note said "anything", but missiles are the PD's and CIWS's (decision 13). A practice dummy only when nothing else is in the ring. When their target leaves the ring or dies, they go back to circling. <br>**The range.** 600 is the stat `patrol_range`, on the carrier's Reach list, so the Targeting Chip and REACH points lift it (a common Targeting Chip adds 8%: 648 u). <br>**The end.** At 20 s they fly home and dock. **Cooldown 30 s from the end**: a 50 s cycle, up 40% of the time. Never refused. <br>**The first wing is unchanged** (Space attack within 1500 u, R recall). With the mode up, 6 fighters are out. <br>**What it is worth:** <br>• 30 DPS while it fights, 12.0 sheet on average. <br>• Against a lone boss: +6.1 realistic, assuming 85% of shots land and the boss is inside 600 u for 60% of the mode. <br>• In a raid: every webber post (111-177 u) is inside the ring, and a webifier (25 hull) dies in about 1 s of patrol fire | 600 u is inside the Lancer's 900 u guns, so to use it on a boss the carrier must close in · it never shoots missiles (decision 13) · it is up 20 s of every 50 |

**On screen.**
- A dashed 600 u ring round the carrier in the fighter colour, on every peer, drawn from the slot.
- The patrol craft carry an amber livery, so the two wings read apart.
- A small chevron sits over whatever they are engaging.
- The slot shows `SUPER 14 s`, and the deck's runway lights strobe.

**Timed, not a toggle.** Always up, the carrier would reach 61.7 realistic. That is 5.6 above the next non-light (the Freighter's 56.1) and only 2.3 under the lowest light (the Wraith's 64), so lights would stay on top, just. The reason for timing it is the choice: a mode with no cost is simply always on, so it is no longer a decision. It would also move the median to 53.

**Warp and disable.** The patrol is leashed to the carrier. After a jump it flies back to the ring at 352 u/s, about 7 s from 2400 u. Both wings keep fighting through the carrier's own overshoot disable (decision 8).

**Engine.**
- **F13, widened:** `WingWay.Orbit` flies round an **anchor**. For the gunships the anchor is their target; for the patrol wing it is the carrier.
- A row gains `EngageStat` (the ring it fights inside) and a `TargetFilter` row, `Patrol`.
- **A new `Wings.All` row, `patrol`** (WingKind.Patrol). Today the rows are fighter 0 and bomber 1 (ShipClasses.cs:144-152). F13 appends the gunship row first, so the patrol is index **3**, which is its wire id. It uses the fighter's stat ids. The wing report already carries every row in order, so guests draw it with **no new RPC**.
- New rows: super_time 20, super_cooldown 30, patrol_range 600, patrol_orbit 300.
- Scramble's rows are deleted.

**Proves.**
- Q launches 3 patrol craft 0.83 ± 0.05 s apart; with a Hangars part that adds a fighter, it launches 4.
- A raider whose hull edge is at VaryNear 590 u from the carrier is engaged; one at 610 u is not.
- With a common Targeting Chip (+8%), one at 640 u is engaged.
- A seeker inside the ring is never fired on.
- Landed DPS on a parked dummy inside the ring is at least 25 over 10 s.
- The mode ends at 20.0 s and the craft dock. The cooldown is 30 s from the end.
- The first wing still attacks a target 1400 u away, so 6 craft are out.
- **Rung 5:** a guest sees 6 craft and the ring.

### 3.2 DESTROYER · Grapnel (E): pull, swing, rip

**Unchanged from v2:** the 700 u reach and the selected target; tow and hurl on a craft; the 16 s cooldown from cast-off; the refusals.

| phase | numbers |
|---|---|
| **PULL** (the anchor is a boss, structure or dummy) | A 0.15 s bite, then the winch hauls the DD in along the line at **450 u/s**, bow held on the anchor. It stops when the DD's centre is **250 u off the anchor's hull**: 320 u from the Lancer's keel, 340 u from the Drake's, 400 u from a pylon, 580 u from the base, 296 u from a dummy. From 700 u to the Lancer takes about **1.0 s**. <br>When the line goes taut, the inward speed drops to zero. The sideways speed is kept, capped at the DD's top (117). <br>Pressed already inside that distance: no pull, and the swing starts at the line it has (never under HitRadius + 150) |
| **SWING** | v2, for the rest of the **5 s** (counted from the press): <br>• A/D fly round the orbit, capped at the DD's top; <br>• W/S reel the line in or out at 100 u/s, between HitRadius + 150 and 700; <br>• the director stays on the anchor; <br>• a Lance runs down the line. <br>At 320 u round the Lancer, 117 u/s is 0.37 rad/s. The Lancer turns at 0.30 rad/s at L1, so the DD can stay off its bow. <br>**Only to L20.** A boss's turn quickens 1% a level (`Missions.Quicken`, Boss.cs:216), so at 320 u the Lancer matches the DD at **L21** (0.366 rad/s). v2's 130 u/s held to L31; the speed cut costs 10 levels. Past L20 the DD reels in (W) toward HitRadius + 150 = 220 u, which is 0.53 rad/s but inside the 340 u wave, or fits Engine chips (+6% top each) |
| **RIP** (cast-off, visual only) | The hook tears free and takes a chunk of hull with it. No damage, no change to the hitbox, nothing in play. The rows are below |
| **TOW / HURL** (the target is a craft) | v2 unchanged: reeled to 160 u off the bow in 0.5 s, hauled up to 3 s, then hurled at 600 u/s up to 900 u, 60 to each body (120 if a heavy is thrown). **No rip on a craft** |
| **casts off** | a second E · 5 s after the press · the anchor dies or warps · a web lands · the DD warps (V). An anchor that dies or warps leaves no rip |

**The rip, as rows.** `FxShape.Debris` is a new Fx shape.

| row | what | numbers |
|---|---|---|
| `rip` | A chunk of the anchor's **own** hull. A jagged shard outline (the `Shots` Shard shapes) is filled with the anchor's texture, cut at the hook point, so no new art is needed. It flies off toward the DD, then tumbles away | size 16% of the anchor's length (Lancer 58 u, Drake 67 u, pylon 35 u, base 90 u, dummy 15 u) · 260 u/s, within ±25° of the line · spins 3-6 rad/s · drag brings it to rest in 2 s · fades over its last 1.0 s · lives 3.0 s |
| `rip_sparks` | hot orange sparks from the wound | 28 over 0.5 s · 120-300 u/s · each lives 0.4-0.7 s |
| `rip_smoke` | grey puffs trailing the chunk | 6 · each lives 1.2 s |
| `scar` | a dark jagged patch where the chunk came from. It is drawn in the anchor's own frame, so it turns with the hull | the chunk's size · 10 s, fading over the last 3 s · at most 3 on one anchor (the oldest goes) |
| `grapnel_rip` | a tearing-metal sound, pitched down on bosses | a `tools/make_sounds.py` row |

- **How every peer sees it:** the host raises it once through the existing NetFx (At = the hook point, To = the DD, Anchor = the anchor's NetId, Size = the chunk). The tumble is seeded from the NetId and the raise count, so every peer draws the same chunk. **No new RPC.**
- **"Fake" is the default: the rip has no effect on play.** The small effect I would argue for, and did not take: 30 damage per rip (x2 on structures) = +1.4 DPS. You said fake.

**Power.** Unchanged at 55 realistic.
- The pull closes the gap the speed cut opens. It also shortens the Lance's run to the anchor: 320 u is 1.9 s at 170 u/s.
- Hull lost against the Lancer goes from 0.66 to 0.70. At 320 u the DD sits inside the Lancer's 340 u wave for about 24% of the fight.

**Counterplay:** the pull lands it inside the Lancer's wave ring · a web refuses the pull (WEBBED) · the rip tells everyone the swing is over.

**Engine.**
- The F8 swing gains a pull phase, owner-side. The host confirms through `Sl("grapnel")`, as in v2.
- Rows: grapnel_bite 0.15, grapnel_pull 450, grapnel_stop 250.
- F9: `FxShape.Debris` plus the rows rip, rip_sparks, rip_smoke and scar.

**Proves.**
- **Pull:** from VaryNear 400 u and 699 u, at VaryAngle, on each of {Lancer, Drake, pylon, base, dummy}. The centre ends 250 ± 10 u off the hull within (distance − stop) / 450 + 0.15 ± 0.05 s, with the bow within 5°.
- A press already inside the stop moves the DD no nearer.
- **Rip:** exactly one raise per cast-off, with Anchor = the anchor's id and Size = 0.16 x its length. None on a craft, and none when the anchor dies. The anchor's hull is the same before and after the rip (director held).
- **Rung 4:** one frame with the chunk mid-tumble and the scar.
- **Rung 5:** a guest draws the chunk within 20 u of the host's.

### 3.3 FREIGHTER · the sentry throw (R) and Unmask (F)

| piece | numbers | counterplay |
|---|---|---|
| **Sentry** (R) | **R with the cursor on open space:** a sentry is thrown from the hull to the cursor, up to **600 u** (further is clamped to 600 u on the same bearing), in **0.8 s**. It unfolds and fires from the moment it lands. In flight it cannot be hit and cannot fire. <br>**R with the cursor within 60 u of one of yours:** it is **recalled**. It lifts off, flies back in 0.8 s and is stowed with the hull it had. <br>The v1 numbers stay: 3 out, 6 s between throws (a recall is free), 120 hull, 650 u, 10 DPS each, and the paint rule. <br>**Refused:** ALL OUT (3 out), RELOADING (the 6 s clock) | raiders go for sentries · a boss warps out of their reach · in flight for 0.8 s |
| **Unmask** (F) | Two hidden mounts open in the hull's flanks for **8 s**: 0.3 s to rise, 0.3 s to fold. <br>• Each fires 7.5 every 0.5 s = **15 DPS**, reaches 800 u (the spotter's reach), with spotter shells at 560 u/s. <br>• They shoot the painted target when it is in reach. Otherwise they take light craft and fighters, then heavies, then the nearest body. By default never a missile (decision 13), which keeps them apart from CIWS and the PD. <br>• **Cooldown 24 s from the press** = 10.0 DPS sheet, the same as Time on target, and about 8 realistic. Never refused | up 8 s of every 24 · 800 u · the paint lapses 5 s after the last spotter hit |

- **Why throw the sentries.** A sentry dropped under the hull sits 800 u from what the spotter paints, outside its own 650 u reach. Thrown 600 u toward the target, it is in reach. That lifts the realistic sentry uptime from 80% to 85%.
- **On screen:** the hull panels slide back and two turrets rise. Every peer draws them from the slot's time left; the shells travel by NetShot.

**Engine.**
- **Sentry throw:** F14 gains launch and recall. The press payload carries the cursor point (F8). The new rows deploy_reach 600, deploy_flight 0.8 and recall_pick 60 replace collect_range 120.
- **Unmask:** two new `ClassArt.Hidden` mount seats, and `TurretSpec.While = "unmask"`, meaning a mount that is live only while a named slot runs. Its TargetFilter is the sentries' with missiles taken out, and the paint as Prefer.
- Rows: unmask_count 2, unmask_damage 7.5, unmask_interval 0.5, unmask_time 8, unmask_cooldown 24.
- tot_* leaves.
- **The sprite batch must mark the 2 flank seats** on the freighter's new hull art.

**Note: the live code has moved since v2.** Since 8fddb84, sentries take bosses and heavies with no paint (`Targeting.Sentry`). That was built from triage Q6 ("may deployed turrets shoot bosses, heavies and pylons on their own?": yes). The approved v2 card gates a boss, pylon or base on the paint. By default the card wins and the FR slice puts the gate back. Because that reverses something already built, it is decision 14. Solo it changes nothing: the spotter paints the boss anyway.

**Proves.**
- **Throw:** a cursor at VaryNear 200 u, 599 u and 800 u, at VaryAngle, lands the sentry within 10 u of the point (of the 600 u clamp for 800 u) at 0.8 ± 0.05 s.
- A cursor 59 u from a sentry recalls it; at 61 u, R throws a new one.
- A 4th throw with 3 out is refused. A throw inside the 6 s clock is refused. A recall is never refused.
- A recalled sentry keeps its hull.
- **Unmask:** a painted dummy at 790 u takes 30 DPS ± 5% over 8 s. The mounts are gone at 8.1 s, and the cooldown is 24 s from the press. Unpainted, the mounts take a webifier ahead of a gunship, and never fire on a seeker at 400 u.
- **Rung 5:** a guest sees the throw land on the host's spot and the mounts rise.

### 3.4 SNIPER · the v1 Anchor, and a new piece where Designate was

**Anchor (F), v1 exactly.**
- Up to 8 s rooted.
- Charge x2.5.
- Reach x1.4 = 3500 u.
- 0.3 s to release.
- Cooldown 12 s from release.
- The v2 x1.25 and the 8 s cooldown are deleted.
- On its own, this puts the Sniper at 36.3 sheet and 34 realistic, an 88 s kill.

**The three candidates.** Each one:
- replaces Designate's DPS;
- is a passive on the railgun, so there is no key conflict;
- is useful solo;
- is not a missile or a torpedo;
- is tuned here to about the median, 51.

| candidate | verb | what it does | realistic (kill @ 3000) | overlap check | risk |
|---|---|---|---|---|---|
| **Overcharge** (default) | charges | Hold past full and the line keeps charging. Damage rises from 45 at full (1.6 s) to **99 (x2.2) at 0.8 s past full**, then holds. Anything that speeds the charge speeds this too: anchored, full is 0.64 s and 99 is 0.96 s | **51.0** (59 s) | none. It is a longer hold, not a new effect. The Dart's rod already shares "a piercing line", and this does not widen that | none on the wire: the hold time decides it |
| Fracture | pierces | Every full line that passes through a body cracks it for 6 s. The Sniper's own next full lines through it deal +18% per crack, up to 3 cracks (+54%) | 51.1 (59 s) | **stacks on a target, like the Wraith's Venom** | none |
| Deadeye | charges | Release within 0.25 s of full: x1.7 (76.5). Released later: x1.0 | 51.4 at 75% perfect releases (58 s) | none | **the wire:** the host judges the hold from the trigger at 20 Hz, so a guest's window is ±50 ms ragged |

None of the three is about *planting*. The Anchor is the Sniper's planting piece, and you said to leave it alone.

**Overcharge, the card.**

| piece | numbers | counterplay |
|---|---|---|
| **Overcharge** (passive) | Rail hold → damage: under 1.6 s it is a tap (8, first body); at 1.6 s, 45; at 2.0 s, 72; at 2.4 s and after, **99**, along the whole 2500 x 14 u line through everything (3500 u anchored). <br>Charge-rate lifts are additive, as ruled. Anchored (x2.5) it reaches 99 at 0.96 s. Anchored under a Tender's Overdrive (x3.0) it reaches 99 at 0.8 s. <br>DPS: 38.1 unanchored (99 every 2.6 s), 85.3 anchored (99 every 1.16 s). Anchored 39% of the time: **56.7 sheet, 51.0 realistic** | a full overcharge is a 2.4 s hold (0.96 s anchored) down a line anything can step off · it does not change the line's 14 u width |

- **On screen:** the charge meter fills blue to full, then white past full on a second bar. The muzzle glow grows, and at 99 there is a click. An overcharged line is drawn 1.5x wide; the hitbox stays 14 u.
- **Other peers:** they see the glow grow from the trigger bit.
- **Engine:** F7's Charge primary becomes a list of band rows {hold, damage, kind}: {0.3, 8, first body}, {1.6, 45, line}, {2.4, 99, line}, interpolated between the last two. New rows: rail_over_time 0.8 (shortened by rate lifts) and rail_over_mult 2.2.
- **Proves:**
  - Holds of {1.59, 1.6, 2.0, 2.4, 3.0 s} on a parked dummy give {8, 45, 72, 99, 99} ± 0.5.
  - Anchored holds of {0.64, 0.8, 0.96 s} give {45, 72, 99}.
  - Anchored under Overdrive, 99 at 0.8 s.
  - Three dummies on a VaryAngle line each take 99.
  - The Anchor's literals, back to v1: cooldown 12.0 s, reach 3500 u.

### 3.5 WARDEN · Taunt (Q), the Beacon renamed, with a guard

| piece | numbers | counterplay |
|---|---|---|
| **Taunt** (Q) | v2's Beacon, renamed: <br>• for **6 s**, raiders standing within 1000 u, or hunting something within 1000 u, take the Warden as their target (`Raider.Call`); <br>• webbers let go and post on the Warden; <br>• heavies waiting at the edge boost in; <br>• emplacement guns prefer the Warden; <br>• called craft take **x1.5** from the Warden. <br>**New: for the same 6 s the Warden takes 33% less from everything.** Every blow is x0.67: bosses, raiders, missiles, rings. <br>**Cooldown 20 s** | bosses never answer the pull, but the guard still works on their hits · 6 s of every 20 · it still gets webbed, lasered and put in a missile's lane |

**On screen, on every peer.** Hardened is a wire bit, so everyone sees it.
- A hex-plate shimmer over the hull.
- A **−33%** tag under the hull, for the 6 s.
- The slot reads `TAUNT 4.2 s  −33%`.
- A 1000 u ring flashes on the press.

**What the guard buys.**
- A full L1 raid now does 13.5 DPS to the Warden (twin lasers at 2.58, §3.7). Under Taunt that drops to 9.0, so **each Taunt saves 27 hull**.
- The guard covers 6 s of every 20. A raid that stays on the Warden, with Taunt pressed on cooldown, kills 270 hull in about **22 s**. In v2 it was 25 s, at the old 1.25 lasers and with no guard: the doubled heavy lasers cost more than the guard gives back. The draft's "30 s" assumed the guard never lapsed.
- A full burn (250) lands 167.5, so the Warden survives it with **102** left (v2: 20). The rock is the same.

**Engine.**
- F2: the applier sets the share, so Taunt applies `StatusSet.Apply(Hardened, 6, 0.67)`.
- The beacon_* rows are renamed taunt_*: taunt_time 6, taunt_reach 1000, taunt_mult 1.5, taunt_cooldown 20, plus the new taunt_guard 0.67.

**Proves.**
- A 30-damage blow lands 20.1 at 0.5 s and at 5.9 s, and 30 at 6.1 s.
- A beam tick of 50 lands 33.5.
- The v2 Beacon checks, under the new name.
- **Rung 4:** the shimmer and the tag.
- **Rung 5:** a guest sees the Warden Hardened.

### 3.6 DART · Ramjet (Q), replacing Jetwash

| piece | numbers | counterplay |
|---|---|---|
| **Ramjet** (Q) | **Lit for 8 s.** While the Dart flies at full speed (within 5% of its current top), top speed rises **+10% of the sheet top every second**, up to **+50%**: 260 → 390 u/s after 5 s. <br>**Turning bleeds it:** −20% of top per second at full yaw rate, less in proportion to a gentler turn. The build keeps running against it, so at full throttle with full rudder the net is −10% a second. A Slingshot is a snap, not a turn, so it keeps the bonus. A warp keeps it too, but the jump sets speed to 0 (PlayerShip.cs:795), so the build pauses until the Dart is back within 5% of top. <br>After 8 s the bonus runs out over 1.0 s. **Cooldown 20 s from the press.** <br>**Everything priced from top speed reads it:** <br>• Pepperbox: 7.5 → **11.25** a missile at +50%, its cap. <br>• The rod: a sprint forces full throttle, so a sprint with the Ramjet lit builds about +30% on its own (3 s). The rod goes 249.2 → **303.2** at +30% and **339.2** at +50% (top 490), under its 360 cap. <br>• Slipstream (x0.7 damage taken at 325 u/s or more) switches on at +25%. <br>**Authority:** the host prices from its own copy. It runs the same bonus from the ship's replicated velocity and heading (20 Hz), so no bonus the ship reports is trusted. It clamps the yaw it measures to the hull's turn rate, and it skips a report in which a Slingshot or a jump lands, so a snap never reads as a turn. The owner runs its own copy for flight | straight lines are predictable · a web drops it under full speed, so it stops building · a turning fight bleeds it · 8 s of every 20 |

- **Power:** +3.4 realistic from the Pepperbox and +2.3 from the rod → **66.8** (v2: 61), a 45 s kill. Lights stay on top.
- **Engine:** F1 gains one new kind of lift, the **Ramp**: a lift whose size is a running total, with a build rate while a condition holds, a bleed per rad/s of yaw, and a cap. Rows: ramjet_time 8, ramjet_build 0.10, ramjet_cap 0.50, ramjet_bleed 0.20, ramjet_cooldown 20.
- **Leaves:** Jetwash, and with it the F9 path zone, Status.Slowed and Raider's speed share.
- **Gear** (wash_* becomes ramjet_*): rol_racing → ramjet_cap +0.30 / ramjet_build −0.15; rol_quick → sling_cooldown +0.50 / ramjet_cooldown −0.25.

**Proves.**
- Flying straight at full throttle, top after {1, 3, 5, 7 s} lit is {286, 338, 390, 390} ± 3.
- Then 1 s of full rudder at full throttle: 390 → 364.
- A 180° Slingshot: top is unchanged, on the owner and on the host's copy.
- A warp at +40%: top is still +40% after the jump, and it builds again once the Dart is back at top.
- Webbed: no build.
- At +50%, Pepperbox missiles deal 11.25, and a rod fired at the end of a lit sprint deals 339.2.
- The bonus is 0 by 9.0 s. The cooldown is 20 s from the press.
- **Rung 5:** the host's rod price for a guest Dart matches the guest's own figure to within 1%.

### 3.7 ENEMY HEAVIES · twin laser at 2.58

- **The lasers.** Each barrel fires at 1.25x the light mean (webifier 1.0, talon 1.4, pod 0.7; mean 1.033), so 1.29 per barrel and **2.58 per heavy**, on the gunship, the cross and the lancerkin.
- **Unchanged:** the missile stays 35 with a 10 s flight, and heavies never web.
- **Knock-on effects on the cards:**
  - **DD Suppress** saves about **38 hull** per raid press (v2: 30). The laser drops 2.58 → 1.29 for 9 s, which saves 11.6, and the held missile saves 26. The Suppress check's literal becomes "gunship 2.58 → 1.29".
  - **Warden:** a full L1 raid now does 13.5 DPS to it (§3.5).
- **Rows:** F20 gives each heavy row 2 barrels at 1.29 each.

**For `raids_v2.md` section 3.** `raids_v2_model.fight` was re-run with heavy DPS 2.58 and heavy_web=999. The v2 column (in brackets) reproduces the current section-3 table to within 0.01.

| L | adds | x boss RUSTY (was) | x boss DRAKE (was) | pinned | fight +% | adds' own DPS /S |
|---|---|---|---|---|---|---|
| 1-5 | 0+0 | x1.00 (x1.00) | x1.00 (x1.00) | 0% | +0% | 0.00 |
| 6-8 | 1+0 | x1.03 (x1.01) | x1.06 (x1.03) | 0% | +4% | 0.22 |
| 9-11 | 1+1 | x1.15 (x1.13) | x1.31 (x1.26) | 8% | +9% | 0.51 |
| 12-14 | 1+2 | x1.18 (x1.15) | x1.38 (x1.32) | 9% | +11% | 0.62 |
| 15-17 | 1+3 | x1.21 (x1.18) | x1.44 (x1.38) | 10% | +12% | 0.75 |
| 18-19 | 2+3 | x1.22 (x1.18) | x1.46 (x1.38) | 10% | +16% | 0.88 |
| 20 | 2+3 | - | x1.46 (x1.38) | 10% | +16% | 0.87 |
| 21-23 | 2+4 | x1.27 (x1.23) | x1.56 (x1.48) | 13% | +17% | 0.94 |
| 24-26 | 2+5 | x1.28 (x1.24) | x1.58 (x1.51) | 14% | +18% | 1.00 |
| 27-29 | 2+6 | x1.30 (x1.26) | x1.62 (x1.53) | 14% | +19% | 1.08 |
| 30-32 | 3+6 | x1.33 (x1.27) | x1.69 (x1.57) | 14% | +26% | 1.42 |
| 33-35 | 3+7 | x1.40 (x1.34) | x1.84 (x1.71) | 19% | +30% | 1.54 |
| 36-38 | 3+8 | x1.44 (x1.37) | x1.92 (x1.79) | 21% | +34% | 1.67 |
| 39-40 | 3+9 | x1.51 (x1.44) | x2.07 (x1.92) | 23% | +39% | 2.06 |

**Time to die**, boss alone → with adds. The old with-adds figure is in brackets. Hull = stock x 1.64 x mH(L), with 0.5%/s regen. **Bold** = dies before the fight ends if it never mitigates.

| L (fight) | hull 120 | hull 200 | hull 420 |
|---|---|---|---|
| 6 DRAKE (62 s) | 72 → 67 s (69) | 158 → 144 s (151) | 2519 → 1435 s (1844) |
| 9 RUSTY (66 s) | 32 → **27 s** (27) | 59 → **49 s** (50) | 182 → 141 s (146) |
| 18 DRAKE (70 s) | 87 → **53 s** (56) | 206 → 106 s (115) | never → 538 s (665) |
| 20 DRAKE (70 s) | 88 → **53 s** (57) | 209 → 108 s (117) | never → 554 s (688) |
| 30 DRAKE (75 s) | 77 → **39 s** (43) | 172 → 75 s (83) | 6505 → 270 s (324) |
| 39 RUSTY (83 s) | 25 → **16 s** (17) | 45 → **28 s** (29) | 127 → **69 s** (74) |
| 40 DRAKE (83 s) | 57 → **24 s** (26) | 118 → **44 s** (48) | 704 → 121 s (136) |

**What it means.**
- **The web is still the danger.** The pinned share is the same in every band (0-23%), because heavies bring no CC. The x-boss figure rises 0.02-0.15.
- **The adds' own guns** now reach 2.06/S at L39 (v2: about 1.5/S).
- **The worst case**, a pilot who ignores the adds: x5.09 at L36 (v2: x4.63).
- **Fight length hardly moves.** L39 with no refills is 78 s at 10.91/S (v2 10.50/S). With 20 s refills it is 88 s at 12.14/S (v2 11.57/S).
- **L30, hull 200:** it now dies at 75.3 s in a 75.1 s fight, so it survives by 0.2 s.
- **For the curve:** the hull factor 1/(1 + fight%) moves by at most 1 point per band.

### 3.8 CHIPS

- **Default reading: two caps.** Slots stay 3 / 4 / 5 / 6. At most **3 Combat** chips and at most **3 utility** chips (Engine, Armour, Targeting) on any hull.

  | tier | slots | what a full hull can hold |
  |---|---|---|
  | capital | 3 | any split |
  | freighter | 4 | 3 + 1 to 1 + 3 |
  | heavy | 5 | 3 + 2 or 2 + 3 |
  | light | 6 | exactly 3 + 3 |

  Everyone's combat lift is capped at the same 3 chips. That closes the x1.48 vs x1.24 gap.
- **The other reading:** 6 slots on every hull, exactly 3 combat + 3 utility. That drops the 3/4/5/6 ladder you chose to keep.
- **The Basic Combat Chip counts as combat**, because it adds damage. So the starting kit is **3** basic chips, not 5 (`Equipment.Default`, Equipment.cs:412).
- A basic chip is a kit part (`Kit = true`), so on an old save the 4th and 5th are emptied, not moved to the hold. They cost nothing.
- This matches the curve's 3820. It assumed 3 kit chips (x1.15) on every class.
- `level_gates.md` (same folder) proposes baking the kit chips into each class's base. If you take it, this line becomes "the x1.15 is in the base", and the combat cap counts only chips you find.
- **Engine:** F15 gives each chip part a `ChipKind` {Combat, Utility}. Sanitize moves anything over a cap to the hold and never deletes it. The window refuses a 4th of a kind.
- **Proves:**
  - A light loaded with 4 Combat chips keeps 3; the 4th is in the hold.
  - A light with 4 Engine chips keeps 3.
  - A capital holds 3 Combat.
  - The starting kit has 3 basic chips.
  - **Rung 5:** the host sanitises a guest's claim of 4.
  - **Rung 4:** the slot layouts.

### 3.9 CAPITALS AND WARP

**Capital speed cut.** Thrust is cut by the same share as top speed, so each ship takes as long to reach its top. Turn rates are unchanged.

| hull | top (u/s) | thrust | reverse thrust | reverse top | turn |
|---|---|---|---|---|---|
| BATTLESHIP | 104 → **88** (−15%) | 56 → 47 | 24 → 20 | 36 → 30 | 1.08 |
| CARRIER | 116.48 → **99** (−15%) | 58.2 → 49.5 | 24.3 → 20.6 | 38.8 → 33 | 0.9 |
| DESTROYER | 130 → **117** (−10%) | 70 → 63 | 30 → 27 | 45 → 40.5 | 1.2 (the v1 kit's; live 1.08) |

- The carrier's thrust, reverse thrust and reverse top are fractions of `CarrierTop` (Ships.cs:186: top / 2, top x 5/24, top / 3), so moving that one constant moves all four. The v2 card's "thrust 48" was stale; the source is 58.2.
- The DD stays the fastest of the line (117 > 99 > 88). Freighters stay at 85.

**Warp today** (PlayerShip.cs:735-796, Hub.cs:1705; line numbers from the working tree during this review).
- V on every class. It charges for 3 s, then jumps 1200 u along the bow. If a target or waypoint lies within 45° of the bow, it jumps toward it and stops short of it.
- Cooldown 30 s, from the jump. The jump sets speed to 0.
- Nothing refuses it but death and the cooldown (`CanWarp`): it works webbed, or Disabled by the old railgun's charge.
- Other peers see the charge glow (the `warping` bit) and snap across the jump.

**Warp v3: hold to charge, release to jump.** It applies to every class.

| | capitals and freighters (the six slow hulls) | heavies and lights |
|---|---|---|
| spool (nothing yet) | 1.0 s | 1.0 s |
| range grows at | **800 u/s** | 600 u/s |
| **safe limit** | **2400 u**, reached after a 4.0 s hold | **1200 u**, reached after a 3.0 s hold (today's 3 s) |
| overshoot | up to +900 u → 3300 u after a 5.1 s hold | up to +900 u → 2100 u after a 4.5 s hold |
| cooldown (from the jump) | **20 s** | 30 s |
| effective long-haul speed | BB 188 · CV 199 · DD 217 · FR 185 u/s (today: 140 · 152 · 166 · 121) | heavy 226 · light 296 (as today) |

- **Hold V.**
  - For the first 1.0 s nothing happens. Release in that time and the warp is cancelled with no cooldown.
  - After that, the range grows at the rate above. It stops at the safe limit + 900 and holds there while V is held.
  - You can steer and thrust while charging, as today.
  - **Refused while anchored** (ANCHORED): the charge starts after the Anchor's 0.3 s release (see the table below).
- **Release V.**
  - The ship jumps the charged distance along the bow.
  - If a target or waypoint lies within 45° of the bow, it jumps toward it instead, and stops short of it as today. So a jump can be shorter than the charge.
  - **The penalty is priced on the jump, not the charge.** So a charge held at the top and aimed at a target or waypoint inside the safe limit lands free. A held V is a ready jump toward what is selected, never a free long hop (risk 5).
- **Overshoot (your rule).** Past the safe limit the ship lands **disabled for 2 s per 300 u over**, at most 900 u over, which is **6 s**. The penalty is proportional: 150 u over is 1 s.
- **Disabled means** no thrust, no turning, no weapons, no abilities and no warp. Passive PD and craft already out keep working. It still takes damage. This is Status.Disabled, a wire bit, so every peer sees a dark, sparking hull.
- **Why the slow hulls get the longer row.** They are the ones you asked to lean on the warp: at 85-117 u/s, a 2400 u hop every 20 s beats flying.
  - The overshoot rule favours them too. Out of combat a battleship's full 3300 u hop nets 198 u/s against 188 safe, because 6 s disabled costs it only 528 u of flying.
  - The same hop loses a light ground: 276 against 296 u/s.
  - In a fight, 6 s dead in the water is the price. The carrier's wings and the freighter's sentries fight on through it, so for those two an overshoot costs only the helm and new orders (decision 8).
  - Every jump sets speed to 0, so these figures run 2-6 u/s high: that is the time spent getting back up to top.

**Warp against every class's moves.**

| move | what the v3 warp does to it | rule |
|---|---|---|
| **Slingshot** (Dart E) | It snaps the bow, so mid-charge it re-aims the jump at once | intended. The host's Ramjet copy never reads the snap as a turn (§3.6) |
| **Rod sprint** (Dart F) | The jump zeroes speed. The sprint's forced throttle runs on, and the rod still leaves at 3.0 s from wherever the Dart is. Its price reads top speed, not speed, so a jump neither raises nor lowers it | none |
| **Ramjet** (Dart Q) | A jump is not a turn, so the bonus is kept. Speed 0 pauses the build | none |
| **Rewind** (Echo Q) | Rewinding 3 s can carry the Echo back across a jump it just made (warp in, fire, Rewind out). That is one report's snap of up to 780 u of flight plus the jump: up to about 2900 u | the host never prices a snap it confirmed as a Rewind. Disabled refuses Rewind, so it cannot undo an overshoot |
| **Shadow step** (Wraith E) | A blink of about 1000 u (900 + 140) in one report | as Rewind: confirmed, never priced |
| **Lunge** (Warrior E) | 420 u in 0.3 s is about 70 u a report, never a snap | none |
| **Grapnel** (DD E) | A DD warp casts off (v2). The pull is 22 u a report. The aimed jump is the other way to close on a hostile (§6) | none |
| **Anchor** (Sniper F) | Today a jump would carry an anchored Sniper to a new spot with the anchor's lifts on: a free re-plant that dodges its "sitting target" counterplay | **V is refused while anchored.** The Anchor itself is untouched |
| **webs** (every class) | A jump leaves the webber's post: a raider is latched only within 12 u of its post (Raider.cs:260), so the web drops and it re-posts at cruise. A pinned ship cannot turn, so it jumps down its frozen bow | today's behaviour. v3 makes it quicker on the slow hulls: 1200 u after 2.5 s, every 20 s (§6) |
| **the Drake's warp** | A boss move, not the drive: `Warp` seconds and a landing ring on the move row (Boss.cs:84-90; Drake.cs:44, :68). A Grapnel on a Drake that warps casts off with no rip. A pilot's landing ghost aimed at the Drake follows it live | Warp.cs's header says the boss opener is a Boss move row, so the next instance does not merge the two |

**The range display.** It is drawn only for the pilot, in world space, while V is held.
1. A faint **safe ring** at the safe limit, shown from the press.
2. The **charge ring**, growing from the hull in the accent colour.
3. Past the safe ring, the band out to +900 is tinted amber to red, with tick rings at **+300 / +600 / +900** labelled **2 s / 4 s / 6 s**. Once the charge crosses the safe ring, the charge ring takes that colour.
4. A **landing ghost**: the hull's outline where it will land, with a thin line to it. It sits on the bow line, or short of the target if one is aimed at.
5. A readout by the ghost: `2400 u`, or `+420 u · DISABLED 2.8 s`.
6. The hull bar reads `WARP 1850 u` while charging, and `DISABLED 2.8 s` after an overshoot.

Other peers see today's charge glow and, after an overshoot, the disabled hull.

**Authority.** No new RPC.
- The jump stays the owner's, as today.
- **The owner locks itself at once.** The host applies Status.Disabled from the jump it measures: the displacement across one report, minus the safe limit from its own copy of the ship's sheet.
- **The host prices every jump it sees, not only the ones the bit brackets.** That means the report where `warping` falls, and also any snap over 600 u (the snap it already detects, PlayerShip.cs:1228). So a guest that clears the bit a report early, or never sets it, buys nothing.
- **Two snaps are never priced:** a Rewind and a Shadow step the host confirmed. The owner keeps 0.1 s between a jump and either of them, both ways round, so no report carries two.

**Engine: a new table, `Warp.cs`** (see F21 in §5).
- `WarpDrive` holds the spool, the charge, the landing rule (today's `WarpArrival`, `WarpCone`, `WarpStandoff`), the penalty rule and the ring draw. The title screen's dodging battleship uses the same drive: it holds V until the charge reads 500 u (1.6 s on the battleship's row), so there is no second warp. Its `DodgeAt` timing (MainMenu.cs:35) is re-derived from that hold.
- The header names what is NOT this drive: the Drake's warp opener, a Boss move row (Boss.cs:84-90).
- Rows `warp_safe`, `warp_rate`, `warp_cooldown` go on the sheet (defaults 1200 / 600 / 30), with overrides on the six slow hulls (2400 / 800 / 20). They are stat rows, so gear can move them later.
- The overshoot numbers stay constants in Warp.cs, proved by literals: 1.0 s spool, 300 u per 2 s, 900 u cap.
- **Deleted:** PlayerShip's warp block, `WarpWarmup`, `WarpCooldown`, `WarpRange`, `WarpHop` and `WarpEvery` (the menu's hop and clock become a scripted hold and a row override).
- **Callers to fix in the same edit.** The numbers are the working tree's during this review; another job is editing these files, so find them by name (`StartWarp|WarpWarmup|WarpStandoff|WarpHop|WarpEvery|_warpCd|_warpFlash|Warping`):
  - SmokeTest.cs.txt :691-692 (the menu's 500 u hop), :715-803 (the menu's dodge), :2722-2807 (the warp section, which reads `WarpStandoff` at :2742 and :2804) and :3539 (the snap flash);
  - Shots.cs.txt :239-245 (the warp frames, plus one new frame for an overshoot);
  - HubNodes.cs:120-122 (the hull bar's WARP readout reads `WarpWarmupLeft` and `WarpCooldownLeft`);
  - MainMenu.cs :23, :35, :140-141, :259-265;
  - the "warp" row in Hints.cs:27.

**Proves.**
- **Holds on a capital:** {0.9, 2.0, 4.0, 5.2, 6.0 s} jump {cancelled with no cooldown, 800, 2400, 3300, 3300} u ± 20, along the bow at VaryAngle headings. A heavy at 3.0 s jumps 1200.
- **Aimed:** a target within 45° ends short of it (today's check), and the jump is shorter than the charge.
- **Overshoot:** {0, 150, 300, 600, 900} u over → disabled {0, 1, 2, 4, 6} s ± 0.1. While disabled there is no thrust, turn, fire, ability or warp, and PD still fires.
- **Cooldown:** 20 s and 30 s, by row.
- **Held at the top:** a capital held 8 s jumps 3300 u; the same hold aimed at a target 1500 u off within 45° lands short of it with no penalty.
- **Anchored:** V is refused ANCHORED. 0.3 s after the release, it charges. (None of Anchor, Rewind or Shadow step exists in the live code, so this line and the Rewind line below are proved in 6c and 6d, not by lane B.)
- **Webbed:** a pinned capital that jumps 1200 u is free of Pinned within 0.3 s.
- **Speeds:** BB 88, CV 99, DD 117, and DD > CV > BB. The carrier's thrust is 49.5.
- **Rung 4:** the ring at the safe limit, the amber band and ticks, the ghost, and the readout.
- **Rung 5:**
  - A guest overshoots 600 u. The host and the other guest see it Disabled for 4 s, even with the guest's own lock bypassed.
  - The same guest clears `warping` one report before a 2100 u jump: still Disabled 6 s on the host.
  - A guest Echo that Rewinds across its own 1200 u jump is never Disabled.

---

## 4 · POWER CHECK: one target, a lone boss, stock, L1

The method is v2's. "Sheet" is maximum uptime; "realistic" is the uptime a pilot actually holds. Two kill columns:
- **@ 3000:** stock, v2's basis.
- **@ 3820 / 3520:** the curve you signed. It assumes 3 kit chips and 1 Weapons point (x1.18), and is built so the median class kills in about 60 s.

"Hull lost" is the share of hull today's boss takes over the class's own kill, after 0.5%/s regen.

| class | tier | hull | sheet | realistic (v2) | kill @ 3000 | kill @ 3820 / 3520 | hull lost: Lancer / Drake |
|---|---|---|---|---|---|---|---|
| BATTLESHIP | capital | 500 | 52.3 | 46.0 | 65 s | 70 / 65 s | 0.11 / 0.18 |
| TENDER | freighter | 380 | 48.4 | 46.0 | 65 s | 70 / 65 s | 0.82 / 0.34 |
| BASTION | freighter | 420 | 55.0 | 47.0 | 64 s | 69 / 63 s | 0.19 / 0.27 |
| **WARDEN** | heavy | 270 | 53.0 | 49.0 (49) | 61 s | 66 / 61 s | **1.06** / 0.49 (v2 1.21 / 0.58) |
| WARRIOR | heavy | 300 | 74.7 | 50.0 | 60 s | 65 / 60 s | 1.30 / 0.48 |
| **SNIPER** | heavy | 240 | 56.7 | **51.0** (43) | 59 s | 63 / 58 s | 0.17 / 0.37 |
| **CARRIER** | capital | 425 | 64.1 | **52.6** (50) | 57 s | 62 / 57 s | 0.28 / 0.12 |
| **DESTROYER** | capital | 395 | 61.7 | 55.0 (55) | 55 s | 59 / 54 s | 0.70 / 0.27 |
| **FREIGHTER** | freighter | 450 | 65.0 | **56.1** (53.5) | 53 s | 58 / 53 s | 0.53 / 0.20 |
| WRAITH | light | 220 | 77.1 | 64.0 | 47 s | 51 / 47 s | 1.48 / 0.60 |
| **DART** | light | 200 | 80.8 | **66.8** (61) | 45 s | 48 / 45 s | 1.21 / 0.61 |
| ECHO | light | 180 | 77.4 | 72.0 | 42 s | 45 / 41 s | 1.34 / 0.70 |

**Summary.**
- **The spread.** Median realistic **51.8** (v2 50), mean 54.6 (v2 53). Spread 46-72 = **1.57x** (v2 1.67x).
- **Lights stay on top.** The lowest light is the Wraith at 64. The highest non-light is now the **Freighter at 56.1** (v2: the DD at 55).
- **The Sniper** goes from last (43) to the median (51.0). The Anchor is untouched; the gain is all Overcharge.
- **The Carrier** goes to 52.6, just above the median. A toggle would have put it at 61.7.
- **The Freighter** rises +2.6: Unmask's 8.1 against Time on target's 7, plus thrown sentries staying in reach.
- **The Dart** rises +5.8 from Ramjet pricing. It is still under the Echo.
- **The Warden** loses 12% less hull per fight under Taunt; its DPS is unchanged.
- **Assumptions behind the moved rows:**
  - Carrier: the boss is inside 600 u for 60% of the mode. That is generous for a standoff hull; at 30% the Carrier is 49.5 and the median 50.5, and lights stay on top either way.
  - Sniper: 0.90 realism (v1: 0.93), because the holds are longer.
  - Dart: an average bonus of +22% while lit, because turning bleeds it.
  - The rung 3 landed-DPS probe (H) replaces all of these.

---

## 5 · FOUNDATIONS: changes to v2 section 5

**Landed since v2** (commit 8fddb84):
- **F0**, honest stats, with rate and speed lifts adding (x2 with x2 is x3).
- **F3**, choosing vs hitting, and the solo boss freeze.

**Not landed:**
- **B**: Boss.cs:509 still reads `s.Next = m.Tick`.
- F2, F16, and everything from F4 on.

| id | foundation | v3 change | cost change |
|---|---|---|---|
| B | Burn clock | unchanged | XS |
| F0 | Honest stats | **landed** | — |
| F1 | Lifts, additive, + Add | **+ Ramp** (the Ramjet: a running total with build, bleed and cap). **− the anchored-line x1.25 lift** (the Anchor is back to v1) | ± 0 |
| F2 | Status registry | **− Slowed (512)**. Hardened now takes its share from the applier (Taunt 0.67; today one row, `rush_guard` 0.5, Statuses.cs:50). **Disabled on a pilot is the warp overshoot**: no helm, guns, abilities or warp; PD and craft stay on. **Trap:** today the only Disabled on a pilot is the old railgun's charge lock (PlayerShip.cs:598), so widening Disabled in slice 1 would also silence that ship's guns and abilities while it charges, until 6c deletes it. Slice 1 moves that lock to Lifts x0 (how the v1 Anchor is built) in the same edit. Unchanged: Suppressed 64, Dazzled 128 and Jammed 256 host-only; Parrying 32 on the wire | ± 0 |
| F3 | Choosing vs hitting | **landed** | — |
| F4 | Hostile damage door + credit | the Beacon's x1.5 becomes Taunt's · Time on target's credit is out · Unmask's mounts are credited as guns | ± 0 |
| F5 | Shot.Strike rewrite | unchanged (Time on target's per-call shells are gone) | ± 0 |
| F6 | Blasts | unchanged | ± 0 |
| F7 | Primaries | **+ Charge bands as rows** (tap / full / overcharge: the Sniper's Overcharge) · **+ `TurretSpec.While`** (a mount live only while a named slot runs) and **`ClassArt.Hidden`** seats (Unmask), with default flank seats so 6b never waits on the sprite batch | + S |
| F8 | Helm, wards, press payload | **+ the Grapnel pull** before the swing · **+ a point in the press payload** (the sentry throw) | + S |
| F9 | Fields, zones, marks | **− the path zone type**: it has no user left, now that the Smoke and then Jetwash are both gone · **− the speed share on Raider's speed lines** (the gravity well pulls; it never slowed) · **+ `FxShape.Debris`** and the rows rip, rip_sparks, rip_smoke and scar · + the Supercarrier ring, Taunt's shimmer and tag, the Unmask panels (all drawn from slots) · the beacon ring becomes the taunt ring | ± 0 (+S Debris, −S path zone) |
| F10 | Mend | unchanged | |
| F11 | Blow kind + Prism | unchanged | |
| F12 | Melee arc | unchanged | |
| F13 | Wing orbit | **widened**: Orbit flies round an **anchor** (a target, or the carrier), with an `EngageStat` and a TargetFilter per row · + the `Wings.All` rows `gunship` (2) then `patrol` (3) | + S |
| F14 | Owned bodies | the sentry's drop and pick-up become a **throw** (0.8 s, to a point ≤ 600 u) and a **recall** (cursor within 60 u) · Time on target is gone · `Raider.Call` serves Taunt | + S |
| F15 | Chips per class | **+ `ChipKind` caps**: at most 3 Combat and 3 utility; slots 3/4/5/6 kept; starting kit 3 basic chips | + XS |
| F16 | Passive PD | unchanged, not landed | |
| F17 | Outgoing door (OutGuards) | unchanged: still three rows. **The curve's DamageScale is one more factor inside `Boss.Out`**, so the curve lands after F17 | |
| F18 | OnDealt hook | unchanged | |
| F19 | Tow and hurl | unchanged (tow and hurl kept) | |
| F20 | Enemy rows | twin laser **2.58 per heavy** (2 barrels at 1.29) · MissileFlight as a row · **one edit with the raids batch's EnemyDef rows** (Cc, Exp) | ± 0 |
| **F21** | **Warp drive** | **NEW, `Warp.cs`**: hold to charge, the landing rule, the overshoot penalty (host-measured on every snap over 600 u except a confirmed Rewind or Shadow step), refused while anchored, the range ring, the rows warp_safe / warp_rate / warp_cooldown, and the title screen's ship on the same path. Deletes PlayerShip's warp block. The Drake's warp opener stays a Boss move row, named in the header | **M** |
| **F22** | **Capital helm rows** | **NEW**: BB, CV and DD top, thrust and reverse (the `CarrierTop` constant 116.48 → 99) | XS |
| — | Small rows | `Slot.At`, `AbilityDef.Forces`, `IRaidTarget.Calls` and `ShotDef.Command` are all kept | |
| H | Harness | + rows for the 9 new or changed pieces | + S |

**Dropped in v3, in one list:**
- Scramble: fighters skipping their rest, bombers skipping their rearm.
- Time on target: the tot_* rows.
- Jetwash: the wash_* rows, the path zone, Status.Slowed and the speed share.
- The Anchor's x1.25 lift and its 8 s cooldown.
- The Beacon's name (its rows become taunt_*).
- collect_range, replaced by recall_pick.
- `WarpWarmup`, `WarpRange` and `WarpHop`.

**Total:** about **133 S-units** (v2: 125). Warp is +2.5; the pull, the throw, the patrol wing, Unmask's mount, Overcharge, Debris and the harness are +1 each; Ramp, Taunt and the chip caps are small; Jetwash, Time on target and the Anchor lift come out. The review's additions (the railgun lock onto lifts, the host pricing every snap, V refused while anchored, default flank seats) are about +0.5 together, which stays inside "about 133".

---

## 6 · NEW OVERLAPS

| pair | verdict |
|---|---|
| WD Taunt's guard vs BB Brace | **Accepted, split by depth.** Brace is deep and short: x0.35 for 3 s at half speed, timed for a super. Taunt is shallow and long: x0.67 for 6 s, and it comes with pulling raiders onto you. The damage cuts are now Brace, Taunt, the Lunge's half, Slipstream and the Bubble. Each has its own condition, and only the Bubble covers anyone else |
| CV Supercarrier vs BB CIWS vs FR sentries | **Accepted, split by target.** CIWS fires only at missiles, lights and fighters within 460 u, for 6 s. The patrol takes any body except a missile within 600 u, for 20 s. The sentries are planted and follow the paint |
| FR Unmask vs BB CIWS | **Accepted, once Unmask skips missiles** (decision 13). Both are extra guns for a few seconds. CIWS works close and on PD targets only; Unmask follows the paint to 800 u, bosses included |
| CV Supercarrier vs FR Unmask | **Accepted, split by carrier and aim.** These are the closest pair: both are timed extra guns that choose their own targets. The patrol is craft flying a 600 u ring round the carrier, 20 s of every 50. Unmask is two hull mounts that follow the paint to 800 u, 8 s of every 24. Neither fires on a missile |
| CV Supercarrier vs CV Warp gunships | **Accepted, split by anchor.** Both are F13 orbits. The gunships go to chosen targets up to 3000 u out; the patrol never leaves the carrier's ring |
| FR sentry recall vs FR Redeploy | **Accepted, split by count and speed.** A recall stows one sentry and re-throws it on the 6 s clock. Redeploy lands all three round the freighter in 1 s |
| every class's warp vs the web-breakers (EC Rewind and EMP, WA Whirlwind, DD Grapnel on a pinner) | **Accepted.** A jump leaves the webber's post, so the warp already breaks a web on all 12. v3 makes it quicker on the six slow hulls (1200 u after 2.5 s, every 20 s instead of 3 s and 30 s). The raids model's pinned share does not count it, so that share is an upper bound |
| the aimed warp vs DD Grapnel pull | **Accepted.** Both close on a hostile. The jump stops 160 u off the nose and ends nothing. The pull starts inside 700 u and ends in the swing |
| DA Ramjet vs DA sprint vs WR Veil's x1.35 speed | **Accepted.** The sprint is a forced 3 s line that ends in the rod. The Ramjet is a slow build that rewards flying straight. The Veil's speed is a side effect of hiding |
| DD Grapnel pull vs WR Lunge / Shadow step vs DA Slingshot | **Accepted.** The pull is tied to a hostile, is slow to set up (0.15 s bite, then about 1 s), and ends in a swing, not in a strike |
| SN Fracture (if chosen) vs WR Venom | **Flagged.** Both build stacks on one target. This is why Overcharge is the default |
| **No class slows anything any more** | **Flagged.** Jetwash was the only one. The Curtain's note "Jetwash is the only slow" becomes "no class has a slow". The enemy web still slows pilots (Pinned: 20% of top, Statuses.cs:42), and the gravity well still pulls |

**Solo.** Every changed piece works for a pilot alone:
- the patrol guards the carrier;
- the pull and swing need only a boss;
- Unmask and the throw follow the freighter's own paint;
- Overcharge, the Ramjet and the warp are the pilot's own;
- Taunt pulls pinners off the hauler and guards the Warden itself.

---

## 7 · BUILD ORDER: the whole class batch

**Standing rules:**
- Kits go before items (your ruling).
- A writer works in its own git worktree.
- **Compile rungs (1-2) can run in every worktree at once:** `typecheck.ps1` keeps one scratch folder per checkout.
- **Engine rungs (3-6) run one at a time across all worktrees.** Every smoke run uses the one folder `%TEMP%\warships_smoke`, so a second run wipes the first.
- Each slice is committed with its own checks.
- Rung 6 runs once, at the end.

**Before anything:** the tree is clean. The in-flight jobs commit first: everything `git status` shows (about 30 modified scripts, the new Docks.cs and Landmarks.cs, the sounds, and both harness files) and the `net_tree` worktree.

### The lanes

| lane | what | starts after | files it owns | its rungs |
|---|---|---|---|---|
| **A · combat core** (the spine; one writer) | slices 1 → 6 below | now | Boss, Combat, Shots, Missiles, Turrets, Abilities, PlayerShip (outside the warp block), Raider (see Raider.cs below) | 2 per slice; 3 per slice |
| **B · warp + capital speed** | F21, F22 | slice 1 (needs F2's pilot Disabled gate, and the railgun lock moved off Disabled) | **new Warp.cs**; PlayerShip's warp block; the V key in Hub; HubNodes' readout; MainMenu; Hints; the speed and warp rows in Ships.cs / Stats.cs; the harness warp sections | 2 · 3 · 5 (host-measured penalty) · its frames ride the single rung-4 sweep |
| **C · chips** | F15 with the kind caps | now: it needs nothing from lane A | Equipment, EquipmentWindow, Character (save) | 2 · 3 · 5 (the host sanitises a guest) |
| **D · fields and effects** | F9 (fields, zones, marks, `FxShape.Debris` and its rows) | slice 1 | Fx.cs, FxNode, the slot-drawn fields in PlayerShip._Draw | 2 · 3 (raises and lifetimes) |
| **E · wings** | F13 widened (gunships + patrol) | slice 2 (F4 credits wing hits) | ShipClasses.cs (Wing), the fighter and patrol rows in Stats | 2 · 3 · 5 (6 craft on a guest) |

**Merge points.**
- **B** merges into A before slice 4, because F8 also edits PlayerShip's input path and Hub.
- **C, D and E** merge before slice 6.
- Each lane rebases on A before every engine run.

**The files more than one lane touches.** Each writer keeps to its own region, and each lane rebases before every engine run.
- **SmokeTest.cs.txt:** each lane writes its checks inside its own named method, so merges stay line-disjoint.
- **Ships.cs:** B's helm and warp rows, the sprite batch's `ClassArt`, and A's kits in slice 6.
- **PlayerShip.cs:** A outside the warp block, B the warp block (the ring's draw lives in Warp.cs, with one call in `_Draw`), D the slot-drawn fields in `_Draw`.
- **Stats.cs:** B's warp rows and E's fighter and patrol rows.
- **Boss.cs:** A (slice 1's burn clock, slice 2's `Boss.Out`, slice 5's Burn walk) and the curve lane's DamageScale inside `Boss.Out`. They are line-disjoint; whichever merges second rebases.
- **CHANGES.md:** a lane writes its Handoff only at merge.

**Raider.cs has one writer at a time.** In order: slice 1's stale comments (Raider.cs:17-20, :48) → slice 2's F17 + F20 → the raids batch (Squads) → slice 5's F14 `Call`, F19 Towed, and the Dazzled / Jammed gates.

### Lane A's slices

1. **Slice 1, the rest of "land now":** B (one line, Boss.cs:509), F2 (with the railgun lock moved to Lifts x0), F16, and the stale strength and missile comments (Raider.cs:17-20, :48; Waves.cs:174). Rung 2, then rung 3. Re-baseline the sheet.
2. **Slice 2, the damage doors:** F4 + F17 + F18, F1 (+ Add, + Ramp), F20 (2.58, one edit with the raids EnemyDef rows). Rung 3.
   - *Then the curve batch can land its DamageScale through `Boss.Out`, in its own lane.*
3. **Slice 3, the weapon layer:** F5, then F6, then F7 (+ charge bands, + `While` / `Hidden`). Rung 3.
4. **Slice 4:** F8 (+ the pull, + the point payload), F10. Merge B. Rung 3.
5. **Slice 5:** F12, F11, F14 (+ throw and recall), F19. Merge C, D and E. Rung 3.
   - **Rung 5 once**, for everything that crosses peers so far: NetIds on predicted missiles, the F8 payload, the prism band, NetDecoy, the patrol report and the warp penalty. Skip it if lanes B and E already proved theirs.
6. **Slice 6, the kits by tier.** Each tier's witness rows go in the same edit, at rung 3.
   - **6a capitals:** BB (the arcs, Brace, CIWS); CV (gunships on E, **Supercarrier**); DD (Suppress, **Grapnel pull, swing, rip, tow**).
   - **6b freighters:** FR (**Unmask**, **sentry throw and recall**, the paint gate back on the sentries unless decision 14 says otherwise); TE; BA.
   - **6c heavies:** Warrior (Prism); Sniper (**v1 Anchor, Overcharge**, Tether, Flares, and V refused while anchored); Warden (**Taunt**, Curtain).
     - **Rung 5 once:** Suppress, Grapnel, Unmask, the throw, Supercarrier, Prism, Flares, Taunt.
   - **6d lights:** Dart (Pepperbox, sprint and rod, **Ramjet**, Slingshot); Echo (EMP, and Rewind's exemption from the warp penalty); Wraith (Shadow step's exemption).
     - **Rung 5 once:** the Pepperbox, the sprint, the host's Ramjet pricing, EMP, a guest's Rewind across its own jump.
7. **Rung 4 once**, for every visual in the batch: the prism wedge, clip and fan; the Curtain; the flares; the rip and scar; the patrol ring; the taunt shimmer; the Unmask panels; the warp ring, band, ghost and readout; the 3-ability bar; the chip slots.
8. **Rung 6 once.** If it fails: read which check failed, drop to the lowest rung that covers it, fix, re-prove there, then run rung 6 once more.

### Other batches

- **Curve** (reference-pilot table, boss hull 3820 / 3520, salvage): after slice 2, in its own lane.
- **Raids** (Squads.cs): after slice 2, before slice 5, because of Raider.cs.
- **Sprites:** the 12 player ships can start now that the kits are signed. They must mark the Freighter's 2 flank seats for Unmask and use the Battleship's 4 flanking twins. F7's default flank seats mean 6b does not wait for them. The sprite batch edits `ClassArt` in Ships.cs; rebase before each engine run.
- **Level walls** (`level_gates.md`, same folder, not yet signed): it reads F15's slot counts, so it lands after lane C.
- **Items:** after the whole class batch (your ruling).

---

## 8 · DECISIONS STILL OPEN (say nothing and the default stands)

| # | fork | default | alternative |
|---|---|---|---|
| 1 | Where the Sniper's new piece sits | **a passive on the railgun (Overcharge)**; F / Q / E stay as approved | a key: it replaces the Tether mine or the Flares (say which) |
| 2 | Which new piece | **Overcharge** (51.0) | Fracture (51.1; stacks like Venom) · Deadeye (51.4; a ragged window on the wire) |
| 3 | Supercarrier | **timed: 20 s, cooldown 30 s from the end** (52.6 realistic) | a toggle (61.7: above every non-light, 2.3 under the lowest light; always on, so no longer a choice) |
| 4 | Grapnel on a boss | **pull, then the v2 swing** | pull only: the DD parks 250 u off the hull and the line holds it still |
| 5 | When the rip happens | **at cast-off** (the hook tears free) | when the pull arrives |
| 6 | Does the rip do anything | **no, visual only ("fake")** | 30 damage per rip, x2 on structures (+1.4 DPS) |
| 7 | Overshoot penalty | **proportional**: 2 s per 300 u, so 150 u over is 1 s | in whole steps: 1-300 u over is 2 s, 301-600 is 4 s, 601-900 is 6 s |
| 8 | What "disabled" stops | **helm, guns, abilities and warp**; PD and craft keep going (so the carrier's wings and the freighter's sentries fight on, and those two lose only the helm and new orders) | the helm only |
| 9 | Freighters' warp row | **the capitals' row** (2400 u, 20 s): they are the slowest hulls | the heavies' row (1200 u, 30 s) |
| 10 | Chips | **caps: 3 combat and 3 utility; slots 3/4/5/6** | 6 slots on every hull, exactly 3 + 3 |
| 11 | The Freighter at 56.1, now the top non-light | **keep** | Unmask cooldown 24 → 30 s (→ 54.5) |
| 12 | The Ramjet in the Dart's pricing | **counts** ("its top speed is damage"; the Dart is 66.8) | flight only (the Dart stays at 61) |
| 13 | Do the patrol wing and Unmask fire on missiles? Your carrier note said "engage anything" | **no**: missiles stay with the PD and CIWS, which keeps the three apart | yes, last in line |
| 14 | Sentries and an unpainted boss, pylon or base | **the v2 card: only when painted** (the FR slice puts the gate back) | keep the live rule from triage Q6 (8fddb84): they take one when nothing smaller is in reach, with the paint as Prefer. No work, and the same solo |

---

## 9 · RISKS

1. **Warp across peers** (F21).
   - The jump is the owner's; the penalty is the host's, from the jump it measures. A dropped packet either side of the jump adds one or two packets of ordinary flight to the measure: 6-12 u, under 0.1 s of penalty.
   - The charge lasts at least 1 s, so the `warping` bit rides many packets. The jump itself is the snap over 600 u that the host already detects (PlayerShip.cs:1228).
   - The bit is the guest's own, so the host prices any snap over 600 u, bit or no bit. Only a confirmed Rewind or Shadow step is exempt, and the owner keeps 0.1 s between those and a jump.
   - **Mitigation:** rung 5 bypasses the guest's own lock, clears its bit early, and Rewinds across a jump.
   - The title screen's dodge becomes a scripted hold on the same drive. Its checks (:691, :715-803) are rewritten in the same edit.
2. **Ramjet pricing parity.** The host's bonus is computed from 20 Hz state, so it can trail the owner's by one packet (0.05 s): at most 1% of top, under 1% of a rod.
   - **Mitigation:** the price is the host's alone. Rung 5 asserts they agree within 1%.
3. **Power is still estimated, not flown.** Three classes moved on assumed uptimes: the Carrier's time inside 600 u, the Sniper's 0.90 realism and the Dart's bleed.
   - **Mitigation:** the DealtBy probe plus `Fly()` at rung 3 give landed DPS per class. The levers are named: Supercarrier cooldown, `rail_over_mult`, `ramjet_bleed`, Unmask cooldown. No other number moves.
4. **Merge pressure.** Raider.cs, Ships.cs, PlayerShip.cs, Stats.cs, Boss.cs and SmokeTest.cs.txt are shared by lanes A-E and the raids, curve and sprites batches.
   - **Mitigation:** one writer at a time on Raider.cs, in the order in §7. Each lane keeps its checks in named methods and rebases before every engine run.
5. **A held warp.** The charge holds at the top while V is held. Because the penalty is priced on the jump, a held V aimed at a target or waypoint inside the safe limit is a jump kept ready.
   - **Lever if it bites:** vent the charge 2 s after it reaches the top, and start the cooldown.
6. **The DD's swing past L20.** Boss turn quickens 1% a level, and at 117 u/s the 320 u orbit stops outrunning the Lancer's bow at L21 (§3.2).
   - **Levers:** the DD's top back up toward 130 (L31), or a shorter grapnel_stop.
