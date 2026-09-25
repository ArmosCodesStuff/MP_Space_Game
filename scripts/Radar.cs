using Godot;
using System.Linq;

// The radar: a round, north-up scope in the top-right corner, centred on the
// player (the escape pod, while the ship is in stasis), reaching Range world units.
// Its size is a setting in the Escape menu (small, medium, large).
//   white arrow   you                     blue arrows   other players
//   red           hostiles (ringed yellow when selected): a dot, a diamond for Tag.Heavy
//   a bracket     a squad in formation round its members (SquadSight: the same on every peer)
//   a chevron     a squad wholly off the scope, on the rim toward it, labelled "1+3"
//   yellow        gatherers and the hauler
//   the landmarks, and the turrets a freighter left out, are Hub.ScopeMarks: one row
//   each, picked below and drawn below, so nothing can be pickable and invisible
//   a thin box    what the free camera is looking at, when it is unlocked
// Purely local: it draws what this machine already knows.
public partial class Radar : Control
{
    public Hub Hub;
    public static readonly float[] Sizes = { 160f, 230f, 310f };
    public const float Range = 3600f;        // past the outposts (3000 u), so they sit inside the rim

    public override void _Ready()
    {
        Name = "Radar";
        AnchorLeft = AnchorRight = 1f;
        MouseFilter = MouseFilterEnum.Stop;              // it takes clicks: pick things on it
    }

