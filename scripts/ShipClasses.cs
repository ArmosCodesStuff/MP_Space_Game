using System.Linq;
using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// SHIP CLASSES — the first three of an intended nine.
//
//   BATTLESHIP : four main guns aimed with the cursor, a broadside of all of them, plus PD
//   CARRIER    : fighters sent at the selected target, a bomber strike of its own, plus PD
//   DESTROYER  : two main guns aimed with the cursor, guided missile bursts, plus PD; the fastest
//   (the keys are the player's: see Abilities)
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

// WHAT A RAIDER GOES AFTER: a player's ship, or a ship of a base's fleet (UtilityShip). One
// contract where six type switches -- in Raider and in Hub -- had to agree on alive-ness, hull
// shape, damage and the pin, and a new kind of target meant finding all six.
public interface IRaidTarget : IStatused
{
    bool InReach { get; }                                    // there, and alive, to be attacked
    void Hit(double d, Vector2 from, string source);
    (float halfLength, float halfWidth) Extent { get; }      // the hull's ellipse, for holding station beside it
}

// ── Wing craft ───────────────────────────────────────────────────────────────
//   FIGHTERS wait docked INSIDE the carrier. Ordered to attack the selected target
//            (while it is within control range), they launch and fly strafing runs
//            like aircraft -- steady speed, limited turn rate: 3 shots, through the
//            target by 1.2x its diameter, turn, repeat. After 15 s engaged they dock
//            and rest 3 s. Recall brings them home.
//   BOMBERS  are an active ability. They wait PARKED on the carrier's deck, in bays either
//            side of the runway -- small, the deck being far below, as the hauler's pad is --
//            half to port and half to starboard, rearming there. On the order they take off
//            one at a time, LaunchInterval apart (the fighters' cadence): each rolls onto the
//            runway and up it to the bow, growing to flight size as it lifts, as the hauler
//            does. They fly at the target, turn to face it at launch distance, and launch
//            torpedoes straight ahead -- the torpedoes do not track -- then come home over the
//            carrier's centre, settle onto the runway there, shrinking back to deck size, and
//            taxi to their bay. A target beyond the strike range (twice the fighters' control
//            range) is refused, and a strike whose target goes beyond it is called off.
public enum WingKind { Fighter, Bomber }

public partial class Wing : Node2D
{
    public PlayerShip Carrier;
    public WingKind Kind;
    public int Ammo;
    public Vector2 Velocity;

    // The bomber is the LARGER airframe (28.125 to the fighter's 17), so the two read apart at a glance.
    public const float FighterLength = 17f, BomberLength = 28.125f;
    private Sprite2D _sprite;
    private double _cd;

    // ── fighters: strafing runs ──────────────────────────────────────────────
    // Each pass: 3 shots, straight THROUGH the target until 1.2x its diameter past
    // it -- and at least one of the fighter's own turning circles past it -- then turn
    // for the next. They fly like aircraft -- steady speed, limited turn rate -- so the
    // pass and the turn are real, and a target left INSIDE the turning circle can never
    // be brought onto the nose: the fighter only flies laps round it. A small, slow
    // target (a light raider riding a pinned hauler) ended up there after every pass,
    // and was never fired on again. After 15 s engaged they fly home and dock INSIDE
    // the carrier for a 3 s rest; with nothing to fight they stay inside.
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
    // the carrier's clock when it last took off: a fighter out of the hangar, a bomber off the deck
    public double LaunchedAt { get; private set; } = -1;

