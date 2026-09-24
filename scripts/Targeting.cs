using System.Collections.Generic;
using Godot;

// WHO MAY BE SHOT, AND BY WHAT. Every gun, wing, missile and boss used to read `Combat.Hostiles`
// and then filter it with a type test of its own, so a new enemy or a new hidden state had to be
// found again in each one. One filter type, written in TAGS, over the one Nearest.
//
// TWO QUESTIONS, NEVER ONE. A filter is asked who it would CHOOSE -- aim at, home on, pick to come
// for -- or who it would HIT -- catch in a blow that lands where it lands: a ring, a beam, a ram, a
// burst, a sweep. Stealth (Status.Untargetable) is the whole difference: it hides a thing from
// every choice and from no blow. A caller says which it is doing -- Chooses, Choosable, Nearest;
// or Hits, Hittable -- and there is no third way to ask.
//
// NOTHING IS CACHED. A dropped enemy leaves Combat.Hostiles and must leave every turret's aim in
// the same frame (Hub.LetGo), so every query is answered from the live list -- and so is the
// question whether a turret may go on holding what it already has (Turret.StillThere).
public readonly struct TargetFilter
{
    // FALLBACK: what it allows only as a last resort. A chooser that HOLDS a target (Turret) ranks
    // these below everything else it may take, and never holds one while anything else it may take
    // is in reach. Chooses and Hits admit them like any other tag, so a one-off choice or a sweep
    // (Nearest, Choosable, Hittable) is not changed by it: "last" means something only to what
    // keeps a target.
    public readonly Tag Require, Forbid, Fallback;
    public TargetFilter(Tag require = Tag.None, Tag forbid = Tag.None, Tag fallback = Tag.None)
    { Require = require; Forbid = forbid; Fallback = fallback; }

    // HITTING: alive, and of a kind this filter takes. What a blow that lands in an area asks.
    public bool Hits(object o)
    {
        if (o == null || (o is IHittable h && !h.Alive)) return false;
        var t = TagExt.Of(o);
        if (Require != Tag.None && (t & Require) == 0) return false;
        return Forbid == Tag.None || (t & Forbid) == 0;
    }
    // CHOOSING: all of that, and seen. What anything that PICKS asks.
    public bool Chooses(object o) => Hits(o) && !Targeting.Hidden(o);

    public bool IsFallback(object o) => TagExt.Is(o, Fallback);
}

public static class Targeting
{
    // Point defence takes missiles and small craft only -- never a heavy, a boss or a dummy hulk.
    // (a practice dummy's FIGHTER is Light, so PD still engages it; the hulk itself is not.)
    public static readonly TargetFilter PointDefence = new(require: Tag.Missile | Tag.Light | Tag.Fighter);
    // ANYTHING BUT A MISSILE IN FLIGHT. Three things want exactly this: something hostile choosing
    // a ship or a fleet craft to attack, a FIRE-AND-FORGET SEEKER choosing what to chase, and a blow
    // landing on everything under it (asked through Hits). One row, because a second one with the
    // same contents is a second one to keep true.
    public static readonly TargetFilter Attackable = new(forbid: Tag.Missile);
    // What a wing takes for itself when its target dies: never a missile, never a practice dummy
    // (it cannot die, so the wing would strafe it for ever). A SEEKER DOES NOT USE THIS, and using
    // it is what made the warden look broken -- the dummy a pilot tests on was the one thing its
    // missiles refused to fly at. The reasoning above is about a wing RE-TARGETING; a seeker hits
    // once and dies, so it never applied to one.
    public static readonly TargetFilter WingPrey = new(forbid: Tag.Missile | Tag.Dummy);
    // Raiding craft, for the base's guns: what actually comes at the station.
    public static readonly TargetFilter Craft = new(require: Tag.Light | Tag.Heavy, forbid: Tag.Dummy);
    // WHAT A TURRET LEFT STANDING TAKES: anything hostile -- a missile, a raider of any weight, a
    // boss, a station -- ranked as point defence ranks (Turret.Rank), so missiles and small craft
    // still come first. Point defence's own filter would keep it off a heavy, a boss and a pylon,
    // which is most of what a freighter's turrets are for. A practice dummy is a FALLBACK: taken
    // only while nothing that can die is in reach, and let go the moment something is -- a pilot
    // tests its turrets by dropping them beside one, and a raid that arrives takes them off it.
    public static readonly TargetFilter Sentry = new(fallback: Tag.Dummy);

    // Hidden things are CHOSEN by nothing hostile (stealth) and HIT by everything that lands on
    // them (TargetFilter.Hits). The pilot's own clicks and Tab go through Combat.Pickable, not
    // through here, so a hidden ally is still selectable.
    public static bool Hidden(object o) => o is IStatused s && s.Statuses.Has(Status.Untargetable);

    // the nearest thing in `list` to `at`, within `within`, that `filter` would CHOOSE and `also` accepts
    public static IHittable Nearest(List<IHittable> list, Vector2 at, TargetFilter filter,
                                    float within = float.MaxValue, System.Func<IHittable, bool> also = null)
        => Combat.Nearest(list, at, h => h.Position, within, h => filter.Chooses(h) && (also == null || also(h)));

    // everything in `list` the filter would CHOOSE, in list order: what a volley is shared among,
    // and what a boss can see
    public static IEnumerable<IHittable> Choosable(List<IHittable> list, TargetFilter filter)
    {
        foreach (var h in list) if (filter.Chooses(h)) yield return h;
    }
    // everything in `list` the filter would HIT, in list order: what a pulse, a sweep or a ring lands on
    public static IEnumerable<IHittable> Hittable(List<IHittable> list, TargetFilter filter)
    {
        foreach (var h in list) if (filter.Hits(h)) yield return h;
    }
}

// Anything a status can be put on: a ship, a boss, an enemy, a base's craft.
public interface IStatused
{
    StatusSet Statuses { get; }
    void ApplyStatus(Status s, double seconds);
}
