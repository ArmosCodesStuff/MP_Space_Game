# ledger_fields (lane D, wt/fields): F9 fields, zones, marks (kits_v31 §6 F9, §3.3; kits_v3 §5, §3.2, §3.5; kits_v2 §5)

BUILD PHASE: no engine run. Per job: typecheck + verify -Quick + a read of the diff. Checks written now, run in the final test phase.

## JOB 0 -- the lane's job list (foundations first)
- J1 FOUNDATION `Fields` (in Fx.cs): a slot-drawn field is a ROW {Id, Slot, Look, RadiusStat|HullShare, Tint, FadeStat, TagStat};
  `Fields.Up(ship)` (pure, what is up now) and `Fields.Draw(ship)` (one switch on Look). PlayerShip._Draw's hardcoded bubble
  block becomes row `bubble` (deleted there). Checks: pure contract + live bubble on 3 varied spots; guest sees the bubble field (rung 5).
- J2 ROWS on J1: `patrol` (slot super: dashed 600 u ring, patrol_range), `taunt` (slot taunt: hex shimmer + "-33%" tag from
  taunt_guard), `boost` (slot boost: the drive's plume), and Fx row `taunt_ring` (the 1000 u flash on the press).
  Checks: pure per-row literals; live wherever a class owns the slot (other lanes build the abilities); frames 81-83.
- J3 FOUNDATION + ROWS: FxShape Debris / Sparks / Puffs / Scar, FxDef.With (companions raised on every peer from ONE raise),
  FxDef.Cap (per anchor), a node that rides nothing but READS its anchor (Loose), `Fx.Rip(anchor, hook, toward)`, seeded tumble
  `Fx.Tumble`; rows rip, rip_sparks, rip_smoke, scar. Checks: rip on dummy / pylon / base / Lancer at VaryAngle, companions,
  scar cap 3, lifetimes, determinism; guest draws the chunk within 20 u of the host's (rung 5); frame 84.
- J4 RECORD: CHANGES Unreleased + Handoff, DESIGN trap note; final POST lists what the test phase owes.

Defaults taken (no ruling found in docs/plans/README.md):
- D1 slot ids the field rows key on: `super` (lane E's Supercarrier), `taunt` (Warden Taunt), `boost` (lane B's drive row).
  A lane that picks another id changes ONE row here. Checks print `NOTE unbound field` until a class owns the slot.
- D2 the rip's DAMAGE (1% MaxHp + 10 through Dealt.Deal, credited grapnel) belongs to the grapnel (lane A F8, not built);
  this lane gives it `Fx.Rip` for the look only. Debris sound `grapnel_rip`: row name reserved, null until a make_sounds row exists.
- D3 Unmask panels: never built in this tree; nothing to delete.

## PRE J1 -- Fields table (foundation), the bubble as its first row
tier: opus. intent: FieldDef/Fields in Fx.cs; PlayerShip._Draw's bubble block -> Fields.Draw(this); checks FieldsContractChecks, FieldsBubbleChecks, guest FieldsGuestChecks.
files: scripts/Fx.cs, scripts/PlayerShip.cs (_Draw only), tools/smoketest/SmokeTest.cs.txt
HEAD: c639504e620e016f2d358223a68d167ac59f0da2
  scripts/Fx.cs 7178f9d49a0e60badfa40b127f77bfec5bff14c2
  scripts/PlayerShip.cs 2a91383506540c06018bbd6f4240f69b5955f7aa
  tools/smoketest/SmokeTest.cs.txt 9949bb6009a1bf028fa1a6c78bb9facb92693c3a

## POST J1
verdict: compiles (typecheck 0 errors; verify -Quick ALL CHECKS PASSED). engine-unproven: rungs owed in the final test phase.
files: scripts/Fx.cs (FieldLook, FieldDef, FieldUp, Fields), scripts/PlayerShip.cs (_Draw: bubble block -> Fields.Draw), SmokeTest.cs.txt
(FieldsContractChecks + FieldsBubbleChecks after the solo bubble checks; FieldsGuestBubbleChecks after "arena guest: its bubble is the host's").
owed: rung 3 x2 (contract + live bubble), rung 5 (guest bubble field); frame 79_freighter_turrets_and_bubble re-read by eye (same draw, now via the row).
next: J2.

## PRE J2 -- field rows: patrol ring, taunt shimmer + tag, boost plume; Fx row taunt_ring
tier: opus. intent: FieldLook Dashed/Shimmer/Plume + 3 rows (D1 slot ids super/taunt/boost), Fx.TauntRing=10 row; checks FieldsRowChecks (pure), FieldsLiveRowChecks (NOTE when unbound), FieldsTauntRingChecks; frames 81_patrol_ring, 82_taunt_shimmer, 83_boost_plume, 83b_taunt_ring.
files: scripts/Fx.cs, tools/smoketest/SmokeTest.cs.txt, tools/screens/Shots.cs.txt
HEAD: e20b886ecfe1b2983e03f6a9aa885f45dd6e181c
  scripts/Fx.cs d68f9e424568643ae1b68539b493b81e052837e0
  tools/smoketest/SmokeTest.cs.txt 3cb15b0f1b15ed066d618a27b68a97b30b011e36
  tools/screens/Shots.cs.txt 20b45d75d2da1b5f72b79798cbcf86619432b69f

## POST J2
verdict: compiles (typecheck 0; verify -Quick ALL CHECKS PASSED). engine-unproven: rungs owed in the final test phase.
files: scripts/Fx.cs (FieldLook Dashed/Shimmer/Plume; rows patrol/taunt/boost; Fx.TauntRing = 10, row taunt_ring),
SmokeTest.cs.txt (FieldsRowChecks + FieldsTauntRingChecks after FieldsBubbleChecks; FieldsLiveRowChecks after LaneAHeavyRowsChecks),
Shots.cs.txt (FieldsFrames after frame 79: 81_patrol_ring, 82_taunt_shimmer, 83_boost_plume, 83b_taunt_ring).
owed: rung 3 x2; rung 4 frames 81/82/83/83b by eye (81-83 print "shot skipped" until lanes E / A(Taunt) / B merge their slots);
FieldsLiveRowChecks prints "NOTE unbound field row" until then -- after those merges the NOTE lines must be gone.
next: J3.

## PRE J3 -- the torn chunk: FxShape Debris/Sparks/Puffs/Scar, rows rip/rip_sparks/rip_smoke/scar, Fx.Tear
tier: opus. intent: FxDef With/Cap/Loose/Count; FxNode Start + seeded Fx.Tumble; Fx.Tear(anchor, hook, toward) raises ONE rip (Anchor = NetId, Size = 0.16 x hull art length); companions spawned on every peer; Shots.Shards made internal (one word) for the chunk's outline. Checks FieldsRipChecks (dummy x3 / pylon / base / Lancer), host raise + guest watch (rung 5); frame 84_rip_chunk_and_scar.
files: scripts/Fx.cs, scripts/Shots.cs (one word), tools/smoketest/SmokeTest.cs.txt, tools/screens/Shots.cs.txt
HEAD: e2a150545fe8355018d00df806f49a6887c2aabe
  scripts/Fx.cs 6708cbc8d3b47d6297a6fce2c09f9a1958fff814
  scripts/Shots.cs 7e0371e1688ca3b10d069b84db62dc9d36037c12
  tools/smoketest/SmokeTest.cs.txt 744894650335d775046565b9d75ec6744268f9f4
  tools/screens/Shots.cs.txt 29757171a6848358671d59844dd1b7e5fb7b07d7

## POST J3
verdict: compiles (typecheck 0; verify -Quick ALL CHECKS PASSED). engine-unproven: rungs owed in the final test phase.
files: scripts/Fx.cs (FxShape Debris/Sparks/Puffs/Scar; FxDef With/Cap/Loose/Count; rows rip 11, rip_sparks 12, rip_smoke 13, scar 14;
Fx.Tear / HullArt / HullLength / LengthOf / Seed / U / Tumble; FxNode Start, Seed, cut-from-art UVs), scripts/Shots.cs (Shards private -> internal),
SmokeTest.cs.txt (FieldsRipChecks, FieldsScarCapChecks, FieldsRipYardChecks after FieldsTauntRingChecks; Lancer after LaneAOutDoorBossChecks;
base + pylon after LaneAOutDoorBaseChecks; rung 5: FieldsHostTears before the ahost 2 s wait, FieldsGuestRipWatch after cruiseWatch,
FieldsGuestRipChecks after GuestSeesTheFight), Shots.cs.txt (84_rip_chunk_and_scar in FieldsFrames, 84b_rip_on_boss after frame 39).
owed: rung 3 x2 (dummy x3 incl. a 10.3 s timed run, scar cap, Lancer, base, pylon); rung 5 x2 (guest draws the host's chunk);
rung 4 frames 84 and 84b by eye -- confirm DrawPolygon's UVs (normalised 0..1 assumed) show the hull plating, not a smear.
risk noted: a chunk and scar are children of their anchor, so a boss killed within 3 s of a tear takes its chunk with it.
next: J4 (records).

## PRE J4 -- records
tier: opus. intent: CHANGES Handoff + Unreleased (one hunk each), DESIGN note on fields and one-raise effects; final POST (what the test phase owes).
files: docs/CHANGES.md, docs/DESIGN.md, docs/plans/ledger_fields.md
HEAD: aabfb255cb9652e649ccc3635127a7b71baac995
  docs/CHANGES.md 599d498bd5f33695107e66d9bb26924863012e64
  docs/DESIGN.md 8545bdea6ff8dab64c0a56282434bd1e257fe1c8

## POST J4 (lane D final)
verdict: every job committed; typecheck 0 and verify -Quick ALL CHECKS PASSED at HEAD. engine-unproven: rungs owed in the final test phase.
files: docs/CHANGES.md (Handoff + Unreleased "Class kits, lane D"), docs/DESIGN.md ("Fields and one-raise effects"), this ledger.
THE FINAL TEST PHASE OWES (the lane's merge chain: quick,solo,solo,six,six,screens):
- rung 3 (solo) on two seeds: FieldsContractChecks, FieldsBubbleChecks, FieldsRowChecks, FieldsTauntRingChecks, FieldsRipYardChecks
  (FieldsRipChecks x3 dummies, one timed to 10.3 s, + FieldsScarCapChecks), FieldsLiveRowChecks, FieldsRipChecks on the Lancer (arena)
  and on the base and a pylon (siege).
- rung 5 (six) twice (new guest checks): FieldsGuestBubbleChecks, FieldsGuestRipChecks (host side FieldsHostTears).
- rung 4 (screens): frames 81_patrol_ring, 82_taunt_shimmer, 83_boost_plume, 83b_taunt_ring, 84_rip_chunk_and_scar, 84b_rip_on_boss by eye
  (84/84b: the chunk shows the hull's plating -- DrawPolygon UVs assumed normalised), and 79 unchanged (bubble now drawn by its row).
- after lanes E (super), A (Warden Taunt) and B (boost drive) merge: the "NOTE unbound field row" and "shot skipped 81/82/83" lines must be gone;
  if one stays, that lane named its slot differently: edit the one row in Fields.All.
defaults taken: D1 slot ids, D2 rip damage left to the grapnel (Fx.Tear is the look), D3 no Unmask panels existed.

## PRE gate fix -- the opus merge gate's four findings
tier: opus. intent: (1) Loose pieces leave their anchor for the world (Combat.World) the frame they go up, companions built by the chunk then (Loose ones in the world, the scar on its hull while it stands), so a kill within 3 s keeps the chunk (invariant A: no companion object is made unless it is added); FieldsRipChecks tests Anchor not parent; new FieldsRipOutlivesChecks frees the torn hull 0.2 s after a tear and the chunk is still drawn at 1 s. (2) dummy tears pass 14.72 (kits_v3 3.2 'dummy 15 u', within 0.3 of 15). (3) FieldsGuestRipWatch compares flight with the seeded direction rebuilt from Fx.U(chunk.Seed,0), not the 25 deg bound. (4) scar drawn from Outline(1f). Delete the CHANGES Known-broken line and the DESIGN trap bullet.
files: scripts/Fx.cs, tools/smoketest/SmokeTest.cs.txt, docs/CHANGES.md, docs/DESIGN.md, docs/plans/ledger_fields.md
HEAD: 70d524e91df14a94ce332b7bb2fd3618f5a629d7
  scripts/Fx.cs 07d5e196522e1fbb69269c98ee4448288078116c
  tools/smoketest/SmokeTest.cs.txt 7eb58a6232732108108093b893922296d2fe4b52
  docs/CHANGES.md 20a5b73ba0329aa7058d93fd7fb2523efc5278b3
  docs/DESIGN.md 1437ce91cc1186f01b3d37e722c91e578c1cc6c8

## POST gate fix
verdict: compiles (typecheck 0; verify -Quick ALL CHECKS PASSED). engine-unproven: rungs owed in the final test phase.
files: scripts/Fx.cs (FxNode.Settle: companions built on the anchor in one deferred step, a Loose piece Reparent'ed to
Combat.World once read; Fx.Entered moved to _EnterTree so a reparented node stays listed; scar Outline(1f); Tear header:
raise BEFORE the hit), SmokeTest.cs.txt (FieldsRipChecks: Anchor == NetId and parent == Combat.World; dummies pass 14.72
+ a check 0.16 x LengthOf(dummy) within 0.3 of 15; FieldsTearAhead + FieldsRipOutlivesChecks on pylons[2], killed 0.2 s
after its tear, chunk drawn at 1 s with 2 sprays; FieldsGuestRipWatch vs the seeded line, budget atan((20 + speed x
(flight + wire)) / distance)), CHANGES (Known-broken line cut), DESIGN (trap bullet replaced by the true rule + the
raise-before-the-hit trap).
also fixed: FieldsRipChecks(pylons[0]) ran after all four pylons were killed (a freed node); moved before the shield block.
owed: rung 3 x2 (siege: pylon rip + FieldsRipOutlivesChecks; yard dummies at 14.72); rung 5 x2 (FieldsGuestRipChecks);
rung 4 frames 84/84b (scar now the chunk's full size).
open for the coordinator: the grapnel (lane A) must call Fx.Tear BEFORE its 1% + 10 hit, or a killing rip finds no anchor.
next: none (lane D done).
