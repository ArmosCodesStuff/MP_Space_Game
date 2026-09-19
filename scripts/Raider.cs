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
//   It fires a 1 DPS laser (the raider damage "x"; a heavy's is 2x -- chunk 6b).
//
// Targets: the nearest player ship, miner, salvager or hauler.
// ─────────────────────────────────────────────────────────────────────────────
public enum RaiderKind { Light }

public partial class Raider : Node2D, IHittable
{
    public Hub Hub;
    public RaiderKind Kind = RaiderKind.Light;
    public int NetId { get; set; }
    public double Hp = LightHull;
    public bool Alive => Hp > 0;
    public float HitRadius => Length * 0.4f;
    public bool Selectable => true;

    public const float Length = 34f;                   // twice a carrier fighter
    public const double LightHull = 25;
    public const double RaiderDps = 1.0;               // x
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
    public bool Latched { get; private set; }
    public bool Boosting => _boostLeft > 0;
    public float Speed { get; private set; }
    private double _boostLeft, _shot;
    private bool _boostUsed;
    private Vector2 _netPos; private float _netRot; private bool _hasNet;
    private Vector2? _tether;                          // guests: where the web goes
    private Sprite2D _sprite;

    public override void _Ready()
    {
        var tex = GD.Load<Texture2D>("res://enemy_light_fighter.png");
        _sprite = new Sprite2D { Texture = tex, Scale = Vector2.One * (Length / tex.GetHeight()) };
        AddChild(_sprite);
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
        if (!Up(Target)) { Target = Hub.RaiderTargets().OrderBy(t => t.Position.DistanceTo(Position)).FirstOrDefault(); Latched = false; _boostUsed = false; }
        if (Target == null) { Speed = 0; QueueRedraw(); return; }

        // my post around the target: ahead, left or right by its heading, 90 u out
        var mates = Hub.Raiders.Where(r => r.Alive && r.Target == Target).OrderBy(r => r.NetId).ToList();
        int slot = Mathf.Max(0, mates.IndexOf(this)) % Posts.Length;
        var postDir = Vector2.Up.Rotated(Target.Rotation + Posts[slot]);
        var post = Target.Position + postDir * (Extent(Target, postDir) + Hold);
        float toTarget = Position.DistanceTo(Target.Position);

        if (!_boostUsed && toTarget <= BoostAt) { _boostUsed = true; _boostLeft = BoostTime; }
        _boostLeft = System.Math.Max(0, _boostLeft - delta);
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
                Strike(Target, RaiderDps * ShotEvery);
                Combat.Flash(Position, Target.Position, new Color(1f, 0.3f, 0.25f));
            }
        }
        QueueRedraw();
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
        if (Boosting) Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, Length * 1.6f, new Color(1f, 0.35f, 0.25f), 1f, true);
        else Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, Length, new Color(1f, 0.35f, 0.25f), 0.5f, Speed > 1f);
    }
}
