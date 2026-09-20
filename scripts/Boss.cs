using Godot;
using System.Linq;

// THE SILVER LANCER -- the first bounty boss. Host-simulated; every peer draws it from
// the host's 10 Hz state and plays its telegraphs from the host's events.
// One rhythm, 30 s long (all damage x the tier's scale):
//   GUNS       always: 3.6 every 1.2 s (3 DPS) at the nearest ship within 900 u
//   DEATH BEAM at 6 s, then every 30 s: a red line for 2 s, then live for 3 s,
//              checking every 0.25 s: 50 each time it lands on a ship (a ship's 0.52 s
//              invulnerability to one source means a hit about every 0.75 s)
//   CHARGE     15 s after each beam: a red line for 1.5 s, then a ram along it at
//              1200 u/s: 40 to any ship in its path
//   TRIDENT    between them (every 15 s from 13.5 s): 3 guided missiles, 0 and +-25
//              degrees, 15 each, twice the size -- INTERCEPTABLE (PD shoots them)
//   SHOCKWAVE  every 17 s: a red ring for 1.8 s, then 45 within 340 u
// Slow and heavy: it closes on the party to about 650 u and turns ponderously.
public partial class Boss : Node2D, IHittable
{
    public Hub Hub;
    // hull and damage by level and party: S(L)(1 + 0.6(P-1)) and S(L)(1 + 0.2(P-1))
    public double HullMult = 1, DamageMult = 1;
    public double MaxHp => Missions.Current.Hull * HullMult;
    public const float Length = 360f, HalfWidth = 70f;
    public const int Id = 3000;
    public double Hp;
    public bool Alive => Hp > 0;
    public int NetId => Id;
    public float HitRadius => HalfWidth;
    public bool Covers(Vector2 p, float pad)      // a capsule along its keel, like a player ship
    {
        var fwd = Vector2.Up.Rotated(Rotation); float half = Length * 0.5f - HalfWidth;
        float t = Mathf.Clamp((p - Position).Dot(fwd), -half, half);
        return p.DistanceTo(Position + fwd * t) <= HalfWidth + pad;
    }

    public const double BeamWindup = 6.0, WaveWindup = 1.8;
    // As the beam charges, the boss launches two light ESCORTS at its target -- one 45 degrees
    // to port, one to starboard -- boosting all the way in to pin the pilot in the beam. Fragile
    // on purpose (point defence, 1 DPS a turret, must be able to kill them inside the charge).
    public const float EscortAngle = 45f;
    public const double EscortHull = 3;           // 3 s at one PD turret (1 DPS): killable inside the charge
    public const double WebToBeam = 1.0;          // the beam starts charging 1 s after the web should land
    public const float TurnRate = 0.3f;           // its native turn, ponderous (rad/s) -- and all it has while charging
    // The beam reaches right across the arena: once it is charging, distance is no escape --
    // only angle is. Its ACTIVATION is unchanged (the 30 s rhythm, the nearest pilot, the escorts'
    // predicted web); this is reach, not trigger.
    public const float BeamLength = 10000f, BeamWidth = 70f, WaveRadius = 340f;
    public const double GunDamage = 3.6, GunEvery = 1.2;                       // 3 DPS
    public const double BeamEvery = 30, BeamLive = 3.0, BeamTick = 0.25, BeamDamage = 50;
    public const double ChargeWindup = 1.5, ChargeDamage = 40; public const float ChargeSpeed = 1200f, ChargeLength = 900f;
    public const double TridentEvery = 15, TridentDamage = 15; public const float TridentSpread = 25f, TridentSpeed = 135f, TridentRange = 1920f;
    private double _guns = 2.0, _missiles = 13.5, _beam = 6.0, _charge = 21.0, _wave = 10.0, _send;
    private double _beamLive = -1, _beamTickT;
    private (Vector2 a, Vector2 b)? _pendingCharge; private double _chargeT; private Vector2? _dashTo;
    public int Volleys { get; private set; }                                   // for the smoke test
    public bool Charging => _dashTo.HasValue;
    public bool BeamCharging => _beamCharging;
    // Frozen: a super move is winding up. It holds station so the red line it drew is the line
    // it actually fires down -- the tell used to drift off the hull while it kept closing.
    public bool Locked => _beamCharging || _pendingCharge != null || _dashTo.HasValue;
    // Where the beam goes RIGHT NOW: straight out of the nose. The telegraph is a child of the
    // boss drawn down the same axis, so the drawing and the hit are the same line by construction.
    public (Vector2 a, Vector2 b) BeamSegment()
    {
        var nose = ToGlobal(new Vector2(0, -Length * 0.5f));
        return (nose, nose + Vector2.Up.Rotated(Rotation) * BeamLength);
    }
    // The beam's escorts: port and starboard of the nose, straight at the pilot. Returns roughly
    // how long until their web should land -- the boss commits its beam on that estimate, so
    // shooting them down does not cancel the beam, it only means facing it free to move.
    public double LaunchEscorts(Node2D target)
    {
        if (!Net.Sim || target == null || GetParent() is not Hub hub) return Raider.EscortShiver;
        var nose = Vector2.Up.Rotated(Rotation);
        double eta = 0; bool port = true;
        foreach (float side in new[] { -EscortAngle, EscortAngle })
        {
            var dir = nose.Rotated(Mathf.DegToRad(side));
            var at = Position + dir * (HalfWidth + 60f);
            var r = hub.SpawnRaider(at, RaiderKind.Light, 0, Missions.S(Missions.Level));
            // the one launched to port flanks to port, the other to starboard
            r?.Escort(target, dir, BeamWindup, EscortHull * Missions.S(Missions.Level), port ? -Mathf.Pi / 2f : Mathf.Pi / 2f);
            eta = System.Math.Max(eta, Raider.WebEta(at.DistanceTo(target.Position)));
            port = false;
        }
        return eta;
    }

