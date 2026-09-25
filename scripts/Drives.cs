using Godot;
using System;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// DRIVES -- what V does: every hull names ONE drive row (ClassDef.Drive), and V presses it.
//
// REPLACED: PlayerShip's warp block (a 3 s warm-up and a 1200 u hop on every class, with its
// WarpWarmup / WarpRange / WarpHop / WarpEvery and a display ship's own hop and clock) and Hints'
// single "warp" card. The capitals WARP (a Jump, held and released); the other nine BOOST (a
// Surge, tapped). kits_v31 §3.4 (F21).
//
// NOT THIS: the Drake's warp opener is a Boss move row (the move's Warp seconds and its landing
// ring, Boss.cs / Drake.cs). It is a boss's move, never a drive; do not merge the two.
//
// A NEW DRIVE ROW fills in: its Id (the slot's id and its Hints card's), its Kind (Jump: held,
// charged, released into a relocation the host prices; Surge: a timed lift on the ability path),
// its AbilityDef Row (Default = V, Kind, Name, Short, Blurb, Refuse, Show, and Press / SpeedStat for
// a Surge), its StatRows (the numbers, on the sheet, so gear can move them) and its word on the
// controls line. Abilities.For appends the class's drive row after its own, so it has a slot on the
// wire and a box on the bar; it is not in ClassDef.Abilities, so no level wall and no Resupply
// reaches it.
// ─────────────────────────────────────────────────────────────────────────────
public enum DriveKind { Jump, Surge }

public class DriveDef
{
    public string Id;
    public DriveKind Kind;
    public AbilityDef Row;                                 // its slot: the bar, the wire, the press
    public StatRow[] Rows = Array.Empty<StatRow>();        // its numbers, on the sheet (ShipStats)
    public string Controls = "";                           // its part of the controls line
}

// ONE SHIP'S WARP, beside its slot. The charge is the OWNER's (it flies, so it jumps); the slot's
// Cool is the drive's cooldown, the host's word (the owner predicts it); the host's own half is
// the pricing of a jump it saw between two reports.
public class DriveRun
{
    public double Held = -1;     // owner: seconds V has been held on a warp charge; below 0, none
    public bool Remote;          // everyone else: the owner reports a charge (the glow)
    public double Flash;         // the landing flash, on every peer
    public double Lock;          // owner: its own overshoot lock, until the host's Disabled bit arrives
    public Vector2? From;        // host: where the last report of this live ship put it
    public bool Bit;             // host: the last report's charge bit
    public double Quiet;         // host: a relocation it caused -- nothing is priced for this long
    public int Clamped;          // host: reports whose speed the clamp cut (F24)
}

public static class Drives
{
    // THE WARP'S FIXED RULES, proved by literals (kits_v3 §3.9; the numbers per hull are rows):
    public const double Spool = 1.0;                  // held this long before the range grows; a release inside it cancels, no cooldown
    public const float OverCap = 900f;                // the charge stops this far past the safe limit
    public const float PerStep = 300f;                // 2 s disabled for every 300 u over (proportional)
    public const double StepSecs = 2.0;
    public const float Cone = Mathf.Pi / 4f;          // a target or waypoint this near the bow is jumped at
    // The gap between the ship's NOSE and the target's circle: a capital is most of 400 u long, and
    // 60 u of it put a battleship's bow through whatever it aimed at.
    public const float Standoff = 160f;
    public const float SnapAt = 600f;                 // a report this far from the last is a jump, bit or no bit
    public const double FlashFor = 0.6;
    public const double QuietFor = 1.0;               // after a relocation the host made
    public const float ClampShare = 1.1f;             // the host's speed clamp: 10% over the hull's own figure
    // A HARNESS SWITCH, as Net.SkipGoodbye is: the host roles of the smoke test move their guests' hulls
    // by hand, which is exactly a bit-less snap, so they turn this off outside the checks that price
    // one. A charged jump (its bit falls) is priced whatever it says. Always on in play.
    public static bool PriceSnaps = true;

