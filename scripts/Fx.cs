using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// WHAT AN EFFECT OR A WARNING LOOKS LIKE — one row each, raised by name, seen by everyone.
//
// There was one effect node in the game (Explosion) and everything else improvised. The
// freighter's shockwave, the warrior's EMP and the echo's detonation were drawn as SPOKES OF
// LASER FLASHES, because Combat.Flash happened to be the only drawing that reached a guest; the
// echo's burst was added straight to the host's tree, so nobody else ever saw it. Every new
// ability was going to invent its own again.
//
// An effect is a ROW: a shape, a colour, how long it lives, how wide it draws. `Fx.Raise` names
// the row and the world puts it up -- on the host AND on every guest, through one RPC
// (Hub.NetFx). One node draws them all, with the switch on the row's shape in one place.
//
// A WARNING IS A ROW HERE TOO, with `Warn` set: a red lane or a red zone, drawn at full size for
// a wind-up the RAISE gives, filling as the moment comes, flashing as the attack lands and
// staying lit while the attack itself lasts. Telegraph was a second node for the same sentence
// -- a shape, at a place, for a time, with a sound -- with a `bool Line` where this has a shape,
// fields set one by one where this has a raise, and an RPC of its own beside the one that
// already carried every effect. Pure visuals either way: the host resolves the hit when the time
// is up. A warning's CLOCK and its two SOUNDS are the MOVE's (BossMove.Windup, Cue, Strike), not
// the shape's, so they ride the raise: one boss move plays its cue with no warning at all, and
// a sound written in both tables is a sound that can disagree with itself.
//
// A NEW EFFECT OR WARNING IS A ROW HERE and a call to Fx.Raise / Fx.Warn. Nothing else in the
// game learns about it.
// ─────────────────────────────────────────────────────────────────────────────
public enum FxShape
{
    Burst,     // a disc that swells and fades, with a ring running ahead of it: something died
    Ring,      // one expanding ring: a wave going out, and how far it reaches
    Spokes,    // a ring with spokes to its edge: a pulse that struck everything inside it
    Bar,       // a thick straight bar from A to B, fading: a railgun's line
    Lane,      // A WARNING: a wide straight lane from A to B -- a beam, a ram, a fan, a thrown rock
    Zone,      // A WARNING: a disc at A -- a shockwave, a warp in, a missile's blast
}

public class FxDef
{
    public string Id;
    public FxShape Shape;
    public Color Tint = new(1f, 0.7f, 0.3f);
    public double Life = 0.6;
    public float Width = 2f;        // the stroke of a ring, a spoke or a bar
    public int Spokes = 8;
    public bool Fill = true;        // a soft disc inside the ring
    public string Sound;            // an effect's own noise: Sfx.Special id, or null for silence
    // A WARNING, NOT AN EFFECT: something is about to be hit here, and the raise says for how
    // long. The row's Life is not used -- a warning lives its wind-up, its hold and one flash --
    // and its sounds are the raise's, because they are the move's.
    public bool Warn;
}

// ONE RAISE, whatever it is: which row, where, how big -- and, a warning only, how long the
// wind-up is, how long the attack itself lasts after it, how much of the wind-up is already
// gone, what it rides, and the move's two sounds. One struct because it is also what goes on the
// wire (Hub.NetFx): a field added here is added in ONE place, not in a hook, an RPC and two
// call sites.
public struct FxRaise
{
    public int Id;
    public Vector2 At, To;          // where it happens; the far end of a bar or a lane
    public float Size;              // a disc's radius, a lane's WIDTH
    public double Time, Hold, Since;
    public int Anchor;              // Fx.World, or the NetId of the hull it rides
    public string Cue, Strike;      // as the warning goes up, and as the attack lands
}

public static class Fx
{
    // The index IS the id on the wire (Hub.NetFx), so APPEND ONLY.
    public const int Burst = 0, Lost = 1, Rebuilt = 2, Wave = 3, Emp = 4, Echo = 5, Rail = 6,
                     WarnLane = 7, WarnZone = 8;
    // WHAT A WARNING RIDES: the world itself, or the NetId of the hull it is drawn on. A beam's
    // and a dash's lane are drawn in the BOSS'S OWN FRAME and parented to it, so the line it drew
    // is the line it fires down however the hull turns; everything else is pinned to the ground
    // it will hit. The world resolves the id (Hub.AddFx).
    public const int World = 0;
    // How long a warning stays lit once its attack is over.
    public const double Flash = 0.35;
    private static readonly Color Warning = new(1f, 0.15f, 0.12f);

