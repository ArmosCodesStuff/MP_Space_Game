using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// YARD — the idle economy of the hub, as one node (Hub/Yard, the same path on
// every peer so its RPCs arrive). It owns:
//
//   stock        one figure per resource id (Gathering.Resources), credits, and what has
//                been delivered -- never a field per resource
//   upgrades     their levels, purchases (a guest asks, the host decides)
//   the fleet    miners and salvagers, as many as the +1 upgrades allow
//   the arms     the base's five service arms: each takes one miner or salvager at
//                a time, loading through its open face (north; the top arm, west),
//                marked by gently flashing blue hologram bars
//   the queue    ships that come home to full arms wait in line, first come first
//                served, and take the next arm that frees up
//   the hauler   and its dispatch
//
// HOST-OWNED: the host simulates all of it and sends totals and levels once a
// second, ship and hauler state ten times a second. A guest's own yard is parked
// while it visits (banked first, so nothing it carries is lost) and restored after.
// ─────────────────────────────────────────────────────────────────────────────
public partial class Yard : Node2D
{
    public Hub Hub;
    public double Credits;
    public readonly List<Gatherer> Gatherers = new();
    public Hauler Hauler;

    // THE STOCK, BY RESOURCE ID. `Ore` and `Salvage` were two fields, and every place that read
    // them -- the deposit, the bank, the trip, the refit, the totals packet and the save -- had
    // to name both; a third gatherer's resource was a third field in six places. A resource is
    // a row's Resource now, and this dictionary is keyed by it. Delivered is the running total
    // that was TotalMined / TotalSalvaged.
    private readonly Dictionary<string, double> _stock = new(), _delivered = new();
    public double Stock(string res) => _stock.GetValueOrDefault(res);
    public void SetStock(string res, double v) => _stock[res] = v;
    public double Delivered(string res) => _delivered.GetValueOrDefault(res);
    // everything the base is holding, across every resource (what the hauler can still load)
    public double StockHeld { get { double t = 0; foreach (var r in Gathering.Resources) t += Stock(r); return t; } }

    private readonly Dictionary<string, int> _levels = new();
    // A guest's OWN base, set aside while it visits someone else's. Static: the scene is
    // reloaded when the party goes to the arena and back, and an instance field went
    // with it -- a guest who followed the party lost its own base.
    private static readonly Dictionary<string, double> _ownStock = new();
    private static double _ownCredits;
    private static readonly Dictionary<string, int> _ownLevels = new();
    private static readonly Dictionary<string, double> _ownInvested = new();
    private static bool _parked;
    private double _totalsCd, _stateCd, _t, _saveCd;
    // How often the base is written to disk while it ticks over. Mining income arrives every
    // second and a save a second would be absurd; a minute of an idle economy is a small thing
    // to lose to a power cut, and every DELIBERATE change -- a purchase, leaving -- saves at once.
    private const double AutoSaveEvery = 30;

    // ── the service arms ─────────────────────────────────────────────────────
    // Pad centres measured from base_station.png (pixels from its centre x 0.6).
    public class Arm
    {
        public Vector2 Pad;       // pad centre, relative to the base
        public Vector2 Open;      // the open face: ships load through this side
        public float Reach;       // pad centre to its open face
        public float Face;        // length of the open face
        public Gatherer Occupant;
    }

    public readonly Arm[] Arms =
    {
        // Measured from the art (each pad's rotated outline): its centre, and its
        // north-facing edge -- the outward normal (Open), the distance to it (Reach) and
        // its length (Face). The four diagonal pads are turned 30 degrees, so their
        // north faces tilt with them; every bay, the top one included, loads from its north face.
        new() { Pad = new(0f, -195.9f),      Open = new(0f, -1f),          Reach = 22.2f, Face = 54.6f },
        new() { Pad = new(-169.5f, -97.9f),  Open = new(0.501f, -0.865f),  Reach = 28.1f, Face = 45.6f },
        new() { Pad = new(169.5f, -97.9f),   Open = new(-0.501f, -0.865f), Reach = 28.1f, Face = 45.6f },
        new() { Pad = new(-169.5f, 97.9f),   Open = new(-0.501f, -0.865f), Reach = 28.1f, Face = 45.6f },
        new() { Pad = new(169.5f, 97.9f),    Open = new(0.501f, -0.865f),  Reach = 28.1f, Face = 45.6f },
    };

