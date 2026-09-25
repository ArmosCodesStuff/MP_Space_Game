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
// recharging (0..1). Fail is a refusal, shown in red for a moment; Locked is a level wall the
// pilot has not reached (Unlocks), and the line says the level that opens it.
public struct SlotState { public string Line; public bool Lit, Fail, Locked; public float Busy; }

public class AbilityDef
{
    public string Id, Name, Short, Blurb;
    public AbilityKind Kind = AbilityKind.Press;
    public Key Default;
    public bool Open;               // an open hotkey: bound, shown, does nothing yet
    // A WEAPON'S OWN ACTION (the guns, their fire mode, R, point defence, the wing's orders, the
    // sentries): never behind a level wall. Every other row a class lists is one of its abilities
    // 1, 2 and 3, in list order, opened by the pilot's level (Unlocks.AbilityAt).
    public bool Weapon;
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
    //   Expire  -- on the HOST alone: what it resolves (the railgun's shot, the lunge's end, the
    //              echo's blast, the magazine a reload refills). The host gate is the LOOP's, so a new row is safe by default.
    // Each is handed the ship, and a row that stored a number reads it back from its own slot
    // (PlayerShip.Sl): the echo detonates Sl("echo").Own, so no number has to be carried here.
    public Action<PlayerShip> Elapsed, Expire;

    // WHAT THIS ROW HEARS OF ITS OWN SHIP'S BLOWS, WHILE IT RUNS (F18): every weapon's hit comes
    // through PlayerShip.NoteDealt (the hostile damage door, Dealt.Deal), which calls this on every
    // row whose Left > 0 (and While, if it narrows the run) -- the target, how much, and the
    // weapon's id (Dealt.*, or a shot row's own). The echo is the one row that uses it today: it
    // stores the damage and where it landed in its own slot (PlayerShip.Sl("echo").Own / .At)
    // rather than NoteDealt knowing the echo by name.
    public Action<PlayerShip, IHittable, double, string> OnDealt;

    // WHILE IT RUNS, what it lifts. A row that speeds a ship's guns or its hull up names the stat
    // id that says by how much (x2: twice as fast); PlayerShip.FireRate and PlayerShip.SpeedMult
    // ADD every running row that names one (PlayerShip.LiftShares, the sheet's own rule), which
    // replaced two hardcoded `if`s naming slot ids and stat ids by string. The rate reaches every
    // gun's reload -- main guns, point defence, dropped turrets, the wing's shots -- through
    // PlayerShip.Cadence; the speed lifts its top speed and its thrust. `While` narrows it to part of a run: the dart's roll buffs nothing until the
    // untouchable part of it is over.
    //   The SLIDE (strafe speed and strafe thrust, PlayerShip.StrafeMult) takes StrafeStat when a row
    // names one, and its SpeedStat otherwise: the boost's slide is a row of its own (surge_strafe), so
    // gear can lift the slide without the top speed (Convoy Rig) or the top speed without the slide.
    public string RateStat, SpeedStat, StrafeStat;
    public Func<PlayerShip, bool> While;
    // WHILE IT RUNS, what it HOLDS the helm to: a share taken after the lifts are summed
    // (PlayerShip.Held), so no speed lift moves a held hull. 0 roots it, heading included; 0.5
    // halves it; 1, the default, holds nothing. `While` narrows it the same way.
    public double Hold = 1;

    // A MELEE ROW IT SWINGS (Melee.cs): a Hold row swings it while the trigger holds, a Press row while
    // its Left runs, once every that row's Every seconds at the ship's Cadence (PlayerShip.Swings).
    public MeleeDef Swing;
    // WHILE IT RUNS, THE TRIGGER'S WEAPON HOLDS (the whirlwind's spin, the prism stance): no swing and
    // no shot off the trigger. Read by PlayerShip.Stilled, never by the row's id.
    public bool Stills;