    // ── bombers: the deck ─────────────────────────────────────────────────────
    // On the deck a bomber is drawn at LandedScale -- far below, as the hauler on its pad -- and
    // grows to full size as it lifts, on the hauler's curve (Altitude). Every deck move runs in the
    // carrier's frame on its own clock, so a moving or turning carrier carries it exactly, and a
    // landing always ends: it is timed, not steered.
    public const float LandedScale = 0.65f;         // the hauler's own: parked, 18 u across, inside the 21.3 u of white beside the runway
    public const double LiftTime = 1.6, LandTime = 1.0, TaxiTime = 0.6;   // (the lift's run up 118 u of runway ends at about its top speed)
    private const double RollOut = 0.25;            // the share of a lift spent rolling out onto the runway
    private const float TouchRadius = 10f;          // over the carrier's centre within this, a bomber sets down
    private enum BSt { Docked, Lifting, Approach, Aim, Launch, Return, Landing, Taxiing }
    private BSt _b = BSt.Docked;
    private double _deckT;                          // seconds into the deck move under way
    private Vector2 _touch; private float _touchRot; // where it set down, in the carrier's frame, and its heading then
    private bool _called;                           // counted into a strike when it was ordered, waiting on the deck for its turn
    public void Call() => _called = true;
    private bool OnDeck => _b is BSt.Docked or BSt.Lifting or BSt.Landing or BSt.Taxiing;
    // 0 on the deck, 1 at flight height (the hauler's SmoothStep)
    private float Altitude => _b switch
    {
        BSt.Docked or BSt.Taxiing => 0f,
        BSt.Lifting => Mathf.SmoothStep(0f, 1f, (float)((_deckT / LiftTime - RollOut) / (1 - RollOut))),
        BSt.Landing => 1f - Mathf.SmoothStep(0f, 1f, (float)(_deckT / LandTime)),
        _ => 1f,
    };
    public float VisualScale => F ? 1f : Mathf.Lerp(LandedScale, 1f, Altitude);
    public string BomberPhase => _b.ToString();
    private void Go(BSt s) { _b = s; _deckT = 0; }

    private const float CrawlSpeed = 0.25f;     // bombers keep closing at this fraction of top speed while they launch
    public bool Launching => _b == BSt.Launch;
    public bool Docked => _b == BSt.Docked;
    public bool Armed => _b == BSt.Docked && _rearm <= 0 && Ammo > 0;
    // A bomber that joins a wing already in service (a refit) arrives empty and rearms like one
    // just home: swapping a bay on and off must not reload a wing that has fired.
    public void StartRearm() { Ammo = 0; _rearm = S["bomber_rearm"]; }
    public double RearmLeft => _b == BSt.Docked ? Math.Max(0, _rearm) : 0;
    private double _rearm;

    // Where the host says this craft is. Guests steer toward it.
    private NetPose _net;

    private ShipStats S => Carrier.Stats;
    private bool F => Kind == WingKind.Fighter;
    private float Speed => (float)(F ? S["fighter_speed"] : S["bomber_speed"]);
    private float Accel => (float)S["bomber_accel"];            // bombers only: fighters fly at a steady speed
    private float Range => (float)(F ? S["fighter_range"] : S["launch_range"]);

    public void Init(PlayerShip carrier, WingKind kind, Vector2 at)
    {
        Carrier = carrier; Kind = kind; Position = at;
        Ammo = (int)S["bomber_ammo"];
        // Fighter and bomber have their own airframes, and the bomber is the larger of
        // the two (FighterLength, BomberLength) so they read apart at a glance.
        _sprite = Sprites.Fit(F ? "res://wing_fighter.png" : "res://wing_bomber.png", F ? FighterLength : BomberLength);
        _fit = _sprite.Scale;
        AddChild(_sprite);
        ZIndex = 4;
    }
    private Vector2 _fit;                       // the sprite's scale at flight size

    public void SetNet(Vector2 p, float rot) => _net.Set(p, rot);

    // (A wing is only ever ticked by its carrier, and the carrier frees its wings on its way out.
    // A carrier in stasis keeps its wing.)
    public void Tick(double delta)
    {
        Visible = Carrier.IsVisibleInTree() && !Inside;   // docked fighters are inside the hull
        QueueRedraw();                                     // the engine plume; a bomber's shadow on the deck
        if (!F) { _deckT += delta; _sprite.Scale = _fit * VisualScale; }

        if (!Net.Sim)
        {   // the host flies the wing; a guest follows where it says it is -- except a bomber
            // on the deck (parked, lifting, landing, taxiing), placed here exactly in the
            // carrier's frame on its own clock, rather than trailing a moving carrier by a
            // packet's worth of lag
            if (!F && (OnDeck || !_net.Has)) { DeckPose(delta); return; }
            if (_net.Has) _net.Follow(this, (float)delta);
            return;
        }

        _cd -= delta;
        if (F) TickFighter(delta); else TickBomber(delta);
    }

