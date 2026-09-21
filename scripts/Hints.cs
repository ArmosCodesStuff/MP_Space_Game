using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// HINTS -- the tutorial, as the owner asked for it: never in the way. The first time a pilot meets
// a system, a small card in a corner says how it works in a line or two and fades after 8 s. It
// takes no input and blocks nothing; the Esc menu turns hints off; and each character remembers
// the hints it has been shown (Character.HintsSeen), so a veteran pilot sees none and a new one
// sees each once.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Hints : CanvasLayer
{
    // Every hint there is: its id (what a character remembers), a title and a line or two.
    public static readonly Dictionary<string, (string Title, string Body)> All = new()
    {
        ["flight"]      = ("FLYING", "W ahead, S astern, A / D the rudder. A capital ship turns on a radius; almost stopped, it pivots slowly. Every key is listed along the bottom of the screen."),
        ["target"]      = ("TARGETING", "Tab takes the nearest enemy; left-click one, or click it on the radar. Esc lets it go."),
        ["abilities"]   = ("ABILITIES", "The bar along the bottom shows each ability and its key. K lists every number your ship flies and fights with, and changes any ability's key."),
        ["base"]        = ("THE BASE", "B, or left-click the station. Miners, salvagers and the hauler earn while you fly; spend the credits here. REFIT changes your ship."),
        ["tio"]         = ("THE TIO", "Left-click the building for boss missions. When every pilot is READY the portal opens and the party goes through together."),
        ["equipment"]   = ("EQUIPMENT", "I. Bosses drop parts that lean hard one way; your hold keeps what you are not flying. Fit them here; each class keeps its own."),
        ["loot"]        = ("LOOT", "Crates only you can see. Fly over one to take it; any you leave behind come home with you."),
        ["multiplayer"] = ("MULTIPLAYER", "HOST THIS WORLD, then COPY ADDRESS for your friends. To join a friend, type their address, then JOIN."),
        ["raid"]        = ("RAIDERS", "They come for your base after a failed mission, and for your hauler on an escort. The base's guns help; miners, salvagers and the hauler cannot fight back."),
        ["hauler"]      = ("THE HAULER", "DISPATCH sends it alone: past the portal it may be lost (EVASION, on the HAULER tab, lowers the risk). ESCORT flies it the long way for five times the pay; keep the raiders off it."),
        ["pilot"]       = ("A LEVEL UP", "L spends your points on rudder, hull, engines and weapons. Bosses give EXP; the first clear of each level gives more."),
        ["warp"]        = ("WARP", "V charges for 3 s, then jumps you ahead the way the ship is pointing. 30 s to recharge."),
        ["stasis"]      = ("STASIS", "Your ship is held in stasis, not lost. Fly the escape pod clear; when the ship is ready, F re-boards it."),
        ["boss"]        = ("THE BOSS", "Red shapes are its attacks, drawn before they land: get out of them. Beat it for EXP, a bounty and parts."),
    };
}
