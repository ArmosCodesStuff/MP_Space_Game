using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// THE TRANSPORT: THE ONE PLACE THAT NAMES WEBRTC (docs/plans/network_webrtc.md §7). The official
// Godot webrtc-native plugin, vendored in addons/webrtc_native/, supplies the implementation of the
// WebRtc* classes that core GodotSharp declares; this file owns what the game needs to know about it:
// the plugin row and the presence test, the channel table (and which channel an RPC rides, and how
// much waits on it), the sealing rule, the one way to hang up, the STUN rows and the walk over them,
// and the transport's timings, the beat's included (QuietMs, BeatMs). It replaced the old transport's
// timeouts, throttle, round-trip statistic and disconnect: Net's session runs on WebRtcMultiplayerPeer.
//
// A PLUGIN UPGRADE IS A ROW: `Plugin` names the library, its version, the native class it registers
// and its files; nothing else in the game names them. It is a readonly record, so it is part of the
// build's fingerprint: two builds on different plugins do not meet. A NEW STUN SERVER IS A ROW of
// `Servers`, which is NOT readonly: which servers a PC asks is not part of what two builds agree on.
public static class Link
{
    public sealed record PluginRow(string Name, string Version, string NativeClass, string Extension, string ReleaseDll);
    public static readonly PluginRow Plugin = new("webrtc-native", "1.2.1", "WebRTCLibPeerConnection",
        "res://addons/webrtc_native/webrtc_native.gdextension", "libwebrtc_native.windows.template_release.x86_64.dll");

    // IS THE PLUGIN LOADED? Not a return code: with the DLL missing (or quarantined) the engine
    // still answers Initialize, CreateOffer and CreateServer with Ok, and only a data channel comes
    // back null (SPIKE F4). The class the library registers is the honest test, asked once.
    // Not a readonly field: this machine's files are not part of the build's fingerprint.
    public static bool Available => !PretendMissing && (_loaded ??= ClassDB.ClassExists(Plugin.NativeClass));
    private static bool? _loaded;
    // The smoke test's way to be a machine without the plugin, to prove the gate on a real menu.
    public static bool PretendMissing;
    // What the player reads where HOST and JOIN would be (plan §6.1, "plugin missing").
    public static string MissingText =>
        $"Multiplayer is off on this PC: its network library, {Plugin.ReleaseDll} ({Plugin.Name} {Plugin.Version}), did not load. "
        + "It must sit beside Warships.exe; an antivirus may have quarantined it. Run PLAY.bat again to put it back. Playing offline works as always.";

    // THE REPLY WINDOW (plan §3.4): how long after a friend makes its reply the host's game may take it
    // and still connect. The friend's side starts ICE's give-up clock when it pastes the invite, so the
    // reply has to come back inside that clock; the friend's countdown shows this number. MEASURED
    // (DESIGN.md, R0): an in-process pair over the LAN host candidate connected at every delay up to
    // 60 s; the rule is that longest delay less 10 s, never above 30. A const, so it is part of the
    // build's fingerprint, and a solo-run check holds a pair to it on every run.
    public const int ReplyWindowS = 30;

