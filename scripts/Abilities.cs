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
    // THE PRESS CARRIES A POINT (F8's payload): the owner's cursor in the world rides WITH the press
    // (PlayerShip.UseAbility -> RequestAbility), and the host writes it into this row's own slot
    // (Sl(id).At) before Press runs. So a row reads where it was aimed from its slot -- never from a
    // guest's AimPoint, which is a report behind the click. A point that is not a number presses
    // nothing. A reach is the row's own: Abilities.Toward clamps a point onto it, on its bearing.
    public bool TakesPoint;
    // THE PRESS CARRIES THE PILOT'S PICKS (F8's payload: the gunships' 1-3 targets): the NetIds of what
    // the owner has picked (Hub.Targets) ride WITH the press, and the host -- holding them to the class's
    // own ClassDef.Targets, dropping repeats and anything not alive -- sets PlayerShip.Picked before
    // Press runs. A row reads its targets there, never from a guest's selection, which is never sent.
    public bool TakesTargets;

    public Action<PlayerShip, IHittable> Press;
    // WHAT THE OWNER DOES AT ONCE, on its own machine, when its press passes the courtesy checks (PlayerShip.UseAbility),
    // handed the point it pressed at: flight is the owner's, so a snap of the heading or the hull cannot wait a round
    // trip for the host. The host's Press still resolves the rest (the cooldown). The Slingshot.
    public Action<PlayerShip, Vector2> AtOnce;
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
    //              reverb's blast, the magazine a reload refills). The host gate is the LOOP's, so a new row is safe by default.
    // Each is handed the ship, and a row that stored a number reads it back from its own slot
    // (PlayerShip.Sl): the reverb detonates Sl("reverb").Own, so no number has to be carried here.
    public Action<PlayerShip> Elapsed, Expire;

    // WHAT THIS ROW HEARS OF ITS OWN SHIP'S BLOWS, WHILE IT RUNS (F18): every weapon's hit comes
    // through PlayerShip.NoteDealt (the hostile damage door, Dealt.Deal), which calls this on every
    // row whose Left > 0 (and While, if it narrows the run) -- the target, how much, and the
    // weapon's id (Dealt.*, or a shot row's own). The reverb is the one row that uses it today: it
    // stores the damage and where it landed in its own slot (PlayerShip.Sl("reverb").Own / .At)
    // rather than NoteDealt knowing the reverb by name.
    public Action<PlayerShip, IHittable, double, string> OnDealt;
    // WHAT THIS ROW DOES WHEN ITS SHIP FIRES ITS MAIN GUNS, WHILE IT RUNS (host, PlayerShip.FireControl, before the volley
    // leaves): the Veil ends. Null: every other row.
    public Action<PlayerShip> OnFire;

    // WHILE IT RUNS, what it lifts. A row that speeds a ship's guns or its hull up names the stat
    // id that says by how much (x2: twice as fast); PlayerShip.FireRate and PlayerShip.SpeedMult
    // ADD every running row that names one (PlayerShip.LiftShares, the sheet's own rule), which
    // replaced two hardcoded `if`s naming slot ids and stat ids by string. The rate reaches every
    // gun's reload -- main guns, point defence, dropped turrets, the wing's shots -- through
    // PlayerShip.Cadence; the speed lifts its top speed and its thrust. `While` narrows it to part of a run (a
    // row whose lift waits on a status or a phase of its own run).
    //   The SLIDE (strafe speed and strafe thrust, PlayerShip.StrafeMult) takes StrafeStat when a row
    // names one, and its SpeedStat otherwise: the boost's slide is a row of its own (surge_strafe), so
    // gear can lift the slide without the top speed (Convoy Rig) or the top speed without the slide.
    public string RateStat, SpeedStat, StrafeStat;
    // A SCOPED LIFT: RateOn names the ONE interval stat its RateStat reaches (the CIWS's x8 on pd_interval alone);
    // null reaches every reload, as above. DamageStat lifts the damage stat DamageOn names, the same share rule
    // (PlayerShip.DamageOf). `While` narrows both.
    public string RateOn, DamageStat, DamageOn;
    // WHILE IT RUNS, what it lifts the PUSH AHEAD by and nothing else (the Dart's sprint, x3): a thrust-only lift,
    // added to SpeedStat's shares (PlayerShip.ThrustMult), so the boost's +50% on a sprint is x3.5. The top speed is
    // SpeedStat's and SpeedAdd's, never this.
    public string ThrustStat;
    // WHILE IT RUNS, THE THROTTLE IS FORCED OPEN (PlayerShip.Forced, read by LocalFlight as a web's pin is): S does
    // nothing, the rudder and the slide stay the pilot's. The Dart's sprint.
    public bool Forces;
    // WHILE IT RUNS, what it lifts the REACH of the ship's weapons by (the anchor's x1.4), added like every
    // other lift (PlayerShip.ReachMult). A gun that reads it multiplies its own range row: the railgun.
    public string ReachStat;
    // NARROWS A RUNNING ROW'S LIFTS (PlayerShip.Lifts) to the frames it says yes: the CIWS lifts nothing while Disabled.
    public Func<PlayerShip, bool> While;
    // WHILE IT RUNS, what it HOLDS the helm to: a share taken after the lifts are summed
    // (PlayerShip.Held), so no speed lift moves a held hull. 0 roots it, heading included; 0.5
    // halves it; 1, the default, holds nothing.
    public double Hold = 1;

    // A MELEE ROW IT SWINGS (Melee.cs): a Hold row swings it while the trigger holds, a Press row while
    // its Left runs, once every that row's Every seconds at the ship's Cadence (PlayerShip.Swings).
    public MeleeDef Swing;
    // WHILE IT RUNS, THE TRIGGER'S WEAPON HOLDS (the whirlwind's spin, the prism stance): no swing and
    // no shot off the trigger. Read by PlayerShip.Stilled, never by the row's id.
    public bool Stills;
    // WHILE IT RUNS, IT DRAWS THE HOSTILE GUNS: an emplacement's gun takes this pilot before any other in its reach
    // (IRaidTarget.Draws, read by Emplacement.Prefer). Read by the flag, never by the row's id. The Taunt.
    public bool Draws;

    // A FLAT TOP SPEED (F1's Add), on top of SpeedStat's multiplier, before the hold: the stat id
    // this row's ship sheet names for it (PlayerShip.SpeedAdds sums every running row's, added in
    // PlayerShip.TopSpeed -- see D18). The Dart's sprint (Ab.Rod) is the first row.
    public string SpeedAdd;
    // A LIFT WHOSE SIZE IS A RUNNING TOTAL (F1's Ramp, D18): builds while it runs and Condition
    // holds, bleeds with the turn (whether or not Condition holds), caps, and drains once the row
    // stops. See RampSpec below -- the row names three stat ids and a condition; the math is not
    // its own. The Dart's Ramjet (Ab.Ramjet).
    public RampSpec Ramp;
    // A DASH (DashSpec below): pressed, it carries the hull a fixed distance along its nose, and the
    // host strikes what lies on that line. The Warrior's lunge is the first row.
    public DashSpec Dash;
    // A STANCE (StanceSpec below): pressed it holds a Status for its Time, pressed again it drops, and any
    // other of the class's abilities pressed ends it; its cooldown runs from the END. The prism stance.
    public StanceSpec Stance;
    // A GUN THAT RELOADS ACTIVELY (ActiveReload.cs): its chamber, its sweet spot and its charge, a
    // row of that table; set on a Hold weapon row, whose trigger then charges a seated round and whose
    // release fires it as Loose, handed the charge share (0 = let go at once, 1 = full; PlayerShip's
    // charge latch, on the host).
    public ReloadSpec Reload;
    public Action<PlayerShip, double> Loose;
    // A GUN DOWN THE NOSE (Bores.cs): set on a Hold weapon row, whose trigger fires the spec's rounds off its
    // rails at the ship's Cadence of the row's Every (Bores.Tick, host). The Dart's Pepperbox.
    public BoreSpec Bore;
    // A ROUND OF ITS OWN FIRED DOWN THE NOSE AS ITS RUN ENDS (Bores.cs; PlayerShip.Part from the row's Expire, host),
    // priced at the top speed the run gave (the row's own SpeedAdd still counted); its Recoil, if it names one, is
    // the share of the hull's speed kept after (the owner, on its own falling edge of a Forces row: PlayerShip._forcedBy).
    // The Rod from God.
    public BoreSpec Parting;
    // A CHARGED ROW: it holds Charges (a stat id) presses; its slot's N counts those spent, and one comes back
    // every Recharge seconds (a stat id), one at a time (PlayerShip.Spend, and TickAbilities' Cool). The tether.
    public string Charges, Recharge;
    // A ZONE IT LAYS (Zones.cs), at the stern or the cursor: a row of Zones.All (PlayerShip.Lay). The tether mine, the curtain, the well.
    public ZoneDef Lays;
    // A DECOY SALVO IT POPS round the hull (Decoys.cs): a row of Decoys.All (PlayerShip.Pop). The flares.
    public DecoyDef Pops;
    // THE COOLDOWN A PRESS SETS (a stat id), for a row whose press spends through PlayerShip.Spend (Pops, Lays):
    // the flares, the curtain.
    public string Cooldown;
    // A TIMED ROW (PlayerShip.RunFor): pressed, it runs Time seconds (a stat id) and its Cooldown starts from the
    // press; while it runs its Hold, lifts and OnDealt apply as any row's. Guard (a stat id) is the share of every
    // blow the hull takes meanwhile (Hardened at that share: two hardenings keep the stronger, D9). The Brace.
    public string Time, Guard;
    // COOLS FROM THE END: its Cooldown starts when its time runs out, not at the press (the Supercarrier's 30 s).
    public bool CoolAfter;
    // A SORTIE ROW (PlayerShip.Launch): pressed, it sends this wing row's craft (Wings.All) -- one sortie a pick for a
    // row flown at a target (the pilot's picks, else the selected), one round the carrier for a ToHull row -- and runs
    // the row's LifeStat. Nothing sent spends nothing. The gunships, the Supercarrier.
    public WingKind? Sends;
    // A BOW SHOT (BowShot below, PlayerShip.FireAlong): pressed, one round of a Shots.All row leaves the nose straight
    // along the heading, and its Cooldown starts from the press. The Long Lance.
    public BowShot Bow;
    // A HOOK (HookSpec below, PlayerShip.Hook): a line to the selected hostile -- flown round what cannot move, towing
    // what can -- cast off by a second press, and its Cooldown from the cast-off. The Grapnel.
    public HookSpec Hook;
    // A FIELD (kits6b-J8): WHILE IT RUNS, its lifts (RateStat, SpeedStat, ...) reach every other live pilot within
    // this stat's radius of the ship too, added to that pilot's own by the same share rule (PlayerShip.Lifts): the
    // Tender's Overdrive. A row that Cuts reaches every pilot inside it, the presser included.
    public string Aura;
    // WHILE IT RUNS, ON THE HOST, EVERY FRAME: handed the ship and the frame's share of what is left (never more
    // than Left), so a field's whole effect is its time exactly (TickAbilities). The Repair field.
    public Action<PlayerShip, double> Tick;
    // A PRESS THAT CUTS COOLING (D45): this stat's seconds off every cooldown still running on every pilot in its
    // Aura -- the rows of its ClassDef.Abilities, never its drive, never a row of the pressing row's own id -- the
    // way the clock would have run them (PlayerShip.CoolBy). Nothing cooling anywhere: refused, and free. The Resupply.
    public string Cuts;

    public SlotState State(PlayerShip s, IHittable selected) =>
        Show != null ? Show(s, selected) : new SlotState { Line = "READY" };
}

