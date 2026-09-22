using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// HAULER — carries the yard's stock out through the portal for sale.
//
// Home is the base's bottom pad. The portal sits on the same horizontal line, so
// every move between them is perfectly flat -- except an escort's. It lands SMALLER (65%) than it flies:
// shrinking onto the pad, with its shadow tucking in underneath, reads as settling
// down out of the sky; growing again reads as lifting off.
//
//   LOADING    landed, facing the portal, filling its pods from the stock. Once one
//              pod is full the base owner may send it: DISPATCH (alone) or ESCORT.
//              Completely full, it goes alone by itself -- only with AUTO-SELL (the
//              level-3 boss beaten, and 4462 cr).
//   LIFTING    grows to flight size
//   DEPARTING  a lone run: slides slowly east to the portal
//   ESCORTING  an escort: flies counter-clockwise round the four outposts (Hub.EscortRoute),
//              holding 4 s beside each to offload a quarter of the load (drawn as the loading
//              run backwards), then on to the portal, while a wave of raiders hunts it every
//              20 s; it must reach the portal alive
//   CHARGING   shakes inside a building blue aura
//   AWAY       warps out; 30 s later its cargo is sold. A lone run gets through with the
//              EVASION chance, or the cargo is lost and it comes back empty; an escort
//              is paid 5x.
//   ARRIVING   warps back in facing the base, its pods flashing empty
//   RETURNING  slides slowly west to above its pad
//   LANDING    shrinks back onto the pad, swinging round to face the portal, then loads again
//   DESTROYED  lost to a raid: gone, and rebuilt on its pad 30 s later (UtilityShip)
//
// Six pods are painted on its hull; it starts with one working and the HAULER tab
// adds more (up to six) and makes each bigger.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Hauler : UtilityShip, ITurretHost
{
    public enum St { Loading, Lifting, Departing, Escorting, Charging, Away, Arriving, Returning, Landing, Destroyed }

    public St State = St.Loading;
    public bool Escorted { get; private set; }   // this run is an escort
    public bool RunLost { get; private set; }    // a lone run the dice went against: its cargo is gone
    public static float? PretendRoll;            // the smoke test's way to fix the dice (like Net.PretendProtocol)
    // Sent to guests with every state report: the two switches, and above them the escort's leg
    // (where the route line starts; while it holds at an outpost, that outpost).
    public const int FlagEscorted = 1, FlagLost = 2, LegShift = 2;
    public int NetFlags => (Escorted ? FlagEscorted : 0) | (RunLost ? FlagLost : 0) | (_leg << LegShift);
    // What this load sells for at the portal: x5 on an escort, nothing if a lone run was lost.
    public double Payout => Cargo * Economy.CreditsPerUnit * (Escorted ? Economy.EscortPay : RunLost ? 0 : 1);
    private int _leg, _waves;                    // an escort: the route point it is flying to, the waves sent
    public int Leg => _leg;
    // An escort holding at an outpost: seconds of the stop left (guests are sent it, and count it down).
    private double _stop;
    public double StopLeft => _stop;
    // AN ESCORT DELIVERS A QUARTER OF ITS LOAD AT EACH OUTPOST -- drawn, not paid: the pay (x5) is
    // on the whole load, at the portal, exactly as before, so a lost escort still loses it all and a
    // save mid-run still counts the cargo aboard. The pods show what is left: the quarters handed
    // over so far, the one going out now included.
    public double Delivered => !Escorted ? 0
        : State == St.Escorting ? Math.Min(_leg, Hub.Outposts.Length) + (_stop > 0 ? 1 - _stop / Economy.OutpostStop : 0)
        : State is St.Charging or St.Away ? Hub.Outposts.Length : 0;
    public double ShownCargo => Cargo * (1 - Delivered / Hub.Outposts.Length);
    public double T;                         // seconds in the current state
    public double LastSale;                  // credits paid on the last return
    public const float Length = 200f;
    public const float East = Mathf.Pi / 2f, West = -Mathf.Pi / 2f;
    private const float Accel = 25f;
    private float _speed;

    // the six painted pods, in the hull's own frame at flight size (nose up):
    // filled in pairs, front to back, port then starboard
    private static readonly Vector2[] PodCentre =
        { new(-23.6f, -51.6f), new(23.6f, -51.6f), new(-23.6f, -6.0f), new(23.6f, -6.0f), new(-23.6f, 39.5f), new(23.6f, 39.5f) };
    public static readonly Vector2 PodSize = new(21f, 43.4f);

    private Sprite2D _sprite;
    private Node2D _overlay;                 // drawn above the hull: pods, cargo motes, the sale

    // ── its own point defence (ITurretHost) ─────────────────────────────────
    // One mount on the spine, the same component a warship carries -- but nobody flies this, so
    // there is no firing window: it is online whenever the hauler is out where it can be reached.
    // Its damage is the yard's to buy (hauler_pd_damage); the rest of the gun is fixed.
    private Turret _pd;
    private readonly List<Turret> _pdMounts = new();
    private static readonly Vector2 PdMount = new(0f, 8f);     // on the spine, just aft of centre
    public Node2D AsNode => this;
    public bool PdOnline => InReach && !Lost;
    public float PdRing => 0f;                       // no window, so no ring to run down
    public Vector2 AimAt => Position;                // it has no main guns
    public float FastSwing => 0f;
    public IReadOnlyList<Turret> Siblings => _pdMounts;
    public void NoteDealt(double d, Vector2 at) { }  // it keeps no combat clock and no echo
    public PlayerShip Credit => null;                // its shots are the base's, not a pilot's
    public TurretSpec Spec(bool pd) => new()
    {
        Damage = Yard?.Value("hauler_pd_damage") ?? Economy.HaulerPdDamage,
        Interval = Economy.HaulerPdInterval,
        Range = (float)Economy.HaulerPdRange,
        Turn = (float)Economy.HaulerPdTurn,
        Texture = "res://turret_pd.png",
        TexScale = 1.3085f / 5.5f, Barrel = 7.2f, Ring = 3.93f,
        Tint = new Color(0.78f, 0.82f, 0.9f),
    };
    private Vector2 _netPos; private float _netRot; private bool _hasNet;
    private readonly RandomNumberGenerator _rng = new();

    public int FullPods => (int)Math.Min(Yard.Pods, Math.Floor((Cargo + 1e-6) / Yard.PodSize));
    public bool CanDispatch => State == St.Loading && FullPods >= 1;

    public override void _Ready()
    {
        var tex = GD.Load<Texture2D>("res://hauler.png");
        _sprite = new Sprite2D { Texture = tex };
        AddChild(_sprite);
        _overlay = new Node2D();
        _overlay.Draw += DrawOverlay;
        AddChild(_overlay);
        _pd = new Turret();
        AddChild(_pd);
        _pd.Setup(this, PdMount, pd: true);
        _pdMounts.Add(_pd);
        ResetToPad();
    }

    public void ResetToPad() { EndEscort(); RunLost = false; Go(St.Loading); Cargo = 0; _speed = 0; _hasNet = false; Position = Hub.HaulerPad; Rotation = East; Hull = MaxHull; RebuildIn = 0; WaitingForCredits = false; }

    // out of reach while away (through the portal)
    public override double MaxHull => Economy.HaulerHull;
    public override string Category => "HAULER";
    public override string Label => "Hauler";
    public override bool InReach => State is not (St.Destroyed or St.Away);
    public override bool Lost => State == St.Destroyed;
    public override (float halfLength, float halfWidth) Extent => (Length * 0.5f * VisualScale, Length * 0.12f * VisualScale);
    protected override float LostBlast => 70f;
    protected override float RebuiltBlast => 60f;
    protected override void OnLost() { _speed = 0; EndEscort(); Go(St.Destroyed); Effects(); }   // gone at once, its hunters with it

    public void SetNet(Vector2 p, float rot, int state, float t, float cargo, float sale, float hull, float rebuild, int flags, float stop)
    {
        bool wasLost = Lost;
        (_netPos, _netRot, _hasNet, _stop) = (p, rot, true, stop);
        (Escorted, RunLost, _leg) = ((flags & FlagEscorted) != 0, (flags & FlagLost) != 0, flags >> LegShift);
        if ((St)state != State || Math.Abs(T - t) > 0.5) T = t;
        (State, Cargo, LastSale) = ((St)state, cargo, sale);
        FromHost(hull, rebuild, wasLost);
    }

    // Sent on its way: alone, or on an escort. Only while loading, only with at least one full pod,
    // and only by the base's owner (Yard.RequestDispatch).
    public void Dispatch(bool escorted)
    {
        if (!Net.Sim || !CanDispatch || !Yard.IsMyOwnBase) return;
        Escorted = escorted; RunLost = false; _leg = 0; _waves = 0; _stop = 0;
        Go(St.Lifting);
    }

    // An escort over -- at the portal, lost, or the hauler reset: its hunters withdraw.
    private void EndEscort()
    {
        if (Escorted && Net.IsHost && Yard?.Hub != null) Yard.Hub.CallOff(this);
        Escorted = false; _leg = 0; _waves = 0; _stop = 0;
    }

    private void Go(St s) { State = s; T = 0; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        T += delta;
        WatchHull();
        _pd.Tick(delta);                     // always on: the host's copy fires, a guest's tracks
        if (Net.Sim) Simulate(dt);
        else
        {
            GuestClock(dt);
            // Counted down between reports, but never to zero here: the host ends the stop and moves
            // the leg on in the same frame, and a guest that ended it first would show the quarter
            // just handed over back in the pods until the report with the next leg arrived.
            if (_stop > 0) _stop = Math.Max(1e-3, _stop - delta);
            if (_hasNet)
            {   // on its lane only x moves (the lane is exact); an escort flies free
                float k = Mathf.Clamp(10f * dt, 0f, 1f);
                Position = State == St.Escorting ? Position.Lerp(_netPos, k) : new Vector2(Mathf.Lerp(Position.X, _netPos.X, k), _netPos.Y);
                Rotation = Mathf.LerpAngle(Rotation, _netRot, k);
            }
        }
        Effects();
        QueueRedraw(); _overlay.QueueRedraw();
    }

    private void Simulate(float dt)
    {
        TickStatus(dt);
        switch (State)
        {
            case St.Loading:
                Position = Hub.HaulerPad; Rotation = East;
                Cargo += Yard.TakeStock(Math.Min(Economy.HaulerLoadRate * dt, Yard.Capacity - Cargo));
                if (Yard.AutoSell && Cargo >= Yard.Capacity - 1e-6) Dispatch(escorted: false);   // full: it goes alone by itself
                break;
            case St.Lifting:  if (T >= Economy.HaulerLift) Go(Escorted ? St.Escorting : St.Departing); break;
            case St.Departing: if (Slide(Hub.PortalPos.X, dt)) Go(St.Charging); break;
            case St.Escorting:
                // the waves run on T, which a stop does not reset: they keep coming while it offloads
                if (T >= Economy.EscortFirstWave + _waves * Economy.EscortWaveEvery) Yard.Hub.HuntWave(this, _waves++);
                if (_stop > 0)
                {   // holding beside an outpost, offloading; then on to the next point
                    if ((_stop -= dt) <= 0) { _stop = 0; _leg = Math.Min(_leg + 1, Hub.EscortRoute.Length - 1); }
                }
                else if (Fly(Hub.EscortRoute[_leg], dt))
                {
                    if (_leg == Hub.EscortRoute.Length - 1) Go(St.Charging);    // the portal
                    else _stop = Economy.OutpostStop;                           // an outpost
                }
                break;
            case St.Charging:
                if (T >= Economy.HaulerCharge)
                {   // THE JUMP. An escort has made it: its hunters withdraw. A lone run rolls against EVASION.
                    if (Escorted) Yard.Hub.CallOff(this);
                    else RunLost = (PretendRoll ?? _rng.Randf()) >= Yard.RunSafe;
                    Go(St.Away); Yard.PortalFlash();
                }
                break;
            case St.Away:
                if (T >= Economy.HaulerAway)
                {
                    LastSale = Payout;
                    Yard.Credits += LastSale; Cargo = 0;
                    Rotation = West; Go(St.Arriving); Yard.PortalFlash();
                }
                break;
            case St.Arriving: if (T >= 2.0) Go(St.Returning); break;
            case St.Returning:
                Rotation = West;
                if (Slide(Hub.HaulerPad.X, dt)) Go(St.Landing);   // over the pad: descend, turning as it goes
                break;
            case St.Destroyed:              // rebuilt on its pad 30 s later, for 10% of what the hauler's upgrades have cost
                if (TickRebuild(dt)) { ResetToPad(); Burst(rebuilt: true); }
                break;
            case St.Landing:
            {   // down onto the pad, swinging from west to east on the way: it arrives facing the portal
                float k = Mathf.Clamp((float)(T / Economy.HaulerLand), 0f, 1f);
                Rotation = West + Mathf.Pi * Mathf.SmoothStep(0f, 1f, k);
                if (k >= 1f) { Rotation = East; Escorted = RunLost = false; Go(St.Loading); }
                break;
            }
        }
        if (State != St.Escorting) Position = new Vector2(Position.X, Hub.LaneY);   // the lane: every move is flat but an escort's
    }

    // An escort's leg: toward `to` at the hauler's speed (20% of it while pinned), easing in to stop
    // there (every point of the route is a stop: an outpost, or the portal), the nose turning onto
    // the heading at 2 rad/s (not while pinned, like any utility ship). True when it arrives.
    private bool Fly(Vector2 to, float dt)
    {
        var d = to - Position; float dist = d.Length();
        float top = (float)Yard.Value("hauler_speed") * (Pinned ? StatusSet.PinSpeed : 1f);
        _speed = Motion.ArriveSpeed(_speed, dist, top, Accel, dt);
        if (Pinned) _speed = Mathf.Min(_speed, top);
        if (!Pinned && dist > 1f) Rotation = Mathf.RotateToward(Rotation, Aim.Along(d), 2f * dt);
        if (dist < 0.5f || _speed * dt >= dist) { Position = to; _speed = 0; return true; }
        Position += d / dist * _speed * dt;
        return false;
    }

    // along x only, easing in and out, never faster than the hauler's speed
    private bool Slide(float toX, float dt)
    {
        float d = toX - Position.X, dist = Mathf.Abs(d);
        float top = (float)Yard.Value("hauler_speed") * (Pinned ? StatusSet.PinSpeed : 1f);    // pinned: 20% along its lane
        _speed = Motion.ArriveSpeed(_speed, dist, top, Accel, dt);
        if (Pinned) _speed = Mathf.Min(_speed, top);
        Position += new Vector2(Mathf.Sign(d) * Mathf.Min(_speed * dt, dist), 0);
        if (dist >= 0.5f) return false;
        Position = new Vector2(toX, Position.Y); _speed = 0; return true;
    }

    // 0 on the pad, 1 at flight height
    private float Altitude => State switch
    {
        St.Loading => 0f,
        St.Lifting => Mathf.SmoothStep(0f, 1f, (float)(T / Economy.HaulerLift)),
        St.Landing => 1f - Mathf.SmoothStep(0f, 1f, (float)(T / Economy.HaulerLand)),
        _ => 1f,
    };
    public float VisualScale => Mathf.Lerp(Economy.HaulerLandedScale, 1f, Altitude);

    // Shake, warp, and the landing size all live on the sprite, so the node itself
    // never leaves its line.
    private void Effects()
    {
        bool gone = State == St.Away && T >= 0.35;
        _sprite.Visible = _overlay.Visible = !gone && State != St.Destroyed;
        // the mount rides the hull: hidden with it, and shrunk with it on the pad
        _pd.Visible = _sprite.Visible;
        _pd.Position = PdMount * VisualScale;
        _pd.Scale = Vector2.One * VisualScale;
        _sprite.Position = State == St.Charging
            ? new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f)) * (0.5f + 2.5f * (float)Math.Min(1, T / Economy.HaulerCharge))
            : Vector2.Zero;
        float warp = State == St.Away ? 1f - (float)Math.Min(1, T / 0.35) : State == St.Arriving ? (float)Math.Min(1, T / 0.4) : 1f;
        float k = Length / _sprite.Texture.GetHeight() * VisualScale;
        _sprite.Scale = new Vector2(k * warp, k * (State == St.Away ? 1f + 2f * (1f - warp) : 1f));
        _sprite.Modulate = State == St.Charging || State == St.Away || (State == St.Arriving && T < 0.6)
            ? new Color(0.75f, 0.9f, 1.3f) : Colors.White;
    }

    public bool AuraOn => State == St.Charging;
    public bool WarpedOut => !_sprite.Visible;

    // below the hull: its shadow (further out the higher it flies) and the aura
    public override void _Draw()
    {
        if (WarpedOut) return;
        // three light-yellow plumes at its three nozzles (x = -17.7, 0, +17.7 u at full size)
        float vs = VisualScale, thr = State is St.Departing or St.Escorting or St.Returning ? 1f : 0.2f;
        foreach (float nx in new[] { -17.7f, 0f, 17.7f })
            Plume.Draw(this, new Vector2(nx, Length * 0.5f) * vs, Vector2.Down, Length * 0.45f * vs, Plume.Utility, thr, thr > 0.5f);
        var tex = _sprite.Texture;
        var size = tex.GetSize() * (Length / tex.GetHeight()) * VisualScale;
        var drop = (new Vector2(10f, 16f) * (0.15f + Altitude)).Rotated(-Rotation);   // screen down-right, in the hull's frame
        DrawTextureRect(tex, new Rect2(drop - size / 2, size), false, new Color(0, 0, 0, 0.45f - 0.15f * Altitude));

        float aura = State == St.Charging ? (float)Math.Min(1, T / Economy.HaulerCharge)
                   : State == St.Arriving ? Mathf.Max(0f, 1f - (float)(T / 1.0)) : 0f;
        if (aura <= 0) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)T * 14f);
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(0.45f, 1f));        // stretched along the hull
        for (int i = 0; i < 3; i++)
        {
            float r = Length * (0.36f + 0.08f * i) + 6f * pulse;
            DrawCircle(Vector2.Zero, r, new Color(0.35f, 0.65f, 1f, (0.20f + 0.15f * pulse) * aura / (i + 1)));
            DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, new Color(0.7f, 0.9f, 1f, 0.9f * aura / (i + 1)), 3f);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    // above the hull: the pods, the cargo stream while loading or offloading, the sale on return
    private void DrawOverlay()
    {
        float s = VisualScale;
        int pods = Yard.Pods; double podSize = Yard.PodSize; double shown = ShownCargo;
        float flash = State == St.Arriving ? Mathf.Max(0f, 1f - (float)(T / 1.2)) : 0f;   // emptying on return
        for (int i = 0; i < Economy.MaxPods; i++)
        {
            var r = new Rect2((PodCentre[i] - PodSize / 2) * s, PodSize * s);
            if (i >= pods) { _overlay.DrawRect(r, new Color(0.03f, 0.04f, 0.06f, 0.62f)); continue; }   // not yet bought
            float fill = (float)Math.Clamp((shown - i * podSize) / podSize, 0, 1);
            if (fill > 0)   // fills from the rear of the pod forward (and an offload empties it the other way)
                _overlay.DrawRect(new Rect2(r.Position + new Vector2(0, r.Size.Y * (1 - fill)), new Vector2(r.Size.X, r.Size.Y * fill)),
                                  new Color(0.35f, 0.8f, 1f, fill >= 1 ? 0.5f : 0.35f));
            if (flash > 0) _overlay.DrawRect(r, new Color(1f, 0.95f, 0.7f, 0.55f * flash));
            _overlay.DrawRect(r, new Color(0.6f, 0.9f, 1f, fill >= 1 ? 0.95f : 0.45f), false, 1f);
        }

        var inv = GlobalTransform.AffineInverse();
        if (State == St.Loading && Cargo < Yard.Capacity - 1e-6 && Yard.Ore + Yard.Salvage > 0.5)
        {   // crates of stock coming down the stem into the pod being filled
            int into = Math.Min(pods - 1, (int)(Cargo / podSize));
            var from = inv * Hub.StemFoot; var to = PodCentre[into] * s;
            for (int k = 0; k < 5; k++)
            {
                float f = (float)((T * 1.2 + k / 5.0) % 1.0);
                _overlay.DrawRect(new Rect2(from.Lerp(to, f) - new Vector2(2.5f, 2.5f), new Vector2(5f, 5f)),
                                  new Color(0.9f, 0.75f, 0.45f, Mathf.Min(1f, 4f * (1f - f))));
            }
        }
        if (State == St.Escorting && _stop > 0 && shown > 0.5)
        {   // OFFLOADING beside an outpost: the loading run backwards -- crates out of the pod being
            // emptied (the last one filled), across to the station
            int outOf = Math.Clamp((int)Math.Ceiling(shown / podSize) - 1, 0, pods - 1);
            var from = PodCentre[outOf] * s; var to = inv * Hub.Outposts[Math.Min(_leg, Hub.Outposts.Length - 1)].at;
            for (int k = 0; k < 5; k++)
            {
                float f = (float)((T * 1.2 + k / 5.0) % 1.0);
                _overlay.DrawRect(new Rect2(from.Lerp(to, f) - new Vector2(2.5f, 2.5f), new Vector2(5f, 5f)),
                                  new Color(0.9f, 0.75f, 0.45f, Mathf.Min(1f, 4f * (1f - f))));
            }
        }
        if (State == St.Arriving || (State == St.Returning && T < 1.0))
        {   // the sale, rising off the hull -- x5 for an escort; nothing, and why, for a lost run
            float age = (float)(State == St.Arriving ? T : 2.0 + T), a = Mathf.Clamp(1.6f - age * 0.5f, 0f, 1f);
            _overlay.DrawSetTransform(inv * (GlobalPosition + new Vector2(0, -60f - 14f * age)), -GlobalRotation, Vector2.One);
            if (RunLost) Txt.Centre(_overlay, ThemeDB.FallbackFont, Vector2.Zero, "CARGO LOST", 20, Ui.Bad with { A = a });
            else Txt.Centre(_overlay, ThemeDB.FallbackFont, Vector2.Zero, $"+{LastSale:0} cr" + (Escorted ? "  ×5" : ""), 20, new Color(1f, 0.9f, 0.5f, a));
            _overlay.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}

// The hauler's floating control, riding above it while it loads: DISPATCH (alone, with the
// EVASION chance) and ESCORT (x5), and a bar of the load with a tick between pods. The buttons
// are the base owner's; a guest sees the load. Greyed out until one pod is full.
public partial class HaulerHud : Control
{
    public Hub Hub;
    private Yard Yard => Hub.Yard;
    private Button _go, _escort;
    private const float W = 260f, GoW = 160f;

    public override void _Ready()
    {
        Name = "HaulerHud";
        Ui.Style(this);          // a CanvasLayer child of its own: it inherits nothing
        MouseFilter = MouseFilterEnum.Ignore;
        _go = new Button { Name = "Dispatch", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(GoW, 30) };
        _go.Pressed += () => Yard.RequestDispatch(escorted: false);
        _escort = new Button { Name = "Escort", FocusMode = FocusModeEnum.None, Text = "ESCORT  ×5", Position = new Vector2(GoW + 4, 0),
                               CustomMinimumSize = new Vector2(W - GoW - 4, 30) };
        _escort.Pressed += () => Yard.RequestDispatch(escorted: true);
        AddChild(_go); AddChild(_escort);
    }

    public override void _Process(double delta)
    {
        var h = Yard.Hauler;
        Position = GetViewport().GetCanvasTransform() * (h.GlobalPosition + new Vector2(0, -70f)) - new Vector2(W / 2, 40);
        // Shown only while loading, wholly on screen, and not under the BASE menu: at a
        // screen edge it was cut in half, and under the menu its bar peeked out as a
        // stray sliver.
        bool onScreen = GetViewportRect().Encloses(new Rect2(Position, new Vector2(W, 46)));
        bool underMenu = GetParent()?.GetNodeOrNull("BasePanel") != null;
        _go.Visible = _escort.Visible = Yard.IsMyOwnBase;
        Visible = h.State == Hauler.St.Loading && onScreen && !underMenu;
        if (!Visible) return;
        _go.Disabled = _escort.Disabled = !h.CanDispatch;
        if (h.CanDispatch && Yard.IsMyOwnBase) Hub.Hints.Meet("hauler");
        Ui.SetText(_go, h.CanDispatch ? $"DISPATCH  {Yard.RunSafe * 100:0}% safe" : $"LOADING  {h.Cargo:0}/{Yard.PodSize:0}");
        QueueRedraw();
    }

    public override void _Draw()
    {
        var h = Yard.Hauler;
        var r = new Rect2(0, 34, W, 8);
        DrawRect(r.Grow(3), new Color(0.04f, 0.06f, 0.10f, 0.9f));
        DrawRect(new Rect2(r.Position, new Vector2(W * (float)Math.Clamp(h.Cargo / Math.Max(1, Yard.Capacity), 0, 1), r.Size.Y)),
                 new Color(0.35f, 0.8f, 1f));
        for (int i = 1; i < Yard.Pods; i++) DrawLine(new Vector2(W * i / Yard.Pods, 32), new Vector2(W * i / Yard.Pods, 44), new Color(0.04f, 0.06f, 0.10f), 2f);
        DrawRect(r, new Color(0.6f, 0.8f, 1f, 0.8f), false, 1f);
    }
}
