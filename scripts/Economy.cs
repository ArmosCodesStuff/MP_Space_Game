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
//                         step upgrades : each level adds a step (one more ship or pod;
//                                         +7% EVASION), each costs 2x the last, up to a
//                                         cap: 5 miners, 5 salvagers, 6 pods, 95% safe
//                         switches      : bought once (AUTO-SELL), some only after a boss
//   THE HAULER'S RUNS   a lone run (DISPATCH) gets through with the EVASION chance, or is lost
//                       past the portal with its cargo; an ESCORT flies the long way with
//                       raider waves hunting it, and pays 5x at the portal.
// ─────────────────────────────────────────────────────────────────────────────
public static class Economy
{
    // ── miners and salvagers, before upgrades ────────────────────────────────
    private const double BaseHold = 100;          // units
    private const double BaseShipSpeed = 110;     // u/s
    private const double BaseGatherRate = 2;      // units per second while beaming
    public const double UnloadRate = 50;         // units per second into an arm

    // ── the hauler ───────────────────────────────────────────────────────────
    public const int    MaxPods = 6;             // the pods painted on its hull
    private const double BasePodSize = 300;       // units per pod, before upgrades
    public const double HaulerLoadRate = 25;     // units per second from the stock
    public const double CreditsPerUnit = 1;
    public const double HaulerSpeed = 60;        // u/s -- it moves slowly
    public const double HaulerAway = 30;         // seconds through the portal
    public const double HaulerCharge = 3;        // seconds of blue aura before the jump
    // AUTO-SELL: 3x the average level-5 cost across the salvager rows, estimated ONCE and fixed
    // (the owner's rule): (6400 + 244 + 244 + 244 + 305) / 5 = 1487.4, x3 = 4462.2. A THEORETICAL
    // level 5, as the owner put it: the salvager count stops at 4 purchases, so its 6400 is the
    // price the formula would ask, not one a player can pay.
    public const double AutoSellCost = 4462;
    public const int AutoSellBoss = 3;            // ...and only once the level-3 boss is beaten
    // THE ESCORT: 5x the income; 4 waves of hunters, the first 5 s in, then every 20 s
    public const double EscortPay = 5;
    public const int EscortWaves = 4;
    public const double EscortFirstWave = 5, EscortWaveEvery = 20;
    // ── hull, and rebuilding what is lost ──
    public const double UtilityHull = 120, HaulerHull = 150;
    public const double RebuildDelay = 30;         // seconds after it is destroyed
    public const double RebuildShare = 0.10;       // of everything invested so far in its category
    public const double HaulerLift = 1.5;        // seconds to lift off the pad
    public const double HaulerLand = 3.0;        // seconds to descend onto it, turning 180 degrees on the way
    public const float  HaulerLandedScale = 0.65f;   // 200 u long in flight, 130 u landed

    // ── upgrades ─────────────────────────────────────────────────────────────
    public enum Kind { Percent, Count, Unlock }
    public const double PercentCostGrowth = 1.25, PercentEffect = 0.10;
    private const double CountCostGrowth = 2.0;

    public class Upgrade
    {
        public string Id, Tab, Name, Unit, Blurb;
        public Kind Kind;
        public double BaseCost, BaseValue;
        public double Step = 1;                      // what a level of a Count row adds
        public int Max = int.MaxValue;               // most levels that can be bought (step upgrades and switches)
        public int NeedsBoss;                        // the base owner's highest boss level before it can be bought
        public bool OwnerOnly;                       // the base owner's alone to buy: a guest's request is refused
    }

    public static readonly string[] Tabs = { "MINERS", "SALVAGERS", "HAULER" };

