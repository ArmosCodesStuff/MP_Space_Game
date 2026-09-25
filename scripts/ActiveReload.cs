using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// ACTIVE RELOAD -- a gun that holds its round in a CHAMBER, reloads by itself after every shot,
// and pays a press timed inside a SWEET SPOT of that reload: the round being loaded is ENHANCED
// (docs/plans/sniper_active_reload.md). New: nothing did this before. It replaced the Sniper's
// cooldown after the rail (rail_cooldown); the Sniper's railgun is its first row, and nothing here
// knows that.
//
// A NEW ROW (ReloadSpec, reached by the ability id of the gun's own weapon row, AbilityDef.Reload)
// MUST FILL IN: the ability id whose Slot holds the chamber (PlayerShip.Slot: Left = seconds of
// reload left, Own = this reload's full length, N = the Chamber below), and the stat ids it reads --
// the reload (seconds, Inverse, so every rate lift runs it through PlayerShip.Cadence), where the
// spot opens and how wide it is (percent of the reload), the multiplier a perfect round carries,
// and the charge to full (seconds, Inverse). They are the class's own rows (ClassDef.Rows), so gear
// moves them. The gun's own code calls three doors and nothing else: Spent (it fired), Seat (its
// row's Elapsed), Take (the multiplier on the round it is firing). The owner's key comes in through
// Press (the stroke rule, PlayerShip.Stroke); the host's verdict is Judge. What it fires on a
// release is its row's Loose, handed the charge share (PlayerShip.ChargeLatch, the host).
// A NEW ROW MUST NOT add a field to the wire, or a case anywhere that names its gun.
//
// ONE KEY, ONE MEANING PER STROKE (§1.1): a stroke's meaning is fixed at its press by the chamber
// then -- reloading, it is the TIMING press (judged at once, the first of a reload only, and it
// never fires however long it is held); seated, it is the CHARGE (the trigger rises, and its
// release fires). The owner never raises the trigger for a timing stroke, so the host never has
// to guess which of the two a held key was.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReloadSpec { public string Id, Reload, SpotAt, Spot, Perfect, Charge; }

// Slot.N. Reloading: Empty (no press yet), Missed or Perfect (the one press is spent).
// Seated: Seated (plain) or Enhanced. Rides the host's report like every slot.
public enum Chamber { Empty = 0, Missed = 1, Perfect = 2, Seated = 3, Enhanced = 4 }
public enum Verdict { None, Perfect, Missed }

public static class ActiveReload
{
    public static readonly ReloadSpec Rail = new() { Id = "railgun", Reload = "rail_reload",
        SpotAt = "rail_spot_at", Spot = "rail_spot", Perfect = "rail_perfect", Charge = "rail_charge" };

    public const double FlashTime = 0.25;     // the white or grey flash on the owner's bar (§4.2)
    public const double GiveWay = 0.15;       // how long past a round trip the owner's view may disagree with the host's (§3.3)

    public static bool Reloading(in PlayerShip.Slot sl) => sl.N <= (int)Chamber.Perfect && sl.Left > 0;
    public static bool Seated(in PlayerShip.Slot sl) => sl.N >= (int)Chamber.Seated;
    public static double Progress(in PlayerShip.Slot sl) => sl.Own > 0 ? 1 - sl.Left / sl.Own : 1;
    // where the spot opens and closes, as shares of the reload
    public static (double open, double close) Spot(ShipStats st, ReloadSpec r)
        => (st[r.SpotAt] / 100, (st[r.SpotAt] + st[r.Spot]) / 100);

    // HOST: the shot left. The chamber is empty; the reload runs at the rate the ship has now, and
    // keeps that length whatever lifts land during it.
    public static void Spent(PlayerShip s, ReloadSpec r)
    { ref var sl = ref s.Sl(r.Id); sl.N = (int)Chamber.Empty; sl.Own = sl.Left = s.Cadence(r.Reload); }

