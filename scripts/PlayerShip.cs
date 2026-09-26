using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// PLAYER SHIP — you ARE this.
//
// It handles like a naval ship: thrust only along the keel (W ahead, S astern),
// the rudder (A/D) turns it on a radius that needs way on to work, and sideways
// drift bleeds off as if the hull were in water. No strafing; almost stopped, the rudder
// pivots the hull slowly on the spot.
//
// Networking split, following Net's rule:
//   the OWNER reads input: it moves the ship, and reports where it aims and
//     whether it holds the guns key, because input latency must feel instant;
//   the HOST resolves everything that deals damage or spends a resource, and
//     reports hull, ability state and wing positions back;
//   everyone else interpolates.
// ─────────────────────────────────────────────────────────────────────────────
public partial class PlayerShip : Node2D, IHittable, IRaidTarget, ITagged, ITurretHost, IPrism, IMendable
{
    public int OwnerId = 1;

    // Identity as announced by the owner. Every ship carries its OWN copy -- reading
    // the static Character here would paint every ship in the local player's colours.
    public string Pilot = Character.Defaults.Name;
    public Color Main = Character.Defaults.Main, Accent = Character.Defaults.Accent;

    // A DISPLAY SHIP: the title screen's battleship. It is a real PlayerShip -- the same
    // turrets, shells, broadside and warp -- but nobody is flying it, so it reads no keyboard and
    // no mouse. Whatever drives it sets AimPoint, Trigger and AutopilotTo itself.
    public bool Demo;

    public ShipClass Class = ShipClass.Battleship;
    public ShipStats Stats = new(ShipClass.Battleship);
    public bool Alive { get; private set; } = true;
    public Vector2? AutopilotTo;                          // set by READY: the owner's ship flies itself there

    // ── as a target: for ENEMY fire only (Combat.Players) ──────────────────
    public int NetId => NetIds.In(NetIds.Player, OwnerId);   // same on every peer: the pilot's seat
    public Tag Tags => Tag.Player;
    public float HitRadius => MyArt.HalfWidth;
    public bool Covers(Vector2 p, float pad) => Combat.KeelCovers(this, MyArt.Length, MyArt.HalfWidth, p, pad);
    bool IRaidTarget.InReach => Alive;
    (float halfLength, float halfWidth) IRaidTarget.Extent => (MyArt.Length * 0.5f, MyArt.HalfWidth);
    // ...and as a hull a friend may mend (Mend.Give): never a wreck
    bool IMendable.Mendable => Alive;
    double IMendable.HullNow => Hp;
    double IMendable.HullMax => MaxHp;
    float IMendable.BodyRadius => HitRadius;
    void IMendable.Mended(double d) => Hp += d;

    // ── death: stasis, and the escape pod ───────────────────────────────────
    // 24 s in stasis, then F re-boards at a third of the hull (the owner's ruling). A party with a
    // pilot still flying fights on meanwhile; the whole party in stasis at once fails the mission.
    public const double StasisTime = 24, ReboardHull = 0.33;

    // ── "in combat": dealt or took damage in the last CombatHold seconds (host) ──
    // Out of combat after 12 s: the one global rule (music and regeneration use it). The equipment
    // base's shorter lock (Landmarks.CalmNeeded, 10 s) reads the SAME clock through CalmFor. So the
    // game has one "last blow" with two thresholds on it, never two clocks.
    public const double CombatHold = 12;
    // Regeneration, always: 0.5% of max hull a second in combat, 3% out of it.
    public const double RegenInCombat = 0.005, RegenOutOfCombat = 0.03;
    // One ongoing source (an ability, a weapon, an area) lands on a ship at most once per 0.52 s.
    // WHEN each source last landed, and nothing else. An entry older than HitGap can only ever
    // say "let it through", which is exactly what no entry says, so it is provably dead: SweepHits
    // throws it away on the same clock the gap is measured against. It used to grow by one entry
    // per attacker that had EVER touched this hull, for the life of the world. The tally beside
    // it is not a timer and is NOT swept -- the HUD and the checks read it.
    private const double HitGap = 0.52;
    private readonly System.Collections.Generic.Dictionary<string, double> _lastHitBy = new();
    private double _sweepAt;                     // the clock time the next sweep of _lastHitBy is due at
    public readonly System.Collections.Generic.Dictionary<string, double> DamageBySource = new();   // host: damage taken, by source
    public readonly System.Collections.Generic.Dictionary<string, double> DealtBy = new();           // host: damage dealt, by weapon id (Dealt.Deal)
    public readonly System.Collections.Generic.Dictionary<string, double> MendedBy = new();          // host: hull healed on others, by source id (Mend.Give)
    private double _combatT;
    public bool InCombat => _combatT > 0;
    // SECONDS SINCE THE LAST BLOW, as far as the clock above can tell. It runs down from
    // CombatHold, so anything longer reads as CombatHold, and a rule that needs more calm than that
    // cannot be read off it (a smoke check holds Landmarks.CalmNeeded under CombatHold). On a
    // guest it is the host's figure, from the host-state packet, counted down here between packets.
    public double CalmFor => CombatHold - _combatT;
    public void NoteCombat() { if (Net.Sim) _combatT = CombatHold; }
    private double _stasis;
    public double StasisLeft => _stasis;
    public bool CanReboard => !Alive && _stasis <= 0;
    private EscapePod _pod;
    private ShieldFlash _shield;
    // where the camera should look: the pod while the ship is in stasis
    public Vector2 ViewPosition => !Alive && IsInstanceValid(_pod) ? _pod.Position : Position;
    public double Hp, MaxHp;

    // ── art: sprite, size, and where the turrets sit ─────────────────────────
    // Shared with the character preview, so the preview shows the real hull with
    // turrets on the real mounts. Offsets are in world units, nose up (-y).
    public ClassArt MyArt => Classes.Art(Class);

    // ── the owner's intent, replicated at 20 Hz ──────────────────────────────
    public Vector2 AimPoint;               // where the main guns point
    private bool Thrusting;                 // the owner is on the throttle (plume flicker)
    private bool _trigger;
    // GUNS KEY HELD (battleship, destroyer) -- and A WRECK DOES NOT FIRE. Three drivers write it:
    // the local flight, the display ship's demo, and the WIRE. A guest's 20 Hz report is built
    // before it can learn it died (its death arrives on the host's 10 Hz packet), so the raw field
    // was re-armed after Die() cleared it and the host fired live shells out of a hull in stasis
    // for a round trip -- for ever, from a client that simply never cleared it. FireControl was
    // the one host-side weapon path with no liveness test; the rule belongs here, where every gun
    // and every readout that asks already looks.
    public bool Trigger { get => _trigger && Alive; set => _trigger = value; }
    public bool Staggered;                 // fire mode: false = salvo, true = staggered

    // ── orders and ability state, held on the host ───────────────────────────
    public IHittable WingTarget { get; private set; }      // fighters engage this
    private bool _attacking;                                  // an attack is on: only a recall (R) ends it

    // Fighters out on an attack whose target is gone take the nearest other hostile to where they are,
    // within control range of the carrier, and the whole wing with them; with none left, they go home.
    // Never a practice dummy: it cannot die, so the wing would strafe it for ever (the pilot may pick one).
    public IHittable FighterTarget(Vector2 from)
    {
        if (!_attacking || !Net.Sim || WingTarget is { Alive: true }) return WingTarget;
        WingTarget = Targeting.Nearest(Combat.Hostiles, from, Targeting.WingPrey, float.MaxValue,
                                       h => Position.DistanceTo(h.Position) <= Stats["control_range"]);
        _attacking = WingTarget != null;
        return WingTarget;
    }
    public IHittable StrikeTarget { get; private set; }    // bombers run at this
    private int _strikesOut;

    // ── what this ship's own class brings ────────────────────────────────

    // WHAT LIFTS ITS RATE OF FIRE, and WHAT LIFTS ITS SPEED, right now -- the tender's overdrive,
    // the V boost, the wraith's veil. Every running ability that names a
    // stat for one of them (AbilityDef.RateStat, AbilityDef.SpeedStat) lifts it by that figure,
    // and the row knows its own slot, so the next buff is a ROW rather than an `if` naming a slot
    // id and a stat id by string.
    //   Lifts ADD (LiftShares; the owner's ruling): two x2 buffs at once are x3, not x4. A product
    // would have compounded every rate part the item pass adds.
    //   The rate reaches the reloads through ONE door, Cadence, which adds the lifts' shares to
    // the reload's own bonus -- so a part's +100% under a x2 lift is x3 as well: the main
    // guns (FireControl), the point defence (Spec), every turret it has out (Deployed.Spec) and
    // every craft of its wing. SpeedMult lifts the top speed AND the thrust (Steer); StrafeMult the
    // slide's speed and thrust, by each row's StrafeStat where it names one (the boost's surge_strafe).
    public float FireRate => (float)Stat.Scale(Lifts(Lift.Rate));
    public float SpeedMult => (float)Stat.Scale(Lifts(Lift.Speed));
    public float StrafeMult => (float)Stat.Scale(Lifts(Lift.Strafe));
    // REACH: every running row's ReachStat, the same rule (the anchor's x1.4 on the railgun's line)
    public float ReachMult => (float)Stat.Scale(Lifts(Lift.Reach));
    // DAMAGE: the stat's own figure, lifted by every running row whose DamageOn names it (the CIWS's x3 on pd_damage)
    public double DamageOf(string stat) => Stats[stat] * Stat.Scale(Lifts(Lift.Damage, stat));
    // THE PUSH AHEAD: the speed lifts' shares PLUS every running row's ThrustStat (the sprint's x3), the same rule --
    // a sprint under the boost is x3.5. Steer's thrust reads it; the top speed never does.
    public float ThrustMult => (float)Stat.Scale(Lifts(Lift.Speed) + Lifts(Lift.Thrust));
    private enum Lift { Rate, Speed, Strafe, Reach, Thrust, Damage }
    private readonly List<double> _lifts = new();
    // `on` is the stat a scoped row must name (RateOn / DamageOn); a rate asked with none takes the unscoped rows alone
    private static string LiftStat(AbilityDef def, Lift kind, string on) =>
        kind switch {
            Lift.Rate => def.RateOn == null || def.RateOn == on ? def.RateStat : null,
            Lift.Damage => def.DamageOn != null && def.DamageOn == on ? def.DamageStat : null,
            Lift.Speed => def.SpeedStat, Lift.Reach => def.ReachStat, Lift.Thrust => def.ThrustStat, _ => def.StrafeStat ?? def.SpeedStat };
    private double Lifts(Lift kind, string on = null)
    {
        _lifts.Clear();
        // A FIELD'S LIFTS (AbilityDef.Aura, kits6b-J8): every OTHER live pilot's running Aura row whose radius this
        // hull stands inside lifts it by that pilot's own figure, beside this ship's own rows
        foreach (var h in Combat.Players)
        {
            if (h is not PlayerShip p || ReferenceEquals(p, this) || !p.Alive) continue;
            foreach (var def in Abilities.For(p.Class))
            {
                if (def.Aura == null || LiftStat(def, kind, on) is not { } their || p.Sl(def.Id).Left <= 0) continue;
                if (Position.DistanceTo(p.Position) <= p.Stats[def.Aura]) _lifts.Add(p.Stats[their]);
            }
        }
        foreach (var def in Abilities.For(Class))
        {
            ref var sl = ref Sl(def.Id);
            string stat = LiftStat(def, kind, on);
            if (stat != null && sl.Left > 0 && (def.While == null || def.While(this))) _lifts.Add(Stats[stat]);
            // a RAMP's running total is already a share (F1, D18): 1 + it reads the same as any
            // other lift's raw multiplier would, and it keeps lifting through its post-run drain,
            // not only while Left > 0. It lifts the helm alone: speed and slide.
            if ((kind == Lift.Speed || kind == Lift.Strafe) && def.Ramp != null && sl.Own > 0) _lifts.Add(1 + sl.Own);
        }
        return LiftShares(_lifts);
    }
    // EVERY RUNNING ROW'S FLAT SpeedAdd (F1's Add, D18), summed: added to the sheet's own top speed
    // AFTER the lift's multiplier, in TopSpeed below.
    private double SpeedAdds()
    {
        double sum = 0;
        foreach (var def in Abilities.For(Class))
        {
            if (def.SpeedAdd == null || Sl(def.Id).Left <= 0) continue;
            sum += Stats[def.SpeedAdd];
        }
        return sum;
    }
    // THE ONE PLACE Add's ARITHMETIC LIVES (D18): sheet x lift (SpeedMult, already Scale(shares))
    // PLUS every running row's flat add, THEN the hold on the whole -- so a hold of x0 also zeroes
    // what Add gave it. Pure and static, so it is provable before any row uses SpeedAdd.
    public static float TopSpeed(float sheet, float lift, double add, float hold) => (float)((sheet * lift + add) * hold);
    // Lifts, as SHARES: each adds what it is worth over x1 (x2 is +1, x0.85 is -0.15), and the ship
    // takes Stat.Scale of the sum -- the sheet's own rule, on its own floor. A sheet with no such
    // row answers 0: that lifts nothing, rather than stopping the ship.
    // WHAT HOLDS IT, right now: every running row's Hold (AbilityDef.Hold), MULTIPLIED, and taken
    // AFTER the lifts are summed -- the share rule: buffs add, holds and slows multiply, as the
    // web's PinSpeed does. So no lift moves a held hull: x0 is rooted whatever else runs. Steer
    // puts it on the whole helm, both ways and the rudder.
    public float Held
    {
        get
        {
            double h = 1;
            foreach (var def in Abilities.For(Class))
                if (def.Hold < 1 && Sl(def.Id).Left > 0) h *= def.Hold;
            return (float)h;
        }
    }
    public static double LiftShares(IReadOnlyList<double> lifts)
    {
        double shares = 0;
        for (int i = 0; i < lifts.Count; i++) if (lifts[i] > 0) shares += lifts[i] - 1;
        return shares;
    }
    // THE SECONDS BETWEEN SHOTS of the gun whose interval stat this is, at the rate this ship fires
    // at now: the one place a lift becomes time. (A broadside's volley gap and a missile burst's
    // refire are not a gun's reload, and no class that has them carries a rate row.)
    // Burst Feed's window (Items.Door.AfterDrive) lifts the primary's reloads alone, for AfterDriveSecs
    // after the drive's run ends.
    public double Cadence(string intervalStat) => Stats.With(intervalStat, Lifts(Lift.Rate, intervalStat)
        + (_afterDrive > 0 && Items.IdsOf("@primary_rate", Class).Contains(intervalStat)
            ? Items.Shares(Items.Door.AfterDrive, Stats, default) : 0));
    private double _afterDrive;

    // ── THE CONDITIONS THIS SHIP'S GEAR LIFTS (Items.Conditions), each at its one door ──
    private IHittable _spinOn;                      // Spin-up Feed: the target, since when, and the last blow
    private double _spinSince, _spinLast;
    public double HullLeft => MaxHp > 0 ? Hp / MaxHp : 1;
    private Items.Blow BlowOn(IHittable target, double d) => new(target, HullLeft, d, MaxHp);
    // A blow this ship deals, weighed (Dealt.Deal): its Dealt rows added, and the primary's ramp. A
    // repeat (Items.Repeats: the reverb's blast, a dose's tick) is what was already weighed, and passes as it is.
    public double Outgoing(IHittable target, double d, string weapon)
    {
        if (Array.IndexOf(Items.Repeats, weapon) >= 0) return d;        // weighed once, as each stored blow landed
        double share = Items.Shares(Items.Door.Dealt, Stats, BlowOn(target, d));
        if (Array.IndexOf(Items.PrimaryShots, weapon) >= 0)
        {
            if (target != _spinOn || _clock - _spinLast > Items.SpinDrop) { _spinOn = target; _spinSince = _clock; }
            _spinLast = _clock;
            share += Items.SpinShare(Items.Shares(Items.Door.Spin, Stats, default), _clock - _spinSince);
        }
        // A CRAFT THIS SHIP CALLED (the Taunt) takes its taunt_mult -- a row of the dealer's sheet, 0 (nothing)
        // on a class without it
        double called = target is ICalled { CalledBy: { } by } && ReferenceEquals(by, this) && Stats["taunt_mult"] > 0 ? Stats["taunt_mult"] : 1;
        return d * (1 + share) * called * Backstab(target);
    }
    // BACKSTAB (a stat row, the Wraith's passive): a blow this ship lands from inside the target's rear arc (backstab_arc
    // degrees either side of its tail) takes backstab_mult; a target with no heading (IHittable.Facing) has no behind.
    // 0 on every other sheet: x1.
    public double Backstab(IHittable target)
    {
        double mult = Stats["backstab_mult"];
        if (mult <= 0 || target?.Facing is not { } nose) return 1;
        var from = Position - target.Position;
        if (from.LengthSquared() < 1e-6f) return 1;
        return Mathf.RadToDeg(Mathf.Abs(from.AngleTo(-nose))) <= Stats["backstab_arc"] ? mult : 1;
    }
    // A kill this ship made: every ability cooldown left, less the Kill rows' share.
    public void NoteKill()
    {
        double cut = Math.Min(1, Items.Shares(Items.Door.Kill, Stats, default));
        if (cut <= 0) return;
        for (int i = 1; i < _slots.Length; i++) _slots[i].Cool *= 1 - cut;          // every slot once; 0 is the spare
    }
    // How much faster a turret of this ship swings onto `t` (Turret.RotSpeed).
    public float TrackingOn(IHittable t) => (float)(1 + Items.Shares(Items.Door.Tracking, Stats, BlowOn(t, 0)));
    private double WebCut => Items.Shares(Items.Door.Web, Stats, default);

    // ── ABILITY SLOTS ────────────────────────────────────────────────────
    // The state of every ability this class carries, in the class's own order. Each ability
    // counts the same four things: how long it has LEFT running, how long it must COOL before it
    // is ready again, one number it names itself (the gap to the next volley, damage stored, a
    // charge) and a COUNT (bursts in the magazine, volleys still to fire). A new ability adds no
    // field here and nothing to the wire -- it names its slot and uses the four.
    //
    // Slot 0 is the SPARE: an id this class does not carry lands there, so asking a battleship
    // about its magazine is harmless rather than a crash.
    public struct Slot { public double Left, Cool, Own; public int N; public Vector2 At; }
    private Slot[] _slots = new Slot[1];                 // the spare alone, until the class is fitted
    private readonly Dictionary<string, int> _slotAt = new();
    private readonly Dictionary<string, (Slot slot, double clock)> _slotsAway = new();   // every slot a class change put away, and when (FitClass)
    public ref Slot Sl(string id) => ref _slots[_slotAt.TryGetValue(id, out int i) ? i : 0];

