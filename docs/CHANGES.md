# Warships — change log

**This is a living document, not an archive.** It records what changed recently and what the build
can and cannot do *right now*. Old entries are deleted, not accumulated.

Read `DESIGN.md` for why the game is built the way it is. Read this for where it stands and what
just moved.

---

## The protocol (binding on whoever edits this project, human or AI)

**1. Every change lands here before the work is called done.** One line per change, under the right
heading. A change that is not written down did not happen.

**2. Say what it IS, not what was done to it.** "Carrier flies 8 fighters" not "rebalanced carrier".
Someone reading cold must learn the current state from the entry itself.

**3. Prune on every update.** This file has a budget: **one release section, plus Unreleased.**
When a release section is written, the previous one is **deleted**, not archived. Anything durable
from it belongs in `DESIGN.md` — move it there first, then delete it here.

**4. Delete entries that stopped being true.** If a later change reverses an earlier one, remove the
earlier entry rather than adding a note beside it. This file never contradicts itself.

**5. `Known broken` is mandatory and honest.** Anything that compiles but does not work belongs
there. An empty list is a claim, so only write it when it is true.

**6. A compile is not a verification.** `builds clean` and `works` are different claims. Never write
the second when only the first was tested.

**7. The Handoff section is rewritten, not appended.** It must always let a fresh session with no
history pick the work up from it alone. Update it in the same change as the code.

---

## Handoff — read this first

**State as of 2026-09-18: version 0.3.0 plus Unreleased (see it for everything since: docking arms
and the unload queue, the fleet and hauler pods, the idle economy, base menu, bunker-buster missile,
painted turrets, docking bombers and more).** Typecheck clean against the real `GodotSharp.dll`,
`dotnet build` clean (0 warnings), and the smoke test passes three runs in a row: 419 checks across
a single-player run and a host with two guests. Unlike earlier releases, this one was also **looked at**:
`tools/screens/run.sh` renders the select screen, the creator, a fresh spawn under the base, both
classes in the hub, the K window, a bomber strike, and turret close-ups on a virtual display; layout
faults have been fixed from those frames more than once.

**As of 2026-09-20 the whole harness also runs natively on the developer's Windows machine** (see
Unreleased → *The harnesses run on Windows*): typecheck 0 errors, build 0 warnings, analysers 0
findings, xref 0 unused, smoke **432 pass / 6 of 6 runs** (the 2 short of 434 assert the sandbox's
missing router and internet), sweep **67 frames, 0 lint** on the real GPU with the project's own
Forward+ renderer. So "how it looks on the developer's own GPU" is no longer unconfirmed for the
swept states. What remains unconfirmed is how it feels in a hand-played session — nothing here
replaces someone actually flying it.

**One real bug was found doing that, and is fixed:** `Character.Save()` wrote over the live `.cfg`
non-atomically, so a second instance sharing the same `user://` could read a half-written character
(`ConfigFile parse error … Unterminated string`) — about 1 run in 3 under WSL, and reachable from
the developer's own "Run Multiple Instances" multiplayer testing. It now writes a temp file and
renames it into place. See Unreleased → Fixed.

### What the game is right now

A Godot 4.7.2 C# game where your ship is your character. From the main menu you pick or create a
character (Terraria-style slots, each row showing the real ship in its colours), then sail it in a
persistent hub: a sun and asteroid belt above the base, a salvage wreck to the left, a wormhole to
the right, and **three target dummies** south-east of the base for testing weapons. Capital ships
**handle like naval ships** — no strafing, a turning radius. Two of nine planned classes are
flyable. Every class action is an **ability** on a bar along the bottom, **remappable in the K
window**. The **battleship** aims its main guns with the cursor (salvo or staggered), fires missiles
from a magazine it reloads with R, and has activated point defence. The **carrier** sends fighters
at a target, recalls them with R, launches bomber strikes of unguided torpedoes, and has activated
point defence. Peer-to-peer multiplayer works over localhost. There are no enemies, no wormhole
travel, and no economy beyond an idle ore/salvage counter.

### Controls (hub) — fixed keys

| Input | Does |
|---|---|
| W / S | ahead / astern (thrust along the keel only) |
| A / D | rudder. Turns on a radius; does nothing with no way on. No strafing. |
| Mouse | aims the battleship's main guns. The hull does not follow it. |
| **Left-click** | **select** the hostile under the cursor — or, while placing, **confirm**. Never attacks. |
| Right-click | cancel a placement. Nothing else. |
| **Tab** | select the hostile **nearest your ship**, always. No cycling. |
| K | abilities & keys (remapping) / stats window |
| Mouse wheel | zoom: 33% further out to 1.5× closer |
| Y | free camera: arrow keys or the screen edge move it (5000 u tether); Y again returns |
| L | pilot: level, EXP, points and upgrades |
| V | warp (every capital ship; 3 s warm-up, 30 s cooldown; aims at the jump) |
| Click the radar | select an enemy there, or make a landmark or pilot a waypoint |
| Left-click a building | its menu (TIO: WARP TO TARGET; base: BASE) |
| B | base menu: upgrades, and REFIT (the only way to change class, name or colours; costs 10%) |
| 1 – 6 | open hotkeys: bound and remappable, empty until abilities or items fill them |
| Esc | back one layer: text box → placement → creator → K window → base menu → target → **Esc menu** (radar size, music volume, quit) |

### Abilities — default keys, all remappable in K

| Battleship | | Carrier | |
|---|---|---|---|
| Space (hold) | main guns | Space | fighters: attack the target |
| V | salvo ↔ staggered | R | fighters: recall |
| F | missile (magazine 2) | F | bomber strike (unguided torpedoes) |
| R | reload missiles (16 s) | Q | point defence (15 s window, 15 s recharge) |
| Q | point defence (15 s window, 15 s recharge) | | |

### Where things are

- Scenes: `MainMenu.tscn` → `CharacterSelect.tscn` → `Hub.tscn`. `Net.cs` is an autoload.
- Game code is `scripts/` only. `DESIGN.md` has a one-line map of every file.
- Every combat and helm number: `scripts/Stats.cs`. Abilities and default keys:
  `scripts/Abilities.cs`. Sprites, lengths, turret textures, barrel lengths and mounts per class:
  `PlayerShip.Art`. The turrets are cut from the hull art (see DESIGN.md → Art for the method).
- Characters: `user://characters/<timestamp id>.cfg`. Key bindings: `user://settings.cfg`, section
  `keys`. On Windows, `user://` is `%APPDATA%\Godot\app_userdata\Warships\`.
- Input: `Hub._Input` (Esc, text-box focus), `Hub._UnhandledInput` (fixed keys, then ability
  lookup), `StatsWindow._Input` (key capture). The helm and the guns key are polled in
  `PlayerShip.LocalFlight`.

### How to verify a change

**Every harness now has a Windows PowerShell port**, so the project can be verified on the
developer's own machine with no WSL — see `docs/README.md` for the exact commands and
`DESIGN.md` for what differs. The Linux originals are unchanged and still the sandbox path.

1. **Typecheck:** `cd typecheck && sh typecheck.sh` (Windows: `typecheck\typecheck.ps1`) → must
   print `0 errors.` and exit 0. Needs the .NET 8 SDK and `typecheck/GodotSharp.dll` (gitignored;
   on Windows it is already in the NuGet cache at
   `C:\Users\<you>\.nuget\packages\godotsharp\4.7.2\lib\net8.0\`). Exits 2 without an SDK.
2. **Smoke test:** `sh tools/smoketest/run.sh <path to Godot_v4.7.2-stable_mono_linux.x86_64>`
   → must end `SMOKE TEST PASSED`. Real engine, headless, solo at a fixed 60 fps plus a host and a
   guest over localhost, in its own `user://`. Add checks to `tools/smoketest/SmokeTest.cs.txt`.
   Windows: `tools\smoketest\run.ps1 <godot win64 exe>`, and the bar there is **432 pass, 6/6 runs**,
   not `SMOKE TEST PASSED` — two checks assert the sandbox's lack of a router and internet and
   cannot pass on a real network. See DESIGN.md → Smoke test before touching them.
3. **Look at it:** `sh tools/screens/run.sh <same binary>` → frames in `/tmp/shots/`. Do this for
   anything visual; the smoke test cannot see. Windows: `tools\screens\run.ps1 <godot win64 exe>`
   → `%TEMP%\shots`, no virtual display needed, real GPU and the project's own renderer.
4. **Prove new checks can fail:** break the feature on purpose, watch the check fail, restore.
   Probed so far: C-key stacking, salvo/staggered equal rate, Tab-nearest, torpedo no-tracking.
   Run it **three times** before calling it done; one run hid a flaky check this release.
5. **Read build warnings.** Two in this release were real bugs (hidden Godot members).

**Sandbox setup** (a Linux container with no Godot). Install from the Ubuntu archive only — a stale
third-party apt source made plain `apt-get update` fail here:

    apt-get update -o Dir::Etc::sourcelist=/etc/apt/sources.list.d/ubuntu.sources -o Dir::Etc::sourceparts=-
    apt-get install -y dotnet-sdk-8.0 xvfb libgl1-mesa-dri libglx-mesa0 libgl1 libxcursor1 libxinerama1 libxrandr2 libxi6

and fetch the engine from
`https://github.com/godotengine/godot/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_linux_x86_64.zip`.
The GitHub releases *API* is rate-limited from sandboxes; the direct URL works. NuGet is not
reachable, which is fine: both harnesses build from the `nupkgs` folder that ships with the engine.

### Working with the developer

- They run the game on **Windows** in the Godot editor, and paste build errors and screenshots back.
- Hand back **individual files** for a small change and the zip for a large one, listing every
  changed and new file either way. `GodotSharp.dll` never goes in the zip.
- There is no git remote. "Push" means: tested, logged here, and handed back.
- **Standing rules from the developer:** left-click is only ever select or confirm-placement, never
  an attack; Tab always takes the nearest enemy; no class hotkeys; capital ships handle like naval
  ships (no strafing, a turning radius) and are slow; abilities live on a remappable bar, with six
  open hotkeys after every class's own; bombers dock half to port, half to starboard; test before
  handing back, and say what was tested versus only compiled.
- **Before editing, diff the workspace against the last handoff zip.** Once, seven files had
  changed after a handoff with no record of who changed them (it proved to be an unfinished earlier
  pass at the same request). It was reviewed, kept, finished and tested, not overwritten.
- When a decision could go two ways and the wrong one is expensive, ask before building.

### Open questions — built as a guess, awaiting confirmation

- **"Range of standard fighters +100%"** was applied to their **control range** (700 → 1400: how far
  from the carrier they will fight), not their weapon range (300), because the bomber rule speaks of
  the fighters' *max* range. Weapon range is one number in `Stats.cs` if that was meant.
- **Turret colour.** The cut-out turrets take the hull colour, as painted. Accent-coloured turrets
  are a one-line change in `Turret.Recolor`.
- **Which sprite is which.** The developer called image 4 "the red one"; it has no red pixels. It
  was used as the bomber (the edits described fit it), and the large grey ship as both carrier and
  fighter, as literally asked. Swapping is one path in `Wing.Init` / `PlayerShip.Art`.
- **The carrier has 3 PD turrets** (was 2), one per painted dome. +0.5 PD DPS while active.
- **"Battleship turrets rotate slowly"** was applied to its PD (τ/3); the main guns were already
  slow (τ/4) and are unchanged.
- **The ship cannot turn when dead in the water.** Pure turning-radius physics; no pivoting.
- **The missile rate** kept its old long-run value: magazine 2, 16 s reload.
- **Dummy meters reset themselves** on the first hit after 5 s idle (R was needed for abilities).
- Still open from 0.2.0: fighters never engage on their own; Tab's "nearest" is measured from the
  ship; left-click on empty space keeps the target; Esc from the hub ends a hosted session unasked.

### Next up

