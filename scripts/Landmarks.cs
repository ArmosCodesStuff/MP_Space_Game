using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// LANDMARKS — the places of the home world, one row each, and THE ONE GATE on what may be done
// only while a pilot is over one of them.
//
// A ROW says where a place is (At), how big its footprint is (Half: a click inside it opens the
// place, and a ship whose centre is inside it is OVER it), what it is built from (Art, as a node
// called NodeName, with Label drawn under it), how the scope marks it (Pick, Shape, Glyph, Colour
// -> Hub.ScopeMarks), what a left-click on it opens (Opens, or null), the Services it offers (or
// None), and the docks on its hull (Docks, or null: Docks.cs). A place Hub builds itself -- the
// trade portal is a node with state of its own, the belt is a sun and nine rocks, the salvage
// field is the wreck its salvagers work -- has no Art and no footprint here: its row is where it
// is and how the scope marks it.
//
// REPLACED: the same few facts written out by hand in four places in Hub. The base, the TIO, the
// recycler and the four outposts were each a position in the layout block, a sprite built by hand
// in BuildWorld (the recycler had none), a row typed into ScopeMarks and, for the three that opened
// something, a test in SelectAt -- a sprite's rectangle, a 200 u circle and a 110 u circle; the
// portal, the field and the belt were a third list of scope rows beside them. The recycler is not
// a row of its own any more. It is the EQUIPMENT BASE's, as the owner asked:
// "the equipment thing should become its own base, which should also be where the recycler is".
//
// A NEW PLACE AT HOME IS A ROW HERE and nothing in Hub: Id, Name, At, its mark (Pick, Shape,
// Glyph, Colour), and whichever of Half, NodeName + Art, Label, Opens, Services and Docks it has.
// A mark that comes and goes (the mission portal, a dropped turret, an emplacement) is not a
// landmark: it stays a row of Hub.ScopeMarks.
//
// A SERVICE is something a pilot may do only while OVER a place that offers it and CALM:
// CalmNeeded seconds since its ship last dealt or took damage (the owner's "10 second combat
// lock"). Serves(ship, service) is the ONLY place that rule is written. Every button that spends
// through a service asks it, to grey itself. Every action asks it again before it spends
// (Yard.BuyGearLevel, Yard.QueueScrap), so a press that raced its pilot off the pad is refused in
// the words the button was greyed with. A new service is three things: a Service member, a
// place's Services naming it, and one Serves call at the top of its action -- never a second
// test of position.
//
// NOT ON THE WIRE, on purpose. A part's level, the hold and the scrap queue belong to the pilot
// (Character; the salvage is Yard.OwnStock), and they are spent on the pilot's own machine, so the
// gate runs there too. It reads two things. One is its own ship's position, which that machine
// flies. The other is the combat clock, which only the host keeps and sends back
// (PlayerShip.CalmFor, from the host-state packet). A guest is refused on the host's word, one
// packet late at most.
// ─────────────────────────────────────────────────────────────────────────────
[Flags]
public enum Service { None = 0, LevelGear = 1, Scrap = 2 }

public sealed class Landmark
{
    public string Id;                      // what code and checks ask for it by
    public string Name;                    // on the scope, and as a waypoint
    public Vector2 At;                     // its centre in the home world
    public Vector2 Half;                   // its footprint's half-extents: what a click hits, and what a ship is OVER
    public float Pick;                     // how far short a warp to it stops (and its pick radius on the scope)
    public Hub.MarkShape Shape;            // its glyph on the scope...
    public Vector2 Glyph;                  // ...that glyph's half-size...
    public Color Colour;                   // ...and its colour
    public string NodeName;                // the node its art is built as (the harness finds "Base", "TIO", "OutpostSE" by it)
    public Func<Landmark, Node2D> Art;     // what it is built from (null: Hub builds it itself)
    public string Label;                   // its name in the world, drawn under it (null: none)
    public Action<Hub> Opens;              // what a left-click on it opens (null: nothing)
    public Service Services;               // what may be done only while over it (Landmarks.Serves)
    public DockDef[] Docks;                // the pads on its hull a craft docks at, measured off its Art (null: none)

    public bool Covers(Vector2 p) => Mathf.Abs(p.X - At.X) <= Half.X && Mathf.Abs(p.Y - At.Y) <= Half.Y;
    public bool Offers(Service s) => s != Service.None && (Services & s) == s;
    public Hub.ScopeMark Mark => new(Name, At, Pick, Shape, Glyph, Colour);
}

