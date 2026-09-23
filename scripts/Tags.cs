// WHAT A THING IS, for code that must ask. A tag is not a type test: point defence wants "missiles
// and small craft", a freighter's shockwave wants "anything with the boss tag", stealth wants
// "every hostile that could target me" -- and each of those used to name concrete classes
// (`h is Torpedo`, `h is Raider r && !r.Heavy`), so a new enemy or a new class meant finding every
// list again and getting all of them right.
//
// A thing declares its tags once (ITagged) and every rule is written against the tags, never
// against a class name or a hit radius guessed to mean "small".
[System.Flags]
public enum Tag
{
    None = 0,
    Light = 1,          // a small craft: a light raider, a carrier fighter, a practice fighter
    Heavy = 2,          // a gunship-weight enemy
    Boss = 4,           // a bounty boss: what the freighter's shockwave disables
    Fighter = 8,        // a craft flown from a carrier (a wing, not a ship)
    // EVERY Shot, whatever its row (Shots.All): a shell, a slug, a piece of scrap, a torpedo, a
    // missile, a seeker. It is what keeps guns and wings OFF anything in flight (Shot.Strike,
    // Targeting.Attackable, Targeting.WingPrey) and what point defence looks FOR -- but only a row
    // marked Interceptable, which is the seeker alone, is given an id and joins Combat.Hostiles,
    // so a seeker is the only thing carrying this tag that can actually be shot down. It read
    // "thrown bodies" and no thrown body has ever carried it: a boss's rock is a plain Node2D
    // (ThrownRock), in no list, tagged nothing, and interceptable by nothing.
    Missile = 16,
    Structure = 32,     // it does not fly: a deployed turret, a station's gun
    Dummy = 64,         // a practice target: unkillable, and never worth a wing's time
    Player = 128,       // a pilot's ship
    Fleet = 256,        // a base's own craft: the miners, the salvagers, the hauler
}

public interface ITagged
{
    Tag Tags { get; }
}

public static class TagExt
{
    // what a thing is, when it may not be tagged at all (a plain IHittable, a node)
    public static Tag Of(object o) => o is ITagged t ? t.Tags : Tag.None;
    public static bool Is(object o, Tag any) => (Of(o) & any) != 0;
}
