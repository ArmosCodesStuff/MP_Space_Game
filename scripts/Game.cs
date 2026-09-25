using Godot;

// THE BUILD'S IDENTITY. One number, bumped by one for each release that goes out as a zip.
//
// A character file records the version it was written by. When that does not match, the select
// screen greys the character out rather than loading it: a save written before a mechanic existed
// has no sensible value for it, and guessing one quietly is how a character ends up half in the
// old world and half in the new. The message says so, and says the door is not closed.
//
// WHEN TO BUMP IT: whenever a release changes what a character file MEANS in a way that cannot be
// carried forward -- a new persisted field with no sensible default, a changed unit, a rebalanced
// cost that invalidates what was bought. A part that is renamed or moves to another class is
// carried forward instead (Equipment.Migrated), and needs no bump. Not for a visual change, a fix,
// or anything a save cannot notice.
public static class Game
{
    public const int Version = 3;

    // WHICH BUILD THIS IS, for a bug report to name. Read once out of the BUILD.txt a release
    // ships beside the executable (tools\pack.ps1 writes it); "dev" in the editor and in every
    // harness, where there is no release. It is DISPLAY ONLY -- Net.Protocol is the identity that
    // decides whether two peers may play, and it is computed by reflection over what the build
    // actually contains.
    //
    // ONE LINE OF PARSING, ON PURPOSE. All the game wants from BUILD.txt is the value of one key,
    // so it reads that key and nothing else.
    //
    // NEVER readonly, NEVER const, and this is not a style note. Net.Fingerprint() folds in every
    // static field that is IsLiteral or IsInitOnly whose type is Plain, and string is Plain -- so
    // a readonly build string would enter the PROTOCOL HASH, and two byte-identical builds packed
    // a minute apart would refuse each other at the handshake. A plain mutable static behind a
    // property is invisible to it; a rung-3 check asserts that structurally.
    private static string _build;
    public static string Build
    {
        get
        {
            if (_build != null) return _build;
            _build = "dev";
            try
            {
                var beside = System.IO.Path.GetDirectoryName(OS.GetExecutablePath());
                var at = System.IO.Path.Combine(beside ?? "", "BUILD.txt");
                if (System.IO.File.Exists(at))
                    foreach (var raw in System.IO.File.ReadAllLines(at))
                        if (raw.StartsWith("build=") && raw.Length > 6) { _build = raw[6..].Trim(); break; }
            }
            catch (System.Exception) { }      // no release beside us: "dev" is the honest answer
            return _build;
        }
    }


    // Shown when a character cannot be loaded because it predates this build. Kept here, beside
    // the number that causes it, rather than in the screen that happens to draw it.
    public const string IncompatibleNote =
        "Too old to load. Importing may come later; for now, make a new one.";

    // THE ONE WAY OUT. The window's close button, the menu's QUIT and the smoke test all come
    // through here, because a bare Quit() cuts off three things that need a moment to finish:
    //   * the session -- saved (a batched save still waiting too), the goodbye heard (up to 2 s:
    //     Net's LetGoMs), the listener closed;
    //   * every sound. The mixer lets go of a stopped playback only on its next cycles, so a
    //     sound still playing at exit was reported as "resources still in use at exit" --
    //     cannon.wav and missile_whoosh.wav, the two long ones, caught mid-battle.
    // The world is paused while it waits, so nothing starts a new sound meanwhile. The wait is in
    // REAL time: at a fixed frame rate a second of game time can pass in a few milliseconds.
    private const ulong MixerMs = 150, NetMs = 10000;
    private static bool _quitting;
    // Set as the engine is told to stop: a listener thread still out past the wait must not hand
    // anything back to an engine that is shutting down.
    public static bool ShuttingDown { get; private set; }
    public static async void Quit(int code = 0)
    {
        if (_quitting) return;
        _quitting = true;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = true;
        Net.I?.Close();
        Music.I?.Silence();
        Sfx.Close();
        bool netBusy() => Net.I != null && !Net.I.NetworkIdle;
        // a goodbye can take its 2 s to be heard: get the window out of the way rather than freeze it
        if (netBusy()) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Minimized);
        ulong start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < MixerMs || (netBusy() && Time.GetTicksMsec() - start < NetMs))
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        ShuttingDown = true;
        tree.Quit(code);
    }
}
