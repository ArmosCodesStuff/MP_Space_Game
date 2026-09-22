using Godot;
using System.Collections.Generic;
using System.Linq;

// THE SILVER LANCER -- the bounty boss of the odd levels (Missions.ForLevel).
// One rhythm, 30 s long (all damage x the level's and party's scale):
//   GUNS       always: 3.6 every 1.2 s (3 DPS) at the nearest ship within 900 u
//   DEATH BEAM every 30 s, in three steps, the boss holding position through all of them:
//              ESCORTS OUT: two escorts go for the nearest pilot to web it; the boss turns to
//                face that pilot -- the only turning the beam allows, and only now.
//              CHARGE: once the web has actually PINNED the pilot (this cycle's escorts under way;
//                or, the escorts all down, once their web would have landed; or when they are
//                overdue), a red line down the nose
//                for 6 s. From here to the beam's end it is HARD LOCKED: no turn, no move -- the
//                line shown is the line fired, and the way out is to leave it.
//              LIVE: 3 s along that line, checking every 0.25 s: 50 each time it lands on a ship
//                (a ship's 0.52 s invulnerability to one source means a hit about every 0.75 s)
//   CHARGE     15 s after each beam (never during one): a red line for 1.5 s, then a ram along it
//              at 1200 u/s: 40 to any ship in its path -- the one special that moves it
//   TRIDENT    between them (every 15 s from 13.5 s): 3 guided missiles, 0 and +-25
//              degrees, 15 each, twice the size -- INTERCEPTABLE (PD shoots them)
//   SHOCKWAVE  every 17 s: a red ring for 1.8 s, held still, then 45 within 340 u
// Slow and heavy: between its specials it closes on the party to about 650 u and turns ponderously.
public partial class Lancer : Boss
{
    public const float HullLength = 360f, HullHalfWidth = 70f;
    public override float Length => HullLength;
    public override float HalfWidth => HullHalfWidth;
    protected override string Sprite => "res://boss_raider.png";      // raider red, a white skull on its centre

    public const double BeamWindup = 6.0, WaveWindup = 1.8;
    // As the beam opens, the boss launches two light ESCORTS at its target -- one 45 degrees
    // to port, one to starboard -- boosting all the way in to pin the pilot in the beam. Fragile
    // on purpose (point defence, 1 DPS a turret, must be able to kill them inside the charge).
    private const float EscortAngle = 45f;
    public const double EscortHull = 3;           // 3 s at one PD turret (1 DPS): killable inside the charge
    private const double WebToBeam = 1.0;          // with every escort down, the charge waits this long past their predicted web
    // Escorts that neither pin nor die (a pilot outrunning them) cannot hold the beam off: it
    // charges this long after they launch at the latest. Under 6 s so the whole beam -- this, the
    // 6 s charge and 3 s live -- ends inside the 15 s before the ram.
    private const double BeamArmMax = 5.0;
    // The beam reaches right across the arena: once it is charging, distance is no escape --
    // only angle is.
    public const float BeamLength = 10000f, BeamWidth = 70f, WaveRadius = 340f;
    public const double GunDamage = 3.6, GunEvery = 1.2;                       // 3 DPS
    public const double BeamEvery = 30, BeamLive = 3.0, BeamTick = 0.25, BeamDamage = 50;
    public const double ChargeWindup = 1.5, ChargeDamage = 40; public const float ChargeSpeed = 1200f, ChargeLength = 900f;
    public const double TridentEvery = 15, TridentDamage = 15; public const float TridentSpread = 25f, TridentSpeed = 135f, TridentRange = 1920f;
    private double _guns = 2.0, _missiles = 13.5, _beam = 6.0, _charge = 21.0, _wave = 10.0;
    private double _beamLive = -1, _beamTickT, _beamT;
    private (Vector2 a, Vector2 b)? _pendingCharge; private double _chargeT; private Vector2? _dashTo;
    private Vector2? _pendingWave; private double _waveT;
    public int Volleys { get; private set; }                                   // for the smoke test
    public bool Charging => _dashTo.HasValue;
    public bool BeamCharging { get; private set; }

    // The escorts' phase: >= 0 while they are out, counting the seconds since they launched.
    private double _beamArm = -1, _beamPredict, _beamOverdue;
    private readonly List<Raider> _escorts = new();
    private PlayerShip _beamTarget;
    public bool EscortsOut => _beamArm >= 0;
    // How the last charge started, and how long after the escorts launched (for the smoke test).
    public string ChargeCause { get; private set; } = "";
    public double ArmedFor { get; private set; }
    private bool BeamBusy => _beamArm >= 0 || BeamCharging || _beamLive >= 0;

    // Frozen: a super move is winding up or firing. The escorts' phase counts: from the moment they
    // launch the boss holds its position, and turns only as the beam's own aim decides.
    public override bool Locked => BeamBusy || _pendingCharge != null || _dashTo.HasValue || _pendingWave != null;
    protected override double NextSuperHost => System.Math.Min(_beam, _charge);
    public override int TelegraphsPending => (BeamCharging ? 1 : 0) + (_pendingWave != null ? 1 : 0);

