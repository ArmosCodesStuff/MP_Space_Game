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

    // ── A MISSION'S GARRISON, ON ITS ROW'S CLOCK (was Hub.TickArena's _garrisonT / _garrisonWaves) ──
    // A row with no Roster (a siege) is a wave sent every WaveEvery from FirstWave and forgotten. A
    // row with one (a bounty's adds) is SLOTS: dealt once, each squad of the deal a slot that comes
    // on its clock or on the boss's hull (Waves.SlotDue), keeps its kinds, and -- wiped -- comes back
    // WaveEvery later, paying nothing. Held stops the clock (the harness tests a boss alone with it).
    public static bool Held;
    private double _gT;
    private int _gWaves;
    private sealed class Slot { public int Heavies, Lights; public double Due; public bool First = true; public Squad Squad; }
    private List<Slot> _slots;
    public void RestartGarrison() { _gT = 0; _gWaves = 0; _slots = null; }
    public void TickGarrison(double delta)
    {
        if (!Net.IsHost || Held) return;
        var kind = Missions.KindOf(Missions.Kind);
        if (kind.WaveEvery <= 0) return;                       // its row brings none
        _gT += delta;
        var b = GarrisonBrief(0);
        var row = Waves.For(WaveTrigger.Garrison, b);
        if (row?.Roster == null)
        {
            if (_gT >= kind.FirstWave + _gWaves * kind.WaveEvery) Garrison(Missions.Level, Hub.ArenaCentre, _gWaves++);
            return;
        }
        _slots ??= Waves.Deal(row.Roster(b), Missions.ForLevel(b.Level).AddsFloor)
                        .Select(q => new Slot { Heavies = q.heavies, Lights = q.lights }).ToList();
        var boss = _hub.Boss;
        double hull = GodotObject.IsInstanceValid(boss) && boss.MaxHp > 0 ? boss.Hp / boss.MaxHp : 1;
        for (int k = 0; k < _slots.Count; k++)
        {
            var s = _slots[k];
            if (s.Squad != null)
            {
                if (!s.Squad.Empty) continue;
                s.Squad = null; s.First = false; s.Due = _gT + kind.WaveEvery;   // wiped: back, the same kinds
                continue;
            }
            bool due = s.First ? Waves.SlotDue(k, _slots.Count, _gT - kind.FirstWave, kind.WaveEvery, hull, true) : _gT >= s.Due;
            if (!due) continue;
            var sb = GarrisonBrief(k); sb.Heavies = s.Heavies; sb.Lights = s.Lights;
            int id = Send(WaveTrigger.Garrison, sb, roll: k, pays: s.First);
            s.Squad = _live.FirstOrDefault(q => q.Id == id);
        }
    }
    // A garrison's brief: the arena's mission and level, formed on the party's centre, the fan
    // measured along the line to the boss (or to the arena's centre)
    private WaveBrief GarrisonBrief(int index)
    {
        var ships = _hub.Ships.Where(GodotObject.IsInstanceValid).ToList();
        var centre = ships.Count > 0 ? ships.Aggregate(Vector2.Zero, (a, s) => a + s.Position) / ships.Count : Hub.ArenaCentre;
        var boss = _hub.Boss;
        var toward = GodotObject.IsInstanceValid(boss) ? boss.Position : Hub.ArenaCentre;
        if (toward.DistanceTo(centre) < 1f) toward = centre + Vector2.Up;
        return new WaveBrief { Pilots = _hub.PartySize, Index = index, Level = Missions.Level, Origin = centre, Anchor = toward,
                               Mission = Missions.KindOf(Missions.Kind).Id };
    }

    public int Patrol(Vector2 at, double level = 1) =>
        Send(WaveTrigger.Called, new WaveBrief { Pilots = _hub.PartySize, CalledLevel = level, Origin = Hub.BasePos, Anchor = at });

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
    // `roll` >= 0 fixes the kinds (a rostered slot keeps its own); `pays` is a first fill's EXP.
    private int Send(WaveTrigger trigger, WaveBrief b, int roll = -1, bool pays = true)
    {
        if (!Net.IsHost) return 0;
        var d = Waves.For(trigger, b);
        if (d?.Crew == null) return 0;
        int squads = System.Math.Max(1, d.Squads + d.SquadsPerPilot * System.Math.Max(0, b.Pilots - 1));
        double strength = d.Strength?.Invoke(b) ?? 1, agility = d.Agility?.Invoke(b) ?? 1;
        int first = 0, wave = ++_waves;
        for (int s = 0; s < squads; s++)
        {
            int draw = roll >= 0 ? roll + s : _roll++;
            var at = Waves.SquadAt(d, b, s, squads);
            float heading = 0f;
            if (d.FormFor > 0)
            {   // FORM-UP: FormFor seconds of its own pace outside its commit range, on its bearing
                var crew = d.Crew.SelectMany(c => Enumerable.Repeat(Enemies.Of(Waves.KindOf(c, draw)), c.Count(b))).ToList();
                var dir = (at - b.Origin).LengthSquared() > 1e-6f ? (at - b.Origin).Normalized() : Vector2.Up;
                at = b.Origin + dir * (Squads.CommitOf(crew) + Squads.PaceOf(crew, agility) * (float)d.FormFor);
                heading = Aim.Face(at, b.Origin);
            }
            var squad = Form(d.Doctrine ?? Squads.Patrol, at, wave, heading);
            if (s == 0) first = squad.Id;
            squad.Quarry = b.Quarry;
            // ...and WHERE IT WAITS with nothing to fight: a ring of the row's Hold round its own
            // spot, or the base's perimeter round the brief's Origin (every home wave's is the base)
            squad.Station = d.Hold > 0 ? at : b.Origin;
            squad.Circuit = d.Hold > 0 ? d.Hold : Squad.PerimeterR;
            foreach (var c in d.Crew)
            {
                int n = c.Count(b), kind = Waves.KindOf(c, draw);
                for (int i = 0; i < n; i++)
                {
                    var r = _hub.SpawnRaider(at + c.Slot(i, n).Rotated(heading), kind, squad, strength, d.HullShare?.Invoke(b) ?? 1);
                    if (r == null) continue;
                    r.Agility = agility; r.Level = b.Level; r.DamageShare = d.DamageShare?.Invoke(b) ?? 1;
                    r.Worth = pays ? r.Def.Exp * d.Exp : 0;     // a refill, and every wave with no Exp, pays nothing
                }
            }
        }
        return first;
    }
}
