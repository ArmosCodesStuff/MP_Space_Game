using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// WHAT A FIGHT IS SPOKEN THROUGH — the contracts, and the carrier's craft.
//
//   IHittable      anything a shot, a turret or a click can find: the same id, and the same name,
//                  on every peer
//   IRaidTarget    anything a raider may come for -- a pilot's ship, a ship of a base's fleet, a
//                  turret left standing: how it is hit, the hull shape it is held station beside,
//                  and what coming for it is worth
//   WingKind       WHICH ROW of Wings.All a craft is: its art, its size, the stats it flies
//                  and fights on, whether it parks on a deck, its magazine and its cadence
//   WingWay/Wing   the three machines a craft is flown BY, and the craft itself
//
// THE CLASSES ARE NOT HERE. A class is a row of Classes.All (Ships.cs), and a pilot's levels and
// points are Progression + Character + PilotWindow. This header was a census of three classes
// "of an intended nine" and a list of what the pilot layer did not have yet; a census rots on the
// next class, so there is none.
//
// AUTHORITY: turrets and wings are COMBAT, so the host simulates them. A client
// renders what it is told. The owner decides only its heading, where it aims, and
// whether it is pulling the trigger -- never what the shot hits.
// ─────────────────────────────────────────────────────────────────────────────
public interface IHittable
{
    // Same on every peer, so a guest can name a target to the host ("attack 1000").
    int NetId { get; }
    Vector2 Position { get; }
    float HitRadius { get; }
    bool Alive { get; }
    void TakeDamage(double d);
    // WHAT IT IS CALLED wherever a target is named (the HUD's target line): its id, unless the
    // kind gives itself a name -- a practice dummy is "TARGET DUMMY 3". A type test asked this
    // (`Selected is TargetDummy td`), so a second kind with a name of its own meant a second arm
    // on that expression.
    string Label => $"#{NetId}";

    // HOW FAR BELOW ITS HULL IT ALREADY WRITES SOMETHING. The range readout is drawn under a
    // selected target (Hub), and a target that writes its own text down there had the two land on
    // top of each other: the practice dummy's "10s avg / total" line sits at HitRadius + 22 in
    // 13 px and the range sat at HitRadius + 30 in 12, which is not a gap.
    //
    // The DRAWER cannot know this -- it has an IHittable and nothing else -- so the TARGET says
    // it, the same way it says its own Label rather than being type-tested for one. Anything with
    // nothing under it keeps the 0 and nothing moves for it.
    float LabelDrop => 0f;
    // Does a projectile at p (with pad for its own size) touch this? A circle by
    // default; a long ship answers with a capsule along its keel.
    bool Covers(Vector2 p, float pad) => p.DistanceTo(Position) <= HitRadius + pad;
    // THE SHARE OF ITS HULL LEFT, 0..1 (Executioner's "under 35%"). A thing with no hull to speak of
    // answers 1, so nothing executes it.
    double HullLeft => 1;
    // WHERE ITS NOSE POINTS, a unit vector, for a thing with a heading (a raider, a boss): what "behind it" means (the
    // Wraith's Backstab, PlayerShip.Outgoing). Null -- a structure, a dummy, a round -- has no behind.
    Vector2? Facing => null;
    // A missile (Tag.Missile) can be shot down but is never SELECTED (click or Tab); a body in
    // flight with a hull of its own (Tag.Hulled) is picked like any hull.
    bool Selectable => true;
}

// WHAT A RAIDER GOES AFTER: a player's ship, or a ship of a base's fleet (UtilityShip). One
// contract where six type switches -- in Raider and in Hub -- had to agree on alive-ness, hull
// shape, damage and the pin, and a new kind of target meant finding all six.
public interface IRaidTarget : IStatused
{
    bool InReach { get; }                                    // there, and alive, to be attacked
    void Hit(double d, Vector2 from, string source);
    (float halfLength, float halfWidth) Extent { get; }      // the hull's ellipse, for holding station beside it
    // WHAT COMING FOR IT IS WORTH, in credits: the load a hauler is carrying (Hauler.Payout), and
    // 0 for everything that carries none -- which is part of how hard an escort's hunters come
    // (Hub.EscortThreat). That asked `quarry is Hauler h ? h.Payout : 0`, so a second kind of
    // target worth raiding meant a second arm on the type test.
    double Payout => 0;
    // WHAT DRAWS THE HOSTILE GUNS: true while a row that Draws runs on it (AbilityDef.Draws: the Taunt), and an
    // emplacement's gun then takes it before any other in its reach (Emplacement.Prefer). False for everything else.
    bool Draws => false;
}

