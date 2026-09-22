using Godot;
using System;

// THE TITLE SCREEN'S PIRATES. Real targets, not decoration: they register in Combat.Hostiles,
// so the battleship's own turrets, shells and broadside find them through exactly the code that
// fights in the hub. Their own guns are cosmetic -- the display ship has no hull to lose.
//
// Three kinds, so the diorama reads as a fight rather than a swarm:
//   Light  fast, fragile, straight attack runs past the hull
//   Heavy  slower, tougher, bigger; it closes and stays, so there is always something to shoot
//   Web    a webifier: it holds off at range and paints the capital with a tether beam. That is
//          the mechanic the death beam's escorts use, shown on the title screen before you meet it
public enum MenuFoeKind { Light, Heavy, Web }

public partial class MenuFoe : Node2D, IHittable
{
    // One id space of its own: the menu is a world, but not the hub's world.
    private const int IdBase = 30000;
    private static int _next;

    public MenuFoeKind Kind;
    public Vector2 Velocity;
    public Vector2 Home;                 // the capital it is attacking
    public Node2D Target;                // the display ship: the webifier draws its tether to it
    private double _hp, _maxHp, _gun, _respawn, _weave;
    private bool _passing;
    // A child Sprite2D, NOT a Texture2D held in a C# field. A field keeps the managed wrapper
    // alive past the node, its native reference with it, and the engine reports "2 resources still
    // in use at exit" -- the two textures only this scene ever loaded. A sprite child owns its
    // texture and releases it with the node, which is also how everything else here draws.
    private Sprite2D _sprite;
    private readonly string _art;
    private readonly float _length;          // the size it IS, in world units -- not a scale factor
    private readonly Random _rng;

    public int NetId { get; } = IdBase + (++_next);
    public float HitRadius { get; private set; }
    public bool Alive { get; private set; } = true;
    public bool Webbing { get; private set; }     // drawing its tether this frame

    public MenuFoe(MenuFoeKind kind, Random rng)
    {
        Kind = kind; _rng = rng;
        // A WORLD LENGTH, NOT A SCALE FACTOR. Every other ship in the build declares how big it
        // is and derives the scale from the art (PlayerShip.FitClass, Raider); only these carried
        // hand-picked multipliers -- 0.55, 0.40, 0.42 -- which say nothing about how big the thing
        // is and stop being right the moment the art is recut.
        //
        // The lengths are the REAL raiders' lengths, so the title screen shows the enemies at the
        // size you meet them at, for the same reason it shows the ship at the game's own zoom.
        (string art, double hp, float len, float radius) = kind switch
        {
            MenuFoeKind.Heavy => ("res://enemy_heavy_hull.png",   26.0, Raider.HeavyLength, Raider.HeavyLength * 0.3f),
            MenuFoeKind.Web   => ("res://enemy_light_tier_2.png", 10.0, Raider.LightLength, Raider.LightLength * 0.4f),
            _                 => ("res://enemy_light_fighter.png", 6.0, Raider.LightLength, Raider.LightLength * 0.4f),
        };
        _art = art; _maxHp = _hp = hp; _length = len; HitRadius = radius;
    }

    public override void _Ready()
    {
        _sprite = Sprites.Fit(_art, _length);          // exactly how Raider and PlayerShip do it
        // The raiders wear their red, from Raider's own constants: untinted they drew in the art's
        // bare grey and read as neutral hulls rather than as something shooting at you.
        if (Kind != MenuFoeKind.Web) _sprite.Modulate = Kind == MenuFoeKind.Heavy ? Raider.HeavyTint : Raider.LightTint;
        AddChild(_sprite);
    }

    public void TakeDamage(double d)
    {
        if (!Alive) return;
        _hp -= d;
        if (_hp > 0) return;
        Alive = false; Webbing = false;
        _respawn = 2.0 + _rng.NextDouble() * 2.5;
        Died?.Invoke(GlobalPosition);
    }

    public event Action<Vector2> Died;            // the menu draws the burst

    private float Speed => Kind switch { MenuFoeKind.Heavy => 62f, MenuFoeKind.Web => 74f, _ => 118f };
    private float Standoff => Kind switch { MenuFoeKind.Heavy => 210f, MenuFoeKind.Web => 330f, _ => 0f };

    // Where a light fighter aims its run: PERPENDICULAR to the approach, so the line misses the
    // hull. A point on a ring around the capital does not: a straight line through one can still
    // go clean through the ship.
    private Vector2 RunAim()
    {
        var toward = (Home - GlobalPosition).Normalized();
        if (toward == Vector2.Zero) toward = Vector2.Right;
        var perp = new Vector2(-toward.Y, toward.X);
        float off = (130f + (float)_rng.NextDouble() * 130f) * (_rng.NextDouble() < 0.5 ? -1f : 1f);
        return Home + perp * off;
    }

