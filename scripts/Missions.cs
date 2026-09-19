using System;

// Bounty missions from Threat Intelligence Operations: bosses, difficulty tiers, rewards.
//   Bosses are data (more types slot in here). Each boss has TIERS: tier n is
//   1.1^n times as strong (hull and every attack). Beating a tier unlocks the next,
//   and the TIO selects the newest unlocked tier whenever the host opens it.
//   The host picks; the tier travels to the guests with the mission state.
public static class Missions
{
    public class BossType { public string Id, Name; public double Hull; }
    public static readonly BossType[] Bosses =
    {
        new() { Id = "silver_lancer", Name = "SILVER LANCER", Hull = 3000 },
    };
    public static BossType Current => Bosses[0];
    public static string BossName => Current.Name;

    public const double TierStep = 1.10;                   // each tier 10% stronger, multiplicatively
    public static double Scale(int tier) => Math.Pow(TierStep, tier);
    public static int Tier;                                // the selected tier (host decides)
    public static int Unlocked(string bossId) => Character.BossBeaten.TryGetValue(bossId, out var t) ? t + 1 : 0;

    public const int BossExp = 300;          // each pilot, for the kill (shared)
    public const int MissionExp = 100;       // each pilot, for completing the mission (shared)
    public const int BossCredits = 2000;
}
