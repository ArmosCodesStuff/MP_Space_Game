using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// A GUN, AND WHATEVER IT IS BOLTED TO.
//
// The turret was a part of PlayerShip: it read the ship's stat sheet, the ship's art, the ship's
// point-defence window and the ship's accent colour. So the only thing in the game that could
// mount one was a pilot's ship -- and a freighter's deployed turret, a hauler's own mount, the
// base's gun and a class whose point defence never switches off all wanted the same swing, the
// same claim-sharing acquisition and the same reload that carries its remainder.
//
// A turret now asks its HOST two things: what gun this is (TurretSpec -- numbers and art, read
// every tick so a bonus applies at once) and whether it may fire. Anything that can answer those
// can mount one.
//
//   MAIN GUNS aim where the host says (a pilot's cursor) and fire only when the host tells them
//             to; the host decides when (salvo or staggered), the turret just shoots along
//             wherever its barrel points at that moment.
//   POINT DEFENCE fires by itself while the host says it is online. Every turret picks and tracks
//             its OWN target, preferring one no sibling has claimed, so a group spreads across
//             what is coming rather than piling onto one of it.
// ─────────────────────────────────────────────────────────────────────────────

// What the gun IS. A struct, built per tick: the numbers live wherever the host keeps them (a
// ship's sheet, a yard upgrade, a boss's table), and the turret never learns which.
public struct TurretSpec
{
    public double Damage, Interval;      // per shot, and the seconds between shots
    public float Range, Turn;            // its reach, and how fast the barrel swings (rad/s)
    public float ShellSpeed;             // main guns: the shell's speed
    public string Texture;               // the sprite (barrels up, pivot at the sheet's centre)
    public float TexScale;               // world units per turret-texture pixel
    public float Barrel, Ring;           // muzzle from the pivot; the radius of the ring it draws
    public Color Tint;
}

public interface ITurretHost
{
    Node2D AsNode { get; }                      // what the turret is a child of, and turns with
    TurretSpec Spec(bool pd);
    bool PdOnline { get; }                      // point defence may fire now (a window, or always)
    float PdRing { get; }                       // 0..1 the PD ring shows, 0 for no ring at all
    Vector2 AimAt { get; }                      // where the main guns point
    float FastSwing { get; }                    // 0, or a turn rate that overrides the spec's
    IReadOnlyList<Turret> Siblings { get; }     // PD turrets that may have claimed a target
    void NoteDealt(double d, Vector2 at);       // what this gun just did, and where it landed
    PlayerShip Credit { get; }                  // whose shell it is, for the tally (may be null)
}

public partial class Turret : Node2D
{
    public ITurretHost Host;
    public Vector2 Offset;
    public bool PointDefense;

    private float _angle;
    private double _cd;
    public IHittable Target { get; private set; }

    private bool Online => !PointDefense || Host.PdOnline;
    // a target that has left the world (Hub.DropRaider, through PlayerShip.Forget)
    public void Forget(IHittable t) { if (ReferenceEquals(Target, t)) Target = null; }

    private TurretSpec S => Host.Spec(PointDefense);
    public float Range => S.Range;
    public double Interval => S.Interval;
    // Through a broadside the main turrets swing fast enough to come round from anywhere onto the
    // cursor within the wind-up; the host names that rate, and the faster of the two wins.
    private float RotSpeed
    {
        get { var s = S; return PointDefense ? s.Turn : Mathf.Max(s.Turn, Host.FastSwing); }
    }

    private Sprite2D _sprite;          // the turret's own sprite (TurretSpec.Texture)

    public void Setup(ITurretHost host, Vector2 offset, bool pd)
    {
        Host = host; Offset = offset; PointDefense = pd;
        ZIndex = 5;
        var spec = S;
        // stored barrels-UP; the turret's barrel direction is +X, so turn it a quarter
        _sprite = new Sprite2D { Texture = GD.Load<Texture2D>(spec.Texture), Rotation = Mathf.Pi / 2f,
                                 Scale = Vector2.One * spec.TexScale };
        AddChild(_sprite);
        Recolor();
        // A child of the host already inherits its rotation, so the mount offset is
        // set once, in the host's frame. Rotating it by the host's rotation again walked
        // the mounts off the hull as it turned.
        Position = Offset;
        _angle = Host.AsNode.Rotation - Mathf.Pi / 2f;
        GlobalRotation = _angle;
    }