- **The first non-instant ability.** `Hub.BeginPlacement(label, onConfirm)` is ready: left-click
  confirms at the cursor, right-click or Esc cancels, a ring marks the spot. Nothing calls it yet.
- **Enemies that fight back:** populate `Combat.Hostiles` with ships that damage players. Player
  hull, death, and wing losses are coded but have never run.
- **Wormhole transit** into an instanced hostile system.
- **Loot, refining into trade goods, and hauler runs** — the economy loop in `DESIGN.md`.
- **A world save.** Ore and salvage reset every launch.
- **Something that grants stat bonuses** (skills or upgrades). The save and the K window carry them.
- **Balance:** the battleship out-damages the carrier about 2.4× on paper; levers are in `DESIGN.md`.

---

## Unreleased

### Review pass 3 complete: all 46 scripts read line by line

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**432 pass, 6/6 runs, three in a row**; sweep 67 frames, 0 lint. Six new checks, each with a mutant
that makes it fail.

**Seven scripts were missing from `REVIEW.md`'s own list** — `Equipment`, `EquipmentWindow`,
`Explosion`, `HealthBar`, `Plume`, `Shell` and `Txt` — so a pass that ticked every row would still
have skipped them. They are listed now, and one of them (`Shell`) held a real finding.

#### Fixed

- **Quitting to the main menu from the arena left the next session in the arena.** `Hub.Sector` is
  static and only `GoTo` ever set it, so the Esc menu's QUIT TO MAIN MENU carried it out of the
  session: the next run skipped `BuildWorld` and started in an arena with **no base, no economy and
  a boss**. The menu now ends the trip, whichever route reached it.
- **Guests read the dummies' DPS off the wrong hulls.** The dummies are numbered 1, 3, 4 and 5 (2's
  spot holds the two practice fighters), but the readout was routed by list position: 3's figures
  landed on 4, 4's on 5, and **5's were dropped silently**. Routed by number now.
- **`Combat.Clear()` kept a freed Hub alive.** It dropped `OnFlash` and `OnTorpedo` but not
  `OnShell`, so a static delegate went on holding the old Hub — calling `AddChild` and `Rpc` on a
  dead node, and never collectable once you were back at the menu.
- **`Settings.Save()` could be read half-written**, exactly as `Character.Save()` could: two
  instances share one `user://`, so a rebind saved by one left the other with a parse error and it
  silently fell back to defaults for the session. Temp file plus rename, same as characters.
- **Hardening, no reachable trigger found:** `Hub.NetIdentity` now sanitises `name` (it already
  sanitised `bought` and `equip`; a null threw on `.Length`); `Gatherer`'s docking and unloading
  states self-heal instead of indexing `Yard.Arms[-1]`; `Progression.Flats` skips a null stat key.
- **Two per-frame text re-shapes removed.** `Hub`'s controls hint and `SessionMenu`'s reveal button
  reassigned `Text` sixty times a second with strings that change on a refit or a rebind. Setting
  `Text` re-shapes the glyphs, and these are MSDF fonts.
- **`PlayerShip` aged its signal lights inside `_Draw`.** Drawing is not guaranteed — an off-screen
  canvas item is culled — so a ship that drifted out of view kept its lights lit for ever. Aged in
  `_Process` now, the way `Torpedo` has always aged its smoke.
- **The boss broadcast its state to peers who were not in the arena.** Godot addresses RPCs by node
  path, so a guest still building the arena got packets for a `Hub/Boss` it did not have:
  `Node not found`, `Invalid packet received`, and it missed the state anyway. This is the failure
  `RpcHome` was written for, on the other side of the same door. Both now go through the new
  `Hub.RpcToSector`. Surfaced by raising the boss to 30 Hz while locked, which made the window
  three times easier to hit.
- **Nine per-frame `.Text` assignments across six files**, consolidated behind `Ui.SetText` rather
  than four copies of the same guard. `BasePanel` alone rewrote about thirteen a frame.
- **Two hot-path allocations removed.** `Raider` ran `Where`+`OrderBy`+`ToList`+`IndexOf` over every
  raider, per light, per frame, to find its post slot; `Turret.Acquire` ran `Combat.Near` plus a
  LINQ chain plus a `ToList` **every frame for every PD turret** whenever nothing was in range,
  because a null target never satisfies the re-acquire guard. Both are single allocation-free
  passes now, with identical selection rules.
- **`ShipPreview` called `GD.Load` inside `_Draw`** — seven ResourceLoader lookups a frame for a
  live battleship preview. Cached.

Full detail, including what was found and deliberately *not* changed, is in `REVIEW.md`.

### The death beam: escorts first, and an honest tell

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**426 pass, 6/6 runs, three runs in a row**; sweep 67 frames, 0 lint; the bar and the beam looked
at. Six mutants, one at a time — every new check was
made to fail on purpose, and one of them **failed to fail** and had to be rewritten (below).

#### Added

- **The boss holds still while a super move winds up.** It used to keep closing at 30 u/s and
  turning through the whole 6 s beam charge while the red line stayed pinned to the spot it drew it
  from — the tell drifted off the hull. It now locks position for the beam charge *and* the ram
  wind-up.
- **The tell rides the hull.** Rather than freezing a world-space line, the telegraph is now a
  **child of the boss** in its local frame, so the line drawn and the line fired are the same thing
  by construction. Guests get it for free: they already lerp the boss's rotation, so no per-frame
  line updates cross the wire.
- **It tracks while it charges, at its own ponderous 0.3 rad/s** — so a pilot who is *not* webbed
  can still out-angle the beam before it fires.
- **The beam opens with its escorts, not with a red line.** Two lights launch 45° to port and
  starboard, **shiver on the spot** while their noses come round onto the pilot, then break into a
  boost with a **plume three times over** and flank the pilot **port and starboard** (they take
  fixed sides now instead of raid slots, so the pair always straddles it).
- **The charge is timed off a _predicted_ web**, not off the escorts actually arriving: the shiver
  plus the run-in at boost speed, then one second. So shooting the escorts down never cancels the
  beam — it means facing the beam free to move, which is the reward for beating the mechanic.
- **A skinny bar under the boss's hull bar** fills towards its next super move (the ram, or the
  beam's escorts going out — 15 s apart) and warms towards white as it tops out.
- **The beam reaches 10000 u**, indicator and damage alike (was 1800). Once it is charging,
  **distance is no escape — only angle is**, which is what makes out-turning it the counterplay and
  the web the trap. **Activation is unchanged**: the same 30 s rhythm, the same nearest-pilot
  target, the same predicted-web trigger. This is reach, not trigger.

#### Changed

- The boss sends its state at **30 Hz while locked**, 10 Hz otherwise, **and guests stop smoothing
  it while it is locked** — they take the host's position and angle flat. With the tell riding the
  hull the hull's *angle* became load-bearing, and smoothing is no longer cosmetic: the lerp lags
  about 0.1 s, which at 0.3 rad/s is ~1.7°, some **300 u at the beam's 10000 u reach** against a
  beam 70 u wide. A guest would have watched the hit land far outside the line it was shown. The
  boss is standing still and turning slowly while locked, so there is nothing to smooth away.
  Found while reviewing the multiplayer code, not by a test.
- The rhythm check now times the beam from its **commit** (escorts launching) rather than its red
  line. The rhythm itself is unchanged; the line is simply later now, and timing off it measured
  the new delay instead of the beat.

#### A check that could not fail

The first version of "the boss is locked in place" asserted zero drift — and a mutant that ignored
the lock **entirely** still passed it, because the boss sits inside its own 650 u standoff during
that part of the run and would not have closed anyway. The check was really only reading the
`Locked` flag. It now pushes the boss out past 650 u first, where it wants to close at 30 u/s, and
puts it back afterwards — left out there, its tridents take so long to arrive that the
point-defence check below times out. Re-run against the same mutant it now fails, **drifting
22.500 u**: 30 u/s across the 0.75 s window, to the unit.
*This is the constant-comparison lesson again in a new costume: a check must be able to observe the
behaviour, not just the switch that is supposed to cause it.*

#### Mutants, one at a time — all five caught

| Mutant | Check it had to break |
|---|---|
| `Locked` always false | the boss is locked in place |
| the beam telegraph back in world space | the tell rides the hull |
| `EscortShiver = 0` | the escorts shiver before the run in |
| the super bar reading the far timer | the bar resets as the beam commits |
| movement ignoring `Locked`, flag intact | the boss is locked in place *(missed at first — see above)* |
| the old 1800 u beam reach | beam and indicator both reach 10000 u |

### The harnesses run on Windows

**Checked:** every harness run on Windows against the numbers the sandbox produces — typecheck
**0 errors** (real `GodotSharp.dll`), `dotnet build` **0 warnings 0 errors**, analysers **0 findings**,
cross-reference **UNUSED ANYWHERE: 0**, smoke test **three runs in a row, 419 pass and 6/6 runs
finished each time** (identical every run — no flaky check), sweep **67 frames, SWEEP DONE, 0 LINT**.
Frames looked at. No game code changed.

#### Added

- **PowerShell ports of all four shell harnesses**, so the project can be verified on Windows with no
  WSL: `typecheck\typecheck.ps1`, `tools\analyse\run.ps1`, `tools\smoketest\run.ps1`,
  `tools\screens\run.ps1`. `tools/analyse/xref.py` already ran under Windows `python` unchanged.
  The originals are untouched and remain the Linux path.
- **A WSL Ubuntu 24.04 distro** provisioned to match the sandbox (dotnet-sdk-8.0 from the archive at
  `/usr/lib/dotnet`, Xvfb, Mesa, and the Linux mono engine at `/opt/godot`), so the original `.sh`
  scripts run unmodified. Verified there: typecheck `0 errors.`, xref `UNUSED ANYWHERE: 0`, smoke
  **419 pass, 6/6 runs**. Note **24.04, not the default**: WSL now installs Ubuntu 26.04, which ships
  no .NET 8 at all — only `dotnet-sdk-10.0` — and the project targets `net8.0`.
- The runners take the plain `Godot_v4.7.2-stable_mono_win64.exe` and **swap themselves to the
  `_console.exe`** beside it, refusing to run if it is absent: the GUI binary writes nothing to
  stdout, so a headless run's every check would be invisible.
- The Windows analyser **builds in place instead of reusing the smoke test's offline build folder** —
  NuGet is reachable on Windows, so the "run the smoke test first" prerequisite does not apply. It
  refuses to run if a `.editorconfig` already exists at the root, and always removes its own.
- The Windows sweep **needs no virtual display** and renders on the real GPU with the project's own
  Forward+ renderer. `-Compat` forces the Linux `opengl3` / `gl_compatibility` path for comparison.

#### Known broken

- **Two of the 434 smoke checks cannot pass on any machine with a router and internet** — `no UPnP
  router…` and `no router or internet here…`. Both hard-require `Net.Reach.LanOnly`, i.e. the
  sandbox's absence of a router and of internet; on a real network reachability resolves otherwise
  and they fail by construction. They are not regressions and the behaviour they cover is real.
  **432 is the bar on Windows *and* under WSL**; only the sandbox itself reaches 434. Making them
  branch on the environment is not done.
*(The non-atomic character save that was listed here is now fixed — see Fixed, below.)*

#### Fixed

