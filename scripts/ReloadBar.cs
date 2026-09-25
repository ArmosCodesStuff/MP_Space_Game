using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// THE RELOAD BAR -- an active reload's timing bar (ActiveReload.cs), drawn under the owner's own
// ship where the pilot's eyes already are (docs/plans/sniper_active_reload.md §4). New: nothing drew
// this before; it follows a ship the way HaulerHud does (a Control on the HUD layer, placed each frame
// from the canvas transform). It reads only the owner's ReloadView and the chamber on its slot, so
// any gun whose trigger row carries a Reload gets it; nothing here names a class or a gun.
//
// It shows from the shot until Linger after the round seats, all through a charge, and while an
// enhanced round waits; it hides on a wreck, off screen, under the BASE menu, or wherever it would
// meet the bottom HUD (the hull panel or the ability bar: under the free camera the HUD wins).
// ─────────────────────────────────────────────────────────────────────────────
public partial class ReloadBar : Control
{
    public Hub Hub;
    public const float W = 180, H = 22, Track = 160, Gap = 20;
    public const double Linger = 0.6;             // how long the bar stays after the round seats
    private static readonly Color TrackC = new(0x2E / 255f, 0x33 / 255f, 0x3A / 255f, 0.85f), Edge = new(0x15 / 255f, 0x18 / 255f, 0x1C / 255f),
        Grey = new(0x9A / 255f, 0xA0 / 255f, 0xA8 / 255f), Dim = new(0x6B / 255f, 0x70 / 255f, 0x79 / 255f),
        Rail = new(0x73 / 255f, 0xB3 / 255f, 0xFF / 255f), MissC = new(0x8A / 255f, 0x90 / 255f, 0x99 / 255f);
    private double _seatedFor = Linger;
    private PlayerShip _s;
    private ReloadSpec _r;

    // Pure: whether the bar is wanted at all -- a reload or a charge running, an enhanced round
    // waiting, or the round seated less than Linger ago.
    public static bool Wanted(in ActiveReload.View v, int chamber, double seatedFor)
        => v.Running || v.Charge > 0 || chamber == (int)Chamber.Enhanced || seatedFor < Linger;
    // Pure: how far below the ship's centre the bar's top edge sits, in screen pixels: half the hull
    // on screen and Gap, so it never touches the hull at any zoom.
    public static float Drop(float hullLength, float zoom) => hullLength * 0.5f * zoom + Gap;

    public override void _Ready()
    {
        Name = "ReloadBar";
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(W, H);
        PivotOffset = Size / 2;
    }

    public override void _Process(double delta)
    {
        _s = Hub?.MyShip;
        _r = _s == null ? null : Abilities.TriggerOf(_s.Class)?.Reload;
        if (_s == null || _r == null || !_s.Alive) { Visible = false; _seatedFor = Linger; return; }
        var v = _s.ReloadView;
        _seatedFor = v.Running || v.Charge > 0 ? 0 : _seatedFor + delta;
        var ct = GetViewport().GetCanvasTransform();
        Position = ct * _s.GlobalPosition + new Vector2(-W / 2, Drop(_s.MyArt.Length, ct.Scale.Y));
        var me = new Rect2(Position, Size);
        bool clear = GetViewportRect().Encloses(me) && GetParent()?.GetNodeOrNull("BasePanel") == null;
        if (clear && GetParent() is { } layer)
            foreach (var n in layer.GetChildren())
                if (n is HullHud or AbilityBar && n is Control hud && hud.Visible && hud.GetGlobalRect().Grow(8).Intersects(me)) clear = false;
        Visible = clear && Wanted(v, _s.Sl(_r.Id).N, _seatedFor);
        if (Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_s == null || _r == null) return;
        var v = _s.ReloadView;
        int n = _s.Sl(_r.Id).N;
        bool perfect = v.Said == Verdict.Perfect;
        // the perfect flash swells the whole bar 1.12 -> 1.0 about its centre
        double f = v.Flash / ActiveReload.FlashTime;
        float k = perfect && f > 0 ? 1f + 0.12f * (float)f : 1f;
        DrawSetTransform(Size / 2 * (1 - k), 0, new Vector2(k, k));

        var track = new Rect2(0, 7, Track, 8);
        DrawRect(track, TrackC);
        if (v.Charge > 0)
        {   // a charge stroke refills the track in rail blue, a white tick at the end once full
            double full = Math.Max(1e-6, _s.Cadence(_r.Charge));
            float share = (float)Math.Clamp(v.Charge / full, 0, 1);
            DrawRect(new Rect2(track.Position, new Vector2(Track * share, 8)), Rail);
            if (share >= 1) DrawRect(new Rect2(Track - 2, 5, 2, 12), Colors.White);
        }
        else
        {
            float share = v.Running ? (float)Math.Clamp(v.Length > 0 ? v.Since / v.Length : 1, 0, 1) : 1f;
            DrawRect(new Rect2(track.Position, new Vector2(Track * share, 8)), Grey);
        }
        DrawRect(track, Edge, false, 1f);

        // THE SWEET SPOT: a box taller than the track, white; dimmed for the rest of a reload after a miss
        var (open, close) = ActiveReload.Spot(_s.Stats, _r);
        var box = new Rect2(Track * (float)open, 3, Track * (float)(close - open), 16);
        var boxC = v.Running && v.Said == Verdict.Missed ? Dim : Colors.White;
        DrawRect(box, new Color(boxC, 0.2f));
        DrawRect(box, boxC, false, 2f);

        // THE CHAMBER PIP: empty an outline, seated grey, enhanced white with a glow pulsing at 2 Hz
        if (n == (int)Chamber.Enhanced)
        {
            float a = 0.95f + 0.05f * Mathf.Sin(Time.GetTicksMsec() * 0.001f * Mathf.Tau * 2f);
            Pip(new Color(1, 1, 1, 0.25f * a), 4f);
            Pip(new Color(1, 1, 1, a), 0f);
        }
        else if (n >= (int)Chamber.Seated) Pip(Grey, 0f);
        else PipOutline(Grey);

        // THE FLASH: white on a perfect press, grey on a miss, fading over FlashTime
        if (f > 0)
            DrawRect(new Rect2(Vector2.Zero, Size), perfect ? new Color(1, 1, 1, 0.9f * (float)f) : new Color(MissC, 0.8f * (float)f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    // a 10 x 16 capsule at x 170-180, grown by `glow`
    private void Pip(Color c, float glow)
    {
        float r = 5 + glow;
        DrawCircle(new Vector2(175, 8), r, c);
        DrawCircle(new Vector2(175, 14), r, c);
        DrawRect(new Rect2(175 - r, 8, 2 * r, 6), c);
    }
    private void PipOutline(Color c)
    {
        DrawArc(new Vector2(175, 8), 4.5f, Mathf.Pi, Mathf.Tau, 12, c, 1f);
        DrawArc(new Vector2(175, 14), 4.5f, 0, Mathf.Pi, 12, c, 1f);
        DrawLine(new Vector2(170.5f, 8), new Vector2(170.5f, 14), c, 1f);
        DrawLine(new Vector2(179.5f, 8), new Vector2(179.5f, 14), c, 1f);
    }
}
