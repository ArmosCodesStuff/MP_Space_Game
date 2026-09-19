using Godot;

// PILOT (L): level, EXP, points, and the four upgrades they buy. Each row's BUY costs
// one more point than its last purchase (1, 2, 3, ...). Purchases save at once and
// refit the ship; the host is told with the pilot's identity.
public partial class PilotWindow : PanelContainer
{
    public Hub Hub;
    private Label _head, _exp, _points;
    private ProgressBar _bar;
    private readonly Label[] _info = new Label[Progression.All.Length];
    private readonly Button[] _buy = new Button[Progression.All.Length];

    public override void _Ready()
    {
        Name = "PilotWindow";
        Position = new Vector2(360, 92);
        AddThemeStyleboxOverride("panel", Ui.PanelStyle(10, 1f));
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 6); AddChild(col);
        _head = new Label(); _head.AddThemeFontSizeOverride("font_size", 20); col.AddChild(_head);
        _exp = new Label(); col.AddChild(_exp);
        _bar = new ProgressBar { CustomMinimumSize = new Vector2(440, 10), ShowPercentage = false, MaxValue = 1 }; col.AddChild(_bar);
        _points = new Label(); col.AddChild(_points);
        col.AddChild(new Label { Text = "EXP: completing missions and killing bosses, shared by everyone in the session.",
                                 AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0), Modulate = new Color(1, 1, 1, 0.6f) });
        for (int i = 0; i < Progression.All.Length; i++)
        {
            int k = i;
            var row = new HBoxContainer { Name = "Pilot_" + Progression.All[i].Id }; row.AddThemeConstantOverride("separation", 10);
            _info[i] = new Label { CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _buy[i] = new Button { Name = "Buy", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(130, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _buy[i].Pressed += () => { if (Progression.TryBuy(Progression.All[k].Id)) Hub.PilotChanged(); };
            row.AddChild(_info[i]); row.AddChild(_buy[i]); col.AddChild(row);
        }
        col.AddChild(new Label { Text = "L or Esc closes.", Modulate = new Color(1, 1, 1, 0.45f) });
    }

    public override void _Process(double delta)
    {
        int need = Progression.ExpToNext(Character.Level);
        _head.Text = $"PILOT  ·  {Character.Name}  ·  LEVEL {Character.Level}";
        _exp.Text = $"EXP {Character.Exp} / {need} to level {Character.Level + 1}";
        _bar.Value = (double)Character.Exp / need;
        _points.Text = $"Points to spend: {Character.Points}";
        for (int i = 0; i < Progression.All.Length; i++)
        {
            var u = Progression.All[i]; int n = Character.Bought[i], cost = Progression.Cost(n);
            double per = u.Id == "turn" ? 1 : u.Per;                    // shown in degrees
            _info[i].Text = $"{u.Name}  ·  Lv {n}   (+{per * n:0.#} {u.Unit} so far; next +{per:0.#})";
            _buy[i].Text = $"BUY  {cost} pt";
            _buy[i].Disabled = Character.Points < cost || n >= Progression.MaxPerUpgrade;
        }
    }
}
