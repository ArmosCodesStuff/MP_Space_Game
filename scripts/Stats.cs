using Godot;
using System;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// STATS — every number the ship fights and flies with, in one place.
//
// This sheet is the ONLY source: turrets, wings and flight read their values from
// here, and the stats window (K) prints this same sheet. So the window cannot
// disagree with the game; if a number changes, it changes for both.
//
// Each stat is (Base + Flat) x (1 + Bonus).
//   Flat  -- whole additions: the pilot's purchases (Progression.Flats) and gear's (Equipment.Adds:
//            one fighter fewer, two more bursts in the magazine).
//   Bonus -- shares keyed by stat id (0.10 = +10%): gear's (Equipment.Bonuses) and the character's
//            own (Character.Bonuses), summed (ShipStats.Sum).
// Interval stats (time between shots) are the exception: a rate-of-fire bonus DIVIDES them, so
// +100% RoF halves the interval and doubles the DPS. Either way the multiplier never falls below
// 0.1: gear leans hard, and a stack of downsides must shrink a number, never zero it, turn it
// negative or divide by zero.
// ─────────────────────────────────────────────────────────────────────────────
public class Stat
{
    public string Id, Label, Unit, Group;
    public double Base, Bonus, Flat;   // Flat: purchases and gear's whole additions, added before the bonus
    public bool Inverse;           // an interval: bonus divides instead of multiplies
    public int Decimals = 1;

    private const double MinScale = 0.1;
    // HOW SHARES STACK, wherever they stack: ADDED, then 1 + the sum, never below MinScale. A
    // stat's own bonuses, a ship's running lifts (PlayerShip.LiftShares) and the two together
    // (With, which PlayerShip.Cadence reads) are this one rule, so two +100% rate buffs are x3 --
    // two parts, two abilities, or a part under an ability -- and never x4 (the owner's ruling).
    public static double Scale(double shares) => System.Math.Max(MinScale, 1.0 + shares);
    public double Value => With(0);
    // ...and with more shares ADDED to its own: what a running lift makes of it.
    public double With(double shares)
    {
        double k = Scale(Bonus + shares);
        return Inverse ? (Base + Flat) / k : (Base + Flat) * k;
    }
    public string Fmt(double v) => v.ToString("F" + Decimals) + (Unit.Length > 0 ? " " + Unit : "");
}

// A DAMAGING SYSTEM, as a class declares it (ClassDef.Weapons): what the K window calls it, how
// its rate is worked out from a ship's own sheet, an optional line saying how that rate is made up,
// and whether it is a burst the ship cannot hold (so no total may add it). It replaced the K
// window's `if (Fit.Guns) ... else <the carrier>`, which named four figures in its total and so
// left out the railgun, the hunters, the EMP, the deployed turrets, the echo and the carrier's
// torpedoes. A new weapon is a row here plus its id in the class rows that carry it.
public class DpsSource
{
    public string Label;
    public Func<ShipStats, double> Rate;
    public Func<ShipStats, string> Note;
    public bool Burst;
}

// One damaging system's answer for one ship: what to print, and whether the total counts it.
public readonly struct DpsLine
{
    public readonly string Label, Note;
    public readonly double Dps;
    public readonly bool Sustained;
    public DpsLine(string label, double dps, bool sustained, string note)
    { Label = label; Dps = dps; Sustained = sustained; Note = note; }
}

public class ShipStats
{
    // The carrier's pace: every one of its helm figures is a fraction of its own top speed, so it
    // gets under way on its own clock. Named here because the class's row (Ships.cs) is written
    // in terms of it.
    public const double CarrierTop = 99;

    public readonly ShipClass Class;
    public readonly ClassDef Def;
    public readonly List<Stat> All = new();
    private readonly Dictionary<string, Stat> _byId = new();

    public double this[string id] => _byId.TryGetValue(id, out var s) ? s.Value : 0;
    // A row with more shares added to its OWN bonus -- a running lift's (PlayerShip.Cadence) -- so
    // a part's +100% rate and an overdrive's x2 are x3 together, as two parts are. 0 for a row this
    // sheet has not got, as the indexer answers.
    public double With(string id, double shares) => _byId.TryGetValue(id, out var s) ? s.With(shares) : 0;

