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
    public const int DefaultPort = 27015;
    public const int MaxPlayers = 8;

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

    private void OnConnected()
    {
        _localId = Multiplayer.GetUniqueId();
        Players.TryAdd(_localId, new PlayerInfo { Id = _localId });
        Say($"Connected as player {_localId}.");
        SessionChanged?.Invoke();
    }

    // ── session lifecycle ────────────────────────────────────────────────────

    // Offline play. Deliberately the same path as hosting: one set of rules.
    public void StartOffline(string reason = null)
    {
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

    private void OpenRouterPort(int port, int gen)
    {
        string ext = "", why = ""; Upnp mapped = null;
        try
        {
            var u = new Upnp();
            int r = u.Discover(2000, 2, "InternetGatewayDevice");
            var gw = u.GetGateway();
            if (r != (int)Upnp.UpnpResult.Success || gw == null || !gw.IsValidGateway()) why = "no-upnp";
            else if (u.AddPortMapping(port, port, "Warships", "UDP", 0) != (int)Upnp.UpnpResult.Success) why = "refused";
            else { ext = u.QueryExternalAddress(); mapped = u; }
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
    public const string PublicIpService = "https://api.ipify.org";
    private HttpRequest _ipReq;
    private string _publicIp = "", _routerExt = "", _routerWhy = "";
    private bool _publicDone, _routerDone;
    private volatile bool _upnpBusy;
    private int _hostPort;
    // single player (and between sessions): no socket, no lookup, no router job
    public bool NetworkIdle => _peer == null && _ipReq == null && !_upnpBusy;

    public static (Reach reach, string address, string message) Describe(string routerExt, bool mapped, string publicIp, int port, string lan)
    {
        bool pub = publicIp.Length > 0;
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
                    $"Hosting. Your router would not open port {port} by itself (UPnP is off or unsupported): forward UDP {port} "
                  + $"to this PC ({lan.Split(':')[0]}) in your router's settings, then friends elsewhere join with the address below.");
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
    public static bool LooksLikeIpv4(string s)
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
        var d = Describe(_routerExt, _routerExt.Length > 0, _publicIp, _hostPort, LanAddress);
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
        _publicIp = _routerExt = _routerWhy = ""; _publicDone = _routerDone = false;
        _upnpBusy = true;
        System.Threading.Tasks.Task.Run(() => OpenRouterPort(port, gen));
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
