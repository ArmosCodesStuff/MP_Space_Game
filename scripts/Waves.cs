using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// WAVES — what arrives, in what shape, and how hard: one row per wave.
//
// REPLACED: three wave builders in Hub.cs, each with its composition written into it, plus four
// loose difficulty statics --
//   Hub.StartRaid     2 patrols and 1 more per extra pilot, on a ring at the map's edge, at the
//                     failed mission's own strength
//   Hub.SpawnPatrol   ALWAYS `lights` pinners in a row 40 u apart plus AT MOST ONE standoff at
//                     (0, 90), with one pin kind and one standoff kind drawn per wave -- so a
//                     wave could never mix two pinner kinds, never field two standoffs, and
//                     never field none of either
//   Hub.HuntWave      1 patrol and 1 more per extra pilot, a heavy on odd waves only, a side
//                     flip, a 0.35 spread and half hull
//   EscortThreat / ThreatStrength / ThreatLights / ThreatAgility
// There is ONE builder now -- Raids.Send -- and it reads a row of Waves.All and nothing else.
//
// A NEW WAVE IS A ROW HERE. A row must fill in:
//   Id            what it is called
//   Trigger       what sets it off: a mission lost, an escort's leg N, or a wave asked for outright
//   When          which of those moments it covers (null: all of them)
//   Squads        how many groups arrive -- a base, plus SquadsPerPilot for each pilot past the first
//   Form/Radius/Turn/Alternate   where those groups form up: a ring at a radius, a fan off an
//                 anchor so many radians a group, or the anchor itself
//   Hold          the ring each squad holds with nothing to fight, round its OWN spot. 0 -- every
//                 wave but a blockade -- is the base's perimeter, which is where they always went
//   Crew          rows of (an enemy named outright OR a way to draw one, how many, where they sit)
//   HullShare     the share of its row's hull each of them is built with
//   Strength      where its hull-and-damage multiplier comes from
//   Agility       ...and its speed and turning
//
// HOW A TRIGGER PICKS ITS ROW (Waves.For): the LAST row whose trigger matches and whose `When`
// says yes. A row with no `When` always says yes, so the plain row is written first and the rows
// that take over from it later are written under it.
//
// THE SCHEDULE IS NOT HERE. WHEN an escort's waves come is Economy.EscortFirstWave /
// EscortWaveEvery -- the run's own clock, which the Yard owns. This table is what turns up.
// ─────────────────────────────────────────────────────────────────────────────

// WHAT SETS A WAVE OFF.
public enum WaveTrigger
{
    Failed,      // a mission the party lost: the boss sends its raiders to the base
    Hunt,        // an escort's leg N: hunters sent after one quarry
    Called,      // asked for outright, at a spot, at a strength named on the spot
    Garrison,    // a mission's target defending itself: wave N of ITS own clock (Missions.Kinds)
    Blockade,    // a lane cut: a squad sitting ON it, holding that spot rather than the base's ring
}

// WHERE THE SQUADS OF A WAVE FORM UP, measured from the brief's Origin.
public enum WaveForm
{
    Ring,        // spread evenly round Origin at Radius, the whole ring turned by Turn
    Fan,         // off the line Origin -> Anchor, each squad Turn further round than the last
    Spot,        // every squad on Anchor itself
}

// ONE KIND IN A WAVE'S CREW: which enemy, how many of it, and where they sit relative to their
// squad's own spot. A line of them is centred on At and spaced by Step.
public readonly struct WaveCrew
{
    public readonly int Kind;                      // a row of Enemies.All, or Waves.Drawn to draw one
    public readonly EnemyWay Way;                  // which way to draw from, when Kind is Waves.Drawn
    public readonly int Nth;                       // ...this many rows on from the squad's own draw
    public readonly Func<WaveBrief, int> Count;    // how many of them this wave brings
    public readonly Vector2 At;                    // the line's centre, from the squad's spot
    public readonly Vector2 Step;                  // and how far apart they sit along it
    public WaveCrew(int kind, EnemyWay way, int nth, Func<WaveBrief, int> count, Vector2 at, Vector2 step)
        => (Kind, Way, Nth, Count, At, Step) = (kind, way, nth, count, at, step);
}

