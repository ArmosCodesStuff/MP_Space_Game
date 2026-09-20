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
2. **The sell run is a choice at dispatch.** Send a hauler with an escort and travel with it for
   full value, or send it alone in non-interactive mode for a reduced payout.
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
  DISPATCH button with a per-pod progress bar** sends it once **at least one pod is full**; with
  **every pod full it leaves by itself**. Then: lift off, slow flat run east, blue aura, warp out,
  30 s away, warp back (its sale paid, 1 credit per unit), drift back, turn 180°, settle.
- **Upgrades** cost credits, in tabs **MINERS, SALVAGERS, HAULER** (plus **REFIT**) in the BASE
  menu (B). +10% upgrades cost 1.25× the last level; +1 upgrades cost 2× the last and stop at their
  cap (the button reads MAX). A guest's BUY and DISPATCH are requests the host checks.
- **REFIT**'s RESET is the only way into the ship menu (class, name, colours): 10% of ore, salvage
  and credits — the world's for its host, a guest's own parked totals for a guest — on a second
  click.

## Camera, radar, music, and playing over the internet

- **Camera**: wheel zoom between `DefaultZoom / ZoomOutMax` (33% further out) and `× ZoomInMax`
  (1.5). **Y** frees it; arrows or the screen edge pan it, tethered to `ClassArt.CameraRange`
  (5000; `PlayerShip.FighterCameraRange` 2500 for fighter-class ships). All in `Hub.MoveCamera`.
- **Radar** (`Radar.cs`): local only, draws what this machine knows; size is `Settings.RadarSize`.
- **Esc menu** (`EscMenu.cs`): the last Esc layer. Multiplayer cannot pause, so it locks the helm.
- **Music** (`Music.cs`, an autoload): both loops always play; their levels cross-fade by mood,
  which the hub sets each frame from the selection and `PlayerShip.InCombat` (host-tracked: dealing
  or taking damage within 8 s). `Music.CombatZone` forces combat for instanced systems. Silence it
  a moment before quitting (real time), or the mixer still holds the loops at exit.
- **Internet play** (`Net.cs`): ENet over UDP 27015 with a direct connection, so the host must be
  reachable. HOST tries UPnP on a background thread and reports one of: reachable from the
  internet (public address shown and copyable); router refused / no UPnP; carrier-grade NAT
  (100.64.0.0/10), which no home setting can fix. The fallbacks are a manual port forward, or a VPN
  such as Tailscale or ZeroTier (everyone joins the VPN; use its addresses). There is no relay
  server or NAT punch-through: that needs infrastructure outside the game.

## The batch after the review began (signed off by the player), in chunks

- **A (DONE)**: bomber docking; boss 600; fighters 2/shot; torpedoes 15; utility hull 120; guard 0.52 s;
  red missile tips; PD only missiles and light fighters; dummy 2 -> two practice fighters.
- **B (DONE; escorts 3 hull)**: the beam charges **6 s**; meanwhile the boss launches **2 light fighters, 45° to port and to
  starboard**, straight at the player, their boost lasting until they reach it (a pin to hold the pilot
  in the beam unless point defence — or, for a fighter pilot, their guns — kills them); live **3 s**,
  **0.25 s ticks, 50** (half the old tick, twice as long); the boss **raider red with a white skull**.
- **C (DONE — carrier measured 19.07 DPS; guns 5.9 a shell; control 1080 u)**: **battleship total DPS = 1.25 × carrier's**; **carrier range = 1.5 × battleship's**; the
  battleship's main guns fire **shells at 4× the player missile's speed (520 u/s), not tracking**.
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

## Built so far

| File | What it is |
|---|---|
| `Net.cs` | session lifecycle (host / join / offline), peer tracking, `Net.Sim` authority guard |
| `PlayerShip.cs` | naval helm, per-class art and mounts, ability dispatch, PD/missile state, salvo/staggered fire, replication |
| `Abilities.cs` | each class's abilities, default keys, remapping (swap on clash), the controls line |
| `AbilityBar.cs` | the bottom-centre bar: key, name, live state, recharge sweep |
| `Torpedo.cs` | unguided torpedo with smoke trail; host copy damages, guest copy is cosmetic |
| `Hub.cs` | the hub world and its host-owned resource tick, plus `Sun` and `Portal` |
| `ShipClasses.cs` | `Turret` (cursor-aimed mains, claim-aware PD), `Wing` (fighter; bomber strike cycle), `ShipClass`, `IHittable` |
| `Stats.cs` | `ShipStats`: every combat and flight number, base / bonus / final, derived DPS |
| `StatsWindow.cs` | the K window: Abilities & keys tab (remapping), Stats tab |
| `Combat.cs` | host-side target lookup by range, id and ray; hit flashes; torpedo launch hook |
| `TargetDummy.cs` | the hub's DPS meter |
| `Character.cs` | character slots on disk (list, load, save, delete, migrate); the nine-class table |
| `CharacterCreator.cs` | creator UI (new or edit), the 3-page class selector, `ShipPreview` (real sprite, real mounts) |
| `CharacterSelect.cs` | the select screen and delete confirmation |
| `SessionMenu.cs` | host / join / offline UI |
| `Settings.cs` | machine-local settings: volume and ability key bindings |
| carried over | `Txt` and `HealthBar` (in use), `Ui`, `Econ`, `Sound` (unused), the menu diorama, the sprites |

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

Each of these compiled clean and was wrong at runtime. The smoke test covers all of them.

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

`tools/smoketest/run.ps1` is the same harness for Windows, driving the win64 mono build. It runs the
whole suite: **417 pass, 6/6 runs finished**, against the 419 the sandbox reports. The two that
cannot pass here are not regressions — they assert the *sandbox's* network, and say so in their own
comments (`// no router in the sandbox`, `// no router, no internet in the sandbox`):

- `no UPnP router: LAN hosting stands, and the status explains the fix`
- `no router or internet here: network only, nothing to reveal`

Both hard-require `Net.Reach.LanOnly`. On a real machine behind a real router with real internet,
reachability resolves to something else and the assertion fails by construction. **Windows is
therefore a 417/417 bar, not 419/419**, until those two checks learn to branch on the environment.
Do not "fix" them by relaxing the assertion: what they verify — that a player with no route out is
told so, and offered the port-forward and Tailscale routes — is real behaviour worth keeping.

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

- Fleet ships you order around your own ship (the RTS half of the hybrid)
- Wormhole transit into an instanced hostile system
- Combat, loot drops, and dropping a haul at the refinery
- Refining loot into trade goods; hauler dispatch with the escort/alone choice
- Ship-as-character persistence, so your ship travels between worlds
