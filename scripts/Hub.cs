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
//     the base in the CENTRE, and the EQUIPMENT BASE beside it on the right (Landmarks.cs)
//     the outbound wormhole to the RIGHT  (trade runs, and the way to other systems)
//
// Everything that produces is host-owned (Net.Sim). Clients render the same world
// but never tick it, so two players in one hub always see the same numbers.
//
// WHAT THIS FILE STILL OWNS: the world's layout and what is built in it, the ships and the
// session handshake, the sectors, the mission, the RPCs (every wire in the game is a method on
// this node, because it is at the same path on every peer), and what the local player sees --
// the camera, the HUD, the selection, the input and the side window.
//
// WHAT IT NO LONGER OWNS, and where it went:
//   Waves.cs      what a wave is made of -- one row per wave (was three builders in here)
//   Raids.cs      the raid clock and the ONE wave builder (was TickRaid/StartRaid/SpawnPatrol/
//                 HuntWave/CallOff and four loose threat statics)
//   Spawned.cs    host-spawned replicated things -- one mechanic, one row per kind (was the
//                 raider group and the deployed-turret group, the same six methods twice)
//   Votes.cs      the party's votes -- one mechanic, one row per vote (was READY and RETURN,
//                 the same four steps twice)
//   Session.cs    what outlives a world: which sector each guest is in, held places, recent
//                 kills (the only reason this file had statics)
//   HubNodes.cs   the seven small nodes that touch none of this state
//   Landmarks.cs  the places at home -- every building and the three fields: where each stands, its
//                 art, its label, its scope mark, what a click on it opens and what it serves
// The public names those replaced are kept HERE as one-line faces (SpawnPatrol, HuntWave,
// CallOff, SpawnRaider, RaiderDown, Drop, DeployedDown, DeployedTaken, Raiders, Deployed,
// PeerSector, EndSession, SetMyReady, SetMyReturn, IsReady, AllReady, IReturned, ReturnReady,
// ReturnTotal), so nothing else in the game or the harness had to be re-pointed. A face is one
// line onto the one mechanic -- never a second copy of it.
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
    private static readonly float SpawnClear = Classes.All.Max(c => c.Art.Length) * 0.5f + 30f;
    public static readonly Vector2 StemFoot = new(0f, 175f);  // where the pad hangs from the station
    // Both fields' nearest edges sit 1500 u from the base's centre (about 6.7 battleship
    // lengths): room for a blockade outside the base's 600 u missile cover. Measured to each
    // field's boundary -- the belt's nearest rock edge (sun at y -1794 with 9 rocks on the
    // 520 x 286 ellipse, rocks 15 u), the wreck's visible edge toward the base (340 u out).
    public static readonly Vector2 SunPos    = new(0, -1794);
    public static readonly Vector2 WreckPos  = new(-1840, 60);
    public static readonly Vector2 PortalPos = new(1500, 219);
    // WHERE A MISSION IS BUILT in the arena: where a boss's NOSE stands (so a bigger boss stands
    // no nearer the party), and a raid site's centre. It was `BasePos + new Vector2(0, -700f)`
    // written into BuildArena, which made it the boss's alone.
    public static readonly Vector2 ArenaCentre = BasePos + new Vector2(0, -700f);
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
    // THE EQUIPMENT BASE, as the owner put it: "its own base", on the RIGHT of the station, with the
    // recycler on it (Landmarks.cs draws it and gates what is done there). Its 320 x 240 u pad runs
    // 560 - 880 u east and from 180 u north to 60 u south of the station's centre: 298 u clear of
    // the station's 262 u edge, and 159 u north of the haul lane's centre line (y 219).
    public static readonly Vector2 EquipmentPos = new(720, -60);
    public const float TioHeight = 260f;   // on the lane through that pad
    // THE PRACTICE RANGE: every target the hub builds for testing weapons, ONE ROW EACH -- its
    // number (its label, and its NetId: NetIds.Dummy + number - 1, the same on every peer), where it
    // stands, and what it is: ARMED (it fires back inside its ring) or a practice FIGHTER (a light
    // craft, so point defence has something to train on). They are numbered 1, 3, 4 and 5: 2's place
    // went to the two fighters. A new target is a row here and nothing in _Ready. REPLACED: three
    // anchor points here and a second list in _Ready that put the fighters either side of the middle
    // one and armed whichever target was number 3.
    //
    // WHERE, AND WHY THERE: south of the base, on its line, 500 u or more from every leg the hauler
    // flies -- the lone run both ways and the escort's five, each taken on past its end. The hauler's
    // own point defence takes any LIGHT craft inside 400 u, a practice fighter included, so a range
    // inside that had its meters moved by every run that passed; and a hunter's 90 u blast lands where
    // the hauler will be 12 s on -- on a pilot practising there (the blast takes pilots and the fleet,
    // never a practice target). On the base's line, 500 u off the lone run (y 219) means south of
    // y 719, and 500 u off the escort's last leg (the south-west outpost to the portal, crossing x = 0
    // at y ~973) means north of y 414 or south of y 1533: so a centred range starts at y ~1530. It
    // keeps 500 u from every lane to an outpost too, where the couriers run. The smoke test's layout
    // check proves both against this table.
    public static readonly (int number, Vector2 at, bool armed, bool fighter)[] PracticeTargets =
    {
        (1, new Vector2(-150, 1670), false, false),     // a plain hulk, to the west
        (3, new Vector2(150, 1850),  true,  false),     // the armed hulk, to the south-east
        (4, new Vector2(112, 1528),  false, true),      // the two practice fighters, to the north-east
        (5, new Vector2(188, 1572),  false, true),
    };
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
    public void SelectTarget(IHittable h) { _targets.Clear(); if (h != null) _targets.Add(h); Waypoint = null; }
    // ...and one MORE of them, up to what the hull can hold: shift-click, or a ctrl-drag's box.
    // Picking one already held drops it again, which is the only way to let one go without
    // starting over.
    public void AddTarget(IHittable h)
    {
        if (h == null) return;
        Waypoint = null;
        if (_targets.Remove(h)) return;
        if (_targets.Count >= TargetCap) { if (TargetCap <= 1) { _targets.Clear(); } else return; }
        _targets.Add(h);
    }
    public void SelectWaypoint(string name, Vector2 at, float radius) { _targets.Clear(); Waypoint = at; WaypointName = name; WaypointRadius = radius; }
    public void ClearSelection() { _targets.Clear(); Waypoint = null; }
    // what a warp aims at right now: the target, else the waypoint
    public (bool has, Vector2 at, float radius) WarpAim() =>
        Selected is { } t ? (true, t.Position, t.HitRadius)
        : Waypoint is { } w ? (true, w, WaypointRadius) : (false, Vector2.Zero, 0f);

    // ── THE SCOPE MARKS: everything the radar can point at ───────────────────
    // ONE TABLE. A row says what a mark is called, where it is, how close a warp to it stops
    // (its pick radius too), and the glyph that stands for it -- shape, half-size and colour.
    // Radar._GuiInput picks from this table and Radar._Draw draws from it, so a mark can never
    // again be pickable with nothing drawn for it. REPLACED: two hand-written lists of the same
    // seven landmarks, one in each half of Radar.cs, which had already drifted -- MISSION PORTAL
    // was clickable with no glyph, so the only way to that waypoint was a click on empty scope.
    // A NEW MARK IS A ROW -- of Landmarks.All if it is a place at home, here if it comes and goes --
    // and nothing at all in Radar.cs. Which rows exist is decided here
    // too: the home landmarks only at home, the mission portal only while it is open, and a
    // turret a freighter left out in whichever sector it stands in.
    public enum MarkShape { Dot, Box, Diamond, Ring }
    public readonly struct ScopeMark
    {
        public readonly string Name;        // what the waypoint is called
        public readonly Vector2 At;         // where it is, in the world
        public readonly float Pick;         // the waypoint's radius: a warp stops this far short
        public readonly MarkShape Shape;
        public readonly Vector2 Size;       // half-extents on the scope (X is the radius of a Dot or a Ring)
        public readonly Color Colour;
        public ScopeMark(string name, Vector2 at, float pick, MarkShape shape, Vector2 size, Color colour)
            => (Name, At, Pick, Shape, Size, Colour) = (name, at, pick, shape, size, colour);
    }

    public IEnumerable<ScopeMark> ScopeMarks()
    {
        if (!InArena)
        {   // home's landmarks, one row each of Landmarks.All: the arena has none of them
            foreach (var l in Landmarks.All) yield return l.Mark;
            if (Mission == MissionState.PortalOpen)
                yield return new ScopeMark("MISSION PORTAL", MissionPortalPos, 160f, MarkShape.Ring, new Vector2(5f, 5f), new Color(1f, 0.35f, 0.3f));
        }
        // THE TURRETS A FREIGHTER LEFT OUT, in either sector: the one thing a pilot places by hand
        // that the scope would not show, so three of them 3000 u away could not be found again.
        // In the owner's hull colour, as the turret itself is drawn.
        foreach (var t in Deployed)
            if (IsInstanceValid(t))
                yield return new ScopeMark(t.Label, t.Position, DeployedTurret.Radius, MarkShape.Box, new Vector2(2.5f, 2.5f),
                                           t.Ship != null && IsInstanceValid(t.Ship) ? t.Ship.Main : new Color(0.55f, 0.58f, 0.62f));
        // ...AND WHATEVER THE MISSION BUILT. A raid site is 2400 u across and nothing else puts it
        // on the scope, so four pylons were four things a pilot could only find by flying at them.
        foreach (var e in Emplacements)
            if (IsInstanceValid(e))
                yield return new ScopeMark(e.Label, e.Position, e.HitRadius, MarkShape.Diamond, new Vector2(3.5f, 4f), e.Def.Tint);
    }

    public Boss Boss { get; private set; }
    // WHAT THIS MISSION IS WON BY KILLING -- the boss, or the pirate base. The arena's status line
    // reads this and names no kind (Missions.Kinds[].Quarry).
    public IQuarry Quarry => Missions.KindOf(Missions.Kind).Quarry?.Invoke(this);
    private double _arenaEndT = -1;                         // the FAILURE clock only: a wiped party home in 3 s
    public bool MissionWon { get; private set; }
    // THE PARTY'S VOTES. READY (launch a mission) and RETURN (leave a won arena) are ONE
    // mechanic with a row each (Votes.cs). The comment that stood here said they were kept apart
    // "so a launch-ready is never mistaken for a return-ready" -- which an index does, without a
    // second set of fields, a second pair of RPCs and a second tally.
    private readonly Vote[] _votes = Votes.Fresh();
    public bool IReturned => _votes[Votes.Return].Mine;
    public int ReturnReady => _votes[Votes.Return].Ready(this);
    public int ReturnTotal => _votes[Votes.Return].Total(this);
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
    private Portal _missionPortal;
    private readonly Dictionary<int, PlayerShip> _ships = new();
    private Camera2D _cam;

    // ── camera ──────────────────────────────────────────────────────────────
    // The wheel zooms, from 33% further out than the default to 1.5x closer. Y frees
    // the camera: arrow keys, or the mouse against a screen edge, move it -- no
    // further than the ship's CameraRange (5000 for capital ships). Y again returns
    // it to the ship, keeping the zoom.
    // ZoomOutMax is how much FURTHER out than the default the wheel will go: 15% more of it than
    // it was (1.33), because a boss's reach grows with the level and a fight you cannot see the
    // edges of is a fight fought on the minimap. The wheel never goes past this ceiling -- a boss
    // too big for it is framed by MOVING the camera, not by raising it (BossReach, below).
    public const float DefaultZoom = 0.9f, ZoomOutMax = 1.53f, ZoomInMax = 1.5f;
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
    // THE THINGS THIS PILOT HAS PICKED, in the order they were picked. One source of truth: what
    // used to be a single `_selected` is the FIRST of this list, so everything that asks for "the
    // target" keeps working and nothing has to be kept in step with anything else. How many it may
    // hold is the class's own row (ClassDef.Targets).
    private readonly List<IHittable> _targets = new();
    public IHittable Selected => _targets.FirstOrDefault(t => t != null && t.Alive);
    public IReadOnlyList<IHittable> Targets => _targets;
    public int TargetCap => MyShip is { } me && Classes.Known(me.Class) ? Classes.Of(me.Class).Targets : 1;
    private readonly List<(Vector2 a, Vector2 b, Color c, double t)> _flashes = new();
    public IReadOnlyList<(Vector2 a, Vector2 b, Color c, double t)> Flashes => _flashes;

    public override void _Ready()
    {
        Ui.Install(GetTree());                                  // the game-wide look
        // THE SKY, behind everything in the world: a layer per row of Sky.All, each taking a
        // different share of the camera's motion so that flying reads as flying. Purely local --
        // nothing about it is networked, and no two peers need agree on it.
        var sky = new CanvasLayer { Layer = -100, Name = "Stars" }; AddChild(sky);
        Sky.Build(sky);

        I = this;
        _world = Session.NextWorld();
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
        AddChild(new Popups { Name = "Popups" });
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
        layer.AddChild(new QuarryBar { Hub = this });               // shows itself in the arena
        layer.AddChild(new ReturnButton { Hub = this });            // RETURN TO BASE, after a win only
        layer.AddChild(new Radar { Hub = this });
        layer.AddChild(new AbilityBar { Hub = this });
        if (!InArena) layer.AddChild(new HaulerHud { Hub = this });
        Hints = new Hints { Hub = this }; AddChild(Hints);
        // THE SOFT TUTORIAL, on a first character only: a card at a time with CONTINUE, and SKIP
        // in the top right. It offers itself and removes itself; a pilot that has met anything at
        // all, or been through it before, never sees it.
        var tour = new Tour { Hub = this }; AddChild(tour); tour.StartIfNew();

        Settings.EnsureLoaded();       // ability key bindings
        Combat.Clear();
        Hooks();

        // THE SHIPS BEFORE THE BOSS. The boss sizes itself to the party in its _Ready, and it used
        // to be built first, count zero ships, and come out a solo boss for every party.
        _guestBefore = !Net.IsHost;
        RebuildShips();
        if (InArena) BuildArena();                   // registered as a target AFTER Combat.Clear
        else if (Raids.Pending > 0 && Net.IsHost) _raids.Owed(Raids.Pending);
        Raids.Pending = 0;
        // THE PRACTICE RANGE lives at home: one target per row of PracticeTargets, and nothing about
        // any one of them here. A fighter's node is named for its place among the fighters
        // (PracticeFighter1, 2, ...), a hulk's for its number (TargetDummy1, 3).
        int fighters = 0;
        foreach (var (n, at, armed, fighter) in InArena ? System.Array.Empty<(int, Vector2, bool, bool)>() : PracticeTargets)
        {
            var d = new TargetDummy { Name = fighter ? $"PracticeFighter{++fighters}" : $"TargetDummy{n}", Number = n, Armed = armed, Fighter = fighter, Position = at, ZIndex = 3 };
            AddChild(d); _dummies.Add(d);
            Combat.Hostiles.Add(d);
            d.Published = (last, avg, total) => ToWorld(nameof(NetDummy), n, last, avg, total);
        }

        if (Net.I != null)
        {
            Net.I.PlayerJoined   += OnPlayerJoined;
            Net.I.PlayerLeft     += OnPlayerLeft;
            Net.I.SessionChanged += OnSessionChanged;
            Net.I.Admitted       += OnAdmitted;
            ReportSector();                                      // this world is loaded: tell the host where we are
        }
        Hints.Meet("flight");
    }

    // Net is an autoload and outlives this scene. Every handler added in _Ready comes
    // off here, or the next join or status line calls into a freed Hub.
    // EVERY HOOK THIS WORLD INSTALLS, in one place. Combat.Clear drops them all on the way out
    // (each is a lambda holding this node), so there has to be exactly one place that puts them
    // back -- the harness used to restore them by hand from a list it kept, and every hook added
    // after that list was written was silently lost for the rest of the run.
    private void Hooks()
    {
        // Flashes are made on the host, where the shots happen. Guests are sent them,
        // or a guest firing at the dummy would see nothing at all.
        Combat.OnFlash = (a, b, beam) =>
        {
            AddFlash(a, b, beam);
            ToWorld(nameof(NetFlash), a, b, beam);
        };
        // Shells and torpedoes are born in this world; the host's copy deals damage, and guests
        // get the launch and fly a cosmetic copy (the run is straight and steady, so it lands in
        // the same place).
        // EFFECTS GO UP EVERYWHERE. The host raises one and every guest gets it, exactly as a
        // flash does -- an effect added straight to this tree was one only the host ever saw.
        Fx.On = r =>
        {
            AddFx(r);
            ToWorld(nameof(NetFx), r.Id, r.At, r.To, r.Size, r.Time, r.Hold, r.Since, r.Anchor, r.Cue ?? "", r.Strike ?? "");
        };
        Combat.World = this;
        Combat.ShotFired = s => ToWorld(nameof(NetShot), s.Kind, s.Position, s.Dir, s.Speed, s.Range,
                                        s.Radius, s.TargetId, s.TurnRate, s.NetId, s.Size, s.Variant);
    }

    public override void _ExitTree()
    {
        if (Net.I != null)
        {
            Net.I.PlayerJoined   -= OnPlayerJoined;
            Net.I.PlayerLeft     -= OnPlayerLeft;
            Net.I.SessionChanged -= OnSessionChanged;
            Net.I.Admitted       -= OnAdmitted;
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
        var neb = Assets.Load<Texture2D>("res://nebula.png");
        AddChild(new Sprite2D { Texture = neb, Position = new Vector2(0, -200), Scale = new Vector2(7, 7),
                                Modulate = new Color(0.30f, 0.42f, 0.72f, 0.28f), ZIndex = -10 });

        AddChild(new Sun { Position = SunPos });

        var tex = new[] { "res://asteroid_1.png", "res://asteroid_2.png", "res://asteroid_3.png" };
        for (int i = 0; i < BeltRocks; i++)
        {
            float a = Mathf.Tau * i / BeltRocks;
            var r = new Sprite2D {
                Texture = Assets.Load<Texture2D>(tex[i % 3]),
                Position = SunPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * BeltR,
                Scale = new Vector2(0.5f, 0.5f), ZIndex = 1 };
            AddChild(r); _rocks.Add(r);
        }

        // the wreck: its edge 1500 u from the base, like the belt's
        AddChild(new Sprite2D { Texture = Assets.Load<Texture2D>("res://behemoth_wreck.png"),
                                Position = WreckPos, Scale = new Vector2(0.34f, 0.34f), ZIndex = 1 });

        // THE BUILDINGS, one row each (Landmarks.cs): the base, the TIO, the equipment base and the
        // four outposts. They are permanent, and nothing here is replicated: every peer builds the
        // same ones. A name is drawn in the world like every world label -- a UI control would count
        // as "off-screen" whenever the camera looked elsewhere. A row with no Art (the portal, the
        // field, the belt) is built by this method itself, around this loop.
        foreach (var l in Landmarks.All)
        {
            if (l.Art == null) continue;
            var art = l.Art(l);
            art.Name = l.NodeName; art.Position = l.At; art.ZIndex = 2;
            AddChild(art);
            if (l.Label != null) AddChild(new WorldLabel { Text = l.Label, Position = l.At + new Vector2(0, l.Half.Y + 16), ZIndex = 2 });
        }
        AddChild(new BaseDefense());                               // the base's own laser and missiles
        AddChild(new MissionBar { Hub = this, ZIndex = 6 });
        // THE LANES BETWEEN THEM AND THE BASE -- their couriers and their guns, by row (Lanes.cs).
        // Permanent and deterministic like the outposts themselves, so nothing here is replicated
        // either: what IS decided about a lane is whether it is CUT, and every peer works that out
        // from the raiders it already holds.
        Lanes.Build(this);

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
    public static SectorKind? PeerSector(int id) => Session.SectorOf(id);
    // A session over (offline, a guest now, the main menu): which world each guest was in, and the
    // places held for dropped pilots, go with it.
    // ── A SHIP'S STATE, BY WAY OF THE HUB ─────────────────────────────────────
    // The unreliable updates for a ship are sent to the hub, which is at the same path on every
    // peer, and not to the ship's node, which is not: a pilot's first updates can overtake Godot's
    // (reliable) word that it joined, and reached another guest with no ship for it yet -- "Node
    // not found" in that guest's log (seen over a lossy link). The hub hands each to its ship, or
    // drops it. The owner's report goes to ITS OWN ship only: nobody moves another's.
    // A GUEST'S REPORT GOES TO THE HOST, which passes it on (Net.ToHeard): broadcast, it was passed on
    // by Godot to every guest at once -- a guest just let in included, before it could take it.
    public PlayerShip ShipOf(int peer) => _ships.TryGetValue(peer, out var s) && IsInstanceValid(s) ? s : null;
    public void SendShipState(float px, float py, float vx, float vy, float rot, float ax, float ay, bool trigger, bool staggered,
                              float podX, float podY, float podRot, bool warping)
    {
        var report = new Variant[] { Net.LocalId, px, py, vx, vy, rot, ax, ay, trigger, staggered, podX, podY, podRot, warping };
        if (Net.IsHost) Net.ToHeard(0, this, nameof(NetShipState), report);
        else Net.AskHost(this, nameof(NetShipState), report);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Ships)]
    private void NetShipState(int owner, float px, float py, float vx, float vy, float rot, float ax, float ay, bool trigger, bool staggered,
                              float podX, float podY, float podRot, bool warping)
    {
        // on the host, a pilot's own report of its own ship; on a guest, only what the host passes on
        if (Net.IsHost ? !Net.FromPlayer(this, out int who) || who != owner : Net.SenderOf(this) != 1) return;
        ShipOf(owner)?.ApplyState(px, py, vx, vy, rot, ax, ay, trigger, staggered, podX, podY, podRot, warping);
        Net.ToHeard(owner, this, nameof(NetShipState), owner, px, py, vx, vy, rot, ax, ay, trigger, staggered, podX, podY, podRot, warping);
    }
    public void SendHostState(int owner, double hp, double maxHp, bool alive, double stasis, int statusBits, double combat,
                              float[] slotLeft, float[] slotCool, float[] slotOwn, int[] slotN,
                              int wingTarget, int strikeTarget, Vector2[] wingPos, float[] wingRot, int[] wingState, float[] wingRearm) =>
        Net.ToHeard(0, this, nameof(NetHostState), owner, hp, maxHp, alive, stasis, statusBits, combat, slotLeft, slotCool, slotOwn, slotN,
                    wingTarget, strikeTarget, wingPos, wingRot, wingState, wingRearm);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.HostShips)]
    private void NetHostState(int owner, double hp, double maxHp, bool alive, double stasis, int statusBits, double combat,
                              float[] slotLeft, float[] slotCool, float[] slotOwn, int[] slotN,
                              int wingTarget, int strikeTarget, Vector2[] wingPos, float[] wingRot, int[] wingState, float[] wingRearm) =>
        ShipOf(owner)?.ApplyHostState(hp, maxHp, alive, stasis, statusBits, combat, slotLeft, slotCool, slotOwn, slotN,
                                      wingTarget, strikeTarget, wingPos, wingRot, wingState, wingRearm);
    public void SendShield(int owner, float side) => Net.ToHeard(0, this, nameof(NetShield), owner, side);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Shields)]
    private void NetShield(int owner, float side) => ShipOf(owner)?.ApplyShield(side);

    // A WORLD'S OWN NODES REPORT BY WAY OF THE HUB TOO. The base exists only at home and the boss only
    // in the arena, and an unreliable report can land after its world has gone: the base's last report
    // before the party left for the arena reached a guest already there -- "Node not found: Hub/Yard"
    // -- once each stream had a channel of its own and no newer report on a shared one threw the late
    // one away. The hub is at the same path in every world, so it takes each and hands it on, or drops
    // it. Reliable reports need none of this: they queue behind the word that moves the world.
    public void SendBase(Vector2[] gp, float[] gr, int[] gs, float[] gc, Vector2[] gb, float[] gh, float[] gw, int[] ga,
                         Vector2 hp, float hr, int hs, float ht, float hc, float sale, float hh, float hw, int hf, float hstop) =>
        RpcHome(this, nameof(NetBase), gp, gr, gs, gc, gb, gh, gw, ga, hp, hr, hs, ht, hc, sale, hh, hw, hf, hstop);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Base)]
    private void NetBase(Vector2[] gp, float[] gr, int[] gs, float[] gc, Vector2[] gb, float[] gh, float[] gw, int[] ga,
                         Vector2 hp, float hr, int hs, float ht, float hc, float sale, float hh, float hw, int hf, float hstop)
    {
        if (IsInstanceValid(Yard)) Yard.TakeState(gp, gr, gs, gc, gb, gh, gw, ga, hp, hr, hs, ht, hc, sale, hh, hw, hf, hstop);
    }
    public void SendBoss(Vector2 p, float rot, double hp, bool locked, double nextSuper, double superGap, double hullMult) =>
        RpcToSector(SectorKind.Arena, this, nameof(NetBoss), p, rot, hp, locked, nextSuper, superGap, hullMult);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Boss)]
    private void NetBoss(Vector2 p, float rot, double hp, bool locked, double nextSuper, double superGap, double hullMult)
    {
        if (IsInstanceValid(Boss)) Boss.TakeState(p, rot, hp, locked, nextSuper, superGap, hullMult);
    }
    // a special move's sound, for the guests in the arena (Boss.Sound)
    public void SendBossSound(string name, Vector2 at) => RpcToSector(SectorKind.Arena, this, nameof(NetBossSound), name, at);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.BossSounds)]
    private void NetBossSound(string name, Vector2 at)
    {
        if (InArena) Sfx.Special(name, at);
    }

    public static void EndSession() => Session.End();
    // Send only to the peers that are IN a given sector. A peer still loading that world has no
    // such node yet, and Godot logs "Node not found ... Invalid packet received" for every stray
    // packet -- it is not fatal, but it is noise that hides real errors, and that peer misses the
    // state anyway. The yard has needed this since the first arena run; the BOSS needs the same,
    // because the host starts broadcasting its state before every guest has built the arena.
    public void RpcToSector(SectorKind k, Node node, StringName method, params Variant[] args)
    {
        foreach (var id in Multiplayer.GetPeers())
            if (Session.SectorOf(id) == k) node.RpcId(id, method, args);
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
    private void NetMySector(int s, int trip)
    {
        if (!Net.FromPlayer(this, out int who)) return;
        // A STALE REPORT IS IGNORED (audit P9): one sent just before its world heard NetSector lands
        // in the host's NEW world claiming the old one, and recording it sent this pilot none of the
        // world it was about to be in. A trip is the host's count of sector moves (Session.Trip);
        // -1 is a pilot that has heard none yet, which is always news.
        if (trip >= 0 && trip != Session.Trip) return;
        Session.Sectors[who] = (SectorKind)s;
        if ((SectorKind)s != Sector) { RpcId(who, nameof(NetSector), (int)Sector, Missions.Kind, Missions.Level, Session.Trip); return; }
        // The place a pilot is owed goes first and is never metered: a reconnecting pilot's held
        // spot is delivered by whichever report first finds it in the host's world.
        if (_placeFor.Remove(who, out var owed)) { RpcId(who, nameof(NetPlace), owed.at, owed.rot); ShipOf(who)?.Relocated(); }
        // THE CATCH-UP, and only it, is rate-limited -- one ask, dozens of reliable packets. Half a
        // second: the guest's own repeats are at 1 s and 3 s (ReportSector). Never the FIRST report
        // this world has from a pilot (audit P9): it is the only way that pilot learns of what was
        // spawned before it arrived, and a meter stamped by its previous world's report skipped it.
        bool first = _caughtUp.Add(who);
        if (!Net.Metered(who, nameof(NetMySector), 0.5) && !first) return;
        RpcId(who, nameof(NetMission), MissionArgs());
        foreach (var v in _votes) RpcId(who, nameof(NetVote), v.Wire(this));
        // EVERY HOST-SPAWNED THING IN THIS WORLD, of every kind, down the one path. This was two
        // hand-written loops, one per kind, each spelling out that kind's wire arguments -- so a
        // third kind meant a third loop here as well as a third set of six methods.
        for (int k = 0; k < Spawns.All.Count; k++)
            if (Set(k) is { } set)
                foreach (var n in set.Nodes)
                {
                    var seed = set.Kind.Seed(n);
                    RpcId(who, nameof(NetSpawn), k, seed.NetId, seed.At, seed.N, seed.A, seed.B);
                }
        // ...AND WHATEVER ELSE THIS MISSION OWES A LATE ARRIVAL, from its row: a boss's warnings
        // and the rock in its tractor. A raid owes nothing here -- its hulls came down the loop
        // above, and its shield is worked out from them on every peer alike.
        if (Sector == SectorKind.Arena) Missions.KindOf(Missions.Kind).CatchUp?.Invoke(this, who);
        if (MissionWon) RpcId(who, nameof(NetWon), _ships.Count);
    }
    private void ReportSector() { if (!Net.IsHost) Net.AskHost(this, nameof(NetMySector), (int)Sector, Session.HeardTrip); }
    private readonly HashSet<int> _caughtUp = new();           // the pilots this world has sent its catch-up

    private bool _guestBefore;
    private void OnSessionChanged()
    {
        // Raiders belong to whoever simulates them. Becoming a guest, the ones this world had
        // would sit frozen (nothing simulates them any more); leaving a host, its copies would come
        // alive in your own world and attack your base. Either way they go.
        if (_guestBefore || !Net.IsHost) Set(Spawns.Raider)?.DropAll(this, burst: false);
        bool wasGuest = _guestBefore;
        _guestBefore = !Net.IsHost;
        // The session over (or now another's): who is held, who is READY and -- for a guest that was
        // one -- the mission all belonged to it. Kept, a held pilot kept a solo world's portal shut,
        // and a guest found the host's portal still open in its own world. (The host's LEVEL stays
        // until this pilot docks at the TIO, which picks its own again: READY is pressed there.)
        if (!(Net.IsHost && Net.IsOnline)) { EndSession(); _away.Clear(); foreach (var v in _votes) v.Clear(); }
        if (wasGuest && Net.IsHost) { Mission = MissionState.Idle; MissionT = 0; }
        // the host gone while the party is in the arena: home, to your own base
        if (InArena && !Net.IsOnline) { GoTo(SectorKind.Home); return; }
        Yard?.OnSessionChanged(!Net.IsHost);    // parks or restores your own yard (home only)
        RebuildShips();
    }

    // A GUEST THE HOST HAS LET IN introduces itself -- where it is, and who (Net.Admitted). The host
    // and the guests already here introduce themselves to it from OnPlayerJoined.
    private void OnAdmitted()
    {
        ReportSector();
        SendIdentity();
    }

    // A REBUILD IS NOT A ROSTER CHANGE: every ship goes and comes straight back. Reporting each
    // arrival would broadcast the party once per pilot, and at world build that lands on peers
    // still loading a world of their own ("Node not found"). Every peer reports its world when it
    // has one, and that report is what brings it up to date (NetMySector).
    private bool _rebuilding;
    private void RebuildShips()
    {
        _rebuilding = true;
        foreach (var s in _ships.Values)
            if (IsInstanceValid(s)) { RemoveChild(s); s.QueueFree(); }   // out now, so the name frees up
        _ships.Clear();

        SpawnFor(Net.LocalId);          // your own ship, always
        if (Net.I != null)
            foreach (var id in Net.I.Players.Keys) SpawnFor(id);
        _rebuilding = false;
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
        RosterChanged();
    }

    // ── A DROPPED PILOT'S PLACE (host) ───────────────────────────────────────
    // THE BOOK IS Session (Session.Places, Session.Held, Session.HoldFor): a held place has to
    // outlive this world, because the host reloads its own scene on every trip and the pilot it
    // waits for may only come back into the next one. What is left here is the per-world glue --
    // which world this is, who is away, and whose place is waiting to be delivered.
    private int _world;                                         // this world, of all the host has built
    private readonly Dictionary<int, string> _away = new();     // the held pilots, by their old peer id: the party's absent members
    private readonly Dictionary<int, (Vector2 at, float rot)> _placeFor = new();

    private void OnPlayerLeft(int peer, Net.PlayerInfo info, bool onPurpose)
    {
        if (Net.IsHost && Net.IsOnline && info.CharacterId.Length > 0)
        {
            var h = onPurpose ? null : Session.Hold(peer, info, _world, ShipOf(peer));
            // a kill in the moments before the host noticed the drop: owed too (paid once, by serial)
            if (h != null) h.Owed.AddRange(Session.Owed(peer, _world));
            if (h != null) Session.Places[info.CharacterId] = h;
            else foreach (var v in _votes) v.Forget(peer);
        }
        Session.Sectors.Remove(peer);
        DespawnFor(peer);                        // which reports the roster, the ship already gone
        if (_replacing.Remove(peer, out var heir)) TryRestoreHold(heir);   // its place, to the pilot back with its token
    }
    private void TryRestoreHold(int peer)
    {
        if (!Net.IsHost || !Net.I.Players.TryGetValue(peer, out var p) || p.CharacterId.Length == 0) return;
        if (!Session.Places.Remove(p.CharacterId, out var h) || !_ships.ContainsKey(peer)) { if (h != null) Session.Places[p.CharacterId] = h; return; }
        RestoreHeld(h, peer);
        RosterChanged();
    }
    private void RestoreHeld(Session.Held h, int peer)
    {
        foreach (var v in _votes) v.Rekey(h.OldPeer, peer);
        // ITS TURRETS ARE ITS OWN AGAIN (audit P10): keyed to the old id, the returning pilot could
        // not collect them, was not held to its cap, and they fired a spare sheet. Each finds its
        // ship again by its owner (DeployedTurret.Ship).
        foreach (var t in Deployed) if (t.OwnerId == h.OldPeer) { t.OwnerId = peer; t.Ship = null; }
        if (h.World == _world && _ships.TryGetValue(peer, out var s) && IsInstanceValid(s))
        {
            s.Restore(h.Hp, h.Alive, h.Stasis);
            if (PeerSector(peer) == Sector) { RpcId(peer, nameof(NetPlace), h.Pos, h.Rot); s.Relocated(); }   // never a jump (Drives)
            else _placeFor[peer] = (h.Pos, h.Rot);
        }
        foreach (var k in h.Owed) PayKill(k, h.OldPeer, peer);          // what it missed (a kill it did get is not paid again)
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetPlace(Vector2 at, float rot) { if (MyShip is { } me) me.Place(at, rot); }
    private void ExpireHolds()
    {
        var gone = Session.Lapsed();
        if (gone.Count == 0) return;
        foreach (var id in gone)
        {
            foreach (var v in _votes) v.Forget(Session.Places[id].OldPeer);
            Session.Places.Remove(id);
        }
        RosterChanged();
    }
    private void RefreshAway()
    {
        _away.Clear();
        foreach (var h in Session.Places.Values) _away[h.OldPeer] = h.Name;
    }

    // ── THE PARTY CHANGED ────────────────────────────────────────────────────
    // ONE OPERATION, called from the two places that own `_ships` (SpawnFor, DespawnFor) and from
    // the two that own a held place (TryRestoreHold, ExpireHolds). Everything decided by WHO IS IN
    // THE PARTY is decided here: the absent list, the report every peer's READY and RETURN buttons
    // are drawn from, and the RETURN gate. REPLACED: three hand-written `RefreshAway();
    // BroadcastMission();` pairs, none of which asked the gate -- which was asked only when a
    // RETURN button was pressed, so the last un-returned pilot dropping out of a WON arena left the
    // party sitting in it until somebody toggled the button.
    private void RosterChanged()
    {
        if (!Net.IsHost || _rebuilding) return;
        RefreshAway();
        BroadcastMission();
        BroadcastVotes();       // who counts has changed: every tally goes out, and anything it now carries happens
    }

    private void DespawnFor(int peerId)
    {
        if (!_ships.TryGetValue(peerId, out var s)) return;
        _ships.Remove(peerId);
        if (IsInstanceValid(s)) { RemoveChild(s); s.QueueFree(); }
        RosterChanged();
    }

    // ── identity ─────────────────────────────────────────────────────────────
    // Identity is owner-announced (see Character). It rides on the Hub rather than
    // the ship because the Hub exists on every peer before any ship does, so an
    // announcement can never arrive at a node that is not there yet.
    private void ApplyLocalIdentity()
    {
        if (_ships.TryGetValue(Net.LocalId, out var me) && IsInstanceValid(me)) ApplyIdentity(me);
    }

    // THE ONE WAY A SHIP GETS ITS PILOT: name, colours, class, the pilot's upgrades, gear and gear levels --
    // your own from Character, anyone else's from what they announced.
    private void ApplyIdentity(PlayerShip s)
    {
        if (s.OwnerId == Net.LocalId)
        {
            s.SetIdentity(Character.Name, Character.Main, Character.Accent, Character.Class);
            s.SetProgress(Character.Bought, Character.Peak);
            s.SetEquipment(Character.LoadoutFor(Character.Class), Character.GearLevel);
        }
        else if (Net.I != null && Net.I.Players.TryGetValue(s.OwnerId, out var p) && p.HasIdentity)
        {
            s.SetIdentity(p.Name, p.Main, p.Accent, p.Class);
            s.SetProgress(p.Bought, p.Peak);
            s.SetEquipment(p.Equip, p.GearLevel);
        }
    }

    private void SendIdentity(int toPeer = 0)
    {
        if (!Net.IsOnline) return;
        // THE LEVELS GO WITH THE GEAR, as two lists in step (one dictionary's keys and values, which
        // enumerate in the same order): every peer lifts this pilot's parts by this pilot's levels.
        var args = new Variant[] { Net.LocalId, Character.Name, Character.Main, Character.Accent, (int)Character.Class, Character.Bought, Character.Level, Character.Peak,
                                   Character.LoadoutFor(Character.Class), Character.GearLevel.Keys.ToArray(), Character.GearLevel.Values.ToArray(), Character.Id,
                                   "" };
        // THE REJOIN TOKEN GOES TO THE HOST ALONE (P10b). Every other peer hears the same identity with
        // no token: a co-player that learnt it could claim this pilot's place (Session.MayClaim).
        foreach (int to in toPeer != 0 ? new[] { toPeer } : Multiplayer.GetPeers())
        {
            args[^1] = to == 1 && !Net.IsHost ? Session.Rejoin : "";
            RpcId(to, nameof(NetIdentity), args);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetIdentity(int peer, string name, Color main, Color accent, int cls, int[] bought, int level, int peak, string[] equip,
                             string[] gearIds, int[] gearLevels, string characterId, string token)
    {
        // a peer may only describe itself
        if (Net.SenderOf(this) != peer || Net.I == null) return;
        if (!Net.I.Players.TryGetValue(peer, out var p)) Net.I.Players[peer] = p = new Net.PlayerInfo();
        // Everything else off this wire is sanitised (bought is null-guarded below, equip inside
        // Equipment.Sanitize, the gear levels by Equipment.SanitizeLevels); the name was not. A null
        // here threw on .Length, and a null that got past became a null Pilot in every label that draws it.
        name = string.IsNullOrEmpty(name) ? Character.Defaults.Name : name.Length > 24 ? name[..24] : name;
        // Purchases are held to what the claimed level could have paid for, and to what this host
        // lets any claim spend (Progression.MaxSpendLevel). It used to weigh the RAW wire level
        // against the claim and refuse the lot on a mismatch, so a peer claiming two billion
        // bought every row maxed, and an honest pilot past level 100 arrived as a stock hull.
        bought = Progression.Afford(bought, level);
        characterId ??= "";
        if (characterId.Length > 64) characterId = "";
        p.Token = token ?? "";
        // A HELD PLACE IS NOT CLAIMABLE BY NAME. The character id is the guest's own word, so a
        // peer announcing the id of a pilot ALREADY in the party took its place the moment that
        // pilot dropped: its party slot, its READY, its position, and every kill owed to it. The
        // place is the REJOIN TOKEN's (Session.MayClaim): an id another live peer is flying
        // belongs to that peer unless this one holds its token -- the same pilot back before the
        // host saw its old connection die, which is then let go and its place handed over (P10b).
        int holder = characterId.Length == 0 ? 0 : Net.I.Players.FirstOrDefault(kv => kv.Key != peer && kv.Value.CharacterId == characterId).Key;
        if (characterId.Length > 0 && !Session.MayClaim(characterId, token ?? "", holder != 0)) characterId = "";
        else if (holder != 0 && Net.IsHost) { _replacing[holder] = peer; Net.I.Hang(holder); }
        var cship = _ships.TryGetValue(peer, out var cs) && IsInstanceValid(cs) ? cs : null;
        var klass = Refit(p.HasIdentity ? p.Class : null, Classes.Sanitize(cls), InArena, cship?.InCombat == true);
        (p.Name, p.Main, p.Accent, p.Class, p.Bought, p.Equip, p.CharacterId, p.HasIdentity) =
            (name, main, accent, klass, bought, equip ?? System.Array.Empty<string>(), characterId, true);
        // ITS OWN LEVELS, held to the ladder and to parts that exist, like the loadout: what this
        // peer's copy of its ship is lifted by. Never this host's -- those are the host's pilot's.
        p.GearLevel = Equipment.SanitizeLevels((gearIds ?? System.Array.Empty<string>()).Zip(gearLevels ?? System.Array.Empty<int>()));
        p.Level = System.Math.Max(1, level);                    // a claim; what it may SPEND is capped above
        p.Peak = Progression.Claim(System.Math.Max(peak, level));   // ...and what it OPENS (Unlocks), by the same cap
        if (_ships.TryGetValue(peer, out var s) && IsInstanceValid(s)) ApplyIdentity(s);
        if (Net.IsHost && characterId.Length > 0 && holder == 0) RpcId(peer, nameof(NetToken), Session.TokenFor(characterId));
        TryRestoreHold(peer);
    }
    // the pilot's rejoin token, from the host that issued it (Session.Rejoin)
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetToken(string token) { if (!Net.IsHost) Session.Rejoin = token ?? ""; }
    private readonly Dictionary<int, int> _replacing = new();  // an old connection let go -> the peer its place goes to

    // A CLASS IS CHANGED AT REFIT, never in a fight (audit P8): RefitOpen is the one rule, read by the
    // host for every announcement (Refit) and by the pilot's own REFIT, class picker and base menu
    // (MayRefit), so an honest pilot never flies a class the host refused. A peer announcing another
    // class in the arena, or while its ship is in combat, keeps the one it had; a first announcement
    // (had == null) is always taken. A change taken keeps the hull's fraction and every cooldown
    // (PlayerShip.FitClass), so announcing B and then A heals nothing and resets nothing.
    public static bool RefitOpen(bool arena, bool inCombat) => !arena && !inCombat;
    public static ShipClass Refit(ShipClass? had, ShipClass want, bool arena, bool inCombat) =>
        had is { } h && h != want && !RefitOpen(arena, inCombat) ? h : want;

    // ── missions: Threat Intelligence Operations (host-authoritative) ────────
    public enum MissionState { Idle, Opening, PortalOpen }
    public MissionState Mission { get; private set; }
    public double MissionT { get; private set; }
    public const double PortalOpenTime = 3.0;
    // the mission portal opens off the TIO's top-right corner
    // From constants, not the TIO's sprite: in the arena, or in the frame a scene is being
    // swapped, there is no sprite -- and asking for this crashed (found by the arena run).
    public const float TioHalfWidth = TioHeight * 630f / 876f / 2f;      // the art is 630 x 876 px (and the TIO's footprint: Landmarks)
    public Vector2 MissionPortalPos => TioPos + new Vector2(TioHalfWidth + 110f, -TioHeight / 2f - 70f);
    // the party is everyone in the session, and every pilot whose place is held (reconnecting):
    // one who was not READY keeps the portal shut until it is back, or its place lapses
    // (Votes.All["launch"].Voters is this list, so the rule is written once)
    public IEnumerable<int> PartyIds => _ships.Keys.Concat(_away.Keys);
    public bool IsReady(int id) => _votes[Votes.Launch].Says(id);
    public bool AllReady => _votes[Votes.Launch].Carried(this);
    public string PilotName(int id) => _ships.TryGetValue(id, out var s) && IsInstanceValid(s) ? s.Pilot
                                     : _away.TryGetValue(id, out var n) ? $"{n} (reconnecting)" : $"pilot {id}";
    public bool TioOpen => SideIs<TioWindow>();

    public void OpenTio()
    {
        if (SideIs<TioWindow>()) return;
        Hints.Meet("tio");
        if (Net.IsHost && Mission == MissionState.Idle)
        {   // docking at the TIO selects the newest unlocked level of THIS operation's own ladder
            Missions.Level = Missions.Unlocked(Missions.Kind);
            BroadcastMission();
        }
        ToggleSide(() => new TioWindow { Hub = this });
    }

    // ── ONE PRESS, ANY VOTE (Votes.cs) ──────────────────────────────────────
    // Two faces onto one mechanic. What each press DOES besides counting, whose agreement it
    // needs, when it may be cast and what carrying it does are the row's, not this code's.
    public void SetMyReady(bool ready) => SetMyVote(Votes.Launch, ready);
    public void SetMyReturn(bool on) => SetMyVote(Votes.Return, on);
    public void SetMyVote(int vote, bool on)
    {
        if (vote < 0 || vote >= _votes.Length) return;
        var v = _votes[vote];
        if (!v.Def.Open(this)) return;
        v.Def.Cast?.Invoke(this, on);
        if (Net.IsHost) { v.Set(Net.LocalId, on); BroadcastVotes(); }
        else { v.Mark(on); Net.AskHost(this, nameof(RequestVote), vote, on); }
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestVote(int vote, bool on)
    {
        if (!Net.FromPlayer(this, out int who) || !_ships.ContainsKey(who)) return;   // only a pilot in the session
        if (vote < 0 || vote >= _votes.Length || !_votes[vote].Def.Open(this)) return;
        _votes[vote].Set(who, on); BroadcastVotes();
    }
    // host: every tally to the session -- as the mission packet the READY tally used to ride in
    // goes -- and then act on whichever have carried.
    private void BroadcastVotes()
    {
        if (!Net.IsHost) return;
        if (Net.IsOnline) foreach (var v in _votes) Rpc(nameof(NetVote), v.Wire(this));
        Settle();
    }
    private void Settle()
    {
        if (!Net.IsHost) return;
        foreach (var v in _votes) if (v.Carried(this)) v.Def.Carried?.Invoke(this);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetVote(int vote, int[] ids, int[] said, int ready, int total)
    {
        if (vote >= 0 && vote < _votes.Length) _votes[vote].Apply(ids, said, ready, total);
    }

    private const float PortalEnterRadius = 190f;

    // ── moving the party between sectors (host decides; every peer follows) ──
    public void EnterSector(SectorKind k)
    {
        if (!Net.IsHost) return;
        if (k == SectorKind.Arena) Yard?.SaveForTrip();
        Session.Trip++;
        if (Net.IsOnline) Rpc(nameof(NetSector), (int)k, Missions.Kind, Missions.Level, Session.Trip);
        // NOBODY IS IN A SECTOR WHILE THEY ARE MOVING BETWEEN THEM. Every peer was left recorded
        // in the world it is LEAVING until its new world reported itself, so for a round trip plus
        // a scene load the host went on sending that world's traffic to peers that had already
        // gone: the yard's 10 Hz state reaching an arena, where there is no Yard node to deliver it
        // to ("Node not found: Hub/Yard", seen in one run of three). Forgetting them sends nothing
        // until each one reports, and its report is also what brings it up to date (NetMySector).
        foreach (var id in Multiplayer.GetPeers()) Session.Sectors.Remove(id);
        GoTo(k);
    }
    // With the KIND and the level: a world is BUILT from both (the arena builds what the mission
    // row says, at the level's scale), so a peer must have them BEFORE the scene changes, not in
    // the mission report that follows. The KIND FIRST: a category owns its own ladder, so the
    // level being set has to land on the ladder the host meant.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    // A TRIP ALREADY MADE IS NOT MADE AGAIN (audit P9): the host answers a stale report with the
    // world this pilot is already in, and reloading it rebuilt the arena without the spawns its
    // first report had caught up on.
    private void NetSector(int k, int kind, int level, int trip)
    {
        if (trip == Session.HeardTrip) return;
        Session.HeardTrip = trip;
        Missions.Kind = kind; Missions.Level = level; GoTo((SectorKind)k);
    }
    private void GoTo(SectorKind k)
    {
        Sector = k;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Hub.tscn");
    }

    // ── the arena: what the mission built, and how it ends ───────────────────
    // WHAT A MISSION IS is a row of Missions.Kinds. This reads the row and nothing else: a third
    // kind is a row, not a branch here. (It was `new Boss()` spelled out, because a bounty was the
    // only thing a mission had ever been able to be.)
    private void BuildArena() => Missions.KindOf(Missions.Kind).Build?.Invoke(this);
    // The BOUNTY's own build, reached from its row: the level's boss (Missions.ForLevel), the same
    // one on every peer.
    public void BuildBoss()
    {
        var type = Missions.ForLevel(Missions.Level);
        Boss = new Boss { Hub = this, Type = type,                     // facing the party, its nose on the spot
                          Position = ArenaCentre + Vector2.Up * (type.Length * 0.5f), Rotation = Mathf.Pi };
        AddChild(Boss);
    }
    // host: an emplacement is down. It goes the way every host-spawned thing goes; if it was the
    // one the mission is WON by killing, that is the mission over. Which it is, is the row's
    // (Missions.Kinds[].Quarry) -- a pylon falls and nothing happens, and nothing here says pylon.
    public void EmplacementDown(Emplacement e)
    {
        if (!Net.IsHost || e == null) return;
        bool quarry = ReferenceEquals(Quarry, e);
        var at = e.Position;
        Down(Spawns.Emplacement, e, burst: true, 0f);
        if (quarry) MissionCleared(at);
    }

    // host: the mission's quarry is dead -- a boss, or a pirate base. Its adds, its raiders and
    // what it has in the air die with it -- collecting is not fighting. Every pilot gets its own
    // EXP (its level, its first clears) and its share of the bounty, then its own crates. Nothing
    // sends anyone home on a clock: each pilot presses RETURN when it is done. (Was BossDefeated,
    // which was true of the only mission there was.)
    public void MissionCleared(Vector2 at)
    {
        if (!Net.IsHost || MissionWon) return;
        MissionWon = true;
        foreach (var r in Raiders.ToList()) RaiderDown(r);
        // A cruise missile outlives the base that fired it by half a minute, a seeker its boss by
        // seconds: every hostile body with an id comes down on every peer (MissileDown), and none of
        // them counts as shot down (Intercept, not TakeDamage).
        foreach (var body in GetChildren().OfType<Shot>().Where(x => x.HostileFire && x.NetId != 0 && x.Alive).ToList())
        { body.Intercept(); MissileDown(body.NetId); }
        AnnounceClear(Missions.Kind, Missions.Level, at);
        _votes[Votes.Return].Clear();
        ToWorld(nameof(NetWon), _ships.Count);               // the party may now RETURN; how many must
    }
    // Guests see the quarry at zero too (its last hull report went out before the killing blow),
    // and learn how many pilots the RETURN needs. WHAT a guest's copy must be told is the row's: a
    // boss is still standing there and has to be put to zero, a pirate base was removed by
    // Hub.Down and every guest heard it.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetWon(int total)
    {
        MissionWon = true;
        _votes[Votes.Return].Guest(total);
        Missions.KindOf(Missions.Kind).Won?.Invoke(this);
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
        if (!Net.IsHost) return;
        Yard.TripClock += delta;                              // what the base is missing, in game time
        // THE MISSION'S OWN GARRISON -- a siege's waves, a bounty's adds -- on its row's clock (Raids)
        if (!MissionWon) _raids.TickGarrison(delta);
        // the whole party in stasis at once: the mission fails, everyone goes home
        if (!MissionWon && _arenaEndT < 0 && _ships.Count > 0 && _ships.Values.All(s => IsInstanceValid(s) && !s.Alive)) _arenaEndT = 3.0;
        if (_arenaEndT >= 0 && (_arenaEndT -= delta) <= 0)
        {
            _arenaEndT = -1;
            if (!MissionWon) Raids.Pending = Missions.Level;           // failed: the boss sends its raiders after you
            EnterSector(SectorKind.Home);
        }
    }

    // Shells, torpedoes and the missiles shot down go on a reliable channel of their own
    // (NetChannels.Cosmetic), so a lost one never holds up a telegraph or the economy's report.

    // host: a missile was shot down; every guest bursts its copy
    public void MissileDown(int id) => ToWorld(nameof(NetMissileDown), id);
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = NetChannels.Cosmetic)]
    private void NetMissileDown(int id)
    {
        foreach (var t in GetChildren().OfType<Shot>()) if (t.NetId == id) t.Intercept();
    }

    // host: pick a level between 1 and Missions.SkipAhead past the newest unlocked ON THIS
    // OPERATION'S OWN LADDER. The level is per category (Missions.Level), so this never moves the other one.
    public void SelectLevel(int level)
    {
        if (!Net.IsHost || Mission != MissionState.Idle) return;
        Missions.Level = System.Math.Clamp(level, 1, Missions.Top(Missions.Kind));
        BroadcastMission();
    }

    // host: pick which OPERATION the party flies -- a row of Missions.Kinds, in the TIO above the
    // level. Each category keeps the level it was left on; the clamp is there because a category's
    // ladder can only ever shrink by a save being edited, and a selection past its top would then
    // launch a mission that is not unlocked.
    public void SelectMission(int kind)
    {
        if (!Net.IsHost || Mission != MissionState.Idle) return;
        Missions.Kind = System.Math.Clamp(kind, 0, Missions.Kinds.Length - 1);
        Missions.Level = System.Math.Clamp(Missions.Level, 1, Missions.Top(Missions.Kind));
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
        // everyone READY opens the portal; everyone AT the portal goes through. The votes are
        // asked every frame, not only when a button is pressed: a pilot LEAVING can make the rest
        // of the party unanimous, and nothing is pressed then.
        if (Net.IsHost) Settle();
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
    // WHO IS READY IS NOT IN HERE. Every tally is on the one vote packet (NetVote), so the launch
    // READY and the arena RETURN reach a guest by the same route rather than two.
    private Variant[] MissionArgs() =>
        new Variant[] { (int)Mission, MissionT, Missions.Kind, Missions.Level, _away.Keys.ToArray(), _away.Values.ToArray() };
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetMission(int state, double t, int kind, int level, int[] away, string[] awayNames)
    {
        Missions.Kind = kind;                     // the kind FIRST: the level lands on its ladder
        Missions.Level = level;
        Mission = (MissionState)state; MissionT = t;
        _away.Clear(); for (int i = 0; i < away.Length && i < awayNames.Length; i++) _away[away[i]] = awayNames[i] ?? "";
    }

    // ── raids: a failed mission brings the boss's raiders to your base ─────────
    // WHAT arrives is a row of Waves.All; the clock and the ONE builder are Raids.cs. The three
    // faces below are the names the rest of the game and the harness already call, each one line.
    //
    // Where a raid comes in from, and where a hunt forms up: outside the outposts (3000 u). It stays
    // here because it is the MAP'S EDGE -- layout, like BasePos and PortalPos -- and Raider.EdgeSpot
    // reads it as such. The wave's ring radius is this constant, in its row.
    public const float RaidEdge = 4200f;
    private readonly Raids _raids;
    public Hub() { _raids = new Raids(this); }      // this world's director, built before _Ready
    public int SpawnPatrol(Vector2 at, double level = 1) => _raids.Patrol(at, level);
    public void HuntWave(Node2D quarry, int wave) => _raids.Hunt(quarry, wave);
    public void GarrisonWave(Vector2 at, int wave) => _raids.Garrison(Missions.Level, at, wave);
    public void CallOff(Node2D quarry) => _raids.CallOff(quarry);
    public void Blockade(int lane) => _raids.Blockade(lane);

    // ── HOST-SPAWNED, REPLICATED: ONE MECHANIC (Spawned.cs) ──────────────────
    // Raiders and the turrets the freighters leave out were the same six methods written twice --
    // mint an id from a NetIds space, add, tell the world, guard for idempotence, remove with or
    // without a burst. They are two ROWS of Spawns.All now, each with a bag of its own, and a
    // third kind is a third row: nothing below this comment names a kind.
    private readonly List<SpawnSet> _sets = Spawns.All.Select(k => k.Bag(k)).ToList();
    private SpawnSet Set(int kind)
    {
        // a row added to the table after this world was built gets its bag the first time it is asked for
        while (_sets.Count < Spawns.All.Count) _sets.Add(Spawns.All[_sets.Count].Bag(Spawns.All[_sets.Count]));
        return kind >= 0 && kind < _sets.Count ? _sets[kind] : null;
    }
    public IReadOnlyList<Raider> Raiders => ((SpawnSet<Raider>)_sets[Spawns.Raider]).Live;
    public IReadOnlyList<DeployedTurret> Deployed => ((SpawnSet<DeployedTurret>)_sets[Spawns.Turret]).Live;
    public IReadOnlyList<Emplacement> Emplacements => ((SpawnSet<Emplacement>)_sets[Spawns.Emplacement]).Live;

    // host: mint an id from this kind's space, build it, and tell this world
    public Node2D Spawn(int kind, Vector2 at, int n = 0, double a = 1, double b = 1)
    {
        if (!Net.IsHost || Set(kind) is not { } set) return null;
        var seed = new SpawnSeed(NetIds.Next(set.Kind.Space), at, n, a, b);
        var made = set.Add(this, seed);
        ToWorld(nameof(NetSpawn), kind, seed.NetId, seed.At, seed.N, seed.A, seed.B);
        return made;
    }
    // Idempotent: a late joiner is sent everything that exists, and one of them may already have
    // reached it the ordinary way.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetSpawn(int kind, int id, Vector2 at, int n, double a, double b)
    {
        if (Set(kind) is { } set && !set.Has(id)) set.Add(this, new SpawnSeed(id, at, n, a, b));
    }
    // host: it is gone -- with a burst or quietly -- and with the host's last word on its hull,
    // because the host removes it before that hull reaches anyone
    public void Down(int kind, Node2D what, bool burst, float hp)
    {
        if (!Net.IsHost || what == null || Set(kind) is not { } set) return;
        ToWorld(nameof(NetSpawnGone), kind, set.Kind.Seed(what).NetId, burst, hp);
        set.Drop(this, what, burst, hp);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetSpawnGone(int kind, int id, bool burst, float hp)
    {
        if (Set(kind) is { } set) set.DropById(this, id, burst, hp);
    }
    private double _raiderSend;

    // WHAT A RAID CAN REACH: player ships, and the utility ships at home -- SEEN OR NOT. A raider
    // chooses from it only what it can see (Raider.Up); a raid's burst lands on all of it
    // (Missiles.Raid), because stealth stops a raider picking a ship, not a missile bursting on one.
    public IEnumerable<Node2D> RaiderTargets()
    {
        foreach (var s in _ships.Values) if (Raider.Reachable(s)) yield return s;
        // a turret a freighter left out is the base's too: something to go for, and something
        // that shoots back
        foreach (var t in Deployed) if (Raider.Reachable(t)) yield return t;
        // No fleet to come for: out in the arena there is no base, and at home there is a frame
        // before it is built. The sector is the reason; the null is the frame.
        if (InArena || Yard == null) yield break;
        foreach (var u in Yard.Fleet) if (Raider.Reachable(u)) yield return u;
    }

    // (A wave's composition, an escort's threat and the party's standing are Waves.cs; the
    // builder that reads them is Raids.Send. Nothing about a wave is written in this file.)

    // ── a freighter's turret: dropped, collected, or shot off its base ──────
    // A ROW OF Spawns.All (its space, its burst, how it is built and what a joiner is told), so
    // these three are faces onto Spawn/Down and nothing else. Picked up by its owner or shot off
    // its base: both go the same way, and only the burst differs.
    public DeployedTurret Drop(PlayerShip owner, Vector2 at, double hull) =>
        owner == null ? null : Spawn(Spawns.Turret, at, owner.OwnerId, hull, hull) as DeployedTurret;
    public void DeployedDown(DeployedTurret t) => Down(Spawns.Turret, t, burst: true, (float)t.Hp);
    public void DeployedTaken(DeployedTurret t) => Down(Spawns.Turret, t, burst: false, (float)t.Hp);

    // A raider is a row of Spawns.All too. Its SQUAD is the host's own bookkeeping and is not on
    // the wire (bar its id in the flags), so it is joined after the spawn; none named is a squad of
    // its own (Raids.Enlist).
    public Raider SpawnRaider(Vector2 at, int kind = Enemies.Webifier, Squad squad = null, double level = 1, double hullShare = 1)
    {
        if (Spawn(Spawns.Raider, at, kind, level, hullShare) is not Raider r) return null;
        _raids.Enlist(r, squad);
        return r;
    }
    public IReadOnlyList<Squad> Squads => _raids.LiveSquads;
    public Squad FormSquad(SquadDoctrine doctrine, Vector2 at, float heading = 0f) => _raids.Form(doctrine, at, 0, heading);
    public void RestartGarrison() => _raids.RestartGarrison();
    public PostBook Posts => _raids.Posts;
    public void RaiderDown(Raider r) => Down(Spawns.Raider, r, burst: true, (float)System.Math.Max(0, r.Hp));
    // A PAID KILL (a boss fight's add): every pilot in this world is paid at its OWN level -- this
    // one here, each guest by the reliable NetKillExp (a flag on the unreliable raider packet could
    // be lost with the kill). Named for the mechanism: any kill that pays comes through it.
    public void PayKill(double worth, int level)
    {
        if (!Net.IsHost) return;
        if (MyShip != null) Progression.AwardKill(worth, level);
        ToWorld(nameof(NetKillExp), worth, level);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetKillExp(double worth, int level) => Progression.AwardKill(worth, level);

    // LET GO OF A HOSTILE, EVERYWHERE, IN THIS CALL. As the selection and as every ship's orders:
    // on a guest a raider's hull never reads zero (the host removes it before its last hull
    // reaches anyone), and a withdrawn hunter (Raids.CallOff) is freed with hull left -- either
    // way it stays "alive" and freed, and a carrier's fighters would go on reading it. It leaves
    // the hostiles NOW, not when the queued free takes it out of the tree at the frame's end: a
    // turret that ticked later in the same frame would acquire it again. Leaving the hostiles IS
    // how every gun lets go, whatever carries it (Turret.StillThere), so nothing here names a turret.
    // Reached from a row's `Gone` (Spawns.All), so every kind that is a hostile lets go this way
    // and a kind that nothing holds simply has no `Gone`.
    public void LetGo(IHittable h)
    {
        _targets.Remove(h);
        Combat.Hostiles.Remove(h);
        foreach (var s in _ships.Values) if (IsInstanceValid(s)) s.Forget(h);
    }

    // ── A PREDICTED MISSILE, WHOSEVER IT IS (Missiles.cs) ───────────────────
    // The heavy fighter's was the only one in the game and this was written for it alone: the
    // tuple's third field was a RAIDER id, the blast was resolved against RaiderTargets() only and
    // landed through IRaidTarget.Hit only, and the flight, the blast and the telegraph's colour
    // were read straight off Raider. The outposts answer a blockade with the same missile from the
    // other side (Lanes.cs), so WHOSE it is is a row of Missiles.All and its numbers are the
    // launcher's own spec. Nothing below names a raider or an outpost.
    // EACH ONE HAS AN ID, in the one space every missile's id comes from (NetIds.Missile, as an
    // interceptable shot's), sent with the launch: whatever must name one blast on every peer -- a
    // flare pulling its landing mark aside (F14's NetDecoy) -- names it by that id.
    private readonly List<(Vector2 at, double left, int from, MissileSpec shot, int id)> _blasts = new();
    public int BlastsPending => _blasts.Count;
    public void ThrowMissile(MissileSpec m, Vector2 from, Vector2 at, int fromId)
    {
        if (!Net.IsHost) return;
        int id = Combat.NextMissileId();
        _blasts.Add((at, m.Flight, fromId, m, id));
        // the circle it marks, as every warning is marked: a row of Fx.All, on every peer -- red
        // for a threat, the friendly shape for one of ours
        Fx.Warn(new FxRaise { Id = Missiles.Of(m.Side).Mark, At = at, To = at, Size = m.Blast, Time = m.Flight });
        ShowMissile(m.Side, from, at, m.Flight, m.Blast, id);
        ToWorld(nameof(NetMissile), m.Side, from, at, m.Flight, m.Blast, id);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetMissile(int side, Vector2 from, Vector2 at, double flight, float blast, int id) =>
        ShowMissile(side, from, at, Net.Arriving(flight), blast, id);
    private void ShowMissile(int side, Vector2 from, Vector2 at, double flight, float blast, int id) =>
        AddChild(new MissileVisual { Side = side, From = from, To = at, Flight = flight, Blast = blast, NetId = id });
    private void TickBlasts(double delta)
    {
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var b = _blasts[i]; b.left -= delta;
            if (b.left > 0) { _blasts[i] = b; continue; }
            _blasts.RemoveAt(i);
            // WHAT IT MAY CATCH is its row's: a pool to look through and a TargetFilter over it,
            // never a type test -- asked what it HITS, never what it would choose, so a burst lands
            // on a ship gone dark. The row's id is the FAMILY the blow belongs to and the launcher's
            // own id is added to it, so two of them are never mistaken for one source
            // (PlayerShip.Incoming) -- "heavy:1042:missile", exactly the name it always had.
            var side = Missiles.Of(b.shot.Side);
            foreach (var t in side.Pool(this).ToList())
                if (side.Prey.Hits(t) && Raider.Gap(b.at, t) <= b.shot.Blast)
                    side.Land(t, b.shot.Damage, b.at, $"{side.Id}:{b.from}:missile");
        }
    }

    // In packets of at most 24 raiders. A full failed-mission raid is 40, and in one packet that
    // was 1.5 KB -- past the internet's usual 1.2 KB, so the transport split it, and a packet with
    // one piece lost waits for that piece: every raider froze for that update. Each packet names its
    // raiders, so each stands alone.
    private const int RaidersPerPacket = 24;
    // EVERY HOST-OWNED THING THE WORLD CAN DAMAGE, on one clock. A raider's packet carries where
    // it is as well, because it moves; everything that HOLDS A SPOT -- a dropped turret, a pirate
    // base, a shield pylon -- has nothing to say but what is left of it, and says it here. This
    // named the turrets outright, so a third kind with a hull meant a third packet and a third
    // RPC; the kinds that have a hull to report are the rows of Spawns.All that fill in `Hull`.
    private void SendWorldState(double delta)
    {
        if (!Net.IsHost || !Net.IsOnline) return;
        _raiderSend -= delta; if (_raiderSend > 0) return; _raiderSend = 0.1;
        for (int k = 0; k < Spawns.All.Count; k++)
        {
            if (Set(k) is not { Kind.Hull: not null } set) continue;
            var live = set.Nodes.ToList();
            if (live.Count == 0) continue;
            ToWorld(nameof(NetHulls), k, live.Select(n => set.Kind.Seed(n).NetId).ToArray(),
                                         live.Select(n => set.Kind.Hull(n)).ToArray());
        }
        foreach (var part in Raiders.Chunk(RaidersPerPacket))
            ToWorld(nameof(NetRaiders), part.Select(r => r.NetId).ToArray(), part.Select(r => r.Position).ToArray(),
                part.Select(r => r.Rotation).ToArray(), part.Select(r => (float)r.Hp).ToArray(),
                part.Select(r => r.TetherTo ?? new Vector2(float.NaN, float.NaN)).ToArray(),
                part.Select(r => r.NetFlags).ToArray());
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Hulls)]
    private void NetHulls(int kind, int[] ids, float[] hp)
    {
        if (Set(kind) is not { Kind.TakeHull: not null } set) return;
        int n = Mathf.Min(ids.Length, hp.Length);
        for (int i = 0; i < n; i++)
            if (set.Find(ids[i]) is { } node) set.Kind.TakeHull(node, hp[i]);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Raiders)]
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

    // ── A MISSION CLEARED, ONCE FOR EACH PILOT ────────────────────────────────
    // The record, the serial and the 12 s window are Session (Session.Kill, Session.Kills,
    // Session.NextKill, Session.OwedWindowMs): a clear outlives this world, because the pilot it is
    // owed to may only come back into the next one. The KIND travels with it: an owed one may be
    // paid in a world flying a different operation, and the kind is which LADDER it counts on.
    //
    // a clear, as each guest receives it: its share of the bounty, its own parts, its own EXP
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetKill(int kind, int level, int party, long serial, string[] drops, Vector2 at)
    {
        if (!Character.PayOnce(serial)) return;
        Yard.AddGuestShare(Missions.BountyEach(kind, level, party));        // to its own base, set aside while it visits
        ReceiveLoot(drops, at, level);
        Progression.AwardClear(kind, level);                                // (which saves: the share and the serial with it)
    }
    public int PartySize => _ships.Count;

    // host: a level-L mission is cleared. Every pilot credited with it -- the ships here, in stasis
    // or not (a pilot shot down still helped win it), and any whose place is held in this world --
    // gets its own EXP, its share of the bounty (split among all of them) and its own crates, each
    // sent to that pilot alone: a pilot whose world is still loading still gets them, and nobody
    // sees another's. The held are owed theirs until they are back. WHETHER there are crates at all
    // is the mission row's (MissionKind.Drops), not this code's.
    public void AnnounceClear(int kind, int level, Vector2 at)
    {
        if (!Net.IsHost) return;
        var held = Session.Places.Values.Where(h => h.World == _world).ToList();
        var here = _ships.Where(kv => IsInstanceValid(kv.Value)).Select(kv => (kv.Key, kv.Value.Class));
        var k = new Session.Kill { Serial = Session.NextKill(), Kind = kind, Level = level, Party = System.Math.Max(1, PartySize + held.Count), World = _world, At = at,
                                   When = Time.GetTicksMsec(), Present = _ships.Keys.ToHashSet(),
                                   Drops = Missions.KindOf(kind).Drops
                                         ? Loot.RollParty(here.Concat(held.Select(h => (h.OldPeer, h.Class))), level)
                                         : new Dictionary<int, string[]>() };
        Session.Note(k);
        foreach (var h in held) h.Owed.Add(k);
        Character.PayOnce(k.Serial);
        Yard.AddHostShare(Missions.BountyEach(k.Kind, k.Level, k.Party));  // the host's own share, paid at home
        ReceiveLoot(k.Drops.GetValueOrDefault(Net.LocalId, System.Array.Empty<string>()), at, k.Level);
        Progression.AwardClear(k.Kind, k.Level);                          // (which saves: the share and the parts with it)
        foreach (var peer in k.Present.Where(p => p != Net.LocalId)) PayKill(k, peer, peer);
    }
    // one pilot's share of a clear: `key` is who it was at the clear, `peer` who it is now
    private void PayKill(Session.Kill k, int key, int peer)
    {
        RpcId(peer, nameof(NetKill), k.Kind, k.Level, k.Party, k.Serial, k.Drops.GetValueOrDefault(key, System.Array.Empty<string>()), k.At);
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

    // A BOSS BIGGER THAN THE WHEEL'S WIDEST ZOOM CAN SHOW is framed by MOVING the centre toward it,
    // not by raising the ceiling (the owner, 2026-09-25: the wheel never zooms out past what it
    // already reaches). Generic for any boss row -- the far end of ITS hull, from ITS Length, never
    // a Drake `if`. The centre slides toward whichever end (nose or stern) sits farther from the
    // ship, only as far as that end needs and never past BossPilotMargin from the ship itself, so a
    // boss too big even then (Length > 2 x reach, the two margins) stays a design question, not a
    // camera bug -- and is gated to boss encounters (within 2 x reach) so a boss across the map
    // never tugs the view.
    private const float BossPilotMargin = 150f, BossEdgeMargin = 80f;
    private Vector2 BossFramed(Vector2 anchor)
    {
        if (Boss is not { Alive: true } b) return anchor;
        float reach = GetViewport().GetVisibleRect().Size.Y * 0.5f / ZoomLevel;
        if (anchor.DistanceTo(b.Position) > reach * 2f) return anchor;
        var half = Vector2.Up.Rotated(b.Rotation) * (b.Length * 0.5f);
        var far = anchor.DistanceSquaredTo(b.Position + half) > anchor.DistanceSquaredTo(b.Position - half)
                  ? b.Position + half : b.Position - half;
        float need = anchor.DistanceTo(far) - (reach - BossEdgeMargin);
        if (need <= 0f) return anchor;
        float slide = Mathf.Min(need, reach - BossPilotMargin);
        return slide <= 0f ? anchor : anchor + (far - anchor).Normalized() * slide;
    }

    private void MoveCamera(PlayerShip me, float dt)
    {
        _cam.Zoom = _cam.Zoom.Lerp(new Vector2(ZoomLevel, ZoomLevel), Mathf.Clamp(10f * dt, 0f, 1f));
        var anchor = me.ViewPosition;                                  // the pod, while in stasis
        if (!FreeCamera)
        {
            anchor = BossFramed(anchor);
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
        if (Hints.Wants("base") && !InArena && me.Position.DistanceTo(BasePos) < Hints.BaseMeet) Hints.Meet("base");
        if (Hints.Wants("tio") && !InArena && me.Position.DistanceTo(TioPos) < Hints.TioMeet) Hints.Meet("tio");
        if (Hints.Wants("target") && Combat.Nearest(Combat.Hostiles, me.Position, h => h.Position, Hints.TargetMeet, Combat.Pickable) != null) Hints.Meet("target");
        if (Hints.Wants("abilities") && Selected != null) Hints.Meet("abilities");
        if (Hints.Wants("raid") && !InArena && Raiders.Count > 0) Hints.Meet("raid");
        if (Hints.Wants("stasis") && !me.Alive) Hints.Meet("stasis");
        if (Hints.Wants("boss") && InArena && IsInstanceValid(Boss)) Hints.Meet("boss");
        // the hull's own drive card (warp or boost) once something far off is picked; the slide's once a hostile is near
        if (me.Drive is { } dv && Hints.Wants(dv.Id) && WarpAim() is { has: true } aim && aim.at.DistanceTo(me.Position) > Hints.WarpMeet) Hints.Meet(dv.Id);
        if (Hints.Wants("strafe") && me.Stats["strafe_speed"] > 0
            && Combat.Nearest(Combat.Hostiles, me.Position, h => h.Position, Hints.TargetMeet, Combat.Pickable) != null) Hints.Meet("strafe");
    }

    // Tab: ALWAYS the live hostile nearest your ship, at any range. No cycling --
    // pressing it again re-picks the nearest, so it never lands on a far target.
    // Switch to anything else with a left-click. Never a missile point defence alone may have
    // (Combat.Pickable): in the arena the boss's trident was often nearer than the boss, and Tab
    // took a missile. A cruise missile (Tag.Hulled) is a target like any hull, and Tab takes it
    // when it is the nearest.
    private void SelectNearest()
    {
        var me = MyShip;
        if (me != null) SelectTarget(Combat.Nearest(Combat.Hostiles, me.Position, h => h.Position, ok: Combat.Pickable));
    }

    // Left-click in the world: the hostile under the cursor (its hit circle, plus a
    // little slack), nearest the click if circles overlap. Clicking empty space keeps
    // the current target, so a stray click never drops it; Esc clears it.
    // true if the click landed on something (a hostile to select, or a building)
    // A CTRL-DRAG'S BOX, from the press to the release: everything pickable inside it, nearest to
    // the box's middle first, up to what the hull can hold. Nothing inside it leaves the selection
    // as it was rather than emptying it -- a stray drag should not cost a pilot its target.
    private Vector2? _boxFrom;
    public Rect2? SelectionBox => _boxFrom is { } f && MyShip != null
        ? new Rect2(new Vector2(Mathf.Min(f.X, GetGlobalMousePosition().X), Mathf.Min(f.Y, GetGlobalMousePosition().Y)),
                    (GetGlobalMousePosition() - f).Abs()) : null;
    private void BoxSelect(Vector2 a, Vector2 b)
    {
        var box = new Rect2(new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)), (b - a).Abs());
        var mid = box.Position + box.Size * 0.5f;
        var inside = Combat.Hostiles.Where(h => Combat.Pickable(h) && box.HasPoint(h.Position))
                                    .OrderBy(h => h.Position.DistanceSquaredTo(mid)).Take(TargetCap).ToList();
        if (inside.Count == 0) return;
        _targets.Clear(); _targets.AddRange(inside); Waypoint = null;
    }

    // what a click at `world` would pick, without picking it
    private IHittable PickAt(Vector2 world) =>
        Combat.Nearest(Combat.Hostiles, world, h => h.Position,
                       ok: h => Combat.Pickable(h) && world.DistanceTo(h.Position) <= h.HitRadius + 16f);

    private bool SelectAt(Vector2 world)
    {
        var best = PickAt(world);
        if (best != null) { SelectTarget(best); return true; }
        // BUILDINGS: a left-click on one opens its window. The building is the row whose footprint
        // holds the click and that has a window to open (Landmarks.cs). This happens at home only,
        // because the arena builds none of them.
        if (!InArena && Landmarks.All.FirstOrDefault(l => l.Opens != null && l.Covers(world)) is { } hit) { hit.Opens(this); return true; }
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
        SendWorldState(delta);
        if (Net.IsHost && Session.Places.Count > 0) ExpireHolds();
        if (Net.IsHost) _raids.Tick(delta);
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
        _targets.RemoveAll(t => t == null || !t.Alive);
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
            ship += Selected != null ? $"    target: {Selected.Label}  ({me.Position.DistanceTo(Selected.Position):0} u)"
                                       + (_targets.Count > 1 ? $"  +{_targets.Count - 1}" : "")
                  : Waypoint != null ? $"    waypoint: {WaypointName}"
                                     : "    no target (Tab / click)";
            if (Raiders.Count > 0) ship += $"    |    RAIDERS {Raiders.Count}";
            if (Placing) ship += $"    PLACING {_placingLabel}: left-click to confirm, right-click / Esc to cancel";
        }
        // WHICH SECTOR, not whether a Yard happens to exist: a third sector, or a home world
        // without a base, would have silently read as the arena.
        var q = InArena ? Quarry : null;                 // the boss, or the pirate base: the row decides
        string place = InArena
            ? $"ARENA  ·  {(q?.Title ?? "")} (LEVEL {Missions.Level})  {(q?.Hp ?? 0):0} / {(q?.MaxHp ?? 0):0}" + (MissionWon ? "  ·  DEFEATED" : "")
            : Yard == null ? ""          // the frame at home before the base is built: nothing to report yet
            // One reading per resource the fleet gathers, named by its row, so a third gatherer
            // puts its own stock on the line without an edit here.
            : string.Join("    ", Gathering.All.Select(g => $"{g.Unit.ToUpperInvariant()} {Yard.Stock(g.Resource):0}"))
              + $"    CREDITS {Yard.Credits:0}"
              + $"    HAULER {Yard.Hauler.Cargo:0}/{Yard.Capacity:0} {Yard.Hauler.State.ToString().ToUpperInvariant()}"
              // ...and the lanes, but only while one of them is cut: a full flow is the usual state
              + Lanes.Readout(this);
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

        // THE RANGE TO WHAT IS SELECTED, under it. The HUD names the target; what decides whether
        // anything reaches it is how far off it is, and that was nowhere on screen. Under the thing
        // rather than over it, because a hull bar and a pilot's name are already above.
        if (MyShip is { } from)
            foreach (var aim in _targets)
            {
                if (aim is not { Alive: true }) continue;
                DrawArc(aim.Position, aim.HitRadius + 14f, 0, Mathf.Tau, 24, new Color(1f, 0.85f, 0.3f, 0.55f), 1.5f);
                Txt.Centre(this, font, aim.Position + new Vector2(0, aim.HitRadius + 30f + aim.LabelDrop),
                           $"{from.Position.DistanceTo(aim.Position):0} u", Txt.Size(12), new Color(1f, 0.85f, 0.3f, 0.9f));
            }
        if (SelectionBox is { } box) DrawRect(box, new Color(1f, 0.85f, 0.3f, 0.55f), false, 1.5f);

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
                // CTRL AND DRAG: everything the box covers, up to what the hull can hold. The press
                // starts it; the release below is what picks, so a click that never moves is still
                // an ordinary click.
                else if (mb.CtrlPressed && TargetCap > 1) _boxFrom = at;
                // SHIFT: one more, or one fewer if it was already held
                else if (mb.ShiftPressed && TargetCap > 1) AddTarget(PickAt(at));
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
        if (e is InputEventMouseButton up && !up.Pressed && up.ButtonIndex == MouseButton.Left && _boxFrom is { } from)
        {
            BoxSelect(from, GetGlobalMousePosition());
            _boxFrom = null;
            GetViewport().SetInputAsHandled();
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
            else if (kk.Keycode == Key.V) mine.PressDrive();           // the drive (Drives.cs): a fixed key; a warp's release is read by the ship
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

    // ONE LAUNCH ON THE WIRE. There were three of these -- a shell's, a slug's and a torpedo's --
    // with the same four fields and their own spellings of the rest. A guest builds the same row
    // (Shots.Of) and flies a cosmetic copy: it draws and bursts, and damages nothing.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = NetChannels.Cosmetic)]
    private void NetShot(int kind, Vector2 from, Vector2 dir, float speed, float range,
                         float radius, int target, float turn, int id, float size, int variant) =>
        AddChild(new Shot { Kind = kind, Position = from, Dir = dir, Speed = speed, Range = range, Cosmetic = true,
                            Radius = radius, TargetId = target, TurnRate = turn, NetId = id, Size = size, Variant = variant,
                            Lead = (float)(1.0 - Net.Arriving(1.0)) });   // the round trip (see Shot.Lead)

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetFx(int id, Vector2 a, Vector2 b, float size, double time, double hold, double since, int anchor, string cue, string strike) =>
        // A GUEST'S WARNING ENDS WHEN ITS OWN POSITION IS JUDGED, not when the host's clock says
        // (Net.Arriving): on the internet the two are a round trip apart, and a pilot who cleared
        // the red on their own screen was hit "outside" it. An effect has no wind-up to shorten.
        AddFx(new FxRaise { Id = id, At = a, To = b, Size = size, Time = time > 0 ? Net.Arriving(time) : 0,
                            Hold = hold, Since = since, Anchor = anchor,
                            Cue = string.IsNullOrEmpty(cue) ? null : cue, Strike = string.IsNullOrEmpty(strike) ? null : strike });
    private void AddFx(FxRaise r)
    {
        // WHAT IT RIDES: the world, or the thing with that NetId -- a boss whose beam must swing
        // with its hull. Anything the world simulates and gives an id to can carry a warning.
        var rides = r.Anchor == Fx.World ? this : Combat.ById(r.Anchor) as Node2D;
        // A HULL'S WARNING RAISED AGAIN REPLACES ITS LANE, on every peer (a beam's wind-up stretched
        // by the live escape floor, Boss.Floor): the lane it had would run out, and sound its strike,
        // at the old time. The same move's warning on the same hull: same row, anchor and cue.
        if (r.Anchor != Fx.World)
            foreach (var n in Fx.Warnings.Where(n => n.Id == r.Id && n.Anchor == r.Anchor && n.Cue == r.Cue
                                                     && n.GetParent() == (rides ?? this)).ToList())
                n.QueueFree();
        (rides ?? this).AddChild(new FxNode { Id = r.Id, Position = r.At, To = r.To, Radius = r.Size, Time = r.Time,
                                              Hold = r.Hold, Since = r.Since, Anchor = r.Anchor, Cue = r.Cue, Strike = r.Strike });
    }
    // one warning, as it stands, to the one peer that missed it (Boss.CatchUp)
    public void FxTo(int peer, FxRaise r) =>
        RpcId(peer, nameof(NetFx), r.Id, r.At, r.To, r.Size, r.Time, r.Hold, r.Since, r.Anchor, r.Cue ?? "", r.Strike ?? "");

    // A FLASH BY ITS ROW. Its colour and its note are the row's (Beam.All), so the wire carries the
    // row's index and nothing else about it; Beam.Of reads an index this build has no row for as
    // point defence rather than throwing on a guest.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Flashes)]
    private void NetFlash(Vector2 a, Vector2 b, int beam) => AddFlash(a, b, beam);
    public const double FlashLife = 0.10;                            // a beam's flash, seconds
    private void AddFlash(Vector2 a, Vector2 b, int beam)
    {
        var row = Beam.Of(beam);
        _flashes.Add((a, b, row.Tint, FlashLife)); Sfx.Beam(a, b, row);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = NetChannels.Dummies)]
    private void NetDummy(int number, double last, double avg, double total)
    {
        // BY NUMBER, not by position. The dummies are numbered 1, 3, 4, 5 (2's spot holds the two
        // practice fighters), so `_dummies[number - 1]` sent 3's readout to 4, 4's to 5, and
        // dropped 5's entirely because 5 > Count. Guests saw the wrong numbers on the wrong hulls.
        foreach (var d in _dummies) if (d.Number == number) { d.SetReadout(last, avg, total); return; }
    }

    // THE SIDE WINDOW. BASE, PILOT, EQUIPMENT, the RECYCLER and the TIO all sit in one spot, so at
    // most one is open: opening one closes whichever held the spot, and asking for the one that is
    // open closes it. Four fields with four hand-written "close the others" lists used to disagree --
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
    // ...and OPEN one, leaving it alone if it is already up: what a click on a building does
    // (Landmarks), and what EQUIPMENT's RECYCLER button does. A toggle there would shut the window a
    // second click on the same building meant to keep open.
    public void OpenSide<T>(System.Func<T> make) where T : Control { if (!SideIs<T>()) ToggleSide(make); }
    private void ToggleBase() { if (!InArena) ToggleSide(() => new BasePanel { Hub = this }); }   // the arena: no base to open
    public void ToggleEquipment() => ToggleSide(() => new EquipmentWindow { Hub = this });
    public void TogglePilot() => ToggleSide(() => new PilotWindow { Hub = this });
    public bool CreatorOpen => IsInstanceValid(_creator);
    public bool StatsOpen => IsInstanceValid(_statsWin);

    // A purchase: refit the ship now, and tell the host (it resolves hull and damage).
    public void PilotChanged() { ApplyLocalIdentity(); SendIdentity(); }

    // REFIT: the only way into the ship menu (it costs 10%; see Yard.ResetCost). At the base, out of a
    // fight: the host would refuse the class it picks (Refit), and the pilot would fly one nobody else sees.
    public bool MayRefit => RefitOpen(InArena, MyShip?.InCombat == true);
    public void ResetShip()
    {
        if (IsInstanceValid(_creator) || !MayRefit) return;
        Yard.ChargeReset();
        Progression.Refit();                                         // ...and a level, and the last point it spent
        if (SideIs<BasePanel>()) CloseSide();
        OpenCreator();
    }
}