    public static readonly FxDef[] All =
    {
        new() { Id = "burst",   Shape = FxShape.Burst, Tint = new(1f, 0.70f, 0.30f), Life = 0.6 },
        new() { Id = "lost",    Shape = FxShape.Burst, Tint = new(1f, 0.70f, 0.30f), Life = 0.6 },
        new() { Id = "rebuilt", Shape = FxShape.Burst, Tint = new(0.5f, 0.80f, 1.0f), Life = 0.6 },
        // a freighter's shockwave: a wide, slow ring that says exactly how far it threw things
        new() { Id = "wave",    Shape = FxShape.Ring,  Tint = new(0.60f, 0.80f, 1f), Life = 0.9, Width = 3f },
        // a warrior's EMP: a hard pulse with spokes, close in
        new() { Id = "emp",     Shape = FxShape.Spokes, Tint = new(0.70f, 0.90f, 1f), Life = 0.5, Width = 2f, Spokes = 10 },
        // an echo's detonation: everything it remembered, at once
        new() { Id = "echo",    Shape = FxShape.Spokes, Tint = new(1f, 0.80f, 0.45f), Life = 0.7, Width = 2.5f, Spokes = 6 },
        // a sniper's railgun: the line it threw down
        new() { Id = "rail",    Shape = FxShape.Bar,   Tint = new(0.45f, 0.70f, 1f), Life = 0.35, Width = 7f, Fill = false },
        // ── THE TWO WARNINGS: every red shape a boss or a raider raises is one of these ──
        // A lane: a beam, a ram, one line of a scrap fan, a thrown rock's path. A zone: a
        // shockwave, a warp in, a heavy missile's blast, the title screen's area shot. How long,
        // how wide and what it sounds like are the MOVE's, and come with the raise.
        new() { Id = "warn_lane", Shape = FxShape.Lane, Tint = Warning, Warn = true },
        new() { Id = "warn_zone", Shape = FxShape.Zone, Tint = Warning, Warn = true },
    };

    public static FxDef Of(int id) => All[id >= 0 && id < All.Length ? id : Burst];

    // Set by the live world: it puts the effect up here AND tells its guests (Hub), or just puts
    // it up (the title screen). Dropped with the world by Combat.Clear -- it is a lambda holding
    // that world, like every other hook here.
    public static System.Action<FxRaise> On;

    // `at` is where it happens; `radius` how far it reaches; `to` the far end of a bar.
    //
    // ONLY THE SIMULATOR RAISES. The hook puts it up here and tells every guest through one RPC
    // (Hub.NetFx), so a guest that also raised its own -- the burst on a turret it saw removed,
    // the blast at the end of a missile it was drawing, a miner's loss -- drew the same effect
    // twice. The single player and the title screen are their own simulator and are unaffected.
    public static void Raise(int id, Vector2 at, float radius = 30f)
    { if (Net.Sim) On?.Invoke(new FxRaise { Id = id, At = at, To = at, Size = radius }); }
    public static void Line(int id, Vector2 a, Vector2 b)
    { if (Net.Sim) On?.Invoke(new FxRaise { Id = id, At = a, To = b }); }

    // A WARNING: the same rows, the same hook and the same one RPC, with the clock and the two
    // sounds the move has. It takes the whole raise because it has seven things to say and they
    // read better named than counted -- and because that struct is what the wire carries.
    public static void Warn(FxRaise r) { if (Net.Sim) On?.Invoke(r); }

    // EVERY NODE UP RIGHT NOW. A node puts itself in as it enters the tree and takes itself out
    // as it leaves, so a world that goes away empties this by itself: there is no list to clear
    // and nothing to leak.
    private static readonly System.Collections.Generic.List<FxNode> LiveNodes = new();
    internal static void Entered(FxNode n) { if (!LiveNodes.Contains(n)) LiveNodes.Add(n); }
    internal static void Left(FxNode n) => LiveNodes.Remove(n);
    // The warnings still standing, for the peer that has to be told about them (Boss.CatchUp).
    public static System.Collections.Generic.IEnumerable<FxNode> Warnings
    {
        get { foreach (var n in LiveNodes) if (n.Warn && !n.IsQueuedForDeletion()) yield return n; }
    }
    // EVERY WARNING DOWN: whatever raised them threatens nothing any more. A boss that is down
    // calls this on every peer -- a beam half wound up used to go on charging, and "fire", over
    // a DEFEATED boss.
    public static void ClearWarnings()
    { foreach (var n in new System.Collections.Generic.List<FxNode>(LiveNodes)) if (n.Warn) n.QueueFree(); }
}

// One node for every row -- an effect or a warning: the shape is the row's, so a new one of
// either never brings a new node. A warning runs on the RAISE's clock (Time, Hold, Since) and
// plays the RAISE's sounds; everything else lives the row's Life and plays the row's.
public partial class FxNode : Node2D
{
    public int Id;
    public Vector2 To;
    public float Radius = 30f;              // a disc's radius; a LANE's width
    public double Time, Hold, Since;        // a warning's wind-up, its hold, and what is gone of it
    public int Anchor;                      // what it rides: Fx.World, or a hull's NetId
    public string Cue, Strike;              // the move's sounds, as it goes up and as it lands
    private double _t;
    private bool _struck;
    public double Elapsed => _t;
    public bool Warn => Fx.Of(Id).Warn;
    private FxDef D => Fx.Of(Id);
    private double Life => D.Warn ? Time + Hold + Fx.Flash : D.Life;

