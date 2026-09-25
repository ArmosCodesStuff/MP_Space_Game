# Protocol and replication review (read-only)

Scope: every `[Rpc]` in `scripts/`, `Net.cs`, Hub's sync loops, Yard and Boss state, NetPose, and
the Godot 4 ENet/SceneMultiplayer source they run on (checked against godotengine/godot master:
`modules/enet/enet_multiplayer_peer.cpp`, `enet_connection.cpp`, `enet_packet_peer.cpp`,
`modules/multiplayer/scene_multiplayer.cpp`, `thirdparty/enet/protocol.c`).

Tree state: another agent was editing the working tree while this was read. Hub.cs, Net.cs, Boss.cs,
PlayerShip.cs, Equipment.cs, Deployed.cs and others carry uncommitted edits. **Line numbers are as
read at the end of this pass and may move.** Each citation also names the method.

---

## 0. The owner's failure report: exact strings and the code that produces them

The owner saw "the port was not available" on the host. The friend saw "unavailable" or "port not
open". Two different host-side paths can produce the host's message, and they need different
fixes. **Ask the owner which one he saw.**

| Who | When | Exact text (status line in the MULTIPLAYER panel) | Code |
|---|---|---|---|
| Host | presses HOST, the socket binds | `Hosting on your network (<lan>:27015). Asking your router to open port 27015, and the internet for your public address...` | Net.cs:546 `Host` |
| Host | **the local UDP bind failed** (a second Warships is running, or a crashed one still holds 27015). The host is NOT hosting. | `Could not open port 27015: another program, or another copy of Warships, is using it. Playing offline.` | Net.cs:536 `Host` |
| Host | bound, but no router mapped the port and the public IP is known | `Hosting. Your router did not open port 27015 -- no router answered. Forward UDP 27015 to this PC (<pc>) in your router's settings (turn UPnP on there and it will do this by itself next time), then friends elsewhere join the address below. If friends still cannot get in, allow Warships (Godot) through the Windows firewall, for private AND public networks.` The `why` part can also read `-- the router refused (<router's errorDescription>)`. | Net.cs:451-455 `Describe` + Net.cs:403 `Firewall`; the `why` text comes from Router.cs:87 |
| Host | nothing mapped, and the router is behind another router | Net.cs:448-450 ("...and it sits behind a second router...") | `Describe` |
| Host | CGNAT | `Hosting for your network (<lan>). Your provider puts you behind a shared address (carrier-grade NAT), so no amount of port forwarding lets friends reach you directly. Use a virtual network (Tailscale, ZeroTier, Radmin VPN), or let a friend host.` | Net.cs:443-445 |
| Host | no router and no public IP | Net.cs:456-458 | `Describe` |
| Guest | JOIN pressed | `Connecting to <host> ...` | Net.cs:610 `Connect` |
| Guest | no answer within 12 s (`JoinTimeoutMs`, Net.cs:290, enforced in Net.cs:116), or ENet reports `ConnectionFailed` (Net.cs:98) | `Could not reach <host>. Check the address; if the host plays from home, their MULTIPLAYER panel says what their router still needs. Playing offline.` The HUD then reads `OFFLINE`. | Net.cs:292-293 `CouldNotReach`, reached via `Failed` → `GoOffline` |
| Guest | an ENet link came up and dropped during the Godot auth step | `Could not get in to <host>: it let the connection go before the handshake finished. Playing offline.` | Net.cs:308 `OnHostGone` |
| Guest | DNS failure | `Could not find <host>. Check the address. Playing offline.` | Net.cs:630 |

Notes:
- **In the MANUAL case, the reveal button and COPY ADDRESS both offer `<publicIp>:27015` anyway**
  (Net.cs:473 `Reachable` sets `InternetAddress` for Manual; SessionMenu.cs:67 copies it first). A
  host who skips the manual forward hands the friend an address that times out, and the friend
  then sees `Could not reach`. That fits the owner's report exactly.
