using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// HELM MOVES (F8) -- flight the OWNER flies under a law other than the helm's, for as long as a move
// lasts: the Grapnel's pull and swing first (kits_v3 §3.2), then the dashes and blinks of the heavies
// and lights (6c / 6d) as rows.
//
// NEW (slice 4); it replaced nothing. The owner flies its own hull (PlayerShip.LocalFlight), so a move
// that drags, swings or throws it is flown there too: while a HelmRun lasts, LocalFlight calls this
// instead of Steer (B's helm, untouched), and the reports carry the result like any other flight.
//
// WARDS. While the host's mark runs, the host lets that ship's reports claim the move's speed (Ward):
// its speed clamp and its jump pricing (Drives, F24) read it instead of the hull's own top, so a 450
// u/s pull is neither cut back to the helm's 117 nor priced as a jump.
//
// HOST CONFIRM. The owner starts a move on its own press, at once (PlayerShip.BeginHelm); the host,
// taking the same press, MARKS the move's slot (Confirm: Left = the move's time, N = the anchor's
// NetId) and later ends it (Release, or the slot's own clock). The owner casts off if the mark has not
// come within ConfirmWithin of its press, and the moment a mark it had is gone -- so a guest's move
// the host refused lasts half a second at most. The host's own ship is marked in the same press.
//
// A NEW ROW fills in: its Id; its Law (one owner frame: the ship, its run, throttle and rudder, dt;
// it moves the hull and never decides the end -- Fly does, by the same rules for every row). Its
// numbers come in the HelmNums the ability that starts it fills from its own stat rows (the
// destroyer's grapnel_* rows, 6a), so gear moves them; nothing here is a number but the fixed rules.
// ─────────────────────────────────────────────────────────────────────────────
public class HelmMove
{
    public string Id;
    public Action<PlayerShip, HelmRun, float, float, float> Law;
}

// A MOVE'S NUMBERS, filled by the ability that starts it. Slot: the ability's slot the host marks.
// The tether's: Bite (s before the winch hauls), Pull (u/s inward), Stop (u off the anchor's hull
// where the pull ends), Clear (the shortest line, u off the hull), Reach (the longest line, and the
// press's range, from the anchor's centre), Reel (W/S, u/s), Time (s from the press).
public struct HelmNums
{
    public string Slot;
    public float Bite, Pull, Stop, Clear, Reach, Reel;
    public double Time;
}

// WHY A MOVE ENDED, for the ability that started it (the rip reads it, 6a) and the checks.
public enum HelmEnd { None, Pressed, Time, AnchorLost, Webbed, Disabled, Drive, Unconfirmed, Host }

// ONE SHIP'S MOVE, on its owner. Move is null when none runs.
public class HelmRun
{
    public HelmMove Move;
    public HelmNums N;
    public IHittable Anchor;
    public int AnchorId;
    public double T;             // seconds since the press
    public float Line;           // the swing's line, centre to centre
    public float Side;           // the speed round the anchor (starboard +); NaN until the move reads it
    public bool Pulling, Confirmed;
    public HelmEnd Ended;        // the last move's end
    // THE HOST'S SIDE of a guest's move (the WARD): the slot it marked and the speed it lets the
    // guest's reports claim while that slot runs.
    public string WardSlot;
    public float Ward;
    public bool On => Move != null;
}

// A hull's flight state, for the pure laws: where it is, how it moves, its heading.
public struct HelmBody { public Vector2 Pos, Vel; public float Rot; }

public static class HelmMoves
{
    public const double ConfirmWithin = 0.5;         // the owner casts off an unmarked move after this

    public static readonly HelmMove Tether = new() { Id = "tether", Law = TetherLaw };
    public static readonly HelmMove[] All = { Tether };

