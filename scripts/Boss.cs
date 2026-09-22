using Godot;
using System.Collections.Generic;
using System.Linq;

// A BOUNTY BOSS -- what every boss is. Its hull and damage, scaled by the level and the party; a
// hostile the party's weapons find; host-simulated, every peer drawing it from the host's state
// (10 Hz, 30 Hz while it is Locked) and playing its telegraphs from the host's events; the bar of
// its next super move. What it DOES is its own class (Lancer, ...): Missions.ForLevel says which
// boss a level is, and every peer builds that one for the level (Hub.BuildArena), named "Boss" so
// the host's RPCs find it.
//
// LOCKED: a super move is winding up or firing. The boss then neither closes nor turns except as
// that move itself decides -- the red line it drew is the line it fires down -- and a guest takes
// its pose flat rather than easing it (see _netLocked).
public abstract partial class Boss : Node2D, IHittable, ITagged, IStatused
{
    public Hub Hub;
    public Missions.BossType Type;              // which boss: its name and its base hull
    // hull and damage by level and party: S(L)(1 + 0.6(P-1)) and S(L)(1 + 0.2(P-1))
    public double HullMult = 1, DamageMult = 1;
    public double MaxHp => Type.Hull * HullMult;
    public abstract float Length { get; }
    public abstract float HalfWidth { get; }
    protected abstract string Sprite { get; }
    public const int Id = NetIds.Boss;
    public Tag Tags => Tag.Boss;
    // A bastion's shockwave cannot throw a boss, so it holds it still instead (Status.Disabled):
    // it neither moves nor acts while it lasts. The host decides; guests simply stop being told
    // to move it.
    private StatusSet _status;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status s, double seconds) { if (Net.Sim) _status.Apply(s, seconds); }
    public bool Held => _status.Has(Status.Disabled);
    public double Hp;
    public bool Alive => Hp > 0;
    public int NetId => Id;
    public float HitRadius => HalfWidth;
    public bool Covers(Vector2 p, float pad) => Combat.KeelCovers(this, Length, HalfWidth, p, pad);

    public const float TurnRate = 0.3f;           // its native turn, ponderous (rad/s)
    public abstract bool Locked { get; }
    public abstract int TelegraphsPending { get; }
    protected abstract void Tick(List<PlayerShip> pilots, double delta);

    // The skinny bar under the health bar: how far along the wait for the next SUPER MOVE. The host
    // says how long is left (NextSuperHost) and across what gap it counts (SuperGap, reset as each
    // super commits).
    protected abstract double NextSuperHost { get; }
    protected double SuperGap = 6.0;
    // HOST-ONLY TIMERS, SO GUESTS ARE TOLD. A boss's super timers only ever tick under Net.Sim, so on
    // a guest they sit at their starting values for the whole fight and the bar under the boss's
    // hull read zero from the first shot to the last. It is drawn state, so it goes on the wire.
    // Between packets a guest runs the clock down itself: the bar has to move smoothly at 10 Hz,
    // and a countdown corrected thirty times a second cannot drift anywhere.
    private double _netNextSuper = -1, _netSuperGap;
    public double NextSuperIn => Net.Sim ? System.Math.Max(0, NextSuperHost) : System.Math.Max(0, _netNextSuper);
    public double SuperFill
    {
        get { double gap = Net.Sim ? SuperGap : _netSuperGap; return gap <= 0 ? 0 : System.Math.Clamp(1 - NextSuperIn / gap, 0, 1); }
    }
    private NetPose _net; private bool _netLocked;
    private double _send;
    private HullWatch _hullWatch;

    public override void _Ready()
    {
        int party = System.Math.Max(1, Hub.PartySize);
        HullMult = Missions.HullMult(Missions.Level, party); DamageMult = Missions.DamageMult(Missions.Level, party); Hp = MaxHp;
        Name = "Boss";
        AddChild(Sprites.Fit(Sprite, Length));
        ZIndex = 4;
        Combat.Hostiles.Add(this);
    }
    public override void _ExitTree() => Combat.Hostiles.Remove(this);

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;
        Hp = System.Math.Max(0, Hp - d);
        if (Hp <= 0) Hub.BossDefeated();
    }
    // the host's word that it is down: a peer that never had its figures (one that arrived after the
    // win) shows nothing for it
    public void Downed() { if (!_net.Has) _hullWatch = default; Hp = 0; }

    private IEnumerable<PlayerShip> Pilots => Targeting.All(Combat.Players, Targeting.Attackable).OfType<PlayerShip>();

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _hullWatch.Tick(this, Hp, taken: false);
        if (!Alive && _telegraphs.Count > 0) ClearTelegraphs();
        if (!Net.Sim)
        {
            if (_net.Has)
            {
                if (_netLocked)
                {   // Locked, the hull IS the telegraph, and smoothing it is no longer cosmetic:
                    // the lerp lags about 0.1 s, which at 0.3 rad/s is ~1.7 degrees -- some 300 u
                    // at a beam's 10000 u reach, against a beam 70 u wide. A guest would watch
                    // the hit land far outside the line it was shown. Take the host's figures flat;
                    // the boss is standing still and at most turning slowly (to face its target
                    // while its escorts are out), so there is nothing to smooth.
                    Position = _net.Pos; Rotation = _net.Rot;
                }
                else _net.Follow(this, dt);
                if (_netNextSuper > 0) _netNextSuper = System.Math.Max(0, _netNextSuper - delta);
            }
            return;
        }
        if (!Alive) return;
        _status.Tick(delta);
        if (Held) { QueueRedraw(); return; }         // held still: no approach, no ability, no turn
        var pilots = Pilots.ToList();
        if (pilots.Count > 0)
        {
            if (!Locked) Approach(pilots, dt);
            Tick(pilots, delta);
        }
        _send -= delta;
        // While a super move winds up the HULL IS THE TELEGRAPH, so a guest needs its angle far
        // more often than 10 Hz: 30 Hz while locked, and guests stop smoothing it (see _netLocked).
        // To the peers actually IN the arena. Broadcasting to everyone meant a guest still loading
        // the arena scene got packets for a Hub/Boss it did not have yet -- "Node not found",
        // "Invalid packet received" -- and at 30 Hz while locked there are three times as many
        // chances to land in that window.
        if (_send <= 0 && Net.IsOnline)
        {
            _send = Locked ? 1.0 / 30 : 0.1;
            Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetState), Position, Rotation, Hp, Locked,
                             NextSuperIn, SuperGap, HullMult);
        }
    }

    // Unlocked, it closes on the party, ponderously: turning onto the party's centre, and moving in
    // to about 650 u of it.
    private void Approach(List<PlayerShip> pilots, float dt)
    {
        var centre = pilots.Aggregate(Vector2.Zero, (s, p) => s + p.Position) / pilots.Count;
        float want = Aim.Face(Position, centre);
        Rotation = Mathf.RotateToward(Rotation, want, TurnRate * dt);
        if (Position.DistanceTo(centre) > 650f) Position += (centre - Position).Normalized() * 30f * dt;
    }

    // host: show a telegraph here and on every guest.
    // onHull: a and b are in the BOSS'S OWN FRAME and the telegraph is parented to it, so the
    // warning swings with the hull instead of being pinned to the spot the boss stood on when it
    // drew it. Guests already follow the boss's pose from NetState, so their copy tracks too --
    // no per-frame line updates over the wire.
    // cue / strike: the move's sounds, as the warning goes up and as it lands (Telegraph)
    protected void Tele(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull = false, double hold = 0,
                        string cue = null, string strike = null)
    {
        ShowTelegraph(line, a, b, size, time, onHull, hold, cue, strike);
        // arena peers only, for the same reason as NetState above
        if (Net.IsOnline) Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetTelegraph), line, a, b, size, time, onHull, hold, cue ?? "", strike ?? "");
    }
    private void ShowTelegraph(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull, double hold, string cue, string strike)
    {
        var t = new Telegraph { Line = line, A = a, B = b, Width = size, Radius = size, Duration = time, Hold = hold,
                                Cue = string.IsNullOrEmpty(cue) ? null : cue, Strike = string.IsNullOrEmpty(strike) ? null : strike };
        (onHull ? (Node)this : GetParent()).AddChild(t);
        _telegraphs.RemoveAll(x => !IsInstanceValid(x));
        _telegraphs.Add(t);
    }
    // A boss that is down threatens nothing, and the party stays with it until every pilot presses
    // RETURN: a beam half wound up went on charging, then "fired", over a DEFEATED boss. Its warnings
    // go with it, on every peer (a guest's copy dies by the host's figure).
    private readonly List<Telegraph> _telegraphs = new();
    private void ClearTelegraphs()
    {
        foreach (var t in _telegraphs) if (IsInstanceValid(t)) t.QueueFree();
        _telegraphs.Clear();
    }

    // A guest's warning ends when ITS position is judged, not when the host's clock says (see
    // Net.Arriving): on the internet the two were a round trip apart, and a pilot who cleared the
    // red on their own screen was hit "outside" it.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetTelegraph(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull, double hold, string cue, string strike) =>
        ShowTelegraph(line, a, b, size, Net.Arriving(time), onHull, hold, cue, strike);

    // host: a special move's sound, here and on every guest in the arena -- for a move with no
    // warning to carry it (Tele's cue and strike)
    protected void Sound(string name, Vector2 at)
    {
        Sfx.Special(name, at);
        if (Net.IsOnline) Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetSound), name, at);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetSound(string name, Vector2 at) => Sfx.Special(name, at);

    // A PILOT ARRIVING MID-FIGHT -- a rejoin, a slow load -- missed every warning already up (and the
    // Drake's rock, if one is thrown): each is sent once, as it starts, to the peers in the arena
    // then. The host sends such a peer what is up now, as it stands (Hub.NetMySector). A warning's
    // opening sound is not replayed; its landing sound still plays.
    public virtual void CatchUp(int peer)
    {
        foreach (var t in _telegraphs)
        {
            if (!IsInstanceValid(t)) continue;
            double left = t.Duration - t.Elapsed, hold = t.Hold;
            if (left < 0) { hold += left; left = 0; }
            if (left <= 0 && hold <= 0) continue;                  // landed: only its flash is left
            RpcId(peer, nameof(NetTelegraph), t.Line, t.A, t.B, t.Width, left, t.GetParent() == this, hold, "", left > 0 ? t.Strike ?? "" : "");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(Vector2 p, float rot, double hp, bool locked, double nextSuper, double superGap, double hullMult)
    {
        if (!_net.Has) _hullWatch = default;       // the host's first figure is where this peer starts counting
        // the host's scale too: a guest built its boss for the party it saw, and one that rejoined
        // mid-fight saw a different party
        _net.Set(p, rot); Hp = hp; _netLocked = locked; HullMult = hullMult;
        _netNextSuper = nextSuper; _netSuperGap = superGap;
    }
}
