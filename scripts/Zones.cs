using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// ZONES (F9) — a patch of space a pilot lays, which the host watches and every peer draws.
//
// REPLACED: nothing; the v1 tether mine and the curtain were never built. The gravity well's own host list and
// drawing (kits 6b) folded into it as its third row. A zone is a row of Zones.All, laid by
// an ability row that names it (AbilityDef.Lays), and in the world it is one spawn of Spawns.All ("Zone"): every
// peer draws it from the seed (the row, where, its rotation, its age), so a zone costs one message.
//
// A ROW OF Zones.All MUST FILL IN (what every peer draws is literal on the row; what only the host decides is a
// stat of the LAYER's sheet, read at the lay and kept on the host's node):
//   Id       what it is called (a blow it deals is credited to this id, Dealt)
//   Reach    how far from its centre -- or from its bar -- it reaches (u)
//   Length   0: a disc of Reach. Otherwise a BAR: a line this long across its rotation, Reach either side of it
//   Touch    a body counts once its HULL is within reach (IHittable.Covers); otherwise once its centre is
//   Arm      seconds after it is laid before it takes anything (dim until then)
//   Life     seconds from the lay to its end (0: until it goes off)
//   Prey     what it takes (a TargetFilter, never a type test)
//   Holds    a TRAP: the first prey inside sets it off, every prey inside is held by this Status for HoldFor
//            (a stat), and it goes (Status.None: it is no trap)
//   First, Tick, Every   a FIELD (stat ids; null: it deals nothing): a body takes First the frame it is first
//            inside, then Tick every Every seconds while it stays
//   Light, Heavy   a PULL (stat ids; null: it pulls nothing): every prey inside is dragged straight at its
//            centre, never past it, at Light u/s -- Heavy for a Tag.Heavy craft. A craft LATCHED on its prey
//            (ISquadMember.Latched) or on a tow line (ITowable.Towed) is not loose, and stays put
//   Most     a stat: at most this many of the row laid by one pilot; the oldest goes (null: no limit)
//   Near, Far   laid at the cursor, clamped this near and far, its bar across the aim (0, 0: at the stern)
//   Tint     its colour
//
// AUTHORITY: the host lays it (Zones.Lay, from the ability's press), decides what it takes (Zones.Tick)
// and lets it go (Hub.Down); a guest's copy takes nothing.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ZoneDef
{
    public string Id;
    public float Reach, Length;
    public bool Touch;
    public double Arm, Life;
    public TargetFilter Prey;
    public Status Holds;
    public string HoldFor, Most, First, Tick, Every, Light, Heavy;
    public float Near, Far;
    public Color Tint;
}

public static class Zones
{
    // The index IS the id on the wire (the spawn seed's N), so APPEND ONLY.
    public const int Tether = 0, Curtain = 1, Well = 2;

    public static readonly ZoneDef[] All =
    {
        // THE SNIPER'S TETHER MINE (kits_v2's card, v1): 170 u, live 0.5 s after the drop, the first raiding
        // craft in reach sets it off and every raiding craft in reach is held (Disabled) tether_hold seconds
        new() { Id = "tether", Reach = 170f, Arm = 0.5, Prey = Targeting.Raiding, Holds = Status.Disabled,
                HoldFor = "tether_hold", Most = "tether_most", Tint = new Color(0.45f, 0.85f, 1f) },
        // THE WARDEN'S FLAK CURTAIN (kits_v2's card): 500 x 80 u at the cursor (150-700 u), across the aim, live
        // 0.5 s after the press for 6 s; a raiding craft touching it takes 20, then 10 every 0.5 s
        new() { Id = "curtain", Reach = 40f, Length = 500f, Touch = true, Arm = 0.5, Life = 6.0, Prey = Targeting.Raiding,
                First = "curtain_first", Tick = "curtain_tick", Every = "curtain_every", Near = 150f, Far = 700f,
                Tint = new Color(1f, 0.62f, 0.25f) },
        // THE BASTION'S GRAVITY WELL (kits_v31's card): 280 u at the cursor (up to 900 u out), for 6 s from the press;
        // a loose raiding craft inside is dragged to its centre at well_light u/s, a heavy one at well_heavy
        new() { Id = "well", Reach = 280f, Life = 6.0, Prey = Targeting.Pullable, Light = "well_light", Heavy = "well_heavy",
                Near = 0f, Far = 900f, Tint = new Color(0.72f, 0.48f, 1f) },
    };
    public static ZoneDef Of(int row) => All[row >= 0 && row < All.Length ? row : Tether];
    public static int RowOf(ZoneDef d) => System.Array.IndexOf(All, d);

