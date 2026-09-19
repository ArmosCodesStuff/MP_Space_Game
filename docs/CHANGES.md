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
`dotnet build` clean (0 warnings), and the smoke test passes three runs in a row: 256 checks across
a single-player run and a host with two guests. Unlike earlier releases, this one was also **looked at**:
`tools/screens/run.sh` renders the select screen, the creator, a fresh spawn under the base, both
classes in the hub, the K window, a bomber strike, and turret close-ups on a virtual display; layout
faults have been fixed from those frames more than once.
Still unconfirmed: how it looks on the developer's own machine and GPU.

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

1. **Typecheck:** `cd typecheck && sh typecheck.sh` → must print `0 errors.` and exit 0. Needs the
   .NET 8 SDK and `typecheck/GodotSharp.dll` (gitignored; the developer supplies it from
   `C:\Users\<you>\.nuget\packages\godotsharp\4.7.2\lib\net8.0\`). Exits 2 without an SDK.
2. **Smoke test:** `sh tools/smoketest/run.sh <path to Godot_v4.7.2-stable_mono_linux.x86_64>`
   → must end `SMOKE TEST PASSED`. Real engine, headless, solo at a fixed 60 fps plus a host and a
   guest over localhost, in its own `user://`. Add checks to `tools/smoketest/SmokeTest.cs.txt`.
3. **Look at it:** `sh tools/screens/run.sh <same binary>` → frames in `/tmp/shots/`. Do this for
   anything visual; the smoke test cannot see.
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
