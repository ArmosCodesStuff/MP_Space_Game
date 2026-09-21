using Godot;

// The in-game menu (Esc, once there is nothing else to close): radar size, music on/off and
// volume, the tutorial's hints on/off, and the way out. Settings save at once; the hints switch
// is the pilot's (each character remembers its own), and saves with it. The game keeps running behind
// it -- this is a multiplayer world, it cannot pause -- but the helm is locked.
public partial class EscMenu : CanvasLayer
{
    public Hub Hub;

    public override void _Ready()
    {
        Layer = 30;
        var centre = new CenterContainer(); centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(centre);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        col.AddThemeConstantOverride("separation", 10);
        centre.AddChild(Ui.Wrap(col, 16));
        var title = Ui.Lbl("MENU", Ui.Title, Ui.Accent);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);

        col.AddChild(Ui.Heading("Radar size"));
        col.AddChild(Choice(new[] { "SMALL", "MEDIUM", "LARGE" }, () => Settings.RadarSize, i => Settings.RadarSize = i, "Radar"));
        var music = new Button { Name = "MusicToggle", FocusMode = Control.FocusModeEnum.None, Text = Settings.MusicOn ? "MUSIC: ON" : "MUSIC: OFF" };
        music.Pressed += () => { Settings.MusicOn = !Settings.MusicOn; Settings.Save(); music.Text = Settings.MusicOn ? "MUSIC: ON" : "MUSIC: OFF"; };
        col.AddChild(music);
        col.AddChild(Ui.Heading("Music volume"));
        var steps = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
        col.AddChild(Choice(new[] { "0%", "25%", "50%", "75%", "100%" },
                            () => System.Array.FindIndex(steps, v => Mathf.IsEqualApprox(v, Settings.MusicVolume)),
                            i => Settings.MusicVolume = steps[i], "Music"));
        var hints = new Button { Name = "HintsToggle", FocusMode = Control.FocusModeEnum.None, Text = Character.HintsOff ? "HINTS: OFF" : "HINTS: ON" };
        hints.Pressed += () => { Character.HintsOff = !Character.HintsOff; Character.Save(); hints.Text = Character.HintsOff ? "HINTS: OFF" : "HINTS: ON"; };
        col.AddChild(hints);

        var resume = new Button { Text = "RESUME", Name = "Resume", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
        resume.Pressed += () => Hub.ToggleEscMenu();
        col.AddChild(resume);
        var quit = new Button { Text = "QUIT TO MAIN MENU", Name = "Quit", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
        // The session ends on arrival: MainMenu._Ready goes offline for every route back to it.
        // Going offline HERE, first, ran while the arena was still loaded -- the hub answered by
        // sending the party home, and that scene change replaced the menu's.
        quit.Pressed += () => GetTree().ChangeSceneToFile("res://MainMenu.tscn");
        col.AddChild(quit);
        var foot = Ui.Lbl("Esc closes.", Ui.Small, Ui.Dim);
        foot.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(foot);
    }

    // a row of toggle buttons, one pressed; choosing saves the setting
    private static Control Choice(string[] labels, System.Func<int> current, System.Action<int> set, string name)
    {
        var row = Ui.HBox(6, name);
        var group = new ButtonGroup();
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var b = new Button { Text = labels[i], Name = $"{name}{i}", ToggleMode = true, ButtonGroup = group,
                                 FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                                 ButtonPressed = current() == i };
            b.Pressed += () => { set(idx); Settings.Save(); };
            row.AddChild(b);
        }
        return row;
    }
}
