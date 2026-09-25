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

### R1R PRE (merge gate 2 fix: WaveCrew's delegate field skipped by StructRow)
- model: sonnet (escalate to opus after one red)
- intent: step 0, merge `version-l` (f6ae54c, carries the walls lane and docs/tool commits) into
  `wt/net`; resolve Hub.NetIdentity's peak (walls) vs net's changes from both ledgers, keeping both
  behaviours; typecheck + `-Quick` green (a new walls table the fingerprint must now hash is a fix
  here, with its check). Then job R1R: gate 2's defect -- `StructRow` (scripts/Net.cs ~245-248) requires
  every field Plain, so `WaveCrew` (a Func `Count` field) is skipped and prints bare, and
  `Waves.Patrol`/`HuntPin` (private static readonly rows of `WaveDef`, whose `Crew: WaveCrew[]` field)
  never entered the fingerprint. Fix: `StructRow` accepts a field that is Plain OR a delegate
  (`fields.All(f => Plain(f.FieldType) || typeof(Delegate).IsAssignableFrom(f.FieldType))`); `Show`
  prints a delegate's type name, null as `null`. New `BuildChecks` row after the value-tuple check
  moving one `WaveCrew.Step` on `Waves.All["raid"].Crew`, asserting the fingerprint moves and the
  `Waves.All=` part contains `WaveCrew{`, restored, fingerprint restored. Rewrite the Net.cs comment and
  DESIGN.md trap (~1365-1366) that say WaveCrew "stays out"; reword the Post check's comment in
  SmokeTest.cs.txt (~2230-2231) from "a readonly struct whose public fields are all Plain" to the
  current rule (any game value type or ValueTuple`N` whose public fields are Plain or delegates,
  readonly or not).
- files: scripts/Net.cs, tools/smoketest/SmokeTest.cs.txt, docs/DESIGN.md, docs/plans/ledger_webrtc.md
- from: 94ad917127663fcc1651739f4afb0d89fc1826a9
- hashes (pre-merge): Net.cs cedcd5086de1baca30525bd70ad1bc88ad4f403a; SmokeTest.cs.txt
  016aef3225e0914b4fdfde01215783003e2559be; DESIGN.md 0fb7242345053d4db8a5e4c664de98646c4475f0;
  ledger ce2d58d4728ece7025ce438590e73cd69865a63b; CHANGES.md b029fd808f0ec7e0000563353199ae0fa335e4e2

### R1R POST
- verdict: done. Step 0's merge (`fea3466`): walls lane's Character.Peak kept beside net's Bought
  property; CHANGES.md/DESIGN.md keep both lanes' entries, the stale pre-walls-merge multi-lane status
  block dropped. `Unlocks.All` needed no fingerprint fix of its own: it is already a record class
  (`<Clone>$`), so it already reaches `Net.Plain` through the Table/record path, not `StructRow`.
  Job R1R: `StructRow` now accepts a field that is Plain OR a delegate; `Show` prints a delegate as its
  type name. New `BuildChecks` row after the value-tuple check moves `Waves.All["raid"]`'s crew step,
  asserts the fingerprint moves and `Waves.All=` contains `WaveCrew{` (not bare), restores and asserts
  the fingerprint is the build's again. Net.cs's own comment (~236-245) was already written to the new
  rule when it was authored; DESIGN.md's trap (~1367-1380) and SmokeTest's Post-check comment
  (~2253-2256) rewritten to match. `typecheck` and `verify.ps1 -Quick` both ALL CHECKS PASSED.
- files: scripts/Net.cs (`StructRow`, `Show`), tools/smoketest/SmokeTest.cs.txt (Post check comment,
  new delegate-row `BuildChecks`), docs/DESIGN.md (struct-row trap, four kinds now)
