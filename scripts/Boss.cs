using Godot;
using System.Collections.Generic;
using System.Linq;

// -----------------------------------------------------------------------------
// WHAT A BOSS DOES -- one row per move, and nothing about a move anywhere else.
//
// There were two bosses and each was a hand-written clock: five countdowns in Lancer.Tick, three
// more in Drake.Tick, every one the same shape -- count a field down, check nothing else is
// running, reset it, set SuperGap, raise a telegraph, act -- and every one free to spell that
// shape differently. The Drake's header said outright that its numbers were forked from the
// Lancer's, and the Lancer's ram read the beam's constant for its own cadence. A third boss meant
// a third copy of all of it.
//
// A move is now a ROW (BossMove) and this file is the clock that runs one. Its numbers -- period,
// starting phase, wind-up, damage, reach, width, speed, turn, count, spread, the cue it plays,
// whether it holds the hull still -- are the row's. Only the WAYS a move runs are behaviour, and
// there are six, as there are two EnemyWays for the raiders:
//   BOLT   an instant bolt of light at the nearest ship within Find
//   SHOOT  Count bodies of Shot, fanned Spread degrees apart about the aim (a main gun is one)
//   BEAM   a line down the nose for Windup, then Live seconds burning along it, judged every Tick
//   DASH   a line down the nose for Windup, then the hull down it at Speed, Damage on contact
//   RING   a circle round the hull for Windup, then everything within Reach
//   THROW  a body on the flank in a tractor, a lane for Windup, then the body hurled down it
// Two OPENERS may come before a move's wind-up, and both are numbers rather than paths of their
// own: ESCORTS (launch them, hold, and charge the moment their web pins) and WARP (a ring where
// it will land, then the hull there, facing the pilot).
//
// A THIRD BOSS WRITES: one Missions.BossType row, and one BossMove[] in a file of its own, as
// Lancer.cs and Drake.cs hold theirs. Nothing here is edited for it. A move shape no row can
// express is a seventh MoveWay and one arm of Warn/Land/Burn -- a third EnemyWay, not a subclass.
// -----------------------------------------------------------------------------
public enum MoveWay { Bolt, Shoot, Beam, Dash, Ring, Throw }

// WHAT MUST BE IDLE BEFORE A MOVE MAY ARM. Every move had its own hand-written guard and no two
// agreed: the Lancer's beam checked only itself, its ram checked everything, its shockwave checked
// everything that MOVES the hull (it is centred where the boss stands, and a boss that rammed out
// of its own ring would leave it behind), and its guns and trident checked nothing at all. Four
// rules, spelled out eight times; here they are four words, and the row says which.
public enum MoveWait { Nothing, Itself, Movers, Everything }

// ONE MOVE. Everything a boss does is one of these, and a boss is the array of them on its row.
public class BossMove
{
    // A copy to scale for one fight, leaving the build's own row untouched (Boss.Scaled).
    public object MemberwiseCloneOf() => MemberwiseClone();

    public string Id;                  // reached by id -- S("beam"), S("throw") -- never by type
    public MoveWay Way;
    public MoveWait Waits;
    public double Every, First;        // its period, and how far into the fight the first one falls
    public double Windup;              // seconds of warning before it lands; 0: no warning at all
    public bool Busy;                  // it holds the hull still while it runs (Boss.Locked)
    public bool Super;                 // the skinny bar under the hull counts down to it
    public double Damage;              // before the level's and the party's scale (DamageMult)
    public float Find;                 // how far it looks for a target; 0: the whole arena
    public float Reach;                // how far the move itself carries: a beam, a dash, a lane
    public float Width;                // the warning's width, and a beam's own. A move whose
                                       // warning is as wide as the BODY it warns about leaves this
                                       // 0 and Boss.Warn works it out instead (MoveWay.Throw).
    public float Speed, Range, Radius; // a dash's speed; a fired body's speed, flight and body
    public float Muzzle;               // how far ahead of the nose a fired body is born
    public float Offset;               // how far off the flank a thrown body is held
    public int Shot = Shots.Shell;     // which row of Shots.All a SHOOT move fires
    public int Count = 1;              // bodies in one firing
    public float Spread;               // degrees between them, fanned about the aim
    public double Live, Tick;          // a beam's burn, and how often that burn is judged
    // A burn is judged at both ends and every Tick between: 3 s every 0.25 s is 13, the last AT 3 s.
    // COUNTED, and the last one ends the burn. Timed, the last judgement and the burn's end fell on
    // one instant, two accumulated clocks decided which came first, and a full burn was 200 or 250.
    public int Judgements => Mathf.FloorToInt(Live / Tick + 1e-6) + 1;
    public double Flight;             // a thrown body's flight down its lane
    public float Turn;                 // a guided body's turn rate (rad/s)
    public float Size = 1f;            // a fired body's drawn size
    public string Source;              // DamageSource: what the blow is called on a hull
    public string Cue, Strike;         // Sfx.Special as the warning goes up, and as it lands
    // A BOLT'S FLASH: a row of Beam.All, its line and its note. A Bolt row NAMES one: row 0 is
    // point defence, a friend's note. (Not MoveWay.Beam -- that is a move that burns down a line
    // for Live seconds, and it is heard by its Cue and Strike.)
    public int Beam;
    // THE ESCORT OPENER. Escorts light craft at +-EscortAngle, EscortOut off the hull, each with
    // EscortHull hull and boosting for the wind-up. The wind-up then starts on the PIN; with every
    // escort shot down, WebGrace past when their web was predicted to land; at the outside
    // WebSlack past that, and never later than ArmMax.
    public int Escorts, EscortKind;
    public float EscortAngle, EscortOut;
    public double EscortHull, WebGrace, WebSlack, ArmMax;
    // THE WARP OPENER. A ring WarpRing across where it will land, Warp seconds, Standoff off the
    // nearest pilot.
    public double Warp;
    public float Standoff, WarpRing;
    public string WarpSound;
    // it SHIFTS the hull: what MoveWait.Movers waits for
    public bool Shifts => Way == MoveWay.Dash || Warp > 0;
}

