using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// GATHERERS — the miners and salvagers.
//
//   OUTBOUND   fly to their work spot (see Yard.WorkSpot)
//   WORKING    beam until the hold is full: the miner with one steady shaft, the
//              salvager with a narrow, sweeping scan of forking arcs
//   QUEUED     full, and every arm is busy: wait in line (Yard's queue)
//   DOCKING    an arm is theirs: fly to its open face and turn nose-in
//   UNLOADING  empty into the arm, then free it for the next in line
//
// Above each, a hollow yellow bar fills as the hold does. Host-simulated; guests
// draw from the Yard's 10 Hz state.
// ─────────────────────────────────────────────────────────────────────────────
public enum GatherKind { Miner, Salvager }

public partial class Gatherer : Node2D
{
    public enum St { Outbound, Working, Queued, Docking, Unloading }

    public Yard Yard;
    public GatherKind Kind;
    public int Index;                        // which miner (or salvager): 0, 1, 2 ...
    public St State = St.Outbound;
    public double Cargo;
    public Vector2 Velocity;
    public Vector2 BeamTo;                   // world point the beam works (host-chosen)
    public const float Length = 40f;
    private const float Accel = 220f;

    private Sprite2D _sprite;
    private Vector2 _netPos; private float _netRot; private bool _hasNet;
    private readonly RandomNumberGenerator _rng = new();
    private double _crackle, _t;
    private readonly List<Vector2[]> _bolts = new();

    bool M => Kind == GatherKind.Miner;
    public double Hold  => Yard.Value(M ? "miner_hold" : "salvager_hold");
    public double Speed => Yard.Value(M ? "miner_speed" : "salvager_speed");
    public double Rate  => Yard.Value(M ? "mine_rate" : "salvage_rate");
    public bool Beaming => State == St.Working;

    public override void _Ready()
    {
        var tex = GD.Load<Texture2D>(M ? "res://miner.png" : "res://salvager.png");
        _sprite = new Sprite2D { Texture = tex, Scale = Vector2.One * (Length / tex.GetHeight()) };
        AddChild(_sprite);
        ZIndex = 1;
    }

    public void SetNet(Vector2 p, float rot, int state, float cargo, Vector2 beam)
    {
        (_netPos, _netRot, _hasNet) = (p, rot, true);
        (State, Cargo, BeamTo) = ((St)state, cargo, beam);
    }