    // Two sets of shares or additions, added key by key (either may be null).
    public static Dictionary<string, double> Sum(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        var d = new Dictionary<string, double>();
        foreach (var src in new[] { a, b })
            if (src != null) foreach (var kv in src) d[kv.Key] = d.GetValueOrDefault(kv.Key) + kv.Value;
        return d;
    }

    // Retune ONE ship's sheet, for a ship that is not a pilot's: the title screen's battleship
    // fires its broadside every five seconds rather than the class's ten. It is the sheet that
    // changes, not the class, so the ship still reads every other number the real one does.
    public void SetBase(string id, double v) { if (_byId.TryGetValue(id, out var s)) s.Base = v; }

    // A row of the sheet. `b` is the DEFAULT: the class's own row overrides it by id
    // (ClassDef.Nums), so a class states only what is different about it.
    private void Add(string group, string id, string label, double b, string unit, int dec = 1, bool inverse = false)
    {
        var s = new Stat { Group = group, Id = id, Label = label, Unit = unit, Decimals = dec, Inverse = inverse,
                           Base = Def.Nums.TryGetValue(id, out double own) ? own : b };
        All.Add(s); _byId[id] = s;
    }

    public ShipStats(ShipClass cls, IReadOnlyDictionary<string, double> bonuses = null, IReadOnlyDictionary<string, double> flats = null)
    {
        Class = cls; Def = Classes.Of(cls);

        Add("Hull",   "hull",          "Hull points",        300, "", 0);
        // WHAT EVERY ABILITY'S COOLDOWN IS MULTIPLIED BY. 1.00 out of the yard; the pilot's
        // COOLING points take it down half a percent a level, and PlayerShip refuses to let it go
        // below a floor -- an ability with no cooldown is not an ability. Every class has this row
        // because every class has abilities, which is also what lets one upgrade buy something on
        // all twelve.
        Add("Hull",   "cooldown_share", "Ability cooldowns",    1.00, "x", 2, inverse: true);

        // Capital ships handle like naval ships: thrust only along the keel, sideways drift
        // bleeds off fast, and they turn on a radius -- no strafing; almost stopped, the rudder
        // pivots the hull slowly. The battleship sets the pace and is the default below: 88 u/s,
        // the carrier 99 and the destroyer, the fastest capital ship, 117 (F22: the capitals are
        // the slowest hulls in the water and rely on the warp; each thrust was cut with its top, so
        // every hull takes as long to reach its top as before). Each class's own figures are in
        // its row (Ships.cs).
        Add("Helm", "thrust",         "Ahead acceleration",  47, "u/s²", 0);
        Add("Helm", "reverse_thrust", "Astern acceleration", 20, "u/s²", 0);
        Add("Helm", "max_speed",      "Top speed ahead",     88, "u/s", 0);
        Add("Helm", "reverse_speed",  "Top speed astern",    30, "u/s", 0);
        Add("Helm", "turn_radius",    "Turning radius",      107, "u", 0, inverse: true);
        Add("Helm", "turn_rate",      "Rudder limit",        1.08, "rad/s", 2);
        Add("Helm", "water_drag",     "Drag",                0.35, "/s", 2);
        Add("Helm", "keel",           "Keel grip (drift loss)", 4.0, "/s", 1);
        // THE SLIDE (F24): Shift + A/D push the hull sideways toward strafe_speed at strafe_thrust,
        // the nose holding its heading. 0 on the sheet, so a hull that names no figure (the three
        // capitals) never slides: that is read from the stat, never from the class. A speed lift
        // lifts both (the boost's +50%), and a hold's share lands on the speed (PlayerShip.Steer).
        Add("Helm", "strafe_speed",   "Strafe speed (Shift + A/D)", 0, "u/s", 1);
        Add("Helm", "strafe_thrust",  "Strafe acceleration", 0, "u/s²", 0);

        if (Def.Has(Fit.Guns))
        {   // cursor-aimed turrets: the battleship's four, the destroyer's two
            Add("Main guns", "main_count",    "Barrels",            4, "", 0);
            // THE 50 DPS PASS: every class averages about 50. The battleship is the steady one, from range:
            // 4 x 17.9 every 2 s (its rate halved) is 35.8, and its broadside 14.3 more; the destroyer's
            // guns are the smaller half of a bursty 50 (2 x 7.5 a second, 15).
            Add("Main guns", "main_damage",   "Damage per shot",    17.9, "", 2);
            Add("Main guns", "main_interval", "Reload (per barrel)",2.0, "s", 2, inverse: true);
            Add("Main guns", "main_range",    "Range",              1000, "u", 0);
            Add("Main guns", "main_turn",     "Turret turn rate",   Mathf.Tau / 4f, "rad/s", 2);
            Add("Main guns", "shell_speed",   "Shell speed",        650, "u/s", 0);
        }
        if (Def.Has(Fit.Broadside))
        {   // F: the turrets swing onto the cursor through the wind-up, then every main gun fires,
            // volley after volley. Its shells are main-gun shells, at this multiple of their damage.
            Add("Broadside", "broadside_volleys",  "Volleys",                 3, "", 0);
            Add("Broadside", "broadside_mult",     "Shell damage (x main)",   1.0, "x", 2);
            Add("Broadside", "broadside_windup",   "Wind-up (turrets aim)",   0.5, "s", 2, inverse: true);
            Add("Broadside", "broadside_gap",      "Between volleys",         0.25, "s", 2, inverse: true);
            Add("Broadside", "broadside_cooldown", "Cooldown after",          14, "s", 1, inverse: true);   // 12 shells a 15 s cycle: 14.3 DPS
        }
        if (Def.Has(Fit.Missiles))
        {   // F: a guided BURST of three, one at the target and two launched wide that curve in onto
            // it (PlayerShip.FireMissile). A magazine of bursts, reloaded by hand (R).
            // the destroyer's burst: 3 bursts of 3 at 49, 0.4 s apart, then 9 s to reload -- 441 in 9.8 s, 45 DPS
            Add("Missile", "missile_damage",   "Damage (each)",     49.0, "", 1);
            Add("Missile", "missile_mag",      "Magazine (bursts)", 3, "", 0);
            Add("Missile", "missile_refire",   "Between bursts",    0.4, "s", 1, inverse: true);
            Add("Missile", "missile_reload",   "Reload (R)",        9.0, "s", 1, inverse: true);
            Add("Missile", "missile_range",    "Range",             900, "u", 0);
            Add("Missile", "missile_speed",    "Speed",             200, "u/s", 0);
            Add("Missile", "missile_turn",     "Guidance (turn)",   1.5, "rad/s", 2);
        }

        if (Def.Has(Fit.Pd))
        {   // PASSIVE: every mount fires whenever the ship is alive, with no key, no window and no
            // recharge -- 1 DPS a mount. Battleship and destroyer mounts are heavier and swing slower
            // than the carrier's.
            Add("Point defence", "pd_count",    "Turrets",           2, "", 0);
            Add("Point defence", "pd_damage",   "Damage per shot",   0.5, "", 2);
            Add("Point defence", "pd_interval", "Reload",            0.5, "s", 2, inverse: true);
            Add("Point defence", "pd_range",    "Range",             460, "u", 0);
            Add("Point defence", "pd_turn",     "Turret turn rate",  Mathf.Tau / 3f, "rad/s", 2);
        }

        if (Def.Has(Fit.Wing))
        {
            Add("Fighters", "fighter_count",    "Craft",            3, "", 0);

            Add("Fighters", "fighter_damage",   "Damage per shot",  2.5, "", 1);   // 3 x 2.5 / 0.35 s, ~70% of the time: 15 DPS
            Add("Fighters", "fighter_interval", "Reload",           0.35, "s", 2, inverse: true);
            Add("Fighters", "fighter_range",    "Weapon range",     300, "u", 0);
            Add("Fighters", "fighter_speed",    "Top speed",        352, "u/s", 0);
            Add("Fighters", "fighter_burst",    "Firing before rest", 15, "s", 0);
            Add("Fighters", "fighter_rest",     "Rest, docked inside", 3, "s", 0, inverse: true);
            Add("Fighters", "fighter_turn",     "Turn rate",        3.5, "rad/s", 1);
            Add("Fighters", "control_range",    "Control range",    1500, "u", 0);   // 1.5x the battleship's guns (1000)

            // An active ability, and the carrier's long-range burst. Torpedoes run straight and steady:
            // no tracking, so from 1900 u (about twice the others' reach) a moving target often steps out
            // of the way. 8 torpedoes of 137.5 a ~14 s strike: 78.6 DPS if every one lands, about 39 at half.
            Add("Bombers", "bomber_count",    "Craft",              2, "", 0);

            Add("Bombers", "torpedo_damage",  "Torpedo damage",     137.5, "", 0);
            Add("Bombers", "bomber_ammo",     "Torpedoes per run",  4, "", 0);
            Add("Bombers", "torpedo_interval","Between launches",   0.5, "s", 2, inverse: true);
            Add("Bombers", "torpedo_speed",   "Torpedo speed",      300, "u/s", 1);
            Add("Bombers", "torpedo_range",   "Torpedo run",        2600, "u", 0);
            Add("Bombers", "launch_range",    "Launch distance",    1900, "u", 1);
            Add("Bombers", "bomber_rearm",    "Rearm on the carrier", 6, "s", 1, inverse: true);
            Add("Bombers", "bomber_speed",    "Top speed",          190, "u/s", 0);
            Add("Bombers", "bomber_accel",    "Acceleration",       500, "u/s²", 0);

            // WARP GUNSHIPS (Wings.All "gunship"): two to a target, warped onto a 240 u circle round
            // it, 2.5 a shot every 0.25 s -- 10 DPS each -- for 12 s, sent only at a target inside
            // 3000 u of the carrier. The key and its cooldown are the class's ability row.
            Add("Gunships", "gunship_count",    "Craft a target",   2, "", 0);
            Add("Gunships", "gunship_damage",   "Damage per shot",  2.5, "", 1);
            Add("Gunships", "gunship_interval", "Reload",           0.25, "s", 2, inverse: true);
            Add("Gunships", "gunship_range",    "Weapon range",     300, "u", 0);
            Add("Gunships", "gunship_orbit",    "Orbit",            240, "u", 0);
            Add("Gunships", "gunship_speed",    "Top speed",        160, "u/s", 0);
            Add("Gunships", "gunship_turn",     "Turn rate",        2.0, "rad/s", 1);
            Add("Gunships", "gunship_time",     "On station",       12, "s", 0);
            Add("Gunships", "gunship_leash",    "Launch range",     3000, "u", 0);
            // THE PATROL (Wings.All "patrol", the Supercarrier): the fighters' own numbers, round the
            // carrier 300 u out, taking anything whose hull comes inside 600 u of it, for 20 s.
            Add("Patrol", "patrol_range", "Patrol ring",   600, "u", 0);
            Add("Patrol", "patrol_orbit", "Circling at",   300, "u", 0);
            Add("Patrol", "patrol_time",  "Up for",        20, "s", 0);
        }

        // The class's OWN rows: whatever its abilities are made of (ClassDef.Rows).
        foreach (var r in Def.Rows) Add(r.Group, r.Id, r.Label, r.Base, r.Unit, r.Dec, r.Inverse);
        // ...and its DRIVE's (Drives.cs): what V is made of, on the sheet so gear can move it.
        if (Def.Drive != null) foreach (var r in Def.Drive.Rows) Add(r.Group, r.Id, r.Label, r.Base, r.Unit, r.Dec, r.Inverse);
        // ...and the CONDITIONS its category's gear can lift (Items.RowsOf): x1 until a part does.
        foreach (var r in Items.RowsOf(cls)) Add("Conditions", r.Id, r.Label, 1, "x", 2);

        if (bonuses != null)
            foreach (var kv in bonuses)
                if (_byId.TryGetValue(kv.Key, out var s)) s.Bonus = kv.Value;
        if (flats != null)
            foreach (var kv in flats)
                if (_byId.TryGetValue(kv.Key, out var s)) s.Flat = kv.Value;

        // Bombers reach twice as far as the fighters are controlled: defined FROM the
        // control range, and it takes the same bonus, so the two can never drift apart.
        if (Def.Has(Fit.Wing))
        {
            var cr = _byId["control_range"];
            Add("Bombers", "strike_range", "Strike range (2× control)", 2 * cr.Base, "u", 0);
            _byId["strike_range"].Bonus = cr.Bonus;
        }
    }

