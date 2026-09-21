using System.Linq;
using Godot;
using System;

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

// What fired a laser, which decides which report is played. Not a volume: the level lives in
// the file (see tools/gain.ps1), so a new kind of shot is a new sound rather than a new offset.
public enum ShotSound { Light, Fighter, Boss }

public interface IHittable
{
    // Same on every peer, so a guest can name a target to the host ("attack 1000").
    int NetId { get; }
    Vector2 Position { get; }
    float HitRadius { get; }
    bool Alive { get; }
    void TakeDamage(double d);
    // Does a projectile at p (with pad for its own size) touch this? A circle by
    // default; a long ship answers with a capsule along its keel.
    bool Covers(Vector2 p, float pad) => p.DistanceTo(Position) <= HitRadius + pad;
    // Missiles can be shot down, but they are never SELECTED (click or Tab).
    bool Selectable => true;
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

    private bool Online => !PointDefense || Ship.PdActive;

    private ShipStats S => Ship.Stats;
    private float RotSpeed => (float)(PointDefense ? S["pd_turn"] : S["main_turn"]);
    public float Range    => (float)(PointDefense ? S["pd_range"] : S["main_range"]);
    private double ShotDamage => PointDefense ? S["pd_damage"] : S["main_damage"];
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
            Target.TakeDamage(ShotDamage); Ship.NoteCombat();
            Combat.Flash(wp + Vector2.Right.Rotated(GlobalRotation) * BarrelLength, Target.Position, new Color(0.7f, 0.95f, 1f));
        }
        QueueRedraw();
    }

    // Point defence shoots ONLY missiles and light fighters (light raiders, and the practice
    // fighters) -- never heavies, bosses or the dummies. Missiles first; within that, the
    // nearest one no sibling turret has claimed (if all are claimed, the nearest regardless).
    private static bool PdTargets(IHittable h) => h is Torpedo || (h is Raider r && !r.Heavy) || (h is TargetDummy d && d.Fighter);
    public static int PdPriority(IHittable h) => h is Torpedo ? 0 : h.HitRadius < 20f ? 1 : 2;
    // Same rule as before -- best by (priority, then distance), preferring one no sibling turret
    // has claimed, falling back to the best claimed one -- but in a single pass with no
    // allocation. This used to be Combat.Near (which allocates and sorts) plus a LINQ chain and
    // another ToList, and line 111 calls it EVERY FRAME FOR EVERY PD TURRET while point defence
    // is active with nothing in range, because a null Target never satisfies the guard.
    private IHittable Acquire(Vector2 from)
    {
        IHittable bestFree = null, bestAny = null;
        int freePri = 0, anyPri = 0; float freeDist = 0, anyDist = 0;
        foreach (var h in Combat.Hostiles)
        {
            if (h == null || !h.Alive || !PdTargets(h)) continue;
            float d = from.DistanceTo(h.Position);
            if (d > Range) continue;
            int p = PdPriority(h);
            if (bestAny == null || p < anyPri || (p == anyPri && d < anyDist)) { bestAny = h; anyPri = p; anyDist = d; }
            bool claimed = false;
            foreach (var t in Ship.PdTurrets) if (t != this && t.Target == h) { claimed = true; break; }
            if (claimed) continue;
            if (bestFree == null || p < freePri || (p == freePri && d < freeDist)) { bestFree = h; freePri = p; freeDist = d; }
        }
        return bestFree ?? bestAny;
    }

    // One main-gun shot, along the barrel as it points RIGHT NOW. Host only.
    public void Shoot()
    {
        if (!Net.Sim) return;
        var dir = Vector2.Right.Rotated(GlobalRotation);
        var from = GlobalPosition + dir * BarrelLength;
        if (!PointDefense)
        {   // the main guns fire SHELLS: straight, 4x the missile's speed, as far as their range
            Combat.FireShell(from, dir, (float)S["missile_speed"] * Shell.SpeedMult, Range, ShotDamage, Ship);
            return;
        }
        var hit = Combat.RayHit(from, dir, Range, out var end);
        hit?.TakeDamage(ShotDamage);
        if (hit != null) Ship.NoteCombat();
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
        // PD shows the ship's cycle, just outside its ring, in the accent colour:
        // the firing window running down, or the recharge. Main guns draw nothing here,
        // so leave before doing any of the work -- this runs per turret, per frame.
        if (!PointDefense) return;
        var lit = Online ? Ship.Accent : new Color(0.35f, 0.35f, 0.40f);
        float frac = Ship.PdActive ? Ship.PdActiveFrac : Ship.PdRechargeFrac;
        if (frac <= 0) return;
        float r = Art.PdRing + 1.6f;
        DrawArc(Vector2.Zero, r, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * frac, 16,
                Ship.PdActive ? new Color(lit.R, lit.G, lit.B, 0.9f) : new Color(1f, 0.5f, 0.3f, 0.6f), 1.2f);
    }
}

