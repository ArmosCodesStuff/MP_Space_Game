using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// THE HUB'S PLAIN NODES — the six small classes that hang off the world and touch none of its
// state. Cut out of the end of Hub.cs, unchanged: they read only what Hub already publishes
// (MyShip, Flashes, Mission, MissionT, MissionPortalPos) or nothing at all, so they never had a
// reason to sit in the world host's file.
//
// A NEW ONE OF THESE BELONGS HERE, not in Hub.cs: if it needs a Hub field to be built with, it is
// a plain node; if it needs to reach into the world's private state, it is not, and the world
// should be publishing what it wants instead.
// ─────────────────────────────────────────────────────────────────────────────

// A small warm star for the belt to sit around. Drawn rather than spritework so
// it can pulse without another asset.
public partial class Sun : Node2D
{
    private double _t;
    public override void _Process(double delta) { _t += delta; QueueRedraw(); }
    public override void _Draw()
    {
        float p = 1f + 0.03f * Mathf.Sin((float)_t * 1.3f);
        DrawCircle(Vector2.Zero, 210f * p, new Color(1f, 0.82f, 0.42f, 0.10f));
        DrawCircle(Vector2.Zero, 150f * p, new Color(1f, 0.78f, 0.33f, 0.22f));
        DrawCircle(Vector2.Zero, 104f * p, new Color(1f, 0.86f, 0.48f));
        DrawCircle(Vector2.Zero,  78f * p, new Color(1f, 0.96f, 0.80f));
    }
}

// The outbound wormhole: trade runs leave through it, and it is the door to
// every hostile system.
public partial class Portal : Node2D
{
    private double _t, _flash;
    public Color Tint = new(0.45f, 0.75f, 1f), Core = new(0.10f, 0.16f, 0.34f);   // the mission portal is red
    public void Flash() => _flash = 0.8;        // a hauler jumping through, either way
    public override void _Process(double delta) { _t += delta; _flash = System.Math.Max(0, _flash - delta); QueueRedraw(); }
    public override void _Draw()
    {
        float s = (float)_t;
        if (_flash > 0)
        {
            float k = (float)(_flash / 0.8);
            DrawCircle(Vector2.Zero, 60f + 110f * (1f - k), new Color(0.6f, 0.85f, 1f, 0.55f * k));
        }
        for (int i = 0; i < 4; i++)
        {
            float r = 70f + i * 26f + 6f * Mathf.Sin(s * 1.7f + i);
            DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 40,
                    new Color(Tint.R, Tint.G, Tint.B, 0.55f - 0.10f * i), 3f);
        }
        DrawCircle(Vector2.Zero, 56f, new Color(Core.R, Core.G, Core.B, 0.85f));
    }
}

// The player's hull, big and always on screen: a bar along the bottom centre.
//
// Its two readouts sit ON the bar, so every letter must read against whatever is under IT: dark on
// the fill (dark reads on green and on red alike), light on the empty track. Choosing one colour
// per string cannot do that -- the fill's edge runs through the middle of the text at most hull
// values, and the part past it went dark on dark ("WARP R", half a HULL number). So the bar is two
// layers drawing the same words: this node draws the track and the LIGHT copy; _fill, a child
// clipped to the filled width, draws the fill and the DARK copy over it. The UI lint cannot catch
// this kind of fault: it measures position and width, not contrast.
public partial class HullHud : Control
{
    private readonly StyleBox _panel = Ui.PanelStyle();   // built once: _Draw runs every frame
    private readonly StyleBox _track = Ui.Box(Ui.Deep, Ui.Line, 5);
    public Hub Hub;
    private const float W = 440, H = 22;
    private Control _fill;
    private float _frac;

