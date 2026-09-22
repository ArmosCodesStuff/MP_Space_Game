using Godot;
using System.Collections.Generic;

// DAMAGE NUMBERS: small figures where damage lands, floating up and fading -- pale gold for damage
// dealt (to raiders, bosses, the dummies), red for damage taken (by the players' ships and the base's
// utility ships). Every peer shows what IT sees, from hulls it already has: each damageable thing
// watches its own hull (HullWatch) -- its own hit on the host, the host's figures on a guest -- so no
// message is sent for them. A figure stands at the IMPACT POINT where one is known (NoteImpact: where
// a shell or a torpedo struck, the side of a ship a hit came from), else round the thing's centre.
// Hits on one thing in quick succession add into one figure rather than stacking a column of them.
// One per world (the Hub's), freed with it.
public partial class DamageNumbers : Node2D
{
    public const double Life = 1.2, Fade = 0.6;     // seconds on screen, the last `Fade` of them fading
    public const float Rise = 26f;                  // how far a figure floats up over its life (world units)
    public const double Merge = 0.3;                // a hit on the same thing within this of its figure adds to it
    private const float Jitter = 10f;               // round the thing's centre, so a stream of figures never stacks exactly
    private const float ImpactJitter = 4f;          // round a known impact point
    private const double ImpactFresh = 0.3;         // an impact point is the next figure's if it is this recent

    public class Figure { public ulong Who; public Vector2 At; public double Amount, Age; public bool Taken; }
    private readonly List<Figure> _figures = new();
    public IReadOnlyList<Figure> Figures => _figures;
    private readonly Dictionary<ulong, (Vector2 at, double t)> _impacts = new();
    private double _clock;
    private static DamageNumbers _live;
    public static DamageNumbers Live => GodotObject.IsInstanceValid(_live) && _live.IsInsideTree() ? _live : null;

    public override void _EnterTree() { _live = this; ZIndex = 9; ZAsRelative = false; }
    public override void _ExitTree() { if (_live == this) _live = null; }

    public static void Show(Node2D who, double amount, bool taken)
    {
        var l = Live;
        if (l == null || amount <= 1e-6 || !GodotObject.IsInstanceValid(who)) return;
        ulong id = who.GetInstanceId();
        foreach (var f in l._figures)
            if (f.Who == id && f.Taken == taken && f.Age < Merge) { f.Amount += amount; return; }
        bool struck = l._impacts.Remove(id, out var hit) && l._clock - hit.t <= ImpactFresh;
        l._figures.Add(new Figure { Who = id, Amount = amount, Taken = taken,
                                    At = (struck ? hit.at : who.GlobalPosition) + new Vector2(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * (struck ? ImpactJitter : Jitter) });
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
            if ((_figures[i].Age += delta) >= Life) _figures.RemoveAt(i);
        if (_impacts.Count > 0)
            foreach (var id in new List<ulong>(_impacts.Keys)) if (_clock - _impacts[id].t > ImpactFresh) _impacts.Remove(id);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont; int size = Txt.Size(11);
        foreach (var f in _figures)
        {
            float a = f.Age < Life - Fade ? 1f : (float)((Life - f.Age) / Fade);
            var col = f.Taken ? new Color(1f, 0.42f, 0.32f, a) : new Color(1f, 0.9f, 0.55f, a);
            Txt.Centre(this, font, f.At + new Vector2(0, -Rise * (float)(f.Age / Life)), f.Amount < 10 ? f.Amount.ToString("0.#") : f.Amount.ToString("0"), size, col);
        }
    }
}

// One thing's hull, watched from frame to frame: what it lost since the last look is shown
// (DamageNumbers). A rise -- a repair, a rebuild, a refit -- only moves the mark.
public struct HullWatch
{
    private double _last; private bool _seen;
    public void Tick(Node2D who, double hull, bool taken)
    {
        if (_seen && hull < _last - 1e-6) DamageNumbers.Show(who, _last - hull, taken);
        _last = hull; _seen = true;
    }
}
