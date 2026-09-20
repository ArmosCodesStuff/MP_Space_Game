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

    public static string Id = "";           // file stem; empty = nothing loaded
    public static string Name = "Commander";
    public static Color Main   = new(0.55f, 0.72f, 1.00f);   // hull
    public static Color Accent = new(1.00f, 0.78f, 0.35f);   // turrets, engines, trim
    public static ShipClass Class = ShipClass.Battleship;

    // Stat bonuses as fractions keyed by stat id (see ShipStats). Saved with the
    // character; nothing grants any yet.
    public static readonly Dictionary<string, double> Bonuses = new();
    // pilot progression (see Progression)
    public static int Exp, Level = 1, Points;
    public static readonly int[] Bought = new int[Progression.All.Length];
    // the levels of each boss this pilot has beaten (the +250 first-clear bonus, and unlocking)
    public static readonly Dictionary<string, HashSet<int>> BossCleared = new();
    // equipment, per class: what is on each ship, and the chips taken off it
    public static readonly Dictionary<ShipClass, string[]> Loadout = new();
    private static readonly Dictionary<ShipClass, List<string>> Spares = new();
    public static string[] LoadoutFor(ShipClass c) =>
        Loadout.TryGetValue(c, out var l) ? l : Loadout[c] = Equipment.Default(c);
    public static List<string> SparesFor(ShipClass c) =>
        Spares.TryGetValue(c, out var l) ? l : Spares[c] = new List<string>();

    // One saved character, as the select screen lists it.
    public class Slot
    {
        public string Id, Name;
        public Color Main, Accent;
        public ShipClass Class;
    }

    private static string PathOf(string id) => $"{Dir}/{id}.cfg";

    // A fresh, unsaved character with its own id. Nothing touches disk until Save().
    public static void NewBlank()
    {
        Id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        Name = "Commander";
        Main = new(0.55f, 0.72f, 1.00f);
        Accent = new(1.00f, 0.78f, 0.35f);
        Class = ShipClass.Battleship;
        Bonuses.Clear();
        Exp = 0; Level = 1; Points = 0; Array.Clear(Bought); BossCleared.Clear(); Loadout.Clear(); Spares.Clear();
    }

    public static void Save()
    {
        if (string.IsNullOrEmpty(Id)) return;
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        var c = new ConfigFile();
        c.SetValue("id", "name", Name);
        c.SetValue("id", "main", Main);
        c.SetValue("id", "accent", Accent);
        c.SetValue("id", "class", (int)Class);
        foreach (var kv in Bonuses) c.SetValue("bonus", kv.Key, kv.Value);
        c.SetValue("progress", "exp", Exp); c.SetValue("progress", "level", Level); c.SetValue("progress", "points", Points);
        for (int i = 0; i < Bought.Length; i++) c.SetValue("progress", "bought_" + Progression.All[i].Id, Bought[i]);
        foreach (var kv in BossCleared) c.SetValue("boss_cleared", kv.Key, string.Join(",", kv.Value.OrderBy(x => x)));
        foreach (var kv in Loadout) c.SetValue("equipment", kv.Key.ToString(), string.Join(",", kv.Value));
        foreach (var kv in Spares) c.SetValue("spares", kv.Key.ToString(), string.Join(",", kv.Value));

        // Write a temp file and rename it over the real one, never straight onto it.
        // ConfigFile.Save truncates and rewrites in place, so anything reading the same
        // path at that moment sees a half-written file: Load and List both hit
        // "ConfigFile parse error ... Unterminated string" and the character silently
        // fails to load or drops out of the list. Two instances share one user:// in the
        // smoke test and in "Run Multiple Instances", so this is reachable, not theoretical.
        // After the rename the live path only ever holds a complete file.
        var tmp = PathOf(Id) + ".tmp";      // not ".cfg", so List() skips it if one is left
        if (c.Save(tmp) != Error.Ok) return;
        if (DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tmp),
                                     ProjectSettings.GlobalizePath(PathOf(Id))) != Error.Ok)
        {
            // Losing the save outright is worse than the race it was avoiding.
            c.Save(PathOf(Id));
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tmp));
        }
    }

    public static bool Load(string id)
    {
        var c = new ConfigFile();
        if (c.Load(PathOf(id)) != Error.Ok) return false;
        Id      = id;
        Name    = (string)c.GetValue("id", "name", "Commander");
        Main    = (Color)c.GetValue("id", "main", new Color(0.55f, 0.72f, 1.00f));
        Accent  = (Color)c.GetValue("id", "accent", new Color(1.00f, 0.78f, 0.35f));
        int cls = (int)c.GetValue("id", "class", 0);
        Class   = Enum.IsDefined(typeof(ShipClass), cls) ? (ShipClass)cls : ShipClass.Battleship;
        Bonuses.Clear();
        if (c.HasSection("bonus"))
            foreach (var k in c.GetSectionKeys("bonus")) Bonuses[k] = (double)c.GetValue("bonus", k, 0.0);
        Exp = (int)c.GetValue("progress", "exp", 0); Level = Math.Max(1, (int)c.GetValue("progress", "level", 1));
        Points = Math.Max(0, (int)c.GetValue("progress", "points", 0));
        for (int i = 0; i < Bought.Length; i++) Bought[i] = Math.Clamp((int)c.GetValue("progress", "bought_" + Progression.All[i].Id, 0), 0, Progression.MaxPerUpgrade);
        Loadout.Clear(); Spares.Clear();
        foreach (ShipClass sc in Enum.GetValues(typeof(ShipClass)))
        {
            if (c.HasSectionKey("equipment", sc.ToString()))
                Loadout[sc] = Equipment.Sanitize(sc, ((string)c.GetValue("equipment", sc.ToString(), "")).Split(','));
            if (c.HasSectionKey("spares", sc.ToString()))
                // `gid`, not `id`: `id` here is the CHARACTER's id, the parameter this method was
                // called with. Shadowing it with a gear id made the line read as if it were
                // filtering on the character.
                Spares[sc] = ((string)c.GetValue("spares", sc.ToString(), "")).Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Where(gid => Equipment.ById(gid)?.Slot == GearSlot.Chip).ToList();
        }
        BossCleared.Clear();
        if (c.HasSection("boss_cleared"))
            foreach (var k in c.GetSectionKeys("boss_cleared"))
                BossCleared[k] = ((string)c.GetValue("boss_cleared", k, "")).Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n >= 1).ToHashSet();
        else if (c.HasSection("bosses"))   // an old save: tiers from 0 -- tier t beaten means levels 1..t+1
            foreach (var k in c.GetSectionKeys("bosses"))
                BossCleared[k] = Enumerable.Range(1, Math.Max(0, (int)c.GetValue("bosses", k, -1) + 1)).ToHashSet();
        return true;
    }

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
            int cls = (int)c.GetValue("id", "class", 0);
            list.Add(new Slot {
                Id = f[..^4],
                Name = (string)c.GetValue("id", "name", "Commander"),
                Main = (Color)c.GetValue("id", "main", new Color(0.55f, 0.72f, 1.00f)),
                Accent = (Color)c.GetValue("id", "accent", new Color(1.00f, 0.78f, 0.35f)),
                Class = Enum.IsDefined(typeof(ShipClass), cls) ? (ShipClass)cls : ShipClass.Battleship,
            });
        }
        return list;
    }

    // Removes the character's file from disk. There is no undo; the select screen
    // asks first.
    public static bool Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var err = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(PathOf(id)));
        if (id == Id) Id = "";
        return err == Error.Ok;
    }

    // Makes sure SOMETHING is loaded, for a hub entered without the select screen
    // (running Hub.tscn directly from the editor, or the smoke test).
    public static void EnsureLoaded()
    {
        if (!string.IsNullOrEmpty(Id) && FileAccess.FileExists(PathOf(Id))) return;
        var all = List();
        if (all.Count > 0 && Load(all[0].Id)) return;
        NewBlank(); Save();
    }
}

// The nine classes, three to a page. Two are flyable; the rest are declared so
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
                Blurb = "300 hull. Four cursor-aimed main guns, two point-defence turrets, a missile magazine." },
        new() { Id = ShipClass.Carrier, Name = "CARRIER", Ready = true,
                Blurb = "200 hull. Three point-defence turrets, four fighters, two torpedo bombers." },
        new() { Id = ShipClass.Battleship, Name = "MONITOR", Ready = false,
                Blurb = "Reserved." },
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
}