- **A character save can no longer be read half-written.** `Character.Save()` wrote straight over
  the live `.cfg` with `c.Save(PathOf(Id))`; `ConfigFile.Save` truncates and rewrites in place, so
  anything reading that path at that instant got
  `ERROR: ConfigFile parse error at user://characters/<id>.cfg:34: Unterminated string` — `Load`
  returning false, or the character silently dropping out of the select screen's list. It now saves
  to `<id>.cfg.tmp` and renames that over the real file, so the live path only ever holds a complete
  character. The temp name deliberately does not end in `.cfg`, so a leftover is never listed as a
  character; if the rename fails it falls back to the direct save rather than lose the character.
  Found by chasing an intermittent smoke failure (1 run in 3 under WSL: arena host and arena guest
  hit the same file at the same line at the same moment). **Reachable outside the test** — the
  developer's own way of testing multiplayer, *"Run two instances (Debug → Run Multiple
  Instances)"*, shares one `user://` exactly as the six smoke peers do.
  **Two checks added** (smoke is now **419 on Windows, 421 in a sandbox**): a stray `.tmp` is never
  listed as a character, and `Save()` consumes the temp file rather than leaving one behind. The
  second was **proved able to fail**: a mutant restoring the exact old `c.Save(PathOf(Id));` line in
  a scratch copy fails it and passes with the fix. The first guards `List()`'s `.cfg` filter and
  passes either way — it is a guard, not a regression detector for this change.

#### Changed

- `project.godot` **adopted as the Godot editor re-saves it**. The editor drops every setting equal to
  an engine default and keeps no comments, so seven lines and two comments went on the first Windows
  open. All seven confirmed equal to the 4.7.2 defaults by asking the engine, so behaviour is
  unchanged; the two comments' reasoning now lives in `DESIGN.md`, where the editor cannot delete it.
- `.gitignore` now covers `*.import` and `*.uid` — Godot import output, regenerated on the first open
  after extracting. Checked first that no `.tscn` references a generated `uid://`.
- **`.gitattributes` pins `* -text`**, and `core.autocrlf` is `false` for this repo. Git for Windows
  sets `core.autocrlf=true` system-wide, so the next checkout would have rewritten every text file to
  CRLF: every hash in `MANIFEST.sha256` would change (the integrity check would report ~100 false
  failures), `CODE_SNAPSHOT.txt` would break the same way, and every `tools/**/*.sh` would become
  `bad interpreter: /bin/sh^M` in WSL. Caught before any checkout, so nothing was damaged.
- `version/MANIFEST.sha256` regenerated: **124 entries, all verifying** (118 before, plus `CLAUDE.md`,
  `.gitattributes` and the four `.ps1` ports).
- `version/CODE_SNAPSHOT.txt` regenerated: **64 files, 10936 lines** (was 59 / 10363). It had been
  left stale for one commit on the reasoning that no *game* code had moved — but `project.godot` is
  in the snapshot and had been adopted from the editor, so it recorded 43 lines / `076b8ff5db99`
  against an actual 42 / `aaa06503a405`. Now added: the four `.ps1` ports, beside the `.sh` files it
  already carried, and **`typecheck/GodotStub.cs`**, which had been missing for the project's whole
  history despite being tracked code and the typecheck's fallback without `GodotSharp.dll`.
  **Verified by splitting it back** exactly as its own header describes and comparing all 64 against
  disk: every one identical, and no tracked code file is missing from it.
- The snapshot's file list is now **derived** (root files in their fixed order, then `scripts/`,
  `typecheck/`, `tools/*` each sorted ordinal) rather than inherited from the previous snapshot.
  Inheriting is how `GodotStub.cs` stayed missing: a file absent from the list was absent from the
  next list, forever.

### Earlier in Unreleased

### Chunk G: the equipment menu

**Checked:** smoke test **three runs in a row, 419 checks each, 0 compiler warnings, 0 analyser
findings, 0 unused members**; screenshot sweep **67 frames, complete, 0 lint**; the window looked at.
Mutants caught: the loadout aliased (a chip off changed nothing); a host ignoring a guest's gear (caught
only once the test guest flew a non-default loadout — with the default, a broken host looked identical).

#### Added

- **EQUIPMENT (I, or its button beside PILOT)**: the gear on the pilot's **current** ship — **Weapon,
  Engines, Shield (the hull points), Hull (the frame, and its point defence), Utility** — and **five
  chips**. Locked to the class: **each class keeps its own gear**, saved with the character, so what is
  left on a ship is still there when the pilot switches back. Parts change no looks, only numbers.
- **Common defaults**: battleship — Mk I Main Battery, Standard Drive Cluster, Basic Deflector Array,
  Reinforced Frame, Missile Rack; carrier — **Mk I Fighter Hangars** (weapon) and a Bomber Bay (utility).
  They reproduce the ship's own numbers. **Five Basic Combat Chips, each +5% damage and +5% hull**: **+25%
  in all** (battleship 375 hull, 7.375 a shell; carrier 250 hull, fighters 2.5, torpedoes 18.75).
- **Chips come off and go back on** (a spare list); the core parts stay put until the inventory gives
  them something to swap with.
- **The host applies each pilot's gear** (it resolves damage and hull): the loadout travels with the
  identity and is sanitised on arrival (known items, the right slots, at most five chips).

#### Fixed (found while building)

- The ship kept a reference to the saved loadout, so a chip taken off in the window changed nothing — it
  keeps a copy now. `Hub.EquipmentOpen` (never used) removed.

#### Changed (tests)

- Checks with literal numbers now state them with the chips (a shell 7.375, a missile 6.25, the K window
  29.50 DPS, the carrier reference 19.07 × 1.25 = 23.84). **Pilot points are added before percentage
  bonuses** (as designed), so a Hull point shows +6.25 with the chips — a question for the player.

### Earlier in Unreleased

### Chunk F: hull upgrades for miners and salvagers

**Checked:** smoke test **three runs in a row, 410 checks each, 0 compiler warnings, 0 analyser
findings, 0 unused members**; screenshot sweep **66 frames, complete, 0 lint**; the new row looked at in
the SALVAGERS tab. A 100 cr mutant (the ordinary price) fails the pricing check.

#### Added

- **Miner hull** (MINERS tab) and **Salvager hull** (SALVAGERS tab): **+10% hull a level** (120 → 132 →
  …), **125 cr to start — 25% above the other +10% rows — and ×1.25 a level** (156, 195, …). Buying a level
  gives the ships of that kind their new hull at once (a full ship stays full), rebuilds come back at the
  upgraded hull, and the spend counts toward the category's investment (and so its rebuild price).

### Earlier in Unreleased

### Chunk E: the heavy fighters

**Checked:** smoke test **three runs in a row, 407 checks each, 0 compiler warnings, 0 analyser
findings, 0 unused members**; screenshot sweep **66 frames, complete, 0 lint**; the new heavy looked at in
the game (waiting at the map's edge, facing the ship, its missile flying at the red circle). Mutants —
each alone: waiting near the target; a 300% boost; no jump filter; blind aiming — all caught.

#### Changed

- **The heavy's look: a snub-nosed gunship** (the player's option b) — short and wide, a blunt wedge prow
  welded to the hull, angular cheek plates, twin swept tail fins; its one turret on the join. The old
  art is in `art_unused/enemy_heavy_hull_v1.png`.
- **Heavies wait at the map's edge** (3200 u from the base) at the point nearest their target, facing
  it. **Once the target is pinned they boost at 700% until 300 u away**, then close at cruise to their
  post (135 u off the hull, astern) and fire 2 DPS. The missile still fires within 500 u. (The old
  "450 u astern" wait and the 300 u/s afterburner are gone.)
