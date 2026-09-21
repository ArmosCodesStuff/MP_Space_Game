using Godot;

// A small hull bar over a ship, drawn in the calling CanvasItem's local space; barTopLeft is the
// bar's top-left corner. Sized by Txt.UiScale -- the inverse of the camera zoom -- so it keeps a
// CONSTANT ON-SCREEN size while the ships under it scale: without that the overlay balloons when
// zoomed in and swallows the sprite; with it, the furniture stays put and the ship grows.
public static class HealthBar
{
    public static void Draw(CanvasItem ci, Vector2 barTopLeft, float w, float h, double cur, double max, Color fill)
    {
        float k = Txt.UiScale;
        barTopLeft *= k; w *= k; h *= k;
        float frac = max > 0 ? (float)(cur / max) : 0f;
        frac = Mathf.Clamp(frac, 0f, 1f);

        // Plain rects, not a StyleBox: these are a few pixels tall and drawn once per ship per
        // frame, and a rounded corner on a 5 px bar is mush. Only the colours come from the palette.
        ci.DrawRect(new Rect2(barTopLeft, new Vector2(w, h)), Ui.Deep with { A = 0.92f });
        if (frac > 0f)
            ci.DrawRect(new Rect2(barTopLeft, new Vector2(w * frac, h)), fill);
        ci.DrawRect(new Rect2(barTopLeft, new Vector2(w, h)), Ui.Line, false, 1f);
    }
}