    // ── derived figures: computed from the stats above, never stored ─────────
    public double MainDpsPerBarrel => Def.Has(Fit.Guns) ? this["main_damage"] / this["main_interval"] : 0;
    public double MainDps          => MainDpsPerBarrel * this["main_count"];
    public double PdDpsPerTurret   => Def.Has(Fit.Pd) ? this["pd_damage"] / this["pd_interval"] : 0;
    // Passive, so what it deals while firing is what it sustains: there is no window to share.
    public double PdDps            => PdDpsPerTurret * this["pd_count"];
    // A full magazine of bursts, fired as fast as it allows, then reloaded: damage per cycle over cycle time.
    public double MissileDps => Def.Has(Fit.Missiles)
        ? this["missile_mag"] * PlayerShip.BurstSides.Length * this["missile_damage"]
          / (this["missile_reload"] + (this["missile_mag"] - 1) * this["missile_refire"])
        : 0;
    // One broadside's shells, and its whole cycle: the wind-up, the volleys, then the cooldown.
    public double BroadsideDamage => this["broadside_volleys"] * this["main_count"] * this["main_damage"] * this["broadside_mult"];
    public double BroadsideCycle  => this["broadside_windup"] + (this["broadside_volleys"] - 1) * this["broadside_gap"] + this["broadside_cooldown"];
    public double BroadsideDps    => Def.Has(Fit.Broadside) && BroadsideCycle > 0 ? BroadsideDamage / BroadsideCycle : 0;
    public double FighterDpsEach   => Def.Has(Fit.Wing) ? this["fighter_damage"] / this["fighter_interval"] : 0;
    public double FighterDps       => FighterDpsEach * this["fighter_count"];
    // Fighters strafe and then dock to rest, so the rate they HOLD is their firing rate over the
    // share of the time they are out -- the burst over the whole burst-and-rest cycle, the
    // sheet's own two figures.
    public double FighterDuty      => Def.Has(Fit.Wing) && this["fighter_burst"] + this["fighter_rest"] > 0
                                    ? this["fighter_burst"] / (this["fighter_burst"] + this["fighter_rest"]) : 0;
    public double FighterSustainedDps => FighterDps * FighterDuty;
    public double TorpedoesPerRun  => this["bomber_ammo"] * this["bomber_count"];
    // A bomber's whole cycle: out to the launch distance, the torpedoes away one by one, home
    // again, and the rearm on the deck. The strike was printed as "damage if all hit" and counted
    // in nothing, so the carrier's heaviest weapon was in no total at all.
    public double BomberCycle      => Def.Has(Fit.Wing)
                                    ? 2 * this["launch_range"] / this["bomber_speed"]
                                      + (this["bomber_ammo"] - 1) * this["torpedo_interval"] + this["bomber_rearm"] : 0;
    public double TorpedoDps       => BomberCycle > 0 ? TorpedoesPerRun * this["torpedo_damage"] / BomberCycle : 0;

