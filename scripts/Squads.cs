using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// SQUADS -- raiders that fly, commit and fight as one (raids v2, docs/plans/raids_squads_adds.md §1).
//
// REPLACED, in Raider.cs: `Patrol`, a serial that was the only link between raiders once they had
// spawned; `Station` / `Circuit` / `_orbit` / `Circle()` / `Choose()`; the per-target rank over
// `Posts[]`, which wrapped after three so a fourth light stacked on the first; each raider's own
// boost trigger (`_boostUsed` at BoostAtMine); the heavy's wait at the map's edge (`EdgeSpot`,
// `HeavyBoostStop`) and both its pin gates; station keeping at Max(top, d*12), which threw a latched
// light 240 u in one frame after a warp; and a boss's escorts (`Escort()`, `IsEscort`, the shiver),
// which are a boss fight's squad wave 1 now (Waves.All "bounty_adds").
//
// A NEW DOCTRINE IS A ROW of Squads.All, reached by id. A row must fill in:
//   Id          what a WaveDef names
//   Detect      0: take a target at once. >0: circle Station until something is this close to the anchor
//   Pick        Nearest, or Loneliest (fewest of its friends within Crowd, then nearest)
//   Prefer      a tag taken first whenever one is up (Tag.None: none)
//   Spread      prefer a target no other squad of its wave holds, if within 1.5x the nearest
//   CatchUp     a member off its slot flies at up to this x its own cruise
//   CommitAt    0: the smallest commit range among its pinners (every member's, with none)
//   Leash       a target further than this x CommitAt from the squad's centre means re-form
//   Reboost     seconds from one burn to the next: a re-commit inside it closes at cruise
//   Front/Rear  posts, degrees off the target's bow: a pinner takes Front, a standoff Rear
//   LaserNeedsPin / MissileNeedsPin   a standoff member holds that weapon until the target is pinned
//
// THE CONTRACT: ISquadMember is all a squad reads of what flies in it.
// AUTHORITY: host only. A squad is bookkeeping; what a guest sees of it is two bits and its id in
// the raider packet (Raider.NetFlags).
// ─────────────────────────────────────────────────────────────────────────────
public enum SquadPick { Nearest, Loneliest }
public enum SquadPhase { Hold, Approach, Commit }

public sealed class SquadDoctrine
{
    public string Id;
    public float Detect;
    public SquadPick Pick;
    public Tag Prefer;
    public bool Spread;
    public float CatchUp = 1.5f;
    public float CommitAt;
    public float Leash = 1.5f;
    public double Reboost = 10;
    public float[] Front = { 0f, -90f, 90f, -45f, 45f, -135f, 135f, -22.5f, 22.5f, -67.5f, 67.5f, -112.5f, 112.5f };
    public float[] Rear = { 180f, -155f, 155f, -130f, 130f };
    public bool LaserNeedsPin;
    public bool MissileNeedsPin = true;
}

// WHAT A SQUAD READS OF A MEMBER.
public interface ISquadMember
{
    Vector2 Position { get; }
    bool Alive { get; }
    int NetId { get; }
    bool Latched { get; }
    bool Pins { get; }            // its row puts a status on what it holds: it takes a Front post
    float Cruise { get; }         // its own cruise, x agility
    float Burn { get; }           // its boost, x agility
    float CommitRange { get; }    // how far out its own burn starts (the burn's length plus its reach)
    float Hold { get; }           // how far off the hull it posts
}

public static class Squads
{
    public static readonly SquadDoctrine Lone = new() { Id = "lone" };
    public static readonly SquadDoctrine Patrol = new() { Id = "patrol", Detect = 2000f };
    public static readonly SquadDoctrine Gank = new() { Id = "gank", Pick = SquadPick.Loneliest, Prefer = Tag.Player, Spread = true };
    public static readonly SquadDoctrine[] All = { Lone, Patrol, Gank };

    // FOR A CREW NOT YET BUILT (Raids' FormFor placement): the pace of its slowest, and its commit
    // range -- the smallest among its pinners, or among all of them with none.
    public static float PaceOf(IEnumerable<EnemyDef> crew, double agility = 1) =>
        crew.Select(d => d.Cruise * (float)agility).DefaultIfEmpty(0f).Min();
    public static float CommitOf(IEnumerable<EnemyDef> crew)
    {
        var all = crew.ToList();
        var pin = all.Where(d => d.Cc != null).ToList();
        return (pin.Count > 0 ? pin : all).Select(d => d.Cruise * d.BoostMult * (float)d.BoostTime + d.Reach).DefaultIfEmpty(0f).Min();
    }

