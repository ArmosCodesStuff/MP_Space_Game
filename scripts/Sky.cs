using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// THE SKY — what is behind the world, and how fast it goes past.
//
// REPLACED: eight lines in Hub._Ready that made ONE TextureRect, anchored FullRect on a CanvasLayer
// of its own. Anchored to the screen is anchored to the screen: it could not move, at any speed,
// for any reason -- so a ship at 190 u/s crossed a field of stars that never shifted a pixel, and
// the only thing on screen saying the ship was moving was the speed readout. A hull that feels
// nailed down is a hull nobody enjoys flying.
//
// WHAT A LAYER IS: a row, drawn by one SkyPlane (below) -- a tiled texture on the sky's CanvasLayer,
// offset every frame by the row's share of the camera's travel. This file decides WHICH layers
// exist and what each one is made of; SkyPlane is the only code that moves them.
//
// A ROW OF Sky.All MUST FILL IN:
//   Id        what it is called, for a check to name and for a reader to find
//   Texture   what it draws. Several rows may share one; Scale and Tint are what make them differ
//   Drift     HOW MUCH OF THE WORLD'S MOTION A PLAYER SEES IT MAKE. 0 is nailed to the screen
//             (the old behaviour, and the bug); 1 slides past exactly like the world does at
//             zoom 1. Far things drift LESS: that is the whole illusion, and why the rows run far
//             to near. It goes the SAME WAY as the world, always -- a sky that slides with the
//             ship instead of past it reads as the stars swinging round the hull.
//   Scale     how big the tile is drawn. A far layer wants a SMALLER tile: more, finer specks
//   Tint      its colour, alpha included -- a far layer is dimmer as well as slower
//   Spin      a few degrees of rotation, so two rows sharing one texture do not line up and read
//             as one grid sliding over itself
//   Z         the order within the sky. All of it is behind the world (Hub puts it on Layer -100)
//
// ADDING A LAYER IS A ROW. A nebula, a dust band, a debris field: a row here and nothing else, and
// nothing in Hub changes for it.
//
// IT IS PURELY LOCAL. Nothing here is networked, nothing here is simulated, nothing here can be
// hit, and no check on the wire knows it exists -- two peers may see different skies without
// disagreeing about anything that matters.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SkyLayer
{
    public string Id;
    public string Texture;
    public float Drift;
    public float Scale;
    public Color Tint;
    public float Spin;
    public int Z;
}

public static class Sky
{
    // The one texture the layers are made from today. Rows may name their own; sharing this one
    // and differing by Scale, Tint and Spin is what keeps a three-layer sky at zero new art.
    public const string Stars = "res://stars.png";

    // How far apart in speed the layers are. The near layer drifts at 0.45 -- less than half the
    // world -- so the sky NEVER looks like it is keeping up with the ship; it looks like distance.
    // Past about 0.6 the illusion inverts and the stars start reading as debris flying by.
    public static readonly SkyLayer[] All =
    {
        // FAR: fine, dim, and nearly still. Small scale = a denser field of smaller specks.
        new() { Id = "far",  Texture = Stars, Drift = 0.08f, Scale = 0.55f, Spin = 0f,
                Tint = new Color(0.62f, 0.68f, 0.85f, 0.45f), Z = -3 },
        // MID: the field the old static layer used to be, now moving.
        new() { Id = "mid",  Texture = Stars, Drift = 0.22f, Scale = 1.00f, Spin = 17f,
                Tint = new Color(0.85f, 0.88f, 1.00f, 0.70f), Z = -2 },
        // NEAR: few, large and bright, and quick enough to read as motion at a glance. Sparse on
        // purpose -- a dense near layer is visual noise over the thing the player is aiming at.
        new() { Id = "near", Texture = Stars, Drift = 0.45f, Scale = 2.10f, Spin = 41f,
                Tint = new Color(1.00f, 1.00f, 1.00f, 0.38f), Z = -1 },
    };

    // Build the whole sky under `parent` (a CanvasLayer the caller owns). One node per row, made
    // from the row and nothing else.
    public static void Build(Node parent)
    {
        foreach (var row in All)
        {
            var tex = Assets.Load<Texture2D>(row.Texture);
            if (tex == null) continue;                 // a row naming art that is not there is skipped, not fatal
            parent.AddChild(new SkyPlane { Row = row, Art = tex, Name = "Sky_" + row.Id });
        }
    }
}

// ONE LAYER, DRAWN WHERE THIS FILE SAYS.
//
// IT LIVES IN SCREEN SPACE. The sky's CanvasLayer (Hub, Layer -100) does not follow the camera, so
// a plane's Position is a place ON THE SCREEN, and the only motion it has is the one given here:
// -Drift x the camera's position. The world slides across the screen by -1 x the camera's travel;
// the sky slides the same way by a fraction of it. The sign is the whole bug this replaced -- a
// first version wrote +(1 - Drift) x the camera, which is world-space thinking on a screen-space
// node: every layer slid WITH the ship, and the world slid against it.
//
// NO EDGE TO REACH, AT ANY DISTANCE. Position is unbounded (a ship 60 000 u out has a near layer
// 27 000 px off), so the drawn region follows the middle of the screen instead: it is re-centred on
// the WHOLE TILE under the screen's centre, in the plane's own rotated, scaled frame. Moving a tiled
// region by whole tiles moves nothing a player can see, so the field never steps; and because the
// region is re-centred rather than the plane moved, Position stays continuous and a check can read
// the true drift off it. A plane that was only moved, never re-centred, ran out of region far from
// the origin and left the large dark wedges players reported.
public partial class SkyPlane : Node2D
{
    public SkyLayer Row;
    public Texture2D Art;
    private const float Span = 20000f;      // the region drawn, in the plane's own units: 11 000 px at the finest scale
    private Vector2 _cell;                  // the whole tile under the middle of the screen, where the region is centred

    // What is drawn, in the plane's own frame. Public so a check can prove the screen is inside it.
    public Rect2 Drawn => new(_cell - new Vector2(Span, Span) / 2, new Vector2(Span, Span));

    public override void _Ready()
    {
        ZIndex = Row.Z;
        TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
        RotationDegrees = Row.Spin;
        Modulate = Row.Tint;
        Scale = new Vector2(Row.Scale, Row.Scale);
    }

    // Tiled from the region's corner, and the corner only ever moves by whole tiles plus the one
    // constant half-span -- so the phase of the art on screen never jumps when the region re-centres.
    public override void _Draw() => DrawTextureRect(Art, Drawn, tile: true);

    public override void _Process(double _)
    {
        var cam = GetViewport()?.GetCamera2D();
        if (cam == null) return;
        Position = -cam.GlobalPosition * Row.Drift;
        var mid = Transform.AffineInverse() * (GetViewportRect().Size / 2);
        var tile = Art.GetSize();
        var cell = new Vector2(Mathf.Floor(mid.X / tile.X) * tile.X, Mathf.Floor(mid.Y / tile.Y) * tile.Y);
        if (cell == _cell) return;
        _cell = cell;
        QueueRedraw();
    }
}
