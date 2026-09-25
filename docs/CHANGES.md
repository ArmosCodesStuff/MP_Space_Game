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

**2026-09-24 (local session): R0 is green** -- rung 2, rung 3 on seeds 90331 and 4127, `-OneDll`,
`-ReplyWindow` (every delay to 60 s connected: `Link.ReplyWindowS` = 30, DESIGN.md). Three harness
faults fixed on the way (ledger J6). Owner rulings since: the Echo's Rewind goes back 8 s with hull,
from a 0.5 s snapshot ring; no boss-hull trim for adds (docs/plans/README.md). **Next:** the bar
(`verify.ps1 -Update`), a push to both branches, then R1 (network_webrtc.md §13), whose first edit
adds `Link.ReplyWindowS = 30` and the permanent reply-window check.

**2026-09-25 (cloud session): WebRTC slice R0's code landed** (tested since: above).
Ledger: `docs/plans/ledger_webrtc.md` (jobs J1-J6, decisions D1-D7, v1 of the plan is not in the repo).
`scripts/Link.cs` (plugin row, `Available`, `Channels()`, `Sealed`, `Hang`), SessionMenu's
plugin-missing gate, the harness's stream on channel 12, the R0 pair checks in the solo run, the
reply-window measurement (opt-in), `tools/import.ps1` and both runners calling it. The session is
still ENet: R2 switches it. The plugin is vendored in `addons/webrtc_native/` (9474f91: the
`.gdextension` trimmed to the two `windows.*.x86_64` lines and both DLLs, byte-identical to the
spike's); with all 7 `LICENSE.*` files: the plugin is fully vendored.
The runners refuse a `.gdextension` that names a file not vendored.

**2026-09-25 (cloud session, design only, no game code touched):** step 1 of `docs/plans/README.md` is
done. `kits_v31.md` and `numbers_curve_raids_items.md` are reconciled in the numbers file's §8: it owns
every curve, boss, raid, chip and item number; the kits file owns what each class does. L1 Lancer 3222 /
Drake 2968; non-super moves Lancer x0.744 / Drake x0.787; no starting chips; the Dart at 72; no hull trim
for adds. `docs/plans/models/numbers_v2.py` carries the rows (and now runs on Python 3.11); its output is
`models/run.txt`. Step 2, WebRTC slices R0-R5 (`docs/plans/network_webrtc.md`), is under way: R0 above.

**This folder is the `WarShips_Version_L` fork** (git branch `version-l`), copied from `Downloads\Warships`
at its last VERIFIED commit (b258590, slice 3b) so this work stays off `main`: nothing here is pushed,
and the original folder is untouched. Its first batch is the owner's line art -- every ship redrawn from
the owner's drawings, bombers that park on the carrier's deck and lift off and land like the hauler, the
escort's threat, boss sounds and damage numbers (Unreleased, the second section); its second makes the
ships bigger and the default grey and white (the first section). The owner asked for **one test run at
the end of each batch and no mutant runs** for the rest of that session.

**State as of 2026-09-23: the owner's FOURTH batch is all but built** -- the refit that costs a
level, the EXP rules and its own popup, the 1200 u warp, RAIDS as a second mission category with
the SIEGE in it, gear levelled with salvage, up to three targets at once, three more pilot
upgrades (REACH, COOLING, GUNNERY), a boss curve retuned to 1.025 a level, a soft tutorial, the
RECYCLER, the lanes with their couriers and outpost guns, and 104 new per-mechanism checks. Every item of it is
built.

**The THIRD batch, underneath it** -- nine new classes (three freighters
that deploy their own turrets, three heavy fighters, three lights), four new enemies in the wave
rotation, an always-on point-defence mount on the hauler, and the refactor underneath all of it:
a class, an ability, an enemy and a turret are now ROWS OF A TABLE, not code (`scripts/Ships.cs`,
`Abilities.cs`, `Enemies.cs`, `Turrets.cs`), with tags, statuses and targeting shared by everything
that fights (see Unreleased, first section). On top of the second batch -- the broadside, the
destroyer, the outposts, both bosses -- and the first -- the owner's line art, the carrier's deck,
the escort's threat, boss sounds, damage numbers, gear, loot, the hauler's runs, reconnection, the
tutorial -- and plug and play for any network.
Save format 2: `ShipClass` gained nine members, appended, so every existing character loads exactly
as it was. The last VERIFIED commit is the baseline; the map of every script, member, RPC, spawn
site, event hookup and asset is `version/MAP.md` (`python tools/map.py`, regenerated by
`verify.ps1 -Update`). The guesses made where the owner's answers left a gap are under Open
questions -- check those first.
**Latest change:** multiplayer slice S1 and the home batch landed together. S1: a guest is online
only once the host's welcome arrives and it has answered (`Net.NetWelcome`, `Net.NetWelcomed`,
`Net.ToHeard`), every ordered stream has its own channel and lives on Hub (`NetChannels`), a silent
peer is kept 8 s, and a goodbye -- or any hang-up of a live link -- drains up to 2 s so it survives a
lossy link. The home batch: a pilot shot down is back in 24 s (`PlayerShip.StasisTime`); the pirate
base has no guns, only one CRUISE MISSILE every 15 s any weapon can shoot down (`Tag.Hulled`,
`Shots.Cruise`), and its launcher never follows a dark pilot; the EQUIPMENT BASE right of the station
carries the recycler, and a part is levelled or scrapped only there, 10 s clear of combat
(`scripts/Landmarks.cs`); every beam is its own note and a quarter quieter (`scripts/Beam.cs`); the
couriers dock at the fleet's pads (`scripts/Docks.cs`); the practice range stands south of the base
(`Hub.PracticeTargets`); and every file loads through one holder (`scripts/Assets.cs`). Everything
earlier -- guest gear levels, sentries that fight, stealth that stops choosing but never hitting,
rate buffs that reach the guns, the sky and the Drake, the launcher's removal -- is under Unreleased,
newest first. THE PLANS for what comes next (the signed-off kits, level walls, the WebRTC switch, the
numbers, sprites, items) and the owner's rulings are in `docs/plans/README.md`. `PLAY.bat` ->
`play.ps1` -> `tools/install.ps1` puts the published build on a machine with nothing installed, and
`tools/pack.ps1` is the one command that makes a release; the five owner steps are in
`docs/README.md` under Releasing.

**Verification** is one command, `verify.ps1` (see `CLAUDE.md` §6 and `docs/README.md`): typecheck
against the real `GodotSharp.dll`, `dotnet build` with 0 warnings, analysers 0 findings, xref 0
unused, the smoke test three full runs in a row (**6 of 6 runs** each: solo, a host with
two guests, and a two-player arena; `tools\smoketest\run.ps1 -Wan` repeats the multiplayer runs
with every guest joining through a simulated internet -- 90 ms each way, jitter, 2% loss), and the
screenshot sweep on the developer's own GPU with the project's Forward+ renderer. This batch adds
`16b_courier_sharing_an_arm`, `16c_courier_at_an_outpost_clamp`, `67_siege_missile_warning`,
`67b_siege_cruise_missile`, `67c_siege_cruise_selected`, `73b_sniper_rail_flash`, `81_equipment_base`,
`81b_equipment_base_in_combat` and `81c_recycler`; the sweep draws 108 frames, `LINT: 0`. The smoke test builds its own network (fake routers, no internet), so every check means
the same on every machine -- there are no "environmental" failures to discount.
What none of it replaces: someone actually flying it, and a real session between two homes.

### What the game is right now

A Godot 4.7.2 C# game where your ship is your character. From the main menu you pick or create a
character (Terraria-style slots, each row showing the real ship in its colours), then sail it in a
persistent hub: a sun and asteroid belt above the base, a salvage wreck to the left, a wormhole to
the right, and **a practice range** south of the base — two target dummies and two practice fighters, clear of the hauler — for testing weapons. Capital ships
**handle like naval ships** — no strafing, a turning radius. **Twelve classes are flyable**: three of
the line (battleship, carrier, destroyer), three freighters that deploy their own turrets, three
heavy fighters and three lights. Every class action is an **ability** on a bar along the bottom, **remappable in the K
window**. The **battleship** aims four main guns with the cursor (salvo or staggered), fires a
**broadside** of all of them (F: a 0.5 s wind-up while they swing onto the cursor, three volleys, a 10 s
cooldown), and has activated point defence. Every ship is the owner's line art, tinted with the pilot's
hull and accent colours (grey and white by default). The **destroyer**, the fastest (130 u/s), aims two main
guns the same way and fires **missile bursts** (three guided missiles, the outer two curving in) from a
magazine it reloads with R. The **carrier** sends fighters
at a target, recalls them with R, launches bomber strikes of unguided torpedoes -- the bombers taking
off in turn from its deck and landing back on it -- and has activated point defence. The **TIO** offers boss missions -- RUSTY BUCKET on the odd levels, the DRAKE BASTION on the even
ones: everyone READY opens a portal to the boss's arena;
a failed mission brings the boss's raiders to your base. A kill drops each pilot its own **crates of
gear** -- 120 parts that each lean hard one way, fitted from the pilot's **hold** in the equipment
window (I). The **hauler** sells alone (DISPATCH, with the EVASION chance of getting through) or on
an **ESCORT** (round four outposts, offloading at each, hunted by a raider wave every 20 s, 5x the pay);
AUTO-SELL waits for the
level-3 boss and 4462 cr. A guest that **drops** is retried and let back into its place; and a new
pilot is shown **corner hints** the first time it meets each system. The **base** (B) runs an idle economy --
miners in the belt, salvagers at the wreck, a hauler that sells the load -- and buys upgrades; the
pilot levels up (L) and fits parts (I). **Multiplayer** is host-and-join over the internet: HOST
opens the port by itself where the network allows it (UPnP, NAT-PMP, PCP, a router behind a
router) and says in words what to forward where it cannot; a VPN or overlay address and IPv6 are
offered too.

### Controls (hub) — fixed keys

| Input | Does |
|---|---|
| W / S | ahead / astern (thrust along the keel only) |
| A / D | rudder. Turns on a radius; does nothing with no way on. No strafing. |
| Mouse | aims the main guns (battleship, destroyer). The hull does not follow it. |
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

Every class carries Space and G (main guns, salvo ↔ staggered) unless it flies craft instead, Q
for point defence if it has any, F for what the class is FOR, and six open hotkeys.

| Class | Space | G | F | Other |
|---|---|---|---|---|
| Battleship | main guns | fire mode | broadside (3 volleys, 14 s cooldown) | Q point defence |
| Destroyer | main guns | fire mode | missile burst (magazine of 3) | R reload (9 s), Q point defence |
| Carrier | fighters: attack | — | bomber strike | R recall, Q point defence |
| Freighter | main gun | fire mode | bubble (400 soaked, 8 s) | T deploy, C collect, Q point defence |
| Tender | main gun | fire mode | overdrive (x2 rate of fire, 8 s) | T deploy, C collect, Q point defence |
| Bastion | main gun | fire mode | shockwave (1000 u, or a boss held 3 s) | T deploy, C collect, Q point defence |
| Sniper | main gun | fire mode | railgun (3 s charge, locked, 150 at 2500 u) | |
| Warrior | main guns | fire mode | rush (2.5 s, then an EMP) | |
| Warden | main gun | fire mode | six hunter-seekers | point defence is always on |
| Dart | main gun | fire mode | barrel roll (1.2 s untouchable, then a boost) | |
| Echo | main gun | fire mode | echo (5 s remembered, then detonated) | |
| Wraith | main gun | fire mode | stealth (5 s unseen) | |

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

1. **Everything:** `powershell -ExecutionPolicy Bypass -File verify.ps1 -Update` -> must print
   `ALL CHECKS PASSED`. The smaller gears (`-Quick`, `-Fast`) and each step on its own are in
   `CLAUDE.md` §6. Linux: the `.sh` originals (`typecheck/typecheck.sh`, `tools/smoketest/run.sh`,
   `tools/screens/run.sh`) take the Godot binary as an argument.
2. **Add checks** to `tools/smoketest/SmokeTest.cs.txt`, and **prove each can fail**: put the real
   old code back in a scratch copy and watch the check fail.
3. **Look at it:** anything visual goes through the sweep (`%TEMP%\shots` on Windows); read the
   frames. The smoke test cannot see.
4. **Read build warnings.** Two in an earlier release were real bugs (hidden Godot members).

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
- The remote is `origin` (GitHub, branch `main`); local work is on `master` and pushed as
  `git push origin master:main` after a VERIFIED commit.
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

From the 2026-09-24 triage (the WarShips_Version_L fork):
- **A practice dummy is a turret's FALLBACK**, not "only when its owner tests on it": nothing the host
  holds says what a guest is testing on (a pilot's selection is local, and a turret's seed has no
  room for one), so that rule waits for turret orders. A dropped turret takes a dummy only while
  nothing that can die is in reach. Default: as built.
- **A sentry already on a heavy or a boss does not switch** when a light or a missile arrives: only
  acquisition ranks, as point defence always has. Default: as built.

From the line-art batch (the WarShips_Version_L fork):
- **"25% larger / smaller" was read as every dimension** (lengths 378, 283.5, 212.6 u). The battleship's
  turrets are 2.5x, not scaled with the rest alone, to keep the drawing's turret-to-hull proportion on the
  doubled beam. Parked bombers are 65% to fill the bigger deck.
- **The sprites are as sharp as the drawings allow**: they are small (250-600 px), so they are enlarged,
  sharpened and cleaned but not redrawn. A clean-line redraw was tried and made fine detail blotchy.
  Larger source drawings would make sharper ships.
- **Turrets wear the accent colour** (white by default), not the hull's -- the accent was already named
  "turrets, engines, trim", and on the line art a turret in the hull colour disappears into it.
- **Point defence "on the back"** is the stern quarters on the battleship and the destroyer; the carrier's
  third PD moved to its stern block, clear of the bow where bombers lift off.
- **Bombers take off on their own 0.83 s clock**, beside the fighters' rather than sharing it, and land
  without a queue (they come home about 0.83 s apart anyway).
- **The dummies' practice fighters stay white** (they share the light raider's art): they read as targets.
- **The heavy raider keeps its one turret**, now on the spine behind the canopy.
- **The escort threat's "something else" is the route** -- how far along the escort the wave comes -- so
  an escort builds to its end. "Player level and stats" is half the party's mean pilot level, half its
  ships' hull over their classes' own. A guest's level is its own claim, bounded to 1..100.
- **Damage numbers show on every hit that changes a hull**, the dummies' included; hits on one thing within
  0.3 s add into one figure.

From slice 3 of the 2026-09-22 batch:
- **"25% more threat" was read per special, against the Lancer's matching one**: a scrap piece is a
  trident missile's 15 x1.25 (18.75, each piece its own hit); the rock is 1.25 x the 200 a pilot standing
  in the Lancer's beam takes (250, to every ship in its lane); the warnings are the ram's 1.5 s and the
  beam's 6 s, x1.25 (1.875 and 7.5 s). All are `Drake.ThreatMult` times a Lancer constant.
- **The rock flies its whole lane**, striking every ship it passes, and breaks apart at the lane's end,
  not on the first ship -- so its path is fixed at the throw and needs no impact message to guests.
- **The Drake's main gun keeps firing through its specials** (as the Lancer's guns do); only moving and
  turning stop.
- **The shotgun keeps the trident's 15 s cadence but not its 13.5 s phase**: it comes at 17 s, 32, 47 ...
  because the first throw (6 s, and 9.1 s long) covered 13.5 s. A 2 s breather after each special
  instead would keep 13.5 s but drift the cadence; say if that is wanted.
- **The Lancer's beam waits for this cycle's escorts to have left the launch point** before a pin counts
  (a pilot still held by the last beam's escorts would otherwise skip the turn-to-face phase).

From slice 2 of the 2026-09-22 batch:
- **The offload is drawn, not paid.** A quarter of the load goes out at each outpost as the pods
  drain, but the x5 pay is on the whole load at the portal, as before. Paying per outpost would be a
  change to `Hauler.Payout` and the escort's loss rule.
- **"Every other wave has a heavy"** was read as alternating: waves 2, 4, 6 ... bring one (per patrol);
  waves 1, 3, 5 ... are three lights. Uncapped waves over a four-minute route are about twelve, so a
  pilot who ignores them faces a crowd -- by design, but worth flying.
- **The hauler docks 230 u in from each outpost**, on the base's side, so it never overlaps the station.
- **The fighters' pass now runs a turning diameter (~201 u) past a small target** as well as 1.2 of
  its diameters: the owner's "overshoot 1.2x" is kept as a minimum. Passes on small targets are longer,
  and they keep coming.

From slice 1 of the 2026-09-22 batch:
- **Close in, the burst's fan narrows.** At the full 70° the outer two missiles cannot come round onto a
  target nearer than ~200 u (they circle it until their run ends), so inside ~250 u the side angle is the
  widest that still lands (34° at 150 u). The alternative is a minimum burst range; say if so.
- **The broadside fires along the barrels as they point** at each volley, onto the cursor as it last
  reached the host -- so a pilot can sweep the volleys by moving the mouse. Locking the aim at the end of
  the wind-up is one line in `PlayerShip.TickAbilities` if that was meant.
- The destroyer's gun line is named **Twins** (Rapid, Heavy, Long, Tracking Twins) so a drop never reads
  like the battleship's Battery of the same lean; the kit is **Mk I Twin Turrets**.
- The title screen's battleship fires its broadside **every 5 s** (the class's is 10) so the scene shows it.

From the 2026-09-21 batch (each is a constant or one rule to change):
- **Crates nobody flew over are KEPT**: they are on the pilot's file from the kill and reach the hold
  the next time the pilot enters a world (the bounty-on-quit rule). If they should be lost instead:
  `Character.Unclaimed` and `Loot.ClaimAll` go, and four checks change.
