using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

// Who you are. Your ship is your character, so this travels with you between
// your own world and anyone else's -- it is the one piece of player state that
// is NOT host-owned, because it is cosmetic and identity, not a resource.
//
// Several characters, Terraria-style: one file each in user://characters/, picked
// on the select screen. The static fields are the character currently in play.
public static class Character
{
    public const string Dir = "user://characters";

    // A new pilot, and whatever a file or a packet leaves out. One place: these were written out
    // nine times across six files. Nested, so the smoke test's inventory of what a character SAVES
    // does not mistake them for fields.
    public static class Defaults
    {
        public const string Name = "Commander";
        public static readonly Color Main = new(0.60f, 0.60f, 0.60f);     // hull: grey
        public static readonly Color Accent = new(1.00f, 1.00f, 1.00f);   // turrets, engines, trim: white
    }

    public static string Id = "";           // file stem; empty = nothing loaded
    public static string Name = Defaults.Name;
    public static Color Main   = Defaults.Main;
    public static Color Accent = Defaults.Accent;
    public static ShipClass Class = ShipClass.Battleship;

    // Stat bonuses as fractions keyed by stat id (see ShipStats). Saved with the
    // character; nothing grants any yet.
    public static readonly Dictionary<string, double> Bonuses = new();
    // pilot progression (see Progression)
    public static int Exp, Level = 1, Points;
    public static readonly int[] Bought = new int[Progression.All.Length];
    // the levels of each boss this pilot has beaten (the +250 first-clear bonus, and unlocking)
    public static readonly Dictionary<string, HashSet<int>> BossCleared = new();
    // THE TUTORIAL: the hints this pilot has been shown (Hints.All ids), and its off switch (Esc menu)
    public static readonly HashSet<string> HintsSeen = new();
    public static bool HintsOff;

    // THE BASE. It belongs to the pilot, not to the session: each character has its own, and a
    // guest visiting someone else's sets its own aside rather than sharing theirs. Yard owns the
    // live numbers and writes them here; this is the copy that reaches disk.
    public static double BaseOre, BaseSalvage, BaseCredits;
    public static readonly Dictionary<string, int> BaseLevels = new();
    public static readonly Dictionary<string, double> BaseInvested = new();
    // equipment, per class: what is on each ship. The hold is the pilot's: every part owned and not
    // fitted, for any class -- a part taken off goes into it, a part fitted comes out of it. Counts,
    // by part id. (GearHold, not Hold: Hold is already a member of four other types.)
    public static readonly Dictionary<ShipClass, string[]> Loadout = new();
    public static string[] LoadoutFor(ShipClass c) =>
        Loadout.TryGetValue(c, out var l) ? l : Loadout[c] = Equipment.Default(c);
    public static readonly Dictionary<string, int> GearHold = new();
    public static void Stow(string id) { if (Equipment.ById(id) != null) GearHold[id] = GearHold.GetValueOrDefault(id) + 1; }
    public static bool Unstow(string id)
    {
        if (id == null || !GearHold.TryGetValue(id, out var n) || n <= 0) return false;
        if (n == 1) GearHold.Remove(id); else GearHold[id] = n - 1;
        return true;
    }
    // LOOT a boss dropped for this pilot and not yet flown over. On disk from the moment of the kill,
    // so a quit, a crash or a lost host costs nothing: the next world this pilot enters claims them.
    public static readonly List<string> Unclaimed = new();
    // THE KILLS THIS PILOT HAS BEEN PAID FOR, by the host's serial (the last 32). A kill can reach a
    // pilot twice -- at the kill and again as owed after a drop -- and on its file, not in memory, it
    // is paid once even when the game was restarted in between.
    public static readonly List<long> PaidKills = new();
    private const int PaidKept = 32;
    public static bool PayOnce(long serial)
    {
        if (PaidKills.Contains(serial)) return false;
        PaidKills.Add(serial);
        if (PaidKills.Count > PaidKept) PaidKills.RemoveRange(0, PaidKills.Count - PaidKept);
        return true;
    }