    // Staggered fire spaces barrels evenly across one reload, so it matches salvo's
    // rate exactly: N barrels every interval, or one barrel every interval / N.
    public double StaggerStep => this["main_count"] > 0 ? this["main_interval"] / this["main_count"] : 0;

    // WHAT THIS SHIP KILLS WITH: one line per damaging system its class declares (ClassDef.Weapons).
    public IEnumerable<DpsLine> DamageLines
    {
        get { foreach (var w in Def.Weapons) yield return new DpsLine(w.Label, w.Rate(this), !w.Burst, w.Note?.Invoke(this) ?? ""); }
    }
    // The rate it can hold forever: every line that is not a burst. The window added MainDps,
    // MissileDps, BroadsideDps and PdDps, which is nine of the twelve classes short.
    public double SustainedDps
    {
        get { double t = 0; foreach (var l in DamageLines) if (l.Sustained) t += l.Dps; return t; }
    }
}

// THE CATALOGUE OF DAMAGING SYSTEMS. Every one in the game, once; a class's row (Ships.cs) lists
// the ones it carries, so the three freighters share one entry for their deployed turrets rather
// than a copy each. A new weapon is a row here and an id in the class rows that carry it -- the K
// window is not touched, and a thirteenth class prints correctly with no UI edit.
public static class Dps
{
    public static readonly DpsSource Main = new()
    {
        Label = "Main guns", Rate = s => s.MainDps,
        Note = s => $"{s["main_count"]:0} barrels at {s.MainDpsPerBarrel:0.00} DPS  ·  salvo {s["main_count"]:0} every "
                  + $"{s["main_interval"]:0.00} s = staggered 1 every {s.StaggerStep:0.00} s",
    };

