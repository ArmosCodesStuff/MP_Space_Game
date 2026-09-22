using Godot;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// A TURRET LEFT BEHIND. The freighters drop these (T) and pick them up again (C, sitting over
// one). It does not fly: it holds the spot it was left on and shoots what comes near with the
// same component a warship's point defence uses, so there is one acquisition rule, one swing and
// one reload in the game rather than a second copy for structures.
//
// Its gun is its OWNER's sheet (deploy_damage, deploy_interval, deploy_range), read every tick --
// so a level bought while three are out improves all three -- and its owner's overdrive doubles
// their rate of fire with its own.
//
// HOST-OWNED. The host drops it, tells every guest (Hub.NetDeploy), and only the host's copy
// deals damage or loses hull; a guest's copy tracks and draws. Raiders will go for it like any
// other thing the base owns (Hub.RaiderTargets), which is the point of leaving one somewhere.
// ─────────────────────────────────────────────────────────────────────────────
public partial class DeployedTurret : Node2D, IRaidTarget, ITagged, ITurretHost
{
    public PlayerShip Ship;               // the pilot who dropped it; null on a guest whose ship has not arrived
    public int OwnerId;
    public int NetId;
    public double Hp, MaxHp;
    public bool Alive => Hp > 0;
    public Tag Tags => Tag.Structure;

    // the gun, when there is no owner to ask (a guest's copy before its ship arrives)
    public const double SpareDamage = 6, SpareInterval = 0.5;
    public const float SpareRange = 500f, Radius = 20f;

    private Turret _gun;
    private readonly List<Turret> _mounts = new();
    private StatusSet _status;

    public Node2D AsNode => this;
    public bool PdOnline => Alive;                 // no window: it fires whenever it is standing
    public float PdRing => 0f;
    public Vector2 AimAt => Position;
    public float FastSwing => 0f;
    public IReadOnlyList<Turret> Siblings => _mounts;
    public void NoteDealt(double d, Vector2 at) => Ship?.NoteDealt(d, at);
    public PlayerShip Credit => Ship;

    public TurretSpec Spec(bool pd) => new()
    {
        Damage   = Ship != null ? Ship.Stats["deploy_damage"] : SpareDamage,
        Interval = (Ship != null ? Ship.Stats["deploy_interval"] : SpareInterval) / (Ship != null ? Ship.FireRate : 1),
        Range    = Ship != null ? (float)Ship.Stats["deploy_range"] : SpareRange,
        Turn     = Mathf.Tau / 2f,
        Texture  = "res://turret_deploy.png",
        TexScale = 0.16f, Barrel = 20f, Ring = 0f,
        Tint     = Ship?.Accent ?? new Color(0.8f, 0.82f, 0.86f),
    };

    // ── as something a raider can go after ──────────────────────────────────
    public bool InReach => Alive;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status s, double seconds) { if (Net.Sim) _status.Apply(s, seconds); }
    public (float halfLength, float halfWidth) Extent => (Radius, Radius);
    public void Hit(double d, Vector2 from, string source) => TakeDamage(d);
    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive) return;
        DamageNumbers.NoteImpact(this, Position);
        Hp -= d;
        if (Hp <= 0) (GetParent() as Hub)?.DeployedDown(this);
    }

    public override void _Ready()
    {
        ZIndex = 3;
        _gun = new Turret();
        AddChild(_gun);
        _gun.Setup(this, Vector2.Zero, pd: true);
        _mounts.Add(_gun);
    }

    public override void _Process(double delta)
    {
        // its pilot may have arrived after it did (a guest joining mid-fight)
        if (Ship == null || !IsInstanceValid(Ship)) Ship = (GetParent() as Hub)?.ShipOf(OwnerId);
        if (Net.Sim) _status.Tick(delta);
        _gun.Tick(delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // a low plated base under the gun, in the owner's hull colour, and what is left of it
        var hull = Ship?.Main ?? new Color(0.55f, 0.58f, 0.62f);
        DrawCircle(Vector2.Zero, Radius, hull with { A = 0.85f });
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 24, new Color(0.1f, 0.12f, 0.15f, 0.9f), 2f);
        if (MaxHp > 0 && Hp < MaxHp)
        {
            float w = Radius * 2f, f = (float)Mathf.Clamp(Hp / MaxHp, 0, 1);
            DrawRect(new Rect2(-Radius, -Radius - 8f, w, 3f), new Color(0, 0, 0, 0.55f));
            DrawRect(new Rect2(-Radius, -Radius - 8f, w * f, 3f), new Color(0.4f, 0.85f, 0.45f));
        }
    }
}
