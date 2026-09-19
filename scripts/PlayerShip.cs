using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// PLAYER SHIP — you ARE this.
//
// It handles like a naval ship: thrust only along the keel (W ahead, S astern),
// the rudder (A/D) turns it on a radius that needs way on to work, and sideways
// drift bleeds off as if the hull were in water. No strafing, no pivoting.
//
// Networking split, following Net's rule:
//   the OWNER reads input: it moves the ship, and reports where it aims and
//     whether it holds the guns key, because input latency must feel instant;
//   the HOST resolves everything that deals damage or spends a resource, and
//     reports hull, ability state and wing positions back;
//   everyone else interpolates.
// ─────────────────────────────────────────────────────────────────────────────
public partial class PlayerShip : Node2D, IHittable
{
    public int OwnerId = 1;

    // Identity as announced by the owner. Every ship carries its OWN copy -- reading
    // the static Character here would paint every ship in the local player's colours.
    public string Pilot = "Commander";
    public Color Main = new(0.55f, 0.72f, 1.00f), Accent = new(1.00f, 0.78f, 0.35f);

    public ShipClass Class = ShipClass.Battleship;
    public ShipStats Stats = new(ShipClass.Battleship);
    public bool Alive { get; private set; } = true;

    // ── as a target: for ENEMY fire only (Combat.Players) ──────────────────
    public const int NetIdBase = 2000;                  // same on every peer: 2000 + owner
    public int NetId => NetIdBase + OwnerId;
    public float HitRadius => MyArt.HalfWidth;
    // A capsule along the keel: a circle would be far too wide for a long hull.
    public bool Covers(Vector2 p, float pad)
    {
        var fwd = Vector2.Up.Rotated(Rotation);
        float half = Mathf.Max(0f, MyArt.Length * 0.5f - MyArt.HalfWidth);
        float t = Mathf.Clamp((p - Position).Dot(fwd), -half, half);
        return p.DistanceTo(Position + fwd * t) <= MyArt.HalfWidth + pad;
    }

    // ── death: stasis, and the escape pod ───────────────────────────────────
    public const double StasisTime = 120, ReboardHull = 0.33;

    // ── "in combat": dealt or took damage in the last CombatHold seconds (host) ──
    public const double CombatHold = 8;
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
    public class ClassArt
    {
        public string Texture;
        public float Length;          // nose to tail, world units
        public float HalfWidth = 20f; // half the hull's beam: the collider and the shield
        // How far the free camera (Y) may wander from the ship. Capital ships 5000;
        // a fighter-class ship, when there is one, gets FighterCameraRange.
        public float CameraRange = 5000f;
        public Vector2[] Mains = Array.Empty<Vector2>(), Pds = Array.Empty<Vector2>();

        // The turrets are the ones painted on the ship: cut out of the hull art into
        // their own sprites (barrels up, pivot at the ring centre), with the hull
        // repaired underneath, so they can turn. Null = draw the simple fallback.
        public string MainTurret, PdTurret;
        public float TurretTexScale = 1f;   // world units per turret-texture pixel
        public float MainBarrel = 24f, PdBarrel = 15f, PdRing = 4.5f;   // world units

        // Bomber docks along the flanks: x out from the keel, the centre of each row,
        // and the spacing between craft in a row (world units).
        public float DockX = 36f, DockY = 5f, DockSpacing = 52f;
    }

    public static readonly Dictionary<ShipClass, ClassArt> Art = new()
    {
        // Battleship, 224 u (texture 300 px: 0.7467 u/px). Its four painted double-barrel
        // turrets on the centreline are the four main guns (one gun per turret); the
        // two small sponson turrets are point defence. Offsets measured from the art.
        // (texture 300 px at 224 u = 0.7467 u/px; every offset and size below is the
        // art's measured pixel position times that)
        [ShipClass.Battleship] = new ClassArt {
            Texture = "res://battleship_hull.png", Length = 224f, HalfWidth = 40f,
            Mains = new Vector2[] { new(0.41f, -65.41f), new(0.38f, -36.81f), new(0.59f, 48.76f), new(0.38f, 76.23f) },
            Pds   = new Vector2[] { new(-33.25f, 0.45f), new(34.97f, 0.45f) },
            MainTurret = "res://turret_bs_main.png", PdTurret = "res://turret_bs_pd.png", TurretTexScale = 0.7467f,
            MainBarrel = 20.9f, PdBarrel = 9.8f, PdRing = 5.0f },
        // Carrier, 170 u (full art 1668 px: 0.1019 u/px). Its three painted domes are
        // its point-defence turrets.
        [ShipClass.Carrier] = new ClassArt {
            Texture = "res://carrier_player.png", Length = 170f, HalfWidth = 24.5f,
            // bombers back in, tail to the hull: half the hull's beam (24.5) plus half a
            // 28.1 u bomber, spaced by a bomber's ~28 u span
            DockX = 39.5f, DockY = 5f, DockSpacing = 30f,
            Pds = new Vector2[] { new(-5.78f, 8.04f), new(5.78f, 8.04f), new(0f, 18.76f) },
            PdTurret = "res://turret_carrier.png", TurretTexScale = 0.2013f,
            PdBarrel = 3.7f, PdRing = 2.9f },
    };

