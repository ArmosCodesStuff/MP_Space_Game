using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// LANES — a held route that carries a share of the base's income: the couriers that fly it, what
// cuts it, and the gun at its far end. ONE ROW PER LANE, and nothing about a lane anywhere else.
//
// WHY A FILE, AND WHY "LANE". The mechanism is A ROUTE BETWEEN TWO HELD POINTS THAT CARRIES A
// SHARE OF SOMETHING AND CAN BE CUT. The four runs to the outposts are its FIRST USE, not its
// definition: a lane to a second system, or one that carries research rather than ore, is a row
// here and nothing else anywhere. Before this the outposts were four positions and a sprite
// (Hub.Outposts) and nothing was ever delivered along them -- an escort held beside each one on
// its way to the portal, and that was the whole of what a lane had ever meant.
//
// A ROW MUST FILL IN:
//   Id / Outpost     what it is called, and which of Hub.Outposts is its FAR end. Its near end is
//                    always the base: a route to the base is what a lane IS.
//   Share            its share of the base's passive income. The four are a quarter each and add
//                    up to one; five rows at 0.2 would be fifths, with no arithmetic changed here
//                    or anywhere else -- the share is a FIELD, never a count.
//   Texture/Tint/Length/Nozzles   the couriers' art (the HullArt it derives from, Sprites.cs)
//   Drones/Speed     how many couriers fly it, and how fast
//   Dwell            how long each of them sits on a dock before it leaves
//
// WHERE THE INCOME IS CUT: Yard.Deposit, AND NOWHERE ELSE. Every unit the fleet ever delivers
// comes through that one method, so cutting a lane is ONE MULTIPLY by Lanes.Flow -- the sum of the
// shares of the lanes still carrying. All four open is 1.0; one cut is 0.75; two are 0.5. (The
// AWAY SHARE -- the twentieth of an estimate the base is paid for the time the party spent in the
// arena, Yard.AwayShare -- is deliberately NOT cut: nobody was in that world, no courier was
// flying in it and no blockade was standing on anything.)
//
// WHAT CUTS ONE: anything hostile sitting on it. A lane is cut while a live raider is within
// Lanes.Hold of the lane's STAND -- Lanes.Along of the way out from the base, well outside the
// base's own 600 u guns, 600 u outside the patrol perimeter (Raider.PerimeterR) so a patrol doing
// its rounds never cuts anything, and inside the outpost's reach so the outpost can answer.
// EVERY PEER WORKS THAT OUT FROM THE RAIDER LIST IT ALREADY HOLDS (Hub.Raiders is replicated by
// Spawns.All), exactly as Emplacement.Shielded works out a shield from the pylons it can see -- so
// nothing is sent for a blockade, there is no new RPC, and no peer can disagree about one. Clear
// them and the lane pays again in the same frame. A raid that merely stands on a lane cuts it
// while it is standing there, which is the same sentence and the right one.
//
// THE COURIERS ARE NOT A SECOND ECONOMY. A courier carries nothing and banks nothing: the income
// is the Yard's and a courier SHOWS it. IT CANNOT BE DESTROYED -- it is not IHittable and not
// IRaidTarget, it is in no Combat list and in no Hub.RaiderTargets, so nothing in the game can
// select it, shoot it or take a point off it. Cut its lane and it simply holds where it is, which
// is what a stopped convoy looks like; clear the lane and it goes on from there. It is therefore
// not on the wire either: every peer flies its own copies off its own clock -- they drift a little
// and it does not matter, because no number anywhere is read off where a courier is -- exactly as
// every peer builds its own outposts rather than being sent them.
//
// A COURIER DOCKS WHERE THE FLEET DOES. Each end of its run is a DOCK (Docks.cs): the base's
// service arm nearest its outpost -- the arm a miner unloads in -- and the clamp on its outpost's
// corner nearest the base. It berths and swings nose-in by the fleet's own arithmetic, sits there
// its row's Dwell, and leaves. It HOLDS NO DOCK: the Yard's reservation is for deliveries and a
// courier delivers nothing, so a miner holding its arm keeps it and the courier berths beside it.
//
// THE GUN AT THE FAR END is the lane's too, by its row, so a fifth lane arrives defended. It
// throws the heavy fighter's own predicted missile (Missiles.cs) in friendly colours, on the one
// gun component every other mount in the game uses (Turrets.cs), and the yard buys its RANGE, its
// RATE OF FIRE and its MISSILE SPEED -- three rows of Economy.All under the OUTPOSTS tab, one set
// for every outpost, as deploy_* is one set for every turret a freighter leaves out. It shoots
// what Targeting.Craft allows and nothing else: raiding craft, never a pilot, never the fleet,
// never a dropped turret, never a practice dummy -- a filter, never a type test.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class LaneDef : HullArt
{
    public string Id;            // reached by id or row, never by type
    public int Outpost;          // which of Hub.Outposts is its far end
    public double Share;         // its share of the base's passive income
    public int Drones;           // how many fly it, spread evenly round the round trip
    public float Speed;          // u/s
    public double Dwell;         // seconds a courier sits on each dock before it leaves
}