    private readonly List<Gatherer> _queue = new();

    public override void _Ready()
    {
        // a pilot coming home offline (the host left, or the mission ended) gets back
        // the base it set aside
        if (_parked && !Net.IsOnline) CallDeferred(nameof(OnSessionChanged), false);
        ZIndex = 3;                                        // above the base
        Hauler = new Hauler { Yard = this, Name = "Hauler" };
        AddChild(Hauler);
        // THREE WAYS A BASE ARRIVES, and only one of them is the save file. A trip snapshot wins
        // (the party is coming home from the arena and the base never went anywhere); a parked
        // base wins next (a guest getting its own back); otherwise this is the pilot arriving in
        // their own world, and the file is what they left behind.
        if (_trip == null && !_parked) LoadFromCharacter();
        // The trip's levels BEFORE the fleet is built: the other way round, the first ships came
        // off the pad at level 0 of everything.
        ReturnFromTrip();
        SyncFleet();
    }

    // ── the base on disk ─────────────────────────────────────────────────────
    // Only when this Yard really is MINE. A guest standing in someone else's base sees their ore
    // and their upgrades; writing those to its own character would hand it a base it never built.
    // (Not "and the sector is home": a Yard only exists at home, and leaving for the arena sets the
    // sector BEFORE this Yard's own exit -- where that clause made the one save on the way out
    // write nothing.)
    public bool IsMyOwnBase => Net.Sim && !_parked;

    // THE BASE OWNER'S BOSS RECORD: what a gated upgrade asks for. The host is the owner of the
    // base everyone is standing in; a guest is told it with the totals.
    private int _ownerBoss;
    public int OwnerBoss => Net.IsHost ? Missions.HighestBeaten(Missions.Bounty) : _ownerBoss;
    public bool AutoSell => Level("hauler_autosell") >= 1;
    public double RunSafe => Value("hauler_evasion") / 100.0;       // a lone run's chance of getting through

    public void StoreToCharacter()
    {
        if (_parked)
        {
            // A GUEST STANDING IN SOMEONE ELSE'S BASE still has one of its own, set aside in the
            // _own fields. Leaving from here -- the host drops, the window closes, the session
            // ends -- has to write THAT, not the base it happens to be looking at. Writing the
            // visible one would hand the guest a base it never built; writing nothing would lose
            // the one it did.
            WriteStock(_ownStock); Character.BaseCredits = _ownCredits;
            Character.BaseLevels.Clear();   foreach (var kv in _ownLevels)   Character.BaseLevels[kv.Key] = kv.Value;
            Character.BaseInvested.Clear(); foreach (var kv in _ownInvested) Character.BaseInvested[kv.Key] = kv.Value;
            return;
        }
        if (!IsMyOwnBase) return;
        var (stock, credits) = Banked();
        WriteStock(stock); Character.BaseCredits = credits;
        Character.BaseLevels.Clear();   foreach (var kv in _levels)   Character.BaseLevels[kv.Key] = kv.Value;
        Character.BaseInvested.Clear(); foreach (var kv in _invested) Character.BaseInvested[kv.Key] = kv.Value;
    }

    // The character's copy of the stock: one entry per resource this build has, always, so what
    // reaches disk does not depend on which piles happen to be non-empty.
    private static void WriteStock(Dictionary<string, double> from)
    {
        Character.BaseStock.Clear();
        foreach (var r in Gathering.Resources) Character.BaseStock[r] = from.GetValueOrDefault(r);
    }

    public void SaveBase()
    {
        StoreToCharacter();
        if (_parked || IsMyOwnBase) Character.Save();
    }