    // THE CHANNELS A SESSION NEGOTIATES, read off every [Rpc] in the game's assembly -- the harness's
    // own stream included, since both ends of a test run are one build (§3.2). Transfer channel N is
    // entry N - 1; a channel whose RPCs all ask for one unreliable mode gets it, anything else
    // (a Reliable RPC on it, two modes, no RPC at all) is Reliable. Channel 0 is the plugin's own
    // three. Each entry becomes its own data channel, so its own SCTP stream: a loss holds back only
    // that row (NetChannels says which stream is which).
    public static Godot.Collections.Array Channels()
    {
        var rpcs = Rpcs().Select(x => x.rpc).Where(a => a.TransferChannel > 0).ToList();
        int top = rpcs.Count == 0 ? 0 : rpcs.Max(a => a.TransferChannel);
        var modes = new Godot.Collections.Array();
        for (int ch = 1; ch <= top; ch++)
        {
            var asked = rpcs.Where(a => a.TransferChannel == ch).Select(a => a.TransferMode).Distinct().ToList();
            modes.Add((int)(asked.Count == 1 ? asked[0] : MultiplayerPeer.TransferModeEnum.Reliable));
        }
        return modes;
    }
    // Every [Rpc] in the game's assembly, by its method's name.
    private static IEnumerable<(string name, RpcAttribute rpc)> Rpcs()
    {
        const BindingFlags every = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return typeof(Link).Assembly.GetTypes().SelectMany(t => t.GetMethods(every))
            .Select(m => (m.Name, m.GetCustomAttribute<RpcAttribute>())).Where(x => x.Item2 != null);
    }
    // THE CHANNEL AN RPC RIDES, by its method's name, read the way Channels() reads it: for the backlog
    // guard (§3.8), which would skip a send whose row is backed up. -1 for a name no RPC has, or one
    // that RPCs on two channels share.
    public static int ChannelOf(string method)
    {
        var on = Rpcs().Where(x => x.name == method).Select(x => x.rpc.TransferChannel).Distinct().ToList();
        return on.Count == 1 ? on[0] : -1;
    }
    // HOW MANY BYTES WAIT TO LEAVE on one peer's channel (§3.8): every channel is reliable (§2.1), so a
    // lost datagram holds the rest of its row back, and this is where that shows. Transfer channel N
    // above 0 is the peer's data channel 2 + N (the three before it are channel 0's own); 0 reads
    // channel 0's reliable one. 0 for a peer or a channel that is not there. It counts only what waits
    // BEYOND the SCTP socket's own send buffer: a burst that fits inside that reads 0 (R1's first run:
    // 1 MB put in one frame, 0 read), so a guard on it acts only once a row is backed up that far.
    public static int Backlog(WebRtcMultiplayerPeer mp, int id, int channel)
    {
        if (mp == null || !mp.HasPeer(id)) return 0;
        using var peer = mp.GetPeer(id);
        var channels = peer["channels"].AsGodotArray();
        int at = channel <= 0 ? 0 : 2 + channel;
        if (at >= channels.Count) return 0;
        using var ch = channels[at].As<WebRtcDataChannel>();
        return ch?.GetBufferedAmount() ?? 0;
    }

    // ── the STUN rows and the walk (§5) ──────────────────────────────────────
    // A STUN ROW: a public server that tells this PC its internet address. Stateless: nothing to sign up
    // for, nothing that expires. There is no TURN row, no kind column and no candidate filter: the
    // ruling allows these two lookups and nothing else (§0.1, §5.1).
    public sealed record StunRow(string Url);
    // Walked in order, one per connection. Never readonly (see the header), and the harness swaps it for
    // the box's rows.
    public static StunRow[] Servers = { new("stun:stun.l.google.com:19302"), new("stun:stun.cloudflare.com:3478") };
    // The row the next walk starts at: the one that last answered in this process, or past the end when
    // none did, so that later gathers skip STUN rather than wait for it again. HOST and JOIN set it back
    // to 0.
    public static int StunFirst;
    public const int GatherMs = 2000;               // a STUN row that has not answered by then is passed over (§5.2)
    public const int LinkMs = 12000;                // a reply taken has this long to connect (§3.3 A6)
    public const int InviteLifeS = 900;             // an invite nobody answers is hung up after this (§3.3 A5)