public static class Lanes
{
    // THE STATIONS' LIVERY: the pale blue the outposts are drawn on the scope in. The courier art
    // (the pack's drone_economy, courier.png) is grey and TINTED -- never a recoloured file, exactly
    // as a raider's is -- and flies on its three bells, as tools/make_ships.ps1 prints them.
    public static readonly Color Livery = new(0.62f, 0.78f, 0.92f);
    private static readonly Nozzle[] CourierBells = { new(-2.09f, 14.74f, 2.02f), new(0f, 14.87f, 2.11f), new(2.09f, 14.74f, 2.02f) };

    public static readonly LaneDef[] All =
    {
        new() { Id = "se", Outpost = 0, Share = 0.25, Texture = "res://courier.png", Length = 30f, Tint = Livery, Nozzles = CourierBells, Drones = 2, Speed = 320f, Dwell = 2.0 },
        new() { Id = "ne", Outpost = 1, Share = 0.25, Texture = "res://courier.png", Length = 30f, Tint = Livery, Nozzles = CourierBells, Drones = 2, Speed = 320f, Dwell = 2.0 },
        new() { Id = "nw", Outpost = 2, Share = 0.25, Texture = "res://courier.png", Length = 30f, Tint = Livery, Nozzles = CourierBells, Drones = 2, Speed = 320f, Dwell = 2.0 },
        new() { Id = "sw", Outpost = 3, Share = 0.25, Texture = "res://courier.png", Length = 30f, Tint = Livery, Nozzles = CourierBells, Drones = 2, Speed = 320f, Dwell = 2.0 },
    };

    // ── where a blockade stands on a lane ────────────────────────────────────
    public const float Along = 0.8f;       // where a blockade forms, along the lane
    public const float Ring = 200f;        // ...the ring its squad holds there (WaveDef.Hold)
    public const float Hold = 450f;        // ...and how near one must be for the lane to be cut

    // ── the lanes' own clock ─────────────────────────────────────────────────
    // WHEN a blockade comes, as Economy.EscortFirstWave / EscortWaveEvery is when an escort's
    // waves come: the schedule belongs to the thing that runs it (Raids.TickLanes), and WHAT
    // arrives is a row of Waves.All. A blockade waits on the base owner's standing (Unlocks,
    // Opens.Raids), because the lanes are a pressure on a base with something to lose.
    public const double FirstBlockade = 420, BlockadeEvery = 420;

    // ── the gun at a lane's far end, before anything is bought ───────────────
    public const string Tab = "OUTPOSTS";
    public const string RangeId = "outpost_range", RateId = "outpost_rate", SpeedId = "outpost_speed";
    public const float  GunRange = 1200f;      // u -- it covers its own stand from the first day
    public const double GunRate = 6.0;         // missiles a MINUTE: one every 10 s
    public const float  GunSpeed = 300f;       // u/s -- the flight is the gap over this
    public const double GunDamage = 42.0;      // what the blast does: the gunship's own figure
    public const float  GunBlast = 90f;        // ...and how wide it is where it lands
    public const double GunMinFlight = 0.6;    // never less than this in the air, however close
    public static readonly float GunTurn = Mathf.Tau / 6f;
    public static readonly Color GunTint = new(0.62f, 0.82f, 1f);