- **A full session (8 guests) gets the same `Could not reach ... Check the address` text.** ENet
  refuses the connection, which surfaces as ConnectionFailed. The text is wrong for that case.

Nothing in the protocol layer, once connected, would stop a session from working. The owner's
failure is reachability. The findings below are what will make a session that does connect
**feel broken, drift, or drop**, and every one of them gets worse behind a relay. A relay is what
the "host cannot open a port" design will need, because it adds a hop.

---

## 1. Findings, ranked

Confidence: **confirmed** means read in code and engine source. **mechanism confirmed** means the
path is real, but how often or how badly it happens is estimated.

### P1 · ENet's packet throttle silently drops unreliable state at the SENDER (never configured) — mechanism confirmed

- Nothing calls `ENetPacketPeer.ThrottleConfigure`. A grep for `ThrottleConfigure`/`throttle` in
  scripts/ finds nothing.
- ENet (thirdparty/enet/protocol.c, `enet_protocol_check_outgoing_commands`) drops each unreliable
  command with probability `1 - packetThrottle/32`. `enet_peer_throttle` runs on every ACK and
  lowers `packetThrottle` by 2 whenever a sample RTT exceeds the last epoch's lowest RTT by more
  than 2x its variance. On a jittery internet path (the harness's `-Wan` default is ±25 ms), and
  above all right after a burst of reliable sends inflates the RTT (a catch-up, a salvo of
  `NetShot`), the throttle falls. ENet then throws away `NetShipState`, `NetHostState`,
  `NetRaiders`, `NetHulls`, `Yard.NetState` and `Boss.NetState` **before they reach the wire**, on
  top of real loss.
- Effect: raiders, ships, the boss and the fleet stutter or freeze. A guest's hull and ability bars
  go stale. The host's view of a guest ship dead-reckons and then stops, and after 1 s it forces
  `Trigger=false`, so the guest's guns stop firing (PlayerShip.cs:1210 `RemoteFollow`).
- Fix: set deceleration to 0 on every ENet link once it is up, so the throttle never falls:
  `peer.ThrottleConfigure(5000, 2, 0)`. On the host, do it per guest in `OnPeer(joined)`
  (Net.cs:685, next to `SetTimeout`). On the guest, do it in `OnConnected` (Net.cs:224).
  `enet_peer_throttle_configure` also sends the settings to the remote end, so both directions are
  covered.
- Prove it at rung 5 `-Wan` with `WARSHIPS_WAN="150,40,3"`. Count `NetRaiders` arrivals per second
  on a guest before and after the fix; a counter in the harness is enough.

### P2 · `Net.IsOnline` is true during the Godot auth handshake, so a joining guest runs the ONLINE-HOST role — confirmed

- Net.cs:37-38: `IsOnline` reads the ENet peer's `GetConnectionStatus()`. ENet sets `CONNECTED` at
  the ENet CONNECT event, before SceneMultiplayer auth (enet_multiplayer_peer.cpp `poll`, client
  branch). `_isHost` stays true and `LocalId` stays 1 until `OnConnected`.
- From ENet connect until auth completes (at least one RTT, and up to `AuthTimeout` = 10 s on a
  lossy path, Net.cs:91), the joining player's world therefore acts as an online host:
  - `PlayerShip._Process` → `SendHostState` (`Net.Sim && Net.IsOnline`) and `SendState` →
    `Rpc(NetShipState)` both broadcast.
  - `BroadcastVotes`, `BroadcastMission`, `SendShield` and `PilotChanged→SendIdentity` broadcast
    too, if triggered.
- A client broadcast goes through `SceneMultiplayer.send_command`'s relay branch. That branch sends
  to peer 1 **without checking admission**. The host still has the guest in `pending_peers`, so
  `poll()` hits `ERR_CONTINUE(... != SYS_COMMAND_AUTH)` and drops the packet with an engine error.
  If the host has already admitted the guest, the Authority-mode ones are refused with "RPC not
  allowed" errors.
- These are exactly the `SYS_COMMAND_AUTH` errors tools/smoketest/run.ps1 whitelists under `-Wan`
  (`$dropRe += '|SYS_COMMAND_AUTH'`). The comment there calls it "a packet which overtook the
  handshake's last (lost, resent) one". The root cause is that the guest sends game traffic before
  it has been let in. The whitelist now hides any real auth-path error.
- Visible symptom: during the handshake the joining player's HUD reads `HOSTING (1)` (Hub.cs
  `_Process` HUD line), the panel reads `hosting · 1`, and COPY ADDRESS is visible
  (SessionMenu.cs:99-100).
