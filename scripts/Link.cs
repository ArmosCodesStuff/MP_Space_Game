using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// THE TRANSPORT: THE ONE PLACE THAT NAMES WEBRTC (docs/plans/network_webrtc.md §7). The official
// Godot webrtc-native plugin, vendored in addons/webrtc_native/, supplies the implementation of the
// WebRtc* classes that core GodotSharp declares; this file owns what the game needs to know about it.
// It grows slice by slice: R0 (this) holds the plugin row, the presence test, the channel table, the
// sealing rule and the one way to hang up. R1 adds the STUN rows and the walk, R2 moves the session
// onto it and deletes ENet's SetTimeout, ThrottleConfigure, RoundTripTime and PeerDisconnectLater.
//
// A PLUGIN UPGRADE IS A ROW: `Plugin` names the library, its version, the native class it registers
// and its files; nothing else in the game names them. It is a readonly record, so it is part of the
// build's fingerprint: two builds on different plugins do not meet.
//
// TRAP WHILE S1 IS IN THE TREE: Net's private `Link(ENetPacketPeer, int)` hides this class inside Net
// (CS0119), so until R2 deletes that method only code outside Net names `Link`.
public static class Link
{
    public sealed record PluginRow(string Name, string Version, string NativeClass, string Extension, string ReleaseDll);
    public static readonly PluginRow Plugin = new("webrtc-native", "1.2.1", "WebRTCLibPeerConnection",
        "res://addons/webrtc_native/webrtc_native.gdextension", "libwebrtc_native.windows.template_release.x86_64.dll");

    // IS THE PLUGIN LOADED? Not a return code: with the DLL missing (or quarantined) the engine
    // still answers Initialize, CreateOffer and CreateServer with Ok, and only a data channel comes
    // back null (SPIKE F4). The class the library registers is the honest test, asked once.
    // Not a readonly field: this machine's files are not part of the build's fingerprint.
    public static bool Available => !PretendMissing && (_loaded ??= ClassDB.ClassExists(Plugin.NativeClass));
    private static bool? _loaded;
    // The smoke test's way to be a machine without the plugin, to prove the gate on a real menu.
    public static bool PretendMissing;

    // THE CHANNELS A SESSION NEGOTIATES, read off every [Rpc] in the game's assembly -- the harness's
    // own stream included, since both ends of a test run are one build (§3.2). Transfer channel N is
    // entry N - 1; a channel whose RPCs all ask for one unreliable mode gets it, anything else
    // (a Reliable RPC on it, two modes, no RPC at all) is Reliable. Channel 0 is the plugin's own
    // three. Each entry becomes its own data channel, so its own SCTP stream: a loss holds back only
    // that row (NetChannels says which stream is which).
    public static Godot.Collections.Array Channels()
    {
        const BindingFlags every = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var rpcs = typeof(Link).Assembly.GetTypes().SelectMany(t => t.GetMethods(every))
            .Select(m => m.GetCustomAttribute<RpcAttribute>()).Where(a => a != null && a.TransferChannel > 0).ToList();
        int top = rpcs.Count == 0 ? 0 : rpcs.Max(a => a.TransferChannel);
        var modes = new Godot.Collections.Array();
        for (int ch = 1; ch <= top; ch++)
        {
            var asked = rpcs.Where(a => a.TransferChannel == ch).Select(a => a.TransferMode).Distinct().ToList();
            modes.Add((int)(asked.Count == 1 ? asked[0] : MultiplayerPeer.TransferModeEnum.Reliable));
        }
        return modes;
    }

    // A BUNDLE IS SEALED ONE POLL AFTER GATHERING SAYS COMPLETE (v1 §2.6 S2): the last candidates
    // arrive through signals inside the poll that reports Complete, so a bundle read in that same
    // frame can miss one. Call once a frame after the connection's peer has been polled; true from
    // the first frame after the one that first saw Complete.
    public static bool Sealed(WebRtcPeerConnection c)
    {
        ulong id = c.GetInstanceId(), frame = Engine.GetProcessFrames();
        if (c.GetGatheringState() != WebRtcPeerConnection.GatheringState.Complete) { _completeAt.Remove(id); return false; }
        if (_completeAt.TryGetValue(id, out ulong at)) return at < frame;
        foreach (var gone in _completeAt.Keys.Where(k => !GodotObject.IsInstanceIdValid(k)).ToList()) _completeAt.Remove(gone);
        _completeAt[id] = frame;
        return false;
    }
    private static readonly Dictionary<ulong, ulong> _completeAt = new();

    // EVERY HANG-UP GOES THROUGH HERE (v1 §2.6 S3): the plugin's remove_peer prints an engine ERROR
    // for an id it does not have, and a run with an ERROR line is a red run. A gone id is a no-op.
    public static void Hang(WebRtcMultiplayerPeer mp, int id)
    {
        if (mp != null && mp.HasPeer(id)) mp.RemovePeer(id);
    }
}