    public void Tick(double delta)
    {
        if (!PointDefense)
        {
            // Main guns follow the cursor on every peer: the owner's own mouse, or the
            // aim point it last sent. On guests this is cosmetic; the host's copy is
            // the one whose barrel direction decides where shots go.
            Swing((Host.AimAt - GlobalPosition).Angle(), delta);
            QueueRedraw();
            return;
        }

        float rest = Host.AsNode.Rotation - Mathf.Pi / 2f;
        if (!Online) { Target = null; _cd = 0; Swing(rest, delta); QueueRedraw(); return; }

        // Acquire. Runs on guests too, as cosmetics: the same rule on the same
        // positions picks the same targets, so a guest sees its turrets track what
        // the host's are shooting. Only the host's copy deals damage.
        Vector2 wp = GlobalPosition;
        if (Target == null || !Target.Alive || wp.DistanceTo(Target.Position) > Range * 1.15f) Target = Acquire(wp);

        float desired = Target != null ? (Target.Position - wp).Angle() : rest;
        float diff = Swing(desired, delta);
        if (!Net.Sim) { QueueRedraw(); return; }

        // An 8-degree cone, so a turret still swinging does not fire. The reload
        // CARRIES its remainder rather than resetting: resetting rounds every shot up
        // to a whole frame, which quietly cost 3% of PD's stated DPS at 60 fps.
        bool canFire = Target != null && Mathf.Abs(diff) <= Mathf.DegToRad(8f);
        _cd -= delta;
        if (!canFire) { if (_cd < 0) _cd = 0; }
        else for (int n = 0; _cd <= 0 && n < 8; n++)
        {   // the target held for the shot: a shot that kills it drops it from this turret (Hub.DropRaider)
            var tgt = Target;
            if (tgt == null) break;
            var spec = S;
            _cd += spec.Interval;
            tgt.TakeDamage(spec.Damage); Host.NoteDealt(spec.Damage, tgt.Position);
            Combat.Flash(wp + Vector2.Right.Rotated(GlobalRotation) * spec.Barrel, tgt.Position, new Color(0.7f, 0.95f, 1f));
        }
        QueueRedraw();
    }

    // Point defence shoots ONLY missiles and small craft (light raiders, fighters, the practice
    // fighters) -- never heavies, bosses or the dummy hulks (Targeting.PointDefence). Missiles
    // first; within that, the nearest one no sibling turret has claimed (if all are claimed, the
    // nearest regardless).
    public static int PdPriority(IHittable h) => TagExt.Is(h, Tag.Missile) ? 0 : TagExt.Is(h, Tag.Light | Tag.Fighter) ? 1 : 2;
    // Best by (priority, then distance), preferring one no sibling turret has claimed, falling
    // back to the best claimed one -- in a single pass with no allocation. This used to be a LINQ
    // chain with two lists built and sorted, and Tick calls it EVERY FRAME FOR EVERY PD TURRET
    // while point defence is active with nothing in range, because a null Target never satisfies
    // the guard.
    private IHittable Acquire(Vector2 from)
    {
        IHittable bestFree = null, bestAny = null;
        int freePri = 0, anyPri = 0; float freeDist = 0, anyDist = 0;
        float range = Range;
        foreach (var h in Combat.Hostiles)
        {
            if (!Targeting.PointDefence.Allows(h)) continue;
            float d = from.DistanceTo(h.Position);
            if (d > range) continue;
            int p = PdPriority(h);
            if (bestAny == null || p < anyPri || (p == anyPri && d < anyDist)) { bestAny = h; anyPri = p; anyDist = d; }
            bool claimed = false;
            foreach (var t in Host.Siblings) if (t != this && t.Target == h) { claimed = true; break; }
            if (claimed) continue;
            if (bestFree == null || p < freePri || (p == freePri && d < freeDist)) { bestFree = h; freePri = p; freeDist = d; }
        }
        return bestFree ?? bestAny;
    }

    // One main-gun shot, along the barrel as it points RIGHT NOW: a SHELL, straight, at the guns'
    // own shell speed, as far as their range, at `mult` times a shell's damage (a broadside's
    // multiple). Host only. (Point defence never comes here: it fires from Tick, at what it has acquired.)
    public void Shoot(double mult = 1.0)
    {
        if (!Net.Sim) return;
        var spec = S;
        var dir = Vector2.Right.Rotated(GlobalRotation);
        Combat.FireShell(GlobalPosition + dir * spec.Barrel, dir, spec.ShellSpeed, spec.Range, spec.Damage * mult, Host.Credit);
    }

    // _angle is a WORLD angle, so it is applied as GlobalRotation; as a local
    // rotation it would add the host's heading on top. Returns how far off the
    // desired angle the barrel was before this step.
    private float Swing(float desired, double delta)
    {
        float diff = Mathf.Wrap(desired - _angle, -Mathf.Pi, Mathf.Pi);
        float step = RotSpeed * (float)delta;
        _angle = Mathf.Abs(diff) <= step ? desired : _angle + Mathf.Sign(diff) * step;
        GlobalRotation = _angle;
        return diff;
    }

    // The turret takes the host's accent (turrets, engines, trim) over its hull colour.
    public void Recolor()
    {
        if (_sprite != null) _sprite.Modulate = S.Tint;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // PD shows the host's cycle, just outside its ring: the firing window running down, or
        // the recharge. Main guns draw nothing here, and a host whose PD never switches off shows
        // no ring at all (PdRing 0), so leave before doing any of the work -- this runs per
        // turret, per frame.
        if (!PointDefense) return;
        float frac = Host.PdRing;
        if (frac <= 0) return;
        var spec = S;
        var lit = Online ? spec.Tint : new Color(0.35f, 0.35f, 0.40f);
        float r = spec.Ring + 1.6f;
        DrawArc(Vector2.Zero, r, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * frac, 16,
                Online ? new Color(lit.R, lit.G, lit.B, 0.9f) : new Color(1f, 0.5f, 0.3f, 0.6f), 1.2f);
    }
}