    public override void _Ready()
    {
        AnchorLeft = AnchorRight = 0.5f; AnchorTop = AnchorBottom = 1f;
        OffsetLeft = -W / 2; OffsetRight = W / 2; OffsetTop = -150; OffsetBottom = -150 + H;
        MouseFilter = MouseFilterEnum.Ignore;
        _fill = new Control { Name = "Fill", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        _fill.Draw += () => DrawFill(_fill);
        AddChild(_fill);
    }

    public override void _Process(double delta)
    {
        var s = Hub?.MyShip;
        _frac = s == null ? 0 : (float)Mathf.Clamp(s.Hp / Mathf.Max(1, s.MaxHp), 0, 1);
        _fill.Visible = s != null;
        _fill.Size = new Vector2(1 + (W - 2) * _frac, H);
        QueueRedraw(); _fill.QueueRedraw();
    }

    public override void _Draw()
    {
        if (Hub?.MyShip == null) return;
        _panel.Draw(GetCanvasItem(), new Rect2(-8, -6, W + 16, H + 12));   // its panel
        _track.Draw(GetCanvasItem(), new Rect2(0, 0, W, H));
        Readouts(this, onFill: false);
        // THE VICTIM'S LINE, over the bar: a squad on its burn at this ship names what it brings
        if (SquadSight.GankLine(Hub.Raiders, Hub.Ships, Hub.MyShip) is { } gank)
            Txt.D(this, ThemeDB.FallbackFont, new Vector2(0, -12), gank, HorizontalAlignment.Center, W, 15, Ui.Bad);
    }

    // Your own hull keeps its green-to-red reading -- it is the one number you glance at while
    // being shot, and a palette accent would say nothing about how close you are to dying. The
    // TRACK comes from the palette so the bar still belongs to the rest of the HUD.
    private void DrawFill(Control c)
    {
        if (Hub?.MyShip == null) return;
        c.DrawRect(new Rect2(1, 1, (W - 2) * _frac, H - 2), _frac > 0.35f ? Ui.Good : Ui.Bad);
        Readouts(c, onFill: true);
    }

    private void Readouts(CanvasItem c, bool onFill)
    {
        var s = Hub.MyShip;
        Txt.D(c, ThemeDB.FallbackFont, new Vector2(0, H - 5), s.Alive ? $"HULL  {s.Hp:0} / {s.MaxHp:0}"
                  : s.CanReboard ? "SHIP READY  —  F re-boards"
                  : $"SHIP IN STASIS  {(int)s.StasisLeft / 60}:{(int)s.StasisLeft % 60:00}  —  flying the escape pod",
              HorizontalAlignment.Center, W, 15, onFill && _frac > 0.35f ? Ui.Deep : Colors.White);
        if (!s.Alive) return;
        // off the fill the warp readout takes its state's colour; on it, dark like the hull's
        var warp = onFill ? Ui.Deep : s.Warping ? Ui.Accent : s.WarpCooldownLeft > 0 ? Ui.Dim : Ui.Good;
        Txt.D(c, ThemeDB.FallbackFont, new Vector2(W - 150, H - 5),
              s.Warping ? $"WARPING  {s.WarpWarmupLeft:0.0} s" : s.WarpCooldownLeft > 0 ? $"WARP  {s.WarpCooldownLeft:0} s" : "WARP  READY",
              HorizontalAlignment.Right, 144, 12, warp);
    }
}

// Draws the hub's hit flashes (see Hub._Ready for why it is a layer of its own).
public partial class FlashLayer : Node2D
{
    public Hub Hub;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        foreach (var f in Hub.Flashes)
            DrawLine(f.a, f.b, new Color(f.c.R, f.c.G, f.c.B, (float)(f.t / Hub.FlashLife) * 0.9f), 2f);
    }
}

// Text that lives in the world (a building's name): drawn, not a UI control.
public partial class WorldLabel : Node2D
{
    public string Text = "";
    public override void _Draw() => Txt.Centre(this, ThemeDB.FallbackFont, Vector2.Zero, Text, Txt.Size(14), new Color(0.8f, 0.85f, 0.95f, 0.8f));
}

// The mission portal's opening bar, at the TIO's top right: world-drawn, on every peer.
public partial class MissionBar : Node2D
{
    public Hub Hub;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Hub.Mission != Hub.MissionState.Opening) return;
        var at = Hub.MissionPortalPos; const float W = 180f, H = 12f;
        float k = Mathf.Clamp((float)(Hub.MissionT / Hub.PortalOpenTime), 0f, 1f);
        DrawRect(new Rect2(at - new Vector2(W / 2, H / 2), new Vector2(W, H)), new Color(0.05f, 0.05f, 0.08f, 0.9f));
        DrawRect(new Rect2(at - new Vector2(W / 2, H / 2), new Vector2(W * k, H)), new Color(1f, 0.4f, 0.3f, 0.95f));
        DrawRect(new Rect2(at - new Vector2(W / 2, H / 2), new Vector2(W, H)), new Color(1f, 0.6f, 0.5f), false, 1.5f);
        Txt.Centre(this, ThemeDB.FallbackFont, at + new Vector2(0, -16), "OPENING PORTAL", Txt.Size(14), new Color(1f, 0.7f, 0.6f));
    }
}
