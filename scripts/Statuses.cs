using System.Collections.Generic;

// WHAT IS BEING DONE TO A THING, right now: held by a web, knocked out by a shockwave, hidden,
// hardened, parrying. Each was a bool and a timer on whichever class needed it (a ship's `_pinT`,
// a utility craft's `PinT`), so a second one meant a second pair, and nothing could ask "is this
// disabled?" without knowing what it was.
//
// THE HOST DECIDES. A guest counts nothing down: it is sent the bits (PlayerShip's state) and
// shows them. A status with no time left is simply absent.
// THESE VALUES ARE THE WIRE FORMAT (StatusSet.Bits). Never renumber one that is here.
// 16 is unassigned. 32 is Parrying (F11): every peer draws a prism stance from it. (Shielded held 32 once and was never
// set, read or implemented by anything; the only such pool is the freighter's bubble.)
// 64 and up are HOST-ONLY (StatusSet.HostOnly): the host resolves them and no packet carries them.
[System.Flags]
public enum Status
{
    None = 0,
    Pinned = 1,          // a web: held to PinSpeed of top speed, thrusting, unable to turn
    Disabled = 2,        // knocked out: no moves, no turning, no firing (a boss under a shockwave; on a
                         // pilot: no helm, main guns, ability presses or warp -- its point defence,
                         // its wing and its turrets already out fight on)
    Untargetable = 4,    // nothing hostile may choose it (stealth)
    Hardened = 8,        // taking less: by the share its applier gave (StatusSet.Apply), else the row's
    Parrying = 32,       // a prism stance is up: light caught off its guard is split, rounds reflected (Prism.cs; on the wire)
    Suppressed = 64,     // its guns weakened, its throws held (StatusSet.OutGuards; host-only)
    Dazzled = 128,       // blinded: its throws held (StatusSet.OutGuards; host-only; never on a boss)
    Jammed = 256,        // its guns silenced, its throws held (StatusSet.OutGuards; host-only; never on a boss)
    Unwebbed = 512,      // no web can take it: a pin asked of it is refused (the whirlwind; PlayerShip.ApplyStatus; host-only)
}

// WHICH OF A HOSTILE'S BLOWS the outgoing door is scaling (StatusSet.Out): a gun of a craft or a
// structure, a boss's move, or a boss's super (the moves its skinny bar counts down to).
public enum OutKind { Gun, Move, Super }

// WHAT A STATUS DOES TO WHAT ITS HOLDER DEALS, as a row: the "weaken what shoots" statuses are
// one table read by one door (StatusSet.Out), never a gate in each weapon. A NEW
// ROW: the status, what it multiplies each kind of blow by (1 lets it all through, 0 stops it),
// whether it holds a launcher's throw (the clock keeps its zero; it throws at the lapse), and
// the kinds of thing it never reaches at all (Spares: a boss is immune to Dazzled and Jammed, so
// they are never on it and its Move and Super are 1).
public struct OutGuard
{
    public Status Status;
    public double Gun, Move, Super;
    public bool HoldsThrow;
    public bool BlocksLatch;      // a raider carrying it takes no NEW latch (kits_v2 F17: Dazzled, Jammed)
    public bool DropsLatch;       // ...and lets go of the one it holds (Jammed)
    public bool HoldsAim;         // its turret stops tracking its target (Jammed: the Echo's EMP)
    public Tag Spares;
}

// WHAT A STATUS DOES TO A BLOW, as a row. PlayerShip.Guarded walks this table. THE SHARE COMES
// FROM WHOEVER APPLIED THE STATUS (StatusSet.Apply's share: a taunt gives its own taunt_guard
// row, 0.67), and the row's Share is only the default for an applier
// that named none, so a hardening from any class carries that class's own figure.
// A NEW ROW: the status, and the share a blow is multiplied by when the applier named none.
public struct StatusGuard
{
    public Status Status;    // the status that must be on the thing for this row to apply
    public double Share;     // what a blow is multiplied by: 0 stops it, 1 lets it all through
}

public struct StatusSet
{
    public const float PinSpeed = 0.2f;        // a pinned ship: 20% of its top speed

    // IN ORDER, each row's share multiplied in the order written; a share of 0 stops the blow there.
    // Half of it is what Hardened means when its applier named no share of its own.
    public static readonly StatusGuard[] Guards =
    {
        new() { Status = Status.Hardened, Share = 0.5 },    // half of it, unless the applier says otherwise
    };