    // One saved character, as the select screen lists it.
    public class Slot
    {
        public string Id, Name;
        public Color Main, Accent;
        public ShipClass Class;
        public int Version;                       // the build that wrote it
        public bool Playable => Version == Game.Version;
    }

    private static string PathOf(string id) => $"{Dir}/{id}.cfg";

    // A fresh, unsaved character with its own id. Nothing touches disk until Save().
    public static void NewBlank()
    {
        SaveIfPending();                                   // the pilot being replaced keeps what it had
        Id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        (Name, Main, Accent, Class) = (Defaults.Name, Defaults.Main, Defaults.Accent, ShipClass.Battleship);
        Bonuses.Clear();
        Exp = 0; Level = 1; Points = 0; Array.Clear(Bought); BossCleared.Clear(); Loadout.Clear(); GearHold.Clear(); Unclaimed.Clear(); PaidKills.Clear();
        HintsSeen.Clear(); HintsOff = false;
        BaseOre = BaseSalvage = BaseCredits = 0; BaseLevels.Clear(); BaseInvested.Clear();
    }

    // THE BATCHED SAVE, for things that come in runs -- loot picked up crate after crate. Each call
    // (re)starts a 2.5 s wait and the file is written once, when the calls stop; any Save() in the
    // meantime makes it unnecessary. The wait counts GAME time, pumped by the Net autoload, which
    // outlives every scene; leaving a session, quitting or loading another pilot writes a pending
    // save at once (Net.Close, Load, NewBlank). It never makes a file: a pilot not yet saved is not
    // written by a pickup.
    private const double SaveDelay = 2.5;
    private static double _saveIn;
    public static void SaveSoon()
    {
        if (string.IsNullOrEmpty(Id) || !FileAccess.FileExists(PathOf(Id))) return;
        _saveIn = SaveDelay;
    }
    public static void TickSave(double delta)
    {
        if (_saveIn <= 0) return;
        if ((_saveIn -= delta) <= 0) Save();
    }
    public static void SaveIfPending() { if (_saveIn > 0) Save(); }

    public static void Save()
    {
        _saveIn = 0;                                       // whatever was waiting is in this one
        if (string.IsNullOrEmpty(Id)) return;
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        var c = new ConfigFile();
        c.SetValue("id", "version", Game.Version);      // the build that wrote it, always THIS one
        c.SetValue("id", "name", Name);
        c.SetValue("id", "main", Main);
        c.SetValue("id", "accent", Accent);
        c.SetValue("id", "class", (int)Class);
        foreach (var kv in Bonuses) c.SetValue("bonus", kv.Key, kv.Value);
        c.SetValue("progress", "exp", Exp); c.SetValue("progress", "level", Level); c.SetValue("progress", "points", Points);
        for (int i = 0; i < Bought.Length; i++) c.SetValue("progress", "bought_" + Progression.All[i].Id, Bought[i]);
        foreach (var kv in BossCleared) c.SetValue("boss_cleared", kv.Key, string.Join(",", kv.Value.OrderBy(x => x)));
        c.SetValue("base", "ore", BaseOre); c.SetValue("base", "salvage", BaseSalvage); c.SetValue("base", "credits", BaseCredits);
        foreach (var kv in BaseLevels) c.SetValue("base_levels", kv.Key, kv.Value);
        foreach (var kv in BaseInvested) c.SetValue("base_invested", kv.Key, kv.Value);
        foreach (var kv in Loadout) c.SetValue("equipment", kv.Key.ToString(), string.Join(",", kv.Value));
        foreach (var kv in GearHold.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal)) c.SetValue("gear_hold", kv.Key, kv.Value);
        c.SetValue("loot", "unclaimed", string.Join(",", Unclaimed));
        c.SetValue("loot", "paid", string.Join(",", PaidKills));
        c.SetValue("hints", "seen", string.Join(",", HintsSeen.OrderBy(x => x, StringComparer.Ordinal)));
        c.SetValue("hints", "off", HintsOff);

