using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// A SHIP'S OWN RECENT PAST, a mark every so often (the Echo's Rewind, kits_v2's card + the README ruling: back
// 8 s, position, heading, velocity AND hull). New with slice 6d; it replaced nothing.
//
// A Trail is a ring of Marks on one clock: Note keeps one whenever `every` seconds have passed since the last,
// and Back answers the mark closest to `back` seconds ago -- the oldest there is, with less history than that.
// Each peer fills what it owns: the pilot's machine its flight (position, heading, velocity), the host the hull.
// A row that rewinds reads it by its stats (rewind_every, rewind_back): a sheet without them keeps no trail.
// ─────────────────────────────────────────────────────────────────────────────
public readonly record struct Mark(double T, Vector2 At, float Heading, Vector2 Velocity, double Hull);

public sealed class Trail
{
    private Mark[] _ring = System.Array.Empty<Mark>();
    private int _next, _count;

    // The marks to hold for `back` seconds at one every `every`, and a few beyond it so the one nearest the edge is kept.
    public static int Size(double back, double every) => every > 0 ? (int)System.Math.Ceiling(back / every) + 4 : 0;

    public void Note(double now, double every, double back, Mark m)
    {
        int size = Size(back, every);
        if (size <= 0) return;
        if (_ring.Length != size) { _ring = new Mark[size]; _next = 0; _count = 0; }
        if (_count > 0 && now - _ring[(_next - 1 + size) % size].T < every - 1e-9) return;
        _ring[_next] = m with { T = now };
        _next = (_next + 1) % size; _count = System.Math.Min(_count + 1, size);
    }

    // the mark whose time is closest to now - back; the oldest kept when the trail is shorter than that. Null: none.
    public Mark? Back(double now, double back)
    {
        if (_count == 0) return null;
        Mark best = default; double bestGap = double.MaxValue;
        for (int i = 0; i < _count; i++)
        {
            var m = _ring[i];
            double gap = System.Math.Abs(now - back - m.T);
            if (gap < bestGap) { best = m; bestGap = gap; }
        }
        return best;
    }

    public void Clear() { _next = 0; _count = 0; }
}
