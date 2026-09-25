using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// A GUN, AND WHATEVER IT IS BOLTED TO.
//
// The turret was a part of PlayerShip: it read the ship's stat sheet, the ship's art, the ship's
// point-defence window and the ship's accent colour. So the only thing in the game that could
// mount one was a pilot's ship -- and a freighter's deployed turret, a hauler's own mount, the
// base's gun and a warden's heavy point defence all wanted the same swing, the
// same claim-sharing acquisition and the same reload that carries its remainder.
//
// A turret now asks its HOST two things: what gun this is (TurretSpec -- numbers, art, the row
// of Shots.All it fires and what it may pick, read every tick so a bonus applies at once) and
// whether it may fire. Anything that can answer those can mount one.
//
//   MAIN GUNS aim where the host says (a pilot's cursor) and fire only when the host tells them
//             to; the host decides when (salvo or staggered), the turret just shoots along
//             wherever its barrel points at that moment.
//   POINT DEFENCE fires by itself while the host says it is online. Every turret picks and tracks
//             its OWN target, preferring one no sibling has claimed, so a group spreads across
//             what is coming rather than piling onto one of it. WHAT it may pick is the gun's
//             own (TurretSpec.Prey): a warship's and the hauler's take missiles and small craft
//             (Targeting.PointDefence); a turret left standing takes anything hostile
//             (Targeting.Sentry). Nothing tells it to let go: what it holds is asked of the live
//             world every tick (StillThere).
// ─────────────────────────────────────────────────────────────────────────────

// What the gun IS. A struct, built per tick: the numbers live wherever the host keeps them (a
// ship's sheet, a yard upgrade, a boss's table), and the turret never learns which.
public struct TurretSpec
{
    public double Damage, Interval;      // per shot, and the seconds between shots
    public float Range, Turn;            // its reach, and how fast the barrel swings (rad/s)
    // WHAT IT FIRES: a row of Shots.All. This struct carried the whole gun EXCEPT the round in it,
    // and Shoot put a main gun's shell down the barrel whatever was bolted on. Shots.Shell is 0,
    // so a gun that says nothing fires a shell.
    public int Kind;
    // WHO IT MAY PICK, when it picks for itself (a mount set up `pd`): Turret.Acquire takes only
    // what this allows, ranked by Turret.Rank, with its Fallback below every rank. A main gun never
    // picks, so a main gun's is never read -- but a mount that picks MUST name one: the default
    // filter forbids nothing and has no fallback, so it would take every hostile and HOLD a
    // practice dummy like anything else.
    public TargetFilter Prey;
    public float ShellSpeed;             // main guns: the shell's speed
    public string Texture;               // the sprite (barrels up, pivot at the sheet's centre)
    public float TexScale;               // world units per turret-texture pixel
    public float Barrel;                 // muzzle from the pivot
    public Color Tint;
    // WHAT ITS FLASH IS, when it is point defence: a row of Beam.All -- the line's colour and the
    // note it is heard at. Beam.Point is 0, so a gun that says nothing is a warship's point
    // defence; a freighter's dropped turret and the hauler's mount name their own.
    public int Beam;
    // WHAT A HULL CALLS A BLOW FROM THIS GUN (DamageSource): the FAMILY name, to which Combat.Fire
    // adds the firing body's own identity. Null -- every gun a pilot's ship carries -- keeps the
    // behaviour these all had: the blow is nameless, and the 0.52 s per-source gap does not hold it.
    public string Source;
    // A ROUND THAT IS MORE THAN A SHELL. All 0 -- every gun a pilot's ship carries -- is a shell's
    // flight: straight, one size, no hull, fired the moment the host says. A round that HOMES turns
    // after the target its host names (Turret.Shoot) at Homing rad/s; Size is its drawn size and
    // its hit radius with it (0: 1, the row's own); Hull is the round's own hull, for a row whose
    // body is a target (Tag.Hulled); Windup is the seconds of warning its host raises before each
    // shot (Turret.Warn).
    public float Homing, Size;
    public double Hull, Windup;
    // A ROUND'S ECHO (the Echo's repeater, kits_v2's card): every main-gun round leaves an echo of RepeatShare of its
    // damage (Shots row "echo") from the muzzle it left and along the bearing it took, RepeatDelay seconds later -- the
    // turret records both at the shot (Turret.Shoot), so a ship that has moved or turned since fires it from where it
    // was. 0: no echo, every gun but the Echo's.
    public double RepeatShare, RepeatDelay;
    // A VOLLEY FANNED ABOUT THE BARREL (the Wraith's scattergun): Pellets rounds at the full damage each, spread evenly from
    // -Fan to +Fan degrees about the barrel's bearing. 0 or 1: one round down the barrel, every other gun.
    public int Pellets;
    public float Fan;
    public readonly float RoundSize => Size > 0 ? Size : 1f;
}

