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
    // WHAT A HULL MUST HAVE FOR THIS TO FIT, by stat id: a part fits a hull that has AT LEAST
    // ONE of them. THE ONE FITTING RULE -- there is no second one anywhere.
    //   A ROLLED part names the stats it MOVES, because a part whose every stat is missing there
    //   would do nothing at all. A Rapid Battery moves main_interval and main_damage, so it fits
    //   every class with main guns -- eleven of the twelve -- without naming one of them; Elite
    //   Hangars move fighter_speed, so only a carrier can wear them; a Basic Combat Chip moves
    //   every weapon stat in the game, so it fits everything, and lifts whichever of them that
    //   hull has.
    //   A HULL'S OWN hardware (Kit, built by Own below) moves nothing, so it names instead the
    //   SIGNATURE row of the hulls born with it. Every class has one nothing else has --
    //   broadside_mult, fighter_count, missile_mag, bubble_pool, overdrive_mult, wave_range,
    //   rail_damage, rush_mult, hunter_count, roll_time, echo_time, stealth_time -- so one id
    //   names one hull, and three ids name the three freighters that share a cargo gun. It is
    //   the same OR as every other part, which is why Equipment.KitOwners -- a second table and
    //   a second answer to "does this fit" -- is gone.
    public string[] Needs = System.Array.Empty<string>();
    public bool Kit;                         // a hull's own hardware: never dropped
    // THE STATS THIS PART IS FOR -- the `up` list it was written with, as opposed to what it takes
    // in exchange. Levelling a part with salvage improves these and leaves the drawbacks exactly as
    // printed, so a Rapid Battery's guns get faster and its shells never get lighter. Without this
    // the two are indistinguishable once they are both a number in Pct: a share is negative on a
    // reload because a shorter reload IS the improvement.
    public string[] Ups = System.Array.Empty<string>();
    public Dictionary<string, double> Pct = new(), Add = new();

    // A HULL'S OWN HARDWARE, as a class row writes it (ClassDef.Kit) and as Build yields it: a
    // label with a name and a blurb that changes no number (the sheet already IS the ship),
    // fitting the hulls whose signature rows it names. Its id is what a SAVE FILE holds, so an
    // id here is never changed. KitPart -- a four-field mini-item this had to convert into an
    // ItemDef anyway -- is gone.
    // STATIC ORDER: Classes.All builds these while IT is initialising, so this may touch no
    // static field of Equipment (Equipment.All reads Classes.All, and the two would deadlock).
    public static ItemDef Own(GearSlot slot, string id, string name, string blurb, params string[] needs) =>
        new() { Slot = slot, Id = id, Name = name, Blurb = blurb, Kit = true, Needs = needs };
}