    public static readonly DpsSource Broadside = new()
    {
        Label = "Broadside, averaged over its cycle", Rate = s => s.BroadsideDps,
        Note = s => $"{s["broadside_volleys"]:0} volleys × {s["main_count"]:0} shells × {s["main_damage"] * s["broadside_mult"]:0.00} "
                  + $"= {s.BroadsideDamage:0.0} damage, every {s.BroadsideCycle:0.0} s",
    };

    public static readonly DpsSource Missiles = new()
    {
        Label = "Missiles, averaged over a reload", Rate = s => s.MissileDps,
        Note = s => $"{s["missile_mag"]:0} bursts of {PlayerShip.BurstSides.Length} at {s["missile_damage"]:0.0}, "
                  + $"reloaded in {s["missile_reload"]:0.0} s",
    };

    public static readonly DpsSource Pd = new()
    {
        Label = "Point defence", Rate = s => s.PdDps,
        Note = s => $"{s["pd_count"]:0} turrets at {s.PdDpsPerTurret:0.00} DPS, always on",
    };

    public static readonly DpsSource Fighters = new()
    {
        Label = "Fighters, averaged over their rest", Rate = s => s.FighterSustainedDps,
        Note = s => $"{s["fighter_count"]:0} craft at {s.FighterDpsEach:0.00} DPS while firing, {s.FighterDuty * 100:0}% of the time",
    };

