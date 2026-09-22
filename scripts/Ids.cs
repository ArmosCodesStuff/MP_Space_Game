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
    public const int Dummy = 1000, Player = 2000, Boss = 3000, Enemy = 5000, Missile = 10000, Deployed = 20000, Menu = 30000;

    private static readonly Dictionary<int, int> Handed = new();

    // the next id in a space: Next(NetIds.Enemy) -> 5001, 5002, ...
    public static int Next(int space)
    {
        Handed.TryGetValue(space, out int n);
        Handed[space] = ++n;
        return space + n;
    }

    // a thing whose place says which one it is (a pilot's seat, a dummy's number)
    public static int In(int space, int index) => space + index;

    public static void Reset() => Handed.Clear();
}
