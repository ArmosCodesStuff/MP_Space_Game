using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
public partial class Net : Node, Rendezvous.IHostDesk, Rendezvous.IGuestDesk
{
    public const int DefaultPort = 27015;

    public static Net I { get; private set; }

    // Single player runs as a host with nobody connected, so IsHost is true offline -- and while
    // a join is still in its handshake: until a host has accepted this build, this is still your world.
    public static bool IsHost => I == null || I._isHost;
    // IN A SESSION AND LET IN, not merely "the link is up". The transport reports a link connected the
    // moment it comes up, before Godot's handshake has let either end in; a guest that sent then was
    // throwing packets at a host that had not admitted it -- an engine error there
    // ("SYS_COMMAND_AUTH") -- and read itself as HOSTING in its own HUD and panel meanwhile. A
    // guest is online once the host's welcome arrives (NetWelcome); until then it is Connecting.
    public static bool IsOnline => I != null && I._inSession && !I.Connecting && I._peer != null
        && I._peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
    public static int LocalId => I == null ? 1 : I._localId;

    private bool _isHost = true;
    private int _localId = 1;
    private WebRtcMultiplayerPeer _peer;
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
        public Dictionary<string, int> GearLevel = new();  // what the pilot levelled each core slot to, by slot (Equipment.SanitizeLevels on arrival)
        public string CharacterId = "";                   // the pilot's stable identity: a peer id changes on a reconnect
        public string Token = "";                         // the rejoin token its last identity carried: only the host is sent one (Hub.SendIdentity)
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
        // that always exists, and what it owns -- the connections and the listener -- is what a
        // closed window must not strand.
        GetTree().AutoAcceptQuit = false;
    }
    // A CONNECTION IS UP: the handshake starts, and a host's invite has done its work -- its entry leaves
    // the pending table, the connection is the session's now, and the row it came by is kept (§3.9).
    private void OnAuthenticating(long id)
    {
        if (_isHost && Pending.Find((int)id) is { } e) { _rowOf[(int)id] = e.Row; Pending.Remove((int)id); }
        // ICE was up by now at the latest: a poll that brought both in one frame reads them together
        if (_times.TryGetValue(_isHost ? (int)id : _joinId, out var t)) { t.Channels = Time.GetTicksMsec(); if (t.Ice == 0) t.Ice = t.Channels; }
        ((SceneMultiplayer)Multiplayer).SendAuth((int)id, BitConverter.GetBytes(Claimed(Pretend.Auth)));
    }
    private void OnPeerJoined(long id) => OnPeer((int)id, true);
    private void OnPeerLeft(long id) => OnPeer((int)id, false);
    private void OnConnectionFailed() => Failed(_joinRow == Rendezvous.Paste ? ReplyRanOut : CouldNotReach);

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
        foreach (var row in Rendezvous.Paths) row.Close();
        HangAll();
        _answer?.Close(); _answer = null;                   // the guest's own gather: nobody lets it go after this
        foreach (var (p, _) in _lettingGo) p.Close();
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
        PumpSession();
        Beat(delta);
        SampleBacklog(delta);
        // Every attempt has a hard deadline of its own: a typed address 12 s from JOIN (5 for a retry), a
        // pasted invite the reply window and the host's time to link from the moment its reply was made.
        if (Connecting && _deadline > 0 && Time.GetTicksMsec() >= _deadline) { Failed(_joinRow == Rendezvous.Paste ? ReplyRanOut : CouldNotReach); return; }
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
    // THE SMOKE TEST'S WAY TO BE A DIFFERENT BUILD, to prove each refusal on a real connection: where the
    // pretended build shows (§3.8). Code: this player's knock, and its own check of a pasted invite (the
    // listener refuses the one, the other is refused before any network step). Auth: the in-band handshake
    // (OnAuth), the last guard.
    [Flags] public enum Pretend { None = 0, Code = 1, Auth = 2 }
    public static Pretend PretendAt;
    // The build this player claims at `where`: its own, or the one next to it.
    public static int Claimed(Pretend where) => PretendAt.HasFlag(where) ? Protocol ^ 1 : Protocol;

    // ONE PEER LET GO: an invite not yet connected (hung up, gone from the pending table), or a live peer --
    // a refusal, a replacement, the watchdog. Every hang-up goes through Link.Hang (a gone id is a no-op);
    // a live peer's at the end of the frame, since the call may come from inside the peer's own poll.
    public void Hang(int peer)
    {
        if (Pending.Find(peer) is { } e)
        {
            Pending.Remove(peer);
            if (e.Conn != null) e.Conn.Close(); else Link.Hang(_peer, peer);
            return;
        }
        var mp = _peer;
        Callable.From(() => Link.Hang(mp, peer)).CallDeferred();
    }

    private void OnAuth(long id, byte[] data)
    {
        int theirs = data.Length == 4 ? BitConverter.ToInt32(data) : -1;
        var sm = (SceneMultiplayer)Multiplayer;
        if (Accepts(theirs) && !PretendAt.HasFlag(Pretend.Auth)) { sm.CompleteAuth((int)id); return; }
        // NOT IsHost: a player still connecting keeps the host role over its own world until a
        // host has let it in -- which is exactly the moment this runs.
        if (!Connecting)
        {
            Say($"Refused a player on a different build of the game (theirs {theirs:x8}, this one {Protocol:x8}).");
            Hang((int)id);
        }
        else
        {
            string why = $"That host is on a different build of the game (theirs {theirs:x8}, yours {Claimed(Pretend.Auth):x8}): "
                         + "you both need the same release (Esc menu, bottom). Playing offline.";
            Callable.From(() => GoOffline(why)).CallDeferred();   // not inside the peer's own poll
        }
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
                if ((f.IsLiteral || f.IsInitOnly) && Plain(f.FieldType) && f.GetCustomAttribute<LiveAttribute>() == null)
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
    // records, tables, struct rows, arrays and COLLECTIONS of them (a List, a Dictionary, a HashSet of
    // Plain things: NetIds.Widths, Spawns.All, Hints.All). Not engine objects, and never a field marked
    // [Live]: what a run fills in (the key bindings, the character, a cache) is not the build.
    private static bool Plain(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(Color) || t == typeof(Vector2)
        || (t.IsArray && Plain(t.GetElementType()))
        || (t.GetMethod("<Clone>$") != null && !typeof(GodotObject).IsAssignableFrom(t))
        || Table(t) || StructRow(t) || Collection(t);
    // A COLLECTION TABLE: any System.Collections.Generic type (List, Dictionary, HashSet, their
    // interfaces) whose type arguments are all Plain. Show writes a list in order, a dictionary and a
    // set in key order, so two machines that built them in another order still agree.
    private static bool Collection(Type t) =>
        t.IsGenericType && t.Namespace == "System.Collections.Generic" && typeof(System.Collections.IEnumerable).IsAssignableFrom(t)
        && t.GetGenericArguments().All(Plain);
    private static bool IsSet(Type t) =>
        t.GetInterfaces().Append(t).Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ISet<>) || i.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)));
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
        System.Collections.IList l => "[" + string.Join(",", l.Cast<object>().Select(x => Show(x, depth))) + "]",
        System.Collections.IEnumerable e when IsSet(v.GetType()) => "{" + string.Join(",", e.Cast<object>()
            .Select(x => Show(x, depth)).OrderBy(x => x, StringComparer.Ordinal)) + "}",
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
        LastHost = _joinText; _dropClock = -1; Attempt = 0; CanReconnect = false; _rejoin = false; _hostSaidBye = false;
        JoinedBy = _joinRow?.Id ?? ""; _answer = null; _answering = null; _deadline = 0;
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
        if (_times.TryGetValue(_joinId, out var t)) t.Admitted = Time.GetTicksMsec();
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
        if (!_isHost || !Players.ContainsKey(from)) return;
        _heard.Add(from);
        if (_times.TryGetValue(from, out var t)) t.Admitted = Time.GetTicksMsec();
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
    private Link.Trip _trip = new();
    private double _beatClock;
    private void Beat(double delta)
    {
        if (!IsOnline || _peerGone) return;
        var peers = _isHost ? _heard.ToList() : new List<int> { 1 };
        foreach (int p in peers) _longestQuiet = System.Math.Max(_longestQuiet, _silence[p] = Link.Quiet(_silence.GetValueOrDefault(p), delta));
        _beatClock += delta;
        if (_beatClock * 1000 >= Link.BeatMs)
        {
            _beatClock = 0;
            foreach (int p in peers) RpcId(p, nameof(NetBeat), (long)Time.GetTicksMsec());
        }
        foreach (int p in peers)
        {
            if (!Link.Overdue(_silence[p])) continue;
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

    // ── the goodbye (§3.7) ──────────────────────────────────────────────────
    // A host that closed and a host that vanished look the same from the other end, and so do a guest
    // that quit and one whose Wi-Fi dropped. So whoever leaves on purpose says so first: a guest told
    // goodbye does not try to get back in, and a host told goodbye does not hold the pilot's place. THE
    // HEARER HANGS UP: the goodbye's sender keeps polling its old peer until the other end has let it go
    // or LetGoMs has passed (see Shutdown: every link that is up goes this way).
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetBye()
    {
        int from = Multiplayer.GetRemoteSenderId();
        if (!_isHost && from == 1) { _hostSaidBye = true; Hang(1); }
        else if (_isHost && Players.ContainsKey(from)) { _leaving.Add(from); Hang(from); }
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
    // HOW LONG A GOODBYE IS GIVEN: NetBye, then the other end's hang-up, which this end sees as its
    // connection closing. One lost packet costs a resend of at least a round trip (SCTP's least resend
    // time is 200 ms), and a second loss of the same packet doubles it; two fit in 2 s.
    // EVERY GOODBYE KEEPS ITS OWN TIME. Ending a session let any earlier goodbye go at once, so a
    // pilot that pressed HOST and then quit a second later cut its own goodbye off; a quitting game
    // waits for these (NetworkIdle). WebRTC's sockets are its own, not a port a new session binds, so
    // HOST leaves them to finish too.
    private readonly List<(WebRtcMultiplayerPeer peer, ulong until)> _lettingGo = new();
    private const ulong LetGoMs = 2000;
    private void PumpLetGo()
    {
        for (int i = _lettingGo.Count - 1; i >= 0; i--)
        {
            var (p, until) = _lettingGo[i];
            p.Poll();
            if (p.GetPeers().Count == 0 || Time.GetTicksMsec() >= until) { p.Close(); _lettingGo.RemoveAt(i); }
        }
    }

    // ── reconnecting ────────────────────────────────────────────────────────
    // A guest whose host drops without a goodbye goes offline -- its own world keeps running. BY A ROW
    // THAT CAN SIGNAL AGAIN BY ITSELF (Rendezvous.IRendezvousPath.Auto: the typed address) it tries the
    // same host again 3 times over about 20 s, each try a new knock; then RECONNECT, one more try on the
    // button. By invite it cannot: the host's panel makes a fresh invite for that pilot, and the pilot
    // waits for it. The host holds the pilot's place meanwhile (Session.HoldFor).
    public static readonly double[] RetryAt = { 2.0, 8.0, 14.0 };   // seconds after the drop
    private const int RetryTimeoutMs = 5000, JoinTimeoutMs = 12000;
    private bool BackIn => _retrying || _rejoin;           // a try to get back in to the host just lost, not a JOIN
    // What a typed address that never answered means, and the one next step: an address is for the
    // same network or a virtual one, and the host's firewall has to let it in.
    private string CouldNotReach => $"No answer from {_joinTarget} in {JoinTimeoutMs / 1000} s. An address works on the same network or over Radmin VPN, "
                                  + "and the host must allow Warships through Windows Firewall; for friends elsewhere, ask the host for an invite code. Playing offline.";
    // What a pasted invite whose reply never led anywhere means (§6.2).
    private string ReplyRanOut => $"{_joinTarget} did not connect within {Link.ReplyWindowS} s of your reply. MAKE A FRESH REPLY and send that one, "
                                + "or ask them for a new invite. Playing offline.";
    public string LastHost { get; private set; } = "";
    // WHO THIS GUEST JOINED, as the join named it: the invite's pilot name, or the address typed. The key its
    // rejoin tokens are kept by (Session.Rejoins).
    public string HostName => _joinTarget;
    public int Attempt { get; private set; }
    private double _dropClock = -1;
    public bool Reconnecting => _dropClock >= 0;
    public bool CanReconnect { get; private set; }
    private bool _retrying, _rejoin;
    private ulong _attemptAt, _deadline;                   // the attempt's start, and when it is given up (0: not yet running)

    private void OnHostGone()
    {
        _peerGone = true;
        // Still in the handshake: an attempt that did not happen, not a session lost.
        if (Connecting) { Failed($"Could not get in to {_joinTarget}: it let the connection go before the handshake finished. Playing offline."); return; }
        if (_hostSaidBye) { GoOffline("Host closed the session. Playing offline."); return; }
        StopReconnecting();
        if (_joinRow is { Auto: false })
        {
            Offline($"Lost the connection to {_joinTarget}. Ask them for a new invite code: your place is held {Session.HoldFor:0} s. Your own world keeps running.");
            return;
        }
        Offline($"Lost the connection to {_joinTarget}. Trying to get back in, {RetryAt.Length} times in the next 20 s. Your own world keeps running.");
        if (LastHost.Length > 0) _dropClock = 0;
    }

    // A connection that did not happen: a retry that failed, the RECONNECT button's try, or a JOIN.
    private void Failed(string reason)
    {
        if (_rejoin) { _rejoin = false; Offline($"Could not get back in to {LastHost}. RECONNECT to try again."); CanReconnect = true; return; }
        if (!Reconnecting)
        {
            // an invite can be answered again, as long as its host has not taken a reply for it (§3.3 A guest 7)
            string invite = _joinRow == Rendezvous.Paste ? _joinText : "";
            GoOffline(reason);
            FreshFrom = invite;
            return;
        }
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
        _joinGen++;                                        // an attempt still in flight connects to nothing
        Shutdown();
    }

    // ── hosting (§3.1, §3.3) ────────────────────────────────────────────────
    // A HOST IS A SERVER PEER AND ITS ROWS: CreateServer is Connected in the same call, and every row of
    // Rendezvous.Paths opens on this desk -- the paste row's clipboard pickup, the address row's listener
    // (from `port`, the nine after it, then one the OS picks). One session serves both: an invite friend in
    // another country and a Radmin friend at once. Single player never makes one: no socket, no lookup.
    public const int MaxPlayers = 8;                       // the host included, and invites not yet answered
    public bool Host(int port = DefaultPort)
    {
        StopReconnecting(); FreshFrom = "";
        Close();                                           // a session in progress ends properly: saved, the others told
        Link.StunFirst = 0;
        var p = new WebRtcMultiplayerPeer();
        if (p.CreateServer(Link.Channels()) != Error.Ok)
        {
            p.Dispose();
            GoOffline("Could not start a session: the network library refused it. Playing offline.");
            return false;
        }
        _peer = p; Multiplayer.MultiplayerPeer = p;
        _isHost = true; _localId = 1; Connecting = false; _inSession = true;
        Players.Clear(); Players[1] = new PlayerInfo();
        int from = Rendezvous.ListenFrom;
        Rendezvous.ListenFrom = port;
        foreach (var row in Rendezvous.Paths)
        {
            try { row.Open(this); }
            catch (System.Net.Sockets.SocketException) { }   // not even a port the OS picks: invites carry on without the listener
        }
        Rendezvous.ListenFrom = from;
        int at = Rendezvous.ListenPort;
        Addresses = at == 0 ? new List<string>() : new List<string> { $"Same network: {Adapters.Lan()}:{at}" }
            .Concat(Adapters.Overlays().Select(o => $"On {o.name}: {o.ip}:{at}")).ToList();
        // the listener's port as it came out: hosting and invites carry on either way (§6.1)
        Say("Hosting. INVITE A FRIEND makes a code for one friend. Friends on this network or on Radmin VPN can type an address below instead."
            + (at == 0 ? " No port could be opened for typed addresses, so friends join by invite only."
               : port != 0 && at != port ? $" Port {port} is taken on this PC, so friends typing an address use port {at}." : ""));
        SessionChanged?.Invoke();
        return true;
    }
    // What the host panel lists under INVITE A FRIEND: one line per address the listener serves.
    public List<string> Addresses { get; private set; } = new();
    // single player (and between sessions): no connection, no goodbye still on its way, no listener
    public bool NetworkIdle => _peer == null && _lettingGo.Count == 0 && Rendezvous.ListenPort == 0;

    // ── the host's desk (Rendezvous.IHostDesk) ───────────────────────────────
    // ONE ENTRY PER INVITE NOT YET CONNECTED (§3.1): made by INVITE A FRIEND (the paste row) or by a knock
    // on the listener (the address row); it leaves the table the moment its connection is up (OnAuthenticating).
    public Rendezvous.Pending Pending { get; } = new();
    public bool Full => Players.Count + Pending.Count >= MaxPlayers;
    private string FullText => $"The session is full ({MaxPlayers} players, counting invites not yet answered). Cancel an invite to free a place.";
    // the invites being gathered, each with who waits for it and what to do once it is made
    private readonly List<(Rendezvous.Entry e, Rendezvous.IRendezvousPath row, TaskCompletionSource<Rendezvous.Record> done, Action<Rendezvous.Entry> then)> _making = new();
    // the row each connected guest came by: a paste guest who drops gets a fresh invite (§3.9)
    private readonly Dictionary<int, string> _rowOf = new();

    // INVITE A FRIEND: an invite for one friend, gathered on the STUN rows, copied when it is made.
    public void Invite()
    {
        if (!_isHost || !IsOnline) return;
        if (Full) { Say(FullText); return; }
        Say("Making an invite…");
        MakeInvite(Rendezvous.Paste, 0, "", e => Say("Invite copied. Send it to one friend, privately (it holds your IP addresses). When their reply comes, "
                                                    + "just copy it: the game takes it. Or paste it here." + (e.Conn.NoStun ? NoStunHost : "")));
    }
    private const string NoStunHost = " No STUN server answered, so this invite works only on your network and over Radmin VPN.";
    // The newest invite not yet answered, as text: what COPY puts on the clipboard ("" for none).
    public string LastInvite => Pending.All.LastOrDefault(e => e.Code.Length > 0 && e.Stage == Rendezvous.Stage.Waiting)?.Code ?? "";

    public Task<Rendezvous.Record> Invite(Rendezvous.Record knock) =>
        MakeInvite(Rendezvous.Address, knock.Guest, knock.Name, null) ?? Task.FromResult<Rendezvous.Record>(null);

    // An entry with a drawn id, a connection added under it and its description asked for; the invite comes
    // out of PumpSession once the walk has sealed it (§3.3 A host 2-4). null when there is no room.
    private Task<Rendezvous.Record> MakeInvite(Rendezvous.IRendezvousPath row, uint guest, string name, Action<Rendezvous.Entry> then)
    {
        if (!_isHost || !IsOnline || Full) return null;
        var e = new Rendezvous.Entry { Id = DrawId(), Row = row.Id, Guest = guest, Name = name ?? "", Made = Time.GetTicksMsec(), Stage = Rendezvous.Stage.Waiting };
        Pending.Add(e);
        e.Conn = new Link.Gather(_peer, e.Id, row.Stun, c => c.CreateOffer());
        var done = new TaskCompletionSource<Rendezvous.Record>();
        _making.Add((e, row, done, then));
        return done.Task;
    }
    // THE HOST DRAWS EVERY ID (§3.1): 2 ... 2^31-1, not a player, not pending, not a held place's old id.
    // The invite carries it, so the guest's CreateClient(id) and this AddPeer(conn, id) agree.
    private int DrawId()
    {
        int id;
        do id = (int)Random.Shared.NextInt64(2, int.MaxValue);
        while (Players.ContainsKey(id) || Pending.Find(id) != null || Session.Places.Values.Any(h => h.OldPeer == id));
        return id;
    }

    // A code the host was handed: the reply box, or the clipboard pickup. A reply is taken; anything else
    // is named. On a guest or offline, an invite is a JOIN.
    public void TakeCode(string text)
    {
        if (!_isHost || !IsOnline) { Join(text); return; }
        if (Rendezvous.Find(text, Rendezvous.Kind.Reply) is { } reply) { Replied(reply); return; }
        Say(Rendezvous.Find(text, Rendezvous.Kind.Invite) != null ? "That is an invite: send it to a friend. Their reply goes here."
            : "That code is damaged or cut off: copy the whole message again.");
    }
    // A REPLY FOR A PENDING INVITE (§3.3 A host 6): the friend's description and candidates set on the
    // invite's connection, which then has Link.LinkMs to come up.
    public void Replied(Rendezvous.Record reply)
    {
        if (Pending.Find(reply.Id) is not { Stage: Rendezvous.Stage.Waiting, Conn: { Done: true } } e)
        {
            Say("That reply is for an invite this session no longer has (used, cancelled or expired). Send them a new invite.");
            return;
        }
        try
        {
            e.Conn.Conn.SetRemoteDescription("answer", Rendezvous.Sdp.Build(reply));
            foreach (var c in reply.Candidates) e.Conn.Conn.AddIceCandidate("0", 0, Rendezvous.Sdp.Line(c));
        }
        catch (FormatException) { Say("That code is damaged or cut off: copy the whole message again."); return; }
        e.Stage = Rendezvous.Stage.Linking; e.Until = Time.GetTicksMsec() + Link.LinkMs;
        Times(e.Id).Reply = Time.GetTicksMsec();
        if (reply.Name.Length > 0) e.Name = reply.Name;
        Say($"{Who(e)} is joining…");
    }
    private static string Who(Rendezvous.Entry e) => e.Name.Length > 0 ? e.Name : "Your friend";
    public void Refused(Rendezvous.Record knock, Rendezvous.Why why)
    {
        string who = knock.Name.Length > 0 ? knock.Name : "a player";
        Say(why switch
        {
            Rendezvous.Why.Build => $"Refused {who} on a different build of the game (theirs {knock.Build} {knock.Proto:x8}, this one {Game.Build} {Protocol:x8}).",
            Rendezvous.Why.Full => $"{who} knocked, but the session is full ({MaxPlayers} players, counting invites not yet answered).",
            _ => $"Turned {who} away: {Rendezvous.KnocksPerMinute} knocks a minute from one address is the most.",
        });
    }

    // ── the guest's desk (Rendezvous.IGuestDesk) ─────────────────────────────
    public Rendezvous.Record Knock() => new()
    {
        Kind = Rendezvous.Kind.Knock, Proto = Claimed(Pretend.Code), Guest = Rendezvous.Guest, Build = Game.Build, Name = Character.Name,
    };
    // THE ANSWER TO AN INVITE (§3.3 A guest 3-5): a client peer with the invite's id, one connection as its
    // host, the offer set; the reply comes out of PumpSession once this side's walk has sealed it.
    public Task<Rendezvous.Record> Answer(Rendezvous.Record invite)
    {
        if (!Connecting || _asking != _joinGen) throw new OperationCanceledException("that join is over");
        var mp = new WebRtcMultiplayerPeer();
        if (mp.CreateClient(invite.Id, Link.Channels()) != Error.Ok) { mp.Dispose(); throw new InvalidOperationException("the network library refused a client session"); }
        _peer = mp; Multiplayer.MultiplayerPeer = mp;
        string offer = Rendezvous.Sdp.Build(invite);
        _answer = new Link.Gather(mp, 1, _joinRow.Stun, c => c.SetRemoteDescription("offer", offer));
        var done = new TaskCompletionSource<Rendezvous.Record>();
        _answering = (invite, done);
        return done.Task;
    }
    private Link.Gather _answer;                           // this guest's connection to its host, until the host lets it in
    private (Rendezvous.Record invite, TaskCompletionSource<Rendezvous.Record> done)? _answering;
    // The reply this guest made for a pasted invite, and when (the panel's countdown: Link.ReplyWindowS).
    public string ReplyCode { get; private set; } = "";
    public ulong ReplyAt { get; private set; }
    // A pasted invite whose reply ran out, or whose connection failed: MAKE A FRESH REPLY answers it again.
    public string FreshFrom { get; private set; } = "";
    public bool FreshReply() { string invite = FreshFrom; return invite.Length > 0 && Join(invite); }

    // ── each frame: the invites being made, the pending table, the rows, this guest's answer ──
    private void PumpSession()
    {
        if (_peer == null) return;
        ulong now = Time.GetTicksMsec();
        for (int i = _making.Count - 1; i >= 0; i--)
        {
            var (e, row, done, then) = _making[i];
            if (Pending.Find(e.Id) != e || e.Conn?.Conn == null) { _making.RemoveAt(i); done.TrySetResult(null); continue; }   // hung up meanwhile
            if (!e.Conn.Step()) continue;
            _making.RemoveAt(i);
            Rendezvous.Record invite;
            try { invite = Rendezvous.Sdp.Strip(Rendezvous.Kind.Invite, e.Conn.Sdp, e.Conn.Lines); }
            catch (FormatException x) { Hang(e.Id); Say($"Could not make an invite: {x.Message}."); done.TrySetResult(null); continue; }
            invite.Id = e.Id; invite.Proto = Protocol; invite.Build = Game.Build; invite.Name = Character.Name; invite.NoStun = e.Conn.NoStun;
            int dropped = Fit(invite, row);
            if (row == Rendezvous.Paste) { e.Code = Rendezvous.Encode(invite); Rendezvous.Copy(e.Code); }
            Made("invite", e.Id, row, e.Conn, dropped, invite);
            var t = Times(e.Id); t.Row = row.Id; t.Invite = now;
            done.TrySetResult(invite);
            then?.Invoke(e);
        }
        if (_isHost && IsOnline)
        {
            foreach (var row in Rendezvous.Paths) row.Poll();
            foreach (var e in Pending.All.ToList())
            {
                if (e.Stage == Rendezvous.Stage.Linking) StampIce(e.Id, e.Conn?.Conn, now);
                if (e.Stage == Rendezvous.Stage.Linking && now >= e.Until)
                {
                    // host step 8: this invite is spent (a connection takes one answer), so a paste friend gets a fresh one at once
                    string who = Who(e), reason = Reason(e.Conn?.Conn);
                    bool paste = e.Row == Rendezvous.Paste.Id;
                    Hang(e.Id);
                    if (!paste) Say($"{who} could not get through ({reason}).");
                    else if (MakeInvite(Rendezvous.Paste, 0, e.Name, _ => Say($"{who} could not get through ({reason}). Their reply may have run out: "
                                                                      + $"a reply works for {Link.ReplyWindowS} s. A new invite for them: COPY.")) == null) Say(FullText);
                }
                else if (e.Stage == Rendezvous.Stage.Waiting && e.Row == Rendezvous.Paste.Id && now - e.Made >= Link.InviteLifeS * 1000UL)
                {
                    Hang(e.Id);
                    Say($"An invite from {Link.InviteLifeS / 60} min ago ran out unused.");
                }
            }
        }
        if (_answering is var (inv, answered) && _answer != null && _answer.Step())
        {
            _answering = null;
            foreach (var c in inv.Candidates) _answer.Conn.AddIceCandidate("0", 0, Rendezvous.Sdp.Line(c));
            Rendezvous.Record reply;
            try { reply = Rendezvous.Sdp.Strip(Rendezvous.Kind.Reply, _answer.Sdp, _answer.Lines); }
            catch (FormatException x) { answered.TrySetException(x); return; }
            reply.Id = inv.Id; reply.Name = Character.Name; reply.NoStun = _answer.NoStun;
            int dropped = Fit(reply, _joinRow);
            Made("reply", inv.Id, _joinRow, _answer, dropped, reply);
            var t = Times(inv.Id); t.Row = _joinRow.Id; t.Invite = _attemptAt; t.Reply = now; _joinId = inv.Id;
            if (_joinRow == Rendezvous.Paste)
            {
                ReplyCode = Rendezvous.Encode(reply); ReplyAt = now;
                Rendezvous.Copy(ReplyCode);
                _deadline = now + Link.ReplyWindowS * 1000UL + Link.LinkMs;   // the host's window to take it, then its time to link
                Say($"Reply copied. Send it to {_joinTarget} now: they have {Link.ReplyWindowS} s to take it."
                    + (inv.NoStun ? $" This invite works only on {_joinTarget}'s network or over Radmin VPN." : ""));
            }
            answered.TrySetResult(reply);
        }
        if (Connecting && _answering == null && _answer is { Done: true }) StampIce(_joinId, _answer.Conn, now);
        // a pasted invite's connection that failed is over at once, not at the end of its countdown
        if (Connecting && _joinRow == Rendezvous.Paste && _answer is { Done: true, Conn: { } conn }
            && conn.GetConnectionState() is WebRtcPeerConnection.ConnectionState.Failed or WebRtcPeerConnection.ConnectionState.Closed)
            Failed($"Could not get through to {_joinTarget}: {Reason(conn)}. Try again with you hosting, or use Radmin VPN (radmin-vpn.com). Playing offline.");
    }
    // A join's "ICE connected" (the report): the first frame its connection reads Connected.
    private void StampIce(int id, WebRtcPeerConnection c, ulong now)
    {
        if (_times.TryGetValue(id, out var t) && t.Ice == 0 && c?.GetConnectionState() == WebRtcPeerConnection.ConnectionState.Connected) t.Ice = now;
    }
    private static string Reason(WebRtcPeerConnection c) => c?.GetConnectionState() switch
    {
        WebRtcPeerConnection.ConnectionState.Failed => "no path between the two networks worked",
        WebRtcPeerConnection.ConnectionState.Closed => "the connection was closed",
        null => "the connection is gone",
        _ => $"no answer in {Link.LinkMs / 1000} s",
    };
    // THE FIT RULE with this PC's own addresses: an overlay's and the LAN's are ranked above the rest.
    private static int Fit(Rendezvous.Record r, Rendezvous.IRendezvousPath row)
    {
        var overlays = Adapters.Overlays().Select(o => System.Net.IPAddress.Parse(o.ip)).ToList();
        return Rendezvous.Fit(r, row, overlays, System.Net.IPAddress.Parse(Adapters.Lan()));
    }

    // ── COPY NETWORK REPORT (§6.3) ───────────────────────────────────────────
    // What a player pastes to the developer when a join did not work: this build, where the session
    // stands, every code this game made (its STUN row and how long, what the fit rule dropped, how long
    // the code is), every join's times, the most that waited per NetChannels row, the longest silence.
    // Kept for the whole game, not the session: the report is copied after a failure, offline.
    private readonly List<string> _made = new();
    // Ice: the connection reached Connected (ICE and DTLS up, PumpSession); Channels: SCTP's channels
    // open, the handshake starting (OnAuthenticating). The gap between them is the channels' own.
    private sealed class JoinTimes { public string Row = ""; public ulong Invite, Reply, Ice, Channels, Admitted; }
    private readonly Dictionary<int, JoinTimes> _times = new();
    private int _joinId;                                  // a guest's own id: the invite's, the key of its join's times
    private readonly Dictionary<int, int> _peakBacklog = new();
    private double _longestQuiet, _backlogClock;
    private const int ReportKeeps = 16;                   // codes and joins, the newest
    private JoinTimes Times(int id)
    {
        if (_times.TryGetValue(id, out var t)) return t;
        if (_times.Count >= ReportKeeps) _times.Remove(_times.MinBy(kv => kv.Value.Invite).Key);
        return _times[id] = new JoinTimes();
    }
    private void Made(string kind, int id, Rendezvous.IRendezvousPath row, Link.Gather g, int dropped, Rendezvous.Record r)
    {
        string stun = g.Row >= 0 && !g.NoStun ? $"STUN {Link.Servers[g.Row].Url} answered" : row.Stun ? "no STUN row answered" : "no STUN row asked";
        _made.Add($"{kind} {id} by {row.Id}: {stun}, sealed in {g.DoneAt - g.Began} ms; the fit dropped {dropped} candidates; {row.Measure(r)} long");
        if (_made.Count > ReportKeeps) _made.RemoveAt(0);
    }
    // Four times a second: the most waiting on each row of each live link (Link.Backlog).
    private void SampleBacklog(double delta)
    {
        if (!IsOnline || _peer is not WebRtcMultiplayerPeer mp) return;
        _backlogClock += delta;
        if (_backlogClock < 0.25) return;
        _backlogClock = 0;
        _rows ??= Link.Channels().Count;
        foreach (int p in _isHost ? _heard : new HashSet<int> { 1 })
            for (int ch = 1; ch <= _rows; ch++)
                _peakBacklog[ch] = System.Math.Max(_peakBacklog.GetValueOrDefault(ch), Link.Backlog(mp, p, ch));
    }
    private int? _rows;
    public string Report()
    {
        string At(JoinTimes t, ulong at) => at == 0 ? "never" : $"+{at - t.Invite} ms";
        var rowName = typeof(NetChannels).GetFields().Where(f => f.IsLiteral).ToDictionary(f => (int)f.GetRawConstantValue(), f => f.Name);
        var r = new System.Text.StringBuilder();
        r.AppendLine($"WARSHIPS NETWORK REPORT · build {Game.Build} ({Protocol:x8}) · {Time.GetDatetimeStringFromSystem()}");
        r.AppendLine(Connecting ? $"joining {_joinTarget}" : !IsOnline ? "offline" : _isHost ? $"hosting: {Players.Count} players, {Pending.Count} invites pending"
                     : $"a guest of {_joinTarget}, joined by {JoinedBy}");
        r.AppendLine(Rendezvous.ListenPort == 0 ? "typed addresses: no listener" : $"typed addresses: {string.Join("; ", Addresses)}");
        r.AppendLine($"STUN rows: {string.Join(", ", Link.Servers.Select(s => s.Url))}; the next walk starts at row {Link.StunFirst + 1}");
        r.AppendLine($"reply window {Link.ReplyWindowS} s; clipboard pickup on");
        r.AppendLine($"round trip {RoundTrip * 1000:0} ms; longest silence from a peer {_longestQuiet:0.00} s (row {NetChannels.Beat}, the beat)");
        r.AppendLine("peak backlog by row: " + (_peakBacklog.Count == 0 ? "none measured"
                     : string.Join(", ", _peakBacklog.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {rowName.GetValueOrDefault(kv.Key, "stream")} {kv.Value} B"))));
        foreach (var m in _made) r.AppendLine(m);
        foreach (var (id, t) in _times.OrderBy(kv => kv.Value.Invite))
            r.AppendLine($"join {id} by {t.Row}: invite made +0 ms, reply {(_isHost ? "taken" : "made")} {At(t, t.Reply)}, ICE connected {At(t, t.Ice)}, channels open {At(t, t.Channels)}, admitted {At(t, t.Admitted)}");
        r.Append($"last: {LastStatus}");
        return r.ToString();
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

    private int _joinGen, _asking;
    private string _joinTarget = "", _joinText = "";
    private Rendezvous.IRendezvousPath _joinRow;
    // The row this guest came in by ("paste" or "address"; "" offline or on the host).
    public string JoinedBy { get; private set; } = "";
    // JOIN: the text is an invite (the paste row) or an address (the address row), by Rendezvous.Paths.
    public bool Join(string text) { StopReconnecting(); FreshFrom = ""; return Connect(text, retry: false); }

    private bool Connect(string text, bool retry)
    {
        text ??= "";
        var row = Rendezvous.PathFor(text);
        Rendezvous.Record invite = null;
        if (row == null)
        {
            Say(Rendezvous.HoldsCode(text) ? "That code is damaged or cut off: copy the whole message again."
                : string.IsNullOrWhiteSpace(text) ? "Paste an invite, or type the host's address, first."
                : $"\"{text.Trim()}\" is not an invite or an address. Paste the friend's whole message, or type the host's IP, or IP:port.");
            return false;
        }
        if (row == Rendezvous.Paste)
        {
            invite = Rendezvous.Find(text, Rendezvous.Kind.Invite);
            if (invite == null) { Say("That is a reply code: it goes to the host, not into JOIN."); return false; }
            // the build check comes before any network step: no WebRTC object is made for another build's invite
            if (invite.Proto != Claimed(Pretend.Code))
            {
                Say($"This invite is from build {invite.Build} ({invite.Proto:x8}); you are on {Game.Build} ({Claimed(Pretend.Code):x8}). "
                    + "You both need the same release (Esc menu, bottom).");
                return false;
            }
        }
        bool had = _inSession;
        Close();                                           // hosting or in a session: that ends first, properly
        Players[1] = new PlayerInfo();
        Connecting = true;
        ulong now = Time.GetTicksMsec();
        _joinText = text; _joinRow = row; _retrying = retry; _attemptAt = now; ReplyCode = ""; ReplyAt = 0;
        if (!retry) Link.StunFirst = 0;
        if (invite != null) { _joinTarget = invite.Name.Length > 0 ? invite.Name : "the host"; _deadline = 0; }
        else
        {
            var (host, port) = ParseAddress(text).Value;
            _joinTarget = port == DefaultPort ? host : $"{host}:{port}";
            _deadline = now + (ulong)(BackIn ? RetryTimeoutMs : JoinTimeoutMs);
        }
        int gen = ++_joinGen; _asking = gen;
        Say(invite != null ? $"Answering {_joinTarget}'s invite ..."
            : retry ? $"Getting back in to {_joinTarget} ... (attempt {Attempt} of {RetryAt.Length})" : $"Connecting to {_joinTarget} ...");
        if (had) SessionChanged?.Invoke();                 // the guests that were here are gone: rebuild without them
        _ = Joining(gen, row.Start(text, this));
        return true;
    }
    // THE ROW'S ANSWER: an invite answered (nothing more to do: the connection comes up by itself) or a
    // refusal, or the way it failed. A timeout is left to the attempt's deadline, so a silent host reads
    // "No answer ... in 12 s" at 12 s, not at the 3 s a connection's read is given.
    private async Task Joining(int gen, Task<Rendezvous.Record> start)
    {
        Rendezvous.Record got = null; string why = null; bool refusedBuild = false;
        try { got = await start; }
        catch (System.Net.Sockets.SocketException x) when (x.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused)
        { why = $"Nothing is hosting at {_joinTarget}: that PC turned the connection away. Check the address, and that the host pressed HOST THIS WORLD. Playing offline."; }
        catch (System.Net.Sockets.SocketException x) when (x.SocketErrorCode is System.Net.Sockets.SocketError.HostNotFound or System.Net.Sockets.SocketError.NoData)
        { why = $"Could not find {_joinTarget}. Check the address. Playing offline."; }
        catch (Exception x) when (x is OperationCanceledException or System.IO.IOException or System.Net.Sockets.SocketException) { }
        catch (FormatException) { why = $"{_joinTarget} answered with something that is not Warships. Playing offline."; }
        catch (InvalidOperationException x) { why = $"Could not start a session: {x.Message}. Playing offline."; }
        if (gen != _joinGen || !Connecting) return;        // superseded, or already over
        if (got is { Kind: Rendezvous.Kind.Refuse } r)
        {
            refusedBuild = r.Why == Rendezvous.Why.Build;
            why = r.Why switch
            {
                Rendezvous.Why.Build => $"That host is on a different build of the game (theirs {r.Build} {r.Proto:x8}, yours {Game.Build} {Claimed(Pretend.Code):x8}): "
                                        + "you both need the same release (Esc menu, bottom). Playing offline.",
                Rendezvous.Why.Full => $"{_joinTarget} is full ({MaxPlayers} players, counting invites not yet answered). Playing offline.",
                Rendezvous.Why.Rate => $"{_joinTarget} turned the knock away: too many from this address in a minute. Wait a minute and JOIN again. Playing offline.",
                _ => $"{_joinTarget} stopped hosting while this knocked. Playing offline.",
            };
        }
        if (why == null) return;
        if (refusedBuild) GoOffline(why); else Failed(why);
    }

    // What a name's lookup gives the dialer: its IPv4 address when it has one, else its IPv6 one -- a name
    // with only an AAAA record read as "no such host". Link-local IPv6 is left out: it means
    // something only on the adapter it came from, which a name cannot say.
    public static string DialAddress(IEnumerable<System.Net.IPAddress> found) =>
        found.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                         || (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !a.IsIPv6LinkLocal))
             .OrderBy(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 0 : 1)
             .FirstOrDefault()?.ToString() ?? "";

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

    // EVERY INVITE NOT YET CONNECTED HUNG UP (Hang: a pending entry's connection is closed here and now),
    // every record being made let go, and the guest's wait for its answer called off. Shutdown and _ExitTree.
    private void HangAll()
    {
        foreach (var e in Pending.All.ToList()) Hang(e.Id);
        foreach (var m in _making) m.done.TrySetResult(null);
        _making.Clear();
        if (_answering is var (_, answered)) answered.TrySetCanceled();
        _answering = null;
    }

    private void Shutdown()
    {
        // the rows first: no knock and no pickup reaches a session that is ending; then every invite not yet
        // connected is hung up, because GetPeers lists them and nobody else will (§3.7)
        foreach (var row in Rendezvous.Paths) row.Close();
        HangAll();
        _answer = null;
        if (_peer != null)
        {
            // EVERY LINK THAT IS UP IS LET GO GENTLY, goodbye or not: what it has queued goes first -- the
            // goodbye (NetBye), or a refused player's own build, which the host must hear to say it refused
            // one. Not when the other end is already gone, nor for the smoke test's crash.
            if (!SkipGoodbye && !_peerGone && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected && _peer.GetPeers().Count > 0)
                _lettingGo.Add((_peer, Time.GetTicksMsec() + LetGoMs));
            else _peer.Close();
            _peer = null;
        }
        Addresses = new(); JoinedBy = ""; _rowOf.Clear(); _deadline = 0;
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
            // The welcome, first of everything this host sends it: the guest is let in (NetWelcome).
            if (_isHost) RpcId(id, nameof(NetWelcome));
            // TryAdd: their identity may have arrived a moment before the join event.
            Players.TryAdd(id, new PlayerInfo());
            Say($"Player {id} joined.");
            PlayerJoined?.Invoke(id);
        }
        else
        {
            Players.Remove(id, out var info); ForgetAsks(id); _heard.Remove(id); _silence.Remove(id);
            bool onPurpose = _leaving.Remove(id);
            bool paste = _rowOf.Remove(id, out var row) && row == Rendezvous.Paste.Id;
            Say(onPurpose || !_isHost ? $"Player {id} left." : $"Player {id} dropped.");   // only the host hears goodbyes
            PlayerLeft?.Invoke(id, info ?? new PlayerInfo(), onPurpose);
            // A PASTE GUEST CANNOT KNOCK AGAIN (§3.9): its way back is a fresh invite, made at once -- unless
            // its pilot is already back under another id (its rejoin token beat this old link: Hub let it go).
            bool back = info?.CharacterId is { Length: > 0 } cid && Players.Values.Any(p => p.CharacterId == cid);
            if (_isHost && IsOnline && paste && !onPurpose && !back)
            {
                string who = info?.Name is { Length: > 0 } n ? n : "Your friend";
                if (MakeInvite(Rendezvous.Paste, 0, info?.Name ?? "", _ => Say($"{who} dropped. Send them this invite to come back "
                                                                              + $"(their place is held {Session.HoldFor:0} s): COPY.")) == null) Say(FullText);
            }
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
    // HOW FAR A PEER'S OWN CLOCK MAY HONESTLY DISAGREE WITH THE HOST'S, in seconds (the active reload's
    // judgement, ActiveReload.Judge): the owner's report step, a frame and twice the peer's measured
    // jitter, never more than LeewayCap -- and 0 for the host's own ship and offline. This transport
    // (WebRTC) reports no per-peer jitter, so a guest is given the cap.
    public const double LeewayCap = 0.10;
    public static double Leeway(int peer) => !IsOnline || peer == LocalId ? 0 : LeewayCap;

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
    [Live] private static readonly Dictionary<(int peer, string ask), ulong> _asked = new();
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
// TRANSFER CHANNELS: ONE PER STREAM. Every row is a data channel of its own, and so its own SCTP
// stream: a packet lost on one row holds back only that row until it is resent. Every one of these
// streams used to share channel 0, where a base report waited on (or, on the old transport, was
// thrown away behind) a ship report sent a millisecond later -- a guest saw the hauler's hold start
// seconds late, by lottery. The reliable channel 0 carries everything else.
//
// A NEW STREAM IS A ROW: a constant here with a number no other row has, read by its own
// [Rpc(..., TransferChannel = NetChannels.X)] ON THE HUB -- the one node at the same path in every
// world, since an unreliable report can land after its world has gone (Hub.NetBase). A rung-3 check
// reads every Rpc attribute and fails an UnreliableOrdered one off the Hub, on channel 0, off this
// table, or sharing a channel with anything else.
// Each row is negotiated when a link is made (Link.Channels reads every [Rpc] for them), and transfer
// channel N is the peer's data channel 2 + N (channel 0's three come first).
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

// WHAT A RUN FILLS IN, not what the build is: a static readonly collection marked [Live] (the pilot's
// save, the session, the key bindings, a cache) is never part of Net.Fingerprint. Every other static
// readonly List / Dictionary / HashSet of Plain things is a constant table and is hashed; a new live one
// takes this mark in the same edit, or the build's fingerprint moves as a run fills it
// (FollowFingerprintCollectionChecks guards both).
[AttributeUsage(AttributeTargets.Field)]
public sealed class LiveAttribute : Attribute { }
