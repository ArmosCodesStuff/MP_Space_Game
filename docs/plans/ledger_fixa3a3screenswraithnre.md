# ledger fixa3a3screenswraithnre (round a3, a3-screens-wraith-nre, kind code -> harness)

## PRE
- Reproduced: fixa3a3screenswraithnre_a screens, 53 frames, NRE _Shots.cs:525 (H.Yard null).
- Root cause: frames 76c..78d carry ARENA in the HUD. 37_mission_portal_open leaves the portal open (READY);
  LaneA6dRodFrames sprints the dart from (-900,1100) at rot 0.4, nose toward Hub.MissionPortalPos (-446,440),
  ~1000 u: it enters the 190 u radius and the game (correctly) takes the party to the arena. No Yard there.
- Not the wraith lane and not the game: the harness (kits6d rod frame). Round 1's ObjectDisposed camera was the same scene change.
- Fix: rod lane noses down-left (Pi + 0.4), away from the portal; the stale camera re-fetch dropped; a throw after
  the rod lane and before the hull block if home was left (fails on the old code).

## POST done: sweep complete, 169 frames, 0 exceptions (was 53 / 5)
- typecheck 0; quick ALL CHECKS PASSED (fixa3a3screenswraithnre_p/1).
- screens x2 (fixa3a3screenswraithnre_p/2, _p2/1): SWEEP DONE, 45_hull_and_rebuild + 46_damaged_miner shot, FAIL 0, exc 0; 76c now at home.
- seen: LINT 16 = 6d_k_stats_conditions x2 (pre-existing) + 73*_sniper HelpPanel 2052 px wide x14 (frames newly reached).
- CHANGES entry: screens: the Dart's rod frame sprints away from the open mission portal; the sweep no longer ends in the arena.
