using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// SHIP CLASSES — the first two of an intended nine.
//
//   BATTLESHIP : main guns aimed with the cursor and fired with Space, plus PD
//   CARRIER    : fighters and bombers sent at the selected target with Space, plus PD
//
// Ported from Space Fleet Idle's capital ship, cut down to the initial layer.
// Deliberately NOT carried over yet: siege mode, cloak, phase, ambush volleys,
// burn-rate modifiers, XP doctrines, module points. Those were built on top of
// this layer and can come back once the nine classes exist.
//
// AUTHORITY: turrets and wings are COMBAT, so the host simulates them. A client
// renders what it is told. The owner decides only its heading, where it aims, and
// whether it is pulling the trigger -- never what the shot hits.
// ─────────────────────────────────────────────────────────────────────────────
public enum ShipClass { Battleship, Carrier }

public interface IHittable
{
    // Same on every peer, so a guest can name a target to the host ("attack 1000").
    int NetId { get; }
    Vector2 Position { get; }
    float HitRadius { get; }
    bool Alive { get; }
    void TakeDamage(double d);
}

// ── Turret ───────────────────────────────────────────────────────────────────
// Sits on the hull, rotates independently. Rotation is the whole feel of it: a
// turret that snaps instantly reads as a hitscan, one that has to swing reads as
// a warship.
//
//   MAIN GUNS aim at the owner's cursor and fire only when the owner holds the
//             guns key; the SHIP decides when (salvo or staggered), the turret just
//             shoots along wherever its barrel points at that moment.
//   POINT DEFENCE is idle until the ship activates it. While active, every turret
//             picks and tracks its OWN target, preferring one no sibling turret has
//             claimed, so a group gets spread across rather than piled onto.
//
// Every number comes from Ship.Stats, read each tick, so a bonus applies at once.
public partial class Turret : Node2D
{
    public PlayerShip Ship;
    public Vector2 Offset;
    public bool PointDefense;

    private float _angle;
    private double _cd;
    public IHittable Target { get; private set; }

    public bool Online => !PointDefense || Ship.PdActive;

    private ShipStats S => Ship.Stats;
    public float RotSpeed => (float)(PointDefense ? S["pd_turn"] : S["main_turn"]);
    public float Range    => (float)(PointDefense ? S["pd_range"] : S["main_range"]);
    public double ShotDamage => PointDefense ? S["pd_damage"] : S["main_damage"];
    public double Interval   => PointDefense ? S["pd_interval"] : S["main_interval"];

    private Sprite2D _sprite;          // the painted turret, cut from the hull art
    private PlayerShip.ClassArt Art => Ship.MyArt;
    private float BarrelLength => PointDefense ? Art.PdBarrel : Art.MainBarrel;

    public void Setup(PlayerShip ship, Vector2 offset, bool pd)
    {
        Ship = ship; Offset = offset; PointDefense = pd;
        ZIndex = 5;
        var texPath = pd ? Art.PdTurret : Art.MainTurret;
        {
            // stored barrels-UP; the turret's barrel direction is +X, so turn it a quarter
            _sprite = new Sprite2D { Texture = GD.Load<Texture2D>(texPath), Rotation = Mathf.Pi / 2f,
                                     Scale = Vector2.One * Art.TurretTexScale };
            AddChild(_sprite);
            Recolor();
        }
        // A child of the ship already inherits its rotation, so the mount offset is
        // set once, in the ship's frame. Rotating it by Ship.Rotation again walked
        // the mounts off the hull as the ship turned.
        Position = Offset;
        _angle = Ship.Rotation - Mathf.Pi / 2f;
        GlobalRotation = _angle;
    }

    public void Tick(double delta)
    {
        if (!PointDefense)
        {
            // Main guns follow the cursor on every peer: the owner's own mouse, or the
            // aim point it last sent. On guests this is cosmetic; the host's copy is
            // the one whose barrel direction decides where shots go.
            Swing((Ship.AimPoint - GlobalPosition).Angle(), delta);
            QueueRedraw();
            return;
        }

        float rest = Ship.Rotation - Mathf.Pi / 2f;
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
        {
            _cd += Interval;
            Target.TakeDamage(ShotDamage);
            Combat.Flash(wp, Target.Position, new Color(0.7f, 0.95f, 1f));
        }
        QueueRedraw();
    }

