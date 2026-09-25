# Warships — design brief

A new game reusing Space Fleet Idle's art. **Your ship is your character** (Terraria-style): it
lives in a persistent hub, and you take it through wormholes into instanced hostile systems, your
own world, or someone else's.

This file records the spec as given, plus the architecture that follows from it. It is the thing to
read first; nothing here is final until the open questions at the bottom are answered.


> **Every change must be recorded in `CHANGES.md` before the work is called done**, and that file is
> pruned on every update rather than accumulated. Durable reasoning moves here; the moving state
> stays there.

---

## The loop

```
HUB (safe, idles)                    WORMHOLE                 HOSTILE SYSTEM (instanced)
  belt + sun above base   --ore-->   [ right ]  -- you fly -->  clear enemies, take loot
  salvage wreck to left --salvage-->                                     |
             ^                                                          v
             |                          REFINERY  <-- drop the haul (big injection)
             |                              |
             |                              v
        haulers out the right portal <-- TRADE GOODS  --escort or idle-penalty--> sold
```

1. **Idle floor.** The hub produces **ORE** and **SALVAGE** only, 1/sec each at baseline.
2. **Active spike.** Clearing an instanced system is where real material comes from.
3. **Refining.** Dropped loot converts into **trade goods** — the thing worth selling.
4. **Selling.** Haulers run goods out through the right-hand portal. Escort them for full value,
   or let them go unattended in non-interactive mode for a penalty. Haulers are worth **8× the
   passive rate**, so the sell run is the main throughput, not the idle tick.

## The hub

- Safe. Nothing hostile spawns. It idles whether or not you are present.
- A small asteroid belt **above** the base, orbiting a **small sun**.
- A **salvage wreck** to the **left** of the base, somewhat smaller than the old behemoth.
- A **wormhole to the right** — outbound trade, and the way into other systems.
- The **EQUIPMENT BASE**, a pad to the **right** of the base with caution tape across it. EQUIPMENT and
  the RECYCLER are its windows. A part is levelled or scrapped only over it, and only 10 s clear of
  combat (`Landmarks.Serves`). Its windows open anywhere to look.

## Resources

| | Source | Used for |
|---|---|---|
| **Ore** | hub belt, mined | refining into trade goods |
| **Salvage** | hub wreck, cut | refining into trade goods |
| **Trade goods** | refinery | sold via hauler runs |
| **Credits** | selling goods | upgrades |

Everything starts at **1 per second** and scales from there. No third currency.

## What carries over from Space Fleet Idle

Reused as-is or lightly adapted — these were built and validated:

- All 26 sprites
- MSDF font settings and `Txt` (measure-then-draw, never clips)
- `HealthBar` with constant on-screen sizing
- The main menu diorama (renamed **Warships**)
- `Econ` curve helpers, `Sound`
- The audit tooling in `tools/`

## What is deliberately NOT carried over

- Outposts, belts-at-range, the 15-site cap
- Bounty, ore grade, industrial control, the faction triangle (Concord/Syndicate)
- The old tutorial
- Global fleet caps tied to refinery nodes

---

## Settled decisions

1. **Hybrid control.** You fly your own ship directly (WASD, nose follows the mouse) and order a
   fleet around it RTS-style. Input, camera and combat are built for both.
2. **The sell run is a choice at dispatch.** ESCORT: fly with the hauler round the four outposts while
   raider waves hunt it, for **5x** the pay if it reaches the portal. DISPATCH: send it alone, and it
   gets through with the EVASION upgrade's chance -- or its cargo is lost past the portal. (The
   owner's 2026-09-21 answer; it replaced "alone for a reduced payout".)
3. **Real multiplayer, now**, peer-to-peer over ENet — one peer hosts, the rest join. Not architected-for-later.

## The authority model

The one thing that cannot be retrofitted, so it is in from the first file:

> **The host owns the world. Each player owns their ship.**

| Host decides | Client decides |
|---|---|
| resource ticks, enemy spawns and AI, loot rolls, refining, hauler dispatch and payout | its own ship's thrust and heading |

A client never says "I now have 500 ore" — it says "I am pressing thrust", and the host publishes
where everything ended up. Ship position is owner-authoritative at 20 Hz with dead reckoning between
updates, because input latency is the one thing that has to feel instant; an `OwnerId` check on the
receiving end stops any peer shoving another's ship around.

