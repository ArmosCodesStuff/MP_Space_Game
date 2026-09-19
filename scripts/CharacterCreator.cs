using Godot;

// Character creator: name, hull colour, accent colour, and the class selector.
// Two uses: making a NEW character on the select screen (IsNew: CREATE or cancel),
// and editing the current one in the hub with C (every close saves).
public partial class CharacterCreator : CanvasLayer
{
    public bool IsNew;
    // Fired once, when the creator closes. True if the character was saved.
    public System.Action<bool> Done;
    // Fired on every edit, so the ship under the panel can update live.
    public System.Action Changed;
    private bool _closed;

    private LineEdit _name;
    private ColorPickerButton _main, _accent;
    private ShipPreview _preview;
    private int _page;
    private VBoxContainer _cards;
    private Label _pageLabel;

    public override void _Ready()
    {
        Layer = 20;

        // fully opaque: at 0.96 the select screen's title showed through
        var dim = new ColorRect { Color = new Color(0.02f, 0.03f, 0.06f, 1f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var centre = new CenterContainer();          // centred on any screen size
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(centre);
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 24);
        centre.AddChild(root);

        // ── left: identity ───────────────────────────────────────────────────
        var left = new VBoxContainer(); left.AddThemeConstantOverride("separation", 10);
        root.AddChild(Ui.Wrap(left, 14));

        left.AddChild(Head("COMMISSION YOUR SHIP"));

        left.AddChild(new Label { Text = "Name" });
        _name = new LineEdit { Text = Character.Name, CustomMinimumSize = new Vector2(300, 0) };
        _name.MaxLength = 24;
        _name.TextChanged += t => { Character.Name = t.Trim().Length > 0 ? t.Trim() : "Commander"; };
        _name.TextSubmitted += _ => _name.ReleaseFocus();
        left.AddChild(_name);

        left.AddChild(new Label { Text = "Hull colour" });
        _main = new ColorPickerButton { Color = Character.Main, CustomMinimumSize = new Vector2(300, 34),
                                        EditAlpha = false, FocusMode = Control.FocusModeEnum.None };
        _main.ColorChanged += c => { Character.Main = c; _preview?.QueueRedraw(); Changed?.Invoke(); };
        left.AddChild(_main);

        left.AddChild(new Label { Text = "Accent colour" });
        _accent = new ColorPickerButton { Color = Character.Accent, CustomMinimumSize = new Vector2(300, 34),
                                          EditAlpha = false, FocusMode = Control.FocusModeEnum.None };
        _accent.ColorChanged += c => { Character.Accent = c; _preview?.QueueRedraw(); Changed?.Invoke(); };
        left.AddChild(_accent);

        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        _preview = new ShipPreview { Live = true, CustomMinimumSize = new Vector2(300, 220) };
        left.AddChild(_preview);

        var go = new Button { Text = IsNew ? "CREATE" : "DONE", CustomMinimumSize = new Vector2(300, 44), FocusMode = Control.FocusModeEnum.None };
        go.AddThemeFontSizeOverride("font_size", 20);
        go.Pressed += Close;
        left.AddChild(go);

        if (IsNew)
        {
            var cancel = new Button { Text = "CANCEL", CustomMinimumSize = new Vector2(300, 34), FocusMode = Control.FocusModeEnum.None };
            cancel.Pressed += Cancel;
            left.AddChild(cancel);
        }
        left.AddChild(new Label { Text = IsNew ? "Esc cancels." : "Esc also closes this panel.",
                                  Modulate = new Color(1, 1, 1, 0.55f) });

        // ── right: class selector, three to a page ───────────────────────────
        var right = new VBoxContainer(); right.AddThemeConstantOverride("separation", 10);
        root.AddChild(Ui.Wrap(right, 14));

        right.AddChild(Head("CHOOSE A CLASS"));

        var nav = new HBoxContainer(); nav.AddThemeConstantOverride("separation", 8);
        var prev = new Button { Text = "<", FocusMode = Control.FocusModeEnum.None }; prev.Pressed += () => Turn(-1); nav.AddChild(prev);
        _pageLabel = new Label { CustomMinimumSize = new Vector2(120, 0),
                                 HorizontalAlignment = HorizontalAlignment.Center };
        nav.AddChild(_pageLabel);
        var next = new Button { Text = ">", FocusMode = Control.FocusModeEnum.None }; next.Pressed += () => Turn(1); nav.AddChild(next);
        right.AddChild(nav);

        _cards = new VBoxContainer(); _cards.AddThemeConstantOverride("separation", 8);
        right.AddChild(_cards);

        Rebuild();
    }

    // Every saving way out goes through here, so closing always saves and notifies.
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        Character.Save();
        Done?.Invoke(true);
        QueueFree();
    }

