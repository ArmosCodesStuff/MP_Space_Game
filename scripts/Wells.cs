using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// GRAVITY WELLS (kits 6b, D39) — a patch of ground that drags small craft into its centre.
//
// REPLACED: nothing; the first is the Bastion's E (PlayerShip.CastWell).
//
// A WELL MUST FILL IN (from its caster's sheet as it goes down):
//   At       its centre
//   Radius   a craft whose centre is within this is pulled
//   Left     seconds it has left
//   Light    how fast it drags a light craft (u/s)
//   Heavy    ...and anything heavier (u/s)
// WHAT MOVES is Targeting.Pullable: raiding craft, light or heavy. A boss, a structure, a practice dummy and
// anything in flight never move, and neither does a craft LATCHED on its prey (ISquadMember.Latched) or on
// a tow line (ITowable.Towed): the well pulls what is loose. It drags a craft straight at its centre and
// never past it.
//
// AUTHORITY: the host keeps the list (Hub.Wells) and moves the craft; every peer sees them move in the
// world state it already sends, and draws the well from the one Fx raise (Fx.Well) made as it went down.
// ─────────────────────────────────────────────────────────────────────────────
public struct Well
{
    public Vector2 At;
    public float Radius, Light, Heavy;
    public double Left;
}

public static class Wells
{
    // One host tick: every well pulls what it reaches, and a well whose time is up is gone.
    public static void Tick(List<Well> wells, List<IHittable> hostiles, double delta)
    {
        for (int i = wells.Count - 1; i >= 0; i--)
        {
            var w = wells[i];
            foreach (var h in Targeting.Hittable(hostiles, Targeting.Pullable))
            {
                if (h is not Node2D n || h is ISquadMember { Latched: true } || h is ITowable { Towed: not null }) continue;
                var off = w.At - n.GlobalPosition;
                float d = off.Length();
                if (d > w.Radius || d < 1e-3f) continue;
                float rate = TagExt.Is(h, Tag.Heavy) ? w.Heavy : w.Light;
                n.GlobalPosition += off / d * Mathf.Min(d, rate * (float)delta);
            }
            w.Left -= delta;
            if (w.Left <= 0) wells.RemoveAt(i); else wells[i] = w;
        }
    }
}
