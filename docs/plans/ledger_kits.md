# Ledger: class-kits batch, lane A (combat core)

Writer: one agent in worktree `WarShips_wt_kits`, branch `wt/kits`, started at 03d47ba.
Spec: `kits_v31.md` §8 (Step 0 item 3, lane A slices 1-2), §6 foundations, v3 §5 (`kits_v3.md`),
v2 §5 (`kits_v2.md`). A PRE with no POST is an interrupted job: compare the files with the hashes,
revert or keep the half-made edits, then run the job again (CLAUDE.md §2b rule 3).

## Decisions taken where the spec is silent

(D-numbers; each is the plan's default or the smallest reading of it.)

- **D1** `raids_v2.md` is already in the repo, byte-identical, as `raids_squads_adds.md`. Not copied a
  second time (two copies is two truths); the README row and kits_v31's references name it instead.
- **D2** The copies are named `kits_v2.md` / `kits_v3.md` beside `kits_v31.md`; their text still says
  `signoff_v2.md` / `raids_v2.md` (unchanged copy), so each header maps the old names.
- **D3** Spec read for the code slices: kits_v2 §5 (foundations, OutGuards, build order) and the card
  sections a foundation names; kits_v3 §5 and §7; kits_v31 §6, §8, §10. The rest was diffed / skimmed
  by heading to keep this agent under its context cap.
- **D4** F2's new statuses (Parrying 32 on the wire; Suppressed 64, Dazzled 128, Jammed 256 host-only)
  and the host-only wire mask land with their first users (6a Suppress, 6c Prism/Flares, 6d EMP):
  enum members are under UNUSED ANYWHERE (tools/analyse/xref.py), so declaring them now fails rung 2.
- **D5** The share rule's HOLD half (F1) lands in slice 1, because the railgun lock needs it;
  Add and Ramp stay in slice 2.
- **D6** A hold multiplies the whole helm after the lifts' sum: both speed caps, both thrusts and the
  rudder, so x0 roots the hull heading included (the railgun keeps "cannot turn or thrust"). It is
  `AbilityDef.Hold` (a share on the row, 1 = none). If the 6c Anchor must turn while rooted, 6c
  splits the rudder off as its own row. Lane B puts the same hold on strafe_speed.
- **D7** Disabled on a pilot: no helm, no main guns (they keep reloading), no ability presses (owner
  Refuse says DISABLED; the host's DoAbility gate drops them; a Local toggle and the wreck's reboard
  still work), no warp; PD, the wing and turrets already out keep fighting.
- **D8** F16 (passive PD) is its own job (J3): it deletes the pd ability, window, recharge and
  Fit.AlwaysPd across ~25 harness lines, two of them rung-5 guest checks. The per-hull numbers (CV
  3 -> 2 mounts, Warden 10 -> 1 DPS) go with their cards in 6a / 6c, where the power table is
  re-witnessed.
- **D9** Hardened takes its share from the applier: `IStatused.ApplyStatus(s, seconds, share)`;
  with no share given, the Guards row's default (0.5). Two hardenings at once keep the stronger
  (lower) share until the status lapses. `StatusGuard.Stat` is deleted (its one user was rush_guard).

## Jobs

### J1 · PRE · specs into the repo
- Intent: copy v2 and v3 sign-offs into `docs/plans/` unchanged (one-line origin header), list them in
  the README table, repoint references to scratchpad paths, and add a short `docs/DESIGN.md` section
  pointing at them (kits_v31 §8 Step 0 item 3).
- Files: docs/plans/kits_v2.md (new), docs/plans/kits_v3.md (new), docs/plans/README.md,
  docs/plans/kits_v31.md, docs/plans/numbers_curve_raids_items.md, docs/DESIGN.md, this ledger.
- Start: 03d47baa504be57aad842eccdfff0b49218623bc
- Hashes: README.md 677dd2a1 · kits_v31.md 0b3f77b7 · DESIGN.md 8d15c0e2 ·
  numbers_curve_raids_items.md 72c3f80d

### J1 · POST
- Verdict: done (docs only; no code, no rung needed).
- Scratch `kits3/signoff_v31.md` vs repo `kits_v31.md`: the repo copy carries the 2026-09-25
  reconciliation (L1 3091/2847 -> 3222/2968; the x0.64 cut -> Lancer x0.744 / Drake x0.787; §5 moved
  to `numbers_curve_raids_items.md`; decision 15 reversed: no boss-hull trim for adds; decisions 6, 8,
  9, 14 marked settled; check 11's literals 1.075/3091 -> 1.098/3222) and the Echo Rewind ruling
  (8 s, hull too, 0.5 s ring of 16). Nothing else differs.
- Files: kits_v2.md, kits_v3.md (new); README.md (3 rows, raids row); kits_v31.md (lines 15, 39, 501,
  513); numbers_curve_raids_items.md (line 44); DESIGN.md (one pointer section under Ship classes).
- Next: J2, lane A slice 1.

### J2 · PRE · slice 1a: B, F2 (with the hold), the stale comments
- Intent: B `s.Next += m.Tick` (Boss.cs Burn); F2 per D4/D6/D7/D9 (railgun lock -> `Hold = 0`);
  stale comments Raider.cs:17-20 (7 s -> 12 s flight), :48 and Waves.cs:174 (1.1 -> 1.025 = Missions.S),
  Enemies.cs:44 (7 s). Checks in lane A's own methods: `BurnClockChecks` (called from the Lancer
  arena, after the fallback beams), `HoldAndDisabledChecks`; rewrites of the sniper's charge checks
  (Status.Disabled -> the hold) and the Hardened share check.
- Files: scripts/Boss.cs, Statuses.cs, PlayerShip.cs, Abilities.cs, Targeting.cs, Raider.cs, Waves.cs,
  Enemies.cs, Deployed.cs, Emplacements.cs, UtilityShip.cs; tools/smoketest/SmokeTest.cs.txt;
  docs/CHANGES.md.
- Start: e4bd15156a6302a7fd2ae133afd7c8b8990deb32
- Hashes: Boss 28201da9 · Statuses ea97ced8 · PlayerShip 6feada71 · Abilities 8807eee5 ·
  Targeting cf2e7399 · Raider 11393eb2 · Waves 3a5fd6f9 · Enemies 9412635e · Deployed 9fee7444 ·
  Emplacements b7df20d3 · UtilityShip 83d1ab96 · SmokeTest 00e6f1ba · CHANGES 3631a5f6

### J2 · POST
- Verdict: rung 1 (typecheck, 0 errors) and rung 2 (`verify.ps1 -Quick`, ALL CHECKS PASSED, UNUSED 0)
  green in the worktree. Rung 3 NOT run (no engine runs in this lane).
- Files: Boss.cs (Burn `+=`), Statuses.cs (share from the applier, StatusGuard.Stat gone),
  Targeting.cs + the six IStatused implementers (share parameter), PlayerShip.cs (Held, Steer's
  hold, PressHeld, FireControl and CanWarp gates, the rush's share, ChargeRail without Disabled),
  Abilities.cs (`Hold`; railgun `Hold = 0`), Raider.cs / Waves.cs / Enemies.cs (stale comments),
  SmokeTest.cs.txt, CHANGES.md.
- Checkpoint: the commit after this entry ("Kits lane A slice 1a").
- Next: J3 = F16 passive PD (D8), then slice 2.

### J2b · PRE · the burn clock's knife edge (second agent)
- Intent: the burn's 13th judgement and its end were two clocks meeting at 3.0 s (Next <= 0 vs
  T < 0), so the 5th hit of `LaneABurnClockChecks` sat on the float's edge. Count the judgements
  (D10) so the last one ends the burn; the check asserts 5 hits / 0.75 s / 250 AND nothing more in
  a window ending 0.4 s past the burn. Also the natural-fight beam (Lancer arena): its sidestep at
  2.2 s is 2 frames before the new clock's 4th hit (2.233 s) and its `200..350` range is the old
  clock's truth (J2 left it: it would read 150/200) -> sidestep at 1.9 s, exactly 3 hits = 150.
- Files: scripts/Boss.cs, tools/smoketest/SmokeTest.cs.txt, docs/CHANGES.md, this ledger.
- Start: 4d09868cda2dc97ef0abdaa3e7b041fcdb06d43f
- Hashes: Boss 6c56402f · SmokeTest de2ec320 · CHANGES c3298075

### J2b · POST
- Verdict: rung 2 (`verify.ps1 -Quick`) ALL CHECKS PASSED, UNUSED 0. No engine rung.
- Files: Boss.cs (`BossMove.Judgements`, `Slot.Left`; Land sets the count, Burn's last judgement
  ends the burn, `Slot.T` no longer times a beam); SmokeTest (`LaneABurnClockChecks` window +
  burn length; the Lancer arena's sidestep 2.2 -> 1.9 s and its damage check 200-350 -> 150);
  CHANGES.md (entry; the 1a entry's knife-edge note deleted).
- **D10** The knife edge was in the GAME, not only the check: the 13th judgement and the burn's end
  were two accumulated clocks meeting at 3.0 s. The burn counts its judgements
  (floor(Live / Tick + 1e-6) + 1, a constant from the row) and its last one ends it.
- Checkpoint: the commit after this entry. Next: J3 (F16).

### J3 · PRE · F16, passive point defence
- Intent: PD fires whenever its ship is alive, no key: delete `Ab.Pd` + the six class-row entries,
  `Fit.AlwaysPd`, rows pd_active / pd_reload, PlayerShip's Pd window members + `StartPd` + Die's
  close, `ITurretHost.PdRing` + 5 implementers + `Turret._Draw` + `TurretSpec.Ring` +
  `ClassArt.PdRing` (13 art rows), `Stats.PdDuty` / `PdSustainedDps` (-> `PdDps`). Restate
  fr_endurance / fr_armour on PD stats (D11). Per-hull numbers unchanged (D8). Harness: rewrite every
  pd-key / window / ring site (incl. rung-5 8991 and 9325), the Lancer arena's "no point defence"
  (range zeroed, test only), the passive-PD fallout the audit agent reports; new `LaneAPassivePdChecks`.
  Shots: drop the dead Q presses, frame 23 renamed. Docs: CHANGES Handoff key table, DESIGN 106/553.
- Files: scripts/Abilities.cs, Ships.cs, Stats.cs, PlayerShip.cs, Turrets.cs, Deployed.cs,
  Emplacements.cs, Hauler.cs, Lanes.cs, Equipment.cs; tools/smoketest/SmokeTest.cs.txt;
  tools/screens/Shots.cs.txt; docs/CHANGES.md; docs/DESIGN.md; this ledger.
- Start: b178a9033e031b97ea4f4c7ae72c21b6817dc1bd
- Hashes: Abilities 382300d7 · Ships d9464120 · Stats c7a5e69a · PlayerShip b05be6b6 ·
  Turrets f86052a8 · Deployed 3009ebda · Emplacements e0b836e3 · Hauler bbc72c9e · Lanes 0125e73a ·
  Equipment a2c659c3 · SmokeTest 1cdcdf11 · Shots 809555bf · CHANGES 42426021 · DESIGN 375fd574

### J3 · POST
- Verdict: rung 2 (`verify.ps1 -Quick`) ALL CHECKS PASSED (typecheck, build, analysers, xref,
  UNUSED 0, checks-with-code). No engine rung.
- Files: as the PRE, all touched. A read-only audit agent swept the harness for passive-PD fallout;
  its HIGH/LOW findings were fixed with `PdReachOff` (below) except three LOW ones left to rung 3
  (the carrier's raider checks ~4780-4900 and the armed dummy's seeker ~5000: each light loses an
  estimated 8-12 of 25 hull to PD, so they should hold) and two trivially-still-true ones (the menu
  diorama's counts at 0.4 s; the beam-notes `P("point_defence") > 0`).
- **D11** `PlayerShip.PdOnline => Alive`: a wreck's mounts are quiet, as the closed window made them
  for every hull but the warden (whose AlwaysPd fired from a wreck). Disabled does not stop PD (D7).
- **D12** Endurance Frame: +30% pd_interval (shorter reload), +30% pd_turn; Armoured Frame: +20% hull,
  -20% pd_interval. Both stay PD-only / everything-fitting, so every Wear count is unchanged.
- **D13** PD cannot be switched off, so a check that was written with PD off takes the hull's
  `pd_range` base to 0 for the while and restores it (`PdReachOff`, lane A's section of SmokeTest).
  Used: the xp missile-before-light pick (until both spawn), the Lancer arena from the "left alone"
  escorts to the kill, the siege's main-gun kill, the bastion's wave, the carrier's wing retarget,
  the freighter's dropped turret, the hauler pad, and the session host (rest of its role).
- **D14** Rung 5: the host sets a held light raider beside the guest's carrier (after "guest's wing
  hit the dummy") and checks the host's copy of the guest's mounts take it; it lives 20 s. The guest
  samples every frame, from 1 s after its Space/F until after "fighters at the target" (+ up to
  10 s), for a flash leaving one of its own PD mounts (`H.Flashes`, within 40 u of a mount).
- Merge note: `ClassArt.PdRing` is gone from all 13 art rows in Ships.cs; the art lane (wt_art)
  edits those rows too -- whichever merges second drops `PdRing = x` from its lines.
- Checkpoint: the commit after this entry. Next: slice 2 (a FRESH agent: this one passed ~150k).

## Engine rungs owed to the main session (run in the worktree, rebased, one engine at a time)

| after | rung | seeds | look for (PASS lines) |
|---|---|---|---|
| J2 slice 1a | 3 (`tools\smoketest\run.ps1 -Solo`) | two different seeds | "the burn clock:" x3 · "the railgun's charge is a hold of x0" x3 · "a DISABLED warden" x3 · "Hardened at the applier's share" x3 · rewritten: "the railgun charges with the hull held at x0", "then its railgun's whole", the ability sweep's railgun row, "a 100 blow on a HARDENED battleship" |
| J2b knife edge | 3 (`-Solo`) | two different seeds | "the burn clock:" x3 (now "... and nothing in the 0.4 s after it"; burned 3.000 +- 0.017 s) · "the beam, judged every 0.25 s for 50, through the 0.52 s guard: 3 hits ... 150" · unchanged neighbours "the live beam holds the line it drew", "and the line is still drawn 2 s into its 3 s burn", "the ram, due mid-beam, waits for the beam to end" |
| J3 F16 | 3 (`-Solo`) | two different seeds | new: "every hull that mounts point defence is in the passive-PD table", 7 x "...: point defence with nothing pressed and no key for it". Rewritten: "PD fires with nothing pressed", "each battleship PD turret picks its own target", "no window and no recharge: 15.5 to 17.5 s on", "Point defence: 2.00 DPS ... = 64.65", "keys tab lists the battleship's 3 abilities + 6 open slots", "Esc cancels a capture", the BB / DD bar lines, "carrier PD: three turrets on three different LIGHT targets", "a hunter called off while the fighters and point defence are on it", "the warden's point defence is on with nothing pressed -- ... 10 DPS", the sustained-total pair (freighter 50, carrier), the 12 fittings-sweep lines, "every ability on every bar has a witness", the pd_range reach row, "a battleship's point defence picks a cruise missile ... before a light raider", "point defence, with nothing pressed, shoots the boss's missiles down", "left alone (no point defence), the escorts pin the pilot", the siege's "falls to the battleship's MAIN GUNS ... point defence out of reach", "a 1000 u shockwave throws nothing in flight", "a carrier's wing sent at a raider that dies", "a turret left standing shoots what comes near it", the hauler pad's three. Watch (unchanged but now with PD on): the carrier raider checks (webifier DPS 3.0 +- 0.7, the gunship's 700% burn and laser on a pinned carrier), the armed dummy's 50 hit |
| J3 F16 | 4 (`tools\screens\run.ps1`) | - | LINT 0; read by eye: 4_hub_battleship (PD firing, no ring), 7_hub_carrier_strike, 23_bar_battleship_cooldown (renamed: no PD slot on the bar), one close-up (no ring round a PD mount) |
| J3 F16 | 5 (`tools\smoketest\run.ps1`) | - | host "a guest's point defence fires on the host with nothing pressed"; guest "guest sees its own point defence fire with nothing pressed and no key for it"; unchanged neighbours "guest's bomber strike launched real torpedoes on the host", "the host's raiders reached this guest" |

## Handover 2: the second agent stopped after J3 (context cap), at a job boundary

Nothing in flight: J2b and J3 have POSTs; the tree is clean at the J3 commit. The next agent starts
SLICE 2 with a PRE entry, from the map's last paragraph below ("Then slice 2"); J3's map above is
DONE (kept for the record of what it touched). Split slice 2 into jobs that share files, e.g.
J4 = F4 + F17 + F18 (PlayerShip.Incoming / Guarded / NoteDealt, Statuses.cs), J5 = F1 Add + Ramp
(AbilityDef, PlayerShip.LiftShares), J6 = F20 (2.58, one edit with the raids' EnemyDef rows Cc, Exp;
MissileFlight onto the row). `PdReachOff` (SmokeTest, lane A section) is the way to hold a hull's
point defence out of a check.

## Handover 1: the first agent stopped here (context cap), at a job boundary

Nothing is in flight: J1 and J2 have POSTs, the tree is clean at the J2 commit. The next agent starts
J3 with a PRE entry. Map of what J3 touches (found by J2's greps; line numbers at 38401be):

**J3 · F16, passive point defence** (v2: "PD is passive: 1 DPS per node"; D8 keeps per-hull numbers).
- Delete: `Ab.Pd` (Abilities.cs ~119-127) and `Ab.Pd` from six class rows (Ships.cs BB 179, CV 204,
  DD 230, FR 270, TE 306, BA 343); `Fit.AlwaysPd` (Ships.cs:42, the Warden's Fit 408); the rows
  `pd_active` / `pd_reload` (Stats.cs:178-179); PlayerShip `PdActive/PdReady/PdLeft/PdRechargeLeft/
  PdActiveFrac/PdRechargeFrac` (182-187), `StartPd` (~505), `Sl("pd").Left = 0` (~988);
  `PdOnline` becomes `Stats.Def.Has(Fit.Pd)`; `ITurretHost.PdRing` and every `PdRing => 0f`
  (Turrets.cs:74, 273-276 ring draw; Deployed/Emplacements/Hauler/Lanes) and `ClassArt.PdRing` +
  `Spec.Ring` if the ring then has no reader; `Stats.PdDuty` (234-242, note at 316) collapses to 1.
- Equipment.cs:367/369 `fr_endurance` / `fr_armour` move pd_active/pd_reload: restate on PD stats
  only (e.g. pd_damage/pd_range) so every hull's Wear count (SmokeTest ~3954) is unchanged.
- Harness rewrites (grep `Ab\.Pd|"pd"|PdActive|PdReady|PdRecharge|pd_active|pd_reload|AlwaysPd|
  PdRing|PdDuty`): 2568, 2886-2887 (key-capture example uses "pd" on Q: pick another BB ability),
  2942 and 3194 (ability id lists), 3125-3135 (the window/recharge block -> PD fires with nothing
  pressed), 3535/3541, 3954 (`Fit.Pd ? 9`), 5617 (warden), 5866/5872, 5943 (sweep row), 5993-5997,
  6137/6149, 6832/6848 (Lancer arena: "without point defence" must now kill or hold the PD another
  way, e.g. a pilot class with no Fit.Pd or the escorts out of PD reach), 7655, and the rung-5 pair
  8838 ("guest's PD activation reached the host") and 9172-9173 ("guest sees its PD window") ->
  a guest's PD fires with nothing pressed, host-decided.
- New check: from 3 varied spots, every PD hull's point defence takes a light raider in reach
  with no key pressed, at 1 DPS a mount (pd_damage / pd_interval literal 0.5 / 0.5).
- Rungs owed after J3: 3 on two seeds, and it adds to rung 5's first run (the guest PD check).

**Then slice 2** (§8): F4 + F17 + F18; F1 (+ Add, + Ramp; the hold half is in J2); F20 (2.58, with the
raids' EnemyDef rows Cc, Exp; MissileFlight onto the row). Specs: kits_v2.md §5 (F17 OutGuards
table, F18 deletes the echo arm in NoteDealt), kits_v3.md §5, kits_v31.md §6.
