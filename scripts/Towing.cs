using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// TOW AND HURL (F19) — a craft held off a ship's bow, then thrown down it.
//
// REPLACED: nothing (new). The Destroyer's Grapnel (6a) tows a craft it hooks: this file is the host
// state that owns the craft's position while it lasts, checked before the craft's own AI (Raider.cs,
// beside Disabled), so a towed craft is off its post and cannot web or fire.
//
// A ROW OF Towing.All MUST FILL IN:
//   Id        what it is called -- and the DealtBy weapon id its hurl is credited under
//   Offset    how far off the tower's bow (from the hull's edge) it is held
//   Reel      seconds to reel it there from where it was hooked
//   Haul      seconds it is held before it is hurled on its own
//   Speed     the hurl's speed down the tower's bow
//   Reach     how far the hurl flies when it strikes nothing
//   Impact    what the first hostile it strikes AND the thrown craft each take (Towing.Impact:
//             x2 when the thrown craft is a heavy)
// A thing is towed through ITowable, never by its type; what may be towed at all is
// Targeting.Throwable, and what a hook swings round instead is Targeting.Immovable.
//
// AUTHORITY: host only. Guests see the craft where the host's raider packet puts it.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class TowRow
{
    public string Id;
    public float Offset;
    public double Reel, Haul;
    public float Speed, Reach;
    public double Impact;
}

public interface ITowable
{
    TowState Towed { get; set; }
    bool Heavy { get; }
}

// ONE TOW, from the hook to the end of its hurl.
public sealed class TowState
{
    public TowRow Row;
    public Node2D By;              // the tower: its bow is where the craft is held and where it is thrown
    public ITurretHost Credit;     // who the hurl's blows are credited to
    public double T;               // seconds since the hook
    public Vector2 Hooked;         // where the craft was when it was hooked
    public bool Flung;
    public Vector2 Dir;            // the hurl's heading, fixed as it is thrown
    public float Flown;
}

public static class Towing
{
    public const int Grapnel = 0;
    public static readonly TowRow[] All =
    {
        // THE DESTROYER'S GRAPNEL (kits_v2's card, kits_v3 3.2 TOW / HURL)
        new() { Id = "hurl", Offset = 160f, Reel = 0.5, Haul = 3.0, Speed = 600f, Reach = 900f, Impact = 60 },
    };
    public static TowRow Of(int row) => All[row >= 0 && row < All.Length ? row : Grapnel];

    // ── the pure rules ──────────────────────────────────────────────────────────
    public static double Impact(TowRow row, bool heavy) => row.Impact * (heavy ? 2 : 1);
    // where a towed craft is held: `offset` off the tower's bow, from the hull's edge
    public static Vector2 Bow(Node2D by, float offset)
    {
        var fwd = Vector2.Up.Rotated(by.Rotation);
        return by.Position + fwd * (Raider.Extent(by, fwd) + offset);
    }

    // ── the host ────────────────────────────────────────────────────────────────
    public static bool Tow(ITowable t, Node2D by, ITurretHost credit, int row = Grapnel)
    {
        if (!Net.Sim || t is not Node2D n || !Targeting.Throwable.Hits(t) || Targeting.Immovable.Hits(t) || !GodotObject.IsInstanceValid(by)) return false;
        t.Towed = new TowState { Row = Of(row), By = by, Credit = credit, Hooked = n.Position };
        return true;
    }
    // throw it now, down the tower's bow (it is thrown on its own at the row's Haul)
    public static void Hurl(ITowable t)
    {
        if (t.Towed is not { Flung: false } s || !GodotObject.IsInstanceValid(s.By)) return;
        s.Flung = true; s.Dir = Vector2.Up.Rotated(s.By.Rotation);
    }

    // One frame of it. False when it is over (the caller clears Towed and its AI takes over again).
    public static bool Step(ITowable t, Node2D n, double delta)
    {
        var s = t.Towed;
        if (s == null) return false;
        s.T += delta;
        if (!s.Flung)
        {
            if (!GodotObject.IsInstanceValid(s.By) || s.By is IHittable { Alive: false }) return false;
            var hold = Bow(s.By, s.Row.Offset);
            n.Position = s.T < s.Row.Reel ? s.Hooked.Lerp(hold, (float)(s.T / s.Row.Reel)) : hold;
            if (s.T >= s.Row.Haul) Hurl(t);
            return true;
        }
        // THE HURL: swept, and the first hostile body it touches ends it
        float step = Mathf.Min(s.Row.Speed * (float)delta, s.Row.Reach - s.Flown);
        var from = n.Position; var to = from + s.Dir * step;
        IHittable struck = null; Vector2 at = to;
        float pad = (t as IHittable)?.HitRadius ?? 0f;
        Shots.Sweep(from, to, 6f, p =>
        {
            foreach (var h in Combat.Hostiles)
                if (!ReferenceEquals(h, t) && Targeting.Attackable.Hits(h) && h.Covers(p, pad)) { struck = h; at = p; return true; }
            return false;
        });
        n.Position = at; s.Flown += from.DistanceTo(at);
        if (struck != null)
        {
            double d = Impact(s.Row, t.Heavy);
            Dealt.Deal(struck, d, s.Credit, s.Row.Id);
            if (t is IHittable self) Dealt.Deal(self, d, s.Credit, s.Row.Id);
            return false;
        }
        return s.Flown < s.Row.Reach - 0.01f;
    }
}
