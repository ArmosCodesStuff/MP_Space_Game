using Godot;

// Godot's DrawString CLIPS to its width parameter rather than shrinking or wrapping, so any label
// whose text grew past the width it was written with silently loses its tail ("...PLACE OUTP").
// These helpers measure the string first and place it by its real width, so nothing is ever cut.
public static class Txt
{
    // Set by Hub each frame from the camera zoom (the class was called Zone once; the name outlived
    // it here by a rename). World-space labels multiply their font size by
    // this so they stay a constant size on screen instead of being magnified into a blur.
    public static float UiScale = 1f;
    public static int Size(int basePx) => Mathf.Max(6, Mathf.RoundToInt(basePx * UiScale));

    // Drop-in for DrawString: same arguments, same resulting position, but never clipped.
    // `width` sets the alignment box; text is never truncated to it.
    public static void D(CanvasItem c, Font f, Vector2 pos, string text,
                         HorizontalAlignment align, float width, int size, Color col)
    {
        if (f == null || string.IsNullOrEmpty(text)) return;
        float mw = f.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        float dx = align switch
        {
            HorizontalAlignment.Center => width > 0 ? (width - mw) * 0.5f : -mw * 0.5f,
            HorizontalAlignment.Right  => width > 0 ? (width - mw) : -mw,
            _ => 0f,
        };
        c.DrawString(f, pos + new Vector2(dx, 0), text, HorizontalAlignment.Left, -1, size, col);
    }
    // Centred on a point, for labels that should sit over something.
    public static void Centre(CanvasItem c, Font f, Vector2 at, string text, int size, Color col)
        => D(c, f, at, text, HorizontalAlignment.Center, 0, size, col);
}
