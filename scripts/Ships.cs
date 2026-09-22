using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// -----------------------------------------------------------------------------
// WHAT A CLASS IS - one row each, and nothing about a class anywhere else.
//
// A class used to be spread over five files: its art and mounts in PlayerShip.Art, its numbers in
// a `V(battleship, carrier, destroyer)` helper inside every Stats row, its abilities in an
// Abilities dictionary, its name and blurb in a Classes table, and what it carried in four
// two-way tests (`Guns(c) => c is Battleship or Destroyer`). Twelve classes written that way is a
// hunt through five files for each of them, and a missed test is a class that silently cannot shoot.
//
// Now: ONE ClassDef per class -- its hull, its mounts, its deck, the numbers that differ from the
// sheet's defaults, what it is FITTED with, its abilities, its name. A new class is a row here
// plus a member of ShipClass (append only: the number is what a character file holds).
// -----------------------------------------------------------------------------

// Every class in the game. APPEND ONLY: the number is what a character file stores.
public enum ShipClass
{
    Battleship, Carrier, Destroyer,                     // line
    FreightHauler, FreightTender, FreightBastion,       // freight -- they deploy their own turrets
    HeavySniper, HeavyWarrior, HeavyWarden,             // heavy fighters
    LightDart, LightEcho, LightWraith,                  // lights
}

// What a class is FITTED with -- asked of the class, never "is it the battleship". The stat sheet
// grows the matching group, the ship builds the matching hardware and the window prints the
// matching figures, all from this one flag.
[Flags]
public enum Fit
{
    None = 0,
    Guns = 1,          // cursor-aimed main turrets
    Broadside = 2,     // every main gun, volley after volley (F)
    Missiles = 4,      // a magazine of guided bursts
    Wing = 8,          // fighters and bombers from a deck
    Pd = 16,           // point-defence turrets
}

// Every hull is the owner's line art (tools/make_ships.ps1: symmetrical, grey, tinted here with
// the hull colour), and every mount below is measured from it: the tool prints them. A turret at
// TurretTexScale 1/5.5 is 12 u across the housing, the muzzles 12.2 u from the pivot; point
// defence 6 u across, the muzzle 5.5 u out. Each class mounts them at its own multiple of that.
public class ClassArt
{
    public string Texture;
    public float Length;          // nose to tail, world units
    public float HalfWidth = 20f; // half the hull's beam: the collider and the shield
    // How far the free camera (Y) may wander from the ship: 5000 for a capital ship.
    public float CameraRange = 5000f;
    public Vector2[] Mains = Array.Empty<Vector2>(), Pds = Array.Empty<Vector2>();

    // The turrets are their own sprites (barrels up, pivot at the sheet's centre), so they
    // can turn: one main turret and one point-defence turret for every class, each class
    // mounting them at its own size (tools/make_ships.ps1 draws both).
    public string MainTurret = "res://turret_main.png", PdTurret = "res://turret_pd.png";
    public float TurretTexScale = 1f;   // world units per turret-texture pixel
    public float MainBarrel = 24f, PdBarrel = 15f, PdRing = 4.5f;   // world units

    // The carrier's deck. Bombers park in BAYS on the white either side of the runway: x out
    // from the keel, the centre of each row, the spacing along it. They lift off at the
    // runway's bow end, RunwayBow ahead of the centre, and land on its centre (world units).
    public float BayX, BayY, BaySpacing, RunwayBow;
    public float EngineInset;          // the engine's plume this far in from the stern (world units)
}

public class ClassDef
{
    public ShipClass Id;
    public string Name, Blurb;
    public bool Ready;                       // flyable: the creator offers it, Sanitize accepts it
    public Fit Fit;
    public ClassArt Art = new();
    // This class's own numbers, by stat id. Everything it does NOT name it takes from the sheet's
    // default (Stats.cs), so a row here is a difference, never a copy.
    public Dictionary<string, double> Nums = new();
    public AbilityDef[] Abilities = Array.Empty<AbilityDef>();
    public string Hint = "";                 // the controls line's own part, before the common one
    public bool Has(Fit f) => (Fit & f) != 0;
}

