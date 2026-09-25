using Godot;
using System.Collections.Generic;

// SOUND EFFECTS -- local and cosmetic: every peer plays what it sees.
//   Beams (Beam.All): each kind's own report where the shot leaves -- its row names the file
//     and the pitch, so one file is several beams, each its own note -- and a crackle where it
//     lands.
//   Self-propelled projectiles (missiles, torpedoes): a soft whoosh at launch, and a
//     low thunk with a soft reverb tail when they hit.
//   Every boss special move has its own sound (ByName; tools/make_sounds.py makes them): the
//     Lancer's beam charging and its growl firing, its ram, shockwave and trident; the Drake's
//     gun, warp, scrap blast, tractor, throw and the rock breaking.
// Loudness follows the CAMERA: the distance from the view's centre to the sound, and
// the zoom (zoomed out, the listener is further away). A laser hit is also softer and
// a little lower the longer the shot that made it.
public static class Sfx
{
    private const float ListenerHeight = 700f;       // at zoom 1: how far "above" the view the ears are
    private const float Cull = 5000f;                // past this effective distance: silent
    [Live] static readonly Dictionary<string, double> _last = new();

    // Every sound there is (sfx/<name>.wav), and the least time between two of it FROM ONE NAME: a
    // wing firing is not a wall of noise. The name is a beam's row id when a beam plays the file --
    // two beams on one file are two notes, and one must not silence the other -- else the file's.
    static readonly Dictionary<string, double> _gap = new() { ["cannon"] = 0.05, ["laser_light"] = 0.04, ["laser_fighter"] = 0.04, ["laser_boss"] = 0.08, ["laser_hit"] = 0.03, ["missile_whoosh"] = 0.05, ["impact_thunk"] = 0.05,
        ["boss_beam_charge"] = 0.5, ["boss_beam"] = 0.5, ["boss_ram"] = 0.5, ["boss_shockwave"] = 0.5, ["boss_trident"] = 0.3,
        ["drake_gun"] = 0.2, ["drake_warp"] = 0.5, ["drake_scrap"] = 0.3, ["drake_tractor"] = 0.5, ["drake_throw"] = 0.5, ["drake_rock"] = 0.3,
        ["rail_perfect"] = 0.2, ["rail_miss"] = 0.2 };
    [Live] public static readonly Dictionary<string, int> Played = new();            // by that name, for the smoke test
    static Node _pool; static int _next;
    static bool _closed;                             // quitting: see Close
    const int Voices = 16;

    // decibels for a sound `dist` from the view's centre, at camera zoom `zoom`
    public static float VolumeAt(float dist, float zoom)
    {
        float h = ListenerHeight / Mathf.Max(0.05f, zoom);
        float eff = Mathf.Sqrt(dist * dist + h * h);
        if (eff > Cull) return float.NegativeInfinity;
        return -20f * Mathf.Log(eff / ListenerHeight) / Mathf.Log(10f);
    }

    // EVERY TRIM HERE IS ZERO, and that is the point: each file is stored at the level it is
    // meant to be heard at (tools/gain.ps1 rewrote them), so a sound's loudness is a property of
    // the sound and not of whoever happens to play it. The one exception is the hit, which fades
    // with the length of the shot that made it -- that is a function of the shot, not a level.
    // THE BEAMS SIT A QUARTER UNDER, and that is in their files too (the owner: "lower the volume
    // default of beams by 25%"): every file a beam is heard by was rewritten at 0.75 of the level
    // it shipped at -- laser_light/_fighter/_boss/_hit by tools/gain.ps1, the death beam's and the
    // tractor's by BEAM in tools/make_sounds.py. Not a trim here, not a bus, not a setting: a
    // second 0.75 anywhere would make them 0.56.
    //
    // A PITCH IS NOT A LEVEL. A beam plays its row's file at its row's pitch, gated and counted
    // under its row's id, so two beams on one file are two notes that never silence each other.
    public static void Beam(Vector2 from, Vector2 to, BeamDef beam)
    {
        Play(beam.Report, from, 0f, beam.Pitch, beam.Id);
        float shot = from.DistanceTo(to);
        Play("laser_hit", to, -3f * shot / 1000f, 1.0f - 0.12f * Mathf.Clamp(shot / 1000f, 0f, 1f));
    }
    public static void Missile(Vector2 at) => Play("missile_whoosh", at, 0f, 1f);
    // A SOUND BY ITS FILE'S NAME: a boss's move, an effect's, a shot row's, a gun's cue (the active
    // reload's tick and click). A name that is none of them plays nothing: it may have come over the wire.
    public static void ByName(string name, Vector2 at) { if (name != null && _gap.ContainsKey(name)) Play(name, at, 0f, 1f); }
    public static void Impact(Vector2 at) => Play("impact_thunk", at, 0f, 1f);
    // a battleship gun: the thunk, higher -- a cannon's report, not a laser's buzz. Its own file
    // now rather than the impact played quietly, so the impact can be tuned without moving it.
    public static void Cannon(Vector2 at) => Play("cannon", at, 0f, 1.7f);

    // `file` is what is heard; `key` is whose it is -- the gate it waits on and the name it is
    // counted under. A beam's is its row's id; anything else's is its file.
    static void Play(string file, Vector2 at, float trimDb, float pitch, string key = null)
    {
        key ??= file;
        var tree = Engine.GetMainLoop() as SceneTree;
        var cam = tree?.Root.GetViewport().GetCamera2D();
        if (cam == null || _closed) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (_last.TryGetValue(key, out var t) && now - t < _gap[file]) return;     // a wing firing is not a wall of noise
        float db = VolumeAt(cam.GetScreenCenterPosition().DistanceTo(at), cam.Zoom.X);
        if (float.IsNegativeInfinity(db)) return;
        _last[key] = now;
        Played[key] = Played.TryGetValue(key, out var n) ? n + 1 : 1;
        if (!IsInstanceValid(_pool))
        {
            _pool = new SfxPool { Name = "SfxPool" };
            tree.Root.CallDeferred(Node.MethodName.AddChild, _pool);
            for (int i = 0; i < Voices; i++) _pool.AddChild(new AudioStreamPlayer());
            foreach (var s in _gap.Keys) Assets.Load<AudioStream>(Wav(s));     // every file read now, not mid-battle
        }
        var p = _pool.GetChild<AudioStreamPlayer>(_next); _next = (_next + 1) % Voices;
        p.Stream = Assets.Load<AudioStream>(Wav(file)); p.VolumeDb = db + trimDb; p.PitchScale = pitch;
        if (p.IsInsideTree()) p.Play(); else p.CallDeferred(AudioStreamPlayer.MethodName.Play);
    }
    static bool IsInstanceValid(Node n) => n != null && GodotObject.IsInstanceValid(n);
    static string Wav(string name) => $"res://sfx/{name}.wav";

    // Quitting: stop every voice, empty it, and play nothing more. A stopped voice's playback is
    // released by the mixer on its next cycles, which is why Game.Quit waits a moment after this.
    // The streams are Assets', held until the engine shuts C# down (Assets.cs says why). Also the
    // pool's own way out, for an exit that skipped Game.Quit.
    public static void Close()
    {
        _closed = true;
        if (IsInstanceValid(_pool))
            foreach (var c in _pool.GetChildren())
                if (c is AudioStreamPlayer p) { p.Stop(); p.Stream = null; }
        _pool = null;
    }
}

// The voice pool. It lives on the root and outlives every scene, so its exit is the process's.
public partial class SfxPool : Node
{
    public override void _ExitTree() => Sfx.Close();
}