    // Nearest hostile in range that no other PD turret on this ship has claimed;
    // if every one in range is claimed, the nearest regardless.
    private IHittable Acquire(Vector2 from)
    {
        var inRange = Combat.Near(from, Range);
        foreach (var h in inRange)
        {
            bool claimed = false;
            foreach (var t in Ship.PdTurrets) if (t != this && t.Target == h) { claimed = true; break; }
            if (!claimed) return h;
        }
        return inRange.Count > 0 ? inRange[0] : null;
    }

    // One main-gun shot, along the barrel as it points RIGHT NOW. Host only.
    public void Shoot()
    {
        if (!Net.Sim) return;
        var dir = Vector2.Right.Rotated(GlobalRotation);
        var from = GlobalPosition + dir * BarrelLength;
        var hit = Combat.RayHit(from, dir, Range, out var end);
        hit?.TakeDamage(ShotDamage);
        Combat.Flash(from, end, new Color(1f, 0.85f, 0.5f));
    }

    // _angle is a WORLD angle, so it is applied as GlobalRotation; as a local
    // rotation it would add the ship's heading on top. Returns how far off the
    // desired angle the barrel was before this step.
    private float Swing(float desired, double delta)
    {
        float diff = Mathf.Wrap(desired - _angle, -Mathf.Pi, Mathf.Pi);
        float step = RotSpeed * (float)delta;
        _angle = Mathf.Abs(diff) <= step ? desired : _angle + Mathf.Sign(diff) * step;
        GlobalRotation = _angle;
        return diff;
    }

    // The painted turret takes the hull colour, as it had when it was part of the hull.
    public void Recolor()
    {
        if (_sprite != null) _sprite.Modulate = Ship.Main;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var lit = Online ? Ship.Accent : new Color(0.35f, 0.35f, 0.40f);
        // PD shows the ship's cycle, just outside its ring, in the accent colour:
        // the firing window running down, or the recharge
        if (!PointDefense) return;
        float frac = Ship.PdActive ? Ship.PdActiveFrac : Ship.PdRechargeFrac;
        if (frac <= 0) return;
        float r = Art.PdRing + 1.6f;
        DrawArc(Vector2.Zero, r, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * frac, 16,
                Ship.PdActive ? new Color(lit.R, lit.G, lit.B, 0.9f) : new Color(1f, 0.5f, 0.3f, 0.6f), 1.2f);
    }
}

// ── Wing craft ───────────────────────────────────────────────────────────────
// Fighters and bombers fly with real acceleration, so the acceleration stat on the sheet is a real number.
//
//   FIGHTERS hold an orbit until ordered to attack the selected target; they fight
//            it while it stays within the carrier's control range. Recall returns them.
//   BOMBERS  are an active ability. They wait DOCKED on the carrier's flanks, half to
//            port and half to starboard, rearming there. On the order they fly at the
//            target, turn to face it at launch distance, and launch torpedoes straight
//            ahead -- the torpedoes do not track -- then return to their own slot.
//            A target beyond the strike range (twice the fighters' control range) is
//            refused, and a strike whose target goes beyond it is called off.
public enum WingKind { Fighter, Bomber }

public partial class Wing : Node2D
{
    public PlayerShip Carrier;
    public WingKind Kind;
    public double Hp, MaxHp;
    public bool Alive = true;
    public int Ammo;
    public Vector2 Velocity;

    public const float FighterLength = 17f, BomberLength = 37.5f;   // halved / three-quarters in this release
    private Sprite2D _sprite;
    private double _cd, _orbitA;
    // Fighters fly in bursts: after 15 s of firing a fighter goes back to the centre of
    // the carrier and rests 3 s there before rejoining.
    private double _fired, _rest;
    private bool _goingHome;
    public bool Resting => _goingHome || _rest > 0;
    private enum BSt { Docked, Approach, Aim, Launch, Return }
    public const float CrawlSpeed = 0.25f;     // bombers keep closing at this fraction of top speed while they launch
    public bool Launching => _b == BSt.Launch;
    private BSt _b = BSt.Docked;
    public bool Docked => _b == BSt.Docked;
    public bool Armed => _b == BSt.Docked && _rearm <= 0 && Ammo > 0;
    public double RearmLeft => _b == BSt.Docked ? Math.Max(0, _rearm) : 0;
    private double _rearm;

    // Where the host says this craft is. Guests steer toward it.
    private Vector2 _netPos; private float _netRot; private bool _hasNet;

