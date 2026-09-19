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
    private AudioStreamOggVorbis _ambS, _cmbS;
    public float AmbientLevel { get; private set; }
    public float CombatLevel { get; private set; }

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        (_amb, _ambS) = Loop("res://music_ambient.ogg");
        (_cmb, _cmbS) = Loop("res://music_combat.ogg");
    }

    // Release the streams on the way out; left playing, they were reported as
    // "resources still in use at exit".
    public override void _ExitTree()
    {
        foreach (var p in new[] { _amb, _cmb }) if (IsInstanceValid(p)) { p.Stop(); p.Stream = null; }
        _ambS?.Dispose(); _cmbS?.Dispose(); _ambS = _cmbS = null;
        if (I == this) I = null;
    }

    private (AudioStreamPlayer, AudioStreamOggVorbis) Loop(string path)
    {
        var s = GD.Load<AudioStreamOggVorbis>(path);
        s.Loop = true;                                 // both files are built to loop seamlessly
        var p = new AudioStreamPlayer { Stream = s, VolumeDb = -80f };
        AddChild(p); p.Play();
        return (p, s);
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

    // Stop both loops. Call this a moment BEFORE quitting: the mixer lets go of its
    // playbacks on its next cycle, and quitting in the same frame left them "still in
    // use at exit".
    public void Silence()
    {
        foreach (var p in new[] { _amb, _cmb }) if (IsInstanceValid(p)) { p.Stop(); p.Stream = null; }
    }

    private static float Db(float linear) => linear <= 0.0005f ? -80f : Mathf.LinearToDb(linear);
}
