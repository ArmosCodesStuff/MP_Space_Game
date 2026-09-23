using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE PARTY'S VOTES — one consensus mechanic, one row per vote.
//
// REPLACED: READY (launch a mission) and RETURN (leave a won arena), which were the same four
// steps written twice in Hub.cs and deliberately forked -- "Kept apart from the launch READY so a
// launch-ready is never mistaken for a return-ready". Both were: a local toggle; a guest's request
// checked for its sender and for membership of the party; a host tally; and a move once every
// voter agrees. They differed only in WHO counts, WHEN it may be cast, what pressing it does
// besides counting, and what carrying it does -- which is what a row says. A THIRD VOTE IS A ROW,
// not another pair of RPCs.
//
// ON THE WIRE: one packet per vote (Hub.NetVote) carrying who voted, what each said, and the
// host's own two numbers. It goes to the whole session, as the mission packet the READY tally
// used to ride inside does -- the hub is the same node in both sectors, and a guest's button must
// read right wherever it is standing.
//
// AUTHORITY: the host's tally is the only one. A guest marks its own press so its button answers
// at once, and the host's next packet overwrites it.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class VoteDef
{
    public string Id;
    public Func<Hub, bool> Open;                   // may it be cast, and may it carry, right now
    public Func<Hub, IEnumerable<int>> Voters;     // whose agreement it needs
    public Action<Hub, bool> Cast;                 // what pressing it does besides counting (null: nothing)
    public Action<Hub> Carried;                    // the host's move once they all agree
}

public static class Votes
{
    // The index IS the vote on the wire (Hub.NetVote), so APPEND ONLY.
    public const int Launch = 0, Return = 1;

    public static readonly VoteDef[] All =
    {
        // READY. The party is everyone in the session AND every pilot whose place is held
        // (reconnecting): one who was not READY keeps the portal shut until it is back, or its
        // place lapses. Pressing it also flies you to where the portal will open (a manual key
        // cancels that).
        new() { Id = "launch",
                Open = h => h.PartySize > 0,
                Voters = h => h.PartyIds,
                Cast = (h, on) => { if (h.MyShip is { } me) me.AutopilotTo = on ? h.MissionPortalPos : null; },
                Carried = h => h.StartMission() },

        // RETURN, after a win only. Each pilot presses it when it has finished collecting, and the
        // whole party warps home once every pilot PRESENT has -- a held place does not block it.
        // A stasis pilot may press it too (it is a HUD button, not a ship order).
        new() { Id = "return",
                Open = h => h.MissionWon && h.PartySize > 0,
                Voters = h => h.Ships.Select(s => s.OwnerId),
                Carried = h => h.EnterSector(Hub.SectorKind.Home) },
    };

    // one of each for a world
    public static Vote[] Fresh()
    {
        var v = new Vote[All.Length];
        for (int i = 0; i < v.Length; i++) v[i] = new Vote(i);
        return v;
    }
}

// ONE VOTE, AS THIS PEER KNOWS IT. The host keeps the tally and works its own numbers out; a
// guest keeps the map the host last sent, its own press, and the host's two numbers.
public sealed class Vote
{
    public readonly int Index;
    public VoteDef Def => Votes.All[Index];
    private readonly Dictionary<int, bool> _said = new();
    private bool _mine;
    private int _ready, _total;
    public Vote(int index) { Index = index; }

    public bool Says(int id) => _said.TryGetValue(id, out var yes) && yes;
    public bool Mine => Net.IsHost ? Says(Net.LocalId) : _mine;
    public void Mark(bool on) { _mine = on; }                       // a guest's own press, before the host answers
    public void Set(int who, bool on) { _said[who] = on; }
    public void Forget(int who) { _said.Remove(who); }
    public void Rekey(int from, int to) { if (_said.Remove(from, out var yes)) _said[to] = yes; }
    public void Clear() { _said.Clear(); _mine = false; _ready = 0; _total = 0; }
    public void Guest(int total) { _said.Clear(); _mine = false; _ready = 0; _total = total; }

    public int Ready(Hub h) => Net.IsHost ? Def.Voters(h).Count(Says) : _ready;
    public int Total(Hub h) => Net.IsHost ? Def.Voters(h).Count() : _total;
    public bool Carried(Hub h)
    {
        if (!Def.Open(h)) return false;
        var who = Def.Voters(h).ToList();
        return who.Count > 0 && who.All(Says);
    }

    // RPCs carry int[], not bool[]
    public Variant[] Wire(Hub h)
    {
        var ids = _said.Keys.ToArray();
        return new Variant[] { Index, ids, ids.Select(i => _said[i] ? 1 : 0).ToArray(), Ready(h), Total(h) };
    }
    public void Apply(int[] ids, int[] said, int ready, int total)
    {
        _said.Clear();
        for (int i = 0; i < ids.Length && i < said.Length; i++) _said[ids[i]] = said[i] != 0;
        _ready = ready; _total = total;
        _mine = Says(Net.LocalId);                                  // the host's word on this peer's own press
    }
}
