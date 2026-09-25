using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// MEND (F10) -- the one door every heal one ship gives another goes through: the Tender's lance and
// its Repair field (6b) are its users, as rows that call it.
//
// NEW (slice 4); it replaced nothing (no ship healed another before it). NOT THIS: regeneration
// (PlayerShip's 0.5% / 3% a second) runs on every peer between the host's reports and is the hull's
// own; the reboard's hull is the wreck's own rule. Both stay where they are.
//
// THE RULE: host only (a guest's hull is the host's figure, sent in its report); nothing to a wreck or
// a craft lost; never above the hull's maximum; nothing for an amount that is not a positive number.
// The healer is credited only what LANDED (PlayerShip.MendedBy, by the source's id -- the ability
// row's own id), so a lance held on a full hull is worth nothing on the sheet, as it is in the fight.
// A NEW HEALER needs no code here: its row calls Give with its own id.
// WHAT IT MENDS is an IMendable (kits6b-J7, D43): a pilot's hull, a sentry, a ship of the base's fleet.
// A NEW KIND OF FRIENDLY HULL fills in the members below and is in Hub.RaiderTargets, and every heal
// reaches it; nothing here names a kind.
// ─────────────────────────────────────────────────────────────────────────────
public interface IMendable
{
    Vector2 Position { get; }
    bool Mendable { get; }        // there to mend: alive, out in the field (a wreck or a lost craft is not)
    double HullNow { get; }
    double HullMax { get; }
    float BodyRadius { get; }     // how far from its centre a beam touches it (the lance's first body)
    void Mended(double d);        // Give's alone: the hull goes up by d
}

public static class Mend
{
    // Heals `to` by up to `amount`; returns what landed.
    public static double Give(IMendable to, double amount, PlayerShip by, string source)
    {
        if (!Net.Sim || to == null || !to.Mendable || !double.IsFinite(amount) || amount <= 0) return 0;
        double took = Math.Min(amount, to.HullMax - to.HullNow);
        if (took <= 0) return 0;
        to.Mended(took);
        if (by != null) by.MendedBy[source] = by.MendedBy.GetValueOrDefault(source) + took;
        return took;
    }

    // EVERY FRIENDLY HULL A HEAL CAN REACH, where the world is `hub`: every pilot (Combat.Players) and what
    // a raid can reach there (the sentries out, the base's fleet at home -- Hub.RaiderTargets) that can be
    // mended, each once.
    public static IEnumerable<IMendable> Friendlies(Hub hub)
    {
        var seen = new HashSet<IMendable>();
        foreach (var h in Combat.Players) if (h is IMendable m && m.Mendable && seen.Add(m)) yield return m;
        if (hub == null) yield break;
        foreach (var n in hub.RaiderTargets()) if (n is IMendable m && m.Mendable && seen.Add(m)) yield return m;
    }
}
