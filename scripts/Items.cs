using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// ITEMS -- every part that drops, as rows: the hull categories, the tier law, the roles a line
// lifts, and the LINES (numbers_curve_raids_items.md §3). Equipment.Build yields Items.Build.
//
// REPLACED: 62 rarity lines written per class system (Rapid Battery, Deep Bubble, Hard Coils...) at
// three rarities (x1 / x1.5 / x2 on the upside), each naming the stat ids it moved, so a part fitted
// the hulls that had them. Now a line belongs to a HULL CATEGORY (capital, freighter, heavy, light;
// chips to every hull) and exists at TEN TIERS: the same item, every up x1.10 a tier, the price fixed.
//
// A LINE MUST FILL IN: its stem (ids are {stem}_t{n}), name, slot, category (none: a chip, and then
// its ChipKind), blurb, its UPS as (stat key, the share at T1 -- one of the Tiers leans), its PRICE
// as (stat, share) from Price (never grows with the tier), and for a RIDER its count rows (Rider:
// stat, base count) -- the +n comes from Tiers.Plus and the rows become what a hull must have (a
// rider fits only a hull with one of them). `Par` marks the reference line Par wears; `Conditional`
// a line whose up applies only in a situation (unpriced, §3.1; Par never wears one).
//
// A STAT KEY is a stat id, or a ROLE: `@name` lifts every row of that role the hull's sheet has
// (Roles below), and `~id` lifts the EXCESS of a x-multiplier row over x1 (the boost's x1.5:
// "+25%" takes +50% to +62.5%). Equipment expands both per hull (Items.Expand), so a line never
// names a class, and the kits' rows join a role in ONE table.
// ─────────────────────────────────────────────────────────────────────────────
public enum HullCat { Capital, Freighter, Heavy, Light }

// WHICH HULL IS WHICH CATEGORY: a row per category, the classes it holds. A thirteenth class joins a row.
public static class Hulls
{
    public sealed class Row
    {
        public HullCat Cat;
        public string Name;
        public ShipClass[] Classes;
    }

    public static readonly Row[] All =
    {
        new() { Cat = HullCat.Capital,   Name = "CAPITAL",   Classes = new[] { ShipClass.Battleship, ShipClass.Carrier, ShipClass.Destroyer } },
        new() { Cat = HullCat.Freighter, Name = "FREIGHTER", Classes = new[] { ShipClass.FreightHauler, ShipClass.FreightTender, ShipClass.FreightBastion } },
        new() { Cat = HullCat.Heavy,     Name = "HEAVY",     Classes = new[] { ShipClass.HeavySniper, ShipClass.HeavyWarrior, ShipClass.HeavyWarden } },
        new() { Cat = HullCat.Light,     Name = "LIGHT",     Classes = new[] { ShipClass.LightDart, ShipClass.LightEcho, ShipClass.LightWraith } },
    };

    public static HullCat Of(ShipClass c) => All.First(r => Array.IndexOf(r.Classes, c) >= 0).Cat;
    public static string NameOf(HullCat cat) => All.First(r => r.Cat == cat).Name;
}

// THE TIER LAW (§3.1): one growth, x1.10 a tier, ten tiers. The T1 leans are the owner's caps.
public static class Tiers
{
    public const int Count = 10;
    public const double Growth = 1.10;
    // THE T1 LEANS: a power up (damage, hull, output) +25%; a MULTIPLIER (rate, cooldown, duration)
    // +20%, because it multiplies every damage share under it; reach and area 18%; top speed and
    // acceleration 22%; a chip 8%; an unconditional line with no price HALF the power lean (R7).
    public const double Power = 0.25, Multiplier = 0.20, Reach = 0.18, Top = 0.22, Chip = 0.08, Half = 0.125;
    public static double P(int t) => Math.Pow(Growth, Math.Clamp(t, 1, Count) - 1);
    // A RIDER'S +n on a count of `b` (R8): +1, and +2 from T6 on a base of 6 or more, from T9 on 4.
    public static int Plus(int b, int t) => t >= (b >= 6 ? 6 : 9) ? 2 : 1;
    // What the recycler pays: 100 x 1.25^(t-1), faster than power on purpose (§3.4).
    public const double ScrapBase = 100, ScrapGrowth = 1.25;
    public static double Scrap(int t) => Math.Round(ScrapBase * Math.Pow(ScrapGrowth, Math.Clamp(t, 1, Count) - 1));
}

