using Godot;
using System.Linq;

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
        // Above the HUD's buttons: opened, this panel reaches over BASE and PILOT, which drew on top
        // of it and took the clicks meant for it.
        Layer = 2;
        var root = Ui.VBox(8);
        var panel = Ui.Wrap(root);                   // on a panel, like every HUD element
        panel.Position = new Vector2(10, 48);
        panel.MouseFilter = Control.MouseFilterEnum.Pass;
        AddChild(panel);

        _toggle = new Button { Name = "NetToggle", FocusMode = Control.FocusModeEnum.None, ToggleMode = true,
                               Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(220, 0) };
        _toggle.Toggled += on => { _options.Visible = on; if (!on) _addr.ReleaseFocus(); Refresh(); };
        root.AddChild(_toggle);

        _options = Ui.VBox(8, "NetOptions");
        _options.Visible = false;
        root.AddChild(_options);

        _addr = new LineEdit { PlaceholderText = "host IP, or IP:port", CustomMinimumSize = new Vector2(300, 0) };
        // Enter joins -- under the same one-press-a-second limit as the button -- and either way
        // the box lets go of the keyboard so the helm works again.
        _addr.TextSubmitted += t => { _addr.ReleaseFocus(); Limited(DoJoin); };
        _options.AddChild(_addr);

        // FocusMode None: a clicked button must not keep keyboard focus, or Space/Enter
        // would press it again mid-flight.
        // one press a second: starting or stopping a session is not free, and nothing
        // should be able to hammer it
        _hostBtn = Ui.Btn("HOST THIS WORLD", () => Limited(() => Net.I?.Host()), "Host");
        _joinBtn = Ui.Btn("JOIN", () => Limited(DoJoin), "Join");
        _offBtn = Ui.Btn("PLAY OFFLINE", () => Limited(() => Net.I?.GoOffline()), "Offline");
        _options.AddChild(_hostBtn); _options.AddChild(_joinBtn); _options.AddChild(_offBtn);
        _sessionBtns = new[] { _hostBtn, _joinBtn, _offBtn };

        _status = Ui.Lbl(Net.I?.LastStatus ?? "", Ui.Small, Ui.Dim);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(320, 0);
        _options.AddChild(_status);
        // the address friends should type, one click to the clipboard: the internet one if there
        // is one, else a virtual network's, else IPv6, else the local network's
        _copy = Ui.Btn("COPY ADDRESS", () =>
        {
            var n = Net.I;
            if (n == null) return;
            var a = new[] { n.InternetAddress, n.OverlayAddresses.Select(o => o.address).FirstOrDefault(), n.Ipv6Address, n.LanAddress }
                    .FirstOrDefault(x => !string.IsNullOrEmpty(x));
            if (a != null) DisplayServer.ClipboardSet(a);
        });
        _copy.Name = "CopyAddress";
        _options.AddChild(_copy);
        // the address friends in other cities need -- hidden until clicked
        _reveal = Ui.Btn("", () => { _revealed = !_revealed; Refresh(); });
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
        foreach (var btn in _sessionBtns) btn.Disabled = locked;
        // HOST while hosting would drop every guest to start the same session again
        _hostBtn.Disabled |= Net.IsHost && Net.IsOnline;
        var n = Net.I;
        bool hosting = n != null && Net.IsHost && Net.IsOnline;
        _copy.Visible = hosting && n.Reachability != Net.Reach.Checking;
        // The addresses friends elsewhere need, hidden until clicked: the internet one, and this
        // PC's IPv6 one when it has one. Ui.SetText: only when changed -- MSDF glyphs re-shape.
        bool shown = hosting && n.Reachability != Net.Reach.Checking && (n.InternetAddress.Length > 0 || n.Ipv6Address.Length > 0);
        _reveal.Visible = shown;
        if (!shown) _revealed = false;
        else
        {
            string both = string.Join("   ·   IPv6 ", new[] { n.InternetAddress, n.Ipv6Address }.Where(a => a.Length > 0));
            Ui.SetText(_reveal, _revealed ? $"Friends elsewhere join: {both}   (click to hide)"
                                          : "Friends elsewhere join: \u2022\u2022\u2022.\u2022\u2022\u2022.\u2022\u2022\u2022.\u2022\u2022\u2022   (click to reveal)");
        }
    }
    private void OnPeers(int _) => Refresh();

    // The folded button's label: what it is, and where you stand.
    private void Refresh()
    {
        if (_toggle == null) return;
        string where = !Net.IsOnline ? (Net.I != null && Net.I.Connecting ? "connecting…" : "offline")
                     : Net.IsHost ? $"hosting · {Net.I.Players.Count}" : $"guest · {Net.I.Players.Count}";
        _toggle.Text = $"{(_toggle.ButtonPressed ? "▾" : "▸")}  MULTIPLAYER  ({where})";
    }

    private void DoJoin() => Net.I?.Join(_addr.Text);

    private Button _hostBtn, _joinBtn, _offBtn, _reveal;
    private Button[] _sessionBtns;
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
}