public static class Equipment
{
    public const int CoreSlots = 5, ChipSlots = 5, Slots = CoreSlots + ChipSlots;
    public static readonly GearSlot[] Core = { GearSlot.Weapon, GearSlot.Engines, GearSlot.Shield, GearSlot.Hull, GearSlot.Utility };
    // Above All: static fields start in the order they are written, and Build() reads these.
    private static readonly double[] Scale = { 1.0, 1.5, 2.0 };        // the upside, by rarity
    private static readonly string[] Suffix = { "", " II", " III" };
    // WHAT "EVERY WEAPON REACHES FURTHER" MEANS: whatever the classes say it does. It was six ids
    // written out here, so a part promising every weapon did nothing for a sniper's railgun, a
    // warden's hunters, a warrior's EMP, a bastion's wave, a freighter's turrets or an echo's
    // blast -- seven of the nine newest classes bought a lie.
    private static string[] Ranges => Classes.EveryReachStat().ToArray();
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
    // THE ONE RULE: it fits if the slot matches and the hull has one of the stats it asks for.
    // No part names a class, and a hull's own hardware is a part like any other -- it asks for
    // the signature row only the hulls born with it have (ItemDef.Needs). There were two rules
    // here, one of them a dictionary of class lists built by scanning every class row.
    public static bool Fits(ItemDef i, GearSlot s, ShipClass c)
    {
        if (i == null || i.Slot != s) return false;
        if (i.Needs.Length == 0) return true;             // a part that asks nothing fits anything
        var sheet = Sheet(c);
        foreach (var need in i.Needs) if (sheet.Contains(need)) return true;
        return false;
    }
    // Every hull that can wear it, in the order the classes are written.
    private static IEnumerable<ShipClass> Hulls(ItemDef i) =>
        Classes.All.Where(d => d.Ready && Fits(i, i.Slot, d.Id)).Select(d => d.Id);
    // What a part asks of a hull, in words, for the equipment window -- worked out from the same
    // one rule, so a kit part and a rolled part are said the same way.
    //   THREE HULLS OR FEWER and it names them: "FREIGHTER / TENDER / BASTION" for the cargo gun,
    //   "FREIGHTER" for a bubble part. That is every signature part, kit or rolled.
    //   MORE THAN THREE and the names would be a list nobody reads, so it says the SYSTEMS it
    //   moves instead, ANY ONE of which is enough (Fits) -- "Main guns", "Hull or Point defence".
    // It printed the raw stat ids joined by commas once ("needs fighter_speed, fighter_damage,
    // fighter_turn, fighter_count"), which named nothing a player has ever seen. Describe cannot
    // help: the only sheet it can read is the CURRENT class's, which by definition has not got
    // them. AllStats (Stats.cs) holds every class's.
    public static string NeedsSaid(ItemDef i)
    {
        if (i == null || i.Needs.Length == 0) return "any hull";
        var hulls = Hulls(i).ToList();
        return hulls.Count > 0 && hulls.Count <= 3
            ? string.Join(" / ", hulls.Select(Classes.NameOf))
            : string.Join(" or ", i.Needs.Select(AllStats.Group).Distinct());
    }
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
            var it = new ItemDef { Id = $"{id}_{r + 1}", Name = name + Suffix[r], Slot = slot, Rarity = (Rarity)r, Needs = needs, Blurb = blurb,
                                   Ups = (up ?? None).Select(t => t.stat).Distinct().ToArray() };
            foreach (var (s, p) in up) it.Pct[s] = it.Pct.GetValueOrDefault(s) + p * Scale[r];
            foreach (var (s, p) in down ?? Array.Empty<(string, double)>()) it.Pct[s] = it.Pct.GetValueOrDefault(s) + p;
            foreach (var (s, a) in add ?? Array.Empty<(string, int[])>()) it.Add[s] = a[r];
            yield return it;
        }
    }

    private static IEnumerable<ItemDef> Build()
    {
        // ── a hull's own hardware: the ship's own numbers, and never a drop ──
        // EVERY CLASS'S OWN, out of its row (ClassDef.Kit) as the ItemDef it already is -- there
        // is nothing left to convert. Several classes may carry the SAME part (three freighters
        // share one cargo gun, two heavies one light cannon), written once and yielded once.
        foreach (var k in Classes.All.SelectMany(d => d.Kit).GroupBy(k => k.Id).Select(g => g.First()))
            yield return k;
        // ...and the four every ship in the game is born with, which ask nothing of a hull
        yield return ItemDef.Own(GearSlot.Engines, "std_drive", "Standard Drive Cluster", "the helm: speed and turning");
        yield return ItemDef.Own(GearSlot.Shield, "basic_deflector", "Basic Deflector Array", "the ship's hull points");
        yield return ItemDef.Own(GearSlot.Hull, "reinforced_frame", "Reinforced Frame", "the frame, and its point defence");
        var basic = ItemDef.Own(GearSlot.Chip, "chip_basic", "Basic Combat Chip", "+5% damage, +5% hull");
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
            Line("cv_line", "Line Hangars", GearSlot.Weapon, "one more fighter, with more damage and speed",
                 new[] { ("fighter_damage", 0.20), ("fighter_speed", 0.10) }, null,
                 new[] { ("fighter_count", new[] { 1, 1, 1 }) }),
            Line("cv_leash", "Leash Hangars", GearSlot.Weapon, "fighters: more control range and weapon range; less damage",
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
            Line("dd_salvo", "Salvo Rack", GearSlot.Utility, "more bursts a magazine; a slower reload and lighter warheads",
                 None, new[] { ("missile_reload", -0.25), ("missile_damage", -0.20) },
                 new[] { ("missile_mag", new[] { 1, 2, 3 }) }),
            Line("dd_buster", "Buster Rack", GearSlot.Utility, "far more damage; less speed and guidance",
                 new[] { ("missile_damage", 1.00) }, new[] { ("missile_speed", -0.20), ("missile_turn", -0.30) }),
            Line("dd_seeker", "Seeker Rack", GearSlot.Utility, "much more guidance and speed; lighter warheads",
                 new[] { ("missile_turn", 1.00), ("missile_speed", 0.20) }, new[] { ("missile_damage", -0.20) }),
            Line("dd_loader", "Loader Rack", GearSlot.Utility, "a faster reload and less between bursts; less damage",
                 new[] { ("missile_reload", 0.60), ("missile_refire", 0.40) }, new[] { ("missile_damage", -0.25) }),
            // ── carrier bomber bay ──
            Line("cv_heavybay", "Heavy Bay", GearSlot.Utility, "one bomber, with far more torpedo damage; slower",
                 new[] { ("torpedo_damage", 0.80) }, new[] { ("bomber_speed", -0.20) },
                 new[] { ("bomber_count", new[] { -1, -1, -1 }) }),
            Line("cv_widebay", "Wide Bay", GearSlot.Utility, "more bombers, lighter torpedoes",
                 None, new[] { ("torpedo_damage", -0.30) },
                 new[] { ("bomber_count", new[] { 1, 2, 3 }) }),
            Line("cv_longbay", "Long Bay", GearSlot.Utility, "a longer torpedo run, launch distance and speed; less damage",
                 new[] { ("torpedo_range", 0.40), ("launch_range", 0.30), ("torpedo_speed", 0.20) }, new[] { ("torpedo_damage", -0.15) }),
            Line("cv_rapidbay", "Rapid Bay", GearSlot.Utility, "a faster rearm and more torpedoes a run; less damage",
                 new[] { ("bomber_rearm", 0.60) }, new[] { ("torpedo_damage", -0.20) },
                 new[] { ("bomber_ammo", new[] { 1, 2, 3 }) }),
            // ── THE NINE SYSTEMS THAT HAD NO FAMILY ──
            // Every one of the nine new classes wore its kit part in the utility slot and nothing
            // else: the bubble, the overdrive, the shockwave, the railgun, the rush, the hunters,
            // the roll, the echo and the veil had no rolled part in the game, while the broadside,
            // the racks and the bays above had four lines each at three rarities. One family each
            // now, in the same shape, so the twelfth class is as well served as the first.
            // EVERY LINE NAMES ONLY ITS OWN SYSTEM'S STAT IDS. That is what keeps it on the one
            // hull that has them (ItemDef.Needs): a bubble part that paid for its pool in
            // max_speed would fit every ship in the game and do nothing at all on eleven of them.
            // ── the freighter's bubble ──
            Line("bub_deep", "Deep Bubble", GearSlot.Utility, "it soaks far more damage; a smaller radius",
                 new[] { ("bubble_pool", 0.50) }, new[] { ("bubble_radius", -0.20) }),
            Line("bub_wide", "Wide Bubble", GearSlot.Utility, "a much larger radius; it soaks less",
                 new[] { ("bubble_radius", 0.40) }, new[] { ("bubble_pool", -0.25) }),
            Line("bub_long", "Long Bubble", GearSlot.Utility, "much longer up; a longer cooldown",
                 new[] { ("bubble_time", 0.60) }, new[] { ("bubble_cooldown", -0.25) }),
            Line("bub_snap", "Snap Bubble", GearSlot.Utility, "a much shorter cooldown; less time up, and it soaks less",
                 new[] { ("bubble_cooldown", 0.50) }, new[] { ("bubble_time", -0.25), ("bubble_pool", -0.15) }),
            // ── the tender's overdrive ──
            Line("ovr_hard", "Hard Coils", GearSlot.Utility, "a higher rate of fire, for less time up",
                 new[] { ("overdrive_mult", 0.30) }, new[] { ("overdrive_time", -0.30) }),
            Line("ovr_long", "Long Coils", GearSlot.Utility, "much longer up, at a lower rate of fire",
                 new[] { ("overdrive_time", 0.60) }, new[] { ("overdrive_mult", -0.20) }),
            Line("ovr_quick", "Quick Coils", GearSlot.Utility, "a much shorter cooldown; less time up",
                 new[] { ("overdrive_cooldown", 0.50) }, new[] { ("overdrive_time", -0.25) }),
            Line("ovr_heavy", "Heavy Coils", GearSlot.Utility, "a much higher rate of fire; a longer cooldown",
                 new[] { ("overdrive_mult", 0.45) }, new[] { ("overdrive_cooldown", -0.30) }),
            // ── the bastion's shockwave ──
            Line("wav_far", "Wide Emitter", GearSlot.Utility, "much more reach; it throws them less far",
                 new[] { ("wave_range", 0.40) }, new[] { ("wave_push", -0.25) }),
            Line("wav_hard", "Heavy Emitter", GearSlot.Utility, "throws them much further; a shorter reach",
                 new[] { ("wave_push", 0.60) }, new[] { ("wave_range", -0.20) }),
            Line("wav_hold", "Holding Emitter", GearSlot.Utility, "holds a boss far longer; it shoves less",
                 new[] { ("wave_disable", 0.50) }, new[] { ("wave_push", -0.25) }),
            Line("wav_quick", "Quick Emitter", GearSlot.Utility, "a much shorter cooldown; it holds a boss less",
                 new[] { ("wave_cooldown", 0.50) }, new[] { ("wave_disable", -0.30) }),
            // ── the sniper's railgun ──
            Line("rlg_slug", "Heavy Slug", GearSlot.Utility, "far more damage; a longer charge, locked",
                 new[] { ("rail_damage", 0.50) }, new[] { ("rail_charge", -0.25) }),
            Line("rlg_quick", "Quick Charge", GearSlot.Utility, "a much shorter charge; less damage",
                 new[] { ("rail_charge", 0.60) }, new[] { ("rail_damage", -0.25) }),
            Line("rlg_long", "Long Barrel", GearSlot.Utility, "much more reach; a narrower beam",
                 new[] { ("rail_range", 0.40) }, new[] { ("rail_width", -0.30) }),
            Line("rlg_wide", "Wide Beam", GearSlot.Utility, "a far wider beam; less reach and a longer cooldown",
                 new[] { ("rail_width", 0.80) }, new[] { ("rail_range", -0.20), ("rail_cooldown", -0.20) }),
            // ── the warrior's rush, and the EMP it ends in ──
            Line("rsh_burn", "Burn Drive", GearSlot.Utility, "much more top speed, for less time up",
                 new[] { ("rush_mult", 0.30) }, new[] { ("rush_time", -0.25) }),
            Line("rsh_iron", "Iron Drive", GearSlot.Utility, "less damage taken while rushing; a weaker EMP",
                 new[] { ("rush_guard", -0.20) }, new[] { ("emp_damage", -0.25) }),
            Line("rsh_shock", "Shock Drive", GearSlot.Utility, "an EMP that hits harder and holds them longer; a shorter rush",
                 new[] { ("emp_damage", 0.50), ("emp_stun", 0.25) }, new[] { ("rush_time", -0.30) }),
            Line("rsh_wide", "Wide Drive", GearSlot.Utility, "much more EMP reach; a longer cooldown",
                 new[] { ("emp_range", 0.50) }, new[] { ("rush_cooldown", -0.25) }),
            // ── the warden's hunter-seekers ──
            Line("hnt_heavy", "Heavy Cells", GearSlot.Utility, "far more damage each; one missile fewer",
                 new[] { ("hunter_damage", 0.60) }, null,
                 new[] { ("hunter_count", new[] { -1, -1, -1 }) }),
            Line("hnt_swarm", "Swarm Cells", GearSlot.Utility, "more missiles, each of them lighter",
                 None, new[] { ("hunter_damage", -0.25) },
                 new[] { ("hunter_count", new[] { 1, 2, 3 }) }),
            Line("hnt_seeker", "Seeker Cells", GearSlot.Utility, "much more guidance and speed; less damage each",
                 new[] { ("hunter_turn", 0.60), ("hunter_speed", 0.20) }, new[] { ("hunter_damage", -0.20) }),
            Line("hnt_rapid", "Rapid Cells", GearSlot.Utility, "a much shorter cooldown; less reach",
                 new[] { ("hunter_cooldown", 0.50) }, new[] { ("hunter_range", -0.20) }),
            // ── the dart's barrel roll, and the boost out of it ──
            Line("rol_long", "Long Roll", GearSlot.Utility, "untouchable for longer; a shorter boost",
                 new[] { ("roll_time", 0.40) }, new[] { ("boost_time", -0.30) }),
            Line("rol_racing", "Racing Roll", GearSlot.Utility, "more top speed out of the roll; a lower rate of fire",
                 new[] { ("boost_speed", 0.20) }, new[] { ("boost_rof", -0.15) }),
            Line("rol_hot", "Hot Roll", GearSlot.Utility, "a higher rate of fire out of the roll; less top speed",
                 new[] { ("boost_rof", 0.30) }, new[] { ("boost_speed", -0.15) }),
            Line("rol_quick", "Quick Roll", GearSlot.Utility, "a much shorter cooldown; untouchable for less",
                 new[] { ("roll_cooldown", 0.50) }, new[] { ("roll_time", -0.25) }),
            // ── the echo's core ──
            Line("ech_deep", "Deep Core", GearSlot.Utility, "far more of what it dealt; a smaller blast radius",
                 new[] { ("echo_share", 0.40) }, new[] { ("echo_radius", -0.25) }),
            Line("ech_wide", "Wide Core", GearSlot.Utility, "a far wider blast; it repeats less",
                 new[] { ("echo_radius", 0.40) }, new[] { ("echo_share", -0.20) }),
            Line("ech_long", "Long Core", GearSlot.Utility, "it remembers far longer; a longer cooldown",
                 new[] { ("echo_time", 0.50) }, new[] { ("echo_cooldown", -0.25) }),
            Line("ech_quick", "Quick Core", GearSlot.Utility, "a much shorter cooldown; it remembers less",
                 new[] { ("echo_cooldown", 0.50) }, new[] { ("echo_time", -0.30) }),
            // ── the wraith's veil ──
            // stealth_speed and stealth_rof are what the veil DOES besides hiding it (Ships.cs:
            // the wraith's rows, x1.00 each, so a ship with no such part is exactly as it was).
            Line("vel_deep", "Deep Veil", GearSlot.Utility, "unseen far longer; a longer cooldown",
                 new[] { ("stealth_time", 0.50) }, new[] { ("stealth_cooldown", -0.25) }),
            Line("vel_flicker", "Flicker Veil", GearSlot.Utility, "a much shorter cooldown; unseen for less",
                 new[] { ("stealth_cooldown", 0.60) }, new[] { ("stealth_time", -0.30) }),
            Line("vel_swift", "Swift Veil", GearSlot.Utility, "more top speed while unseen; unseen for less",
                 new[] { ("stealth_speed", 0.25) }, new[] { ("stealth_time", -0.20) }),
            Line("vel_ambush", "Ambush Veil", GearSlot.Utility, "a higher rate of fire while unseen; less top speed",
                 new[] { ("stealth_rof", 0.30) }, new[] { ("stealth_speed", -0.15) }),
            // ── engines ──
            Line("drv_racing", "Racing Drive", GearSlot.Engines, "more top speed ahead and ahead acceleration; a lower rudder limit",
                 new[] { ("max_speed", 0.25), ("thrust", 0.30) }, new[] { ("turn_rate", -0.25) }),
            Line("drv_helm", "Helm Drive", GearSlot.Engines, "a higher rudder limit and a tighter turning radius; less top speed",
                 new[] { ("turn_rate", 0.40), ("turn_radius", 0.30) }, new[] { ("max_speed", -0.15) }),
            Line("drv_surge", "Surge Drive", GearSlot.Engines, "much more ahead and astern acceleration; a lower top speed ahead",
                 new[] { ("thrust", 0.80), ("reverse_thrust", 0.80) }, new[] { ("max_speed", -0.10) }),
            Line("drv_cruiser", "Cruiser Drive", GearSlot.Engines, "more top speed ahead and astern; less ahead acceleration",
                 new[] { ("max_speed", 0.15), ("reverse_speed", 0.50) }, new[] { ("thrust", -0.20) }),
            // ── shield: the hull points ──
            Line("sh_bulwark", "Bulwark Array", GearSlot.Shield, "much more hull; less top speed ahead",
                 new[] { ("hull", 0.40) }, new[] { ("max_speed", -0.15) }),
            Line("sh_balanced", "Balanced Array", GearSlot.Shield, "more hull, and more point-defence range",
                 new[] { ("hull", 0.20), ("pd_range", 0.10) }),
            Line("sh_glass", "Glass Array", GearSlot.Shield, "every weapon hits harder; less hull",
                 AllDamage(0.20), new[] { ("hull", -0.25) }),
            Line("sh_screen", "Screen Array", GearSlot.Shield, "more hull; less ahead acceleration",
                 new[] { ("hull", 0.25) }, new[] { ("thrust", -0.10) }),
            // ── hull: the frame and its point defence ──
            Line("fr_flak", "Flak Frame", GearSlot.Hull, "point defence: more damage and a shorter reload; less range",
                 new[] { ("pd_damage", 0.60), ("pd_interval", 0.30) }, new[] { ("pd_range", -0.15) }),
            Line("fr_picket", "Picket Frame", GearSlot.Hull, "point defence: more range and turret turn rate; less damage",
                 new[] { ("pd_range", 0.40), ("pd_turn", 0.50) }, new[] { ("pd_damage", -0.20) }),
            Line("fr_endurance", "Endurance Frame", GearSlot.Hull, "point defence: a longer firing window, a shorter recharge",
                 new[] { ("pd_active", 0.50), ("pd_reload", 0.30) }),
            Line("fr_armour", "Armoured Frame", GearSlot.Hull, "more hull; a shorter point-defence firing window",
                 new[] { ("hull", 0.20) }, new[] { ("pd_active", -0.30) }),
            // ── frames for a hull with no point defence to reinforce ──
            // The four above are point defence's: nine of their twelve parts move nothing but pd_
            // stats, so the five classes that mount no point defence -- the sniper, the warrior,
            // the dart, the echo and the wraith -- could wear three of the twelve. These three
            // families are about the HULL itself, so every ship in the game can wear them.
            Line("fr_braced", "Braced Frame", GearSlot.Hull, "more hull; a lower rudder limit and top speed ahead",
                 new[] { ("hull", 0.30) }, new[] { ("turn_rate", -0.20), ("max_speed", -0.10) }),
            Line("fr_spar", "Spar Frame", GearSlot.Hull, "a higher rudder limit and a tighter turning radius; less hull",
                 new[] { ("turn_rate", 0.45), ("turn_radius", 0.20) }, new[] { ("hull", -0.20) }),
            Line("fr_keel", "Keel Brace", GearSlot.Hull, "more keel grip and drag: no drift; a little less top speed",
                 new[] { ("keel", 0.60), ("water_drag", 0.25) }, new[] { ("max_speed", -0.08) }),
            // ── frames for the turrets a freighter carries about ──
            // Its deployed turrets ARE its frame: these only mean anything on a hull that drops them.
            Line("cr_cradle", "Turret Cradle", GearSlot.Hull, "deployed turrets: much more turret hull; less range",
                 new[] { ("deploy_hull", 0.60) }, new[] { ("deploy_range", -0.15) }),
            Line("cr_rack", "Launch Rack", GearSlot.Hull, "deployed turrets: a shorter gap between drops; less damage",
                 new[] { ("deploy_cooldown", 0.50) }, new[] { ("deploy_damage", -0.20) }),
            Line("cr_heavy", "Heavy Cradle", GearSlot.Hull, "deployed turrets: more damage and range; longer between drops",
                 new[] { ("deploy_damage", 0.45), ("deploy_range", 0.25) }, new[] { ("deploy_cooldown", -0.30) }),
            // ── chips ──
            Line("chip_combat", "Combat Chip", GearSlot.Chip, "every weapon hits harder; a little less hull",
                 AllDamage(0.08), new[] { ("hull", -0.05) }),
            Line("chip_armour", "Armour Chip", GearSlot.Chip, "more hull; a little less damage",
                 new[] { ("hull", 0.08) }, AllDamage(-0.05)),
            Line("chip_engine", "Engine Chip", GearSlot.Chip, "more top speed ahead and rudder limit",
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

    // ── LEVELLING A PART WITH SALVAGE ────────────────────────────────────────────────────────
    // Salvage had nothing to buy but a refit, and a part was worth exactly what it rolled. A level
    // adds 5% to what the part is FOR (ItemDef.Ups) and nothing to what it costs you, at 100
    // salvage for the first and a quarter more each time, stopping at +200%.
    //
    // A LEVEL BELONGS TO THE PART ID, not to a copy: the hold has always stored counts by id and
    // there is no instance id anywhere in the save or on the wire, so "this Rapid Battery II" is
    // not a thing the game can say. Levelling one levels every one you own, which is also the
    // reading that cannot lose a levelled part by scrapping the wrong copy.
    public const int MaxLevel = 40;                 // 40 x 5% = +200%
    public const double LevelStep = 0.05, LevelCost = 100, LevelGrowth = 1.25;
    public static int LevelOf(string id) => System.Math.Clamp(Character.GearLevel.GetValueOrDefault(id ?? "", 0), 0, MaxLevel);
    // What the NEXT level costs, in salvage -- or -1 at the ceiling.
    public static double NextLevelCost(string id) =>
        LevelOf(id) >= MaxLevel ? -1 : System.Math.Round(LevelCost * System.Math.Pow(LevelGrowth, LevelOf(id)));
    // Everything a part is for, lifted by its level. A kit part has no ups and never moves.
    private static double Lifted(ItemDef i, string stat, double v) =>
        System.Array.IndexOf(i.Ups, stat) < 0 ? v : v * (1 + LevelStep * LevelOf(i.Id));

    // What a loadout does to the sheet, summed per stat over its SANITISED parts, so a part in the
    // wrong slot or for another class changes nothing: the shares, and the whole additions.
    public static Dictionary<string, double> Bonuses(ShipClass c, string[] loadout) =>
        Sum(c, loadout, i => i.Pct.ToDictionary(kv => kv.Key, kv => Lifted(i, kv.Key, kv.Value)));
    public static Dictionary<string, double> Adds(ShipClass c, string[] loadout) =>
        Sum(c, loadout, i => i.Add.ToDictionary(kv => kv.Key, kv => Lifted(i, kv.Key, kv.Value)));
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