    // The broadside (battleship): a wind-up while the turrets swing onto the cursor, then every main
    // gun fires, volley after volley, then the cooldown. The host acts on it; every peer counts the
    // timers down between reports, so a guest's bar and turrets move smoothly.
    public double BroadsideWindupLeft => Sl("broadside").Left;
    public int BroadsideVolleysLeft => Sl("broadside").N;
    public double BroadsideCooldownLeft => Sl("broadside").Cool;
    // wound up or firing: the main turrets swing fast enough to reach the cursor within the wind-up
    public bool BroadsideTracking => BroadsideWindupLeft > 0 || BroadsideVolleysLeft > 0;
    private bool BroadsideReady => Stats.Def.Has(Fit.Broadside) && Alive && !BroadsideTracking && BroadsideCooldownLeft <= 0;

    // ── what this ship's turrets are bolted to (ITurretHost) ─────────────
    // A turret asks these and nothing else, so the same component sits on a ship, on a freighter's
    // deployed mount, on the hauler and on anything else that grows a gun.
    public Node2D AsNode => this;
    // POINT DEFENCE IS PASSIVE: no key, no window, no recharge. It fires whenever the ship is
    // alive -- helm-disabled or not -- at whatever its row gives it; a wreck's mounts are quiet.
    public bool PdOnline => Alive;
    public Vector2 AimAt => AimPoint;
    // Through a broadside the main turrets come round onto the cursor from anywhere within the
    // wind-up -- half a turn in its time -- or at their own rate if that is already faster.
    public float FastSwing => BroadsideTracking ? (float)(Math.PI / Math.Max(0.05, Stats["broadside_windup"])) : 0f;
    public IReadOnlyList<Turret> Siblings => PdTurrets;
    // WHAT THIS SHIP JUST DEALT, and to what (F18). Every weapon that knows whose it is comes
    // through here -- a turret, a shell, a torpedo -- so "it is in combat" has one place to be set,
    // the tally (DealtBy) has one place to grow, and every RUNNING ability hears it through its own
    // row (AbilityDef.OnDealt) rather than this method special-casing the echo by name.
    public void NoteDealt(double d, IHittable target, string weapon)
    {
        NoteCombat();
        DealtBy[weapon] = DealtBy.GetValueOrDefault(weapon) + d;
        foreach (var def in Abilities.For(Class))
        {
            if (def.OnDealt == null || Sl(def.Id).Left <= 0) continue;
            def.OnDealt(this, target, d, weapon);
        }
    }
    public PlayerShip Credit => this;

    public TurretSpec Spec(bool pd)
    {
        var art = MyArt;
        return new TurretSpec {
            Prey     = Targeting.PointDefence,        // what its PD takes: a main gun never picks
            Weapon   = pd ? Dealt.Pd : null,          // what the door credits its PD's blows as
            Kind     = pd ? Shots.Shell : Stats.Def.Shot,   // the round its mains fire (ClassDef.Shot: the freighter's spotter, the Warden's flak)
            Damage   = DamageOf(pd ? "pd_damage" : "main_damage"),   // lifted by every running row's DamageOn (the CIWS x3)
            Interval = Cadence(pd ? "pd_interval" : "main_interval"),
            Range    = (float)(pd ? Stats["pd_range"] : Stats["main_range"]),
            Turn     = (float)(pd ? Stats["pd_turn"]  : Stats["main_turn"]),
            ShellSpeed = (float)Stats["shell_speed"],
            // each main round's echo (the Echo's repeater): a stat row, 0 on every sheet that has none
            RepeatShare = pd ? 0 : Stats["echo_share"], RepeatDelay = Stats["echo_delay"],
            // a volley fanned about the barrel (the Wraith's scattergun): stat rows, 0 (one round) on every other sheet
            Pellets  = pd ? 0 : (int)Stats["scatter_pellets"], Fan = (float)Stats["scatter_spread"],
            Texture  = pd ? art.PdTurret : art.MainTurret,
            TexScale = art.TurretTexScale,
            Barrel   = pd ? art.PdBarrel : art.MainBarrel,
            Tint     = Accent,
        };
    }

    private readonly List<Turret> _turrets = new();
    private readonly List<Turret> _mains = new();
    public readonly List<Turret> PdTurrets = new();
    private readonly List<Wing> _wings = new();
    private double _gunCd;
    private int _nextBarrel;

    public Vector2 Velocity;
    private float _yawRate;            // current turn rate, rad/s
    private Sprite2D _sprite;

    // What remote peers steer toward between updates.
    private Vector2 _netPos, _netVel;
    private float _netRot, _netAge;
    private double _sendCd, _hostSendCd;
    private const double SendInterval = 0.05;    // owner -> all, 20 Hz
    private const double HostInterval = 0.10;    // host -> all, 10 Hz

    // Name the node BEFORE adding it: RPCs address nodes by path, and Godot's
    // auto-generated names differ between peers, so an unnamed ship never receives one.
    public static string NodeName(int ownerId) => $"Ship_{ownerId}";

    public void Init(int ownerId, Vector2 at)
    {
        OwnerId = ownerId;
        Position = _netPos = at;
        AimPoint = at + new Vector2(0, -400);
        SetMultiplayerAuthority(ownerId);
        if (!Combat.Players.Contains(this)) Combat.Players.Add(this);
        _sprite = new Sprite2D();
        AddChild(_sprite);
        ZIndex = 4;
        FitClass();
    }

    // ── loadout ──────────────────────────────────────────────────────────────
    private void FitClass()
    {
        foreach (var t in _turrets) t.QueueFree();
        foreach (var w in _wings) w.QueueFree();
        _turrets.Clear(); _mains.Clear(); PdTurrets.Clear(); _wings.Clear();
        WingTarget = StrikeTarget = null; _strikesOut = 0; _attacking = false;

        // A CLASS CHANGE IS A REFIT (audit P8): the hull keeps its FRACTION, as Restat does (a new ship's
        // first fit, MaxHp 0, is a full one), and no slot is refreshed: each one's cooldown and count are
        // kept by id through every class change, run down by the game seconds it was away, so flying B
        // and then A again hands back A's bar as it would be now. What was running (Left, Own) ends.
        double frac = MaxHp > 0 ? Hp / MaxHp : 1;
        foreach (var (id, at) in _slotAt) _slotsAway[id] = (_slots[at], _clock);
        _dashLeft = 0; _dashRow = null;            // a dash's carry is running too: it ends with its row
        Stats = BuildSheet();
        MaxHp = Stats["hull"];
        Hp = Alive ? Math.Max(1, frac * MaxHp) : MaxHp;
        _hullWatch = default;                      // a refit, not damage
        // The class's slots, index 0 the spare: a new one with every timer at zero.
        var list = Abilities.For(Class);
        _slots = new Slot[list.Length + 1];
        _slotAt.Clear();
        for (int i = 0; i < list.Length; i++) _slotAt[list[i].Id] = i + 1;
        if (Array.IndexOf(list, Ab.FireMode) < 0) Staggered = false;   // a hull with no fire mode (the destroyer) fires salvoes
        _netSlotLeft = new float[_slots.Length]; _netSlotCool = new float[_slots.Length];
        _netSlotOwn = new float[_slots.Length]; _netSlotN = new int[_slots.Length];
        foreach (var (id, at) in _slotAt)
            if (_slotsAway.Remove(id, out var kept))
            {
                ref var s = ref _slots[at];
                s.Cool = Math.Max(0, kept.slot.Cool - (_clock - kept.clock));
                s.N = kept.slot.N;                 // its count (charges spent, a chamber) as it was
            }
        // a chamber is fitted seated (a reload that was running ended with the refit), and the owner's view with it
        foreach (var def in list) if (def.Reload is { } rl) ActiveReload.Seat(this, rl);
        ReloadView = default; _charge = -1; _strokeDown = false;

        var art = MyArt;
        var tex = Assets.Load<Texture2D>(art.Texture);
        _sprite.Texture = tex;
        _sprite.Scale = Vector2.One * (art.Length / tex.GetHeight());
        _sprite.Modulate = Main;
        if (!IsInstanceValid(_shield)) { _shield = new ShieldFlash(); AddChild(_shield); }
        _shield.HalfWidth = art.HalfWidth; _shield.HalfLength = art.Length * 0.5f; _shield.Tint = Accent;

        for (int i = 0; i < Math.Min((int)Stats["main_count"], art.Mains.Length); i++) AddTurret(art.Mains[i], false, i < art.MainBears.Length ? art.MainBears[i] : new Vector2(0f, 180f));
        for (int i = 0; i < Math.Min((int)Stats["pd_count"], art.Pds.Length); i++) AddTurret(art.Pds[i], true, new Vector2(0f, 180f));
        FitWings(fresh: true);
    }

    // THE ONE SHEET: class, gear and purchases. Every ship carries its pilot's gear, the levels that
    // pilot bought for it, and its purchases (they come with the identity); your own adds
    // Character.Bonuses. The title screen's ship (Demo) flies the kit, whatever the pilot has fitted
    // or levelled.
    private ShipStats BuildSheet()
    {
        if (Mine && !Demo) _fitted = (string[])Character.LoadoutFor(Class).Clone();   // a COPY: the window edits the saved one in place
        var loadout = Loadout;
        var pct = Equipment.Bonuses(Class, loadout, Peak, Levels);
        if (Mine && !Demo) pct = ShipStats.Sum(pct, Character.Bonuses);
        pct = ShipStats.Sum(pct, Progression.Shares(_bought, Class));       // the pilot's own percentages
        return new ShipStats(Class, pct, ShipStats.Sum(Progression.Flats(_bought, Class), Equipment.Adds(Class, loadout, Peak, Levels)));
    }

    // THE WING, brought to the sheet's counts: gear adds fighters or takes bombers away, and may be
    // changed mid-flight. Fighters first, then bombers -- the order the host's wing report is read
    // in, so every peer's list lines up with the host's. A craft removed is freed where it is; a
    // change in the bombers calls off a strike in progress; a smaller magazine or bomb load never
    // holds more than it has room for. A bomber added to a wing in service arrives rearming, not
    // armed (fresh: the whole wing being built). (A battleship's sheet has no wing: both counts read 0.)
    private void FitWings(bool fresh)
    {
        int wantF = (int)Stats["fighter_count"], wantB = (int)Stats["bomber_count"], hadB = WingCount(WingKind.Bomber);
        while (WingCount(WingKind.Fighter) < wantF && AddWing(WingKind.Fighter, WingCount(WingKind.Fighter))) { }
        while (WingCount(WingKind.Fighter) > wantF) RemoveWing(WingKind.Fighter);
        // bombers after the fitted craft and before any sortie's, which ride at the end of the list
        while (WingCount(WingKind.Bomber) < wantB && AddWing(WingKind.Bomber, Fitted)) if (!fresh) _wings[Fitted - 1].StartRearm();
        while (WingCount(WingKind.Bomber) > wantB) RemoveWing(WingKind.Bomber);
        if (WingCount(WingKind.Bomber) != hadB) { StrikeTarget = null; _strikesOut = 0; }
        if (!Net.Sim) return;
        foreach (var w in _wings) if (w.Def.AmmoStat != null) w.Ammo = Math.Min(w.Ammo, (int)Stats[w.Def.AmmoStat]);
    }

    private void AddTurret(Vector2 offset, bool pd, Vector2 bears)
    {
        var t = new Turret { Bears = bears }; AddChild(t); t.Setup(this, offset, pd);
        _turrets.Add(t); if (pd) PdTurrets.Add(t); else _mains.Add(t);
    }

    private bool AddWing(WingKind k, int at)
    {
        if (GetParent() is not { } world) return false;
        var w = new Wing(); world.AddChild(w);
        w.Init(this, k, Position + new Vector2(GD.Randf() * 120 - 60, GD.Randf() * 120 - 60));
        _wings.Insert(at, w);
        return true;
    }
    private void RemoveWing(WingKind k) => DropWing(_wings.FindLastIndex(x => x.Kind == k));

    public void SetClass(ShipClass c) { Class = c; FitClass(); }

    // The pilot's purchased upgrades (Progression), and the highest level it has reached (its PEAK,
    // what the level walls read: Unlocks). The host needs both: it resolves hull and damage, and it
    // is held to the walls: a chip slot opens on the sheet the moment the peak reaches it (Loadout).
    // Either one changing refits the ship (Restat). A ship no identity has reached yet is a level-1
    // pilot's.
    private int[] _bought = new int[Progression.All.Length];
    public int[] Bought => _bought;
    public int Peak { get; private set; } = 1;
    public void SetProgress(int[] bought, int peak)
    {
        var b = new int[Progression.All.Length];
        for (int i = 0; i < b.Length && i < (bought?.Length ?? 0); i++) b[i] = Math.Clamp(bought[i], 0, Progression.MaxPerUpgrade);
        int pk = Progression.Claim(peak);
        if (b.AsSpan().SequenceEqual(_bought) && pk == Peak) return;
        _bought = b; Peak = pk;
        Restat();
    }

    // A REFIT: the sheet rebuilt from class, gear and purchases, and the wing brought to it. The
    // hull keeps its FRACTION: keeping the damage taken instead let a pilot heal by swapping a big
    // shield part on and off (10 of 615 became 316 of 375).
    private void Restat()
    {
        double frac = MaxHp > 0 ? Hp / MaxHp : 1;
        Stats = BuildSheet();
        MaxHp = Stats["hull"];
        if (Alive) Hp = Math.Max(1, frac * MaxHp);
        _hullWatch = default;                      // a refit, not damage
        FitWings(fresh: false);
    }

    // Equipment: the owner's saved loadout for this class, and the levels its pilot bought for its
    // parts (on the host, what the identity carried; your own, from Character -- copied, never
    // shared). A change to EITHER refits: a level bought for a part already fitted is the same
    // loadout, and must refit all the same.
    // WHAT IS FITTED IS KEPT WHOLE (_fitted: what this hull can wear); what FLIES is what the
    // pilot's peak has opened of it (Loadout), so a chip slot that opens mid-session applies the
    // chip already in it, with nothing sent again.
    private string[] _fitted;
    public string[] Loadout => Equipment.Sanitize(Class, _fitted, Peak);
    private Dictionary<string, int> _levels = new();
    public IReadOnlyDictionary<string, int> Levels => _levels;
    public void SetEquipment(string[] ids, IReadOnlyDictionary<string, int> levels)
    {
        var f = Equipment.Sanitize(Class, ids, Unlocks.Top);
        var lv = Equipment.SanitizeLevels(levels?.Select(kv => (kv.Key, kv.Value)));
        if (_fitted != null && f.AsSpan().SequenceEqual(_fitted)
            && lv.Count == _levels.Count && lv.All(kv => _levels.GetValueOrDefault(kv.Key) == kv.Value)) return;
        _fitted = f; _levels = lv;
        Restat();
    }

    public void SetIdentity(string pilot, Color main, Color accent, ShipClass cls)
    {
        Pilot = pilot; Main = main; Accent = accent;
        if (cls != Class) SetClass(cls);
        else if (_sprite != null) _sprite.Modulate = Main;
        if (IsInstanceValid(_shield)) _shield.Tint = Accent;
        foreach (var t in _turrets) t.Recolor();
    }

    // Bomber `w`'s bay on the deck, in the carrier's frame: bombers alternate port, starboard,
    // port... so the two sides of the runway always split them evenly (6 bombers -> 3 and 3; an
    // odd one goes to port), and each row is centred on BayY. A parked bomber faces the bow.
    public Vector2 Bay(Wing w)
    {
        int i = 0, n = 0;
        foreach (var x in _wings) { if (!x.Def.Parks) continue; if (x == w) i = n; n++; }
        bool port = i % 2 == 0;
        int onSide = port ? (n + 1) / 2 : n / 2, j = i / 2;
        var art = MyArt;
        return new Vector2(port ? -art.BayX : art.BayX, art.BayY + (j - (onSide - 1) / 2f) * art.BaySpacing);
    }

    public int WingCount(WingKind k) { int n = 0; foreach (var w in _wings) if (w.Kind == k) n++; return n; }
    private int Fitted { get { int n = 0; foreach (var w in _wings) if (!w.Def.Sortie) n++; return n; } }

    // A SORTIE (a Wings.All row with a LifeStat): the row's count of craft sent out for its LifeStat
    // seconds beside the fitted wing, never counted into it -- the patrol round this carrier, or
    // gunships at `given`. They ride at the END of the wing list, so the host's report lines the
    // fitted craft up on every peer as before. The host's to decide; a guest's copies come from the
    // report. Refused (0 sent): not the host, no wing, a wreck, or a given target beyond the row's
    // EngageStat (the gunships' 3000 u, checked here and only here).
    public int Sortie(WingKind k, IHittable given = null)
    {
        var row = Wings.Of(k);
        if (!Net.Sim || !Alive || !Stats.Def.Has(Fit.Wing) || !row.Sortie) return 0;
        if (given != null && !Wings.Within(this, row, given)) return 0;
        int n = (int)Stats[row.CountStat], sent = 0;
        for (int i = 0; i < n; i++)
            if (AddWing(k, _wings.Count)) { _wings[^1].SendOut(given, _clock + Stats[row.LifeStat]); sent++; }
        return sent;
    }
    public int BombersReady { get { int n = 0; foreach (var w in _wings) if (w.Kind == WingKind.Bomber && w.Armed) n++; return n; } }
    public double BomberRearmLeft { get { double m = 0; foreach (var w in _wings) if (w.Kind == WingKind.Bomber) m = Math.Max(m, w.RearmLeft); return m; } }

