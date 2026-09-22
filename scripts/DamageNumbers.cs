using Godot;
using System.Collections.Generic;

// DAMAGE NUMBERS: small figures where damage lands, floating up and fading -- pale gold for damage
// dealt (to raiders, bosses, the dummies), red for damage taken (by the players' ships and the base's
// utility ships). Every peer shows what IT sees, from hulls it already has: each damageable thing
// watches its own hull (HullWatch) -- its own hit on the host, the host's figure on a guest -- so no
// message is sent for them. Hits on one thing in quick succession add into one figure rather than
// stacking a column of them. One per world (the Hub's), freed with it.
public partial class DamageNumbers : Node2D
{
    public const double Life = 1.2, Fade = 0.6;     // seconds on screen, the last `Fade` of them fading
    public const float Rise = 26f;                  // how far a figure floats up over its life (world units)
    public const double Merge = 0.3;                // a hit on the same thing within this of its figure adds to it
    private const float Jitter = 10f;               // round the thing's centre, so a stream of figures never stacks exactly

    public class Figure { public ulong Who; public Vector2 At; public double Amount, Age; public bool Taken; }
    private readonly List<Figure> _figures = new();
    public IReadOnlyList<Figure> Figures => _figures;
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
        l._figures.Add(new Figure { Who = id, Amount = amount, Taken = taken,
                                    At = who.GlobalPosition + new Vector2(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * Jitter });
    }

    public override void _Process(double delta)
    {
        for (int i = _figures.Count - 1; i >= 0; i--)
            if ((_figures[i].Age += delta) >= Life) _figures.RemoveAt(i);
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