    // THE PINNERS ON A TARGET RIGHT NOW: how many hold it with a status, and how much of their
    // ROWS' hull is left on them (Def.Hull x Hp / MaxHull -- a level scales hull and the pilot's
    // guns alike, so the row's figure is the strip at every level). A boss's escape floor reads it.
    public static (int count, double hull) PinnersOn(Hub hub, Node2D target)
    {
        int n = 0; double hull = 0;
        if (hub == null || target == null) return (0, 0);
        foreach (var r in hub.Raiders)
            if (r.Alive && r.Latched && r.Pins && ReferenceEquals(r.Target, target))
            { n++; hull += r.Def.Hull * Math.Max(0, r.Hp) / Math.Max(1e-9, r.MaxHull); }
        return (n, hull);
    }
}

// THE POST BOOK: who holds which post on which target, across every squad. A post is sticky -- held
// until its holder dies or its squad re-forms -- so nobody reshuffles when a mate falls.
public sealed class PostBook
{
    private readonly Dictionary<(Node2D target, bool rear), List<ISquadMember>> _held = new();
    public int Take(Node2D target, bool rear, int count, ISquadMember m)
    {
        if (!_held.TryGetValue((target, rear), out var list)) _held[(target, rear)] = list = new List<ISquadMember>();
        for (int k = 0; k < list.Count; k++)
            if (list[k] == null) { list[k] = m; return k; }
        list.Add(m);
        return (list.Count - 1) % Math.Max(1, count);
    }
    public void Release(ISquadMember m)
    {
        foreach (var list in _held.Values)
            for (int k = 0; k < list.Count; k++) if (ReferenceEquals(list[k], m)) list[k] = null;
        foreach (var key in _held.Where(kv => kv.Value.All(x => x == null)).Select(kv => kv.Key).ToList()) _held.Remove(key);
    }
    public int Held => _held.Values.Sum(l => l.Count(x => x != null));
}

public sealed class Squad
{
    public const float CircleSpeed = 100f;     // a squad circling its ring
    public const float PerimeterR = 1800f;     // the base's own ring, the default Circuit
    public const float Crowd = 800f;           // Loneliest: friends within this count against a target
    public readonly int Id, Wave;
    public readonly SquadDoctrine Doctrine;
    private readonly PostBook _book;
    private readonly IReadOnlyList<Squad> _all;
    private readonly List<ISquadMember> _members = new();
    private readonly Dictionary<ISquadMember, Vector2> _slot = new();
    private readonly Dictionary<ISquadMember, float> _post = new();
    public Squad(int id, int wave, SquadDoctrine doctrine, PostBook book, IReadOnlyList<Squad> all)
    { Id = id; Wave = wave; Doctrine = doctrine ?? Squads.Lone; _book = book; _all = all; }

    public IReadOnlyList<ISquadMember> Members => _members;
    public bool Empty => _members.Count == 0;
    public Vector2 Anchor;
    public float Heading;                       // the squad frame's rotation: -Y of a slot points along it
    public SquadPhase Phase { get; private set; } = SquadPhase.Hold;
    public Node2D Target { get; private set; }
    public Node2D Quarry;
    // A CALL (Raider.Call; Taunt): a timed target override. While it is up the squad goes for the
    // caller whatever it was after; at the lapse it picks again, its Quarry first. Quarry is untouched.
    public Node2D CalledBy => _callLeft > 0 && Raider.Up(_call) ? _call : null;
    private Node2D _call;
    private double _callLeft;
    public void Call(Node2D by, double seconds)
    {
        if (!Raider.Up(by) || seconds <= 0) return;
        _call = by; _callLeft = seconds;
        if (!ReferenceEquals(Target, by)) { Target = by; Reform(); }
    }
    public Vector2 Station;
    public float Circuit = PerimeterR;
    public bool Burning { get; private set; }  // this commit is on the boost (Reboost says whether)
    public double Left { get; private set; }   // time on target: seconds until every member is posted
    public float Moving { get; private set; }  // how fast the anchor moved this frame
    private Node2D _dark;
    private Lead _track;
    private float _orbit = float.NaN;
    private double _sinceBurn = double.MaxValue;

