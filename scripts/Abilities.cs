using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// ABILITIES — what each class can do, and which key does it.
//
// Every class action is an ability with an id, a default key, and a kind:
//   Press : fires once when the key goes down (broadside, missile, recall, activate PD)
//   Hold  : active while the key is held (the main guns)
// The ability bar shows them in order; the K window remaps them. Bindings are
// per class and saved with this MACHINE's settings (Settings), not the character:
// they are how this keyboard is set up, not who you are.
//
// Some keys are never bindable because the hub uses them for fixed controls.
// ─────────────────────────────────────────────────────────────────────────────
public enum AbilityKind { Press, Hold }

public class AbilityDef
{
    public string Id, Name, Short, Blurb;
    public AbilityKind Kind = AbilityKind.Press;
    public Key Default;
    public bool Open;               // an open hotkey: bound, shown, does nothing yet
}

public static class Abilities
{
    // Fixed hub controls. Binding one of these would break flying or the menus.
    public static readonly HashSet<Key> Reserved = new()
    {
        Key.W, Key.A, Key.S, Key.D, Key.Tab, Key.Escape, Key.K, Key.B, Key.L, Key.I, Key.V, Key.Enter, Key.KpEnter,
        Key.Y, Key.Up, Key.Down, Key.Left, Key.Right,        // the free camera
    };

    // Six open hotkeys after every class's own abilities -- every class, including
    // ones not built yet -- ready for abilities and items to come. They bind and
    // remap like any ability; pressing one does nothing until it is assigned.
    // NOTE for whoever fills these in: every class's array holds the SAME six AbilityDef
    // instances, because `For` concatenates this one array onto each class's own. That is fine
    // while an AbilityDef is read-only data. The moment an open slot carries something per class
    // -- an assigned item from the inventory, say -- writing to it would write it for every class
    // at once. Give each class its own copies then.
    private const int OpenSlots = 6;
    private static readonly AbilityDef[] Open = Enumerable.Range(1, OpenSlots).Select(i => new AbilityDef {
        Id = $"open{i}", Name = $"Open slot {i}", Short = "", Open = true, Default = Key.Key0 + i,
        Blurb = "Unassigned — for abilities and items to come." }).ToArray();
    private static readonly Dictionary<ShipClass, AbilityDef[]> _full = new();

    public static AbilityDef[] For(ShipClass c)
    {
        if (_full.TryGetValue(c, out var f)) return f;
        return _full[c] = Classes.Of(c).Abilities.Concat(Open).ToArray();
    }

    private static string SettingKey(ShipClass c, string id) => $"{c}.{id}";

    public static Key KeyFor(ShipClass c, string id)
    {
        // a saved binding on a fixed key (V is warp now) is ignored: the default stands
        if (Settings.Keys.TryGetValue(SettingKey(c, id), out var k) && !Reserved.Contains((Key)k)) return (Key)k;
        foreach (var a in For(c)) if (a.Id == id) return a.Default;
        return Key.None;
    }

    public static AbilityDef ByKey(ShipClass c, Key k)
    {
        foreach (var a in For(c)) if (KeyFor(c, a.Id) == k) return a;
        return null;
    }

    // Binds a key. If another ability of the same class had it, the two SWAP, so a
    // rebind can never leave two abilities on one key or one ability on none.
    // Returns an error for a reserved key, or null on success.
    public static string Bind(ShipClass c, string id, Key k)
    {
        if (Reserved.Contains(k)) return $"{OS.GetKeycodeString(k)} is reserved for a fixed control.";
        var old = KeyFor(c, id);
        var clash = ByKey(c, k);
        if (clash != null && clash.Id != id) Settings.Keys[SettingKey(c, clash.Id)] = (int)old;
        Settings.Keys[SettingKey(c, id)] = (int)k;
        Settings.Save();
        return null;
    }

    public static void ResetDefaults(ShipClass c)
    {
        foreach (var a in For(c)) Settings.Keys.Remove(SettingKey(c, a.Id));
        Settings.Save();
    }

    public static string KeyName(Key k) => k == Key.None ? "—" : OS.GetKeycodeString(k);

    // The controls line along the bottom of the hub: the class's own part (ClassDef.Hint) and
    // the fixed controls every class shares. Built ONCE per class -- the Hub asks for this every
    // frame to see whether the line changed, and concatenating it allocated a string each time
    // for text that only moves on a refit.
    private const string CommonHint = "W ahead  ·  S astern  ·  A/D rudder  ·  left-click select  ·  Tab nearest enemy  ·  wheel zoom  ·  Y free camera  ·  K abilities & stats  ·  B base  ·  L pilot  ·  I equipment  ·  V warp  ·  Esc menu";
    private static readonly Dictionary<ShipClass, string> _hints = new();

    public static string ControlsHint(ShipClass c)
    {
        if (_hints.TryGetValue(c, out var h)) return h;
        string own = Classes.Of(c).Hint;
        return _hints[c] = own.Length > 0 ? own + "  ·  " + CommonHint : CommonHint;
    }
}
