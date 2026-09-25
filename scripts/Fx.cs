using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// WHAT AN EFFECT OR A WARNING LOOKS LIKE — one row each, raised by name, seen by everyone.
//
// There was one effect node in the game (Explosion) and everything else improvised. The
// freighter's shockwave, the warrior's EMP and the echo's detonation were drawn as SPOKES OF
// LASER FLASHES, because Combat.Flash happened to be the only drawing that reached a guest; the
// echo's burst was added straight to the host's tree, so nobody else ever saw it. Every new
// ability was going to invent its own again.
//
// An effect is a ROW: a shape, a colour, how long it lives, how wide it draws. `Fx.Raise` names
// the row and the world puts it up -- on the host AND on every guest, through one RPC
// (Hub.NetFx). One node draws them all, with the switch on the row's shape in one place.
//
// A WARNING IS A ROW HERE TOO, with `Warn` set: a red lane or a red zone, drawn at full size for
// a wind-up the RAISE gives, filling as the moment comes, flashing as the attack lands and
// staying lit while the attack itself lasts. Telegraph was a second node for the same sentence
// -- a shape, at a place, for a time, with a sound -- with a `bool Line` where this has a shape,
// fields set one by one where this has a raise, and an RPC of its own beside the one that
// already carried every effect. Pure visuals either way: the host resolves the hit when the time
// is up. A warning's CLOCK and its two SOUNDS are the MOVE's (BossMove.Windup, Cue, Strike), not
// the shape's, so they ride the raise: one boss move plays its cue with no warning at all, and
// a sound written in both tables is a sound that can disagree with itself.
//
// A NEW EFFECT OR WARNING IS A ROW HERE and a call to Fx.Raise / Fx.Warn. Nothing else in the
// game learns about it.
// ─────────────────────────────────────────────────────────────────────────────
public enum FxShape
{
    Burst,     // a disc that swells and fades, with a ring running ahead of it: something died
    Ring,      // one expanding ring: a wave going out, and how far it reaches
    Spokes,    // a ring with spokes to its edge: a pulse that struck everything inside it
    Bar,       // a thick straight bar from A to B, fading: a railgun's line
    Lane,      // A WARNING: a wide straight lane from A to B -- a beam, a ram, a fan, a thrown rock
    Zone,      // A WARNING: a disc at A -- a shockwave, a warp in, a missile's blast
    Debris,    // a chunk of the ANCHOR's own hull, cut at A, thrown toward To and tumbling to rest
    Sparks,    // Count hot sparks sprayed from A toward To's side, over the first half second
    Puffs,     // Count grey puffs left along the chunk's own path (the same seed as its Debris)
    Scar,      // a dark jagged patch at A, in the anchor's own frame, so it turns with the hull
    Vortex,    // a steady disc at A with rings drawn in to its centre: something pulls here, as long as the raise says
}

public class FxDef
{
    public string Id;
    public FxShape Shape;
    public Color Tint = new(1f, 0.7f, 0.3f);
    public double Life = 0.6;
    public float Width = 2f;        // the stroke of a ring, a spoke or a bar
    public int Spokes = 8;
    public bool Fill = true;        // a soft disc inside the ring
    public string Sound;            // an effect's own noise: Sfx.ByName id, or null for silence
    // A WARNING, NOT AN EFFECT: something is about to be hit here, and the raise says for how
    // long. The row's Life is not used -- a warning lives its wind-up, its hold and one flash --
    // and its sounds are the raise's, because they are the move's.
    public bool Warn;
    // THE TORN CHUNK'S KIND (Debris, Sparks, Puffs, Scar): rows raised WITH this one on every peer,
    // from the one raise (so the wire carries one effect, not four); at most Cap of this row on one
    // anchor, the oldest going; Loose, it READS its anchor (the hull art, where it went up, its
    // heading) as it goes up, then leaves it for the world, so a hull that dies under it does not
    // take it along; and how many pieces a spray has.
    public int[] With;
    public int Cap;
    public bool Loose;
    public int Count;
}

// ONE RAISE, whatever it is: which row, where, how big -- and, a warning only, how long the
// wind-up is, how long the attack itself lasts after it, how much of the wind-up is already
// gone, what it rides, and the move's two sounds. One struct because it is also what goes on the
// wire (Hub.NetFx): a field added here is added in ONE place, not in a hook, an RPC and two
// call sites.
public struct FxRaise
{
    public int Id;
    public Vector2 At, To;          // where it happens; the far end of a bar or a lane
    public float Size;              // a disc's radius, a lane's WIDTH
    public double Time, Hold, Since;
    public int Anchor;              // Fx.World, or the NetId of the hull it rides
    public string Cue, Strike;      // as the warning goes up, and as the attack lands
}