    private ShipStats S => Carrier.Stats;
    private bool F => Kind == WingKind.Fighter;
    private float Speed => (float)(F ? S["fighter_speed"] : S["bomber_speed"]);
    private float Accel => (float)(F ? S["fighter_accel"] : S["bomber_accel"]);
    private float Range => (float)(F ? S["fighter_range"] : S["launch_range"]);

    public void Init(PlayerShip carrier, WingKind kind, Vector2 at)
    {
        Carrier = carrier; Kind = kind; Position = at;
        MaxHp = Hp = F ? S["fighter_hp"] : S["bomber_hp"];
        Ammo = (int)S["bomber_ammo"];
        // Fighter: the carrier's own hull, small (34 u long). Bomber: its own airframe,
        // larger (50 u) so the two read apart at a glance.
        var tex = GD.Load<Texture2D>(F ? "res://wing_fighter.png" : "res://wing_bomber.png");
        float len = F ? FighterLength : BomberLength;
        _sprite = new Sprite2D { Texture = tex, Scale = Vector2.One * (len / tex.GetHeight()) };
        AddChild(_sprite);
        ZIndex = 4;
        _orbitA = GD.Randf() * Mathf.Tau;
    }

    public void SetNet(Vector2 p, float rot) { _netPos = p; _netRot = rot; _hasNet = true; }

    public void Tick(double delta)
    {
        if (!Alive) return;
        if (!IsInstanceValid(Carrier) || !Carrier.Alive) { Alive = false; QueueFree(); return; }
        Visible = Carrier.IsVisibleInTree();          // a hidden carrier hides its wing

        if (!Net.Sim)
        {   // the host flies the wing; a guest follows where it says it is -- except a
            // bomber the host reports docked, which is locked to its slot here exactly,
            // rather than trailing a moving carrier by a packet's worth of lag
            if (!F && _b == BSt.Docked) { SnapToDock(); return; }
            if (_hasNet)
            {
                Position = Position.Lerp(_netPos, Mathf.Clamp(10f * (float)delta, 0f, 1f));
                Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(10f * (float)delta, 0f, 1f));
            }
            else if (F) Orbit(delta, 160f);
            else SnapToDock();
            return;
        }

