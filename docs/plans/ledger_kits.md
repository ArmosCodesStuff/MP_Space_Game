# Ledger: class-kits batch, lane A (combat core)

Writer: one agent in worktree `WarShips_wt_kits`, branch `wt/kits`, started at 03d47ba.
Spec: `kits_v31.md` §8 (Step 0 item 3, lane A slices 1-2), §6 foundations, v3 §5 (`kits_v3.md`),
v2 §5 (`kits_v2.md`). A PRE with no POST is an interrupted job: compare the files with the hashes,
revert or keep the half-made edits, then run the job again (CLAUDE.md §2b rule 3).

## Decisions taken where the spec is silent

(D-numbers; each is the plan's default or the smallest reading of it.)

- **D1** `raids_v2.md` is already in the repo, byte-identical, as `raids_squads_adds.md`. Not copied a
  second time (two copies is two truths); the README row and kits_v31's references name it instead.
- **D2** The copies are named `kits_v2.md` / `kits_v3.md` beside `kits_v31.md`; their text still says
  `signoff_v2.md` / `raids_v2.md` (unchanged copy), so each header maps the old names.
- **D3** Spec read for the code slices: kits_v2 §5 (foundations, OutGuards, build order) and the card
  sections a foundation names; kits_v3 §5 and §7; kits_v31 §6, §8, §10. The rest was diffed / skimmed
  by heading to keep this agent under its context cap.
- **D4** F2's new statuses (Parrying 32 on the wire; Suppressed 64, Dazzled 128, Jammed 256 host-only)
  and the host-only wire mask land with their first users (6a Suppress, 6c Prism/Flares, 6d EMP):
  enum members are under UNUSED ANYWHERE (tools/analyse/xref.py), so declaring them now fails rung 2.
- **D5** The share rule's HOLD half (F1) lands in slice 1, because the railgun lock needs it;
  Add and Ramp stay in slice 2.
- **D6** A hold multiplies the whole helm after the lifts' sum: both speed caps, both thrusts and the
  rudder, so x0 roots the hull heading included (the railgun keeps "cannot turn or thrust"). It is
  `AbilityDef.Hold` (a share on the row, 1 = none). If the 6c Anchor must turn while rooted, 6c
  splits the rudder off as its own row. Lane B puts the same hold on strafe_speed.
- **D7** Disabled on a pilot: no helm, no main guns (they keep reloading), no ability presses (owner
  Refuse says DISABLED; the host's DoAbility gate drops them; a Local toggle and the wreck's reboard
  still work), no warp; PD, the wing and turrets already out keep fighting.
- **D8** F16 (passive PD) is its own job (J3): it deletes the pd ability, window, recharge and
  Fit.AlwaysPd across ~25 harness lines, two of them rung-5 guest checks. The per-hull numbers (CV
  3 -> 2 mounts, Warden 10 -> 1 DPS) go with their cards in 6a / 6c, where the power table is
  re-witnessed.
- **D9** Hardened takes its share from the applier: `IStatused.ApplyStatus(s, seconds, share)`;
  with no share given, the Guards row's default (0.5). Two hardenings at once keep the stronger
  (lower) share until the status lapses. `StatusGuard.Stat` is deleted (its one user was rush_guard).

## Jobs

### J1 · PRE · specs into the repo
- Intent: copy v2 and v3 sign-offs into `docs/plans/` unchanged (one-line origin header), list them in
  the README table, repoint references to scratchpad paths, and add a short `docs/DESIGN.md` section
  pointing at them (kits_v31 §8 Step 0 item 3).
- Files: docs/plans/kits_v2.md (new), docs/plans/kits_v3.md (new), docs/plans/README.md,
  docs/plans/kits_v31.md, docs/plans/numbers_curve_raids_items.md, docs/DESIGN.md, this ledger.
- Start: 03d47baa504be57aad842eccdfff0b49218623bc
- Hashes: README.md 677dd2a1 · kits_v31.md 0b3f77b7 · DESIGN.md 8d15c0e2 ·
  numbers_curve_raids_items.md 72c3f80d

### J1 · POST
- Verdict: done (docs only; no code, no rung needed).
- Scratch `kits3/signoff_v31.md` vs repo `kits_v31.md`: the repo copy carries the 2026-09-25
  reconciliation (L1 3091/2847 -> 3222/2968; the x0.64 cut -> Lancer x0.744 / Drake x0.787; §5 moved
  to `numbers_curve_raids_items.md`; decision 15 reversed: no boss-hull trim for adds; decisions 6, 8,
  9, 14 marked settled; check 11's literals 1.075/3091 -> 1.098/3222) and the Echo Rewind ruling
  (8 s, hull too, 0.5 s ring of 16). Nothing else differs.
- Files: kits_v2.md, kits_v3.md (new); README.md (3 rows, raids row); kits_v31.md (lines 15, 39, 501,
  513); numbers_curve_raids_items.md (line 44); DESIGN.md (one pointer section under Ship classes).
- Next: J2, lane A slice 1.

### J2 · PRE · slice 1a: B, F2 (with the hold), the stale comments
- Intent: B `s.Next += m.Tick` (Boss.cs Burn); F2 per D4/D6/D7/D9 (railgun lock -> `Hold = 0`);
  stale comments Raider.cs:17-20 (7 s -> 12 s flight), :48 and Waves.cs:174 (1.1 -> 1.025 = Missions.S),
  Enemies.cs:44 (7 s). Checks in lane A's own methods: `BurnClockChecks` (called from the Lancer
  arena, after the fallback beams), `HoldAndDisabledChecks`; rewrites of the sniper's charge checks
  (Status.Disabled -> the hold) and the Hardened share check.
- Files: scripts/Boss.cs, Statuses.cs, PlayerShip.cs, Abilities.cs, Targeting.cs, Raider.cs, Waves.cs,
  Enemies.cs, Deployed.cs, Emplacements.cs, UtilityShip.cs; tools/smoketest/SmokeTest.cs.txt;
  docs/CHANGES.md.
- Start: e4bd15156a6302a7fd2ae133afd7c8b8990deb32
- Hashes: Boss 28201da9 · Statuses ea97ced8 · PlayerShip 6feada71 · Abilities 8807eee5 ·
  Targeting cf2e7399 · Raider 11393eb2 · Waves 3a5fd6f9 · Enemies 9412635e · Deployed 9fee7444 ·
  Emplacements b7df20d3 · UtilityShip 83d1ab96 · SmokeTest 00e6f1ba · CHANGES 3631a5f6

### J2 · POST
- Verdict: rung 1 (typecheck, 0 errors) and rung 2 (`verify.ps1 -Quick`, ALL CHECKS PASSED, UNUSED 0)
  green in the worktree. Rung 3 NOT run (no engine runs in this lane).
- Files: Boss.cs (Burn `+=`), Statuses.cs (share from the applier, StatusGuard.Stat gone),
  Targeting.cs + the six IStatused implementers (share parameter), PlayerShip.cs (Held, Steer's
  hold, PressHeld, FireControl and CanWarp gates, the rush's share, ChargeRail without Disabled),
  Abilities.cs (`Hold`; railgun `Hold = 0`), Raider.cs / Waves.cs / Enemies.cs (stale comments),
  SmokeTest.cs.txt, CHANGES.md.
- Checkpoint: the commit after this entry ("Kits lane A slice 1a").
- Next: J3 = F16 passive PD (D8), then slice 2.

## Engine rungs owed to the main session (run in the worktree, rebased, one engine at a time)

| after | rung | seeds | look for (PASS lines) |
|---|---|---|---|
| J2 slice 1a | 3 (`tools\smoketest\run.ps1 -Solo`) | two different seeds | "the burn clock:" x3 · "the railgun's charge is a hold of x0" x3 · "a DISABLED warden" x3 · "Hardened at the applier's share" x3 · rewritten: "the railgun charges with the hull held at x0", "then its railgun's whole", the ability sweep's railgun row, "a 100 blow on a HARDENED battleship" |

## Handover: the first agent stopped here (context cap), at a job boundary

Nothing is in flight: J1 and J2 have POSTs, the tree is clean at the J2 commit. The next agent starts
J3 with a PRE entry. Map of what J3 touches (found by J2's greps; line numbers at 38401be):

**J3 · F16, passive point defence** (v2: "PD is passive: 1 DPS per node"; D8 keeps per-hull numbers).
- Delete: `Ab.Pd` (Abilities.cs ~119-127) and `Ab.Pd` from six class rows (Ships.cs BB 179, CV 204,
  DD 230, FR 270, TE 306, BA 343); `Fit.AlwaysPd` (Ships.cs:42, the Warden's Fit 408); the rows
  `pd_active` / `pd_reload` (Stats.cs:178-179); PlayerShip `PdActive/PdReady/PdLeft/PdRechargeLeft/
  PdActiveFrac/PdRechargeFrac` (182-187), `StartPd` (~505), `Sl("pd").Left = 0` (~988);
  `PdOnline` becomes `Stats.Def.Has(Fit.Pd)`; `ITurretHost.PdRing` and every `PdRing => 0f`
  (Turrets.cs:74, 273-276 ring draw; Deployed/Emplacements/Hauler/Lanes) and `ClassArt.PdRing` +
  `Spec.Ring` if the ring then has no reader; `Stats.PdDuty` (234-242, note at 316) collapses to 1.
- Equipment.cs:367/369 `fr_endurance` / `fr_armour` move pd_active/pd_reload: restate on PD stats
  only (e.g. pd_damage/pd_range) so every hull's Wear count (SmokeTest ~3954) is unchanged.
- Harness rewrites (grep `Ab\.Pd|"pd"|PdActive|PdReady|PdRecharge|pd_active|pd_reload|AlwaysPd|
  PdRing|PdDuty`): 2568, 2886-2887 (key-capture example uses "pd" on Q: pick another BB ability),
  2942 and 3194 (ability id lists), 3125-3135 (the window/recharge block -> PD fires with nothing
  pressed), 3535/3541, 3954 (`Fit.Pd ? 9`), 5617 (warden), 5866/5872, 5943 (sweep row), 5993-5997,
  6137/6149, 6832/6848 (Lancer arena: "without point defence" must now kill or hold the PD another
  way, e.g. a pilot class with no Fit.Pd or the escorts out of PD reach), 7655, and the rung-5 pair
  8838 ("guest's PD activation reached the host") and 9172-9173 ("guest sees its PD window") ->
  a guest's PD fires with nothing pressed, host-decided.
- New check: from 3 varied spots, every PD hull's point defence takes a light raider in reach
  with no key pressed, at 1 DPS a mount (pd_damage / pd_interval literal 0.5 / 0.5).
- Rungs owed after J3: 3 on two seeds, and it adds to rung 5's first run (the guest PD check).

**Then slice 2** (§8): F4 + F17 + F18; F1 (+ Add, + Ramp; the hold half is in J2); F20 (2.58, with the
raids' EnemyDef rows Cc, Exp; MissileFlight onto the row). Specs: kits_v2.md §5 (F17 OutGuards
table, F18 deletes the echo arm in NoteDealt), kits_v3.md §5, kits_v31.md §6.
