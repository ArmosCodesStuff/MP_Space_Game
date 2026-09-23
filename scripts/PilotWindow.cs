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
            _info[i] = new Label { CustomMinimumSize = new Vector2(440, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                                   VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _buy[i] = new Button { Name = "Buy", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(134, 34), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _buy[i].Pressed += () => { if (Progression.TryBuy(Progression.All[k].Id)) Hub.PilotChanged(); };
            row.AddChild(_info[i]); row.AddChild(_buy[i]); col.AddChild(Ui.CardWrap(row));
        }
        // The three figures are the mission table's (Missions), read live: they were written out
        // here as well, and the TIO window reads the same three from the table, so the same numbers
        // sat in two places with nothing to keep them together.
        var note = Ui.Lbl($"EXP from boss kills: {Missions.KillExp} x the boss's level / yours, +{Missions.FirstClearExp} the first time you beat a level, "
                        + $"+{Missions.CompletionExp} for completing. Every pilot earns their own.",
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
            Ui.SetText(_info[i], $"{u.Name}  ·  Lv {n}" + Buys(u, n));
            Ui.SetText(_buy[i], $"BUY  {cost} pt");
            _buy[i].Disabled = Character.Points < cost || n >= Progression.MaxPerUpgrade;
        }
    }

    // WHAT THIS ROW HAS BOUGHT ON THIS HULL, and what the next point buys: one line per stat the
    // purchase really moves, read from the same place the purchase reads (Progression.StatsFor),
    // named and printed in that stat's own units (AllStats).
    // It printed the row's own `Per` before, with a `u.Id == "turn"` special case to turn radians
    // into degrees. On Weapons that was a lie: StatsFor ignores Per there and asks the class
    // (Classes.Damage), so a sniper's point is +1 on its gun AND +7.5 on its railgun, and a
    // carrier's fighters take nothing at all -- while the row said "next +1" for all of it.
    // The unit conversion is gone with the special case: a stat prints in the units the STATS tab
    // prints it in, so the two windows cannot disagree.
    private static string Buys(Progression.Upgrade u, int n)
    {
        string s = "";
        foreach (var (stat, per) in Progression.StatsFor(u, Character.Class))
            // A SHARE READS AS A PERCENTAGE and an amount in the stat's own units, because
            // "+0.03 damage" is not what three percent of a weapon looks like to anybody.
            s += u.Share
                ? "\n    " + AllStats.Said(stat) + "   " + (per * n * 100).ToString("+0.#;-0.#;0") + "% now, " + (per * 100).ToString("+0.#;-0.#;0") + "% next"
                : "\n    " + AllStats.Said(stat) + "   +" + AllStats.Fmt(stat, per * n) + " now, +" + AllStats.Fmt(stat, per) + " next";
        return s.Length > 0 ? s : "\n    nothing on this hull";
    }
}
