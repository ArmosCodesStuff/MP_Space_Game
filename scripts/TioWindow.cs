using Godot;
using System.Linq;

// WARP TO TARGET -- the Threat Intelligence Operations window (left-click the TIO).
// A bounty, and the party (everyone in the session) with each pilot's READY. READY
// flies your ship to where the portal opens; once EVERY pilot is ready the portal
// opens (a 3 s bar at the TIO's top right), and once every ship is at it, the party
// goes through together.
public partial class TioWindow : PanelContainer
{
    public Hub Hub;
    private Label _party, _status;
    private Button _ready, _down, _up;
    private Label _tier, _bounty;

    public override void _Ready()
    {
        Name = "TioWindow";
        Position = new Vector2(360, 92);
        AddThemeStyleboxOverride("panel", Ui.PanelStyle(10, 1f));
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 6); AddChild(col);
        var head = new Label { Text = "WARP TO TARGET" }; head.AddThemeFontSizeOverride("font_size", 20); col.AddChild(head);
        col.AddChild(new Label { Text = "Threat Intelligence Operations", Modulate = new Color(1, 1, 1, 0.6f) });
        _bounty = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0) };
        col.AddChild(_bounty);
        var diff = new HBoxContainer { Name = "Difficulty" }; diff.AddThemeConstantOverride("separation", 8); col.AddChild(diff);
        diff.AddChild(new Label { Text = "DIFFICULTY", Modulate = new Color(0.55f, 0.8f, 1f) });
        _down = new Button { Name = "LevelDown", Text = "◀", FocusMode = FocusModeEnum.None }; _down.Pressed += () => Hub.SelectLevel(Missions.Level - 1);
        _tier = new Label { CustomMinimumSize = new Vector2(230, 0), HorizontalAlignment = HorizontalAlignment.Center };
        _up = new Button { Name = "LevelUp", Text = "▶", FocusMode = FocusModeEnum.None }; _up.Pressed += () => Hub.SelectLevel(Missions.Level + 1);
        diff.AddChild(_down); diff.AddChild(_tier); diff.AddChild(_up);
        col.AddChild(new Label { Text = "PARTY  (everyone in the session)", Modulate = new Color(0.55f, 0.8f, 1f) });
        _party = new Label(); col.AddChild(_party);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); col.AddChild(row);
        _ready = new Button { Name = "Ready", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(215, 36) };
        _ready.Pressed += () => Hub.SetMyReady(!Hub.IsReady(Net.LocalId));
        row.AddChild(_ready);
        _status = new Label(); col.AddChild(_status);
        col.AddChild(new Label { Text = "Esc closes.", Modulate = new Color(1, 1, 1, 0.45f) });
    }

    public override void _Process(double delta)
    {
        if (Hub == null) return;
        int top = Missions.Unlocked(Missions.Current.Id);
        int lv = Missions.Level, party = System.Math.Max(1, Hub.PartySize);
        Ui.SetText(_tier, $"LEVEL {lv}  ·  ×{Missions.S(lv):0.00}" + (lv == top ? "  (newest)" : ""));
        _down.Disabled = !Net.IsHost || lv <= 1 || Hub.Mission != Hub.MissionState.Idle;
        _up.Disabled = !Net.IsHost || lv >= top || Hub.Mission != Hub.MissionState.Idle;
        bool first = !(Character.BossCleared.TryGetValue(Missions.Current.Id, out var cl) && cl.Contains(lv));
        Ui.SetText(_bounty, $"BOUNTY  ·  {Missions.BossName}  ·  party of {party}\n"
                     + $"You: {Missions.KillExpFor(lv, Character.Level)} EXP for the kill (your level {Character.Level})"
                     + (first ? $" + {Missions.FirstClearExp} first clear" : "") + $" + {Missions.CompletionExp} completing, "
                     + $"{Missions.BountyEach(lv, party):0} credits each.");
        Ui.SetText(_party, string.Join("\n", Hub.PartyIds.OrderBy(i => i).Select(i => $"  {Hub.PilotName(i)}   {(Hub.IsReady(i) ? "READY" : "not ready")}")));
        bool mine = Hub.IsReady(Net.LocalId);
        Ui.SetText(_ready, mine ? "READY  ✓" : "READY");
        int waiting = Hub.PartyIds.Count(i => !Hub.IsReady(i));
        Ui.SetText(_status, Hub.Mission switch
        {
            Hub.MissionState.Opening    => $"Opening the portal…  {Hub.MissionT:0.0} / {Hub.PortalOpenTime:0} s",
            Hub.MissionState.PortalOpen => "The portal is open: the party goes through when every ship is at it.",
            _ => waiting > 0 ? $"Waiting for {waiting} pilot(s) to press READY. READY flies you to the portal." : "Everyone is ready.",
        });
    }
}
