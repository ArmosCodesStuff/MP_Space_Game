using Godot;
using System.Collections.Generic;
using System.Linq;

// The BASE menu (B, or the BASE button), in tabs:
//   MINERS, SALVAGERS, HAULER : their upgrades. A row shows the level, the number it
//     changes (now -> next), and the price. +10% upgrades cost 1.25x per level; step
//     upgrades (another ship, another pod, +7% EVASION) cost 2x per level and stop at
//     MAX; a switch (AUTO-SELL) reads OFF / ON. BUY greys out until affordable, and
//     reads LOCKED until the base owner has beaten the boss a row asks for. A guest's
//     BUY is a request.
//   REFIT : the only way to change class, name or colours. RESET costs 10% of your
//     ore, salvage and credits, and asks for a second click before it pays.
public partial class BasePanel : PanelContainer
{
    public Hub Hub;
    private string _tab = "MINERS";
    private Label _fleet;          // this category's investment, its rebuild cost, and what is being rebuilt
    private VBoxContainer _body;
    private Label _credits;
    private readonly Dictionary<string, (Label info, Button buy)> _rows = new();
    private Label _resetCost; private Button _reset;
    private double _armed;                    // seconds the RESET confirmation stays live

    private Yard Y => Hub.Yard;

    public override void _Ready()
    {
        Name = "BasePanel";
        Position = new Vector2(360, 92);   // clear of the multiplayer panel, even when it is open (it reaches x ~283)
        Ui.Panelise(this);   // fully opaque: the world (a hull bar) showed through as a faint line
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        col.AddThemeConstantOverride("separation", 12);
        AddChild(col);
        // Title line: the name on the left, the one number you spend from on the right.
        var head = new HBoxContainer();
        head.AddChild(Ui.Lbl("BASE", Ui.Title, Ui.Accent));
        _credits = Ui.Lbl("", Ui.Body, Ui.Dim);
        _credits.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _credits.HorizontalAlignment = HorizontalAlignment.Right;
        _credits.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(_credits);
        col.AddChild(head);

        var tabs = Ui.HBox(6);
        var group = new ButtonGroup();
        foreach (var t in Economy.Tabs.Append("REFIT"))
        {
            var b = Ui.Tab(t, group, t == _tab);
            var tab = t; b.Toggled += on => { if (on) ShowTab(tab); };
            tabs.AddChild(b);
        }
        col.AddChild(tabs);
        _body = Ui.VBox(8);
        col.AddChild(_body);
        col.AddChild(Ui.Lbl("B or Esc closes.", Ui.Small, Ui.Dim));
        ShowTab(_tab);
    }