    public static readonly DpsSource Bombers = new()
    {
        Label = "Bomber torpedoes, averaged over the run and the rearm", Rate = s => s.TorpedoDps,
        Note = s => $"{s.TorpedoesPerRun:0} torpedoes × {s["torpedo_damage"]:0.0} = {s.TorpedoesPerRun * s["torpedo_damage"]:0.0} "
                  + $"every {s.BomberCycle:0.0} s, if every one hits (unguided)",
    };

    public static readonly DpsSource Deployed = new()
    {
        Label = "Deployed turrets, all of them out", Rate = s => s["deploy_damage"] / s["deploy_interval"] * s["deploy_max"],
        Note = s => $"{s["deploy_max"]:0} turrets at {s["deploy_damage"] / s["deploy_interval"]:0.00} DPS each",
    };

    public static readonly DpsSource Railgun = new()
    {
        Label = "Railgun, a perfect reload every round",
        Rate = s => s["rail_damage"] * s["rail_perfect"] / (s["rail_reload"] + s["rail_charge"]),
        Note = s => $"{s["rail_damage"] * s["rail_perfect"]:0} every {s["rail_reload"] + s["rail_charge"]:0.0} s, the reload and the charge, with a perfect reload every time ({s["rail_damage"]:0} without)",
    };

    public static readonly DpsSource Blade = new()
    {
        Label = "Blade, held", Rate = s => s["blade_damage"] / s["blade_interval"],
        Note = s => $"{s["blade_damage"]:0} to each body in its arc every {s["blade_interval"]:0.00} s",
    };

