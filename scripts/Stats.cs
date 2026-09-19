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
// Each stat is Base x (1 + Bonus). Bonuses are fractions keyed by stat id
// (0.10 = +10%), stored on the character. Nothing grants bonuses yet, so they all
// read +0% until a skill or upgrade system exists to fill them.
//
// Interval stats (time between shots) are the exception: a rate-of-fire bonus
// DIVIDES them, so +100% RoF halves the interval and doubles the DPS.
// ─────────────────────────────────────────────────────────────────────────────
public class Stat
{
    public string Id, Label, Unit, Group;
    public double Base, Bonus;
    public bool Inverse;           // an interval: bonus divides instead of multiplies
    public int Decimals = 1;

    public double Value => Inverse ? Base / (1.0 + Bonus) : Base * (1.0 + Bonus);
    public string Fmt(double v) => v.ToString("F" + Decimals) + (Unit.Length > 0 ? " " + Unit : "");
}

public class ShipStats
{
    public readonly ShipClass Class;
    public readonly List<Stat> All = new();
    private readonly Dictionary<string, Stat> _byId = new();

    public double this[string id] => _byId.TryGetValue(id, out var s) ? s.Value : 0;

    private void Add(string group, string id, string label, double b, string unit, int dec = 1, bool inverse = false)
    {
        var s = new Stat { Group = group, Id = id, Label = label, Base = b, Unit = unit, Decimals = dec, Inverse = inverse };
        All.Add(s); _byId[id] = s;
    }

    public ShipStats(ShipClass cls, IReadOnlyDictionary<string, double> bonuses = null)
    {
        Class = cls;
        bool bs = cls == ShipClass.Battleship;

        Add("Hull",   "hull",          "Hull points",        bs ? 300 : 200, "", 0);

        // Capital ships handle like naval ships: thrust only along the keel, sideways
        // drift bleeds off fast, and they turn on a radius -- no strafing, no pivoting.
        // 0.3.x: speeds and accelerations are 40% of 0.3.0's (same time to get under
        // way, far less top speed); the radius is chosen so that at full speed they
        // still turn 20% faster than 0.3.0 did (battleship 0.81 -> 0.97 rad/s,
        // carrier 0.63 -> 0.76), and the rudder limit is up 20% so it never caps that.
        Add("Helm", "thrust",         "Ahead acceleration",  bs ? 56 : 48, "u/s²", 0);
        Add("Helm", "reverse_thrust", "Astern acceleration", bs ? 24 : 20, "u/s²", 0);
        Add("Helm", "max_speed",      "Top speed ahead",     bs ? 104 : 96, "u/s", 0);
        Add("Helm", "reverse_speed",  "Top speed astern",    bs ? 36 : 32, "u/s", 0);
        Add("Helm", "turn_radius",    "Turning radius",      bs ? 107 : 127, "u", 0, inverse: true);
        Add("Helm", "turn_rate",      "Rudder limit",        bs ? 1.08 : 0.9, "rad/s", 2);
        Add("Helm", "water_drag",     "Drag",                0.35, "/s", 2);
        Add("Helm", "keel",           "Keel grip (drift loss)", 4.0, "/s", 1);

        if (bs)
        {
            Add("Main guns", "main_count",    "Barrels",            4, "", 0);
            Add("Main guns", "main_damage",   "Damage per shot",    1.5, "", 2);
            Add("Main guns", "main_interval", "Reload (per barrel)",1.0, "s", 2, inverse: true);
            Add("Main guns", "main_range",    "Range",              720, "u", 0);
            Add("Main guns", "main_turn",     "Turret turn rate",   Mathf.Tau / 4f, "rad/s", 2);

            // A magazine, reloaded by hand (R): 2 missiles, then a 16 s reload.
            Add("Missile", "missile_damage",   "Damage",            5.0, "", 1);
            Add("Missile", "missile_mag",      "Magazine",          2, "", 0);
            Add("Missile", "missile_refire",   "Between shots",     0.6, "s", 1, inverse: true);
            Add("Missile", "missile_reload",   "Reload (R)",        16.0, "s", 1, inverse: true);
            Add("Missile", "missile_range",    "Range",             900, "u", 0);
            // a bunker buster: slow, and barely guided
            Add("Missile", "missile_speed",    "Speed",             130, "u/s", 0);
            Add("Missile", "missile_turn",     "Guidance (turn)",   0.35, "rad/s", 2);
        }

        // An active ability: activating opens the firing window; the reload runs after
        // it closes. Battleship mounts are heavier and swing slower than the carrier's.
        Add("Point defence", "pd_count",    "Turrets",           bs ? 2 : 3, "", 0);
        Add("Point defence", "pd_damage",   "Damage per shot",   0.5, "", 2);
        Add("Point defence", "pd_interval", "Reload",            0.5, "s", 2, inverse: true);
        Add("Point defence", "pd_range",    "Range",             460, "u", 0);
        Add("Point defence", "pd_turn",     "Turret turn rate",  bs ? Mathf.Tau / 3f : Mathf.Tau / 1.2f, "rad/s", 2);
        Add("Point defence", "pd_active",   "Firing window",     15, "s", 0);
        Add("Point defence", "pd_reload",   "Recharge after",    15, "s", 0, inverse: true);

        if (!bs)
        {
            Add("Fighters", "fighter_count",    "Craft",            4, "", 0);
            Add("Fighters", "fighter_hp",       "Hull each",        48, "", 0);
            Add("Fighters", "fighter_damage",   "Damage per shot",  0.175, "", 3);
            Add("Fighters", "fighter_interval", "Reload",           0.35, "s", 2, inverse: true);
            Add("Fighters", "fighter_range",    "Weapon range",     300, "u", 0);
            Add("Fighters", "fighter_speed",    "Top speed",        352, "u/s", 0);
            Add("Fighters", "fighter_accel",    "Acceleration",     900, "u/s²", 0);
            Add("Fighters", "fighter_burst",    "Firing before rest", 15, "s", 0);
            Add("Fighters", "fighter_rest",     "Rest at the carrier", 3, "s", 0, inverse: true);
            Add("Fighters", "control_range",    "Control range",    1400, "u", 0);

            // An active ability. Torpedoes run straight and steady: no tracking, so a
            // moving target can step out of the way.
            Add("Bombers", "bomber_count",    "Craft",              2, "", 0);
            Add("Bombers", "bomber_hp",       "Hull each",          110, "", 0);
            Add("Bombers", "torpedo_damage",  "Torpedo damage",     3.0, "", 1);
            Add("Bombers", "bomber_ammo",     "Torpedoes per run",  4, "", 0);
            Add("Bombers", "torpedo_interval","Between launches",   0.35, "s", 2, inverse: true);
            Add("Bombers", "torpedo_speed",   "Torpedo speed",      120, "u/s", 0);
            Add("Bombers", "torpedo_range",   "Torpedo run",        1215, "u", 0);
            Add("Bombers", "launch_range",    "Launch distance",    567, "u", 0);
            Add("Bombers", "bomber_rearm",    "Rearm on the carrier", 6, "s", 1, inverse: true);
            Add("Bombers", "bomber_speed",    "Top speed",          190, "u/s", 0);
            Add("Bombers", "bomber_accel",    "Acceleration",       500, "u/s²", 0);
        }

        if (bonuses != null)
            foreach (var kv in bonuses)
                if (_byId.TryGetValue(kv.Key, out var s)) s.Bonus = kv.Value;

        // Bombers reach twice as far as the fighters are controlled: defined FROM the
        // control range, and it takes the same bonus, so the two can never drift apart.
        if (!bs)
        {
            var cr = _byId["control_range"];
            Add("Bombers", "strike_range", "Strike range (2× control)", 2 * cr.Base, "u", 0);
            _byId["strike_range"].Bonus = cr.Bonus;
        }
    }

