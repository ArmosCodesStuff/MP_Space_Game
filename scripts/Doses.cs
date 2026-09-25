using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// A DOSE: damage over time that STACKS on a target (the Wraith's Venom, DL3). New with slice 6d; it replaced nothing.
//
// A DoseDef is a row: its Id is the credit its ticks carry through the damage door (Dealt.Deal: PlayerShip.DealtBy, and
// Items.Repeats lets a tick pass as it is -- the rate is the row's, no backstab or prime on it), and it names the dealer's
// sheet rows for its Cap (stacks on one target), its Rate (per stack, per second) and its Last (seconds it runs after the
// last stack was added); Every is the tick, Fx the effect raised on the target at each tick (every peer). A row that
// doses calls PlayerShip.Dose; the ship's Doses ticks them on the HOST, from the first stack on, until Last runs out
// or the target leaves the world. A new stacking poison, burn or bleed is a row here.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class DoseDef
{
    public string Id, Cap, Rate, Last;
    public double Every = 0.5;
    public int Fx = -1;
}

public static class DoseRows
{
    // the Wraith's Venom: each landed pellet a stack, up to 10, 1.25 a second each, 5 s after the last
    public static readonly DoseDef Venom = new() { Id = Dealt.Venom, Cap = "venom_cap", Rate = "venom_dps", Last = "venom_last", Fx = global::Fx.Venom };
}

// ONE SHIP'S DOSES on everything it has dosed (host). The ship owns it, so it goes with the ship.
public sealed class Doses
{
    private sealed class On { public DoseDef Def; public IHittable T; public int Stacks; public double Last, Next; }
    private readonly List<On> _on = new();

    public int StacksOn(IHittable t, DoseDef def)
    {
        foreach (var o in _on) if (o.Def == def && ReferenceEquals(o.T, t)) return o.Stacks;
        return 0;
    }

    // a stack more of `def` on `t` at `now`, up to the row's cap; the clock to its end starts again
    public void Add(DoseDef def, IHittable t, double now, ShipStats st)
    {
        int cap = (int)st[def.Cap];
        if (cap <= 0 || t == null || !t.Alive) return;
        foreach (var o in _on)
            if (o.Def == def && ReferenceEquals(o.T, t)) { o.Stacks = System.Math.Min(cap, o.Stacks + 1); o.Last = now; return; }
        _on.Add(new On { Def = def, T = t, Stacks = 1, Last = now, Next = now + def.Every });
    }

    // every tick due by `now`: Stacks x Rate x Every through the damage door, credited to `by` under the row's id
    public void Tick(double now, ShipStats st, ITurretHost by)
    {
        for (int i = _on.Count - 1; i >= 0; i--)
        {
            var o = _on[i];
            double end = o.Last + st[o.Def.Last];
            while (o.Next <= now && o.Next <= end && Combat.Hostiles.Contains(o.T) && o.T.Alive)
            {
                Dealt.Deal(o.T, o.Stacks * st[o.Def.Rate] * o.Def.Every, by, o.Def.Id);
                if (o.Def.Fx >= 0) global::Fx.Raise(o.Def.Fx, o.T.Position, o.T.HitRadius);
                o.Next += o.Def.Every;
            }
            if (now > end || !Combat.Hostiles.Contains(o.T) || !o.T.Alive) _on.RemoveAt(i);
        }
    }

    public void Clear() => _on.Clear();
}
