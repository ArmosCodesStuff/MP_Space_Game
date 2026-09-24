using System.Collections.Generic;
using Godot;

// WHO MAY BE SHOT, AND BY WHAT. Every gun, wing, missile and boss used to read `Combat.Hostiles`
// and then filter it with a type test of its own, so a new enemy or a new hidden state had to be
// found again in each one. One filter type, written in TAGS, over the one Nearest.
//
// NOTHING IS CACHED. A dropped enemy leaves Combat.Hostiles and must leave every turret's aim in
// the same frame (docs/DESIGN.md: Hub.DropRaider), so every query is answered from the live list.
public readonly struct TargetFilter
{
    public readonly Tag Require, Forbid;
    public TargetFilter(Tag require = Tag.None, Tag forbid = Tag.None) { Require = require; Forbid = forbid; }

    public bool Allows(object o)
    {
        if (o == null || (o is IHittable h && !h.Alive)) return false;
        var t = TagExt.Of(o);
        if (Require != Tag.None && (t & Require) == 0) return false;
        if (Forbid != Tag.None && (t & Forbid) != 0) return false;
        return !Targeting.Hidden(o);
    }
}

public static class Targeting
{
    // Point defence takes missiles and small craft only -- never a heavy, a boss or a dummy hulk.
    // (a practice dummy's FIGHTER is Light, so PD still engages it; the hulk itself is not.)
    public static readonly TargetFilter PointDefence = new(require: Tag.Missile | Tag.Light | Tag.Fighter);
    // ANYTHING BUT A MISSILE IN FLIGHT. Two things want exactly this: something hostile choosing a
    // ship or a fleet craft to attack, and a FIRE-AND-FORGET SEEKER choosing what to chase. One
    // row, because a second one with the same contents is a second one to keep true.
    public static readonly TargetFilter Attackable = new(forbid: Tag.Missile);
    // What a wing takes for itself when its target dies: never a missile, never a practice dummy
    // (it cannot die, so the wing would strafe it for ever). A SEEKER DOES NOT USE THIS, and using
    // it is what made the warden look broken -- the dummy a pilot tests on was the one thing its
    // missiles refused to fly at. The reasoning above is about a wing RE-TARGETING; a seeker hits
    // once and dies, so it never applied to one.
    public static readonly TargetFilter WingPrey = new(forbid: Tag.Missile | Tag.Dummy);
    // Raiding craft, for the base's guns: what actually comes at the station.
    public static readonly TargetFilter Craft = new(require: Tag.Light | Tag.Heavy, forbid: Tag.Dummy);

    // Hidden things are not chosen by anything hostile (stealth). The pilot's own clicks and Tab
    // go through Combat.Pickable, not through here, so a hidden ally is still selectable.
    public static bool Hidden(object o) => o is IStatused s && s.Statuses.Has(Status.Untargetable);

    // the nearest thing in `list` to `at`, within `within`, that `filter` and `also` both accept
    public static IHittable Nearest(List<IHittable> list, Vector2 at, TargetFilter filter,
                                    float within = float.MaxValue, System.Func<IHittable, bool> also = null)
        => Combat.Nearest(list, at, h => h.Position, within, h => filter.Allows(h) && (also == null || also(h)));

    // everything in `list` the filter accepts, in list order: for a pulse, a sweep, a wave
    public static IEnumerable<IHittable> All(List<IHittable> list, TargetFilter filter)
    {
        foreach (var h in list) if (filter.Allows(h)) yield return h;
    }
}

// Anything a status can be put on: a ship, a boss, an enemy, a base's craft.
public interface IStatused
{
    StatusSet Statuses { get; }
    void ApplyStatus(Status s, double seconds);
}
