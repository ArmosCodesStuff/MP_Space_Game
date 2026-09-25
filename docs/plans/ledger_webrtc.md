# Ledger: WebRTC slices (docs/plans/network_webrtc.md)

CLAUDE.md §2b rule 3. Every job writes a PRE before it touches anything and a POST when finished.
A PRE with no POST is an INTERRUPTED job: compare its files with the recorded hashes, revert its
half-made edits (or keep those that match the plan exactly), then run it again.

## R0 · the plugin inside Warships (plan §13, §10.4 R0)

Environment of this batch: Linux cloud container. Rung 1 only (`typecheck/typecheck.sh`, real
GodotSharp.dll, scripts + both harness files). No Godot, no PowerShell: rungs 2-6 run later on the
owner's PC. The plugin zip could not be downloaded, so NO binaries are vendored here.

### Decisions where the plan leaves a gap (default taken, go on)

- D1 · **v1 is not in the repo.** `git log` knows only v2 (`f2b822e`); v1 lived in the owner's
  scratchpad (`scratchpad\webrtc\rtc_design.md`). What v2 cites from v1 §7, §8.1, §8.2, §11.5 R0 was
  rebuilt from v2's own restatements (§2.1, §7, §8, §10.4) and the spike record. Where v1 held a
  literal this batch could not read, the default below is used and marked.
- D2 · **Plugin-missing text** (v1 §6.1, unread): `Link.MissingText`, naming the DLL from the
  `Link.Plugin` row, saying an antivirus may have quarantined it and that PLAY.bat restores it, and
  that offline play works. Replace with v1's literal if the owner has it.
- D3 · **The one-DLL experiment** (v1 §8.1, unread), read as: can the editor/debug build load the
  RELEASE DLL, so the repo vendors one DLL instead of two? Built as `run.ps1 -OneDll`: the scratch
  copy's `.gdextension` points its debug line at the release DLL and the debug DLL is deleted from
  the copy; a green solo run (`the plugin loaded` + the pair checks) says one DLL is enough.
- D4 · **The reply-window measurement is opt-in** (`run.ps1 -ReplyWindow`, which is solo-only):
  the late pairs are expected to fail, and the plugin turns library errors into engine `ERROR:`
  lines (v1 S9) that fail an ordinary run. In that mode `ERROR:` lines are printed as expected, not
  counted. `Link.ReplyWindowS` is NOT added in R0 (no caller until the edit that sets it, which also
  replaces the measurement with the permanent check, §3.4).
- D5 · **"An RPC on the highest row arrives" is proved with a raw packet** on that channel through
  `WebRtcMultiplayerPeer.PutPacket`/`GetPacket`: a harness node other than `_Test` has no script
  path (only `_Test.cs` matches its file), so SceneMultiplayer could not route an RPC to it, and
  `_Test` itself is the one autoload. The transport fact (the row's data channel opens and carries
  a packet with that channel number) is the same; R2's rate check proves an RPC on it at rung 5.
- D6 · **Runner guard:** `tools/import.ps1 -Require` refuses a run when a required extension is not
  in the folder, when an extension names a library file that is not there (this is also what
  enforces the `.gdextension` trim to the two Windows x86_64 lines), or when the import did not
  register it. Both runners require `res://addons/webrtc_native/webrtc_native.gdextension`.
- D7 · GodotStub gains nothing in R0 (plan §7 puts it in R2); rung 1 here and on the owner's PC uses
  the real GodotSharp.dll.

### What the owner must drop in (cannot be downloaded here)

