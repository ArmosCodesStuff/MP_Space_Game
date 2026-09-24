using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE PREDICTED MISSILE — a spot guessed ahead of a target, a circle marking it for the whole
// flight, and a blast when the flight is up. ONE MECHANIC, ONE ROW PER SIDE.
//
// REPLACED: the heavy fighter's missile, written into three files as if a raider were the only
// thing that could ever throw one --
//   Raider    PredictSpot, MaxStep, WildSpeed, MaxLead, and a hand-kept _lastTargetPos/_targetVel
//             pair recomputed inline every frame
//   Hub       a `_blasts` tuple whose third field was a RAIDER id, a blast resolved against
//             Hub.RaiderTargets() and nothing else, landing through IRaidTarget.Hit and nothing
//             else, at Raider.BlastRadius over Raider.MissileFlight
//   HubNodes  HeavyMissileVisual, with the raiders' red painted into its _Draw
// The outposts answer a blockade with the SAME missile from the other side (Lanes.cs), so WHOSE
// it is is a row and the launcher's numbers are a struct. Nothing below this comment names a
// raider or an outpost.
//
// WHAT ONE IS: MissileSpec -- built per shot, exactly as TurretSpec is, so the numbers live
// wherever the launcher keeps them (an enemy's row, a yard upgrade) and this file never learns
// which.
//
// A ROW OF Missiles.All MUST FILL IN:
//   Id               what it is called -- AND the family name a blow from it carries, to which
//                    the blast adds the launcher's own id, so two launchers are never mistaken
//                    for one source (see DamageSource, PlayerShip.Incoming)
//   Mark             the Fx row its telegraph is drawn as: red for a threat, friendly for ours
//   Body/Nose/Glow   what the thing in the air is painted in
//   Pool             everything the blast might catch
//   Prey             ...and who in there it may hurt -- a TargetFilter, never a type test
//   Land             how a hit lands on one of those
//
// THE INDEX IS ON THE WIRE (Hub.NetMissile carries it, so a guest paints the right missile), so
// APPEND ONLY.
//
// AUTHORITY: only the host throws one (Hub.ThrowMissile) and only the host lands the blast
// (Hub.TickBlasts). A guest is told the launch and flies a cosmetic copy, as it does every shot,
// and its telegraph arrives down the effect RPC every other warning uses.
// ─────────────────────────────────────────────────────────────────────────────

// WHAT A PREDICTED MISSILE IS, at the moment it is thrown.
public struct MissileSpec
{
    public int Side;          // a row of Missiles.All: whose it is, and what colour
    public double Damage;     // what the blast does where it lands
    public float Blast;       // how wide it is there
    public double Flight;     // how long it is in the air -- and so how far ahead it must guess
}

public sealed class MissileSide
{
    public string Id;                                        // reached by row, never by type
    public int Mark;                                         // the Fx row its telegraph is
    public Color Body, Nose, Glow;
    public Func<Hub, IEnumerable<Node2D>> Pool;
    public TargetFilter Prey;
    public Action<Node2D, double, Vector2, string> Land;
}

public static class Missiles
{
    // The index IS the id on the wire (Hub.NetMissile), so APPEND ONLY.
    public const int Raid = 0, Outpost = 1;

    // More than Step in one frame is a JUMP (a warp; 3000 u/s -- nothing flies that fast) and not
    // motion; faster than Wild is not flying at all (4x a capital ship). Both were Raider's own
    // private consts, and the second launcher would have copied them.
    public const float Step = 50f, Wild = 400f;