    public static readonly Upgrade[] All =
    {
        new() { Id = "miner_count",     Tab = "MINERS",    Kind = Kind.Count,   Name = "Miners",           BaseValue = 1,              Unit = "",          BaseCost = 400, Max = 4, Blurb = "+1 miner (up to 5)" },
        new() { Id = "miner_hold",      Tab = "MINERS",    Kind = Kind.Percent, Name = "Miner hold",       BaseValue = BaseHold,       Unit = "ore",       BaseCost = 100, Blurb = "+10% hull space" },
        new() { Id = "miner_speed",     Tab = "MINERS",    Kind = Kind.Percent, Name = "Miner engines",    BaseValue = BaseShipSpeed,  Unit = "u/s",       BaseCost = 100, Blurb = "+10% speed" },
        new() { Id = "mine_rate",       Tab = "MINERS",    Kind = Kind.Percent, Name = "Mining beam",      BaseValue = BaseGatherRate, Unit = "ore/s",     BaseCost = 100, Blurb = "+10% mining speed" },
        new() { Id = "miner_hull",      Tab = "MINERS",    Kind = Kind.Percent, Name = "Miner hull",       BaseValue = UtilityHull,    Unit = "hull",      BaseCost = 125, Blurb = "+10% hull (25% dearer to start)" },
        new() { Id = "salvager_count",  Tab = "SALVAGERS", Kind = Kind.Count,   Name = "Salvagers",        BaseValue = 1,              Unit = "",          BaseCost = 400, Max = 4, Blurb = "+1 salvager (up to 5)" },
        new() { Id = "salvager_hold",   Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvager hold",    BaseValue = BaseHold,       Unit = "salvage",   BaseCost = 100, Blurb = "+10% hull space" },
        new() { Id = "salvager_speed",  Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvager engines", BaseValue = BaseShipSpeed,  Unit = "u/s",       BaseCost = 100, Blurb = "+10% speed" },
        new() { Id = "salvage_rate",    Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvage beam",     BaseValue = BaseGatherRate, Unit = "salvage/s", BaseCost = 100, Blurb = "+10% salvage speed" },
        new() { Id = "salvager_hull",   Tab = "SALVAGERS", Kind = Kind.Percent, Name = "Salvager hull",    BaseValue = UtilityHull,    Unit = "hull",      BaseCost = 125, Blurb = "+10% hull (25% dearer to start)" },
        new() { Id = "hauler_pods",     Tab = "HAULER",    Kind = Kind.Count,   Name = "Cargo pods",       BaseValue = 1,              Unit = "",          BaseCost = 500, Max = MaxPods - 1, Blurb = "+1 pod (up to 6)" },
        new() { Id = "pod_size",        Tab = "HAULER",    Kind = Kind.Percent, Name = "Pod size",         BaseValue = BasePodSize,    Unit = "per pod",   BaseCost = 150, Blurb = "+10% per pod" },
        // (at the END: a guest is sent the levels in this order)
        new() { Id = "hauler_evasion",  Tab = "HAULER",    Kind = Kind.Count,   Name = "Evasion",          BaseValue = 60, Step = 7,   Unit = "% safe",    BaseCost = 400, Max = 5, Blurb = "+7% chance a lone run gets through (up to 95%)" },
        new() { Id = "hauler_autosell", Tab = "HAULER",    Kind = Kind.Unlock,  Name = "Auto-sell",        BaseValue = 0,              Unit = "",          BaseCost = AutoSellCost, Max = 1, NeedsBoss = AutoSellBoss, OwnerOnly = true, Blurb = "completely full, it goes by itself" },
    };

    public static Upgrade ById(string id) { foreach (var u in All) if (u.Id == id) return u; return null; }

    // The price of buying level (level + 1): 1.25x per level, or 2x for a step upgrade; a switch
    // costs its price once.
    public static double Cost(Upgrade u, int level) =>
        Math.Round(u.BaseCost * Math.Pow(u.Kind == Kind.Count ? CountCostGrowth : PercentCostGrowth, level));

    // The number at a level: +10% per level, a step per level, or a switch's 0 / 1.
    public static double Value(Upgrade u, int level) => u.Kind switch
    {
        Kind.Count => u.BaseValue + u.Step * level,
        Kind.Unlock => level,
        _ => u.BaseValue * (1 + PercentEffect * level),
    };

    public static bool Maxed(Upgrade u, int level) => level >= u.Max;
}
