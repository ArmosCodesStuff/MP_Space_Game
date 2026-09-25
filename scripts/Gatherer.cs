using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// GATHERERS — ONE ROW PER GATHERER, and nothing about a gatherer anywhere else.
//
//   OUTBOUND   fly to their work spot (see Yard.WorkSpot)
//   WORKING    beam until the hold is full: a Shaft is one steady beam, a Scan a
//              narrow, sweeping spray of forking arcs
//   QUEUED     full, and every arm is busy: wait in line (Yard's queue)
//   DOCKING    an arm is theirs: fly to its berth off the open face (Dock.Berth, Docks.cs)
//   UNLOADING  swing nose-in (Docks.NoseIn), empty into the arm, then free it for the next in line
//
// Above each, a hollow yellow bar fills as the hold does. Host-simulated; guests
// draw from the Yard's 10 Hz state.
//
// A gatherer's KIND was `bool M => Kind == GatherKind.Miner` and then `M ? "miner_hold" :
// "salvager_hold"` -- its hold, its speed, its rate, its hull, its sprite, its tab, its label,
// its beam and the colour of its cargo. Economy.All carried the SAME FIVE ROWS TWICE, identical
// in kind, cost, growth and cap and differing only in an id prefix, a label and a unit;
// Yard.Deposit was an if/else into two named fields; Character.Save had a key per resource. A
// refiner was five ternaries, a third Match in the fleet sync, a third branch in Deposit, a third
// economy tab, a third resource field and two new save keys -- and nothing would force any of it.
//
// A NEW GATHERER MUST FILL IN: an appended GatherKind and a row. That row generates its five
// economy rows (Economy.Rows), its tab, its stock, its save key and its place in the fleet.
// A NEW GATHERER MUST NOT: add a field to Yard, a key to Character.Save, a row to Economy.All,
// or an arm to any `Kind ==` test. It needs a new Site or Beam only if it works somewhere new or
// looks new doing it.
//
// THE SAVE FORMAT IS PART OF THE TABLE. Resource is the key under [base] and the upgrade ids are
// the keys under [base_levels], so the ids here are EXACTLY the ids already on every pilot's
// disk. That is why RateId is a field: the beam's row is "mine_rate", not "miner_rate", and
// renaming it would silently drop a paid-for level off every save file in the world.
// ─────────────────────────────────────────────────────────────────────────────
public enum GatherKind { Miner, Salvager }
public enum GatherSite { Rocks, Wreck }        // where it works (Yard.WorkSpot flies one of these)
public enum GatherBeam { Shaft, Scan }         // what working looks like (Gatherer._Draw draws one)

public class GathererDef
{
    public string Id, Name;          // "miner" / "Miner": its upgrade ids' prefix, and its label
    public string Texture;
    public string Tab;               // the BASE tab its five rows -- and its rebuild price -- are under
    public string Resource;          // the stock id it deposits into, AND its key under [base]
    public string Unit;              // what its hold and its beam are priced in ("ore", then "ore/s")
    public GatherSite Site;
    public GatherBeam Beam;
    public Color CargoTint;          // the load dropping through the arm's open face
    public double Hold = 100;        // units, before upgrades
    public double Speed = 110;       // u/s
    public double Rate = 2;          // units a second while beaming
    public double Hull = 120;
    // ITS BEAM'S ROW. Four of its five ids are "<Id>_count / _hold / _speed / _hull"; the beam's
    // is not, because the beam's id on disk is "mine_rate". Data, not a rule.
    public string RateId, RateName, RateBlurb;
    // Where its ships come off the pad, from the base, and the step between them.
    public Vector2 Muster, MusterStep;

    public string CountId => Id + "_count";
    public string HoldId  => Id + "_hold";
    public string SpeedId => Id + "_speed";
    public string HullId  => Id + "_hull";
}

