using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// YARD — the idle economy of the hub, as one node (Hub/Yard, the same path on
// every peer so its RPCs arrive). It owns:
//
//   stock        ore, salvage, credits, and what has been delivered
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
    public double Ore, Salvage, Credits;
    public double TotalMined, TotalSalvaged;              // delivered, host-side
    public readonly List<Gatherer> Gatherers = new();
    public Hauler Hauler;

    private readonly Dictionary<string, int> _levels = new();
    // A guest's OWN base, set aside while it visits someone else's. Static: the scene is
    // reloaded when the party goes to the arena and back, and an instance field went
    // with it -- a guest who followed the party lost its own base.
    private static double _ownOre, _ownSalvage, _ownCredits;
    private static readonly Dictionary<string, int> _ownLevels = new();
    private static readonly Dictionary<string, double> _ownInvested = new();
    private static bool _parked;
    private double _totalsCd, _stateCd, _t;

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
        if (_parked && !Net.IsOnline) CallDeferred(nameof(RestoreOwn));
        ZIndex = 3;                                        // above the base
        Hauler = new Hauler { Yard = this, Name = "Hauler" };
        AddChild(Hauler);
        SyncFleet();
        ReturnFromTrip();
    }

    // ── away on a mission ────────────────────────────────────────────────────
    // The base is not simulated while the party is in the arena. It is saved on the
    // way out; on return it gets back what it had, any credits earned out there, and
    // 1/20 of what its fleet would have gathered in the time away.
    public const double AwayShare = 1.0 / 20.0;
    // everything spent on upgrades so far, per category (MINERS, SALVAGERS, HAULER)
    private readonly Dictionary<string, double> _invested = new();
    private static readonly string[] Categories = { "MINERS", "SALVAGERS", "HAULER" };
    public double Invested(string category) => _invested.TryGetValue(category, out var v) ? v : 0;
    public double RebuildCost(string category) => Math.Round(Invested(category) * Economy.RebuildShare);

    private sealed record Trip(double Ore, double Salvage, double Credits, Dictionary<string, int> Levels, Dictionary<string, double> Invested,
                               double OrePerSec, double SalvagePerSec);
    public static double TripClock;                   // game seconds in the arena (Hub counts them)
    private static Trip _trip;
    public static double TripCredits;                 // earned while away (a bounty), paid on return
    public static double LastAway, LastAwayOre, LastAwaySalvage;   // what the last return credited
    public static double TripStartCredits;            // the credits set aside when the last trip began
    // a guest's share of a bounty goes to its OWN base, set aside while it visits
    public static void AddGuestShare(double credits) => _ownCredits += credits;
    // the host's share, earned in the arena (where there is no yard): paid on the way home
    public static void AddHostShare(double credits) => TripCredits += credits;

    // Reaching the main menu ends the session. These statics outlive the scene ON PURPOSE -- a
    // sector change and a visit both tear the Yard down and rebuild it -- but nothing cleared them
    // when the SESSION ended, and `Yard._Ready` applies them unconditionally. So quitting from the
    // arena left `_trip` behind and the next session's economy was overwritten by that snapshot,
    // and quitting while visiting left `_parked` set so the next session restored the base from
    // the one before. Across characters too: the yard is not saved per character, or at all.
    public static void EndSession()
    {
        _trip = null;
        TripCredits = TripClock = TripStartCredits = 0;
        LastAway = LastAwayOre = LastAwaySalvage = 0;
        _parked = false;
        _ownOre = _ownSalvage = _ownCredits = 0;
        _ownLevels.Clear(); _ownInvested.Clear();
    }

    public void SaveForTrip()
    {
        _trip = new Trip(Ore, Salvage, Credits, new Dictionary<string, int>(_levels), new Dictionary<string, double>(_invested),
                         FleetRate(GatherKind.Miner), FleetRate(GatherKind.Salvager));
        TripCredits = 0; TripClock = 0; TripStartCredits = Credits;
    }

    private void ReturnFromTrip()
    {
        if (_trip == null) return;
        var t = _trip; _trip = null;
        LastAway = TripClock;
        LastAwayOre = t.OrePerSec * LastAway * AwayShare; LastAwaySalvage = t.SalvagePerSec * LastAway * AwayShare;
        Ore = t.Ore + LastAwayOre; Salvage = t.Salvage + LastAwaySalvage;
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
            if (g.Kind == k)
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
        else RpcId(1, nameof(RequestBuy), id);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestBuy(string id) { if (Net.IsHost) TryBuy(id); }

    public bool TryBuy(string id)
    {
        var u = Economy.ById(id);
        if (u == null || !Net.Sim) return false;
        int lv = Level(id);
        double cost = Economy.Cost(u, lv);
        if (Economy.Maxed(u, lv) || Credits < cost) return false;
        Credits -= cost; _levels[id] = lv + 1;
        _invested[u.Tab] = Invested(u.Tab) + cost;                // what a rebuild in this category is 10% of
        if (id is "miner_hull" or "salvager_hull")                   // the living ships of that kind gain it at once
            foreach (var g in Gatherers)
                if (g.State != Gatherer.St.Destroyed && (g.Kind == GatherKind.Miner) == (id == "miner_hull"))
                    g.Hull += Economy.UtilityHull * Economy.PercentEffect;
        SyncFleet();
        return true;
    }

    // ── the fleet: as many miners and salvagers as the levels say ────────────
    // Built in a fixed order (miners, then salvagers, by number) on every peer, so the
    // host's state arrays line up with each guest's list.
    private void SyncFleet()
    {
        void Match(GatherKind k, int want)
        {
            var have = Gatherers.Where(g => g.Kind == k).OrderBy(g => g.Index).ToList();
            for (int i = have.Count; i < want; i++)
            {
                var g = new Gatherer { Yard = this, Kind = k, Index = i, Name = $"{k}{i + 1}",
                                       Position = Hub.BasePos + (k == GatherKind.Miner ? new Vector2(-60 + 30 * i, -300) : new Vector2(-300, -60 + 30 * i)) };
                AddChild(g); Gatherers.Add(g);
            }
            foreach (var g in have.Where(g => g.Index >= want)) { Release(g); Gatherers.Remove(g); g.QueueFree(); }
        }
        Match(GatherKind.Miner, (int)Value("miner_count"));
        Match(GatherKind.Salvager, (int)Value("salvager_count"));
        Gatherers.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Index.CompareTo(b.Index));
    }

    // Where a ship works. Miners take different asteroids, nearest the base first;
    // salvagers spread over the face of the wreck that looks toward the base.
    private static readonly float[] WreckSpread = { 0f, 0.35f, -0.35f, 0.7f, -0.7f, 1.05f };
    // Where the hull really begins along each of those approaches, measured from
    // behemoth_wreck.png at its 0.34 scale (the outline is ragged, so no single radius
    // works: most approaches meet solid hull ~96 u from the centre, one not until 343).
    private static readonly float[] WreckEdge = { 96f, 96f, 101f, 95f, 343f, 112f };
    public (Vector2 spot, Vector2 target) WorkSpot(Gatherer g)
    {
        if (g.Kind == GatherKind.Miner)
        {
            var rocks = Hub.Rocks.OrderBy(r => r.Position.DistanceTo(Hub.BasePos)).ToList();
            var rock = rocks.Count > 0 ? rocks[g.Index % rocks.Count].Position : Hub.SunPos;
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
        if (best >= 0) { Arms[best].Occupant = g; _queue.Remove(g); return best; }
        if (!_queue.Contains(g)) _queue.Add(g);
        return -1;
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
        Arms[i].Occupant = null;
        if (_queue.Count > 0) { Arms[i].Occupant = _queue[0]; _queue.RemoveAt(0); }
    }

    // ── stock ────────────────────────────────────────────────────────────────
    public void Deposit(GatherKind k, double amount)
    {
        if (k == GatherKind.Miner) { Ore += amount; TotalMined += amount; }
        else { Salvage += amount; TotalSalvaged += amount; }
    }

    // The hauler loads the larger pile first, then both evenly.
    public double TakeStock(double want)
    {
        want = Math.Min(want, Ore + Salvage);
        if (want <= 0) return 0;
        double gap = Math.Min(Math.Abs(Ore - Salvage), want);
        if (Ore >= Salvage) Ore -= gap; else Salvage -= gap;
        double rest = want - gap;
        Ore -= rest / 2; Salvage -= rest / 2;
        return want;
    }

    // ── the hauler ───────────────────────────────────────────────────────────
    public int Pods => (int)Value("hauler_pods");
    public double PodSize => Value("pod_size");
    public double Capacity => Pods * PodSize;

    public void RequestDispatch()
    {
        if (Net.IsHost) Hauler.Dispatch();
        else RpcId(1, nameof(RequestDispatchRpc));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDispatchRpc() { if (Net.IsHost) Hauler.Dispatch(); }

    public void PortalFlash()
    {
        Hub.Portal.Flash();
        if (Net.IsHost && Net.IsOnline) Hub.RpcHome(this, nameof(NetPortalFlash));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetPortalFlash() => Hub.Portal.Flash();

    // ── REFIT's price: 10% of the stock you own ──────────────────────────────
    // The world's if you are its host (or offline); a guest's own, parked totals.
    public (double ore, double salvage, double credits) ResetCost() =>
        _parked ? (_ownOre * 0.1, _ownSalvage * 0.1, _ownCredits * 0.1) : (Ore * 0.1, Salvage * 0.1, Credits * 0.1);

    public void ChargeReset()
    {
        if (_parked) { _ownOre *= 0.9; _ownSalvage *= 0.9; _ownCredits *= 0.9; }
        else { Ore *= 0.9; Salvage *= 0.9; Credits *= 0.9; }
    }

    // ── visiting: park your own yard, restore it after ───────────────────────
    public void OnSessionChanged(bool guest)
    {
        if (guest && !_parked)
        {
            Bank();                                        // nothing your ships carry is lost
            (_ownOre, _ownSalvage, _ownCredits) = (Ore, Salvage, Credits);
            _ownLevels.Clear(); foreach (var kv in _levels) _ownLevels[kv.Key] = kv.Value;
            _ownInvested.Clear(); foreach (var kv in _invested) _ownInvested[kv.Key] = kv.Value;
            _parked = true;
            ResetShips();                                  // the host's packets drive them now
        }
        else if (!guest && _parked)
        {
            (Ore, Salvage, Credits) = (_ownOre, _ownSalvage, _ownCredits);
            _levels.Clear(); foreach (var kv in _ownLevels) _levels[kv.Key] = kv.Value;
            _invested.Clear(); foreach (var kv in _ownInvested) _invested[kv.Key] = kv.Value;
            _parked = false;
            ResetShips();                                  // not the host's last state
        }
        SyncFleet();
    }

    // Before parking: gatherer cargo returns to stock; the hauler's returns too if it
    // is still home, or is paid if it is already through the portal.
    private void Bank()
    {
        foreach (var g in Gatherers) { Deposit(g.Kind, g.Cargo); g.Cargo = 0; }
        if (Hauler.Cargo <= 0) return;
        if (Hauler.State == Hauler.St.Away) Credits += Hauler.Cargo * Economy.CreditsPerUnit;
        else { Ore += Hauler.Cargo / 2; Salvage += Hauler.Cargo / 2; }
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
        if (!Net.Sim || !Net.IsOnline) return;
        _totalsCd -= delta;
        if (_totalsCd <= 0)
        {
            _totalsCd = 1.0;
            var lv = Economy.All.Select(u => Level(u.Id)).ToArray();
            Hub.RpcHome(this, nameof(NetTotals), Ore, Salvage, Credits, lv, Categories.Select(Invested).ToArray());
        }
        _stateCd -= delta;
        if (_stateCd <= 0) { _stateCd = 0.1; SendState(); }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetTotals(double ore, double salvage, double credits, int[] levels, double[] invested)
    {
        for (int i = 0; i < Math.Min(invested.Length, Categories.Length); i++) _invested[Categories[i]] = invested[i];
        (Ore, Salvage, Credits) = (ore, salvage, credits);
        for (int i = 0; i < Math.Min(levels.Length, Economy.All.Length); i++) _levels[Economy.All[i].Id] = levels[i];
        SyncFleet();
    }

    private void SendState()
    {
        int n = Gatherers.Count;
        var gp = new Vector2[n]; var gr = new float[n]; var gs = new int[n]; var gc = new float[n]; var gb = new Vector2[n]; var gh = new float[n];
        for (int i = 0; i < n; i++)
        {
            var g = Gatherers[i];
            (gp[i], gr[i], gs[i], gc[i], gb[i], gh[i]) = (g.Position, g.Rotation, (int)g.State, (float)g.Cargo, g.BeamTo, (float)g.Hull);
        }
        var h = Hauler;
        Hub.RpcHome(this, nameof(NetState), gp, gr, gs, gc, gb, gh, h.Position, h.Rotation, (int)h.State, (float)h.T, (float)h.Cargo, (float)h.LastSale, (float)h.Hull);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(Vector2[] gp, float[] gr, int[] gs, float[] gc, Vector2[] gb, float[] gh,
                          Vector2 hp, float hr, int hs, float ht, float hc, float sale, float hh)
    {
        // the fleet follows the levels (1 s); until they agree, skip the ships this once
        if (gp.Length == Gatherers.Count)
            for (int i = 0; i < gp.Length; i++) Gatherers[i].SetNet(gp[i], gr[i], gs[i], gc[i], gb[i], gh[i]);
        Hauler.SetNet(hp, hr, hs, ht, hc, sale, hh);
    }

    // ── the hologram docking bars: two bars across each arm's open face ──────
    public override void _Draw()
    {
        // a damaged ship shows its hull: a small bar under it, green to red
        foreach (var g in Gatherers) HullBar(g.Position + new Vector2(-15, 26), 30, g.Hull, g.MaxHull, g.Visible);
        if (Hauler != null) HullBar(Hauler.Position + new Vector2(-40, 50), 80, Hauler.Hull, Hauler.MaxHull, Hauler.State is not (Hauler.St.Destroyed or Hauler.St.Away));
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

    private void RestoreOwn() => OnSessionChanged(false);

    private void HullBar(Vector2 world, float w, double hull, double max, bool shown)
    {
        if (!shown || hull >= max) return;
        var at = ToLocal(world); float k = (float)Math.Clamp(hull / max, 0, 1);
        DrawRect(new Rect2(at, new Vector2(w, 3.5f)), new Color(0.1f, 0.1f, 0.12f, 0.85f));
        DrawRect(new Rect2(at, new Vector2(w * k, 3.5f)), new Color(1f - k, 0.35f + 0.55f * k, 0.3f));
    }
}
