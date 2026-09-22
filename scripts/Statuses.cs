using System.Collections.Generic;

// WHAT IS BEING DONE TO A THING, right now: held by a web, knocked out by a shockwave, hidden,
// hardened, dodging. Each was a bool and a timer on whichever class needed it (a ship's `_pinT`,
// a utility craft's `PinT`), so a second one meant a second pair, and nothing could ask "is this
// disabled?" without knowing what it was.
//
// THE HOST DECIDES. A guest counts nothing down: it is sent the bits (PlayerShip's state) and
// shows them. A status with no time left is simply absent.
[System.Flags]
public enum Status
{
    None = 0,
    Pinned = 1,          // a web: held to PinSpeed of top speed, thrusting, unable to turn
    Disabled = 2,        // knocked out: no moves, no turning, no firing (a boss under a shockwave)
    Untargetable = 4,    // nothing hostile may choose it (stealth)
    Hardened = 8,        // taking less: the share is the guard's, not the status's
    Evading = 16,        // the next hits miss outright
    Shielded = 32,       // a pool absorbs damage before the hull
}

public struct StatusSet
{
    public const float PinSpeed = 0.2f;        // a pinned ship: 20% of its top speed

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
