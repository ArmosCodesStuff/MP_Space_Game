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
// WHAT A LAYER IS: a row. `Parallax2D` does the work -- it reads the canvas transform every frame
// and offsets itself by `ScrollScale`, repeating itself forever at `RepeatSize` so there is no edge
// to reach. This file decides only WHICH layers exist and what each one is made of.
//
// A ROW OF Sky.All MUST FILL IN:
//   Id        what it is called, for a check to name and for a reader to find
//   Texture   what it draws. Several rows may share one; Scale and Tint are what make them differ
//   Drift     HOW MUCH OF THE WORLD'S MOTION A PLAYER SEES IT MAKE. 0 is nailed to the screen
//             (the old behaviour, and the bug); 1 slides past exactly like the world does. Far
//             things drift LESS: that is the whole illusion, and why the rows run far to near.
//
//             It is written in the player's terms and converted once, in Build. The engine's own
//             `ScrollScale` is the INVERSE -- 1 means "follows the camera exactly", which is a
//             layer that never appears to move -- and stating the table in engine terms means
//             every future row is written backwards by whoever reads the numbers and trusts them.
//             Measured: on-screen travel is exactly (1 - ScrollScale) x the camera's, so
//             ScrollScale = 1 - Drift and the row says what it looks like.
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
            var tex = GD.Load<Texture2D>(row.Texture);
            if (tex == null) continue;                 // a row naming art that is not there is skipped, not fatal
            // REPEAT FOREVER. RepeatSize is the tile's drawn size, so it must carry the row's
            // Scale or the seam lands mid-tile and the field visibly steps as it crosses.
            var layer = new Parallax2D
            {
                Name = "Sky_" + row.Id,
                // THE ONE CONVERSION: the row says what a player sees, the engine wants its
                // inverse (see the header). Nothing else in the file knows about ScrollScale.
                ScrollScale = new Vector2(1f - row.Drift, 1f - row.Drift),
                RepeatSize = new Vector2(tex.GetWidth() * row.Scale, tex.GetHeight() * row.Scale),
                RepeatTimes = 3,
                ZIndex = row.Z,
            };
            var art = new Sprite2D
            {
                Texture = tex,
                Centered = false,
                Scale = new Vector2(row.Scale, row.Scale),
                Modulate = row.Tint,
                RotationDegrees = row.Spin,
            };
            layer.AddChild(art);
            parent.AddChild(layer);
        }
    }
}
