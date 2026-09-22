using Godot;
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
    public double Value
    {
        get
        {
            double k = System.Math.Max(MinScale, 1.0 + Bonus);
            return Inverse ? (Base + Flat) / k : (Base + Flat) * k;
        }
    }
    public string Fmt(double v) => v.ToString("F" + Decimals) + (Unit.Length > 0 ? " " + Unit : "");
}

public class ShipStats
{
    // The carrier's pace: every one of its helm figures is a fraction of its own top speed, so it
    // gets under way on its own clock. Named here because the class's row (Ships.cs) is written
    // in terms of it.
    public const double CarrierTop = 116.48;

    public readonly ShipClass Class;
    public readonly ClassDef Def;
    public readonly List<Stat> All = new();
    private readonly Dictionary<string, Stat> _byId = new();

    public double this[string id] => _byId.TryGetValue(id, out var s) ? s.Value : 0;

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

        // Capital ships handle like naval ships: thrust only along the keel, sideways drift
        // bleeds off fast, and they turn on a radius -- no strafing; almost stopped, the rudder
        // pivots the hull slowly. The battleship sets the pace and is the default below: the
        // carrier is 12% faster (116.48 u/s) and the destroyer, the fastest capital ship, 25%
        // (130). Each class's own figures are in its row (Ships.cs).
        Add("Helm", "thrust",         "Ahead acceleration",  56, "u/s²", 0);
        Add("Helm", "reverse_thrust", "Astern acceleration", 24, "u/s²", 0);
        Add("Helm", "max_speed",      "Top speed ahead",     104, "u/s", 0);
        Add("Helm", "reverse_speed",  "Top speed astern",    36, "u/s", 0);
        Add("Helm", "turn_radius",    "Turning radius",      107, "u", 0, inverse: true);
        Add("Helm", "turn_rate",      "Rudder limit",        1.08, "rad/s", 2);
        Add("Helm", "water_drag",     "Drag",                0.35, "/s", 2);
        Add("Helm", "keel",           "Keel grip (drift loss)", 4.0, "/s", 1);

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
        {   // An active ability: activating opens the firing window; the reload runs after
            // it closes. Battleship and destroyer mounts are heavier and swing slower than the carrier's.
            Add("Point defence", "pd_count",    "Turrets",           2, "", 0);
            Add("Point defence", "pd_damage",   "Damage per shot",   0.5, "", 2);
            Add("Point defence", "pd_interval", "Reload",            0.5, "s", 2, inverse: true);
            Add("Point defence", "pd_range",    "Range",             460, "u", 0);
            Add("Point defence", "pd_turn",     "Turret turn rate",  Mathf.Tau / 3f, "rad/s", 2);
            Add("Point defence", "pd_active",   "Firing window",     15, "s", 0);
            Add("Point defence", "pd_reload",   "Recharge after",    15, "s", 0, inverse: true);
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
        }

        // The class's OWN rows: whatever its abilities are made of (ClassDef.Rows).
        foreach (var r in Def.Rows) Add(r.Group, r.Id, r.Label, r.Base, r.Unit, r.Dec, r.Inverse);

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
    // PD only fires during its window, so its sustained rate is scaled by the duty fraction.
    public double PdDuty           => Def.Has(Fit.Pd) ? this["pd_active"] / (this["pd_active"] + this["pd_reload"]) : 0;
    private double PdDps            => PdDpsPerTurret * this["pd_count"];
    public double PdSustainedDps   => PdDps * PdDuty;
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
    public double TorpedoesPerRun  => this["bomber_ammo"] * this["bomber_count"];

    // Staggered fire spaces barrels evenly across one reload, so it matches salvo's
    // rate exactly: N barrels every interval, or one barrel every interval / N.
    public double StaggerStep => this["main_count"] > 0 ? this["main_interval"] / this["main_count"] : 0;
}
