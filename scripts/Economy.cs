using System;
using System.Linq;

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
    public const double AutoSellCost = 4462;      // ...bought once the level-3 boss is beaten (Unlocks)
    // THE ESCORT: 5x the income; a wave of hunters 5 s in, then every 20 s for as long as the run
    // lasts; a 4 s hold at each outpost to offload.
    // WHEN a wave comes is here, because it is the run's own clock and the Yard runs the run.
    // WHAT arrives in it -- how many, of what, how hard -- is a row of Waves.All (scripts/Waves.cs);
    // no composition belongs in this file.
    public const double EscortPay = 5;
    public const double EscortFirstWave = 5, EscortWaveEvery = 20;
    public const double OutpostStop = 4;
    // ── hull, and rebuilding what is lost ──
    public const double HaulerHull = 262.5;
    // THE RECYCLER: what a part scraps for, by rarity, and how long each one takes. A part could
    // not be got rid of at all before this, so a hold filled with commons a pilot would never fit
    // again. The figures are the salvage a gear level costs at the bottom of its ladder (100), so
    // one common is one early level of something kept.
    public const double ScrapEvery = 5.0;
    public static double ScrapValue(Rarity r) => r switch { Rarity.Epic => 600, Rarity.Rare => 250, _ => 100 };
    public const double RebuildDelay = 30;         // seconds after it is destroyed
    public const double RebuildShare = 0.10;       // of everything invested so far in its category
    // ITS OWN POINT DEFENCE: one turret on the spine, firing whenever the hauler is out there.
    // It is nobody's ship -- there is no window to open and no ability bar to open it from -- so
    // the mount is simply always on. The yard buys its damage up (hauler_pd_damage, +5% a level);
    // everything else about it is fixed. 1 a shot every 0.5 s inside 400 u is 2 DPS, a nuisance to
    // a missile and a light raider rather than a defence, which is what the escort is for.
    public const double HaulerPdDamage = 1.0, HaulerPdInterval = 0.5;
    public const double HaulerPdRange = 400, HaulerPdTurn = System.Math.Tau / 2;
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
        public double Per = PercentEffect;           // what a level of a Percent row adds (a fraction of the base)
        public int Max = int.MaxValue;               // most levels that can be bought (step upgrades and switches)
        public bool OwnerOnly;                       // the base owner's alone to buy: a guest's request is refused
    }

    // The tabs of the BASE window, in order: one per gatherer row, then the hauler's, then the
    // outposts' guns (Lanes.cs). A guest is sent each tab's investment in this order
    // (Yard.NetTotals), so the window follows the table. APPEND ONLY.
    public const string HaulerTab = "HAULER";
    public static readonly string[] Tabs = Gathering.All.Select(d => d.Tab).Append(HaulerTab).Append(Lanes.Tab).ToArray();

    // EVERY GATHERER HAS THE SAME FIVE ROWS: one more ship, a bigger hold, faster engines, a
    // faster beam, more hull. They were written out TWICE -- same kind, same price, same growth,
    // same cap -- differing only in an id prefix, a label and a unit, so a third gatherer was five
    // more copies to keep in step with the first ten. The shape lives here once; a gatherer's row
    // (Gathering.All) fills in the prefix, the words and the base numbers.
    //
    // THE IDS ARE ON DISK, in every pilot's [base_levels]. Four of the five are "<Id>_count",
    // "_hold", "_speed" and "_hull"; the beam's is the row's own RateId, because the id already
    // written to disk is "mine_rate" and not "miner_rate" -- generating it would make
    // Economy.ById return null and silently drop a level the player paid for.
    private static Upgrade[] Rows(GathererDef g) => new Upgrade[]
    {
        new() { Id = g.CountId, Tab = g.Tab, Kind = Kind.Count,   Name = g.Name + "s",        BaseValue = 1,       Unit = "",            BaseCost = 400, Max = 4, Blurb = $"+1 {g.Name.ToLowerInvariant()} (up to 5)" },
        new() { Id = g.HoldId,  Tab = g.Tab, Kind = Kind.Percent, Name = g.Name + " hold",    BaseValue = g.Hold,  Unit = g.Unit,        BaseCost = 100, Blurb = "+10% hull space" },
        new() { Id = g.SpeedId, Tab = g.Tab, Kind = Kind.Percent, Name = g.Name + " engines", BaseValue = g.Speed, Unit = "u/s",         BaseCost = 100, Blurb = "+10% speed" },
        new() { Id = g.RateId,  Tab = g.Tab, Kind = Kind.Percent, Name = g.RateName,          BaseValue = g.Rate,  Unit = g.Unit + "/s", BaseCost = 100, Blurb = g.RateBlurb },
        new() { Id = g.HullId,  Tab = g.Tab, Kind = Kind.Percent, Name = g.Name + " hull",    BaseValue = g.Hull,  Unit = "hull",        BaseCost = 125, Blurb = "+10% hull (25% dearer to start)" },
    };

    // The hauler's own rows. Declared ABOVE All because a static field initialiser runs in the
    // order it is written, and All reads this one.
    private static readonly Upgrade[] HaulerRows =
    {
        new() { Id = "hauler_pods",     Tab = "HAULER",    Kind = Kind.Count,   Name = "Cargo pods",       BaseValue = 1,              Unit = "",          BaseCost = 500, Max = MaxPods - 1, Blurb = "+1 pod (up to 6)" },
        new() { Id = "pod_size",        Tab = "HAULER",    Kind = Kind.Percent, Name = "Pod size",         BaseValue = BasePodSize,    Unit = "per pod",   BaseCost = 150, Blurb = "+10% per pod" },
        // (at the END: a guest is sent the levels in this order)
        new() { Id = "hauler_evasion",  Tab = "HAULER",    Kind = Kind.Count,   Name = "Evasion",          BaseValue = 60, Step = 7,   Unit = "% safe",    BaseCost = 400, Max = 5, Blurb = "+7% chance a lone run gets through (up to 95%)" },
        new() { Id = "hauler_autosell", Tab = "HAULER",    Kind = Kind.Unlock,  Name = "Auto-sell",        BaseValue = 0,              Unit = "",          BaseCost = AutoSellCost, Max = 1, OwnerOnly = true, Blurb = "full, it goes by itself" },
        new() { Id = "hauler_speed",    Tab = "HAULER",    Kind = Kind.Percent, Name = "Hauler engines",   BaseValue = HaulerSpeed,    Unit = "u/s",       BaseCost = 150, Per = 0.02, Blurb = "+2% speed per level" },
        new() { Id = "hauler_pd_damage",Tab = "HAULER",    Kind = Kind.Percent, Name = "Hauler point defence", BaseValue = HaulerPdDamage, Unit = "per shot", BaseCost = 200, Per = 0.05, Blurb = "+5% damage per level" },
    };

    // THE OUTPOSTS' GUNS: ONE SET OF ROWS FOR ALL OF THEM, as deploy_* is one set for every turret
    // a freighter leaves out -- so a fifth lane arrives with its gun already upgradeable and no row
    // is added here. The base numbers are the guns' own (Lanes), exactly as a gatherer's five rows
    // take theirs from its row. The owner asked for three: range, rate of fire, missile speed.
    // (RATE is missiles a MINUTE so a +10% row can climb; the gun divides 60 by it.)
    private static readonly Upgrade[] LaneRows =
    {
        new() { Id = Lanes.RangeId, Tab = Lanes.Tab, Kind = Kind.Percent, Name = "Outpost missile range", BaseValue = Lanes.GunRange, Unit = "u",    BaseCost = 300, Blurb = "+10% reach on every outpost" },
        new() { Id = Lanes.RateId,  Tab = Lanes.Tab, Kind = Kind.Percent, Name = "Outpost rate of fire",  BaseValue = Lanes.GunRate,  Unit = "/min", BaseCost = 300, Blurb = "+10% missiles a minute" },
        new() { Id = Lanes.SpeedId, Tab = Lanes.Tab, Kind = Kind.Percent, Name = "Outpost missile speed", BaseValue = Lanes.GunSpeed, Unit = "u/s",  BaseCost = 300, Blurb = "+10% speed: off the mark sooner" },
    };

    // Every gatherer's five rows in table order, then the hauler's, then the outposts'. THE ORDER
    // IS THE WIRE: a guest is sent the levels as an array indexed by position (Yard.NetTotals), so
    // adding a gatherer adds five rows before the hauler's -- which is why both peers must be on
    // the same build, and Net's fingerprint is what makes sure of it. APPEND ONLY.
    public static readonly Upgrade[] All = Gathering.All.SelectMany(Rows).Concat(HaulerRows).Concat(LaneRows).ToArray();

    public static Upgrade ById(string id) { foreach (var u in All) if (u.Id == id) return u; return null; }

    // The price of buying level (level + 1): 1.25x per level, or 2x for a step upgrade; a switch
    // costs its price once.
    public static double Cost(Upgrade u, int level) =>
        Math.Round(u.BaseCost * Math.Pow(u.Kind == Kind.Count ? CountCostGrowth : PercentCostGrowth, level));

    // The number at a level: a Percent row's own share per level (10% unless it says otherwise),
    // a step per level, or a switch's 0 / 1.
    public static double Value(Upgrade u, int level) => u.Kind switch
    {
        Kind.Count => u.BaseValue + u.Step * level,
        Kind.Unlock => level,
        _ => u.BaseValue * (1 + u.Per * level),
    };

    public static bool Maxed(Upgrade u, int level) => level >= u.Max;
}