    // ── the tether's pure rules (kits_v3 §3.2) ──────────────────────────────────────────────────
    // where the pull stops, centre to centre: the anchor's hull + Stop (250 off a Lancer's 70 is 320)
    public static float StopAt(float hullR, HelmNums n) => hullR + n.Stop;
    // the shortest line the swing keeps: the anchor's hull + Clear (150)
    public static float Shortest(float hullR, HelmNums n) => hullR + n.Clear;
    // THE LINE A PRESS AT `dist` SWINGS ON: the stop, reached by the pull; pressed already inside it,
    // the distance it has, never under the shortest (no pull: it moves no nearer)
    public static float LineFor(float dist, float hullR, HelmNums n) =>
        dist > StopAt(hullR, n) ? StopAt(hullR, n) : Math.Max(dist, Shortest(hullR, n));
    // how long the pull takes from `dist`: the bite, then the haul in at Pull
    public static double PullTime(float dist, float hullR, HelmNums n) =>
        n.Bite + Math.Max(0f, dist - StopAt(hullR, n)) / n.Pull;
    // the heading whose bow points along `dir`
    public static float Bow(Vector2 dir) => Vector2.Up.AngleTo(dir);

    // ONE FRAME OF THE TETHER. `t`: seconds since the press. `side`: the speed round the anchor, HELD
    // here rather than read back off the velocity each frame -- the velocity's inward part, read
    // against the next frame's turned tangent, would feed the sideways speed a little every frame
    // (a 450 u/s pull added 60 u/s of it a second). PULLING: through the bite the hull
    // coasts, then the winch hauls it in at Pull, its sideways speed kept (capped at `top`), until the
    // line is taut at the stop: the inward speed goes, the sideways stays, and the swing begins. THE
    // SWING: the hull stays `line` from the anchor's centre, rudder (A/D) drives it round (toward
    // rudder x top at `thrust`, its speed kept with no key), throttle reels (W in, S out, at Reel,
    // between Shortest and Reach). The bow is on the anchor throughout.
    public static HelmBody TetherStep(HelmBody b, Vector2 anchor, float hullR, HelmNums n, ref float line, ref bool pulling,
                                      ref float side, double t, float throttle, float rudder, float top, float thrust, float dt)
    {
        var toward = anchor - b.Pos;
        float dist = toward.Length();
        var inward = dist > 1e-3f ? toward / dist : Vector2.Up.Rotated(b.Rot);
        var tangent = new Vector2(-inward.Y, inward.X);       // starboard of a bow on the anchor: D
        if (float.IsNaN(side) || (pulling && t < n.Bite)) side = Mathf.Clamp(b.Vel.Dot(tangent), -top, top);
        if (pulling)
        {
            if (t < n.Bite) { b.Pos += b.Vel * dt; b.Rot = Bow(anchor - b.Pos); return b; }
            float stopAt = StopAt(hullR, n);
            if (dist - n.Pull * dt <= stopAt)
            {   // taut: on the stop, the inward speed gone
                b.Pos = anchor - inward * stopAt;
                b.Vel = tangent * side;
                pulling = false;
                line = stopAt;
            }
            else
            {
                b.Vel = inward * n.Pull + tangent * side;
                b.Pos += b.Vel * dt;
            }
            b.Rot = Bow(anchor - b.Pos);
            return b;
        }
        if (rudder != 0f) side = Mathf.MoveToward(side, rudder * top, thrust * dt);
        float vt = side = Mathf.Clamp(side, -top, top);
        float was = line;
        line = Mathf.Clamp(line - throttle * n.Reel * dt, Shortest(hullR, n), n.Reach);
        // round the anchor: +vt (starboard) turns the outward arm the negative way
        var outward = (-inward).Rotated(-vt * dt / Mathf.Max(line, 1f));
        b.Pos = anchor + outward * line;
        var nowIn = -outward;
        b.Vel = new Vector2(-nowIn.Y, nowIn.X) * vt + outward * ((line - was) / Mathf.Max(dt, 1e-6f));
        b.Rot = Bow(nowIn);
        return b;
    }

    private static void TetherLaw(PlayerShip s, HelmRun r, float throttle, float rudder, float dt)
    {
        var b = TetherStep(new HelmBody { Pos = s.Position, Vel = s.Velocity, Rot = s.Rotation },
                           r.Anchor.Position, r.Anchor.HitRadius, r.N, ref r.Line, ref r.Pulling, ref r.Side, r.T,
                           throttle, rudder, s.TopNow, (float)s.Stats["thrust"], dt);
        s.Position = b.Pos; s.Velocity = b.Vel; s.Rotation = b.Rot;
    }

