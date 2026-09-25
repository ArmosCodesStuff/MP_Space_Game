> Copied unchanged on 2026-09-24 from the sign-off session's scratchpad (`signoff_v2.md`, now this file); `kits_v3.md` then `kits_v31.md` supersede it where they say so.

# SIGN-OFF v2: 12 class kits

Design only. Nothing is built until you approve it. **This replaces v1 (`signoff.md`) as the document you sign.**
Your notes on v1 are applied as binding. The cross-check's findings are applied too. Where the cross-check and a tier draft disagreed, the source or v1 decided (§8).
These binding rules still hold:
- 1 primary weapon + 3 abilities per class.
- PD is passive: 1 DPS per node, 2 nodes on every capital.
- Main-gun bands: 25 / 45 / 65.
- Chips: 3 / 4 / 5 / 6.
- Rate buffs stack **additively**.
- Hulls: capitals BB 500 / CV 425 / DD 395 · heavies 240-300 · lights 180-220, and lights keep the highest DPS.
- No ability may be strictly for other players.

---

## 1 · WHAT CHANGED FROM v1

| class | slot | v1 | v2 |
|---|---|---|---|
| CARRIER | gunships | on **X** | on **E**. No bar uses X |
| DESTROYER | role | smoke-screened torpedo knife fighter | hooks, circles, suppresses, torpedoes it |
| DESTROYER | Q | Smoke screen | **Suppressing fire** |
| DESTROYER | E | Depth charges | **Grapnel**: swings round a boss; tows and hurls a raider |
| DESTROYER | gear | caps draft: dd_salvo = a second Lance | 4 dd_* ids kept, retargeted to small Lance/Grapnel multipliers, renamed |
| FREIGHTER | R | nothing | **Sentry** drop / collect (moved off F) |
| FREIGHTER | F | Deploy / collect sentry | **Time on target** |
| WARRIOR | hull | 200 | **300** |
| WARRIOR | F | Parry: 0.35 s window, Riposte, 25% beam leak | **Prism stance**: 2 s; splits or redirects light by angle |
| SNIPER | hull | 160 | **240** |
| SNIPER | E | Designate (Exposed) | **Flares**: missiles and fighters |
| SNIPER | F | Anchor x2.5 charge, cooldown 12 s | also anchored full charges x1.25, cooldown 8 s (decision 4) |
| WARDEN | hull | 180 | **270** |
| WARDEN | Q | Aegis link (ally only) | **Beacon**: draws raiders; x1.5 from the Warden on them |
| WARDEN | E | Decoy | **Flak curtain** |
| DART | hull | 120 | **200** |
| DART | Space | Mass driver: kinetic rods, 65 DPS | **Pepperbox**: command-guided micro-missiles, 45 DPS stock |
| DART | F | Rod from God, instant | **3 s sprint** (+200% thrust, +100 top, forced thrust), **then** the rod |
| DART | Q | Afterburner | **Jetwash** (decision 3) |
| ECHO | hull | 120 | **180** |
| ECHO | E | Sonar ping | **EMP** |
| ECHO | Space | echoes home on pinged targets | echoes fly straight |
| WRAITH | hull | 120 | **220** |
| enemy heavies | laser | 2.0 / 2.6 / 1.8 DPS today | **twin laser, 1.25 DPS** on all three (decision 2) |
| enemy heavies | missile | 42, 12 s flight (a const) | **35, 10 s flight**; the flight becomes a row |
| boss | L1 hull | 2950 | **3000** |
| gone | | Smoke, depth charges, Aegis link, Decoy, Designate and Exposed, Sonar ping, Riposte, Afterburner, Mass driver | |

Unchanged: BATTLESHIP, TENDER (one text fix), BASTION.
Settled since v1:
- Decision 1: gunships go on E (your note).
- Decision 2: A, the enemy heavies.
- Decision 4: additive.
- Decisions 5 and 6: moot. The prism replaces the parry, and the smoke is gone.

---

## 2 · THE TABLE

| class | role (5 words) | Space | F | Q | E | R | passive | hull |
|---|---|---|---|---|---|---|---|---|
| BATTLESHIP | turns its side, outguns everything | Main battery (4 turrets, arcs) | Broadside | Brace | CIWS | salvo / stagger | PD x2 | 500 |
| CARRIER | fights only through its craft | Fighter wing (attack) | Bomber strike | Scramble | Warp gunships | recall | PD x2 | 425 |
| DESTROYER | hooks, circles, suppresses, torpedoes it | Director battery | Long Lance | Suppressing fire | Grapnel | | PD x2 | 395 |
| FREIGHTER | paints targets; sentries converge fire | Spotter cannon (paints) | Time on target | Bubble | Redeploy | sentry drop / collect | PD x2 | 450 |
| TENDER | one beam, heal or harm | Mending lance | Overdrive field | Repair field | Resupply | | PD x2 | 380 |
| BASTION | pulls them in, shells them | Siege mortar | Bunker buster | Shockwave | Gravity well | | PD x2 | 420 |
| WARRIOR | blade cuts; prism splits light | Blade | Prism stance | Whirlwind | Lunge | | none | 300 |
| SNIPER | plants, charges, pierces, flares missiles | Charge railgun | Anchor | Tether mine | Flares | | none | 240 |
| WARDEN | draws raiders in, shreds them | Proximity flak | Hunters | Beacon | Flak curtain | | PD x1 | 270 |
| DART | its top speed is damage | Pepperbox | Rod from God (sprint, then rod) | Jetwash | Slingshot | | Slipstream | 200 |
| ECHO | every shot and pulse repeats | Echo repeater | Reverb | Rewind | EMP | | none | 180 |
| WRAITH | unseen poisoned shotgun from behind | Ambush scattergun | Veil | Venom | Shadow step | | Backstab | 220 |

**Keys.**
- Space: the weapon. F / Q / E: the abilities. V: warp.
- R is only a weapon's own second action: BB salvo/stagger, CV recall, FR sentry.
- X, G, T and C are unused.
- Off their key's usual meaning: CIWS and Resupply on E, Venom on Q.

**Missiles: one identity each.**
- CV bombers: an unguided swarm.
- DD Lance: one large torpedo.
- WD Hunters: a fire-and-forget guided swarm.
- BA Buster: a bunker-buster; it stops on the first body and deals x2 to a boss or structure.
- DA Rod: a kinetic penetrator.
- DA Pepperbox: a command-guided primary stream, with no seeker and no lock.
- FR Time-on-target rounds are shells, not missiles.

**Solo rule:** every ability of all 12 classes is useful to a pilot flying alone (audit §8.2).

---

## 3 · CARDS

The format is v1's. Numbers are given as range · damage · duration · cooldown. "Proves" places things with Vary / VaryAngle / VaryNear and asserts literals. Every check is rung 3 unless marked.

**Unchanged; see v1 §3:**
- BATTLESHIP.
- BASTION.
- TENDER, with one text fix. Resupply reads "every friendly within 500 u, **you included**". It cuts 8 s from the Tender's own Overdrive and Repair cooldowns, which is its solo use.

### CARRIER: one key
- Warp gunships move from X to **E**. Everything else is v1: Space attack, R recall, F Bombers, Q Scramble, PD x2, hull 425, turn 0.9, thrust 48, radius 140.
- Gunships: 2 per selected target (1-3) · 240 u orbit · 12 s · 10 DPS each · 3000 u from the carrier · cooldown 25 s · 9.6 DPS averaged against a lone boss. They stay at 10 DPS, because v1 decision 2 went to the enemy heavies.
- **Engine:** `Default = Key.E` on the row. It still needs F8 (targets[]) and F13 (orbit). Cost XS.
- **Proves:**
  - E with {1, 2, 3} selected gives {2, 4, 6} gunships.
  - `Abilities.ByKey(c, Key.X)` is null for all 12 classes.
  - Rebinding E to F swaps the two (Abilities.Bind).
  - Rung 4: the gunships are the third slot on the bar.

### DESTROYER: hull 395 · top 130 · turn 1.2 · radius 95 · 3 chips (unchanged)
The loop: hook it, circle it bow-on, suppress it, put the Lance into it.

