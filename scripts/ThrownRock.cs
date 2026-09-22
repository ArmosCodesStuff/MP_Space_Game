using Godot;
using System.Collections.Generic;

// THE DRAKE BASTION'S THROWN ROCK. A big asteroid appears beside the boss, held in its tractor beam
// through the wind-up, then hurled down a straight lane -- slow to start, then very fast (the distance
// flown goes as the cube of the time) -- striking every ship in its path once, and breaking apart at
// the lane's end. Its whole path is fixed when the throw starts (from, to, hold, flight), so every peer
// flies the same rock from one event and no positions are streamed; a guest shortens only the HOLD
// (Net.Arriving), never the flight, so the rock it draws is where the host's hit lands. A Hold below
// zero (a peer that arrived mid-flight, Drake.CatchUp) starts it that far into its flight. A rock
// whose boss is down threatens nothing: it goes with it.
public partial class ThrownRock : Node2D
{
    public Boss Boss;
    public Vector2 From, To;
    public double Hold, Flight, Damage;
    public float Radius;
    public int Variant;
    public bool Cosmetic;
    private double _t, _broken = -1;
    private const double BreakTime = 0.8;
    private Vector2 _last;
    private readonly HashSet<PlayerShip> _struck = new();
    private Sprite2D _sprite;
    public bool Thrown => _t >= Hold;
    public double Elapsed => _t;
    public bool Done => _broken >= 0;
    // the share of the lane flown `s` seconds into a flight of `flight`: the cube -- slow, then very fast
    public static float Flown(double s, double flight) => (float)System.Math.Pow(System.Math.Clamp(s / flight, 0, 1), 3);

    public override void _Ready()
    {
        ZIndex = 5; Position = _last = From;
        // fitted by its LONGER side, so the rock drawn is never wider than the lane or the hit it deals
        // (the art is wider than it is tall, and fitting by height drew it 20 u past the lane's edges)
        var tex = GD.Load<Texture2D>(Variant == 0 ? "res://boss_rock_1.png" : "res://boss_rock_2.png");
        _sprite = new Sprite2D { Texture = tex, Scale = Vector2.One * (Radius * 2f / Mathf.Max(tex.GetWidth(), tex.GetHeight())) };
        AddChild(_sprite);
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Boss) || !Boss.Alive) { QueueFree(); return; }
        if (_broken >= 0)
        {
            if ((_broken += delta) >= BreakTime) QueueFree();
            QueueRedraw();
            return;
        }
        _t += delta;
        if (_t < Hold) _sprite.Rotation += 0.4f * (float)delta;          // held, turning slowly in the beam
        else
        {
            float f = Flown(_t - Hold, Flight);
            Position = From.Lerp(To, f);
            _sprite.Rotation += 3f * (float)delta;
            if (!Cosmetic && Net.Sim)
            {   // every ship it passes through, once: swept from last frame's place in short steps
                int n = Mathf.Max(1, Mathf.CeilToInt(_last.DistanceTo(Position) / 12f));
                for (int i = 1; i <= n; i++)
                {
                    var p = _last.Lerp(Position, (float)i / n);
                    foreach (var h in Combat.Players)
                        if (h is PlayerShip ps && ps.Alive && !_struck.Contains(ps) && ps.Covers(p, Radius))
                        { _struck.Add(ps); ps.Hit(Damage, p, "boss:rock"); }
                }
            }
            _last = Position;
            if (f >= 1f)
            {   // the lane's end: it breaks apart
                _broken = 0; _sprite.Visible = false; Sfx.Special("drake_rock", Position);
                GetParent().AddChild(new Explosion { Position = Position, Radius = Radius * 1.2f });
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_broken >= 0)
        {   // its pieces fly out and fade
            float k = (float)(_broken / BreakTime);
            for (int i = 0; i < 8; i++)
            {
                var d = Vector2.Right.Rotated(i * Mathf.Tau / 8f + Variant) * Radius * (0.3f + 1.6f * k);
                DrawCircle(d, Radius * 0.22f * (1f - 0.5f * k), new Color(0.45f, 0.33f, 0.24f, 1f - k));
            }
            return;
        }
        if (_t < Hold)
        {   // THE TRACTOR: a pale green shaft from the boss's nose to the rock, and its grip round it
            var nose = ToLocal(Boss.ToGlobal(new Vector2(0, -Boss.Length * 0.5f)));
            Beam.Draw(this, nose, Vector2.Zero, _t, new Color(0.4f, 1f, 0.7f, 0.16f), new Color(0.5f, 1f, 0.8f, 0.4f),
                      new Color(0.9f, 1f, 0.95f, 0.9f), new Color(0.6f, 1f, 0.85f, 0.5f), 1.6f);
            DrawArc(Vector2.Zero, Radius * 1.05f, 0, Mathf.Tau, 40, new Color(0.5f, 1f, 0.8f, 0.5f), 3f);
        }
    }
}
