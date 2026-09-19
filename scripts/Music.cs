using Godot;

// Stateful music (an autoload). Two loops play continuously and cross-fade by mood:
//   Ambient : nothing selected                   ambient full, combat silent
//   Alert   : an enemy is selected               ambient 70%, combat 20%
//   Combat  : you dealt or took damage in the
//             last 8 s, or you are in a combat
//             zone (CombatZone: for the instanced
//             systems to come)                   ambient silent, combat 100%
// Every level is a share of the player's music volume (Settings.MusicVolume), which
// is itself under the master volume. The hub sets the mood each frame; any other
// scene falls back to Ambient. Purely local.
public partial class Music : Node
{
    public static Music I { get; private set; }
    public enum Mood { Ambient, Alert, Combat }
    public Mood Target = Mood.Ambient;
    public static bool CombatZone;                     // set by an instanced combat system
    public const float FadePerSecond = 0.7f;           // about 1.5 s for a full cross-fade
    public const float AmbientTrim = 0.45f;            // the ambient loop is meant to sit far back

    private AudioStreamPlayer _amb, _cmb;
    public float AmbientLevel { get; private set; }
    public float CombatLevel { get; private set; }

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        _amb = Loop("res://music_ambient.ogg");
        _cmb = Loop("res://music_combat.ogg");
    }

    private AudioStreamPlayer Loop(string path)
    {
        var s = GD.Load<AudioStreamOggVorbis>(path);
        s.Loop = true;                                 // both files are built to loop seamlessly
        var p = new AudioStreamPlayer { Stream = s, VolumeDb = -80f };
        AddChild(p); p.Play();
        return p;
    }

    public override void _Process(double delta)
    {
        var mood = CombatZone ? Mood.Combat : Target;
        float a = mood switch { Mood.Ambient => 1f, Mood.Alert => 0.7f, _ => 0f };
        float c = mood switch { Mood.Ambient => 0f, Mood.Alert => 0.2f, _ => 1f };
        float step = FadePerSecond * (float)delta;
        AmbientLevel = Mathf.MoveToward(AmbientLevel, a, step);
        CombatLevel = Mathf.MoveToward(CombatLevel, c, step);
        _amb.VolumeDb = Db(AmbientLevel * AmbientTrim * Settings.MusicVolume);
        _cmb.VolumeDb = Db(CombatLevel * Settings.MusicVolume);
    }

    private static float Db(float linear) => linear <= 0.0005f ? -80f : Mathf.LinearToDb(linear);
}
