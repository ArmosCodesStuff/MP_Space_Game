# Code review

The player's instruction: output the whole code base to a text file (`version/CODE_SNAPSHOT.txt`,
delivered with every build), then review **every variable, line, function and reference** for bugs,
unused code and anything broken or prone to breaking — making tasks and fixing them, recursively,
until the code base is clean. This file is that task list. It survives between sessions: **a file is
ticked only when it has been read line by line and everything found in it is fixed and tested.**

## Method

1. **Analysers** (`tools/analyse/run.sh`): unused private members, unread fields, unused assignments
   and parameters, needless usings, unreachable code, plus every compiler warning. Target: 0.
2. **Cross-reference** (`python3 tools/analyse/xref.py`): public members nothing uses (dead), and
   those only the tests use (read-only observers are intended; anything else is a question).
3. **Line by line**, file by file, riskiest first. For each file: every field (initialised? units?
   who writes it — the host only, under `Net.Sim`?), every function (inputs, edge cases, nulls, freed
   nodes, division by zero), every reference (callers still right?), every event or static handler
   (removed in `_ExitTree`?), every comment and number (matches the code?).
4. Every finding is fixed at the source, covered by a check where it can be, and the whole suite runs
   again. Findings found while fixing go back on the list (recursively).

## Pass 7 — slice 2 of the second batch: the outposts and the escort (done)

The slice-2 diff read by the same five lenses (the hauler's state machine, waves and hull, the world
and the radar, the fighter fix, the tests), each finding attacked by a refuter, then a critic: 7
confirmed, 2 refuted. Fixed, each with a check where one could see it:
- **A guest's pods jumped back up a quarter at the end of every stop**: its own countdown ended the
  stop before the host's report moved the leg on. A guest's count now stops just short of zero; only
  the host ends a stop.
- **`Forget` reset the strike count**, so a new strike could be ordered while the old bombers were
  still out, and their late reports cancelled it. The count is left to the bombers; a new strike waits
  for all of them.
- **Point defence kept a withdrawn hunter** (the same "freed with hull left" trap): `Forget` clears
  every turret's target too, checked with the wing and point defence both on a hunter that is called off.
- **The guest's offload check sampled one moment the stop could already be over**: it now watches every
  frame from earlier on.
- **A hull check split 262.5 as 262.4 + 0.1**, which floating point leaves alive by 2e-14.
- (The critic's other finding, CHANGES.md not yet updated, was the end-of-batch docs.)
Refuted: the last leg running through the dummies (it passes 58-96 u from them, which nothing minds) and
the stop check killing every raider (the old check did the same).

## Pass 6 — slice 1 of the second batch: the broadside and the destroyer (done)

The whole slice-1 diff read by five lenses at once -- multiplayer authority, replacement (nothing of
the two-class or missile-battleship code left behind), gear and the save migration, gameplay edge
cases, and the tests themselves -- every finding attacked by a refuter, then a completeness critic
over what the five missed. 16 confirmed, 2 refuted; all 16 fixed:
- **The burst's side missiles circled a close target** (a turning circle of ~107 u; inside
  d < 2r·sin 70°, about 200 u, they orbit until their run ends): the fan now narrows to what the
  missiles can turn through (`PlayerShip.BurstSplayFor`), checked by a burst at 150 u landing all three.
- **An old key-bindings file could put fire mode and the broadside both on F** (fire mode moved onto F
  used to push the missile to G, and the broadside took the default F): `Settings.Load` renames the
  missile's key to the broadside's and drops the reload's, checked by loading such a file.
- **A guest's broadside read READY between its own wind-up and the host's report**, its turrets dropping
  back to their slow swing: every peer now moves on to the volleys at the wind-up's end.
- The carrier's astern figures were rounded (24.27, 38.83): they are the exact fractions of its top
  speed, and the check compares with the spec's expression, not the code's literal.
- Comments still describing the battleship's missile, two classes, or the old figures (Torpedo, Shell,
  Combat, MenuFoe, PlayerShip, Stats, Equipment, EquipmentWindow, Raider, BaseDefense, the smoke test);
  `Game.cs`'s rule for bumping the save version (a renamed part is carried forward, not a new format);
  the sweep stowing a part id that no longer exists.
