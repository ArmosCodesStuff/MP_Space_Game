using Godot;
using System.Linq;

// A BATTLESHIP SHELL: the main guns' round. It flies straight -- no tracking -- at 4x the
// ship's missile speed, as far as the guns' range, and hits the first hostile it touches
// (never a missile: that is point defence's job). The host's shell does the damage; guests
// fly a cosmetic copy. Its path is swept in short steps each frame, so it cannot pass
// through a small target between frames.
public partial class Shell : Node2D
{
    public const float SpeedMult = 4f;         // x the ship's missile speed
    public Vector2 Dir; public float Speed, Range; public double Damage;
    public bool Cosmetic; public PlayerShip Source;
    private float _flown;

    public override void _Ready() { ZIndex = 6; Rotation = Dir.Angle() + Mathf.Pi / 2f; }

    public override void _Process(double delta)
    {
        float step = Speed * (float)delta;
        var from = Position;
        Position += Dir * step; _flown += step;
        if (!Cosmetic && Net.Sim)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(step / 6f));
            for (int i = 1; i <= n; i++)
            {
                var p = from.Lerp(Position, (float)i / n);
                var hit = Combat.Hostiles.FirstOrDefault(h => h != null && h.Alive && h is not Torpedo && h.Covers(p, 3f));
                if (hit == null) continue;
                hit.TakeDamage(Damage);
                Source?.NoteCombat();
                Sfx.Impact(p);
                QueueFree(); return;
            }
        }
        if (_flown >= Range) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {   // a bright slug with a short glow behind it
        DrawLine(new Vector2(0, 4f), new Vector2(0, 22f), new Color(1f, 0.75f, 0.35f, 0.35f), 3f);
        DrawRect(new Rect2(-1.8f, -5f, 3.6f, 9f), new Color(1f, 0.92f, 0.7f));
        DrawColoredPolygon(new[] { new Vector2(-1.8f, -5f), new Vector2(0, -8f), new Vector2(1.8f, -5f) }, new Color(1f, 0.85f, 0.5f));
    }
}
