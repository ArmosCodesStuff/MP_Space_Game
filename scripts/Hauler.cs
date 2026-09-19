using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// HAULER — carries the yard's stock out through the portal for sale.
//
// Home is the base's bottom pad. The portal sits on the same horizontal line, so
// every move between them is perfectly flat. It lands SMALLER (65%) than it flies:
// shrinking onto the pad, with its shadow tucking in underneath, reads as settling
// down out of the sky; growing again reads as lifting off.
//
//   LOADING    landed, facing the portal, filling its pods from the stock. Its
//              floating DISPATCH button works once one pod is full; completely
//              full, it goes by itself.
//   LIFTING    grows to flight size
//   DEPARTING  slides slowly east to the portal
//   CHARGING   shakes inside a building blue aura
//   AWAY       warps out; 30 s later its cargo is sold for credits
//   ARRIVING   warps back in facing the base, its pods flashing empty
//   RETURNING  slides slowly west to above its pad
//   TURNING    swings 180 degrees to face the portal
//   LANDING    shrinks back onto the pad, then loads again
//
// Six pods are painted on its hull; it starts with one working and the HAULER tab
// adds more (up to six) and makes each bigger.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Hauler : Node2D
{
    public enum St { Loading, Lifting, Departing, Charging, Away, Arriving, Returning, Landing, Destroyed }

    public Yard Yard;
    public St State = St.Loading;
    public double T;                         // seconds in the current state
    public double Cargo;
    public double LastSale;                  // credits paid on the last return
    public const float Length = 200f;
    public const float East = Mathf.Pi / 2f, West = -Mathf.Pi / 2f;
    private const float Accel = 25f;
    private float _speed;

    // the six painted pods, in the hull's own frame at flight size (nose up):
    // filled in pairs, front to back, port then starboard
    public static readonly Vector2[] PodCentre =
        { new(-23.6f, -51.6f), new(23.6f, -51.6f), new(-23.6f, -6.0f), new(23.6f, -6.0f), new(-23.6f, 39.5f), new(23.6f, 39.5f) };
    public static readonly Vector2 PodSize = new(21f, 43.4f);

    private Sprite2D _sprite;
    private Node2D _overlay;                 // drawn above the hull: pods, cargo motes, the sale
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
        ResetToPad();
    }

    public void ResetToPad() { Go(St.Loading); Cargo = 0; _speed = 0; _hasNet = false; Position = Hub.HaulerPad; Rotation = East; Hull = MaxHull; RebuildIn = 0; WaitingForCredits = false; }

    // hull, and being rebuilt (host-owned; guests get hull and state). Out of reach while away.
    public double Hull = Economy.HaulerHull;
    public double MaxHull => Economy.HaulerHull;
    public double RebuildIn;
    public bool WaitingForCredits;
    private double _pinT;
    public bool Pinned => _pinT > 0;
    public void PinFor(double s) { if (Net.Sim) _pinT = System.Math.Max(_pinT, s); }
    public void TakeDamage(double d)
    {
        if (!Net.Sim || State is St.Destroyed or St.Away) return;
        Hull -= d;
        if (Hull > 0) return;
        Hull = 0; Cargo = 0; _speed = 0; Go(St.Destroyed); Effects();          // gone at once
        RebuildIn = Economy.RebuildDelay; WaitingForCredits = false;
        GetParent().AddChild(new Explosion { Position = Position, Radius = 70f });
    }

    public void SetNet(Vector2 p, float rot, int state, float t, float cargo, float sale, float hull)
    {
        Hull = hull;
        (_netPos, _netRot, _hasNet) = (p, rot, true);
        if ((St)state != State || Math.Abs(T - t) > 0.5) T = t;
        (State, Cargo, LastSale) = ((St)state, cargo, sale);
    }

    // The button's request. Only while loading, and only with at least one full pod.
    public void Dispatch() { if (Net.Sim && CanDispatch) Go(St.Lifting); }

    private void Go(St s) { State = s; T = 0; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        T += delta;
        if (Net.Sim) Simulate(dt);
        else if (_hasNet)
        {
            Position = new Vector2(Mathf.Lerp(Position.X, _netPos.X, Mathf.Clamp(10f * dt, 0f, 1f)), _netPos.Y);
            Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(10f * dt, 0f, 1f));
        }
        Effects();
        QueueRedraw(); _overlay.QueueRedraw();
    }

    private void Simulate(float dt)
    {
        _pinT = System.Math.Max(0, _pinT - dt);
        switch (State)
        {
            case St.Loading:
                Position = Hub.HaulerPad; Rotation = East;
                Cargo += Yard.TakeStock(Math.Min(Economy.HaulerLoadRate * dt, Yard.Capacity - Cargo));
                if (Cargo >= Yard.Capacity - 1e-6) Go(St.Lifting);          // full: it goes by itself
                break;
            case St.Lifting:  if (T >= Economy.HaulerLift) Go(St.Departing); break;
            case St.Departing: if (Slide(Hub.PortalPos.X, dt)) Go(St.Charging); break;
            case St.Charging:
                if (T >= Economy.HaulerCharge) { Go(St.Away); Yard.PortalFlash(); }
                break;
            case St.Away:
                if (T >= Economy.HaulerAway)
                {
                    LastSale = Cargo * Economy.CreditsPerUnit;
                    Yard.Credits += LastSale; Cargo = 0;
                    Rotation = West; Go(St.Arriving); Yard.PortalFlash();
                }
                break;
            case St.Arriving: if (T >= 2.0) Go(St.Returning); break;
            case St.Returning:
                Rotation = West;
                if (Slide(Hub.HaulerPad.X, dt)) Go(St.Landing);   // over the pad: descend, turning as it goes
                break;
            case St.Destroyed:
            {   // rebuilt on its pad 30 s later, for 10% of what the hauler's upgrades have cost
                RebuildIn = System.Math.Max(0, RebuildIn - dt);
                if (RebuildIn > 0) break;
                double cost = Yard.RebuildCost("HAULER");
                if (Yard.Credits < cost) { WaitingForCredits = true; break; }
                Yard.Credits -= cost;
                ResetToPad();
                GetParent().AddChild(new Explosion { Position = Position, Radius = 60f, Tint = new Color(0.5f, 0.8f, 1f) });
                break;
            }
            case St.Landing:
            {   // down onto the pad, swinging from west to east on the way: it arrives facing the portal
                float k = Mathf.Clamp((float)(T / Economy.HaulerLand), 0f, 1f);
                Rotation = West + Mathf.Pi * k * k * (3f - 2f * k);
                if (k >= 1f) { Rotation = East; Go(St.Loading); }
                break;
            }
        }
        Position = new Vector2(Position.X, Hub.LaneY);                 // the lane: every move is flat
    }

    // along x only, easing in and out, never faster than the hauler's speed
    private bool Slide(float toX, float dt)
    {
        float d = toX - Position.X, dist = Mathf.Abs(d);
        float top = (float)Economy.HaulerSpeed * (Pinned ? Raider.PinSpeed : 1f);    // pinned: 20% along its lane
        _speed = Mathf.MoveToward(_speed, Mathf.Min(top, Mathf.Sqrt(2f * Accel * dist)), Accel * dt);
        if (Pinned) _speed = Mathf.Min(_speed, top);
        Position += new Vector2(Mathf.Sign(d) * Mathf.Min(_speed * dt, dist), 0);
        if (dist >= 0.5f) return false;
        Position = new Vector2(toX, Position.Y); _speed = 0; return true;
    }

    // 0 on the pad, 1 at flight height
    public float Altitude => State switch
    {
        St.Loading => 0f,
        St.Lifting => Ease((float)(T / Economy.HaulerLift)),
        St.Landing => 1f - Ease((float)(T / Economy.HaulerLand)),
        _ => 1f,
    };
    private static float Ease(float k) { k = Mathf.Clamp(k, 0f, 1f); return k * k * (3f - 2f * k); }
    public float VisualScale => Mathf.Lerp(Economy.HaulerLandedScale, 1f, Altitude);

    // Shake, warp, and the landing size all live on the sprite, so the node itself
    // never leaves its line.
    private void Effects()
    {
        bool gone = State == St.Away && T >= 0.35;
        _sprite.Visible = _overlay.Visible = !gone && State != St.Destroyed;
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
        float vs = VisualScale, thr = State is St.Departing or St.Returning ? 1f : 0.2f;
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

    // above the hull: the pods, the cargo stream while loading, the sale on return
    private void DrawOverlay()
    {
        float s = VisualScale;
        int pods = Yard.Pods; double podSize = Yard.PodSize;
        float flash = State == St.Arriving ? Mathf.Max(0f, 1f - (float)(T / 1.2)) : 0f;   // emptying on return
        for (int i = 0; i < Economy.MaxPods; i++)
        {
            var r = new Rect2((PodCentre[i] - PodSize / 2) * s, PodSize * s);
            if (i >= pods) { _overlay.DrawRect(r, new Color(0.03f, 0.04f, 0.06f, 0.62f)); continue; }   // not yet bought
            float fill = (float)Math.Clamp((Cargo - i * podSize) / podSize, 0, 1);
            if (fill > 0)   // fills from the rear of the pod forward
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
        if (State == St.Arriving || (State == St.Returning && T < 1.0))
        {   // the sale, rising off the hull
            float age = (float)(State == St.Arriving ? T : 2.0 + T);
            _overlay.DrawSetTransform(inv * (GlobalPosition + new Vector2(0, -60f - 14f * age)), -GlobalRotation, Vector2.One);
            Txt.Centre(_overlay, ThemeDB.FallbackFont, Vector2.Zero, $"+{LastSale:0} cr", 20, new Color(1f, 0.9f, 0.5f, Mathf.Clamp(1.6f - age * 0.5f, 0f, 1f)));
            _overlay.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}

// The hauler's floating control, riding above it while it loads: DISPATCH, and a
// bar of the load with a tick between pods. Greyed out until one pod is full.
public partial class HaulerHud : Control
{
    public Hub Hub;
    private Yard Yard => Hub.Yard;
    private Button _go;
    private const float W = 190f;

    public override void _Ready()
    {
        Name = "HaulerHud";
        MouseFilter = MouseFilterEnum.Ignore;
        _go = new Button { Name = "Dispatch", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(W, 30) };
        _go.Pressed += () => Yard.RequestDispatch();
        AddChild(_go);
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
        Visible = h.State == Hauler.St.Loading && onScreen && !underMenu;
        if (!Visible) return;
        _go.Disabled = !h.CanDispatch;
        _go.Text = h.CanDispatch ? $"DISPATCH  ({h.FullPods}/{Yard.Pods} pods full)" : $"LOADING  {h.Cargo:0}/{Yard.PodSize:0}";
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
