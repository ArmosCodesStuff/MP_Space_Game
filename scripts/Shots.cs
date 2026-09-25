using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// EVERYTHING THAT FLIES AND HITS — one row each, one flyer.
//
// There were three of these classes, each with its own copy of the same job: step along a
// heading, sweep the step for something it touches, deal the damage, burst, and draw itself.
// Shell.cs, Slug.cs and Torpedo.cs shared the sweep, the lifetime, the cosmetic-copy rule and the
// hit -- and differed only in WHO they hit, HOW LONG they burst for and WHAT THEY LOOK LIKE. A
// fourth weapon meant a fourth copy, and the railgun and the hunter-seekers this session nearly
// were two more.
//
// A shot is now a ROW (ShotDef) and a call. Its numbers -- speed, range, damage, radius, guidance,
// size -- come from whatever fires it; its behaviour comes from the row. One RPC carries all of
// them to guests (Hub.NetShot) where there used to be three.
//
// NOT here: ThrownRock. A boss's rock is not a projectile -- it is held in a tractor beam, thrown
// down a fixed lane on a cubic ease, strikes everything in the lane ONCE and breaks at the end.
// It shares the sweep (Shots.Sweep) and nothing else.
// ─────────────────────────────────────────────────────────────────────────────
public enum ShotLook { Bullet, Ball, Shard, Missile }

public class ShotDef
{
    public string Id;
    public bool AtPlayers;           // who it may touch: the base's enemies, or the pilots
    public float Pad = 3f;           // added to the target's own radius when testing a touch
    public float Sweep = 6f;         // the path is tested every this many units; 0 = the end point only
    public double Burst;             // seconds it burns where it stopped; 0 = it simply goes
    public bool Smoke;               // a trail behind it, and it lingers until the last of it fades
    public bool Guided;              // its nose may turn toward a target (TurnRate, TargetId)
    public bool Interceptable;       // it joins Combat.Hostiles with an id: whatever may strike its Tags can bring it down
    public bool MarkEveryPeer;       // the damage number is drawn wherever it is seen, not just on the host
    public ShotLook Look;
    public bool Heavy;               // the bigger body, the darker smoke, the wider blast
    public string Sound;             // Sfx.ByName id at launch, or null (Bullet and Missile have their own)
    // WHAT IT COUNTS AS to everything that asks (Tags.cs). Tag.Missile -- point defence's alone, and
    // the first hit brings it down -- for every row but one whose body carries a HULL of its own:
    // that row says Tag.Hulled, and its body is a target like any hull (Shot.Hull).
    public Tag Tags = Tag.Missile;
    public string Label;             // what a target line calls one, for a row that can be picked (Tag.Hulled)
    // HOW MANY BODIES IT STRIKES BEFORE IT ENDS: 1 = the first it touches, 0 = everything on its path
    // (it flies on to its range). The same word and the same rule as a Lines row's (Lines.cs), so a
    // piercing slug and a piercing line are one idea. Each body is struck once, however many of the
    // sweep's points fall inside it.
    public int Stops = 1;
    // A PRISM MAY TURN IT BACK (F11, Prism.cs): a round of this row caught on a guard is fired back as a
    // friendly Reflect round (Shot.Strike). The siege's cruise missile never is.
    public bool Reflectable;
    // A DECOY MAY TURN IT (F14, Decoys.cs): a hostile guided row a burning point lures onto itself. The
    // siege's cruise missile never is.
    public bool Decoyable;
    // A FUSED ROUND (the Warden's flak): it bursts once a hostile hull is within Fuse of it, or at the end of its
    // range, and every hostile hull within Fuse of the burst takes it (Shot.Burst) -- x ResistShare on one that
    // carries a Resists tag (a boss's x0.75). 0: it strikes what it touches, as every other row does.
    public float Fuse;
    public Tag Resists;
    public double ResistShare = 1;
    // A HIT MARKS ITS TARGET FOR ITS FIRER (the freighter's spotter, kits 6b): the stat id, on the firer's
    // sheet, of the seconds a hit paints for -- or null, a round that marks nothing. The host calls
    // PlayerShip.PaintOn on a hostile it strikes; the paint rides the firer's slots to every peer.
    public string Paint;
    // A ROUND BUILT FOR WHAT HOLDS A SPOT (the bastion's bunker buster, kits 6b): a body carrying any of
    // Versus takes VersusMult times the blow, and a body behind a shield (IShielded, Emplacements.cs)
    // takes Through of it (0 = the shield stops it, as it stops everything else).
    public Tag Versus;
    public double VersusMult = 1, Through;
}