- Fix: `IsOnline => I != null && I._inSession && I._peer != null && status == Connected`.
  `_inSession` becomes true only in `Host()` (Net.cs:540) and `OnConnected` (Net.cs:221). Then
  drop the `|SYS_COMMAND_AUTH` whitelist from run.ps1.
- Prove it at rung 5 `-Wan` with the whitelist removed: zero `SYS_COMMAND_AUTH` lines.

### P3 · A guest's gear-part LEVELS are not on the wire — confirmed at HEAD; **being fixed in the working tree right now**

- At HEAD, `Equipment.LevelOf` reads the LOCAL `Character.GearLevel`, and `NetIdentity` carries part
  ids only. The host therefore resolves every guest's hull and damage with the **host's** levels
  for those part ids, and the host is authoritative for both. A guest's salvage spent on levels
  does nothing online.
- The uncommitted working tree adds `gearIds`/`gearLevels` to `NetIdentity` (Hub.cs:771 area),
  adds `PlayerInfo.GearLevel`, `SetEquipment(ids, levels)` (PlayerShip.cs:396) and
  `Equipment.SanitizeLevels`. It re-announces on purchase through `PilotChanged`
  (EquipmentWindow.cs:148).
- It needs rung 5 to prove it: a guest with a levelled part, and the host's sheet for that ship
  asserted against a literal.

### P4 · The boss sends NOTHING while it is Held (a freighter's shockwave) — confirmed

- In `Boss._Process`, `if (Held) { QueueRedraw(); return; }` (Boss.cs:287) comes **before** the
  `_send`/`RpcToSector(NetState)` block (Boss.cs:294-305).
- For `wave_disable` seconds, guests get no boss hull, pose or super timer, while the party is
  pouring damage into a stationary target. The guest's hull bar freezes and then jumps. The
  guest's super bar keeps counting down locally (Boss.cs `_netNextSuper -= delta`) while the host's
  super timers are frozen, then snaps back.
- Fix: move the send block above the `Held` return (and above `if (!Alive) return` if a final zero
  is wanted; `NetWon` already covers the death).
- Prove it at rung 5 (the arena roles): hold the boss, damage it, and have the guest's `Boss.Hp`
  follow within 0.3 s.

### P5 · The shockwave moves Structures and Dummies on the host only; their positions never replicate — confirmed

- `PlayerShip.Shockwave` (PlayerShip.cs:563-583) pushes every Attackable hostile node that is not
  tagged Boss (`n.Position += ... * push`, PlayerShip.cs:579). That includes `Tag.Structure`
  emplacements (the pirate base and its pylons, Emplacements.cs:130) and `Tag.Dummy` target dummies
  and practice fighters (TargetDummy.cs:28).
- Emplacements replicate hull only (`NetHulls`; Spawned.cs says "`At` never changes"). Dummies are
  built on each peer and never replicated. After one shockwave the host's pirate base is 100s of
  units from where every guest draws it, and guests aim at an empty spot.
- A late joiner's catch-up sends the *moved* position (`Seed` reads `e.Position`), so two guests
  can disagree with each other as well.
- Fix, and it generalises: the shockwave should push only what is on the wire as moving. Add
  `forbid: Tag.Structure | Tag.Dummy` to the filter it uses, or a `TargetFilter` row for
  "throwable". The alternative is to replicate emplacement position, which is wrong for a thing
  that "holds a spot".
- Prove it at rung 5: shockwave next to an emplacement and assert the guest's position equals the
  host's.

