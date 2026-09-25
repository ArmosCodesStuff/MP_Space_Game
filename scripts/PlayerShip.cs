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
public partial class PlayerShip : Node2D, IHittable, IRaidTarget, ITagged, ITurretHost, IPrism
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
    // the V boost, the dart's boost, the wraith's veil. Every running ability that names a
    // stat for one of them (AbilityDef.RateStat, AbilityDef.SpeedStat) lifts it by that figure,
    // and the row knows its own slot, so the next buff is a ROW rather than an `if` naming a slot
    // id and a stat id by string.
    //   Lifts ADD (LiftShares; the owner's ruling): two x2 buffs at once are x3, not x4. A product
    // would have compounded every rate part the item pass adds.
    //   The rate reaches the reloads through ONE door, Cadence, which adds the lifts' shares to
    // the reload's own bonus -- so a part's +100% under an overdrive's x2 is x3 as well: the main
    // guns (FireControl), the point defence (Spec), every turret it has out (Deployed.Spec) and
    // every craft of its wing. SpeedMult lifts the top speed AND the thrust (Steer); StrafeMult the
    // slide's speed and thrust, by each row's StrafeStat where it names one (the boost's surge_strafe).
    public float FireRate => (float)Stat.Scale(Lifts(Lift.Rate));
    public float SpeedMult => (float)Stat.Scale(Lifts(Lift.Speed));
    public float StrafeMult => (float)Stat.Scale(Lifts(Lift.Strafe));
    private enum Lift { Rate, Speed, Strafe }
    private readonly List<double> _lifts = new();
    private double Lifts(Lift kind)
    {
        _lifts.Clear();
        foreach (var def in Abilities.For(Class))
        {
            ref var sl = ref Sl(def.Id);
            string stat = kind == Lift.Rate ? def.RateStat : kind == Lift.Speed ? def.SpeedStat : def.StrafeStat ?? def.SpeedStat;
            if (stat != null && sl.Left > 0 && (def.While == null || def.While(this))) _lifts.Add(Stats[stat]);
            // a RAMP's running total is already a share (F1, D18): 1 + it reads the same as any
            // other lift's raw multiplier would, and it keeps lifting through its post-run drain,
            // not only while Left > 0.
            if (kind != Lift.Rate && def.Ramp != null && sl.Own > 0) _lifts.Add(1 + sl.Own);
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
            if (def.While != null && !def.While(this)) continue;
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
                if (def.Hold < 1 && Sl(def.Id).Left > 0 && (def.While == null || def.While(this))) h *= def.Hold;
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
    public double Cadence(string intervalStat) => Stats.With(intervalStat, Lifts(Lift.Rate)
        + (_afterDrive > 0 && Items.IdsOf("@primary_rate", Class).Contains(intervalStat)
            ? Items.Shares(Items.Door.AfterDrive, Stats, default) : 0));
    private double _afterDrive;

    // ── THE CONDITIONS THIS SHIP'S GEAR LIFTS (Items.Conditions), each at its one door ──
    private IHittable _spinOn;                      // Spin-up Feed: the target, since when, and the last blow
    private double _spinSince, _spinLast;
    public double HullLeft => MaxHp > 0 ? Hp / MaxHp : 1;
    private Items.Blow BlowOn(IHittable target, double d) => new(target, HullLeft, d, MaxHp);
    // A blow this ship deals, weighed (Dealt.Deal): its Dealt rows added, and the primary's ramp. A
    // repeat (Items.Repeats: the echo's blast) is what was already weighed, and passes as it is.
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
        return d * (1 + share);
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

    // Missiles (destroyer): a magazine of bursts, reloaded by hand.
    public int MissilesLoaded => Sl("missile").N;
    public double MissileReloadLeft => Sl("reload").Left;
    public bool Reloading => MissileReloadLeft > 0;
    private bool CanFireMissile => Stats.Def.Has(Fit.Missiles) && MissilesLoaded > 0 && !Reloading && Sl("missile").Cool <= 0;

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
            if (def.While != null && !def.While(this)) continue;
            def.OnDealt(this, target, d, weapon);
        }
    }
    public PlayerShip Credit => this;

    public TurretSpec Spec(bool pd)
    {
        var art = MyArt;
        return new TurretSpec {
            Prey     = Targeting.PointDefence,        // what its PD takes: a main gun never picks
            Damage   = pd ? Stats["pd_damage"]   : Stats["main_damage"],
            Interval = Cadence(pd ? "pd_interval" : "main_interval"),
            Range    = (float)(pd ? Stats["pd_range"] : Stats["main_range"]),
            Turn     = (float)(pd ? Stats["pd_turn"]  : Stats["main_turn"]),
            ShellSpeed = (float)Stats["shell_speed"],
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
        Stats = BuildSheet();
        MaxHp = Stats["hull"];
        Hp = Alive ? Math.Max(1, frac * MaxHp) : MaxHp;
        _hullWatch = default;                      // a refit, not damage
        // The class's slots, index 0 the spare: a new one with every timer at zero and the magazine full.
        var list = Abilities.For(Class);
        _slots = new Slot[list.Length + 1];
        _slotAt.Clear();
        for (int i = 0; i < list.Length; i++) _slotAt[list[i].Id] = i + 1;
        _netSlotLeft = new float[_slots.Length]; _netSlotCool = new float[_slots.Length];
        _netSlotOwn = new float[_slots.Length]; _netSlotN = new int[_slots.Length];
        Sl("missile").N = (int)Stats["missile_mag"];
        foreach (var (id, at) in _slotAt)
            if (_slotsAway.Remove(id, out var kept))
            {
                ref var s = ref _slots[at];
                s.Cool = Math.Max(0, kept.slot.Cool - (_clock - kept.clock));
                s.N = kept.slot.N;                 // a magazine over the new sheet's is cut to it (FitWings)
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

        for (int i = 0; i < Math.Min((int)Stats["main_count"], art.Mains.Length); i++) AddTurret(art.Mains[i], false);
        for (int i = 0; i < Math.Min((int)Stats["pd_count"], art.Pds.Length); i++) AddTurret(art.Pds[i], true);
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
        Sl("missile").N = Math.Min(MissilesLoaded, (int)Stats["missile_mag"]);
        foreach (var w in _wings) if (w.Def.AmmoStat != null) w.Ammo = Math.Min(w.Ammo, (int)Stats[w.Def.AmmoStat]);
    }

    private void AddTurret(Vector2 offset, bool pd)
    {
        var t = new Turret(); AddChild(t); t.Setup(this, offset, pd);
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
    public void UseAbility(string id, int targetId)
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
        if (Net.Sim) DoAbility(id, targetId);
        else Net.AskHost(this, nameof(RequestAbility), id, targetId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestAbility(string id, int targetId)
    {
        if (Net.FromPlayer(this, out int who) && who == OwnerId) DoAbility(id, targetId);
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

    private void DoAbility(string id, int targetId)
    {
        var def = Abilities.Find(Class, id);
        if (def == null || !Net.Sim || (!Alive && !def.WhenWrecked) || PressHeld(def)
            || Unlocks.LockedAt(Class, Peak, def) != null) return;
        var target = targetId != 0 ? Combat.ById(targetId) : null;
        // ANY OTHER OF THE CLASS'S ABILITIES ends a running stance (the lunge and the whirlwind end the
        // prism's): read by the row's Stance, never by an id. The drive (V) and the weapon's own rows do not.
        if (def.Stance == null && !def.Weapon && Array.IndexOf(Classes.Of(Class).Abilities, def) >= 0
            && def.Refuse?.Invoke(this, target) == null)
            foreach (var st in Abilities.For(Class))
                if (st.Stance != null && Sl(st.Id).Left > 0) EndStance(st.Id);
        def.Press?.Invoke(this, target);
    }

    // ── what the abilities do. The catalogue (Abilities.cs) points at these, and each one
    // guards itself: the press arrives from a guest's keyboard, so the host never trusts it.
    // DISABLED, a pilot presses nothing (the wreck's own reboard aside). Its point defence, its wing
    // and its turrets already out are not presses, and fight on.
    private bool PressHeld(AbilityDef def) => Disabled && !def.WhenWrecked;
    public void Reboard() { if (CanReboard) { Alive = true; Hp = MaxHp * ReboardHull; _stasis = 0; } }
    public void StartBroadside() { if (BroadsideReady) Sl("broadside").Left = Stats["broadside_windup"]; }
    public void StartReload()
    {
        if (Stats.Def.Has(Fit.Missiles) && !Reloading && MissilesLoaded < (int)Stats["missile_mag"])
            Sl("reload").Left = Stats["missile_reload"];
    }
    public void OrderAttack(IHittable t) { if (Stats.Def.Has(Fit.Wing) && t != null) { WingTarget = t; _attacking = true; } }
    public void RecallWing() { WingTarget = null; _attacking = false; }
    // ── THE FREIGHTERS ──────────────────────────────────────────────────
    private Hub MyHub => GetParent() as Hub;

    // THE SENTRY THROW (F14). R with the cursor on open space throws one from the hull to the
    // cursor, clamped to deploy_reach on the same bearing; it lands deploy_flight later, and in
    // flight it is no body (nothing hits it, it fires at nothing). R with the cursor within
    // recall_pick of one of yours recalls it: never refused, and its hull is stowed for the next
    // throw. The host keeps the throws in flight; every peer sees the landing mark (Fx.AimZone).
    private readonly List<(Vector2 At, double Left, double Hp, double Max)> _throws = new();
    private readonly Stack<(double Hp, double Max)> _stowed = new();

    // How many of this pilot's turrets are standing or in flight.
    public int TurretsOut
    {
        get
        {
            var h = MyHub; if (h == null) return 0;
            int n = _throws.Count; foreach (var t in h.Deployed) if (t.OwnerId == OwnerId) n++;
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
            _stowed.Push((mine.Hp, mine.MaxHp));
            MyHub?.DeployedTaken(mine);
            return;
        }
        if (Sl("deploy").Cool > 0 || TurretsOut >= (int)Stats["deploy_max"]) return;
        var at = ThrowPoint(cursor);
        double flight = Stats["deploy_flight"];
        var (hp, max) = _stowed.Count > 0 ? _stowed.Pop() : (Stats["deploy_hull"], Stats["deploy_hull"]);
        _throws.Add((at, flight, hp, max));
        Fx.Warn(new FxRaise { Id = Fx.AimZone, At = at, To = at, Size = DeployedTurret.Radius, Time = flight });
        Sl("deploy").Cool = Cooling(Stats["deploy_cooldown"]);
    }
    // host, each frame: a throw whose flight is over lands as a turret
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
    // Every bubble that covers `victim` spends itself on this blow before the hull sees it.
    private static double ThroughBubbles(PlayerShip victim, double d)
    {
        foreach (var h in Combat.Players)
            if (d > 0 && h is PlayerShip p && p.BubbleUp && victim.Position.DistanceTo(p.Position) <= p.BubbleRadius)
                d = p.SpendBubble(d);
        return d;
    }

    public void StartOverdrive()
    {
        if (Sl("overdrive").Cool > 0) return;
        ref var o = ref Sl("overdrive");
        o.Left = Stats["overdrive_time"]; o.Cool = Cooling(Stats["overdrive_cooldown"]);
    }

    // Everything within reach is thrown clear -- and what is too big to throw (a boss) is held
    // still instead, which is what the reach is really for.
    public void Shockwave()
    {
        if (Sl("shockwave").Cool > 0) return;
        Sl("shockwave").Cool = Cooling(Stats["wave_cooldown"]);
        float reach = (float)Stats["wave_range"], push = (float)Stats["wave_push"];
        // Targeting.Throwable, not the raw list: nothing in flight is thrown, a missile or a body
        // with a hull of its own. THROWING one was worse than hitting it -- the host moved a live
        // hostile seeker 1000 u while every guest flew its own copy along the old path, so the two
        // peers held a damaging missile a thousand units apart.
        foreach (var h in new List<IHittable>(Targeting.Hittable(Combat.Hostiles, Targeting.Throwable)))
        {
            if (h.Position.DistanceTo(Position) > reach) continue;
            if (TagExt.Is(h, Tag.Boss)) { (h as IStatused)?.ApplyStatus(Status.Disabled, Stats["wave_disable"]); continue; }
            if (h is Node2D n)
            {
                var away = n.Position - Position;
                n.Position += (away.LengthSquared() > 1f ? away.Normalized() : Vector2.Up) * push;
            }
        }
        Fx.Raise(Fx.Wave, Position, reach);                 // the ring, on every peer: how far it threw them
    }

    // ── THE HEAVY FIGHTERS ──────────────────────────────────────────────
    // The railgun commits: while it charges the ship cannot turn or thrust (its row's Hold of x0,
    // taken after the lifts: see Held), and at the end everything on the line takes the whole of it.
    public void ChargeRail()
    {
        if (Sl("railgun").Left > 0 || Sl("railgun").Cool > 0) return;
        Sl("railgun").Left = Stats["rail_charge"];
    }
    // The charge is spent (the Railgun row's Expire, on the host): the band its charge reached
    // (Charges.Of("railgun")) says how much of rail_damage goes down which line, from the nose
    // along the heading, as far as rail_range.
    public void FireRail()
    {
        double charged = 1 - Sl("railgun").Left / Stats["rail_charge"];
        var (mult, line) = Charges.At(Charges.Of("railgun"), charged);
        var nose = Aim.Nose(this, MyArt.Length * 0.5f);
        Lines.Strike(line, this, nose, nose + Vector2.Up.Rotated(Rotation) * (float)Stats["rail_range"], Stats["rail_damage"] * mult);
        Sl("railgun").Cool = Cooling(Stats["rail_cooldown"]);
    }

    // Six missiles, each on a target of its own while there are targets to go round; what is left
    // over goes at the nearest one.
    // WHAT THE CELL CAN SEE, in one place, because the slot has to say "NOTHING IN REACH" before
    // the press and the press has to find the same things afterwards. Targeting.Attackable, not
    // WingPrey: a seeker hits once and dies, so a practice dummy is a perfectly good target for it.
    public List<IHittable> SeekerPrey()
    {
        float range = (float)Stats["hunter_range"];
        var seen = new List<IHittable>();
        foreach (var h in Targeting.Choosable(Combat.Hostiles, Targeting.Attackable))
            if (Position.DistanceTo(h.Position) <= range) seen.Add(h);
        return seen;
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
                                 t.NetId, (float)Stats["hunter_turn"], heavy: false, hostile: false, source: this);
        }
    }

    // ── THE LIGHTS ──────────────────────────────────────────────────────
    // The roll: nothing can touch it while it turns, and it comes out faster and firing quicker.
    public void BarrelRoll()
    {
        if (Sl("roll").Cool > 0) return;
        ref var r = ref Sl("roll");
        r.Left = Stats["roll_time"] + Stats["boost_time"];      // the roll, then the boost
        r.Cool = Cooling(Stats["roll_cooldown"]);
        ApplyStatus(Status.Evading, Stats["roll_time"]);
    }

    // The echo remembers what this ship dealt, through its own row's OnDealt (AbilityDef.OnDealt,
    // called by NoteDealt while Sl("echo").Left > 0), and puts all of it down at once, where the
    // last of it landed (Slot.At).
    public void StartEcho()
    {
        if (Sl("echo").Cool > 0) return;
        ref var e = ref Sl("echo");
        e.Left = Stats["echo_time"]; e.Own = 0; e.Cool = Cooling(Stats["echo_cooldown"]); e.At = Position;
    }
    // The echo's time is up (the Echo row's Expire, on the host; Left is already 0 here, so this
    // blast does not re-store itself through NoteDealt). What it remembered is its OWN slot's Own
    // and At, so the row hands it nothing but the ship and no number travels through the tick.
    public void Detonate()
    {
        ref var e = ref Sl("echo");
        double stored = e.Own; e.Own = 0; var at = e.At;
        if (stored <= 0) return;
        double blast = stored * Stats["echo_share"];
        float reach = (float)Stats["echo_radius"];
        NoteCombat();                                       // detonating is combat, whether or not it lands
        foreach (var h in new List<IHittable>(Targeting.Hittable(Combat.Hostiles, Targeting.Attackable)))
        {
            if (h.Position.DistanceTo(at) > reach) continue;
            Dealt.Deal(h, blast, this, Dealt.Echo);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
        Fx.Raise(Fx.Echo, at, reach);                       // on every peer, where it remembered
    }

    public void GoDark()
    {
        if (Sl("stealth").Cool > 0) return;
        ref var s = ref Sl("stealth");
        s.Left = Stats["stealth_time"]; s.Cool = Cooling(Stats["stealth_cooldown"]);
        ApplyStatus(Status.Untargetable, s.Left);
    }

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
    // THE PAINT (F14): one hostile at a time, for so long -- what its sentries take first
    // (DeployedTurret.Prefer). Host. The spotter's hit raises it (6b); nothing else reads it.
    private IHittable _paint;
    private double _paintLeft;
    public IHittable Painted => _paintLeft > 0 && _paint != null && _paint.Alive && Combat.Hostiles.Contains(_paint) ? _paint : null;
    public void PaintOn(IHittable t, double seconds) { _paint = t; _paintLeft = seconds; }

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

    // A BURST (destroyer): three missiles off the nose, one straight at the target and two launched
    // up to 70 degrees to either side, all guided -- each heading turns toward the target at
    // missile_turn rad/s, so the outer two curve in onto it from the flanks. One burst spends one
    // of the magazine.
    //   A missile heading theta off a target d away can only come round onto it if d > 2 r sin(theta),
    // r being its turning radius (speed / turn); any closer and it circles the target until its run
    // ends. So close in, the fan narrows to what the missiles can still turn through, with a margin
    // (BurstSplayFor): the full 70 degrees from about 250 u out on the kit's rack.
    public static readonly int[] BurstSides = { 0, -1, 1 };
    public const float BurstSplay = 70f;
    public static float BurstSplayFor(float d, float speed, float turn)
    {
        float reach = 0.8f * d / (2f * speed / Mathf.Max(0.01f, turn));
        return reach >= Mathf.Sin(Mathf.DegToRad(BurstSplay)) ? BurstSplay : Mathf.RadToDeg(Mathf.Asin(reach));
    }
    public void FireMissile(IHittable t)
    {
        if (!Net.Sim || !CanFireMissile) return;
        float range = (float)Stats["missile_range"];
        if (t == null || Position.DistanceTo(t.Position) > range) return;   // a target in range, or nothing
        Sl("missile").N--; Sl("missile").Cool = Stats["missile_refire"];
        var nose = ToGlobal(new Vector2(0, -MyArt.Length * 0.5f));
        float speed = (float)Stats["missile_speed"], turn = (float)Stats["missile_turn"];
        float splay = BurstSplayFor(nose.DistanceTo(t.Position), speed, turn);
        foreach (int side in BurstSides)
            Combat.LaunchTorpedo(nose, (t.Position - nose).Rotated(Mathf.DegToRad(side * splay)), speed, range * 1.4f,
                                 Stats["missile_damage"], t.NetId, turn, heavy: true, source: this);
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
    // StatusSet.Guards (Statuses.cs), walked in the order written there -- evasion first, because
    // it decides whether the blow happened at all, then the shares, then the bubbles. The share is
    // the one its APPLIER named (a taunt's taunt_guard 0.67) and the row's default where
    // none was named -- never a row read off this hull's own sheet.
    private double Guarded(double d)
    {
        d *= 1 - Math.Min(1, Items.Shares(Items.Door.Taken, Stats, BlowOn(null, d)));   // Ablative Skin: a small hit
        foreach (var g in StatusSet.Guards)
        {
            if (!_status.Has(g.Status)) continue;
            d *= _status.ShareOf(g.Status, g.Share);
            if (d <= 0) return 0;
        }
        return ThroughBubbles(this, d);                                  // a bubble over it spends first
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
        if (Net.Sim) { _status.Tick(delta); HoldWeb(delta); _paintLeft = System.Math.Max(0, _paintLeft - delta); TickThrows(delta); }
        // the drive's clocks run on every peer: the landing flash used to fade only on the owner's,
        // and a remote ship's warp left it lit for good
        Drives.Tick(_drive, delta);
        UpdatePod();
        if (Mine) { Drives.TickOwner(this, _drive, DriveHeld || (!Demo && !Hub.ControlsLocked && Input.IsKeyPressed(Key.V)), delta); LocalFlight(dt); }
        else      RemoteFollow(dt);

        TickAbilities(delta);

        if (Mine && Abilities.TriggerOf(Class)?.Reload is { } view) ActiveReload.Step(this, view, delta);
        if (Net.Sim) { FireControl(delta); Swings(delta); DashSweeps(); ChargeLatch(delta); }
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
    // the railgun's shot, the echo's blast, the magazine a reload refills). Every
    // peer counts down so a guest's bars move smoothly between host packets, and the next packet
    // corrects any drift. A timed ability is a row and nothing else: this loop is the only expiry.
    private void TickAbilities(double delta)
    {
        _afterDrive = Math.Max(0, _afterDrive - delta);
        foreach (var def in Abilities.For(Class))
        {
            ref var sl = ref Sl(def.Id);
            if (sl.Cool > 0) sl.Cool = Math.Max(0, sl.Cool - delta);
            // A RAMP's running total (F1, D18) steps every frame regardless of Left, so it keeps
            // draining after the row stops -- Steer already ran this frame (LocalFlight, above),
            // so _yawRate is this frame's, not last frame's. OWNER-STEPPED: only the peer that
            // holds the helm (Mine) has a throttle and a yaw to read; every other copy of the
            // ship (the host's of a guest's included) never steps it, and ApplyHostState leaves
            // the owner's own value alone (DESIGN.md, the authority model).
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
            if (Staggered) { _mains[_nextBarrel % _mains.Count].Shoot(); _nextBarrel = (_nextBarrel + 1) % _mains.Count; }
            else foreach (var m in _mains) m.Shoot();
            _gunCd += step;
        }
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

    // THE WHIRLWIND'S PRESS (host): its time up, its cooldown, its first blow at once; every web on
    // the hull let go (the pin and the web's own ask), and none may take it until the spin is over.
    public void Whirl()
    {
        ref var sl = ref Sl("whirlwind");
        if (sl.Left > 0 || sl.Cool > 0) return;
        sl.Left = Stats["whirl_time"]; sl.Cool = Cooling(Stats["whirl_cooldown"]); sl.Own = 0;
        _webAsked = 0; _webPhase = 0; _status.Clear(Status.Pinned);
        _status.Apply(Status.Unwebbed, sl.Left);
    }

    // ── a stance (AbilityDef.Stance: the prism) ─────────────────────────────
    // THE PRESS, on the host: up (its Time, its Status for that Time, no split yet), or, pressed while it
    // runs, dropped. Its cooldown is set where it ENDS (EndStance), never here.
    public void Stance(string id)
    {
        var st = Abilities.Find(Class, id)?.Stance;
        ref var sl = ref Sl(id);
        if (st == null) return;
        if (sl.Left > 0) { EndStance(id); return; }
        if (sl.Cool > 0) return;
        sl.Left = Stats[st.Time]; sl.N = 0; sl.Own = 0;
        _status.Apply(st.Holds, sl.Left);
    }
    // IT ENDS: dropped, ended by another press, or run out (the row's Elapsed, on every peer, so every bar
    // shows the cooldown at once). The cooldown runs from here; the host lets the Status go.
    public void EndStance(string id)
    {
        var st = Abilities.Find(Class, id)?.Stance;
        if (st == null) return;
        ref var sl = ref Sl(id);
        sl.Left = 0; sl.Cool = Cooling(Stats[st.Cooldown]);
        if (Net.Sim) _status.Clear(st.Holds);
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
            if (def.Stance is not { } st || Sl(def.Id).Left <= 0) continue;
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
            if (def.Dash is { } d && Sl(def.Id).Left > 0) DashSweep(def.Id, 1 - Sl(def.Id).Left / Stats[d.Time]);
    }
    // THE OWNER'S CARRY: from the first frame it sees a dash row's Left (the host's press, here at once
    // on the host, a packet later on a guest), the hull goes the row's Reach along its nose over the
    // row's whole Time, whatever the lifts, the helm or its speed -- exactly Reach, the last frame's step
    // cut to what is left. A fresh press is told from a stale packet by its cooldown jumping back up.
    private AbilityDef _dashRow;
    private double _dashLeft, _dashCool0 = double.NegativeInfinity, _dashAt;
    private bool DashCarry(float dt)
    {
        if (Disabled) { _dashLeft = 0; return false; }
        if (_dashLeft <= 0)
            foreach (var def in Abilities.For(Class))
            {
                if (def.Dash == null || Sl(def.Id).Left <= 0) continue;
                if (Sl(def.Id).Cool <= _dashCool0 - (_clock - _dashAt) + 1.0) continue;    // the dash already flown
                _dashRow = def; _dashLeft = Stats[def.Dash.Time]; _dashCool0 = Sl(def.Id).Cool; _dashAt = _clock;
                break;
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
        Thrusting = throttle != 0f;
        if (!DashCarry(dt)) Steer(throttle, rudder, strafe, dt);   // a dash carries the hull instead of the helm

        // the main guns aim at the cursor; the hull does not follow it
        if (!Demo) Trigger = false;              // a display ship's driver owns the trigger
        if (!locked)
        {
            AimPoint = GetGlobalMousePosition();
            Trigger = Abilities.TriggerOf(Class) is { } trig && Stroke(trig, Input.IsKeyPressed(Abilities.KeyFor(Class, trig.Id)));
        }

        SendState(dt);
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
        // stasis: a cold, pulsing blue; no turrets
        if (_sprite != null)
            _sprite.Modulate = Alive ? Main : new Color(0.45f, 0.62f, 0.95f, 0.55f + 0.12f * Mathf.Sin(Time.GetTicksMsec() / 300f));
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
        if (throttle > 0) along += (float)Stats["thrust"] * lift * throttle * dt;
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
            if (Hub.PeerSector(OwnerId) == Hub.Sector) Drives.Priced(this, _drive, _netPos, warping, age, Drives.SpeedCap(TopNow, StrafeNow));
            else _drive.From = null;
            _netVel = Drives.Clamp(_drive, _netVel, TopNow, StrafeNow);
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