public static class Fx
{
    // The index IS the id on the wire (Hub.NetFx), so APPEND ONLY.
    public const int Burst = 0, Lost = 1, Rebuilt = 2, Wave = 3, Emp = 4, Echo = 5, Rail = 6,
                     WarnLane = 7, WarnZone = 8, AimZone = 9, TauntRing = 10,
                     Rip = 11, RipSparks = 12, RipSmoke = 13, Scar = 14,
                     RailEnhanced = 15, Tot = 16, Well = 17;
    // WHAT A WARNING RIDES: the world itself, or the NetId of the hull it is drawn on. A beam's
    // and a dash's lane are drawn in the BOSS'S OWN FRAME and parented to it, so the line it drew
    // is the line it fires down however the hull turns; everything else is pinned to the ground
    // it will hit. The world resolves the id (Hub.AddFx).
    public const int World = 0;
    // How long a warning stays lit once its attack is over.
    public const double Flash = 0.35;
    private static readonly Color Warning = new(1f, 0.15f, 0.12f);
    // ...and the same shape from YOUR side. An outpost's missile marks where it will land too, and
    // a mark in the enemy's red would read as a threat to the pilot watching it.
    private static readonly Color Friendly = new(0.35f, 0.75f, 1f);

    public static readonly FxDef[] All =
    {
        new() { Id = "burst",   Shape = FxShape.Burst, Tint = new(1f, 0.70f, 0.30f), Life = 0.6 },
        new() { Id = "lost",    Shape = FxShape.Burst, Tint = new(1f, 0.70f, 0.30f), Life = 0.6 },
        new() { Id = "rebuilt", Shape = FxShape.Burst, Tint = new(0.5f, 0.80f, 1.0f), Life = 0.6 },
        // a freighter's shockwave: a wide, slow ring that says exactly how far it threw things
        new() { Id = "wave",    Shape = FxShape.Ring,  Tint = new(0.60f, 0.80f, 1f), Life = 0.9, Width = 3f },
        // a warrior's EMP: a hard pulse with spokes, close in
        new() { Id = "emp",     Shape = FxShape.Spokes, Tint = new(0.70f, 0.90f, 1f), Life = 0.5, Width = 2f, Spokes = 10 },
        // an echo's detonation: everything it remembered, at once
        new() { Id = "echo",    Shape = FxShape.Spokes, Tint = new(1f, 0.80f, 0.45f), Life = 0.7, Width = 2.5f, Spokes = 6 },
        // a sniper's railgun: the line it threw down
        new() { Id = "rail",    Shape = FxShape.Bar,   Tint = new(0.45f, 0.70f, 1f), Life = 0.35, Width = 7f, Fill = false },
        // ── THE TWO WARNINGS: every red shape a boss or a raider raises is one of these ──
        // A lane: a beam, a ram, one line of a scrap fan, a thrown rock's path. A zone: a
        // shockwave, a warp in, a heavy missile's blast, the title screen's area shot. How long,
        // how wide and what it sounds like are the MOVE's, and come with the raise.
        new() { Id = "warn_lane", Shape = FxShape.Lane, Tint = Warning, Warn = true },
        new() { Id = "warn_zone", Shape = FxShape.Zone, Tint = Warning, Warn = true },
        // ...AND THE SAME ZONE IN YOUR OWN COLOURS: where one of YOUR side's shots will land. It
        // is drawn and timed exactly as a warning is -- it fills as the shot comes in -- but
        // nothing of yours is in danger from it, so it must not read red. Missiles.All names it,
        // and a second friendly telegraph is this row again.
        new() { Id = "aim_zone",  Shape = FxShape.Zone, Tint = Friendly, Warn = true },
        // a warden's Taunt: its reach, flashed once on the press (the raise's Size is the reach)
        new() { Id = "taunt_ring", Shape = FxShape.Ring, Tint = new(1f, 0.62f, 0.25f), Life = 0.8, Width = 3f, Fill = false },
        // -- THE TORN CHUNK (the destroyer's grapnel at cast-off; kits_v3 3.2, kits_v31 3.3) --
        // One raise (Fx.Tear): the chunk, cut from the anchor's own art at the hook and thrown at
        // ChunkSpeed within ChunkSpread degrees of the line to the destroyer, spinning 3-6 rad/s, at
        // rest in ChunkRest, fading over its last second; with it, 28 sparks from the wound (0.4-0.7 s
        // each), 6 grey puffs along its path (1.2 s each) and a scar on the hull for 10 s (the last
        // 3 fading), at most 3 on one anchor.
        new() { Id = "rip",        Shape = FxShape.Debris, Tint = new(1f, 0.55f, 0.25f), Life = 3.0, Loose = true, With = new[] { RipSparks, RipSmoke, Scar } },
        new() { Id = "rip_sparks", Shape = FxShape.Sparks, Tint = new(1f, 0.62f, 0.2f), Life = 1.2, Loose = true, Count = 28 },
        new() { Id = "rip_smoke",  Shape = FxShape.Puffs,  Tint = new(0.55f, 0.55f, 0.58f), Life = 3.0, Loose = true, Count = 6 },
        new() { Id = "scar",       Shape = FxShape.Scar,   Tint = new(0.10f, 0.07f, 0.06f), Life = 10.0, Cap = 3 },
        // an enhanced rail round (ActiveReload's perfect press): the rail's bar in white, 1.5x as wide
        new() { Id = "rail_enhanced", Shape = FxShape.Bar, Tint = new(0.92f, 0.96f, 1f), Life = 0.35, Width = 10.5f, Fill = false },
        // the freighter's Time on target: each line it converged on the paint, in the spotter's amber
        new() { Id = "tot",        Shape = FxShape.Bar,    Tint = new(1f, 0.78f, 0.35f), Life = 0.35, Width = 5f, Fill = false },
        // the bastion's gravity well (Wells.cs): its reach, for the caster's well_time (the raise's own life)
        new() { Id = "well",       Shape = FxShape.Vortex, Tint = new(0.72f, 0.48f, 1f), Life = 6.0, Width = 2f },
    };