From webrtc-native **1.2.1** release `godot-extension-webrtc_native.zip`
(sha256 `f37d03da03da3ff0d092542a04586644f889135cb7a1c3566ad57513203a553b`), into
`addons\webrtc_native\`:
- `webrtc_native.gdextension`, with every line under `[libraries]` deleted except the two
  `windows.*.x86_64` ones (import.ps1 refuses any line whose file is absent);
- `lib\libwebrtc_native.windows.template_debug.x86_64.dll` (4,098,560 B, sha256 `706ba39c…07a3`);
- `lib\libwebrtc_native.windows.template_release.x86_64.dll` (4,124,672 B, sha256 `bf9d09a5…0476`);
- the 7 `LICENSE.*` files.
Then: `tools\smoketest\run.ps1 -Solo` (rung 3), `-Solo -OneDll` once, `-ReplyWindow` once
(record its `reply window` line in DESIGN.md; under 15 s stops the batch, §3.4).

### Jobs

- J1 Link.cs core + StreamChannel move + the pair checks (Channels, Sealed, Hang, lifetime, Close/DisconnectPeer, main thread, ObjectCount)
- J2 SessionMenu's plugin-missing gate + its check
- J3 the reply-window measurement (harness, opt-in)
- J4 tools/import.ps1 + both runners calling it (-Require guard, -OneDll, -ReplyWindow)
- J5 records (CHANGES Handoff + Unreleased + Known broken; this ledger)

#### J1 PRE
- intent: new `scripts/Link.cs` (Plugin row, Available, Channels(), Sealed, Hang); harness
  `StreamChannel` 200 -> `NetChannels.BossSounds + 1`; the R0 pair checks in the solo run.
- files: scripts/Link.cs (new), tools/smoketest/SmokeTest.cs.txt
- from: 57e2c26ddb8c996e97767258e248070f31e367b6
- hashes: SmokeTest.cs.txt 671205d802d89218de55fac3beef9684692cee08; Link.cs absent

#### J1 POST
- verdict: done; rung 1 green (0 errors, 0 warnings from csc); xref UNUSED ANYWHERE 0. Untested at rung 3.
- files: scripts/Link.cs (new), tools/smoketest/SmokeTest.cs.txt (StreamChannel, RtcPair, RtcUntil,
  RtcChannels, WebRtcPairs called just before the solo leak check)
- checkpoint: 39df356
- next: J2

#### J2 PRE
- intent: SessionMenu's plugin-missing gate (HOST and JOIN, button, Enter and handler) with
  `Link.MissingText` (D2) shown in the panel; `Link.PretendMissing` seam; a solo check of the gate.
- files: scripts/Link.cs, scripts/SessionMenu.cs, tools/smoketest/SmokeTest.cs.txt
- from: 39df356
- hashes: Link.cs 073ecfe49c2afbaefed5c656d645e60c2391fe5b; SessionMenu.cs 78118dbfc110c28032abd5d191dd411839b3b793;
  SmokeTest.cs.txt 625c6d598822ce243f925a9292f9c5077e8c0b92

#### J2 POST
- verdict: done; rung 1 green, 0 warnings, UNUSED ANYWHERE 0. Untested at rung 3.
- files: scripts/Link.cs (MissingText, PretendMissing), scripts/SessionMenu.cs (gate + PluginMissing label),
  tools/smoketest/SmokeTest.cs.txt (gate check after "offline, there is no RECONNECT")
- checkpoint: f70a5bb
- next: J3

#### J3 PRE
- intent: the one-time reply-window measurement (§3.4): seven pairs started at the top of the solo
  run when the role gets `replywindow`, host applying the reply 5/10/15/25/35/45/60 s after it was
  made; judged before the leak check; prints `  reply window: ...` and checks the window >= 15 s.
- files: tools/smoketest/SmokeTest.cs.txt
- from: f70a5bb
- hashes: SmokeTest.cs.txt ec361f2c82d3a52bda6472fb55eb2d6cf2249b82

#### J3 POST
- verdict: done; rung 1 green, 0 warnings. Untested (needs the plugin and rung 3 -ReplyWindow).
- files: tools/smoketest/SmokeTest.cs.txt (ReplyDelaysS, _replyWindow, MeasureReplyWindow, judged at the top of WebRtcPairs)
- checkpoint: d777dec
- next: J4

#### J4 PRE
- intent: new tools/import.ps1 (import judged by what it registered, SPIKE F1 re-import, -Require,
  every named library present = the trim rule); tools/smoketest/run.ps1 and tools/screens/run.ps1
  call it; run.ps1 gains -OneDll (D3) and -ReplyWindow (D4).
- files: tools/import.ps1 (new), tools/smoketest/run.ps1, tools/screens/run.ps1
- from: d777dec
- hashes: smoketest/run.ps1 35a6d10c0761475c1fe6f318f36c41f0cdaa94b3; screens/run.ps1 54762a3dc645dd6c978b764ca1edae0c659a5696; import.ps1 absent

#### J4 POST
- verdict: done; PowerShell not available here, so the scripts are read-checked only (no pwsh).
  Rung 1 green. Non-ASCII removed from the .ps1 files (PowerShell 5.1 reads them as ANSI).
- files: tools/import.ps1 (new), tools/smoketest/run.ps1, tools/screens/run.ps1
- checkpoint: 2952468
- next: J5

#### J5 PRE
- intent: records: CHANGES Handoff state note at the top, an Unreleased entry, Known broken.
- files: docs/CHANGES.md, docs/plans/ledger_webrtc.md
- from: 2952468
- hashes: CHANGES.md 3895a96d393d8db7b8ab29704e99da5eb3a67d3b

#### J5 POST
- verdict: done. CHANGES: Handoff note at the top (what landed, what to drop in, rungs to run),
  the step-1 note's NEXT updated, an Unreleased entry with its Checks and Known broken (R0).
- files: docs/CHANGES.md, docs/plans/ledger_webrtc.md
- checkpoint: the J5 commit (records)
- next: the owner vendors the plugin, then rung 2, rung 3 x2 seeds, -OneDll, -ReplyWindow;
  DESIGN.md gets the measured window; then R1.

#### J6 PRE (local session, 2026-09-24)
- intent: the R0 engine rungs on the owner's PC, fixing what they find.
- files: verify.ps1, tools/smoketest/SmokeTest.cs.txt, docs/CHANGES.md, docs/DESIGN.md, this ledger
- from: 2abb715

#### J6 POST
- verdict: done, R0 green. `-Quick` green; `-Solo` seeds 90331 and 4127 green; `-Solo -OneDll` green;
  `-ReplyWindow` green: every delay 5-60 s connected 4-6 ms after the reply, guest Connecting at
  each, so `Link.ReplyWindowS` = 30.
- found and fixed on the way: verify's text step read the DLLs as text (hung 28 min; 21c538c);
  the pending-entry check freed a guest mid-DTLS-handshake (two plugin ERROR lines, 1 run in 2; its
  invite is no longer delivered); the Warden's hunters flew on into the Echo blast check (seed 90331,
  136 where 80; the hunters case intercepts its own now).
- next: the bar (rung 6), a push to both branches, then R1 (plan §13). R1's first edit adds
  `Link.ReplyWindowS = 30` and turns the one-time measurement into the permanent check (§3.4).

## R1 · the codes and the rows (plan §13, §10.4 R1)

Environment: the owner's PC, worktree `WarShips_wt_net` (branch `wt/net`, from 03d47ba). Compile
rungs only (typecheck, `verify.ps1 -Quick`), each started only while no Godot process runs: a
verification run is going in the main checkout and shares `%TEMP%\warships_smoke`. No engine rung in
this batch; the main session runs rungs 3 (and later 5) from the list at the end of this section.
Owner defaults: clipboard pickup YES; the 90 s hold KEEP; UPnP DELETE (in R2, not here). No
third-party infrastructure but the Google and Cloudflare STUN rows.

### Jobs (split so each shares its files)

- J7 `Link.ReplyWindowS` = 30 and the permanent reply-window check (the measurement and
  `run.ps1 -ReplyWindow` deleted)
- J8 `scripts/Rendezvous.cs`, the codec half: records and `Pack`/`Unpack` with the check, `Sdp`
  (template, `Strip`, `Build`), the text (`WSI`/`WSR`, Crockford), `Fit`, `Paths` and `Claims`, the
  clipboard seam; its checks (codec, byte for byte on the live pair, text, paths, fit, mutability)
- J9 the rest of `Link.cs` (`Servers`, `StunFirst`, `Config`, the walk, `GatherMs`, `LinkMs`,
  `InviteLifeS`, `Backlog`, `ChannelOf`) and the box (`wan.py` rewritten, started by run.ps1 for every
  run); the walk, sealing, backlog and channel checks
- J10 the rows end to end: the pending table, the listener and dialer, the paste row's pickup, the
  courier (files, and rewriting codes for the box); the address-row, paste-row and pair-proxy checks
- J11 records: CHANGES (R1 Unreleased, Known broken, Handoff), DESIGN, this ledger's rungs owed

#### J7 PRE
- intent: `Link.ReplyWindowS = 30` (the R0 measurement, DESIGN.md); the one-time seven-pair
  measurement replaced by the permanent check (one pair, the host applying the reply
  `ReplyWindowS` after the guest made it, connected within 2 s of that; started at the top of every
  solo run, judged in `WebRtcPairs`); `run.ps1 -ReplyWindow` and its `replywindow` argument deleted.
- files: scripts/Link.cs, tools/smoketest/SmokeTest.cs.txt, tools/smoketest/run.ps1, docs/DESIGN.md
- from: 03d47baa504be57aad842eccdfff0b49218623bc
- hashes: Link.cs bbbd24edad2809221b5142ccf4707d402ff325d1; SmokeTest.cs.txt
  00e6f1badf3c7f5922b1134e0a9f9cb1bce834e9; run.ps1 0b37ee28966a97c067047e35fb50a6b6d396884a;
  DESIGN.md 8d15c0e26f08e71e8b96c10151871587e72eea15

#### J7 POST
- verdict: done; rung 1 green (0 errors, real GodotSharp.dll). Untested at rung 3.
- files: scripts/Link.cs (`ReplyWindowS`), tools/smoketest/SmokeTest.cs.txt (`ReplyWindowHolds`
  started at the top of every solo run, judged first in `WebRtcPairs` with the literal 30 check;
  `ReplyDelaysS`/`MeasureReplyWindow` deleted), tools/smoketest/run.ps1 (`-ReplyWindow` deleted),
  docs/DESIGN.md (the reply-window entry)
- checkpoint: 6eef84e
- next: J8

#### J8 PRE
- intent: new `scripts/Rendezvous.cs`, the codec half (§4): the four records, `Pack`/`Unpack` with
  the 4-byte check, `Sdp` (the 17-line template read off SPIKE `runs\stun`, `Strip`, `Build`, the
  candidate line), the text (`WSI`/`WSR`, Crockford base32, the self-delimiting forgiving `Find`),
  `Fit` (the /64 rule, then rank), `IRendezvousPath` with `Id`/`Claims`/`Auto`/`Stun`/`Budget`/
  `Measure` and `Paths = {Paste, Address}`, the clipboard seam; the J8 checks in the solo run.
- files: scripts/Rendezvous.cs (new), tools/smoketest/SmokeTest.cs.txt, docs/plans/ledger_webrtc.md
- from: 6eef84ef5e9ef64118927d17bef5081b121173bf
- hashes: SmokeTest.cs.txt d48a10c19ad3b63856058e58c95f4eb3d323e516; Rendezvous.cs absent

#### J8 POST
- verdict: done; rung 1 green; `-Quick`'s build, analysers (0) and text green, xref 0 after the
  wire-values check named `Why.Full`/`Why.Closed` (the listener, J10, uses them too). Untested at rung 3.
- files: scripts/Rendezvous.cs (new: records, codec, `Sdp`, text, `HoldsCode`, `Fit`, the two rows,
  `Paths`/`PathFor`, `Clipboard`); tools/smoketest/SmokeTest.cs.txt (`RtcPair.Offer`/`Answer`/
  `OfferLines`/`AnswerLines`; the spike's bundles as literals; `Said`, `VaryRecord`, `ThroughCodec`,
  `Refusal`, `Codes()` run at the top of `WebRtcPairs` with no plugin needed; the live-bundle check
  after pair `a` connects)
- decisions:
  - D8 · **The spike's real bundles are harness literals**, the owner's internet address replaced by
    203.0.113.7 (TEST-NET-3), so the byte-for-byte check runs on every machine (plugin or not) as
    well as on the live pair's own bundles.
  - D9 · **Names clip to 16 bytes, build ids to 24** (§4.1's bullet "clipped to 16 bytes"; the
    table's "1+n <= 16" read as the field's text, not its length byte). The shared name rule's "empty
    -> the default name" stays R2's (it lands with the rule's move out of `Hub.NetIdentity`).
  - D10 · **An IPv6 non-host candidate's tail is `raddr :: rport 0`** (RFC 8839); libjuice's own is
    unread (§4.1 says so). Only an IPv6 server-reflexive would show it, and no network here has one.
  - D11 · **Candidate lines are strict like SDP lines**: anything but `raddr x rport y` after the
    type is refused by name (a plugin upgrade that adds `generation 0` fails the solo run, not the field).
  - D12 · **A damaged code is claimed by no row** (`HoldsCode`: a prefix and 64+ code characters, more
    than any host-name label holds), so R3's JOIN box can say "damaged" instead of looking it up as a
    host name.
- checkpoint: dba1847
- next: J9

#### J9 PRE
- intent: the rest of `Link.cs` (§5, §7): `StunRow` and `Servers` (the Google and Cloudflare rows,
  mutable), `StunFirst`, `Config(row)` (one STUN row or none), `Gather` (the walk: seal by `Sealed`
  or `GatherMs`, next row with the same id, `StunFirst` remembered, the guest adding the invite's
  candidates only after its walk), `GatherMs`/`LinkMs`/`InviteLifeS`, `ChannelOf`, `Backlog`. The box:
  `wan.py` rewritten (STUN responder 127.0.41.1:3478, silent UDP 19482/19483, silent TCP 19481, the
  pair proxy on 127.0.42.1, blackhole, stats on HTTP 19480, the ENet relays for `-Wan` kept as
  `--relay` until R2); run.ps1 starts it for every run. Checks: the table's literals, the walk on a
  host's and a guest's peer (silent rows: 4,000 ms, host candidates only, flag; the memory under
  100 ms; the answering row 2 with `StunFirst == 1`), the sealing rule x20 both ways, `ChannelOf`,
  `Backlog`, `Link.Servers` mutable.
- files: scripts/Link.cs, tools/smoketest/SmokeTest.cs.txt, tools/smoketest/wan.py,
  tools/smoketest/run.ps1, docs/plans/ledger_webrtc.md
- from: dba184745e2c4a68accdd95689853a5ca28f3e4f
- hashes: Link.cs 03adbb3c947d4f2a9ddc7a0a7c1e8080a4bfe278; SmokeTest.cs.txt
  6bdace8c4ea55b0f433cb2027c76e8a55ee01fb5; wan.py 8074569012982663452a023d1521dc638d385c2c;
  run.ps1 aef2608da377c95c7771e1917f934beaa45e48af

#### J9 POST
- verdict: done; `verify.ps1 -Quick` ALL CHECKS PASSED (0 warnings, 0 analyser findings, xref 0).
  The box self-tested with Python alone (no engine): 127.0.41.1 and 127.0.42.1 bind on this PC, the
  responder maps a request to its source, the proxy carries both ways from the right ports, the
  blackhole counts, the silent TCP port accepts and says nothing, a relay carries. Untested at rung 3.
- files: scripts/Link.cs (`Rpcs`, `ChannelOf`, `Backlog`, `StunRow`, `Servers`, `StunFirst`,
  `GatherMs`/`LinkMs`/`InviteLifeS`, `Config`, `Gather`); tools/smoketest/wan.py (the box);
  tools/smoketest/run.ps1 (the box for every run, `Stop-Box` in the finally, the relays as
  `--relay`); tools/smoketest/SmokeTest.cs.txt (`Box`, the box's rows, `LoopbackSrflx`, `Walk`,
  `Walked`, `Walks()` after the pair frees; `ChannelOf` and `Backlog` on pair `a`)
- decisions:
  - D13 · **`QuietMs` and `BeatMs` land in R2 with the beat**, not here: S1's `Net.QuietMs` is the
    one 8,000 until R2 moves it ("one constant, not two", §7). R1 adds the timings R1's code or R2's
    Pending needs: `GatherMs`, `LinkMs`, `InviteLifeS`.
  - D14 · **The box**: control on 127.0.0.1:19480; path `0,0,0` in plain runs (delay and loss only
    under `-Wan`); stopped by `POST /box/quit` so it prints its stats; it carries the ENet relays for
    `-Wan` (`--relay 28115:27115`, `28125:27125`) until R2 moves `-Wan` onto the pair proxy. run.sh
    (Linux) does not start it: the Linux runners have no plugin (R0 Known broken).
  - D15 · **A guest adds the invite's candidates only after its walk** (`Link.Gather`'s header): a
    guest that can reach the host starts DTLS before the host has the reply (P2), and a walk that
    remakes that connection would free it mid-handshake (two plugin ERROR lines). This orders §3.3 A
    guest steps 3-4; R2's `Join` must follow it.
  - D16 · **`LoopbackSrflx` (harness, true)**: the plan's fallback as a switch. If rung 3 shows libjuice
    drops the box's loopback-mapped server-reflexive candidate, set it false and write it in DESIGN.md;
    the answering-row walk and the sealing rule then hold the timing and host candidates only.
- checkpoint: 06db494
- next: J10

#### J11a PRE (records for J7-J9, written early: the agent's context passed ~150k at J9)
- intent: CHANGES Unreleased entry "WebRTC slice R1, first part" with Checks and an honest Known
  broken; DESIGN traps (the guest's candidates after its walk; the codec held to the plugin's bytes);
  this ledger's handoff below.
- files: docs/CHANGES.md, docs/DESIGN.md, docs/plans/ledger_webrtc.md
- from: 06db494f33757be51bf2484e10f547966dd17d9d
- hashes: CHANGES.md 3631a5f66e39f19cadbab0fe47a8297093fa8f87; DESIGN.md
  163ea4ac19477da8e39ccb834a951cea47ca696c; ledger 36ef1fcbca521bcf13532b30929b555a1ff4d2b5

#### J11a POST
- verdict: done (records only). The CHANGES Handoff is NOT touched: it is written when R1 ends (J11).
- checkpoint: the J11a commit
- next: a FRESH agent does J10, then J11, from the handoff below.

#### J9b PRE (rung 3 red at 6a19f5a, seed 11400714819323466726: three FAILs)
- intent: (1, 2) the mutability checks compared a swapped fingerprint with `Net.Protocol`, taken at
  startup, while the fingerprint itself had moved during the run (both swaps read 7991f5f3 against
  3724c77b): compare swapped with unswapped at the same moment, keep the structural test, and name
  what moved since the run began in the message. (3) `Link.Backlog` read 0 with 1 MB put: the burst
  fit inside the SCTP socket's own send buffer, which the plugin does not count; put 200,000 B until
  the row backs up (at most 8 MB, same frame, no poll), then drain.
- files: tools/smoketest/SmokeTest.cs.txt, scripts/Link.cs (Backlog's comment), docs/DESIGN.md,
  docs/plans/ledger_webrtc.md
- from: 6a19f5a75707f2abddd1bedc423cd406edb06aed
- hashes: SmokeTest.cs.txt b55f0f84e23554e0962f194abb6ee583eff16048; Link.cs
  d5cabe36989944d50ed1fdf1b73aa67685a9f3c6; DESIGN.md fe54c65e067b8a8112259743714b9ffc26ec52b0

#### J9b POST
- verdict: done; `verify.ps1 -Quick` ALL CHECKS PASSED. Untested at rung 3.
- files: tools/smoketest/SmokeTest.cs.txt (`FingerprintNow`, `FingerprintParts`, `_partsAtStart` taken
  at the top of the solo run, `Moved`; the two seam checks; the backlog burst), scripts/Link.cs
  (Backlog's comment), docs/DESIGN.md (two traps)
- decisions:
  - D17 · **A seam is outside the fingerprint by the fingerprint's own rule** (neither literal nor
    init-only); its check compares swapped with unswapped at the same moment. The drift itself
    (3724c77b at startup, 7991f5f3 near the end of a solo run) is OPEN: the next rung 3 names the moved
    parts in those two checks' messages whenever the fingerprint is not the startup's.
  - D18 · **`Link.Backlog` counts only what waits beyond the SCTP send buffer** (1 MB fit and read 0).
    The check puts 200,000 B in one frame until the row backs up (8 MB cap); 0 after 8 MB would mean
    the plugin reports nothing, and the message says how many packets it took.
- rungs owed: rung 3 twice (two seeds): "Rendezvous.Clipboard is mutable and outside the build's
  fingerprint", "Link.Servers is mutable and outside the build's fingerprint", "Link.Backlog reads a
  burst waiting on row 12 once it outgrows the SCTP send buffer"; read the first two's messages for
  the moved parts.
- checkpoint: the J9b commit
- next: J10

#### J10 PRE
- intent: the rows end to end (handoff below): `Rendezvous.Guest`, the pending table (`Stage`,
  `Entry`, `Pending`), `IHostDesk`/`IGuestDesk`, `IRendezvousPath` gains `Open`/`Close`/`Start`/`Poll`;
  the address row's listener (dual-stack TCP, `ListenPorts`, `ListenFrom`, `ListenPort`, 2-byte framing,
  `ReadMs`, `KnocksPerMinute`, the ordered checks, `closed`) and dialer; the paste row's `Start` and
  clipboard pickup (`PickupMs`); `Net.DefaultPort` public. Harness: the `Desk`, the courier (files and
  `Rewrite`), `RtcPair`'s carry, `Polling`, `Rows`/`AddressRows`/`PasteRows`/`Proxied`.
- files: scripts/Rendezvous.cs, scripts/Net.cs, tools/smoketest/SmokeTest.cs.txt, docs/plans/ledger_webrtc.md
- from: 876079540db9f18be7b3d9adba2341847fe0f806
- hashes: Rendezvous.cs 7d5031b69bfe29a413212a796c813179394abee6; Net.cs
  15e0d9a02d21b7016d720356385d2887a0b1d89d; SmokeTest.cs.txt 33ac9f60e6a133c2b12f622f3c9c6cb2f4d59cac
- patches (repeatable, exact-match): `%TEMP%\lane_net\j10_rendezvous.py`, `j10_harness.py`, `j10_fix.py`

#### J10 POST
- verdict: done; rung 1 green, `verify.ps1 -Quick` ALL CHECKS PASSED (0 warnings, xref 0). Untested
  at rung 3.
- files: scripts/Rendezvous.cs (Guest, Stage/Entry/Pending, the desks, the rows' Open/Close/Start/
  Poll, the listener, the dialer, the framing, the pickup), scripts/Net.cs (`DefaultPort` public),
  tools/smoketest/SmokeTest.cs.txt (`Desk`, `IsRefusal`, `CourierFile`, `Rewrite`, `Polling`,
  `Message`, `Rows`/`PasteRows`/`AddressRows`/`Proxied`, RtcPair's carry and `Hand`)
- decisions:
  - D19 · **The address row's `Poll` does nothing**: its listener hands each knock over by
    `CallDeferred` (§3.3 B); `Poll` is on the contract for the rows that watch something (the paste
    row's clipboard). R2's `Net._Process` polls every row of `Paths`.
  - D20 · **Every refusal reaches `IHostDesk.Refused`** (build, rate, full), not only build: the desk
    decides what the host reads. `closed` does not (the host is the one closing).
  - D21 · **An address-row invite lives on its connection**: a connection that ends without the reply
    (timeout, close, a wrong id) hangs its entry up through the desk; an invite the desk makes after
    its connection was answered (`closed`, or 3 s passed) is hung up too.
  - D22 · **The dialer's connect has no bound of its own**: R2's JOIN bounds the whole flow by
    `JoinTimeoutMs` (§3.3 B guest 4); each record read or written has `ReadMs`.
  - D23 · **The listener falls back to IPv4 only where the OS has no IPv6** (`Socket.OSSupportsIPv6`;
    a Linux container), and the IPv6 join form becomes a second IPv4 one there.
- rungs owed (rung 3, two seeds): "the listener takes 27015, then 27016 ... 27024"; "the guest's mark
  is drawn once per process"; "by the address row a knock brings an invite"; "a newer knock from the
  same guest leaves one pending entry"; "a knock is refused `full`"; "an address that knocks 21 times
  in a minute"; "a knock waiting on the host's desk when the listener closes"; "by the paste row,
  through the courier's files"; "the clipboard pickup hands a reply over once"; "an in-process pair
  whose codes the courier rewrote"; "a packet sent into a 1 s blackhole"; plus every J7-J9b check.
  If the pair-proxy check fails twice at rung 3 with `to_host`/`to_guest` 0: plan §10.2's fallback
  (Known broken; R4 uses `Net.DropBeatsFor`).
- checkpoint: the J10 commit
- next: J9c (the fingerprint's own drift, below), then J11

#### J9c PRE (the fingerprint drifts; rung 3 on 8760795: both seams 7991f5f3, the startup's 3724c77b, "0 parts moved since the run began")
- intent: D17's cause. `Net.Protocol = Fingerprint()` was a readonly field INITIALIZER: the walk ran
  inside Net's own type initialization and hashed `Net.Protocol=0` (itself: a readonly int is Plain)
  and every Net static declared below it as unset; every later fingerprint sees them set, so it moved
  before the harness began (hence no part moved within the run). Fix: `Protocol` a property with a
  private setter, set in Net's static constructor (after every field initializer; not a field, so it
  never hashes itself). `Character.Bought` a property over a mutable field (live pilot state in a
  readonly int[] is hashed: latent, the same class of fault). The parts diff also keeps the hash at the
  harness's start, so it says when the fingerprint moved before the run began. The seam checks back to
  `== Net.Protocol`; new checks: the fingerprint never hashes itself and is the build's at the harness's
  start; a purchase leaves it the build's. DESIGN's J9b trap rewritten to the cause.
- added (coordinator, walls review): `Net.Plain` accepts a readonly struct of the game's whose public
  fields are all Plain (`StructRow`), written out like a table row: `Post`, `Dock`, `TargetFilter`
  rows were never hashed. `WaveCrew` stays out (its `Count` is a `Func`). Check: one pirate-base
  post moved moves the fingerprint. Net.cs has one writer: this lane.

#### J9c POST
- verdict: done; `verify.ps1 -Quick` ALL CHECKS PASSED. Untested at rung 3.
- files: scripts/Net.cs (`Protocol` a property set by `static Net()`; `StructRow` in `Plain` and
  `Show`), scripts/Character.cs (`Bought` a property over a mutable field), tools/smoketest/
  SmokeTest.cs.txt (`FingerprintParts` under the invariant culture, `_hashAtStart`, `Moved` says when
  it moved before the run began; the three seam checks back to `== Net.Protocol`; BuildChecks' three
  new checks), docs/DESIGN.md (the J9b trap rewritten to the cause; the struct-row trap)
- decisions:
  - D17 (closed) · **The drift was the fingerprint hashing itself mid-initialization**, not a seam and
    not the pilot: `Protocol = Fingerprint()` as a field initializer read `Net.Protocol=0` and the Net
    statics below it unset. `Bought` was the same class of fault, latent (0 parts moved in the run).
  - D24 · **`StructRow` needs at least one public field, all Plain**; a record struct (no public
    fields) keeps its old treatment (not hashed), `WaveCrew` stays out (a `Func` field).
- rungs owed (rung 3, two seeds): "nothing about the pilot is part of the build's fingerprint"; "the
  build's fingerprint never hashes itself and is whole when it is taken"; "a struct row is part of the
  build's fingerprint"; the three seam checks ("Rendezvous.Clipboard is mutable ...", "Link.Servers is
  mutable ...", "the guest's mark ..."), each now against `Net.Protocol`; the existing "the build
  fingerprint covers the stat sheets, the gear table and the prices". Rung 5 once R2 lands: the
  fingerprint's value changed (a property now, struct rows in), which both ends of a run share.
- checkpoint: the J9c commit
- next: J11

#### J11 PRE
- intent: CHANGES -- the R1 entry (J7-J10, J9b, J9c; checks; Known broken as it stands after rung 3
  on 8760795 was green on two seeds) and the Handoff (R1 landed, rungs owed, next R2); this ledger's
  final rungs list.
- files: docs/CHANGES.md, docs/plans/ledger_webrtc.md
- from: e4cac192617f94fcad60a30282c6a6eb4d3a367b
- hashes: CHANGES.md da834e7498e5aea27d5fe023816cca703f7a5cb6

#### J11 POST
- verdict: done (records only; `-Quick` run before the commit).
- files: docs/CHANGES.md (the Handoff's R1 paragraph; "WebRTC slice R1" replaces "R1, first part",
  its Known broken as it stands), this ledger (the rungs owed, below).
- checkpoint: the J11 commit
- next: the main session's rungs below; then R2 (a fresh agent reads this ledger and plan §13).
- files: scripts/Net.cs, scripts/Character.cs, tools/smoketest/SmokeTest.cs.txt, docs/DESIGN.md,
  docs/plans/ledger_webrtc.md
- from: 3e4df1bb54cf87085e3fcd8cf0c2d00047b48f2e
- hashes: Net.cs 1941e3c152c073ab3a7b43d9b684594bf8b402de; Character.cs
  fbe2cc81e905f8e3d2746f55b2a40b3efa1b7efb; SmokeTest.cs.txt b2d6896f4c20c4f32054c062c99b685b9b0de203;
  DESIGN.md 5b58972fb5eca8c2f587f99fe9f31eded98d8392

### HANDOFF for the fresh agent (J10, J11)

Read: this section, `scripts/Rendezvous.cs` (whole, ~420 lines), `scripts/Link.cs` (whole), the
harness's R1 block (grep `WEBRTC, R1` in SmokeTest.cs.txt: `Codes()`, `Walks()`, `Box()`), plan §3.3 B,
§4.1, §10.2, §10.4 R1 (the last three bullets). Environment rules as in this section's header; ALWAYS
give .NET file calls absolute paths (`[IO.File]::ReadAllText` resolves a relative path against the
process's directory, which is the MAIN checkout, not PowerShell's location: J8 read and rewrote the main
checkout's SmokeTest.cs.txt that way, bytes unchanged, mtime touched).

**J10 · the rows end to end** (files: scripts/Rendezvous.cs, scripts/Net.cs (only `DefaultPort`
private -> public, for the listener's first port), tools/smoketest/SmokeTest.cs.txt). The contract
this batch designed, so R2's Net drops in as the desk:
- `Rendezvous.Guest`: uint, drawn once per process, a PROPERTY over a non-readonly private field
  (`_guest ??= ...` in the getter). NEVER a static readonly: `Net.Fingerprint` folds every readonly
  static of a Plain type into `Net.Protocol`, and a per-process value there would make every process a
  different build (the `Game.Build` trap, BuildChecks). Add it to the mutability checks.
- The pending table, in Rendezvous (Net holds one in R2): `enum Stage { Waiting, Linking }`,
  `sealed class Entry { int Id; string Row; uint Guest; Stage Stage; }`, `sealed class Pending` (Add,
  Remove, Find(id), OfGuest(guest), Count). Keep it minimal: R2 adds the connection, the name, `Made`.
- `interface IHostDesk { Pending Pending; bool Full; Task<Record> Invite(Record knock); void
  Hang(int id); void Replied(Record reply); void Refused(Record knock, Why why); }` and `interface
  IGuestDesk { Record Knock(); Task<Record> Answer(Record invite); }`. `IRendezvousPath` gains
  `void Open(IHostDesk)`, `void Close()`, `Task<Record> Start(string text, IGuestDesk)`.
- Address row = the LISTENER (host) and the DIALER (guest). Listener: dual-stack TCP
  (`IPv6Any`, `DualMode`), ports `Net.DefaultPort` .. +9, then 0 (OS-chosen), overridable for the
  check; records length-prefixed (2 bytes big-endian), cap `WireMax`; 3 s read timeout; the accept loop
  on a background task only moves bytes and hands each knock to the main thread by
  `Callable.From(..).CallDeferred()` (skip when `Game.ShuttingDown`), answers come back by a
  `TaskCompletionSource`. Checks IN ORDER (§3.3 B host 2): the build (`knock.Proto != Net.Protocol` ->
  refuse `Why.Build` carrying the host's proto and build, and `desk.Refused`), the rate (20 knocks a
  minute per remote address -> `Why.Rate`), one pending entry per guest (`desk.Hang` each old entry of
  `Pending.OfGuest(knock.Guest)`), capacity (`desk.Full` -> `Why.Full`); then `await desk.Invite(knock)`,
  send it, read the reply (3 s), `desk.Replied` on the main thread. `Close()` refuses a knock in flight
  with `Why.Closed`. Dialer (`Address.Start`): `Net.ParseAddress`, `Net.DialAddress` for a name, TCP
  connect, send `desk.Knock()`, read the invite or refuse; an invite -> `await desk.Answer(invite)`, send
  it, close; return the host's record.
- Paste row: `Start(text, desk)` = `Find(text, Kind.Invite)` -> `await desk.Answer(invite)` -> return
  the invite (the UI shows `Encode(reply)`); `Open(desk)` arms the CLIPBOARD PICKUP (§15 Q1, owner
  default YES): `Paste.Poll()` (R2's `Net._Process` calls it; the harness in R1) reads `Clipboard()` at
  most every 500 ms, and hands a reply whose id is a Waiting entry, once per distinct text, to
  `desk.Replied`; `Close()` disarms.
- Harness (§10.4 R1 "rows end to end", "pair proxy"): a `_Test` desk implementing both interfaces
  with a real `Rendezvous.Pending` and dummy invites/replies (`VaryRecord`). Checks: over an in-process
  listener on a free port (OS-chosen: never 27015, the main checkout's runs use it), knock -> invite ->
  reply reaches `desk.Replied` with the invite's id; a second knock from the same `guest` leaves one
  pending entry (the first hung up through `desk.Hang`); `full`, `build` (a knock with
  `Net.Protocol ^ 1`) and `rate` (a fresh listener, 21 knocks) refusals, each read by the dialer;
  the paste row through the COURIER's files (`invite-{port}-{n}.txt`, `reply-...`, under
  `ProjectSettings.GlobalizePath("res://")`), the reply picked up through the swapped
  `Rendezvous.Clipboard` by `Paste.Poll` (a Discord-style wrapping). The COURIER (harness): unpacks a
  code with the game's codec, keeps one IPv4 host candidate, replaces its address/port with
  127.0.42.1:A (invite) or :B (reply) from `POST /box/pair`, registers
  `POST /box/pair/{n}?host=127.0.0.1:{host's port}&guest=127.0.0.1:{guest's port}&seed={Seed}` (the
  real endpoint is 127.0.0.1 and the candidate's port: libjuice binds one any-address socket; the box
  self-test showed 127.0.42.1 -> 127.0.0.1 works), packs again. The PAIR-PROXY check: an in-process
  pair whose bundles cross as courier-rewritten codes (Strip -> rewrite -> Encode -> Find ->
  `Sdp.Build` and `Sdp.Line`) connects through the box, and `/box/stats` shows `to_host` and `to_guest`
  above 0 for its pair; optionally a packet sent into `POST /box/blackhole?s=1` arrives after it lifts
  and `blackholed` counted it. Fallback if libjuice will not use the proxy after two rung-3 attempts
  (plan §10.2): CHANGES Known broken, and R4's watchdog uses `Net.DropBeatsFor(s)` instead.
- Then `-Quick` green (poll for Godot first), commit with its `Checks:` line.

**J11 · records**: CHANGES -- turn "R1, first part" into the R1 entry (J10's checks, Known broken
updated), and the Handoff (R1 landed, the rungs below owed, next R2); DESIGN if J10 finds a trap; this
ledger's POSTs and the final rungs list.

### Rungs the main session owes for R1 (none run by this lane)

Done: J7-J9b at 8760795, `-Quick` and rung 3 green on seeds 11400714819323466726 and
11400714819323463562 (the loopback server-reflexive holds: `LoopbackSrflx` stays true, D16 closed).
Owed on the R1 records commit (it carries J10 and J9c):
1. `verify.ps1 -Quick` (green here at e4cac19).
2. **Rung 3 twice, two seeds** (`tools\smoketest\run.ps1 -Solo -Seed <a>`, `<b>`). PASS on everything
   that passed at 8760795, and the new or rewritten: "nothing about the pilot is part of the build's
   fingerprint"; "the build's fingerprint never hashes itself and is whole when it is taken"; "a struct
   row is part of the build's fingerprint"; "Rendezvous.Clipboard is mutable and outside the build's
   fingerprint"; "Link.Servers is mutable and outside the build's fingerprint" (both now against
   `Net.Protocol`); "the listener takes 27015, then 27016 ... 27024"; "the guest's mark is drawn once per
   process"; "by the address row a knock brings an invite"; "a newer knock from the same guest leaves
   one pending entry"; "a knock is refused `full`"; "an address that knocks 21 times in a minute";
   "a knock waiting on the host's desk when the listener closes"; "by the paste row, through the
   courier's files"; "the clipboard pickup hands a reply over once"; "an in-process pair whose codes the
   courier rewrote to the box's address connects through the box"; "a packet sent into a 1 s blackhole
   arrives after it lifts"; no ERROR line. If the pair-proxy check fails twice with `to_host`/`to_guest`
   0: plan §10.2's fallback (CHANGES Known broken; R4's watchdog uses `Net.DropBeatsFor`).
3. Rung 5 is R2's (nothing in R1 crosses peers; the fingerprint's new value is shared by both ends).
4. Optional: one `run.ps1 -Wan` (the ENet relays moved into the box, `--relay`).

### R1P PRE (the owed chain: quick, solo, solo, six)
- model: sonnet (escalate to opus after one red)
- intent: run the chain the main session owes (`verify.ps1 -Quick`, rung 3 twice on two seeds, then
  the six-role run) at HEAD e2d31b3, through the shared `rungs.ps1` runner, tags `net_r1a`, `net_r1b`,
  ... Read each solo log for the owed check names (a name that never printed is a failure). Fix any
  red at the lowest rung that sees it, two seeds before its commit. The pair-proxy check is the known
  unknown: two failures with `to_host`/`to_guest` 0 and nothing else to try takes the ledger's
  fallback (plan §10.2: CHANGES Known broken, R4's watchdog on `Net.DropBeatsFor`).
- files (may touch, only if a rung is red): scripts/Link.cs, scripts/Net.cs, scripts/Rendezvous.cs,
  scripts/Character.cs, tools/smoketest/SmokeTest.cs.txt, tools/smoketest/run.ps1,
  tools/smoketest/wan.py, docs/CHANGES.md, docs/DESIGN.md, docs/plans/ledger_webrtc.md
- from: e2d31b36761bae422356ad7e16bfd3bc3fa3680b
- hashes: Link.cs 67b37be6f99ba9dbcfbf5910a6fe4c070856751d; Net.cs
  3c10715d9675c949b56010b2c106d2f000254341; Rendezvous.cs 513eddbc99c42984530cce2bfa28a8659ed2446c;
  Character.cs a23df4a51223f6f9a9be822b4da2d77197d0c862; SmokeTest.cs.txt
  c595dacea846b11d8e750bcfd1541bc2183f4830; run.ps1 ad134d9c0ffbe6ddba0fb3016534699cebf9a2be;
  wan.py 74fc642f5d25bd2c3c1d94dd244dda414bc625d4; CHANGES.md 658342ca494153aae9cd0272e31a6f9d131f8e33;
  DESIGN.md c55c31673f59a2cfc04f9bc70a93253d72c2d3a8; ledger 3ba6ceff4f9f343e6091472e281ad979f7d4e06f

### R1P POST
- verdict: ALL GREEN. `net_r1a` (tag) hit rung 3 red first pass, seed 11400714819322686448: two FAILs,
  both J9c's, neither visible below rung 3 -- "nothing about the pilot is part of the build's
  fingerprint" (asserted zero `Character.`-prefixed fingerprint parts at all; `Character` carries five
  of its own `const` bounds -- `Dir`, `MaxBonus`, `MaxStock`, `PaidKept`, `SaveDelay` -- the same for
  every peer on this build whichever pilot is loaded, always part of the fingerprint, never the pilot)
  and "every field of Character is accounted for by this test" (named the field `Bought`; it is a
  property now, so reflection sees the compiler's backing field, `<Bought>k__BackingField`, the same
  way `LastSaveRenamed`'s already does). Fixed at rung 3 (the only rung that sees a reflection-driven
  runtime assertion), checkpoint c4a7e01. `net_r1b` from c4a7e01: `-Quick` green (81s); rung 3 green
  twice, seeds 11400714819323513555 (143s) and 11400714819323526641 (134s), every owed check name
  read back PASS in both logs (none missing), no ERROR line; the six-role run green (251s, seed
  11400714819323519265), all six roles `fails=0`, no build/protocol refusal anywhere -- host and every
  guest agree on J9c's `Net.Protocol`. **The pair-proxy unknown is CLOSED, in the box's favour:**
  libjuice does use the loopback pair proxy as a remote candidate ("an in-process pair whose codes the
  courier rewrote to the box's address connects through the box, datagrams both ways": 9 to the host, 9
  to the guest, 0 before its endpoints were known); the blackhole check passed alongside it (1468 ms).
  The `Net.DropBeatsFor` fallback (plan §10.2) was never needed.
- files: tools/smoketest/SmokeTest.cs.txt (the two rung-3 fixes above), docs/DESIGN.md (the trap),
  this ledger (PRE/POST)
- checkpoint: c4a7e01 (the fix); nothing to commit for the green re-run itself (records below carry it)
- next: CHANGES.md Handoff and Known broken updated to this rung 3/5-equivalent result (the pair-proxy
  line removed, not a fallback); then the bar, a merge to `version-l`, and R2.

### R1Q PRE (the merge gate's StructRow fix)
- model: sonnet (escalate to opus after one red)
- intent: the merge gate failed the lane on `scripts/Net.cs:239-242`: `StructRow` required
  `IsReadOnlyAttribute`, so a MUTABLE struct row (`StatusSet.Guards`: `StatusGuard[]`;
  `EmplacementDef.Gun`: `TurretSpec?`, the pirate base's cruise missile) and a `System.ValueTuple`N`
  row (`Hub.PracticeTargets`, `Hub.Outposts`'s names) never entered the build's fingerprint --
  `Show` printed a mutable struct as its bare type name, and a ValueTuple row was skipped entirely.
  Fix: `StructRow` accepts any value type of the game's own assembly, or any `System.ValueTuple`N`,
  whose public instance fields are all Plain; the `IsReadOnlyAttribute` test is dropped (a value in
  a static readonly field or a table row is as fixed as what holds it). Add `BuildChecks` rows
  beside the Post check (SmokeTest.cs.txt:2233) that move one Guards Share, the base gun's Damage,
  and one PracticeTargets row -- each must move the fingerprint and be restored. Rewrite the
  DESIGN.md:1354-1357 trap to match. Then re-prove the lane: `-Quick`, rung 3 twice (two seeds), the
  six-role run, through the shared `rungs.ps1` runner, tags `net_r1c`, `net_r1d`, ...
- files (may touch, only if a rung is red): scripts/Net.cs, tools/smoketest/SmokeTest.cs.txt,
  docs/DESIGN.md, docs/plans/ledger_webrtc.md
- from: 77c3174c913611418ee30327bb33c6b1e8b6d79b
- hashes: Net.cs 3c10715d9675c949b56010b2c106d2f000254341; SmokeTest.cs.txt
  9cefc479ecf01d7a1db0132a85d1bc0a5612fa3e; DESIGN.md f2f244bc2ec1fec574c2fc903f0f3bc2fee15f7b;
  ledger 14b87e23ffde1226385b72b49d16e32d23df02a3

### R1Q POST
- verdict: ALL GREEN. Rung 1 clean (0 errors, real GodotSharp.dll). `net_r1c` (tag) at 77c3174: `-Quick`
  ALL CHECKS PASSED (84s, after waiting 495s for another engine run on the machine); rung 3 green twice,
  seeds 11400714819323524536 (128s) and 11400714819323511382 (126s), all three new checks PASS on both
  ("a mutable struct row is part of the build's fingerprint: one status guard's share moved moves it...",
  "the pirate base's mutable gun is part of the build's fingerprint: its damage moved moves it...", "a
  value-tuple row is part of the build's fingerprint: one practice target moved moves it..."), every
  check the R1P handoff owed read back PASS in both logs (none missing), no FAIL, no ERROR line; the
  six-role run green (239s, seed 11400714819323521240), all six roles `fails=0` (solo, guest, host,
  guest2, aguest, ahost), no build/protocol refusal anywhere -- host and every guest still agree on
  `Net.Protocol` with the widened `StructRow`.
- files: scripts/Net.cs (`StructRow`: any value type of the game's assembly, or any
  `System.ValueTuple`N`, whose public instance fields are all Plain -- `IsReadOnlyAttribute` dropped),
  tools/smoketest/SmokeTest.cs.txt (`BuildChecks`: three new checks beside the post check -- a status
  guard's `Share`, the pirate base's `Gun.Damage`, one `PracticeTargets` row, each moved and restored),
  docs/DESIGN.md (the struct-row trap rewritten to the three kinds and the fix), this ledger (PRE/POST)
- checkpoint: the R1Q commit (code + records together)
- next: CHANGES.md Handoff and the R1 Unreleased entry updated to carry this fix (same commit); then
  the bar, a merge to `version-l`, and R2.