| piece | numbers | counterplay |
|---|---|---|
| Director battery (Space), v1 | 700 u · 2 x 11.25 every 0.5 s = **45.0 DPS** · leads a selected hostile within 840 u | jinking beats the lead |
| Long Lance (F), v1 | one torpedo along the heading · **300** · 170 u/s · 3000 u · cooldown 18 s = 16.7 DPS if it lands | slow and straight |
| **Suppressing fire** (Q) | For **6 s**, every hostile a **director shell** hits is SUPPRESSED until **3 s** after its last hit. While suppressed: <br>• its guns deal **x0.5** (raider lasers, pylon and base guns); <br>• a boss's non-super moves deal **x0.7**; supers (BossMove.Super) stay x1.0; <br>• it throws no predicted missile: the clock holds, and it throws at the lapse; <br>• it still moves, turns and webs. <br>It is a status, so two DDs do not stack. The Lance and the PD never apply it. A grey chevron shows on every peer. **Cooldown 20 s** from the press. <br>**Per press at L1:** <br>• raid: about **30 hull**. The heavy's laser drops 1.25 -> 0.625 for 9 s (5.6), and its missile clock is held 9 of its 12 s (0.75 x 35 = 26); <br>• pirate base: about **147** (4 guns x 18 every 2.2 s = 32.7 DPS, halved for 9 s); <br>• Lancer: **8-35** (guns 3.6 -> 2.52, wave 45 -> 31.5, trident 15 -> 10.5). <br>Lasers and guns scale with S(L), so x1.6 at L20. The missile does not scale | only what the director HITS is suppressed, so a jinking light keeps full damage · it lapses 3 s after the last hit · supers and webs are untouched · it is wasted with nothing under the guns |
| **Grapnel** (E) | Needs a selected hostile within **700 u**. The lighter body moves, judged by `Targeting.Immovable = Boss\|Structure\|Dummy` (v1's Bastion row). <br>**SWING** (the anchor is immovable): <br>• up to **5 s**; the line is fixed at the press range, clamped to [HitRadius + 150, 700] u; <br>• the bow is held on the anchor, and tangential velocity is kept; <br>• A/D thrust round the orbit (capped at 130 u/s), W/S reel at 100 u/s; <br>• the director stays on the anchor, and a Lance fired mid-swing runs down the line into it; <br>• the orbit turns 0.30 rad/s at 430 u (a boss's turn rate) and 0.19 rad/s at 700 u. <br>**TOW** (the target is a craft): <br>• reeled to **160 u** off the bow in 0.5 s and hauled up to **3 s**; off its post it cannot web or fire; <br>• then **hurled** along the bow at 600 u/s, up to 900 u; <br>• the first hostile it strikes and the thrown craft each take **60** (120 if a heavy is thrown); <br>• hooking your own pinner drags it off its post, and the web drops. <br>**Casts off** on a second E, at the time limit, when the anchor dies or warps, or when a web lands during a swing. |v| is kept. <br>**Refused:** NO TARGET, OUT OF RANGE, a missile, or WEBBED (the swing only; towing while pinned is allowed). **Cooldown 16 s** from cast-off. <br>Sheet about 0, plus 60 per hurl (about +1 realistic) | a swing is a predictable circle: the Lancer's 340 u wave catches a short line, a ram down the nose catches a swing that crosses it, and bolts are hitscan · a web ends the swing, and the Drake's warp snaps it · a hurl stops at the first body, so escorts shield the boss · the other pinners still web you while you tow one |
| passive | PD x2 | |

**Gear.** The four ids are kept. The rule forbids a second Lance, so each is retargeted and renamed:

| id | new name | stats |
|---|---|---|
| dd_salvo | Rapid Reload | lance_cooldown x0.8, lance_damage x0.85 |
| dd_buster | Heavy Warhead | lance_damage x2.0, lance_speed x0.8 |
| dd_seeker | Long-Run Tubes | lance_speed x1.25, lance_range x1.2, lance_damage x0.8 |
| dd_loader | Deck Winch | grapnel_cooldown x0.7, suppress_time x0.8 |

Kit parts dd_main_battery and dd_missile_rack Need lance_damage. Equipment.Migrated (Equipment.cs:92) still lands on dd_missile_rack.

**Engine.**
- Suppress (S): F4 (the DealtBy weapon tag) + F18 OnDealt + F2 Suppressed + an F17 OutGuards row + an F9 chevron.
- Grapnel (M + S):
  - F8 owner-side swing: a LocalFlight constraint on the anchor's replicated position.
  - The host confirms through Sl("grapnel") {Left, N = anchor NetId, Own = line}. Unconfirmed after 0.5 s, the owner casts off.
  - F9 draws the line. F19 does the tow and hurl.
- Rows: grapnel_reach / swing / tow / hurl / impact / cooldown; suppress_window 6, suppress_time 3, suppress_cooldown 20.

**Leaves:** Smoke screen, depth charges, and F6's depth-charge row.

**Proves.**
- **Suppress.**
  - A director shell, a Lance and a PD round each hit a webifier; only the shell suppresses.
  - Targets {webifier, gunship, pylon, base, Lancer, Drake}, hit at {0.5, 5.9 s} into the window, measured at {2.9, 3.1 s} after the last hit. Literals: webifier 1.0 -> 0.5 · gunship 1.25 -> 0.625 · pylon 9 -> 4.5 · base 18 -> 9 · Lancer guns 3.6 -> 2.52 · wave 45 -> 31.5 · Drake slug 6 -> 4.2 · beam tick 50 -> 50 · rock 250 -> 250.
  - A gunship at VaryNear 300-500 u whose clock reaches 0 while suppressed throws nothing, then throws within 0.1 s of the lapse.
  - A suppressed pinner still pins.
  - A shell at 6.1 s applies nothing. Two DDs give x0.5. Cooldown literal 20.
- **Swing.**
  - Anchors {Lancer, Drake, pirate base, dummy} x line {HitRadius + 100 (clamps to +150), 400, 699}; 701 is refused.
  - x {A, D} x {W, S}: range within 15 u of the line and the bow within 5° for 5.0 s.
  - Cast-off at {second E, 5.0 s, Drake warp, web} keeps |v| within 5%.
  - A Lance fired at {1, 3, 4.5 s} from a VaryAngle bearing hits the anchor.
- **Tow.**
  - Craft {webifier, talon, pod, gunship, cross, lancerkin} at VaryNear {150, 400, 699}: 160 ± 10 u off the bow within 0.5 s, laser tally flat while hauled.
  - Hurled at {a raider 300 u down the line, the boss 600 u down it, nothing}: 60 or 120 to both bodies, or 900 u flown for a miss.
  - A webifier posted ahead pins the DD. Hooking it clears Pinned within 0.35 s. Hooking the boss while pinned is refused WEBBED.
- **Gear.** dd_salvo launches 1 torpedo per press and cools in 14.4 s. dd_buster puts 600 on a parked dummy. Each of the 4 ids loads from a save and fits only the DD.
- **Rung 5.**
  - A guest DD suppresses (the host decides), and the chevron shows on the other guest.
  - A guest's swing is drawn on the host and the other guest, hull within 60 u.
  - A towed raider follows a guest's bow.

### FREIGHTER: hull 450 · 85 u/s · 4 chips (unchanged)

| piece | numbers | counterplay |
|---|---|---|
| Spotter cannon (Space), v1 | 800 u · 31.25 every 1.25 s = **25.0 DPS** · a hit paints the target for 5 s, one target at a time | the paint lapses 5 s after the last hit |
| **Sentry** (R) | R is the weapon's second action, because the sentries shoot what the spotter paints. <br>Drop one under the hull; press R within 120 u of one of yours to pick it up. <br>v1 numbers: 3 out · 6 s between drops · 120 hull · 650 u · 10 DPS each. <br>Unpainted priority: missiles, then light craft and fighters, then heavies. A boss, pylon or base only when painted | raiders go for sentries · a boss warps out of reach |
| **Time on target** (F) | Needs a paint under 5 s old. <br>• Every gun the freighter owns fires ONE round: the spotter, plus each sentry within **1500 u** of the paint. <br>• Each round aims at where the target will be in **1.2 s** (Missiles.Predict on its Lead). <br>• Each round's speed is its distance / 1.2 s, so all land in one instant under a friendly ring. The floor is 200 u/s, so a gun closer than 240 u lands early. <br>• **40** per round, to the first hostile body on its line: 40 with no sentries, 160 with three. <br>**Cooldown 16 s** = 10.0 DPS sheet at 4 guns, about 7 realistic | a target that reverses or boosts inside the 1.2 s is missed by most rounds · an escort in a lane takes that round · the Drake warps out of the spot |
| Bubble (Q), v1 | pool 400 · 260 u · 8 s · cooldown 25 s | |
| Redeploy (E), v1 | sentries fold and land on a 150 u ring 1.0 s later · cooldown 20 s | |
| passive | PD x2 | |