// WHAT A WAVE IS BUILT AGAINST: everything a row may ask about the moment it is sent.
public sealed class WaveBrief
{
    public int Pilots = 1;        // ships in the party
    public int Index;             // which wave of its run this is (an escort's leg; 0 otherwise)
    public int Level = 1;         // the mission level behind it
    public double Threat;         // an escort's threat, when there is one
    public double Scale = 1;      // a strength named outright (a called wave)
    public Vector2 Origin;        // what the wave forms up around
    public Vector2 Anchor;        // the point a Fan is measured to, and a Spot sits on
    public Node2D Quarry;         // the one thing it hunts, or null for whatever it finds
}

public sealed class WaveDef
{
    public string Id;
    public WaveTrigger Trigger;
    public Func<WaveBrief, bool> When;            // null: whenever its trigger fires
    public int Squads = 1;                        // squads at one pilot
    public int SquadsPerPilot;                    // ...and this many more for each pilot past the first
    public WaveForm Form;
    public float Radius;                          // Ring: how far out
    public float Turn;                            // Ring: the ring's own offset. Fan: radians a squad
    public bool Alternate;                        // Fan: odd waves come round the other side
    // WHERE ITS SQUADS WAIT with nothing to fight: a ring of this radius round the squad's OWN
    // spot. 0 is the base's perimeter (Raider.PerimeterR), which is what every wave that is not
    // holding a place wants, so only a blockade fills this in.
    public float Hold;
    public WaveCrew[] Crew;
    public double HullShare = 1;
    public Func<WaveBrief, double> Strength;      // null: x1
    public Func<WaveBrief, double> Agility;       // null: x1
}

public static class Waves
{
    public const int Drawn = -1;                  // a crew row that draws its enemy rather than naming one
    public const double HunterHull = 0.5;         // an escort's hunters are built at half hull

    // ── AN ESCORT'S THREAT, as a mission level: a quarter each from ──────────
    //   the load    1, and a level for every 1000 cr the cargo will fetch escorted (Hauler.Payout)
    //   the party   half its pilots' mean level, half its ships' toughness -- 1, and a level for each
    //               10% step its hull stands over its class's own (purchases and gear)
    //   the boss    the highest the base owner has beaten (1 before any)
    //   the route   how far along the escort the wave comes: 1 at the first, +0.5 a wave
    // A fresh base's first wave with a light load is about 1.4. The threat makes the hunters' hull and
    // damage (the missions' 10% a level), their numbers (a light more a patrol every 3 levels, 3 more
    // at most) and, very slightly, their speed and turning (1% a level, 10% at most).
    public const double CreditsPerThreat = 1000, ThreatPerWave = 0.5;
    public static double EscortThreat(double loadCredits, double partyLevel, double partyToughness, int highestBoss, int wave) =>
        0.25 * ((1 + Math.Max(0, loadCredits) / CreditsPerThreat)
              + (0.5 * partyLevel + 0.5 * partyToughness)
              + Math.Max(1, highestBoss)
              + (1 + ThreatPerWave * Math.Max(0, wave)));
    public static double ThreatStrength(double threat) => Math.Pow(Missions.LevelStep, Math.Max(0, threat - 1));
    public static int ThreatLights(double threat) => 3 + Math.Min(3, (int)Math.Floor(Math.Max(0, threat - 1) / 3));
    public static double ThreatAgility(double threat) => 1 + Math.Min(0.1, 0.01 * Math.Max(0, threat - 1));

    // The party, for an escort's threat: each pilot's level (the host's from its character, a
    // guest's as its identity claimed it, bounded) and its ship's toughness.
    // Past this a level stops making a raid stronger -- the host's own as much as a guest's claim.
    // It was a clamp on the stored claim alone, which left an honest host's level unbounded and
    // made a display bound look like the validation rule it was not.
    public const int ThreatLevelCap = 100;
    public static (double level, double toughness) Standing(IEnumerable<PlayerShip> ships)
    {
        double level = 0, toughness = 0; int n = 0;
        foreach (var s in ships)
        {
            if (!GodotObject.IsInstanceValid(s)) continue;
            level += Math.Min(ThreatLevelCap,
                s.Mine ? Character.Level : Net.I != null && Net.I.Players.TryGetValue(s.OwnerId, out var p) ? p.Level : 1);
            double own = new ShipStats(s.Class)["hull"];
            toughness += 1 + Math.Log(Math.Max(1, own > 0 ? s.MaxHp / own : 1)) / Math.Log(Missions.LevelStep);
            n++;
        }
        return n == 0 ? (1, 1) : (level / n, toughness / n);
    }