    // Deployed IN the fight rather than flown in from the edge. The first frame of the title
    // screen has to read as a battle already in progress: a heavy that starts 1600 u out and
    // closes at 62 u/s is off screen for the first half minute, which is the whole time most
    // people look at a menu.
    public void Deploy(Vector2 home)
    {
        float a = (float)(_rng.NextDouble() * Math.PI * 2);
        float r = Standoff > 0f ? Standoff * (0.95f + (float)_rng.NextDouble() * 0.25f)
                                : 300f + (float)_rng.NextDouble() * 220f;
        Respawn(home, r);
    }

    public void Respawn(Vector2 home, float ringRadius)
    {
        Home = home;
        float a = (float)(_rng.NextDouble() * Math.PI * 2);
        GlobalPosition = home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringRadius;
        _hp = _maxHp; Alive = true; _passing = false; Webbing = false;
        _gun = _rng.NextDouble();
        Velocity = (RunAim() - GlobalPosition).Normalized() * Speed;
    }

    // Returns a shot to draw this frame, or null. The menu owns the drawing.
    public (Vector2 from, Vector2 to)? Tick(double delta, Vector2 home, float ringRadius)
    {
        Home = home;
        if (!Alive)
        {
            _respawn -= delta;
            if (_respawn <= 0) Respawn(home, ringRadius);
            return null;
        }
        float d = GlobalPosition.DistanceTo(home);
        Webbing = false;

        if (Standoff > 0f)
        {
            // Heavies and webifiers HOLD a distance and circle, so the scene always has something
            // in frame. A light fighter that flies past is gone for seconds at a time.
            _weave += delta;
            var inward = (home - GlobalPosition).Normalized();
            var perp = new Vector2(-inward.Y, inward.X) * (Kind == MenuFoeKind.Web ? 0.75f : 0.5f);
            float pull = Mathf.Clamp((d - Standoff) / 160f, -1f, 1f);
            var want = (inward * pull + perp * Mathf.Sin((float)_weave * 0.5f)).Normalized() * Speed;
            Velocity = Velocity.MoveToward(want, 90f * (float)delta);
        }
        else
        {
            if (!_passing && d < 250f) _passing = true;                  // committed: fly the run through
            else if (_passing && d > 680f)
            {
                Velocity = (RunAim() - GlobalPosition).Normalized() * Speed;
                _passing = false;
            }
        }
        GlobalPosition += Velocity * (float)delta;
        Rotation = Velocity.Angle() + Mathf.Pi / 2f;

        // The webifier does not shoot: it TETHERS, continuously, whenever it is in range. That is
        // what makes it read as a different threat from the ones firing tracers.
        if (Kind == MenuFoeKind.Web)
        {
            Webbing = d < 420f;
            return null;
        }
        _gun -= delta;
        float range = Kind == MenuFoeKind.Heavy ? 340f : 560f;
        if (_gun > 0 || d > range) return null;
        _gun = (Kind == MenuFoeKind.Heavy ? 0.85 : 1.15) * (0.7 + _rng.NextDouble() * 0.6);
        var scatter = new Vector2((float)(_rng.NextDouble() - 0.5) * 34f, (float)(_rng.NextDouble() - 0.5) * 34f);
        return (GlobalPosition, home + scatter);
    }

    public override void _Draw()
    {
        // THE TETHER IS DRAWN BY THE FOE, in the foe's own space, converting the ship's position
        // with ToLocal. Drawn from the menu instead it came out in a different space and ended
        // hundreds of units short of the hull -- a beam that misses what it is tethering reads as
        // a bug in the beam, not in whose _Draw it sits in.
        if (Webbing && Target != null && GodotObject.IsInstanceValid(Target))
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.GetTicksMsec() / 110f);
            DrawLine(Vector2.Zero, ToLocal(Target.GlobalPosition),
                     new Color(0.65f, 0.35f, 1f, 0.35f + 0.3f * pulse), 2.6f);
        }
        if (!Alive) return;
        if (_hp < _maxHp)
            HealthBar.Draw(this, new Vector2(-22, -HitRadius - 14), 44, 4, _hp, _maxHp, Ui.Bad);
    }

    public override void _Process(double delta)
    {
        if (_sprite != null) _sprite.Visible = Alive;
        QueueRedraw();
    }

    // Drop the texture reference deterministically. These two sprites are the only things in the
    // build that load enemy_heavy_hull and enemy_light_tier_2 during a solo run, so if their
    // reference outlives the scene the engine counts exactly two resources still in use at exit.
    public override void _ExitTree()
    {
        Died = null;
        if (GodotObject.IsInstanceValid(_sprite)) _sprite.Texture = null;
        Target = null;
    }
}