// F1'S RAMP: a lift that is a RUNNING TOTAL instead of a fixed multiplier -- the Dart's Ramjet
// rewards flying straight and bleeds it into a turn. A row is nothing but three stat ids (how fast
// it builds, its ceiling, how hard turning costs it) and a condition; the number itself lives in
// the ship's own slot (PlayerShip.Sl(id).Own -- per-ability state is how it reaches the wire, same
// as the reverb's stored damage). Step is the ONE place the arithmetic lives: pure, no ship and no
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

// A BOW SHOT: one round of the Shots.All row Kind, off the nose along the heading (PlayerShip.FireAlong), at the
// ship's own damage, speed and range stats. A row names stat ids, never numbers:
public class BowShot
{
    public int Kind;
    public string Damage, Speed, Range;   // per round, u/s, u
}

// A HOOK: a line to the selected hostile within Reach (PlayerShip.Hook). Round what cannot move (Targeting.Immovable:
// a boss, a structure, a dummy) the OWNER flies the helm Move (HelmMoves: the bite, the pull to Stop off its hull,
// the swing on A/D and W/S between Clear and Reach, for Time) and the host marks it; casting off -- a second press,
// the Time, a web, a disable, the ship's own warp charge -- tears a chunk away (the RIP: RipShare of the anchor's
// maximum hull plus RipFlat, through the damage door; none when the anchor died or warped). What can be thrown
// (an ITowable that Targeting.Throwable passes) is towed off the bow on the Towing.All row Tow and hurled. The
// Cooldown starts at the cast-off. A row names stat ids, never numbers:
public class HookSpec
{
    public HelmMove Move;
    public int Tow;
    public string Reach, Bite, Pull, Stop, Clear, Reel, Time, Cooldown;   // u, s, u/s, u off the hull, u off the hull, u/s, s, s
    public string RipShare, RipFlat;                                        // x the anchor's maximum hull, + hull
}

