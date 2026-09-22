using Godot;

// A TELEGRAPH: the red warning for a high-damage attack, shown for its whole wind-up
// so pilots can get out. A LINE (a beam: from, to, width) or a CIRCLE (centre,
// radius). It fills as the moment approaches, then flashes when the attack lands --
// and stays lit for as long as the attack itself lasts (Hold: the death beam burns for
// 3 s) -- then frees itself. Pure visuals: the host resolves the hit when the time is up. It
// carries the attack's sounds too, so every peer that draws it hears them in step with it.
public partial class Telegraph : Node2D
{
    public bool Line;
    public Vector2 A, B;           // line: from and to (world); circle: A is the centre
    public float Width, Radius;
    public double Duration, Hold;
    // Cue plays as the warning goes up, Strike as the attack lands (Sfx.Special; either may be null)
    public string Cue, Strike;
    private double _t;
    public double Elapsed => _t;
    private const double Flash = 0.35;

    public override void _Ready() { ZIndex = 7; ZAsRelative = false; }

    public override void _Process(double delta)
    {
        if (_t == 0 && Cue != null) Sfx.Special(Cue, ToGlobal(A));
        bool winding = _t < Duration;
        _t += delta;
        if (winding && _t >= Duration && Strike != null) Sfx.Special(Strike, ToGlobal(A));
        if (_t > Duration + Hold + Flash) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = (float)Mathf.Clamp(_t / Duration, 0, 1);
        bool firing = _t >= Duration;
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)_t * (8f + 10f * k));
        var edge = new Color(1f, 0.15f, 0.12f, firing ? 1f : 0.55f + 0.35f * pulse);
        var fill = firing ? new Color(1f, 0.85f, 0.8f, 0.7f * (float)(1 - System.Math.Max(0, _t - Duration - Hold) / Flash)) : new Color(1f, 0.1f, 0.08f, 0.10f + 0.12f * k);
        if (Line)
        {
            var dir = (B - A).Normalized(); var side = new Vector2(-dir.Y, dir.X) * Width / 2f;
            DrawColoredPolygon(new[] { A + side, B + side, B - side, A - side }, fill);
            if (!firing)   // the wind-up creeps down the beam's length
                DrawColoredPolygon(new[] { A + side, A + (B - A) * k + side, A + (B - A) * k - side, A - side }, new Color(1f, 0.15f, 0.1f, 0.22f));
            DrawPolyline(new[] { A + side, B + side, B - side, A - side, A + side }, edge, 2.5f);
        }
        else
        {
            DrawCircle(A, Radius, fill);
            if (!firing) DrawCircle(A, Radius * k, new Color(1f, 0.15f, 0.1f, 0.20f));   // the wind-up grows outward
            DrawArc(A, Radius, 0, Mathf.Tau, 72, edge, 3f);
        }
    }
}