    private void TickFighter(double delta)
    {
        float dt = (float)delta;
        var t = Carrier.FighterTarget(Position);          // a dead target is replaced while they are out
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
                if (engage && Carrier.TakeLaunchSlot(WingKind.Fighter)) { _f = FSt.Launch; _launchT = 0; _engaged = 0; LaunchedAt = Carrier.Clock; Carrier.Signal(Carrier.Position); }
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
                if (beyond >= Mathf.Max(Overshoot * 2f * t.HitRadius, TurnDiameter)) { LastOvershoot = beyond; _f = FSt.Turn; }
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
    public float TurnDiameter => 2f * Speed / TurnRate;
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
        // Carrier.Alive, as the fighter's `engage` has: a strike ordered in the moment the deck
        // died kept flying, and a host that ran a guest's press on a wreck launched a fresh one.
        bool live = Carrier.Alive && t != null && t.Alive && Carrier.Position.DistanceTo(t.Position) <= S["strike_range"];
        switch (_b)
        {
            case BSt.Docked:
                DeckPose(delta);
                if (_rearm > 0) { _rearm -= delta; if (_rearm <= 0) Ammo = (int)S["bomber_ammo"]; }
                if (_called && live)
                {   // in the strike it was counted into: it takes off when the deck gives it its turn
                    if (Carrier.TakeLaunchSlot(WingKind.Bomber))
                    { _called = false; Go(BSt.Lifting); LaunchedAt = Carrier.Clock; Carrier.Signal(Position); }
                }
                // called off before its turn came: it answers for itself, once, as a bomber out does
                else if (_called) { _called = false; Carrier.NoteStrikeDone(); }
                break;

            case BSt.Lifting:
                DeckPose(delta);
                if (_deckT >= LiftTime)
                {   // off the bow with the speed its run up the runway ended at, in the carrier's frame
                    // (the pace of the run's u-squared at its end) -- a frame's jump of the carrier, a
                    // warp or a snap, is not a speed
                    float run = 2f * (Carrier.Bay(this).Y + Carrier.MyArt.RunwayBow) / (float)(LiftTime * (1 - RollOut));
                    Velocity = Carrier.Velocity + Vector2.Up.Rotated(Carrier.Rotation) * run;
                    _b = BSt.Approach;
                }
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
                if (Ammo <= 0) { _b = BSt.Return; Carrier.NoteStrikeDone(); }
                break;

            case BSt.Return:                           // home: over the carrier's centre, moving with it
                FlyToward(Carrier.Position, Speed, delta, Carrier.Velocity);
                if (Position.DistanceTo(Carrier.Position) < TouchRadius)
                {
                    _touch = Carrier.ToLocal(Position); _touchRot = Mathf.AngleDifference(Carrier.Rotation, Rotation);
                    Go(BSt.Landing); Carrier.Signal(Position);
                }
                break;

            case BSt.Landing:
                DeckPose(delta);
                if (_deckT >= LandTime) Go(BSt.Taxiing);
                break;

            case BSt.Taxiing:
                DeckPose(delta);
                if (_deckT >= TaxiTime) { Go(BSt.Docked); _rearm = S["bomber_rearm"]; Carrier.Signal(Position); }
                break;
        }
    }