    private double _beamT; private bool _beamCharging;
    private double _beamArm = -1;                 // >= 0: escorts away, counting down to the charge
    private PlayerShip _beamTarget;
    private Vector2? _pendingWave; private double _waveT;
    // The skinny bar under the health bar: how far along the wait for the next SUPER MOVE --
    // the ram, or the death beam's escorts going out. Both are 15 s apart once the fight settles.
    private double _superGap = 6.0;
    public double NextSuperIn => System.Math.Max(0, System.Math.Min(_beam, _charge));
    public double SuperFill => _superGap <= 0 ? 0 : System.Math.Clamp(1 - NextSuperIn / _superGap, 0, 1);
    private Vector2 _netPos; private float _netRot; private bool _hasNet; private bool _netLocked;

    public override void _Ready()
    {
        int party = System.Math.Max(1, Hub.PartySize);
        HullMult = Missions.HullMult(Missions.Level, party); DamageMult = Missions.DamageMult(Missions.Level, party); Hp = MaxHp;
        Name = "Boss";
        var tex = GD.Load<Texture2D>("res://boss_raider.png");     // raider red, a white skull on its centre
        AddChild(new Sprite2D { Texture = tex, Scale = Vector2.One * (Length / tex.GetHeight()) });
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

    private System.Collections.Generic.IEnumerable<PlayerShip> Pilots => Combat.Players.OfType<PlayerShip>().Where(p => p.Alive);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!Net.Sim)
        {
            if (_hasNet)
            {
                if (_netLocked)
                {   // Locked, the hull IS the telegraph, and smoothing it is no longer cosmetic:
                    // the lerp lags about 0.1 s, which at 0.3 rad/s is ~1.7 degrees -- some 300 u
                    // at the beam's 10000 u reach, against a beam 70 u wide. A guest would watch
                    // the hit land far outside the line it was shown. Take the host's figures flat;
                    // the boss is standing still and turning slowly, so there is nothing to smooth.
                    Position = _netPos; Rotation = _netRot;
                }
                else { Position = Position.Lerp(_netPos, Mathf.Clamp(10f * dt, 0f, 1f)); Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(10f * dt, 0f, 1f)); }
            }
            return;
        }
        if (!Alive) return;
        var pilots = Pilots.ToList();
        if (pilots.Count > 0)
        {   // close on the party, ponderously -- unless a super move has it locked down, when it
            // neither closes nor turns except as that move's own aim decides
            if (!Locked)
            {
                var centre = pilots.Aggregate(Vector2.Zero, (s, p) => s + p.Position) / pilots.Count;
                float want = (centre - Position).Angle() + Mathf.Pi / 2f;
                Rotation += Mathf.Clamp(Mathf.AngleDifference(Rotation, want), -TurnRate * dt, TurnRate * dt);
                float d = Position.DistanceTo(centre);
                if (d > 650f) Position += (centre - Position).Normalized() * 30f * dt;
            }
            Tick(pilots, delta);
        }
        _send -= delta;
        // While a super move winds up the HULL IS THE TELEGRAPH, so a guest needs its angle far
        // more often than 10 Hz. Turning at 0.3 rad/s, 100 ms between figures is ~1.7 degrees,
        // which at the beam's 10000 u reach puts the far end ~300 u off against a beam 70 u wide.
        // 30 Hz while locked, and guests stop smoothing it (see _netLocked), closes that.
        // To the peers actually IN the arena. Broadcasting to everyone meant a guest still loading
        // the arena scene got packets for a Hub/Boss it did not have yet -- "Node not found",
        // "Invalid packet received" -- and at 30 Hz while locked there are three times as many
        // chances to land in that window.
        if (_send <= 0 && Net.IsOnline)
        {
            _send = Locked ? 1.0 / 30 : 0.1;
            Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetState), Position, Rotation, Hp, Locked);
        }
    }

    private void Tick(System.Collections.Generic.List<PlayerShip> pilots, double delta)
    {
        var nose = ToGlobal(new Vector2(0, -Length * 0.5f));
        PlayerShip Nearest(float within) => pilots.Where(p => p.Position.DistanceTo(Position) <= within).OrderBy(p => p.Position.DistanceTo(Position)).FirstOrDefault();
        _guns -= delta;
        if (_guns <= 0) { _guns = GunEvery; var t = Nearest(900f); if (t != null) { t.Hit(GunDamage * DamageMult, Position, "boss:guns"); Combat.Flash(nose, t.Position, new Color(1f, 0.5f, 0.35f), boss: true); } }
        _missiles -= delta;
        if (_missiles <= 0)
        {   // a trident at the nearest ship: straight at it and 25 degrees either side
            _missiles = TridentEvery; Volleys++;
            var t = pilots.OrderBy(p => p.Position.DistanceTo(Position)).First();
            var aim = (t.Position - nose).Normalized();
            foreach (float deg in new[] { -TridentSpread, 0f, TridentSpread })
            {
                var dir = aim.Rotated(Mathf.DegToRad(deg));
                Combat.LaunchTorpedo(nose + dir * 14f, dir, TridentSpeed, TridentRange, TridentDamage * DamageMult, t.NetId, 1.4f,
                                     heavy: false, hostile: true, hitSource: "boss:missiles", size: 2f);
            }
        }
        _beam -= delta;
        if (_beam <= 0 && _beamArm < 0 && !_beamCharging && _beamLive < 0)
        {   // the death beam OPENS WITH ITS ESCORTS. The charge follows when their web should
            // land -- an estimate made now, not a wait on them actually arriving, so killing
            // them still ends in a beam: one the pilot is free to fly out of.
            _beam = BeamEvery; _superGap = System.Math.Min(_beam, _charge);
            _beamTarget = pilots.OrderBy(p => p.Position.DistanceTo(Position)).First();
            _beamArm = LaunchEscorts(_beamTarget) + WebToBeam;
        }
        if (_beamArm >= 0 && (_beamArm -= delta) <= 0)
        {   // locked down now; the telegraph rides the hull, so the line cannot lie
            _beamArm = -1; _beamCharging = true; _beamT = BeamWindup;
            if (!IsInstanceValid(_beamTarget) || !_beamTarget.Alive) _beamTarget = pilots.FirstOrDefault();
            Tele(true, new Vector2(0, -Length * 0.5f), new Vector2(0, -Length * 0.5f - BeamLength), BeamWidth, BeamWindup, onHull: true);
        }
        if (_beamCharging)
        {   // held still, tracking with nothing but its own ponderous turn: a quick pilot who is
            // not webbed can still get outside the arc before it fires
            if (IsInstanceValid(_beamTarget) && _beamTarget.Alive)
            {
                float aim = (_beamTarget.Position - Position).Angle() + Mathf.Pi / 2f;
                Rotation += Mathf.Clamp(Mathf.AngleDifference(Rotation, aim), -TurnRate * (float)delta, TurnRate * (float)delta);
            }
            if ((_beamT -= delta) <= 0) { _beamCharging = false; _beamLive = BeamLive; _beamTickT = 0; }
        }
        if (_beamLive >= 0)
        {   // live: a check every 0.25 s, down the nose as it points at this instant
            _beamTickT -= delta;
            if (_beamTickT <= 0)
            {
                _beamTickT = BeamTick;
                var (la, lb) = BeamSegment();
                foreach (var p in pilots)
                    if (DistToSegment(p.Position, la, lb) <= BeamWidth / 2f + p.HitRadius) p.Hit(BeamDamage * DamageMult, Position, "boss:beam");
            }
            _beamLive -= delta;
        }
        _charge -= delta;
        if (_charge <= 0 && _pendingCharge == null && !_dashTo.HasValue && !_beamCharging)
        {   // the ram: come round onto the pilot, then hold dead still for the wind-up so the
            // line drawn is the line rammed down
            _charge = BeamEvery; _superGap = System.Math.Min(_beam, _charge);
            var t = pilots.OrderBy(p => p.Position.DistanceTo(Position)).First();
            Rotation = (t.Position - Position).Angle() + Mathf.Pi / 2f;
            var a = Position; var bb = a + Vector2.Up.Rotated(Rotation) * ChargeLength;
            _pendingCharge = (a, bb); _chargeT = ChargeWindup;
            Tele(true, Vector2.Zero, new Vector2(0, -ChargeLength), HalfWidth * 2f, ChargeWindup, onHull: true);
        }
        if (_pendingCharge is { } ch && (_chargeT -= delta) <= 0) { _dashTo = ch.b; _pendingCharge = null; }
        if (_dashTo is { } to)
        {
            Position = Position.MoveToward(to, ChargeSpeed * (float)delta);
            foreach (var p in pilots) if (Covers(p.Position, p.HitRadius)) p.Hit(ChargeDamage * DamageMult, Position, "boss:charge");
            if (Position.DistanceTo(to) < 1f) _dashTo = null;
        }
        _wave -= delta;
        if (_wave <= 0 && _pendingWave == null)
        {
            _wave = 17.0; _pendingWave = Position; _waveT = WaveWindup;
            Tele(false, Position, Vector2.Zero, WaveRadius, WaveWindup);
        }
        if (_pendingWave is { } c && (_waveT -= delta) <= 0)
        {
            foreach (var p in pilots) if (p.Position.DistanceTo(c) <= WaveRadius + p.HitRadius) p.Hit(45 * DamageMult, c, "boss:wave");
            _pendingWave = null;
        }
    }

    public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    // host: show a telegraph here and on every guest.
    // onHull: a and b are in the BOSS'S OWN FRAME and the telegraph is parented to it, so the
    // warning swings with the hull instead of being pinned to the spot the boss stood on when it
    // drew it. Guests already lerp the boss's rotation from NetState, so their copy tracks too --
    // no per-frame line updates over the wire.
    private void Tele(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull = false)
    {
        ShowTelegraph(line, a, b, size, time, onHull);
        // arena peers only, for the same reason as NetState above
        if (Net.IsOnline) Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetTelegraph), line, a, b, size, time, onHull);
    }
    private void ShowTelegraph(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull) =>
        (onHull ? (Node)this : GetParent()).AddChild(new Telegraph { Line = line, A = a, B = b, Width = size, Radius = size, Duration = time });

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetTelegraph(bool line, Vector2 a, Vector2 b, float size, double time, bool onHull) => ShowTelegraph(line, a, b, size, time, onHull);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(Vector2 p, float rot, double hp, bool locked) { _netPos = p; _netRot = rot; _hasNet = true; Hp = hp; _netLocked = locked; }

    public int TelegraphsPending => (_beamCharging ? 1 : 0) + (_pendingWave != null ? 1 : 0);
}
