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

### J3b · PRE · kits job 1b: the four rung-3 fails at ad19fd8
- Intent: rung 3 at ad19fd8 (seed 11400714819323522083) failed 4: (1) "carrier PD: three turrets on
  three different LIGHT targets", (2) "three lights, 1 DPS each (1.40 DPS)", (3+4) "a DISABLED warden
  at 160 / -4 deg ... turn it 7.92 deg". Fix each at its cause in the game where the game is wrong
  (Disabled's heading in Steer; a PD mount's hold that never re-picks now that no window starts a
  fresh pick), the geometry where the check is (PD out of reach while the lights' own DPS is read);
  the DISABLED-warden check made to turn the hull into the Disabled on every run, not by accident;
  a new lane A check for the re-pick; DESIGN's stale "PD is an active ability" line; CHANGES entry.
- Files: scripts/PlayerShip.cs, scripts/Turrets.cs, tools/smoketest/SmokeTest.cs.txt,
  docs/CHANGES.md, docs/DESIGN.md, this ledger (its uncommitted Handover 3 goes in unchanged).
- Start: ad19fd82c02453fa994c45a50af120f75c4a0a79
- Hashes: PlayerShip 39e359e4 · Turrets 80a48423 · SmokeTest b676d56d · CHANGES 415d0412 ·
  DESIGN a4928663

### J3b · POST
- Verdict: rung 1 (typecheck, 0 errors) and rung 2 (`verify.ps1 -Quick`, ALL CHECKS PASSED, 0
  warnings, UNUSED 0) green in the worktree. No engine rung.
- Causes: (1) GAME: a PD mount held its target for as long as it lived; no window means no fresh
  pick, so the carrier's mount doubled up on a practice fighter never took the third light.
  (2) CHECK: the carrier's passive mounts shoot the three latched lights while their DPS is read
  (1.40 of 3; less, not more). (3+4) GAME: Steer zeroed a Disabled yaw only on the turning circle; at
  pivot speed the rate carried in from the previous run's free W+A turn (~0.85 rad/s after the 0.3 s
  wait) coasted down at the rudder's 2.5 rad/s^2: 0.85^2 / 5 = 0.144 rad = 8 deg (7.92 read); the
  1st heading carried none.