**Engine.**
- Sentry (XS): the v1 Ab.Deploy row with Collect folded in (Abilities.cs:202-226), with `Default = Key.R`, plus the F14 sentry filter and Prefer.
- Time on target (S), no new wire:
  - F5 paint.
  - Combat.Fire (Combat.cs:115) of Shots.Shell, with speed and range set per call; NetShot already carries both.
  - Missiles.Predict and Lead (Missiles.cs:102-126).
  - Fx.AimZone, raised as Hub.ThrowMissile raises it (Hub.cs:1218).
  - F4 credit.
  - Rows: tot_damage 40, tot_flight 1.2, tot_reach 1500, tot_cooldown 16.

**Leaves:** the F key for sentries, and the T/C keys and the collect witness. The MP C press becomes R.

**Proves.**
- **Sentry.**
  - R drops a sentry at a Vary spot.
  - R at {119, 121 u} from one of yours picks it up or drops a new one.
  - F no longer drops.
  - A 4th press with 3 out is refused ALL OUT.
- **Time on target.**
  - Sentries at VaryNear {1499, 1501 u} from the paint x {0, 1, 3} out give {1, 2, 4} rounds.
  - Arrivals land within 0.05 s of 1.2 s from VaryNear 250-1400 u on VaryAngle bearings. A gun at 150 u lands early.
  - 40 per round on a parked dummy (160 with 3 sentries).
  - A target moving straight at {0, 80, 130 u/s} takes every round. One reversing 0.3 s after the press takes at most 1 of 4.
  - A light across one lane takes that round, and the boss takes the rest.
  - Paint age 4.9 s fires; 5.1 s is refused NO PAINT.
- **Rung 5:** guest sentries pick the same target id, and a guest sees every round land on the host's spot.

### WARRIOR: hull 300 · 5 chips

| piece | numbers | counterplay |
|---|---|---|
| Blade (Space), v1 | 160 u · ±55° · 26 every 0.40 s = **65.0 DPS** | kiting; every boss ring reaches it |
| Whirlwind (Q), v1 | 2 s spin · 210 u · 40 DPS · clears and blocks webs · cooldown 14 s | |
| Lunge (E), v1 | 420 u dash in 0.3 s · 40 per body · half damage taken · cooldown 7 s | |
| **Prism stance** (F) | **2.0 s**; press F again to drop it. The guard **g** faces the cursor, clamped to ±90° off the nose. <br>Light caught within **±60°** of g is resolved by θ = angle(g, −u), the angle between the guard and the line back to the source: <br>• **SQUARE** (θ ≤ 15°): 75% goes back along g as a friendly beam; 25% continues straight on as the enemy's. <br>• **SLANT** (15-60°): 50% is mirrored off the blade, r = u − 2(u·g)g, as a friendly beam; 50% bends θ/2 the other way and stays the enemy's. <br>• **OPEN** (over 60°): not caught. <br>The Warrior takes **0** of a caught blow. <br>**Children:** <br>• every child beam is 36 u wide; <br>• a friendly child reaches 1500 u to the first hostile body (900 u for rays); <br>• a through child reaches the parent's remaining length (400 u for rays) and hits every pilot on it, under the parent's source name; <br>• children never split again; <br>• splits fire on the Warrior's own tick: about every 0.75 s, at most 3 per stance. <br>**Projectiles** are reflected, never split. SQUARE sends them back along g at x1.5 speed; SLANT sends them along r at x1.0. Both keep x1.0 damage and become friendly and unguided. The siege missile is never caught (Reflectable = false). <br>**In stance:** top speed x0.5, no swings; Q or E ends it. **Cooldown 12 s from its end** (a 14 s cycle). <br>**At L1** (tick 50): SQUARE puts 37.5 into the Lancer and lets 12.5 through, so one stance puts 112.5 into the boss. SLANT at 40° mirrors 25 at 80° and bends 25 by 20° | the rear 60° is never covered · rings, rams, the rock, blasts and webs are never caught · astern heavies re-post dead astern · a pinned Warrior cannot turn |

**Why a party is not immune behind one Warrior:**
1. The stance lasts 2 s against a 3 s burn: 3 of 5 ticks are caught, and 2 land in full.
2. SQUARE lets 25% through down the same line.
3. SLANT bends 50% onto whoever stands on the new line. An ally directly behind is clear only from 48 / sin(θ/2) u back: 100 u at 60°, 140 u at 40°, 370 u at 15°.
4. Children never split, so a second Warrior cannot finish the job.
5. It has one facing.
6. A 14 s cycle covers the beam or the trident, not both.

Best case for an ally directly behind: SQUARE saves 45%. SLANT saves 60%, and the other half lands on someone else's line.

**Hull 300:** it survives a full burn (250) with 50 left, or with 200 left with the stance up. It also survives one rock.

**Engine (L).**
- F11, reshaped: the blow kind is kept; the 0.5 s buffer and the per-peer RTT are dropped.
- **NEW Prism.cs**:
  - `Prism.Bands` rows {MaxAngle, OutShare, OutAlong Guard|Mirror, ThroughShare, ThroughBend};
  - the contract `IPrism.Catches`;
  - `Prism.Resolve`.
- Boss.Burn walks pilots along the beam and stops at the first catcher.
- Shot.Strike reflects (Shots.cs:197).
- Also used: F2 Parrying = 32 (wire) · F1 speed Lift · F5 Reflect row + Reflectable · F4 credit · F12 guard clamp · a new Fx row warn_beam (a live-beam lane a prism may clip).
- Every peer draws the wedge, the clip and the children from the Parrying bit, AimPoint (20 Hz) and the boss pose (30 Hz while Locked). The resolved band rides the prism slot's N (risk 1). No new RPC.

**Leaves:**
- The 0.35 s window, the success reset and Riposte (riposte_*).
- The 2.5 s whiff lockout, and the 25% beam leak (v1 decision 5).
- rsh_* parts that v1 pointed at parry_* now name prism_*.

**Proves.**
- **Bands.** Beam heading VaryAngle; the Warrior 400-1500 u along the beam and 0-60 u off-axis. θ is the asserted input: {0, 14, 16, 45, 59, 61, 120, 180} gives SQUARE, SQUARE, SLANT, SLANT, SLANT, OPEN, OPEN, OPEN.
- **SQUARE.** The Warrior takes 0. A raider on g at Vary 300-1400 u takes 37.5. An ally behind at Vary 150-600 u takes 12.5.
- **SLANT at 40°.**
  - A target on r takes 25.
  - An ally on t at Vary 200-500 u takes 25.
  - An ally on the old axis 300 u back takes 0; one 60 u back takes 25.
- **No re-split.** A second Warrior in stance on the through line is hit, not split.
- **Timing and cost.**
  - A tick at 1.9 s is caught; one at 2.1 s lands 50.
  - A drop at Vary 0.3-1.7 s starts the 12.0 s cooldown from the drop.
  - Speed x0.5 and no blade damage while in stance.
- **Projectiles.**
  - A slug at θ {0, 30, 59, 61} goes along g at x1.5, along r, along r, and lands.
  - The armed dummy's seeker is reflected.
  - Never caught: a ring, the rock, a ram, a raid blast.
- **Rays.**
  - A front-arc webifier laser is split.
  - An astern gunship always lands.
  - A cursor at 120° clamps g to 90°.
- **Hull witness:** a pilot held on the beam axis for a whole burn takes exactly 250. This needs fix B (§5).
- **Rung 5:** the guest draws, the host decides, and the reflected round arrives by NetShot.
- **Rung 4:** one frame each of the wedge, a SQUARE clip and a SLANT fan.

### SNIPER: hull 240 · 5 chips

