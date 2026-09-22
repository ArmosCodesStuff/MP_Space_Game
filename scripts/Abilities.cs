using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// ABILITIES — what each class can do, and which key does it.
//
// An ability is DATA: an id, a default key, a kind (Press fires on the key down, Hold is active
// while it is held), and three functions that are the whole of its behaviour --
//   Press  : what it does, on the host.
//   Refuse : why the owner cannot press it right now, checked on the owner's own machine so the
//            slot can say so at once (the host checks again inside Press).
//   Show   : what its slot on the bar says.
// There was a switch on the id in each of those three places, in three files, so an ability was
// written in pieces and a new one meant finding all three. Now an ability is one entry in the
// catalogue below, and a class carries a list of them (Ships.cs).
//
// Its per-ship STATE is a Slot on the ship (PlayerShip.Slot): how long it has left running, how
// long until it is ready, a number it names itself, and a count. Nothing here holds state -- one
// AbilityDef is shared by every ship of that class, on every peer.
//
// The ability bar shows a class's list in order; the K window remaps them. Bindings are per class
// and saved with this MACHINE's settings (Settings), not the character: they are how this keyboard
// is set up, not who you are. Some keys are never bindable (Reserved): the hub flies with them.
// ─────────────────────────────────────────────────────────────────────────────
public enum AbilityKind { Press, Hold }

// What a slot on the bar says, whether it is lit (active/engaged), and how much of it is still
// recharging (0..1). Fail is a refusal, shown in red for a moment.
public struct SlotState { public string Line; public bool Lit, Fail; public float Busy; }

public class AbilityDef
{
    public string Id, Name, Short, Blurb;
    public AbilityKind Kind = AbilityKind.Press;
    public Key Default;
    public bool Open;               // an open hotkey: bound, shown, does nothing yet
    // The owner's own intent, not an order to the host: it rides in the ship's state report
    // instead of being asked for (the fire mode).
    public bool Local;

    public Action<PlayerShip, IHittable> Press;
    public Func<PlayerShip, IHittable, string> Refuse;
    public Func<PlayerShip, IHittable, SlotState> Show;

    public SlotState State(PlayerShip s, IHittable selected) =>
        Show != null ? Show(s, selected) : new SlotState { Line = "READY" };
}

// THE CATALOGUE. Every ability in the game, once. A class's row (Ships.cs) lists the ones it
// carries, so two classes with point defence share this one entry rather than a copy each.
public static class Ab
{
    public static readonly AbilityDef Guns = new()
    {
        Id = "guns", Name = "Main guns", Short = "GUNS", Kind = AbilityKind.Hold, Default = Key.Space,
        Blurb = "Hold to fire. The barrels aim at the cursor and swing slowly.",
        Show = (s, _) => new SlotState { Line = s.Staggered ? "STAGGERED" : "SALVO", Lit = s.Trigger },
    };

    public static readonly AbilityDef FireMode = new()
    {
        Id = "firemode", Name = "Fire mode", Short = "MODE", Default = Key.G, Local = true,
        Blurb = "Salvo (every barrel at once) or staggered (one at a time). Same rate.",
        Press = (s, _) => s.Staggered = !s.Staggered,
        Show = (s, _) => new SlotState { Line = s.Staggered ? "STAGGERED" : "SALVO" },
    };

    public static readonly AbilityDef Broadside = new()
    {
        Id = "broadside", Name = "Broadside", Short = "BROADSIDE", Default = Key.F,
        Blurb = "The turrets swing onto the cursor, then every main gun fires three volleys. The ship steers throughout.",
        Press = (s, _) => s.StartBroadside(),
        Show = (s, _) =>
        {
            if (s.BroadsideWindupLeft > 0) return new SlotState { Line = "AIMING", Lit = true };
            if (s.BroadsideVolleysLeft > 0) return new SlotState { Line = "FIRING", Lit = true };
            if (s.BroadsideCooldownLeft > 0)
                return new SlotState { Line = $"{s.BroadsideCooldownLeft:0}s",
                                       Busy = (float)(s.BroadsideCooldownLeft / s.Stats["broadside_cooldown"]) };
            return new SlotState { Line = "READY" };
        },
    };

    public static readonly AbilityDef Pd = new()
    {
        Id = "pd", Name = "Point defence", Short = "PD", Default = Key.Q,
        Blurb = "Opens a firing window; each turret picks and tracks its own target. Recharges after.",
        Press = (s, _) => s.StartPd(),
        Show = (s, _) =>
        {
            if (s.PdActive) return new SlotState { Line = $"ACTIVE {s.PdLeft:0}s", Lit = true };
            if (!s.PdReady) return new SlotState { Line = $"{s.PdRechargeLeft:0}s", Busy = s.PdRechargeFrac };
            return new SlotState { Line = "READY" };
        },
    };