public interface ITurretHost
{
    Node2D AsNode { get; }                      // what the turret is a child of, and turns with
    TurretSpec Spec(bool pd);
    bool PdOnline { get; }                      // point defence may fire now
    Vector2 AimAt { get; }                      // where the main guns point
    float FastSwing { get; }                    // 0, or a turn rate that overrides the spec's
    IReadOnlyList<Turret> Siblings { get; }     // PD turrets that may have claimed a target
    void NoteDealt(double d, IHittable target, string weapon);  // what this gun just did, and to what
    PlayerShip Credit { get; }                  // whose shell it is, for the tally (may be null)
    // WHAT IT TAKES FIRST, whatever its rank, while it is in reach and the gun's Prey may choose it:
    // a sentry's owner's paint (F14). An emplacement names its drawing pilot (a Taunt), which its own gun's choice
    // reads (Emplacement.WarnAndFire): its mount is a main gun and follows AimAt. Null changes nothing.
    IHittable Prefer => null;
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
    // WHAT IT HOLDS IS ASKED OF THE LIVE WORLD, EVERY TICK -- never told to it. Nothing can list
    // every turret to tell it: a gun on the hauler or on a dropped turret is on no pilot's ship. A
    // target leaves the world by leaving Combat.Hostiles (Hub.LetGo, in the call that drops it), and
    // a hunter withdrawn with its hull left (Hub.CallOff) still reads alive -- so the question is
    // "still in that list, AND alive", and the list is asked FIRST: a node that has left the world
    // is asked nothing of its own, and never its position.
    private static bool StillThere(IHittable t) => t != null && Combat.Hostiles.Contains(t) && t.Alive;

    private TurretSpec S => Host.Spec(PointDefense);
    public float Range => S.Range;
    public double Interval => S.Interval;
    // WHERE ITS MUZZLE IS AND WHICH WAY IT POINTS, in the world, right now: what a beam out of this barrel
    // leaves along (the Tender's lance, PlayerShip.MainBore). On every peer: the barrel's swing is.
    public (Vector2 at, Vector2 dir) Bore
    {
        get { var d = Vector2.Right.Rotated(GlobalRotation); return (GlobalPosition + d * S.Barrel, d); }
    }
    // Through a broadside the main turrets swing fast enough to come round from anywhere onto the
    // cursor within the wind-up; the host names that rate, and the faster of the two wins.
    private float RotSpeed
    {
        get { var s = S; return PointDefense ? s.Turn * (Host.Credit?.TrackingOn(Target) ?? 1f) : Mathf.Max(s.Turn, Host.FastSwing); }
    }

    private Sprite2D _sprite;          // the turret's own sprite (TurretSpec.Texture)

    public void Setup(ITurretHost host, Vector2 offset, bool pd)
    {
        Host = host; Offset = offset; PointDefense = pd;
        ZIndex = 5;
        var spec = S;
        // stored barrels-UP; the turret's barrel direction is +X, so turn it a quarter
        _sprite = new Sprite2D { Texture = Assets.Load<Texture2D>(spec.Texture), Rotation = Mathf.Pi / 2f,
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
            Repeat(delta);
            return;
        }

        float rest = Host.AsNode.Rotation - Mathf.Pi / 2f;
        if (!Online) { Target = null; _cd = 0; Swing(rest, delta); return; }

        // Acquire. Runs on guests too, as cosmetics: the same rule on the same
        // positions picks the same targets, so a guest sees its turrets track what
        // the host's are shooting. Only the host's copy deals damage. A FALLBACK (a practice
        // dummy, to a turret left standing) is never HELD: it is picked again every tick, so the
        // first thing that can die to come into reach takes the gun off it. What it does hold, it
        // keeps only until something free betters it (Acquire's `held`).
        // THE PAINT (Host.Prefer) OUTRANKS THE HOLD: a paint in reach that it is not on, or the paint it
        // was following lapsing, is a fresh pick -- otherwise a sentry would stay on a seeker when a
        // paint went up, and on the painted gunship after the paint lapsed.
        Vector2 wp = GlobalPosition;
        var prefer = Preferred(wp);
        bool repick = (prefer != null && !ReferenceEquals(prefer, Target)) || (_onPrefer && !ReferenceEquals(prefer, Target));
        Target = repick || !StillThere(Target) || S.Prey.IsFallback(Target) || wp.DistanceTo(Target.Position) > Range * 1.15f
            ? Acquire(wp, null) : Acquire(wp, Target);
        _onPrefer = prefer != null && ReferenceEquals(prefer, Target);

        float desired = Target != null ? (Target.Position - wp).Angle() : rest;
        float diff = Swing(desired, delta);
        if (!Net.Sim) return;

        // An 8-degree cone, so a turret still swinging does not fire. The reload
        // CARRIES its remainder rather than resetting: resetting rounds every shot up
        // to a whole frame, which quietly cost 3% of PD's stated DPS at 60 fps.
        bool canFire = Target != null && Mathf.Abs(diff) <= Mathf.DegToRad(8f);
        _cd -= delta;
        if (!canFire) { if (_cd < 0) _cd = 0; }
        else for (int n = 0; _cd <= 0 && n < 8; n++)
        {   // the target held for the shot: a shot that kills it ends the run here, and the next
            // tick picks again
            var tgt = Target;
            if (!StillThere(tgt)) { Target = null; break; }
            var spec = S;
            _cd += spec.Interval;
            Dealt.Deal(tgt, spec.Damage, Host, Host is PlayerShip ? Dealt.Pd : Dealt.Turret);
            Combat.Flash(wp + Vector2.Right.Rotated(GlobalRotation) * spec.Barrel, tgt.Position, spec.Beam);
        }
    }

