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
//   PIN       -- takes a FRONT post (Squads.cs), holds station 90% of its reach off the hull, and
//                puts its row's Cc on what it holds (Pinned: a fifth of its speed, unable to turn).
//   STANDOFF  -- takes a REAR post astern and hammers the target from just inside its own reach,
//                pinned or not; it never holds anything (no Cc, at any level), and its missile flies
//                only at a target something else has pinned.
// How either CLOSES -- in formation, then a shared burn -- is its squad's (Squads.cs), not a way.
// Everything else about an enemy is this table, so a new one is a row.
// ─────────────────────────────────────────────────────────────────────────────
public enum EnemyWay { Pin, Standoff }

// Its ART -- Texture, Length (nose to tail: every size below is a share of it), Tint and the
// Nozzles -- is the HullArt it derives from (Sprites.cs).
public class EnemyDef : HullArt
{
    public string Id, Name;
    public EnemyWay Way;
    public float HitShare = 0.4f;        // its hit radius, as a share of its length
    public double Hull = 25;
    public double Dps = 1.0;             // x -- the raider damage the whole game is scaled in
    public double ShotEvery = 1.0;       // 1 s: slower than a target's 0.52 s gap, so no shot is wasted
    public float Cruise = 100f;          // under the destroyer's 117 and every smaller hull's top; over the battleship's 88 and the carrier's 99 (F22), which cannot outrun it
    public float BoostMult = 5f;
    public double BoostTime = 3.0;
    public float Reach = 100f;           // how close it fights from; it holds station at 90% of it
    public bool Turret;                  // a turret on its spine that tracks what it shoots
    // ITS MAIN TURRET, when Turret is set, as shares of its OWN length: the mount aft of centre,
    // and how wide the housing is -- both off its own art (tools/make_ships.ps1 prints the mount
    // and the housing's edge), so every row that sets Turret names both.
    public float TurretAft, TurretWidth;
    // HOW MANY GUNS FIRE TOGETHER (F20): one Strike per volley carries Barrels x Dps, so two heavy
    // barrels landing at once are not eaten by the target's own 0.52 s hit gap the way two separate
    // Strikes would be; drawn as that many flashes from barrel offsets either side of the turret's
    // centre. A light fires from its nose, never a turret, so it stays at 1.
    public int Barrels = 1;
    public bool Missiles;                // the fat missile, thrown at where the target WILL be
    // ITS MISSILE, when Missiles is set. These were `Raider` consts, so every row that set
    // Missiles threw the SAME missile: the Lancerkin, whose whole point is standing 260 u off,
    // fired the gunship's 500 u missile and could not be given its own.
    public float MissileRange = 500f;    // it throws one from within this
    public double MissileEvery = 12.0;   // seconds between them
    public double MissileFlight = 10.0;  // seconds in the air
    // WHAT THE BLAST DOES, before Strength. Only thrown at a pinned target (Raider.TickHeavy).
    public double MissileDamage = 35;
    public float BlastRadius = 90f;      // how wide the blast is where it lands
    // WHAT ITS GUN IS SEEN AND HEARD AS: a row of Beam.All. A light fires from its nose and a heavy
    // from its turret, but what the shot looks and sounds like is the row's. EVERY ROW NAMES ONE:
    // row 0 is YOUR point defence, and an enemy that fell through to it would buzz like a friend.
    public int Beam;
    // WHAT A LATCH APPLIES TO WHAT IT HOLDS (F20): a Pin-way row names the status its own hold
    // puts on the target (every one today is Pinned); a Standoff row names none -- it never
    // latches onto anything, it only fires from range. Raider's latch applies whatever this names.
    public Status? Cc;
    // BOUNTY, before Strength (F20; lane G pays it: not built here). 6 for a light, 18 for a heavy.
    public double Exp;

    public float Hold => Reach * 0.9f;   // posted just inside its reach, so nothing is lost to drift
    public Tag Tag => Way == EnemyWay.Standoff ? Tag.Heavy : Tag.Light;
}