public static class Landmarks
{
    // THE COMBAT LOCK: seconds since the ship last dealt or took damage before a service will work.
    // It is read off the one combat clock, which can count at most PlayerShip.CombatHold (12 s).
    public const double CalmNeeded = 10;

    // THE FOUR OUTPOSTS, one place each, in the order of Hub.Outposts -- which stays their table,
    // because the lanes, the hauler and the escort route index it; a lane reaches its outpost's row
    // by that same index. Written before All, which is made of them: static fields start in the
    // order they are written.
    public static readonly Landmark[] Outposts = Hub.Outposts.Select(o => new Landmark
    {
        Id = "outpost_" + o.name.ToLowerInvariant(), Name = "OUTPOST " + o.name, At = o.at,
        Half = new Vector2(Hub.OutpostHeight / 2f, Hub.OutpostHeight / 2f), Pick = Hub.OutpostHeight * 0.5f,
        Shape = Hub.MarkShape.Diamond, Glyph = new Vector2(3.5f, 4f), Colour = new Color(0.55f, 0.72f, 0.85f),
        NodeName = "Outpost" + o.name, Art = _ => Sprites.Fit("res://outpost.png", Hub.OutpostHeight), Label = "OUTPOST " + o.name,
        Docks = Docks.OutpostClamps,
    }).ToArray();

    public static readonly Landmark[] All = new[]
    {
        // THE BASE. Its footprint is the square inside the station's 262 u disc.
        new Landmark
        {
            Id = "base", Name = "BASE", At = Hub.BasePos, Half = new Vector2(185f, 185f), Pick = 260f,
            Shape = Hub.MarkShape.Box, Glyph = new Vector2(4f, 4f), Colour = new Color(0.75f, 0.78f, 0.8f),
            NodeName = "Base", Art = _ => new Sprite2D { Texture = Assets.Load<Texture2D>("res://base_station.png"), Scale = Vector2.One * Hub.BaseScale },
            Opens = h => h.OpenSide(() => new BasePanel { Hub = h }),
            Docks = Docks.BaseArms,
        },
        // THREAT INTELLIGENCE OPERATIONS. Its footprint is its art's own rectangle.
        new Landmark
        {
            Id = "tio", Name = "THREAT INTELLIGENCE", At = Hub.TioPos, Half = new Vector2(Hub.TioHalfWidth, Hub.TioHeight / 2f), Pick = 140f,
            Shape = Hub.MarkShape.Box, Glyph = new Vector2(3f, 4f), Colour = new Color(0.6f, 0.64f, 0.7f),
            NodeName = "TIO", Art = _ => Sprites.Fit("res://tio_building.png", Hub.TioHeight), Label = "THREAT INTELLIGENCE OPERATIONS",
            Opens = h => h.OpenTio(),
        },
        // THE EQUIPMENT BASE: a 320 x 240 u pad, drawn (ServicePad), with caution tape across its
        // centre. A click opens EQUIPMENT, whose title line leads on to the RECYCLER. It is wide
        // enough to set any hull's centre on; a battleship (378 u) overhangs it, as a ship overhangs
        // a landing pad.
        new Landmark
        {
            Id = "equipment", Name = "EQUIPMENT BASE", At = Hub.EquipmentPos, Half = new Vector2(160f, 120f), Pick = 120f,
            Shape = Hub.MarkShape.Box, Glyph = new Vector2(4f, 3f), Colour = ServicePad.Tape,
            NodeName = "EquipmentBase", Art = l => new ServicePad { Row = l }, Label = "EQUIPMENT BASE",
            Opens = h => h.OpenSide(() => new EquipmentWindow { Hub = h }),
            Services = Service.LevelGear | Service.Scrap,
        },
        // THE TRADE PORTAL, THE SALVAGE FIELD AND THE MINING BELT: scenery. Nothing is clicked or
        // served there, and Hub builds each of them itself.
        new Landmark
        {
            Id = "portal", Name = "PORTAL", At = Hub.PortalPos, Pick = 160f,
            Shape = Hub.MarkShape.Ring, Glyph = new Vector2(5f, 5f), Colour = new Color(0.4f, 0.8f, 1f),
        },
        new Landmark
        {
            Id = "salvage_field", Name = "SALVAGE FIELD", At = Hub.WreckPos, Pick = 340f,
            Shape = Hub.MarkShape.Dot, Glyph = new Vector2(4f, 4f), Colour = new Color(0.5f, 0.35f, 0.25f, 0.9f),
        },
        // the belt's sun: the rocks ring it, and nothing is drawn at the point the mark picks
        new Landmark
        {
            Id = "belt", Name = "MINING BELT", At = Hub.SunPos, Pick = 540f,
            Shape = Hub.MarkShape.Ring, Glyph = new Vector2(4f, 4f), Colour = new Color(1f, 0.82f, 0.42f),
        },
    }.Concat(Outposts).ToArray();

