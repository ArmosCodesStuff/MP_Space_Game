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
- **Open — for the player:** `Hub.BeginPlacement` (the "left-click to place" mode) is used by no game
  feature any more, only by its test. Keep it dormant for later building, or remove the mechanism?
- Result: **0 unused members**.

## Pass 3 — line by line (to do, in this order)

| # | File | Lines | Why this position | Reviewed |
|---|---|---|---|---|
| 1 | `scripts/Net.cs` | 315 | networking, authority, threads | ☐ |
| 2 | `scripts/SessionMenu.cs` | 118 | networking, authority, threads | ☐ |
| 3 | `scripts/Hub.cs` | 1175 | the world, input, sessions, sectors, raids, missions | ☐ |
| 4 | `scripts/PlayerShip.cs` | 746 | combat, authority, replication | ☐ |
| 5 | `scripts/Raider.cs` | 278 | combat, authority, replication | ☐ |
| 6 | `scripts/Boss.cs` | 185 | combat, authority, replication | ☐ |
| 7 | `scripts/Yard.cs` | 418 | economy, saves, rewards | ☐ |
| 8 | `scripts/Gatherer.cs` | 249 | economy, saves, rewards | ☐ |
| 9 | `scripts/Hauler.cs` | 316 | economy, saves, rewards | ☐ |
| 10 | `scripts/Combat.cs` | 81 | combat, authority, replication | ☐ |
| 11 | `scripts/Torpedo.cs` | 154 | combat, authority, replication | ☐ |
| 12 | `scripts/ShipClasses.cs` | 492 | combat, authority, replication | ☐ |
| 13 | `scripts/BaseDefense.cs` | 55 | combat, authority, replication | ☐ |
| 14 | `scripts/Missions.cs` | 38 | economy, saves, rewards | ☐ |
| 15 | `scripts/Progression.cs` | 86 | economy, saves, rewards | ☐ |
| 16 | `scripts/Character.cs` | 175 | economy, saves, rewards | ☐ |
| 17 | `scripts/MainMenu.cs` | 224 | UI and the rest | ☐ |
| 18 | `scripts/CharacterCreator.cs` | 211 | UI and the rest | ☐ |
| 19 | `scripts/StatsWindow.cs` | 198 | UI and the rest | ☐ |
| 20 | `scripts/CharacterSelect.cs` | 171 | UI and the rest | ☐ |
| 21 | `scripts/Stats.cs` | 159 | UI and the rest | ☐ |
| 22 | `scripts/Abilities.cs` | 131 | UI and the rest | ☐ |
| 23 | `scripts/Radar.cs` | 129 | UI and the rest | ☐ |
| 24 | `scripts/BasePanel.cs` | 118 | UI and the rest | ☐ |
| 25 | `scripts/TargetDummy.cs` | 117 | UI and the rest | ☐ |
| 26 | `scripts/AbilityBar.cs` | 112 | UI and the rest | ☐ |
| 27 | `scripts/Ui.cs` | 99 | UI and the rest | ☐ |
| 28 | `scripts/Economy.cs` | 79 | UI and the rest | ☐ |
| 29 | `scripts/Music.cs` | 78 | UI and the rest | ☐ |
| 30 | `scripts/BossBar.cs` | 74 | UI and the rest | ☐ |
| 31 | `scripts/Shield.cs` | 71 | UI and the rest | ☐ |
| 32 | `scripts/TioWindow.cs` | 66 | UI and the rest | ☐ |
| 33 | `scripts/Sfx.cs` | 65 | UI and the rest | ☐ |
| 34 | `scripts/EscMenu.cs` | 59 | UI and the rest | ☐ |
| 35 | `scripts/Settings.cs` | 58 | UI and the rest | ☐ |
| 36 | `scripts/PilotWindow.cs` | 54 | UI and the rest | ☐ |
| 37 | `scripts/EscapePod.cs` | 50 | UI and the rest | ☐ |
| 38 | `scripts/Telegraph.cs` | 47 | UI and the rest | ☐ |
| 39 | `scripts/Autopilot.cs` | 40 | UI and the rest | ☐ |

Then the tools: `tools/smoketest/SmokeTest.cs.txt`, `tools/screens/Shots.cs.txt`, the run scripts.

## Findings log (pass 3)

(none yet)
