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
    public const int DefaultPort = 27015;
    private const int MaxPlayers = 8;

    public static Net I { get; private set; }

    // Single player runs as a host with nobody connected, so IsHost is true offline -- and while
    // a join is still in its handshake: until a host has accepted this build, this is still your world.
    public static bool IsHost => I == null || I._isHost;
    // IN A SESSION AND LET IN, not merely "the link is up". ENet reports a link connected the moment
    // it comes up, before Godot's handshake has let either end in; a guest that sent then was
    // throwing packets at a host that had not admitted it -- an engine error there
    // ("SYS_COMMAND_AUTH") -- and read itself as HOSTING in its own HUD and panel meanwhile. A
    // guest is online once the host's welcome arrives (NetWelcome); until then it is Connecting.
    public static bool IsOnline => I != null && I._inSession && !I.Connecting && I._peer != null
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
    // JOIN pressed, and the host has not let this pilot in yet: the address looked up, connected
    // to, the handshake, then the host's welcome.
    public bool Connecting { get; private set; }

    // Everyone in the session, host included. Keyed by peer id.
    public readonly Dictionary<int, PlayerInfo> Players = new();
    // Identity is the one piece of player state that is not host-owned: each owner
    // announces its own, and everyone stores it here so a ship spawned later still
    // gets the right name, colours, class, pilot upgrades, gear and gear levels.
    public class PlayerInfo
    {
        public string Name = Character.Defaults.Name;
        public Color Main = Character.Defaults.Main, Accent = Character.Defaults.Accent;
        public ShipClass Class = ShipClass.Battleship;
        public int[] Bought = Array.Empty<int>();
        public string[] Equip = Array.Empty<string>();
        public Dictionary<string, int> GearLevel = new();  // what the pilot levelled each part to, by id (Equipment.SanitizeLevels on arrival)
        public string CharacterId = "";                   // the pilot's stable identity: a peer id changes on a reconnect
        public int Level = 1;                              // the pilot's level, as claimed (an escort's threat)
        public int Peak = 1;                               // the highest level it has reached, as claimed (Progression.Claim): what its walls read
        public bool HasIdentity;
    }

    public event Action<int> PlayerJoined;
    public event Action<int, PlayerInfo, bool> PlayerLeft;          // peer, who it was, whether it said goodbye
    public event Action<string> Status;
    // A guest's session is open both ways: the host has let it in, and from here what it sends
    // reaches the host (see NetWelcome). The moment a guest introduces itself.
    public event Action Admitted;

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
        sm.PeerAuthenticating += OnAuthenticating;
        Multiplayer.PeerConnected    += OnPeerJoined;
        Multiplayer.PeerDisconnected += OnPeerLeft;
        Multiplayer.ConnectedToServer += OnConnected;
        // A failed or dropped connection falls back to offline -- and a DROP is retried (OnHostGone).
        // Without this a guest sat forever with IsHost false: the hub stopped and nothing said why.
        Multiplayer.ConnectionFailed   += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnHostGone;
        GoOffline();
        // The close button goes through Game.Quit like every other way out. This is the one node
        // that always exists, and what it owns -- the socket and the router ports -- is what a
        // closed window must not strand.
        GetTree().AutoAcceptQuit = false;
    }
    private void OnAuthenticating(long id) => ((SceneMultiplayer)Multiplayer).SendAuth((int)id, BitConverter.GetBytes(PretendProtocol ?? Protocol));
    private void OnPeerJoined(long id) => OnPeer((int)id, true);
    private void OnPeerLeft(long id) => OnPeer((int)id, false);
    private void OnConnectionFailed() => Failed(CouldNotReach);

    // EVERYTHING _Ready HOOKED INTO THE TREE'S MULTIPLAYER COMES OFF, AND EVERY SOCKET IS CLOSED
    // (invariant A). The tree's multiplayer outlives this node by a few steps of the engine's own
    // teardown; left to it, this node's handlers, its handshake callback and a peer made here -- all
    // of them objects the scripting side answers for -- were let go only as that side shut down.
    public override void _ExitTree()
    {
        var sm = (SceneMultiplayer)Multiplayer;
        sm.PeerAuthenticating -= OnAuthenticating;
        sm.PeerConnected -= OnPeerJoined;
        sm.PeerDisconnected -= OnPeerLeft;
        sm.ConnectedToServer -= OnConnected;
        sm.ConnectionFailed -= OnConnectionFailed;
        sm.ServerDisconnected -= OnHostGone;
        sm.AuthCallback = new Callable();
        foreach (var (p, _, _) in _lettingGo) p.Close();
        _lettingGo.Clear();
        _peer?.Close(); _peer = null;
        sm.MultiplayerPeer = null;
        if (I == this) I = null;
    }

    // This node outlives every scene: the batched save's clock, the goodbye's last second, and the
    // reconnect attempts.
    public override void _Process(double delta)
    {
        Character.TickSave(delta);
        PumpLetGo();
        Beat(delta);
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
    // SET BY THE STATIC CONSTRUCTOR, after every static field initializer of Net has run, and a property:
    // as a readonly field initializer the walk ran mid-way through Net's own initialization, hashed this
    // very field as 0 and every Net static declared below it as unset, and so differed from any
    // fingerprint taken later in the same process (R1's first runs: 3724c77b against 7991f5f3).
    public static int Protocol { get; private set; }
    static Net() => Protocol = Fingerprint();
    public static bool Accepts(int protocol) => protocol == Protocol;
    // The smoke test's way to be a different build, to prove the refusal on a real connection.
    public static int? PretendProtocol;

    // THE HOST LETS ONE PEER GO: the one hang-up every refusal and replacement goes through.
    public void Hang(int peer) => (_peer as ENetMultiplayerPeer)?.GetPeer(peer)?.PeerDisconnectLater();

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
            Hang((int)id);
        }
        else GoOffline($"That host is on a different build of the game (theirs {theirs:x8}, yours {PretendProtocol ?? Protocol:x8}): "
                     + "you both need the same release (Esc menu, bottom). Playing offline.");
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
    // records, tables, struct rows and arrays of them. Not engine objects, and not the game's live
    // collections (a list or dictionary field is what a run fills in -- the key bindings, the character).
    private static bool Plain(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(Color) || t == typeof(Vector2)
        || (t.IsArray && Plain(t.GetElementType()))
        || (t.GetMethod("<Clone>$") != null && !typeof(GodotObject).IsAssignableFrom(t))
        || Table(t) || StructRow(t);
    // A STRUCT ROW: a value type of the game's own assembly whose public fields are all Plain OR A
    // DELEGATE (a site's post, a target filter, a status guard, a turret's spec, a wave's crew row
    // whose Count is a Func), or a `System.ValueTuple`N` whose fields are all Plain (a table row of
    // names, positions and flags -- Hub.Outposts, Hub.PracticeTargets). As fixed as a table row, and
    // written out the same way; before this, an array of them was not hashed at all. READONLY OR NOT:
    // a value in a static readonly field or a table row is as fixed as what holds it -- a mutable
    // struct (StatusSet.Guards' StatusGuard, EmplacementDef.Gun's TurretSpec) is no less part of the
    // build for having settable fields, and `IsReadOnlyAttribute` only ever told us the struct itself,
    // never the array holding it, could not be reassigned in place. A DELEGATE FIELD (WaveCrew.Count)
    // no longer keeps its row out, but it is written only as its delegate type name (`Show`, e.g.
    // Func`2): WHICH code is assigned there, and what it computes, are NOT compared. The row's other
    // fields (Kind, Way, Nth, At, Step) are, so Waves.Patrol, Waves.HuntPin and WaveDef.Crew are
    // hashed field by field while a changed Count rule still goes unseen.
    private static bool StructRow(Type t) =>
        t.IsValueType && !t.IsPrimitive && !t.IsEnum
        && (t.Assembly == typeof(Net).Assembly || (t.Namespace == "System" && t.Name.StartsWith("ValueTuple`", StringComparison.Ordinal)))
        && t.GetFields(BindingFlags.Public | BindingFlags.Instance) is { Length: > 0 } fields
        && fields.All(f => Plain(f.FieldType) || typeof(Delegate).IsAssignableFrom(f.FieldType));
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
        Delegate del => del.GetType().Name,
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        GodotObject g => g.GetType().Name,
        Array a => "[" + string.Join(",", a.Cast<object>().Select(x => Show(x, depth))) + "]",
        System.Collections.IDictionary d => "{" + string.Join(",", d.Keys.Cast<object>()
            .Select(k => Show(k, depth) + ":" + Show(d[k], depth)).OrderBy(x => x, StringComparer.Ordinal)) + "}",
        _ when depth < 4 && (Table(v.GetType()) || StructRow(v.GetType())) => v.GetType().Name + "{" + string.Join(",", v.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name).Select(f => f.Name + "=" + Show(f.GetValue(v), depth + 1))) + "}",
        _ => v.ToString(),
    };

    // The handshake is done: this is a guest now, with its own id, and the world is rebuilt as one.
    // It stays Connecting -- sending nothing -- until the host's welcome (NetWelcome).
    private void OnConnected()
    {
        _localId = Multiplayer.GetUniqueId();
        _isHost = false; _inSession = true;
        // the host that let us in is the one to come back to; an unreachable one is given up on in 12 s
        LastHost = _joinAddress; _dropClock = -1; Attempt = 0; CanReconnect = false; _rejoin = false; _hostSaidBye = false;
        if ((_peer as ENetMultiplayerPeer)?.GetPeer(1) is { } host) Link(host, 12000);
        Players.TryAdd(_localId, new PlayerInfo());
        SessionChanged?.Invoke();
    }

    // THE HOST'S FIRST WORD TO A GUEST IT HAS LET IN, and the moment that guest is online. Godot
    // admits a peer once each end has said "done", and each end starts sending the moment it has
    // heard the other's: a guest's first ship reports left right beside its own "done" and, on a
    // link that reorders, overtook it -- to a host still waiting for that "done", which threw them
    // away with an engine error. The host sends this only once it has let the guest in, so a guest
    // that waits for it sends nothing early. (Its reliable words were never at risk: they queue
    // behind the "done" on the same channel. Its ship reports go unreliably, on another.)
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetWelcome()
    {
        if (_isHost || !_inSession || !Connecting) return;
        Connecting = false;
        RpcId(1, nameof(NetWelcomed));
        Say($"Connected as player {_localId}.");
        Admitted?.Invoke();
    }

    // THE ANSWER TO THE WELCOME, and the moment the host may send a guest anything unreliable.
    // Godot lets a guest in on the guest's "done", which the guest may send before the HOST's "done"
    // has reached it -- and if the host's was lost, what the host then broadcast (and every other
    // guest's report it passed on) reached a guest still waiting for it, which threw it all away
    // with an engine error. The welcome travels behind the host's "done" on the reliable channel,
    // so this answer proves the "done" arrived. Until it comes the guest is sent only reliable
    // words (they queue behind the "done" too) and nothing of a world it has not reported.
    private readonly HashSet<int> _heard = new();
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetWelcomed()
    {
        int from = Multiplayer.GetRemoteSenderId();
        if (_isHost && Players.ContainsKey(from)) _heard.Add(from);
    }
    public static bool Hears(int peer) => I != null && I._heard.Contains(peer);
    // The host's one way to send to every guest outside a world's own traffic (which waits for the
    // guest's report of its world: Hub.RpcToSector): only to guests that have answered the welcome,
    // leaving out `except` (0 leaves out nobody). Nothing on a guest, and nothing offline.
    public static void ToHeard(int except, Node node, StringName method, params Variant[] args)
    {
        if (I == null || !IsHost || !IsOnline) return;
        foreach (int peer in I._heard.ToList())
            if (peer != except) node.RpcId(peer, method, args);
    }

    // ── the beat and the watchdog (plan §3.6; Link's constants) ──────────────
    // Every Link.BeatMs each end of a live link sends NetBeat (the host to each guest that answered its
    // welcome, a guest to its host), and the other end echoes it. Anything heard from a peer -- a beat or
    // an echo -- ends its silence; a silence over Link.QuietMs, counted in capped frames (Link.Quiet),
    // drops it: the host hangs the guest up, a guest takes its host as gone (OnHostGone: the retries).
    // Its own row, NetChannels.Beat, so a beat never waits behind the game's reliable words.
    private readonly Dictionary<int, double> _silence = new();
    private global::Link.Trip _trip = new();
    private double _beatClock;
    private void Beat(double delta)
    {
        if (!IsOnline || _peerGone) return;
        var peers = _isHost ? _heard.ToList() : new List<int> { 1 };
        foreach (int p in peers) _silence[p] = global::Link.Quiet(_silence.GetValueOrDefault(p), delta);
        _beatClock += delta;
        if (_beatClock * 1000 >= global::Link.BeatMs)
        {
            _beatClock = 0;
            foreach (int p in peers) RpcId(p, nameof(NetBeat), (long)Time.GetTicksMsec());
        }
        foreach (int p in peers)
        {
            if (!global::Link.Overdue(_silence[p])) continue;
            _silence.Remove(p);
            if (!_isHost) { OnHostGone(); return; }
            _heard.Remove(p);                              // nothing more is sent to it while it is let go
            Hang(p);
        }
    }
    // Heard from this peer on a live link: the host's guests that answered the welcome, a guest's host.
    private bool Beating(int from) => IsOnline && (_isHost ? _heard.Contains(from) : from == 1);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = NetChannels.Beat)]
    private void NetBeat(long sentMs)
    {
        int from = Multiplayer.GetRemoteSenderId();
        if (!Beating(from)) return;
        _silence[from] = 0;
        RpcId(from, nameof(NetBeatBack), sentMs);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = NetChannels.Beat)]
    private void NetBeatBack(long sentMs)
    {
        int from = Multiplayer.GetRemoteSenderId();
        if (!Beating(from)) return;
        _silence[from] = 0;
        if (!_isHost) _trip.Echo(((long)Time.GetTicksMsec() - sentMs) / 1000.0);
    }

    // A LIVE LINK, set up the same at both ends (a guest's to its host, the host's to each guest).
    //   How long a silent peer is kept: ENet gives one up once its resends have run out (the
    //   limit, 32) AND QuietMs has passed -- or at `maxMs`, whichever comes first. On a fast link
    //   the resends run out in about a second, so QuietMs is what a stall on either machine has to
    //   outlast: a first scene load, a garbage-collection pause, a Wi-Fi roam. At 4 s any of them
    //   ended the session.
    //   The throttle never falls. At its defaults ENet throws unreliable packets away BEFORE they
    //   are sent whenever a round trip comes back slower than the last few -- every jitter spike on
    //   the internet, and worst right after a burst of reliable sends -- and those are every ship,
    //   raider, boss and base report a player draws from. Deceleration 0 keeps every one.
    private static void Link(ENetPacketPeer p, int maxMs)
    {
        p.SetTimeout(32, Math.Min(global::Link.QuietMs, maxMs), maxMs);
        p.ThrottleConfigure(5000, 2, 0);
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
    // is queued, waits for it to be acknowledged, then sends its own disconnect -- and is pumped
    // until that is done or LetGoMs has passed (see Shutdown: every link that is up goes this way).
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
    private void SayGoodbye()
    {
        if (!_inSession || Connecting || !IsOnline || SkipGoodbye || _peerGone) return;
        if (_isHost) Rpc(nameof(NetBye)); else RpcId(1, nameof(NetBye));
    }
    // HOW LONG A GOODBYE IS GIVEN. It is two reliable packets in turn: the game's NetBye, then --
    // once that is acknowledged -- ENet's own disconnect, the one the other end acts on. Over the
    // internet a round trip is up to 230 ms (90 +/- 25 ms each way in the smoke test's), and a
    // packet lost on the way (2-5% of them) is resent after the round trip plus four times its
    // variance -- up to half a second on a link a few seconds old, doubled for a second loss of the
    // same packet. Two losses fit in 2 s; the 1 s it was given let one run-of-the-mill loss past it,
    // and the other end then waited out its whole timeout instead.
    // EVERY GOODBYE KEEPS ITS OWN TIME. Ending a session let any earlier goodbye go at once, so a
    // pilot that pressed HOST and then quit a second later cut its own goodbye off; a quitting game
    // waits for these (NetworkIdle).
    private readonly List<(ENetMultiplayerPeer peer, ulong until, bool server)> _lettingGo = new();
    private const ulong LetGoMs = 2000;
    private void PumpLetGo()
    {
        for (int i = _lettingGo.Count - 1; i >= 0; i--)
        {
            var (p, until, _) = _lettingGo[i];
            if (p.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected) { _lettingGo.RemoveAt(i); continue; }   // already let go
            bool busy = p.Host != null && p.Host.GetPeers().Any(x => x.GetState() != ENetPacketPeer.PeerState.Disconnected);
            if (!busy || Time.GetTicksMsec() >= until) { p.Close(); _lettingGo.RemoveAt(i); }
            else p.Poll();
        }
    }

    // ── reconnecting ────────────────────────────────────────────────────────
    // A guest whose host drops without a goodbye goes offline -- its own world keeps running --
    // and tries the same host again 3 times over about 20 s. Then RECONNECT, one more try on the
    // button. The host holds the pilot's place meanwhile (Hub: 90 s).
    public static readonly double[] RetryAt = { 2.0, 8.0, 14.0 };   // seconds after the drop
    private const int RetryTimeoutMs = 5000, JoinTimeoutMs = 12000;
    private bool BackIn => _retrying || _rejoin;           // a try to get back in to the host just lost, not a JOIN
    // What a typed address that never answered means, and the one next step: an address is for the
    // same network or a virtual one, and the host's firewall has to let it in.
    private string CouldNotReach => $"No answer from {_joinTarget} in {JoinTimeoutMs / 1000} s. An address works on the same network or over Radmin VPN, "
                                  + "and the host must allow Warships through Windows Firewall; for friends elsewhere, use the host's room code. Playing offline.";
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
        SayGoodbye();
        _inSession = false;
        _joinGen++;                                        // a lookup still in flight connects to nothing
        Shutdown();
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
    public bool NetworkIdle => _peer == null && _ipReq == null && _routerJobs == 0 && _lettingGo.Count == 0;

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
        Close();                                           // a session in progress ends properly: saved, the others told
        // A HOST's old socket is let go now: it holds the port about to be bound again. A GUEST's is
        // left to finish -- its goodbye is still on the way out, and cut off, the host it left held
        // its place as a drop.
        foreach (var (old, _, _) in _lettingGo.Where(g => g.server).ToList()) old.Close();
        _lettingGo.RemoveAll(g => g.server);
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
            try { ip = DialAddress(System.Net.Dns.GetHostAddresses(host)); }
            catch (Exception) { /* no such name: reported below */ }
            if (!Game.ShuttingDown) Callable.From(() => ConnectTo(gen, ip, port)).CallDeferred();
        });
        return true;
    }

    // What a name's lookup hands ENet: its IPv4 address when it has one, else its IPv6 one -- a name
    // with only an AAAA record read as "no such host". Link-local IPv6 is left out: it means
    // something only on the adapter it came from, which a name cannot say.
    public static string DialAddress(IEnumerable<System.Net.IPAddress> found) =>
        found.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                         || (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !a.IsIPv6LinkLocal))
             .OrderBy(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 0 : 1)
             .FirstOrDefault()?.ToString() ?? "";

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
        int deadline = BackIn ? RetryTimeoutMs : JoinTimeoutMs;
        foreach (var server in p.Host.GetPeers()) server.SetTimeout(32, Math.Min(global::Link.QuietMs, deadline), deadline);
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

    private void Shutdown()
    {
        if (_peer != null)
        {
            // EVERY LINK THAT IS UP IS LET GO GENTLY, goodbye or not: what it has queued goes first --
            // the goodbye (NetBye), or a refused player's own build, which the host must hear to say
            // it refused one. A knock hung up the moment it read the host's build, and when its own
            // had been lost on the way it was never resent: the host never knew. Not when the other
            // end is already gone, nor for the smoke test's crash.
            // (the status first: a link the engine has already closed -- a drop, a failed connect -- has
            // no host to ask, and asking is an engine error)
            if (!SkipGoodbye && !_peerGone && _peer is ENetMultiplayerPeer e
                && e.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected && e.Host != null
                && e.Host.GetPeers().Any(p => p.GetState() == ENetPacketPeer.PeerState.Connected))
            {
                foreach (var p in e.Host.GetPeers()) if (p.GetState() == ENetPacketPeer.PeerState.Connected) p.PeerDisconnectLater();
                _lettingGo.Add((e, Time.GetTicksMsec() + LetGoMs, e.GetUniqueId() == 1));
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
        Players.Clear(); _asked.Clear(); _heard.Clear();
        _hostSaidBye = false; _leaving.Clear(); _peerGone = false;
        _silence.Clear(); _trip = new(); _beatClock = 0;
    }

    private void OnPeer(int id, bool joined)
    {
        if (joined)
        {
            // A friend whose connection dies without a goodbye (their PC sleeps, their Wi-Fi
            // drops) is let go in about 10 s instead of ENet's 30, which left a ghost ship
            // drifting through everyone's world. The host's to set: only it holds a real
            // connection to each guest (a guest hears of the others through the host). Then the
            // welcome, first of everything this host sends it: the guest is let in (NetWelcome).
            if (_isHost && (_peer as ENetMultiplayerPeer)?.GetPeer(id) is { } guest)
            {
                Link(guest, 10000);
                RpcId(id, nameof(NetWelcome));
            }
            // TryAdd: their identity may have arrived a moment before the join event.
            Players.TryAdd(id, new PlayerInfo());
            Say($"Player {id} joined.");
            PlayerJoined?.Invoke(id);
        }
        else
        {
            Players.Remove(id, out var info); ForgetAsks(id); _heard.Remove(id); _silence.Remove(id);
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
    public static double Arriving(double warning) => System.Math.Max(warning * 0.4, warning - RoundTrip);
    // a guest's round trip to the host, in seconds (0 on the host, offline, and before the first echo):
    // the least of the beat's last echoes (Link.Trip)
    public static double RoundTrip => IsHost || I == null ? 0 : I._trip.Least;

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
        who = SenderOf(node);
        return IsHost && I != null && I.Players.ContainsKey(who);
    }
    // A RATE METER for the EXPENSIVE ANSWER to a guest's request -- not for the request itself.
    // True when `gap` seconds have passed since this peer last had this answer, and false while it
    // is early. NetMySector answers a four-byte ask with the whole world -- the mission, every
    // raider, every turret, the boss's warnings, all reliable -- so a peer calling it in a loop
    // made the host marshal dozens of packets an ask, on the channel every other peer's
    // telegraphs share. What the report itself does (where that peer is, the place it is owed)
    // stays unmetered: a dropped report is a pilot left where it spawned.
    private static readonly Dictionary<(int peer, string ask), ulong> _asked = new();
    public static bool Metered(int peer, string ask, double gap)
    {
        ulong now = Time.GetTicksMsec();
        if (_asked.TryGetValue((peer, ask), out var last) && now - last < (ulong)(gap * 1000)) return false;
        _asked[(peer, ask)] = now;
        return true;
    }
    // a peer is forgotten: so is what it asked for
    private static void ForgetAsks(int peer)
    {
        foreach (var k in _asked.Keys.Where(k => k.peer == peer).ToList()) _asked.Remove(k);
    }
    // Who sent the RPC now being run on `node` -- 0 if it is no longer in the tree. A world on its
    // way out (a scene change, the game quitting) still gets the packets already on the wire, and
    // out of the tree its Multiplayer is null: a peer's 20 Hz ship report landing in that moment
    // threw on the arena host after every check had passed.
    public static int SenderOf(Node node) => node.IsInsideTree() ? node.Multiplayer.GetRemoteSenderId() : 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// TRANSFER CHANNELS: ONE PER STREAM. An ordered unreliable packet is delivered only if nothing
// sent after it ON THE SAME CHANNEL has arrived first (ENet discards it otherwise), and every one
// of these streams used to share channel 0 (a WebRTC peer maps channels to data channels alike): on
// a link that reorders, a base report was thrown away because a ship report sent a millisecond
// later got there first -- a guest saw the hauler's hold start seconds late, by lottery. Each
// stream is ordered only against itself now. The reliable channel 0 carries everything else.
//
// A NEW STREAM IS A ROW: a constant here with a number no other row has, read by its own
// [Rpc(..., TransferChannel = NetChannels.X)] ON THE HUB -- the one node at the same path in every
// world, since an unreliable report can land after its world has gone (Hub.NetBase). A rung-3 check
// reads every Rpc attribute and fails an UnreliableOrdered one off the Hub, on channel 0, off this
// table, or sharing a channel with anything else.
// ENet opens 255 channels at both ends (channel count 0 at CreateServer and CreateClient), and a
// transfer channel N is ENet channel N + 1, so a row may go up to 254.
// ─────────────────────────────────────────────────────────────────────────────
public static class NetChannels
{
    // RELIABLE, and still its own: reliable RPCs share one ordered channel by default, so one lost
    // packet holds up everything behind it until it is resent -- and a firing battleship sends a
    // shell every quarter second. Shells, torpedoes and the missiles shot down (in order with their
    // torpedoes) go here, so a lost one never delays a telegraph, a raider's arrival or the economy.
    public const int Cosmetic = 1;
    public const int Ships = 2;         // Hub.NetShipState: each pilot's own ship, from its owner, 20 Hz
    public const int HostShips = 3;     // Hub.NetHostState: what the host decides of each ship, 10 Hz
    public const int Shields = 4;       // Hub.NetShield: a deflector's facing
    public const int Raiders = 5;       // Hub.NetRaiders: every raider, 10 Hz
    public const int Hulls = 6;         // Hub.NetHulls: what is left of every thing that holds a spot, 10 Hz
    public const int Flashes = 7;       // Hub.NetFlash: a hit's flash
    public const int Dummies = 8;       // Hub.NetDummy: a practice target's readout
    public const int Base = 9;          // Hub.NetBase: the gatherers and the hauler, 10 Hz, for the Yard
    public const int Boss = 10;         // Hub.NetBoss: the boss, 10-30 Hz
    public const int BossSounds = 11;   // Hub.NetBossSound: a special's sound
    // RELIABLE, on Net (the autoload that outlives every world): the beat and its echo (Net.NetBeat,
    // NetBeatBack). Its own row, so a beat never queues behind channel 0's reliable words; reliable, so
    // the Hub-only and one-RPC-a-row rules for ordered streams leave its two RPCs alone.
    public const int Beat = 12;
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