| piece | numbers | counterplay |
|---|---|---|
| Charge railgun (Space), v1 | tap 8 · full charge **45** along a 2500 x 14 u line through everything · 25.0 DPS | leave the line during the charge |
| Tether mine (Q), v1 | 2 charges · 170 u · holds raiding craft 3 s | |
| **Anchor** (F) | v1: up to 8 s rooted · charge x2.5 · reach x1.4 = 3500 u · 0.3 s to release. <br>**Plus** (decision 4): anchored full charges deal **x1.25** (56.25), and the cooldown is **8 s** from release (v1: 12 s). <br>Anchored 67.0 DPS, duty 0.5 | it is planted, so a sitting target · 240 hull dies to a full burn or a rock |
| **Flares** (E) | **6 flares** in a ring **180 u** out (0.6 s to coast out), the first dead astern. They burn where they stop for **5 s**. **Cooldown 16 s**, never refused. <br>• **Guided missile** (Decoyable; default Guided && AtPlayers): within **500 u** of a burning flare it turns onto the nearest one at its own turn rate (sticky) and bursts there harmlessly. <br>• **Predicted missile:** a landing mark within **300 u** of a burning flare, with at least 1.0 s of flight left, moves onto it (sticky). The red circle slides on every peer. <br>• **Fighter** (Light\|Heavy, never a boss or structure): within **150 u** of a burning flare it is DAZZLED until 4 s after it leaves. A dazzled craft cannot LATCH anew and throws no missile. Raiders fire only while latched (Raider.cs:261-270, :319-320), so no new web means no laser either. One already latched keeps its web; curing that is the Echo's EMP. <br>The ring covers every pinner post (111-177 u) and the astern heavy post, so while flares burn nothing new pins the Sniper, for up to 9 s. Anchored, every flare is 180 u off the hull, so a 90 u blast clears it | flares stay where they burn · 5 s · slugs, scrap, beams, rings, rams and the rock ignore them · an existing web holds, so pop them before the raid posts · a seeker already on the hull can clip it · bosses are never dazzled · friendly missiles ignore flares |

**Hull 240:** range is its defence. At 2500-3500 u it is outside the Lancer's 900 u guns, the Drake's 1100 u gun and the trident's 1920 u flight. Staying anchored in a telegraphed lane kills it (burn 250, rock 250).

**Engine (M).**
- A `Spawns.All` row, `flares`: one spawn per salvo, and every peer derives the 6 points from the seed.
- F14: `NetDecoy(netId, point)` turns any Decoyable guided Shot or predicted missile onto a point, on every peer.
- Predicted missiles get NetIds. Hub._blasts is unnumbered today (Hub.cs:1210); F6 adds the ids.
- Guidance gains a point target beside Combat.PlayerById (Shots.cs:167-168).
- Status.Dazzled is host-only, an F17 OutGuards row (no new latch, no throw).
- An F9 mark sits on each dazzled raider.
- F5 `ShotDef.Decoyable`, false for the SiegeMissile.
- The Anchor lever is an F1 damage lift scoped to anchored full charges, plus the anchor_cooldown row.

**Leaves:** Designate, and Exposed (from F2 and F4).

**Proves.**
- **The salvo.** Heading VaryAngle: 6 flares at 180 ± 5 u, 60° apart, the first astern. Present at 4.9 s, gone at 5.1 s.
- **Seekers.**
  - The armed dummy's seeker at a varied bearing, with flares popped Vary 0-0.4 s after launch: it bursts on a flare, and the Sniper takes 0.
  - A seeker 490 u from the nearest flare turns; one at 510 u does not.
- **Predicted missiles** (anchored).
  - A mark 180 u from a flare moves, and the Sniper takes 0.
  - A mark 310 u from every flare stays.
  - A flare that burns out first still takes the blast.
- **Dazzle.**
  - {webifier, talon, pod} posting 149 u from a flare never latch while dazzled, and the Sniper is never Pinned. At 151 u they latch.
  - A webifier latched before the pop stays latched.
  - A dazzled gunship throws no missile.
  - The boss's guns still land, and hunters fired through the flares still hit.
- **Anchor.**
  - An anchored full charge deals 56.25 to each of 3 dummies on a VaryAngle line; unanchored, 45.
  - The cooldown is 8.0 s from a release at Vary 1-8 s.
  - With a Tender's Overdrive on, a full anchored charge takes 0.533 s.
- **Rung 5:** a guest sees the seeker turn, the mark slide, and the flares at the host's spots.

### WARDEN: hull 270 · 5 chips

| piece | numbers | counterplay |
|---|---|---|
| Proximity flak (Space), v1 | 45 DPS · x0.75 on a boss · 70 u fuse · 700 u | spread out |
| Hunters (F), v1 | 6 x 45 · 1200 u · cooldown 14 s. Prey order: raiders latched on a friendly hull (the Warden included), then the nearest | |
| **Beacon** (Q) | For **6 s**, every raider (Light\|Heavy) standing within **1000 u** of the Warden, or hunting a target within 1000 u of it, takes the Warden as its target: <br>• latched pinners let go and post on the Warden; <br>• standoff heavies treat the Warden as pinned and boost in astern at once, instead of waiting 4200 u out; <br>• hostile emplacement guns with the Warden in range prefer it. <br>Never a boss, a missile or a dummy. **Called craft take x1.5 from anything the Warden deals** for the 6 s (flak, hunters, curtain, PD). Afterwards each goes back to what it hunted (its Quarry is untouched). **Cooldown 20 s.** No damage reduction | anything beyond 1000 u · bosses never listen · 6 s · the Warden gets webbed, lasered from astern and put in a missile lane · 20 s |
| **Flak curtain** (E) | 5 flak shells burst in a line at the cursor (clamped to 150-700 u), perpendicular to the aim: **500 x 80 u**, **6 s**, live 0.5 s after the press. <br>A hostile craft (Light\|Heavy, not a boss or structure) touching it takes **20**, then **10 every 0.5 s** while inside. <br>No slow: Jetwash is the only slow. Missiles, bosses, structures and allies are untouched. **Cooldown 18 s** | it is 500 u wide, so go round · laid parallel to the threat it does nothing · a craft already posted never crosses it |
| passive | PD x1 at 1 DPS | |

**Solo at L1**, a craft crossing the curtain at right angles, without the Beacon / under it (x1.5):

| craft | hull | without Beacon | under Beacon |
|---|---|---|---|
| talon | 18 | dies | dies |
| boosting webifier | 25 | takes 20, left at 5 | dies |
| pod at cruise | 60 | takes about 50, left at 10 | dies |
| gunship at cruise | 100 | takes about 60 | takes about 90 |
| Lancer escort | 3 | dies | dies |

Under the Beacon, one flak burst (22.5 x 1.5 = 33.75) kills a webifier.

**Hull 270:** a full L1 raid on a Warden under its own Beacon does about **10.9 DPS**: the pinners' 3.1, two heavies at 1.25 each, and 2 x 35 missiles per 12 s at 90% landing. So 270 buys about 25 s. It survives a full burn (20 left) or a rock, not both.

**Engine.**
- Beacon (S):
  - `Raider.Call(target, seconds)`: v1's drafted Lure, renamed for the mechanism. It is a timed target override and leaves Quarry alone.
  - NEW `IRaidTarget.Calls`, read where TickHeavy computes pinned (Raider.cs:309).
  - The paint's TargetFilter.Prefer is reused for emplacement choosers (Emplacements.cs:214).
  - An F4 row: x1.5 when the hostile's CalledBy is the dealer.
  - An F9 ring on the press.
- Curtain (S): F9 Zones gain a Bar shape (a capsule), plus a Zone row `curtain`, F4 credit, and cosmetic Flak shells.

**Leaves:** Aegis link, the Decoy and its L-cost body, and the drafted Zones.Grounds.

**Proves.**
- **Beacon.**
  - Raiders at 999 u switch; at 1001 u they do not.
  - A raider at 1400 u hunting the hauler 900 u from the Warden switches.
  - A raider latched on an ally lets go within 0.25 s.
  - An edge gunship boosts in at once; with no Beacon it waits.
  - At 6.1 s every raider is back on its own target, and an escort's hunter returns to the hauler.
  - The boss ignores it, and its escort webifiers switch.
  - Solo raid on the hauler: all 3 pinners are pulled off.
  - Flak deals 33.75 to a called webifier and 22.5 to one that was not called.
- **Curtain.**
  - A cursor at 100 / 900 u clamps to 150 / 700.
  - The line is perpendicular, with the aim VaryAngle'd.
  - A raider centred (40 + r − 1) u off the axis takes 20, then 10 per 0.5 s. At (40 + r + 1) u it takes 0.
  - The boss, a structure and a seeker take 0.
  - Gone at 6.1 s. The credit shows in the Warden's NoteDealt.