    // ── the pure rules ──────────────────────────────────────────────────────────
    // The point of a zone's line nearest `p`: its centre for a disc, else the nearest point of its bar, which
    // runs across `rot` (along Vector2.Right.Rotated(rot)).
    public static Vector2 Nearest(ZoneDef d, Vector2 at, float rot, Vector2 p)
    {
        if (d.Length <= 0) return at;
        var half = Vector2.Right.Rotated(rot) * d.Length * 0.5f;
        return Geometry2D.GetClosestPointToSegment(p, at - half, at + half);
    }
    // Whether a point `p` is inside a zone of row `d` laid at `at`, rotated `rot`.
    public static bool Inside(ZoneDef d, Vector2 at, float rot, Vector2 p) => Nearest(d, at, rot, p).DistanceTo(p) <= d.Reach;
    // Whether a body counts: its hull within reach for a Touch row, its centre otherwise.
    public static bool Takes(ZoneDef d, Vector2 at, float rot, IHittable h)
        => d.Touch ? h.Covers(Nearest(d, at, rot, h.Position), d.Reach) : Inside(d, at, rot, h.Position);
    // Where a zone laid by a ship at `from` aiming at `aim` goes, and its rotation: the cursor clamped to Near-Far
    // with the bar across the aim; the stern, at the ship's own rotation, for a row with no Far.
    public static (Vector2 at, float rot) Spot(ZoneDef d, Vector2 from, float shipRot, float halfLength, Vector2 aim)
    {
        if (d.Far <= 0) return (from + Vector2.Down.Rotated(shipRot) * halfLength, shipRot);
        var off = aim - from;
        var dir = off.LengthSquared() > 1e-6f ? off.Normalized() : Vector2.Up.Rotated(shipRot);
        float reach = Mathf.Clamp(off.Length(), d.Near, d.Far);
        return (from + dir * reach, dir.Angle() + Mathf.Pi / 2f);
    }

    // ── the host ────────────────────────────────────────────────────────────────
    // A row laid by `by` at `at`, rotated `rot`: what the host alone decides read from its sheet now; past the
    // row's Most of this pilot's, the oldest goes first.
    public static ZoneNode Lay(Hub hub, ZoneDef d, Vector2 at, float rot, PlayerShip by)
    {
        if (!Net.IsHost || by == null) return null;
        int row = RowOf(d);
        if (d.Most != null)
        {
            var mine = hub.Laid.Where(z => z.Row == row && z.OwnerId == by.OwnerId).ToList();
            int most = System.Math.Max(1, (int)by.Stats[d.Most]);
            for (int i = 0; i < mine.Count && mine.Count - i >= most; i++) hub.Down(Spawns.Zone, mine[i], burst: false, float.NaN);
        }
        if (hub.Spawn(Spawns.Zone, at, row, rot, 0) is not ZoneNode z) return null;
        z.OwnerId = by.OwnerId;
        double S(string id) => id != null ? by.Stats[id] : 0;
        z.HoldFor = S(d.HoldFor); z.FirstHit = S(d.First); z.TickHit = S(d.Tick); z.Every = S(d.Every);
        z.LightPull = S(d.Light); z.HeavyPull = S(d.Heavy);
        return z;
    }

    // Every frame: a zone past its Life goes; an armed trap with prey inside goes off -- every prey inside held,
    // the zone gone; an armed pull drags what is inside and loose toward its centre; an armed field strikes what
    // is inside on its own clock.
    public static void Tick(Hub hub, double delta)
    {
        if (!Net.IsHost || hub.Laid.Count == 0) return;
        foreach (var z in hub.Laid.ToList())
        {
            var d = z.Def;
            if (d.Life > 0 && z.Age >= d.Life) { hub.Down(Spawns.Zone, z, burst: false, float.NaN); continue; }
            if (!z.Armed) continue;
            var inside = Targeting.Hittable(Combat.Hostiles, d.Prey).Where(h => Takes(d, z.At, z.Rot, h)).ToList();
            if (d.Holds != Status.None)
            {
                if (inside.Count == 0) continue;
                foreach (var h in inside) if (h is IStatused s) s.ApplyStatus(d.Holds, z.HoldFor);
                hub.Down(Spawns.Zone, z, burst: true, float.NaN);
                continue;
            }
            if (d.Light != null) foreach (var h in inside) Pull(z, h, delta);
            if (d.First == null) continue;
            var by = hub.ShipOf(z.OwnerId);
            foreach (var h in inside)
            {
                if (!z.Next.TryGetValue(h, out double next)) { z.Next[h] = z.Age + z.Every; Dealt.Deal(h, z.FirstHit, by, d.Id); }
                // the remainder carries while it stays inside; a body that was out a whole tick or more
                // restarts its clock from now (one tick on its return, never the ticks it missed at once)
                else if (z.Age >= next - 1e-6) { z.Next[h] = (z.Age - next >= z.Every ? z.Age : next) + z.Every; Dealt.Deal(h, z.TickHit, by, d.Id); }
            }
        }
    }

