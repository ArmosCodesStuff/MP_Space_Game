using System;
using System.Collections.Generic;
using System.Linq;

// Bounty missions from Threat Intelligence Operations: bosses, LEVELS, and rewards.
//   ONE LADDER OF LEVELS, the bosses taking them in turn (ForLevel): odd levels the Silver Lancer,
//   even levels the Drake Bastion. LEVELS start at 1:
//     S(L) = 1.1^(L-1)   -- a level-5 boss is 1.1^4 = 1.46x.
//   A PARTY of P pilots: the boss's hull x S(L)(1 + 0.6(P-1)), its damage x S(L)(1 + 0.2(P-1)).
//   A failed level-L mission's raids are S(L) too.
//   Beating a level -- whichever boss held it -- unlocks the next; the TIO selects the newest when
//   the host opens it. The first-clear bonus is paid once a level, not once a boss.
//   REWARDS, per pilot, each computed on the pilot's own machine:
//     the kill   round(200 x boss level / pilot level)  (a level-5 boss for a level-10 pilot: 100)
//     +250       the first time that pilot beats that level
//     +100       for completing the mission
//     credits    2000 x S(L) x (1 + 0.5(P-1)), split evenly among the P pilots (solo earns the most)
//     parts      Loot.CratesFor(L) crates of gear, rolled by the host for each pilot (see Loot)
public static class Missions
{
    // A boss: its id (a pilot's clears are recorded under it), its name, its base hull, and how to
    // build one.
    public class BossType { public string Id, Name; public double Hull; public Func<Boss> Make; }
    public static readonly BossType[] Bosses =
    {
        new() { Id = "silver_lancer", Name = "SILVER LANCER", Hull = 760, Make = () => new Lancer() },
        new() { Id = "drake_bastion", Name = "DRAKE BASTION", Hull = 700, Make = () => new Drake() },
    };
    // The boss of a level: every peer works it out from the replicated level alone.
    public static BossType ForLevel(int level) => Bosses[(Math.Max(1, level) - 1) % Bosses.Length];

    public const double LevelStep = 1.10;
    public static double S(int level) => Math.Pow(LevelStep, Math.Max(1, level) - 1);
    public static int Level = 1;                            // the selected level (the host decides; replicated)
    // The levels this pilot has cleared, of every boss this build knows (an id in the file it does
    // not know counts for nothing): one ladder, whoever held each level.
    private static IEnumerable<int> ClearedLevels =>
        Character.BossCleared.Where(kv => Bosses.Any(b => b.Id == kv.Key)).SelectMany(kv => kv.Value);
    public static bool Cleared(int level) => ClearedLevels.Contains(level);
    public static int HighestBeaten => ClearedLevels.DefaultIfEmpty(0).Max();
    public static int Unlocked => HighestBeaten + 1;

    public static double HullMult(int level, int party) => S(level) * (1 + 0.6 * (Math.Max(1, party) - 1));
    public static double DamageMult(int level, int party) => S(level) * (1 + 0.2 * (Math.Max(1, party) - 1));
    private const double BountyBase = 2000;
    public static double BountyEach(int level, int party) => BountyBase * S(level) * (1 + 0.5 * (Math.Max(1, party) - 1)) / Math.Max(1, party);

    public const int KillExp = 200, FirstClearExp = 250, CompletionExp = 100;
    public static int KillExpFor(int bossLevel, int pilotLevel) => (int)Math.Round(KillExp * (double)bossLevel / Math.Max(1, pilotLevel));
}
