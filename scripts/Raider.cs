using Godot;
using System.Linq;

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
//   short, hard laser: 2x the raider damage. Within 500 u it also fires a fat missile at where
//   the target WILL be in 7 s (its speed carried forward): a red circle marks the spot
//   for all 7 s, and the blast lands there -- move off the line and it misses.
//
// Targets: the nearest player ship, miner, salvager or hauler.
//
// PATROLS: 3 lights and 1 heavy circle a perimeter round the base (1800 u out) together.
// When a target comes within 2000 u of the patrol, its lights break off to tackle it, and
// its heavy takes the SAME target and waits astern for the pin. (A raider on its own,
// in no patrol, simply goes for the nearest target.)
// ─────────────────────────────────────────────────────────────────────────────
public enum RaiderKind { Light, Heavy }

public partial class Raider : Node2D, IHittable
{
    public Hub Hub;
    public RaiderKind Kind = RaiderKind.Light;
    public int NetId { get; set; }
    public double Hp;
    public bool Alive => Hp > 0;
    public bool Heavy => Kind == RaiderKind.Heavy;
    public float Length => Heavy ? HeavyLength : LightLength;
    // a raid's raiders are as strong as the boss that was failed: S(L) = 1.1^(L-1)
    public double Strength = 1;          // S(L) (was "Scale", which hid Node2D.Scale)
    public double MaxHull => (Heavy ? HeavyHull : LightHull) * Strength;
    public float HitRadius => Length * (Heavy ? 0.3f : 0.4f);
    public bool Selectable => true;

    public const float LightLength = 34f;              // twice a carrier fighter
    public const float HeavyLength = 4f * LightLength; // 136 u
    public const double LightHull = 25, HeavyHull = 100;
    public const double RaiderDps = 1.0;               // x -- a light's laser; a heavy's is 2x
    public const double HeavyDps = 2 * RaiderDps;
    public const double HeavyShotEvery = 1.0;
    public const float HeavyBoostMult = 7f;            // 700%: a heavy closing on a pinned target...
    public const float HeavyBoostStop = 300f;          // ...until this close, then at cruise
    public const float HeavyReach = 150f, HeavyHold = 0.9f * HeavyReach;
    public const float MissileRange = 500f, BlastRadius = 90f;
    public const double MissileFlight = 7.0, MissileEvery = 12.0, MissileDamage = 30;
    public const float PerimeterR = 1800f, Detect = 2000f, PatrolSpeed = 100f;
    public const float MaxStep = 50f;                  // more than this in one frame is a jump (a warp), not motion
    public const float WildSpeed = 400f;               // faster than this is not flying (4x a capital ship)
    public const float MaxLead = WildSpeed * (float)MissileFlight;   // no sane prediction lands further off

    // Where a heavy's missile aims: the target carried 7 s forward by its velocity -- UNLESS
    // anything is out of place (a wild speed, a broken number, a spot absurdly far off):
    // then right at where the target is now.
    public static Vector2 PredictSpot(Vector2 pos, Vector2 vel)
    {
        bool sane = float.IsFinite(vel.X) && float.IsFinite(vel.Y) && vel.Length() <= WildSpeed;
        var spot = sane ? pos + vel * (float)MissileFlight : pos;
        return float.IsFinite(spot.X) && float.IsFinite(spot.Y) && spot.DistanceTo(pos) <= MaxLead ? spot : pos;
    }
    public int Patrol;                                 // 0: on its own
    private float _orbit;                              // patrols: its angle round the perimeter
    public const double ShotEvery = 1.0;
    public const float Cruise = 100f;                  // an unupgraded capital ship's pace
    public const float BoostMult = 5f;                 // 500%
    public const double BoostTime = 3.0;
    public const float PinRange = 100f;
    public const float Hold = 0.9f * PinRange;         // posted 90 u out
    public const float PinSpeed = 0.2f;                // a pinned ship: 20% of top speed
    public static float BoostAt => Cruise * BoostMult * (float)BoostTime + PinRange;   // 1600 u
    static readonly float[] Posts = { 0f, -Mathf.Pi / 2f, Mathf.Pi / 2f };               // ahead, left, right

