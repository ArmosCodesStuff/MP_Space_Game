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
    // Pressable while the hull is a wreck. One row has it (reboard, the escape pod's F); the gate
    // is PlayerShip.DoAbility, so a new ability is refused from stasis without saying anything.
    public bool WhenWrecked;

    public Action<PlayerShip, IHittable> Press;
    public Func<PlayerShip, IHittable, string> Refuse;
    public Func<PlayerShip, IHittable, SlotState> Show;

    // WHEN ITS TIME RUNS OUT. PlayerShip's tick runs EVERY row's Cool and Left down by itself,
    // so a timed ability is a row: it needs no entry in a list of "which ids have timers" and no
    // case in a switch on the id. It replaced both -- and a tenth timed ability added as a row
    // used to compile, bind, draw on the bar and never count down, silently.
    //   Elapsed -- on EVERY peer, the moment Left reaches zero: the phase the bar and the turrets
    //              must show at once, before the host's next report (the broadside leaving its
    //              wind-up for its volleys). Nothing that damages or spends belongs here.
    //   Expire  -- on the HOST alone: what it resolves (the railgun's shot, the rush's EMP, the
    //              echo's blast, the recharge after a point-defence window, the magazine a reload
    //              refills). The host gate is the LOOP's, so a new row is safe by default.
    // Each is handed the ship, and a row that stored a number reads it back from its own slot
    // (PlayerShip.Sl): the echo detonates Sl("echo").Own, so no number has to be carried here.
    public Action<PlayerShip> Elapsed, Expire;

    // WHILE IT RUNS, what it lifts. A row that speeds a ship's guns or its hull up names the stat
    // id that says by how much (x2: twice as fast); PlayerShip.FireRate and PlayerShip.SpeedMult
    // ADD every running row that names one (PlayerShip.LiftShares, the sheet's own rule), which
    // replaced two hardcoded `if`s naming slot ids and stat ids by string. The rate reaches every
    // gun's reload -- main guns, point defence, dropped turrets, the wing's shots -- through
    // PlayerShip.Cadence; the speed lifts its top speed and its thrust. `While` narrows it to part of a run: the dart's roll buffs nothing until the
    // untouchable part of it is over.
    public string RateStat, SpeedStat;
    public Func<PlayerShip, bool> While;
    // WHILE IT RUNS, what it HOLDS the helm to: a share taken after the lifts are summed
    // (PlayerShip.Held), so no speed lift moves a held hull. 0 roots it, heading included; 0.5
    // halves it; 1, the default, holds nothing. `While` narrows it the same way.
    public double Hold = 1;

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
        Blurb = "Hold to fire. The barrels follow the cursor, slowly.",
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
        Blurb = "The turrets swing onto the cursor, then every main gun fires, volley after volley. You keep steering.",
        Press = (s, _) => s.StartBroadside(),
        // the wind-up spent: every peer moves on to the volleys (the bar, the turrets' fast
        // swing) until the host's next report says how many are left; only the host fires them
        Elapsed = s => s.Sl("broadside").N = Math.Max(1, (int)s.Stats["broadside_volleys"]),
        Expire = s => s.Sl("broadside").Own = 0,
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
        Blurb = "Opens a firing window: each turret picks its own target. Recharges after.",
        Press = (s, _) => s.StartPd(),
        Expire = s => s.Sl("pd").Cool = s.Stats["pd_reload"],      // the window closed: the recharge
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
        Blurb = "Three guided missiles: one at the target, two wide that curve in. Needs a target in range; uses the magazine.",
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
        Blurb = "Refills the magazine. Nothing fires while it runs.",
        Press = (s, _) => s.StartReload(),
        Expire = s => s.Sl("missile").N = (int)s.Stats["missile_mag"],   // loaded: the magazine full
        Show = (s, _) => s.Reloading
            ? new SlotState { Line = $"{s.MissileReloadLeft:0.0}s", Busy = (float)(s.MissileReloadLeft / s.Stats["missile_reload"]) }
            : new SlotState { Line = s.MissilesLoaded >= (int)s.Stats["missile_mag"] ? "FULL" : "READY" },
    };

    public static readonly AbilityDef Attack = new()
    {
        Id = "attack", Name = "Fighters: attack", Short = "ATTACK", Default = Key.Space,
        Blurb = "Sends the fighters at the selected target, inside control range.",
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
        Blurb = "Calls the fighters home; they dock inside.",
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


    // ── the freighters ───────────────────────────────────────────────────────
    public static readonly AbilityDef Deploy = new()
    {
        Id = "deploy", Name = "Deploy turret", Short = "DEPLOY", Default = Key.T,
        Blurb = "Drops a turret where you are. It holds the spot and shoots what comes near until you collect it (C) or it dies.",
        Press = (s, _) => s.DeployTurret(),
        Refuse = (s, _) => s.TurretsOut >= (int)s.Stats["deploy_max"] ? "ALL OUT"
                         : s.Sl("deploy").Cool > 0 ? "RELOADING" : null,
        Show = (s, _) =>
        {
            int max = (int)s.Stats["deploy_max"];
            if (s.Sl("deploy").Cool > 0)
                return new SlotState { Line = $"{s.Sl("deploy").Cool:0.0}s", Busy = (float)(s.Sl("deploy").Cool / s.Stats["deploy_cooldown"]) };
            return new SlotState { Line = $"{max - s.TurretsOut}/{max}", Lit = s.TurretsOut > 0 };
        },
    };

    public static readonly AbilityDef Collect = new()
    {
        Id = "collect", Name = "Collect turret", Short = "COLLECT", Default = Key.C,
        Blurb = "Picks up one of your turrets you are sitting over, to drop again.",
        Press = (s, _) => s.CollectTurret(),
        Refuse = (s, _) => s.TurretsOut == 0 ? "NONE OUT" : s.NearestOwnTurret() == null ? "NOT OVER ONE" : null,
        Show = (s, _) => new SlotState { Line = s.TurretsOut == 0 ? "NONE OUT" : s.NearestOwnTurret() != null ? "PICK UP" : "FLY OVER ONE",
                                         Lit = s.NearestOwnTurret() != null },
    };

    public static readonly AbilityDef Bubble = new()
    {
        Id = "bubble", Name = "Bubble", Short = "BUBBLE", Default = Key.F,
        Blurb = "A bubble over you and everyone near: it soaks damage until its pool is spent or the time is up.",
        Press = (s, _) => s.RaiseBubble(),
        Refuse = (s, _) => s.Sl("bubble").Cool > 0 ? "CHARGING" : null,
        Show = (s, _) => Timed(s, "bubble", "bubble_cooldown", $"UP {s.Sl("bubble").N}"),
    };

    public static readonly AbilityDef Overdrive = new()
    {
        Id = "overdrive", Name = "Overdrive", Short = "OVERDRIVE", Default = Key.F,
        Blurb = "Everything you own fires faster: your gun, your point defence, every turret out.",
        Press = (s, _) => s.StartOverdrive(),
        RateStat = "overdrive_mult",
        Refuse = (s, _) => s.Sl("overdrive").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "overdrive", "overdrive_cooldown", $"x{s.Stats["overdrive_mult"]:0.#}"),
    };

    public static readonly AbilityDef Shockwave = new()
    {
        Id = "shockwave", Name = "Shockwave", Short = "WAVE", Default = Key.F,
        Blurb = "Throws everything near you clear. What is too big to throw (a boss) is held still instead.",
        Press = (s, _) => s.Shockwave(),
        Refuse = (s, _) => s.Sl("shockwave").Cool > 0 ? "CHARGING" : null,
        Show = (s, _) => Timed(s, "shockwave", "wave_cooldown", "READY"),
    };

    // ── the heavy fighters ───────────────────────────────────────────────────
    public static readonly AbilityDef Railgun = new()
    {
        Id = "railgun", Name = "Railgun", Short = "RAIL", Default = Key.F,
        Blurb = "A charge you cannot turn or thrust through, then a straight blue line through everything on it.",
        Press = (s, _) => s.ChargeRail(),
        Hold = 0,                                           // the charge: rooted, heading and all
        Expire = s => s.FireRail(),
        Refuse = (s, _) => s.Sl("railgun").Left > 0 ? "CHARGING" : s.Sl("railgun").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Sl("railgun").Left > 0
            ? new SlotState { Line = $"CHARGE {s.Sl("railgun").Left:0.0}s", Lit = true,
                              Busy = (float)(s.Sl("railgun").Left / s.Stats["rail_charge"]) }
            : Timed(s, "railgun", "rail_cooldown", "READY"),
    };

    public static readonly AbilityDef Rush = new()
    {
        Id = "rush", Name = "Rush", Short = "RUSH", Default = Key.F,
        Blurb = "A burst of speed at a fraction of the damage taken. It ends in an EMP that stuns everything close.",
        Press = (s, _) => s.StartRush(),
        Expire = s => s.RushEmp(),
        SpeedStat = "rush_mult",
        Refuse = (s, _) => s.Sl("rush").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "rush", "rush_cooldown", "RUSHING"),
    };

    public static readonly AbilityDef Hunters = new()
    {
        Id = "hunters", Name = "Hunter-seekers", Short = "HUNTERS", Default = Key.F,
        Blurb = "A cell of missiles, each taking a target of its own; all of them at the nearest if there is only one.",
        Press = (s, _) => s.LaunchHunters(),
        Refuse = (s, _) => s.Sl("hunters").Cool > 0 ? "RELOADING"
                         : s.SeekerPrey().Count == 0 ? "NOTHING IN REACH" : null,
        Show = (s, _) => Timed(s, "hunters", "hunter_cooldown", "AWAY"),
    };

    // ── the lights ───────────────────────────────────────────────────────────
    public static readonly AbilityDef Roll = new()
    {
        Id = "roll", Name = "Barrel roll", Short = "ROLL", Default = Key.F,
        Blurb = "Nothing can hit you while you roll; you come out faster and firing quicker.",
        Press = (s, _) => s.BarrelRoll(),
        RateStat = "boost_rof", SpeedStat = "boost_speed",
        While = s => !s.Statuses.Has(Status.Evading),     // the boost is the part after the roll
        Refuse = (s, _) => s.Sl("roll").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Statuses.Has(Status.Evading)
            ? new SlotState { Line = "ROLLING", Lit = true }
            : Timed(s, "roll", "roll_cooldown", "BOOST"),
    };

    public static readonly AbilityDef Echo = new()
    {
        Id = "echo", Name = "Bullet echo", Short = "ECHO", Default = Key.F,
        Blurb = "The echo remembers the damage you deal, then detonates all of it where your last shot landed.",
        Press = (s, _) => s.StartEcho(),
        Expire = s => s.Detonate(),
        Refuse = (s, _) => s.Sl("echo").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Sl("echo").Left > 0
            ? new SlotState { Line = $"{s.Sl("echo").Own:0} STORED", Lit = true }
            : Timed(s, "echo", "echo_cooldown", "READY"),
    };

    public static readonly AbilityDef Stealth = new()
    {
        Id = "stealth", Name = "Stealth", Short = "STEALTH", Default = Key.F,
        Blurb = "While the veil is up nothing hostile can pick you: whatever was coming for you goes elsewhere, or gives up.",
        // what the veil itself is worth, x1.00 each until a part moves one (Ships.cs, the wraith)
        RateStat = "stealth_rof", SpeedStat = "stealth_speed",
        Press = (s, _) => s.GoDark(),
        Refuse = (s, _) => s.Sl("stealth").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Statuses.Has(Status.Untargetable)
            ? new SlotState { Line = $"UNSEEN {s.Statuses.Left(Status.Untargetable):0.0}s", Lit = true }
            : Timed(s, "stealth", "stealth_cooldown", "READY"),
    };

    // The shape nearly every timed ability shows: running (lit, with its own word), cooling
    // (a countdown and the sweep), or ready.
    private static SlotState Timed(PlayerShip s, string id, string coolStat, string up)
    {
        ref var sl = ref s.Sl(id);
        if (sl.Left > 0) return new SlotState { Line = $"{up} {sl.Left:0.0}s", Lit = true };
        if (sl.Cool > 0) return new SlotState { Line = $"{sl.Cool:0}s", Busy = (float)(sl.Cool / s.Stats[coolStat]) };
        return new SlotState { Line = "READY" };
    }

    // Not on any bar: the escape pod's F. Every class has it, and it is reached by id.
    public static readonly AbilityDef Reboard = new()
    {
        Id = "reboard", Name = "Reboard", Short = "REBOARD", Default = Key.F, WhenWrecked = true,
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
        if (!Classes.Known(c)) return _hints[c] = "placeholder";      // not a class this build has
        string own = Classes.Of(c).Hint;
        return _hints[c] = own.Length > 0 ? own + "  ·  " + CommonHint : CommonHint;
    }
}
