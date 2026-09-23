using System;
using System.Collections.Generic;
using System.Linq;

// EQUIPMENT -- a ship's parts, in slots. The menu (I) shows the gear of the pilot's CURRENT class;
// each class keeps its own loadout (saved with the character), so gear left on a ship is still on
// it when the pilot switches back. Parts change no looks, only numbers.
//   Slots: Weapon, Engines, Shield (the hull points), Hull (the frame and its point defence),
//   Utility -- and five Chips.
//
// THE STARTING KIT (Mk I etc.) reproduces the ship's own numbers; each kit chip adds +5% to every
// weapon's damage and +5% hull. EVERYTHING ELSE DROPS from bosses (Loot): per slot type, FOUR
// SPECIALISATIONS, each leaning hard one way, at THREE RARITIES. The upside grows with rarity
// (x1, x1.5, x2); the downside does not, so a rarer copy is strictly better. Weapon and Utility
// parts fit one class; Engines, Shield, Hull and chips fit any capital ship (every class is one).
//
// A part is a set of stat changes: shares (Pct: +0.30 = +30%; on an interval or a radius the
// share is a RATE and divides, see Stat) and whole additions (Add: one fighter fewer, two more
// bursts in the magazine). The host applies every pilot's gear (it resolves damage and hull):
// the loadout travels with the identity and is sanitised on arrival.
public enum GearSlot { Weapon, Engines, Shield, Hull, Utility, Chip }
public enum Rarity { Common, Rare, Epic }

public class ItemDef
{
    public string Id, Name, Blurb;
    public GearSlot Slot;
    public Rarity Rarity = Rarity.Common;
    public ShipClass? Class;                 // one class's part; null: any capital ship
    public bool Kit;                         // the starting kit: never dropped
    public Dictionary<string, double> Pct = new(), Add = new();
}

public static class Equipment
{
    public const int CoreSlots = 5, ChipSlots = 5, Slots = CoreSlots + ChipSlots;
    public static readonly GearSlot[] Core = { GearSlot.Weapon, GearSlot.Engines, GearSlot.Shield, GearSlot.Hull, GearSlot.Utility };
    // Above All: static fields start in the order they are written, and Build() reads these.
    private static readonly double[] Scale = { 1.0, 1.5, 2.0 };        // the upside, by rarity
    private static readonly string[] Suffix = { "", " II", " III" };
    private static readonly string[] Ranges = { "main_range", "missile_range", "fighter_range", "control_range", "torpedo_range", "pd_range" };
    private static readonly (string, double)[] None = Array.Empty<(string, double)>();

    public static readonly ItemDef[] All = Build().ToArray();
    private static readonly Dictionary<string, ItemDef> ByIdMap = All.ToDictionary(i => i.Id);
    public static ItemDef ById(string id) => id != null && ByIdMap.TryGetValue(id, out var i) ? i : null;
    // what can drop: everything but the kit
    public static IEnumerable<ItemDef> Drops => All.Where(i => !i.Kit);

    // PARTS THAT CHANGED CLASS, by the id a save file from before knows them by. The battleship's
    // missile rack and its four rack lines are the destroyer's now (the battleship fires a
    // broadside instead): Character.Load reads every part id through this, so in the hold they
    // become the destroyer's parts, and fitted to a battleship they no longer fit and go into it.
    private static readonly string[] RackLines = { "salvo", "buster", "seeker", "loader" };
    public static string Migrated(string id)
    {
        if (id == "bs_missile_rack") return "dd_missile_rack";
        foreach (var line in RackLines)
            if (id.StartsWith("bs_" + line + "_", StringComparison.Ordinal)) return "dd_" + id[3..];
        return id;
    }
    public static bool Fits(ItemDef i, GearSlot s, ShipClass c) => i != null && i.Slot == s && (i.Class == null || i.Class == c);
    public static GearSlot SlotAt(int k) => k < CoreSlots ? Core[k] : GearSlot.Chip;

    // Every weapon's damage on ANY class, for a part that lifts all of them. The list of what
    // counts as a weapon was written out here per class as well as in Progression; it is the
    // CLASS's to state now (ClassDef.Damage), and both read that one declaration. A stat a hull
    // does not have is simply not on its sheet, so a chip that names it changes nothing there.
    private static (string, double)[] AllDamage(double p) =>
        Classes.EveryDamageStat().Select(s => (s, p)).ToArray();

