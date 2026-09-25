using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE PRISM: light caught on a guard and split (F11, kits_v2 Warrior card; kits_v31 §6: the
// children are Lines rows).
//
// REPLACED: v1's parry (a 0.35 s window, a riposte, a whiff lockout, a 25% beam leak) was drafted
// and never built; its blow buffer and per-peer RTT are dropped. What stands in its place is ONE
// rule read by every hostile blow that can be caught:
//   * a blow says what KIND it is (BlowKind): light (a boss's beam, a craft's or a bolt's ray), a
//     round in flight (a Shot), or something no prism touches (Area: a ring, a ram, the rock, a blast, a web);
//   * a pilot that can catch says so (IPrism: Status.Parrying, on the wire, and its guard g, which
//     faces the cursor clamped to ±90° off the nose by Melee.Guard, the clamp the blade shares);
//   * the angle θ between g and the line back to the source picks a BAND (Prism.Bands): SQUARE
//     sends most of it back along g and lets the rest through; SLANT mirrors half off the blade and
//     bends the other half θ/2 the other way; OPEN (past the last band) is not caught at all;
//   * the catcher takes nothing of a caught blow, and the children are two Lines rows
//     (Lines.PrismOut: the first hostile body, credited "prism"; Lines.PrismThrough: every pilot on
//     it, under the parent's name). Children strike through Lines, never through here, so they
//     never split again.
// A beam's judgement walks the pilots on it nearest first and stops at the first catcher
// (Prism.Walk, Boss.Burn); a round is reflected where it strikes (Shot.Strike: a Reflectable row,
// fired back as Shots.Reflect along the band's direction at its RoundSpeed); a ray -- a craft's
// laser (Raider.Strike), a boss's bolt -- splits where it lands.
//
// A NEW BAND is a row of Prism.Bands (its widest angle, what goes back and along what, what goes
// on and whether it bends). How often a stance may split and how long it lasts are the stance's
// own (AbilityDef.Stance: prism_split, prism_splits, prism_time), asked through IPrism.Split.
// ─────────────────────────────────────────────────────────────────────────────
// Area: a ring, a ram, the rock, a blast, a web -- what lands where it lands and is never caught
public enum BlowKind { Beam, Ray, Shot, Area }

// what the caught share goes back along: the guard itself, or the mirror of the light off the blade
public enum PrismOut { Guard, Mirror }

public sealed class PrismBand
{
    public string Id;
    public float MaxAngle;          // degrees: θ at most this falls in this band (the first that takes it)
    public double OutShare;         // what goes back as a friendly line
    public PrismOut OutAlong;
    public double ThroughShare;     // what goes on as the enemy's
    public bool Bends;              // the part that goes on turns θ/2 away from the mirrored part
    public float RoundSpeed;        // a reflected round's speed, x its own
}

// A PILOT THAT CAN CATCH LIGHT: whether it is catching now, and the world angle of its guard.
public interface IPrism
{
    bool Prismatic { get; }
    float GuardAngle { get; }
    // May this catch split now? The stance's own tick and cap (PlayerShip.Split); a caught blow that
    // may not still takes the catcher nothing. Counts the split when it says yes.
    bool Split();
}

// ONE RESOLVED CATCH: the band (-1: OPEN, not caught), the unit directions of the two children,
// their shares, and θ in degrees.
public readonly struct PrismSplit
{
    public readonly int Band;
    public readonly Vector2 Out, Through;
    public readonly double OutShare, ThroughShare;
    public readonly float Theta;
    public PrismSplit(int band, Vector2 outDir, double outShare, Vector2 through, double throughShare, float theta)
    { Band = band; Out = outDir; OutShare = outShare; Through = through; ThroughShare = throughShare; Theta = theta; }
    public bool Caught => Band >= 0;
}

public static class Prism
{
    public const float GuardMax = Mathf.Pi / 2f;                     // the guard clamp: ±90° off the nose
    public const float BeamReach = 1500f, RayReach = 900f, RayThrough = 400f;

    public static readonly PrismBand[] Bands =
    {
        new() { Id = "square", MaxAngle = 15f, OutShare = 0.75, OutAlong = PrismOut.Guard,  ThroughShare = 0.25, Bends = false, RoundSpeed = 1.5f },
        new() { Id = "slant",  MaxAngle = 60f, OutShare = 0.50, OutAlong = PrismOut.Mirror, ThroughShare = 0.50, Bends = true,  RoundSpeed = 1.0f },
    };

    // light and rounds may be caught; an Area blow never is
    public static bool Catchable(BlowKind k) => k == BlowKind.Beam || k == BlowKind.Ray || k == BlowKind.Shot;

    // the band θ (degrees) falls in, or -1 for OPEN. Pure.
    public static int BandOf(float thetaDeg)
    {
        for (int i = 0; i < Bands.Length; i++) if (thetaDeg <= Bands[i].MaxAngle) return i;
        return -1;
    }

    // THE RULE, pure: guard `g` and the light's heading `u` (from the source toward the catcher),
    // both unit. θ = angle(g, −u). SQUARE: back along g, the rest straight on along u. SLANT: back
    // along the mirror r = u − 2(u·g)g, the rest along u turned θ/2 away from r.
    public static PrismSplit Resolve(Vector2 g, Vector2 u)
    {
        float theta = Mathf.RadToDeg(g.AngleTo(-u));
        theta = Mathf.Abs(theta);
        int band = BandOf(theta);
        if (band < 0) return new PrismSplit(-1, Vector2.Zero, 0, u, 1, theta);
        var b = Bands[band];
        var mirror = u - 2f * u.Dot(g) * g;
        var outDir = b.OutAlong == PrismOut.Guard ? g : mirror.Normalized();
        float side = Mathf.Sign(u.Cross(mirror));
        var through = b.Bends ? u.Rotated(-side * Mathf.DegToRad(theta / 2f)) : u;
        return new PrismSplit(band, outDir, b.OutShare, through.Normalized(), b.ThroughShare, theta);
    }

    // What `at` makes of light heading `u` at it: OPEN unless it is a prism catching now.
    public static PrismSplit Resolve(IHittable at, Vector2 u) =>
        at is IPrism p && p.Prismatic ? Resolve(Vector2.Right.Rotated(p.GuardAngle), u) : new PrismSplit(-1, Vector2.Zero, 0, u, 1, 180f);

    // THE HOST CATCHES ONE, or does not: light of `kind` from `source` reaches `at` carrying `d`,
    // named `sourceName`; `end` is where the parent line would have run to (a beam's remaining length
    // is what goes on). Caught, the two children strike (Lines.PrismOut, Lines.PrismThrough) and it
    // returns true: the catcher takes nothing. A round (BlowKind.Shot) is reflected by Shot.Strike
    // instead, so it never comes here.
    public static bool Catch(IHittable at, BlowKind kind, Vector2 source, Vector2 end, double d, string sourceName)
    {
        if (kind == BlowKind.Shot || !Catchable(kind) || !Net.Sim) return false;
        var u = (at.Position - source).Normalized();
        var split = Resolve(at, u);
        if (!split.Caught) return false;
        if (at is IPrism p && !p.Split()) return true;       // caught between the stance's splits: nothing lands, nothing splits
        bool ray = kind == BlowKind.Ray;
        var from = at.Position;
        Lines.Strike(Lines.PrismOut, at as PlayerShip, from, from + split.Out, d * split.OutShare, ray ? RayReach : BeamReach);
        float on = ray ? RayThrough : Mathf.Max(0f, (end - from).Dot(u));
        if (on > 0f) Lines.Strike(Lines.PrismThrough, at as PlayerShip, from, from + split.Through, d * split.ThroughShare, on, sourceName, at);
        return true;
    }

    // A BEAM'S JUDGEMENT (Boss.Burn): the pilots on the segment a..b, `halfWidth` either side, nearest
    // first, each taking `d` under `sourceName` -- until the first that catches it, where it splits
    // and goes no further. Host.
    public static void Walk(Vector2 a, Vector2 b, float halfWidth, double d, Vector2 from, string sourceName, IEnumerable<PlayerShip> pool)
    {
        foreach (var p in Lines.Pick(a, b, halfWidth, 0, pool.ToList(), p => p.Position, p => p.HitRadius))
        {
            if (Catch(p, BlowKind.Beam, a, b, d, sourceName)) break;
            p.Hit(d, from, sourceName);
        }
    }
}
