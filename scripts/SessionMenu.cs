using Godot;

// Host, join, or play offline -- folded behind one button so it does not take up
// the screen. The button always shows where you stand (offline, hosting, guest)
// and opens the options. Offline is the same code path as hosting with no peers,
// so nothing can behave differently between them.
public partial class SessionMenu : CanvasLayer
{
    private LineEdit _addr;
    private Label _status;
    private Button _toggle;
    private VBoxContainer _options;

    public override void _Ready()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        var panel = Ui.Wrap(root);                   // on a panel, like every HUD element
        panel.Position = new Vector2(10, 48);
        panel.MouseFilter = Control.MouseFilterEnum.Pass;
        AddChild(panel);

        _toggle = new Button { Name = "NetToggle", FocusMode = Control.FocusModeEnum.None, ToggleMode = true,
                               Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(220, 0) };
        _toggle.Toggled += on => { _options.Visible = on; if (!on) _addr.ReleaseFocus(); Refresh(); };
        root.AddChild(_toggle);

        _options = new VBoxContainer { Name = "NetOptions", Visible = false };
        root.AddChild(_options);

        _addr = new LineEdit { PlaceholderText = "host IP, or IP:port", CustomMinimumSize = new Vector2(300, 0) };
        // Enter joins, and either way the box lets go of the keyboard so the helm works again.
        _addr.TextSubmitted += t => { _addr.ReleaseFocus(); DoJoin(); };
        _options.AddChild(_addr);

        // FocusMode None: a clicked button must not keep keyboard focus, or Space/Enter
        // would press it again mid-flight.
        _options.AddChild(Btn("HOST THIS WORLD", () => Net.I?.Host()));
        _options.AddChild(Btn("JOIN",            DoJoin));
        _options.AddChild(Btn("PLAY OFFLINE",    () => Net.I?.GoOffline()));

        _status = new Label { Text = Net.I?.LastStatus ?? "" };
        _options.AddChild(_status);
        if (Net.I != null) { Net.I.Status += OnStatus; Net.I.SessionChanged += Refresh; Net.I.PlayerJoined += OnPeers; Net.I.PlayerLeft += OnPeers; }
        Refresh();
    }

    // Net is an autoload and outlives this menu. Unsubscribe, or the next status
    // line is written to a freed Label.
    public override void _ExitTree()
    {
        if (Net.I != null) { Net.I.Status -= OnStatus; Net.I.SessionChanged -= Refresh; Net.I.PlayerJoined -= OnPeers; Net.I.PlayerLeft -= OnPeers; }
    }

    private void OnStatus(string s) { _status.Text = s; Refresh(); }
    private void OnPeers(int _) => Refresh();

    // The folded button's label: what it is, and where you stand.
    private void Refresh()
    {
        if (_toggle == null) return;
        string where = !Net.IsOnline ? (Net.I != null && !Net.IsHost ? "connecting…" : "offline")
                     : Net.IsHost ? $"hosting · {Net.I.Players.Count}" : $"guest · {Net.I.Players.Count}";
        _toggle.Text = $"{(_toggle.ButtonPressed ? "▾" : "▸")}  MULTIPLAYER  ({where})";
    }

    private void DoJoin() => Net.I?.Join(_addr.Text);

    private static Button Btn(string text, System.Action onPress)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += onPress;
        return b;
    }
}
