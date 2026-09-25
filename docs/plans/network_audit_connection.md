# The connection path, end to end: how a real attempt fails

Read-only audit of `scripts/Net.cs`, `scripts/Router.cs`, `scripts/SessionMenu.cs`, `scripts/Hints.cs`,
`scripts/Game.cs`, `play.ps1`, `tools/install.ps1`, `project.godot`, docs. Nothing was built or run.
Line numbers are HEAD 242b1aa plus the working tree as of this read.

---

## 0 · The owner's report, decoded

**The owner's network is on record.** `docs/CHANGES.md:1642-1650`: a TP-Link (UPnP on) behind the
provider's cable modem (192.168.4.1, silent). The TP-Link's internet side is 192.168.4.40, which is
**double NAT**.

On that network, `Router.Open` (`Router.cs:61-111`) opens the TP-Link, sees that its external address
is private (`Router.cs:95`), then asks 192.168.4.1 and .254 (`FrontCandidates`, `Router.cs:122-128`)
by unicast UPnP and NAT-PMP/PCP. It gets silence and sets `Front`, so `Net.Describe` returns
**MANUAL** (`Net.cs:417-421`). This is what the host saw:

> Hosting. Your router opened port 27015, but it sits behind a second router (usually your internet
> provider's modem) that did not answer. Once, in THAT router's settings: forward UDP 27015 to
> 192.168.4.40 -- or put 192.168.4.40 in its DMZ, or switch it to bridge mode. After that, friends
> anywhere join the address below, every time. If friends still cannot get in, allow Warships (Godot)
> through the Windows firewall, for private AND public networks.

If the TP-Link refused that day, the return comes before the front step (`Router.cs:85-89`), and the
host sees `Net.cs:446-449` instead: *"Hosting. Your router did not open port 27015 -- the router
refused (…), and it sits behind a second router. Forward UDP 27015 to this PC … and in the router in
front of it forward UDP 27015 to 192.168.4.40 …"*. With UPnP off on a single router, the host sees
`Net.cs:451-454` (*"Your router did not open port 27015 -- no router answered"*, why from `Router.cs:87`).
All three fit the owner's words, "the port was not available on my end".

A less likely candidate for the owner's words is `Net.cs:535`: *"Could not open port 27015: another
program, or another copy of Warships, is using it. Playing offline."* That one is a bind failure. It
can be told apart easily: it ends with "Playing offline", and the panel's button then reads
`MULTIPLAYER (offline)`, not `(hosting · 1)`.

**The guest** hits the 12 s hard deadline (`Net.cs:115`, `JoinTimeoutMs` at `Net.cs:289`) or ENet's
`ConnectionFailed` (`Net.cs:97`). Either one produces `CouldNotReach` (`Net.cs:291-292`):

> Could not reach 1.2.3.4. Check the address; if the host plays from home, their MULTIPLAYER panel
> says what their router still needs. Playing offline.

This matches the guest's "unavailable / port not open".

**The owner handed out a dead address because the game offered it.** In MANUAL the address is still
published: `InternetAddress` is set for Internet *and* Manual (`Net.cs:472`). The panel shows
*"Friends elsewhere join: <public>:27015"* and **COPY ADDRESS** exactly as it does in the working case
(`SessionMenu.cs:63-70`, `100-111`). The first-run hint says *"HOST THIS WORLD, then COPY ADDRESS for
your friends"* (`Hints.cs:23`). The game knows the port is closed and still presents the address as
live. The explanation is a ~400-character paragraph in a 320 px label inside a folded panel
(`SessionMenu.cs:57-60`). Any later status line overwrites it (`Say`, `Net.cs:699`), for example
"Player 2 left."

---

## 1 · Ranked: why a two-machine session has never worked

These are walls in series: each one hides the next. The first three will each stop the owner in turn,
in this order.

| # | Failure | Where | Host sees | Guest sees | Likelihood it is THE reason |
|---|---|---|---|---|---|
| 1 | **Host port never reaches the internet**: double NAT with a silent front router (the owner's network), UPnP off, or the router refused | `Router.cs:61-111`, `Net.cs:417-421 / 446-449 / 451-454` | MANUAL text above, plus a revealable, copyable public address | `CouldNotReach` after 12 s | **Certain** for the owner's tests to date. No code path works until someone logs into the provider's gateway. |
| 2 | **Windows Defender Firewall drops the guest's first packet on the host** | no rule anywhere: no `netsh`/`New-NetFirewallRule` in the repo; `install.ps1` only extracts. The prompt fires at `CreateServer` (`Net.cs:533`) | **Nothing**: "Hosting for the internet …" stays up. ENet never sees the packet. | `CouldNotReach` after 12 s | **Very high as the second wall.** It also blocks LAN play. Details below. |
| 3 | **Build mismatch at the handshake** | `Net.cs:145-159`, fingerprint `Net.cs:161-188`; `play.ps1:30-53` | `Net.cs:154` "Refused a player on a different build of the game (theirs x, this one y)." Shown only in the folded panel. | `Net.cs:157-158` "That host is on a different build of the game (theirs x, yours y): you both need the same zip. Playing offline." | **Near-certain as the third wall** while the owner hosts via PLAY.bat. Details below. |
| 4 | **Self-test by hairpin**: the host joins its own public address from inside the house | nothing warns about it | nothing | `CouldNotReach` | High as a *misleading* result. Most routers do not loop back, and through two NATs almost none do. The only tests on record are 127.0.0.1 (`play.ps1:74-79`, `docs/README.md:281-283`), which bypass every item here. |
| 5 | **CGNAT** (mobile hotspot, 5G/fixed-wireless home internet, Starlink, some fibre) | detected only when a router *answers* with 100.64/10 (`Router.cs:142`, `Net.cs:441-444`, `435-438`) | right answer when detected. **Misdiagnosed** in two cases: router silent + CGNAT gives MANUAL "forward UDP 27015 in your router" (`Net.cs:451-454`), which cannot work. CGNAT on a 10.x/172.16/192.168 pool looks like "a second router" (`Router.cs:95-110`), so the player is told to forward on the ISP's box (`Net.cs:417-421 / 446-449`). | `CouldNotReach` | Medium overall. Low for the owner (cable is usually public), but unproven: compare the modem's WAN IP on its status page with the revealed address. If they differ, it is CGNAT, and no forward will ever work. |
| 6 | **"Success" that does not forward** | never checked from outside. Also `Map` deletes a conflicting 27015 mapping and takes it (`Router.cs:310-312`), and 27015 is Valve's default server port. | `Net.cs:424-428` "Hosting for the internet: friends anywhere join …", which is false | `CouldNotReach` | Low-medium. Some routers accept `AddPortMapping` and do nothing. |
| 7 | **Host behind a VPN** | detected only when UPnP opened (`Net.cs:429-434`) | router silent + VPN gives MANUAL with the **VPN exit IP** as the friends' address (`Net.cs:451-454`, from `publicIp`). That address is dead even after a correct forward. | `CouldNotReach` | Low-medium |
| 8 | **The IPv6 offer** | address appended and revealed (`Net.cs:522`, `SessionMenu.cs:108`), `Router.GlobalIpv6` (`Router.cs:196-210`) | "Friends with IPv6 internet can also try your IPv6 address" | `CouldNotReach` | High that it fails *when tried*. No pinhole is ever requested (no IGDv2 `WANIPv6FirewallControl`, no PCP-v6 in `Router.cs`). Consumer routers block unsolicited inbound IPv6 by default, and the Windows rule in #2 applies to v6 too. A name that has only AAAA records gets "Could not find" because DNS keeps IPv4 only (`Net.cs:616`). |
| 9 | **Wrong address typed** | `Net.ParseAddress` (`Net.cs:567-589`) | nothing | `Net.cs:599` "… is not an address. Type the host's IP, or IP:port." / `Net.cs:629` "Could not find X. Check the address." (DNS or `CreateClient` failed) / `CouldNotReach` | Medium for strangers. In LAN-ONLY, COPY ADDRESS falls back to the **LAN** address (`SessionMenu.cs:67`), which is useless to a remote friend. The reveal is a Button, not selectable text, so an IPv6 address has to be retyped by hand. |
| 10 | **UDP 27015 cannot be bound** | `Net.cs:533-537`; fixed port, no port field (`SessionMenu.cs:51`) | `Net.cs:535` "Could not open port 27015: another program, or another copy of Warships, is using it. Playing offline." | `CouldNotReach` | Low. Causes: a `-Two` window, a copy still quitting (minimised for up to 10 s, `Game.cs:73,89`), a Source-engine server, or a Windows excluded port range (Hyper-V/WSL/Docker, only if the dynamic range was moved). There is no fallback port. |
| 11 | **Windows will not run it**: SmartScreen, Smart App Control, or AV heuristics on an unsigned Godot export | `docs/README.md:125-146` | – | – | Low-medium for strangers. The attempt ends before it starts. |
| 12 | **Wi-Fi client isolation** (guest SSIDs, dorms, public Wi-Fi) | – | nothing | `CouldNotReach` | Low. It only affects LAN play. |
| 13 | **Guest on an IPv6-only mobile network** (NAT64) typing an IPv4 literal | `Net.cs:612` | nothing | `CouldNotReach`, unless the OS has CLAT | Low |

### #2 in detail: the Windows Firewall wall

- **The prompt fires on the first `CreateServer`**, which binds UDP 27015 on the wildcard
  (`Net.cs:532-533`). Microsoft's own rule for that prompt: *"If they respond No or cancel the prompt,
  block rules are created"*, and a non-admin user gets block rules whatever they click. Those rules
  then outrank any allow rule until someone deletes them.
- **The prompt is probably hidden.** The game starts borderless fullscreen (`project.godot:26-27`,
  `window/size/mode=3`, `borderless=true`). A dialog from another process usually lands *behind*
  that window and only flashes in the taskbar. This is typical Windows behaviour, not verified on
  this machine. Until someone answers, inbound traffic is dropped.
- **Source and release get separate rules.** From source the program is the Godot mono exe (the
  prompt says "Godot Engine"). The release is `play\Warships.exe`. Firewall rules are per full path,
  so each exe prompts separately, and so does every new install folder.
- **The firewall hint is incomplete.** It exists only in the Internet/Manual texts (`Net.cs:402`,
  used at 421/425/428/449/454). The LAN-ONLY texts (`Net.cs:435-438`, `441-444`, `455-457`) never
  mention it, yet LAN and overlay play (Tailscale/Radmin adapters are often on the Public profile)
  need the same rule.
- **What each side sees.** The host sees nothing. The guest sees `CouldNotReach`, which tells them to
  check the host's router, and that is the wrong advice.

### #3 in detail: the build-mismatch wall

- **The owner and the friend run different builds.** On the owner's machine PLAY.bat runs FROM
  SOURCE, because Godot mono and `dotnet` are both present (`play.ps1:30-49`, `dotnet build` at
  `:65`). The friend has neither, so they get `releases/latest` (`play.ps1:53`, `install.ps1:33`).
- **The builds are far apart.** The latest release is `2026-09-23.2ea5a0a` (published
  2026-09-24T03:44Z, per the GitHub API). HEAD is **18 commits ahead**, and the working tree differs
  from that release in **25 files under `scripts/`**. 11 of them are uncommitted right now.
- **Any of those changes flips the fingerprint.** It covers every RPC signature and every static
  constant, table row and class sheet (`Net.cs:170-188`), so the connection is refused on both
  sides.
- **Source vs release at the same commit is probably equal, but never tested.** There is no
  `#if`/`IsDebugBuild` in `scripts/`. The compiler-generated types that differ between Debug and
  ExportRelease (async state machines, display classes) carry no fingerprinted static fields. Every
  harness still runs N copies of *one* build.
- **The refusal wording is stale.** "you both need the same zip" is left from before PLAY.bat and
  install.ps1.
- **The fingerprint is never shown before connecting.** EscMenu shows `Game.Build` only
  (`EscMenu.cs:51-52`), and that reads "dev" from source.
- **A local test can settle it.** On one machine: host from source, then join `127.0.0.1` from
  `play\Warships.exe` (run `tools\install.ps1` directly). No network is involved, and the refusal,
  if any, prints both fingerprints.

---

## 2 · Every string on the path, and what produces it

**Host**

| When | Text | Code |
|---|---|---|
| HOST pressed | "Hosting on your network (192.168.x.y:27015). Asking your router to open port 27015, and the internet for your public address..." | `Net.cs:545` |
| bind failed | "Could not open port 27015: another program, or another copy of Warships, is using it. Playing offline." | `Net.cs:535` |
| every hop opened | "Hosting for the internet: …join the address below (click to reveal, or copy it)." + firewall suffix | `Net.cs:424-428`, `402` |
| double NAT, front silent | text in §0 | `Net.cs:417-421` |
| VPN (only when mapped) | "…this PC's internet traffic goes out another way -- usually a VPN…" | `Net.cs:432-434` |
| mapped, CGNAT | "Hosting for your network (…). Your router opened the port, but your provider puts you behind a shared address (carrier-grade NAT)…" | `Net.cs:435-438` |
| not mapped, CGNAT | "…no amount of port forwarding lets friends reach you directly. Use a virtual network…, or let a friend host." | `Net.cs:441-444` |
| not mapped, double NAT | "Your router did not open port 27015 -- {why}, and it sits behind a second router…" | `Net.cs:446-449` |
| not mapped, single router | "Your router did not open port 27015 -- {why}. Forward UDP 27015 to this PC…" | `Net.cs:451-454`; why = "no router answered" / "the router refused (…)" `Router.cs:87` |
| nothing learned | "…Neither your router nor the internet could be asked for your public address…" | `Net.cs:455-457` |
| appended if not INTERNET | " Friends on your {Tailscale…} network join {ip}:27015." / " Friends with IPv6 internet can also try your IPv6 address (in the panel)." | `Net.cs:521-522` |
| reveal button | "Friends elsewhere join: •••.•••.•••.•••   (click to reveal)" → "…: {v4}   ·   IPv6 {v6}" | `SessionMenu.cs:103-111` |
| different build knocks | "Refused a player on a different build of the game (theirs …, this one …)." | `Net.cs:154` |
| guest in | "Player N joined." | `Net.cs:687` |

**Guest**

| When | Text | Code |
|---|---|---|
| empty / garbage box | "Enter a host address first." / "\"x\" is not an address. Type the host's IP, or IP:port." | `Net.cs:599` |
| JOIN | "Connecting to {host} ..." (HUD: "CONNECTING", `Hub.cs:1525`) | `Net.cs:609` |
| name did not resolve / client not created | "Could not find {host}. Check the address. Playing offline." | `Net.cs:629` |
| no answer in 12 s (ports, firewall, CGNAT, hairpin, wrong IP) | `CouldNotReach` text in §0 | `Net.cs:291-292` via `97` / `115` |
| link up then dropped mid-handshake | "Could not get in to {host}: it let the connection go before the handshake finished. Playing offline." | `Net.cs:307` |
| different build | "That host is on a different build of the game (theirs …, yours …): you both need the same zip. Playing offline." | `Net.cs:157-158` |
| in | "Connected as player N." | `Net.cs:225` |

`CouldNotReach` covers seven different causes: #1, #2, #4, #5, #7, #8 and #12. The guest has no way to
tell them apart, and the host sees nothing for any of them.

---

## 3 · What the ladder can and cannot see here

- **What the smoke test covers.** Every connection runs on 127.0.0.1 (`SmokeTest.cs.txt:117-118`),
  the routers are fake (`Router.Fake`, `fakeigd.py`), and `-Wan` is a loopback relay (`wan.py`). The
  `Describe` branches are unit-checked (`SmokeTest.cs.txt:3189-3226`), and so is the refusal
  (`PretendProtocol`, `:7055`).
- **What it cannot see.** No rung can see #1-#13. There are no real firewalls, real NATs, hairpins,
  IPv6, DNS, or cross-build pairs. A green rung 5 says nothing about whether two homes can connect.
- **The cheapest real checks.** (a) The same-machine source-vs-release join from §1 #3, which covers
  the build wall. (b) One remote friend, once the owner's modem forwards 27015, which covers the
  firewall and the path.

---

## 4 · What this means for the design (connection-path facts only)

1. **"The host cannot open a port" means nobody may need unsolicited inbound traffic.** Both ends
   must dial OUT, either to each other (hole punching, via a rendezvous) or to a relay. A relay
   removes #1, #2, #5 and #7 at once. Hole punching removes #1 and #2 on endpoint-independent NATs
   (most home routers, and RFC 6888 requires it of CGNs). It fails against symmetric NAT and some
   mobile carriers, so a relay is the floor.
2. **Solicited traffic also clears #2.** When the host sends first to the guest's exact public
   endpoint, or holds a flow open to a relay, the guest's packets are *replies* to Windows Firewall,
   so no inbound rule or prompt is involved. Standard WFP flow behaviour; verify on a real machine.
3. **Godot's ENet already has the punch primitives.** `ENetConnection.SocketSend(addr, port, bytes)`
   is documented for exactly this purpose ("establish entries in NAT routing tables … won't work for
   clients behind Symmetric NAT"). `ENetMultiplayerPeer.CreateClient(addr, port, 0, 0, 0, localPort)`
   pins the guest's source port. ENet cannot read a STUN reply, so the public endpoint has to be
   learned another way: a plain .NET socket on the port *before* ENet binds it, or a rendezvous that
   records the source address of a `SocketSend` registration.
4. **A transport swap touches every ENet-specific call.** `Net.cs` talks to `ENetMultiplayerPeer`
   directly at `155`, `223`, `279-282`, `633`, `653-658`, `684` and `718-719` (SetTimeout,
   PeerDisconnectLater, Host.GetPeers, the RTT statistic). WebRTC, EOS or Steam would need these
   behind an interface. A relay that forwards raw ENet datagrams would not.
5. **The relay's shape is already proven in the repo.** `tools/smoketest/wan.py` (61 lines) is a UDP
   relay that ENet runs through unchanged, with one upstream socket per guest. The rung-5 `-Wan` runs
   pass through it. What is missing is a public host and code-keyed registration. The doc's "needs
   infrastructure" (`DESIGN.md:232`) is the real cost: a free-tier VM or a ~$5/mo VPS.
6. **Put the build check before the network.** Carry `Net.Protocol` in the join code or rendezvous
   record, so a mismatch is named instantly rather than after the router is fixed. Stop PLAY.bat from
   silently hosting a different build from the one friends install, or show the fingerprint on the
   panel.
7. **Join by code, not IP.** Typing an IP is the source of #9, and the IP is already treated as
   secret (click-to-reveal).
8. **Tell the truth about the address.** Do not offer COPY ADDRESS or "Friends elsewhere join" in
   MANUAL until a path is proven. Probing from outside needs the rendezvous anyway.
9. **Small fixes worth carrying:**
   - fall back to another port when the bind fails (#10);
   - put the firewall hint in every branch (#2);
   - have `Describe` compare `routerExt` with `publicIp` in the unmapped branches (#7);
   - treat "router silent + public IP" as "maybe CGNAT", not as a certainty that a forward works (#5);
   - resolve AAAA records too (#8);
   - if IPv6 stays, request a pinhole (IGDv2 `AddPinhole`/PCP) or stop advertising it;
   - the front modem may look "silent" because many IGDs ignore *unicast* M-SEARCH (UDA 1.0). Fetching
     the common description URLs on 192.168.4.1 directly would tell "UPnP off" from "UPnP on but
     deaf". (`Router.cs:100` is the unicast search.)

---

## 5 · Unblocking today, with no code

1. **The owner's own network**, as the game already says:
   - in the provider gateway (192.168.4.1), forward UDP 27015 to 192.168.4.40, or put 192.168.4.40 in
     its DMZ, or switch it to bridge mode;
   - allow the exe that will host through Windows Firewall, Private + Public;
   - host from `play\Warships.exe` (`tools\install.ps1`), **not** PLAY.bat from source, so both ends
     run the same build;
   - test from a phone hotspot or a friend, never by joining your own public IP.
2. **Swap roles.** The friend hosts if their router is a single NAT with UPnP on. Outbound from the
   owner's double NAT is fine.
3. **A virtual network on both machines** (Tailscale, ZeroTier, Radmin VPN). The game already detects
   one and prints its address (`Router.cs:175-191`, `Net.cs:521`). Both machines still need the
   firewall rule.
4. **A playit.gg UDP tunnel on the host only.** The guest types the playit `name:port`, and
   `Net.cs:612-619` resolves names off the main thread. 2026 reviews say generic UDP is still on the
   free tier, but confirm in the dashboard.

Sources: [Microsoft: Windows Firewall rules](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/rules) ·
[Godot: ENetConnection](https://docs.godotengine.org/en/stable/classes/class_enetconnection.html) ·
[GitHub API: MP_Space_Game releases](https://api.github.com/repos/ArmosCodesStuff/MP_Space_Game/releases?per_page=5) ·
[Pinggy: playit.gg alternatives 2026](https://pinggy.io/blog/best_playit_gg_alternatives/) ·
[space-node: playit.gg free plan and limits](https://space-node.net/blog/playit-gg-free-minecraft-server-guide-2026)