public static class Enemies
{
    // The raiders' art is grey; these give it raider red, the heavier hulls darker.
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
                Nozzles = new Nozzle[] { new(-4.79f, 16.56f, 2.62f), new(4.79f, 16.56f, 2.62f) },
                Length = 34f, Hull = 25, Dps = 1.0, Reach = 100f, Beam = Beam.LightRaider,
                Cc = Status.Pinned, Exp = 6 },

        new() { Id = "gunship", Name = "Gunship", Way = EnemyWay.Standoff,
                Texture = "res://enemy_heavy_hull.png", Tint = HeavyTint,
                Nozzles = new Nozzle[] { new(-13.51f, 67.53f, 14.67f), new(12.34f, 67.53f, 14.67f) },
                Length = 136f, HitShare = 0.3f, Hull = 100, Dps = 1.29, Barrels = 2, Reach = 150f,
                BoostMult = 7f, Missiles = true, Beam = Beam.HeavyRaider, Exp = 18,
                Turret = true, TurretAft = 20.96f / 136f, TurretWidth = 10.24f / 136f },   // on its painted twin

        // ── the owner's four new hulls ──────────────────────────────────────
        // A TALON is a webifier that gave up its armour for speed: it arrives first and holds on.
        new() { Id = "talon", Name = "Talon", Way = EnemyWay.Pin,
                Texture = "res://enemy_talon_hull.png", Tint = TalonTint,
                Nozzles = new Nozzle[] { new(-6.69f, 19.69f, 3.54f), new(6.54f, 19.69f, 3.54f) },
                Length = 40f, Hull = 18, Dps = 1.4, Cruise = 130f, Reach = 90f, Beam = Beam.LightRaider,
                Cc = Status.Pinned, Exp = 6 },

        // A POD is slow and hard to shift -- it webs from further out and takes a while to kill.
        new() { Id = "pod", Name = "Pod", Way = EnemyWay.Pin,
                Texture = "res://drone_sensor.png", Tint = PodTint,   // shared with the siege's pylon (D18)
                Nozzles = new Nozzle[] { new(-0.21f, 23.27f, 7.76f) },               // its one stern vent
                Length = 52f, HitShare = 0.45f, Hull = 60, Dps = 0.7, Cruise = 78f,
                BoostMult = 3.5f, Reach = 130f, Beam = Beam.LightRaider, Cc = Status.Pinned, Exp = 6 },

        // A CROSS is a gunship with the missile racks stripped out and the gun wound up.
        new() { Id = "cross", Name = "Cross", Way = EnemyWay.Standoff,
                Texture = "res://enemy_cross_hull.png", Tint = CrossTint,
                Nozzles = new Nozzle[] { new(-10.64f, 59.60f, 12.44f), new(9.83f, 59.60f, 12.44f) },
                Length = 120f, HitShare = 0.34f, Hull = 130, Dps = 1.29, Barrels = 2, ShotEvery = 0.8,
                Reach = 140f, BoostMult = 6f, Beam = Beam.HeavyRaider, Exp = 18,
                Turret = true, TurretAft = 28.09f / 120f, TurretWidth = 12.04f / 120f },   // on the clean aft deck

        // A LANCERKIN is the slim missile boat, its tubes down its spine: it stands further off
        // than any of them and throws the same predicted missile.
        new() { Id = "lancerkin", Name = "Lancerkin", Way = EnemyWay.Standoff,
                Texture = "res://enemy_lancerkin_hull.png", Tint = KinTint,
                Nozzles = new Nozzle[] { new(-11.46f, 74.54f, 13.66f), new(11.57f, 74.54f, 13.66f) },
                Length = 150f, HitShare = 0.3f, Hull = 150, Dps = 1.29, Barrels = 2, ShotEvery = 1.2,
                Reach = 260f, BoostMult = 5.5f, Missiles = true, Beam = Beam.HeavyRaider, Exp = 18,
                Turret = true, TurretAft = 37.50f / 150f, TurretWidth = 12.26f / 150f },   // on the plate aft of its tubes
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