    // One specialisation at all three rarities: `up` is the lean (scaled by rarity), `down` its
    // price (not scaled), `add` whole-number changes, one value per rarity.
    private static IEnumerable<ItemDef> Line(string id, string name, GearSlot slot, ShipClass? cls, string blurb,
                                             (string stat, double pct)[] up, (string stat, double pct)[] down = null,
                                             (string stat, int[] byRarity)[] add = null)
    {
        for (int r = 0; r < Scale.Length; r++)
        {
            var it = new ItemDef { Id = $"{id}_{r + 1}", Name = name + Suffix[r], Slot = slot, Rarity = (Rarity)r, Class = cls, Blurb = blurb };
            foreach (var (s, p) in up) it.Pct[s] = it.Pct.GetValueOrDefault(s) + p * Scale[r];
            foreach (var (s, p) in down ?? Array.Empty<(string, double)>()) it.Pct[s] = it.Pct.GetValueOrDefault(s) + p;
            foreach (var (s, a) in add ?? Array.Empty<(string, int[])>()) it.Add[s] = a[r];
            yield return it;
        }
    }

    private static IEnumerable<ItemDef> Build()
    {
        // ── the starting kit: the ship's own numbers ──
        static ItemDef Kit(string id, string name, GearSlot s, ShipClass? c, string blurb) =>
            new() { Id = id, Name = name, Slot = s, Class = c, Kit = true, Blurb = blurb };
        yield return Kit("bs_main_battery", "Mk I Main Battery", GearSlot.Weapon, ShipClass.Battleship, "the four main turrets");
        yield return Kit("cv_fighter_hangars", "Mk I Fighter Hangars", GearSlot.Weapon, ShipClass.Carrier, "the fighter wing");
        yield return Kit("dd_main_battery", "Mk I Twin Turrets", GearSlot.Weapon, ShipClass.Destroyer, "the two main turrets");
        yield return Kit("std_drive", "Standard Drive Cluster", GearSlot.Engines, null, "the helm: speed and turning");
        yield return Kit("basic_deflector", "Basic Deflector Array", GearSlot.Shield, null, "the ship's hull points");
        yield return Kit("reinforced_frame", "Reinforced Frame", GearSlot.Hull, null, "the frame, and its point defence");
        yield return Kit("bs_broadside", "Broadside Battery", GearSlot.Utility, ShipClass.Battleship, "the broadside: every main gun, volley on volley");
        yield return Kit("cv_bomber_bay", "Bomber Bay", GearSlot.Utility, ShipClass.Carrier, "the bomber wing");
        yield return Kit("dd_missile_rack", "Missile Rack", GearSlot.Utility, ShipClass.Destroyer, "the missile bursts and their magazine");
        var basic = Kit("chip_basic", "Basic Combat Chip", GearSlot.Chip, null, "+5% damage, +5% hull");
        foreach (var (s, p) in AllDamage(0.05)) basic.Pct[s] = p;
        basic.Pct["hull"] = 0.05;
        yield return basic;

        var bs = ShipClass.Battleship; var cv = ShipClass.Carrier; var dd = ShipClass.Destroyer;
        IEnumerable<ItemDef>[] lines =
        {
            // ── battleship main battery ──
            Line("bs_rapid", "Rapid Battery", GearSlot.Weapon, bs, "fires far faster, hits lighter",
                 new[] { ("main_interval", 0.60) }, new[] { ("main_damage", -0.30) }),
            Line("bs_heavy", "Heavy Battery", GearSlot.Weapon, bs, "huge shells, slow to reload and to train",
                 new[] { ("main_damage", 0.80) }, new[] { ("main_interval", -0.35), ("main_turn", -0.30) }),
            Line("bs_long", "Long Battery", GearSlot.Weapon, bs, "reaches much further, a little lighter",
                 new[] { ("main_range", 0.40) }, new[] { ("main_damage", -0.15) }),
            Line("bs_track", "Tracking Battery", GearSlot.Weapon, bs, "turrets that swing fast; shorter reach",
                 new[] { ("main_turn", 1.00), ("main_damage", 0.10) }, new[] { ("main_range", -0.15) }),
            // ── carrier fighter hangars: the owner's three (Elite III is two fighters, +200% speed and
            //    damage, +50% turning; Swarm III is nine, 22% slower and weaker; Line is between) ──
            Line("cv_elite", "Elite Hangars", GearSlot.Weapon, cv, "two fighters, very fast and very deadly",
                 new[] { ("fighter_speed", 1.00), ("fighter_damage", 1.00), ("fighter_turn", 0.25) }, null,
                 new[] { ("fighter_count", new[] { -1, -1, -1 }) }),
            Line("cv_swarm", "Swarm Hangars", GearSlot.Weapon, cv, "a swarm of slower, weaker fighters",
                 None, new[] { ("fighter_speed", -0.22), ("fighter_damage", -0.22) },
                 new[] { ("fighter_count", new[] { 3, 4, 6 }) }),
            Line("cv_line", "Line Hangars", GearSlot.Weapon, cv, "one more fighter, a little better all round",
                 new[] { ("fighter_damage", 0.20), ("fighter_speed", 0.10) }, null,
                 new[] { ("fighter_count", new[] { 1, 1, 1 }) }),
            Line("cv_leash", "Leash Hangars", GearSlot.Weapon, cv, "fighters that fight far from the carrier",
                 new[] { ("control_range", 0.40), ("fighter_range", 0.20) }, new[] { ("fighter_damage", -0.10) }),
            // ── destroyer main turrets: the battery's four leans, on two guns ──
            Line("dd_rapid", "Rapid Twins", GearSlot.Weapon, dd, "fires far faster, hits lighter",
                 new[] { ("main_interval", 0.60) }, new[] { ("main_damage", -0.30) }),
            Line("dd_heavy", "Heavy Twins", GearSlot.Weapon, dd, "huge shells, slow to reload and to train",
                 new[] { ("main_damage", 0.80) }, new[] { ("main_interval", -0.35), ("main_turn", -0.30) }),
            Line("dd_long", "Long Twins", GearSlot.Weapon, dd, "reaches much further, a little lighter",
                 new[] { ("main_range", 0.40) }, new[] { ("main_damage", -0.15) }),
            Line("dd_track", "Tracking Twins", GearSlot.Weapon, dd, "turrets that swing fast; shorter reach",
                 new[] { ("main_turn", 1.00), ("main_damage", 0.10) }, new[] { ("main_range", -0.15) }),
            // ── battleship broadside ──
            Line("bs_heavyside", "Heavy Broadside", GearSlot.Utility, bs, "shells that hit far harder; a longer cooldown",
                 new[] { ("broadside_mult", 0.40) }, new[] { ("broadside_cooldown", -0.25) }),
            Line("bs_rapidside", "Rapid Broadside", GearSlot.Utility, bs, "back far sooner, lighter shells",
                 new[] { ("broadside_cooldown", 0.50) }, new[] { ("broadside_mult", -0.25) }),
            Line("bs_barrage", "Barrage Broadside", GearSlot.Utility, bs, "more volleys; slower to aim and to return",
                 None, new[] { ("broadside_windup", -0.30), ("broadside_cooldown", -0.20) },
                 new[] { ("broadside_volleys", new[] { 1, 2, 3 }) }),
            Line("bs_snap", "Snap Broadside", GearSlot.Utility, bs, "fires almost at once; one volley fewer",
                 new[] { ("broadside_windup", 1.00) }, null,
                 new[] { ("broadside_volleys", new[] { -1, -1, -1 }) }),
            // ── destroyer missile rack ──
            Line("dd_salvo", "Salvo Rack", GearSlot.Utility, dd, "more bursts a magazine, a slower reload",
                 None, new[] { ("missile_reload", -0.25), ("missile_damage", -0.20) },
                 new[] { ("missile_mag", new[] { 1, 2, 3 }) }),
            Line("dd_buster", "Buster Rack", GearSlot.Utility, dd, "devastating missiles, slow and clumsy",
                 new[] { ("missile_damage", 1.00) }, new[] { ("missile_speed", -0.20), ("missile_turn", -0.30) }),
            Line("dd_seeker", "Seeker Rack", GearSlot.Utility, dd, "missiles that hunt; lighter warheads",
                 new[] { ("missile_turn", 1.00), ("missile_speed", 0.20) }, new[] { ("missile_damage", -0.20) }),
            Line("dd_loader", "Loader Rack", GearSlot.Utility, dd, "reloads fast, lighter warheads",
                 new[] { ("missile_reload", 0.60), ("missile_refire", 0.40) }, new[] { ("missile_damage", -0.25) }),
            // ── carrier bomber bay ──
            Line("cv_heavybay", "Heavy Bay", GearSlot.Utility, cv, "one bomber, torpedoes that break ships",
                 new[] { ("torpedo_damage", 0.80) }, new[] { ("bomber_speed", -0.20) },
                 new[] { ("bomber_count", new[] { -1, -1, -1 }) }),
            Line("cv_widebay", "Wide Bay", GearSlot.Utility, cv, "more bombers, lighter torpedoes",
                 None, new[] { ("torpedo_damage", -0.30) },
                 new[] { ("bomber_count", new[] { 1, 2, 3 }) }),
            Line("cv_longbay", "Long Bay", GearSlot.Utility, cv, "torpedoes launched from far off",
                 new[] { ("torpedo_range", 0.40), ("launch_range", 0.30), ("torpedo_speed", 0.20) }, new[] { ("torpedo_damage", -0.15) }),
            Line("cv_rapidbay", "Rapid Bay", GearSlot.Utility, cv, "rearms fast, more torpedoes a run",
                 new[] { ("bomber_rearm", 0.60) }, new[] { ("torpedo_damage", -0.20) },
                 new[] { ("bomber_ammo", new[] { 1, 2, 3 }) }),
            // ── engines ──
            Line("drv_racing", "Racing Drive", GearSlot.Engines, null, "much faster, turns wider",
                 new[] { ("max_speed", 0.25), ("thrust", 0.30) }, new[] { ("turn_rate", -0.25) }),
            Line("drv_helm", "Helm Drive", GearSlot.Engines, null, "turns tight and quick, slower",
                 new[] { ("turn_rate", 0.40), ("turn_radius", 0.30) }, new[] { ("max_speed", -0.15) }),
            Line("drv_surge", "Surge Drive", GearSlot.Engines, null, "leaps forward and back, a lower top speed",
                 new[] { ("thrust", 0.80), ("reverse_thrust", 0.80) }, new[] { ("max_speed", -0.10) }),
            Line("drv_cruiser", "Cruiser Drive", GearSlot.Engines, null, "fast ahead and astern, slow to get going",
                 new[] { ("max_speed", 0.15), ("reverse_speed", 0.50) }, new[] { ("thrust", -0.20) }),
            // ── shield: the hull points ──
            Line("sh_bulwark", "Bulwark Array", GearSlot.Shield, null, "a great deal of hull, slower",
                 new[] { ("hull", 0.40) }, new[] { ("max_speed", -0.15) }),
            Line("sh_balanced", "Balanced Array", GearSlot.Shield, null, "more hull, a longer point defence",
                 new[] { ("hull", 0.20), ("pd_range", 0.10) }),
            Line("sh_glass", "Glass Array", GearSlot.Shield, null, "every weapon hits harder; less hull",
                 AllDamage(0.20), new[] { ("hull", -0.25) }),
            Line("sh_screen", "Screen Array", GearSlot.Shield, null, "more hull, sluggish off the mark",
                 new[] { ("hull", 0.25) }, new[] { ("thrust", -0.10) }),
            // ── hull: the frame and its point defence ──
            Line("fr_flak", "Flak Frame", GearSlot.Hull, null, "point defence that shreds, shorter",
                 new[] { ("pd_damage", 0.60), ("pd_interval", 0.30) }, new[] { ("pd_range", -0.15) }),
            Line("fr_picket", "Picket Frame", GearSlot.Hull, null, "point defence that reaches, lighter",
                 new[] { ("pd_range", 0.40), ("pd_turn", 0.50) }, new[] { ("pd_damage", -0.20) }),
            Line("fr_endurance", "Endurance Frame", GearSlot.Hull, null, "point defence that fires longer and returns sooner",
                 new[] { ("pd_active", 0.50), ("pd_reload", 0.30) }),
            Line("fr_armour", "Armoured Frame", GearSlot.Hull, null, "a tougher frame, a shorter point-defence window",
                 new[] { ("hull", 0.20) }, new[] { ("pd_active", -0.30) }),
            // ── chips ──
            Line("chip_combat", "Combat Chip", GearSlot.Chip, null, "every weapon hits harder; a little less hull",
                 AllDamage(0.08), new[] { ("hull", -0.05) }),
            Line("chip_armour", "Armour Chip", GearSlot.Chip, null, "more hull; a little less damage",
                 new[] { ("hull", 0.08) }, AllDamage(-0.05)),
            Line("chip_engine", "Engine Chip", GearSlot.Chip, null, "faster, and turns quicker",
                 new[] { ("max_speed", 0.06), ("turn_rate", 0.06) }),
            Line("chip_target", "Targeting Chip", GearSlot.Chip, null, "every weapon reaches further",
                 Ranges.Select(r => (r, 0.08)).ToArray()),
        };
        foreach (var l in lines) foreach (var it in l) yield return it;
    }