// ── Wing craft: ONE ROW PER CRAFT ────────────────────────────────────────────
// A craft's KIND was a bool in disguise -- `bool F => Kind == WingKind.Fighter`, and then
// `F ? a : b` for its sprite, its length, the speed stat it read, the range stat it read, its
// visual scale and its draw size. A third craft -- an interceptor, a gunship, a repair drone --
// was a third arm on every one of those. Now a row says what a craft IS, and Wing is the code
// that flies one.
//
// TWO WAYS of flying are real behaviour and stay behaviour:
//   STRAFE  -- waits docked INSIDE the carrier. Ordered to attack the selected target (while it
//              is within control range) it launches and flies strafing runs like an aircraft --
//              steady speed, limited turn rate: Shots shots, through the target by Overshoot of
//              its diameter (and at least one of its own turning circles), turn, repeat. After
//              BurstStat seconds engaged it docks and rests RestStat. Recall brings it home.
//   STRIKE  -- an active ability. It waits PARKED on the carrier's deck (Parks), in bays either
//              side of the runway -- small, the deck being far below, as the hauler's pad is --
//              half to port and half to starboard, rearming there. On the order they take off one
//              at a time, LaunchInterval apart: each rolls onto the runway and up it to the bow,
//              growing to flight size as it lifts, as the hauler does. They fly at the target,
//              turn to face it at RangeStat, and fire straight ahead -- the rounds do not track --
//              then come home over the carrier's centre, settle onto the runway there, shrinking
//              back to deck size, and taxi to their bay. A target beyond the strike range (twice
//              the fighters' control range) is refused, and a strike whose target leaves it is
//              called off.
//   ORBIT   -- holds a circle round an ANCHOR (OrbitStat out) and fires on its target from
//              there, a hitscan shot whatever way the nose points. It WARPS: on its turn it
//              appears on its circle round the target it was given, and when its sortie ends or
//              the target dies it is gone the same way. (The gunships.)
//
// A SORTIE (a row with a LifeStat) is no part of the fitted wing: an ability sends it out
// (PlayerShip.Sortie) for LifeStat seconds beside the craft the carrier always flies, then it
// comes home and is freed. WHAT IT FIGHTS is either GIVEN (a gunship's target, which must be
// inside the row's EngageStat of the carrier when it goes) or PICKED (a row with Picks: the
// patrol takes whatever the turret's order, Turret.RankIn, puts first among what its filter
// chooses inside its EngageStat ring round the carrier -- a seeker before a raider before a
// boss, a practice dummy last -- and between targets it circles the carrier OrbitStat out).
// The fighter's ring is its row's EngageStat too.
//
// A NEW CRAFT MUST FILL IN: an appended WingKind, a row, and its count/speed/range/gun stats on
// the sheet (Stats.cs); a sortie its LifeStat and EngageStat. It needs a new tick ONLY if it flies
// a way none of the three machines flies.
// A NEW CRAFT MUST NOT: touch TickStrafe, TickStrike, TickOrbit or any `Kind ==` test.
public enum WingKind { Fighter, Bomber, Gunship, Patrol }
public enum WingWay { Strafe, Strike, Orbit }

// Its ART -- Texture, Length (the airframe at flight size), Tint and its bells -- is the HullArt it
// derives from (Sprites.cs).
public class WingDef : HullArt
{
    public string Id, Name;
    public WingWay Way;                 // which machine flies it
    public Vector2 Launch;              // where a launched round leaves: the port side's, mirrored each round (hull frame, u)
    public bool Parks;                  // it waits in a bay ON the deck, not inside the hull
    public float LandedScale = 1f;      // drawn this small parked, growing to 1 as it lifts
    // WHAT IT READS OFF THE CARRIER'S SHEET. Every number a craft flies and fights on is a stat
    // id here, never a literal in a tick: a new craft points at its own rows.
    public string CountStat;            // how many of it the carrier flies
    public string SpeedStat;
    public string AccelStat;            // null: it flies at a steady speed and never accelerates
    public string RangeStat;            // how close it fights from
    public string IntervalStat, DamageStat;    // its gun: seconds between shots, damage a shot
    public string TurnStat;             // its turn rate, rad/s (Strafe)
    public string BurstStat, RestStat;  // seconds it fights before it goes home, and rests (Strafe)
    public string AmmoStat, RearmStat;  // its magazine, and the wait to refill it. null: neither
    public string ShotSpeedStat, ShotRangeStat;   // its round, when the round is a launched one
    public int Beam;                    // its shot, when the shot is a flash: a row of Beam.All (its line and its note)
    public int Shots = 1;               // shots a pass (Strafe)
    public float Overshoot = 1f;        // ...carried on through the target by this many diameters
    public float Crawl = 1f;            // it keeps closing at this share of top speed while firing
    // HARD LIMIT: craft of this kind leave the carrier at least this far apart. Deliberately a
    // row, not a stat: no upgrade or rate-of-fire bonus may ever change it.
    public double LaunchInterval;
    // THE DECK, when Parks: the seconds a lift, a landing and a taxi take, the share of a lift
    // spent rolling out onto the runway, and how near the carrier's centre it sets down.
    public double Lift, Land, Taxi;
    public double RollOut = 0.25;
    public float Touch = 10f;
    // WHERE IT MAY FIGHT: a ring round the carrier's centre, EngageStat across -- measured to the
    // target's hull edge when ToHull, to its centre otherwise. A craft with an order and a picking
    // craft test it every tick; a craft GIVEN its target (Orbit) only when it is sent.
    public string EngageStat;
    public bool ToHull;
    public TargetFilter? Picks;         // it chooses its own target (see the header); null: it is told
    public string OrbitStat;            // the circle it holds round its anchor, or idles on round the carrier
    public string LifeStat;             // a SORTIE's seconds out; null: a fitted craft
    public bool Sortie => LifeStat != null;
}

