using Godot;
using System;
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
        c.Save(PathOf(Id));
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