- **Pickup is contact only** (the owner's "collected by flying over them"): no magnet. The crates lie
  on a 240 u ring where the boss died; a pilot leaves by pressing **RETURN** (below), not on a clock.
- **DISPATCH is the base owner's too**, like ESCORT: a lone run can now lose the load, so a visiting
  guest no longer sends the host's hauler (it used to be able to). For the same reason **AUTO-SELL is
  the owner's to buy** (it starts lone runs); a guest may still buy EVASION and the rest.
- **A bomber added by a refit arrives rearming**, not armed: swapping a bay part on and off must not
  reload a wing that has fired. (Fighters added by a refit are ready at once: they carry no ammunition.)
- **A lost lone run loses the cargo, not the hauler**: it comes back empty, CARGO LOST over it.
  EVASION: 60% safe, +7% a level, 5 levels (400-6400 cr), 95% at the cap.
- **The escort**: the route south round the TIO, east, then up to the portal (about 4400 u, ~75 s);
  4 waves, 5 s in then every 20 s, one patrol each (+1 per extra pilot) at the base owner's boss
  strength; a wave's hunters withdraw, not explode, when the hauler jumps or is lost.
- **A dropped pilot's place is held 90 s** by its character id; a boss killed meanwhile is owed to
  it -- its EXP, bounty share and crates arrive when it is back. It is retried at 2, 8 and 14 s,
  then RECONNECT. The boss is scaled by the ships present, not the held ones; the bounty is split
  among both (a held pilot's share is not the others' windfall).
- **Hints**: bottom-right, 8 s then a 1 s fade, one at a time. Beyond the owner's list, level-ups,
  warp, stasis and the arena boss have a hint each. The off switch is per character.
- **The escort shares the raid machinery** (waves hunting a quarry), not the TIO's mission flow: the
  "mission framework" planned as a foundation was not needed.

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
- **Dummy meters reset themselves** on the first hit after 5 s idle (R was needed for abilities).
- Still open from 0.2.0: fighters never engage on their own; Tab's "nearest" is measured from the
  ship; left-click on empty space keeps the target; Esc from the hub ends a hosted session unasked.

### Next up

- Fly the line art: the three hulls at the game's zoom, the bombers' deck, and the new sizes.
- Fly the second batch: the broadside, the destroyer, the escort's twelve waves, and both bosses.
- A real two-home session: everything multiplayer here is proved on one machine and through a
  simulated internet (`run.ps1 -Wan`), never yet between two houses.
- Balance with gear in play: the battleship out-damages the carrier about 1.6x on paper, and the
  hardest-leaning parts (Elite III, Swarm III, Glass III) have not been flown.
- `Hub.BeginPlacement` for the first non-instant ability; wormhole transit into an instanced system.

**The owner's batch of 2026-09-23, in their order** (decisions already taken are marked):

1. **A refit costs a LEVEL as well as the resources**, and undoes one purchase: the TRUE most
   recent one, which means the character file starts recording the ORDER points were spent in (a
   new field and a format bump; a file with no log falls back to the dearest point). The pilot
   loses the level AND the point -- the stat comes off the sheet -- but **keeps its progress toward
   the next level**: 560/1000 stays 560/1000.
2. **A boss awards 100 more EXP at the base**, and **none at all when the boss's level is under
   half the pilot's** (level 5 against a level 1 or 2 boss pays nothing).
3. **A pilot's warp reaches 1200 u, not 2000.**
4. **A distance-to-target readout** under the target's window: a small widget.
5. **An EXP popup with its own entry point and its own look** -- bold white, above the pilot's own
   ship -- asked for as a copy of the damage numbers. It gets its own function and its own row;
   what it does NOT get is a copied file, because a second floating readout is exactly the second
   of a thing the rules say to make a row of (one mechanism, a row each for damage and for EXP,
   differing in colour, weight, where it rises from and how long it lives).
6. **A boss's escorts stop belonging to the Lancer.** A boss row says what it calls in; a boss with
   escorts gains one more every 10 levels to a maximum of five, with formation places for five (or
   a queue, where the next waits for one of the others to die).
7. **A warp does not land a capital ship on top of the thing it is aimed at.**
8. **RAIDS: a second CATEGORY of Threat Intelligence Operations mission**, beside BOSS, with the
   siege as its first entry. A mission KIND is the new table (`MissionKind`); everything under it
   is rows. Raids share the boss ladder's unlocked levels and scale off the same selected level.
   - The crescent hull is the pirate base, in red and black; the other is a pylon.
   - Four pylons on an invisible X, 1200 u out on each diagonal, 400 hull each.
   - **The four pylons hold the base's shield up: every one must fall before the base can be hurt**,
     and the base wears a shield sprite on each side while they stand.
   - The base has twice the hull of that level's boss (level 3: about 1600).
   - Only the base drops loot.
   - **Waves come in as an escort's do** -- the same director, the same cadence -- but at FULL
     strength: none of the escort's reductions to damage or hull.

9. **GEAR IS LEVELLED WITH SALVAGE** -- the sink the loot loop is missing, and the first thing
   salvage buys directly other than a refit.
   - Each level adds **5%** to a part's passive benefits. **The drawbacks do not scale**: a Rapid
     Battery's faster guns improve, its softer shells stay exactly as printed.
   - **100 salvage for the first level, x1.25 compounding**, capped at **+200%** (40 levels).
     Level 20 is 34k salvage all in, level 40 is 3.01M (the figure first written here, 3.76M, was
     an arithmetic slip caught by the check) -- about an hour and about 84 hours of a full
     salvager fleet, so +100% is a session's goal and +200% is a save's.
   - **A level belongs to the part ID**, not to a copy: upgrade a Rapid Battery II and every one
     you own is that level. The hold stores counts by id, and instance ids do not exist anywhere
     in the save or on the wire.
   - Bought where a part is fitted (the EQUIPMENT window), paid from YOUR OWN base's salvage even
     while visiting another host -- the rule a refit already follows.

10. **UP TO THREE TARGETS AT ONCE, for the hulls big enough to hold them.** Ctrl + left-drag a box
    over enemies, or shift + left-click to add one at a time; clicking normally still picks one.
    - **How many a hull may hold is a ROW** (`ClassDef.Targets`, 1 by default and 3 on the
      capitals), so "capital ships only" is data rather than a list of class names in an `if`, and
      a thirteenth class states its own answer.
    - Default for what a set DOES, to be confirmed when it is built: anything that takes "the
      selected target" uses the FIRST of the set, and anything that can spread -- a wing's orders,
      a hunter volley, a missile burst -- divides across it.
13. **THREE MORE PILOT UPGRADES** beside Rudder, Hull, Engines and Weapons, chosen by the owner:
    - **REACH** -- +1% weapon range a level, read from the class row the way Weapons reads its
      damage. Needs `ClassDef.Reach` (the Targeting Chip wants it too: today that part lifts a
      hardcoded list of six stat ids and does nothing for seven of the nine new classes).
    - **COOLING** -- -0.5% on every ability's cooldown a level, capped so a long career is -30%
      and not free abilities. The only upgrade that touches what makes the nine classes differ.
    - **RATE OF FIRE** -- the main gun fires faster per level.
14. **THE BOSS CURVE PRESSES.** Tuned against a pilot who beats each level's boss TWICE before
    moving on: at that rate the boss should pull SLIGHTLY ahead, so a third clear or better gear is
    the answer to a wall. Measure first -- two clears' EXP, the points they buy and the gear those
    kills drop, against 10% hull and damage and 1% wind-up and reach -- and report the numbers
    before changing any of them.
15. **A BOSS'S PROJECTILES SCALE FASTER over time; A PIRATE'S DO NOT.** A raider scales in its own
    stats only and throws the same missile at level 40 as at level 1.
16. **A HEAVY FIGHTER'S MISSILE LANDS AFTER 12 s** (it is 7 today) and hits about 40% harder. That
    moves the prediction: the lead is computed from the flight, so a longer flight means a longer
    guess, and the anticipation code has to be checked against it rather than assumed.
17. **A SOFT TUTORIAL** on a pilot's first character: a layer with CONTINUE buttons, and a SKIP
    TUTORIAL button at the top right of the screen.
18. **MORE CHECKS, AND MORE VARIED ONES**, per mechanism rather than per scenario: minimum and
    maximum range for every class, every class ability exercised, and variation in the geometry
    each run draws (the harness already seeds its own -- this is about covering more of what the
    game has with it).

19. **THE LANES: couriers, blockades, and outpost guns.** The four outposts are positions today
    and nothing travels between them; this makes the base's passive income a thing you can SEE and
    a thing that can be cut.
    - **Small drones** -- the miner's and salvager's hulls at a fraction of the size, tinted to
      the station they belong to -- carry commodities between the base and each outpost, both ways.
    - **A lane is a row** (the new table): its outpost, its two ends, its courier, and the share of
      passive income it carries -- **25% each, four lanes**.
    - **A blockade is a wave row**: raiders sit on a lane and stop it. While a lane is blockaded
      its quarter of the income stops. Several can be cut at once, and the answer is the pilot.
    - **Each outpost has a gun, and the gun is upgradable** -- range, rate of fire, missile speed --
      through the economy table like every other upgrade. It fires the heavy fighter's own missile
      mechanism in friendly colours, which makes it a late-game deterrent rather than a decoration.

*(Item 11, the gear-line clarity pass, is built. Item 12, the dumb launcher, was built and then
deleted: `tools/install.ps1` does its job with nothing installed. See Unreleased. Nothing is
outstanding from the batch of 2026-09-23.)*

---

## Unreleased

### Level walls, job 4: Auto-sell and the lanes' blockades are rows of the unlock table (2026-09-25, lane C, `wt/walls`)

**The base's two boss gates fold into `Unlocks.All`** as rows measured by the base owner's highest boss
beaten (`Measure.Boss`): Auto-sell at boss 3 (`Opens.Economy`, `hauler_autosell`), blockades at boss 1
(`Opens.Raids`), asked through `Unlocks.Boss(what, id)`. `Economy.Upgrade.NeedsBoss`,
`Economy.AutoSellBoss` and `Lanes.NeedsBoss` are deleted; `Yard.TryBuy`, the base panel's LOCKED line and
`Raids`' lane clock read the table. Behaviour is unchanged.

**Checks:** `WallChecks` (new, rung 3): the two rows against the plan's literals (3, 1) and an unnamed row
open (0); the lanes' clock waits with no bounty boss beaten and runs with 1 and 4 beaten. The existing
Auto-sell checks (LOCKED until boss 3, the owner's record on a guest) now run through the table unchanged.

**Known broken:** none known; compiles (rungs 1-2), rung 3 not yet run.

### Level walls, job 2 (F15): six chip slots on every hull, the kind caps, no starting chips (2026-09-25, lane C, `wt/walls`)

**Every hull has six chip slots** (`Equipment.ChipSlots` 6), opened in order by the pilot's peak
(`Unlocks`: level 2, 4, 8, 10, 12, 14), with **at most 3 Combat and 3 Utility chips** (`ChipKind` on
`ItemDef`, `Equipment.KindCap`; the Combat Chip is Combat, Armour, Engine and Targeting are Utility).
**`chip_basic` is deleted and `Equipment.Default` fits no chips**: a stock ship is its class row (a
battleship 300 hull where it flew 375). `Equipment.Sanitize(c, ids, peak)` empties a chip in an unopened
slot or over its kind's cap, and `Bonuses` / `Adds` take the peak, so the host holds a guest's claim to
its own walls. `PlayerShip` keeps what is fitted whole and flies what its peak opens (`Loadout`), so a
slot opening mid-session applies its chip. `Character.Load` moves every chip it cannot fly into the hold
(never deleted). The equipment window shows six chip rows, a locked one reading `CHIP 4  ·  LOCKED · L10`
with no button; EQUIP uses the first open empty slot (`Equipment.ChipFit`) and greys out with why ("next
chip slot opens at level 10", "at most 3 Combat chips").

**Checks:** `ChipChecks` (new, rung 3): open slots at levels 1/2/4/8/14 = 0/1/2/3/6 through Sanitize and
the sheet, the Utility cap, `ChipFit`'s five answers, and files at levels 1/3/14 with five Combat chips
loading 0/1/3 fitted and the rest held, x battleship / warden / echo, plus a file with no peak. Solo
(rung 3): EQUIP and UNEQUIP of an Armour Chip I (+8% hull) in place of the five kit chips, and EQUIP greyed
with both reasons and a locked row at peak 8. Rewritten to the stock numbers (no chips): the creator card
(300), broadside 214.8, a shell 17.9, the refit's fraction (60/300, 108/540, 45/225), the destroyer 250,
the missiles 147, a torpedo 137.5, the carrier's 93.6 DPS reference, the worst-case stack (-37% / -45% /
-60%, chips counted three of a kind), hull points +5 / +10, the arena's 140 / 400 / 6. Rung 5 (new or
rewritten): the arena guest's two Armour Chips (290); Guesty's four Armour Chips held to three on the host
(248); the third player's five chips at peak 3 flying one on the host, on itself and on the other guest (324).
Shots: `58b_eq_chip_walls` (new, peak 4: two chips, four locked rows, a greyed EQUIP).

**Known broken:** none known; compiles (rungs 1-2), rungs 3, 4 and 5 not yet run.

### Level walls, job 1: the unlock table, the pilot's peak, save format 3 (2026-09-25, lane C, `wt/walls`)

**`scripts/Unlocks.cs` is the one table of what a pilot's level opens** (docs/plans/level_walls.md with
the owner's rulings): ability 1 at level 1, chip slot 1 at 2, ability 2 at 3, chip slot 2 at 4,
ability 3 at 6, chip slots 3-6 at 8, 10, 12 and 14 (`Unlocks.All`, queried by `At`, `Count`, `Top`).
**Walls read `Character.Peak`**, the highest level a pilot has ever reached: saved as
`[progress] peak`, loaded as max(peak, level), raised by `AddExp`, never lowered by a refit. The
identity carries it after the level; the host takes it through `Progression.Claim` (1-100, the cap a
claim's spending already had) and hands it to the ship (`PlayerShip.SetProgress(bought, peak)`,
`PlayerShip.Peak`). **`Game.Version` is 3**: format 2 files are listed greyed out and refused, with
nothing migrated or refunded (owner's ruling). Nothing reads the walls yet: chip slots (job 2) and
abilities (job 3) do.

**Checks:** `WallChecks` (new, rung 3): the table against the plan's literals; a level up, a refit and
a reload from levels 5, 13 and 2 keep the peak; files with no peak / a peak under the level / above it.
Rewritten: the save round trip and its field inventory (`Peak`), the build stamp (3), the handshake's
literal (3), fixtures written as this build's format, a format-2 file refused. Rung 5 (new): the host's
copy of a guest reads peak 14 at level 1. The harness roles and both Shots pilots fly with every wall
open (`Peak = Unlocks.Top`, level 1).

**Known broken:** none known; compiles (rung 1-2), rungs 3 and 5 not yet run.

### verify's text step skips a binary by what it holds, not by its extension (2026-09-24, in the WarShips_Version_L fork)

The text step skipped `.png`, `.ogg` and `.wav` by name, so the two vendored plugin DLLs (4 MB each)
were read as text, and `-Quick` ran for 28 minutes without finishing on the millions of matches.
A file with a NUL in its first 8000 bytes (git's own test) is now binary and skipped, whatever its
extension; git's `w/-text` is not used because it also calls a lone carriage return binary, the very
thing the step exists to catch. The step prints how many it skipped.

**Checks:** `-Quick` with an untracked probe holding a form feed and a lone carriage return: text
FAILED on exactly those two (`zz_ctrl_probe.txt:1 U+000C`, `U+000D`), 160 text files read, 148
binary skipped, 69 s.

### The WebRTC plugin inside Warships: slice R0 (2026-09-25, in the WarShips_Version_L fork)

**`scripts/Link.cs` is the one place that names WebRTC** (docs/plans/network_webrtc.md §7). The
plugin is a row (`Link.Plugin`: webrtc-native 1.2.1, `WebRTCLibPeerConnection`, its extension file and
release DLL); `Link.Available` asks whether that class exists, never a return code (a missing DLL
still answers Ok, SPIKE F4); `Link.Channels()` is the channel table a session negotiates, read off
every `[Rpc]` in the build (a channel whose RPCs all ask for one unreliable mode gets it, anything
else is Reliable); `Link.Sealed` says a bundle is final one poll after gathering is Complete;
`Link.Hang` is the one way to hang up (a gone id is a no-op, not an engine ERROR). The session itself
is still ENet until R2. **Without the plugin, HOST and JOIN are shut** (button, Enter, handler) and
the multiplayer panel says which file is missing, that an antivirus may have quarantined it, and that
PLAY.bat puts it back; offline play is untouched. The harness's own stream moved from channel 200 to
12 (`NetChannels.BossSounds + 1`), inside the channels WebRTC opens. **`tools/import.ps1`** imports a
folder and judges it by what it registered (SPIKE F1's crash-at-exit on the first import is imported
again, not failed); both runners call it and refuse to run without `addons/webrtc_native`.
`run.ps1 -ReplyWindow` measures the invite reply window once; `run.ps1 -OneDll` runs on the release
DLL alone.

**Checks:** new in the solo role -- the plugin loaded; 12 negotiated channels above 0 (Cosmetic
Reliable, 11 ordered, the stream last); CreateServer Connected in the same call; an in-process pair
connects, the host offering; 15 data channels open at each end, none with a packet lifetime; a
packet on channel 12 arrives on it both ways; Close and DisconnectPeer seen within 1 s; `Link.Hang`
on a gone id and on a pending entry; every handler on the main thread; three connect-and-close cycles
leave the object count flat; the plugin-missing gate shuts HOST, JOIN and Enter and opens again; with
`-ReplyWindow`, the measured window is at least 15 s (it is 30: DESIGN.md). Rewritten on the way:
the pending-entry check never hands its guest the invite (freed mid-DTLS-handshake, the plugin
printed two ERROR lines in one run of two); the Warden's Hunters case downs its own seekers, and the
Echo's blast check reads the marks after its wait and prints the missiles in flight (seed 90331 read
136 where 80 was stored). Rung 2 green; rung 3 green on seeds 90331 and 4127, `-OneDll` and
`-ReplyWindow`.

**Known broken (R0):**
- **The in-process pair missed its 5 s once**, in the third run of a bar (six engines at once); it
  connects in 11-14 ms everywhere else, the failing seed included, and in three rung-5 runs. Not
  understood. The check now says which step a stalled pair stopped at (`RtcPair.Stage`), so the
  next one explains itself.
- **A real guest freed mid-handshake prints two plugin ERROR lines** (DESIGN.md traps): the harness
  sidesteps it; R2's guest role will meet it when an invite is hung up.
- **The plugin-missing text is this batch's own** (plan v1 §6.1's literal was not in the repo); the
  one-DLL experiment is read as "the editor loads the release DLL" (v1 §8.1 unread). Ledger D1-D3.
- **The Linux runners** (`run.sh`) have no plugin (it is vendored for Windows x86_64 only), so the
  WebRTC checks fail there.

### "Handle is not initialized" after DONE: every file is held by Assets (2026-09-24, in the WarShips_Version_L fork)

**Every file the game loads comes through `Assets.Load`** (`scripts/Assets.cs`), which keeps each one's
C# object for the life of the process. The two `Handle is not initialized` ERRORs in a solo run that
passed every check (and in an arena guest's) were Godot swapping the GC handle of a texture whose C#
object the collector had already taken: the same path was being loaded again from the engine's
cache while that object's finalizer ran on its own thread -- a backdrop across a scene change, a
raider's hull between waves. They only looked like part of the quit: `run.ps1` prints a role's stderr after all of
its stdout. Replaced: a `GD.Load` at every call site, Sfx's own table of streams (and its disposal at
quit -- the engine disposes every live C# object when it shuts C# down, before it counts resources
still in use), and the ship preview's texture cache. Music's two loops, which the engine's cache never
sees, come through `Assets.Own`.

**Checks:** new in the solo role -- the engine's loader is called from Assets alone, read from the
build's IL with the harness in it, and the scan must find Assets' own calls; and the title screen's
foe, its last sprite gone after the scene changes of the leak check, keeps the same C# object through a
full collection and is handed out again as it. The harness's own three `GD.Load`s go through Assets,
and an unused one (`bomb0`) is gone. The ERROR itself comes and goes, so only clean engine runs show it
gone: compiles, rung 3 and rung 5 owed.

### A pilot shot down is back in 24 s (2026-09-24, in the WarShips_Version_L fork)

**Stasis is 24 s** (the owner's ruling). At 0 hull the ship lies in stasis for `PlayerShip.StasisTime`,
24 s, and then F re-boards it at a third of its hull. While a partner is still flying, the mission goes
on without the pilot; the whole party in stasis at once still fails it.

**Checks:** rewritten in the solo role -- a ship shot down reads 0:24 of stasis. New in the solo role --
in the Drake's arena with a second ship in the party (made by `Hub.SpawnFor`, the door a guest's ship
comes through), a pilot shot down is still waiting on every frame short of 24 s of its own clock, is
ready on the first frame at it, is refused by F at 20 s, re-boards at 24 s, and the mission never
begins to fail; the whole party in stasis still fails it. No guest role needs a check of its own: a
guest's countdown is the host's figure, sent with its hull. None of it has run on an engine yet: rung
3 (two seeds) is owed.

### The pirate base answers with one cruise missile every 15 s, and anything a pilot carries can shoot it down (2026-09-24, in the WarShips_Version_L fork)

**The siege's damage is its garrison's.** The base's four guns and every pylon's gun are gone -- their
rows deleted: `EmplacementDef.Gun` is one mount at the centre or null, and `Guns`, `GunRing`,
`DamageSource.BaseGun` and `PylonGun` no longer exist. What the base adds is a **cruise missile every
15 s** at the nearest pilot within 4500 u: a red lane to that pilot 1.5 s ahead, riding the base so it
goes down with it, then a slow (120 u/s), guided (1.2 rad/s) round three times a seeker's size,
leaving down its lane (the barrel turns at 2.51 rad/s, so it comes round from any bearing inside the
warning; it follows the pilot the warning went up for while it can see it, and a pilot gone dark leaves
it on the last point it was seen at -- aiming is choosing -- so the round leaves down that line and flies
on straight while the pilot stays dark) -- 126 damage on the level's and party's scale
(the curve's 8.4 a second for the base, over 15 s) and 30 hull on the level's alone, because it is
fired at one pilot. The clock runs down whether or not anyone is in reach and waits loaded; an EMP's
hold raises no warning and spends one already up without its shot. The waves are unchanged, and so
are the base's and the pylons' hulls: the curve's rows for them wait for its sign-off.

**A missile a gun can hit is a mechanism: `Tag.Hulled`.** A `Shots.All` row may give its body a hull
(`ShotDef.Tags`, the launcher's `TurretSpec.Hull`, `Combat.Fire(hull:)`). Such a body is not a
`Missile`: a shell strikes it; a railgun, an EMP's blow, an echo, a seeker and a carrier's wing each
take their own damage off it (an EMP's hold has nothing to hold: a body in flight carries no statuses);
Tab and a click pick it by name (CRUISE MISSILE); point defence and a dropped turret take it before a
light craft and wear it down a shot at a time. The one rule it shares with a missile: nothing throws it (`Targeting.Throwable`, now the
shockwave's filter). A launcher's warning is `Turret.Warn`, and a round's homing, size, hull and
wind-up are fields on `TurretSpec`, all 0 on every gun a pilot carries.

**A won mission takes its hostile bodies out of the air** on every peer (`Hub.MissionCleared`), so a
missile fired before the base fell does not land on a pilot out collecting; a boss's seekers go too,
and none of them counts as shot down.

**Checks:** new in the solo role -- the launcher's row against literals (one mount on the base, none
on a pylon, 15 s, 1.5 s, 120 u/s, 4500 u, 1.2 rad/s, size 3, 126 and 30 on their scales, "base:missile",
a barrel that comes round inside the warning) and the cruise row (at the pilots, guided, an id,
`Tag.Hulled`); a Hulled body against a seeker at three seeded spots, the shells from a seeded side
(point defence ranks it first, a blow, a wing and Tab may have it, a throw may not; a shell takes 10
of its 30 and passes the seeker; 19 more and it stands, 1 more and it falls, where the seeker falls to
0.5); the hauler's always-on mount wears a hull of 3 down a shot at a time, from three seeded
bearings; at three seeded bearings each, the barrel turned square away from the pilot as the clock
comes due still sends the round within 0.02 rad of the pilot 1.5 s after its lane, and a pilot gone
dark for 3 s and moved 300 u off the line once the lane is up is fired at down the line to where it
was last seen, the round never turning while it stays dark; an EMP's hold of 3 s with the clock due
raises no lane and fires nothing, the warning goes up as it lets go, a hold part way through a warning
spends it with no round, and the next warning goes up 15 s after the spent one; a battleship parked
in reach for one minute at a seeded bearing, distance and heading -- nothing launched but cruise
missiles, the first within one interval and one warning, 15 s apart, each 1.5 s after a lane riding
the base out to the pilot and leaving down it, flying 120 u/s, the first landing for 126 x the
level-3 scale, the second shot down by the main guns with point defence off; every weapon a pilot
carries against a still cruise missile of 30 hull at a seeded spot, the hull coming off by that
weapon's own damage -- the railgun, the warrior's EMP, the echo, the warden's seekers (every one of
them on it), a carrier's wing sent at a raider that dies and taking the missile for itself, and a
dropped turret and a battleship's point defence each picking it before a nearer light raider; the
base's fall takes a missile in the air and a warning up with it, counted as nothing shot down; a
Lancer's seeker in the air at the kill is down and out of the hostiles 0.6 s later, counted as
nothing shot down. Rewritten in the solo role -- seven kinds of shot, not six; the shockwave throws
neither a seeker nor a cruise missile; the siege's parking spot is 5400 u out, beyond the
launcher's reach. New in the arena roles -- the host fires the base's round at the guest, which
stands half its hull and falls to the other half; the guest sees it as the base's round, aimed at
itself, in its hostiles and pickable, and sees it fall before the win; and a siege at level 1
between the two bounties, the guest parked in the launcher's reach: the host makes the clock due,
lets the warning and its round go at the guest, raises the next warning and brings the base down
with it up and the round in the air, and the guest sees every lane as a child of the pirate base
with its NetId and, within 0.6 s of the win, no lane and no cruise missile left. Rewritten in the
arena guest's role -- its level, EXP and credits after the second bounty count the siege's clear
(level 4 with 50 EXP, 12345 + 1500 + 1500 + 1537.5 credits). Three new sweep frames,
`67_siege_missile_warning`, `67b_siege_cruise_missile` and `67c_siege_cruise_selected` (the missile
picked, named on the target line). None of it has run on an engine yet: rung 3 (two seeds), rung 4
(read the three frames: the siege has never been in the sweep) and rung 5 are owed.

### The equipment base, with the recycler on it, behind a 10 s combat lock (2026-09-24, in the WarShips_Version_L fork)

**The equipment is a base of its own.** It stands right of the station, at (720, -60): a 320 x 240 u pad
with a band of caution tape across its centre and a beacon at each corner, labelled EQUIPMENT BASE and
on the scope in place of RECYCLER. A click on it opens EQUIPMENT, whose title line has RECYCLER; the
recycler is on it now, not a spot beside the TIO with nothing drawn there, and its window sits in the
side spot instead of stretching off the left of the screen. The pad's rim is green under a ship it will
serve and amber under one still in combat.

**A part is levelled, or queued for scrap, only there, and only 10 s after the ship last dealt or took
damage.** One rule, `Landmarks.Serves(ship, service)`, is asked twice: by the buttons, which grey
themselves with the reason on the window ("fly onto the EQUIPMENT BASE." / "in combat, N s more."), and
again by `Yard.BuyGearLevel` and `Yard.QueueScrap` before anything is spent. The windows still open
anywhere to look; away from home EQUIPMENT has no RECYCLER button and no reason line, because there is
no yard to spend from. FIT, EQUIP, UNEQUIP, LOCK and TAKE BACK are not gated. The 10 s is read off the
existing combat clock (`PlayerShip.CalmFor`), so "out of combat" for regeneration and music is still 12 s.
The lock is the host's word: a guest reads the host's combat clock, so it is refused on the host's
figure.

**The places at home are rows** (`scripts/Landmarks.cs`): the base, the TIO, the equipment base, the four
outposts, and the portal, the salvage field and the belt. Each row holds its place, footprint, art,
label, scope mark, click, services and docks, replacing four hand-written copies in `Hub` (positions,
sprites, scope rows, click tests); `Hub.ScopeMarks` keeps only the marks that come and go. Every
building's click is now its footprint -- the base's a 185 u square where it was a 200 u circle. A
station's docks are its row's (`Landmark.Docks`), reached by the landmark's id, and the rows are two
groups named for the art they are measured on, `Docks.BaseArms` and `Docks.OutpostClamps`. Deleted with them: `Hub.RecyclerPos`,
`RecyclerOpen`, `ToggleRecycler` and `_tioSprite`.

**Checks:** new in the solo role -- the equipment base's place against literals (right of the station,
clear of its 262 u, drawn, on the scope, the one place that serves, the lock 10 s and under the clock's
12); over it and calm it levels a part and queues and takes back a part, at three seeded spots with
seeded headings and seeded calm; a real blow from a seeded side shuts it at once and still at 9.9 s with
nothing spent, and it serves again at 10.1 s, three times, and the lock runs out on its own clock; on
the pad's edge it serves and 2-12 u off it refuses, at 8 seeded spots either side, and the old
recycler spot refuses; a click on the pad opens EQUIPMENT with SALVAGE live, grey in a fight and off
the pad with the reason, RECYCLER hands the spot to a recycler that stands on the screen (the anchoring
bug) with QUEUE grey off the pad and live on it; in the siege's arena, whose coordinates run under
the pad, a calm ship at a seeded spot on the pad's footprint is refused, and EQUIPMENT opens there with
no RECYCLER and an empty foot line. Rewritten in the solo role -- the recycler's checks and the level
bought in the window run on the pad, calm (the level at a seeded spot on it); the scope's ten marks
name EQUIPMENT BASE; the docks check proves the base carries the five arms, each outpost the four
clamps, and no other place any. Rewritten in the guest role -- the mid-session level is bought on the
pad, calm, for the one call.
New in the third player's role -- set on the pad just after the host hit it, it is refused for combat
on the host's figure. Three new sweep frames, `81_equipment_base`, `81b_equipment_base_in_combat`
and `81c_recycler`; 55 and 58 now show RECYCLER, the grey SALVAGE buttons and the reason.
None of it has run on an engine yet: rung 3 (two seeds), rung 4 (read the three frames) and rung 5
are owed.

### Every beam is its own note, and a quarter quieter (2026-09-24, in the WarShips_Version_L fork)

**Six beams were one buzz at one note.** Every flash in the game -- a warship's point defence, a
freighter's dropped turret, the hauler's mount, the base's laser and both kinds of raider -- played
`laser_light` at 1.00. Each is now its own note on that file: your side climbs above point defence
a whole tone at a time (base 1.12, dropped turret 1.26, hauler 1.41) and the raiders fall below it
a minor third at a time (light 0.84, heavy 0.71). A carrier's fighters keep their own file at 1.00;
a boss's bolt keeps `laser_boss` at 0.90, and a sniper's railgun sits four semitones under it at
0.71. Every pair on one file is at least a semitone apart. Each beam is rate-limited under its own
id, not its file's, so a raider firing never silences your point defence on the same file.

**A beam is a row** (`Beam.All`, `scripts/Beam.cs`): its id, the colour its flash is drawn in, its
report and its pitch. `Combat.Flash(a, b, beam)` names the row; `EnemyDef.Beam`, `WingDef.Beam`,
`TurretSpec.Beam` (0 is point defence) and `BossMove.Beam` say which, and the base's laser and the
railgun name theirs where they fire. The wire (`Hub.NetFlash`) carries the row's index and no
colour. Every colour is the one that beam was always drawn in. Deleted: `ShotSound` and its
switch, `Sfx.Laser` (now `Sfx.Beam`), `WingDef.ShotTint`/`Report`, `BossMove.Flash`, and the
colour literals at the five call sites.

**Beams are 25% quieter, in the files.** `laser_light`, `laser_fighter`, `laser_boss` and the
`laser_hit` crackle were rewritten at 0.75 by `tools/gain.ps1`; the death beam's charge and burn
and the Drake's tractor are made at 0.75 of their old level by `BEAM` in `tools/make_sounds.py`.
No setting, bus or trim was added and nothing saved moved: the master and music settings are as
they were. The other eleven sound files are byte-for-byte what they were.

**The railgun was silent on every guest.** Its report was played inside `FireRail`, which runs on
the host alone. It now goes through `Combat.Flash` like every beam, so every peer hears it; the
one thing drawn differently is a 2 px, 0.1 s flash line in the rail's own colour inside its 7 px
bar.

**Checks:** rewritten in the solo role -- the five "sounds played" checks count by beam id
(point defence, the boss's bolt, the fighters) and a new one hears the sniper's own railgun
through `Combat.Flash`; the Lancer guns' row names the bolt. New in the solo role -- every beam's
id, tint, file and pitch pinned to literals row by row (the check that plays every row reads the
table this proves), no two on a file under a semitone apart, and who fires
which (the six enemies, a warship's PD, a dropped turret, the hauler); every row, fired from a
varied spot down a varied line, counted once under its id on a voice at its file, its pitch, the
camera's own level and the master bus; every beam file at 0.75 of its shipped peak (literals
measured at 242b1aa) and the eleven others unmoved. New in the guest role -- a guest hears its
fighters' beams under the fighter's own row, sent by index. New in the arena roles -- the host flies a
sniper and fires the real railgun 240-600 u from the guest, and the guest's count of "rail" rises
from what it had before it joined. One new sweep frame, `73b_sniper_rail_flash`: the 2 px flash line
inside the rail's bar, snapped inside its 0.1 s. None of it has run on an engine yet: rung 3 (two
seeds), rung 4 (read the frame) and rung 5 are owed.

### Couriers dock at the fleet's own pads (2026-09-24, in the WarShips_Version_L fork)

**They stopped short of everything.** A lane's courier turned round on the straight line between
the base and its outpost, 300 u off the base's centre and 110 u off the outpost's: open space, 122
u from the nearest service arm and pointing down the lane. It now DOCKS: at the base, in the
service arm nearest its outpost -- the arm a miner unloads in -- and at the outpost, nose-in to the
clamp on the corner facing the base. Each half of its round trip is 2 s on the dock it is leaving
(`LaneDef.Dwell`), then the flight, eased at both ends.

**A dock is a row** (`scripts/Docks.cs`, new): the base's five arms moved out of `Yard.Arms`
unchanged and in the wire's order, and each outpost's four corner clamps were measured off
`outpost.png` beside them. A miner and a courier find their spot by the one `Dock.Berth`, their pad
by the one `Docks.Nearest`, and swing nose-in by the one `Docks.NoseIn`. `Yard.UnloadSpot`,
`Lanes.Run`, `Lanes.BaseDock`, `Lanes.Dock` and `Lanes.Cycle` are gone. The Yard still reserves arms
for the fleet; a courier reserves nothing, so it shares an arm with whatever miner holds it, berthed
at the outboard end of the face beside it (their wingtips overlap by about 3 u, the miner drawn
over). Couriers now draw over the stations (z 3), under the fleet.

**Checks:** 3 new in the solo role -- the docks against literals (the five arms in the wire's
order, an outpost's four clamps, each lane's two end docks); every courier on all four bearings,
started a seeded distance out and seen flying, ends within 50 u of its pad's centre with its nose
within 10 u of the open face and pointing in within 5 degrees; a courier and a miner share one arm
side by side, the miner keeping it. Changed: "every lane has its couriers" drops its turn-round
clause, the both-ways check ends on the two berths, the arms loop reads the moved fields, "never
two ships on one arm" says what it proves (never two of the fleet holding one), and the practice
range's 500 u lane clearance also measures every courier's berth-to-berth flight. Two new sweep
frames, `16b_courier_sharing_an_arm` and `16c_courier_at_an_outpost_clamp`. None of it
has run on an engine yet: rung 3 (two seeds) and rung 4 (read both frames) are owed.

### The practice range stands south of the base, on its line, out of the hauler's way (2026-09-24, in the WarShips_Version_L fork)

- **Where it was.** The two target dummies and the two practice fighters stood where the escort's last leg
  (south-west outpost to the portal) ran 2 u from dummy 1 and 11 u from fighter 4.
- **What that cost.** The lone run passed 309 u from fighter 4, inside the hauler's own 400 u point
  defence, which takes any light craft. So every run shot the practice fighters and moved their meters. And a
  hunter's 90 u blast, aimed 12 s ahead of the hauler, could land on a pilot practising there.
- **Where it is now.** The range is centred on x = 0, 1528-1850 u south:
  - dummy 1 at (-150, 1670);
  - fighters 4 and 5 at (112, 1528) and (188, 1572);
  - the armed dummy 3 at (150, 1850).

  Every target is 545 u or more from every leg the hauler flies and 979 u or more from every lane. On the
  base's line nothing nearer is 500 u clear of both the lone run and the last leg.
- **One table.** The range is `Hub.PracticeTargets` (number, spot, armed, fighter). It replaced three anchor
  points and a second list in `Hub._Ready` that armed whichever target was number 3.
- **Two new checks.**
  - The layout against the table, with the 500 u / 1200 u / 50 u / 800 u literals.
  - The hauler on its last leg, at the nearest point, taking nothing of the range, while a fighter brought
    inside 400 u is taken.
- **The point-defence checks moved with it** park at a seeded spot within 20 u of (-120, 150) off the two
  practice fighters -- 172 u or more from the armed dummy -- and the carrier's third light target is placed
  off the parked hull, not off a fixed point.
- **Sweep frames re-anchored on the range**: 4, 7 and 8 stand beside dummy 1 as before; 25 and 26 start
  3465 u off it as before; 44 is framed lower and wider (0.2) so the range, the belt and all four outposts
  are in it, clear of the hull bar.

### A guest is online once the host has let it in, a stall is not a drop, and a guest's HOST still says goodbye (2026-09-24, in the WarShips_Version_L fork)

The first slice (S1) of the multiplayer redesign: the protocol fixes P1, P2, P11 and P13 and the
failure wording, before any new way in.

**A joining player counted as online -- and as the host -- before anyone had let it in.**
`Net.IsOnline` read the ENet link, which calls itself connected before Godot's handshake has let
either end in. For that stretch the joining player's own world broadcast as a host into a host
that threw it all away with an engine error ("SYS_COMMAND_AUTH"), and its panel read `hosting · 1`.
`IsOnline` now needs a session and a finished join. A guest's join finishes at the host's WELCOME
(`Net.NetWelcome`, the first thing a host sends a guest it has let in), not at the handshake: each
end lets the other in on hearing its "done", so a guest's first ship report, sent unreliably right
beside its own "done", could overtake it on a link that reorders. From the handshake to the welcome
a guest is `Connecting` (panel "connecting…", HUD CONNECTING) and sends nothing; on the welcome it
introduces itself (`Net.Admitted` -> `Hub.OnAdmitted`: where it is, and who). Gone with it: the
introduction's two repeats at 1 s and 3 s, which covered for the host throwing the first away;
`SendIdentity`'s guard against a guest mid-handshake; and the `-Wan` smoke run's pass for
`SYS_COMMAND_AUTH` lines, which now fail the run like any engine error.

**A stall is not a drop.** A silent peer is kept 8 s (`Net.QuietMs`), not 4. ENet's resends run out
in about a second on a fast link, so the minimum was the longest stall either machine could survive:
a first scene load, a garbage-collection pause or a Wi-Fi roam ended the session. The maxima stay
(10 s on the host, 12 s on a guest), and a try to get back in is kept for its whole 5 s.
**ENet's throttle never falls**: every live link, at both ends, is set to interval 5000 ms,
acceleration 2, deceleration 0 (`Net.Link`). At its defaults it threw unreliable packets -- every
ship, raider, boss and base report -- away before sending them whenever a round trip came back
slower than the last few.

**A guest that pressed HOST cut its own goodbye off**, so the host it left held its place as a drop,
or noticed it go only by the timeout. `Host` lets the previous socket go at once only when it was a
host's: that one holds the port about to be bound. **A goodbye is given 2 s, not 1, and keeps its own
time**: it is two reliable packets in turn (NetBye, then ENet's disconnect once NetBye is
acknowledged), and over the internet one lost packet costs up to half a second of resend; a pilot
that pressed HOST and quit a second later also cut it off, because ending a session let any earlier
goodbye go. Every goodbye now pumps until done or its 2 s, and a quitting game waits for them.
**Every link that is up is let go that way, goodbye or not**: a player refused as another build hung
up the moment it read the host's build, and when its own had been lost on the way it was never
resent, so the host never knew it had refused anyone. **Net lets go of the tree's multiplayer when it
leaves** (invariant A): its handlers, its handshake callback and its peers had been left to the
engine's teardown.

**A guest just let in is sent nothing unreliable until it answers the welcome** (`Net.NetWelcomed`,
`Net.ToHeard`). Godot lets a guest in on the guest's "done", which can leave before the HOST's "done"
has reached it; when the host's was lost, the host's broadcasts and the other guests' reports it passed
on reached a guest that was still waiting, and Godot threw them away with an engine error
("SYS_COMMAND_AUTH", 27 lines on one join over the simulated internet). The welcome travels behind the
host's "done", so the answer proves it arrived. The host's ship, host-state and shield reports go
only to guests that have answered; a world's own traffic already waited for a guest's report of its
world. A guest's own ship report now goes to the host, which passes it on to the others that have
answered (`Hub.NetShipState` names its owner): Godot's own relay passed it on to every guest at once.

**The words.** A typed address that never answers: "No answer from {address} in 12 s. An address
works on the same network or over Radmin VPN, and the host must allow Warships through Windows
Firewall; for friends elsewhere, use the host's room code. Playing offline." It used to send a friend
elsewhere to the host's router advice. Another build: "...you both need the same release (Esc menu,
bottom)." -- not "the same zip". A name with only an IPv6 address is dialled on it (`Net.DialAddress`);
it read as "Could not find".

**Checks.** Solo: the no-answer text, word for word; a looked-up name is dialled on its IPv4 address,
else its IPv6 one, never a link-local one; every `UnreliableOrdered` RPC names a row of `NetChannels`
that no other RPC uses and is the Hub's, the rows are distinct and inside the 255 channels a session
opens. Host: each guest's link keeps every unreliable packet
(throttle 5000/2/0); a guest leaving by pressing HOST is heard going under 3 s after its last report,
on either link (a goodbye is given 2 s; one cut off waits out the 8 s timeout); neither guest had answered the welcome at the moment it was let in, and both have once
they are here; the host's fifteen seconds before it closes now
also wait for the third player to have gone. Guest: its own end of the link reads 5000/2/0 the moment
the handshake is done, before the host's settings can arrive; a 10 Hz unreliable stream from the host
(the harness's own, on a channel of its own) arrives at 8 a second or more over 12 s -- the link
alone loses at most 5%; no frame of its join is online or reads
hosting; it waits for the host to close rather than a fixed 6 s, and reads its own world's portal
after that world's next tick, not in the frame the hang-up lands. Third player: the other-build
refusal, word for word; the same frame-by-frame watch over its three joins; pressing HOST is leaving
on purpose. Arena pair: after the guest's return each world freezes for 6 s in turn and the other
keeps it -- 5 s or more of silence heard, the same peer, no leave -- and the arena guest's joins are
watched frame by frame.

**Every ordered unreliable stream has a transfer channel of its own** (`NetChannels`, in Net.cs: a
table of rows, read by each `[Rpc(... TransferChannel = NetChannels.X)]`; a new stream is a row). Such
a packet is delivered only if nothing sent after it on its channel arrived first, and ship, raider,
hull, shield, flash, dummy, base and boss reports all shared channel 0: over the simulated internet,
which reorders datagrams sent milliseconds apart, a base report lost to a ship report that overtook
it made a guest see the hauler's outpost hold start seconds late, on some runs and not others. The
shells' reliable channel is the table's first row. ENet already opens 255 channels at both ends.
**Every such stream is the Hub's** (`Hub.NetBase` for the base, `Hub.NetBoss` and `NetBossSound` for
the boss), handed on to its node or dropped: the base exists only at home and the boss only in the
arena, and with no newer report on a shared channel to throw a late one away, the base's last report
before the party left reached a guest already in the arena ("Node not found: Hub/Yard").

**The title battleship comes back on station after a dodge.** A dodge lands it at rest with its bow
pointing away from where it stood, and `Autopilot.Capital` with its mark astern crawls round the
turning circle at a tenth of its thrust: over 20 s from 250 u, longer than the 15 s between area
shots, so after its first dodge the title ship was never on station again. The menu's pilot now
comes round at the ship's own rudder limit whenever home is more than 0.35 rad off the bow (where the
autopilot's rudder is hard over anyway), then hands it to the autopilot (`MainMenu.ComeRound`). Its
station check used to push the ship off with whatever heading the scene had left, so it passed or
failed on the draw; it now pushes it at rest with the bow 2.3-3.0 rad off home, in any direction.

The session fingerprint changes (a new RPC): builds before this one refuse to join it. Save format
stays 2. Compiled and statically checked (rungs 1-2); the engine rungs are still to run.

### A guest's gear levels are its own on the host; a level refits the ship at once (2026-09-24, in the WarShips_Version_L fork)

**A guest's salvage bought it nothing online.** A sheet lifted every part by `Character.GearLevel` --
the levels of the pilot at THAT keyboard -- so on a host a guest's ship was lifted by the host's
levels for the same part ids, and the host's replicated hull said so on the guest's own screen. The
levels now travel with the identity (`NetIdentity` gains `gearIds, gearLevels`), are sanitised on
arrival (`Equipment.SanitizeLevels`: parts this build knows, 1-40) and are kept on
`PlayerInfo.GearLevel` and the ship (`PlayerShip.Levels`). `Equipment.Bonuses`/`Adds` take the
levels to lift by; `LevelOf` is only the local pilot's, for its own window, recycler and prices.

**A level refitted nothing, even offline.** `SetEquipment` refitted only for a changed part, so a
level bought in the window lifted the ship at the next refit of any kind. It compares the levels too.

**One sanitiser for levels.** `Character.Load` clamped `gear_level` on its own, keeping unknown ids
and skipping `Equipment.Migrated`; it goes through `SanitizeLevels` now, so a rack line that moved to
the destroyer keeps the level paid for it.

The session fingerprint changes: builds before this one refuse to join it. Save format stays 2.

### A turret left standing shoots anything hostile, and no gun holds what has left the world (2026-09-24, in the WarShips_Version_L fork)

**A freighter's turrets take a boss, a heavy and a station.** They picked like point defence, so
they took only missiles and small craft and brought nothing to a boss fight. A gun now names what
it may pick (`TurretSpec.Prey`); a dropped turret's is `Targeting.Sentry`: anything hostile,
missiles and small craft first, then the nearest of the rest. A practice dummy is a FALLBACK
(`TargetFilter.Fallback`): taken only while nothing that can die is in reach and never held over
anything that comes in -- drop turrets beside a dummy to read their DPS. Point defence is unchanged.

**No gun holds a target that has left the world.** A turret asks every tick whether what it holds
is still in `Combat.Hostiles` and alive (`Turret.StillThere`). Only turrets on pilots' ships were
ever told a target had gone, so a freighter's dropped turret holding a hunter when an escort ended
read the freed node every frame after and never fired again -- and so could the hauler's own mount,
after a reset mid-escort or on a guest. `Turret.Forget` is gone; `PlayerShip.Forget` keeps the
wing's order and the strike. `Turret.PdPriority` is `Turret.Rank`: it ranks for both kinds of
self-picking gun.

**Checks.** A dropped turret given a light, a nearer heavy and a nearer boss in one frame takes the
light, then the heavy, lets go of the heavy called off with its hull left, then hits the boss; alone
beside a practice dummy it takes the dummy, and a light arriving further off takes the gun off it.
The sentry table is proved on literals. The hauler's own mount lets go of a hunter called off while
it holds it, then shoots down the missile that follows.

### Stealth hides a pilot from being chosen, never from being hit (2026-09-24, in the WarShips_Version_L fork)

**A pilot who went dark stopped the boss.** `TargetFilter` asked one question and stealth answered
it for every list a filter built, the lists blows land on among them. A solo pilot under
`Status.Untargetable` emptied the boss's list: it stopped closing and turning, a running beam or
ram froze where it was, and every clock it had stood still until the pilot came back. A dark pilot
beside a seen one stood in a shockwave, a beam or a ram untouched, and a raid's missile burst passed
over any ship gone dark.

**A filter now answers two questions**: `Chooses` (aimed at, homed on, picked: stealth hides) and
`Hits` (a blow landing where it lands: it does not), with `Targeting.Choosable` / `Hittable` over a
list and `Nearest` choosing; `Allows` and `Targeting.All` are gone. The boss keeps both lists each
frame: with nobody in sight it closes on and aims at the last place it saw anyone and its clocks
run on; with nobody alive it waits. A raider picks only what it can see (`Raider.Up`) from
everything a raid can reach (`Raider.Reachable`, `Hub.RaiderTargets`), and a raid's burst lands on
all of it. A guided body stops homing while its target is dark and flies on straight.

**Checks.** In the arena, its only pilot dark, the boss's clocks run 1 s in 1 s, it comes back round
onto where it last saw the pilot at 0.3 rad/s, its 45 shockwave lands in full on the dark pilot, its
ram comes down the line to the last-seen spot, and a seeker thrown at the dark pilot flies straight.
In the hub, a raid's 42 burst lands on a dark wraith the raider has not picked. A guest's cosmetic
copy of a seeker reads the same status bits and stops homing the same way; no check sees that half.

### Gunnery and Cooling points work the way they say (2026-09-24, in the WarShips_Version_L fork)

- **Every Gunnery point lengthened the gun's cycle and every Cooling point the cooldowns**, by half
  a percent, because both wrote a NEGATIVE share onto an inverse stat -- and a share on an interval
  or a cooldown DIVIDES it, so a negative one makes it longer. They are positive now, the way a rate
  part's always were: twenty Gunnery points take a battleship's 2 s reload to 1.818 s (it was
  2.222) and twenty Cooling points take its cooldowns to x0.909 (it was x1.111).
- The K window shows those bonuses green (it printed them red), and the L window still reads
  "-0.5% a level": it prints what a share DOES to the figure (`AllStats.Change`), not the share.
- Saved pilots' points start helping at once (the default; it is what the rows always said).
- Check: twenty points of each on a battleship, measured on the sheet -- down, both of them. The
  table check now pins +0.005.

### A rate of fire reaches the guns, and a speed lift reaches the throttle (2026-09-24, in the WarShips_Version_L fork)

- **Every rate buff now fires faster.** The main guns read the raw reload, so the tender's overdrive
  on its own gun, the dart's boost, the wraith's veil and the Hot Roll and Ambush Veil parts moved
  `Spec().Interval` -- which the point defence and the checks read -- and not one main-gun shell.
  Every gun's reload -- main guns, point defence, dropped turrets, the carrier's fighters' and
  bombers' shots -- now comes from one place, `PlayerShip.Cadence` (a magazine reload, a rearm, a
  broadside's gap and a burst's refire are not lifted; no class that has them carries a rate row).
