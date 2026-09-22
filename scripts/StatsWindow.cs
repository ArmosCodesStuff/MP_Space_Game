using Godot;

// The K window, two tabs:
//   ABILITIES & KEYS : every ability of the class, what it does, and its key. Click a
//                      key, press a new one. A clash swaps the two; reserved keys are
//                      refused. Saved to this machine's settings.
//   STATS            : the ship's live ShipStats -- the same object the turrets, wings
//                      and helm read -- as base, added (purchases and gear's whole
//                      additions), bonus (a downside in red) and final, with derived DPS.
public partial class StatsWindow : CanvasLayer
{
    public PlayerShip Ship;
    private GridContainer _grid;
    private VBoxContainer _derived, _statsPane, _keysPane, _keyRows;
    private Label _title, _keyMsg;
    private ShipStats _shown;
    private string _capturing;                  // ability id waiting for a key
    public bool Capturing => _capturing != null;

    public override void _Ready()
    {
        Layer = 15;
        // ONE width, not three. The panel was pinned between two offsets 576 apart while its
        // contents asked for 532 plus padding; the restyle's roomier rows pushed that past 576, the
        // panel grew past the offset it was pinned to, and it hung off the right edge of the screen.
        // The sweep's UI lint caught it. Everything below is measured from PanelW so the three
        // numbers cannot drift apart again.
        const int PanelW = 600, Gutter = 16, Pad = 14;
        var panel = new PanelContainer { Position = new Vector2(1920 - Gutter - PanelW, 60), CustomMinimumSize = new Vector2(PanelW, 0) };
        Ui.Panelise(panel, Pad);      // the one panel look
        // anchor to the right edge so it stays put at any window size
        panel.AnchorLeft = panel.AnchorRight = 1f;
        panel.OffsetLeft = -Gutter - PanelW; panel.OffsetRight = -Gutter; panel.OffsetTop = 60;
        AddChild(panel);

        // PanelW less the panel's own content margins (Pad + 4 a side), less the scrollbar gutter.
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(PanelW - 2 * (Pad + 4) - 18, 780),
                                           HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 12);
        // a right margin, so the scrollbar never sits on the "final" column
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 18);
        scroll.AddChild(margin);
        margin.AddChild(col);

        _title = Ui.Lbl("", Ui.Title, Ui.Accent);
        col.AddChild(_title);

        var tabs = Ui.HBox(6);
        col.AddChild(tabs);
        var group = new ButtonGroup();
        var keysTab  = Ui.Tab("ABILITIES & KEYS", group, false); keysTab.Name = "KeysTab";
        var statsTab = Ui.Tab("STATS", group, false); statsTab.Name = "StatsTab";
        tabs.AddChild(keysTab); tabs.AddChild(statsTab);

        // ── keys pane ──
        _keysPane = Ui.VBox(10);
        col.AddChild(_keysPane);
        // the message sits above the rows, so it is seen without scrolling
        _keyMsg = Ui.Lbl("", Ui.Body, Ui.Warn);
        _keyMsg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _keysPane.AddChild(_keyMsg);
        _keyRows = Ui.VBox(8);
        _keysPane.AddChild(_keyRows);
        var reset = new Button { Text = "RESET TO DEFAULTS", FocusMode = Control.FocusModeEnum.None, Name = "ResetKeys" };
        reset.Pressed += () => { if (IsInstanceValid(Ship)) { Abilities.ResetDefaults(Ship.Class); _keyMsg.Text = "Defaults restored."; RebuildKeys(); } };
        _keysPane.AddChild(reset);
        var fixedKeys = Ui.Lbl("Fixed, not bindable: W A S D (helm), Tab, K, B, Y and the arrow keys (camera), Esc, Enter.", Ui.Small, Ui.Dim);
        fixedKeys.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _keysPane.AddChild(fixedKeys);

        // ── stats pane ──
        _statsPane = Ui.VBox(10);
        col.AddChild(_statsPane);
        var rule = Ui.Lbl("Final = (base + added) × (1 + bonus). Reload, cooldown and radius bonuses divide instead.", Ui.Small, Ui.Dim);
        rule.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _statsPane.AddChild(rule);
        _derived = new VBoxContainer(); _statsPane.AddChild(_derived);
        _grid = new GridContainer { Columns = 5 };
        _grid.AddThemeConstantOverride("h_separation", 18);
        _grid.AddThemeConstantOverride("v_separation", 2);
        _statsPane.AddChild(_grid);

        keysTab.Toggled  += on => { if (on) ShowPane(keys: true); };
        statsTab.Toggled += on => { if (on) ShowPane(keys: false); };
        keysTab.ButtonPressed = true; ShowPane(keys: true);

        col.AddChild(Ui.Lbl("K or Esc closes.", Ui.Small, Ui.Dim));
        Rebuild();
    }

    public void ShowPane(bool keys) { _keysPane.Visible = keys; _statsPane.Visible = !keys; }

    public override void _Process(double delta)
    {
        // the sheet object is replaced on a class swap or bonus change; follow it
        if (IsInstanceValid(Ship) && Ship.Stats != _shown) Rebuild();
    }

    // While waiting for a key, the next key press is the new binding and goes
    // nowhere else. _Input runs before the hub's handlers, so it cannot also fire an
    // ability. Esc cancels the capture rather than closing the window.
    public override void _Input(InputEvent e)
    {
        if (_capturing == null || e is not InputEventKey k || !k.Pressed || k.Echo) return;
        GetViewport().SetInputAsHandled();
        if (k.Keycode == Key.Escape) { _capturing = null; _keyMsg.Text = "Cancelled."; RebuildKeys(); return; }
        var err = Abilities.Bind(Ship.Class, _capturing, k.Keycode);
        _keyMsg.Text = err ?? $"{AbilityName(_capturing)} is now {Abilities.KeyName(k.Keycode)}.";
        _capturing = null;
        RebuildKeys();
    }

    private string AbilityName(string id) { foreach (var a in Abilities.For(Ship.Class)) if (a.Id == id) return a.Name; return id; }

    public void StartCapture(string id) { _capturing = id; _keyMsg.Text = $"Press a key for {AbilityName(id)}.  Esc cancels."; RebuildKeys(); }

    private void RebuildKeys()
    {
        Ui.Clear(_keyRows);
        if (!IsInstanceValid(Ship)) return;
        foreach (var ab in Abilities.For(Ship.Class))
        {
            var row = Ui.HBox(12, "Key_" + ab.Id);
            var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 2);
            info.AddChild(Ui.Lbl(ab.Name + (ab.Kind == AbilityKind.Hold ? "  (hold)" : ""), Ui.Body));
            var blurb = Ui.Lbl(ab.Blurb, Ui.Small, Ui.Dim);
            blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart; blurb.CustomMinimumSize = new Vector2(360, 0);
            info.AddChild(blurb);
            row.AddChild(info);
            var id = ab.Id;
            // The button carries the binding, so a capture in progress has to be unmistakable:
            // accent, not the same grey as a key that is simply set.
            bool live = _capturing == id;
            var btn = new Button { Name = "Bind", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 36),
                                   SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                                   Text = live ? "press a key…" : Abilities.KeyName(Abilities.KeyFor(Ship.Class, id)) };
            if (live) btn.AddThemeColorOverride("font_color", Ui.Accent);
            btn.Pressed += () => StartCapture(id);
            row.AddChild(btn);
            _keyRows.AddChild(Ui.CardWrap(row));
        }
    }

    private static Label Cell(string t, bool head = false, bool right = false, Color? c = null)
    {
        var l = Ui.Lbl(t, head ? Ui.Body : Ui.Small, head ? Ui.Accent : (Color?)null);
        l.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        if (c.HasValue) l.AddThemeColorOverride("font_color", c.Value);
        if (!right) l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return l;
    }

    private void Rebuild()
    {
        if (!IsInstanceValid(Ship)) return;
        RebuildKeys();
        var s = Ship.Stats; _shown = s;
        _title.Text = $"{Ship.Pilot} — {Classes.NameOf(s.Class)}";

        Ui.Clear(_derived);
        Ui.Clear(_grid);

        // ── damage per entity, and totals ──
        var acc = new Color(1f, 0.85f, 0.5f);
        void D(string label, double v, string unit = "DPS") =>
            _derived.AddChild(Cell($"{label}:  {v:0.00} {unit}", false, false, acc));
        _derived.AddChild(Cell("DAMAGE", true));
        if (s.Def.Has(Fit.Guns))
        {
            D("Per main barrel", s.MainDpsPerBarrel);
            D($"Main guns ({s["main_count"]:0} barrels)", s.MainDps);
            _derived.AddChild(Cell($"Salvo: {s["main_count"]:0} shots every {s["main_interval"]:0.00} s   ·   "
                                 + $"Staggered: 1 shot every {s.StaggerStep:0.00} s   (same rate)", false, false, acc));
            if (s.Def.Has(Fit.Broadside))
            {
                _derived.AddChild(Cell($"Broadside: {s["broadside_volleys"]:0} volleys × {s["main_count"]:0} shells × "
                                     + $"{s["main_damage"] * s["broadside_mult"]:0.00} = {s.BroadsideDamage:0.0} damage, every {s.BroadsideCycle:0.0} s",
                                       false, false, acc));
                D("Broadside, averaged over its cycle", s.BroadsideDps);
            }
            if (s.Def.Has(Fit.Missiles))
                D($"Missiles (magazine of {s["missile_mag"]:0} bursts of {PlayerShip.BurstSides.Length}, averaged over a reload)", s.MissileDps);
        }
        else
        {
            // fighters strafe (3 shots a pass, then turn) and rest, so this is their rate
            // WHILE firing a burst, not a sustained figure
            D("Per fighter, while firing a burst", s.FighterDpsEach);
            D($"Fighters ({s["fighter_count"]:0}), all firing at once", s.FighterDps);
            _derived.AddChild(Cell($"Bomber strike: {s.TorpedoesPerRun:0} torpedoes × {s["torpedo_damage"]:0.0} = "
                                 + $"{s.TorpedoesPerRun * s["torpedo_damage"]:0.0} damage if all hit (unguided)", false, false, acc));
        }
        D("Per PD turret (while firing)", s.PdDpsPerTurret);
        D($"PD sustained, if re-activated as soon as it recharges ({s.PdDuty * 100:0}% duty)", s.PdSustainedDps);
        double total = s.MainDps + s.MissileDps + s.BroadsideDps + s.PdSustainedDps;
        _derived.AddChild(Cell($"SUSTAINED TOTAL:  {total:0.00} DPS" + (s.Def.Has(Fit.Wing) ? "  (+ fighter runs, + bomber strikes)" : ""), true, false, acc));
        _derived.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        // ── every stat: base / added / bonus / final ──
        string group = null;
        foreach (var st in s.All)
        {
            if (st.Group != group)
            {
                group = st.Group;
                _grid.AddChild(Cell(group.ToUpperInvariant(), true, false, new Color(0.5f, 0.78f, 1f)));
                _grid.AddChild(Cell("base", true, true, new Color(1, 1, 1, 0.5f)));
                _grid.AddChild(Cell("added", true, true, new Color(1, 1, 1, 0.5f)));
                _grid.AddChild(Cell("bonus", true, true, new Color(1, 1, 1, 0.5f)));
                _grid.AddChild(Cell("final", true, true, new Color(1, 1, 1, 0.5f)));
            }
            _grid.AddChild(Cell("  " + st.Label));
            _grid.AddChild(Cell(st.Fmt(st.Base), false, true, new Color(1, 1, 1, 0.6f)));
            _grid.AddChild(Cell(st.Flat != 0 ? (st.Flat > 0 ? "+" : "−") + st.Fmt(System.Math.Abs(st.Flat)) : "", false, true, new Color(1, 1, 1, 0.6f)));
            // a downside reads as one: red, not the green every non-zero bonus used to get
            _grid.AddChild(Cell($"{(st.Bonus >= 0 ? "+" : "−")}{System.Math.Abs(st.Bonus) * 100:0}%", false, true,
                                st.Bonus > 0 ? Ui.Good : st.Bonus < 0 ? Ui.Bad : new Color(1, 1, 1, 0.4f)));
            _grid.AddChild(Cell(st.Fmt(st.Value), false, true));
        }
    }
}
