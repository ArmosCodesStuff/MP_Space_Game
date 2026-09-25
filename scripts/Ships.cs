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
    Pd = 16,           // point-defence turrets: passive, firing whenever the ship is alive
    Deploy = 32,       // it drops turrets of its own and picks them up again (the freighters)
}

// Every hull is the owner's pack art (tools/make_ships.ps1: turned nose-up, trimmed, grey, tinted
// here with the hull colour), and every mount below is measured from it: the tool prints them. A turret at
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
    public float MainBarrel = 24f, PdBarrel = 15f;   // world units

    // The carrier's deck. Bombers park in BAYS on the white either side of the runway: x out
    // from the keel, the centre of each row, the spacing along it. They lift off at the
    // runway's bow end, RunwayBow ahead of the centre, and land on its centre (world units).
    public float BayX, BayY, BaySpacing, RunwayBow;
    public float EngineInset;          // the engine's plume this far in from the stern (world units)
}

// One row of the stat sheet, declared by the class that needs it (ClassDef.Rows).
public class StatRow
{
    public string Group, Id, Label, Unit = "";
    public double Base;
    public int Dec = 1;
    public bool Inverse;                     // an interval: a rate bonus divides it
}

public class ClassDef
{
    public ShipClass Id;
    public string Name, Blurb;
    public bool Ready;                       // flyable: the creator offers it, Sanitize accepts it
    // HOW MANY THINGS IT MAY HOLD AT ONCE. One for a hull with one pair of eyes; three on the
    // capitals, which have the crew and the fire control for it. "Capital ships only" is this
    // number, not a list of class names in an `if` -- a thirteenth class states its own answer.
    public int Targets = 1;
    public Fit Fit;
    // WHAT ITS MAIN MOUNTS FIRE: a row of Shots.All (TurretSpec.Kind). A shell unless the class says
    // otherwise -- the freighter's spotter round, whose hit paints (ShotDef.Paint).
    public int MainShot = Shots.Shell;
    public ClassArt Art = new();
    // This class's own numbers, by stat id. Everything it does NOT name it takes from the sheet's
    // default (Stats.cs), so a row here is a difference, never a copy.
    public Dictionary<string, double> Nums = new();
    // WHAT ITS WEAPONS ARE, and what ONE pilot Weapons level adds to each, in that stat's own
    // units. The one place a class says this: Progression turns it into the pilot's flat
    // additions, and the gear that lifts "every weapon" (the Combat Chip, the Glass Array) takes
    // its share on every id any class names here. A 0 is a weapon gear reaches and the pilot's
    // points do not -- which is exactly what the carrier's fighters and the destroyer's missiles
    // were before this existed, and a saved pilot must not notice the change.
    // It defaults to EMPTY on purpose: a new class that forgets it is caught by a check, rather
    // than quietly given the battleship's gun.
    public Dictionary<string, double> Damage = new();
    // WHAT ITS WEAPONS REACH, by stat id -- the same shape as Damage and for the same reason. A
    // pilot's REACH points and the Targeting Chip both lift these, and both used to work off a
    // list written somewhere else: the chip's was six ids hardcoded in Equipment, which did
    // nothing at all for seven of the nine newest classes. The share is per id, so a class whose
    // reach should climb slower says so.
    public Dictionary<string, double> Reach = new();
    // ...and HOW FAST THEY CYCLE: the seconds-between-shots ids, for the pilot's Gunnery points.
    // A share of one of these is applied NEGATIVE (Progression): a shorter interval is the gain.
    public Dictionary<string, double> Cycle = new();
    // WHAT IT KILLS WITH, for the K window's DAMAGE block: the damaging systems it carries, by id
    // out of the one catalogue (Dps, in Stats.cs). Each one works its own sustained rate out of
    // this ship's sheet, and the window adds up the ones the ship can hold. The window used to ask
    // `if (Fit.Guns) ... else` and total four named figures, so the railgun, the hunters, the EMP,
    // the deployed turrets, the echo and the carrier's torpedoes were printed nowhere and counted
    // in nothing -- a sniper read 7.50 DPS beside a railgun worth 37.50.
    public DpsSource[] Weapons = Array.Empty<DpsSource>();
    // Rows only this class has: the numbers its own abilities are made of. They join the sheet
    // like any other, so the K window prints them and gear and purchases can move them, without
    // Stats.cs knowing that this class exists.
    public StatRow[] Rows = Array.Empty<StatRow>();
    // THE PARTS IT IS BORN WITH -- its weapon mount and its signature system -- as ordinary
    // ItemDefs (ItemDef.Own, Equipment.cs): the same type a boss drops, so there is one part type
    // and one fitting rule. A kit part changes no numbers (the sheet already IS the ship): it is
    // a label, its id is what a save file holds, and it names the SIGNATURE stat of the hulls
    // born with it, so it fits those and nothing else. More than one class may carry the same
    // part (the freighters' cargo gun): it is written once, above All. The drive, the deflector,
    // the frame and the chips are every ship's and live in Equipment. Everything ELSE it can wear
    // follows from its sheet by that same rule (Equipment.Fits).
    public ItemDef[] Kit = Array.Empty<ItemDef>();
    public AbilityDef[] Abilities = Array.Empty<AbilityDef>();
    // WHAT V DOES: one row of Drives.All (the warp on the capitals, the boost on the nine). Not in
    // Abilities: Abilities.For appends it after them, so it has a slot and no level wall.
    public DriveDef Drive;
    public string Hint = "";                 // the controls line's own part, before the common one
    public bool Has(Fit f) => (Fit & f) != 0;
}