- **Rate buffs add, they do not multiply** (the owner's ruling): two x2 lifts are x3, and a +100%
  rate part under a x2 overdrive is x3 too, not x4 -- `Cadence` adds the lifts' shares to the
  reload's own bonus (`Stat.With`, on the sheet's rule and its 0.1 floor). No class carries two
  lifts of one kind and no check wears a rate part, so nothing measured moves today.
- **A speed lift lifts the thrust too.** Drag holds a hull to thrust / drag, so the rush's 2.5x
  stopped at 371 of 475 u/s, and from rest reached about 214 before it ended. It now gathers way
  2.5x as fast and reaches 475.
- **A fighter's three shots come 0.35 s apart**, the sheet's reload: the burst ran its reload down
  twice a frame (0.17 s). Three shots a pass, the same damage a pass.
- Checks: the tender's shells counted leaving the barrel (1 s, then 0.5 s under overdrive); fighter
  gaps at x1 and at a lent x2; the rush's gain from rest (2.5x) and its top (475 u/s); the stacking
  rule on literals, a part under a lift included.

### The sky goes the way the world goes, and has no edge; the Drake backs off before it throws (2026-09-24, in the WarShips_Version_L fork)

**The sky slid the wrong way.** Players found the parallax disorienting, and it was: every layer
moved WITH the ship while the world moved against it, so the stars seemed to swing round the
hull. The sky lives on a `CanvasLayer`, which is screen space, and its offset was written as if
it were world space (`+(1 - Drift)` x the camera). `SkyPlane` (replacing `Parallax2D`, whose
repeat had the same trouble) now sits at `-Drift` x the camera: the world's own direction, at the
row's share of its speed.

