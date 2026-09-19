using Godot;

// The radar: a round, north-up scope in the top-right corner, centred on the
// player (the escape pod, while the ship is in stasis), reaching Range world units.
// Its size is a setting in the Escape menu (small, medium, large).
//   white arrow   you                     blue arrows   other players
//   red           hostiles (ringed yellow when selected)
//   yellow        gatherers and the hauler
//   grey          the base; cyan ring the portal; brown the wreck and asteroids
//   a thin box    what the free camera is looking at, when it is unlocked
// Purely local: it draws what this machine already knows.
public partial class Radar : Control
{
    public Hub Hub;
    public static readonly float[] Sizes = { 160f, 230f, 310f };
    public const float Range = 3000f;

    public override void _Ready()
    {
        Name = "Radar";
        AnchorLeft = AnchorRight = 1f;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        float s = Sizes[Mathf.Clamp(Settings.RadarSize, 0, 2)];
        OffsetLeft = -10f - s; OffsetRight = -10f; OffsetTop = 10f; OffsetBottom = 10f + s;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var me = Hub?.MyShipPublic;
        if (me == null) return;
        float r = Size.X * 0.5f;
        var c = new Vector2(r, r);
        float k = (r - 6f) / Range;
        var centre = me.ViewPosition;
        Vector2 P(Vector2 world) => c + (world - centre) * k;
        bool Inside(Vector2 p) => p.DistanceTo(c) <= r - 4f;

        DrawCircle(c, r, new Color(0.04f, 0.06f, 0.10f, 0.96f));
        DrawArc(c, r - 1f, 0, Mathf.Tau, 64, new Color(0.30f, 0.40f, 0.55f, 0.9f), 1.5f);
        DrawArc(c, (r - 6f) / 3f, 0, Mathf.Tau, 48, new Color(0.3f, 0.45f, 0.6f, 0.25f), 1f);
        DrawArc(c, (r - 6f) * 2f / 3f, 0, Mathf.Tau, 48, new Color(0.3f, 0.45f, 0.6f, 0.25f), 1f);
        DrawLine(c + new Vector2(0, -r + 3f), c + new Vector2(0, -r + 9f), new Color(0.6f, 0.8f, 1f, 0.8f), 1.5f);   // north

        foreach (var rock in Hub.Rocks) { var p = P(rock.Position); if (Inside(p)) DrawCircle(p, 1.2f, new Color(0.55f, 0.45f, 0.35f, 0.8f)); }
        { var p = P(Hub.WreckPos); if (Inside(p)) DrawCircle(p, 4f, new Color(0.5f, 0.35f, 0.25f, 0.9f)); }
        if (Hub.Boss != null && IsInstanceValid(Hub.Boss)) { var p = P(Hub.Boss.Position); if (Inside(p)) DrawCircle(p, 6f, new Color(1f, 0.3f, 0.25f)); }   // the boss
        if (!Hub.InArena) { var p = P(Hub.TioPos); if (Inside(p)) DrawRect(new Rect2(p - new Vector2(3, 4), new Vector2(6, 8)), new Color(0.6f, 0.64f, 0.7f)); }   // the TIO
        if (!Hub.InArena) { var p = P(Hub.BasePos); if (Inside(p)) DrawRect(new Rect2(p - new Vector2(4, 4), new Vector2(8, 8)), new Color(0.75f, 0.78f, 0.8f)); }
        { var p = P(Hub.PortalPos); if (Inside(p)) DrawArc(p, 5f, 0, Mathf.Tau, 16, new Color(0.4f, 0.8f, 1f), 1.5f); }
        if (Hub.Yard != null) foreach (var g in Hub.Yard.Gatherers) { var p = P(g.Position); if (Inside(p)) DrawCircle(p, 1.8f, Plume.Utility); }
        if (Hub.Yard?.Hauler != null && Hub.Yard.Hauler.Visible) { var p = P(Hub.Yard.Hauler.Position); if (Inside(p)) DrawRect(new Rect2(p - new Vector2(3, 1.5f), new Vector2(6, 3)), Plume.Utility); }
        foreach (var h in Combat.Hostiles)
        {
            if (h == null || !h.Alive) continue;
            var p = P(h.Position);
            if (!Inside(p)) p = c + (p - c).Normalized() * (r - 5f);          // off-scope: pinned to the rim
            DrawCircle(p, 2.6f, new Color(1f, 0.35f, 0.3f));
            if (h == Hub.Selected) DrawArc(p, 5f, 0, Mathf.Tau, 16, new Color(1f, 0.85f, 0.3f), 1.2f);
        }
        foreach (var s in Hub.Ships)
        {
            if (s == me) continue;
            var p = P(s.Position); if (!Inside(p)) p = c + (p - c).Normalized() * (r - 5f);
            Arrow(p, s.Rotation, new Color(0.55f, 0.85f, 1f));
        }
        if (!me.Alive) DrawCircle(P(me.Position), 2.5f, new Color(0.5f, 0.6f, 0.8f));   // the ship in stasis
        Arrow(c, me.Alive ? me.Rotation : 0f, Colors.White);
        if (Hub.FreeCamera)
        {   // what the free camera sees -- clipped to the disc: drawn whole, the box
            // ran outside the radar whenever the view reached its edge
            var half = Hub.GetViewportRect().Size * 0.5f / Hub.ZoomLevel * k;
            var cp = P(Hub.CameraPosition);
            Vector2[] q = { cp - half, cp + new Vector2(half.X, -half.Y), cp + half, cp + new Vector2(-half.X, half.Y) };
            for (int e = 0; e < 4; e++)
                for (int i = 0; i < 24; i++)
                {
                    Vector2 p0 = q[e].Lerp(q[(e + 1) % 4], i / 24f), p1 = q[e].Lerp(q[(e + 1) % 4], (i + 1) / 24f);
                    if (((p0 + p1) * 0.5f).DistanceTo(c) <= r - 1f) DrawLine(p0, p1, new Color(1f, 1f, 1f, 0.5f), 1f);
                }
        }
    }

    private void Arrow(Vector2 p, float rot, Color col)
    {
        var f = Vector2.Up.Rotated(rot); var sd = new Vector2(-f.Y, f.X);
        DrawColoredPolygon(new[] { p + f * 5f, p - f * 3.5f + sd * 3.5f, p - f * 3.5f - sd * 3.5f }, col);
    }
}
