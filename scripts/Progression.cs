using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// PILOT PROGRESSION -- EXP, levels, and the points they buy.
//
//   EXP comes from boss kills: the host announces the kill and its level, and every
//   pilot computes its own share (Missions: the kill by level, +250 first clear, +100).
//   Every level needs 1000 EXP and pays 1 point.
//   Points buy flat upgrades; each upgrade's next level costs one more point than
//   the last (1, 2, 3, ...).
//
// Purchases are saved with the character and sent to the host with its identity,
// because the host resolves hull and damage.
// ─────────────────────────────────────────────────────────────────────────────
public static class Progression
{
    public class Upgrade { public string Id, Name, Unit; public double Per; }

    // The damage upgrade lands on each hull's main weapon: its gun shells, or its torpedoes.
    public static string DamageStat(ShipClass c) => c == ShipClass.Battleship ? "main_damage" : "torpedo_damage";
    public static string StatFor(string id, ShipClass c) => id switch
    {
        "turn" => "turn_rate", "hull" => "hull", "speed" => "max_speed", "damage" => DamageStat(c), _ => null
    };

    public static readonly Upgrade[] All =
    {
        new() { Id = "turn",   Name = "Rudder",  Per = Mathf.DegToRad(1f), Unit = "°/s" },   // +1 degree per second
        new() { Id = "hull",   Name = "Hull",    Per = 5,  Unit = "hull" },
        new() { Id = "speed",  Name = "Engines", Per = 1,  Unit = "u/s" },
        new() { Id = "damage", Name = "Weapons", Per = 1,  Unit = "damage" },
    };
    public const int MaxPerUpgrade = 60;       // a sanity cap on what a peer may claim

    public static int Cost(int owned) => owned + 1;                        // 1, 2, 3, ...
    public const int ExpPerLevel = 1000;
    public static int ExpToNext => ExpPerLevel;                             // always 1000, whatever the level

    // Flat stat additions for a set of purchases, on a given hull.
    public static System.Collections.Generic.Dictionary<string, double> Flats(int[] bought, ShipClass c)
    {
        var d = new System.Collections.Generic.Dictionary<string, double>();
        for (int i = 0; i < All.Length && i < (bought?.Length ?? 0); i++)
            if (bought[i] > 0) d[StatFor(All[i].Id, c)] = bought[i] * All[i].Per;
        return d;
    }

    // EXP for the local pilot: level up as far as it reaches, a point per level.
    public static int AddExp(int amount)
    {
        if (amount <= 0) return 0;
        int gained = 0;
        Character.Exp += amount;
        while (Character.Exp >= ExpToNext)
        {
            Character.Exp -= ExpToNext;
            Character.Level++; Character.Points++; gained++;
        }
        Character.Save();
        return gained;
    }

    // A boss beaten at `level`: this pilot's own EXP -- the kill by its own level, the first
    // clear of that level, completing the mission -- and the record. Returns the EXP given.
    public static int AwardBossKill(int level)
    {
        var id = Missions.Current.Id;
        if (!Character.BossCleared.TryGetValue(id, out var set)) Character.BossCleared[id] = set = new System.Collections.Generic.HashSet<int>();
        bool first = set.Add(level);
        int exp = Missions.KillExpFor(level, Character.Level) + (first ? Missions.FirstClearExp : 0) + Missions.CompletionExp;
        AddExp(exp);                                                         // (saves)
        return exp;
    }

    public static bool TryBuy(string id)
    {
        int i = Array.FindIndex(All, u => u.Id == id);
        if (i < 0) return false;
        int cost = Cost(Character.Bought[i]);
        if (Character.Points < cost || Character.Bought[i] >= MaxPerUpgrade) return false;
        Character.Points -= cost; Character.Bought[i]++;
        Character.Save();
        return true;
    }
}