    public static readonly DriveDef Warp = new()
    {
        Id = "warp", Kind = DriveKind.Jump, Controls = "V hold to warp",
        Rows = new StatRow[]
        {
            new() { Group = "Warp", Id = "warp_safe",     Label = "Safe range",     Base = 2400, Unit = "u",   Dec = 0 },
            new() { Group = "Warp", Id = "warp_rate",     Label = "Charge rate",    Base = 800,  Unit = "u/s", Dec = 0 },
            new() { Group = "Warp", Id = "warp_cooldown", Label = "Cooldown",       Base = 20,   Unit = "s",   Dec = 1, Inverse = true },
        },
        Row = new AbilityDef
        {
            Id = "warp", Name = "Warp drive", Short = "WARP", Kind = AbilityKind.Hold, Default = Key.V,
            Blurb = "Hold V. After a moment the range grows out to the safe ring; release to jump along the bow, or to just short of a target or waypoint the bow is on. Held past the ring it reaches further, and you land DISABLED for longer the further over you went.",
            // pressed as an ability (never by V: Hub hands V to PressDrive) it says how it is used
            Refuse = (s, _) => Refusal(s) ?? "HOLD V",
            Show = (s, _) => WarpShow(s),
        },
    };

    public static readonly DriveDef Boost = new()
    {
        Id = "boost", Kind = DriveKind.Surge, Controls = "V boost  ·  Shift + A/D strafe",
        Rows = new StatRow[]
        {
            new() { Group = "Boost", Id = "surge_time",     Label = "Lasts",          Base = 3,   Unit = "s", Dec = 1 },
            new() { Group = "Boost", Id = "surge_cooldown", Label = "Cooldown",       Base = 15,  Unit = "s", Dec = 1, Inverse = true },
            new() { Group = "Boost", Id = "surge_lift",     Label = "Speed and strafe", Base = 1.5, Unit = "x", Dec = 2 },
        },
        Row = new AbilityDef
        {
            Id = "boost", Name = "Boost", Short = "BOOST", Default = Key.V,
            Blurb = "Tap V: more top speed, thrust and strafe for a few seconds, then it recharges. It does not break a web. Hold Shift and A/D to slide the hull sideways, the nose where it is.",
            SpeedStat = "surge_lift",                       // one F1 lift: top, thrust, strafe speed and strafe thrust
            Press = (s, _) => Surge(s),
            Refuse = (s, _) => Refusal(s),
            Show = (s, _) => SurgeShow(s),
        },
    };

    public static readonly DriveDef[] All = { Warp, Boost };

    public static DriveDef Of(ShipClass c) => Classes.Of(c).Drive;
    // The class's drive row as Abilities.For appends it: none for a class this build does not have.
    public static AbilityDef[] RowOf(ShipClass c) => Of(c) is { } d ? new[] { d.Row } : Array.Empty<AbilityDef>();

    // ── the pure rules ──────────────────────────────────────────────────────────────────────
    // THE CHARGE: held seconds -> reach. Inside the spool -1 (a release cancels it); then the rate,
    // stopping at the safe limit + OverCap however long it is held.
    public static float Reach(double held, double rate, double safe) =>
        held < Spool ? -1f : (float)Math.Min((held - Spool) * rate, safe + OverCap);
    // THE PRICE of a jump that went `over` past the safe limit: 2 s a 300 u, at most 900 u of it.
    public static double DisabledFor(double over) => StepSecs * Math.Clamp(over, 0, OverCap) / PerStep;
    // where a jump at `target` ends: on the line from here, just short of its hull
    public static Vector2 Arrival(Vector2 from, Vector2 target, float targetRadius, float shipLength) =>
        target - (target - from).Normalized() * (targetRadius + shipLength * 0.5f + Standoff);

    // THE HOST'S SPEED CLAMP (F24): a legal slide at full speed ahead reads hypot(top, strafe), so
    // top x 1.1 alone would read a boosted slide as a cheat.
    public static float SpeedCap(float top, float strafe) => new Vector2(top, strafe).Length() * ClampShare;

