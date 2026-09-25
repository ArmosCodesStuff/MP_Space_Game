using System;
using System.Collections.Generic;
using System.Linq;

// EQUIPMENT -- a ship's parts, in slots. The menu (I) shows the gear of the pilot's CURRENT class;
// each class keeps its own loadout (saved with the character), so gear left on a ship is still on
// it when the pilot switches back. Parts change no looks, only numbers.
//   Slots: Weapon, Engines, Shield (the hull points), Hull (the frame and its point defence),
//   Utility -- and SIX Chips on every hull, opened in order by the pilot's highest level (Unlocks:
//   slot 1 at level 2 ... slot 6 at 14), with at most three of each ChipKind (KindCap).
//
// THE STARTING KIT (Mk I etc.) reproduces the ship's own numbers and fits no chips: a ship is
// balanced as its class, not as its class plus a kit. EVERYTHING ELSE DROPS from bosses (Loot):
// the LINES of Items.cs, by HULL CATEGORY, each at TEN TIERS (x1.10 a tier on the upside, the
// price fixed), so a higher tier of a line is strictly better. A line fits every hull of its
// category; a chip fits every hull.
//
// A part is a set of stat changes: shares (Pct: +0.30 = +30%; on an interval or a radius the
// share is a RATE and divides, see Stat) and whole additions (Add: one more volley). A share's key
// may be a ROLE (Items.Expand: "@output" is every ability output the hull has). The host applies
// every pilot's gear (it resolves damage and hull): the loadout, and the levels its pilot bought
// for its slots, travel with the identity and are sanitised on arrival (Sanitize, SanitizeLevels).
public enum GearSlot { Weapon, Engines, Shield, Hull, Utility, Chip }
// WHAT A CHIP IS FOR: a hull carries at most Equipment.KindCap of each (the fit rule beside Fits).
public enum ChipKind { Combat, Utility }

public class ItemDef
{
    public string Id, Name, Blurb;
    public GearSlot Slot;
    public int Tier = 1;                     // 1-10 (Tiers); a kit part is tier 1
    public HullCat? Cat;                     // the hull category it fits; null: any hull (chips, the kit)
    public ItemLine Line;                    // the row it was built from (Items.Lines); null for a kit part
    public ChipKind Kind;                    // a chip's kind (Equipment.KindCap); no other slot reads it
    // WHAT A HULL MUST HAVE FOR THIS TO FIT, by stat id: a part fits a hull that has AT LEAST ONE
    // of them (and is of its category, if it has one). A RIDER names its count rows (Magazine Core:
    // the broadside's volleys, a bomber's torpedoes), so the destroyer, with neither, cannot wear it.
    // A HULL'S OWN hardware (Kit, built by Own below) names the SIGNATURE row of the hulls born with
    // it, so one id names one hull, and three ids the three freighters that share a cargo gun.
    public string[] Needs = System.Array.Empty<string>();
    public bool Kit;                         // a hull's own hardware: never dropped
    // THE STATS THIS PART IS FOR -- its line's ups, as opposed to what it takes in exchange.
    // Levelling a slot with salvage improves these and leaves the price exactly as printed.
    public string[] Ups = System.Array.Empty<string>();
    public Dictionary<string, double> Pct = new(), Add = new();

    // A HULL'S OWN HARDWARE, as a class row writes it (ClassDef.Kit) and as Build yields it: a
    // label with a name and a blurb that changes no number (the sheet already IS the ship),
    // fitting the hulls whose signature rows it names. Its id is what a SAVE FILE holds.
    // STATIC ORDER: Classes.All builds these while IT is initialising, so this may touch no
    // static field of Equipment (Equipment.All reads Classes.All, and the two would deadlock).
    public static ItemDef Own(GearSlot slot, string id, string name, string blurb, params string[] needs) =>
        new() { Slot = slot, Id = id, Name = name, Blurb = blurb, Kit = true, Needs = needs };
}

