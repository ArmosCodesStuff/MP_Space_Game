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
    // ONE UPGRADE, AND WHAT IT MOVES. What a row changed used to live in a SECOND private table
    // (id -> stat) plus an `if (id == "damage")` inside StatsFor, so a fifth row here was fully
    // purchasable and silently moved nothing. The row carried a step and a unit of its own beside
    // that, which the damage branch ignored and the PILOT window advertised anyway; the window
    // reads StatsFor now, in the STATS tab's own units, so a row states what it moves and no more.
    public class Upgrade
    {
        public string Id, Name;
        // The stats ONE level moves, and by how much each, in that stat's own units.
        public (string stat, double per)[] Moves = Array.Empty<(string, double)>();
        // Instead of Moves: ask the CLASS what its weapons are (ClassDef.Damage), because they are
        // a different set on every hull -- a sniper's railgun, a warden's hunters, a warrior's EMP,
        // a freighter's deployed turrets.
        public bool Weapons;
    }

    // stat id -> what ONE level of `u` adds on class `c`: the row's own list, or the class's weapons
    public static IEnumerable<(string stat, double per)> StatsFor(Upgrade u, ShipClass c)
    {
        if (u.Weapons)
        {
            foreach (var kv in Classes.Damage(c)) if (kv.Value != 0) yield return (kv.Key, kv.Value);
            yield break;
        }
        foreach (var m in u.Moves) if (m.per != 0) yield return m;
    }

    public static readonly Upgrade[] All =
    {
        new() { Id = "turn",   Name = "Rudder",  Moves = new[] { ("turn_rate", (double)Mathf.DegToRad(1f)) } },   // +1 degree per second
        new() { Id = "hull",   Name = "Hull",    Moves = new[] { ("hull", 5.0) } },
        new() { Id = "speed",  Name = "Engines", Moves = new[] { ("max_speed", 1.0) } },
        new() { Id = "damage", Name = "Weapons", Weapons = true },
    };
    public const int MaxPerUpgrade = 60;       // a sanity cap on what a peer may claim
    // THE MOST A CLAIM MAY BUY on someone else's host. A pilot's level is its own word and there
    // is no way to check it, so what a claim may SPEND is capped here -- 99 points, a level-100
    // pilot's worth. The gate used to read the raw wire level, so a peer claiming two billion
    // bought every row maxed on the host's own copy of its ship: +300 hull and four times the
    // damage, from a level-1 pilot.
    public const int MaxSpendLevel = 100;
    // The claim, trimmed until a pilot of that level could have paid for it: the DEAREST point
    // goes first, so what survives is the most of what was claimed. Trimmed rather than refused
    // whole -- a pilot past level 100 used to arrive on any host it visited as a stock hull.
    public static int[] Afford(int[] bought, int level)
    {
        var b = new int[All.Length];
        for (int i = 0; i < b.Length && i < (bought?.Length ?? 0); i++)
            b[i] = System.Math.Clamp(bought[i], 0, MaxPerUpgrade);
        int lv = System.Math.Clamp(level, 1, MaxSpendLevel);
        while (!Affordable(b, lv))
        {
            int most = 0;
            for (int i = 1; i < b.Length; i++) if (b[i] > b[most]) most = i;
            if (b[most] == 0) break;
            b[most]--;
        }
        return b;
    }

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
            // A row that declares neither Moves nor Weapons yields nothing and adds nothing: a row
            // that can be bought and does not exist. A check names one (SmokeTest: every row moves
            // at least one stat on every flyable class).
            if (bought[i] <= 0) continue;
            foreach (var (stat, per) in StatsFor(All[i], c))
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
        // Nothing at all under the mark -- the bonuses too, not just the kill's share.
        int exp = Missions.WorthExp(level, Character.Level)
                ? Missions.KillExpFor(level, Character.Level) + (first ? Missions.FirstClearExp : 0) + Missions.CompletionExp
                : 0;
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
        Character.Spent.Add(i);                      // so a refit can undo THIS one, not the dearest
        Character.Save();
        return true;
    }

    // ── WHAT A REFIT COSTS A PILOT, beside the ore, the salvage and the credits ──────────────
    // One LEVEL, and the most recent point it spent comes back off the sheet. The points that
    // bought it are refunded -- it is a refit, not a forfeit -- and the level's own point goes
    // with the level, so a refit can never leave a pilot ahead of where it started. What it does
    // NOT take is the progress toward the next level: losing 560 of 1000 nobody had spent would be
    // a second cost, hidden, on top of the one the panel quotes.
    //
    // A file written before purchases were recorded has no order to read, so the DEAREST point
    // goes instead -- the row with the most levels, whose last point cost the most.
    // What a refit would undo, so the panel can name it before the pilot commits to it.
    public static string NextRefund
    {
        get
        {
            int i = Character.Spent.Count > 0 && Character.Bought[Character.Spent[^1]] > 0 ? Character.Spent[^1] : -1;
            if (i < 0)
                for (int k = 0; k < All.Length; k++) if (Character.Bought[k] > 0 && (i < 0 || Character.Bought[k] > Character.Bought[i])) i = k;
            return i < 0 ? "" : $" (a level of {All[i].Name} comes off)";
        }
    }

    public static (string name, int refunded) Refit()
    {
        int i = -1;
        if (Character.Spent.Count > 0)
        {
            i = Character.Spent[^1];
            Character.Spent.RemoveAt(Character.Spent.Count - 1);
            if (Character.Bought[i] <= 0) i = -1;    // a list out of step with the counts: fall through
        }
        if (i < 0)
            for (int k = 0; k < All.Length; k++) if (Character.Bought[k] > 0 && (i < 0 || Character.Bought[k] > Character.Bought[i])) i = k;

        int back = 0;
        if (i >= 0) { back = Cost(Character.Bought[i] - 1); Character.Bought[i]--; Character.Points += back; }
        if (Character.Level > 1) { Character.Level--; Character.Points = Math.Max(0, Character.Points - 1); }
        Character.Save();
        return (i >= 0 ? All[i].Name : null, back);
    }
}
