# Ledger: lane G (raids) -- wt/raids

Spec: raids_squads_adds.md (all), numbers_curve_raids_items.md §2 + §4 rows 5-7, kits_v31.md §8 row G,
models/raids_v2_model.py, adds_model.py, numbers_v2.py (§2 constants). Rulings: version-l docs/plans/README.md
"Raids and bosses" (escorts = squad wave 1 from L1; ANY add's web starts the beam; escape >= 0.6 s; H,L,L,L;
30 s refills with the same kinds; refills pay no EXP; adds pay EXP; no boss hull trim).
BUILD PHASE: no engine run. Per job: typecheck + verify -Quick + own diff read. Checks written, run later.

## Defaults taken (open decisions)
- D-a Par.cs does not exist in this tree (lane F). The escape floor reads a beam-row figure instead:
  `StripDps` = 0.7 x par's L1 DPS against craft (57.6, numbers run.txt:118) = 40.32, with the pinner hull
  taken at ROW hull x (Hp / MaxHull). Raiders and par scale alike (numbers §2.4), so the strip is the same
  at every level; lane F may swap StripDps for Par.Dps(L, craft) x level scale. StripShare = 55/46.
- D-b The beam's escort row fields are deleted (numbers §4 row 5); the Rusty's squad-1 floor of 2 pinners
  is a BossType row field (`AddsFloor` = 2 Rusty, 0 Drake), read by the roster: slot 1 lights = max(floor, dealt).
- D-c No `escort` doctrine: with the escorts gone (ruling) nothing flies it. Rows: lone, patrol, gank.
- D-d Raider.Pays is a figure, `Worth` (EnemyDef.Exp x WaveDef.Exp; 0 pays nothing), set on the first fill only.
- D-e Readability scope in this lane: flag bits (lead, lock, squad id), link lines and lock lines drawn.
  NOT built here (not this lane's files): radar diamond/bracket/chevron, HUD "GANK:" line, name labels.
- D-f "the squad RPC" = the one new reliable RPC, `NetKillExp` (add EXP to guests) + the squad bits in the
  raider packet; guest-role checks cover both and a beam started by an add's web.

## Jobs (foundations first)
- J1 foundations: Lead.Jumped; Raider.Level/Worth; Missions.ExpFor (KillExpFor through it); WaveDef
  Doctrine/Roster/Mission/Exp/FormFor, HullShare as a func, WaveCrew Sweep, WaveBrief.Mission, For() on Mission;
  BossType.AddsFloor; Bounty clock 0/30.
- J2 Squads.cs + Raider flies from its squad (posts book, time on target, capped keeping, heavies laser
  unpinned, missile on a pin, no edge wait); Raids builds squads; harness callers + 6a/6b/6c rewrite; N1-N10, N17.
- J3 Boss: escorts deleted; beam charges on ANY pin of its target (s.Target.Pinned) or ArmMax; live escape
  floor. Harness escort/beam block rewritten; N16.
- J4 bounty_adds row, roster H,L,L,L, Raids.TickGarrison (Hub clock moved), arrival by hull/30 s, refill 30 s
  same kinds; N11-N14.
- J5 EXP: kill branch pays, Hub.PayKill + NetKillExp, Progression.AwardKill; N15.
- J6 wire + look: flag bits, link/lock lines; guest checks (N18), frames (N19).
- J7 record: CHANGES Unreleased + Handoff, DESIGN traps.