    // ── the two ways a crew row says WHICH enemy ─────────────────────────────
    private static WaveCrew Draw(EnemyWay way, int nth, Func<WaveBrief, int> count, Vector2 at, Vector2 step)
        => new(Drawn, way, nth, count, at, step);
    private static WaveCrew Named(int kind, Func<WaveBrief, int> count, Vector2 at, Vector2 step)
        => new(kind, EnemyWay.Pin, 0, count, at, step);

    // THE PLAIN SQUAD: three pinners in a row 40 u apart, and one standoff 90 u behind them.
    // This was Hub.SpawnPatrol's whole composition, written out in code with `lights` and `heavy`
    // as arguments -- which is why nothing could ever bring two of either.
    private static readonly WaveCrew[] Patrol =
    {
        Draw(EnemyWay.Pin, 0, _ => 3, Vector2.Zero, new Vector2(40f, 0f)),
        Draw(EnemyWay.Standoff, 0, _ => 1, new Vector2(0f, 90f), Vector2.Zero),
    };
    // an escort's pinners: as many as its threat asks for, in the same 40 u row
    private static readonly WaveCrew HuntPin =
        Draw(EnemyWay.Pin, 0, b => ThreatLights(b.Threat), Vector2.Zero, new Vector2(40f, 0f));

    public static readonly WaveDef[] All =
    {
        // A FAILED MISSION'S RAID. Level L (the failed boss's): raiders at S(L) = 1.1^(L-1), the
        // boss's own scaling. 2 squads, and 1 more per extra pilot in the session, in from the
        // map's edge (the clock that holds them 3 s is Raids.Delay).
        new() { Id = "raid", Trigger = WaveTrigger.Failed,
                Squads = 2, SquadsPerPilot = 1,
                Form = WaveForm.Ring, Radius = Hub.RaidEdge, Turn = 0.4f,
                Strength = b => Missions.S(b.Level),
                Crew = Patrol },

        // A SQUAD ASKED FOR OUTRIGHT, at a spot, at a strength named with it: the plain squad,
        // and nothing about the party in it (Hub.SpawnPatrol's old default arguments).
        new() { Id = "patrol", Trigger = WaveTrigger.Called,
                Form = WaveForm.Spot,
                Strength = b => b.Scale,
                Crew = Patrol },

        // AN ESCORT'S HUNTERS: a wave sent after one quarry, in from the map's edge beside it --
        // one squad, and one more per extra pilot, at HALF their hull. Alternate waves come round
        // from alternate sides. The first wave, and every even one after it, is pinners only.
        new() { Id = "hunt", Trigger = WaveTrigger.Hunt, When = b => b.Index % 2 == 0,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Fan, Turn = 0.35f, Alternate = true,
                HullShare = HunterHull,
                Strength = b => ThreatStrength(b.Threat), Agility = b => ThreatAgility(b.Threat),
                Crew = new[] { HuntPin } },

        // ...and every odd one brings a standoff with it (the second, the fourth, ...).
        new() { Id = "hunt_heavy", Trigger = WaveTrigger.Hunt, When = b => b.Index % 2 == 1,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Fan, Turn = 0.35f, Alternate = true,
                HullShare = HunterHull,
                Strength = b => ThreatStrength(b.Threat), Agility = b => ThreatAgility(b.Threat),
                Crew = new[] { HuntPin, Draw(EnemyWay.Standoff, 0, _ => 1, new Vector2(0f, 90f), Vector2.Zero) } },

        // FROM THE SEVENTH WAVE ON the pinners come in TWO kinds: the next row of the rotation
        // flies a second line 70 u ahead of the first. Nothing could field two pinner kinds before
        // this table -- one kind of each way per wave was the builder's whole vocabulary.
        new() { Id = "hunt_mixed", Trigger = WaveTrigger.Hunt, When = b => b.Index % 2 == 0 && b.Index >= 6,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Fan, Turn = 0.35f, Alternate = true,
                HullShare = HunterHull,
                Strength = b => ThreatStrength(b.Threat), Agility = b => ThreatAgility(b.Threat),
                Crew = new[] { HuntPin, Draw(EnemyWay.Pin, 1, _ => 2, new Vector2(0f, -70f), new Vector2(40f, 0f)) } },

        // ...AND FROM THE TENTH, TWO standoffs, 120 u apart: one drawn from the rotation and the
        // gunship itself, named outright.
        new() { Id = "hunt_twin", Trigger = WaveTrigger.Hunt, When = b => b.Index % 2 == 1 && b.Index >= 9,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Fan, Turn = 0.35f, Alternate = true,
                HullShare = HunterHull,
                Strength = b => ThreatStrength(b.Threat), Agility = b => ThreatAgility(b.Threat),
                Crew = new[] { HuntPin,
                               Draw(EnemyWay.Standoff, 0, _ => 1, new Vector2(-60f, 90f), Vector2.Zero),
                               Named(Enemies.Gunship, _ => 1, new Vector2(60f, 90f), Vector2.Zero) } },

        // A MISSION'S GARRISON -- the pirate base answering a siege. The escort's SCHEDULE (a first
        // wave, then one every so often) with none of the escort's REDUCTIONS: HullShare is the
        // default 1, not Waves.HunterHull, and the strength is the mission's own level, not an
        // escort's threat. One squad, and one more for each pilot past the first, off a ring round
        // the thing being besieged.
        new() { Id = "siege", Trigger = WaveTrigger.Garrison,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Ring, Radius = GarrisonRing, Turn = 0.35f,
                Strength = b => Missions.S(b.Level),
                Crew = Patrol },

        // ...AND FROM THE THIRD WAVE ON a gunship comes with each squad.
        new() { Id = "siege_heavy", Trigger = WaveTrigger.Garrison, When = b => b.Index >= 2,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Ring, Radius = GarrisonRing, Turn = 0.35f,
                Strength = b => Missions.S(b.Level),
                Crew = new[] { Patrol[0], Patrol[1],
                               Named(Enemies.Gunship, _ => 1, new Vector2(0f, 160f), Vector2.Zero) } },

        // A BLOCKADE -- the plain squad, sitting ON a lane 80% of the way out (Lanes.Stand) and
        // holding a tight ring THERE rather than the perimeter round the base. That is the whole
        // difference between a blockade and a patrol, and it is one field. At the base owner's own
        // standing on the bounty ladder, because what a blockade costs is that base's income.
        new() { Id = "blockade", Trigger = WaveTrigger.Blockade,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Spot, Hold = Lanes.Ring,
                Strength = b => Missions.S(b.Level),
                Crew = Patrol },

        // ...AND FROM THE THIRD ONE a gunship holds the lane with them: a late blockade is what
        // the outposts' own guns are bought for.
        new() { Id = "blockade_heavy", Trigger = WaveTrigger.Blockade, When = b => b.Index >= 2,
                Squads = 1, SquadsPerPilot = 1,
                Form = WaveForm.Spot, Hold = Lanes.Ring,
                Strength = b => Missions.S(b.Level),
                Crew = new[] { Patrol[0], Patrol[1],
                               Named(Enemies.Gunship, _ => 1, new Vector2(0f, 160f), Vector2.Zero) } },
    };

