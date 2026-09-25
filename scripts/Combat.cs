using Godot;
using System.Collections.Generic;

// WHAT DEALT IT -- the name a blow carries into PlayerShip.Incoming, which keys the 0.52 s
// per-source gap and the damage tally on it. They were free-form strings spelled by hand at the
// call sites, and two of them are ONE CHARACTER apart: "boss:gun" is the Drake Bastion's main gun
// and "boss:guns" is the odd-level boss's lasers (Lancer.cs; a player reads it as RUSTY BUCKET). A typo makes one weapon silently suppress
// another's hits, or stop suppressing its own, and nothing in the build could catch it. A name
// used by a file that owns a weapon is a member here or it does not exist.
//
// These are the FAMILY only -- what the weapon is. Combat.Fire adds the firing body's own identity
// to whatever name it is given (Shots.SourceKey), so a volley of any size lands every hit; a blow
// that does not fly (a beam's tick, a ram, a shockwave) passes the family straight to Hit and
// keeps the single shared name the gap is for.
public static class DamageSource
{
    // the odd-level boss (Lancer.cs; shown as RUSTY BUCKET -- these names are the frozen id's,
    // and the runtime keys beside them are on every save and every wire, so none of them move)
    public const string LancerGuns = "boss:guns";
    public const string LancerMissiles = "boss:missiles";
    public const string LancerBeam = "boss:beam";
    public const string LancerCharge = "boss:charge";
    public const string LancerWave = "boss:wave";
    // the Drake Bastion (Drake.cs)
    public const string DrakeGun = "boss:gun";
    public const string DrakeScrap = "boss:scrap";
    // what an emplacement answers with, one name per row of Emplacements.All that answers at all
    public const string BaseMissile = "base:missile";
}

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

    // Interceptable missiles get ids the host hands out and sends with the launch (NetIds).
    public static int NextMissileId() => NetIds.Next(NetIds.Missile);

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
    // What a player may pick with Tab or a click: alive, and selectable -- never a missile point
    // defence alone may have (Tag.Missile); a body in flight with a hull of its own (Tag.Hulled) is.
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

    // How far `p` lies off the segment a-b: a beam's reach, a lane's width, a sweep's step.
    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    // Set by the live world so combat can draw without knowing what world it is in.
    // A BEAM'S FLASH, and its sound. `beam` is a row of Beam.All -- what fired it -- and the row
    // says how the line is drawn and which note it is heard at, so a new kind of beam, or a new
    // note for an old one, is answered in that table and never at a call site. No default: a
    // caller that names no row is a caller that has not said what it fires.
    public static System.Action<Vector2, Vector2, int> OnFlash;
    public static void Flash(Vector2 a, Vector2 b, int beam) => OnFlash?.Invoke(a, b, beam);

    // THE WORLD PROJECTILES ARE BORN INTO: the hub, or the title screen's diorama. Every shell and
    // torpedo is spawned HERE, into whatever world set itself -- one spawner, where each world
    // carried its own copy of the job (the shell three times, the torpedo twice). The host's copy
    // is the one that deals damage; a world with guests to tell hooks ShellFired / TorpedoFired
    // and sends the launch on, and guests fly a cosmetic copy.
    public static Node World;
    // One hook for everything that flies: the world sends the launch on to its guests, which fly a
    // cosmetic copy of their own (the run is deterministic, so it lands where the host's lands).
    public static System.Action<Shot> ShotFired;

    // THE ONE DOOR EVERYTHING THAT FLIES COMES THROUGH. `kind` is a row of Shots.All -- what it
    // hits, how it ends, what it looks like; everything else is the weapon's own numbers, down to
    // `hull`: its body's own hull, for a row whose body is a target (Tag.Hulled) -- 0 falls to the
    // first blow. The host's copy deals the damage and the world tells its guests to fly a
    // cosmetic one; the hull is the host's alone, since a guest's copy damages nothing and is hurt
    // by nothing.
    public static Shot Fire(int kind, Vector2 from, Vector2 dir, float speed, float range, double damage,
                            float radius = 0f, int targetId = 0, float turnRate = 0f, PlayerShip source = null,
                            string hitSource = null, float size = 1f, int variant = 0, double hull = 0)
    {
        if (World == null) return null;
        var s = new Shot { Kind = kind, Position = from, Dir = dir.Normalized(), Speed = speed, Range = range,
                           Damage = damage, Radius = radius, TargetId = targetId, TurnRate = turnRate,
                           // EVERY SHOT IS ITS OWN SOURCE: the caller names the weapon, the body's
                           // identity is added here, so no volley can be mistaken for one ongoing
                           // source and swallowed by PlayerShip's 0.52 s gap
                           Source = source, HitSource = Shots.SourceKey(hitSource), Size = size, Variant = variant, Hull = hull,
                           NetId = Shots.Of(kind).Interceptable ? NextMissileId() : 0 };
        World.AddChild(s);
        ShotFired?.Invoke(s);
        return s;
    }

    // a boss's dodgeable round, and the scrap of the same volley: never a point-defence target,
    // because a round that is MEANT to be dodged, and that point defence could delete, would never
    // need dodging
    public static void FireSlug(Vector2 from, Vector2 dir, float speed, float range, float radius, double damage,
                                bool scrap, int variant, string hitSource)
        => Fire(scrap ? Shots.Scrap : Shots.Slug, from, dir, speed, range, damage, radius,
                hitSource: hitSource, variant: variant);

    // Unguided torpedoes pass targetId 0; a missile passes its target and a small turn rate, and
    // heavy for its looks. hostile = fired BY an enemy, so it seeks and hits player ships -- and it
    // gets an id, so point defence can shoot it down on every peer at once. source: the player
    // ship that fired it (host only), credited with combat on a hit.
    public static void LaunchTorpedo(Vector2 from, Vector2 dir, float speed, float range, double damage,
                                     int targetId = 0, float turnRate = 0f, bool heavy = false, bool hostile = false,
                                     PlayerShip source = null, string hitSource = null, float size = 1f)
        => Fire(hostile ? Shots.Seeker : heavy ? Shots.Missile : Shots.Torpedo, from, dir, speed, range, damage,
                targetId: targetId, turnRate: turnRate, source: source, hitSource: hitSource, size: size);

    // Dropped by the world on its way out. EVERY hook set by that world must go: each one is a
    // lambda holding the Hub, so one left behind is a freed node the next shot calls into, and a
    // Hub that can never be collected once you are back at the menu.
    // A world ends: its lists, its hooks (each a lambda holding that world) and its ids.
    public static void Clear()
    {
        Hostiles.Clear(); Players.Clear(); OnFlash = null; World = null; ShotFired = null; Fx.On = null;
        NetIds.Reset(); Shots.ResetSources(); Shot.Intercepted = 0;
    }
}