**The dark wedges were the edge of the sky.** A plane moved with the camera and drew a fixed
region, so far enough out the region slid off the screen and its corners showed through as large
dark triangles. The region now re-centres on the whole tile under the middle of the screen --
moving a tiled region by whole tiles changes nothing anyone can see -- so there is no edge at any
distance, and the plane's own position stays continuous for a check to read.

**The check that let it through measured distance, not direction.** It now hops the ship 200 u
in a seeded direction and requires every layer to move within 3 degrees of a point fixed in the
world, by its own share; and it puts the ship 32 000 - 80 000 u out, in a seeded direction, and
requires every corner of the screen to be inside what each layer draws.

**The Drake's asteroid throw starts with a warp back.** It used to start from wherever it
stood, often inside the pilot's face, where an 1800 u lane is not a threat anyone can answer. It
now warps to 1300 u on its own side of its target first (the shotgun's own `Warp`/`Standoff`
fields -- a row, not a new move), and the rock flies in 1.2 s instead of 1.6, a third faster; the
7.5 s of red lane is unchanged. `Boss.Open` now calls `Aimed` after a warp: every opener before
this aimed down the nose, so nothing noticed it was skipped, and the first move with both a warp
and a thrown body got a lane between the warp's leftover coordinates and no rock at all. The
shotgun still comes 11 s after each throw starts, clear of its 9.7 s.

### Seven sprites nothing loads stop shipping, and the record stops describing a deleted launcher (2026-09-24, in the WarShips_Version_L fork)

**`art_4x/` shipped in every release.** Five hulls at twice and the two turrets at four times the
resolution the game loads -- 13 MB of PNG that nothing in `scripts/`, a scene, `tools/` or
`project.godot` names -- sat outside every `.gdignore`, and the export preset takes
`all_resources`, so Godot imported each one and packed it into `Warships.pck`, about 3 MB once
imported. It moved under `art_unused/` in 242b1aa. The same was true of 45 `.translation` files:
Godot imports a `.csv` as a string table, one file per column, so the two review sheets in
`version/` became 45 translations, and those were committed. They are deleted and `version/` has
a `.gdignore`; the smoke run still writes both sheets through `FileAccess`, which a `.gdignore`
does not touch. The next `tools/pack.ps1` prints what `code.zip` weighs without them.

**Three `Known broken` lines were not true.** Each of the nine classes has had a utility family
since ff3e3e0 (four lines a system at three rarities, checked in the gear check); a boss has been
a row since 8dc513e (`Missions.BossType` and a `BossMove[]`); a turret a freighter left out has
been on the radar since 90edaca (`Hub.ScopeMarks`, checked from the arena guest). All three are
deleted, and the two `DESIGN.md` paragraphs that still called a boss a subclass say what it is.
One true line is added: offline, `PLAY.bat` cannot start a build that is already installed.

