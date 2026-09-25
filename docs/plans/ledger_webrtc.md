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
- checkpoint: the J7 commit ("R1 J7: ...")
- next: J8