    public static readonly MissileSide[] All =
    {
        // A RAIDER'S. Its blast catches what a raid can reach -- the pilots, the fleet, the
        // turrets a freighter left out (Hub.RaiderTargets) -- and lands as every raider blow
        // lands. Its id is "heavy" because that is the name already on this blow in every
        // pilot's damage tally.
        new() { Id = "heavy", Mark = Fx.WarnZone,
                Body = new Color(0.45f, 0.30f, 0.28f), Nose = new Color(1f, 0.30f, 0.25f),
                Glow = new Color(1f, 0.60f, 0.30f),
                Pool = h => h.RaiderTargets(), Prey = Targeting.Attackable,
                Land = (n, d, at, src) => (n as IRaidTarget)?.Hit(d, at, src) },

        // AN OUTPOST'S (Lanes.cs). The same missile from the other side: it looks through the
        // hostiles and may hurt RAIDING CRAFT alone -- Targeting.Craft, the filter the base's own
        // guns already use -- so a pilot, a miner, a hauler, a dropped turret and a practice
        // dummy are every one of them outside it without a word about what class any of them is.
        new() { Id = "outpost", Mark = Fx.AimZone,
                Body = new Color(0.28f, 0.34f, 0.42f), Nose = new Color(0.45f, 0.85f, 1f),
                Glow = new Color(0.70f, 0.95f, 1f),
                Pool = _ => Combat.Hostiles.OfType<Node2D>(), Prey = Targeting.Craft,
                Land = (n, d, at, src) => (n as IHittable)?.TakeDamage(d) },
    };

    public static MissileSide Of(int side) => All[side >= 0 && side < All.Length ? side : Raid];

    // WHERE IT AIMS: the target carried `flight` forward by its velocity -- UNLESS anything is out
    // of place (a wild speed, a broken number, a spot absurdly far off): then right at where the
    // target is now. A longer flight is a LONGER GUESS, so the bound follows the flight it was
    // handed rather than being written down a second time.
    public static Vector2 Predict(Vector2 pos, Vector2 vel, double flight)
    {
        bool sane = float.IsFinite(vel.X) && float.IsFinite(vel.Y) && vel.Length() <= Wild;
        var spot = sane ? pos + vel * (float)flight : pos;
        float lead = Wild * (float)Math.Max(0, flight);
        return float.IsFinite(spot.X) && float.IsFinite(spot.Y) && spot.DistanceTo(pos) <= lead ? spot : pos;
    }
}

// WHAT A TARGET IS DOING, frame to frame: the velocity a prediction is made from. A launcher
// holds one of these and feeds it the target's position; a jump is read as standing still, or the
// predicted spot lands miles away. Raider kept this by hand in two fields, and the second
// launcher would have kept a second copy of the same four lines.
public struct Lead
{
    private Vector2 _last;
    private bool _has;
    public Vector2 Velocity { get; private set; }
    public void Watch(Vector2 at, double delta)
    {
        var moved = at - _last;
        Velocity = _has && delta > 0 && moved.Length() < Missiles.Step ? moved / (float)delta : Vector2.Zero;
        _last = at; _has = true;
    }
}

// THE THING IN THE AIR, flying straight to its marked point (the host lands the blast). Its
// colours are its side's and its size is the raise's, so there is one of these and not one a side.
public partial class MissileVisual : Node2D
{
    public int Side;
    public Vector2 From, To;
    public double Flight;
    public float Blast = 90f;                  // the burst it leaves; the thrower always says
    private double _t;
    private MissileSide Def => Missiles.Of(Side);

    public override void _Ready() { Position = From; Rotation = Aim.Face(From, To); ZIndex = 6; }

    public override void _Process(double delta)
    {
        _t += delta;
        Position = From.Lerp(To, (float)Math.Min(1, _t / Math.Max(1e-3, Flight)));
        if (_t >= Flight) { Fx.Raise(Fx.Burst, To, Blast * 0.8f); QueueFree(); }
        QueueRedraw();
    }

    public override void _Draw()
    {   // fat: wider and longer than the player's missile
        var d = Def;
        DrawRect(new Rect2(-5f, -16f, 10f, 34f), d.Body);
        DrawColoredPolygon(new[] { new Vector2(-5f, -16f), new Vector2(0, -25f), new Vector2(5f, -16f) }, d.Nose);
        DrawCircle(new Vector2(0, 19f), 4.2f, d.Glow);
    }
}
