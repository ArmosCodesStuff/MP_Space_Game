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
    [Live] public static readonly Dictionary<string, double> Bonuses = new();
    // pilot progression (see Progression)
    public static int Exp, Level = 1, Points;
    // THE HIGHEST LEVEL THIS PILOT HAS EVER REACHED: what the level walls read (Unlocks). A refit
    // takes a level off and never this, so it can never lock again what a pilot had opened.
    public static int Peak = 1;
    // How many of each upgrade the pilot owns, by Progression.All's index. A property over a MUTABLE
    // field, never a readonly array: Net.Fingerprint reads every readonly array of numbers as a fixed
    // value of the build, so a pilot's purchases would enter the hash two peers are matched on.
    public static int[] Bought { get; private set; } = new int[Progression.All.Length];
    // THE ORDER THE POINTS WERE SPENT IN, one upgrade index per purchase, oldest first. `Bought`
    // says how many of each a pilot owns and never said WHICH it bought last, so a refit could not
    // undo the most recent one. A file written before this has no list: the refit falls back to
    // the dearest point it can see (Progression.Refit).
    [Live] public static readonly List<int> Spent = new();
    // the levels of each boss this pilot has beaten (the +250 first-clear bonus, and unlocking)
    [Live] public static readonly Dictionary<string, HashSet<int>> BossCleared = new();
    // THE TUTORIAL: the hints this pilot has been shown (Hints.All ids), and its off switch (Esc menu)
    [Live] public static readonly HashSet<string> HintsSeen = new();
    public static bool HintsOff;
    // Whether this pilot has been walked through the tour (Tour.cs) -- taken to the end or skipped.
    // Separate from HintsOff, which is the player turning hints off for good.
    public static bool TourDone;
    // Whether this pilot has been walked through the tour (Tour.cs) -- taken to the end or skipped.

    // THE BASE. It belongs to the pilot, not to the session: each character has its own, and a
    // guest visiting someone else's sets its own aside rather than sharing theirs. Yard owns the
    // live numbers and writes them here; this is the copy that reaches disk.
    // The stock BY RESOURCE ID (Gathering.Resources) -- which is also its key in the file, so
    // "ore" and "salvage" are the same keys two fields wrote and a third resource needs no new
    // code here. Credits are not gathered, so they stay their own figure.
    [Live] public static readonly Dictionary<string, double> BaseStock = new();
    public static double BaseCredits;
    [Live] public static readonly Dictionary<string, int> BaseLevels = new();
    // WHAT EACH CORE SLOT HAS BEEN LEVELLED TO with salvage, by the slot's name (Equipment.LevelKey,
    // LevelOf): per pilot, shared by every class it flies, whatever part sits in the slot.
    [Live] public static readonly Dictionary<string, int> GearLevel = new();
    // PARTS THE RECYCLER MAY NOT TOUCH, by id, because the hold counts parts by id and there is no
    // "this copy" for a lock to be about.
    [Live] public static readonly HashSet<string> GearLocked = new();
    public static void ToggleGearLock(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!GearLocked.Remove(id)) GearLocked.Add(id);
        Save();
    }
    [Live] public static readonly Dictionary<string, double> BaseInvested = new();
    // equipment, per class: what is on each ship. The hold is the pilot's: every part owned and not
    // fitted, for any class -- a part taken off goes into it, a part fitted comes out of it. Counts,
    // by part id. (GearHold, not Hold: Hold is already a member of four other types.)
    [Live] public static readonly Dictionary<ShipClass, string[]> Loadout = new();
    public static string[] LoadoutFor(ShipClass c) =>
        Loadout.TryGetValue(c, out var l) ? l : Loadout[c] = Equipment.Default(c);
    [Live] public static readonly Dictionary<string, int> GearHold = new();
    public static void Stow(string id) { if (Equipment.ById(id) != null) GearHold[id] = GearHold.GetValueOrDefault(id) + 1; }
    public static bool Unstow(string id)
    {
        if (id == null || !GearHold.TryGetValue(id, out var n) || n <= 0) return false;
        if (n == 1) GearHold.Remove(id); else GearHold[id] = n - 1;
        return true;
    }
    // LOOT a boss dropped for this pilot and not yet flown over. On disk from the moment of the kill,
    // so a quit, a crash or a lost host costs nothing: the next world this pilot enters claims them.
    [Live] public static readonly List<string> Unclaimed = new();
    // THE KILLS THIS PILOT HAS BEEN PAID FOR, by the host's serial (the last 32). A kill can reach a
    // pilot twice -- at the kill and again as owed after a drop -- and on its file, not in memory, it
    // is paid once even when the game was restarted in between.
    [Live] public static readonly List<long> PaidKills = new();
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
        Exp = 0; Level = 1; Peak = 1; Points = 0; Array.Clear(Bought); BossCleared.Clear(); Loadout.Clear(); GearHold.Clear(); Unclaimed.Clear(); PaidKills.Clear();
        GearLevel.Clear(); GearLocked.Clear();
        HintsSeen.Clear(); HintsOff = false; TourDone = false;
        BaseCredits = 0; BaseStock.Clear(); BaseLevels.Clear(); BaseInvested.Clear();
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
        c.SetValue("progress", "peak", Peak);
        for (int i = 0; i < Bought.Length; i++) c.SetValue("progress", "bought_" + Progression.All[i].Id, Bought[i]);
        // The ids, not the indices: a row added or moved in Progression.All would otherwise turn
        // one pilot's rudder into another's hull.
        c.SetValue("progress", "spent", string.Join(",", Spent.Select(i => Progression.All[i].Id)));
        foreach (var kv in BossCleared) c.SetValue("boss_cleared", kv.Key, string.Join(",", kv.Value.OrderBy(x => x)));
        foreach (var kv in GearLevel) if (kv.Value > 0) c.SetValue("gear_level", kv.Key, kv.Value);
        c.SetValue("gear", "locked", string.Join(",", GearLocked.OrderBy(x => x, StringComparer.Ordinal)));
        c.SetValue("hints", "tour_done", TourDone);
        // ONE KEY PER RESOURCE ID, in table order, then the credits: "ore", "salvage", "credits",
        // the same three keys in the same order two named fields wrote.
        foreach (var r in Gathering.Resources) c.SetValue("base", r, BaseStock.GetValueOrDefault(r));
        c.SetValue("base", "credits", BaseCredits);
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

    // EVERY DOUBLE THIS FILE TAKES OFF DISK COMES THROUGH HERE: finite, and inside the bounds that
    // mean something for it. The three stock figures used Math.Max(0, x), which passes NaN and
    // +Infinity straight through (Math.Max returns NaN when either argument is NaN), and the bonus
    // sheet had no bound at all -- and a bonus reaches combat through PlayerShip's percentage
    // sheet, while an endless balance makes every cost affordable for ever and is written straight
    // back to disk on the next save. A broken number reads as nothing.
    private const double MaxStock = 1e12;      // the most of a resource a FILE may claim to hold
    private const double MaxBonus = 10;        // +1000%: a sheet bonus past this is an edited file
    private static double Num(ConfigFile c, string section, string key, double min, double max)
    {
        double v = (double)c.GetValue(section, key, 0.0);
        return Math.Clamp(double.IsFinite(v) ? v : 0, min, max);
    }

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
            foreach (var k in c.GetSectionKeys("bonus")) Bonuses[k] = Num(c, "bonus", k, -1, MaxBonus);
        // The base. Negative stock is refused outright, an unknown upgrade id is dropped and a level
        // is held to its upgrade's cap: this is a file on the player's disk, and a level for an
        // upgrade that no longer exists, or more levels than it has, would be spent money nothing
        // can show.
        // A RESOURCE THIS FILE HAS NEVER HEARD OF READS 0, not a refusal: that is the whole of
        // what a new gatherer costs a pilot's existing save.
        BaseStock.Clear();
        foreach (var r in Gathering.Resources) BaseStock[r] = Num(c, "base", r, 0, MaxStock);
        BaseCredits = Num(c, "base", "credits", 0, MaxStock);
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
                BaseInvested[k] = Num(c, "base_invested", k, 0, MaxStock);
        Exp = (int)c.GetValue("progress", "exp", 0); Level = Math.Max(1, (int)c.GetValue("progress", "level", 1));
        Peak = Math.Max(Level, (int)c.GetValue("progress", "peak", 0));      // never below the level: a file without it reads its level
        Points = Math.Max(0, (int)c.GetValue("progress", "points", 0));
        for (int i = 0; i < Bought.Length; i++) Bought[i] = Math.Clamp((int)c.GetValue("progress", "bought_" + Progression.All[i].Id, 0), 0, Progression.MaxPerUpgrade);
        Spent.Clear();
        foreach (var spentId in ((string)c.GetValue("progress", "spent", "")).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int at = Array.FindIndex(Progression.All, u => u.Id == spentId); // an id this build no longer has counts for nothing
            if (at >= 0) Spent.Add(at);
        }
        // A part id this build does not know reads as nothing (the slot takes the class's own kit). A
        // dropped part the file has fitted where it may not fly is never lost: it goes into the hold.
        // That is a part that no longer fits its slot (the slot takes the class's own kit), a chip in
        // a slot this pilot's peak has not opened, and a chip over its kind's cap (Equipment.Sanitize,
        // read with the peak loaded above).
        Loadout.Clear();
        var displaced = new List<string>();
        foreach (ShipClass sc in Enum.GetValues(typeof(ShipClass)))
            if (c.HasSectionKey("equipment", sc.ToString()))
            {
                var ids = ((string)c.GetValue("equipment", sc.ToString(), "")).Split(',');
                var clean = Equipment.Sanitize(sc, ids, Peak);
                for (int k = 0; k < ids.Length && k < Equipment.Slots; k++)
                    if (Equipment.ById(ids[k]) is { Kit: false } part && clean[k] != part.Id) displaced.Add(part.Id);
                Loadout[sc] = clean;
            }
        // The hold, loot and hints: parts and hints this build knows, and counts above zero.
        // (`gid`, not `id`: `id` is the CHARACTER's id, the parameter this method was called with.)
        GearHold.Clear();
        if (c.HasSection("gear_hold"))
            foreach (var gid in c.GetSectionKeys("gear_hold"))
                if (Equipment.ById(gid) != null && (int)c.GetValue("gear_hold", gid, 0) is var n && n > 0)
                    GearHold[gid] = GearHold.GetValueOrDefault(gid) + n;
        foreach (var gid in displaced) Stow(gid);
        Unclaimed.Clear();
        Unclaimed.AddRange(((string)c.GetValue("loot", "unclaimed", "")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Where(gid => Equipment.ById(gid) is { Kit: false }));
        PaidKills.Clear();
        foreach (var t in ((string)c.GetValue("loot", "paid", "")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (long.TryParse(t, out var serial)) PayOnce(serial);
        HintsSeen.Clear();
        // An unlock card's id (Unlock.Hint) is no row of Hints.All (D15): the same query Hints.Card
        // uses is the one that keeps it, or a wall crossed on the class this file was flying gets
        // dropped silently on every load.
        foreach (var h in ((string)c.GetValue("hints", "seen", "")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Hints.Card(h) != null) HintsSeen.Add(h);
        HintsOff = (bool)c.GetValue("hints", "off", false);
        BossCleared.Clear();
        TourDone = (bool)c.GetValue("hints", "tour_done", false);
        GearLevel.Clear(); GearLocked.Clear();
        foreach (var lockedId in ((string)c.GetValue("gear", "locked", "")).Split(',', StringSplitOptions.RemoveEmptyEntries))
            GearLocked.Add(lockedId);
        // What is there is taken in like a claim off the wire (Equipment.SanitizeLevels: core slot
        // names, 1-40); anything else in the section is left behind (saves are disregarded).
        if (c.HasSection("gear_level"))
            foreach (var (lid, lv) in Equipment.SanitizeLevels(c.GetSectionKeys("gear_level")
                         .Select(gid => (gid, (int)c.GetValue("gear_level", gid, 0)))))
                GearLevel[lid] = lv;
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
