using Godot;
using System.Collections.Generic;

// THE DRAKE BASTION -- the bounty boss of the even levels (Missions.ForLevel). A heavy scrapper: no
// escorts, a lower, steadier pressure than the Lancer, and two specials on the Lancer's own clocks that
// each hit 25% harder with 25% more warning (ThreatMult), all damage x the level's and party's scale:
//   MAIN GUN       always: a slow shell (220 u/s) every 2.5 s at the nearest ship within 1100 u,
//                  6 each (2.4 DPS) -- straight, and dodged by moving
//   SCRAP SHOTGUN  every 15 s (the trident's cadence), from 17 s: it WARPS to range -- a ring
//                  where it will land, 1 s -- 600 u from the nearest pilot, then a fan of seven red
//                  lines for 1.875 s (the Lancer's 1.5 s ram warning, +25%), then seven pieces of
//                  scrap down them at once -- the same fixed fan every time, 10 degrees apart --
//                  18.75 each (a trident missile's 15, +25%), each its own hit
//   ASTEROID THROW every 30 s (the death beam's slot, from 6 s): a big rock appears beside it and its
//                  tractor beam takes hold; a red lane to the target for 7.5 s (the beam's 6 s
//                  charge, +25%); then the rock is hurled down it -- slow to start, then very fast --
//                  striking every ship in the lane for 250 (the least a pilot standing in the
//                  Lancer's beam takes, 4 ticks of 50, +25%), and breaking apart at the lane's end
// It holds still through its specials -- the warp is the shotgun's own move -- and between them
// closes on the party like any boss. The two never run back to back: a throw holds it 9.1 s (7.5 s
// of warning, 1.6 s of flight), so the shotgun comes 11 s after each throw starts (and 4 s after it,
// in the cycle's other half), never on the trident's own 13.5 s, which fell inside the first throw.
public partial class Drake : Boss
{
    public const float HullLength = 420f, HullHalfWidth = 90f;
    public override float Length => HullLength;
    public override float HalfWidth => HullHalfWidth;
    protected override string Sprite => "res://boss_drake.png";

    public const double ThreatMult = 1.25;          // its specials against the Lancer's: damage, and warning
    public const double GunDamage = 6, GunEvery = 2.5;                                      // 2.4 DPS
    public const float GunSpeed = 220f, GunRange = 1300f, GunReach = 1100f, GunRadius = 7f;
    public const double ShotgunEvery = 15, WarpWarning = 1.0;
    public const float WarpStandoff = 600f;
    public const int ScrapPieces = 7;
    public const float ScrapStep = 10f, ScrapSpeed = 480f, ScrapRange = 1400f, ScrapRadius = 9f;
    public const double FanWindup = ThreatMult * Lancer.ChargeWindup, ScrapDamage = ThreatMult * Lancer.TridentDamage;
    public const double ThrowEvery = 30, ThrowWindup = ThreatMult * Lancer.BeamWindup, ThrowFlight = 1.6;
    public const double RockDamage = ThreatMult * 4 * Lancer.BeamDamage;
    public const float ThrowLength = 1800f, RockRadius = 90f;

    private double _gun = 2.0, _shotgun = 17.0, _throw = 6.0;
    private double _warpT = -1, _fanT = -1;                // >= 0: the warp's warning, the fan's wind-up
    private Vector2 _warpTo; private PlayerShip _shotTarget; private float _fanAim;
    private ThrownRock _rock;
    public int Volleys { get; private set; }               // for the smoke test: shotguns fired
    public int Throws { get; private set; }                // ...and rocks thrown
    public bool Warping => _warpT >= 0;
    public bool FanWinding => _fanT >= 0;
    public ThrownRock Rock => IsInstanceValid(_rock) ? _rock : null;
    private bool Throwing => Rock is { Done: false };
    private bool Busy => Warping || FanWinding || Throwing;

    public override bool Locked => Busy;
    protected override double NextSuperHost => System.Math.Min(_shotgun, _throw);
    public override int TelegraphsPending => (Warping ? 1 : 0) + (FanWinding ? 1 : 0) + (Rock is { Thrown: false } ? 1 : 0);