    // Its row's ELAPSED (every peer, the host included: a phase, not a spend): the round seats,
    // enhanced if this reload's press was perfect. Idempotent; a fitted ship starts Seated.
    public static void Seat(PlayerShip s, ReloadSpec r)
    {
        ref var sl = ref s.Sl(r.Id);
        if (sl.N <= (int)Chamber.Perfect) sl.N = (int)(sl.N == (int)Chamber.Perfect ? Chamber.Enhanced : Chamber.Seated);
    }

    // HOST, at the shot: what the round is worth (x1 plain, the row's Perfect enhanced).
    public static double Take(PlayerShip s, ReloadSpec r)
        => s.Sl(r.Id).N == (int)Chamber.Enhanced ? s.Stats[r.Perfect] : 1.0;

    // THE RULE, pure: a claim (a share of the reload, on the owner's clock) against the host's own
    // share `at`, believed only within `give` of it (a non-finite claim is judged on the host's
    // clock), in the spot [open, close] or not.
    public static Verdict Judged(double at, double claim, double give, double open, double close)
    {
        double judged = double.IsFinite(claim) ? Math.Clamp(claim, at - give, at + give) : at;
        return judged >= open && judged <= close ? Verdict.Perfect : Verdict.Missed;
    }
    // how far the host believes an owner's clock, as a share: the leeway in seconds over this
    // reload's length, never more than a quarter of the spot. Pure.
    public static double Give(double leeway, double length, double open, double close)
        => length > 0 ? Math.Min(leeway / length, (close - open) / 4) : 0;

    // HOST: THE JUDGEMENT. One press a reload (the chamber still Empty); no reload, no press; a
    // wreck presses nothing. Leeway is seconds (Net.Leeway): 0 for the host's own ship.
    public static void Judge(PlayerShip s, ReloadSpec r, double claim, double leeway)
    {
        ref var sl = ref s.Sl(r.Id);
        if (!Net.Sim || !s.Alive || sl.N != (int)Chamber.Empty || sl.Left <= 0) return;
        var (open, close) = Spot(s.Stats, r);
        double at = Progress(sl);
        sl.N = (int)(Judged(at, claim, Give(leeway, sl.Own, open, close), open, close) == Verdict.Perfect ? Chamber.Perfect : Chamber.Missed);
    }

    // OWNER: a timing stroke went down. The first of a reload is judged on the owner's own clock at
    // once (the flash and the cue are the owner's alone), then the host's word follows on the slot:
    // the host player judges it here with no leeway, a guest asks (PlayerShip.RequestReloadPress).
    public static void Press(PlayerShip s, ReloadSpec r)
    {
        ref var v = ref s.ReloadView;
        if (!v.Running || v.Said != Verdict.None) return;
        double share = v.Length > 0 ? v.Since / v.Length : 1;
        var (open, close) = Spot(s.Stats, r);
        v.Said = share >= open && share <= close ? Verdict.Perfect : Verdict.Missed;
        v.Flash = FlashTime;
        Sfx.ByName(v.Said == Verdict.Perfect ? "rail_perfect" : "rail_miss", s.Position);
        if (Net.Sim) Judge(s, r, share, 0);
        else s.AskReloadPress(r.Id, (float)share);
    }

    // OWNER: its own charge stroke let go on a round its view held seated: the view starts a reload
    // on its own clock, at the length the host will give it (the host's replaces it when it comes).
    public static void Shot(PlayerShip s, ReloadSpec r)
    {
        ref var v = ref s.ReloadView;
        v.Running = true; v.Since = 0; v.Length = s.Cadence(r.Reload); v.Said = Verdict.None; v.Off = 0; v.Charge = 0;
    }

