using Godot;
using System;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// AN EMPLACEMENT — a HOSTILE hull that does not move, shoots back, and may stand behind a shield
// something else is holding up. One row per kind of hull; one class that is every row; one Post[]
// per SITE those hulls are built on.
//
// WHY NOT DeployedTurret (Deployed.cs). It is the same SHAPE -- a hull that holds a spot and mounts
// a Turret -- and it is the wrong SIDE of the fight in five separate ways: it is not IHittable, so
// nothing the party fires can find it and no click can select it; it is in no Combat.Hostiles; its
// gun is read off its owner pilot's stat sheet every tick, and a pirate base has no pilot; it is
// drawn as a plated circle with no art and no shield; and raiders come FOR it (Hub.RaiderTargets).
// What the two DO share is already shared and is not copied here: ITurretHost and Turret (one gun,
// one swing, one reload), Spawns.All (one host-spawned replicated mechanic), StatusSet, HullWatch
// and Popups.
//
// A NEW ROW MUST FILL IN: its id, what a scope and the HUD call it, its HULL AT A LEVEL (a Func,
// so a row may be measured against the level's boss or state a flat figure -- one field, one
// path), its art and the length that art is drawn at, the two colours the grey art is tinted in at
// draw time (the sprites are grey on transparent and are NEVER recoloured in the file, exactly as
// a raider's are -- Raider._Ready), the row whose live ones hold ITS shield up (null: it has none),
// and the gun it answers with (a TurretSpec, like every other gun in the game).
//
// A NEW SITE is a Post[] -- a row of All, and where it stands from Hub.ArenaCentre -- plus the one
// mission row that names it (Missions.Kinds[].Build). No code is edited for either.
//
// AUTHORITY: the host alone raises a site, damages a hull and drops one. A guest builds its copies
// from Hub.NetSpawn, takes their hulls from Hub.NetHulls, and works the SHIELD out for itself --
// it is "anything of the shielding row still standing", and a guest holds the same list the host
// holds, so there is nothing to put on the wire for it.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class EmplacementDef
{
    public string Id;                           // reached by id, never by type
    public string Label;                        // what the scope, the HUD and a target line call it
    public Func<int, double> Hull;              // its hull at level L, before Missions.HullMult
    public string Sprite;
    public float Length, HalfWidth;             // drawn nose to tail, and the hull's half width
    public Color Main, Trim;                    // the grey art tinted, and what is drawn over it
    public string Shields;                      // the row whose live ones hold its shield up
    public TurretSpec Gun;                      // what it answers with, before Missions.DamageMult
    public int Guns = 1;                        // how many mounts...
    public float GunRing;                       // ...and how far off the centre they sit
}

// WHERE ONE HULL OF A SITE STANDS: a row of Emplacements.All, and its spot from Hub.ArenaCentre.
public readonly struct Post
{
    public readonly string Row;
    public readonly Vector2 At;
    public Post(string row, Vector2 at) { Row = row; At = at; }
}

public static class Emplacements
{
    public const string Base = "pirate_base", Pylon = "pirate_pylon";

