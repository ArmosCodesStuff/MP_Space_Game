using Godot;
using System;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// AN EMPLACEMENT — a HOSTILE hull that does not move, may answer with a gun, and may stand behind a shield
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
// path), its art (HullArt: Texture, Length, Tint -- grey on transparent, NEVER recoloured in the
// file, exactly as a raider's is -- Raider._Ready), its hit half-width, the row whose live ones
// hold ITS shield up (null: it has none), and what it answers with: a TurretSpec on one mount at
// its centre, like every other gun in the game -- its round any row of Shots.All, and a warning
// before each shot if the spec asks for one (TurretSpec.Windup) -- or null, for a hull that is
// only something to be got through.
//
// A NEW SITE is a Post[] -- a row of All, and where it stands from Hub.ArenaCentre -- plus the one
// mission row that names it (Missions.Kinds[].Build). No code is edited for either.
//
// AUTHORITY: the host alone raises a site, damages a hull and drops one. A guest builds its copies
// from Hub.NetSpawn, takes their hulls from Hub.NetHulls, and works the SHIELD out for itself --
// it is "anything of the shielding row still standing", and a guest holds the same list the host
// holds, so there is nothing to put on the wire for it.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class EmplacementDef : HullArt
{
    public string Id;                           // reached by id, never by type
    public string Label;                        // what the scope, the HUD and a target line call it
    public Func<int, double> Hull;              // its hull at level L, before Missions.HullMult
    public float HalfWidth;                     // the hull's half width (Texture/Length/Tint: HullArt)
    public Color Trim;                          // what is drawn over the art (the health bar)
    public string Shields;                      // the row whose live ones hold its shield up
    // WHAT IT ANSWERS WITH, on one mount at its centre, before Missions.DamageMult (and a Hulled
    // round's hull before Missions.HullMult for one pilot) -- or null: it answers with nothing
    public TurretSpec? Gun;
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
    public const double PylonShare = 0.125;         // a pylon's hull, of the Lancer's row
    // THE BOSS ROW A SIEGE IS SIZED ON, by id: the order of Missions.Bosses sets which boss holds a
    // level, so a place in it is never read for this.
    private const string SiegeRow = "silver_lancer";
    private static double SiegeHull => Missions.Bosses.First(b => b.Id == SiegeRow).Hull;

    public static readonly EmplacementDef[] All =
    {
        // THE PIRATE BASE. The hull of the LANCER'S ROW (SiegeRow, 3222 at level 1) on the
        // level's own scale -- a row of its own, never the boss that holds the level (Lancer and
        // Drake take turns, and the base would swing 8% between odd and even levels) -- and nothing may
        // touch it while a pylon stands. It carries NO GUNS: a siege's damage is its garrison's
        // (Waves.All, "siege"), and what the base adds is one CRUISE MISSILE every 15 s at the
        // nearest pilot within 4500 u -- a red lane to that pilot 1.5 s ahead, a barrel quick enough
        // to come round from any bearing inside it (Turn x Windup > pi) so the round leaves down its
        // own lane, then a round slow enough to be met (120 u/s) that every weapon a pilot carries
        // can bring down (Tag.Hulled). 126 a missile is the base's share of a siege at the curve's
        // fit, 8.4 a second, over the 15 s between them; 30 hull is two seconds of a median main gun,
        // on the level's scale alone because it is fired at ONE pilot (Emplacement._hull).
        new() { Id = Base, Label = "PIRATE BASE", Hull = _ => SiegeHull,
                Texture = "res://pirate_base.png", Length = 483f, HalfWidth = 330f,
                Tint = new Color(0.72f, 0.20f, 0.17f), Trim = new Color(0.08f, 0.08f, 0.10f),
                Shields = Pylon,
                Gun = new TurretSpec { Kind = Shots.Cruise, Damage = 126, Interval = 15, Range = 4500f,
                                       Turn = Mathf.Tau / 2.5f, ShellSpeed = 120f,
                                       Homing = 1.2f, Size = 3f, Hull = 30, Windup = 1.5,
                                       Texture = "res://turret_main.png", TexScale = 0.26f, Barrel = 34f,
                                       Tint = new Color(0.90f, 0.40f, 0.34f), Source = DamageSource.BaseMissile } },

        // ITS FOUR SHIELD PYLONS. An eighth of the Lancer's row (403 at level 1), on the same scale; nothing shields
        // THEM, nothing ends the mission when one falls, and they answer with nothing -- they are
        // only what has to go first.
        new() { Id = Pylon, Label = "SHIELD PYLON", Hull = _ => PylonShare * SiegeHull,
                Texture = "res://drone_sensor.png", Length = 300f, HalfWidth = 150f,   // the Pod's own file (D18)
                Tint = new Color(0.68f, 0.20f, 0.20f), Trim = new Color(0.08f, 0.08f, 0.10f) },
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
public partial class Emplacement : Node2D, IQuarry, ITagged, IStatused, IShielded, ITurretHost
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
    public void ApplyStatus(Status s, double seconds, double share = double.NaN) { if (Net.Sim && StatusSet.Reaches(s, Tags)) _status.Apply(s, seconds, share); }
    private bool Held => _status.Has(Status.Disabled);

    // UP WHILE ANYTHING OF ITS SHIELDING ROW STILL STANDS. Every peer works this out from the
    // list it already has, so no state is sent for it and no peer can disagree about it.
    public bool Shielded => Def.Shields != null && Hub != null
                         && Hub.Emplacements.Any(e => IsInstanceValid(e) && e.Alive && e.Def.Id == Def.Shields);

    private readonly System.Collections.Generic.List<Turret> _mounts = new();
    private ShieldFlash _shield;
    private HullWatch _hullWatch;
    private Vector2 _aim;
    private double _cd, _dmg = 1, _hull = 1;
    private int _marked;                              // the pilot (its NetId) the warning that is up went up for; 0: none
    private double _warn;                             // ...and what is left of that warning

    // ── the gun, and what carries it (ITurretHost) ───────────────────────────
    // MAIN guns, not point defence: a mount that picks for itself picks from Combat.Hostiles
    // (Turret.Acquire) -- this emplacement's own side -- so a PD mount would never fire at the ship
    // besieging it.
    public Node2D AsNode => this;
    public bool PdOnline => false;
    public Vector2 AimAt => _aim;
    public float FastSwing => 0f;
    public System.Collections.Generic.IReadOnlyList<Turret> Siblings => _mounts;
    public void NoteDealt(double d, IHittable target, string weapon) { }
    public PlayerShip Credit => null;
    // read as each round leaves (Turret.Shoot), so its damage goes out through the door
    // (StatusSet.Out) with whatever is on this hull at that moment
    public TurretSpec Spec(bool pd)
    {
        var s = Def.Gun.GetValueOrDefault();
        s.Damage = _status.Out(s.Damage * _dmg, OutKind.Gun);
        s.Hull *= _hull;
        return s;
    }

    // the host's word on its hull (Hub.NetHulls); the host's own copy keeps its own figure
    public void SetNet(float hp) { if (!Net.Sim) Hp = hp; }

    public void TakeDamage(double d) { if (!Shielded) Take(d); }      // its shield is absolute while it is up...
    public void TakeThrough(double d) => Take(d);                     // ...but for what a row sends through it (IShielded)
    private void Take(double d)
    {
        if (!Net.Sim || !Alive) return;
        Popups.NoteImpact(this, Position);
        Hp = Math.Max(0, Hp - d);
        if (Hp <= 0) Hub?.EmplacementDown(this);
    }

    public override void _Ready()
    {
        ZIndex = 4;
        _dmg = Missions.DamageMult(Missions.Level, Math.Max(1, Hub?.PartySize ?? 1));
        // A ROUND'S OWN HULL is sized for ONE pilot's guns, on the level's scale: it is fired at one
        // pilot, and a party's other guns are not all where it is going
        _hull = Missions.HullMult(Missions.Level, 1);
        var art = Sprites.Fit(Def);                       // grey art, tinted -- never recoloured in the file
        AddChild(art);
        if (Def.Shields != null)
        {
            _shield = new ShieldFlash { HalfWidth = Def.HalfWidth, HalfLength = Def.Length * 0.5f,
                                        Tint = new Color(0.95f, 0.45f, 0.40f) };
            AddChild(_shield);
        }
        if (Def.Gun != null)
        {   // one mount, at its centre
            var t = new Turret();
            AddChild(t);
            t.Setup(this, Vector2.Zero, pd: false);
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
            if (Def.Gun is { } gun)
            {
                var prey = Targeting.Nearest(Combat.Players, Position, Targeting.Attackable, gun.Range);
                WarnAndFire(gun, prey, delta);
                // the barrel follows the pilot a warning is up for, and otherwise the nearest in reach
                // -- while it can SEE that pilot. Aiming is choosing (Targeting.cs), so a pilot gone
                // dark leaves the barrel on the last point it was seen at, as a boss's aim is left, and
                // the round leaves down that line and flies on straight (Shot: homing is choosing too)
                if ((_marked != 0 ? Combat.PlayerById(_marked) : prey) is { } aim && !Targeting.Hidden(aim)) _aim = aim.Position;
            }
        }
        foreach (var m in _mounts) m.Tick(delta);
        QueueRedraw();
    }

    // ITS CLOCK, AND THE WARNING BEFORE EACH SHOT. A reload that carries its remainder, as
    // Turret's does: it runs down whether or not a pilot is in reach and waits at zero, loaded,
    // never banking a second shot. When it is due and a pilot is in reach, the warning goes up for
    // the nearest one (Turret.Warn: a red lane to that pilot for the gun's Windup, riding this
    // hull) and the shot follows when the warning is spent -- at THAT pilot, by its id, if it is
    // still in the fight, and at nobody otherwise. The clock restarts as the warning goes up, so
    // shots are Interval apart whatever the warning's length (a warning longer than the interval
    // sets the pace instead). Held (a status) raises no warning, and spends one already up without
    // its shot. A gun with no wind-up fires the frame after it comes due.
    private void WarnAndFire(TurretSpec gun, IHittable prey, double delta)
    {
        _cd -= delta;
        if (prey == null || Held) { if (_cd < 0) _cd = 0; }
        else if (_marked == 0 && _cd <= 0)
        {
            _cd += gun.Interval; _marked = prey.NetId; _warn = gun.Windup;
            foreach (var m in _mounts) m.Warn(prey, NetId);
            return;                                   // the warning's first frame: none of it is spent yet
        }
        if (_marked == 0 || (_warn -= delta) > 0) return;
        if (!Held && Combat.PlayerById(_marked) != null) foreach (var m in _mounts) m.Shoot(target: _marked);
        _marked = 0;
    }

    public override void _Draw()
    {
        if (MaxHp <= 0 || Hp >= MaxHp) return;
        float w = Def.HalfWidth * 2f, f = (float)Mathf.Clamp(Hp / MaxHp, 0, 1);
        float y = -Def.Length * 0.5f - 20f;
        DrawRect(new Rect2(-Def.HalfWidth, y, w, 6f), Def.Trim with { A = 0.8f });
        DrawRect(new Rect2(-Def.HalfWidth, y, w * f, 6f), Def.Tint);
    }
}