- checkpoint: 1a139c0 (code); this commit adds the chain result and CHANGES records
- chain: `net_r1r` (bash-launched detached background, exit 128 at 0s -- the launcher script itself,
  not rungs.ps1, never actually ran under that method) discarded; re-run `net_r1r2` through the
  PowerShell tool's own background run, at 1a139c0: `-Quick` green (78s); rung 3 green twice, seeds
  11400714819323517228 (130s) and 11400714819323555799 (131s); the six-role run green once (241s, seed
  11400714819323519350, every role `fails=0`). Both new checks ("a row with a delegate field is part of
  the build's fingerprint: one wave crew's step moved moves it, and it is written out, not bare"; "a
  wave crew row put back is the build's fingerprint again") read PASS in both solo logs and the six-role
  log (`[solo]` role), no FAIL or ERROR line anywhere in quick/solo/solo/six.
- next: docs/CHANGES.md Handoff and the R1 Unreleased entry updated to carry this result (same commit);
  done.

### R1S (coordinator) -- gate 3's two comment defects
- Gate 3 (opus) passed the code and failed two comments: Net.cs StructRow + DESIGN.md claimed the assigned delegate moves the
  fingerprint (Show prints only its type name, Func`2); Unlocks.cs (from the walls merge) said Plain() never hashes a plain struct.
- Fixed both as the gate wrote them; comments only. quick net_r1s ALL GREEN. Chain net_r1r2 still covers the code. Next: merge into version-l.

## Lane net2 · S2 + R2-R5 code and record (worktree WarShips_wt_net2, branch wt/net2, from f168508)

BUILD PHASE (CLAUDE.md top, owner ruling): no engine run of any kind. Per job: typecheck, `verify.ps1
-Quick`, a read of the diff. Checks are WRITTEN with each job, in the lane's own named methods
(`S2Checks`, `R2*`, `R3*`, `R4*` in SmokeTest.cs.txt; `WebRtcFrames` in Shots.cs.txt), RUN in the
final test phase. Owner questions take their defaults (README rulings line 101): clipboard pickup YES,
hold 90 s KEEP, UPnP DELETE.

### JOB 0 · the lane's job list (foundations first)
S2 is not built (no ledger entry, no commit; P4's `Held` return still precedes Boss's send, P10's
`RestoreHeld` re-keys votes only), so it comes first (plan §13: before R2).
- **S2a** P4 (Boss sends while Held), P5 (shockwave throws no Structure/Dummy: `Targeting.Throwable`
  row forbids them), P8 (the host refuses a class change in the arena or in combat), P9 (`GoTo` of
  the sector it is in is a no-op; `NetMySector` carries the world serial; a stale one is ignored; a
  world's first report is never metered), P10 (`RestoreHeld` re-keys deployed turrets; the dead
  `back` branch goes). Files: Boss.cs, Targeting.cs, Hub.cs, PlayerShip.cs (smallest hunks),
  SmokeTest `S2Checks`.
- **S2b** P10b, the rejoin token (plan §3.9): the host issues a per-pilot token after admission
  (`NetToken`, Reliable, Hub), the guest keeps it per host and sends it with its identity; a token
  that matches a held place, or a live peer still holding that character, gives the place to the
  returning peer (the old peer hung up). Files: Hub.cs, Session.cs, SmokeTest `S2Checks`.
- **R2a** foundations that compile beside ENet: `NetChannels.Beat` = 12 + `NetBeat`/`NetBeatBack`,
  `StreamChannel` -> `Beat + 1`, `Pending` entries grown (conn, name, made), `PretendAt` flags,
  GodotStub `WebRtc*` members.
- **R2b** THE SWITCH (one edit): Net's session on `WebRtcMultiplayerPeer` (host offers, both rows,
  the listener, `Invite()`, `TakeCode`, `Join(text)` by row), the watchdog, the goodbye's body,
  every hang-up through `Link.Hang`, retries by row (`Auto`); delete ENet, UPnP (Router.cs ->
  Adapters.cs), describe/reach/reveal/public-IP (api.ipify.org), fakeigd, the router scenarios,
  S1's `Net.Link`/throttle/`server` flag; `CouldNotReach` wording; SessionMenu INVITE / reply box /
  JOIN box (minimal); the roles moved, `Drop()` rewritten, waits re-set (P() kept on every port).
- **R3** §6's words, COPY NETWORK REPORT, clipboard pickup wired, Hints, Game.cs's comment, frames
  51_invite_ready / 52_reply_countdown replacing 51-52, reply_expired and join_failed.
- **R4** the watchdog's blackhole check and the rate check at 150/40/5; per-row gap and backlog
  printed.
- **R5** packaging scripts (pack/install/play/snapshot/manifest, NOTES texts) and the record
  (DESIGN, README "Playing with a friend", CHANGES Unreleased + Handoff + Known broken = §10.5).
- Not this lane (the test phase): `pack.ps1 -Dirty`, the one-machine check, rung 6, publishing, §11.
- Decisions taken by default (no source says otherwise): P10b's token is 16 random bytes as hex,
  sent by the host after the welcome and kept by the guest per host address/name for the process;
  it is never in a code (plan §3.9: "carried after connection, not in the code").

#### S2a PRE
- job S2a, tier opus. Intent: P4, P5, P8, P9, P10 (JOB 0's list). HEAD d116c47.
- files: scripts/Boss.cs 85c742d6, scripts/Targeting.cs 226e78b5, scripts/Hub.cs d10481a4,
  scripts/Session.cs cbf19552, tools/smoketest/SmokeTest.cs.txt 9949bb60.
- checks planned: solo `S2Checks` (P5 throw filter + a real shockwave by a BASTION at 3 varied
  bearings leaving a dummy and an emplacement in place and throwing a raider; P8 a class change
  announced in the arena / in combat refused, at home out of combat applied; P9 a stale sector report
  ignored, a same-trip NetSector a no-op, a new trip applied); rung 5 `S2HeldBossHost/Guest` (P4: the
  host holds the boss and steps its hull 0.80/0.75/0.70/0.65 inside the hold; the guest sees at least
  3 of the 4); rung 5 P10 in ArenaMp's drop: a guest's turret re-keyed to its new id.

#### S2a POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED. engine-unproven: rungs owed
  in the final test phase.
- P4 Boss.cs: the send block moved above the `Held` return. P5 Targeting.Throwable forbids
  Structure|Dummy. P8 `Hub.Refit(had, want, arena, inCombat)` (public static, the rule in one place),
  used by NetIdentity on every peer; the audit's "keep the hull fraction via Restat" half NOT built
  (honest refits happen at home; a refused change keeps the old class whole). Risk: a pilot whose REFIT
  UI lets it change class at home while in combat now disagrees with the host until it leaves combat
  and announces again -- the test phase should watch for it. P9 `Session.Trip` (host's count of sector
  moves) / `Session.HeardTrip` (guest; -1 = none, reset by `End`); NetSector and NetMySector carry the
  trip; a stale report is ignored; a trip already made is not reloaded; a world's first report per
  pilot is never metered (`Hub._caughtUp`). P10 RestoreHeld re-keys DeployedTurret.OwnerId/Ship; the
  dead `back` branch deleted.
- checks written (not run): solo `S2Checks` (2 P5 checks, 2 P8 checks), `S2TripChecks` (P9 no-op),
  the rewritten level-with-the-sector check (passes a new trip); rung 5 ArenaMp `S2HeldBossHost` /
  `S2HeldBossGuest` (P4 + the host ignoring a stale report, P9), `S2TurretRekeyed` (P10).
- next: S2b, the rejoin token.

#### S2b PRE
- job S2b, tier opus. Intent: P10b, the rejoin token (JOB 0). HEAD aef72cd. Files: scripts/Hub.cs
  894bf72e, scripts/Session.cs 0b1d74bd, scripts/Net.cs 92949cc7, tools/smoketest/SmokeTest.cs.txt eca57f36.

#### S2b POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED. engine-unproven: rungs owed
  in the final test phase.
- `Session.Tokens` (host: character id -> 32-hex token, cleared by `End`), `Session.Rejoin` (guest: the
  token it holds; NOT cleared by `End`, a drop ends the guest's session), `TokenFor`, `MayClaim(id,
  token, liveHolder)`. Hub: `NetIdentity` gains `token`; the claim is `MayClaim`; a live holder beaten
  by the token is let go (`Net.Hang`, new: the one hang-up, R2 turns its body into `Link.Hang`) and
  its place handed over in `OnPlayerLeft` (`_replacing`); the host sends `NetToken` (Reliable, Hub,
  channel 0) once a pilot's id is taken. `SendIdentity` carries `Session.Rejoin`.
- Default taken: the token lives for the game process (a restarted game has none, and an id with a
  token issued can then no longer be claimed by name until the host's session ends). Recorded as a
  test-phase watch item, not built further.
- checks written (not run): solo `S2TokenChecks` (4: no token by name; 32 hex one per pilot; only the
  token claims, gone or live; End forgets), the loose-Hub identity check's argument list; rung 5
  ArenaMp host "claimed its place with the rejoin token", aguest "came back holding the rejoin token".
  NOT written: the live-holder replacement at rung 5 (a guest back before the host notices its old
  link die) -- it needs the R2 watchdog's 8 s silence; R2's paste-guest return check owes it.
- next: R2a.

### HANDOFF for the fresh agent (R2a onward) -- this agent stopped at ~150k after S2b
Read: this lane's section (JOB 0 list, S2a/S2b POSTs), the R1 HANDOFF above (the J10 contract: the
Pending table, IHostDesk/IGuestDesk, the listener/dialer, the paste row, the courier), plan §3 (whole),
§6, §7, §9, §10.3-10.5, §13's R2-R5 rows. Then Net.cs, Link.cs, Rendezvous.cs, SessionMenu.cs whole;
Router.cs, typecheck/GodotStub.cs, tools/smoketest/run.ps1 by grep. Harness landmarks (SmokeTest.cs.txt,
line numbers as of fea4cc7): header's `P()`/`NoRouterNoInternet` ~25-35 and `RouterScenarios` 60-120
(both go in R2b with fakeigd; KEEP `P()` on every surviving port), `Drop(int)` ~155 (ENet body ->
`Link.Hang` deferred, §3.8), the R1 blocks 326/584/730 (`Codes`, `Walks`, `Rows`, `Proxied`,
`WebRtcPairs` ~1041), role dispatch ~1261, `ArenaMp` (ahost/aguest, address row) ~10270, `Mp`
(host/guest/guest2) ~10970. The S2 methods sit just above `ArenaMp`.
Facts this lane added that R2 must keep: `Net.Hang(peer)` is the one hang-up (OnAuth's refusal and the
rejoin token's replacement use it) -- R2b rewrites its body to `Link.Hang(mp, peer)`; `Hub.NetIdentity`
has a trailing `token` argument; `NetSector`/`NetMySector` carry a trip; the paste guest's return
(plan §3.9) now needs its `Session.Rejoin` token, which it keeps across its drop, and the live-holder
replacement check (S2b POST) is owed with R2's paste-guest drop.
Env: typecheck and `verify.ps1 -Quick` only (no engine). Write `-Quick`'s output to a NEW log name each
run (a previous log can stay locked). Python edits: write the script to the scratchpad and run it (a
bash heredoc holding C# `$"..."` text failed to parse once); read/write with newline='' kept LF.

#### R2a PRE
- job R2a, tier opus (fresh agent from the handoff). Intent (narrowed from JOB 0 to what has a caller
  before the switch; the Pending entry's conn/name/made, `PretendAt` and GodotStub's WebRtc members move
  to R2b, whose edit first uses them -- `UNUSED ANYWHERE: 0` would refuse them here): `NetChannels.Beat`
  = 12, `NetBeat`/`NetBeatBack` on Net (Reliable, row 12), sent every `Link.BeatMs`; `Net.RoundTrip` =
  the least of the last 8 echoes (ENet's RoundTripTime read deleted); the watchdog (frame-capped
  silence per peer over `Link.QuietMs` 8000: the host `Hang`s, a guest takes `OnHostGone`); `Net.QuietMs`
  moves to `Link.QuietMs` (one constant); the harness's `StreamChannel` -> `NetChannels.Beat + 1` and
  every check asserting 12 channels / row 12 rewritten to 13.
- files: scripts/Net.cs 819f6e03, scripts/Link.cs 67b37be6, tools/smoketest/SmokeTest.cs.txt 8097b89d,
  ledger b9f545e9. HEAD 36d2cc3.
- checks planned: solo `R2BeatChecks` (the silence rule at 3 frame shapes: one 5 s stall, 60 short
  frames, a mix; the round trip as the least of the last 8 echoes, an older low one aged out; the beat's
  two RPCs ride row 12 Reliable); rewritten R0 channel checks (13 channels, Beat Reliable, stream 13,
  16 data channels); rung 5 guest: `Net.RoundTrip` above 0 and under 2 s once online a few beats.

#### R2a POST
- verdict: done; typecheck 0 errors (real GodotSharp.dll), `verify.ps1 -Quick` ALL CHECKS PASSED (0
  warnings, UNUSED ANYWHERE: 0). engine-unproven: rungs owed in the final test phase.
- Link.cs: `BeatMs` 500, `QuietMs` 8000 (Net's own deleted: one constant), `FrameCapS` 0.25, `Quiet`,
  `Overdue`, `Echoes` 8, `Trip` (least of the last 8). Net.cs: `NetChannels.Beat` = 12; `NetBeat` /
  `NetBeatBack` (AnyPeer, Reliable, row 12); `Beat(delta)` in `_Process` (host to each heard guest, a
  guest to its host; silence per peer; over QuietMs the host `Hang`s and stops sending to it, a guest
  takes `OnHostGone`); `RoundTrip` = `_trip.Least` (ENet's RoundTripTime read gone); cleared in
  `Shutdown`, a peer's silence forgotten in `OnPeer(left)`. Inside Net the class is named
  `global::Link` until R2b deletes Net's `Link(ENetPacketPeer,int)` (the CS0119 trap).
- Interim (ENet still under it until R2b): the watchdog runs beside ENet's own timeouts; the host's
  `Hang` is still ENet's PeerDisconnectLater. Both go with R2b.
- checks written (not run): solo `R2BeatChecks` (4: capped-frame silence at 3 shapes; the 8 s drop at
  3 silences; the least of the last 8 echoes; the beat's row), rung 5 `R2BeatGuest` (guest's round trip
  > 0 and < 2 s once let in, called in `Mp`'s guest after "guest sees the host"); rewritten R0 checks
  (13 channels with Beat Reliable on 12 and the stream on 13; 16 data channels each end; the highest
  row 13; ChannelOf with the beat's two RPCs on 12 and the stream on 13; the backlog text row 13).
- next: R2b, the switch.

### HANDOFF for the fresh agent (R2b onward) -- this agent stopped at ~140k after R2a
Read: the net2 HANDOFF above (S2 facts), this R2a POST, plan §3.1-3.9, §6, §7 (Net/Link/Rendezvous/
SessionMenu/Adapters/GodotStub rows + "Deleted in R2's edit"), §9, §10.2-10.4, §13 R2. Rendezvous.cs
449-757 (rows, `Pending`/`Entry`, `IHostDesk`/`IGuestDesk`, Listener, Dial, `Clipboard`) is the contract
Net implements: Net becomes BOTH desks (host: `Pending`, `Full`, `Invite(knock)` = a `Link.Gather`
with `stun:false`, `Hang(id)`, `Replied(reply)`, `Refused`; guest: `Knock()`, `Answer(invite)`).
Landmarks as of 143788c:
- Net.cs (~1040 lines): ENet in `Hang` (~185), `OnConnected`'s `Link(host,12000)`, `Link(ENetPacketPeer,int)`
  + its comment (~333-347; delete, then `global::Link` -> `Link` everywhere in Net), `_lettingGo`'s
  `(ENetMultiplayerPeer, until, server)` + `PumpLetGo` (~403-415, body -> WebRtcMultiplayerPeer polled
  until `GetPeers()` empty or 2 s, then Close; `server` flag and Host()'s loop over it deleted),
  the whole "internet hosting" block (`Reach`, `InternetAddress`, `LanAddress`, `OverlayAddresses`,
  `Ipv6Address`, `_hops`, `_hostGen`, `RouterJob`, `Describe`, `IsPublic`, `Reachable`, `AskPublicIp`,
  `FreeIpReq`, `StaleMappingsClosed`, `RouterResult`, `TryDecide`, `PublicIpService`, `Firewall`) ~484-660,
  `Host()` ~662, `ConnectTo` ~771 (ENet client -> `Join(text)` by `Rendezvous.PathFor`: paste row ->
  `Start(text, this)`; address row -> Dial), `Shutdown`'s ENet let-go ~808-823, `OnPeer`'s `Link(guest,
  10000)` ~842, `NetworkIdle` (-> no peer, no let-go, no listener). `CouldNotReach`: "use the host's room
  code" -> "ask the host for an invite code". `PretendProtocol` -> `PretendAt` flags `Code | Auth` (+ the
  pretend proto) per §3.8; harness users at SmokeTest ~11225-11231 (and Shots none).
- Rendezvous.Entry grows `Conn` (WebRtcPeerConnection or the Gather), `Name`, `Made` (ulong ms) in R2b.
- Router.cs -> Adapters.cs: keep only `Lan()`, `Overlays()`, `GlobalIpv6()` if Fit uses it, `IsIpv4`
  if still called; delete Fake, Hop, Report, Open, Close, FrontCandidates, IsCarrierGrade, IsShared.
- SessionMenu.cs (153 lines): COPY ADDRESS + reveal (lines 69-84, 110-122) go; add INVITE A FRIEND
  (hosting only), the reply box (a LineEdit -> `Net.I.TakeCode`), the pending list minimal, address labels
  "Same network: {lan}:{Rendezvous.ListenPort}" / "On {name}: {ip}:{port}"; the JOIN box takes invite or
  address; the guest's reply text + countdown + MAKE A FRESH REPLY.
- Game.cs:86 `routerBusy` -> `netBusy` (§7 row); Hub.cs:1340 comment names ENet (reword); Hub.cs:1620 ok.
- GodotStub.cs:148 `ENetMultiplayerPeer` -> minimal `WebRtcMultiplayerPeer`/`WebRtcPeerConnection` stub.
- Harness SmokeTest.cs.txt, lines naming ENet/Router/reach (delete or rewrite each): 22-119 (header
  comment, `NoRouterNoInternet`, `RouterScenarios`: DELETE, keep `P()`), 125 `HostAt` (drop the Wan +1000
  offset), 149-169 (`Drop` -> `Link.Hang` deferred; the throttle helper), 1221, 1317 (`GetMaxChannels`
  -> `Link.Channels().Length`, rule `r <= opened`), 1359-1380, 5704-5743 and 5840-5848 (reveal/describe
  checks: delete), 6056, 10602, 10885, 11063-11113 (throttle checks: delete), 11425, 11435, 11611, 11686.
  Roles per §10.3: `guest` joins by INVITE via the courier files (R1's courier: grep `Courier`/`invite-`),
  `guest2` by address (other-build knock refused over TCP, then `PretendAt.Auth` refused in-band, then
  joins); `aguest` address retries unchanged. Owed checks: `Net.I.JoinedBy` row per guest; paste guest's
  drop with no retry in 20 s + host's fresh invite + return into its held place WITH the rejoin token;
  the live-holder replacement (S2b owed); solo typed failures (127.0.0.1:9 "Nothing is hosting at" < 3 s;
  127.0.0.1:19481 "No answer from ... in 12 s" 11.8-12.6 s).
- run.ps1:226-227 starts fakeigd (delete + fakeigd.py); tools/rungs.ps1:25 comment names fakeigd's port;
  wan.py:2 mentions; Shots.cs.txt 321-325 (51-52's describe block: R3 replaces with 51_invite_ready /
  52_reply_countdown; R2b must at least delete the Router/Describe lines so it compiles).
Env: `.\verify.ps1 -Quick` is at the REPO ROOT (not tools\). Commit messages: write the file with
`[IO.File]::WriteAllText(path, text, (New-Object Text.UTF8Encoding $false))` -- Out-File's BOM lands in the
subject. Harness `Vary` takes floats (`Vary(3f, 20f)`).

#### R2b PRE
- job R2b, tier opus (fresh agent from the R2b handoff). HEAD 8d8c20c. Intent: THE SWITCH, game side in
  one edit: Net's session on `WebRtcMultiplayerPeer` (Host = `CreateServer(Link.Channels())` + both rows
  opened; Net is both desks; `Invite()` (paste row, walks STUN), `TakeCode(text)`, `Join(text)` by
  `Rendezvous.PathFor`; the Pending entry grows Conn/Name/Made; `LinkMs` deadline; `InviteLifeS`; the
  goodbye's WebRTC body; `Hang` -> `Link.Hang`; retries only for an `Auto` row; `PretendAt` flags
  Code|Auth); DELETE ENet, Router's UPnP family (Router.cs -> Adapters.cs: Lan, Overlays), describe/reach/
  reveal/public-IP, fakeigd.py + run.ps1's start, the router scenarios and reveal/describe/throttle
  checks, S1's `Net.Link`/throttle/`server` flag; `CouldNotReach` "ask the host for an invite code";
  SessionMenu INVITE A FRIEND / reply box / JOIN box (minimal; R3 adds the words and frames); Game.cs
  `netBusy`; GodotStub WebRtc stubs; harness `Drop` -> `Link.Hang` deferred, the channel check on
  `Link.Channels().Length`, `HostAt` without the +1000.
- SPLIT (default taken): the harness's ROLES MOVE in R2c (guest by invite through the courier, guest2's
  knocks, the paste guest's drop/return, the live-holder check, the solo typed failures, wan.py's ENet
  relay). R2b keeps every address-joining role compiling and meaningful: `Join(address)` now knocks the
  host's listener, which `Host(port)` opens from `port`.
- files: scripts/Net.cs 4afaa0da, scripts/Link.cs 12360a50, scripts/Rendezvous.cs 513eddbc,
  scripts/Router.cs 8649d1eb (-> Adapters.cs), scripts/SessionMenu.cs f5bcdcf5, scripts/Game.cs 0041e7a9,
  scripts/Hub.cs 4b2a8b23 (comment), typecheck/GodotStub.cs 0179d8a2, tools/smoketest/SmokeTest.cs.txt
  f5278188, tools/screens/Shots.cs.txt 20b45d75, tools/smoketest/run.ps1 b965bc88,
  tools/smoketest/fakeigd.py a8838032 (deleted), tools/rungs.ps1 d65c7956 (comment).
- checks planned: solo `R2SwitchChecks` (NetworkIdle offline and after GoOffline; a host's INVITE makes a
  paste-row entry Waiting with the invite's id, the invite text decodes to that id and the host's proto;
  the full text at MaxPlayers; a reply for an unknown id refused with §6.1's sentence; an invite pasted into
  the reply box refused; a reply pasted into JOIN refused; Join of another build's invite refused before
  any WebRTC object with the build text; Join of a damaged code; `Link.Hang` on a pending entry frees it);
  the rewritten channel check (`r <= Link.Channels().Length`); deleted with what they tested: router
  scenarios, reveal/describe/public-IP/stale-mapping checks, the two throttle checks (plan §9).

#### R2b POST
- verdict: done; typecheck 0 errors (real GodotSharp.dll), `verify.ps1 -Quick` ALL CHECKS PASSED (0
  findings, UNUSED ANYWHERE: 0). engine-unproven: rungs owed in the final test phase.
- Net: both desks. Host = `CreateServer(Link.Channels())` + every row opened (`Rendezvous.ListenFrom`
  swapped to `port` for the open, restored), `Addresses` for the panel; `Invite()` (paste row, walks),
  `Invite(knock)` (address row, no STUN), `MakeInvite` -> an Entry with a `Link.Gather`, sealed in
  `PumpSession` (Strip, Fit with `Adapters`, `Encode` + `Rendezvous.Copy` on the paste row); `TakeCode`,
  `Replied` (Waiting + gather Done only), Linking deadline `Link.LinkMs` -> hung up + a fresh paste invite;
  `InviteLifeS` sweep; `Full` = players + pending >= 8. Guest: `Join(text)` by `Rendezvous.PathFor`
  (reply / other build / damaged named before any peer), `Answer` = `CreateClient(invite.Id)` + a Gather
  on peer 1 (the invite's candidates added once sealed), `ReplyCode`/`ReplyAt`, `FreshFrom`/`FreshReply`;
  a paste guest's deadline = reply + ReplyWindowS + LinkMs; a timeout on the address row is left to the
  12 s/5 s deadline (so the silent port reads "No answer ... in 12 s"). `JoinedBy`, `_rowOf` (host).
  Every hang-up `Hang` -> `Link.Hang` (a live peer's deferred to the frame's end); goodbye = the hearer
  hangs up, `_lettingGo` polled until `GetPeers()` is empty or 2 s. `PretendAt` = Code|Auth
  (`Claimed(where)`). Paste guest: no retries, §3.9's text; host: a fresh invite on a paste guest's drop.
- Deleted: ENet everywhere, Router.cs (-> Adapters.cs: `Lan()`, `Overlays()`), describe/reach/reveal/
  public IP (api.ipify.org), `Net.Link`/throttle/`server` flag, fakeigd.py + run.ps1's start, wan.py's
  ENet relays + run.ps1's `--relay`, the router scenarios, the describe/IsPublic/stale-mapping/carrier-
  grade checks, the two throttle checks, the reveal check and Shots' describe block (51 kept as the host
  panel; 52 gone until R3). `Rendezvous.Clipboard` default reads only where the display server has a
  clipboard (headless ClipboardGet is an engine ERROR); `Rendezvous.Copy` is the write seam.
  `Rendezvous.Entry` grew Conn/Name/Made/Until/Code; `Pending.All`.
- Defaults taken: the paste guest's give-up is ReplyWindowS + LinkMs after its reply (the plan says "the
  countdown runs out first"; the host still needs its 12 s to link a reply taken at the last second).
  SessionMenu is minimal (INVITE A FRIEND, COPY INVITE, the reply box, the address labels, the guest's
  reply + seconds left, MAKE A FRESH REPLY); R3 sets §6's words and the frames.
- checks written (not run): solo `R2SwitchChecks` (JOIN: reply / another build's invite / PretendAt Code /
  damaged; HOST: 3 invites -> 3 entries, ids, codes; reply box: invite / stranger's reply / words / a real
  reply -> Linking / the same reply again / a hung-up entry's reply; full at 8; offline: all hung up,
  listener closed); rewritten: "127.0.0.1:9" -> "Nothing is hosting at" < 3 s, the silent TCP port
  P(19481) -> "No answer ... in 12 s" 11.8-12.6 s with "ask the host for an invite code", the mid-
  handshake check on P(19481), the channel check `r <= Link.Channels().Count`, the host's listener-port
  check (was the LAN address), `NetworkIdle` wording; `PretendProtocol` -> `PretendAt = Auth` in guest2.
- next: R2c, the roles moved (§10.3) and R2's owed rung-5 checks.

### HANDOFF for the fresh agent (R2c onward) -- this agent stopped at ~150k after R2b
Read: the R2b POST above, plan §3.3, §3.8, §3.9, §6, §10.2-10.4 (R2-R4 rows), §13 R2-R5. Net.cs's session
section (grep `── hosting`, `── the host's desk`, `── the guest's desk`, `PumpSession`, `── joining`) is
the API: `Net.I.Invite()`, `LastInvite`, `Pending` (`.All`, `.Find`), `TakeCode(text)`, `Join(text)`,
`ReplyCode`/`ReplyAt`, `FreshFrom`/`FreshReply()`, `JoinedBy` ("paste"/"address"), `Addresses`,
`Net.PretendAt = Net.Pretend.Code|Auth`, `Hang(id)`. The harness clipboard is `_Test.Clip` (static;
`HermeticNetwork()` swaps `Rendezvous.Clipboard`/`Copy` to it and `Link.Servers` to `BoxStun`).
Landmarks (SmokeTest.cs.txt as of dc6ac67): courier `CourierFile` 695, `Rewrite` 700 (the box pair),
R1's paste-row courier check ~850-900, role dispatch 1176, `ArenaMp` 10304, `Mp` 11014 (host 11017,
guest2 11222, guest after), `R2SwitchChecks` just above `S2HeldBossHost`.
**R2c · the roles moved (§10.3)** (files: SmokeTest.cs.txt, maybe Net.cs for a fix): `guest` joins BY
INVITE: the host role, once hosting, calls `Net.I.Invite()` and writes `Clip` (its invite) to
`CourierFile("invite", P(27115), n)`; the guest reads it, waits `Vary(0.5f, 3f)`, and puts it through the
JOIN box's handler (`SessionMenu`'s LineEdit TextSubmitted, or `Net.I.Join`); it writes `Net.I.ReplyCode`
to `CourierFile("reply", ...)`; the host puts that through the reply box's handler (`ReplyBox`
TextSubmitted -> `TakeCode`), and ONCE per run through the clipboard seam instead (set `Clip` to a
Discord-style `Message(reply)`: the pickup takes it). Under `-Wan` the courier `Rewrite`s both codes to
the box pair. `guest2` by ADDRESS: first `PretendAt = Code` -> refused over TCP before any peer (its text
"That host is on a different build of the game (theirs {build} {proto:x8}, yours ...)" and the host's
"Refused {name} on a different build ..." counted once; `refusals` at the host now counts both lines
starting "Refused": raise the host's expected count to 2 or split), then `PretendAt = Auth` refused
in-band (existing check), then joins. New checks: `Net.I.JoinedBy` == "paste" for guest, "address" for
guest2/aguest; the paste guest's drop (host `Drop(id)`): no reconnect attempt for 20 s (`!Net.I.Connecting
&& !Net.I.Reconnecting` throughout), guest status "Lost the connection to {hostName}. Ask them for a new
invite code: your place is held 90 s. Your own world keeps running."; the host's status "{name} dropped.
Send them this invite to come back (their place is held 90 s): COPY." and a new paste entry; the courier
carries the fresh invite; the guest lands back in its held place WITH its rejoin token (Hub's existing
"claimed its place with the rejoin token" line); the live-holder replacement (S2b owed: the guest rejoins
before the host's watchdog lets its old peer go -- e.g. guest `Net.SkipGoodbye` + GoOffline, then joins the
fresh invite at once; the host hands the place over, `_replacing`). Role limits in run.ps1 (120/150 s)
may need raising for the courier waits. `aguest` unchanged (address retries at 2/8/14 s, RECONNECT 19 s).
**R3** words (§6.1/6.2 table: most literals are already in Net.cs; add the listener-moved text if
`ListenPort != port`), COPY NETWORK REPORT (§6.3: `Net.Report()` + a SessionMenu button, per invite/reply
the STUN row + ms (`Gather.Row`, `DoneAt - Began`), candidates dropped by `Fit`, code length; per join
the times; per-row gap/backlog via `Link.Backlog`; ReplyWindowS; pickup on), Hints "multiplayer" row
(§6.3 literal), Shots: replace `51_address_hidden` with `51_invite_ready` + `52_reply_countdown`, add
`reply_expired` and `join_failed` at the next free numbers (grep `Snap("5` / `Snap("6`), checks for each.
**R4** the watchdog blackhole check (`Box("POST", "/box/blackhole?s=12")` after the guest's return under
-Wan: both ends drop at 8 +/- 1 s, the fresh invite brings it back) and the rate check at 150/40/5 with
per-row gap/backlog printed. **R5** pack/install/play/snapshot/manifest NOTES texts (§8), DESIGN, README
"Playing with a friend", CHANGES (Unreleased + Handoff + Known broken = §10.5) in the LAST job.
Env: `typecheck\typecheck.ps1` from typecheck/, `.\verify.ps1 -Quick` at the repo root (log to a new
name outside the tree). Python edits: write the script to the scratchpad with the Write tool (a bash
heredoc turns "\n" inside C# strings into real newlines: it broke SessionMenu once this job).

#### R2c PRE
- job R2c, tier opus (fresh agent from the R2c handoff). HEAD 4d90422. Intent: the roles moved (§10.3):
  `guest` joins BY INVITE through the courier (host posts `Message(LastInvite)` to invite-{P(27115)}-{n},
  the guest waits Vary(0.5,3) s and submits it to the JOIN box, posts its reply; the host takes reply 0
  through the clipboard seam, the rest through the ReplyBox); under -Wan the GUEST rewrites both codes to
  a box pair (it knows Wan; the host gets `wan` too for its waits); `guest2` knocks as another build
  (`PretendAt.Code`, refused over TCP, no WebRTC peer on any frame), then `Auth`, then joins; the host
  counts the two refusals apart; `JoinedBy` per role; the paste guest's drop (no attempt for 20 s, §3.9's
  text both ends, a fresh invite) and return into its held place with its token; the live-holder
  replacement (the guest orphans its live peer and joins a fresh invite at once: the host lets the old
  link go and hands the place over, no "dropped", no fresh invite). Net fix found reading: a peer let go
  because its pilot is back under another id is not a paste drop (no fresh invite). run.ps1: `$gx` to the
  host, role limit 120 -> 180 s for the three-player set, expected engine lines as named rows.
- files: scripts/Net.cs a18ed578, scripts/SessionMenu.cs 02b0223a (JOIN box named "JoinBox"),
  tools/smoketest/SmokeTest.cs.txt 4818ac3e, tools/smoketest/run.ps1 37cb0c81, ledger a0e9ab6a.
- checks planned: rung 5 host `R2cHostCourier` / `R2cHostReturns`, guest `R2cJoinByInvite` /
  `R2cGuestReturns`, guest2's other-build knock; rewritten: the host's refusal count (split in two), the
  guest's end (the drop now precedes "Host closed").

#### R2c POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED (0 findings, UNUSED ANYWHERE: 0).
  engine-unproven: rungs owed in the final test phase.
- Harness (SmokeTest.cs.txt, methods above `S2HeldBossHost`): `Post` (write whole, then move), `R2cPostInvite`,
  `R2cTakeReply` (clipboard seam or ReplyBox's TextSubmitted), `R2cJoinByInvite` (JoinBox's TextSubmitted;
  under -Wan the guest rewrites both codes and registers the box pair), `R2cHostCourier`, `R2cRows`,
  `R2cHostReturns`, `R2cGuestReturns`. `guest` joins by invite; its end reordered: the host DROPS it after the
  third player leaves (§3.9 text), its own-world checks run there, then 20 s of no attempt, the return by
  fresh invite, the live holder (its `_peer` orphaned by reflection: up, never polled, so the host sees
  silence, not a close), then "Host closed". `guest2` knocks with `PretendAt.Code` first (no WebRTC peer on
  any frame), then Auth, then joins; `JoinedBy` asserted for guest/guest2/aguest.
- Net fix: a paste peer let go while its pilot is back under another id (the rejoin token beat it) gets no
  fresh invite (`OnPeer`). SessionMenu: the JOIN box is named `JoinBox`.
- run.ps1: the host gets `wan` under -Wan (its waits), the three-player roles' limit 120 -> 180 s (20 s of
  no-retry, two returns), `$expected` named rows for engine chatter (the UPnP sentence gone).
- checks written (not run): rung 5 host "INVITE A FRIEND's invite went out by the courier ... taken off the
  clipboard", "one session serves both rows", "a paste guest that drops is held 90 s and gets a fresh
  invite", "came back by the fresh invite ... rejoin token", "the pilot back with its token while its old
  link still lived"; guest "joined by the host's invite ... within 30 s", "dropped from an invite ...",
  "tries nothing by itself for 20 s", "back in by the host's fresh invite", "back again while its old link
  still lived"; guest2 "its knock as another build is refused by the listener before any connection",
  "joined by the typed address"; aguest "joined, by the typed address". Rewritten: the host's refusal count
  (one at the knock, one in-band, counted apart), the guest's "Lost the connection ... held 90 s" (was
  "Host closed" at that point), guest2's joinWatch text (four joins).
- Test-phase watch: the live holder leans on the orphaned peer's native threads keeping the link up
  (libdatachannel); if the host sees a close instead, the check reads "again > 0" -- then orphan by
  blackhole under -Wan only.
- next: R3.

#### R3 PRE
- job R3, tier opus. HEAD 353203a. Intent: §6's words still missing (the listener moved / could not bind,
  appended to HOST's line; a SocketException from the OS-picked port no longer escapes Host), COPY NETWORK
  REPORT (§6.3: `Net.Report()`, a line per code made -- row, STUN row and ms, candidates the fit dropped,
  length -- and per join -- row, invite made, reply made/taken, connected, admitted; peak backlog per
  NetChannels row sampled 4 a second; the beat's longest silence; ReplyWindowS; pickup on/off), the pending
  list with COPY and CANCEL (§7 SessionMenu row; §6.1's full text sends the host to it), Hints'
  "multiplayer" row (§6.3 literal), Shots 51_invite_ready + 52_reply_countdown replacing 51_address_hidden,
  60_reply_expired, 61_join_failed (next free numbers). Game.cs already reads `netBusy` (nothing to do).
- Defaults: v1 §6.1's two listener texts are not in the repo, so they are written here (" Port {p} is taken
  on this PC, so friends typing an address use port {at}." / " No port could be opened for typed addresses,
  so friends join by invite only."); the report's "longest gap per row" is the beat's row only (the game
  cannot see which row a packet came in on; the harness times its own stream, R4).
- files: scripts/Net.cs f9150394, scripts/SessionMenu.cs 47ea0157, scripts/Hints.cs 87bce0b3,
  tools/smoketest/SmokeTest.cs.txt 38784fa1, tools/screens/Shots.cs.txt f2d2d599, ledger 959a60ad.
- checks planned: solo `R3WordsChecks` (listener moved at 3 varied ports/depths; the Linking deadline's
  text + fresh invite; the 15-min expiry text; CANCEL from full frees a place; the report's lines; COPY
  NETWORK REPORT on the clipboard; the hint literal; the guest's countdown-ran-out text + MAKE A FRESH
  REPLY at 3 varied invites; the connection-closed text); Shots frames 51, 52, 60, 61.

#### R3 POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED (0 findings, UNUSED ANYWHERE: 0).
  engine-unproven: rungs owed in the final test phase (3, 4 with LINT: 0, 5).
- Net: HOST's line carries the listener's outcome (moved: " Port {p} is taken on this PC, so friends typing an
  address use port {at}."; none: " No port could be opened for typed addresses, so friends join by invite
  only."), a SocketException from the OS-picked port no longer escapes Host; `Report()` (build, session,
  addresses, STUN rows + the walk's start, "reply window 30 s; clipboard pickup on", round trip + the beat's
  longest silence, peak backlog per NetChannels row sampled 4/s, a line per code made with its STUN row, ms,
  fit drops and length, a line per join with invite/reply/connected/admitted times, the last status);
  `CopyInvite` deleted (the pending list's COPY replaces it).
- SessionMenu: the pending list (`PendingList`, a row `Invite{id}` per entry: who, stage, COPY, CANCEL);
  COPY NETWORK REPORT (`CopyReport`); INVITE stays pressable when full (it says how to free a place).
- Hints "multiplayer": §6.3's literal. Game.cs already read `netBusy` (no change).
- Shots: 51_invite_ready, 52_reply_countdown, 60_reply_expired, 61_join_failed (51_address_hidden gone; a
  hermetic session: no STUN rows, the run's own clipboard).
- checks written (not run): solo `R3WordsChecks` (listener moved at 3 varied bases, 1-3 ports taken; a free
  port adds nothing; the Linking deadline's text + fresh invite; the 15-min expiry; the report's lines; COPY
  NETWORK REPORT; CANCEL from full; the hint; the guest's reply + countdown label, the countdown-ran-out text,
  MAKE A FRESH REPLY answering the same invite, at 2 varied invites; the closed-connection text).
- Not checkable in one process: the "no port could be opened" text (every port and the OS's own taken).
- next: R4.

#### R4 PRE
- job R4, tier opus. HEAD a58bcfb. Intent (§10.4 R4, -Wan only): the watchdog -- after the paste guest's
  return the guest blackholes the box for 12 s and posts the moment (UTC ms, the courier's file
  blackhole-{port}-0); both ends drop each other 8 +/- 1 s after it (the guest's §3.9 text, the host's
  "Player {id} dropped."); the host's fresh invite, posted once the blackhole lifts, brings it back (invite
  2; the live holder moves to 3 under -Wan). The rate check is S1's streamRate >= 8 (run at
  WARSHIPS_WAN=150,40,5 in the test phase); printed, not asserted: the stream row's longest gap over its
  12 s and each end's peak backlog per NetChannels row (Net.Report's line).
- files: tools/smoketest/SmokeTest.cs.txt 3d1f31df, ledger 4670d059.
- checks planned: rung 5 -Wan guest "the watchdog drops the host 8 +/- 1 s into a blackhole", host "the
  watchdog drops the guest 8 +/- 1 s into a blackhole", both "back by the fresh invite after it lifts".

#### R4 POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED. engine-unproven: rung 5 -Wan owed
  in the final test phase (default path, then WARSHIPS_WAN=150,40,5 for the rate check).
- Harness: `R4GuestBlackhole` / `R4HostBlackhole` (after the paste guest's first return, -Wan only: the guest
  posts `/box/blackhole?s=12` and its wall-clock start through the courier; each end's watchdog drop timed
  from it, 7-9 s; back by invite 2 after it lifts; the live holder moves to invite 3 under -Wan), `R4Backlog`
  (Net.Report's backlog + round-trip lines), `NetStream` keeps the longest wait between two. Printed under
  -Wan, not asserted: the stream row's longest gap over the rate check's 12 s and the guest's and the host's
  peak backlog per NetChannels row. The rate check is S1's (>= 8 a second), unchanged.
- checks written (not run): rung 5 -Wan guest "into a 12 s blackhole, its watchdog lets the host go 8 +/- 1
  s in", "back by the host's fresh invite once the blackhole lifted"; host "... lets the guest go 8 +/- 1 s
  in", "the fresh invite, carried after the blackhole lifted, brings the guest back into its place".
- next: R5 (packaging NOTES and the record; CHANGES in it, the lane's last job).

#### R5 PRE
- job R5, tier opus. HEAD 547fa86. Intent: packaging (§8, v1 R6) and the record (§13 R5), the lane's LAST
  job. pack.ps1: tools\import.ps1 before the export (SPIKE F1), the release DLL's presence after it, the
  seven licences copied into the export before its file listing (runtime part, no part rule), NOTES
  [PLAYING WITH A FRIEND] (+ the privacy line) and [THIRD-PARTY]; install.ps1: a presence check of every
  `in=` file, used by step 2 (an install missing a file is not "already installed") and after step 4;
  play.ps1: tools\import.ps1 before a launch from source; snapshot.ps1: the trimmed .gdextension is in the
  master copy. Record: DESIGN (the WebRTC section: plugin row, one STUN row per connection and the walk, no
  ICE restart, the reply window 30 s and its measurement, every channel reliable and per row, seal one
  poll after Complete, Link.Hang, the codec's template rule, the watchdog, the traps), README ("Playing
  with a friend"; the -Two text), CHANGES (Unreleased + Handoff + Known broken = §10.5; CLAUDE.md's rung-4
  frame count goes stale by the new frames).
- files: tools/pack.ps1 8ed20edb, tools/install.ps1 22492678, play.ps1 27ba61e4, tools/snapshot.ps1 bd84c158,
  docs/DESIGN.md 8545bdea, docs/README.md c6c0a1c1, docs/CHANGES.md 599d498b, ledger 03c6ea59.
- checks: packaging has no engine rung in this lane: `pack.ps1 -Dirty` (the export check) and the
  one-machine check are the test phase's (not this lane); rung 6 reads the manifest/snapshot.

#### R5 POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED (0 control characters). The five
  edited scripts parse (PowerShell's own parser). engine-unproven: `pack.ps1 -Dirty`, the one-machine check
  and rung 6 are the test phase's.
- pack.ps1: tools\import.ps1 -Require the plugin before the export; refuses an export without
  libwebrtc_native.windows.template_release.x86_64.dll; the seven LICENSE.* into `licences\` before the
  listing (runtime part); NOTES [PLAYING WITH A FRIEND] (+ the privacy line, W = 30) and [THIRD-PARTY].
  install.ps1: `Missing` (every `in=` file present), asked by step 2 and after unpacking. play.ps1: the
  import once (when extension_list.cfg lacks the plugin), by the console build; -Two's text names invites.
  snapshot.ps1: `.gdextension` in the master copy.
- Record: DESIGN (internet play rewritten for WebRTC; getting back in by row; goodbye; deadlines; traps:
  the UPnP/loopback ones deleted, round trip, GetPeer, Link.Hang, pre-handshake drop, the 8 s stall, rows,
  WebRTC close, no ICE restart, the seal, one STUN row per connection; the harness's network; the file
  table), README ("Playing with a friend", the install's presence check, Testing multiplayer), CHANGES
  (Handoff paragraph, the player summary, Unreleased entry with Known broken = §10.5 + the no-port text).
- Defaults recorded: [THIRD-PARTY] wording written here (v1 §8.4 is not in the repo); the release's file
  count and size are left to the next pack (README says so).
- LANE DONE: S2, R2 (a-c), R3, R4, R5 built; nothing run on the engine.

#### GATE FIX PRE
- job gate-fix, tier opus. HEAD 1035596. Intent: the opus merge gate's six findings, each code fix with its
  check (written, not run). (1) the rejoin token to the host alone (SendIdentity sends every other peer "");
  rung-5 third-player check that its copy of Guesty's identity never carried one. (2) P8's REFIT: one rule
  (Hub.RefitOpen, read by Hub.Refit, ResetShip, the class picker and the base menu's RESET); a class change
  keeps the hull fraction and does not refresh slots (PlayerShip.FitClass carries each slot's cooldown and
  count by id, run down by the game seconds it was away); solo checks at three situations. (3) the JOIN box
  fills itself from a copied invite (SessionMenu, PickupMs); solo check. (4) run.ps1: 205 s for the three
  roles under -Wan. (5) the report's per-join line gains "ICE connected" (the peer connection reaching
  Connected), the old "connected" reads "channels open"; R3WordsChecks wants both. (6) two stale router
  comments in SmokeTest.cs.txt (invariant C).
- files: scripts/Hub.cs 4feec202, scripts/PlayerShip.cs 2a913835, scripts/Net.cs 80af0a72,
  scripts/SessionMenu.cs 38dc6940, scripts/CharacterCreator.cs f108033a, scripts/BasePanel.cs 3d8362a6,
  tools/smoketest/SmokeTest.cs.txt 6101e98e, tools/smoketest/run.ps1 18b57ec1, tools/screens/Shots.cs.txt
  148de467, docs/CHANGES.md 8107904f, ledger 108a7a6f.

#### GATE FIX POST
- verdict: done; typecheck 0 errors, `verify.ps1 -Quick` ALL CHECKS PASSED. engine-unproven: rungs owed in
  the final test phase (3 twice, 4, 5 `six,six`, 5 `-Wan`).
- (1) Hub.SendIdentity: one RpcId per peer (Multiplayer.GetPeers(), or the one asked for); the token rides
  only to peer 1 from a guest, "" to everyone else. NetIdentity keeps what it heard in PlayerInfo.Token.
- (2) Hub.RefitOpen (the one rule) -> Hub.Refit (host), Hub.MayRefit (ResetShip, BasePanel's RESET reads
  "IN A FIGHT: NO RESET" and is shut), CharacterCreator's class card (Hub.Refit). PlayerShip.FitClass: the
  hull keeps its fraction (a dead ship still fits full, as before); every slot's Cool and N are kept by id
  in _slotsAway through class changes, Cool run down by the game seconds away; Left/Own end.
- (3) SessionMenu.FillJoinBox: open panel, not hosting, not Connecting, empty box: Rendezvous.Clipboard
  every PickupMs, an invite not the text read last time fills JoinBox.
- (4) run.ps1: host/guest/guest2 limit 205 s under -Wan, 180 otherwise. (5) JoinTimes Ice/Channels; the
  line reads "ICE connected", "channels open"; Ice stamped from PumpSession (host Linking entries, guest
  _answer), or at channels open when one poll brought both. (6) the two router comments rewritten.
- Checks written (not run): rung 3 `GateFixRefitChecks` ("at home in a fight, from 3 varied moments ...
  REFIT charges nothing and opens nothing, and a class picked in the creator is refused", "a class changed
  at home out of a fight keeps the hull's fraction", "a class change does not reset a cooldown"),
  `GateFixJoinBoxChecks` ("the JOIN box fills itself from a copied invite, 3 of 3 ... a reply or words
  leave it alone"), R3WordsChecks' wanted list + " ms, ICE connected never, channels open never, admitted
  never"; rung 5 guest2 `GateFixTokenWatch/Check` ("Guesty's identity reached it with no rejoin token"),
  host `GateFixJoinTimes` ("invite made, reply taken, ICE connected, channels open, admitted, in that
  order"); rung 4 frame 20_base_refit_in_combat (Shots `RefitInCombatFrame`).
- Watch in the test phase: FitClass no longer refills the hull or clears cooldowns on ANY class change, so
  an older solo check that relied on a class switch as a heal or a cooldown reset goes red there; fix it
  at rung 3 by setting what it needs (Hp, Sl(id).Cool) explicitly, never by restoring the old refill.
- Slip, undone: one PowerShell [IO.File] write with a relative path landed in WarShips_Version_L's
  tools/smoketest/run.ps1 (the same R4 edit); reverted there at once with git checkout (tree clean again).
- LANE DONE (gate fix): nothing run on the engine.

## JOB gate 2 comments (PRE)
- tier opus; intent: fix two stale Router comments (invariant C); files tools/smoketest/SmokeTest.cs.txt (07cbc10ddc980f0d94c04a41a9397bec5ef83dfd); HEAD feacfc45b50c0bf43572b7b2ffcea1d807befd9e
- POST: done; two comments/message reworded (line 5699 is a home router, kept); typecheck 0 errors, quick green; no engine run.

## JOB M3 merge version-l (PRE)
- tier opus; intent: merge version-l 9b05fd3 (kits A slices 1-3, art, drives, fields, curve, wings) into wt/net2; HEAD c389b45
- conflicted: tools/smoketest/SmokeTest.cs.txt, tools/smoketest/run.ps1, docs/CHANGES.md
- POST: resolved.
  - SmokeTest header: version-l's `Trimmed` helper kept; its old router-era HERMETIC NETWORK comment dropped (net2 rewrote it above HermeticNetwork).
  - arena guest join: net2's JoinedBy == "address" check kept, plus version-l's `await LaneBGuestDrives()`.
  - host mid-session level: version-l's curve literal 1.2725 (base 1.2875 was changed only there) + its hostRampHold cleanup, then net2's R2cHostReturns and the -Wan backlog print.
  - guest before the drop: version-l's guest-flown ramp block (D18), then net2's "host DROPS this guest" comment.
  - run.ps1: net2's $lim (205 under -Wan, else 180); version-l's side was the old literal 180.
  - CHANGES.md: Handoff = net2's entry + every version-l entry (net2's stale kits K2 entry dropped: version-l's K3 entry supersedes it); Unreleased keeps both sides.
  - Wire: no channel/enum collision (version-l changed no NetChannels; Beat = 12 stays net2's).
  - typecheck 0 errors, quick ALL CHECKS PASSED; no engine run.