**Guests ask; the host acts.** A guest's ability key is one RPC to the host (`RequestAbility(id,
target NetId)`); the host checks the sender owns that ship before doing anything. The owner's aim
point, guns key and fire mode ride along with its position at 20 Hz; the host fires the guns from
them. The host sends back, at 10 Hz per ship: hull, PD window and recharge, missile magazine and
reload, the broadside's wind-up, volleys left and cooldown, the fighters' target, and every wing
craft's position and state. It also sends hit flashes,
the dummies' readouts, and each torpedo launch — guests fly a cosmetic copy of a torpedo, since its
run is straight and steady.

**Identity is owner-announced, and rides on the Hub.** Name, colours, class, pilot upgrades, gear
and the levels bought for that gear are the one client-owned state. Each owner broadcasts its own;
everyone stores it in `Net.Players`, sanitised on arrival. It goes through the Hub rather than the
ship because the Hub exists on every peer before any ship does. **A ship's sheet is a function of
its own pilot only:** `Equipment.Bonuses`/`Adds` take the levels to lift by and a ship passes its
own (`PlayerShip.Levels`); `Equipment.LevelOf` is the pilot at this keyboard, for its own windows
and prices, and never reaches a sheet. Levels are taken in one way, `Equipment.SanitizeLevels`,
whether they come off the wire, off disk or onto a ship.

**Ships are rebuilt on every session change.** They are keyed and authorised by peer id, and
`LocalId` changes on host, join and drop. `Net.SessionChanged` fires; the world throws its ships
away and respawns from `Net.Players`.

**Offline is not a separate mode.** Single player is a host with no peers, so there is exactly one
code path and offline can never drift from online.

### What the host must never take on trust (2026-09-22)

Four rules, each of which was once missing, each now held in ONE place so the next thing that
arrives on the wire inherits it rather than repeating it:

- **A claim is bounded before it is spent, not after.** The affordability gate read the raw wire
  level and the clamp landed on the line below, on the stored field. Bound the value, then read it.
  And bound by TRIMMING, not by refusing: a claim refused whole makes an honest pilot past the cap
  fly a stock hull (`Progression.Afford`).
- **A wreck does nothing.** A guest's 20 Hz report is built BEFORE the host's 10 Hz word of its own
  death arrives, so anything the wire writes -- a trigger, a key -- comes back armed for a round
  trip. Put the liveness rule on the field every reader shares (`PlayerShip.Trigger`) and on the
  one door presses pass through (`DoAbility`), never in each gun.
- **`Refuse` is the owner's courtesy; it is never the guard.** It runs on the guest so the slot can
  say why at once. A press the host acts on must be held to something the host decides.
- **The meter belongs on the expensive ANSWER, never on the door.** A guest's request is also how
  it learns what it is owed -- a held place, the world it should be in. Rate-limiting the handler
  drops the report with the answer, and a reconnecting pilot stays where it spawned. `Net.Metered`
  guards the catch-up; the report itself is always heard.

And one shape that keeps recurring: **a thing the host simulates has state a guest must be TOLD,
and "it was sent once, at creation" is not telling.** A deployed turret's hull, a carrier's strike
target, a docked arm: each looked replicated because the object itself was.

## The idle economy

One node runs it: **`Yard`** (`Hub/Yard`, the same path on every peer, so its RPCs arrive). It
owns the stock, the upgrade levels, the fleet, the base's service arms and their queue, and the
hauler. Every number is in `Economy.cs`. It is host-owned: the host simulates, sends totals and
levels once a second and ship and hauler state ten times a second; a guest's own yard is parked
(banked first, so nothing its ships carry is lost) while it visits and restored after.

- **Miners and salvagers** (1 of each to start, **up to 5 of each** via +1 upgrades at 400, 800,
  1600, 3200). 100 hold, 110 u/s, **2 units/s**. Miners take different asteroids, nearest the base
  first; salvagers spread over the face of the wreck toward the base. The miner's beam is one steady
  shaft; the salvager's is a **narrow, sweeping scan** of thin arcs. A **hollow yellow bar** over
  each ship fills with its hold. Sprites: the developer's small X-shaped ship, mirrored exactly
  symmetrical, recoloured by part (miner ore brown with white; salvager safety orange with black
  caution chevrons).
- **The base's five service arms** each take **one miner or salvager at a time**, loading through
  their **open face** as if they were open-topped containers: the north side, tilted 30 degrees
  with the four diagonal arms. **Blue hologram bars** across each open face flash gently (brighter
  when the arm is taken). A full ship takes the free arm nearest it; if all five are taken it
  **joins the queue** (a line north-east of the top arm) and the head of the line takes the next
  arm to free up. Never two of the fleet holding one arm.
- **Every place a craft pulls up to is a dock** (`scripts/Docks.cs`), a row measured off its
  station's art: the base's five arms, and the four clamps on each outpost's corners. Which station
  carries which is its place's row (`Landmark.Docks`), reached by the landmark's id. A craft finds
  its spot by one arithmetic (`Dock.Berth`: its own length, its nose 4 u off the open face) and
  swings nose-in at one rate (`Docks.NoseIn`). **Who holds a dock is not the dock's business**: the
  Yard reserves arms for the fleet because an unload is a delivery. A lane's **courier** delivers
  nothing and holds nothing: it docks at the base arm nearest its outpost and the outpost clamp
  nearest the base, sits 2 s, and leaves -- and on an arm a miner holds it berths at the outboard
  end of the face, beside the miner rather than under it. **The base's rows are in the order the
  wire sends an arm's index** (`Yard.NetState`): append only.
- **The hauler** lands on the base's **bottom pad, enlarged to 140 × 85 u**. The pad and the
  portal share one horizontal line (y = 219), so it always moves perfectly flat. Landed, it is drawn
  at **65%** of its 200 u flight size; lifting off it grows back, and settling it shrinks, with a
  hover wobble — the illusion of climbing and descending, though it never leaves the plane.
  It has **six cargo pods** painted on its hull; it starts with **one working pod of 300**, and the
  HAULER tab adds pods (up to 6: 500, 1000, 2000, …) and pod size (+10%). Docked, it loads from the
  stock (the larger pile first) with a stream of motes, each pod lighting as it fills. A **floating
  control with a per-pod progress bar** -- **DISPATCH** (alone, showing its chance) and **ESCORT
  x5**, the base owner's only -- sends it once **at least one pod is full**. With **every pod full it
  leaves by itself only with AUTO-SELL**: a one-off 4462 cr upgrade (3x the average level-5 salvager
  upgrade, estimated once and fixed) that needs the base owner to have beaten the level-3 boss.
  A lone run: lift off, slow flat run east, blue aura, warp out, 30 s away (it gets through with the
  **EVASION** chance, 60% +7% a level to 95%; if not, its cargo is lost), warp back (the sale paid,
  1 credit per unit, or CARGO LOST), drift back, turn 180°, settle.
- **An escort** flies `Hub.EscortRoute` instead, the only move off the lane: **counter-clockwise round
  the four outposts from the south-east** -- SE, NE, NW, SW, then the portal (~13,000 u, about four
  minutes). It eases in beside each outpost (230 u in, on the base's side, clear of the station) and **holds 4 s offloading
  a quarter of the load**, drawn as the loading run backwards: the last-filled pod drains while crates
  stream from it across to the station. **The offload is drawn, not paid**: the cargo aboard, the 5x
  sale at the portal, a lost escort losing all of it and a save counting it are exactly as they were
  (`Hauler.Delivered` / `ShownCargo` are display only). The stop lives inside ESCORTING (`_stop`), not
  a state of its own, because the waves run on the state's clock and the lane-snap, the route line
  and the guests' easing all key on ESCORTING; guests are sent the stop's time left with the rest of
  the hauler's state. A raider wave hunts it **every 20 s from 5 s in, for as long as the run lasts**
  (their `Quarry`): the first wave three light fighters, every other wave after it a heavy as well (one
  patrol, plus one per extra pilot), all at **half hull** (`Raider.HullShare`, carried by
  `NetRaiderSpawn`; not `Strength`, which scales their damage too). At the jump they withdraw and the
  sale pays 5x. The hauler has **262.5 hull**. **Nothing is ever lost to a trip or a save**: the Yard
  counts what the fleet and the hauler carry as home (`Yard.Banked`).
- **The outposts** (`Hub.Outposts`, `outpost.png` at 170 u): four small permanent stations `Hub.OutpostOut` out (3000 u) on
  the diagonals, labelled OUTPOST SE / NE / NW / SW, on the radar as small diamonds and pickable there
  as waypoints. Every peer builds the same four in `BuildWorld`; nothing about them is replicated.
- **Upgrades** cost credits, in tabs **MINERS, SALVAGERS, HAULER** (plus **REFIT**) in the BASE
  menu (B). +10% upgrades cost 1.25× the last level; step upgrades cost 2× the last and stop at their
  cap (the button reads MAX); a switch reads OFF / ON, and LOCKED until the base owner has beaten the
  boss it asks for. A guest's BUY is a request the host checks; DISPATCH and ESCORT have no guest path.
- **REFIT**'s RESET is the only way into the ship menu (class, name, colours): 10% of ore, salvage
  and credits — the world's for its host, a guest's own parked totals for a guest — on a second
  click.

## Camera, radar, music, and playing over the internet

- **Camera**: wheel zoom between `DefaultZoom / ZoomOutMax` (33% further out) and `× ZoomInMax`
  (1.5). **Y** frees it; arrows or the screen edge pan it, tethered to `ClassArt.CameraRange`
  (5000 for a capital ship). All in `Hub.MoveCamera`. A boss too big for that ceiling is framed by
  `Hub.BossFramed` sliding the centre toward its far hull end instead of raising the ceiling (the
  owner's open question, 2026-09-25) -- generic from the boss row's own `Length`, gated to actual
  encounters, and capped so the ship never drifts past `BossPilotMargin` from the edge.
- **Radar** (`Radar.cs`): local only, draws what this machine knows; size is `Settings.RadarSize`.
- **Esc menu** (`EscMenu.cs`): the last Esc layer. Multiplayer cannot pause, so it locks the helm.
- **Music** (`Music.cs`, an autoload): both loops always play; their levels cross-fade by mood,
  which the hub sets each frame from the selection and `PlayerShip.InCombat` (host-tracked: dealing
  or taking damage within 12 s). `Music.CombatZone` forces combat in the arena.
- **Leaving** (`Game.Quit`): the ONE way out -- the window's close button, the menu's QUIT and the
  harnesses. It pauses the world, closes the session (saved; router ports closed again), stops every
  sound, and waits in REAL time for the mixer to let go (150 ms) and for router jobs (up to 10 s,
  window minimised). A bare `Quit()` left sounds playing ("resources still in use at exit") and could
  crash inside a router thread.
- **Internet play** (`Net.cs`, `Router.cs`): ENet over UDP 27015, a direct connection, so the host
  must be reachable. HOST starts LAN hosting at once; a background job (`Router.Open`) then tries to
  open the port: UPnP asked directly (our own .NET client), then NAT-PMP / PCP at the gateway, then
  Godot's UPnP as a last resort. If the router that opened it is behind ANOTHER router (its internet
  side is private -- your router behind the provider's modem, the developer's own network), the one
  in front is asked too, directly (a multicast search does not cross a router). What the player is
  told (`Net.Describe`), from the routers' report and a public "what is my IP" lookup:
  INTERNET (every hop opened), MANUAL (one step by hand: the exact forward left -- to the inner
  router's internet side when the outer one is silent -- or "turn the VPN off" when this PC's traffic
  leaves by a VPN), LAN ONLY (carrier-grade NAT, or nothing learned). Whatever the routers did,
  virtual networks friends can share (Tailscale, ZeroTier, Radmin VPN, Hamachi, found by adapter
  NAME) and this PC's IPv6 address are offered too -- IPv6 has no NAT, the one way in left behind
  carrier-grade NAT. There is no relay server or NAT punch-through: that needs infrastructure.
- **The handshake** is Godot's authentication step (`SceneMultiplayer.AuthCallback`), BEFORE a peer
  counts as connected: each side sends `Net.Protocol`, a fingerprint of the build (every RPC's
  signature, every constant and fixed value, the save version) and refuses a mismatch on its own
  screen. Nothing is spawned, sent or relayed for an unverified peer. `Game.Version` is the SAVE
  format; the fingerprint is the SESSION format.
- **Joining** keeps your own world running until a host answers (`Net.Connecting`), looks names up
  off the main thread, and reads IPv6 / `[v6]:port` / pasted URLs. A late joiner is caught up when it
  reports its world (`Hub.NetMySector`: mission, raiders, a win; a guest in the wrong world is brought
  into the host's). A dropped friend is let go in ~10 s; the session saves on every leaving route.
- **Latency.** Host-owned things are followed with `NetPose` (eased, and carried forward along their
  measured velocity for at most 0.25 s). A guest's telegraphs are shortened by its round trip
  (`Net.Arriving`) so they end when the guest's own position is judged. Cosmetic reliable traffic
  (shells, torpedoes) rides its own ENet channel so a lost one does not hold up the rest; raider
  updates go in packets of 24, under the internet's ~1.2 KB.
- **The WebRTC reply window** (R0, measured once on 2026-09-24 by a seven-pair run since deleted, an
  in-process pair over the LAN host candidate): a host applying the reply 5, 10, 15, 25, 35, 45 or
  60 s after the guest made it connected every time, 4-6 ms after the reply, the guest `Connecting` at
  each. Every delay up to 60 s connected, so `Link.ReplyWindowS` = min(60 - 10, 30) = **30 s**. Every
  solo run now holds one pair to it (the host takes the reply 30 s after it was made, and must be
  connected within 2 s of that), in the background from the top of the run. A real internet path is
  unmeasured (network_webrtc.md §3.4, the owner's two-machine test).

## The batch after the review began (signed off by the player), in chunks

- **A (DONE)**: bomber docking; boss 600; fighters 2/shot; torpedoes 15; utility hull 120; guard 0.52 s;
  red missile tips; PD only missiles and light fighters; dummy 2 -> two practice fighters.
  *(As shipped THEN. The 50 DPS pass has since moved every weapon figure in that line -- a
  fighter's shot and a bomber's torpedo are `fighter_damage` and `torpedo_damage` in `Stats.cs`,
  and the rows are the truth. The utility hull is still 120: `Economy.UtilityHull`.)*
- **B (DONE; escorts 3 hull)**: the beam charges **6 s**; meanwhile the boss launches **2 light fighters, 45° to port and to
  starboard**, straight at the player, their boost lasting until they reach it (a pin to hold the pilot
  in the beam unless point defence — or, for a fighter pilot, their guns — kills them); live **3 s**,
  **0.25 s ticks, 50** (half the old tick, twice as long); the boss the pack's `frigate_a` in the owner's
  **red and black** (its skull went with its old art).
- **C (DONE — carrier measured 19.07 DPS; guns 5.9 a shell; control 1080 u)**: **battleship total DPS = 1.25 × carrier's**; **carrier range = 1.5 × battleship's**; the
  battleship's main guns fire **shells at 520 u/s** (their own stat since gear came: a missile rack
  must not change the guns), **not tracking**.
- **D (DONE)**: capital ships **turn in place at ≤ 5% of top speed**, about 10°/s; no strafing.
- **E (DONE)**: heavies **snub-nosed (option b)**; they **wait at the map's edge** nearest their target and,
  once it is pinned, **boost at 700% until 300 u away**; missile within 500 u.
- **F (DONE)**: **Miner hull / Salvager hull** upgrades, +10% a level, **125 cr to start** (25% above the other
  +10% rows), ×1.25 a level.
- **G (DONE)**: the **equipment menu** (I, and a button): Weapon, Engines, Shield (health), Hull (mods), Utility,
  and **5 chips**; the gear is **locked to the class** (no mode switch); items drive the stats they
  cover; common defaults reproduce today's numbers; **each default chip: +5% damage and +5% hull**;
  **gear left on a class stays on it** when the pilot switches class and back. Carriers: "Fighter
  Hangars" in the weapon slot.
- Kept: `Hub.BeginPlacement` (the player will reuse the placement mode).

## The player's specification for the coming chunks (2-8), as given

Recorded here so every chunk builds from the written word, not from memory.

- **Chunk 2 — boss and combat tuning. (DONE)** Boss hull **1200** at tier 0. Boss lasers about **3 DPS** base.
  Cadence: the **death beam every 30 s**; a telegraphed **charge every 30 s on the opposite cadence**
  (15 s apart); **trident missile volleys in between** — **3 missiles, 0° and ±25°, 15 damage each,
  2× the size, ~10% slower, ~20% more range**. **Damage from one ongoing source lands at most once per
  0.35 s** on a player; **persistent mechanics (the beam, zones) check every 0.51 s**; the beam does
  **100 per landed instance**. **Out of combat = 12 s** without dealing or taking damage (music and
  regeneration). **Regeneration: 0.5% of max hull per second in combat, 3% out of combat.** Music:
  in the "target selected, not yet fighting" state the **ambient plays 50% softer** under the softened
  combat track. **Player (battleship) missiles: sharper tip, 20% skinnier, 25% longer, higher detail.**
- **Chunk 3 — the boss health bar (DONE)**: damage leaves a chunk that bounces up and down a few times,
  quickly, turning white as it fades.
- **Chunk 4 — layout (DONE: 1500 u to each field's edge)**: the salvage field ~500 u further left; the mining belt 300–500 u closer; both
  the **same distance from the base, measured to each area's boundary**.
- **Chunk 5 — utility hull and rebuilds (DONE, but NOT at the hulls asked for)**: the 60 and 150 here
  were never built; a miner and a salvager have `Economy.UtilityHull` (120, which is what chunk A
  above said) and the hauler `Economy.HaulerHull` (262.5). Those two consts own the figures.
  Destroyed ones are
  rebuilt at the base after 30 s for **10% of all money invested so far in that category's upgrades**
  (a running total per category: miner, salvager, hauler).
- **Chunk 6 — enemy fighters.** A variable **raider damage x**: **light fighters 1 DPS** (x), **heavy
  fighters 2x**. **Light fighters** (the owner's small fighter, in raider red, **2× a carrier fighter's size**) are
  **webifiers**: within **100 u** they **pin** a target — it is held to **20% of max speed with forced
  thrust and cannot turn** (a soft crowd control). They cruise **as slow as an unupgraded capital ship**
  but **boost to 500% for ~3 s** once about **1200 u** out (the distance ~3 s of boost covers, plus
  100 u), landing **near** the target, not on it, in **formation: ahead, left and right**. **Heavy
  fighters** (the owner's crescent-winged fighter, **one turret** on its spine, **~4× a light fighter's
  size**) hang back **behind the target**, facing it, choosing an angle of approach, until it is
  tackled; then they close to their short, high-DPS lasers. Heavies also fire a **fat missile (at least
  the player missile's size) within 500 u, 7 s to impact, at the target's predicted position**, with a
  **red circle marking the blast** (the boss telegraph's red) — dodgeable. Both types hold **within 90%
  of their weapon range** so they never lose DPS to repositioning. **Patrols: 3 lights + 1 heavy.**
- **New chunk (after 6) — base weapons (DONE)**: a 5 DPS laser at 300 u and a tracking 25-damage missile at
  600 u, ~120 u/s, one every 5 s. **Chunk 4's distance is decided: 1500 u** to each field's edge.
- **Chunk 6 is split**: 6a light fighters and the pin (DONE); 6b heavy fighters and the predicted-impact
  missile (DONE); 6c patrols (3 light + 1 heavy) (DONE). Art for both is staged in `art_unused/`.
- **Open question: pilot points are amplified by percentage gear (a Hull point = +6.25 with the chips) — keep, or make points exact?**
- **The player's next batch, as given (after chunks 7 and 8 unless reordered):**
  1. **(DONE) Internet address**: the host's panel shows the address a friend in another city needs — the
     public IP (asked of a public "what is my IP" service, compared with the router's own report to
     catch shared/carrier NAT) and the port — behind a **click-to-reveal** button. (Today it is the
     router's report via UPnP when that works, else only the LAN address.)
  2. **(DONE) Single player stays silent** (already: no socket or lookups offline — to be proven by a test);
     the multiplayer buttons get a **1 s rate limit**.
  3. **(DONE) Party size scales the boss and the rewards.** (Formula: to be agreed.)
  4. **(DONE) EXP to the next level is always 1000.**
  5. **(DONE) EXP by level**: reward = base × enemy level ÷ pilot level (a level-5 enemy for a level-10 pilot:
     half; a level-2 boss for a level-1 pilot: double). Base: 200 at equal level (to be confirmed).
  6. **(DONE) +250 EXP for a first kill** of a boss (per boss or per boss level: to be agreed).
  7. **(DONE) Raids after a failed mission are scaled like its boss**: a level-n wave is 1.1^n, like a level-n
     boss. Boss "levels" replace tiers (numbering from 0 or 1: to be agreed).
  8. **(IN PROGRESS — see docs/REVIEW.md) Last: a full code review** — every variable, line, function and reference — with a task list
     worked recursively until the code is clean.
- **Chunk 7 — raids (DONE)**: on a failed mission the boss sends patrols in **from the perimeter of the base
  map** against the miners, salvagers, hauler and players.
- **Chunk 8 — the BASE button in the arena, and a dedicated multiplayer arena test. (DONE)**

## Sound and look

- **Sounds are local and cosmetic**: every peer plays what it sees (`Sfx`). Loudness is by the camera
  (distance from the view's centre, and zoom as listener height); per-sound rate limits keep a wing
  from becoming noise.
- **Every fired beam is a row of `Beam.All`**: the colour of its flash and its report, a file of
  `sfx/` at a pitch of its own. Beams that share a file are different notes (your side's climb in
  whole tones above point defence, the raiders' fall below it in minor thirds). Each is gated under
  its own id, not its file's. The wire carries the row's index only.
- **A beam's level is in its file**: every file a beam is heard by is stored at 0.75 of where it was
  made (`gain.ps1`; `BEAM` in `make_sounds.py`). There is no beam volume in Sfx, on a bus or in
  Settings. A second 0.75 anywhere would make the beams 0.56.
- **One look** (`Ui`): shaded panels from small generated textures, one theme for all controls,
  installed on the root window. New UI takes the theme; nothing builds its own panel style.
- **Boss tiers**: 1.1^n, unlocked by beating the tier below, auto-selected at the TIO, saved per pilot.

## The arena

- **A scene change**: `Hub.GoTo` reloads the game scene with `Hub.Sector` set; the host tells every
  guest to do the same. Anything that must survive the trip lives outside the scene (static): the
  host's trip record (`Yard._trip`) and a guest's set-aside base (`Yard._own*`).
- **A boss is a row, and one class runs every row.** `Boss` is everything every boss is:
  hull and damage scaled by level and party, a hostile, host-simulated and drawn on guests from
  `NetState` (flat while `Locked`), telegraphs, the super-move bar, the approach. Everything that
  makes one boss differ from another is its `Missions.BossType` row -- name, hull, art, shape and
  a `BossMove[]`, one row per move, the arrays held in `Lancer.cs` and `Drake.cs`.
  **One ladder of levels, the bosses taking them in turn** (`Missions.ForLevel`): every peer works out
  a level's boss from the replicated level alone, and a peer sent into a world is told the level WITH
  the sector (`NetSector`), because the arena's boss is built from it before any mission report
  arrives -- a guest brought into an arena late used to build it from its own stale level. A level
  counts as cleared whichever boss held it (`Missions.Cleared`, `HighestBeaten`, `Unlocked`): the
  first-clear bonus is once a level, and a pilot's older clears all still count.
- **Telegraph first**: every high-damage boss attack shows a red zone for its whole wind-up
  (`Telegraph`), then the host resolves the hit.
- **A telegraph belongs to its weapon.** The beam and the ram are drawn as **children of the boss**
  in its own frame, and it **holds station** for their wind-ups, so the line shown is the line fired.
  The shockwave stays a world-space circle — it is an effect at a place, not out of the hull.
- **The title screen flies the real ship, on purpose.** `MainMenu` was written to touch no game
  classes so that no gameplay change could break it. That is reversed: the menu now builds a real
  `PlayerShip` in `Demo` mode and real hostiles, because a self-contained menu can show something
  the game does not do, and a title screen that lies about the ship is worse than a title screen
  that breaks loudly when the ship changes. The smoke test covers it, so it breaks loudly.

- **The death beam is a trap you can spring or break.** It opens with two escorts, not a red line:
  they shiver at the launch point while coming round onto the pilot, boost in on a triple-length
  plume, flank **port and starboard**, and web. From the moment they launch **the boss holds its
  position**, and turns only to face the pilot -- the one turn the beam allows. **The charge begins
  when the web has actually pinned the pilot** (the owner's call, 2026-09-22: it used to begin on a
  prediction made at launch, and a pilot running or slipping behind the boss saw it charge before
  anything had pinned them). Kill the escorts first and it still comes, once their web would have
  landed -- a beam you can fly out of; and escorts that neither pin nor die cannot stall it past 5 s
  after launch (`BeamArmMax`: under 6 s, so the beam's whole run ends inside the 15 s before the ram).
  **Beat the lights and the beam becomes dodgeable; ignore them and it cannot miss.**
- **From the charge to the beam's end the boss is HARD LOCKED**: no turn, no move. The red line it
  shows is the line it fires, so the telegraph is a promise, and the pilot's last decision is
  *before* the charge -- escape the web, or leave the arc -- while they still have a ship that
  answers the rudder. A boss that tracked while it charged (it once did, at 0.3 rad/s, until the web
  landed) was the game visibly playing against its own telegraph. The ram waits for a beam to end
  rather than snapping round during it; the shockwave's wind-up holds still too. Only the ram moves
  the boss during a special.
- **The Drake Bastion (the even levels) is the odd boss's opposite in feel**: no escorts and no trap,
  a slow gun that is dodged by moving, and two big specials. **Its figures are its OWN literals.** They
  were once written as a `ThreatMult` times a constant of the other boss's, which meant tuning one boss
  silently retuned the other; the two are not the same fight and must be tuned apart, so each row now
  carries its own numbers and nothing multiplies across. The SCRAP SHOTGUN warps it to 600 u of the
  nearest pilot (a ring shows where, 1 s) and fires a fixed fan -- the same seven lines every time, so
  it is learned, not rolled. The ASTEROID THROW warps it back to 1300 u of the pilot first (the same
  ring, 1 s), then holds a 180 u rock in a tractor beam over a red lane for 7.5 s and hurls it: its path is fixed at the throw (the distance flown as the cube of the time
  -- slow, then very fast), so every peer flies the same rock from one event and a guest shortens only
  the hold.
  **A THROWN BODY'S LANE IS DERIVED FROM THE BODY, never written beside it.** `BossMove.Width` is left
  0 on the throw row and `Boss.Warn` raises the lane at twice the body's radius. The trap it closes:
  `Boss.Scaled` lifts `Radius` one percent a level and would never have lifted a written `Width`, so
  the rock grew out of the red lane warning about it -- 85.3 u past each edge by level 40. `Offset`,
  the clearance the body is held at off the flank, is scaled with `Radius` for the same reason: it is
  HalfWidth + Radius + 40, so a body that grows and an offset that does not ends with the rock drawn
  inside the hull holding it.
  **A BOSS'S ID IS NOT ITS NAME.** `Missions.BossType.Id` ("silver_lancer", "drake_bastion") is a save
  key -- `Character.BossCleared` writes it into `[boss_cleared]` and every build there has ever been
  wrote those two strings -- so the id is frozen and the `Name` beside it is free. `Lancer.cs`,
  `DamageSource.Lancer*` and the runtime keys `boss:guns`/`boss:beam` are all named for the ID, not for
  the screen: the odd-level boss is shown to a player as RUSTY BUCKET and none of them moved. **Its rounds are
  not missiles**: `Slug` is never in `Combat.Hostiles`, so point defence cannot delete a shot that is
  meant to be dodged.
- **What a self-picking gun takes first**: missiles, then small craft, then anything else
  (`Turret.Rank`). What it may take at all is the gun's own `TurretSpec.Prey`: point defence
  `Targeting.PointDefence` (never past the small craft), a turret left standing `Targeting.Sentry`
  (anything hostile; a practice dummy only as a `TargetFilter.Fallback`, never held over anything
  that can die).
- **Test harness trap**: three processes on fixed timings do not choreograph scene changes well; the
  multiplayer arena needs its own purpose-built test.

## Pilot progression and missions

- **EXP is shared**: the host awards it (`Hub.AwardPartyExp`) to every pilot in the session, for
  mission completion and boss kills. Levels need 100 × 1.5^(level−1); each pays one point.
- **Upgrades are flat** and cost 1, 2, 3, … per upgrade. They are applied as the stat's `Flat`
  part, before any percentage bonus. The host gets them with the pilot's identity and validates
  that the claimed purchases are affordable at the claimed level.
- **Missions are host-authoritative**: party = everyone in the session; READY is a request the host
  records and broadcasts; WARP needs everyone ready; the portal opens after a 3 s bar.
- **Level walls (`Unlocks.cs`, 2026-09-25)**: one table of what a pilot's level opens -- ability 1 at 1,
  ability 2 at 3, ability 3 at 6, chip slots at 2/4/8/10/12/14 -- plus the base's boss-beaten rows
  (Auto-sell, the lanes' blockades). **A wall reads the PEAK** (`Character.Peak`, the highest level ever
  reached, on the identity), never the level: a refit costs a level and is the only class change, so
  reading the level would lock again what a pilot had opened. **The host holds it**
  (`PlayerShip.DoAbility`, `Equipment.Sanitize` at the ship's peak); the owner's press, the bar, K and
  the equipment window only say it (`Unlocks.Locked`: `LOCKED · L3`).
- **Trap: a class's list order IS its unlock order.** Abilities 1, 2 and 3 are the class's rows in
  `ClassDef.Abilities` order, skipping `AbilityDef.Weapon` rows (a weapon's own actions) and the open
  hotkeys -- reached by position, never by name. Reordering a class's list moves its walls; a new
  weapon action without `Weapon = true` becomes a walled ability and shifts every one after it. Keys
  never move with the walls (the Carrier's bar reads F E Q, the Warrior's E Q F). A walled row must
  be a press: a held row is polled, and nothing on that path reads a wall (WallChecks asserts it).

## Launch limits and targeting rules

- **Fighters launch at least 0.83 s apart**, always. It is a constant, never a stat, so nothing that
  scales rates of fire can touch it.
- **The missile needs a selected target in range.** Refusals are shown on the ability's own slot
  (`PlayerShip.Fail` / `FailNote`), the pattern for any ability that can be refused.
- **Stealth stops choosing, never hitting.** A filter is asked one of two things: `Chooses` (aimed
  at, homed on, picked -- stealth hides) or `Hits` (a blow that lands where it lands -- it does not).
  A new chooser asks `Chooses` / `Nearest` / `Choosable`; a new area blow asks `Hits` / `Hittable`.
  A boss with nobody in sight aims at the last place it saw anyone and its clocks run on; with nobody
  alive it waits. A launcher's barrel is the same: it holds the last point it saw the pilot its
  warning is up for, so a pilot gone dark is fired at down that line, and the round flies on straight
  while the pilot stays dark (homing is choosing too).

## Damage, death and the two colours

- **Who can be hit**: player weapons hit `Combat.Hostiles`; enemy fire hits `Combat.Players`. A
  player ship's collider is a capsule along its keel (`PlayerShip.Covers`); everything else is a
  circle (`IHittable.Covers`). Escape pods are in neither list: nothing can touch them.
- **Hits** go through `PlayerShip.Hit(damage, from, source)` on the host, which applies the damage and
  tells every peer which side to light on the shield (`ShieldFlash`: one generic hex panel, four sides).
- **A source lands at most once per 0.52 s, and A PROJECTILE IS ITS OWN SOURCE.** The gap
  (`PlayerShip.Incoming`, keyed by the source's name) is there for an ONGOING source — a sweeping
  beam, a ram, an area tick — where one name has to cover every tick of one thing. Three bodies that
  each strike once are three sources: the Lancer's trident passed one name for all three seekers and
  a hull felt only the first, and the Drake numbered its seven scrap pieces by hand to avoid exactly
  that. `Combat.Fire` now adds the firing body's own identity to whatever name it is given
  (`Shots.SourceKey`, `family#n`), so a volley of any size lands every hit while a beam, a ram or an
  area tick — none of which come through `Combat.Fire` — keep the single shared name they need. The
  names themselves are members of `DamageSource` (`Combat.cs`): "boss:gun" and "boss:guns" are two
  weapons on two bosses one character apart, and a typo there suppresses hits in silence.
