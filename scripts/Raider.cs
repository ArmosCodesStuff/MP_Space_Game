using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// RAIDERS -- enemy fighters. Host-simulated; guests draw them from the host's
// 10 Hz state. They are hostile: selectable, and every player weapon can hit them.
//
//   LIGHT (a webifier): cruises as slow as an unupgraded capital ship, then, once
//   within BoostAt of its target, boosts to 500% for 3 s -- which lands it at its post
//   rather than on top of the target. Lights hold posts AHEAD, LEFT and RIGHT of the
//   target (by its heading), 90 u out: inside 90% of their 100 u reach, so they never
//   lose damage to repositioning. Held there, a light PINS its target: the target is
//   kept to 20% of its top speed, thrusting forward, unable to turn -- a soft lock.
//   It fires a 1 DPS laser (the raider damage "x").
//
//   HEAVY: a snub-nosed gunship -- 4x a light's length, one turret. It waits at the MAP'S
//   EDGE nearest its target, facing it, until the target is pinned; then it boosts at 700%
//   until 300 u away, and closes at cruise to 135 u off the hull, astern, where it fires a
//   short, hard laser: 2x the raider damage. Within its own row's missile reach (a gunship's
//   500 u) it also fires a fat missile at where the target WILL be in 7 s (its speed carried
//   forward): a red circle marks the spot for all 7 s, and the blast lands there -- move off the
//   line and it misses.
//
// Targets: the nearest player ship, miner, salvager or hauler -- except a HUNTER, sent after
// one quarry (the hauler on an escort), which goes for its quarry while it is there.
//
// PATROLS: 3 lights and 1 heavy circle a perimeter round the base (1800 u out) together.
// When a target comes within 2000 u of the patrol, its lights break off to tackle it, and
// its heavy takes the SAME target and waits astern for the pin. (A raider on its own,
// in no patrol, simply goes for the nearest target.)
// ─────────────────────────────────────────────────────────────────────────────
public partial class Raider : Node2D, IHittable, ITagged, IStatused
{
    public Hub Hub;
    // WHICH enemy this is: a row of Enemies.All. The index is what goes on the wire.
    public int Kind = Enemies.Webifier;
    public EnemyDef Def => Enemies.Of(Kind);
    public int NetId { get; set; }
    public double Hp;
    public bool Alive => Hp > 0;
    public bool Heavy => Def.Way == EnemyWay.Standoff;
    public Tag Tags => Def.Tag;
    // what is being done to it: a warrior's EMP holds it still (Statuses). Host-decided; a guest
    // sees it stop because the host stops sending it anywhere.
    private StatusSet _status;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status st, double seconds) { if (Net.Sim) _status.Apply(st, seconds); }
    public float Length => Def.Length;
    // a raid's raiders are as strong as the boss that was failed: S(L) = 1.1^(L-1)
    public double Strength = 1;          // S(L) (was "Scale", which hid Node2D.Scale)
    // The share of that hull it is built with: an escort's hunters come at half (Hub.HunterHull).
    // Not Strength, which scales its damage too. Set before it enters the tree (_Ready reads it).
    public double HullShare = 1;
    // Speed and turning, x: an escort's hunters fly a very little quicker the harder the escort
    // (Hub.ThreatAgility). The host flies every raider; guests follow where it says.
    public double Agility = 1;
    private HullWatch _hullWatch;
    public double MaxHull => Def.Hull * Strength * HullShare;
    public float HitRadius => Length * Def.HitShare;
    public bool Selectable => true;

    // The two the rest of the game names by hand -- the plain webifier and the plain gunship.
    // Every figure comes from their rows (Enemies.All), so there is one place a number lives.
    public static float LightLength => Enemies.Of(Enemies.Webifier).Length;      // twice a carrier fighter
    public static float HeavyLength => Enemies.Of(Enemies.Gunship).Length;
    public static double RaiderDps => Enemies.Of(Enemies.Webifier).Dps;          // x -- the game's damage unit
    public static double HeavyDps => Enemies.Of(Enemies.Gunship).Dps;
    public static float HeavyReach => Enemies.Of(Enemies.Gunship).Reach;
    private const float HeavyBoostStop = 300f;          // boosting in, until this close, then at cruise
    // THE MISSILE IS THE ROW'S: EnemyDef.MissileRange / MissileEvery / MissileDamage /
    // BlastRadius, read through Def. These two are what the rest of the game names by hand, and
    // both are the plain gunship's figures.
    // TWELVE SECONDS IN THE AIR. It was seven; the owner asked for twelve and about 40% more
    // damage with it, so the answer to a heavy's missile is to be somewhere else when it lands
    // rather than to tank it. A longer flight is a LONGER GUESS, and Missiles.Predict leads by
    // exactly the flight it is handed, so the figure lives here once and nowhere else.
    // HOW a predicted missile flies, telegraphs and lands is Missiles.cs, whosever it is: the
    // outposts throw the same one back (Lanes.cs), which is why none of it is in this file.
    public const double MissileFlight = 12.0;
    public static float BlastRadius => Enemies.Of(Enemies.Gunship).BlastRadius;
    public const float PerimeterR = 1800f, Detect = 2000f, PatrolSpeed = 100f;
    public int Patrol;                                 // 0: on its own
    // WHERE IT WAITS with nothing to fight: a ring of Circuit round Station. The base's own
    // perimeter, unless its wave row named a place to hold (WaveDef.Hold) -- a blockade sits ON
    // the lane it is cutting rather than drifting round the base. The host's own bookkeeping, set
    // at the spawn exactly as Quarry and Agility are; a guest is told where it IS, which is all a
    // guest needs.
    public Vector2 Station = Hub.BasePos;
    public float Circuit = PerimeterR;
    private float _orbit;                              // patrols: its angle round that ring
    public static float PinRange => Enemies.Of(Enemies.Webifier).Reach;
    // How far out a raider of THIS kind lights its boost: far enough that the burn ends at its
    // post rather than on top of the target.
    private float BoostAtMine => Def.Cruise * Def.BoostMult * (float)Def.BoostTime + Def.Reach;
    public static float BoostAt                        // the webifier's: 1600 u
    {
        get { var d = Enemies.Of(Enemies.Webifier); return d.Cruise * d.BoostMult * (float)d.BoostTime + d.Reach; }
    }
    static readonly float[] Posts = { 0f, -Mathf.Pi / 2f, Mathf.Pi / 2f };               // ahead, left, right

    public Node2D Target { get; private set; }
    // WHAT WENT DARK ON IT. A raider re-decides only when its target stops being valid, so a ship
    // that turned invisible for five seconds used to hand it a miner 3000 u away FOR EVER: the new
    // target stayed valid, nothing ever looked back, and one press of stealth sent the whole wave
    // at the base's fleet permanently. It keeps what it lost and takes it back the moment it can
    // see it again -- five seconds of losing you, not a handover.
    private Node2D _dark;
    public Node2D Quarry;                         // a hunter's: set by the host at the spawn
    // An ESCORT (launched by a boss): its target is set, its boost lasts until it is posted
    // (or `boostFor` runs out), and it is fragile.
    public bool IsEscort { get; private set; }
    public const double EscortShiver = 1.0;            // hangs at the launch point, shaking, coming round onto the pilot
    private const float EscortShake = 3f;               // how far the shiver throws it
    private const float EscortPlume = 3f;               // its boost plume: three times the usual reach
    private double _shiver; private Vector2 _shiverHome; private float _escortPost;
    // An ESCORT (launched by a boss): it hangs at the launch point shivering while its nose comes
    // round onto the pilot, then breaks into a long boost and flanks -- one to PORT, one to
    // STARBOARD, holding station the way a raid's lights do. Fragile on purpose.
    public void Escort(Node2D target, Vector2 launchDir, double boostFor, double hull, float post)
    {
        Target = target; IsEscort = true; _boostUsed = true; _boostLeft = boostFor; Hp = hull;
        Rotation = Aim.Along(launchDir);
        _shiver = EscortShiver; _shiverHome = Position; _escortPost = post;
    }
    public bool Shivering => Net.Sim ? _shiver > 0 : (_netFlags & FlagShiver) != 0;
    // Roughly when an escort launched now would have its web on the target: the shiver, then the
    // run in at boost speed. A PREDICTION, not a promise -- the pilot may shoot it down first. The
    // Lancer charges on the actual pin; this is only its floor when every escort is shot down, and
    // (plus a second, at most 5 s) its deadline when they neither pin nor die.
    public static double WebEta(float distance)
    {
        var d = Enemies.Of(Enemies.Webifier);
        return EscortShiver + distance / (d.Cruise * d.BoostMult);
    }
    public bool Latched { get; private set; }
    public bool Boosting => Net.Sim ? _boostLeft > 0 : (_netFlags & FlagBoost) != 0;
    public float Speed { get; private set; }
    private double _boostLeft, _shot, _missileCd = 2.0;
    private Lead _lead;                                // what its target is doing (Missiles.cs)
    private Sprite2D _turret;
    private const float TurretPixels = 66f;            // turret_main.png is 66 px across: the ART's figure, not an enemy's
    private bool _boostUsed;
    private NetPose _net;
    private Vector2? _tether;                          // guests: where the web goes
    private Sprite2D _sprite;

    public override void _Ready()
    {
        Hp = MaxHull;
        _sprite = Sprites.Fit(Def.Texture, Length);
        _sprite.Modulate = Def.Tint;
        AddChild(_sprite);
        if (Def.Turret)
        {   // the main turret on its spine behind the canopy, mounted where ITS OWN ROW says and
            // as wide as its row says -- both shares of its length, so a new hull states where its
            // gun sits instead of being scaled off the gunship's
            _turret = new Sprite2D { Texture = GD.Load<Texture2D>("res://turret_main.png"),
                                     Position = new Vector2(0, Length * Def.TurretAft),
                                     Scale = Vector2.One * (Length * Def.TurretWidth / TurretPixels),
                                     Modulate = Def.Tint, ZIndex = 1 };
            AddChild(_turret);
        }
        ZIndex = 5;
        Combat.Hostiles.Add(this);
    }
    // (the blow that finishes it is shown as it goes: it leaves before its next frame)
    public override void _ExitTree() { _hullWatch.Tick(this, System.Math.Max(0, Hp), taken: false); Combat.Hostiles.Remove(this); }

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;
        Hp -= d;
        if (Hp <= 0) Hub.RaiderDown(this);
    }

    // ── what it can go after: an IRaidTarget in reach ──
    public static bool Up(Node2D t) =>
        GodotObject.IsInstanceValid(t) && t is IRaidTarget r && r.InReach && !Targeting.Hidden(r);
    // How far the target's hull reaches from its centre along `dir`: an ellipse with the
    // hull's half-length and half-width. Posts and reach are measured from the HULL, so a
    // raider holds station beside a long ship, never on top of its bow.
    private static float Extent(Node2D t, Vector2 dir)
    {
        var (len, wid) = t is IRaidTarget r ? r.Extent : (20f, 12f);
        var fwd = Vector2.Up.Rotated(t.Rotation); var side = new Vector2(-fwd.Y, fwd.X);
        float a = dir.Dot(fwd), b = dir.Dot(side);
        return 1f / Mathf.Sqrt(a * a / (len * len) + b * b / (wid * wid));
    }
    public static float Gap(Vector2 from, Node2D t) =>
        from.DistanceTo(t.Position) - Extent(t, (from - t.Position).Normalized());
    void Strike(Node2D t, double d) => (t as IRaidTarget)?.Hit(d, Position, $"raider:{NetId}");

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _hullWatch.Tick(this, System.Math.Max(0, Hp), taken: false);
        if (!Net.Sim)
        {
            _net.Follow(this, dt);
            QueueRedraw();
            return;
        }
        if (!Alive) return;
        _status.Tick(delta);
        if (_status.Has(Status.Disabled)) { Speed = 0; QueueRedraw(); return; }   // stunned: it sits there
        if (_dark != null && !GodotObject.IsInstanceValid(_dark)) _dark = null;
        if (!Up(Target))
        {
            if (Target != null && GodotObject.IsInstanceValid(Target) && Targeting.Hidden(Target)) _dark = Target;
            Target = Choose(); Latched = false; _boostUsed = false;
        }
        else if (_dark != null && !ReferenceEquals(_dark, Target) && Up(_dark))
        {   // it is back: so is the hunt, from wherever this took it
            Target = _dark; Latched = false; _boostUsed = false;
        }
        if (ReferenceEquals(_dark, Target)) _dark = null;
        if (Target == null) { Speed = 0; if (Patrol != 0) Circle(delta); QueueRedraw(); return; }
        // its target's velocity, from frame to frame -- the one tracker every predicted missile in
        // the game reads (Missiles.Lead), jump filter and all
        _lead.Watch(Target.Position, delta);
        if (Heavy) { TickHeavy(delta); QueueRedraw(); return; }

        if (_shiver > 0)
        {   // shaking on the spot, nose swinging onto the pilot -- then it lights up and goes
            _shiver -= delta;
            float ms = Time.GetTicksMsec();
            Position = _shiverHome + new Vector2(Mathf.Sin(ms * 0.061f), Mathf.Cos(ms * 0.083f)) * EscortShake;
            var onto = Aim.Face(Position, Target.Position);
            Rotation = Mathf.LerpAngle(Rotation, onto, Mathf.Clamp(6f * dt, 0f, 1f));
            Speed = 0;
            QueueRedraw();
            return;                                   // the boost is not spent while it sits here
        }

        // My post around the target: ahead, left or right by its heading, 90 u out. An escort
        // keeps the side it was launched on instead of taking a slot, so the pair always flanks.
        //
        // The slot is my rank by NetId among the raiders sharing this target -- counted, not
        // sorted. This ran as Where + OrderBy + ToList + IndexOf EVERY FRAME FOR EVERY LIGHT, so
        // a raid of two patrols did six list allocations and six sorts over the whole raider list
        // per frame. Counting lower NetIds gives the identical index with no allocation at all.
        int slot = 0;
        foreach (var r in Hub.Raiders)
            if (r != this && r.Alive && !r.IsEscort && r.Target == Target && r.NetId < NetId) slot++;
        slot %= Posts.Length;
        var postDir = Vector2.Up.Rotated(Target.Rotation + (IsEscort ? _escortPost : Posts[slot]));
        var post = Target.Position + postDir * (Extent(Target, postDir) + Def.Hold);
        float toTarget = Position.DistanceTo(Target.Position);

        if (!_boostUsed && toTarget <= BoostAtMine) { _boostUsed = true; _boostLeft = Def.BoostTime; }
        _boostLeft = System.Math.Max(0, _boostLeft - delta);
        if (IsEscort && Latched) _boostLeft = 0;                          // posted: the long boost is over
        float top = (Boosting ? Def.Cruise * Def.BoostMult : Def.Cruise) * (float)Agility;
        float d = Position.DistanceTo(post);
        Speed = Mathf.Min(top, d * 6f);                                       // ease onto the post
        // once posted it keeps station however the target moves
        Position = Position.MoveToward(post, (Latched ? Mathf.Max(top, d * 12f) : Speed) * dt);
        Latched = Position.DistanceTo(post) < 12f && Gap(Position, Target) <= Def.Reach;
        var face = Aim.Face(Position, Target.Position);
        Rotation = Mathf.LerpAngle(Rotation, face, Mathf.Clamp(8f * (float)Agility * dt, 0f, 1f));

        if (Latched)
        {
            (Target as IRaidTarget)?.ApplyStatus(Status.Pinned, 0.25);
            _shot -= delta;
            if (_shot <= 0)
            {
                _shot = Def.ShotEvery;
                Strike(Target, Def.Dps * Def.ShotEvery * Strength);
                Combat.Flash(Position, Target.Position, new Color(1f, 0.3f, 0.25f));
            }
        }
        QueueRedraw();
    }

    // what to go after: alone, the nearest target; in a patrol, only once one is within
    // 2000 u of the patrol -- and a patrol's heavy takes whatever its lights have taken
    private Node2D Choose()
    {
        if (Up(Quarry)) return Quarry;
        if (Patrol == 0) return Combat.Nearest(Hub.RaiderTargets(), Position, t => t.Position);
        if (!Heavy && Combat.Nearest(Hub.RaiderTargets(), Position, t => t.Position, Detect) is { } near) return near;
        foreach (var r in Hub.Raiders)                                           // a mate spotted one
            if (r != this && r.Patrol == Patrol && r.Alive && !r.Heavy && Up(r.Target)) return r.Target;
        return null;
    }

    // a patrol with nothing to do circles the ring it was given, together: the perimeter round
    // the base, or -- for a blockade -- a tight ring on the lane it is holding (Station/Circuit)
    private void Circle(double delta)
    {
        float dt = (float)delta;
        if (_orbit == 0f) _orbit = (Position - Station).Angle();
        _orbit += PatrolSpeed / Mathf.Max(1f, Circuit) * dt;
        var spot = Station + Vector2.Right.Rotated(_orbit) * Circuit;
        var step = spot - Position;
        Position = Position.MoveToward(spot, PatrolSpeed * 1.5f * dt);     // catches its moving spot, at a believable pace
        if (step.Length() > 1f) Rotation = Mathf.LerpAngle(Rotation, Aim.Along(step), Mathf.Clamp(4f * dt, 0f, 1f));
        Speed = PatrolSpeed;
    }


    // the heavy: wait at the map's edge until the target is pinned, then boost in; missiles within 500 u
    public static Vector2 EdgeSpot(Vector2 target) =>
        Hub.BasePos + ((target - Hub.BasePos).LengthSquared() > 1f ? (target - Hub.BasePos).Normalized() : Vector2.Right) * Hub.RaidEdge;
    private void TickHeavy(double delta)
    {
        float dt = (float)delta;
        var astern = -Vector2.Up.Rotated(Target.Rotation);                    // it comes in from the rear
        bool pinned = Target is IStatused st && st.Statuses.Has(Status.Pinned);
        var dest = pinned ? Target.Position + astern * (Extent(Target, astern) + Def.Hold)
                          : EdgeSpot(Target.Position);                        // patient, at the map's edge nearest it
        float top = (pinned && Position.DistanceTo(Target.Position) > HeavyBoostStop ? Def.Cruise * Def.BoostMult : Def.Cruise) * (float)Agility;
        float d = Position.DistanceTo(dest);
        Speed = Mathf.Min(top, d * 6f);
        Position = Position.MoveToward(dest, Speed * dt);
        var face = Aim.Face(Position, Target.Position);
        Rotation = Mathf.LerpAngle(Rotation, face, Mathf.Clamp(4f * (float)Agility * dt, 0f, 1f));
        if (_turret != null) _turret.GlobalRotation = face;                  // its one turret tracks the target
        Latched = pinned && Gap(Position, Target) <= Def.Reach;
        if (Latched)
        {
            _shot -= delta;
            if (_shot <= 0)
            {
                _shot = Def.ShotEvery;       // 1 s: slower than a target's 0.52 s invulnerability, so no shot is wasted
                Strike(Target, Def.Dps * Def.ShotEvery * Strength);
                Combat.Flash(ToGlobal(_turret?.Position ?? Vector2.Zero), Target.Position, new Color(1f, 0.35f, 0.25f));
            }
        }
        _missileCd -= delta;
        if (Def.Missiles && _missileCd <= 0 && Position.DistanceTo(Target.Position) <= Def.MissileRange)
        {   // at where it WILL be: its velocity carried the whole flight forward -- from ITS row's
            // reach, on its row's cadence, for its row's damage
            _missileCd = Def.MissileEvery;
            // THE ROW'S DAMAGE, AND NOTHING ELSE. A raider's own hull and guns climb with the
            // level through Strength, but the MISSILE it throws is the same missile at level 40 as
            // at level 1 -- the owner's rule, and the reason a level-40 raid is survivable at all:
            // a blast that scaled with the level would be unavoidable rather than merely heavy.
            // (A boss's projectiles DO scale; that is Missions.Quicken, and it is a boss.)
            Hub.ThrowMissile(new MissileSpec { Side = Missiles.Raid, Damage = Def.MissileDamage,
                                              Blast = Def.BlastRadius, Flight = MissileFlight },
                             Position, Missiles.Predict(Target.Position, _lead.Velocity, MissileFlight), NetId);
        }
    }

    // Boosting and Shivering are read by _Draw and were host-only, so a guest drew neither the
    // escorts' triple-length plume nor any raider's boost plume -- the plume was single-player.
    // Two bits on the same packet that already carries position; a whole array for two booleans
    // would cost more than the state it replicates.
    public const int FlagBoost = 1, FlagShiver = 2;
    public int NetFlags => (Boosting ? FlagBoost : 0) | (Shivering ? FlagShiver : 0);
    private int _netFlags;
    public void SetNet(Vector2 p, float rot, double hp, Vector2? tether, int flags)
    {
        if (!_net.Has) _hullWatch = default;       // the host's first figure is where this peer starts counting
        _net.Set(p, rot); Hp = hp; _tether = tether; _netFlags = flags;
    }
    public Vector2? TetherTo => Net.Sim ? (Latched && Up(Target) ? Target.Position : null) : _tether;

    public override void _Draw()
    {
        if (TetherTo is { } to)   // the web: a faint red line to what it holds
        {
            var inv = GlobalTransform.AffineInverse();
            DrawLine(Vector2.Zero, inv * to, new Color(1f, 0.3f, 0.25f, 0.35f), 1.5f);
        }
        float plume = Heavy ? Length * 0.5f : Length;
        // an escort's run-in is unmistakable: it goes on the boost with a plume three times over
        if (Boosting && IsEscort && !Shivering) Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, plume * EscortPlume, new Color(1f, 0.35f, 0.25f), 1f, true);
        else if (Boosting || (Heavy && Speed > Def.Cruise + 1f)) Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, plume * 1.6f, new Color(1f, 0.35f, 0.25f), 1f, true);
        else Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, plume, new Color(1f, 0.35f, 0.25f), 0.5f, Speed > 1f);
    }
}
