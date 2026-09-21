using Godot;

// Stateful music (an autoload). Two loops play continuously and cross-fade by mood:
//   Ambient : nothing selected                   ambient full, combat silent
//   Alert   : an enemy is selected               ambient 35%, combat 20%
//   Combat  : you dealt or took damage in the
//             last 12 s, or you are in the arena
//             (CombatZone)                        ambient silent, combat 100%
// Every level is a share of the player's music volume (Settings.MusicVolume), which
// is itself under the master volume. The hub sets the mood each frame; any other
// scene falls back to Ambient. Purely local.
public partial class Music : Node
{
    public static Music I { get; private set; }
    public enum Mood { Ambient, Alert, Combat }
    public Mood Target = Mood.Ambient;
    public static bool CombatZone;                     // the arena sets it (Hub), the menu clears it
    private const float FadePerSecond = 0.7f;           // about 1.5 s for a full cross-fade
    private const float AmbientTrim = 0.45f;            // the ambient loop is meant to sit far back
    public const float Master = 0.65f;                 // the whole score, 35% quieter than it started

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
        Silence();
        _ambS?.Dispose(); _cmbS?.Dispose(); _ambS = _cmbS = null;
        if (I == this) I = null;
    }

    private (AudioStreamPlayer, AudioStreamOggVorbis) Loop(string path)
    {
        // CacheMode.Ignore, not GD.Load. GD.Load puts the stream in ResourceLoader's cache, which
        // holds it for the life of the process -- disposing our handle in _ExitTree then releases
        // nothing, and the engine reports both oggs as "resources still in use at exit". Nothing
        // else in the build wants these two files, so there is no sharing to lose by owning them
        // outright. It also means the Loop flag below is set on OUR copy rather than on a cached
        // resource every other loader would then see mutated.
        var s = (AudioStreamOggVorbis)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
        s.Loop = true;                                 // both files are built to loop seamlessly
        var p = new AudioStreamPlayer { Stream = s, VolumeDb = -80f };
        AddChild(p); p.Play();
        return (p, s);
    }

    public override void _Process(double delta)
    {
        var mood = CombatZone ? Mood.Combat : Target;
        // Alert (a target selected, no fighting yet): the ambient at half its level (0.35),
        // so it does not compete with the softened combat track under it
        float a = mood switch { Mood.Ambient => 1f, Mood.Alert => 0.35f, _ => 0f };
        float c = mood switch { Mood.Ambient => 0f, Mood.Alert => 0.2f, _ => 1f };
        float step = FadePerSecond * (float)delta;
        AmbientLevel = Mathf.MoveToward(AmbientLevel, a, step);
        CombatLevel = Mathf.MoveToward(CombatLevel, c, step);
        float vol = Settings.MusicOn ? Settings.MusicVolume : 0f;       // the Esc menu's MUSIC ON/OFF
        _amb.VolumeDb = Db(AmbientLevel * AmbientTrim * vol * Master);
        _cmb.VolumeDb = Db(CombatLevel * vol * Master);
    }

    // Stop both loops -- Game.Quit, which then gives the mixer the moment it needs to let go.
    public void Silence()
    {
        foreach (var p in new[] { _amb, _cmb }) if (IsInstanceValid(p)) { p.Stop(); p.Stream = null; }
    }

    private static float Db(float linear) => linear <= 0.0005f ? -80f : Mathf.LinearToDb(linear);
}
