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

## J1 PRE -- tier opus -- foundations (Lead.Jumped, Raider.Level/Worth, ExpFor, WaveDef fields, Mission routing, AddsFloor, bounty clock)
HEAD 3576d7d. Files: Missiles.cs 5fabd6a7, Raider.cs ae7dd1a1, Missions.cs e1afdde8, Waves.cs 728a92f5, Raids.cs 580898db, SmokeTest.cs.txt 9949bb60.
## J1 POST -- done. typecheck 0 errors, quick ALL PASSED. Files: Missiles, Missions, Waves, Raids, Raider, SmokeTest.
Checks: RaidsFoundationChecks (ExpFor literals, light 6 / heavy 18, Garrison routing N14, Lead.Jumped x3, vee slots,
AddsFloor 2/0); rewritten: the siege clock check (bounty 0/30, siege 12/25). Raider.Worth moved to J5 (unused till then).
engine-unproven: rungs owed in the final test phase (solo x2).
Job list revised at J2: the escort removal and the beam's start/floor moved INTO J2 (the Raider rewrite deletes
the escort internals, so Boss could not compile between them). J3 = bounty_adds + garrison slots; J4 = EXP pay;
J5 = guest checks + look; J6 = record.
## J2 PRE -- tier opus -- Squads.cs, Raider flies from its squad, Raids owns squads + post book, escorts deleted,
the beam charges on ANY pin (s.Target.Pinned) or ArmMax 5 s, the live escape floor.
(PRE written after the edits, a process slip: no interruption happened.) HEAD 1b1cbc5. Files at HEAD: Boss 85c742d6,
Hub d10481a4, Lancer 08ac0402, Raider 9691fc7a, Raids 0630bbc6, Waves c788f02e, MenuFoe 7e5b57f3, Shots 20b45d75,
SmokeTest 70a30bf0; Squads.cs new.
## J2 POST -- done. typecheck 0 errors; quick ALL PASSED. Hub hunks: SpawnRaider(Squad), Squads/Posts/FormSquad
faces, one comment. Lancer hunk: the beam row's escort fields -> ArmMax + floor fields. MenuFoe: one comment.
Checks new: RaidsSquadChecks (N1+N2 vee closes as one / still-or-moving pinned in 6 s, wire bits 1/4/8 + id bits
8-23 on the host, N5 warp, N4 leader + light lost, N3 twelve on one hull, N6 heavy never pins L1/20/40, N9 sticky,
N10 hide/return, N17 book empty); 6b rewritten (N8 lone heavy, N7 missile only on a pin, 700 burn cap at 2250 u);
arena beam block rewritten (arms, launches nothing; ANY pin starts it; floor literals 4.08/4.82/4.20/7.94; floor
at start with 5 webs; live floor stretch + 0.6 s after strip; overdue 5 s; PD kills a webifier; adds go with
the boss + 0 squads/posts); rewritten callers: lane-A burn/out-door clears, blockade Station/Circuit, patrol ids,
raid squad ids + "4200 u" text. Frames: 49a_squad_inbound, 49b_squad_lock_lines, 49_heavy_astern_missile (was
49_heavy_waiting_missile). Deleted: Raider.HeavyReach, Boss escorts/EscortsAt/LaunchEscorts, Raider escort/patrol.
Known broken (for CHANGES): a stretched wind-up replaces the host's lane; a guest keeps its first lane (its
strike sound early). engine-unproven: rungs owed in the final test phase (solo x2, screens: 49a/49b/49).
## J3 PRE -- tier opus -- bounty_adds (roster H,L,L,L, AddsFloor, FormFor 10, boss multipliers), Raids.TickGarrison (Hub's clock moved), slot arrival by hull or k x 30 s, 30 s refill with the same kinds. HEAD 8a0e732. Files: Waves 802bfb40, Raids 786e09a1, Squads 6f64ccb8, Hub f881e921, SmokeTest 1d09ef15.
## J3 POST -- done. typecheck 0 errors; quick ALL PASSED (re-run owed to the slot-Due tidy, a one-line change;
J4's quick covers it). Hub hunk: TickArena's garrison clock -> _raids.TickGarrison, the two fields gone, one face
RestartGarrison. Raids.Held (static) holds the arena clock for the harness. Raider.Worth set at the spawn (first
fill only; paid in J4). Checks new: RaidsAddsTableChecks (N11 schedule + 1..60 sweep, Rusty floor, N12
thresholds by literal, slot kinds, form-up distances 2600/3250/3040/1729/2940/2910, L18 P2 multipliers, only
bounty_adds pays, N14 routing); RaidsArenaAddsChecks (live L1: squad wave 1 of 2 webifiers at 2600 +-5% in
formation 9 s; N16 an add's web starts the beam; N13 refill empty at +29.5, same kinds at +30.5, Worth 0,
never above the roster). The Solo arena holds the adds while the boss's own moves are tested.
engine-unproven: rungs owed in the final test phase (solo x2).
## J4 PRE -- tier opus -- the adds pay: Raider kill branch -> Hub.PayKill -> host AwardKill + reliable NetKillExp -> each guest AwardKill at its own level. HEAD 5a3374d. Files: Raider 95a18a25, Hub 91e428b3, Progression 59a6058a, SmokeTest 279b0c63.
## J4 POST -- done. typecheck 0 errors; quick ALL PASSED. Raider: the kill branch pays (Worth > 0) before
RaiderDown; Hub: PayKill + reliable NetKillExp (new RPC: Net.Protocol changes); Progression.AwardKill;
PilotWindow's EXP note names the adds. Checks new: N15 by literal inside RaidsArenaAddsChecks (L6 light 6,
heavy 18 / 9 / 0 at PL 6 / 12 / 13, L39 light 3 at PL 78, refill 0, the sweep and a quiet withdrawal 0).
engine-unproven: rungs owed in the final test phase (solo x2).