// A STANCE: a Status held for Time (PlayerShip.Stance), dropped by a second press or by any other of the
// class's three abilities (PlayerShip.DoAbility), its Cooldown from the moment it ends (PlayerShip.EndStance,
// on every peer through the row's Elapsed). Split names the split tick of a prism stance: at most Splits
// catches split in one stance, Every seconds apart (PlayerShip.Split, read by Prism.Catch). Release names
// how long a second press takes to let it go (the anchor's 0.3 s; none: at once), and Keeps leaves it
// running when another of the class's abilities is pressed (the anchor keeps through the flares). Holds
// may be Status.None: a stance whose whole effect is its row's lifts and hold. Stat ids:
public class StanceSpec
{
    public string Time, Cooldown, Every, Splits, Release;
    public Status Holds;
    public bool Keeps;
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

    // THE MENDING LANCE (the Tender's primary, kits6b-J7): held, a beam out of the main barrel onto the first
    // body it touches, a tick every main_interval (PlayerShip.LanceTick, on the host): it burns a hostile and
    // mends a friend. Its slot says what it is on, on every peer.
    public static readonly AbilityDef Lance = new()
    {
        Weapon = true, Id = PlayerShip.LanceSlot, Name = "Mending lance", Short = "LANCE", Kind = AbilityKind.Hold, Default = Key.Space,
        Blurb = "Hold for a beam out of the main barrel. The first thing it touches, it burns if it is hostile and mends if it is a friend -- a pilot, a sentry, the fleet.",
        Show = (s, _) =>
        {
            ref var sl = ref s.Sl(PlayerShip.LanceSlot);
            bool on = sl.Left > 0;
            return new SlotState { Line = !on ? "LANCE" : sl.N == PlayerShip.LanceMend ? "MENDING" : sl.N == PlayerShip.LanceBurn ? "BURNING" : "LANCE", Lit = s.Trigger || on };
        },
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

    // THE LONG LANCE (the Destroyer's F, v1): one torpedo straight off the bow along the heading -- 300, 170 u/s, a
    // 3000 u run -- that stops on the first hostile it touches and never on a missile (Shots row "longlance"); 18 s from
    // the press. A bow-shot row (AbilityDef.Bow, PlayerShip.FireAlong): never refused but COOLING.
    public static readonly AbilityDef LongLance = new()
    {
        Id = "longlance", Name = "Long Lance", Short = "LANCE", Default = Key.F,
        Blurb = "One heavy torpedo straight off the bow: 300 damage to the first hostile it meets, up to 3000 u out. Slow; lead with the hull.",
        Bow = new BowShot { Kind = Shots.LongLance, Damage = "lance_damage", Speed = "lance_speed", Range = "lance_range" },
        Cooldown = "lance_cooldown",
        Press = (s, _) => s.FireAlong("longlance"),
        Refuse = (s, _) => s.Sl("longlance").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "longlance", "lance_cooldown", "LANCE"),
    };