    public static LaneDef Of(int lane) => All[lane >= 0 && lane < All.Length ? lane : 0];
    public static string Name(int lane) => Hub.Outposts[Of(lane).Outpost].name;
    // ITS TWO ENDS: the base, and the outpost its row names.
    public static (Vector2 near, Vector2 far) Ends(int lane) => (Hub.BasePos, Hub.Outposts[Of(lane).Outpost].at);
    // ...and the DOCK AT EACH END its couriers use (Docks.cs): the base's service arm nearest the
    // outpost, and the outpost's clamp nearest the base. The fleet's own pads by the fleet's own
    // "nearest pad" rule, held by nobody -- so a courier shares an arm with whatever miner holds it.
    public static (Dock home, Dock away) EndDocks(int lane)
    {
        var (a, b) = Ends(lane);
        var home = Docks.On(Landmarks.ById("base")); var away = Docks.On(Landmarks.Outposts[Of(lane).Outpost]);
        return (home[Docks.Nearest(home, b)], away[Docks.Nearest(away, a)]);
    }
    public static Vector2 Stand(int lane) { var (a, b) = Ends(lane); return a.Lerp(b, Along); }
    public static Vector2 GunAt(int lane) { var (a, b) = Ends(lane); return b + (a - b).Normalized() * (Hub.OutpostHeight * 0.25f); }

    // CUT while anything hostile is standing on it. Worked out on every peer from the raiders it
    // already holds, so a blockade is state nobody has to send and nobody can disagree about.
    public static bool Cut(Hub hub, int lane)
    {
        if (hub == null || lane < 0 || lane >= All.Length) return false;
        var at = Stand(lane);
        foreach (var r in hub.Raiders)
            if (GodotObject.IsInstanceValid(r) && r.Alive && r.Position.DistanceTo(at) <= Hold) return true;
        return false;
    }

    public static int OpenCount(Hub hub) { int n = 0; for (int i = 0; i < All.Length; i++) if (!Cut(hub, i)) n++; return n; }
    // the first lane still carrying, or -1: what the blockade clock cuts next
    public static int NextOpen(Hub hub) { for (int i = 0; i < All.Length; i++) if (!Cut(hub, i)) return i; return -1; }

    // WHAT THE BASE IS STILL BEING PAID: the shares of the lanes still carrying. Four quarters
    // open is 1, one cut is 0.75, two are 0.5 -- and the figure is the ROWS' and never a count, so
    // a fifth row at 0.2 is fifths with nothing here to change.
    public static double Flow(Hub hub)
    {
        if (hub == null) return 1;
        double f = 0;
        for (int i = 0; i < All.Length; i++) if (!Cut(hub, i)) f += All[i].Share;
        return Math.Clamp(f, 0, 1);
    }

    // The HUD says so only when there is something to say: a full flow is the usual state, and a
    // line that is always there is a line nobody reads.
    public static string Readout(Hub hub)
    {
        int open = OpenCount(hub);
        if (open >= All.Length) return "";
        var cut = new List<string>();
        for (int i = 0; i < All.Length; i++) if (Cut(hub, i)) cut.Add(Name(i));
        return $"    LANES {open}/{All.Length} ({Flow(hub) * 100:0}%)  BLOCKADE {string.Join(" ", cut)}";
    }

    // EVERY LANE'S COURIERS AND ITS GUN, on every peer, beside the outposts they belong to
    // (Hub.BuildWorld). Nothing here is replicated: a courier is cosmetic and a gun fires only
    // under Net.Sim, and what it fires goes out down the missile's own RPC.
    public static void Build(Hub hub)
    {
        if (hub == null) return;
        for (int i = 0; i < All.Length; i++)
        {
            var d = All[i];
            hub.AddChild(new LaneGun { Hub = hub, Lane = i, Name = $"LaneGun_{d.Id}" });
            int drones = Math.Max(1, d.Drones);
            for (int j = 0; j < drones; j++)
                hub.AddChild(new Courier { Hub = hub, Lane = i, Phase = j / (float)drones, Name = $"Courier_{d.Id}_{j}" });
        }
    }
}

