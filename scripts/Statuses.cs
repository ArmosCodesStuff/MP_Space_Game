using System.Collections.Generic;

// WHAT IS BEING DONE TO A THING, right now: held by a web, knocked out by a shockwave, hidden,
// hardened, dodging. Each was a bool and a timer on whichever class needed it (a ship's `_pinT`,
// a utility craft's `PinT`), so a second one meant a second pair, and nothing could ask "is this
// disabled?" without knowing what it was.
//
// THE HOST DECIDES. A guest counts nothing down: it is sent the bits (PlayerShip's state) and
// shows them. A status with no time left is simply absent.
// THESE VALUES ARE THE WIRE FORMAT (StatusSet.Bits). Never renumber one that is here.
// A sixth status takes 32, and 32 is free: Shielded held it, was declared "a pool absorbs damage
// before the hull", and was never set, read or implemented by anything -- the only such pool is
// the freighter's bubble, which is Sl("bubble") and PlayerShip.ThroughBubbles. No packet ever
// carried it, so deleting it left 1..16 untouched and no gap below them.
[System.Flags]
public enum Status
{
    None = 0,
    Pinned = 1,          // a web: held to PinSpeed of top speed, thrusting, unable to turn
    Disabled = 2,        // knocked out: no moves, no turning, no firing (a boss under a shockwave)
    Untargetable = 4,    // nothing hostile may choose it (stealth)
    Hardened = 8,        // taking less: by StatusSet.Guards' share, not by one class's own stat
    Evading = 16,        // the next hits miss outright
}

// WHAT A STATUS DOES TO A BLOW, as a row. A status that scales damage names the stat id that
// says by how much on a hull that has such a row, and the share to use on a hull that has not.
// PlayerShip.Guarded walks this table; it used to read Stats["rush_guard"] -- a row only the
// HeavyWarrior carries -- out of a path every class goes through, so a second hardening class,
// a gear part or a boss debuff got a status that did nothing at all (the sheet answers 0 for an
// id it has not got, and the `> 0` beside it swallowed that).
// A NEW ROW: the status, the stat id that scales it ("" for none), and that share.
public struct StatusGuard
{
    public Status Status;    // the status that must be on the thing for this row to apply
    public string Stat;      // the stat id that scales it where the sheet has one; "" for none
    public double Share;     // what a blow is multiplied by otherwise: 0 stops it, 1 lets it all through
}

public struct StatusSet
{
    public const float PinSpeed = 0.2f;        // a pinned ship: 20% of its top speed

    // IN ORDER, and the order is the point: evasion decides whether the blow happened at all
    // before anything else scales it. Half of it is what Hardened has always meant, and a hull
    // with a row of its own (the HeavyWarrior's rush_guard) still says how much for itself.
    public static readonly StatusGuard[] Guards =
    {
        new() { Status = Status.Evading,  Stat = "",           Share = 0 },     // the dart's roll: it is not there to be hit
        new() { Status = Status.Hardened, Stat = "rush_guard", Share = 0.5 },   // the warrior's rush: half of it
    };

    private Dictionary<Status, double> _left;

    public bool Has(Status s) => _left != null && _left.TryGetValue(s, out double t) && t > 0;
    public double Left(Status s) => _left != null && _left.TryGetValue(s, out double t) ? t : 0;

    // the host applies: the longer of what it has and what it is given
    public void Apply(Status s, double seconds)
    {
        _left ??= new Dictionary<Status, double>();
        _left[s] = System.Math.Max(Left(s), seconds);
    }
    public void Clear(Status s) { if (_left != null) _left.Remove(s); }

    public void Tick(double delta)
    {
        if (_left == null || _left.Count == 0) return;
        foreach (var s in new List<Status>(_left.Keys))
        {
            double t = _left[s] - delta;
            if (t <= 0) _left.Remove(s); else _left[s] = t;
        }
    }

    // on the wire: one int, so a guest shows what the host resolved
    public int Bits
    {
        get
        {
            int b = 0;
            if (_left != null) foreach (var kv in _left) if (kv.Value > 0) b |= (int)kv.Key;
            return b;
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