public static class Wings
{
    // The index IS the id on the wire: PlayerShip's wing report is read in list position order,
    // fighters first. So APPEND ONLY.
    public static readonly WingDef[] All =
    {
        // The bomber is the LARGER airframe (28.125 to the fighter's 17), so the two read apart
        // at a glance; 0.65 parked is the hauler's own (12 u across, inside the 21.3 u of white
        // beside the runway), and 1.6 / 1.0 / 0.6 s are the deck's moves (the lift's run up 118 u
        // of runway ends at about its top speed). The art is the pack's fighter_delta and fighter_g
        // (the owner's picks), the bells and the bomber's wingtip rails as tools/make_ships.ps1
        // prints them.
        new() { Id = "fighter", Name = "Fighter", Way = WingWay.Strafe,
                Texture = "res://wing_fighter.png", Length = 17f,
                Nozzles = new Nozzle[] { new(-2.62f, 8.29f, 1.42f), new(2.62f, 8.29f, 1.42f) },
                CountStat = "fighter_count", SpeedStat = "fighter_speed", RangeStat = "fighter_range",
                IntervalStat = "fighter_interval", DamageStat = "fighter_damage",
                TurnStat = "fighter_turn", BurstStat = "fighter_burst", RestStat = "fighter_rest",
                EngageStat = "control_range",
                Beam = Beam.Fighter,
                Shots = 3, Overshoot = 1.2f, LaunchInterval = 0.83 },

        new() { Id = "bomber", Name = "Bomber", Way = WingWay.Strike,
                Texture = "res://wing_bomber.png", Length = 28.125f,
                Nozzles = new Nozzle[] { new(-2.99f, 13.08f, 2.04f), new(3.11f, 13.08f, 2.04f) },
                Launch = new(-8.48f, -0.12f),                                  // the front of its wingtip rails
                Parks = true, LandedScale = 0.65f,
                CountStat = "bomber_count", SpeedStat = "bomber_speed", AccelStat = "bomber_accel",
                RangeStat = "launch_range",
                IntervalStat = "torpedo_interval", DamageStat = "torpedo_damage",
                ShotSpeedStat = "torpedo_speed", ShotRangeStat = "torpedo_range",
                AmmoStat = "bomber_ammo", RearmStat = "bomber_rearm",
                Crawl = 0.25f, LaunchInterval = 0.83,
                Lift = 1.6, Land = 1.0, Taxi = 0.6 },

        // WARP GUNSHIPS (the carrier's E): gunship_count to a target, warped onto a 240 u circle
        // round it, 10 DPS each for 12 s. The target must be within 3000 u of the carrier when they
        // go, and only then. The bomber's airframe in steel until a gunship is cast (lane H). They
        // warp, so they leave a quarter second apart, not the deck's 0.83.
        new() { Id = "gunship", Name = "Gunship", Way = WingWay.Orbit,
                Texture = "res://wing_bomber.png", Length = 32f, Tint = new Color(0.72f, 0.80f, 0.90f),
                CountStat = "gunship_count", SpeedStat = "gunship_speed", RangeStat = "gunship_range",
                IntervalStat = "gunship_interval", DamageStat = "gunship_damage", TurnStat = "gunship_turn",
                OrbitStat = "gunship_orbit", EngageStat = "gunship_leash", LifeStat = "gunship_time",
                Beam = Beam.Fighter, LaunchInterval = 0.25 },

        // THE PATROL (the carrier's Q, Supercarrier): a second wing on the FIGHTER'S OWN NUMBERS --
        // its count, gun, speed and turn, so whatever lifts a fighter lifts these -- circling the
        // carrier 300 u out and taking whatever Turret.RankIn puts first inside 600 u of it, missiles
        // included, for 20 s. It does not rest. Amber, so the two wings read apart.
        new() { Id = "patrol", Name = "Patrol", Way = WingWay.Strafe,
                Texture = "res://wing_fighter.png", Length = 17f, Tint = new Color(1f, 0.72f, 0.25f),
                CountStat = "fighter_count", SpeedStat = "fighter_speed", RangeStat = "fighter_range",
                IntervalStat = "fighter_interval", DamageStat = "fighter_damage", TurnStat = "fighter_turn",
                EngageStat = "patrol_range", ToHull = true, Picks = Targeting.Patrol,
                OrbitStat = "patrol_orbit", LifeStat = "patrol_time",
                Beam = Beam.Fighter,
                Shots = 3, Overshoot = 1.2f, LaunchInterval = 0.83 },
    };

    public static WingDef Of(WingKind k) => All[(int)k];

    // How far `h` is from the carrier by the row's own measure (ToHull: to its hull's edge), and
    // whether that is inside the row's EngageStat.
    public static float Reach(PlayerShip carrier, WingDef row, IHittable h)
        => carrier.Position.DistanceTo(h.Position) - (row.ToHull ? h.HitRadius : 0f);
    public static bool Within(PlayerShip carrier, WingDef row, IHittable h)
        => Reach(carrier, row, h) <= carrier.Stats[row.EngageStat];