public static class Classes
{
    // THE MOUNTS MORE THAN ONE HULL IS BORN WITH -- one ItemDef each, fitted to every hull that
    // carries it below. A part fits the hulls whose SIGNATURE row it names (ItemDef.Needs), so a
    // shared mount names one id per hull: the cargo gun the three freighters' three systems, the
    // light cannon the sniper's railgun and the warden's hunters, the dart cannon the three
    // lights'. Written above All because static fields start in the order they are written, and
    // All is what fits them.
    private static readonly ItemDef CargoGun = ItemDef.Own(GearSlot.Weapon, "freight_main_gun", "Mk I Cargo Gun",
        "the freighter's single main turret", "bubble_pool", "overdrive_mult", "wave_range");
    private static readonly ItemDef HeavyCannon = ItemDef.Own(GearSlot.Weapon, "heavy_main_gun", "Mk I Light Cannon",
        "the single light turret", "rail_damage", "hunter_count");
    private static readonly ItemDef LightCannon = ItemDef.Own(GearSlot.Weapon, "light_main_gun", "Mk I Dart Cannon",
        "the light's single turret", "roll_time", "echo_time", "stealth_time");

    public static readonly ClassDef[] All =
    {
        // -- page 1: the line ------------------------------------------------
        new() { Id = ShipClass.Battleship, Name = "BATTLESHIP", Ready = true, Targets = 3, Fit = Fit.Guns | Fit.Broadside | Fit.Pd,
            Blurb = "Four cursor-aimed main guns, a broadside of all four, two point-defence turrets.",
            Hint = "BATTLESHIP  ·  mouse aims the main guns",
            Drive = Drives.Warp,
            Nums = new() {
                ["hull"] = 300,
                ["thrust"] = 47, ["reverse_thrust"] = 20, ["max_speed"] = 88, ["reverse_speed"] = 30,
                ["turn_radius"] = 107, ["turn_rate"] = 1.08,
                ["main_count"] = 4, ["main_damage"] = 17.9, ["main_interval"] = 2.0, ["main_range"] = 1000, ["shell_speed"] = 650,
                ["pd_count"] = 2,
            },
            Damage = new() { ["main_damage"] = 1 },
            Reach = new() { ["main_range"] = 1, ["pd_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Broadside, Dps.Pd },
            Kit = new[] {
                ItemDef.Own(GearSlot.Weapon, "bs_main_battery", "Mk I Main Battery", "the four main turrets", "broadside_mult"),
                ItemDef.Own(GearSlot.Utility, "bs_broadside", "Broadside Battery", "every main gun, volley on volley", "broadside_mult"),
            },
            Art = new ClassArt {
                // battleship_bb05 (the pack, J5): 4 mains on the flanking twins (the two forward
                // rows, both sides -- Q3's default), the barrels of all 6 painted twins patched
                // clean (Q4); PD on the aft domes; the turret scale k measured off the art's own housing
                Texture = "res://battleship_hull.png", Length = 378f, HalfWidth = 43.875f,
                Mains = new Vector2[] { new(-23.99f, -74.28f), new(23.99f, -74.28f), new(-23.99f, -36.27f), new(23.99f, -36.27f) },
                Pds   = new Vector2[] { new(-58.99f, 71.73f), new(58.99f, 71.73f) },
                TurretTexScale = 1.9f / 5.5f, MainBarrel = 23.18f, PdBarrel = 10.45f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Broadside } },

        new() { Id = ShipClass.Carrier, Name = "CARRIER", Ready = true, Targets = 3, Fit = Fit.Wing | Fit.Pd,
            Blurb = "No main gun: point defence, a fighter wing, torpedo bombers off its deck.",
            Hint = "CARRIER",
            Drive = Drives.Warp,
            Nums = new() {
                ["hull"] = 200,
                ["thrust"] = ShipStats.CarrierTop / 2, ["reverse_thrust"] = ShipStats.CarrierTop * 5 / 24, ["max_speed"] = ShipStats.CarrierTop, ["reverse_speed"] = ShipStats.CarrierTop / 3,
                ["turn_radius"] = 127, ["turn_rate"] = 0.9,
                ["pd_count"] = 3, ["pd_turn"] = Mathf.Tau / 1.2f,
            },
                // the 0: gear reaches the fighters, the pilot's points do not -- as it was
            Damage = new() { ["torpedo_damage"] = 1, ["fighter_damage"] = 0 },
            Reach = new() { ["fighter_range"] = 1, ["control_range"] = 1, ["torpedo_range"] = 1, ["launch_range"] = 1, ["pd_range"] = 1, ["patrol_range"] = 1 },
            Cycle = new() { ["fighter_interval"] = 1, ["bomber_rearm"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Fighters, Dps.Bombers, Dps.Pd },
            Kit = new[] {
                ItemDef.Own(GearSlot.Weapon, "cv_fighter_hangars", "Mk I Fighter Hangars", "the fighter wing", "fighter_count"),
                ItemDef.Own(GearSlot.Utility, "cv_bomber_bay", "Bomber Bay", "the bomber wing", "bomber_count"),
            },
            Art = new ClassArt {
                // carrier_a (the pack, J5): a PD pair on the flanks amidships and one on the stern
                // centreline; the deck (Bay*/RunwayBow/EngineInset) is the bays abreast amidships,
                // the runway run from the bow and the engines inset at the stern
                Texture = "res://carrier_player.png", Length = 283.5f, HalfWidth = 40.02f,
                BayX = 25.01f, BayY = 8.34f, BaySpacing = 46.69f, RunwayBow = 110.06f, EngineInset = 8f,
                Pds = new Vector2[] { new(-30.01f, -0.21f), new(30.01f, -0.21f), new(0f, 134.22f) },
                TurretTexScale = 1.9178f / 5.5f, PdBarrel = 10.51f },
            Abilities = new[] { Ab.Attack, Ab.Recall, Ab.Bombers } },

        new() { Id = ShipClass.Destroyer, Name = "DESTROYER", Ready = true, Targets = 3, Fit = Fit.Guns | Fit.Missiles | Fit.Pd,
            Blurb = "Fastest of the line. Two cursor-aimed main guns, missile bursts of three, two point-defence turrets.",
            Hint = "DESTROYER  ·  mouse aims the main guns",
            Drive = Drives.Warp,
            Nums = new() {
                ["hull"] = 250,
                ["thrust"] = 63, ["reverse_thrust"] = 27, ["max_speed"] = 117, ["reverse_speed"] = 40.5,
                ["turn_radius"] = 107, ["turn_rate"] = 1.08,
                ["main_count"] = 2, ["main_damage"] = 7.5, ["main_interval"] = 1.0, ["main_range"] = 720, ["shell_speed"] = 520,
                ["pd_count"] = 2,
            },
                // the 0: gear reaches the missiles, the pilot's points do not -- as it was
            Damage = new() { ["main_damage"] = 1, ["missile_damage"] = 0 },
            Reach = new() { ["main_range"] = 1, ["missile_range"] = 1, ["pd_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["missile_reload"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Missiles, Dps.Pd },
            Kit = new[] {
                ItemDef.Own(GearSlot.Weapon, "dd_main_battery", "Mk I Twin Turrets", "the two main turrets", "missile_mag"),
                ItemDef.Own(GearSlot.Utility, "dd_missile_rack", "Missile Rack", "the missile bursts and their magazine", "missile_mag"),
            },
            Art = new ClassArt {
                // destroyer_dd22 (the pack, J5): both mains on the keel gun cluster near the bow,
                // PD re-seated on the flank domes
                Texture = "res://destroyer_hull.png", Length = 212.625f, HalfWidth = 27.8f,
                Mains = new Vector2[] { new(0.20f, -14.20f), new(0.20f, 17.81f) },
                Pds   = new Vector2[] { new(-30.81f, 5.82f), new(30.81f, 5.82f) },
                TurretTexScale = 1.3085f / 5.5f, MainBarrel = 16.03f, PdBarrel = 7.2f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Missile, Ab.Reload } },

        // -- page 2: freight, which carries its own defences -------------------
        new() { Id = ShipClass.FreightHauler, Name = "FREIGHTER", Ready = true, Fit = Fit.Guns | Fit.Pd | Fit.Deploy, MainShot = Shots.Spotter,
            Blurb = "Toughest hull there is. A spotter cannon that paints what it hits, three sentries that shoot the paint first, and a bubble that soaks damage.",
            Hint = "FREIGHTER  ·  mouse aims the spotter  ·  a hit paints",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 450,
                ["thrust"] = 63.5, ["reverse_thrust"] = 28.2, ["max_speed"] = 120, ["reverse_speed"] = 42.4,
                ["turn_radius"] = 150, ["turn_rate"] = 0.85, ["strafe_speed"] = 60, ["strafe_thrust"] = 120,
                ["main_count"] = 1, ["main_damage"] = 31.25, ["main_interval"] = 1.25, ["main_range"] = 800, ["shell_speed"] = 560,   // the spotter: 25 DPS
                ["pd_count"] = 2,
            },
                // half a point each: three turrets are out at once, so a level is worth 1.5 across them
            Damage = new() { ["main_damage"] = 1, ["deploy_damage"] = 0.5 },
            Reach = new() { ["main_range"] = 1, ["deploy_range"] = 1, ["pd_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["deploy_interval"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Deployed, Dps.Pd },
            Kit = new[] {
                CargoGun,
                ItemDef.Own(GearSlot.Utility, "freight_bubble", "Bubble Projector", "the bubble, and what it soaks", "bubble_pool"),
            },
            Rows = new StatRow[] {
                new() { Group = "Deployed turrets", Id = "deploy_damage",   Label = "Damage per shot",  Base = 5, Dec = 1 },
                new() { Group = "Deployed turrets", Id = "deploy_interval", Label = "Reload",           Base = 0.5, Unit = "s", Dec = 2, Inverse = true },
                new() { Group = "Deployed turrets", Id = "deploy_range",    Label = "Range",            Base = 650, Unit = "u", Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_hull",     Label = "Turret hull",      Base = 120, Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_max",      Label = "Out at once",      Base = 3, Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_cooldown", Label = "Between drops",    Base = 6, Unit = "s", Dec = 1, Inverse = true },
                new() { Group = "Deployed turrets", Id = "deploy_reach",    Label = "Throw reach",      Base = 600, Unit = "u", Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_flight",   Label = "Throw flight",     Base = 0.8, Unit = "s", Dec = 1 },
                new() { Group = "Deployed turrets", Id = "recall_pick",     Label = "Recall within",    Base = 60, Unit = "u", Dec = 0 },
                new() { Group = "Spotter", Id = "paint_time", Label = "A hit paints for", Base = 5, Unit = "s", Dec = 1 },
                new() { Group = "Bubble", Id = "bubble_pool", Label = "Damage it soaks", Base = 400, Dec = 0 },
                new() { Group = "Bubble", Id = "bubble_radius", Label = "Radius",        Base = 260, Unit = "u", Dec = 0 },
                new() { Group = "Bubble", Id = "bubble_time", Label = "Time up",         Base = 8, Unit = "s", Dec = 1 },
                new() { Group = "Bubble", Id = "bubble_cooldown", Label = "Cooldown",    Base = 25, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://freight_hauler_hull.png", Length = 230f, HalfWidth = 59.74f,
                Mains = new Vector2[] { new(0.0f, -50.6f) },
                Pds   = new Vector2[] { new(-32.9f, 64.4f), new(32.9f, 64.4f) },
                TurretTexScale = 2.20f / 5.5f, MainBarrel = 27.0f, PdBarrel = 12.1f },
            Abilities = new[] { Ab.Guns, Ab.Deploy, Ab.Bubble } },
        new() { Id = ShipClass.FreightTender, Name = "TENDER", Ready = true, Fit = Fit.Guns | Fit.Pd | Fit.Deploy,
            Blurb = "One main gun, two point-defence turrets, three deployable turrets, and an overdrive that lifts everything's rate of fire.",
            Hint = "TENDER  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 400,
                ["thrust"] = 63.5, ["reverse_thrust"] = 28.2, ["max_speed"] = 120, ["reverse_speed"] = 42.4,
                ["turn_radius"] = 150, ["turn_rate"] = 0.85, ["strafe_speed"] = 60, ["strafe_thrust"] = 120,
                ["main_count"] = 1, ["main_damage"] = 12, ["main_interval"] = 1.0, ["main_range"] = 800, ["shell_speed"] = 560,
                ["pd_count"] = 2,
            },
            Damage = new() { ["main_damage"] = 1, ["deploy_damage"] = 0.5 },
            Reach = new() { ["main_range"] = 1, ["deploy_range"] = 1, ["pd_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["deploy_interval"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Deployed, Dps.Pd },
            Kit = new[] {
                CargoGun,
                ItemDef.Own(GearSlot.Utility, "freight_overdrive", "Overdrive Coils", "the overdrive, and how long it holds", "overdrive_mult"),
            },
            Rows = new StatRow[] {
                new() { Group = "Deployed turrets", Id = "deploy_damage",   Label = "Damage per shot",  Base = 6, Dec = 1 },
                new() { Group = "Deployed turrets", Id = "deploy_interval", Label = "Reload",           Base = 0.5, Unit = "s", Dec = 2, Inverse = true },
                new() { Group = "Deployed turrets", Id = "deploy_range",    Label = "Range",            Base = 500, Unit = "u", Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_hull",     Label = "Turret hull",      Base = 120, Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_max",      Label = "Out at once",      Base = 3, Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_cooldown", Label = "Between drops",    Base = 6, Unit = "s", Dec = 1, Inverse = true },
                new() { Group = "Deployed turrets", Id = "deploy_reach",    Label = "Throw reach",      Base = 600, Unit = "u", Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_flight",   Label = "Throw flight",     Base = 0.8, Unit = "s", Dec = 1 },
                new() { Group = "Deployed turrets", Id = "recall_pick",     Label = "Recall within",    Base = 60, Unit = "u", Dec = 0 },
                new() { Group = "Overdrive", Id = "overdrive_mult", Label = "Rate of fire", Base = 2, Unit = "x", Dec = 1 },
                new() { Group = "Overdrive", Id = "overdrive_time", Label = "Time up",      Base = 8, Unit = "s", Dec = 1 },
                new() { Group = "Overdrive", Id = "overdrive_cooldown", Label = "Cooldown", Base = 24, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://freight_tender_hull.png", Length = 230f, HalfWidth = 57.48f,
                Mains = new Vector2[] { new(0.0f, -50.6f) },
                Pds   = new Vector2[] { new(-31.6f, 64.4f), new(31.6f, 64.4f) },
                TurretTexScale = 2.20f / 5.5f, MainBarrel = 27.0f, PdBarrel = 12.1f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Overdrive, Ab.Deploy } },
        new() { Id = ShipClass.FreightBastion, Name = "BASTION", Ready = true, Fit = Fit.Guns | Fit.Pd | Fit.Deploy,
            Blurb = "One main gun, two point-defence turrets, three deployable turrets, and a shockwave that throws what is near it clear, or holds a boss still.",
            Hint = "BASTION  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 400,
                ["thrust"] = 63.5, ["reverse_thrust"] = 28.2, ["max_speed"] = 120, ["reverse_speed"] = 42.4,
                ["turn_radius"] = 150, ["turn_rate"] = 0.85, ["strafe_speed"] = 60, ["strafe_thrust"] = 120,
                ["main_count"] = 1, ["main_damage"] = 12, ["main_interval"] = 1.0, ["main_range"] = 800, ["shell_speed"] = 560,
                ["pd_count"] = 2,
            },
            Damage = new() { ["main_damage"] = 1, ["deploy_damage"] = 0.5 },
            Reach = new() { ["main_range"] = 1, ["deploy_range"] = 1, ["wave_range"] = 1, ["pd_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["deploy_interval"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Deployed, Dps.Pd },
            Kit = new[] {
                CargoGun,
                ItemDef.Own(GearSlot.Utility, "freight_emitter", "Shockwave Emitter", "the shockwave's reach and its push", "wave_range"),
            },
            Rows = new StatRow[] {
                new() { Group = "Deployed turrets", Id = "deploy_damage",   Label = "Damage per shot",  Base = 6, Dec = 1 },
                new() { Group = "Deployed turrets", Id = "deploy_interval", Label = "Reload",           Base = 0.5, Unit = "s", Dec = 2, Inverse = true },
                new() { Group = "Deployed turrets", Id = "deploy_range",    Label = "Range",            Base = 500, Unit = "u", Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_hull",     Label = "Turret hull",      Base = 120, Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_max",      Label = "Out at once",      Base = 3, Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_cooldown", Label = "Between drops",    Base = 6, Unit = "s", Dec = 1, Inverse = true },
                new() { Group = "Deployed turrets", Id = "deploy_reach",    Label = "Throw reach",      Base = 600, Unit = "u", Dec = 0 },
                new() { Group = "Deployed turrets", Id = "deploy_flight",   Label = "Throw flight",     Base = 0.8, Unit = "s", Dec = 1 },
                new() { Group = "Deployed turrets", Id = "recall_pick",     Label = "Recall within",    Base = 60, Unit = "u", Dec = 0 },
                new() { Group = "Shockwave", Id = "wave_range",    Label = "Reach",          Base = 1000, Unit = "u", Dec = 0 },
                new() { Group = "Shockwave", Id = "wave_push",     Label = "Throws them",    Base = 1000, Unit = "u", Dec = 0 },
                new() { Group = "Shockwave", Id = "wave_disable",  Label = "Holds a boss",   Base = 3, Unit = "s", Dec = 1 },
                new() { Group = "Shockwave", Id = "wave_cooldown", Label = "Cooldown",       Base = 30, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                // frigate_c (the pack, J5): the main sits aft, on the ring turret the art draws at
                // the stern, with the PD pair either side of it
                Texture = "res://freight_bastion_hull.png", Length = 230f, HalfWidth = 49.34f,
                Mains = new Vector2[] { new(0.0f, 53.48f) },
                Pds   = new Vector2[] { new(-27.1f, 64.4f), new(27.1f, 64.4f) },
                TurretTexScale = 2.20f / 5.5f, MainBarrel = 27.0f, PdBarrel = 12.1f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Shockwave, Ab.Deploy } },

        // -- page 3: heavy fighters --------------------------------------------
        new() { Id = ShipClass.HeavySniper, Name = "SNIPER", Ready = true, Fit = Fit.Guns,
            Blurb = "Fast. A light main gun, and a railgun: locked while it charges, then a straight blue line through everything on it.",
            Hint = "SNIPER  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 140,
                ["thrust"] = 130, ["reverse_thrust"] = 60, ["max_speed"] = 190, ["reverse_speed"] = 70,
                ["turn_radius"] = 55, ["turn_rate"] = 2.2, ["strafe_speed"] = 95, ["strafe_thrust"] = 380,
                ["main_count"] = 1, ["main_damage"] = 6, ["main_interval"] = 0.8, ["main_range"] = 900, ["shell_speed"] = 700,
            },
                // 7.5 = 5% of the railgun's 150, what a level is worth on a battleship's shell
            Damage = new() { ["main_damage"] = 1, ["rail_damage"] = 7.5 },
            Reach = new() { ["main_range"] = 1, ["rail_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["rail_charge"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Railgun },
            Kit = new[] {
                HeavyCannon,
                ItemDef.Own(GearSlot.Utility, "heavy_railgun", "Railgun Mount", "the railgun's charge and its slug", "rail_damage"),
            },
            Rows = new StatRow[] {
                new() { Group = "Railgun", Id = "rail_damage", Label = "Damage",         Base = 150, Dec = 0 },
                new() { Group = "Railgun", Id = "rail_charge", Label = "Charge (locked)",Base = 3, Unit = "s", Dec = 1, Inverse = true },
                new() { Group = "Railgun", Id = "rail_range",  Label = "Reach",          Base = 2500, Unit = "u", Dec = 0 },
                new() { Group = "Railgun", Id = "rail_width",  Label = "Beam width",     Base = 14, Unit = "u", Dec = 0 },
                new() { Group = "Railgun", Id = "rail_cooldown", Label = "Cooldown",     Base = 1, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://heavy_sniper_hull.png", Length = 120f, HalfWidth = 30.03f,
                Mains = new Vector2[] { new(0.0f, -24.0f) },
                TurretTexScale = 1.18f / 5.5f, MainBarrel = 14.5f, PdBarrel = 6.5f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Railgun } },
        new() { Id = ShipClass.HeavyWarrior, Name = "WARRIOR", Ready = true, Fit = Fit.Guns,
            Blurb = "Fast. Two main guns, and a rush: a burst of speed at a fraction of the damage taken, ending in a stunning EMP.",
            Hint = "WARRIOR  ·  mouse aims the main guns",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 140,
                ["thrust"] = 130, ["reverse_thrust"] = 60, ["max_speed"] = 190, ["reverse_speed"] = 70,
                ["turn_radius"] = 55, ["turn_rate"] = 2.2, ["strafe_speed"] = 95, ["strafe_thrust"] = 380,
                ["main_count"] = 2, ["main_damage"] = 9, ["main_interval"] = 0.7, ["main_range"] = 600, ["shell_speed"] = 520,
            },
                // 3 = 5% of the EMP's 60
            Damage = new() { ["main_damage"] = 1, ["emp_damage"] = 3 },
            Reach = new() { ["main_range"] = 1, ["emp_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Emp },
            Kit = new[] {
                ItemDef.Own(GearSlot.Weapon, "warrior_main_guns", "Mk I Twin Cannons", "the two light turrets", "rush_mult"),
                ItemDef.Own(GearSlot.Utility, "heavy_rush_drive", "Rush Drive", "the rush, and the EMP it ends in", "rush_mult"),
            },
            Rows = new StatRow[] {
                new() { Group = "Rush", Id = "rush_mult",  Label = "Top speed",        Base = 2.5, Unit = "x", Dec = 1 },
                new() { Group = "Rush", Id = "rush_time",  Label = "Time up",          Base = 2.5, Unit = "s", Dec = 1 },
                new() { Group = "Rush", Id = "rush_guard", Label = "Damage taken",     Base = 0.5, Unit = "x", Dec = 2 },
                new() { Group = "Rush", Id = "emp_damage", Label = "EMP damage",       Base = 60, Dec = 0 },
                new() { Group = "Rush", Id = "emp_range",  Label = "EMP reach",        Base = 260, Unit = "u", Dec = 0 },
                new() { Group = "Rush", Id = "emp_stun",   Label = "EMP holds them",   Base = 2, Unit = "s", Dec = 1 },
                new() { Group = "Rush", Id = "rush_cooldown", Label = "Cooldown",      Base = 18, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://heavy_warrior_hull.png", Length = 120f, HalfWidth = 31.54f,
                Mains = new Vector2[] { new(-12.0f, -24.0f), new(12.0f, -24.0f) },
                TurretTexScale = 1.18f / 5.5f, MainBarrel = 14.5f, PdBarrel = 6.5f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Rush } },
        new() { Id = ShipClass.HeavyWarden, Name = "WARDEN", Ready = true, Fit = Fit.Guns | Fit.Pd,
            Blurb = "Fast. Point defence that hits ten times as hard as a warship's, a modest main gun, and hunter-seekers that each take a target of their own.",
            Hint = "WARDEN  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 140,
                ["thrust"] = 130, ["reverse_thrust"] = 60, ["max_speed"] = 190, ["reverse_speed"] = 70,
                ["turn_radius"] = 55, ["turn_rate"] = 2.2, ["strafe_speed"] = 95, ["strafe_thrust"] = 380,
                ["main_count"] = 1, ["main_damage"] = 12, ["main_interval"] = 0.6, ["main_range"] = 700, ["shell_speed"] = 600,
                // ITS ONE MOUNT IS A GUN, NOT A NUISANCE. "Half efficiency, always on" was first read
            // as half a warship's damage PER SHOT: 0.25 every 0.5 s is 0.5 DPS, which is 50 seconds
            // to kill one 25-hull light raider -- the class's whole reason for existing did
            // nothing a pilot could see. It is half by MOUNTS instead: one mount where a warship
            // carries two, firing the same 0.5 s cycle, at 5 a shot. 10 DPS, always, with nothing
            // pressed -- and with its 20 DPS gun and 19.3 from the hunters that is the 50 the
            // whole game is tuned to.
            ["pd_count"] = 1, ["pd_damage"] = 5.0, ["pd_range"] = 420,
            },
                // 2.25 = 5% of a hunter's 45 (pd_damage is left out on purpose: every class has that row,
                // so naming it here would hand every ship in the game a chip-powered point-defence buff)
            Damage = new() { ["main_damage"] = 1, ["hunter_damage"] = 2.25 },
            Reach = new() { ["main_range"] = 1, ["hunter_range"] = 1, ["pd_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1, ["pd_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Hunters, Dps.Pd },
            Kit = new[] {
                HeavyCannon,
                ItemDef.Own(GearSlot.Utility, "heavy_hunter_cells", "Hunter Cells", "the six hunter-seekers", "hunter_count"),
            },
            Rows = new StatRow[] {
                new() { Group = "Hunters", Id = "hunter_count",  Label = "Missiles",      Base = 6, Dec = 0 },
                new() { Group = "Hunters", Id = "hunter_damage", Label = "Damage (each)", Base = 45, Dec = 0 },
                new() { Group = "Hunters", Id = "hunter_speed",  Label = "Speed",         Base = 260, Unit = "u/s", Dec = 0 },
                new() { Group = "Hunters", Id = "hunter_turn",   Label = "Guidance",      Base = 2.5, Unit = "rad/s", Dec = 2 },
                new() { Group = "Hunters", Id = "hunter_range",  Label = "Reach",         Base = 1200, Unit = "u", Dec = 0 },
                new() { Group = "Hunters", Id = "hunter_cooldown", Label = "Cooldown",    Base = 14, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://heavy_warden_hull.png", Length = 120f, HalfWidth = 28.68f,
                Mains = new Vector2[] { new(0.0f, -24.0f) },
                Pds   = new Vector2[] { new(0.0f, 31.2f) },
                TurretTexScale = 1.18f / 5.5f, MainBarrel = 14.5f, PdBarrel = 6.5f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Hunters } },

        // -- page 4: lights -----------------------------------------------------
        new() { Id = ShipClass.LightDart, Name = "DART", Ready = true, Fit = Fit.Guns,
            Blurb = "Fastest thing with a pilot in it. A barrel roll nothing can hit, and a burst of speed and rate of fire out of it.",
            Hint = "DART  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 90,
                ["thrust"] = 190, ["reverse_thrust"] = 90, ["max_speed"] = 260, ["reverse_speed"] = 95,
                ["turn_radius"] = 35, ["turn_rate"] = 3.0, ["strafe_speed"] = 130, ["strafe_thrust"] = 520,
                ["main_count"] = 1, ["main_damage"] = 5, ["main_interval"] = 0.35, ["main_range"] = 500, ["shell_speed"] = 620,
            },
            Damage = new() { ["main_damage"] = 1 },
            Reach = new() { ["main_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1 },
            Weapons = new[] { Dps.Main },
            Kit = new[] {
                LightCannon,
                ItemDef.Own(GearSlot.Utility, "light_roll_thrusters", "Roll Thrusters", "the barrel roll, and the boost after it", "roll_time"),
            },
            Rows = new StatRow[] {
                new() { Group = "Barrel roll", Id = "roll_time",  Label = "Untouchable",   Base = 1.2, Unit = "s", Dec = 1 },
                new() { Group = "Barrel roll", Id = "boost_time", Label = "Boost after",   Base = 4, Unit = "s", Dec = 1 },
                new() { Group = "Barrel roll", Id = "boost_speed",Label = "Top speed",     Base = 1.6, Unit = "x", Dec = 2 },
                new() { Group = "Barrel roll", Id = "boost_rof",  Label = "Rate of fire",  Base = 1.25, Unit = "x", Dec = 2 },
                new() { Group = "Barrel roll", Id = "roll_cooldown", Label = "Cooldown",   Base = 12, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://light_dart_hull.png", Length = 70f, HalfWidth = 16.78f,
                Mains = new Vector2[] { new(0.0f, -10.5f) },
                TurretTexScale = 0.65f / 5.5f, MainBarrel = 8.0f, PdBarrel = 3.6f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Roll } },
        new() { Id = ShipClass.LightEcho, Name = "ECHO", Ready = true, Fit = Fit.Guns,
            Blurb = "Its echo remembers the damage it deals, then detonates the lot where the last shot landed.",
            Hint = "ECHO  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 90,
                ["thrust"] = 190, ["reverse_thrust"] = 90, ["max_speed"] = 260, ["reverse_speed"] = 95,
                ["turn_radius"] = 35, ["turn_rate"] = 3.0, ["strafe_speed"] = 130, ["strafe_thrust"] = 520,
                ["main_count"] = 1, ["main_damage"] = 5, ["main_interval"] = 0.35, ["main_range"] = 500, ["shell_speed"] = 620,
            },
                // echo_share is NOT a weapon: the blast is a share of damage already dealt, so it
                // grows with main_damage on its own, and naming it would count the same purchase twice
            Damage = new() { ["main_damage"] = 1 },
            Reach = new() { ["main_range"] = 1, ["echo_radius"] = 1 },
            Cycle = new() { ["main_interval"] = 1 },
            Weapons = new[] { Dps.Main, Dps.Echo },
            Kit = new[] {
                LightCannon,
                ItemDef.Own(GearSlot.Utility, "light_echo_core", "Echo Core", "what the echo remembers, and its blast", "echo_time"),
            },
            Rows = new StatRow[] {
                new() { Group = "Echo", Id = "echo_time",   Label = "It remembers for", Base = 5, Unit = "s", Dec = 1 },
                new() { Group = "Echo", Id = "echo_radius", Label = "Blast radius",     Base = 220, Unit = "u", Dec = 0 },
                new() { Group = "Echo", Id = "echo_share",  Label = "Of what it dealt", Base = 1, Unit = "x", Dec = 2 },
                new() { Group = "Echo", Id = "echo_cooldown", Label = "Cooldown",       Base = 15, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                // fighter_f (the pack, J5): one main on the centreline stands for the paired
                // barrels the art draws on both wings (the flavour, "fires twice")
                Texture = "res://light_echo_hull.png", Length = 70f, HalfWidth = 14.68f,
                Mains = new Vector2[] { new(0.0f, -23.39f) },
                TurretTexScale = 0.65f / 5.5f, MainBarrel = 8.0f, PdBarrel = 3.6f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Echo } },
        new() { Id = ShipClass.LightWraith, Name = "WRAITH", Ready = true, Fit = Fit.Guns,
            Blurb = "While its veil is up nothing hostile can pick it: whatever was coming for it goes elsewhere, or gives up.",
            Hint = "WRAITH  ·  mouse aims the main gun",
            Drive = Drives.Boost,
            Nums = new() {
                ["hull"] = 90,
                ["thrust"] = 190, ["reverse_thrust"] = 90, ["max_speed"] = 260, ["reverse_speed"] = 95,
                ["turn_radius"] = 35, ["turn_rate"] = 3.0, ["strafe_speed"] = 130, ["strafe_thrust"] = 520,
                ["main_count"] = 1, ["main_damage"] = 5, ["main_interval"] = 0.35, ["main_range"] = 500, ["shell_speed"] = 620,
            },
            Damage = new() { ["main_damage"] = 1 },
            Reach = new() { ["main_range"] = 1 },
            Cycle = new() { ["main_interval"] = 1 },
            Weapons = new[] { Dps.Main },
            Kit = new[] {
                LightCannon,
                ItemDef.Own(GearSlot.Utility, "light_stealth_veil", "Stealth Veil", "how long nothing can pick it", "stealth_time"),
            },
            Rows = new StatRow[] {
                new() { Group = "Stealth", Id = "stealth_time", Label = "Unseen for", Base = 5, Unit = "s", Dec = 1 },
                // WHAT IT DOES WHILE IT IS UNSEEN, x1.00 each: the veil changes nothing about the
                // ship until a part moves one of them, so a wraith with no veil gear is exactly
                // the ship it was. They are read the way the dart's boost is read -- the ability
                // row names them (Ab.Stealth's SpeedStat and RateStat) and PlayerShip.SpeedMult
                // and FireRate add up every running row that names one. The veil had two
                // numbers, which is not a family: two of its four lines had nothing to trade.
                new() { Group = "Stealth", Id = "stealth_speed", Label = "Top speed, unseen",    Base = 1, Unit = "x", Dec = 2 },
                new() { Group = "Stealth", Id = "stealth_rof",   Label = "Rate of fire, unseen", Base = 1, Unit = "x", Dec = 2 },
                new() { Group = "Stealth", Id = "stealth_cooldown", Label = "Cooldown", Base = 20, Unit = "s", Dec = 1, Inverse = true },
            },
            Art = new ClassArt {
                Texture = "res://light_wraith_hull.png", Length = 70f, HalfWidth = 14.97f,
                Mains = new Vector2[] { new(0.0f, -10.5f) },
                TurretTexScale = 0.65f / 5.5f, MainBarrel = 8.0f, PdBarrel = 3.6f },
            Abilities = new[] { Ab.Guns, Ab.FireMode, Ab.Stealth } },
    };

    private static readonly Dictionary<ShipClass, ClassDef> ById = All.ToDictionary(c => c.Id);

    // A number this build has no class for -- a save or a packet from another version. It reads
    // as a hull with nothing on it rather than throwing: no fit, no abilities, no art.
    private static readonly ClassDef Missing = new() { Name = "UNKNOWN", Blurb = "" };
    public static bool Known(ShipClass c) => ById.ContainsKey(c);
    public static ClassDef Of(ShipClass c) => ById.TryGetValue(c, out var d) ? d : Missing;
    public static ClassArt Art(ShipClass c) => Of(c).Art;
    public static bool Has(ShipClass c, Fit f) => Of(c).Has(f);
    // stat id -> what one Weapons level adds. Asked of the class, never "is it the carrier".
    public static IReadOnlyDictionary<string, double> Damage(ShipClass c) => Of(c).Damage;
    // every weapon stat any class has: what a part that lifts "every weapon" reaches
    public static IReadOnlyDictionary<string, double> ReachOf(ShipClass c) => Of(c).Reach;
    public static IReadOnlyDictionary<string, double> CycleOf(ShipClass c) => Of(c).Cycle;
    public static IEnumerable<string> EveryDamageStat()
    {
        var seen = new SortedSet<string>();
        foreach (var c in All) foreach (var k in c.Damage.Keys) seen.Add(k);
        return seen;
    }
    // Its name as the screens print it ("BATTLESHIP"), from the one table that holds it.
    public static string NameOf(ShipClass c) => Of(c).Name;
    // A class number from a file or a packet: one this build can actually fly, or the Battleship.
    public static ShipClass Sanitize(int cls) =>
        Enum.IsDefined(typeof(ShipClass), cls) && Of((ShipClass)cls).Ready ? (ShipClass)cls : ShipClass.Battleship;

    public const int PerPage = 3;
    public static int Pages => (All.Length + PerPage - 1) / PerPage;
}