    // WHERE A JUMP OF `reach` LANDS, decided when it jumps, by the heading then: the target or
    // waypoint if it lies within 45 degrees of the bow (short of it, never further than the reach and
    // never backwards), else straight on. The ghost is drawn from the same answer.
    public static Vector2 Landing(PlayerShip s, float reach)
    {
        var bow = Vector2.Up.Rotated(s.Rotation);
        var dest = s.Position + bow * reach;
        var aim = (s.GetParent() as Hub)?.WarpAim() ?? (false, Vector2.Zero, 0f);
        if (aim.has && Mathf.Abs(bow.AngleTo(aim.at - s.Position)) <= Cone)
        {
            var step = Arrival(s.Position, aim.at, aim.radius, s.MyArt.Length) - s.Position;
            if (step.Length() > reach) step = step.Normalized() * reach;
            if (step.Dot(bow) > 0) dest = s.Position + step;
        }
        return dest;
    }

    // ── presses ─────────────────────────────────────────────────────────────────────────────
    // Why V does nothing now, on the owner (a courtesy: the host holds the same rules). Read from the
    // hull, never the class: a hull held at x0 (a railgun's charge, an Anchor) is ANCHORED.
    private static string Refusal(PlayerShip s)
    {
        if (!s.Alive) return "IN STASIS";
        if (s.Disabled) return "DISABLED";
        if (s.Held <= 0f) return "ANCHORED";
        return s.Sl(s.Drive.Id).Cool > 0 ? "COOLING" : null;
    }

    // V pressed: a Surge goes the ability path (the host's slot is the truth; the owner flies at
    // once on its own copy); a Jump starts the owner's charge.
    public static bool Press(PlayerShip s, DriveRun r)
    {
        var d = s.Drive;
        if (d == null || !s.Mine) return false;
        if (d.Kind == DriveKind.Surge)
        {
            s.UseAbility(d.Id, 0);
            if (!Net.Sim) Surge(s);                          // the owner's prediction, under the same guard
            return s.Sl(d.Id).Left > 0;
        }
        if (r.Held >= 0) return false;
        if (Refusal(s) is { } why) { s.Fail(d.Id, why); return false; }
        r.Held = 0;
        return true;
    }

    // THE BOOST: a timed lift on its own slot. Its cooldown runs from the press.
    private static void Surge(PlayerShip s)
    {
        ref var sl = ref s.Sl(Boost.Id);
        if (Refusal(s) != null) return;
        sl.Left = s.Stats["surge_time"];
        sl.Cool = s.Cooling(s.Stats["surge_cooldown"]);
    }

    // ── the frame ───────────────────────────────────────────────────────────────────────────
    public static void Tick(DriveRun r, double dt)
    {
        r.Flash = Math.Max(0, r.Flash - dt);
        r.Lock = Math.Max(0, r.Lock - dt);
        r.Quiet = Math.Max(0, r.Quiet - dt);
    }

    // THE OWNER'S CHARGE: held, it grows; released past the spool, it jumps. Nothing survives stasis
    // or a disable, and a release inside the spool costs nothing.
    public static void TickOwner(PlayerShip s, DriveRun r, bool held, double dt)
    {
        if (r.Held < 0) return;
        if (!s.Alive || s.Disabled || s.Drive?.Kind != DriveKind.Jump) { r.Held = -1; return; }
        r.Held += dt;
        if (held) return;
        float reach = Reach(r.Held, s.Stats["warp_rate"], s.Stats["warp_safe"]);
        r.Held = -1;
        if (reach >= 0) Jump(s, r, reach);
    }

    // THE JUMP, on the owner: it moves, stops, and locks itself at once for any overshoot. The
    // cooldown is predicted here and set by the host from what it measures (Priced); a host-owned
    // ship is its own host, and pays here.
    private static void Jump(PlayerShip s, DriveRun r, float reach)
    {
        var dest = Landing(s, reach);
        double off = DisabledFor(s.Position.DistanceTo(dest) - s.Stats["warp_safe"]);
        s.Position = dest; s.Velocity = Vector2.Zero;
        r.Flash = FlashFor;
        r.Lock = off;
        s.Sl(Warp.Id).Cool = s.Cooling(s.Stats["warp_cooldown"]);
        if (Net.Sim && off > 0) s.ApplyStatus(Status.Disabled, off);
    }

