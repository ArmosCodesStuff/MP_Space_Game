using Godot;

// The in-game menu (Esc, once there is nothing else to close): radar size, music
// volume, and the way out. Settings save at once. The game keeps running behind
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

        var resume = new Button { Text = "RESUME", Name = "Resume", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
        resume.Pressed += () => Hub.ToggleEscMenu();
        col.AddChild(resume);
        var quit = new Button { Text = "QUIT TO MAIN MENU", Name = "Quit", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
        quit.Pressed += () => { Net.I?.GoOffline(); GetTree().ChangeSceneToFile("res://MainMenu.tscn"); };
        col.AddChild(quit);
        var foot = Ui.Lbl("Esc closes.", Ui.Small, Ui.Dim);
        foot.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(foot);
    }

    // a row of toggle buttons, one pressed; choosing saves the setting
    private static Control Choice(string[] labels, System.Func<int> current, System.Action<int> set, string name)
    {
        var row = new HBoxContainer { Name = name }; row.AddThemeConstantOverride("separation", 6);
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
