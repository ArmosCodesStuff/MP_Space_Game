using Godot;

// THE LOOK -- one panel and one button style for every screen.
// Panels: a vertical gradient (lighter at the top), a bright 1 px top edge like light
// on a bevel, a crisp border, 3 px corners and a soft drop shadow outside the panel.
// Built as small generated textures (a flat style cannot shade), cached, and installed
// as the root window's theme so every Control inherits it.
public static class Ui
{
    const int Shadow = 5, Corner = 3;
    static readonly System.Collections.Generic.Dictionary<string, ImageTexture> _tex = new();
    static Theme _theme;

    // A panel or button face: size N, gradient top->bottom, border, top highlight, optional shadow.
    static ImageTexture Face(string key, Color top, Color bottom, Color border, Color highlight, bool shadow, float alpha = 1f)
    {
        key += alpha.ToString("0.00");
        if (_tex.TryGetValue(key, out var t)) return t;
        int pad = shadow ? Shadow : 0, inner = 24, n = inner + 2 * pad;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // distance outside the rounded inner rect (0 inside)
                float dx = Mathf.Max(Mathf.Max(pad + Corner - x, x - (n - 1 - pad - Corner)), 0);
                float dy = Mathf.Max(Mathf.Max(pad + Corner - y, y - (n - 1 - pad - Corner)), 0);
                float cornerD = Mathf.Sqrt(dx * dx + dy * dy) - Corner;                  // >0 outside the corner arc
                bool inRect = x >= pad && x < n - pad && y >= pad && y < n - pad;
                bool inside = inRect && cornerD <= 0.5f;
                if (!inside)
                {
                    if (!shadow) { img.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }
                    float od = Mathf.Max(Mathf.Max(pad - x, x - (n - 1 - pad)), Mathf.Max(pad - y, y - (n - 1 - pad)));
                    od = Mathf.Max(od, cornerD);
                    float a = Mathf.Clamp(1f - od / Shadow, 0f, 1f);
                    img.SetPixel(x, y, new Color(0, 0, 0, 0.45f * a * a * alpha)); continue;
                }
                float k = (float)(y - pad) / (inner - 1);
                var c = top.Lerp(bottom, k);
                bool edge = x == pad || x == n - 1 - pad || y == pad || y == n - 1 - pad || cornerD > -1f;
                if (edge) c = border;
                else if (y == pad + 1) c = highlight;                                      // light on the top bevel
                img.SetPixel(x, y, new Color(c.R, c.G, c.B, c.A * alpha));
            }
        return _tex[key] = ImageTexture.CreateFromImage(img);
    }

    static StyleBoxTexture Box(ImageTexture face, bool shadow, float padX, float padY)
    {
        int m = (shadow ? Shadow : 0) + Corner + 2;
        var s = new StyleBoxTexture { Texture = face };
        s.TextureMarginLeft = s.TextureMarginRight = s.TextureMarginTop = s.TextureMarginBottom = m;
        if (shadow) { s.ExpandMarginLeft = s.ExpandMarginRight = s.ExpandMarginTop = s.ExpandMarginBottom = Shadow; }
        s.ContentMarginLeft = s.ContentMarginRight = padX; s.ContentMarginTop = s.ContentMarginBottom = padY;
        return s;
    }

    static readonly Color Border = new(0.36f, 0.47f, 0.63f), Top = new(0.12f, 0.15f, 0.21f), Bottom = new(0.045f, 0.055f, 0.085f),
                          Shine = new(0.52f, 0.64f, 0.82f);

    // Every panel in the game. Opaque by default: menus sit over the world.
    public static StyleBoxTexture PanelStyle(int pad = 10, float alpha = 1f) =>
        Box(Face("panel", Top, Bottom, Border, Shine, true, alpha), true, pad, pad * 0.6f);

    public static PanelContainer Wrap(Control c, int pad = 6)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", PanelStyle(pad));
        p.AddChild(c);
        return p;
    }

    // The game-wide theme: buttons, text fields and bars in the same shaded style.
    private static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            _theme = new Theme();
            StyleBox Btn(string k, Color t, Color b, Color border, Color shine) => Box(Face("btn_" + k, t, b, border, shine, false), false, 10, 5);
            _theme.SetStylebox("normal", "Button", Btn("normal", new Color(0.16f, 0.19f, 0.26f), new Color(0.08f, 0.09f, 0.13f), new Color(0.32f, 0.41f, 0.55f), new Color(0.40f, 0.50f, 0.66f)));
            _theme.SetStylebox("hover", "Button", Btn("hover", new Color(0.21f, 0.26f, 0.35f), new Color(0.10f, 0.12f, 0.17f), new Color(0.55f, 0.70f, 0.92f), new Color(0.62f, 0.76f, 0.95f)));
            _theme.SetStylebox("pressed", "Button", Btn("pressed", new Color(0.07f, 0.08f, 0.11f), new Color(0.15f, 0.19f, 0.26f), new Color(0.55f, 0.70f, 0.92f), new Color(0.10f, 0.12f, 0.16f)));
            _theme.SetStylebox("disabled", "Button", Btn("disabled", new Color(0.09f, 0.10f, 0.12f), new Color(0.06f, 0.065f, 0.08f), new Color(0.20f, 0.23f, 0.28f), new Color(0.13f, 0.15f, 0.18f)));
            _theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
            _theme.SetStylebox("hover_pressed", "Button", _theme.GetStylebox("pressed", "Button"));
            _theme.SetColor("font_disabled_color", "Button", new Color(0.55f, 0.58f, 0.64f));
            _theme.SetStylebox("normal", "LineEdit", Btn("edit", new Color(0.04f, 0.05f, 0.08f), new Color(0.07f, 0.08f, 0.11f), new Color(0.30f, 0.38f, 0.50f), new Color(0.05f, 0.06f, 0.09f)));
            _theme.SetStylebox("focus", "LineEdit", new StyleBoxEmpty());
            _theme.SetStylebox("background", "ProgressBar", Btn("barbg", new Color(0.04f, 0.05f, 0.07f), new Color(0.07f, 0.08f, 0.10f), new Color(0.26f, 0.32f, 0.42f), new Color(0.05f, 0.06f, 0.08f)));
            _theme.SetStylebox("fill", "ProgressBar", Btn("barfill", new Color(0.45f, 0.72f, 1f), new Color(0.22f, 0.45f, 0.78f), new Color(0.50f, 0.75f, 1f), new Color(0.75f, 0.9f, 1f)));
            _theme.SetStylebox("panel", "PanelContainer", PanelStyle());
            return _theme;
        }
    }

    // Once per launch (idempotent): the whole window takes the theme.
    public static void Install(SceneTree tree) { if (tree.Root.Theme != Theme) tree.Root.Theme = Theme; }

    // Set a control's text only when it actually CHANGED. Assigning Text re-shapes the control's
    // glyphs, and these are MSDF fonts; several HUD panels rewrite theirs every frame for strings
    // that move about once a second (a credits tick, a countdown, a level). Build the string as
    // before -- it is the assignment that costs, not the interpolation.
    public static void SetText(Label l, string t) { if (l.Text != t) l.Text = t; }
    public static void SetText(Button b, string t) { if (b.Text != t) b.Text = t; }
}