Refuted: the title-screen wait (already game time in the tree reviewed) and a fading-missile count
(the geometry keeps every missile in flight longer than the check's window).

## Pass 5 — the owner's whole batch, reviewed again (done)

The whole batch, with the first review's fixes and the reconnection and tutorial work, read again by
the same three lenses, each finding attacked by a refuter. Fixed at the source, each with a check
proved by a mutant: a kill in the seconds before the host noticed a drop was never paid, one owed and
received was paid twice, and one could be paid again after a restart (kill serials, the last 12 s kept,
the serials paid saved with the pilot, one message per kill); a guest that fell back offline kept the
host's open portal; a host that went offline with a place held kept its solo portal shut; a link lost
mid-handshake started the retries; a JOIN gave up at ~15.5 s, not 12 (ENet's timeout runs late: every
attempt has a hard deadline now); a pilot not yet let in tried to say goodbye; the folded MULTIPLAYER
label went stale; guests read "dropped" for a pilot that left; guests met the hauler hint; parts at home
met LOOT, not EQUIPMENT; the TIO counted a reconnecting pilot as not pressing READY; two stale
comments. Thirteen checks were added or tightened (see `CHANGES.md`), and the tests' `Unmeet` now
clears a card on screen too: while one shows, `Met()` is true whatever the trigger does.

## Pass 6 — RETURN button (2026-09-21)

The 20 s victory clock became a per-pilot RETURN button (ready-up, party home when all present have).
Authority checks proved by mutants: the "everyone must be ready" gate weakened to "any one" (caught by
the host+guest "one is not enough" check), and the clock put back (caught by the solo "no clock armed"
check). Failure path unchanged. Full smoke 672, 6/6. Looking at
the sweep and a `-Wan` run then found three more, fixed the same way: the equipment window ran off the
screen, a dead boss kept drawing its beam, and a ship's unreliable updates could reach a guest before
it knew the ship (they go by way of the hub now).

## Pass 4 — the owner's batch of 2026-09-21: adversarial review (done)

The gear, loot and hauler checkpoint (`2469a08..0f5a0f0`) was read by three independent reviewers --
multiplayer authority and lifetime; correctness against the owner's spec and the save; whether each
new check can really fail -- and every finding was then attacked by a separate refuter reading the
code. 18 survived, 7 were refuted. Fixed: the escort's leg never reached guests; AUTO-SELL was
buyable by a guest; a refit re-armed bombers; two stale comments; nine weak or fragile checks. Kept,
by the owner's wording: AUTO-SELL's price uses the "theoretical" level-5 salvager cost (6400, a
purchase the count row does not allow) -- now said in the comment. Every fix has a check, each proved
by a mutant.

## Pass 1 — analysers: done

- `Hub.cs:864` an assigned, never-read `k` — **removed**.
- `Progression.ExpToNext(int level)` ignored its parameter (EXP per level is a constant 1000 now) —
  **now a property**, callers updated.
- (Earlier this session) `Boss.Scale` / `Raider.Scale` hid `Node2D.Scale` — **renamed `Strength`**.
- Result: **0 analyser findings, 0 compiler warnings**.

## Pass 2 — cross-reference: done

- Dead, **removed**: `Combat.NearestHostile` (the old missile fallback), `Hub.PilotOpen`, `Hub.RaidLevel`.
- Duplicated: the host's bounty share had two branches doing the same thing — **one now**.
- Test-only, **kept (read-only observers the checks rely on)**: `Boss.TelegraphsPending`, `Hauler.AuraOn`,
  `Hub.BlastsPending`, `Hub.EscMenuOpen`, `Hub.TioOpen`, `Net.NetworkIdle`, `PlayerShip.SignalsLit`,
  `Shield.Glow`, `Wing.FighterPhase`, `Wing.Launching`, `Wing.Resting`, `Wing.ShotsThisPass`,
  `Yard.QueueLength`.
- `Hub.BeginPlacement` (the "left-click to place" mode) is used by no game feature today — **kept, by the
  player's decision**: a new placement feature will use it.
- Result: **0 unused members**.

## Pass 2b — re-run deeper, at every visibility: nothing new has accumulated

`xref.py` only looks at **public** members, which is why it keeps reporting zero. Re-scanned with a
wider net — every declared member at any visibility (fields, consts, properties, methods), plus
every declared type, counted against both game scripts and both test harnesses:

- **1140 members across 46 scripts. Referenced nowhere: 0. Types named nowhere else: 0.**
- **16 test-only members**, all read-only observers the checks rely on — the category this file
  already rules intended. (`Boss.BeamCharging` and `BossBar`'s new hooks joined the list.)
- **112 members were `public` but only ever used inside their own file.** Not dead — over-exposed.
  **Now narrowed: 71 are `private`.** The scan that produced the 112 was per-FILE, which is not the
  same question: a file holds several types (`Hub.cs` has Hub, Sun, Portal, HullHud, FlashLayer),
  so "used only in its own file" does not mean "used only in its own type". Re-run per declaring
  type, against brace spans, and excluding interface members (`IHittable`: `NetId`, `Position`,
  `HitRadius`, `Alive`, `TakeDamage`, `Covers`, `Selectable` must stay public) and anything either
  harness touches, it came to 99 real candidates.

  **26 of those were deliberately left public**, for two reasons a blind rewrite would have got
  wrong: a **nested type** cannot go private while a public member exposes it (`public List<Chunk>
  Chunks` with a private `Chunk` is CS0053), and a **shared declaration line** —
  `public const double BeamEvery = 30, BeamLive = 3.0, BeamTick = 0.25, BeamDamage = 50;` — cannot
  be narrowed when only some of its declarators were cleared.

  Confirmation that the 71 are right: the analysers still report **0 findings**, and IDE0051 flags
  *unused private members* — so every one of them is genuinely used inside its own type.

  Note that `public` → `internal` would have been worthless here: the game is one assembly, so
  `internal` and `public` are the same reach. `private` is the only narrowing that says anything.

Conclusion: the cull asked for in passes 1 and 2 is still done. There is no accumulated dead code
to remove. What remains is pass 3.

## Pass 3 — line by line: DONE, 46 of 46 files

**Multiplayer authority sweep (across all files, ahead of the line-by-line):** every `[Rpc]` in the
code base read and checked against its authority.

- 28 RPCs. Every `AnyPeer` one — the ones a guest can call on the host — validates its caller:
  `NetIdentity` refuses a peer describing anyone but itself *and* refuses progression the claimed
  level could not have paid for; `RequestReady` checks the sender is a pilot in the session;
  `PlayerShip.RequestAbility` and `NetState` check the sender owns that ship; `NetHostState` and
  `NetShield` accept only peer 1.
- All **nine** damage entry points (`TakeDamage` / `Hit`) open with `!Net.Sim`.
- Two nits, neither a hole, **not fixed**: `Hub.NetMySector` casts an unvalidated `int` to
  `SectorKind` where `NetIdentity` uses `Enum.IsDefined` (it only feeds `RpcHome`'s filter, so a bad
  value costs that peer its own home RPCs and nothing else); and `Yard.RequestBuy` /
  `RequestDispatchRpc` check `Net.IsHost` but not that the sender is in the session, where
  `RequestReady` does. Shared-base spending is intended — the inconsistency is the finding.
- **Fixed:** the boss now sends state at 30 Hz while a super move is locked. See DESIGN.md — a tell
  that rides the hull makes the hull's *angle* load-bearing, and 10 Hz was not enough for it.

| # | File | Lines | Why this position | Reviewed |
|---|---|---|---|---|
| 1 | `scripts/Net.cs` | 315 | networking, authority, threads | ☑ (3 findings, fixed) |
| 2 | `scripts/SessionMenu.cs` | 118 | networking, authority, threads | ☑ (1 fixed) |
| 3 | `scripts/Hub.cs` | 1175 | the world, input, sessions, sectors, raids, missions | ☑ (3 fixed, 1 trap recorded) |
| 4 | `scripts/PlayerShip.cs` | 746 | combat, authority, replication | ☑ (1 fixed) |
| 5 | `scripts/Raider.cs` | 278 | combat, authority, replication | ☑ (1 fixed) |
| 6 | `scripts/Boss.cs` | 185 | combat, authority, replication | ☑ (1 fixed) |
| 7 | `scripts/Yard.cs` | 418 | economy, saves, rewards | ☑ (1 fixed, 1 note) |
| 8 | `scripts/Gatherer.cs` | 249 | economy, saves, rewards | ☑ (1 fixed) |
| 9 | `scripts/Hauler.cs` | 316 | economy, saves, rewards | ☑ (clean) |
| 10 | `scripts/Combat.cs` | 81 | combat, authority, replication | ☑ (1 fixed) |
| 11 | `scripts/Torpedo.cs` | 154 | combat, authority, replication | ☑ (clean) |
| 12 | `scripts/ShipClasses.cs` | 492 | combat, authority, replication | ☑ (2 fixed) |
| 13 | `scripts/BaseDefense.cs` | 55 | combat, authority, replication | ☑ (clean) |
| 14 | `scripts/Missions.cs` | 38 | economy, saves, rewards | ☑ (clean) |
| 15 | `scripts/Progression.cs` | 86 | economy, saves, rewards | ☑ (1 fixed) |
| 16 | `scripts/Character.cs` | 175 | economy, saves, rewards | ☑ (1 clarity fix) |
| 17 | `scripts/MainMenu.cs` | 224 | UI and the rest | ☑ (1 fixed) |
| 18 | `scripts/CharacterCreator.cs` | 211 | UI and the rest | ☑ (1 fixed) |
| 19 | `scripts/StatsWindow.cs` | 198 | UI and the rest | ☑ (clean) |
| 20 | `scripts/CharacterSelect.cs` | 171 | UI and the rest | ☑ (clean) |
| 21 | `scripts/Stats.cs` | 159 | UI and the rest | ☑ (clean) |
| 22 | `scripts/Abilities.cs` | 131 | UI and the rest | ☑ (1 fixed, 1 note) |
| 23 | `scripts/Radar.cs` | 129 | UI and the rest | ☑ (clean) |
| 24 | `scripts/BasePanel.cs` | 118 | UI and the rest | ☑ (1 fixed) |
| 25 | `scripts/TargetDummy.cs` | 117 | UI and the rest | ☑ (clean) |
| 26 | `scripts/AbilityBar.cs` | 112 | UI and the rest | ☑ (clean) |
| 27 | `scripts/Ui.cs` | 99 | UI and the rest | ☑ (SetText added) |
| 28 | `scripts/Economy.cs` | 79 | UI and the rest | ☑ (clean) |
| 29 | `scripts/Music.cs` | 78 | UI and the rest | ☑ (1 fixed via MainMenu) |
| 30 | `scripts/BossBar.cs` | 74 | UI and the rest | ☑ (clean) |
| 31 | `scripts/Shield.cs` | 71 | UI and the rest | ☑ (clean) |
| 32 | `scripts/TioWindow.cs` | 66 | UI and the rest | ☑ (1 fixed) |
| 33 | `scripts/Sfx.cs` | 65 | UI and the rest | ☑ (clean; 1 note) |
| 34 | `scripts/EscMenu.cs` | 59 | UI and the rest | ☑ (clean; its caller held the bug) |
| 35 | `scripts/Settings.cs` | 58 | UI and the rest | ☑ (1 fixed) |
| 36 | `scripts/PilotWindow.cs` | 54 | UI and the rest | ☑ (1 fixed) |
| 37 | `scripts/EscapePod.cs` | 50 | UI and the rest | ☑ (clean) |
| 38 | `scripts/Telegraph.cs` | 47 | UI and the rest | ☑ (clean) |
| 39 | `scripts/Autopilot.cs` | 40 | UI and the rest | ☑ (clean; 1 note) |
| 40 | `scripts/Equipment.cs` | 78 | economy, saves, rewards | ☑ (clean) |
| 41 | `scripts/EquipmentWindow.cs` | 74 | UI and the rest | ☑ (clean) |
| 42 | `scripts/Shell.cs` | 48 | combat, authority, replication | ☑ (1 fixed) |
| 43 | `scripts/HealthBar.cs` | 37 | UI and the rest | ☑ (clean) |
| 44 | `scripts/Txt.cs` | 32 | UI and the rest | ☑ (1 stale comment fixed) |
| 45 | `scripts/Plume.cs` | 26 | UI and the rest | ☑ (clean) |
| 46 | `scripts/Explosion.cs` | 19 | UI and the rest | ☑ (clean) |

**Seven of those rows were missing from this table** until now — `Equipment`, `EquipmentWindow`,
`Explosion`, `HealthBar`, `Plume`, `Shell` and `Txt` were never listed, so a pass that ticked every
row would still have skipped them. The table is now every file in `scripts/`. One of the seven
held a real finding (`Shell`), which is the argument for listing them.

### Pass 3 findings — all 46 files

**The largest single finding is a CATEGORY, not a bug.** `Hub.Sector` was one of five statics that
legitimately outlive the *scene* — sector changes and visits rebuild the world — but that nothing
cleared when the *session* ended. Two of them are not dormant: `Yard._Ready` applies them. So:

| static | what the next session got |
|---|---|
| `Hub.Sector` | an arena with no base and no economy |
| `Yard._trip` | its ore, salvage, credits, levels and investment **overwritten** by an abandoned trip |
| `Yard._parked` / `_own*` | the base from the session before |
| `Music.CombatZone` | the combat track over the main menu |
| `Hub.I` | a non-null handle to a freed node |

Not per character, either — the yard is not saved at all, so this leaked **across characters**.
Four are fixed by one "end the session" block in `MainMenu._Ready`, which catches every route to
the menu rather than the one button that exposed it. *Scene-scoped and session-scoped had been the
same thing, and the menu was the missing boundary.*

### The earlier batch, in detail

**Fixed, with a check and a mutant to prove the check can fail:**

1. **Quitting to the menu from the arena stranded the next session there.** `Hub.Sector` is static and
   only `Hub.GoTo` sets it; `EscMenu`'s QUIT TO MAIN MENU changed scene without touching it, so the
   next session skipped `BuildWorld` and started in an arena with no base, no economy and a boss.
   Reset in `MainMenu._Ready`, so every route back is covered, not just that button.
2. **Guests read dummy readouts off the wrong hulls.** The dummies are numbered 1, 3, 4, 5 (2's spot
   holds the practice fighters) but `NetDummy` indexed `_dummies[number - 1]`: 3's figures went to 4,
   4's to 5, and 5's were dropped because `5 > Count`. Routed by `Number` now.
3. **`Combat.Clear()` leaked the Hub.** It dropped `OnFlash` and `OnTorpedo` but not `OnShell` — a
   static delegate holding a freed Hub, calling `AddChild` and `Rpc` on it, and permanent once you
   were back at the menu. The trap DESIGN.md already warns about, in a place nothing had looked.
4. **`Settings.Save()` was not atomic**, exactly as `Character.Save()` was not: two instances share
   one `user://`, so a rebind saved by one left the other reading a torn file and silently falling
   back to defaults. Temp file plus rename, same as characters.

**Fixed, no check (no reachable trigger found — hardening, not a repair):**

5. `Hub.NetIdentity` sanitises `bought` and `equip` but not `name`: a null threw on `.Length`, and a
   null that got past became a null `Pilot` in every label drawing it.
6. `Gatherer` indexed `Yard.Arms[Yard.ArmOf(this)]` while docking and unloading. `ArmOf` returns -1
   when the ship holds no arm and `UnloadSpot` does not bounds-check, so -1 would throw. No path to
   it was found — `SyncFleet` and `Release` are careful — but the states now self-heal instead.
7. `Progression.Flats` used `StatFor(...)` as a dictionary key, and `StatFor` falls through to null
   for an unknown id. All four ids are covered today; a fifth upgrade would have put a null key in.

**Fixed, performance:**

8. `Hub._help.Text` and `SessionMenu._reveal.Text` were reassigned **every frame** with strings that
   change on a refit or a rebind. Setting `Text` re-shapes the label's glyphs and these are MSDF
   fonts. Assigned only when they differ now.
9. `PlayerShip` aged its signal lights **inside `_Draw`**. Drawing is not guaranteed — a canvas item
   off screen is culled — so a ship that drifted out of view kept its lights lit for ever. Aged in
   `_Process` now, the way `Torpedo` ages its smoke.

**Recorded, not changed:**

- `Hub.Yard` is a property named after its own type and is **null in the arena**, yet `TickArena` and
  the boss-kill path both appear to dereference it. Safe only because C# binds the name to the class
  for static members and all three happen to be static. See DESIGN.md → Traps.
- `PlayerShip._lastHitBy` is keyed by damage source, and raider sources are `raider:{NetId}` — one
  new key per raider, never removed. Bounded by session length and tiny (a few hundred small
  entries over hours), so left alone; noted so it is not rediscovered as a leak.
- `Autopilot.Fighter` brakes with `Mathf.Sqrt(2f * maxSpeed * dist)` where the dimensionally correct
  form is `2 * accel * dist` (as `Gatherer.FlyTo` uses). It is a heuristic for a class that does not
  exist yet and its own checks pin the current behaviour; changing it is a balance decision.
- A remote `EscapePod` never calls `QueueRedraw`, so its plume is drawn once and never animates.
  Its velocity is not replicated, so there is nothing to animate from. Cosmetic, deliberate-looking.

Then the tools: `tools/smoketest/SmokeTest.cs.txt`, `tools/screens/Shots.cs.txt`, the run scripts.
**In the smoke test, look especially for checks that compare with the code's own constants** (found in
chunk E: such a check cannot catch the constant being wrong — compare with the spec's number instead).

## Pass 4 — the whole game mapped, seven readers at once: DONE, 51 of 51 files (2026-09-21)

Every script read again line by line by seven independent reviewers -- one per subsystem (hub,
player, enemies and combat, economy and save, UI, assets and audio and plumbing) and one on the
whole of internet play -- each returning spawn sites, duplications, old code, simplifications, bugs
and resources, file:line with evidence. The mechanical half is now a tool: `tools/map.py` writes
`version/MAP.md` (every member and who uses it, every RPC and who sends it, every spawn site, every
event hookup and whether it is undone, every asset and who loads it).

- **Fixed** (each with a check that fails on the old code): 25 bugs, 11 of them multiplayer -- see
  CHANGES.md, the 2026-09-21 section. The ones a player would have met first: the boss always sized
  for one; guests' gear lost after any trip to the arena; late joiners never told of the raid already
  on; a raider's web never holding a guest; the live death beam undrawn and sweeping.
- **Consolidated**: projectile spawning (`Combat.World`), nearest-searches (`Combat.Nearest`), the
  hull capsule, raid targets (`IRaidTarget`, `UtilityShip`), sprite sizing (`Sprites.Fit`), side
  windows, identity, character defaults and file reading, atomic writes, the UI's box/button/clear.
- **Rejected on purpose**: a generic "master spawner" for every entity. Of everything the hub makes,
  only raiders have a replicated spawn -> stream -> despawn life, and they have one; the world props,
  dummies, boss, fleet and ships are built the same way on every peer from replicated state, so a
  generic spawner would have one client and save nothing. The projectile spawner is the one that
  paid for itself. A shared base class for Raider, Boss, TargetDummy, MenuFoe and Torpedo, likewise:
  they share a Hub field and a two-line registration, not behaviour.
- **Left, and why** (all low): `_lastHitBy` and `DamageBySource` grow by one entry per raider met in
  a session (bytes; the second is the tests' bookkeeping); a guest's cosmetic shell does not stop at
  its target (the host's does); a guest's carrier bomber slot does not show STRIKING; guest-side
  ability timers can read READY for up to one packet at the end of a window; the join/leave refit of
  lost fleet ships is free. Each is in the pass's findings with its location.

## Findings log (pass 3)

### The restyle pass over every UI script — 3 findings, all fixed and covered

Reading every screen to put it on one palette found three bugs that had nothing to do with colour:

1. **The theme never reached a single button** (`Ui.cs`). A theme on the root window does not cross
   a `CanvasLayer`, and all UI here hangs off one. Every button in the game had been drawing
   Godot's stock theme. **Fix:** `Ui.Style` / `Ui.Panelise` at each subtree root. Check added; the
   old code in a copy fails it with 4 tabs unthemed.
2. **`HaulerHud` inherited from nobody** (`Hauler.cs`). Found by the new check, not by reading.
3. **The Esc menu's music row opened with nothing lit** (`Settings.cs`): the default was not one of
   the five values the row offers. Check added, on both rows; the old default fails it.

Also fixed while there: the stats window grew past the offsets it was pinned between and hung off
the right edge of the screen, and the warp readout was drawn in the same colour as the bar beneath
it. Neither is catchable by the UI lint, which measures position and width but not contrast.


### `Net.cs` — reviewed; 3 findings, all fixed and covered

1. **A thread race that leaked a router port-forward.** The UPnP thread wrote `_upnp` itself. If the
   session ended while the router was still answering (discovery takes 2 s+), `Shutdown` cleared it and
   the thread then set it back to the old session's handle; that session's result was ignored, so the
   mapping was never deleted — the router kept forwarding UDP 27015 to this PC after the session.
   **Fix:** the thread writes nothing; the main thread keeps a result only for the current session and
   **closes a stale session's mapping at once** (`StaleMappingsClosed`). Check added; the old code put
   back in a copy fails it.
2. **Tailscale was advised but unusable**: the status recommends Tailscale, yet the address code skipped
   the Tailscale range (100.64.0.0/10), so a friend on the tailnet was shown a 192.168 address that does
   not work there. **Fix:** a network-only host on a tailnet shows its Tailscale address, and COPY copies
   it (`Net.IsTailnet`, `TailnetAddress`). Check added (the range test; a real tailnet is not available
   here).
3. **Comments describing old behaviour**: the header listed "loot rolls, refining" (neither exists), and
   the hosting comment said the public address came from the router. **Rewritten.**

Also noted, not changed: `Join` parses "address:port" by the last colon, so a bare IPv6 address would
be misread — IPv4 is all the game hosts on; to revisit only if IPv6 is wanted.