    private void LoadFromCharacter()
    {
        _stock.Clear();
        foreach (var r in Gathering.Resources) SetStock(r, Character.BaseStock.GetValueOrDefault(r));
        Credits = Character.BaseCredits;
        _levels.Clear();   foreach (var kv in Character.BaseLevels)   _levels[kv.Key] = kv.Value;
        _invested.Clear(); foreach (var kv in Character.BaseInvested) _invested[kv.Key] = kv.Value;
    }

    // Leaving for any reason -- the menu, a scene change, the window closing -- writes the base.
    public override void _ExitTree() => SaveBase();

    // ── away on a mission ────────────────────────────────────────────────────
    // The base is not simulated while the party is in the arena. It is saved on the
    // way out; on return it gets back what it had, any credits earned out there, and
    // 1/20 of what its fleet would have gathered in the time away.
    public const double AwayShare = 1.0 / 20.0;
    // everything spent on upgrades so far, per category (MINERS, SALVAGERS, HAULER)
    private readonly Dictionary<string, double> _invested = new();

    public double Invested(string category) => _invested.TryGetValue(category, out var v) ? v : 0;
    public double RebuildCost(string category) => Math.Round(Invested(category) * Economy.RebuildShare);

    private sealed record Trip(Dictionary<string, double> Stock, double Credits, Dictionary<string, int> Levels,
                               Dictionary<string, double> Invested, Dictionary<string, double> PerSec);
    public static double TripClock;                   // game seconds in the arena (Hub counts them)
    private static Trip _trip;
    public static double TripCredits;                 // earned while away (a bounty), paid on return
    public static double LastAway;                    // how long the last trip was, in game seconds
    // ...and what it credited, per resource. Two named statics for a two-resource fleet was the
    // same trap as two named fields.
    private static readonly Dictionary<string, double> _lastAwayGain = new();
    public static double LastAwayGained(string res) => _lastAwayGain.GetValueOrDefault(res);
    public static double TripStartCredits;            // the credits set aside when the last trip began
    // A bounty share. On disk AT ONCE (the caller saves next): quitting between the kill and home
    // (the victory window) used to keep the EXP and lose the credits. The live base still
    // gets it on the way home -- a guest's into its own, set-aside base; the host's with the
    // trip -- and the next save writes that live figure over this one, so nothing is counted twice.
    public static void AddGuestShare(double credits) { _ownCredits += credits; Character.BaseCredits += credits; }
    public static void AddHostShare(double credits) { TripCredits += credits; Character.BaseCredits += credits; }

    // Reaching the main menu ends the session. These statics outlive the scene ON PURPOSE -- a
    // sector change and a visit both tear the Yard down and rebuild it -- but nothing cleared them
    // when the SESSION ended, and `Yard._Ready` applies them unconditionally. So quitting from the
    // arena left `_trip` behind and the next session's economy was overwritten by that snapshot,
    // and quitting while visiting left `_parked` set so the next session restored the base from
    // the one before -- another character's, even.
    public static void EndSession()
    {
        _trip = null;
        TripCredits = TripClock = TripStartCredits = 0;
        LastAway = 0; _lastAwayGain.Clear();
        _parked = false;
        _ownCredits = 0; _ownStock.Clear();
        _ownLevels.Clear(); _ownInvested.Clear();
    }

    public void SaveForTrip()
    {
        Bank();                                                   // the loads come home first: nothing flies off with the party
        var stock = new Dictionary<string, double>(); var perSec = new Dictionary<string, double>();
        foreach (var r in Gathering.Resources) stock[r] = Stock(r);
        foreach (GatherKind k in Enum.GetValues(typeof(GatherKind))) perSec[Gathering.Of(k).Resource] = FleetRate(k);
        _trip = new Trip(stock, Credits, new Dictionary<string, int>(_levels), new Dictionary<string, double>(_invested), perSec);
        TripCredits = 0; TripClock = 0; TripStartCredits = Credits;
    }