    public Node2D Target { get; private set; }
    // An ESCORT (launched by a boss): its target is set, its boost lasts until it is posted
    // (or `boostFor` runs out), and it is fragile.
    public bool IsEscort { get; private set; }
    public const double EscortShiver = 1.0;            // hangs at the launch point, shaking, coming round onto the pilot
    public const float EscortShake = 3f;               // how far the shiver throws it
    public const float EscortPlume = 3f;               // its boost plume: three times the usual reach
    private double _shiver; private Vector2 _shiverHome; private float _escortPost;
    // An ESCORT (launched by a boss): it hangs at the launch point shivering while its nose comes
    // round onto the pilot, then breaks into a long boost and flanks -- one to PORT, one to
    // STARBOARD, holding station the way a raid's lights do. Fragile on purpose.
    public void Escort(Node2D target, Vector2 launchDir, double boostFor, double hull, float post)
    {
        Target = target; IsEscort = true; _boostUsed = true; _boostLeft = boostFor; Hp = hull;
        Rotation = launchDir.Angle() + Mathf.Pi / 2f;
        _shiver = EscortShiver; _shiverHome = Position; _escortPost = post;
    }
    public bool Shivering => _shiver > 0;
    // Roughly when an escort launched now would have its web on the target: the shiver, then the
    // run in at boost speed. A PREDICTION, not a promise -- the pilot may shoot it down first,
    // and the boss commits to its beam on this estimate either way.
    public static double WebEta(float distance) => EscortShiver + distance / (Cruise * BoostMult);
    public bool Latched { get; private set; }
    public bool Boosting => _boostLeft > 0;
    public float Speed { get; private set; }
    private double _boostLeft, _shot, _missileCd = 2.0;
    private Vector2 _lastTargetPos; private Vector2 _targetVel;
    private Sprite2D _turret;
    private bool _boostUsed;
    private Vector2 _netPos; private float _netRot; private bool _hasNet;
    private Vector2? _tether;                          // guests: where the web goes
    private Sprite2D _sprite;