    // ── abilities ────────────────────────────────────────────────────────────
    // One entry point for every class action. The owner's key press calls
    // UseAbility; the host acts on it directly, a guest sends it as a request, and
    // the host checks the sender owns this ship first. A client says "I pressed
    // missile at target 1000", never "I did 5 damage". The broadside aims at the owner's
    // cursor, which reaches the host with the rest of its intent (AimPoint), not in the request.
    // A row that TAKES A POINT (AbilityDef.TakesPoint, F8) sends the cursor IN the request: where it
    // was when the key went down, not where the next report says it is. A row that TAKES TARGETS
    // (AbilityDef.TakesTargets) sends `picks`, the NetIds the pilot has picked; any other sends none.
    public void UseAbility(string id, int targetId, int[] picks = null)
    {
        var def = Abilities.Find(Class, id);
        if (def == null) return;                       // not an ability this class carries
        // BEHIND A LEVEL WALL (Unlocks): the slot says the level that opens it, and nothing is asked --
        // before a local press too, which the host would never see. The host holds the same wall.
        if (Unlocks.LockedAt(Class, Peak, def) is int at) { Fail(id, Unlocks.Locked(at)); return; }
        // A LOCAL ability is the owner's own intent (the fire mode): it rides in the state report
        // rather than being asked for.
        if (def.Local) { def.Press?.Invoke(this, null); return; }
        // Why it cannot be pressed, decided on the OWNER's machine so the slot says so at once.
        // The host checks again inside Press: this is a courtesy, never the guard.
        if (PressHeld(def)) { Fail(id, "DISABLED"); return; }
        if (def.Refuse?.Invoke(this, targetId != 0 ? Combat.ById(targetId) : null) is { } why) { Fail(id, why); return; }
        // A HOOK's own half, here on the owner: it flies the helm move itself (HookOwner) before the host is asked
        if (def.Hook != null && HookOwner(def, targetId != 0 ? Combat.ById(targetId) : null) is { } no) { Fail(id, no); return; }
        var point = def.TakesPoint ? AimPoint : Vector2.Zero;
        var ids = def.TakesTargets && picks != null ? picks : System.Array.Empty<int>();
        PressTarget = targetId != 0 ? Combat.ById(targetId) : null;
        if (Mine && Alive) def.AtOnce?.Invoke(this, point);
        if (Net.Sim) DoAbility(id, targetId, point, ids);
        else Net.AskHost(this, nameof(RequestAbility), id, targetId, point, ids);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestAbility(string id, int targetId, Vector2 at, int[] targets)
    {
        if (Net.FromPlayer(this, out int who) && who == OwnerId) DoAbility(id, targetId, at, targets);
    }

    // THE ACTIVE RELOAD'S TIMING PRESS (ActiveReload.Press): a guest's claim of how far through its
    // reload it was, as a share on its own clock. The host judges it, believed within the sender's
    // leeway only, and only from the ship's own pilot.
    public void AskReloadPress(string id, float claim) => Net.AskHost(this, nameof(RequestReloadPress), id, claim);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestReloadPress(string id, float claim)
    {
        if (Net.FromPlayer(this, out int who) && who == OwnerId && Abilities.Find(Class, id)?.Reload is { } r)
            ActiveReload.Judge(this, r, claim, Net.Leeway(who));
    }

    // THE GATE, not a courtesy. Refuse (above) runs on the owner so the slot says why at once;
    // this is what the host is held to. A wreck presses nothing but the one ability declared for
    // it (AbilityDef.WhenWrecked -- reboard). The nine newest abilities each repeated `!Net.Sim ||
    // !Alive` in their own body and the seven oldest never did, so a guest in stasis could fire a
    // missile burst, switch on point defence and order a bomber strike out of its own wreck.
    // An ability behind a level wall (Unlocks) is refused here too, for the peak this ship's pilot
    // announced: a guest that skips its own refusal still presses nothing its level has not opened.
    // WHAT A COOLDOWN REALLY IS, once the pilot's COOLING points are in it: the sheet's share of
    // the row's seconds, never below a floor. Every ability that sets a cooldown reads it here
    // rather than each one multiplying for itself.
    public const double CoolFloor = 0.5;              // half the row's seconds, whatever is bought
    public double Cooling(double seconds) => seconds * System.Math.Max(CoolFloor, Stats["cooldown_share"]);

    // `at`: the press's point (F8), written into the row's own slot for a row that takes one; a point
    // that is not a number (nothing an honest owner sends) presses nothing. `targets`: the pilot's
    // picks (F8), for a row that takes them: Picked holds them for its Press.
    private void DoAbility(string id, int targetId, Vector2 at, int[] targets)
    {
        var def = Abilities.Find(Class, id);
        if (def == null || !Net.Sim || (!Alive && !def.WhenWrecked) || PressHeld(def)
            || Unlocks.LockedAt(Class, Peak, def) != null) return;
        if (def.TakesPoint)
        {
            if (!float.IsFinite(at.X) || !float.IsFinite(at.Y)) return;
            Sl(id).At = at;
        }
        if (def.TakesTargets) Pick(targets);
        var target = targetId != 0 ? Combat.ById(targetId) : null;
        // ANY OTHER OF THE CLASS'S ABILITIES ends a running stance (the lunge and the whirlwind end the
        // prism's): read by the row's Stance, never by an id. The drive (V) and the weapon's own rows do not.
        if (def.Stance == null && !def.Weapon && Array.IndexOf(Classes.Of(Class).Abilities, def) >= 0
            && def.Refuse?.Invoke(this, target) == null)
            foreach (var st in Abilities.For(Class))
                if (st.Stance is { Keeps: false } && Sl(st.Id).Left > 0) EndStance(st.Id);
        def.Press?.Invoke(this, target);
    }

    // THE PICKS A PRESS CARRIED, as the host takes them: no more than the class may hold at once
    // (ClassDef.Targets -- a claim past it is cut, in the order sent), each once, and only what
    // Combat.ById finds alive (a dead or unknown id is dropped). A row that takes targets reads them here.
    private readonly List<IHittable> _picked = new();
    public IReadOnlyList<IHittable> Picked => _picked;
    private void Pick(int[] ids)
    {
        _picked.Clear();
        int cap = Classes.Known(Class) ? Classes.Of(Class).Targets : 1;
        foreach (int nid in (ids ?? System.Array.Empty<int>()).Distinct().Take(cap))
            if (Combat.ById(nid) is { Alive: true } h) _picked.Add(h);
    }

    // ── what the abilities do. The catalogue (Abilities.cs) points at these, and each one
    // guards itself: the press arrives from a guest's keyboard, so the host never trusts it.
    // DISABLED, a pilot presses nothing (the wreck's own reboard aside). Its point defence, its wing
    // and its turrets already out are not presses, and fight on.
    private bool PressHeld(AbilityDef def) => Disabled && !def.WhenWrecked;
    public void Reboard() { if (CanReboard) { Alive = true; Hp = MaxHp * ReboardHull; _stasis = 0; } }
    public void StartBroadside() { if (BroadsideReady) Sl("broadside").Left = Stats["broadside_windup"]; }
    public void OrderAttack(IHittable t) { if (Stats.Def.Has(Fit.Wing) && t != null) { WingTarget = t; _attacking = true; } }
    public void RecallWing() { WingTarget = null; _attacking = false; }
    // ── THE FREIGHTERS ──────────────────────────────────────────────────
    private Hub MyHub => GetParent() as Hub;

    // THE SENTRY THROW (F14). R with the cursor on open space throws one from the hull to the
    // cursor, clamped to deploy_reach on the same bearing; it lands deploy_flight later, and in
    // flight it is no body (nothing hits it, it fires at nothing). R with the cursor within
    // recall_pick of one of yours recalls it: never refused; it flies deploy_flight back (still out
    // while it flies) and its hull is stowed for the next throw when it lands. The host keeps the
    // throws and the recalls in flight, and their count in the deploy slot's N (the slots ride the
    // host report, so a guest's TurretsOut reads it); every peer sees the landing mark (Fx.AimZone).
    private readonly List<(Vector2 At, double Left, double Hp, double Max)> _throws = new();
    private readonly List<(double Left, double Hp, double Max)> _home = new();
    private readonly Stack<(double Hp, double Max)> _stowed = new();
    private void CountFlights() => Sl("deploy").N = _throws.Count + _home.Count;

    // How many of this pilot's turrets are standing or in flight (thrown, or recalled and flying home).
    public int TurretsOut
    {
        get
        {
            var h = MyHub; if (h == null) return 0;
            int n = Net.Sim ? _throws.Count + _home.Count : Sl("deploy").N;
            foreach (var t in h.Deployed) if (t.OwnerId == OwnerId) n++;
            return n;
        }
    }
    // The own turret an R at `cursor` recalls: the nearest within recall_pick, or none.
    public DeployedTurret RecallAt(Vector2 cursor)
    {
        var h = MyHub; if (h == null) return null;
        DeployedTurret best = null; float bd = (float)Stats["recall_pick"];
        foreach (var t in h.Deployed)
        {
            if (t.OwnerId != OwnerId) continue;
            float d = cursor.DistanceTo(t.Position);
            if (d <= bd) { bd = d; best = t; }
        }
        return best;
    }
    // Where a throw at `cursor` lands.
    public Vector2 ThrowPoint(Vector2 cursor) => Position + (cursor - Position).LimitLength((float)Stats["deploy_reach"]);
    public void DeployTurret(Vector2 cursor)
    {
        if (!Stats.Def.Has(Fit.Deploy)) return;
        if (RecallAt(cursor) is { } mine)
        {
            _home.Add((Stats["deploy_flight"], mine.Hp, mine.MaxHp));
            MyHub?.DeployedTaken(mine);
            CountFlights();
            return;
        }
        if (Sl("deploy").Cool > 0 || TurretsOut >= (int)Stats["deploy_max"]) return;
        var at = ThrowPoint(cursor);
        double flight = Stats["deploy_flight"];
        var (hp, max) = _stowed.Count > 0 ? _stowed.Pop() : (Stats["deploy_hull"], Stats["deploy_hull"]);
        _throws.Add((at, flight, hp, max));
        CountFlights();
        Fx.Warn(new FxRaise { Id = Fx.AimZone, At = at, To = at, Size = DeployedTurret.Radius, Time = flight });
        Sl("deploy").Cool = Cooling(Stats["deploy_cooldown"]);
    }
    // REDEPLOY (kits6b-J3, D41), on the host: every LANDED sentry of this pilot's folds (it leaves the
    // world at once, no burst) and flies redeploy_flight to a ring redeploy_ring round the hull as it
    // stands now, 2 pi / deploy_max apart from the bow (120 deg for three), each with the hull it had.
    // In flight it is a throw like any other: no body, counted as out, marked where it lands.
    public List<DeployedTurret> OwnLanded()
    {
        var mine = new List<DeployedTurret>();
        if (MyHub is { } h) foreach (var t in h.Deployed) if (t.OwnerId == OwnerId) mine.Add(t);
        return mine;
    }
    public void Redeploy()
    {
        var mine = OwnLanded();
        if (Sl("redeploy").Cool > 0 || mine.Count == 0) return;
        Sl("redeploy").Cool = Cooling(Stats["redeploy_cooldown"]);
        double flight = Stats["redeploy_flight"];
        float ring = (float)Stats["redeploy_ring"], step = Mathf.Tau / Math.Max(1, (int)Stats["deploy_max"]);
        var bow = Vector2.Up.Rotated(Rotation);
        for (int i = 0; i < mine.Count; i++)
        {
            var t = mine[i];
            var at = Position + bow.Rotated(step * i) * ring;
            _throws.Add((at, flight, t.Hp, t.MaxHp));
            MyHub?.DeployedTaken(t);
            Fx.Warn(new FxRaise { Id = Fx.AimZone, At = at, To = at, Size = DeployedTurret.Radius, Time = flight });
        }
        CountFlights();
    }
    // host, each frame: a throw whose flight is over lands as a turret; a recall whose flight is
    // over is stowed for the next throw
    private void TickThrows(double delta)
    {
        for (int i = _throws.Count - 1; i >= 0; i--)
        {
            var th = _throws[i];
            th.Left -= delta;
            if (th.Left > 0) { _throws[i] = th; continue; }
            _throws.RemoveAt(i);
            MyHub?.Drop(this, th.At, th.Hp, th.Max);
        }
        for (int i = _home.Count - 1; i >= 0; i--)
        {
            var hm = _home[i];
            hm.Left -= delta;
            if (hm.Left > 0) { _home[i] = hm; continue; }
            _home.RemoveAt(i);
            _stowed.Push((hm.Hp, hm.Max));
        }
        CountFlights();
    }

    // The bubble is a POOL IN THE WORLD, not a flag on one hull: it covers whoever is inside it
    // when a blow lands, and everyone inside spends the same pool.
    public bool BubbleUp => Sl("bubble").Left > 0 && Sl("bubble").Own > 0;
    public float BubbleRadius => (float)Stats["bubble_radius"];
    public double BubbleLeft => Sl("bubble").Own;
    public void RaiseBubble()
    {
        if (Sl("bubble").Cool > 0) return;
        ref var b = ref Sl("bubble");
        b.Left = Stats["bubble_time"]; b.Own = Stats["bubble_pool"]; b.Cool = Cooling(Stats["bubble_cooldown"]);
        b.N = (int)Stats["bubble_pool"];
    }
    private double SpendBubble(double d)
    {
        ref var b = ref Sl("bubble");
        double take = Math.Min(b.Own, d);
        b.Own -= take; b.N = (int)b.Own;
        return d - take;
    }
    // Every bubble that covers `at` spends itself on this blow before the hull there sees it: EVERY
    // friendly hull's door calls it (kits6b-J3) -- a pilot's (Guarded), a sentry's and a fleet craft's
    // (DeployedTurret / UtilityShip.TakeDamage) -- so the bubble is a place, not a list of kinds.
    public static double ThroughBubbles(Vector2 at, double d)
    {
        foreach (var h in Combat.Players)
            if (d > 0 && h is PlayerShip p && p.BubbleUp && at.DistanceTo(p.Position) <= p.BubbleRadius)
                d = p.SpendBubble(d);
        return d;
    }

    // A TIMED ROW'S PRESS (AbilityDef.Time), on the host: it runs its Time, its Cooldown from the press, and
    // hardens the hull at its Guard share for that time. The row's Refuse holds a press while it cools (or runs).
    // A Swing row's first blow lands at once; a Sheds row lets go every web and takes none for its Time (the
    // whirlwind); a Call row calls the raiders round it onto this ship for its Time (the Taunt).
    public void RunFor(string id)
    {
        var def = Abilities.Find(Class, id);
        ref var sl = ref Sl(id);
        if (!Net.Sim || def?.Time == null || sl.Cool > 0) return;
        Engage(def, Stats[def.Time]);
        if (def.Swing != null) sl.Own = 0;
        if (def.Guard != null) _status.Apply(Status.Hardened, sl.Left, Stats[def.Guard]);
        if (def.Sheds) { LetGoWebs(); _status.Apply(Status.Unwebbed, sl.Left); }
        if (def.Call is { } call && MyHub is { } hub) CallIn(hub, call, sl.Left);
    }
    // A CALL (AbilityDef.Call), on the host: every raider squad with a member within the spec's reach, or hunting a
    // target within it, is called onto this ship for `secs` (Raider.Call: a boss, a missile or a practice craft never --
    // Targeting.Raiding); the reach flashes for every peer. The x taunt_mult is Outgoing's; the row Draws while it runs.
    private void CallIn(Hub hub, CallSpec call, double secs)
    {
        float reach = (float)Stats[call.Reach];
        Fx.Raise(call.Ring, Position, reach);
        foreach (var r in hub.Raiders.ToList())
            if (Targeting.Raiding.Hits(r) && (r.Position.DistanceTo(Position) <= reach || (r.Target is { } t && t.Position.DistanceTo(Position) <= reach)))
                r.Call(this, secs);
    }
    // A STATUS THIS SHIP PUTS ON A HOSTILE IT HIT (a row's OnDealt, on the host): for the stat's seconds, through the
    // hostile's own ApplyStatus (its Reaches decides whether the status can hold it at all), with the row's mark raised
    // on the hull: at most one raise every Remark on one hull (_marked: this ship's clock of its last raise of that mark
    // there), each living the status's time left plus Remark -- so under a stream of shells the mark is raised again
    // every half second and outlasts the status by under half a second, not one raise a shell. Suppressing fire's chevron.
    public const double Remark = 0.5;
    private readonly Dictionary<(int id, int mark), double> _marked = new();
    public void Afflict(IHittable target, Status st, string timeStat, int mark)
    {
        if (!Net.Sim || target is not IStatused held || !target.Alive) return;
        held.ApplyStatus(st, Stats[timeStat]);
        if (!held.Statuses.Has(st)) return;
        var key = (target.NetId, mark);
        if (_marked.TryGetValue(key, out var at) && _clock - at < Remark) return;
        foreach (var old in _marked.Where(m => _clock - m.Value >= Remark).Select(m => m.Key).ToList()) _marked.Remove(old);
        _marked[key] = _clock;
        Fx.Mark(mark, target, held.Statuses.Left(st) + Remark);
    }
    // A row's time and its cooldown set at once: from the press, or after the time when it CoolAfter.
    private void Engage(AbilityDef def, double secs)
    {
        ref var sl = ref Sl(def.Id);
        sl.Left = secs;
        if (def.Cooldown != null) sl.Cool = (def.CoolAfter ? secs : 0) + Cooling(Stats[def.Cooldown]);
    }
    // A SORTIE ROW'S PRESS (AbilityDef.Sends), on the host: the craft go out (Sortie: the host's leash on each target),
    // and only if one went does the row run the wing's LifeStat and start its cooldown.
    public void Launch(string id, IHittable selected)
    {
        var def = Abilities.Find(Class, id);
        ref var sl = ref Sl(id);
        if (!Net.Sim || def?.Sends is not { } kind || sl.Cool > 0 || sl.Left > 0) return;
        var row = Wings.Of(kind);
        int sent = 0;
        if (row.ToHull) sent = Sortie(kind);
        else foreach (var t in _picked.Count > 0 ? _picked.ToList() : selected != null ? new List<IHittable> { selected } : new List<IHittable>())
            sent += Sortie(kind, t);
        if (sent > 0) Engage(def, Stats[row.LifeStat]);
    }

    // A TIMED ROW'S PRESS (host): its Left from `time`, its cooldown from `cool`, both from the press. The sprint.
    public void Run(string id, string time, string cool)
    {
        ref var sl = ref Sl(id);
        if (!Net.Sim || !Alive || sl.Left > 0 || sl.Cool > 0) return;
        sl.Left = Stats[time]; sl.Cool = Cooling(Stats[cool]);
    }

    // WHILE A FORCING ROW RUNS (AbilityDef.Forces) the throttle is held open (LocalFlight): the sprint. Forcing is
    // that row, null when none runs.
    public AbilityDef Forcing
    {
        get
        {
            foreach (var def in Abilities.For(Class)) if (def.Forces && Sl(def.Id).Left > 0) return def;
            return null;
        }
    }
    public bool Forced => Forcing != null;

    // THE TOP SPEED `def`'s run gave it, the moment the run is over: TopNow with the row's own flat add still in
    // (its Left is already 0 when its Expire runs). What a parting round is priced at.
    public float TopOf(AbilityDef def) => TopSpeed((float)Stats["max_speed"], SpeedMult, SpeedAdds() + (def.SpeedAdd != null ? Stats[def.SpeedAdd] : 0), 1f);

    // A PARTING ROUND (AbilityDef.Parting, host, from the row's Expire): one round of the row's own spec down the nose,
    // priced at the top its run gave. A wreck fires nothing.
    public void Part(string id)
    {
        if (!Alive || Abilities.Find(Class, id) is not { Parting: { } row } def) return;
        Bores.Launch(this, row, 0, Bores.DamageAt(this, row, TopOf(def)), id);
    }

    // ITS RECOIL (the owner, LocalFlight): on the owner's own FALLING EDGE of a forcing row -- the helm frame its copy
    // of the row stops running, whether its own countdown got there or the host's packet said so first -- the hull
    // keeps the row's Parting Recoil share of its speed. The helm runs before TickAbilities, so on the host the round
    // (Expire, the frame before) has already left with the speed the run ended at. The row forcing last helm frame:
    private AbilityDef _forcedBy;

    // FLAT OUT AND KEEPING UP (a Ramp row's Condition, the Ramjet): the keel speed within `share` of the current top,
    // at full throttle. The owner reads its own throttle; the HOST'S copy of a guest's ship has no throttle to read, only
    // the reported velocity, so it asks the speed alone (DL9: a hull that lets go drops out of the 5% in a moment).
    public bool FullAhead(float share)
        => (!Mine || _throttle >= 1f) && (Mine ? Velocity : _netVel).Dot(Vector2.Up.Rotated(Mine ? Rotation : _netRot)) >= share * TopNow;
    private float _throttle;

    // THE HOST'S OWN COPY OF A GUEST'S RAMP (v3 §3.6 Authority), stepped once a report, over the time since the last:
    // the yaw from the two reported headings, clamped to the hull's turn rate (a report cannot claim a sharper turn),
    // and none across a snap (SkipYawFor: the Slingshot's heading is a snap, not a turn; every report inside the window,
    // which spans the reliable press and the unreliable report racing it). The host prices only from this.
    private void RampsFromReport(float rot, float age)
    {
        float yaw = age > 0 ? Mathf.AngleDifference(_rampRot, rot) / age : 0f;
        if (_clock < _skipYawUntil) yaw = 0f;
        _rampRot = rot;
        double turnRate = Stats["turn_rate"], dt = Math.Min(age, 0.5f);
        double share = turnRate > 0 ? Math.Min(1, Math.Abs(yaw) / turnRate) : 0;
        foreach (var def in Abilities.For(Class))
        {
            if (def.Ramp is not { } ramp) continue;
            ref var sl = ref Sl(def.Id);
            bool holding = sl.Left > 0;
            sl.Own = RampSpec.Step(sl.Own, Stats[ramp.Build], Stats[ramp.Cap], Stats[ramp.Bleed],
                                   holding, holding && (ramp.Condition == null || ramp.Condition(this)), share, dt);
        }
    }
    private float _rampRot;
    // the reports inside the next `secs` carry a snap, not a turn (set on the host by a row that snaps the heading)
    private double _skipYawUntil;
    public void SkipYawFor(double secs) => _skipYawUntil = _clock + secs;

    // THE SNAP (the owner, AbilityDef.AtOnce): the nose and the whole velocity onto the bearing to `at`, the speed kept.
    // The heading is SET, not turned: no yaw, so a Ramp keeps its total (the host's copy skips the report, Slung).
    public void Snap(Vector2 at)
    {
        if (at.DistanceTo(Position) < 1f) return;
        var (rot, v) = Snapped(Position, Velocity, at);
        Rotation = rot; Velocity = v; _yawRate = 0f;
    }
    // pure: the heading whose nose points from `from` at `at`, and `v`'s size along it
    public static (float rot, Vector2 v) Snapped(Vector2 from, Vector2 v, Vector2 at)
    {
        var dir = (at - from).Normalized();
        return (dir.Angle() + Mathf.Pi / 2, dir * v.Length());
    }
    // ITS PRESS ON THE HOST: the cooldown, and the host's copy of a guest's ship told the next report is a snap
    public void Slung(string id, string cool)
    {
        ref var sl = ref Sl(id);
        if (!Net.Sim || !Alive || sl.Cool > 0) return;
        sl.Cool = Cooling(Stats[cool]);
        if (!Mine) SkipYawFor(0.4);
    }

    // SLIPSTREAM (a stat row, the Dart's passive): a hull whose sheet names slip_speed takes slip_guard of every blow
    // while its ACTUAL speed is at or past it; a sheet without it (every other class) reads 0 and takes x1. On the host a
    // guest's speed is its report's, held to SlipSpeed's ceiling of the host's own lifts.
    public double SlipShare()
    {
        double at = Stats["slip_speed"];
        if (at <= 0) return 1;
        float speed = Mine ? Velocity.Length() : SlipSpeed(_netVel, TopNow, StrafeNow);
        return speed >= at ? Stats["slip_guard"] : 1;
    }
    // pure: a reported speed held to hypot(top, slide) x 1.1 -- the fastest the hull can honestly go, a margin over
    public static float SlipSpeed(Vector2 reported, float top, float strafe) => Mathf.Min(reported.Length(), Mathf.Sqrt(top * top + strafe * strafe) * 1.1f);

    public void StartOverdrive()
    {
        if (Sl("overdrive").Cool > 0) return;
        ref var o = ref Sl("overdrive");
        o.Left = Stats["overdrive_time"]; o.Cool = Cooling(Stats["overdrive_cooldown"]);
    }

    // A COOLDOWN RUN DOWN BY `secs`: the clock's own step (TickAbilities, every frame) and a Resupply's cut (D45) are
    // this one rule -- a CHARGED row (AbilityDef.Charges) gets a charge back when it reaches 0, and the next starts if
    // any is still out.
    private void CoolBy(AbilityDef def, ref Slot sl, double secs)
    {
        sl.Cool = Math.Max(0, sl.Cool - secs);
        if (sl.Cool <= 0 && def.Charges != null && sl.N > 0 && --sl.N > 0) sl.Cool = Cooling(Stats[def.Recharge]);
    }

    // THE PILOTS A FIELD'S PRESS REACHES (AbilityDef.Aura): every live pilot within its radius of this hull, this one
    // included.
    public IEnumerable<PlayerShip> InAura(AbilityDef def)
    {
        if (def?.Aura == null) yield break;
        float r = (float)Stats[def.Aura];
        foreach (var h in Combat.Players)
            if (h is PlayerShip p && p.Alive && (ReferenceEquals(p, this) || p.Position.DistanceTo(Position) <= r)) yield return p;
    }
    // WHAT A CUT WOULD TAKE: the ability rows still cooling on the pilots in `id`'s aura (their ClassDef.Abilities,
    // never the drive, never a row of the id itself). On every peer: the slots are the host's report.
    public int CoolingInAura(string id)
    {
        int n = 0;
        foreach (var p in InAura(Abilities.Find(Class, id)))
            foreach (var def in Classes.Of(p.Class).Abilities)
                if (def.Id != id && p.Sl(def.Id).Cool > 0) n++;
        return n;
    }
    // RESUPPLY (D45), on the host: the row's Cuts seconds off every cooldown CoolingInAura counts, through CoolBy, then
    // its own cooldown -- or, nothing cooling, nothing at all (refused, and free). Returns how many it cut.
    public int Resupply(string id)
    {
        var def = Abilities.Find(Class, id);
        if (def?.Cuts == null || !Net.Sim || Sl(id).Cool > 0 || CoolingInAura(id) == 0) return 0;
        double secs = Stats[def.Cuts];
        int n = 0;
        foreach (var p in InAura(def).ToList())
            foreach (var row in Classes.Of(p.Class).Abilities)
            {
                if (row.Id == id) continue;
                ref var sl = ref p.Sl(row.Id);
                if (sl.Cool <= 0) continue;
                p.CoolBy(row, ref sl, secs);
                n++;
            }
        Spend(def);
        return n;
    }

    // THE REPAIR FIELD (kits6b-J8), on the host: up for repair_time, its cooldown set; every frame it runs (its row's
    // Tick) every friendly hull within field_radius of this one -- this one too -- is mended repair_share of its
    // MAXIMUM a second (Mend.Give, credited "repair"). A wreck or a craft lost is no friendly hull (Mend).
    public void StartRepair()
    {
        ref var r = ref Sl("repair");
        if (r.Cool > 0) return;
        r.Left = Stats["repair_time"]; r.Cool = Cooling(Stats["repair_cooldown"]);
    }
    public void RepairTick(double dt)
    {
        float reach = (float)Stats["field_radius"];
        double share = Stats["repair_share"] * dt;
        foreach (var m in Mend.Friendlies(MyHub).ToList())
            if (m.Position.DistanceTo(Position) <= reach) Mend.Give(m, share * m.HullMax, this, "repair");
    }

    // TIME ON TARGET (D36), on the host: every gun of this pilot's that can reach the paint lands one
    // Lines.Tot line on it in this one tick -- the spotter (the main barrel's tip toward the paint) and
    // each LANDED sentry (Hub.Deployed; a throw in flight is no gun) within tot_reach of it. Each line
    // ends at the painted target and lands tot_damage on every hostile on it (Lines: Hostiles only).
    // Returns how many lines it threw.
    public int TimeOnTarget()
    {
        if (Sl("tot").Cool > 0 || Painted is not { } t) return 0;
        Sl("tot").Cool = Cooling(Stats["tot_cooldown"]);
        float reach = (float)Stats["tot_reach"];
        var to = t.Position;
        var muzzles = new List<Vector2>();
        if (_mains.Count > 0)
        {
            var m = _mains[0].GlobalPosition;
            muzzles.Add(m + (to - m).Normalized() * Spec(false).Barrel);
        }
        if (MyHub is { } h)
            foreach (var d in h.Deployed)
                if (d.OwnerId == OwnerId) muzzles.Add(d.GlobalPosition);
        int n = 0;
        foreach (var from in muzzles)
        {
            if (from.DistanceTo(to) > reach) continue;
            Lines.Strike(Lines.Tot, this, from, to, Stats["tot_damage"]);
            n++;
        }
        return n;
    }

    // Everything within reach is thrown clear -- and what cannot be thrown (a boss, a structure, a
    // practice dummy: Targeting.Immovable) is HELD still instead, wave_disable, never moved.
    public void Shockwave()
    {
        if (Sl("shockwave").Cool > 0) return;
        Sl("shockwave").Cool = Cooling(Stats["wave_cooldown"]);
        float reach = (float)Stats["wave_range"], push = (float)Stats["wave_push"];
        // Targeting.Shaken, not the raw list: nothing in flight is reached, a missile or a body
        // with a hull of its own. THROWING one was worse than hitting it -- the host moved a live
        // hostile seeker 1000 u while every guest flew its own copy along the old path, so the two
        // peers held a damaging missile a thousand units apart. What holds a spot is held, not moved:
        // every peer builds it where it stands and never hears of a move (audit P5).
        foreach (var h in new List<IHittable>(Targeting.Hittable(Combat.Hostiles, Targeting.Shaken)))
        {
            if (h.Position.DistanceTo(Position) > reach) continue;
            if (Targeting.Immovable.Hits(h)) { (h as IStatused)?.ApplyStatus(Status.Disabled, Stats["wave_disable"]); continue; }
            if (Targeting.Throwable.Hits(h) && h is Node2D n)
            {
                var away = n.Position - Position;
                n.Position += (away.LengthSquared() > 1f ? away.Normalized() : Vector2.Up) * push;
            }
        }
        Fx.Raise(Fx.Wave, Position, reach);                 // the ring, on every peer: how far it threw them
    }

    // THE BUNKER BUSTER (the Bastion's F): one slow heavy round from the main barrel toward the cursor,
    // buster_damage, stopping on the first body. What it does to a boss, a structure or a shield is its
    // Shots row's (Shots.Buster: Versus, Through), so the rule is data and any hull firing it keeps it.
    public void FireBuster()
    {
        if (Sl("buster").Cool > 0) return;
        Sl("buster").Cool = Cooling(Stats["buster_cooldown"]);
        var from = _mains.Count > 0 ? _mains[0].GlobalPosition : Position;
        var off = Sl("buster").At - from;
        var dir = off.LengthSquared() > 1f ? off.Normalized() : Vector2.Up.Rotated(Rotation);
        Combat.Fire(Shots.Buster, from + dir * Spec(false).Barrel, dir, (float)Stats["buster_speed"], (float)Stats["buster_range"],
                    Stats["buster_damage"], source: this, hitSource: "buster", size: 1.8f);
    }

    // ── THE HEAVY FIGHTERS ──────────────────────────────────────────────
    // THE RAILGUN'S SHOT (its row's Loose, on the host, when a charge stroke is let go on a seated
    // round): the band the charge reached (Charges.Of: rail_tap at once, ramping to the whole at full)
    // times the round in the chamber (ActiveReload.Take: x rail_perfect enhanced, drawn and heard
    // white) of rail_damage, from the nose along the heading as far as rail_range (x ReachMult: the
    // anchor's x1.4); then the chamber is spent and reloads by itself.
    public void FireRail(double share)
    {
        var r = ActiveReload.Rail;
        var (mult, line) = Charges.At(Charges.Of(r.Id), share, id => Stats[id]);
        double round = ActiveReload.Take(this, r);
        if (round > 1) line = Lines.RailEnhanced;
        var nose = Aim.Nose(this, MyArt.Length * 0.5f);
        Lines.Strike(line, this, nose, nose + Vector2.Up.Rotated(Rotation) * (float)Stats["rail_range"] * ReachMult, Stats["rail_damage"] * mult * round);
        ActiveReload.Spent(this, r);
    }

    // Six missiles, each on a target of its own while there are targets to go round, in the cell's PREY ORDER
    // (kits_v2's Warden card): whatever has a web on a friendly hull first (ISquadMember.Latched -- a raider
    // latches on nothing else), then the nearest; what is left over goes round again from the first.
    // WHAT THE CELL CAN SEE, in one place, because the slot has to say "NOTHING IN REACH" before
    // the press and the press has to find the same things afterwards. Targeting.Attackable, not
    // WingPrey: a seeker hits once and dies, so a practice dummy is a perfectly good target for it.
    public List<IHittable> SeekerPrey()
    {
        float range = (float)Stats["hunter_range"];
        var seen = new List<IHittable>();
        foreach (var h in Targeting.Choosable(Combat.Hostiles, Targeting.Attackable))
            if (Position.DistanceTo(h.Position) <= range) seen.Add(h);
        return seen.OrderBy(h => h is ISquadMember { Latched: true } ? 0 : 1).ThenBy(h => Position.DistanceTo(h.Position)).ToList();
    }

    public void LaunchHunters()
    {
        if (Sl("hunters").Cool > 0) return;
        int n = (int)Stats["hunter_count"];
        float range = (float)Stats["hunter_range"];
        var seen = SeekerPrey();
        // NOTHING IN REACH SPENDS NOTHING. The cooldown used to be taken on the line above this
        // search, so a press with an empty sky burned all 14 s and fired nothing -- which is
        // indistinguishable, from the cockpit, from the ability being broken.
        if (seen.Count == 0) return;
        Sl("hunters").Cool = Cooling(Stats["hunter_cooldown"]);
        var nose = Aim.Nose(this, MyArt.Length * 0.5f);
        for (int i = 0; i < n; i++)
        {
            var t = seen[i % seen.Count];
            float side = (i % 2 == 0 ? -1 : 1) * (12f + 8f * (i / 2));
            var dir = (t.Position - nose).Rotated(Mathf.DegToRad(side));
            Combat.LaunchTorpedo(nose, dir, (float)Stats["hunter_speed"], range * 1.6f, Stats["hunter_damage"],
                                 t.NetId, (float)Stats["hunter_turn"], hostile: false, source: this);
        }
    }

    // ── THE LIGHTS ──────────────────────────────────────────────────────
    // The reverb remembers what this ship dealt, through its own row's OnDealt (AbilityDef.OnDealt,
    // called by NoteDealt while Sl("reverb").Left > 0), and puts reverb_share of it down at once, where
    // the last of it landed (Slot.At). Its guns run at reverb_rate meanwhile (the row's RateStat).
    public void StartReverb()
    {
        if (Sl("reverb").Cool > 0) return;
        ref var e = ref Sl("reverb");
        e.Left = Stats["reverb_time"]; e.Own = 0; e.Cool = Cooling(Stats["reverb_cooldown"]); e.At = Position;
    }
    // The reverb's time is up (the Reverb row's Expire, on the host; Left is already 0 here, so this
    // blast does not re-store itself through NoteDealt). What it remembered is its OWN slot's Own
    // and At, so the row hands it nothing but the ship and no number travels through the tick.
    public void Detonate()
    {
        ref var e = ref Sl("reverb");
        double stored = e.Own; e.Own = 0; var at = e.At;
        if (stored <= 0) return;
        double blast = stored * Stats["reverb_share"];
        float reach = (float)Stats["reverb_radius"];
        NoteCombat();                                       // detonating is combat, whether or not it lands
        foreach (var h in new List<IHittable>(Targeting.Hittable(Combat.Hostiles, Targeting.Attackable)))
        {
            if (h.Position.DistanceTo(at) > reach) continue;
            Dealt.Deal(h, blast, this, Dealt.Reverb);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
        Fx.Raise(Fx.Reverb, at, reach);                     // on every peer, where it remembered
    }

    // THE REWIND (the Echo's Q, kits_v2's card + the README ruling). ON THE OWNER (AbilityDef.AtOnce: flight is the
    // owner's): position, heading and velocity back to its trail's mark nearest rewind_back ago, the speed held to
    // the top it has now. The boost's time left is not the trail's and is not rewound.
    private readonly Trail _trail = new();
    public Trail Past => _trail;                     // what the checks read and clear
    public void Rewind()
    {
        if (_trail.Back(_clock, Stats["rewind_back"]) is not { } m) return;
        float top = TopNow;
        Position = m.At; Rotation = m.Heading; _yawRate = 0f;
        Velocity = m.Velocity.Length() > top ? m.Velocity.Normalized() * top : m.Velocity;
    }
    // ITS PRESS ON THE HOST: the cooldown; the hull back to the HOST'S OWN mark (hull is host state: a guest's report
    // never carries it), never above the hull it has room for, never a wreck raised (DoAbility refuses one); every web
    // let go.
    public void Rewound()
    {
        ref var sl = ref Sl("rewind");
        if (!Net.Sim || !Alive || sl.Cool > 0) return;
        sl.Cool = Cooling(Stats["rewind_cooldown"]);
        if (_trail.Back(_clock, Stats["rewind_back"]) is { } m) Hp = Math.Min(MaxHp, m.Hull);
        LetGoWebs();
    }

    // THE EMP'S PRESS (host): its cooldown, the first pulse where it stands, and the second's point and time (the row's
    // Expire fires it from Slot.At once emp_echo has run).
    public void StartEmp()
    {
        ref var sl = ref Sl("emp");
        if (!Net.Sim || !Alive || sl.Cool > 0) return;
        sl.Cool = Cooling(Stats["emp_cooldown"]); sl.At = Position; sl.Left = Stats["emp_echo"];
        Pulse(Position);
    }
    // ONE PULSE (host): every hostile craft within emp_range of `at` jammed for emp_jam (Targeting.Jammable; a status
    // an OutGuards row spares is never put on what it spares). No damage.
    public void Pulse(Vector2 at)
    {
        float reach = (float)Stats["emp_range"];
        foreach (var h in new List<IHittable>(Targeting.Hittable(Combat.Hostiles, Targeting.Jammable)))
            if (h.Position.DistanceTo(at) <= reach && h is IStatused st) st.ApplyStatus(Status.Jammed, Stats["emp_jam"]);
        NoteCombat();
        Fx.Raise(Fx.Emp, at, reach);
    }

    // THE VEIL (the Wraith's F, DL3; host): Untargetable for veil_time (its SpeedStat lifts the top while it runs), and the
    // next main volley primed at veil_break (Prime). Firing ends it (Ab.Veil's OnFire): Unveil.
    public void Veil()
    {
        ref var s = ref Sl("veil");
        if (s.Cool > 0) return;
        s.Left = Stats["veil_time"]; s.Cool = Cooling(Stats["veil_cooldown"]);
        ApplyStatus(Status.Untargetable, s.Left);
        Prime(Stats["veil_break"]);
    }
    public void Unveil() { Sl("veil").Left = 0; _status.Clear(Status.Untargetable); }
    // THE NEXT MAIN VOLLEY'S MULTIPLE (host): FireControl spends it on the first volley that leaves, then it is 1 again.
    public void Prime(double mult) => _primed = mult;
    public double Primed => _primed;
    public const float VeiledAlpha = 0.35f;

    // VENOM (the Wraith's Q, DL3; host): coated for venom_time, cooling venom_cooldown; while it runs its OnDealt doses
    public void Coat()
    {
        ref var s = ref Sl("venom");
        if (s.Cool > 0) return;
        s.Left = Stats["venom_time"]; s.Cool = Cooling(Stats["venom_cooldown"]);
    }
    // A STACK OF A DOSE ROW on what one of this ship's PRIMARY rounds just hit (Items.PrimaryShots); never on a round in
    // flight (a missile, a cruise missile's hull). Host: Doses ticks it.
    public void Dose(DoseDef def, IHittable t, string weapon)
    {
        if (!Net.Sim || Array.IndexOf(Items.PrimaryShots, weapon) < 0 || TagExt.Is(t, Tag.Missile | Tag.Hulled)) return;
        _doses.Add(def, t, _clock, Stats);
    }
    public int DosedOn(IHittable t, DoseDef def) => _doses.StacksOn(t, def);
    private readonly Doses _doses = new();

    // THE TARGET THE LAST PRESS NAMED (UseAbility's targetId), for an AtOnce row that needs it: the Shadow step.
    public IHittable PressTarget { get; private set; }
    // SHADOW STEP (the Wraith's E, DL3). Pure: the spot `behind` u behind `t` -- its tail, or, for a thing with no heading,
    // the far side from `from` -- pushed on along that line until clear of its hull, so owner, host and checks agree.
    public static Vector2 StepSpot(Vector2 from, IHittable t, float behind)
    {
        var dir = t.Facing is { } nose ? -nose : (t.Position - from).Normalized();
        if (dir.LengthSquared() < 1e-6f) dir = Vector2.Down;
        var spot = t.Position + dir * behind;
        for (int i = 0; i < 40 && t.Covers(spot, 20f); i++) spot += dir * 20f;
        return spot;
    }
    public static bool Steppable(IHittable t) => t != null && Combat.Hostiles.Contains(t) && t.Alive && !TagExt.Is(t, Tag.Missile);
    // the OWNER's blink: onto the spot, nose on it, the speed it had along the new nose
    public void Step(IHittable t)
    {
        if (!Steppable(t)) return;
        var spot = StepSpot(Position, t, (float)Stats["step_behind"]);
        float speed = Velocity.Length();
        Position = spot; Rotation = Aim.Face(spot, t.Position); _yawRate = 0f;
        Velocity = Vector2.Up.Rotated(Rotation) * speed;
    }
    // its press on the HOST: the cooldown, the spot noted, the host's ramjet-style copies told a snap came (SkipYawFor), every
    // web let go. The reach is judged with 1.25x slack: a guest's position here is a report behind its own.
    public void Stepped(IHittable t)
    {
        ref var sl = ref Sl("step");
        if (!Net.Sim || !Alive || sl.Cool > 0 || !Steppable(t) || Position.DistanceTo(t.Position) > Stats["step_reach"] * 1.25) return;
        sl.Cool = Cooling(Stats["step_cooldown"]); sl.At = StepSpot(Position, t, (float)Stats["step_behind"]);
        if (!Mine) SkipYawFor(0.4);
        LetGoWebs();
    }
    private double _primed = 1;

    public void OrderStrike(IHittable t)
    {
        // within the strike range (twice the fighters' control range) only
        if (!Stats.Def.Has(Fit.Wing) || t == null || StrikeTarget != null || _strikesOut > 0 || BombersReady <= 0
            || Position.DistanceTo(t.Position) > Stats["strike_range"]) return;
        // the bombers armed now are the strike: each is called, and answers once
        StrikeTarget = t; _strikesOut = 0;
        foreach (var w in _wings) if (w.Def.Parks && w.Armed) { w.Call(); _strikesOut++; }
    }

    // ── THE DRIVE (V) -- the class's own row (Drives.cs) ───────────────────────────────────
    // V presses the drive this hull names (ClassDef.Drive): the capitals hold it to warp, the nine
    // tap it to boost. The drive's slot is in Abilities.For, so its cooldown and its time are on the
    // wire like any ability's; the warp's charge is the owner's (it flies), in _drive beside it.
    private readonly DriveRun _drive = new();
    public DriveDef Drive => Drives.Of(Class);
    // A HELM MOVE (F8, HelmMoves.cs): flight the owner flies under a move's law instead of the helm,
    // started by an ability's press on the owner and marked by the host.
    private readonly HelmRun _helm = new();
    public HelmRun Helm => _helm;
    public string BeginHelm(HelmMove m, IHittable anchor, HelmNums n) =>
        Mine ? HelmMoves.Begin(this, _helm, m, anchor, n) : "NOT THE OWNER";
    public void CastOff() => HelmMoves.End(this, _helm, HelmEnd.Pressed);

    // ── A HOOK (AbilityDef.Hook: the Grapnel) ───────────────────────────────
    // A hook row's slot: Left while the line holds (the swing's Time, or a tow's haul), N the anchor's or the craft's
    // NetId, At where the anchor was last frame (a step past Drives.SnapAt is its warp); Cool from the cast-off.
    public const float HookSlack = 60f;              // the host's give on a guest's reach: it measured a packet ago
    public HelmNums HookNums(AbilityDef def)
    {
        var h = def.Hook;
        return new HelmNums { Slot = def.Id, Bite = (float)Stats[h.Bite], Pull = (float)Stats[h.Pull], Stop = (float)Stats[h.Stop],
                              Clear = (float)Stats[h.Clear], Reach = (float)Stats[h.Reach], Reel = (float)Stats[h.Reel], Time = Stats[h.Time] };
    }
    private bool Hooked(string id) => Sl(id).Left > 0 || (_helm.On && _helm.N.Slot == id);
    // WHAT THE LINE WOULD TAKE, or why not: an anchor (Targeting.Immovable), a craft to tow (ITowable, Throwable), or
    // NO HOLD (a missile, a hulled body). Pressed while the line holds, nothing is refused: the press casts off.
    public string HookRefusal(string id, IHittable t) => HookWhy(id, t, 0f);
    private string HookWhy(string id, IHittable t, float slack)
    {
        var h = Abilities.Find(Class, id)?.Hook;
        if (h == null) return "NO HOLD";
        if (Hooked(id)) return null;
        if (Sl(id).Cool > 0) return "COOLING";
        if (t == null || !t.Alive) return "NO TARGET";
        bool anchor = Targeting.Immovable.Hits(t);
        if (!anchor && !(t is ITowable && Targeting.Throwable.Hits(t))) return "NO HOLD";
        if (Position.DistanceTo(t.Position) > Stats[h.Reach] + slack) return "OUT OF RANGE";
        if (anchor && Pinned) return "WEBBED";                 // the swing only: a craft is towed webbed or not
        return null;
    }
    // THE OWNER'S HALF of the press: a second press casts its own move off at once; an anchor begins the move here
    // (the host marks it, or the owner casts it off unmarked: HelmMoves.ConfirmWithin). A craft is the host's alone.
    private string HookOwner(AbilityDef def, IHittable t)
    {
        if (_helm.On && _helm.N.Slot == def.Id) { CastOff(); return null; }
        if (Sl(def.Id).Left > 0 || !Targeting.Immovable.Hits(t)) return null;
        return BeginHelm(def.Hook.Move, t, HookNums(def));
    }
    // THE PRESS, on the host: a second press casts off; else the anchor's move is marked, or the craft towed.
    public void Hook(string id, IHittable t)
    {
        var def = Abilities.Find(Class, id);
        if (!Net.Sim || def?.Hook is not { } h) return;
        ref var sl = ref Sl(id);
        if (sl.Left > 0) { CastOffHook(id, rip: true); return; }
        if (HookWhy(id, t, HookSlack) != null) return;
        if (Targeting.Immovable.Hits(t))
        {
            HelmMoves.Confirm(this, t, HookNums(def), Stats[h.Time]);
            Sl(id).At = t.Position;
        }
        else if (t is ITowable craft && Towing.Tow(craft, this, this, h.Tow))
        {
            sl.Left = Towing.Of(h.Tow).Haul; sl.N = t.NetId;
        }
    }
    // CAST OFF, on the host: a craft still held is hurled; an anchor still there (neither dead nor warped: `rip`) is
    // RIPPED; the line is released and the cooldown starts. Run by the second press, the slot's Expire, and HookWatch.
    public void CastOffHook(string id, bool rip)
    {
        var h = Abilities.Find(Class, id)?.Hook;
        if (!Net.Sim || h == null) return;
        ref var sl = ref Sl(id);
        var t = sl.N != 0 ? Combat.ById(sl.N) : null;
        if (t is ITowable { Towed: { Flung: false } tow } craft && tow.By == this) Towing.Hurl(craft);
        else if (rip && t != null && t.Alive && Targeting.Immovable.Hits(t)) Rip(id, h, t);
        HelmMoves.Release(this, id);
        sl.Cool = Cooling(Stats[h.Cooldown]);
    }
    // THE HOST'S WATCH on a line that holds, every frame: the anchor or the craft gone, or the anchor warped, ends it with
    // no rip; a web, a disable or this ship's own warp charge (its own, or a guest's reported one) ends a swing WITH one
    // (its owner has cast off already).
    private void HookWatch()
    {
        foreach (var def in Abilities.For(Class))
        {
            if (def.Hook == null) continue;
            ref var sl = ref Sl(def.Id);
            if (sl.Left <= 0 || sl.N == 0) continue;
            var t = Combat.ById(sl.N);
            if (t == null || !t.Alive || !Alive) { CastOffHook(def.Id, rip: false); continue; }
            if (!Targeting.Immovable.Hits(t)) continue;                       // a tow runs by Towing.Step
            if (t.Position.DistanceTo(sl.At) > Drives.SnapAt) { CastOffHook(def.Id, rip: false); continue; }
            sl.At = t.Position;
            if (Pinned || Disabled || Charging || _drive.Remote) CastOffHook(def.Id, rip: true);
        }
    }
    // THE RIP: the chunk's look raised FIRST (Fx.Tear finds the anchor among the living), then the hit through the
    // damage door, credited to the row's id -- the row's share of the anchor's own maximum hull (IQuarry; a dummy has
    // none) plus the flat.
    public double RipOf(HookSpec h, IHittable t) => Stats[h.RipShare] * ((t as IQuarry)?.MaxHp ?? 0) + Stats[h.RipFlat];
    private void Rip(string id, HookSpec h, IHittable t)
    {
        var dir = Position - t.Position;
        var hook = t.Position + (dir.LengthSquared() > 1e-6f ? dir.Normalized() : Vector2.Up) * t.HitRadius * 0.8f;
        Fx.Tear(t, hook, Position);
        Dealt.Deal(t, RipOf(h, t), this, id);
    }
    // THE TOP A GUEST'S REPORT MAY CLAIM, on the host: its hull's, or a marked move's ward (F8).
    public float ReportTop => Math.Max(TopNow, HelmMoves.Ward(this));
    // A DRIVER'S HOLD OF V -- the title screen's ship, or a check -- read with the pilot's own key.
    public bool DriveHeld;
    public bool PressDrive() => Drives.Press(this, _drive);
    public bool Charging => _drive.Held >= 0;
    public double ChargeHeld => _drive.Held;
    public float ChargeReach => Drives.Reach(_drive.Held, Stats["warp_rate"], Stats["warp_safe"]);
    public double DriveLock => _drive.Lock;
    public double JumpFlash => _drive.Flash;
    // DISABLED as this peer knows it: the host's status, or the owner's own lock after an overshoot,
    // held until the host's bit arrives (a guest's jump reaches the host a report later).
    public bool Disabled => _status.Has(Status.Disabled) || _drive.Lock > 0;
    // A RELOCATION THE HOST MADE (a returning pilot put back where it was: Hub's NetPlace): the
    // reports that follow are the ship arriving there, never a jump to price (Drives.Priced).
    public void Relocated() { _drive.Quiet = Drives.QuietFor; _drive.From = null; }
    public int SpeedClamps => _drive.Clamped;
    // THE FASTEST THIS HULL GOES NOW, ahead and sideways, lifted and unheld: what the host's speed
    // clamp and its jump pricing allow a guest's report (F24's hypot rule, Drives.SpeedCap).
    public float TopNow => TopSpeed((float)Stats["max_speed"], SpeedMult, SpeedAdds(), 1f);
    public float StrafeNow => StrafeTop((float)Stats["strafe_speed"], StrafeMult, 1f);

    // ── what is being done to it (Statuses): a raider's web today, and whatever a class's
    // ability puts on it next. The HOST decides; a guest is sent the bits and shows them.
    // Pinned holds the ship to StatusSet.PinSpeed of top speed, thrusting, unable to turn.
    private StatusSet _status;
    public StatusSet Statuses => _status;
    // A PRISM while Parrying is on it (F11, on the wire): its guard faces the cursor, ±90° off the nose
    public bool Prismatic => _status.Has(Status.Parrying);
    public float GuardAngle => Melee.Guard(Melee.Nose(this), (AimPoint - Position).Angle(), Prism.GuardMax);
    public void ApplyStatus(Status s, double seconds, double share = double.NaN)
    {
        if (!Net.Sim) return;
        if (s == Status.Pinned && _status.Has(Status.Unwebbed)) return;   // no web takes it (the whirlwind)
        if (s == Status.Pinned && WebCut > 0)
        {   // Web Breaker: the web's ask is kept whole, and the pin's own clock holds it shorter
            _webAsked = Math.Max(_webAsked, seconds);
            HoldWeb(0);
            return;
        }
        _status.Apply(s, seconds, share);
    }
    public bool Pinned => _status.Has(Status.Pinned);
    // THE WEB'S OWN HOLD CLOCK (Web Breaker, host): what the webs on this ship still ask for (the
    // longest ask, whole) and how long since the web took. A raider's latch asks again every frame
    // for as long as it is on, so a web holds by rounds (Items.WebHoldLeft): pinned (1 - cut) of each,
    // then free for the rest of it, the latch still on. The ask running out ends the web and the clock.
    private double _webAsked, _webPhase;
    private void HoldWeb(double delta)
    {
        if (_webAsked <= 0) return;
        _webAsked = Math.Max(0, _webAsked - delta);
        _webPhase += delta;
        double left = _webAsked > 0 ? Math.Min(Items.WebHoldLeft(_webPhase, WebCut), _webAsked) : 0;
        if (left > 0) _status.Apply(Status.Pinned, left);
        else _status.Clear(Status.Pinned);
        if (_webAsked <= 0) _webPhase = 0;                  // the web is over: the next one starts a round
    }
    // THE PAINT (F14, D33): one hostile at a time, for so long -- what its sentries take first
    // (DeployedTurret.Prefer) and what Time on target converges on. A hit of a Paint round raises it
    // on the host (Shot.Strike: the freighter's spotter). It lives in the primary's own slot (the
    // `guns` row: Left = seconds left, N = the target's NetId), so it rides the host's report to every
    // peer with no field of its own, counts down on every peer between reports (TickAbilities), and a
    // guest's sentry copies and its F refusal read the host's paint.
    public const string PaintSlot = "guns";
    public IHittable Painted => Sl(PaintSlot).Left > 0 && Sl(PaintSlot).N != 0 && Combat.ById(Sl(PaintSlot).N) is { Alive: true } t
                                && Combat.Hostiles.Contains(t) ? t : null;
    public void PaintOn(IHittable t, double seconds) { ref var p = ref Sl(PaintSlot); p.N = t?.NetId ?? 0; p.Left = t != null ? seconds : 0; }

    // A refused ability: its slot shows the reason, in red, for a moment.
    public const double FailShow = 1.5;
    private readonly Dictionary<string, (string msg, double until)> _fails = new();
    private double _clock;                                          // game seconds, for timing
    public double Clock => _clock;
    public void Fail(string id, string msg) => _fails[id] = (msg, _clock + FailShow);
    public string FailNote(string id) => _fails.TryGetValue(id, out var f) && _clock < f.until ? f.msg : null;

    // Craft leave the carrier one at a time, at least the row's LaunchInterval apart: fighters out
    // of the hangar, bombers off the deck, EACH KIND ON ITS OWN CLOCK -- one slot per row of
    // Wings.All rather than two named fields and a ternary picking between them, so a third craft
    // brings its own clock with it.
    private readonly double[] _nextLaunch = new double[System.Enum.GetValues<WingKind>().Length];
    public bool TakeLaunchSlot(WingKind k)
    {
        if (_clock < _nextLaunch[(int)k]) return false;
        _nextLaunch[(int)k] = _clock + Wings.Of(k).LaunchInterval;
        return true;
    }

    public void NoteStrikeDone() { if (--_strikesOut <= 0) StrikeTarget = null; }
    // A target that has left the world: the wing and the bombers let go of it. (Its turrets need
    // no telling: a turret holds only what is still in Combat.Hostiles -- Turret.StillThere.) The
    // strike's count is NOT reset: bombers already out still report back (NoteStrikeDone), and a new
    // strike waits for all of them, as it did when a dead target stayed the strike's until then.
    public void Forget(IHittable t)
    {
        if (ReferenceEquals(WingTarget, t)) WingTarget = null;
        if (ReferenceEquals(StrikeTarget, t)) StrikeTarget = null;
    }

    // A BOW SHOT'S PRESS (AbilityDef.Bow), on the host: one round of its Shots.All row off the nose, straight along the
    // heading, at the sheet's damage, speed and range; its Cooldown from the press. The Long Lance. The weapon id its
    // blows carry is the row's own (Shots.Of(kind).Id), so a listener tells a Lance from a director shell.
    public void FireAlong(string id)
    {
        var def = Abilities.Find(Class, id);
        if (!Net.Sim || def?.Bow is not { } b || Sl(id).Cool > 0) return;
        Combat.Fire(b.Kind, Aim.Nose(this, MyArt.Length * 0.5f), Vector2.Up.Rotated(Rotation), (float)Stats[b.Speed],
                    (float)Stats[b.Range], Stats[b.Damage], source: this, hitSource: id);
        if (def.Cooldown != null) Sl(id).Cool = Cooling(Stats[def.Cooldown]);
    }

    // ── THE ONE DOOR DAMAGE COMES THROUGH ───────────────────────────────
    // A shell, a beam, a ram, a missile's blast, an area tick: all of it ends here, so the
    // per-source gap, the tally, the guards a status puts in the way and the death are written
    // once. A class's defence (hardening, evasion, a bubble) belongs in Guarded, never in the
    // weapon that fired.
    //   `from` is where it came from, when the hit knows: that hit lights the shield on that
    // side, draws its number at the hull's edge there, and counts as combat. A hit with no
    // origin (a guest's word, a hull correction) only moves the hull, as it always has.
    public void Incoming(double d, string source = null, Vector2? from = null)
    {
        if (!Net.Sim || !Alive) return;       // only the host resolves damage
        if (source != null)
        {   // one ongoing source lands on this ship at most once per HitGap
            if (_lastHitBy.TryGetValue(source, out var at) && _clock - at < HitGap) return;
            _lastHitBy[source] = _clock;
        }
        d = Guarded(d);
        if (d <= 0) return;
        if (from is { } at2)
        {
            // THE TALLY IS KEYED BY THE FAMILY, THE GAP BY THE BODY. A shot is its own source so a
            // volley of three lands three times (Shots.SourceKey: "boss:missiles#41"), but the
            // tally the HUD and the checks read wants one line per WEAPON, not one per round --
            // keyed by the body it would gain an entry for every shell ever fired at this hull.
            string tally = Family(source);
            DamageBySource[tally] = (DamageBySource.TryGetValue(tally, out var sum) ? sum : 0) + d;
            var v = (at2 - Position).Rotated(-Rotation);
            float side = Mathf.Atan2(v.X, -v.Y);            // 0 = ahead, clockwise
            _shield?.Flash(side);
            // where it struck: the hull's edge toward the hit, off the keel's nearest point
            var keel = new Vector2(0, Mathf.Clamp(v.Y, -MyArt.Length * 0.5f, MyArt.Length * 0.5f));
            Popups.NoteImpact(this, ToGlobal(keel + (v - keel).LimitLength(MyArt.HalfWidth)));
            if (Net.IsOnline) (GetParent() as Hub)?.SendShield(OwnerId, side);
            NoteCombat();                                   // taking damage is combat
        }
        Hp -= d;
        if (Hp <= 0) Die();
    }

    // Every source whose last blow is older than HitGap: its entry can now only say "allow it",
    // which is what an absent entry says, so it is dropped. DamageBySource is left alone -- it is
    // a tally the HUD and the checks read, not a timer.
    // The weapon a source names, without the body that carried it: everything before the '#'.
    private static string Family(string source)
    {
        if (source == null) return "?";
        int hash = source.IndexOf('#');
        return hash < 0 ? source : source[..hash];
    }
    private void SweepHits()
    {
        if (_lastHitBy.Count == 0) return;
        foreach (var k in new List<string>(_lastHitBy.Keys))
            if (_clock - _lastHitBy[k] >= HitGap) _lastHitBy.Remove(k);
    }

    // WHAT THE STATUSES ON THIS SHIP LET THROUGH. Each status that changes a blow is a row of
    // StatusSet.Guards (Statuses.cs), walked in the order written there, then the bubbles. The share is
    // the one its APPLIER named (a taunt's taunt_guard 0.67) and the row's default where
    // none was named -- never a row read off this hull's own sheet.
    private double Guarded(double d)
    {
        d *= 1 - Math.Min(1, Items.Shares(Items.Door.Taken, Stats, BlowOn(null, d)));   // Ablative Skin: a small hit
        d *= SlipShare();                                                                // Slipstream: fast, x0.7
        foreach (var g in StatusSet.Guards)
        {
            if (!_status.Has(g.Status)) continue;
            d *= _status.ShareOf(g.Status, g.Share);
            if (d <= 0) return 0;
        }
        return ThroughBubbles(Position, d);                              // a bubble over it spends first
    }

    public void TakeDamage(double d) => Incoming(d);
    // A hit that knows where it came from: damage, and the shield lights that side.
    public void Hit(double d, Vector2 from, string source = null) => Incoming(d, source, from);

    // a guest's word of a hit on this ship: the side it came from, which is where it struck
    public void ApplyShield(float side)
    {
        _shield?.Flash(side);
        Popups.NoteImpact(this, ToGlobal(new Vector2(Mathf.Sin(side) * MyArt.HalfWidth, -Mathf.Cos(side) * MyArt.Length * 0.5f)));
    }

    // A RETURNING PILOT's ship, put back as it was (Hub's held places). Place: on the owner, where
    // it was left. Restore: on the host, its hull -- or its stasis, with the time it had left.
    public void Place(Vector2 at, float rot)
    {
        Position = at; Rotation = rot; Velocity = Vector2.Zero; _yawRate = 0; AutopilotTo = null;
    }
    public void Restore(double hp, bool alive, double stasis)
    {
        if (!Net.Sim) return;
        if (alive) Hp = Math.Clamp(hp, 1, MaxHp);
        else { Die(); _stasis = stasis; }
        _hullWatch = default;                      // a pilot put back as it was, not damage
    }

    // Into stasis where it lies; the pilot takes to the escape pod.
    private void Die()
    {
        Hp = 0; Alive = false; _stasis = StasisTime;
        Velocity = Vector2.Zero;
        WingTarget = null; StrikeTarget = null;
    }

    public bool Mine => Net.OwnedByMe(this);

    // ── frame ────────────────────────────────────────────────────────────────
    // Damage taken, shown where it lands (Popups). The host watches its own hull; a guest the
    // host's figures as they arrive (ApplyHostState) -- never the hull it regenerates between them.
    private HullWatch _hullWatch;
    private double _hostMax = -1;                  // the last hull size the host reported (a refit changes it)
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += delta;
        // the per-source gap is measured against this clock, so it is swept on this clock
        if (_clock >= _sweepAt) { _sweepAt = _clock + HitGap; SweepHits(); }
        if (Net.Sim) _hullWatch.Tick(this, Alive ? Hp : 0, taken: true);
        if (!Alive) _stasis = Math.Max(0, _stasis - delta);   // the host's clock rules; guests re-sync each packet
        if (_combatT > 0) _combatT = Math.Max(0, _combatT - delta);
        if (Alive && Hp < MaxHp) Hp = Math.Min(MaxHp, Hp + MaxHp * (InCombat ? RegenInCombat : RegenOutOfCombat) * delta);
        // The HOST decides pinned. A guest used to count its own (never-set) timer down here and
        // overwrite the host's flag every frame, so a raider's web never held a guest at all.
        if (Net.Sim) { _status.Tick(delta); HoldWeb(delta); TickThrows(delta); }
        // the drive's clocks run on every peer: the landing flash used to fade only on the owner's,
        // and a remote ship's warp left it lit for good
        Drives.Tick(_drive, delta);
        UpdatePod();
        if (Mine) { Drives.TickOwner(this, _drive, DriveHeld || (!Demo && !Hub.ControlsLocked && Input.IsKeyPressed(Key.V)), delta); LocalFlight(dt); }
        else      RemoteFollow(dt);

        TickAbilities(delta);
        // THE TRAIL (a sheet naming rewind_every: the Echo's Rewind): a mark of where it was and its hull, every so often
        if (Alive && Stats["rewind_every"] > 0)
            _trail.Note(_clock, Stats["rewind_every"], Stats["rewind_back"], new Mark(0, Position, Rotation, Velocity, Hp));

        if (Mine && Abilities.TriggerOf(Class)?.Reload is { } view) ActiveReload.Step(this, view, delta);
        if (Net.Sim) { FireControl(delta); Bored(delta); Swings(delta); DashSweeps(); ChargeLatch(delta); _doses.Tick(_clock, Stats, this); }
        foreach (var t in _turrets) t.Tick(delta);
        for (int i = _wings.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_wings[i])) { _wings.RemoveAt(i); continue; }
            _wings[i].Tick(delta);
            if (_wings[i].Done) { _wings[i].QueueFree(); _wings.RemoveAt(i); }    // a sortie's craft, home or warped out
        }
        if (WingTarget != null && !WingTarget.Alive) WingTarget = null;
        // Age the signal lights HERE, not in _Draw. Drawing is not guaranteed to happen -- a
        // canvas item off screen is culled -- so a ship that drifted out of view kept its lights
        // lit for ever. Torpedo ages its smoke in _Process for the same reason; _Draw only draws.
        for (int i = _signals.Count - 1; i >= 0; i--)
        {
            var (p, t) = _signals[i]; t -= delta;
            if (t <= 0) _signals.RemoveAt(i); else _signals[i] = (p, t);
        }

