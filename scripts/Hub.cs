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
    // The base: base_station.png, 874 x 874 (its centre is the station's centre),
    // drawn at 0.6. Its bottom pad was enlarged to 140 x 84 u and is the hauler's
    // landing pad. The haul lane is the pad's centre line; the portal sits on it, so
    // the hauler's every move is flat -- except an escort's (EscortRoute).
    public const float BaseScale = 0.6f;
    public static readonly Vector2 HaulerPad = new(0f, 219f);     // the enlarged bottom pad's centre: base_station.png row 802, (802 - 437) x 0.6
    public static float LaneY => HaulerPad.Y;
    public const float BaseBottom = 260f;                     // the pad's lower edge
    // a new ship's centre below the pad: half the longest class and a margin, so any class spawns clear
    private static readonly float SpawnClear = PlayerShip.Art.Values.Max(a => a.Length) * 0.5f + 30f;
    public static readonly Vector2 StemFoot = new(0f, 175f);  // where the pad hangs from the station
    // Both fields' nearest edges sit 1500 u from the base's centre (about 6.7 battleship
    // lengths): room for a blockade outside the base's 600 u missile cover. Measured to each
    // field's boundary -- the belt's nearest rock edge (sun at y -1794 with 9 rocks on the
    // 520 x 286 ellipse, rocks 15 u), the wreck's visible edge toward the base (340 u out).
    public static readonly Vector2 SunPos    = new(0, -1794);
    public static readonly Vector2 WreckPos  = new(-1840, 60);
    public static readonly Vector2 PortalPos = new(1500, 219);
    // FOUR OUTPOSTS, 3000 u out from the base on the diagonals (1000 further than they were) (y grows south): small permanent
    // stations the escort delivers to, named for their corner, in the order the escort visits
    // them -- counter-clockwise from the south-east. Every peer builds them (BuildWorld).
    public const float OutpostOut = 3000f, OutpostHeight = 170f;
    public static readonly (string name, Vector2 at)[] Outposts =
    {
        ("SE", BasePos + new Vector2(1, 1).Normalized() * OutpostOut), ("NE", BasePos + new Vector2(1, -1).Normalized() * OutpostOut),
        ("NW", BasePos + new Vector2(-1, -1).Normalized() * OutpostOut), ("SW", BasePos + new Vector2(-1, 1).Normalized() * OutpostOut),
    };
    // Where the hauler holds beside an outpost to offload: on its base side, clear of the station --
    // half the hauler (100 u), arriving nose-first, plus half the station (85 u), plus a gap.
    public const float OutpostDock = 230f;
    public static Vector2 DockAt(Vector2 outpost) => outpost + (BasePos - outpost).Normalized() * OutpostDock;
    // AN ESCORT's long way to the portal: to each outpost in turn, holding at each (Hauler), then to
    // the jump (~13,000 u). Written after the outposts and PortalPos, which it is made of: static
    // fields start in the order they are written.
    public static readonly Vector2[] EscortRoute = Outposts.Select(o => DockAt(o.at)).Append(PortalPos).ToArray();
    // Threat Intelligence Operations: south-west of the base, clear of the wreck, the
    // salvage routes, the haul lane and the dummies.
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
    public Hints Hints { get; private set; }                  // the tutorial's corner card (every world)

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
    private double _arenaEndT = -1;                         // the FAILURE clock only: a wiped party home in 3 s
    public bool MissionWon { get; private set; }
    // AFTER A WIN there is no clock. Each pilot presses RETURN when it has finished collecting, and
    // the whole party warps home once every pilot PRESENT has (a held place does not block it). Kept
    // apart from the launch READY so a launch-ready is never mistaken for a return-ready.
    private readonly HashSet<int> _returning = new();
    private bool _iReturned;                                // this peer's own press, guest side
    private int _returnReady, _returnTotal;                 // the host's tally, for a guest's button
    public bool IReturned => Net.IsHost ? _returning.Contains(Net.LocalId) : _iReturned;
    public int ReturnReady => Net.IsHost ? _returning.Count(_ships.ContainsKey) : _returnReady;
    public int ReturnTotal => Net.IsHost ? _ships.Count : _returnTotal;
    // This pilot's own crates, in this world (nobody else's are ever here).
    private readonly List<LootCrate> _crates = new();
    public IReadOnlyList<LootCrate> Crates => _crates;
    public int CratesDropped { get; private set; }
    public void CrateTaken(LootCrate c) => _crates.Remove(c);
    private EscMenu _esc;
    public bool EscMenuOpen => IsInstanceValid(_esc);
    public void ToggleEscMenu()
    {
        if (IsInstanceValid(_esc)) { _esc.QueueFree(); _esc = null; return; }
        _esc = new EscMenu { Hub = this }; AddChild(_esc);
    }
    public System.Collections.Generic.IEnumerable<PlayerShip> Ships => _ships.Values;
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
        _world = ++_worldSerial;
        if (Net.IsHost) RefreshAway();               // places held from the world before this one
        // BEFORE BuildWorld, NOT AFTER. Normally the select screen has loaded a character; this
        // covers running the hub directly (editor F6, the smoke test). It used to sit fifty lines
        // below, and BuildWorld builds the Yard, whose _Ready reads Character.Base* -- so the base
        // was loaded from statics that were still zero, and the Yard's first-frame autosave then
        // wrote those zeros back over the character file EnsureLoaded had just read. Silent base
        // loss, every time a hub was entered without the select screen.
        Character.EnsureLoaded();
        // Every way into a world passes here, so this is where drops not flown over reach the hold:
        // the arena left behind, a quit in the victory window, a host lost before home.
        Loot.ClaimAll();
        Music.CombatZone = InArena;
        if (!InArena) BuildWorld();                  // the arena's boss comes after Combat.Clear, below
        // Hit flashes get their own layer ABOVE the hulls (ships sit at z 4) and below the
        // turret sprites, so a shot is seen leaving a turret on top of the ship -- drawn at
        // the world's own level they started underneath it.
        AddChild(new FlashLayer { Hub = this, ZIndex = 8, ZAsRelative = false });
        AddChild(new DamageNumbers { Name = "DamageNumbers" });
        _cam = new Camera2D { Zoom = new Vector2(DefaultZoom, DefaultZoom) }; AddChild(_cam); _cam.MakeCurrent();

        var layer = new CanvasLayer(); AddChild(layer);
        _hudLayer = layer;
        Control HudButton(string text, string name, System.Action press, float x)
        {
            var wrap = Ui.Wrap(Ui.Btn(text, press, name)); wrap.Position = new Vector2(x, 48);
            layer.AddChild(wrap);
            return wrap;
        }
        HudButton("BASE (B)", "BaseButton", ToggleBase, 256).Visible = !InArena;   // no base out there
        HudButton("PILOT (L)", "PilotButton", TogglePilot, 356);
        HudButton("EQUIPMENT (I)", "EquipmentButton", ToggleEquipment, 446);
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
        layer.AddChild(new ReturnButton { Hub = this });            // RETURN TO BASE, after a win only
        layer.AddChild(new Radar { Hub = this });
        layer.AddChild(new AbilityBar { Hub = this });
        if (!InArena) layer.AddChild(new HaulerHud { Hub = this });
        Hints = new Hints { Hub = this }; AddChild(Hints);

        Settings.EnsureLoaded();       // ability key bindings
        Combat.Clear();
        // Flashes are made on the host, where the shots happen. Guests are sent them,
        // or a guest firing at the dummy would see nothing at all.
        Combat.OnFlash = (a, b, c, snd) =>
        {
            AddFlash(a, b, c, snd);
            ToWorld(nameof(NetFlash), a, b, c, (int)snd);
        };
        // Shells and torpedoes are born in this world; the host's copy deals damage, and guests
        // get the launch and fly a cosmetic copy (the run is straight and steady, so it lands in
        // the same place).
        Combat.World = this;
        Combat.ShellFired = s => ToWorld(nameof(NetShell), s.Position, s.Dir, s.Speed, s.Range);
        Combat.TorpedoFired = t => ToWorld(nameof(NetTorpedo), t.Position, t.Dir, t.Speed, t.Range, t.TargetId, t.TurnRate, t.Heavy, t.HostileFire, t.NetId, t.Size);
        Combat.SlugFired = s => ToWorld(nameof(NetSlug), s.Position, s.Dir, s.Speed, s.Range, s.Radius, (int)s.Look, s.Variant);

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
            d.Published = (last, avg, total) => ToWorld(nameof(NetDummy), n, last, avg, total);
        }

        if (Net.I != null)
        {
            Net.I.PlayerJoined   += OnPlayerJoined;
            Net.I.PlayerLeft     += OnPlayerLeft;
            Net.I.SessionChanged += OnSessionChanged;
            ReportSector();                                      // this world is loaded: tell the host where we are
        }
        Hints.Meet("flight");
    }

    // Net is an autoload and outlives this scene. Every handler added in _Ready comes
    // off here, or the next join or status line calls into a freed Hub.
    public override void _ExitTree()
    {
        if (Net.I != null)
        {
            Net.I.PlayerJoined   -= OnPlayerJoined;
            Net.I.PlayerLeft     -= OnPlayerLeft;
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

        // the wreck: its edge 1500 u from the base, like the belt's
        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>("res://behemoth_wreck.png"),
                                Position = WreckPos, Scale = new Vector2(0.34f, 0.34f), ZIndex = 1 });

        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>("res://base_station.png"), Name = "Base",
                                Position = BasePos, Scale = new Vector2(BaseScale, BaseScale), ZIndex = 2 });
        AddChild(new BaseDefense());                               // the base's own laser and missiles
        var tio = Sprites.Fit("res://tio_building.png", TioHeight);
        tio.Name = "TIO"; tio.Position = TioPos; tio.ZIndex = 2;
        AddChild(tio);
        _tioSprite = tio;
        AddChild(new MissionBar { Hub = this, ZIndex = 6 });
        // its name, drawn in the world like every world label (a UI control here counted
        // as "off-screen" whenever the camera looked elsewhere)
        AddChild(new WorldLabel { Text = "THREAT INTELLIGENCE OPERATIONS", Position = TioPos + new Vector2(0, TioHeight / 2 + 16), ZIndex = 2 });
        foreach (var (name, at) in Outposts)
        {   // the outposts: permanent, and nothing to replicate -- every peer builds the same four
            var o = Sprites.Fit("res://outpost.png", OutpostHeight);
            o.Name = "Outpost" + name; o.Position = at; o.ZIndex = 2;
            AddChild(o);
            AddChild(new WorldLabel { Text = "OUTPOST " + name, Position = at + new Vector2(0, OutpostHeight / 2 + 16), ZIndex = 2 });
        }

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
    // A session over (offline, a guest now, the main menu): which world each guest was in, and the
    // places held for dropped pilots, go with it.
    // ── A SHIP'S STATE, BY WAY OF THE HUB ─────────────────────────────────────
    // The unreliable updates for a ship are sent to the hub, which is at the same path on every
    // peer, and not to the ship's node, which is not: a pilot's first updates can overtake Godot's
    // (reliable) word that it joined, and reached another guest with no ship for it yet -- "Node
    // not found" in that guest's log (seen over a lossy link). The hub hands each to its ship, or
    // drops it. The owner's report goes to the SENDER'S ship only: nobody moves another's.
    private PlayerShip ShipOf(int peer) => _ships.TryGetValue(peer, out var s) && IsInstanceValid(s) ? s : null;
    public void SendShipState(float px, float py, float vx, float vy, float rot, float ax, float ay, bool trigger, bool staggered,
                              float podX, float podY, float podRot, bool warping) =>
        Rpc(nameof(NetShipState), px, py, vx, vy, rot, ax, ay, trigger, staggered, podX, podY, podRot, warping);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetShipState(float px, float py, float vx, float vy, float rot, float ax, float ay, bool trigger, bool staggered,
                              float podX, float podY, float podRot, bool warping) =>
        ShipOf(Net.SenderOf(this))?.ApplyState(px, py, vx, vy, rot, ax, ay, trigger, staggered, podX, podY, podRot, warping);
    public void SendHostState(int owner, double hp, double maxHp, bool alive, double stasis, bool pinned, double combat, double pdLeft, double pdRecharge,
                              int mag, double reload, double bsWindup, int bsVolleys, double bsCooldown,
                              int wingTarget, Vector2[] wingPos, float[] wingRot, int[] wingState, float[] wingRearm) =>
        Rpc(nameof(NetHostState), owner, hp, maxHp, alive, stasis, pinned, combat, pdLeft, pdRecharge, mag, reload, bsWindup, bsVolleys, bsCooldown,
            wingTarget, wingPos, wingRot, wingState, wingRearm);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetHostState(int owner, double hp, double maxHp, bool alive, double stasis, bool pinned, double combat, double pdLeft, double pdRecharge,
                              int mag, double reload, double bsWindup, int bsVolleys, double bsCooldown,
                              int wingTarget, Vector2[] wingPos, float[] wingRot, int[] wingState, float[] wingRearm) =>
        ShipOf(owner)?.ApplyHostState(hp, maxHp, alive, stasis, pinned, combat, pdLeft, pdRecharge, mag, reload, bsWindup, bsVolleys, bsCooldown,
                                      wingTarget, wingPos, wingRot, wingState, wingRearm);
    public void SendShield(int owner, float side) => Rpc(nameof(NetShield), owner, side);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetShield(int owner, float side) => ShipOf(owner)?.ApplyShield(side);

    public static void EndSession() { _peerSector.Clear(); _held.Clear(); _recentKills.Clear(); }
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
    // A WORLD EVENT -- a flash, a shell, a raider, a readout -- to the peers in THIS world only. The
    // hub is the same node in both sectors, so sent to everyone it reached a guest still in (or
    // loading) the other one and landed in the wrong world: a home raid's raiders spawned in a
    // guest's arena. Host only. Session-wide news (identity, sector moves, the kill's EXP, the
    // mission) still goes to everyone.
    private void ToWorld(StringName method, params Variant[] args) { if (Net.IsHost && Net.IsOnline) RpcToSector(Sector, this, method, args); }
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
        if ((SectorKind)s != Sector) { RpcId(who, nameof(NetSector), (int)Sector, Missions.Level); return; }
        RpcId(who, nameof(NetMission), MissionArgs());
        foreach (var r in Raiders) RpcId(who, nameof(NetRaiderSpawn), r.NetId, r.Position, (int)r.Kind, r.Strength, r.HullShare);
        if (Sector == SectorKind.Arena && IsInstanceValid(Boss) && Boss.Alive) Boss.CatchUp(who);   // the warnings (and a rock) up now
        if (MissionWon) { RpcId(who, nameof(NetWon), _ships.Count); RpcId(who, nameof(NetReturnCount), ReturnReady, ReturnTotal); }
        if (_placeFor.Remove(who, out var place)) RpcId(who, nameof(NetPlace), place.at, place.rot);
    }
    private void ReportSector() { if (!Net.IsHost) Net.AskHost(this, nameof(NetMySector), (int)Sector); }

    private bool _guestBefore;
    private void OnSessionChanged()
    {
        // Raiders belong to whoever simulates them. Becoming a guest, the ones this world had
        // would sit frozen (nothing simulates them any more); leaving a host, its copies would come
        // alive in your own world and attack your base. Either way they go.
        if (_guestBefore || !Net.IsHost) foreach (var r in Raiders.ToList()) DropRaider(r, burst: false);
        bool wasGuest = _guestBefore;
        _guestBefore = !Net.IsHost;
        ReportSector();
        // The session over (or now another's): who is held, who is READY and -- for a guest that was
        // one -- the mission all belonged to it. Kept, a held pilot kept a solo world's portal shut,
        // and a guest found the host's portal still open in its own world. (The host's LEVEL stays
        // until this pilot docks at the TIO, which picks its own again: READY is pressed there.)
        if (!(Net.IsHost && Net.IsOnline)) { EndSession(); _away.Clear(); _ready.Clear(); }
        if (wasGuest && Net.IsHost) { Mission = MissionState.Idle; MissionT = 0; }
        // the host gone while the party is in the arena: home, to your own base
        if (InArena && !Net.IsOnline) { GoTo(SectorKind.Home); return; }
        Yard?.OnSessionChanged(!Net.IsHost);    // parks or restores your own yard (home only)
        RebuildShips();
        // a guest that just connected introduces itself; the host and existing
        // guests introduce themselves to it from OnPlayerJoined
        if (Net.IsOnline && !Net.IsHost)
        {
            SendIdentity();
            // ...and says it all twice more. Over the internet its first words can reach the host
            // before the host has finished letting it in -- a lost handshake packet is resent AFTER
            // the words that followed it -- and the host throws away what arrives too early: then it
            // never knew this guest's world, and never sent it anything for it. Both are safe to repeat.
            foreach (double later in new[] { 1.0, 3.0 })
                GetTree().CreateTimer(later).Timeout += () =>
                {
                    if (IsInstanceValid(this) && Net.IsOnline && !Net.IsHost) { ReportSector(); SendIdentity(); }
                };
        }
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
        s.Init(peerId, BasePos + new Vector2(0, BaseBottom + SpawnClear + 60 * _ships.Count));
        _ships[peerId] = s;
        ApplyIdentity(s);
        TryRestoreHold(peerId);                // its identity may have arrived before its ship
    }

    // ── A DROPPED PILOT'S PLACE (host) ───────────────────────────────────────
    // A guest whose connection drops -- no goodbye -- keeps its place for 90 s: its party slot and
    // READY, and, in the same world, its hull, stasis and where it was. It is known by its
    // CHARACTER id (a peer id changes on a reconnect). The boss is scaled by the ships present, and
    // a kill while it is away is owed to it: its EXP, its bounty share and its crates arrive when it
    // does. A pilot that leaves on purpose gives its place up. (The id is the guest's own claim, as
    // its name and gear are: the host has no other way to know a returning player.)
    public const double HoldFor = 90;
    private sealed class Held
    {
        public int OldPeer, World; public string Name = ""; public ShipClass Class; public ulong Until;
        public Vector2 Pos; public float Rot; public double Hp, Stasis; public bool Alive = true;
        public readonly List<Kill> Owed = new();
    }
    private static readonly Dictionary<string, Held> _held = new();
    private static int _worldSerial;
    private int _world;                                         // this world, of all the host has built
    private readonly Dictionary<int, string> _away = new();     // the held pilots, by their old peer id: the party's absent members
    private readonly Dictionary<int, (Vector2 at, float rot)> _placeFor = new();

    private void OnPlayerLeft(int peer, Net.PlayerInfo info, bool onPurpose)
    {
        if (Net.IsHost && Net.IsOnline && info.CharacterId.Length > 0)
        {
            var back = Net.I.Players.FirstOrDefault(kv => kv.Key != peer && kv.Value.CharacterId == info.CharacterId).Key;
            var h = onPurpose ? null : HoldOf(peer, info);
            // a kill in the moments before the host noticed the drop: owed too (paid once, by serial)
            if (h != null) h.Owed.AddRange(_recentKills.Where(k => k.World == _world && k.Present.Contains(peer) && Time.GetTicksMsec() - k.When <= OwedWindowMs));
            if (h != null && back != 0) RestoreHeld(h, back);    // it is already back under a new id
            else if (h != null) _held[info.CharacterId] = h;
            else _ready.Remove(peer);
        }
        _peerSector.Remove(peer);
        if (Net.IsHost) { RefreshAway(); BroadcastMission(); }
        DespawnFor(peer);
    }
    private Held HoldOf(int peer, Net.PlayerInfo info)
    {
        var h = new Held { OldPeer = peer, World = _world, Name = info.Name, Class = info.Class,
                           Until = Time.GetTicksMsec() + (ulong)(HoldFor * 1000) };
        if (_ships.TryGetValue(peer, out var s) && IsInstanceValid(s))
            (h.Pos, h.Rot, h.Hp, h.Alive, h.Stasis) = (s.Position, s.Rotation, s.Hp, s.Alive, s.StasisLeft);
        return h;
    }
    private void TryRestoreHold(int peer)
    {
        if (!Net.IsHost || !Net.I.Players.TryGetValue(peer, out var p) || p.CharacterId.Length == 0) return;
        if (!_held.Remove(p.CharacterId, out var h) || !_ships.ContainsKey(peer)) { if (h != null) _held[p.CharacterId] = h; return; }
        RestoreHeld(h, peer);
        RefreshAway(); BroadcastMission();
    }
    private void RestoreHeld(Held h, int peer)
    {
        if (_ready.Remove(h.OldPeer, out var r)) _ready[peer] = r;
        if (h.World == _world && _ships.TryGetValue(peer, out var s) && IsInstanceValid(s))
        {
            s.Restore(h.Hp, h.Alive, h.Stasis);
            if (PeerSector(peer) == Sector) RpcId(peer, nameof(NetPlace), h.Pos, h.Rot); else _placeFor[peer] = (h.Pos, h.Rot);
        }
        foreach (var k in h.Owed) PayKill(k, h.OldPeer, peer);          // what it missed (a kill it did get is not paid again)
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetPlace(Vector2 at, float rot) { if (MyShip is { } me) me.Place(at, rot); }
    private void ExpireHolds()
    {
        ulong now = Time.GetTicksMsec();
        var gone = _held.Where(kv => kv.Value.Until <= now).Select(kv => kv.Key).ToList();
        if (gone.Count == 0) return;
        foreach (var id in gone) { _ready.Remove(_held[id].OldPeer); _held.Remove(id); }
        RefreshAway(); BroadcastMission();
    }
    private void RefreshAway()
    {
        _away.Clear();
        foreach (var h in _held.Values) _away[h.OldPeer] = h.Name;
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
        var args = new Variant[] { Net.LocalId, Character.Name, Character.Main, Character.Accent, (int)Character.Class, Character.Bought, Character.Level, Character.LoadoutFor(Character.Class), Character.Id };
        if (toPeer == 0) Rpc(nameof(NetIdentity), args);
        else             RpcId(toPeer, nameof(NetIdentity), args);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetIdentity(int peer, string name, Color main, Color accent, int cls, int[] bought, int level, string[] equip, string characterId)
    {
        // a peer may only describe itself
        if (Net.SenderOf(this) != peer || Net.I == null) return;
        if (!Net.I.Players.TryGetValue(peer, out var p)) Net.I.Players[peer] = p = new Net.PlayerInfo();
        // Everything else off this wire is sanitised (bought is null-guarded below, equip inside
        // Equipment.Sanitize); the name was not. A null here threw on .Length, and a null that got
        // past became a null Pilot in every label that draws it.
        name = string.IsNullOrEmpty(name) ? Character.Defaults.Name : name.Length > 24 ? name[..24] : name;
        // purchases the claimed level could not have paid for are refused outright
        if (!Progression.Affordable(bought, level)) bought = new int[Progression.All.Length];
        characterId ??= "";
        if (characterId.Length > 64) characterId = "";
        (p.Name, p.Main, p.Accent, p.Class, p.Bought, p.Equip, p.CharacterId, p.HasIdentity) =
            (name, main, accent, Classes.Sanitize(cls), bought, equip ?? System.Array.Empty<string>(), characterId, true);
        p.Level = System.Math.Clamp(level, 1, 100);             // a claim: bounded before it weighs on an escort's threat
        if (_ships.TryGetValue(peer, out var s) && IsInstanceValid(s)) ApplyIdentity(s);
        TryRestoreHold(peer);
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
    // the party is everyone in the session, and every pilot whose place is held (reconnecting):
    // one who was not READY keeps the portal shut until it is back, or its place lapses
    public IEnumerable<int> PartyIds => _ships.Keys.Concat(_away.Keys);
    public bool IsReady(int id) => _ready.TryGetValue(id, out var r) && r;
    public bool AllReady => _ships.Count > 0 && PartyIds.All(IsReady);
    public string PilotName(int id) => _ships.TryGetValue(id, out var s) && IsInstanceValid(s) ? s.Pilot
                                     : _away.TryGetValue(id, out var n) ? $"{n} (reconnecting)" : $"pilot {id}";
    public bool TioOpen => SideIs<TioWindow>();

    public void OpenTio()
    {
        if (SideIs<TioWindow>()) return;
        Hints.Meet("tio");
        if (Net.IsHost && Mission == MissionState.Idle)
        {   // docking at the TIO selects the newest unlocked tier
            Missions.Level = Missions.Unlocked;
            BroadcastMission();
        }
        ToggleSide(() => new TioWindow { Hub = this });
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

    // ── RETURN from a won arena. Each pilot toggles its own; the host takes the whole party home
    // once every pilot present has pressed it. A stasis pilot may press it too (it is a HUD button,
    // not a ship order). Mirrors SetMyReady above.
    public void SetMyReturn(bool on)
    {
        if (!MissionWon) return;
        if (Net.IsHost) { if (on) _returning.Add(Net.LocalId); else _returning.Remove(Net.LocalId); AfterReturn(); }
        else { _iReturned = on; Net.AskHost(this, nameof(RequestReturn), on); }
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestReturn(bool on)
    {
        if (!Net.FromPlayer(this, out int who) || !_ships.ContainsKey(who) || !MissionWon) return;
        if (on) _returning.Add(who); else _returning.Remove(who);
        AfterReturn();
    }
    // host: tell the arena how many are ready, and take everyone home once every present pilot is
    private void AfterReturn()
    {
        ToWorld(nameof(NetReturnCount), ReturnReady, ReturnTotal);
        if (_ships.Count > 0 && _ships.Keys.All(_returning.Contains)) EnterSector(SectorKind.Home);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetReturnCount(int ready, int total) { _returnReady = ready; _returnTotal = total; }

    private const float PortalEnterRadius = 190f;

    // ── moving the party between sectors (host decides; every peer follows) ──
    public void EnterSector(SectorKind k)
    {
        if (!Net.IsHost) return;
        if (k == SectorKind.Arena) Yard?.SaveForTrip();
        if (Net.IsOnline) Rpc(nameof(NetSector), (int)k, Missions.Level);
        GoTo(k);
    }
    // With the level: a world is built from it (the arena's boss is the level's), so a peer must
    // have it BEFORE the scene changes, not in the mission report that follows.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetSector(int k, int level) { Missions.Level = System.Math.Max(1, level); GoTo((SectorKind)k); }
    private void GoTo(SectorKind k)
    {
        Sector = k;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Hub.tscn");
    }

    // ── the arena: the boss, and how it ends ────────────────────────────────
    // The level's boss (Missions.ForLevel), the same one on every peer.
    private void BuildArena()
    {
        var type = Missions.ForLevel(Missions.Level);
        Boss = type.Make();
        Boss.Hub = this; Boss.Type = type; Boss.Position = BasePos + new Vector2(0, -700f); Boss.Rotation = Mathf.Pi;
        AddChild(Boss);
    }

    // host: the boss is dead. Its escorts and raiders die with it -- collecting is not fighting. Every
    // pilot gets its own EXP (its level, its first clears) and its share of the bounty, then its own
    // crates. Nothing sends anyone home on a clock: each pilot presses RETURN when it is done.
    public void BossDefeated()
    {
        if (!Net.IsHost || MissionWon) return;
        MissionWon = true;
        foreach (var r in Raiders.ToList()) RaiderDown(r);
        AnnounceBossKill(Missions.Level, IsInstanceValid(Boss) ? Boss.Position : Vector2.Zero);
        _returning.Clear();
        ToWorld(nameof(NetWon), _ships.Count);               // the party may now RETURN; how many must
    }
    // Guests see the boss at zero too (its last hull report went out before the killing blow), and
    // learn how many pilots the RETURN needs.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetWon(int total)
    {
        MissionWon = true;
        _returnTotal = total; _returnReady = 0; _iReturned = false;
        if (IsInstanceValid(Boss)) Boss.Downed();
    }

    // This pilot's drops: on its own file at once (like the bounty), then as crates to fly over
    // on a ring round where the boss died. Not in the arena any more: straight into the hold.
    private void ReceiveLoot(string[] items, Vector2 at, int level)
    {
        items = Loot.Sanitize(items, Loot.CratesFor(level));
        if (items.Length > 0) Hints.Meet(InArena ? "loot" : "equipment");   // at home the parts go straight into the hold
        Character.Unclaimed.AddRange(items);
        Character.Save();
        if (!InArena) { Loot.ClaimAll(); return; }
        for (int i = 0; i < items.Length; i++)
        {
            var c = new LootCrate { Name = $"Crate_{CratesDropped}", Hub = this, Item = items[i],
                                    Position = at + Vector2.Right.Rotated(Mathf.Tau * i / items.Length + 0.3f) * Loot.Ring };
            AddChild(c); _crates.Add(c); CratesDropped++;
        }
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

    // THE COSMETIC CHANNEL. Every reliable RPC shares one ordered ENet channel by default, so on
    // the internet one lost packet holds up everything queued behind it until it is resent --
    // and a firing battleship sends a shell every quarter second. Shells, torpedoes and the
    // missiles shot down (which must stay in order with their torpedoes) go on their own channel,
    // so a lost one no longer delays a telegraph, a raider's arrival or the economy's report.
    private const int Cosmetic = 1;

    // host: a missile was shot down; every guest bursts its copy
    public void MissileDown(int id) => ToWorld(nameof(NetMissileDown), id);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Cosmetic)]
    private void NetMissileDown(int id)
    {
        foreach (var t in GetChildren().OfType<Torpedo>()) if (t.NetId == id) t.Intercept();
    }

    // host: pick a level between 1 and the newest unlocked
    public void SelectLevel(int level)
    {
        if (!Net.IsHost || Mission != MissionState.Idle) return;
        Missions.Level = System.Math.Clamp(level, 1, Missions.Unlocked);
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
        return new Variant[] { (int)Mission, MissionT, ids, rs, Missions.Level, _away.Keys.ToArray(), _away.Values.ToArray() };
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetMission(int state, double t, int[] ids, int[] ready, int level, int[] away, string[] awayNames)
    {
        Missions.Level = level;
        Mission = (MissionState)state; MissionT = t;
        _ready.Clear(); for (int i = 0; i < ids.Length && i < ready.Length; i++) _ready[ids[i]] = ready[i] != 0;
        _away.Clear(); for (int i = 0; i < away.Length && i < awayNames.Length; i++) _away[away[i]] = awayNames[i] ?? "";
    }

    // ── raids: a failed mission brings the boss's raiders to your base ─────────
    // Level L (the failed boss's): raiders at S(L) = 1.1^(L-1), the boss's own scaling.
    // 2 patrols, and 1 more per extra pilot in the session, in from the map's edge 3 s
    // after the party is home (so every guest's world is loaded before they appear).
    private static int PendingRaidLevel;                        // survives the scene change home
    // Where a raid comes in from, and where a heavy waits for its quarry to be pinned: far enough
    // outside the outposts (3000 u) that the wait is the same 1430 u standoff it was tuned at.
    public const float RaidEdge = 4200f;
    public const double RaidDelay = 3.0;
    private double _raidIn = -1; private int _raidLevel;


    private void StartRaid(int level)
    {
        int patrols = 2 + System.Math.Max(0, _ships.Count - 1);
        for (int i = 0; i < patrols; i++)
        {
            float a = Mathf.Tau * i / patrols + 0.4f;
            SpawnPatrol(BasePos + Vector2.Right.Rotated(a) * RaidEdge, Missions.S(level));   // S(L) = 1.1^(L-1)
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
        foreach (var s in _ships.Values) if (Raider.Up(s)) yield return s;
        if (Yard == null) yield break;
        foreach (var u in Yard.Fleet) if (Raider.Up(u)) yield return u;
    }

    // A patrol: `lights` light fighters (3, unless an escort's threat adds more) and (unless `heavy`
    // is false) 1 heavy, spawned together at `at` on the perimeter -- hunters of `quarry`, if one is
    // given, built with `hullShare` of their hull, at `agility` of their speed and turning.
    private int _patrols;
    public int SpawnPatrol(Vector2 at, double scale = 1, Node2D quarry = null, bool heavy = true, double hullShare = 1, int lights = 3, double agility = 1)
    {
        if (!Net.IsHost) return 0;
        int id = ++_patrols;
        var crew = new List<(Vector2 off, RaiderKind kind)>();
        for (int i = 0; i < lights; i++) crew.Add((new Vector2((i - (lights - 1) / 2f) * 40f, 0), RaiderKind.Light));
        if (heavy) crew.Add((new Vector2(0, 90), RaiderKind.Heavy));
        foreach (var (off, kind) in crew)
        {
            var r = SpawnRaider(at + off, kind, id, scale, hullShare);
            r.Quarry = quarry; r.Agility = agility;
        }
        return id;
    }

    // AN ESCORT'S HUNTERS: a wave sent after one quarry, in from the map's edge beside it -- one
    // patrol, and one more per extra pilot, at HALF their hull. The first wave is light fighters
    // only; every other wave after it brings a heavy too (the second, the fourth, ...). Alternate
    // waves come round from alternate sides. How hard each wave is, is the escort's THREAT.
    public const double HunterHull = 0.5;
    public void HuntWave(Node2D quarry, int wave)
    {
        if (!Net.IsHost) return;
        var (level, toughness) = PartyStanding();
        double t = EscortThreat(quarry is Hauler h ? h.Payout : 0, level, toughness, Missions.HighestBeaten, wave);
        int patrols = 1 + System.Math.Max(0, _ships.Count - 1);
        var edge = Raider.EdgeSpot(quarry.Position) - BasePos;
        for (int i = 0; i < patrols; i++)
            SpawnPatrol(BasePos + edge.Rotated((wave % 2 == 0 ? 1 : -1) * 0.35f * (i + 1)), ThreatStrength(t), quarry,
                        heavy: wave % 2 == 1, hullShare: HunterHull, lights: ThreatLights(t), agility: ThreatAgility(t));
    }

    // AN ESCORT'S THREAT, as a mission level: a quarter each from
    //   the load    1, and a level for every 1000 cr the cargo will fetch escorted (Hauler.Payout)
    //   the party   half its pilots' mean level, half its ships' toughness -- 1, and a level for each
    //               10% step its hull stands over its class's own (purchases and gear)
    //   the boss    the highest the base owner has beaten (1 before any)
    //   the route   how far along the escort the wave comes: 1 at the first, +0.5 a wave
    // A fresh base's first wave with a light load is about 1.4. The threat makes the hunters' hull and
    // damage (the missions' 10% a level), their numbers (a light more a patrol every 3 levels, 3 more
    // at most) and, very slightly, their speed and turning (1% a level, 10% at most).
    public const double CreditsPerThreat = 1000, ThreatPerWave = 0.5;
    public static double EscortThreat(double loadCredits, double partyLevel, double partyToughness, int highestBoss, int wave) =>
        0.25 * ((1 + System.Math.Max(0, loadCredits) / CreditsPerThreat)
              + (0.5 * partyLevel + 0.5 * partyToughness)
              + System.Math.Max(1, highestBoss)
              + (1 + ThreatPerWave * System.Math.Max(0, wave)));
    public static double ThreatStrength(double threat) => System.Math.Pow(Missions.LevelStep, System.Math.Max(0, threat - 1));
    public static int ThreatLights(double threat) => 3 + System.Math.Min(3, (int)System.Math.Floor(System.Math.Max(0, threat - 1) / 3));
    public static double ThreatAgility(double threat) => 1 + System.Math.Min(0.1, 0.01 * System.Math.Max(0, threat - 1));

    // the party, for an escort's threat: each pilot's level (the host's from its character, a
    // guest's as its identity claimed it, bounded) and its ship's toughness (EscortThreat)
    private (double level, double toughness) PartyStanding()
    {
        double level = 0, toughness = 0; int n = 0;
        foreach (var s in _ships.Values)
        {
            if (!IsInstanceValid(s)) continue;
            level += s.Mine ? Character.Level : Net.I != null && Net.I.Players.TryGetValue(s.OwnerId, out var p) ? p.Level : 1;
            double own = new ShipStats(s.Class)["hull"];
            toughness += 1 + System.Math.Log(System.Math.Max(1, own > 0 ? s.MaxHp / own : 1)) / System.Math.Log(Missions.LevelStep);
            n++;
        }
        return n == 0 ? (1, 1) : (level / n, toughness / n);
    }
    // The hunt is over (the quarry reached the portal, or was lost): its hunters withdraw -- gone,
    // not shot down.
    public void CallOff(Node2D quarry)
    {
        if (!Net.IsHost) return;
        foreach (var r in Raiders.Where(r => r.Quarry == quarry).ToList())
        {
            DropRaider(r, burst: false);
            ToWorld(nameof(NetRaiderGone), r.NetId, false, (float)r.Hp);
        }
    }

    public Raider SpawnRaider(Vector2 at, RaiderKind kind = RaiderKind.Light, int patrol = 0, double scale = 1, double hullShare = 1)
    {
        if (!Net.IsHost) return null;
        var r = AddRaider(++_raiderIds, at, kind, patrol, scale, hullShare);
        ToWorld(nameof(NetRaiderSpawn), r.NetId, at, (int)kind, scale, hullShare);
        return r;
    }
    // Idempotent: a late joiner is sent every raider that exists, and one of them may already
    // have reached it the ordinary way.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRaiderSpawn(int id, Vector2 at, int kind, double scale, double hullShare)
    {
        if (Raiders.All(x => x.NetId != id)) AddRaider(id, at, (RaiderKind)kind, 0, scale, hullShare);
    }
    private Raider AddRaider(int id, Vector2 at, RaiderKind kind, int patrol, double scale, double hullShare)
    {
        var r = new Raider { Hub = this, Kind = kind, Patrol = patrol, Strength = scale, HullShare = hullShare, NetId = id, Position = at, Name = $"Raider_{id}" };
        Raiders.Add(r); AddChild(r);
        return r;
    }

    public void RaiderDown(Raider r)
    {
        DropRaider(r, burst: true);
        ToWorld(nameof(NetRaiderGone), r.NetId, true, (float)System.Math.Max(0, r.Hp));
    }
    // (with its last hull: the host removes a raider before that hull reaches anyone, and a guest
    // shows the blow that finished it from this)
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRaiderGone(int id, bool burst, float hp)
    {
        if (Raiders.FirstOrDefault(x => x.NetId == id) is { } r) { r.Hp = hp; DropRaider(r, burst); }
    }
    // Also let go of it as the selection and as every ship's target: on a guest a raider's hull
    // never reads zero (the host removes it before its last hull reaches anyone), and a withdrawn
    // hunter (CallOff) is freed with hull left -- either way it stayed "alive" and freed, and a
    // carrier's fighters and point defence went on reading it. It leaves the hostiles NOW, not when
    // the queued free takes it out of the tree at the frame's end: a turret that ticked later in the
    // same frame acquired it again.
    private void DropRaider(Raider r, bool burst)
    {
        if (burst) AddChild(new Explosion { Position = r.Position, Radius = 28f });
        if (ReferenceEquals(_selected, r)) _selected = null;
        Combat.Hostiles.Remove(r);
        foreach (var s in _ships.Values) if (IsInstanceValid(s)) s.Forget(r);
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
        ToWorld(nameof(NetHeavyMissile), from, at);
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
                if (Raider.Gap(b.at, t) <= Raider.BlastRadius) (t as IRaidTarget)?.Hit(b.damage, b.at, $"heavy:{b.from}:missile");
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
            ToWorld(nameof(NetRaiders), part.Select(r => r.NetId).ToArray(), part.Select(r => r.Position).ToArray(),
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

    // ── A BOSS KILL, ONCE FOR EACH PILOT ──────────────────────────────────────
    // Every kill has a serial, unique to the host that announced it: a pilot is paid for a serial
    // once, however the payment reaches it -- at the kill, or owed when it comes back from a drop.
    // The host keeps the kills of the last 12 s, because a pilot whose connection died just before
    // one is still in the party when it happens (the host notices a dead link only seconds later),
    // and would otherwise never be paid. The serials a pilot has been paid are on its file
    // (Character.PaidKills), so not even a restart in between pays one twice.
    private sealed class Kill
    {
        public long Serial; public int Level, Party, World; public Vector2 At; public ulong When;
        public Dictionary<int, string[]> Drops; public HashSet<int> Present;
    }
    private static long _killSerial = System.DateTime.UtcNow.Ticks;
    private static readonly List<Kill> _recentKills = new();
    private const ulong OwedWindowMs = 12000;

    // a boss kill, as each guest receives it: its share of the bounty, its own parts, its own EXP
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetKill(int level, int party, long serial, string[] drops, Vector2 at)
    {
        if (!Character.PayOnce(serial)) return;
        Yard.AddGuestShare(Missions.BountyEach(level, party));              // to its own base, set aside while it visits
        ReceiveLoot(drops, at, level);
        Progression.AwardBossKill(level);                                   // (which saves: the share and the serial with it)
    }
    public int PartySize => _ships.Count;

    // host: a level-L boss is down. Every pilot credited with it -- the ships here, in stasis or not
    // (a pilot shot down still helped win it), and any whose place is held in this world -- gets its
    // own EXP, its share of the bounty (split among all of them) and its own crates, each sent to
    // that pilot alone: a pilot whose world is still loading still gets them, and nobody sees
    // another's. The held are owed theirs until they are back.
    public void AnnounceBossKill(int level, Vector2 at)
    {
        if (!Net.IsHost) return;
        var held = _held.Values.Where(h => h.World == _world).ToList();
        var here = _ships.Where(kv => IsInstanceValid(kv.Value)).Select(kv => (kv.Key, kv.Value.Class));
        var k = new Kill { Serial = ++_killSerial, Level = level, Party = System.Math.Max(1, PartySize + held.Count), World = _world, At = at,
                           When = Time.GetTicksMsec(), Present = _ships.Keys.ToHashSet(),
                           Drops = Loot.RollParty(here.Concat(held.Select(h => (h.OldPeer, h.Class))), level) };
        _recentKills.RemoveAll(x => k.When - x.When > OwedWindowMs);
        _recentKills.Add(k);
        foreach (var h in held) h.Owed.Add(k);
        Character.PayOnce(k.Serial);
        Yard.AddHostShare(Missions.BountyEach(k.Level, k.Party));        // the host's own share, paid at home
        ReceiveLoot(k.Drops.GetValueOrDefault(Net.LocalId, System.Array.Empty<string>()), at, k.Level);
        Progression.AwardBossKill(k.Level);                              // (which saves: the share and the parts with it)
        foreach (var peer in k.Present.Where(p => p != Net.LocalId)) PayKill(k, peer, peer);
    }
    // one pilot's share of a kill: `key` is who it was at the kill, `peer` who it is now
    private void PayKill(Kill k, int key, int peer)
    {
        RpcId(peer, nameof(NetKill), k.Level, k.Party, k.Serial, k.Drops.GetValueOrDefault(key, System.Array.Empty<string>()), k.At);
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



    public PlayerShip MyShip => _ships.TryGetValue(Net.LocalId, out var s) && IsInstanceValid(s) ? s : null;

    private void ToggleStats()
    {
        if (IsInstanceValid(_statsWin)) { _statsWin.QueueFree(); _statsWin = null; return; }
        if (MyShip == null) return;
        _statsWin = new StatsWindow { Ship = MyShip };
        AddChild(_statsWin);
    }

    // THE TUTORIAL'S TRIGGERS: the first time this pilot meets each system. Read from what every
    // peer has -- its own ship, the world it sees -- so a guest meets them as a host does.
    private void MeetHints(PlayerShip me)
    {
        if (Hints.Wants("base") && Yard != null && me.Position.DistanceTo(BasePos) < Hints.BaseMeet) Hints.Meet("base");
        if (Hints.Wants("tio") && Yard != null && me.Position.DistanceTo(TioPos) < Hints.TioMeet) Hints.Meet("tio");
        if (Hints.Wants("target") && Combat.Nearest(Combat.Hostiles, me.Position, h => h.Position, Hints.TargetMeet, Combat.Pickable) != null) Hints.Meet("target");
        if (Hints.Wants("abilities") && Selected != null) Hints.Meet("abilities");
        if (Hints.Wants("raid") && Yard != null && Raiders.Count > 0) Hints.Meet("raid");
        if (Hints.Wants("stasis") && !me.Alive) Hints.Meet("stasis");
        if (Hints.Wants("boss") && InArena && IsInstanceValid(Boss)) Hints.Meet("boss");
        if (Hints.Wants("warp") && WarpAim() is { has: true } aim && aim.at.DistanceTo(me.Position) > Hints.WarpMeet) Hints.Meet("warp");
    }

    // Tab: ALWAYS the live hostile nearest your ship, at any range. No cycling --
    // pressing it again re-picks the nearest, so it never lands on a far target.
    // Switch to anything else with a left-click. Never a missile (Combat.Pickable): in the
    // arena the boss's trident was often nearer than the boss, and Tab took a missile.
    private void SelectNearest()
    {
        var me = MyShip;
        if (me != null) _selected = Combat.Nearest(Combat.Hostiles, me.Position, h => h.Position, ok: Combat.Pickable);
    }

    // Left-click in the world: the hostile under the cursor (its hit circle, plus a
    // little slack), nearest the click if circles overlap. Clicking empty space keeps
    // the current target, so a stray click never drops it; Esc clears it.
    // true if the click landed on something (a hostile to select, or a building)
    private bool SelectAt(Vector2 world)
    {
        var best = Combat.Nearest(Combat.Hostiles, world, h => h.Position,
                                  ok: h => Combat.Pickable(h) && world.DistanceTo(h.Position) <= h.HitRadius + 16f);
        if (best != null) { SelectTarget(best); return true; }
        // buildings: a left-click on one opens its menu
        if (IsInstanceValid(_tioSprite) && _tioSprite.GetRect().HasPoint(_tioSprite.ToLocal(world))) { OpenTio(); return true; }
        if (!InArena && world.DistanceTo(BasePos) < 200f) { if (!SideIs<BasePanel>()) ToggleBase(); return true; }
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
        if (Net.IsHost && _held.Count > 0) ExpireHolds();
        if (Net.IsHost) TickRaid(delta);
        if (Net.IsHost) TickBlasts(delta);
        TickMission(delta);
        var me = MyShip;
        if (Music.I != null)
            Music.I.Target = me != null && me.InCombat ? Music.Mood.Combat
                           : Selected != null ? Music.Mood.Alert : Music.Mood.Ambient;
        ControlsLocked = IsInstanceValid(_creator) || IsInstanceValid(_esc) || GetViewport().GuiGetFocusOwner() is LineEdit
                         || (IsInstanceValid(_statsWin) && _statsWin.Capturing);
        // bars and labels keep a constant on-screen size whatever the zoom
        Txt.UiScale = 1f / _cam.Zoom.X;
        if (_selected != null && !_selected.Alive) _selected = null;
        if (me != null) MoveCamera(me, (float)delta);
        if (me != null) MeetHints(me);

        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.t -= delta;
            if (f.t <= 0) _flashes.RemoveAt(i); else _flashes[i] = f;
        }
        QueueRedraw();

        string ship = "";
        if (me != null)
        {
            // weapon and ability state lives on the ability bar; this line is who and where.
            // Built every frame so a class change or a rebind shows at once, but only ASSIGNED
            // when it actually differs (Ui.SetText): setting Text re-shapes the label's glyphs.
            Ui.SetText(_help, Abilities.ControlsHint(me.Class));
            ship = $"    |    {Character.Name}  {Classes.NameOf(me.Class)}"
                 + $"  {Mathf.Abs(me.SpeedAhead):0} u/s{(me.SpeedAhead < -1 ? " astern" : "")}";
            ship += Selected != null ? "    target: " + (Selected is TargetDummy td ? $"TARGET DUMMY {td.Number}" : $"#{Selected.NetId}")
                  : Waypoint != null ? $"    waypoint: {WaypointName}"
                                     : "    no target (Tab / click)";
            if (Raiders.Count > 0) ship += $"    |    RAIDERS {Raiders.Count}";
            if (Placing) ship += $"    PLACING {_placingLabel}: left-click to confirm, right-click / Esc to cancel";
        }
        string place = Yard == null
            ? $"ARENA  ·  {(IsInstanceValid(Boss) ? Boss.Type.Name : "")} (LEVEL {Missions.Level})  {(IsInstanceValid(Boss) ? Boss.Hp : 0):0} / {(IsInstanceValid(Boss) ? Boss.MaxHp : 0):0}" + (MissionWon ? "  ·  DEFEATED" : "")
            : $"ORE {Yard.Ore:0}    SALVAGE {Yard.Salvage:0}    CREDITS {Yard.Credits:0}"
              + $"    HAULER {Yard.Hauler.Cargo:0}/{Yard.Capacity:0} {Yard.Hauler.State.ToString().ToUpperInvariant()}";
        Ui.SetText(_hud, place + ship
                  + (Net.IsOnline ? (Net.IsHost ? $"        HOSTING ({_ships.Count})" : $"        GUEST ({_ships.Count})")
                     : Net.I == null ? "        OFFLINE"
                     : Net.I.Reconnecting ? $"        RECONNECTING ({System.Math.Max(1, Net.I.Attempt)} of {Net.RetryAt.Length})"
                     : Net.I.CanReconnect ? "        HOST LOST: RECONNECT (MULTIPLAYER)"
                     : Net.I.Connecting ? "        CONNECTING" : "        OFFLINE")
                  + (FreeCamera ? "        FREE CAMERA (Y)" : ""));
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
            // by zoom (HealthBar scales positions by Txt.UiScale, so divide it back out)
            float above = (s.MyArt.Length * 0.5f + 16f) / Txt.UiScale;
            HealthBar.Draw(this, new Vector2(-40, -above), 80, 6, s.Hp, s.MaxHp,
                           s.Hp / Mathf.Max(1, s.MaxHp) > 0.35 ? new Color(0.4f, 0.9f, 0.5f) : new Color(1f, 0.4f, 0.3f));
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
            else if (IsInstanceValid(_side)) CloseSide();
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
            else if (kk.Keycode == Key.V) mine.StartWarp();            // warp: a fixed key, not a slot
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

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Cosmetic)]
    private void NetShell(Vector2 from, Vector2 dir, float speed, float range) =>
        AddChild(new Shell { Position = from, Dir = dir, Speed = speed, Range = range, Cosmetic = true });

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Cosmetic)]
    private void NetSlug(Vector2 from, Vector2 dir, float speed, float range, float radius, int look, int variant) =>
        AddChild(new Slug { Position = from, Dir = dir, Speed = speed, Range = range, Radius = radius, Look = (Slug.Kind)look, Variant = variant, Cosmetic = true,
                            Lead = (float)(1.0 - Net.Arriving(1.0)) });   // the round trip (see Slug.Lead)

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Cosmetic)]
    private void NetTorpedo(Vector2 from, Vector2 dir, float speed, float range, int target, float turn, bool heavy, bool hostile, int id, float size) =>
        AddChild(new Torpedo { Position = from, Dir = dir, Speed = speed, Range = range, Cosmetic = true, TargetId = target, TurnRate = turn,
                               Heavy = heavy, HostileFire = hostile, NetId = id, Size = size });

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetFlash(Vector2 a, Vector2 b, Color c, int snd)
        => AddFlash(a, b, c, System.Enum.IsDefined(typeof(ShotSound), snd) ? (ShotSound)snd : ShotSound.Light);
    public const double FlashLife = 0.10;                            // a laser shot's flash, seconds
    private void AddFlash(Vector2 a, Vector2 b, Color c, ShotSound snd) { _flashes.Add((a, b, c, FlashLife)); Sfx.Laser(a, b, snd); }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetDummy(int number, double last, double avg, double total)
    {
        // BY NUMBER, not by position. The dummies are numbered 1, 3, 4, 5 (2's spot holds the two
        // practice fighters), so `_dummies[number - 1]` sent 3's readout to 4, 4's to 5, and
        // dropped 5's entirely because 5 > Count. Guests saw the wrong numbers on the wrong hulls.
        foreach (var d in _dummies) if (d.Number == number) { d.SetReadout(last, avg, total); return; }
    }

    // THE SIDE WINDOW. BASE, PILOT, EQUIPMENT and the TIO all sit in one spot, so at most one is
    // open: opening one closes whichever held the spot, and asking for the one that is open
    // closes it. Four fields with four hand-written "close the others" lists used to disagree --
    // the TIO opened over BASE or PILOT drew one window on top of another.
    private Control _side;
    private bool SideIs<T>() where T : Control => _side is T && IsInstanceValid(_side);
    private void CloseSide() { if (IsInstanceValid(_side)) _side.QueueFree(); _side = null; }
    private void ToggleSide<T>(System.Func<T> make) where T : Control
    {
        bool open = SideIs<T>();
        CloseSide();
        if (!open) { _side = make(); _hudLayer.AddChild(_side); }
    }
    private void ToggleBase() { if (Yard != null) ToggleSide(() => new BasePanel { Hub = this }); }   // the arena: no base to open
    public void ToggleEquipment() => ToggleSide(() => new EquipmentWindow { Hub = this });
    public void TogglePilot() => ToggleSide(() => new PilotWindow { Hub = this });
    public bool CreatorOpen => IsInstanceValid(_creator);
    public bool StatsOpen => IsInstanceValid(_statsWin);

    // A purchase: refit the ship now, and tell the host (it resolves hull and damage).
    public void PilotChanged() { ApplyLocalIdentity(); SendIdentity(); }

    // REFIT: the only way into the ship menu (it costs 10%; see Yard.ResetCost).
    public void ResetShip()
    {
        if (IsInstanceValid(_creator) || Yard == null) return;       // REFIT is at the base
        Yard.ChargeReset();
        if (SideIs<BasePanel>()) CloseSide();
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
//
// Its two readouts sit ON the bar, so every letter must read against whatever is under IT: dark on
// the fill (dark reads on green and on red alike), light on the empty track. Choosing one colour
// per string cannot do that -- the fill's edge runs through the middle of the text at most hull
// values, and the part past it went dark on dark ("WARP R", half a HULL number). So the bar is two
// layers drawing the same words: this node draws the track and the LIGHT copy; _fill, a child
// clipped to the filled width, draws the fill and the DARK copy over it. The UI lint cannot catch
// this kind of fault: it measures position and width, not contrast.
public partial class HullHud : Control
{
    private readonly StyleBox _panel = Ui.PanelStyle();   // built once: _Draw runs every frame
    private readonly StyleBox _track = Ui.Box(Ui.Deep, Ui.Line, 5);
    public Hub Hub;
    private const float W = 440, H = 22;
    private Control _fill;
    private float _frac;

    public override void _Ready()
    {
        AnchorLeft = AnchorRight = 0.5f; AnchorTop = AnchorBottom = 1f;
        OffsetLeft = -W / 2; OffsetRight = W / 2; OffsetTop = -150; OffsetBottom = -150 + H;
        MouseFilter = MouseFilterEnum.Ignore;
        _fill = new Control { Name = "Fill", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        _fill.Draw += () => DrawFill(_fill);
        AddChild(_fill);
    }

    public override void _Process(double delta)
    {
        var s = Hub?.MyShip;
        _frac = s == null ? 0 : (float)Mathf.Clamp(s.Hp / Mathf.Max(1, s.MaxHp), 0, 1);
        _fill.Visible = s != null;
        _fill.Size = new Vector2(1 + (W - 2) * _frac, H);
        QueueRedraw(); _fill.QueueRedraw();
    }

    public override void _Draw()
    {
        if (Hub?.MyShip == null) return;
        _panel.Draw(GetCanvasItem(), new Rect2(-8, -6, W + 16, H + 12));   // its panel
        _track.Draw(GetCanvasItem(), new Rect2(0, 0, W, H));
        Readouts(this, onFill: false);
    }

    // Your own hull keeps its green-to-red reading -- it is the one number you glance at while
    // being shot, and a palette accent would say nothing about how close you are to dying. The
    // TRACK comes from the palette so the bar still belongs to the rest of the HUD.
    private void DrawFill(Control c)
    {
        if (Hub?.MyShip == null) return;
        c.DrawRect(new Rect2(1, 1, (W - 2) * _frac, H - 2), _frac > 0.35f ? Ui.Good : Ui.Bad);
        Readouts(c, onFill: true);
    }

    private void Readouts(CanvasItem c, bool onFill)
    {
        var s = Hub.MyShip;
        Txt.D(c, ThemeDB.FallbackFont, new Vector2(0, H - 5), s.Alive ? $"HULL  {s.Hp:0} / {s.MaxHp:0}"
                  : s.CanReboard ? "SHIP READY  —  press F to re-board"
                  : $"SHIP IN STASIS  {(int)s.StasisLeft / 60}:{(int)s.StasisLeft % 60:00}  —  flying the escape pod",
              HorizontalAlignment.Center, W, 15, onFill && _frac > 0.35f ? Ui.Deep : Colors.White);
        if (!s.Alive) return;
        // off the fill the warp readout takes its state's colour; on it, dark like the hull's
        var warp = onFill ? Ui.Deep : s.Warping ? Ui.Accent : s.WarpCooldownLeft > 0 ? Ui.Dim : Ui.Good;
        Txt.D(c, ThemeDB.FallbackFont, new Vector2(W - 150, H - 5),
              s.Warping ? $"WARPING  {s.WarpWarmupLeft:0.0} s" : s.WarpCooldownLeft > 0 ? $"WARP  {s.WarpCooldownLeft:0} s" : "WARP  READY",
              HorizontalAlignment.Right, 144, 12, warp);
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
            DrawLine(f.a, f.b, new Color(f.c.R, f.c.G, f.c.B, (float)(f.t / Hub.FlashLife) * 0.9f), 2f);
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
