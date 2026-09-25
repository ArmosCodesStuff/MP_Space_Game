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

## Agent 2 (resumes after Handover 1)

### kits5-J5 · PRE · F14b: the sentry throw and recall -- tier opus
- Intent: Deploy (R) throws to the cursor (AimPoint, D31) clamped to deploy_reach 600, landing after deploy_flight
  0.8 s (host pending list, counted in TurretsOut, an Fx.AimZone mark); R within recall_pick 60 of an own turret
  recalls it (never refused; hull stowed and reused by the next throw). Delete Ab.Collect, CollectTurret,
  NearestOwnTurret, collect_range. Hub.Drop gains the max hull. Checks LaneASentryThrowChecks + the rewrites.
- D33: the recall stows at once (the 0.8 s flight home is not drawn; 6b may add it). Stowed hulls are a stack.
- Files: scripts/PlayerShip.cs, scripts/Abilities.cs, scripts/Ships.cs, scripts/Hub.cs, SmokeTest.cs.txt, this ledger.
- HEAD d95adc3cd5707097f16cb74209f3f06a06f82eb3 · PlayerShip.cs 0ab60fbad195ccedd5b1ea7f6353d1f5ce0560c0 · Abilities.cs 4f130c86b8b5160df23cfb8eb9358de085b58e9d · Ships.cs 667188f159e3bf5a7ecf560191480eb366e8cf2f · Hub.cs bb3d4f97e167719b852743c3c0fb7971669b7909 · SmokeTest.cs.txt 2e777f1794453c44155090b591384b8655a51318
### kits5-J5 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Deploy row (R, "SENTRY"): recall first (RecallAt within recall_pick, never refused), else throw to
  ThrowPoint (clamped to deploy_reach), a host pending list ticked with the statuses, counted in TurretsOut, landing
  via Hub.Drop(owner, at, hull, max) after deploy_flight; Fx.AimZone marks the landing on every peer. Ab.Collect,
  CollectTurret, NearestOwnTurret and the three collect_range rows deleted; deploy_reach / deploy_flight / recall_pick
  rows added to the three freighters.
- Harness rewrites (6.3): SentryAt helper; the credit check, three-out, recall + RELOADING (was C / NOT OVER ONE),
  the wreck message, the tender, the missile-first pick, the ability sweep and fit rows, the never-walled rows and the
  walls kit, rung 5 host/guest (guest throws at its held cursor and the landing is asserted on the host's spot; R
  recalls). The guest's `For(FreightHauler).Length == 13` was stale before this job (5 + drive + 6 = 12); now 11.
- Files: PlayerShip.cs, Abilities.cs, Ships.cs, Hub.cs; SmokeTest.cs.txt (LaneASentryThrowChecks after LaneASentryPreferChecks).
- Next: kits5-J6.
### kits5-J6 · PRE · F14c: Raider.Call -- tier opus
- Intent: Squad.Call(by, seconds): a timed target override (Pick takes the caller while it is up; the dark re-take waits;
  at the lapse the squad re-picks, its Quarry first), CalledBy; Raider.Call / Raider.CalledBy are faces onto the squad.
  D34: a call is the SQUAD's (a squad fights as one; Taunt calls every raider in reach, so every squad it touches).
  D35: Taunt's x1.5 from the caller (an F4 row reading CalledBy) and the 1000 u pick land with the Taunt row (6c, D4).
  Checks LaneARaiderCallChecks.
- Files: scripts/Squads.cs, scripts/Raider.cs (one hunk: Call / CalledBy beside Target), SmokeTest.cs.txt, this ledger.
- HEAD f79996d070572b339dd654999aa8785cbe2b7ac2 · Squads.cs b5bf1dbccefbede9ef35cc8f3ad3058c3cc1d09b · Raider.cs c0166966922981f9190af1b44f85c82263ccd785 · SmokeTest.cs.txt 9f97d2f64165a5fe561427504e2034c74120b9da
### kits5-J6 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Squad.Call / CalledBy (the override applied at once with a Reform, so a latched webber lets go; the
  lapse re-picks, Quarry first; the dark re-take waits while called); Raider.Call / CalledBy (Raider.cs: 3 lines).