        _cd -= delta;
        if (F) TickFighter(delta); else TickBomber(delta);
    }

    private void TickFighter(double delta)
    {
        if (_goingHome)
        {
            FlyToward(Carrier.Position, Speed, delta, Carrier.Velocity);
            if (Position.DistanceTo(Carrier.Position) < 8f) { _goingHome = false; _rest = S["fighter_rest"]; }
            return;
        }
        if (_rest > 0)
        {   // resting at the carrier's centre, riding with it
            Position = Carrier.Position; Velocity = Carrier.Velocity; Rotation = Carrier.Rotation;
            _rest -= delta;
            if (_rest <= 0) { _rest = 0; _fired = 0; }
            return;
        }
        var t = Carrier.WingTarget;
        bool inControl = t != null && t.Alive && Carrier.Position.DistanceTo(t.Position) <= S["control_range"];
        if (!inControl) { if (_cd < 0) _cd = 0; Orbit(delta, 160f); return; }

        float d = Position.DistanceTo(t.Position);
        if (d > Range * 0.8f) FlyToward(t.Position, Speed, delta);
        else { FlyToward(Position, Speed, delta); FaceToward(t.Position, delta); }   // brake and hold

        if (d > Range) { if (_cd < 0) _cd = 0; return; }
        _fired += delta;
        if (_fired >= S["fighter_burst"]) { _goingHome = true; _cd = 0; return; }
        for (int n = 0; _cd <= 0 && n < 8; n++)
        {   // carried remainder, as with the turrets, so measured DPS matches the sheet
            _cd += S["fighter_interval"];
            t.TakeDamage(S["fighter_damage"]);
            Combat.Flash(Position, t.Position, new Color(0.7f, 0.95f, 1f));
        }
    }

    private void TickBomber(double delta)
    {
        var t = Carrier.StrikeTarget;
        bool live = t != null && t.Alive && Carrier.Position.DistanceTo(t.Position) <= S["strike_range"];
        switch (_b)
        {
            case BSt.Docked:
                SnapToDock();
                if (_rearm > 0) { _rearm -= delta; if (_rearm <= 0) Ammo = (int)S["bomber_ammo"]; }
                if (live && Armed) { _b = BSt.Approach; Velocity = Carrier.Velocity; }
                break;

            case BSt.Approach:
                if (!live) { _b = BSt.Return; Carrier.NoteStrikeDone(); break; }
                FlyToward(t.Position, Speed, delta);
                if (Position.DistanceTo(t.Position) <= Range) _b = BSt.Aim;
                break;

            case BSt.Aim:
            {   // creep in at a quarter speed while the nose swings onto the target; the
                // torpedoes go where it points
                if (!live) { _b = BSt.Return; Carrier.NoteStrikeDone(); break; }
                FlyToward(t.Position, Speed * CrawlSpeed, delta);
                FaceToward(t.Position, delta);
                float off = Mathf.Abs(Mathf.AngleDifference(Rotation - Mathf.Pi / 2f, (t.Position - Position).Angle()));
                if (off < Mathf.DegToRad(4f)) { _b = BSt.Launch; _cd = 0; }
                break;
            }

            case BSt.Launch:
                if (live) { FlyToward(t.Position, Speed * CrawlSpeed, delta); FaceToward(t.Position, delta); }   // still closing, slowly
                else { Velocity = Velocity.MoveToward(Vector2.Zero, Accel * (float)delta); Position += Velocity * (float)delta; }
                if (_cd <= 0 && Ammo > 0)
                {
                    _cd += S["torpedo_interval"]; Ammo--;
                    var dir = Vector2.Up.Rotated(Rotation);           // straight off the nose
                    Combat.LaunchTorpedo(Position + dir * BomberLength * 0.45f, dir, (float)S["torpedo_speed"],
                                         (float)S["torpedo_range"], S["torpedo_damage"]);
                }
                if (Ammo <= 0) { _b = BSt.Return; Carrier.NoteStrikeDone(); }
                break;

            case BSt.Return:
            {   // home to this bomber's own slot, match the carrier's way, and lock on
                var (slot, _) = Carrier.DockSlot(this);
                FlyToward(slot, Speed, delta, Carrier.Velocity);
                if (Position.DistanceTo(slot) < 6f) { _b = BSt.Docked; _rearm = S["bomber_rearm"]; SnapToDock(); }
                break;
            }
        }
    }

    // Docked: fixed to the carrier's flank, heading with it.
    public void SnapToDock()
    {
        var (p, rot) = Carrier.DockSlot(this);
        Position = p; Rotation = rot; Velocity = Carrier.Velocity;
    }

    // Replicated so a guest's ability bar knows what its bombers are doing.
    public int StateCode => (int)_b;
    public void SetNetState(int code, double rearm) { if (!Net.Sim) { _b = (BSt)code; _rearm = rearm; } }
    public bool IsBomber => Kind == WingKind.Bomber;

    private void Orbit(double delta, float r)
    {
        _orbitA += delta * 0.9;
        var want = Carrier.Position + new Vector2(Mathf.Cos((float)_orbitA), Mathf.Sin((float)_orbitA)) * r;
        FlyToward(want, Speed * 0.55f, delta);
    }

    // Accelerate toward a point, easing off to arrive rather than overshoot.
    // `carry` is the velocity of whatever the craft is homing on (the carrier, when
    // docking), so it arrives moving WITH it rather than stopping dead beside it.
    private void FlyToward(Vector2 to, float spd, double delta, Vector2 carry = default)
    {
        var d = to - Position;
        float dist = d.Length();
        // the fastest speed from which this craft can still stop in `dist`
        float arrive = Mathf.Sqrt(2f * Accel * dist);
        var want = carry + (dist > 1f ? d / dist * Mathf.Min(spd, arrive) : Vector2.Zero);
        Velocity = Velocity.MoveToward(want, Accel * (float)delta);
        Position += Velocity * (float)delta;
        if (Velocity.LengthSquared() > 100f) FaceToward(Position + Velocity, delta);
    }

    private void FaceToward(Vector2 to, double delta)
    {
        float want = (to - Position).Angle() + Mathf.Pi / 2f;
        Rotation = Mathf.LerpAngle(Rotation, want, Mathf.Clamp(9f * (float)delta, 0f, 1f));
    }

    public void TakeDamage(double d)
    {
        if (!Net.Sim) return;
        Hp -= d;
        if (Hp <= 0) { Alive = false; QueueFree(); }
    }
}
