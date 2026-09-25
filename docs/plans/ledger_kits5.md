# Ledger: kits lane A, slice 5 (F12, F11, F14, F19)

Writer: one agent in worktree `WarShips_wt_kits5`, branch `wt/kits5`, started at 54db5c3 (version-l, fast-forward).
Spec: kits_v2 §5 + cards (Warrior, Sniper flares, Destroyer tow, Freighter, Warden), kits_v3 §3.2/3.3/3.5 + §5,
kits_v31 §3.1 + §6 + §7 + §8; README rulings. Conventions and D1-D27: `ledger_kits.md`. BUILD PHASE: no engine;
per job typecheck + verify -Quick; every check written now, run in the final test phase.
A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits, redo it first.

## Decisions taken where the spec is silent (D28 on; D1-D27 are ledger_kits.md's)

- **D28** Foundations, not kits: slice 5 builds the mechanism each foundation names and its checks; the class
  rows and keys (Warrior blade/whirl/lunge/stance, Sniper flares ability, Warden taunt, DD grapnel) are slice 6.
  A table row whose only reader is a 6x ability lands with it (D4); the mechanism is proved by checks now.
- **D29** F12 `Melee.cs`: `MeleeDef {Id, Reach (stat id), Arc (half-arc, degrees), Stops}`, pure `Melee.Pick`,
  host door `Melee.Strike(def, by, damage)` (Dealt, credited under Id), pure `Melee.Guard(nose, aim, maxOff)`:
  the guard clamp the prism shares (±90°). `Melee.All` gets its rows (blade 160 u ±55°, whirl 210 u ±180°) in 6c.