    // WHAT A CRAFT THAT PICKS FOR ITSELF TAKES (a row with Picks): of what the row's filter would
    // choose inside its ring, the best by the turret's order (Turret.RankIn: in flight, then small
    // craft, then the rest, a fallback last), then the nearest the carrier. HELD, a target is kept
    // while it lives and stays in the ring, and gives way only to a BETTER rank -- a seeker coming
    // in over the raider it is on; anything that can die over a fallback -- never to one merely
    // nearer, so a patrol does not flick between two raiders.
    public static IHittable Pick(PlayerShip carrier, WingDef row, IHittable held)
    {
        var f = row.Picks.GetValueOrDefault();
        IHittable best = null; int bestRank = 0; float bestD = 0;
        foreach (var h in Combat.Hostiles)
        {
            if (!f.Chooses(h) || !Within(carrier, row, h)) continue;
            int r = Turret.RankIn(f, h); float d = carrier.Position.DistanceTo(h.Position);
            if (best == null || r < bestRank || (r == bestRank && d < bestD)) { best = h; bestRank = r; bestD = d; }
        }
        bool keep = held != null && held.Alive && Combat.Hostiles.Contains(held) && f.Chooses(held) && Within(carrier, row, held)
                    && (best == null || Turret.RankIn(f, held) <= bestRank);
        return keep ? held : best;
    }
}

public partial class Wing : Node2D
{
    public PlayerShip Carrier;
    public WingKind Kind;
    public int Ammo;
    public Vector2 Velocity;

    private Sprite2D _sprite;
    private double _cd;

    // ── a sortie (PlayerShip.Sortie): its end on the carrier's clock, what it was given ──
    private double _until = -1;
    private IHittable _given, _prey;
    // Sent out until the carrier's clock reads `until`, at `given` (a gunship's target; null for
    // a craft that picks its own). The carrier frees it once it is Done (home, or warped out).
    public void SendOut(IHittable given, double until) { _given = given; _until = until; }
    public double Until => _until;
    public bool Done { get; private set; }
    private bool Over => Def.Sortie && (Carrier.Clock >= _until || !Carrier.Alive);
    // what it is on: a picking craft's own pick, a gunship's given target (for a check, and a chevron)
    public IHittable Engaging => Def.Picks != null ? _prey : _given;

    // ── fighters: strafing runs ──────────────────────────────────────────────
    // Each pass: 3 shots, straight THROUGH the target until 1.2x its diameter past
    // it -- and at least one of the fighter's own turning circles past it -- then turn
    // for the next. They fly like aircraft -- steady speed, limited turn rate -- so the
    // pass and the turn are real, and a target left INSIDE the turning circle can never
    // be brought onto the nose: the fighter only flies laps round it. A small, slow
    // target (a light raider riding a pinned hauler) ended up there after every pass,
    // and was never fired on again. After 15 s engaged they fly home and dock INSIDE
    // the carrier for a 3 s rest; with nothing to fight they stay inside.
    // CIRCLE: a craft with an OrbitStat (the patrol) between targets, round the carrier.
    private enum FSt { Docked, Launch, Approach, Burst, Overshoot, Turn, Home, Circle }
    private FSt _f = FSt.Docked;
    private float _heading;                     // fighter's nose, world angle
    private int _shots;                         // shots fired this pass
    private double _engaged, _rest, _launchT;
    public bool Resting => Strafes && (_f == FSt.Home || (_f == FSt.Docked && _rest > 0));
    public bool Inside => Def.Way switch { WingWay.Strafe => _f == FSt.Docked, WingWay.Orbit => _o == OSt.Waiting, _ => false };
    public string FighterPhase => _f.ToString();
    public int ShotsThisPass => _shots;
    public float LastOvershoot { get; private set; }   // how far past the target the last pass went
    // the carrier's clock when it last took off: a fighter out of the hangar, a bomber off the deck
    public double LaunchedAt { get; private set; } = -1;

    // ── gunships: waiting their turn inside, then on their circle round the target ──
    private enum OSt { Waiting, Orbit }
    private OSt _o = OSt.Waiting;
    public string OrbitPhase => _o.ToString();

    // ── bombers: the deck ─────────────────────────────────────────────────────
    // On the deck a bomber is drawn at LandedScale -- far below, as the hauler on its pad -- and
    // grows to full size as it lifts, on the hauler's curve (Altitude). Every deck move runs in the
    // carrier's frame on its own clock, so a moving or turning carrier carries it exactly, and a
    // landing always ends: it is timed, not steered.
    private enum BSt { Docked, Lifting, Approach, Aim, Launch, Return, Landing, Taxiing }
    private BSt _b = BSt.Docked;
    private double _deckT;                          // seconds into the deck move under way
    private Vector2 _touch; private float _touchRot; // where it set down, in the carrier's frame, and its heading then
    private bool _called;                           // counted into a strike when it was ordered, waiting on the deck for its turn
    public void Call() => _called = true;
    private bool OnDeck => _b is BSt.Docked or BSt.Lifting or BSt.Landing or BSt.Taxiing;
    // 0 on the deck, 1 at flight height (the hauler's SmoothStep)
    private float Altitude => _b switch
    {
        BSt.Docked or BSt.Taxiing => 0f,
        BSt.Lifting => Mathf.SmoothStep(0f, 1f, (float)((_deckT / Def.Lift - Def.RollOut) / (1 - Def.RollOut))),
        BSt.Landing => 1f - Mathf.SmoothStep(0f, 1f, (float)(_deckT / Def.Land)),
        _ => 1f,
    };
    public float VisualScale => Def.Parks ? Mathf.Lerp(Def.LandedScale, 1f, Altitude) : 1f;
    public string BomberPhase => _b.ToString();
    private void Go(BSt s) { _b = s; _deckT = 0; }