**Nor were three more.** `docs/README.md` said `PLAY.bat` starts a test build unpacked into
`play\` by hand -- it plays the source where the developer tools are, and elsewhere
`tools/install.ps1` replaces any build that is not the published one -- and that `Warships.exe`
changes every release, which is exactly what the part split is built to prevent. The entry below
said `play.ps1` warns that a first run takes a minute or two; that warning went when the two play
scripts were merged.

**The launcher's last words.** Its deletion left it described as current in `DESIGN.md`'s
release section, four `Known broken` entries, the Handoff, the Next up list, two entries below,
`tools/install.ps1`'s header, notes in `pack.ps1` and `snapshot.ps1`, and comments in `Game.cs`
and the harness. Each now says what is true: `PLAY.bat` -> `play.ps1` -> `tools/install.ps1`
installs the published build, and `tools/pack.ps1` makes one.

**Two comments described what the code used to do**: the utility families in `Equipment.cs`, and
the Drake's throw in `Drake.cs`, whose row comment still gave its old 1.6 s of flight. `DESIGN.md`
left out the throw's warp back to 1300 u. Each now says what is true.

**Three backslash paths had become control characters** -- `\f` a form feed twice in
`docs/README.md`, `\a` and `\r` a bell and a carriage return in a `verify.ps1` comment, fixed in
242b1aa -- and nothing could see them. `verify.ps1` has a `text` step now, at rung 2: any control
character in a tracked text file but a tab, a newline or a CRLF's carriage return fails it. Run
over the files as they were before 242b1aa it names all four, at `docs/README.md` :73 and :195
and `verify.ps1` :51.

### The launcher is gone (2026-09-24, in the WarShips_Version_L fork)

**Deleted, not moved.** `launcher/` (3 files, 417 lines) and `scripts/Builds.cs` (the
install-and-update mechanism it was built on) are removed, along with the 30 rung-3 checks that
proved that mechanism and the `dotnet publish` step in `tools/pack.ps1`.

**Why it stopped earning its place.** `tools/install.ps1` does the job with nothing installed --
Windows PowerShell is the whole requirement -- so the launcher was a 66 MB unsigned executable
standing between a player and a script that needed no executable at all. Smart App Control
hard-blocks an unsigned exe with no way through, which is the one failure mode the launcher could
not talk its way out of; a PowerShell script is not blocked by it.

**What the game keeps.** `Game.Build` still reads the build id out of the `BUILD.txt` a release
ships beside the executable, and still shows it at the bottom of the Esc menu -- but it parses the
one key it wants in six lines instead of calling into a file that ran an update engine. The check
that it is a property over a mutable static stays, because that trap is about THIS game's protocol
hash and has nothing to do with the launcher: a `readonly` build string would enter
`Net.Fingerprint` and make two byte-identical builds refuse each other.

**What a release is now:** five assets, not six. `tools/pack.ps1` exports, hashes, cuts the notes
and zips the two parts; nothing publishes an executable for a player to keep. The bracketed
sections in `NOTES.txt` stayed even though the thing that drew them in colour is gone -- they read
perfectly well as plain text, which is all GitHub's release body needs them to do.

### Six more steps that could not fail, found by sweeping every PowerShell file (2026-09-24, in the WarShips_Version_L fork)

After the analyser turned out never to have run, four agents read all fifteen `.ps1` files looking
for the same family. They found it repeatedly. Everything below was reproduced in a scratch folder
before it was touched.

**The build step never rebuilt, so it asked a question that could only come back clean.**
`verify.ps1` ran `dotnet build` with no `-t:Rebuild`, and an incremental build of an unchanged tree
emits no warnings whatever the code says. Measured on a project with a live CS0219: cold build
`1 Warning(s)`, the very next build `0 Warning(s)`, rebuild `1 Warning(s)` again. So invariant D's
COMPILER half was as unenforced as its analyser half, on every bar after the first.
`tools/analyse/run.ps1` had had the flag all along.

**The analyser still could not tell a failed build from a clean one.** Fixing the quoting made it
run; it did not make it check. Measured on a project that does not compile: exit code 1, a log
full of `error CS0103`, and the script printed `ANALYSERS: 0 findings` -- because a build that
dies produces no WARNING lines to count. It reads the exit code now, and a mutant proves it: a
line of nonsense appended to `Sky.cs` turns it red, removing it turns it green.

**The snapshot was dropping source, and by the very mistake its own comment warns about.** It
bucketed discovered files into four HARD-CODED directories, so `launcher/Main.cs` and
`launcher/Launcher.csproj` matched the filter, belonged to no bucket, and were silently absent
from the master copy; `PLAY.bat` and `Warships.sln` never reached the filter at all. The
directories are discovered now, a check proves nothing discovered is dropped, and the file went
from 111 entries to 115. A dead `Dir-Files` helper that nothing called went with it.

**The manifest passed a verification that failed.** `sha256sum`'s exit code was never read and
there was no floor on the count, so an empty list printed `manifest: 0 files, 0 not verifying` and
exited 0 while sha256sum had exited 1. That is precisely how a release manifest that hashed ONE of
189 files passed its own check earlier the same day. It now refuses an empty list, reads the exit
code, and requires an OK line per file.

**The screenshot sweep had no failing exit path at all.** `SWEEP INCOMPLETE` printed and fell
through; `$lint` and `$frames` were computed, printed and thrown away. A sweep whose engine died
at frame 0 ended on `frames: 0 | LINT: 0` and exited 0 -- and CLAUDE.md section 7 tells a reader to
trust `LINT: 0` for rung 4. The bar re-derived it from stdout so the BAR was covered; rung 4 run on
its own, which the ladder tells you to do for anything visual, was not.

**A half-copied tree was not noticed.** `robocopy`'s exit code is a bitfield -- 0-7 success, 8 and
up failure -- and both engine runners discarded it, so a partial copy went on to build, import and
report on whatever happened to arrive.

### The gate could not go red in three more places, and there was a second play script (2026-09-24, in the WarShips_Version_L fork)

**`0 Warning(s)` is a SUBSTRING of `10 Warning(s)`.** `verify.ps1` matched both that and
`0 errors.` unanchored, so a build with ten warnings and a typecheck with ten errors both reported
clean. Anchored with `(^|\s)`, and proved: the old pattern matches `10 Warning(s)`, the new one
does not. (`ANALYSERS: 0 findings` and `UNUSED ANYWHERE: 0` were already safe -- their prefixes
pin them.)

**The smoke step counted the checks and threw the number away.** Six `DONE` lines with twelve
checks behind them passed exactly like six with eleven hundred: it measured COMPLETION and never
COVERAGE. The three runs are now compared against EACH OTHER -- no figure to keep up to date, and
it fails when checks stop running rather than when somebody adds some.

**The sweep step did the same with its frame count**, so a sweep that rendered NOTHING still said
`SWEEP DONE` and `LINT: 0`. Nothing to draw is nothing to lint, and an empty pass looked exactly
like a clean one.

**And the seeds are printed.** Every run wipes the scratch folder, so run 3 destroyed run 2's logs
and with them the number that reproduces run 2's geometry. `CLAUDE.md` promises a failure comes
back with `run.ps1 -Seed <n>`; until now the gate never printed one.

**There were two play scripts.** A root `play.ps1` had existed since it was written; a
`tools/play.ps1` was added today doing the same job, which is the exact thing CLAUDE.md forbids --
two ways to do a thing is how the next instance picks the wrong one. Merged into the root one,
which keeps its `-Two`, and `tools/play.ps1` is deleted. `PLAY.bat` points at the root.

### The analyser step had never run (2026-09-24, in the WarShips_Version_L fork)

**`ANALYSERS: 0 findings` was a green line about a build that never happened.**
`tools/analyse/run.ps1` passed `-p:NoWarn='CS1591;CS1573;CS1587'`; PowerShell strips the quotes
off a native command's argument, MSBuild split on the `;` and aborted with `MSB1006: Property is
not valid. Switch: CS1573` before loading the project at all -- and the script then grepped that
error log for warnings, found none, and reported a pass. Every `verify.ps1` run in the Windows
history of this project has matched that line. **CLAUDE.md invariant D has been unenforced for the
analyser half since the port.**

The fix is `%3B`, which MSBuild decodes itself, and which the Linux original always had.

**It found 8 things the moment it ran:** unnecessary `using` directives in `BaseDefense`, `Raider`,
`RecyclerPanel`, `ShipClasses`, `Tour`, `Turrets` and `Waves`, and an unread assignment in
`MenuFoe.Deploy`. All eight fixed. The header of the tool now says what the failure looked like,
and says to check the log is a build if it ever prints 0 again -- because that is precisely what a
broken one looks like.

### A clone can be played on a machine with nothing installed (2026-09-24, in the WarShips_Version_L fork)

**Nothing in the repo launched the game.** There is no runnable file here and there is not meant
to be -- no `.exe`, no `.pck`, `dist\` gitignored -- so anyone given access had to know to install
the right Godot, know that the PLAIN build cannot run a C# project at all, find the engine, and
know `--path`. Each of those fails with an error about something else.

**`PLAY.bat` at the root, and `play.ps1` beside it.** The bat is one line, because PowerShell
refuses a `.ps1` on a double-click by default and `-ExecutionPolicy Bypass` answers that for one
command without changing anything on the machine. The script resolves the engine through
`tools/find-godot.ps1` -- the same resolver every other tool here uses, so `$env:WARSHIPS_GODOT`
and a gitignored `local.config.ps1` work exactly as they already did -- and checks the two things
that are ever actually missing BEFORE the engine can fail confusingly about them: the .NET SDK,
and that the engine is the mono build. If either is missing it plays the published build instead
(below); `-Editor`, which needs both, prints the download links.

**AND IT WORKS WITH NOTHING INSTALLED, through `tools/install.ps1`.**
Windows PowerShell 5.1 ships with every Windows 10 and 11, and `Get-FileHash`, `Expand-Archive`
and `Invoke-WebRequest` are all built in, so a script can go from a bare machine to a running game
with no Godot, no .NET and no git. It reads `BUILD.txt` from the release, downloads
each `part=` zip, checks each against the SHA-256 the release itself published, unpacks them, and
writes `BUILD.txt` LAST so an install that died halfway cannot claim to be a finished one. Then it
starts the game.

**It installs into `play\`, never `dist\`,** which `tools/pack.ps1` deletes whole on every
release -- a game installed there would be destroyed by the next build. `play\` is gitignored and
no tool here touches it.

**Proved end to end, from an empty folder:** 2 parts, 79 MB, both checksums matched, 189 files
unpacked, all 189 verified against `BUILD.sha256` with `sha256sum -c`, and the game started with a
window titled Warships. A second run says it is already installed and just starts it.

### The sky is layered, so flying looks like flying (2026-09-24, in the WarShips_Version_L fork)

**The background could not move, by construction.** It was one `TextureRect`, anchored `FullRect`
on a `CanvasLayer` -- anchored to the screen is anchored to the screen, so a ship at 190 u/s
crossed a starfield that never shifted a pixel, and the only thing on screen saying the ship was
moving at all was the speed readout.

**`scripts/Sky.cs` is new: a layer is a row.** `SkyLayer{Id, Texture, Drift, Scale, Tint, Spin, Z}`
and three rows -- far, mid, near -- at 0.08, 0.22 and 0.45 of the world's motion. `Sky.Build`
makes one `SkyPlane` per row and nothing else; the eight lines in `Hub._Ready` are gone. A
nebula, a dust band or a debris field is a row here and nothing in `Hub` changes for it.

**Zero new art.** All three rows draw `stars.png`; what makes them read as distance is `Scale`
(0.55 / 1.00 / 2.10 -- a far layer is a denser field of finer specks), `Tint` (dimmer and bluer
further out) and `Spin` (0 / 17 / 41 degrees, so two rows sharing one texture never line up and
read as a single grid sliding over itself).

**`Drift` is written in the player's terms**: 0.08 is "drifts at eight percent of the world".

**Checks:** the rows against literals and strictly ordered far to near, every drift above 0 (a 0
is the static sky this replaced) and below 0.6 (past which stars read as debris rather than
distance), each plane tiled at its own scale and angle, and the one that would have caught the
original bug -- move the ship and read where each layer is actually DRAWN, by its own row's share.
The direction and the far-out edge are the next entry's.

### One command makes a release, and the game says which build it is (2026-09-23, in the WarShips_Version_L fork)

**A player had no way to get a new build but to be sent one.** There was no scripted export at
all: `docs/README.md` said to open the editor and use *Project -> Export*, and that nothing here
had been exported or tested that way.

**`tools/manifest.ps1` was generalised rather than copied.** It now takes `-Root`, `-Out` and
`-Files`, defaulting to exactly its old behaviour, so `verify.ps1`'s call is unchanged -- and
`tools/pack.ps1` is the second caller, pointed at the export. One hashing implementation, two
callers. **Note for the record: `version/MANIFEST.sha256` is the SOURCE manifest and always was.**
It is built from `git ls-files` and contains no build output at all, not one `.dll`; the earlier
plan here said it was the release manifest, and it is not. The release manifest is a second output
of the same tool.

**`tools/pack.ps1` is the one command that makes a release.** It refuses a dirty tree, exports
headlessly, hashes all 189 files, cuts `NOTES.txt` out of this file, zips the two parts and prints
the upload list. Measured: **189 files, 191 MB installed; an ordinary release changes 5 of them,
10 MB zipped against 73.** The part split is decided in the script and written out as 189 explicit
`in=` lines, because pattern matching is logic, and logic belongs in a file a pull can fix.

**And the game says which build it is**, at the bottom of the Esc menu -- `Game.Build`, read once
out of the `BUILD.txt` a release ships beside the executable, `"dev"` in the editor and in every
harness. Display only: `Net.Protocol` is still the identity that decides whether two peers may
play. **It is a property over a mutable static, and that is not a style choice.**
`Net.Fingerprint()` folds in every static field that is `IsLiteral || IsInitOnly` whose type is
`Plain`, and `string` is `Plain` -- so a `readonly` build string would enter the protocol hash and
two byte-identical builds packed a minute apart would refuse each other at the handshake. A check
asserts that structurally, the way the trap is laid.

**Check:** the fingerprint blindness above, at rung 3.

**Two things found by building it rather than planning it.** The plain Godot `.exe` on Windows is
a GUI-subsystem binary, so PowerShell does not wait for it: the export "produced no exe" about a
file that appeared a moment later, and pack uses `_console.exe` like the smoke runner does. A child
`powershell -File ... -Files $files` flattens the array, so the manifest hashed exactly ONE of 189
files and verified perfectly -- a green line about almost nothing; it is called in process now, and
pack checks the line count against the file count.

### A rock twice the size, a lane that cannot drift from it, and a boss called RUSTY BUCKET (2026-09-23, in the WarShips_Version_L fork)

**The Drake Bastion's thrown asteroid doubles.** The body is 180 u (was 90) and its red lane is
360 u (was 180). It is held 310 u off the flank (was 220) -- the clearance the row has always
been: the hull's own half-width, the body, and a 40 u gap, 90 + 180 + 40.

**The lane is no longer a number written beside the body.** `BossMove.Width` is left 0 on the
throw row and `Boss.Warn` raises the lane at twice the body's radius. Two numbers that must stay
equal, written separately, had already drifted: `Boss.Scaled` lifts `Radius` one percent a level
and never lifted `Width`, so the rock grew out of the red lane warning about it -- 42.7 u past
each edge by level 40 at the old size, and 85.3 u at the new one. `Offset` is scaled with
`Radius` now for the same reason: a body that grows and a clearance that does not ends with the
rock drawn inside the hull holding it. (The comment in the harness claiming the offset was
already scaled had been wrong since it was written.)

**The odd-level boss is RUSTY BUCKET.** Its id stays `silver_lancer`: that string is a save key --
`Character.BossCleared` writes it into `[boss_cleared]`, and every build there has ever been wrote
it -- so every pilot's bounty ladder reads exactly as before. `Lancer.cs`, `DamageSource.Lancer*`
and the runtime keys `boss:guns`/`boss:beam` are named for the id and none of them moved. One
display string changed; the whole player-facing path derives from it (`Boss.Title` -> the ARENA
line and the health bar; `Missions.Title` -> the TIO bounty line).

**A frame the sweep was not really taking.** The four Drake frames set the camera's own `Zoom`,
which `Hub.MoveCamera` lerps back to `Hub.ZoomLevel` every frame -- in the arena too. They were
being taken at the default zoom with the boss about 100 u above the top edge, so
`65_drake_tractor_lane` could not show the boss holding the lane it is named for. They go through
`Hub.ZoomLevel` now, like every other staged frame in the file.

**Checks:** the throw row's literals (180 / 310 / no Width, and the offset against
`HalfWidth + Radius + 40`); the live throw, which now proves the rock clears the flank and the
lane is exactly twice the body; and on a guest, that a peer arriving mid-throw is sent a rock of
the host's size in a lane of its own width -- the only path the figure takes across the wire
(`NetRock`), and nothing asserted it before.

**Two stale statements fixed in passing.** `Missions.cs` still documented the boss scale as
`1.1^(L-1)`; it has been 1.025 since the curve was retuned. `DESIGN.md` still described the
Drake's numbers as `Drake.ThreatMult` times a constant of the other boss's -- that member does not
exist and the design is now the opposite: each boss carries its own literals so tuning one cannot
retune the other.

### Four lanes out of the base, a blockade that cuts one, and a gun at each outpost to answer it (2026-09-23, in the WarShips_Version_L fork)

**The passive income had no geography.** Miners and salvagers filled piles, a hauler sold them, and
nothing a raid did could touch the flow between them: a raid could kill a gatherer, but it could
never interrupt the base's trade. There was nothing on the map between the base and its four
outposts at all.

**Four lanes, each a row.** `scripts/Lanes.cs` is new: `LaneDef{Id, Outpost, Share, Texture, Length,
Tint, Drones, Speed}`, four rows at 0.25 each summing to 1. A lane is named for the mechanism -- a
route between two held points carrying a share that can be cut -- so a fifth lane at 0.2 is a row
and fifths everywhere follow from it.

**Couriers.** Two small drones per lane, the miner and salvager art at 22 u in the outposts' own
livery, running base to outpost and back at 320 u/s. They carry the share; they are not
`IHittable`, not `IRaidTarget`, and in no list -- nothing can shoot one. A cut lane's couriers hold
where they are, carrying nothing.

**A blockade cuts one.** `WaveTrigger.Blockade` and two rows of `Waves.All` (a squad, and from the
third blockade a gunship with them), at the base owner's own standing on the bounty ladder. They
form on the lane's stand, 80% of the way out (2400 u, 600 u outside the patrol perimeter, so a
patrol never cuts a lane by accident) and hold a 200 u ring. First at 420 s, then every 420 s, once
the owner has beaten one bounty.

**The income is cut in exactly one place:** `Yard.Deposit`, `amount *= Lanes.Flow(Hub)`. Every unit
the fleet ever delivers comes through there, so a blockade is one multiply: one lane cut pays 75%,
two pay 50%. A lane reads as cut while a live raider sits within 450 u of its stand -- every peer
derives that from the replicated raider list, as `Emplacement.Shielded` already does, so there is
**no new RPC and no new field on the wire**.

**A gun at each outpost, in a new OUTPOSTS tab.** 1200 u, six missiles a minute, 300 u/s, 42 damage
in a 90 u blast, on the same `Turret` component every other mount uses, shooting through
`Targeting.Craft` -- raiding craft only, so a pilot, a miner, a hauler, a dropped turret and a
practice dummy are outside it without a word about what class any of them is. Three upgrade rows:
`outpost_range`, `outpost_rate`, `outpost_speed`.

**And the missile became a row, because there are two launchers now.** `scripts/Missiles.cs` is
new: `MissileSpec{Side, Damage, Blast, Flight}` built per shot exactly as `TurretSpec` is, and
`MissileSide{Id, Mark, Body/Nose/Glow, Pool, Prey, Land}` -- two rows, a raider's and an outpost's.
Deleted in the same edit: `Raider.PredictSpot`, `MaxStep`, `WildSpeed`, `_targetVel`, `Hub`'s
raider-only blast tuple, and `HeavyMissileVisual`. A raid blow still carries the exact old source
key `heavy:<id>:missile`, so no damage tally moved.

**A tenth `Fx` row, `aim_zone`:** the warning zone in your own colours, for where one of *your*
side's shots will land. Drawn and timed exactly as a red one is; a second friendly telegraph is
this row again.

**Checks:** 15 new in the solo role -- four lanes at a quarter each summing to one, couriers both
ways on their lane, one cut costing exactly a quarter (100 delivered pay 100, then 75, then 50),
couriers holding on a cut lane, the gun's unbought figures against literals, the gun throwing at
the blockader and not at the pilot, a blockade as a trigger and a row rather than a spawner, and
the income restored the moment it is cleared.

**Two stale literals fixed while proving it.** The arena guest's bounty share still read 1650 --
the pay scale has been 1.025 a level since the boss curve, so it is 1537.5. And the ability arena
sat 554 u from an outpost, where the new gun's 42 landed on a mark before the ability under test
reached it; it has moved out to where the reach sweep already proved nothing is shooting.

### A peer in transit is in no sector, and the runtime the game targets is written once (2026-09-22, in the WarShips_Version_L fork)

**A peer moving between worlds was still recorded in the one it was leaving.** `EnterSector` tells
every peer to go, then changes its own scene -- and each peer's sector was only corrected when its
NEW world reported itself, a round trip and a scene load later. For that whole window the host went
on sending the old world's traffic to peers that had already gone, which is the exact thing
`RpcToSector` exists to prevent: the yard's 10 Hz state arriving in an arena, where there is no
`Yard` node to deliver it to. It cost three engine errors in one bar run of three -- `Node not
found: "Hub/Yard"`, then a cache miss, then `Invalid packet received` -- and nothing in the game
noticed, because a lost packet for a world you have left looks exactly like a packet you did not
need. The host forgets every peer's sector the moment it moves the party; each one is placed again
by its own report, which is also what brings it up to date.

**The runtime is stated in one place, and `net10.0` was tried and reverted.** `net8.0` leaves
support in November 2026, so the move was attempted. It builds with 0 warnings and it PLAYS -- three
bar runs of 830 checks, six processes each, and the screenshot sweep, all green on .NET 10. But
about one process exit in five ends in `0xC000001D`, an illegal instruction inside
`GodotObject.Finalize` under `GC.RunFinalizers`, after the engine prints `Leaked unsafe reference
to object` for a handful of resources and then `FATAL: Condition "!rc_owner" is true`. That is the
.NET finalizers running after Godot has torn its object database down: a shutdown-order problem
between 4.7.2 and a runtime it does not target. The same wrappers are outstanding on net8.0, where
the teardown tolerates them, so it is not a leak in game code and there is nothing here to fix.
**The runtime moves when the engine does** -- `Godot.NET.Sdk` is pinned to the binary anyway -- and
November 2026 is the deadline for that upgrade, not for a csproj edit.

What survives from the attempt is worth keeping: the framework was written down in SIX places --
`typecheck.ps1` and `typecheck.sh` each pinned "8" three times, for the compiler to use, the
reference pack to compile against, and the message printed when neither is found -- and both now
read `<TargetFramework>` out of `Warships.csproj` and follow it. A rung that checks the game against
a framework it is no longer built for is worse than no rung: it is green for the wrong reason.

Nothing else in the build has a support cliff: not one `PackageReference` in the project, the five
Python tools import the standard library alone, and the engine has no release train to fall off.

### The host's word: what a wreck may do, what a claim may buy, and what a guest is told (2026-09-22, in the WarShips_Version_L fork)

Found by a code-only audit of the whole build -- six read-only passes over combat, rewards, UI, all
34 RPCs, every mutable static, and a map of each mechanic to the table that owns it -- and then by
putting all forty findings through a refutation pass before a line was edited. Twenty of them were
checked twice with different lenses; **seven of the twenty came back with two different verdicts**,
and in two cases the second lens stopped a fix being written for a bug that was not there. Three
holes a modified client could walk through, and six places a guest was shown something untrue.

**A claim buys only what it could have paid for.** A pilot's level is its own word -- the host has
no way to check it -- and the gate that held a claim to it read the RAW wire figure, while the
clamp landed on the line below, on the stored field. So a peer announcing level 2000000000 with
every row maxed had all of it granted on the host's own copy of its ship: **+300 hull on a 300-hull
battleship and four times its main damage**, from a level-1 pilot. What a claim may SPEND is now
capped (`Progression.MaxSpendLevel`, 99 points), and a claim above it is **trimmed, dearest point
first, rather than refused whole** -- refusing whole is why an honest pilot past level 100 arrived
on anyone's host as a stock hull.

**A wreck fires nothing.** `Trigger` is written by three drivers -- the local flight, the demo ship
and the WIRE -- and a guest's 20 Hz report is built before the host's 10 Hz word of its own death
reaches it, so a cleared trigger was re-armed for a round trip and the host fired live shells out of
a hull in stasis; a client that never cleared it fired for ever. The rule now lives on the field
they share (`Trigger => _trigger && Alive`), so every gun and every readout inherits it.

**A wreck presses nothing but reboard.** `Refuse` runs on the OWNER and is a courtesy, never the
guard -- and the guard did not exist: a guest in stasis could fire a missile burst, switch on point
defence, reload and order a bomber strike out of its own wreck. One gate at `PlayerShip.DoAbility`,
with the exception declared as a row field (`AbilityDef.WhenWrecked`, set on reboard alone); the
nine newest abilities stop repeating it in their own bodies and the seven oldest stop lacking it.
A carrier's bombers now check the deck is alive, as its fighters already did.

**A wave, a pulse and a blast leave a missile in flight alone.** The shockwave, the EMP and the
echo read the raw hostile list, where interceptable seekers also live, so they deleted missiles and
counted each as a point-defence intercept -- and the shockwave **threw one its full 1000 u on the
host alone**, while every guest flew its own copy along the old path: two peers holding one
damaging missile a thousand units apart. All four loops (the railgun's too) now read
`Targeting.Attackable`, the row that already says "never a missile in flight".

**A held place is not claimable by name.** The character id is the guest's own claim, and nothing
checked that two peers were not flying the same one: announce the id of a pilot in the party, wait
for them to drop, and the host handed over its party slot, its READY, its position and every kill
owed to it -- bounty, EXP and crates -- while the real pilot came back to nothing. An id another
live peer is flying is refused.

**What a guest was not told.** A deployed turret's hull was sent once, at the drop, so its owner
watched a pristine turret take a whole raid and then vanish in a burst (`NetDeployHulls`, on the
world tick that already carries the raiders; `NetDeploy` carries the maximum too, so a mid-fight
joiner's bar has the right denominator). What a carrier's bombers were sent at was host-only, so a
guest's own BOMB slot read "RETURNING" for the whole of every strike it ordered (`strikeTarget`,
beside `wingTarget`). And an effect was raised by the host AND by each guest that ran the same
code, so a guest drew every burst twice -- only the simulator raises now, and `Hub.NetFx` is the
one road to a guest.

**The catch-up is metered; the report is not.** `NetMySector` answers a four-byte ask with the
whole world -- the mission, every raider, every turret, the boss's warnings, all reliable -- with
no limit of any kind, so a peer in a loop made the host marshal dozens of packets an ask on the
channel every other peer's telegraphs share. `Net.Metered` guards the answer. It first guarded the
whole handler, which cost a reconnecting pilot the held place it was waiting for and was caught by
the six-process run: **the meter belongs on the expensive answer, never on the door**.

Smaller, all of a kind: the arena's trip clock is wound only by the host (every guest advanced a
host-owned economy figure); the selected mission level is a floored property with a session reset,
so a guest that visited a host on level 12 stops carrying 12 into its own next session and
broadcasting it; a world's intercept counter and the title screen's ids end with the world, like
every other counter; and a pilot's level stops making a raid stronger past the same cap for the
host's own pilot as for a guest's claim.

**The checks:** a wreck's trigger and a press reaching `DoAbility` from its own peer (both poked
the way a guest reaches them, not through the owner's courtesy); a 1000 u shockwave that moves a
seeker 0 u and takes 0 intercepts; a claim of two billion with every row maxed trimmed to 98 points
and +30 hull; on the guest of the arena pair, the 45 the host put into one of its turrets arriving
as 75 of 120, and the turret's burst drawn exactly once. One racy check went with them -- it proved
"a guest cannot make its own turret" by pressing T and looking a frame later, which on a loopback
depends on losing a race; it now asks the host-only door directly.

### A class says what its weapons are and what it is born with, and two of the nine fly in a session (2026-09-22, in the WarShips_Version_L fork)

Three gaps from the flight test's Known broken list, closed by three declarations on the class row.

**The pilot's Weapons points reach what the class calls its weapons.** `Progression` named ONE stat
for every class in the game -- torpedoes for a carrier, main guns for everything else -- so a
sniper's railgun, a warden's hunters, a warrior's EMP and a freighter's deployed turrets took
nothing from a pilot's damage purchases, and `Equipment` held the same fact again in its own
per-class switch. The row declares it once (`ClassDef.Damage`: stat id → what one level adds) and
both read it.

| a level of Weapons adds | |
|---|---|
| railgun | +7.5 (5% of its 150) |
| hunter-seekers | +2.25 each |
| the warrior's EMP | +3 |
| a dropped turret | +0.5 (three are out at once: 1.5 across them) |
| every light's gun | +1 |
| the line, unchanged | +1 to the gun, or the torpedoes |

The gear that lifts "every weapon" reaches those eight stats too, so a railgun finally takes the
+25% five starting chips have always given a battleship's shells: **it lands 187.5, not 150**. A
saved pilot's points do exactly what they always did on the three original classes -- the carrier's
`fighter_damage` and the destroyer's `missile_damage` are declared at a step of 0 for that reason,
and `pd_damage` is declared by nobody, because the union of these declarations is what the chips
reach and every class has a point-defence row.

**Gear fits by what a hull HAS, not by which hull it is.** Every part carried a `ShipClass`, so a
freighter, a sniper or a dart could wear nothing but the parts that named no class at all. A part
declares the stats it moves now, and fits a hull with at least one of them -- because a part whose
every stat is missing there would do nothing. A Rapid Battery (`main_interval`, `main_damage`) fits
all eleven classes with main guns; Elite Hangars (`fighter_speed`) fit the carrier alone; a Basic
Combat Chip moves every weapon stat in the game and fits everything, lifting whichever of them that
hull has. `ItemDef.Class` is gone, with the class argument on all forty part lines.

**Every hull has all seven slots filled with parts that mean something on it.**

- Each class comes out of the yard with ITS OWN two mounts (`ClassDef.Kit`, read by
  `Equipment.Default`): a sniper starts with a Railgun Mount, a wraith with a Stealth Veil. Twelve
  rows name seven weapon mounts and twelve signature systems between them (three freighters share
  one cargo gun).
- The HULL slot says what the hull is: three new families of hull-and-handling frames (**Braced**,
  **Spar**, **Keel Brace**) fit every ship, and three **turret cradles** fit only a hull that drops
  turrets. A sniper wears 12 hull parts where it wore 3; a freighter wears 30.
- 138 drops, up from 120. A new class gets 24 weapon parts where it had none, all 12 engines, all
  12 shields, all 12 chips, and its own two mounts.

**Two of the nine fly in a session.** The two-player arena run flies a WARRIOR (host) and a
FREIGHTER (guest) at home before its first arena trip, and proves what can only be wrong between
two peers: a guest's T makes nothing on the guest and a 120-hull turret firing the GUEST's sheet
arrives from the host; the guest's 400 bubble spends 150 of itself on the HOST's hull and the guest
reads 250 left off the host's slot arrays; the host's rush reads HARDENED on the guest (which can
only come from `statusBits`); and the EMP's ring and a destroyed turret's burst both arrive as `Fx`
rows. A refit is announced now (`SetClass` goes through `Hub.PilotChanged`), so each peer can see
which hull the other flies.

Two things that run taught, both written into the checks: a guest's hull is the HOST's word and
lags a round trip behind its own refit, and a refit rebuilds the ship's slots -- so an ability still
running is discarded with them.

### Two more tables underneath: everything that flies, and everything that flashes (2026-09-22, in the WarShips_Version_L fork)

No new behaviour. Four files fewer, and the two things every future weapon and every future ability
would otherwise have copied.

**Everything that flies is one flyer with a row** (`scripts/Shots.cs`). `Shell.cs`, `Slug.cs` and
`Torpedo.cs` were three classes with one copy each of the same job -- step along a heading, sweep
the step for something it touches, deal the damage, burst, draw itself -- differing only in WHO
they hit, HOW LONG they burst for and WHAT THEY LOOK LIKE. Six rows now:

| row | hits | path tested | ends | looks |
|---|---|---|---|---|
| `shell` | the base's enemies | every 6 u | gone at once | a bright slug |
| `slug` | the pilots | every 6 u | bursts 0.25 s | a slow glowing ball (a boss's gun) |
| `scrap` | the pilots | every 6 u | bursts 0.25 s | a tumbling shard |
| `torpedo` | the base's enemies | the end point | bursts 0.5 s, smoke | a missile |
| `missile` | the base's enemies | the end point | bursts 0.5 s, smoke | a heavy missile |
| `seeker` | the pilots | the end point | bursts 0.5 s, smoke | a missile, and **point defence can take it** |

`Combat.Fire` is the one door every projectile comes through; `FireShell`, `FireSlug` and
`LaunchTorpedo` are one line each, naming a row. **On the wire, three launch RPCs became one**
(`NetShot`), and `Combat`'s three launch hooks became one `ShotFired`. The path test is
`Shots.Sweep`, shared -- a step longer than a hull is how a shot passes through it between frames,
and there is one place that gets that right now. A boss's thrown rock is deliberately NOT a row:
it is held in a tractor beam, thrown down a fixed lane on a cubic ease, strikes everything in the
lane once and breaks at the end.

**Everything that flashes is a row too** (`scripts/Fx.cs`), and every peer sees it. There was one
effect node (`Explosion`) and everything else improvised: the freighter's shockwave, the warrior's
EMP and the echo's detonation were drawn as SPOKES OF LASER FLASHES, because `Combat.Flash` was the
only drawing that reached a guest -- and the echo's burst was added straight to the host's own tree,
where nobody else ever saw it. Seven rows (a burst, a fleet craft lost, one rebuilt, a wave, an EMP,
an echo, a railgun's bar) in four shapes (burst, ring, spokes, bar), raised by name with `Fx.Raise`
and sent to every guest through one RPC (`Hub.NetFx`). `Explosion.cs` is deleted.

**And the harness bug that hid it.** A check calls `Combat.Clear` and then restored the hooks BY
HAND from a list written when there were four of them: `SlugFired` was already being dropped, and
`Fx.On` joined it, so every effect raised after that point in the run went up on nobody's screen.
The hooks go back through the world's own installer (`Hub.Hooks`, extracted for exactly this), and
the check asserts every one of them is dropped and then restored -- it cannot drift again.

### Twelve classes, six enemies, and the tables they are rows of (2026-09-22, in the WarShips_Version_L fork)

**Nine new classes, flyable.** The creator offers four pages of three.

| | Hull | Speed | Guns | What it is for |
|---|---|---|---|---|
| **FREIGHTER** | 400 | 85 u/s | 1 main, 2 PD | Three deployable turrets, and a bubble: 400 damage soaked for everything inside 260 u, 8 s, 25 s cooldown |
| **TENDER** | 400 | 85 u/s | 1 main, 2 PD | Three deployable turrets, and overdrive: its gun, its point defence and every turret it has out fire twice as fast for 8 s |
| **BASTION** | 400 | 85 u/s | 1 main, 2 PD | Three deployable turrets, and a shockwave: everything within 1000 u thrown a thousand units clear -- and a boss, which cannot be thrown, held still for 3 s instead |
| **SNIPER** | 140 | 190 u/s | 1 main | A railgun: 3 s charging with the hull locked (no thrust, no rudder), then 150 down a 2500 u line through everything on it |
| **WARRIOR** | 140 | 190 u/s | 2 main | A rush: 2.5 s at 2.5x speed taking half damage, ending in an EMP -- 60 damage and a 2 s hold on everything within 260 u |
| **WARDEN** | 140 | 190 u/s | 1 main, 1 PD | Point defence that never switches off (0.25 a shot, half a warship's), and six hunter-seekers that each take a target of their own |
| **DART** | 90 | 260 u/s | 1 main | A barrel roll: 1.2 s nothing can touch it, then 4 s at +60% speed and +25% rate of fire |
| **ECHO** | 90 | 260 u/s | 1 main | Its echo remembers every point of damage it deals for 5 s, then detonates the lot within 220 u of where the last of it landed |
| **WRAITH** | 90 | 260 u/s | 1 main | Five seconds nothing hostile can pick it: whatever was coming for it goes after someone else, or gives up |

- **T drops a turret, C picks one up.** Three out at once, collected from within 120 u (it says
  `NOT OVER ONE` from further). A dropped turret is 120 hull, 6 damage a shot every 0.5 s inside
  500 u, fires by itself, and is something raiders will go for. Its gun is its owner's sheet, read
  every tick, so a level bought while three are out improves all three.
- **Every class's own numbers are rows of its own** (`ClassDef.Rows`), so the K window prints the
  railgun's charge and the bubble's pool beside the hull and the helm without Stats.cs knowing
  those classes exist.

**Four new enemies**, drawn from the owner's sheets, in the rotation from the first wave:

| | Hull | Damage | Speed | |
|---|---|---|---|---|
| **Talon** | 18 | 1.4 x | 130 u/s | A webifier that gave up its armour: it arrives first and holds on |
| **Pod** | 60 | 0.7 x | 78 u/s | Slow, hard to shift, webs from 130 u |
| **Cross** | 130 | 2.6 x | 100 u/s | A gunship with the racks stripped and the gun wound up; no missiles |
| **Lancerkin** | 150 | 1.8 x | 100 u/s | Stands 260 u off -- further than anything else -- and throws the predicted missile |

Each wave takes one kind that PINS and one that STANDS OFF, in turn, so a row added to
`Enemies.All` joins the rotation without a line of code anywhere.

**The hauler carries its own point defence**, always on: it is nobody's ship, so there is no window
to open and no bar to open it from. 1 damage a shot every 0.5 s inside 400 u, and
`hauler_pd_damage` (BASE > HAULER, 200 cr) buys +5% a level.

**What the refactor changed underneath** (no behaviour of its own):

- `scripts/Ships.cs` -- **one row per class**: its hull and mounts, the numbers that differ from
  the sheet's defaults, what it is FITTED with (`Fit.Guns | Broadside | Missiles | Wing | Pd |
  Deploy | AlwaysPd`), its abilities, its name and its controls line. `Classes.Guns/Broadside/
  Missiles/Wing`, `PlayerShip.Art` and `Abilities.ByClass` are gone.
- `scripts/Abilities.cs` -- **one entry per ability**, carrying `Press` (what it does, on the
  host), `Refuse` (why the owner cannot, checked on the owner's machine so the slot says so at
  once) and `Show` (what its slot says). The three switches on the ability id, in three files, are
  gone; two classes with point defence share one entry.
- `PlayerShip.Slot` -- every ability counts the same four things (running, cooling, its own number,
  a count). `_pdLeft`, `_pdRecharge`, `_mag`, `_missileReload`, `_missileRefire`, `_bsWindup`,
  `_bsGap`, `_bsCooldown` and `_bsVolleys` are gone, and with them **seven named fields on the
  wire**: `NetHostState` carries four slot arrays, so a new ability changes no protocol.
- `scripts/Turrets.cs` -- **a turret is a component**: it asks its `ITurretHost` what gun this is
  (`TurretSpec`, read every tick) and whether it may fire. A warship, a hauler and a turret
  standing in space now share one swing, one claim-sharing acquisition and one reload.
- `scripts/Enemies.cs` -- one row per enemy; `Raider` is the code that flies one.
- `scripts/Tags.cs`, `Targeting.cs` -- **what a thing IS**, and one filter type over one `Nearest`.
  Point defence's `h is Torpedo || (h is Raider r && !r.Heavy) || (h is TargetDummy d && d.Fighter)`
  and its `HitRadius < 20f` guess at "small" are gone, and stealth has one place to stop a hostile
  choosing you.
- `scripts/Statuses.cs` -- pinned, held, hidden, hardened, evading, shielded: the host resolves,
  sends the bits, and a guest shows them. The pin's bool-and-timer on two classes is gone.
- `scripts/Ids.cs`, `Aim.cs` -- one id space per kind (six hardcoded bases gone), and one place for
  "point a nose-up hull at that" (twenty copies) and arrive-steering (five).
- `PlayerShip.Incoming` -- **the one door damage comes through**, holding the 0.52 s per-source
  gap, the tally, the impact point, the death, and `Guarded()`, where evasion, hardening and
  bubbles meet damage so that no weapon knows about any of them.
- **The Drake's figures are its own** (1.875 s, 18.75, 7.5 s, 250). They were written as 1.25x the
  Lancer's constants, so tuning one boss silently retuned the other.

**The harness is source, and is type-checked as source.** `typecheck.ps1` compiles
`tools/smoketest/SmokeTest.cs.txt` and `tools/screens/Shots.cs.txt` with `scripts/`: a rename used
to pass every cheap check and fail three minutes into an engine run, after the copy and the
import. `verify.ps1` also stopped asking for integrity outside `-Update`, where the manifest is by
definition the last change's -- a red line in every mid-session verdict is how a real failure gets
waved through.

**Varied geometry, reproducible.** Every run prints `SEED n`; `run.ps1 -Seed <n>` puts the same
numbers back for every role. The new checks place their ships and raiders somewhere different each
run; every figure they assert stays a literal.

### Known broken (as of this batch)

- **A warp to the EQUIPMENT BASE stops short of the pad.** Like every waypoint, a warp stops the
  mark's Pick (120 u) plus half the hull plus the standoff from its centre, which is past the pad's
  120 u half-height and 160 u half-width, so the pilot flies the last stretch onto the deck by hand.
  Built as planned (open question 7); a Pick of 0 for a place a ship is meant to land ON is the fix
  if the owner wants it.
- **Some beams make no sound, and never did**: the mining beam, the salvage beam and a raider's web
  are drawn but silent -- each is held for seconds, and `Sfx` has no looping voice to hum one with
  (a new table) -- and the title screen's raider tracers are silent.

- **The typed-address failure names the host's room code, which does not exist yet.** Room codes are
  slice S5 of the multiplayer redesign; until it lands, "use the host's room code" points at nothing.
- **One RPC that carries several streams still orders them against each other.** Each ordered
  unreliable RPC has a channel of its own now (`NetChannels`), but some carry more than one stream:
  `NetShipState` one per pilot (the host passes the others' on, on the same channel), `NetHostState` one
  per ship, `NetHulls` one per kind, `NetRaiders` a chunk per 24 raiders, and the flashes of a burst.
  On a link that reorders, a report for one is still thrown away when one for another, sent after
  it, arrives first. The next report replaces it; a channel per pilot, or unsequenced reports that
  carry their own clock, would end it. Left for the transport decision.

- **A chip cannot be levelled.** The equipment window's chip rows show UNEQUIP or nothing, never
  UPGRADE (`EquipmentWindow.cs:88`, `off ?? Upgrade(...)`): a fitted chip always has the UNEQUIP
  button, and an empty slot has no part.
- **A kit part's UPGRADE takes salvage and lifts nothing.** Every core slot shows UPGRADE, the kit's
  Standard Drive, Basic Deflector, Reinforced Frame and the class's own mounts included; a kit part
  has no `Ups`, so the level is paid for, printed as "+5%" beside the name, and moves no number. A new
  pilot's first UPGRADE is therefore a salvage sink. The fix is one condition in
  `EquipmentWindow.Upgrade` (no button for a part with no `Ups`).
- **A dropped turret nearest a shielded hull spends its fire on the shield.** A turret left
  standing takes any hostile in reach, and a pirate-base hull behind its shield
  (`Emplacement.Shielded`) is in `Combat.Hostiles` and takes nothing while the shield is up, so the
  turret holds it until the pylons fall to something else. A shield is not a tag or a status a
  filter can read. For turret orders (the class kits).
- **A freighter's Engines points stop buying speed at 128.6 u/s** (45 thrust / 0.35 drag): the
  Engines row and the drive parts move max_speed without thrust, so points past about the 44th buy
  nothing, and a Cruiser Drive II or III (-20% thrust) leaves it short of its own top speed (a 102.9
  u/s ceiling under 104.1 and 110.5). For the item pass.
- **A part on a lift's own row still multiplies it**: Hot Roll's +30% on boost_rof makes the dart's
  boost x1.25 x 1.3 = x1.625, not +55%. The same holds for a drive part under a speed lift: a Racing
  Drive's +25% under the rush is x3.125, not x2.75. For the item pass.
- **The pirate base's one turret never aims on a guest.** `Emplacement._aim` is set only under
  `Net.Sim`, so on a guest the launcher's barrel points at the world origin for the whole siege while
  its lane and its rounds come from the host where they should. Cosmetic; the fix is to aim at the
  nearest pilot on every peer, as `Turret.Tick` already does for point defence.
- **A bastion's shockwave throws the pirate base and its pylons on the host alone.** They are
  `Tag.Structure`, which `Targeting.Throwable` allows, and an emplacement's position is never sent
  after it spawns, so a guest keeps them where they stood. Adding `Tag.Structure` to `Throwable` fixes
  it and changes what a shockwave does to a site: the owner's call.
- **SMART APP CONTROL HARD-BLOCKS THIS on the machines that have it**, and the answer is a note
  asking the player to switch it off. It is a Windows 11 feature separate from SmartScreen: no
  "Run anyway", no bypass, and it is only ever on for clean installs of 22H2 or later. Switching
  it off **cannot be undone** -- Windows lets that setting go one way and the only route back is a
  reinstall -- so the release notes say that plainly and tell the player it is fine to decline.
  A section of `NOTES.txt` carries it (`tools/pack.ps1`), one row there. THE HONEST POSITION:
  this asks a friend to permanently disable a security feature to play a game. It is the owner's
  call and it is made knowingly, but it is the reason to revisit signing, or Steam (whose client
  is trusted, and which installs and updates on its own), if this ever goes past friends.
- **Nothing is signed, and that is a decision rather than an omission.** SmartScreen trusts a
  certificate's REPUTATION, not the presence of a signature: a new OV certificate shows the same
  "Windows protected your PC" wall until downloads accrue against it, and only an EV certificate
  (~$300-600/yr plus a hardware token) is trusted on day one. Not worth it for a game going to
  friends. `tools/pack.ps1` puts *More info -> Run anyway* at the top of every release's NOTES.txt
  instead -- a standing preamble, so it is true of every release without being typed into the
  record once a batch and then forgotten. Revisit if strangers start playing; the file whose
  reputation would accrue is `Warships.exe`, which rides in `runtime.zip` and does not change
  between releases.
- **The headless export sometimes finishes and never exits.** On the first real release run
  `savepack` printed DONE, all 189 files were on disk, and the Godot process then sat at 0 CPU for
  eight minutes without leaving -- hanging `pack.ps1` with nothing to read. It is judged by its
  OUTPUT now: `pack.ps1` waits `-ExportWait` seconds (600 by default), then stops waiting and lets
  the files decide. Whether this is the same teardown fault as the net10.0 exit crash is unknown;
  it did not happen on three earlier `-Dirty` runs.
- ~~No release has been published.~~ **The first release is out and the channel is proved.** Tag
  `2026-09-23.2ea5a0a` on `ArmosCodesStuff/MP_Space_Game`, all five assets. Fetched ANONYMOUSLY
  over the path `tools/install.ps1` reads, `releases/latest/download/`: `BUILD.txt` came back
  byte-identical to the local one, and `code.zip` came back at 10,271,112 bytes with a SHA-256
  equal to the one its own `part=` line claims. That is the whole chain -- redirect, CDN, bytes,
  hash.
- **The repo is PUBLIC, and so is the download.** It was private until the release went out;
  `tools/install.ps1` carries no token, so a private repo would return 404 to it and nobody could
  install at all. Made public deliberately, after a scan of the working tree and ~400 commits of
  history found no token, key or credential (`local.config.ps1`, the only machine-local config, is
  gitignored and untracked). **"Public but with a hidden URL" is not a thing on GitHub:** a public
  repo's releases page is listed, browsable and indexed. What protects the build is that nobody is
  looking, which is not access control. Gating it for real needs a service in front of the
  download that `tools/install.ps1` asks instead of GitHub -- one line there (`$Base`), and a
  fresh download of the repo for every player who already has one. A design for exactly that (a Cloudflare Worker holding the token plus a list of
  player keys) was drafted and set aside when the owner chose public.
- **Offline, `PLAY.bat` cannot start a build that is already installed** on a machine without the
  developer tools. `tools/install.ps1` asks the release what is current before it looks at
  `play\`, and stops when the release cannot be reached, so a player with no network is told it
  cannot reach the release and gets no game. `play\Warships.exe` itself still runs.
- **The runtime leaves support in November 2026 and cannot move until the engine does.** `net8.0`
  is what Godot 4.7.2 targets; on `net10.0` the game builds, plays and passes the whole bar, but
  roughly one process exit in five crashes inside Godot's own teardown. The fix is a Godot upgrade,
  not a csproj edit.

- **Only two of the nine have flown in a session** -- a warrior and a freighter, in the arena pair.
  The wing and deck classes, the sniper's rail line and the echo's detonation are still unproven
  between two peers.

- **Nobody has flown the nine new classes.** They are proved by the solo smoke run (every ability,
  every number) and by the sweep's frames; their BALANCE is arithmetic, not play. The three
  freighters share one hull sheet, and the lights' 90 hull against a raid is a guess.
- **The bubble covers pilots' ships only.** A miner, a salvager or the hauler standing inside it
  takes its damage on the hull: their damage does not come through `PlayerShip.Incoming`.

### Bigger ships, grey and white (2026-09-22, in the WarShips_Version_L fork)

- **The battleship is by far the largest**: its drawing twice as wide, then 35% and 25% larger -- 378 u,
  a 43.875 u half-beam, its turrets 2.5x (the drawing's own turret-to-hull proportion on the doubled
  beam).
- **The carrier is 25% smaller than the battleship** (283.5 u, a 40 u half-beam) and **the destroyer 25%
  smaller than the carrier** (212.6 u, a 27.8 u half-beam). Their mounts, the carrier's bays and runway,
  and their turrets scale with them. The hulls are drawn at 4 px a unit at their sizes
  (`tools/make_ships.ps1`). The carrier's engine plume sits 8 u in from its stern (`ClassArt.EngineInset`).
- **Bombers park at 65% on the bigger deck** (the hauler's own landed size, and what fits the 22.4 u of
  white beside the runway), and lift off over 1.6 s: the run up 124 u of runway ends about at their
  top speed.
- **The default hull is grey (0.6, 0.6, 0.6) and the default accent white.** A pilot still in an earlier
  default -- the pale blue, the blue after it, the gold accent -- loads in today's.
- **New ships spawn clear of the base whatever the class**: half the longest class and a margin below
  the pad (a 302 u battleship at the old spot overlapped it).

**Tests**: smoke **752 pass, 6 of 6 runs, three runs in a row; sweep 86 frames, LINT 0; ALL CHECKS PASSED**. Changed checks: every size above as literals (302.4, 35.1; 297.5; 227.5,
29.75), the bombers' 65% and the new deck band, the lift up to 115.5 u ahead over 1.6 s (also the guest's
deck-clock pose), grey and white, and a pilot in the blue and gold loading in grey and white.

### The owner's line art, the carrier's deck, the escort's threat, boss sounds, damage numbers (2026-09-22, in the WarShips_Version_L fork)

- **Every ship is the owner's line art now**: grey on transparent for the game to tint, each made exactly
  symmetrical, redrawn at twice its final size, sharpened, cut from its paper and brought down
  (`tools/make_ships.ps1`, from the drawings in `art_source/`, which Godot ignores). The sprites it
  replaced are in `retired/art/`.
  - **Carrier**: a runway down its centre with white deck either side, three sponsons a flank; point
    defence on the two middle sponsons and the stern block.
  - **Battleship**: its four painted turrets painted over, the four moving main turrets standing where
    they stood on the spine; point defence on the stern quarters.
  - **Destroyer** (the smallest of the three): main turrets on the fore spine and the central plate,
    point defence on the stern quarters. (The three's sizes: the entry above.)
  - **Turrets**: the owner's twin-barrelled turret is every main turret (`turret_main.png`); point
    defence is a smaller, round, single-barrelled turret in its style (`turret_pd.png`). Turrets wear
    the pilot's **accent** colour.
  - **Raiders**: the crescent-winged fighter is the heavy (its one turret on the spine), the small fighter
    the light; both in raider red (`Raider.LightTint` is new, and the title screen's lights wear it too).
- **Bombers park on the carrier's deck**, small, in bays either side of the runway, and **take off
  one at a time, 0.83 s apart** (the fighters' cadence): each rolls onto the runway and up it to the bow,
  growing to flight size as it lifts, as the hauler lifts off its pad. Home, a bomber comes in over the
  carrier's centre and **settles onto the runway there**, shrinking back to deck size, then taxis to its
  bay and rearms. Every deck move runs in the carrier's frame on a clock, on the host and on guests alike;
  its shadow on the deck tucks in as it lands. A bomber still waiting its turn when a strike is called off
  answers for itself, so the strike ends.
- **An escort's hunters scale with the escort's THREAT** (`Hub.EscortThreat`), a mission level made a
  quarter each from **the load** (a level for every 1000 cr the cargo will fetch escorted), **the party**
  (half its pilots' mean level, half its ships' toughness: a level for each 10% of hull over the class's
  own), **the highest boss beaten**, and **the route** (half a level a wave, so an escort builds). It sets
  their hull and damage (the missions' 10% a level), their numbers (a light more a patrol every 3 levels,
  3 more at most) and, very slightly, their speed and turning (1% a level, 10% at most). Before, only the
  highest boss (and the party's size, for the patrols) counted.
- **Every boss special has its own sound** (`tools/make_sounds.py`, synthesised): the Lancer's death beam
  charging, then firing as a **low growling buzz**; its ram, shockwave and trident; the Drake's gun, warp,
  scrap blast, tractor, throw and the rock breaking. They ride the warnings (`Telegraph.Cue`/`Strike`), so
  every peer hears each move when it sees it -- a guest in step with its own shortened warning.
- **A guest arriving mid-fight is sent the boss's warnings as they stand, and the Drake's rock** mid-hold
  or mid-flight (`Boss.CatchUp`, from the host's catch-up for a peer reporting its world): a guest that
  rejoined during a throw saw no rock and no lane, yet could be hit. The Lancer's beam had the same gap.
- **Damage numbers**: small figures where damage lands, floating up and fading after a second -- gold for
  damage dealt, red for damage taken -- at the impact point where one is known (a shell's, a torpedo's,
  the side a ship was hit from), else round the thing's centre. Every peer shows what it sees from the
  hulls it already has (each thing watches its own, `HullWatch`; a guest from the host's figures), so
  nothing more is sent; quick hits on one thing add into one figure.
- Removed: `tools/make_destroyer.ps1` (the destroyer is the owner's drawing now); `turret_bs_main.png`,
  `turret_bs_pd.png` and `turret_carrier.png` (retired); the bombers' backing into flank slots
  (`PlayerShip.DockSlot`, `ClassArt.DockX/DockY/DockSpacing`, the Backing state).

**Tests**: smoke **751 pass, 6 of 6 runs, three runs in a row (734 before), 0 problems; UNUSED ANYWHERE 0; ALL CHECKS PASSED first time**. New checks: the default colours, and a pale-default pilot loading in
today's; the battleship's hull and turret art, the turrets on the spine and the stern, in the accent; the
destroyer smaller than the other two; parked bombers small on the white deck, facing the bow;
take-offs at least 0.83 s apart, from 40% to full size, up to the runway's bow end; landings on the
centre (within 10 u), full size to deck size, then the taxi to the bay; and a guest seeing its own
bombers lift off and land at deck size in its own carrier's frame; the escort's threat (its formula as
literals, and the first wave carrying this escort's); every boss special's sound heard in its fight; a
guest reporting mid-throw sent one rock with what was left of its warning, and its lane; damage numbers
dealt, taken, merged and faded, and on a guest. Sweep: **86 frames**, two new --
a bomber lifting off (10c) and one landing (10d). As the owner asked for this session, **one test run at
the end and no mutant runs**: the new checks were seen to pass, not seen to fail against the old code.
Reviewed before the run (REVIEW.md, pass 10).

### Slice 3b of the second batch: the Drake Bastion (2026-09-22)

- **A second boss, the DRAKE BASTION, holds the even levels** (the Silver Lancer the odd ones, on the
  one ladder). 700 hull, 420 u, the owner's rust-brown sprite (`boss_drake.png`, recovered from the
  session's transcript and trimmed); no escorts. Lower, steadier pressure than the Lancer, and two
  specials on the Lancer's own clocks, each 25% harder with 25% more warning:
  - **Main gun**: a slow shell (220 u/s) every 2.5 s, 6 a hit -- 2.4 DPS -- dodged by moving. Never a
    point-defence target.
  - **Scrap shotgun** (every 15 s -- the trident's cadence -- from 17 s): a ring where it will land, then it WARPS to
    600 u from the nearest pilot; a fan of seven red lines for 1.875 s, held still; then seven pieces
    of scrap down them -- the same fixed fan every time, 10 degrees apart -- 18.75 each, each its own hit.
  - **Asteroid throw** (every 30 s, the death beam's slot): a big rock (`boss_rock_1/2.png`) appears
    beside it in a pale green tractor beam; a red lane of the rock's width for 7.5 s, held still; then
    the rock is hurled down it -- slow to start, then very fast (the distance goes as the cube of the
    time) -- 250 to every ship in the lane, breaking apart at the lane's end.
- New: `Drake.cs`, `Slug.cs` (a boss's dodgeable round: host damage, guests' cosmetic copies through
  `Hub.NetSlug`, never in `Combat.Hostiles`), `ThrownRock.cs` (the rock's whole path fixed at the throw,
  so guests fly it from one event, `Drake.NetRock`), and `Beam.cs` -- the miners' cutting beam and the
  tractor are one drawing now, not two.

**Tests**: smoke **734 pass, 6/6 runs**, three runs in a row. New checks: level 2 is the Drake (770 hull at level 2, 420 u); its gun
(220 u/s, 2.5 s apart, 6.6 a hit at level 2); the shotgun (the warp to 600 u, the ring, a fan of 7
lines for 1.875 s held still, seven pieces at -30..+30 in tens, the middle one striking for 20.625);
the throw (the rock 220 u beside it, a 7.5 s lane of its width, boss and rock held still, half the
flight covering an eighth of the 1800 u lane, 275 to the pilot, broken at the end); the bosses taking
the levels in turn, and a level cleared under either being cleared (no second first-clear bonus);
the new Combat hook dropped with the world. **The arena pair's second mission is now the Drake's
level**: the guest builds the Drake, and its shells and its thrown rock reach it (its pay: a level-2
kill -- level 3, and a 1650 share). Sweep: the Drake's arena, its scrap fan and scrap in flight, its
tractor over the lane, and the rock in flight.
**Mutants**: the new Combat hook left set, the rock not sent to guests, and the old per-boss
first-clear rule -- every one caught, in a copy reproducing the baseline's 733 checks.
**Review**: a four-lens read found 4 (8 refuted) plus 2 from the critic: the first throw (9.1 s of it)
swallowed the shotgun's 13.5 s slot and every other shotgun then ran straight on from a throw -- the
shotgun now keeps the 15 s cadence from 17 s, 11 s after each throw starts (checked); a guest's copies
of the dodgeable rounds trailed the host's hit test by a round trip (they now lead by it, as the
game's warnings do); the rock was drawn 20 u wider than its lane (now fitted by its longer side); and a
stale bar comment. The critic's rejoin gap is under Known broken. See REVIEW.md, pass 9.

### Slice 3a of the second batch: the Lancer's beam as a hard lock, and a base for more bosses (2026-09-22)

- **The owner's report on the Silver Lancer is fixed.** It could charge its beam before its escorts
  had pinned the pilot, and it turned while it charged. Now, from the moment the escorts launch **it
  holds its position** and turns only to face the pilot; **the charge begins when the web has actually
  pinned the pilot** (or, the escorts all shot down, once their web would have landed; or, escorts
  that neither pin nor die, 5 s after launch at the latest); and **from the charge to the end of the
  beam it is hard locked** -- no turn, no move: the red line it shows is the line it fires. The ram
  never starts during a beam (it waits for it), and the shockwave's wind-up holds still too.
- **The Silver Lancer has 760 hull** (was 600).
- **Bosses are classes on a shared base**: `Boss` (abstract) holds what every boss is -- scaling,
  replication, telegraphs, the super-move bar -- and `Lancer` its weapons. **One ladder of levels,
  the bosses taking them in turn** (`Missions.ForLevel`; only the Lancer exists until slice 3b, so
  play is unchanged). A level counts as cleared whichever boss held it (the first-clear bonus once a
  level); the boss's name on the bar, the HUD and the TIO comes from the level's boss.
- **A peer sent into a world is told the level with it** (`NetSector`): the arena's boss is built
  from the level, and a guest brought in late (or back after a drop) had only its own stale level.

**Tests**: smoke **724 pass, 6/6 runs**, three runs in a row. New checks: escorts out, the boss holds (pushed past its standoff)
and turns only to face the pilot (twisted 0.6 rad off it); the charge starts on the pin, well before
the web was predicted (1.0 s against 2.8); hard locked while it charges (twisted off the pilot, it does
not turn back); one ladder (clears count whoever held the level; an unknown boss's count for nothing);
760 hull; the level-3 hull. **Mutants** (solo): the old tracking charge, the old estimate-made-at-
launch trigger, and the old unlocked escorts' phase -- every one caught, in a copy reproducing the solo run's 590 checks (drifting 11 u; charging only on the estimate; turning 0.155 rad in 0.5 s).
The fallbacks are checked on beams started by hand: the escorts shot down, it charges once their web
would have landed and not before; the pilot 3000 u off, it charges 5 s after the launch; the ram, due
mid-beam, waits for the beam to end and then comes. And `NetSector` sets the level before the world
is rebuilt (called directly: a returning guest's level turned out to be corrected by the host's mission
broadcast before the sector arrived, so the race needs a sector sent before any broadcast -- narrow,
but the call now carries the level either way). Its mutant (the old `NetSector`) is caught.
**Review**: a four-lens read found 10 (2 refuted) plus 3 from the critic: a pilot still held by the
LAST beam's escorts (they never expire) started the next beam's charge the frame it committed -- a pin
now counts only once this cycle's escorts have left the launch point; the ram could dash inside a
shockwave's wind-up (now each waits for the other); the hard-lock check ran with the pilot still
pinned, where the old boss froze too (it now waits for the pilot to be free); the new level on
`NetSector` had no check, and the fallbacks and the ram guard none either; and stale comments. All
fixed; see REVIEW.md, pass 8.

### Slice 2 of the second batch: the outposts, the escort's stops, the waves, the fighters (2026-09-22)

- **Four OUTPOSTS**: small permanent stations 2000 u out on the diagonals, labelled OUTPOST SE / NE /
  NW / SW (`outpost.png`, moved out of `art_unused/`), on the radar as small diamonds and pickable
  there as waypoints. Every peer builds the same four; nothing about them is replicated.
- **The escort's route runs counter-clockwise round them from the south-east** -- SE, NE, NW, SW,
  then the portal (~13,000 u, about four minutes). At each outpost the hauler eases in beside it (on
  the base's side) and **holds 4 s, offloading**: the loading run backwards -- the last-filled pod
  drains while crates stream from it across to the station, a quarter of the load per outpost. The
  offload is drawn only: the cargo aboard, the **x5 pay** at the portal, a lost escort losing it all,
  and a save counting it are unchanged. Guests are sent the stop's time left with the hauler's state.
- **A raider wave every 20 s for the whole route** (from 5 s in; the old cap of four waves is gone).
  **The first wave is light fighters only; every other wave after it brings a heavy too** (one patrol,
  plus one per extra pilot). **Every hunter has half its hull** (`Raider.HullShare`, carried to guests
  by `NetRaiderSpawn`; raids, patrols and the arena keep theirs).
- **The hauler has 262.5 hull** (150 +75%).
- **The owner's report -- carrier fighters flying through an escort's light raiders without firing --
  is fixed.** A strafing pass stopped 1.2 target diameters past it (~33 u for a light), which left a
  slow-moving light inside the fighter's ~100 u turning radius; pure pursuit can never bring a target
  inside the turning circle onto the nose, so the fighter flew laps round it until it went home. The
  pass now also clears one turning diameter (`Wing.TurnDiameter`, from the live stats). In a
  simulation of `Wing.TickFighter` the old rule stalled 9-11 s in every one of 77 set-ups against a
  light drifting at 12 u/s; the new one's worst gap is under 2 s, and the smoke test measures it.
- **A raider that leaves the world is dropped as every ship's target** (`PlayerShip.Forget`, from
  `Hub.LetGo`): the wing's order and the bombers' strike; a turret, whatever carries it, holds only
  what is still in `Combat.Hostiles` (`Turret.StillThere`). A withdrawn hunter
  (the escort over) is freed with hull left, so it still read "alive" and fighters and point defence
  went on reading a freed node. It also leaves `Combat.Hostiles` at once rather than at the frame's end,
  where a turret ticking later in the same frame took it back (found by the new check: an
  `ObjectDisposedException` a frame later). A new bomber strike waits until the last one's bombers
  have all reported back, as it did when a dead target stayed the strike's until then.
- **The intermittent arena-host `NullReferenceException` is found and fixed.** A run in this batch
  caught it with a full trace: `Hub.NetShipState`, reached after every check had passed, as the
  scenario ended -- a guest's 20 Hz ship report landing on a Hub already out of the tree, where
  `Multiplayer` is null. `Net.SenderOf` reads the sender only inside the tree (0 otherwise), and every
  handler that read it directly goes through it (`NetShipState`, `NetIdentity`, `Net.FromPlayer`), with a
  check that calls them on a Hub outside the tree. It has the shape of the exception the RETURN runs
  logged once (after every check, from an engine callback, on the arena host); it is believed to be the
  same, and is off Known broken on that basis -- if one ever shows again, it is something else.

**Tests**: smoke **718 pass, 6/6 runs**, three runs in a row. New checks: the four outposts at their literal positions with their
labels, and one picked on the radar; the route's order and docking; leaving south-east for the first
outpost; wave one three lights at half hull, wave two a heavy at half hull, wave three lights again,
and a twelfth wave on time; the stop at the SE outpost (docked, held 4 s with the clock running, the
pods 300 -> 262 -> 225 while the cargo stays 300, then on toward NE); the hauler's 262.5 hull; the
fighters on a small target drifting at 12 u/s (every fighter keeps firing, no 4 s gap); a hunter called
off while the wing and point defence are on it; a guest watching the host's hauler hold at the SW
outpost and drain. **Mutants** (one scratch run reproducing the baseline): the stop not sent to guests,
the old four-wave cap, the old `DropRaider`, the old overshoot, and the old unguarded RPC sender -- every one caught, in one scratch run reproducing the baseline's 718 checks (the old overshoot measured at 3 shots a fighter and 10-12 s without one). Sweep: the escort frame
is now the hauler offloading at the south-east outpost. **An old check's race, fixed**: "a miner is pinned the same way"
failed once in three runs -- the miner it picked was nearly full, left for home at 110 u/s as the
raider arrived, and a raider cruising at 100 never latched. The check now empties that miner's hold,
so it keeps working, still, while it is pinned.
**Review**: the same five-lens adversarial read found 7 (2 refuted): a guest's pods jumping back up at
the end of each stop, the strike count reset by `Forget`, point defence holding a freed hunter, the
guest's offload check sampling a moment it could miss, and a hull check lost to floating point. All
fixed; see REVIEW.md, pass 7.

### Slice 1 of the second batch: the broadside, the destroyer, faster ships, closer bombers (2026-09-22)

- **The battleship's F is a BROADSIDE**, not a missile. A 0.5 s wind-up while every main turret swings
  onto the cursor (fast enough to come round from anywhere in time), then **three volleys of all four
  guns, 0.25 s apart**, at a shell's own damage; a **10 s cooldown**. The ship steers throughout. The
  bar reads AIMING, FIRING, then the cooldown. Its bar is Space guns, G fire mode, F broadside, Q PD --
  **no missile, no reload** any more. 70.8 damage every 11 s: 6.44 DPS on top of the guns' 23.6.
- **Broadside gear** (the battleship's utility slot, which the missile racks left): Heavy Broadside
  (×1.4 shells at I, a longer cooldown), Rapid Broadside (back 50% sooner at I, ×0.75 shells), Barrage
  Broadside (+1/+2/+3 volleys, slower wind-up and cooldown), Snap Broadside (a wind-up twice as fast at
  I, one volley fewer), each at three rarities. The kit is the **Broadside Battery**.
- **A third class, the DESTROYER** (page 1 of the selector, where MONITOR was reserved): 250 hull,
  201.6 u, **two** main turrets (bow and stern), two PD turrets, and the **missile burst** on F: three
  guided missiles, one at the target and two launched up to 70° either side that curve in, from a
  magazine of **2 bursts** reloaded with R (16 s). Needs a selected target in range. Close in, the fan
  narrows so the outer two can still come round (see DESIGN → Abilities). **The fastest capital ship:
  130 u/s.** Its hull is generated from the battleship's (`tools/make_destroyer.ps1`: 75% of the beam,
  90% of the length, missile pods where the inner turrets stood).
- **The missile racks are the destroyer's** (Salvo, Buster, Seeker, Loader Rack, and the kit's Missile
  Rack), and it has its own four gun lines (Rapid, Heavy, Long, Tracking Twins). **120 drops**, a
  10-part kit.
- **Saves are carried forward, not wiped**: a battleship's rack parts become the destroyer's --
  in the hold, in unclaimed loot, and a fitted one goes into the hold while the battleship takes the
  Broadside Battery (`Equipment.Migrated`, read by `Character.Load`). Format stays 2. A key given to
  the battleship's missile on this machine is the broadside's now (`Settings.Load`).
- **Speeds**: the carrier **116.48 u/s** (12% faster than the battleship's 104), the destroyer 130
  (25%); each class's accelerations and astern speed scale with it; turning unchanged.
- **Bombers close to 283.5 u** before they launch (half the old distance) and their **torpedoes run
  at 112.5 u/s** (25% faster).
- Code: `Classes.Guns/Broadside/Missiles/Wing` replace every two-way class test; the stat sheet is
  one `V(battleship, carrier, destroyer)` per figure. The host's ship report carries the broadside's
  wind-up, volleys and cooldown (`Hub.NetHostState`).

**Tests**: smoke **705 pass, 6/6 runs**, three runs in a row. New checks: the broadside end to end (the turrets swung from ~135° away
onto the dummy within the wind-up, three volleys of four at 0.5 / 0.75 / 1.0 s, 88.5 damage, the ship
under way meanwhile, the cooldown and its refusals); the destroyer's hull, speeds, bar, two guns, burst
geometry at range and close in (all three land), magazine, reload and refit; the broadside gear and the
destroyer's gear on paper; the bombers' launch distance in flight; three-class loot shares; a save file
from before (fitted rack to the hold, counts merged, unclaimed renamed); an old key-bindings file; the
title ship's broadside; a GUEST's broadside is the host's (not started locally, then the wind-up, the
shells and the cooldown reach it); the arena guest flies a destroyer. The save migration, the key
migration and the guest-authority check were each proved by a mutant in one scratch run that reproduced
the baseline's 705 checks (the old `Load`: the hold and unclaimed checks caught it; the old
`Settings.Load`; a guest that starts its own broadside) -- all caught. Sweep: new frames for the
destroyer close up, the burst, the broadside aiming and firing, and the destroyer's bar.
**Review**: an adversarial read of the whole diff (five lenses, each finding attacked by a refuter,
then a completeness critic) found 16 real issues -- the close-range burst circling its target, the old
key bindings, a guest's bar flickering to READY between the wind-up and the host's report, rounded
carrier figures, and a dozen comments still describing the battleship's missile. All fixed; see
REVIEW.md, pass 6.

### Exporting a .exe (2026-09-21)

- **`Warships.sln` added** (Godot's own layout: Debug, ExportDebug, ExportRelease). The editor's .NET
  export refuses a C# project with no `<assembly_name>.sln` beside it -- one "no solution file" error
  per script -- although the run button works without one. A plain `dotnet build` still works: with a
  `.sln` and `.csproj` of the same name, MSBuild takes the solution. `export_presets.cfg` (Windows
  Desktop, to `../Warships.exe`) is committed with it. The exe still needs Godot's export templates.

### RETURN button replaces the victory clock (2026-09-21)

- **A won arena is left on demand, not on a timer.** After the boss dies a **RETURN TO BASE** button
  appears for every pilot (top-centre, under the boss bar); the boss bar shows the tally, e.g.
  `2 / 3 READY TO RETURN`. Each pilot presses it when it has finished collecting, and the host warps
  the **whole party home once every pilot present has pressed it** — mirrors the READY-to-launch flow.
  A held (reconnecting) place does not block it. A pilot in stasis can press it too (it is a HUD
  button, not a ship order). The old 20 s `VictoryWindow`/`HomeIn` countdown is gone.
- **The button appears only on a win** — it is a kill-condition control, so it can never end a mission
  that is still on. A **failed** mission (the whole party in stasis) still auto-returns after 3 s, as
  before: a wiped party is in stasis and cannot press anything, and leaving that on a clock is the only
  way not to soft-lock. `_arenaEndT` is now the failure clock only.
- New: `ReturnButton.cs`; `Hub.SetMyReturn`/`IReturned`/`ReturnReady`/`ReturnTotal`, the
  `RequestReturn` / `NetReturnCount` RPCs, and `NetWon` now carries the party size, not a countdown.

**Tests**: smoke **672 pass, 6/6 runs**. New checks: the button and no clock armed on a win (solo);
one pilot's RETURN is not enough, the party waits, both press → home together (host+guest). Each
proved by a mutant (the gate weakened to "any one"; the clock put back) — both caught.

### The owner's batch: gear, loot, the hauler's runs, reconnection, the tutorial (2026-09-21)

Built foundations first (the save, the tables, the refit), then in the owner's order. Every new
check was proved against a mutant reproducing the old code; see Tests below.

- **Save format 2** (`Game.Version` 2): the last release's characters are listed greyed out and
  refused, by the owner's rule. New on the file: the pilot's **hold** (`[gear_hold]`, part = count),
  **unclaimed loot** and the kills already paid (`[loot]`), the **hints** seen and the off switch (`[hints]`). Each is
  sanitised on load (unknown ids dropped, counts above zero, a base level held to its cap).
- **The batched save** (`Character.SaveSoon`): 2.5 s after the LAST call, each call restarting the
  wait; written at once on leaving, quitting or switching pilot; it never makes a file of its own.
- **Gear: 96 parts that drop** -- 12 per slot type, 4 specialisations x 3 rarities, each leaning hard
  one way (the owner's Elite Hangars III: 2 fighters, +200% speed and damage, +50% turning; Swarm III:
  9 fighters, 22% slower and weaker; Line between). Weapon and Utility per class; Engines, Shield,
  Hull and chips for any capital ship. The upside grows with rarity (x1 / x1.5 / x2), the downside
  does not. Rarities are Common / Rare / Epic (Uncommon and Legendary are gone). The kit stays Common.
- **A stat's multiplier never falls below 0.1**, whatever gear stacks up (it could reach zero or go
  negative). `ShipStats.Sum` adds shares and whole additions from gear, purchases and the character.
- **The refit rebuilds the wing**: a hangar or bay that adds or removes craft frees or launches them
  where they are, fighters listed before bombers on every peer; a smaller magazine or bomb load never
  holds more; a strike counted on the old bombers is called off. **The hull keeps its fraction**
  across a refit (keeping the damage taken let a pilot heal by swapping a shield part on and off).
- **The pilot's hold** replaces the per-class spare chips: every part owned and not fitted, for either
  class. The equipment window (I) shows the ship's parts beside the hold, each with what it does in
  words; FIT / EQUIP / UNEQUIP move parts between them. **K shows base, added, bonus and final**, a
  downside in red.
- **The main guns fire at their own shell speed** (520 u/s), not 4x the missile's: a missile rack
  no longer changes the guns.
- **The handshake fingerprints the tables** too -- every gear part, upgrade row, boss type and each
  class's stat sheet -- so two builds that differ only in a number no longer shake hands.
- **Loot** (`Loot.cs`): the host rolls each pilot's drops at a boss kill -- 2 crates, 3 from boss
  level 6, 4 from 11; Common, then Rare (30%) from 6, Epic (15%) from 11; 70% for the pilot's own
  class or general -- and sends each pilot only its own. They are on the pilot's file at once; the
  crates exist only on that pilot's machine and are taken by flying over them (the pod, in stasis).
  There is **no victory clock**: a pilot leaves by pressing RETURN. The boss's escorts and raiders die
  with it. Crates not flown over reach the hold the next time the pilot enters a world. The radar shows
  your crates; the TIO names the parts a level drops. A pilot in stasis at the kill still gets its own.
- **The hauler**: **AUTO-SELL is locked** until the base owner has beaten the level-3 boss and bought
  the 4462 cr upgrade (3x the average level-5 salvager upgrade, estimated once); until then a full
  hauler waits. **EVASION** (HAULER tab) sets a lone run's chance of getting through past the portal,
  60% +7% a level to 95%; a lost run comes back empty, CARGO LOST. **ESCORT**, beside DISPATCH on the
  floating control, flies the long way round while waves of raiders hunt the hauler (they go for it,
  not the nearer ship), and pays **5x** at the portal; lost on the way, the cargo is gone and the
  usual rebuild starts. DISPATCH and ESCORT are the base owner's alone. **A trip or a save never loses
  a load** any more: what the fleet and the hauler carry counts as home.
- **Reconnection**: whoever leaves on purpose says **goodbye** first (the host or a guest), and the
  goodbye is let out before the socket closes. A guest whose host **drops** without one goes back to
  its own world and **retries 3 times over ~20 s** (2, 8, 14 s), then offers **RECONNECT** (the panel
  and the HUD say which); a host that closed on purpose is not retried. **The host holds a dropped
  pilot's place 90 s** by its character id -- party slot, READY, and in the same world its hull,
  stasis and position -- and restores it on return, even into the arena; a kill while it was away,
  or in the 12 s before the host noticed the drop, is owed to it, and every kill is paid once by its
  serial however it arrives -- the serials a pilot has been paid are on its file (`[loot] paid`, the
  last 32), so not even a restart in between pays one twice. A kill is ONE message (its EXP, share and
  parts together), sized to the kill's own level. A held pilot that was not READY keeps the portal shut until it is back or
  its place lapses. A failed attempt to join or rejoin from offline no longer rebuilds the pilot's own
  ship. **A JOIN gives up in 12 s** (it took ~15.5: ENet's own timeout runs late), **each try to get
  back in in 5 s** (the RECONNECT button's too); a link that drops before the host has let the pilot
  in is a failed JOIN, not a lost session.
  The boss's scale travels with its state, so a guest's boss is the host's.
- **The tutorial** (`Hints.cs`): a small card in the bottom-right corner the first time a pilot meets
  each system -- flying, targeting, abilities, the base, the TIO, multiplayer, raiders, the hauler,
  loot, equipment, a level-up, warp, stasis, the boss -- 8 s, then a 1 s fade, one at a time. It
  never takes input; its clock holds under the Esc menu, the creator and K. Each character remembers
  what it has been shown; **the Esc menu's HINTS switch** is per character and saves at once.
- **Harness**: each multiplayer role flies its own character id (`-host`, `-guest`...); the runners'
  limit is 120 s; every role prints its elapsed time; `OnDisk` reads a saved value; the arena helper
  waits for the arena world to be BUILT, not just for the sector to change.
- **An adversarial review of the gear, loot and hauler work** (three independent lenses, each finding
  then attacked by a refuter; `REVIEW.md`) found, and this batch fixed: guests drew the escort route
  from its first point (the leg now travels in the hauler's flags); a guest could buy AUTO-SELL on the
  host's base; a refit re-armed bombers for free; two stale comments; and nine weak or fragile checks
  (rarity scaling only checked at Epic, Epic's absence at level 10, a one-sided EVASION cap, wave timing
  read from the code's own constants, a lane threshold two frames from failing, a possible id
  collision, a pickup never tested against a ship beside a crate, the wire's drop cap, and a save while
  the hauler is through the portal).
- **A second adversarial review** (the same three lenses, over the whole batch) found, and this batch
  fixed: a kill in the seconds before the host noticed a drop was never paid to that pilot, and a kill
  owed and also received would have been paid twice (now: kill serials, the last 12 s kept, and the
  serials paid saved with the pilot -- else a hard-killed game restarted inside the hold was a loot dupe); a guest
  that fell back offline kept the host's open portal, and a host that went offline with a place held
  kept its solo portal shut; a link lost mid-handshake started the reconnect retries; a pilot leaving
  before it was let in tried to say goodbye; the folded MULTIPLAYER label went stale while retrying; a
  guest read "dropped" for a pilot that left; a guest was taught the hauler's buttons it cannot press;
  parts reaching a pilot at home taught LOOT (crates) instead of EQUIPMENT; the TIO counted a pilot
  reconnecting as one not pressing READY; two stale comments. And thirteen checks were added or made
  tighter: a kill while a pilot is held, the paid-once rule, drops checked part by part against what the
  host rolled, a guest leaving on purpose, pressing RECONNECT, the retry times pinned (2, 8, 14 s, the
  button at 19), a JOIN's 12 s, the hint distances either side of each line, the hint clock under the
  creator and K, a hint never shown twice, the save by 3 s, the restored hull to the point, and a lost
  run saving no stock. The tests' `Unmeet` now also clears a card that is ON SCREEN: while one shows,
  `Met()` was true whatever the trigger did (the new BASE distance check tripped over it).
- **Found by looking at the sweep and by the `-Wan` run**, and fixed: **the equipment window (I)
  scrolls both columns** and ends above the hull bar -- a ship's ten parts ran off the bottom of a
  1080 screen; **a boss that dies takes its warnings with it** -- with the arena lingering after a win, a beam
  half wound up went on charging, then "fired", over a DEFEATED boss; **a ship's unreliable updates go
  by way of the hub** (its state, the host's state for it, a shield flash) -- a pilot's first updates
  could reach another guest before Godot's word that it had joined, "Node not found" in that guest's
  log; an update for a ship not known yet is simply dropped. Only the sender's own ship takes its
  report, as before.
- **Harness**: a drop is simulated as the host sees a real one -- the link cut at the end of the frame
  (`PeerDisconnectNow`) -- not by `DisconnectPeer`, a polite hang-up during which, over a slow link,
  the host went on sending into a peer ENet had already emptied ("max channels: 0").

**Tests**: smoke **672 pass, 6/6 runs**, three runs in a row, and clean over the simulated internet
(`-Wan`); sweep **76 frames, 0 lint**, the new and changed frames looked at (the equipment window with
a full hold, K with gear, a Wide Bay's five bombers, the escort mid-route, a lost run, the hint card,
the arena's crates after a kill, the base's HAULER tab, the TIO's parts line, the Esc menu's switch).
Every new check was proved by a mutant -- the old code put back where there was old code -- in scratch
copies that reproduced the baseline count exactly: 9 gear runs, 9 loot/hauler runs, 11
reconnection/tutorial runs, and 7 for the second review's fixes and checks (35 mutants, every one
caught).

#### Known broken

- **A guest can show one false red figure when a pilot's held hull is restored** on a rejoin: the host's
  report cannot tell a restore from a hit (the host itself shows nothing). A hull "epoch" in the report
  would fix it.
- Not yet done: a session between two homes (see Next up), and flying for balance -- the new gear, the
  broadside and the destroyer, and an escort's twelve uncapped waves.

### Plug and play for any network, a real build handshake, and a full audit (2026-09-21)

**The network, diagnosed on the developer's own:** a TP-Link (UPnP on) behind the provider's cable
modem (192.168.4.1, silent). Godot's `Upnp` refused the TP-Link outright -- its internet side,
192.168.4.40, is private -- and the game said "no router answered". Now:

- **`Router.cs` (new)**: UPnP asked directly in .NET, then NAT-PMP, then PCP, then Godot's UPnP; a
  router behind another asks the one in front too; the real LAN adapter (not WSL / Hyper-V / VPN);
  virtual networks (Tailscale, ZeroTier, Radmin VPN, Hamachi) and the IPv6 address offered to
  friends. On this network the player is now told the one step left: *in the provider's router,
  forward UDP 27015 to 192.168.4.40 (or DMZ it, or bridge mode)* -- after which it is plug and play.
- **`Net.cs` (rewritten)**: the handshake is Godot's authentication step on a build fingerprint
  (`Net.Protocol`), replacing NetHello/NetRefused -- a refused player is told why, and nothing is sent
  for an unverified peer. Joining keeps your world running until a host answers; names are looked up
  off the main thread; IPv6, `[v6]:port` and pasted URLs are read; a dropped friend goes in ~10 s;
  every leaving route saves (a host closing, a failed connection); HOST no longer silently drops
  guests; a port that cannot open says why. Guests' requests go through `Net.AskHost` and the host
  checks the sender with `Net.FromPlayer` (BUY and DISPATCH accepted anyone before).
- **Multiplayer**: a late joiner is caught up (mission, raiders, a win; pulled into the host's world);
  ships get their pilot's upgrades and gear after a scene change (the host judged guests at base
  stats after any trip to the arena); the boss is scaled to the real party (it counted zero ships and
  was always a solo boss); guests see the boss at zero, their own pinned state (a raider's web never
  held a guest), another ship's warp flash fade, a lost fleet ship's burst and its rebuild counting
  down; a silent peer stops coasting after 0.5 s and releases its trigger after 1 s; NaN positions
  from a client are refused; world events go only to peers in that world (`Hub.ToWorld`); cosmetic
  traffic rides its own channel; raider updates split under the MTU; guests see raiders a quarter
  second ahead of their last report (`NetPose`); a guest's telegraphs end when it is judged; a joining
  guest repeats its introduction (a lossy path drops early packets).
- **Bugs fixed along the way**: the live death beam held no line and was not drawn for 2.65 of its
  3 s; Tab and a click could select a missile; QUIT TO MAIN MENU from the arena landed in the hub;
  leaving for the arena did not save the base; a bounty was lost if you quit in the four seconds
  before home; new miners and salvagers started below their upgraded hull; a direct hub entry made a
  new character when the oldest file was another build's; a guest could buy two levels with one
  click over lag; two side windows could stack; the multiplayer panel sat under the HUD buttons.
- **Exit**: `Game.Quit` is the one way out. The "2 resources still in use at exit" were cannon.wav
  and missile_whoosh.wav still playing (not a cache); and quitting within seconds of hosting crashed
  in Godot's Upnp thread. Both gone.
- **Consolidated** (one of each where there were several): `Combat.World` spawns every shell and
  torpedo (five hand-written copies); `Combat.Nearest` (ten nearest-searches); `Combat.KeelCovers`;
  `IRaidTarget` (six type switches) and `UtilityShip` (the fleet's hull, pin, loss and rebuild, twice);
  `Sprites.Fit` (eight sprite-sizing copies, two static texture caches); one side-window slot; one
  identity path (`Hub.ApplyIdentity`); `Character.Defaults` (nine copies), `Character.ReadSlot`,
  `Character.WriteAtomically` (shared with Settings), `Classes.Sanitize` / `Classes.NameOf`;
  `Progression.Affordable`; `Ui.VBox` / `Ui.HBox` / `Ui.Btn` / `Ui.Clear`. **Removed**: Combat.RayHit
  and the unreachable PD branch that called it, wing hull (nothing could damage a wing, yet the K
  window listed it), `FighterCameraRange`, `Character.Version`, the pre-versioning "bosses" branch,
  `Missions.BossName`, `Hub.MyShipPublic`, `HealthBar.UiScale` and its dead number branch, `RaidScale`,
  `Yard.Categories`, `SfxPool`'s second sound list, `BaseDefense.Hub`, `MenuFoe`'s `new Position`,
  about thirty comments describing behaviour that no longer exists; `capital_ship.png` to `art_unused/`.
- **Tests**: the harness builds its own network (no router, no internet -- the two "environmental"
  failures are gone) and never touches the real router; eleven router scenarios against
  `fakeigd.py`; `run.ps1 -Wan` runs multiplayer through a simulated internet (`wan.py`); the runner
  fails a crashed process. `tools/map.py` writes `version/MAP.md`. Once the bar's sweep stopped
  with no output after the smoke runs (it passed on its own straight after; the cause was never
  shown). Now the sweep retries clearing its scratch folder and reports a failure on a
  `SWEEP FAILED` line, and `verify.ps1` prints a failed step's last lines, not a bare FAILED.


### The title screen's heavies wear the raiders' red

They drew in the art's bare grey, which read as a neutral hull rather than as something shooting at
you. The tint is `Raider.HeavyTint` -- the raiders' own constant, promoted from a literal inside
`Raider` so the enemy on the menu is the colour of the enemy you meet, and stays that way.


### Every smoke role gets its own character

**Checked:** typecheck 0; build 0 warnings; analysers 0; xref 0 unused; smoke **474 pass, 6/6 runs**.

**The "guest base save bug" was not a game bug.** All six smoke processes share ONE `user://` --
`run.ps1` wipes it once for the whole set -- and only the solo role ever made a character. The host
and the guests therefore converged on the SAME file, last writer wins. The check that read "the
guest's file" was reading the host's: it held `name="Arena Host"` and that host's credits, which is
where the 1500 came from. Reading the file the run left behind is what settled it.