public static class Shots
{
    // The index IS the id on the wire (Hub.NetShot), so APPEND ONLY.
    public const int Shell = 0, Slug = 1, Scrap = 2, Torpedo = 3, Missile = 4, Seeker = 5, Cruise = 6, Reflect = 7, Flak = 8, Spotter = 9, Buster = 10;

    public static readonly ShotDef[] All =
    {
        // a main gun's round: straight, and it hits the first thing that is not a missile point
        // defence alone may have (Tag.Missile) -- a cruise missile's hull it strikes like any hull
        new() { Id = "shell",   AtPlayers = false, Pad = 3f,  Sweep = 6f, Look = ShotLook.Bullet },
        // a boss's slow, dodgeable round, and the scrap of the same volley
        new() { Id = "slug",    AtPlayers = true,  Pad = 0f,  Sweep = 6f, Burst = 0.25, Look = ShotLook.Ball, Sound = "drake_gun", Reflectable = true },
        new() { Id = "scrap",   AtPlayers = true,  Pad = 0f,  Sweep = 6f, Burst = 0.25, Look = ShotLook.Shard, Reflectable = true },
        // a bomber's torpedo: straight, trailing smoke, and it detonates on what it touches
        new() { Id = "torpedo", AtPlayers = false, Pad = 4f,  Sweep = 0f, Burst = 0.5, Smoke = true, Guided = true,
                MarkEveryPeer = true, Look = ShotLook.Missile },
        // the destroyer's burst and the warden's hunters: the same thing, heavier
        new() { Id = "missile", AtPlayers = false, Pad = 4f,  Sweep = 0f, Burst = 0.5, Smoke = true, Guided = true,
                MarkEveryPeer = true, Look = ShotLook.Missile, Heavy = true },
        // fired AT the pilots, and the one thing point defence exists for
        new() { Id = "seeker",  AtPlayers = true,  Pad = 4f,  Sweep = 0f, Burst = 0.5, Smoke = true, Guided = true,
                Interceptable = true, MarkEveryPeer = true, Look = ShotLook.Missile, Reflectable = true, Decoyable = true },
        // A CRUISE MISSILE: slow, long-legged and guided, fired AT the pilots -- and a target in its
        // own right. Its body carries a hull (Tag.Hulled, not Tag.Missile), so every gun, blow, wing
        // and seeker a pilot has may bring it down and point defence wears it down rather than
        // deleting it; how much hull, like how fast and how hard, is the launcher's (TurretSpec).
        new() { Id = "cruise",  AtPlayers = true,  Pad = 4f,  Sweep = 0f, Burst = 0.5, Smoke = true, Guided = true,
                Interceptable = true, MarkEveryPeer = true, Look = ShotLook.Missile,
                Tags = Tag.Hulled, Label = "CRUISE MISSILE" },
        // A ROUND A PRISM TURNED BACK (Shot.Strike): the enemy's own round, now the catcher's -- straight,
        // unguided, at the base's enemies. Its speed, damage and size are the caught round's.
        new() { Id = "reflect", AtPlayers = false, Pad = 3f, Sweep = 6f, Burst = 0.25, Look = ShotLook.Ball },
        // THE WARDEN'S PROXIMITY FLAK (kits_v2's card): it bursts 70 u off a hostile hull (or at its range), and
        // everything within 70 u of the burst takes the round; a boss x0.75
        new() { Id = "flak", AtPlayers = false, Pad = 3f, Sweep = 6f, Look = ShotLook.Bullet,
                Fuse = 70f, Resists = Tag.Boss, ResistShare = 0.75 },
        // THE FREIGHTER'S SPOTTER ROUND (kits 6b): a shell whose hit paints what it strikes for paint_time
        // (5 s), the target its sentries take first and Time on target converges on
        new() { Id = "spotter", AtPlayers = false, Pad = 3f, Sweep = 6f, Look = ShotLook.Bullet, Paint = "paint_time" },
        // THE BASTION'S BUNKER BUSTER (kits 6b): one slow heavy round that stops on the first body -- x2 on
        // a boss or a structure, and a quarter of that through a pylon's shield (180 / 360 / 90)
        new() { Id = "buster", AtPlayers = false, Pad = 4f, Sweep = 6f, Burst = 0.4, Look = ShotLook.Ball,
                Versus = Tag.Boss | Tag.Structure, VersusMult = 2, Through = 0.25 },
    };

