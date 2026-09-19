using Godot;

// A short burst where something is destroyed (or appears): a flash and a ring, then gone.
public partial class Explosion : Node2D
{
    public float Radius = 30f;
    public Color Tint = new(1f, 0.7f, 0.3f);
    public const double Life = 0.6;
    private double _t;
    public override void _Ready() { ZIndex = 7; ZAsRelative = false; }
    public override void _Process(double delta) { _t += delta; if (_t >= Life) QueueFree(); QueueRedraw(); }
    public override void _Draw()
    {
        float k = (float)(_t / Life);
        DrawCircle(Vector2.Zero, Radius * (0.4f + 0.8f * k), new Color(Tint.R, Tint.G, Tint.B, 0.55f * (1 - k)));
        DrawArc(Vector2.Zero, Radius * (0.6f + 1.2f * k), 0, Mathf.Tau, 32, new Color(1f, 0.9f, 0.7f, 0.8f * (1 - k)), 2f);
    }
}