### P6 · Warning timing: no host timestamp, reliable HOL delay, and the host's smoothing lag are uncompensated — mechanism confirmed, magnitude estimated

`Net.Arriving(w) = max(0.4w, w - RTT)` (Net.cs:716) shortens a guest's warning by one ENet-smoothed
RTT, measured when the warning arrives. Two things still make a guest who cleared the red on its own
screen get hit:

1. **The host judges a remote ship at its SMOOTHED position.**
   - `RemoteFollow` lerps `Position` toward the dead-reckoned `_netPos` at `12*dt`
     (PlayerShip.cs:1210-1226). The steady-state lag at constant speed is about 1/12 s, roughly
     83 ms.
   - Every host hit test reads `p.Position`: the beam, dash and ring (Boss.cs `Burn`/`Land`) and
     the missile blast (Hub.cs `TickBlasts`).
   - `Arriving` does not include that lag, so the guest is judged about 80 ms "early" even on a LAN.
     That is more than LAN latency.
   - `Net.RoundTrip` is 0 on the host (Net.cs:718-720), so nothing host-side compensates either.
2. **Head-of-line delay is invisible to the guest.**
   - `NetFx` (warnings), `NetMissile`, `NetRock`, `NetSpawn`, `NetKill`, `NetMission`, `NetVote`,
     `Yard.NetTotals` (1 Hz, reliable) and `NetIdentity` all share ENet channel 0 (reliable,
     ordered).
   - One lost datagram holds every warning queued behind it for about RTT + RTO. At 2-5% loss and
     150-250 ms RTT that is roughly +250-500 ms on a few percent of warnings.
   - The warning carries no host timestamp (`FxRaise.Since` only serves the catch-up), so the guest
     cannot subtract the delay.
- Fix:
  - Put a host clock on the wire. The host's `Time.GetTicksMsec()` in `NetHostState` is enough, and
    the guest keeps offset = hostTime - localTime - RTT/2.
  - Stamp each `NetFx`/`NetMissile`/`NetRock` with its host raise time, and have the guest show
    `end = raise + w - RTT/2 - viewLag`.
  - Either judge remote ships at `_netPos` rather than the smoothed `Position`, or subtract a
    `RemoteLag` constant (1/12 s) in `Arriving`.
  - Move `NetFx` to its own reliable channel, so economy and spawn traffic cannot block a warning.
- Prove it at rung 5 `-Wan`: a guest that leaves the zone exactly at its displayed end must not be
  hit (vary the geometry with `VaryNear`).

### P7 · A guest's cursor-aimed main guns fire at where the target was about 2 one-way trips plus view lag ago — mechanism confirmed

- Main turrets swing to the raw cursor: `Swing((Host.AimAt - GlobalPosition).Angle(), ...)`
  (Turrets.cs:125). `AimAt` is the guest's `AimPoint`, which reaches the host one one-way trip late
  (PlayerShip.cs:1232 `ApplyState`).
- The guest put its cursor on a raider it draws at "host position one one-way trip ago + NetPose
  lerp lag". NetPose extrapolates only the packet age (Net.cs:772-801, `Ahead` 0.25 s), not the
  1/rate = 100 ms lerp lag.
- Net effect at 150 ms RTT against a 300 u/s raider: shots land about 75 u behind a target whose
  hit radius is tens of units. Guests "can't hit anything" and the host can.
- Fix options:
  - (a) When the cursor is over a hostile, send its NetId plus the cursor's offset from it. The
    host aims at its own current position of that hostile plus the offset.
  - (b) Cheaper: the host extrapolates the guest's `AimPoint` along the hostile nearest to it by
    (guest RTT + 0.1 s).
  - Both keep host authority.
- Prove it at rung 5 `-Wan`: a guest firing at a moving practice fighter hits it at a rate within
  X% of the host's.

### P8 · Authority hole: a guest can full-heal and reset every cooldown mid-fight by re-announcing its class — confirmed

