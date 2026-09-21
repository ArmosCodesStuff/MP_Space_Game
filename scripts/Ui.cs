using Godot;

// THE LOOK -- one palette, one panel, one button, for every screen.
//
// Flat dark surfaces, 10 px corners, a hairline border and one accent blue. Nothing is
// shaded: the old look drew a vertical gradient with a bright bevel along the top edge,
// which is why it had to bake little textures and nine-patch them. A flat surface is
// exactly what StyleBoxFlat does natively -- antialiased corners, a real border, a soft
// shadow -- so the texture generator and its cache are gone.
//
// Three surfaces, in order of lightness, and everything on screen is one of them:
//   Deep    the inside of a field, or the empty part of a bar -- darker than the panel
//   Panel   a window, a HUD panel, the thing that floats over the world
//   Card    a row, a slot, a tab INSIDE a panel -- one step up, so rows read as objects
//
// The drawn HUD (ability bar, boss bar, radar, health bars) is not made of Controls and
// cannot inherit the theme, so it reads the same colours from here and builds its own
// boxes with Box(). One palette, two rendering paths.
public static class Ui
{
    // ---------------------------------------------------------------- palette
    public static readonly Color
        Deep   = new(0.043f, 0.055f, 0.078f),   // #0b0e14
        Panel  = new(0.071f, 0.094f, 0.137f),   // #121823
        Card   = new(0.102f, 0.129f, 0.173f),   // #1a212c
        Line   = new(0.184f, 0.227f, 0.302f),   // #2f3a4d  hairline borders
        Accent = new(0.302f, 0.639f, 1.000f),   // #4da3ff  headings, the live tab, bar fills
        Text   = new(0.788f, 0.831f, 0.902f),   // #c9d4e6
        Dim    = new(0.518f, 0.573f, 0.659f),   // #8492a8  secondary text
        Good   = new(0.290f, 0.871f, 0.502f),   // #4ade80
        Warn   = new(0.984f, 0.749f, 0.141f),   // #fbbf24
        Bad    = new(0.973f, 0.443f, 0.443f);   // #f87171

    // Corner radii. A panel is rounder than the things inside it: a card with its container's
    // radius looks like it is bulging out of it.
    public const int PanelCorner = 10, CardCorner = 7;

    // ---------------------------------------------------------------- type scale
    // Five sizes, and every screen picks from them. The old code set sizes ad hoc -- 11, 12,
    // 13, 14, 15, 16, 20, 22, 34, 64 -- so no two headings on different screens matched.
    public const int Display = 56, Title = 22, Head = 17, Body = 14, Small = 12;

    // ---------------------------------------------------------------- building blocks

