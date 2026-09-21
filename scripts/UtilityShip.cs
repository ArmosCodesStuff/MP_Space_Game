using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// THE BASE'S FLEET: the miners, the salvagers and the hauler. What they share -- a hull that
// raiders can take, a pin, the loss, and the rebuild 30 s later for 10% of what that category's
// upgrades have cost -- was written out once in each, and every raider-facing switch named both.
//
// Host-owned; guests are told hull, state and the rebuild (Yard.NetState). A guest now also sees
// what used to happen only on the host: the burst when one is lost and when it comes back, and the
// rebuild counting down (the BASE menu read "rebuilt in 0 s" for the whole thirty).
// ─────────────────────────────────────────────────────────────────────────────
public abstract partial class UtilityShip : Node2D, IRaidTarget
{
    public Yard Yard;
    public double Cargo, Hull, RebuildIn;
    public bool WaitingForCredits;
    protected double PinT;
    public bool Pinned => PinT > 0;
    public void PinFor(double s) { if (Net.Sim) PinT = Math.Max(PinT, s); }

    public abstract double MaxHull { get; }
    public abstract string Category { get; }        // the BASE tab its upgrades (and its rebuild price) are under
    public abstract string Label { get; }           // "Miner 2", "Hauler"
    public abstract bool InReach { get; }           // out in the field, where a raider can get at it
    public abstract bool Lost { get; }              // destroyed: waiting to be rebuilt
    public abstract (float halfLength, float halfWidth) Extent { get; }
    protected abstract float LostBlast { get; }
    protected abstract float RebuiltBlast { get; }
    protected abstract void OnLost();               // off the field at once: its state, and its arm or pad

    public void Hit(double d, Vector2 from, string source) => TakeDamage(d);
    public void TakeDamage(double d)
    {
        if (!Net.Sim || !InReach) return;
        Hull -= d;
        if (Hull > 0) return;
        Hull = 0; Cargo = 0;
        OnLost();
        RebuildIn = Economy.RebuildDelay; WaitingForCredits = false;
        Burst(rebuilt: false);
    }

    // The host's rebuild clock. True: paid for, rebuild it now (the caller puts it back to work).
    protected bool TickRebuild(float dt)
    {
        RebuildIn = Math.Max(0, RebuildIn - dt);
        if (RebuildIn > 0) return false;
        double cost = Yard.RebuildCost(Category);
        if (Yard.Credits < cost) { WaitingForCredits = true; return false; }
        Yard.Credits -= cost; WaitingForCredits = false;
        return true;
    }

    protected void Burst(bool rebuilt) =>
        GetParent().AddChild(new Explosion { Position = Position, Radius = rebuilt ? RebuiltBlast : LostBlast,
                                             Tint = rebuilt ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.7f, 0.3f) });

    // The rebuild as the host reports it: seconds to go, or -1 while it waits for the credits.
    public float NetRebuild => WaitingForCredits ? -1f : (float)RebuildIn;
    // A guest's side of it: the host's word on hull and rebuild -- and when a report shows the
    // ship lost, or back again, the burst it would have seen on the host. `wasLost`: as it stood
    // before this report's state was applied.
    protected void FromHost(float hull, float rebuild, bool wasLost)
    {
        Hull = hull; WaitingForCredits = rebuild < 0; RebuildIn = Math.Max(0, rebuild);
        if (wasLost != Lost) Burst(rebuilt: wasLost);
    }
    // ...and between reports the clock runs down here, as it does on the host.
    protected void GuestClock(float dt)
    {
        if (Lost && !WaitingForCredits) RebuildIn = Math.Max(0, RebuildIn - dt);
    }
}