    private void ReturnFromTrip()
    {
        if (_trip == null) return;
        var t = _trip; _trip = null;
        LastAway = TripClock;
        _lastAwayGain.Clear();
        foreach (var r in Gathering.Resources)
        {
            double gain = t.PerSec.GetValueOrDefault(r) * LastAway * AwayShare;
            _lastAwayGain[r] = gain;
            SetStock(r, t.Stock.GetValueOrDefault(r) + gain);
        }
        Credits = t.Credits + TripCredits; TripCredits = 0;
        _levels.Clear(); foreach (var kv in t.Levels) _levels[kv.Key] = kv.Value;
        _invested.Clear(); foreach (var kv in t.Invested) _invested[kv.Key] = kv.Value;
        SyncFleet();
    }

    // A fleet's steady income: each ship fills its hold, flies its real route out and
    // back, and unloads. (An estimate: good enough for a 1/20 share.)
    public double FleetRate(GatherKind k)
    {
        double rate = 0;
        foreach (var g in Gatherers)
            if (g.Def == Gathering.Of(k))
            {
                double trip = WorkSpot(g).spot.DistanceTo(Hub.BasePos);           // its real route, one way
                rate += g.Hold / (g.Hold / g.Rate + 2 * trip / g.Speed + g.Hold / Economy.UnloadRate);
            }
        return rate;
    }

    // ── upgrades ─────────────────────────────────────────────────────────────
    public int Level(string id) => _levels.TryGetValue(id, out var l) ? l : 0;
    public double Value(string id) => Economy.Value(Economy.ById(id), Level(id));

    public void BuyUpgrade(string id)
    {
        if (Net.IsHost) TryBuy(id);
        else Net.AskHost(this, nameof(RequestBuy), id);
    }

    // A player in the session, not just any peer: this spends the HOST's credits.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestBuy(string id) { if (Net.FromPlayer(this, out _) && Economy.ById(id) is { OwnerOnly: false }) TryBuy(id); }

    public bool TryBuy(string id)
    {
        var u = Economy.ById(id);
        if (u == null || !Net.Sim) return false;
        int lv = Level(id);
        double cost = Economy.Cost(u, lv);
        if (Economy.Maxed(u, lv) || Credits < cost || OwnerBoss < u.NeedsBoss) return false;
        Credits -= cost; _levels[id] = lv + 1;
        _invested[u.Tab] = Invested(u.Tab) + cost;                // what a rebuild in this category is 10% of
        SaveBase();                                               // a purchase is deliberate: keep it now
        // Tell the guests NOW, not at the next 1 s report: a guest's BUY button only changes when
        // the new level arrives, and over the internet a second click inside that second bought
        // the NEXT level too.
        _totalsCd = 0;
        // a hull row: the living ships of THAT gatherer gain the level at once, and what a level
        // is worth is the row's own step, not a constant read twice
        foreach (GatherKind k in Enum.GetValues(typeof(GatherKind)))
            if (Gathering.Of(k).HullId == id)
                foreach (var g in Gatherers)
                    if (g.State != Gatherer.St.Destroyed && g.Kind == k)
                        g.Hull += Economy.Value(u, lv + 1) - Economy.Value(u, lv);
        SyncFleet();
        return true;
    }

