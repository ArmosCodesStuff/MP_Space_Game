using Godot;

// Reusable health/material bar with a numeric value drawn to the LEFT of the bar.
// Drawn in the calling CanvasItem's local space; barTopLeft is the bar's top-left corner.
public static class HealthBar
{
    // Inverse of the camera zoom, so the bar and its number keep a CONSTANT ON-SCREEN size while
    // the ships under them scale. Without this the overlay balloons when zoomed in and swallows
    // the sprite; with it, the furniture stays put and the ship grows, which is what reads well.
    public static float UiScale = 1f;

    public static void Draw(CanvasItem ci, Vector2 barTopLeft, float w, float h,
                            double cur, double max, Color fill, Font font, int fontSize = 13)
    {
        float k = UiScale;
        barTopLeft *= k; w *= k; h *= k;
        float frac = max > 0 ? (float)(cur / max) : 0f;
        frac = Mathf.Clamp(frac, 0f, 1f);

        ci.DrawRect(new Rect2(barTopLeft, new Vector2(w, h)), new Color(0.10f, 0.10f, 0.13f, 0.92f));
        if (frac > 0f)
            ci.DrawRect(new Rect2(barTopLeft, new Vector2(w * frac, h)), fill);
        ci.DrawRect(new Rect2(barTopLeft, new Vector2(w, h)), new Color(0f, 0f, 0f, 0.55f), false, 1f);

        if (font != null)
        {
            int shown = Mathf.CeilToInt((float)cur);
            if (shown < 0) shown = 0;
            // right-aligned box that ends just to the left of the bar
            float boxW = 64f * k;
            var boxPos = new Vector2(barTopLeft.X - boxW - 6f * k, barTopLeft.Y + h);
            Txt.D(ci, font, boxPos, shown.ToString(), HorizontalAlignment.Right, boxW,
                          Mathf.Max(6, Mathf.RoundToInt(fontSize * k)), new Color(0.88f, 0.93f, 1f));
        }
    }
}