    // ── the beat and the watchdog (§3.6) ─────────────────────────────────────
    // EVERY LINK BEATS: each end sends NetBeat every BeatMs on NetChannels.Beat, and the other echoes it.
    // A peer silent -- no beat and no echo -- for longer than QuietMs is dropped (the host hangs it up,
    // a guest takes its host as gone). Without it a hard-killed peer is noticed after 25-26 s (SPIKE F3).
    // QuietMs is S1's 8 s: what a stall on either machine (a first scene load, a collection pause, a
    // Wi-Fi roam) has to outlast.
    public const int BeatMs = 500;
    public const int QuietMs = 8000;
    // SILENCE IS COUNTED IN CAPPED FRAMES: a frame adds at most FrameCapS, so this machine's own stall
    // (one 5 s frame) is not the other end's silence. The watchdog judges the link, not the frame rate.
    public const double FrameCapS = 0.25;
    public static double Quiet(double silence, double delta) => silence + Math.Min(Math.Max(delta, 0), FrameCapS);
    public static bool Overdue(double silence) => silence * 1000 > QuietMs;
    // THE ROUND TRIP: the least of the last Echoes beats' echoes, in seconds (0 before the first). The
    // least, not the mean: a beat that waited behind a resend says nothing about the path.
    public const int Echoes = 8;
    public sealed class Trip
    {
        private readonly Queue<double> _last = new();
        public double Least => _last.Count == 0 ? 0 : _last.Min();
        public void Echo(double seconds)
        {
            if (seconds < 0) return;                        // a clock that ran backwards: nothing learned
            _last.Enqueue(seconds);
            while (_last.Count > Echoes) _last.Dequeue();
        }
    }

    // ONE STUN ROW PER CONNECTION (§5.2): libdatachannel shuffles its server list and uses the first STUN
    // entry, so two rows in one configuration would be a coin toss, not a fallback. A row outside the
    // table (-1, or past its end) is a configuration with no STUN at all: this PC's own addresses.
    public static Godot.Collections.Dictionary Config(int row)
    {
        var servers = new Godot.Collections.Array();
        if (row >= 0 && row < Servers.Length)
            servers.Add(new Godot.Collections.Dictionary { ["urls"] = new Godot.Collections.Array { Servers[row].Url } });
        return new Godot.Collections.Dictionary { ["iceServers"] = servers };
    }

    // THE WALK (§5.2): one side's bundle -- an invite's offer, or a reply's answer -- gathered on one STUN
    // row at a time. Start at StunFirst; gather; seal (Sealed: Complete and one poll, or GatherMs when the
    // row never answers). A server-reflexive candidate ends the walk and makes its row StunFirst. None,
    // and the connection is hung up and made again on the next row, WITH THE SAME ID. No row answering
    // leaves this PC's own addresses with NoStun set, and moves StunFirst past the end. A row that does
    // not walk (the typed address: a LAN or an overlay, where host candidates are the path) gathers with
    // no STUN row at all, in 12-14 ms (SPIKE).
    // A GUEST ADDS THE INVITE'S CANDIDATES ONLY ONCE ITS WALK IS DONE: a guest that knows where the host
    // is may reach it and start its DTLS handshake before the host has the reply (P2), and a connection
    // made again under that handshake prints two plugin ERROR lines (DESIGN.md traps). Without the
    // host's candidates, a connection being walked has nothing to check.
    public sealed class Gather
    {
        public readonly WebRtcMultiplayerPeer Mp;
        public readonly int Id;
        private readonly Action<WebRtcPeerConnection> _describe;   // makes this side's description: CreateOffer, or the offer set
        public WebRtcPeerConnection Conn { get; private set; }
        public int Row { get; private set; }                      // the STUN row in use; -1 for none
        public Error Added { get; private set; }                  // the last AddPeer: a connection made again gets its id back at once
        public string Sdp { get; private set; } = "";
        private readonly List<string> _arrived = new();
        public IReadOnlyList<string> Arrived => _arrived;         // every candidate line so far, sealed or not
        public string[] Lines { get; private set; } = Array.Empty<string>();   // the sealed bundle's candidate lines
        public bool Done { get; private set; }
        public bool NoStun { get; private set; }
        public readonly ulong Began = Time.GetTicksMsec();
        public ulong DoneAt { get; private set; }
        // every seal: by Complete or by the clock, the row's deadline, when it happened, and the step before it
        public readonly List<(bool complete, ulong deadline, ulong at, ulong before)> Seals = new();
        private ulong _rowAt, _stepAt;

