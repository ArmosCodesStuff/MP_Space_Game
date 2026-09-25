using Godot;

// ─────────────────────────────────────────────────────────────────────────────
// WHAT COMES AT YOU — one row per enemy, and nothing about an enemy anywhere else.
//
// There were two enemies and they were written as `Heavy ? this : that` in thirty places inside
// Raider: the sprite, the tint, the length, the hull, the damage, the reload, the reach, the
// boost, the turret, the missile. A third enemy meant a third arm on every one of those.
//
// Now a row says what an enemy IS, and Raider is the code that flies one. Two WAYS of fighting
// are real behaviour and stay behaviour:
//   PIN       -- closes, holds station 90% of its reach off the hull, and WEBS what it holds
//                (the target is kept to a fifth of its speed, thrusting, unable to turn).
//   STANDOFF  -- waits at the map's edge until something else has pinned the target, then boosts
//                in astern and hammers it from just outside its own reach.
// Everything else about an enemy is this table, so a new one is a row.
// ─────────────────────────────────────────────────────────────────────────────
public enum EnemyWay { Pin, Standoff }

public class EnemyDef
{
    public string Id, Name;
    public string Texture;
    public EnemyWay Way;
    public float Length = 34f;
    public float HitShare = 0.4f;        // its hit radius, as a share of its length
    public double Hull = 25;
    public double Dps = 1.0;             // x -- the raider damage the whole game is scaled in
    public double ShotEvery = 1.0;       // 1 s: slower than a target's 0.52 s gap, so no shot is wasted
    public float Cruise = 100f;          // under every capital ship's top speed (104 to 130)
    public float BoostMult = 5f;
    public double BoostTime = 3.0;
    public float Reach = 100f;           // how close it fights from; it holds station at 90% of it
    public bool Turret;                  // a turret on its spine that tracks what it shoots
    // ITS MAIN TURRET, when Turret is set, as shares of its OWN length: the mount aft of centre,
    // and how wide the housing is. These were the GUNSHIP's 19.5 u and 13 u, scaled inside Raider
    // by Length / the gunship's length -- so every hull that mounted a turret reached into the
    // gunship's row by name and none could be given a mount of its own.
    public float TurretAft = 19.5f / 136f;
    public float TurretWidth = 13f / 136f;
    public bool Missiles;                // the fat missile, thrown at where the target WILL be
    // ITS MISSILE, when Missiles is set. These were `Raider` consts, so every row that set
    // Missiles threw the SAME missile: the Lancerkin, whose whole point is standing 260 u off,
    // fired the gunship's 500 u missile and could not be given its own. (The 7 s flight is not a
    // row: Hub takes it as a compile-time default -- see Raider.MissileFlight.)
    public float MissileRange = 500f;    // it throws one from within this
    public double MissileEvery = 12.0;   // seconds between them
    // WHAT THE BLAST DOES, before Strength. 42 rather than 30: the missile is 12 s in the air now
    // instead of 7, so it is dodged far more easily and has to be worth dodging.
    public double MissileDamage = 42;
    public float BlastRadius = 90f;      // how wide the blast is where it lands
    public Color Tint;
    // WHAT ITS GUN IS SEEN AND HEARD AS: a row of Beam.All. A light fires from its nose and a heavy
    // from its turret, but what the shot looks and sounds like is the row's. EVERY ROW NAMES ONE:
    // row 0 is YOUR point defence, and an enemy that fell through to it would buzz like a friend.
    public int Beam;

    public float Hold => Reach * 0.9f;   // posted just inside its reach, so nothing is lost to drift
    public Tag Tag => Way == EnemyWay.Standoff ? Tag.Heavy : Tag.Light;
}

