using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// UNLOCKS -- what a pilot's level opens, as ONE table every class reads (docs/plans/level_walls.md,
// with the owner's rulings: every hull has 6 chip slots; walls read the highest level ever reached).
//
//   level 1  the weapon and ability 1      level 6   ability 3
//   level 2  chip slot 1                   level 8   chip slot 3
//   level 3  ability 2                     level 10  chip slot 4
//   level 4  chip slot 2                   level 12  chip slot 5
//                                          level 14  chip slot 6
//
// A WALL READS THE PILOT'S PEAK (Character.Peak): the highest level it has ever reached. A refit costs
// a level and is also the only way to change class, so reading the level itself would lock again
// what a pilot had already opened.
//
// THE BASE'S WALLS ARE ROWS HERE TOO, measured by the highest boss the base owner has beaten
// (Measure.Boss: Yard.OwnerBoss, Missions.HighestBeaten): Auto-sell after boss 3, the lanes'
// blockades after boss 1. They were two numbers of their own (Economy's NeedsBoss, Lanes.NeedsBoss).
//
// A NEW ROW fills in: what it is measured by (Measure), the level that opens it (At), what it opens
// (Opens) and which one of those it is (Nth: ability 2, chip slot 5; or Id: the Economy row it
// opens). Nothing else names a level: every question about a wall comes through the queries below.
// ─────────────────────────────────────────────────────────────────────────────
public enum Measure { Pilot, Boss }                 // the pilot's highest level reached (Character.Peak); the base owner's highest boss beaten
public enum Opens { Ability, ChipSlot, Economy, Raids }

public readonly struct Unlock
{
    public readonly Measure By;
    public readonly int At, Nth;
    public readonly Opens What;
    public readonly string Id;                      // the row of another table it opens (an Economy id), or null
    public Unlock(Measure by, int at, Opens what, int nth, string id = null) { By = by; At = at; What = what; Nth = nth; Id = id; }
}

public static class Unlocks
{
    public static readonly Unlock[] All =
    {
        new(Measure.Pilot, 1, Opens.Ability, 1),
        new(Measure.Pilot, 2, Opens.ChipSlot, 1),
        new(Measure.Pilot, 3, Opens.Ability, 2),
        new(Measure.Pilot, 4, Opens.ChipSlot, 2),
        new(Measure.Pilot, 6, Opens.Ability, 3),
        new(Measure.Pilot, 8, Opens.ChipSlot, 3),
        new(Measure.Pilot, 10, Opens.ChipSlot, 4),
        new(Measure.Pilot, 12, Opens.ChipSlot, 5),
        new(Measure.Pilot, 14, Opens.ChipSlot, 6),
        new(Measure.Boss, 3, Opens.Economy, 0, "hauler_autosell"),     // Auto-sell
        new(Measure.Boss, 1, Opens.Raids, 0),                          // the lanes' blockades
    };

    // The level that opens the nth of `what`; int.MaxValue when the table has no such row, so a 4th
    // ability or a 7th chip slot is never open rather than always open.
    public static int At(Opens what, int nth)
    {
        foreach (var u in All) if (u.What == what && u.Nth == nth) return u.At;
        return int.MaxValue;
    }

    // How many of `what` a pilot at `peak` has open.
    public static int Count(Opens what, int peak) => All.Count(u => u.What == what && u.By == Measure.Pilot && u.At <= peak);

    // The level at which every pilot row is open.
    public static int Top => All.Where(u => u.By == Measure.Pilot).Max(u => u.At);

    // The boss level the base owner must have beaten before `what` (the row `id` of it) opens; 0
    // when no row walls it, so an Economy row this table does not name is open from the start.
    public static int Boss(Opens what, string id = null)
    {
        foreach (var u in All) if (u.By == Measure.Boss && u.What == what && u.Id == id) return u.At;
        return 0;
    }
}