    // ── the host's half: a guest's report ───────────────────────────────────────────────────
    // THE HOST PRICES EVERY JUMP IT SEES, between two reports of the same live ship: the report
    // where the charge bit falls with the hull moved further than it could have flown since the last
    // one, and any snap over SnapAt whatever the bit says -- so a guest that clears the bit early, or
    // never sets it, buys nothing. Only a warp hull is priced. A relocation the host itself made
    // (Quiet) and a ship new to this world (no From) are never a jump. `age`: seconds since the last
    // report; `flight`: the fastest the hull may fly, u/s.
    public static void Priced(PlayerShip s, DriveRun r, Vector2 now, bool bit, float age, float flight)
    {
        bool fell = r.Bit && !bit;
        r.Bit = bit;
        var from = r.From;
        r.From = s.Alive ? now : null;
        if (from is not { } was || r.Quiet > 0 || !s.Alive || s.Drive?.Kind != DriveKind.Jump) return;
        float moved = was.DistanceTo(now);
        float jump = moved - flight * Math.Max(age, 0.05f);
        if (!(fell && jump > 0) && !(PriceSnaps && moved > SnapAt)) return;
        s.Sl(Warp.Id).Cool = s.Cooling(s.Stats["warp_cooldown"]);
        double off = DisabledFor(jump - s.Stats["warp_safe"]);
        if (off > 0) s.ApplyStatus(Status.Disabled, off);
    }

    // THE HOST'S SPEED CLAMP (F24): the velocity it reads off a report, held to SpeedCap of the hull's
    // own lifted top and slide, every cut counted.
    public static Vector2 Clamp(DriveRun r, Vector2 v, float top, float strafe)
    {
        float cap = SpeedCap(top, strafe);
        if (v.Length() <= cap) return v;
        r.Clamped++;
        return v.Normalized() * cap;
    }

    // ── what the slot and the hull bar say ─────────────────────────────────────────────────
    private static SlotState WarpShow(PlayerShip s)
    {
        ref var sl = ref s.Sl(Warp.Id);
        if (s.Charging)
        {
            float reach = s.ChargeReach;
            if (reach < 0) return new SlotState { Line = "WARP  SPOOLING", Lit = true };
            float over = reach - (float)s.Stats["warp_safe"];
            return new SlotState { Line = over > 0 ? $"WARP {reach:0} u  +{over:0}" : $"WARP {reach:0} u", Lit = true };
        }
        double off = Math.Max(s.DriveLock, s.Statuses.Left(Status.Disabled));
        if (s.Disabled) return new SlotState { Line = $"DISABLED {off:0.0} s", Fail = true };
        if (sl.Cool > 0) return new SlotState { Line = $"WARP {sl.Cool:0} s", Busy = (float)(sl.Cool / Math.Max(0.01, s.Stats["warp_cooldown"])) };
        return new SlotState { Line = "WARP READY" };
    }
    private static SlotState SurgeShow(PlayerShip s)
    {
        ref var sl = ref s.Sl(Boost.Id);
        if (sl.Left > 0) return new SlotState { Line = $"BOOST {sl.Left:0.0} s", Lit = true };
        if (sl.Cool > 0) return new SlotState { Line = $"BOOST {sl.Cool:0} s", Busy = (float)(sl.Cool / Math.Max(0.01, s.Stats["surge_cooldown"])) };
        return new SlotState { Line = "BOOST READY" };
    }