    public static FxDef Of(int id) => All[id >= 0 && id < All.Length ? id : Burst];

    // Set by the live world: it puts the effect up here AND tells its guests (Hub), or just puts
    // it up (the title screen). Dropped with the world by Combat.Clear -- it is a lambda holding
    // that world, like every other hook here.
    public static System.Action<FxRaise> On;

    // `at` is where it happens; `radius` how far it reaches; `to` the far end of a bar; `time` an effect's
    // own life where the raise decides it (a well lasts its caster's well_time), 0 for its row's Life.
    //
    // ONLY THE SIMULATOR RAISES. The hook puts it up here and tells every guest through one RPC
    // (Hub.NetFx), so a guest that also raised its own -- the burst on a turret it saw removed,
    // the blast at the end of a missile it was drawing, a miner's loss -- drew the same effect
    // twice. The single player and the title screen are their own simulator and are unaffected.
    public static void Raise(int id, Vector2 at, float radius = 30f, double time = 0)
    { if (Net.Sim) On?.Invoke(new FxRaise { Id = id, At = at, To = at, Size = radius, Time = time }); }
    public static void Line(int id, Vector2 a, Vector2 b)
    { if (Net.Sim) On?.Invoke(new FxRaise { Id = id, At = a, To = b }); }

    // A WARNING: the same rows, the same hook and the same one RPC, with the clock and the two
    // sounds the move has. It takes the whole raise because it has seven things to say and they
    // read better named than counted -- and because that struct is what the wire carries.
    public static void Warn(FxRaise r) { if (Net.Sim) On?.Invoke(r); }

    // TEAR A CHUNK OUT OF A HULL: the LOOK of it, raised once and drawn by every peer. `hook` is where
    // on the hull (in the world), `toward` where the chunk is thrown (the destroyer). It rides the
    // anchor's NetId, so every peer cuts it from the same art at the same spot of the same hull and
    // tumbles it on the same seed. What the rip DEALS is its caller's hit (the damage door). A hull
    // with no art of its own (a drawn practice dummy) still tears: a plain chunk, sized off its hit
    // circle. Raise it BEFORE the hit it goes with: the world finds the anchor among the living
    // (Combat.ById), so a tear raised after the hit that killed its hull has nothing to cut from.
    public const float ChunkShare = 0.16f, ChunkSpeed = 260f, ChunkSpread = 25f, ChunkRest = 2f;
    public static void Tear(IHittable anchor, Vector2 hook, Vector2 toward)
    {
        if (!Net.Sim || anchor is not Node2D n) return;
        On?.Invoke(new FxRaise { Id = Rip, At = n.ToLocal(hook), To = toward, Size = ChunkShare * LengthOf(anchor), Anchor = anchor.NetId });
    }
    // how long a hull is, for its chunk: its art's length, else the width of its hit circle
    public static float LengthOf(IHittable h) => h is Node n && HullLength(n) is > 0 and var l ? l : 2 * h.HitRadius;
    // A HULL'S OWN ART: the longest Sprite2D directly under it (a turret's is shorter), and its length.
    public static Sprite2D HullArt(Node n)
    {
        Sprite2D best = null;
        foreach (var c in n.GetChildren())
            if (c is Sprite2D s && s.Texture != null && (best == null || s.Texture.GetHeight() * s.Scale.Y > best.Texture.GetHeight() * best.Scale.Y)) best = s;
        return best;
    }
    public static float HullLength(Node n) => HullArt(n) is { } s ? s.Texture.GetHeight() * s.Scale.Y : 0f;

    // WHERE THE CHUNK IS, t seconds after it went up at `start`, and how far it has spun: a pure
    // function of the raise (Seed of its anchor and its hook), so every peer draws the same chunk
    // with nothing more on the wire. It leaves at ChunkSpeed and slows evenly to rest at ChunkRest.
    public static int Seed(int anchor, Vector2 at) =>
        unchecked(anchor * 73856093 ^ Mathf.RoundToInt(at.X * 8) * 19349663 ^ Mathf.RoundToInt(at.Y * 8) * 83492791);
    public static float U(int seed, int k)
    {
        uint h = unchecked((uint)seed * 2654435761u ^ (uint)k * 40503u);
        h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13; h = unchecked(h * 3266489917u); h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777216f;
    }
    public static (Vector2 At, float Spin) Tumble(int seed, Vector2 start, Vector2 toward, double t)
    {
        var line = toward - start;
        var dir = (line.LengthSquared() > 1e-6f ? line.Normalized() : Vector2.Up).Rotated(Mathf.DegToRad((U(seed, 0) * 2 - 1) * ChunkSpread));
        float rate = (3f + 3f * U(seed, 1)) * (U(seed, 2) < 0.5f ? -1f : 1f);
        float k = (float)System.Math.Clamp(t, 0, ChunkRest);
        float run = k - k * k / (2 * ChunkRest);           // v0 (1 - t / rest), integrated
        return (start + dir * ChunkSpeed * run, rate * run);
    }

