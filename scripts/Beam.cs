using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// A BEAM — a line of light from whatever makes it to whatever it lands on. Two kinds, one file.
//
// A SHAFT (Beam.Draw) stays lit while a tool works: a miner's cutting beam, the Drake Bastion's
// tractor. A steady core inside a soft glow that breathes -- one drawing for every such beam, so
// they cannot drift apart in how a beam looks. `t` is the caller's clock (it drives the
// breathing); the colours are the glow, the body, the core and the bright tip where it lands.
//
// A FLASH (Beam.All) is FIRED: a line from the muzzle to what it hit, gone in Hub.FlashLife. One
// row per kind -- the colour its line is drawn in and the report it is heard by, a file of sfx/
// played at a PITCH of the row's own. What it replaced: a three-value enum (ShotSound) that every
// flash in the game went through, so a raider's laser, every point defence in the game and the
// base's own gun were one buzz at one note, and each one's colour was a literal at its call site.
// One file is now several beams, each its own note (the owner: "different beams have different
// frequency sounds"), and each is rate-limited under its own id rather than its file's (Sfx).
//
// Combat.Flash names the row; the world draws and plays it (Hub.AddFlash); a guest is sent the
// row's index and nothing else (Hub.NetFlash). THE INDEX IS THE ID ON THE WIRE, so APPEND ONLY.
// Row 0 is point defence, because a TurretSpec that says nothing about its beam is a warship's --
// which also means anything ELSE that fires a flash must name its row, or it buzzes like a friend.
//
// A NEW BEAM MUST FILL IN: an appended const and row -- its id (what Sfx gates and counts it
// under, so never a sound file's name), its tint, its report (a file Sfx knows) and a pitch at
// least a semitone from every other row on that file -- and the field on whatever fires it
// (EnemyDef.Beam, WingDef.Beam, TurretSpec.Beam, BossMove.Beam), or the const where only one
// thing fires it (BaseDefense, PlayerShip.FireRail).
// A NEW BEAM MUST NOT: pass a Color or name a sound at a call site, or add an arm to any switch.
//
// NOT ROWS HERE: the Lancer's death beam and the Drake's tractor. Each is a MOVE that burns or
// holds for seconds, and its sounds are the move's own files (BossMove.Cue / Strike) -- distinct
// files, not pitches of a shared one. Every file a beam of either kind is heard by is stored at
// 0.75 of the level it was made at (tools/gain.ps1; BEAM in tools/make_sounds.py): the owner's
// "beams 25% quieter", in the file, as every level in this game is.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class BeamDef
{
    public string Id;            // what Sfx gates and counts it under; what a check names it by
    public Color Tint;           // its flash's line (FlashLayer)
    public string Report;        // sfx/<Report>.wav, where it leaves the muzzle
    public float Pitch = 1f;     // ...played at this pitch: its own note
}

public static class Beam
{
    // The index IS the id on the wire (Hub.NetFlash), so APPEND ONLY.
    public const int Point = 0, Base = 1, Deployed = 2, Hauler = 3, LightRaider = 4, HeavyRaider = 5,
                     Fighter = 6, Bolt = 7, Rail = 8, RailEnhanced = 9, Tot = 10;

    // YOUR SIDE CLIMBS AND THEIRS FALLS, so a fight reads by ear. On laser_light, point defence
    // keeps the note every flash used to share -- it is the one heard most -- your other guns climb
    // above it a whole tone at a time and the raiders fall below it a minor third at a time. On
    // laser_boss the rail sits four semitones under a boss's bolt. The tints are the colours these
    // beams have always been drawn in. (Pd is declared first: a static initialiser reads it.)
    private static readonly Color Pd = new(0.7f, 0.95f, 1f);
    public static readonly BeamDef[] All =
    {
        new() { Id = "point_defence", Tint = Pd,                    Report = "laser_light", Pitch = 1.00f },
        new() { Id = "base",          Tint = new(0.6f, 0.9f, 1f),   Report = "laser_light", Pitch = 1.12f },
        new() { Id = "deployed",      Tint = Pd,                    Report = "laser_light", Pitch = 1.26f },
        new() { Id = "hauler",        Tint = Pd,                    Report = "laser_light", Pitch = 1.41f },
        new() { Id = "light_raider",  Tint = new(1f, 0.3f, 0.25f),  Report = "laser_light", Pitch = 0.84f },
        new() { Id = "heavy_raider",  Tint = new(1f, 0.35f, 0.25f), Report = "laser_light", Pitch = 0.71f },
        // a carrier's fighters: a file of their own, 35% under a capital ship's
        new() { Id = "fighter",       Tint = Pd,                    Report = "laser_fighter", Pitch = 1.00f },
        // a boss's bolt, and a sniper's railgun -- the deep file, the rail a deeper note; the rail's
        // tint is its bar's (Fx "rail"), because its flash is drawn inside that bar
        new() { Id = "bolt",          Tint = new(1f, 0.5f, 0.35f),  Report = "laser_boss", Pitch = 0.90f },
        new() { Id = "rail",          Tint = new(0.45f, 0.70f, 1f), Report = "laser_boss", Pitch = 0.71f },
        // an enhanced rail round (ActiveReload): white, two semitones under the rail
        new() { Id = "rail_enhanced", Tint = new(0.92f, 0.96f, 1f), Report = "laser_boss", Pitch = 0.63f },
        // the freighter's Time on target: its bar's tint (Fx "tot"), the rail's file a tone above it
        new() { Id = "tot",           Tint = new(1f, 0.78f, 0.35f), Report = "laser_boss", Pitch = 0.80f },
    };

    public static BeamDef Of(int id) => All[id >= 0 && id < All.Length ? id : Point];

    public static void Draw(CanvasItem c, Vector2 a, Vector2 b, double t, Color glow, Color body, Color core, Color tip, float width = 1f)
    {
        float breathe = 1f + 0.15f * Mathf.Sin((float)t * 11f);
        c.DrawLine(a, b, glow, 9f * width * breathe);
        c.DrawLine(a, b, body, 4.5f * width * breathe);
        c.DrawLine(a, b, core, 1.6f * width);
        c.DrawCircle(b, 5f * width * breathe, tip);
    }
}