    // ── derived figures: computed from the stats above, never stored ─────────
    public double MainDpsPerBarrel => Class == ShipClass.Battleship ? this["main_damage"] / this["main_interval"] : 0;
    public double MainDps          => MainDpsPerBarrel * this["main_count"];
    public double PdDpsPerTurret   => this["pd_damage"] / this["pd_interval"];
    // PD only fires during its window, so its sustained rate is scaled by the duty fraction.
    public double PdDuty           => this["pd_active"] / (this["pd_active"] + this["pd_reload"]);
    public double PdDps            => PdDpsPerTurret * this["pd_count"];
    public double PdSustainedDps   => PdDps * PdDuty;
    // A full magazine, fired as fast as it allows, then reloaded: damage per cycle over cycle time.
    public double MissileDps => Class == ShipClass.Battleship
        ? this["missile_mag"] * this["missile_damage"]
          / (this["missile_reload"] + (this["missile_mag"] - 1) * this["missile_refire"])
        : 0;
    public double FighterDpsEach   => Class == ShipClass.Carrier ? this["fighter_damage"] / this["fighter_interval"] : 0;
    public double FighterDps       => FighterDpsEach * this["fighter_count"];
    public double TorpedoesPerRun  => this["bomber_ammo"] * this["bomber_count"];

    // Staggered fire spaces barrels evenly across one reload, so it matches salvo's
    // rate exactly: N barrels every interval, or one barrel every interval / N.
    public double StaggerStep => this["main_count"] > 0 ? this["main_interval"] / this["main_count"] : 0;
}