    public override void _Ready()
    {
        Hp = MaxHull;
        var tex = GD.Load<Texture2D>(Heavy ? "res://enemy_heavy_hull.png" : "res://enemy_light_fighter.png");
        _sprite = new Sprite2D { Texture = tex, Scale = Vector2.One * (Length / tex.GetHeight()) };
        AddChild(_sprite);
        if (Heavy)
        {   // the battleship's front turret on its mount (62.4 px down the 150 px half-hull), dark as the hull
            _sprite.Modulate = new Color(0.62f, 0.40f, 0.40f);
            var tt = GD.Load<Texture2D>("res://turret_bs_main.png");
            // the turret mount: row 40.3 of the 100 px snub-nosed hull
            float px = Length / tex.GetHeight();
            _turret = new Sprite2D { Texture = tt, Position = new Vector2(0, (40.3f - tex.GetHeight() / 2f) * px),
                                     Scale = Vector2.One * px * 0.62f, Modulate = new Color(0.62f, 0.40f, 0.40f), ZIndex = 1 };
            AddChild(_turret);
        }
        ZIndex = 5;
        Combat.Hostiles.Add(this);
    }
    public override void _ExitTree() => Combat.Hostiles.Remove(this);

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;
        Hp -= d;
        if (Hp <= 0) Hub.RaiderDown(this);
    }

    // ── what it can go after ──
    static bool Up(Node2D t) => GodotObject.IsInstanceValid(t) && t switch
    {
        PlayerShip p => p.Alive,
        Gatherer g => g.State != Gatherer.St.Destroyed,
        Hauler h => h.State is not (Hauler.St.Destroyed or Hauler.St.Away),
        _ => false,
    };
    // How far the target's hull reaches from its centre along `dir`: an ellipse with the
    // hull's half-length and half-width. Posts and reach are measured from the HULL, so a
    // raider holds station beside a long ship, never on top of its bow.
    public static float Extent(Node2D t, Vector2 dir)
    {
        (float len, float wid) = t switch
        {
            PlayerShip p => (PlayerShip.Art[p.Class].Length * 0.5f, PlayerShip.Art[p.Class].HalfWidth),
            Hauler h => (Hauler.Length * 0.5f * h.VisualScale, Hauler.Length * 0.12f * h.VisualScale),
            _ => (20f, 12f),
        };
        var fwd = Vector2.Up.Rotated(t.Rotation); var side = new Vector2(-fwd.Y, fwd.X);
        float a = dir.Dot(fwd), b = dir.Dot(side);
        return 1f / Mathf.Sqrt(a * a / (len * len) + b * b / (wid * wid));
    }
    public static float Gap(Vector2 from, Node2D t) =>
        from.DistanceTo(t.Position) - Extent(t, (from - t.Position).Normalized());
    void Strike(Node2D t, double d)
    {
        switch (t)
        {
            case PlayerShip p: p.Hit(d, Position, $"raider:{NetId}"); break;
            case Gatherer g: g.TakeDamage(d); break;
            case Hauler h: h.TakeDamage(d); break;
        }
    }
    static void Pin(Node2D t)
    {
        switch (t)
        {
            case PlayerShip p: p.PinFor(0.25); break;
            case Gatherer g: g.PinFor(0.25); break;
            case Hauler h: h.PinFor(0.25); break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!Net.Sim)
        {
            if (_hasNet)
            {
                Position = Position.Lerp(_netPos, Mathf.Clamp(10f * dt, 0f, 1f));
                Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(10f * dt, 0f, 1f));
            }
            QueueRedraw();
            return;
        }
        if (!Alive) return;
        if (!Up(Target)) { Target = Choose(); Latched = false; _boostUsed = false; }
        if (Target == null) { Speed = 0; if (Patrol != 0) Circle(delta); QueueRedraw(); return; }
        // its target's velocity, from frame to frame -- except a JUMP (a warp; over 50 u in one frame,
        // 3000 u/s, nothing flies that fast), which would send the predicted missile miles off
        var moved = Target.Position - _lastTargetPos;
        _targetVel = dt > 0 && moved.Length() < MaxStep ? moved / dt : Vector2.Zero; _lastTargetPos = Target.Position;
        if (Heavy) { TickHeavy(delta); QueueRedraw(); return; }

        if (_shiver > 0)
        {   // shaking on the spot, nose swinging onto the pilot -- then it lights up and goes
            _shiver -= delta;
            float ms = Time.GetTicksMsec();
            Position = _shiverHome + new Vector2(Mathf.Sin(ms * 0.061f), Mathf.Cos(ms * 0.083f)) * EscortShake;
            var onto = (Target.Position - Position).Angle() + Mathf.Pi / 2f;
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
        var post = Target.Position + postDir * (Extent(Target, postDir) + Hold);
        float toTarget = Position.DistanceTo(Target.Position);

        if (!_boostUsed && toTarget <= BoostAt) { _boostUsed = true; _boostLeft = BoostTime; }
        _boostLeft = System.Math.Max(0, _boostLeft - delta);
        if (IsEscort && Latched) _boostLeft = 0;                          // posted: the long boost is over
        float top = Boosting ? Cruise * BoostMult : Cruise;
        float d = Position.DistanceTo(post);
        Speed = Mathf.Min(top, d * 6f);                                       // ease onto the post
        // once posted it keeps station however the target moves
        Position = Position.MoveToward(post, (Latched ? Mathf.Max(top, d * 12f) : Speed) * dt);
        Latched = Position.DistanceTo(post) < 12f && Gap(Position, Target) <= PinRange;
        var face = (Target.Position - Position).Angle() + Mathf.Pi / 2f;
        Rotation = Mathf.LerpAngle(Rotation, face, Mathf.Clamp(8f * dt, 0f, 1f));

        if (Latched)
        {
            Pin(Target);
            _shot -= delta;
            if (_shot <= 0)
            {
                _shot = ShotEvery;
                Strike(Target, RaiderDps * ShotEvery * Strength);
                Combat.Flash(Position, Target.Position, new Color(1f, 0.3f, 0.25f));
            }
        }
        QueueRedraw();
    }

    // what to go after: alone, the nearest target; in a patrol, only once one is within
    // 2000 u of the patrol -- and a patrol's heavy takes whatever its lights have taken
    private Node2D Choose()
    {
        var all = Hub.RaiderTargets().ToList();
        if (Patrol == 0) return all.OrderBy(t => t.Position.DistanceTo(Position)).FirstOrDefault();
        var mates = Hub.Raiders.Where(r => r.Patrol == Patrol && r.Alive && r != this).ToList();
        if (Heavy) return mates.Where(r => !r.Heavy && Up(r.Target)).Select(r => r.Target).FirstOrDefault();
        var near = all.Where(t => t.Position.DistanceTo(Position) <= Detect).OrderBy(t => t.Position.DistanceTo(Position)).FirstOrDefault();
        if (near != null) return near;
        return mates.Where(r => !r.Heavy && Up(r.Target)).Select(r => r.Target).FirstOrDefault();   // a mate spotted one
    }

    // a patrol with nothing to do circles the perimeter round the base, together
    private void Circle(double delta)
    {
        float dt = (float)delta;
        if (_orbit == 0f) _orbit = (Position - Hub.BasePos).Angle();
        _orbit += PatrolSpeed / PerimeterR * dt;
        var spot = Hub.BasePos + Vector2.Right.Rotated(_orbit) * PerimeterR;
        var step = spot - Position;
        Position = Position.MoveToward(spot, PatrolSpeed * 1.5f * dt);     // catches its moving spot, at a believable pace
        if (step.Length() > 1f) Rotation = Mathf.LerpAngle(Rotation, step.Angle() + Mathf.Pi / 2f, Mathf.Clamp(4f * dt, 0f, 1f));
        Speed = PatrolSpeed;
    }

    static bool PinnedNow(Node2D t) => t switch { PlayerShip p => p.Pinned, Gatherer g => g.Pinned, Hauler h => h.Pinned, _ => false };

    // the heavy: wait at the map's edge until the target is pinned, then boost in; missiles within 500 u
    public static Vector2 EdgeSpot(Vector2 target) =>
        Hub.BasePos + ((target - Hub.BasePos).LengthSquared() > 1f ? (target - Hub.BasePos).Normalized() : Vector2.Right) * Hub.RaidEdge;
    private void TickHeavy(double delta)
    {
        float dt = (float)delta;
        var astern = -Vector2.Up.Rotated(Target.Rotation);                    // it comes in from the rear
        bool pinned = PinnedNow(Target);
        var dest = pinned ? Target.Position + astern * (Extent(Target, astern) + HeavyHold)
                          : EdgeSpot(Target.Position);                        // patient, at the map's edge nearest it
        float top = pinned && Position.DistanceTo(Target.Position) > HeavyBoostStop ? Cruise * HeavyBoostMult : Cruise;
        float d = Position.DistanceTo(dest);
        Speed = Mathf.Min(top, d * 6f);
        Position = Position.MoveToward(dest, Speed * dt);
        var face = (Target.Position - Position).Angle() + Mathf.Pi / 2f;
        Rotation = Mathf.LerpAngle(Rotation, face, Mathf.Clamp(4f * dt, 0f, 1f));
        if (_turret != null) _turret.GlobalRotation = face;                  // its one turret tracks the target
        Latched = pinned && Gap(Position, Target) <= HeavyReach;
        if (Latched)
        {
            _shot -= delta;
            if (_shot <= 0)
            {
                _shot = HeavyShotEvery;      // 1 s: slower than a target's 0.52 s invulnerability, so no shot is wasted
                Strike(Target, HeavyDps * HeavyShotEvery * Strength);
                Combat.Flash(ToGlobal(_turret?.Position ?? Vector2.Zero), Target.Position, new Color(1f, 0.35f, 0.25f));
            }
        }
        _missileCd -= delta;
        if (_missileCd <= 0 && Position.DistanceTo(Target.Position) <= MissileRange)
        {   // at where it WILL be: its velocity carried 7 s forward
            _missileCd = MissileEvery;
            Hub.HeavyMissile(Position, PredictSpot(Target.Position, _targetVel), NetId, MissileDamage * Strength);
        }
    }

    public void SetNet(Vector2 p, float rot, double hp, Vector2? tether) { _netPos = p; _netRot = rot; _hasNet = true; Hp = hp; _tether = tether; }
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
        else if (Boosting || (Heavy && Speed > Cruise + 1f)) Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, plume * 1.6f, new Color(1f, 0.35f, 0.25f), 1f, true);
        else Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, plume, new Color(1f, 0.35f, 0.25f), 0.5f, Speed > 1f);
    }
}