// ── Wing craft ───────────────────────────────────────────────────────────────
//   FIGHTERS wait docked INSIDE the carrier. Ordered to attack the selected target
//            (while it is within control range), they launch and fly strafing runs
//            like aircraft -- steady speed, limited turn rate: 3 shots, through the
//            target by 1.2x its diameter, turn, repeat. After 15 s engaged they dock
//            and rest 3 s. Recall brings them home.
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

    // The bomber is the LARGER airframe (28.125 to the fighter's 17), so the two read apart at a
    // glance. The old note here said "25% smaller", which had not been true since they were
    // resized and contradicted Init's own comment a hundred lines below.
    public const float FighterLength = 17f, BomberLength = 28.125f;
    private Sprite2D _sprite;
    private double _cd;

    // ── fighters: strafing runs ──────────────────────────────────────────────
    // Each pass: 3 shots, straight THROUGH the target until 1.2x its diameter past
    // it, then turn for the next. They fly like aircraft -- steady speed, limited
    // turn rate -- so the pass and the turn are real. After 15 s engaged they fly
    // home and dock INSIDE the carrier for a 3 s rest; with nothing to fight they
    // stay inside.
    public const int BurstShots = 3;
    // HARD LIMIT: fighters leave the hangar at least this far apart. Deliberately a constant,
    // not a stat: no upgrade or rate-of-fire bonus may ever change it.
    public const double LaunchInterval = 0.83;
    public const float Overshoot = 1.2f;
    private enum FSt { Docked, Launch, Approach, Burst, Overshoot, Turn, Home }
    private FSt _f = FSt.Docked;
    private float _heading;                     // fighter's nose, world angle
    private int _shots;                         // shots fired this pass
    private double _engaged, _rest, _launchT;
    public bool Resting => F && (_f == FSt.Home || (_f == FSt.Docked && _rest > 0));
    public bool Inside => F && _f == FSt.Docked;
    public string FighterPhase => _f.ToString();
    public int ShotsThisPass => _shots;
    public float LastOvershoot { get; private set; }   // how far past the target the last pass went
    public double LaunchedAt { get; private set; } = -1; // the carrier's clock when it last left the hangar
    private enum BSt { Docked, Approach, Aim, Launch, Return, Backing }
    private const float DockSnap = 6f;              // within this of its slot, a backing bomber is home
    private const double BackingLimit = 2.5;        // and after this long backing in, it is home regardless
    private Vector2 _lastSlot; private bool _haveSlot;
    // how its slot is moving now: the carrier's motion AND its turning, measured frame to frame
    private Vector2 SlotVelocity(Vector2 slot, double delta)
    {
        var v = _haveSlot && delta > 0 ? (slot - _lastSlot) / (float)delta : Carrier.Velocity;
        _lastSlot = slot; _haveSlot = true;
        return v;
    }
    private const float CrawlSpeed = 0.25f;     // bombers keep closing at this fraction of top speed while they launch
    public bool Launching => _b == BSt.Launch;
    private BSt _b = BSt.Docked;
    public bool Docked => _b == BSt.Docked;
    public bool Armed => _b == BSt.Docked && _rearm <= 0 && Ammo > 0;
    public double RearmLeft => _b == BSt.Docked ? Math.Max(0, _rearm) : 0;
    private double _rearm, _backT;

    // Where the host says this craft is. Guests steer toward it.
    private Vector2 _netPos; private float _netRot; private bool _hasNet;

    private ShipStats S => Carrier.Stats;
    private bool F => Kind == WingKind.Fighter;
    private float Speed => (float)(F ? S["fighter_speed"] : S["bomber_speed"]);
    private float Accel => (float)S["bomber_accel"];            // bombers only: fighters fly at a steady speed
    private float Range => (float)(F ? S["fighter_range"] : S["launch_range"]);

    public void Init(PlayerShip carrier, WingKind kind, Vector2 at)
    {
        Carrier = carrier; Kind = kind; Position = at;
        MaxHp = Hp = F ? S["fighter_hp"] : S["bomber_hp"];
        Ammo = (int)S["bomber_ammo"];
        // Fighter and bomber have their own airframes, and the bomber is the larger of
        // the two (FighterLength, BomberLength) so they read apart at a glance.
        var tex = GD.Load<Texture2D>(F ? "res://wing_fighter.png" : "res://wing_bomber.png");
        float len = F ? FighterLength : BomberLength;
        _sprite = new Sprite2D { Texture = tex, Scale = Vector2.One * (len / tex.GetHeight()) };
        AddChild(_sprite);
        ZIndex = 4;
    }

    public void SetNet(Vector2 p, float rot) { _netPos = p; _netRot = rot; _hasNet = true; }

    public void Tick(double delta)
    {
        if (!Alive) return;
        if (!IsInstanceValid(Carrier)) { Alive = false; QueueFree(); return; }   // a carrier in stasis keeps its wing
        Visible = Carrier.IsVisibleInTree() && !Inside;   // docked fighters are inside the hull
        QueueRedraw();                                     // the engine plume

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
            else if (!F) SnapToDock();
            return;
        }

        _cd -= delta;
        if (F) TickFighter(delta); else TickBomber(delta);
    }

    private void TickFighter(double delta)
    {
        float dt = (float)delta;
        var t = Carrier.WingTarget;
        bool engage = t != null && t.Alive && Carrier.Alive && Carrier.Position.DistanceTo(t.Position) <= S["control_range"];
        if (_f is FSt.Approach or FSt.Burst or FSt.Overshoot or FSt.Turn)
        {
            _engaged += delta;
            if (!engage || _engaged >= S["fighter_burst"]) _f = FSt.Home;
        }
        switch (_f)
        {
            case FSt.Docked:
                Position = Carrier.Position; Velocity = Carrier.Velocity; _heading = Carrier.Rotation - Mathf.Pi / 2f;
                if (_rest > 0) { _rest -= delta; break; }
                if (engage && Carrier.TakeLaunchSlot()) { _f = FSt.Launch; _launchT = 0; _engaged = 0; LaunchedAt = Carrier.Clock; Carrier.Signal(Carrier.Position); }
                break;
            case FSt.Launch:                       // out along the carrier's heading, clear of the hull
                _launchT += delta; Fly(dt);
                if (_launchT > 0.5) _f = FSt.Approach;
                break;
            case FSt.Approach:
                SteerTo(t.Position, dt); Fly(dt);
                if (Position.DistanceTo(t.Position) <= Range && OffAngle(t.Position) < Mathf.DegToRad(12f))
                { _f = FSt.Burst; _shots = 0; _cd = 0; }
                break;
            case FSt.Burst:
                SteerTo(t.Position, dt); Fly(dt);
                _cd -= delta;
                if (_cd <= 0 && _shots < BurstShots)
                {
                    _cd += S["fighter_interval"]; _shots++;
                    t.TakeDamage(S["fighter_damage"]); Carrier.NoteCombat();
                    Combat.Flash(Position, t.Position, new Color(0.7f, 0.95f, 1f), ShotSound.Fighter);
                }
                if (_shots >= BurstShots) _f = FSt.Overshoot;
                break;
            case FSt.Overshoot:                    // straight on, through and past it
            {
                Fly(dt);
                float beyond = (Position - t.Position).Dot(Vector2.Right.Rotated(_heading));
                if (beyond >= Overshoot * 2f * t.HitRadius) { LastOvershoot = beyond; _f = FSt.Turn; }
                break;
            }
            case FSt.Turn:                         // come round for the next pass
                SteerTo(t.Position, dt); Fly(dt);
                if (OffAngle(t.Position) < Mathf.DegToRad(20f)) _f = FSt.Approach;
                break;
            case FSt.Home:
                SteerTo(Carrier.Position, dt); Fly(dt, 0.7f);
                if (Position.DistanceTo(Carrier.Position) < 60f)
                {   // in through the hangar
                    _f = FSt.Docked; _rest = _engaged > 0 ? S["fighter_rest"] : 0; _engaged = 0;
                    Carrier.Signal(Carrier.Position);
                }
                break;
        }
        Rotation = _heading + Mathf.Pi / 2f;
    }

    private float TurnRate => (float)S["fighter_turn"];
    private float OffAngle(Vector2 p) => Mathf.Abs(Mathf.AngleDifference(_heading, (p - Position).Angle()));
    private void SteerTo(Vector2 p, float dt)
    {
        float diff = Mathf.AngleDifference(_heading, (p - Position).Angle());
        _heading += Mathf.Clamp(diff, -TurnRate * dt, TurnRate * dt);
    }
    private void Fly(float dt, float throttle = 1f)
    {
        Velocity = Vector2.Right.Rotated(_heading) * Speed * throttle;
        Position += Velocity * dt;
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
                if (live && Armed) { _b = BSt.Approach; Velocity = Carrier.Velocity; Carrier.Signal(Position); }
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
                                         (float)S["torpedo_range"], S["torpedo_damage"], source: Carrier);
                }
                if (Ammo <= 0) { _b = BSt.Return; _haveSlot = false; Carrier.NoteStrikeDone(); }
                break;

            case BSt.Return:
            {   // to a point just outboard of its own slot...
                var (slot, rot) = Carrier.DockSlot(this);
                var slotVel = SlotVelocity(slot, delta);
                var outward = Vector2.Up.Rotated(rot);
                FlyToward(slot + outward * 45f, Speed, delta, slotVel);
                if (Position.DistanceTo(slot + outward * 45f) < 8f) { _b = BSt.Backing; _backT = 0; }
                break;
            }
            case BSt.Backing:
            {   // ...then turn nose-out and back in, tail first. It rides the slot's TRUE motion:
                // a turning carrier swings the slot sideways as fast as a bomber backs in, and
                // following only the carrier's own velocity left it hovering and jittering beside
                // the slot, never docking, never re-arming.
                var (slot, rot) = Carrier.DockSlot(this);
                var slotVel = SlotVelocity(slot, delta);
                _backT += delta;
                Rotation = Mathf.LerpAngle(Rotation, rot, Mathf.Clamp(6f * (float)delta, 0f, 1f));
                Position += slotVel * (float)delta;
                if (_backT > 0.4) Position = Position.MoveToward(slot, 60f * (float)delta);
                if (Position.DistanceTo(slot) < DockSnap || _backT > BackingLimit)
                { _b = BSt.Docked; _rearm = S["bomber_rearm"]; SnapToDock(); Carrier.Signal(slot); }
                break;
            }
        }
    }

    // Docked: in its slot on the carrier's flank, nose out, tail to the hull.
    private void SnapToDock()
    {
        var (p, rot) = Carrier.DockSlot(this);
        Position = p; Rotation = rot; Velocity = Carrier.Velocity;
    }

    // Replicated so a guest shows the same state: its ability bar, docked fighters
    // hidden inside, and the signal light when anything lands or takes off.
    public int StateCode => F ? 100 + (int)_f : (int)_b;
    public void SetNetState(int code, double rearm)
    {
        if (Net.Sim) return;
        bool wasDocked = F ? _f == FSt.Docked : _b == BSt.Docked;
        if (code >= 100) _f = (FSt)(code - 100); else { _b = (BSt)code; _rearm = rearm; }
        bool docked = F ? _f == FSt.Docked : _b == BSt.Docked;
        if (docked != wasDocked) Carrier.Signal(docked && !F ? Carrier.DockSlot(this).pos : Position);
    }
    public bool IsBomber => Kind == WingKind.Bomber;

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

    public override void _Draw()
    {
        if (Inside || (!F && _b == BSt.Docked)) return;          // engines off when docked
        float len = F ? FighterLength : BomberLength;
        Plume.Draw(this, new Vector2(0, len * 0.5f), Vector2.Down, len, Carrier.Accent,
                   Velocity.Length() / Mathf.Max(1f, Speed), Velocity.Length() > 2f);
    }
}
