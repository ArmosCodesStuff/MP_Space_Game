using Godot;

// A GLOWING SHAFT between two points: a steady core inside a soft glow that breathes. One drawing
// for every beam that is a tool rather than a weapon -- a miner's cutting beam, the Drake Bastion's
// tractor -- so the two cannot drift apart in how a beam looks. `t` is the caller's clock (it drives
// the breathing); the colours are the glow, the body, the core and the bright tip where it lands.
public static class Beam
{
    public static void Draw(CanvasItem c, Vector2 a, Vector2 b, double t, Color glow, Color body, Color core, Color tip, float width = 1f)
    {
        float breathe = 1f + 0.15f * Mathf.Sin((float)t * 11f);
        c.DrawLine(a, b, glow, 9f * width * breathe);
        c.DrawLine(a, b, body, 4.5f * width * breathe);
        c.DrawLine(a, b, core, 1.6f * width);
        c.DrawCircle(b, 5f * width * breathe, tip);
    }
}
