using Godot;
using System;
using System.Collections.Generic;

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

    // WHAT AN UPGRADE MOVES, on a given hull. Everything but Weapons lands on one stat, the same
    // on every class. WEAPONS asks the CLASS what its weapons are (ClassDef.Damage): it used to
    // name one stat here -- torpedoes for a carrier, main guns for everything else -- so a
    // sniper's railgun, a warden's hunters, a warrior's EMP and a freighter's deployed turrets
    // took nothing at all from a pilot's damage points.
    private static readonly Dictionary<string, string> Simple = new()
    {
        ["turn"] = "turn_rate", ["hull"] = "hull", ["speed"] = "max_speed",
    };
    // stat id -> what ONE level of `id` adds on class `c`
    public static IEnumerable<(string stat, double per)> StatsFor(string id, ShipClass c, double per)
    {
        if (id == "damage")
        {
            foreach (var kv in Classes.Damage(c)) if (kv.Value != 0) yield return (kv.Key, kv.Value);
            yield break;
        }
        if (Simple.TryGetValue(id, out var one)) yield return (one, per);
    }

    public static readonly Upgrade[] All =
    {
        new() { Id = "turn",   Name = "Rudder",  Per = Mathf.DegToRad(1f), Unit = "°/s" },   // +1 degree per second
        new() { Id = "hull",   Name = "Hull",    Per = 5,  Unit = "hull" },
        new() { Id = "speed",  Name = "Engines", Per = 1,  Unit = "u/s" },
        new() { Id = "damage", Name = "Weapons", Per = 1,  Unit = "damage" },
    };
    public const int MaxPerUpgrade = 60;       // a sanity cap on what a peer may claim

    public static int Cost(int owned) => owned + 1;                        // 1, 2, 3, ...
    // Whether a pilot of `level` could have paid for `bought`: a point a level after the first,
    // Cost(k) for the k-th of each. The host holds every pilot's announcement to it -- by this
    // rule, not a formula copied beside it that a change to Cost would silently leave behind.
    public static bool Affordable(int[] bought, int level)
    {
        long spent = 0;
        foreach (var n in bought ?? System.Array.Empty<int>())
        {
            if (n > MaxPerUpgrade) return false;
            for (int k = 0; k < n; k++) spent += Cost(k);
        }
        return spent <= System.Math.Max(0, level - 1);
    }
    private const int ExpPerLevel = 1000;
    public static int ExpToNext => ExpPerLevel;                             // always 1000, whatever the level

    // Flat stat additions for a set of purchases, on a given hull.
    public static Dictionary<string, double> Flats(int[] bought, ShipClass c)
    {
        var d = new Dictionary<string, double>();
        for (int i = 0; i < All.Length && i < (bought?.Length ?? 0); i++)
        {
            // StatsFor yields nothing for an id it does not know. Every id in All is covered
            // today, but adding a fifth upgrade and forgetting it would otherwise put a null key
            // in here -- an ArgumentNullException a long way from the cause.
            if (bought[i] <= 0) continue;
            foreach (var (stat, per) in StatsFor(All[i].Id, c, All[i].Per))
                d[stat] = d.GetValueOrDefault(stat) + bought[i] * per;
        }
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
        if (gained > 0) { Hub.I?.Hints?.Meet("pilot"); Hub.I?.PilotChanged(); }   // the new level goes out with the identity
        Character.Save();
        return gained;
    }

    // A boss beaten at `level`: this pilot's own EXP -- the kill by its own level, the first
    // clear of that level, completing the mission -- and the record. Returns the EXP given.
    public static int AwardBossKill(int level)
    {   // recorded under the boss that held the level; first by the ladder, whoever held it before
        var id = Missions.ForLevel(level).Id;
        bool first = !Missions.Cleared(level);
        if (!Character.BossCleared.TryGetValue(id, out var set)) Character.BossCleared[id] = set = new System.Collections.Generic.HashSet<int>();
        set.Add(level);
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
