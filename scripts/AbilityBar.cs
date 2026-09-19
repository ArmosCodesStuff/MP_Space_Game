using Godot;

// The ability bar: one slot per ability of the player's class, in order, along the
// bottom centre. Each shows its key, its name, and its live state -- ready, active,
// a countdown, ammunition -- with a dark sweep over the slot while it recharges.
// Keys follow the current bindings, so a remap in the K window shows here at once.
public partial class AbilityBar : Control
{
    private readonly StyleBoxFlat _panel = Ui.PanelStyle();   // built once: _Draw runs every frame
    public Hub Hub;
    public const float SlotW = 118, SlotH = 64, Gap = 8, GroupGap = 22;   // extra space before the open slots

    public override void _Ready()
    {
        AnchorLeft = AnchorRight = 0.5f; AnchorTop = AnchorBottom = 1f;
        OffsetTop = -40 - SlotH; OffsetBottom = -40;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    // What a slot says, whether it is lit (active/engaged), and how much of it is
    // still recharging (0..1).
    public struct SlotState { public string Line; public bool Lit; public float Busy; }

    public static SlotState StateOf(PlayerShip s, string id, IHittable selected)
    {
        var st = new SlotState { Line = "READY" };
        var S = s.Stats;
        switch (id)
        {
            case "guns":
                st.Line = s.Staggered ? "STAGGERED" : "SALVO"; st.Lit = s.Trigger; break;
            case "firemode":
                st.Line = s.Staggered ? "STAGGERED" : "SALVO"; break;
            case "missile":
                st.Line = s.Reloading ? "RELOADING" : s.MissilesLoaded == 0 ? "EMPTY · R" : $"{s.MissilesLoaded}/{S["missile_mag"]:0}";
                if (s.Reloading) st.Busy = (float)(s.MissileReloadLeft / S["missile_reload"]);
                break;
            case "reload":
                if (s.Reloading) { st.Line = $"{s.MissileReloadLeft:0.0}s"; st.Busy = (float)(s.MissileReloadLeft / S["missile_reload"]); }
                else st.Line = s.MissilesLoaded >= (int)S["missile_mag"] ? "FULL" : "READY";
                break;
            case "pd":
                if (s.PdActive) { st.Line = $"ACTIVE {s.PdLeft:0}s"; st.Lit = true; }
                else if (!s.PdReady) { st.Line = $"{s.PdRechargeLeft:0}s"; st.Busy = s.PdRechargeFrac; }
                break;
            case "attack":
                if (s.WingTarget != null) { st.Line = "ENGAGED"; st.Lit = true; }
                else if (selected == null) st.Line = "NO TARGET";
                else if (s.Position.DistanceTo(selected.Position) > S["control_range"]) st.Line = "OUT OF RANGE";
                break;
            case "recall":
                st.Line = s.WingTarget != null ? "READY" : "IN ORBIT"; break;
            case "bombers":
            {
                int total = s.WingCount(WingKind.Bomber), ready = s.BombersReady;
                if (s.StrikeTarget != null) { st.Line = "STRIKING"; st.Lit = true; }
                else if (ready == total && total > 0)
                    st.Line = selected == null ? "NO TARGET"
                            : s.Position.DistanceTo(selected.Position) > S["strike_range"] ? "OUT OF RANGE"
                            : $"READY {ready}/{total}";
                else if (s.BomberRearmLeft > 0) { st.Line = $"REARM {s.BomberRearmLeft:0}s"; st.Busy = (float)(s.BomberRearmLeft / S["bomber_rearm"]); }
                else st.Line = "RETURNING";
                break;
            }
        }
        return st;
    }

    public override void _Draw()
    {
        var s = Hub?.MyShipPublic;
        if (s == null) return;
        var list = Abilities.For(s.Class);
        bool hasOwn = list.Length > 0 && !list[0].Open;
        float total = list.Length * SlotW + (list.Length - 1) * Gap + (hasOwn ? GroupGap : 0);
        float x = -total / 2f;
        var font = ThemeDB.FallbackFont;
        _panel.Draw(GetCanvasItem(), new Rect2(x - 8, -8, total + 16, SlotH + 16));   // the bar's panel
        for (int i = 0; i < list.Length; i++)
        {
            var ab = list[i];
            if (i > 0 && ab.Open && !list[i - 1].Open) x += GroupGap;
            var r = new Rect2(x, 0, SlotW, SlotH);
            x += SlotW + Gap;
            string key0 = Abilities.KeyName(Abilities.KeyFor(s.Class, ab.Id));
            if (ab.Open)
            {   // an open hotkey: a quiet empty slot with its key
                DrawRect(r, new Color(0.06f, 0.07f, 0.09f, 0.75f));
                DrawRect(r, new Color(0.35f, 0.4f, 0.5f, 0.45f), false, 1f);
                Txt.D(this, font, r.Position + new Vector2(6, 16), key0, HorizontalAlignment.Left, 0, 13, new Color(1f, 0.85f, 0.4f, 0.6f));
                continue;
            }
            var st = StateOf(s, ab.Id, Hub.Selected);
            DrawRect(r, st.Lit ? new Color(0.16f, 0.30f, 0.22f, 0.95f) : new Color(0.08f, 0.09f, 0.12f, 0.92f));
            if (st.Busy > 0)   // recharge sweep: a dark band shrinking from the top
                DrawRect(new Rect2(r.Position, new Vector2(SlotW, SlotH * Mathf.Clamp(st.Busy, 0, 1))), new Color(0, 0, 0, 0.55f));
            DrawRect(r, st.Lit ? new Color(0.45f, 1f, 0.6f) : new Color(0.45f, 0.55f, 0.7f, 0.7f), false, 1.5f);

            string key = Abilities.KeyName(Abilities.KeyFor(s.Class, ab.Id));
            Txt.D(this, font, r.Position + new Vector2(6, 16), key, HorizontalAlignment.Left, 0, 13, new Color(1f, 0.85f, 0.4f));
            if (ab.Kind == AbilityKind.Hold)
                Txt.D(this, font, r.Position + new Vector2(0, 16), "hold", HorizontalAlignment.Right, SlotW - 6, 11, new Color(1, 1, 1, 0.45f));
            Txt.D(this, font, r.Position + new Vector2(0, 38), ab.Short, HorizontalAlignment.Center, SlotW, 16, Colors.White);
            Txt.D(this, font, r.Position + new Vector2(0, 56), st.Line, HorizontalAlignment.Center, SlotW, 11, new Color(0.8f, 0.9f, 1f, 0.85f));
        }
    }
}