// THE PRICES (§3.1): "-15% of power", weighted by the stat. A price never grows with the tier.
public static class Price
{
    public const double Damage = -0.15, Hull = -0.15, Top = -0.24, Turn = -0.28, Range = -0.28, Area = -0.28,
        Tracking = -0.33, Accel = -0.33, ShotSpeed = -0.33, Astern = -0.42, ChipHelm = -0.04;
    // a rider's second price, 0.5 ln((b+1)/b) of power, on top speed
    public const double RiderTop4 = -0.17, RiderTop6 = -0.12;
}

public sealed class ItemLine
{
    public string Stem, Name, Blurb;
    public GearSlot Slot;
    public HullCat? Cat;                       // null: a chip, fitting every hull
    public ChipKind Kind;
    public (string key, double t1)[] Up = Array.Empty<(string, double)>();
    public (string key, double share)[] Down = Array.Empty<(string, double)>();
    public (string stat, int b)[] Rider = Array.Empty<(string, int)>();
    public bool Par, Conditional;
}

public static class Items
{
    // ── THE ROLES: what "every ability output" means, row by row, keyed by stat id ──
    // `@damage` (every weapon's damage) and `@reach` (every weapon's reach) are the classes' own
    // declarations (ClassDef.Damage, ClassDef.Reach). The rest are rows here: a kit row that is an
    // ability's output, area or duration joins its role by id (the lane's ITEM ASSUMPTIONS).
    private static readonly (string role, string[] ids)[] Roles =
    {
        ("@primary",       new[] { "main_damage", "fighter_damage", "blade_damage" }),
        ("@primary_rate",  new[] { "main_interval", "fighter_interval", "blade_interval" }),
        ("@primary_range", new[] { "main_range", "fighter_range", "blade_reach" }),
        ("@tracking",      new[] { "main_turn", "fighter_turn" }),
        ("@shot_speed",    new[] { "shell_speed", "fighter_speed" }),
        ("@output",        new[] { "broadside_mult", "torpedo_damage", "missile_damage", "bubble_pool", "overdrive_mult",
                                   "wave_push", "rail_damage", "hunter_damage", "echo_share", "lunge_damage", "whirl_damage" }),
        ("@area",          new[] { "bubble_radius", "wave_range", "echo_radius", "whirl_reach", "hunter_range", "launch_range", "taunt_reach" }),
        ("@duration",      new[] { "bubble_time", "overdrive_time", "wave_disable", "stealth_time", "roll_time", "echo_time", "whirl_time", "prism_time", "anchor_time", "tether_hold", "taunt_time",
                                   "brace_time" }),
    };

    // The stat ids a key stands for on hull `c`: a role's rows the hull has, or the id itself.
    public static IEnumerable<string> IdsOf(string key, ShipClass c)
    {
        if (key.StartsWith('~')) return new[] { key[1..] };
        if (!key.StartsWith('@')) return new[] { key };
        if (key == "@damage") return Classes.Damage(c).Keys;
        if (key == "@reach") return Classes.ReachOf(c).Keys;
        var sheet = Equipment.SheetOf(c);
        return Roles.Where(r => r.role == key).SelectMany(r => r.ids).Where(sheet.Contains);
    }