        if (WriteAtomically(c, PathOf(Id)) is bool renamed) LastSaveRenamed = renamed;
    }

    // Write a temp file and rename it over the real one, never straight onto it. ConfigFile.Save
    // truncates and rewrites in place, so anything reading the same path at that moment sees a
    // half-written file: Load and List both hit "ConfigFile parse error ... Unterminated string"
    // and the character silently fails to load or drops out of the list. Two instances share one
    // user:// in the smoke test and in "Run Multiple Instances", so this is reachable, not
    // theoretical. After the rename the live path only ever holds a complete file. The temp is
    // not ".cfg", so List() skips it if one is ever left. Settings saves the same way.
    // null: nothing could be written; otherwise whether the rename -- the safe path -- ran.
    internal static bool? WriteAtomically(ConfigFile c, string path)
    {
        var tmp = path + ".tmp";
        if (c.Save(tmp) != Error.Ok) return null;
        if (DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(path)) == Error.Ok) return true;
        // Losing the save outright is worse than the race it was avoiding.
        c.Save(path);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tmp));
        return false;
    }

    // Did the last Save go through the rename, or fall back to writing over the live file?
    // Both paths end with the right contents on disk and no temp file left, so a check that
    // only looks at the files CANNOT TELL THEM APART -- and the rename is the whole point.
    // Windows' rename() refuses an existing destination, so if Godot ever stopped working
    // around that, every save would quietly take the unsafe path and nothing would say so.
    public static bool LastSaveRenamed { get; private set; }

    public static bool Load(string id)
    {
        SaveIfPending();                                   // the pilot being replaced keeps what it had
        var c = new ConfigFile();
        if (c.Load(PathOf(id)) != Error.Ok) return false;
        // A file from another build is REFUSED here, not repaired -- by the same rule the select
        // screen greys a row with (Slot.Playable). Load() is the only way a character reaches
        // memory, so this is the one place that has to hold: a guest joining, a smoke test, or a
        // future entry point all come through here. A file from before versioning reads 0.
        var s = ReadSlot(c, id);
        if (!s.Playable) return false;
        // Only NOW: a refused load must leave nothing behind, and Id is what Save() writes to.
        // Set before the check, a refused character would still be the one the next save
        // overwrites.
        (Id, Name, Main, Accent, Class) = (id, s.Name, s.Main, s.Accent, s.Class);
        Bonuses.Clear();
        if (c.HasSection("bonus"))
            foreach (var k in c.GetSectionKeys("bonus")) Bonuses[k] = (double)c.GetValue("bonus", k, 0.0);
        // The base. Negative stock is refused outright, an unknown upgrade id is dropped and a level
        // is held to its upgrade's cap: this is a file on the player's disk, and a level for an
        // upgrade that no longer exists, or more levels than it has, would be spent money nothing
        // can show.
        BaseOre = Math.Max(0, (double)c.GetValue("base", "ore", 0.0));
        BaseSalvage = Math.Max(0, (double)c.GetValue("base", "salvage", 0.0));
        BaseCredits = Math.Max(0, (double)c.GetValue("base", "credits", 0.0));
        BaseLevels.Clear();
        if (c.HasSection("base_levels"))
            foreach (var k in c.GetSectionKeys("base_levels"))
            {
                var up = Economy.ById(k);
                if (up != null) BaseLevels[k] = Math.Clamp((int)c.GetValue("base_levels", k, 0), 0, up.Max);
            }
        BaseInvested.Clear();
        if (c.HasSection("base_invested"))
            foreach (var k in c.GetSectionKeys("base_invested"))
                BaseInvested[k] = Math.Max(0, (double)c.GetValue("base_invested", k, 0.0));
        Exp = (int)c.GetValue("progress", "exp", 0); Level = Math.Max(1, (int)c.GetValue("progress", "level", 1));
        Points = Math.Max(0, (int)c.GetValue("progress", "points", 0));
        for (int i = 0; i < Bought.Length; i++) Bought[i] = Math.Clamp((int)c.GetValue("progress", "bought_" + Progression.All[i].Id, 0), 0, Progression.MaxPerUpgrade);
        // Every part id read from the file goes through Equipment.Migrated first (see there). A
        // dropped part that no longer fits the slot it was fitted in is not lost: it goes into the
        // hold, and the slot takes the class's own kit.
        Loadout.Clear();
        var displaced = new List<string>();
        foreach (ShipClass sc in Enum.GetValues(typeof(ShipClass)))
            if (c.HasSectionKey("equipment", sc.ToString()))
            {
                var ids = ((string)c.GetValue("equipment", sc.ToString(), "")).Split(',').Select(Equipment.Migrated).ToArray();
                for (int k = 0; k < ids.Length && k < Equipment.Slots; k++)
                    if (Equipment.ById(ids[k]) is { Kit: false } part && !Equipment.Fits(part, Equipment.SlotAt(k), sc)) displaced.Add(part.Id);
                Loadout[sc] = Equipment.Sanitize(sc, ids);
            }
        // The hold, loot and hints: parts and hints this build knows, and counts above zero.
        // (`gid`, not `id`: `id` is the CHARACTER's id, the parameter this method was called with.)
        GearHold.Clear();
        if (c.HasSection("gear_hold"))
            foreach (var gid in c.GetSectionKeys("gear_hold"))
                if (Equipment.Migrated(gid) is var mid && Equipment.ById(mid) != null && (int)c.GetValue("gear_hold", gid, 0) is var n && n > 0)
                    GearHold[mid] = GearHold.GetValueOrDefault(mid) + n;
        foreach (var gid in displaced) Stow(gid);
        Unclaimed.Clear();
        Unclaimed.AddRange(((string)c.GetValue("loot", "unclaimed", "")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Select(Equipment.Migrated).Where(gid => Equipment.ById(gid) is { Kit: false }));
        PaidKills.Clear();
        foreach (var t in ((string)c.GetValue("loot", "paid", "")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (long.TryParse(t, out var serial)) PayOnce(serial);
        HintsSeen.Clear();
        foreach (var h in ((string)c.GetValue("hints", "seen", "")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Hints.All.ContainsKey(h)) HintsSeen.Add(h);
        HintsOff = (bool)c.GetValue("hints", "off", false);
        BossCleared.Clear();
        if (c.HasSection("boss_cleared"))
            foreach (var k in c.GetSectionKeys("boss_cleared"))
                BossCleared[k] = ((string)c.GetValue("boss_cleared", k, "")).Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n >= 1).ToHashSet();
        return true;
    }

    // A pilot still in an earlier default -- the pale blue hull that left the line-art hulls near
    // white, the blue that followed it, the gold accent -- never chose it, only inherited it: they
    // are given the default as it is now.
    private static Color SavedHull(Color c) =>
        c.IsEqualApprox(new Color(0.55f, 0.72f, 1.00f)) || c.IsEqualApprox(new Color(0.30f, 0.50f, 0.95f)) ? Defaults.Main : c;
    private static Color SavedAccent(Color c) => c.IsEqualApprox(new Color(1.00f, 0.78f, 0.35f)) ? Defaults.Accent : c;

    // The [id] section: what the select screen lists, and what Load checks before anything else.
    private static Slot ReadSlot(ConfigFile c, string id) => new()
    {
        Id = id,
        Version = (int)c.GetValue("id", "version", 0),
        Name = (string)c.GetValue("id", "name", Defaults.Name),
        Main = SavedHull((Color)c.GetValue("id", "main", Defaults.Main)),
        Accent = SavedAccent((Color)c.GetValue("id", "accent", Defaults.Accent)),
        Class = Classes.Sanitize((int)c.GetValue("id", "class", 0)),
    };

    // Every saved character, oldest first (ids are creation timestamps).
    public static List<Slot> List()
    {
        var list = new List<Slot>();
        using var d = DirAccess.Open(Dir);
        if (d == null) return list;
        var files = new List<string>(d.GetFiles());
        files.Sort(StringComparer.Ordinal);
        foreach (var f in files)
        {
            if (!f.EndsWith(".cfg")) continue;
            var c = new ConfigFile();
            if (c.Load($"{Dir}/{f}") != Error.Ok) continue;
            list.Add(ReadSlot(c, f[..^4]));
        }
        return list;
    }

    // Removes the character's file from disk. There is no undo; the select screen
    // asks first.
    public static bool Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var err = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PathOf(id)));
        if (id == Id) { Id = ""; _saveIn = 0; }
        return err == Error.Ok;
    }

    // Makes sure SOMETHING is loaded, for a hub entered without the select screen
    // (running Hub.tscn directly from the editor, or the smoke test).
    public static void EnsureLoaded()
    {
        if (!string.IsNullOrEmpty(Id) && FileAccess.FileExists(PathOf(Id))) return;
        // The first one that CAN load: taking the oldest, even when it is from another build,
        // made a new character on every direct entry while a playable one sat further down.
        if (List().FirstOrDefault(s => s.Playable) is { } s && Load(s.Id)) return;
        NewBlank(); Save();
    }
}