    public static ShotDef Of(int id) => All[id >= 0 && id < All.Length ? id : Shell];

    // A FIRED SHOT'S OWN DAMAGE SOURCE. A ship ignores a repeat from the same source NAME inside
    // PlayerShip's 0.52 s gap -- a rule written for an ONGOING source (a sweeping beam, a ram, an
    // area tick), where one name has to cover every tick of one thing. A PROJECTILE is not
    // ongoing: it is a body that strikes once and ends, so three seekers of one volley are three
    // sources, not one. They used to share the weapon's name and a hull felt only the first of
    // them; the Drake numbered its seven scrap pieces by hand to dodge it, which is this fix
    // written once per weapon and forgotten by the next one. The weapon still says what it IS
    // ("boss:missiles", DamageSource in Combat.cs); the "#n" after it is this body's own, handed
    // out here so no caller can forget it. A beam, a ram or an area tick never comes through
    // Combat.Fire and keeps the single shared name it needs. Host-side only: HitSource is not on
    // the wire (Hub.NetShot) and a guest's copy is Cosmetic and damages nothing.
    private static int _fired;
    public static string SourceKey(string family) => family == null ? null : family + "#" + (++_fired);
    // with the world (Combat.Clear), or a second session in one process carries the first's count
    public static void ResetSources() => _fired = 0;

    // THE PATH TEST every flying thing shares: walk `from` to `to` in steps no longer than
    // `step` (0 = just the end), and hand each point to `at` until it says it struck something.
    // A step longer than a target is wide is how a shot passes through it between frames.
    public static bool Sweep(Vector2 from, Vector2 to, float step, System.Func<Vector2, bool> at)
    {
        int n = step > 0 ? Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / step)) : 1;
        for (int i = 1; i <= n; i++)
            if (at(from.Lerp(to, (float)i / n))) return true;
        return false;
    }
}

public partial class Shot : Node2D, IHittable, ITagged
{
    public int Kind = Shots.Shell;
    public ShotDef Def => Shots.Of(Kind);
    public Tag Tags => Def.Tags;

    public Vector2 Dir;
    public float Speed, Range, Radius, Size = 1f;
    public double Damage;
    public bool Cosmetic;                  // a guest's copy: it flies and bursts, and damages nothing
    public int TargetId;                   // 0 = unguided
    public float TurnRate;                 // rad/s its nose may turn toward the target
    public PlayerShip Source;              // who fired it (the host's copy): a hit is their combat
    public string HitSource;               // family#body (Shots.SourceKey): its OWN source, so a volley is never one
    public int Variant;                    // scrap: which jagged shape
    public float Lead;                     // a guest's copy starts this far into the flight (the round trip)
    public double Hull;                    // what is left of its body's hull (host): 0 falls to the first blow
    public int? Stops;                     // the firer's own count of bodies (ShotDef.Stops); null = its row's