    // Where a bomber on the deck is, from the carrier and its own clock: parked in its bay, facing
    // the bow; LIFTING, rolling out onto the runway beside its bay, then up it to the bow end,
    // gathering speed; LANDING, from where it came over the centre down onto it, turning to face
    // the bow; TAXIING, from the centre to its bay.
    private void DeckPose(double delta)
    {
        var bay = Carrier.Bay(this);
        var local = bay; float turn = 0f;
        switch (_b)
        {
            case BSt.Lifting:
            {
                float k = (float)(_deckT / LiftTime), roll = (float)RollOut;
                var onRunway = new Vector2(0f, bay.Y);
                if (k < roll) local = bay.Lerp(onRunway, Mathf.SmoothStep(0f, 1f, k / roll));
                else
                {
                    float u = Mathf.Clamp((k - roll) / (1f - roll), 0f, 1f);
                    local = onRunway.Lerp(new Vector2(0f, -Carrier.MyArt.RunwayBow), u * u);
                }
                break;
            }
            case BSt.Landing:
            {
                float k = Mathf.SmoothStep(0f, 1f, (float)(_deckT / LandTime));
                local = _touch.Lerp(Vector2.Zero, k); turn = Mathf.LerpAngle(_touchRot, 0f, k);
                break;
            }
            case BSt.Taxiing:
                local = Vector2.Zero.Lerp(bay, Mathf.SmoothStep(0f, 1f, (float)(_deckT / TaxiTime)));
                break;
        }
        var at = Carrier.ToGlobal(local);
        Velocity = delta > 0 ? (at - Position) / (float)delta : Carrier.Velocity;
        Position = at; Rotation = Carrier.Rotation + turn;
    }

    // Replicated so a guest shows the same state: its ability bar, docked fighters hidden inside,
    // a bomber's deck moves, and the signal light when anything lands or takes off.
    public int StateCode => F ? 100 + (int)_f : (int)_b;
    public void SetNetState(int code, double rearm)
    {
        if (Net.Sim) return;
        if (code >= 100)
        {
            bool wasDocked = _f == FSt.Docked;
            _f = (FSt)(code - 100);
            if ((_f == FSt.Docked) != wasDocked) Carrier.Signal(Position);
            return;
        }
        _rearm = rearm;
        var b = (BSt)code;
        if (b == _b) return;
        // a deck move runs on this peer's own clock from here; a landing from where this peer shows it
        if (b == BSt.Landing) { _touch = Carrier.ToLocal(Position); _touchRot = Mathf.AngleDifference(Carrier.Rotation, Rotation); }
        Go(b);
        if (b is BSt.Lifting or BSt.Landing or BSt.Docked) Carrier.Signal(Position);
    }
    public bool IsBomber => Kind == WingKind.Bomber;

    // Accelerate toward a point, easing off to arrive rather than overshoot.
    // `carry` is the velocity of whatever the craft is homing on (the carrier's centre, coming
    // home to land), so it arrives moving WITH it rather than stopping dead over it.
    private void FlyToward(Vector2 to, float spd, double delta, Vector2 carry = default)
    {
        var d = to - Position;
        float dist = d.Length();
        // the fastest speed from which this craft can still stop in `dist`
        float arrive = Motion.Arrive(spd, dist, Accel);
        var want = carry + (dist > 1f ? d / dist * arrive : Vector2.Zero);
        Velocity = Velocity.MoveToward(want, Accel * (float)delta);
        Position += Velocity * (float)delta;
        if (Velocity.LengthSquared() > 100f) FaceToward(Position + Velocity, delta);
    }

    private void FaceToward(Vector2 to, double delta)
    {
        float want = Aim.Face(Position, to);
        Rotation = Mathf.LerpAngle(Rotation, want, Mathf.Clamp(9f * (float)delta, 0f, 1f));
    }


    public override void _Draw()
    {
        if (Inside) return;                                      // a fighter in the hangar
        float len = (F ? FighterLength : BomberLength) * VisualScale;
        if (!F && OnDeck)
        {   // its shadow on the deck, tucked in when parked, further out and fainter the higher it
            // rises (the hauler's): screen down-right, in the bomber's own frame
            float alt = Altitude;
            var size = _sprite.Texture.GetSize() * _sprite.Scale;
            var drop = (new Vector2(3f, 5f) * (0.15f + alt)).Rotated(-Rotation);
            DrawTextureRect(_sprite.Texture, new Rect2(drop - size / 2, size), false, new Color(0, 0, 0, (0.45f - 0.15f * alt) * (1f - alt)));
            if (_b is BSt.Docked or BSt.Taxiing) return;       // engines off on the deck
        }
        Plume.Draw(this, new Vector2(0, len * 0.5f), Vector2.Down, len, Carrier.Accent,
                   Velocity.Length() / Mathf.Max(1f, Speed), Velocity.Length() > 2f);
    }
}