    public override void _Ready()
    {
        ZIndex = 7; ZAsRelative = false;
        Fx.Entered(this);
        // A warning's opening sound is the move's, an effect's is its row's; and a copy sent on to
        // a peer that arrived mid-warning has already missed it. GlobalPosition, not Position: a
        // warning that rides a hull is a child of it, and its Position is an offset from the
        // boss's nose rather than a place in the world.
        var opening = Cue ?? D.Sound;
        if (opening != null && Since <= 0) Sfx.Special(opening, GlobalPosition);
    }
    public override void _ExitTree() => Fx.Left(this);

    public override void _Process(double delta)
    {
        _t += delta;
        if (D.Warn && !_struck && _t >= Time)
        {   // it lands: the move's own sound, once -- and never for a copy that arrived after it
            _struck = true;
            if (Time > 0 && Strike != null) Sfx.Special(Strike, GlobalPosition);
        }
        if (_t >= Life) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var d = D;
        // A WARNING FILLS over the whole wind-up, including whatever of it had gone before this
        // copy went up; an effect runs out over its row's Life.
        float k = (float)Mathf.Clamp(d.Warn ? (Since + _t) / System.Math.Max(1e-6, Since + Time) : _t / d.Life, 0, 1);
        float fade = 1 - k;
        var c = d.Tint;
        // A warning pulses faster as the moment comes, flashes white-hot as the attack lands, and
        // dies away over Fx.Flash once the attack itself is over.
        bool firing = d.Warn && _t >= Time;
        Color edge = default, fill = default;
        if (d.Warn)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)_t * (8f + 10f * k));
            edge = new Color(c.R, c.G, c.B, firing ? 1f : 0.55f + 0.35f * pulse);
            fill = firing ? new Color(1f, 0.85f, 0.8f, 0.7f * (float)(1 - System.Math.Max(0, _t - Time - Hold) / Fx.Flash))
                          : new Color(c.R, c.G, c.B, 0.10f + 0.12f * k);
        }
        switch (d.Shape)
        {
            case FxShape.Burst:
                DrawCircle(Vector2.Zero, Radius * (0.4f + 0.8f * k), new Color(c.R, c.G, c.B, 0.55f * fade));
                DrawArc(Vector2.Zero, Radius * (0.6f + 1.2f * k), 0, Mathf.Tau, 32, new Color(1f, 0.9f, 0.7f, 0.8f * fade), 2f);
                break;
            case FxShape.Ring:
                if (d.Fill) DrawCircle(Vector2.Zero, Radius * k, new Color(c.R, c.G, c.B, 0.10f * fade));
                DrawArc(Vector2.Zero, Radius * k, 0, Mathf.Tau, 96, new Color(c.R, c.G, c.B, fade), d.Width);
                break;
            case FxShape.Spokes:
                if (d.Fill) DrawCircle(Vector2.Zero, Radius * k, new Color(c.R, c.G, c.B, 0.12f * fade));
                DrawArc(Vector2.Zero, Radius * k, 0, Mathf.Tau, 64, new Color(c.R, c.G, c.B, fade), d.Width);
                for (int i = 0; i < d.Spokes; i++)
                {
                    var dir = Vector2.Up.Rotated(Mathf.Tau * i / d.Spokes);
                    DrawLine(dir * (Radius * k * 0.25f), dir * (Radius * k), new Color(c.R, c.G, c.B, 0.7f * fade), d.Width * 0.6f);
                }
                break;
            case FxShape.Bar:
            {
                // To is in the PARENT's frame, like Position: a warning that rides a hull is a
                // child of it, and ToLocal would read the far end as a point in the world.
                var b = To - Position;
                DrawLine(Vector2.Zero, b, new Color(c.R, c.G, c.B, 0.35f * fade), d.Width * 2.2f);
                DrawLine(Vector2.Zero, b, new Color(c.R, c.G, c.B, fade), d.Width);
                DrawLine(Vector2.Zero, b, new Color(1f, 1f, 1f, 0.9f * fade), d.Width * 0.3f);
                break;
            }
            case FxShape.Lane:
            {
                var b = To - Position;
                var dir = b.Normalized(); var side = new Vector2(-dir.Y, dir.X) * Radius / 2f;
                DrawColoredPolygon(new[] { side, b + side, b - side, -side }, fill);
                if (!firing)   // the wind-up creeps down the lane's length
                    DrawColoredPolygon(new[] { side, b * k + side, b * k - side, -side }, new Color(c.R, c.G, c.B, 0.22f));
                DrawPolyline(new[] { side, b + side, b - side, -side, side }, edge, 2.5f);
                break;
            }
            case FxShape.Zone:
                DrawCircle(Vector2.Zero, Radius, fill);
                if (!firing) DrawCircle(Vector2.Zero, Radius * k, new Color(c.R, c.G, c.B, 0.20f));   // it grows outward
                DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 72, edge, 3f);
                break;
        }
    }
}