Every non-solo role now makes its own character before entering the hub, and the arena guest's base
check is a real assertion again -- it passes, naming the file's owner as well as the figure:
`arena guest: its own base reached its OWN file (Arena Guest, 13845 of 13845)`. So base persistence
across a session was correct all along; nothing could prove it while two peers shared one file.

It also exposed a test that depended on inherited state: the three-player guest edited
`LoadoutFor(Character.Class)` before setting its class, so with a fresh character the two chips came
off a hull that pilot never flies. Class first, then gear.

### The project sets itself up now

**`powershell -ExecutionPolicy Bypass -File verify.ps1`** runs the whole bar — typecheck, build,
analysers, cross-reference, smoke test **×3**, screenshot sweep, integrity — and prints one
verdict. `-Quick` stops after the static checks. Nothing is passed in.

Two things used to need a human, and both were the kind that fail quietly:

- **`typecheck/GodotSharp.dll`** is gitignored on purpose (a build dependency, not game content),
  so every fresh clone or unzipped copy arrived without it and the typecheck **silently dropped to
  the hand-written stub** — the weak check the harness exists to avoid, reporting "0 errors" all
  the while. `typecheck.ps1` now copies it from the NuGet cache when it is missing, and says so.
- **The Godot binary path** had to be typed into every harness call. `tools\find-godot.ps1`
  resolves it: an explicit `-Godot`, else `$env:WARSHIPS_GODOT`, else a gitignored
  `local.config.ps1` at the repo root, else a search of the usual places. `-Godot` is optional on
  both runners now.

