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
2. **The sell run is a choice at dispatch.** ESCORT: fly with the hauler the long way round while
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
reload, the fighters' target, and every wing craft's position and state. It also sends hit flashes,
the dummies' readouts, and each torpedo launch — guests fly a cosmetic copy of a torpedo, since its
run is straight and steady.

**Identity is owner-announced, and rides on the Hub.** Name, colours and class are the one
client-owned state. Each owner broadcasts its own; everyone stores it in `Net.Players`. It goes
through the Hub rather than the ship because the Hub exists on every peer before any ship does.

**Ships are rebuilt on every session change.** They are keyed and authorised by peer id, and
`LocalId` changes on host, join and drop. `Net.SessionChanged` fires; the world throws its ships
away and respawns from `Net.Players`.

**Offline is not a separate mode.** Single player is a host with no peers, so there is exactly one
code path and offline can never drift from online.

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
  their **open face** as if they were open-topped containers: the north side, except the top arm,
  which opens to the west. **Blue hologram bars** across each open face flash gently (brighter when
  the arm is taken). A full ship takes the free arm nearest it; if all five are taken it **joins
  the queue** (a line north-east of the top arm) and the head of the line takes the next arm to
  free up. Never two ships on one arm.
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
  1 credit per unit, or CARGO LOST), drift back, turn 180°, settle. An escort flies `Hub.EscortRoute`
  instead, the only move off the lane, while waves of raiders sent after it (their `Quarry`) hunt
  it; at the jump they withdraw and the sale pays 5x. **Nothing is ever lost to a trip or a save**:
  the Yard counts what the fleet and the hauler carry as home (`Yard.Banked`).
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
  (5000 for a capital ship). All in `Hub.MoveCamera`.
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

## The batch after the review began (signed off by the player), in chunks

- **A (DONE)**: bomber docking; boss 600; fighters 2/shot; torpedoes 15; utility hull 120; guard 0.52 s;
  red missile tips; PD only missiles and light fighters; dummy 2 -> two practice fighters.
- **B (DONE; escorts 3 hull)**: the beam charges **6 s**; meanwhile the boss launches **2 light fighters, 45° to port and to
  starboard**, straight at the player, their boost lasting until they reach it (a pin to hold the pilot
  in the beam unless point defence — or, for a fighter pilot, their guns — kills them); live **3 s**,
  **0.25 s ticks, 50** (half the old tick, twice as long); the boss **raider red with a white skull**.
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
- **Chunk 5 — utility hull and rebuilds (DONE)**: miners/salvagers 60 hull, hauler 150; destroyed ones are
  rebuilt at the base after 30 s for **10% of all money invested so far in that category's upgrades**
  (a running total per category: miner, salvager, hauler).