    private float _flown, _puffCd;
    private bool _spent;
    private double _burnt;
    private readonly List<(Vector2 p, float age)> _smoke = new();
    private readonly HashSet<IHittable> _struck = new();   // every body this one has struck: each is struck once
    public const float SmokeLife = 1.6f, PuffEvery = 0.03f;

    // ── as a target: a seeker falls to one hit, a Hulled body is worn down ───
    public int NetId { get; set; }
    public const float BodyRadius = 8f;                 // its hit radius at size 1
    public float HitRadius => BodyRadius * Size;
    public bool Alive => !_spent;
    // Picked like any hull when it IS one (Tag.Hulled); a Missile never is
    public bool Selectable => (Def.Tags & Tag.Hulled) != 0;
    public string Label => Def.Label ?? $"#{NetId}";
    public static int Intercepted;                       // brought down (host), for the record
    // What it is, in the words the rest of the game uses. The rows carry the truth; these are how
    // a caller (and a check) asks without naming an index.
    public bool HostileFire => Def.AtPlayers;
    public bool Heavy => Def.Heavy;
    public bool IsMissile => Def.Look == ShotLook.Missile;
    // LURED (Hub.NetDecoy, on every peer): it steers for this point instead of its target, and bursts
    // there, harmlessly, once within `_catch` of it. Sticky.
    public bool Decoyable => Def.Decoyable;
    public Vector2? DecoyPoint { get; private set; }
    private float _catch;
    public void DecoyTo(Vector2 point, float catchRadius) { DecoyPoint = point; _catch = catchRadius; }
    public void TakeDamage(double d)
    {
        if (!Net.Sim || _spent) return;
        // A HULLED BODY WEARS DOWN: what it was launched with (Combat.Fire's hull), less every blow,
        // and it falls at nothing. A body launched with none -- every row but a Hulled one -- falls
        // to the first blow, as a seeker always has.
        if ((Hull -= d) > 0) return;
        Intercepted++;
        Intercept();
        Hub.I?.MissileDown(NetId);
    }
    public void Intercept() { if (!_spent) End(); Combat.Hostiles.Remove(this); }

    public override void _Ready()
    {
        ZIndex = 6; Rotation = Aim.Along(Dir);
        var d = Def;
        if (d.Sound != null) Sfx.ByName(d.Sound, Position);
        else if (d.Look == ShotLook.Bullet) Sfx.Cannon(Position);
        else if (d.Smoke) Sfx.Missile(GlobalPosition);          // self-propelled: a soft whoosh
        if (d.Interceptable && NetId != 0) Combat.Hostiles.Add(this);
        if (Lead > 0) { _flown = Mathf.Min(Speed * Lead, Range); Position += Dir * _flown; }
    }
    public override void _ExitTree() => Combat.Hostiles.Remove(this);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var d = Def;
        for (int i = _smoke.Count - 1; i >= 0; i--)
        {
            var s = _smoke[i]; s.age += dt;
            if (s.age >= SmokeLife) _smoke.RemoveAt(i); else _smoke[i] = s;
        }

        if (_spent)
        {
            _burnt += delta;
            // smoke lingers past the burst; everything else goes when its burst is done
            if (_burnt >= d.Burst && (!d.Smoke || _smoke.Count == 0)) QueueFree();
            QueueRedraw();
            return;
        }

        // HOMING IS CHOOSING: while its target is dark (Targeting.Hidden) a body does not follow
        // it -- it flies on down the heading it had, and still strikes whatever it touches (Strike
        // asks nobody's stealth); the target seen again, it homes again. A guest's copy reads the
        // same status bits, which the host sends every peer for every ship.
        if (d.Guided && TurnRate > 0 && GuideTo(d) is { } spot)
        {   // guided: the nose turns toward the target (or its lure) at TurnRate, and never snaps
            float want = (spot - GlobalPosition).Angle(), have = Dir.Angle();
            Dir = Dir.Rotated(Mathf.Clamp(Mathf.AngleDifference(have, want), -TurnRate * dt, TurnRate * dt));
            Rotation = Aim.Along(Dir);
        }

