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
    // THE STATS IT MOVES, and so what a hull must have for it to be worth wearing: a part fits a
    // hull that has AT LEAST ONE of them, because a part whose every stat is missing there would
    // do nothing at all. A Rapid Battery moves main_interval and main_damage, so it fits every
    // class with main guns -- eleven of the twelve -- without naming one of them; Elite Hangars
    // move fighter_speed, so only a carrier can wear them; a Basic Combat Chip moves every
    // weapon stat in the game, so it fits everything, and lifts whichever of them that hull has.
    // It was one ShipClass before, which is why the nine new classes could wear nothing but the
    // parts that named no class at all.
    public string[] Needs = System.Array.Empty<string>();
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
    // A class's own sheet, by stat id -- built once per class, and lazily: Equipment's static
    // fields are built before this is ever asked, and a sheet read during that would be a cycle.
    private static readonly Dictionary<ShipClass, HashSet<string>> SheetCache = new();
    private static HashSet<string> Sheet(ShipClass c) =>
        SheetCache.TryGetValue(c, out var h) ? h : SheetCache[c] = new ShipStats(c).All.Select(x => x.Id).ToHashSet();
    // Which classes name a given KIT part in their row (Ships.cs). A kit part is the ship's own
    // hardware: it fits the hulls that are born with it, and no others.
    private static readonly Dictionary<string, HashSet<ShipClass>> KitOwners =
        Classes.All.SelectMany(d => d.Kit.Select(k => (k.Id, d.Id)))
               .GroupBy(t => t.Item1).ToDictionary(g => g.Key, g => g.Select(t => t.Item2).ToHashSet());

    // It fits if the slot matches AND the hull has every stat it moves. No part names a class.
    public static bool Fits(ItemDef i, GearSlot s, ShipClass c)
    {
        if (i == null || i.Slot != s) return false;
        if (KitOwners.TryGetValue(i.Id, out var born)) return born.Contains(c);
        if (i.Needs.Length == 0) return true;             // a part that moves nothing fits anything
        var sheet = Sheet(c);
        foreach (var need in i.Needs) if (sheet.Contains(need)) return true;
        return false;
    }
    // What a part asks of a hull, in words, for the equipment window: the SYSTEMS it moves, ANY ONE
    // of which is enough (Fits) -- "Fighters", "Hull or Point defence". It printed the raw stat ids
    // joined by commas ("needs fighter_speed, fighter_damage, fighter_turn, fighter_count"), which
    // named nothing a player has ever seen and read as a list of demands rather than a choice.
    // Describe could not help: the only sheet it can read is the CURRENT class's, which by
    // definition has not got them. AllStats (Stats.cs) holds every class's.
    public static string NeedsSaid(ItemDef i) =>
        KitOwners.TryGetValue(i?.Id ?? "", out var born)
            ? string.Join(" / ", born.Select(Classes.NameOf))
            : i == null || i.Needs.Length == 0 ? "any hull"
            : string.Join(" or ", i.Needs.Select(AllStats.Group).Distinct());
    public static GearSlot SlotAt(int k) => k < CoreSlots ? Core[k] : GearSlot.Chip;

    // Every weapon's damage on ANY class, for a part that lifts all of them. The list of what
    // counts as a weapon was written out here per class as well as in Progression; it is the
    // CLASS's to state now (ClassDef.Damage), and both read that one declaration. A stat a hull
    // does not have is simply not on its sheet, so a chip that names it changes nothing there.
    private static (string, double)[] AllDamage(double p) =>
        Classes.EveryDamageStat().Select(s => (s, p)).ToArray();

    // One specialisation at all three rarities: `up` is the lean (scaled by rarity), `down` its
    // price (not scaled), `add` whole-number changes, one value per rarity.
    private static IEnumerable<ItemDef> Line(string id, string name, GearSlot slot, string blurb,
                                             (string stat, double pct)[] up, (string stat, double pct)[] down = null,
                                             (string stat, int[] byRarity)[] add = null)
    {
        // What it needs is what it moves: every stat id it names, once.
        var needs = (up ?? None).Select(t => t.stat)
            .Concat((down ?? None).Select(t => t.stat))
            .Concat((add ?? Array.Empty<(string, int[])>()).Select(t => t.stat))
            .Distinct().ToArray();
        for (int r = 0; r < Scale.Length; r++)
        {
            var it = new ItemDef { Id = $"{id}_{r + 1}", Name = name + Suffix[r], Slot = slot, Rarity = (Rarity)r, Needs = needs, Blurb = blurb };
            foreach (var (s, p) in up) it.Pct[s] = it.Pct.GetValueOrDefault(s) + p * Scale[r];
            foreach (var (s, p) in down ?? Array.Empty<(string, double)>()) it.Pct[s] = it.Pct.GetValueOrDefault(s) + p;
            foreach (var (s, a) in add ?? Array.Empty<(string, int[])>()) it.Add[s] = a[r];
            yield return it;
        }
    }

    private static IEnumerable<ItemDef> Build()
    {
        // ── the starting kit: the ship's own numbers ──
        static ItemDef Kit(string id, string name, GearSlot s, string blurb) =>
            new() { Id = id, Name = name, Slot = s, Kit = true, Blurb = blurb };
        // EVERY CLASS'S OWN, from its row (ClassDef.Kit): its weapon mount and its signature
        // system. Two classes may name the same id -- three freighters share one cargo gun -- so
        // the part is built once.
        foreach (var k in Classes.All.SelectMany(d => d.Kit).GroupBy(k => k.Id).Select(g => g.First()))
            yield return Kit(k.Id, k.Name, k.Slot, k.Blurb);
        // ...and the four every ship in the game is born with
        yield return Kit("std_drive", "Standard Drive Cluster", GearSlot.Engines, "the helm: speed and turning");
        yield return Kit("basic_deflector", "Basic Deflector Array", GearSlot.Shield, "the ship's hull points");
        yield return Kit("reinforced_frame", "Reinforced Frame", GearSlot.Hull, "the frame, and its point defence");
        var basic = Kit("chip_basic", "Basic Combat Chip", GearSlot.Chip, "+5% damage, +5% hull");
        foreach (var (s, p) in AllDamage(0.05)) basic.Pct[s] = p;
        basic.Pct["hull"] = 0.05;
        yield return basic;

        IEnumerable<ItemDef>[] lines =
        {
            // ── battleship main battery ──
            Line("bs_rapid", "Rapid Battery", GearSlot.Weapon, "fires far faster, hits lighter",
                 new[] { ("main_interval", 0.60) }, new[] { ("main_damage", -0.30) }),
            Line("bs_heavy", "Heavy Battery", GearSlot.Weapon, "huge shells, slow to reload and to train",
                 new[] { ("main_damage", 0.80) }, new[] { ("main_interval", -0.35), ("main_turn", -0.30) }),
            Line("bs_long", "Long Battery", GearSlot.Weapon, "reaches much further, a little lighter",
                 new[] { ("main_range", 0.40) }, new[] { ("main_damage", -0.15) }),
            Line("bs_track", "Tracking Battery", GearSlot.Weapon, "turrets that swing fast; shorter reach",
                 new[] { ("main_turn", 1.00), ("main_damage", 0.10) }, new[] { ("main_range", -0.15) }),
            // ── carrier fighter hangars: the owner's three (Elite III is two fighters, +200% speed and
            //    damage, +50% turning; Swarm III is nine, 22% slower and weaker; Line is between) ──
            Line("cv_elite", "Elite Hangars", GearSlot.Weapon, "two fighters, very fast and very deadly",
                 new[] { ("fighter_speed", 1.00), ("fighter_damage", 1.00), ("fighter_turn", 0.25) }, null,
                 new[] { ("fighter_count", new[] { -1, -1, -1 }) }),
            Line("cv_swarm", "Swarm Hangars", GearSlot.Weapon, "a swarm of slower, weaker fighters",
                 None, new[] { ("fighter_speed", -0.22), ("fighter_damage", -0.22) },
                 new[] { ("fighter_count", new[] { 3, 4, 6 }) }),
            Line("cv_line", "Line Hangars", GearSlot.Weapon, "one more fighter, a little better all round",
                 new[] { ("fighter_damage", 0.20), ("fighter_speed", 0.10) }, null,
                 new[] { ("fighter_count", new[] { 1, 1, 1 }) }),
            Line("cv_leash", "Leash Hangars", GearSlot.Weapon, "fighters that fight far from the carrier",
                 new[] { ("control_range", 0.40), ("fighter_range", 0.20) }, new[] { ("fighter_damage", -0.10) }),
            // ── destroyer main turrets: the battery's four leans, on two guns ──
            Line("dd_rapid", "Rapid Twins", GearSlot.Weapon, "fires far faster, hits lighter",
                 new[] { ("main_interval", 0.60) }, new[] { ("main_damage", -0.30) }),
            Line("dd_heavy", "Heavy Twins", GearSlot.Weapon, "huge shells, slow to reload and to train",
                 new[] { ("main_damage", 0.80) }, new[] { ("main_interval", -0.35), ("main_turn", -0.30) }),
            Line("dd_long", "Long Twins", GearSlot.Weapon, "reaches much further, a little lighter",
                 new[] { ("main_range", 0.40) }, new[] { ("main_damage", -0.15) }),
            Line("dd_track", "Tracking Twins", GearSlot.Weapon, "turrets that swing fast; shorter reach",
                 new[] { ("main_turn", 1.00), ("main_damage", 0.10) }, new[] { ("main_range", -0.15) }),
            // ── battleship broadside ──
            Line("bs_heavyside", "Heavy Broadside", GearSlot.Utility, "shells that hit far harder; a longer cooldown",
                 new[] { ("broadside_mult", 0.40) }, new[] { ("broadside_cooldown", -0.25) }),
            Line("bs_rapidside", "Rapid Broadside", GearSlot.Utility, "back far sooner, lighter shells",
                 new[] { ("broadside_cooldown", 0.50) }, new[] { ("broadside_mult", -0.25) }),
            Line("bs_barrage", "Barrage Broadside", GearSlot.Utility, "more volleys; slower to aim and to return",
                 None, new[] { ("broadside_windup", -0.30), ("broadside_cooldown", -0.20) },
                 new[] { ("broadside_volleys", new[] { 1, 2, 3 }) }),
            Line("bs_snap", "Snap Broadside", GearSlot.Utility, "fires almost at once; one volley fewer",
                 new[] { ("broadside_windup", 1.00) }, null,
                 new[] { ("broadside_volleys", new[] { -1, -1, -1 }) }),
            // ── destroyer missile rack ──
            Line("dd_salvo", "Salvo Rack", GearSlot.Utility, "more bursts a magazine, a slower reload",
                 None, new[] { ("missile_reload", -0.25), ("missile_damage", -0.20) },
                 new[] { ("missile_mag", new[] { 1, 2, 3 }) }),
            Line("dd_buster", "Buster Rack", GearSlot.Utility, "devastating missiles, slow and clumsy",
                 new[] { ("missile_damage", 1.00) }, new[] { ("missile_speed", -0.20), ("missile_turn", -0.30) }),
            Line("dd_seeker", "Seeker Rack", GearSlot.Utility, "missiles that hunt; lighter warheads",
                 new[] { ("missile_turn", 1.00), ("missile_speed", 0.20) }, new[] { ("missile_damage", -0.20) }),
            Line("dd_loader", "Loader Rack", GearSlot.Utility, "reloads fast, lighter warheads",
                 new[] { ("missile_reload", 0.60), ("missile_refire", 0.40) }, new[] { ("missile_damage", -0.25) }),
            // ── carrier bomber bay ──
            Line("cv_heavybay", "Heavy Bay", GearSlot.Utility, "one bomber, torpedoes that break ships",
                 new[] { ("torpedo_damage", 0.80) }, new[] { ("bomber_speed", -0.20) },
                 new[] { ("bomber_count", new[] { -1, -1, -1 }) }),
            Line("cv_widebay", "Wide Bay", GearSlot.Utility, "more bombers, lighter torpedoes",
                 None, new[] { ("torpedo_damage", -0.30) },
                 new[] { ("bomber_count", new[] { 1, 2, 3 }) }),
            Line("cv_longbay", "Long Bay", GearSlot.Utility, "torpedoes launched from far off",
                 new[] { ("torpedo_range", 0.40), ("launch_range", 0.30), ("torpedo_speed", 0.20) }, new[] { ("torpedo_damage", -0.15) }),
            Line("cv_rapidbay", "Rapid Bay", GearSlot.Utility, "rearms fast, more torpedoes a run",
                 new[] { ("bomber_rearm", 0.60) }, new[] { ("torpedo_damage", -0.20) },
                 new[] { ("bomber_ammo", new[] { 1, 2, 3 }) }),
            // ── engines ──
            Line("drv_racing", "Racing Drive", GearSlot.Engines, "much faster, turns wider",
                 new[] { ("max_speed", 0.25), ("thrust", 0.30) }, new[] { ("turn_rate", -0.25) }),
            Line("drv_helm", "Helm Drive", GearSlot.Engines, "turns tight and quick, slower",
                 new[] { ("turn_rate", 0.40), ("turn_radius", 0.30) }, new[] { ("max_speed", -0.15) }),
            Line("drv_surge", "Surge Drive", GearSlot.Engines, "leaps forward and back, a lower top speed",
                 new[] { ("thrust", 0.80), ("reverse_thrust", 0.80) }, new[] { ("max_speed", -0.10) }),
            Line("drv_cruiser", "Cruiser Drive", GearSlot.Engines, "fast ahead and astern, slow to get going",
                 new[] { ("max_speed", 0.15), ("reverse_speed", 0.50) }, new[] { ("thrust", -0.20) }),
            // ── shield: the hull points ──
            Line("sh_bulwark", "Bulwark Array", GearSlot.Shield, "a great deal of hull, slower",
                 new[] { ("hull", 0.40) }, new[] { ("max_speed", -0.15) }),
            Line("sh_balanced", "Balanced Array", GearSlot.Shield, "more hull, a longer point defence",
                 new[] { ("hull", 0.20), ("pd_range", 0.10) }),
            Line("sh_glass", "Glass Array", GearSlot.Shield, "every weapon hits harder; less hull",
                 AllDamage(0.20), new[] { ("hull", -0.25) }),
            Line("sh_screen", "Screen Array", GearSlot.Shield, "more hull, sluggish off the mark",
                 new[] { ("hull", 0.25) }, new[] { ("thrust", -0.10) }),
            // ── hull: the frame and its point defence ──
            Line("fr_flak", "Flak Frame", GearSlot.Hull, "point defence that shreds, shorter",
                 new[] { ("pd_damage", 0.60), ("pd_interval", 0.30) }, new[] { ("pd_range", -0.15) }),
            Line("fr_picket", "Picket Frame", GearSlot.Hull, "point defence that reaches, lighter",
                 new[] { ("pd_range", 0.40), ("pd_turn", 0.50) }, new[] { ("pd_damage", -0.20) }),
            Line("fr_endurance", "Endurance Frame", GearSlot.Hull, "point defence that fires longer and returns sooner",
                 new[] { ("pd_active", 0.50), ("pd_reload", 0.30) }),
            Line("fr_armour", "Armoured Frame", GearSlot.Hull, "a tougher frame, a shorter point-defence window",
                 new[] { ("hull", 0.20) }, new[] { ("pd_active", -0.30) }),
            // ── frames for a hull with no point defence to reinforce ──
            // The four above are point defence's: nine of their twelve parts move nothing but pd_
            // stats, so the five classes that mount no point defence -- the sniper, the warrior,
            // the dart, the echo and the wraith -- could wear three of the twelve. These three
            // families are about the HULL itself, so every ship in the game can wear them.
            Line("fr_braced", "Braced Frame", GearSlot.Hull, "a heavier frame: it takes more, and comes round slower",
                 new[] { ("hull", 0.30) }, new[] { ("turn_rate", -0.20), ("max_speed", -0.10) }),
            Line("fr_spar", "Spar Frame", GearSlot.Hull, "stripped to the spars: it turns far harder, and dents",
                 new[] { ("turn_rate", 0.45), ("turn_radius", 0.20) }, new[] { ("hull", -0.20) }),
            Line("fr_keel", "Keel Brace", GearSlot.Hull, "it holds the water: no drift, and a little heavy",
                 new[] { ("keel", 0.60), ("water_drag", 0.25) }, new[] { ("max_speed", -0.08) }),
            // ── frames for the turrets a freighter carries about ──
            // Its deployed turrets ARE its frame: these only mean anything on a hull that drops them.
            Line("cr_cradle", "Turret Cradle", GearSlot.Hull, "the turrets it drops stand up to far more",
                 new[] { ("deploy_hull", 0.60) }, new[] { ("deploy_range", -0.15) }),
            Line("cr_rack", "Launch Rack", GearSlot.Hull, "drops them far sooner, and lighter",
                 new[] { ("deploy_cooldown", 0.50) }, new[] { ("deploy_damage", -0.20) }),
            Line("cr_heavy", "Heavy Cradle", GearSlot.Hull, "turrets that hit much harder and reach further; slow to drop",
                 new[] { ("deploy_damage", 0.45), ("deploy_range", 0.25) }, new[] { ("deploy_cooldown", -0.30) }),
            // ── chips ──
            Line("chip_combat", "Combat Chip", GearSlot.Chip, "every weapon hits harder; a little less hull",
                 AllDamage(0.08), new[] { ("hull", -0.05) }),
            Line("chip_armour", "Armour Chip", GearSlot.Chip, "more hull; a little less damage",
                 new[] { ("hull", 0.08) }, AllDamage(-0.05)),
            Line("chip_engine", "Engine Chip", GearSlot.Chip, "faster, and turns quicker",
                 new[] { ("max_speed", 0.06), ("turn_rate", 0.06) }),
            Line("chip_target", "Targeting Chip", GearSlot.Chip, "every weapon reaches further",
                 Ranges.Select(r => (r, 0.08)).ToArray()),
        };
        foreach (var l in lines) foreach (var it in l) yield return it;
    }

    // the loadout: 5 core slots in order, then 5 chips ("" = empty)
    // What a ship of this class comes out of the yard with: ITS OWN two parts, from its row
    // (ClassDef.Kit), and the four every ship is born with. It used to name three classes here and
    // hand everything else the battleship's mounts -- which the nine new classes cannot even wear,
    // so their weapon and utility slots came up empty.
    public static string[] Default(ShipClass c)
    {
        var kit = Classes.Of(c).Kit;
        string Own(GearSlot s) => kit.FirstOrDefault(k => k.Slot == s)?.Id ?? "";
        return new[] { Own(GearSlot.Weapon), "std_drive", "basic_deflector", "reinforced_frame", Own(GearSlot.Utility),
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