- `NetIdentity` accepts `cls` at any time (Hub.cs:771 area). `ApplyIdentity` → `SetIdentity` → a
  class change calls `SetClass` → `FitClass` (PlayerShip.cs:409, 282), which sets
  `MaxHp = Hp = Stats["hull"]` and rebuilds `_slots` (every cooldown at zero, the magazine full).
  Announcing class B and then A heals to full and resets the whole bar.
- Honest clients change class only at REFIT (home only; `ResetShip` returns in the arena), but the
  host never checks.
- Fix: the host refuses a class change while `InArena` or while the ship is `InCombat`. Otherwise
  it applies it through the Restat path, keeping the hull fraction (as `SetEquipment` already does)
  and not refreshing slots.
- Everything else guest-sent is guarded and was checked. `NetShipState` is keyed to the sender's
  ship. `NetMySector`, `RequestVote` and `RequestBuy` use `FromPlayer`. `RequestAbility` requires
  `who == OwnerId` (PlayerShip.cs:453). `NetIdentity` requires sender == peer. `NetBye` only
  affects the sender. Godot enforces `RpcMode.Authority` on relayed packets too (the target checks
  node authority, which is 1 for Hub/Yard/Boss), so a guest cannot call a host-only RPC on another
  guest through the relay.

### P9 · Stale sector report plus an unconditional reload can rebuild a guest's arena WITHOUT its spawns — mechanism confirmed, rare

- `NetMySector(int s)` carries no world or generation number (Hub.cs:537). `EnterSector` forgets
  every peer's sector (Hub.cs:893).
- A guest report sent just before the guest heard `NetSector` arrives at the host's new world
  claiming the old sector. The host records it wrongly and answers `NetSector` again (Hub.cs:541).
- The guest's `NetSector` → `GoTo` reloads the scene **even when it is already in that sector**
  (Hub.cs:901-906). The reloaded arena reports again, and if that report falls within 0.5 s of the
  first, `Net.Metered` (Hub.cs:547) skips the catch-up.
- The catch-up is the only way a guest learns of emplacements and garrison raiders spawned while
  `Session.Sectors` was empty. The result is an arena with no pirate base and no escorts on that
  guest.
- Fix:
  - `GoTo(k)` does nothing if `k == Sector` and the kind and level match.
  - `NetMySector` carries the world serial the host sent in `NetSector` (`_world`), and the host
    ignores stale ones.
  - The catch-up is never metered for the first report of a world.

### P10 · Reconnect leaves state keyed to the OLD peer id — confirmed, low

- Deployed turrets keep `OwnerId` = the old id. `RestoreHeld` (Hub.cs:674) re-keys votes only. The
  returning freighter pilot:
  - cannot collect them (PlayerShip.cs:507 `t.OwnerId != OwnerId`);
  - is not counted against its deploy cap (PlayerShip.cs:497), so it can exceed the cap;
  - finds they fire with "spare" stats (Deployed.cs:50-52; `ShipOf(old id)` is null).
- `OnPlayerLeft`'s "already back under a new id" branch (Hub.cs:656 `back`) is effectively
  unreachable, because `NetIdentity` blanks a `CharacterId` that a live peer still holds
  (Hub.cs:793). Recovery relies on the 1 s and 3 s identity repeats landing after the old peer times
  out. It usually works, because both ends time out within about 1 s of each other.
- Fix: in `RestoreHeld`, re-key `DeployedTurret.OwnerId`/`Ship` from `h.OldPeer` to `peer`. Delete
  the dead `back` branch, or let `NetIdentity` accept an id whose owner is in `Session.Places` or is
  timing out.

### P11 · Timeouts are aggressive: a 4 s stall with outstanding reliable data drops the session — design risk

- `SetTimeout(32, 4000, 10000)` is set on the host per guest (Net.cs:685) and
  `(32, 4000, 12000)` on the guest (Net.cs:224). The ENet defaults are 5 s and 30 s.
- ENet disconnects once 6 send attempts have passed **and** 4 s have elapsed, or at 10-12 s
  regardless (protocol.c `enet_protocol_check_timeouts`).