    // back to work, empty (a session change)
    public void ResetToWork() { State = St.Outbound; Cargo = 0; Velocity = Vector2.Zero; _hasNet = false; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += delta;
        if (Net.Sim) Simulate(dt);
        else if (_hasNet)
        {
            Position = Position.Lerp(_netPos, Mathf.Clamp(10f * dt, 0f, 1f));
            Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(10f * dt, 0f, 1f));
        }
        if (!M && Beaming)
        {
            _crackle -= delta;
            if (_crackle <= 0) { _crackle = 0.05; Rescan(); }
        }
        QueueRedraw();
    }

    private void Simulate(float dt)
    {
        switch (State)
        {
            case St.Outbound:
            {
                var (spot, target) = Yard.WorkSpot(this);
                BeamTo = target;
                if (FlyTo(spot, dt)) State = St.Working;
                break;
            }
            case St.Working:
            {
                BeamTo = Yard.WorkSpot(this).target;
                Brake(dt); Face(BeamTo, dt);
                Cargo = Math.Min(Hold, Cargo + Rate * dt);
                if (Cargo >= Hold) State = Yard.RequestArm(this) >= 0 ? St.Docking : St.Queued;
                break;
            }
            case St.Queued:
                if (Yard.ArmOf(this) >= 0) { State = St.Docking; break; }
                FlyTo(Yard.QueueSpot(Math.Max(0, Yard.QueueIndex(this))), dt);
                break;
            case St.Docking:
            {
                int arm = Yard.ArmOf(this);
                if (FlyTo(Yard.UnloadSpot(arm), dt)) State = St.Unloading;
                break;
            }
            case St.Unloading:
            {
                var a = Yard.Arms[Yard.ArmOf(this)];
                Position = Position.Lerp(Yard.UnloadSpot(Yard.ArmOf(this)), Mathf.Clamp(8f * dt, 0f, 1f));
                Rotation = Mathf.LerpAngle(Rotation, (-a.Open).Angle() + Mathf.Pi / 2f, Mathf.Clamp(6f * dt, 0f, 1f));
                double amt = Math.Min(Cargo, Economy.UnloadRate * dt);
                Cargo -= amt; Yard.Deposit(Kind, amt);
                if (Cargo <= 1e-6) { Cargo = 0; Yard.Release(this); State = St.Outbound; }
                break;
            }
        }
    }

    // arrive-steering: accelerate toward the point, easing off to stop on it
    private bool FlyTo(Vector2 to, float dt)
    {
        var d = to - Position; float dist = d.Length();
        float spd = Mathf.Min((float)Speed, Mathf.Sqrt(2f * Accel * dist));
        Velocity = Velocity.MoveToward(dist > 1f ? d / dist * spd : Vector2.Zero, Accel * dt);
        Position += Velocity * dt;
        if (Velocity.LengthSquared() > 25f) Face(Position + Velocity, dt);
        return dist < 4f && Velocity.Length() < 12f;
    }

    private void Brake(float dt) { Velocity = Velocity.MoveToward(Vector2.Zero, Accel * dt); Position += Velocity * dt; }

    private void Face(Vector2 at, float dt) =>
        Rotation = Mathf.LerpAngle(Rotation, (at - Position).Angle() + Mathf.Pi / 2f, Mathf.Clamp(6f * dt, 0f, 1f));

    private Vector2 Nose() => ToGlobal(new Vector2(0, -Length * 0.45f));

    // The salvager's scan: two forking arcs inside a narrow cone (about ±3 degrees),
    // the cone itself sweeping slowly side to side across the hull.
    private void Rescan()
    {
        _bolts.Clear();
        var nose = Nose(); var to = BeamTo - nose;
        float len = to.Length(), sweep = 0.20f * Mathf.Sin((float)_t * 2.4f);
        for (int b = 0; b < 2; b++)
        {
            var dir = to.Normalized().Rotated(sweep + _rng.RandfRange(-0.05f, 0.05f));
            var bolt = Jag(nose, nose + dir * len * _rng.RandfRange(0.96f, 1.0f), 8, 3.5f);
            _bolts.Add(bolt);
            if (b == 0) _bolts.Add(Jag(bolt[5], nose + dir.Rotated(_rng.RandfRange(-0.06f, 0.06f)) * len * 0.98f, 4, 2.5f));
        }
    }

    private Vector2[] Jag(Vector2 a, Vector2 b, int n, float amp)
    {
        var pts = new Vector2[n + 1];
        var perp = (b - a).Normalized().Orthogonal();
        for (int i = 0; i <= n; i++)
            pts[i] = a.Lerp(b, i / (float)n) + perp * ((i == 0 || i == n) ? 0 : _rng.RandfRange(-amp, amp));
        return pts;
    }

    public override void _Draw()
    {
        var inv = GlobalTransform.AffineInverse();
        if (Beaming && M)
        {   // one shaft: a steady core with a soft glow that breathes
            var a = inv * Nose(); var b = inv * BeamTo;
            float breathe = 1f + 0.15f * Mathf.Sin((float)_t * 11f);
            DrawLine(a, b, new Color(1f, 0.55f, 0.2f, 0.18f), 9f * breathe);
            DrawLine(a, b, new Color(1f, 0.7f, 0.35f, 0.45f), 4.5f * breathe);
            DrawLine(a, b, new Color(1f, 0.95f, 0.85f, 0.95f), 1.6f);
            DrawCircle(b, 5f * breathe, new Color(1f, 0.8f, 0.5f, 0.6f));
        }
        else if (Beaming)
            foreach (var bolt in _bolts)
            {
                var p = new Vector2[bolt.Length];
                for (int i = 0; i < p.Length; i++) p[i] = inv * bolt[i];
                DrawPolyline(p, new Color(0.5f, 0.85f, 1f, 0.25f), 4f);
                DrawPolyline(p, new Color(0.85f, 0.95f, 1f, 0.95f), 1.3f);
            }

        if (State == St.Unloading && Cargo > 0)
        {   // cargo dropping through the arm's open face
            var col = M ? new Color(0.75f, 0.5f, 0.3f) : new Color(0.75f, 0.78f, 0.82f);
            for (int k = 0; k < 4; k++)
            {
                float f = (float)((_t * 1.6 + k / 4.0) % 1.0);
                DrawRect(new Rect2(new Vector2(-2f, -Length * 0.45f - f * 26f) - new Vector2(0, 0), new Vector2(4f, 4f)), new Color(col.R, col.G, col.B, 1f - f));
            }
        }

        // the cargo bar: a hollow yellow rectangle over the ship, filling with the hold
        DrawSetTransform(inv * (GlobalPosition + new Vector2(0, -30f)), -GlobalRotation, Vector2.One);
        const float W = 30f, H = 4f;
        float fill = (float)Math.Clamp(Cargo / Math.Max(1, Hold), 0, 1);
        if (fill > 0) DrawRect(new Rect2(-W / 2, -H / 2, W * fill, H), new Color(1f, 0.85f, 0.2f, 0.85f));
        DrawRect(new Rect2(-W / 2, -H / 2, W, H), new Color(1f, 0.85f, 0.2f), false, 1f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