    public void Join(ISquadMember m)
    {
        if (_members.Contains(m)) return;
        _members.Add(m);
        _slot[m] = (m.Position - Anchor).Rotated(-Heading);
    }
    public void Leave(ISquadMember m)
    {
        _members.Remove(m); _slot.Remove(m); _post.Remove(m);
        _book.Release(m);
    }

    // THE LEAD: the first standoff member, else the lowest NetId. The anchor is virtual, so a lead
    // falling moves nothing: its slot stays empty.
    public ISquadMember Leader =>
        _members.Where(m => !m.Pins).OrderBy(m => m.NetId).FirstOrDefault() ?? _members.OrderBy(m => m.NetId).FirstOrDefault();
    public float Pace => _members.Count == 0 ? 0f : _members.Min(m => m.Cruise);
    public float CommitAt
    {
        get
        {
            if (Doctrine.CommitAt > 0) return Doctrine.CommitAt;
            var pin = _members.Where(m => m.Pins).ToList();
            return (pin.Count > 0 ? pin : _members).Select(m => m.CommitRange).DefaultIfEmpty(0f).Min();
        }
    }
    public Vector2 Centre => _members.Count == 0 ? Anchor : _members.Aggregate(Vector2.Zero, (a, m) => a + m.Position) / _members.Count;
    public Vector2 SlotOf(ISquadMember m) => Anchor + _slot.GetValueOrDefault(m).Rotated(Heading);
    public bool Posted(ISquadMember m) => Phase == SquadPhase.Commit && _post.ContainsKey(m) && Target != null;
    public float PostAngle(ISquadMember m) => _post.GetValueOrDefault(m);
    public Vector2 PostOf(ISquadMember m)
    {
        var dir = Vector2.Up.Rotated(Target.Rotation + Mathf.DegToRad(_post.GetValueOrDefault(m)));
        return Target.Position + dir * (Raider.Extent(Target, dir) + m.Hold);
    }
    public float Top(ISquadMember m) => Burning ? m.Burn : m.Cruise;

    private static bool Live(ISquadMember m) => (m is not GodotObject g || GodotObject.IsInstanceValid(g)) && m.Alive;

    public void Tick(Hub hub, double delta)
    {
        float dt = (float)delta;
        for (int i = _members.Count - 1; i >= 0; i--) if (!Live(_members[i])) Leave(_members[i]);
        if (Empty) return;
        if (_sinceBurn < double.MaxValue) _sinceBurn += delta;
        Moving = 0f;
        if (_dark != null && !GodotObject.IsInstanceValid(_dark)) _dark = null;
        if (_call != null && ((_callLeft -= delta) <= 0 || !Raider.Up(_call)))
        {   // the call is over: back to what it would have taken
            bool held = ReferenceEquals(Target, _call);
            _call = null; _callLeft = 0;
            if (held) { var next = Pick(hub); if (!ReferenceEquals(next, Target)) { Target = next; Reform(); } }
        }
        if (!Raider.Up(Target))
        {   // it died, hid or left: re-pick, keeping what went dark to take back the moment it is seen
            if (Target != null && GodotObject.IsInstanceValid(Target) && Targeting.Hidden(Target)) _dark = Target;
            var next = Pick(hub);
            if (!ReferenceEquals(next, Target)) { Target = next; Reform(); }
        }
        else if (_call == null && _dark != null && !ReferenceEquals(_dark, Target) && Raider.Up(_dark)) { Target = _dark; Reform(); }
        if (ReferenceEquals(_dark, Target)) _dark = null;
        if (Target == null) { Circle(dt); return; }
        _track.Watch(Target.Position, delta);
        if (_track.Jumped && Phase == SquadPhase.Commit) Reform();       // a warp: the posts mean nothing now
        if (Phase != SquadPhase.Commit)
        {
            Phase = SquadPhase.Approach;
            Approach(dt);
            if (Anchor.DistanceTo(Target.Position) <= CommitAt) Commit();
            else return;
        }
        if (Raider.Gap(Centre, Target) > Doctrine.Leash * CommitAt) { Reform(); return; }   // it fled
        double left = 0;
        foreach (var m in _members) left = Math.Max(left, m.Position.DistanceTo(PostOf(m)) / Math.Max(1f, Top(m)));
        Left = left;
    }