    // Where the beam goes RIGHT NOW: straight out of the nose. The telegraph is a child of the
    // boss drawn down the same axis, so the drawing and the hit are the same line by construction.
    public (Vector2 a, Vector2 b) BeamSegment()
    {
        var nose = ToGlobal(new Vector2(0, -Length * 0.5f));
        return (nose, nose + Vector2.Up.Rotated(Rotation) * BeamLength);
    }
    // The beam's escorts: port and starboard of the nose, straight at the pilot. Returns roughly
    // how long until their web should land -- the charge's floor if they are all shot down first.
    public double LaunchEscorts(Node2D target)
    {
        if (!Net.Sim || target == null || Hub == null) return Raider.EscortShiver;
        var nose = Vector2.Up.Rotated(Rotation);
        double eta = 0; bool port = true;
        _escorts.Clear();
        foreach (float side in new[] { -EscortAngle, EscortAngle })
        {
            var dir = nose.Rotated(Mathf.DegToRad(side));
            var at = Position + dir * (HalfWidth + 60f);
            var r = Hub.SpawnRaider(at, RaiderKind.Light, 0, Missions.S(Missions.Level));
            // the one launched to port flanks to port, the other to starboard
            r?.Escort(target, dir, BeamWindup, EscortHull * Missions.S(Missions.Level), port ? -Mathf.Pi / 2f : Mathf.Pi / 2f);
            if (r != null) _escorts.Add(r);
            eta = System.Math.Max(eta, Raider.WebEta(at.DistanceTo(target.Position)));
            port = false;
        }
        return eta;
    }

    protected override void Tick(List<PlayerShip> pilots, double delta)
    {
        var nose = ToGlobal(new Vector2(0, -Length * 0.5f));
        PlayerShip Nearest(float within = float.MaxValue) => Combat.Nearest(pilots, Position, p => p.Position, within);
        _guns -= delta;
        if (_guns <= 0) { _guns = GunEvery; var t = Nearest(900f); if (t != null) { t.Hit(GunDamage * DamageMult, Position, "boss:guns"); Combat.Flash(nose, t.Position, new Color(1f, 0.5f, 0.35f), ShotSound.Boss); } }
        _missiles -= delta;
        if (_missiles <= 0)
        {   // a trident at the nearest ship: straight at it and 25 degrees either side
            _missiles = TridentEvery; Volleys++;
            var t = Nearest();
            var aim = (t.Position - nose).Normalized();
            foreach (float deg in new[] { -TridentSpread, 0f, TridentSpread })
            {
                var dir = aim.Rotated(Mathf.DegToRad(deg));
                Combat.LaunchTorpedo(nose + dir * 14f, dir, TridentSpeed, TridentRange, TridentDamage * DamageMult, t.NetId, 1.4f,
                                     heavy: false, hostile: true, hitSource: "boss:missiles", size: 2f);
            }
        }
        _beam -= delta;
        if (_beam <= 0 && !BeamBusy)
        {   // the death beam OPENS WITH ITS ESCORTS
            _beam = BeamEvery; SuperGap = System.Math.Min(_beam, _charge);
            _beamTarget = Combat.Nearest(pilots, Position, p => p.Position);
            double eta = LaunchEscorts(_beamTarget);
            _beamArm = 0; _beamPredict = eta + WebToBeam; _beamOverdue = System.Math.Min(_beamPredict + 1.0, BeamArmMax);
        }
        if (_beamArm >= 0)
        {   // ESCORTS OUT: held in place; turning to face the pilot is all it does
            _beamArm += delta;
            if (!IsInstanceValid(_beamTarget) || !_beamTarget.Alive) _beamTarget = pilots.FirstOrDefault();
            if (_beamTarget != null)
                Rotation = Mathf.RotateToward(Rotation, (_beamTarget.Position - Position).Angle() + Mathf.Pi / 2f, TurnRate * (float)delta);
            // A pin counts once this cycle's escorts have left the launch point: a pilot still held by
            // the LAST beam's escorts (they never expire) would otherwise skip this whole phase.
            bool pinned = _beamArm >= Raider.EscortShiver && _beamTarget != null && _beamTarget.Pinned;
            bool escortsDown = !_escorts.Any(r => IsInstanceValid(r) && r.Alive);
            if (pinned || (escortsDown && _beamArm >= _beamPredict) || _beamArm >= _beamOverdue)
            {   // THE CHARGE, hard locked from here to the beam's end; the telegraph rides the hull,
                // so the line cannot lie
                ChargeCause = pinned ? "pinned" : _beamArm >= _beamOverdue ? "overdue" : "escorts down";
                ArmedFor = _beamArm;
                _beamArm = -1; BeamCharging = true; _beamT = BeamWindup;
                Tele(true, new Vector2(0, -Length * 0.5f), new Vector2(0, -Length * 0.5f - BeamLength), BeamWidth, BeamWindup, onHull: true, hold: BeamLive);
            }
        }
        if (BeamCharging && (_beamT -= delta) <= 0) { BeamCharging = false; _beamLive = BeamLive; _beamTickT = 0; }
        if (_beamLive >= 0)
        {   // live: a check every 0.25 s, down the nose -- which has not moved since the charge began
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
        if (_charge <= 0 && _pendingCharge == null && !_dashTo.HasValue && !BeamBusy && _pendingWave == null)
        {   // the ram: come round onto the pilot, then hold dead still for the wind-up so the
            // line drawn is the line rammed down. Never during a beam or a shockwave's wind-up (which
            // hold the boss still): it waits for them to end.
            _charge = BeamEvery; SuperGap = System.Math.Min(_beam, _charge);
            var t = Combat.Nearest(pilots, Position, p => p.Position);
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
        if (_wave <= 0 && _pendingWave == null && _pendingCharge == null && !_dashTo.HasValue)
        {   // (not while it rams: the ring is centred on the boss, and it would ram out of its own ring)
            _wave = 17.0; _pendingWave = Position; _waveT = WaveWindup;
            Tele(false, Position, Vector2.Zero, WaveRadius, WaveWindup);
        }
        if (_pendingWave is { } c && (_waveT -= delta) <= 0)
        {
            foreach (var p in pilots) if (p.Position.DistanceTo(c) <= WaveRadius + p.HitRadius) p.Hit(45 * DamageMult, c, "boss:wave");
            _pendingWave = null;
        }
    }
}