    public bool Launching => _b == BSt.Launch;
    public bool Docked => _b == BSt.Docked;
    public bool Armed => _b == BSt.Docked && _rearm <= 0 && Ammo > 0;
    // A bomber that joins a wing already in service (a refit) arrives empty and rearms like one
    // just home: swapping a bay on and off must not reload a wing that has fired.
    public void StartRearm() { Ammo = 0; _rearm = S[Def.RearmStat]; }
    public double RearmLeft => _b == BSt.Docked ? Math.Max(0, _rearm) : 0;
    private double _rearm;

    // Where the host says this craft is. Guests steer toward it.
    private NetPose _net;

    private ShipStats S => Carrier.Stats;
    // WHAT THIS CRAFT IS. Every number and every art path below comes off this row.
    public WingDef Def => Wings.Of(Kind);
    private bool Strafes => Def.Way == WingWay.Strafe;
    // Whether it has a bay on the deck -- which is also what PlayerShip.Bay lays out.
    public bool Parks => Def.Parks;
    private float Speed => (float)S[Def.SpeedStat];
    private float Accel => (float)S[Def.AccelStat];             // only a craft with one is asked
    private float Range => (float)S[Def.RangeStat];

    public void Init(PlayerShip carrier, WingKind kind, Vector2 at)
    {
        Carrier = carrier; Kind = kind; Position = at;
        Ammo = Def.AmmoStat == null ? 0 : (int)S[Def.AmmoStat];     // no AmmoStat: no magazine
        _sprite = Sprites.Fit(Def);
        _fit = _sprite.Scale;
        AddChild(_sprite);
        ZIndex = 4;
    }
    private Vector2 _fit;                       // the sprite's scale at flight size

    public void SetNet(Vector2 p, float rot) => _net.Set(p, rot);

    // (A wing is only ever ticked by its carrier, and the carrier frees its wings on its way out.
    // A carrier in stasis keeps its wing.)
    public void Tick(double delta)
    {
        Visible = Carrier.IsVisibleInTree() && !Inside;   // docked fighters are inside the hull
        QueueRedraw();                                     // the engine plume; a bomber's shadow on the deck
        if (Def.Parks) { _deckT += delta; _sprite.Scale = _fit * VisualScale; }

        if (!Net.Sim)
        {   // the host flies the wing; a guest follows where it says it is -- except a craft
            // on the deck (parked, lifting, landing, taxiing), placed here exactly in the
            // carrier's frame on its own clock, rather than trailing a moving carrier by a
            // packet's worth of lag
            if (Def.Parks && (OnDeck || !_net.Has)) { DeckPose(delta); return; }
            if (_net.Has) _net.Follow(this, (float)delta);
            return;
        }

        _cd -= delta;
        switch (Def.Way)
        {
            case WingWay.Strafe: TickStrafe(delta); break;
            case WingWay.Strike: TickStrike(delta); break;
            case WingWay.Orbit:  TickOrbit(delta); break;
        }
    }

