using Godot;
using System.Collections.Generic;

// Character select, Terraria-style: every saved character as a row with its ship,
// name, class and colours, and PLAY / DELETE on each. NEW CHARACTER opens the
// creator. DELETE asks first, then removes the character's file from disk.
public partial class CharacterSelect : Control
{
    private VBoxContainer _rows;
    private Label _empty;
    private ConfirmationDialog _confirm;
    private CharacterCreator _creator;
    private string _pendingDelete;

    public override void _Ready()
    {
        Ui.Install(GetTree());                                  // the game-wide look
        var bg = new ColorRect { Color = new Color(0.02f, 0.03f, 0.06f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var col = new VBoxContainer { CustomMinimumSize = new Vector2(760, 0) };
        col.AddThemeConstantOverride("separation", 14);
        centre.AddChild(col);

        var title = new Label { Text = "SELECT YOUR SHIP", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        col.AddChild(title);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(760, 520),
                                           HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_rows);

        _empty = new Label { Text = "No characters yet. Make one to start.",
                             HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.6f) };
        col.AddChild(_empty);

        var bar = new HBoxContainer(); bar.AddThemeConstantOverride("separation", 10);
        col.AddChild(bar);
        bar.AddChild(Btn("NEW CHARACTER", OpenNew, 20, true));
        bar.AddChild(Btn("BACK", Back, 20, true));

        // The delete confirmation. A real modal: nothing else responds until it closes.
        _confirm = new ConfirmationDialog { Title = "Delete character", OkButtonText = "DELETE",
                                            CancelButtonText = "KEEP", Exclusive = true };
        _confirm.Confirmed += ConfirmDelete;
        _confirm.Canceled  += () => _pendingDelete = null;
        AddChild(_confirm);
        // the game's panel look, not Godot's default grey
        _confirm.AddThemeStyleboxOverride("panel", Ui.PanelStyle(14, 0.97f));
        // the title bar lives in the border's top expand margin: reserve it, or the title
        // floats over whatever is behind the dialog
        var border = Ui.PanelStyle(6, 0.99f); border.ExpandMarginTop = 32; border.ExpandMarginLeft = border.ExpandMarginRight = border.ExpandMarginBottom = 6;
        _confirm.AddThemeStyleboxOverride("embedded_border", border);
        _confirm.AddThemeStyleboxOverride("embedded_unfocused_border", border);

        Refresh();
    }

    private static Button Btn(string text, System.Action onPress, int size = 15, bool grow = false)
    {
        var b = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, size >= 20 ? 44 : 34) };
        b.AddThemeFontSizeOverride("font_size", size);
        if (grow) b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.Pressed += onPress;
        return b;
    }

    // Rebuilt from disk every time, so the list is always what is actually saved.
    private void Refresh()
    {
        foreach (var c in _rows.GetChildren()) { _rows.RemoveChild(c); c.QueueFree(); }
        List<Character.Slot> all = Character.List();
        _empty.Visible = all.Count == 0;
        foreach (var slot in all) _rows.AddChild(Row(slot));
    }

    private Control Row(Character.Slot s)
    {
        var panel = new PanelContainer { Name = "Row_" + s.Id };
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);

        row.AddChild(new ShipPreview { Main = s.Main, Accent = s.Accent, Class = s.Class,
                                       CustomMinimumSize = new Vector2(200, 88) });

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        var name = new Label { Text = s.Name }; name.AddThemeFontSizeOverride("font_size", 22);
        info.AddChild(name);
        info.AddChild(new Label { Text = s.Class == ShipClass.Battleship ? "BATTLESHIP" : "CARRIER",
                                  Modulate = new Color(1, 1, 1, 0.7f) });
        var swatches = new HBoxContainer(); swatches.AddThemeConstantOverride("separation", 6);
        swatches.AddChild(Swatch(s.Main, "hull"));
        swatches.AddChild(Swatch(s.Accent, "accent"));
        info.AddChild(swatches);
        row.AddChild(info);

        var buttons = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(140, 0) };
        buttons.AddThemeConstantOverride("separation", 6);
        var play = Btn("PLAY", () => Play(s.Id)); play.Name = "Play";
        var del  = Btn("DELETE", () => AskDelete(s)); del.Name = "Delete";
        del.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.5f));
        buttons.AddChild(play); buttons.AddChild(del);
        row.AddChild(buttons);
        return panel;
    }

    private static Control Swatch(Color c, string label)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4);
        h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(22, 14) });
        h.AddChild(new Label { Text = label, Modulate = new Color(1, 1, 1, 0.55f) });
        return h;
    }

    private void Play(string id)
    {
        if (!Character.Load(id)) { Refresh(); return; }   // file vanished: show what is really there
        GetTree().ChangeSceneToFile("res://Hub.tscn");
    }

    private void AskDelete(Character.Slot s)
    {
        _pendingDelete = s.Id;
        _confirm.DialogText = $"Delete {s.Name} ({(s.Class == ShipClass.Battleship ? "Battleship" : "Carrier")})?\n\n"
                            + "This removes the character from disk. It cannot be undone.";
        _confirm.PopupCentered(new Vector2I(460, 170));
    }

    private void ConfirmDelete()
    {
        if (_pendingDelete != null) Character.Delete(_pendingDelete);
        _pendingDelete = null;
        Refresh();
    }

    private void OpenNew()
    {
        if (IsInstanceValid(_creator)) return;
        Character.NewBlank();
        _creator = new CharacterCreator { IsNew = true };
        _creator.Done += saved => { _creator = null; if (saved) Refresh(); };
        AddChild(_creator);
    }

    private void Back() => GetTree().ChangeSceneToFile("res://MainMenu.tscn");

    public override void _Input(InputEvent e)
    {
        // same text-box rule as the hub: a click off the name box lets go of it
        var focus = GetViewport().GuiGetFocusOwner();
        if (e is InputEventMouseButton mb && mb.Pressed && focus is LineEdit le && !le.GetGlobalRect().HasPoint(mb.Position))
            le.ReleaseFocus();

        if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            if (focus is LineEdit le2) le2.ReleaseFocus();
            else if (IsInstanceValid(_creator)) _creator.Cancel();
            else Back();
        }
    }
}
