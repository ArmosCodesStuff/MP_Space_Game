using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// A MELEE ARC: a blow that lands at once on everything inside a wedge in front of a hull -- nothing
// flies, nothing runs down a line. ONE ROW EACH (F12, kits_v2 §5).
//
// REPLACED: nothing yet -- there was no close-in weapon in the game. The Warrior's blade (160 u,
// ±55°), its whirlwind (210 u, all round) and whatever strikes the bodies a dash passes are the same
// thing with other numbers, so the thing is a row and the wedge is written once, here. The GUARD
// CLAMP beside it (Melee.Guard) is the one rule for "a facing that follows the cursor but never
// strays more than so far off the nose": the prism's guard (Prism.cs) reads it, so a stance and a
// blade can never disagree about where the hull is facing.
//
// A ROW (Melee.All, which the Warrior's kit opens in 6c with its blade and whirlwind) MUST FILL IN:
//   Id     the weapon name its blows carry (Dealt, DealtBy on the striker)
//   Reach  the stat id of its reach on the striker's sheet, measured from the hull's centre to the
//          edge of the body (a body whose own radius reaches into the arc is in it)
//   Arc    its half-angle either side of the nose, in degrees: 180 is all the way round
//   Stops  how many bodies it strikes, nearest first: 0 = everything in the arc (ShotDef.Stops' word)
// No row exists before its class does (D28): the rule is proved without one.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MeleeDef
{
    public string Id;
    public string Reach;
    public float Arc;
    public int Stops;
}

public static class Melee
{
    // WHAT AN ARC FROM `from`, facing `facing` (radians, Aim's world angle), `reach` long and
    // `halfArc` (radians) either side, strikes, nearest first: every body in `pool` whose centre is
    // within reach plus its radius and whose bearing is within halfArc of the facing, the first
    // `stops` of them (0 = all). Pure -- no world, no frame.
    public static List<T> Pick<T>(Vector2 from, float facing, float reach, float halfArc, int stops, IEnumerable<T> pool,
                                  Func<T, Vector2> at, Func<T, float> radius)
    {
        var inArc = pool.Where(t =>
                         {
                             var off = at(t) - from;
                             float d = off.Length();
                             if (d > reach + radius(t)) return false;
                             return d < 1e-3f || Mathf.Abs(Mathf.AngleDifference(facing, off.Angle())) <= halfArc;
                         })
                        .OrderBy(t => from.DistanceSquaredTo(at(t)))
                        .ToList();
        return stops > 0 && inArc.Count > stops ? inArc.GetRange(0, stops) : inArc;
    }

    // THE GUARD CLAMP: a facing that follows `aim` (a world angle, radians) but never more than
    // `maxOff` either side of `nose`. The prism's guard (±90°) and any stance that faces the cursor
    // read this one rule. Pure.
    public static float Guard(float nose, float aim, float maxOff) =>
        nose + Mathf.Clamp(Mathf.AngleDifference(nose, aim), -maxOff, maxOff);

    // A HULL'S NOSE as a world angle: every hull is drawn nose-up, so it faces Up turned by its rotation.
    public static float Nose(Node2D hull) => Vector2.Up.Rotated(hull.Rotation).Angle();

    // THE HOST LANDS ONE: the row's arc off `by`'s nose, its reach off `by`'s sheet, `damage` to each
    // hostile it strikes through the door (credited to `by` under the row's Id). Returns what it struck.
    public static List<IHittable> Strike(MeleeDef d, PlayerShip by, double damage)
    {
        if (!Net.Sim) return new List<IHittable>();
        var hit = Pick(by.Position, Nose(by), (float)by.Stats[d.Reach], Mathf.DegToRad(d.Arc), d.Stops,
                       Targeting.Hittable(Combat.Hostiles, Targeting.Attackable).ToList(), h => h.Position, h => h.HitRadius);
        foreach (var h in hit)
        {
            Dealt.Deal(h, damage, by, d.Id);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
        return hit;
    }
}