- **Rung 5:** a guest sees the raiders swing.

### DART: hull 200 · 6 chips

| piece | numbers | counterplay |
|---|---|---|
| **Pepperbox** (Space) | Command-guided micro-missiles: <br>• two nose rails alternate, **6 a second**; <br>• each launches along the nose at **520 u/s + ship velocity**; <br>• each turns up to **6 rad/s** toward the owner's LIVE cursor, and flies straight on once within 24 u of it; <br>• **750 u**; first hostile body only, never a missile, no area. <br>Damage **7.5 x clamp(top / 260, 1.0, 1.5)** per missile, with top read from the sheet on the host: <br>• stock: **45.0 DPS** (the mid band); <br>• top 325: 56.25; <br>• in the sprint (360): 62.3; <br>• top 390+: 67.5 | talons jink inside its 87 u turn circle · a boosting raider outruns it from behind · whatever stands in front takes it · a cursor behind the nose wastes the flight turning |
| **Rod from God** (F) | **Press:** a **3.0 s sprint**: <br>• thrust **+200%** (x3); <br>• top speed **+100 u/s** flat, added after every multiplier; <br>• **forced thrust**: throttle full, S dead, rudder free. <br>**At 3.0 s** the rod fires along the nose at nose x 300 + ship velocity. It pierces every hostile on its line once, flies 1400 u, and never hits a missile. <br>**Damage** 180 x clamp(top / 260, 1.0, **2.0**), priced at the moment it fires: <br>• **249.2** stock (top 360); <br>• 294.2 with Racing Drive (425); <br>• 360 at 520+. <br>The recoil leaves 30% of speed. **Cooldown 12 s from the press** = 20.8 DPS stock, +4.3 from the Pepperbox priced at 360 | a 3 s telegraph down a line that can curve but never stop · raiders boosting at x5 sidestep it · webbed mid-sprint, the rod goes down the frozen heading · the recoil parks it at about 108 u/s with no Slipstream |
| **Jetwash** (Q), decision 3 | For **4.0 s** the Dart lays a wake of 80 u discs, one every 0.1 s, each living 2.0 s. The ribbon is speed x 2 s: 520 u at 260 u/s, 720 u in the sprint. <br>Unlatched hostile craft whose centre is in the wake move at **x0.4**, boost included, until 0.5 s after they leave. <br>Bosses, structures, missiles and latched craft are untouched. No damage. It is the game's only slow. **Cooldown 16 s** | the wake is only behind · a web posted beside the hull and standoff heavies at 150-260 u never cross it · a webbed Dart lays only 104 u · a 700 u/s gunship is still at 280 u/s |
| Slingshot (E), v1 | snaps the hull and its velocity onto the cursor bearing, up to 180°, keeping 100% of speed · cooldown 6 s · it re-aims the rod mid-sprint | raiders turn to track it |
| Slipstream (passive), v1 | x0.7 damage taken at 325 u/s or more of actual speed; the host clamps reported speed to top x 1.1. At stock it is live only inside the sprint; Racing Drive common puts the top at exactly 325 | a web or the recoil drops it under 325 |

**Missile identity.** The Pepperbox and the Hunters are the closest pair. The Pepperbox passes only while:
- it never locks or seeks: its only guidance point is the launcher's AimPoint;
- a dead launcher's missiles fly straight;
- it is drawn as tracer darts, not Hunter bodies.

**Engine.**
- Pepperbox:
  - The F7 Guns path with Bore, Inherit and SpeedPriced.
  - An F5 row, `Pepper`, replaces v1's Rod row.
  - NEW `ShotDef.Command`: steer to Combat.PlayerById(TargetId).AimAt, where TargetId is the launcher's id. NetShot already carries target and turn (Hub.cs:1694), and AimPoint is on every peer at 20 Hz (PlayerShip.cs:1204), so there is no wire change.
  - NEW stat row `shell_turn` 6.0. The kit id is `dart_pepperbox`.
- Rod:
  - The Railgun row shape: Press sets Left, and Expire fires on the host (Abilities.cs:257-268).
  - F1 lifts, plus a NEW F1 `Add` term.
  - NEW `AbilityDef.Forces`, which shares the web's forced-thrust line (PlayerShip.cs:1086), now `if (Pinned || Forced) throttle = 1f; if (Pinned) rudder = 0f`.
  - F5 Penetrator, and F8 for the recoil.
- Jetwash (0 RPC):
  - The F9 path zone: the type v1 built for Smoke.
  - Status.Slowed, host-only.
  - One speed share read by Raider's two speed lines (Raider.cs:252, :312), shared with the gravity well.
- **Wire:** 6 NetShot/s, 9 under Overdrive. If that bites, send a NetShot with a count and fire ripples of 3.

**Gear.** The ids are kept:
- light_roll_thrusters becomes "Sprint Thrusters" and Needs sprint_time.
- rol_long: sprint_time +0.40 / rod_cooldown −0.20.
- rol_racing: wash_time +0.30 / wash_slow −0.15.
- rol_hot: sprint_speed +0.30 / sprint_time −0.15.
- rol_quick: sling_cooldown +0.50 / wash_cooldown −0.25.

**Leaves:** the Mass driver, the instant rod, and the Afterburner.

**Proves.**
- **Pepperbox.**
  - Per missile at top {260, 325, 390, 450}: {7.5, 9.375, 11.25, 11.25}, and 10.385 in the sprint.
  - Landed DPS 45 ± 3% over 6 s on a still dummy at VaryNear 300-700 u, VaryAngle within 90° of the nose.
  - Range: 749 u lands, 751 u fizzles.
  - Cursor switched mid-flight from dummy A to dummy B at VaryAngle 30-90°: the missiles in flight land on B.
  - The launcher wrecked mid-flight: its missiles fly straight, with no orphans.
  - A light in front of a boss takes the missile. A seeker on the path is flown through.
  - 12 ± 1 NetShot in 2 s.
  - After a Slingshot sideslip, launch velocity = nose x 520 + ship velocity.
- **Sprint.**
  - Thrust is 3.0x unsprinted, measured from rest over 0.25 s.
  - Top = sheet + 100: {260 -> 360, 325 -> 425}.
  - With S held and W off, speed still rises and A/D still yaw.
- **Rod.**
  - It leaves at 3.0 s ± 1 frame along the nose, at VaryAngle headings.
  - Damage {249.2, 294.2, 360}, never above 360.
  - It pierces {1, 3} dummies on a VaryAngle line with VaryNear spacing, each once. A missile on the line is untouched.
  - Speed after is 30% ± 2%. The lifts are gone after 3.0 s.
  - Pressed while webbed, it fires down the frozen heading.
  - Wrecked mid-sprint: no rod, and the slot is clear.
  - Cooldown 12 s from the press.
- **Jetwash.**
  - The wake is {520, 720} u ± 5% at {260, 360} u/s.
  - A raider crossing at VaryAngle with its centre {38, 42} u off the path moves at x0.40 ± 0.02 / x1.0.
  - A boosting webifier drops 500 -> 200.
  - The slow ends 0.5 s after leaving.
  - A latched raider, the boss, a pylon and a seeker are unchanged.
  - No disc after 4.0 s, none left at 6.0 s.
- **Slipstream:** {324, 326} u/s.
- **Rung 5:**
  - A guest's copies land within 30 u of the host's.
  - A guest's sprint starts within one RTT.
  - The wake raises 0 Fx RPC.

### ECHO: hull 180 · 6 chips

| piece | numbers | counterplay |
|---|---|---|
| Echo repeater (Space) | v1 numbers: 22 every 0.5 s, plus an echo of 11 after 0.6 s from the recorded muzzle = **66 DPS**, 500 u, 620 u/s. <br>**Echoes now fly straight**: the homing left with the ping. EchoRound drops Guided; if only its look still differs, it becomes Shell with a ghost tint | a jinking or boosting raider slips its echoes |
| Reverb (F), v1 | 5 s · x1.2 rate · 35% of the store blasts in 220 u · cooldown 18 s | |
| Rewind (Q), v1 | back 3 s · clears a web · cooldown 30 s | |
| **EMP** (E) | A **300 u** pulse centred on the Echo JAMS every hostile craft inside (Light\|Heavy, escorts included). **0.6 s later it pulses again from the press point** and refreshes the jam. <br>Jammed for **4.0 s** (up to 4.6 s with the second pulse): <br>• no web, no gun or laser, no missile launch; the turret stops tracking; <br>• it still flies, chooses, posts and boosts; <br>• the web on a pilot lapses in 0.25 s; <br>• standoff heavies, which come in only on a PINNED target (Raider.cs:309-312), head back to the edge. <br>Bosses, structures, dummies and missiles in flight are immune. No damage. **Cooldown 18 s** | 300 u is inside its own 500 u reach, so it must let them close · craft arriving after the second pulse are free · jammed craft fire the moment it ends · it does nothing to a boss or a missile |

