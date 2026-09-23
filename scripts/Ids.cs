using System.Collections.Generic;

// NET IDS. Every peer names the same thing by the same number, so a guest can say "attack 1042" and
// the host knows what it means. One SPACE per kind, far enough apart that a kind can grow without
// running into the next, and one counter per space so nothing has to know what else exists. The
// six bases these replace were written into six files, each with its own counter and its own reset.
//
// Numbers are the HOST's to hand out: it sends them with whatever it spawns. A space's counter is
// cleared with the world (Combat.Clear), or a second session in one process carries the first's.
public static class NetIds
{
    public const int Dummy = 1000, Player = 2000, Boss = 3000, Enemy = 5000, Missile = 10000, Deployed = 20000, Menu = 30000, Emplacement = 40000;

    // HOW WIDE EACH SPACE IS, beside its base. The counter had no ceiling and a seat no bound, so
    // the 5000th enemy of a session was 10000 -- a MISSILE's number -- and all five kinds share one
    // Combat.Hostiles, where a lookup returns the first id that matches. Both ways in stay inside
    // the space now: they wrap round rather than walk into the next kind's numbers.
    private static readonly Dictionary<int, int> Widths = new()
    {
        [Dummy] = 1000, [Player] = 1000, [Boss] = 2000, [Enemy] = 5000,
        [Missile] = 10000, [Deployed] = 10000, [Menu] = 10000, [Emplacement] = 1000,
    };
    private static int Wide(int space) => Widths.TryGetValue(space, out var w) && w > 1 ? w : 1000;

    private static readonly Dictionary<int, int> Handed = new();

    // the next id in a space: Next(NetIds.Enemy) -> 5001, 5002, ... and round to 5001 again at the
    // space's edge, never to 0, which is In(space, 0) -- the space's first seat
    public static int Next(int space)
    {
        Handed.TryGetValue(space, out int n);
        Handed[space] = n = n % (Wide(space) - 1) + 1;
        return space + n;
    }

    // A thing whose place says which one it is (a pilot's seat, a dummy's number), FOLDED into the
    // space: an index is not always a small number -- PlayerShip hands this a random 31-bit peer
    // id, which walked into other kinds' numbers and overflowed int past 2147481647.
    public static int In(int space, int index)
    {
        int w = Wide(space);
        return space + (index % w + w) % w;
    }

    public static void Reset() => Handed.Clear();
}
