# DESTROYER

## Identity
Capital. Hull 395. Top 117 u/s (thrust 63, reverse 27/40.5, turn radius 95, turn rate 1.2) — fastest of the
three capitals, outruns the Lancer's bow until L21 (kits_v31 §3.5). Never strafes. Role: "hooks, hauls in,
circles, torpedoes it" (kits_v31 §2). Drive: **WARP** (capital-only, README/kits_v31 §3.4): 1.0 s spool,
range grows 800 u/s (warp_rate), safe to 2400 u (warp_safe, 4.0 s hold), overshoot +900 u, disabled 2 s /
300 u over, cooldown 20 s (warp_cooldown). A DD warp casts off the Grapnel **with** a rip; an anchor that
itself dies or warps casts off with none.

## Primary — Director battery (Space, Hold)
2 turrets, 11.25 dmg every 0.5 s each = **45.0 DPS** sheet, 700 u range, shells 760 u/s, turret turn 2.09
rad/s. With a hostile **selected** within 840 u (`director_lead` = 1.2 × main_range) the guns lead it
themselves (aim on the target's velocity); otherwise they follow the cursor. Source: kits6a A6a-7 (v1's
numbers, unchanged); the lead ("Directed") is new here, built as `PlayerShip.Directed`.

## Abilities (F L1 → Q L3 → E L6)

**F — Long Lance.** Key F, cooldown 18.0 s (`lance_cooldown`). One torpedo straight off the bow along the
heading: 300 dmg (`lance_damage`), 170 u/s (`lance_speed`), 3000 u run (`lance_range`), unguided, stops on
the first hostile it touches, never a missile. Source: kits_v2/v3 (replaces the old missile burst; DD's
FireMode, missiles and reload "leave"), numbers kits6a A6a-7. A slow, heavy single shot needing the hull
led onto the target.

**Q — Suppressing fire.** Key Q, cooldown 20.0 s (`suppress_cooldown`) from the press. For 6 s
(`suppress_window`) every hostile the DIRECTOR BATTERY's shells hit (not the Lance, not PD) is Suppressed
until 3 s after its last hit (`suppress_time`): its guns deal x0.5, a boss's non-super moves x0.7, supers
stay x1.0, and it throws no predicted missile until the lapse (StatusSet.OutGuards: Gun 0.5, Move 0.7,
Super 1.0, HoldsThrow). A grey chevron marks it on every peer. Source:
kits_v2 §3 (unchanged since v2), numbers kits6a A6a-8. What it does: a soft-CC field the main guns paint
onto whatever they hit, cutting a group's return fire mid-engagement.

**E — Grapnel.** Key E, cooldown 16.0 s (`grapnel_cooldown`) from cast-off. Needs a selected hostile within
700 u (`grapnel_reach`). A boss, structure or dummy (Immovable): 0.15 s bite (`grapnel_bite`), winched in at
450 u/s (`grapnel_pull`) to 250 u off the hull (`grapnel_stop`, never nearer than `grapnel_clear` 150 u),
then swings bow-on up to 5 s (`grapnel_swing`, A/D round it, W/S reel at 100 u/s). A craft: towed 160 u off
the bow in 0.5 s, then hurled 600 u/s up to 900 u, 60/120 dmg each way. **Casting off an anchor's swing
rips 1% of its own maximum hull + 10** (`grapnel_rip_share` 0.01, `grapnel_rip_flat` 10) — e.g. 42.2 on the
L1 Lancer. Applies to bosses/structures/dummies, never a craft; none if the anchor died or warped; a
second E, the 5 s limit, a web and the DD's own warp all rip (kits_v31 §3.3). Refused NO TARGET / OUT OF
RANGE / NO HOLD (a missile) / WEBBED (swing
only) / COOLING. Source: README (quoted above, in rulings), kits_v31 §3.3 (the one change from v3: the
rip now deals damage), numbers kits6a A6a-9.

## Interactions (§7)
- **DD rip vs BA Bunker buster**: accepted, flagged — both add boss/structure damage; the rip is **the
  only damage in the game read from the target's own hull**, ~3% of every kill at every level, scaling
  with the party (lever: `grapnel_rip_share`).
- **Capitals' warp vs web-breakers**: a DD jump drops any web on it (latch within 12 u); an anchor's own
  warp casts off the Grapnel with no rip.

## Rulings (docs/plans/README.md)
- "hulls: BB 500 / CV 425 / DD 395." · "WARP... is CAPITAL-ONLY (BB, CV, DD). Capitals are slower overall
  and rely on it."
- "Destroyer Grapnel PULLS the destroyer to the target and tears a chunk off stations, bosses and pylons: a
  visual AND a hit of 1% of the target's total hull + 10."
- numbers_curve_raids_items.md §8 R4: the rip is "a share of the boss's total hull... never reaches craft"
  — this is the rip's contribution to the DD's realistic-DPS figure vs the L1 Lancer, not a redefinition of
  where it applies (kits_v31 §3.3's bosses/structures/dummies stands, matched by the built checks).
- Level walls: F Long Lance L1, Q Suppressing fire L3, E Grapnel L6.

## BUILT
`scripts/Ships.cs` `ShipClass.Destroyer`: `Drive = Drives.Warp`; `Nums` hull 395, thrust 63, max_speed 117,
turn_radius 95, turn_rate 1.2, main_count 2, main_damage 11.25, main_interval 0.5, main_range 700,
shell_speed 760, main_turn 2.09, pd_count 2; `Rows` director_lead 1.2, lance_damage/speed/range/cooldown
(300/170/3000/18), suppress_window/time/cooldown (6/3/20), grapnel_reach/bite/pull/stop/clear/reel/swing/
cooldown/rip_share/rip_flat (700/0.15/450/250/150/100/5/16/0.01/10); `Abilities = { Ab.Guns, Ab.LongLance,
Ab.Suppress, Ab.Grapnel }` (learn order F Q E, matches the walls). Ability ids: `longlance`, `suppress`,
`grapnel`. Shots row: `Shots.LongLance`.

Check methods (all in `tools/smoketest/SmokeTest.cs.txt`): `LaneA6aDirectorChecks`, `LaneA6aLanceChecks`,
`LaneA6aLanceHitChecks`, `LaneA6aSuppressChecks`, `LaneA6aSuppressWeaponChecks`, `LaneA6aSuppressGunChecks`,
`LaneA6aGrapnelPullChecks`, `LaneA6aGrapnelRipChecks`, `LaneA6aGrapnelTowChecks`, rung 5
`LaneA6aSuppressHostChecks`/`GuestChecks`, `LaneA6aGrapnelGuestChecks`, `LaneA6aGrapnelPartyRipChecks`.
Frames: `85d_dd_suppressed`, `85e_dd_grapnel_rip`, `43_lance_away`, `43b_lance_closeup`. Warp/strafe
(shared BB/CV): `LaneBWarpChecks`, `LaneBStrafeChecks`.

## OPEN
- **Ability name vs code id.** Every spec source names this weapon "Long Lance." The built id is
  `longlance`, not `lance` — the Tender's own beam already holds `lance`/`Ab.Lance`. A documented,
  intentional rename to dodge a wire clash (ledger_kits6a.md, version-l merge note), but no spec file
  itself says so.
- **The rip's sound.** kits_v3 §3.2 lists a `grapnel_rip` tearing-metal sound row (`make_sounds.py`,
  pitched down on bosses). Not built: the rip is silent (ledger_kits6a.md A6a-14, owed to lane D/sounds).
  The visual (chunk, sparks, smoke, scar) is built; the named sound is not.