public static class Equipment
{
    // Six chip slots on every hull, opened in order by the pilot's peak (Unlocks, Opens.ChipSlot),
    // and at most KindCap chips of each ChipKind among them.
    public const int CoreSlots = 5, ChipSlots = 6, Slots = CoreSlots + ChipSlots, KindCap = 3;
    public static readonly GearSlot[] Core = { GearSlot.Weapon, GearSlot.Engines, GearSlot.Shield, GearSlot.Hull, GearSlot.Utility };

    public static readonly ItemDef[] All = Build().ToArray();
    private static readonly Dictionary<string, ItemDef> ByIdMap = All.ToDictionary(i => i.Id);
    public static ItemDef ById(string id) => id != null && ByIdMap.TryGetValue(id, out var i) ? i : null;
    // what can drop: everything but the kit
    public static IEnumerable<ItemDef> Drops => All.Where(i => !i.Kit);

    // A class's own bare sheet -- built once per class, and lazily: Equipment's static fields are
    // built before this is ever asked, and a sheet read during that would be a cycle.
    private static readonly Dictionary<ShipClass, ShipStats> SheetCache = new();
    private static ShipStats Bare(ShipClass c) => SheetCache.TryGetValue(c, out var s) ? s : SheetCache[c] = new ShipStats(c);
    public static HashSet<string> SheetOf(ShipClass c) => Bare(c).All.Select(x => x.Id).ToHashSet();
    // a row's own number on a bare hull of class `c` (0 where it has no such row)
    public static double BaseOf(ShipClass c, string id) => Bare(c)[id];
    // THE ONE RULE: it fits if the slot matches, the hull is of the part's category (if it has
    // one), and the hull has one of the stats the part asks for (if it asks any).
    public static bool Fits(ItemDef i, GearSlot s, ShipClass c)
    {
        if (i == null || i.Slot != s) return false;
        if (i.Cat is { } cat && global::Hulls.Of(c) != cat) return false;
        if (i.Needs.Length == 0) return true;             // a part that asks nothing fits anything
        var sheet = SheetOf(c);
        foreach (var need in i.Needs) if (sheet.Contains(need)) return true;
        return false;
    }
    // Every hull that can wear it, in the order the classes are written.
    private static IEnumerable<ShipClass> Hulls(ItemDef i) =>
        Classes.All.Where(d => d.Ready && Fits(i, i.Slot, d.Id)).Select(d => d.Id);
    // What a part asks of a hull, in words, for the equipment window -- worked out from the same
    // one rule, so a kit part and a dropped part are said the same way. THREE HULLS OR FEWER and it
    // names them ("BATTLESHIP / CARRIER" for Magazine Core); a category line names its category;
    // otherwise the systems it needs, ANY ONE of which is enough (Fits).
    public static string NeedsSaid(ItemDef i)
    {
        if (i == null || (i.Needs.Length == 0 && i.Cat == null)) return "any hull";
        var hulls = Hulls(i).ToList();
        return hulls.Count > 0 && hulls.Count <= 3 ? string.Join(" / ", hulls.Select(Classes.NameOf))
             : i.Cat is { } cat ? global::Hulls.NameOf(cat)
             : string.Join(" or ", i.Needs.Select(AllStats.Group).Distinct());
    }
    public static GearSlot SlotAt(int k) => k < CoreSlots ? Core[k] : GearSlot.Chip;

    private static IEnumerable<ItemDef> Build()
    {
        // ── a hull's own hardware: the ship's own numbers, and never a drop ──
        // EVERY CLASS'S OWN, out of its row (ClassDef.Kit). Several classes may carry the SAME part
        // (three freighters share one cargo gun), written once and yielded once.
        foreach (var k in Classes.All.SelectMany(d => d.Kit).GroupBy(k => k.Id).Select(g => g.First()))
            yield return k;
        // ...and the three every ship in the game is born with, which ask nothing of a hull
        yield return ItemDef.Own(GearSlot.Engines, "std_drive", "Standard Drive Cluster", "the helm: speed and turning");
        yield return ItemDef.Own(GearSlot.Shield, "basic_deflector", "Basic Deflector Array", "the ship's hull points");
        yield return ItemDef.Own(GearSlot.Hull, "reinforced_frame", "Reinforced Frame", "the frame, and its point defence");
        // ── everything that drops: every line at every tier ──
        foreach (var it in Items.Build()) yield return it;
    }