    // Only a NEW character can be abandoned: nothing was written for it yet.
    // Editing an existing one has no cancel, because edits apply as you make them.
    public void Cancel()
    {
        if (!IsNew) { Close(); return; }
        if (_closed) return;
        _closed = true;
        Done?.Invoke(false);
        QueueFree();
    }

    private static Label Head(string t)
    {
        var l = new Label { Text = t };
        l.AddThemeFontSizeOverride("font_size", 22);
        return l;
    }

    private void Turn(int d)
    {
        _page = (_page + d + Classes.Pages) % Classes.Pages;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var c in _cards.GetChildren()) c.QueueFree();
        _pageLabel.Text = $"page {_page + 1} / {Classes.Pages}";

        for (int i = 0; i < Classes.PerPage; i++)
        {
            int idx = _page * Classes.PerPage + i;
            if (idx >= Classes.All.Length) break;
            var e = Classes.All[idx];

            var card = new Button {
                CustomMinimumSize = new Vector2(460, 92),
                Disabled = !e.Ready,
                FocusMode = Control.FocusModeEnum.None,
                Text = e.Ready
                    ? $"{e.Name}{(Character.Class == e.Id && e.Ready ? "     [ SELECTED ]" : "")}\n{e.Blurb}"
                    : $"{e.Name}     — not yet flyable\n{e.Blurb}",
            };
            card.AddThemeFontSizeOverride("font_size", 13);
            var captured = e;
            card.Pressed += () => { Character.Class = captured.Id; if (!IsNew) Character.Save(); Rebuild(); _preview?.QueueRedraw(); Changed?.Invoke(); };
            _cards.AddChild(card);
        }
    }
}

// The ship as it will look in game: the class's real sprite, tinted with the hull
// colour exactly as PlayerShip tints it, with accent-coloured turrets on the real
// mounts (PlayerShip.Art). Live reads the current character every redraw; otherwise
// it draws the fixed values it was given (the select screen shows one per row).
public partial class ShipPreview : Control
{
    public bool Live;
    public Color Main, Accent;
    public ShipClass Class;

    public override void _Process(double delta) { if (Live) QueueRedraw(); }

    public override void _Draw()
    {
        if (Live) { Main = Character.Main; Accent = Character.Accent; Class = Character.Class; }
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.05f, 0.07f, 0.12f));
        if (!PlayerShip.Art.TryGetValue(Class, out var art)) return;
        var tex = GD.Load<Texture2D>(art.Texture);

        // In a wide box (the select screen's rows) the ship lies nose-right so it can
        // be drawn large; in a tall box (the creator) it stands nose-up.
        bool lie = Size.X > Size.Y * 1.15f;
        var box = lie ? new Vector2(Size.Y, Size.X) : Size;                 // the box in the ship's frame
        float texScale = art.Length / tex.GetHeight();                     // world units per texture px
        var worldSize = tex.GetSize() * texScale;
        float k = Mathf.Min(box.Y * 0.88f / worldSize.Y, box.X * 0.88f / worldSize.X);   // screen px per world unit
        var shipXf = new Transform2D(lie ? Mathf.Pi / 2f : 0f, Size * 0.5f);   // the ship's frame on screen
        DrawSetTransformMatrix(shipXf);
        var c = Vector2.Zero;
        var drawSize = worldSize * k;
        // the accent lights the engines: a plume at the stern shows the choice
        Plume.Draw(this, c + new Vector2(0, drawSize.Y * 0.5f), Vector2.Down, drawSize.Y, Accent, 0.8f, false);
        DrawTextureRect(tex, new Rect2(c - drawSize * 0.5f, drawSize), false, Main);

        // turrets: the same cut-out sprites the game turns, pointing forward, in the
        // hull colour; the drawn fallback only for a class without turret art
        void Mount(Vector2 off, bool pd)
        {   // the cut-out turret sprites the game turns, as painted: aft turrets face aft
            var p = c + off * k;
            var tt = GD.Load<Texture2D>(pd ? art.PdTurret : art.MainTurret);
            var sz = tt.GetSize() * art.TurretTexScale * k;
            DrawSetTransformMatrix(shipXf * new Transform2D(!pd && off.Y > 0 ? Mathf.Pi : 0f, p));
            DrawTextureRect(tt, new Rect2(-sz * 0.5f, sz), false, Main);
            DrawSetTransformMatrix(shipXf);
        }
        foreach (var m in art.Mains) Mount(m, false);
        foreach (var m in art.Pds) Mount(m, true);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
