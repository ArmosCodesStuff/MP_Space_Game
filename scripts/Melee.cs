using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// A MELEE ARC: a blow that lands at once on everything inside a wedge in front of a hull -- nothing
// flies, nothing runs down a line. ONE ROW EACH (F12, kits_v2 §5).
//
// REPLACED: the Warrior's twin cannons (6c) -- there was no close-in weapon before. The Warrior's blade (160 u,
// ±55°), its whirlwind (210 u, all round) and whatever strikes the bodies a dash passes are the same
// thing with other numbers, so the thing is a row and the wedge is written once, here. The GUARD
// CLAMP beside it (Melee.Guard) is the one rule for "a facing that follows the cursor but never
// strays more than so far off the nose": the prism's guard (Prism.cs) reads it, so a stance and a
// blade can never disagree about where the hull is facing.
//
// A ROW (Melee.All: the Warrior's blade and whirlwind) MUST FILL IN:
//   Id     the weapon name its blows carry (Dealt, DealtBy on the striker)
//   Reach  the stat id of its reach on the striker's sheet, measured from the hull's centre to the
//          edge of the body (a body whose own radius reaches into the arc is in it)
//   Arc    its half-angle either side of the nose, in degrees: 180 is all the way round
//   Stops  how many bodies it strikes, nearest first: 0 = everything in the arc (ShotDef.Stops' word)
//   Damage the stat id of one blow, and Every the stat id of the seconds between blows (an interval: the
//          ship's Cadence, so a rate lift reaches it). An ability row names its melee row (AbilityDef.Swing)
//          and PlayerShip.Swings lands it while that row runs: a Hold row while the trigger holds, a Press
//          row while its Left runs.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MeleeDef
{
    public string Id;
    public string Reach;
    public float Arc;
    public int Stops;
    public string Damage, Every;
}

public static class Melee
{
    // THE ROWS. The blade: the Warrior's primary (kits_v2 Warrior card: 160 u, ±55°, 26 every 0.40 s).
    public static readonly MeleeDef Blade = new() { Id = "blade", Reach = "blade_reach", Arc = 55f, Stops = 0,
                                                    Damage = "blade_damage", Every = "blade_interval" };

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

    // THE SWING, DRAWN on every peer from what the wire already carries (the trigger bit, the slots'
    // Left): each running Swing row's wedge in the hull's own frame (nose up), its edge, and a blade
    // sweeping across it once every that row's Every seconds. Called from PlayerShip._Draw.
    public static void Draw(PlayerShip s)
    {
        if (!s.Alive) return;
        foreach (var def in Abilities.For(s.Class))
        {
            if (def.Swing is not { } row || !Running(s, def)) continue;
            float reach = (float)s.Stats[row.Reach], half = Mathf.DegToRad(row.Arc);
            float nose = -Mathf.Pi / 2f, every = (float)Math.Max(0.05, s.Stats[row.Every]);
            float phase = (float)(s.Clock % every) / every;
            var edge = new Color(0.85f, 0.95f, 1f, 0.55f);
            s.DrawArc(Vector2.Zero, reach, nose - half, nose + half, 32, edge, 2f);
            if (row.Arc < 180f)
            {
                s.DrawLine(Vector2.Zero, Vector2.Right.Rotated(nose - half) * reach, new Color(edge, 0.25f), 1.5f);
                s.DrawLine(Vector2.Zero, Vector2.Right.Rotated(nose + half) * reach, new Color(edge, 0.25f), 1.5f);
            }
            float sweep = nose - half + 2f * half * phase;
            s.DrawLine(Vector2.Right.Rotated(sweep) * reach * 0.25f, Vector2.Right.Rotated(sweep) * reach,
                       new Color(1f, 1f, 1f, 0.85f * (1f - phase)), 3f);
        }
    }

    // WHETHER A SWING ROW IS SWINGING NOW: a Hold row while the trigger holds and nothing stills it, a
    // Press row while its Left runs. The one rule PlayerShip.Swings (host) and Draw (every peer) read.
    public static bool Running(PlayerShip s, AbilityDef def) =>
        def.Kind == AbilityKind.Hold ? s.Trigger && !s.Stilled : s.Sl(def.Id).Left > 0;

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
