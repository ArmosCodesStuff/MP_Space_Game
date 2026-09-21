using System.Collections.Generic;
using System.Linq;

// EQUIPMENT -- a ship's parts, in slots. The menu (I) shows the gear of the pilot's CURRENT
// class; each class keeps its own loadout (saved with the character), so gear left on a ship
// is still on it when the pilot switches back. Parts change no looks, only numbers.
//   Slots: Weapon, Engines, Shield (the hull points), Hull (the frame and its mods, point
//   defence among them), Utility -- and five Chips.
//   The COMMON defaults below reproduce the ship's own numbers; each default chip adds
//   +5% damage and +5% hull. The host applies a pilot's equipment (it resolves damage and
//   hull): the loadout travels with the identity, and is checked on arrival.
public enum GearSlot { Weapon, Engines, Shield, Hull, Utility, Chip }
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

public class ItemDef
{
    public string Id, Name, Blurb;
    public GearSlot Slot;
    public Rarity Rarity = Rarity.Common;
    public ShipClass? Class;                 // null: fits any capital ship
    public double DamagePct, HullPct;        // what it adds, as a share of the ship's own numbers
}

public static class Equipment
{
    public const int CoreSlots = 5, ChipSlots = 5, Slots = CoreSlots + ChipSlots;
    public static readonly GearSlot[] Core = { GearSlot.Weapon, GearSlot.Engines, GearSlot.Shield, GearSlot.Hull, GearSlot.Utility };

    public static readonly ItemDef[] All =
    {
        new() { Id = "bs_main_battery",    Name = "Mk I Main Battery",       Slot = GearSlot.Weapon,  Class = ShipClass.Battleship, Blurb = "the four main turrets" },
        new() { Id = "cv_fighter_hangars", Name = "Mk I Fighter Hangars",    Slot = GearSlot.Weapon,  Class = ShipClass.Carrier,    Blurb = "the fighter wing" },
        new() { Id = "std_drive",          Name = "Standard Drive Cluster",  Slot = GearSlot.Engines, Blurb = "the helm: speed and turning" },
        new() { Id = "basic_deflector",    Name = "Basic Deflector Array",   Slot = GearSlot.Shield,  Blurb = "the ship's hull points" },
        new() { Id = "reinforced_frame",   Name = "Reinforced Frame",        Slot = GearSlot.Hull,    Blurb = "the frame, and its point defence" },
        new() { Id = "bs_missile_rack",    Name = "Missile Rack",            Slot = GearSlot.Utility, Class = ShipClass.Battleship, Blurb = "the missile and its magazine" },
        new() { Id = "cv_bomber_bay",      Name = "Bomber Bay",              Slot = GearSlot.Utility, Class = ShipClass.Carrier,    Blurb = "the bomber wing" },
        new() { Id = "chip_basic",         Name = "Basic Combat Chip",       Slot = GearSlot.Chip,    DamagePct = 0.05, HullPct = 0.05, Blurb = "+5% damage, +5% hull" },
    };
    public static ItemDef ById(string id) => All.FirstOrDefault(i => i.Id == id);
    static bool Fits(ItemDef i, GearSlot s, ShipClass c) => i != null && i.Slot == s && (i.Class == null || i.Class == c);

    // the loadout: 5 core slots in order, then 5 chips ("" = empty)
    public static string[] Default(ShipClass c) => new[]
    {
        c == ShipClass.Carrier ? "cv_fighter_hangars" : "bs_main_battery", "std_drive", "basic_deflector", "reinforced_frame",
        c == ShipClass.Carrier ? "cv_bomber_bay" : "bs_missile_rack",
        "chip_basic", "chip_basic", "chip_basic", "chip_basic", "chip_basic",
    };

    // a loadout made safe: known items in the right slots for the class; a bad core slot gets
    // its default, a bad chip slot is emptied (what the host does with a pilot's claim)
    public static string[] Sanitize(ShipClass c, string[] ids)
    {
        var d = Default(c); var o = new string[Slots];
        for (int k = 0; k < Slots; k++)
        {
            string id = ids != null && k < ids.Length ? ids[k] ?? "" : (k < CoreSlots ? d[k] : "");
            var slot = k < CoreSlots ? Core[k] : GearSlot.Chip;
            o[k] = Fits(ById(id), slot, c) ? id : (k < CoreSlots ? d[k] : "");
        }
        return o;
    }

    // the stat bonuses a loadout gives, as shares: damage on the class's weapons, and hull
    public static string[] DamageStats(ShipClass c) => c == ShipClass.Carrier
        ? new[] { "fighter_damage", "torpedo_damage" } : new[] { "main_damage", "missile_damage" };
    public static Dictionary<string, double> Bonuses(ShipClass c, string[] loadout)
    {
        double dmg = 0, hull = 0;
        foreach (var i in (loadout ?? new string[0]).Select(ById).Where(i => i != null)) { dmg += i.DamagePct; hull += i.HullPct; }
        var b = new Dictionary<string, double>();
        if (dmg != 0) foreach (var s in DamageStats(c)) b[s] = dmg;
        if (hull != 0) b["hull"] = hull;
        return b;
    }
}
