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
// numbers, so the thing is a row and the loop is written once, here. The prism's children (Prism.cs)
// are two rows: `prism_out` (the caught light sent back, to the first hostile) and `prism_through`
// (what continues on as the enemy's, landing on every pilot on it under the parent's own name).
//
// A ROW OF Lines.All MUST FILL IN:
//   Id      the weapon name its blows carry (Dealt, DealtBy on the firer)
//   Width   the stat id of its width on the firer's sheet: a body within half of it (plus the
//           body's own radius) of the segment is on the line -- or null, and Wide is the width in u
//           (a line no sheet owns: the prism's children are 36 u whoever catches the light)
//   Reach   the stat id of its length on the firer's sheet, or null for a row whose every caller
//           names its reach (Strike's `reach`: a prism child's depends on the light it split)
//   Side    whom it lands on: Hostiles (a player's line, through the door, credited to the firer)
//           or Pilots (the enemy's light continuing, PlayerShip.Hit under the caller's source name)
//   Stops   how many bodies it strikes, nearest first along it: 0 = everything on it. The same word
//           and the same rule as a shot's (ShotDef.Stops)
//   AtTarget  true: the line ends at the point it is aimed at (Time on target's lines stop at the
//           painted target), never past it and never past its Reach; false: it runs its whole Reach
//           along the aim, through the aim point
//   Fx      the Fx row drawn along it, on every peer (to the last body struck, for a line that stops)
//   Beam    the Beam row of its report, on every peer
// Width and Reach are stat ids, not numbers, so gear, pilot points and lifts move them as they move
// any other stat. The index is not on the wire: the Fx and the flash are.
// ─────────────────────────────────────────────────────────────────────────────
public enum LineSide { Hostiles, Pilots }

public sealed class LineDef
{
    public string Id;
    public string Width, Reach;
    public float Wide;
    public LineSide Side;
    public int Stops;
    public bool AtTarget;
    public int Fx, Beam;
}

public static class Lines
{
    public const int Rail = 0, PrismOut = 1, PrismThrough = 2, RailEnhanced = 3, Tot = 4;

    public static readonly LineDef[] All =
    {
        // the sniper's railgun: 2500 x 14 u along the nose, through everything on it, its whole reach
        new() { Id = Dealt.Rail, Width = "rail_width", Reach = "rail_range", Stops = 0, AtTarget = false, Fx = global::Fx.Rail, Beam = global::Beam.Rail },
        // the prism's children (F11): 36 u, the reach the light gives them. What is sent back takes the
        // first hostile body; what goes on lands on every pilot on it, the catcher never
        new() { Id = Dealt.Prism, Wide = 36f, Stops = 1, Side = LineSide.Hostiles, Fx = global::Fx.Rail, Beam = global::Beam.Rail },
        new() { Id = "prism_through", Wide = 36f, Stops = 0, Side = LineSide.Pilots, Fx = global::Fx.Rail, Beam = global::Beam.Rail },
        // an enhanced rail round (ActiveReload): the rail's line and credit, drawn and heard white
        new() { Id = Dealt.Rail, Width = "rail_width", Reach = "rail_range", Stops = 0, AtTarget = false, Fx = global::Fx.RailEnhanced, Beam = global::Beam.RailEnhanced },
        // the freighter's Time on target (6b, D36): one line from each gun that can reach the paint,
        // 14 u wide, ending AT the painted target, landing on every hostile on it
        new() { Id = Dealt.Tot, Width = "tot_width", Reach = "tot_reach", Stops = 0, AtTarget = true, Fx = global::Fx.Tot, Beam = global::Beam.Tot },
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

    // WHERE A LINE AIMED FROM `from` AT `to` ENDS, for a row whose Reach on the firer's sheet is
    // `reach`: at `to` (never past `reach`) for an AtTarget row, else `reach` along from->to. Pure;
    // Strike reads its segment here and nowhere else.
    public static Vector2 End(LineDef d, float reach, Vector2 from, Vector2 to) =>
        from + (to - from).Normalized() * (d.AtTarget ? Mathf.Min(from.DistanceTo(to), reach) : reach);

    // THE HOST LANDS ONE: from `from` aimed at `to` (End says how far it runs), the row's width off
    // `by`'s sheet (or its Wide), `damage` to each body it strikes -- a Hostiles row through the door
    // (credited to `by` under the row's Id), a Pilots row by PlayerShip.Hit under `source`, never on
    // `skip` -- then the bar and the report on every peer. `reach` >= 0 names the length for a row
    // with no Reach stat. Returns where the drawn line ends.
    public static Vector2 Strike(int row, PlayerShip by, Vector2 from, Vector2 to, double damage,
                                 float reach = -1f, string source = null, IHittable skip = null)
    {
        var d = Of(row);
        var b = End(d, reach >= 0 ? reach : (float)by.Stats[d.Reach], from, to);
        var dir = (b - from).Normalized();
        float halfWidth = (d.Width != null ? (float)by.Stats[d.Width] : d.Wide) * 0.5f;
        var pool = d.Side == LineSide.Pilots ? Combat.Players : Combat.Hostiles;
        var hit = Pick(from, b, halfWidth, d.Stops, Targeting.Hittable(pool, Targeting.Attackable).Where(h => h != skip).ToList(),
                       h => h.Position, h => h.HitRadius);
        foreach (var h in hit)
        {
            if (d.Side == LineSide.Pilots) (h as PlayerShip)?.Hit(damage, from, source);
            else Dealt.Deal(h, damage, by, d.Id);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
        var end = d.Stops > 0 && hit.Count > 0 && hit.Count >= d.Stops
            ? from + dir * (hit[^1].Position - from).Dot(dir)
            : b;
        global::Fx.Line(d.Fx, from, end);                   // the line it threw, on every peer
        Combat.Flash(from, end, d.Beam);                    // ...and its report, on every peer
        return end;
    }
}
