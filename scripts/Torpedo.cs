using Godot;
using System.Collections.Generic;

// A bomber's torpedo. It runs STRAIGHT at a steady speed along the heading it was
// launched on -- no tracking, no acceleration -- trailing smoke, and detonates on
// the first hostile it touches or when its run is spent. Aim is the bomber's job;
// a target that moves can step out of the way.
//
// The host's copy deals the damage. Guests are sent the launch (from, heading) and
// fly a cosmetic copy of their own: the run is deterministic, so it goes where the
// host's goes and bursts in the same place, without streaming positions.
// The battleship's missile is the same projectile with two differences: it is
// BARELY guided (its heading turns toward its target at no more than TurnRate), and
// it is HEAVY -- a slow bunker buster with a bigger body, darker smoke and a bigger
// blast. Guests run the same guidance on the same positions, so their cosmetic
// copy follows the host's.
public partial class Torpedo : Node2D, IHittable
{
    public Vector2 Dir;
    public float Speed, Range;
    public double Damage;
    public bool Cosmetic;             // a guest's copy: draws and bursts, never damages
    public int TargetId;              // 0 = unguided
    public float TurnRate;            // rad/s the heading may turn toward the target
    public bool Heavy;                // the bunker buster's look
    public bool HostileFire;          // fired by an enemy: seeks and hits player ships
    public PlayerShip Source;         // who fired it (host copy only): a hit counts as their combat

    private float _flown;
    private bool _spent;
    private double _burst;            // time since detonation, for the flash
    private readonly List<(Vector2 p, float age)> _smoke = new();
    private float _puffCd;

    public const float SmokeLife = 1.6f, PuffEvery = 0.03f;

    // As a TARGET (hostile missiles only): small, and one hit brings it down. The host
    // resolves the hit and tells every guest to burst its copy (by NetId).
    public int NetId { get; set; }
    public float HitRadius => 8f * Size;
    public string HitSource;          // the damage source's name (a player lands one hit per source per 0.35 s)
    public float Size = 1f;           // a boss's missiles are twice the size
    public bool Alive => !_spent;
    public bool Selectable => false;
    public static int Intercepted;                       // missiles shot down (host), for the record
    public void TakeDamage(double d)
    {
        if (!Net.Sim || _spent) return;
        Intercepted++;
        Intercept();
        Hub.I?.MissileDown(NetId);
    }
    public void Intercept() { if (!_spent) Detonate(); Combat.Hostiles.Remove(this); }

