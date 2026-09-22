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
    public static IHittable PlayerById(int id) => Live(Players, id);

    // Interceptable missiles get ids the host hands out and sends with the launch.
    private static int _nextMissile = 10000;
    public static int NextMissileId() => ++_nextMissile;

    public static IHittable ById(int id) => Live(Hostiles, id);
    private static IHittable Live(List<IHittable> l, int id)
    {
        foreach (var h in l) if (h != null && h.Alive && h.NetId == id) return h;
        return null;
    }

    // THE NEAREST, one way for everyone: of `items`, the one nearest `at` within `within` that
    // `ok` accepts, or null. There were ten of these -- the boss choosing a pilot, a raider its
    // prey, the base's guns, Tab, a click, the radar -- several running every frame, and most
    // built and sorted a whole list to take its first. No allocation here, and a tie goes to the
    // first in order, as a stable sort's first did.
    public static T Nearest<T>(IEnumerable<T> items, Vector2 at, System.Func<T, Vector2> pos, float within = float.MaxValue,
                               System.Func<T, bool> ok = null) where T : class
    {
        T best = null; float bd = 0f;
        foreach (var x in items)
        {
            if (x == null || (ok != null && !ok(x))) continue;
            float d = at.DistanceTo(pos(x));
            if (d <= within && (best == null || d < bd)) { best = x; bd = d; }
        }
        return best;
    }
    // What a player may pick with Tab or a click: alive, and selectable -- never a missile.
    public static bool Pickable(IHittable h) => h.Alive && h.Selectable;

    // A long hull's hit shape: a capsule down its keel (a circle would be far too wide), `length`
    // nose to tail and `halfWidth` across, around a node pointing up its local Y.
    public static bool KeelCovers(Node2D n, float length, float halfWidth, Vector2 p, float pad)
    {
        var fwd = Vector2.Up.Rotated(n.Rotation);
        float half = Mathf.Max(0f, length * 0.5f - halfWidth);
        float t = Mathf.Clamp((p - n.Position).Dot(fwd), -half, half);
        return p.DistanceTo(n.Position + fwd * t) <= halfWidth + pad;
    }

    // Set by the live world so combat can draw without knowing what world it is in.
    // A laser shot's flash, and its sound. The KIND is what fired it, not a boss/not-boss
    // boolean: a carrier's fighters want their own report, quieter than a capital ship's, and
    // "make the fighters quieter" has to be answerable somewhere other than at every call site.
    public static System.Action<Vector2, Vector2, Color, ShotSound> OnFlash;
    public static void Flash(Vector2 a, Vector2 b, Color c, ShotSound snd = ShotSound.Light)
        => OnFlash?.Invoke(a, b, c, snd);

    // THE WORLD PROJECTILES ARE BORN INTO: the hub, or the title screen's diorama. Every shell and
    // torpedo is spawned HERE, into whatever world set itself -- one spawner, where each world
    // carried its own copy of the job (the shell three times, the torpedo twice). The host's copy
    // is the one that deals damage; a world with guests to tell hooks ShellFired / TorpedoFired
    // and sends the launch on, and guests fly a cosmetic copy.
    public static Node World;
    public static System.Action<Shell> ShellFired;
    public static System.Action<Torpedo> TorpedoFired;

    // a main-gun shell (battleship, destroyer): straight, and it hits the first hostile it touches
    public static void FireShell(Vector2 from, Vector2 dir, float speed, float range, double damage, PlayerShip source)
    {
        if (World == null) return;
        var s = new Shell { Position = from, Dir = dir.Normalized(), Speed = speed, Range = range, Damage = damage, Source = source };
        World.AddChild(s);
        ShellFired?.Invoke(s);
    }
    // Unguided torpedoes pass targetId 0; a missile passes its target and a small turn rate, and
    // heavy for its looks. hostile = fired BY an enemy, so it seeks and hits player ships -- and it
    // gets an id, so point defence can shoot it down on every peer at once. source: the player
    // ship that fired it (host only), credited with combat on a hit.
    public static void LaunchTorpedo(Vector2 from, Vector2 dir, float speed, float range, double damage,
                                     int targetId = 0, float turnRate = 0f, bool heavy = false, bool hostile = false,
                                     PlayerShip source = null, string hitSource = null, float size = 1f)
    {
        if (World == null) return;
        var t = new Torpedo { Position = from, Dir = dir.Normalized(), Speed = speed, Range = range, Damage = damage, TargetId = targetId,
                              TurnRate = turnRate, Heavy = heavy, HostileFire = hostile, Source = source, HitSource = hitSource, Size = size,
                              NetId = hostile ? NextMissileId() : 0 };
        World.AddChild(t);
        TorpedoFired?.Invoke(t);
    }

    // Dropped by the world on its way out. EVERY hook set by that world must go: each one is a
    // lambda holding the Hub, so one left behind is a freed node the next shot calls into, and a
    // Hub that can never be collected once you are back at the menu.
    public static void Clear() { Hostiles.Clear(); Players.Clear(); OnFlash = null; World = null; ShellFired = null; TorpedoFired = null; }
}
