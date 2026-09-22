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
public abstract partial class Boss : Node2D, IHittable
{
    public Hub Hub;
    public Missions.BossType Type;              // which boss: its name and its base hull
    // hull and damage by level and party: S(L)(1 + 0.6(P-1)) and S(L)(1 + 0.2(P-1))
    public double HullMult = 1, DamageMult = 1;
    public double MaxHp => Type.Hull * HullMult;
    public abstract float Length { get; }
    public abstract float HalfWidth { get; }
    protected abstract string Sprite { get; }
    public const int Id = 3000;
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

    private IEnumerable<PlayerShip> Pilots => Combat.Players.OfType<PlayerShip>().Where(p => p.Alive);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
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
        float want = (centre - Position).Angle() + Mathf.Pi / 2f;
        Rotation = Mathf.RotateToward(Rotation, want, TurnRate * dt);
        if (Position.DistanceTo(centre) > 650f) Position += (centre - Position).Normalized() * 30f * dt;
    }

    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    // host: show a telegraph here and on every guest.
    // onHull: a and b are in the BOSS'S OWN FRAME and the telegraph is parented to it, so the
    // warning swings with the hull instead of being pinned to the spot the boss stood on when it
    // drew it. Guests already follow the boss's pose from NetState, so their copy tracks too --
    // no per-frame line updates over the wire.
    protected void Tele(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull = false, double hold = 0)
    {
        ShowTelegraph(line, a, b, size, time, onHull, hold);
        // arena peers only, for the same reason as NetState above
        if (Net.IsOnline) Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetTelegraph), line, a, b, size, time, onHull, hold);
    }
    private void ShowTelegraph(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull, double hold)
    {
        var t = new Telegraph { Line = line, A = a, B = b, Width = size, Radius = size, Duration = time, Hold = hold };
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
    private void NetTelegraph(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull, double hold) =>
        ShowTelegraph(line, a, b, size, Net.Arriving(time), onHull, hold);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(Vector2 p, float rot, double hp, bool locked, double nextSuper, double superGap, double hullMult)
    {
        // the host's scale too: a guest built its boss for the party it saw, and one that rejoined
        // mid-fight saw a different party
        _net.Set(p, rot); Hp = hp; _netLocked = locked; HullMult = hullMult;
        _netNextSuper = nextSuper; _netSuperGap = superGap;
    }
}