    // ── the owner ───────────────────────────────────────────────────────────────────────────────
    // A PRESS (PlayerShip.BeginHelm): why it cannot start, or null and the run begun. The anchor is
    // the press's target, measured centre to centre against the Reach.
    public static string Begin(PlayerShip s, HelmRun r, HelmMove m, IHittable anchor, HelmNums n)
    {
        if (anchor == null || !anchor.Alive) return "NO TARGET";
        if (s.Pinned) return "WEBBED";
        if (s.Disabled) return "DISABLED";
        float dist = s.Position.DistanceTo(anchor.Position);
        if (dist > n.Reach) return "OUT OF RANGE";
        r.Move = m; r.N = n; r.Anchor = anchor; r.AnchorId = anchor.NetId;
        r.T = 0; r.Confirmed = false; r.Ended = HelmEnd.None; r.Side = float.NaN;
        r.Pulling = dist > StopAt(anchor.HitRadius, n);
        r.Line = LineFor(dist, anchor.HitRadius, n);
        return null;
    }

    // WHY THE RUN MUST END NOW, or None: the same rules for every row.
    private static HelmEnd Why(PlayerShip s, HelmRun r)
    {
        r.Anchor = Combat.ById(r.AnchorId);                       // among the living, or null
        if (r.Anchor == null) return HelmEnd.AnchorLost;
        if (s.Pinned) return HelmEnd.Webbed;
        if (s.Disabled) return HelmEnd.Disabled;
        if (s.Charging) return HelmEnd.Drive;
        ref var sl = ref s.Sl(r.N.Slot);
        bool marked = sl.Left > 0 && sl.N == r.AnchorId;
        if (marked) r.Confirmed = true;
        else if (r.Confirmed) return HelmEnd.Host;
        else if (r.T > ConfirmWithin) return HelmEnd.Unconfirmed;
        return r.T >= r.N.Time ? HelmEnd.Time : HelmEnd.None;
    }

    // THE OWNER'S FRAME: true while a move flies the hull (LocalFlight then skips Steer). It ends the
    // run first if it must, and the helm flies this frame.
    public static bool Fly(PlayerShip s, HelmRun r, float throttle, float rudder, float dt)
    {
        if (!r.On) return false;
        r.T += dt;
        var why = Why(s, r);
        if (why != HelmEnd.None) { End(s, r, why); return false; }
        r.Move.Law(s, r, throttle, rudder, dt);
        return true;
    }

    // CAST OFF: |v| is kept -- the hull is turned onto its velocity, so the keel carries it on rather
    // than killing a swing's sideways speed.
    public static void End(PlayerShip s, HelmRun r, HelmEnd why)
    {
        if (!r.On) return;
        r.Move = null; r.Anchor = null; r.Ended = why;
        if (s.Velocity.Length() > 1f) s.Rotation = Bow(s.Velocity);
    }

    // ── the host ────────────────────────────────────────────────────────────────────────────────
    // THE MARK: the host took the press. Only the host speaks for it; its report carries the slot.
    // `time`: the slot's (the move's own Time, as a rule). It wards the move's fastest: the pull, or
    // the swing round at the hull's top while it reels.
    public static void Confirm(PlayerShip s, IHittable anchor, HelmNums n, double time)
    {
        if (!Net.Sim || anchor == null) return;
        ref var sl = ref s.Sl(n.Slot);
        sl.Left = time; sl.N = anchor.NetId;
        s.Helm.WardSlot = n.Slot;
        s.Helm.Ward = Math.Max(n.Pull, new Vector2(s.TopNow, n.Reel).Length());
    }
    // THE WARD NOW: the speed the host lets this ship's reports claim, 0 once the mark is gone.
    public static float Ward(PlayerShip s) =>
        s.Helm.WardSlot != null && s.Sl(s.Helm.WardSlot).Left > 0 ? s.Helm.Ward : 0f;
    // ...and its end, before the slot's own clock: the owner casts off when the report says so.
    public static void Release(PlayerShip s, string slot)
    {
        if (!Net.Sim) return;
        ref var sl = ref s.Sl(slot);
        sl.Left = 0; sl.N = 0;
        if (s.Helm.WardSlot == slot) s.Helm.WardSlot = null;
    }
}
