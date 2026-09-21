using System;
using System.Linq;

// Bounty missions from Threat Intelligence Operations: bosses, LEVELS, and rewards.
//   Bosses are data (more types slot in here). LEVELS start at 1 (the base boss):
//     S(L) = 1.1^(L-1)   -- a level-5 boss is 1.1^4 = 1.46x.
//   A PARTY of P pilots: the boss's hull x S(L)(1 + 0.6(P-1)), its damage x S(L)(1 + 0.2(P-1)).
//   A failed level-L mission's raids are S(L) too.
//   Beating a level unlocks the next; the TIO selects the newest when the host opens it.
//   REWARDS, per pilot, each computed on the pilot's own machine:
//     the kill   round(200 x boss level / pilot level)  (a level-5 boss for a level-10 pilot: 100)
//     +250       the first time that pilot beats that level
//     +100       for completing the mission
//     credits    2000 x S(L) x (1 + 0.5(P-1)), split evenly among the P pilots (solo earns the most)
public static class Missions
{
    public class BossType { public string Id, Name; public double Hull; }
    public static readonly BossType[] Bosses =
    {
        new() { Id = "silver_lancer", Name = "SILVER LANCER", Hull = 600 },
    };
    public static BossType Current => Bosses[0];

    private const double LevelStep = 1.10;
    public static double S(int level) => Math.Pow(LevelStep, Math.Max(1, level) - 1);
    public static int Level = 1;                            // the selected level (the host decides; replicated)
    public static int Beaten(string bossId) => Character.BossCleared.TryGetValue(bossId, out var set) && set.Count > 0 ? set.Max() : 0;
    public static int Unlocked(string bossId) => Beaten(bossId) + 1;

    public static double HullMult(int level, int party) => S(level) * (1 + 0.6 * (Math.Max(1, party) - 1));
    public static double DamageMult(int level, int party) => S(level) * (1 + 0.2 * (Math.Max(1, party) - 1));
    private const double BountyBase = 2000;
    public static double BountyEach(int level, int party) => BountyBase * S(level) * (1 + 0.5 * (Math.Max(1, party) - 1)) / Math.Max(1, party);

    public const int KillExp = 200, FirstClearExp = 250, CompletionExp = 100;
    public static int KillExpFor(int bossLevel, int pilotLevel) => (int)Math.Round(KillExp * (double)bossLevel / Math.Max(1, pilotLevel));
}
