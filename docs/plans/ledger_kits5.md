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