    // A part's shares as the hull `c` takes them: every role expanded onto its rows, every `~id`
    // turned into a share of the row (its excess over x1, at the hull's own base).
    public static Dictionary<string, double> Expand(ShipClass c, IReadOnlyDictionary<string, double> shares)
    {
        var d = new Dictionary<string, double>();
        foreach (var (key, v) in shares)
            foreach (var id in IdsOf(key, c))
            {
                double share = v;
                if (key.StartsWith('~'))
                {
                    double b = Equipment.BaseOf(c, id);
                    share = b > 1 ? v * (b - 1) / b : 0;
                }
                d[id] = d.GetValueOrDefault(id) + share;
            }
        return d;
    }

    private static (string, double)[] U(params (string, double)[] x) => x;
    private static (string, int)[] R(params (string, int)[] x) => x;

    // ── THE LINES (§3.3): 11 capital, 10 freighter, 11 heavy, 10 light, 6 chips ──
    public static readonly ItemLine[] Lines =
    {
        // CAPITAL: the line of battle, standoff gunnery, the alpha strike, tempo, the fortress, the
        // escort screen, warp assault and the broadside dance
        new() { Stem = "heavy_battery", Name = "Heavy Battery", Slot = GearSlot.Weapon, Cat = HullCat.Capital, Par = true,
                Blurb = "fewer, heavier hits: the primary hits harder; its turrets and craft track slower",
                Up = U(("@primary", Tiers.Power)), Down = U(("@tracking", Price.Tracking)) },
        new() { Stem = "director_suite", Name = "Director Suite", Slot = GearSlot.Weapon, Cat = HullCat.Capital,
                Blurb = "hits what jinks: faster tracking and shots, a little more damage; shorter reach",
                Up = U(("@tracking", Tiers.Power), ("@shot_speed", Tiers.Power), ("@primary", 0.05)), Down = U(("@primary_range", Price.Range)) },
        new() { Stem = "escort_hunter", Name = "Escort Hunter", Slot = GearSlot.Weapon, Cat = HullCat.Capital, Conditional = true,
                Blurb = "against craft only: more damage and tracking. Nothing on a boss",
                Up = U(("craft_damage", Tiers.Power), ("craft_tracking", Tiers.Power)) },
        new() { Stem = "salvo_core", Name = "Salvo Core", Slot = GearSlot.Utility, Cat = HullCat.Capital, Par = true,
                Blurb = "the alpha strike: every ability hits harder, from closer",
                Up = U(("@output", Tiers.Power)), Down = U(("@area", Price.Area)) },
        new() { Stem = "magazine_core", Name = "Magazine Core", Slot = GearSlot.Utility, Cat = HullCat.Capital,
                Blurb = "every ability hits harder, and one more volley or torpedo; closer, and slower on the helm",
                Up = U(("@output", Tiers.Power)), Down = U(("@area", Price.Area), ("max_speed", Price.RiderTop4)),
                Rider = R(("broadside_volleys", 6), ("bomber_ammo", 4)) },
        new() { Stem = "tempo_core", Name = "Tempo Core", Slot = GearSlot.Utility, Cat = HullCat.Capital,
                Blurb = "tempo: every ability cooldown recovers faster; less top speed",
                Up = U(("cooldown_share", Tiers.Multiplier)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "bulwark_belt", Name = "Bulwark Belt", Slot = GearSlot.Shield, Cat = HullCat.Capital, Par = true,
                Blurb = "the fortress: more hull; less top speed",
                Up = U(("hull", Tiers.Power)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "aegis_array", Name = "Aegis Array", Slot = GearSlot.Shield, Cat = HullCat.Capital,
                Blurb = "the escort screen: point defence hits harder and reaches further; slower to accelerate",
                Up = U(("pd_damage", Tiers.Power), ("pd_range", Tiers.Reach)), Down = U(("thrust", Price.Accel)) },
        new() { Stem = "armoured_citadel", Name = "Armoured Citadel", Slot = GearSlot.Hull, Cat = HullCat.Capital, Par = true,
                Blurb = "the second hull source: more hull; a lower rudder limit",
                Up = U(("hull", Tiers.Power)), Down = U(("turn_rate", Price.Turn)) },
        new() { Stem = "warp_spine", Name = "Warp Spine", Slot = GearSlot.Hull, Cat = HullCat.Capital,
                Blurb = "further and sooner on the jump: a longer safe warp, a faster charge; less top speed",
                Up = U(("warp_safe", Tiers.Reach), ("warp_rate", Tiers.Power)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "helm_drive", Name = "Helm Drive", Slot = GearSlot.Engines, Cat = HullCat.Capital,
                Blurb = "the broadside dance: a higher rudder limit, a tighter turn; less top speed",
                Up = U(("turn_rate", Tiers.Power), ("turn_radius", Tiers.Power)), Down = U(("max_speed", Price.Top)) },

        // FREIGHTER: the heavy hauler, artillery, the long grind, field engineering and the convoy
        new() { Stem = "heavy_ordnance", Name = "Heavy Ordnance", Slot = GearSlot.Weapon, Cat = HullCat.Freighter, Par = true,
                Blurb = "the heavy hauler's guns: every weapon hits harder; the primary tracks slower",
                Up = U(("@damage", Tiers.Power)), Down = U(("@tracking", Price.Tracking)) },
        new() { Stem = "long_arc", Name = "Long Arc", Slot = GearSlot.Weapon, Cat = HullCat.Freighter,
                Blurb = "artillery: more reach, faster shells, a little more damage; slower tracking",
                Up = U(("@primary_range", Tiers.Reach), ("@shot_speed", Tiers.Power), ("@primary", 0.05)), Down = U(("@tracking", Price.Tracking)) },
        new() { Stem = "spinup_feed", Name = "Spin-up Feed", Slot = GearSlot.Weapon, Cat = HullCat.Freighter,
                Blurb = "the long grind: the primary ramps up on one target; shorter reach",
                Up = U(("spinup_damage", Tiers.Power)), Down = U(("@primary_range", Price.Range)) },
        new() { Stem = "field_core", Name = "Field Core", Slot = GearSlot.Utility, Cat = HullCat.Freighter, Par = true,
                Blurb = "every ability's output is stronger, over a smaller area",
                Up = U(("@output", Tiers.Power)), Down = U(("@area", Price.Area)) },
        new() { Stem = "field_emitter", Name = "Field Emitter", Slot = GearSlot.Utility, Cat = HullCat.Freighter,
                Blurb = "the field engineer: bigger, longer zones; less top speed",
                Up = U(("@area", Tiers.Reach), ("@duration", Tiers.Multiplier)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "cargo_plating", Name = "Cargo Plating", Slot = GearSlot.Shield, Cat = HullCat.Freighter, Par = true,
                Blurb = "the armoured hauler: more hull; slower to accelerate",
                Up = U(("hull", Tiers.Power)), Down = U(("thrust", Price.Accel)) },
        new() { Stem = "buffer_array", Name = "Buffer Array", Slot = GearSlot.Shield, Cat = HullCat.Freighter,
                Blurb = "no compromise: a little more hull, and nothing to pay for it",
                Up = U(("hull", Tiers.Half)) },
        new() { Stem = "cargo_spine", Name = "Cargo Spine", Slot = GearSlot.Hull, Cat = HullCat.Freighter, Par = true,
                Blurb = "the second hull source: more hull; a lower rudder limit",
                Up = U(("hull", Tiers.Power)), Down = U(("turn_rate", Price.Turn)) },
        new() { Stem = "convoy_rig", Name = "Convoy Rig", Slot = GearSlot.Hull, Cat = HullCat.Freighter,
                Blurb = "the convoy: the boost lasts longer and strafes harder; less top speed",
                Up = U(("surge_time", Tiers.Multiplier), ("~surge_strafe", Tiers.Power)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "hauler_drive", Name = "Hauler Drive", Slot = GearSlot.Engines, Cat = HullCat.Freighter,
                Blurb = "reach over distance: more top speed and acceleration; a lower rudder limit",
                Up = U(("max_speed", Tiers.Top), ("thrust", Tiers.Top)), Down = U(("turn_rate", Price.Turn)) },

        // HEAVY: the duellist, the marksman, the swarm, the finisher, the stance-holder, the raid survivor
        new() { Stem = "heavy_barrel", Name = "Heavy Barrel", Slot = GearSlot.Weapon, Cat = HullCat.Heavy, Par = true,
                Blurb = "hit harder: every weapon's damage; the primary tracks slower",
                Up = U(("@damage", Tiers.Power)), Down = U(("@tracking", Price.Tracking)) },
        new() { Stem = "rapid_action", Name = "Rapid Action", Slot = GearSlot.Weapon, Cat = HullCat.Heavy,
                Blurb = "tempo: the primary cycles faster; shorter reach",
                Up = U(("@primary_rate", Tiers.Multiplier)), Down = U(("@primary_range", Price.Range)) },
        new() { Stem = "executioner", Name = "Executioner", Slot = GearSlot.Weapon, Cat = HullCat.Heavy, Conditional = true,
                Blurb = "the finisher: more damage against a target under 35% hull",
                Up = U(("execute_damage", Tiers.Power)) },
        new() { Stem = "tactical_core", Name = "Tactical Core", Slot = GearSlot.Utility, Cat = HullCat.Heavy, Par = true,
                Blurb = "every ability hits harder, from closer",
                Up = U(("@output", Tiers.Power)), Down = U(("@area", Price.Area)) },
        new() { Stem = "endurance_core", Name = "Endurance Core", Slot = GearSlot.Utility, Cat = HullCat.Heavy,
                Blurb = "hold the stance longer: every ability lasts longer; less top speed",
                Up = U(("@duration", Tiers.Multiplier)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "swarm_rack", Name = "Swarm Rack", Slot = GearSlot.Utility, Cat = HullCat.Heavy,
                Blurb = "every ability hits harder, and one more hunter or flare; closer, and slower on the helm",
                Up = U(("@output", Tiers.Power)), Down = U(("@area", Price.Area), ("max_speed", Price.RiderTop6)),
                Rider = R(("hunter_count", 6), ("flare_count", 6)) },
        new() { Stem = "heavy_plating", Name = "Heavy Plating", Slot = GearSlot.Shield, Cat = HullCat.Heavy, Par = true,
                Blurb = "stand and trade: more hull; slower astern",
                Up = U(("hull", Tiers.Power)), Down = U(("reverse_speed", Price.Astern)) },
        new() { Stem = "web_breaker", Name = "Web Breaker", Slot = GearSlot.Shield, Cat = HullCat.Heavy, Conditional = true,
                Blurb = "the raid survivor: a web on you holds shorter and slows you less",
                Up = U(("web_resist", Tiers.Power)) },
        new() { Stem = "bulkhead_frame", Name = "Bulkhead Frame", Slot = GearSlot.Hull, Cat = HullCat.Heavy, Par = true,
                Blurb = "the second hull source: more hull; a lower rudder limit",
                Up = U(("hull", Tiers.Power)), Down = U(("turn_rate", Price.Turn)) },
        new() { Stem = "quickboost_frame", Name = "Quick-Boost Frame", Slot = GearSlot.Hull, Cat = HullCat.Heavy,
                Blurb = "more boosts: the boost recharges faster; less top speed",
                Up = U(("surge_cooldown", Tiers.Multiplier)), Down = U(("max_speed", Price.Top)) },
        new() { Stem = "vector_drive", Name = "Vector Drive", Slot = GearSlot.Engines, Cat = HullCat.Heavy,
                Blurb = "the side-step: more strafe thrust and astern speed; less top speed",
                Up = U(("strafe_thrust", Tiers.Power), ("reverse_speed", Tiers.Power)), Down = U(("max_speed", Price.Top)) },

        // LIGHT: the glass cannon, hit-and-run, the raid dancer, swarm armour and the dogfighter
        new() { Stem = "hot_barrel", Name = "Hot Barrel", Slot = GearSlot.Weapon, Cat = HullCat.Light, Par = true,
                Blurb = "hit harder: every weapon's damage; slower shots",
                Up = U(("@damage", Tiers.Power)), Down = U(("@shot_speed", Price.ShotSpeed)) },
        new() { Stem = "redline", Name = "Redline", Slot = GearSlot.Weapon, Cat = HullCat.Light, Conditional = true,
                Blurb = "the glass cannon's gamble: more damage while your hull is under 50%",
                Up = U(("redline_damage", Tiers.Power)) },
        new() { Stem = "burst_feed", Name = "Burst Feed", Slot = GearSlot.Weapon, Cat = HullCat.Light, Conditional = true,
                Blurb = "hit and run: the primary cycles faster for 4 s after a boost ends",
                Up = U(("afterboost_rate", Tiers.Multiplier)) },
        new() { Stem = "ace_core", Name = "Ace Core", Slot = GearSlot.Utility, Cat = HullCat.Light, Par = true,
                Blurb = "every ability hits harder, from closer",
                Up = U(("@output", Tiers.Power)), Down = U(("@area", Price.Area)) },
        new() { Stem = "reset_core", Name = "Reset Core", Slot = GearSlot.Utility, Cat = HullCat.Light, Conditional = true,
                Blurb = "the raid dancer: a kill takes a share off every ability cooldown left",
                Up = U(("kill_reset", Tiers.Power)) },
        new() { Stem = "reactive_plating", Name = "Reactive Plating", Slot = GearSlot.Shield, Cat = HullCat.Light, Par = true,
                Blurb = "a light that can take a beam tick: more hull; slower to accelerate",
                Up = U(("hull", Tiers.Power)), Down = U(("thrust", Price.Accel)) },
        new() { Stem = "ablative_skin", Name = "Ablative Skin", Slot = GearSlot.Shield, Cat = HullCat.Light, Conditional = true,
                Blurb = "swarm armour: hits under 10% of your hull deal less",
                Up = U(("small_hit_cut", Tiers.Multiplier)) },
        new() { Stem = "stressed_skin", Name = "Stressed Skin", Slot = GearSlot.Hull, Cat = HullCat.Light, Par = true,
                Blurb = "the second hull source: more hull; a lower rudder limit",
                Up = U(("hull", Tiers.Power)), Down = U(("turn_rate", Price.Turn)) },
        new() { Stem = "featherweight_frame", Name = "Featherweight Frame", Slot = GearSlot.Hull, Cat = HullCat.Light,
                Blurb = "the dogfighter: snappier acceleration and rudder; less hull",
                Up = U(("thrust", Tiers.Top), ("turn_rate", Tiers.Power)), Down = U(("hull", Price.Hull)) },
        new() { Stem = "burner_drive", Name = "Burner Drive", Slot = GearSlot.Engines, Cat = HullCat.Light,
                Blurb = "the straight-line burst: the boost's top speed; a lower rudder limit",
                Up = U(("~surge_lift", Tiers.Power)), Down = U(("turn_rate", Price.Turn)) },

        // CHIPS: generic, every hull, at most Equipment.KindCap of each kind; no salvage level
        new() { Stem = "chip_combat", Name = "Combat Chip", Slot = GearSlot.Chip, Kind = ChipKind.Combat,
                Blurb = "every weapon hits harder; a little less top speed",
                Up = U(("@damage", Tiers.Chip)), Down = U(("max_speed", Price.ChipHelm)) },
        new() { Stem = "chip_gunner", Name = "Gunner Chip", Slot = GearSlot.Chip, Kind = ChipKind.Combat,
                Blurb = "the primary hits harder; a little lower rudder limit",
                Up = U(("@primary", 0.10)), Down = U(("turn_rate", Price.ChipHelm)) },
        new() { Stem = "chip_hunter", Name = "Hunter Chip", Slot = GearSlot.Chip, Kind = ChipKind.Combat, Conditional = true,
                Blurb = "against craft only: more damage",
                Up = U(("craft_damage", Tiers.Chip)) },
        new() { Stem = "chip_armour", Name = "Armour Chip", Slot = GearSlot.Chip, Kind = ChipKind.Utility,
                Blurb = "more hull; a little lower rudder limit",
                Up = U(("hull", Tiers.Chip)), Down = U(("turn_rate", Price.ChipHelm)) },
        new() { Stem = "chip_engine", Name = "Engine Chip", Slot = GearSlot.Chip, Kind = ChipKind.Utility,
                Blurb = "more top speed and rudder limit",
                Up = U(("max_speed", 0.05), ("turn_rate", 0.06)) },
        new() { Stem = "chip_target", Name = "Targeting Chip", Slot = GearSlot.Chip, Kind = ChipKind.Utility,
                Blurb = "every weapon reaches further",
                Up = U(("@reach", 0.06)) },
    };

    // ── THE CONDITIONS (§3.1, I6): a line whose up counts only in a situation lifts a SHEET ROW AT x1
    // (base 1; the line's share takes it to 1 + share), and the row is read at ONE door. A row fills
    // in its id, label, door, the ceiling its share stops at (0: none) and, for a door that weighs a
    // blow, When. Every row is on the sheet of every hull a conditional line naming it fits
    // (RowsOf), so a hull with none of the gear reads x1 and every door is a no-op on it.
    public enum Door { Dealt, Taken, Tracking, Kill, Web, AfterDrive, Spin }
    public readonly record struct Blow(IHittable Target, double OwnLeft, double D, double OwnMax);
    public sealed class Condition
    {
        public string Id, Label;
        public Door Door;
        public double Cap;                       // the share's ceiling; 0: none
        public Func<Blow, bool> When;            // null: always, at its door
    }

    public const double ExecuteBelow = 0.35, RedlineBelow = 0.50, SmallHit = 0.10;
    public const double AfterDriveSecs = 4, SpinSecs = 5, SpinDrop = 1;
    // what a CRAFT is to Escort Hunter and the Hunter Chip: a small craft or a wing, never a boss
    public static bool IsCraft(IHittable t) => t != null && TagExt.Is(t, Tag.Light | Tag.Fighter) && !TagExt.Is(t, Tag.Boss);
    // THE PRIMARY'S BLOWS by weapon id (Dealt): what Spin-up Feed ramps. The kits' new primaries add
    // their ids here (ITEM ASSUMPTIONS A10).
    public static readonly string[] PrimaryShots = { "shell", "fighter", "blade" };   // a wing craft's blow is its Wings row id; a melee row's its Id
    // THE BLOWS THAT REPEAT BLOWS ALREADY WEIGHED (Dealt.Deal): the echo puts down what its ship dealt,
    // and every blow it stored was weighed at the door as it landed, so PlayerShip.Outgoing passes a
    // repeat as it is. A second repeating weapon is a row here.
    public static readonly string[] Repeats = { Dealt.Echo };

    public static readonly Condition[] Conditions =
    {
        new() { Id = "craft_damage",    Label = "Damage against craft",       Door = Door.Dealt,    When = b => IsCraft(b.Target) },
        new() { Id = "execute_damage",  Label = "Damage, target under 35%",   Door = Door.Dealt,    When = b => b.Target != null && b.Target.HullLeft < ExecuteBelow },
        new() { Id = "redline_damage",  Label = "Damage, own hull under 50%", Door = Door.Dealt,    When = b => b.OwnLeft < RedlineBelow },
        new() { Id = "craft_tracking",  Label = "Tracking onto craft",        Door = Door.Tracking, When = b => IsCraft(b.Target) },
        new() { Id = "small_hit_cut",   Label = "Small hits cut",             Door = Door.Taken, Cap = 0.40, When = b => b.D < SmallHit * b.OwnMax },
        new() { Id = "kill_reset",      Label = "Cooldowns off on a kill",    Door = Door.Kill },
        new() { Id = "web_resist",      Label = "A web shorter and weaker",   Door = Door.Web,   Cap = 0.60 },
        new() { Id = "afterboost_rate", Label = "Primary rate after a boost", Door = Door.AfterDrive },
        new() { Id = "spinup_damage",   Label = "Primary ramp on one target", Door = Door.Spin },
    };

    // The condition rows on hull `c`'s sheet: every one a conditional line that fits it names.
    public static IEnumerable<Condition> RowsOf(ShipClass c)
    {
        var cat = Hulls.Of(c);
        var named = Lines.Where(l => l.Conditional && (l.Cat == null || l.Cat == cat)).SelectMany(l => l.Up).Select(u => u.key).ToHashSet();
        return Conditions.Where(r => named.Contains(r.Id));
    }

    // One row's share on a sheet: what its gear lifted it by, at its ceiling; 0 on a sheet without it.
    public static double ShareOf(ShipStats s, Condition r)
    {
        double v = s[r.Id];
        double share = v > 1 ? v - 1 : 0;
        return r.Cap > 0 ? Math.Min(r.Cap, share) : share;
    }
    // EVERY ROW AT A DOOR whose situation holds, ADDED (the share rule: buffs add).
    public static double Shares(Door door, ShipStats s, in Blow b)
    {
        double sum = 0;
        foreach (var r in Conditions)
            if (r.Door == door && (r.When == null || r.When(b))) sum += ShareOf(s, r);
        return sum;
    }
    // THE RAMP (Spin-up Feed): the share builds evenly to its whole over SpinSecs on one target.
    public static double SpinShare(double share, double secsOn) => share * Math.Clamp(secsOn / SpinSecs, 0, 1);
    // A WEB'S TOP SPEED under a cut of `cut`: the slow (1 - pin) shrinks by the cut.
    public static float PinnedSpeed(float pin, double cut) => (float)(1 - (1 - pin) * (1 - Math.Clamp(cut, 0, 1)));
    // HOW LONG A WEB HOLDS under a cut (PlayerShip's web clock): a web is held in rounds of WebRound
    // seconds, pinned for (1 - cut) of each and free for the rest, so a raider's latch, which asks for
    // the pin again every frame, holds (1 - cut) of the time it is on, and a single timed web about
    // the same share of its time. With no cut the round is all hold: a web holds as long as it is asked.
    // `phase` is the seconds since the web took; the answer is the seconds of this round's hold left
    // (0: the free part of the round).
    public const double WebRound = 2.0;
    public static double WebHoldLeft(double phase, double cut)
    {
        double hold = WebRound * (1 - Math.Clamp(cut, 0, 1)), at = phase % WebRound;
        return at < hold ? hold - at : 0;
    }

    public static string IdOf(string stem, int tier) => $"{stem}_t{tier}";

    // EVERY LINE AT EVERY TIER: the ups x P(t), the price as written, the rider's +n by Tiers.Plus.
    public static IEnumerable<ItemDef> Build()
    {
        foreach (var l in Lines)
            for (int t = 1; t <= Tiers.Count; t++)
            {
                var it = new ItemDef { Id = IdOf(l.Stem, t), Name = $"{l.Name} T{t}", Blurb = l.Blurb, Slot = l.Slot, Tier = t,
                                       Cat = l.Cat, Kind = l.Kind, Line = l,
                                       Needs = l.Rider.Select(r => r.stat).ToArray(),
                                       Ups = l.Up.Select(u => u.key).Distinct().ToArray() };
                foreach (var (key, t1) in l.Up) it.Pct[key] = it.Pct.GetValueOrDefault(key) + t1 * Tiers.P(t);
                foreach (var (key, share) in l.Down) it.Pct[key] = it.Pct.GetValueOrDefault(key) + share;
                foreach (var (stat, b) in l.Rider) it.Add[stat] = Tiers.Plus(b, t);
                yield return it;
            }
    }
}