`verify.ps1` reports the two sandbox-only network checks separately from real failures, so a clean
run on a real machine reads as clean rather than as two mysterious breakages.

Why this was worth doing rather than leaving in a handover note: the machine-specific knowledge
lived in a session's memory, which is keyed to the folder path. Move or re-extract the project and
it is gone. In the repo it travels with the code.

### The atomic-save checks could not fail

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**433 pass, 6/6 runs, three in a row**; sweep 67 frames, 0 lint.

`Character.Save` and `Settings.Save` write a temp file and rename it over the live one. If the
rename fails they fall back to writing straight over the live file — **and delete the temp**. So
both paths leave identical state on disk, and the checks that looked at the files
("goes through a temp file and leaves none behind") **passed either way**. They could not tell the
fix from the bug they were written for.

Proved with a mutant that breaks only the rename: the file-based check still reported PASS while
every save took the unsafe path. Both saves now record which path they took (`LastSaveRenamed`)
and the checks read that; the same mutant now fails them.

Also verified, by asking the engine rather than assuming: `DirAccess.rename_absolute` over an
**existing** destination returns OK on Windows in 4.7.2 and replaces the contents. That mattered
because the Windows CRT's `rename()` refuses an existing destination — had Godot not worked around
it, the fix would have been inert on Windows since the day it was written, and nothing would have
said so.

