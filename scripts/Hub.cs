using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// HUB — the safe world. It idles whether or not you are here, and it is where
// every session starts, online or off.
//
// Layout, as specified:
//     small sun + a short asteroid belt ABOVE the base
//     a salvage wreck to the LEFT
//     the base in the CENTRE
//     the outbound wormhole to the RIGHT  (trade runs, and the way to other systems)
//
// Everything that produces is host-owned (Net.Sim). Clients render the same world
// but never tick it, so two players in one hub always see the same numbers.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Hub : Node2D
{
    // ── the hub's layout ─────────────────────────────────────────────────────
    public static readonly Vector2 BasePos = Vector2.Zero;
    // The base: base_station.png, 1024 x 1024 (its centre is the station's centre),
    // drawn at 0.6. Its bottom pad was enlarged to 140 x 84 u and is the hauler's
    // landing pad. The haul lane is the pad's centre line; the portal sits on it, so
    // the hauler's every move is flat.
    public const float BaseScale = 0.6f;
    public static readonly Vector2 HaulerPad = new(0f, 219f);     // the enlarged bottom pad's centre: base_station.png row 877, (877 - 512) x 0.6
    public static float LaneY => HaulerPad.Y;
    public const float BaseBottom = 260f;                     // the pad's lower edge
    public static readonly Vector2 StemFoot = new(0f, 175f);  // where the pad hangs from the station
    // Both fields' nearest edges sit 1500 u from the base's centre (about 6.7 battleship
    // lengths): room for a blockade outside the base's 600 u missile cover. Measured to each
    // field's boundary -- the belt's nearest rock edge (sun at y -1794 with 9 rocks on the
    // 520 x 286 ellipse, rocks 15 u), the wreck's visible edge toward the base (340 u out).
    private const float FieldEdge = 1500f;
    public static readonly Vector2 SunPos    = new(0, -1794);
    public static readonly Vector2 WreckPos  = new(-1840, 60);
    public static readonly Vector2 PortalPos = new(1500, 219);
    // Threat Intelligence Operations: south-west of the base, clear of the wreck, the
    // salvage routes, the haul lane and the dummies. (Bounty missions come next.)
    public static readonly Vector2 TioPos = new(-650, 640);
    public const float TioHeight = 260f;   // on the lane through that pad
    // three dummies south-east of the base, below the haul lane and far enough apart
    // that a click is never ambiguous
    public static readonly Vector2[] DummyPos = { new(600, 670), new(900, 550), new(900, 850) };
    private const float BeltR = 520f;          // a short belt, tight around the sun
    private const int   BeltRocks = 9;

    // True while the keyboard and mouse belong to the UI: a text box has focus or the
    // character creator is open. PlayerShip reads this before flying or aiming.
    public static bool ControlsLocked { get; private set; }

    private readonly List<Node2D> _rocks = new();
    public IReadOnlyList<Node2D> Rocks => _rocks;
    public Portal Portal { get; private set; }
    public Yard Yard { get; private set; }                    // the idle economy (home only)

    // ── sectors: HOME (the base) and the ARENA (a bounty). One scene, built either
    // way; the host moves every peer between them together (EnterSector). ──────
    public enum SectorKind { Home, Arena }
    public static SectorKind Sector = SectorKind.Home;
    public static bool InArena => Sector == SectorKind.Arena;
    public static Hub I { get; private set; }

    // A WAYPOINT: a landmark or a pilot picked on the radar -- somewhere to warp to, never a
    // target for weapons. A target and a waypoint exclude each other; Esc or a double-click
    // on empty space clears either.
    public Vector2? Waypoint { get; private set; }
    public string WaypointName { get; private set; } = "";
    private float WaypointRadius { get; set; }   // private already: the inner `private set` would be redundant
    public void SelectTarget(IHittable h) { _selected = h; Waypoint = null; }
    public void SelectWaypoint(string name, Vector2 at, float radius) { _selected = null; Waypoint = at; WaypointName = name; WaypointRadius = radius; }
    public void ClearSelection() { _selected = null; Waypoint = null; }
    // what a warp aims at right now: the target, else the waypoint
    public (bool has, Vector2 at, float radius) WarpAim() =>
        _selected != null && _selected.Alive ? (true, _selected.Position, _selected.HitRadius)
        : Waypoint is { } w ? (true, w, WaypointRadius) : (false, Vector2.Zero, 0f);
    public Boss Boss { get; private set; }
    private double _arenaEndT = -1;                         // counts down to going home
    public bool MissionWon { get; private set; }
    private EscMenu _esc;
    public bool EscMenuOpen => IsInstanceValid(_esc);
    public void ToggleEscMenu()
    {
        if (IsInstanceValid(_esc)) { _esc.QueueFree(); _esc = null; return; }
        _esc = new EscMenu { Hub = this }; AddChild(_esc);
    }
    public System.Collections.Generic.IEnumerable<PlayerShip> Ships => _ships.Values;
    private BasePanel _base;
    private PilotWindow _pilot;
    private TioWindow _tio;
    private EquipmentWindow _equip;
    private Sprite2D _tioSprite;
    private Portal _missionPortal;
    private readonly Dictionary<int, PlayerShip> _ships = new();
    private Camera2D _cam;

    // ── camera ──────────────────────────────────────────────────────────────
    // The wheel zooms, from 33% further out than the default to 1.5x closer. Y frees
    // the camera: arrow keys, or the mouse against a screen edge, move it -- no
    // further than the ship's CameraRange (5000 for capital ships). Y again returns
    // it to the ship, keeping the zoom.
    public const float DefaultZoom = 0.9f, ZoomOutMax = 1.33f, ZoomInMax = 1.5f;
    public const float PanSpeed = 1400f, EdgeBand = 14f;
    public float ZoomLevel { get; private set; } = DefaultZoom;
    public bool FreeCamera { get; private set; }
    public Vector2 CameraPosition => _cam.Position;
    private Label _hud, _help;
    private CanvasLayer _hudLayer;
    private CharacterCreator _creator;
    private StatsWindow _statsWin;
    private readonly List<TargetDummy> _dummies = new();

    // A non-instant ability waiting for its spot: left-click confirms it at the
    // cursor, right-click or Esc cancels. Nothing uses this yet; it is the slot the
    // first placed ability plugs into, so left-click stays "select, or confirm".
    private System.Action<Vector2> _placing;
    private string _placingLabel;
    public bool Placing => _placing != null;

    // The local player's selected target. A UI choice, not an order: it is sent to
    // the host only as the argument of an order (Space, F).
    private IHittable _selected;
    public IHittable Selected => _selected != null && _selected.Alive ? _selected : null;
    private readonly List<(Vector2 a, Vector2 b, Color c, double t)> _flashes = new();
    public IReadOnlyList<(Vector2 a, Vector2 b, Color c, double t)> Flashes => _flashes;

    public override void _Ready()
    {
        Ui.Install(GetTree());                                  // the game-wide look
        // Background stars: a static, tiled image on a deep screen-space layer, behind
        // everything in the world. Purely local -- nothing about it is networked.
        var sky = new CanvasLayer { Layer = -100, Name = "Stars" }; AddChild(sky);
        var stars = new TextureRect { Texture = GD.Load<Texture2D>("res://stars.png"),
                                      StretchMode = TextureRect.StretchModeEnum.Tile, MouseFilter = Control.MouseFilterEnum.Ignore };
        stars.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        sky.AddChild(stars);

        I = this;
        // BEFORE BuildWorld, NOT AFTER. Normally the select screen has loaded a character; this
        // covers running the hub directly (editor F6, the smoke test). It used to sit fifty lines
        // below, and BuildWorld builds the Yard, whose _Ready reads Character.Base* -- so the base
        // was loaded from statics that were still zero, and the Yard's first-frame autosave then
        // wrote those zeros back over the character file EnsureLoaded had just read. Silent base
        // loss, every time a hub was entered without the select screen.
        Character.EnsureLoaded();
        Music.CombatZone = InArena;
        if (!InArena) BuildWorld();                  // the arena's boss comes after Combat.Clear, below
        // Hit flashes get their own layer ABOVE the hulls (ships sit at z 4) and below the
        // turret sprites, so a shot is seen leaving a turret on top of the ship -- drawn at
        // the world's own level they started underneath it.
        AddChild(new FlashLayer { Hub = this, ZIndex = 8, ZAsRelative = false });
        _cam = new Camera2D { Zoom = new Vector2(DefaultZoom, DefaultZoom) }; AddChild(_cam); _cam.MakeCurrent();

        var layer = new CanvasLayer(); AddChild(layer);
        _hudLayer = layer;
        var baseBtn = new Button { Text = "BASE (B)", Name = "BaseButton", FocusMode = Control.FocusModeEnum.None };
        baseBtn.Pressed += ToggleBase;
        var baseWrap = Ui.Wrap(baseBtn); baseWrap.Position = new Vector2(256, 48);
        baseWrap.Visible = !InArena;                                   // no base out there
        layer.AddChild(baseWrap);
        var pilotBtn = new Button { Text = "PILOT (L)", Name = "PilotButton", FocusMode = Control.FocusModeEnum.None };
        pilotBtn.Pressed += TogglePilot;
        var pilotWrap = Ui.Wrap(pilotBtn); pilotWrap.Position = new Vector2(356, 48);
        layer.AddChild(pilotWrap);
        var eqBtn = new Button { Text = "EQUIPMENT (I)", Name = "EquipmentButton", FocusMode = Control.FocusModeEnum.None };
        eqBtn.Pressed += ToggleEquipment;
        var eqWrap = Ui.Wrap(eqBtn); eqWrap.Position = new Vector2(446, 48);
        layer.AddChild(eqWrap);
        // the stats line sits on its own panel so it reads over anything behind it
        var hudPanel = new PanelContainer { Position = new Vector2(10, 8), Name = "HudPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        Ui.Panelise(hudPanel, 12);
        layer.AddChild(hudPanel);
        _hud = new Label();
        _hud.AddThemeFontSizeOverride("font_size", 16);
        hudPanel.AddChild(_hud);

        // class-specific controls line; rewritten each frame so a class change or a
        // rebind shows at once (see Abilities.ControlsHint)
        _help = new Label { Text = "",
                               Modulate = new Color(1, 1, 1, 0.5f) };
        _help.AddThemeFontSizeOverride("font_size", 12);
        // anchored to the bottom edge; offsets, not Position, place an anchored control
        var helpPanel = Ui.Wrap(_help, 5);          // the controls line sits on a panel too
        helpPanel.Name = "HelpPanel";
        helpPanel.AnchorTop = helpPanel.AnchorBottom = 1f;
        helpPanel.OffsetLeft = 8; helpPanel.OffsetTop = -34; helpPanel.OffsetBottom = -6;
        helpPanel.GrowVertical = Control.GrowDirection.Begin;
        layer.AddChild(helpPanel);

        layer.AddChild(new HullHud { Hub = this });
        layer.AddChild(new BossBar { Hub = this });                 // shows itself in the arena
        layer.AddChild(new Radar { Hub = this });
        layer.AddChild(new AbilityBar { Hub = this });
        if (!InArena) layer.AddChild(new HaulerHud { Hub = this });

        Settings.EnsureLoaded();       // ability key bindings
        Combat.Clear();
        // Flashes are made on the host, where the shots happen. Guests are sent them,
        // or a guest firing at the dummy would see nothing at all.
        Combat.OnFlash = (a, b, c, snd) =>
        {
            AddFlash(a, b, c, snd);
            if (Net.IsHost && Net.IsOnline) Rpc(nameof(NetFlash), a, b, c, (int)snd);
        };
        // Torpedoes: the host's copy deals damage; guests get the launch and fly a
        // cosmetic copy (the run is straight and steady, so it lands in the same place).
        Combat.OnShell = (from, dir, speed, range, dmg, source) =>
        {
            AddChild(new Shell { Position = from, Dir = dir, Speed = speed, Range = range, Damage = dmg, Source = source });
            Sfx.Cannon(from);
            if (Net.IsHost && Net.IsOnline) Rpc(nameof(NetShell), from, dir, speed, range);
        };
        Combat.OnTorpedo = (from, dir, speed, range, dmg, target, turn, heavy, hostile, source, hitSource, size) =>
        {
            int id = hostile ? Combat.NextMissileId() : 0;       // hostile missiles can be shot down
            SpawnTorpedo(from, dir, speed, range, dmg, false, target, turn, heavy, hostile, source, id, hitSource, size);
            if (Net.IsHost && Net.IsOnline) Rpc(nameof(NetTorpedo), from, dir, speed, range, target, turn, heavy, hostile, id, size);
        };

        // THE SHIPS BEFORE THE BOSS. The boss sizes itself to the party in its _Ready, and it used
        // to be built first, count zero ships, and come out a solo boss for every party.
        _guestBefore = !Net.IsHost;
        RebuildShips();
        if (InArena) BuildArena();                   // registered as a target AFTER Combat.Clear
        else if (PendingRaidLevel > 0 && Net.IsHost) { _raidLevel = PendingRaidLevel; _raidIn = RaidDelay; }
        PendingRaidLevel = 0;
        // the dummies live at home: 1 (plain), 3 (armed), and where 2 stood, two PRACTICE
        // FIGHTERS (4 and 5) -- light-fighter targets for point defence to train on
        var targets = new (int n, Vector2 at, bool fighter)[] { (1, DummyPos[0], false), (3, DummyPos[2], false),
                                                                (4, DummyPos[1] + new Vector2(-38, -22), true), (5, DummyPos[1] + new Vector2(38, 22), true) };
        foreach (var (n, at, fighter) in InArena ? System.Array.Empty<(int, Vector2, bool)>() : targets)
        {
            var d = new TargetDummy { Name = fighter ? $"PracticeFighter{n - 3}" : $"TargetDummy{n}", Number = n, Armed = n == 3, Fighter = fighter, Position = at, ZIndex = 3 };
            AddChild(d); _dummies.Add(d);
            Combat.Hostiles.Add(d);
            d.Published = (last, avg, total) => { if (Net.IsOnline) Rpc(nameof(NetDummy), n, last, avg, total); };
        }

        if (Net.I != null)
        {
            Net.I.PlayerJoined   += OnPlayerJoined;
            Net.I.PlayerLeft     += DespawnFor;
            Net.I.SessionChanged += OnSessionChanged;
            ReportSector();                                      // this world is loaded: tell the host where we are
        }
    }

    // Net is an autoload and outlives this scene. Every handler added in _Ready comes
    // off here, or the next join or status line calls into a freed Hub.
    public override void _ExitTree()
    {
        if (Net.I != null)
        {
            Net.I.PlayerJoined   -= OnPlayerJoined;
            Net.I.PlayerLeft     -= DespawnFor;
            Net.I.SessionChanged -= OnSessionChanged;
        }
        Combat.Clear();
        // A static pointing at a freed node is worse than a null one: it still reads non-null, so
        // `Hub.I?.` sails straight through and calls into a dead object. Guarded on `== this`
        // because a scene change can build the next Hub before this one is out of the tree.
        if (I == this) I = null;
        ControlsLocked = false;
        if (Music.I != null) Music.I.Target = Music.Mood.Ambient;
    }

    // ── world ────────────────────────────────────────────────────────────────
    private void BuildWorld()
    {
        var neb = GD.Load<Texture2D>("res://nebula.png");
        AddChild(new Sprite2D { Texture = neb, Position = new Vector2(0, -200), Scale = new Vector2(7, 7),
                                Modulate = new Color(0.30f, 0.42f, 0.72f, 0.28f), ZIndex = -10 });

        AddChild(new Sun { Position = SunPos });

        var tex = new[] { "res://asteroid_1.png", "res://asteroid_2.png", "res://asteroid_3.png" };
        for (int i = 0; i < BeltRocks; i++)
        {
            float a = Mathf.Tau * i / BeltRocks;
            var r = new Sprite2D {
                Texture = GD.Load<Texture2D>(tex[i % 3]),
                Position = SunPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * BeltR,
                Scale = new Vector2(0.5f, 0.5f), ZIndex = 1 };
            AddChild(r); _rocks.Add(r);
        }

        // the wreck: its edge FieldEdge from the base, like the belt's
        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>("res://behemoth_wreck.png"),
                                Position = WreckPos, Scale = new Vector2(0.34f, 0.34f), ZIndex = 1 });

        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>("res://base_station.png"), Name = "Base",
                                Position = BasePos, Scale = new Vector2(BaseScale, BaseScale), ZIndex = 2 });
        AddChild(new BaseDefense { Hub = this });                 // the base's own laser and missiles
        var tioTex = GD.Load<Texture2D>("res://tio_building.png");
        var tio = new Sprite2D { Texture = tioTex, Name = "TIO", Position = TioPos, Scale = Vector2.One * (TioHeight / tioTex.GetHeight()), ZIndex = 2 };
        AddChild(tio);
        _tioSprite = tio;
        AddChild(new MissionBar { Hub = this, ZIndex = 6 });
        // its name, drawn in the world like every world label (a UI control here counted
        // as "off-screen" whenever the camera looked elsewhere)
        AddChild(new WorldLabel { Text = "THREAT INTELLIGENCE OPERATIONS", Position = TioPos + new Vector2(0, TioHeight / 2 + 16), ZIndex = 2 });

        Portal = new Portal { Position = PortalPos, Name = "Portal" };
        AddChild(Portal);
        Yard = new Yard { Hub = this, Name = "Yard" };
        AddChild(Yard);
    }

    // ── ships and sessions ───────────────────────────────────────────────────
    // Ships are keyed and authorised by peer id, and LocalId changes when you host,
    // join or drop. So on any session change every ship is thrown away and rebuilt
    // from Net.Players. Before this, joining left your offline ship keyed as peer 1:
    // on the guest it became the host's ship, and the guest never got one of its own.
    // ── which world each guest is in ─────────────────────────────────────────
    // A guest reports its sector whenever its world loads. Home-only nodes (the yard) send
    // only to guests at home: in a sector change the peers arrive at different moments, and
    // a message for a node a peer does not have is an engine error (seen in the arena run).
    // Static: it must outlive the host's own scene reloads.
    private static readonly Dictionary<int, SectorKind> _peerSector = new();
    public static SectorKind? PeerSector(int id) => _peerSector.TryGetValue(id, out var s) ? s : null;
    // Send only to the peers that are IN a given sector. A peer still loading that world has no
    // such node yet, and Godot logs "Node not found ... Invalid packet received" for every stray
    // packet -- it is not fatal, but it is noise that hides real errors, and that peer misses the
    // state anyway. The yard has needed this since the first arena run; the BOSS needs the same,
    // because the host starts broadcasting its state before every guest has built the arena.
    public void RpcToSector(SectorKind k, Node node, StringName method, params Variant[] args)
    {
        foreach (var id in Multiplayer.GetPeers())
            if (_peerSector.TryGetValue(id, out var s) && s == k) node.RpcId(id, method, args);
    }
    public void RpcHome(Node node, StringName method, params Variant[] args) => RpcToSector(SectorKind.Home, node, method, args);
    // A guest's report is also the moment to bring it up to date. Much of the world is sent only
    // when it CHANGES -- the mission, which raiders exist, a win -- so a friend who joined late,
    // or whose world had not finished loading, never heard of any of it: raiders attacked it from
    // empty space, and an open mission portal was invisible to the one pilot it waited for. And a
    // guest in the wrong world is brought into the host's: the party is wherever the host is.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetMySector(int s)
    {
        if (!Net.FromPlayer(this, out int who)) return;
        _peerSector[who] = (SectorKind)s;
        if ((SectorKind)s != Sector) { RpcId(who, nameof(NetSector), (int)Sector); return; }
        RpcId(who, nameof(NetMission), MissionArgs());
        foreach (var r in Raiders) RpcId(who, nameof(NetRaiderSpawn), r.NetId, r.Position, (int)r.Kind, r.Strength);
        if (MissionWon) RpcId(who, nameof(NetWon));
    }
    private void ReportSector() { if (!Net.IsHost) Net.AskHost(this, nameof(NetMySector), (int)Sector); }

    private bool _guestBefore;
    private void OnSessionChanged()
    {
        // Raiders belong to whoever simulates them. Becoming a guest, the ones this world had
        // would sit frozen (nothing simulates them any more); leaving a host, its copies would come
        // alive in your own world and attack your base. Either way they go.
        if (_guestBefore || !Net.IsHost) foreach (var r in Raiders.ToList()) DropRaider(r, burst: false);
        _guestBefore = !Net.IsHost;
        ReportSector();
        if (Net.IsHost) foreach (var id in _peerSector.Keys.ToList()) if (!Net.IsOnline || !Multiplayer.GetPeers().Contains(id)) _peerSector.Remove(id);
        // the host gone while the party is in the arena: home, to your own base
        if (InArena && !Net.IsOnline) { GoTo(SectorKind.Home); return; }
        Yard?.OnSessionChanged(!Net.IsHost);    // parks or restores your own yard (home only)
        RebuildShips();
        // a guest that just connected introduces itself; the host and existing
        // guests introduce themselves to it from OnPlayerJoined
        if (Net.IsOnline && !Net.IsHost) SendIdentity();
    }

    private void RebuildShips()
    {
        foreach (var s in _ships.Values)
            if (IsInstanceValid(s)) { RemoveChild(s); s.QueueFree(); }   // out now, so the name frees up
        _ships.Clear();

        SpawnFor(Net.LocalId);          // your own ship, always
        if (Net.I != null)
            foreach (var id in Net.I.Players.Keys) SpawnFor(id);
        if (IsInstanceValid(_statsWin)) _statsWin.Ship = MyShip;
    }

    private void OnPlayerJoined(int peerId)
    {
        SpawnFor(peerId);
        SendIdentity(peerId);           // tell the newcomer who we are
    }

    private void SpawnFor(int peerId)
    {
        if (_ships.ContainsKey(peerId)) return;
        var s = new PlayerShip { Name = PlayerShip.NodeName(peerId) };
        AddChild(s);
        // spawn clear of the base: its half-height (180 u) plus a ship's half-length and a gap
        s.Init(peerId, BasePos + new Vector2(0, BaseBottom + 140f + 60 * _ships.Count));   // clear of the pad even at 224 u
        _ships[peerId] = s;
        ApplyIdentity(s);
    }

    private void DespawnFor(int peerId)
    {
        if (!_ships.TryGetValue(peerId, out var s)) return;
        _ships.Remove(peerId);
        if (IsInstanceValid(s)) { RemoveChild(s); s.QueueFree(); }
    }

    // ── identity ─────────────────────────────────────────────────────────────
    // Identity is owner-announced (see Character). It rides on the Hub rather than
    // the ship because the Hub exists on every peer before any ship does, so an
    // announcement can never arrive at a node that is not there yet.
    private void ApplyLocalIdentity()
    {
        if (_ships.TryGetValue(Net.LocalId, out var me) && IsInstanceValid(me)) ApplyIdentity(me);
    }

    // THE ONE WAY A SHIP GETS ITS PILOT: name, colours, class, the pilot's upgrades and gear --
    // your own from Character, anyone else's from what they announced. A ship spawned used to get
    // only the name and colours, so after any scene change (to the arena and back) every ship flew
    // at base stats: the host judged guests' hulls and damage without their upgrades.
    private void ApplyIdentity(PlayerShip s)
    {
        if (s.OwnerId == Net.LocalId)
        {
            s.SetIdentity(Character.Name, Character.Main, Character.Accent, Character.Class);
            s.SetProgress(Character.Bought);
            s.SetEquipment(Character.LoadoutFor(Character.Class));
        }
        else if (Net.I != null && Net.I.Players.TryGetValue(s.OwnerId, out var p) && p.HasIdentity)
        {
            s.SetIdentity(p.Name, p.Main, p.Accent, p.Class);
            s.SetProgress(p.Bought);
            s.SetEquipment(p.Equip);
        }
    }

    private void SendIdentity(int toPeer = 0)
    {
        if (!Net.IsOnline) return;
        // a guest mid-handshake still reports LocalId 1; peers would rightly reject
        // that as impersonating the host, so wait for the real id (OnSessionChanged)
        if (!Net.IsHost && Net.LocalId == 1) return;
        var args = new Variant[] { Net.LocalId, Character.Name, Character.Main, Character.Accent, (int)Character.Class, Character.Bought, Character.Level, Character.LoadoutFor(Character.Class) };
        if (toPeer == 0) Rpc(nameof(NetIdentity), args);
        else             RpcId(toPeer, nameof(NetIdentity), args);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetIdentity(int peer, string name, Color main, Color accent, int cls, int[] bought, int level, string[] equip)
    {
        // a peer may only describe itself
        if (Multiplayer.GetRemoteSenderId() != peer || Net.I == null) return;
        if (!Net.I.Players.TryGetValue(peer, out var p)) Net.I.Players[peer] = p = new Net.PlayerInfo();
        // Everything else off this wire is sanitised (bought is null-guarded below, equip inside
        // Equipment.Sanitize); the name was not. A null here threw on .Length, and a null that got
        // past became a null Pilot in every label that draws it.
        name = string.IsNullOrEmpty(name) ? Character.Defaults.Name : name.Length > 24 ? name[..24] : name;
        // purchases the claimed level could not have paid for are refused outright
        if (!Progression.Affordable(bought, level)) bought = new int[Progression.All.Length];
        (p.Name, p.Main, p.Accent, p.Class, p.Bought, p.Equip, p.HasIdentity) =
            (name, main, accent, Classes.Sanitize(cls), bought, equip ?? System.Array.Empty<string>(), true);
        if (_ships.TryGetValue(peer, out var s) && IsInstanceValid(s)) ApplyIdentity(s);
    }

    // ── missions: Threat Intelligence Operations (host-authoritative) ────────
    public enum MissionState { Idle, Opening, PortalOpen }
    public MissionState Mission { get; private set; }
    public double MissionT { get; private set; }
    public const double PortalOpenTime = 3.0;
    // the mission portal opens off the TIO's top-right corner
    // From constants, not the TIO's sprite: in the arena, or in the frame a scene is being
    // swapped, there is no sprite -- and asking for this crashed (found by the arena run).
    private const float TioHalfWidth = TioHeight * 630f / 876f / 2f;     // the art is 630 x 876 px
    public Vector2 MissionPortalPos => TioPos + new Vector2(TioHalfWidth + 110f, -TioHeight / 2f - 70f);
    private readonly Dictionary<int, bool> _ready = new();
    public IEnumerable<int> PartyIds => _ships.Keys;                 // the party is everyone in the session
    public bool IsReady(int id) => _ready.TryGetValue(id, out var r) && r;
    public bool AllReady => _ships.Count > 0 && _ships.Keys.All(IsReady);
    public string PilotName(int id) => _ships.TryGetValue(id, out var s) && IsInstanceValid(s) ? s.Pilot : $"pilot {id}";
    public bool TioOpen => IsInstanceValid(_tio);

    public void OpenTio()
    {
        if (IsInstanceValid(_tio)) return;
        if (Net.IsHost && Mission == MissionState.Idle)
        {   // docking at the TIO selects the newest unlocked tier
            Missions.Level = Missions.Unlocked(Missions.Current.Id);
            BroadcastMission();
        }
        if (IsInstanceValid(_base)) ToggleBase();
        if (IsInstanceValid(_pilot)) TogglePilot();
        _tio = new TioWindow { Hub = this };
        _hudLayer.AddChild(_tio);
    }

    public void SetMyReady(bool ready)
    {
        // READY also flies you to where the mission portal opens (a manual key cancels it)
        if (_ships.TryGetValue(Net.LocalId, out var me) && IsInstanceValid(me)) me.AutopilotTo = ready ? MissionPortalPos : null;
        if (Net.IsHost) { _ready[Net.LocalId] = ready; BroadcastMission(); }
        else Net.AskHost(this, nameof(RequestReady), ready);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestReady(bool ready)
    {
        if (!Net.FromPlayer(this, out int who) || !_ships.ContainsKey(who)) return;   // only a pilot in the session
        _ready[who] = ready; BroadcastMission();
    }

    private const float PortalEnterRadius = 190f;

    // ── moving the party between sectors (host decides; every peer follows) ──
    public void EnterSector(SectorKind k)
    {
        if (!Net.IsHost) return;
        if (k == SectorKind.Arena) Yard?.SaveForTrip();
        if (Net.IsOnline) Rpc(nameof(NetSector), (int)k);
        GoTo(k);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetSector(int k) => GoTo((SectorKind)k);
    private void GoTo(SectorKind k)
    {
        Sector = k;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Hub.tscn");
    }

    // ── the arena: the boss, and how it ends ────────────────────────────────
    private void BuildArena()
    {
        Boss = new Boss { Hub = this, Position = BasePos + new Vector2(0, -700f), Rotation = Mathf.Pi };
        AddChild(Boss);
    }

    // host: the boss is dead -- shared EXP for the kill and the mission, credits home
    public void BossDefeated()
    {
        if (!Net.IsHost || MissionWon) return;
        MissionWon = true;
        // every pilot gets its own EXP (its level, its first clears) and its share of the bounty;
        // the host's own record (and so the next level unlocking) updates in its own award
        AnnounceBossKill(Missions.Level);
        _arenaEndT = 4.0;
        if (Net.IsOnline) Rpc(nameof(NetWon));
    }
    // Guests see the boss at zero too: its last hull report went out before the killing blow, so
    // for the four seconds before home it stood there at a sliver, still selectable.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetWon()
    {
        MissionWon = true;
        if (IsInstanceValid(Boss)) Boss.Hp = 0;
    }

    private void TickArena(double delta)
    {
        // `Yard` here is the TYPE, not this Hub's Yard property -- which is null in the arena,
        // because the arena skips BuildWorld. C# binds the name to the class when the member is
        // static, so every `Yard.<static>` call in this file is safe (see also AddGuestShare and
        // AddHostShare, both reached from the arena). Make any of them an instance member and
        // these lines start throwing. See DESIGN.md -> Traps.
        Yard.TripClock += delta;                              // what the base is missing, in game time
        if (!Net.IsHost) return;
        // the whole party in stasis at once: the mission fails, everyone goes home
        if (!MissionWon && _arenaEndT < 0 && _ships.Count > 0 && _ships.Values.All(s => IsInstanceValid(s) && !s.Alive)) _arenaEndT = 3.0;
        if (_arenaEndT >= 0 && (_arenaEndT -= delta) <= 0)
        {
            _arenaEndT = -1;
            if (!MissionWon) PendingRaidLevel = Missions.Level;        // failed: the boss sends its raiders after you
            EnterSector(SectorKind.Home);
        }
    }

    // host: a missile was shot down; every guest bursts its copy
    public void MissileDown(int id) { if (Net.IsHost && Net.IsOnline) Rpc(nameof(NetMissileDown), id); }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetMissileDown(int id)
    {
        foreach (var t in GetChildren().OfType<Torpedo>()) if (t.NetId == id) t.Intercept();
    }

    // host: pick a level between 1 and the newest unlocked
    public void SelectLevel(int level)
    {
        if (!Net.IsHost || Mission != MissionState.Idle) return;
        Missions.Level = System.Math.Clamp(level, 1, Missions.Unlocked(Missions.Current.Id));
        BroadcastMission();
    }

    public void StartMission()
    {
        if (!Net.IsHost || !AllReady || Mission != MissionState.Idle) return;
        Mission = MissionState.Opening; MissionT = 0;
        BroadcastMission();
    }

    private void TickMission(double delta)
    {
        if (InArena) { TickArena(delta); return; }
        // everyone READY opens the portal; everyone AT the portal goes through
        if (Net.IsHost && Mission == MissionState.Idle && AllReady) StartMission();
        if (Net.IsHost && Mission == MissionState.PortalOpen && _ships.Count > 0
            && _ships.Values.All(s => IsInstanceValid(s) && s.Position.DistanceTo(MissionPortalPos) <= PortalEnterRadius))
        { EnterSector(SectorKind.Arena); return; }
        if (Mission == MissionState.Opening)
        {   // every peer animates the bar; only the host decides the portal is open
            MissionT += delta;
            if (Net.IsHost && MissionT >= PortalOpenTime) { Mission = MissionState.PortalOpen; MissionT = 0; BroadcastMission(); }
        }
        SyncMissionPortal();
    }

    private void SyncMissionPortal()
    {
        bool want = Mission == MissionState.PortalOpen;
        if (want && !IsInstanceValid(_missionPortal))
        {
            _missionPortal = new Portal { Name = "MissionPortal", Position = MissionPortalPos,
                                          Tint = new Color(1f, 0.35f, 0.3f), Core = new Color(0.30f, 0.05f, 0.05f) };
            AddChild(_missionPortal); _missionPortal.Flash();
        }
        else if (!want && IsInstanceValid(_missionPortal)) { _missionPortal.QueueFree(); _missionPortal = null; }
    }

    private void BroadcastMission()
    {
        if (Net.IsOnline) Rpc(nameof(NetMission), MissionArgs());
    }
    private Variant[] MissionArgs()
    {
        var ids = _ready.Keys.ToArray(); var rs = ids.Select(i => _ready[i] ? 1 : 0).ToArray();   // RPCs carry int[], not bool[]
        return new Variant[] { (int)Mission, MissionT, ids, rs, Missions.Level };
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetMission(int state, double t, int[] ids, int[] ready, int level)
    {
        Missions.Level = level;
        Mission = (MissionState)state; MissionT = t;
        _ready.Clear(); for (int i = 0; i < ids.Length && i < ready.Length; i++) _ready[ids[i]] = ready[i] != 0;
    }

    // ── raids: a failed mission brings the boss's raiders to your base ─────────
    // Level L (the failed boss's): raiders at S(L) = 1.1^(L-1), the boss's own scaling.
    // 2 patrols, and 1 more per extra pilot in the session, in from the map's edge 3 s
    // after the party is home (so every guest's world is loaded before they appear).
    private static int PendingRaidLevel;                        // survives the scene change home
    public const float RaidEdge = 3200f;
    public const double RaidDelay = 3.0;
    private double _raidIn = -1; private int _raidLevel;
    private static double RaidScale(int level) => Missions.S(level);           // S(L) = 1.1^(L-1)

    private void StartRaid(int level)
    {
        int patrols = 2 + System.Math.Max(0, _ships.Count - 1);
        for (int i = 0; i < patrols; i++)
        {
            float a = Mathf.Tau * i / patrols + 0.4f;
            SpawnPatrol(BasePos + Vector2.Right.Rotated(a) * RaidEdge, RaidScale(level));
        }
    }

    private void TickRaid(double delta)
    {
        if (_raidIn < 0) return;
        if ((_raidIn -= delta) <= 0) { _raidIn = -1; StartRaid(_raidLevel); }
    }

    // ── raiders (enemy fighters): host-simulated, replicated ──────────────────
    public readonly List<Raider> Raiders = new();
    private int _raiderIds = 5000;
    private double _raiderSend;

    // what a raider may go after: player ships, and the utility ships at home
    public IEnumerable<Node2D> RaiderTargets()
    {
        foreach (var s in _ships.Values) if (IsInstanceValid(s) && s.Alive) yield return s;
        if (Yard == null) yield break;
        foreach (var g in Yard.Gatherers) if (g.State != Gatherer.St.Destroyed) yield return g;
        if (Yard.Hauler != null && Yard.Hauler.State is not (Hauler.St.Destroyed or Hauler.St.Away)) yield return Yard.Hauler;
    }

    // A patrol: 3 lights and 1 heavy, spawned together at `at` on the perimeter.
    private int _patrols;
    public int SpawnPatrol(Vector2 at, double scale = 1)
    {
        if (!Net.IsHost) return 0;
        int id = ++_patrols;
        SpawnRaider(at + new Vector2(-40, 0), RaiderKind.Light, id, scale); SpawnRaider(at, RaiderKind.Light, id, scale);
        SpawnRaider(at + new Vector2(40, 0), RaiderKind.Light, id, scale); SpawnRaider(at + new Vector2(0, 90), RaiderKind.Heavy, id, scale);
        return id;
    }

    public Raider SpawnRaider(Vector2 at, RaiderKind kind = RaiderKind.Light, int patrol = 0, double scale = 1)
    {
        if (!Net.IsHost) return null;
        var r = AddRaider(++_raiderIds, at, kind, patrol, scale);
        if (Net.IsOnline) Rpc(nameof(NetRaiderSpawn), r.NetId, at, (int)kind, scale);
        return r;
    }
    // Idempotent: a late joiner is sent every raider that exists, and one of them may already
    // have reached it the ordinary way.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRaiderSpawn(int id, Vector2 at, int kind, double scale)
    {
        if (Raiders.All(x => x.NetId != id)) AddRaider(id, at, (RaiderKind)kind, 0, scale);
    }
    private Raider AddRaider(int id, Vector2 at, RaiderKind kind, int patrol, double scale)
    {
        var r = new Raider { Hub = this, Kind = kind, Patrol = patrol, Strength = scale, NetId = id, Position = at, Name = $"Raider_{id}" };
        Raiders.Add(r); AddChild(r);
        return r;
    }

    public void RaiderDown(Raider r)
    {
        DropRaider(r, burst: true);
        if (Net.IsHost && Net.IsOnline) Rpc(nameof(NetRaiderGone), r.NetId);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRaiderGone(int id)
    {
        if (Raiders.FirstOrDefault(x => x.NetId == id) is { } r) DropRaider(r, burst: true);
    }
    // Also let go of it as the selection: on a guest a raider's hull never reads zero (the host
    // removes it before its last hull reaches anyone), so a selected one stayed "alive" and freed.
    private void DropRaider(Raider r, bool burst)
    {
        if (burst) AddChild(new Explosion { Position = r.Position, Radius = 28f });
        if (ReferenceEquals(_selected, r)) _selected = null;
        Raiders.Remove(r); r.QueueFree();
    }

    // A heavy's missile: flies 7 s to a marked point, and the host lands the blast there.
    private readonly List<(Vector2 at, double left, int from, double damage)> _blasts = new();
    public int BlastsPending => _blasts.Count;
    public void HeavyMissile(Vector2 from, Vector2 at, int raiderId, double damage)
    {
        if (!Net.IsHost) return;
        _blasts.Add((at, Raider.MissileFlight, raiderId, damage));
        ShowHeavyMissile(from, at);
        if (Net.IsOnline) Rpc(nameof(NetHeavyMissile), from, at);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetHeavyMissile(Vector2 from, Vector2 at) => ShowHeavyMissile(from, at, Net.Arriving(Raider.MissileFlight));
    private void ShowHeavyMissile(Vector2 from, Vector2 at, double flight = Raider.MissileFlight)
    {
        AddChild(new Telegraph { Line = false, A = at, Radius = Raider.BlastRadius, Duration = flight });
        AddChild(new HeavyMissileVisual { From = from, To = at, Flight = flight });
    }
    private void TickBlasts(double delta)
    {
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var b = _blasts[i]; b.left -= delta;
            if (b.left > 0) { _blasts[i] = b; continue; }
            _blasts.RemoveAt(i);
            foreach (var t in RaiderTargets().ToList())
                if (Raider.Gap(b.at, t) <= Raider.BlastRadius)
                    switch (t)
                    {
                        case PlayerShip p: p.Hit(b.damage, b.at, $"heavy:{b.from}:missile"); break;
                        case Gatherer g: g.TakeDamage(b.damage); break;
                        case Hauler h: h.TakeDamage(b.damage); break;
                    }
        }
    }

    // In packets of at most 24 raiders. A full failed-mission raid is 40, and in one packet that
    // was 1.5 KB -- past the internet's usual 1.2 KB, so ENet split it, and an unreliable packet
    // with one piece lost is lost whole: every raider froze for that update. Each packet names its
    // raiders, so each stands alone.
    private const int RaidersPerPacket = 24;
    private void SendRaiders(double delta)
    {
        if (!Net.IsHost || !Net.IsOnline || Raiders.Count == 0) return;
        _raiderSend -= delta; if (_raiderSend > 0) return; _raiderSend = 0.1;
        foreach (var part in Raiders.Chunk(RaidersPerPacket))
            Rpc(nameof(NetRaiders), part.Select(r => r.NetId).ToArray(), part.Select(r => r.Position).ToArray(),
                part.Select(r => r.Rotation).ToArray(), part.Select(r => (float)r.Hp).ToArray(),
                part.Select(r => r.TetherTo ?? new Vector2(float.NaN, float.NaN)).ToArray(),
                part.Select(r => r.NetFlags).ToArray());
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetRaiders(int[] ids, Vector2[] pos, float[] rot, float[] hp, Vector2[] tether, int[] flags)
    {
        // Shortest array wins. NetHostState already guards its wing arrays this way; this one
        // indexed five arrays off the length of the first, so one short packet was an exception
        // on a guest rather than a frame of stale raiders.
        int n = Mathf.Min(ids.Length, Mathf.Min(pos.Length, Mathf.Min(rot.Length, Mathf.Min(hp.Length, Mathf.Min(tether.Length, flags.Length)))));
        for (int i = 0; i < n; i++)
        {
            var r = Raiders.FirstOrDefault(x => x.NetId == ids[i]);
            r?.SetNet(pos[i], rot[i], hp[i], float.IsNaN(tether[i].X) ? null : tether[i], flags[i]);
        }
    }

    // ── a boss kill, as each guest receives it: its own EXP, its own share of the bounty ──
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetBossKill(int level, int party)
    {
        Yard.AddGuestShare(Missions.BountyEach(level, party));              // to its own base, set aside while it visits
        Progression.AwardBossKill(level);                                   // (which saves: the share with it)
    }
    public int PartySize => _ships.Count;

    // host: a level-L boss is down -- every pilot's own EXP and its share of the bounty
    public void AnnounceBossKill(int level)
    {
        if (!Net.IsHost) return;
        int party = System.Math.Max(1, PartySize);
        Yard.AddHostShare(Missions.BountyEach(level, party));             // the host's share, paid at home
        Progression.AwardBossKill(level);                                 // (which saves: the share with it)
        if (Net.IsOnline) Rpc(nameof(NetBossKill), level, party);
    }

    // ── character creator ────────────────────────────────────────────────────
    // One instance, owned here; REFIT opens it and it cannot stack.
    private void OpenCreator()
    {
        if (IsInstanceValid(_creator)) return;
        _creator = new CharacterCreator();
        _creator.Changed += ApplyLocalIdentity;
        _creator.Done += _ => { PilotChanged(); _creator = null; };
        AddChild(_creator);
    }

    private void MoveCamera(PlayerShip me, float dt)
    {
        _cam.Zoom = _cam.Zoom.Lerp(new Vector2(ZoomLevel, ZoomLevel), Mathf.Clamp(10f * dt, 0f, 1f));
        var anchor = me.ViewPosition;                                  // the pod, while in stasis
        if (!FreeCamera)
        {
            // follow smoothly -- but cut straight to the ship after a warp: panning 2000 u is disorienting
            if (_cam.Position.DistanceTo(anchor) > 1200f) _cam.Position = anchor;
            else _cam.Position = _cam.Position.Lerp(anchor, Mathf.Clamp(6f * dt, 0f, 1f));
            return;
        }
        var pan = Vector2.Zero;
        if (!ControlsLocked)
        {
            if (Input.IsKeyPressed(Key.Left))  pan.X -= 1;
            if (Input.IsKeyPressed(Key.Right)) pan.X += 1;
            if (Input.IsKeyPressed(Key.Up))    pan.Y -= 1;
            if (Input.IsKeyPressed(Key.Down))  pan.Y += 1;
            var m = GetViewport().GetMousePosition(); var vs = GetViewport().GetVisibleRect().Size;
            if (m.X <= EdgeBand) pan.X -= 1; else if (m.X >= vs.X - EdgeBand) pan.X += 1;
            if (m.Y <= EdgeBand) pan.Y -= 1; else if (m.Y >= vs.Y - EdgeBand) pan.Y += 1;
        }
        if (pan != Vector2.Zero) _cam.Position += pan.Normalized() * PanSpeed / ZoomLevel * dt;
        var off = _cam.Position - anchor;                               // the tether
        if (off.Length() > me.MyArt.CameraRange) _cam.Position = anchor + off.Normalized() * me.MyArt.CameraRange;
    }

    public void SetZoom(float z) => ZoomLevel = Mathf.Clamp(z, DefaultZoom / ZoomOutMax, DefaultZoom * ZoomInMax);
    public void ToggleFreeCamera() => FreeCamera = !FreeCamera;

    public PlayerShip MyShipPublic => MyShip;
    private void SpawnTorpedo(Vector2 from, Vector2 dir, float speed, float range, double dmg, bool cosmetic,
                              int target = 0, float turn = 0f, bool heavy = false, bool hostile = false, PlayerShip source = null, int id = 0,
                              string hitSource = null, float size = 1f)
    {
        AddChild(new Torpedo { Position = from, Dir = dir, Speed = speed, Range = range, Damage = dmg, Cosmetic = cosmetic,
                               TargetId = target, TurnRate = turn, Heavy = heavy, HostileFire = hostile, Source = source, NetId = id,
                               HitSource = hitSource, Size = size });
    }

    private PlayerShip MyShip => _ships.TryGetValue(Net.LocalId, out var s) && IsInstanceValid(s) ? s : null;

    private void ToggleStats()
    {
        if (IsInstanceValid(_statsWin)) { _statsWin.QueueFree(); _statsWin = null; return; }
        if (MyShip == null) return;
        _statsWin = new StatsWindow { Ship = MyShip };
        AddChild(_statsWin);
    }

    // Tab: ALWAYS the live hostile nearest your ship, at any range. No cycling --
    // pressing it again re-picks the nearest, so it never lands on a far target.
    // Switch to anything else with a left-click.
    private void SelectNearest()
    {
        var me = MyShip;
        if (me == null) return;
        var all = Combat.Near(me.Position, float.MaxValue);
        _selected = all.Count > 0 ? all[0] : null;
    }

    // Left-click in the world: the hostile under the cursor (its hit circle, plus a
    // little slack), nearest the click if circles overlap. Clicking empty space keeps
    // the current target, so a stray click never drops it; Esc clears it.
    // true if the click landed on something (a hostile to select, or a building)
    private bool SelectAt(Vector2 world)
    {
        IHittable best = null; float bd = float.MaxValue;
        foreach (var h in Combat.Hostiles)
        {
            if (h == null || !h.Alive) continue;
            float d = world.DistanceTo(h.Position);
            if (d <= h.HitRadius + 16f && d < bd) { bd = d; best = h; }
        }
        if (best != null) { SelectTarget(best); return true; }
        // buildings: a left-click on one opens its menu
        if (IsInstanceValid(_tioSprite) && _tioSprite.GetRect().HasPoint(_tioSprite.ToLocal(world))) { OpenTio(); return true; }
        if (!InArena && world.DistanceTo(BasePos) < 200f) { if (!IsInstanceValid(_base)) ToggleBase(); return true; }
        return false;
    }

    public void BeginPlacement(string label, System.Action<Vector2> confirm)
    {
        _placing = confirm; _placingLabel = label;
    }

    private void CancelPlacement() { _placing = null; _placingLabel = null; }

    // ── tick ─────────────────────────────────────────────────────────────────
    public override void _Process(double delta)
    {
        SendRaiders(delta);
        if (Net.IsHost) TickRaid(delta);
        if (Net.IsHost) TickBlasts(delta);
        TickMission(delta);
        if (Music.I != null)
        {
            var own = MyShip;
            Music.I.Target = own != null && own.InCombat ? Music.Mood.Combat
                           : Selected != null ? Music.Mood.Alert : Music.Mood.Ambient;
        }
        ControlsLocked = IsInstanceValid(_creator) || IsInstanceValid(_esc) || GetViewport().GuiGetFocusOwner() is LineEdit
                         || (IsInstanceValid(_statsWin) && _statsWin.Capturing);
        // bars and labels keep a constant on-screen size whatever the zoom
        HealthBar.UiScale = Txt.UiScale = 1f / _cam.Zoom.X;
        if (_selected != null && !_selected.Alive) _selected = null;

        // ONLY the host produces. A client that ticked its own copy would drift
        // from the host's within seconds and then argue about it.
        if (_ships.TryGetValue(Net.LocalId, out var mine) && IsInstanceValid(mine))
            MoveCamera(mine, (float)delta);

        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.t -= delta;
            if (f.t <= 0) _flashes.RemoveAt(i); else _flashes[i] = f;
        }
        QueueRedraw();

        string ship = "";
        var me = MyShip;
        if (me != null)
        {
            // weapon and ability state lives on the ability bar; this line is who and where.
            // Built every frame so a class change or a rebind shows at once, but only ASSIGNED
            // when it actually differs: setting Text re-shapes the label's glyphs, and this
            // string changes on a refit or a rebind, not sixty times a second.
            var hint = Abilities.ControlsHint(me.Class);
            if (_help.Text != hint) _help.Text = hint;
            ship = $"    |    {Character.Name}  {Classes.NameOf(me.Class)}"
                 + $"  {Mathf.Abs(me.SpeedAhead):0} u/s{(me.SpeedAhead < -1 ? " astern" : "")}";
            ship += Selected != null ? "    target: " + (Selected is TargetDummy td ? $"TARGET DUMMY {td.Number}" : $"#{Selected.NetId}")
                  : Waypoint != null ? $"    waypoint: {WaypointName}"
                                     : "    no target (Tab / click)";
            if (Raiders.Count > 0) ship += $"    |    RAIDERS {Raiders.Count}";
            if (Placing) ship += $"    PLACING {_placingLabel}: left-click to confirm, right-click / Esc to cancel";
        }
        string place = Yard == null
            ? $"ARENA  ·  {Missions.BossName} (LEVEL {Missions.Level})  {(IsInstanceValid(Boss) ? Boss.Hp : 0):0} / {(IsInstanceValid(Boss) ? Boss.MaxHp : 0):0}" + (MissionWon ? "  ·  DEFEATED" : "")
            : $"ORE {Yard.Ore:0}    SALVAGE {Yard.Salvage:0}    CREDITS {Yard.Credits:0}"
              + $"    HAULER {Yard.Hauler.Cargo:0}/{Yard.Capacity:0} {Yard.Hauler.State.ToString().ToUpperInvariant()}";
        _hud.Text = place + ship
                  + (Net.IsOnline ? (Net.IsHost ? $"        HOSTING ({_ships.Count})" : $"        GUEST ({_ships.Count})") : "        OFFLINE")
                  + (FreeCamera ? "        FREE CAMERA (Y)" : "");
    }

    public override void _Draw()
    {

        // Hull bars on every ship, and other players' names. Drawn here rather than on
        // the ship so they stay upright while it turns.
        var font = ThemeDB.FallbackFont;
        foreach (var s in _ships.Values)
        {
            if (!IsInstanceValid(s) || !s.IsVisibleInTree()) continue;   // hidden ships show nothing
            // Your own hull shows on the HUD bar; over your ship it is only clutter. Other
            // players' ships keep their bars, so you can see how they are doing.
            if (s.Mine) continue;
            DrawSetTransform(s.Position, 0f, Vector2.One);
            // above the hull whatever the class: half its length plus a margin, unscaled
            // by zoom (HealthBar scales positions by UiScale, so divide it back out)
            float above = (s.MyArt.Length * 0.5f + 16f) / Txt.UiScale;
            HealthBar.Draw(this, new Vector2(-40, -above), 80, 6, s.Hp, s.MaxHp,
                           s.Hp / Mathf.Max(1, s.MaxHp) > 0.35 ? new Color(0.4f, 0.9f, 0.5f) : new Color(1f, 0.4f, 0.3f), null);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            Txt.Centre(this, font, s.Position + new Vector2(0, -(s.MyArt.Length * 0.5f + 28f)), s.Pilot, Txt.Size(16), new Color(0.75f, 0.9f, 1f, 0.85f));
        }

        // a pending placement shows where the click will land
        if (Placing)
        {
            var m = GetGlobalMousePosition();
            DrawArc(m, 60f, 0, Mathf.Tau, 40, new Color(0.5f, 1f, 0.7f, 0.8f), 2f);
            DrawLine(m - new Vector2(12, 0), m + new Vector2(12, 0), new Color(0.5f, 1f, 0.7f), 2f);
            DrawLine(m - new Vector2(0, 12), m + new Vector2(0, 12), new Color(0.5f, 1f, 0.7f), 2f);
        }

        // selection brackets on the chosen target
        var t = Selected;
        if (t != null)
        {
            float r = t.HitRadius + 12f, L = 14f;
            var c = new Color(1f, 0.85f, 0.3f);
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                var corner = t.Position + new Vector2(sx * r, sy * r);
                DrawLine(corner, corner - new Vector2(sx * L, 0), c, 2.5f);
                DrawLine(corner, corner - new Vector2(0, sy * L), c, 2.5f);
            }
        }
    }

    // _Input, not _UnhandledInput: these must run before the GUI swallows the event.
    public override void _Input(InputEvent e)
    {
        var focus = GetViewport().GuiGetFocusOwner();

        // Clicking anywhere outside a focused text box lets go of it. Godot keeps
        // LineEdit focus on a click that lands on nothing focusable -- the world, a
        // panel background -- so the address box stayed live and ate the keyboard.
        if (e is InputEventMouseButton mb && mb.Pressed && focus is LineEdit le
            && !le.GetGlobalRect().HasPoint(mb.Position))
            le.ReleaseFocus();

        if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            // Esc peels back one layer: text box, placement, creator, stats window,
            // target, then the menu. Handled FIRST: ChangeSceneToFile pulls this node
            // out of the tree at once, after which GetViewport() is null.
            if (IsInstanceValid(_statsWin) && _statsWin.Capturing) return;   // the window takes this Esc
            GetViewport().SetInputAsHandled();
            if (focus is LineEdit le2) le2.ReleaseFocus();
            else if (IsInstanceValid(_esc)) ToggleEscMenu();
            else if (Placing) CancelPlacement();
            else if (IsInstanceValid(_creator)) _creator.Close();
            else if (IsInstanceValid(_statsWin)) ToggleStats();
            else if (IsInstanceValid(_base)) ToggleBase();
            else if (IsInstanceValid(_pilot)) TogglePilot();
            else if (IsInstanceValid(_tio)) { _tio.QueueFree(); _tio = null; }
            else if (IsInstanceValid(_equip)) ToggleEquipment();
            else if (Selected != null || Waypoint != null) ClearSelection();
            else ToggleEscMenu();                                   // the menu holds "quit to main menu"
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (IsInstanceValid(_creator)) return;          // the panel owns the screen
        var mine = MyShip;
        if (mine == null) return;

        // LEFT-CLICK is reserved for two things only: confirming a pending placement,
        // otherwise selecting what is under the cursor. It never fires a weapon or
        // orders a wing -- attacks are Space. RIGHT-CLICK only cancels a placement.
        // Only clicks the GUI did not take arrive here, so UI clicks never select.
        if (e is InputEventMouseButton wheel && wheel.Pressed
            && (wheel.ButtonIndex == MouseButton.WheelUp || wheel.ButtonIndex == MouseButton.WheelDown))
        {   // the wheel zooms: up is closer
            SetZoom(ZoomLevel * (wheel.ButtonIndex == MouseButton.WheelUp ? 1.1f : 1f / 1.1f));
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            var at = GetGlobalMousePosition();
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (Placing) { var confirm = _placing; CancelPlacement(); confirm(at); }
                // a double-click on empty space clears the target
                else if (!SelectAt(at) && mb.DoubleClick) ClearSelection();
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Right && Placing)
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (e is InputEventKey kk && kk.Pressed && !kk.Echo)
        {
            // fixed controls first; every other key is looked up in this class's
            // ability bindings (remappable in the K window). Class is changed only in
            // REFIT in the base menu -- there are no class hotkeys.
            if (kk.Keycode == Key.Y) ToggleFreeCamera();
            else if (kk.Keycode == Key.Tab) SelectNearest();
            else if (kk.Keycode == Key.K) ToggleStats();
            else if (kk.Keycode == Key.B && !InArena) ToggleBase();
            else if (kk.Keycode == Key.L) TogglePilot();
            else if (kk.Keycode == Key.I) ToggleEquipment();
            else if (kk.Keycode == Key.V) MyShipPublic?.StartWarp();   // warp: a fixed key, not a slot
            else
            {
                // in stasis the only order is F: re-board once the ship is ready
                if (!mine.Alive) { if (kk.Keycode == Key.F) mine.UseAbility("reboard", 0); GetViewport().SetInputAsHandled(); return; }
                var ab = Abilities.ByKey(mine.Class, kk.Keycode);
                if (ab == null) return;
                if (ab.Kind == AbilityKind.Press && !ab.Open) mine.UseAbility(ab.Id, Selected?.NetId ?? 0);
                // Hold abilities (the main guns) are read by polling in PlayerShip.
            }
            GetViewport().SetInputAsHandled();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetShell(Vector2 from, Vector2 dir, float speed, float range)
    {
        AddChild(new Shell { Position = from, Dir = dir, Speed = speed, Range = range, Cosmetic = true });
        Sfx.Cannon(from);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetTorpedo(Vector2 from, Vector2 dir, float speed, float range, int target, float turn, bool heavy, bool hostile, int id, float size)
        => SpawnTorpedo(from, dir, speed, range, 0, true, target, turn, heavy, hostile, null, id, null, size);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetFlash(Vector2 a, Vector2 b, Color c, int snd)
        => AddFlash(a, b, c, System.Enum.IsDefined(typeof(ShotSound), snd) ? (ShotSound)snd : ShotSound.Light);
    private void AddFlash(Vector2 a, Vector2 b, Color c, ShotSound snd) { _flashes.Add((a, b, c, 0.10)); Sfx.Laser(a, b, snd); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetDummy(int number, double last, double avg, double total)
    {
        // BY NUMBER, not by position. The dummies are numbered 1, 3, 4, 5 (2's spot holds the two
        // practice fighters), so `_dummies[number - 1]` sent 3's readout to 4, 4's to 5, and
        // dropped 5's entirely because 5 > Count. Guests saw the wrong numbers on the wrong hulls.
        foreach (var d in _dummies) if (d.Number == number) { d.SetReadout(last, avg, total); return; }
    }

    private void ToggleBase()
    {
        if (Yard == null) return;                                      // the arena: there is no base to open
        if (IsInstanceValid(_base)) { _base.QueueFree(); _base = null; return; }
        if (IsInstanceValid(_equip)) ToggleEquipment();             // they share a spot
        _base = new BasePanel { Hub = this };
        if (IsInstanceValid(_pilot)) TogglePilot();                // the two share a spot
        _hudLayer.AddChild(_base);
    }
    public bool CreatorOpen => IsInstanceValid(_creator);
    public bool StatsOpen => IsInstanceValid(_statsWin);

    public void ToggleEquipment()
    {
        if (IsInstanceValid(_equip)) { _equip.QueueFree(); _equip = null; return; }
        if (IsInstanceValid(_base)) ToggleBase();                 // they share a spot
        if (IsInstanceValid(_pilot)) TogglePilot();
        if (IsInstanceValid(_tio)) { _tio.QueueFree(); _tio = null; }
        _equip = new EquipmentWindow { Hub = this };
        _hudLayer.AddChild(_equip);
    }

    public void TogglePilot()
    {
        if (IsInstanceValid(_equip)) ToggleEquipment();
        if (IsInstanceValid(_pilot)) { _pilot.QueueFree(); _pilot = null; return; }
        if (IsInstanceValid(_base)) ToggleBase();                 // the two share a spot
        _pilot = new PilotWindow { Hub = this };
        _hudLayer.AddChild(_pilot);
    }

    // A purchase: refit the ship now, and tell the host (it resolves hull and damage).
    public void PilotChanged() { ApplyLocalIdentity(); SendIdentity(); }

    // REFIT: the only way into the ship menu (it costs 10%; see Yard.ResetCost).
    public void ResetShip()
    {
        if (IsInstanceValid(_creator) || Yard == null) return;       // REFIT is at the base
        Yard.ChargeReset();
        if (IsInstanceValid(_base)) ToggleBase();
        OpenCreator();
    }
}

// A small warm star for the belt to sit around. Drawn rather than spritework so
// it can pulse without another asset.
public partial class Sun : Node2D
{
    private double _t;
    public override void _Process(double delta) { _t += delta; QueueRedraw(); }
    public override void _Draw()
    {
        float p = 1f + 0.03f * Mathf.Sin((float)_t * 1.3f);
        DrawCircle(Vector2.Zero, 210f * p, new Color(1f, 0.82f, 0.42f, 0.10f));
        DrawCircle(Vector2.Zero, 150f * p, new Color(1f, 0.78f, 0.33f, 0.22f));
        DrawCircle(Vector2.Zero, 104f * p, new Color(1f, 0.86f, 0.48f));
        DrawCircle(Vector2.Zero,  78f * p, new Color(1f, 0.96f, 0.80f));
    }
}

// The outbound wormhole: trade runs leave through it, and it is the door to
// every hostile system.
public partial class Portal : Node2D
{
    private double _t, _flash;
    public Color Tint = new(0.45f, 0.75f, 1f), Core = new(0.10f, 0.16f, 0.34f);   // the mission portal is red
    public void Flash() => _flash = 0.8;        // a hauler jumping through, either way
    public override void _Process(double delta) { _t += delta; _flash = System.Math.Max(0, _flash - delta); QueueRedraw(); }
    public override void _Draw()
    {
        float s = (float)_t;
        if (_flash > 0)
        {
            float k = (float)(_flash / 0.8);
            DrawCircle(Vector2.Zero, 60f + 110f * (1f - k), new Color(0.6f, 0.85f, 1f, 0.55f * k));
        }
        for (int i = 0; i < 4; i++)
        {
            float r = 70f + i * 26f + 6f * Mathf.Sin(s * 1.7f + i);
            DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 40,
                    new Color(Tint.R, Tint.G, Tint.B, 0.55f - 0.10f * i), 3f);
        }
        DrawCircle(Vector2.Zero, 56f, new Color(Core.R, Core.G, Core.B, 0.85f));
    }
}

// The player's hull, big and always on screen: a bar along the bottom centre.
public partial class HullHud : Control
{
    private readonly StyleBox _panel = Ui.PanelStyle();   // built once: _Draw runs every frame
    private readonly StyleBox _track = Ui.Box(Ui.Deep, Ui.Line, 5);
    public Hub Hub;
    private const float W = 440, H = 22;

    public override void _Ready()
    {
        AnchorLeft = AnchorRight = 0.5f; AnchorTop = AnchorBottom = 1f;
        OffsetLeft = -W / 2; OffsetRight = W / 2; OffsetTop = -150; OffsetBottom = -150 + H;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var s = Hub?.MyShipPublic;
        if (s == null) return;
        _panel.Draw(GetCanvasItem(), new Rect2(-8, -6, W + 16, H + 12));   // its panel
        float frac = (float)Mathf.Clamp(s.Hp / Mathf.Max(1, s.MaxHp), 0, 1);
        // Your own hull keeps its green-to-red reading -- it is the one number you glance at while
        // being shot, and a palette accent would say nothing about how close you are to dying. The
        // TRACK comes from the palette so the bar still belongs to the rest of the HUD.
        var fill = frac > 0.35f ? Ui.Good : Ui.Bad;
        _track.Draw(GetCanvasItem(), new Rect2(0, 0, W, H));
        DrawRect(new Rect2(1, 1, (W - 2) * frac, H - 2), fill);
        Txt.D(this, ThemeDB.FallbackFont, new Vector2(0, H - 5), s.Alive ? $"HULL  {s.Hp:0} / {s.MaxHp:0}"
                  : s.CanReboard ? "SHIP READY  —  press F to re-board"
                  : $"SHIP IN STASIS  {(int)s.StasisLeft / 60}:{(int)s.StasisLeft % 60:00}  —  flying the escape pod",
              HorizontalAlignment.Center, W, 15, frac > 0.35f ? Ui.Deep : Colors.White);
        // The warp readout sits ON the hull bar, so its colour has to depend on what is UNDER it.
        // Drawn in the palette's green it vanished completely on a full hull -- green on green --
        // and the same would happen to a red-tinted state on a nearly-empty one. Above the fill it
        // takes the state colour; on the fill it goes dark, which reads on green and on red alike.
        // The words carry the state either way. The UI lint cannot catch this: it measures
        // position and width, not contrast.
        if (s.Alive)
        {
            bool onFill = frac > (W - 150f) / W;
            var warp = onFill ? Ui.Deep
                     : s.Warping ? Ui.Accent : s.WarpCooldownLeft > 0 ? Ui.Dim : Ui.Good;
            Txt.D(this, ThemeDB.FallbackFont, new Vector2(W - 150, H - 5),
                  s.Warping ? $"WARPING  {s.WarpWarmupLeft:0.0} s" : s.WarpCooldownLeft > 0 ? $"WARP  {s.WarpCooldownLeft:0} s" : "WARP  READY",
                  HorizontalAlignment.Right, 144, 12, warp);
        }
    }
}

// Draws the hub's hit flashes (see Hub._Ready for why it is a layer of its own).
public partial class FlashLayer : Node2D
{
    public Hub Hub;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        foreach (var f in Hub.Flashes)
            DrawLine(f.a, f.b, new Color(f.c.R, f.c.G, f.c.B, (float)(f.t / 0.10) * 0.9f), 2f);
    }
}

// Text that lives in the world (a building's name): drawn, not a UI control.
public partial class WorldLabel : Node2D
{
    public string Text = "";
    public override void _Draw() => Txt.Centre(this, ThemeDB.FallbackFont, Vector2.Zero, Text, Txt.Size(14), new Color(0.8f, 0.85f, 0.95f, 0.8f));
}

// The mission portal's opening bar, at the TIO's top right: world-drawn, on every peer.
public partial class MissionBar : Node2D
{
    public Hub Hub;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Hub.Mission != Hub.MissionState.Opening) return;
        var at = Hub.MissionPortalPos; const float W = 180f, H = 12f;
        float k = Mathf.Clamp((float)(Hub.MissionT / Hub.PortalOpenTime), 0f, 1f);
        DrawRect(new Rect2(at - new Vector2(W / 2, H / 2), new Vector2(W, H)), new Color(0.05f, 0.05f, 0.08f, 0.9f));
        DrawRect(new Rect2(at - new Vector2(W / 2, H / 2), new Vector2(W * k, H)), new Color(1f, 0.4f, 0.3f, 0.95f));
        DrawRect(new Rect2(at - new Vector2(W / 2, H / 2), new Vector2(W, H)), new Color(1f, 0.6f, 0.5f), false, 1.5f);
        Txt.Centre(this, ThemeDB.FallbackFont, at + new Vector2(0, -16), "OPENING PORTAL", Txt.Size(14), new Color(1f, 0.7f, 0.6f));
    }
}

// The heavy's fat missile, drawn flying straight to its marked point (the host lands the blast).
public partial class HeavyMissileVisual : Node2D
{
    public Vector2 From, To; public double Flight;
    private double _t;
    public override void _Ready() { Position = From; Rotation = (To - From).Angle() + Mathf.Pi / 2f; ZIndex = 6; }
    public override void _Process(double delta)
    {
        _t += delta;
        Position = From.Lerp(To, (float)System.Math.Min(1, _t / Flight));
        if (_t >= Flight) { GetParent().AddChild(new Explosion { Position = To, Radius = Raider.BlastRadius * 0.8f }); QueueFree(); }
        QueueRedraw();
    }
    public override void _Draw()
    {   // fat: wider and longer than the player's missile
        var body = new Color(0.45f, 0.30f, 0.28f);
        DrawRect(new Rect2(-5f, -16f, 10f, 34f), body);
        DrawColoredPolygon(new[] { new Vector2(-5f, -16f), new Vector2(0, -25f), new Vector2(5f, -16f) }, new Color(1f, 0.3f, 0.25f));
        DrawCircle(new Vector2(0, 19f), 4.2f, new Color(1f, 0.6f, 0.3f));
    }
}