    public override void _Ready()
    {
        Sfx.Missile(GlobalPosition);                        // self-propelled: a soft whoosh at launch
        ZIndex = 6; Rotation = Dir.Angle() + Mathf.Pi / 2f;
        if (HostileFire && NetId != 0) Combat.Hostiles.Add(this);
    }
    public override void _ExitTree() => Combat.Hostiles.Remove(this);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _smoke.Count - 1; i >= 0; i--)
        {
            var s = _smoke[i]; s.age += dt;
            if (s.age >= SmokeLife) _smoke.RemoveAt(i); else _smoke[i] = s;
        }

        if (!_spent)
        {
            var tgt = TurnRate > 0 && TargetId != 0 ? (HostileFire ? Combat.PlayerById(TargetId) : Combat.ById(TargetId)) : null;
            if (tgt != null)
            {   // barely guided: the nose creeps toward the target, never snaps
                float want = (tgt.Position - GlobalPosition).Angle(), have = Dir.Angle();
                float turn = Mathf.Clamp(Mathf.AngleDifference(have, want), -TurnRate * dt, TurnRate * dt);
                Dir = Dir.Rotated(turn);
                Rotation = Dir.Angle() + Mathf.Pi / 2f;
            }
            float step = Speed * dt;
            GlobalPosition += Dir * step; _flown += step;
            _puffCd -= dt;
            if (_puffCd <= 0) { _puffCd += PuffEvery; _smoke.Add((GlobalPosition - Dir * 8f, 0f)); }

            foreach (var h in HostileFire ? Combat.Players : Combat.Hostiles)
            {
                if (h == null || !h.Alive || !h.Covers(GlobalPosition, 4f)) continue;
                if (!Cosmetic && Net.Sim)
                {   // a player ship is told where the hit came from, for its shield
                    if (h is PlayerShip ps) ps.Hit(Damage, GlobalPosition - Dir * 10f, HitSource); else h.TakeDamage(Damage);
                    if (IsInstanceValid(Source)) Source.NoteCombat();
                }
                Detonate(); break;
            }
            if (!_spent && _flown >= Range) Detonate();
        }
        else
        {
            _burst += delta;
            // linger until the last of the smoke has faded, then go
            if (_smoke.Count == 0 && _burst > 0.5) QueueFree();
        }
        QueueRedraw();
    }

    private void Detonate() { _spent = true; _burst = 0; Sfx.Impact(GlobalPosition); }

    public override void _Draw()
    {
        // smoke is stored in world space; draw it relative to this node
        var inv = GlobalTransform.AffineInverse();
        foreach (var (p, age) in _smoke)
        {
            float k = age / SmokeLife;
            // bright enough to read against black space; spreads as it fades
            var smoke = Heavy ? new Color(0.45f, 0.43f, 0.42f) : new Color(0.86f, 0.86f, 0.88f);
            DrawCircle(inv * p, (Heavy ? 5f : 3.5f) + (Heavy ? 14f : 10f) * k, new Color(smoke.R, smoke.G, smoke.B, 0.75f * (1f - k) * (1f - k * 0.3f)));
        }
        if (!_spent)
        {
            if (Heavy)
            {   // the bunker buster: long and slim (6.4 x 32.5), a sharp nose, swept fins, a band and a seam
                var body = new Color(0.58f, 0.60f, 0.56f); var dark = new Color(0.30f, 0.31f, 0.29f);
                DrawRect(new Rect2(-3.2f, -12f, 6.4f, 26f), body);
                DrawColoredPolygon(new[] { new Vector2(-3.2f, -12f), new Vector2(0, -19.5f), new Vector2(3.2f, -12f) }, new Color(0.85f, 0.25f, 0.2f));
                DrawRect(new Rect2(-3.2f, -10f, 6.4f, 2f), new Color(0.85f, 0.25f, 0.2f));
                DrawLine(new Vector2(0, -8f), new Vector2(0, 11f), dark, 0.8f);
                DrawColoredPolygon(new[] { new Vector2(-3.2f, 6f), new Vector2(-7f, 13f), new Vector2(-3.2f, 12f) }, dark);
                DrawColoredPolygon(new[] { new Vector2(3.2f, 6f), new Vector2(7f, 13f), new Vector2(3.2f, 12f) }, dark);
                DrawCircle(new Vector2(0, 13.5f), 2.8f, new Color(1f, 0.75f, 0.35f));
            }
            else if (Size != 1f)
            {
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One * Size);
                DrawRect(new Rect2(-2.2f, -9f, 4.4f, 18f), new Color(0.85f, 0.85f, 0.8f));
                DrawCircle(new Vector2(0, 9f), 2.4f, new Color(1f, 0.7f, 0.3f));
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                DrawRect(new Rect2(-2.2f, -9f, 4.4f, 18f), new Color(0.85f, 0.85f, 0.8f));
                DrawCircle(new Vector2(0, 9f), 2.4f, new Color(1f, 0.7f, 0.3f));
            }
        }
        else if (_burst < 0.5)
        {
            float k = (float)(_burst / 0.5), big = Heavy ? 2f : 1f;
            DrawCircle(Vector2.Zero, (10f + 30f * k) * big, new Color(1f, 0.7f, 0.3f, 0.8f * (1f - k)));
            if (Heavy) DrawArc(Vector2.Zero, 70f * k, 0, Mathf.Tau, 40, new Color(1f, 0.9f, 0.6f, 0.7f * (1f - k)), 3f);
        }
    }
}