- At LAN RTT the 6 attempts take about 1.5 s, so **any 4 s main-thread stall** on either machine
  ends the session. ENet is serviced from Godot's main loop. Candidates: a first-time scene or
  texture load, a GC pause, a Wi-Fi roam, a laptop lid.
- Consider timeoutMinimum 8 s and maximum 20 s. The "ghost ship" concern that motivated 10 s is
  already handled by `RemoteFollow`, which stops dead reckoning after 0.5 s and the trigger after
  1 s.

### P12 · `NetShot` is reliable for every shell — improvement

- It sits on its own channel (Hub.cs:1010 `Cosmetic = 1`), so it no longer blocks channel 0. Under
  loss it still arrives in bursts behind a lost shell. `Lead = min(0.6, RTT)` (Hub.cs:1709) ignores
  that extra delay, so shells appear late and far down range.
- Keep reliable only for rows that are interceptable or that `NetMissileDown` must find. Send
  plain shells `UnreliableOrdered` with a host timestamp (P6), so a guest can advance each one by
  its true age.
- The channel is valid: `create_server`/`create_client` with channel count 0 opens 255 ENet
  channels (enet_connection.cpp `connect_to_host` maps 0 to the maximum), and custom channel 1
  maps to ENet channel `SYSCH_MAX(2)+1-1 = 2`.

### P13 · Minor

- `Host()` calls `EndLetGo()` right after `Close()` (Net.cs:532). When a **guest** presses HOST,
  that kills the goodbye it just queued, so the old host holds its place for 90 s. The comment's
  reason ("this port is about to be bound again") applies only to a host re-hosting. Fix: call
  `EndLetGo` only when the old peer was a server.
- `PlayerShip.NetId = NetIds.In(Player, OwnerId)` = `2000 + OwnerId % 1000` (PlayerShip.cs:41).
  ENet peer ids are random 31-bit numbers, so two pilots collide 1 time in 1000. On guests, a
  cosmetic guided shot aimed at one pilot then homes on the other. Fix: key by seat, or by
  `Players` index.
- The DNS lookup takes IPv4 only (Net.cs:617), so a hostname with only AAAA records fails as
  "Could not find".
- `NetHostState` sends every ship's slot arrays (Left/Cool/Own/N) to every peer at 10 Hz. Only the
  owner draws them. Sending them `RpcId(owner)` and the rest to everyone cuts about 40% of the
  host's upload.

---

## 2. Checked and sound (do not re-audit)

- **No "Node not found" window across a scene change.** `ChangeSceneToFile` is deferred.
  `SceneTree::process` polls multiplayer, then flushes the deferred call (the old Hub leaves), then
  adds the pending scene in `_flush_scene_change` in the **same frame**. The next poll always finds
  the new `/root/Hub`. Packets after `NetSector` in the same poll land on the old Hub. Session-wide
  ones (`NetMission`, `NetVote`) are re-sent by the new world's catch-up, and `NetKill` works on
  statics.
- **World traffic is gated by sector.** `ToWorld`/`RpcToSector` send only to peers that reported
  the host's sector (Hub.cs:519-530). `EnterSector` forgets all peers first, and the guest's first
  report brings everything (Hub.cs:537-565). Boss and Yard use the same gate.
- **The first RPCs after join are ordered after the auth completion.** Auth, completion and Hub
  RPCs share ENet channel 0, reliable and ordered. The host's `SendIdentity(peer)` from
  `OnPlayerJoined` cannot beat the guest's admission. Relayed ADD_PEER precedes relayed
  identities. The 1 s and 3 s repeats (Hub.cs:597) are harmless. With P2 fixed they are only
  insurance.