        public Gather(WebRtcMultiplayerPeer mp, int id, bool stun, Action<WebRtcPeerConnection> describe)
        {
            Mp = mp; Id = id; _describe = describe;
            Row = stun && StunFirst >= 0 && StunFirst < Servers.Length ? StunFirst : -1;
            _stepAt = Began;
            Make();
        }
        private void Make()
        {
            var c = new WebRtcPeerConnection();
            Conn = c; _arrived.Clear(); Sdp = ""; _rowAt = Time.GetTicksMsec();
            c.Initialize(Config(Row));
            c.SessionDescriptionCreated += (type, sdp) => { if (c != Conn) return; c.SetLocalDescription(type, sdp); Sdp = sdp; };
            c.IceCandidateCreated += (media, index, name) => { if (c == Conn) _arrived.Add(name); };
            Added = Mp.AddPeer(c, Id);
            _describe(c);
        }
        // Once a frame, after the peer has been polled. True once the bundle is final.
        public bool Step()
        {
            if (Done || Conn == null) return Done;
            ulong now = Time.GetTicksMsec(), deadline = _rowAt + GatherMs;
            bool complete = Sealed(Conn);
            if (complete || now >= deadline)
            {
                Seals.Add((complete, deadline, now, _stepAt));
                bool srflx = _arrived.Any(l => l.Contains(" typ srflx", StringComparison.Ordinal));
                if (srflx) { StunFirst = Row; Finish(false); }
                else if (Row < 0) Finish(true);
                else if (Row + 1 < Servers.Length) { Close(); Row++; Make(); }
                else { StunFirst = Servers.Length; Finish(true); }
            }
            _stepAt = now;
            return Done;
        }
        private void Finish(bool noStun)
        {
            Done = true; NoStun = noStun; DoneAt = Time.GetTicksMsec(); Lines = _arrived.ToArray();
        }
        // Lets this connection go: off the peer, closed, and released here and now rather than by the
        // collector's thread (Assets.cs: a handle released there can turn into an engine ERROR). Once.
        public void Close()
        {
            if (Conn == null) return;
            Hang(Mp, Id);
            Conn.Close();
            Conn.Dispose();
            Conn = null;
        }
    }

    // A BUNDLE IS SEALED ONE POLL AFTER GATHERING SAYS COMPLETE (v1 §2.6 S2): the last candidates
    // arrive through signals inside the poll that reports Complete, so a bundle read in that same
    // frame can miss one. Call once a frame after the connection's peer has been polled; true from
    // the first frame after the one that first saw Complete.
    public static bool Sealed(WebRtcPeerConnection c)
    {
        ulong id = c.GetInstanceId(), frame = Engine.GetProcessFrames();
        if (c.GetGatheringState() != WebRtcPeerConnection.GatheringState.Complete) { _completeAt.Remove(id); return false; }
        if (_completeAt.TryGetValue(id, out ulong at)) return at < frame;
        foreach (var gone in _completeAt.Keys.Where(k => !GodotObject.IsInstanceIdValid(k)).ToList()) _completeAt.Remove(gone);
        _completeAt[id] = frame;
        return false;
    }
    private static readonly Dictionary<ulong, ulong> _completeAt = new();

    // EVERY HANG-UP GOES THROUGH HERE (v1 §2.6 S3): the plugin's remove_peer prints an engine ERROR
    // for an id it does not have, and a run with an ERROR line is a red run. A gone id is a no-op.
    public static void Hang(WebRtcMultiplayerPeer mp, int id)
    {
        if (mp != null && mp.HasPeer(id)) mp.RemovePeer(id);
    }
}