    // The one rounded box. Drawn Controls cache one of these and call Draw() on it; themed
    // Controls get theirs through the theme below.
    public static StyleBoxFlat Box(Color bg, Color border, int corner = CardCorner, int borderW = 1)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true };
        s.SetBorderWidthAll(borderW);
        s.SetCornerRadiusAll(corner);
        return s;
    }

    static StyleBoxFlat Pad(StyleBoxFlat s, float x, float y)
    {
        s.ContentMarginLeft = s.ContentMarginRight = x;
        s.ContentMarginTop = s.ContentMarginBottom = y;
        return s;
    }

    // Every panel in the game. Opaque by default: menus sit over the world, and a hull bar
    // showing through a menu as a faint line is the reason this is not translucent.
    public static StyleBoxFlat PanelStyle(int pad = 10, float alpha = 1f)
    {
        var s = Pad(Box(new Color(Panel, alpha), new Color(Line, alpha), PanelCorner), pad + 4, pad);
        s.ShadowColor = new Color(0, 0, 0, 0.55f * alpha);
        s.ShadowSize = 14;
        s.ShadowOffset = new Vector2(0, 4);
        return s;
    }

    // A row inside a panel: the flat card everything else is built out of.
    public static StyleBoxFlat CardStyle(int padX = 12, int padY = 9) => Pad(Box(Card, Line), padX, padY);

    // THE THEME REACHES A CONTROL THROUGH ITS ANCESTORS, AND A CanvasLayer IS NOT ONE.
    //
    // Every piece of UI in this game hangs off a CanvasLayer, and a theme set on the root window
    // does not cross it -- so for the project's whole history the theme's Button entries reached
    // nothing and every button in the game drew Godot's stock theme. It was invisible because the
    // stock theme is also a dark rounded rectangle. Proved by setting the themed button fill to
    // pure red: zero red pixels in any frame, while the theme resource itself still reported the
    // red. Styling one PanelContainer with this turned every button inside it red at once,
    // including ones with no theme of their own.
    //
    // So: call this on the ROOT CONTROL of each UI subtree -- everything under it inherits. The
    // smoke test asserts a real button resolves to this theme, so a subtree that misses the call
    // fails loudly instead of quietly looking like Godot.
    public static void Style(Control c) => c.Theme = Theme;
    // A dialog is a Window, not a Control, and breaks the chain the same way a CanvasLayer does.
    public static void Style(Window w) => w.Theme = Theme;

    public static PanelContainer Wrap(Control c, int pad = 6)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Theme };
        p.AddThemeStyleboxOverride("panel", PanelStyle(pad));
        p.AddChild(c);
        return p;
    }

    // A window panel: the one panel look plus the theme, so a window never has to remember both.
    public static void Panelise(PanelContainer p, int pad = 10, float alpha = 1f)
    {
        p.Theme = Theme;
        p.AddThemeStyleboxOverride("panel", PanelStyle(pad, alpha));
    }

    // A row card around any control, for lists of actions and readouts.
    public static PanelContainer CardWrap(Control c, int padX = 12, int padY = 9)
    {
        var p = new PanelContainer { Theme = Theme };
        p.AddThemeStyleboxOverride("panel", CardStyle(padX, padY));
        p.AddChild(c);
        return p;
    }

    // ---------------------------------------------------------------- labels

    public static Label Lbl(string text, int size = Body, Color? col = null)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (col.HasValue) l.AddThemeColorOverride("font_color", col.Value);
        return l;
    }

    // A column or a row with its spacing -- the two lines every box in the UI was built with.
    public static VBoxContainer VBox(int separation, string name = null) => Spaced(new VBoxContainer(), separation, name);
    public static HBoxContainer HBox(int separation, string name = null) => Spaced(new HBoxContainer(), separation, name);
    private static T Spaced<T>(T box, int separation, string name) where T : BoxContainer
    {
        box.AddThemeConstantOverride("separation", separation);
        if (name != null) box.Name = name;
        return box;
    }

    // A button that never keeps keyboard focus: one that kept it would be pressed again by Space or
    // Enter mid-flight. `size` sets the font, and a big button's height with it.
    public static Button Btn(string text, System.Action onPress, string name = null, int size = 0)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        if (name != null) b.Name = name;
        if (size > 0) { b.AddThemeFontSizeOverride("font_size", size); b.CustomMinimumSize = new Vector2(0, size >= Head ? 46 : 34); }
        if (onPress != null) b.Pressed += onPress;
        return b;
    }

    // Empty a container now: out of the tree at once (so a rebuilt list never lays out beside
    // the one it replaces for a frame), freed at the end of the frame.
    public static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }

    // A section heading: small, upper case, accent, with a hairline rule under it. The rule is
    // what makes a list of rows read as a section rather than as a pile.
    public static VBoxContainer Heading(string text)
    {
        var v = VBox(4);
        v.AddChild(Lbl(text.ToUpperInvariant(), Small, Accent));
        var rule = new Panel { CustomMinimumSize = new Vector2(0, 1) };
        rule.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Line });
        v.AddChild(rule);
        return v;
    }

    // A tab in a strip: a toggle button, so the theme's "pressed" box -- accent border, accent
    // text -- marks the live one without any per-tab colour code at the call site.
    public static Button Tab(string text, ButtonGroup group, bool on)
    {
        var b = new Button { Text = text, Name = "Tab_" + text, ToggleMode = true, ButtonGroup = group,
                             FocusMode = Control.FocusModeEnum.None, ButtonPressed = on };
        b.AddThemeFontSizeOverride("font_size", Small);
        return b;
    }

    // ---------------------------------------------------------------- the theme
    static Theme _theme;
    private static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            _theme = new Theme();

            // 2 px, not 1. The game renders at 2560x1440 with no UI scaling, and a 1 px border
            // on a button is a quarter of a millimetre of screen -- it disappeared entirely in the
            // first sweep, which is how a bright accent outline on the live tab went unnoticed.
            StyleBoxFlat Btn(Color bg, Color border) => Pad(Box(bg, border, CardCorner, 2), 14, 7);
            _theme.SetStylebox("normal", "Button", Btn(Card, Line));
            _theme.SetStylebox("hover", "Button", Btn(Card.Lerp(Accent, 0.10f), Line.Lerp(Accent, 0.45f)));
            // Toggle buttons use "pressed" for ON, so this is also the live tab in a strip.
            _theme.SetStylebox("pressed", "Button", Btn(Deep.Lerp(Accent, 0.14f), Accent));
            _theme.SetStylebox("disabled", "Button", Btn(Panel, Line.Lerp(Panel, 0.5f)));
            _theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
            _theme.SetStylebox("hover_pressed", "Button", _theme.GetStylebox("pressed", "Button"));
            _theme.SetFontSize("font_size", "Button", Body);
            _theme.SetColor("font_color", "Button", Text);
            _theme.SetColor("font_hover_color", "Button", Colors.White);
            _theme.SetColor("font_pressed_color", "Button", Accent);
            _theme.SetColor("font_hover_pressed_color", "Button", Accent);
            _theme.SetColor("font_disabled_color", "Button", Dim with { A = 0.55f });

            _theme.SetFontSize("font_size", "Label", Body);
            _theme.SetColor("font_color", "Label", Text);

            _theme.SetStylebox("normal", "LineEdit", Pad(Box(Deep, Line), 10, 6));
            _theme.SetStylebox("focus", "LineEdit", Pad(Box(Deep, Accent), 10, 6));
            _theme.SetColor("font_color", "LineEdit", Text);
            _theme.SetColor("caret_color", "LineEdit", Accent);
            _theme.SetColor("selection_color", "LineEdit", Accent with { A = 0.30f });

            _theme.SetStylebox("background", "ProgressBar", Box(Deep, Line, 4));
            _theme.SetStylebox("fill", "ProgressBar", Box(Accent, Accent, 4));

            _theme.SetStylebox("panel", "PanelContainer", PanelStyle());
            _theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = Line, Thickness = 1 });
            _theme.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = Line, Thickness = 1, Vertical = true });
            return _theme;
        }
    }

    // Once per launch (idempotent): the root window takes the theme, which covers any Control that
    // is NOT under a CanvasLayer. Everything that is needs Style() or Panelise() -- see above.
    public static void Install(SceneTree tree) { if (tree.Root.Theme != Theme) tree.Root.Theme = Theme; }

    // Set a control's text only when it actually CHANGED. Assigning Text re-shapes the control's
    // glyphs, and these are MSDF fonts; several HUD panels rewrite theirs every frame for strings
    // that move about once a second (a credits tick, a countdown, a level). Build the string as
    // before -- it is the assignment that costs, not the interpolation.
    public static void SetText(Label l, string t) { if (l.Text != t) l.Text = t; }
    public static void SetText(Button b, string t) { if (b.Text != t) b.Text = t; }
}
