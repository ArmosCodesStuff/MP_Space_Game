using Godot;

// A TELEGRAPH: the red warning for a high-damage attack, shown for its whole wind-up
// so pilots can get out. A LINE (a beam: from, to, width) or a CIRCLE (centre,
// radius). It fills as the moment approaches, then flashes when the attack lands and
// frees itself. Pure visuals: the host resolves the hit when the time is up.
public partial class Telegraph : Node2D
{
    public bool Line;
    public Vector2 A, B;           // line: from and to (world); circle: A is the centre
    public float Width, Radius;
    public double Duration;
    private double _t;
    private const double Flash = 0.35;

    public override void _Ready() { ZIndex = 7; ZAsRelative = false; }

    public override void _Process(double delta)
    {
        _t += delta;
        if (_t > Duration + Flash) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = (float)Mathf.Clamp(_t / Duration, 0, 1);
        bool firing = _t >= Duration;
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)_t * (8f + 10f * k));
        var edge = new Color(1f, 0.15f, 0.12f, firing ? 1f : 0.55f + 0.35f * pulse);
        var fill = firing ? new Color(1f, 0.85f, 0.8f, 0.7f * (float)(1 - (_t - Duration) / Flash)) : new Color(1f, 0.1f, 0.08f, 0.10f + 0.12f * k);
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