    // WHAT IT GOES FOR: its caller while a call is up; its quarry while there is one; else what it can see (within Detect of the
    // anchor, for a squad that holds a ring), the preferred tag first, spread across its wave, and
    // the nearest or the loneliest of those.
    private Node2D Pick(Hub hub)
    {
        if (CalledBy is { } caller) return caller;
        if (Raider.Up(Quarry)) return Quarry;
        if (hub == null) return null;
        var all = hub.RaiderTargets().ToList();
        var seen = all.Where(Raider.Up).ToList();
        if (Doctrine.Detect > 0) seen = seen.Where(t => t.Position.DistanceTo(Anchor) <= Doctrine.Detect).ToList();
        if (seen.Count == 0) return null;
        if (Doctrine.Prefer != Tag.None && seen.Any(t => TagExt.Is(t, Doctrine.Prefer))) seen = seen.Where(t => TagExt.Is(t, Doctrine.Prefer)).ToList();
        if (Doctrine.Spread)
        {
            float near = seen.Min(t => t.Position.DistanceTo(Anchor));
            var free = seen.Where(t => t.Position.DistanceTo(Anchor) <= 1.5f * near
                                       && !_all.Any(o => o != this && Wave != 0 && o.Wave == Wave && ReferenceEquals(o.Target, t))).ToList();
            if (free.Count > 0) seen = free;
        }
        return Doctrine.Pick == SquadPick.Loneliest
            ? seen.OrderBy(t => all.Count(o => !ReferenceEquals(o, t) && o.Position.DistanceTo(t.Position) <= Crowd))
                  .ThenBy(t => t.Position.DistanceTo(Anchor)).First()
            : seen.OrderBy(t => t.Position.DistanceTo(Anchor)).First();
    }

    // NOTHING TO FIGHT: a squad that holds a ring circles it; one that does not sits where it is.
    private void Circle(float dt)
    {
        Phase = SquadPhase.Hold;
        if (Doctrine.Detect <= 0) return;
        if (float.IsNaN(_orbit)) _orbit = (Anchor - Station).Angle();
        _orbit += CircleSpeed / Mathf.Max(1f, Circuit) * dt;
        var spot = Station + Vector2.Right.Rotated(_orbit) * Circuit;
        var step = spot - Anchor;
        var was = Anchor;
        Anchor = Anchor.MoveToward(spot, CircleSpeed * Doctrine.CatchUp * dt);        // catches its moving spot
        if (step.Length() > 1f) Heading = Mathf.LerpAngle(Heading, Aim.Along(step), Mathf.Clamp(4f * dt, 0f, 1f));
        Moving = dt > 0 ? was.DistanceTo(Anchor) / dt : 0f;
    }

    // IN FORMATION: the anchor at the slowest member's pace, turning only as fast as the outermost
    // slot can keep up with at CatchUp -- so the shape holds through the turn.
    private void Approach(float dt)
    {
        float pace = Pace;
        float radius = _slot.Values.Select(v => v.Length()).DefaultIfEmpty(0f).Max();
        float turn = radius < 1f ? Mathf.Tau * 60f : (Doctrine.CatchUp - 1f) * pace / radius;
        Heading = Mathf.RotateToward(Heading, Aim.Face(Anchor, Target.Position), turn * dt);
        Anchor += Vector2.Up.Rotated(Heading) * pace * dt;
        Moving = pace;
    }

    // COMMIT: every member takes the lowest free post of its way, and the burn is shared -- all
    // arrive together (Left), on the boost unless the last burn was inside Reboost.
    private void Commit()
    {
        Phase = SquadPhase.Commit;
        foreach (var m in _members) _book.Release(m);
        _post.Clear();
        Burning = _sinceBurn >= Doctrine.Reboost;
        if (Burning) _sinceBurn = 0;
        foreach (var m in _members.OrderBy(m => m.NetId))
        {
            var rows = m.Pins ? Doctrine.Front : Doctrine.Rear;
            _post[m] = rows[_book.Take(Target, !m.Pins, rows.Length, m) % rows.Length];
        }
    }

    // RE-FORM at the survivors' centre: posts released, back to the approach (or the ring).
    private void Reform()
    {
        foreach (var m in _members) _book.Release(m);
        _post.Clear();
        Anchor = Centre;
        Phase = Target != null ? SquadPhase.Approach : SquadPhase.Hold;
        if (Target != null) Heading = Aim.Face(Anchor, Target.Position);
    }
}