    private void TickStrafe(double delta)
    {
        float dt = (float)delta;
        // a dead target is replaced while they are out: the carrier's order, or the craft's own pick
        var t = Def.Picks != null ? _prey = Wings.Pick(Carrier, Def, _prey) : Carrier.FighterTarget(Position);
        bool engage = t != null && t.Alive && Carrier.Alive && !Over && Wings.Within(Carrier, Def, t);
        // between targets: back round the carrier (a craft with an OrbitStat, while its sortie lasts), or home
        var idle = Def.OrbitStat != null && !Over ? FSt.Circle : FSt.Home;
        if (_f is FSt.Approach or FSt.Burst or FSt.Overshoot or FSt.Turn)
        {
            _engaged += delta;
            if (!engage || (Def.BurstStat != null && _engaged >= S[Def.BurstStat])) _f = idle;
        }
        switch (_f)
        {
            case FSt.Docked:
                // a sortie that ends (or loses its carrier) before this craft is out never sends it
                if (Def.Sortie && Over) { Done = true; break; }
                Position = Carrier.Position; Velocity = Carrier.Velocity; _heading = Carrier.Rotation - Mathf.Pi / 2f;
                if (_rest > 0) { _rest -= delta; break; }
                // a sortie goes as soon as it is sent; a fitted craft when there is something to fight
                bool go = Def.Sortie ? !Over : engage;
                if (go && Carrier.TakeLaunchSlot(Kind)) { _f = FSt.Launch; _launchT = 0; _engaged = 0; LaunchedAt = Carrier.Clock; Carrier.Signal(Carrier.Position); }
                break;
            case FSt.Circle:
                if (_f != idle) { _f = idle; break; }                   // its sortie is over: home
                if (engage) { _f = FSt.Approach; break; }
                CircleRound(Carrier.Position, (float)S[Def.OrbitStat], dt);
                break;
            case FSt.Launch:                       // out along the carrier's heading, clear of the hull
                _launchT += delta; Fly(dt);
                if (_launchT > 0.5) _f = FSt.Approach;
                break;
            case FSt.Approach:
                SteerTo(t.Position, dt); Fly(dt);
                if (Position.DistanceTo(t.Position) <= Range && OffAngle(t.Position) < Mathf.DegToRad(12f))
                { _f = FSt.Burst; _shots = 0; _cd = 0; }
                break;
            case FSt.Burst:
                SteerTo(t.Position, dt); Fly(dt);
                // the reload runs down ONCE a frame, in Tick; its gap is the carrier's Cadence, so a
                // lift on the carrier's rate of fire reaches its wing as it reaches its guns
                if (_cd <= 0 && _shots < Def.Shots)
                {
                    _cd += Carrier.Cadence(Def.IntervalStat); _shots++;
                    Dealt.Deal(t, S[Def.DamageStat], Carrier, Def.Id);
                    Combat.Flash(Position, t.Position, Def.Beam);
                }
                if (_shots >= Def.Shots) _f = FSt.Overshoot;
                break;
            case FSt.Overshoot:                    // straight on, through and past it
            {
                Fly(dt);
                float beyond = (Position - t.Position).Dot(Vector2.Right.Rotated(_heading));
                if (beyond >= Mathf.Max(Def.Overshoot * 2f * t.HitRadius, TurnDiameter)) { LastOvershoot = beyond; _f = FSt.Turn; }
                break;
            }
            case FSt.Turn:                         // come round for the next pass
                SteerTo(t.Position, dt); Fly(dt);
                if (OffAngle(t.Position) < Mathf.DegToRad(20f)) _f = FSt.Approach;
                break;
            case FSt.Home:
                SteerTo(Carrier.Position, dt); Fly(dt, 0.7f);
                if (Position.DistanceTo(Carrier.Position) < 60f)
                {   // in through the hangar; a sortie's craft is done, and the carrier frees it
                    _f = FSt.Docked; _rest = _engaged > 0 && Def.RestStat != null ? S[Def.RestStat] : 0; _engaged = 0;
                    Carrier.Signal(Carrier.Position);
                    if (Def.Sortie) Done = true;
                }
                break;
        }
        Rotation = _heading + Mathf.Pi / 2f;
    }

    // ORBIT: its turn inside the carrier, then it WARPS onto its circle round the target it was
    // given (on the side facing the carrier), holds that circle, and fires a hitscan shot at it
    // whenever the target is inside its gun's reach and the reload allows. When the sortie is over
    // or the target is gone it warps out: Done, and the carrier frees it.
    private void TickOrbit(double delta)
    {
        float dt = (float)delta;
        var t = _given;
        if (t == null || !t.Alive || Over) { Done = true; return; }
        float r = (float)S[Def.OrbitStat];
        if (_o == OSt.Waiting)
        {
            Position = Carrier.Position; Velocity = Carrier.Velocity; _cd = 0;
            if (!Carrier.TakeLaunchSlot(Kind)) return;
            var side = Carrier.Position - t.Position;
            side = side.LengthSquared() > 1f ? side.Normalized() : Vector2.Right;
            Position = t.Position + side * r; _heading = side.Angle() + Mathf.Pi / 2f;
            _o = OSt.Orbit; LaunchedAt = Carrier.Clock; Carrier.Signal(Carrier.Position);
            Rotation = _heading + Mathf.Pi / 2f;
            return;
        }
        CircleRound(t.Position, r, dt);
        if (Position.DistanceTo(t.Position) > Range) { if (_cd < 0) _cd = 0; }
        else for (int n = 0; _cd <= 0 && n < 8 && t.Alive; n++)
        {   // the reload carries its remainder, as a turret's does
            _cd += Carrier.Cadence(Def.IntervalStat);
            Dealt.Deal(t, S[Def.DamageStat], Carrier, Def.Id);
            Combat.Flash(Position, t.Position, Def.Beam);
        }
        Rotation = _heading + Mathf.Pi / 2f;
    }

    // Round `anchor` at radius r, as an aircraft flies: at its own speed and turn rate, nose on a
    // point `Lead` radians ahead on the circle, pushed out by 1/cos(Lead) so that the circle it
    // settles on IS r (aimed at the circle itself, pursuit settles at r cos(Lead)). From outside it
    // comes in, from inside it goes out, and a moving anchor (the carrier under way, a boss) is
    // followed. It needs r above its own turning radius (speed / turn): the rows' 300 and 240 u
    // against the fighter's 101 and the gunship's 80.
    private const float Lead = 0.6f;
    private void CircleRound(Vector2 anchor, float r, float dt)
    {
        var off = Position - anchor;
        float at = off.LengthSquared() > 1f ? off.Angle() : _heading;
        SteerTo(anchor + Vector2.Right.Rotated(at + Lead) * (r / Mathf.Cos(Lead)), dt);
        Fly(dt);
    }

