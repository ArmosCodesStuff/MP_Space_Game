using Godot;
using System.Collections.Generic;

// EVERY FILE THE GAME LOADS, AND THE ONE PLACE THAT LOADS IT. Replaced a GD.Load at each call site
// (Sprites.Fit, the turrets, the hub's and the title's backdrops, the landmarks, the sky, the thrown
// rock, the hauler, the base's gun, the ship's hull), Sfx's own table of streams and the ship
// preview's texture cache. A new file is one call, Assets.Load<T>("res://..."). The engine's loader
// is called here and nowhere else in the build, the harness included: a rung-3 check reads the
// build's code for any other call.
//
// HELD FOR THE LIFE OF THE PROCESS, AND THAT IS THE POINT, not a speed-up. Godot 4.7.2 keeps only a
// WEAK handle to a resource's C# object while nothing but that object holds the resource -- a texture
// whose last sprite has gone. The collector may then take the C# object and queue its finalizer,
// and until that finalizer runs the resource is alive and still in the engine's cache. A load of the
// same path then is handed the cached resource, and its first reference swaps the dead handle
// (csharp_script.cpp, _instance_binding_reference_callback), blanks it and still calls it weak. If
// the finalizer -- on its own thread -- lets go of its reference before the load has handed the
// resource to C#, every reference after that which brings the count back to two asks .NET to swap a
// null handle: "Handle is not initialized" from ScriptManagerBridge.SwapGCHandleForType, an ERROR in
// the log (one to three per load, by where the finalizer lands; the runs showed two) and a red run. It needs a collection and a finalizer to land inside
// one load, which is why it came and went: a backdrop across a scene change, a raider's hull between
// waves, the thrown rock between throws. A C# object held here is never collected, so its handle is
// never dead.
//
// NOTHING HERE IS RELEASED ON THE WAY OUT, on purpose. The engine disposes every live C# object when
// it shuts C# down -- after the scene tree is freed, before its count of "resources still in use at
// exit" -- as it always has for the Theme Ui.cs keeps in a static. Disposing one earlier, in
// Game.Quit, would hand a node that still draws it a disposed texture.
public static class Assets
{
    [Live] private static readonly Dictionary<string, Resource> _held = new();

    public static T Load<T>(string path) where T : Resource
    {
        if (_held.TryGetValue(path, out var r)) return (T)r;
        var loaded = GD.Load<T>(path);
        if (loaded != null) _held[path] = loaded;
        return loaded;
    }

    // A copy of its own, which the engine's cache never sees: nothing can hand it out behind its C#
    // object's back, so it needs no holding. The caller owns it and disposes it (Music's two loops).
    public static T Own<T>(string path) where T : Resource =>
        (T)ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore);
}
