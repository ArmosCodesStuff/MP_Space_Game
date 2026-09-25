# Warships multiplayer on WebRTC, v2: invite codes, no servers

Written 2026-09-24 against HEAD 8fddb84 plus the dirty tree, the S1 worktree (`scratchpad\net_tree`,
branch `net-pass`, uncommitted: read with `git -C net_tree diff`), the reviewed v1
(`scratchpad\webrtc\rtc_design.md`, "v1" below), the spike (`spike.md`, `spike\runs\`), and the
libjuice `agent.c` the v1 review saved (`webrtc\review\juice_agent.c.txt`, libjuice `3c40a35`).
Everything was read-only. Nothing was built or run; the only computation was a Python size model
(MODEL, §4.4).

**What this is.** v1 was approved in shape; the owner then ruled out every hosted rendezvous (no
ntfy.sh, no TURN, no hosted anything) and asked for **invite codes the players paste to each other**,
with public STUN from Google and Cloudflare as the only outside contact. v2 is v1 with that change
carried through everywhere it reaches. **Where v1 and v2 disagree, v2 wins.** Sections v2 does not
restate are v1's, unchanged: the plugin facts (v1 §2.1-2.6: S1-S10, F1-F10), the message cap (v1
§3.9), packaging (v1 §8, with the deltas in §8 here), and v1 §13's SOURCE list.

**Evidence levels** are v1's (READ, PLUGIN, SPIKE, API, SOURCE, RECALLED), plus MODEL (computed this
pass, §4.4). Every RECALLED fact names the slice and rung that proves it and the fallback if false.

**Critic pass (09-24, same day, read-only):** ten fixes, listed in §16. Each is already folded into
the section it touches; §16 says what was wrong.

**In one paragraph.** Every session is still one `WebRtcMultiplayerPeer` in server mode on the host
with one `WebRtcPeerConnection` per guest (v1 §3.1). What changes is how two machines swap the
session descriptions. **The host always makes the offer** (the spike's proven direction), on both
ways in: (1) **INVITE A FRIEND** packs the host's offer into a code of about 200 characters, which
the host sends on Discord; the friend pastes it into JOIN and gets a reply code of about 160
characters to send back; the host pastes it (or, with the owner's OK, just copies it: the game picks
it up). One invite per friend. (2) A friend on the same network or on Radmin VPN **types the host's
address** as today; the same invite and reply then travel over a small TCP listener on the host,
with no pasting, and that friend reconnects by itself after a drop. Both feed one session. The codes
are the plugin's SDP reduced to the fields ICE and DTLS need (ufrag, password, fingerprint,
candidates) plus the build fingerprint and a 31-bit invite id: 600 characters of the plugin's own
bundle become about 200 (§4.4). Public STUN comes from two rows, Google then Cloudflare, tried one
per connection because the library uses only one; if neither answers, the code carries this PC's own
addresses and still works on a LAN and over Radmin. There is no TURN row and no TURN code at all. The
new fact v2 must live with: **the friend's side starts ICE's 39.5 s give-up clock the moment it
pastes the invite** (SOURCE, §2.2), so the reply must reach the host's game within a window R0
measures (placeholder 25 s); the friend sees a countdown. S1 carries over almost whole, and its
channel-per-stream table now confines WebRTC's everything-is-reliable pauses to one stream at a time
(§9).

---

## 0 · The rulings this version answers

### 0.1 The network rulings

| Ruling | Where |
|---|---|
| The official Godot webrtc-native plugin (spike proved it) | unchanged from v1 (§2 there) |
| No third-party infrastructure except public STUN lookups from Google and Cloudflare; the game falls back gracefully when none answers | §5. The one other outside lookup the game makes today, `Net.PublicIpService` (`https://api.ipify.org`, the public-address reveal), is deleted in R2 (§7); S1's interim ENet release still calls it (§9) |
| No ntfy.sh, no hosted rendezvous, no TURN accounts | §1, §5.3 |
| Signalling = invite codes pasted to each other (e.g. over Discord) | §3, §4 |
| Friends need nothing extra | §0.3 |
| The owner gave a standing OK for new releases | v1 §16 Q5 is answered; §13 R5 publishes after the bar |

### 0.2 What the combat rulings put on the wire

None is a network decision. Each rides an existing row: a new **stream** is a row of S1's
`NetChannels` (a rung-3 check refuses an `UnreliableOrdered` RPC off that table), a new **request** is
an `AskHost`/`FromPlayer` RPC, and the build refusal keeps mismatched builds apart before any
connection (§3.3).

| Ruling | On the wire | Carried by |
|---|---|---|
| 6 chip slots on every hull (at most 3 combat, at most 3 utility); walls from the highest level reached | a longer loadout in `Hub.NetIdentity` | reliable channel 0; far below the 262,140 B cap (v1 §3.9) |
| Warp is capital-only (hold-to-warp, range UI, overshoot disable 2 s per 300 u, max 900); non-capital V = +50% top speed and strafe for 3 s, 15 s cooldown | the pilot's own motion and its `warping` flag | `Hub.NetShipState` on `NetChannels.Ships` |
| Freighter F = Time on Target: hitscan lines landing on the painted target at once | host-decided hits; the lines drawn like any flash | `Hub.NetFlash` on `NetChannels.Flashes` (or its own row if it needs fields a flash lacks) |
| Sentries launch to the cursor (600 u, 0.8 s, R recalls); prefer painted targets, else anything hostile | a guest request the host checks; targeting stays host-side | `AskHost`/`FromPlayer`; `Targeting.Sentry` keeps 8fddb84's filter (bosses and heavies included) and gains the painted-first preference, host-side, so nothing new on the wire |
| Grapnel pulls the destroyer toward the target; the chunk deals 1% of total hull + 10 | a real host-decided hit; the pull is the pilot's own motion | the hit path as today; `NetShipState` |
| Supercarrier: a second automated wing patrolling ~600 u (range moddable), engaging anything | more wing entries per ship | `NetHostState`'s wing arrays on `NetChannels.HostShips` |
| Taunt: pull + 33% damage reduction for 6 s | a host-decided status on the Warden; the pulled enemies' motion | `statusBits` in `NetHostState` on `NetChannels.HostShips` (READ: ship statuses ride there, not on a reliable path); the pulled enemies on their own rows (`Raiders`, `Boss`) |
| Heavies' twin laser (each barrel 1.25x the light mean: 2.58 DPS per heavy) | twice the flashes per heavy | `NetChannels.Flashes` |
| Respawn 24 s; the whole party down fails the mission | host timers and a mission event | reliable channel 0 |
| Raid squads (to 3H+9L at L39, refills after 30 s) | more raiders in `NetRaiders` (chunked per 24) | `NetChannels.Raiders`; about 32 B a raider against the cap |
| Items by hull category, 10 tiers | item ids only, never the catalogue | the fingerprint keeps two catalogues apart |
| Salvage levels on the slot, per pilot | travel with `NetIdentity` (trusted, as today) | reliable channel 0 |

### 0.3 "Friends need nothing extra"

- The plugin DLL arrives in the runtime part PLAY.bat already downloads, and imports only Windows
  system DLLs (v1 §2.2, SPIKE F5).
- The codes travel over whatever the players already use to talk: Discord, a text message. Nothing
  to install, no account, no website.
- The one new habit: the friend pastes one code and sends one code back. Same-house and Radmin
  friends type an address, as today.
- Caveats, unchanged from v1 §0.1: a Windows firewall prompt on first use (ENet builds raise the same
  one); antivirus treatment of an unsigned DLL is untested (install.ps1's presence check names a
  quarantined file, v1 §8.5); a network that blocks UDP outright cannot play (libjuice is UDP only).
- **New caveat, from dropping TURN:** two strict NATs (both mapping per destination, or one doing so
  against a port-filtering one; a phone hotspot is the common case) cannot meet directly, and with no
  relay nothing in the game can join them. Radmin VPN is the fallback (§11 step 11), and it is the one
  case where friends install something (both PCs, as with Radmin today). ENet had the same limit only
  where the host's router opened no port: where UPnP, NAT-PMP or PCP opened one (Router.cs), ENet
  reached a friend behind any router, and deleting that code (v1 §7) gives it up. So it is a partial
  regression, and what "no TURN" costs (§15 Q3).

---

## 1 · What v2 changes from v1

| v1 | v2 | Why |
|---|---|---|
| Room codes through ntfy.sh (`code` row, boards, topic/key/AES seal, `room`/`join`/`accept`/`refuse`/`closed` records, 429 texts, 2 h republish, `Rendezvous.Service`) | **gone, never built** | ruling |
| `paste` row: built last, first to drop; the guest made the first blob | **the internet path**, built in R1-R3; the host makes the first code (the invite), the friend replies | ruling |
| The guest made the offer (a direction the spike never ran; an R0 NEW check with a written fallback) | **the host offers on every row**, as in every spike run | the invite is the host's offer; the typed row follows it, so there is one flow; one R0 unknown gone |
| Typed-address row: the guest's `join` record, the host's `accept` | **knock → invite → reply** over the same TCP connection, gathered with no STUN | one flow; LAN and overlays need only host candidates |
| Records as JSON, deflated (and AES-sealed for ntfy) | **packed binary**; Crockford base32 text with a prefix and a 4-byte check; no deflate, no encryption | 600 characters become about 200 (§4.4); there is no second channel to carry a key |
| One STUN row (Google); a second row rejected as a coin toss | **two STUN rows, walked in order, one per connection** | ruling; the library still uses only one per connection (v1 §2.3) |
| A TURN row for the owner to fill (ExpressTURN), `route=relay/direct`, `Link.Only` | **none**: no TURN row, no TURN kind, no candidate filter in the game | ruling |
| Harness: a hand-written TURN stand-in in `wan.py`; relay-only roles for `-Wan` | **the courier and a UDP pair proxy** in the box; `-Wan` puts lag and loss on invite-carried sessions | no TURN anywhere; the harness carries codes the way Discord does, so it may route them like a network would (§10.2) |
| Every row retried by itself (2/8/14 s) | only rows that can re-signal alone (**typed addresses**) retry; an invite friend gets a fresh invite, made for the host automatically | no server to re-signal through |
| — | **the reply window** (§3.4): measured in R0, a countdown for the friend, clipboard pickup for the host (§15 Q1) | the friend's ICE clock starts at paste (SOURCE, §2.2) |
| v1 §2.6 S1: the ten ordered streams share one reliable SCTP stream per peer | each stream has its own data channel (S1's `NetChannels`), so a loss stalls one stream | S1 landed a channel per stream (§9) |
| The backlog guard fallback needed per-peer sends: "a new table" | a condition in `Net.ToHeard` and `Hub.RpcToSector` | S1 made every host stream per-peer already (§3.8) |
| R5 (paste) and R6 (packaging) | paste folded into R1-R3; packaging is R5 | – |
| v1 §16 Q1 (ntfy), Q2 (paste), Q4 (TURN account), Q5 (releases) | answered by the rulings | – |

---

## 2 · The plugin: what v1 established, and what pasting adds

### 2.1 Kept from v1, restated only where v2 leans on it

- **Every channel is reliable** (v1 §2.6 S1, SOURCE): Godot writes `maxPacketLifetime`, the plugin
  reads `maxPacketLifeTime`. An `UnreliableOrdered` RPC travels reliable-ordered; a lost datagram
  holds back later messages **on its own data channel** until SCTP resends it. R0 pins the fact
  (`GetMaxPacketLifeTime() == -1`).
- **Seal a bundle one poll after `Complete`** (v1 §2.6 S2): `Link.Sealed(conn)`. Unchanged; the STUN
  walk (§5.2) uses it on every attempt.
- **Every hang-up through `Link.Hang(mp, id)`** (v1 §2.6 S3): `HasPeer` first, because `remove_peer`
  and `disconnect_peer` print an engine `ERROR:` for a missing id and run.ps1 fails a role on one.
- Signals arrive inside `Poll()` on the main thread (S4); server mode is `Connected` at once (S5);
  channels are pre-negotiated with fixed ids (S6); a pending peer never receives a broadcast (S7);
  `SetRemoteDescription("offer")` makes the answer itself (S8); library `Error` lines become engine
  `ERROR:` lines (S9); remote loopback candidates are accepted (S10).
- The spike's findings F1-F10 and where each lands: v1 §2.5, unchanged.

### 2.2 New for pasting: the ICE timers across a human hop (SOURCE: `agent.c`, libjuice `3c40a35`)

A pasted exchange puts seconds to minutes between the offer and the answer. Four facts decide what
survives that:

| # | Fact | Where in `agent.c` | Consequence |
|---|---|---|---|
| P1 | **The give-up clock starts when the remote description is set AND local gathering is done**, not before. It fails the agent only once it has expired and no check is still pending ("Connectivity timer expired"). `ICE_PAC_TIMEOUT` is 39.5 s (v1 §2.3, `agent.h`). A check's retransmissions double from a minimum and end with one long wait (the schedule is SOURCE; its constants were not re-read, and nothing rests on them) | `agent_update_pac_timer`; `agent_bookkeeping` (`pending_count == 0 && agent->pac_timestamp`) | **The host's invite can wait indefinitely**: the host sets no remote description until the reply comes. **The friend's reply cannot**: the friend's agent sets the host's offer at paste, finishes gathering within `GatherMs`, and gives up about 39.5 s later unless the host's checks have arrived. That is the reply window (§3.4) |
| P2 | **The offerer answers checks before it has the answer.** A binding request whose username starts with the local ufrag and whose integrity matches the local password is answered at once, "prior to receiving the candidates from its peer" (RFC 8445 §7.3), a peer-reflexive remote candidate and pair are created, and those pairs are unfrozen the moment the remote description arrives | `agent_verify_stun_binding` (the RFC comment), `agent_dispatch_stun` → `agent_add_remote_reflexive_candidate`, `agent_set_remote_description` ("Unfreezing existing candidate pairs") | On a path where the friend's early checks reach the host (a LAN, Radmin, or a host NAT that filters by nothing), the friend's ICE succeeds before the host pastes, and the connection completes within a round trip or two of the paste. On the usual home NAT (filtering by address and port) the early checks are dropped at the host's router, and the window is P1's |
| P3 | **An unanswered offer keeps its internet address alive.** After a STUN server answers, its entry keeps sending binding requests every `STUN_KEEPALIVE_PERIOD` until a pair is nominated | the success branch of `agent_process_stun_binding` ("We want to send keepalives now"), `agent_arm_keepalive` (`AGENT_STUN_ENTRY_TYPE_SERVER`) | The host's router keeps the invite's server-reflexive port mapped while the invite waits. The period's value (15 s) is RECALLED from `agent.h`; nothing rests on the number, only on the keepalive, which is SOURCE |
| P4 | **The answerer is the DTLS client.** The answer the plugin made in the spike says `a=setup:active` | SPIKE `runs\stun\answer_2.json` | When P2 lets the friend's ICE complete early, the friend's DTLS client starts at once, and libdatachannel's DTLS handshake timeout (value RECALLED, unknown) may bound the window before P1 does. R0 measures the window on exactly that path (§3.4) |

### 2.3 What the spike's real bundles show about the SDP (SPIKE `runs\stun\offer_2.json`, `answer_2.json`)

- The plugin's SDP is 17 fixed lines. Only five values vary per connection: the `o=` session id
  (meaningless to the far end), `a=fingerprint:sha-256` (32 bytes), `a=setup` (`actpass` in an
  offer, `active` in the answer), `a=ice-ufrag` (4 characters) and `a=ice-pwd` (22 characters), both
  from the base64 alphabet. `a=mid:0`, `a=sctp-port:5000` and `a=max-message-size:262144` are
  constants of this plugin version.
- Candidates arrive separately, one line each: `candidate:1 1 UDP 2114977791 <lan-ip> 54617 typ host`
  and `candidate:2 1 UDP 1678769919 <public-ip> 54617 typ srflx raddr 0.0.0.0 rport 0`. libjuice
  already hides the related address (`raddr 0.0.0.0 rport 0`).
- **This PC's NAT kept the port** (54617 inside and out; 54581 on the second socket). Port
  preservation is the usual sign of endpoint-independent mapping, the kind hole punching needs. It is
  a hint about this network, not proof about the owner's double NAT (v1 §2.7), which the field test
  settles.

---

## 3 · The session

### 3.1 Shape

- **Host:** `CreateServer(Link.Channels())`, synchronous and `Connected` at once (v1 §3.1, SOURCE
  S5). Beside it: the **listener** (TCP, §3.3 B) and the **pending table** `Net.Pending`: one entry
  per invite not yet connected, whichever way it was asked for.
- **Guest:** `CreateClient(id, Link.Channels())` with **the id the invite carries**, then one
  `WebRtcPeerConnection` added as peer 1.
- **The host draws every id** (2 … 2³¹−1; not in `Players`, not pending, not a held place's `OldPeer`,
  v1 §3.1). The invite carries it, so the guest's `CreateClient(id)` and the host's `AddPeer(conn, id)`
  agree by construction. v1's "guest draws the id, host refuses a clash, guest retries" is gone.
- **Server relay, channels, auth, S1's welcome:** as v1 §3.1-3.2, with the channel list now taken
  from S1's `NetChannels` rows (§3.2).

### 3.2 Channels: S1's `NetChannels`, one data channel per row

- S1 gives every ordered stream a transfer channel of its own (`NetChannels`, net_tree Net.cs): 1
  `Cosmetic` (Reliable), 2 `Ships`, 3 `HostShips`, 4 `Shields`, 5 `Raiders`, 6 `Hulls`, 7 `Flashes`,
  8 `Dummies`, 9 `Base`, 10 `Boss`, 11 `BossSounds` (all UnreliableOrdered). v2 adds one row:
  **12 `Beat`, Reliable** (like `Cosmetic`), for the beat (§3.6). Not UnreliableOrdered: S1's rung-3
  check (READ, net_tree SmokeTest.cs.txt) fails every UnreliableOrdered RPC that is not the Hub's or
  that shares its row with another RPC, and the beat is two RPCs (`NetBeat`, `NetBeatBack`) on `Net`,
  the autoload that outlives every world (the Hub belongs to each world). The check leaves Reliable
  rows alone, as it does `Cosmetic`'s two RPCs, and under the quirk (§2.1) every row is reliable
  anyway, so the wire is the same. Not channel 0 either (v1's place): there it would queue behind
  every reliable message.
- `Link.Channels()` builds `channels_config` by reflection over **every** `RpcAttribute` in the
  game's assembly, one mode per channel above 0, gaps filled with `Reliable` (v1 §3.2). With the
  `Beat` row a release gets `[Reliable, UnreliableOrdered × 10, Reliable]`: with channel 0's three
  built-in channels, **15 negotiated data channels per peer** (ids 1-3, then 4-15, SOURCE S6); S1's 11
  rows alone give 14. S1's check ("one row per stream, no sharing") already enforces what v1's own
  two-mode check was for; v2 reuses it, with one line changed (below).
- **The harness's stream needs a channel too.** S1's rate check sends its own UnreliableOrdered RPC,
  `_Test.NetStream`, on `StreamChannel = 200`, clear of the table on purpose (READ). WebRTC opens only
  the channels in `channels_config`, and the fingerprint leaves the harness's `_` types out, so a
  config built from the fingerprint's view has no channel 200: nothing sent on it would arrive, and
  the rate check v2 keeps (§9) could not pass. Hence "every `RpcAttribute`", the harness's included
  (both ends of a test run are one build; the fingerprint's own scope is unchanged), and **R0 sets
  `StreamChannel = NetChannels.BossSounds + 1`** (12; R2's edit that adds `Beat` makes it
  `NetChannels.Beat + 1`, and S1's `!rows.Contains(StreamChannel)` catches a missed change at rung 3).
  A test build therefore negotiates one data channel more than a release: **15 in R0, 16 from R2**.
- **S1's check reads ENet's channel count** (`ENetMultiplayerPeer … GetMaxChannels()`, a row r
  needing r + 1 below it). R2 reads `Link.Channels().Length` instead (a row r needs r at most that),
  in the edit that deletes ENet: GodotStub loses `ENetMultiplayerPeer`, so the old line would not
  compile.
- **Every data channel is its own SCTP stream, and SCTP orders each stream alone.** So under the
  reliable quirk a lost raider update holds back later raider updates only: ships, boss, base and the
  beat carry on. In v1 all ten streams shared channel 0's "ordered" channel and one loss stalled them
  all; S1's table removed that.
- Within one row the quirk still bites: `NetShipState` carries every pilot, `NetRaiders` a chunk per
  24 raiders. A loss for one delays the others on that row by one resend (about a round trip, 200 ms
  minimum RTO; v1 §2.6). `-Wan` prints the longest gap per row (§10.4 R4).
- R0 proves every row's data channel opens on the in-process pair (15: S1's 11 rows, the harness's
  stream, the 3 built-in) and that an RPC on the highest row arrives; R2's first rung 3 re-proves it
  at 16 with `Beat`.

### 3.3 A join, step by step

**A · By invite (paste row)**

*Host*
1. **INVITE A FRIEND** (hosting only). Refused while `Players.Count + Pending.Count >= MaxPlayers`
   (8, the host included; ENet's `CreateServer(port, 8)` admits 8 guests today, so the cap is one
   pilot lower, as v1 had it): the full text (§6.1).
2. Draw the id (§3.1). `new WebRtcPeerConnection()`, `Initialize(Link.Config(row))`,
   `mp.AddPeer(conn, id)` (before any description, v1 §2.3), `CreateOffer()`. Keep the
   `SessionDescriptionCreated` SDP (and call `SetLocalDescription`, a no-op here, S8) and every
   `IceCandidateCreated` line.
3. `Link.Sealed(conn)`. If the bundle has no server-reflexive candidate and another STUN row remains,
   the STUN walk rebuilds the connection with the next row (§5.2).
4. Pack an **invite** (§4.1): the build fingerprint and build id, the id, the host's name, the SDP's
   five variable fields, the fitted candidates. Show it and put it on the clipboard.
5. `Pending[id] = {conn, row Paste, made, state Waiting}`. It lives `Link.InviteLifeS` (900 s)
   unanswered, then is hung up (`Link.Hang`) and its panel line says so. P3 keeps its internet
   address valid meanwhile.
6. **A reply comes in** (the reply box, or the clipboard, §15 Q1). Unpack; its id must be a pending
   entry in state Waiting, else a sentence (§6.1) and nothing else happens. Then
   `SetRemoteDescription("answer", Sdp.Build(reply))`, `AddIceCandidate` for each candidate, state
   Linking, deadline `Link.LinkMs` (12,000 ms). "{name} is joining…".
7. `peer_connected(id)` → `OnPeer(joined)` as today: S1's welcome, then the beat starts.
8. The deadline passes first → `Link.Hang(mp, id)`, "{name} could not get through ({reason})", and
   **a fresh invite for {name}** is made at once (one COPY away), because this one is spent: a
   connection takes one answer, and a second is "ICE restart is not supported" (v1 §2.3).

*Guest*
1. The JOIN box gets text (typed, pasted, or filled from the clipboard). `Rendezvous.Paths` asks each
   row whether it `Claims` the text; the paste row claims anything holding `WSI` followed by a code
   whose check matches (§4.3). A reply code (`WSR`) gets its own sentence ("that one goes to the
   host").
2. **Build check before any network step:** `invite.proto != (PretendAt.Code ? PretendProtocol :
   Protocol)` → the build text naming both builds (§6.2). No WebRTC object is made.
3. `mp.CreateClient(invite.id, Link.Channels())`, `Multiplayer.MultiplayerPeer = mp` (status
   `Connecting`, as `ConnectTo` today). `new WebRtcPeerConnection()`, `Initialize(Link.Config(row))`,
   `mp.AddPeer(conn, 1)`, `SetRemoteDescription("offer", Sdp.Build(invite))` (the plugin makes the
   answer, S8), `AddIceCandidate` for each invite candidate.
4. `Link.Sealed(conn)`; the same STUN walk, rebuilding the guest's own connection if its bundle has
   no server-reflexive candidate.
5. Pack the **reply** (§4.1), show it, put it on the clipboard: "Reply copied. Send it to {hostName}
   now…" with a countdown of `Link.ReplyWindowS` (§3.4).
6. Connected → SceneMultiplayer's auth → `ConnectedToServer` → S1's `NetWelcome` → `Admitted`,
   exactly as today.
7. The countdown runs out first (or `GetConnectionState()` reads `Failed`/`Closed`) → the attempt is
   closed and "{hostName} did not connect within {W} s of your reply" with **MAKE A FRESH REPLY**,
   which repeats steps 3-5 **from the same invite**. That works as long as the host has not pasted a
   reply for it; if the host pasted the stale one, the host's panel already holds a fresh invite
   (host step 8).

**B · By typed address (the listener; LAN, Radmin, Tailscale)**

*Guest*
1. The address row claims anything `ParseAddress` accepts (unchanged, Net.cs) that the paste row did
   not claim. The name is resolved with S1's `DialAddress` (IPv4, else IPv6, never link-local).
2. TCP connect to the host's listener; send a **knock** (§4.1: build fingerprint, build id, the
   player's name, and `guest`, a 32-bit value drawn once per game process).
3. Read the host's answer on the same connection: an **invite** (then guest steps 2-4 of A, gathering
   **with no STUN row**, §5.2) or a **refuse** (`full`, `build`, `rate`; §6.2). Send the **reply** on
   the same connection and close it.
4. Connected → as A6. The whole flow keeps `JoinTimeoutMs` (12,000) for a first join and
   `RetryTimeoutMs` (5,000) for a retry, as today.

*Host*
1. The listener: TCP on 27015, then 27016 … 27024, then an OS-chosen port; a dual-stack socket
   (v1 §4.3). Per connection: 8 KB read cap, 3 s read timeout, 20 knocks a minute. The listener thread
   only moves bytes; it hands each knock to the main thread (`Callable.CallDeferred`).
2. Checks, in order: a session; the build (refuse `build`, and the host line naming who knocked);
   the rate; **one pending entry per `guest`** (a newer knock supersedes the older pending entry,
   which is hung up, v1 §3.3); capacity (refuse `full`).
3. Host steps 2-5 of A with **no STUN row**, `row Address`; send the invite on the connection; read
   the reply (3 s); then host steps 6-8 of A. A failed address join makes no fresh invite: its guest
   retries by itself.

**One session serves both.** Both paths end in the same `Pending` table, the same `TakeReply`, the same
`OnPeer(joined)`. The host's panel shows INVITE A FRIEND and the typed addresses at once (§6.1). A
session can hold an invite friend in another country and a Radmin friend at the same time.

### 3.4 The reply window

- **What sets it.** The friend's agent starts P1's clock at paste; the host's checks must reach it
  before the clock runs out. So the time that matters is from the friend making the reply to the
  host's game applying it: the friend copies it into Discord, the host sees it, copies it, and the
  game takes it.
  - On the usual home NAT (address- and port-filtering at the host), the friend's early checks die at
    the host's router and the bound is P1's **39.5 s** (SOURCE).
  - Where they get through (LAN, Radmin, a permissive host NAT), P2 completes the friend's ICE early,
    P4 starts its DTLS client, and libdatachannel's DTLS handshake timeout may bound it sooner
    (RECALLED; value unknown).
- **R0 measures it once**, on the path that can be shorter: an in-process pair over the LAN host
  candidate, where the host answers early checks (P2). Seven pairs start together; each host applies
  its reply after 5, 10, 15, 25, 35, 45 or 60 s; the run records which connected. About 65 s, one
  time, recorded in DESIGN.md. (The short delays are there so the measurement always yields a
  number: a DTLS bound under 15 s would otherwise read as "nothing connected".)
- **`Link.ReplyWindowS`** (const, so it is in the fingerprint) = the longest delay up to which every
  pair connected, minus 10 s, and never above 30. **Placeholder until R0: 25 s.** The same edit that
  sets it replaces the measurement with the permanent check (§10.4 R0). **If it comes out under 15 s,
  R0's stop rule applies** and the owner is told before R1: a friend whose early checks reach the
  host (a permissive host router) would have too little time to carry the reply by hand, and the
  typed address cures only the LAN and Radmin cases.
- **What the players get:**
  - the friend's reply is on the clipboard the moment it is made, with a countdown;
  - **clipboard pickup (§15 Q1, default yes):** while an invite is pending, the host's game reads the
    clipboard twice a second and takes a reply for that invite from it, so the host's part is "copy
    the friend's message": in combat, in a menu, alt-tabbed. The reply box in the panel is the
    manual path;
  - on expiry, MAKE A FRESH REPLY (same invite), and a host who pasted a stale reply gets a fresh
    invite for that friend (§3.3 A, host step 8).
- **Why the friend does not offer instead.** Whichever side answers starts the clock at its paste,
  and the other side must paste back inside it. With the host offering (the ruling's order), the
  critical paste is the host's, which clipboard pickup reduces to a copy. Switching sides would move
  the critical paste to the friend and add nothing else.

### 3.5 Budgets

- **Typed address**, measured parts (SPIKE Q4): gathering 12-14 ms each side with no STUN; ICE, DTLS
  and SCTP 17-45 ms; plus a TCP round trip. Well under a second on a LAN, inside the unchanged 12 s
  first-join and 5 s retry budgets.
- **Invite**: making it takes one gather, 66-110 ms with an answering STUN server (SPIKE), at most
  `GatherMs` per unanswered row (2,000 ms), so 4 s at worst once per session (§5.2). The rest is human:
  the friend's countdown, then the host's 12 s from paste to connected.

### 3.6 The beat and the watchdog

v1 §3.6 unchanged except where noted: `NetBeat(long sentMs)` and `NetBeatBack(long sentMs)` on
`Net`, every `Link.BeatMs` (500), on **`NetChannels.Beat`** (row 12, Reliable, its own SCTP stream,
§3.2); `Net.RoundTrip` is the minimum of the last 8 echoes; silence is the frame-capped
(0.25 s a frame) sum since the last beat or echo; above `Link.QuietMs` (8,000, S1's literal) the peer
is dropped (`Link.Hang` on the host; `OnHostGone` on a guest). Why the game needs it at all: a
hard-killed peer is otherwise noticed after 25-26 s (SPIKE F3).

### 3.7 Goodbye (S1's shape, WebRTC's body)

- **S1's list survives**: every goodbye keeps its own time (`_lettingGo`), and `LetGoMs` stays S1's
  2,000. The reason carries over: a goodbye is `NetBye` then the other end's hang-up, and one lost
  packet costs a resend of at least a round trip (SCTP's minimum RTO is 200 ms, v1 §2.6).
- **The body changes**: an entry is a `WebRtcMultiplayerPeer`, polled each frame until `GetPeers()` is
  empty or its 2 s pass, then `Close()`. A host hangs up its pending entries first (`Link.Hang`),
  because `GetPeers()` lists them and nobody else will (v1 §3.5).
- **S1's `server` flag and `Host()`'s early close of a host's let-go are deleted**: they existed
  because ENet's host socket held the port about to be bound again. WebRTC's sockets are ephemeral
  and the listener closes at `Shutdown`, not at the end of a goodbye. S1's check (a guest pressing
  HOST keeps its goodbye) stays and passes by construction.
- The hearer hangs up; `SkipGoodbye` closes at once (v1 §3.5).

### 3.8 Drop, refusal, full, the backlog guard

- The harness's `Drop(peer)` is `Link.Hang` deferred to the end of the frame (v1 §3.7).
- The in-band build refusal (`OnAuth`) stays as the last guard, with `PeerDisconnectLater` replaced by
  `Link.Hang`. The harness knob is `Net.PretendAt`, a flags value `Code | Auth` (v1's `Record` is now
  `Code`: the invite's check on the friend's side and the knock's on the host's).
- **The backlog guard (the written fallback for the quirk, v1 §3.2) is now a condition, not a table.**
  S1 made every host stream per-peer: `Net.ToHeard` loops `RpcId` over the guests that answered the
  welcome, and `Hub.RpcToSector` loops `RpcId` over the peers in a world (READ net_tree Hub.cs:562).
  If the field or `-Wan` shows a backlog that never drains, the guard is: skip this send to this peer
  when `Link.Backlog(mp, peer, channel)` is over a threshold, in those two loops, with the channel
  looked up from the method name by the same reflection that builds `Link.Channels()`. Built only on
  evidence, and the owner is told first.

### 3.9 Reconnect and the 90 s hold, by row

- A row says whether it can re-signal by itself: `IRendezvousPath.Auto`.
  - **Address (Auto):** retries at 2, 8 and 14 s (5 s each) and RECONNECT at 19 s, unchanged. Each
    retry is a new knock, so a new invite and a new connection.
  - **Paste (not Auto):** no retries, no RECONNECT button. The guest reads "Lost the connection to
    {hostName}. Ask them for a new invite code: your place is held 90 s. Your own world keeps
    running." The host's panel makes a fresh invite **for that pilot** at once ("{name} dropped. Send
    them this invite to come back: COPY").
- `Session.HoldFor` stays 90 s (§15 Q2). The returning pilot reclaims its place by S2's P10b rejoin
  token, carried after connection, not in the code. S2 therefore lands before R2, as in v1.

---

## 4 · The codes

### 4.1 Records

One codec, `Rendezvous.Pack/Unpack`, for all four kinds. The paste row carries invite and reply as
text (§4.3); the listener carries all four as length-prefixed bytes.

| Kind | By | Carried by | Fields (after the kind byte) |
|---|---|---|---|
| **invite** | host | paste (text `WSI…`) or listener | flags, proto (4), id (4), build (1+n ≤ 24), host name (1+n ≤ 16), ICE ufrag and password, fingerprint (32), candidates |
| **reply** | guest | paste (text `WSR…`) or listener | flags, id (4), guest name (1+n ≤ 16), ICE ufrag and password, fingerprint (32), candidates |
| **knock** | guest | listener only | proto (4), guest (4), build (1+n), name (1+n) |
| **refuse** | host | listener only | why (1: `full`, `build`, `rate`, `closed`), proto (4), build (1+n) |

Every record ends in a **check**: the first 4 bytes of SHA-256 over everything before it (BCL
`SHA256.HashData`).

**Byte layout, format version 1** (all integers big-endian):

- byte 0: kind (high 4 bits: 1 invite, 2 reply, 3 knock, 4 refuse) and format version (low 4 bits).
- flags (invite, reply), 1 byte: bits 0-1 the SDP's `a=setup` (0 actpass, 1 active, 2 passive);
  bit 2 **no STUN answered** (the code carries this PC's own addresses only, so the far side can say
  so).
- ICE credentials: a length byte, then the characters packed 6 bits each over libjuice's base64
  alphabet (`A-Z a-z 0-9 + /`). 4 + 22 characters (SPIKE) take 2 + 3 + 17 = 22 bytes. The codec
  refuses a character outside the alphabet.
- fingerprint: the 32 bytes of `a=fingerprint:sha-256`. The codec refuses another algorithm.
- candidates: a count byte, then per candidate: type and family (1 byte: bits 0-1 host / srflx /
  prflx / relay, bit 2 IPv6), foundation (1 byte; the codec refuses a non-numeric one or one over
  255), priority (4), port (2), address (4 or 16). Component 1 and UDP are implied: libjuice
  gathers nothing else. The related address is always written back as `raddr 0.0.0.0 rport 0`, as
  libjuice itself writes it for IPv4 (SPIKE); what it writes for an IPv6 candidate is unread (this
  network has none), so the IPv6 tail follows whatever R1 or the field report shows. ICE ignores the
  related address, so only the byte-for-byte check could notice.
- names: the shared name rule (v1 §3.3: empty → the default name; the rule `Hub.NetIdentity` applies,
  moved into one function), then clipped to 16 bytes at a character boundary.

**The session nonce is the invite's id.** It is drawn per invite, unguessable to anyone without the
invite (31 random bits), dies with the session, and doubles as the friend's peer id. A reply is
matched to its invite by it; a reply for an id the host no longer has pending gets a sentence (used,
cancelled, expired, or from an earlier HOST). A separate 8-byte session value would add 13 characters
and no function, so there is none.

### 4.2 The SDP template (`Rendezvous.Sdp`)

- **Strip (sending):** the local SDP is read line by line against the plugin's 17-line template
  (§2.3). Each line must match its template line, with placeholders only for `o=`'s session id, the
  fingerprint, the setup value, the ufrag and the password. **A line the template does not know is
  refused**, and the refusal names it. A plugin upgrade that adds a line the far end needs therefore
  fails R1's rung-3 check on the first run, never silently in the field.
- **Build (receiving):** the template with the carried values, a fresh random `o=` id, and `a=setup`
  from the flags. Candidate lines are rebuilt exactly as libjuice writes them:
  `candidate:{foundation} 1 UDP {priority} {address} {port} typ {type}` plus the `raddr … rport 0`
  tail for non-host types.
- **Proved byte for byte** (R1): the rebuilt SDP equals the original with only the `o=` id changed,
  and every rebuilt candidate line equals the original. The template and the version nibble are
  constants, so they enter `Net.Protocol`: two builds whose codecs differ refuse each other on the
  build check before any network step.

### 4.3 The text form (paste row)

- **`WSI` or `WSR`, then Crockford base32** of the packed bytes (`0-9 A-Z` without `I L O U`), no
  padding.
- **Why base32 and not base64 or base58:**
  - no `_` or `*` (Discord turns `_x_` into italics, and a copy of the rendered text loses them), no
    `-` or `+` or `/` (a double-click selects only up to them);
  - case-insensitive, and `I`/`L` read as 1 and `O` as 0, so a hand-retyped or re-cased code still
    decodes;
  - one word: a double-click selects the whole code.
  - base58 would be 15% shorter but case-sensitive; base64url 17% shorter with both problems above.
- **Reading is forgiving:** the decoder looks for `WSI`/`WSR` anywhere in the pasted text (a whole
  copied Discord message, name and time included, still works) and tries each place it occurs until
  one decodes. From there it takes base32 characters, skipping spaces, line breaks and hyphens,
  **only as many as the record needs**: every field is fixed-length or length-prefixed, so the record
  says its own length as it is read. The check then decides. So words after the code ("thanks!", or
  a sentence around it) are ignored, and a `wsr` inside a name before the code is
  passed over; with "take characters until they stop", either would read as damage. A cut-off or
  mangled code is caught by the check and gets "That code is damaged or cut off: copy the whole
  message again."
- **Budget: 400 characters per code** (`Rendezvous.PasteBudget`). A non-Nitro Discord message holds
  2,000 (common knowledge, not re-read), so a code plus a sentence is one message.
- **The fit rule** (`Rendezvous.Fit(candidates, budget)`): first, a global IPv6 address on a /64 the
  code already carries is dropped, fit or not (a temporary and a stable address on one /64 are one
  path, so the second adds length and no route; §4.4 C and D and R1's check rely on this). Then
  candidates are ranked, and the lowest are dropped until the code fits: server-reflexive first; then
  host candidates on an overlay (`Adapters.Overlays()`: Radmin, Tailscale, ZeroTier, Hamachi); then
  the LAN address (`Adapters.Lan()`); then global IPv6; then the rest. The report says how many were
  dropped. On the listener the budget is 8,192 B and only the /64 rule drops anything.

### 4.4 Sizes (MODEL, this pass; release build id of 18 characters, an 8-character name)

The model packs the layout above. Its "plugin's own bundle" column reproduces the spike's measured
real bundle (672 characters of JSON, 600-604 deflated and base64: `runs\stun`; the one-candidate
`runs\pair` bundles are 580-581 and 544-548), which calibrates it.

| Shape | Plugin's bundle, JSON | Same, deflated + base64 | Packed invite / reply | **Code: invite / reply** |
|---|---|---|---|---|
| A · one LAN address + srflx (this PC, SPIKE) | 672 | 600 / 604 | 121 B / 98 B | **197 / 160** |
| B · + a Radmin address | 739 | 627 / 628 | 133 B / 110 B | **216 / 179** |
| C · + two IPv6 on one /64 (fit keeps one) | 854 | 679 / 676 | 145 B / 122 B | **235 / 199** |
| D · 4 IPv4 adapters + 4 IPv6 on two /64 (fit keeps two IPv6) | 1,232 | 787 / 792 | 205 B / 182 B | **331 / 295** |
| E · 10 IPv4 + 9 IPv6 + srflx (20 candidates) | 2,024 | 986 | 445 B / 422 B before fit | **fit caps it at 400** |

- A source run's build id is `dev`: an invite is 15 B (24 characters) shorter.
- **Deflate does not pay on the packed form**: +5 B on A-C, −5 B on D, −83 B only on E, which the fit
  rule already handles. It paid on JSON because 90% of JSON is text the far end already knows;
  packing removes that text instead of compressing it. So there is no deflate step and no second
  format.
- The floor is the fingerprint (32 B), the ICE credentials (22 B) and the headers: 74 B (122
  characters) for a reply before any candidate; the usual two candidates make it 98 B, 160
  characters. Nothing further can come out without weakening DTLS.
- The model was re-run by the critic from the layout above: every packed size and code length in the
  table reproduces exactly (invite = 97 B + 12 per IPv4 and 24 per IPv6 candidate, reply = 74 B +
  the same; code = 3 + ⌈8n/5⌉; 400 characters hold 248 B). The JSON column is illustrative (it moves
  with the addresses' lengths; A is the spike's measured 672, and the deflated A figures are the
  spike's own: offer 600, answer 604).

### 4.5 Security

- **Not encrypted, on purpose.** A code could only be encrypted with a key sent by some other channel,
  and there is none. The code is the secret: whoever holds an invite can answer it, as whoever held an
  IP could join an ENet host before. The panel says "send it privately" (§6.1).
- **What a code shows:** the sender's LAN, overlay and public addresses, IPv6 included. The same as an
  address typed into JOIN today, and less than a public post.
- **What protects the game:** each code carries its sender's DTLS fingerprint, so the connection is to
  exactly the two machines that made the codes, and game traffic is DTLS-encrypted (ENet's is
  plaintext). An invite takes one reply; a second is refused with a sentence. The host only ever
  applies a reply its player pasted or copied. `OnAuth`'s build check and S2's rejoin token stay.
- **The listener** (typed row) takes knocks from anyone who can reach the port, which on a LAN or an
  overlay is the same exposure as ENet's UDP 27015 today; its limits are §3.3 B.

---

## 5 · STUN rows, and no TURN

### 5.1 The table

`Link.Servers` is a mutable static array of rows `{Url}`: never readonly, so it stays out of
`Net.Protocol` and the harness can swap it.

| Row | Url | Why |
|---|---|---|
| 1 | `stun:stun.l.google.com:19302` | the one server the spike tried from here: it answered (gathering 66 ms and 110 ms, SPIKE `runs\stun`) |
| 2 | `stun:stun.cloudflare.com:3478` | the ruling's second provider; the standard STUN port, which strict networks block less often than 19302; not yet exercised from here |

- Rows are stateless: nothing to sign up for, nothing that expires. A new server is a row.
- **No TURN row, no `Kind` column, no candidate filter.** v1's `Link.Only`, `route=` and the TURN row
  are not built.

### 5.2 The walk (`Link.Gather`)

- **One STUN row per connection.** libdatachannel shuffles its server list and uses the first STUN
  entry (v1 §2.3, SOURCE), so two rows in one configuration would be a coin toss, not a fallback.
  `Link.Config(row)` therefore holds exactly one STUN row.
- **The walk:** start at `Link.StunFirst` (0 at start). Gather; seal by `Link.Sealed` (Complete + one
  poll, or `GatherMs`). If the bundle holds a server-reflexive candidate, stop, and `StunFirst` becomes
  this row for the rest of the process. If not and a row remains, hang the connection up
  (`Link.Hang` on the host; the guest's own peer 1 likewise) and rebuild it on the next row with the
  **same id** (R1 proves the id is free again at once; if not, the rebuilt connection gets a new
  id). If no row answers, the code carries host
  candidates only, its flag says so, and `StunFirst` moves past the end, so later gathers skip STUN
  until the next HOST or JOIN.
- **Cost:** about 0.1 s when row 1 answers; at most 2,000 ms per unanswered row, so 4 s once per
  session per machine when none answers.
- **Only the paste row walks.** `IRendezvousPath.Stun` is false for the address row, which gathers
  with no STUN row at all: a typed address means a LAN or an overlay, where host candidates are the
  path, and a knock then costs 12-14 ms of gathering (SPIKE) instead of up to 4 s.
- **The graceful fallback, concretely:** with no STUN answer an invite still carries the LAN address,
  the Radmin address and any global IPv6, so it connects a friend on the same network, on the same
  Radmin network, or on IPv6 with both firewalls opened by ICE's simultaneous sends. The far side's
  text names the case (§6.2).
- **Privacy line (NOTES and the report):** the game asks one of these servers for this PC's internet
  address when it makes an invite or a reply, and at no other time; single player never does.

### 5.3 What no TURN costs, stated once

- Pairs that ICE cannot join directly stay unjoined: both sides mapping per destination, or one doing
  so facing a port-filtering router. With a relay they would connect; without one, Radmin VPN is the
  answer (§11 step 11), and the report tells which case it was (both sides' candidate lists, v1 §6.3).
- The owner's double NAT (v1 §2.7): fine if both routers map endpoint-independently (this PC's NAT
  preserved ports, §2.3); if the modem maps per destination, the owner cannot be joined directly by a
  port-filtering friend, and hosting from the other side or Radmin is the path.
- What ENet had and this gives up: a host whose router opened a port by UPnP, NAT-PMP or PCP
  (Router.cs) could be reached from behind any router. ICE could do the same only with its ports
  pinned (`libdatachannel.portRangeBegin/End`, which the plugin reads, v1 §2.3) and mapped by that
  code, one port per connection since the UDP mux is off; unproven, and not planned (§15 Q3).

---

## 6 · What the player reads (the checks assert these literals)

v1 §6 stands wherever it does not name room codes, ntfy, the matchmaker or TURN. What changes:

### 6.1 Host panel

| When | Text |
|---|---|
| HOST pressed | "Hosting. INVITE A FRIEND makes a code for one friend. Friends on this network or on Radmin VPN can type an address below instead." |
| below it | one label per address the listener serves: "Same network: {lan}:{port}", "On {Radmin VPN}: {ip}:{port}" (v1, from `Adapters`) |
| listener moved / could not bind | v1 §6.1's two texts (hosting and invites carry on either way) |
| INVITE pressed | "Making an invite…" |
| invite ready | "Invite copied. Send it to one friend, privately (it holds your IP addresses). When their reply comes, just copy it: the game takes it. Or paste it here." (the second sentence without Q1: "Paste their reply here as soon as it comes: it works for {W} s.") |
| no STUN answered | + " No STUN server answered, so this invite works only on your network and over Radmin VPN." |
| reply taken | "{name} is joining…" |
| not connected 12 s after the reply | "{name} could not get through ({reason}). Their reply may have run out: a reply works for {W} s. A new invite for them: COPY." |
| an invite not answered in 15 min | "An invite from {n} min ago ran out unused." |
| a paste guest dropped | "{name} dropped. Send them this invite to come back (their place is held {HoldFor} s): COPY." |
| full | "The session is full ({MaxPlayers} players, counting invites not yet answered). Cancel an invite to free a place." |
| an invite pasted into the reply box | "That is an invite: send it to a friend. Their reply goes here." |
| a reply for an unknown or used id | "That reply is for an invite this session no longer has (used, cancelled or expired). Send them a new invite." |
| another build knocked (typed row) | v1 §6.1's text |
| plugin missing | v1 §6.1's text |

### 6.2 Guest

| When | Text |
|---|---|
| a valid invite pasted | "Reply copied. Send it to {hostName} now: they have {W} s to take it. {countdown}" |
| the invite's flag: host had no STUN answer | + " This invite works only on {hostName}'s network or over Radmin VPN." |
| another build's invite | "This invite is from build {theirBuild} ({theirProto:x8}); you are on {ourBuild} ({ourProto:x8}). You both need the same release (Esc menu, bottom)." (S1's wording) |
| a reply pasted into JOIN | "That is a reply code: it goes to the host, not into JOIN." |
| damaged code | "That code is damaged or cut off: copy the whole message again." |
| the countdown ran out | "{hostName} did not connect within {W} s of your reply. MAKE A FRESH REPLY and send that one, or ask them for a new invite. Playing offline." |
| ICE failed | v1 §6.2's "Could not get through to {hostName}: {reason}. {next} Playing offline.", with the TURN sentence deleted; `{next}` is "Try again with you hosting, or use Radmin VPN (radmin-vpn.com)." |
| lost the host, paste row | §3.9's text |
| typed address refused / no answer / other build / full | v1 §6.2 and S1's `CouldNotReach`, with S1's "use the host's room code" changed to "ask the host for an invite code" (§9) |

### 6.3 COPY NETWORK REPORT

v1 §6.3, minus the matchmaker lines, plus:
- per invite and reply made: which STUN row answered (or none) and in how many ms; how many
  candidates the fit rule dropped; the code's length in characters;
- per join: the row, and the times of invite made, reply made (guest) or taken (host), ICE connected,
  channels open, admitted;
- the longest gap and the peak backlog **per `NetChannels` row** (§3.2), not one figure per peer;
- `Link.ReplyWindowS`, and whether the clipboard pickup is on.

`Hints.cs` "multiplayer" row: "HOST THIS WORLD, then INVITE A FRIEND and send them the code. To join,
paste an invite into JOIN and send back the reply it copies."

---

## 7 · Files and contracts (CLAUDE.md §3)

| File | Owns | Rows / contract | Header must say |
|---|---|---|---|
| **new** `scripts/Link.cs` | the transport: the one place that names WebRTC | `Link.Plugin` row and `Link.Available` (v1); **`Link.Servers`** (STUN rows, mutable), `StunFirst` and `Gather` (the walk), `Config(row)`; `Channels()` (reflection over every `RpcAttribute` in the assembly, i.e. S1's `NetChannels` rows plus a test run's stream, gaps `Reliable`, §3.2) and `ChannelOf(method)`; consts `GatherMs` 2000, `QuietMs` 8000, `BeatMs` 500, `LinkMs` 12000, `InviteLifeS` 900, `ReplyWindowS` (R0; placeholder 25); `Sealed`, `Hang`, `Backlog(mp, id, channel)`; the beat's bookkeeping | replaces ENet's `SetTimeout`, `ThrottleConfigure`, `RoundTripTime`, `PeerDisconnectLater` and let-go, and net_design's `Stun.cs`/`Routes.cs`/`Relay.cs`. A new STUN server is a row; a plugin upgrade is a row. **Trap while S1 is in the tree:** S1's private `Net.Link(ENetPacketPeer,int)` hides the class inside Net (CS0119), so R0 references `Link` only from SessionMenu, and R2 deletes the method in the edit that first uses the class inside Net (v1 §7) |
| **new** `scripts/Rendezvous.cs` | how two machines swap session descriptions | `IRendezvousPath {Id, Claims(text), Auto, Stun, Budget, host Open/Close, guest Start}`; `Rendezvous.Paths = {Paste, Address}` (first claimant wins); the four record kinds and `Pack/Unpack`; `Sdp` (template, `Strip`, `Build`); text (`WSI`/`WSR`, Crockford, check); `Fit`; the listener and the dialer; the clipboard seam `Rendezvous.Clipboard` (a mutable `Func<string>`, default `DisplayServer.ClipboardGet`, swapped by the harness) | replaces the reveal and copy of a public address, `Describe`, `Reach`, `PublicIpService`. A new way to carry codes is a row; a new record is a kind |
| `scripts/Net.cs` | the session | `Host` (`CreateServer` + listener); `Invite()`; `TakeCode(text)` (a reply on a host, an invite on a guest); `Pending`; `Join(text)` by row; `NetBeat`/`NetBeatBack`; the goodbye (§3.7); `Report()`; S1's `NetWelcome`, `Admitted`, `ToHeard`, `DialAddress` kept; **`NetChannels` gains `Beat = 12`** (a Reliable row, §3.2), and its header's ENet sentence ("255 channels…") becomes "each row is a negotiated data channel, id 3 + row" | – |
| `scripts/Router.cs` → **`scripts/Adapters.cs`** | this PC's LAN and overlay addresses | `Adapters.Lan()`, `Adapters.Overlays()`, used by the labels and by `Fit`'s ranking; the UPnP, NAT-PMP and PCP code deleted | renamed in the edit that deletes UPnP (v1 §7) |
| `scripts/SessionMenu.cs` | the panel | INVITE A FRIEND, the pending list with COPY and CANCEL (§6.1's full text sends the host to it), the reply box, the address labels; the JOIN box (invite or address); the guest's reply, countdown and MAKE A FRESH REPLY; COPY NETWORK REPORT | – |
| `scripts/Game.cs`, `scripts/Hints.cs` | Quit's wait; the hint | `netBusy = !Net.I.NetworkIdle` (no peer, no let-go, no listener); §6.3's hint | – |
| `typecheck/GodotStub.cs` | the weak stub | `WebRtc*` members used, `ClassDB.ClassExists`; `ENetMultiplayerPeer` deleted | – |
| **new** `tools/import.ps1` | importing a folder, judged by what it registered (SPIKE F1) | v1 §7 and §8.2, unchanged | – |
| `addons/webrtc_native/` | the plugin | v1 §8.1 | – |
| `tools/smoketest/wan.py` | **the network between players**, rewritten as the box | §10.1 | – |
| `tools/smoketest/run.ps1`, `SmokeTest.cs.txt`, `tools/screens/Shots.cs.txt` | the checks | §10; run.ps1's expected engine lines are named rows (v1 §2.6 S9) | – |
| `tools/pack.ps1`, `tools/install.ps1`, `play.ps1`, `tools/snapshot.ps1`, `tools/manifest.ps1` | packaging | v1 §8, deltas in §8 | – |

**Deleted in R2's edit** (invariant C; `UNUSED ANYWHERE: 0`): v1 §7's list, which is Net.cs's
reach/describe/public-IP code, every ENet use, Router.cs's UPnP family, `fakeigd.py`, the router
scenarios, the reveal checks and frames 51-52's describe block. Plus, from S1: `Net.Link(ENetPacketPeer,
int)` and its throttle, `Net.QuietMs` (it moves to `Link.QuietMs`, one constant, not two), the
`server` flag of `_lettingGo` and `Host()`'s loop over it, and `PumpLetGo`'s ENet body. Changed,
not deleted: S1's channel check's ENet channel count, and the harness's `StreamChannel` (§3.2).
**Never built** (they were v1-only): `Rendezvous.Service`, the boards, the ntfy records and texts,
`Link.Only`, `route=`, the TURN row.

**Kept:** `ParseAddress`, `DialAddress`, the retries and RECONNECT (for Auto rows), `NetBye`, the
fingerprint, `Metered`, `AskHost`, `FromPlayer`, `SenderOf`, `ToHeard`, `NetPose`, `Session`, and the
whole authority model.

---

## 8 · Packaging

v1 §8 stands whole: `addons/webrtc_native/` vendored with the `.gdextension` trimmed to the two
Windows x86_64 lines, the R0 one-DLL experiment, `tools\import.ps1` for SPIKE F1's first-import
crash (both runners, play.ps1, and pack.ps1 before its export), the DLL in the runtime part with no
part rule, the seven licences copied by pack.ps1 **before** its file listing, install.ps1's presence
check used by step 2 as well, `Link.Available` gating HOST and JOIN, and the fingerprint notes. The
deltas:

- **NOTES `[PLAYING WITH A FRIEND]`:** "Host: MULTIPLAYER, HOST THIS WORLD, INVITE A FRIEND, and send
  the code privately (Discord is fine). Friend: paste it into JOIN and send back the reply it copies.
  Host: copy the reply (the game takes it) or paste it into the panel, within {W} seconds. Same house
  or Radmin VPN: type the host's address instead; no codes needed."
- **NOTES, one privacy line:** "When you make an invite or a reply, the game asks Google's or
  Cloudflare's public STUN server for this PC's internet address. Nothing else contacts any server."
- `[THIRD-PARTY]` unchanged (v1 §8.4).
- **An ENet build and a WebRTC build cannot meet** (v1 §8.7). A friend on the old build who pastes an
  invite into JOIN gets the old "No answer from…" or "Could not find…" text; the invite text's
  "You both need the same release" and the build ids in the Esc menu are the cure, as before.

---

## 9 · S1 under WebRTC: what it contributes, what it makes moot

READ from `git -C net_tree diff` (Net.cs, Hub.cs, Boss.cs, Yard.cs, MainMenu.cs, run.ps1,
SmokeTest.cs.txt, typecheck.ps1, analyse/run.ps1, CHANGES, DESIGN).

| S1 piece | Under WebRTC |
|---|---|
| **`NetChannels`: one transfer channel per stream** (11 rows; each ordered RPC names its row; a rung-3 check refuses an ordered RPC off the table or sharing) | **Contributes the most.** Each row becomes its own negotiated data channel and SCTP stream (§3.2), so the reliable quirk's pause after a loss stays in that stream; v1 feared it would stall every state update at once. `Link.Channels()` reads these rows; S1's check is the check v1 would have written. v2 adds row 12 `Beat` (Reliable, so the check's Hub-only and one-RPC-a-row rules, which bind UnreliableOrdered RPCs, leave the beat's two RPCs on `Net` alone). The header's ENet sentence ("ENet opens 255 channels… up to 254") is replaced in R2, and so is the check's one ENet line (`GetMaxChannels()` → `Link.Channels().Length`); the harness's own stream moves off channel 200 into the negotiated set in R0 (§3.2) |
| **S1's own Known broken: one RPC carrying several streams orders them against each other, and a report is thrown away when a later one for another overtakes it** | **The "thrown away" half is moot**: WebRTC delivers every message on a row, in order, so nothing is discarded. What remains is the quirk's cost: a loss delays the other streams on that row by one resend. `-Wan` prints the gap per row (§10.4 R4), and CHANGES rewrites that entry |
| **`NetWelcome` / `NetWelcomed` / `Admitted` / `ToHeard`; `IsOnline` needs a session and a finished join; `Hub.OnAdmitted`; `SendIdentity`'s guard and its 1 s / 3 s repeats deleted; run.ps1's `SYS_COMMAND_AUTH` pass deleted** | **Survive unchanged, and are still needed.** SceneMultiplayer's auth rides channel 0's reliable data channel, a pilot's report rides `Ships`: different SCTP streams, with no order between them, exactly the reordering S1 fixed on ENet. `-Wan`'s zero-`SYS_COMMAND_AUTH` assertion keeps holding it |
| **Host streams sent per peer** (`ToHeard`; guests' reports relayed by the host, `NetShipState` names its owner) | **Survives, and makes the backlog guard a condition in two loops instead of a new table** (§3.8) |
| **Goodbye: `_lettingGo` list, every goodbye keeps its own time, `LetGoMs` 2000, a quitting game waits for them** | **The list, the per-goodbye time and the 2,000 survive**; the ENet pump body is replaced (§3.7) |
| **P13 as S1 wrote it: the `server` flag, `Host()` closing a host's let-go because its socket holds the port** | **Moot** and deleted: ICE sockets are ephemeral, the listener closes at `Shutdown`. Its check stays and passes by construction |
| **`QuietMs` 8000 in `Net.Link(ENetPacketPeer,int)` (`SetTimeout`); the stall checks ("a stall is not a drop, both ways"); re-timed waits** | **The 8,000 and the checks survive**, carried by the beat's watchdog (§3.6); the method is deleted |
| **ENet throttle `ThrottleConfigure(5000, 2, 0)` and its two checks (host: each guest's link reads 5000/2/0; guest: its own end does)** | **Moot and deleted**: WebRTC has no throttle, and nothing is dropped before sending. The guest's `streamRate ≥ 8 a second over 12 s` survives as the rate check; under the quirk it can fail only on a backlog |
| **AAAA: `Net.DialAddress`** (IPv4, else IPv6, never link-local) and its check | **Survives**: the typed row's TCP dial uses it |
| **Wording: `CouldNotReach`** ("No answer from {addr} in 12 s. An address works on the same network or over Radmin VPN, and the host must allow Warships through Windows Firewall; for friends elsewhere, use the host's room code.") and "you both need the same release (Esc menu, bottom)" | **Survives with one edit**: room codes will never exist, so "use the host's room code" becomes "ask the host for an invite code" in R2. **Recommendation for S1 now:** its ENet release has no way in for friends elsewhere but Radmin, so the S1 text should say "for friends elsewhere, use Radmin VPN" rather than point at a room code (S1's own Known broken already flags it) |
| **The public-address lookup, `Net.PublicIpService` (`https://api.ipify.org`, READ net_tree Net.cs:500)** | **Deleted in R2** (§7), with the reveal it serves. **Recommendation for S1 now:** it is third-party infrastructure the network ruling excludes, and S1's ENet release still calls it on every HOST. Either S1 drops the lookup and lets `Describe` work from its `routerExt` argument alone (the router's own answer by UPnP or NAT-PMP, already passed in), losing the second opinion that spots a double NAT or carrier-grade NAT, or the owner accepts it for that one interim release |
| **typecheck/analyse scratch per checkout; MainMenu `ComeRound`** | transport-free; survive |

**Net effect:** S1 is not wasted. Everything that is about the game's protocol (the welcome, the
per-stream channels, the per-peer sends, the goodbye's shape, the 8 s, the dial, the words) carries
into R2; only its ENet plumbing (throttle, `SetTimeout`, the port-reuse flag, the channel check's
ENet count) goes.

---

## 10 · The harness

### 10.1 The box (`wan.py`, rewritten; stdlib Python; one process; run.ps1 starts it for every run, solo included, where `fakeigd.py` stood)

- **A STUN responder** on `127.0.41.1:3478`: Binding requests only, answered with XOR-MAPPED-ADDRESS
  (about 20 lines). It makes the walk exact at rung 3 and gives the sealing check a last candidate to
  race for.
- **Two silent UDP ports**, `127.0.0.1:19482` and `:19483`: as STUN rows, a network that answers
  neither provider.
- **A silent TCP port**, `127.0.0.1:19481`: accepts and never answers (the typed "no answer in 12 s"
  check, hermetic).
- **The pair proxy** (`POST /box/pair` → two loopback ports `A` and `B` on `127.0.42.1`; then
  `POST /box/pair/{n}?host=…&guest=…` with the real endpoints): a datagram arriving at `A` leaves
  from `B` to the host's real address; one arriving at `B` leaves from `A` to the guest's. To each
  side the other is one fixed address, like a NAT with fixed mappings. Delay, jitter and loss from
  `WARSHIPS_WAN` (S1's defaults: 90 ms ± 25 each way, 2% lost; "150,40,5" for a worse day), seeded
  by the run's `SEED`.
- **`POST /box/blackhole?s=…`** (drop every proxied datagram for s seconds), **`GET /box/stats`**
  (datagrams per pair and direction), and it exits by itself after a given time, as today.
- **No ntfy stand-in** (nothing to stand in for) and **no TURN stand-in** (v1's biggest harness
  unknown is gone with TURN).

### 10.2 The courier

- The harness carries codes the way Discord does. The host role writes each invite it makes to
  `res://invite-{port}-{n}.txt` (globalized: the scratch copy is shared, as v1's room file); the guest
  role reads it, waits a `Vary`'d 0.5-3 s (where a friend would be), and puts it through **the JOIN
  box's own handler**; it writes the reply it gets to `reply-{port}-{n}.txt`; the host role puts that
  through **the reply box's handler**, and once per run through the clipboard seam instead, which
  proves the pickup path (§15 Q1). Headless clipboards are never relied on.
- **Under `-Wan` the courier rewrites the codes**: it unpacks each with the game's own codec, keeps
  one IPv4 host candidate, replaces it with the box pair's `A` (in the invite) or `B` (in the reply),
  registers the real endpoints with the box, and packs it again. Each side then knows the other only
  by a box address, so every game datagram crosses the box's delay and loss. This is harness code
  acting as the network, not a seam in the game: the game's codes are unchanged and nothing in
  `scripts/` knows the courier exists.
- **The unknown, and its fallback.** libjuice accepts loopback remote candidates (SOURCE S10), but a
  pair whose local side is the LAN host candidate and whose remote side is a loopback proxy has not
  been run. R1 proves it on the in-process pair. **If libjuice will not use it after two attempts at
  rung 3:** `-Wan` loses loss and delay on game datagrams (CHANGES Known broken), and the watchdog
  check uses a harness-only `Net.DropBeatsFor(s)` knob (a mutable static like `SkipGoodbye`) instead
  of the blackhole.

### 10.3 Roles

The six stay; every one runs WebRTC.

| Role | Row | What it proves beyond today |
|---|---|---|
| `solo` (rung 3) | – | the plugin loaded; in-process pairs (host offers); the 16 channels (a release has 15, §3.2); codec round trips on real bundles; the reply window check; the STUN walk; the typed failures; `NetworkIdle` after `GoOffline` |
| `host` (27115) | serves both | makes invites for the courier; the fresh invite after a paste guest's drop; both rows in one session |
| `guest` | **paste** (courier) | joins by invite; **its drop does not retry by itself** (no attempt over 20 s), the host's fresh invite brings it back into its held place (P10b); under `-Wan`, every datagram through the box: the rate check, zero `SYS_COMMAND_AUTH`, the watchdog |
| `guest2` | **address** `127.0.0.1:27115` | knocks as another build (refused over TCP before any WebRTC object, both texts); a knock that passes with `PretendAt = Auth` refused in-band by `OnAuth`; then joins; `Drop(thirdOld)` and its automatic retries |
| `ahost` (27125) | address | – |
| `aguest` | address | the host's vanish (`SkipGoodbye`), retries at 2/8/14 s and RECONNECT at 19 s, unchanged literals |

- **`-Wan` now covers the invite path** (the `guest` role), which is the internet path. The typed rows
  run direct in every run: they model a LAN or an overlay, and the box cannot stand between two
  processes that exchange their codes themselves. What that leaves unseen is listed in §10.5.
- Direct ICE on one machine needs an adapter that is up (v1 §11.3): the `host` role fails with "rung 5
  needs a network adapter that is up (this machine has none)" otherwise, and so does the solo pair.
- `HostAt(port)` is `127.0.0.1:{port}` (the listener). S1's `wan` port offset (+1000) is deleted.

### 10.4 Checks per slice (literals from the ruling, an RFC, the spike or the table they prove)

**R0 (runner guard, then rung 3, solo).** v1 §11.5 R0, except:
- the in-process pair's **host makes the offer** (the spike's direction); v1's NEW "client offers"
  check and its fallback are deleted;
- the harness's `StreamChannel` moves from 200 to `NetChannels.BossSounds + 1` (§3.2), and the pair
  is configured with `Link.Channels()`: every row's data channel opens (15: S1's 11 rows, the
  harness's stream, the 3 built-in), an RPC on the highest row arrives, and the lifetime fact holds
  on every row (`GetMaxPacketLifeTime() == -1`); S1's channel check still holds (12 is no row; ENet
  opens 255), and the rate check that uses the channel is next run at rung 5, in R2;
- **NEW, the reply window (§3.4):** the one-time seven-pair measurement (replies at 5, 10, 15, 25,
  35, 45, 60 s), recorded in DESIGN.md, under 15 s stopping the batch (§3.4); then, in the same edit
  that sets `ReplyWindowS`, the permanent check:
  a pair whose host applies the reply `ReplyWindowS` after the guest made it connects within 2 s of
  that. It starts at the top of the solo run and is judged in the background, so it adds no wall time
  beyond the run's own length.
- unchanged from v1: `Link.Available`; `CreateServer` → `Connected` in the same call; handlers on the
  main thread; `Close` and `DisconnectPeer` seen within 1 s; `Link.Hang` on a gone id prints nothing;
  `ObjectCount` flat over three connect/close cycles; the one-DLL experiment. **Stop rule** as v1.

**R1 (rung 3, solo).**
- The codec: invite, reply, knock and refuse round-trip; **the bundles of R0's live pair, stripped and
  rebuilt, equal the originals byte for byte** except `o=`'s id; a template-unknown SDP line is
  refused and named; a foundation over 255, a non-base64 credential character and a non-sha-256
  fingerprint are refused.
- The text: a code round-trips; it is found inside a whole Discord-style message (name, time, line
  breaks, words after the code, a name holding `wsr` before it); one changed character fails the
  check; lower case decodes; `I`, `L`, `O` decode as 1, 1, 0.
- `Rendezvous.Paths`: every invite and reply is claimed by the paste row and none by the address row;
  a plain address is claimed by the address row.
- The fit rule: the 20-candidate shape E fits 400 characters with its srflx, overlay and LAN
  candidates kept; two IPv6 addresses on one /64 keep one.
- The walk, on a host's peer and on a guest's peer 1: with rows `{127.0.0.1:19482,
  127.0.0.1:19483}` a bundle is sealed with host candidates only and its flag set, at 4,000 ms and no
  more than two frames late (one per seal); the next gather in the process takes under 100 ms (the
  memory). With rows `{19482, 127.0.41.1:3478}` the walk stops at row 2 with a server-reflexive
  candidate and `StunFirst == 1`. (If libjuice will not gather a loopback-mapped srflx, this second
  check asserts the timing only, and DESIGN.md says so.)
- The sealing rule (v1 §2.6 S2): 20 gathers against the box's STUN responder, each sealed by
  `Link.Sealed`, each holding its server-reflexive candidate (under the same fallback, its host
  candidates only); without the responder, each holding every host candidate a gather sealed 1 s
  after `Complete` holds.
- The rows end to end with dummy codes: the address row over an in-process listener on a free port
  (knock → invite → reply; a second knock from the same `guest` leaves one pending entry; `full`,
  `build` and `rate` refusals); the paste row through the courier files. This gives every row member a
  harness caller, which `UNUSED ANYWHERE: 0` requires (v1 §11.5).
- The pair proxy: the in-process pair, its codes rewritten by the courier, connects through the box,
  and `/box/stats` counts datagrams both ways (§10.2's unknown).
- `Link.Servers` and `Rendezvous.Clipboard` are mutable (out of the fingerprint).

**R2 (rung 3, then rung 5, then rung 5 `-Wan` once).**
- Every existing multiplayer check passes on WebRTC, with its waits re-set (v1 §11.5 R2); S1's
  channel check counts `Link.Channels()` (§3.2) and passes with `Beat` on `Net` as a Reliable row.
- New: `guest` joined by invite and `guest2` by address (`Net.I.JoinedBy` records the row); `guest2`'s
  other-build knock refused before any connection, counted once by the host's text, no peer ever
  appearing; the in-band `OnAuth` refusal; the paste guest's drop with no retry, the host's fresh
  invite, and the return into the held place; the solo typed failures (`127.0.0.1:9` refused with
  "Nothing is hosting at 127.0.0.1:9" in under 3 s; `127.0.0.1:19481` "No answer from … in 12 s"
  between 11.8 and 12.6 s).
- `-Wan`: zero `SYS_COMMAND_AUTH` lines (S1), every other check green through the box.

**R3 (rungs 3, 4, 5).** Every §6 string asserted literally where its path runs; clipboard pickup
through the seam (if §15 Q1 is yes); Shots: `51_invite_ready` (host panel with an invite, the
address labels and the pending list) and `52_reply_countdown` (the guest's reply and countdown)
**replace** `51_address_hidden` and `52_address_revealed`; `reply_expired` and `join_failed` take
the next free numbers when R3 lands (53-58 and more are taken, v1 §11.5). On a hermetic session,
`LINT: 0`.

**R4 (rung 5 `-Wan`).**
- **The watchdog:** after the `guest`'s return, the box blackholes its pair for 12 s. Both ends drop
  each other at 8 ± 1 s (`QuietMs`); the host's fresh invite, carried by the courier after the
  blackhole lifts, brings the guest back. About 25 s more for the `guest` role, whose limit rises by
  that much.
- **The rate check** at `WARSHIPS_WAN="150,40,5"`: S1's `streamRate ≥ 8 a second over 12 s`. Under
  the quirk it fails only on a backlog that never drains; then §3.8's guard is the next step, with
  the owner told first.
- **Printed, not asserted:** the longest gap and the peak backlog per `NetChannels` row, at the
  default path and at 150/40/5. No literal exists for them yet, so none is invented; they go into
  CHANGES, and a field report or the owner sets one.

**R5 (rung 6).** v1 R6's packaging checks, then the bar once.

### 10.5 What no rung can see (CHANGES "Known broken" until §11 is done)

- Real NATs between two homes, the owner's double NAT, and the Block-rule question (v1 §2.4).
- **The reply window over a real internet path**, and whether real players make it inside it.
- Google's and Cloudflare's STUN from real homes (the spike reached Google's once from here).
- Loss and delay on the **typed rows' own datagrams** (they run direct; §10.3).
- The exported Warships.exe with the release DLL across two machines; antivirus and SmartScreen; the
  GUI editor's first open with the addon; a machine with no adapter up; IPv6 between homes (v1
  §11.6).

---

## 11 · The two-machine test, for the owner and a friend

**Before:** the WebRTC release is out (standing OK). Both have Discord open in a DM. Radmin VPN is the
fallback: the owner has it; the friend installs it only if step 11 is reached.

1. **Update.** The owner: `tools\install.ps1 -Force`. The friend: PLAY.bat. Both open the Esc menu:
   the build ids at the bottom must match. **Write down any Windows Security or SmartScreen message.**
2. **Owner:** MULTIPLAYER → HOST THIS WORLD → INVITE A FRIEND. The code is copied. Note whether the
   panel says no STUN server answered. Paste it into the DM.
3. **Friend:** copy the whole message. MULTIPLAYER → the JOIN box (it may have filled itself) → JOIN.
   The reply is copied: **paste it into the DM straight away.** Note the countdown.
4. **Owner:** copy the friend's message (right-click → Copy Text). The game takes it; or paste it into
   the panel's reply box. **If Windows asks about the firewall on either PC, write down which button
   was pressed and which boxes were ticked.**
5. **Connected?** Play five minutes, including a fight. Then both: COPY NETWORK REPORT, paste into the
   DM. **Not connected 30 s after step 4?** Both copy the report anyway, then go to step 11.
6. **Drop:** the friend turns Wi-Fi off (or pulls the cable) for 15 s. The owner's panel should say
   "{name} dropped" after about 8 s, with a new invite ready. Wi-Fi back on; the owner sends the new
   invite; steps 3-4 again, **inside 90 s**: the friend should land back in their place.
7. **Slow reply:** the friend leaves (Esc → Leave); the owner makes a new invite; the friend pastes
   it but **waits until the countdown runs out** before sending the reply. The friend's screen should
   say so; MAKE A FRESH REPLY, send that one; the owner copies it: it should connect.
8. **Swap:** the friend hosts and invites, the owner joins. Steps 2-5 once. Reports again.
9. **The firewall question** (v1 §2.4): the owner removes Warships' Allow rule (Windows Security →
   Firewall & network protection → Allow an app → untick Warships) or cancels the prompt, then makes a
   new invite. Does it still connect? Put the rule back afterwards.
10. **A second device in the owner's house** (if any): join by the typed address shown under the
    invite button, then by an invite.
11. **Radmin fallback** (if steps 2-5 failed both ways, or any time): both join the same Radmin VPN
    network. The friend types the owner's Radmin address (the panel lists "On Radmin VPN:
    26.x.x.x:27015") into JOIN. It should connect in about a second, with no codes, and after a Wi-Fi
    blip come back by itself. An invite made while both are on Radmin works too.
12. Send every report to the developer session.

**What the results mean:**

| Result | Meaning | Next |
|---|---|---|
| Steps 2-5 worked both ways | ICE works between the two homes | done; Radmin only for convenience |
| Worked only with one side hosting | the other side's router maps per destination | that side hosts; the reports show which |
| Failed both ways, both reports list a server-reflexive candidate | two strict NATs: without a relay, nothing in the game can join them | Radmin (step 11); by the ruling, no TURN |
| Failed, a report says no STUN server answered | that network blocks STUN (or all UDP) | Radmin, or another network |
| Step 9 failed only with the rule removed | a Block rule stops ICE here | the design adds an install-time firewall rule, which needs the owner's OK |
| Step 7's countdown felt too short in step 3-4 | the players need longer than the plugin allows | tell the developer: `ReplyWindowS` moves, up to what R0 measured |
| Step 6 took longer than 90 s | the hold is short for pasting | §15 Q2 |

---

## 12 · Verified, and not

**READ this pass:** v1 whole; spike.md; the spike's real bundles (`runs\stun\offer_2.json`,
`answer_2.json`: the SDP's lines, `a=setup:active` in the answer, a 4-character ufrag and 22-character
password, `raddr 0.0.0.0 rport 0`, the kept port); `git -C net_tree diff` for every file it touches;
net_tree's `Hub.RpcToSector` (per-peer `RpcId`), `Game.Build` (read from BUILD.txt; `dev` from
source), `Net.PretendProtocol`, `SkipGoodbye`, `AskHost`, `FromPlayer`; SmokeTest's `HostAt`, `Drop`,
`NoRouterNoInternet`, `RouterScenarios`, the `streamRate` check and its literals; run.ps1's `-Wan`
start and defaults; pack.ps1's build id format; `Session.HoldFor`; the repo has no base32, base64 or
deflate code today and one clipboard write (SessionMenu's COPY ADDRESS).

**SOURCE this pass** (libjuice `agent.c` at `3c40a35`, the file the v1 review saved): P1-P3 of §2.2
(`agent_update_pac_timer`, the bookkeeping's failure branch, `agent_verify_stun_binding`,
`agent_dispatch_stun`, `agent_add_remote_reflexive_candidate`, `agent_set_remote_description`,
`agent_arm_keepalive`, the retransmission schedule).

**MODEL:** §4.4's sizes (a Python pack of the layout, calibrated against the spike's measured
bundles).

**RECALLED or unproven, each proved before anything rests on it:**

| Fact | Proved in | If false |
|---|---|---|
| libdatachannel's DTLS handshake timeout bounds the window when early checks succeed | R0's measurement (it is the path it runs) | nothing to change: `ReplyWindowS` comes from the measurement either way |
| `STUN_KEEPALIVE_PERIOD` is 15 s | not needed: only the keepalive's existence is relied on (SOURCE) | – |
| the rebuilt SDP is accepted by libdatachannel | R1, byte for byte against the plugin's own | the codec carries the plugin's SDP text verbatim (about 450 more characters per code) |
| a peer id can be re-added at once after `remove_peer` (the walk), on a host's peer and on a guest's peer 1 | R1's walk check, both sides | the host's walk draws a new id for the rebuilt connection; a guest makes a new client peer with the invite's id |
| libjuice uses a loopback pair proxy as a remote candidate | R1 (§10.2) | `-Wan` loses datagram loss; `DropBeatsFor` for the watchdog |
| libjuice gathers a loopback-mapped srflx from the box's STUN responder | R1 | the walk check asserts timing only |
| 15-16 negotiated data channels per peer open (a test build carries the harness's stream, §3.2) | R0 (15), R2 (16) | fewer rows, merging the least busy streams onto shared rows, with the owner told. Moving them onto channel 0 is not a fallback: it brings back the cross-stream stalls |
| Discord's 2,000-character message limit for non-Nitro accounts | common knowledge | codes are 400 at most anyway |

**Unverified, with the step that settles it:** ICE between real homes and the double NAT (§11 steps
2-8); the reply window with real players (steps 3-4, 7); the Block rule (step 9); antivirus and
SmartScreen (step 1); the quirk's cost on a real lossy path (step 5's report lines).

---

## 13 · Slice plan, with rungs

**Coordination** (as v1 §14): the combat batch edits Hub, PlayerShip, Boss, Targeting, Turrets,
Deployed and SmokeTest now; S1 is uncommitted in `net_tree` (Net.cs, Hub.cs, run.ps1, SmokeTest).
Every R slice touches SmokeTest and most touch Net.cs, so **R0 starts after both have committed**.
Writers never run in parallel; a read-only review may run beside any slice. No engine rung while a
compile rung is red.

| Slice | Content | Proved at |
|---|---|---|
| S1 (in flight, ENet) | as net_tree; with §9's wording recommendation | its own rungs |
| S2 (transport-free) | P4, P5, P8, P9, P10 + **P10b, the rejoin token**, before R2: a paste guest's return lands in its held place through it | rung 5 |
| **R0 · the plugin inside Warships** | vendor `addons/webrtc_native/`; `tools\import.ps1` and both runners calling it; `Link.cs` with `Plugin`, `Available`, `Hang`, `Sealed`, `Channels()` over every `RpcAttribute` (row 12 `Beat` comes in R2, below); the harness's `StreamChannel` moved from 200 to `NetChannels.BossSounds + 1` (§3.2); SessionMenu's plugin-missing gate; the R0 checks, including **the reply-window measurement** and the one-DLL experiment. **Stop rule:** a NEW check red with its fallback red stops the batch | rung 1 → 2 → 3 |
| R1 · the codes and the rows | `Rendezvous.cs` (records, `Sdp`, text, `Fit`, the listener and dialer, `Paths`, the clipboard seam); the rest of `Link.cs` (`Servers` with the two STUN rows, the walk, `Config`, constants, `Backlog`, `ChannelOf`); the box (STUN responder, silent ports, pair proxy, blackhole, stats) started by run.ps1 for every run (fakeigd stays until R2 deletes what it serves); the courier; every row member given a harness caller | rung 2 → 3 |
| **R2 · the switch** (one edit) | Net's session on `WebRtcMultiplayerPeer`; host offers on both rows; `Pending`; INVITE A FRIEND, the reply box and the JOIN box (minimal); the listener; the beat and watchdog (`Beat` = 12, a Reliable row, `StreamChannel` → `NetChannels.Beat + 1`, S1's channel check counting `Link.Channels()`, §3.2); the goodbye (§3.7); every hang-up through `Link.Hang`; retries by row (`Auto`); the shared name rule; GodotStub; `PretendAt` flags; **delete** ENet, UPnP, describe/reach/reveal/public-IP, fakeigd, the router scenarios, S1's `Net.Link` and throttle and `server` flag (§7); Router.cs → Adapters.cs; `CouldNotReach`'s invite wording; the roles moved (§10.3), `Drop()` rewritten, waits re-set; run.ps1's expected engine lines as named rows | rung 2 → 3 → 5 → 5 `-Wan` once |
| R3 · words, report, clipboard, frames | §6's texts; COPY NETWORK REPORT (§6.3); clipboard pickup (§15 Q1); Hints; Game.cs's comment; frames 51-52 replaced and two new | rung 3, 4 (`LINT: 0`), 5 |
| R4 · the watchdog and the rate | the blackhole check; the rate check at 150/40/5; the per-row gap and backlog printed | rung 5 `-Wan` |
| R5 · packaging and record | v1 R6 (pack.ps1, install.ps1, play.ps1, snapshot, manifest) with §8's NOTES texts; DESIGN.md: the plugin's hashes, one STUN row per connection and the walk, no ICE restart, **the reply window and its measurement**, every channel reliable and per-row, seal one poll after Complete, `Link.Hang`, the codec's template rule, the spike's traps; README: sizes and "Playing with a friend"; CHANGES: Handoff, Known broken = §10.5, the `-Wan` numbers. CLAUDE.md is not edited (the owner's standing rule), so its rung-4 frame count goes stale by the new frames and CHANGES says so (v1 §14). Then a `pack.ps1 -Dirty` export check (v1 §8.3), the one-machine check (the exported game hosts, a source window joins by invite, against the real STUN rows: the ruling allows those lookups), and **rung 6 once**. Then publish (standing OK) and run §11 | `verify.ps1 -Update` |

On R0's `Beat` row: CLAUDE.md's `UNUSED ANYWHERE: 0` counts members, and S1's check reads every
`NetChannels` row against an RPC. The simplest honest order is to add row 12 **in R2**, in the same
edit as `NetBeat`/`NetBeatBack`; R0's channel check then runs over S1's 11 rows plus the harness's
stream and proves 15 data channels, and R2's first rung 3 re-proves 16 (a release negotiates 15).

- **Riskiest first:** R0's reply-window measurement decides a number every player sees; R1's
  byte-for-byte codec check and the pair proxy are the two things everything after rests on; each has
  its fallback written above.
- After R5: publish (standing OK), then §11 with the friend.

---

## 14 · For the owner (six lines)

1. Invite codes replace every server: HOST, then INVITE A FRIEND copies a code of about 200 characters for one friend; they paste it into JOIN and send back a reply of about 160. Only Google's or Cloudflare's STUN is asked for your internet address, and if neither answers, codes still work on your network and over Radmin.
2. One catch is timing: the reply must reach your game within about 25 s of the friend making it. The plugin gives up about 40 s after the friend pastes on a typical home router, possibly sooner elsewhere, and the exact window is measured first. The friend sees a countdown and can make a fresh reply; with your OK, your game takes the reply off the clipboard, so you only copy their message.
3. Same house or Radmin: friends type your address instead, with no codes, and reconnect by themselves. A friend over the internet who drops needs a fresh invite, which your panel makes at once; their place is held 90 s.
4. No relay means two strict home routers may never connect directly. Today's build can still reach such a friend when the host's router opens a port by UPnP; the plan drops that (Q3). Radmin VPN is then the way in, and the test with your friend shows which you have. Networks that block UDP cannot play, as today. Friends install nothing for invites; only the Radmin fallback needs Radmin on both PCs, as today.
5. S1 carries over almost whole: its channel per stream now keeps WebRTC's "everything arrives reliably" pauses to one stream at a time, and the welcome, the 8 s drop, the goodbye and the wording stay. Only ENet's plumbing goes. S1's release still asks api.ipify.org for your address, which your no-third-party rule excludes; the plan removes it with the switch, and S1 can drop it sooner.
6. Scope: a new table (the invite and reply codes, and the two ways in), built as R0-R5 after S1 and S2 land. R0 measures the reply window before anything rests on it.

---

## 15 · Questions (the default is used if unanswered)

1. **Clipboard pickup.** While you have an invite waiting, the game reads the clipboard twice a second
   and takes a reply code for that invite from it; anything else is ignored, and nothing is stored or
   sent. A friend's JOIN box fills itself from a copied invite the same way. Without it, you paste the
   reply into the panel yourself, inside the window. [yes]
2. **The hold for a dropped friend.** Their place is held 90 s, the time for a fresh invite and reply
   to go both ways. Longer for friends who joined by invite? [keep 90 s; §11 step 6 shows whether it
   is enough]
3. **UPnP for the host.** Today's build asks the host's router to open its port (UPnP, NAT-PMP,
   PCP), which lets a friend behind any router in. Without TURN, that is the one in-game way past two
   strict routers. Keeping it means pinning ICE to a range of ports and mapping them (§5.3), unproven.
   [no: deleted as v1 planned. Your own double NAT does not answer UPnP (v1 §2.7), so it would not
   help you host; revisit if §11's "failed both ways" row appears with a friend's router that does]

Not questions: the reliable-delivery quirk (measured, printed, a written guard; v1 §16), and the
build id inside every invite (about 30 characters, kept because it lets a friend on another build read
which one to get).

---

## 16 · Critic pass (09-24): what was wrong, and where it is fixed

Checked against every ruling, against net_tree and the repo where cited, against libjuice `agent.c`
(P1-P3 hold as written, including the triggered check that revives a failed pair when the host's
late check arrives), and by re-running §4.4's model.

1. **The beat broke S1's check (§3.2, §3.6, §7, §9, §13).** S1's rung-3 check fails an
   UnreliableOrdered RPC that is not the Hub's or shares its row; v2 put two UnreliableOrdered RPCs
   on `Net` on one row while saying it "reuses the check and adds none". `Beat` is now a Reliable row
   (identical on the wire under the quirk), the RPCs stay on the autoload.
2. **The harness's rate stream had no channel (§3.2, §7, §10.3, §10.4, §12, §13).** `_Test.NetStream`
   sits on `StreamChannel = 200`, and the fingerprint's view (which `Link.Channels()` mirrored) leaves
   `_` types out, so the kept `streamRate` check could not pass; and S1's check counts channels with
   `ENetMultiplayerPeer.GetMaxChannels()`, which R2 deletes. Now: `Channels()` reads every
   `RpcAttribute`, `StreamChannel` is last row + 1, the check counts `Link.Channels()`; 15/16 channels
   in a test build (a release 15).
3. **§0.2 misstated four rulings.** Sentries "`Targeting.Sentry` unchanged" contradicted the
   painted-first ruling; "2.58 DPS each" read as per barrel (it is per heavy); Taunt's status rides
   `NetHostState`, not a reliable path; the V boost's 15 s cooldown was missing.
4. **Third-party lookup not named (§0.1, §9, §14).** `api.ipify.org` is in the tree today and in S1's
   release. v2's end state deletes it (as v1 did), but never said so against the ruling; the
   recommendation for S1 is added.
5. **"Not a regression" was false (§0.3, §5.3, §14, §15 Q3).** ENet with a UPnP-opened port reached
   friends behind any router; deleting UPnP with TURN gone loses that. Stated, and asked (default:
   delete, as v1).
6. **R0 could not measure a short window (§3.4, §10.4).** The grid started at 15 s and the formula
   had no rule for a short result. Now 5 and 10 s are probed, the window is "every delay up to",
   and under 15 s stops the batch with the owner told.
7. **The decoder did not know where a code ends (§4.3, §10.4 R1).** "Take base32 characters,
   skipping spaces" swallows words after the code, and the first `wsr` in a name would be taken for
   the prefix. The record is self-delimiting, every prefix place is tried.
8. **The fit rule contradicted its own figures (§4.3).** It dropped only "until the code fits", yet
   §4.4 C/D and R1 drop a second address on one /64 at 145-205 B. The /64 rule now runs first,
   unconditionally.
9. **§4.4 numbers.** Every packed size and code length reproduces; the deflated A figures were swapped
   (offer 600, answer 604); base64url is 17% shorter, not 18%; "the floor" was the reply with two
   candidates (74 B bare).
10. **Small ones.** The cap is 8 pilots with the host (ENet admits 8 guests today); `RpcToSector` is
    Hub.cs:562 now; the panel needs CANCEL (the full text asks for it); the IPv6 `raddr` tail was
    unread, not known; the walk's timing literal allowed one frame for two seals; the walk's id
    re-add is proved on a guest's peer 1 too; owner line 2 said the plugin gives up at 25 s.

Nothing third-party remains after R2 but the two STUN rows; friends install nothing for invites (the
Radmin fallback is the one exception, as today). No combat number is set here: §0.2 only routes the
rulings onto existing rows, so the 60 s boss and the raid pace are the numbers job's to hold.