- **The missile never aims at nonsense**: its lead (the target's velocity × 7 s) ignores a JUMP — a warp
  moved a ship thousands of units in one frame, and the missile aimed miles off — and, at the player's
  request, **anything out of place (a speed over 400 u/s, a broken number, a lead over 2800 u) aims it
  at where the target is now**.

#### Fixed (tests)

- **A check that could not fail**: the boost check compared the speed with the code's own constant, so a
  300% mutant passed it. The heavy's checks now compare with the spec's literals (700 u/s, 300 u,
  3200 u). Older checks may share this flaw — it is on the review list.

### Earlier in Unreleased

### Chunk D: turning in place

**Checked:** smoke test **three runs in a row, 404 checks each, 0 compiler warnings, 0 analyser
findings, 0 unused members**; screenshot sweep complete, 0 lint (no visual change). A 20°/s pivot, put in
on purpose in a copy, fails both pivot checks.

#### Changed

- **Capital ships pivot in place** at or below **5% of top speed**: the rudder turns the hull on the spot
  at **10°/s** (easing in, like the rudder), and **nothing moves it sideways** — no strafing. Above 5% the
  naval turning circle is unchanged; a pinned ship still cannot turn at all. The autopilot uses the same
  helm, so it pivots too.
- (Noted for the player: just above 5% the turning circle is slower than the pivot — about 3°/s at 6% —
  as specified; the two could be blended across 5–10% if that ever feels odd.)

#### Changed (tests)

- "Dead in the water: A/D neither pivot nor strafe" became "A/D pivot slowly and never strafe".

### Earlier in Unreleased

### Chunk C: balance, and the battleship's shells

**Checked:** smoke test **three runs in a row, 402 checks each, 0 compiler warnings, 0 analyser
findings, 0 unused members**; screenshot sweep **66 frames, complete, 0 lint**; shells in flight looked
at. Mutants (shells at 2x, the old 1.5 gun damage, the old 1400 range) all caught.

#### Changed

- **The battleship's main guns fire shells**: straight — no tracking — at **4× its missile's speed
  (520 u/s)**, as far as the guns reach (720 u); the first hostile a shell touches takes the hit (never a
  missile — that is point defence's work). A cannon's report instead of the laser buzz. Point defence
  stays instant.
- **Balance, from a measurement**: the carrier's sustained output (fighters and bomber strikes on a
  dummy from 520 u, bombers re-sent as they re-arm) **measured 19.07 DPS over 90 s**. The battleship's
  total (main guns + missiles; point defence excluded — it cannot hit bosses) is set to 1.25× that:
  **5.9 a shell** (was 1.5), **23.9 DPS in all**. The carrier's **control range is 1080 u** — 1.5× the
  battleship's 720 u guns — and its bomber strike range stays twice that (2160 u).
- A **drift guard** re-measures the carrier over 40 s every run (19.20 this time): if later changes move
  it more than 25% from the reference, the suite says so.

#### Changed (tests)

- The salvo/staggered DPS checks wait for shells still in flight before reading the dummy, and compare
  with the sheet's own figure (staggered: exactly 472.0 in 20 s).

### Earlier in Unreleased

### Chunk B: the beam's escorts, the boss repainted

**Checked:** smoke test **three runs in a row, 397 checks each, 0 compiler warnings, 0 analyser
findings, 0 unused members**; screenshot sweep **65 frames, complete, 0 lint**; the repainted boss and
the arena mid-charge (the escorts pinning the carrier) looked at. Mutants (escorts straight ahead; a 3 s
charge) caught.

#### Changed

- **The death beam charges for 6 s** (was 2), then lives 3 s (0.25 s ticks, 50 a tick — chunk A).
- **As it charges, the boss launches two light ESCORTS at its target** — one **45° to port**, one **45°
  to starboard** of its nose — their boost lasting until they reach their posts. They pin the pilot
  inside the beam **unless point defence kills them first**: they are fragile on purpose, **3 hull**
  (× the boss's level), so one PD turret (1 DPS) kills one inside the charge. (A default: at 25, point
  defence could not have saved anyone.)
- **The boss is raider red with a white skull on its centre** (`boss_raider.png`, baked so the skull
  stays white; the old art is in `art_unused/`).

#### Fixed

- `BackingFor` — added in chunk A and never used — **removed** (found by the cross-reference scan).

### Earlier in Unreleased

### Chunk A of the new batch: the bomber docking fix, numbers, point defence, practice fighters

**Checked:** smoke test **three runs in a row, 394 checks each, 0 compiler warnings, 0 analyser
findings**; screenshot sweep **65 frames, complete, 0 lint**; the practice fighters looked at.
Proven by mutants: **the exact old docking code fails the new full-speed check**; point defence without
its filter fails; a heavy on 0.5 s shots fails. **Not yet looked at:** the new red tips on the bomber,
base and boss missiles.

#### Fixed

- **Bombers never docked when the carrier was fast.** Backing in, a bomber moved to its slot and *then*
  added the carrier's velocity — overshooting by one frame's travel. From about 90 u/s that is more than
  the 1.5 u it needed, so it jittered beside its slot forever and never re-armed ("sometimes": only at
  speed). Now it rides the slot's true motion (moving and turning, measured frame to frame), then moves
  in; within 6 u it is home, and after 2.5 s backing it is home regardless.
- **Guests mid-transition got engine errors** ("Hub/Yard not found"): the base's messages now go only to
  guests who have reported they are home (they report whenever their world loads).

#### Changed

- **Boss 600 hull** at level 1 (level and party scaling on top).
- **Fighters 2 damage per shot** (was 0.175); **torpedoes 15** (was 3).
- **Miners and salvagers 120 hull** (was 60); the hauler stays 150.
- **Invulnerability to one ongoing source: 0.52 s** (was 0.35).
- **Point defence shoots only missiles and light fighters** — never heavies, bosses or dummies.
- **One dummy became two practice light fighters**, so point defence has something to train on.
- **Every missile has a small red triangle tip.**
- **The heavy's laser fires every 1 s for 2** (still 2 DPS): at 0.5 s, half its shots fell inside the
  new 0.52 s guard.
- **The death beam: live 3 s, a tick every 0.25 s, 50 a tick** (with the guard, a hit about every
  0.75 s — 5 to 7 hits). Its 6 s charge and the two fighters it launches come in chunk B.

### Earlier in Unreleased

### Code review — passes 1 and 2 done; pass 3 begun (`Net.cs` reviewed)

**Checked:** smoke test **three runs in a row, 391 checks each, 0 compiler warnings**; analysers
**0 findings**; cross-reference **0 unused members**; screenshot sweep **65 frames, complete, 0 lint**;
the race fix proven by putting the old code back in a copy (caught). The full task list and its
progress live in `docs/REVIEW.md`.

#### Added

- **`tools/analyse/`**: `run.sh` runs the .NET code analysers over the game scripts (unused members,
  unread fields, unused assignments and parameters, needless usings, unreachable code); `xref.py` lists
  public members nothing uses, or only the tests use.
- **`docs/REVIEW.md`**: the method, both automated passes' results, a findings log, and the
  line-by-line checklist of every script, riskiest first — ticked only when reviewed and fixed.

#### Fixed

- **A thread race leaked a router port-forward** (`Net.cs`): the UPnP thread stored its handle itself,
  so a session that ended while the router was answering left UDP 27015 forwarded to this PC. The
  thread now writes nothing; a stale session's mapping is closed at once.
- **Tailscale was advised but unusable**: a network-only host on a tailnet now shows and copies its
  Tailscale address (100.64.0.0/10) — the status already told friends to use Tailscale.
- **Dead or redundant code removed**: `Combat.NearestHostile`, `Hub.PilotOpen`, `Hub.RaidLevel`, an
  unused local, `ExpToNext`'s ignored parameter, a duplicated bounty branch.
- **Comments describing old behaviour** rewritten (`Net.cs`).

#### Open (for the player)

- `Hub.BeginPlacement` — the "left-click to place" mode — is used by no game feature, only its test.
  Keep it dormant for later, or remove it?

### Earlier in Unreleased

### Step 3: levels, per-pilot EXP, party scaling — adopted from an interrupted run

**Integrity findings.** An interrupted run (23:52–00:18) built step 3 and a sweep-completeness check,
committed as three checkpoints, never reported and never `VERIFIED:`. **Reviewed line by line against
the approved formula, tested, adopted.** Re-verified here: smoke test **three runs in a row, 389 checks
each, 0 compiler warnings**; my own mutants (1500 EXP a level, an unsplit bounty, an inverted kill ratio)
all caught; screenshot sweep **65 frames, 0 lint, complete**; the TIO's level row, the boss bar and the
PILOT window looked at.

#### Changed (adopted — the formula the player approved)

- **Boss levels start at 1**: S(L) = 1.1^(L−1) (level 5 = ×1.4641). Tiers are gone; the TIO selects
  "LEVEL L". Old saves carry over (tier t beaten → levels 1…t+1 cleared).
- **Party of P pilots**: boss hull × S(L)(1 + 0.6(P−1)), damage × S(L)(1 + 0.2(P−1)). Raids after a
  failed level-L mission × S(L), **2 patrols + 1 per extra pilot** (confirmed by the player).
- **EXP, per pilot, on its own machine**: the kill **round(200 × boss level ÷ pilot level)**, **+250 the
  first time that pilot clears that level** (a set of cleared levels per boss), **+100 for completing**.
  **Every level needs 1000 EXP.**
- **Credits**: **2000 × S(L) × (1 + 0.5(P−1)), split evenly among the P pilots** — the host's share to
  its base, each guest's to its own base (set aside while it visits). Solo earns the most.

#### Added (adopted)

- **The screenshot sweep must finish**: it ends with `SWEEP DONE`, and a sweep cut off part-way is
  reported as incomplete — it can never pass as clean again.

#### To tidy in the code review

- The host's bounty share goes to the same place by two branches (`Yard.TripCredits`); one will do.

### Earlier in Unreleased

### Chunk 8b: a dedicated two-player arena test

**Checked:** smoke test **three runs in a row, 383 checks each, 0 compiler warnings** (9 new, on a
two-player run of their own; undoing the stranded-guest fix in a copy was caught); screenshot sweep
**62 frames, 0 lint**.

#### Added (tests)

- **A two-player arena run** (`ahost` / `aguest`, its own port, after the three-player run): both
  pilots READY, both at the portal, **into the arena together**; the boss dies, **both home**; into the
  arena again, **the host drops** — and the guest **lands home, offline, with its own base** (a marker
  credit total set before it joined: 12345). The first network-level proof of the two arena fixes.

#### Fixed

- **`MissionPortalPos` crashed without the TIO's sprite** — in the arena, or in the frame a scene is
  being swapped (found by the new run: a guest pressing READY just after coming home). It is computed
  from constants now (the TIO is 260 u tall, its art 630 × 876 px).

### Earlier in Unreleased

### Raids (chunk 7) and the arena's BASE button (chunk 8a) — adopted from an interrupted run

**Integrity findings.** An interrupted run (22:46–23:03) built chunk 7 and chunk 8a and made its own
`VERIFIED:` commit (3647e12) for chunk 7; chunk 8a was committed after it, unverified. None of it had
been reported to the player. **Reviewed line by line, tested, adopted** — it is the approved plan.
**Re-verified here, independently:** smoke test **three runs in a row, 374 checks each, 0 compiler
warnings**; its raid checks proven by my own mutants (raids ignoring the level; a raid after a win —
both caught); screenshot sweep **62 frames, 0 lint**; the raid frame looked at (HUD "RAIDERS 8").

#### Added (adopted)

- **Raids**: a **failed mission** sends the boss's raiders to your base — **2 patrols, and 1 more per
  extra pilot** (a choice the run made; see Decided) — in from the **map's edge (3200 u)**, **3 s after
  the party is home** (every guest's world loads first). Raiders are as strong as the failed boss:
  hull, lasers and the heavies' missile × **S(L) = 1.1^(L−1)**. No raid after a win. The HUD counts
  the raiders.
- **In the arena there is no BASE button, and B opens nothing** (the menu would read a base that does
  not exist there).

#### Fixed

- **`Boss.Scale` and `Raider.Scale` hid Godot's own `Node2D.Scale`** (two compiler warnings; any code
  meaning the node's scale would have got the strength number). Both are **`Strength`** now; the build
  has **0 warnings**. From here on the warning count is checked with every run.

#### Decided (open to change)

- **Raid size: 2 patrols + 1 per extra pilot** (the interrupted run's choice, in the spirit of the
  party scaling).

### Earlier in Unreleased

### Chunk 7: raids

**Checked:** smoke test **three runs in a row, 372 checks each** (5 new; raids after a win, and unscaled
raiders, were each put in on purpose in a copy and caught); screenshot sweep, 0 lint; a raid closing and
the HUD's count looked at.

#### Added

- **A failed mission brings a raid.** When the whole party is in stasis and goes home, the boss sends
  its raiders after them: **2 patrols** (3 lights and a heavy each) **plus 1 per extra pilot**, in from
  the **edge of the base's map (3200 u)**, **3 s after the party is home** (so every guest's world has
  loaded first). A won mission brings none.
- **Raiders are as strong as the failed boss**: hull and every damage (lasers, the heavy's missile)
  × **S(L) = 1.1^(L−1)**, L the failed boss's level (today's tier + 1; the same scale exactly).
- **The HUD counts them** ("RAIDERS 8").

### Earlier in Unreleased

### Connecting with friends elsewhere

**Checked:** smoke test **three runs in a row, 367 checks each** (14 new, some on the host's run; a
double NAT taken for the internet, an address shown before the click, and no rate limit were each put
in on purpose in a copy and caught); screenshot sweep, 0 lint; the address hidden and revealed looked
at. **Not testable here:** a real router and the live public-address service (the sandbox has
neither — the host's run proves the "nothing learned" path end to end).

#### Changed

- **The address friends in other cities need.** Hosting now asks a public "what is my IP" service
  (`api.ipify.org`) as well as the router (UPnP), and decides from both:
  **Internet** (the router opened the port and is the internet's edge: friends join public:port);
  **Manual** (the router would not open it: forward UDP 27015 to this PC, then friends join
  public:port); **network only** (the router's outside address is private or differs from the public
  one — another router or the provider's shared address is in the way — or nothing could be learned).
  Before, a router without UPnP left only the local 192.168… address, useless from another city.
- **The address is behind a click-to-reveal** in the multiplayer panel ("•••.•••.•••.•••" until
  clicked), and **no status line prints it**. COPY ADDRESS copies it without revealing it.
- **Host, join and offline answer one press a second** (game time).
- **Single player is proven silent**: no socket, no web lookup, no router job (`Net.NetworkIdle`);
  lookups die with their session.

#### Decided (the player's answers)

- The connection fix went ahead of raids.
- Party size: boss hull +60% and boss damage +20% per extra pilot; the bounty +50% per extra pilot,
  **split evenly**, so a solo pilot earns the most.
- Boss kills: 200 EXP at equal level, proportionate to the level chosen; +250 the first time each
  level is beaten. The first boss is level 1.
- **Awaiting the player's approval before it is built**: the formula proposed in reply —
  S(L) = 1.1^(L−1); boss hull × S(L) × (1 + 0.6(P−1)), damage × S(L) × (1 + 0.2(P−1)); a failed level-L
  mission's raids × S(L); kill EXP = round(200 × boss level ÷ pilot level), +250 first clear of that
  level, +100 for completing; 1000 EXP per pilot level; bounty 2000 × S(L) × (1 + 0.5(P−1)) split
  evenly among P.

### Earlier in Unreleased

### Base weapons

**Checked:** smoke test **three runs in a row, 353 checks each** (5 new; a base that fires at anything
hostile, a 200 u/s missile and an 800 u laser were each put in on purpose in a copy and caught);
screenshot sweep **61 frames, 0 lint**; the turret and a base missile striking a raider looked at.

#### Added

- **The base's own weapons**, on a turret on the station's upper deck (ranges from the base's centre):
  a **5 DPS laser at 300 u** (2.5 every 0.5 s) and a **tracking missile every 5 s: 25 damage, 600 u,
  120 u/s** (1.2× the capital ships' average top speed). **Raiders only — never the practice dummies.**
  Host-simulated; guests see every shot, and their turret tracks locally.

### Earlier in Unreleased

### Chunk 6c: patrols

**Checked:** smoke test **three runs in a row, 348 checks each** (3 new; no detection limit, and a heavy
that never joins its lights, were each put in on purpose in a copy and caught); screenshot sweep
**60 frames, 0 lint** (patrols reuse the raider art already looked at).

#### Added

- **Patrols**: **3 lights and 1 heavy**, spawned together (`Hub.SpawnPatrol`). With nothing in reach they
  **circle a perimeter 1800 u round the base** (catching their spot at 150 u/s). When a player ship or
  utility ship comes **within 2000 u** (a default), the **lights break off to tackle it** and **their heavy
  takes the same target**, waiting astern for the pin. A raider in no patrol still goes for the nearest
  target. Raids (chunk 7) will spawn patrols in play.

### Earlier in Unreleased

### Chunk 6b: heavy raiders and the predicted missile

**Checked:** smoke test **three runs in a row, 345 checks each** (7 new, one over the network; a heavy
that never waits, a blast that cannot be dodged, and a 1x laser were each put in on purpose in a copy
and caught); screenshot sweep **60 frames, 0 lint**; a heavy waiting astern with its missile in flight
and the red circle looked at.

#### Added

- **Heavy raiders**: the battleship cut in half (the bow half), 30% skinnier, dark red, **4× a light's
  size (136 u)**, with **one turret** — the battleship's front turret — tracking the target on its own.
  Hull 100 (a default).
- **They wait**: **astern of the target** (the approach is from the rear), facing it, **450 u out**,
  holding their laser — until the target is **pinned**. Then they **afterburn** in (300 u/s, a default)
  to **135 u off the hull** (90% of a 150 u reach) and fire a short, hard laser at **2× the raider
  damage (2 DPS)**.
- **Their missile**: within **500 u**, a fat missile at **where the target will be in 7 s** (its speed
  carried forward). A **red circle** in the boss's red marks the spot for all 7 s; the blast (90 u,
  30 damage — defaults) lands exactly there, so **moving off the circle dodges it**. One every 12 s
  (a default). Guests see the circle and the missile; the host lands the blast.

#### Changed (tests)

- **The bounty-credit check is exact**: home credits = the credits set aside at departure + 2000
  (`Yard.TripStartCredits`). A fixed 3000–3400 window failed whenever the hauler sold a load just
  before departure — legal, and shifted into view by the longer run.

### Earlier in Unreleased

### Chunk 6a: light raiders and the pin

**Checked:** smoke test **three runs in a row, 338 checks each** (11 new, one of them over the network;
a pin that does not slow, a boost that does not boost, and all raiders on one post were each put in
on purpose in a copy and caught); screenshot sweep **59 frames, 0 lint**; three raiders pinning a
battleship looked at.

#### Added

- **Light raiders** (enemy fighters; the black-and-red sprite, 34 u, 25 hull — the hull a default).
  Host-simulated and sent to guests; hostile, so every player weapon can hit them and point defence
  treats them as small craft. They go after the nearest **player ship, miner, salvager or hauler**.
- **Their approach**: they cruise at 100 u/s (an unupgraded capital ship), then from **1600 u** — the
  distance 3 s at **500%** covers, plus their 100 u reach — they **boost for 3 s**, landing at their
  post rather than on the target.
- **Their posts**: **ahead, left and right** of the target by its heading, **90 u off its hull**
  (inside 90% of their 100 u reach). Measured from the hull, so they sit beside a long ship, not on it.
- **The pin**: while a light holds its post within 100 u, the target is **held to 20% of its top speed,
  thrusting forward, unable to turn** — player ships (the host tells the owner's helm), miners,
  salvagers and the hauler (20% along its lane). A faint red tether shows the web.
- **Their laser**: **1 DPS** each (the raider damage x).
- Nothing spawns them yet in play: patrols (6c) and raids (7) will. The tests spawn them directly.

#### Changed (tests)

- The raider block hands the ship back at full hull, and the armed-dummy check counts the missile's
  own damage — regeneration had fooled a "hull changed" wait.

#### Decided

- **Boost distance: 1600 u**, from the player's own formula (3 s at 500% + 100 u), which lands the
  raider at its post; the literal "1200 u" would overshoot by 300 u.

### Earlier in Unreleased

### Chunk 6, art: the enemy fighters (staged; no code yet)

- **Light fighter** (`art_unused/enemy_light_fighter.png`): the player's black-and-red sprite, cropped,
  saved at **twice a carrier fighter's texture size** (278 px against 139), so it draws at 34 u to the
  fighter's 17 u.
- **Heavy fighter** (`art_unused/enemy_heavy_hull.png`): the battleship's hull **cut in half
  horizontally** (the bow half), **30% skinnier**, the cut closed with a tapered stern and a dark rim.
  Its **one turret** is the battleship's main turret on the front mount (150.5, 62.4 px), turning on its
  own in the game, as on the battleship. To be drawn about 4× a light fighter (≈136 u).
- Both looked at. They move out of `art_unused/` when the fighters' code lands (chunk 6a/6b).

### Earlier in Unreleased

### Radar selection; warp aims when it jumps

**Checked:** smoke test **three runs in a row, 327 checks each** (5 new; a warp that ignores the heading
at the jump, and a radar that ignores clicks, were each put in on purpose in a copy and caught);
screenshot sweep **58 frames, 0 lint**; the radar's waypoint ring and the HUD's waypoint line looked at.

#### Added

- **Click a marker on the radar to select it.** An enemy becomes the target, as a click in the world
  would make it. A landmark — the base, Threat Intelligence, the portal, the mission portal while it
  is open, the salvage field, the mining belt — or another pilot becomes a **waypoint**: somewhere to
  warp to, never a target for weapons. The radar rings the waypoint and the HUD names it
  ("waypoint: BASE"). A target and a waypoint replace each other; Esc or a double-click on empty space
  clears either.

#### Changed

- **Warp aims at the moment it jumps.** V only starts the 3 s charge; when it jumps, it goes to the
  target or waypoint if that lies within 45° of the bow **at that moment**, else 2000 u along the
  heading **at that moment**. So a pilot can press V and swing onto a target while it charges.

#### Fixed

- **The radar and the K window overlapped** (224 × 180 px; the UI lint found it once the radar took
  clicks): the radar steps aside while the K window is open.

### Earlier in Unreleased

### Chunk 5 of 9: utility-ship hull and rebuilds

**Checked:** smoke test **three runs in a row, 322 checks each** (9 new; a 20% rebuild price and a rebuild
that never waits, each put in on purpose in a copy, were caught); screenshot sweep **57 frames, 0 lint**;
a damaged miner's hull bar and the base menu's rebuild line looked at, close.

#### Added

- **Hull for the utility ships**: miners and salvagers **60**, the hauler **150**. A damaged one shows a
  small hull bar under it (green to red).
- **Losing one**: it is destroyed with a burst — its cargo is lost, and it gives up its unloading arm or
  its place in the queue to the next ship. **30 s later it is rebuilt at the base** (the hauler on its
  pad) **for 10% of everything invested so far in its category's upgrades**; short of that, it waits
  until it can be paid for. (Nothing damages them yet: the enemy fighters, next, will.)
- **Investment per category** (miners, salvagers, hauler): every upgrade's price is added to its
  category's running total. Kept across the arena trip and a guest's visit, and sent to guests.
- **The base menu shows it**: on each tab, "Invested so far" and what a rebuild costs, and any ship
  being rebuilt ("rebuilt in 28 s", or "waiting for 600 cr").

#### Fixed

- **The base, pilot and TIO windows overlapped the multiplayer panel when it was open** (42 px, found
  by the UI lint): they open at x 360 now.

### Earlier in Unreleased

### Chunk 4 of 9: the layout

**Checked:** smoke test **three runs in a row, 313 checks each** (the edge check proven by putting the
belt back in a copy: 1205 u, caught); screenshot sweep **55 frames, 0 lint**; the whole yard looked at
from above.

#### Changed

- **Both fields' nearest edges are 1500 u from the base's centre** (≈6.7 battleship lengths), measured
  to each field's boundary: the belt's nearest rock (the sun moves from y −1500 to −1794) and the
  wreck's visible edge toward the base (its centre moves from x −1250 to −1840). That leaves a band
  for a blockade outside the base's 600 u missile cover. Measured in the running game: belt 1499 u,
  wreck 1501 u.
- **The base's income estimate** (the 1/20 credit while away) uses each ship's real route instead of a
  fixed 1000 u.

### Earlier in Unreleased

### Chunk 3 of 9: the boss health bar

**Checked:** smoke test **three runs in a row, 312 checks each** (7 new; the bounce and the merging each
broken on purpose in a copy and caught); screenshot sweep **54 frames, 0 lint**; the bar mid-bounce and
**the new player missile (carried over from chunk 2)** looked at, close up.

#### Added

- **The boss's health bar**, top centre in the arena: name, tier and hull over a red bar. **Every hit
  leaves a chunk** — exactly the slice it took — that **bobs up and down a few times, quickly, the
  bounces dying away, while it turns white and fades** (0.8 s). **Hits within 60 ms share one chunk**,
  so a stream of small hits reads as one clean chunk. Guests see it too (it follows the boss's hull).

#### Fixed

- **The bar threw on its first draw** before any boss existed (every peer, found by the run): it starts
  hidden and never draws without a boss.
- **Test: the burst check could not fail** — it hit ten times inside one frame, and the bar reads the
  hull once a frame. It now spans frames, and a merge mutant proves it.

### Earlier in Unreleased

### Chunk 2 of 9: boss and combat tuning

**Checked:** smoke test **three runs in a row, 305 checks each** (13 new; four rules broken on purpose
in a copy — per-source hits, regeneration, the beam's tick, the trident's spread — and every one was
caught); screenshot sweep **53 frames, 0 lint**. **Not looked at yet:** the new player missile's shape.

#### Changed

- **The boss (tier 0): 1200 hull; guns 3 DPS** (3.6 every 1.2 s).
- **The boss's rhythm (30 s)**: the **death beam** (first at 6 s, then every 30 s) is now **live for 1 s
  after its telegraph, checking every 0.51 s, 100 each time it lands**; a **charge** 15 s after each
  beam — a red line for 1.5 s, then a ram along it at 1200 u/s, **40** to any ship in its path; a
  **trident volley** half-way between — **3 guided missiles at 0° and ±25°, 15 each, twice the size,
  135 u/s (10% slower), 1920 u (20% further)**, all interceptable. (All boss damage × the tier's scale.)
- **One source lands at most once per 0.35 s on a player** (a trident's three missiles are one source).
- **Regeneration**: 0.5% of max hull a second in combat, 3% out of it.
- **Out of combat = 12 s** without dealing or taking damage (music and regeneration).
- **Music**: with a target selected (the softened combat track), the ambient plays at half (0.35).
- **The player's missile**: sharper nose, 20% skinnier, 25% longer, swept fins, a band and a seam.

#### Added

- **Damage taken by source** (`PlayerShip.DamageBySource`), host-side — used by the tests, ready for a
  damage meter.

#### Decided

- **Field distance (chunk 4)**: both fields' edges **1500 u from the base's centre** (≈6.7 battleship
  lengths), leaving a blockade band outside the base's 600 u missile cover.
- **New chunk — base weapons** (after the enemy fighters): a **5 DPS laser, 300 u**, and a **tracking
  25-damage missile, 600 u, ~120 u/s** (1.2× the capital ships' average top speed), **one every 5 s**
  (default).

### Earlier in Unreleased

### Chunk 1 of 8: warp

**Checked:** smoke test **three runs in a row, 292 checks each** (8 new, each proven able to fail by
breaking warp on purpose); screenshot sweep **53 frames, 0 lint**; the warp frames looked at.

#### Added

- **Warp (V) for every capital ship** — a fixed key and a **hull cooldown, never an ability-bar slot**.
  After a **3 s warm-up** (an accent-coloured charge around the hull) the ship jumps to the **selected
  entity if it is within 45° of the bow**, stopping just short of it (its radius + half the ship + 60 u),
  otherwise **2000 u straight ahead**. Then a **30 s cooldown**. The hull bar reads WARP READY /
  WARPING 1.4 s / WARP 12 s. Other players see the charge and a clean snap, not a glide.
- **The camera cuts to your ship after a jump** (a 2000 u pan was disorienting).

#### Changed

- **Fire mode moves from V to G.** V is a fixed key now; a saved binding on a fixed key is ignored.

### Earlier in Unreleased

### Sound, UI remaster, double-click clear, boss tiers (this batch, part 1 of 2)

**Checked:** smoke test **three runs in a row, 284 checks each**; screenshot sweep **51 frames, 0 lint**;
the new UI and the TIO's difficulty row looked at.

#### Changed

- **Music is 35% quieter**: the whole score plays through a 0.65 master (so every slider position is quieter).
- **UI remaster**: every panel is a shaded, sharper face — a vertical gradient, a bright top edge, a
  crisp border, 3 px corners and a soft drop shadow — and buttons, text fields and bars share one theme
  in the same style (installed on the root window). **The old flat panel style is deleted**, including
  the K window's own copy.

#### Added

- **Sound effects** (synthesised, `sfx/`): lasers **buzz** where they fire (a light buzz for ships and
  craft, a **deep grinding buzz for a boss**) and **crackle** where they hit; missiles and torpedoes
  **whoosh** at launch and land with a **low thunk and a soft reverb**. Loudness follows the camera —
  the distance from the view's centre and the zoom — and a laser hit is also softer and lower the
  longer the shot.
- **Double-click on empty space clears the target** (a single click there does not).
- **Boss difficulty tiers**: tier n is **1.1^n** as strong (hull and every attack). Beating a tier
  unlocks the next, and the TIO **selects the newest unlocked tier** when the host opens it; the host
  can step down to any beaten tier (◀ ▶). Saved per pilot; the tier reaches the guests with the mission.
  Bosses are data now, ready for more types.

#### Still to come (in order)

- **Light and heavy enemy fighters** (tackle and afterburn), then **base raids** on failure, then
  regeneration, the death beam's ticks and invulnerability, the boss health bar, trident missiles.
- Carried over: the BASE button in the arena, the multiplayer arena test, warp (V).

### Earlier in Unreleased

### The boss mission (this batch)

**Checked:** smoke test **three runs in a row, 269 checks each**; screenshot sweep **51 frames, 0 lint**;
the arena frames looked at (the death-beam and shockwave telegraphs, the boss).

**Integrity findings.** Two commits (16:55, 16:59) and an uncommitted screenshot-tool edit came from an
interrupted run of this request. All of it serves the player's current requests: **reviewed, tested,
adopted**. Its claim of 267 checks held.

#### Added

- **READY autopilots you to the mission portal**, with a **capital-ship** variant (rudder and throttle,
  turning radius) and a **fighter** variant (point and burn; ready for the first player fighter class,
  tested on its own). **Everyone READY** opens the portal; **everyone at the portal** goes through.
- **The arena**, a scene change (the game scene reloaded in arena mode): the **Silver Lancer** — 3000
  hull; guns every 1.2 s; **4 guided missiles every 7 s that point defence can shoot down**; a **death
  beam** behind a 2 s **red line telegraph** (60 damage); a **shockwave** behind a 1.8 s **red ring
  telegraph** (45 within 340 u). A **win** pays **300 + 100 EXP to every pilot** and **2000 credits**,
  then home; a **wipe** (every ship in stasis) goes home with nothing.
- **The base while away earns 1/20** of its fleet's income for the time spent on the mission.
- **Point defence takes missiles first, then small (fighter-sized) craft, then everything else.**
- **Music is on by default**, with a **MUSIC ON/OFF** toggle in the Esc menu (saved).

#### Fixed

- **A guest who followed the party into the arena lost its own base**: the base it set aside was held
  on an object the arena's scene reload destroyed. It is kept apart from the scene now.
- **A guest whose host dropped mid-arena was stranded there** (no base; the next look at it crashed).
  It is sent home, to its own base.
- **Test: `Me` was the host's ship on every guest** (it looked up `Ship_1`); it is each peer's own ship.

#### Not yet verified

- **The multiplayer arena end to end.** Seen working: all three pilots at the portal, the host taking
  the party through, the first guest arriving. Not yet shown by a passing test: the second guest
  arriving, and the two fixes above over the network (the three-process timing needs its own test).
- **The BASE button in the arena**, where there is no base: it may be harmless or may fail.

#### Still to come

- **Warp (V)** for every capital ship (fire mode to G; 30 s cooldown).

### Earlier in Unreleased

### Pilot progression, building menus, the TIO mission portal (this batch)

**Checked:** smoke test **three runs in a row, 256 checks each** (22 new, including the party of
three over the network); screenshot sweep **49 frames, 0 lint findings**, the new frames looked at.

#### Added

- **Pilot progression.** EXP comes from completing missions and killing bosses and is **shared by
  everyone in the session** (the host awards it). Each level needs **100 × 1.5^(level−1)** EXP and
  pays **1 point**. The **PILOT window (L**, or the PILOT button) spends points on flat upgrades:
  **Rudder +1°/s turn, Hull +5, Engines +1 speed, Weapons +1 damage** (the battleship's main guns,
  the carrier's torpedoes). Each upgrade's next level costs one more point (**1, 2, 3, …**).
  Saved with the character; the ship refits at once, keeping damage already taken.
- **Flat stat bonuses**: every stat is now (base + flat) × (1 + bonus); pilot upgrades are the flat part.
- **The host knows each pilot's upgrades** (they travel with the pilot's identity), because the host
  resolves hull and damage. It refuses purchases the pilot's claimed level could not have paid for.
- **Left-click opens a building's menu**: the TIO opens **WARP TO TARGET**, the base station opens BASE.
  A hostile under the cursor is still selected first.
- **WARP TO TARGET**: the Silver Lancer bounty (300 EXP each for the kill, 100 each for completing,
  2000 credits), **the party — everyone in the session — with each pilot's READY**, and **WARP**
  (host only, unlocked when everyone is ready). WARP runs a **3 s bar at the TIO's top right**, then
  the **red mission portal** opens there, on every screen.

#### Still to come (next, in order)

- **The boss arena**: the scene change through the mission portal, the Silver Lancer (≈3000 hull,
  guided missiles and a gun battery, slow and heavy), and the EXP and credits on the kill.
- **Warp (V)** for every capital ship.

#### Known limits

- **Progression is kept on each pilot's own machine.** The host checks a claim is affordable at the
  pilot's level, but cannot check the level itself; a server that owns characters would.

### Earlier in Unreleased

### Missiles, wing, hauler landing, PD, TIO (this batch)

**Checked:** smoke test **three runs in a row, 234 checks each**; every new check proven able to fail
(the feature broken on purpose in a copy); screenshot sweep **45 frames, 0 lint findings**, looked at.

#### Changed

- **Missiles need a selected target within range.** No more fallback to the nearest hostile. Pressing
  F without one fires nothing; the missile slot shows **NO TARGET** or **OUT OF RANGE** in red for
  1.5 s (checked on the owner's machine at once; the host checks again).
- **The battleship starts with one missile** (magazine 1).
- **The carrier starts with three fighters** (was four).
- **Fighters leave the hangar at least 0.83 s apart.** A fixed constant (`Wing.LaunchInterval`), not a
  stat: no upgrade or rate-of-fire bonus may change it.
- **Torpedoes: 90 u/s** (25% slower) **and one every 0.5 s** (70% of the old rate) — the new baseline.
- **Bombers are snout-nosed and 25% smaller** (28.1 u): the long nose boom is replaced by a broad,
  rounded snout carrying the trefoil. Their docking slots are re-fitted. The old sprite is in
  `art_unused/wing_bomber_longnose.png`.
- **The hauler turns while it descends** onto its pad (3 s), arriving facing the portal. The separate
  turn on the pad is gone (`Hauler.St.Turning` and `Economy.HaulerTurn` removed).
- **Hit flashes are drawn above the hulls** and below the turrets, and PD shots leave from the barrel
  tip: they used to be drawn at the world's own level, so they looked as if fired from under the ship.

#### Added

- **Threat Intelligence Operations**, a building south-west of the base, from the player's sprite made
  exactly symmetrical; its name is drawn beneath it and it has a radar marker. (Its missions: next.)
- **The boss sprite**, cropped and staged in `art_unused/boss_silver_lancer.png` until the boss exists.

#### Decided

- The "level 13 cost" idea is **dropped** at the player's request.

### Earlier in Unreleased

### Verification pass (first run of the project protocol)

**Checked:** code analysers clean; host-authority review of every write to world state (all
host-guarded or called only from host-guarded code); smoke test **three runs in a row, 226 checks
each**; screenshot sweep **45 frames, 0 lint findings**, every new frame looked at.

**Integrity findings.** Six commits (06:26–06:45) were never reported to the player: steady plumes,
camera zoom and free camera, radar, Esc menu, composed music, UPnP internet hosting. The plume change
matches a request and is **adopted**. The rest was verified working, and the player has since
**confirmed all of it was requested: adopted**. Their change-log claim that
the new frames had been looked at did not hold (the Esc-menu frame showed no menu; see below).

#### Added

- **Leak checks in the smoke test**: after leaving and re-entering the hub, orphan nodes must not
  grow and, once .NET garbage is collected, neither may the object count. Proven: a deliberately
  leaked node makes the orphan check fail.

#### Fixed

- **The hull bar and ability bar built a new panel style every frame** (about 130 short-lived
  objects per hub visit, freed only when .NET collected). Each is built once now.
- **The radar's free-camera box spilled outside the radar disc** at the view's edge; it is clipped.
- **The screenshot tool's Esc-menu frame showed no menu** and left the menu open in later frames:
  Esc peels layers, and a selected target went first. The tool opens and closes it directly now.

### Earlier in Unreleased (before this pass)

### Camera, radar, music, internet hosting (this batch)

**Checked:** smoke test **three runs in a row, 224 checks each** (new: zoom limits, free camera and
its 5000 u tether, edge pan, radar sizes, Esc menu, music moods, the hosting fallback);
screenshot sweep **45 frames, 0 lint findings**, the new frames looked at. **Not checkable here:**
the music has been verified technically (length, seamless loops, levels) but not listened to; and
internet hosting through a real router, across real networks, has not been tried.

#### Added

- **Camera zoom**: the mouse wheel, from **33% further out** than the default to **1.5× closer**.
- **Free camera (Y)**: move it with the **arrow keys** or the **mouse against a screen edge**, up to
  **5000 u** from your ship (2500 for a fighter-class ship, when there is one). **Y** again returns
  to the ship and keeps your zoom. The HUD says FREE CAMERA; the radar boxes what it sees.
- **Radar**, top right: north-up, centred on you (on your pod in stasis), 3000 u. Base, portal,
  wreck, asteroids, gatherers, hauler, hostiles (selected one ringed), other players.
- **Esc menu**: Esc, once nothing else is open, opens it: **radar size** (small, medium, large),
  **music volume**, RESUME, QUIT TO MAIN MENU. Settings save at once. The helm locks while it is
  open; the world keeps running (multiplayer cannot pause).
- **Stateful music**: two original loops, composed for the game in code (`music_ambient.ogg`,
  64 s; `music_combat.ogg`, 32 s at 120 BPM), both seamless. **Ambient** when nothing is selected;
  **combat at 20%** when an enemy is selected; **combat at 100%** when you dealt or took damage in
  the last 8 s (host-tracked, so it works for guests), or in a combat zone (`Music.CombatZone`, for
  the instanced systems to come). Every level is a share of the music volume. Replace either file
  with a real track of the same name and nothing else changes.
- **Internet hosting**: HOST asks your router (**UPnP**) to forward UDP 27015 to this PC and reads
  back your public address, so friends in other cities or countries can join. The multiplayer panel
  shows "friends anywhere join: IP:port" with **COPY ADDRESS**. When the router will not (UPnP off),
  or your provider shares one public address among many homes (**carrier-grade NAT**), it says so
  and what to do instead (forward the port, or Tailscale/ZeroTier). The port is closed again when
  you stop hosting. LAN hosting starts at once either way.

#### Changed

- **Engine plumes hold steady** unless the craft is moving or thrusting.
- **Esc no longer drops straight to the main menu**; that is QUIT in the Esc menu.
- **Fixed keys** now include **Y and the arrow keys** (camera); the mouse wheel zooms.

#### Fixed

- **The music loops were "still in use at exit"**: the player now silences them a moment before
  quitting, in real time, so the audio mixer lets them go.

### Earlier in Unreleased

### Inspection, combat and death, wings, engines (this batch)

**Checked:** code analysers clean; smoke test **three runs in a row, 203 checks each**; screenshot
sweep **43 frames, 0 lint findings**, with the new frames looked at (bays, shield, stasis and pod,
battleship size, docked bombers, strike, plumes, own ship without a bar).

#### Inspection (step 1)

- Ran the compiler's analysers (unused/unread private members, unused assignments and parameters,
  needless usings) over all code: only **4 needless `using` lines** — removed. Public members used
  only by tests are test hooks and stay; `BeginPlacement` stays as the hook for placed abilities.
- **Stale comments fixed**: the ship menu no longer opens with C; saves are per character, not
  `character.cfg`; the fighter and bomber comment quoted old sizes. `Gatherer.BoltCount` (dead) removed.

#### Added

- **Enemy fire hits players**: player ships have a **capsule collider along the keel** (half-width
  40 u battleship, 24.5 u carrier). Nothing else changed about who can hit whom.
- **Shield flash**: a hexagonal projection lights the **side that was hit** (front, right, back or
  left, relative to the ship's heading) in the **accent colour**, and fades. One generic panel for
  all four sides. Visual only.
- **Armed target dummy**: the bottom-right dummy fires a guided missile every **5 s** at the nearest
  player ship within **150 u**, for **50 damage**; a faint red ring shows its reach.
- **Death and the escape pod**: at 0 hull the ship goes into **stasis** where it lies (cold blue,
  turrets dark, a countdown over it) for **2 minutes**, and the pilot flies an **escape pod** (24 u,
  60% of a salvager) that nothing can target. After the timer, **F** re-boards the ship at **33%
  hull**. The camera follows the pod meanwhile.
- **Fighters dock inside the carrier** (out of sight) when idle and to rest; **bombers back into
  their slots**, nose out, tail to the hull, and stay visible. A faint, flashing yellow and orange
  **signal light** marks every landing and take-off.
- **Fighter strafing runs** (all fighters): each pass fires **3 shots**, carries on **through the
  target by 1.2× its diameter**, then turns for the next pass. Fighters fly like aircraft now
  (steady speed, 3.5 rad/s turn). After 15 s engaged they dock and rest 3 s.
- **Engine plumes on everything**, sized by the ship's length and brightened by throttle: player
  ships, fighters, bombers and escape pods in the **accent colour**; miners, salvagers and the
  hauler in **light yellow**, unaffected by player colours. Missiles keep their smoke trails.

#### Changed

- **Colours**: the **hull colour** is the ship and its turrets; the **accent colour** is engines and
  lighting only (plumes, shields, PD arcs). The ship menu's preview shows the accent as a plume.
- **Battleship 224 u** long (was 160, +40%); turrets, mounts and collider scale with it.
- **Your own ship shows no health bar** over itself (the HUD's hull bar is yours); other players'
  bars remain.
- **Service bays sit on each pad's north edge**, parallel to it; the **top bay unloads from the
  north** too.

#### Removed

- **`fighter_accel`** — fighters fly at a steady speed with a turn rate now, so the stat did nothing
  (it still showed in the K window).

#### Fixed

- **The K window no longer counts fighters as sustained damage.** They strafe and rest, so their
  2.0 DPS (four fighters) is now shown as the rate while firing, outside the sustained total.
- **Two comments** still described fighters orbiting and bombers docked parallel to the hull.
- **A faded shield left its last faint frame on screen** for good (no redraw as the glow reached
  zero). Found in the screenshots.

### Earlier in Unreleased (previous batch)

### Code inspection, docking bays, player bars, bigger battleship (this batch, in progress)

**Checked so far:** typecheck, build 0 warnings, smoke 186/186, 41-frame sweep with 0 lint findings;
the bays and the battleship looked at in rendered frames.

#### Inspection (step 1)

- **Whole-codebase audit:** a full rebuild shows **0 compiler warnings**; **no references** to
  removed features remain in code or comments; **every network message** checks its sender or is
  host-only by design (`RequestAbility` checks the sender owns the ship); **every change to
  host-owned state** is host-guarded or applies host data. No incorrect calls were found.
- **Removed** the unused `Gatherer.BoltCount`.
- **Comments state what the code does, not its history**: eight comments that narrated old
  behaviour ("used to…", "was 700", "the old 8 s cooldown") were rewritten. History lives here.
- Found, not yet fixed: a ship at 0 hull only stops being "alive", and its wing deletes itself for
  good. The escape pod and stasis work in this batch replaces that.
- **Git**: the project is now a git repository; the verified build is the first commit.

#### Changed

- **Docking bays follow the pads.** Each bay's open face is the north-facing edge of its pad,
  measured from the art. The four diagonal pads are turned 30°, so their bars now lie along their
  tilted north edges instead of floating level above them, and ships nose straight into them.
- **The top bay loads from the north** (was the west), bar and all.
- **Your own ship shows no health bar over itself**; your hull is on the HUD bar. Other players'
  ships keep their bars and names.
- **The battleship is 40% larger**: 224 u (was 160), with every turret mount, turret sprite,
  barrel length and PD ring scaled to match. Ships spawn 40 u further from the base to clear it.

### UI and visual audit (this batch)

**Checked:** every feature through the full smoke test (186 checks, three runs in a row); every
screen state rendered — **41 frames**, from the main menu to each stage of the hauler's run — and
**looked at**; and a new automated **UI lint** run on every frame, which reports **0 findings**
after the fixes below.

#### Added

- **UI lint in the screenshot tool**: on every frame it flags any visible control that is
  off-screen, any label or button whose text is wider than its box, and any two top-level HUD blocks
  that overlap.
- **The screenshot tool clears a stale display lock** left by a sandbox restart, stops with "NO
  VIRTUAL DISPLAY" if none comes up, and streams its output line by line, so a run that is cut
  short still leaves its log.
- **The screenshot tool covers every state**: 41 frames — menus and dialogs, all four base tabs
  and the armed RESET, key capture and a refused key, missile flight and impact, every ability-bar
  state, the placement ring, torpedoes, fighters resting, and the hauler lifting, departing,
  charging, arriving, landing and turning.

#### Fixed

- **The DISPATCH widget** shows only while the hauler is loading, wholly on screen, and not under
  the BASE menu. It was being drawn half off the screen edge.
- **The K window's final column** is no longer under its scrollbar (right margin).
- **The main menu's title** sits with its panel, centred together, instead of pinned to the top.
- **The delete confirmation** uses the game's panel style with a proper title bar (it was Godot's
  default grey, then briefly a title floating over the list).
- **The BASE menu and K window are fully opaque.** A faint line under the last BUY button was the
  player's own hull bar showing through the panel. Other panels went from 90% to 96% opacity.
- **BUY buttons keep their own height**, centred beside the two-line descriptions.
- **Hidden ships hide their wing and bars** (only the screenshot tool hides a ship).
- **The hauler lands on its pad, then turns there** at landed size. Turning at full size swept
  its 200 u length into the station.
- **Salvagers aim at the wreck's measured hull**: its outline is ragged, and a fixed radius left
  some beams cutting empty space.
- **The ship menu is centred on any screen size**; the **BASE menu opens clear of the multiplayer
  panel**; the **address box placeholder is shorter** so it is not clipped; the **K window's
  key-binding message sits above the rows** so it is seen without scrolling.

### Docking arms, fleet, hauler pods, wing tuning, clean-up (this batch)

**Checked:** typecheck clean, build 0 warnings, and the smoke test passed three runs in a row
(185 checks: single-player, then a host and two guests). Rendered and looked at: the base with its
hologram bars, docked ships and the unload queue; the hauler landed on the enlarged pad with its
pods and DISPATCH bar; the salvager's scan beam; the smaller fighters and bombers.

#### Added

- **Five service arms** on the base. Each takes one miner or salvager at a time, loading through
  its open face — north, or west at the top arm — marked by **blue hologram bars that flash
  gently** (brighter when taken).
- **An unload queue**: a full ship takes the free arm nearest it, or waits in line north-east of
  the top arm; the head of the line takes the next arm that frees up.
- **+1 Miner and +1 Salvager upgrades**: 400, 800, 1600, 3200 credits (each 2× the last), **up to 5
  of each**; the button reads MAX at the cap. Miners spread over different asteroids and salvagers
  over the face of the wreck.
- **A hollow yellow progress bar** over each miner and salvager, filling with its hold.
- **Hauler cargo pods**: six painted on its hull; it **starts with one of 300**. The **HAULER tab**
  adds pods (500, 1000, 2000, … up to 6) and pod size (+10% per level, 1.25× cost).
- **Manual dispatch**: a **floating DISPATCH button with a per-pod progress bar** over the docked
  hauler. It works once **at least one pod is full**; with **every pod full** the hauler leaves by
  itself. A guest's press is a request the host checks.
- **Hauler landing illusion**: it settles onto its pad by **shrinking to 65%** with a hover
  wobble, and lifts off by growing back — descending and climbing on the z-axis in appearance.
- **Hauler loading and unloading effects**: motes stream from the base into the filling pod, pods
  light as they fill, and flash empty on its return.
- **Base menu tabs**: MINERS, SALVAGERS, HAULER, REFIT.

#### Changed

- **The bottom pad is 140 u wide and twice as tall** (140 × 85 u), and the hauler lands **on** it.
  The pad and the portal share one horizontal line, **y = 219**; the portal moved to (1500, 219).
- **The target dummies are 250 u lower**: (600, 670), (900, 550), (900, 850), out of the hauler's
  path.
- **Mining and salvage rate 2 units/s** by default (was 8).
- **Fighters are 50% smaller** (17 u) and fly in bursts: after **15 s of firing** each returns to
  the **carrier's centre for a 3 s rest**, then rejoins.
- **Bombers are 25% smaller** (37.5 u; their docks moved in to match) and **keep closing slowly
  while they launch** instead of stopping.
- **Torpedoes are 50% slower (120 u/s) with 35% more range (1215 u)**, and bombers launch from 35%
  further out (**567 u**).
- **The salvager's beam is a narrow, sweeping scan** (thin arcs in a tight cone) rather than a wide
  fork.
- **The economy moved into one node, `Yard`**: stock, upgrades, fleet, arms, queue, hauler and
  their networking, out of `Hub`.

#### Removed

- **`Econ.cs` and `Sound.cs`** (never used), the **five audit tools in `tools/`** (they never ran),
  the **one-time migration from the old single-character save**, and the **leftover GOODS counter**
  and its networking.
- **18 unused sprites moved to `art_unused/`** (Godot ignores the folder), kept at the developer's
  request.

### Economy, base menu, missile, panels

**Checked:** typecheck clean, build 0 warnings, and the smoke test passed **three runs in a row with
168 checks each** — 136 single-player, then a **host and two guests** (9 + 20 + 3). Rendered and
looked at: the BASE menu, the miner's steady beam, the salvager's forking beam, the hauler charging
in its aura at the portal, the new base with the hauler docked on the lane, and the panels on every
HUD element. The run found one real bug (below) and the frames found three visual flaws (ragged BUY
buttons, a faint aura, a mistimed shot), all fixed.

#### Added

- **Miner and salvager**, the utility ships that gather now. Each starts with **100 hold**, 110 u/s
  and 8 units/s. The miner holds off the asteroid nearest the base and mines it with **one steady
  beam**; the salvager holds off the wreck and cuts it with a **forking beam that re-forks every
  0.06 s**. Each flies home when full and **unloads at the player base**: miner at the top pad (ore),
  salvager at the upper-left pad (salvage).
- **Miner and salvager sprites** from the developer's small X-shaped ship, mirrored exactly
  symmetrical: the salvager **safety orange with black caution chevrons**, the miner **ore brown
  with white struts, caps and core**.
- **Hauler** (the developer's long ship with blue-tinted panels). Docked beside the base, facing
  the portal, it loads 200 from the base's stock; when full it moves **slowly and perfectly flat**
  to the portal, **shakes in a building blue aura for 3 s**, **warps out**, is gone **30 s**, and
  **warps back** facing the base; its cargo is sold (1 credit per unit); it **drifts slowly** back
  to its dock and **turns 180°** to face the portal, then loads again.
- **Upgrades** (in the BASE menu): miner hold, salvager hold, miner speed, salvager speed, mining
  rate, salvage rate. Each level **+10%**; each level costs **1.25× the last** (100, 125, 156, …).
  BUY greys out when you cannot afford it. A guest's BUY is a request the host checks.
- **BASE menu (B, or the BASE button)**: the upgrades, and **REFIT**, whose **RESET** button (two
  clicks) takes **10% of ore, salvage and credits** and opens the ship menu. It is now the only way
  to change class, name or colours.
- **Bunker-buster missile**: the battleship's missile is a real projectile — heavy, slow
  (**130 u/s**) and **barely guided** (turns at most **0.35 rad/s** toward its target), with dark
  smoke and a big blast. Guests fly a cosmetic copy.
- **Panels behind every UI element**: the multiplayer control, the BASE button, the controls line,
  the ability bar, the hull bar, the base menu, the creator's columns and the main menu now sit on
  the same panel style as the stats line (`Ui.PanelStyle`).
- **Three-player smoke test**: a host and two guests; each sees the other two.

#### Changed

- **New player base sprite** (`base_station.png`), drawn at 0.6 (406 × 440 u).
- **`salvager.png` is replaced** by the new salvager. The old file was an unused sprite carried over
  from Space Fleet Idle; nothing referenced it. `base_player.png` (the old base) is likewise now
  unused and kept only as a carried-over asset.
- **The portal moved to (1500, 200)**: the haul lane, 200 u below the base's centre, runs through
  both it and the hauler's dock.
- **The passive 1/s ore and salvage tick is gone**; only the gatherers bring resources in.
- **C no longer opens the ship menu** (REFIT does). The fixed keys are now W A S D, Tab, K, **B**,
  Esc, Enter; C is free to bind.
- **HUD line**: ore, salvage, credits, and the hauler's load and state (GOODS dropped until
  refining exists).
- **The smoke test stops if it cannot compile**, streams its output line by line (so a long run
  can be watched), and the solo run's time limit is 20 minutes. A full run takes about 4 minutes.

#### Fixed

- **Joining someone's session no longer loses what your ships carry.** Before your world is
  parked, the miner's and salvager's cargo returns to stock, and the hauler's either returns (still
  at home) or is paid out (already through the portal). Found by the test: a guest came back 16
  units short.
- **`Hauler.Hidden` hid Godot's `Hidden` signal**; renamed `WarpedOut`.
- **Leaving or joining a session resets the miner, salvager and hauler** to docked and empty. They
  used to keep the host's last state, so a guest who left while the host's hauler was out would be
  paid the host's cargo in their own world.
- **A guest's own upgrade levels are parked and restored** with their totals; the host's levels
  used to overwrite them for good.
- **The smoke test fails fast on an exception.** A test that threw used to stop without quitting,
  and the run idled to its time limit — which is what looked like a stall last session. Output is
  now line-buffered, so a running test can be watched.

### Earlier in Unreleased

**Checked:** typecheck, build (0 warnings), smoke test ×3 (106 + 22 checks), and rendered frames of
everything visual below: the hub with its panel, folded multiplayer button, stars and ability bar;
both ships up close with every turret trained on one point; bombers docked; fighters attacking.
Measured: battleship top speed 104 u/s (40% of 260) and full-speed turn rate 0.972 rad/s (1.197× the
old 0.812).

### Changed

- **The turrets are the ones painted on the ships.** Each was cut out of the hull art into its own
  sprite and the hull repaired underneath, so the painted turret itself turns. Battleship: its four
  double-barrel centreline turrets are the four main guns (one gun each) and its two sponson turrets
  are point defence. Carrier: its three domes are its point defence. They take the hull colour, as
  they did when painted; the accent colour shows on the PD cycle arc.
- **Turrets are in proportion** — the art's own size, replacing the oversized drawn circles.
- **Battleship is 160 u long** (was 102), on a new `battleship_hull.png`. The main menu diorama
  keeps the original `capital_ship.png` with its turrets painted on.
- **The player base is 1.2 scale** (was 0.75): 360 u tall. Ships spawn below it, clear of it.
- **Previews draw the cut-out turrets** as painted: forward pair ahead, aft pair astern.
- **Carrier hull regenerated** from the repaired full-size art; still exactly symmetrical.

- **Capital ships are slower and turn quicker**: speeds and accelerations are 40% of 0.3.0's
  (battleship 104 u/s, carrier 96 u/s ahead), and turning radii are 107 u and 127 u, so the turn
  rate at full speed is 20% faster than before. Rudder limits are up 20% so they never cap it.
- **Fighter control range 1400 u** (was 700).
- **The fighter is a new sprite**: the developer's black-and-purple fighter recoloured white
  (brightness remapped so shading keeps its direction, dark outline kept, purple to neutral).
- **The stats line sits on a panel** at the top left.
- **Multiplayer options fold behind one button**, "▸ MULTIPLAYER (offline / hosting · n / guest ·
  n)", closed by default.

### Added

- **Bombers dock on the carrier's flanks**, alternating port and starboard so the two sides always
  split evenly (6 → 3 and 3; an odd one goes to port), facing with the carrier. They launch from the
  dock, return to their own slot, and rearm there. On a guest, a bomber the host reports docked is
  locked to its slot locally, so it does not trail a moving carrier.
- **Bomber strike range: 2800 u**, defined as twice the fighters' control range (and taking the
  same bonus). A target beyond it is refused, and a strike whose target moves beyond it is called
  off. The fighters' and bombers' slots say "OUT OF RANGE" when the selection is too far.
- **Background stars**: a seamless 1024 px tiled image (`stars.png`) on a screen-space layer at
  −100, behind everything. Purely local; nothing networked.
- **Six open hotkeys (1–6)** after every class's own abilities, on every class including future
  ones. They show on the bar, remap in K, and do nothing until filled.
- Turret textures `turret_bs_main.png`, `turret_bs_pd.png`, `turret_carrier.png`.
- **Screenshot tool** now also frames a fresh spawn under the base, and zoomed close-ups of both
  ships with every turret trained on a fixed point (controls locked so the mouse cannot move it).
- Smoke checks: turrets are the cut-out sprites (4 main + 2 PD), battleship 160 u, base 1.2, spawn
  clear of the base.

---

## 0.3.0 — naval helm, abilities, new art (2026-09-18)

**How this was checked.** See the Handoff. Measured: a battleship at full ahead turns a 320.0 u
circle against the sheet's 320, with 0.00 u/s sideways drift; salvo and staggered both 6.00 DPS;
fighters 2.013 DPS against 2.00; a single torpedo hits a still target and misses one that steps
aside after launch.

### Added

- **Naval helm.** W ahead, S astern, A/D rudder. Thrust only along the keel; the keel kills
  sideways drift; the ship turns on a radius (battleship 320 u, carrier 380 u) and cannot turn with
  no way on. Top speed 260 / 240 u/s ahead, 90 / 80 astern.
- **Ability bar** along the bottom: each ability's key, name, live state (ready, active with time
  left, magazine count, rearm countdown) and a recharge sweep.
- **Remappable keys** in the K window's new **Abilities & keys** tab: click, press a key. A clash
  swaps the two; fixed keys are refused with the reason; Esc cancels the capture; reset to defaults.
  Saved per class in `settings.cfg`.
- **Point defence is an active ability** (Q): a 15 s window, then 15 s recharge. While active each
  turret picks its own target, preferring one no sibling has claimed.
- **Missile magazine** (2) with **R to reload** (16 s); 0.6 s between shots.
- **Bomber strike** (carrier, F): bombers run at the target, stop at 420 u, line up and launch 4
  torpedoes each, then return and rearm for 6 s.
- **Torpedoes**: straight, steady 240 u/s, no tracking, smoke trail, 3 damage, 900 u run. Guests
  fly a cosmetic copy from the launch message.
- **R recalls the fighters** (carrier).
- **New art.** Carrier: the developer's grey ship, made exactly symmetrical, 170 u long, its three
  painted domes as PD mounts. Fighter: the same ship at 34 u. Bomber: the developer's small airframe
  at 2× resolution, wings swept forward, radiation trefoil on the nose, 50 u.
- **Previews show the real ship**: the class sprite in the hull colour with accent turrets on the
  real mounts, nose-right in the select screen's rows.
- **Class-specific controls line**; a class without abilities shows "placeholder".
- **`tools/screens/`**: renders real screenshots on a virtual display.
- **Guests see ability state:** PD window, magazine and reload, fighter target, bomber state.

### Changed

- **The hull no longer follows the mouse**; only the battleship's main guns do.
- **Carrier: Space sends only the fighters**; bombers are their own ability.
- **Battleship PD swings slowly** (τ/3); the carrier's stays fast (τ/1.2).
- **Carrier point defence: 3 turrets** (was 2).
- **Dummy meters reset themselves** on the first hit after 5 s idle; R no longer resets them.
- **The K window has two tabs**: Abilities & keys, and Stats.
- **Hull bar and pilot name sit above the hull** at any ship length.
- **Creator background is opaque**; the select screen no longer shows through.
- **Class blurbs** describe the current loadouts.

### Removed

- **Class hotkeys 1 and 2**, their code and their hint. Class changes only in the creator.
- **Hard-coded ability keys** in the hub; everything goes through the bindings.
- `Projectile.cs`, replaced by `Torpedo.cs`.

### Fixed

- **The ship preview** was a placeholder rectangle with dots, never the real ship. It now draws the
  class sprite.
- **`Wing.Ready` and `StatsWindow.Name()` hid Godot's own members** (the `Ready` signal and
  `Node.Name`). Renamed `Armed` and `AbilityName`.

### Known broken

- **No enemies yet.** The dummies never fire back or die. Player hull damage, death and wing losses
  have never run.
- **The wormhole is scenery.**
- **No hauler runs, and no world save.** Ore and salvage reset every launch.
- **Multiplayer is tested only on one machine** (localhost): three players, but never across two
  machines or the internet. ENet over UDP port 27015 with a direct connection: the host must be
  reachable (port forwarded, or a VPN such as Tailscale or ZeroTier). There is no NAT punch-through
  or relay.
- **No world save.** Ore, salvage, credits and upgrade levels reset every launch.
- **"200 units below the player base"** was read as below its centre; the lane runs past the base's
  bottom pad.
- **The hauler carries raw ore and salvage**; there is no refinery or trade goods yet.
- **While turning on its pad the hauler overhangs it** by about 22 u at each end. Turning there,
  at landed size, is what keeps it clear of the station.
- **Wing replication matches craft by list position.** Correct while nothing dies.
- **Stat bonuses are not replicated.** The host runs other players' ships on base stats. Harmless
  while every bonus is +0%.
- **A black hull colour makes a black ship.** The colour multiplies the sprite by design; a
  near-black choice hides all detail. No floor is applied.
- **Checked on a virtual display only**, not on the developer's machine.
- **The audit tools in `tools/*.py` do not run.** They hard-code Space Fleet Idle paths.
- **Carried over but unused:** `Ui`, `Econ`, `Sound`.
- **Balance is lopsided.** The battleship beats the carrier by about 2.4× on paper.