    // the loadout: 5 core slots in order, then 5 chips ("" = empty)
    public static string[] Default(ShipClass c)
    {
        var (weapon, utility) = c switch
        {
            ShipClass.Carrier   => ("cv_fighter_hangars", "cv_bomber_bay"),
            ShipClass.Destroyer => ("dd_main_battery", "dd_missile_rack"),
            _                   => ("bs_main_battery", "bs_broadside"),
        };
        return new[] { weapon, "std_drive", "basic_deflector", "reinforced_frame", utility,
                       "chip_basic", "chip_basic", "chip_basic", "chip_basic", "chip_basic" };
    }

    // a loadout made safe: known items in the right slots for the class; a bad core slot gets
    // its default, a bad chip slot is emptied (what the host does with a pilot's claim)
    public static string[] Sanitize(ShipClass c, string[] ids)
    {
        var d = Default(c); var o = new string[Slots];
        for (int k = 0; k < Slots; k++)
        {
            string id = ids != null && k < ids.Length ? ids[k] ?? "" : (k < CoreSlots ? d[k] : "");
            o[k] = Fits(ById(id), SlotAt(k), c) ? id : (k < CoreSlots ? d[k] : "");
        }
        return o;
    }

    // What a loadout does to the sheet, summed per stat over its SANITISED parts, so a part in the
    // wrong slot or for another class changes nothing: the shares, and the whole additions.
    public static Dictionary<string, double> Bonuses(ShipClass c, string[] loadout) => Sum(c, loadout, i => i.Pct);
    public static Dictionary<string, double> Adds(ShipClass c, string[] loadout) => Sum(c, loadout, i => i.Add.ToDictionary(kv => kv.Key, kv => (double)kv.Value));
    private static Dictionary<string, double> Sum(ShipClass c, string[] loadout, Func<ItemDef, IReadOnlyDictionary<string, double>> part)
    {
        var d = new Dictionary<string, double>();
        foreach (var it in Sanitize(c, loadout).Select(ById).Where(i => i != null))
            d = ShipStats.Sum(d, part(it));
        return d;
    }

    // A part's effects in words, for a class: "Fighters · Top speed +100%". A share on an interval
    // or a radius is shown as what it does to the number the K window prints (+60% rate is a 38%
    // shorter reload). Stats the class does not have are left out -- a general part names only
    // what it changes on THIS ship.
    public static IEnumerable<string> Describe(ItemDef item, ShipClass c)
    {
        if (item == null) yield break;
        var sheet = new ShipStats(c).All.ToDictionary(s => s.Id);
        foreach (var (id, p) in item.Pct.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (sheet.TryGetValue(id, out var s))
            {
                double change = s.Inverse ? 1.0 / (1.0 + p) - 1.0 : p;
                yield return $"{s.Group} · {s.Label} {(change >= 0 ? "+" : "−")}{Math.Abs(change) * 100:0}%";
            }
        foreach (var (id, n) in item.Add.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (sheet.TryGetValue(id, out var s))
                yield return $"{s.Group} · {s.Label} {(n >= 0 ? "+" : "−")}{Math.Abs(n)}";
    }
}