    // THE OUTGOING DOOR'S ROWS (kits_v2 §5, F17): every hostile blow -- a raider's laser, a boss's
    // move, a structure's gun -- is multiplied by the row of each status on the thing that deals
    // it, and a launcher's throw waits while any row that holds it is on.
    public static readonly OutGuard[] OutGuards =
    {
        new() { Status = Status.Suppressed, Gun = 0.5, Move = 0.7, Super = 1.0, HoldsThrow = true },
        new() { Status = Status.Dazzled,    Gun = 1.0, Move = 1.0, Super = 1.0, HoldsThrow = true, BlocksLatch = true, Spares = Tag.Boss },
        new() { Status = Status.Jammed,     Gun = 0.0, Move = 1.0, Super = 1.0, HoldsThrow = true, BlocksLatch = true, DropsLatch = true, HoldsAim = true, Spares = Tag.Boss },
    };
    // never on the wire: the host resolves them, and a guest has nothing that reads them
    public const Status HostOnly = Status.Suppressed | Status.Dazzled | Status.Jammed | Status.Unwebbed;

    // WHETHER A STATUS CAN BE PUT ON A THING OF THESE TAGS at all: no OutGuards row spares it
    public static bool Reaches(Status s, Tag on)
    {
        foreach (var g in OutGuards) if (g.Status == s && (g.Spares & on) != 0) return false;
        return true;
    }

    // THE OUTGOING DOOR: what a blow of this kind comes to, from a thing carrying these statuses
    public double Out(double d, OutKind kind)
    {
        if (_left == null) return d;
        foreach (var g in OutGuards)
            if (Has(g.Status)) d *= kind switch { OutKind.Gun => g.Gun, OutKind.Move => g.Move, _ => g.Super };
        return d;
    }
    // ...and whether a launcher carrying them must hold its throw
    public bool HoldsThrow => Any(g => g.HoldsThrow);
    // ...and whether its turret must stop tracking
    public bool HoldsAim => Any(g => g.HoldsAim);

    // ...and whether a raider carrying them may take a new latch, or keep the one it has
    public bool BlocksLatch => Any(g => g.BlocksLatch);
    public bool DropsLatch => Any(g => g.DropsLatch);
    private bool Any(System.Func<OutGuard, bool> row)
    {
        if (_left == null) return false;
        foreach (var g in OutGuards) if (row(g) && Has(g.Status)) return true;
        return false;
    }

    private Dictionary<Status, double> _left;
    private Dictionary<Status, double> _share;       // the share an applier named, while it lasts (host only)

    public bool Has(Status s) => _left != null && _left.TryGetValue(s, out double t) && t > 0;
    public double Left(Status s) => _left != null && _left.TryGetValue(s, out double t) ? t : 0;

    // the host applies: the longer of what it has and what it is given. A SHARE (NaN: none) is
    // what a blow is multiplied by while it lasts; two at once keep the stronger, the lower.
    public void Apply(Status s, double seconds, double share = double.NaN)
    {
        _left ??= new Dictionary<Status, double>();
        _left[s] = System.Math.Max(Left(s), seconds);
        if (double.IsNaN(share)) return;
        _share ??= new Dictionary<Status, double>();
        _share[s] = _share.TryGetValue(s, out double had) ? System.Math.Min(had, share) : share;
    }
    // the share its applier named, or the row's default when none did
    public double ShareOf(Status s, double fallback) =>
        _share != null && _share.TryGetValue(s, out double v) ? v : fallback;
    public void Clear(Status s) { _left?.Remove(s); _share?.Remove(s); }

    public void Tick(double delta)
    {
        if (_left == null || _left.Count == 0) return;
        foreach (var s in new List<Status>(_left.Keys))
        {
            double t = _left[s] - delta;
            if (t <= 0) { _left.Remove(s); _share?.Remove(s); } else _left[s] = t;
        }
    }

    // on the wire: one int, so a guest shows what the host resolved -- less what is host-only
    public int Bits
    {
        get
        {
            int b = 0;
            if (_left != null) foreach (var kv in _left) if (kv.Value > 0) b |= (int)kv.Key;
            return b & ~(int)HostOnly;
        }
    }
    // a guest takes the host's word: held for a packet's worth of time, replaced by the next
    public void FromBits(int bits, double hold = 0.25)
    {
        _left?.Clear();
        foreach (Status s in System.Enum.GetValues(typeof(Status)))
            if (s != Status.None && (bits & (int)s) != 0) Apply(s, hold);
    }
}
