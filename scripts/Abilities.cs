using Godot;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// ABILITIES — what each class can do, and which key does it.
//
// Every class action is an ability with an id, a default key, and a kind:
//   Press : fires once when the key goes down (missile, recall, activate PD)
//   Hold  : active while the key is held (the battleship's main guns)
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
    public static readonly Dictionary<ShipClass, AbilityDef[]> ByClass = new()
    {
        [ShipClass.Battleship] = new[]
        {
            new AbilityDef { Id = "guns",     Name = "Main guns",     Short = "GUNS",   Kind = AbilityKind.Hold, Default = Key.Space,
                             Blurb = "Hold to fire. The barrels aim at the cursor and swing slowly." },
            new AbilityDef { Id = "firemode", Name = "Fire mode",     Short = "MODE",   Default = Key.V,
                             Blurb = "Salvo (all barrels at once) or staggered (one at a time). Same rate." },
            new AbilityDef { Id = "missile",  Name = "Missile",       Short = "MSL",    Default = Key.F,
                             Blurb = "A slow bunker buster, barely guided: at the target if in range, else the nearest. Uses the magazine." },
            new AbilityDef { Id = "reload",   Name = "Reload missiles", Short = "RELOAD", Default = Key.R,
                             Blurb = "Refills the missile magazine. Nothing fires while it runs." },
            new AbilityDef { Id = "pd",       Name = "Point defence", Short = "PD",     Default = Key.Q,
                             Blurb = "Opens a firing window; each turret picks and tracks its own target. Recharges after." },
        },
        [ShipClass.Carrier] = new[]
        {
            new AbilityDef { Id = "attack",  Name = "Fighters: attack", Short = "ATTACK", Default = Key.Space,
                             Blurb = "Sends the fighters at the selected target while it is within control range." },
            new AbilityDef { Id = "recall",  Name = "Fighters: recall", Short = "RECALL", Default = Key.R,
                             Blurb = "Calls the fighters back to orbit the carrier." },
            new AbilityDef { Id = "bombers", Name = "Bomber strike",    Short = "BOMB",   Default = Key.F,
                             Blurb = "Bombers run at the target and launch torpedoes straight ahead. No tracking." },
            new AbilityDef { Id = "pd",      Name = "Point defence",    Short = "PD",     Default = Key.Q,
                             Blurb = "Opens a firing window; each turret picks and tracks its own target. Recharges after." },
        },
    };

    // Fixed hub controls. Binding one of these would break flying or the menus.
    public static readonly HashSet<Key> Reserved = new()
    {
        Key.W, Key.A, Key.S, Key.D, Key.Tab, Key.Escape, Key.K, Key.B, Key.Enter, Key.KpEnter,
    };

    // Six open hotkeys after every class's own abilities -- every class, including
    // ones not built yet -- ready for abilities and items to come. They bind and
    // remap like any ability; pressing one does nothing until it is assigned.
    public const int OpenSlots = 6;
    private static readonly AbilityDef[] Open = System.Linq.Enumerable.Range(1, OpenSlots).Select(i => new AbilityDef {
        Id = $"open{i}", Name = $"Open slot {i}", Short = "", Open = true, Default = Key.Key0 + i,
        Blurb = "Unassigned — for abilities and items to come." }).ToArray();
    private static readonly Dictionary<ShipClass, AbilityDef[]> _full = new();

    public static AbilityDef[] For(ShipClass c)
    {
        if (_full.TryGetValue(c, out var f)) return f;
        var own = ByClass.TryGetValue(c, out var a) ? a : System.Array.Empty<AbilityDef>();
        return _full[c] = own.Concat(Open).ToArray();
    }

    private static string SettingKey(ShipClass c, string id) => $"{c}.{id}";

    public static Key KeyFor(ShipClass c, string id)
    {
        if (Settings.Keys.TryGetValue(SettingKey(c, id), out var k)) return (Key)k;
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

    // The controls line along the bottom of the hub, per class. A class without an
    // ability list yet (the seven reserved ones) gets a placeholder.
    public static string ControlsHint(ShipClass c)
    {
        const string common = "W ahead  ·  S astern  ·  A/D rudder  ·  left-click select  ·  Tab nearest enemy  ·  K abilities & stats  ·  B base (upgrades, refit)  ·  Esc back";
        if (!ByClass.ContainsKey(c)) return "placeholder";
        return c switch
        {
            ShipClass.Battleship => "BATTLESHIP  ·  mouse aims the main guns  ·  " + common,
            ShipClass.Carrier    => "CARRIER  ·  " + common,
            _ => "placeholder",
        };
    }
}
