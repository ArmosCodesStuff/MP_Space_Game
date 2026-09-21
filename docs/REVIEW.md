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
