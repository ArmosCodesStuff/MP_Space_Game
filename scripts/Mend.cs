using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// MEND (F10) -- the one door every heal one ship gives another goes through: the Tender's lance and
// its Repair field (6b) are its first users, as rows that call it.
//
// NEW (slice 4); it replaced nothing (no ship healed another before it). NOT THIS: regeneration
// (PlayerShip's 0.5% / 3% a second) runs on every peer between the host's reports and is the hull's
// own; the reboard's hull is the wreck's own rule. Both stay where they are.
//
// THE RULE: host only (a guest's hull is the host's figure, sent in its report); nothing to a wreck;
// never above the hull's maximum; nothing for an amount that is not a positive number. The healer is
// credited only what LANDED (PlayerShip.MendedBy, by the source's id -- the ability row's own id),
// so a lance held on a full hull is worth nothing on the sheet, as it is in the fight.
// A NEW HEALER needs no code here: its row calls Give with its own id.
// ─────────────────────────────────────────────────────────────────────────────
public static class Mend
{
    // Heals `to` by up to `amount`; returns what landed.
    public static double Give(PlayerShip to, double amount, PlayerShip by, string source)
    {
        if (!Net.Sim || to == null || !to.Alive || !double.IsFinite(amount) || amount <= 0) return 0;
        double took = Math.Min(amount, to.MaxHp - to.Hp);
        if (took <= 0) return 0;
        to.Hp += took;
        if (by != null) by.MendedBy[source] = by.MendedBy.GetValueOrDefault(source) + took;
        return took;
    }
}
