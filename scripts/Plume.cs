using Godot;

// A small engine plume: a soft outer glow and a hot core, flickering, pointing out
// of the stern. Its size follows the ship's length; its reach follows the throttle.
// Player craft pass their accent colour; utility ships a light yellow no player
// setting changes. (Missiles are not engines here: they keep their smoke trail.)
public static class Plume
{
    public static readonly Color Utility = new(1f, 0.93f, 0.55f);
    // the flame's width as a share of the size it is drawn at: a bell `b` wide is drawn at b / Width
    public const float Width = 0.07f;

    // at: the nozzle, in the caller's local frame; back: unit vector out of the stern.
    // active: moving or thrusting -- only then does the flame flicker; at rest it
    // holds a steady idle glow.
    public static void Draw(CanvasItem ci, Vector2 at, Vector2 back, float shipLength, Color col, float throttle, bool active)
    {
        float flick = active ? 0.85f + 0.15f * Mathf.Sin(Time.GetTicksMsec() / 37f + at.X) : 1f;
        float len = shipLength * 0.16f * (0.35f + 0.65f * Mathf.Clamp(throttle, 0f, 1f)) * flick;
        float w = Mathf.Max(1.2f, shipLength * Width * 0.5f);
        var side = new Vector2(-back.Y, back.X);
        Vector2[] Tear(float l, float ww) => new[] { at + side * ww, at + back * l, at - side * ww };
        ci.DrawColoredPolygon(Tear(len * 1.25f, w * 1.5f), new Color(col.R, col.G, col.B, 0.22f));
        ci.DrawColoredPolygon(Tear(len, w), new Color(col.R, col.G, col.B, 0.55f));
        ci.DrawColoredPolygon(Tear(len * 0.55f, w * 0.5f), new Color(1f, 1f, 1f, 0.7f));
    }
}
