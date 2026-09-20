using Godot;

// Audio and other machine-local preferences. Space Fleet Idle kept these on its
// prestige Profile; Warships has no prestige, so they live on their own.
// Separate from Character on purpose: Character is WHO you are and travels with
// you between worlds, Settings is how this machine is configured.
public static class Settings
{
    public const string Path = "user://settings.cfg";

    public static readonly float[] VolumeSteps = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    public static int VolumeIdx = 3;
    public static float MusicVolume = 0.6f;          // 0..1, a share of the master volume
    public static bool MusicOn = true;               // on by default; the Esc menu toggles it
    public static int RadarSize = 1;                 // 0 small, 1 medium, 2 large
    public static float Volume => VolumeSteps[Mathf.Clamp(VolumeIdx, 0, VolumeSteps.Length - 1)];

    public static void ApplyVolume()
    {
        int bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) return;
        AudioServer.SetBusMute(bus, Volume <= 0.001f);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(Volume, 0.0001f)));
    }

    // Ability key bindings, "Class.abilityId" -> Key. Only changed keys are stored;
    // anything absent uses the ability's default (see Abilities).
    public static readonly System.Collections.Generic.Dictionary<string, int> Keys = new();
    private static bool _loaded;

    public static void Save()
    {
        var c = new ConfigFile();
        c.SetValue("audio", "volume_idx", VolumeIdx);
        c.SetValue("audio", "music", MusicVolume);
        c.SetValue("audio", "music_on", MusicOn);
        c.SetValue("ui", "radar_size", RadarSize);
        foreach (var kv in Keys) c.SetValue("keys", kv.Key, kv.Value);
        // Temp file, then rename -- the same reason Character.Save does it. ConfigFile.Save
        // truncates and rewrites in place, and two instances share one user:// (the documented
        // "Run Multiple Instances" way of testing multiplayer). A rebind saved by one while the
        // other was loading left the reader with a parse error, and it silently fell back to
        // defaults for the whole session.
        var tmp = Path + ".tmp";                 // not ".cfg": never mistaken for the real file
        if (c.Save(tmp) != Error.Ok) return;
        if (DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tmp),
                                     ProjectSettings.GlobalizePath(Path)) != Error.Ok)
        {
            c.Save(Path);                        // losing the settings outright is worse
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tmp));
        }
    }

    public static void Load()
    {
        _loaded = true;
        var c = new ConfigFile();
        if (c.Load(Path) != Error.Ok) return;
        VolumeIdx = (int)c.GetValue("audio", "volume_idx", VolumeIdx);
        MusicVolume = (float)c.GetValue("audio", "music", MusicVolume);
        MusicOn = (bool)c.GetValue("audio", "music_on", true);
        RadarSize = Mathf.Clamp((int)c.GetValue("ui", "radar_size", RadarSize), 0, 2);
        Keys.Clear();
        if (c.HasSection("keys"))
            foreach (var k in c.GetSectionKeys("keys")) Keys[k] = (int)c.GetValue("keys", k, 0);
    }

    // For scenes entered without the main menu (the editor's F6, the smoke test).
    public static void EnsureLoaded() { if (!_loaded) Load(); }
}
