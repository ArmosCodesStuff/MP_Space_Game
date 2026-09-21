using Godot;

// PILOT (L): level, EXP (1000 a level), points, and the four upgrades they buy. Each row's BUY costs
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
        Ui.Panelise(this);
        var col = Ui.VBox(12); AddChild(col);
        _head = Ui.Lbl("", Ui.Title, Ui.Accent); col.AddChild(_head);
        // Level, bar and points are one block: they are the same number said three ways.
        var lvl = Ui.VBox(6);
        _exp = Ui.Lbl("", Ui.Body); lvl.AddChild(_exp);
        _bar = new ProgressBar { CustomMinimumSize = new Vector2(440, 8), ShowPercentage = false, MaxValue = 1 }; lvl.AddChild(_bar);
        _points = Ui.Lbl("", Ui.Body, Ui.Accent); lvl.AddChild(_points);
        col.AddChild(Ui.CardWrap(lvl));
        col.AddChild(Ui.Heading("Upgrades"));
        for (int i = 0; i < Progression.All.Length; i++)
        {
            int k = i;
            var row = Ui.HBox(12, "Pilot_" + Progression.All[i].Id);
            _info[i] = new Label { CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
            _buy[i] = new Button { Name = "Buy", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(134, 34), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _buy[i].Pressed += () => { if (Progression.TryBuy(Progression.All[k].Id)) Hub.PilotChanged(); };
            row.AddChild(_info[i]); row.AddChild(_buy[i]); col.AddChild(Ui.CardWrap(row));
        }
        var note = Ui.Lbl("EXP from boss kills: 200 x the boss's level / yours, +250 the first time you beat a level, +100 for completing. Every pilot earns their own.",
                          Ui.Small, Ui.Dim);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart; note.CustomMinimumSize = new Vector2(440, 0);
        col.AddChild(note);
        col.AddChild(Ui.Lbl("L or Esc closes.", Ui.Small, Ui.Dim));
    }

    public override void _Process(double delta)
    {
        int need = Progression.ExpToNext;
        Ui.SetText(_head, $"PILOT  ·  {Character.Name}  ·  LEVEL {Character.Level}");
        Ui.SetText(_exp, $"EXP {Character.Exp} / {need} to level {Character.Level + 1}");
        _bar.Value = (double)Character.Exp / need;
        Ui.SetText(_points, $"{Character.Points} point(s) to spend");
        for (int i = 0; i < Progression.All.Length; i++)
        {
            var u = Progression.All[i]; int n = Character.Bought[i], cost = Progression.Cost(n);
            double per = u.Id == "turn" ? 1 : u.Per;                    // shown in degrees
            Ui.SetText(_info[i], $"{u.Name}  ·  Lv {n}   (+{per * n:0.#} {u.Unit} so far; next +{per:0.#})");
            Ui.SetText(_buy[i], $"BUY  {cost} pt");
            _buy[i].Disabled = Character.Points < cost || n >= Progression.MaxPerUpgrade;
        }
    }
}