    // ── the fleet: as many miners and salvagers as the levels say ────────────
    // Built in a fixed order (miners, then salvagers, by number) on every peer, so the
    // host's state arrays line up with each guest's list.
    private void SyncFleet()
    {
        foreach (GatherKind k in Enum.GetValues(typeof(GatherKind)))
        {
            var d = Gathering.Of(k);
            int want = (int)Value(d.CountId);
            var have = Gatherers.Where(g => g.Kind == k).OrderBy(g => g.Index).ToList();
            for (int i = have.Count; i < want; i++)
            {
                var g = new Gatherer { Yard = this, Kind = k, Index = i, Name = $"{d.Name}{i + 1}",
                                       Position = Hub.BasePos + d.Muster + d.MusterStep * i };
                AddChild(g); Gatherers.Add(g);
            }
            foreach (var g in have.Where(g => g.Index >= want)) { Release(g); Gatherers.Remove(g); g.QueueFree(); }
        }
        Gatherers.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Index.CompareTo(b.Index));
    }

    // Where a ship works, by its row's Site. ROCKS take different asteroids, nearest the base
    // first; a WRECK's spread over the face that looks toward the base. (WreckSpread and
    // WreckEdge are measurements of one piece of art, so they live here beside the arm that
    // flies it, not on a gatherer's row.)
    private static readonly float[] WreckSpread = { 0f, 0.35f, -0.35f, 0.7f, -0.7f, 1.05f };
    // Where the hull really begins along each of those approaches, measured from
    // behemoth_wreck.png at its 0.34 scale (the outline is ragged, so no single radius
    // works: most approaches meet solid hull ~96 u from the centre, one not until 343).
    private static readonly float[] WreckEdge = { 96f, 96f, 101f, 95f, 343f, 112f };
    private List<Vector2> _rocksNearFirst;
    public (Vector2 spot, Vector2 target) WorkSpot(Gatherer g)
    {
        if (g.Def.Site == GatherSite.Rocks)
        {
            // the belt's rocks, nearest the base first: sorted once, not every frame for every miner
            var rocks = _rocksNearFirst ??= Hub.Rocks.OrderBy(r => r.Position.DistanceTo(Hub.BasePos)).Select(r => r.Position).ToList();
            var rock = rocks.Count > 0 ? rocks[g.Index % rocks.Count] : Hub.SunPos;
            return (rock + (Hub.BasePos - rock).Normalized() * 70f, rock);
        }
        var d = (Hub.BasePos - Hub.WreckPos).Normalized().Rotated(WreckSpread[g.Index % WreckSpread.Length]);
        // hold 140 u off the measured hull and cut 15 u into it, so the beam always lands
        float edge = WreckEdge[g.Index % WreckEdge.Length];
        return (Hub.WreckPos + d * (edge + 140f), Hub.WreckPos + d * (edge - 15f));
    }

    // ── arms and the queue ───────────────────────────────────────────────────
    // A full ship asks for an arm. It gets the free one nearest it, or joins the end
    // of the queue; when an arm frees up, the head of the queue gets it.
    public int RequestArm(Gatherer g)
    {
        int mine = ArmOf(g);
        if (mine >= 0) return mine;
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < Arms.Length; i++)
        {
            if (Arms[i].Occupant != null) continue;
            float d = g.Position.DistanceTo(Hub.BasePos + Arms[i].Pad);
            if (d < bd) { bd = d; best = i; }
        }
        if (best >= 0) { SetArm(g, best); _queue.Remove(g); return best; }
        if (!_queue.Contains(g)) _queue.Add(g);
        return -1;
    }

    // WHICH ARM HOLDS WHICH SHIP, on every peer. The one place a ship moves between arms: the
    // host decides it (RequestArm, Release) and sends the index with that ship's state; a guest
    // sets it here from the wire. -1 is "in no arm". Before this, Occupant was written only under
    // Net.Sim, so on a guest the hologram bars never brightened for a ship it could plainly see
    // standing in one -- and _Draw reads Occupant, with no Net.Sim branch of its own.
    public void SetArm(Gatherer g, int arm)
    {
        for (int i = 0; i < Arms.Length; i++) if (Arms[i].Occupant == g) Arms[i].Occupant = null;
        if (arm >= 0 && arm < Arms.Length) Arms[arm].Occupant = g;
    }

    public int ArmOf(Gatherer g) { for (int i = 0; i < Arms.Length; i++) if (Arms[i].Occupant == g) return i; return -1; }
    public int QueueIndex(Gatherer g) => _queue.IndexOf(g);
    public int QueueLength => _queue.Count;
    public Vector2 QueueSpot(int i) => Hub.BasePos + new Vector2(70f + 46f * i, -330f);   // a line north-east of the top arm

    // where a ship holds to unload: just outside the arm's open face, nose in
    public Vector2 UnloadSpot(int arm) { var a = Arms[arm]; return Hub.BasePos + a.Pad + a.Open * (a.Reach + Gatherer.Length * 0.5f + 4f); }

    public void Release(Gatherer g)
    {
        _queue.Remove(g);
        int i = ArmOf(g);
        if (i < 0) return;
        SetArm(g, -1);
        if (_queue.Count > 0) { SetArm(_queue[0], i); _queue.RemoveAt(0); }
    }

    // ── stock ────────────────────────────────────────────────────────────────
    // A DELIVERY, into the resource its row names. One line for any number of resources: the
    // if/else into two named fields is what a third gatherer used to have to edit.
    public void Deposit(string res, double amount)
    {
        SetStock(res, Stock(res) + amount);
        _delivered[res] = Delivered(res) + amount;
    }

    // The hauler takes the DEEPEST pile first, then evenly: every pile is left at the same depth.
    // With two that is exactly "the gap, then both evenly"; with three it needs no new arm.
    public double TakeStock(double want)
    {
        want = Math.Min(want, StockHeld);
        if (want <= 0) return 0;
        var deep = Gathering.Resources.Select(Stock).OrderByDescending(v => v).ToArray();
        double level = 0;
        for (int k = 1; k <= deep.Length; k++)
        {   // can the top k piles alone give `want`, cut no deeper than the (k+1)-th?
            double floor = k < deep.Length ? deep[k] : 0, takeable = 0, sum = 0;
            for (int i = 0; i < k; i++) { takeable += deep[i] - floor; sum += deep[i]; }
            if (takeable >= want) { level = (sum - want) / k; break; }
        }
        foreach (var r in Gathering.Resources) SetStock(r, Math.Min(Stock(r), level));
        return want;
    }

    // ── the hauler ───────────────────────────────────────────────────────────
    public int Pods => (int)Value("hauler_pods");
    public double PodSize => Value("pod_size");
    public double Capacity => Pods * PodSize;

    // DISPATCH and ESCORT are the base owner's alone: a lone run can lose the load, and an escort
    // is a mission for the pilot whose hauler it is. A guest has no path to either.
    public void RequestDispatch(bool escorted) { if (IsMyOwnBase) Hauler.Dispatch(escorted); }

    public void PortalFlash()
    {
        Hub.Portal.Flash();
        if (Net.IsHost && Net.IsOnline) Hub.RpcHome(this, nameof(NetPortalFlash));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetPortalFlash() => Hub.Portal.Flash();

    // ── REFIT's price: 10% of the stock you own ──────────────────────────────
    // The world's if you are its host (or offline); a guest's own, parked totals. Asked one
    // resource at a time, so the window lists whatever the table holds (BasePanel).
    public double ResetCost(string res) => (_parked ? _ownStock.GetValueOrDefault(res) : Stock(res)) * 0.1;
    public double ResetCostCredits => (_parked ? _ownCredits : Credits) * 0.1;

    public void ChargeReset()
    {
        foreach (var r in Gathering.Resources)
            if (_parked) _ownStock[r] = _ownStock.GetValueOrDefault(r) * 0.9;
            else SetStock(r, Stock(r) * 0.9);
        if (_parked) _ownCredits *= 0.9; else Credits *= 0.9;
    }

    // ── visiting: park your own yard, restore it after ───────────────────────
    public void OnSessionChanged(bool guest)
    {
        if (guest && !_parked)
        {
            Bank();                                        // nothing your ships carry is lost
            _ownStock.Clear(); foreach (var r in Gathering.Resources) _ownStock[r] = Stock(r);
            _ownCredits = Credits;
            _ownLevels.Clear(); foreach (var kv in _levels) _ownLevels[kv.Key] = kv.Value;
            _ownInvested.Clear(); foreach (var kv in _invested) _ownInvested[kv.Key] = kv.Value;
            _parked = true;
            ResetShips();                                  // the host's packets drive them now
        }
        else if (!guest && _parked)
        {
            foreach (var r in Gathering.Resources) SetStock(r, _ownStock.GetValueOrDefault(r));
            Credits = _ownCredits;
            _levels.Clear(); foreach (var kv in _ownLevels) _levels[kv.Key] = kv.Value;
            _invested.Clear(); foreach (var kv in _ownInvested) _invested[kv.Key] = kv.Value;
            _parked = false;
            ResetShips();                                  // not the host's last state
        }
        SyncFleet();
    }

    // THE STOCK AS IF EVERY LOAD WERE HOME: gatherer cargo back to ore and salvage; the hauler's
    // back to stock too if it is still here (an escort included), or paid its outcome if it is
    // already through the portal. Parking, a trip and a save all count the loads this way: with
    // AUTO-SELL locked, a full hauler waiting on its pad is the usual state, and a trip or a quit
    // used to throw its load away.
    private (Dictionary<string, double> stock, double credits) Banked()
    {
        var stock = new Dictionary<string, double>();
        foreach (var r in Gathering.Resources) stock[r] = Stock(r);
        double credits = Credits;
        foreach (var g in Gatherers) stock[g.Def.Resource] += g.Cargo;
        if (Hauler.Cargo > 0)
        {
            if (Hauler.State == Hauler.St.Away) credits += Hauler.Payout;
            // its load came off the piles evenly (TakeStock), so it goes back the same way
            else foreach (var r in Gathering.Resources) stock[r] += Hauler.Cargo / Gathering.Resources.Length;
        }
        return (stock, credits);
    }
    private void Bank()
    {
        var (stock, credits) = Banked();
        foreach (var r in Gathering.Resources) SetStock(r, stock.GetValueOrDefault(r));
        Credits = credits;
        foreach (var g in Gatherers) g.Cargo = 0;
        Hauler.Cargo = 0;
    }

    private void ResetShips()
    {
        _queue.Clear();
        foreach (var a in Arms) a.Occupant = null;
        foreach (var g in Gatherers) g.ResetToWork();
        Hauler.ResetToPad();
    }

    // ── frame, and the host's reports ────────────────────────────────────────
    public override void _Process(double delta)
    {
        _t += delta;
        QueueRedraw();
        // The autosave runs BEFORE the online-only return below: an offline pilot is exactly the
        // one whose base only exists in their own file, and skipping them would have saved the
        // base for every player except the single-player one.
        _saveCd -= delta;
        if (_saveCd <= 0) { _saveCd = AutoSaveEvery; SaveBase(); }
        if (!Net.Sim || !Net.IsOnline) return;
        _totalsCd -= delta;
        if (_totalsCd <= 0)
        {
            _totalsCd = 1.0;
            var lv = Economy.All.Select(u => Level(u.Id)).ToArray();
            Hub.RpcHome(this, nameof(NetTotals), Gathering.Resources.Select(Stock).ToArray(), Credits, lv,
                        Economy.Tabs.Select(Invested).ToArray(), Missions.HighestBeaten(Missions.Bounty));
        }
        _stateCd -= delta;
        if (_stateCd <= 0) { _stateCd = 0.1; SendState(); }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetTotals(double[] stock, double credits, int[] levels, double[] invested, int ownerBoss)
    {
        _ownerBoss = ownerBoss;
        for (int i = 0; i < Math.Min(invested.Length, Economy.Tabs.Length); i++) _invested[Economy.Tabs[i]] = invested[i];
        // by position, as the levels are: the resources are a table, in table order
        for (int i = 0; i < Math.Min(stock.Length, Gathering.Resources.Length); i++) SetStock(Gathering.Resources[i], stock[i]);
        Credits = credits;
        for (int i = 0; i < Math.Min(levels.Length, Economy.All.Length); i++) _levels[Economy.All[i].Id] = levels[i];
        SyncFleet();
    }

    private void SendState()
    {
        int n = Gatherers.Count;
        // ga: the ARM each ship is in, -1 for none. The eighth per-gatherer array, because the
        // arms were host-only state that the drawing reads on every peer.
        var gp = new Vector2[n]; var gr = new float[n]; var gs = new int[n]; var gc = new float[n]; var gb = new Vector2[n]; var gh = new float[n]; var gw = new float[n]; var ga = new int[n];
        for (int i = 0; i < n; i++)
        {
            var g = Gatherers[i];
            (gp[i], gr[i], gs[i], gc[i], gb[i], gh[i], gw[i], ga[i]) = (g.Position, g.Rotation, (int)g.State, (float)g.Cargo, g.BeamTo, (float)g.Hull, g.NetRebuild, ArmOf(g));
        }
        var h = Hauler;
        Hub.RpcHome(this, nameof(NetState), gp, gr, gs, gc, gb, gh, gw, ga, h.Position, h.Rotation, (int)h.State, (float)h.T, (float)h.Cargo, (float)h.LastSale, (float)h.Hull, h.NetRebuild, h.NetFlags, (float)h.StopLeft);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(Vector2[] gp, float[] gr, int[] gs, float[] gc, Vector2[] gb, float[] gh, float[] gw, int[] ga,
                          Vector2 hp, float hr, int hs, float ht, float hc, float sale, float hh, float hw, int hf, float hstop)
    {
        // the fleet follows the levels (1 s); until they agree, skip the ships this once
        int n = Math.Min(gp.Length, Math.Min(gr.Length, Math.Min(gs.Length, Math.Min(gc.Length, Math.Min(gb.Length, Math.Min(gh.Length, Math.Min(gw.Length, ga.Length)))))));
        if (n == Gatherers.Count)
            for (int i = 0; i < n; i++) { Gatherers[i].SetNet(gp[i], gr[i], gs[i], gc[i], gb[i], gh[i], gw[i]); SetArm(Gatherers[i], ga[i]); }
        Hauler.SetNet(hp, hr, hs, ht, hc, sale, hh, hw, hf, hstop);
    }

    // The whole fleet, for whatever treats a miner, a salvager and the hauler alike.
    public IEnumerable<UtilityShip> Fleet => Gatherers.Cast<UtilityShip>().Append(Hauler);

    // ── the hologram docking bars: two bars across each arm's open face ──────
    public override void _Draw()
    {
        // a damaged ship shows its hull: a small bar under it, green to red
        foreach (var g in Gatherers) HullBar(g.Position + new Vector2(-15, 26), 30, g.Hull, g.MaxHull, g.InReach);
        if (Hauler != null) HullBar(Hauler.Position + new Vector2(-40, 50), 80, Hauler.Hull, Hauler.MaxHull, Hauler.InReach);
        // an escort's route, ahead of it: the way it will fly, on every peer
        if (Hauler is { State: Hauler.St.Escorting } eh)
        {
            var pts = new List<Vector2> { ToLocal(eh.Position) };
            pts.AddRange(Hub.EscortRoute.Skip(eh.Leg).Select(ToLocal));
            DrawPolyline(pts.ToArray(), new Color(0.45f, 0.8f, 1f, 0.35f), 2f);
        }
        for (int i = 0; i < Arms.Length; i++)
        {
            var a = Arms[i];
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)_t * 2.2f + i * 1.3f);   // a gentle flash
            float alpha = (a.Occupant != null ? 0.55f : 0.30f) + 0.30f * pulse;
            var across = new Vector2(-a.Open.Y, a.Open.X) * (a.Face * 0.5f);
            var col = new Color(0.35f, 0.75f, 1f, alpha);
            foreach (float off in new[] { 3f, 9f })
            {
                var c = Hub.BasePos + a.Pad + a.Open * (a.Reach + off);
                DrawLine(c - across, c + across, col, 2f);
            }
            var o0 = Hub.BasePos + a.Pad + a.Open * (a.Reach + 1f);
            DrawLine(o0 - across, o0 - across + a.Open * 12f, col, 1.5f);            // the posts at each end
            DrawLine(o0 + across, o0 + across + a.Open * 12f, col, 1.5f);
        }
    }


    private void HullBar(Vector2 world, float w, double hull, double max, bool shown)
    {
        if (!shown || hull >= max) return;
        var at = ToLocal(world); float k = (float)Math.Clamp(hull / max, 0, 1);
        DrawRect(new Rect2(at, new Vector2(w, 3.5f)), new Color(0.1f, 0.1f, 0.12f, 0.85f));
        DrawRect(new Rect2(at, new Vector2(w * k, 3.5f)), new Color(1f - k, 0.35f + 0.55f * k, 0.3f));
    }
}
