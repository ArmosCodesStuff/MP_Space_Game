using Godot;

// The one panel look for every piece of HUD and menu: dark, slightly see-through,
// a thin blue-grey border, rounded corners. Everything readable sits on one.
public static class Ui
{
    public static StyleBoxFlat PanelStyle(int pad = 10, float alpha = 0.96f)   // 0.90 let bright world art (hologram bars) bleed through
    {
        var s = new StyleBoxFlat { BgColor = new Color(0.04f, 0.06f, 0.10f, alpha), BorderColor = new Color(0.30f, 0.40f, 0.55f, 0.8f) };
        s.SetBorderWidthAll(1); s.SetCornerRadiusAll(6);
        s.ContentMarginLeft = s.ContentMarginRight = pad; s.ContentMarginTop = s.ContentMarginBottom = pad * 0.6f;
        return s;
    }

    public static PanelContainer Wrap(Control c, int pad = 6)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", PanelStyle(pad));
        p.AddChild(c);
        return p;
    }
}
