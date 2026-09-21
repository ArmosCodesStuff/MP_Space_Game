using Godot;
using System.Collections.Generic;

// Combat services. Everything here is HOST-SIDE: target lookup, damage, and the
// hit flashes clients are told to draw. A client asking "who is nearest" and
// acting on it would be inventing damage, which is exactly what Net forbids.
// (Target SELECTION is the exception: it is a local UI choice, and a guest sends
// the chosen NetId to the host as a request.)
public static class Combat
{
    // Registered by whatever world is live (hub, hostile system, someone else's world).
    public static readonly List<IHittable> Hostiles = new();

    // Player ships, as targets for ENEMY fire only (a hostile dummy's missile). Ships
    // register themselves; an escape pod is in no list, so nothing can touch it.
    public static readonly List<IHittable> Players = new();
    public static IHittable PlayerById(int id)
    {
        foreach (var p in Players) if (p != null && p.Alive && p.NetId == id) return p;
        return null;
    }

    // Interceptable missiles get ids the host hands out and sends with the launch.
    private static int _nextMissile = 10000;
    public static int NextMissileId() => ++_nextMissile;

    public static IHittable ById(int id)
    {
        foreach (var h in Hostiles) if (h != null && h.Alive && h.NetId == id) return h;
        return null;
    }

    // Live hostiles within `within` of a point, nearest first. Tab cycles through this.
    public static List<IHittable> Near(Vector2 p, float within)
    {
        var l = new List<IHittable>();
        foreach (var h in Hostiles) if (h != null && h.Alive && p.DistanceTo(h.Position) <= within) l.Add(h);
        l.Sort((a, b) => p.DistanceTo(a.Position).CompareTo(p.DistanceTo(b.Position)));
        return l;
    }

    // An aimed shot: the first hostile whose hit circle the ray crosses, within range.
    // `end` is where the shot stops -- the hit point, or the end of its reach.
    public static IHittable RayHit(Vector2 from, Vector2 dir, float range, out Vector2 end)
    {
        dir = dir.Normalized();
        IHittable best = null; float bestT = range;
        foreach (var h in Hostiles)
        {
            if (h == null || !h.Alive) continue;
            var to = h.Position - from;
            float t = to.Dot(dir);                              // along the ray
            if (t < 0 || t > range + h.HitRadius) continue;
            float miss = (to - dir * t).Length();               // off the ray
            if (miss > h.HitRadius) continue;
            float entry = Mathf.Max(0f, t - Mathf.Sqrt(h.HitRadius * h.HitRadius - miss * miss));
            if (entry < bestT) { bestT = entry; best = h; }
        }
        end = from + dir * (best != null ? bestT : range);
        return best;
    }

    // Set by the live world so combat can draw without knowing what world it is in.
    // A laser shot's flash, and its sound. The KIND is what fired it, not a boss/not-boss
    // boolean: a carrier's fighters want their own report, quieter than a capital ship's, and
    // "make the fighters quieter" has to be answerable somewhere other than at every call site.
    public static System.Action<Vector2, Vector2, Color, ShotSound> OnFlash;
    public static void Flash(Vector2 a, Vector2 b, Color c, ShotSound snd = ShotSound.Light)
        => OnFlash?.Invoke(a, b, c, snd);

    // Set by the live world: launches a projectile there (and, on a host, tells
    // guests). Unguided torpedoes pass targetId 0; the missile passes its target and
    // a small turn rate, and heavy for its looks.
    // hostile = fired BY an enemy, so it seeks and hits player ships, not hostiles.
    // source: the player ship that fired it (host only), credited with combat on a hit.
    public static System.Action<Vector2, Vector2, float, float, double, int, float, bool, bool, PlayerShip, string, float> OnTorpedo;
    // a battleship's shell (the host's does the damage; guests fly a cosmetic copy)
    public static System.Action<Vector2, Vector2, float, float, double, PlayerShip> OnShell;
    public static void FireShell(Vector2 from, Vector2 dir, float speed, float range, double damage, PlayerShip source)
        => OnShell?.Invoke(from, dir.Normalized(), speed, range, damage, source);
    public static void LaunchTorpedo(Vector2 from, Vector2 dir, float speed, float range, double damage,
                                     int targetId = 0, float turnRate = 0f, bool heavy = false, bool hostile = false,
                                     PlayerShip source = null, string hitSource = null, float size = 1f)
        => OnTorpedo?.Invoke(from, dir.Normalized(), speed, range, damage, targetId, turnRate, heavy, hostile, source, hitSource, size);

    // Dropped by the world on its way out. EVERY hook set by that world must go: each one is a
    // lambda holding the Hub, so one left behind is a freed node the next shot calls into, and a
    // Hub that can never be collected once you are back at the menu. OnShell was missed here.
    public static void Clear() { Hostiles.Clear(); Players.Clear(); OnFlash = null; OnShell = null; OnTorpedo = null; }
}
