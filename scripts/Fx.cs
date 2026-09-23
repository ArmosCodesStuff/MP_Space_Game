using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// WHAT AN EFFECT LOOKS LIKE — one row each, raised by name, seen by everyone.
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
// A NEW EFFECT IS A ROW HERE and a call to Fx.Raise. Nothing else in the game learns about it.
// ─────────────────────────────────────────────────────────────────────────────
public enum FxShape
{
    Burst,     // a disc that swells and fades, with a ring running ahead of it: something died
    Ring,      // one expanding ring: a wave going out, and how far it reaches
    Spokes,    // a ring with spokes to its edge: a pulse that struck everything inside it
    Bar,       // a thick straight bar from A to B, fading: a railgun's line
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
    public string Sound;            // Sfx.Special id, or null for silence
}

public static class Fx
{
    // The index IS the id on the wire (Hub.NetFx), so APPEND ONLY.
    public const int Burst = 0, Lost = 1, Rebuilt = 2, Wave = 3, Emp = 4, Echo = 5, Rail = 6;

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
    };

    public static FxDef Of(int id) => All[id >= 0 && id < All.Length ? id : Burst];

    // Set by the live world: it puts the effect up here AND tells its guests (Hub), or just puts
    // it up (the title screen). Dropped with the world by Combat.Clear -- it is a lambda holding
    // that world, like every other hook here.
    public static System.Action<int, Vector2, Vector2, float> On;

    // `at` is where it happens; `radius` how far it reaches; `to` the far end of a bar.
    //
    // ONLY THE SIMULATOR RAISES. The hook puts it up here and tells every guest through one RPC
    // (Hub.NetFx), so a guest that also raised its own -- the burst on a turret it saw removed,
    // the blast at the end of a missile it was drawing, a miner's loss -- drew the same effect
    // twice. The single player and the title screen are their own simulator and are unaffected.
    public static void Raise(int id, Vector2 at, float radius = 30f) { if (Net.Sim) On?.Invoke(id, at, at, radius); }
    public static void Line(int id, Vector2 a, Vector2 b) { if (Net.Sim) On?.Invoke(id, a, b, 0f); }
}

// One node for every row: the shape is the row's, so a new effect never brings a new node.
public partial class FxNode : Node2D
{
    public int Id;
    public Vector2 To;
    public float Radius = 30f;
    private double _t;
    private FxDef D => Fx.Of(Id);

    public override void _Ready()
    {
        ZIndex = 7; ZAsRelative = false;
        if (D.Sound != null) Sfx.Special(D.Sound, Position);
    }

    public override void _Process(double delta)
    {
        _t += delta;
        if (_t >= D.Life) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var d = D;
        float k = (float)Mathf.Clamp(_t / d.Life, 0, 1), fade = 1 - k;
        var c = d.Tint;
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
                var b = ToLocal(To);
                DrawLine(Vector2.Zero, b, new Color(c.R, c.G, c.B, 0.35f * fade), d.Width * 2.2f);
                DrawLine(Vector2.Zero, b, new Color(c.R, c.G, c.B, fade), d.Width);
                DrawLine(Vector2.Zero, b, new Color(1f, 1f, 1f, 0.9f * fade), d.Width * 0.3f);
                break;
            }
        }
    }
}