- Files: PlayerShip.cs (Steer: `disabled || hold <= 0` zeroes the yaw at any speed; the turning
  circle's Disabled half deleted), Turrets.cs (`Acquire(from, held)`, `Claimed`), SmokeTest
  (`LaneADisabledChecks` turns into every Disabled; `LaneAPdRepickChecks` new, called after
  `LaneAPassivePdChecks`; the lights' block takes `PdReachOff` until its DPS check; a comment on the
  carrier PD check), DESIGN.md (line ~623: PD passive + the re-pick), CHANGES.md (entry; the F16,
  J2b and slice 1a Known broken lines made true to the ad19fd8 run).
- **D20** A held PD / sentry target gives way to a FREE one that betters it: a lower rank, or the
  same rank while a sibling shares it; never down the ranks, never to a fallback, never by distance
  alone (so no flicking). This restores what a window's fresh pick gave (spread, missiles first).
- **D21** Disabled freezes the heading like a hold of x0 but is NOT folded into `Held`: a hold of x0
  also clamps both speed caps to 0 (a dead stop), and D7 asks no helm, not a stop. A Disabled hull
  still coasts on its speed with no thrust and no turn.
- Checkpoint: the commit after this entry ("Kits lane A job 1b"). Next: slice 2 (Handover 3, J4).

### J4 · PRE · F17, the outgoing door (OutGuards)
- Intent: D16. `Status.Suppressed 64 / Dazzled 128 / Jammed 256`, `StatusSet.HostOnly` masked out of
  `Bits`; `StatusSet.OutGuards` rows {Status, Gun, Move, Super, HoldsThrow, Bosses}; the door
  `StatusSet.Out(d, OutKind)` + `HoldsThrow`; `Boss.Out(move)` replaces the six
  `m.Damage * DamageMult` sites; `Raider.Strike` (both lasers) and `Emplacement.Spec` (its gun) go
  through the door; the heavy's missile throw is held; a boss refuses a status whose row says
  `Bosses = false`. Checks: `LaneAOutDoorChecks` (table + bits + webifiers + gunship hold),
  `LaneAOutDoorBossChecks` (Lancer arena, beside the burn clock), `LaneAOutDoorBaseChecks` (siege).
- Files: scripts/Statuses.cs, Boss.cs, Raider.cs, Emplacements.cs; tools/smoketest/SmokeTest.cs.txt;
  docs/CHANGES.md; docs/DESIGN.md; this ledger.
- Start: 7cf773943a18bae3605bddf28dee7e767c2a0752
- Hashes: Statuses aab38ab7 · Boss d19b3df4 · Raider 49bea117 · Emplacements a158ce08 ·
  SmokeTest 8ecb4f3b · CHANGES d2837272 · DESIGN 5c8418a0

### J4 · POST
- Verdict: rung 2 (`verify.ps1 -Quick`) ALL CHECKS PASSED (0 errors, 0 warnings, UNUSED 0). No engine rung.
- Files: Statuses.cs (3 statuses, `OutKind`, `OutGuard`, `OutGuards`, `HostOnly`, `Reaches`, `Out`,
  `HoldsThrow`, `Bits` masked), Boss.cs (`Out(m)` at the six sites, ApplyStatus through `Reaches`),
  Raider.cs (Strike through the door; the throw held; ApplyStatus through `Reaches`), Emplacements.cs
  (Spec through the door; ApplyStatus through `Reaches`), SmokeTest (3 methods + 3 calls), CHANGES,
  DESIGN (tables row).
- **D22** "Bosses immune" is a tag on the row (`OutGuard.Spares = Tag.Boss`), read by the door users'
  ApplyStatus through `StatusSet.Reaches(s, Tags)`: a spared status is never put on the thing, so a
  Dazzled / Jammed boss is unchanged by construction (its rows' Move / Super are 1 all the same).
  PlayerShip / Deployed / UtilityShip ApplyStatus are not gated: no row spares a player-side tag.
- **D23** A raider blow the door takes to 0 (Jammed) is not struck at all, so it starts no 0.52 s gap on
  the hull; a Jammed emplacement still fires a 0-damage round (the EMP's "guns silenced" is 6d's).
- Harness timing note (applies to every lane A check): a continuation after `await Wait(x)` runs at the
  END of a frame (timers run after `_process`); after `await ToSignal(ProcessFrame)` at the START of the
  next, before any `_process`. So `Wait` then one ProcessFrame await spans NO node `_process`: a rate
  read across it is 0 (that is job 1c's cause).
- Checkpoint: the commit after this entry ("Kits lane A J4"). Next: job 1c (coordinator), then J5.

### J1c · PRE · kits job 1c: the DISABLED warden's "turning at 0 deg/s"
- Intent: rung 3 at 606201f (seed 11400714819323522083): the three "a DISABLED warden" lines fail only
  on their setup's `spin > 20 deg/s` (read 0). Cause (harness): `await Wait(x)` resumes at the END of a
  frame, then `r0`, then `await ToSignal(ProcessFrame)` resumes at the START of the next, before any
  `_process` -- the rate was read across no frame of the hull. Fix: hold W + the rudder, reading the
  rate each frame across one `_process` (frame start to frame start), until a seeded 0.4-0.8 s have
  passed AND it turns faster than 25 deg/s (the effect); keys up and Disabled in that same
  continuation, so the hull's next frame is its first disabled one with that yaw on it. Assert > 20.
- Files: tools/smoketest/SmokeTest.cs.txt (LaneADisabledChecks only), docs/CHANGES.md, this ledger.
- Start: a538f55480b9bbd089013d63261081f2d55befa1
- Hashes: SmokeTest f5b490ae · CHANGES 07410cc5

### J1c · POST
- Verdict: rung 2 (`verify.ps1 -Quick`) ALL CHECKS PASSED (0 errors, 0 warnings, UNUSED 0). No engine rung.
- Files: SmokeTest (LaneADisabledChecks' setup: the rate read frame start to frame start while held,
  until >= the seeded time and > 25 deg/s), CHANGES (1c entry; 1b's Known broken made true to the
  606201f run), this ledger (J4 + J1c rows in the rungs table).
- Checkpoint: the commit after this entry ("Kits lane A job 1c"). Next: J5 (F4 + F18).

### Job P · PRE · prove J4 (F17 OutGuards) + job 1c (the DISABLED rate fix) at rung 3
- Intent: nothing has run in the engine since 606201f. Run the chain quick, solo@11400714819323522083,
  solo to prove J4's OutGuards checks and job 1c's DISABLED-rate fix together. Fix any red at its
  cause (game code if the game is wrong, harness if the check is wrong) and re-prove at rung 3 on
  two different seeds before this job's POST. Literals J4's checks must read: the outgoing door's
  table; 3 x "a webifier's laser goes out through the door" (Suppressed 1.50 / Jammed 0.00 /
  Dazzled 3.00); 3 x "a Suppressed gunship ... holds its missile" (0-2 frames after the lapse);
  "Suppressed reaches a boss; Dazzled and Jammed ..."; 3 x "a Suppressed boss: its shockwave ...
  31.5 ... beam ... 50 ... 45"; 3 x "a Suppressed base's launcher ... its round carries". Job 1c:
  3 x "a DISABLED warden at N deg, turning at N deg/s ...: ... turn it 0.00 deg" with N > 25.
- Files (may touch if red): scripts/Statuses.cs, scripts/Boss.cs, scripts/Raider.cs,
  scripts/Emplacements.cs, tools/smoketest/SmokeTest.cs.txt, docs/CHANGES.md, this ledger.
- Model tier: sonnet (no red from a previous agent; J1c's POST was green at rung 2).
- Start: 1d5fb855170633e954201cd771e8f8c66f4bd9ec
- Hashes: Statuses 011a71d4 · Boss 85c742d6 · Raider 5e550c48 · Emplacements 5898f5d1 ·
  SmokeTest 31a4f66b · CHANGES 64c4e269

### Job P · POST
- Verdict: green. Chain quick, solo@11400714819323522083, solo: first pass hit one red at rung 3
  (seeded), "five statuses and no sixth" (a pre-F17 check asserting `Enum.GetValues(typeof(Status))
  .Length == 6`, stale since F17 added 3 more host-only members). Fixed in the CHECK, not the game:
  `StatusSet.Bits` already masks host-only members off the wire (D16), so the wire format was never
  broken -- only the raw enum-length trip wire was. Rewrote it to count non-host-only members (5)
  apart from the enum's total (9). Re-ran the full chain clean: quick green, solo@11400714819323522083
  green, solo (fresh seed 11400714819323472548) green. J4's OutGuards checks (the table, the
  webifier/gunship/boss/base door checks) and job 1c's DISABLED-rate fix are proved on two seeds.
- Files: tools/smoketest/SmokeTest.cs.txt (the one check rewritten), docs/CHANGES.md (new Job P
  entry; J4's and job 1c's Rungs/Known broken lines made true).
- Checkpoint: the commit after this entry ("Kits lane A Job P"). Next: J5 (F4 + F18, D17, Handover 4).

## Engine rungs owed to the main session (run in the worktree, rebased, one engine at a time)

| after | rung | seeds | look for (PASS lines) |
|---|---|---|---|
| J2 slice 1a | 3 (`tools\smoketest\run.ps1 -Solo`) | two different seeds | "the burn clock:" x3 · "the railgun's charge is a hold of x0" x3 · "a DISABLED warden" x3 · "Hardened at the applier's share" x3 · rewritten: "the railgun charges with the hull held at x0", "then its railgun's whole", the ability sweep's railgun row, "a 100 blow on a HARDENED battleship" |
| J2b knife edge | 3 (`-Solo`) | two different seeds | "the burn clock:" x3 (now "... and nothing in the 0.4 s after it"; burned 3.000 +- 0.017 s) · "the beam, judged every 0.25 s for 50, through the 0.52 s guard: 3 hits ... 150" · unchanged neighbours "the live beam holds the line it drew", "and the line is still drawn 2 s into its 3 s burn", "the ram, due mid-beam, waits for the beam to end" |
| J3 F16 | 3 (`-Solo`) | two different seeds | new: "every hull that mounts point defence is in the passive-PD table", 7 x "...: point defence with nothing pressed and no key for it". Rewritten: "PD fires with nothing pressed", "each battleship PD turret picks its own target", "no window and no recharge: 15.5 to 17.5 s on", "Point defence: 2.00 DPS ... = 64.65", "keys tab lists the battleship's 3 abilities + 6 open slots", "Esc cancels a capture", the BB / DD bar lines, "carrier PD: three turrets on three different LIGHT targets", "a hunter called off while the fighters and point defence are on it", "the warden's point defence is on with nothing pressed -- ... 10 DPS", the sustained-total pair (freighter 50, carrier), the 12 fittings-sweep lines, "every ability on every bar has a witness", the pd_range reach row, "a battleship's point defence picks a cruise missile ... before a light raider", "point defence, with nothing pressed, shoots the boss's missiles down", "left alone (no point defence), the escorts pin the pilot", the siege's "falls to the battleship's MAIN GUNS ... point defence out of reach", "a 1000 u shockwave throws nothing in flight", "a carrier's wing sent at a raider that dies", "a turret left standing shoots what comes near it", the hauler pad's three. Watch (unchanged but now with PD on): the carrier raider checks (webifier DPS 3.0 +- 0.7, the gunship's 700% burn and laser on a pinned carrier), the armed dummy's 50 hit |
| J3 F16 | 4 (`tools\screens\run.ps1`) | - | LINT 0; read by eye: 4_hub_battleship (PD firing, no ring), 7_hub_carrier_strike, 23_bar_battleship_cooldown (renamed: no PD slot on the bar), one close-up (no ring round a PD mount) |
| J3 F16 | 5 (`tools\smoketest\run.ps1`) | - | host "a guest's point defence fires on the host with nothing pressed"; guest "guest sees its own point defence fire with nothing pressed and no key for it"; unchanged neighbours "guest's bomber strike launched real torpedoes on the host", "the host's raiders reached this guest" |
| J3b job 1b | 3 (`-Solo`) | 11400714819323522083 and one other | the four that failed: "carrier PD: three turrets on three different LIGHT targets -- never a plain dummy" · "three lights, 1 DPS each (3.0 +- 0.7)" · "a DISABLED warden at N deg, turning at N deg/s on A/D when it is disabled: ... turn it 0.00 deg" x3 (each turning > 20 deg/s) · new "BATTLESHIP / CARRIER / DESTROYER: a point-defence mount gives way to something free that betters what it holds" x3. Neighbours the re-pick could move: "with more turrets than light targets, the spare turret still never takes a plain dummy", "each battleship PD turret picks its own target", the 7 x passive-PD table lines, "a battleship's point defence picks a cruise missile ... before a light raider", "a turret left standing takes the small craft first", "a hunter called off while the fighters and point defence are on it", "pinned: A does not turn it" |
| J4 F17 | 3 (`-Solo`) | two different seeds | new: "the outgoing door's table" · 3 x "a webifier's laser goes out through the door" (Suppressed 1.50 / Jammed 0.00 / Dazzled 3.00) · 3 x "a Suppressed gunship ... holds its missile" (0-2 frames after the lapse) · "Suppressed reaches a boss; Dazzled and Jammed ..." · 3 x "a Suppressed boss: its shockwave ... 31.5 ... beam ... 50 ... 45" · 3 x "a Suppressed base's launcher ... its round carries". Neighbours: the Lancer arena's stealth block (right after), the siege's "ONLY THE BASE DROPS" (after the base check), the WARRIOR block (after the door checks; 3 gunship blasts land 12 s later ~4500 u from base) |
| J1c | 3 (`-Solo`) | 11400714819323522083 and one other | 3 x "a DISABLED warden at N deg, turning at N deg/s ...: ... turn it 0.00 deg" with N > 25 |

## Handover 4: the fourth agent did J4 and job 1c, and stops before J5 (context)

Nothing in flight: J4 and J1c have POSTs; the tree is clean at the J1c commit. The next FRESH writer
starts **J5 (F4 + F18, D17)** with a PRE entry. Read D17 below and J4's POST (D22, D23 and the harness
timing note: `await Wait` resumes at a frame's END, `await ToSignal(ProcessFrame)` at the next frame's
START, so a read spanning one of each covers no `_process`). J5's map at 0938f68:
- **Blows a player's side deals** (each becomes `Dealt.Deal(target, d, by, weapon)`, which does
  `TakeDamage` then the credit): Turrets.cs:171 (PD / main-gun mount tick: `tgt.TakeDamage` +
  `Host.NoteDealt`), Shots.cs:225-226 (a shell/torpedo/missile body: `h.TakeDamage(Damage)` +
  `Source.NoteDealt`; the weapon id must come from the shot's row/kind), PlayerShip.cs:618-619 (rail),
  :643-644 (emp: `emp_damage`, which v2 drops later -- leave the row, route the blow), :718 (the echo's
  blast), ShipClasses.cs:317 (the carrier wing's fighter: `t.TakeDamage(S[Def.DamageStat])` +
  `Carrier.NoteCombat()`), Missiles.cs:94 (a friendly missile side's Land: `TakeDamage(d)`),
  BaseDefense.cs:41 (the base's laser: `LaserDealt` -- nobody's ship; decide credit = none).
- **`ITurretHost.NoteDealt(d, at)`** (Turrets.cs:77) and its implementers: PlayerShip.cs:226 (the echo
  arm at :229-230 -- F18 deletes it), Deployed.cs:42 (forwards to its owner Ship), Emplacements.cs:170,
  Hauler.cs:90, Lanes.cs:290 (no-ops). The harness calls `NoteDealt(d, pos)` directly at SmokeTest
  6164, 6261, 6803 ("as a shell of its own would"): rewrite them to the new signature in the same edit.
- **The echo** (PlayerShip.cs:125 `_echoAt`, :701-722 Press / blast): its row's `OnDealt` stores d
  (`Own += d`) and where it landed; v2's small row `Slot.At` replaces `_echoAt` -- take it in J5 if the
  OnDealt needs a place for "where" (it does), since it is the same edit.
- **DealtBy**: does not exist yet; `PlayerShip.DealtBy` (host, by weapon id) is new in J5.
- Checks owed (D17): `LaneADamageDoorChecks`, 3 seeded spots: DealtBy[weapon] == hull lost for the BB's
  main guns, PD, a freighter's dropped turret (credited to its owner), the railgun, a torpedo; the echo
  stores exactly what was dealt while it runs and nothing after.

## Handover 3: slice 2's plan (the third agent read the spec and stopped at its context cap)

Nothing in flight: J3b (job 1b) has its POST, the tree is clean at its commit. No slice-2 job has
started. The next FRESH writer starts J4 with a PRE entry, then J5, J6, J7 (one writer at a time;
a new agent per job if its context passes ~150k).

Read this, not the specs again: it is kits_v2 §5 (F4/F17/F18/F20 + the OutGuards table), v3 §3.6
(Ramjet) / §3.7 (heavies) / §5, v31 §6 / §8, README rulings, raids_squads_adds.md's EnemyDef rows,
reduced to what each job builds. Jobs run ONE AT A TIME, each by a fresh writer, in this order.

- **D15** Slice 2 is four jobs, not three, so each fits one writer's context and the files split:
  **J4** F17 (Boss.cs, Raider.cs, Emplacements.cs, Statuses.cs) · **J5** F4 + F18 (Turrets, Shots,
  Missiles, PlayerShip's NoteDealt, Deployed, ShipClasses' wing, Abilities' echo row, a new owning
  file) · **J6** F1 Add + Ramp (Abilities.cs AbilityDef, PlayerShip Lifts, one line in Steer) ·
  **J7** F20 (Enemies.cs, Raider.cs, Hub's MissileFlight readers). J4 before J7: both edit Raider's
  Strike and missile line; J7 then only changes rows and the pin gate.
- **D16 (J4, F17)** The three OutGuards statuses are declared now, because the OutGuards table names
  them (their use, so D4's UNUSED objection no longer holds for these three): `Suppressed = 64,
  Dazzled = 128, Jammed = 256`, host-only: `StatusSet.HostOnly` (the three) is masked out of `Bits`.
  Parrying 32 still lands with 6c. Nothing in the game applies them until 6a (DD Suppress), 6c (SN
  Flares) and 6d (EC EMP); the harness applies them on the host through `IStatused.ApplyStatus`.
  The table (kits_v2 §5), one row each, `StatusSet.OutGuards`: {Status, Gun (its guns vs craft and
  structures), Move (boss non-super), Super, HoldsThrow, Bosses (reaches a boss)} =
  Suppressed {0.5, 0.7, 1.0, held, yes} · Dazzled {1.0, -, -, held, bosses immune} · Jammed
  {0, -, -, held, bosses immune}. "Held" = the launcher's clock keeps its zero and it throws at the
  lapse. The latch columns (new / existing web) are NOT built now: slice 5's Dazzled / Jammed gates
  are their reader. ONE door for all hostile damage (name it for the mechanism, e.g.
  `StatusSet.Out(in StatusSet by, double d, OutKind kind)` returning the share-scaled damage, and a
  `Holds(kind)` for throws); `Boss.Out(move)` = the door on `m.Damage * DamageMult` with the move's
  kind (super or not), replacing all six `m.Damage * DamageMult` sites (Boss.cs ~478, 487, 497, 519,
  525, 599; the curve lane will put DamageScale inside Boss.Out); Raider.Strike (both the light's and
  the heavy's laser), the heavy's missile throw (held) and the Emplacement gun (Emplacements.cs,
  `_dmg`) go through the same door. Emplacement must be IStatused (check; J2 gave the six
  implementers a share parameter). Checks in `LaneAOutDoorChecks`: the table against those literals
  in one place; from 3 varied spots a Suppressed webifier latched on a pilot lands x0.5, a Jammed
  one 0, a Dazzled one x1.0 (hull lost over a window that starts on the first landed hit); a
  Suppressed gunship holds its missile and throws it within a frame or two of the lapse; a Suppressed
  boss's non-super move lands x0.7 and its super x1.0; a Dazzled / Jammed boss is unchanged; a
  Suppressed emplacement gun x0.5; `Bits` of Pinned|Suppressed is 1. Host-only: no rung 5.
- **D17 (J5, F4 + F18)** The hostile damage door: ONE static function every player-side blow on a
  hostile goes through (target.TakeDamage, then the credit), in a new file named for the mechanism
  (e.g. `Dealt.cs`, `static class Dealt { Deal(IHittable target, double d, ITurretHost by, string
  weapon) }`); weapon ids are consts in that file (main, pd, turret, rail, emp, echo, missile,
  hunter, fighter, torpedo, ... whatever the sites are). The credit: `ITurretHost.NoteDealt` gains
  the target and the weapon; PlayerShip keeps `DealtBy` (host: damage dealt, by weapon id: the
  harness's DealtBy probe) and NoteCombat, then runs **F18**: `AbilityDef.OnDealt(ship, target, d,
  weapon)` of every RUNNING row (Sl(id).Left > 0 and While). The echo's arm in NoteDealt is deleted:
  the Echo row's OnDealt stores d and where it landed. No hostile-side rows yet (Taunt's x1.5 is 6c,
  Buster etc. later): the door is the foundation; do not build an empty table. Sites (grep
  `TakeDamage\(|NoteDealt\(`): Turrets.cs:169, Shots.cs:226, PlayerShip rail ~619 / emp ~644 / echo
  ~715, Missiles.cs:94, the wing's shots and torpedoes, Deployed, anything else found; a hostile
  hitting a player (Incoming) is NOT this door. Checks in `LaneADamageDoorChecks`: from 3 varied
  spots, DealtBy[weapon] equals the hull the target lost for the BB's main guns, PD, a freighter's
  dropped turret (credited to its owner), the railgun and a torpedo; the echo stores exactly what
  was dealt while it runs and nothing after (existing echo checks unchanged, they are its old truth).
- **D18 (J6, F1)** **Add**: `AbilityDef.SpeedAdd` (a stat id) adds a flat top speed after the lifts'
  multiplier: top = sheet x Scale(shares) + adds, then the hold (Held) on the whole. **Ramp**: a lift
  whose size is a running total kept in the row's slot (`Own`: per-ability state is how it reaches
  the wire), with `AbilityDef.Ramp` {build, cap, bleed stat ids, and the condition}: while it runs
  and the condition holds, +build a second; minus bleed x (|yaw| / the hull's turn rate) a second;
  clamped to [0, cap]; after the run it drains to 0 over 1.0 s; its value is a share in
  `LiftShares` on the speed side. No row uses either until 6d (the Dart); prove the pure step with
  v3 §3.6's literals on a 260 u/s sheet, varied by frame rate (1/30, 1/60, 1/144 s): top after
  {1, 3, 5, 7} s straight at full throttle {286, 338, 390, 390} +- 3; then 1 s of full rudder
  390 -> 364; 0 by 9.0 s. Add: 260 x 1.5 + 100 = 490 (v31 §3.4). Steer is lane B's: one line.
- **D19 (J7, F20)** Heavy rows: `EnemyDef.Barrels` (1; heavies 2) and `Dps` PER BARREL, heavies 1.29
  (gunship 2.0, cross 2.6, lancerkin 1.8 today), so 2.58 per heavy; one Strike per volley of
  Dps x Barrels x ShotEvery x Strength (two strikes from one source would be eaten by the 0.52 s
  HitGap), drawn as two flashes from two barrel offsets on the turret. `MissileFlight` is a row (10;
  the `Raider.MissileFlight` const 12 is deleted, callers read the row), `MissileDamage` 42 -> 35
  (README ruling "heavy missile 35 in 10 s, only at a pinned target"; numbers §4 row 8's "42" is
  today's code, not a ruling), and the throw only at a pinned target (the pin gate on the missile
  line). Raids rows in the same edit: `Cc` (a Status: pin rows Pinned, standoff rows none; the
  light's latch line applies `Def.Cc`), `Exp` (6 light, 18 heavy; lane G pays it). Lane G keeps
  "heavies laser unpinned", the tether draw and the pay. Checks in `LaneAHeavyRowsChecks`: the rows
  against 2 x 1.29 = 2.58 / 35 / 10 / Cc / Exp in one place; from 3 varied spots a gunship on a
  pinned pilot lands 2.58 +- 0.15 DPS; a missile's flight is 10 s and it lands 35; an unpinned
  target inside 500 u draws no missile. Rewrite every old-truth check (grep `Dps|2\.6|1\.8|
  MissileFlight|MissileDamage|42|HeavyDps` in SmokeTest near the raider sections).

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
