using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// THE SESSION'S BOOK — what outlives a world.
//
// Cut out of Hub.cs, which is where every static in the game's world code lived, and for one
// reason: a host reloads its own scene on every trip (home to the arena and back), and none of
// this may be lost when it does. Three things are the session's, not the world's --
//   SECTORS   which world each guest is in, so home-only traffic reaches only peers at home
//   PLACES    the place held for a pilot whose connection dropped: 90 s, by CHARACTER ID, which
//             is the guest's own claim (a peer id changes on a reconnect, and the host has no
//             other way to know a returning player -- Hub.NetIdentity refuses an id a live peer
//             is already flying)
//   KILLS     the boss kills of the last 12 s, because a pilot whose link died just before one is
//             still in the party when it happens -- the host notices a dead link seconds later --
//             and would otherwise never be paid
// Session.End() is the whole of forgetting them. Hub keeps the RPCs and the per-world glue: who
// is away, and whose place is waiting to be delivered.
// ─────────────────────────────────────────────────────────────────────────────
public static class Session
{
    // A DROPPED PILOT'S PLACE: its party slot and its votes, and, in the same world, its hull,
    // its stasis and where it was. The boss is scaled by the ships present, and a kill while it is
    // away is owed to it: its EXP, its bounty share and its crates arrive when it does. A pilot
    // that leaves on purpose gives its place up.
    public sealed class Held
    {
        public int OldPeer, World; public string Name = ""; public ShipClass Class; public ulong Until;
        public Vector2 Pos; public float Rot; public double Hp, Stasis; public bool Alive = true;
        public readonly List<Kill> Owed = new();
    }

    // A BOSS KILL, ONCE FOR EACH PILOT. Every kill has a serial, unique to the host that announced
    // it: a pilot is paid for a serial once, however the payment reaches it -- at the kill, or
    // owed when it comes back from a drop. The serials a pilot has been paid are on its file
    // (Character.PaidKills), so not even a restart in between pays one twice.
    public sealed class Kill
    {
        // Kind: which row of Missions.Kinds was cleared. An owed clear may be paid in a world
        // flying a different operation, and it decides which LADDER the clear counts on, so it
        // travels with the kill rather than being read off Missions.Kind when it lands.
        public long Serial; public int Kind, Level, Party, World; public Vector2 At; public ulong When;
        public Dictionary<int, string[]> Drops; public HashSet<int> Present;
    }

    public const double HoldFor = 90;
    public const ulong OwedWindowMs = 12000;

    [Live] public static readonly Dictionary<int, Hub.SectorKind> Sectors = new();
    [Live] public static readonly Dictionary<string, Held> Places = new();
    [Live] public static readonly List<Kill> Kills = new();

    private static long _serial = DateTime.UtcNow.Ticks;
    private static int _worlds;
    public static long NextKill() => ++_serial;
    // THE TRIPS (audit P9): the host counts its sector moves, and a guest keeps the last one it made,
    // so a report from before a move and a move already made are both told apart from news. -1: none.
    public static int Trip, HeardTrip = -1;
    public static int NextWorld() => ++_worlds;          // this world, of all the host has built

    // A session over (offline, a guest now, the main menu): all three go with it.
    public static void End() { Sectors.Clear(); Places.Clear(); Kills.Clear(); Tokens.Clear(); HeardTrip = -1; }

    // THE REJOIN TOKEN (audit P10b). A pilot's character id is its own word, so on its own it let
    // anyone who knew it take a held place, and it could not take back a place whose old connection
    // the host had not yet seen die. The host issues each pilot a token once it is in (Hub.NetToken);
    // the pilot keeps it (Rejoin: never forgotten by End, since a drop ends the guest's session) and
    // sends it with its identity. Once a token is issued for an id, that id's place is the token's.
    // It is carried after connection, never in an invite code (network_webrtc.md §3.9).
    [Live] public static readonly Dictionary<string, string> Tokens = new();
    public static string Rejoin = "";
    public static string TokenFor(string id)
    {
        if (!Tokens.TryGetValue(id, out var t))
            Tokens[id] = t = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        return t;
    }
    // May a peer announcing `id` with `token` have that pilot's place? A live peer still holding it
    // gives it up only to its token; otherwise an id with no token issued is claimed by name, as
    // before there were tokens.
    public static bool MayClaim(string id, string token, bool liveHolder)
    {
        bool issued = Tokens.TryGetValue(id, out var t), match = issued && t == token;
        return liveHolder ? match : !issued || match;
    }

    public static Hub.SectorKind? SectorOf(int id) => Sectors.TryGetValue(id, out var s) ? s : null;

    // the place to hold for a pilot that dropped without saying goodbye
    public static Held Hold(int peer, Net.PlayerInfo info, int world, PlayerShip ship)
    {
        var h = new Held { OldPeer = peer, World = world, Name = info.Name, Class = info.Class,
                           Until = Time.GetTicksMsec() + (ulong)(HoldFor * 1000) };
        if (ship != null && GodotObject.IsInstanceValid(ship))
            (h.Pos, h.Rot, h.Hp, h.Alive, h.Stasis) = (ship.Position, ship.Rotation, ship.Hp, ship.Alive, ship.StasisLeft);
        return h;
    }

    // the kills in the moments before a drop that this pilot was still in the party for (paid
    // once, by serial)
    public static IEnumerable<Kill> Owed(int peer, int world) =>
        Kills.Where(k => k.World == world && k.Present.Contains(peer) && Time.GetTicksMsec() - k.When <= OwedWindowMs);

    public static void Note(Kill k)
    {
        Kills.RemoveAll(x => k.When - x.When > OwedWindowMs);
        Kills.Add(k);
    }

    // the character ids whose hold has run out
    public static List<string> Lapsed()
    {
        ulong now = Time.GetTicksMsec();
        return Places.Where(kv => kv.Value.Until <= now).Select(kv => kv.Key).ToList();
    }
}