### Review pass 3 complete: all 46 scripts read line by line

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**432 pass, 6/6 runs, three in a row**; sweep 67 frames, 0 lint. Six new checks, each with a mutant
that makes it fail.

**71 over-exposed members narrowed from `public` to `private`.** They were public but used only
inside their own type. Two things a blind rewrite would have broken, and which cost 26 of the 99
candidates their narrowing: a **nested type** cannot go private while a public member exposes it
(CS0053), and a **shared declaration line** cannot be narrowed when only some of its declarators
were cleared. Interface members (`IHittable`) and anything either harness touches were excluded up
front. `public` → `internal` would have changed nothing: one assembly, same reach.

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

### The base is part of the character, and leaving writes it

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**473 pass, 6/6 runs**; sweep 67 frames, 0 lint. Three mutants, one at a time.

#### Added

- **The base economy persists.** Credits, ore, salvage, every upgrade level and every category's
  invested total live on the character now, so a pilot comes back to the base they built. Before
  this, only the pilot and the machine's settings were written anywhere.
- **Three ways a base arrives, and only one is the file.** A trip snapshot wins (the party is
  coming home from the arena and the base never went anywhere); a parked base wins next (a guest
  getting its own back); otherwise the pilot is arriving in their own world and the file is what
  they left behind.
- **A guest's own base is the parked one.** A guest standing in someone else's base still has one
  of its own, set aside. Writing the visible one would hand it a base it never built; writing
  nothing would lose the one it did.
- **Leaving a session writes that peer's character**, wherever it happens: PLAY OFFLINE, the host
  closing the lobby, a connection that failed. They all pass through `StartOffline`, and a session
  can end with nothing else moving — no scene change to save on its way out, and the autosave up
  to 30 s away.
- Deliberate changes save at once (a purchase, leaving); income ticking over autosaves every 30 s.

#### Two checks that were not checking

- **The reflection round trip only covered what it SET.** A field left at its default reads the
  same before and after whether it is saved or not — so the base fields were added, not saved, and
  it passed. There is now a **field inventory**: every static field of `Character`, public and
  private, against the list this test exercises. Add a field and it goes red until it is either
  set by the round trip or named as deliberately not persisted. *This is the part that notices.*
- It also swept public fields only, so `Spares` — `private static readonly` — was never covered
  at all, by either check.
- **The first "a guest that leaves saves its base" check was vacuous.** Going home is a scene
  change, and `Yard._ExitTree` writes on the way out whatever `Net` does; the mutant passed. The
  real check ends a session with nothing else moving, and the mutant fails it with a stale figure.

### Sound levels live in the sound, and a save knows which build wrote it

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**468 pass, 6/6 runs, three runs in a row, identical every run**; sweep 67 frames, 0 lint.

#### Changed — audio

- **Every effect is stored at the level it is meant to be heard at**, and `Sfx` plays it at 0 dB.
  The trims that used to sit at the call sites (`-6`, `-8`, `-2`, `-9`) are baked into the files by
  `tools/gain.ps1`, so a sound's loudness is a property of the sound rather than of whoever happens
  to play it. The one exception is the hit, which still fades with the length of the shot that made
  it — that is a function of the shot, not a level.
- **A carrier's fighters have their own report, 35% quieter** than a capital ship's
  (`laser_fighter.wav`).
- **A missile launch is 35% quieter** (`missile_whoosh.wav`). **The impact is untouched** — it was
  already right, and its file is re-cut at exactly the level it used to play at.
- **The battleship's gun has its own file** (`cannon.wav`) instead of being the impact played
  quietly at a higher pitch, so the impact can be tuned without moving the gun with it.
- Originals are in `retired/sfx/`, which carries a `.gdignore` so the build does not import them.

#### Changed — sizes

- **`MenuFoe` declares a world length and derives its scale from the art**, exactly as
  `PlayerShip.FitClass` and `Raider` do. It was the only place left carrying hand-picked
  multipliers — 0.55, 0.40, 0.42 — which say nothing about how big a thing is and stop being right
  the moment the art is recut. The lengths are the real raiders' lengths, so the title screen shows
  the enemies at the size you meet them at.

#### Added — the build stamp

- **`Game.Version`**, one number, bumped by one per release that goes out as a zip. A character
  file records the build that wrote it; loading one from another build is refused **in
  `Character.Load`**, not in the screen, because `Load` is the only way a character reaches memory.
  The select screen greys the row, disables PLAY and shows `Game.IncompatibleNote`.

#### New checks

| Check | What it pins |
|---|---|
| a carrier fighter's report is 35% quieter, IN THE FILE | 0.650, measured from the asset's own bytes |
| a missile launch is 35% quieter than it was heard before | 0.650, against the retired file at its old trim |
| the impact is exactly as loud as it was | 1.000 |
| a carrier's fighters have their own report | the routing, not the file |
| the battleship's guns have their own file | as above |
| the title screen's enemies are the size the game's raiders are | equality with `Raider`'s lengths |
| everything a character holds survives a save and a load | **enumerated by reflection** |
| a character from another build is listed, and marked unplayable | the gate |
| this run starts with no saved characters | the harness's fresh slate, asserted |

**The level checks read the asset, not the code.** A check on loudness that reads a number in the
code would pass whatever the file contains. They also do not use `GD.Load`: an imported
`AudioStreamWav` is what Godot made of the asset, so measuring it answers a question about the
importer. The first version did exactly that and reported a ratio of 1.000 for two files that
differ by 35%.

### A multiplayer pass: three things a guest was not being told

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**454 pass, 6/6 runs**; sweep 67 frames, 0 lint. Three mutants, one at a time, each reproducing
the real old code.

Every `AnyPeer` RPC in the build was re-read first, and all four that carry identity validate their
sender correctly (`NetIdentity` refuses a peer describing anyone but itself, `RequestReady` keys off
the sender, `PlayerShip.NetState` refuses anyone but the owner, `NetHostState` refuses anyone but
the host). No authority hole was found. What was found was the other half of the rule — **every
visible state reaches guests** — and three pieces of it did not.

#### Fixed

- **The boss's super-move bar sat at zero on a guest, for the whole fight.** `_beam` and `_charge`
  only tick under `Net.Sim`, so on a guest they never moved and `SuperFill` was always 0. The
  countdown now rides the packet that already carries the boss's position, and a guest runs the
  clock down itself between packets so the bar moves smoothly at 10 Hz — a figure corrected thirty
  times a second cannot drift.
- **The escorts' triple-length boost plume was single-player.** `Boosting` and `Shivering` are read
  by `_Draw` and were host-only, so a guest drew neither that plume nor any raider's boost plume.
  They ride the raider packet as two bits.
- **Reaching the main menu did not end the network session.** Every other session-scoped thing is
  reset there — `Hub.Sector`, the trip snapshot, the combat-music flag — each with the argument
  that covering every route back matters more than covering the Esc menu's quit button. The session
  itself was not, because the menu used to be sprites and structs with nothing to say on the wire.
  It builds a real `PlayerShip` now, so a session surviving into it means the title screen sending
  state on an RPC path no peer has. The mutant showed the rest of the damage: two later checks saw
  a live socket in what is supposed to be single player.

#### New checks

| Check | Mutant that had to break it |
|---|---|
| the boss's super-move bar runs on a guest | the countdown never leaves the host — *0.0 → 0.0 s, fill 0.00 → 0.00* |
| a shivering escort says so on the wire, and the bits change when it goes | host-only `Boosting`/`Shivering`, empty `NetFlags` — *(0,0)* |
| reaching the main menu ends the session too | the menu leaves the session running — *and two later checks fail with it* |

The guest-side bar is sampled only once a figure has **arrived**: before the first packet the
countdown reads 0, and a first sample of 0 cannot tell "it went down" from "it never started".

#### A leak found and released

`Sfx`'s stream cache is static and held every `.wav` for the life of the process, so the engine
reported them as `resources still in use at exit` — the same message `Music` already carries an
`_ExitTree` to avoid. `Sfx.Release()` stops the voices and disposes the streams; `Play()` rebuilds
the pool and reloads on demand, so it is safe at any time. Confirmed in isolation: five resources
at exit became four.

### The title screen flies the real battleship

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**447 pass, 6/6 runs, three runs in a row**; sweep 67 frames, 0 lint; the menu looked at.

#### Changed

- **The menu ship is a real `PlayerShip`** with `Demo` set: the same turrets, shells, missile,
  warp and autopilot the game flies. It reads no keyboard and no mouse; the menu is its pilot.
  What the title screen shows is the battleship, or it is a bug in the battleship.
- **Heavy fighters and a webifier** join the light fighters, as real `Combat.Hostiles` the ship's
  own guns acquire. Heavies and the webifier **hold a standoff and circle** rather than making
  passes, so there is always something in frame; the webifier does not shoot at all — it paints
  the hull with a tether, the same mechanic the death beam's escorts use.
- **It dodges an area shot.** One is called every 15 s and lands 8 s later. With **4 s to go** the
  ship turns its bow away and jumps **500 u** — the warp runs along the keel, so the turn *is* the
  aim — and the 3 s warm-up puts it clear **a full second before impact**. Then it flies itself
  back to station. Every number is the ship's own: its turn rate, its warm-up, its autopilot.
- **The diorama has a camera** (zoom 1.45) and sits in the band between the title and the panel.
  Zooming the view is the honest way to make ships read at a glance; scaling the sprites would
  make the title screen show a ship that is not the size of the ship.

#### The self-containment rule is reversed, deliberately

`MainMenu` used to say it touched no game classes so that no gameplay change could break it. The
cost was that it could show something the game does not do. It is a small **world** now: it
registers hostiles, wires `Combat.OnFlash` / `OnShell` / `OnTorpedo`, and drops all of it in
`_ExitTree` — every hook is a lambda holding the node, and one left behind is a freed node the
next world's shot calls into.

#### New checks

| Check | What it pins |
|---|---|
| the title screen flies a REAL battleship | `Demo`, and the class |
| two missiles on a 5 s reload and a 500 u hop | the retune survives `Init` |
| heavies, a webifier and light fighters, all real targets | every foe is in `Combat.Hostiles` |
| pushed 250 u off station it flies itself home | the autopilot, measured from a real displacement |
| the area shot is called 8 s before it lands | 8.0 s |
| it starts the jump with four seconds to go | 4.0 s |
| and is clear a whole second before impact | 1.0 s |
| the jump actually moved it | 549 u |

#### A ship that passed every check and drew nothing

`PlayerShip.Init()` is what builds a ship — its sprite, its turrets, its stat sheet. The menu
never called it. The node still moved, warped, held station and reported its position perfectly,
so **every mechanical check passed on an invisible ship**; only the sweep frame showed the hull
was missing. *A check on behaviour is not a check on being drawn.* The retune also has to come
**after** `Init`, because `Init` rebuilds the stat sheet from the class and discards anything set
before it.

### A cleaner, more futuristic look, on one palette

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**439 pass, 6/6 runs, three runs in a row**; sweep 67 frames, 0 lint; every restyled screen looked
at. Three mutants, one at a time.

#### Added

- **One palette and one type scale, in `Ui.cs`.** Three stacked surfaces — `Deep`, `Panel`, `Card`
  — plus `Line`, `Accent`, `Text`, `Dim`, `Good`, `Warn`, `Bad`, and five sizes (56/22/17/14/12).
  The old code set font sizes ad hoc — 11, 12, 13, 14, 15, 16, 20, 22, 34, 64 — so no two headings
  on different screens matched, and secondary text was white at some opacity rather than a colour.
- **Flat surfaces, 10 px corners, hairline borders, a soft shadow.** Nothing is shaded any more, so
  `StyleBoxFlat` does it natively: the texture generator that baked nine-patch images to fake a
  gradient and a lit top bevel is **gone**, along with its cache and its signed-distance corner
  maths.
- **Reusable blocks**: `Ui.Heading` (small caps, accent, with a rule under it), `Ui.CardWrap` (a
  row that reads as an object on the panel), `Ui.Tab` (a toggle whose live state is the theme's
  accent box), `Ui.Lbl`, `Ui.Panelise`, `Ui.Style`.
- **The drawn HUD reads the same palette.** The ability bar, hull bar, boss bar and health bars are
  not Controls and cannot inherit a theme, so they build their own boxes with `Ui.Box()` from the
  same colours. One palette, two rendering paths. Their boxes are built once: `_Draw` runs every
  frame.

#### Changed

- **The title belongs to the top of the screen, not to the menu.** WARSHIPS was stacked with the
  menu panel and the pair centred together, which put the word across the capital ship. It is
  pinned near the top now — and the diorama moved up with it, because a centred panel then landed
  on the ship instead. Three bands: title, fight, menu.
- **Your hull bar and the boss's keep their own colours.** They are the two bars that mean *how
  close is something to dying*; an accent-blue health bar would be a lie about what it measures.
  Only their tracks come from the palette.
- **The stats window derives its width from one constant.** It was pinned between two offsets 576
  apart while its contents asked for more, so the panel grew past the offset and hung off the right
  edge of the screen.

#### Three bugs the restyle exposed, all older than it

- **The theme never reached a single button.** A theme set on the root window does not cross a
  `CanvasLayer`, and every piece of UI here hangs off one — so for the project's whole history
  `Ui`'s Button entries reached nothing and every button drew Godot's stock theme. It hid in plain
  sight because the stock theme is also a dark rounded rectangle; the one visible symptom, a
  `font_disabled_color` that did nothing, was never chased. Found by setting the themed fill to
  pure red and counting pixels: **zero** in any frame, while the theme itself still reported the
  red. See *Traps* in `DESIGN.md`.
- **The Esc menu's music row opened with nothing selected.** It finds the live step by matching
  `Settings.MusicVolume` against the five the menu offers, and the default was `0.6` — not one of
  them. Now `0.75`, and `0.75` rather than `0.5` on purpose: a default equal to the value a check
  sets is a check that cannot fail.
- **`HaulerHud` was styled by nobody.** The new check found it, not the audit.

#### New checks

| Check | Mutant that had to break it |
|---|---|
| all 19 buttons take the game's theme, not Godot's | `Panelise` sets the panel box but not the theme *(the real old code)* — 4 tabs unthemed |
| the Radar and Music rows show exactly one live choice | the old `MusicVolume = 0.6` — *0 of 5 lit* |
| the harness runs at 5% master volume whatever the settings say | no cap *(the real old code)* — *75.0%* |

#### A regression caught in the frames, not by a check

Moving the warp readout to the palette drew it green on a full green hull bar and **WARP READY
vanished**. It now takes its colour from what is under it. *The UI lint could never have caught
this: it measures position and width, not contrast.* This is the argument for looking at the
frames rather than trusting a clean lint.

### The test harnesses run at 5% master volume

An automated run is not a game session: one smoke test launches six real Godot windows and the
sweep one more, all playing combat audio with nobody listening. `Settings.TestVolume` is a runtime
override — `Save()` never writes it, so it cannot leak into the player's `settings.cfg`, and every
later `ApplyVolume()` honours it, including the ones the menus trigger.

### The death beam: escorts first, and an honest tell

**Checked:** typecheck 0 errors; build 0 warnings; analysers 0 findings; xref 0 unused; smoke
**435 pass, 6/6 runs, three runs in a row**; sweep 67 frames, 0 lint; the bar and the beam looked
at. Eight mutants, one at a time — every new check was
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
- **The moment the web lands, the aim is final.** The boss stops turning as well as moving: the
  escorts existed to stop the pilot, and once they have, the line on screen is the line that fires.
  Tracking a target that cannot dodge would be the beam chasing something pinned, which reads as
  the game cheating in its own favour; freezing reads as the trap closing. It also puts the pilot's
  last chance *before* the web rather than after it. The lock is cleared at the start of every
  charge, never carried from the last one, and killing the escorts leaves the boss tracking all the
  way to the shot.
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
| the aim never locking (tracks through the web) | once the pilot is webbed the boss stops turning — *turned 0.1550 rad, its full 0.3 rad/s across the 0.5 s window* |
| the aim locked from the first frame | while the pilot is free the boss keeps turning onto it — *0.600 → 0.600 rad off, no movement at all* |

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

*(The two smoke checks that could only pass in a sandbox with no router and no internet now pass
everywhere: the harness builds its own network -- see the 2026-09-21 section. The non-atomic
character save is fixed -- see Fixed, below.)*

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
  turrets dark, a countdown over it) for **24 s**, and the pilot flies an **escape pod** (24 u,
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

### Known broken (the state at 0.3.0, kept as that release's record -- the current list is under Unreleased)

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
