using Godot;
using System.Collections.Generic;
using System.Linq;

// The BASE menu (B, or the BASE button), in tabs:
//   MINERS, SALVAGERS, HAULER : their upgrades. A row shows the level, the number it
//     changes (now -> next), and the price. +10% upgrades cost 1.25x per level; +1
//     upgrades (another ship, another pod) cost 2x per level and stop at MAX (5
//     bought). BUY greys out until affordable. A guest's BUY is a request.
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
        AddThemeStyleboxOverride("panel", Ui.PanelStyle(10, 1f));   // fully opaque: the world (a hull bar) showed through as a faint line
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        col.AddThemeConstantOverride("separation", 6);
        AddChild(col);
        var head = new Label { Text = "BASE" }; head.AddThemeFontSizeOverride("font_size", 20);
        col.AddChild(head);

        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 4);
        var group = new ButtonGroup();
        foreach (var t in Economy.Tabs.Append("REFIT"))
        {
            var b = new Button { Text = t, Name = "Tab_" + t, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None,
                                 ButtonPressed = t == _tab };
            var tab = t; b.Toggled += on => { if (on) ShowTab(tab); };
            tabs.AddChild(b);
        }
        col.AddChild(tabs);
        _credits = new Label(); col.AddChild(_credits);
        _body = new VBoxContainer(); _body.AddThemeConstantOverride("separation", 6);
        col.AddChild(_body);
        col.AddChild(new Label { Text = "B or Esc closes.", Modulate = new Color(1, 1, 1, 0.45f) });
        ShowTab(_tab);
    }

    public void ShowTab(string tab)
    {
        _tab = tab; _rows.Clear(); _reset = null;
        foreach (var c in _body.GetChildren()) { _body.RemoveChild(c); c.QueueFree(); }
        if (tab == "REFIT")
        {
            _body.AddChild(new Label { Text = "Change class, name or colours. Costs 10% of your ore, salvage and credits.",
                                       AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.7f) });
            _resetCost = new Label(); _body.AddChild(_resetCost);
            _reset = new Button { Name = "Reset", Text = "RESET", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 36) };
            _reset.Pressed += () => { if (_armed <= 0) _armed = 4.0; else { _armed = 0; Hub.ResetShip(); } };
            _body.AddChild(_reset);
            return;
        }
        _fleet = new Label { Name = "Fleet", Modulate = new Color(0.8f, 0.88f, 1f) }; _body.AddChild(_fleet);
        _body.AddChild(new Label { Text = "+10% upgrades cost 1.25× per level.  +1 upgrades cost 2× per level; each row shows its cap.",
                                   Modulate = new Color(1, 1, 1, 0.55f) });
        foreach (var u in Economy.All.Where(u => u.Tab == tab))
        {
            var row = new HBoxContainer { Name = "Up_" + u.Id }; row.AddThemeConstantOverride("separation", 10);
            var info = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var id = u.Id;
            // centred at its own 32 px height rather than stretched to the two-line label
            var buy = new Button { Name = "Buy", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 32),
                                   SizeFlagsVertical = SizeFlags.ShrinkCenter };
            buy.Pressed += () => Y.BuyUpgrade(id);
            row.AddChild(info); row.AddChild(buy);
            _body.AddChild(row);
            _rows[u.Id] = (info, buy);
        }
    }

    public override void _Process(double delta)
    {
        if (_armed > 0) _armed -= delta;
        _credits.Text = $"Credits: {Y.Credits:0}";
        if (IsInstanceValid(_fleet) && _tab != "REFIT")
        {
            string lost = _tab == "HAULER"
                ? (Y.Hauler != null && Y.Hauler.State == Hauler.St.Destroyed ? Rebuilding("Hauler", Y.Hauler.RebuildIn, Y.Hauler.WaitingForCredits, _tab) : "")
                : string.Join("", Y.Gatherers.Where(g => g.Category == _tab && g.State == Gatherer.St.Destroyed)
                                            .Select(g => Rebuilding($"{(g.Kind == GatherKind.Miner ? "Miner" : "Salvager")} {g.Index + 1}", g.RebuildIn, g.WaitingForCredits, _tab)));
            _fleet.Text = $"Invested so far: {Y.Invested(_tab):0} cr  ·  a rebuild costs {Y.RebuildCost(_tab):0} cr (10%)" + lost;
        }
        foreach (var (id, (info, buy)) in _rows)
        {
            var u = Economy.ById(id); int lv = Y.Level(id);
            bool max = Economy.Maxed(u, lv);
            string now = $"{Economy.Value(u, lv):0.#}", next = max ? "MAX" : $"{Economy.Value(u, lv + 1):0.#}";
            info.Text = $"{u.Name}  ·  Lv {lv}\n{now} → {next} {u.Unit}   ({u.Blurb})";
            double cost = Economy.Cost(u, lv);
            buy.Text = max ? "MAX" : $"BUY  {cost:0} cr";
            buy.Disabled = max || Y.Credits < cost;
        }
        if (_reset != null)
        {
            var (o, s, c) = Y.ResetCost();
            _resetCost.Text = $"Cost now: {o:0} ore, {s:0} salvage, {c:0} credits";
            _reset.Text = _armed > 0 ? "CLICK AGAIN TO PAY AND RESET" : "RESET";
            _reset.Disabled = Hub.CreatorOpen;
        }
    }

    string Rebuilding(string who, double inS, bool waiting, string tab) =>
        waiting ? $"\n  {who} lost: rebuild waiting for {Y.RebuildCost(tab):0} cr" : $"\n  {who} lost: rebuilt in {inS:0} s";
}
