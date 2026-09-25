# ledger_rvhv — Fable review fixes, scope hv (hv-R6c-1..7)

## PRE rvhv-1 (tier opus, review fixes hv-R6c-1..7)
intent: apply review_tasks.json entries hv-R6c-1..7, each with its check; typecheck + verify -Quick; prove quick,solo,six (tag rvhv_a)
HEAD 1cd733ef758e80eaa84af974c37d394ae4759a96
scripts/Ships.cs babc8caee6b545869619fc03e12af1718582e68c
tools/smoketest/SmokeTest.cs.txt 6d35985a0fb27c294a42de8f2ec1a210ad1d0293
docs/plans/cards/warden.md 3661bd48206a9fbddfcbe8ae067d36766febc256
scripts/Raider.cs 1f5c2e67d69fbcc84a755d4ee975fe1ecb8d5647
scripts/Squads.cs 7973e37a643b981acd236827867f42f1ebe399bc
docs/CHANGES.md abdf0e431c826fb3f29a182a47d14fa8dee03c90
docs/DESIGN.md d435428b890027b2f703b6860d77e06c628627ac
scripts/Zones.cs c3d0423812fea0491f499c26b9609a4fcad66dcb
scripts/Abilities.cs 98463948b56b01d00e657d4bcdc4f90ac51d2918
scripts/PlayerShip.cs 097c434df09c3a96ff76f1cf28fd4745791a54a3

## POST rvhv-1 (commits 971e418, b244497)
- hv-R6c-1 applied: Ships.cs Warden pd_damage override + its comment gone (sheet 0.5 / 0.5 s = 1 DPS); rewritten SustainedDps (+0.5/0.5), LaneAPassivePdChecks Warden row (1, 0.5, 420), heavies-lane Warden PD block (1 DPS; 3.5-4.5 in 4.1-4.4 s after the first shot); card/CHANGES/DESIGN. solo: PASS.
- hv-R6c-2 applied: Raider pinned |= caller of its squad's Call; Squad.Call sets _sinceBurn = Reboost. New LaneA6cTauntChecks part 2 (3 runs). DEVIATION: the throw is read from the Raid warning circle at the Warden (Hub.ThrowMissile is a predicted blast, no Shot with TargetId: the entry's InFlight() read can never see it). D57 out of CHANGES Known broken, DESIGN 6c line. solo: PASS x3.
- hv-R6c-3 applied: Raider latch gate refuses Def.Cc == Pinned on an Unwebbed target; LaneA6cWhirlChecks part 2 rewritten (unlatched, hull == MaxHp sampled at the spin's end, before the re-latch). solo: red, same as version-l (the Warrior is wrecked by then: whirlwind 0/0/0/0 and "takes it again" fail on rvcap_a too); the new clauses read True.
- hv-R6c-4 applied: Zones tick restarts from now only after a gap of a whole tick (DEVIATION: the entry's Max(next, Age) + Every drops the remainder while inside; intent kept). New out-and-back-in part in LaneA6cCurtainChecks. solo: PASS x3.
- hv-R6c-5 applied: AbilityDef.Sheds + CallSpec Call (Reach, Ring); Whirlwind/Taunt press RunFor (Time/Guard/Cooldown rows); RunFor zeroes a Swing row's Own; Whirl() and Taunt() deleted. solo: WARRIOR SPIN / WARDEN TAUNT witnesses PASS, taunt runs PASS.
- hv-R6c-6 applied: DashSweeps ends a dash row (Left 0, Hardened cleared) while Disabled. New LaneA6cLungeChecks part 4 (disable at 0.08-0.15 s; narrowed from 0.18 so the 330 u gunship is never on the sweep's edge). solo: red inside the version-l Warrior breakage (the lunge moves 0 u from run 2 on, on rvcap_a too) -- unproven.
- hv-R6c-7 not applied: false premise. guest2 is in the host/guest/guest2 world (Mp), where the guest flies a Carrier (no Reload row); the Sniper reload runs in ahost/aguest, which has no third pilot. G4 needs a third arena role (run.ps1 + ArenaMp): coordinator's call.
- Own red fixed: rvhv_a solo threw at WARDEN TAUNT (the PD probe freed by QueueFree stayed in Hub.Raiders; the Taunt's call read its Position) -> TakeDamage; rvhv_b clean of it.
- Proof: rvhv_a (971e418) quick green, solo red (own throw, fixed); rvhv_b (b244497) quick green, solo 214 fails: none traced to these edits (each also red on rvcap_a or rvlt_a2, or a one-seed flake in lanes these edits do not touch). rvhv_c six: 253 fails vs 243 on rvcap_b; the extra ones are the Dart pepperbox/rod and a build-hex mismatch, none of them in these lanes; the arena guest taunt passes, the reload G1-G3 pass.
- next: the Warrior lane's version-l red (Warrior wrecked after the lunge's run 1) blocks proving hv-R6c-3/6.