// A COURIER — a small drone shuttling between a lane's two ends, both ways, for ever. It is what
// the flow of income LOOKS like and nothing more: it holds no cargo figure, banks nothing, and
// cannot be hit, selected or destroyed (see the file header). Each half of its round trip starts
// DOCKED -- on the base's service arm nearest its outpost going out, on the outpost's clamp
// nearest the base coming back (Lanes.EndDocks) -- for its row's Dwell, swinging nose-in; then it
// FLIES to the other dock, eased at both ends so it reads as leaving and arriving. Its clock is
// public so a check can put it at a point of the run rather than waiting one out.
public partial class Courier : Node2D
{
    public Hub Hub;
    public int Lane;
    public float Phase;              // where in the round trip this one starts, 0..1
    public double Clock;             // seconds into the round trip

    public LaneDef Def => Lanes.Of(Lane);
    public Dock Home { get; private set; }                          // the base's arm it docks on
    public Dock Away { get; private set; }                          // ...and its outpost's clamp
    // WHERE IT SITS on each: its own length, at the face's outboard end (Dock.Abreast). The
    // centre line is for the craft that holds a dock, and a courier holds none.
    public Vector2 HomeBerth { get; private set; }
    public Vector2 AwayBerth { get; private set; }
    public double Cycle { get; private set; }                       // two dwells and two flights, s
    public bool Outbound { get; private set; } = true;              // base -> outpost, or back
    public bool Docked { get; private set; }                        // on a dock, not flying
    public bool Held { get; private set; }                          // its lane is cut: it waits
    public int Runs => (int)(Clock / Math.Max(0.001, Cycle) * 2);   // one-way runs finished

    public override void _Ready()
    {
        var art = Sprites.Fit(Def);                                 // grey art in the stations' livery
        AddChild(art);
        // over the stations it docks on (2), under the Yard's fleet (3 + 1): on a shared arm the
        // miner holding it is drawn over the courier beside it
        ZIndex = 3;
        float half = art.Texture.GetWidth() * art.Scale.X * 0.5f;  // its own half-width, off its art
        (Home, Away) = Lanes.EndDocks(Lane);
        HomeBerth = Home.Berth(Def.Length, Home.Abreast(half));
        AwayBerth = Away.Berth(Def.Length, Away.Abreast(half));
        Cycle = 2.0 * (HomeBerth.DistanceTo(AwayBerth) / Mathf.Max(1f, Def.Speed) + Def.Dwell);
        Place(1f);                   // a whole second of swing, clamped to all of it: it starts ON its heading
    }

    public override void _Process(double delta)
    {
        Held = Lanes.Cut(Hub, Lane);
        if (!Held) Clock += delta;                                  // a cut lane's couriers hold
        Place((float)delta);
        QueueRedraw();
    }

    // WHERE THE CLOCK PUTS IT. Each half of the round trip begins on a dock and ends on the other:
    // Def.Dwell seconds berthed and swinging nose-in, then the flight across.
    private void Place(float dt)
    {
        double f = (Clock / Math.Max(0.001, Cycle) + Phase) % 1.0;
        Outbound = f < 0.5;
        double inHalf = (Outbound ? f : f - 0.5) * Cycle;          // seconds into this half
        var (on, at, to) = Outbound ? (Home, HomeBerth, AwayBerth) : (Away, AwayBerth, HomeBerth);
        Docked = inHalf < Def.Dwell;
        if (Docked) { Position = at; Docks.NoseIn(this, on, dt); return; }
        float k = (float)((inHalf - Def.Dwell) / Math.Max(0.001, Cycle * 0.5 - Def.Dwell));
        Position = at.Lerp(to, Mathf.SmoothStep(0f, 1f, k));        // easing reads as leaving and arriving
        Rotation = Mathf.LerpAngle(Rotation, Aim.Face(at, to), Mathf.Clamp(Docks.Swing * dt, 0f, 1f));
    }