    // SUPPRESSING FIRE (the Destroyer's Q, kits_v2): for 6 s every hostile a director SHELL hits is Suppressed until 3 s
    // after its last hit (StatusSet.OutGuards: guns x0.5, a boss's moves x0.7, supers whole, its throw held; it still
    // moves and webs), a grey chevron over it on every peer; 20 s from the press. A timed row (RunFor) that hears its
    // own ship's blows (OnDealt) and afflicts through PlayerShip.Afflict: the Lance ("longlance") and the PD ("pd") carry
    // other weapon ids and never apply it. A status, so two destroyers do not stack.
    public static readonly AbilityDef Suppress = new()
    {
        Id = "suppress", Name = "Suppressing fire", Short = "SUPPRESS", Default = Key.Q,
        Blurb = "For 6 s every hostile your main guns hit deals half damage with its guns and holds its missiles, until 3 s after its last hit.",
        Time = "suppress_window", Cooldown = "suppress_cooldown",
        OnDealt = (s, t, _, w) => { if (w == Shots.Of(Shots.Shell).Id) s.Afflict(t, Status.Suppressed, "suppress_time", Fx.Chevron); },
        Press = (s, _) => s.RunFor("suppress"),
        Refuse = (s, _) => s.Sl("suppress").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "suppress", "suppress_cooldown", "SUPPRESS"),
    };

    // THE GRAPNEL (the Destroyer's E, kits_v2 / v3 3.2 / v31 3.3): the selected hostile within 700 u. A boss, a structure
    // or a dummy: a 0.15 s bite, the winch hauls the hull in at 450 u/s to 250 u off its hull, then it swings bow-on for
    // the rest of 5 s from the press (A/D round it, W/S reel at 100 u/s, 150-700 u); casting off rips 1% of its maximum
    // hull + 10. A raider: towed 160 u off the bow and hurled (Towing.All "hurl"). 16 s from the cast-off. A hook row.
    public static readonly AbilityDef Grapnel = new()
    {
        Id = "grapnel", Name = "Grapnel", Short = "GRAPNEL", Default = Key.E,
        Blurb = "Hooks the selected hostile within 700 u. A boss, station or dummy: the winch hauls you in and you swing round it bow-on for up to 5 s, and casting off tears 1% of its hull away. A raider: towed off your bow, then hurled. E again casts off.",
        Hook = new HookSpec { Move = HelmMoves.Tether, Tow = Towing.Grapnel, Reach = "grapnel_reach", Bite = "grapnel_bite", Pull = "grapnel_pull",
                              Stop = "grapnel_stop", Clear = "grapnel_clear", Reel = "grapnel_reel", Time = "grapnel_swing",
                              Cooldown = "grapnel_cooldown", RipShare = "grapnel_rip_share", RipFlat = "grapnel_rip_flat" },
        Press = (s, t) => s.Hook("grapnel", t),
        Expire = s => s.CastOffHook("grapnel", rip: true),
        Refuse = (s, t) => s.HookRefusal("grapnel", t),
        Show = (s, _) => Timed(s, "grapnel", "grapnel_cooldown", "HOOKED"),
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

    // WARP GUNSHIPS (the Carrier's E, kits_v2): two craft to each of up to three picks (else the selected), warped onto
    // a 240 u circle round it for 12 s, 10 DPS each; a pick past 3000 u gets none. 25 s from the press.
    public static readonly AbilityDef Gunships = new()
    {
        Id = "gunships", Name = "Warp gunships", Short = "GUNSHIPS", Default = Key.E,
        Blurb = "Two gunships warp onto each of up to three picked targets (or the selected one) within 3000 u and circle it for 12 s.",
        Sends = WingKind.Gunship, TakesTargets = true, Cooldown = "gunship_cooldown",
        Press = (s, t) => s.Launch("gunships", t),
        Refuse = (s, sel) =>
        {
            if (s.Sl("gunships").Cool > 0) return "COOLING";
            var picks = (s.GetParent() as Hub)?.Targets.Where(t => t != null && t.Alive).ToList() is { Count: > 0 } ts ? ts : sel != null ? new List<IHittable> { sel } : new List<IHittable>();
            if (picks.Count == 0) return "NO TARGET";
            return picks.Any(p => Wings.Within(s, Wings.Of(WingKind.Gunship), p)) ? null : "OUT OF RANGE";
        },
        Show = (s, _) => Timed(s, "gunships", "gunship_cooldown", "GUNSHIPS"),
    };

    // SUPERCARRIER (the Carrier's Q, kits_v3 §3.1 / v31 §3.2): a second, automated wing -- the fighter's own numbers --
    // circling the carrier for 20 s and taking whatever Turret.Rank puts first inside 600 u of it, missiles included
    // (Wings.All "patrol"). 30 s from the END. Its slot id is the patrol ring's (Fx.Fields keys the ring on "super").
    public static readonly AbilityDef Supercarrier = new()
    {
        Id = "super", Name = "Supercarrier", Short = "SUPER", Default = Key.Q,
        Blurb = "A second wing circles the carrier for 20 s, taking anything that comes within 600 u of it, missiles included.",
        Sends = WingKind.Patrol, Cooldown = "super_cooldown", CoolAfter = true,
        Press = (s, _) => s.Launch("super", null),
        Refuse = (s, _) => s.Sl("super").Left > 0 ? "RUNNING" : s.Sl("super").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "super", "super_cooldown", "SUPER"),
    };

    // ── the freighters ───────────────────────────────────────────────────────
    public static readonly AbilityDef Deploy = new()
    {
        Weapon = true, Id = "deploy", Name = "Sentry", Short = "SENTRY", Default = Key.R, TakesPoint = true,
        Blurb = "Throws a sentry to the cursor, up to 600 u; it lands 0.8 s later and shoots what comes near. R with the cursor on one of yours recalls it.",
        Press = (s, _) => s.DeployTurret(s.Sl("deploy").At),
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

    // TIME ON TARGET (6b, D36): every gun that can reach the paint -- the spotter and each landed
    // sentry within tot_reach of it -- lands one line on it in the same host tick (Lines.Tot).
    public static readonly AbilityDef Tot = new()
    {
        Id = "tot", Name = "Time on target", Short = "T.O.T.", Default = Key.F,
        Blurb = "The spotter and every sentry in reach fire one rail line each at the painted target, all landing at once.",
        Press = (s, _) => s.TimeOnTarget(),
        Refuse = (s, _) => s.Sl("tot").Cool > 0 ? "COOLING" : s.Painted == null ? "NO PAINT" : null,
        Show = (s, _) => s.Sl("tot").Cool > 0 || s.Painted != null ? Timed(s, "tot", "tot_cooldown", "READY")
                                                                   : new SlotState { Line = "NO PAINT" },
    };

    public static readonly AbilityDef Bubble = new()
    {
        Id = "bubble", Name = "Bubble", Short = "BUBBLE", Default = Key.Q,
        Blurb = "A bubble over you and every friendly hull near -- allies, sentries, the fleet: it soaks damage until its pool is spent or the time is up.",
        Press = (s, _) => s.RaiseBubble(),
        Refuse = (s, _) => s.Sl("bubble").Cool > 0 ? "CHARGING" : null,
        Show = (s, _) => Timed(s, "bubble", "bubble_cooldown", $"UP {s.Sl("bubble").N}"),
    };

    // REDEPLOY (kits6b-J3): every sentry out folds and lands round the hull a second later.
    public static readonly AbilityDef Redeploy = new()
    {
        Id = "redeploy", Name = "Redeploy", Short = "REDEPLOY", Default = Key.E,
        Blurb = "Every sentry you have out folds up and lands in a ring round you a second later, each with the hull it had.",
        Press = (s, _) => s.Redeploy(),
        Refuse = (s, _) => s.Sl("redeploy").Cool > 0 ? "COOLING" : s.OwnLanded().Count == 0 ? "NONE OUT" : null,
        Show = (s, _) => Timed(s, "redeploy", "redeploy_cooldown", "READY"),
    };

    // THE OVERDRIVE FIELD (the Tender's ability 1, kits6b-J8): for its time every gun of yours, and of every pilot within
    // field_radius -- their sentries and craft through them -- fires overdrive_mult as fast (an Aura row).
    public static readonly AbilityDef Overdrive = new()
    {
        Id = "overdrive", Name = "Overdrive field", Short = "OVERDRIVE", Default = Key.F,
        Blurb = "For eight seconds you and every friendly pilot near you fire half again as fast -- guns, point defence, sentries and craft.",
        Press = (s, _) => s.StartOverdrive(),
        RateStat = "overdrive_mult", Aura = "field_radius",
        Refuse = (s, _) => s.Sl("overdrive").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "overdrive", "overdrive_cooldown", $"x{s.Stats["overdrive_mult"]:0.#}"),
    };

    // THE REPAIR FIELD (the Tender's ability 2, kits6b-J8): for repair_time every friendly hull within field_radius --
    // you, a pilot, a sentry, the fleet -- is mended repair_share of its MAXIMUM a second (Mend, "repair").
    public static readonly AbilityDef Repair = new()
    {
        Id = "repair", Name = "Repair field", Short = "REPAIR", Default = Key.Q, Aura = "field_radius",
        Blurb = "For eight seconds every friendly hull near you -- yours too -- is repaired 2% of its full hull a second.",
        Press = (s, _) => s.StartRepair(),
        Tick = (s, dt) => s.RepairTick(dt),
        Refuse = (s, _) => s.Sl("repair").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "repair", "repair_cooldown", "UP"),
    };

    // RESUPPLY (the Tender's ability 3, kits6b-J8, D45): 8 s off every ability still cooling on every pilot within
    // field_radius, you included -- never a drive, never a Resupply.
    public static readonly AbilityDef Resupply = new()
    {
        Id = "resupply", Name = "Resupply", Short = "RESUPPLY", Default = Key.E, Aura = "field_radius", Cuts = "resupply_cut",
        Cooldown = "resupply_cooldown",
        Blurb = "Takes eight seconds off every ability still cooling -- yours and every friendly pilot's near you. Not the drive.",
        Press = (s, _) => s.Resupply("resupply"),
        Refuse = (s, _) => s.Sl("resupply").Cool > 0 ? "COOLING" : s.CoolingInAura("resupply") == 0 ? "NOTHING COOLING" : null,
        Show = (s, _) => Timed(s, "resupply", "resupply_cooldown", "READY"),
    };

    public static readonly AbilityDef Buster = new()
    {
        Id = "buster", Name = "Bunker buster", Short = "BUSTER", Default = Key.F, TakesPoint = true,
        Blurb = "One slow heavy round at the cursor that stops on the first thing it meets: double on a boss or a structure, and a quarter of that through a pylon's shield.",
        Press = (s, _) => s.FireBuster(),
        Refuse = (s, _) => s.Sl("buster").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "buster", "buster_cooldown", "READY"),
    };

    public static readonly AbilityDef Shockwave = new()
    {
        Id = "shockwave", Name = "Shockwave", Short = "WAVE", Default = Key.Q,
        Blurb = "Throws everything near you clear. What cannot be thrown -- a boss, a structure -- is held still instead.",
        Press = (s, _) => s.Shockwave(),
        Refuse = (s, _) => s.Sl("shockwave").Cool > 0 ? "CHARGING" : null,
        Show = (s, _) => Timed(s, "shockwave", "wave_cooldown", "READY"),
    };

    // THE BRACE (the Battleship's Q, v1): 3 s taking x0.35 of every blow at half the top speed; 25 s from the press.
    // A timed row (RunFor): its guard runs through a warp jump, and the broadside and guns keep firing under it.
    public static readonly AbilityDef Brace = new()
    {
        Id = "brace", Name = "Brace", Short = "BRACE", Default = Key.Q,
        Blurb = "For 3 s the hull takes 35% of every blow, at half its top speed. The guns keep firing.",
        Time = "brace_time", Guard = "brace_share", Cooldown = "brace_cooldown", Hold = 0.5,
        Press = (s, _) => s.RunFor("brace"),
        Refuse = (s, _) => s.Sl("brace").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "brace", "brace_cooldown", "BRACED"),
    };

    // CIWS (the Battleship's E, v1): 6 s of both point-defence mounts at x8 rate and x3 damage (1.5 every 0.0625 s,
    // 24 DPS a mount) on PD's own prey and rank (Targeting.PointDefence, Turret.Rank); 20 s from the press. A scoped
    // lift (RateOn / DamageOn): nothing but the PD mounts. An overshoot's Disabled stops the lift (decision 13: it is
    // a gun), while the plain PD keeps firing.
    public static readonly AbilityDef Ciws = new()
    {
        Id = "ciws", Name = "CIWS", Short = "CIWS", Default = Key.E,
        Blurb = "For 6 s both point-defence turrets fire eight times as fast for three times the damage: missiles, fighters and light craft within 460 u.",
        Time = "ciws_time", Cooldown = "ciws_cooldown",
        RateStat = "ciws_rate", RateOn = "pd_interval", DamageStat = "ciws_damage", DamageOn = "pd_damage",
        While = s => !s.Disabled,
        Press = (s, _) => s.RunFor("ciws"),
        Refuse = (s, _) => s.Sl("ciws").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "ciws", "ciws_cooldown", "CIWS"),
    };

    public static readonly AbilityDef Well = new()
    {
        Id = "well", Name = "Gravity well", Short = "WELL", Default = Key.E, TakesPoint = true,
        Blurb = "A well at the cursor that drags loose raiding craft into its centre -- the light ones twice as fast. Bosses, structures and anything latched stay put.",
        Lays = Zones.All[Zones.Well], Cooldown = "well_cooldown",
        Press = (s, _) => s.Lay("well"),
        Refuse = (s, _) => s.Sl("well").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "well", "well_cooldown", "READY"),
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

    // THE RAILGUN (the Sniper's primary, sniper_active_reload.md): one round in the chamber (ActiveReload.Rail).
    // Seated, Space held charges it and its release fires (Loose, handed the charge share); after
    // every shot it reloads by itself, and Space inside the sweet spot enhances the round loading.
    public static readonly AbilityDef Railgun = new()
    {
        Weapon = true, Id = "railgun", Name = "Railgun", Short = "RAIL", Kind = AbilityKind.Hold, Default = Key.Space,
        Blurb = "Hold to charge, let go to fire: a straight blue line through everything on it. It reloads in 3 s after every shot; Space in the white box (0.6 s of the 3 s) makes the next round hit x1.5.",
        Reload = ActiveReload.Rail,
        Elapsed = s => ActiveReload.Seat(s, ActiveReload.Rail),
        Loose = (s, share) => s.FireRail(share),
        Show = (s, _) => ActiveReload.Slot(s, ActiveReload.Rail),
    };

    // THE ANCHOR (the Sniper's F, v1): up to 8 s rooted (a hold of x0, so the boost is refused ANCHORED),
    // every interval x2.5 (the charge AND the reload, through Cadence), the rail's reach x1.4; F again
    // weighs it in 0.3 s; the cooldown runs 12 s from the release. The flares and the tether keep it.
    public static readonly AbilityDef Anchor = new()
    {
        Id = "anchor", Name = "Anchor", Short = "ANCHOR", Default = Key.F,
        Blurb = "Plant the ship for up to 8 s: it cannot move or turn, but the railgun charges and reloads x2.5 and reaches x1.4. F again weighs it in 0.3 s.",
        Stance = new StanceSpec { Time = "anchor_time", Cooldown = "anchor_cooldown", Release = "anchor_release", Holds = Status.None, Keeps = true },
        Hold = 0, RateStat = "anchor_rate", ReachStat = "anchor_reach",
        Press = (s, _) => s.Stance("anchor"),
        Elapsed = s => s.EndStance("anchor"),
        Refuse = (s, _) => s.Sl("anchor").Left <= 0 && s.Sl("anchor").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Sl("anchor").Left > s.Stats["anchor_release"]
            ? new SlotState { Line = $"ANCHORED {s.Sl("anchor").Left:0.0}s", Lit = true }
            : s.Sl("anchor").Left > 0 ? new SlotState { Line = "WEIGHING", Lit = true }
            : Timed(s, "anchor", "anchor_cooldown", "READY"),
    };

    // THE TETHER MINE (the Sniper's Q, v1): dropped at the stern, live 0.5 s later; the first raiding craft
    // within 170 u sets it off and every one within 170 u is held 3 s. Two charges, 12 s each; two out at
    // most, the oldest goes. It keeps the Anchor.
    public static readonly AbilityDef Tether = new()
    {
        Id = "tether", Name = "Tether mine", Short = "TETHER", Default = Key.Q,
        Blurb = "Drops a mine astern. Half a second later the first raider within 170 u sets it off, and every raider within 170 u is held for 3 s. Two charges; never a boss.",
        Charges = "tether_charges", Recharge = "tether_recharge", Lays = Zones.All[Zones.Tether],
        Press = (s, _) => s.Lay("tether"),
        Refuse = (s, _) => s.Sl("tether").N >= (int)s.Stats["tether_charges"] ? "NO CHARGES" : null,
        Show = (s, _) =>
        {
            int max = (int)s.Stats["tether_charges"], left = max - s.Sl("tether").N;
            return new SlotState { Line = $"{left}/{max}", Lit = left > 0,
                                   Busy = left < max ? (float)(s.Sl("tether").Cool / s.Stats["tether_recharge"]) : 0f };
        },
    };

    // THE FLARES (the Sniper's E, kits_v2's card): six in a ring round the hull, burning 5 s where they stop
    // (Decoys.All "flares": the lure, the mark and the dazzle are Decoys.Tick's). 16 s; only the cooldown refuses.
    // It keeps the Anchor.
    public static readonly AbilityDef Flares = new()
    {
        Id = "flares", Name = "Flares", Short = "FLARES", Default = Key.E,
        Blurb = "Six flares in a ring 180 u out, burning 5 s: guided missiles turn onto them, landing marks slide onto them, and raiders near one are dazzled (no new web, no missile).",
        Pops = Decoys.All[Decoys.Flares], Cooldown = "flare_cooldown",
        Press = (s, _) => s.Pop("flares"),
        Refuse = (s, _) => s.Sl("flares").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "flares", "flare_cooldown", "READY"),
    };

    // THE TAUNT (the Warden's Q, kits_v3 §3.5): for 6 s every raider standing within 1000 u, or hunting a target
    // within it, comes for the Warden and takes x1.5 from all it deals; the Warden takes 33% less meanwhile, and an
    // emplacement's gun with the Warden in reach takes it first (Draws).
    public static readonly AbilityDef Taunt = new()
    {
        Id = "taunt", Name = "Taunt", Short = "TAUNT", Default = Key.Q,
        Blurb = "For 6 s every raider within 1000 u, or hunting anything within it, comes for you instead, and takes half again from everything you deal. You take 33% less meanwhile, and an enemy emplacement with you in reach fires at you first. Never a boss.",
        Draws = true,
        Press = (s, _) => s.Taunt(),
        Refuse = (s, _) => s.Sl("taunt").Left > 0 ? "TAUNTING" : s.Sl("taunt").Cool > 0 ? "COOLING" : null,
        // running, the slot reads its time and its guard (kits_v3 §3.5: "TAUNT 4.2s  -33%")
        Show = (s, _) => s.Sl("taunt").Left > 0
            ? new SlotState { Line = $"TAUNT {s.Sl("taunt").Left:0.0}s  -{(1 - s.Stats["taunt_guard"]) * 100:0}%", Lit = true }
            : Timed(s, "taunt", "taunt_cooldown", "TAUNT"),
    };

    // THE FLAK CURTAIN (the Warden's E, kits_v2's card): 500 x 80 u at the cursor (150-700 u), across the aim, live
    // 0.5 s after the press for 6 s: a raider touching it takes 20, then 10 every 0.5 s (Zones.All "curtain"). 18 s.
    public static readonly AbilityDef Curtain = new()
    {
        Id = "curtain", Name = "Flak curtain", Short = "CURTAIN", Default = Key.E,
        Blurb = "A wall of flak 500 u long at the cursor (150-700 u away), across your aim, for 6 s: a raider touching it takes 20, then 10 every half second. Nothing slows; bosses and missiles pass untouched.",
        Lays = Zones.All[Zones.Curtain], Cooldown = "curtain_cooldown",
        Press = (s, _) => s.Lay("curtain"),
        Refuse = (s, _) => s.Sl("curtain").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "curtain", "curtain_cooldown", "READY"),
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
    // THE PEPPERBOX (the Dart's primary, kits_v2's card): held, two nose rails alternate, 6 darts a second
    // (pepper_interval through Cadence); each leaves along the nose at 520 u/s plus the ship's own velocity and
    // turns up to 6 rad/s onto the pilot's LIVE cursor (Shots "pepper", Commanded), 750 u, the first hostile
    // body. 7.5 a dart, priced on the host at the launch: x clamp(top speed / 260, 1, 1.5).
    public static readonly AbilityDef Pepperbox = new()
    {
        Weapon = true, Id = "pepperbox", Name = "Pepperbox", Short = "PEPPER", Kind = AbilityKind.Hold, Default = Key.Space,
        Blurb = "Hold to fire: two nose rails, six darts a second, each steering onto the cursor as it flies. The faster you are going, the harder they hit: up to half again at 390 u/s.",
        Bore = new BoreSpec { Shot = Shots.Pepper, Damage = "pepper_damage", Every = "pepper_interval", Speed = "pepper_speed",
                              Range = "pepper_range", Turn = "pepper_turn", PriceTop = "price_top", PriceCap = "pepper_cap",
                              Rails = new[] { -5f, 5f } },
        Show = (s, _) => new SlotState { Line = $"x{Bores.Price(1, s.TopNow, s.Stats["price_top"], s.Stats["pepper_cap"]):0.00}", Lit = s.Trigger },
    };

    // ROD FROM GOD (the Dart's F, kits_v2's card): a 3 s sprint -- thrust x3, top +100 u/s, the throttle forced open
    // (S dead, the rudder free) -- and as it ends a rod down the nose at 300 u/s plus the ship's own, 1400 u, through
    // every hostile body on its line once (never a missile): 180 x clamp(top / 260, 1, 2) at the top the sprint gave.
    // The hull keeps 30% of its speed after. 12 s from the press. Wrecked mid-sprint: no rod.
    public static readonly AbilityDef Rod = new()
    {
        Id = "rod", Name = "Rod from God", Short = "ROD", Default = Key.F,
        Blurb = "Sprint for 3 s, thrust x3 and 100 u/s over your top, the throttle wide open; as it ends a rod leaves the nose and goes through everything in 1400 u. The faster you were going, the harder it hits: up to twice at 520 u/s. You keep 30% of your speed after.",
        ThrustStat = "sprint_thrust", SpeedAdd = "sprint_add", Forces = true,
        Parting = new BoreSpec { Shot = Shots.Rod, Damage = "rod_damage", Speed = "rod_speed", Range = "rod_range",
                                 PriceTop = "price_top", PriceCap = "rod_cap", Recoil = "rod_recoil" },
        Press = (s, _) => s.Run("rod", "sprint_time", "rod_cooldown"),
        Expire = s => s.Part("rod"),
        Refuse = (s, _) => s.Sl("rod").Left > 0 ? "SPRINTING" : s.Sl("rod").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "rod", "rod_cooldown", "SPRINT"),
    };

    // RAMJET (the Dart's Q, kits_v3 §3.6): lit 8 s; at full throttle with the keel within 5% of the current top it builds
    // +10% top a second, to +50%; a full turn bleeds 20% a second whether or not it builds (the slide never does); once
    // it goes out it drains in at most 1 s. 20 s from the press. A Ramp row (F1, D18): the owner flies its own total, the
    // host prices from ITS copy, stepped from the guest's reports (PlayerShip.RampsFromReport).
    public static readonly AbilityDef Ramjet = new()
    {
        Id = "ramjet", Name = "Ramjet", Short = "RAMJET", Default = Key.Q,
        Blurb = "Lit for 8 s: flat out and flying straight, your top speed climbs 10% a second, to half again. Turning bleeds it. The faster you go, the harder your darts and your rod hit.",
        Ramp = new RampSpec { Build = "ramjet_build", Cap = "ramjet_cap", Bleed = "ramjet_bleed", Condition = s => s.FullAhead(0.95f) },
        Press = (s, _) => s.Run("ramjet", "ramjet_time", "ramjet_cooldown"),
        Refuse = (s, _) => s.Sl("ramjet").Left > 0 ? "LIT" : s.Sl("ramjet").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Sl("ramjet").Left > 0
            ? new SlotState { Line = $"+{s.Sl("ramjet").Own * 100:0}% {s.Sl("ramjet").Left:0.0}s", Lit = true }
            : Timed(s, "ramjet", "ramjet_cooldown", "READY"),
    };

    // SLINGSHOT (the Dart's E, kits_v2's card): the heading AND the whole velocity (the slide included) snapped onto the
    // cursor's bearing, up to 180°, the speed kept -- a snap, not a turn, so the Ramjet keeps what it built (the host's
    // copy skips the report: SkipYaw). A sprint's rod leaves down the new nose. 6 s.
    public static readonly AbilityDef Slingshot = new()
    {
        Id = "slingshot", Name = "Slingshot", Short = "SLING", Default = Key.E, TakesPoint = true,
        Blurb = "Snaps your nose and all of your speed onto the cursor, even straight behind you. A snap, not a turn: the ramjet keeps what it built.",
        AtOnce = (s, at) => s.Snap(at),
        Press = (s, _) => s.Slung("slingshot", "sling_cooldown"),
        Refuse = (s, _) => s.Sl("slingshot").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "slingshot", "sling_cooldown", "READY"),
    };

    // THE ECHO'S REVERB (kits_v2's card): 5 s of guns at x1.2 while it remembers what they deal, then 35% of the lot in
    // 220 u where the last of it landed; 18 s from the press
    public static readonly AbilityDef Reverb = new()
    {
        Id = "reverb", Name = "Reverb", Short = "REVERB", Default = Key.F,
        Blurb = "Your guns run hot for five seconds while the reverb remembers what they deal, then a third of it goes off where your last shot landed.",
        RateStat = "reverb_rate",
        Press = (s, _) => s.StartReverb(),
        OnDealt = (s, t, d, w) => { ref var e = ref s.Sl("reverb"); e.Own += d; e.At = t.Position; },
        Expire = s => s.Detonate(),
        Refuse = (s, _) => s.Sl("reverb").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Sl("reverb").Left > 0
            ? new SlotState { Line = $"{s.Sl("reverb").Own:0} STORED", Lit = true }
            : Timed(s, "reverb", "reverb_cooldown", "READY"),
    };

    // THE ECHO'S REWIND (kits_v2's card, the README ruling): back 8 s -- position, heading and velocity on the owner,
    // the hull on the host, each from its own trail (a mark every 0.5 s, the one nearest 8 s ago); every web let go; 30 s
    public static readonly AbilityDef Rewind = new()
    {
        Id = "rewind", Name = "Rewind", Short = "REWIND", Default = Key.Q,
        Blurb = "Back to where you were eight seconds ago: the place, the heading, the speed and the hull. Any web on you lets go.",
        AtOnce = (s, _) => s.Rewind(),
        Press = (s, _) => s.Rewound(),
        Refuse = (s, _) => s.Sl("rewind").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "rewind", "rewind_cooldown", "READY"),
    };

    // THE ECHO'S EMP (kits_v2's card): every hostile craft within 300 u JAMMED 4 s (no strikes, no launches, the turret
    // held, a web let go), a second pulse 0.6 s later from where it was pressed refreshing it; no damage; 18 s
    public static readonly AbilityDef Emp = new()
    {
        Id = "emp", Name = "EMP", Short = "EMP", Default = Key.E,
        Blurb = "Jams every hostile craft within 300 u for four seconds: no shots, no missiles, and any web on a friend lets go. It pulses twice.",
        Press = (s, _) => s.StartEmp(),
        Expire = s => s.Pulse(s.Sl("emp").At),
        Refuse = (s, _) => s.Sl("emp").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "emp", "emp_cooldown", "READY"),
    };

    // THE WRAITH'S VENOM (DL3): 6 s coated -- each pellet that lands puts a stack of poison on what it hit (up to 10,
    // 1.25 a second each, for 5 s after the last: DoseRows.Venom); 22 s
    public static readonly AbilityDef Venom = new()
    {
        Id = "venom", Name = "Venom", Short = "VENOM", Default = Key.Q,
        Blurb = "Coats your pellets for six seconds: each one that lands poisons what it hits, up to ten doses, each eating 1.25 a second until five seconds after the last.",
        Press = (s, _) => s.Coat(),
        OnDealt = (s, t, _, weapon) => s.Dose(DoseRows.Venom, t, weapon),
        Refuse = (s, _) => s.Sl("venom").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "venom", "venom_cooldown", "COATED"),
    };

    // THE WRAITH'S SHADOW STEP (DL3): a blink to 140 u behind the selected hostile within 900 u, nose on it, the speed
    // kept; any web lets go; 14 s. The owner blinks at the press (flight is its own); the host counts it.
    public static readonly AbilityDef Step = new()
    {
        Id = "step", Name = "Shadow step", Short = "STEP", Default = Key.E,
        Blurb = "Blinks you 140 u behind the hostile you have selected, up to 900 u off, nose on it and your speed kept. Any web on you lets go.",
        AtOnce = (s, _) => s.Step(s.PressTarget),
        Press = (s, t) => s.Stepped(t),
        Refuse = (s, t) => !PlayerShip.Steppable(t) ? "NO TARGET"
                         : s.Position.DistanceTo(t.Position) > s.Stats["step_reach"] ? "OUT OF REACH"
                         : s.Sl("step").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => Timed(s, "step", "step_cooldown", "READY"),
    };

    // THE WRAITH'S VEIL (DL3): 5 s nothing hostile can pick it (it can still be hit), x1.35 top speed, and the next volley
    // x3 -- fired from inside it, which ends it, or the first after; 18 s
    public static readonly AbilityDef Veil = new()
    {
        Id = "veil", Name = "Veil", Short = "VEIL", Default = Key.F,
        Blurb = "For five seconds nothing hostile can pick you, and you run faster. Your next volley hits three times as hard; firing drops the veil.",
        SpeedStat = "veil_speed",
        Press = (s, _) => s.Veil(),
        OnFire = s => s.Unveil(),
        Refuse = (s, _) => s.Sl("veil").Cool > 0 ? "COOLING" : null,
        Show = (s, _) => s.Statuses.Has(Status.Untargetable)
            ? new SlotState { Line = $"VEILED {s.Statuses.Left(Status.Untargetable):0.0}s", Lit = true }
            : Timed(s, "veil", "veil_cooldown", "READY"),
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
    [Live] private static readonly Dictionary<ShipClass, AbilityDef[]> _full = new();

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

    // A PRESSED POINT HELD TO A REACH (F8): the point itself if it lies within `reach` of `from`, else
    // the point `reach` out on the same bearing. Pure; a row that takes a point (TakesPoint) and
    // has a reach reads its slot's At through this.
    public static Vector2 Toward(Vector2 from, Vector2 at, float reach)
    {
        var d = at - from;
        return d.Length() <= reach ? at : from + d.Normalized() * reach;
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
    [Live] private static readonly Dictionary<ShipClass, string> _hints = new();

    public static string ControlsHint(ShipClass c)
    {
        if (_hints.TryGetValue(c, out var h)) return h;
        if (!Classes.Known(c)) return _hints[c] = "placeholder";      // not a class this build has
        string own = Classes.Of(c).Hint;
        string drive = Drives.Of(c) is { } d ? "  ·  " + d.Controls : "";            // V, and Shift on a hull that slides
        return _hints[c] = (own.Length > 0 ? own + "  ·  " : "") + CommonHint + drive + "  ·  Esc menu";
    }
}