    // How far out a garrison forms up from what it is defending: outside the pirate base's own
    // 2200 u guns, so a wave is never born under them.
    public const float GarrisonRing = 2600f;

    // THE ROW A TRIGGER MEANS RIGHT NOW: the LAST one that matches, so a row written under
    // another takes over from it wherever its `When` says yes.
    public static WaveDef For(WaveTrigger trigger, WaveBrief b)
    {
        WaveDef found = null;
        foreach (var d in All) if (d.Trigger == trigger && (d.When == null || d.When(b))) found = d;
        return found;
    }

    // where squad `i` of `n` forms up
    public static Vector2 SquadAt(WaveDef d, WaveBrief b, int i, int n) => d.Form switch
    {
        WaveForm.Ring => b.Origin + Vector2.Right.Rotated(Mathf.Tau * i / Math.Max(1, n) + d.Turn) * d.Radius,
        WaveForm.Fan  => b.Origin + (b.Anchor - b.Origin).Rotated((d.Alternate && b.Index % 2 != 0 ? -1f : 1f) * d.Turn * (i + 1)),
        _             => b.Anchor,
    };

    // WHICH enemy a crew row brings: the one it names, or the `Nth` row on from this squad's own
    // draw among the kinds of its way -- so every row added to Enemies.All joins the rotation by
    // existing, and two crew rows of one way come out as two different kinds.
    public static int KindOf(WaveCrew c, int roll)
    {
        if (c.Kind >= 0) return c.Kind;
        var ids = Enemies.OfWay(c.Way);
        return ids.Length == 0 ? Enemies.Webifier : ids[(roll + c.Nth) % ids.Length];
    }
}
