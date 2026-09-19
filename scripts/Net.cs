using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// NET — the authority model, and the one thing in this project that must be
// right from day one, because retrofitting it is what kills games.
//
// THE RULE, in one line: the HOST owns the world, each PLAYER owns their ship.
//
//   host authority   : resource ticks, enemy spawns and AI, loot rolls, refining,
//                      hauler dispatch and payouts, anything a client could cheat
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

    public bool Host(int port = DefaultPort)
    {
        Shutdown();
        var p = new ENetMultiplayerPeer();
        if (p.CreateServer(port, MaxPlayers) != Error.Ok) { Say($"Could not open port {port}."); StartOffline(); return false; }
        _peer = p; Multiplayer.MultiplayerPeer = p;
        _isHost = true; _localId = 1;
        Players.Clear(); Players[1] = new PlayerInfo { Id = 1 };
        Say($"Hosting on port {port}. Others can join your world.");
        SessionChanged?.Invoke();
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