    public static readonly AbilityDef Missile = new()
    {
        Id = "missile", Name = "Missile burst", Short = "MSL", Default = Key.F,
        Blurb = "Three guided missiles: one at the target, two launched wide that curve in. Needs a selected target in range. Uses the magazine.",
        Press = (s, t) => s.FireMissile(t),
        Refuse = (s, t) => t == null ? "NO TARGET"
                         : s.Position.DistanceTo(t.Position) > s.Stats["missile_range"] ? "OUT OF RANGE" : null,
        Show = (s, _) =>
        {
            var st = new SlotState { Line = s.Reloading ? "RELOADING"
                                          : s.MissilesLoaded == 0 ? "EMPTY · R"
                                          : $"{s.MissilesLoaded}/{s.Stats["missile_mag"]:0}" };
            if (s.Reloading) st.Busy = (float)(s.MissileReloadLeft / s.Stats["missile_reload"]);
            return st;
        },
    };

    public static readonly AbilityDef Reload = new()
    {
        Id = "reload", Name = "Reload missiles", Short = "RELOAD", Default = Key.R,
        Blurb = "Refills the missile magazine. Nothing fires while it runs.",
        Press = (s, _) => s.StartReload(),
        Show = (s, _) => s.Reloading
            ? new SlotState { Line = $"{s.MissileReloadLeft:0.0}s", Busy = (float)(s.MissileReloadLeft / s.Stats["missile_reload"]) }
            : new SlotState { Line = s.MissilesLoaded >= (int)s.Stats["missile_mag"] ? "FULL" : "READY" },
    };

    public static readonly AbilityDef Attack = new()
    {
        Id = "attack", Name = "Fighters: attack", Short = "ATTACK", Default = Key.Space,
        Blurb = "Sends the fighters at the selected target while it is within control range.",
        Press = (s, t) => s.OrderAttack(t),
        Show = (s, sel) =>
        {
            if (s.WingTarget != null) return new SlotState { Line = "ENGAGED", Lit = true };
            if (sel == null) return new SlotState { Line = "NO TARGET" };
            if (s.Position.DistanceTo(sel.Position) > s.Stats["control_range"]) return new SlotState { Line = "OUT OF RANGE" };
            return new SlotState { Line = "READY" };
        },
    };

    public static readonly AbilityDef Recall = new()
    {
        Id = "recall", Name = "Fighters: recall", Short = "RECALL", Default = Key.R,
        Blurb = "Calls the fighters home: they dock inside the carrier.",
        Press = (s, _) => s.RecallWing(),
        Show = (s, _) => new SlotState { Line = s.WingTarget != null ? "READY" : "HOME" },
    };

    public static readonly AbilityDef Bombers = new()
    {
        Id = "bombers", Name = "Bomber strike", Short = "BOMB", Default = Key.F,
        Blurb = "Bombers run at the target and launch torpedoes straight ahead. No tracking.",
        Press = (s, t) => s.OrderStrike(t),
        Show = (s, sel) =>
        {
            int total = s.WingCount(WingKind.Bomber), ready = s.BombersReady;
            if (s.StrikeTarget != null) return new SlotState { Line = "STRIKING", Lit = true };
            if (ready == total && total > 0)
                return new SlotState { Line = sel == null ? "NO TARGET"
                                            : s.Position.DistanceTo(sel.Position) > s.Stats["strike_range"] ? "OUT OF RANGE"
                                            : $"READY {ready}/{total}" };
            if (s.BomberRearmLeft > 0)
                return new SlotState { Line = $"REARM {s.BomberRearmLeft:0}s",
                                       Busy = (float)(s.BomberRearmLeft / s.Stats["bomber_rearm"]) };
            return new SlotState { Line = "RETURNING" };
        },
    };

    // Not on any bar: the escape pod's F. Every class has it, and it is reached by id.
    public static readonly AbilityDef Reboard = new()
    {
        Id = "reboard", Name = "Reboard", Short = "REBOARD", Default = Key.F,
        Blurb = "Climbs back aboard the hull while the pod is still beside it.",
        Press = (s, _) => s.Reboard(),
    };

    // Abilities every class has without listing them.
    public static readonly AbilityDef[] Universal = { Reboard };
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

    // An ability by id: one of this class's, or one every class has (reboard).
    public static AbilityDef Find(ShipClass c, string id)
    {
        foreach (var a in For(c)) if (a.Id == id) return a;
        foreach (var a in Ab.Universal) if (a.Id == id) return a;
        return null;
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