    public const float FighterCameraRange = 2500f;
    public ClassArt MyArt => Art[Class];

    // ── the owner's intent, replicated at 20 Hz ──────────────────────────────
    public Vector2 AimPoint;               // where the main guns point
    public bool Thrusting;                 // the owner is on the throttle (plume flicker)
    public bool Trigger;                   // guns key held (battleship)
    public bool Staggered;                 // fire mode: false = salvo, true = staggered

    // ── orders and ability state, held on the host ───────────────────────────
    public IHittable WingTarget { get; private set; }      // fighters engage this
    public IHittable StrikeTarget { get; private set; }    // bombers run at this
    private int _strikesOut;

    // Point defence: an active ability. Active window, then recharge.
    private double _pdLeft, _pdRecharge;
    public bool PdActive => _pdLeft > 0;
    public bool PdReady => _pdLeft <= 0 && _pdRecharge <= 0;
    public double PdLeft => _pdLeft;
    public double PdRechargeLeft => _pdRecharge;
    public float PdActiveFrac => (float)(_pdLeft / Math.Max(0.001, Stats["pd_active"]));
    public float PdRechargeFrac => (float)(_pdRecharge / Math.Max(0.001, Stats["pd_reload"]));

    // Missiles: a magazine, reloaded by hand.
    private int _mag;
    private double _missileReload, _missileRefire;
    public int MissilesLoaded => _mag;
    public double MissileReloadLeft => _missileReload;
    public bool Reloading => _missileReload > 0;
    public bool CanFireMissile => Class == ShipClass.Battleship && _mag > 0 && !Reloading && _missileRefire <= 0;

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
    private float _netRot;
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
        WingTarget = StrikeTarget = null; _strikesOut = 0;
        _pdLeft = _pdRecharge = 0;

        // Your own ship carries your bonuses. Other ships use base stats: bonuses
        // are not replicated yet (and nothing grants any yet).
        Stats = new ShipStats(Class, Mine ? Character.Bonuses : null, Progression.Flats(_bought, Class));
        MaxHp = Hp = Stats["hull"];
        _mag = (int)Stats["missile_mag"]; _missileReload = _missileRefire = 0;

        var art = MyArt;
        var tex = GD.Load<Texture2D>(art.Texture);
        _sprite.Texture = tex;
        _sprite.Scale = Vector2.One * (art.Length / tex.GetHeight());
        _sprite.Modulate = Main;
        if (!IsInstanceValid(_shield)) { _shield = new ShieldFlash(); AddChild(_shield); }
        _shield.HalfWidth = art.HalfWidth; _shield.HalfLength = art.Length * 0.5f; _shield.Tint = Accent;

