using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE RAID DIRECTOR — the clock, the one wave builder, and calling a hunt off.
//
// Cut out of Hub.cs: PendingRaidLevel, RaidDelay, _raidIn/_raidLevel, TickRaid, StartRaid,
// SpawnPatrol, HuntWave, CallOff, _patrols and _waveRoll. The world host owns one of these
// (Hub._raids) and keeps the faces the rest of the game already calls -- Hub.SpawnPatrol,
// Hub.HuntWave, Hub.CallOff -- each one line onto this. The threat maths went to Waves.
//
// THERE IS ONE BUILDER (Send) AND IT IS THE ONLY PATH. It reads a row of Waves.All: how many
// squads, where each forms up, what is in it, how strong, how tough and how quick. Nothing about
// a wave is written here -- a new wave is a row in Waves.All, and nothing at all in this file.
//
// IT OWNS THE SQUADS (Squads.cs): the live list, ticked here ahead of the raiders' own _Process
// (Hub._Process calls Tick first), and the post book every squad takes its posts from.
//
// AUTHORITY: the host alone builds waves. Every raider it mints is replicated by Hub.Spawn.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class Raids
{
    private readonly Hub _hub;
    public Raids(Hub hub) { _hub = hub; }

    // A RAID THE ARENA OWES THIS BASE: set when a mission is lost, spent by the next world that
    // loads. Static, because it has to survive the scene change home (was Hub.PendingRaidLevel).
    public static int Pending;
    // ...and it is held this long once that world is up, so every guest's world has loaded
    // before the raiders appear.
    public const double Delay = 3.0;
    private double _in = -1;
    private int _level;
    private int _squads;          // the serial every squad is numbered from (Squad.Id)
    private int _waves;           // ...and every wave (Squad.Wave: what Spread keeps apart)
    private int _roll;            // which row of Enemies.All the next squad draws
    private readonly List<Squad> _live = new();
    public readonly PostBook Posts = new();
    public IReadOnlyList<Squad> LiveSquads => _live;

    public void Owed(int level) { _level = level; _in = Delay; }
    public void Tick(double delta)
    {
        foreach (var s in _live.ToList()) s.Tick(_hub, delta);
        _live.RemoveAll(s => s.Empty);                  // a squad with nobody left is gone
        TickLanes(delta);
        if (_in < 0) return;
        if ((_in -= delta) <= 0) { _in = -1; Raid(_level); }
    }

    // ── THE LANES' OWN CLOCK ────────────────────────────────────────────────
    // One lane cut at a time, never one already cut, only at home (the arena has no lanes), and
    // only once this base has something to lose -- a pilot who has never flown a bounty is not
    // blockaded. The SCHEDULE is Lanes.FirstBlockade / BlockadeEvery, the lanes' own clock, as an
    // escort's is Economy.EscortFirstWave / EscortWaveEvery; WHAT arrives is a row of Waves.All
    // and nothing in this file. Several stand at once only because the pilot left the first one
    // standing, which is the point of them.
    private double _laneIn = Lanes.FirstBlockade;
    private int _blockades;
    private void TickLanes(double delta)
    {
        if (Hub.InArena || Missions.HighestBeaten(Missions.Bounty) < Unlocks.Boss(Opens.Raids)) return;
        if ((_laneIn -= delta) > 0) return;
        _laneIn = Lanes.BlockadeEvery;
        int lane = Lanes.NextOpen(_hub);
        if (lane >= 0) Blockade(lane);
    }

    // A LANE CUT: one squad on its stand, and one more for each pilot past the first, at the base
    // owner's own standing. THE ONE DOOR -- the clock above comes through it, and so does anything
    // else that wants a lane cut on purpose (Hub.Blockade).
    public void Blockade(int lane)
    {
        if (!Net.IsHost || lane < 0 || lane >= Lanes.All.Length) return;
        var at = Lanes.Stand(lane);
        Send(WaveTrigger.Blockade, new WaveBrief { Pilots = _hub.PartySize, Index = _blockades++,
             Level = System.Math.Max(1, Missions.HighestBeaten(Missions.Bounty)), Origin = at, Anchor = at });
    }

    // ── the five moments a wave comes from ──────────────────────────────────
    public void Raid(int level) =>
        Send(WaveTrigger.Failed, new WaveBrief { Pilots = _hub.PartySize, Level = level, Origin = Hub.BasePos });

    public int Patrol(Vector2 at, double scale = 1) =>
        Send(WaveTrigger.Called, new WaveBrief { Pilots = _hub.PartySize, Scale = scale, Origin = Hub.BasePos, Anchor = at });

    // A MISSION'S TARGET DEFENDING ITSELF: wave `wave` of its own clock, formed up round `at`, at
    // the mission's level. It is the same builder an escort's hunters come from -- what differs is
    // the ROW (Waves.All: "siege"), which brings them at full hull.
    public void Garrison(int level, Vector2 at, int wave) =>
        Send(WaveTrigger.Garrison, new WaveBrief { Pilots = _hub.PartySize, Index = wave, Level = level, Origin = at,
                                                   Mission = Missions.KindOf(Missions.Kind).Id });

    public void Hunt(Node2D quarry, int wave)
    {
        if (quarry == null) return;
        var (level, toughness) = Waves.Standing(_hub.Ships);
        // the BOSS ladder, said outright: an escort's threat has always been judged by how far up
        // the bounties the base owner has got, and the raids climb a ladder of their own
        double threat = Waves.EscortThreat((quarry as IRaidTarget)?.Payout ?? 0, level, toughness, Missions.HighestBeaten(Missions.Bounty), wave);
        Send(WaveTrigger.Hunt, new WaveBrief
        {
            // its level is its threat's: Missions.Level is the arena's, which a hunt at home never reads
            Pilots = _hub.PartySize, Index = wave, Level = System.Math.Max(1, (int)threat), Quarry = quarry,
            Threat = threat,
            Origin = Hub.BasePos, Anchor = Raider.EdgeSpot(quarry.Position),
        });
    }

    // The hunt is over (the quarry reached the portal, or was lost): its hunters withdraw -- gone,
    // not shot down.
    public void CallOff(Node2D quarry)
    {
        if (!Net.IsHost) return;
        foreach (var r in _hub.Raiders.Where(r => r.Quarry == quarry).ToList())
            _hub.Down(Spawns.Raider, r, burst: false, (float)r.Hp);
    }

    // A SQUAD, FORMED: numbered, on the book, in the live list. `wave` groups the squads one Send
    // made (0: a squad of its own).
    public Squad Form(SquadDoctrine doctrine, Vector2 at, int wave, float heading = 0f)
    {
        var s = new Squad(++_squads, wave, doctrine, Posts, _live) { Anchor = at, Station = at, Heading = heading };
        _live.Add(s);
        return s;
    }
    // EVERY RAIDER FLIES IN A SQUAD: the one it was built for, or a squad of its own (doctrine
    // "lone": the nearest target, no preference) -- a stray spawn, a test's, a practice foe's.
    public void Enlist(Raider r, Squad squad)
    {
        if (r == null) return;
        squad ??= Form(Squads.Lone, r.Position, 0);
        r.Squad = squad;
        squad.Join(r);
    }

    // ── THE ONE BUILDER ─────────────────────────────────────────────────────
    // Squads, spread by the row's form; each squad draws one row of Enemies.All per way, and each
    // crew row takes the kind `Nth` on from that draw -- so a row asking for two kinds of pinner
    // gets two, which no wave could do before. Every squad flies its row's doctrine, from the
    // brief's Origin (its ring's centre), on the row's Hold if it names one. Returns the FIRST
    // squad's id, which is what Hub.SpawnPatrol has always handed back.
    private int Send(WaveTrigger trigger, WaveBrief b)
    {
        if (!Net.IsHost) return 0;
        var d = Waves.For(trigger, b);
        if (d?.Crew == null) return 0;
        int squads = System.Math.Max(1, d.Squads + d.SquadsPerPilot * System.Math.Max(0, b.Pilots - 1));
        double strength = d.Strength?.Invoke(b) ?? 1, agility = d.Agility?.Invoke(b) ?? 1;
        int first = 0, wave = ++_waves;
        for (int s = 0; s < squads; s++)
        {
            int roll = _roll++;
            var at = Waves.SquadAt(d, b, s, squads);
            var squad = Form(d.Doctrine ?? Squads.Patrol, at, wave);
            if (s == 0) first = squad.Id;
            squad.Quarry = b.Quarry;
            // ...and WHERE IT WAITS with nothing to fight: a ring of the row's Hold round its own
            // spot, or the base's perimeter round the brief's Origin (every home wave's is the base)
            squad.Station = d.Hold > 0 ? at : b.Origin;
            squad.Circuit = d.Hold > 0 ? d.Hold : Squad.PerimeterR;
            foreach (var c in d.Crew)
            {
                int n = c.Count(b), kind = Waves.KindOf(c, roll);
                for (int i = 0; i < n; i++)
                {
                    var r = _hub.SpawnRaider(at + c.Slot(i, n), kind, squad, strength, d.HullShare?.Invoke(b) ?? 1);
                    if (r == null) continue;
                    r.Agility = agility; r.Level = b.Level;
                }
            }
        }
        return first;
    }
}
