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
        var bg = new ColorRect { Color = Ui.Deep };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        var col = new VBoxContainer { CustomMinimumSize = new Vector2(760, 0) };
        col.AddThemeConstantOverride("separation", 14);
        centre.AddChild(col);

        var title = Ui.Lbl("SELECT YOUR SHIP", Ui.Display, Ui.Text);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(760, 520),
                                           HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_rows);

        _empty = Ui.Lbl("No characters yet. Make one to start.", Ui.Body, Ui.Dim);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_empty);

        var bar = Ui.HBox(10);
        col.AddChild(bar);
        foreach (var b in new[] { Ui.Btn("NEW CHARACTER", OpenNew, size: Ui.Head), Ui.Btn("BACK", Back, size: Ui.Head) })
        {
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            bar.AddChild(b);
        }

        // The delete confirmation. A real modal: nothing else responds until it closes.
        _confirm = new ConfirmationDialog { Title = "Delete character", OkButtonText = "DELETE",
                                            CancelButtonText = "KEEP", Exclusive = true };
        _confirm.Confirmed += ConfirmDelete;
        _confirm.Canceled  += () => _pendingDelete = null;
        AddChild(_confirm);
        // the game's panel look, not Godot's default grey
        Ui.Style(_confirm);
        _confirm.AddThemeStyleboxOverride("panel", Ui.PanelStyle(14, 0.97f));
        // the title bar lives in the border's top expand margin: reserve it, or the title
        // floats over whatever is behind the dialog
        var border = Ui.PanelStyle(6, 0.99f); border.ExpandMarginTop = 32; border.ExpandMarginLeft = border.ExpandMarginRight = border.ExpandMarginBottom = 6;
        _confirm.AddThemeStyleboxOverride("embedded_border", border);
        _confirm.AddThemeStyleboxOverride("embedded_unfocused_border", border);

        Refresh();
    }


    // Rebuilt from disk every time, so the list is always what is actually saved.
    private void Refresh()
    {
        Ui.Clear(_rows);
        List<Character.Slot> all = Character.List();
        _empty.Visible = all.Count == 0;
        foreach (var slot in all) _rows.AddChild(Row(slot));
    }

    private Control Row(Character.Slot s)
    {
        // A character is a CARD, like every other row in the game: the list used to be
        // PanelContainers taking the window panel style, so each row looked like its own window.
        var panel = new PanelContainer { Name = "Row_" + s.Id };
        panel.AddThemeStyleboxOverride("panel", Ui.CardStyle(14, 12));
        var row = Ui.HBox(14);
        panel.AddChild(row);

        var art = new ShipPreview { Main = s.Main, Accent = s.Accent, Class = s.Class,
                                    CustomMinimumSize = new Vector2(200, 88) };
        if (!s.Playable) art.Modulate = new Color(1, 1, 1, 0.35f);   // greyed with the rest of the row
        row.AddChild(art);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        info.AddThemeConstantOverride("separation", 4);
        info.AddChild(Ui.Lbl(s.Name, Ui.Title, s.Playable ? Ui.Text : Ui.Dim));
        info.AddChild(Ui.Lbl(Classes.NameOf(s.Class), Ui.Small,
                             s.Playable ? Ui.Accent : Ui.Dim));
        // A character written by another build cannot be loaded, and the row says why rather than
        // failing when PLAY is pressed. Deleting it is still allowed: it is the player's character
        // and the only thing they can currently do with it.
        if (!s.Playable)
        {
            var note = Ui.Lbl(Game.IncompatibleNote, Ui.Small, Ui.Warn);
            note.Name = "Incompatible";
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.CustomMinimumSize = new Vector2(300, 0);
            info.AddChild(note);
        }
        var swatches = Ui.HBox(6);
        swatches.AddChild(Swatch(s.Main, "hull"));
        swatches.AddChild(Swatch(s.Accent, "accent"));
        info.AddChild(swatches);
        row.AddChild(info);

        var buttons = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(140, 0) };
        buttons.AddThemeConstantOverride("separation", 6);
        var play = Ui.Btn("PLAY", () => Play(s.Id), "Play", Ui.Body);
        play.Disabled = !s.Playable;
        var del  = Ui.Btn("DELETE", () => AskDelete(s), "Delete", Ui.Body);
        del.AddThemeColorOverride("font_color", Ui.Bad);
        buttons.AddChild(play); buttons.AddChild(del);
        row.AddChild(buttons);
        return panel;
    }

    private static Control Swatch(Color c, string label)
    {
        var h = Ui.HBox(4);
        h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(22, 14) });
        h.AddChild(Ui.Lbl(label, Ui.Small, Ui.Dim));
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
        _confirm.DialogText = $"Delete {s.Name} ({Classes.NameOf(s.Class)})?\n\n"
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