        if (Net.Sim && Net.IsOnline)
        {
            _hostSendCd -= delta;
            if (_hostSendCd <= 0) { _hostSendCd = HostInterval; SendHostState(); }
        }
        QueueRedraw();
    }

    // EVERY ABILITY THIS CLASS CARRIES, BY ITS OWN ROW. The cooldown runs down, a timer that is
    // up runs down, and on the frame it reaches zero the ROW says what happens: Elapsed on every
    // peer (the phase the bar must show at once), Expire on the host alone (what it resolves --
    // the railgun's shot, the reverb's blast, the magazine a reload refills). Every
    // peer counts down so a guest's bars move smoothly between host packets, and the next packet
    // corrects any drift. A timed ability is a row and nothing else: this loop is the only expiry.
    private void TickAbilities(double delta)
    {
        // A WRECK ENDS ITS HELM MOVE (F8): the owner's flight stops at the wreck (LocalFlight), so the
        // run's own end rules never see it -- left on, it would fly the re-boarded hull from where it lay.
        if (!Alive) HelmMoves.End(this, _helm, HelmEnd.Wrecked);
        if (Net.Sim) HookWatch();
        _afterDrive = Math.Max(0, _afterDrive - delta);
        foreach (var def in Abilities.For(Class))
        {
            ref var sl = ref Sl(def.Id);
            if (sl.Cool > 0) CoolBy(def, ref sl, delta);
            // A RAMP's running total (F1, D18) steps every frame regardless of Left, so it keeps
            // draining after the row stops -- Steer already ran this frame (LocalFlight, above),
            // so _yawRate is this frame's, not last frame's. OWNER-STEPPED: only the peer that
            // holds the helm (Mine) has a throttle and a yaw to read, and ApplyHostState leaves the
            // owner's own value alone (DESIGN.md, the authority model). The HOST steps its own copy of
            // a guest's from the guest's reports instead (RampsFromReport), and prices from that.
            if (def.Ramp is { } ramp && Mine)
            {
                bool holding = sl.Left > 0;
                bool cond = holding && (ramp.Condition == null || ramp.Condition(this));
                double turnRate = Stats["turn_rate"];
                double yawShare = turnRate > 0 ? Math.Min(1, Math.Abs(_yawRate) / turnRate) : 0;
                sl.Own = RampSpec.Step(sl.Own, Stats[ramp.Build], Stats[ramp.Cap], Stats[ramp.Bleed],
                                       holding, cond, yawShare, delta);
            }
            if (sl.Left <= 0) continue;
            if (Net.Sim) def.Tick?.Invoke(this, Math.Min(delta, sl.Left));   // a field's frame (AbilityDef.Tick): never past its time
            sl.Left -= delta;
            if (sl.Left > 0) continue;
            sl.Left = 0;
            if (def == Drive?.Row) _afterDrive = Items.AfterDriveSecs;   // the drive's run is over: Burst Feed's window
            def.Elapsed?.Invoke(this);
            if (Net.Sim) def.Expire?.Invoke(this);
        }

        if (Stats.Def.Has(Fit.Broadside))
        {   // THE VOLLEYS: a rhythm, not a timer, so the loop above cannot own them. The wind-up
            // is the row's Left and the cooldown after is its Cool, both run down up there, and
            // the row's Elapsed is what moves it from the one to the other on every peer. A ship
            // lost in the middle of a broadside loses the rest of it.
            ref var bs = ref Sl("broadside");
            if (!Alive) { bs.Left = 0; bs.N = 0; }
            if (bs.N > 0)
            {   // every main gun, along its barrel as it points now; the gap CARRIES its remainder, as the guns' reload does
                if (Net.Sim) bs.Own -= delta;
                for (int n = 0; Net.Sim && bs.Own <= 0 && bs.N > 0 && n < 8; n++)
                {
                    foreach (var m in _mains) m.Shoot(Stats["broadside_mult"]);
                    bs.Own += Stats["broadside_gap"];
                    if (--bs.N == 0) bs.Cool = Cooling(Stats["broadside_cooldown"]);
                }
            }
        }
    }

    // ── main guns: salvo or staggered ────────────────────────────────────────
    // SALVO fires every barrel at once, once per reload. STAGGERED fires one barrel
    // every reload / barrels, rotating through them. Same shots per second either
    // way: the rate is identical, only the rhythm differs.
    //
    // The reload CARRIES its remainder (cd += step) instead of resetting. Resetting
    // would round every step up to a whole frame -- and staggered takes four steps
    // per salvo's one, so it would fall behind (measured: 4.50 vs 6.00 DPS).
    //
    // The reload is the ship's CADENCE -- the sheet's, with whatever is running added to its bonus
    // -- the door its point defence, its dropped turrets and its wing come through too.
    private void FireControl(double delta)
    {
        if (!Stats.Def.Has(Fit.Guns) || _mains.Count == 0) return;
        // idle, or DISABLED: the guns hold, and keep reloading
        if (!Trigger || _status.Has(Status.Disabled)) { _gunCd = Math.Max(0, _gunCd - delta); return; }

        double interval = Cadence("main_interval");
        double step = Staggered ? interval / _mains.Count : interval;
        _gunCd -= delta;
        for (int n = 0; _gunCd <= 0 && n < 32; n++)
        {
            // a running row that a shot ends (the Veil) hears it first; a primed volley (Prime) goes at its multiple, once
            double k = 1;
            if (Net.Sim)
            {
                foreach (var def in Abilities.For(Class)) if (def.OnFire != null && Sl(def.Id).Left > 0) def.OnFire(this);
                k = _primed; _primed = 1;
            }
            FireOnce(k);
            _gunCd += step;
        }
    }

    // ONE ROUND OF THE PRIMARY, by the class's kind (ClassDef.Primary, D35): the one place a kind is read.
    private void FireOnce(double k)
    {
        switch (Stats.Def.Primary)
        {
            case Primary.Lob: Lob(); break;
            case Primary.Beam: { var (at, dir) = MainBore; LanceTick(at, dir); break; }
            default:
                if (Staggered) { _mains[_nextBarrel % _mains.Count].Shoot(k); _nextBarrel = (_nextBarrel + 1) % _mains.Count; }
                else foreach (var m in _mains) m.Shoot(k);
                break;
        }
    }

    // A LOB (the Bastion's siege mortar): a predicted blast of the class's Missiles.All row thrown from
    // the main mount onto the cursor -- clamped to mortar_min .. main_range on the cursor's bearing (the
    // bow's, for a cursor on the hull) -- landing mortar_flight later, main_damage in mortar_blast.
    public Vector2 LobPoint(Vector2 cursor)
    {
        var off = cursor - Position;
        var dir = off.LengthSquared() > 1f ? off.Normalized() : Vector2.Up.Rotated(Rotation);
        return Position + dir * Mathf.Clamp(off.Length(), (float)Stats["mortar_min"], (float)Stats["main_range"]);
    }
    // THE MENDING LANCE (the Tender's primary, Primary.Beam, kits6b-J7, D44): one tick of a beam out of
    // `from` along `dir` (FireOnce: the main barrel, which follows the cursor at main_turn), main_range
    // long, onto the FIRST body it touches, nearest first -- a hostile takes main_damage through the door
    // (Dealt.Lance), a friendly hull is mended lance_heal (Mend.Give, "lance"): never both, never this
    // hull, and nothing in flight (a missile, a hulled round) stops it. Host only. What it touched rides
    // the lance's slot to every peer, so each draws it (Fields' Lance look): Left while the trigger holds,
    // N one of Lance*, Own how far it reached. Returns what it touched.
    public const string LanceSlot = "lance";
    public const int LanceNone = 0, LanceMend = 1, LanceBurn = 2;
    public (Vector2 at, Vector2 dir) MainBore => _mains.Count > 0 ? _mains[0].Bore : (Position, Vector2.Up.Rotated(Rotation));
    public int LanceTick(Vector2 from, Vector2 dir)
    {
        if (!Net.Sim) return LanceNone;
        float reach = (float)Stats["main_range"];
        var to = from + dir.Normalized() * reach;
        var pool = new List<(Vector2 at, float r, IHittable foe, IMendable friend)>();
        foreach (var h in Targeting.Hittable(Combat.Hostiles, Targeting.Attackable))
            if (!TagExt.Is(h, Tag.Missile | Tag.Hulled)) pool.Add((h.Position, h.HitRadius, h, null));
        foreach (var m in Mend.Friendlies(MyHub))
            if (!ReferenceEquals(m, this)) pool.Add((m.Position, m.BodyRadius, null, m));
        var first = Lines.Pick(from, to, 0f, 1, pool, p => p.at, p => p.r);
        int touched = LanceNone; float len = reach;
        if (first.Count > 0)
        {
            var (at, _, foe, friend) = first[0];
            len = Mathf.Clamp((at - from).Dot(dir.Normalized()), 0f, reach);
            if (foe != null) { Dealt.Deal(foe, Stats["main_damage"], this, Dealt.Lance); touched = LanceBurn; }
            else { Mend.Give(friend, Stats["lance_heal"], this, Dealt.Lance); touched = LanceMend; }
        }
        ref var sl = ref Sl(LanceSlot);
        sl.Left = Cadence("main_interval") * 2;          // drawn a tick past the last, so a held beam never blinks
        sl.N = touched; sl.Own = len;
        return touched;
    }

    private void Lob()
    {
        var from = _mains.Count > 0 ? _mains[0].GlobalPosition : Position;
        MyHub?.ThrowMissile(new MissileSpec { Side = Stats.Def.LobSide, Damage = Stats["main_damage"],
                                              Blast = (float)Stats["mortar_blast"], Flight = Stats["mortar_flight"] },
                            from, LobPoint(AimPoint), NetId);
    }

    // ── an active reload (AbilityDef.Reload: the Sniper's railgun) ─────────────
    // THE OWNER'S STROKE RULE (ActiveReload.cs, one meaning per stroke): a trigger row that reloads
    // actively fixes each stroke's meaning at its press -- the owner's view reloading, it is the timing
    // press (ActiveReload.Press) and the trigger stays low until it is let go; otherwise it is the
    // charge, and the trigger follows the key. A guest's own charge let go starts its view's reload.
    public ActiveReload.View ReloadView;
    private bool _strokeDown, _strokeTimes;
    private bool Stroke(AbilityDef def, bool down)
    {
        if (def.Reload is not { } r) return down;
        if (down && !_strokeDown)
        {
            _strokeTimes = ReloadView.Running;
            if (_strokeTimes) ActiveReload.Press(this, r);
        }
        else if (!down && _strokeDown && !_strokeTimes && !Net.Sim && ReloadView.Charge > 0) ActiveReload.Shot(this, r);
        _strokeDown = down;
        return down && !_strokeTimes;
    }
    // THE HOST'S CHARGE LATCH: a charge begins on the later of the trigger rising and the chamber
    // seating, fills at the ship's Cadence of the row's Charge, and on the trigger falling fires the
    // row's Loose with the share it reached (1 at full, held past full is full). A wreck or a DISABLED
    // hull loses a charge it had begun; the round stays in the chamber.
    private double _charge = -1;
    private void ChargeLatch(double delta)
    {
        if (Abilities.TriggerOf(Class) is not { Reload: { } r } def) return;
        if (!Alive || Disabled || Stilled) { _charge = -1; return; }
        if (_charge < 0) { if (Trigger && ActiveReload.Seated(Sl(r.Id))) _charge = 0; return; }
        if (Trigger) { _charge += delta; return; }
        double share = Math.Min(1, _charge / Math.Max(1e-6, Cadence(r.Charge)));
        _charge = -1;
        def.Loose?.Invoke(this, share);
    }

    // ── a gun down the nose: every row that names a Bore (the Pepperbox), fired while the trigger holds ──
    private void Bored(double delta)
    {
        bool firing = Alive && Trigger && !_status.Has(Status.Disabled) && !Stilled;
        foreach (var def in Abilities.For(Class))
            if (def.Bore != null) Bores.Tick(this, def, firing, delta);
    }

    // ── melee: every row that names a Swing (the blade, the whirlwind) ─────────
    // A Hold row swings while the trigger holds and nothing Stills it; a Press row while its Left
    // runs. Each keeps its own clock in its slot's Own (host-side; the wire's copy is only drawn),
    // which CARRIES its remainder like the guns' reload, and a swing lands at once on the first frame.
    public bool Stilled
    {
        get
        {
            foreach (var def in Abilities.For(Class)) if (def.Stills && Sl(def.Id).Left > 0) return true;
            return false;
        }
    }
    private void Swings(double delta)
    {
        foreach (var def in Abilities.For(Class))
        {
            if (def.Swing is not { } row) continue;
            ref var sl = ref Sl(def.Id);
            bool on = Alive && !_status.Has(Status.Disabled) && Melee.Running(this, def);
            if (!on) { sl.Own = Math.Max(0, sl.Own - delta); continue; }
            sl.Own -= delta;
            for (int n = 0; sl.Own <= 0 && n < 8; n++)
            {
                Melee.Strike(row, this, Stats[row.Damage]);
                sl.Own += Cadence(row.Every);
            }
        }
    }

    // WHAT A PRESS SPENDS, on the host: a CHARGED row (AbilityDef.Charges) one charge if one is left (its slot's N
    // counts the spent ones), the recharge started if none was running; a row with a Cooldown its cooldown, if it
    // is not cooling. False: nothing to spend, and the press does nothing.
    private bool Spend(AbilityDef def)
    {
        ref var sl = ref Sl(def.Id);
        if (def.Charges != null)
        {
            if (sl.N >= (int)Stats[def.Charges]) return false;
            sl.N++;
            if (sl.Cool <= 0) sl.Cool = Cooling(Stats[def.Recharge]);
            return true;
        }
        if (def.Cooldown == null) return true;
        if (sl.Cool > 0) return false;
        sl.Cool = Cooling(Stats[def.Cooldown]);
        return true;
    }
    // A ZONE LAID (AbilityDef.Lays: the tether mine, the curtain, the well), on the host: what the press spends, then the
    // row laid where it goes (Zones.Spot: the stern, or the cursor clamped with its bar across the aim), what the
    // host decides of it read from this sheet now (Zones.Lay).
    public void Lay(string id)
    {
        var def = Abilities.Find(Class, id);
        if (def?.Lays is not { } row || !Net.Sim || MyHub is not { } hub || !Spend(def)) return;
        var (at, rot) = Zones.Spot(row, Position, Rotation, MyArt.Length * 0.5f, def.TakesPoint ? Sl(id).At : AimPoint);
        Zones.Lay(hub, row, at, rot, this);
    }

    // A SALVO POPPED (AbilityDef.Pops: the flares), on the host: what the press spends (its Cooldown), then the
    // row round the hull (Hub.Flares), as many as this sheet's CountStat row says (the row's Count without one).
    public void Pop(string id)
    {
        var def = Abilities.Find(Class, id);
        if (def?.Pops is not { } row || !Net.Sim || MyHub is not { } hub || !Spend(def)) return;
        hub.Flares(Position, Rotation, Array.IndexOf(Decoys.All, row), row.CountStat != null ? (int)Math.Round(Stats[row.CountStat]) : 0);
    }

    // IT DRAWS THE HOSTILE GUNS (IRaidTarget.Draws) while any of its class's rows that Draws runs: the Taunt. Read by
    // an emplacement's gun (Emplacement.Prefer), on the host, where the row's Left is the host's own.
    public bool Draws
    {
        get
        {
            foreach (var def in Abilities.For(Class)) if (def.Draws && Sl(def.Id).Left > 0) return true;
            return false;
        }
    }

    // EVERY WEB ON THE HULL LET GO (host): the pin and the web's own ask. A latch still on asks again next frame.
    private void LetGoWebs() { _webAsked = 0; _webPhase = 0; _status.Clear(Status.Pinned); }

    // ── a stance (AbilityDef.Stance: the prism) ─────────────────────────────
    // THE PRESS, on the host: up (its Time, its Status for that Time, no split yet), or, pressed while it
    // runs, dropped -- at once, or over its Release (what is left is cut to it; a press inside the release
    // changes nothing). Its cooldown is set where it ENDS (EndStance), never here.
    public void Stance(string id)
    {
        var st = Abilities.Find(Class, id)?.Stance;
        ref var sl = ref Sl(id);
        if (st == null) return;
        if (sl.Left > 0)
        {
            if (st.Release == null) EndStance(id);
            else sl.Left = Math.Min(sl.Left, Stats[st.Release]);
            return;
        }
        if (sl.Cool > 0) return;
        sl.Left = Stats[st.Time]; sl.N = 0; sl.Own = 0;
        if (st.Holds != Status.None) _status.Apply(st.Holds, sl.Left);
    }
    // IT ENDS: dropped, ended by another press, or run out (the row's Elapsed, on every peer, so every bar
    // shows the cooldown at once). The cooldown runs from here; the host lets the Status go.
    public void EndStance(string id)
    {
        var st = Abilities.Find(Class, id)?.Stance;
        if (st == null) return;
        ref var sl = ref Sl(id);
        sl.Left = 0; sl.Cool = Cooling(Stats[st.Cooldown]);
        if (Net.Sim && st.Holds != Status.None) _status.Clear(st.Holds);
    }
    // THE SPLIT TICK (IPrism.Split, the host's Prism.Catch): a running stance splits at most its Splits
    // catches, Every seconds apart (a hair early is on time: a burn's own tick is the same 0.75 s); its
    // slot counts them (N) and keeps when the last one fell, in seconds into the stance (Own). A catch in
    // between still takes the catcher nothing. With no stance running (the status put on by hand) every
    // catch splits.
    public const double SplitEarly = 0.05;
    public bool Split()
    {
        foreach (var def in Abilities.For(Class))
        {
            if (def.Stance is not { Splits: not null } st || Sl(def.Id).Left <= 0) continue;   // a stance that splits
            ref var sl = ref Sl(def.Id);
            double into = Stats[st.Time] - sl.Left;
            if (sl.N >= (int)Stats[st.Splits] || (sl.N > 0 && into - sl.Own < Stats[st.Every] - SplitEarly)) return false;
            sl.N++; sl.Own = into;
            return true;
        }
        return true;
    }

    // ── a dash (AbilityDef.Dash: the lunge) ─────────────────────────────────
    // THE PRESS, on the host: the row's Time up, its cooldown, where and which way it started (the
    // slot's At and Own, which the wire carries), the hardening, and nothing struck yet.
    private readonly HashSet<IHittable> _dashStruck = new();
    public void StartDash(string id)
    {
        var d = Abilities.Find(Class, id)?.Dash;
        ref var sl = ref Sl(id);
        if (d == null || sl.Left > 0 || sl.Cool > 0) return;
        sl.Left = Stats[d.Time]; sl.Cool = Cooling(Stats[d.Cooldown]);
        sl.At = Position; sl.Own = Rotation;
        _dashStruck.Clear();
        if (d.Guard != null) ApplyStatus(Status.Hardened, sl.Left, Stats[d.Guard]);   // the dash says how hard
    }
    // THE HOST'S SWEEP: every hostile body within the hull's half-beam (plus its own radius) of the line
    // from the press's spot, along its heading, `progress` (0..1) of the way to the row's Reach, struck
    // once a dash for the row's Damage. Run every frame the row is up, and at 1 by its Expire.
    public void DashSweep(string id, double progress)
    {
        var d = Abilities.Find(Class, id)?.Dash;
        if (d == null || !Net.Sim) return;
        ref var sl = ref Sl(id);
        var from = sl.At;
        var to = from + Vector2.Up.Rotated((float)sl.Own) * (float)(Stats[d.Reach] * Math.Clamp(progress, 0, 1));
        foreach (var h in new List<IHittable>(Targeting.Hittable(Combat.Hostiles, Targeting.Attackable)))
        {
            if (_dashStruck.Contains(h)) continue;
            var near = Geometry2D.GetClosestPointToSegment(h.Position, from, to);
            if (near.DistanceTo(h.Position) > MyArt.HalfWidth + h.HitRadius) continue;
            _dashStruck.Add(h);
            Dealt.Deal(h, Stats[d.Damage], this, id);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
    }
    private void DashSweeps()
    {
        foreach (var def in Abilities.For(Class))
            if (def.Dash is { } d && Sl(def.Id).Left > 0)
            {   // DISABLED inside the dash: the row ends where the hull stopped (the owner's carry stops on the same
                // state, which rides the wire) -- no Expire sweep to the Reach it never went, no hardening left on
                if (Disabled) { Sl(def.Id).Left = 0; _status.Clear(Status.Hardened); continue; }
                DashSweep(def.Id, 1 - Sl(def.Id).Left / Stats[d.Time]);
            }
    }
    // THE OWNER'S CARRY: from the first frame it sees a dash row's Left (the host's press, here at once
    // on the host, a packet later on a guest), the hull goes the row's Reach along its nose over the
    // row's whole Time, whatever the lifts, the helm or its speed -- exactly Reach, the last frame's step
    // cut to what is left. A fresh press is told from a stale packet by its cooldown jumping back up over
    // what it read the frame before (a packet's drift is well under a second; a cooldown cut to 0 and pressed
    // again inside a second is a fresh press all the same).
    private AbilityDef _dashRow;
    private double _dashLeft;
    private readonly Dictionary<string, double> _dashCoolWas = new();
    private bool DashCarry(float dt)
    {
        if (Disabled) { _dashLeft = 0; return false; }
        foreach (var def in Abilities.For(Class))
        {
            if (def.Dash == null) continue;
            double cool = Sl(def.Id).Cool, was = _dashCoolWas.GetValueOrDefault(def.Id);
            _dashCoolWas[def.Id] = cool;
            if (_dashLeft > 0 || Sl(def.Id).Left <= 0 || cool <= was + 1.0) continue;     // running, idle, or the dash already flown
            _dashRow = def; _dashLeft = Stats[def.Dash.Time];
        }
        if (_dashLeft <= 0 || _dashRow?.Dash is not { } d) return false;
        double step = Math.Min(dt, _dashLeft);
        _dashLeft -= step;
        Position += Vector2.Up.Rotated(Rotation) * (float)(Stats[d.Reach] / Stats[d.Time] * step);
        return true;
    }

    // ── the owner steers it: naval handling ──────────────────────────────────
    private void LocalFlight(float dt)
    {
        var forcing = Forcing;
        if (forcing != _forcedBy && _forcedBy is { Parting.Recoil: { } keep } && Alive && Abilities.Find(Class, _forcedBy.Id) == _forcedBy)
            Velocity *= (float)Stats[keep];                                          // a parting round's recoil (_forcedBy)
        _forcedBy = forcing;
        if (!Alive)
        {   // in stasis the hull stays put; the pod flies (EscapePod reads the keys)
            Velocity = Vector2.Zero;
            SendState(dt);
            return;
        }
        // Input.IsKeyPressed polls the raw keyboard, so it does not care that a text
        // box has focus. Hub decides when the controls belong to the UI instead.
        bool locked = Hub.ControlsLocked || Demo;
        float throttle = 0f, rudder = 0f, strafe = 0f;
        if (!locked)
        {
            if (Input.IsKeyPressed(Key.W)) throttle += 1f;
            if (Input.IsKeyPressed(Key.S)) throttle -= 1f;
            if (Input.IsKeyPressed(Key.A)) rudder -= 1f;
            if (Input.IsKeyPressed(Key.D)) rudder += 1f;
            // SHIFT TURNS A/D INTO A SLIDE (F24) on a hull whose sheet has one (strafe_speed > 0):
            // the nose holds its heading, so the guns and the cursor keep their meaning. A hull with
            // no slide (the capitals) reads Shift + A/D as the rudder it always was.
            if (Input.IsKeyPressed(Key.Shift) && Stats["strafe_speed"] > 0) { strafe = rudder; rudder = 0f; }
        }
        if (throttle != 0f || rudder != 0f || strafe != 0f) AutopilotTo = null;   // any helm key takes the controls back
        else if (AutopilotTo is { } dest)
            (throttle, rudder) = Autopilot.Capital(Position, Rotation, Velocity, (float)Stats["max_speed"], dest, 60f);
        if (Pinned) { throttle = 1f; rudder = 0f; strafe = 0f; AutopilotTo = null; }   // forced thrust, no rudder, no slide
        else if (forcing != null) { throttle = 1f; AutopilotTo = null; }                 // a sprint: forced thrust, the rudder free
        _throttle = throttle;
        Thrusting = throttle != 0f;
        // A HELM MOVE (F8) flies the hull by its own law while it lasts; a dash carries the hull instead
        // of the helm; otherwise the helm steers
        if (HelmMoves.Fly(this, _helm, throttle, rudder, dt)) _yawRate = 0f;
        else if (!DashCarry(dt)) Steer(throttle, rudder, strafe, dt);

        // the main guns aim at the cursor (or, a director's, lead the selected); the hull does not follow it
        if (!Demo) Trigger = false;              // a display ship's driver owns the trigger
        if (!locked)
        {
            AimPoint = Directed(GetGlobalMousePosition(), (GetParent() as Hub)?.Selected, dt);
            Trigger = Abilities.TriggerOf(Class) is { } trig && Stroke(trig, Input.IsKeyPressed(Abilities.KeyFor(Class, trig.Id)));
        }

        SendState(dt);
    }

    // THE DIRECTOR (a sheet whose director_lead is above 0: the destroyer's battery). With a hostile SELECTED within
    // main_range x director_lead, the aim point is where a shell at shell_speed meets it on its present course
    // (Missiles.Predict on a Lead watched frame to frame), so the guns lead it by themselves; with none, the cursor.
    // Worked out on the owner and sent as its aim, as the cursor was: the host fires at it, no new wire.
    private Lead _director;
    public Vector2 Directed(Vector2 cursor, IHittable selected, double dt)
    {
        double lead = Stats["director_lead"];
        if (lead <= 0 || selected is not { Alive: true } || Position.DistanceTo(selected.Position) > Stats["main_range"] * lead)
        { _director = default; return cursor; }
        _director.Watch(selected, selected.Position, dt);
        double speed = Math.Max(1, Stats["shell_speed"]);
        var at = selected.Position;
        // the flight to where it will be, taken again from that point: 8 passes settle to a unit or two at 400 u/s
        for (int i = 0; i < 8; i++) at = Missiles.Predict(selected.Position, _director.Velocity, Position.DistanceTo(at) / speed);
        return at;
    }

    private void SendState(float dt)
    {
        _sendCd -= dt;
        if (_sendCd > 0 || !Net.IsOnline) return;
        _sendCd = SendInterval;
        var pod = IsInstanceValid(_pod) ? _pod.Position : Position;
        float podRot = IsInstanceValid(_pod) ? _pod.Rotation : 0f;
        (GetParent() as Hub)?.SendShipState(Position.X, Position.Y, Velocity.X, Velocity.Y, Rotation,
            AimPoint.X, AimPoint.Y, Trigger, Staggered, pod.X, pod.Y, podRot, Charging);
    }

    // The pod exists exactly while the ship is in stasis: flown by its owner,
    // shown to everyone else at the position the owner reports.
    private void UpdatePod()
    {
        if (!Alive && !IsInstanceValid(_pod))
        {
            _pod = new EscapePod { Name = $"Pod_{OwnerId}", Local = Mine, Position = Position, Rotation = Rotation, Engine = Accent };
            GetParent().AddChild(_pod);
        }
        else if (Alive && IsInstanceValid(_pod)) { _pod.QueueFree(); _pod = null; }
        // stasis: a cold, pulsing blue; no turrets. Unpickable (the Veil, Status.Untargetable, on every peer from the host's
        // status bits): the hull at VeiledAlpha, a shimmer
        if (_sprite != null)
            _sprite.Modulate = !Alive ? new Color(0.45f, 0.62f, 0.95f, 0.55f + 0.12f * Mathf.Sin(Time.GetTicksMsec() / 300f))
                             : Targeting.Hidden(this) ? new Color(Main, VeiledAlpha + 0.06f * Mathf.Sin(Time.GetTicksMsec() / 120f)) : Main;
        foreach (var t in _turrets) t.Visible = Alive;
    }

    // The helm. Velocity is split into
    // the component along the keel and the component across it. Thrust only ever
    // adds along the keel; water drag slows both; the keel kills sideways drift
    // quickly, so the ship goes where it points. The rudder turns at speed/radius
    // -- a turning circle -- capped by the rudder limit, and does nothing dead in
    // the water. THE SLIDE (F24, `strafe` -1..1): while it is held the across speed moves toward
    // strafe x StrafeTop at the sheet's strafe_thrust x the lift, and the keel lets it; otherwise
    // the keel's grip damps it, as it always did.
    private void Steer(float throttle, float rudder, float strafe, float dt)
    {
        var fwd = Vector2.Up.Rotated(Rotation);           // nose direction
        var side = new Vector2(-fwd.Y, fwd.X);
        float along = Velocity.Dot(fwd), across = Velocity.Dot(side);

        // DISABLED: no thrust, no rudder, whatever is pressed, and the heading holds (below). HELD
        // (a running row's Hold, after the lifts): the thrust both ways, both caps and the rudder,
        // all by the one share.
        bool disabled = Disabled;
        if (disabled || Pinned) strafe = 0f;              // a web or Disabled: no slide, as no rudder
        if (disabled) { throttle = 0f; rudder = 0f; }
        float hold = Held;
        throttle *= hold; rudder *= hold;
        // A SPEED LIFT (the boost, the veil) lifts the push ahead WITH the top speed. The water holds a
        // hull to thrust / drag, so a lift on the cap alone would stop there: a heavy pushes 130
        // against 0.35, which is 371 u/s where a 2.5x lift on its 190 promises 475. Astern is
        // not lifted, and neither is its cap.
        float lift = SpeedMult;
        if (throttle > 0) along += (float)Stats["thrust"] * ThrustMult * throttle * dt;
        else if (throttle < 0) along += (float)Stats["reverse_thrust"] * throttle * dt;
        along -= along * Mathf.Clamp((float)Stats["water_drag"] * dt, 0f, 1f);
        // F1's Add lands on the CAP alone (TopSpeed), same as the lift itself does not touch astern.
        float top = TopSpeed((float)Stats["max_speed"], lift, SpeedAdds(), hold);
        along = Mathf.Clamp(along, -(float)Stats["reverse_speed"] * hold,
                            top * (Pinned ? Items.PinnedSpeed(StatusSet.PinSpeed, WebCut) : 1f));
        if (strafe != 0f)
        {   // the slide takes its own lift (StrafeMult: the boost's surge_strafe), the same rule
            float slide = StrafeMult;
            across = Mathf.MoveToward(across, strafe * StrafeTop((float)Stats["strafe_speed"], slide, hold),
                                      (float)Stats["strafe_thrust"] * slide * dt);
        }
        else across *= Mathf.Exp(-(float)Stats["keel"] * dt);
        if (hold <= 0f) across = 0f;                      // rooted: nothing slides it either

        // turning circle: yaw rate = speed / radius, capped by the rudder; astern the
        // rudder reverses, as it does on a real ship. At or below 5% of top speed the
        // rudder PIVOTS the hull slowly on the spot instead, like a tank -- the heading
        // turns, nothing moves the hull sideways (no strafing). Pinned: no turning at all.
        if (!Pinned && Mathf.Abs(along) <= PivotBelow * (float)Stats["max_speed"])
            _yawRate = Mathf.MoveToward(_yawRate, rudder * PivotRate, 2.5f * dt);
        else
        {
            float cap = Mathf.Min(Mathf.Abs(along) / (float)Stats["turn_radius"], (float)Stats["turn_rate"]);
            float want = rudder * cap * Mathf.Sign(along == 0 ? 1 : along);
            _yawRate = Mathf.MoveToward(_yawRate, want, 2.5f * dt);     // the rudder takes a moment to bite
            if (Pinned) _yawRate = 0f;                                  // pinned: it cannot turn
        }
        // Disabled or rooted: the heading holds too, at ANY speed -- including the yaw it carried
        // in. At pivot speed the rudder's 2.5 rad/s^2 bite would otherwise coast that rate down:
        // 8 degrees from a 0.85 rad/s turn.
        if (disabled || hold <= 0f) _yawRate = 0f;
        Rotation += _yawRate * dt;

        fwd = Vector2.Up.Rotated(Rotation); side = new Vector2(-fwd.Y, fwd.X);
        Velocity = fwd * along + side * across;
        Position += Velocity * dt;
    }

    public float SpeedAhead => Velocity.Dot(Vector2.Up.Rotated(Rotation));
    // THE SLIDE'S OWN SPEED, by F1's share rule: the sheet's strafe_speed x the lift (the boost's +50%
    // is a lift like any other), THEN every hold multiplied on the whole -- so the Prism's x0.5 is a
    // share of the lifted slide and the Anchor's x0 stops it, as they do the top speed. Pure, so it is
    // provable without a hull.
    public static float StrafeTop(float sheet, float lift, float hold) => sheet * lift * hold;
    // the slide as it is now: + to starboard (D), - to port (A)
    public float SpeedAcross { get { var f = Vector2.Up.Rotated(Rotation); return Velocity.Dot(new Vector2(-f.Y, f.X)); } }
    private const float PivotBelow = 0.05f;                     // the pivot works at <= 5% of top speed
    private static readonly float PivotRate = Mathf.DegToRad(10f);   // slowly: 10 degrees a second

    // ── everyone else follows it ─────────────────────────────────────────────
    private void RemoteFollow(float dt)
    {
        // Dead reckoning: carry the last known velocity forward so a remote ship keeps moving
        // smoothly between the 20 Hz updates instead of stuttering -- for half a second, no more.
        // A friend whose connection dies sends no goodbye; carried forward for ever, their ship
        // flew off in a straight line until the timeout, and a held trigger kept firing on the
        // host the whole way.
        _netAge += dt;
        if (_netAge < 0.5f) _netPos += _netVel * dt;
        if (_netAge > 1f) Trigger = false;
        // Its real velocity, not zero: the engine plume reads it, and so does the launch of a
        // guest carrier's fighters on the host.
        Velocity = _netAge < 0.5f ? _netVel : Vector2.Zero;
        // a warp is a jump, not a glide: snap across it (and flash where it lands)
        if (Position.DistanceTo(_netPos) > Drives.SnapAt) { Position = _netPos; _drive.Flash = Drives.FlashFor; }
        else Position = Position.Lerp(_netPos, Mathf.Clamp(12f * dt, 0f, 1f));
        Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(12f * dt, 0f, 1f));
    }

    // The owner's report (Hub.NetShipState hands it only to the SENDER'S own ship: nobody can
    // move another's). No impossible numbers: a NaN position fails every distance test, so the
    // ship could not be hit, and a far-off one would drag the host's whole world with it.
    public void ApplyState(float px, float py, float vx, float vy, float rot, float ax, float ay, bool trigger, bool staggered,
                           float podX, float podY, float podRot, bool warping)
    {
        if (!new[] { px, py, vx, vy, rot, ax, ay, podX, podY, podRot }.All(float.IsFinite)) return;
        float age = _netAge;                       // since the last report: what the hull could have flown
        _netAge = 0f;
        _netPos = new Vector2(px, py);
        _netVel = new Vector2(vx, vy);
        _netRot = rot;
        if (Net.Sim) RampsFromReport(rot, age);
        AimPoint = new Vector2(ax, ay);
        Trigger = trigger;          // the host fires on this; a guest only draws
        Staggered = staggered;
        if (IsInstanceValid(_pod) && !_pod.Local) _pod.SetNet(new Vector2(podX, podY), podRot);
        _drive.Remote = warping;
        if (Net.Sim)
        {   // THE HOST'S READING of a guest's report (Drives): a jump it made is priced, and the speed it
            // claims is held to what its hull can do now. Priced only between two reports of the ship IN
            // THIS WORLD: entering one drops every peer's sector until it reports again (Hub.EnterSector),
            // and a guest's last reports from the world it left can still land here: priced, they would
            // make its first report from the new spawn a snap, and a capital would arrive DISABLED.
            if (Hub.PeerSector(OwnerId) == Hub.Sector) Drives.Priced(this, _drive, _netPos, warping, age, Drives.SpeedCap(ReportTop, StrafeNow));
            else _drive.From = null;
            _netVel = Drives.Clamp(_drive, _netVel, ReportTop, StrafeNow);
        }
    }

    // The host's side of the conversation: hull, ability state, and the wing.
    // Re-used every report: four arrays the size of the class's slots, filled and sent (the wing
    // arrays beside them are built the same way). Allocating them per packet was 20 Hz of garbage.
    private float[] _netSlotLeft = System.Array.Empty<float>(), _netSlotCool = System.Array.Empty<float>(), _netSlotOwn = System.Array.Empty<float>();
    private int[] _netSlotN = System.Array.Empty<int>();

    private void SendHostState()
    {
        int n = _wings.Count;
        var pos = new Vector2[n]; var rot = new float[n]; var st = new int[n]; var rearm = new float[n];
        for (int i = 0; i < n; i++)
        {
            pos[i] = _wings[i].Position; rot[i] = _wings[i].Rotation;
            st[i] = _wings[i].StateCode; rearm[i] = (float)_wings[i].RearmLeft;
        }
        for (int i = 0; i < _slots.Length; i++)
        {
            _netSlotLeft[i] = (float)_slots[i].Left; _netSlotCool[i] = (float)_slots[i].Cool;
            _netSlotOwn[i] = (float)_slots[i].Own;   _netSlotN[i] = _slots[i].N;
        }
        (GetParent() as Hub)?.SendHostState(OwnerId, Hp, MaxHp, Alive, _stasis, _status.Bits, _combatT,
            _netSlotLeft, _netSlotCool, _netSlotOwn, _netSlotN, WingTarget?.NetId ?? 0, StrikeTarget?.NetId ?? 0,
            pos, rot, st, rearm);
    }

    // the host's report (Hub.NetHostState: only the host speaks for combat state)
    public void ApplyHostState(double hp, double maxHp, bool alive, double stasis, int statusBits, double combat,
                               float[] slotLeft, float[] slotCool, float[] slotOwn, int[] slotN,
                               int wingTarget, int strikeTarget, Vector2[] wingPos, float[] wingRot, int[] wingState, float[] wingRearm)
    {
        if (System.Math.Abs(maxHp - _hostMax) > 1e-9) _hullWatch = default;    // the first report, or a refit: not damage
        _hostMax = maxHp;
        _hullWatch.Tick(this, alive ? hp : 0, taken: true);
        Hp = hp; MaxHp = maxHp; Alive = alive; _stasis = stasis; _status.FromBits(statusBits); _combatT = combat;
        // The slots, by the class's own order. Shortest array wins: a refit mid-packet can leave
        // the two sides a slot apart for one report.
        // A RAMP row's Own is the one field the host does not speak for on the owner's own ship:
        // the owner steps it from its own helm (TickAbilities), which the host never sees.
        var rows = Abilities.For(Class);
        for (int i = 0; i < Math.Min(_slots.Length, slotLeft.Length); i++)
        {
            bool ownerStepped = Mine && i > 0 && i - 1 < rows.Length && rows[i - 1].Ramp != null;
            _slots[i] = new Slot { Left = slotLeft[i], Cool = slotCool[i], Own = ownerStepped ? _slots[i].Own : slotOwn[i], N = slotN[i] };
        }
        WingTarget = wingTarget != 0 ? Combat.ById(wingTarget) : null;
        // What the bombers were sent at. It was host-only, so a guest's own BOMB slot read
        // "RETURNING" for the whole of every strike it ordered -- the one word its bar had for it.
        StrikeTarget = strikeTarget != 0 ? Combat.ById(strikeTarget) : null;
        MatchSorties(wingPos, wingState);
        for (int i = 0; i < Math.Min(_wings.Count, wingPos.Length); i++)
        {
            _wings[i].SetNet(wingPos[i], wingRot[i]);
            _wings[i].SetNetState(wingState[i], wingRearm[i]);
        }
    }

    // SORTIE CRAFT come and go on the host alone (Sortie), so a guest's list is brought to the
    // host's report here: each state code carries its craft's row (Wing.RowCode), and a sortie craft
    // the host reports is added at its place, one it no longer reports is dropped. The fitted craft
    // are FitWings' on every peer, from the same sheet, and are left alone.
    private void MatchSorties(Vector2[] pos, int[] state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            int kind = state[i] / Wing.RowCode;
            if (kind < 0 || kind >= Wings.All.Length || !Wings.All[kind].Sortie) continue;
            while (i < _wings.Count && _wings[i].Def.Sortie && (int)_wings[i].Kind != kind) DropWing(i);
            if ((i >= _wings.Count || (int)_wings[i].Kind != kind) && AddWing((WingKind)kind, Math.Min(i, _wings.Count)))
                _wings[Math.Min(i, _wings.Count - 1)].Position = pos[i];
        }
        for (int i = _wings.Count - 1; i >= state.Length; i--) if (_wings[i].Def.Sortie) DropWing(i);
    }
    private void DropWing(int i) { var w = _wings[i]; _wings.RemoveAt(i); if (IsInstanceValid(w)) w.QueueFree(); }

    // Wings are parented to the world, not the ship, so they fly free of its
    // rotation. That means they do not leave with it: free them here, or a
    // departed player's fighters hang in the hub forever.
    public override void _ExitTree()
    {
        foreach (var w in _wings) if (IsInstanceValid(w)) w.QueueFree();
        _wings.Clear();
        if (IsInstanceValid(_pod)) _pod.QueueFree();
        _marked.Clear();
        _doses.Clear();
        Combat.Players.Remove(this);
    }

    // ── signal lights: a faint, flashing yellow/orange wherever a craft lands or
    // takes off -- the hangar for fighters, the runway or a bay for bombers ───────
    private readonly System.Collections.Generic.List<(Vector2 local, double t)> _signals = new();
    private const double SignalTime = 1.2;
    public void Signal(Vector2 world) => _signals.Add((ToLocal(world), SignalTime));
    public int SignalsLit => _signals.Count;

    public override void _Draw()
    {
        // EVERY FIELD ITS SLOTS HAVE UP (the bubble, ...): one row each in Fields.All (Fx.cs)
        Fields.Draw(this);
        // the drive: a warp's charge glow and landing flash on every peer, and the pilot's range (Drives.Draw)
        Drives.Draw(this, _drive);
        // engine plumes at the stern, in the accent colour
        if (Alive)
            Plume.Draw(this, new Vector2(0, MyArt.Length * 0.5f - MyArt.EngineInset), Vector2.Down, MyArt.Length, Accent,
                       0.25f + 0.75f * Mathf.Abs(SpeedAhead) / (float)Stats["max_speed"], Thrusting || Mathf.Abs(SpeedAhead) > 2f);
        Melee.Draw(this);                           // a blade or a spin swinging, on every peer
        ActiveReload.Draw(this);                    // an enhanced round's glint at the muzzle, on every peer
        foreach (var (p, t) in _signals)
        {
            bool yellow = (int)(t * 7) % 2 == 0;                           // flashing
            var c = yellow ? new Color(1f, 0.9f, 0.35f) : new Color(1f, 0.55f, 0.15f);
            float k = (float)(t / SignalTime);
            DrawCircle(p, 7f, new Color(c.R, c.G, c.B, 0.10f * k));            // nearly transparent
            DrawCircle(p, 2.5f, new Color(c.R, c.G, c.B, 0.35f * k));
        }
        if (!Alive)
        {   // the stasis countdown over the hull, upright whatever the heading
            DrawSetTransform(Vector2.Zero, -Rotation, Vector2.One);
            string t = CanReboard ? "READY  ·  F" : $"STASIS  {(int)_stasis / 60}:{(int)_stasis % 60:00}";
            Txt.Centre(this, ThemeDB.FallbackFont, new Vector2(0, -MyArt.Length * 0.5f - 14f), t, Txt.Size(14), new Color(0.6f, 0.85f, 1f));
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
        if (Mine) return;
        // a soft ring so other players read as players, not as fleet ships
        DrawArc(Vector2.Zero, 34f, 0, Mathf.Tau, 24, new Color(0.55f, 0.85f, 1f, 0.5f), 2f);
    }
}