    // ── the drawing (PlayerShip._Draw calls this once) ─────────────────────────────────────
    // Every peer: the charge glow while a warp charges, and the flash where it lands. The PILOT alone
    // (never the title screen's ship): the range, in world space -- a faint safe ring; the charge
    // ring growing in the accent colour; past the safe ring the band to +900 tinted amber to red with
    // tick rings at +300 / +600 / +900 labelled 2 s / 4 s / 6 s, the charge ring taking the band's
    // colour; the landing ghost with a thin line to it; and its readout.
    private static readonly Color[] Band = { new(1f, 0.78f, 0.2f), new(1f, 0.5f, 0.12f), new(1f, 0.22f, 0.15f) };
    public static void Draw(PlayerShip s, DriveRun r)
    {
        var acc = s.Accent;
        float len = s.MyArt.Length;
        if (s.Charging || r.Remote)
        {
            float k = s.Charging ? Mathf.Clamp((float)(r.Held / Spool), 0f, 1f) : 0.6f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / (60f - 30f * k));
            s.DrawSetTransform(Vector2.Zero, 0f, new Vector2(0.45f, 1f));
            for (int i = 0; i < 3; i++)
                s.DrawArc(Vector2.Zero, len * (0.55f + 0.08f * i) + 5f * pulse, 0, Mathf.Tau, 48,
                          new Color(acc.R, acc.G, acc.B, (0.25f + 0.5f * k) / (i + 1)), 2f);
            s.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
        if (r.Flash > 0)
        {
            float f = (float)(r.Flash / FlashFor);
            s.DrawCircle(Vector2.Zero, len * (0.4f + 0.6f * (1f - f)), new Color(acc.R, acc.G, acc.B, 0.35f * f));
        }
        if (!s.Mine || s.Demo || !s.Charging) return;

        float safe = (float)s.Stats["warp_safe"], reach = s.ChargeReach;
        var font = ThemeDB.FallbackFont;
        // the labels keep their size on screen however far the camera is out (the ring is 2400 u across)
        float z = 1f / Mathf.Max(0.05f, s.GetViewport()?.GetCamera2D()?.Zoom.X ?? 1f);
        int small = Mathf.RoundToInt(Txt.Size(13) * z), big = Mathf.RoundToInt(Txt.Size(15) * z);
        s.DrawSetTransform(Vector2.Zero, -s.Rotation, Vector2.One);        // world-aligned, centred on the hull
        s.DrawArc(Vector2.Zero, safe, 0, Mathf.Tau, 160, new Color(acc.R, acc.G, acc.B, 0.35f), 2f);
        for (int b = 0; b < Band.Length; b++)
        {
            float inner = safe + b * PerStep;
            s.DrawArc(Vector2.Zero, inner + PerStep * 0.5f, 0, Mathf.Tau, 160, Band[b] with { A = 0.07f }, PerStep);
            s.DrawArc(Vector2.Zero, inner + PerStep, 0, Mathf.Tau, 160, Band[b] with { A = 0.5f }, 2f);
            Txt.Centre(s, font, new Vector2(0, -(inner + PerStep) - 6f * z), $"{StepSecs * (b + 1):0} s", small, Band[b]);
        }
        if (reach >= 0)
        {
            float over = reach - safe;
            var ring = over <= 0 ? acc : Band[Math.Min(Band.Length - 1, (int)(over / PerStep))];
            s.DrawArc(Vector2.Zero, Mathf.Max(reach, 1f), 0, Mathf.Tau, 160, ring with { A = 0.9f }, 3f);
            // the landing ghost: the hull's outline where it will land, and a thin line to it
            var at = Landing(s, reach) - s.Position;
            float hw = s.MyArt.HalfWidth;
            var pts = new[] { new Vector2(0, -len * 0.5f), new Vector2(hw, -len * 0.25f), new Vector2(hw, len * 0.5f),
                              new Vector2(-hw, len * 0.5f), new Vector2(-hw, -len * 0.25f), new Vector2(0, -len * 0.5f) }
                      .Select(p => at + p.Rotated(s.Rotation)).ToArray();
            var ghost = ring with { A = 0.75f };
            s.DrawLine(Vector2.Zero, at, ghost with { A = 0.35f }, 1.5f);
            s.DrawPolyline(pts, ghost, 2f);
            float moved = at.Length(), past = moved - safe;
            string read = past > 0 ? $"+{past:0} u  ·  DISABLED {DisabledFor(past):0.0} s" : $"{moved:0} u";
            Txt.Centre(s, font, at + new Vector2(0, len * 0.5f + 22f * z), read, big, past > 0 ? ring : Ui.Text);
        }
        s.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