**The EMP and the Flares are split by timing.**
- Flares PREVENT: no new latch, no throw, and missiles are lured.
- The EMP CURES: existing webs drop and guns go silent.
- It is also distinct from Shockwave (throw or hold), Gravity well (pull), Tether (hold), Veil (not chosen) and Jetwash (slow). The EMP moves nothing and changes no one's target.

**Hull 180:** it is the safest light (500 u stand-off, Rewind, EMP) with the highest realistic DPS (72), so it trades the most hull.

**Engine (S).**
- RushEmp() (PlayerShip.cs:595-606) and Fx.Emp move to the Echo, renamed `Emp(at)`. The row has the Rush's shape: Press, then Expire on the host.
- Status.Jammed is host-only, an F17 OutGuards row. TetherTo (Raider.cs:358) reads it, so guests see the web drop.
- F9 marks sit on each jammed NetId.
- NEW host-only `Slot.At` holds the press point. It replaces `_echoAt` (PlayerShip.cs:116), which is deleted.
- Rows: emp_range 300 (id kept), emp_stun -> emp_jam 4.0, emp_echo 0.6, emp_cooldown 18. emp_damage and its Dps row are deleted.
- **TRAP:** rsh_iron, rsh_shock and rsh_wide name emp_* (Equipment.cs:297-302). The Warrior edit must move them off emp_*, or Fits lets them fit the Echo.

**Leaves:** Sonar ping, echo homing, and the ping's marks on allies' scopes.

**Proves.**
- **Reach and targets.** Raiders at {299, 301} u x {latched webifier, talon, pod, gunship, cross, lancerkin}.
- **Jammed.**
  - 0 strikes and 0 launches over 4.0 s; position still changes.
  - The Echo's Pinned lapses by 0.3 s, and the heavies posted astern leave for the edge.
- **Second pulse.** The Echo moves 250 u at VaryAngle within 0.6 s:
  - a raider entering the press point's 300 u at 0.3 s is jammed at 0.6 s;
  - one 250 u from the new spot but 400 u from the press point is not.
- **Immune.** The boss, a pylon and the base fire on schedule. A trident seeker keeps its TargetId.
- **Timing and cost.** The jam ends at 4.0 s (4.6 s with the second pulse). Raider hull is unchanged. Cooldown 18 s.
- **Echo repeater.** An echo fired at a jinking raider does not curve.
- **Rung 5:** a guest sees the web line drop and the marks.

### WRAITH: hull 220 · everything else v1
- 220 is the highest light hull, still under the heavies' 240.
- Point blank (65.3 DPS at 0 u) puts it inside every ring and inside the Lancer's wave.
- Veil stops it being chosen, not being hit (F3).
- Shadow step lands it among escorts.
- **Proves:** the hull literal 220, plus v1's proves.

### ENEMY HEAVIES (not a class): gunship, cross, lancerkin

| field | today | v2 |
|---|---|---|
| laser | 2.0 / 2.6 / 1.8 DPS, one turret | **twin laser, 1.25 DPS on all three**, drawn as two flashes. That is 1.25 x a webifier, the game's damage unit (decision 2). The cross keeps its 0.8 s cadence and no missile |
| missile | 42 damage, 12 s flight (`Raider.MissileFlight`, a const, Raider.cs:78) | **35 damage, 10 s flight**. The flight becomes a row (EnemyDef.MissileFlight). The missile stays flat at every level |

- **Engine (F20, XS):** one twin-laser field set on the three rows, and MissileFlight moved off the const.
- **Stale comments**, fixed in the same edit:
  - Raider.cs:17-20 says a 7 s flight and "2x".
  - Raider.cs:48 and Waves.cs:174 say S(L) = 1.1^(L-1); it is 1.025^(L-1).
- **Proves:** the three rows against the literals 1.25 / 35 / 10, in one place.

---

## 4 · POWER CHECK: one target, a lone boss, stock, L1

"Sheet" is max uptime and "realistic" is the uptime a pilot holds, as in v1. "Hull lost" is today's boss (DamageMult 1) over the class's own kill, after 0.5%/s regen. Above 1.0 means it dies unless it dodges or uses its defence.

| class | tier | hull (v1) | sheet | realistic | kill @ 3000 | hull lost: Lancer / Drake |
|---|---|---|---|---|---|---|
| SNIPER | heavy | 240 (160) | 46.0 | 43 | 70 s (88 s without decision 4) | 0.19 / 0.44 |
| BATTLESHIP | capital | 500 | 52.3 | 46 | 65 s | 0.11 / 0.18 |
| TENDER | freighter | 380 | 48.4 | 46 | 65 s | 0.82 / 0.34 |
| BASTION | freighter | 420 | 55.0 | 47 | 64 s | 0.19 / 0.27 |
| WARDEN | heavy | 270 (180) | 53.0 | 49 | 61 s | 1.21 / 0.58 |
| CARRIER | capital | 425 | 56.1 | 50 | 60 s | 0.18 / 0.08 |
| WARRIOR | heavy | 300 (200) | 74.7 | 50 | 60 s | 1.30 / 0.48 |
| FREIGHTER | freighter | 450 | 65.0 | 53.5 | 56 s | 0.55 / 0.21 |
| DESTROYER | capital | 395 | 61.7 | 55 | 55 s | 0.66 / 0.27 |
| DART | light | 200 (120) | 70.1 | 61 | 49 s | 1.40 / 0.72 |
| WRAITH | light | 220 (120) | 77.1 | 64 | 47 s | 1.48 / 0.60 |
| ECHO | light | 180 (120) | 77.4 | 72 | 42 s | 1.34 / 0.70 |

**Summary.**
- The median realistic DPS is **50** and the mean is 53. The spread is 43-72, **1.67x** (v1: 1.6x).
- **Lights stay on top.** The lowest light is 61; the highest non-light is 55 (DD).
- Moves from v1:
  - FR +7, from Time on target.
  - DD +2, because the Grapnel keeps the Lance lined up.
  - Sniper −2, because Designate left.
  - Warrior −4, because Riposte left.
  - Dart +2 realistic, though −23.7 sheet (the Pepperbox is the mid band, and the rod is repriced).
- **Chips:** with raw 3/4/5/6 counts, lights gain x1.48 and capitals x1.24, so the Echo reaches about 107. Decision 1 closes that.
- **Slipstream:** a Racing-Drive Dart at 325 u/s has an effective 286 hull. But hull parts cost top speed (Bulwark −15%, Braced −10%) and drop it back under 325, so Slipstream replaces hull gear rather than stacking with it.

**What the boss must be.**

**1. Hull.**
- L1 **3000** solo (median 50 x 60 s). v1 had 2950; today's are 760 (Lancer) and 700 (Drake).
- Per level: **x S(L) = 1.025^(L-1)**. At bounty pace that keeps the kill at 48-61 s to L40 (player_model §5).
- Party: x(1 + 0.6(P − 1)), unchanged.

**2. Damage at L1: DamageMult stays 1.0.**
- Hulls rose by tier: heavies x1.50; lights x1.50 / 1.67 / 1.83; capitals and freighters x1.00.
- A uniform rise that restored the lights' and heavies' threat would hit the capitals by the same x1.5-1.8.

**3. Supers per hit** (burn 250 after fix B; rock 250), as a share of hull:

| tier | hull | share | result |
|---|---|---|---|
| lights | 180-220 | 1.14-1.39 | every light dies to one |
| Sniper | 240 | 1.04 | dies |
| Warden | 270 | 0.93 | 20 left |
| Warrior | 300 | 0.83 | 50 left; 200 with the stance up |
| freighters | 380-450 | 0.56-0.66 | |
| capitals | 395-500 | 0.50-0.63 | |

**4. The boss's damage is mis-shaped, not too low.** Realistic incoming DPS by the pilot's range:

| pilot range | Lancer today | Drake today | target |
|---|---|---|---|
| ≤ 340 u | 8.0 | 3.9 | **5.1** |
| 340-900 u | 6.7 | 3.9 | 5.1 |
| 900-1100 u | 3.4 | 3.9 | 5.1 |
| 1100-1920 u | 3.4 | 2.7 | 5.1 |
| > 1920 u | 1.9 | 2.7 | 5.1 |

- **Why 5.1:** 5.1 = 0.015 x the median v2 hull (340). That is a 60% net loss over a 60 s kill.
- **The lever:** move the Lancer's 900 u hitscan guns (3.0 DPS, 45% of its mid-range output) into moves that reach 1000-3500 u. This is the boss batch's target, not its design.

**5. The level curve is what must rise.** Pilot hull outgrows S(L), so boss damage must sit above S(L) to hold L1's threat:

| gear | L5 | L10 | L20 | L30 | L40 |
|---|---|---|---|---|---|
| one hull part | x1.47 | x1.37 | x1.33 | x1.24 | x1.02 |
| two hull parts | x1.83 | x1.74 | x1.79 | x1.73 | x1.44 |

**Default:** about **x1.35 over S(L) from L5 to L20**, easing to x1.0 by L40.

---

## 5 · FOUNDATIONS

Costs are in S-units: XS 0.25 · S 1 · M 2.5 · L 5. Status: kept, changed, **DROPPED** or **NEW**.

