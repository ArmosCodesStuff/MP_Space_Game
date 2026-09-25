using Godot;
using System;

// ART, SIZED TO THE WORLD. Every hull is its art scaled so the drawing measures `length` world
// units nose to tail -- the one rule the fleet, the raiders, the boss, the pod, the wing, the TIO
// and the title screen's foes all follow, and each of them used to write out for itself.
public static class Sprites
{
    public static Sprite2D Fit(string path, float length)
    {
        var tex = Assets.Load<Texture2D>(path);
        return new Sprite2D { Texture = tex, Scale = Vector2.One * (length / tex.GetHeight()) };
    }

    // a hull row's art at its length, in its tint
    public static Sprite2D Fit(HullArt art)
    {
        var s = Fit(art.Texture, art.Length);
        s.Modulate = art.Tint;
        return s;
    }
}

// A BELL: the centre of its aft rim and its width, in world units from the hull's centre (x to
// starboard, y aft), as tools/make_ships.ps1 prints them off the art. Public FIELDS, not a positional
// record's properties: Net.Fingerprint writes a struct row out by its public fields, and a row with
// none is not a row to it, so a bare Nozzle[] table went unhashed.
public readonly struct Nozzle
{
    public readonly float X, Y, Bell;
    public Nozzle(float x, float y, float bell) { X = x; Y = y; Bell = bell; }
    // the bells of a hull drawn k times the length the tool measured them at
    public static Nozzle[] Scaled(float k, params Nozzle[] bells) =>
        Array.ConvertAll(bells, n => new Nozzle(n.X * k, n.Y * k, n.Bell * k));
}

// WHAT A HULL LOOKS LIKE: the one shape every table that draws a hull takes its art in. It replaced
// each table's own Texture, Length and Tint fields, and the single plume every hull drew from the
// middle of its stern whatever its engines were. A row that derives from it fills in:
//   Texture  a file tools/make_ships.ps1 wrote (nose up, the keel on the centre column)
//   Length   nose to tail, world units -- the art is scaled to it
//   Tint     the grey art's colour (white leaves it grey)
//   Nozzles  every bell the art has, as the tool prints them; one flame each
// How it is HIT is not here: that is each table's own rule (a raider's HitShare, a class's
// HalfWidth), and a new drawing never moves it.
public class HullArt
{
    public string Texture;
    public float Length;
    public Color Tint = Colors.White;
    public Nozzle[] Nozzles = Array.Empty<Nozzle>();

    // A flame out of every bell. `at` is the hull's centre and `k` the drawn size of one world
    // unit, in the caller's frame; `reach` stretches the flame (a boost), `throttle` and `active`
    // are Plume's.
    public void DrawPlumes(CanvasItem ci, Vector2 at, float k, Color col, float throttle, bool active, float reach = 1f)
    {
        foreach (var n in Nozzles)
            Plume.Draw(ci, at + new Vector2(n.X, n.Y) * k, Vector2.Down, n.Bell * k * reach / Plume.Width, col, throttle, active);
    }
}