    // the loadout: 5 core slots in order, then 6 chips ("" = empty)
    // What a ship of this class comes out of the yard with: ITS OWN two parts, from its row
    // (ClassDef.Kit), the three every ship is born with, and no chips.
    public static string[] Default(ShipClass c)
    {
        var kit = Classes.Of(c).Kit;
        string Own(GearSlot s) => kit.FirstOrDefault(k => k.Slot == s)?.Id ?? "";
        var l = new string[Slots]; Array.Fill(l, "");
        (l[0], l[1], l[2], l[3], l[4]) = (Own(GearSlot.Weapon), "std_drive", "basic_deflector", "reinforced_frame", Own(GearSlot.Utility));
        return l;
    }

    // A LOADOUT MADE SAFE for a pilot whose highest level is `peak` -- what the host does with a
    // pilot's claim, and what a file is read through. Known items in the right slots for the class;
    // a bad core slot gets its default. A chip is emptied when it does not fit, when its slot is one
    // `peak` has not opened (Unlocks), or when it is the fourth of its ChipKind, counted in slot
    // order, so the first three stay.
    public static string[] Sanitize(ShipClass c, string[] ids, int peak)
    {
        var d = Default(c); var o = new string[Slots];
        int open = Unlocks.Count(Opens.ChipSlot, peak);
        var kinds = new int[Enum.GetValues<ChipKind>().Length];
        for (int k = 0; k < Slots; k++)
        {
            string id = ids != null && k < ids.Length ? ids[k] ?? "" : d[k];
            var it = ById(id);
            bool ok = Fits(it, SlotAt(k), c) && (k < CoreSlots || (k - CoreSlots < open && kinds[(int)it.Kind]++ < KindCap));
            o[k] = ok ? id : d[k];
        }
        return o;
    }

    // WHERE A CHIP FROM THE HOLD GOES on loadout `l` for a pilot at `peak`: the first empty slot
    // that is open -- or -1, and why not, in the words the window's greyed EQUIP says.
    public static (int slot, string why) ChipFit(string[] l, int peak, ItemDef chip)
    {
        if (chip?.Slot != GearSlot.Chip) return (-1, "not a chip");
        int open = Unlocks.Count(Opens.ChipSlot, peak);
        if (Enumerable.Range(CoreSlots, ChipSlots).Count(k => ById(l[k]) is { Slot: GearSlot.Chip } on && on.Kind == chip.Kind) >= KindCap)
            return (-1, $"at most {KindCap} {chip.Kind} chips");
        for (int k = CoreSlots; k < CoreSlots + open; k++) if (string.IsNullOrEmpty(l[k])) return (k, null);
        return open < ChipSlots ? (-1, $"next chip slot opens at level {Unlocks.At(Opens.ChipSlot, open + 1)}") : (-1, "every chip slot is full");
    }