    // EVERY NODE UP RIGHT NOW. A node puts itself in as it enters the tree and takes itself out
    // as it leaves, so a world that goes away empties this by itself: there is no list to clear
    // and nothing to leak.
    private static readonly System.Collections.Generic.List<FxNode> LiveNodes = new();
    internal static void Entered(FxNode n) { if (!LiveNodes.Contains(n)) LiveNodes.Add(n); }
    internal static void Left(FxNode n) => LiveNodes.Remove(n);
    // The warnings still standing, for the peer that has to be told about them (Boss.CatchUp).
    public static System.Collections.Generic.IEnumerable<FxNode> Warnings
    {
        get { foreach (var n in LiveNodes) if (n.Warn && !n.IsQueuedForDeletion()) yield return n; }
    }
    // EVERY WARNING DOWN: whatever raised them threatens nothing any more. A boss that is down
    // calls this on every peer -- a beam half wound up used to go on charging, and "fire", over
    // a DEFEATED boss.
    public static void ClearWarnings()
    { foreach (var n in new System.Collections.Generic.List<FxNode>(LiveNodes)) if (n.Warn) n.QueueFree(); }
}

// One node for every row -- an effect or a warning: the shape is the row's, so a new one of
// either never brings a new node. A warning runs on the RAISE's clock (Time, Hold, Since) and
// plays the RAISE's sounds; everything else lives the row's Life and plays the row's.
public partial class FxNode : Node2D
{
    public int Id;
    public Vector2 To;
    public float Radius = 30f;              // a disc's radius; a LANE's width
    public double Time, Hold, Since;        // a warning's wind-up, its hold, and what is gone of it
    public int Anchor;                      // what it rides: Fx.World, or a hull's NetId
    public string Cue, Strike;              // the move's sounds, as it goes up and as it lands
    private double _t;
    private bool _struck;
    // A LOOSE piece (Debris, Sparks, Puffs): where it went up in the world, the hook it was cut at in
    // its anchor's frame, the anchor's heading then, and the seed every peer shares.
    public Vector2 Start { get; private set; }
    private Vector2 _at;
    private float _rot0;
    private int _seed;
    public int Seed => _seed;
    private Vector2[] _uv;
    private Texture2D _tex;
    public double Elapsed => _t;
    public bool Warn => Fx.Of(Id).Warn;
    private FxDef D => Fx.Of(Id);
    private double Life => D.Warn ? Time + Hold + Fx.Flash : Time > 0 ? Time : D.Life;

    // In the live list while in the tree, so a Loose piece leaving its hull for the world stays in it.
    public override void _EnterTree() => Fx.Entered(this);
    public override void _Ready()
    {
        ZIndex = 7; ZAsRelative = false;
        var d = D;
        _at = Position;
        _seed = Fx.Seed(Anchor, _at);
        if (d.Cap > 0 && GetParent() is { } home)
        {   // at most Cap on one anchor: the oldest goes
            var same = new System.Collections.Generic.List<FxNode>();
            foreach (var c in home.GetChildren()) if (c is FxNode f && f.Id == Id && !f.IsQueuedForDeletion()) same.Add(f);
            same.Sort((a, b) => b.Elapsed.CompareTo(a.Elapsed));
            for (int i = 0; i < same.Count - d.Cap; i++) same[i].QueueFree();
        }
        if (d.Loose)
        {   // cut from the anchor's art here and now, then out into the world
            if (d.Shape == FxShape.Debris && GetParent() is { } hull && Fx.HullArt(hull) is { } art) CutFrom(art);
            var g = GlobalPosition;
            _rot0 = GetParent() is Node2D p ? p.GlobalRotation : 0f;
            TopLevel = true;
            GlobalPosition = g; Start = g;
            Rotation = d.Shape == FxShape.Debris ? _rot0 : 0f;
        }
        // its companions, and a Loose piece's move to the world: deferred, the parent is mid-AddChild
        if (d.With != null || d.Loose) Callable.From(Settle).CallDeferred();
        // A warning's opening sound is the move's, an effect's is its row's; and a copy sent on to
        // a peer that arrived mid-warning has already missed it. GlobalPosition, not Position: a
        // warning that rides a hull is a child of it, and its Position is an offset from the
        // boss's nose rather than a place in the world.
        var opening = Cue ?? D.Sound;
        if (opening != null && Since <= 0) Sfx.ByName(opening, GlobalPosition);
    }
    public override void _ExitTree() => Fx.Left(this);

