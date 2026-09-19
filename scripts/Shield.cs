using Godot;

// A hexagonal shield projection, lit on the side a hit came from.
//
// One generic panel serves all four sides: a band of hexagons around the hull's
// ellipse, spanning +-50 degrees about the side's direction (front, right, back,
// left, relative to the ship's heading). A hit lights the side nearest the impact
// and it fades out; several sides can glow at once. Purely visual -- damage is
// the host's business.
public partial class ShieldFlash : Node2D
{
    public float HalfWidth = 30f, HalfLength = 60f;    // the hull's half extents, world units
    public Color Tint = new(0.55f, 0.88f, 1f);         // the owner's accent: shields are "misc lighting"
    private readonly float[] _glow = new float[4];     // front, right, back, left
    private const float Fade = 0.7f;
    private bool _wasLit;

    // localAngle: the impact direction in the ship's frame, 0 = straight ahead (-y).
    public void Flash(float localAngle)
    {
        int side = Mathf.PosMod(Mathf.RoundToInt(localAngle / (Mathf.Pi / 2f)), 4);
        _glow[side] = 1f;
        QueueRedraw();
    }

    public float Glow(int side) => _glow[side];

    public override void _Ready() => ZIndex = 6;

    public override void _Process(double delta)
    {
        bool any = false;
        for (int i = 0; i < 4; i++) { _glow[i] = Mathf.Max(0f, _glow[i] - (float)delta / Fade); any |= _glow[i] > 0; }
        // redraw while lit AND once more as it goes out: skipping that last redraw left
        // the final faint frame of hexagons on screen for good
        if (any || _wasLit) QueueRedraw();
        _wasLit = any;
    }

    public override void _Draw()
    {
        float a = HalfWidth * 1.45f + 10f, b = HalfLength * 1.12f + 10f;   // the shield's ellipse
        float hex = Mathf.Clamp(HalfLength / 9f, 4.5f, 9f);                 // hexagon size scales with the ship
        float w = hex * Mathf.Sqrt(3f), h = hex * 1.5f;
        for (int side = 0; side < 4; side++)
        {
            float g = _glow[side];
            if (g <= 0) continue;
            float centre = side * Mathf.Pi / 2f;
            for (float y = -b - hex; y <= b + hex; y += h)
            {
                int row = Mathf.RoundToInt((y + b) / h);
                for (float x = -a - w + (row % 2) * w * 0.5f; x <= a + w; x += w)
                {
                    var p = new Vector2(x, y);
                    float e = Mathf.Sqrt((x * x) / (a * a) + (y * y) / (b * b));   // 1 on the ellipse
                    if (e < 0.80f || e > 1.02f) continue;                            // a band, not a disc
                    float ang = Mathf.Atan2(x, -y);                                  // 0 = ahead, clockwise
                    float off = Mathf.Abs(Mathf.AngleDifference(centre, ang));
                    if (off > Mathf.DegToRad(50f)) continue;
                    float k = g * (1f - off / Mathf.DegToRad(50f)) * (1f - Mathf.Abs(e - 0.91f) * 4f);
                    if (k <= 0.02f) continue;
                    var pts = new Vector2[7];
                    for (int i = 0; i < 7; i++) pts[i] = p + Vector2.Right.Rotated(Mathf.Pi / 6f + i * Mathf.Pi / 3f) * hex * 0.92f;
                    DrawColoredPolygon(pts[..6], new Color(Tint.R, Tint.G, Tint.B, 0.10f * k));
                    DrawPolyline(pts, new Color(Tint.R, Tint.G, Tint.B, 0.75f * k), 1.1f);
                }
            }
        }
    }
}