    // One frame of a pull on one body: straight at the zone's centre, never past it; a latched or towed craft stays.
    static void Pull(ZoneNode z, IHittable h, double delta)
    {
        if (h is not Node2D n || h is ISquadMember { Latched: true } || h is ITowable { Towed: not null }) return;
        var off = z.At - n.GlobalPosition;
        float d = off.Length();
        if (d < 1e-3f) return;
        float rate = (float)(TagExt.Is(h, Tag.Heavy) ? z.HeavyPull : z.LightPull);
        n.GlobalPosition += off / d * Mathf.Min(d, rate * (float)delta);
    }
}

// ONE ZONE IN THE WORLD, drawn on every peer from its seed.
public partial class ZoneNode : Node2D
{
    public int NetId;
    public int Row;
    public Vector2 At;         // where it was laid
    public float Rot;          // its rotation (a bar runs along Vector2.Right.Rotated(Rot))
    public double Age;         // seconds since it was laid (a joiner is told how old it is)
    // THE HOST'S alone: whose it is, and what it holds, deals or pulls (the layer's sheet at the lay), and when each
    // body inside a field is struck next
    public int OwnerId = -1;
    public double HoldFor, FirstHit, TickHit, Every, LightPull, HeavyPull;
    public readonly Dictionary<IHittable, double> Next = new();
    public ZoneDef Def => Zones.Of(Row);
    public bool Armed => Age >= Def.Arm;

    public override void _Ready() { Position = At; Rotation = Rot; ZIndex = 4; }
    public override void _ExitTree() => Next.Clear();
    public override void _Process(double delta) { Age += delta; QueueRedraw(); }
    public override void _Draw()
    {
        var d = Def;
        float live = Armed ? 1f : 0.4f;
        if (d.Life > 0) live *= (float)System.Math.Clamp((d.Life - Age) / 0.5, 0, 1);
        if (d.Length <= 0)
        {
            DrawArc(Vector2.Zero, d.Reach, 0f, Mathf.Tau, 64, new Color(d.Tint, 0.35f * live), 1.5f);
            DrawCircle(Vector2.Zero, d.Reach, new Color(d.Tint, 0.05f * live));
            DrawCircle(Vector2.Zero, 8f, new Color(d.Tint, 0.9f * live));
            DrawCircle(Vector2.Zero, 3f, new Color(1f, 1f, 1f, live));
            if (d.Light != null)   // a pull: three rings running in to the centre
                for (int i = 0; i < 3; i++)
                {
                    float r = d.Reach * (1f - Mathf.PosMod((float)Age * 0.8f + i / 3f, 1f));
                    DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 64, new Color(d.Tint, 0.5f * live * r / d.Reach), 2f);
                }
            return;
        }
        // a bar: its capsule, and five bursts of flak along it, flickering
        float half = d.Length * 0.5f;
        var fill = new Color(d.Tint, 0.12f * live);
        DrawRect(new Rect2(-half, -d.Reach, d.Length, d.Reach * 2f), fill);
        DrawCircle(new Vector2(-half, 0), d.Reach, fill);
        DrawCircle(new Vector2(half, 0), d.Reach, fill);
        var edge = new Color(d.Tint, 0.45f * live);
        DrawLine(new Vector2(-half, -d.Reach), new Vector2(half, -d.Reach), edge, 1.5f);
        DrawLine(new Vector2(-half, d.Reach), new Vector2(half, d.Reach), edge, 1.5f);
        for (int i = 0; i < 5; i++)
        {
            float x = -half + d.Length * (i + 0.5f) / 5f, k = 0.5f + 0.5f * Mathf.Sin((float)Age * 9f + i * 1.7f);
            DrawCircle(new Vector2(x, 0), d.Reach * (0.45f + 0.25f * k), new Color(d.Tint, 0.25f * live * k));
            DrawCircle(new Vector2(x, 0), 5f, new Color(1f, 0.9f, 0.7f, 0.8f * live));
        }
    }
}