public static class Gathering
{
    // The index IS the id on the wire: the fleet is built and reported in this order (Yard's
    // per-gatherer arrays line up by position). So APPEND ONLY.
    public static readonly GathererDef[] All =
    {
        new() { Id = "miner", Name = "Miner", Texture = "res://miner.png", Tab = "MINERS",
                Resource = "ore", Unit = "ore", Site = GatherSite.Rocks, Beam = GatherBeam.Shaft,
                CargoTint = new Color(0.75f, 0.5f, 0.3f),
                RateId = "mine_rate", RateName = "Mining beam", RateBlurb = "+10% mining speed",
                Muster = new Vector2(-60, -300), MusterStep = new Vector2(30, 0) },

        new() { Id = "salvager", Name = "Salvager", Texture = "res://salvager.png", Tab = "SALVAGERS",
                Resource = "salvage", Unit = "salvage", Site = GatherSite.Wreck, Beam = GatherBeam.Scan,
                CargoTint = new Color(0.75f, 0.78f, 0.82f),
                RateId = "salvage_rate", RateName = "Salvage beam", RateBlurb = "+10% salvage speed",
                Muster = new Vector2(-300, -60), MusterStep = new Vector2(0, 30) },
    };

    public static GathererDef Of(GatherKind k) => All[(int)k];

    // EVERY RESOURCE THE FLEET BRINGS IN, in table order: the keys of the Yard's stock, of the
    // guest's totals packet, and of [base] on disk. (Declared after All: a static field's
    // initialiser runs in the order it is written.)
    public static readonly string[] Resources = All.Select(d => d.Resource).Distinct().ToArray();
}

public partial class Gatherer : UtilityShip
{
    public enum St { Outbound, Working, Queued, Docking, Unloading, Destroyed }

    public GatherKind Kind;
    public int Index;                        // which miner (or salvager): 0, 1, 2 ...
    public St State = St.Outbound;
    // WHAT THIS SHIP IS: its row. Every number, every word and every art path below is its.
    public GathererDef Def => Gathering.Of(Kind);
    // Hull: full when it comes off the pad, upgrades included (_Ready). It used to start at the
    // base 120 whatever the hull level, and a fleet with a paid-for upgrade flew under a damage bar.
    public override double MaxHull => Yard.Value(Def.HullId);      // its row's 120, +10% a level
    public override string Category => Def.Tab;
    public override string Label => $"{Def.Name} {Index + 1}";
    public override bool InReach => State != St.Destroyed;
    public override bool Lost => State == St.Destroyed;
    public override (float halfLength, float halfWidth) Extent => (20f, 12f);
    protected override float LostBlast => 30f;
    protected override float RebuiltBlast => 26f;
    protected override void OnLost()
    {
        Yard.Release(this);                          // its arm or its place in the queue goes to the next
        State = St.Destroyed; Velocity = Vector2.Zero; Visible = false;   // gone at once
    }
    public Vector2 Velocity;
    public Vector2 BeamTo;                   // world point the beam works (host-chosen)
    public const float Length = 40f;
    private const float Accel = 220f;

    private Sprite2D _sprite;
    private NetPose _net;
    private readonly RandomNumberGenerator _rng = new();
    private double _crackle, _t;
    private readonly List<Vector2[]> _bolts = new();

    public double Hold  => Yard.Value(Def.HoldId);
    public double Speed => Yard.Value(Def.SpeedId);
    public double Rate  => Yard.Value(Def.RateId);
    public bool Beaming => State == St.Working;

    public override void _Ready()
    {
        Hull = MaxHull;
        _sprite = Sprites.Fit(Def.Texture, Length);
        AddChild(_sprite);
        ZIndex = 1;
    }

    public void SetNet(Vector2 p, float rot, int state, float cargo, Vector2 beam, float hull, float rebuild)
    {
        bool wasLost = Lost;
        _net.Set(p, rot);
        (State, Cargo, BeamTo) = ((St)state, cargo, beam);
        FromHost(hull, rebuild, wasLost);
    }

