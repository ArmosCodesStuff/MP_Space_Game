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
public partial class PlayerShip : Node2D, IHittable, IRaidTarget, ITagged, ITurretHost
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
    public const double StasisTime = 120, ReboardHull = 0.33;

    // ── "in combat": dealt or took damage in the last CombatHold seconds (host) ──
    // Out of combat after 12 s: the one global rule (music and regeneration use it).
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
    private double _combatT;
    public bool InCombat => _combatT > 0;
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
    private Vector2 _echoAt;                      // where this ship's last shot landed

    // WHAT MULTIPLIES ITS RATE OF FIRE, and WHAT MULTIPLIES ITS TOP SPEED, right now -- the
    // tender's overdrive, the warrior's rush, the dart's boost. Every running ability that names
    // a stat for one of them (AbilityDef.RateStat, AbilityDef.SpeedStat) multiplies it, and the
    // row knows its own slot, so the third rate buff is a ROW rather than a third `if` naming a
    // slot id and a stat id by string. FireRate is read by this ship's guns, its point defence
    // and every turret it has out (Spec, Deployed.Spec).
    public float FireRate => Multiplied(rate: true);
    public float SpeedMult => Multiplied(rate: false);
    private float Multiplied(bool rate)
    {
        float k = 1f;
        foreach (var def in Abilities.For(Class))
        {
            string stat = rate ? def.RateStat : def.SpeedStat;
            if (stat == null || Sl(def.Id).Left <= 0) continue;
            if (def.While != null && !def.While(this)) continue;
            k *= (float)Stats[stat];
        }
        return k <= 0 ? 1f : k;      // a sheet with no such row answers 0: unbuffed, never stopped
    }

    // ── ABILITY SLOTS ────────────────────────────────────────────────────
    // The state of every ability this class carries, in the class's own order. Each ability
    // counts the same four things: how long it has LEFT running, how long it must COOL before it
    // is ready again, one number it names itself (the gap to the next volley, damage stored, a
    // charge) and a COUNT (bursts in the magazine, volleys still to fire). A new ability adds no
    // field here and nothing to the wire -- it names its slot and uses the four.
    //
    // Slot 0 is the SPARE: an id this class does not carry lands there, so asking a battleship
    // about its magazine is harmless rather than a crash.
    public struct Slot { public double Left, Cool, Own; public int N; }
    private Slot[] _slots = new Slot[1];                 // the spare alone, until the class is fitted
    private readonly Dictionary<string, int> _slotAt = new();
    public ref Slot Sl(string id) => ref _slots[_slotAt.TryGetValue(id, out int i) ? i : 0];

    // Point defence: an active ability. Active window, then recharge.
    public bool PdActive => Sl("pd").Left > 0;
    public bool PdReady => Sl("pd").Left <= 0 && Sl("pd").Cool <= 0;
    public double PdLeft => Sl("pd").Left;
    public double PdRechargeLeft => Sl("pd").Cool;
    public float PdActiveFrac => (float)(PdLeft / Math.Max(0.001, Stats["pd_active"]));
    public float PdRechargeFrac => (float)(PdRechargeLeft / Math.Max(0.001, Stats["pd_reload"]));

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
    // A class whose point defence never switches off (the warden) has no window to open and no
    // ring to show: it simply fires, at whatever damage its row gives it.
    public bool PdOnline => Stats.Def.Has(Fit.AlwaysPd) || PdActive;
    public float PdRing => Stats.Def.Has(Fit.AlwaysPd) ? 0f : PdActive ? PdActiveFrac : PdRechargeFrac;
    public Vector2 AimAt => AimPoint;
    // Through a broadside the main turrets come round onto the cursor from anywhere within the
    // wind-up -- half a turn in its time -- or at their own rate if that is already faster.
    public float FastSwing => BroadsideTracking ? (float)(Math.PI / Math.Max(0.05, Stats["broadside_windup"])) : 0f;
    public IReadOnlyList<Turret> Siblings => PdTurrets;
    // WHAT THIS SHIP JUST DEALT, and where it landed. Every weapon that knows whose it is comes
    // through here -- a turret, a shell, a torpedo -- so the echo has one place to listen and
    // "it is in combat" has one place to be set.
    public void NoteDealt(double d, Vector2 at)
    {
        NoteCombat();
        ref var echo = ref Sl("echo");
        if (echo.Left > 0) { echo.Own += d; _echoAt = at; }
    }
    public PlayerShip Credit => this;

    public TurretSpec Spec(bool pd)
    {
        var art = MyArt;
        return new TurretSpec {
            Damage   = pd ? Stats["pd_damage"]   : Stats["main_damage"],
            Interval = (pd ? Stats["pd_interval"] : Stats["main_interval"]) / FireRate,
            Range    = (float)(pd ? Stats["pd_range"] : Stats["main_range"]),
            Turn     = (float)(pd ? Stats["pd_turn"]  : Stats["main_turn"]),
            ShellSpeed = (float)Stats["shell_speed"],
            Texture  = pd ? art.PdTurret : art.MainTurret,
            TexScale = art.TurretTexScale,
            Barrel   = pd ? art.PdBarrel : art.MainBarrel,
            Ring     = art.PdRing,
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

        Stats = BuildSheet();
        MaxHp = Hp = Stats["hull"];
        _hullWatch = default;                      // a new hull, not damage
        // The class's slots, fresh: every timer at zero, the magazine full. Index 0 is the spare.
        var list = Abilities.For(Class);
        _slots = new Slot[list.Length + 1];
        _slotAt.Clear();
        for (int i = 0; i < list.Length; i++) _slotAt[list[i].Id] = i + 1;
        _netSlotLeft = new float[_slots.Length]; _netSlotCool = new float[_slots.Length];
        _netSlotOwn = new float[_slots.Length]; _netSlotN = new int[_slots.Length];
        Sl("missile").N = (int)Stats["missile_mag"];

        var art = MyArt;
        var tex = GD.Load<Texture2D>(art.Texture);
        _sprite.Texture = tex;
        _sprite.Scale = Vector2.One * (art.Length / tex.GetHeight());
        _sprite.Modulate = Main;
        if (!IsInstanceValid(_shield)) { _shield = new ShieldFlash(); AddChild(_shield); }
        _shield.HalfWidth = art.HalfWidth; _shield.HalfLength = art.Length * 0.5f; _shield.Tint = Accent;

        for (int i = 0; i < Math.Min((int)Stats["main_count"], art.Mains.Length); i++) AddTurret(art.Mains[i], false);
        for (int i = 0; i < Math.Min((int)Stats["pd_count"], art.Pds.Length); i++) AddTurret(art.Pds[i], true);
        FitWings(fresh: true);
    }

    // THE ONE SHEET: class, gear and purchases. Every ship carries its pilot's gear and purchases
    // (they come with the identity); your own adds Character.Bonuses. The title screen's ship
    // (Demo) flies the kit, whatever the pilot has fitted.
    private ShipStats BuildSheet()
    {
        if (Mine && !Demo) _loadout = (string[])Character.LoadoutFor(Class).Clone();   // a COPY: the window edits the saved one in place
        var pct = Equipment.Bonuses(Class, Loadout);
        if (Mine && !Demo) pct = ShipStats.Sum(pct, Character.Bonuses);
        pct = ShipStats.Sum(pct, Progression.Shares(_bought, Class));       // the pilot's own percentages
        return new ShipStats(Class, pct, ShipStats.Sum(Progression.Flats(_bought, Class), Equipment.Adds(Class, Loadout)));
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
        while (WingCount(WingKind.Bomber) < wantB && AddWing(WingKind.Bomber, _wings.Count)) if (!fresh) _wings[^1].StartRearm();
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
    private void RemoveWing(WingKind k)
    {
        int i = _wings.FindLastIndex(x => x.Kind == k);
        var w = _wings[i]; _wings.RemoveAt(i);
        if (IsInstanceValid(w)) w.QueueFree();
    }

    public void SetClass(ShipClass c) { Class = c; FitClass(); }

    // The pilot's purchased upgrades (Progression). The host needs them too: it resolves
    // hull and damage. Changing them refits the ship (Restat).
    private int[] _bought = new int[Progression.All.Length];
    public int[] Bought => _bought;
    public void SetProgress(int[] bought)
    {
        var b = new int[Progression.All.Length];
        for (int i = 0; i < b.Length && i < (bought?.Length ?? 0); i++) b[i] = Math.Clamp(bought[i], 0, Progression.MaxPerUpgrade);
        if (b.AsSpan().SequenceEqual(_bought)) return;
        _bought = b;
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

    // Equipment: the owner's saved loadout for this class (on the host, what the identity
    // carried). A change refits.
    private string[] _loadout;
    public string[] Loadout => _loadout ?? Equipment.Default(Class);
    public void SetEquipment(string[] ids)
    {
        var l = Equipment.Sanitize(Class, ids);
        if (_loadout != null && l.AsSpan().SequenceEqual(_loadout)) return;
        _loadout = l;
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
        // A LOCAL ability is the owner's own intent (the fire mode): it rides in the state report
        // rather than being asked for.
        if (def.Local) { def.Press?.Invoke(this, null); return; }
        // Why it cannot be pressed, decided on the OWNER's machine so the slot says so at once.
        // The host checks again inside Press: this is a courtesy, never the guard.
        if (def.Refuse?.Invoke(this, targetId != 0 ? Combat.ById(targetId) : null) is { } why) { Fail(id, why); return; }
        if (Net.Sim) DoAbility(id, targetId);
        else Net.AskHost(this, nameof(RequestAbility), id, targetId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestAbility(string id, int targetId)
    {
        if (Net.FromPlayer(this, out int who) && who == OwnerId) DoAbility(id, targetId);
    }

    // THE GATE, not a courtesy. Refuse (above) runs on the owner so the slot says why at once;
    // this is what the host is held to. A wreck presses nothing but the one ability declared for
    // it (AbilityDef.WhenWrecked -- reboard). The nine newest abilities each repeated `!Net.Sim ||
    // !Alive` in their own body and the seven oldest never did, so a guest in stasis could fire a
    // missile burst, switch on point defence and order a bomber strike out of its own wreck.
    // WHAT A COOLDOWN REALLY IS, once the pilot's COOLING points are in it: the sheet's share of
    // the row's seconds, never below a floor. Every ability that sets a cooldown reads it here
    // rather than each one multiplying for itself.
    public const double CoolFloor = 0.5;              // half the row's seconds, whatever is bought
    public double Cooling(double seconds) => seconds * System.Math.Max(CoolFloor, Stats["cooldown_share"]);

    private void DoAbility(string id, int targetId)
    {
        var def = Abilities.Find(Class, id);
        if (def == null || !Net.Sim || (!Alive && !def.WhenWrecked)) return;
        def.Press?.Invoke(this, targetId != 0 ? Combat.ById(targetId) : null);
    }

    // ── what the abilities do. The catalogue (Abilities.cs) points at these, and each one
    // guards itself: the press arrives from a guest's keyboard, so the host never trusts it.
    public void Reboard() { if (CanReboard) { Alive = true; Hp = MaxHp * ReboardHull; _stasis = 0; } }
    public void StartPd() { if (PdReady) Sl("pd").Left = Stats["pd_active"]; }
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

    // How many of this pilot's turrets are standing, and the one it is sitting over (C).
    public int TurretsOut
    {
        get
        {
            var h = MyHub; if (h == null) return 0;
            int n = 0; foreach (var t in h.Deployed) if (t.OwnerId == OwnerId) n++;
            return n;
        }
    }
    public DeployedTurret NearestOwnTurret()
    {
        var h = MyHub; if (h == null) return null;
        DeployedTurret best = null; float bd = (float)Stats["collect_range"];
        foreach (var t in h.Deployed)
        {
            if (t.OwnerId != OwnerId) continue;
            float d = Position.DistanceTo(t.Position);
            if (d <= bd) { bd = d; best = t; }
        }
        return best;
    }
    public void DeployTurret()
    {
        if (!Stats.Def.Has(Fit.Deploy)) return;
        if (Sl("deploy").Cool > 0 || TurretsOut >= (int)Stats["deploy_max"]) return;
        MyHub?.Drop(this, Position, Stats["deploy_hull"]);
        Sl("deploy").Cool = Cooling(Stats["deploy_cooldown"]);
    }
    public void CollectTurret()
    {
        if (!Stats.Def.Has(Fit.Deploy)) return;
        if (NearestOwnTurret() is { } t) MyHub?.DeployedTaken(t);
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
        // Targeting.Attackable, not the raw list: a missile in flight is point defence's business,
        // and THROWING one was worse than hitting it -- the host moved a live hostile seeker 1000 u
        // while every guest flew its own copy along the old path, so the two peers held a damaging
        // missile a thousand units apart.
        foreach (var h in new List<IHittable>(Targeting.All(Combat.Hostiles, Targeting.Attackable)))
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
    // The railgun commits: while it charges the ship cannot turn or thrust (Status.Disabled on
    // itself), and at the end everything on the line takes the whole of it at once.
    public void ChargeRail()
    {
        if (Sl("railgun").Left > 0 || Sl("railgun").Cool > 0) return;
        Sl("railgun").Left = Stats["rail_charge"];
        ApplyStatus(Status.Disabled, Stats["rail_charge"]);
    }
    // The charge is spent (the Railgun row's Expire, on the host).
    public void FireRail()
    {
        var a = Aim.Nose(this, MyArt.Length * 0.5f);
        var b = a + Vector2.Up.Rotated(Rotation) * (float)Stats["rail_range"];
        float halfWidth = (float)Stats["rail_width"] * 0.5f;
        foreach (var h in new List<IHittable>(Targeting.All(Combat.Hostiles, Targeting.Attackable)))
        {
            if (Combat.DistToSegment(h.Position, a, b) <= halfWidth + h.HitRadius)
            {
                h.TakeDamage(Stats["rail_damage"]);
                NoteDealt(Stats["rail_damage"], h.Position);
                if (h is Node2D n) Popups.NoteImpact(n, h.Position);
            }
        }
        Fx.Line(Fx.Rail, a, b);                             // the line it threw, on every peer
        Sfx.Laser(a, b, ShotSound.Boss);
        Sl("railgun").Cool = Cooling(Stats["rail_cooldown"]);
    }

    public void StartRush()
    {
        if (Sl("rush").Cool > 0) return;
        ref var r = ref Sl("rush");
        r.Left = Stats["rush_time"]; r.Cool = Cooling(Stats["rush_cooldown"]);
        ApplyStatus(Status.Hardened, r.Left);
    }
    // The rush ends in an EMP: everything close takes it, and everything small enough is held.
    // (The Rush row's Expire, on the host.)
    public void RushEmp()
    {
        float reach = (float)Stats["emp_range"];
        foreach (var h in new List<IHittable>(Targeting.All(Combat.Hostiles, Targeting.Attackable)))
        {
            if (h.Position.DistanceTo(Position) > reach) continue;
            h.TakeDamage(Stats["emp_damage"]);
            NoteDealt(Stats["emp_damage"], h.Position);
            if (!TagExt.Is(h, Tag.Boss)) (h as IStatused)?.ApplyStatus(Status.Disabled, Stats["emp_stun"]);
        }
        Fx.Raise(Fx.Emp, Position, reach);
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
        foreach (var h in Targeting.All(Combat.Hostiles, Targeting.Attackable))
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

    // The echo remembers what this ship dealt (NoteDealt) and puts all of it down at once, where
    // the last of it landed.
    public void StartEcho()
    {
        if (Sl("echo").Cool > 0) return;
        ref var e = ref Sl("echo");
        e.Left = Stats["echo_time"]; e.Own = 0; e.Cool = Cooling(Stats["echo_cooldown"]);
        _echoAt = Position;
    }
    // The echo's time is up (the Echo row's Expire, on the host). What it remembered is its OWN
    // slot's Own, so the row hands it nothing but the ship and no number travels through the tick.
    public void Detonate()
    {
        ref var e = ref Sl("echo");
        double stored = e.Own; e.Own = 0;
        if (stored <= 0) return;
        double blast = stored * Stats["echo_share"];
        float reach = (float)Stats["echo_radius"];
        foreach (var h in new List<IHittable>(Targeting.All(Combat.Hostiles, Targeting.Attackable)))
        {
            if (h.Position.DistanceTo(_echoAt) > reach) continue;
            h.TakeDamage(blast);
            if (h is Node2D n) Popups.NoteImpact(n, h.Position);
        }
        NoteCombat();
        Fx.Raise(Fx.Echo, _echoAt, reach);                  // on every peer, where it remembered
    }

    public void GoDark()
    {
        if (Sl("stealth").Cool > 0) return;
        ref var s = ref Sl("stealth");
        s.Left = Stats["stealth_time"]; s.Cool = Cooling(Stats["stealth_cooldown"]);
        ApplyStatus(Status.Untargetable, s.Left);
        // whatever had picked this ship lets go of it at once, rather than at its next thought
        foreach (var t in new List<Turret>(Siblings)) t.Forget(this);
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

    // ── WARP (V) -- every capital ship ─────────────────────────────────────────
    // A fixed key and a hull cooldown, never an ability-bar slot. After a 3 s warm-up
    // the ship jumps to the selected target or waypoint if it lies within 45 degrees of the bow
    // (stopping short of it), otherwise 1200 u straight ahead. Movement is the
    // owner's, so the owner jumps; everyone else sees the charge and a clean snap.
    //
    // THE DRIVE THROWS IT 1200 u AND NO FURTHER. An aimed jump used to arrive AT the target
    // whatever the distance, so anything selected was one press away however far off it stood;
    // now the aim decides the heading and the drive decides the reach.
    public const double WarpWarmup = 3.0, WarpCooldown = 30.0;
    // The standoff is the gap between the ship's NOSE and the target's circle. A capital ship is
    // most of 400 u long, so 60 u of it put a battleship's bow through whatever it aimed at.
    public const float WarpRange = 1200f, WarpCone = Mathf.Pi / 4f, WarpStandoff = 160f;
    // A display ship jumps its own distance on its own clock (the title screen dodges an area
    // shot, which wants a short hop and a short wait). Defaulted to the class numbers, so a real
    // ship is unaffected -- these exist so the menu does not need a second warp implementation.
    public double WarpEvery = WarpCooldown;
    public float WarpHop = WarpRange;
    private double _warpLeft = -1, _warpCd, _warpFlash;
    private bool _remoteWarping;
    public bool Warping => _warpLeft >= 0;
    public double WarpWarmupLeft => Math.Max(0, _warpLeft);
    public double WarpCooldownLeft => _warpCd;
    private bool CanWarp => Alive && !Warping && _warpCd <= 0;

    // V starts the charge; WHERE it goes is decided when it jumps, by the heading then --
    // so a pilot can press V and swing onto a target while it charges.
    public bool StartWarp()
    {
        if (!Mine || !CanWarp) return false;
        _warpLeft = WarpWarmup;
        return true;
    }

    // where a warp to `target` ends: on the line from here, just short of its hull
    private static Vector2 WarpArrival(Vector2 from, Vector2 target, float targetRadius, float shipLength) =>
        target - (target - from).Normalized() * (targetRadius + shipLength * 0.5f + WarpStandoff);

    private void TickWarp(float dt)
    {
        if (_warpCd > 0) _warpCd = Math.Max(0, _warpCd - dt);
        if (!Warping) return;
        if (!Alive) { _warpLeft = -1; return; }
        _warpLeft -= dt;
        if (_warpLeft > 0) return;
        // now: the target or waypoint if it lies within 45 degrees of the bow, else straight on
        var bow = Vector2.Up.Rotated(Rotation);
        var dest = Position + bow * WarpHop;
        var aim = (GetParent() as Hub)?.WarpAim() ?? (false, Vector2.Zero, 0f);
        if (aim.has && Mathf.Abs(bow.AngleTo(aim.at - Position)) <= WarpCone)
        {
            var step = WarpArrival(Position, aim.at, aim.radius, MyArt.Length) - Position;
            // The aim gives the heading; the drive gives the reach. Further off than the hop and
            // the jump ends where the drive runs out, on the line to it.
            if (step.Length() > WarpHop) step = step.Normalized() * WarpHop;
            // ...and never backwards: a target already nearer than the arrival standoff would put
            // the arrival BEHIND the ship, which is not a jump toward anything.
            if (step.Dot(bow) > 0) dest = Position + step;
        }
        Position = dest; Velocity = Vector2.Zero;
        _warpLeft = -1; _warpCd = WarpEvery; _warpFlash = 0.6;
    }

    // ── what is being done to it (Statuses): a raider's web today, and whatever a class's
    // ability puts on it next. The HOST decides; a guest is sent the bits and shows them.
    // Pinned holds the ship to StatusSet.PinSpeed of top speed, thrusting, unable to turn.
    private StatusSet _status;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status s, double seconds) { if (Net.Sim) _status.Apply(s, seconds); }
    public bool Pinned => _status.Has(Status.Pinned);

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
    // A target that has left the world: the wing, the bombers and every turret let go of it. The
    // strike's count is NOT reset: bombers already out still report back (NoteStrikeDone), and a new
    // strike waits for all of them, as it did when a dead target stayed the strike's until then.
    public void Forget(IHittable t)
    {
        if (ReferenceEquals(WingTarget, t)) WingTarget = null;
        if (ReferenceEquals(StrikeTarget, t)) StrikeTarget = null;
        foreach (var tu in _turrets) tu.Forget(t);
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
    // this hull's own stat row where it has one and the status's own default where it has not: it
    // read Stats["rush_guard"] here, a row only the HeavyWarrior carries, so a hardening arriving
    // from any second class, a gear part or a boss debuff was a status that did nothing.
    private double Guarded(double d)
    {
        foreach (var g in StatusSet.Guards)
        {
            if (!_status.Has(g.Status)) continue;
            d *= !string.IsNullOrEmpty(g.Stat) && Stats[g.Stat] > 0 ? Stats[g.Stat] : g.Share;
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
        Velocity = Vector2.Zero; Sl("pd").Left = 0;
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
        if (Net.Sim) _status.Tick(delta);
        // the landing flash fades on every peer: it used to fade only on the owner's, and a
        // remote ship's warp left it lit for good
        _warpFlash = Math.Max(0, _warpFlash - dt);
        UpdatePod();
        if (Mine) { TickWarp(dt); LocalFlight(dt); }
        else      RemoteFollow(dt);

        TickAbilities(delta);

        if (Net.Sim) FireControl(delta);
        foreach (var t in _turrets) t.Tick(delta);
        for (int i = _wings.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_wings[i])) { _wings.RemoveAt(i); continue; }
            _wings[i].Tick(delta);
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
    // the railgun's shot, the rush's EMP, the echo's blast, the recharge after a point-defence
    // window, the magazine a reload refills). Every peer counts down so a guest's bars move
    // smoothly between host packets, and the next packet corrects any drift.
    //   This replaced a string[] naming the nine ids that had timers and a switch on the id
    // beside it: a tenth timed ability added as a row compiled, bound, drew on the bar and its
    // Left never moved, with nothing to say so. It replaced the point-defence window's own block
    // and the missile reload's too -- both were an expiry written out by hand -- and a second
    // tick of the freighter's deploy cooldown that this loop's first line already does.
    private void TickAbilities(double delta)
    {
        foreach (var def in Abilities.For(Class))
        {
            ref var sl = ref Sl(def.Id);
            if (sl.Cool > 0) sl.Cool = Math.Max(0, sl.Cool - delta);
            if (sl.Left <= 0) continue;
            sl.Left -= delta;
            if (sl.Left > 0) continue;
            sl.Left = 0;
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
    private void FireControl(double delta)
    {
        if (!Stats.Def.Has(Fit.Guns) || _mains.Count == 0) return;
        if (!Trigger) { _gunCd = Math.Max(0, _gunCd - delta); return; }   // keep reloading while idle

        double interval = Stats["main_interval"];
        double step = Staggered ? interval / _mains.Count : interval;
        _gunCd -= delta;
        for (int n = 0; _gunCd <= 0 && n < 32; n++)
        {
            if (Staggered) { _mains[_nextBarrel % _mains.Count].Shoot(); _nextBarrel = (_nextBarrel + 1) % _mains.Count; }
            else foreach (var m in _mains) m.Shoot();
            _gunCd += step;
        }
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
        float throttle = 0f, rudder = 0f;
        if (!locked)
        {
            if (Input.IsKeyPressed(Key.W)) throttle += 1f;
            if (Input.IsKeyPressed(Key.S)) throttle -= 1f;
            if (Input.IsKeyPressed(Key.A)) rudder -= 1f;
            if (Input.IsKeyPressed(Key.D)) rudder += 1f;
        }
        if (throttle != 0f || rudder != 0f) AutopilotTo = null;        // any helm key takes the controls back
        else if (AutopilotTo is { } dest)
            (throttle, rudder) = Autopilot.Capital(Position, Rotation, Velocity, (float)Stats["max_speed"], dest, 60f);
        if (Pinned) { throttle = 1f; rudder = 0f; AutopilotTo = null; }         // forced thrust, no rudder
        Thrusting = throttle != 0f;
        Steer(throttle, rudder, dt);

        // the main guns aim at the cursor; the hull does not follow it
        if (!Demo) Trigger = false;              // a display ship's driver owns the trigger
        if (!locked)
        {
            AimPoint = GetGlobalMousePosition();
            Trigger = Stats.Def.Has(Fit.Guns) && Input.IsKeyPressed(Abilities.KeyFor(Class, "guns"));
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
            AimPoint.X, AimPoint.Y, Trigger, Staggered, pod.X, pod.Y, podRot, Warping);
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
    // the water.
    private void Steer(float throttle, float rudder, float dt)
    {
        var fwd = Vector2.Up.Rotated(Rotation);           // nose direction
        var side = new Vector2(-fwd.Y, fwd.X);
        float along = Velocity.Dot(fwd), across = Velocity.Dot(side);

        // Held: a railgun charging, a shockwave's stun. No thrust, no rudder, whatever is pressed.
        if (_status.Has(Status.Disabled)) { throttle = 0f; rudder = 0f; }
        if (throttle > 0) along += (float)Stats["thrust"] * throttle * dt;
        else if (throttle < 0) along += (float)Stats["reverse_thrust"] * throttle * dt;
        along -= along * Mathf.Clamp((float)Stats["water_drag"] * dt, 0f, 1f);
        along = Mathf.Clamp(along, -(float)Stats["reverse_speed"],
                            (float)Stats["max_speed"] * SpeedMult * (Pinned ? StatusSet.PinSpeed : 1f));
        across *= Mathf.Exp(-(float)Stats["keel"] * dt);

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
            if (Pinned || _status.Has(Status.Disabled)) _yawRate = 0f;  // pinned or held: it cannot turn
        }
        Rotation += _yawRate * dt;

        fwd = Vector2.Up.Rotated(Rotation); side = new Vector2(-fwd.Y, fwd.X);
        Velocity = fwd * along + side * across;
        Position += Velocity * dt;
    }

    public float SpeedAhead => Velocity.Dot(Vector2.Up.Rotated(Rotation));
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
        if (Position.DistanceTo(_netPos) > 600f) { Position = _netPos; _warpFlash = 0.6; }
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
        _netAge = 0f;
        _netPos = new Vector2(px, py);
        _netVel = new Vector2(vx, vy);
        _netRot = rot;
        AimPoint = new Vector2(ax, ay);
        Trigger = trigger;          // the host fires on this; a guest only draws
        Staggered = staggered;
        if (IsInstanceValid(_pod) && !_pod.Local) _pod.SetNet(new Vector2(podX, podY), podRot);
        _remoteWarping = warping;
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
        for (int i = 0; i < Math.Min(_slots.Length, slotLeft.Length); i++)
            _slots[i] = new Slot { Left = slotLeft[i], Cool = slotCool[i], Own = slotOwn[i], N = slotN[i] };
        WingTarget = wingTarget != 0 ? Combat.ById(wingTarget) : null;
        // What the bombers were sent at. It was host-only, so a guest's own BOMB slot read
        // "RETURNING" for the whole of every strike it ordered -- the one word its bar had for it.
        StrikeTarget = strikeTarget != 0 ? Combat.ById(strikeTarget) : null;
        for (int i = 0; i < Math.Min(_wings.Count, wingPos.Length); i++)
        {
            _wings[i].SetNet(wingPos[i], wingRot[i]);
            _wings[i].SetNetState(wingState[i], wingRearm[i]);
        }
    }

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
        // THE BUBBLE (a freighter's): a ring the size of what it covers, fading as its pool is
        // spent, so everyone can see how much of it is left and who is inside it.
        if (BubbleUp)
        {
            float left = (float)Mathf.Clamp(BubbleLeft / Math.Max(1, Stats["bubble_pool"]), 0, 1);
            var c = new Color(0.55f, 0.85f, 1f, 0.15f + 0.35f * left);
            DrawCircle(Vector2.Zero, BubbleRadius, c with { A = c.A * 0.25f });
            DrawArc(Vector2.Zero, BubbleRadius, 0, Mathf.Tau, 64, c, 2.5f);
        }
        // warp: a charge building in the accent colour (lighting), then a flash where it lands
        if (Warping || _remoteWarping)
        {
            float k = Mine ? 1f - (float)(_warpLeft / WarpWarmup) : 0.6f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / (60f - 30f * k));
            DrawSetTransform(Vector2.Zero, 0f, new Vector2(0.45f, 1f));
            for (int i = 0; i < 3; i++)
                DrawArc(Vector2.Zero, MyArt.Length * (0.55f + 0.08f * i) + 5f * pulse, 0, Mathf.Tau, 48,
                        new Color(Accent.R, Accent.G, Accent.B, (0.25f + 0.5f * k) / (i + 1)), 2f);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
        if (_warpFlash > 0)
        {
            float f = (float)(_warpFlash / 0.6);
            DrawCircle(Vector2.Zero, MyArt.Length * (0.4f + 0.6f * (1f - f)), new Color(Accent.R, Accent.G, Accent.B, 0.35f * f));
        }
        // engine plumes at the stern, in the accent colour
        if (Alive)
            Plume.Draw(this, new Vector2(0, MyArt.Length * 0.5f - MyArt.EngineInset), Vector2.Down, MyArt.Length, Accent,
                       0.25f + 0.75f * Mathf.Abs(SpeedAhead) / (float)Stats["max_speed"], Thrusting || Mathf.Abs(SpeedAhead) > 2f);
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