// The nine classes, three to a page. Three are flyable; the rest are declared so
// the selector, the save format and the UI are all built for nine from the start
// rather than being widened later.
public static class Classes
{
    public class Entry
    {
        public ShipClass Id;
        public string Name, Blurb;
        public bool Ready;
    }

    public static readonly Entry[] All =
    {
        // page 1 — line
        new() { Id = ShipClass.Battleship, Name = "BATTLESHIP", Ready = true,
                Blurb = "300 hull. Four cursor-aimed main guns, a broadside of all four, two point-defence turrets." },
        new() { Id = ShipClass.Carrier, Name = "CARRIER", Ready = true,
                Blurb = "200 hull. Three point-defence turrets, three fighters, two torpedo bombers." },
        new() { Id = ShipClass.Destroyer, Name = "DESTROYER", Ready = true,
                Blurb = "250 hull. The fastest. Two cursor-aimed main guns, missile bursts of three, two point-defence turrets." },
        // page 2 — strike
        new() { Id = ShipClass.Battleship, Name = "CORSAIR",    Ready = false, Blurb = "Reserved." },
        new() { Id = ShipClass.Battleship, Name = "INTERDICTOR",Ready = false, Blurb = "Reserved." },
        new() { Id = ShipClass.Battleship, Name = "LANCER",     Ready = false, Blurb = "Reserved." },
        // page 3 — support
        new() { Id = ShipClass.Battleship, Name = "TENDER",     Ready = false, Blurb = "Reserved." },
        new() { Id = ShipClass.Battleship, Name = "WARDEN",     Ready = false, Blurb = "Reserved." },
        new() { Id = ShipClass.Battleship, Name = "PROSPECTOR", Ready = false, Blurb = "Reserved." },
    };

    public const int PerPage = 3;
    public static int Pages => (All.Length + PerPage - 1) / PerPage;

    // A class number from a file or a packet: one the game has, or the Battleship.
    public static ShipClass Sanitize(int cls) => Enum.IsDefined(typeof(ShipClass), cls) ? (ShipClass)cls : ShipClass.Battleship;
    // Its name as the screens print it ("BATTLESHIP"), from the one table that holds it.
    public static string NameOf(ShipClass c) => All.First(e => e.Id == c).Name;

    // What each class carries -- asked of the class, never "is it the battleship", so a new class
    // is one line here rather than a hunt for every two-way test.
    public static bool Guns(ShipClass c)      => c is ShipClass.Battleship or ShipClass.Destroyer;   // cursor-aimed main turrets
    public static bool Broadside(ShipClass c) => c == ShipClass.Battleship;
    public static bool Missiles(ShipClass c)  => c == ShipClass.Destroyer;                           // a magazine of missile bursts
    public static bool Wing(ShipClass c)      => c == ShipClass.Carrier;                             // fighters and bombers
}
