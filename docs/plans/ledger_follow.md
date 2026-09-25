# Ledger: follow-ups (the coordinator's list, before the test phase)

Writer: one agent at a time in worktree `WarShips_wt_follow`, branch `wt/follow`, cut from version-l d524c33.
BUILD PHASE: no engine run; per job typecheck + verify -Quick + a read of the own diff; every check written now,
run in the final test phase. A PRE with no POST is an interrupted job: compare the hashes, revert half-made edits,
redo it first. Checks go in NAMED methods `Follow<Thing>Checks` (solo) / `Follow<Thing>HostChecks` (rung 5).

## JOB 0 -- the job list (foundations first)

- **follow-J1 Fingerprint hashes constant collections.** Net.Plain/Show: a generic System.Collections.Generic
  collection of Plain types is Plain; Show writes an IList in order, an IDictionary and a set in key order. The
  foundation: `[Live]` (LiveAttribute, Net.cs) marks a static readonly collection a RUN fills in (the pilot, the
  session, caches): the fingerprint skips it. Every such field marked (grep of scripts/, 2026-09-25). Newly hashed:
  NetIds.Widths, Spawns.All, Hints.All, Sfx._gap, Abilities.Reserved, AllStats.Rows, Equipment.ByIdMap, Classes.ById.
  Comments that said "a dictionary is not hashed" (Charge.cs, Character.cs, Net.cs) rewritten. Checks:
  FollowFingerprintCollectionChecks (3 varied tables x an entry moved: FingerprintNow leaves Net.Protocol, back in
  finally), and a guard: every static readonly generic collection in the assembly is either [Live] or has a part.
- **follow-J2 A heavy leads on its first throw after a retarget.** Lead (Missiles.cs) keyed on WHO it watches:
  `Watch(who, at, dt)` drops the old target's sample on a change (a retarget is not a jump), `Ready` once two
  frames of the same target are in; Raider throws only when `_lead.Ready`. The Director's own reset (PlayerShip
  `_directed`) and Lanes' gun move onto the keyed Watch (one mechanism). Checks: FollowLeadRetargetChecks (pure,
  3 varied) and FollowHeavyRetargetLeadChecks (a gunship called off a sentry onto a pinned, moving pilot: its
  first blast lies ahead along the velocity by v x 10 s, 3 varied speeds and bearings).
- **follow-J3 The host prices a warp inside the drive's cooldown.** Drives.Warranted(cool) (cool <= CoolGrace);
  Priced: a jump seen while the host's slot still cools is priced as an unwarranted snap (the whole distance over,
  no safe range) and the cooldown restarts. Checks: FollowWarpCooldownRuleChecks (solo: inside, edge +/- margin,
  after, on Priced itself) and FollowWarpCooldownHostChecks (host role: the guest's copy fed ApplyState in one
  frame: a second warp inside the cooldown is disabled, one after it is not). CHANGES.md in this job (last).

## follow-J1 PRE -- tier opus
Intent: the fingerprint hashes constant List / Dictionary / set tables; [Live] marks what a run fills in. HEAD d524c33.
Files+hashes: Net.cs 4ce1442b, Charge.cs f7b0cc4a, Character.cs 2465da7e, Abilities.cs 8686e20d, Assets.cs 101dac18,
Combat.cs 2167c34e, Equipment.cs ddc1635c, Fx.cs 80ccf9f1, Ids.cs 163284b9, Link.cs c778b0b6, Missions.cs 877752b8,
Session.cs 32035dca, Settings.cs 4c749512, Sfx.cs de506649, Yard.cs 8b627500, SmokeTest.cs.txt a94b5c6b.
## follow-J1 POST -- done (typecheck 0 errors; verify -Quick ALL CHECKS PASSED; own diff read)
Net.Plain + Collection/IsSet, Show: IList in order, set in key order (IDictionary was already key-sorted); LiveAttribute
(end of Net.cs); [Live] on 34 run-filled fields (Abilities 2, Assets, Character 13, Combat 2, Equipment.SheetCache, Fx,
Ids.Handed, Link, Missions._levels, Net._asked, Session 4, Settings.Keys, Sfx 2, Yard 4). Newly hashed besides the five:
AllStats.Rows, Equipment.ByIdMap, Classes.ById (derived constant tables). Charge.cs + SmokeTest comment de-staled.
Check: FollowFingerprintCollectionChecks (solo, after LaneAChargeTableHashedChecks).
engine-unproven: rungs owed in the final test phase. Next: follow-J2.