    // A FLAT TOP SPEED (F1's Add), on top of SpeedStat's multiplier, before the hold: the stat id
    // this row's ship sheet names for it (PlayerShip.SpeedAdds sums every running row's, added in
    // PlayerShip.TopSpeed -- see D18). No row uses it yet.
    public string SpeedAdd;
    // A LIFT WHOSE SIZE IS A RUNNING TOTAL (F1's Ramp, D18): builds while it runs and Condition
    // holds, bleeds with the turn (whether or not Condition holds), caps, and drains once the row
    // stops. See RampSpec below -- the row names three stat ids and a condition; the math is not
    // its own. No row uses it yet (6d, the Dart's Ramjet).
    public RampSpec Ramp;
    // A DASH (DashSpec below): pressed, it carries the hull a fixed distance along its nose, and the
    // host strikes what lies on that line. The Warrior's lunge is the first row.
    public DashSpec Dash;
    // A STANCE (StanceSpec below): pressed it holds a Status for its Time, pressed again it drops, and any
    // other of the class's abilities pressed ends it; its cooldown runs from the END. The prism stance.
    public StanceSpec Stance;

    public SlotState State(PlayerShip s, IHittable selected) =>
        Show != null ? Show(s, selected) : new SlotState { Line = "READY" };
}

// F1'S RAMP: a lift that is a RUNNING TOTAL instead of a fixed multiplier -- the Dart's Ramjet
// rewards flying straight and bleeds it into a turn. A row is nothing but three stat ids (how fast
// it builds, its ceiling, how hard turning costs it) and a condition; the number itself lives in
// the ship's own slot (PlayerShip.Sl(id).Own -- per-ability state is how it reaches the wire, same
// as the echo's stored damage). Step is the ONE place the arithmetic lives: pure, no ship and no
// Godot frame, so it is provable (LaneARampChecks) before any row exists to carry it.
public class RampSpec
{
    public string Build, Cap, Bleed;          // stat ids: gain/second, ceiling, bleed/second at a full turn
    public Func<PlayerShip, bool> Condition;  // gates Build only; null = always holds

    // ADVANCES the running total by dt seconds. Holding (the row still running): +build a second
    // while Condition holds (nothing, if it does not), minus bleed x yawShare a second regardless
    // (yawShare is |yaw| / the hull's turn rate, 0..1) -- clamped to [0, cap]. Not holding (the row
    // has stopped): drains toward 0 at cap a second, so a value at the ceiling empties in exactly
    // 1.0 s and anything less empties sooner.
    public static double Step(double own, double build, double cap, double bleed,
                               bool holding, bool condHolds, double yawShare, double dt)
        => holding
            ? Math.Clamp(own + ((condHolds ? build : 0) - bleed * yawShare) * dt, 0, cap)
            : Math.Max(0, own - cap * dt);
}

// A DASH: a FIXED distance along the nose in a fixed time, never priced from speed (kits_v31 §3.4) --
// no lift, hold or throttle changes it, and the hull's own velocity is left as it was. The OWNER
// carries its hull (PlayerShip.DashCarry: from the first frame it sees the row's Left, for the row's
// whole Time, so a guest that hears of its press a packet late still goes the whole way); the HOST
// strikes every hostile body on the line from where it was pressed, once each (PlayerShip.DashSweep),
// and hardens the hull for the Time at the Guard share. A row names stat ids, never numbers:
public class DashSpec
{
    public string Reach, Time, Damage, Guard, Cooldown;   // u, s, per body, x damage taken, s
}

// A STANCE: a Status held for Time (PlayerShip.Stance), dropped by a second press or by any other of the
// class's three abilities (PlayerShip.DoAbility), its Cooldown from the moment it ends (PlayerShip.EndStance,
// on every peer through the row's Elapsed). Split names the split tick of a prism stance: at most Splits
// catches split in one stance, Every seconds apart (PlayerShip.Split, read by Prism.Catch). Stat ids:
public class StanceSpec
{
    public string Time, Cooldown, Every, Splits;
    public Status Holds;
}

// THE CATALOGUE. Every ability in the game, once. A class's row (Ships.cs) lists the ones it
// carries, so two classes with main guns share this one entry rather than a copy each. (Point
// defence is no entry at all: it is passive, and fires whenever its ship is alive.)
public static class Ab
{
    public static readonly AbilityDef Guns = new()
    {
        Weapon = true, Id = "guns", Name = "Main guns", Short = "GUNS", Kind = AbilityKind.Hold, Default = Key.Space,
        Blurb = "Hold to fire. The barrels follow the cursor, slowly.",
        Show = (s, _) => new SlotState { Line = s.Staggered ? "STAGGERED" : "SALVO", Lit = s.Trigger },
    };