    // OWNER, every frame: its own clock runs, the flash fades, a charge stroke fills. The host's own
    // ship mirrors its slot exactly; a guest's view keeps its own clock (the host's report is a round
    // trip old) and gives way to the host's slot once they have disagreed for a round trip and GiveWay.
    public static void Step(PlayerShip s, ReloadSpec r, double dt)
    {
        ref var v = ref s.ReloadView;
        var sl = s.Sl(r.Id);
        v.Flash = Math.Max(0, v.Flash - dt);
        v.Charge = s.Trigger && !v.Running ? v.Charge + dt : 0;
        var host = sl.N == (int)Chamber.Perfect || sl.N == (int)Chamber.Enhanced ? Verdict.Perfect
                 : sl.N == (int)Chamber.Missed ? Verdict.Missed : Verdict.None;
        if (Net.Sim)
        {
            v.Running = Reloading(sl); v.Length = sl.Own; v.Since = Progress(sl) * sl.Own;
            if (v.Running) v.Said = host; else if (!Seated(sl)) v.Said = Verdict.None;
            return;
        }
        if (v.Running)
        {
            v.Since += dt;
            if (Reloading(sl) && sl.Own > 0) v.Length = sl.Own;                         // the host's length is the truth
            if (v.Since >= v.Length) v.Running = false;
        }
        bool differ = v.Running != Reloading(sl) || (v.Running && host != Verdict.None && host != v.Said);
        v.Off = differ ? v.Off + dt : 0;
        if (v.Off > Net.RoundTrip + GiveWay)
        {   // the host wins: its chamber, its clock and its verdict
            v.Running = Reloading(sl); v.Length = sl.Own; v.Since = Progress(sl) * sl.Own;
            if (host != Verdict.None && host != v.Said) { v.Said = host; v.Flash = FlashTime; }
            v.Off = 0;
        }
        if (!v.Running && Seated(sl)) v.Said = sl.N == (int)Chamber.Enhanced ? Verdict.Perfect : Verdict.None;
    }

    // THE GUN'S SLOT ON THE BAR (§4.3), in step with the owner's view: RELOAD / PERFECT while it
    // reloads, CHARGE while a stroke fills a seated round, then READY or the enhanced round's x.
    public static SlotState Slot(PlayerShip s, ReloadSpec r)
    {
        var v = s.ReloadView;
        if (v.Running)
            return v.Said == Verdict.Perfect ? new SlotState { Line = "PERFECT", Lit = true }
                 : new SlotState { Line = $"RELOAD {Math.Max(0, v.Length - v.Since):0.0}s",
                                   Busy = (float)(v.Length > 0 ? Math.Clamp(1 - v.Since / v.Length, 0, 1) : 0) };
        if (v.Charge > 0)
        {
            double full = Math.Max(1e-6, s.Cadence(r.Charge));
            return new SlotState { Line = $"CHARGE {Math.Max(0, full - v.Charge):0.0}s", Lit = true, Busy = (float)Math.Max(0, 1 - v.Charge / full) };
        }
        return s.Sl(r.Id).N == (int)Chamber.Enhanced ? new SlotState { Line = $"x{s.Stats[r.Perfect]:0.0#} ROUND", Lit = true }
                                                     : new SlotState { Line = "READY" };
    }

    // EVERY PEER (§3.6): a white glint at the muzzle while an enhanced round is seated -- read from
    // the slot, which rides the host's report, so allies see a 180 line coming. The owner's too.
    public static void Draw(PlayerShip s)
    {
        if (!s.Alive) return;
        foreach (var def in Abilities.For(s.Class))
        {
            if (def.Reload is not { } r || s.Sl(r.Id).N != (int)Chamber.Enhanced) continue;
            var tip = new Vector2(0, -s.MyArt.Length * 0.5f);
            float k = 0.8f + 0.2f * Mathf.Sin(Time.GetTicksMsec() * 0.012f);
            s.DrawCircle(tip, 10f * k, new Color(1f, 1f, 1f, 0.22f));
            s.DrawCircle(tip, 3.5f, new Color(1f, 1f, 1f, 0.95f));
        }
    }

    // OWNER ONLY, never on the wire: the owner's own clock since its own shot (Since of Length),
    // what its press was judged (Said), the flash left, how long it has disagreed with the host, and
    // how long its charge stroke has been held on a seated round.
    public struct View { public double Since, Length, Flash, Off, Charge; public Verdict Said; public bool Running; }
}
