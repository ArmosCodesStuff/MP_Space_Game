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
    public const double Life = 0.8, BounceFor = 0.45, Merge = 0.06, BounceHz = 6.67;
    public class Chunk { public double From, To, T; }          // From/To: fractions of max hull
    public readonly List<Chunk> Chunks = new();
    private readonly StyleBox _panel = Ui.PanelStyle(8);        // built once: _Draw runs every frame
    private double _lastHp = -1;

    // vertical offset: a quick up-and-down, dying away
    public static float Offset(double t) =>
        t >= BounceFor ? 0f : (float)(Bounce * Math.Exp(-6 * t) * Math.Sin(2 * Math.PI * BounceHz * t));
    public static float Whiteness(double t) => (float)Math.Clamp(t / (Life * 0.5), 0, 1);       // red -> white
    public static float Alpha(double t) => (float)Math.Clamp(1 - (t - Life * 0.4) / (Life * 0.6), 0, 1);

    public override void _Ready()
    {
        Name = "BossBar";
        AnchorLeft = AnchorRight = 0.5f; OffsetLeft = -W / 2; OffsetRight = W / 2; OffsetTop = 104; OffsetBottom = 104 + H;
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
        _panel.Draw(GetCanvasItem(), new Rect2(-10, -26, W + 20, H + 34));
        Txt.D(this, ThemeDB.FallbackFont, new Vector2(0, -8), $"{Missions.BossName}  ·  TIER {Missions.Tier}  ·  {boss.Hp:0} / {boss.MaxHp:0}",
              HorizontalAlignment.Center, W, 13, new Color(1f, 0.85f, 0.8f));
        float frac = (float)Math.Clamp(boss.Hp / Math.Max(1, boss.MaxHp), 0, 1);
        DrawRect(new Rect2(0, 0, W, H), new Color(0.10f, 0.05f, 0.05f, 0.95f));
        DrawRect(new Rect2(0, 0, W * frac, H), new Color(0.80f, 0.16f, 0.14f));
        DrawRect(new Rect2(0, 0, W * frac, 3), new Color(1f, 0.45f, 0.4f, 0.6f));            // a shine along the top
        var red = new Color(0.95f, 0.25f, 0.2f);
        foreach (var c in Chunks)
        {
            var col = red.Lerp(Colors.White, Whiteness(c.T)); col.A = Alpha(c.T);
            float x0 = (float)(c.From * W), x1 = (float)(c.To * W);
            DrawRect(new Rect2(x0, Offset(c.T), Math.Max(1f, x1 - x0), H), col);
        }
        DrawRect(new Rect2(0, 0, W, H), new Color(0.9f, 0.5f, 0.45f, 0.7f), false, 1.5f);
    }
}
