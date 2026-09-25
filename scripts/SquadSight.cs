using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// SQUAD SIGHT -- a squad as EVERY peer sees it, from the raider packet alone (raids v2,
// docs/plans/raids_squads_adds.md §1e). A Squad is host bookkeeping (Squads.cs); what a guest has
// of it is the squad id in bits 8-23, the lead and lock bits, the line's end and the kind's row
// (Raider.NetFlags, TetherTo, Def). Everything drawn about a squad -- the radar's bracket and rim
// chevron, the victim's HUD line, the row names under the hulls -- reads it through here, so the
// host and a guest draw the same thing from the same figures.
//
// REPLACED: the radar's one dot for every hostile, and nothing on screen that named a squad or
// said who it was burning at.
// A NEW ENEMY ROW needs nothing here: its Name, its Tag (Tag.Heavy counts first) and its Cc (the
// web glyph beside its name) are all this reads.
// ─────────────────────────────────────────────────────────────────────────────
public static class SquadSight
{
    public const double NameShow = 3.0;       // a row's name shows under its hull this long
    public const float VictimReach = 150f;    // a lock line ends on a ship within its hit radius + this

    // THE SQUADS UP: raiders under one id, two or more (a lone raider is no squad on screen)
    public static List<List<Raider>> Groups(IEnumerable<Raider> raiders) =>
        raiders.Where(r => r != null && GodotObject.IsInstanceValid(r) && r.Alive && r.SquadId != 0)
               .GroupBy(r => r.SquadId).Where(g => g.Count() > 1).Select(g => g.ToList()).ToList();

    // flying its slots: nobody committing and nobody holding a line
    public static bool InFormation(IReadOnlyList<Raider> squad) => squad.All(r => !r.Locking && r.TetherTo == null);

    private static bool Heavy(Raider r) => (r.Tags & Tag.Heavy) != 0;

    // THE RADAR'S LABEL for a squad off the scope: its heavies + the rest ("1+3"); the count alone
    // when it brings no heavy
    public static string Label(IReadOnlyList<Raider> squad)
    {
        int h = squad.Count(Heavy);
        return h > 0 ? $"{h}+{squad.Count - h}" : $"{squad.Count}";
    }

    // WHAT IT BRINGS, heavies first, each row once: "GUNSHIP + 3 WEBIFIER"
    public static string Crew(IReadOnlyList<Raider> squad) =>
        string.Join(" + ", squad.OrderBy(r => Heavy(r) ? 0 : 1)
                                .GroupBy(r => r.Def.Name.ToUpperInvariant())
                                .Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));

    // THE SHIP A RAIDER'S LINE ENDS ON: the nearest live ship to its end, if one is that close
    public static PlayerShip Victim(Raider r, IEnumerable<PlayerShip> ships)
    {
        if (r.TetherTo is not { } end) return null;
        PlayerShip best = null; float bd = float.MaxValue;
        foreach (var s in ships)
        {
            if (s == null || !GodotObject.IsInstanceValid(s) || !s.Alive) continue;
            float d = s.Position.DistanceTo(end);
            if (d < bd && d <= s.HitRadius + VictimReach) { bd = d; best = s; }
        }
        return best;
    }

    // THE VICTIM'S HUD LINE: a squad on its burn at this ship ("GANK: GUNSHIP + 3 WEBIFIER"), or null
    public static string GankLine(IEnumerable<Raider> raiders, IEnumerable<PlayerShip> ships, PlayerShip me)
    {
        if (me == null) return null;
        var all = ships.ToList();
        foreach (var sq in Groups(raiders))
            if (sq.Any(r => r.Locking && ReferenceEquals(Victim(r, all), me))) return "GANK: " + Crew(sq);
        return null;
    }
}