    public static readonly AbilityDef FireMode = new()
    {
        Weapon = true, Id = "firemode", Name = "Fire mode", Short = "MODE", Default = Key.G, Local = true,
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
        Weapon = true, Id = "reload", Name = "Reload missiles", Short = "RELOAD", Default = Key.R,
        Blurb = "Refills the magazine. Nothing fires while it runs.",
        Press = (s, _) => s.StartReload(),
        Expire = s => s.Sl("missile").N = (int)s.Stats["missile_mag"],   // loaded: the magazine full
        Show = (s, _) => s.Reloading
            ? new SlotState { Line = $"{s.MissileReloadLeft:0.0}s", Busy = (float)(s.MissileReloadLeft / s.Stats["missile_reload"]) }
            : new SlotState { Line = s.MissilesLoaded >= (int)s.Stats["missile_mag"] ? "FULL" : "READY" },
    };

    public static readonly AbilityDef Attack = new()
    {
        Weapon = true, Id = "attack", Name = "Fighters: attack", Short = "ATTACK", Default = Key.Space,
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
        Weapon = true, Id = "recall", Name = "Fighters: recall", Short = "RECALL", Default = Key.R,
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
        Weapon = true, Id = "deploy", Name = "Sentry", Short = "SENTRY", Default = Key.R,
        Blurb = "Throws a sentry to the cursor, up to 600 u; it lands 0.8 s later and shoots what comes near. R with the cursor on one of yours recalls it.",
        Press = (s, _) => s.DeployTurret(s.AimPoint),
        // a recall is never refused
        Refuse = (s, _) => s.RecallAt(s.AimPoint) != null ? null
                         : s.TurretsOut >= (int)s.Stats["deploy_max"] ? "ALL OUT"
                         : s.Sl("deploy").Cool > 0 ? "RELOADING" : null,
        Show = (s, _) =>
        {
            int max = (int)s.Stats["deploy_max"];
            if (s.RecallAt(s.AimPoint) != null) return new SlotState { Line = "RECALL", Lit = true };
            if (s.Sl("deploy").Cool > 0)
                return new SlotState { Line = $"{s.Sl("deploy").Cool:0.0}s", Busy = (float)(s.Sl("deploy").Cool / s.Stats["deploy_cooldown"]) };
            return new SlotState { Line = $"{max - s.TurretsOut}/{max}", Lit = s.TurretsOut > 0 };
        },
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
    // THE BLADE (the Warrior's primary): held, it swings Melee.Blade every blade_interval at
    // everything in its arc (PlayerShip.Swings, on the host).
    public static readonly AbilityDef Blade = new()
    {
        Weapon = true, Id = "blade", Name = "Blade", Short = "BLADE", Kind = AbilityKind.Hold, Default = Key.Space,
        Blurb = "Hold to swing. Everything within 160 u and 55° of the nose is cut, every swing.",
        Swing = Melee.Blade,
        Show = (s, _) => new SlotState { Line = s.Stilled ? "HELD" : "SWING", Lit = s.Trigger },
    };

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

    // THE LUNGE (the Warrior's E): 420 u along the nose in 0.3 s, 40 to each body on the way, half
    // damage taken while it runs. It ends a prism stance.
    public static readonly AbilityDef Lunge = new()
    {
        Id = "lunge", Name = "Lunge", Short = "LUNGE", Default = Key.E,
        Blurb = "A dash of 420 u straight ahead in 0.3 s: 40 to everything on the way, half damage taken while it runs.",
        Dash = new DashSpec { Reach = "lunge_reach", Time = "lunge_time", Damage = "lunge_damage", Guard = "lunge_guard", Cooldown = "lunge_cooldown" },
        Press = (s, _) => s.StartDash("lunge"),
        Expire = s => s.DashSweep("lunge", 1),
        Refuse = (s, _) => s.Sl("lunge").Left > 0 ? "LUNGING" : s.Sl("lunge").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "lunge", "lunge_cooldown", "LUNGE"),
    };

