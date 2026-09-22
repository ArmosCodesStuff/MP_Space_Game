using Godot;
using System;
using System.Collections.Generic;

// THE BOSS'S HEALTH BAR -- top centre, in the arena only.
// Every hit leaves a CHUNK: the slice of the bar it took. The chunk bobs up and down
// a few times, quickly, the bounces dying away, while it turns from red to white and
// fades off. Hits within 60 ms of each other share one chunk, so a stream of small
// hits reads as one clean chunk rather than a flicker of dozens. Driven by the boss's
// hull, which every peer already has, so guests see it too.
public partial class BossBar : Control
{
    public Hub Hub;
    public const float W = 640, H = 18, Bounce = 6f;
    // Under it, much skinnier: the wind-up to the boss's next SUPER MOVE -- the ram, or the death
    // beam's escorts going out. It fills to full exactly as that move commits, then starts again.
    public const float SuperH = 5f, SuperPad = 3f;
    public const double Life = 0.8, BounceFor = 0.45, Merge = 0.06, BounceHz = 6.67;
    public class Chunk { public double From, To, T; }          // From/To: fractions of max hull
    public readonly List<Chunk> Chunks = new();
    private readonly StyleBox _panel = Ui.PanelStyle(8);        // built once: _Draw runs every frame
    private readonly StyleBox _track = Ui.Box(Ui.Deep, Ui.Line, 4);
    private readonly StyleBox _superTrack = Ui.Box(Ui.Deep, Ui.Line, 3);
    private double _lastHp = -1;

    // vertical offset: a quick up-and-down, dying away
    public static float Offset(double t) =>
        t >= BounceFor ? 0f : (float)(Bounce * Math.Exp(-6 * t) * Math.Sin(2 * Math.PI * BounceHz * t));
    public static float Whiteness(double t) => (float)Math.Clamp(t / (Life * 0.5), 0, 1);       // red -> white
    public static float Alpha(double t) => (float)Math.Clamp(1 - (t - Life * 0.4) / (Life * 0.6), 0, 1);

    public override void _Ready()
    {
        Name = "BossBar";
        AnchorLeft = AnchorRight = 0.5f; OffsetLeft = -W / 2; OffsetRight = W / 2; OffsetTop = 104; OffsetBottom = 104 + H + SuperPad + SuperH;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;                                          // hidden until there is a boss
    }

    public override void _Process(double delta)
    {
        var boss = Hub?.Boss;
        Visible = Hub.InArena && IsInstanceValid(boss);
        if (!Visible) { _lastHp = -1; Chunks.Clear(); return; }
        double max = Math.Max(1, boss.MaxHp), hp = boss.Hp;
        if (_lastHp >= 0 && hp < _lastHp - 1e-9)
        {
            double from = hp / max, to = _lastHp / max;
            var last = Chunks.Count > 0 ? Chunks[^1] : null;
            if (last != null && last.T < Merge && Math.Abs(last.From - to) < 1e-6) last.From = from;    // extend: same burst
            else Chunks.Add(new Chunk { From = from, To = to });
        }
        _lastHp = hp;
        foreach (var c in Chunks) c.T += delta;
        Chunks.RemoveAll(c => c.T >= Life);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var boss = Hub?.Boss;
        if (!IsInstanceValid(boss)) return;                       // the first draw can come before any boss
        _panel.Draw(GetCanvasItem(), new Rect2(-10, -26, W + 20, H + 34 + SuperPad + SuperH));
        // after the kill: your parts collected of those dropped, and how many pilots are ready to go home
        string title = Hub.MissionWon
            ? $"{boss.Type.Name}  ·  DEFEATED  ·  PARTS {Hub.CratesDropped - Hub.Crates.Count} / {Hub.CratesDropped}  ·  {Hub.ReturnReady} / {Hub.ReturnTotal} READY TO RETURN"
            : $"{boss.Type.Name}  ·  LEVEL {Missions.Level}  ·  {boss.Hp:0} / {boss.MaxHp:0}";
        Txt.D(this, ThemeDB.FallbackFont, new Vector2(0, -8), title, HorizontalAlignment.Center, W, 13, Hub.MissionWon ? Ui.Good : Ui.Text);
        float frac = (float)Math.Clamp(boss.Hp / Math.Max(1, boss.MaxHp), 0, 1);
        // The hull stays RED whatever the palette does -- it is the one bar on screen that means
        // "the thing trying to kill you", and reading it as an accent-blue meter would be a lie.
        _track.Draw(GetCanvasItem(), new Rect2(0, 0, W, H));
        DrawRect(new Rect2(1, 1, (W - 2) * frac, H - 2), new Color(0.80f, 0.16f, 0.14f));
        var red = new Color(0.95f, 0.25f, 0.2f);
        foreach (var c in Chunks)
        {
            var col = red.Lerp(Colors.White, Whiteness(c.T)); col.A = Alpha(c.T);
            float x0 = (float)(c.From * W), x1 = (float)(c.To * W);
            DrawRect(new Rect2(x0, Offset(c.T), Math.Max(1f, x1 - x0), H), col);
        }

        // the super-move wind-up: skinny, directly under, filling towards the next one
        float y = H + SuperPad, sf = (float)Math.Clamp(boss.SuperFill, 0, 1);
        _superTrack.Draw(GetCanvasItem(), new Rect2(0, y, W, SuperH));
        // it warms from the accent towards white as it tops out, so a full bar reads at a glance
        var warm = Ui.Accent.Lerp(new Color(1f, 0.95f, 0.85f), sf * sf);
        DrawRect(new Rect2(1, y + 1, (W - 2) * sf, SuperH - 2), warm);
    }
}