| id | foundation | v2 status | serves | cost |
|---|---|---|---|---|
| **B** | **Burn clock** (live bug): Boss.cs:478 `s.Next = m.Tick` throws away the overshoot, so a full burn is 4 or 5 ticks (200 or 250) depending on frames. The fix is `s.Next += m.Tick`: at the harness's `--fixed-fps 60` it is then 5 ticks = 250 every run | **NEW** | every beam; the 240/250 hull witnesses | XS |
| F0 | Honest stats | kept | every rate or speed ability; Dart pricing | S |
| F1 | Lifts, additive | changed: **+ Add** (a flat term summed after the multiplier); + an anchored-line damage lift, the prism speed lift, and the sprint thrust/speed lifts; Afterburner DROPPED | CIWS, Brace, Anchor, Overdrive, Reverb, sprint, Veil, Whirlwind, Prism | M + XS |
| F2 | Status registry | changed: Exposed **DROPPED**. Parrying = **32**, the one new wire bit (32 is free, Statuses.cs:11). Host-only, never on the wire: Suppressed 64, Dazzled 128, Jammed 256, Slowed 512 | Brace, Lunge, Slipstream, Prism, the hostile statuses | M |
| F3 | Choosing vs hitting, plus the live solo boss freeze (Boss.cs:261-267) | changed: only Veil uses it now (Smoke and the Decoy are DROPPED) | Veil | S |
| F4 | Hostile damage door + credit + DealtBy per weapon | changed: Exposed out; the Beacon's x1.5 (CalledBy == dealer) in | Buster, flak x0.75, Backstab, Venom, Veil break, Reverb, the Suppress tag, the Beacon; credit for Curtain, Time on target, hurl | M |
| F5 | One Shot.Strike rewrite | changed. Rows: **Pepper** (replaces Rod), Penetrator, Buster, Pellet, EchoRound (no Guided; may fold into Shell), Flak, Spotter, Reflect, SiegeMissile. Flags: + **Decoyable**, + **Command** | Dart, Bastion, Wraith, Warden, Freighter, Warrior, Echo, Sniper | M |
| F6 | Blasts | changed: the depth-charge row is **DROPPED**; + NetIds on predicted missiles | the mortar; Flares | M |
| F7 | Primaries | changed: + the `shell_turn` row | all 12 | M |
| F8 | Helm, wards, press payload | changed: + the Grapnel swing (owner-side constraint, host confirm) | Slingshot, rod recoil, Rewind, Shadow step, Lunge, Whirlwind, Grapnel, gunship targets[] | M |
| F9 | Fields, zones, marks | changed. **DROPPED:** smoke clouds, the Aegis line, the ping and designate marks. Zones: the well, the Jetwash path (the smoke's type), the Curtain Bar. One speed share on Raider's speed lines. Marks: paint, venom, backstab, chevron, jam, dazzle, grapnel line, beacon ring | Bubble, Repair, Overdrive, CIWS, well, Jetwash, Curtain, every mark | M |
| F10 | Mend | kept | lance, Repair field | S |
| F11 | Blow kind + **Prism** | changed: the 0.5 s blow buffer and per-peer RTT are **DROPPED**; + Prism.cs, the Burn walk, and warn_beam | Prism stance | L |
| F12 | Melee arc | kept; + a guard clamp shared with the prism | Blade, Whirlwind, Lunge | M |
| F13 | Wing orbit | kept | gunships (now on E) | L |
| F14 | Owned bodies | changed: the decoy body (L) is **DROPPED**. Kept: the sentry filter + Prefer (S), now also for emplacement choosers; the tether mine (S). **NEW:** the flares spawn row, `NetDecoy(netId, point)` and point guidance (M); `Raider.Call` | Freighter, Sniper, Warden | S + S + M (v1: S + S + L) |
| F15 | Chips per class | kept (a save change) | all 12 | S-M |
| F16 | Passive PD | kept | 7 PD hulls | XS |
| F17 | **Outgoing door.** One function for all hostile damage: Boss.Out(move) replaces the six `m.Damage * DamageMult` sites (Boss.cs:442, 452, 462, 482, 488, 561), plus Raider.Strike (Raider.cs:190), the heavy's throw (:331) and the Emplacement gun (Emplacements.cs:167). It reads `StatusSet.OutGuards` rows (the table below) | **NEW** | Suppress, Flares, EMP; any later enemy debuff is a row | S |
| F18 | **OnDealt hook:** `AbilityDef.OnDealt(ship, target, d, weapon)` for running rows, fed by F4. It deletes the echo arm in NoteDealt (PlayerShip.cs:193-198) | **NEW** | Suppress, Reverb, Venom | XS |
| F19 | **Tow and hurl:** a host Towed/Flung state checked before the raider's AI (beside Disabled, Raider.cs:204). The hurl is swept with Shots.Sweep + Covers, and the first body ends it. Uses Targeting.Immovable | **NEW** | Grapnel | S |
| F20 | **Enemy rows:** the twin-laser field; MissileFlight moved off the const (Raider.cs:78) | **NEW** | enemy heavies | XS |
| — | **Small rows:** `Slot.At` (EMP; replaces `_echoAt`) · `AbilityDef.Forces` (sprint) · `IRaidTarget.Calls` (Beacon) · `ShotDef.Command` (Pepperbox) | **NEW** | | 3 x XS + S |
| H | Harness: Around(), the DealtBy probe, the heal measurement, the latency knob, witness rows | kept; + rows for 11 new pieces | every Proves line | M |

**OutGuards rows (F17).** The three "weaken what shoots" statuses are one table, not three gates:

| status | its guns vs craft and structures | boss non-super | super | missile throw | new latch | existing latch |
|---|---|---|---|---|---|---|
| Suppressed (DD) | x0.5 | x0.7 | x1.0 | held | yes | kept |
| Dazzled (SN) | x1.0 | bosses immune | | held | **no** | kept |
| Jammed (EC) | x0 | bosses immune | | held | **no** | **dropped** |

"Held" means the clock keeps its zero and the launcher throws at the lapse.

**DROPPED from v1, in one list:**
- Smoke (F9 clouds, F3's user) · the depth-charge row (F6) · the Aegis line (F9) · the decoy body (F14, an L).
- Exposed (F2/F4) · the blow buffer and per-peer RTT (F11) · Riposte and the parry window.
- Afterburner `burn_*` · the Mass driver Rod row · Guided on EchoRound · `rush_guard` · `emp_damage` and its Dps row · `_echoAt` · the drafted Zones.Grounds.

**Build order.** Each slice is committed with its checks.
1. **Land now, no sign-off needed:** F0, F3, F16, F2, B, and the stale strength and missile comments. Re-baseline the sheet.
2. F4 + F17 + F18, F1 (+ Add), F20.
3. F5, then F6, then F7.
4. F8, F10, F9.
5. F12, F11, F13, F14, F19, F15.
6. Kits by tier: capitals (the CV key, DD), freighters (FR), heavies, lights. Each tier's witness rows go in the same edit, proved at rung 3.
   - Rung 5 runs once after the heavies (Suppress, Grapnel, Time on target, Prism, Flares, Beacon) and once after the lights (Pepperbox, sprint, EMP).
   - Rung 4 runs once: the prism wedge, clip and fan; Jetwash; the Curtain; the flares; the 3-ability bar; the chip slots.

**Total:** about **125 S-units** (v1: about 120). Foundations are about 49, class rows about 46, and witnesses and literals about 30.

---

## 6 · DECISIONS STILL OPEN (say nothing and the default stands)

| # | fork | default | alternative |
|---|---|---|---|
| 1 | Chips against the damage ladder (v1 decision 3) | **keep 3/4/5/6 slots, at most 3 Combat Chips on any hull**; the extra slots take Engine, Armour or Targeting | chip strength by tier (capital x1.6, freighter x1.2, heavy x1.0, light x0.8) |
| 2 | "Twin laser at 1.25x a light": the pair, or each barrel? | **the pair**: 1.25 DPS per heavy (today 2.0 / 2.6 / 1.8) · missile 35 · flight 10 s | each barrel at 1.25x the light mean: 2.58 DPS per heavy |
| 3 | Dart Q. You did not ask to remove the Afterburner | **Jetwash**: the sprint IS the Afterburner, folded into the rod | keep a speed button: Ramjet (at full throttle, +10% top per second up to +50%; turning bleeds it) |
| 4 | Sniper after Designate (outside your notes) | **the Anchor absorbs it**: anchored full charges x1.25, cooldown 12 -> 8 s · 43 realistic, 70 s kill | the v1 Anchor: 34 realistic, 88 s kill |
| 5 | Supers against the new hulls | **unchanged** (burn 250, rock 250) | x1.2 (tick 60, burn 300, rock 300): every heavy one-shot; capitals lose 0.60-0.79 per super |
| 6 | Freighter R | **drop under the hull** (v1) | launch to the cursor, up to 600 u in 0.8 s; R over one of yours recalls it (S) |
| 7 | Cheaper fallbacks if a card is refused | **the cards as written** | DD: Bow-on (x0.4 from within 45° of the bow, 6 s, cooldown 20 s) + Hard over (2 s of x3 rudder, cooldown 10 s) · FR F: Unmask (2 hidden mounts at 15 DPS, 8 s, cooldown 24 s) |

---

## 7 · RISKS

1. **The prism across peers** (F11, L).
   - The failure: a guest must see the beam stop at the Warrior, with its children leaving where the host judged them. Everyone draws from the Parrying bit, AimPoint (20 Hz) and a flat boss pose (30 Hz). Near θ = 15° and 60° the owner's cursor and the host's copy of it can disagree on the band, so a guest sees SQUARE while the host deals SLANT.
   - **Mitigation:** the host alone deals damage. The resolved band rides the prism slot's N on the existing slot replication (no RPC), and every peer draws that band. Rung 5 checks the draw against the host at θ {0, 30, 45}. Fix B lands first, so 250 is provable.
2. **Owner-side motion racing the host.**
   - The Grapnel swing constrains the owner's hull to a circle round an anchor it sees at 10 Hz (30 Hz while the boss is Locked). A towed raider on a guest's screen lags the bow by about 0.1 s x speed (13 u at 130 u/s). The sprint's forced thrust, Slingshot, Rewind, Shadow step and the recoil all race a reliable press against unreliable positions.
   - The Pepperbox puts 6-9 reliable NetShot/s per Dart on the cosmetic channel, 36/s with four Darts.
   - **Mitigation:** the F8 payload, and the owner casts off if the host has not confirmed in 0.5 s. Rung 5 checks hull within 60 u. If the wire bites, send NetShot with a count (ripples of 3).
3. **Power and boss shape are estimated, not flown.**
   - The realistic column is a guess at uptime. Against today's boss the hull lost per kill runs 0.08 (CV vs the Drake) to 1.48 (Wraith vs the Lancer). Every light dies to one super.
   - **Mitigation:** the DealtBy probe plus Fly() give landed DPS per class at rung 3. The boss hull is calibrated to the median. The Lancer is reshaped toward 5.1 DPS in every band. Only the named levers move: Echo range 500 -> 400, the Anchor, the rod cap.

---

## 8 · CROSS-CHECK APPLIED

### 8.1 Overlaps

| pair | resolution |
|---|---|
| SN Flares' dazzle vs EC EMP jam | **Split by timing.** Flares prevent: no new latch, no throw, missiles lured. The EMP cures: webs dropped, guns silenced. Gate: `Latched = ... && (wasLatched \|\| !Dazzled)` |
| DA Jetwash vs WD Curtain grounding | **Grounding removed.** The Curtain is a damage wall; Jetwash is the only slow |
| DD Suppress vs Dazzle vs Jam | **Kept.** Suppress alone reaches bosses (x0.7, non-super) and structures, and works on hit. All three are rows of the F17 OutGuards table |
| BB CIWS vs the Warden's flak and Curtain | **Accepted.** CIWS is a close ring round the BB; the Warden works by placement and by calling |
| DD Grapnel tow vs SN Tether vs BA Shockwave | **Accepted.** The Grapnel takes one chosen craft and throws it; the swing is unique |
| FR Time on target vs BB Broadside | **Accepted.** Time on target converges scattered guns on a predicted point |
| DA Pepperbox vs WD Hunters | **Pass**, on the two conditions on the Dart card |
| names | The drafted NetLure is now **NetDecoy**. The Beacon's override is **Raider.Call**. The dd_* display names change (§3) |

### 8.2 Solo audit: the weakest solo ability of each class

| class | ability | alone, it... |
|---|---|---|
| BATTLESHIP | Brace | cuts its own damage taken |
| CARRIER | Scramble | relaunches its own deck |
| DESTROYER | Grapnel | swings bow-on round the boss, breaks its own pin, hurls |
| FREIGHTER | Bubble | covers itself and its sentries |
| TENDER | Resupply | cuts 8 s from its own Overdrive and Repair (+27% Overdrive uptime). The row text now says "you included" |
| TENDER | Mending lance, heal half | needs a friend, but the hauler and miners count, and the damage half works alone |
| BASTION | Gravity well | pulls raiders under its own mortar |
| WARRIOR | Prism stance | its own defence, plus 112.5 back into the boss |
| SNIPER | Flares | clears its own missiles and pins |
| WARDEN | Beacon | flushes the edge heavies and pulls pinners off the hauler; called craft take x1.5 from the Warden |
| DART | Jetwash | sheds its own chasers |
| ECHO | EMP | frees its own web |
| WRAITH | Backstab | a solo boss faces the Wraith itself, and a 200 u orbit at 260 u/s (1.3 rad/s) outruns the boss's 0.3 rad/s turn |

Removed because they failed the rule: the Aegis link (Warden), the ping's marks on allies' scopes (Echo), and the Smoke's hiding of the party (DD).

### 8.3 Source checks that corrected the drafts

| claim in a draft | verdict | source |
|---|---|---|
| a full burn is 250 | **200 or 250**, depending on frames; fix B makes it 5 ticks at the harness's 60 fps | Boss.cs:476-484; tools/smoketest/run.ps1:159, :169 |
| raider Strength is 1.1^(L-1), so the DD's raid saving is "6x at L20" | **1.025^(L-1)**, so 1.6x at L20; the missile is flat | Missions.cs:181-182; Waves.cs:180; Raider.cs:335-340 |
| Parrying and Suppressed both take bit 32 | Parrying = 32 on the wire; host-only statuses 64+ | Statuses.cs:10-24 |
| the twin laser is 2.58 per heavy (cross-check) | the Warden draft's "2.5" is two heavies at 1.25. v1's words, boss_adds and the heavies draft all read the pair at 1.25x a webifier. Decision 2 | Enemies.cs:28, :77-107 |
| missile 42 | 35 at the 10 s flight (v1 2A) | Enemies.cs:50; Raider.cs:78 |
| the DD saves "about 50 hull" per raid press | **about 30**: the missile is delayed (clock held), not cancelled | Raider.cs:330-334 |
| "the Warrior keeps 50, with the stance" | 50 is with **no** stance; with it he keeps 200 | Lancer.cs:56-57 |
| no new latch stops a pin completely | true: lights fire only while latched, and heavies only on a pinned target | Raider.cs:257-270, :309-320 |
| the Emplacement gun's damage site | Emplacements.cs:167 (Spec), not :185 | Emplacements.cs:164-169 |