        float step = Speed * dt;
        var from = GlobalPosition;
        GlobalPosition += Dir * step; _flown += step;
        if (d.Smoke) { _puffCd -= dt; if (_puffCd <= 0) { _puffCd += PuffEvery; _smoke.Add((GlobalPosition - Dir * 8f, 0f)); } }

        Shots.Sweep(from, GlobalPosition, d.Sweep, p => d.Fuse > 0 ? Fused(p, d) : Strike(p, d));
        if (!_spent && DecoyPoint is { } lure && GlobalPosition.DistanceTo(lure) <= _catch) Intercept();   // burst on the lure
        if (!_spent && _flown >= Range) { if (d.Fuse > 0) Burst(GlobalPosition, d); else End(); }
        QueueRedraw();
    }
    // What a guided body steers for: its lure once it has one, else its target while it is seen.
    private Vector2? GuideTo(ShotDef d)
    {
        if (DecoyPoint is { } lure) return lure;
        if (TargetId != 0 && (d.AtPlayers ? Combat.PlayerById(TargetId) : Combat.ById(TargetId)) is { } tgt && !Targeting.Hidden(tgt))
            return tgt.Position;
        return null;
    }

    // What it touches at `p`, and what that costs. True when it has ENDED: it struck its last body
    // (Stops). A body already struck is passed through, so a sweep step inside one body is one blow.
    private bool Strike(Vector2 p, ShotDef d)
    {
        int stops = Stops ?? d.Stops;
        // one body at a time, the collection asked afresh after each blow: a blow may end a body,
        // and a body that ends leaves the list it was found in
        for (var h = Touching(p, d); h != null; h = Touching(p, d))
        {
            _struck.Add(h);
            if (d.MarkEveryPeer && h is Node2D seen) Popups.NoteImpact(seen, p);
            if (!Cosmetic && Net.Sim)
            {
                if (!d.MarkEveryPeer && h is Node2D struck) Popups.NoteImpact(struck, p);
                // a ship is told where the blow came from, for its shield; a hostile is dealt with
                // through the door (Dealt.Deal), the shot's own row naming the weapon. A prism turns a
                // reflectable round back instead (Reflected), and takes nothing of it.
                if (h is PlayerShip ps) { if (!Reflected(ps, p, d)) ps.Hit(Damage, p - Dir * 10f, HitSource); }
                else
                {
                    Dealt.Deal(h, TagExt.Is(h, d.Versus) ? Damage * d.VersusMult : Damage, IsInstanceValid(Source) ? Source : null, d.Id, d.Through);
                    if (d.Paint != null && IsInstanceValid(Source) && h.Alive) Source.PaintOn(h, Source.Stats[d.Paint]);
                }
            }
            if (stops > 0 && _struck.Count >= stops)
            {
                GlobalPosition = p;
                End();
                return true;
            }
        }
        return false;
    }

    // A FUSED ROUND (ShotDef.Fuse) at `p`: true once a hostile hull is within its fuse, and it has burst there.
    private bool Fused(Vector2 p, ShotDef d)
    {
        foreach (var h in Combat.Hostiles)
            if (Fuses(h, p, d)) { Burst(p, d); return true; }
        return false;
    }
    private static bool Fuses(IHittable h, Vector2 p, ShotDef d)
        => h != null && h.Alive && !TagExt.Is(h, Tag.Missile) && h.Covers(p, d.Fuse);
    // THE BURST: every hostile hull within the fuse of `p` takes the round, x ResistShare where it carries the
    // row's Resists tag, through the door (host); the flash is raised for every peer.
    private void Burst(Vector2 p, ShotDef d)
    {
        GlobalPosition = p;
        if (!Cosmetic && Net.Sim)
            foreach (var h in new List<IHittable>(Combat.Hostiles))
                if (Fuses(h, p, d))
                    Dealt.Deal(h, Damage * (TagExt.Is(h, d.Resists) ? d.ResistShare : 1), IsInstanceValid(Source) ? Source : null, d.Id);
        Fx.Raise(Fx.Burst, p, d.Fuse);
        End();
    }

    // A ROUND CAUGHT ON A GUARD (Prism.cs): fired back from `p` as the catcher's own Reflect round --
    // SQUARE along the guard at the band's x1.5, SLANT along the mirror at x1.0 -- unguided, with the
    // damage, size and what is left of the flight it had. Host. False: not caught; it lands.
    private bool Reflected(PlayerShip ps, Vector2 p, ShotDef d)
    {
        if (!d.Reflectable) return false;
        var split = Prism.Resolve(ps, Dir);
        if (!split.Caught) return false;
        Combat.Fire(Shots.Reflect, p, split.Out, Speed * Prism.Bands[split.Band].RoundSpeed, Mathf.Max(Range - _flown, 0f),
                    Damage, radius: Radius, source: ps, size: Size);
        return true;
    }

    // The first body at `p` it may strike and has not: a shell never takes a Missile out of the air --
    // that is point defence's job; a body with a hull of its own (Tag.Hulled) it strikes like any hull.
    private IHittable Touching(Vector2 p, ShotDef d)
    {
        foreach (var h in d.AtPlayers ? Combat.Players : Combat.Hostiles)
            if (h != null && h.Alive && !_struck.Contains(h) && !TagExt.Is(h, Tag.Missile) && h.Covers(p, d.Pad + Radius))
                return h;
        return null;
    }

    private void End()
    {
        _spent = true; _burnt = 0;
        Sfx.Impact(GlobalPosition);
        if (Def.Burst <= 0 && !Def.Smoke) QueueFree();
    }

    // a scrap piece's jagged outline, one of three, in its own frame (nose up)
    internal static readonly Vector2[][] Shards =
    {
        new Vector2[] { new(-7, -6), new(2, -9), new(8, -2), new(5, 7), new(-4, 8), new(-9, 1) },
        new Vector2[] { new(-5, -9), new(6, -7), new(9, 3), new(1, 9), new(-8, 5) },
        new Vector2[] { new(-9, -3), new(-2, -9), new(7, -5), new(8, 6), new(-3, 8) },
    };

    public override void _Draw()
    {
        var d = Def;
        // smoke is kept in world space: draw it relative to this node
        if (d.Smoke)
        {
            var inv = GlobalTransform.AffineInverse();
            foreach (var (p, age) in _smoke)
            {
                float k = age / SmokeLife;
                var smoke = d.Heavy ? new Color(0.45f, 0.43f, 0.42f) : new Color(0.86f, 0.86f, 0.88f);
                DrawCircle(inv * p, (d.Heavy ? 5f : 3.5f) + (d.Heavy ? 14f : 10f) * k,
                           new Color(smoke.R, smoke.G, smoke.B, 0.75f * (1f - k) * (1f - k * 0.3f)));
            }
        }

        if (_spent)
        {
            float k = (float)(_burnt / Mathf.Max(0.01f, (float)d.Burst));
            if (k >= 1) return;
            if (d.Look == ShotLook.Missile)
            {
                float big = d.Heavy ? 2f : 1f;
                DrawCircle(Vector2.Zero, (10f + 30f * k) * big, new Color(1f, 0.7f, 0.3f, 0.8f * (1f - k)));
                if (d.Heavy) DrawArc(Vector2.Zero, 70f * k, 0, Mathf.Tau, 40, new Color(1f, 0.9f, 0.6f, 0.7f * (1f - k)), 3f);
            }
            else   // a quick spray where it struck
                DrawCircle(Vector2.Zero, Radius * (1f + 1.5f * k), new Color(1f, 0.6f, 0.25f, 0.6f * (1 - k)));
            return;
        }

        switch (d.Look)
        {
            case ShotLook.Bullet:   // a bright slug with a short glow behind it
                DrawLine(new Vector2(0, 4f), new Vector2(0, 22f), new Color(1f, 0.75f, 0.35f, 0.35f), 3f);
                DrawRect(new Rect2(-1.8f, -5f, 3.6f, 9f), new Color(1f, 0.92f, 0.7f));
                DrawColoredPolygon(new[] { new Vector2(-1.8f, -5f), new Vector2(0, -8f), new Vector2(1.8f, -5f) }, new Color(1f, 0.85f, 0.5f));
                break;
            case ShotLook.Ball:     // a slow, glowing ball with a short tail: easy to see coming
                DrawLine(new Vector2(0, Radius * 0.5f), new Vector2(0, Radius * 3f), new Color(1f, 0.45f, 0.2f, 0.35f), Radius);
                DrawCircle(Vector2.Zero, Radius * 1.4f, new Color(1f, 0.45f, 0.15f, 0.3f));
                DrawCircle(Vector2.Zero, Radius, new Color(1f, 0.62f, 0.3f));
                DrawCircle(Vector2.Zero, Radius * 0.5f, new Color(1f, 0.92f, 0.75f));
                break;
            case ShotLook.Shard:    // a tumbling shard of hull plate, hot along its edge
            {
                DrawSetTransform(Vector2.Zero, _flown * 0.05f, Vector2.One * (Radius / 9f));
                var shard = Shards[Mathf.PosMod(Variant, Shards.Length)];
                DrawColoredPolygon(shard, new Color(0.42f, 0.30f, 0.24f));
                var loop = new Vector2[shard.Length + 1];
                shard.CopyTo(loop, 0); loop[^1] = shard[0];
                DrawPolyline(loop, new Color(1f, 0.55f, 0.25f, 0.9f), 1.5f);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                break;
            }
            case ShotLook.Missile when d.Heavy:
            {   // long and slim (6.4 x 32.5), a sharp nose, swept fins, a band and a seam
                var body = new Color(0.58f, 0.60f, 0.56f); var dark = new Color(0.30f, 0.31f, 0.29f);
                DrawRect(new Rect2(-3.2f, -12f, 6.4f, 26f), body);
                DrawColoredPolygon(new[] { new Vector2(-3.2f, -12f), new Vector2(0, -19.5f), new Vector2(3.2f, -12f) }, new Color(0.85f, 0.25f, 0.2f));
                DrawRect(new Rect2(-3.2f, -10f, 6.4f, 2f), new Color(0.85f, 0.25f, 0.2f));
                DrawLine(new Vector2(0, -8f), new Vector2(0, 11f), dark, 0.8f);
                DrawColoredPolygon(new[] { new Vector2(-3.2f, 6f), new Vector2(-7f, 13f), new Vector2(-3.2f, 12f) }, dark);
                DrawColoredPolygon(new[] { new Vector2(3.2f, 6f), new Vector2(7f, 13f), new Vector2(3.2f, 12f) }, dark);
                DrawCircle(new Vector2(0, 13.5f), 2.8f, new Color(1f, 0.75f, 0.35f));
                break;
            }
            default:
            {   // a missile at its own size (a boss's are twice as big), a red tip on every one
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One * Size);
                DrawRect(new Rect2(-2.2f, -9f, 4.4f, 18f), new Color(0.85f, 0.85f, 0.8f));
                DrawColoredPolygon(new[] { new Vector2(-2.2f, -9f), new Vector2(0, -13f), new Vector2(2.2f, -9f) }, new Color(0.9f, 0.2f, 0.18f));
                DrawCircle(new Vector2(0, 9f), 2.4f, new Color(1f, 0.7f, 0.3f));
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                break;
            }
        }
    }
}
