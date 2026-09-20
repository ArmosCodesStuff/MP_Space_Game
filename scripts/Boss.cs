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
    public const float BeamLength = 1800f, BeamWidth = 70f, WaveRadius = 340f;
    public const double GunDamage = 3.6, GunEvery = 1.2;                       // 3 DPS
    public const double BeamEvery = 30, BeamLive = 3.0, BeamTick = 0.25, BeamDamage = 50;
    public const double ChargeWindup = 1.5, ChargeDamage = 40; public const float ChargeSpeed = 1200f, ChargeLength = 900f;
    public const double TridentEvery = 15, TridentDamage = 15; public const float TridentSpread = 25f, TridentSpeed = 135f, TridentRange = 1920f;
    private double _guns = 2.0, _missiles = 13.5, _beam = 6.0, _charge = 21.0, _wave = 10.0, _send;
    private double _beamLive = -1, _beamTickT;
    private (Vector2 a, Vector2 b)? _pendingCharge; private double _chargeT; private Vector2? _dashTo;
    public int Volleys { get; private set; }                                   // for the smoke test
    public bool Charging => _dashTo.HasValue;
    // the beam's escorts: port and starboard of the nose, straight at the pilot
    public void LaunchEscorts(Node2D target)
    {
        if (!Net.Sim || target == null || GetParent() is not Hub hub) return;
        var nose = Vector2.Up.Rotated(Rotation);
        foreach (float side in new[] { -EscortAngle, EscortAngle })
        {
            var dir = nose.Rotated(Mathf.DegToRad(side));
            var r = hub.SpawnRaider(Position + dir * (HalfWidth + 60f), RaiderKind.Light, 0, Missions.S(Missions.Level));
            r?.Escort(target, dir, BeamWindup, EscortHull * Missions.S(Missions.Level));
        }
    }

    private (Vector2 a, Vector2 b)? _pendingBeam; private double _beamT;
    private Vector2? _pendingWave; private double _waveT;
    private Vector2 _netPos; private float _netRot; private bool _hasNet;

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
            if (_hasNet) { Position = Position.Lerp(_netPos, Mathf.Clamp(10f * dt, 0f, 1f)); Rotation = Mathf.LerpAngle(Rotation, _netRot, Mathf.Clamp(10f * dt, 0f, 1f)); }
            return;
        }
        if (!Alive) return;
        var pilots = Pilots.ToList();
        if (pilots.Count > 0)
        {   // close on the party, ponderously
            var centre = pilots.Aggregate(Vector2.Zero, (s, p) => s + p.Position) / pilots.Count;
            float want = (centre - Position).Angle() + Mathf.Pi / 2f;
            Rotation += Mathf.Clamp(Mathf.AngleDifference(Rotation, want), -0.3f * dt, 0.3f * dt);
            float d = Position.DistanceTo(centre);
            if (d > 650f && !Charging) Position += (centre - Position).Normalized() * 30f * dt;
            Tick(pilots, delta);
        }
        _send -= delta;
        if (_send <= 0 && Net.IsOnline) { _send = 0.1; Rpc(nameof(NetState), Position, Rotation, Hp); }
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
        if (_beam <= 0 && _pendingBeam == null)
        {   // the death beam: telegraph first, then it fires down the same line
            _beam = BeamEvery;
            var t = pilots.OrderBy(p => p.Position.DistanceTo(Position)).First();
            var a = nose; var b = a + (t.Position - a).Normalized() * BeamLength;
            _pendingBeam = (a, b); _beamT = BeamWindup;
            Tele(true, a, b, BeamWidth, BeamWindup);
            LaunchEscorts(t);
        }
        if (_pendingBeam is { } beam && _beamLive < 0 && (_beamT -= delta) <= 0) { _beamLive = BeamLive; _beamTickT = 0; }
        if (_pendingBeam is { } live && _beamLive >= 0)
        {   // live for 1 s: a check every 0.51 s, 100 each time it lands
            _beamTickT -= delta;
            if (_beamTickT <= 0)
            {
                _beamTickT = BeamTick;
                foreach (var p in pilots)
                    if (DistToSegment(p.Position, live.a, live.b) <= BeamWidth / 2f + p.HitRadius) p.Hit(BeamDamage * DamageMult, Position, "boss:beam");
            }
            if ((_beamLive -= delta) < 0) _pendingBeam = null;
        }
        _charge -= delta;
        if (_charge <= 0 && _pendingCharge == null && !_dashTo.HasValue)
        {   // the charge: a red line first, then a ram down it
            _charge = BeamEvery;
            var t = pilots.OrderBy(p => p.Position.DistanceTo(Position)).First();
            var a = Position; var bb = a + (t.Position - a).Normalized() * ChargeLength;
            _pendingCharge = (a, bb); _chargeT = ChargeWindup;
            Tele(true, a, bb, HalfWidth * 2f, ChargeWindup);
        }
        if (_pendingCharge is { } ch && (_chargeT -= delta) <= 0) { _dashTo = ch.b; Rotation = (ch.b - ch.a).Angle() + Mathf.Pi / 2f; _pendingCharge = null; }
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

    // host: show a telegraph here and on every guest
    private void Tele(bool line, Vector2 a, Vector2 b, float size, double time)
    {
        ShowTelegraph(line, a, b, size, time);
        if (Net.IsOnline) Rpc(nameof(NetTelegraph), line, a, b, size, time);
    }
    private void ShowTelegraph(bool line, Vector2 a, Vector2 b, float size, double time) =>
        GetParent().AddChild(new Telegraph { Line = line, A = a, B = b, Width = size, Radius = size, Duration = time });

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetTelegraph(bool line, Vector2 a, Vector2 b, float size, double time) => ShowTelegraph(line, a, b, size, time);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void NetState(Vector2 p, float rot, double hp) { _netPos = p; _netRot = rot; _hasNet = true; Hp = hp; }

    public int TelegraphsPending => (_pendingBeam != null ? 1 : 0) + (_pendingWave != null ? 1 : 0);
}