- **Chunk 6 — enemy fighters.** A variable **raider damage x**: **light fighters 1 DPS** (x), **heavy
  fighters 2x**. **Light fighters** (the black-and-red sprite, **2× a carrier fighter's size**) are
  **webifiers**: within **100 u** they **pin** a target — it is held to **20% of max speed with forced
  thrust and cannot turn** (a soft crowd control). They cruise **as slow as an unupgraded capital ship**
  but **boost to 500% for ~3 s** once about **1200 u** out (the distance ~3 s of boost covers, plus
  100 u), landing **near** the target, not on it, in **formation: ahead, left and right**. **Heavy
  fighters** (the battleship cut in half, skinnier, the **front turret only**, **~4× a light fighter's
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
  from becoming noise. Boss weapons flag their flashes `boss` for the deeper buzz.
- **One look** (`Ui`): shaded panels from small generated textures, one theme for all controls,
  installed on the root window. New UI takes the theme; nothing builds its own panel style.
- **Boss tiers**: 1.1^n, unlocked by beating the tier below, auto-selected at the TIO, saved per pilot.

## The arena

- **A scene change**: `Hub.GoTo` reloads the game scene with `Hub.Sector` set; the host tells every
  guest to do the same. Anything that must survive the trip lives outside the scene (static): the
  host's trip record (`Yard._trip`) and a guest's set-aside base (`Yard._own*`).
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
  plume, flank **port and starboard**, and web. The charge begins when their web *should* have
  landed — a **prediction** made at launch (shiver + run-in at boost speed, plus a second), never a
  wait on them arriving. So killing the escorts cannot cancel the beam; it earns you a beam you can
  fly out of, because the boss can only track at its own 0.3 rad/s while it charges. That is the
  whole shape of the mechanic: **beat the lights and the beam becomes dodgeable; ignore them and it
  cannot miss.**
- **The web ends the aiming, not just the dodging.** The first frame the pilot is webbed the boss
  stops turning too, and the red line it is already showing is the line it fires. This is a
  fairness rule rather than a balance one: a boss that kept tracking a target it had pinned would
  be chasing something that cannot dodge, and the pilot would watch the line follow them with
  nothing to do about it — the game visibly playing against its own telegraph. Freezing makes the
  telegraph a promise, and moves the pilot's last decision to *before* the web lands, where they
  still have a ship that answers the rudder. The lock resets at the start of every charge, so
  breaking the web on one cycle never carries into the next.
- **Point defence order**: missiles, then small craft, then anything else (`Turret.PdPriority`).
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

## Launch limits and targeting rules

- **Fighters launch at least 0.83 s apart**, always. It is a constant, never a stat, so nothing that
  scales rates of fire can touch it.
- **The missile needs a selected target in range.** Refusals are shown on the ability's own slot
  (`PlayerShip.Fail` / `FailNote`), the pattern for any ability that can be refused.

## Damage, death and the two colours

- **Who can be hit**: player weapons hit `Combat.Hostiles`; enemy fire hits `Combat.Players`. A
  player ship's collider is a capsule along its keel (`PlayerShip.Covers`); everything else is a
  circle (`IHittable.Covers`). Escape pods are in neither list: nothing can touch them.
- **Hits** go through `PlayerShip.Hit(damage, from)` on the host, which applies the damage and tells
  every peer which side to light on the shield (`ShieldFlash`: one generic hex panel, four sides).
- **Death**: 0 hull puts the ship into a 2-minute **stasis** where it lies; the owner flies an
  **escape pod**; afterwards **F** re-boards at 33% hull (a request the host decides). Stasis and
  hull are host state, replicated with the rest.
- **Hull colour** is the ship and its turrets. **Accent colour** is engines and lighting only:
  plumes on the player's ship and everything it launches, shields, PD arcs. Utility ships' engines
  are always light yellow (`Plume.Utility`). Missiles keep their smoke.
- **Fighters** fly strafing runs: 3 shots, through the target by 1.2× its diameter, turn, repeat;
  they live inside the carrier when docked. **Bombers** back into slots, nose out.

## Ship classes

Nine planned, three to a page in the selector, **two flyable**. The selector, save format and UI are
built for nine from the start rather than widened later. A class without its own abilities yet shows
"placeholder" as its controls line. **Class is chosen in the creator only** — there are no class
hotkeys (the developer removed 1/2).

| | Hull | Length | Main guns | PD turrets | Wing | Sprite |
|---|---|---|---|---|---|---|
| **Battleship** | 300 | 224 u | 4 × 1.5 DPS | 2 (slow, τ/3) | — | `battleship_hull.png` |
| **Carrier** | 200 | 170 u | — | 3 (fast, τ/1.2) | 4 fighters + 2 bombers | `carrier_player.png` |

**Damage**: main gun 1.5 per shot per second · PD 1.0 DPS per turret · fighter 0.5 DPS · torpedo 3 ·
missile 5. Every number lives in **`ShipStats`** (`Stats.cs`); turrets, wings and helm read it and
the K window prints the same object, so the two cannot drift. Stat = base × (1 + bonus); reload,
cooldown and radius bonuses divide. Bonuses are saved per character; nothing grants them yet.

### The helm: capital ships handle like naval ships

The developer's call: *no strafing, a turning radius, move as if in a medium* — and *slow*: speeds
and accelerations are 40% of their first values, with radii set so the full-speed turn is 20% faster
than it was (battleship 104 u/s on a 107 u radius, carrier 96 u/s on 127 u). W is ahead, S astern
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
duplicated; the fixed keys (W A S D, Tab, K, C, Esc, Enter) are refused. Bindings are per class and
belong to the machine (`settings.cfg`, section `keys`, only changed keys stored).

| Battleship | default | Carrier | default |
|---|---|---|---|
| Main guns (hold) | Space | Fighters: attack | Space |
| Fire mode (salvo/staggered) | V | Fighters: recall | R |
| Missile | F | Bomber strike | F |
| Reload missiles | R | Point defence | Q |
| Point defence | Q | | |

- **Main guns** swing toward the cursor at τ/4 and fire, while the key is held, along wherever each
  barrel points — a fast flick fires wide. Reach 720 u. **Salvo** fires every barrel once per reload;
  **staggered** fires one every reload ÷ barrels. Same rate: reloads **carry their remainder**
  (`cd += step`); resetting instead rounds each step up to a whole frame and staggered falls behind
  (measured 4.50 against 6.00).
- **Point defence is an active ability**: activation opens a 15 s firing window, then 15 s of
  recharge. While active, **each turret picks and tracks its own target** — the nearest in range
  that no sibling turret has claimed, else the nearest — so a group gets spread across. Battleship
  mounts swing slowly (τ/3); the carrier's fast (τ/1.2). 460 u reach, 8° firing cone.
- **Missiles** come in a magazine of 2 with 0.6 s between shots; **R** reloads it (16 s, nothing
  fires meanwhile). Each is a **bunker buster**: a heavy round launched off the nose at the target
  (the selection if in range, else the nearest) at a slow **130 u/s**, and only **barely guided** —
  its heading turns toward the target at no more than **0.35 rad/s**, so a target that moves early
  enough gets out from under it. It shares `Torpedo.cs` with the bombers' torpedoes (which have no
  guidance at all); guests fly a cosmetic copy with the same guidance.
- **Fighters** (17 u) hold orbit until **attack** sends them at the selected target; they fight
  while it is within the 1400 u control range. They fly in bursts: after **15 s of firing** a
  fighter returns to the **carrier's centre for a 3 s rest**, then rejoins. **R** recalls them.
- **Bombers wait docked** on the carrier's flanks, alternating port and starboard so the sides always
  split evenly (6 → 3 + 3), and rearm there (6 s). **Bomber strike** sends them at the target if it
  is within the **strike range, defined as twice the fighters' control range** (2800 u; it takes the
  same bonus). Bombers are 37.5 u. At launch distance (**567 u**) each swings its nose onto the target
  and launches 4 torpedoes straight ahead **while still closing slowly** (never quite stopped),
  then flies back to its own dock. Torpedoes run at **120 u/s** out to **1215 u**. A strike whose target goes out of range
  is called off. `PlayerShip.DockSlot` computes the slots; `ClassArt.DockX/DockY/DockSpacing` place
  them (wingtips just meeting the engine pods).