    // ONCE UP: its companions go up on its anchor from this very raise (each reads the hull as this
    // did), and a Loose piece, its hull read, leaves it for the world -- nothing after _Ready reads the
    // anchor again, and a hull killed within the chunk's 3 s would free it along with itself. Nothing
    // is built before this runs, so a hull freed first frees this node and nothing is left over.
    private void Settle()
    {
        if (!IsInstanceValid(this) || GetParent() is not { } hull) return;
        var d = D;
        if (d.With != null)
            foreach (int w in d.With)
                hull.AddChild(new FxNode { Id = w, Position = _at, To = To, Radius = Radius, Anchor = Anchor });
        if (d.Loose && Combat.World is { } world && IsInstanceValid(world) && !world.IsQueuedForDeletion() && world != hull)
            Reparent(world);
    }

    // THE CHUNK'S OUTLINE (a scrap shard's, scaled to the raise's Size) and where each corner sits
    // on the anchor's texture, so the chunk shows the very plating it was torn from.
    private Vector2[] Outline(float share)
    {
        var shard = Shot.Shards[Mathf.PosMod(_seed, Shot.Shards.Length)];
        var o = new Vector2[shard.Length];
        for (int i = 0; i < shard.Length; i++) o[i] = shard[i] * (Radius / 18f) * share;
        return o;
    }
    private void CutFrom(Sprite2D art)
    {
        _tex = art.Texture;
        var size = _tex.GetSize();
        var o = Outline(1f);
        _uv = new Vector2[o.Length];
        for (int i = 0; i < o.Length; i++)
        {
            var local = (_at + o[i] - art.Position) / art.Scale;
            _uv[i] = (art.Centered ? local + size / 2 : local) / size;
        }
    }

