using Godot;
using System.Linq;
using System;
using System.Collections.Generic;

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

    // Single player runs as a host with nobody connected, so IsHost is true offline.
    public static bool IsHost => I == null || I._isHost;
    // Connected, not merely "a peer object exists": while a join is still handshaking
    // there is nobody to send to, and an RPC then is an engine error.
    public static bool IsOnline => I != null && I._peer != null
        && I._peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    public static int LocalId => I == null ? 1 : I._localId;

    private bool _isHost = true;
    private int _localId = 1;
    private MultiplayerPeer _peer;

    // Everyone in the session, host included. Keyed by peer id.
    public readonly Dictionary<int, PlayerInfo> Players = new();
    // Identity is the one piece of player state that is not host-owned: each owner
    // announces its own, and everyone stores it here so a ship spawned later still
    // gets the right name, colours and class.
    public class PlayerInfo
    {
        public int Id;
        public string Name = "Commander";
        public Color Main = new(0.55f, 0.72f, 1.00f);
        public Color Accent = new(1.00f, 0.78f, 0.35f);
        public ShipClass Class = ShipClass.Battleship;
        public bool HasIdentity;
    }

    public event Action<int> PlayerJoined;
    public event Action<int> PlayerLeft;
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
        Multiplayer.PeerConnected    += id => OnPeer((int)id, true);
        Multiplayer.PeerDisconnected += id => OnPeer((int)id, false);
        Multiplayer.ConnectedToServer += OnConnected;
        // A failed or dropped connection falls back to offline. Without this a guest
        // sat forever with IsHost false: the hub stopped producing and nothing said why.
        Multiplayer.ConnectionFailed   += () => StartOffline("Could not reach that host. Playing offline.");
        Multiplayer.ServerDisconnected += () => StartOffline("Host closed the session. Playing offline.");
        StartOffline();
    }

    // ── the build handshake ──────────────────────────────────────────────────
    // WE VERSION SAVE FILES BUT NOT SESSIONS, and a session is where two builds can actually
    // disagree. A guest on an older build has different balance numbers, different stats, and
    // possibly different RPC signatures; it joins, it plays, and every number it sees is a
    // quiet lie. The host decides -- it owns the world, so it owns who is in it.
    //
    // KEEP THIS SIGNATURE STABLE. It is the one RPC that has to work between builds that
    // disagree about everything else; change its arguments and the handshake itself becomes the
    // thing that cannot be negotiated.
    public static bool Accepts(int build) => build == Game.Version;

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetHello(int build)
    {
        if (!IsHost) return;
        int who = Multiplayer.GetRemoteSenderId();
        if (Accepts(build)) return;
        Say($"Refused player {who}: build {build}, this is build {Game.Version}.");
        RpcId(who, nameof(NetRefused), Game.Version);
        // Deferred: disconnecting inside the handler for a packet from that same peer is asking
        // the multiplayer layer to tear down what it is currently reading.
        CallDeferred(nameof(Drop), who);
    }

    private void Drop(int who) => Multiplayer.MultiplayerPeer?.DisconnectPeer(who);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRefused(int hostBuild)
    {
        if (Multiplayer.GetRemoteSenderId() != 1) return;          // only the host refuses anyone
        StartOffline($"That host is running build {hostBuild}; this is build {Game.Version}. Playing offline.");
    }

    private void OnConnected()
    {
        _localId = Multiplayer.GetUniqueId();
        // Before anything else this peer says: if the builds disagree, nothing after it means
        // what either side thinks it means.
        RpcId(1, nameof(NetHello), Game.Version);
        Players.TryAdd(_localId, new PlayerInfo { Id = _localId });
        Say($"Connected as player {_localId}.");
        SessionChanged?.Invoke();
    }

    // ── session lifecycle ────────────────────────────────────────────────────

    // Offline play. Deliberately the same path as hosting: one set of rules.
    private void StartOffline(string reason = null)
    {
        // WHOEVER IS LEAVING, WHATEVER THE REASON. Quitting the lobby, the host closing the
        // session, a connection that failed -- they all land here, and each of them is a peer
        // whose character stops being in a session. A scene change saves on its way out, but a
        // session can end without one: press PLAY OFFLINE and nothing moves but the socket.
        // Saving here is cheap and it is the only point every one of those routes passes through.
        //
        // ONLY WHEN THERE WAS A SESSION TO LEAVE. StartOffline also runs at boot and on a failed
        // Host()/Join(), where nothing has left anything -- and saving there wrote a freshly
        // built default base over a good file. Found by a check that had been passing.
        if (IsOnline) SaveLocalCharacter();
        Shutdown();
        _isHost = true; _localId = 1;
        Players.Clear(); Players[1] = new PlayerInfo { Id = 1 };
        Say(reason ?? "Offline session.");
        SessionChanged?.Invoke();
    }

    // ── internet hosting ────────────────────────────────────────────────────
    // Hosting starts at once for your own network. Meanwhile a background thread asks
    // your router (UPnP) to forward the port to this PC, and a web request asks a public
    // "what is my IP" service what the internet sees; the two answers together decide
    // what friends elsewhere need (see Describe). The background thread writes NOTHING:
    // it hands its result back to the main thread, which keeps it only for the current
    // session -- a mapping made for a session that has already ended is closed at once.
    public enum Reach { None, Checking, Internet, Manual, LanOnly }
    public Reach Reachability { get; private set; } = Reach.None;
    public string InternetAddress { get; private set; } = "";
    public string LanAddress { get; private set; } = "";
    private Upnp _upnp; private int _mappedPort, _hostGen;

    private void OpenRouterPort(int port, int gen, string lanIp)
    {
        string ext = "", why = ""; Upnp mapped = null;
        try
        {
            Upnp u = null; UpnpDevice gw = null; int r = -1;
            // THREE PASSES. SSDP discovery is one unacknowledged multicast datagram per attempt --
            // one frame lost on Wi-Fi and the answer is "your router has no UPnP" for the whole
            // session. Cheap to repeat, and it happens on a background thread while LAN hosting is
            // already up, so nobody waits for it.
            for (int attempt = 0; attempt < 3 && (gw == null || !gw.IsValidGateway()); attempt++)
            {
                u = new Upnp();
                // PIN THE INTERFACE. Left to itself the search leaves on whichever adapter Windows
                // thinks is best, and this machine has WSL -- plus, on many machines, Hyper-V or
                // Tailscale. The router never hears a search sent down a virtual adapter.
                if (lanIp.Length > 0) u.DiscoverMulticastIf = lanIp;
                // NO DEVICE FILTER. Godot keeps only replies whose search target CONTAINS this
                // string, while the underlying search tries several targets in turn and stops at
                // the first that answers -- and only the first of those contains
                // "InternetGatewayDevice". A router that answers on any of the others produced a
                // SUCCESS with an empty device list, so GetGateway() came back null and a working
                // router was reported as having no UPnP at all. IsValidGateway() below is the real
                // test and is unaffected by widening this.
                //
                // Under 3 s on purpose: the search advertises its own wait in the request, and
                // Windows only admits unicast replies to a multicast request for about that long.
                // A longer timeout makes Windows worse, not better.
                r = u.Discover(2500, 2, "");
                if (r != (int)Upnp.UpnpResult.Success) continue;
                gw = u.GetGateway();                       // only after the result: it logs an engine error on an empty list
            }
            if (r != (int)Upnp.UpnpResult.Success || gw == null || !gw.IsValidGateway()) why = "no router answered";
            else
            {
                int m = u.AddPortMapping(port, port, "Warships", "UDP", 0);
                // A permanent lease is what we want -- nothing has to renew it -- but plenty of
                // routers only accept a bounded one, and a stale mapping from a session that was
                // killed rather than closed looks like a conflict. Try the finite lease, then
                // clear our own old mapping and try once more.
                if (m != (int)Upnp.UpnpResult.Success) m = u.AddPortMapping(port, port, "Warships", "UDP", 7200);
                if (m != (int)Upnp.UpnpResult.Success)
                {
                    u.DeletePortMapping(port, "UDP");
                    m = u.AddPortMapping(port, port, "Warships", "UDP", 0);
                }
                if (m != (int)Upnp.UpnpResult.Success) why = $"the router refused ({(Upnp.UpnpResult)m})";
                else { ext = u.QueryExternalAddress(); mapped = u; }
            }
        }
        catch (Exception e) { why = e.Message; }
        _upnpBusy = false;
        Callable.From(() => RouterResult(gen, port, ext, why, mapped)).CallDeferred();
    }

    // ── what friends elsewhere need: decided from the router's report AND the public address ──
    // The router (UPnP) says whether it opened the port and what it thinks its outside address
    // is; a public "what is my IP" service says what the internet actually sees. Together:
    //   INTERNET   the router opened the port and IS the edge: friends join <public>:<port>
    //   MANUAL     the router would not open it (UPnP off): forward UDP <port>, then <public>:<port>
    //   LAN ONLY   the router's outside address is private or differs from the public one --
    //              another router or the provider's shared address (carrier-grade NAT) is in
    //              the way -- or nothing could be learned at all
    // The status line never prints the public address: the panel shows it behind a reveal.
    private const string PublicIpService = "https://api.ipify.org";
    private HttpRequest _ipReq;
    private string _publicIp = "", _routerExt = "", _routerWhy = "";
    // Whether AddPortMapping SUCCEEDED -- not whether the router happened to tell us its outside
    // address. Describe used to infer one from the other, so a router that opened the port but
    // answered nothing to QueryExternalAddress was reported as having refused, and the player was
    // sent to forward a port that was already forwarded.
    private bool _routerMapped;
    private bool _publicDone, _routerDone;
    private volatile bool _upnpBusy;
    private int _hostPort;
    // single player (and between sessions): no socket, no lookup, no router job
    public bool NetworkIdle => _peer == null && _ipReq == null && !_upnpBusy;

    public static (Reach reach, string address, string message) Describe(string routerExt, bool mapped, string publicIp, int port, string lan, string why = "")
    {
        bool pub = publicIp.Length > 0;
        // The port IS open; the router just would not say what its outside address is. Without
        // this the run falls through to the manual-forwarding text below -- telling the player to
        // do by hand the thing that already worked.
        if (mapped && routerExt.Length == 0 && pub && !IsSharedAddress(publicIp))
            return (Reach.Internet, $"{publicIp}:{port}",
                    "Hosting for the internet: your router opened the port. It would not say what its outside address is, "
                  + "so this is the address the internet sees. Friends anywhere can join it (click to reveal, or copy it).");
        // Carrier-grade NAT, found on the PUBLIC address rather than the router's. Forwarding
        // cannot work behind it at all, so do not send anyone to their router settings.
        if (!mapped && pub && IsSharedAddress(publicIp))
            return (Reach.LanOnly, lan,
                    $"Hosting for your network ({lan}). Your provider puts you behind a shared address (carrier-grade NAT), "
                  + "so no amount of port forwarding will let friends reach you directly. Use Tailscale or ZeroTier, or let a friend host.");
        if (mapped && routerExt.Length > 0 && !IsSharedAddress(routerExt) && (!pub || publicIp == routerExt))
            return (Reach.Internet, $"{routerExt}:{port}",
                    "Hosting for the internet: friends anywhere can join. Their address is below (click to reveal, or copy it).");
        if (mapped && (IsSharedAddress(routerExt) || (pub && publicIp != routerExt)))
            return (Reach.LanOnly, lan,
                    $"Hosting for your network ({lan}). Your router opened the port, but it is not the last step to the internet: "
                  + "another router, or your provider's shared address (carrier-grade NAT), sits in between, so friends elsewhere "
                  + "cannot reach you directly. Use Tailscale or ZeroTier, or let a friend host.");
        if (pub)
            return (Reach.Manual, $"{publicIp}:{port}",
                    $"Hosting. Your router did not open port {port}" + (why.Length > 0 ? $" -- {why}" : "") + ". "
                  + $"Forward UDP {port} to this PC ({lan.Split(':')[0]}) in your router's settings (turn UPnP on there and it will "
                  + "do this by itself next time), then friends elsewhere join with the address below. Windows may also ask to allow "
                  + "Warships through the firewall the first time -- say yes for private AND public networks.");
        return (Reach.LanOnly, lan,
                $"Hosting for your network ({lan}). Neither your router nor the internet could be asked for your public address. "
              + $"For friends elsewhere, forward UDP {port} to this PC, or use Tailscale or ZeroTier.");
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
            _publicIp = LooksLikeIpv4(ip) ? ip : "";
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
    private static bool LooksLikeIpv4(string s)
    {
        var p = s.Split('.');
        return p.Length == 4 && p.All(x => int.TryParse(x, out int v) && v >= 0 && v <= 255);
    }

    public int StaleMappingsClosed { get; private set; }            // for the record (and the smoke test)
    private void RouterResult(int gen, int port, string ext, string why, Upnp mapped)
    {
        if (gen != _hostGen)
        {   // that session is over: never keep its handle -- close the port it opened
            if (mapped != null) { StaleMappingsClosed++; System.Threading.Tasks.Task.Run(() => mapped.DeletePortMapping(port, "UDP")); }
            return;
        }
        _routerExt = ext; _routerWhy = why; _routerDone = true;
        _routerMapped = mapped != null;
        if (mapped != null) { _upnp = mapped; _mappedPort = port; }
        TryDecide(gen);
    }

    // Tailscale's addresses (100.64.0.0/10): friends on the same tailnet can join on these.
    public static bool IsTailnet(string ip)
    {
        var p = ip.Split('.');
        return p.Length == 4 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b) && a == 100 && b >= 64 && b <= 127;
    }
    public string TailnetAddress { get; private set; } = "";

    private void TryDecide(int gen)
    {
        if (gen != _hostGen || !IsHost || !IsOnline || !(_routerDone && _publicDone)) return;   // the session changed, or one answer is still out
        var d = Describe(_routerExt, _routerMapped, _publicIp, _hostPort, LanAddress, _routerWhy);
        // on a tailnet, friends on it can join even when the internet cannot reach you
        if (d.reach == Reach.LanOnly && TailnetAddress.Length > 0) d.message += $" Friends on your Tailscale network join {TailnetAddress}.";
        Reachable(d.reach, d.address, d.message);
    }

    // Private and carrier-grade (100.64.0.0/10) addresses cannot be reached from outside.
    public static bool IsSharedAddress(string ip)
    {
        var p = ip.Split('.');
        if (p.Length != 4 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b)) return false;
        return a == 10 || (a == 172 && b >= 16 && b <= 31) || (a == 192 && b == 168) || (a == 100 && b >= 64 && b <= 127) || a == 127;
    }

    private static string LocalLan(int port)
    {
        foreach (var a in IP.GetLocalAddresses())
            if (a.Contains('.') && IsSharedAddress(a) && !a.StartsWith("127.") && !a.StartsWith("100.")) return $"{a}:{port}";
        return $"127.0.0.1:{port}";
    }

    public bool Host(int port = DefaultPort)
    {
        Shutdown();
        var p = new ENetMultiplayerPeer();
        if (p.CreateServer(port, MaxPlayers) != Error.Ok) { Say($"Could not open port {port}."); StartOffline(); return false; }
        _peer = p; Multiplayer.MultiplayerPeer = p;
        _isHost = true; _localId = 1;
        Players.Clear(); Players[1] = new PlayerInfo { Id = 1 };
        LanAddress = LocalLan(port); Reachability = Reach.Checking; InternetAddress = "";
        var tn = IP.GetLocalAddresses().FirstOrDefault(IsTailnet);
        TailnetAddress = tn != null ? $"{tn}:{port}" : "";
        Say($"Hosting on your network ({LanAddress}). Asking your router to open port {port}, and the internet for your public address...");
        SessionChanged?.Invoke();
        int gen = ++_hostGen; _hostPort = port;
        _publicIp = _routerExt = _routerWhy = ""; _publicDone = _routerDone = false; _routerMapped = false;
        _upnpBusy = true;
        string lanIp = LanAddress.Split(':')[0];
        System.Threading.Tasks.Task.Run(() => OpenRouterPort(port, gen, lanIp));
        AskPublicIp(gen);
        return true;
    }

    // Accepts "address" or "address:port".
    public bool Join(string address, int port = DefaultPort)
    {
        address = address.Trim();
        int colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address[(colon + 1)..], out int p2)) { port = p2; address = address[..colon]; }
        if (address.Length == 0) { Say("Enter a host address first."); return false; }

        Shutdown();
        var p = new ENetMultiplayerPeer();
        if (p.CreateClient(address, port) != Error.Ok) { StartOffline($"Could not reach {address}:{port}. Playing offline."); return false; }
        _peer = p; Multiplayer.MultiplayerPeer = p;
        _isHost = false;
        Say($"Connecting to {address}:{port} ...");
        return true;
    }

    public void GoOffline() { StartOffline(); }

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

    private void Shutdown()
    {
        if (_peer != null) { _peer.Close(); _peer = null; }
        if (_upnp != null && _mappedPort != 0)
        {   // close the router port we opened (in the background: routers can be slow)
            var u = _upnp; int mp = _mappedPort;
            System.Threading.Tasks.Task.Run(() => u.DeletePortMapping(mp, "UDP"));
        }
        _upnp = null; _mappedPort = 0; _hostGen++;
        FreeIpReq();                                        // no lookup outlives its session
        Reachability = Reach.None; InternetAddress = ""; LanAddress = ""; TailnetAddress = "";
        // An explicit offline peer rather than null: IsServer() and GetUniqueId() then
        // answer 1 / true offline instead of depending on engine fallback behaviour.
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        Players.Clear();
    }

    private void OnPeer(int id, bool joined)
    {
        if (joined)
        {
            // TryAdd: their identity may have arrived a moment before the join event.
            Players.TryAdd(id, new PlayerInfo { Id = id });
            Say($"Player {id} joined.");
            PlayerJoined?.Invoke(id);
        }
        else
        {
            Players.Remove(id);
            Say($"Player {id} left.");
            PlayerLeft?.Invoke(id);
        }
    }

    private void Say(string s) { LastStatus = s; Status?.Invoke(s); GD.Print("[net] " + s); }

    // ── authority helpers ────────────────────────────────────────────────────

    // Guard every simulation step with this. If it returns false, the host will
    // tell you what happened -- do not compute it locally and hope it matches.
    public static bool Sim => IsHost;

    // True when this node is the local player's own ship.
    public static bool OwnedByMe(Node n) => n != null && n.GetMultiplayerAuthority() == LocalId;
}