    private float TurnRate => (float)S[Def.TurnStat];
    public float TurnDiameter => 2f * Speed / TurnRate;
    private float OffAngle(Vector2 p) => Mathf.Abs(Mathf.AngleDifference(_heading, (p - Position).Angle()));
    private void SteerTo(Vector2 p, float dt)
    {
        float diff = Mathf.AngleDifference(_heading, (p - Position).Angle());
        _heading += Mathf.Clamp(diff, -TurnRate * dt, TurnRate * dt);
    }
    private void Fly(float dt, float throttle = 1f)
    {
        Velocity = Vector2.Right.Rotated(_heading) * Speed * throttle;
        Position += Velocity * dt;
    }

    private void TickStrike(double delta)
    {
        var t = Carrier.StrikeTarget;
        // Carrier.Alive, as the fighter's `engage` has: a strike ordered in the moment the deck
        // died kept flying, and a host that ran a guest's press on a wreck launched a fresh one.
        bool live = Carrier.Alive && t != null && t.Alive && Carrier.Position.DistanceTo(t.Position) <= S["strike_range"];
        switch (_b)
        {
            case BSt.Docked:
                DeckPose(delta);
                if (_rearm > 0) { _rearm -= delta; if (_rearm <= 0) Ammo = (int)S[Def.AmmoStat]; }
                if (_called && live)
                {   // in the strike it was counted into: it takes off when the deck gives it its turn
                    if (Carrier.TakeLaunchSlot(Kind))
                    { _called = false; Go(BSt.Lifting); LaunchedAt = Carrier.Clock; Carrier.Signal(Position); }
                }
                // called off before its turn came: it answers for itself, once, as a bomber out does
                else if (_called) { _called = false; Carrier.NoteStrikeDone(); }
                break;

            case BSt.Lifting:
                DeckPose(delta);
                if (_deckT >= Def.Lift)
                {   // off the bow with the speed its run up the runway ended at, in the carrier's frame
                    // (the pace of the run's u-squared at its end) -- a frame's jump of the carrier, a
                    // warp or a snap, is not a speed
                    float run = 2f * (Carrier.Bay(this).Y + Carrier.MyArt.RunwayBow) / (float)(Def.Lift * (1 - Def.RollOut));
                    Velocity = Carrier.Velocity + Vector2.Up.Rotated(Carrier.Rotation) * run;
                    _b = BSt.Approach;
                }
                break;

            case BSt.Approach:
                if (!live) { _b = BSt.Return; Carrier.NoteStrikeDone(); break; }
                FlyToward(t.Position, Speed, delta);
                if (Position.DistanceTo(t.Position) <= Range) _b = BSt.Aim;
                break;

            case BSt.Aim:
            {   // creep in at a quarter speed while the nose swings onto the target; the
                // torpedoes go where it points
                if (!live) { _b = BSt.Return; Carrier.NoteStrikeDone(); break; }
                FlyToward(t.Position, Speed * Def.Crawl, delta);
                FaceToward(t.Position, delta);
                float off = Mathf.Abs(Mathf.AngleDifference(Rotation - Mathf.Pi / 2f, (t.Position - Position).Angle()));
                if (off < Mathf.DegToRad(4f)) { _b = BSt.Launch; _cd = 0; }
                break;
            }

            case BSt.Launch:
                if (live) { FlyToward(t.Position, Speed * Def.Crawl, delta); FaceToward(t.Position, delta); }   // still closing, slowly
                else { Velocity = Velocity.MoveToward(Vector2.Zero, Accel * (float)delta); Position += Velocity * (float)delta; }
                if (_cd <= 0 && Ammo > 0)
                {
                    _cd += Carrier.Cadence(Def.IntervalStat); Ammo--;
                    var dir = Vector2.Up.Rotated(Rotation);           // straight off the nose, off one wingtip rail and then the other
                    var rail = Ammo % 2 == 0 ? Def.Launch : Def.Launch with { X = -Def.Launch.X };
                    Combat.LaunchTorpedo(Position + rail.Rotated(Rotation), dir, (float)S[Def.ShotSpeedStat],
                                         (float)S[Def.ShotRangeStat], S[Def.DamageStat], source: Carrier);
                }
                if (Ammo <= 0) { _b = BSt.Return; Carrier.NoteStrikeDone(); }
                break;

            case BSt.Return:                           // home: over the carrier's centre, moving with it
                FlyToward(Carrier.Position, Speed, delta, Carrier.Velocity);
                if (Position.DistanceTo(Carrier.Position) < Def.Touch)
                {
                    _touch = Carrier.ToLocal(Position); _touchRot = Mathf.AngleDifference(Carrier.Rotation, Rotation);
                    Go(BSt.Landing); Carrier.Signal(Position);
                }
                break;

            case BSt.Landing:
                DeckPose(delta);
                if (_deckT >= Def.Land) Go(BSt.Taxiing);
                break;

            case BSt.Taxiing:
                DeckPose(delta);
                if (_deckT >= Def.Taxi) { Go(BSt.Docked); _rearm = S[Def.RearmStat]; Carrier.Signal(Position); }
                break;
        }
    }

