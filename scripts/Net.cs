using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// ─────────────────────────────────────────────────────────────────────────────
// NET — the authority model, and the one thing in this project that must be
// right from day one, because retrofitting it is what kills games.
//
// THE RULE, in one line: the HOST owns the world, each PLAYER owns their ship.
//
//   host authority   : the economy (gathering, the hauler, sales, upgrades), raiders,
//                      the boss and its attacks, damage, missions and rewards --
//                      anything a client could cheat
//   client authority : its own ship's thrust and heading, and nothing else
//
// A client never tells the host "I now have 500 ore". It tells the host "I am
// pressing thrust", and the host tells everyone where things ended up. Every
// number that matters is computed on the host and replicated down.
//
// Offline is not a separate mode: single player is simply a host with no peers,
// so there is exactly ONE code path and offline can never drift from online.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Net : Node
{
    private const int DefaultPort = 27015;
    private const int MaxPlayers = 8;

    public static Net I { get; private set; }

    // Single player runs as a host with nobody connected, so IsHost is true offline -- and while
    // a join is still connecting: until a host has actually answered, this is still your world.
    public static bool IsHost => I == null || I._isHost;
    // Connected, not merely "a peer object exists": while a join is still handshaking
    // there is nobody to send to, and an RPC then is an engine error.
    public static bool IsOnline => I != null && I._peer != null
        && I._peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    public static int LocalId => I == null ? 1 : I._localId;

    private bool _isHost = true;
    private int _localId = 1;
    private MultiplayerPeer _peer;
    // From a session starting (hosting, or a host letting us in) until it ends. What "there was a
    // session to leave" means -- IsOnline cannot say it: by the time the multiplayer layer reports
    // a host gone or a connection failed, the peer already reads Disconnected, so the leaving
    // routes that mattered most were the ones that never saved.
    private bool _inSession;
    // JOIN pressed, no host has answered yet (the address is looked up, then connected to).
    public bool Connecting { get; private set; }

    // Everyone in the session, host included. Keyed by peer id.
    public readonly Dictionary<int, PlayerInfo> Players = new();
    // Identity is the one piece of player state that is not host-owned: each owner
    // announces its own, and everyone stores it here so a ship spawned later still
    // gets the right name, colours, class, pilot upgrades and gear.
    public class PlayerInfo
    {
        public string Name = Character.Defaults.Name;
        public Color Main = Character.Defaults.Main, Accent = Character.Defaults.Accent;
        public ShipClass Class = ShipClass.Battleship;
        public int[] Bought = Array.Empty<int>();
        public string[] Equip = Array.Empty<string>();
        public string CharacterId = "";                   // the pilot's stable identity: a peer id changes on a reconnect
        public bool HasIdentity;
    }

    public event Action<int> PlayerJoined;
    public event Action<int, PlayerInfo, bool> PlayerLeft;          // peer, who it was, whether it said goodbye
    public event Action<string> Status;

    // Fired whenever LocalId or the host/guest role changes: offline, hosting,
    // connected, dropped. A world rebuilds its ships on this, because every ship is
    // keyed and authorised by peer id and a stale id means flying someone else's hull.
    public event Action SessionChanged;

    // The last status line, so a menu opened after the fact still shows it.
    public string LastStatus { get; private set; } = "";

    public override void _Ready()
    {
        I = this;
        // The batched save, the goodbye's last second and the reconnect clock all run here, and
        // Game.Quit pauses the tree while it waits for exactly those.
        ProcessMode = ProcessModeEnum.Always;
        var sm = (SceneMultiplayer)Multiplayer;
        // THE HANDSHAKE, before a peer counts as connected at all (see Protocol).
        sm.AuthCallback = Callable.From<long, byte[]>(OnAuth);
        sm.AuthTimeout = 10;                               // an internet round trip, with room to spare
        sm.PeerAuthenticating += id => sm.SendAuth((int)id, BitConverter.GetBytes(PretendProtocol ?? Protocol));
        Multiplayer.PeerConnected    += id => OnPeer((int)id, true);
        Multiplayer.PeerDisconnected += id => OnPeer((int)id, false);
        Multiplayer.ConnectedToServer += OnConnected;
        // A failed or dropped connection falls back to offline -- and a DROP is retried (OnHostGone).
        // Without this a guest sat forever with IsHost false: the hub stopped and nothing said why.
        Multiplayer.ConnectionFailed   += () => Failed(CouldNotReach);
        Multiplayer.ServerDisconnected += OnHostGone;
        GoOffline();
        // The close button goes through Game.Quit like every other way out. This is the one node
        // that always exists, and what it owns -- the socket and the router ports -- is what a
        // closed window must not strand.
        GetTree().AutoAcceptQuit = false;
    }

    // This node outlives every scene: the batched save's clock, the goodbye's last second, and the
    // reconnect attempts.
    public override void _Process(double delta)
    {
        Character.TickSave(delta);
        PumpLetGo();
        // Every attempt has a hard deadline. ENet looks at its own timeout only when a resend falls
        // due, and its resends double: a JOIN set to give up in 12 s gave up after 15, a retry
        // set to 5 after 7.5.
        if (Connecting && Time.GetTicksMsec() - _attemptAt > (ulong)(BackIn ? RetryTimeoutMs : JoinTimeoutMs)) { Failed(CouldNotReach); return; }
        if (_dropClock < 0 || GetTree().Paused) return;
        _dropClock += delta;
        if (!Connecting && Attempt < RetryAt.Length && _dropClock >= RetryAt[Attempt]) { Attempt++; Connect(LastHost, retry: true); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Game.Quit();
    }

    // ── the build handshake ──────────────────────────────────────────────────
    // WE VERSION SAVE FILES, AND SESSIONS HAVE TO MATCH TOO -- more exactly, because two builds
    // can disagree in ways a save never sees. Godot addresses a node's RPCs by their place in a
    // sorted list, so a build that adds or renames one shifts every later one: an older guest's
    // packets call the neighbouring method, and every balance number it shows is a quiet lie.
    //
    // So the handshake compares a FINGERPRINT of the build, not a number anyone has to remember
    // to bump: every RPC's name, arguments and delivery, every constant and fixed value in the
    // game's types (prices, timings), every table of them (gear parts, upgrades, bosses), and
    // every class's stat sheet. The same zip on two machines agrees; a changed
    // number or message on either side does not. It runs in Godot's authentication step, BEFORE
    // the peer counts as connected: nothing is spawned for it, sent to it or relayed about it
    // until both sides have seen the other's fingerprint -- and each side refuses on its own, so
    // the refused player learns why without any message having to survive a disconnect.
    public static readonly int Protocol = Fingerprint();
    public static bool Accepts(int protocol) => protocol == Protocol;
    // The smoke test's way to be a different build, to prove the refusal on a real connection.
    public static int? PretendProtocol;

    private void OnAuth(long id, byte[] data)
    {
        int theirs = data.Length == 4 ? BitConverter.ToInt32(data) : -1;
        var sm = (SceneMultiplayer)Multiplayer;
        if (Accepts(theirs) && PretendProtocol == null) { sm.CompleteAuth((int)id); return; }
        // NOT IsHost: a player still connecting keeps the host role over its own world until a
        // host has let it in -- which is exactly the moment this runs.
        if (!Connecting)
        {
            Say($"Refused a player on a different build of the game (theirs {theirs:x8}, this one {Protocol:x8}).");
            (_peer as ENetMultiplayerPeer)?.GetPeer((int)id)?.PeerDisconnectLater();
        }
        else GoOffline($"That host is on a different build of the game (theirs {theirs:x8}, yours {PretendProtocol ?? Protocol:x8}): "
                     + "you both need the same zip. Playing offline.");
    }

    private static int Fingerprint()
    {
        // Invariant culture throughout: a record prints its numbers in the machine's own culture,
        // and a German PC and an English one must not disagree about "1,5" and "1.5".
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try { return Fingerprint(new List<string> { $"save {Game.Version}" }); }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }
    private static int Fingerprint(List<string> parts)
    {
        foreach (var t in typeof(Net).Assembly.GetTypes().Where(t => t.Namespace == null && !t.Name.StartsWith("_")).OrderBy(t => t.FullName))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var m in t.GetMethods(all).OrderBy(m => m.Name))
                if (m.GetCustomAttribute<RpcAttribute>() is { } a)
                    parts.Add($"{t.Name}.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) {a.Mode} {a.TransferMode} {a.TransferChannel}");
            foreach (var f in t.GetFields(all & ~BindingFlags.Instance).OrderBy(f => f.Name))
                if ((f.IsLiteral || f.IsInitOnly) && Plain(f.FieldType))
                    parts.Add($"{t.Name}.{f.Name}={Show(f.GetValue(null))}");
        }
        // THE SHEETS: each class's base numbers are written in ShipStats' constructor, not a field.
        foreach (ShipClass c in Enum.GetValues(typeof(ShipClass)))
            foreach (var s in new ShipStats(c).All) parts.Add($"sheet {c}.{s.Id}={Show(s.Base)}{(s.Inverse ? " inverse" : "")}");
        uint h = 2166136261;                              // FNV-1a: stable across processes, unlike GetHashCode
        foreach (char c in string.Join("\n", parts)) { h ^= c; h *= 16777619; }
        return (int)h;
    }
    // Values whose text is the same on every machine: numbers, words, colours and vectors, and
    // records, tables and arrays of them. Not engine objects, and not the game's live collections
    // (a list or dictionary field is what a run fills in -- the key bindings, the character).
    private static bool Plain(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(Color) || t == typeof(Vector2)
        || (t.IsArray && Plain(t.GetElementType()))
        || (t.GetMethod("<Clone>$") != null && !typeof(GodotObject).IsAssignableFrom(t))
        || Table(t);
    // A TABLE ROW: one of the game's own data classes (a gear part, an upgrade, a boss type). Held in
    // a readonly field it is as fixed as a constant -- but a class, so it has no text of its own:
    // its public fields are written out, dictionaries sorted by key.
    private static bool Table(Type t) =>
        t.IsClass && t != typeof(string) && t.Assembly == typeof(Net).Assembly
        && !typeof(GodotObject).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null;
    private static string Show(object v) => Show(v, 0);
    private static string Show(object v, int depth) => v switch
    {
        null => "null",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        GodotObject g => g.GetType().Name,
        Array a => "[" + string.Join(",", a.Cast<object>().Select(x => Show(x, depth))) + "]",
        System.Collections.IDictionary d => "{" + string.Join(",", d.Keys.Cast<object>()
            .Select(k => Show(k, depth) + ":" + Show(d[k], depth)).OrderBy(x => x, StringComparer.Ordinal)) + "}",
        _ when depth < 4 && Table(v.GetType()) => v.GetType().Name + "{" + string.Join(",", v.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name).Select(f => f.Name + "=" + Show(f.GetValue(v), depth + 1))) + "}",
        _ => v.ToString(),
    };

    private void OnConnected()
    {
        _localId = Multiplayer.GetUniqueId();
        _isHost = false; Connecting = false; _inSession = true;
        // the host that let us in is the one to come back to; an unreachable one is given up on in 12 s
        LastHost = _joinAddress; _dropClock = -1; Attempt = 0; CanReconnect = false; _rejoin = false; _hostSaidBye = false;
        (_peer as ENetMultiplayerPeer)?.GetPeer(1)?.SetTimeout(32, 4000, 12000);
        Players.TryAdd(_localId, new PlayerInfo());
        Say($"Connected as player {_localId}.");
        SessionChanged?.Invoke();
    }

    // ── session lifecycle ────────────────────────────────────────────────────

    // Offline play. Deliberately the same path as hosting: one set of rules. Leaving on purpose --
    // PLAY OFFLINE, the host closing the session, a refused build -- or giving up on a connection
    // lands here, and ends any reconnecting.
    public void GoOffline(string reason = null) { StopReconnecting(); Offline(reason); }

    // Back to offline. The world is told only if something changed: a failed attempt to join (or
    // to get back in) from offline changed nothing, and rebuilding for it swapped the pilot's own
    // ship for a fresh one.
    private void Offline(string reason)
    {
        bool changed = _inSession || !_isHost || _localId != 1 || Players.Keys.Any(k => k != 1);
        Close();
        _isHost = true; _localId = 1; Connecting = false;
        Players[1] = new PlayerInfo();
        Say(reason ?? "Offline session.");
        if (changed) SessionChanged?.Invoke();
    }

    // ── the goodbye ─────────────────────────────────────────────────────────
    // ENet reports a host that closed and a host that vanished the same way, and a guest that
    // quit the same way as one whose Wi-Fi dropped. So whoever leaves on purpose says so first:
    // a guest told goodbye does not try to get back in, and a host told goodbye does not hold the
    // pilot's place. The goodbye must actually leave: the peer is let go gently -- ENet sends what
    // is queued, then disconnects -- and pumped for up to a second.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetBye()
    {
        int from = Multiplayer.GetRemoteSenderId();
        if (!_isHost && from == 1) _hostSaidBye = true;
        else if (_isHost && Players.ContainsKey(from)) _leaving.Add(from);
    }
    public static bool SkipGoodbye;                        // the smoke test's host that vanishes (a crash, a pulled cable)
    private bool _hostSaidBye;
    private readonly HashSet<int> _leaving = new();
    private bool _peerGone;                                  // the other end is already gone: nobody to say goodbye to
    private bool SayGoodbye()
    {
        if (!_inSession || Connecting || !IsOnline || SkipGoodbye || _peerGone) return false;
        if (_isHost) Rpc(nameof(NetBye)); else RpcId(1, nameof(NetBye));
        return true;
    }
    private ENetMultiplayerPeer _letGo;
    private ulong _letGoUntil;
    private const ulong LetGoMs = 1000;
    private void PumpLetGo()
    {
        if (_letGo == null) return;
        if (_letGo.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected) { _letGo = null; return; }   // already let go
        bool busy = _letGo.Host != null && _letGo.Host.GetPeers().Any(p => p.GetState() != ENetPacketPeer.PeerState.Disconnected);
        if (!busy || Time.GetTicksMsec() >= _letGoUntil) EndLetGo(); else _letGo.Poll();
    }
    private void EndLetGo() { if (_letGo == null) return; _letGo.Close(); _letGo = null; }

    // ── reconnecting ────────────────────────────────────────────────────────
    // A guest whose host drops without a goodbye goes offline -- its own world keeps running --
    // and tries the same host again 3 times over about 20 s. Then RECONNECT, one more try on the
    // button. The host holds the pilot's place meanwhile (Hub: 90 s).
    public static readonly double[] RetryAt = { 2.0, 8.0, 14.0 };   // seconds after the drop
    private const int RetryTimeoutMs = 5000, JoinTimeoutMs = 12000;
    private bool BackIn => _retrying || _rejoin;           // a try to get back in to the host just lost, not a JOIN
    private string CouldNotReach => $"Could not reach {_joinTarget}. Check the address; if the host plays from home, "
                                  + "their MULTIPLAYER panel says what their router still needs. Playing offline.";
    public string LastHost { get; private set; } = "";
    private string _joinAddress = "";
    public int Attempt { get; private set; }
    private double _dropClock = -1;
    public bool Reconnecting => _dropClock >= 0;
    public bool CanReconnect { get; private set; }
    private bool _retrying, _rejoin;
    private ulong _attemptAt;

    private void OnHostGone()
    {
        _peerGone = true;
        // Still in the handshake (Godot reports a link that came up and then went as "disconnected",
        // not "failed"): an attempt that did not happen, not a session lost.
        if (Connecting) { Failed($"Could not get in to {_joinTarget}: it let the connection go before the handshake finished. Playing offline."); return; }
        if (_hostSaidBye) { GoOffline("Host closed the session. Playing offline."); return; }
        StopReconnecting();
        Offline($"Lost the connection to {_joinTarget}. Trying to get back in, {RetryAt.Length} times in the next 20 s. Your own world keeps running.");
        if (LastHost.Length > 0) _dropClock = 0;
    }

    // A connection that did not happen: a retry that failed, the RECONNECT button's try, or a JOIN.
    private void Failed(string reason)
    {
        if (_rejoin) { _rejoin = false; Offline($"Could not get back in to {LastHost}. RECONNECT to try again."); CanReconnect = true; return; }
        if (!Reconnecting) { GoOffline(reason); return; }
        Offline($"Lost the connection to {LastHost}: attempt {Attempt} of {RetryAt.Length} failed.");
        if (Attempt < RetryAt.Length) return;
        _dropClock = -1; CanReconnect = true;
        Say($"Could not get back in to {LastHost}. RECONNECT to try again.");
    }

    private void StopReconnecting() { _dropClock = -1; Attempt = 0; CanReconnect = false; _rejoin = false; }

    public void Reconnect()
    {
        if (!CanReconnect) return;
        CanReconnect = false; _rejoin = true;
        Connect(LastHost, retry: false);
    }

    // The session ends; nothing else. Game.Quit calls this alone -- there is no world to fall
    // back to -- and GoOffline calls it first. It saves the world only when there WAS a session to
    // leave: this also runs at boot and after a failed Host()/Join(), and saving the world there
    // once wrote a freshly built default base over a good file. A batched save still waiting
    // (Character.SaveSoon) is written every time -- it holds only the pilot's own statics.
    public void Close()
    {
        if (_inSession) SaveLocalCharacter();
        Character.SaveIfPending();
        bool bye = SayGoodbye();
        _inSession = false;
        _joinGen++;                                        // a lookup still in flight connects to nothing
        Shutdown(bye);
    }

    // ── internet hosting ────────────────────────────────────────────────────
    // Hosting starts at once for your own network. Meanwhile a background thread asks your
    // routers to forward the port to this PC (Router: UPnP, NAT-PMP, PCP, and the router in front
    // of yours if there is one), and a web request asks a public "what is my IP" service what the
    // internet sees; the answers together decide what friends elsewhere need (see Describe). The
    // background thread writes NOTHING: it hands its report back to the main thread, which keeps
    // it only for the current session -- ports opened for a session that has ended are closed.
    public enum Reach { None, Checking, Internet, Manual, LanOnly }
    public Reach Reachability { get; private set; } = Reach.None;
    public string InternetAddress { get; private set; } = "";
    public string LanAddress { get; private set; } = "";
    // For friends on a virtual network with you (Tailscale, ZeroTier, Radmin VPN, Hamachi), and
    // for friends with IPv6 -- the one way in left when carrier-grade NAT closes every IPv4 door.
    public List<(string name, string address)> OverlayAddresses { get; private set; } = new();
    public string Ipv6Address { get; private set; } = "";
    private List<Router.Hop> _hops = new();
    private int _hostGen;

    // Every call to a router runs on a background thread (a router can take seconds), and every
    // one is counted: NetworkIdle, and Game.Quit's wait, mean "none still running". The count is
    // kept on the main thread only -- the job hands its decrement back through the same queue as
    // its result, so the result (which may start the job that closes its ports) always lands
    // first, and "none running" can never be seen between the two.
    private int _routerJobs;
    private void RouterJob(Action job)
    {
        _routerJobs++;
        System.Threading.Tasks.Task.Run(() =>
        {
            try { job(); }
            finally { if (!Game.ShuttingDown) Callable.From(() => _routerJobs--).CallDeferred(); }
        });
    }

    // ── what friends elsewhere need: decided from the routers' report AND the public address ──
    //   INTERNET   every router on the way opened the port: friends join <public>:<port>
    //   MANUAL     a router would not open it -- ours (UPnP off), or the one in front of ours (two
    //              routers, the second silent) -- or this PC's traffic leaves by a VPN: one step
    //              by hand, then <public>:<port>
    //   LAN ONLY   carrier-grade NAT, or nothing could be learned at all
    // The status line never prints the public address: the panel shows it behind a reveal.
    //
    // Where the public address comes from. The smoke test points it somewhere that cannot answer,
    // so that "no internet" is a scenario it sets up rather than a fact about the machine.
    public static string PublicIpService = "https://api.ipify.org";
    private HttpRequest _ipReq;
    private string _publicIp = "";
    private Router.Report _report;
    private bool _publicDone;
    private int _hostPort;
    // single player (and between sessions): no socket, no lookup, no router job
    public bool NetworkIdle => _peer == null && _ipReq == null && _routerJobs == 0 && _letGo == null;

    private const string Firewall = " If friends still cannot get in, allow Warships (Godot) through the Windows firewall, for private AND public networks.";

    // `routerExt`: what the router that opened the port says its internet side is ("" if it would
    // not say, or nothing opened). `why`: why nothing opened. `front`: ours opened, but sits behind
    // a router that did not -- this is our router's internet side, where that one has to forward.
    public static (Reach reach, string address, string message) Describe(string routerExt, bool mapped, string publicIp, int port, string lan,
                                                                         string why = "", string front = "")
    {
        bool pub = IsPublic(publicIp);
        string pc = lan.Split(':')[0];
        if (mapped)
        {
            // TWO ROUTERS, the second silent. One forward there finishes the path, and the game
            // opens its own router every session after that -- so this names the exact address,
            // which nobody would guess (it is not this PC's).
            if (front.Length > 0 && pub)
                return (Reach.Manual, $"{publicIp}:{port}",
                        $"Hosting. Your router opened port {port}, but it sits behind a second router (usually your internet provider's modem) "
                      + $"that did not answer. Once, in THAT router's settings: forward UDP {port} to {front} -- or put {front} in its DMZ, or "
                      + "switch it to bridge mode. After that, friends anywhere join the address below, every time." + Firewall);
            // The port IS open; the router just would not say what its outside address is.
            if (routerExt.Length == 0 && pub)
                return (Reach.Internet, $"{publicIp}:{port}",
                        "Hosting for the internet: your router opened the port. Friends anywhere join the address below (click to reveal, or copy it)." + Firewall);
            if (IsPublic(routerExt) && (!pub || publicIp == routerExt))
                return (Reach.Internet, $"{routerExt}:{port}",
                        "Hosting for the internet: friends anywhere join the address below (click to reveal, or copy it)." + Firewall);
            // The router opened a public address, but the internet sees this PC somewhere else:
            // its traffic leaves by another route -- almost always a VPN.
            if (IsPublic(routerExt) && pub)
                return (Reach.Manual, $"{routerExt}:{port}",
                        "Hosting. Your router opened the port, but this PC's internet traffic goes out another way -- usually a VPN. Friends can try "
                      + "the address below; if they cannot get in, turn the VPN off (or exclude Warships from it) while you host.");
            return (Reach.LanOnly, lan,
                    $"Hosting for your network ({lan}). Your router opened the port, but your provider puts you behind a shared address "
                  + "(carrier-grade NAT), so friends elsewhere cannot reach you directly. Use a virtual network (Tailscale, ZeroTier, Radmin VPN), "
                  + "or let a friend host.");
        }
        // Nothing opened. What the router said about its internet side still tells us which fix works.
        if (Router.IsCarrierGrade(routerExt))
            return (Reach.LanOnly, lan,
                    $"Hosting for your network ({lan}). Your provider puts you behind a shared address (carrier-grade NAT), so no amount of port "
                  + "forwarding lets friends reach you directly. Use a virtual network (Tailscale, ZeroTier, Radmin VPN), or let a friend host.");
        if (pub && Router.IsShared(routerExt))
            return (Reach.Manual, $"{publicIp}:{port}",
                    $"Hosting. Your router did not open port {port}" + (why.Length > 0 ? $" -- {why}" : "") + ", and it sits behind a second router. "
                  + $"Forward UDP {port} to this PC ({pc}) in your router, and in the router in front of it forward UDP {port} to {routerExt}; "
                  + "then friends elsewhere join the address below." + Firewall);
        if (pub)
            return (Reach.Manual, $"{publicIp}:{port}",
                    $"Hosting. Your router did not open port {port}" + (why.Length > 0 ? $" -- {why}" : "") + ". "
                  + $"Forward UDP {port} to this PC ({pc}) in your router's settings (turn UPnP on there and it will "
                  + "do this by itself next time), then friends elsewhere join the address below." + Firewall);
        return (Reach.LanOnly, lan,
                $"Hosting for your network ({lan}). Neither your router nor the internet could be asked for your public address. "
              + $"For friends elsewhere, forward UDP {port} to this PC, or use a virtual network (Tailscale, ZeroTier, Radmin VPN).");
    }

    // A real internet address: IPv4, and none of the ranges that cannot be reached from outside
    // (private, carrier-grade, loopback, link-local, "this network", multicast and reserved).
    public static bool IsPublic(string ip)
    {
        if (!Router.IsIpv4(ip) || Router.IsShared(ip)) return false;
        var o = ip.Split('.').Select(int.Parse).ToArray();
        return o[0] != 0 && !(o[0] == 169 && o[1] == 254) && !(o[0] == 192 && o[1] == 0 && o[2] == 0) && o[0] < 224;
    }

    // What the panel shows. (Public so a test can show the reveal without a real router.)
    public void Reachable(Reach r, string address, string message)
    {
        Reachability = r; InternetAddress = r is Reach.Internet or Reach.Manual ? address : "";
        Say(message);
    }

    private void AskPublicIp(int gen)
    {
        FreeIpReq();
        _ipReq = new HttpRequest { Timeout = 6 };
        AddChild(_ipReq);
        _ipReq.RequestCompleted += (result, code, headers, body) =>
        {
            string ip = result == (long)HttpRequest.Result.Success && code == 200 ? System.Text.Encoding.UTF8.GetString(body).Trim() : "";
            _publicIp = Router.IsIpv4(ip) ? ip : "";
            _publicDone = true;
            Callable.From(FreeIpReq).CallDeferred();
            TryDecide(gen);
        };
        if (_ipReq.Request(PublicIpService) != Error.Ok) { _publicDone = true; FreeIpReq(); TryDecide(gen); }
    }
    private void FreeIpReq()
    {
        if (_ipReq == null) return;
        if (IsInstanceValid(_ipReq)) { _ipReq.CancelRequest(); _ipReq.QueueFree(); }
        _ipReq = null;
    }

    public int StaleMappingsClosed { get; private set; }            // for the record (and the smoke test)
    private void RouterResult(int gen, Router.Report report)
    {
        if (gen != _hostGen)
        {   // that session is over: never keep its ports -- close them
            if (report.Opened.Count > 0) { StaleMappingsClosed++; RouterJob(() => Router.Close(report.Opened)); }
            return;
        }
        _report = report;
        _hops = report.Opened;
        TryDecide(gen);
    }

    private void TryDecide(int gen)
    {
        if (gen != _hostGen || !IsHost || !IsOnline || _report == null || !_publicDone) return;   // the session changed, or one answer is still out
        var r = _report;
        bool mapped = r.Opened.Count > 0;
        var d = Describe(r.Ext, mapped, _publicIp, mapped ? r.ExtPort : _hostPort, LanAddress, r.Why, r.Front);
        // Whatever the routers did: friends on a virtual network with you can always join on it,
        // and friends with IPv6 may get in where IPv4 cannot.
        if (d.reach != Reach.Internet)
        {
            foreach (var (name, address) in OverlayAddresses) d.message += $" Friends on your {name} network join {address}.";
            if (Ipv6Address.Length > 0) d.message += " Friends with IPv6 internet can also try your IPv6 address (in the panel).";
        }
        Reachable(d.reach, d.address, d.message);
    }

    public bool Host(int port = DefaultPort)
    {
        StopReconnecting();
        Close();                                           // a session in progress ends properly: saved, guests told
        EndLetGo();                                        // ...and its socket let go now: this port is about to be bound again
        var p = new ENetMultiplayerPeer();
        if (p.CreateServer(port, MaxPlayers) != Error.Ok)
        {
            GoOffline($"Could not open port {port}: another program, or another copy of Warships, is using it. Playing offline.");
            return false;
        }
        _peer = p; Multiplayer.MultiplayerPeer = p;
        _isHost = true; _localId = 1; Connecting = false; _inSession = true;
        Players.Clear(); Players[1] = new PlayerInfo();
        var lan = Router.Lan();
        LanAddress = $"{lan.Ip}:{port}"; Reachability = Reach.Checking; InternetAddress = "";
        OverlayAddresses = Router.Overlays().Select(o => (o.name, $"{o.ip}:{port}")).ToList();
        Ipv6Address = Router.GlobalIpv6() is { Length: > 0 } v6 ? $"[{v6}]:{port}" : "";
        Say($"Hosting on your network ({LanAddress}). Asking your router to open port {port}, and the internet for your public address...");
        SessionChanged?.Invoke();
        int gen = ++_hostGen; _hostPort = port;
        _publicIp = ""; _publicDone = false; _report = null;
        RouterJob(() =>
        {
            Router.Report rep;
            try { rep = Router.Open(port, lan); }
            catch (Exception e) { rep = new Router.Report { Why = e.Message, ExtPort = port }; }
            // The session may have ended while the routers were asked: close what this opened in
            // this same job, so no moment exists where nothing runs and a port is still open.
            if (gen != System.Threading.Volatile.Read(ref _hostGen) || Game.ShuttingDown) { Router.Close(rep.Opened); return; }
            Callable.From(() => RouterResult(gen, rep)).CallDeferred();
        });
        AskPublicIp(gen);
        return true;
    }

    // ── joining ──────────────────────────────────────────────────────────────
    // What a player pastes: "1.2.3.4", "1.2.3.4:27015", "host.example.com:27015", an IPv6 address
    // bare or as [2001:db8::1]:27015, with stray spaces or a scheme in front. null if it is not
    // an address at all (or names an impossible port).
    public static (string host, int port)? ParseAddress(string text, int defaultPort = DefaultPort)
    {
        var s = (text ?? "").Trim();
        int scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) s = s[(scheme + 3)..];
        s = s.TrimEnd('/').Replace(" ", "");
        string host = s, portText = "";
        if (s.StartsWith("["))                                     // [v6] or [v6]:port
        {
            int close = s.IndexOf(']');
            if (close < 0) return null;
            host = s[1..close];
            if (close + 1 < s.Length) { if (s[close + 1] != ':') return null; portText = s[(close + 2)..]; }
        }
        else if (s.Count(c => c == ':') == 1)                      // host:port (more than one colon is a bare IPv6 address)
        {
            int colon = s.IndexOf(':');
            host = s[..colon]; portText = s[(colon + 1)..];
        }
        int port = defaultPort;
        if (portText.Length > 0 && !(int.TryParse(portText, out port) && port is > 0 and < 65536)) return null;
        return host.Length == 0 ? null : (host, port);
    }

    private int _joinGen;
    private string _joinTarget = "";
    public bool Join(string address) { StopReconnecting(); return Connect(address, retry: false); }

    private bool Connect(string address, bool retry)
    {
        if (ParseAddress(address) is not var (host, port))
        {
            Say(string.IsNullOrWhiteSpace(address) ? "Enter a host address first." : $"\"{address.Trim()}\" is not an address. Type the host's IP, or IP:port.");
            return false;
        }
        bool had = _inSession;
        Close();                                           // hosting or in a session: that ends first, properly
        Players[1] = new PlayerInfo();
        Connecting = true;
        _joinAddress = address; _retrying = retry; _attemptAt = Time.GetTicksMsec();
        _joinTarget = port == DefaultPort ? host : $"{host}:{port}";
        int gen = ++_joinGen;
        Say(retry ? $"Getting back in to {_joinTarget} ... (attempt {Attempt} of {RetryAt.Length})" : $"Connecting to {_joinTarget} ...");
        if (had) SessionChanged?.Invoke();                 // the guests that were here are gone: rebuild without them
        // A NAME is looked up off the main thread: a slow or failing lookup used to freeze the game.
        if (System.Net.IPAddress.TryParse(host, out _)) { ConnectTo(gen, host, port); return true; }
        System.Threading.Tasks.Task.Run(() =>
        {
            string ip = "";
            try { ip = System.Net.Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? ""; }
            catch (Exception) { /* no such name: reported below */ }
            if (!Game.ShuttingDown) Callable.From(() => ConnectTo(gen, ip, port)).CallDeferred();
        });
        return true;
    }

    private void ConnectTo(int gen, string ip, int port)
    {
        if (gen != _joinGen) return;                       // cancelled, or superseded by another JOIN
        var p = new ENetMultiplayerPeer();
        if (ip.Length == 0 || p.CreateClient(ip, port) != Error.Ok)
        {
            Failed($"Could not find {_joinTarget}. Check the address. Playing offline.");
            return;
        }
        // ENet's own timeout as well, so it never keeps its default 30 s (the deadline is _Process's)
        foreach (var server in p.Host.GetPeers()) server.SetTimeout(32, BackIn ? 2000 : 4000, BackIn ? RetryTimeoutMs : JoinTimeoutMs);
        _peer = p; Multiplayer.MultiplayerPeer = p;
    }

    // The live Yard writes the base into Character first: a base is part of a character, and this
    // is the moment the character stops being in a session. Nothing here touches another peer's
    // character -- their file is on their machine, and this runs on every peer for its own.
    private void SaveLocalCharacter()
    {
        if (string.IsNullOrEmpty(Character.Id)) return;          // nothing loaded yet: boot, or the menu
        var yard = (Engine.GetMainLoop() as SceneTree)?.Root?.FindChild("Yard", true, false) as Yard;
        if (yard != null && GodotObject.IsInstanceValid(yard)) yard.SaveBase();
        else Character.Save();
    }

    private void Shutdown(bool bye)
    {
        EndLetGo();
        if (_peer != null)
        {
            if (bye && _peer is ENetMultiplayerPeer e && e.Host != null)
            {   // let the goodbye out (see NetBye)
                foreach (var p in e.Host.GetPeers()) if (p.GetState() == ENetPacketPeer.PeerState.Connected) p.PeerDisconnectLater();
                _letGo = e; _letGoUntil = Time.GetTicksMsec() + LetGoMs;
            }
            else _peer.Close();
            _peer = null;
        }
        if (_hops.Count > 0)
        {   // close the router ports we opened (in the background: routers can be slow)
            var hops = _hops;
            RouterJob(() => Router.Close(hops));
        }
        _hops = new(); _hostGen++;
        FreeIpReq();                                        // no lookup outlives its session
        Reachability = Reach.None; InternetAddress = ""; LanAddress = ""; Ipv6Address = ""; OverlayAddresses = new();
        // An explicit offline peer rather than null: IsServer() and GetUniqueId() then
        // answer 1 / true offline instead of depending on engine fallback behaviour.
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        Players.Clear();
        _hostSaidBye = false; _leaving.Clear(); _peerGone = false;
    }

    private void OnPeer(int id, bool joined)
    {
        if (joined)
        {
            // A friend whose connection dies without a goodbye (their PC sleeps, their Wi-Fi
            // drops) is let go in about 10 s instead of ENet's 30, which left a ghost ship
            // drifting through everyone's world. The host's to set: only it holds a real
            // connection to each guest (a guest hears of the others through the host).
            if (_isHost) (_peer as ENetMultiplayerPeer)?.GetPeer(id)?.SetTimeout(32, 4000, 10000);
            // TryAdd: their identity may have arrived a moment before the join event.
            Players.TryAdd(id, new PlayerInfo());
            Say($"Player {id} joined.");
            PlayerJoined?.Invoke(id);
        }
        else
        {
            Players.Remove(id, out var info);
            bool onPurpose = _leaving.Remove(id);
            Say(onPurpose || !_isHost ? $"Player {id} left." : $"Player {id} dropped.");   // only the host hears goodbyes
            PlayerLeft?.Invoke(id, info ?? new PlayerInfo(), onPurpose);
        }
    }

    private void Say(string s) { LastStatus = s; Status?.Invoke(s); GD.Print("[net] " + s); }

    // ── authority helpers ────────────────────────────────────────────────────

    // Guard every simulation step with this. If it returns false, the host will
    // tell you what happened -- do not compute it locally and hope it matches.
    public static bool Sim => IsHost;

    // True when this node is the local player's own ship.
    public static bool OwnedByMe(Node n) => n != null && n.GetMultiplayerAuthority() == LocalId;

    // A WARNING, as a guest should show it. The host judges a dodgeable hit at the end of the
    // warning against the last position the guest reported -- which left the guest a one-way trip
    // earlier -- and the warning itself arrived a one-way trip late. So the guest must be clear a
    // full round trip before the end the host sent: this is that end. Never below 40% of the
    // warning, whatever the connection. The host (and single player) sees it as sent.
    public static double Arriving(double warning)
    {
        if (IsHost || I?._peer is not ENetMultiplayerPeer e || e.GetPeer(1) is not { } host) return warning;
        double rtt = host.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime) / 1000.0;
        return System.Math.Max(warning * 0.4, warning - rtt);
    }

    // A guest's request to the host: the one way a guest asks for anything. Offline, or still
    // connecting, there is no host to ask -- and an RPC then is an engine error.
    public static void AskHost(Node node, StringName method, params Variant[] args)
    {
        if (IsOnline) node.RpcId(1, method, args);
    }
    // On the host, in a request handler: the sender, if it is a player in this session. A request
    // from anyone else -- a peer still handshaking, or one already gone -- is ignored.
    public static bool FromPlayer(Node node, out int who)
    {
        who = node.Multiplayer.GetRemoteSenderId();
        return IsHost && I != null && I.Players.ContainsKey(who);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// A HOST-OWNED THING AS A GUEST SEES IT: the host's 10 Hz reports of where it is, eased between.
// One follower for the raiders, the carrier wing, the fleet and the boss, where each carried its
// own copy of the same three fields and the same two lines.
//
// It also carries the thing forward along its last measured velocity -- for at most a quarter of
// a second, so a thing that stopped, or a connection that did, never runs off. Over the internet
// a guest used to draw every raider where it had been a round trip and a smoothing lag ago
// (~250 ms), and aimed at that: its shots went where the target no longer was.
// ─────────────────────────────────────────────────────────────────────────────
public struct NetPose
{
    public Vector2 Pos;
    public float Rot;
    public bool Has;
    private Vector2 _vel;
    private double _age;
    private const float Ahead = 0.25f, MaxSpeed = 1500f;

    public void Set(Vector2 p, float rot)
    {
        // Measured between reports, and smoothed: one late or early packet is not a new course. A
        // jump (a warp, a respawn) is not a velocity at all.
        if (Has && _age > 0.02)
        {
            var v = (p - Pos) / (float)_age;
            _vel = v.Length() > MaxSpeed ? Vector2.Zero : _vel.Lerp(v, 0.5f);
        }
        (Pos, Rot, Has, _age) = (p, rot, true, 0);
    }

    public void Follow(Node2D n, float dt, float rate = 10f)
    {
        if (!Has) return;
        _age += dt;
        var ahead = Pos + _vel * Mathf.Min((float)_age, Ahead);
        float k = Mathf.Clamp(rate * dt, 0f, 1f);
        n.Position = n.Position.Lerp(ahead, k);
        n.Rotation = Mathf.LerpAngle(n.Rotation, Rot, k);
    }
}
