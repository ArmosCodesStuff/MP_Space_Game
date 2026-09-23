using Godot;
using System.Collections.Generic;

// POPUPS: short-lived figures that rise off a thing in the world and fade -- the damage it dealt,
// the damage it took, the EXP a kill just paid. NAMED FOR WHAT IT IS, not for the first thing that
// used it: it was DamageNumbers, which was true until something else wanted a floating readout,
// and a name that describes one use is a name the next instance either believes or copies around.
//
// A KIND IS A ROW (Pop.All): its colour, its size, how far it rises, how long it lasts, whether a
// second one merges into the first, and whether it stands where the blow landed or over the thing
// itself. A fourth readout -- credits, salvage, a status -- is a row, not another node.
//
// Every peer shows what IT sees, from hulls it already has: each damageable thing watches its own
// hull (HullWatch) -- its own hit on the host, the host's figures on a guest -- so no message is
// sent for them. A figure stands at the IMPACT POINT where one is known (NoteImpact: where a shell
// or a torpedo struck, the side of a ship a hit came from), else round the thing's centre.
// One per world (the Hub's), freed with it.
public class PopDef
{
    public string Id;
    public Color Tint;
    public int Size = 11;               // Txt.Size scales it with the UI
    public float Rise = 26f;            // how far it floats up over its life, in world units
    public double Life = 1.2, Fade = 0.6;
    public double Merge = 0.3;          // a second within this of the first adds into it; 0: never merge
    public float Jitter = 10f;          // scatter round the point, so a stream never stacks exactly
    public bool AtImpact = true;        // stand where the blow landed, when the thing knows
    public float Above;                 // ...or this far above the thing itself, for a readout that is not a hit
    public string Prefix = "";
}

public static class Pop
{
    // Append only: an id is nothing but an index here.
    public const int Dealt = 0, Taken = 1, Exp = 2;
    public static readonly PopDef[] All =
    {
        new() { Id = "dealt", Tint = new Color(1f, 0.9f, 0.55f) },
        new() { Id = "taken", Tint = new Color(1f, 0.42f, 0.32f) },
        // THE PILOT'S OWN EXP, over its own hull: bolder, whiter, larger and slower than a hit, and
        // it never merges -- two kills are two figures, because a total that grew by itself would
        // read as one bigger kill.
        new() { Id = "exp",   Tint = new Color(1f, 1f, 1f), Size = 15, Rise = 54f, Life = 2.0, Fade = 0.9,
                Merge = 0, Jitter = 0f, AtImpact = false, Above = 46f, Prefix = "+" },
    };
    public static PopDef Of(int kind) => All[kind >= 0 && kind < All.Length ? kind : Dealt];
}

public partial class Popups : Node2D
{
    private const float ImpactJitter = 4f;          // round a known impact point
    private const double ImpactFresh = 0.3;         // an impact point is the next figure's if it is this recent

    public class Figure { public ulong Who; public Vector2 At; public double Amount, Age; public int Kind; }
    private readonly List<Figure> _figures = new();
    public IReadOnlyList<Figure> Figures => _figures;
    private readonly Dictionary<ulong, (Vector2 at, double t)> _impacts = new();
    private double _clock;
    private static Popups _live;
    public static Popups Live => GodotObject.IsInstanceValid(_live) && _live.IsInsideTree() ? _live : null;

    public override void _EnterTree() { _live = this; ZIndex = 9; ZAsRelative = false; }
    public override void _ExitTree() { if (_live == this) _live = null; }

    // Damage dealt and damage taken, the two a hull watch raises.
    public static void Show(Node2D who, double amount, bool taken) => Raise(taken ? Pop.Taken : Pop.Dealt, who, amount);

    // WHAT A KILL JUST PAID, over the pilot's own ship. Its own door, as asked: the EXP is not a
    // hit and never wants a hit's colour, size, merge or impact point.
    public static void Exp(Node2D who, double amount) => Raise(Pop.Exp, who, amount);

    public static void Raise(int kind, Node2D who, double amount)
    {
        var l = Live;
        if (l == null || amount <= 1e-6 || !GodotObject.IsInstanceValid(who)) return;
        var d = Pop.Of(kind);
        ulong id = who.GetInstanceId();
        if (d.Merge > 0)
            foreach (var f in l._figures)
                if (f.Who == id && f.Kind == kind && f.Age < d.Merge) { f.Amount += amount; return; }
        bool struck = false; Vector2 where = who.GlobalPosition - new Vector2(0, d.Above);
        if (d.AtImpact && l._impacts.Remove(id, out var hit) && l._clock - hit.t <= ImpactFresh)
        { struck = true; where = hit.at; }
        float scatter = struck ? ImpactJitter : d.Jitter;
        l._figures.Add(new Figure { Who = id, Amount = amount, Kind = kind,
                                    At = where + new Vector2(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * scatter });
    }

    // where `who` was just struck: its next figure stands there
    public static void NoteImpact(Node2D who, Vector2 at)
    {
        var l = Live;
        if (l != null && GodotObject.IsInstanceValid(who)) l._impacts[who.GetInstanceId()] = (at, l._clock);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        for (int i = _figures.Count - 1; i >= 0; i--)
            if ((_figures[i].Age += delta) >= Pop.Of(_figures[i].Kind).Life) _figures.RemoveAt(i);
        if (_impacts.Count > 0)
            foreach (var id in new List<ulong>(_impacts.Keys)) if (_clock - _impacts[id].t > ImpactFresh) _impacts.Remove(id);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        foreach (var f in _figures)
        {
            var d = Pop.Of(f.Kind);
            float a = f.Age < d.Life - d.Fade ? 1f : (float)((d.Life - f.Age) / d.Fade);
            var col = new Color(d.Tint.R, d.Tint.G, d.Tint.B, a);
            string said = d.Prefix + (f.Amount < 10 ? f.Amount.ToString("0.#") : f.Amount.ToString("0"));
            Txt.Centre(this, font, f.At + new Vector2(0, -d.Rise * (float)(f.Age / d.Life)), said, Txt.Size(d.Size), col);
        }
    }
}

// One thing's hull, watched from frame to frame: what it lost since the last look is shown
// (Popups). A rise -- a repair, a rebuild, a refit -- only moves the mark.
public struct HullWatch
{
    private double _last; private bool _seen;
    public void Tick(Node2D who, double hull, bool taken)
    {
        if (_seen && hull < _last - 1e-6) Popups.Show(who, _last - hull, taken);
        _last = hull; _seen = true;
    }
}