- **MTU.** Every unreliable packet is under about 900 B. (Sizes use Godot's Variant encoding:
  4-byte header plus payload, float32 when exact.)

  | Packet | Size |
  |---|---|
  | `NetRaiders` chunk of 24 | ~820 B |
  | `Yard.NetState` (max 10 gatherers + hauler) | ~560 B |
  | `NetHostState`, battleship | ~300 B |
  | `NetHostState`, carrier with ~10 wings | ~500 B |
  | `NetShipState` | ~115 B |
  | `Boss.NetState` | ~70 B |

  `NetHulls` is unchunked but bounded by turrets plus emplacements. Reliable messages
  (`NetIdentity` with gear levels, a few hundred bytes to about 2 KB) fragment safely. No packet is
  unbounded apart from what a malicious guest might send in its own arrays.
- **Host leaving.**
  - With a goodbye, `NetBye` sets `_hostSaidBye` → "Host closed the session."
  - A crash → 3 retries at 2/8/14 s, then RECONNECT.
  - In the arena, `OnSessionChanged` sends the guest home (Hub.cs:585).
  - The parked yard is restored (Yard.cs:89).
- **Guest dropping.** The host holds the place for 90 s: hull, stasis, position, votes, and kills
  owed by serial. The ship is despawned, `RosterChanged` rebroadcasts the mission and votes, and
  other guests get DEL_PEER.
- **Mid-mission join.**
  - The guest reports Home, gets `NetSector(Arena, kind, level)` and loads the arena. The boss is
    built from the same row; `HullMult` is corrected by `NetState`.
  - The catch-up sends mission, votes, every spawn with live hull, the boss's warnings with time
    left, the rock, and `NetWon`.
- **Raider packets vs spawn/gone on different channels.** Unknown ids are ignored on both sides
  (Hub.cs `NetRaiders`, `NetHulls`), and `Spawn` is idempotent.
- **Build fingerprint.** It includes every RPC signature, mode and channel. Nothing
  machine-dependent is folded in: `static readonly` Random/time fields are not `Plain`.

---

## 3. Send rates, channels, and estimated host upload

| RPC | Dir | Mode / ENet ch | Rate | Size |
|---|---|---|---|---|
| NetShipState | owner → all (guest's via host relay) | UnrelOrdered / 1 | 20 Hz per ship | ~115 B |
| NetHostState | host → all | UnrelOrdered / 1 | 10 Hz per ship | 300-500 B |
| NetRaiders | host → sector | UnrelOrdered / 1 | 10 Hz, 24 per packet | ≤820 B |
| NetHulls | host → sector | UnrelOrdered / 1 | 10 Hz per kind | small |
| Yard.NetState | host → home | UnrelOrdered / 1 | 10 Hz | ~560 B |
| Boss.NetState | host → arena | UnrelOrdered / 1 | 10 Hz, 30 Hz locked | ~70 B |
| NetFlash / NetShield / NetDummy / Boss.NetSound | host → sector | UnrelOrdered / 1 | per event | ~60-90 B |
| Yard.NetTotals | host → home | Reliable / 0 | 1 Hz + on purchase | ~200 B |
| NetFx, NetMissile, NetRock, NetSpawn(Gone), NetKill, NetMission, NetVote, NetIdentity, NetSector, NetPlace, NetWon, NetPortalFlash, Request* | various | Reliable / 0 | per event | — |
| NetShot, NetMissileDown | host → sector | Reliable / 2 ("Cosmetic") | per shell | ~100 B |

- Godot's `ENetMultiplayerPeer.put_packet` calls `flush()` after every send. **Every RPC is its own
  UDP datagram** with about 48 B of overhead, and ENet never coalesces them.
- Estimate for a 4-player home raid with 40 raiders: about 45-55 KB/s per guest, so **about
  1.1-1.3 Mbit/s of host upload** for 3 guests, and about 200 datagrams/s per guest.
- That is fine on cable or fibre. **It saturates a 1 Mbit DSL upload**, which inflates RTT, which
  drives P1's throttle down.
- A relay must carry the same volume, plus guest↔guest traffic twice, because SceneMultiplayer
  relays it through the host.

## 4. What a guest shows between packets

| Thing | Method | Lag it leaves |
|---|---|---|
| Other players' ships | dead-reckon `_netVel` ≤0.5 s, lerp 12/s, snap >600 u (PlayerShip.cs:1210) | one-way (+ one more hop if another guest's) + ~83 ms |
| Raiders, boss, wings, gatherers | `NetPose`: velocity from arrivals, extrapolate ≤0.25 s, lerp 10/s (Net.cs:772) | one-way + ~100 ms; boss taken flat while Locked |
| Pods | lerp 12/s, no extrapolation (EscapePod.cs:28) | one-way + 50 ms send gap + ~83 ms |
| Cosmetic shells | simulated from launch, advanced by `Lead = min(0.6, RTT)` | own shots right; others' over-led by one one-way trip |
| Warnings, missile marks, rock hold | shortened by `Arriving` = RTT (floor 40%) | see P6 |
| Host's view of a guest | same as "other players", and **hit-tested at the smoothed position** | see P6 |

