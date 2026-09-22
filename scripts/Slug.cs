using Godot;
using System.Linq;

// A HOSTILE ROUND: a boss's slow, straight, DODGEABLE shot -- the Drake Bastion's main-gun shell and
// its scrap. Not a missile: point defence never sees it (it is not in Combat.Hostiles), because a round
// that is meant to be dodged, and that point defence could delete, would never need dodging. It hits
// the first player ship it touches, under its own hit source (a volley of scrap lands piece by piece),
// and is swept in short steps so it cannot pass through a ship between frames. The host's copy deals
// the damage; guests fly a cosmetic copy (Hub.NetSlug) that bursts where it touches a ship on their
// own screen, and deals nothing.
public partial class Slug : Node2D
{
    public enum Kind { Shell, Scrap }
    public Kind Look;
    public int Variant;                    // scrap: which of its jagged shapes
    public Vector2 Dir;
    public float Speed, Range, Radius;
    public double Damage;
    public string HitSource;
    public bool Cosmetic;
    // A guest's copy starts this far into its flight (seconds): the round trip. The host judges a hit
    // against where the guest's ship was a one-way trip ago, and the round reached the guest a one-way
    // trip late -- so without the lead, the round a guest sidesteps on its own screen was one it had
    // not yet cleared.
    public float Lead;
    private float _flown;
    private double _burst = -1;            // >= 0: spent, bursting
    private const double BurstTime = 0.25;

    public override void _Ready()
    {
        ZIndex = 6; Rotation = Dir.Angle() + Mathf.Pi / 2f;
        _flown = Mathf.Min(Speed * Lead, Range); Position += Dir * _flown;
    }

    public override void _Process(double delta)
    {
        if (_burst >= 0)
        {
            if ((_burst += delta) >= BurstTime) QueueFree();
            QueueRedraw();
            return;
        }
        float step = Speed * (float)delta;
        var from = Position;
        Position += Dir * step; _flown += step;
        int n = Mathf.Max(1, Mathf.CeilToInt(step / 6f));
        for (int i = 1; i <= n; i++)
        {
            var p = from.Lerp(Position, (float)i / n);
            PlayerShip hit = null;
            foreach (var s in Combat.Players)
                if (s is PlayerShip ps && ps.Alive && ps.Covers(p, Radius)) { hit = ps; break; }
            if (hit == null) continue;
            if (!Cosmetic && Net.Sim) hit.Hit(Damage, p - Dir * 10f, HitSource);
            Position = p; Burst(); return;
        }
        if (_flown >= Range) Burst();
        QueueRedraw();
    }

    private void Burst() { _burst = 0; Sfx.Impact(Position); }

    // a scrap piece's jagged outline, one of three, in its own frame (nose up)
    private static readonly Vector2[][] Shards =
    {
        new Vector2[] { new(-7, -6), new(2, -9), new(8, -2), new(5, 7), new(-4, 8), new(-9, 1) },
        new Vector2[] { new(-5, -9), new(6, -7), new(9, 3), new(1, 9), new(-8, 5) },
        new Vector2[] { new(-9, -3), new(-2, -9), new(7, -5), new(8, 6), new(-3, 8) },
    };

    public override void _Draw()
    {
        if (_burst >= 0)
        {   // a quick spray where it struck
            float k = (float)(_burst / BurstTime);
            DrawCircle(Vector2.Zero, Radius * (1f + 1.5f * k), new Color(1f, 0.6f, 0.25f, 0.6f * (1 - k)));
            return;
        }
        if (Look == Kind.Shell)
        {   // a slow, glowing ball with a short tail: easy to see coming
            DrawLine(new Vector2(0, Radius * 0.5f), new Vector2(0, Radius * 3f), new Color(1f, 0.45f, 0.2f, 0.35f), Radius);
            DrawCircle(Vector2.Zero, Radius * 1.4f, new Color(1f, 0.45f, 0.15f, 0.3f));
            DrawCircle(Vector2.Zero, Radius, new Color(1f, 0.62f, 0.3f));
            DrawCircle(Vector2.Zero, Radius * 0.5f, new Color(1f, 0.92f, 0.75f));
        }
        else
        {   // a tumbling shard of hull plate, hot along its edge
            DrawSetTransform(Vector2.Zero, _flown * 0.05f, Vector2.One * (Radius / 9f));
            var shard = Shards[Mathf.PosMod(Variant, Shards.Length)];
            DrawColoredPolygon(shard, new Color(0.42f, 0.30f, 0.24f));
            DrawPolyline(shard.Append(shard[0]).ToArray(), new Color(1f, 0.55f, 0.25f, 0.9f), 1.5f);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