    // THE WHIRLWIND (the Warrior's Q): 2 s spinning, 10 every 0.25 s to everything within 210 u. The
    // press drops every web on the hull and none can take it while it spins; the blade holds meanwhile.
    public static readonly AbilityDef Whirlwind = new()
    {
        Id = "whirlwind", Name = "Whirlwind", Short = "SPIN", Default = Key.Q,
        Blurb = "Spin for 2 s: 40 DPS to everything within 210 u. It throws off every web, and none can take you while you spin.",
        Swing = Melee.Whirl, Stills = true,
        Press = (s, _) => s.Whirl(),
        Refuse = (s, _) => s.Sl("whirlwind").Left > 0 ? "SPINNING" : s.Sl("whirlwind").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "whirlwind", "whirl_cooldown", "SPIN"),
    };

    // THE PRISM STANCE (the Warrior's F): 2 s catching light off the guard (Prism.cs), at half speed and
    // with no blade; F again drops it, Q or E ends it; the cooldown runs 12 s from its end.
    public static readonly AbilityDef PrismStance = new()
    {
        Id = "prism", Name = "Prism stance", Short = "PRISM", Default = Key.F,
        Blurb = "2 s with the guard on the cursor: light that strikes it square goes back along it, light that strikes it slant is split. Half speed, no blade. F again drops it.",
        Stance = new StanceSpec { Time = "prism_time", Cooldown = "prism_cooldown", Every = "prism_split", Splits = "prism_splits", Holds = Status.Parrying },
        Hold = 0.5, Stills = true,
        Press = (s, _) => s.Stance("prism"),
        Elapsed = s => s.EndStance("prism"),
        Refuse = (s, _) => s.Sl("prism").Left <= 0 && s.Sl("prism").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Sl("prism").Left > 0
            ? new SlotState { Line = $"GUARD {s.Sl("prism").Left:0.0}s", Lit = true }
            : Timed(s, "prism", "prism_cooldown", "READY"),
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
        OnDealt = (s, t, d, w) => { ref var e = ref s.Sl("echo"); e.Own += d; e.At = t.Position; },
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
        Key.W, Key.A, Key.S, Key.D, Key.Shift, Key.Tab, Key.Escape, Key.K, Key.B, Key.L, Key.I, Key.V, Key.Enter, Key.KpEnter,
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
        return _full[c] = Classes.Of(c).Abilities.Concat(Drives.RowOf(c)).Concat(Open).ToArray();   // then V's drive, then the open keys
    }

    // THE TRIGGER: the class's weapon row that is held (the guns, the blade). Space on every class that
    // has one; a class without one (the carrier's wing orders are presses) has no trigger at all.
    public static AbilityDef TriggerOf(ShipClass c) => For(c).FirstOrDefault(a => a.Weapon && a.Kind == AbilityKind.Hold);

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
        // a saved binding on a fixed key is ignored: the default stands
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
        if (Reserved.Contains(KeyFor(c, id))) return $"{KeyName(KeyFor(c, id))} is a fixed control: it stays where it is.";   // the drive, on V
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
    private const string CommonHint = "W ahead  ·  S astern  ·  A/D rudder  ·  left-click select  ·  Tab nearest enemy  ·  wheel zoom  ·  Y free camera  ·  K abilities & stats  ·  B base  ·  L pilot  ·  I equipment";
    private static readonly Dictionary<ShipClass, string> _hints = new();

    public static string ControlsHint(ShipClass c)
    {
        if (_hints.TryGetValue(c, out var h)) return h;
        if (!Classes.Known(c)) return _hints[c] = "placeholder";      // not a class this build has
        string own = Classes.Of(c).Hint;
        string drive = Drives.Of(c) is { } d ? "  ·  " + d.Controls : "";            // V, and Shift on a hull that slides
        return _hints[c] = (own.Length > 0 ? own + "  ·  " : "") + CommonHint + drive + "  ·  Esc menu";
    }
}