public static class Enemies
{
    // The raiders' art is grey line art; these give it raider red, the heavier hulls darker.
    // Shared with the title screen's raiders, so the enemy on the menu is the colour of the enemy
    // you meet.
    public static readonly Color LightTint = new(0.90f, 0.38f, 0.33f);
    public static readonly Color HeavyTint = new(0.62f, 0.40f, 0.40f);
    private static readonly Color TalonTint = new(0.95f, 0.52f, 0.30f);
    private static readonly Color PodTint = new(0.72f, 0.45f, 0.52f);
    private static readonly Color CrossTint = new(0.55f, 0.42f, 0.50f);
    private static readonly Color KinTint = new(0.70f, 0.40f, 0.34f);

    // The index IS the id on the wire (Hub.NetRaiderSpawn), so APPEND ONLY.
    public const int Webifier = 0, Gunship = 1, Talon = 2, Pod = 3, Cross = 4, Lancerkin = 5;

    public static readonly EnemyDef[] All =
    {
        new() { Id = "webifier", Name = "Webifier", Way = EnemyWay.Pin,
                Texture = "res://enemy_light_fighter.png", Tint = LightTint,
                Length = 34f, Hull = 25, Dps = 1.0, Reach = 100f, Beam = Beam.LightRaider },

        new() { Id = "gunship", Name = "Gunship", Way = EnemyWay.Standoff,
                Texture = "res://enemy_heavy_hull.png", Tint = HeavyTint,
                Length = 136f, HitShare = 0.3f, Hull = 100, Dps = 2.0, Reach = 150f,
                BoostMult = 7f, Turret = true, Missiles = true, Beam = Beam.HeavyRaider },

        // ── the owner's four new hulls ──────────────────────────────────────
        // A TALON is a webifier that gave up its armour for speed: it arrives first and holds on.
        new() { Id = "talon", Name = "Talon", Way = EnemyWay.Pin,
                Texture = "res://enemy_talon_hull.png", Tint = TalonTint,
                Length = 40f, Hull = 18, Dps = 1.4, Cruise = 130f, Reach = 90f, Beam = Beam.LightRaider },

        // A POD is slow and hard to shift -- it webs from further out and takes a while to kill.
        new() { Id = "pod", Name = "Pod", Way = EnemyWay.Pin,
                Texture = "res://enemy_pod_hull.png", Tint = PodTint,
                Length = 52f, HitShare = 0.45f, Hull = 60, Dps = 0.7, Cruise = 78f,
                BoostMult = 3.5f, Reach = 130f, Beam = Beam.LightRaider },

        // A CROSS is a gunship with the missile racks stripped out and the gun wound up.
        new() { Id = "cross", Name = "Cross", Way = EnemyWay.Standoff,
                Texture = "res://enemy_cross_hull.png", Tint = CrossTint,
                Length = 120f, HitShare = 0.34f, Hull = 130, Dps = 2.6, ShotEvery = 0.8,
                Reach = 140f, BoostMult = 6f, Turret = true, Beam = Beam.HeavyRaider },

        // A LANCERKIN is built on the Silver Lancer's lines: it stands further off than any of
        // them and throws the same predicted missile.
        new() { Id = "lancerkin", Name = "Lancerkin", Way = EnemyWay.Standoff,
                Texture = "res://enemy_lancerkin_hull.png", Tint = KinTint,
                Length = 150f, HitShare = 0.3f, Hull = 150, Dps = 1.8, ShotEvery = 1.2,
                Reach = 260f, BoostMult = 5.5f, Turret = true, Missiles = true, Beam = Beam.HeavyRaider },
    };

    public static EnemyDef Of(int kind) => All[kind >= 0 && kind < All.Length ? kind : Webifier];

    // The kinds a raid may send, by WAY: the lights that pin, and the gunships that wait for the
    // pin. A wave picks from these, so every new row joins the rotation by existing.
    public static int[] OfWay(EnemyWay w)
    {
        int n = 0;
        foreach (var e in All) if (e.Way == w) n++;
        var ids = new int[n]; int i = 0;
        for (int k = 0; k < All.Length; k++) if (All[k].Way == w) ids[i++] = k;
        return ids;
    }
}