- **Death**: 0 hull puts the ship into a 24 s **stasis** where it lies (the owner's ruling:
  `PlayerShip.StasisTime`); the owner flies an **escape pod**; afterwards **F** re-boards at 33% hull
  (a request the host decides). A party with a pilot still flying fights on meanwhile; the whole party
  in stasis at once fails the mission. Stasis and hull are host state, replicated with the rest.
- **Hull colour** is the hull. **Accent colour** is the turrets, engines and lighting: turrets,
  plumes on the player's ship and everything it launches, shields, PD arcs. Utility ships' engines
  are always light yellow (`Plume.Utility`). Missiles keep their smoke. The defaults are a **grey
  hull (0.6, 0.6, 0.6) and a white accent**, the owner's; the hulls are the pack's grey art (`Sprites.Fit`
  by way of `ClassArt`, same as every other row) that the hull colour multiplies.
- **Fighters** fly strafing runs: 3 shots, through the target by 1.2× its diameter, turn, repeat;
  they live inside the carrier when docked. **Bombers** park small on its deck, facing the bow.

## Ship classes

**Twelve, three to a page in the selector, all flyable**: the line (battleship, carrier,
destroyer), the freighters (freighter, tender, bastion), the heavy fighters (sniper, warrior,
warden) and the lights (dart, echo, wraith). A number this build has no class for reads as a hull
with nothing on it and shows "placeholder" as its controls line, so a save or a packet from
another version cannot crash it. **Class is chosen in the creator only** — there are no class
hotkeys (the developer removed 1/2).

**A CLASS IS A ROW** (`scripts/Ships.cs`). Its hull and mounts (`ClassArt`), the numbers that
differ from the sheet's defaults (`Nums`, by stat id), the rows only it has (`Rows` — the
railgun's charge, the bubble's pool: they join the sheet, so the K window prints them and gear can
move them), what it is FITTED with (`Fit`), its abilities, its name, blurb and controls line. It
was spread over five files before: `PlayerShip.Art`, a `V(battleship, carrier, destroyer)` helper
inside every row of the stat sheet, an `Abilities` dictionary, a `Classes` name table, and four
two-way tests. Twelve classes written that way is a hunt through five files for each of them, and
a missed test is a class that silently cannot shoot.

**ADDING A CLASS**: a member appended to `ShipClass` (the number is what a character file holds,
so never reorder), a row in `Classes.All`, and the abilities it carries — which are themselves
rows (`Ab.*`). Nothing else in the game is touched.

| | Hull | Length | Top speed | Main guns | Its F | PD turrets | Wing | Sprite |
|---|---|---|---|---|---|---|---|---|
| **Battleship** | 300 | 378 u | 104 u/s | 4, 17.9 a shell | broadside | 2 (slow, τ/3) | — | `battleship_hull.png` |
| **Carrier** | 200 | 283.5 u | 116.48 u/s | — | bomber strike | 3 (fast, τ/1.2) | 3 fighters + 2 bombers | `carrier_player.png` |
| **Destroyer** | 250 | 212.6 u | 130 u/s | 2, 7.5 a shell | missile burst | 2 (slow, τ/3) | — | `destroyer_hull.png` |
| **Freighter** | 400 | 230 u | 85 u/s | 1, 12 a shell | bubble (400 soaked) | 2 | 3 deployable turrets | `freight_hauler_hull.png` |
| **Tender** | 400 | 230 u | 85 u/s | 1, 12 a shell | overdrive (x2 fire) | 2 | 3 deployable turrets | `freight_tender_hull.png` |
| **Bastion** | 400 | 230 u | 85 u/s | 1, 12 a shell | shockwave (1000 u) | 2 | 3 deployable turrets | `freight_bastion_hull.png` |
| **Sniper** | 140 | 120 u | 190 u/s | 1, 6 a shell | railgun (150 at 2500 u) | — | — | `heavy_sniper_hull.png` |
| **Warrior** | 140 | 120 u | 190 u/s | 2, 9 a shell | rush + EMP | — | — | `heavy_warrior_hull.png` |
| **Warden** | 140 | 120 u | 190 u/s | 1, 12 a shell | 6 hunter-seekers | 1, always on | — | `heavy_warden_hull.png` |
| **Dart** | 90 | 70 u | 260 u/s | 1, 5 a shell | barrel roll | — | — | `light_dart_hull.png` |
| **Echo** | 90 | 70 u | 260 u/s | 1, 5 a shell | bullet echo | — | — | `light_echo_hull.png` |
| **Wraith** | 90 | 70 u | 260 u/s | 1, 5 a shell | stealth (5 s) | — | — | `light_wraith_hull.png` |

**Damage**: every figure is a row, and the row is the only place it is written down — repeating one
here is how this section came to claim a 5.9 shell for a gun that fires 17.9. The main gun's shell
and its reload belong to the CLASS (`main_damage` / `main_interval` in that class's `Nums`,
`Ships.cs`): the same weapon runs from the dart's 5 every 0.35 s to the battleship's 17.9 every
2 s. Everything else is a default on the sheet (`Stats.cs`), which a class overrides only where its
row says so — point defence `pd_damage` / `pd_interval` (the warden's mount is the one override), a
fighter's `fighter_damage` / `fighter_interval`, a bomber's `torpedo_damage`, a missile's
`missile_damage`, three to a burst (`PlayerShip.BurstSides`). Turrets, wings and helm read that
sheet and the K window prints the same object, so the two cannot drift. Stat = base × (1 + bonus); reload, cooldown and radius bonuses divide. Bonuses are
saved per character; nothing grants them yet.

**A class is asked what it is FITTED with, never "is it the battleship".** `Fit.Guns`
(cursor-aimed main turrets), `Fit.Broadside`, `Fit.Missiles` (a magazine of bursts), `Fit.Wing`
(fighters and bombers), `Fit.Pd`, `Fit.Deploy` (turrets it drops and collects), `Fit.AlwaysPd`
(point defence with no window). The stat sheet grows each group only for a class that carries it
(a row a class lacks reads 0), the ship builds the matching hardware from the same flag, and the K
window prints the matching figures.

**Sizes are the owner's.** The battleship, by far the largest, is its drawing twice as wide, then 35%
and 25% larger: 378 u and a 43.875 u half-beam. The carrier is 25% smaller (283.5 u) and the destroyer
25% smaller again (212.6 u). The hit capsule is the drawn hull. New ships spawn half the longest
class below the pad (`Hub.SpawnClear`), so any class starts clear of the base.

### The helm: capital ships handle like naval ships

The developer's call: *no strafing, a turning radius, move as if in a medium* — and *slow*: speeds
and accelerations are 40% of their first values, with radii set so the full-speed turn is 20% faster
than it was. Battleship 104 u/s on a 107 u radius; the **destroyer is the fastest**, 130 u/s (+25%) on
the same radius, its accelerations and astern speed the battleship's ×1.25; the carrier 116.48 u/s
(+12% of the battleship) on 127 u, its accelerations and astern speed the same fractions of its top
speed as ever (a half, 5/24, a third), so each class gets under way on its own clock. W is ahead, S astern
(weaker), A/D the rudder. Thrust only ever acts along the keel. Velocity is split into along-keel
and across-keel parts: water drag slows both, and the **keel** kills sideways drift within a fraction
of a second, so the ship goes where it points. Yaw rate is **speed ÷ turning radius**, capped by the
rudder limit, so the rudder does nothing dead in the water and astern it reverses. Measured: a
battleship at full ahead holds 104 u/s at 0.972 rad/s — a 107.0 u circle against the sheet's 107 —
with no sideways drift. The hull does **not** follow the cursor; the battleship's main guns do.

### Abilities and the ability bar

Every class action is an ability (`Abilities.cs`): an id, a default key, and a kind — **Press**
(fires once) or **Hold** (active while held). The bar along the bottom shows each ability's key,
name and live state, with a dark sweep while it recharges. **K** opens a window whose first tab
remaps them: click the key, press a new one. A key another ability holds is **swapped**, never
duplicated; the keys the hub itself flies and opens windows with are refused, and they are a set,
not a sentence: `Abilities.Reserved` (W A S D, Tab, Esc, Enter and keypad Enter, K B L I V, and Y
with the four arrows for the free camera). **C is not one of them** — it stopped opening the ship
menu and was never taken back. Bindings are per class and
belong to the machine (`settings.cfg`, section `keys`, only changed keys stored).

| Battleship | default | Destroyer | default | Carrier | default |
|---|---|---|---|---|---|
| Main guns (hold) | Space | Main guns (hold) | Space | Fighters: attack | Space |
| Fire mode (salvo/staggered) | G | Fire mode | G | Fighters: recall | R |
| Broadside | F | Missile burst | F | Bomber strike | F |
| Point defence | Q | Reload missiles | R | Point defence | Q |
| | | Point defence | Q | | |

A binding saved for an ability that has since changed id is carried over when the settings load
(`Settings.Load`): the battleship's `missile` key becomes its `broadside` key, so a pilot who had moved
fire mode onto F never finds two abilities on it, and its `reload`, which is gone, keeps none.

- **Main guns** swing toward the cursor at τ/4 and fire, while the key is held, along wherever each
  barrel points — a fast flick fires wide. Reach is the class's own `main_range` (`Ships.cs`), from
  the dart's 500 u to the battleship's 1000; 720 u is the destroyer's. **Salvo** fires every barrel once per reload;
  **staggered** fires one every reload ÷ barrels. Same rate: reloads **carry their remainder**
  (`cd += step`); resetting instead rounds each step up to a whole frame and staggered falls behind
  (measured 4.50 against 6.00).
- **Point defence is an active ability**: activation opens a 15 s firing window, then 15 s of
  recharge. While active, **each turret picks and tracks its own target** — the nearest in range
  that no sibling turret has claimed, else the nearest — so a group gets spread across. Battleship
  mounts swing slowly (τ/3); the carrier's fast (τ/1.2). 460 u reach, 8° firing cone.
- **The broadside (battleship, F).** A **0.5 s wind-up** in which every main turret swings onto the
  cursor — fast enough to come round from anywhere in time (half a turn in the wind-up, or their own
  τ/4 if that is faster) — then **three volleys of every main gun, 0.25 s apart**, each shell a normal
  shell, then the cooldown (`broadside_cooldown`, 14 s on the kit). The ship steers throughout; nothing about it touches the helm. The
  host fires it (`PlayerShip.TickAbilities`, `Turret.Shoot(mult)`), along each barrel as it points —
  the aim is the owner's cursor as it last reached the host. Guests count the wind-up down themselves
  and show the volleys from its end until the host's report says how many are left, so the bar and
  the turrets' fast swing never drop back to READY in between. `ShipStats.BroadsideDps` owns the
  arithmetic and is the only place it is worked: volleys × barrels × shell × `broadside_mult`, over
  the whole cycle (the wind-up, the gaps between volleys and the cooldown). Its gear (the battleship's utility slot): Heavy (×1.4 shells,
  longer cooldown), Rapid (cooldown +50% rate, ×0.75 shells), Barrage (+1/+2/+3 volleys, slower wind-up
  and cooldown), Snap (a wind-up twice as fast, one volley fewer).
- **The missile burst (destroyer, F).** A magazine of `missile_mag` bursts, `missile_refire` apart;
  **R** reloads it (`missile_reload`, nothing fires meanwhile). A burst is **three guided missiles**
  off the nose (`PlayerShip.BurstSides`) — one straight at the target, two launched **up to 70°**
  either side that curve in — at `missile_speed`, turning `missile_turn`, for `missile_damage` each.
  It needs a selected target inside `missile_range`; the slot says why when it refuses. **Close in, the
  fan narrows**: a missile heading θ off a target d away can only come round onto it if d > 2r·sin θ
  (r = speed ÷ turn); inside that it circles the target until its run ends. So the side angle is
  the widest that still converges with a 0.8 margin — `PlayerShip.BurstSplayFor` is that formula and
  the only place the distance it implies is worked out, and it follows the gear (a Buster Rack turns
  wider). A missile is a row of `Shots.All` (`missile`), the same flyer as the bombers' torpedo at a
  different row; guests fly a cosmetic copy with the same guidance.
- **Fighters** (17 u) hold orbit until **attack** sends them at the selected target; they fight
  while it is within `control_range` (1500 u on the kit). They fly in bursts: after `fighter_burst`
  of firing a fighter returns to the **carrier's centre** to rest for `fighter_rest`, then rejoins.
  **R** recalls them.
- **Bombers park on the carrier's deck**, in bays on the white either side of the runway, drawn at
  **65%** (the deck is far below, as the hauler's pad is; the hauler's own landed size), alternating port and starboard so the sides
  always split evenly (6 → 3 + 3), and rearm there (`bomber_rearm`). **Bomber strike** sends them at the target if
  it is within the **strike range, defined as twice the fighters' control range** (`strike_range`,
  3000 u on the kit; it is built from `control_range` and takes the same bonus, so the two cannot drift). They **take off one at a time, 0.83 s apart** -- the fighters' cadence, on the deck's own
  clock (`PlayerShip.TakeLaunchSlot(kind)`) -- each rolling onto the runway and up it to the bow end,
  **growing to full size as it lifts** (1.6 s, the hauler's SmoothStep). Bombers are 28.1 u. At
  `launch_range` (1900 u on the kit) each swings its nose onto the target
  and launches 4 torpedoes straight ahead **while still closing slowly** (never quite stopped), then
  comes home **over the carrier's centre, settles onto the runway there** (1 s, shrinking back to deck
  size, turning to face the bow) and **taxis to its bay** (0.6 s). Torpedoes run at `torpedo_speed`
  out to `torpedo_range`. A strike whose target goes out of range is called off -- and a bomber still waiting on the
  deck for its turn answers for itself, so the strike ends. `PlayerShip.Bay` places the bays
  (`ClassArt.BayX/BayY/BaySpacing`), `ClassArt.RunwayBow` the end of the take-off.