// A BOUNTY BOSS -- what every boss is. Its hull and damage, scaled by the level and the party; a
// hostile the party's weapons find; host-simulated, every peer drawing it from the host's state
// (10 Hz, 30 Hz while it is Locked) and playing its telegraphs from the host's events; the bar of
// its next super move. What it DOES is the Moves on its row (Missions.BossType): Missions.ForLevel
// says which boss a level is, and every peer builds THE SAME CLASS from that row (Hub.BuildArena),
// named "Boss" so the host's RPCs find it.
//
// LOCKED: a move that holds the hull is winding up or firing. The boss then neither closes nor
// turns except as that move itself decides -- the red line it drew is the line it fires down --
// and a guest takes its pose flat rather than easing it (see _netLocked).
public partial class Boss : Node2D, IQuarry, ITagged, IStatused
{
    public Hub Hub;
    public Missions.BossType Type;              // which boss: its name, its hull, its shape, its moves
    public string Title => Type.Name;           // what the arena's line calls it (IQuarry)
    // hull and damage by level and party: HullScale(L)(1 + 0.6(P-1)) and DamageScale(L)(1 + 0.2(P-1)) (Par)
    public double HullMult = 1, DamageMult = 1;
    public double MaxHp => Type.Hull * HullMult;
    // ITS SHAPE IS ITS ROW'S, as a raider's is EnemyDef's: behaviour is code, geometry is data.
    public float Length => Type.Length;
    public float HalfWidth => Type.HalfWidth;
    public const int Id = NetIds.Boss;
    public Tag Tags => Tag.Boss;
    // A bastion's shockwave cannot throw a boss, so it holds it still instead (Status.Disabled):
    // it neither moves nor acts while it lasts. The host decides; guests simply stop being told
    // to move it. A status an OutGuards row spares a boss (Dazzled, Jammed) is never put on it.
    private StatusSet _status;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status s, double seconds, double share = double.NaN) { if (Net.Sim && StatusSet.Reaches(s, Tags)) _status.Apply(s, seconds, share); }
    public bool Held => _status.Has(Status.Disabled);
    // WHAT A MOVE LANDS FOR: its row's damage on the level's and the party's scale, through the
    // outgoing door (StatusSet.Out) as a super or not. Every blow a move deals is this, once.
    private double Out(BossMove m) => _status.Out(m.Damage * DamageMult, m.Super ? OutKind.Super : OutKind.Move);
    public double Hp { get; set; }              // a property, not a field: IQuarry asks for it
    public bool Alive => Hp > 0;
    public int NetId => Id;
    public float HitRadius => HalfWidth;
    public bool Covers(Vector2 p, float pad) => Combat.KeelCovers(this, Length, HalfWidth, p, pad);

    // -- a move's own live state: one slot per row ----------------------------
    // What PlayerShip.Slot is for an ability. Reached by id -- S("beam"), S("throw") -- never by
    // index and never by the boss's type. The host writes it; a guest's slots sit idle (its boss
    // never ticks) and what a guest must SEE rides NetState and the one effect RPC (Hub.NetFx).
    public enum Phase { Idle, Opening, Winding, Firing }
    public class Slot
    {
        public BossMove M;
        public double Due;                 // seconds until it may arm
        public Phase At;
        public double T;                   // the phase's clock: down, except an escort wait
        public double Next;                // a beam's next judgement
        public int Left;                   // a beam's judgements still to come (BossMove.Judgements)
        public int Fired;                  // how many times it has committed
        public float Aim;                  // the world angle it points down, frozen with the warning
        public Vector2 From, To;           // a dash's end, a warp's landing, a ring's centre, a lane
        public PlayerShip Target;          // the pilot it chose, while it can see one (Sight)
        public Vector2 Spot;               // where it aims: that pilot, or where anyone was last seen
        public double Predict, Overdue, ArmedFor;   // an escort opener's clock
        public string Cause = "";                   // what ended the escorts' wait
        public readonly List<Raider> Escorts = new();
    }
    private Slot[] _slots = System.Array.Empty<Slot>();
    // A move as this level flies it: quicker to wind up, faster in flight, and longer-armed, by
    // Missions.Quicken. The burn of a beam and how often it is judged are left alone -- they are
    // the damage, not the animation.
    // ITS OWN TURN AT THIS LEVEL: the row's rate, one percent a level quicker, worked out once.
    private float _turn;
    public float Turning => _turn > 0 ? _turn : Type.TurnRate;

    private static BossMove Scaled(BossMove m, int level)
    {
        double q = Missions.Quicken(level);
        if (q <= 1.0000001) return m;                       // level 1: the row itself, unchanged
        var c = (BossMove)m.MemberwiseCloneOf();
        c.Windup /= q; c.Warp /= q; c.Flight /= q;
        c.Speed = (float)(c.Speed * q);
        c.Reach = (float)(c.Reach * q); c.Range = (float)(c.Range * q); c.Radius = (float)(c.Radius * q);
        // ...and how far off the flank a thrown body is held goes with the body: it is a clearance
        // (HalfWidth + Radius + a gap), so a rock that grows with the level and an offset that did
        // not would end up drawn inside the hull holding it.
        c.Offset = (float)(c.Offset * q);
        c.Turn = (float)(c.Turn * q);                   // a guided body comes round faster too
        c.Find = (float)(c.Find * q); c.Standoff = (float)(c.Standoff * q); c.EscortOut = (float)(c.EscortOut * q);
        return c;
    }

    public Slot S(string id)
    {
        foreach (var s in _slots) if (s.M.Id == id) return s;
        return null;
    }
    // Locked: a move that HOLDS THE HULL is running, whichever it is.
    public bool Locked => _slots.Any(s => s.At != Phase.Idle && s.M.Busy);
    private bool Shifting => _slots.Any(s => s.At != Phase.Idle && s.M.Shifts);
    // Warnings up now: a wind-up, or a warp's ring. (The ram's wind-up counts too; it always drew
    // a warning, and it was the one warning this count forgot.)
    public int WarningsPending => _slots.Count(s => s.At == Phase.Winding || (s.At == Phase.Opening && s.M.Warp > 0));

    // The skinny bar under the health bar: how far along the wait for the next SUPER MOVE -- the
    // moves whose row says Super. The host says how long is left (NextSuperHost) and across what
    // gap it counts (SuperGap, re-based as each super commits).
    private double NextSuperHost => _slots.Where(s => s.M.Super).Select(s => s.Due).DefaultIfEmpty(0).Min();
    private double SuperGap = 6.0;
    // HOST-ONLY TIMERS, SO GUESTS ARE TOLD. A boss's super timers only ever tick under Net.Sim, so on
    // a guest they sit at their starting values for the whole fight and the bar under the boss's
    // hull read zero from the first shot to the last. It is drawn state, so it goes on the wire.
    // Between packets a guest runs the clock down itself: the bar has to move smoothly at 10 Hz,
    // and a countdown corrected thirty times a second cannot drift anywhere.
    private double _netNextSuper = -1, _netSuperGap;
    public double NextSuperIn => Net.Sim ? System.Math.Max(0, NextSuperHost) : System.Math.Max(0, _netNextSuper);
    public double SuperFill
    {
        get { double gap = Net.Sim ? SuperGap : _netSuperGap; return gap <= 0 ? 0 : System.Math.Clamp(1 - NextSuperIn / gap, 0, 1); }
    }
    private NetPose _net; private bool _netLocked;
    private double _send;
    private HullWatch _hullWatch;

    public override void _Ready()
    {
        int party = System.Math.Max(1, Hub.PartySize);
        HullMult = Missions.HullMult(Missions.Level, party); DamageMult = Missions.DamageMult(Missions.Level, party); Hp = MaxHp;
        Name = "Boss";
        // ITS MOVES, FROM ITS ROW: one slot of live state each, in the row's order
        // ...AND IT COMES ROUND FASTER. A boss that turned at a fixed rate was out-turned by every
        // level's better engines, so late fights were won by circling something that could not
        // follow. The same one percent a level as its wind-ups and its reach.
        _turn = (float)(Type.TurnRate * Missions.Quicken(Missions.Level));
        // THE LEVEL'S OWN COPY OF EVERY MOVE. The rows are the build's, shared by every boss that
        // ever flies, so the level's scale is applied to a CLONE -- scaling the row itself would
        // make the second boss of a session faster than the first. Done here, once, so not one of
        // the twenty-odd places that read a move has to know the level exists.
        _slots = Type.Moves.Select(m => new Slot { M = Scaled(m, Missions.Level), Due = m.First }).ToArray();
        AddChild(Sprites.Fit(Type.Sprite, Length));
        ZIndex = 4;
        Combat.Hostiles.Add(this);
    }
    public override void _ExitTree() => Combat.Hostiles.Remove(this);

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;
        Hp = System.Math.Max(0, Hp - d);
        if (Hp <= 0) Hub.MissionCleared(Position);
    }
    // the host's word that it is down: a peer that never had its figures (one that arrived after the
    // win) shows nothing for it
    public void Downed() { if (!_net.Has) _hullWatch = default; Hp = 0; }

    // WHO IT CAN SEE, AND WHO IT CAN HIT -- two lists, filled once a frame, never one. Stealth
    // (Status.Untargetable) hides a pilot from everything the boss CHOOSES: whom it closes on, whom
    // a move is aimed at, whom its escorts go after, whom a guided body is given. It hides nobody
    // from what LANDS: a ring, a beam and a ram catch every live pilot under them, seen or not
    // (Targeting.cs). With nobody in sight it closes on, and aims at, the last place it saw anyone;
    // its clocks run on.
    private readonly List<PlayerShip> _choosable = new(), _hittable = new();
    private Vector2? _lastSeen;                 // the centre of the pilots it saw, the last frame it saw any
    private void Look()
    {
        _choosable.Clear(); _hittable.Clear();
        _choosable.AddRange(Targeting.Choosable(Combat.Players, Targeting.Attackable).OfType<PlayerShip>());
        _hittable.AddRange(Targeting.Hittable(Combat.Players, Targeting.Attackable).OfType<PlayerShip>());
        if (_choosable.Count > 0) _lastSeen = _choosable.Aggregate(Vector2.Zero, (c, p) => c + p.Position) / _choosable.Count;
    }
    private bool Sees(PlayerShip p) => _choosable.Contains(p);
    // THE PILOT A RUNNING MOVE FOLLOWS: while it is in sight, the move's spot goes with it; out of
    // sight -- dark, down, gone -- the nearest pilot in sight takes its place; with nobody in sight
    // the move keeps the spot it had.
    private void Sight(Slot s)
    {
        if (!Sees(s.Target)) s.Target = Combat.Nearest(_choosable, Position, p => p.Position);
        if (s.Target != null) s.Spot = s.Target.Position;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _hullWatch.Tick(this, Hp, taken: false);
        if (!Alive && Fx.Warnings.Any()) Fx.ClearWarnings();
        if (!Net.Sim)
        {
            if (_net.Has)
            {
                if (_netLocked)
                {   // Locked, the hull IS the telegraph, and smoothing it is no longer cosmetic:
                    // the lerp lags about 0.1 s, which at 0.3 rad/s is ~1.7 degrees -- some 300 u
                    // at a beam's 10000 u reach, against a beam 70 u wide. A guest would watch
                    // the hit land far outside the line it was shown. Take the host's figures flat;
                    // the boss is standing still and at most turning slowly (to face its target
                    // while its escorts are out), so there is nothing to smooth.
                    Position = _net.Pos; Rotation = _net.Rot;
                }
                else _net.Follow(this, dt);
                if (_netNextSuper > 0) _netNextSuper = System.Math.Max(0, _netNextSuper - delta);
            }
            return;
        }
        if (!Alive) return;
        _status.Tick(delta);
        if (Held) { QueueRedraw(); return; }         // held still: no approach, no ability, no turn
        Look();
        if (_hittable.Count > 0)                    // nobody alive at all -- every pilot in stasis -- and it waits
        {
            if (!Locked) Approach(dt);
            Tick(delta);
        }
        _send -= delta;
        // While a super move winds up the HULL IS THE TELEGRAPH, so a guest needs its angle far
        // more often than 10 Hz: 30 Hz while locked, and guests stop smoothing it (see _netLocked).
        // To the peers actually IN the arena. Broadcasting to everyone meant a guest still loading
        // the arena scene got packets for a Hub/Boss it did not have yet -- "Node not found",
        // "Invalid packet received" -- and at 30 Hz while locked there are three times as many
        // chances to land in that window.
        if (_send <= 0 && Net.IsOnline)
        {
            _send = Locked ? 1.0 / 30 : 0.1;
            Hub?.SendBoss(Position, Rotation, Hp, Locked, NextSuperIn, SuperGap, HullMult);
        }
    }

    // Unlocked, it closes on the party, ponderously: turning onto the centre of the party it can
    // see -- or, with nobody in sight, onto where it last saw them (Look) -- at its row's turn rate,
    // and moving in to its row's hold-off. These were 650, 30 and 0.3 written into this method, so
    // every boss ever written closed exactly alike and none could be told to do otherwise.
    private void Approach(float dt)
    {
        if (_lastSeen is not { } centre) return;    // it has never seen anyone: it holds
        float want = Aim.Face(Position, centre);
        Rotation = Mathf.RotateToward(Rotation, want, Turning * dt);
        if (Position.DistanceTo(centre) > Type.HoldOff) Position += (centre - Position).Normalized() * Type.CloseSpeed * dt;
    }

    // -- THE SCHEDULER --------------------------------------------------------
    // ONE PASS OVER THE ROWS: each move's clock, its arming, then whatever it is already doing.
    // In the row's order, so a boss's table IS the order a frame resolves its moves in -- and a
    // phase that ends here is picked up by the next step in the SAME frame, as every one of the
    // eight hand-written clocks did.
    private void Tick(double delta)
    {
        foreach (var s in _slots)
        {
            s.Due -= delta;
            if (s.Due <= 0 && Armable(s)) Arm(s);
            if (s.At == Phase.Opening) Open(s, delta);
            if (s.At == Phase.Winding && (s.T -= delta) <= 0) { s.At = Phase.Idle; Land(s); }
            if (s.At == Phase.Firing) Burn(s, delta);
        }
    }
    // The row says what must be idle; this is the only place that asks.
    private bool Armable(Slot s) => s.M.Waits switch
    {
        MoveWait.Nothing => true,
        MoveWait.Itself => s.At == Phase.Idle,
        MoveWait.Movers => s.At == Phase.Idle && !Shifting,
        _ => _slots.All(x => x.At == Phase.Idle),
    };

    // ARMED, IN ONE PLACE. It was four arms per move -- reset the clock, count it, re-base the
    // super bar, raise the warning -- written out again for each of the eight, and no two of them
    // owed the same four.
    private void Arm(Slot s)
    {
        var m = s.M;
        s.Due = m.Every; s.Fired++;
        if (m.Super) SuperGap = NextSuperHost;
        s.Target = Combat.Nearest(_choosable, Position, p => p.Position, m.Find > 0 ? m.Find : float.MaxValue);
        // WHAT IT AIMS AT: the pilot it chose -- or, with nobody in sight, where it last saw anyone,
        // so going dark is not a pause button. A move that looks only so far (Find) is a gun for
        // whoever is inside that reach, and waits for someone there; so does every move of a boss
        // that has never seen anyone. Either way the clock comes round again.
        if (s.Target != null) s.Spot = s.Target.Position;
        else if (m.Find <= 0 && _lastSeen is { } seen) s.Spot = seen;
        else return;
        if (m.Escorts > 0)
        {   // ESCORTS OUT -- and the wind-up waits on their web (Open). With nobody in sight there is
            // nobody to send them after: none go, and the wind-up comes on Open's own fallbacks.
            double eta = LaunchEscorts(m.Id, s.Target);
            s.At = Phase.Opening; s.T = 0;
            s.Predict = eta + m.WebGrace;
            s.Overdue = System.Math.Min(s.Predict + m.WebSlack, m.ArmMax);
            return;
        }
        if (m.Warp > 0)
        {   // A RING WHERE IT WILL LAND, Standoff off where it aims; the hull follows (Open)
            s.To = s.Spot + (Position - s.Spot).Normalized() * m.Standoff;
            s.At = Phase.Opening; s.T = m.Warp;
            Zone(s.To, m.WarpRing, m.Warp, null, m.WarpSound);
            return;
        }
        Aimed(s);
        if (m.Windup > 0) { Warn(s); s.At = Phase.Winding; s.T = m.Windup; }
        else { if (m.Cue != null) Sound(m.Cue, Nose); Land(s); }
    }

    // Where the move points, taken once and then held: a dash comes round onto its spot (Arm,
    // Sight) and the line it will ram down is fixed here, a ring is centred where the boss stands, a
    // thrown body is swung out on the flank, and everything else is aimed from the nose.
    private void Aimed(Slot s)
    {
        var m = s.M;
        switch (m.Way)
        {
            case MoveWay.Dash:
                Rotation = Aim.Face(Position, s.Spot);
                s.To = Aim.Nose(this, m.Reach);
                break;
            case MoveWay.Ring: s.To = Position; break;
            case MoveWay.Throw: Heave(s); break;
            default: s.Aim = (s.Spot - Nose).Angle(); break;
        }
    }

    // THE OPENERS, in flight. Both end the same way: the warning goes up and the wind-up starts.
    private void Open(Slot s, double delta)
    {
        var m = s.M;
        if (m.Escorts > 0)
        {   // held in place; turning to face the pilot -- or where it was last seen -- is all it does
            s.T += delta;
            Sight(s);
            Rotation = Mathf.RotateToward(Rotation, Aim.Face(Position, s.Spot), Turning * (float)delta);
            // A pin counts once THIS cycle's escorts have left the launch point: a pilot still held
            // by the last cycle's escorts (they never expire) would otherwise skip the whole phase.
            bool pinned = s.T >= Raider.EscortShiver && s.Target != null && s.Target.Pinned;
            bool down = !s.Escorts.Any(r => IsInstanceValid(r) && r.Alive);
            if (!pinned && !(down && s.T >= s.Predict) && s.T < s.Overdue) return;
            s.Cause = pinned ? "pinned" : s.T >= s.Overdue ? "overdue" : "escorts down";
            s.ArmedFor = s.T;
        }
        else if ((s.T -= delta) > 0) return;
        else
        {   // it lands, facing the pilot -- or where one was last seen -- and what follows is aimed
            // from there, fixed from here on
            Position = s.To;
            Sight(s);
            Rotation = Aim.Face(Position, s.Spot);
        }
        // AIMED FROM WHERE IT LANDED: a warp's landing goes through Aimed like every other move,
        // because that is where a dash's line, a ring's centre and a thrown body are worked out.
        Aimed(s);
        Warn(s); s.At = Phase.Winding; s.T = m.Windup;
    }

    // THE WARNING a move draws, by its way. A beam's and a dash's ride the HULL (the line drawn is
    // the line fired, and it swings with the hull rather than being pinned to the spot the boss
    // stood on); a ring, a lane and a fan are drawn in the world.
    private void Warn(Slot s)
    {
        var m = s.M;
        switch (m.Way)
        {
            case MoveWay.Beam:
                Lane(m, new Vector2(0, -Length * 0.5f), new Vector2(0, -Length * 0.5f - m.Reach), onHull: true, hold: m.Live);
                break;
            case MoveWay.Dash:
                Lane(m, Vector2.Zero, new Vector2(0, -m.Reach), onHull: true);
                break;
            case MoveWay.Ring:
                Zone(s.To, m.Reach, m.Windup, m.Cue, m.Strike);
                break;
            case MoveWay.Throw:
                // THE LANE IS THE BODY, not a number beside it: Radius is what ThrownRock draws
                // and what its hit sweep tests against, so the red lane is exactly as wide at
                // every level (Drake.cs, the throw row, says why this is not a Width field).
                Lane(m, s.From, s.To, hold: m.Flight, width: m.Radius * 2f);
                break;
            case MoveWay.Shoot:
                foreach (float a in Fan(s.Aim, m))
                    Lane(m, Nose, Nose + Vector2.Right.Rotated(a) * m.Range);
                break;
        }
    }

    // WHAT THE MOVE DOES when its warning ends (or, with no wind-up, this instant). A BOLT and a
    // GUIDED BODY are CHOSEN blows: they go to a pilot, and only to one still in sight. Everything
    // else lands where it lands -- a fired body hits what it touches; a ring, a beam and a ram
    // catch every live pilot under them -- seen or not (_hittable).
    private void Land(Slot s)
    {
        var m = s.M; var nose = Nose;
        switch (m.Way)
        {
            case MoveWay.Bolt:
                if (!Sees(s.Target)) break;
                s.Target.Hit(Out(m), Position, m.Source);
                Combat.Flash(nose, s.Target.Position, m.Beam);
                break;
            case MoveWay.Shoot:
            {
                int k = 0;
                foreach (float a in Fan(s.Aim, m))
                {
                    var dir = Vector2.Right.Rotated(a);
                    Combat.Fire(m.Shot, nose + dir * m.Muzzle, dir, m.Speed, m.Range, Out(m), m.Radius,
                                Shots.Of(m.Shot).Guided && Sees(s.Target) ? s.Target.NetId : 0, m.Turn,
                                hitSource: m.Source, size: m.Size, variant: k++);
                }
                break;
            }
            case MoveWay.Beam: s.At = Phase.Firing; s.Left = m.Judgements; s.Next = 0; break;
            case MoveWay.Dash: s.At = Phase.Firing; break;
            case MoveWay.Ring:
                foreach (var p in _hittable)
                    if (p.Position.DistanceTo(s.To) <= m.Reach + p.HitRadius) p.Hit(Out(m), s.To, m.Source);
                break;
            case MoveWay.Throw: s.At = Phase.Firing; break;
        }
    }

    // WHAT THE MOVE KEEPS DOING once it has landed: a beam burns, a dash travels, a thrown body
    // flies (ThrownRock does the flying, and the boss is held until it breaks).
    private void Burn(Slot s, double delta)
    {
        var m = s.M;
        switch (m.Way)
        {
            case MoveWay.Beam:
                if ((s.Next -= delta) <= 0)
                {   // judged every Tick, down the nose -- which has not moved since the wind-up began.
                    // ADDED, not set: `= m.Tick` threw away the overshoot, so at 60 fps each judgement
                    // came a frame late (16 frames, not 15) and a full burn was 12 of them, not 13.
                    s.Next += m.Tick;
                    var (la, lb) = Segment(m.Id);
                    foreach (var p in _hittable)
                        if (Combat.DistToSegment(p.Position, la, lb) <= m.Width / 2f + p.HitRadius)
                            p.Hit(Out(m), Position, m.Source);
                    if (--s.Left <= 0) s.At = Phase.Idle;   // its last judgement ends it (Judgements)
                }
                break;
            case MoveWay.Dash:
                Position = Position.MoveToward(s.To, m.Speed * (float)delta);
                foreach (var p in _hittable) if (Covers(p.Position, p.HitRadius)) p.Hit(Out(m), Position, m.Source);
                if (Position.DistanceTo(s.To) < 1f) s.At = Phase.Idle;
                break;
            case MoveWay.Throw:
                if (!IsInstanceValid(_rock) || _rock.Done) s.At = Phase.Idle;
                break;
        }
    }

    // Count directions, Spread degrees apart, centred on the aim: three at 25 are -25, 0, +25;
    // seven at 10 are -30 to +30; one is the aim itself.
    private static IEnumerable<float> Fan(float aim, BossMove m)
    {
        for (int k = 0; k < m.Count; k++) yield return aim + Mathf.DegToRad((k - (m.Count - 1) / 2f) * m.Spread);
    }
    private Vector2 Nose => ToGlobal(new Vector2(0, -Length * 0.5f));
    // Where a move's beam goes RIGHT NOW: straight out of the nose, as far as its row reaches. The
    // warning is a child of the boss drawn down the same axis, so the drawing and the hit are the
    // same line by construction.
    public (Vector2 a, Vector2 b) Segment(string id)
    {
        var nose = Nose;
        return (nose, nose + Vector2.Up.Rotated(Rotation) * (S(id)?.M.Reach ?? 0f));
    }

    // A MOVE'S ESCORTS: EscortAngle to port and to starboard of the nose, EscortOut off the hull,
    // straight at the pilot, boosting for the whole wind-up and fragile on purpose (point defence
    // must be able to kill them inside it). Returns roughly how long until their web should land --
    // the wind-up's floor if they are all shot down first.
    // HOW MANY A MOVE LAUNCHES AT THIS LEVEL: its row's own count, and ONE MORE EVERY TEN LEVELS
    // to a ceiling of five. A move that calls nothing calls nothing however high the level goes, so
    // this belongs to every boss with escorts rather than to the one that happened to have them
    // first. They fan across the same arc whatever the count, so five need no new formation.
    public const int EscortStep = 10, EscortMax = 5;
    public static int EscortsAt(BossMove m, int level) =>
        m == null || m.Escorts <= 0 ? 0
        : System.Math.Min(EscortMax, m.Escorts + System.Math.Max(0, (level - 1) / EscortStep));

    public double LaunchEscorts(string move, Node2D target)
    {
        var s = S(move); var m = s?.M;
        if (!Net.Sim || m == null || target == null || Hub == null) return Raider.EscortShiver;
        var nose = Vector2.Up.Rotated(Rotation);
        double eta = 0;
        int n = EscortsAt(m, Missions.Level);
        s.Escorts.Clear();
        for (int k = 0; k < n; k++)
        {
            float side = n > 1 ? (2f * k / (n - 1) - 1f) * m.EscortAngle : 0f;
            var dir = nose.Rotated(Mathf.DegToRad(side));
            var at = Position + dir * m.EscortOut;
            var r = Hub.SpawnRaider(at, m.EscortKind, 0, Missions.Level);
            // the one launched to port flanks to port, the other to starboard
            r?.Escort(target, dir, m.Windup, m.EscortHull * Par.CraftScale(Missions.Level), side < 0 ? -Mathf.Pi / 2f : Mathf.Pi / 2f);
            if (r != null) s.Escorts.Add(r);
            eta = System.Math.Max(eta, Raider.WebEta(at.DistanceTo(target.Position)));
        }
        return eta;
    }

    // A THROWN BODY: Offset off the flank toward where it aims, a lane Reach long, and the body
    // itself -- the same one on every peer, from one event (ThrownRock). It names the blow it
    // deals, and strikes every live pilot in its lane, seen or not.
    private ThrownRock _rock;
    public ThrownRock Rock => IsInstanceValid(_rock) ? _rock : null;
    private void Heave(Slot s)
    {
        var m = s.M;
        var side = Vector2.Right.Rotated(Rotation);
        if (side.Dot(s.Spot - Position) < 0) side = -side;                     // on the flank toward where it aims
        s.From = Position + side * m.Offset;
        s.To = s.From + (s.Spot - s.From).Normalized() * m.Reach;
        int variant = s.Fired % 2;                                            // the two bodies ThrownRock draws
        _rock = new ThrownRock { Boss = this, From = s.From, To = s.To, Hold = m.Windup, Flight = m.Flight,
                                 Damage = Out(m), Radius = m.Radius, Variant = variant };
        GetParent().AddChild(_rock);
        if (Net.IsOnline) Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetRock), s.From, s.To, m.Windup, m.Flight, m.Radius, variant);
    }

    // A guest's body: the same one, from the same event -- its hold shortened by the trip here,
    // never its flight (see ThrownRock). A catch-up sends what is left of the hold: less than
    // nothing once it is in flight, which starts it that far along its lane -- and a round trip
    // further, as a warning here ends a round trip early. It replaces any body shown.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRock(Vector2 from, Vector2 to, double hold, double flight, float radius, int variant)
    {
        if (IsInstanceValid(_rock)) _rock.QueueFree();
        GetParent().AddChild(_rock = new ThrownRock { Boss = this, From = from, To = to, Hold = hold > 0 ? Net.Arriving(hold) : hold - Net.RoundTrip, Flight = flight,
                                                      Radius = radius, Variant = variant, Cosmetic = true });
    }

    // host: raise this move's warning here and on every guest in the arena -- one ROW of Fx.All,
    // a lane or a zone, replicated by the one RPC that carries every effect (Fx.On -> Hub.NetFx,
    // which sends to the sector this is: the arena). A boss's warning was a node of its own and
    // an RPC of its own. The clock and the two sounds are the MOVE's row, and travel with the
    // raise; a guest shortens the wind-up by its own round trip when it arrives (Hub.NetFx).
    // onHull: a and b are in the BOSS'S OWN FRAME and the warning is anchored to this hull by its
    // NetId, so it swings with the hull instead of being pinned to the spot the boss stood on
    // when it drew it. Guests already follow the boss's pose from NetState, so their copy tracks
    // too -- no per-frame line updates over the wire.
    // width: a move whose warning is as wide as the thing it warns about passes that width here
    // rather than keeping a second copy of it in its row (MoveWay.Throw). 0 means "the row's".
    private void Lane(BossMove m, Vector2 a, Vector2 b, bool onHull = false, double hold = 0, float width = 0) =>
        Fx.Warn(new FxRaise { Id = Fx.WarnLane, At = a, To = b, Size = width > 0 ? width : m.Width, Time = m.Windup, Hold = hold,
                              Anchor = onHull ? NetId : Fx.World, Cue = m.Cue, Strike = m.Strike });
    private void Zone(Vector2 at, float radius, double time, string cue, string strike) =>
        Fx.Warn(new FxRaise { Id = Fx.WarnZone, At = at, To = at, Size = radius, Time = time, Cue = cue, Strike = strike });

    // host: a special move's sound, here and on every guest in the arena -- for a move with no
    // warning to carry it (a warning plays its move's cue and strike itself)
    protected void Sound(string name, Vector2 at)
    {
        Sfx.Special(name, at);
        if (Net.IsOnline) Hub?.SendBossSound(name, at);
    }

    // A PILOT ARRIVING MID-FIGHT -- a rejoin, a slow load -- missed every warning already up (and the
    // Drake's rock, if one is thrown): each is sent once, as it starts, to the peers in the arena
    // then. The host sends such a peer what is up now, as it stands (Hub.NetMySector). A warning's
    // opening sound is not replayed; its landing sound still plays.
    public void CatchUp(int peer)
    {
        foreach (var n in Fx.Warnings)
        {
            double left = n.Time - n.Elapsed, hold = n.Hold;
            if (left < 0) { hold += left; left = 0; }
            if (left <= 0 && hold <= 0) continue;                  // landed: only its flash is left
            Hub?.FxTo(peer, new FxRaise { Id = n.Id, At = n.Position, To = n.To, Size = n.Radius, Time = left, Hold = hold,
                                          Since = n.Since + n.Elapsed, Anchor = n.Anchor, Cue = n.Cue, Strike = n.Strike });
        }
        if (Rock is { Done: false } r) RpcId(peer, nameof(NetRock), r.From, r.To, r.Hold - r.Elapsed, r.Flight, r.Radius, r.Variant);
    }

    // the host's report of this boss, as Hub.NetBoss hands it on (it can outlive the arena)
    public void TakeState(Vector2 p, float rot, double hp, bool locked, double nextSuper, double superGap, double hullMult)
    {
        if (!_net.Has) _hullWatch = default;       // the host's first figure is where this peer starts counting
        // the host's scale too: a guest built its boss for the party it saw, and one that rejoined
        // mid-fight saw a different party
        _net.Set(p, rot); Hp = hp; _netLocked = locked; HullMult = hullMult;
        _netNextSuper = nextSuper; _netSuperGap = superGap;
    }
}
