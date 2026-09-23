using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// HOST-SPAWNED, REPLICATED THINGS — one mechanic, one row per kind.
//
// REPLACED: the same six methods written twice in Hub.cs --
//   raiders   SpawnRaider / NetRaiderSpawn / AddRaider / RaiderDown / NetRaiderGone / DropRaider
//   turrets   Drop / NetDeploy / AddDeployed / DeployedDown+DeployedTaken / NetDeployGone / DropDeployed
// Both did the same five things: mint an id from a NetIds space, build the thing and keep it in a
// list of its own, tell this world, guard a guest's copy against hearing twice about one it
// already has, and remove it -- with a burst or quietly. A THIRD KIND IS A ROW OF Spawns.All, not
// a third copy of those six: Hub.Spawn and Hub.Down are the only ways in, and Hub.NetSpawn and
// Hub.NetSpawnGone are the only two on the wire. Each carries a KIND INDEX and three numbers.
//
// A ROW MUST FILL IN:
//   Id      what it is called -- its nodes are named "<Id>_<net id>"
//   Space   the NetIds space its numbers come from
//   Burst   how wide a burst it leaves when it goes down loudly (0: none)
//   Bag     where the live ones are kept -- `k => new SpawnSet<YourNode>(k)`
//   Make    build one from a seed
//   Seed    what a joiner must be told to build the one that already exists
//   Gone    what the world must let go of when one dies (null: nothing holds it). Its `hp` is
//           the host's last word on the hull; NaN means nothing was said about it.
//
// AUTHORITY: only the host calls Hub.Spawn and Hub.Down. A guest builds and drops its copies from
// the two RPCs alone, and its copies damage nothing.
// ─────────────────────────────────────────────────────────────────────────────

// WHAT IS SAID ABOUT ONE OF THESE, at the spawn and on the wire. The three numbers are whatever
// its row makes of them: a raider's enemy row, its strength and its hull share; a turret's owner,
// its hull and its maximum.
public readonly struct SpawnSeed
{
    public readonly int NetId;
    public readonly Vector2 At;
    public readonly int N;
    public readonly double A, B;
    public SpawnSeed(int netId, Vector2 at, int n, double a, double b) => (NetId, At, N, A, B) = (netId, at, n, a, b);
}

public sealed class SpawnKind
{
    public string Id;
    public int Space;
    public float Burst;
    public Func<SpawnKind, SpawnSet> Bag;
    public Func<Hub, SpawnSeed, Node2D> Make;
    public Func<Node2D, SpawnSeed> Seed;
    public Action<Hub, Node2D, float> Gone;
}

// THE LIVE ONES OF ONE KIND. The typed half is SpawnSet<T>, so a caller reads
// IReadOnlyList<Raider> rather than a list of nodes; this half is what Hub's two RPCs reach,
// which know a kind only by its index.
public abstract class SpawnSet
{
    public readonly SpawnKind Kind;
    protected SpawnSet(SpawnKind kind) { Kind = kind; }
    public abstract IEnumerable<Node2D> Nodes { get; }
    public abstract Node2D Add(Hub hub, SpawnSeed seed);
    public abstract void Drop(Hub hub, Node2D what, bool burst, float hp);
    public Node2D Find(int id)
    {
        foreach (var n in Nodes) if (Kind.Seed(n).NetId == id) return n;
        return null;
    }
    public bool Has(int id) => Find(id) != null;
    public void DropById(Hub hub, int id, bool burst, float hp) { if (Find(id) is { } n) Drop(hub, n, burst, hp); }
    // everything of this kind, at once and locally -- a session change, where nothing is told and
    // nothing is said about any hull (raiders belong to whoever simulates them, and these have
    // stopped being ours; a hull of 0 here would pop a damage number for the whole hull on the way
    // out, because Raider._ExitTree reports the drop to HullWatch)
    public void DropAll(Hub hub, bool burst)
    {
        foreach (var n in new List<Node2D>(Nodes)) Drop(hub, n, burst, float.NaN);
    }
}

public sealed class SpawnSet<T> : SpawnSet where T : Node2D
{
    private readonly List<T> _live = new();
    public IReadOnlyList<T> Live => _live;
    public SpawnSet(SpawnKind kind) : base(kind) { }
    public override IEnumerable<Node2D> Nodes => _live;

    public override Node2D Add(Hub hub, SpawnSeed seed)
    {
        if (Kind.Make(hub, seed) is not T made) return null;
        made.Name = $"{Kind.Id}_{seed.NetId}";
        _live.Add(made); hub.AddChild(made);
        return made;
    }

    // It leaves the live list NOW, not when the queued free takes it out of the tree at the
    // frame's end: a turret that ticked later in the same frame used to acquire a dead raider again.
    public override void Drop(Hub hub, Node2D what, bool burst, float hp)
    {
        if (what is not T one || !_live.Contains(one)) return;
        if (burst && Kind.Burst > 0) Fx.Raise(Fx.Burst, one.Position, Kind.Burst);
        Kind.Gone?.Invoke(hub, one, hp);
        _live.Remove(one); one.QueueFree();
    }
}

public static class Spawns
{
    // The index IS the kind on the wire (Hub.NetSpawn), so APPEND ONLY.
    public const int Raider = 0, Turret = 1;

    public static readonly List<SpawnKind> All = new()
    {
        // RAIDERS -- enemy fighters, host-simulated, guests drawing them from the host's 10 Hz
        // state (Hub.SendWorldState). `Gone` carries the host's last word on the hull, because
        // the host removes a raider before that hull reaches anyone and a guest shows the blow
        // that finished it from this.
        new() { Id = "Raider", Space = NetIds.Enemy, Burst = 28f,
                Bag = k => new SpawnSet<Raider>(k),
                Make = (h, s) => new Raider { Hub = h, Kind = s.N, Strength = s.A, HullShare = s.B,
                                              NetId = s.NetId, Position = s.At },
                Seed = n => { var r = (Raider)n; return new SpawnSeed(r.NetId, r.Position, r.Kind, r.Strength, r.HullShare); },
                Gone = (h, n, hp) => { var r = (Raider)n; if (float.IsFinite(hp)) r.Hp = hp; h.LetGo(r); } },

        // THE TURRETS THE FREIGHTERS HAVE OUT. `Seed` carries the LIVE hull AND the maximum: the
        // join catch-up sends a turret as it stands, and a joiner that took the live figure for
        // the maximum too drew a half-dead turret with a full bar. Nothing else in the world holds
        // one (it is not a hostile), so it needs no `Gone`.
        new() { Id = "Deployed", Space = NetIds.Deployed, Burst = 22f,
                Bag = k => new SpawnSet<DeployedTurret>(k),
                Make = (h, s) => new DeployedTurret { NetId = s.NetId, OwnerId = s.N, Ship = h.ShipOf(s.N),
                                                      Position = s.At, Hp = s.A, MaxHp = s.B },
                Seed = n => { var t = (DeployedTurret)n; return new SpawnSeed(t.NetId, t.Position, t.OwnerId, t.Hp, t.MaxHp); } },
    };
}