- **An escort's hunters scale with its THREAT** (`Hub.EscortThreat`): a mission level made a quarter each
  from the load, the party (level and toughness), the highest boss and the route, which sets their hull
  and damage, their numbers and -- very slightly, 1% a level to 10% -- their speed and turning.
- **A deck move is timed, not steered.** Lifting, landing and taxiing run in the carrier's frame on
  the bomber's own clock: a moving, turning carrier carries it exactly and the move always ends. Only
  the flight home is steered, to a point (the carrier's centre) that does not swing as the carrier
  turns. A guest places the deck moves itself from the state the host sends, on its own clock.
- **Six open hotkeys** (1–6 by default) follow every class's own abilities, for every class. They
  bind and remap like any ability and do nothing until something is assigned.
- **A bomber's torpedoes do not track — and that is the LAUNCH's doing, not the row's.** The
  `torpedo` row of `Shots.All` is marked `Guided`: it is the same flyer the destroyer's missile and
  a boss's seeker are. Guidance only runs when the SHOT is handed both a target and a turn rate
  (`Shot._Process`: `Guided && TurnRate > 0 && TargetId != 0`), and the bomber's
  `Combat.LaunchTorpedo` (`ShipClasses.cs`, `BSt.Launch`) passes neither — that call is where the
  invariant lives, and adding a target id there is all it would take to break it. So a torpedo runs
  straight at `torpedo_speed` out to `torpedo_range`, smoke trailing, and bursts on the first
  hostile it touches. A target that steps aside after launch is missed (checked on single torpedoes: a still target
  takes the hit, a moved one takes nothing, and a homing torpedo fails the same check).

### The input model

This is the developer's rule, not a default to revisit:

- **Left-click is select, or confirm a placement. Nothing else, for any class.** It never fires or
  orders an attack. Clicking empty space keeps the current target.
- **Tab always selects the hostile nearest the ship**, at any range, with no cycling.
- **Attacks and abilities are keys**, all remappable except the fixed ones.
- **Right-click only cancels a placement.**
- **Placement** is `Hub.BeginPlacement(label, onConfirm)`: a non-instant ability calls it, and the
  next left-click confirms at the cursor; right-click or Esc cancels. Nothing uses it yet.
- **Esc peels back one layer**: text box → placement → creator → K window (or a key capture) →
  base menu → target → menu.
- **The ship menu (class, name, colours) is only reachable through REFIT** in the base menu, and
  costs 10%. C no longer opens it.
- **Every piece of HUD sits on a panel** (`Ui.PanelStyle`/`Ui.Wrap`): the stats line, the
  multiplayer control, the BASE button, the controls line, the ability bar, the hull bar, the base
  menu, the K window, the creator's columns and the main menu.

Selection is local UI state. It reaches the host only as the `NetId` argument of an ability, so
every hostile needs a `NetId` that is the same on every peer (`TargetDummy.NetId` is
`NetIds.Dummy + Number - 1`, and `Hub.PracticeTargets` holds numbers 1, 3, 4 and 5: so 1000, 1002, 1003, 1004).

### Balance on paper

**Every class is tuned to about 50 sustained DPS on the kit without chips** — the 50 DPS pass
(`Stats.cs`, at `main_damage`). No class's total is repeated here: a table of them is how this
section came to say 31.0 for a battleship that deals 51. A ship's total is
`ShipStats.SustainedDps`, added up from the damaging systems its class declares
(`ClassDef.Weapons`, out of the one `Dps` catalogue), and the K window prints that same object, so
the window and the game cannot disagree.

What the classes trade is SHAPE, not size. The battleship is the steady one from range, guns plus a
broadside. The destroyer is the fastest hull with the burstiest weapon: a magazine of guided threes
that a turn cannot dodge. The carrier reaches furthest — fighters out to `control_range`, bombers to
twice it — and holds the least hull. The levers are the rows themselves: `main_damage` and
`main_interval` in a class's `Nums`, `broadside_cooldown`, `missile_damage`, each one number, and
`tools\smoketest\run.ps1 -Solo` proves each total against literals.

The hub's test bench is **the practice range: two target dummies (1, and 3 armed) and two practice
fighters (4 and 5)** standing where dummy 2 used to, one row each of `Hub.PracticeTargets`: hostile,
harmless, unkillable, each reporting damage per second. The meter restarts itself on the first hit
after 5 s without one. **It stands 500 u or more from every leg the hauler flies** (a layout check
proves it). The trap: the hauler's own point defence does not know a practice fighter from a raider
(`Targeting.PointDefence` requires Light and forbids nothing), and a hunter's 90 u blast lands where
the hauler will be 12 s on -- on a pilot practising there, never on a target (a raid's blast takes
only `Hub.RaiderTargets`). On the base's line, 500 u off the lone run and the escort's last leg
leaves nothing between y 414 and y 1533, which is why the range is below that leg rather than just
under the base. A route or a range that moves must keep the 500.

### Art

- **Every hull is made by `tools/make_ships.ps1`** from **the pack** (`art_source/pack_2026-09-24/`:
  the owner's 34+ finished, shaded sprites; which entity wears which is `docs/plans/sprites.md`), the
  ONLY source since the capitals' slice (J5) retired the last line drawings. Each `$Finished` row turns
  one file nose-up by quarter turns (never mirrored: the lettering and the asymmetric hulls would
  flip), patches out any painted gun under a moving turret (`Sheet.PatchColumns`, a clean strip of the
  SAME housing tiled over the barrel -- player hulls only, Q4), trims it to the drawing with the keel
  on the centre column, and writes it grey on transparent. Nothing is redrawn or resampled, so a rerun
  gives the same pixels. It prints the row's marks (a turret, a housing's edge) and its **nozzles**
  (each bell's aft rim and width) in world units, for the row in the game. Every hull row derives from
  **`HullArt`** (Sprites.cs: Texture, Length, Tint, Nozzles) and draws one flame per bell; `ClassArt`
  (`PlayerShip`'s twelve rows) carries the same Texture/Length plus its own `HalfWidth`, turret mounts
  and scale (no Nozzles: a class's engine plume is one point, `EngineInset`, not a bell list). The
  sprites every source replaced are in `retired/` (also ignored).
- **Trap: a run of make_ships.ps1 rewrites EVERY file it makes.** The `$Finished` rows are pixel-stable
  (no resampling), but `turret_main.png`/`turret_pd.png` are still drawn procedurally and do not come
  out byte-identical on another machine (GDI+'s bicubic resize) -- `git checkout` them after a run
  unless the turret-drawing block itself changed.
- **Hit sizes never follow the art** -- but a boss's do. A raider is hit on its row's `HitShare` of its
  Length, a class on its `HalfWidth`; the pack re-arted every raider with neither moving. The bosses
  are `Missions.BossSize` (2, the owner's "2 or 3x") times their art's measure, Length, HalfWidth and
  bells alike, and everything a move places about the hull reads the row at use: the nose (L/2), the
  ram's lane (2 HW), the rock's hold (`Boss.FlankHold`: HW + body + gap), the escorts and the warp ring
  (in half-widths), and every stand-off (`HoldOff`, a warp's `Standoff`) measured from the NOSE, so a
  bigger hull stands no nearer the party. The boss spawns with its nose on `Hub.ArenaCentre`. What
  does NOT scale: reaches, ranges and bodies (the beam's 70 u, the rock) -- except a Ring move's own
  telegraph (`BossType.Size` x `Reach`, at use in `Boss.cs`): it is drawn round the hull itself, so the
  Lancer's shockwave reaches 680 u (340 x 2), the owner's open question, 2026-09-25, resolved as a
  default.
- **The 12 player classes** (`Classes.All`, `Ships.cs`), the pack (J5), hit sizes (`HalfWidth`)
  UNCHANGED throughout (Q1) -- the art moved, the collider and the shield did not. **Battleship**
  (`battleship_bb05`): 6 painted twin housings down the spine; the 4 flanking ones (the two forward
  rows, both sides) carry the real moving mains, their painted barrels patched clean, the aft flanking
  pair patched too but left unarmed; the 3 centre (keel) turrets are decoration, untouched (Q3's
  default); point defence on the aft domes. **Carrier** (`carrier_a`): point defence's two flank
  turrets re-seated on the new hull; the third (stern) turret and the whole deck (`BayX`/`BayY`/
  `BaySpacing`/`RunwayBow`/`EngineInset`) kept at today's figures -- the old and new hulls read close
  enough in proportion that a bomber still parks and lifts off correctly proved, but this is the
  LOWEST-confidence row here (re-measure if the owner sees it sit wrong). **Destroyer**
  (`destroyer_dd22`): both main mounts moved onto the keel gun cluster near the bow, point defence
  re-seated on the flank domes. **Freighter/Tender/Sniper/Warrior/Warden/Dart/Wraith**: today's mount
  literals landed on a sensible feature of their new hull by eye (the freight ships' forward "claw" or
  spotter mount, the fighters' prow or wing roots) and were left as they were -- only their `Texture`
  moved. **Bastion** (`frigate_c`): its one main moved from the bow to the stern, onto the ring turret
  its new art actually draws (today's forward mount had nothing there to sit on); point defence kept.
  **Echo** (`fighter_f`): its one main moved forward a little, onto the paired barrels its new art
  draws on both wings (the flavour text, "fires twice"; still one game mount, front and centre of the
  pair). Every moved literal is one row's `Mains`/`Pds`, printed by the tool from a mark on the turned
  art -- see the row's own comment in `tools/make_ships.ps1`.
  **Raiders** (the pack): the webifier `fighter_swept`, the gunship `frigate_b` (its turret on its
  painted twin), the talon `fighter_tri_a`, the pod `drone_sensor` (a round drone; its one bell the stern vent), the cross `gunship_h` (the gunship's
  hull with the rack stripped; its turret on the clean aft deck), the lancerkin `frigate_d` (its turret
  on the plate aft of its tubes), and the title screen's Web `crescent_a` in the webifier's red. Their
  painted guns stay: too small to see under a turret. **Bosses** (the pack): the Rusty Bucket
  `frigate_a`, the Drake Bastion `flagship`, both RED AND BLACK (the owner's ruling): the pack's grey
  multiplied by the owner's swatch red (0.67, 0.03, 0.01), so highlights come out that red and shadows
  black; a pure multiply, since the hull reads on space without a lift -- with their painted guns (a boss has no moving turret) and their
  bells listed on the row (2 and 5; a boss draws no flame), at twice the art's size (above). **The fleet** (the pack): the hauler `cargo_4` (`Hauler.Art`; its
  six painted cargo frames are the six pods, its point defence on the bow dome), the miner and the
  salvager one drone, `drone_salvager` (`gatherer.png`, the owner's pick for both: told apart by the
  row's tint, the average colour of the art each first replaced; the beam and the unloading load leave
  from the row's `Emitter`, the claws' mouth), the lanes' couriers `drone_economy` (a file of their own,
  `courier.png`, 30 u, in the stations' livery), the wing's fighter `fighter_delta` and bomber
  `fighter_g` (its torpedoes leave from the front of its wingtip rails, one then the other:
  `WingDef.Launch`). **One pack file on two rows is one game file**, named for what both rows are
  (`gatherer.png`), never a copy per row. The hauler's
  and the gatherers' `Extent` -- what a raider holds off, not a hit size -- is measured off the art.
- **Turrets**: the owner's twin-barrelled turret is every main turret (`turret_main.png`, lifted out
  of its drawing by an outline, barrels up, the housing's centre the pivot); point defence is a
  smaller, round, single-barrelled turret in the same style, drawn by the tool (`turret_pd.png`). Each
  class mounts them at its own scale (`ClassArt.TurretTexScale`).
- **Unused art** (21 carried-over sprites nothing references) lives in `art_unused/`, which has a
  `.gdignore` so Godot never imports it. Kept deliberately, at the developer's request. **Anything NOT
  under a `.gdignore` ships**, which is why `art_unused/art_4x/` -- five OLD line-art hulls at twice
  and the two OLD turrets at four times the resolution the game loaded, all superseded by the pack --
  was deleted rather than kept (Q9), along with `tools/finish_ships.ps1` (the line-art shading tool it
  was made for; both commands dropped from CLAUDE.md and `docs/README.md`'s command lists in this
  commit). The export preset takes `all_resources`, so every file Godot imports goes into the `.pck`
  whether or not anything loads it -- which is how 45 `.translation` files Godot made out of
  `version/*.csv` rode in every release. `version/` has a `.gdignore` of its own for that reason.
- **Background** (`stars.png`): 1024 px, seamless (stars near an edge wrap), on a screen-space layer
  at −100. Client-side only.
- **Mount offsets are measured, not placed by eye**: the tool carries each mount through every step
  and prints it as `(pixel − size/2) × world-per-pixel`. They live in `PlayerShip.Art` with each
  turret's texture scale, barrel length (where shots start) and ring radius (where the PD arc sits).
- **The colours multiply the sprites** (`Modulate`): a black hull colour gives a black ship.
- `PlayerShip.Art` holds each class's texture, length, turret scale and mounts. The creator and the
  select screen draw from it, so the preview is the real ship with turrets on the real mounts.

## Character

Your ship is your character, so name and colours travel with you between worlds. This is the one
piece of player state that is **not** host-owned — it is identity, not a resource.

- Name, hull colour, accent colour, chosen class, with a live preview
- **Several characters**, Terraria-style: one file each in `user://characters/<id>.cfg`, the id
  being the creation timestamp. The select screen lists them with PLAY and DELETE; delete asks
  first and removes the file. The pre-slots `character.cfg` migrates in once and is kept as
  `.migrated`.
- Edited in the hub with **C**; every close saves. A new character can be cancelled; an edit cannot,
  because edits apply as you make them.
- The class selector runs three pages of three
- **Save format 2** (`Game.Version`): the file also carries the pilot's **hold** (every part owned and
  not fitted, for any class), **unclaimed loot** (dropped at a kill, not yet flown over), and the
  **hints** seen with the tutorial's off switch. A file from another format is greyed out, never
  repaired. **A part that is renamed or changes class is carried forward, not a new format**:
  `Character.Load` reads every part id through `Equipment.Migrated` (the battleship's missile rack and
  its four rack lines are the destroyer's now, `bs_*` → `dd_*`), and a fitted part that no longer fits
  its slot goes into the hold -- on the owner's own file only; the host still sanitises a guest's
  claimed loadout strictly. Things that come in runs (loot pickups, hints) save through `Character.SaveSoon`: 2.5 s
  after the last call, written at once on leaving or switching pilot.

## Gear, loot, the tutorial and reconnection (the owner's batch of 2026-09-21)

- **A part leans hard one way.** Four specialisations per slot type at three rarities: the upside
  grows with rarity (x1 / x1.5 / x2), the downside does not, so a rarer copy is strictly better but
  never free. The owner's carrier examples are literals in the tests (Elite III, Swarm III). No part
  touches a turret count: the mounts are fixed on the drawing, and a part cannot add one.
- **The multiplier floor (0.1)** is there because gear stacks: the worst sum of downsides on any stat
  is -55%, but a file on the player's disk can carry any bonus.
- **The hull keeps its fraction across a refit.** Keeping the damage taken let a pilot swap Bulwark
  III on at 10 hull and come out at 316.
- **Gear is per pilot and trusted like purchases**: the host sanitises a claimed loadout (known parts,
  right slot, right class) but cannot see a guest's hold -- the same trust as `Bought`, and a rarity
  check by level is unsound (a low pilot can be carried to a high boss).
- **Loot never goes to waste.** Drops are on the pilot's file at the kill and claimed on the next
  world entry: a quit, a crash or a lost host costs nothing. Crates are local nodes with no network:
  only their pilot's machine has them. The host rolls; a guest cannot choose what it gets.
- **A goodbye is what tells a closed session from a dropped one.** ENet reports both the same way.
  The goodbye must actually leave, so the peer is disconnected gently and pumped for up to a second.
- **A place is held by character id**, not peer id (it changes on a reconnect). The id is the
  guest's own claim, as its name and gear are: there is nothing else to know a returning player by.
- **A failed attempt changes nothing.** Going offline raises `SessionChanged` only if a session was
  there to leave; otherwise a failed JOIN rebuilt the pilot's own ship at the spawn.
- **Hints read only state every peer has** (its own ship, the world it sees), so a guest meets a raid
  from the raiders it is sent and no hint needs the network.
- **You leave a won arena by pressing RETURN, not on a clock**; the party warps home once every
  present pilot has. It is a ready-up, not an independent exit, because the host runs ONE world at a
  time (Home and Arena are two scenes it swaps between with `ChangeSceneToFile`, and the shared base
  is torn down for the trip -- `SaveForTrip`). So there is no base for a single pilot to return to
  while the others fight on; the whole party moves together, as it always has. The button only shows
  on a win; a wiped party (all in stasis, cannot press anything) still auto-returns after 3 s.
- **A kill is paid once, by its serial.** The host notices a dead link seconds after it happens, so a
  pilot can be counted in a kill it never heard of. The host keeps the last 12 s of kills and owes each
  to a pilot dropped just after; a pilot pays itself for a serial once, however it arrives (at the kill,
  or owed on its return). The bounty is split among everyone credited with the kill -- the ships there
  and the places held -- so a held pilot's share is not the others' windfall. The serials paid are on
  the pilot's FILE: in memory, a game killed hard after a kill and restarted inside the hold was paid
  that kill again -- a loot dupe anyone could do on purpose. So a kill is one message (EXP, share and
  parts): two could be paid apart.
- **Every connection attempt has a deadline of its own**: 12 s for a JOIN, 5 s for a try to get back
  in (automatic or RECONNECT). ENet's own timeout is not one -- see Traps.
- **A session's end takes its party with it.** Who is held, who is READY and -- for a pilot that was a
  guest -- the mission: the host's portal stayed open in a guest's own world, and a held pilot kept a
  solo world's portal shut for good.

## Built so far

`version/MAP.md` maps every script, member, RPC, spawn site, event hookup and asset, with who uses
each (`python tools/map.py`). The files to start from:

| File | What it is |
|---|---|
| `Hub.cs` | the world: layout, sectors (home / arena), ships, raids, missions, loot drops, held places, the HUD |
| `Net.cs` / `Router.cs` | sessions, the build handshake, the goodbye and reconnection; opening the port on any router |
| `PlayerShip.cs` / `ShipClasses.cs` | the ship (helm, abilities, refit, wing) / turrets and wings |
| `Stats.cs` / `Equipment.cs` | every number a ship flies with / the 96 drop parts and the kit |
| `Loot.cs` / `Hints.cs` | drops and crates / the tutorial's corner card |
| `Yard.cs` / `Economy.cs` / `Hauler.cs` / `Gatherer.cs` / `Docks.cs` | the idle economy: the base, its numbers, the hauler's runs, miners and salvagers, and every pad a craft docks at |
| `Landmarks.cs` | the places at home: footprints, art, scope marks, docks, and the one service gate (`Serves`) |
| `Character.cs` / `Game.cs` | the pilot on disk (save format 2, the batched save) / the build's number and the way out |
| `Boss.cs` (the base) / `Lancer.cs` / `Raider.cs` / `Missions.cs` | the arena's boss, raiders and hunters, levels and rewards |

## A note on the typecheck harness

`typecheck/` compiles every script against `GodotStub.cs` with the real Roslyn compiler. It has
failed the project twice, in two different ways, and both fixes are now baked in:

**1. The stub can invent APIs.** It declared a top-level `MultiplayerPeerTransferMode`; Godot nests
it as `MultiplayerPeer.TransferModeEnum`. Code typechecked here and failed in the engine.
*Rule: mirror Godot's exact nesting and spelling when adding to the stub.*

**2. A broken stub silently disabled ALL script checking.** `typecheck.sh` piped through
`grep -v GodotStub` to keep stub noise out of the output. But Roslyn binds declarations before
method bodies — so a declaration error in the stub means it never binds a single method body, and
every script error vanishes. The filter then hid the stub errors that caused it. The harness
reported "0 errors" on code that could not compile.
*Rule: stub errors are FATAL and printed loudly; the script check does not run until they are gone.*

Verified both ways: injecting `NoSuchThing.Boom()` into a script is now caught, and a deliberately
broken stub makes the harness refuse to run rather than report success.

**3. No compiler reported as success.** With no .NET SDK installed, `find` came back empty, nothing
compiled, and the `grep "error CS"` filter turned that into zero errors — under a header that still
said "REAL GodotSharp.dll". *Rule: a missing SDK exits 2, and zero errors only counts if the
compiled output exists.* Verified all three outcomes: clean exits 0, an injected error exits 1, and
no SDK exits 2.

**Resolved.** `GodotSharp.dll` now sits in `typecheck/`, so the harness references the real assembly
and ignores the stub. Verified against three regressions: the invented `MultiplayerPeerTransferMode`,
the missing `Profile` type, and a wrong argument type on `SetMultiplayerAuthority` — all three are
caught now, and all three previously passed. The file came from:

    C:\Users\<you>\.nuget\packages\godotsharp\4.7.2\lib\net8.0\GodotSharp.dll

That makes the check identical to what Godot compiles -- same reference assembly, same Roslyn -- so
an invented or misspelled API cannot pass. The stub remains only as a fallback. The one gap that
survives is Godot's source generators, which synthesise things like `MethodName.Foo`; this codebase
uses `nameof()` instead, so nothing depends on them.

## project.godot belongs to the editor, not to us

`project.godot` was hand-written in the sandbox, where there is no editor: settings were listed
explicitly and carried comments explaining the choice. The first time the project was opened in the
Godot editor on Windows, the editor re-saved the file in its own canonical form — added its standard
header, reordered the sections, dropped every comment, and **deleted every setting whose value
already equalled the engine default**. Seven lines went:

| Dropped | We had | Engine default (4.7.2, confirmed) |
|---|---|---|
| `display/window/vsync/vsync_mode` | 1 | 1 |
| `display/window/size/resizable` | true | true |
| `gui/…/default_font_subpixel_positioning` | 1 | 1 |
| `gui/…/default_font_antialiasing` | 1 | 1 |
| `rendering/renderer/rendering_method` | forward_plus | forward_plus |
| `rendering/renderer/rendering_method.mobile` | mobile | mobile |
| `rendering/textures/…/default_texture_filter` | 1 | 1 |

Confirmed by asking the engine, not by reading docs: a throwaway project plus
`godot --headless --script` printing `ProjectSettings.get_setting(...)` for each key. All seven
matched. **The behaviour is identical either way** — the editor's version was adopted as the
baseline, because re-adding the lines only makes the editor strip them again on its next save.

Two things were lost that were worth keeping, so they live here now:

- **`Net` is an autoload because it owns the session and must exist before any world does.**
- **MSDF fonts (`default_font_multichannel_signed_distance_field`) keep glyphs sharp at any camera
  zoom; `generate_mipmaps` does the same when zoomed out.** Both survived the rewrite (they are not
  defaults) — only the comments explaining them were lost.

The seven dropped settings were all deliberate choices that happen to match today's defaults. If a
future Godot version changes one, this table is the record of what was intended.

*Rule: never treat a `project.godot` diff after an editor session as tampering. Check whether the
removed lines equal the engine defaults first — ask the engine — and keep the reasoning in this file,
where the editor cannot delete it.*

## Traps that have already cost time

- **A file's C# object must not die while the engine can still hand the file out.** Godot 4.7.2 keeps
  only a WEAK handle to a resource's C# object while nothing but that object holds it -- a texture
  whose last sprite has gone. The collector can take the object, and until its finalizer runs the
  resource is alive and in the engine's cache. A load of the same path then swaps a dead handle and
  blanks it; if the finalizer (its own thread) lets go of its reference before the load reaches C#,
  each reference that brings the count back to two asks .NET to swap a null handle: "Handle is not
  initialized" from `ScriptManagerBridge.SwapGCHandleForType`, an ERROR that turns a green run red,
  now and then. (The binding hears a count only as it lands at 2 or at 1.) Every file is loaded through `Assets.Load`, which holds its C# object for the life
  of the process; a rung-3 check reads the build's IL for any other call to the engine's loader.
- **Where an ERROR sits in a harness log says nothing about when it happened.** `run.ps1` writes a
  role's whole stdout and then its stderr, so every engine error lands after that role's `DONE` and
  reads like something the quit did. The handle error above was chased as a shutdown bug for that
  reason alone.
- **`PlayerShip.CalmFor` can say at most `CombatHold` (12 s).** The combat clock runs DOWN from 12, so
  any lock of 12 s or less can be read off it, and a longer one cannot. A smoke check holds
  `Landmarks.CalmNeeded` under it. A lock longer than 12 s needs the clock to count up, and that changes
  a field on the wire (the host-state packet's `combat`).
- **A check that spends through a service must stand on the place that serves it.** `Yard.BuyGearLevel`
  and `Yard.QueueScrap` refuse off the EQUIPMENT BASE or inside the 10 s lock, whatever the button
  says, so a check that presses UPGRADE or QUEUE -- or calls either -- sets its ship down on the pad
  and runs the combat clock out first (the recycler's checks, the level bought in the window, the
  guest's mid-session level), and puts both back after.
- **A spec's figure is not the gun's.** `Spec().Interval` carried the overdrive while `FireControl`
  fired off the raw reload, and every check read the spec. A rate or a speed is proved by counting
  what leaves the barrel or measuring the hull's way, never by reading a figure the gun does not
  fire from. Every gun's reload (main guns, point defence, dropped turrets, the wing's shots)
  becomes time in one place, `PlayerShip.Cadence`, which adds a lift's shares to the reload's own
  bonus rather than multiplying the two; a magazine reload, a bomber's rearm, a broadside's gap and
  a missile burst's refire are not lifted.
- **A share on an inverse stat divides: positive is shorter.** An interval or a cooldown
  (`Stat.Inverse`) is `(Base + Flat) / (1 + Bonus)`, so the improvement is a POSITIVE share, as every
  rate part writes it. Gunnery and Cooling wrote -0.005 and made every point a downside, while the
  only check pinned the table's own sign. Prove the direction on the sheet, and print what a share
  DOES (`AllStats.Change`), never the share.
- **An event sent once reaches only the peers there to hear it.** A boss's warnings and the Drake's rock
  go out as they start, to the peers in the arena then; a guest that arrived later (a rejoin, a slow load)
  never heard of them and was hit by a rock it could not see. `Boss.CatchUp` sends what is up now to a
  peer as it reports its world -- anything new sent once needs the same thought.
- **A guest's hull watch starts from the host's first figure**, never from the hull it built itself --
  or the difference (a boss already hurt, an escort built at 3 x hull) shows as a hit. A guest's own
  ship is watched only through the host's reports, never the hull it regenerates between them.
- **Damage numbers read hulls, not hits.** Each damageable thing watches its own hull from frame to frame
  (`HullWatch`), so a guest shows them from the figures it is already sent, with no message per hit. A
  hull that falls for a reason other than damage -- a refit to a smaller hull -- resets the watch, or it
  shows as damage taken.

- **Line art is white until it is tinted.** The ships are grey drawings that the hull colour
  multiplies, so the old pale-blue default drew them near white; the default is a real blue, and a
  pilot still in the pale one loads in it (`Character.SavedHull`). Raiders need their tints for the
  same reason: untinted, an enemy reads as a neutral hull.
- **Symmetry and alpha are decided at twice the final size, and only halved at the end.** Halving
  weights each pixel by its cover, so a cut-out edge never picks up the paper's white or a black
  fringe; and the tool's mirror samples what lay beyond the old sheet as TRANSPARENT -- sampling it
  as opaque paper put hairlines down both edges of the turret, where the reflected half reached past
  the drawing.

- **`Hub.InArena` changes before the new world exists.** `GoTo` sets the sector at once and swaps the
  scene at the end of the frame, so code waiting for "home" that reads the new world must wait for
  it to be built (`H.Yard != null && H.IsNodeReady()`). A check read a guest's file in between and
  saw loot the new world had not yet claimed.
- **A pursuer with a fixed turn rate cannot reach a target inside its turning circle.** The carrier's
  fighters strafe by pure pursuit (352 u/s, 3.5 rad/s: a ~100 u radius). A pass that stopped only 1.2
  target diameters past a light raider (~33 u) left anything drifting slowly inside that circle, and the
  fighter flew laps round it, never lined up and never fired again until it went home -- the owner's
  "they fly through them but don't shoot". The overshoot now clears a whole turning diameter too
  (`Wing.TurnDiameter`). **A stationary target cannot show this**: the strafing check on a still dummy
  passed throughout; the check that catches it moves a small target at 12 u/s.
- **A node freed with hull left still reads "alive".** A withdrawn hunter (`Hub.CallOff`) is freed with
  its hull, so `Alive` stays true and a carrier's fighters went on reading a freed node.
  `Hub.LetGo` takes it off every ship's orders (`PlayerShip.Forget`) and out of `Combat.Hostiles` at
  once: `QueueFree` only takes a node out of the tree at the frame's end, and a point-defence turret
  that ticked later in the same frame acquired the dropped raider again, then read it freed (an
  `ObjectDisposedException` a frame later). **A turret is never told**: it holds only what is still
  in `Combat.Hostiles` and alive, asked every tick, list first (`Turret.StillThere`). Telling each
  ship's turrets reached the guns on pilots' ships and no others -- a freighter's dropped turret
  holding a hunter when an escort ended threw on it every frame after and never fired again, and so
  did the hauler's own mount after a reset mid-escort (`ResetToPad` calls off the hunters, then puts
  it back on the pad online). A push reaches what one list knows; a re-check against the live list
  reaches everything that holds the reference.
- **The typecheck does not compile the harnesses.** `typecheck.ps1` checks `scripts/*.cs`; the smoke
  test and the sweep (`tools/*/*.cs.txt`) are compiled only inside an engine run, so a typo in a
  check costs a whole run to find. Compile them first: `scripts/*.cs` plus both `.cs.txt` files (as
  `.cs`) with `csc` against `typecheck/GodotSharp.dll` -- the same command line `typecheck.ps1` builds,
  seconds rather than minutes. A local declared in a new `{ }` block that reuses a name the method
  declares elsewhere (CS0136) is the usual catch.
- **A wall-clock wait is not a game-time wait.** `Wait(s)` is a `SceneTree` timer (game seconds); a
  loop on `Time.GetTicksMsec()` measures real seconds, and the headless run does not keep the two in
  step. A check that the title ship flew home in "8 s" failed at 366 u when its wait was rewritten as a
  real-time loop. Poll inside `CreateTimer(s)` (`while (timer.TimeLeft > 0)`) to watch during a
  game-time wait.
- **A class is asked what it carries** (`Classes.Guns` / `Broadside` / `Missiles` / `Wing`), never
  compared with one class. The two-class code tested `== Battleship` in some thirty places, and a third
  class quietly took the battleship's branch (or the carrier's `else`) in every one of them.
- **A renamed id is carried forward, not a new save format.** Bumping `Game.Version` greys out every
  pilot. A part id that changes goes through `Equipment.Migrated`; a key binding whose ability changed
  id is renamed in `Settings.Load`. Both have a check that loads a file written the old way.
- **Static fields start in the order they are written.** `Equipment.All = Build().ToArray()` reads
  `Scale`, `Suffix`, `Ranges` and `None`; declared below it, they are null when it runs.
- **A const named like a Godot method hides it.** `Hints.Show` hid `CanvasLayer.Show()` (warning
  CS0108): named constants in a node class need names Godot does not use.
- **A mutant is caught when the CHECK fails, not when its success text appears.** A check that
  prints a different message on failure ("a character field is not being saved") does not contain
  the success text; match on something both messages share, or on the failure text.
- **A run of mutants must read one version of the code.** The mutant runner copies the repo per run;
  edits made meanwhile change what later runs test. Freeze a snapshot (`%TEMP%\warships_frozen`)
  and run the series against it.
- **Two mutants on one check prove neither.** Group mutants into runs so each check has at most one
  mutant that can make it fail -- and remember a mutant can break a check it does not target (a save
  that drops the hold makes every save-timing check fail too).

- **A sound still playing at exit is a "resource still in use".** The mixer releases a stopped
  playback only on its next cycles; stop everything, then wait in REAL time (`Game.Quit`). It was
  first blamed on `GD.Load`'s cache -- which does not hold resources alive -- and chased for days.
- **Godot's `Upnp` refuses a router whose internet side is private** (double NAT) and returns an
  empty device list for a router that answered on a search target it did not ask for. `Router.cs`
  talks UPnP itself; Godot's is the last resort.
- **Windows will not send from a LAN address to a loopback one** (WSAEADDRNOTAVAIL). Only the
  network-wide search is bound to the LAN adapter; a search to one address binds to any.
- **A player still joining is not a guest yet, and a guest is not online until the host says so.**
  `IsHost` stays true until the handshake lets the player in; `Connecting` stays true until the
  host's welcome (`Net.NetWelcome`). Code that must know which end of a join it is on asks
  `Net.Connecting`, never `IsHost`.
- **Godot throws away, with an engine error, anything but the handshake from a peer it has not let
  in** ("SYS_COMMAND_AUTH" in the log). ENet calls a link connected before the handshake has let
  either end in, and each end lets the other in the moment it hears the other's "done" -- so a guest's
  first unreliable report, sent right beside its own "done", overtook it on a link that reorders.
  `Net.IsOnline` is true only in a session and, on a guest, only after the host's welcome, which the
  host sends once it has let the guest in; every guest send goes through `IsOnline`. The other way:
  the host's own "done" can be lost, and anything unreliable the host sent a guest before the resend
  was thrown away there. The guest answers the welcome (`NetWelcomed`), which travels behind that
  "done"; the host sends unreliably only to guests that have answered (`Net.ToHeard`) or reported a
  world (`Hub.RpcToSector`), and guests' ship reports reach the others through the host, never
  Godot's relay, which sends to every admitted peer at once.
- **ENet's round-trip estimate starts at 500 ms** and settles over the first reliable packets:
  `Net.Arriving` never shortens a warning below 40% of it.
- **`ENetMultiplayerPeer.GetPeer(id)` only knows the peers this process is connected to.** A guest
  hears of the other guests through the host; asking for them is an engine error.

Each of these compiled clean and was wrong at runtime. The smoke test covers all of them.

- **`GD.Load` caches; `Dispose()` does not evict.** A resource loaded with `GD.Load` lives in
  `ResourceLoader`'s cache for the life of the process, so disposing your handle releases nothing
  and the engine reports it as `resources still in use at exit`. `Music` carried an `_ExitTree` that
  stopped the players, nulled the streams and disposed both handles -- it ran, and it did not help.
  `ResourceLoader.Load(path, "", CacheMode.Ignore)` gives sole ownership, and then the dispose works.
  Do NOT dispose a resource the cache is sharing: it broke the teardown chain and the count went up.
  *Find them by name with `--headless --verbose --quit-after`; the smoke runner only gives a count.*
- **A round-trip check only covers the fields it SETS.** Enumerating by reflection at the compare
  step feels thorough and is not: a field left at its default is identical before and after whether
  it is saved or not. The base economy fields were added to `Character`, not saved, and the check
  passed. The thing that notices is an INVENTORY -- every field against a declared list -- so a new
  field fails until someone accounts for it. Sweep non-public statics too: `Spares` is private.
  *Rule: "reflection" is not the same as "covered". Ask what makes the check go red.*
- **A check on an asset must read the asset.** A loudness check that reads a constant in the code
  passes whatever the file contains, and one that reads `GD.Load<AudioStreamWav>` measures what the
  IMPORTER made of the file, not the file: the first version of the sound-level checks reported a
  ratio of 1.000 for two files that differ by 35%. `FileAccess.GetFileAsBytes` reads what shipped.
  *Rule: the same goes for sprite sizes. Measure the thing the player gets.*
- **Host-only state read by drawing code is a guest bug that nothing reports.** `Lancer._beam` and
  `_charge` only tick under `Net.Sim`, and `Raider.Boosting` / `Shivering` are host-only fields;
  all four are read by code that draws. On a guest the boss's super-move bar sat at zero for the
  whole fight and the escorts' triple-length plume never appeared, and neither showed up as an
  error anywhere.
  *Rule: when you add something DRAWN from a value, ask which peers have that value. The solo run
  is the host, so it can never tell you.*
- **A sound played where only the host runs is silence on every guest.** The railgun's report was
  played inside `FireRail`, an ability's `Expire`, which runs under `Net.Sim` alone. A sound belongs
  to what every peer is SENT: a flash, an `FxDef.Sound`, or a move's `Cue` / `Strike`.
  *Rule: an `Sfx.` call inside host-only code is a guest bug.*
- **A node can pass every behavioural check and draw nothing.** The title screen's battleship
  moved, warped, held station and reported its position correctly for several rounds of checks
  while `PlayerShip.Init()` had never been called, so it had no sprite, no turrets and no stat
  sheet of its own. Only a sweep frame showed it.
  *Rule: a check on behaviour is not a check on being drawn. Anything new on screen needs a frame
  looked at, not just a green test.*
- **A theme on the root window does not cross a `CanvasLayer`.** Every piece of UI in this game
  hangs off one, so `Ui`'s Button entries reached nothing for the project's whole history and every
  button drew Godot's stock theme. Nothing looked broken, because the stock theme is *also* a dark
  rounded rectangle. The fix is `Ui.Style` / `Ui.Panelise` at the root Control of each UI subtree —
  descendants inherit from an **ancestor Control**, just not from the window. A dialog is a
  `Window` and breaks the chain the same way.
  *Rule: when a theme appears not to apply, do not reason about it — set the colour to something
  impossible and count the pixels. "The theme resource has the entry" and "the control draws it"
  are different claims, and only the second one matters.*
- **The UI lint measures position and width, not contrast.** A label drawn in the same colour as
  the bar underneath it disappears completely and the sweep still passes with 0 lint. Only looking
  at the frames catches it.
  *Rule: any text drawn ON a filled bar must choose its colour from what is under it.*
- **A Control pinned between two offsets narrower than its contents grows past them.** The stats
  window was pinned 576 apart while asking for 578, so it hung off the right edge of the screen.
  Derive every such width from one constant rather than repeating the number.
- **A default that is not one of the offered choices shows as no choice at all.** `MusicVolume`
  defaulted to 0.6 and the Esc menu offers 0/0.25/0.5/0.75/1, so the row opened with nothing lit.
  *Rule: a setting presented as a row of choices must default to one of them — and not to the one a
  check sets, or the check cannot fail.*

- **Name networked nodes before adding them.** RPCs resolve by node path, and Godot's generated
  names (`@Node2D@23`) differ between peers. Ships are `Ship_<peer id>`. Free a node's name
  (`RemoveChild`) before spawning its replacement, or the new one is silently renamed.
- **Unsubscribe from autoload events in `_ExitTree`.** `Net` outlives every scene. A lambda that
  captures a scene's node calls into a freed object on the next event.
- **`ChangeSceneToFile` removes the current scene at once.** Anything after it that needs the tree
  (`GetViewport()`) is null. Do the tree work first.
- **`Input.IsKeyPressed` ignores GUI focus.** It polls the raw keyboard. Gate gameplay polling on
  `Hub.ControlsLocked`.
- **Godot keeps `LineEdit` focus on a click that hits nothing focusable.** The Hub releases it by
  hand in `_Input`.
- **A child's `Position` is already in its parent's frame.** Do not rotate it by the parent's
  rotation again. A world-space angle is `GlobalRotation`, not `Rotation`.
- **"Online" means connected**, not "a peer object exists". RPCs during the handshake are errors.
- **Never `pkill -f` a pattern that appears in your own command.** It matched the shell running it and
  killed the command mid-edit, twice, silently. Kill by PID instead.
- **A test that throws must still end the run.** An async test that threw simply stopped; nothing
  called Quit, and the run sat until its time limit, looking like a hang. The driver now catches,
  records a FAIL, and quits. Keep the runner's output line-buffered so a live run can be read.
- **Read game state right at the action when something else is changing it.** The docked hauler
  drains stock every frame, so "ore is exactly 900 after RESET" failed after a wait but holds when
  read straight after the (synchronous) click.
- **A smoke run that cannot compile its test must stop.** One run spent minutes on an old build
  after the test failed to compile; `run.sh` now exits on any build error.
- **A restart can leave a stale X lock** (`/tmp/.X99-lock`). Xvfb then will not start and Godot
  renders nothing without complaint; `tools/screens/run.sh` now clears it and refuses to run
  without a display.
- **A translucent panel shows the world through it.** A faint line in the BASE menu was the
  player's own green hull bar behind it. Menus that sit over the world are fully opaque.
- **Measure leaks after forcing a .NET collection.** Godot objects wrapped by C# are freed only when
  the collector runs, so raw object counts climb and then collapse; they look like leaks until you
  collect first. Orphan-node counts need no collection.
- **Tools that press Esc depend on what is open.** Esc peels layers (a selected target goes before
  the Esc menu); automation should open and close things directly.
- **The version/ folder is the baseline**: `CODE_SNAPSHOT.txt` (all code, split back to verify) and
  `MANIFEST.sha256` (every tracked file). The next session starts with `sha256sum -c` against it.
- **"No game code changed" does not mean the snapshot is current.** `CODE_SNAPSHOT.txt` carries
  `project.godot`, the `.tscn` files, the csproj and the harness scripts as well as `scripts/*.cs`.
  Adopting the editor's `project.godot` changed the snapshot's contents while touching no `.cs` at
  all, and it shipped stale for a commit on exactly that reasoning. *Rule: regenerate it whenever any
  file it lists changes, and split it back to prove the result — a baseline that cannot be split back
  is worse than none, because it still looks authoritative.*
- **A list inherited from the last run keeps its own omissions.** `typecheck/GodotStub.cs` was
  missing from `CODE_SNAPSHOT.txt` for the project's whole history, because each regeneration took
  its file list from the previous snapshot: absent once meant absent forever. The generator now
  *derives* the list (root files in their fixed order, then `scripts/`, `typecheck/`, `tools/*`
  sorted ordinal) and the verifier asserts no tracked code file is missing. Now 64 files.
- **Sandboxes restart.** Twice a run was lost to a container restart (uptime reset, processes gone,
  files kept). If a background run goes quiet, check `uptime` before suspecting the code.
- **Background processes die when the command that started them ends** (in this sandbox). A
  test or screenshot run launched with `nohup … &` is killed as soon as its launching command
  returns, so a run must start and finish inside one command (under the 300 s limit): run the
  smoke test and the screenshot sweep in separate commands, in the foreground, with `timeout`.
  An earlier failed sweep blamed on a stale X lock was most likely this.
- **Stream tool output line by line** (`grep --line-buffered`, `sed -u`). Buffered output is lost
  when a run is cut short, which leaves an empty log and no clue.
- **Long runs:** a single tool command is cut off at 300 s. A full smoke run takes 1–4 minutes
  depending on the machine; the screenshot sweep about 4. Run each in its own command.
- **Probe, don't guess, when a visual fault resists.** A faint line under the base menu was blamed
  twice on the wrong thing (the DISPATCH widget, then a stretched button) before a probe listing the
  controls at that exact spot showed there were none — it was the world showing through the panel.
  And check the probe's coordinates: the first probe looked in the wrong place.
- **Menus over the world should be opaque.** Even 96% let a bright hull bar through as a line.
- **Contact sheets hide things too.** A label strip stamped on each tile once covered the HUD's
  top-left and looked like a missing stats panel. Check a full-resolution crop before fixing.
- **Diff the workspace against the last handoff before editing.** Unrecorded changes turned up once
  (an unfinished earlier pass at the same request). Review, keep what is sound, finish it, test it —
  never overwrite blind. A failed text replacement is often the first sign.
- **A mirror axis between pixels is at index (W−1)/2.** A 480-px image mirrors about 239.5, not
  240; centring one of a mirrored pair on 240 broke the carrier's exact symmetry. Re-mirror the
  repaired half afterwards to guarantee it.
- **Timers round up to the next frame.** At a fixed 60 fps a 0.25 s wait is 16 frames; a speed
  measured over it read 256 for 240. Measure over an exact frame count.
- **A property named after its own type hides which one you are calling.** `Hub.Yard` is a property
  of type `Yard`, and it is **null in the arena** (the arena skips `BuildWorld`). Yet `TickArena`
  does `Yard.TripClock += delta`, and the boss-kill path does `Yard.AddHostShare(...)` — both in
  the arena, both apparently dereferencing that null. They are safe only because C# binds the name
  to the *class* when the member is static, and all three members are. The day one of them becomes
  an instance member, three lines start throwing every frame and none of them look wrong.
  *Rule: when a property shares its type's name, say which you meant in a comment, and think twice
  before making one of that type's statics an instance member.*
- **Don't reuse Godot member names.** `Wing.Ready` hid the `Ready` signal and `StatsWindow.Name()`
  hid `Node.Name`; both compiled with only a warning. Treat build warnings as errors to read.
- **A real display reads the real pointer.** Injected mouse-motion events move the aim headless, but
  not under a window; use `Input.WarpMouse`, or set state directly, when rendering screenshots.
- **Test the part, not a behaviour with a re-aiming agent in the loop.** "Move the target during a
  bomber strike and expect no hits" failed 2 runs in 3: a bomber still lining up correctly aimed at
  the new position. The torpedo's no-tracking is now tested on single torpedoes. A flaky check is a
  finding — find out whose fault it is before touching either side.
- **Carried-over code carries old assumptions.** The menu kept Space Fleet Idle's title and a
  CONTINUE button for a save file Warships never writes.
- **PowerShell variable names are case-insensitive.** `$F` (the output filter regex) and a
  `foreach ($f in ...)` loop are the *same variable*; the loop silently overwrote the regex with a
  file path and the smoke test died parsing `C:\Users\...` as a pattern. Nothing warns.
- **PowerShell's `-match` is case-INSENSITIVE; `grep -E` is not.** Ported filters must use `-cmatch`
  / `-cnotmatch`. A case-blind `FAIL` also matches every run's own `fails=0` summary line, which
  turned a clean 417-pass run into "11 problems". A ported check that counts things must be
  re-validated against the count the original produced.
- **`ConfigFile.Save` writes in place, so a concurrent reader sees a torn file.** `Character.Save`
  saved straight over the live path while `Character.Load` and the enumeration read it. Two
  instances sharing one `user://` — the smoke test's six peers, or the documented "Run Multiple
  Instances" way of testing multiplayer — raced, and the reader got `ConfigFile parse error …
  Unterminated string`. Seen on 1 WSL run in 3, on the same file and line in two peers at once.
  **Fixed:** save to `<id>.cfg.tmp`, then `DirAccess.RenameAbsolute` over the real file, so the live
  path only ever holds a complete character. *Rule: any file a second instance might read gets
  written to a temp path and renamed into place, never saved over.* The same shape applies to
  `user://settings.cfg` if it ever grows a concurrent reader.
- **A telegraph pinned to the world drifts off the thing that draws it.** The boss's beam line was
  a fixed world segment computed once from its nose, while the boss kept closing and turning for
  the whole 6 s wind-up: the warning and the weapon parted company. Two halves to the fix, and both
  are needed — **hold the hull still** for the wind-up, and **parent the telegraph to the boss** so
  it is drawn in the hull's own frame. Parenting also buys guests the same tell with no extra
  traffic, because they already lerp the boss's rotation. *Rule: a tell belongs to the thing that
  makes it, not to the spot it was made at.*
- **A tell that rides the hull needs the hull's angle at a useful rate.** At 10 Hz a guest's copy of
  the boss was up to ~2.6° stale, which at the beam's 10000 u reach is ~450 u at the far end — far wider
  than the beam, so a guest could see a hit land outside the line it was shown. It sends at 30 Hz
  while a super move is locked down, and guests stop smoothing it: interpolation that was
  cosmetic becomes a lie once the hull is the sight. *Anything whose ORIENTATION becomes
  load-bearing needs both its update rate AND its interpolation revisited, not just its position.*
- **A check can pass for the wrong reason when the setup makes it vacuous.** "The boss is locked in
  place" asserted zero drift, and a mutant that ignored the lock entirely still passed: inside its
  hold-off the boss would not have closed anyway, so the assertion tested nothing but the
  flag. Arrange the conditions under which the behaviour would actually differ, or the check is
  decoration. Sibling of the constant-comparison lesson above.
- **A fallback that tidies up after itself makes the check blind.** `Character.Save` writes a temp
  file and renames it over the live one; if the rename fails it falls back to writing straight over
  the live file — and deletes the temp. So both paths end with the right contents on disk and no
  temp left, and the check "Save goes through a temp file and leaves none behind" **passes either
  way**. It could not distinguish the fix from the bug. Proved with a mutant that breaks only the
  rename: the file-based check still passed while every save took the unsafe path. `Save` now
  records which path it took (`LastSaveRenamed`) and the check reads that.
  *Rule: when a failure path cleans up like the success path, the files on disk cannot tell you
  which ran — the code has to say so.*
  (Verified separately, by asking the engine: `DirAccess.rename_absolute` over an EXISTING
  destination returns OK on Windows in 4.7.2 and replaces the contents. Worth knowing because the
  Windows CRT's `rename()` refuses an existing destination; Godot works around it. If that ever
  changed, every save would quietly take the fallback, and now something would say so.)
- **A node-addressed RPC to a peer in another sector is an engine error.** Godot routes RPCs by
  node path, so a packet for `Hub/Boss` reaching a peer that has not finished building the arena
  logs `Node not found` / `Invalid packet received`. The yard hit this first and `RpcHome` was the
  answer; the BOSS had the same hole in reverse, broadcasting its state and telegraphs to everyone
  while a guest was still loading. Both now go through `Hub.RpcToSector`. *Rule: anything that
  exists in only one sector sends to that sector's peers, never to all of them.* Raising the boss
  to 30 Hz while locked made the window three times easier to hit, which is how it surfaced.
- **Two smoke runs cannot overlap: they share one scratch folder.** Both runners copy the project
  to a fixed path (`/tmp/warships_smoke`, `%TEMP%\warships_smoke`), so starting a second run
  deletes the first's files underneath it. The victim then prints a wall of
  `ERROR: Cannot open file 'res://scripts/Raider.cs'` and
  `Failed to instantiate an autoload` — which reads exactly like a broken project, not like a
  clobbered temp folder, and it cost a full verification run to work out. `run.ps1` now refuses to
  start with a plain message when it cannot clear the folder. *Run them one at a time.*
- **A flaky result may be a real bug wearing a costume.** The extra errors above looked at first
  like environment noise, were not reproducible on demand, and the evidence was lost because the WSL
  VM shuts down between commands and takes `/tmp` with it. Copy logs out of `/tmp` in the *same*
  command that produced them, then run enough times to catch it.
- **Git for Windows sets `core.autocrlf=true` in its SYSTEM config.** Left alone, the next
  `git checkout` / `stash` / `reset --hard` / fresh clone rewrites every text file to CRLF — which
  changes every hash in `MANIFEST.sha256` (the integrity check this project opens with would report
  ~100 failures that are not real changes), breaks `CODE_SNAPSHOT.txt` the same way, and turns every
  `tools/**/*.sh` into `bad interpreter: /bin/sh^M` in WSL. A `.gitattributes` pinning `* -text`
  now disables conversion repo-wide; do not remove it. Caught before any checkout happened, so no
  damage was done — but `git add` warning "LF will be replaced by CRLF" is the only notice you get.
- **An unreliable RPC to a node the receiver may not have yet races Godot's own news.** Godot tells
  the other guests that a pilot joined on the reliable channel; that pilot's ship updates ride the
  unreliable one, so over a lossy link the first ones arrived at a guest with no ship for them yet:
  "Node not found ... Invalid packet received". A node that exists on every peer (the hub) takes
  them and drops what it cannot place. The same goes for anything new that talks unreliably at once.
- **`DisconnectPeer` is a polite hang-up, and ENet empties the peer at once.** Godot keeps listing
  the peer until the other side answers, and everything sent meanwhile fails ("max channels: 0") --
  unseen on one machine, a round trip of errors on a real link. The game never hangs up on one
  peer in session; the tests simulate a drop with `PeerDisconnectNow`, which is what a drop is.
- **A .NET export needs `Warships.sln`, the run button does not.** Godot's export plugin checks for
  `<assembly_name>.sln` in the project folder and fails every `.cs` file without it; the editor's own
  build falls back to the `.csproj`, so nothing else ever notices it is missing.
- **ENet's timeout is late, and a pre-handshake drop is not a "failure".** ENet looks at a peer's
  timeout only when a resend falls due, and its resends double (0.5, 1.5, 3.5, 7.5, 15.5 s): a JOIN
  set to give up in 12 s gave up after 15, a retry set to 5 after 7.5 -- which made three retries
  take 24.5 s. `Net._Process` holds the real deadline. And Godot raises `connection_failed` only for a
  link that never came up: one that came up and dropped before the host let the pilot in is
  `server_disconnected`, the same as a session lost -- so `OnHostGone` checks `Connecting` first.
- **ENet's timeout MINIMUM is the stall a session survives.** A silent peer is given up once its
  resends run out AND the minimum has passed, and on a fast link the resends run out in about a
  second: at 4 s a scene load or a GC pause ended sessions. It is 8 s (`Net.QuietMs`). And a live
  link's packet throttle is set never to fall (`Net.Link`): at its defaults ENet drops unreliable
  packets AT THE SENDER whenever a round trip comes back slower than the last few.
- **An ordered unreliable packet is thrown away when anything sent after it on its channel got
  there first.** On one shared channel, a ship report overtaking a base report cost the base report,
  on any link that reorders. Every such stream has a transfer channel of its own (`NetChannels`, a
  row each); what still shares one is the streams inside a single RPC (one pilot's ship against
  another's). And every such stream is addressed to the Hub, the one node at the same path in every
  world: a late unreliable report can land after its world has gone, and the Hub drops it where the
  base or the boss would have been "Node not found".
- **Closing an ENet peer still says goodbye to ENet.** `ENetMultiplayerPeer.Close` disconnects every
  connected peer at once, so a host that closes without the game's goodbye (`Net.SkipGoodbye`) is
  still noticed at once on a clean link; only a lost disconnect datagram leaves it to the timeout.
- **The plain `Godot_...win64.exe` writes nothing to stdout.** It is a GUI-subsystem binary, so
  every `GD.Print` from a headless run vanishes and the harness sees an empty log. Use the
  `_console.exe` beside it; both Windows runners swap to it automatically and refuse to run if it
  is missing.
- **Binary is a property of the bytes, not the name.** verify's text step skipped images and sounds
  by extension, so the first vendored DLLs were read as text and `-Quick` hung for half an hour on
  their millions of matches. A NUL in the first 8000 bytes is binary; git's `w/-text` is not the
  test, because it also calls a lone carriage return binary -- the very thing the step looks for.
- **A seeker whose mark is gone flies on and strikes what it meets** (`Shot`: homing is choosing,
  and there is nothing left to choose). A check that sinks its mark and leaves its missiles up
  poisons whatever check stands in their path seconds later: the Warden's twelve hunters hit the
  Echo's blast marks three cases on. A check downs what it launched (`Shot.Intercept`) before it ends.
- **A WebRTC connection freed mid-DTLS-handshake prints two plugin ERROR lines** (libdatachannel:
  "DTLS handshake failed", EOF). A guest holding an invite reaches the host before any reply is
  applied (SPIKE P2) and starts its handshake, so hanging up a pending invite does it whenever the
  timing lands. R0's pending-entry check never delivers its invite; a real guest will still print it.
  The same trap is why a guest adds an invite's candidates only after its STUN walk (`Link.Gather`):
  a walk remakes a connection whose row did not answer, and a guest that already knew the host's
  addresses could be mid-handshake when it does.
- **The codec is held to the plugin's own bytes.** A code carries only the five SDP values that vary
  and the candidates; the far side rebuilds the other 12 lines from `Rendezvous.Sdp.Template`. A
  plugin upgrade that changes or adds a line is refused by name when a code is made, and the solo run's
  byte-for-byte check (the spike's bundles and the live pair's) fails on its first run, never in the
  field.
- **`Link.Backlog` does not see the SCTP socket's own send buffer.** The plugin's buffered amount is
  what libdatachannel queues after that buffer is full: R1's first solo run put 1 MB on one row in one
  frame and read 0. The check puts until the row backs up; §3.8's backlog guard acts only past it.
- **The build's fingerprint must never be computed inside a type initializer, nor hash live state.**
  `Net.Protocol = Fingerprint()` as a readonly field initializer ran mid-way through Net's own
  initialization: it hashed itself as 0 (a readonly int is Plain) and every Net static declared below
  it as unset, so every fingerprint taken later in the process differed (R1: 3724c77b at startup,
  7991f5f3 at any moment after, "0 parts moved" within the run). It is a property now, set in Net's
  static constructor. The same class of fault: `Character.Bought`, the pilot's purchases, was a
  readonly int[] and so hashed live; it is a property over a mutable field. A value that is state, not
  build, is never a readonly static of a Plain type; the solo run's `BuildChecks` hold both.
- **A struct row was invisible to the fingerprint four ways.** `Net.Plain` took records and table
  classes, not structs, so an array of `Post` (the pirate base's site) was never hashed and two builds
  that placed its pylons differently met. The first fix (`StructRow`) demanded `IsReadOnlyAttribute`
  and the game's own assembly, which left three more kinds of constant table through the gate: a MUTABLE
  struct row (`StatusSet.Guards`: `StatusGuard[]`, no `readonly`; `EmplacementDef.Gun`: `TurretSpec?`,
  the pirate base's cruise missile) printed as its bare type name, its numbers invisible; a
  `System.ValueTuple`N` row (`Hub.PracticeTargets`, `Hub.Outposts`'s names) was skipped whole, being a
  different assembly than the game's own; and a row with a DELEGATE FIELD (`WaveCrew.Count`, a `Func`
  saying how many of a kind a wave brings; `Waves.Patrol`, `Waves.HuntPin`) was rejected outright by the
  all-Plain test, so neither `Waves.Patrol` nor `Waves.HuntPin` ever entered the fingerprint, and
  `WaveDef.Crew` printed as bare `[WaveCrew,...]`. Neither is less fixed for it: a value in a static
  readonly field or a table row is as constant as what holds it, and the harness's own checks that move
  a row and put it back (`BuildChecks`) prove the array element changes either way. `StructRow` now takes
  any value type of the game's assembly, or any `System.ValueTuple`N`, whose public instance fields are
  all Plain OR A DELEGATE, readonly or not; `Show` prints a delegate field only as its delegate type name
  (`Func`2`), null as `null`, so WHICH code is assigned and what it computes are NOT compared: a changed
  `Count` rule still goes unseen, while the row's other fields (`Kind`, `Way`, `Nth`, `At`, `Step`) are.
- **"No Character part" was the wrong bar for "nothing about the pilot."** `Character` carries its own
  `const` bounds (`Dir`, `MaxBonus`, `MaxStock`, `PaidKept`, `SaveDelay`) -- the same for every peer on
  this build whichever pilot is loaded, so they belong in the fingerprint and always were part of it.
  The `BuildChecks` solo check first asserted zero `Character.` parts at all and failed on its own
  build-time constants; it now names them and asserts only that nothing a PLAYER decided (`Bought`
  chief among them) is among the rest.
- **A worktree goes BESIDE the project folder, never inside it.** Both runners robocopy the whole
  folder (excluding only `.godot`, `.git`, `bin`, `obj`) into the scratch project, so a worktree
  under `.claude\` or anywhere inside would put a second copy of every script into the build.

## Smoke test

`tools/smoketest/run.sh <godot mono binary>` copies the project to a scratch folder, injects a test
autoload, builds with Godot's SDK, and runs it headless. It runs once solo (at a fixed 60 fps, so
timing checks are exact) and once as a host with a guest over localhost, and fails on any FAIL,
exception or engine error. The scratch copy runs as `WarshipsSmoke`, so it has its own `user://`
and cannot touch real characters — it creates and deletes them. It cannot see the screen, so layout
and look still need a real launch.

Two lessons from building it: a check that samples short-lived state once (a 0.1 s flash) is a
coin flip, so watch across frames; and order checks so none runs after the other process has ended
the session.

`tools/smoketest/run.ps1` is the same harness for Windows, driving the win64 mono build.

**The harness builds its own network.** Every role starts with `Router.Fake = (1, 1)` and a public-IP
service that refuses: no router, no internet, on every machine alike -- the old "two environmental
failures" off the sandbox are gone, and a test run can never open a port on the real router (the
game maps ports itself now). The plug-and-play scenarios point `Router.Fake` at
`tools/smoketest/fakeigd.py`: two fake routers on 127.0.0.1 and 127.0.4.1 answering UPnP, NAT-PMP and
PCP, in eleven networks (one router, two routers, the front one silent, a refusal, carrier-grade NAT,
a VPN, NAT-PMP with a reassigned port, PCP, none...). `run.ps1 -Wan` runs the multiplayer half
through `tools/smoketest/wan.py`, a relay of 90 ms each way, ±25 ms, 2% loss (`WARSHIPS_WAN="ms,jitter,loss"`
for another day). The runner fails a run whose process crashed (a negative exit code) -- a crash at
exit used to cut off the engine's leak report, so the one crashed run was the one that "passed".

**Use Ubuntu 24.04 for the WSL distro, not the default.** `wsl --install -d Ubuntu` now gives 26.04,
whose archive carries no .NET 8 at all — only `dotnet-sdk-10.0` — and this project targets `net8.0`.
24.04 carries `dotnet-sdk-8.0` and installs to `/usr/lib/dotnet`, which is exactly where
`typecheck.sh` looks for it.

## Screenshots

`tools/screens/run.sh` renders **41 frames** covering every screen state, and runs a **UI lint**
on each: off-screen controls, text wider than its box, overlapping HUD blocks. Lint lines start with
`LINT`; zero is the bar. The lint cannot judge taste, so the frames still get looked at.

`tools/screens/run.sh <godot mono binary>` renders real frames into `/tmp/shots/` on a virtual
display (Xvfb, Mesa software GL, compatibility renderer): the select screen, the creator, both
classes in the hub, the K window and a bomber strike. The smoke test cannot see, and these frames
have caught what it could not: a hull bar drawn over the carrier's nose, and previews too small
to read.

`tools/screens/run.ps1` is the Windows equivalent: **97 frames, SWEEP DONE, 0 LINT** (the last ten
are the nine new classes, a freighter with its turrets out and its bubble up, and the four new
enemy hulls in a row), matching the sandbox. It needs no virtual display, so the whole Xvfb dance — and the stale `/tmp/.X99-lock` trap
— does not apply. It renders on the real GPU with the project's own Forward+/Vulkan renderer rather
than Mesa software GL, which is the point of running it here: these frames are what the developer
actually sees. `-Compat` forces the Linux path (`opengl3` / `gl_compatibility`) when comparing runs
side by side. `Shots.cs.txt` hard-codes `/tmp/shots/`; the Windows runner rewrites that in its
*copy* to `%TEMP%\shots` and refuses to run if the string ever stops being there.

Note that Windows display scaling can enlarge the frames (1600x900 requested, 2560x1440 rendered at
160%). The lint is geometry-based and still passed at 0, so this reads as a stronger result, not a
weaker one — but frames from the two platforms are not pixel-comparable.

## The tables the game is made of (2026-09-22)

The rule in `CLAUDE.md` is "generalise, never special-case: a new boss, class, enemy, ability or
upgrade must be a row of data plus parameters, not a new `if`". These are the tables that rule
produced, and what each one replaced.

| Table | A row is | It replaced |
|---|---|---|
| `Ships.cs` → `Classes.All` | a class: art, mounts, its own numbers and rows, its fit, its abilities | art in `PlayerShip.Art`, numbers in a `V(bs, cv, dd)` helper, abilities in a dictionary, names in a table, four two-way tests |
| `Abilities.cs` → `Ab.*` | an ability: `Press`, `Refuse`, `Show` | a `case` in `UseAbility`, one in `DoAbility`, one in `AbilityBar.StateOf` — three files |
| `Enemies.cs` → `Enemies.All` | an enemy: sprite, tint, hull, damage, reach, boost, way | `Heavy ? this : that`, thirty times inside `Raider` |
| `Turrets.cs` → `ITurretHost` | what a gun is bolted to | a turret that could only belong to a `PlayerShip` |
| `Tags.cs` | what a thing IS | `h is Torpedo`, `h is Raider r && !r.Heavy`, `HitRadius < 20f` |
| `Statuses.cs` | what is being DONE to a thing | a bool and a timer per class, per effect |
| `Ids.cs` | an id space per kind | six hardcoded bases with six private counters |
| `Shots.cs` → `Shots.All` | a projectile: who it hits, how its path is tested, how it ends, how it looks | `Shell.cs`, `Slug.cs`, `Torpedo.cs` — three classes, one copy each of the same sweep, lifetime and hit, and three launch RPCs |
| `Fx.cs` → `Fx.All` | an effect: a shape, a colour, a life | one `Explosion` node, and everything else drawn as spokes of laser flashes because that was the only drawing a guest ever saw |
| `Ships.cs` → `ClassDef.Damage` | what a class calls its weapons, and what one pilot level adds to each | `Progression.DamageStat` naming one stat for every class, and `Equipment.DamageStats` naming them again per class |
| `Ships.cs` → `ClassDef.Kit` | the two parts a class is born with | three classes named in `Equipment`, and the battleship's mounts handed to everything else |
| `Equipment.cs` → `ItemDef.Needs` | the stats a part moves, and so the hulls it fits | `ItemDef.Class`: one class per part, which is why nine classes could wear almost nothing |
| `Landmarks.cs` → `Landmarks.All` | a place at home: where it stands, its footprint, art, label, scope mark, click, services and docks | the base, the TIO, the recycler and the outposts written out by hand four times in `Hub` -- layout, `BuildWorld`, `ScopeMarks`, `SelectAt` -- with three different click shapes, and the portal, the field and the belt as a further list of scope rows |

**What is deliberately NOT a table.** The two ways an enemy fights (PIN and STANDOFF), the
ways a boss move runs (`MoveWay`), and a boss's THROWN ROCK -- held in a tractor beam, thrown down a fixed lane on a cubic
ease, striking everything in the lane once and breaking at the end -- are BEHAVIOUR, and behaviour
that differs in kind does not compress into rows without inventing a scripting language to hold
it. The rock still shares the one path test (`Shots.Sweep`), which is the part that repeats. A boss's NUMBERS are rows: `Boss`
runs any `Missions.BossType` and its `BossMove[]`, so a third boss is one row and one move array,
a move shape no row can express is one more `MoveWay`, and a seventh enemy is a row unless it
wants a third way to fight.

**Per-ship ability state is four numbers** (`PlayerShip.Slot`: running, cooling, one the ability
names itself, a count), in the class's own ability order. That is why adding an ability costs no
field on the ship and no field on the wire: `NetHostState` carries four slot arrays where it used
to carry seven named figures, and a guest reads them by index.

**A part fits where it does something.** `Equipment.Fits` asks whether the hull has at least ONE
of the stats the part moves, because a part whose every stat is missing there would change nothing
on it. That one rule gives a Rapid Battery to all eleven classes with main guns, keeps Elite
Hangars on the carrier, lets a chip that lifts every weapon fit everything, and leaves the
point-defence frames off the hulls with no point defence -- without any of them naming a class.

**One door for damage** (`PlayerShip.Incoming`). The 0.52 s per-source gap, the tally, the shield
flash, the impact point, the death -- and `Guarded()`, where a dart's evasion, a warrior's
hardening and a freighter's bubble meet the blow. No weapon in the game knows any of them exist.

**A body in flight is point defence's alone -- unless its row gives it a hull.** `Tag.Missile` keeps a gun, a blow,
a wing and a click off a shot, and every row of `Shots.All` carries it unless it says otherwise. A row may say
`Tags = Tag.Hulled` (the cruise missile): its body takes a hull from whatever fires it (`TurretSpec.Hull`,
`Combat.Fire(hull:)`), wears down under every weapon a pilot has -- an EMP's blow, not its hold: a body in flight
carries no statuses -- and is picked like any hull. The one rule the two
kinds share is `Targeting.Throwable`: nothing in flight is thrown, because every peer flies it from its launch and a
throw happens on the host alone. A launcher's warning rides the launcher (`Turret.Warn`, an `FxRaise.Anchor`), so a
launcher brought down takes it along on every peer. The next shootable projectile is `Tags = Tag.Hulled` on its row
and a hull on its launcher.

## The harness is source (2026-09-22)

`tools/smoketest/SmokeTest.cs.txt` and `tools/screens/Shots.cs.txt` are compiled INTO the game by
their runners (as `scripts/_Test.cs` and `scripts/_Shots.cs`), but `dotnet build` never sees them.
A rename that missed a call in them passed typecheck, build, the analysers and the cross-reference,
and then failed three minutes into an engine run, after a full copy and import, as "the build
failed". `typecheck.ps1` now compiles both with `scripts/`, so that costs a minute instead.

The one breakage that gate still cannot see is REFLECTION at a private field
(`GetField("_bsCooldown")`), which is a string. Prefer a public accessor in the harness where one
exists -- the ability slots are reachable by name (`ship.Sl("broadside").Cool`), which is both
compiler-checked and good for any ability that ever exists.

`verify.ps1` also stopped asking for integrity outside `-Update`: anywhere else the manifest is by
definition the LAST change's, so it could only ever say FAILED, and a red line in every mid-session
verdict is how a real failure gets waved through.

## Next

- A real two-home session (everything multiplayer is proved on one machine and through a simulated
  internet only).
- Balance with the new gear in play; `Hub.BeginPlacement` for the first non-instant ability;
  wormhole transit into an instanced system; the RTS half of hybrid control.

## A release, and what installs it

`tools/pack.ps1` makes one and `tools/install.ps1` installs one; `PLAY.bat` -> `play.ps1` reaches
the installer on any machine without the developer tools. Everything in a release that might need
fixing -- the part split, the 189 explicit `in=` lines, cutting `NOTES.txt` out of
`docs/CHANGES.md` -- is decided in `pack.ps1`, on the developer's machine. `install.ps1` decides
nothing: it reads `BUILD.txt` from the release and checks each part against the SHA-256 that file
states. That matters because a player's copy of it is whatever the repo held the day they
downloaded it, and nothing here updates it.

**`version/MANIFEST.sha256` is the SOURCE manifest, not a release.** It is built from
`git ls-files` and contains no build output at all -- not one `.dll`. Shipping it to a player would
send them `art_source/`, `retired/` and `docs/` and still not name a file the game needs to run.
What it got right is the FORMAT, and `tools/manifest.ps1` is general enough to write both: one
hashing implementation, two callers (`verify.ps1 -Update` and `tools/pack.ps1`).

**The build string must never be `readonly` or `const`.** `Net.Fingerprint()` walks every static
field of every null-namespace type and folds in any that is `IsLiteral || IsInitOnly` with a
`Plain` field type -- and `string` is `Plain`. A `public static readonly string Build` therefore
enters the multiplayer protocol hash, and two byte-identical builds packed a minute apart would
refuse to play together. It is a plain mutable static behind a property, and a rung-3 check asserts
that structurally.

**The version does not go in the .exe.** `export_presets.cfg` leaves `application/file_version`
and `product_version` empty on purpose: `modify_resources=true` patches them into the PE resources,
which would change the 103 MB exe on every release and move it out of the part that stands still.
The id lives in `BUILD.txt` beside it. (`install.ps1` fetches every part today; the split is kept
for an installer that fetches only what changed.)

**`user://` is outside the install.** `%APPDATA%\Godot\app_userdata\Warships\` holds the saves, and
`project.godot` does not set `use_custom_user_dir`. `install.ps1` writes its install folder (`play\` by
default) and a temp folder of its own, and deletes nothing but that temp folder -- a part is
unpacked over what is there -- so a save is out of reach by geometry rather than by care.

**Build ids are opaque strings compared for equality, never ordered.** `2026-09-23.ec0d138` is a
date and a commit for humans. If the published id differs from the installed one,
`install.ps1` fetches and unpacks every part again, which makes re-publishing an older build work
as a rollback with no extra code.

**The download is public, and that is a decision, not an oversight.** Anything `install.ps1` can
fetch unaided, a player can fetch unaided: a token or key inside a file players hold is extractable
in a minute. Gating it for real needs a service in front of the download that `install.ps1` asks
instead of GitHub. It reads one base URL (`$Base`), so that is one line here -- and a player keeps
asking GitHub until they download the repo again.

## The sky: screen space, and a direction a distance check cannot see

`scripts/Sky.cs` owns it. A layer is a row; three of them draw one texture at different scales,
tints and rotations, each on a `SkyPlane`.

**The sky's CanvasLayer is SCREEN space.** It does not follow the camera, so a plane's `Position`
is a place on the screen, and parallax is `Position = -cam.GlobalPosition * Drift`: the world
slides by -1 x the camera, the sky the same way by a fraction. Writing it in world-space terms
(`+(1 - Drift)` x the camera) gives a sky that slides WITH the ship -- it reads as the stars
swinging round the hull, and it is what shipped once.

**A plane that only moves runs out of sky.** Far from the origin its drawn region slides off the
screen and the corners show as dark wedges. The region is re-centred on the whole tile under the
screen's middle instead; a tiled region moved by whole tiles is invisible, and the plane's
position stays continuous.

**Measure direction against the world, not distance.** A check that reads how FAR each layer
moved passes a reversed sky. Take a point fixed in the world through the canvas transform and
require every layer's screen shift to point the same way. (`Parallax2D.ScreenOffset`, from the
first version, was worse still: the raw camera offset, identical on every layer.)
