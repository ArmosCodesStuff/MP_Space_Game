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
    public const float ListenerHeight = 700f;       // at zoom 1: how far "above" the view the ears are
    public const float Cull = 5000f;                // past this effective distance: silent
    static readonly string[] Names = { "laser_light", "laser_boss", "laser_hit", "missile_whoosh", "impact_thunk" };
    static readonly Dictionary<string, AudioStream> _streams = new();
    static readonly Dictionary<string, double> _last = new();
    static readonly Dictionary<string, double> _gap = new() { ["laser_light"] = 0.04, ["laser_boss"] = 0.08, ["laser_hit"] = 0.03, ["missile_whoosh"] = 0.05, ["impact_thunk"] = 0.05 };
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

    public static void Laser(Vector2 from, Vector2 to, bool boss)
    {
        Play(boss ? "laser_boss" : "laser_light", from, boss ? 0f : -6f, boss ? 0.9f : 1.0f);
        float shot = from.DistanceTo(to);
        Play("laser_hit", to, -4f - 3f * shot / 1000f, 1.0f - 0.12f * Mathf.Clamp(shot / 1000f, 0f, 1f));
    }
    public static void Missile(Vector2 at) => Play("missile_whoosh", at, -8f, 1f);
    public static void Impact(Vector2 at) => Play("impact_thunk", at, -2f, 1f);

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
            _pool = new Node { Name = "SfxPool" };
            tree.Root.CallDeferred(Node.MethodName.AddChild, _pool);
            for (int i = 0; i < Voices; i++) _pool.AddChild(new AudioStreamPlayer());
            foreach (var s in Names) _streams[s] = GD.Load<AudioStream>($"res://sfx/{s}.wav");
        }
        var p = _pool.GetChild<AudioStreamPlayer>(_next); _next = (_next + 1) % Voices;
        p.Stream = _streams[name]; p.VolumeDb = db + trimDb; p.PitchScale = pitch;
        if (p.IsInsideTree()) p.Play(); else p.CallDeferred(AudioStreamPlayer.MethodName.Play);
    }
    static bool IsInstanceValid(Node n) => n != null && GodotObject.IsInstanceValid(n);
}