- **Six open hotkeys** (1–6 by default) follow every class's own abilities, for every class. They
  bind and remap like any ability and do nothing until something is assigned.
  **Torpedoes do not track**: straight line, steady 240 u/s, smoke trail, burst on the first hostile
  touched or at 900 u. A target that steps aside after launch is missed (checked on single torpedoes: a still target
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
every hostile needs a `NetId` that is the same on every peer (the dummies are 1000, 1001, 1002).

### Balance on paper

| | Sustained DPS, if everything hits | Kills the other in |
|---|---|---|
| Battleship | 7.60 (6.0 main + 1.0 PD at 50% duty + 0.60 missiles) | 26.3 s |
| Carrier | ≈ 3.5 (1.5 PD + ≈0.8 fighters on strafing runs + ≈1.2 torpedoes) | ≈ 86 s |

⚠ **The battleship wins by about 3.3×**, and more in practice: torpedoes are unguided. Recorded
rather than quietly patched. Levers, each a one-line change in `Stats.cs`: eight fighters instead of
four; fighters at 1.0 DPS; two main barrels instead of four; carrier hull at 300.

The **three target dummies** in the hub are the test bench: hostile, harmless, unkillable, each
reporting damage per second. The meter restarts itself on the first hit after 5 s without one.

### Art

- **Carrier** (`carrier_player.png`) is the developer's grey capital-ship drawing, mirrored
  left-onto-right to be exactly symmetrical. Its three painted domes are its three PD turrets.
- **Fighter** (`wing_fighter.png`) is the developer's black-and-purple fighter recoloured white:
  brightness remapped so shading keeps its direction, the outer outline kept dark against space, and
  the purple (the saturated pixels) taken to neutral with a faint cool cast on the canopy.
- **Unused art** (18 carried-over sprites nothing references) lives in `art_unused/`, which has a
  `.gdignore` so Godot never imports it. Kept deliberately, at the developer's request.
- **Background** (`stars.png`): 1024 px, seamless (stars near an edge wrap), on a screen-space layer
  at −100. Client-side only.
- **Bomber** (`wing_bomber.png`): the developer's small airframe, doubled in resolution, wings swept
  forward by a smooth warp (the tail booms stretch to follow), radiation trefoil on the nose.
- **The turrets are the painted ones, cut out.** Each painted turret was measured (ring centre from
  its dark pixels, barrels by where the pixels differ from the hull beside them), copied into its
  own sprite stored barrels-up with its pivot on the ring centre, and removed from the hull. The hull
  underneath was repaired **row by row**, blending between the clean pixels either side: the hulls'
  details run lengthwise and their bands run across, so a horizontal blend continues both, where
  OpenCV's inpainting smeared X-shaped marks. Blend only from opaque pixels, or a turret at the hull's
  edge pulls in black. Identical painted turrets share the cleanest one's cut-out. Battleship: 4
  double-barrel mains (one gun each) + 2 sponson PD. Carrier: 3 domes, all PD.
- **Mount offsets are measured, not placed by eye**: pixel-index centre → `(i + 0.5 − size/2) ×
  world-per-pixel`. They live in `PlayerShip.Art` with each turret's texture scale, barrel length
  (where shots start) and ring radius (where the PD arc sits).
- **Hull colour multiplies the sprite** (`Modulate`), turrets included. A black hull colour gives a
  black ship.
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
  not fitted, for either class), **unclaimed loot** (dropped at a kill, not yet flown over), and the
  **hints** seen with the tutorial's off switch. A file from another format is greyed out, never
  repaired. Things that come in runs (loot pickups, hints) save through `Character.SaveSoon`: 2.5 s
  after the last call, written at once on leaving or switching pilot.