- **D30** F11 `Prism.cs`: `BlowKind` {Beam, Ray, Shot, Ring, Ram, Rock, Blast, Web}; `Prism.Bands` rows
  (SQUARE ≤15°: 0.75 back along g, 0.25 straight on; SLANT ≤60°: 0.5 mirrored, 0.5 bent θ/2 the other way);
  `IPrism` (PlayerShip: Parrying 32 on the wire, guard = Melee.Guard(nose, cursor, 90°)); pure `Prism.Resolve`;
  host `Prism.Split` fires the children as `Lines` rows `prism_out` (FirstBody = Stops 1, 36 u, 1500 u / 900 u
  for rays, hostiles, credited `prism`) and `prism_through` (Stops 0, 36 u, the parent's remaining length /
  400 u for rays, pilots, under the parent's source). Children never split again. The 0.75 s split tick and
  the 3-per-stance cap are the stance's (6c). `warn_beam` (the clip drawn) lands with the stance's wedge (6c).
- **D31** F14 throw point: the host reads the owner's `AimPoint` (20 Hz intent), as the guns do; F8's press
  payload point (slice 4) may replace it at 6b. Not edited here (slice 4 owns F8's payload).

- **D32** The paint (PlayerShip.PaintOn / Painted) is host-only in slice 5: its one raiser is 6b's spotter hit, and
  6b decides how a guest learns it (rung 5: guest sentries pick the same target id).

## Jobs (foundations before their users; each: PRE, edit + checks, typecheck + quick, POST, commit)

- **kits5-J1 F12** Melee.cs (D29). Checks `LaneAMeleeArcChecks` (pure arc, 3 layouts; guard clamp 3 noses),
  `LaneAMeleeStrikeChecks` (world: front in reach struck, behind / past reach not, credited).
- **kits5-J2 F11a** Prism.cs, Lines rows + Strike generalised (reach override, pilots side), Status.Parrying,
  PlayerShip : IPrism, Boss.Burn walks to the first catcher. Checks `LaneAPrismBandChecks`,
  `LaneAPrismResolveChecks`, `LaneAPrismSplitChecks` (world), `LaneAPrismBurnWalkChecks` (world).
- **kits5-J3 F11b** Shot.Strike reflects (ShotDef.Reflectable; cruise false), rays (raider laser, boss bolt)
  split. Checks `LaneAPrismReflectChecks`, `LaneAPrismRayChecks`.
- **kits5-J4 F14a** the paint (PlayerShip.PaintOn / Painted), ITurretHost.Prefer, Turret hold guard
  re-acquires on a Prefer change. Checks `LaneASentryPreferChecks` (the 4 kits_v31 3.1 sentry lines).
- **kits5-J5 F14b** sentry throw (deploy_reach 600, deploy_flight 0.8) and recall (recall_pick 60), replacing
  collect_range and the Collect row. Checks `LaneASentryThrowChecks`.
- **kits5-J6 F14c** Raider.Call (timed target override, Quarry untouched, CalledBy). Checks `LaneARaiderCallChecks`.
- **kits5-J7 F14d** ShotDef.Decoyable, point guidance, Hub.NetDecoy, the `flares` Spawns row. Checks
  `LaneADecoyChecks`, rung-5 host/guest pair.
- **kits5-J8 F19** Raider Towed / Flung (host state before the AI), Targeting.Immovable. Checks `LaneATowChecks`.
- **kits5-J9** Dazzled / Jammed latch gates (OutGuard.NewLatch / KeepsLatch), CHANGES + DESIGN, final POST.
  Checks `LaneALatchGateChecks`.

### kits5-J1 · PRE · F12 melee arc + guard clamp (D29) -- tier opus
- Intent: new scripts/Melee.cs (MeleeDef, Melee.All, Pick, Strike, Guard). Checks LaneAMeleeArcChecks, LaneAMeleeStrikeChecks.
- Files: scripts/Melee.cs (new), tools/smoketest/SmokeTest.cs.txt, this ledger.
- HEAD af89752ffdd79077d20ea21ac7282c51a034908f · SmokeTest.cs.txt 93096537210a252c1f5955f72522ecfbeda24102
### kits5-J1 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Melee.cs: MeleeDef {Id, Reach stat, Arc deg, Stops}, pure Pick / Guard / Nose, host Strike through
  Dealt. No row yet (D28): Melee.All opens with the Warrior in 6c.
- Files: scripts/Melee.cs (new); SmokeTest.cs.txt (LaneAMeleeArcChecks, LaneAMeleeStrikeChecks after
  LaneAChargeTableHashedChecks, freighter in solo). Trap: SmokeTest.cs.txt is LF; write it in binary.
- Next: kits5-J2.
### kits5-J2 · PRE · F11a prism: bands, resolve, children on Lines, Parrying, the Burn walk (D30) -- tier opus
- Intent: new scripts/Prism.cs; Lines rows prism_out / prism_through + Wide / Side / reach override; Status.Parrying 32;
  PlayerShip : IPrism; Boss.Burn's beam judged through Prism.Walk. Checks LaneAPrismBandChecks, LaneAPrismResolveChecks,
  LaneAPrismSplitChecks, LaneAPrismWalkChecks.
- Files: scripts/Prism.cs (new), scripts/Lines.cs, scripts/Statuses.cs, scripts/PlayerShip.cs, scripts/Boss.cs, SmokeTest.cs.txt, this ledger.
- HEAD 91f1a1ce460659eda9b758b6e44aa89a4d1b3f43 · Lines.cs 217413dba35614e131ba1b5608ec095f5dfa869f · Statuses.cs adc4ed3e918a07dcaa259013ef3b4818c7fc48d8 · PlayerShip.cs 91bfcf083601bcebc85e830529be06b5adf5ded8 · Boss.cs e0c794b34c74d2d622a36a7b986acfcb529d3d1a · SmokeTest.cs.txt d8c63791d8483111b8a7ba282548dbbe1213ebd6
### kits5-J2 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Prism.cs (BlowKind {Beam, Ray, Shot, Area}, Bands square/slant, IPrism, Resolve, Catch, Walk);
  Lines rows PrismOut (1) / PrismThrough (2) with LineDef.Wide + Side and Strike's reach/source/skip; Status.Parrying
  = 32 (on the wire); PlayerShip : IPrism (guard = Melee.Guard(nose, cursor, 90°)); Boss.Burn's beam through
  Prism.Walk (nearest first; it was list order). Children use Fx.Rail / Beam.Rail until 6c gives the stance its own
  rows (D30). Lines.All grew 2 rows: the protocol fingerprint moves.
- Files: scripts/Prism.cs (new), Lines.cs, Dealt.cs (Dealt.Prism), Statuses.cs, PlayerShip.cs, Boss.cs; SmokeTest.cs.txt
  (LaneAPrismBandChecks, LaneAPrismResolveChecks, LaneAPrismSplitChecks, LaneAPrismWalkChecks after the melee pair).
- Next: kits5-J3.
### kits5-J3 · PRE · F11b: rounds reflect, rays split -- tier opus
- Intent: ShotDef.Reflectable (slug, scrap, seeker; never cruise) + the Reflect shot row (appended, index 7); Shot.Strike
  reflects a caught round (SQUARE along g x1.5, SLANT along the mirror x1.0, friendly, unguided, same damage); the raider
  laser and the boss bolt are rays through Prism.Catch. Checks LaneAPrismReflectChecks, LaneAPrismRayChecks.
- Files: scripts/Shots.cs, scripts/Prism.cs, scripts/Raider.cs (Strike, 1 line), scripts/Boss.cs (Bolt), SmokeTest.cs.txt, this ledger.
- HEAD 9584ae90f2f2e3d8fc39f9af0890485d0a81a5fa · Shots.cs 63009ca1c5e1452a882acc5c7802912fd85fa694 · Prism.cs f2f41917868a3f63dfed7e2172343a4c88ecbd60 · Raider.cs 99962dbd495d83b1513279d3c64d3b0a8c0cd2ff · Boss.cs 93dea68ab53ac4bb1cbde923c46197993ee4ec91 · SmokeTest.cs.txt 993c82bf7b7428fbeec3a3813391ccf557d70394
### kits5-J3 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. ShotDef.Reflectable (slug, scrap, seeker); Shots.Reflect = 7 (appended; a wire index); Shot.Strike's
  Reflected fires the catcher's Reflect round (band RoundSpeed x1.5 / x1.0, remaining flight, same damage, size);
  Raider.Strike and the boss's Bolt are rays through Prism.Catch (Raider.cs: one hunk in Strike, D30's ray).
- Files: Shots.cs, Prism.cs (header), Raider.cs, Boss.cs; SmokeTest.cs.txt (LaneAPrismReflectChecks, LaneAPrismRayChecks
  after LaneAPrismWalkChecks). Trap: both set Me.Demo = true for their run so the local cursor does not move the guard.
- Next: kits5-J4.
### kits5-J4 · PRE · F14a: the paint, ITurretHost.Prefer, the hold rule -- tier opus
- Intent: PlayerShip.PaintOn / Painted (host, one target, timed; the spotter raises it in 6b); ITurretHost.Prefer (default
  null; a sentry returns its owner's paint); Turret.Acquire takes the preferred first; the hold guard re-acquires when a
  paint in reach is not its target, or the paint it followed lapses. Checks LaneASentryPreferChecks.
- Files: scripts/Turrets.cs, scripts/Deployed.cs, scripts/PlayerShip.cs, SmokeTest.cs.txt, this ledger.
- HEAD bf67b26ec7e2079c8e5842cae4bff0a033640f1d · Turrets.cs 7515dfde83a9f10c89d987f82cbc295f0191f580 · Deployed.cs b6c6b664bc52cdb1f3a4f3be61a782ab0c691214 · PlayerShip.cs 79d1250b543c03f6bc2bc4db169b90044233a737 · SmokeTest.cs.txt a39988874b3050b07e30f3c8e5d3ad6bf99c645a
### kits5-J4 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. ITurretHost.Prefer is a default interface member (null), so PD, CIWS, the hauler, lane guns and
  emplacements are untouched; DeployedTurret.Prefer = its owner's Painted. Turret: Preferred() above every rank in
  Acquire; the hold re-picks on a new paint in reach or the followed paint lapsing (_onPrefer). The paint is
  host-only: a guest's sentry copies may track otherwise until 6b puts the paint on the wire (D32).
- Files: Turrets.cs, Deployed.cs, PlayerShip.cs (PaintOn / Painted, ticked with the statuses); SmokeTest.cs.txt
  (LaneASentryPreferChecks after LaneAPrismRayChecks).
- Next: kits5-J5.

## Handover 1: the first agent stops after kits5-J4 (context), at a job boundary

Done: J1 (F12), J2-J3 (F11), J4 (F14 Prefer). Next is kits5-J5; no PRE written for it. What the next agent needs:
- **J5 plan (sentry throw + recall).** PlayerShip: `DeployTurret(Vector2 cursor)` = recall the own turret within
  `recall_pick` (60) of the cursor (free, never refused; its Hp/MaxHp pushed on a stow stack, reused by the next
  throw), else throw: point = Position + (cursor - Position).LimitLength(deploy_reach 600), a host pending list
  (point, deploy_flight 0.8 s left, hp, max) ticked beside `_paintLeft` (PlayerShip ~line 1026), then
  `Hub.Drop(owner, at, hull, max)` (add the max param). Raise `Fx.Warn(new FxRaise { Id = Fx.AimZone, At, To = At,
  Size = DeployedTurret.Radius, Time = flight })` at the throw: every peer marks the landing; no new RPC. In flight
  it is no body (cannot be hit, cannot fire). `TurretsOut` counts the pending throws. Press reads `s.AimPoint` (D31).
  Deploy row: Default Key.R, Refuse null when a recall target is under the cursor. DELETE Ab.Collect, CollectTurret,
  NearestOwnTurret and the three `collect_range` rows (Ships.cs FreightHauler/Tender/Bastion, and their
  `Abilities` lists); add deploy_reach / deploy_flight / recall_pick rows to the same three blocks.
- **Harness callers J5 must rewrite (CLAUDE.md 6.3):** SmokeTest.cs.txt ~2804 (UseAbility deploy), ~9638-9670 (T drops
  under the hull, "C over one picks it up", "NOT OVER ONE"; set `fr.Demo = true` + AimPoint, wait > 0.8 s before
  counting H.Deployed), ~9838, ~10469, the ability sweep ~10586-10587 ("collect" row), ~5837/5843 (never-walled
  weapon rows list includes "collect" and Ab.Collect: lane C's walls check), rung 5 ~13819-13867 ("the guest's C
  picked one up") and ~14115-14152 (guest T / C). Guest presses: the guest's AimPoint reaches the host at 20 Hz.
- Traps: SmokeTest.cs.txt is LF (edit in binary / newline=''); verify -Quick needs a 300 s+ timeout.
- J6-J9 as the job list says. Raider.cs hunks still owed: F14 Call (J6), F19 Towed (J8), the latch gates (J9).
