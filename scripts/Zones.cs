using Godot;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// ZONES (F9) — a patch of space a pilot lays, which the host watches and every peer draws.
//
// REPLACED: nothing; the v1 tether mine was never built. A zone is a row of Zones.All, laid by an ability
// row that names it (AbilityDef.Lays), and in the world it is one spawn of Spawns.All ("Zone"): every peer
// draws it from the seed (the row, where, the layer's rotation, its age), so a zone costs one message.
//
// A ROW OF Zones.All MUST FILL IN:
//   Id       what it is called
//   Reach    how far from its centre it takes a body (u; literal, because every peer draws it from the row)
//   Arm      seconds after it is laid before it takes anything (literal, drawn: dim until then)
//   Prey     what it takes (a TargetFilter, never a type test)
//   Holds    the first prey inside SETS IT OFF: every prey inside is held by this Status...
//   HoldFor  ...for this stat of the LAYER's sheet (read at the lay, kept on the host's node), and it goes
//   Most     a stat of the layer's sheet: at most this many of the row laid by one pilot; the oldest goes
//   Tint     its colour
//
// AUTHORITY: the host lays it (Zones.Lay, from the ability's press), decides what it takes (Zones.Tick)
// and lets it go (Hub.Down); a guest's copy takes nothing.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ZoneDef
{
    public string Id;
    public float Reach;
    public double Arm;
    public TargetFilter Prey;
    public Status Holds;
    public string HoldFor, Most;
    public Color Tint;
}

public static class Zones
{
    // The index IS the id on the wire (the spawn seed's N), so APPEND ONLY.
    public const int Tether = 0;

    public static readonly ZoneDef[] All =
    {
        // THE SNIPER'S TETHER MINE (kits_v2's card, v1): 170 u, live 0.5 s after the drop, the first raiding
        // craft in reach sets it off and every raiding craft in reach is held (Disabled) tether_hold seconds
        new() { Id = "tether", Reach = 170f, Arm = 0.5, Prey = Targeting.Raiding, Holds = Status.Disabled,
                HoldFor = "tether_hold", Most = "tether_most", Tint = new Color(0.45f, 0.85f, 1f) },
    };
    public static ZoneDef Of(int row) => All[row >= 0 && row < All.Length ? row : Tether];
    public static int RowOf(ZoneDef d) => System.Array.IndexOf(All, d);

    // ── the pure rule ───────────────────────────────────────────────────────────
    // Whether a body at `p` is inside a zone of row `d` laid at `at`.
    public static bool Inside(ZoneDef d, Vector2 at, Vector2 p) => at.DistanceTo(p) <= d.Reach;

    // ── the host ────────────────────────────────────────────────────────────────
    // A row laid by pilot `owner` at `at`: its hold read from the layer's sheet now; past the row's Most
    // of this pilot's, the oldest goes first.
    public static ZoneNode Lay(Hub hub, ZoneDef d, Vector2 at, float rot, int owner, double holdFor, int most)
    {
        if (!Net.IsHost) return null;
        int row = RowOf(d);
        var mine = hub.Laid.Where(z => z.Row == row && z.OwnerId == owner).ToList();
        for (int i = 0; i < mine.Count && mine.Count - i >= System.Math.Max(1, most); i++) hub.Down(Spawns.Zone, mine[i], burst: false, float.NaN);
        if (hub.Spawn(Spawns.Zone, at, row, rot, 0) is not ZoneNode z) return null;
        z.OwnerId = owner; z.HoldFor = holdFor;
        return z;
    }

    // Every frame: an armed zone with prey inside goes off -- every prey inside held, the zone gone.
    public static void Tick(Hub hub)
    {
        if (!Net.IsHost || hub.Laid.Count == 0) return;
        foreach (var z in hub.Laid.ToList())
        {
            var d = z.Def;
            if (!z.Armed) continue;
            var caught = Targeting.Hittable(Combat.Hostiles, d.Prey).Where(h => Inside(d, z.At, h.Position)).ToList();
            if (caught.Count == 0) continue;
            foreach (var h in caught) if (h is IStatused s) s.ApplyStatus(d.Holds, z.HoldFor);
            hub.Down(Spawns.Zone, z, burst: true, float.NaN);
        }
    }
}

// ONE ZONE IN THE WORLD, drawn on every peer from its seed.
public partial class ZoneNode : Node2D
{
    public int NetId;
    public int Row;
    public Vector2 At;         // where it was laid
    public float Rot;          // the layer's rotation then
    public double Age;         // seconds since it was laid (a joiner is told how old it is)
    // THE HOST'S alone: whose it is, and how long what it takes is held (the layer's sheet at the lay)
    public int OwnerId = -1;
    public double HoldFor;
    public ZoneDef Def => Zones.Of(Row);
    public bool Armed => Age >= Def.Arm;

    public override void _Ready() { Position = At; ZIndex = 4; }
    public override void _Process(double delta) { Age += delta; QueueRedraw(); }
    public override void _Draw()
    {
        var d = Def;
        float live = Armed ? 1f : 0.4f;
        DrawArc(Vector2.Zero, d.Reach, 0f, Mathf.Tau, 64, new Color(d.Tint, 0.35f * live), 1.5f);
        DrawCircle(Vector2.Zero, d.Reach, new Color(d.Tint, 0.05f * live));
        DrawCircle(Vector2.Zero, 8f, new Color(d.Tint, 0.9f * live));
        DrawCircle(Vector2.Zero, 3f, new Color(1f, 1f, 1f, live));
    }
}
