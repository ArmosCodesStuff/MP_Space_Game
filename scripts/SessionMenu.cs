using Godot;

// Host, join, or play offline -- folded behind one button so it does not take up
// the screen. The button always shows where you stand (offline, hosting, guest)
// and opens the options. Offline is the same code path as hosting with no peers,
// so nothing can behave differently between them.
public partial class SessionMenu : CanvasLayer
{
    private LineEdit _addr;
    private Label _status;
    private Button _copy;
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
        // one press a second: starting or stopping a session is not free, and nothing
        // should be able to hammer it
        _hostBtn = Btn("HOST THIS WORLD", () => Limited(() => Net.I?.Host())); _hostBtn.Name = "Host";
        _joinBtn = Btn("JOIN", () => Limited(DoJoin)); _joinBtn.Name = "Join";
        _offBtn = Btn("PLAY OFFLINE", () => Limited(() => Net.I?.GoOffline())); _offBtn.Name = "Offline";
        _options.AddChild(_hostBtn); _options.AddChild(_joinBtn); _options.AddChild(_offBtn);

        _status = new Label { Text = Net.I?.LastStatus ?? "", AutowrapMode = TextServer.AutowrapMode.WordSmart,
                              CustomMinimumSize = new Vector2(320, 0) };
        _options.AddChild(_status);
        // the address friends should type, one click to the clipboard
        _copy = Btn("COPY ADDRESS", () =>
        {
            var a = Net.I?.Reachability is Net.Reach.Internet or Net.Reach.Manual ? Net.I.InternetAddress
                  : !string.IsNullOrEmpty(Net.I?.TailnetAddress) ? Net.I.TailnetAddress      // friends on the tailnet
                  : Net.I?.LanAddress;
            if (!string.IsNullOrEmpty(a)) DisplayServer.ClipboardSet(a);
        });
        _copy.Name = "CopyAddress";
        _options.AddChild(_copy);
        // the address friends in other cities need -- hidden until clicked
        _reveal = Btn("", () => { _revealed = !_revealed; Refresh(); });
        _reveal.Name = "RevealAddress";
        _options.AddChild(_reveal);
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
    public override void _Process(double delta)
    {
        _now += delta;
        bool locked = Locked;                                   // the 1 s rate limit, shown on the buttons
        foreach (var btn in new[] { _hostBtn, _joinBtn, _offBtn }) if (btn != null) btn.Disabled = locked;
        if (_copy != null) _copy.Visible = Net.I != null && Net.IsHost && Net.IsOnline && Net.I.Reachability != Net.Reach.Checking;
        if (_reveal != null)
        {
            bool shown = Net.I != null && Net.IsHost && Net.IsOnline && Net.I.Reachability is (Net.Reach.Internet or Net.Reach.Manual);
            _reveal.Visible = shown;
            if (!shown) _revealed = false;
            _reveal.Text = _revealed ? $"Friends elsewhere join: {Net.I?.InternetAddress}   (click to hide)"
                                     : "Friends elsewhere join: \u2022\u2022\u2022.\u2022\u2022\u2022.\u2022\u2022\u2022.\u2022\u2022\u2022   (click to reveal)";
        }
    }
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

    private Button _hostBtn, _joinBtn, _offBtn, _reveal;
    private bool _revealed;
    public const double PressGap = 1.0;
    private double _now, _lockedUntil;                          // game time: in play, the same as real time
    public bool Locked => _now < _lockedUntil;
    private void Limited(System.Action a)
    {
        if (Locked) return;
        _lockedUntil = _now + PressGap;
        a();
    }
    private static Button Btn(string text, System.Action onPress)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += onPress;
        return b;
    }
}
