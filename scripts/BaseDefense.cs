using Godot;
using System.Linq;

// THE BASE'S OWN WEAPONS -- a laser and a tracking missile, on a turret at the station's
// heart. Host-simulated (its shots reach guests like every other shot); it fires only at
// RAIDERS -- never at the practice dummies. Ranges are from the base's centre.
//   LASER    5 DPS (2.5 every 0.5 s) at the nearest raider within 300 u
//   MISSILE  every 5 s, 25 damage, tracking, 120 u/s (1.2x a capital ship's average top
//            speed), at the nearest raider within 600 u
public partial class BaseDefense : Node2D
{
    public Hub Hub;
    public const float LaserRange = 300f, MissileRange = 600f, MissileSpeed = 120f;
    public const double LaserDps = 5, LaserTick = 0.5, MissileDamage = 25, MissileEvery = 5;
    public double LaserDealt { get; private set; }          // for the record (and the smoke test)
    public int MissilesFired { get; private set; }
    private double _laser, _missile = 1.0;
    private Sprite2D _turret;

    public override void _Ready()
    {
        Name = "BaseDefense";
        Position = Hub.BasePos + new Vector2(0, -60);          // on the station's upper deck
        var tt = GD.Load<Texture2D>("res://turret_bs_main.png");
        _turret = new Sprite2D { Texture = tt, Scale = Vector2.One * 0.75f, Modulate = new Color(0.78f, 0.82f, 0.9f) };
        AddChild(_turret);
        ZIndex = 4;
    }

    private Raider Nearest(float within) => Combat.Hostiles.OfType<Raider>()
        .Where(r => r.Alive && r.Position.DistanceTo(Hub.BasePos) <= within)
        .OrderBy(r => r.Position.DistanceTo(Hub.BasePos)).FirstOrDefault();

    public override void _Process(double delta)
    {
        var aim = Nearest(MissileRange);
        if (aim != null) _turret.GlobalRotation = (aim.Position - GlobalPosition).Angle() + Mathf.Pi / 2f;
        if (!Net.Sim) return;                                   // guests: the turret tracks; the host fires
        _laser -= delta; _missile -= delta;
        var close = Nearest(LaserRange);
        if (close != null && _laser <= 0)
        {
            _laser = LaserTick;
            double hit = LaserDps * LaserTick;
            close.TakeDamage(hit); LaserDealt += hit;
            Combat.Flash(GlobalPosition, close.Position, new Color(0.6f, 0.9f, 1f));
        }
        if (aim != null && _missile <= 0)
        {
            _missile = MissileEvery; MissilesFired++;
            var dir = (aim.Position - GlobalPosition).Normalized();
            Combat.LaunchTorpedo(GlobalPosition + dir * 20f, dir, MissileSpeed, MissileRange * 1.6f, MissileDamage, aim.NetId, 2.5f);
        }
    }
}