    // back to work, empty (a session change)
    public void ResetToWork() { State = St.Outbound; Cargo = 0; Velocity = Vector2.Zero; _net = default; Hull = MaxHull; RebuildIn = 0; WaitingForCredits = false; }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += delta;
        WatchHull();
        Visible = State != St.Destroyed;         // a lost ship is gone until it is rebuilt
        if (Net.Sim) Simulate(dt);
        else
        {
            GuestClock(dt);
            _net.Follow(this, dt);
        }
        if (Def.Beam == GatherBeam.Scan && Beaming)
        {
            _crackle -= delta;
            if (_crackle <= 0) { _crackle = 0.05; Rescan(); }
        }
        QueueRedraw();
    }

    private void Simulate(float dt)
    {
        if (State == St.Destroyed)
        {   // 30 s after it was lost it is rebuilt at the base, for 10% of its category's investment
            if (TickRebuild(dt)) { Hull = MaxHull; State = St.Outbound; Position = Hub.BasePos; Velocity = Vector2.Zero; Burst(rebuilt: true); }
            return;
        }
        if (Pinned)
        {   // pinned by a raider: thrusting forward at 20% of its speed, unable to turn
            TickStatus(dt);
            Velocity = Vector2.Up.Rotated(Rotation) * (float)Speed * StatusSet.PinSpeed;
            Position += Velocity * dt;
            return;
        }
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
                // No arm means it lost the one it was flying to. Arms[-1] would throw; ask again
                // instead, and queue if none is free.
                if (arm < 0) { State = Yard.RequestArm(this) >= 0 ? St.Docking : St.Queued; break; }
                if (FlyTo(Yard.Arms[arm].Dock.Berth(Length), dt)) State = St.Unloading;
                break;
            }
            case St.Unloading:
            {
                int arm = Yard.ArmOf(this);
                if (arm < 0) { State = St.Outbound; break; }        // arm taken away mid-unload: go back out
                // onto its berth and nose in: the berth and the swing every craft on a dock uses
                var dock = Yard.Arms[arm].Dock;
                Position = Position.Lerp(dock.Berth(Length), Mathf.Clamp(8f * dt, 0f, 1f));
                Docks.NoseIn(this, dock, dt);
                double amt = Math.Min(Cargo, Economy.UnloadRate * dt);
                Cargo -= amt; Yard.Deposit(Def.Resource, amt);
                if (Cargo <= 1e-6) { Cargo = 0; Yard.Release(this); State = St.Outbound; }
                break;
            }
        }
    }

    // arrive-steering: accelerate toward the point, easing off to stop on it
    private bool FlyTo(Vector2 to, float dt)
    {
        var d = to - Position; float dist = d.Length();
        float spd = Motion.Arrive((float)Speed, dist, Accel);
        Velocity = Velocity.MoveToward(dist > 1f ? d / dist * spd : Vector2.Zero, Accel * dt);
        Position += Velocity * dt;
        if (Velocity.LengthSquared() > 25f) Face(Position + Velocity, dt);
        return dist < 4f && Velocity.Length() < 12f;
    }

    private void Brake(float dt) { Velocity = Velocity.MoveToward(Vector2.Zero, Accel * dt); Position += Velocity * dt; }

    private void Face(Vector2 at, float dt) =>
        Rotation = Mathf.LerpAngle(Rotation, Aim.Face(Position, at), Mathf.Clamp(6f * dt, 0f, 1f));

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
        // a light yellow plume: utility ships ignore every player colour
        Plume.Draw(this, new Vector2(0, Length * 0.5f), Vector2.Down, Length, Plume.Utility, Velocity.Length() / (float)Speed, Velocity.Length() > 2f);
        var inv = GlobalTransform.AffineInverse();
        if (Beaming && Def.Beam == GatherBeam.Shaft)
            // one shaft: a steady core with a soft glow that breathes
            Beam.Draw(this, inv * Nose(), inv * BeamTo, _t, new Color(1f, 0.55f, 0.2f, 0.18f), new Color(1f, 0.7f, 0.35f, 0.45f), new Color(1f, 0.95f, 0.85f, 0.95f),
                      new Color(1f, 0.8f, 0.5f, 0.6f));
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
            var col = Def.CargoTint;
            for (int k = 0; k < 4; k++)
            {
                float f = (float)((_t * 1.6 + k / 4.0) % 1.0);
                DrawRect(new Rect2(new Vector2(-2f, -Length * 0.45f - f * 26f), new Vector2(4f, 4f)), new Color(col.R, col.G, col.B, 1f - f));
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