    // Where a craft on the deck is, from the carrier and its own clock: parked in its bay, facing
    // the bow; LIFTING, rolling out onto the runway beside its bay, then up it to the bow end,
    // gathering speed; LANDING, from where it came over the centre down onto it, turning to face
    // the bow; TAXIING, from the centre to its bay.
    private void DeckPose(double delta)
    {
        var bay = Carrier.Bay(this);
        var local = bay; float turn = 0f;
        switch (_b)
        {
            case BSt.Lifting:
            {
                float k = (float)(_deckT / Def.Lift), roll = (float)Def.RollOut;
                var onRunway = new Vector2(0f, bay.Y);
                if (k < roll) local = bay.Lerp(onRunway, Mathf.SmoothStep(0f, 1f, k / roll));
                else
                {
                    float u = Mathf.Clamp((k - roll) / (1f - roll), 0f, 1f);
                    local = onRunway.Lerp(new Vector2(0f, -Carrier.MyArt.RunwayBow), u * u);
                }
                break;
            }
            case BSt.Landing:
            {
                float k = Mathf.SmoothStep(0f, 1f, (float)(_deckT / Def.Land));
                local = _touch.Lerp(Vector2.Zero, k); turn = Mathf.LerpAngle(_touchRot, 0f, k);
                break;
            }
            case BSt.Taxiing:
                local = Vector2.Zero.Lerp(bay, Mathf.SmoothStep(0f, 1f, (float)(_deckT / Def.Taxi)));
                break;
        }
        var at = Carrier.ToGlobal(local);
        Velocity = delta > 0 ? (at - Position) / (float)delta : Carrier.Velocity;
        Position = at; Rotation = Carrier.Rotation + turn;
    }

    // Replicated so a guest shows the same state: its ability bar, docked fighters hidden inside,
    // a bomber's deck moves, and the signal light when anything lands or takes off. The thousands
    // are the craft's ROW (Wings.All), so a guest can bring its list to the host's (PlayerShip
    // MatchSorties); the rest is the state in its own machine.
    public const int RowCode = 1000;
    public int StateCode => (int)Kind * RowCode + Def.Way switch { WingWay.Strafe => 100 + (int)_f, WingWay.Orbit => 200 + (int)_o, _ => (int)_b };
    public void SetNetState(int code, double rearm)
    {
        if (Net.Sim) return;
        code %= RowCode;
        if (code >= 200)
        {   // its warp onto the circle is a warp here too: from the host's pose, never eased across
            var o = (OSt)(code - 200);
            if (_o == OSt.Waiting && o == OSt.Orbit && _net.Has) { Position = _net.Pos; Rotation = _net.Rot; }
            _o = o; return;
        }
        if (code >= 100)
        {
            bool wasDocked = _f == FSt.Docked;
            _f = (FSt)(code - 100);
            if ((_f == FSt.Docked) != wasDocked) Carrier.Signal(Position);
            return;
        }
        _rearm = rearm;
        var b = (BSt)code;
        if (b == _b) return;
        // a deck move runs on this peer's own clock from here; a landing from where this peer shows it
        if (b == BSt.Landing) { _touch = Carrier.ToLocal(Position); _touchRot = Mathf.AngleDifference(Carrier.Rotation, Rotation); }
        Go(b);
        if (b is BSt.Lifting or BSt.Landing or BSt.Docked) Carrier.Signal(Position);
    }
    // Accelerate toward a point, easing off to arrive rather than overshoot.
    // `carry` is the velocity of whatever the craft is homing on (the carrier's centre, coming
    // home to land), so it arrives moving WITH it rather than stopping dead over it.
    private void FlyToward(Vector2 to, float spd, double delta, Vector2 carry = default)
    {
        var d = to - Position;
        float dist = d.Length();
        // the fastest speed from which this craft can still stop in `dist`
        float arrive = Motion.Arrive(spd, dist, Accel);
        var want = carry + (dist > 1f ? d / dist * arrive : Vector2.Zero);
        Velocity = Velocity.MoveToward(want, Accel * (float)delta);
        Position += Velocity * (float)delta;
        if (Velocity.LengthSquared() > 100f) FaceToward(Position + Velocity, delta);
    }

    private void FaceToward(Vector2 to, double delta)
    {
        float want = Aim.Face(Position, to);
        Rotation = Mathf.LerpAngle(Rotation, want, Mathf.Clamp(9f * (float)delta, 0f, 1f));
    }


    public override void _Draw()
    {
        if (Inside) return;                                      // a craft inside the hangar
        if (Def.Parks && OnDeck)
        {   // its shadow on the deck, tucked in when parked, further out and fainter the higher it
            // rises (the hauler's): screen down-right, in the bomber's own frame
            float alt = Altitude;
            var size = _sprite.Texture.GetSize() * _sprite.Scale;
            var drop = (new Vector2(3f, 5f) * (0.15f + alt)).Rotated(-Rotation);
            DrawTextureRect(_sprite.Texture, new Rect2(drop - size / 2, size), false, new Color(0, 0, 0, (0.45f - 0.15f * alt) * (1f - alt)));
            if (_b is BSt.Docked or BSt.Taxiing) return;       // engines off on the deck
        }
        Def.DrawPlumes(this, Vector2.Zero, VisualScale, Carrier.Accent, Velocity.Length() / Mathf.Max(1f, Speed), Velocity.Length() > 2f);
    }
}
