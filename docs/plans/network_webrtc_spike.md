# webrtc-native 1.2.1 spike: does it work in this project's setup?

**Verdict: yes.** It works under Godot 4.7.2 .NET, both from the editor binary and in an exported
release build. The typed C# API works as-is. Peers connect in one process, across two or three
processes on this machine with signalling through files, and from the exported exe.

Seven things the port must deal with. None of them blocks it.

1. **The first `--import` crashes on exit.** It happens on the first `--headless --import` after the extension appears. The import itself is complete.
2. **Reliable RPCs have a silent 256 KiB cap.** `RpcId` returns `Ok` and the message is dropped.
3. **A hard drop takes 25-26 s to detect,** and no API shortens it. The game needs its own heartbeat plus `DisconnectPeer`.
4. **A missing DLL is invisible to `Initialize()`,** which still returns `Ok`. It has to be detected another way.
5. **The export does not ship the licence files.**
6. **A console-wrapper pid gotcha** affects the harness's kill tests.
7. **The firewall is not exercised by a one-machine test.**

The friend's side needs nothing new: one extra DLL sits next to the exe, and it imports only
Windows system DLLs.

Everything ran in `scratchpad\webrtc\spike\`. The repo, `net_tree` and `%TEMP%\warships_smoke`
were not touched. Nothing was downloaded. The one outside network use was a single run against
`stun.l.google.com:19302` (Q4).

---

## Summary

| # | Question | Verdict | Key number |
|---|---|---|---|
| 1 | Extension loads under the .NET editor build; C# `new WebRtcPeerConnection()` + `Initialize` work | **PASS** | native class `WebRTCLibPeerConnection`, `Initialize -> Ok`, no "no default implementation" error |
| 2 | Two `WebRtcMultiplayerPeer`s in ONE process (server + client 2), SDP/ICE in memory; data channel opens; `[Rpc]` crosses | **PASS** | connected 19-21 ms after `CreateOffer` (3 runs) |
| 3 | TWO processes, signalling through files (the harness role-pair shape); also THREE (host + 2 guests, server relay) | **PASS** (10/10 multi-process runs connected) | guest: 29-45 ms from reading the offer to `connected_to_server` |
| 4 | Time to connect; what a dropped peer looks like | **measured** | graceful 3-28 ms; **hard kill 25.2-26.3 s** (4 runs); host kick 17 ms |
| 5 | Export: templates present? DLL next to the exe? exported exe connects? | **PASS** | templates `4.7.2.stable.mono` present; `libwebrtc_native.windows.template_release.x86_64.dll` copied next to `Spike.exe`; exported pair connected, all checks green |

---

## Setup

- Godot: `C:\Users\logan\Desktop\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe`.
  It was found with `tools\find-godot.ps1`'s search. The runs use the `_console.exe` beside it, as `tools\smoketest\run.ps1` does.
- Export templates: `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` (windows debug and release, x86_64, x86_32 and arm64).
- dotnet SDKs: 8.0.425 and 10.0.401.
- Unzipped from `godot-extension-webrtc_native.zip` into `spike\proj\addons\webrtc_native\`:
  - `webrtc_native.gdextension` (`compatibility_minimum = 4.3`, entry `webrtc_extension_init`)
  - `LICENSE.*` (7 files)
  - `lib/libwebrtc_native.windows.template_debug.x86_64.dll`: 4,098,560 B, sha256 `706ba39c…07a3`
  - `lib/libwebrtc_native.windows.template_release.x86_64.dll`: 4,124,672 B, sha256 `bf9d09a5…0476`
- **DLL imports** (PE import table, parsed by `spike\tools\peimports.py`): `bcrypt.dll, IPHLPAPI.DLL,
  KERNEL32.dll, msvcrt.dll, WS2_32.dll`, and nothing delay-loaded. All are Windows system DLLs, so
  **no VC++ redistributable is needed** on a friend's machine.
- Spike project (`spike\proj\`, written fresh; only the csproj shape was taken from the repo):
  - `project.godot` (assembly `Spike`, main scene `Main.tscn`)
  - `Spike.csproj`: `Godot.NET.Sdk/4.7.2`, net8.0, `EnableDynamicLoading`
  - `nuget.config`: points at the Godot nupkgs, as the smoke runner does
  - `Spike.sln`: the Godot shape with Debug, ExportDebug and ExportRelease. It is required for export.
  - `export_presets.cfg`: mirrors the repo's preset (x86_64, `embed_pck=false`, console wrapper 0)
  - `Main.cs`: the roles
  - `Pinger.cs`: the RPC surface
- The RPC modes mirror what Warships uses: `Reliable` (21 uses in the repo), `UnreliableOrdered` (10)
  and `Reliable` on `TransferChannel = 1` (Hub's `Cosmetic`). `Unreliable` is also tested.
  - Channel 1 needs `channels_config = [Reliable]` in `CreateServer`/`CreateClient`.
  - On channel ≥ 1 the mode comes from that config, not from the attribute.

### Commands (exact)

```
# build (in spike\proj)
dotnet build -nologo -v q
# import (registers the extension in .godot\extension_list.cfg)
Godot_v4.7.2-stable_mono_win64_console.exe --headless --import --path spike\proj
# one role (editor binary)
Godot_v4.7.2-stable_mono_win64_console.exe --headless --max-fps 60 --path spike\proj --log-file <run>\<role>.godot.log -- <role> [role args] --log <run>\<role>.log
#   roles: probe | inproc [--kick] [--blobs a,b,..] [--stun URL]
#          host  --sig DIR --guests 2[,3] [--until S] [--stay] [--stun URL] [--enet PORT]
#          guest --sig DIR --id N [--relay M] [--drop graceful|exit|none] [--until S] [--stun URL] [--enet PORT]
# export
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path spike\proj --export-release "Windows Desktop" spike\export\Spike.exe
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path spike\proj --export-debug   "Windows Desktop" spike\export_debug\Spike.exe
# one role (exported exe; no --path)
spike\export\Spike.exe --headless --max-fps 60 --log-file <run>\<role>.godot.log -- <role> ... --log <run>\<role>.log
# everything above, staged
powershell -ExecutionPolicy Bypass -File spike\run.ps1 -Stage import|probe|inproc|kick|blobs|stun|export|pair|trio|exitguest|killguest|killhost|enetpair [-Exe spike\export\Spike.exe] [-Tag name]
```

Every run's logs are in `spike\runs\<tag>\`:

- `<role>.log`: the spike's own log, with UTC wall clock and ms since `_Ready`
- `<role>.out` / `<role>.err`: engine stdout and stderr
- `<role>.godot.log`: the engine's log file
- `offer_N.json` / `answer_N.json`: the signalling bundles
- `kill.txt`: when the runner killed a role

---

## Q1: extension + C# API under the .NET editor build: PASS

`run.ps1 -Stage probe` (`runs\probe\probe.log`):

```
GDExtensionManager.GetLoadedExtensions: [res://addons/webrtc_native/webrtc_native.gdextension]
ClassDB.ClassExists(WebRTCLibPeerConnection) = True
new WebRtcPeerConnection(): C# WebRtcPeerConnection, native class WebRTCLibPeerConnection
Initialize({iceServers:[]}) -> Ok
CreateDataChannel(negotiated id 7) -> WebRTCLibDataChannel label=probe id=7
CreateOffer -> Ok
ClassDB.Instantiate("WebRTCPeerConnection"): native class WebRTCLibPeerConnection, C# WebRtcPeerConnectionExtension
new WebRtcMultiplayerPeer().CreateServer([Reliable]) -> Ok
PROBE PASS                      (exit 0; stderr empty)
```

- The typed C# classes (`WebRtcPeerConnection`, `WebRtcMultiplayerPeer`, `WebRtcDataChannel`) live in
  core GodotSharp. `typecheck.ps1` and `dotnet build` compile them with or without the extension present.
- The extension supplies the implementation at runtime, and plain C# `new` gets it.
- **Import crash (F1).** `--headless --import` exits `-1073741819` (0xC0000005, access violation)
  on the first import after the extension appears. The crash comes at exit, after every step
  printed `[ DONE ]`. Nothing goes to stdout or stderr.

  | Case | Result |
  |---|---|
  | fresh `.godot`, with addon | crashed 3/3 |
  | an already-imported project with the addon newly added | crashed 1/1 |
  | the next import | exit 0, every time |
  | same project without the addon (control) | exit 0, 3/3 |
  | `--headless --editor --quit-after 60` on a fresh tree with the addon | exit 0, 2/2 |
  | every game run afterwards | exit 0 |

  - The import output is complete: 32 files in `.godot`, and `extension_list.cfg` is written.
  - The probe ran PASS straight after a crashed import.
  - `tools\smoketest\run.ps1:153` and `tools\screens\run.ps1:106` import a fresh copy on every run
    and ignore the exit code, so they will hit this every run and carry on. Anything that starts
    checking the import's exit code must allow for it, or import twice.

## Q2: two peers in one process: PASS

`run.ps1 -Stage inproc` (`runs\inproc\inproc.log`).

**Setup.**
- `/root/Main/A` and `/root/Main/B` each get their own `SceneMultiplayer`, via
  `GetTree().SetMultiplayer(api, branchPath)`.
- A is `CreateServer([Reliable])` and B is `CreateClient(2,[Reliable])`.
- One `WebRtcPeerConnection` each, with `iceServers: []`.
- SDP and candidates are forwarded in the signal handlers (trickle).
- The `Pinger` node sits at the same relative path in both branches.

```
pcA.CreateOffer -> Ok
A local offer (444 chars) / A cand UDP v4 host / B local answer (444 chars) / B cand UDP v4 host
A EVENT peer_connected 2 / B EVENT connected_to_server / B EVENT peer_connected 1
INPROC CONNECTED: 21 ms from CreateOffer to both sides' multiplayer events (candidates A 1, B 1)
RTT client 2 -> host reliable RPC round trip: 20/20 answered; min 33.2 avg 33.3 max 33.3 ms
A(server) RPC Say recv from peer 2 (me 1): hello from 2 (reliable, ch 0)
A(server) RPC Cos (channel 1) recv from peer 2: hello from 2 (reliable, ch 1)
A(server) BURST recv from peer 2: sent 200 of each; unreliable_ordered got 200 (backwards 0), unreliable got 200
B(client 2) RPC Say recv from peer 1 (me 2): host broadcast after 2 joined (Rpc)
DROP SEEN: server peer_disconnected(2) 17 ms after client Close()      (client got server_disconnected too)
```

- Repeats connected in 20 ms and 19 ms.
- The 33 ms RTT is two frames at 60 fps: in one process, each hop waits for the next poll.
- The data channel opens, and `RpcId`, `Rpc` (broadcast) and `GetRemoteSenderId` all work, both ways.
- **Kick:** `run.ps1 -Stage kick` has the server call `DisconnectPeer(2)`:
  `KICK SEEN: client server_disconnected 17 ms after DisconnectPeer(2); server peer_disconnected fired: True`.

## Q3: two and three processes, signalling through files: PASS

The signalling is **non-trickle**, the shape of an invite code or an encrypted ntfy offer:

1. The host creates one `WebRtcPeerConnection` per guest id, calls `AddPeer(pc, id)` and `CreateOffer()`.
2. It waits for `GetGatheringState() == Complete`.
3. It writes `offer_<id>.json` atomically (tmp file, then rename). The file holds the SDP and every candidate.
4. The guest polls for that file, then `AddPeer(pc,1)`, `SetRemoteDescription(offer)` (which
   auto-creates the answer), and adds the candidates.
5. Once its own gathering completes, the guest writes `answer_<id>.json`, which the host applies.

**`run.ps1 -Stage pair`** (`runs\pair\`):

```
[host]  host->2: gathering complete 12 ms after CreateOffer
[host]  WROTE offer_2.json: offer, sdp 444 chars, 1 candidates [1x UDP v4 host]; json 580 chars, deflate+base64 544 chars
[guest] gathering complete 13 ms after the offer was read
[guest] GUEST CONNECTED: 45 ms from reading the offer to connected_to_server; 92 ms since this process's _Ready
[host]  EVENT peer_connected 2          (+97 ms after the host's _Ready)
[host]  host->2: ICE+DTLS connected 17 ms after the answer was applied
[guest] RTT client 2 -> host reliable RPC round trip: 20/20 answered; min 16.5 avg 16.6 max 16.7 ms
[host]  RPC Say / RPC Cos (channel 1) received; BURST 200/200 ordered, 200/200 unreliable
exit host 0 guest2 0
```

**`run.ps1 -Stage trio`** (`runs\trio\`: host, guest 3, then guest 2 300 ms later; server relay):

```
[host]   EVENT peer_connected 3 (+100 ms) ... EVENT peer_connected 2 (+400 ms)
[guest3] EVENT peer_connected 2               <- the host announces other guests (SceneMultiplayer relay)
[guest3] RPC Say recv from peer 2 (me 3): relayed: 2 -> 3 through the host
[guest2] EVENT peer_disconnected 3            <- and announces their departure
exit host 0 guest2 0 guest3 0
```

Multi-process runs that connected: pair, trio ×2, exitguest, killguest ×2, killhost, stun, and on
the exported exe pair and killguest. **10 of 10**, with no retries.

## Q4: time to connect, and what a drop looks like

### Connect (this machine, loopback via the LAN host candidate, `--max-fps 60`)

| Shape | Measure | ms |
|---|---|---|
| in-process, trickle | `CreateOffer` to both sides' events | 21 / 20 / 19 |
| 2 processes, no STUN | host gathering complete | 12-14 |
| | guest: offer read to answer written | 13-16 |
| | host: answer applied to ICE+DTLS connected | 16-17 |
| | guest: offer read to `connected_to_server` | 29-45 |
| | host `_Ready` to `peer_connected` | 83-116 |
| 2 processes, **STUN** `stun.l.google.com:19302` | host gathering complete | **66** |
| | guest gathering complete | **110** |
| | guest: offer read to connected | **145** |
| | host `_Ready` to `peer_connected` | 239 |
| **ENet baseline** (same spike, `-Stage enetpair`, 127.0.0.1) | `CreateClient` to `connected_to_server` | 26 |

- **RTT**: reliable-RPC round trip averages 16.5-16.7 ms for WebRTC and 16.7 ms for ENet. Both are
  frame-bound at 60 fps, so the transport adds nothing visible on loopback.
- **Engine start** to `_Ready` is about 230 ms (console wrapper start to the first log line).
- **Bundle size** (for the ntfy 4 KB message limit):
  - 1 host candidate: 580 chars of JSON, 544 deflate+base64.
  - host + srflx: 672 JSON, 600-604 deflate+base64.
  - Each extra candidate adds about 90 chars before compression.
- **Candidates here**: IPv4 only. The network profile is Public with IPv6 `NoTraffic`, and libjuice
  produced 1 host candidate (plus 1 srflx with STUN).
  - Host candidates are raw LAN IPs, not mDNS. The offer carries the LAN IP and the public IP,
    which fits the design's plan to encrypt the offer.

### Drop (host `peer_disconnected` / guest `server_disconnected`, wall clock across processes)

| How the peer went | Seen after | Runs |
|---|---|---|
| guest calls `WebRtcMultiplayerPeer.Close()` | 10 ms (pair); 3 ms and 12 ms (trio); 17 ms (in-proc) | 4 |
| guest `GetTree().Quit()` **without** `Close()` | 28 ms: a normal exit closes the connection cleanly | 1 |
| host calls `DisconnectPeer(2)` (kick) | 17 ms on the client | 1 |
| **guest hard-killed** (TerminateProcess of the engine pid) | **25.2 s, 26.1 s, 26.1 s** (the last one on the exported exe) | 3 |
| **host hard-killed** | guest got `peer_disconnected 1` and `server_disconnected` at **26.3 s** | 1 |

- **What a hard drop looks like.** Nothing happens for about 25 s: the connection state stays
  `Connected`, and no `Disconnected` or `Failed` state was ever seen, polling every frame. Then:
  - stderr prints `WARNING: rtc::impl::IceTransport::LogCallback@391: juice: Lost connectivity`
    (`at: LogCallback (src/WebRTCLibPeerConnection.cpp:55)`)
  - the multiplayer peer drops the peer
  - the connection state reads `Closed`
- **(F3)** There is no timeout knob. WebRTC has no `SetTimeout`, and the plugin's `initialize`
  reads only `iceServers` and `libdatachannel.*` (options.md). Warships today drops a silent guest
  in ≤10 s (host) and ≤12 s (guest), via `Net.cs:224/685`. To keep that, add a heartbeat RPC (for
  example 1 Hz, drop after N s) that calls `DisconnectPeer(id)`, which takes 17 ms.
  - This is the same replacement options.md already names for ENet's timeouts and RTT statistic.
    The spike confirms it is needed, and that `DisconnectPeer` does the job.
- Because libdatachannel runs its own threads, a peer whose main loop stalls (a long load) keeps
  its ICE/SCTP alive. Only a heartbeat can tell "stalled" from "fine".

### Message size (F2)

`run.ps1 -Stage blobs` plus the 2- and 3-process runs:

- A reliable `byte[]` RPC **arrives intact up to 262,140 B** of payload.
- **262,144 B, 300,000 B and 1,000,000 B never arrive.**
- For those, `RpcId` still **returns `Ok`**. The only trace is on stderr:
  `ERROR: Message size exceeds limit` / `at: _put_packet (src/WebRTCLibDataChannel.cpp:212)`.
- ENet delivered all nine sizes, 1,000,000 B included (`runs\enetpair`).
- The largest Warships RPCs are per-entity arrays (`Hub.NetRaiders`, `Yard.NetState`,
  `Hub.NetIdentity`), orders of magnitude below that. But nothing enforces the cap, and a miss
  fails silently. The port wants one guard where RPC payloads are built, or a check in the harness.
- Unreliable and ordered bursts of 200 arrived 200/200 on loopback. Loss was not simulated.

## Q5: export: PASS

`run.ps1 -Stage export`, which runs `--export-release "Windows Desktop"`: exit 0 in 4 s.

- The first attempt failed with `ERROR: Export .NET Project: ... no solution file was found at ... Spike.sln`.
  The repo already has `Warships.sln`.
- The output is `Spike.exe` (109 MB), `Spike.pck` (4 KB), the `data_Spike_windows_x86_64\` .NET
  folder, and **`libwebrtc_native.windows.template_release.x86_64.dll` next to `Spike.exe`**.
  - The pck holds `.godot/extension_list.cfg` and `addons/webrtc_native/webrtc_native.gdextension`.
  - The debug export copies `libwebrtc_native.windows.template_debug.x86_64.dll` instead, and its probe PASSes.
- **Exported release exe** (`-Exe spike\export\Spike.exe`):
  - `exp_probe`: `debug_build=False`, native class `WebRTCLibPeerConnection`, **PROBE PASS**.
  - `exp_pair`: guest connected 45 ms after reading the offer. RTT, channel 1, burst and blobs were
    all green, and the exits were `host 0 guest2 0`.
  - `exp_killguest`: hard-drop detection took 26.1 s.
- **Missing DLL, exported (F4)** (`exp_probe_nodll`, DLL renamed for one run then restored). The engine prints:
  ```
  ERROR: GDExtension dynamic library not found: 'res://addons/webrtc_native/webrtc_native.gdextension'.
  ERROR: Error loading extension: 'res://addons/webrtc_native/webrtc_native.gdextension'.
  WARNING: No default WebRTC extension configured.
  ERROR: Required virtual method WebRTCPeerConnectionExtension::_initialize must be overridden before calling.
  ```
  - The C# calls still look fine: **`Initialize -> Ok` and `CreateOffer -> Ok`**. Only
    `CreateDataChannel` returns null, and `CreateServer` also returns `Ok`.
  - So the game **cannot use return codes** to tell that the plugin is missing. Check
    `ClassDB.ClassExists("WebRTCLibPeerConnection")`, or `new WebRtcPeerConnection().GetClass() !=
    "WebRTCPeerConnectionExtension"`, once at start. That is the honest diagnostic for "the DLL
    beside the exe is missing or was quarantined" (design §A2).

---

## Other findings for the port

- **F5 · Nothing new for friends to install.**
  - The DLL depends only on Windows system DLLs, so there is no VC++ runtime to install.
  - Warships already ships a folder (`embed_pck=false` plus the `data_*` .NET folder). The DLL is
    one more file in it, 4.1 MB, and must stay beside the exe.
  - Not tested: SmartScreen or antivirus reaction to an unsigned DLL.
- **F6 · Licences.** The export copies the DLL, but **not** the `LICENSE.*` files.
  - libdatachannel and libjuice are MPL-2.0; mbedTLS is Apache-2.0 OR GPL-2.0+; usrsctp and libsrtp are BSD; plog and webrtc-native are MIT.
  - The distributed folder should carry those seven files, plus a pointer to the upstream source for the MPL parts.
- **F7 · Ids and offers are per guest.** WebRTC peer ids are chosen by the host (`CreateClient(id)`);
  ENet's were random (`2105172405` in the baseline). One `WebRtcPeerConnection`, and so one offer,
  is needed per joining guest, so the host mints an id and an offer for each join request.
- **F8 · Harness shape.** Role processes start together and the guest polls for its offer file. That is
  exactly the smoke runner's host/guest/guest2 launch, so it maps directly. Two runner details:
  - On the editor binary, `_console.exe` is a wrapper and the engine is a **child with another pid**.
    A hard-kill test must kill the pid the role logs (`OS.GetProcessId()`), not the `Start-Process` pid.
    The exported exe has no wrapper.
  - Under PowerShell 5.1 with `$ErrorActionPreference='Stop'`, any stderr line from a native
    command redirected with `*>` becomes a terminating error. The first export attempt died that way.
- **F9 · Firewall.**
  - All tests were one machine talking to itself over its own LAN IP, which Windows Firewall does
    not filter. They say nothing about a second machine.
  - `Get-NetFirewallApplicationFilter` shows Allow-Inbound (Public) rules for the Godot editor exe
    and **none for `spike\export\Spike.exe`**. A "Windows Security Alert" for Spike.exe may be
    waiting on the desktop. Cancel is safe.
  - For the real game the firewall story is the same as ENet's: both sides bind UDP. net_design.md's firewall section stands.
- **F10 · Two peers in one process work** (Q2). A future harness could run host and guest in one
  engine with a `SceneMultiplayer` per branch.

## Not tested (and why)

- **Two real machines, NAT traversal, TURN, and the owner's double NAT.** They cannot be seen from one machine.
- **Loss and latency (`-Wan`-style).**
  - The spike connects over the host candidate directly.
  - A UDP proxy would need the harness to rewrite the candidate's ip:port in the bundle. ICE should
    accept the proxy as a peer-reflexive path, but that is **unverified**.
- **A machine with no active network adapter.** libjuice gave exactly one IPv4 host candidate here
  and does not use loopback, so an offline CI box might get zero candidates.
- **IPv6.** This network has none.
- **The GUI editor opening the project** (only headless `--editor --quit-after` was run: exit 0).
- **The DLL under a real antivirus or SmartScreen.**

## Files

- `spike\proj\`: the project.
  - `Main.cs`, `Pinger.cs`, `project.godot`, `Spike.csproj`, `Spike.sln`, `nuget.config`, `export_presets.cfg`
  - `addons\webrtc_native\`: gdextension, 2 DLLs, 7 licences
- `spike\run.ps1`: the stage runner.
- `spike\tools\peimports.py`: the PE import lister.
- `spike\export\`, `spike\export_debug\`: the exported builds.
- `spike\control\`: the no-addon control used for the import-crash comparison (it has the addon now).
- `spike\runs\<tag>\`: every run's logs.