## Gear, loot, the tutorial and reconnection (the owner's batch of 2026-09-21)

- **A part leans hard one way.** Four specialisations per slot type at three rarities: the upside
  grows with rarity (x1 / x1.5 / x2), the downside does not, so a rarer copy is strictly better but
  never free. The owner's carrier examples are literals in the tests (Elite III, Swarm III). No part
  touches a turret count: the turrets are painted on the hull, and a part cannot add a painted dome.
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
| `Yard.cs` / `Economy.cs` / `Hauler.cs` / `Gatherer.cs` | the idle economy: the base, its numbers, the hauler's runs, miners and salvagers |
| `Character.cs` / `Game.cs` | the pilot on disk (save format 2, the batched save) / the build's number and the way out |
| `Boss.cs` / `Raider.cs` / `Missions.cs` | the arena's boss, raiders and hunters, levels and rewards |

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

- **`Hub.InArena` changes before the new world exists.** `GoTo` sets the sector at once and swaps the
  scene at the end of the frame, so code waiting for "home" that reads the new world must wait for
  it to be built (`H.Yard != null && H.IsNodeReady()`). A check read a guest's file in between and
  saw loot the new world had not yet claimed.
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
- **A player still connecting is not a guest yet** (`IsHost` stays true until a host answers). Code
  that must know which end of a handshake it is on asks `Net.Connecting`, never `IsHost`.
- **Godot drops packets that overtake the last handshake packet** ("SYS_COMMAND_AUTH" in the log) --
  on a lossy path a resent handshake packet arrives after the guest's first words. The guest says its
  introduction twice more (`Hub.OnSessionChanged`); everything in it is safe to repeat.
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
- **Host-only state read by drawing code is a guest bug that nothing reports.** `Boss._beam` and
  `_charge` only tick under `Net.Sim`, and `Raider.Boosting` / `Shivering` are host-only fields;
  all four are read by code that draws. On a guest the boss's super-move bar sat at zero for the
  whole fight and the escorts' triple-length plume never appeared, and neither showed up as an
  error anywhere.
  *Rule: when you add something DRAWN from a value, ask which peers have that value. The solo run
  is the host, so it can never tell you.*
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
  650 u standoff the boss would not have closed anyway, so the assertion tested nothing but the
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
- **ENet's timeout is late, and a pre-handshake drop is not a "failure".** ENet looks at a peer's
  timeout only when a resend falls due, and its resends double (0.5, 1.5, 3.5, 7.5, 15.5 s): a JOIN
  set to give up in 12 s gave up after 15, a retry set to 5 after 7.5 -- which made three retries
  take 24.5 s. `Net._Process` holds the real deadline. And Godot raises `connection_failed` only for a
  link that never came up: one that came up and dropped before the host let the pilot in is
  `server_disconnected`, the same as a session lost -- so `OnHostGone` checks `Connecting` first.
- **The plain `Godot_...win64.exe` writes nothing to stdout.** It is a GUI-subsystem binary, so
  every `GD.Print` from a headless run vanishes and the harness sees an empty log. Use the
  `_console.exe` beside it; both Windows runners swap to it automatically and refuse to run if it
  is missing.

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

`tools/screens/run.ps1` is the Windows equivalent: **67 frames, SWEEP DONE, 0 LINT**, matching the
sandbox. It needs no virtual display, so the whole Xvfb dance — and the stale `/tmp/.X99-lock` trap
— does not apply. It renders on the real GPU with the project's own Forward+/Vulkan renderer rather
than Mesa software GL, which is the point of running it here: these frames are what the developer
actually sees. `-Compat` forces the Linux path (`opengl3` / `gl_compatibility`) when comparing runs
side by side. `Shots.cs.txt` hard-codes `/tmp/shots/`; the Windows runner rewrites that in its
*copy* to `%TEMP%\shots` and refuses to run if the string ever stops being there.

Note that Windows display scaling can enlarge the frames (1600x900 requested, 2560x1440 rendered at
160%). The lint is geometry-based and still passed at 0, so this reads as a stronger result, not a
weaker one — but frames from the two platforms are not pixel-comparable.

## Next

- A real two-home session (everything multiplayer is proved on one machine and through a simulated
  internet only).
- Balance with the new gear in play; `Hub.BeginPlacement` for the first non-instant ability;
  wormhole transit into an instanced system; the RTS half of hybrid control.
