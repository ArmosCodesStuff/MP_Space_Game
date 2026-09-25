using System;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// CHARGE BANDS: what a charged weapon fires as, by how far its charge got. ONE TABLE PER WEAPON.
//
// REPLACED: a charge that could only ever mean "the whole of it" -- the railgun fired its full
// damage down its one line whatever its charge had been, and a second kind of charge (a tap for a
// little down a first-body line, a full charge through everything, a ramp between) would have
// been a second hand-written `if` in the weapon. Now a charge is read off its bands, so a weapon
// that fires differently for a short and a long hold is new rows, never new code.
//
// A TABLE (ChargeTable, a row of Charges.All) MUST FILL IN:
//   Id     the weapon's ability id (Charges.Of reaches it by this)
//   Bands  its bands, lowest At first
// A BAND (ChargeBand) MUST FILL IN:
//   At     the share of the full charge at which this band begins (0 = at once, 1 = full)
//   Mult   what the weapon's damage is multiplied by in this band
//   Pct    (instead of Mult) the stat id on the firer's sheet that says it, in percent, so gear and
//          lifts move it like any row (the railgun's tap, rail_tap)
//   Line   the Lines row it fires down in this band (a tap may stop at the first body, a full
//          charge go through everything)
//   Ramp   true: the multiplier rises in a straight line from the band below's to this one's as
//          the charge crosses from that band's At to this one's (a ramp, not a step)
// A charge short of the lowest band fires as the lowest. Charges.All is an array of rows, never a
// dictionary: Net.Fingerprint hashes a constant table only as an array or a row, so two builds whose
// bands differ refuse each other.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ChargeBand
{
    public double At, Mult = 1;
    public string Pct;
    public int Line;
    public bool Ramp;
}

public sealed class ChargeTable
{
    public string Id;
    public ChargeBand[] Bands;
}

public static class Charges
{
    public static readonly ChargeTable[] All =
    {
        // the sniper's railgun (sniper_active_reload.md 1.4): let go at once it fires rail_tap (40%) of
        // rail_damage, rising in a straight line to the whole at full, down the rail line either way
        new() { Id = "railgun", Bands = new[] { new ChargeBand { At = 0, Pct = "rail_tap", Line = Lines.Rail },
                                                new ChargeBand { At = 1, Mult = 1, Line = Lines.Rail, Ramp = true } } },
    };

    // A WEAPON'S BANDS by its ability id; null for a weapon with no table.
    public static ChargeBand[] Of(string id) => All.FirstOrDefault(t => t.Id == id)?.Bands;

    // WHAT A CHARGE OF `share` (0 = none, 1 = full, more = held past full) FIRES AS: the multiplier
    // on the weapon's damage and the line it goes down. `stat` reads the firer's sheet for a band
    // that names a Pct (null: such a band fires its Mult). Pure, so a table is provable apart from
    // any weapon that uses it.
    public static (double mult, int line) At(ChargeBand[] bands, double share, Func<string, double> stat = null)
    {
        double M(ChargeBand b) => b.Pct != null && stat != null ? stat(b.Pct) / 100 : b.Mult;
        int i = 0;
        while (i + 1 < bands.Length && bands[i + 1].At <= share) i++;
        var b = bands[i];
        if (i + 1 < bands.Length && bands[i + 1].Ramp && share > b.At)
        {
            var up = bands[i + 1];
            double t = (share - b.At) / (up.At - b.At);
            return (M(b) + (M(up) - M(b)) * t, b.Line);
        }
        return (M(b), b.Line);
    }
}