    public void ShowTab(string tab)
    {
        _tab = tab; _rows.Clear(); _reset = null;
        Ui.Clear(_body);
        if (tab == "REFIT")
        {
            _body.AddChild(Ui.Heading("Refit"));
            var note = Ui.Lbl("Change class, name or colours. Costs 10% of your ore, salvage and credits.", Ui.Small, Ui.Dim);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _body.AddChild(note);
            _resetCost = Ui.Lbl("", Ui.Body, Ui.Warn); _body.AddChild(Ui.CardWrap(_resetCost));
            _reset = new Button { Name = "Reset", Text = "RESET", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
            _reset.Pressed += () => { if (_armed <= 0) _armed = 4.0; else { _armed = 0; Hub.ResetShip(); } };
            _body.AddChild(_reset);
            return;
        }
        _body.AddChild(Ui.Heading(tab));
        _fleet = Ui.Lbl("", Ui.Small, Ui.Dim); _fleet.Name = "Fleet";
        _fleet.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(Ui.CardWrap(_fleet));
        foreach (var u in Economy.All.Where(u => u.Tab == tab))
        {
            // One card per upgrade: name and level on the first line, what the money buys on the
            // second, the price on the button. The old version was four bare labels a row and read
            // as a paragraph.
            var row = Ui.HBox(12, "Up_" + u.Id);
            var info = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
            var id = u.Id;
            // centred at its own 34 px height rather than stretched to the two-line label
            var buy = new Button { Name = "Buy", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(128, 34),
                                   SizeFlagsVertical = SizeFlags.ShrinkCenter };
            buy.Pressed += () => Y.BuyUpgrade(id);
            row.AddChild(info); row.AddChild(buy);
            _body.AddChild(Ui.CardWrap(row));
            _rows[u.Id] = (info, buy);
        }
        // The two cost curves come off the table that owns them (Growth, below), and the line no
        // longer calls every Percent row "+10%": two HAULER rows are not (+2% engines, +5% point
        // defence). Each row's own line says its share.
        _body.AddChild(Ui.Lbl($"Percent upgrades cost {Growth(Economy.Kind.Percent)} per level, each by the share its own line names."
                            + $"  Step upgrades cost {Growth(Economy.Kind.Count)} per level; each row shows its cap.",
                              Ui.Small, Ui.Dim with { A = 0.75f }));
    }

    public override void _Process(double delta)
    {
        if (_armed > 0) _armed -= delta;
        Ui.SetText(_credits, $"{Y.Credits:0} cr");
        if (IsInstanceValid(_fleet) && _tab != "REFIT")
        {
            string lost = string.Join("", Y.Fleet.Where(s => s.Category == _tab && s.Lost)
                                                .Select(s => Rebuilding(s.Label, s.RebuildIn, s.WaitingForCredits, _tab)));
            Ui.SetText(_fleet, $"Invested {Y.Invested(_tab):0} cr  ·  a rebuild costs {Y.RebuildCost(_tab):0} cr ({Economy.RebuildShare * 100:0}%)" + lost);
        }
        foreach (var (id, (info, buy)) in _rows)
        {
            var u = Economy.ById(id); int lv = Y.Level(id);
            bool max = Economy.Maxed(u, lv), sw = u.Kind == Economy.Kind.Unlock;
            bool notMine = u.OwnerOnly && !Y.IsMyOwnBase, locked = notMine || Y.OwnerBoss < u.NeedsBoss;
            string gate = notMine ? "  -- the base owner's to buy" : locked ? $"  -- beat the level-{u.NeedsBoss} boss first" : "";
            if (sw) Ui.SetText(info, $"{u.Name}  ·  {(lv >= 1 ? "ON" : "OFF → ON")}\n({u.Blurb}){gate}");
            else
            {
                string now = $"{Economy.Value(u, lv):0.#}", next = max ? "MAX" : $"{Economy.Value(u, lv + 1):0.#}";
                Ui.SetText(info, $"{u.Name}  ·  Lv {lv}\n{now} → {next} {u.Unit}   ({u.Blurb}){gate}");
            }
            double cost = Economy.Cost(u, lv);
            Ui.SetText(buy, max ? (sw ? "ON" : "MAX") : locked ? "LOCKED" : $"BUY  {cost:0} cr");
            buy.Disabled = max || locked || Y.Credits < cost;
        }
        if (_reset != null)
        {
            var (o, s, c) = Y.ResetCost();
            Ui.SetText(_resetCost, $"Cost now: {o:0} ore, {s:0} salvage, {c:0} credits");
            Ui.SetText(_reset, _armed > 0 ? "CLICK AGAIN TO PAY AND RESET" : "RESET");
            _reset.Disabled = Hub.CreatorOpen;
        }
    }


    string Rebuilding(string who, double inS, bool waiting, string tab) =>
        waiting ? $"\n  {who} lost: rebuild waiting for {Y.RebuildCost(tab):0} cr" : $"\n  {who} lost: rebuilt in {inS:0} s";

    // What a level of this KIND of upgrade costs, as a multiple: the table's own price for one of
    // them at level 1 over its price at level 0. The footnote used to state both curves as text.
    static string Growth(Economy.Kind kind)
    {
        var u = Economy.All.First(x => x.Kind == kind);
        return $"{Economy.Cost(u, 1) / Economy.Cost(u, 0):0.##}×";
    }
}