    public override void _Process(double delta)
    {
        _t += delta;
        if (D.Warn && !_struck && _t >= Time)
        {   // it lands: the move's own sound, once -- and never for a copy that arrived after it
            _struck = true;
            if (Time > 0 && Strike != null) Sfx.ByName(Strike, GlobalPosition);
        }
        if (D.Shape == FxShape.Debris)
        {
            var (at, spin) = Fx.Tumble(_seed, Start, To, _t);
            GlobalPosition = at; Rotation = _rot0 + spin;
        }
        if (_t >= Life) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var d = D;
        // A WARNING FILLS over the whole wind-up, including whatever of it had gone before this
        // copy went up; an effect runs out over its row's Life.
        float k = (float)Mathf.Clamp(d.Warn ? (Since + _t) / System.Math.Max(1e-6, Since + Time) : _t / Life, 0, 1);
        float fade = 1 - k;
        var c = d.Tint;
        // A warning pulses faster as the moment comes, flashes white-hot as the attack lands, and
        // dies away over Fx.Flash once the attack itself is over.
        bool firing = d.Warn && _t >= Time;
        Color edge = default, fill = default;
        if (d.Warn)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)_t * (8f + 10f * k));
            edge = new Color(c.R, c.G, c.B, firing ? 1f : 0.55f + 0.35f * pulse);
            fill = firing ? new Color(1f, 0.85f, 0.8f, 0.7f * (float)(1 - System.Math.Max(0, _t - Time - Hold) / Fx.Flash))
                          : new Color(c.R, c.G, c.B, 0.10f + 0.12f * k);
        }
        switch (d.Shape)
        {
            case FxShape.Burst:
                DrawCircle(Vector2.Zero, Radius * (0.4f + 0.8f * k), new Color(c.R, c.G, c.B, 0.55f * fade));
                DrawArc(Vector2.Zero, Radius * (0.6f + 1.2f * k), 0, Mathf.Tau, 32, new Color(1f, 0.9f, 0.7f, 0.8f * fade), 2f);
                break;
            case FxShape.Ring:
                if (d.Fill) DrawCircle(Vector2.Zero, Radius * k, new Color(c.R, c.G, c.B, 0.10f * fade));
                DrawArc(Vector2.Zero, Radius * k, 0, Mathf.Tau, 96, new Color(c.R, c.G, c.B, fade), d.Width);
                break;
            case FxShape.Spokes:
                if (d.Fill) DrawCircle(Vector2.Zero, Radius * k, new Color(c.R, c.G, c.B, 0.12f * fade));
                DrawArc(Vector2.Zero, Radius * k, 0, Mathf.Tau, 64, new Color(c.R, c.G, c.B, fade), d.Width);
                for (int i = 0; i < d.Spokes; i++)
                {
                    var dir = Vector2.Up.Rotated(Mathf.Tau * i / d.Spokes);
                    DrawLine(dir * (Radius * k * 0.25f), dir * (Radius * k), new Color(c.R, c.G, c.B, 0.7f * fade), d.Width * 0.6f);
                }
                break;
            case FxShape.Bar:
            {
                // To is in the PARENT's frame, like Position: a warning that rides a hull is a
                // child of it, and ToLocal would read the far end as a point in the world.
                var b = To - Position;
                DrawLine(Vector2.Zero, b, new Color(c.R, c.G, c.B, 0.35f * fade), d.Width * 2.2f);
                DrawLine(Vector2.Zero, b, new Color(c.R, c.G, c.B, fade), d.Width);
                DrawLine(Vector2.Zero, b, new Color(1f, 1f, 1f, 0.9f * fade), d.Width * 0.3f);
                break;
            }
            case FxShape.Lane:
            {
                var b = To - Position;
                var dir = b.Normalized(); var side = new Vector2(-dir.Y, dir.X) * Radius / 2f;
                DrawColoredPolygon(new[] { side, b + side, b - side, -side }, fill);
                if (!firing)   // the wind-up creeps down the lane's length
                    DrawColoredPolygon(new[] { side, b * k + side, b * k - side, -side }, new Color(c.R, c.G, c.B, 0.22f));
                DrawPolyline(new[] { side, b + side, b - side, -side, side }, edge, 2.5f);
                break;
            }
            case FxShape.Vortex:
            {   // steady for its life, fading over its last half second; three rings run in to the centre
                float a = (float)Mathf.Clamp((Life - _t) / 0.5, 0, 1);
                DrawCircle(Vector2.Zero, Radius, new Color(c.R, c.G, c.B, 0.10f * a));
                DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 96, new Color(c.R, c.G, c.B, 0.8f * a), d.Width);
                for (int i = 0; i < 3; i++)
                {
                    float r = Radius * (1f - Mathf.PosMod((float)_t * 0.8f + i / 3f, 1f));
                    DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 64, new Color(c.R, c.G, c.B, 0.5f * a * r / Radius), d.Width);
                }
                break;
            }
            case FxShape.Zone:
                DrawCircle(Vector2.Zero, Radius, fill);
                if (!firing) DrawCircle(Vector2.Zero, Radius * k, new Color(c.R, c.G, c.B, 0.20f));   // it grows outward
                DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 72, edge, 3f);
                break;
            case FxShape.Debris:
            {   // the plating itself, hot along the torn edge, fading over its last second
                float a = (float)Mathf.Clamp(d.Life - _t, 0, 1);
                var o = Outline(1f);
                var cols = new Color[o.Length];
                System.Array.Fill(cols, new Color(1f, 1f, 1f, a));
                if (_tex != null) DrawPolygon(o, cols, _uv, _tex);
                else DrawColoredPolygon(o, new Color(0.42f, 0.30f, 0.24f, a));
                var loop = new Vector2[o.Length + 1]; o.CopyTo(loop, 0); loop[^1] = o[0];
                DrawPolyline(loop, new Color(c.R, c.G, c.B, a * (1f - 0.7f * k)), 1.5f);
                break;
            }
            case FxShape.Sparks:
            {
                float emit = 0.5f, toward = (To - Start).Angle();
                for (int i = 0; i < d.Count; i++)
                {
                    float born = emit * i / d.Count, life = 0.4f + 0.3f * Fx.U(_seed, 10 + 3 * i);
                    float age = (float)_t - born;
                    if (age < 0 || age > life) continue;
                    var dir = Vector2.Right.Rotated(toward + (Fx.U(_seed, 11 + 3 * i) - 0.5f) * 2.4f);
                    var p = dir * (120f + 180f * Fx.U(_seed, 12 + 3 * i)) * age;
                    DrawLine(p, p - dir * 7f, new Color(c.R, c.G, c.B, 1f - age / life), 1.6f);
                }
                break;
            }
            case FxShape.Puffs:
                for (int i = 0; i < d.Count; i++)
                {
                    float born = 0.35f * i, age = (float)_t - born;
                    if (age < 0 || age > 1.2f) continue;
                    var p = Fx.Tumble(_seed, Start, To, born).At - Start;
                    DrawCircle(p, Radius * (0.15f + 0.2f * age / 1.2f), new Color(c.R, c.G, c.B, 0.35f * (1f - age / 1.2f)));
                }
                break;
            case FxShape.Scar:
            {
                float a = 0.85f * (float)Mathf.Clamp((d.Life - _t) / 3.0, 0, 1);
                var o = Outline(1f);
                DrawColoredPolygon(o, new Color(c.R, c.G, c.B, a));
                var loop = new Vector2[o.Length + 1]; o.CopyTo(loop, 0); loop[^1] = o[0];
                DrawPolyline(loop, new Color(0.45f, 0.22f, 0.1f, a * 0.8f), 1.2f);
                break;
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// A FIELD: WHAT A SHIP DRAWS ROUND ITSELF WHILE ONE OF ITS SLOTS RUNS -- one row each (F9).
//
// PlayerShip._Draw drew the freighter's bubble as a block of its own, and every field the kits add
// (the Supercarrier's patrol ring, the Taunt's shimmer, the boost's plume) was going to be another
// block beside it, each reading its own slot by name. It replaced that block: the bubble is this
// table's first row, and _Draw draws the table (Fields.Draw).
//
// A NEW FIELD IS A ROW: the SLOT it shows for (a class's ability row, or its drive's -- whatever
// id PlayerShip.Sl knows it by), its LOOK, its RADIUS (a stat on the ship's sheet, else a share of
// the hull's length), its tint; and, if it has them, the POOL it fades as it spends (the slot's Own
// against a stat, and it is down once Own is spent) and a TAG (1 - a stat, written "−33%").
// Slots are on the wire (PlayerShip's state report), so every peer draws the same field from the
// same slot and a field needs no RPC. A ship that has no such slot draws nothing for the row.
// ─────────────────────────────────────────────────────────────────────────────
public enum FieldLook
{
    Ring,      // a soft disc and its edge: what it covers (the bubble)
    Dashed,    // a dashed ring, turning slowly: a reach something patrols (the Supercarrier's patrol)
    Shimmer,   // hex plates over the hull, pulsing, and the row's tag under it (the Taunt's guard)
    Plume,     // a long hot plume out of the stern over the engine's own (a drive's boost)
    Wedge,     // a fan on the ship's guard (IPrism.GuardAngle), one shade a band of Prism.Bands (the prism stance)
    Lance,     // a beam out of the main barrel, the slot's Own long, green on a friend and red on a foe (the Tender's lance)
}

public class FieldDef
{
    public string Id, Slot;
    public FieldLook Look;
    public string RadiusStat;               // the radius, from the ship's sheet; null: HullShare of its length
    public float HullShare = 0.6f;
    public Color Tint = new(0.55f, 0.85f, 1f);
    public string PoolStat;                 // fades as the slot's Own is spent against this; down at 0
    public string TagStat;                  // a tag under the hull: 1 - this stat, as a percentage off
}

// ONE FIELD UP RIGHT NOW: its row, its radius, how much of its pool is left (1 with no pool), its tag.
public readonly record struct FieldUp(FieldDef Row, float Radius, float Left, string Tag);

public static class Fields
{
    public static readonly FieldDef[] All =
    {
        // the freighter's bubble: a ring the size of what it covers, fading as its pool is spent, so
        // everyone can see how much of it is left and who is inside it
        new() { Id = "bubble", Slot = "bubble", Look = FieldLook.Ring, RadiusStat = "bubble_radius", PoolStat = "bubble_pool" },
        // the Supercarrier: the ring its patrol wing fights inside, in the fighter's colour (kits_v3 §3.1)
        new() { Id = "patrol", Slot = "super", Look = FieldLook.Dashed, RadiusStat = "patrol_range", Tint = Beam.Of(Beam.Fighter).Tint },
        // the Taunt: a hex-plate shimmer over the hull and what it takes off every blow (kits_v3 §3.5)
        new() { Id = "taunt", Slot = "taunt", Look = FieldLook.Shimmer, HullShare = 0.55f, TagStat = "taunt_guard", Tint = new(1f, 0.62f, 0.25f) },
        // the boost (the nine's drive, kits_v31 §3.4): the engine burning hot while it runs
        new() { Id = "boost", Slot = "boost", Look = FieldLook.Plume, HullShare = 1.8f, Tint = new(1f, 0.85f, 0.55f) },
        // the prism stance (kits_v2 Warrior card): the guard's wedge, SQUARE bright and SLANT faint, on every peer
        new() { Id = "prism", Slot = "prism", Look = FieldLook.Wedge, HullShare = 1.1f, Tint = new(0.75f, 0.95f, 1f) },
        // the Tender's mending lance (kits6b-J7): the beam while the trigger holds, pale on nothing, on every peer
        new() { Id = "lance", Slot = PlayerShip.LanceSlot, Look = FieldLook.Lance, Tint = new(0.85f, 0.95f, 1f) },
        // the Tender's fields (kits6b-J8): what the overdrive lifts and what the repair field mends, the same 500 u
        new() { Id = "overdrive", Slot = "overdrive", Look = FieldLook.Ring, RadiusStat = "field_radius", Tint = new(1f, 0.72f, 0.3f) },
        new() { Id = "repair", Slot = "repair", Look = FieldLook.Ring, RadiusStat = "field_radius", Tint = new(0.45f, 1f, 0.55f) },
    };

    public static FieldDef Of(string id) => System.Array.Find(All, f => f.Id == id);
    // the lance's two colours: what it mends, and what it burns
    public static readonly Color LanceMend = new(0.45f, 1f, 0.55f), LanceBurn = new(1f, 0.45f, 0.3f);

    // WHAT IS UP, from four readers, so the rule is provable with no ship at all: does the ship have
    // the slot, the slot's Left and Own, a stat off its sheet, and its hull's length.
    public static System.Collections.Generic.IEnumerable<FieldUp> Up(System.Func<string, bool> has,
        System.Func<string, (double Left, double Own)> slot, System.Func<string, double> stat, float length)
    {
        foreach (var f in All)
        {
            if (!has(f.Slot)) continue;
            var (left, own) = slot(f.Slot);
            if (left <= 0) continue;
            float share = 1f;
            if (f.PoolStat != null)
            {
                if (own <= 0) continue;
                share = (float)Mathf.Clamp(own / System.Math.Max(1, stat(f.PoolStat)), 0, 1);
            }
            float r = f.RadiusStat != null ? (float)stat(f.RadiusStat) : length * f.HullShare;
            string tag = f.TagStat != null ? $"−{Mathf.RoundToInt((float)(1 - stat(f.TagStat)) * 100)}%" : null;
            yield return new FieldUp(f, r, share, tag);
        }
    }

    public static System.Collections.Generic.IEnumerable<FieldUp> Up(PlayerShip s)
    {
        var mine = Abilities.For(s.Class);
        return Up(id => System.Array.Exists(mine, d => d.Id == id),
                  id => { var sl = s.Sl(id); return (sl.Left, sl.Own); }, id => s.Stats[id], s.MyArt.Length);
    }

    // EVERY FIELD UP ON THIS SHIP, in its own frame (PlayerShip._Draw). The one switch on the look.
    public static void Draw(PlayerShip s)
    {
        foreach (var f in Up(s))
        {
            var c = f.Row.Tint;
            switch (f.Row.Look)
            {
                case FieldLook.Ring:
                {
                    var e = new Color(c.R, c.G, c.B, 0.15f + 0.35f * f.Left);
                    s.DrawCircle(Vector2.Zero, f.Radius, e with { A = e.A * 0.25f });
                    s.DrawArc(Vector2.Zero, f.Radius, 0, Mathf.Tau, 64, e, 2.5f);
                    break;
                }
                case FieldLook.Dashed:
                {   // world-steady: the dashes turn slowly against the ship's own turning
                    const int Dashes = 48;
                    float spin = (float)(Time.GetTicksMsec() / 1000.0 * 0.15) - s.Rotation;
                    for (int i = 0; i < Dashes; i++)
                    {
                        float a0 = spin + Mathf.Tau * i / Dashes;
                        s.DrawArc(Vector2.Zero, f.Radius, a0, a0 + Mathf.Tau / Dashes * 0.55f, 6, new Color(c.R, c.G, c.B, 0.7f), 2.5f);
                    }
                    break;
                }
                case FieldLook.Shimmer:
                {   // hex plates inside the hull's own outline, a pulse running across them
                    var art = s.MyArt;
                    float hx = art.HalfWidth * 1.05f, hy = f.Radius, cell = System.Math.Max(8f, hx * 0.32f);
                    float t = Time.GetTicksMsec() / 1000f;
                    for (float y = -hy; y <= hy; y += cell * 0.87f)
                        for (float x = -hx + ((int)((y + hy) / (cell * 0.87f)) % 2) * cell * 0.5f; x <= hx; x += cell)
                        {
                            if ((x * x) / (hx * hx) + (y * y) / (hy * hy) > 1f) continue;
                            float glow = 0.25f + 0.25f * Mathf.Sin(t * 5f + y * 0.05f + x * 0.03f);
                            var hex = new Vector2[7];
                            for (int k = 0; k < 7; k++) hex[k] = new Vector2(x, y) + Vector2.Right.Rotated(Mathf.Tau * k / 6f + Mathf.Pi / 6f) * cell * 0.5f;
                            s.DrawPolyline(hex, new Color(c.R, c.G, c.B, glow), 1.2f);
                        }
                    if (f.Tag != null)
                    {   // upright under the hull, whatever the heading
                        s.DrawSetTransform(Vector2.Zero, -s.Rotation, Vector2.One);
                        Txt.Centre(s, ThemeDB.FallbackFont, new Vector2(0, art.Length * 0.5f + 22f), f.Tag, Txt.Size(14), c);
                        s.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                    }
                    break;
                }
                case FieldLook.Wedge:
                {   // widest band first, so the square one draws over it; the guard itself a hard line
                    float g = s.GuardAngle - s.Rotation;
                    for (int b = Prism.Bands.Length - 1; b >= 0; b--)
                    {
                        float half = Mathf.DegToRad(Prism.Bands[b].MaxAngle);
                        var fan = new Vector2[18];
                        fan[0] = Vector2.Zero;
                        for (int k = 0; k < 17; k++) fan[k + 1] = Vector2.Right.Rotated(g - half + 2f * half * k / 16f) * f.Radius;
                        s.DrawColoredPolygon(fan, new Color(c.R, c.G, c.B, b == 0 ? 0.32f : 0.14f));
                    }
                    s.DrawLine(Vector2.Zero, Vector2.Right.Rotated(g) * f.Radius * 1.15f, c, 2.5f);
                    break;
                }
                case FieldLook.Plume:
                {
                    var art = s.MyArt;
                    Plume.Draw(s, new Vector2(0, art.Length * 0.5f - art.EngineInset), Vector2.Down, f.Radius, c, 1f, true);
                    break;
                }
                case FieldLook.Lance:
                {   // out of the main barrel as it points on this peer, as far as the host said it reached,
                    // in the colour of what it is on (the slot's N): a friend mended, a foe burned, or nothing
                    var sl = s.Sl(f.Row.Slot);
                    var (at, dir) = s.MainBore;
                    Vector2 a = s.ToLocal(at), b = s.ToLocal(at + dir * (float)sl.Own);
                    var col = sl.N == PlayerShip.LanceMend ? LanceMend : sl.N == PlayerShip.LanceBurn ? LanceBurn : c;
                    s.DrawLine(a, b, col with { A = 0.3f }, 9f);
                    s.DrawLine(a, b, col, 3f);
                    if (sl.N != PlayerShip.LanceNone) s.DrawCircle(b, 7f, col with { A = 0.6f });
                    break;
                }
            }
        }
    }
}