    public static Landmark ById(string id) => All.FirstOrDefault(l => l.Id == id);

    // WHAT THE GATE SAYS: may this ship be served this, and if not, why. It is worded so that a
    // window can print it under its buttons as it stands.
    public readonly record struct Verdict(bool Ok, string Why);

    public static Verdict Serves(PlayerShip ship, Service s)
    {
        if (ship is not { Alive: true }) return new(false, "your ship is in stasis.");
        if (Hub.InArena || !All.Any(l => l.Offers(s) && l.Covers(ship.Position)))
            return new(false, $"fly onto the {string.Join(" or ", All.Where(l => l.Offers(s)).Select(l => l.Name))}.");
        double wait = CalmNeeded - ship.CalmFor;
        return wait > 0 ? new(false, $"in combat, {Math.Ceiling(wait):0} s more.") : new(true, "");
    }
}

// A PAD THAT SERVES: the equipment base's art, and the art of any row that asks for it.
//   - a deck the size of the row's footprint, plated
//   - a band of yellow-and-black caution tape across its centre (the owner's words)
//   - a beacon at each corner
//   - a rim that tells the local pilot what the gate will say: green when it will serve them,
//     amber when they are on it but still in combat, steel when they are elsewhere
// It is drawn, not an image: the tape is a band plus a row of slanted stripes, clipped to the band
// once (Geometry2D.IntersectPolygons), so it fits any Half.
public partial class ServicePad : Node2D
{
    public Landmark Row;
    public static readonly Color Tape = new(0.98f, 0.80f, 0.12f);          // caution yellow (its scope mark too)
    private static readonly Color Ink = new(0.08f, 0.08f, 0.09f), Deck = new(0.16f, 0.18f, 0.21f),
                                  Seam = new(1f, 1f, 1f, 0.06f), Steel = new(0.55f, 0.58f, 0.62f);
    private const float Band = 36f, Stripe = 20f, Plate = 40f;              // the tape's width, one stripe's, one deck plate's
    private Vector2[][] _stripes = Array.Empty<Vector2[]>();
    private Vector2[] _corners = Array.Empty<Vector2>();
    private double _t;

    public override void _Ready()
    {
        var h = Row.Half;
        var band = new[] { new Vector2(-h.X, -Band / 2), new Vector2(h.X, -Band / 2), new Vector2(h.X, Band / 2), new Vector2(-h.X, Band / 2) };
        var cut = new List<Vector2[]>();
        for (float x = -h.X - Band; x < h.X + Band; x += Stripe * 2)
        {   // a stripe leaning 45 degrees, clipped to the band so that none hangs off the deck's edge
            var lean = new[] { new Vector2(x, -Band / 2), new Vector2(x + Stripe, -Band / 2), new Vector2(x + Stripe - Band, Band / 2), new Vector2(x - Band, Band / 2) };
            foreach (var piece in Geometry2D.IntersectPolygons(lean, band)) cut.Add(piece);
        }
        _stripes = cut.ToArray();
        _corners = new[] { new Vector2(-h.X, -h.Y), new Vector2(h.X, -h.Y), new Vector2(h.X, h.Y), new Vector2(-h.X, h.Y) };
    }

    public override void _Process(double delta) { _t += delta; QueueRedraw(); }

    public override void _Draw()
    {
        var h = Row.Half; var deck = new Rect2(-h, h * 2f);
        DrawRect(deck, Deck);
        for (float y = -h.Y + Plate; y < h.Y; y += Plate) DrawLine(new Vector2(-h.X, y), new Vector2(h.X, y), Seam, 1f);
        for (float x = -h.X + Plate; x < h.X; x += Plate) DrawLine(new Vector2(x, -h.Y), new Vector2(x, h.Y), Seam, 1f);
        DrawRect(new Rect2(-h.X, -Band / 2, h.X * 2f, Band), Tape);
        foreach (var s in _stripes) DrawColoredPolygon(s, Ink);
        var me = Hub.I?.MyShip;
        var rim = me != null && Row.Covers(me.Position) ? (Landmarks.Serves(me, Row.Services).Ok ? Ui.Good : Ui.Warn) : Steel;
        DrawRect(deck, rim, false, 4f);
        float pulse = 0.55f + 0.45f * Mathf.Sin((float)_t * 2.6f);
        foreach (var c in _corners) DrawCircle(c, 7f, new Color(Tape.R, Tape.G, Tape.B, pulse));
    }
}