    public static readonly EmplacementDef[] All =
    {
        // THE PIRATE BASE. Twice the hull of the boss that holds the SAME LEVEL -- read off
        // Missions.ForLevel, so there is one ladder and not a second one of its own -- and nothing
        // may touch it while a pylon stands.
        new() { Id = Base, Label = "PIRATE BASE", Hull = l => 2 * Missions.ForLevel(l).Hull,
                Sprite = "res://pirate_base.png", Length = 560f, HalfWidth = 330f,
                Main = new Color(0.72f, 0.20f, 0.17f), Trim = new Color(0.08f, 0.08f, 0.10f),
                Shields = Pylon, Guns = 4, GunRing = 210f,
                Gun = new TurretSpec { Kind = Shots.Slug, Damage = 18, Interval = 2.2, Range = 2200f,
                                       Turn = Mathf.Tau / 6f, ShellSpeed = 900f,
                                       Texture = "res://turret_main.png", TexScale = 0.26f, Barrel = 34f, Ring = 0f,
                                       Tint = new Color(0.90f, 0.40f, 0.34f), Source = DamageSource.BaseGun } },

        // ITS FOUR SHIELD PYLONS. 400 hull to start, on the same ladder above that; nothing shields
        // THEM, and nothing ends the mission when one falls -- they are only what has to go first.
        new() { Id = Pylon, Label = "SHIELD PYLON", Hull = _ => 400,
                Sprite = "res://pirate_pylon.png", Length = 220f, HalfWidth = 150f,
                Main = new Color(0.68f, 0.20f, 0.20f), Trim = new Color(0.08f, 0.08f, 0.10f),
                Guns = 1, GunRing = 0f,
                Gun = new TurretSpec { Kind = Shots.Slug, Damage = 9, Interval = 2.6, Range = 1400f,
                                       Turn = Mathf.Tau / 5f, ShellSpeed = 820f,
                                       Texture = "res://turret_pd.png", TexScale = 0.20f, Barrel = 20f, Ring = 0f,
                                       Tint = new Color(0.90f, 0.40f, 0.34f), Source = DamageSource.PylonGun } },
    };

    public static int RowOf(string id)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
        return 0;
    }
    public static EmplacementDef Of(int row) => All[row >= 0 && row < All.Length ? row : 0];

    // THE PIRATE BASE SITE: four pylons at the ends of an invisible X, 1200 u out on each diagonal
    // (the same X the four outposts stand on at home, Hub.Outposts), and the base in the middle.
    public const float PylonOut = 1200f;
    public static readonly Post[] PirateBase =
    {
        new(Base,  Vector2.Zero),
        new(Pylon, new Vector2( 1f, -1f).Normalized() * PylonOut),
        new(Pylon, new Vector2(-1f, -1f).Normalized() * PylonOut),
        new(Pylon, new Vector2( 1f,  1f).Normalized() * PylonOut),
        new(Pylon, new Vector2(-1f,  1f).Normalized() * PylonOut),
    };

    // HOST: raise a site. Every hull goes up through Hub.Spawn, so each is minted, replicated and
    // caught up by the ONE door that every host-spawned thing uses (Spawned.cs).
    public static void Raise(Hub hub, Post[] site)
    {
        if (!Net.IsHost || hub == null) return;
        int level = Missions.Level, party = Math.Max(1, hub.PartySize);
        foreach (var p in site)
        {
            int row = RowOf(p.Row);
            double hp = Of(row).Hull(level) * Missions.HullMult(level, party);
            hub.Spawn(Spawns.Emplacement, Hub.ArenaCentre + p.At, row, hp, hp);
        }
    }
}

// ONE HULL OF A SITE. Everything about it that is not behaviour is its row's.
public partial class Emplacement : Node2D, IQuarry, ITagged, IStatused, ITurretHost
{
    public Hub Hub;
    public int Kind;                                  // which row of Emplacements.All
    public int NetId { get; set; }
    public double Hp { get; set; }
    public double MaxHp { get; set; }
    public EmplacementDef Def => Emplacements.Of(Kind);
    public bool Alive => Hp > 0;
    public Tag Tags => Tag.Structure;
    public string Title => Def.Label;
    public double SuperFill => -1;              // no wind-up: a base does not charge, it stands
    public string Label => Def.Label;
    public float HitRadius => Def.HalfWidth;
    public bool Covers(Vector2 p, float pad) => Combat.KeelCovers(this, Def.Length, Def.HalfWidth, p, pad);

    private StatusSet _status;
    public StatusSet Statuses => _status;
    public void ApplyStatus(Status s, double seconds) { if (Net.Sim) _status.Apply(s, seconds); }
    private bool Held => _status.Has(Status.Disabled);

