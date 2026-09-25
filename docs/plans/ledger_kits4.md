# Ledger: class-kits lane A, slice 4 (F8 helm, wards, press payload; F10 Mend)

Writer: one agent in worktree `WarShips_wt_kits4`, branch `wt/kits4`, cut from version-l f2f398b.
Spec: kits_v2 §5 (F8, F10 rows) -> kits_v3 §5 (+ the Grapnel pull, + a point in the press payload; the
v3 §3.2 pull / swing numbers, §3.3 the throw's point) -> kits_v31 §6 (unchanged: "+ the pull, + the point
payload"; F10 unchanged). Lane A's earlier record: `ledger_kits.md` (D1-D26+). Build phase: no engine.
A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits, redo it first.

## Decisions taken where the spec is silent (D-numbers continue as K4-n)

- **K4-1** F8 and F10 are FOUNDATIONS; their users land in slice 6 (the DD's Grapnel 6a, the FR's sentry
  throw 6b, the Tender's lance and Repair field 6b). No class row, stat row or bar change here. Each
  foundation is proved now by the harness calling it (as F1's Ramp was, LaneARampChecks).
- **K4-2** The press payload's point: `AbilityDef.TakesPoint`. The owner's cursor (AimPoint) rides with
  the press (`RequestAbility(id, targetId, at)`); the host writes it into the row's own slot
  (`Sl(id).At`) before Press, and a non-finite point presses nothing. The clamp to a reach on the same
  bearing is one pure rule (`Abilities.Toward`), the throw's 600 u clamp in 6b.
- **K4-3** "Helm" is the owner-side constrained flight (`HelmMoves.cs`): a row is a flight law run in
  LocalFlight INSTEAD of Steer while it lasts (B's Steer untouched; one branch at its call). Its first
  row is the tether (the Grapnel's pull, then its swing). Numbers come in a `HelmNums` the caller fills
  (6a reads them off the DD's stat rows); nothing is on a sheet yet.
- **K4-4** Host confirm: the host marks the move's slot (Left = its time, N = the anchor's NetId). The
  owner casts off if the slot is not marked within 0.5 s of its press, or once the host's slot ends.
- **K4-5** "Wards" = what the host allows a guest's reports while a confirmed helm move runs: the host's
  speed clamp (Drives.Clamp, F24) and the jump pricing read the move's own speed (the pull's 450 u/s)
  instead of the hull's top, so the pull is never clamped or priced as a jump.
- **K4-6** Cast-off keeps |v|: the hull is turned onto its velocity, so the keel carries the speed on.
  Casts off on: a second press (the row's), the time, the anchor gone or dead, a web (Pinned), Disabled,
  a warp charge begun, no confirm in 0.5 s, the host's slot ended.
- **K4-7** F10 Mend: `Mend.Give(to, amount, by, source)` is the one heal door, host only: never above
  MaxHp, nothing to a wreck, nothing for a non-finite or non-positive amount; the healer is credited
  only what landed (`PlayerShip.MendedBy[source]`). Regeneration stays where it is (it runs on every peer
  between reports; it is not a heal one ship gives another).

## Job list (foundations before the features that use them)

| job | what | files |
|---|---|---|
| kits4-J1 | F8 press payload point (K4-2) | Abilities.cs, PlayerShip.cs, SmokeTest.cs.txt |
| kits4-J2 | F8 helm moves: the tether (pull, swing), owner run, host confirm, cast-off (K4-3/4/6) | HelmMoves.cs (new), PlayerShip.cs, SmokeTest.cs.txt |
| kits4-J3 | F8 wards: the host's clamp and pricing read a confirmed move's speed (K4-5) | HelmMoves.cs, PlayerShip.cs, SmokeTest.cs.txt |
| kits4-J4 | F10 Mend (K4-7); the record (CHANGES Handoff + Unreleased, DESIGN) | Mend.cs (new), PlayerShip.cs, SmokeTest.cs.txt, CHANGES.md, DESIGN.md |

## Notes

- Merge risk: `DoAbility` gains a third parameter (the point). The harness reaches it by reflection
  (`doAb.Invoke` / `doAbility.Invoke`, ~19 lines) and `RequestAbility` by string: every one passes
  `Vector2.Zero` now. A parallel lane that adds such a call must pass 3 args (typecheck cannot see it).

## Jobs

### kits4-J1 · PRE · F8 press payload point -- tier opus
- Intent: K4-2. AbilityDef.TakesPoint; UseAbility sends AimPoint; RequestAbility/DoAbility take the point;
  DoAbility writes Sl(id).At (non-finite: nothing pressed); pure Abilities.Toward. Harness reflection calls
  pass Vector2.Zero. Checks: LaneA4PayloadChecks (solo).
- Files: scripts/Abilities.cs, scripts/PlayerShip.cs, tools/smoketest/SmokeTest.cs.txt, this ledger.
- HEAD f2f398be8164f0bee865d4dbd8f0b3c507426e77 · Abilities.cs 4f130c86b8b5160df23cfb8eb9358de085b58e9d · PlayerShip.cs 91bfcf083601bcebc85e830529be06b5adf5ded8 · SmokeTest.cs.txt 93096537210a252c1f5955f72522ecfbeda24102
### kits4-J1 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. `AbilityDef.TakesPoint`; `UseAbility` sends AimPoint for such a row; `RequestAbility(id, targetId, at)`
  and `DoAbility(id, targetId, at)`; DoAbility writes `Sl(id).At` (non-finite: nothing pressed); pure
  `Abilities.Toward(from, at, reach)`. The 19 reflection calls and the one string RPC in the harness pass Vector2.Zero.
- Files: scripts/Abilities.cs, scripts/PlayerShip.cs, tools/smoketest/SmokeTest.cs.txt (NEW `LaneA4PayloadChecks`,
  called after FieldsLiveRowChecks).
- Owed at rung 3 (`solo` x2): 3 x "F8: a point N u out at N deg, held to a 600 u reach ..."; 3 x "F8: a press that
  takes a point lands its cursor in the row's own slot"; 3 x "F8: a press whose point is not a number ..."; 3 x
  "F8: a row that takes no point is pressed ...". Watch: every doAbility.Invoke sweep line (now 3 args) and
  "RequestAbility" for a shut ability (rung 5).
- Checkpoint: the commit after this entry. Next: kits4-J2.

### kits4-J2 · PRE · F8 helm moves: the tether (pull, then swing), owner run, host confirm, cast-off -- tier opus
- Intent: K4-3/4/6. New scripts/HelmMoves.cs (HelmMove rows, HelmNums, HelmRun, HelmEnd; pure StopAt / Shortest /
  LineFor / PullTime / TetherStep; owner Fly / Begin / CastOff; host Confirm / Release). PlayerShip: the run, one
  branch at Steer's call in LocalFlight, BeginHelm / CastOff. Checks: LaneA4TetherChecks (pure) and
  LaneA4HelmLiveChecks (a live DD on a dummy).
- Files: scripts/HelmMoves.cs (new), scripts/PlayerShip.cs, tools/smoketest/SmokeTest.cs.txt, this ledger.
- HEAD 509680a10c463adedaccac2f607f1fcb1983459e · PlayerShip.cs 6e53084b9cbe4a826620188b1ba89cca8837c59c · SmokeTest.cs.txt 559dcaf956eaac518cf9afc00ccbb42f9ea6d5ec
- K4-8 (J1 follow-up): the payload's wire check (rung 5) lands with its first row, 6b's sentry throw ("a guest sees
  the throw land on the host's spot"); until a row takes a point no host-decided state reads it.
### kits4-J2 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. New scripts/HelmMoves.cs: `HelmMove` rows (`All` = { `Tether` }), `HelmNums`, `HelmRun`, `HelmEnd`,
  `HelmBody`; pure `StopAt / Shortest / LineFor / PullTime / Bow / TetherStep`; owner `Begin / Fly / End`; host
  `Confirm / Release`; `ConfirmWithin` 0.5. The tether HOLDS its speed round the anchor (`HelmRun.Side`) rather than
  reading it back off the velocity (that fed a 450 u/s pull's inward part into the slide, ~60 u/s a second).
  PlayerShip: `_helm`, `Helm`, `BeginHelm`, `CastOff`; LocalFlight: `if (HelmMoves.Fly(...)) _yawRate = 0; else Steer(...)`.
- Files: scripts/HelmMoves.cs (new), scripts/PlayerShip.cs, SmokeTest.cs.txt (NEW `GrapnelNums`, `TetherTop`, `BowOff`,
  `LaneA4TetherChecks`, `LaneA4HelmLiveChecks`, called after LaneA4PayloadChecks).
- Owed at rung 3 (`solo` x2): "F8: every helm move row ..."; 10 x "F8 tether: pulled from ..."; 3 x "... inside its N u
  stop: no pull"; 3 x "F8 tether: 5 s round a ..."; 3 x "... held 5 s round a ...: the line stops at"; live: "F8 helm: a
  press past 700 u is ..."; 3 x "a move the host never marks ..."; 3 x "a marked pull from ..."; 3 x "a second on A/D
  swings ..."; 3 x "cast off by Pressed / Host / Time ..."; 3 x "Webbed / Disabled / Drive ends the move ...".
  Traps to watch: the live checks use TargetDummy1 (46 u hull, stop 296 u) and the destroyer's `open6` slot as the
  host's mark; a stray web from an earlier check's raider would end a run as Webbed.
- Checkpoint: the commit after this entry. Next: kits4-J3.

### kits4-J3 · PRE · F8 wards: the host's clamp and pricing read a marked move's speed -- tier opus
- Intent: K4-5. Confirm(s, anchor, nums, time) also sets the ward (max of the pull and hypot(top, reel)) on the
  ship's HelmRun (host side); HelmMoves.Ward(s) reads it while the mark's slot runs; PlayerShip.ReportTop =
  max(TopNow, ward) feeds Drives.Priced / Drives.Clamp in ApplyState. Checks: LaneA4WardChecks.
- Files: scripts/HelmMoves.cs, scripts/PlayerShip.cs, tools/smoketest/SmokeTest.cs.txt, this ledger.
- HEAD 46e558328c60beefcb5771b5db46668b61c0d7d3 · HelmMoves.cs f2f2129bb29d99eab9cb856200213488cff2a2d5 · PlayerShip.cs 82b3d73988d6ad32631f194c95716f02232da973 · SmokeTest.cs.txt d484864deea8108eba7510cdce5ae572d1955e03
### kits4-J3 · POST
- Verdict: compiles; typecheck 0 errors, verify -Quick ALL CHECKS PASSED. engine-unproven: rungs owed in the final
  test phase. `HelmMoves.Confirm(s, anchor, nums, time)` (was (s, slot, anchor, time)) marks the slot and sets the
  ward on `HelmRun.WardSlot / Ward` = max(Pull, hypot(TopNow, Reel)); `HelmMoves.Ward(s)`; `Release` drops it.
  PlayerShip `ReportTop` = max(TopNow, ward); ApplyState's Priced and Clamp read it (B's lines, the figure only).
- Files: scripts/HelmMoves.cs, scripts/PlayerShip.cs, SmokeTest.cs.txt (NEW `LaneA4WardChecks`; the two Confirm
  calls in LaneA4HelmLiveChecks moved to the new signature).
- Owed at rung 3 (`solo` x2): 3 x "F8 ward: marked, a report at N u/s stands ...". Owed at rung 5 with 6a (the first
  row that marks a guest): "a guest's pull reaches the host unclamped, hull within 60 u" (v2's swing line).
- Checkpoint: the commit after this entry. Next: kits4-J4 (F10, and the record).