    // A click on the radar picks the nearest thing within 9 px of the click on its face: an
    // enemy becomes the target; a landmark or another pilot becomes a waypoint to warp to.
    private const float PickPx = 9f;
    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        AcceptEvent();
        var me = Hub?.MyShip; if (me == null) return;
        float r = Size.X * 0.5f; var c = new Vector2(r, r); float k = (r - 6f) / Range;
        if (mb.Position.DistanceTo(c) > r) return;
        var world = me.ViewPosition + (mb.Position - c) / k;
        float tol = PickPx / k;
        var best = Combat.Nearest(Combat.Hostiles, world, h => h.Position, tol, Combat.Pickable);
        if (best != null) { Hub.SelectTarget(best); return; }
        var marks = new System.Collections.Generic.List<(string name, Vector2 at, float radius)>();
        foreach (var m in Hub.ScopeMarks()) marks.Add((m.Name, m.At, m.Pick));   // the table _Draw draws
        foreach (var s in Hub.Ships) if (s != me) marks.Add((s.Pilot, s.Position, s.HitRadius));
        (string name, Vector2 at, float radius)? pick = null; float pd = tol;
        foreach (var m in marks) { float d = world.DistanceTo(m.at); if (d <= pd) { pd = d; pick = m; } }
        if (pick is { } p) Hub.SelectWaypoint(p.name, p.at, p.radius);
    }

    public override void _Process(double delta)
    {
        // the K window opens over this corner: the radar steps aside while it is open
        Visible = Hub != null && !Hub.StatsOpen;
        float s = Sizes[Mathf.Clamp(Settings.RadarSize, 0, 2)];
        OffsetLeft = -10f - s; OffsetRight = -10f; OffsetTop = 10f; OffsetBottom = 10f + s;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var me = Hub?.MyShip;
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
        if (IsInstanceValid(Hub.Boss)) { var p = P(Hub.Boss.Position); if (Inside(p)) DrawCircle(p, 6f, new Color(1f, 0.3f, 0.25f)); }   // the boss
        foreach (var cr in Hub.Crates)                                     // this pilot's crates: only its own exist here
            if (IsInstanceValid(cr)) { var p = P(cr.Position); if (Inside(p)) DrawRect(new Rect2(p - new Vector2(2, 2), new Vector2(4, 4)), Ui.TierColor(Equipment.ById(cr.Item)?.Tier ?? 1)); }
        // EVERY MARK THE SCOPE CAN POINT AT, from the one table _GuiInput picks from: the home
        // landmarks, the mission portal while it is open, and the turrets a freighter left out.
        // A row here is a row there: nothing is pickable without a glyph.
        foreach (var m in Hub.ScopeMarks())
        {
            var p = P(m.At);
            if (!Inside(p)) continue;
            if (m.Shape == Hub.MarkShape.Dot) DrawCircle(p, m.Size.X, m.Colour);
            else if (m.Shape == Hub.MarkShape.Box) DrawRect(new Rect2(p - m.Size, m.Size * 2f), m.Colour);
            else if (m.Shape == Hub.MarkShape.Ring) DrawArc(p, m.Size.X, 0, Mathf.Tau, 16, m.Colour, 1.5f);
            else if (m.Shape == Hub.MarkShape.Diamond)
                DrawColoredPolygon(new[] { p + new Vector2(0, -m.Size.Y), p + new Vector2(m.Size.X, 0),
                                           p + new Vector2(0, m.Size.Y), p + new Vector2(-m.Size.X, 0) }, m.Colour);
        }
        // the fleet that is out there: a lost ship, or the hauler through the portal, has no dot
        if (Hub.Yard != null) foreach (var g in Hub.Yard.Gatherers) { var p = P(g.Position); if (g.InReach && Inside(p)) DrawCircle(p, 1.8f, Plume.Utility); }
        if (Hub.Yard?.Hauler is { InReach: true } hl) { var p = P(hl.Position); if (Inside(p)) DrawRect(new Rect2(p - new Vector2(3, 1.5f), new Vector2(6, 3)), Plume.Utility); }
        // SQUADS FIRST: their members are drawn here, so the hostile loop below leaves them out
        var squads = SquadSight.Groups(Hub.Raiders);
        var drawn = new System.Collections.Generic.HashSet<IHittable>(squads.SelectMany(q => q));
        foreach (var sq in squads)
        {
            var pts = sq.Select(m => P(m.Position)).ToList();
            if (!pts.Any(Inside))
            {   // wholly off the scope: ONE chevron on the rim toward it, and what it brings
                var mid = pts.Aggregate(Vector2.Zero, (a, b) => a + b) / pts.Count;
                var d = (mid - c).Normalized(); var sd = new Vector2(-d.Y, d.X);
                DrawColoredPolygon(new[] { c + d * (r - 2f), c + d * (r - 10f) + sd * 5f, c + d * (r - 10f) - sd * 5f }, Hostile);
                Txt.Centre(this, ThemeDB.FallbackFont, c + d * (r - 22f) + new Vector2(0, 4f), SquadSight.Label(sq), 10, Hostile);
                continue;
            }
            for (int i = 0; i < sq.Count; i++) Blip(Inside(pts[i]) ? pts[i] : c + (pts[i] - c).Normalized() * (r - 5f), sq[i]);
            if (!SquadSight.InFormation(sq)) continue;
            // in formation: a bracket round the members on the scope
            var on = pts.Where(Inside).ToList();
            Vector2 lo = on.Aggregate((a, b) => new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y))) - new Vector2(6f, 6f);
            Vector2 hi = on.Aggregate((a, b) => new Vector2(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y))) + new Vector2(6f, 6f);
            var bc = new Color(1f, 0.45f, 0.4f, 0.8f);
            foreach (var (corner, sx, sy) in new[] { (lo, 1f, 1f), (new Vector2(hi.X, lo.Y), -1f, 1f), (hi, -1f, -1f), (new Vector2(lo.X, hi.Y), 1f, -1f) })
            {
                DrawLine(corner, corner + new Vector2(4f * sx, 0), bc, 1f);
                DrawLine(corner, corner + new Vector2(0, 4f * sy), bc, 1f);
            }
        }
        foreach (var h in Combat.Hostiles)
        {
            if (h == null || !h.Alive || drawn.Contains(h)) continue;
            var p = P(h.Position);
            if (!Inside(p)) p = c + (p - c).Normalized() * (r - 5f);          // off-scope: pinned to the rim
            Blip(p, h);
        }
        foreach (var s in Hub.Ships)
        {
            if (s == me) continue;
            var p = P(s.Position); if (!Inside(p)) p = c + (p - c).Normalized() * (r - 5f);
            Arrow(p, s.Rotation, new Color(0.55f, 0.85f, 1f));
        }
        if (Hub.Waypoint is { } wp) { var p = P(wp); if (!Inside(p)) p = c + (p - c).Normalized() * (r - 5f); DrawArc(p, 6f, 0, Mathf.Tau, 16, new Color(1f, 0.85f, 0.3f), 1.4f); }
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

    // ONE HOSTILE on the face: a dot, or a diamond for anything tagged Heavy; ringed when selected
    private static readonly Color Hostile = new(1f, 0.35f, 0.3f);
    private void Blip(Vector2 p, IHittable h)
    {
        if (h is ITagged t && (t.Tags & Tag.Heavy) != 0)
            DrawColoredPolygon(new[] { p + new Vector2(0, -3.8f), p + new Vector2(3.8f, 0), p + new Vector2(0, 3.8f), p + new Vector2(-3.8f, 0) }, Hostile);
        else DrawCircle(p, 2.6f, Hostile);
        if (Hub.I != null && Hub.I.Targets.Any(x => ReferenceEquals(x, h))) DrawArc(p, 5f, 0, Mathf.Tau, 16, new Color(1f, 0.85f, 0.3f), 1.2f);
    }

    private void Arrow(Vector2 p, float rot, Color col)
    {
        var f = Vector2.Up.Rotated(rot); var sd = new Vector2(-f.Y, f.X);
        DrawColoredPolygon(new[] { p + f * 5f, p - f * 3.5f + sd * 3.5f, p - f * 3.5f - sd * 3.5f }, col);
    }
}