        for (int i = 0; i < Math.Min((int)Stats["main_count"], art.Mains.Length); i++) AddTurret(art.Mains[i], false);
        for (int i = 0; i < Math.Min((int)Stats["pd_count"], art.Pds.Length); i++) AddTurret(art.Pds[i], true);
        if (Class == ShipClass.Carrier)
        {
            for (int i = 0; i < (int)Stats["fighter_count"]; i++) AddWing(WingKind.Fighter);
            for (int i = 0; i < (int)Stats["bomber_count"]; i++) AddWing(WingKind.Bomber);
        }
    }

    private void AddTurret(Vector2 offset, bool pd)
    {
        var t = new Turret(); AddChild(t); t.Setup(this, offset, pd);
        _turrets.Add(t); if (pd) PdTurrets.Add(t); else _mains.Add(t);
    }

    private void AddWing(WingKind k)
    {
        var w = new Wing(); GetParent().AddChild(w);
        w.Init(this, k, Position + new Vector2(GD.Randf() * 120 - 60, GD.Randf() * 120 - 60));
        _wings.Add(w);
    }

    public void SetClass(ShipClass c) { Class = c; FitClass(); }

    // The pilot's purchased upgrades (Progression). The host needs them too: it resolves
    // hull and damage. Changing them refits the stats, keeping the damage taken so far.
    private int[] _bought = new int[Progression.All.Length];
    public int[] Bought => _bought;
    public void SetProgress(int[] bought)
    {
        var b = new int[Progression.All.Length];
        for (int i = 0; i < b.Length && i < (bought?.Length ?? 0); i++) b[i] = Math.Clamp(bought[i], 0, Progression.MaxPerUpgrade);
        if (b.AsSpan().SequenceEqual(_bought)) return;
        _bought = b;
        double lost = MaxHp - Hp;
        Stats = new ShipStats(Class, Mine ? Character.Bonuses : null, Progression.Flats(_bought, Class));
        MaxHp = Stats["hull"];
        if (Alive) Hp = Math.Max(1, MaxHp - lost);
    }

    public void SetIdentity(string pilot, Color main, Color accent, ShipClass cls)
    {
        Pilot = pilot; Main = main; Accent = accent;
        if (cls != Class) SetClass(cls);
        else if (_sprite != null) _sprite.Modulate = Main;
        if (IsInstanceValid(_shield)) _shield.Tint = Accent;
        foreach (var t in _turrets) t.Recolor();
    }

    // Where bomber `w` docks: bombers alternate port, starboard, port... so the two
    // flanks always split them evenly (6 bombers -> 3 and 3; an odd one goes to port),
    // and each row is centred on the hull. Returns the world position and heading.
    public (Vector2 pos, float rot) DockSlot(Wing w)
    {
        int i = 0, n = 0;
        foreach (var x in _wings) { if (!x.IsBomber || !x.Alive) continue; if (x == w) i = n; n++; }
        bool port = i % 2 == 0;
        int onSide = port ? (n + 1) / 2 : n / 2, j = i / 2;
        var art = MyArt;
        var local = new Vector2(port ? -art.DockX : art.DockX, art.DockY + (j - (onSide - 1) / 2f) * art.DockSpacing);
        // nose out, tail to the hull: backed into the slot
        return (ToGlobal(local), Rotation + (port ? -Mathf.Pi / 2f : Mathf.Pi / 2f));
    }

    public int WingCount(WingKind k) { int n = 0; foreach (var w in _wings) if (w.Alive && w.Kind == k) n++; return n; }
    public int BombersReady { get { int n = 0; foreach (var w in _wings) if (w.Alive && w.Kind == WingKind.Bomber && w.Armed) n++; return n; } }
    public double BomberRearmLeft { get { double m = 0; foreach (var w in _wings) if (w.Kind == WingKind.Bomber) m = Math.Max(m, w.RearmLeft); return m; } }

    // ── abilities ────────────────────────────────────────────────────────────
    // One entry point for every class action. The owner's key press calls
    // UseAbility; the host acts on it directly, a guest sends it as a request, and
    // the host checks the sender owns this ship first. A client says "I pressed
    // missile at target 1000", never "I did 5 damage".
    public void UseAbility(string id, int targetId)
    {
        // the fire mode is the owner's own intent (it rides in NetState), not a host order
        if (id == "firemode") { Staggered = !Staggered; return; }
        // The missile needs a selected target within range. Checked here, on the owner's
        // machine, so the slot can say why at once; the host checks again.
        if (id == "missile" && Class == ShipClass.Battleship)
        {
            var t = targetId != 0 ? Combat.ById(targetId) : null;
            string why = t == null ? "NO TARGET" : Position.DistanceTo(t.Position) > Stats["missile_range"] ? "OUT OF RANGE" : null;
            if (why != null) { Fail(id, why); return; }
        }
        if (Net.Sim) DoAbility(id, targetId);
        else RpcId(1, nameof(RequestAbility), id, targetId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestAbility(string id, int targetId)
    {
        if (!Net.IsHost || Multiplayer.GetRemoteSenderId() != OwnerId) return;
        DoAbility(id, targetId);
    }

    private void DoAbility(string id, int targetId)
    {
        var t = targetId != 0 ? Combat.ById(targetId) : null;
        switch (id)
        {
            case "reboard": if (CanReboard) { Alive = true; Hp = MaxHp * ReboardHull; _stasis = 0; } break;
            case "pd":      if (PdReady) _pdLeft = Stats["pd_active"]; break;
            case "missile": FireMissile(t); break;
            case "reload":  if (Class == ShipClass.Battleship && !Reloading && _mag < (int)Stats["missile_mag"])
                                _missileReload = Stats["missile_reload"]; break;
            case "attack":  if (Class == ShipClass.Carrier && t != null) WingTarget = t; break;
            case "recall":  WingTarget = null; break;
            case "bombers":
                // within the strike range (twice the fighters' control range) only
                if (Class == ShipClass.Carrier && t != null && StrikeTarget == null && BombersReady > 0
                    && Position.DistanceTo(t.Position) <= Stats["strike_range"])
                { StrikeTarget = t; _strikesOut = BombersReady; }
                break;
        }
    }

    // A refused ability: its slot shows the reason, in red, for a moment.
    public const double FailShow = 1.5;
    private readonly Dictionary<string, (string msg, double until)> _fails = new();
    private double _clock;                                          // game seconds, for timing
    public double Clock => _clock;
    public void Fail(string id, string msg) => _fails[id] = (msg, _clock + FailShow);
    public string FailNote(string id) => _fails.TryGetValue(id, out var f) && _clock < f.until ? f.msg : null;

    // Fighters leave the hangar one at a time, at least Wing.LaunchInterval apart.
    private double _nextLaunch;
    public bool TakeLaunchSlot()
    {
        if (_clock < _nextLaunch) return false;
        _nextLaunch = _clock + Wing.LaunchInterval;
        return true;
    }

    public void NoteStrikeDone() { if (--_strikesOut <= 0) StrikeTarget = null; }

    // A bunker buster: launched off the nose toward the target (the selection if in
    // range, else the nearest hostile), slow, and only barely guided -- its heading
    // creeps toward the target at missile_turn rad/s, so a target that moves early
    // enough can get out from under it.
    private void FireMissile(IHittable t)
    {
        if (!Net.Sim || !CanFireMissile) return;
        float range = (float)Stats["missile_range"];
        if (t == null || Position.DistanceTo(t.Position) > range) return;   // a target in range, or nothing
        _mag--; _missileRefire = Stats["missile_refire"];
        var nose = ToGlobal(new Vector2(0, -MyArt.Length * 0.5f));
        Combat.LaunchTorpedo(nose, t.Position - nose, (float)Stats["missile_speed"], range * 1.4f,
                             Stats["missile_damage"], t.NetId, (float)Stats["missile_turn"], heavy: true, source: this);
    }

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;       // only the host resolves damage
        Hp -= d;
        if (Hp <= 0) Die();
    }

    // A hit that knows where it came from: damage, and the shield lights that side.
    public void Hit(double d, Vector2 from)
    {
        if (!Net.Sim || !Alive) return;
        var v = (from - Position).Rotated(-Rotation);
        float side = Mathf.Atan2(v.X, -v.Y);            // 0 = ahead, clockwise
        _shield?.Flash(side);
        if (Net.IsOnline) Rpc(nameof(NetShield), side);
        NoteCombat();                                   // taking damage is combat
        TakeDamage(d);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetShield(float side) { if (Multiplayer.GetRemoteSenderId() == 1) _shield?.Flash(side); }

    // Into stasis where it lies; the pilot takes to the escape pod.
    private void Die()
    {
        Hp = 0; Alive = false; _stasis = StasisTime;
        Velocity = Vector2.Zero; Trigger = false; _pdLeft = 0;
        WingTarget = null; StrikeTarget = null;
    }

    public bool Mine => Net.OwnedByMe(this);

    // ── frame ────────────────────────────────────────────────────────────────
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += delta;
        if (!Alive) _stasis = Math.Max(0, _stasis - delta);   // the host's clock rules; guests re-sync each packet
        if (_combatT > 0) _combatT = Math.Max(0, _combatT - delta);
        UpdatePod();
        if (Mine) LocalFlight(dt);
        else      RemoteFollow(dt);

        if (Net.Sim) TickAbilities(delta);
        else         TickAbilitiesCosmetic(delta);

        if (Net.Sim) FireControl(delta);
        foreach (var t in _turrets) t.Tick(delta);
        for (int i = _wings.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_wings[i]) || !_wings[i].Alive) { _wings.RemoveAt(i); continue; }
            _wings[i].Tick(delta);
        }
        if (WingTarget != null && !WingTarget.Alive) WingTarget = null;

        if (Net.Sim && Net.IsOnline)
        {
            _hostSendCd -= delta;
            if (_hostSendCd <= 0) { _hostSendCd = HostInterval; SendHostState(); }
        }
        QueueRedraw();
    }

    private void TickAbilities(double delta)
    {
        if (_pdLeft > 0) { _pdLeft -= delta; if (_pdLeft <= 0) { _pdLeft = 0; _pdRecharge = Stats["pd_reload"]; } }
        else if (_pdRecharge > 0) _pdRecharge = Math.Max(0, _pdRecharge - delta);

        if (_missileRefire > 0) _missileRefire = Math.Max(0, _missileRefire - delta);
        if (_missileReload > 0)
        {
            _missileReload -= delta;
            if (_missileReload <= 0) { _missileReload = 0; _mag = (int)Stats["missile_mag"]; }
        }
    }

    // Between host packets a guest counts its timers down itself so the bar moves
    // smoothly; the next packet corrects any drift.
    private void TickAbilitiesCosmetic(double delta)
    {
        if (_pdLeft > 0) _pdLeft = Math.Max(0, _pdLeft - delta);
        else if (_pdRecharge > 0) _pdRecharge = Math.Max(0, _pdRecharge - delta);
        if (_missileReload > 0) _missileReload = Math.Max(0, _missileReload - delta);
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
        if (Class != ShipClass.Battleship || _mains.Count == 0) return;
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
            Velocity = Vector2.Zero; Trigger = false;
            SendState(dt);
            return;
        }
        // Input.IsKeyPressed polls the raw keyboard, so it does not care that a text
        // box has focus. Hub decides when the controls belong to the UI instead.
        bool locked = Hub.ControlsLocked;
        float throttle = 0f, rudder = 0f;
        if (!locked)
        {
            if (Input.IsKeyPressed(Key.W)) throttle += 1f;
            if (Input.IsKeyPressed(Key.S)) throttle -= 1f;
            if (Input.IsKeyPressed(Key.A)) rudder -= 1f;
            if (Input.IsKeyPressed(Key.D)) rudder += 1f;
        }
        Thrusting = throttle != 0f;
        Steer(throttle, rudder, dt);

        // the main guns aim at the cursor; the hull does not follow it
        Trigger = false;
        if (!locked)
        {
            AimPoint = GetGlobalMousePosition();
            Trigger = Class == ShipClass.Battleship && Input.IsKeyPressed(Abilities.KeyFor(Class, "guns"));
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
        Rpc(nameof(NetState), Position.X, Position.Y, Velocity.X, Velocity.Y, Rotation,
            AimPoint.X, AimPoint.Y, Trigger, Staggered, pod.X, pod.Y, podRot);
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

    // Public so the smoke test can drive the helm exactly. Velocity is split into
    // the component along the keel and the component across it. Thrust only ever
    // adds along the keel; water drag slows both; the keel kills sideways drift
    // quickly, so the ship goes where it points. The rudder turns at speed/radius
    // -- a turning circle -- capped by the rudder limit, and does nothing dead in
    // the water.
    public void Steer(float throttle, float rudder, float dt)
    {
        var fwd = Vector2.Up.Rotated(Rotation);           // nose direction
        var side = new Vector2(-fwd.Y, fwd.X);
        float along = Velocity.Dot(fwd), across = Velocity.Dot(side);

        if (throttle > 0) along += (float)Stats["thrust"] * throttle * dt;
        else if (throttle < 0) along += (float)Stats["reverse_thrust"] * throttle * dt;
        along -= along * Mathf.Clamp((float)Stats["water_drag"] * dt, 0f, 1f);
        along = Mathf.Clamp(along, -(float)Stats["reverse_speed"], (float)Stats["max_speed"]);
        across *= Mathf.Exp(-(float)Stats["keel"] * dt);

        // turning circle: yaw rate = speed / radius, capped by the rudder; astern the
        // rudder reverses, as it does on a real ship
        float cap = Mathf.Min(Mathf.Abs(along) / (float)Stats["turn_radius"], (float)Stats["turn_rate"]);
        float want = rudder * cap * Mathf.Sign(along == 0 ? 1 : along);
        _yawRate = Mathf.MoveToward(_yawRate, want, 2.5f * dt);     // the rudder takes a moment to bite
        if (Mathf.Abs(along) < 0.5f) _yawRate = 0f;
        Rotation += _yawRate * dt;

        fwd = Vector2.Up.Rotated(Rotation); side = new Vector2(-fwd.Y, fwd.X);
        Velocity = fwd * along + side * across;
        Position += Velocity * dt;
    }

    public float SpeedAhead => Velocity.Dot(Vector2.Up.Rotated(Rotation));

    // ── everyone else follows it ─────────────────────────────────────────────
    private void RemoteFollow(float dt)
    {
        // Dead reckoning: carry the last known velocity forward so a remote ship
        // keeps moving smoothly between the 20 Hz updates instead of stuttering.
        _netPos += _netVel * dt;
        Position = Position.Lerp(_netPos, Mathf.Clamp(12f * dt, 0f, 1f));
        Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(12f * dt, 0f, 1f));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(float px, float py, float vx, float vy, float rot, float ax, float ay, bool trigger, bool staggered,
                          float podX, float podY, float podRot)
    {
        // Only the ship's own owner may move it. Without this check any peer could
        // shove anyone else's ship around, which is the classic authority hole.
        if (Multiplayer.GetRemoteSenderId() != OwnerId) return;
        _netPos = new Vector2(px, py);
        _netVel = new Vector2(vx, vy);
        _netRot = rot;
        AimPoint = new Vector2(ax, ay);
        Trigger = trigger;          // the host fires on this; a guest only draws
        Staggered = staggered;
        if (IsInstanceValid(_pod) && !_pod.Local) _pod.SetNet(new Vector2(podX, podY), podRot);
    }

    // The host's side of the conversation: hull, ability state, and the wing.
    private void SendHostState()
    {
        int n = _wings.Count;
        var pos = new Vector2[n]; var rot = new float[n]; var st = new int[n]; var rearm = new float[n];
        for (int i = 0; i < n; i++)
        {
            pos[i] = _wings[i].Position; rot[i] = _wings[i].Rotation;
            st[i] = _wings[i].StateCode; rearm[i] = (float)_wings[i].RearmLeft;
        }
        Rpc(nameof(NetHostState), Hp, MaxHp, Alive, _stasis, _combatT, _pdLeft, _pdRecharge, _mag, _missileReload,
            WingTarget?.NetId ?? 0, pos, rot, st, rearm);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetHostState(double hp, double maxHp, bool alive, double stasis, double combat, double pdLeft, double pdRecharge, int mag, double reload,
                              int wingTarget, Vector2[] wingPos, float[] wingRot, int[] wingState, float[] wingRearm)
    {
        if (Multiplayer.GetRemoteSenderId() != 1) return;   // only the host speaks for combat state
        Hp = hp; MaxHp = maxHp; Alive = alive; _stasis = stasis; _combatT = combat;
        _pdLeft = pdLeft; _pdRecharge = pdRecharge; _mag = mag; _missileReload = reload;
        WingTarget = wingTarget != 0 ? Combat.ById(wingTarget) : null;
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
    // takes off -- the hangar for fighters, a slot for bombers ──────────────────
    private readonly System.Collections.Generic.List<(Vector2 local, double t)> _signals = new();
    public const double SignalTime = 1.2;
    public void Signal(Vector2 world) => _signals.Add((ToLocal(world), SignalTime));
    public int SignalsLit => _signals.Count;

    public override void _Draw()
    {
        // engine plumes at the stern, in the accent colour
        if (Alive)
            Plume.Draw(this, new Vector2(0, MyArt.Length * 0.5f), Vector2.Down, MyArt.Length, Accent,
                       0.25f + 0.75f * Mathf.Abs(SpeedAhead) / (float)Stats["max_speed"], Thrusting || Mathf.Abs(SpeedAhead) > 2f);
        for (int i = _signals.Count - 1; i >= 0; i--)
        {
            var (p, t) = _signals[i];
            t -= GetProcessDeltaTime();
            if (t <= 0) { _signals.RemoveAt(i); continue; }
            _signals[i] = (p, t);
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