- Files: Squads.cs, Raider.cs; SmokeTest.cs.txt (LaneARaiderCallChecks after LaneASentryThrowChecks).
- Next: kits5-J7.
### kits5-J7 · PRE · F14d: decoys -- the flares spawn row, NetDecoy, point guidance, the dazzle -- tier opus
- Intent: new scripts/Decoys.cs (DecoyDef rows, Decoys.All "flares": 6 at 180 u, 0.6 s coast, 5 s life, lure 500, mark
  300 with >= 1.0 s left, dazzle 150 for 4 s after; pure Points / Nearest; host Tick; DecoySalvo node); Spawns row Decoy
  (appended, index 3) on a new NetIds.Decoy space; ShotDef.Decoyable (seeker; never cruise); Shot point guidance
  (DecoyTo: turns at its own rate, bursts harmlessly within the row's Catch); MissileSide.Decoyable (the raid's);
  Hub.Decoy / NetDecoy(id, point) moves a shot or a predicted blast (its host landing point, its visual and its mark)
  on every peer; Hub.Flares(at, heading) pops a salvo (the 6c ability calls it). Checks LaneADecoyChecks,
  rung 5 LaneADecoyHostChecks / LaneADecoyGuestChecks, a Shots.cs.txt frame.
- D36: the salvo's 5 s is its whole life from the pop (kits_v2's proof: present at 4.9 s, gone at 5.1 s); it lures
  from the pop, coasting out over the first 0.6 s. D37: a moved mark jumps to the flare (the slide is 6c's drawing).
- Files: scripts/Decoys.cs (new), Shots.cs, Missiles.cs, Hub.cs, Spawned.cs, Ids.cs, SmokeTest.cs.txt, Shots.cs.txt, this ledger.
- HEAD dc7372588041aa9c266de64605eb04d83077d709 · Shots.cs f477175950bfe48c7bde60cfe9bdc34013d9ae41 · Missiles.cs ad3c2e754786c89c0094b2db053bfc48f66b721d · Hub.cs 72bfa7fa73fd4cfd912354c588bfa52aab097f34 · Spawned.cs c639289c278b41425c6f49f48ad4134368a982fd · Ids.cs 27b8929f08dc8de4b0873f3dd0b183460a2b8eca · SmokeTest.cs.txt d12fc7e4a9c210d2ff895c3c2be541e88d3680a8 · Shots.cs.txt 8d63b2b342f7062f74266e599d0b4df59589f079
### kits5-J7 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. Decoys.cs (DecoyDef, Decoys.All "flares", pure Points / Nearest, host Tick: lure, mark, dazzle, the
  salvo's end; DecoySalvo draws on every peer); Spawns.Decoy = 3 on NetIds.Decoy (50000, width 1000); ShotDef.Decoyable
  (seeker) + Shot.DecoyTo / DecoyPoint / GuideTo (bursts via Intercept within Catch 30 u); MissileSide.Decoyable (raid)
  + MissileVisual.Retarget; Hub.Flares, Hub.Decoy + NetDecoy (moves a shot, a host blast, its visual and its circle),
  Hub.Blasts, ThrowMissile returns the id. Every rule reads the resting points (D36).
- Wire: a new RPC (Hub.NetDecoy), a new spawn kind (3) and NetIds space: the protocol fingerprint moves.
- Files: Decoys.cs (new), Shots.cs, Missiles.cs, Hub.cs, Spawned.cs, Ids.cs; SmokeTest.cs.txt (LaneADecoyChecks after
  LaneARaiderCallChecks; rung 5 LaneADecoyHostChecks after the host's 45-into-a-turret check, LaneADecoyGuestChecks
  after the guest's); Shots.cs.txt (LaneADecoyFrames, frame 43_lanea_flares_pull_a_mark, after LaneBDriveFrames).
- Next: kits5-J8.
### kits5-J8 · PRE · F19: tow and hurl -- tier opus
- Intent: new scripts/Towing.cs (TowRow table, row "grapnel": 160 u off the bow, 0.5 s reel, 3 s haul, 600 u/s, 900 u,
  60 a body, x2 for a heavy thrown; ITowable; TowState; host Tow / Hurl / Step: the reel, the haul, the hurl swept with
  Shots.Sweep + IHittable.Covers, the first hostile ends it, both take the impact through Dealt ("hurl", credited to
  the tower)); Raider : ITowable, its Towed state checked before its AI (Raider.cs: one hunk beside Disabled);
  Targeting.Immovable = Boss|Structure|Dummy. The Grapnel's key and its swing are 6a's (D28). Checks LaneATowChecks.
- Files: scripts/Towing.cs (new), Raider.cs, Targeting.cs, Dealt.cs, SmokeTest.cs.txt, this ledger.
- HEAD 20d1dbf79f26023cb3b24173f9cf827ee45d9fd3 · Raider.cs a21bb08a3ecd536c08c6d294e874bbbfdbb8f0e1 · Targeting.cs 6e49ade498064628b49daf437b77b4c7cae5dd67 · Dealt.cs 41b4059a79b35bcb273b8548164c319c52385cc7 · SmokeTest.cs.txt 870adfedf83ef6cf1a804e4867165a0fa43e3f1d
### kits5-J8 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED (one IDE0005 fixed on the second pass).
  engine-unproven: rungs owed in the final test phase. Towing.cs (TowRow table, row "hurl"; ITowable; TowState; pure
  Impact / Bow; host Tow / Hurl / Step); Raider : ITowable, Towed checked after the status tick and before Disabled and
  the squad AI (Raider.cs: 3 lines); Targeting.Immovable. No wire change (the raider packet carries the position).
- Files: Towing.cs (new), Raider.cs, Targeting.cs; SmokeTest.cs.txt (LaneATowChecks after LaneADecoyChecks). Dealt.cs untouched
  (the row's Id is the weapon id).
- Next: kits5-J9.
### kits5-J9 · PRE · the latch gates (Dazzled, Jammed), the record -- tier opus
- Intent: OutGuard.BlocksLatch / DropsLatch (the ledger's NewLatch / KeepsLatch, named so a row's default is "no
  effect"): Dazzled blocks a new latch, Jammed blocks it and drops a held one (kits_v2 F17 table); StatusSet reads
  them; Raider's latch line gated (one hunk). Checks LaneALatchGateChecks (+ the flares x latch interaction).
  CHANGES.md Unreleased + Handoff, DESIGN.md slice-5 section, final POST.
- Files: scripts/Statuses.cs, scripts/Raider.cs, SmokeTest.cs.txt, docs/CHANGES.md, docs/DESIGN.md, this ledger.
- HEAD 4b1882861736b19e33a71fc4e24b814e762a9fe8 · Statuses.cs 81ee9f1ff9e96a57c34cff7a1c9cb0ef58c963e2 · Raider.cs a1d8cdd0730a1cbec984be062c8c41e710a74aca · SmokeTest.cs.txt a916c3c1c453acc1eb31f417d2d2d11fb985e197 · CHANGES.md 0149ff62e9be99106a483e18eed5458e2c4141f4 · DESIGN.md a313778f0865dde72435f741d8664106ecee8fb6
### kits5-J9 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. OutGuard.BlocksLatch / DropsLatch (Dazzled blocks; Jammed blocks and drops); StatusSet.BlocksLatch /
  DropsLatch / HoldsThrow share one Any(row); Raider's latch line gated (one hunk). CHANGES.md Handoff + Unreleased
  (and the freighter key table), DESIGN.md slice-5 section (and Fit.Deploy's line).
- Harness rewrite (6.3): the slice-2 door check's jammed webifier is now asserted to have LET GO (was: still latched).
- Files: Statuses.cs, Raider.cs; SmokeTest.cs.txt (LaneALatchGateChecks after LaneATowChecks); CHANGES.md; DESIGN.md.

## Slice 5 final POST
- J1-J9 built and committed; typecheck + verify -Quick green at every job; NO engine run.
- Owed in the test phase: rung 3 x2 (quick,solo,solo): LaneAMeleeArc/Strike, LaneAPrismBand/Resolve/Split/Walk/Reflect/Ray,
  LaneASentryPrefer/Throw, LaneARaiderCall, LaneADecoy, LaneATow, LaneALatchGate + every rewritten check (freighter
  deploy sites, the ability sweep, the walls, the door's jammed row) + every beam/bolt/laser check (through Prism);
  rung 4: frame 43_lanea_flares_pull_a_mark; rung 5 six,six: the guest's R throw / recall, LaneADecoyHost/GuestChecks.
- Decisions D28-D37 above. Raider.cs hunks this slice: J3 Strike ray, J6 Call/CalledBy, J8 Towed, J9 latch gate.

### kits5 gate fix · PRE · the merge gate's four old-truth callers -- tier opus
- Intent: (1) SmokeTest :10487 the shot-table check asserts 8 rows (+ the reflect row: not AtPlayers, unguided,
  Sweep 6; Smoke still 4); (2) :11590 the wire-status check reads six wire statuses (total 10, wireable 6,
  Parrying == 32, bits 63 round-trip through FromBits); (3) ProveReach "deploy_range" throws the sentry at the
  pilot's own spot (SentryAt) and waits for the 0.8 s landing before taking it; (4) Shots.cs.txt frame
  79_freighter_turrets_and_bubble throws each sentry at its own cursor spot from the yard and waits for the last
  landing before the bubble and the snap.
- Files: tools/smoketest/SmokeTest.cs.txt, tools/screens/Shots.cs.txt, this ledger.
- HEAD 36d0cf839949fa110c81f743b13f83ceb06efc7c · SmokeTest.cs.txt 455ebd8f16344ec4a624f6e633ccf004412cfdd4 · Shots.cs.txt cf93f60b13d95cdc15b628e2042222a1bb6710dc
### kits5 gate fix · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase (rung 3 x2 for the three smoke checks; rung 4 + one read by eye of 79_freighter_turrets_and_bubble).
- Rewritten (6.3): "eight kinds of shot" (8 rows, Reflect == 7, not AtPlayers, unguided, Sweep 6, no smoke; Smoke
  count 4 kept); "six wire statuses and no seventh" (Parrying == 32, bits 63 round-trip, total 10, wireable 6);
  ProveReach deploy_range (SentryAt at the pilot's own spot, polls up to 2 s for the landing); frame 79 (three
  cursors 180 u apart, thrown from the yard, 0.9 s after the last throw before the bubble and the snap).
- Files: SmokeTest.cs.txt, Shots.cs.txt, this ledger.

## MERGE version-l (669c5a3) into wt/kits5 (9f8365f)
- PRE: merge of version-l (lane I items, raids, net2, wings, kits merged). Conflicts: scripts/PlayerShip.cs,
  docs/CHANGES.md. No wire id, enum value or slot index taken by both.
- scripts/PlayerShip.cs: union. Kept this lane's Prismatic/GuardAngle and the paint (F14) beside lane I's Web
  Breaker ApplyStatus (the pin's own hold clock) and HoldWeb; the host tick runs `_status.Tick; HoldWeb;` then the
  paint clock and TickThrows.
- docs/CHANGES.md: both Handoff entries and both Unreleased sections kept (kits5, then items).
- POST: typecheck 0 errors, verify -Quick ALL CHECKS PASSED. Next: merge into version-l.