    // UP WHILE ANYTHING OF ITS SHIELDING ROW STILL STANDS. Every peer works this out from the
    // list it already has, so no state is sent for it and no peer can disagree about it.
    public bool Shielded => Def.Shields != null && Hub != null
                         && Hub.Emplacements.Any(e => IsInstanceValid(e) && e.Alive && e.Def.Id == Def.Shields);

    private readonly System.Collections.Generic.List<Turret> _mounts = new();
    private ShieldFlash _shield;
    private HullWatch _hullWatch;
    private Vector2 _aim;
    private double _cd, _dmg = 1;

    // ── the gun, and what carries it (ITurretHost) ───────────────────────────
    // MAIN guns, not point defence: point defence takes missiles and small craft only
    // (Targeting.PointDefence), so a PD mount would never fire at the ship besieging it.
    public Node2D AsNode => this;
    public bool PdOnline => false;
    public float PdRing => 0f;
    public Vector2 AimAt => _aim;
    public float FastSwing => 0f;
    public System.Collections.Generic.IReadOnlyList<Turret> Siblings => _mounts;
    public void NoteDealt(double d, Vector2 at) { }
    public PlayerShip Credit => null;
    public TurretSpec Spec(bool pd)
    {
        var s = Def.Gun;
        s.Damage *= _dmg;
        return s;
    }

    // the host's word on its hull (Hub.NetHulls); the host's own copy keeps its own figure
    public void SetNet(float hp) { if (!Net.Sim) Hp = hp; }

    public void TakeDamage(double d)
    {
        if (!Net.Sim || !Alive || Shielded) return;      // its shield is absolute while it is up
        Popups.NoteImpact(this, Position);
        Hp = Math.Max(0, Hp - d);
        if (Hp <= 0) Hub?.EmplacementDown(this);
    }

    public override void _Ready()
    {
        ZIndex = 4;
        _dmg = Missions.DamageMult(Missions.Level, Math.Max(1, Hub?.PartySize ?? 1));
        var art = Sprites.Fit(Def.Sprite, Def.Length);
        art.Modulate = Def.Main;                         // grey art, tinted -- never recoloured in the file
        AddChild(art);
        if (Def.Shields != null)
        {
            _shield = new ShieldFlash { HalfWidth = Def.HalfWidth, HalfLength = Def.Length * 0.5f,
                                        Tint = new Color(0.95f, 0.45f, 0.40f) };
            AddChild(_shield);
        }
        int guns = Math.Max(1, Def.Guns);
        for (int i = 0; i < guns; i++)
        {
            var t = new Turret();
            AddChild(t);
            t.Setup(this, guns == 1 ? Vector2.Zero : Vector2.Right.Rotated(Mathf.Tau * i / guns) * Def.GunRing, pd: false);
            _mounts.Add(t);
        }
        Combat.Hostiles.Add(this);
    }
    public override void _ExitTree() => Combat.Hostiles.Remove(this);

    public override void _Process(double delta)
    {
        _hullWatch.Tick(this, Hp, taken: false);
        if (_shield != null) _shield.Up = Shielded && Alive;
        if (Net.Sim && Alive)
        {
            _status.Tick(delta);
            var prey = Targeting.Nearest(Combat.Players, Position, Targeting.Attackable, Def.Gun.Range);
            if (prey != null) _aim = prey.Position;
            _cd -= delta;
            if (prey != null && !Held && _cd <= 0)
            {
                _cd = Def.Gun.Interval;
                foreach (var m in _mounts) m.Shoot();
            }
        }
        foreach (var m in _mounts) m.Tick(delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (MaxHp <= 0 || Hp >= MaxHp) return;
        float w = Def.HalfWidth * 2f, f = (float)Mathf.Clamp(Hp / MaxHp, 0, 1);
        float y = -Def.Length * 0.5f - 20f;
        DrawRect(new Rect2(-Def.HalfWidth, y, w, 6f), Def.Trim with { A = 0.8f });
        DrawRect(new Rect2(-Def.HalfWidth, y, w * f, 6f), Def.Main);
    }
}