## 5. What any "host cannot open a port" transport must give the protocol

The protocol is transport-agnostic apart from the following. A new transport must keep all of it,
or change these call sites in the same edit.

1. **Peer id 1 is the game host.** Code that depends on it:
   - `Net.AskHost` → `RpcId(1)` (Net.cs `AskHost`);
   - `NetBye`'s `from == 1`;
   - `SendIdentity`'s `LocalId == 1` guard;
   - `RoundTrip` via `GetPeer(1)`;
   - the default multiplayer authority of Hub, Yard and Boss (`RpcMode.Authority` → 1).

   A relay SERVER must therefore **not** become peer 1. It has to sit below `MultiplayerPeer`: a
   C# `MultiplayerPeerExtension` over an outbound ENet link to the relay, or `WebRTCMultiplayerPeer`
   in server/client mode, where the game host is the server and gets id 1.
2. **Transfer modes and channels:** Reliable (ordered) ch0, UnreliableOrdered, and at least one
   extra reliable channel (`Cosmetic`). `WebRTCMultiplayerPeer` needs a `channels_config` for it.
3. **Server relay:** `is_server_relay_supported()` must be true. Guests learn of each other only
   through SceneMultiplayer's relayed ADD_PEER/DEL_PEER, and `Hub.OnPlayerJoined` spawns their
   ships from that. Guest-to-guest traffic (`NetShipState`, `NetIdentity`) is relayed by the host.
4. **The ENet-only calls to wrap behind one seam** (a `Link` with SetTimeout, DisconnectLater,
   RoundTrip, and LetGo/Poll/Close):
   - Net.cs:155 `PeerDisconnectLater` (refusing a build);
   - Net.cs:224, 634, 685 `SetTimeout`;
   - Net.cs:276-283 `_letGo.Host.GetPeers()`/`GetState`/`Poll`/`Close`;
   - Net.cs:654-659 `PeerDisconnectLater` on the goodbye;
   - Net.cs:718-720 `RoundTrip` from `ENetPacketPeer.PeerStatistic.RoundTripTime`;
   - Net.cs:534/628 `CreateServer`/`CreateClient`.
5. **The auth fingerprint** (`AuthCallback`/`SendAuth`/`CompleteAuth`) is SceneMultiplayer's own
   and works over any `MultiplayerPeer`.
6. **Latency budget.** Through a relay every one-way trip gains the relay leg. A guest seeing
   another guest pays host↔relay↔guest twice. P1, P6 and P7 go from "noticeable" to "broken"
   behind a relay. Land them with the transport, not after it.
7. Whatever the transport, **fix P2 first**. "Connected" must mean admitted, or every new peer type
   repeats the pre-auth traffic.

## 6. Cheapest proof per fix

| Fix | Rung |
|---|---|
| P2, P3, P8, P9, P13 | 5 (host + guests); P2 with the run.ps1 whitelist removed |
| P1, P6, P7, P12 | 5 `-Wan`, with `WARSHIPS_WAN="150,40,3"` for the bad-day path |
| P4, P5 | 5 (the arena roles) |
| P10 | 5 (the reconnect scenario already in SmokeTest: a freighter drops a turret, drops, rejoins, collects it) |
| P11 | 0/1 (a constant change); prove it by reading the code |
