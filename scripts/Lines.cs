using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// A LINE: a weapon that lands at once along a straight segment -- nothing flies. ONE ROW EACH.
//
// REPLACED: the railgun's own loop in PlayerShip.FireRail (a segment from the nose, every hostile
// within half its width of it struck, the bar and the report raised by hand). Time on target's
// converging lines (6b) and the prism's split children (slice 5) are the same thing with other
// numbers, so the thing is a row and the loop is written once, here.
//
// A ROW OF Lines.All MUST FILL IN:
//   Id      the weapon name its blows carry (Dealt, DealtBy on the firer)
//   Width   the stat id of its width on the firer's sheet: a body within half of it (plus the
//           body's own radius) of the segment is on the line
//   Reach   the stat id of its length on the firer's sheet
//   Stops   how many bodies it strikes, nearest first along it: 0 = everything on it. The same word
//           and the same rule as a shot's (ShotDef.Stops)
//   Fx      the Fx row drawn along it, on every peer (to the last body struck, for a line that stops)
//   Beam    the Beam row of its report, on every peer
// Width and Reach are stat ids, not numbers, so gear, pilot points and lifts move them as they move
// any other stat. The index is not on the wire: the Fx and the flash are.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class LineDef
{
    public string Id;
    public string Width, Reach;
    public int Stops;
    public int Fx, Beam;
}

public static class Lines
{
    public const int Rail = 0;

    public static readonly LineDef[] All =
    {
        // the sniper's railgun: 2500 x 14 u along the nose, through everything on it
        new() { Id = Dealt.Rail, Width = "rail_width", Reach = "rail_range", Stops = 0, Fx = global::Fx.Rail, Beam = global::Beam.Rail },
    };

    public static LineDef Of(int row) => All[row >= 0 && row < All.Length ? row : Rail];

    // WHAT A LINE FROM a TO b STRIKES, nearest first along it: every body in `pool` within
    // `halfWidth` plus its own radius of the segment, the first `stops` of them (0 = all). Pure --
    // no world, no frame -- so the rule is provable apart from any weapon that uses it.
    public static List<T> Pick<T>(Vector2 a, Vector2 b, float halfWidth, int stops, IEnumerable<T> pool,
                                  Func<T, Vector2> at, Func<T, float> radius)
    {
        var dir = b - a;
        var on = pool.Where(t => Combat.DistToSegment(at(t), a, b) <= halfWidth + radius(t))
                     .OrderBy(t => (at(t) - a).Dot(dir))
                     .ToList();
        return stops > 0 && on.Count > stops ? on.GetRange(0, stops) : on;
    }

    // THE HOST LANDS ONE: from `a` along `dir`, the row's reach and width off `by`'s sheet, `damage`
    // to each body it strikes through the door (credited to `by` under the row's Id), then the bar
    // and the report on every peer. Returns where the drawn line ends.
    public static Vector2 Strike(int row, PlayerShip by, Vector2 a, Vector2 dir, double damage)
    {
        var d = Of(row);
        var b = a + dir.Normalized() * (float)by.Stats[d.Reach];
        float halfWidth = (float)by.Stats[d.Width] * 0.5f;
        var hit = Pick(a, b, halfWidth, d.Stops, Targeting.Hittable(Combat.Hostiles, Targeting.Attackable).ToList(),
                       h => h.Position, h => h.HitRadius);
        foreach (var h in hit)
        {
            Dealt.Deal(h, damage, by, d.Id);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
        var end = d.Stops > 0 && hit.Count > 0 && hit.Count >= d.Stops
            ? a + dir.Normalized() * (hit[^1].Position - a).Dot(dir.Normalized())
            : b;
        global::Fx.Line(d.Fx, a, end);                      // the line it threw, on every peer
        Combat.Flash(a, end, d.Beam);                       // ...and its report, on every peer
        return end;
    }
}
