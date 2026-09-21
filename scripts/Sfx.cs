using Godot;
using System.Collections.Generic;

// SOUND EFFECTS -- local and cosmetic: every peer plays what it sees.
//   Lasers: a low buzz where the shot leaves (lighter for normal ships and craft, a
//     deep grinding one for a boss) and a crackle where it hits.
//   Self-propelled projectiles (missiles, torpedoes): a soft whoosh at launch, and a
//     low thunk with a soft reverb tail when they hit.
// Loudness follows the CAMERA: the distance from the view's centre to the sound, and
// the zoom (zoomed out, the listener is further away). A laser hit is also softer and
// a little lower the longer the shot that made it.
public static class Sfx
{
    private const float ListenerHeight = 700f;       // at zoom 1: how far "above" the view the ears are
    private const float Cull = 5000f;                // past this effective distance: silent
    static readonly string[] Names = { "laser_light", "laser_fighter", "laser_boss", "laser_hit",
                                      "missile_whoosh", "impact_thunk", "cannon" };
    static readonly Dictionary<string, AudioStream> _streams = new();
    static readonly Dictionary<string, double> _last = new();

    static readonly Dictionary<string, double> _gap = new() { ["cannon"] = 0.05, ["laser_light"] = 0.04, ["laser_fighter"] = 0.04, ["laser_boss"] = 0.08, ["laser_hit"] = 0.03, ["missile_whoosh"] = 0.05, ["impact_thunk"] = 0.05 };
    public static readonly Dictionary<string, int> Played = new();            // for the smoke test
    static Node _pool; static int _next;
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
    public static void Laser(Vector2 from, Vector2 to, ShotSound snd)
    {
        Play(snd switch { ShotSound.Boss => "laser_boss", ShotSound.Fighter => "laser_fighter", _ => "laser_light" },
             from, 0f, snd == ShotSound.Boss ? 0.9f : 1.0f);
        float shot = from.DistanceTo(to);
        Play("laser_hit", to, -3f * shot / 1000f, 1.0f - 0.12f * Mathf.Clamp(shot / 1000f, 0f, 1f));
    }
    public static void Missile(Vector2 at) => Play("missile_whoosh", at, 0f, 1f);
    public static void Impact(Vector2 at) => Play("impact_thunk", at, 0f, 1f);
    // a battleship gun: the thunk, higher -- a cannon's report, not a laser's buzz. Its own file
    // now rather than the impact played quietly, so the impact can be tuned without moving it.
    public static void Cannon(Vector2 at) => Play("cannon", at, 0f, 1.7f);

    static void Play(string name, Vector2 at, float trimDb, float pitch)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var cam = tree?.Root.GetViewport().GetCamera2D();
        if (cam == null) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (_last.TryGetValue(name, out var t) && now - t < _gap[name]) return;     // a wing firing is not a wall of noise
        float db = VolumeAt(cam.GetScreenCenterPosition().DistanceTo(at), cam.Zoom.X);
        if (float.IsNegativeInfinity(db)) return;
        _last[name] = now;
        Played[name] = Played.TryGetValue(name, out var n) ? n + 1 : 1;
        if (!IsInstanceValid(_pool))
        {
            _pool = new SfxPool { Name = "SfxPool" };
            tree.Root.CallDeferred(Node.MethodName.AddChild, _pool);
            for (int i = 0; i < Voices; i++) _pool.AddChild(new AudioStreamPlayer());
            foreach (var s in Names) _streams[s] = GD.Load<AudioStream>($"res://sfx/{s}.wav");
        }
        var p = _pool.GetChild<AudioStreamPlayer>(_next); _next = (_next + 1) % Voices;
        p.Stream = _streams[name]; p.VolumeDb = db + trimDb; p.PitchScale = pitch;
        if (p.IsInsideTree()) p.Play(); else p.CallDeferred(AudioStreamPlayer.MethodName.Play);
    }
    static bool IsInstanceValid(Node n) => n != null && GodotObject.IsInstanceValid(n);

    // Let the sounds go. _streams is STATIC and holds a handle to every wav for the life of the
    // process, so at shutdown the engine reports them as "resources still in use at exit" -- the
    // same message Music carries an _ExitTree to avoid, and the same fix. Distinct instances only:
    // "cannon" is an alias for impact_thunk and they are one object, so disposing per key would
    // dispose it twice.
    //
    // Safe to call at any time, not only at exit: Play() rebuilds the pool and reloads the
    // streams whenever the pool is gone, so the next sound after a release simply pays for the
    // load again.
    public static void Release()
    {
        if (IsInstanceValid(_pool))
            foreach (var c in _pool.GetChildren())
                if (c is AudioStreamPlayer p) { p.Stop(); p.Stream = null; }
        foreach (var s in new System.Collections.Generic.HashSet<AudioStream>(_streams.Values)) s?.Dispose();
        _streams.Clear();
        _last.Clear();
        _pool = null; _next = 0;
    }
}

// The voice pool, which stops and releases its streams on the way out. A voice STILL PLAYING at
// shutdown holds the stream it is playing, and the engine reports that as "resources still in use
// at exit" -- Music carries an _ExitTree for exactly this reason and its comment is the record of
// it. The pool lives on the root and outlives every scene, so nothing else was ever going to do it.
public partial class SfxPool : Node
{
    public override void _ExitTree() => Sfx.Release();
}
