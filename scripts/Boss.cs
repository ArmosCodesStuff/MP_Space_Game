using Godot;
using System.Linq;

// THE SILVER LANCER -- the first bounty boss. Host-simulated; every peer draws it from
// the host's 10 Hz state and plays its telegraphs from the host's events.
//   GUNS       every 1.2 s: 6 damage at the nearest ship within 900 u
//   MISSILES   every 7 s: 4 guided missiles, 20 each -- INTERCEPTABLE (PD shoots them)
//   DEATH BEAM every 12 s: a red line telegraph for 2 s, then 60 to any ship in it
//   SHOCKWAVE  every 17 s: a red ring telegraph for 1.8 s, then 45 within 340 u
// Slow and heavy: it closes on the party to about 650 u and turns ponderously.
public partial class Boss : Node2D, IHittable
{
    public Hub Hub;
    // hull and every attack scale with the selected tier (Missions.Scale)
    public double Scale = 1;
    public double MaxHp => Missions.Current.Hull * Scale;
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

    public const double BeamWindup = 2.0, WaveWindup = 1.8;
    public const float BeamLength = 1800f, BeamWidth = 70f, WaveRadius = 340f;
    private double _guns = 2.0, _missiles = 4.0, _beam = 6.0, _wave = 10.0, _send;
    private (Vector2 a, Vector2 b)? _pendingBeam; private double _beamT;
    private Vector2? _pendingWave; private double _waveT;
    private Vector2 _netPos; private float _netRot; private bool _hasNet;

    public override void _Ready()
    {
        Scale = Missions.Scale(Missions.Tier); Hp = MaxHp;
        Name = "Boss";
        var tex = GD.Load<Texture2D>("res://boss_silver_lancer.png");
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
            if (d > 650f) Position += (centre - Position).Normalized() * 30f * dt;
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
        if (_guns <= 0) { _guns = 1.2; var t = Nearest(900f); if (t != null) { t.Hit(6 * Scale, Position); Combat.Flash(nose, t.Position, new Color(1f, 0.5f, 0.35f), boss: true); } }
        _missiles -= delta;
        if (_missiles <= 0)
        {
            _missiles = 7.0;
            for (int i = 0; i < 4; i++)
            {
                var t = pilots[i % pilots.Count];
                var dir = Vector2.Up.Rotated(Rotation + (i - 1.5f) * 0.35f);
                Combat.LaunchTorpedo(nose + dir * 10f, dir, 150f, 1600f, 20 * Scale, t.NetId, 1.4f, heavy: false, hostile: true);
            }
        }
        _beam -= delta;
        if (_beam <= 0 && _pendingBeam == null)
        {   // the death beam: telegraph first, then it fires down the same line
            _beam = 12.0;
            var t = pilots.OrderBy(p => p.Position.DistanceTo(Position)).First();
            var a = nose; var b = a + (t.Position - a).Normalized() * BeamLength;
            _pendingBeam = (a, b); _beamT = BeamWindup;
            Tele(true, a, b, BeamWidth, BeamWindup);
        }
        if (_pendingBeam is { } beam && (_beamT -= delta) <= 0)
        {
            foreach (var p in pilots)
                if (DistToSegment(p.Position, beam.a, beam.b) <= BeamWidth / 2f + p.HitRadius) p.Hit(60 * Scale, Position);
            _pendingBeam = null;
        }
        _wave -= delta;
        if (_wave <= 0 && _pendingWave == null)
        {
            _wave = 17.0; _pendingWave = Position; _waveT = WaveWindup;
            Tele(false, Position, Vector2.Zero, WaveRadius, WaveWindup);
        }
        if (_pendingWave is { } c && (_waveT -= delta) <= 0)
        {
            foreach (var p in pilots) if (p.Position.DistanceTo(c) <= WaveRadius + p.HitRadius) p.Hit(45 * Scale, c);
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