    // WHAT A GUN THAT PICKS FOR ITSELF TAKES FIRST: what is in flight -- a missile, or a body with
    // a hull of its own -- then small craft (light raiders, fighters, the practice fighters), then
    // everything else -- a heavy, a boss, a station. WHAT it may take at all is its own
    // (TurretSpec.Prey): point defence never gets past the small craft (Targeting.PointDefence), a
    // turret left standing takes them all (Targeting.Sentry). Below every rank, what its filter
    // takes only as a FALLBACK. Within a rank, the nearest one no sibling turret has claimed (if
    // all are claimed, the nearest regardless).
    // HELD, it is not re-picked by distance (no flicking between two in reach), but it gives way
    // to something FREE that betters it: a lower rank (a missile over the light it is on), or the
    // same rank while a sibling shares what it holds (the spare that doubled up spreads to the
    // first new one in reach) -- never down the ranks, and never to a fallback. Point defence is
    // passive, so no window starts a fresh pick: without this a doubled-up mount would stay
    // doubled up, and one on a light would let a missile through.
    public static int Rank(IHittable h) => TagExt.Is(h, Tag.Missile | Tag.Hulled) ? 0 : TagExt.Is(h, Tag.Light | Tag.Fighter) ? 1 : 2;
    private const int FallbackRank = 3;
    // The whole order as one number, for anything that picks by it (a turret, the carrier's patrol):
    // Rank, or below every rank when `prey` takes it only as a fallback.
    public static int RankIn(TargetFilter prey, IHittable h) => prey.IsFallback(h) ? FallbackRank : Rank(h);
    private bool Claimed(IHittable h)
    {
        foreach (var t in Host.Siblings) if (t != this && t.Target == h) return true;
        return false;
    }
    // Best by (rank, then distance), preferring one no sibling turret has claimed, falling
    // back to the best claimed one -- in a single pass with no allocation. Tick calls it EVERY
    // FRAME FOR EVERY TURRET THAT PICKS FOR ITSELF: to pick, and, holding (`held`), to see whether
    // something free betters what it holds.
    private bool _onPrefer;                  // what it holds is its host's Prefer
    // the host's Prefer, if it is still in the world, in reach and a thing this gun may choose
    private IHittable Preferred(Vector2 from)
    {
        var p = Host.Prefer;
        return StillThere(p) && S.Prey.Chooses(p) && from.DistanceTo(p.Position) <= Range ? p : null;
    }
    private IHittable Acquire(Vector2 from, IHittable held)
    {
        if (Preferred(from) is { } pref) return pref;          // above every rank, claimed or not
        IHittable bestFree = null, bestAny = null;
        int freePri = 0, anyPri = 0; float freeDist = 0, anyDist = 0;
        var spec = S;
        float range = spec.Range;
        foreach (var h in Combat.Hostiles)
        {
            if (!spec.Prey.Chooses(h)) continue;
            float d = from.DistanceTo(h.Position);
            if (d > range) continue;
            int p = RankIn(spec.Prey, h);
            if (bestAny == null || p < anyPri || (p == anyPri && d < anyDist)) { bestAny = h; anyPri = p; anyDist = d; }
            if (Claimed(h)) continue;
            if (bestFree == null || p < freePri || (p == freePri && d < freeDist)) { bestFree = h; freePri = p; freeDist = d; }
        }
        if (held == null) return bestFree ?? bestAny;
        if (bestFree == null || ReferenceEquals(bestFree, held)) return held;
        int heldPri = Rank(held);
        return freePri < heldPri || (freePri == heldPri && Claimed(held)) ? bestFree : held;
    }

