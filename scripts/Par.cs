using System;

// ─────────────────────────────────────────────────────────────────────────────
// PAR — the reference pilot, as rows: what a boss, a raider and every hostile blow are scaled by.
//
// REPLACED: Missions.S(L) = 1.025^(L-1) (and Missions.LevelStep), one factor for hull and damage
// alike, which knew nothing of what a pilot actually gains in a level. The boss now grows as the
// pilot it is built for grows: its hull as par's DPS, its damage as par's hull.
//
// THE PILOT (numbers_curve_raids_items.md §1.1): the DESTROYER base class with NO CHIPS, behind the
// level walls (abilities at PL 1/3/6), 4 clears a level, its points cheaper-first between Weapons and
// Hull, wearing 4 power lines (Weapon, Utility, Shield, Hull frame) at the tier Loot's drop row gives
// it, levelled with salvage to 65% of the gate (+3% a slot level). Each row is that pilot averaged over
// its level's four fights: its HULL, its realistic DPS against CRAFT (no rip), and the LANCER's hull
// that it kills in 60 s (the rip counted, x0.932 = the fleet's walled L1 median over the DD's).
//
// A ROW MUST FILL IN: its level, par's hull, par's DPS against craft, and the Lancer's 60 s hull.
// The rows are `models/numbers_v2.py`'s (run from docs/plans/models): the item pass re-runs the model
// on its own Tiers and Loot rows and re-literals this table, and nothing else moves. Every scale is a
// RATIO of two rows, so every peer computes the same figure from the level alone.
//   HullScale(L)   = Boss(L) / Boss(1)     a boss's and a siege's hull
//   CraftScale(L)  = Craft(L) / Craft(1)   a raider's hull
//   DamageScale(L) = Hull(L) / Hull(1)     every hostile blow (a boss's, a raider's gun, a siege's)
// A level may be fractional (an escort's threat): it is read between the two rows around it. Past
// the last row every scale is flat, as the model is past L40 (tiers stop at T10, the gate at g26).
// ─────────────────────────────────────────────────────────────────────────────
public static class Par
{
    public readonly struct Row
    {
        public readonly int Level;
        public readonly double Hull, Craft, Boss;
        public Row(int level, double hull, double craft, double boss) { Level = level; Hull = hull; Craft = craft; Boss = boss; }
    }