    private Vector2 Nose => ToGlobal(new Vector2(0, -Length * 0.5f));
    // the fan's seven directions, fixed: the aim, and 10, 20 and 30 degrees either side
    public static IEnumerable<float> FanAngles(float aim)
    {
        for (int k = 0; k < ScrapPieces; k++) yield return aim + Mathf.DegToRad((k - (ScrapPieces - 1) / 2) * ScrapStep);
    }

    protected override void Tick(List<PlayerShip> pilots, double delta)
    {
        PlayerShip Nearest(float within = float.MaxValue) => Combat.Nearest(pilots, Position, p => p.Position, within);
        _gun -= delta;
        if (_gun <= 0)
        {
            _gun = GunEvery;
            var t = Nearest(GunReach);
            if (t != null) Combat.FireSlug(Nose, t.Position - Nose, GunSpeed, GunRange, GunRadius, GunDamage * DamageMult, Slug.Kind.Shell, 0, "boss:gun");
        }

        // THE SCRAP SHOTGUN: warp to range, the fan, then the scrap
        _shotgun -= delta;
        if (_shotgun <= 0 && !Busy)
        {
            _shotgun = ShotgunEvery; SuperGap = System.Math.Min(_shotgun, _throw); Volleys++;
            _shotTarget = Nearest();
            _warpTo = _shotTarget.Position + (Position - _shotTarget.Position).Normalized() * WarpStandoff;
            _warpT = WarpWarning;
            Tele(false, _warpTo, Vector2.Zero, HalfWidth * 1.3f, WarpWarning);
        }
        if (_warpT >= 0 && (_warpT -= delta) <= 0)
        {   // it lands, facing the pilot, and the fan is drawn from its nose -- fixed from here on
            _warpT = -1;
            Position = _warpTo;
            if (!IsInstanceValid(_shotTarget) || !_shotTarget.Alive) _shotTarget = Nearest();
            if (_shotTarget != null) Rotation = (_shotTarget.Position - Position).Angle() + Mathf.Pi / 2f;
            _fanAim = Rotation - Mathf.Pi / 2f;
            foreach (float a in FanAngles(_fanAim))
                Tele(true, Nose, Nose + Vector2.Right.Rotated(a) * ScrapRange, ScrapRadius * 2f + 10f, FanWindup);
            _fanT = FanWindup;
        }
        if (_fanT >= 0 && (_fanT -= delta) <= 0)
        {
            _fanT = -1; int k = 0;
            foreach (float a in FanAngles(_fanAim))
            {
                Combat.FireSlug(Nose, Vector2.Right.Rotated(a), ScrapSpeed, ScrapRange, ScrapRadius, ScrapDamage * DamageMult, Slug.Kind.Scrap, k, $"boss:scrap:{k}");
                k++;
            }
        }

        // THE ASTEROID THROW: the rock beside it, the tractor and the lane, then the throw
        _throw -= delta;
        if (_throw <= 0 && !Busy)
        {
            _throw = ThrowEvery; SuperGap = System.Math.Min(_shotgun, _throw); Throws++;
            var t = Nearest();
            var side = Vector2.Right.Rotated(Rotation);
            if (side.Dot(t.Position - Position) < 0) side = -side;              // on the flank toward the pilot
            var from = Position + side * (HalfWidth + RockRadius + 40f);
            var to = from + (t.Position - from).Normalized() * ThrowLength;
            int variant = Throws % 2;
            Tele(true, from, to, RockRadius * 2f, ThrowWindup, hold: ThrowFlight);
            _rock = new ThrownRock { Boss = this, From = from, To = to, Hold = ThrowWindup, Flight = ThrowFlight,
                                     Damage = RockDamage * DamageMult, Radius = RockRadius, Variant = variant };
            GetParent().AddChild(_rock);
            if (Net.IsOnline) Hub?.RpcToSector(Hub.SectorKind.Arena, this, nameof(NetRock), from, to, ThrowWindup, ThrowFlight, RockRadius, variant);
        }
    }

    // A guest's rock: the same one, from the same event -- its hold shortened by the trip here, never
    // its flight (see ThrownRock).
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetRock(Vector2 from, Vector2 to, double hold, double flight, float radius, int variant) =>
        GetParent().AddChild(_rock = new ThrownRock { Boss = this, From = from, To = to, Hold = Net.Arriving(hold), Flight = flight,
                                                      Radius = radius, Variant = variant, Cosmetic = true });
}
