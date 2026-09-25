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
        _toggle.Toggled += on => { _options.Visible = on; if (!on) _addr.ReleaseFocus(); else Hub.I?.Hints?.Meet("multiplayer"); Refresh(); };
        root.AddChild(_toggle);
        // After three failed attempts to get back to a lost host: one more, on the button. Right under
        // the folded panel's title, so it is there without opening anything.
        _reconnect = Ui.Btn("RECONNECT", () => Limited(() => Net.I?.Reconnect()), "Reconnect");
        _reconnect.Visible = false;
        root.AddChild(_reconnect);

        _options = Ui.VBox(8, "NetOptions");
        _options.Visible = false;
        root.AddChild(_options);

        // THE JOIN BOX takes a friend's invite (the whole message is fine) or a host's typed address.
        _addr = new LineEdit { Name = "JoinBox", PlaceholderText = "paste an invite, or type host IP:port", CustomMinimumSize = new Vector2(300, 0) };
        // Enter joins -- under the same one-press-a-second limit as the button -- and either way
        // the box lets go of the keyboard so the helm works again.
        _addr.TextSubmitted += t => { _addr.ReleaseFocus(); Limited(DoJoin); };
        _options.AddChild(_addr);
        // NO PLUGIN, NO SESSION: without the WebRTC library HOST and JOIN are shut, and this says
        // why and what fixes it. Offline play is untouched.
        _missing = Ui.Lbl(Link.MissingText, Ui.Small, Ui.Dim);
        _missing.Name = "PluginMissing";
        _missing.Visible = !Link.Available;
        _missing.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _missing.CustomMinimumSize = new Vector2(320, 0);
        _options.AddChild(_missing);

        // FocusMode None: a clicked button must not keep keyboard focus, or Space/Enter
        // would press it again mid-flight.
        // one press a second: starting or stopping a session is not free, and nothing
        // should be able to hammer it
        _hostBtn = Ui.Btn("HOST THIS WORLD", () => Limited(() => { if (Link.Available) Net.I?.Host(); }), "Host");
        _joinBtn = Ui.Btn("JOIN", () => Limited(DoJoin), "Join");
        _offBtn = Ui.Btn("PLAY OFFLINE", () => Limited(() => Net.I?.GoOffline()), "Offline");
        _options.AddChild(_hostBtn); _options.AddChild(_joinBtn); _options.AddChild(_offBtn);
        _sessionBtns = new[] { _hostBtn, _joinBtn, _offBtn, _reconnect };

        _status = Ui.Lbl(Net.I?.LastStatus ?? "", Ui.Small, Ui.Dim);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(320, 0);
        _options.AddChild(_status);
        // THE HOST'S SIDE (§6.1): INVITE A FRIEND, its code one COPY away, the box a friend's reply is pasted
        // into (the clipboard pickup takes it by itself), and the addresses friends on this network or a
        // virtual one type instead.
        _invite = Ui.Btn("INVITE A FRIEND", () => Limited(() => Net.I?.Invite()), "Invite");
        _options.AddChild(_invite);
        // THE PENDING LIST: one line per invite not yet connected, its code one COPY away, and CANCEL,
        // which frees its place (the full text sends the host here)
        _pending = Ui.VBox(4, "PendingList");
        _options.AddChild(_pending);
        _reply = new LineEdit { Name = "ReplyBox", PlaceholderText = "paste a friend's reply here", CustomMinimumSize = new Vector2(300, 0) };
        _reply.TextSubmitted += t => { _reply.ReleaseFocus(); Net.I?.TakeCode(t); _reply.Text = ""; };
        _options.AddChild(_reply);
        _addresses = Ui.Lbl("", Ui.Small, Ui.Dim);
        _addresses.Name = "Addresses";
        _options.AddChild(_addresses);
        // THE GUEST'S SIDE (§6.2): the reply it made, with how long the host has to take it, and a fresh
        // reply from the same invite once that ran out.
        _replyShown = Ui.Lbl("", Ui.Small, Ui.Dim);
        _replyShown.Name = "ReplyCode";
        _replyShown.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _replyShown.CustomMinimumSize = new Vector2(320, 0);
        _options.AddChild(_replyShown);
        _fresh = Ui.Btn("MAKE A FRESH REPLY", () => Limited(() => Net.I?.FreshReply()), "FreshReply");
        _options.AddChild(_fresh);
        // what a player pastes to the developer when a join did not work (§6.3)
        _options.AddChild(Ui.Btn("COPY NETWORK REPORT", () => { if (Net.I != null) Rendezvous.Copy(Net.I.Report()); }, "CopyReport"));
        if (Net.I != null) { Net.I.Status += OnStatus; Net.I.SessionChanged += Refresh; Net.I.PlayerJoined += OnPeers; Net.I.PlayerLeft += OnLeft; }
        Refresh();
    }

    // Net is an autoload and outlives this menu. Unsubscribe, or the next status
    // line is written to a freed Label.
    public override void _ExitTree()
    {
        if (Net.I != null) { Net.I.Status -= OnStatus; Net.I.SessionChanged -= Refresh; Net.I.PlayerJoined -= OnPeers; Net.I.PlayerLeft -= OnLeft; }
    }

    private void OnStatus(string s) { _status.Text = s; Refresh(); }
    public override void _Process(double delta)
    {
        _now += delta;
        bool locked = Locked;                                   // the 1 s rate limit, shown on the buttons
        foreach (var btn in _sessionBtns) btn.Disabled = locked;
        // HOST while hosting would drop every guest to start the same session again
        _hostBtn.Disabled |= Net.IsHost && Net.IsOnline;
        bool gated = !Link.Available;
        _hostBtn.Disabled |= gated; _joinBtn.Disabled |= gated;
        _missing.Visible = gated;
        var n = Net.I;
        _reconnect.Visible = n != null && n.CanReconnect;
        Refresh();
        bool hosting = n != null && Net.IsHost && Net.IsOnline;
        _invite.Visible = hosting; _reply.Visible = hosting; _addresses.Visible = hosting;
        _invite.Disabled = locked || !hosting;                 // pressed when full, it says how to free a place
        _pending.Visible = hosting;
        if (hosting) ListPending(n);
        if (hosting) Ui.SetText(_addresses, string.Join("\n", n.Addresses));   // Ui.SetText: only when changed -- MSDF glyphs re-shape
        bool replying = n != null && n.Connecting && n.ReplyCode.Length > 0;
        _replyShown.Visible = replying;
        if (replying)
        {
            double left = System.Math.Max(0, Link.ReplyWindowS - (Time.GetTicksMsec() - n.ReplyAt) / 1000.0);
            Ui.SetText(_replyShown, $"{n.ReplyCode}\n{left:0} s left for the host to take it");
        }
        _fresh.Visible = n != null && n.FreshFrom.Length > 0 && !n.Connecting && !Net.IsOnline;
    }
    private void OnPeers(int _) => Refresh();
    private void OnLeft(int _, Net.PlayerInfo __, bool ___) => Refresh();

    // The folded button's label: what it is, and where you stand.
    private void Refresh()
    {
        if (_toggle == null) return;
        var n = Net.I;
        string where = !Net.IsOnline ? (n == null ? "offline" : n.Reconnecting ? $"reconnecting · {System.Math.Max(1, n.Attempt)} of {Net.RetryAt.Length}"
                                      : n.CanReconnect ? "host lost" : n.Connecting ? "connecting…" : "offline")
                     : Net.IsHost ? $"hosting · {Net.I.Players.Count}" : $"guest · {Net.I.Players.Count}";
        Ui.SetText(_toggle, $"{(_toggle.ButtonPressed ? "▾" : "▸")}  MULTIPLAYER  ({where})");
    }

    private void DoJoin() { if (Link.Available) Net.I?.Join(_addr.Text); }

    // Rebuilt only when an entry comes, goes or changes stage.
    private string _listed = "";
    private void ListPending(Net n)
    {
        string now = string.Join(",", n.Pending.All.Select(e => $"{e.Id}:{e.Stage}:{e.Code.Length > 0}"));
        if (now == _listed) return;
        _listed = now;
        foreach (var c in _pending.GetChildren()) c.QueueFree();
        foreach (var e in n.Pending.All)
        {
            var row = Ui.HBox(6, $"Invite{e.Id}");
            string who = e.Name.Length > 0 ? e.Name : e.Row == Rendezvous.Paste.Id ? "an invite" : "a typed address";
            var what = Ui.Lbl($"{who} · {(e.Stage == Rendezvous.Stage.Waiting ? "waiting for a reply" : "joining…")}", Ui.Small, Ui.Dim);
            what.CustomMinimumSize = new Vector2(190, 0);
            what.ClipText = true;
            row.AddChild(what);
            int id = e.Id; string code = e.Code;
            if (code.Length > 0) row.AddChild(Ui.Btn("COPY", () => Rendezvous.Copy(code), "Copy"));
            row.AddChild(Ui.Btn("CANCEL", () => Net.I?.Hang(id), "Cancel"));
            _pending.AddChild(row);
        }
    }

    private Button _hostBtn, _joinBtn, _offBtn, _reconnect, _invite, _fresh;
    private VBoxContainer _pending;
    private Button[] _sessionBtns;
    private Label _missing, _addresses, _replyShown;
    private LineEdit _reply;
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