    public static readonly Row[] Rows =
    {
        new(1, 423.1, 57.61, 3222.0), new(2, 487.2, 63.23, 3536.5), new(3, 532.6, 66.73, 3732.6), new(4, 564.4, 69.94, 4002.8),
        new(5, 593.7, 71.64, 4129.9), new(6, 618.9, 73.24, 4222.1), new(7, 639.1, 76.34, 4400.7), new(8, 652.9, 77.26, 4453.4),
        new(9, 674.9, 78.24, 4509.9), new(10, 692.1, 79.27, 4569.0), new(11, 705.4, 80.86, 4661.0), new(12, 717.1, 82.33, 4745.5),
        new(13, 730.9, 83.30, 4801.6), new(14, 755.3, 84.52, 4871.6), new(15, 771.4, 85.36, 4920.3), new(16, 782.2, 86.11, 4963.1),
        new(17, 796.9, 88.37, 5093.4), new(18, 816.8, 90.11, 5194.0), new(19, 831.3, 91.00, 5245.2), new(20, 847.7, 91.77, 5289.7),
        new(21, 870.5, 93.05, 5363.2), new(22, 893.6, 94.69, 5457.8), new(23, 909.9, 95.64, 5512.8), new(24, 924.0, 97.68, 5630.1),
        new(25, 943.1, 99.40, 5729.6), new(26, 969.5, 101.07, 5826.0), new(27, 988.1, 102.31, 5897.3), new(28, 1011.7, 103.26, 5951.9),
        new(29, 1038.0, 104.74, 6037.5), new(30, 1070.2, 106.72, 6151.2), new(31, 1092.0, 108.14, 6233.1), new(32, 1108.9, 110.51, 6369.7),
        new(33, 1134.0, 112.62, 6491.4), new(34, 1167.5, 114.92, 6624.0), new(35, 1191.6, 116.42, 6710.5), new(36, 1218.5, 117.72, 6785.5),
        new(37, 1252.1, 119.46, 6885.8), new(38, 1284.3, 121.68, 7013.6), new(39, 1307.6, 122.96, 7087.3), new(40, 1324.6, 123.90, 7141.2),
        new(41, 1328.2, 125.35, 7224.7), new(42, 1329.9, 125.82, 7252.2), new(43, 1331.3, 125.89, 7255.9), new(44, 1332.0, 125.90, 7256.4),
        new(45, 1332.2, 125.91, 7257.2), new(46, 1344.0, 125.91, 7257.2), new(47, 1347.9, 125.91, 7257.2), new(48, 1347.9, 125.91, 7257.2),
        new(49, 1347.9, 125.91, 7257.2), new(50, 1347.9, 125.91, 7257.2), new(51, 1347.9, 126.74, 7304.7), new(52, 1347.9, 127.56, 7352.3),
        new(53, 1347.9, 127.56, 7352.3), new(54, 1347.9, 127.56, 7352.3), new(55, 1347.9, 127.56, 7352.3), new(56, 1351.8, 127.56, 7352.3),
        new(57, 1363.4, 127.56, 7352.3), new(58, 1363.4, 127.56, 7352.3), new(59, 1363.4, 127.56, 7352.3), new(60, 1363.4, 127.56, 7352.3),
        new(61, 1363.4, 127.56, 7352.3), new(62, 1363.4, 127.97, 7376.1), new(63, 1363.4, 129.21, 7447.4), new(64, 1363.4, 129.21, 7447.4),
        new(65, 1363.4, 129.21, 7447.4), new(66, 1363.4, 129.21, 7447.4), new(67, 1363.4, 129.21, 7447.4), new(68, 1363.4, 129.21, 7447.4),
        new(69, 1378.9, 129.21, 7447.4), new(70, 1378.9, 129.21, 7447.4), new(71, 1378.9, 129.21, 7447.4), new(72, 1378.9, 129.21, 7447.4),
        new(73, 1378.9, 129.21, 7447.4), new(74, 1378.9, 129.21, 7447.4), new(75, 1378.9, 130.04, 7494.9), new(76, 1378.9, 130.86, 7542.5),
        new(77, 1378.9, 130.86, 7542.5), new(78, 1378.9, 130.86, 7542.5), new(79, 1378.9, 130.86, 7542.5), new(80, 1378.9, 130.86, 7542.5),
    };

    // The value of a column at a (possibly fractional) level: the row's own at a whole level, read
    // straight between the two rows around it otherwise, and held at the first and last rows.
    private static double At(double level, Func<Row, double> col)
    {
        if (double.IsNaN(level) || level <= Rows[0].Level) return col(Rows[0]);
        if (level >= Rows[^1].Level) return col(Rows[^1]);
        int lo = (int)Math.Floor(level) - Rows[0].Level;
        double t = level - Rows[lo].Level;
        return col(Rows[lo]) + (col(Rows[lo + 1]) - col(Rows[lo])) * t;
    }
    public static double HullScale(double level) => At(level, r => r.Boss) / Rows[0].Boss;
    public static double CraftScale(double level) => At(level, r => r.Craft) / Rows[0].Craft;
    public static double DamageScale(double level) => At(level, r => r.Hull) / Rows[0].Hull;

    // THE LEVEL A HULL STANDS AT: the lowest level whose par is at least `share` of par's L1 hull --
    // an escort's party, as tough as par at level L, is read as level L (Waves.Standing). Under 1 is
    // level 1; beyond the last row's, the last row's level.
    public static double LevelAtHull(double share)
    {
        double want = share * Rows[0].Hull;
        if (!(want > Rows[0].Hull)) return Rows[0].Level;
        for (int i = 1; i < Rows.Length; i++)
            if (Rows[i].Hull >= want)
                return Rows[i - 1].Level + (want - Rows[i - 1].Hull) / (Rows[i].Hull - Rows[i - 1].Hull);
        return Rows[^1].Level;
    }
}