public static class Classes
{
    public static readonly ClassDef[] All =
    {
        // -- page 1: the line ------------------------------------------------
        new() { Id = ShipClass.Battleship, Name = "BATTLESHIP", Ready = true, Fit = Fit.Guns | Fit.Broadside | Fit.Pd,
            Blurb = "300 hull. Four cursor-aimed main guns, a broadside of all four, two point-defence turrets.",
            Hint = "BATTLESHIP  ·  mouse aims the main guns",
            Nums = new() {
                ["hull"] = 300,
                ["thrust"] = 56, ["reverse_thrust"] = 24, ["max_speed"] = 104, ["reverse_speed"] = 36,
                ["turn_radius"] = 107, ["turn_rate"] = 1.08,
                ["main_count"] = 4, ["main_damage"] = 17.9, ["main_interval"] = 2.0, ["main_range"] = 1000, ["shell_speed"] = 650,
                ["pd_count"] = 2,
            },
            Art = new ClassArt {
                Texture = "res://battleship_hull.png", Length = 378f, HalfWidth = 43.875f,
                Mains = new Vector2[] { new(0f, -93.85f), new(0f, -7.56f), new(0f, 73.34f), new(0f, 122.15f) },
                Pds   = new Vector2[] { new(-21.3f, 156.93f), new(21.3f, 156.93f) },
                TurretTexScale = 2.5f / 5.5f, MainBarrel = 30.5f, PdBarrel = 13.75f, PdRing = 7.5f },
            Abilities = new[]
            {
                new AbilityDef { Id = "guns",     Name = "Main guns",     Short = "GUNS",   Kind = AbilityKind.Hold, Default = Key.Space,
                                 Blurb = "Hold to fire. The barrels aim at the cursor and swing slowly." },
                new AbilityDef { Id = "firemode", Name = "Fire mode",     Short = "MODE",   Default = Key.G,
                                 Blurb = "Salvo (all barrels at once) or staggered (one at a time). Same rate." },
                new AbilityDef { Id = "broadside", Name = "Broadside",    Short = "BROADSIDE", Default = Key.F,
                                 Blurb = "The turrets swing onto the cursor, then every main gun fires three volleys. The ship steers throughout." },
                new AbilityDef { Id = "pd",       Name = "Point defence", Short = "PD",     Default = Key.Q,
                                 Blurb = "Opens a firing window; each turret picks and tracks its own target. Recharges after." },
            }, },

        new() { Id = ShipClass.Carrier, Name = "CARRIER", Ready = true, Fit = Fit.Wing | Fit.Pd,
            Blurb = "200 hull. Three point-defence turrets, three fighters, two torpedo bombers.",
            Hint = "CARRIER",
            Nums = new() {
                ["hull"] = 200,
                ["thrust"] = ShipStats.CarrierTop / 2, ["reverse_thrust"] = ShipStats.CarrierTop * 5 / 24, ["max_speed"] = ShipStats.CarrierTop, ["reverse_speed"] = ShipStats.CarrierTop / 3,
                ["turn_radius"] = 127, ["turn_rate"] = 0.9,
                ["pd_count"] = 3, ["pd_turn"] = Mathf.Tau / 1.2f,
            },
            Art = new ClassArt {
                Texture = "res://carrier_player.png", Length = 283.5f, HalfWidth = 40.02f,
                BayX = 25.01f, BayY = 8.34f, BaySpacing = 46.69f, RunwayBow = 110.06f, EngineInset = 8f,
                Pds = new Vector2[] { new(-46.39f, -29.75f), new(46.39f, -29.75f), new(0f, 134.22f) },
                TurretTexScale = 1.9178f / 5.5f, PdBarrel = 10.51f, PdRing = 5.76f },
            Abilities = new[]
            {
                new AbilityDef { Id = "attack",  Name = "Fighters: attack", Short = "ATTACK", Default = Key.Space,
                                 Blurb = "Sends the fighters at the selected target while it is within control range." },
                new AbilityDef { Id = "recall",  Name = "Fighters: recall", Short = "RECALL", Default = Key.R,
                                 Blurb = "Calls the fighters home: they dock inside the carrier." },
                new AbilityDef { Id = "bombers", Name = "Bomber strike",    Short = "BOMB",   Default = Key.F,
                                 Blurb = "Bombers run at the target and launch torpedoes straight ahead. No tracking." },
                new AbilityDef { Id = "pd",      Name = "Point defence",    Short = "PD",     Default = Key.Q,
                                 Blurb = "Opens a firing window; each turret picks and tracks its own target. Recharges after." },
            }, },

        new() { Id = ShipClass.Destroyer, Name = "DESTROYER", Ready = true, Fit = Fit.Guns | Fit.Missiles | Fit.Pd,
            Blurb = "250 hull. The fastest. Two cursor-aimed main guns, missile bursts of three, two point-defence turrets.",
            Hint = "DESTROYER  ·  mouse aims the main guns",
            Nums = new() {
                ["hull"] = 250,
                ["thrust"] = 70, ["reverse_thrust"] = 30, ["max_speed"] = 130, ["reverse_speed"] = 45,
                ["turn_radius"] = 107, ["turn_rate"] = 1.08,
                ["main_count"] = 2, ["main_damage"] = 7.5, ["main_interval"] = 1.0, ["main_range"] = 720, ["shell_speed"] = 520,
                ["pd_count"] = 2,
            },
            Art = new ClassArt {
                Texture = "res://destroyer_hull.png", Length = 212.625f, HalfWidth = 27.8f,
                Mains = new Vector2[] { new(0f, -57.31f), new(0f, 24.66f) },
                Pds   = new Vector2[] { new(-13.39f, 68.16f), new(13.39f, 68.16f) },
                TurretTexScale = 1.3085f / 5.5f, MainBarrel = 16.03f, PdBarrel = 7.2f, PdRing = 3.93f },
            Abilities = new[]
            {
                new AbilityDef { Id = "guns",     Name = "Main guns",     Short = "GUNS",   Kind = AbilityKind.Hold, Default = Key.Space,
                                 Blurb = "Hold to fire. The barrels aim at the cursor and swing slowly." },
                new AbilityDef { Id = "firemode", Name = "Fire mode",     Short = "MODE",   Default = Key.G,
                                 Blurb = "Salvo (both barrels at once) or staggered (one at a time). Same rate." },
                new AbilityDef { Id = "missile",  Name = "Missile burst", Short = "MSL",    Default = Key.F,
                                 Blurb = "Three guided missiles: one at the target, two launched wide that curve in. Needs a selected target in range. Uses the magazine." },
                new AbilityDef { Id = "reload",   Name = "Reload missiles", Short = "RELOAD", Default = Key.R,
                                 Blurb = "Refills the missile magazine. Nothing fires while it runs." },
                new AbilityDef { Id = "pd",       Name = "Point defence", Short = "PD",     Default = Key.Q,
                                 Blurb = "Opens a firing window; each turret picks and tracks its own target. Recharges after." },
            }, },

        // -- page 2: freight, which carries its own defences -------------------
        new() { Id = ShipClass.FreightHauler, Name = "FREIGHTER", Blurb = "Reserved." },
        new() { Id = ShipClass.FreightTender, Name = "TENDER", Blurb = "Reserved." },
        new() { Id = ShipClass.FreightBastion, Name = "BASTION", Blurb = "Reserved." },

        // -- page 3: heavy fighters --------------------------------------------
        new() { Id = ShipClass.HeavySniper, Name = "SNIPER", Blurb = "Reserved." },
        new() { Id = ShipClass.HeavyWarrior, Name = "WARRIOR", Blurb = "Reserved." },
        new() { Id = ShipClass.HeavyWarden, Name = "WARDEN", Blurb = "Reserved." },

        // -- page 4: lights -----------------------------------------------------
        new() { Id = ShipClass.LightDart, Name = "DART", Blurb = "Reserved." },
        new() { Id = ShipClass.LightEcho, Name = "ECHO", Blurb = "Reserved." },
        new() { Id = ShipClass.LightWraith, Name = "WRAITH", Blurb = "Reserved." },
    };

    private static readonly Dictionary<ShipClass, ClassDef> ById = All.ToDictionary(c => c.Id);

    public static ClassDef Of(ShipClass c) => ById[c];
    public static ClassArt Art(ShipClass c) => ById[c].Art;
    public static bool Has(ShipClass c, Fit f) => ById[c].Has(f);
    // Its name as the screens print it ("BATTLESHIP"), from the one table that holds it.
    public static string NameOf(ShipClass c) => ById[c].Name;
    // A class number from a file or a packet: one this build can actually fly, or the Battleship.
    public static ShipClass Sanitize(int cls) =>
        Enum.IsDefined(typeof(ShipClass), cls) && ById[(ShipClass)cls].Ready ? (ShipClass)cls : ShipClass.Battleship;

    public const int PerPage = 3;
    public static int Pages => (All.Length + PerPage - 1) / PerPage;
}
