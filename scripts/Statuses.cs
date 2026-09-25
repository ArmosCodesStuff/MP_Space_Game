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
    Disabled = 2,        // knocked out: no moves, no turning, no firing (a boss under a shockwave; on a
                         // pilot: no helm, main guns, ability presses or warp -- its point defence,
                         // its wing and its turrets already out fight on)
    Untargetable = 4,    // nothing hostile may choose it (stealth)
    Hardened = 8,        // taking less: by the share its applier gave (StatusSet.Apply), else the row's
    Evading = 16,        // the next hits miss outright
}

// WHAT A STATUS DOES TO A BLOW, as a row. PlayerShip.Guarded walks this table. THE SHARE COMES
// FROM WHOEVER APPLIED THE STATUS (StatusSet.Apply's share: the warrior's rush gives its own
// rush_guard row, a taunt gives 0.67), and the row's Share is only the default for an applier
// that named none. It used to be a stat id read off the HIT hull's own sheet, so a hardening from
// a second class could only ever be the warrior's figure or the default.
// A NEW ROW: the status, and the share a blow is multiplied by when the applier named none.
public struct StatusGuard
{
    public Status Status;    // the status that must be on the thing for this row to apply
    public double Share;     // what a blow is multiplied by: 0 stops it, 1 lets it all through
}

public struct StatusSet
{
    public const float PinSpeed = 0.2f;        // a pinned ship: 20% of its top speed

    // IN ORDER, and the order is the point: evasion decides whether the blow happened at all
    // before anything else scales it. Half of it is what Hardened means when its applier named
    // no share of its own.
    public static readonly StatusGuard[] Guards =
    {
        new() { Status = Status.Evading,  Share = 0 },      // the dart's roll: it is not there to be hit
        new() { Status = Status.Hardened, Share = 0.5 },    // half of it, unless the applier says otherwise
    };

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