    // One main-gun shot, along the barrel as it points RIGHT NOW: whatever the gun's row FIRES
    // (TurretSpec.Kind), at the guns' own shell speed, as far as their range, at `mult` times its
    // damage (a broadside's multiple) -- straight, or, for a round that homes (TurretSpec.Homing),
    // turning after `target`; at the round's own size and with its own hull. It goes through
    // Combat.Fire, the one door everything that flies comes through, so a gun that fires something
    // else is a field on the spec rather than a branch here. Host only. (Point defence never comes
    // here: it fires from Tick, at what it has acquired.)
    public void Shoot(double mult = 1.0, int target = 0)
    {
        if (!Net.Sim) return;
        var spec = S;
        var bore = Vector2.Right.Rotated(GlobalRotation);
        var muzzle = GlobalPosition + bore * spec.Barrel;
        int n = Mathf.Max(1, spec.Pellets);
        for (int k = 0; k < n; k++)
        {
            var dir = n == 1 ? bore : bore.Rotated(Mathf.DegToRad(spec.Fan * (2f * k / (n - 1) - 1f)));
            Combat.Fire(spec.Kind, muzzle, dir, spec.ShellSpeed, spec.Range, spec.Damage * mult,
                        targetId: spec.Homing > 0 ? target : 0, turnRate: spec.Homing, source: Host.Credit, hitSource: spec.Source,
                        size: spec.RoundSize, hull: spec.Hull);
            if (spec.RepeatShare > 0)
                _echoes.Add(new Echoed(muzzle, dir, spec.ShellSpeed, spec.Range, spec.Damage * mult * spec.RepeatShare, _clock + spec.RepeatDelay));
        }
    }

    // THE ECHOES STILL TO GO (TurretSpec.RepeatShare), each as its round left: muzzle, bearing, speed, reach and its
    // share of the damage, and when it is due on this turret's own clock. Host only (Shoot is); the turret's own list,
    // so they go with it.
    private readonly record struct Echoed(Vector2 At, Vector2 Dir, float Speed, float Range, double Damage, double Due);
    private readonly List<Echoed> _echoes = new();
    private double _clock;
    private void Repeat(double delta)
    {
        _clock += delta;
        if (!Net.Sim) { _echoes.Clear(); return; }
        for (int i = 0; i < _echoes.Count; i++)
        {
            var e = _echoes[i];
            if (e.Due > _clock) continue;
            Combat.Fire(Shots.Echo, e.At, e.Dir, e.Speed, e.Range, e.Damage, source: Host.Credit, hitSource: S.Source);
            _echoes.RemoveAt(i--);
        }
    }

    // THE WARNING BEFORE A SHOT (TurretSpec.Windup): a red lane from where the round will leave this
    // barrel -- the muzzle once it has come round onto the target, not where it points while it is
    // still swinging -- to what the host is about to fire at, as wide as the round, for the
    // wind-up, on every peer (Fx.Warn). The host raises it when its clock comes due and fires
    // Windup later (Emplacement.WarnAndFire); a gun with no wind-up raises nothing. It RIDES the
    // hull whose id the host names (`rides`, an FxRaise.Anchor, in that hull's own frame), so a
    // launcher brought down takes its warning with it on every peer, as a boss's do; Fx.World
    // draws it on the ground. It runs to where the target stands: the round is guided onto it, so
    // that is the line that matters.
    public void Warn(IHittable at, int rides = Fx.World)
    {
        var spec = S;
        if (spec.Windup <= 0 || at == null) return;
        var host = Host.AsNode;
        var muzzle = GlobalPosition + (at.Position - GlobalPosition).Normalized() * spec.Barrel;
        Fx.Warn(new FxRaise { Id = Fx.WarnLane, Anchor = rides,
                              At = rides == Fx.World ? muzzle : host.ToLocal(muzzle),
                              To = rides == Fx.World ? at.Position : host.ToLocal(at.Position),
                              Size = 2f * Shot.BodyRadius * spec.RoundSize, Time = spec.Windup });
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

    // The turret takes the host's accent (turrets, engines, trim) over its hull colour. It draws
    // nothing of its own: its sprite is the whole of it (point defence has no window to show).
    public void Recolor()
    {
        if (_sprite != null) _sprite.Modulate = S.Tint;
    }
}