    // ── SALVAGE LEVELS, ON THE SLOT ──────────────────────────────────────────────────────────
    // REPLACED: a level per PART ID (+5% a level, 100 salvage and a quarter more each time), so a
    // better part dropped threw away everything put into the old one. A level now lives on a CORE
    // SLOT, per pilot, shared by every class that pilot flies: five ladders (Weapon, Engines, Shield,
    // Hull, Utility), keyed by the slot's name. A level lifts what the part in that slot is FOR
    // (ItemDef.Ups) by 3% and nothing it costs; a kit part has no ups and never moves. CHIP SLOTS
    // TAKE NO LEVEL (numbers_curve_raids_items.md §8 R7): there is no chip ladder.
    // THE PRICE: 500 salvage for the first, a tenth more each time (round(500 x 1.10^n)), 40 levels.
    // THE CAP: a level is bought only up to the highest level cleared on any ladder + 1 (LevelCap),
    // checked at purchase, so salvage cannot outrun the bosses; what was bought is never taken back.
    public const int MaxLevel = 40;                 // 40 x 3% = +120%
    public const double LevelStep = 0.03, LevelCost = 500, LevelGrowth = 1.10;
    // A slot's key in a pilot's levels, or null for a slot that takes none (a chip).
    public static string LevelKey(GearSlot s) => s == GearSlot.Chip ? null : s.ToString();
    // WHOSE LEVELS. A sheet is lifted by the levels it is GIVEN -- a ship's by its own pilot's
    // (PlayerShip.Levels, which came with that pilot's identity) -- never by the pilot at this
    // keyboard. LevelOf is THIS pilot's, for what only this pilot sees: the window, the price.
    private static int LevelIn(IReadOnlyDictionary<string, int> levels, GearSlot s) =>
        LevelKey(s) is { } k ? System.Math.Clamp(levels?.GetValueOrDefault(k, 0) ?? 0, 0, MaxLevel) : 0;
    public static int LevelOf(GearSlot s) => LevelIn(Character.GearLevel, s);
    public static int LevelCap => System.Math.Min(MaxLevel, Missions.HighestAnywhere() + 1);
    // Held at the cap: the next level waits for a clear (the price still shows, greyed).
    public static bool Capped(GearSlot s) => LevelOf(s) >= LevelCap;
    // A PILOT'S LEVELS MADE SAFE, as a copy: core slots only, each held to the ladder, a 0 left
    // out. The one way levels are taken in -- a pilot's claim on the wire (Hub.NetIdentity), a file
    // on disk (Character.Load), and what a ship keeps of its pilot's (PlayerShip.SetEquipment). A
    // COPY, so a level bought later is a change the ship can see rather than an edit to a dictionary
    // it already shares.
    public static Dictionary<string, int> SanitizeLevels(IEnumerable<(string id, int level)> claimed)
    {
        var d = new Dictionary<string, int>();
        foreach (var (id, level) in claimed ?? Enumerable.Empty<(string, int)>())
            if (Core.Any(c => LevelKey(c) == id) && level > 0) d[id] = System.Math.Min(level, MaxLevel);
        return d;
    }
    // What the NEXT level of a slot costs, in salvage -- or -1 for a chip slot or at the ceiling.
    public static double NextLevelCost(GearSlot s) =>
        LevelKey(s) == null || LevelOf(s) >= MaxLevel ? -1 : System.Math.Round(LevelCost * System.Math.Pow(LevelGrowth, LevelOf(s)));
    // Everything a part is for, lifted by its SLOT's level in `levels`.
    private static double Lifted(ItemDef i, string stat, double v, IReadOnlyDictionary<string, int> levels) =>
        System.Array.IndexOf(i.Ups, stat) < 0 ? v : v * (1 + LevelStep * LevelIn(levels, i.Slot));

    // What a loadout does to the sheet, summed per stat over its SANITISED parts, so a part in the
    // wrong slot, for another class, or in a chip slot `peak` has not opened changes nothing: the
    // shares, and the whole additions -- each part lifted by the level `levels` gives it (a ship's
    // own pilot's; this pilot's for its previews).
    public static Dictionary<string, double> Bonuses(ShipClass c, string[] loadout, int peak, IReadOnlyDictionary<string, int> levels) =>
        Sum(c, loadout, peak, i => i.Pct.ToDictionary(kv => kv.Key, kv => Lifted(i, kv.Key, kv.Value, levels)));
    public static Dictionary<string, double> Adds(ShipClass c, string[] loadout, int peak, IReadOnlyDictionary<string, int> levels) =>
        Sum(c, loadout, peak, i => i.Add.ToDictionary(kv => kv.Key, kv => Lifted(i, kv.Key, kv.Value, levels)));
    private static Dictionary<string, double> Sum(ShipClass c, string[] loadout, int peak, Func<ItemDef, IReadOnlyDictionary<string, double>> part)
    {
        var d = new Dictionary<string, double>();
        foreach (var it in Sanitize(c, loadout, peak).Select(ById).Where(i => i != null))
            d = ShipStats.Sum(d, Items.Expand(c, part(it)));
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
        foreach (var (id, p) in Items.Expand(c, item.Pct).OrderBy(kv => kv.Key, StringComparer.Ordinal))
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