    // THE DART'S PEPPERBOX at the sheet's price (x1 up to 260 u/s; the host prices each dart from its top speed)
    public static readonly DpsSource Pepper = new()
    {
        Label = "Pepperbox, held", Rate = s => s["pepper_damage"] / s["pepper_interval"],
        Note = s => $"{s["pepper_damage"]:0.0} a dart every {s["pepper_interval"]:0.000} s off two rails, up to x{s["pepper_cap"]:0.0} as the top speed climbs past {s["price_top"]:0} u/s",
    };

    // THE DART'S ROD at the sheet's price, averaged over its cooldown (the host prices each rod from the sprint's top)
    public static readonly DpsSource Rod = new()
    {
        Label = "Rod from God, averaged over its cooldown", Rate = s => s["rod_damage"] / s["rod_cooldown"],
        Note = s => $"{s["rod_damage"]:0} through everything on its line every {s["rod_cooldown"]:0.0} s, up to x{s["rod_cap"]:0.0} as the sprint's top speed climbs past {s["price_top"]:0} u/s",
    };

    public static readonly DpsSource Hunters = new()
    {
        Label = "Hunter-seekers, averaged over their cooldown",
        Rate = s => s["hunter_count"] * s["hunter_damage"] / s["hunter_cooldown"],
        Note = s => $"{s["hunter_count"]:0} × {s["hunter_damage"]:0} = {s["hunter_count"] * s["hunter_damage"]:0} "
                  + $"every {s["hunter_cooldown"]:0.0} s",
    };

    public static readonly DpsSource Echo = new()
    {
        Label = "Echo, averaged over its cooldown",
        Rate = s => s.MainDps * s["echo_share"] * s["echo_time"] / s["echo_cooldown"],
        Note = s => $"it repeats {s["echo_share"]:0.00}× what you deal in {s["echo_time"]:0.0} s, every {s["echo_cooldown"]:0.0} s",
    };
}

// EVERY STAT IN THE GAME, by id -- the one place that can say what a stat id IS when the ship in
// hand has not got it. A window could only ever read the CURRENT class's sheet, so the equipment
// hold printed raw ids ("needs fighter_speed, fighter_damage") at a battleship, and the pilot
// window had no name for a stat its points moved on another hull. Built once, from every class.
public static class AllStats
{
    // id -> the row that describes it. Whichever class was read first owns the row's Base, so
    // nothing here hands the row out: only what a stat is CALLED and how it prints.
    private static readonly Dictionary<string, Stat> Rows = Build();
    private static Dictionary<string, Stat> Build()
    {
        var d = new Dictionary<string, Stat>();
        foreach (var c in Classes.All)
            foreach (var s in new ShipStats(c.Id).All) d.TryAdd(s.Id, s);
        return d;
    }
    // The system it belongs to: "Fighters", "Main guns", "Point defence".
    public static string Group(string id) => Rows.TryGetValue(id, out var s) ? s.Group : id;
    // The system and the row: "Fighters · Top speed".
    public static string Said(string id) => Rows.TryGetValue(id, out var s) ? $"{s.Group} · {s.Label}" : id;
    // A number in that stat's own units: "5", "1 u/s", "0.02 rad/s". It prints as finely as it
    // needs (0.##) rather than at the stat's own decimals, because what a point ADDS is often finer
    // than the figure it adds to -- 7.5 on a railgun the sheet prints whole.
    public static string Fmt(string id, double v) =>
        v.ToString("0.##") + (Rows.TryGetValue(id, out var s) && s.Unit.Length > 0 ? " " + s.Unit : "");
    // WHAT A SHARE DOES TO THE FIGURE, as a share of it: the share itself, or -- on an interval or
    // a cooldown (Stat.Inverse), which a share divides -- what the division comes to. A Gunnery
    // point's +0.5% takes 0.5% off the reload, and the pilot window says so.
    public static double Change(string id, double share) =>
        Rows.TryGetValue(id, out var s) && s.Inverse ? 1 / Stat.Scale(share) - 1 : share;
}
