using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// DECOYS (F14) — burning points a hostile's missiles turn onto and its craft are blinded by.
//
// REPLACED: nothing; v1's drafted decoy BODY (a hull the raiders shot at) was dropped for points.
// A salvo is a row of Spawns.All ("Decoy"): one spawn per salvo, and every peer derives its points
// from the seed (where, the row and its count, the thrower's rotation), so six flares cost one message.
//
// A ROW OF Decoys.All MUST FILL IN:
//   Id         what it is called
//   Count      how many points a salvo throws, evenly round, the first dead astern...
//   CountStat  ...unless the thrower's sheet has this row: then its number (a count rider lifts it)
//   Ring       how far out they stop
//   Coast      seconds to coast out to the ring
//   Life       seconds from the pop to the last of it (it lures the whole time)
//   Lure       a Decoyable guided shot (ShotDef.Decoyable) within this of a point turns onto the nearest
//   Catch      ...and bursts, harmlessly, once within this of it
//   Mark       a Decoyable predicted missile's mark (MissileSide.Decoyable) within this of a point...
//   MarkLeft   ...with at least this much flight left moves onto the nearest
//   Dazzle     a craft whose centre is within this of a point is DAZZLED (Status.Dazzled)...
//   DazzleFor  ...until this long after it leaves
// Every rule reads where the points come to rest, from the pop. Every turn is sticky: a shot or a
// mark moved once stays on its point, and a point that burns out first still takes it. Friendly fire
// is never Decoyable.
//
// AUTHORITY: the host pops a salvo (Hub.Flares), decides every lure (Decoys.Tick) and tells every
// peer through one RPC (Hub.NetDecoy); the dazzle is host-only (Statuses.HostOnly).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class DecoyDef
{
    public string Id;
    public int Count;
    public string CountStat;
    public float Ring;
    public double Coast, Life;
    public float Lure, Catch, Mark;
    public double MarkLeft;
    public float Dazzle;
    public double DazzleFor;
}

public static class Decoys
{
    // The index IS the id on the wire (the spawn seed's N), so APPEND ONLY.
    public const int Flares = 0;
    // THE SEED'S N CARRIES THE ROW AND THE COUNT: row + CountSpan x count (count 0: the row's own Count), so
    // a thrower's own count reaches every peer in the one spawn message.
    public const int CountSpan = 64;
    public static int Pack(int row, int count) => row + CountSpan * Math.Max(0, count);
    public static (int row, int count) Unpack(int n) => (n % CountSpan, n / CountSpan);

    public static readonly DecoyDef[] All =
    {
        // THE SNIPER'S FLARES (kits_v2's card): 6 in a ring 180 u out, 0.6 s to coast out, 5 s alight
        new() { Id = "flares", Count = 6, CountStat = "flare_count", Ring = 180f, Coast = 0.6, Life = 5.0, Lure = 500f, Catch = 30f,
                Mark = 300f, MarkLeft = 1.0, Dazzle = 150f, DazzleFor = 4.0 },
    };
    public static DecoyDef Of(int row) => All[row >= 0 && row < All.Length ? row : Flares];

    // ── the pure rules ──────────────────────────────────────────────────────────
    // Where a salvo's `count` points (0: the row's Count) are `age` seconds after the pop from `at`, the
    // thrower's rotation `rot`: evenly round, the first dead astern, coasting out to the ring.
    public static Vector2[] Points(DecoyDef d, Vector2 at, float rot, double age, int count = 0)
    {
        int n = count > 0 ? count : d.Count;
        float out_ = d.Ring * (float)Math.Clamp(d.Coast > 0 ? age / d.Coast : 1, 0, 1);
        var p = new Vector2[n];
        for (int i = 0; i < n; i++) p[i] = at + Vector2.Down.Rotated(rot + i * Mathf.Tau / n) * out_;
        return p;
    }
    // The nearest of `points` within `reach` of `at`, or none.
    public static Vector2? Nearest(IEnumerable<Vector2> points, Vector2 at, float reach)
    {
        Vector2? best = null; float bd = reach;
        foreach (var p in points) { float d = p.DistanceTo(at); if (d <= bd) { bd = d; best = p; } }
        return best;
    }

    // ── the host, every frame ───────────────────────────────────────────────────
    public static void Tick(Hub hub)
    {
        if (!Net.IsHost) return;
        foreach (var s in hub.Salvos.ToList()) if (s.Age >= s.Def.Life) hub.Down(Spawns.Decoy, s, burst: false, float.NaN);
        if (hub.Salvos.Count == 0) return;
        foreach (var s in hub.Salvos)
        {
            var d = s.Def; var pts = s.Resting;
            foreach (var h in Combat.Hostiles.ToList())
                if (h is Shot sh && sh.Alive && sh.Decoyable && sh.DecoyPoint == null && Nearest(pts, sh.GlobalPosition, d.Lure) is { } p)
                    hub.Decoy(sh.NetId, p, d.Catch);
            foreach (var (id, side, at, left) in hub.Blasts.ToList())
                if (Missiles.Of(side).Decoyable && left >= d.MarkLeft && Nearest(pts, at, d.Mark) is { } p && at.DistanceTo(p) > 0.5f)
                    hub.Decoy(id, p, d.Catch);
            foreach (var r in hub.Raiders)
                if (r.Alive && Nearest(pts, r.Position, d.Dazzle) != null) r.ApplyStatus(Status.Dazzled, d.DazzleFor);
        }
    }
}

// ONE SALVO IN THE WORLD, drawn on every peer from its seed.
public partial class DecoySalvo : Node2D
{
    public int NetId;
    public int Row;
    public int Count;          // its points (0: the row's Count): the thrower's CountStat at the pop
    public float Rot;          // the thrower's rotation at the pop
    public Vector2 At;         // where it was popped
    public double Age;         // seconds since the pop (a joiner is told how old it is)
    public DecoyDef Def => Decoys.Of(Row);
    public Vector2[] Points => Decoys.Points(Def, At, Rot, Age, Count);
    // where they burn: every rule (the lure, the mark, the dazzle) reads these, from the pop
    public Vector2[] Resting => Decoys.Points(Def, At, Rot, Def.Coast, Count);

    public override void _Ready() { Position = Vector2.Zero; ZIndex = 6; }
    public override void _Process(double delta) { Age += delta; QueueRedraw(); }
    public override void _Draw()
    {
        float fade = (float)Math.Clamp((Def.Life - Age) / 0.5, 0, 1);
        foreach (var p in Points)
        {
            DrawCircle(p, 11f, new Color(1f, 0.55f, 0.15f, 0.25f * fade));
            DrawCircle(p, 4.5f, new Color(1f, 0.85f, 0.45f, fade));
        }
    }
}