    public override void _Draw() =>
        Def.DrawPlumes(this, Vector2.Zero, 1f, Plume.Utility, Held || Docked ? 0f : 1f, !Held && !Docked);
}

// THE GUN AT A LANE'S FAR END — an outpost's answer to a blockade, and the late deterrent the
// yard buys. It mounts the one gun component (Turret) for its art and its swing, and throws the
// heavy fighter's own predicted missile (Missiles.cs) in friendly colours. Built by every peer;
// only the host fires, and what it fires reaches guests through the missile's own RPC, exactly as
// a raider's does.
public partial class LaneGun : Node2D, ITurretHost
{
    public Hub Hub;
    public int Lane;

    private readonly List<Turret> _mounts = new();
    private Turret _mount;
    private Vector2 _aim;
    private double _cd;
    private Lead _lead;
    private IHittable _prey;

    // ITS GUN, read every tick, so a level bought while a blockade stands is felt at the next
    // shot. Before the Yard exists (the frame the world is built) each falls back to its own row.
    private double Bought(string id, double fallback) =>
        Hub != null && GodotObject.IsInstanceValid(Hub.Yard) ? Hub.Yard.Value(id) : fallback;
    public float Range => (float)Bought(Lanes.RangeId, Lanes.GunRange);
    public double Interval => 60.0 / Math.Max(0.01, Bought(Lanes.RateId, Lanes.GunRate));
    public float Speed => (float)Bought(Lanes.SpeedId, Lanes.GunSpeed);
    public IHittable Aimed => _prey;                    // what it has picked, for a check's message

    // ── what carries the mount (ITurretHost) ────────────────────────────────
    public Node2D AsNode => this;
    public bool PdOnline => false;                      // a MAIN mount: it never fires by itself
    public Vector2 AimAt => _aim;
    public float FastSwing => 0f;
    public IReadOnlyList<Turret> Siblings => _mounts;
    public void NoteDealt(double d, IHittable target, string weapon) { }  // no tally: nobody's ship
    public PlayerShip Credit => null;
    // THE MOUNT, NOT THE ROUND. This gun's shot is a missile and never leaves the barrel as a
    // shell, so the spec is its art, its swing and its reach; nothing ever calls Turret.Shoot on
    // it, and the 0 damage says so out loud.
    public TurretSpec Spec(bool pd) => new()
    {
        Kind = Shots.Shell, Damage = 0, Interval = Interval, Range = Range, Turn = Lanes.GunTurn,
        ShellSpeed = 0f, Texture = "res://turret_main.png", TexScale = 0.30f, Barrel = 30f,
        Tint = Lanes.GunTint,
    };

    public override void _Ready()
    {
        Position = Lanes.GunAt(Lane);
        ZIndex = 4;
        _aim = Lanes.Stand(Lane);                       // at rest it watches its own lane
        _mount = new Turret();
        AddChild(_mount);
        _mount.Setup(this, Vector2.Zero, pd: false);
        _mounts.Add(_mount);
    }

    public override void _Process(double delta)
    {
        // WHO IT MAY SHOOT: raiding craft, by the filter the base's own guns already use. Never a
        // pilot, never the fleet, never a dropped turret, never a practice dummy -- and never by
        // asking what class a thing is.
        _prey = Targeting.Nearest(Combat.Hostiles, Position, Targeting.Craft, Range);
        if (_prey != null) { _aim = _prey.Position; _lead.Watch(_prey.Position, delta); }
        _mount.Tick(delta);
        if (!Net.Sim) return;                           // guests: the mount tracks; the host fires
        _cd -= delta;
        if (_prey == null || _cd > 0) return;
        _cd = Interval;
        float gap = Position.DistanceTo(_prey.Position);
        double flight = Math.Max(Lanes.GunMinFlight, gap / Mathf.Max(1f, Speed));
        Hub.ThrowMissile(new MissileSpec { Side = Missiles.Outpost, Damage = Lanes.GunDamage,
                                           Blast = Lanes.GunBlast, Flight = flight },
                         Position, Missiles.Predict(_prey.Position, _lead.Velocity, flight), Lane);
    }
}
