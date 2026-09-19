using System;

// ─────────────────────────────────────────────────────────────────────────────
// ECONOMY — every number of the idle game, in one place. (The Yard runs it.)
//
//   MINERS / SALVAGERS  fly out, fill their holds with a beam, and queue for one of
//                       the base's five service arms to unload ore / salvage.
//   HAULER              lands on the base's bottom pad, loads the stock into its
//                       cargo pods, and is dispatched through the portal to sell it.
//   UPGRADES            bought with credits, in tabs:
//                         +10% upgrades : each level +10%, each costs 1.25x the last
//                         +1 upgrades   : one more ship or pod, each costs 2x the last,
//                                         up to a cap: 5 miners, 5 salvagers, 6 pods
// ─────────────────────────────────────────────────────────────────────────────
public static class Economy
{
    // ── miners and salvagers, before upgrades ────────────────────────────────
    public const double BaseHold = 100;          // units
    public const double BaseShipSpeed = 110;     // u/s
    public const double BaseGatherRate = 2;      // units per second while beaming
    public const double UnloadRate = 50;         // units per second into an arm

    // ── the hauler ───────────────────────────────────────────────────────────
    public const int    MaxPods = 6;             // the pods painted on its hull
    public const double BasePodSize = 300;       // units per pod, before upgrades
    public const double HaulerLoadRate = 25;     // units per second from the stock
    public const double CreditsPerUnit = 1;
    public const double HaulerSpeed = 60;        // u/s -- it moves slowly
    public const double HaulerAway = 30;         // seconds through the portal
    public const double HaulerCharge = 3;        // seconds of blue aura before the jump
    // ── hull, and rebuilding what is lost ──
    public const double UtilityHull = 60, HaulerHull = 150;
    public const double RebuildDelay = 30;         // seconds after it is destroyed
    public const double RebuildShare = 0.10;       // of everything invested so far in its category
    public const double HaulerLift = 1.5;        // seconds to lift off the pad
    public const double HaulerLand = 3.0;        // seconds to descend onto it, turning 180 degrees on the way
    public const float  HaulerLandedScale = 0.65f;   // 200 u long in flight, 130 u landed

    // ── upgrades ─────────────────────────────────────────────────────────────
    public enum Kind { Percent, Count }
    public const double PercentCostGrowth = 1.25, PercentEffect = 0.10;
    public const double CountCostGrowth = 2.0;

    public class Upgrade
    {
        public string Id, Tab, Name, Unit, Blurb;
        public Kind Kind;
        public double BaseCost, BaseValue;
        public int Max = int.MaxValue;               // most levels that can be bought (+1 upgrades)
    }

    public static readonly string[] Tabs = { "MINERS", "SALVAGERS", "HAULER" };

    public static readonly Upgrade[] All =
    {
        new() { Id = "miner_count",     Tab = "MINERS",    Kind = Kind.Count,   Name = "Miners",           BaseValue = 1,              Unit = "",          BaseCost = 400, Max = 4, Blurb = "+1 miner (up to 5)" },
        new() { Id = "miner_hold",      Tab = "MINERS",    Kind = Kind.Percent, Name = "Miner hold",       BaseValue = BaseHold,       Unit = "ore",       BaseCost = 100, Blurb = "+10% hull space" },
        new() { Id = "miner_speed",     Tab = "MINERS",    Kind = Kind.Percent, Name = "Miner engines",    BaseValue = BaseShipSpeed,  Unit = "u/s",       BaseCost = 100, Blurb = "+10% speed" },
        new() { Id = "mine_rate",       Tab = "MINERS",    Kind = Kind.Percent, Name = "Mining beam",      BaseValue = BaseGatherRate, Unit = "ore/s",     BaseCost = 100, Blurb = "+10% mining speed" },
        new() { Id = "salvager_count",  Tab = "SALVAGERS", Kind = Kind.Count,   Name = "Salvagers",        BaseValue = 1,              Unit = "",          BaseCost = 400, Max = 4, Blurb = "+1 salvager (up to 5)" },
        new() { Id = "salvager_hold",   Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvager hold",    BaseValue = BaseHold,       Unit = "salvage",   BaseCost = 100, Blurb = "+10% hull space" },
        new() { Id = "salvager_speed",  Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvager engines", BaseValue = BaseShipSpeed,  Unit = "u/s",       BaseCost = 100, Blurb = "+10% speed" },
        new() { Id = "salvage_rate",    Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvage beam",     BaseValue = BaseGatherRate, Unit = "salvage/s", BaseCost = 100, Blurb = "+10% salvage speed" },
        new() { Id = "hauler_pods",     Tab = "HAULER",    Kind = Kind.Count,   Name = "Cargo pods",       BaseValue = 1,              Unit = "",          BaseCost = 500, Max = MaxPods - 1, Blurb = "+1 pod (up to 6)" },
        new() { Id = "pod_size",        Tab = "HAULER",    Kind = Kind.Percent, Name = "Pod size",         BaseValue = BasePodSize,    Unit = "per pod",   BaseCost = 150, Blurb = "+10% per pod" },
    };

    public static Upgrade ById(string id) { foreach (var u in All) if (u.Id == id) return u; return null; }

    // The price of buying level (level + 1): 1.25x per level, or 2x for a +1 upgrade.
    public static double Cost(Upgrade u, int level) =>
        Math.Round(u.BaseCost * Math.Pow(u.Kind == Kind.Count ? CountCostGrowth : PercentCostGrowth, level));

    // The number at a level: +10% per level, or +1 per level.
    public static double Value(Upgrade u, int level) =>
        u.Kind == Kind.Count ? u.BaseValue + level : u.BaseValue * (1 + PercentEffect * level);

    public static bool Maxed(Upgrade u, int level) => level >= u.Max;
}
