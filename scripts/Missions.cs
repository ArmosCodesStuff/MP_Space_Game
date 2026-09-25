using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Bounty missions from Threat Intelligence Operations: bosses, LEVELS, and rewards.
//   ONE LADDER OF LEVELS, the bosses taking them in turn (ForLevel): odd levels RUSTY BUCKET,
//   even levels the DRAKE BASTION. LEVELS start at 1:
//     S(L) = 1.025^(L-1)   -- a level-5 boss is 1.025^4 = 1.10x, a level-40 boss 2.6x. It was
//     1.1 a level, which doubled a boss every seven levels and left no pilot able to catch it.
//   A PARTY of P pilots: the boss's hull x S(L)(1 + 0.6(P-1)), its damage x S(L)(1 + 0.2(P-1)).
//   A failed level-L mission's raids are S(L) too.
//   Beating a level -- whichever boss held it -- unlocks the next; the TIO selects the newest when
//   the host opens it. The first-clear bonus is paid once a level, not once a boss.
//   REWARDS, per pilot, each computed on the pilot's own machine:
//     the kill   round(200 x boss level / pilot level)  (a level-5 boss for a level-10 pilot: 100)
//     +250       the first time that pilot beats that level
//     +100       for completing the mission
//     credits    2000 x S(L) x (1 + 0.5(P-1)), split evenly among the P pilots (solo earns the most)
//     parts      Loot.CratesFor(L) crates of gear, rolled by the host for each pilot (see Loot)
// WHAT A MISSION IS WON BY KILLING: a hull with a NAME and a MAXIMUM, so the arena's line reads
// one thing whether the mission built a boss or a pirate base. Boss answers it; Emplacement
// answers it. It says nothing about how the thing dies -- that is the mission row's business.
public interface IQuarry : IHittable
{
    string Title { get; }
    double Hp { get; }
    double MaxHp { get; }
    // How full the wind-up to its next big move is, 0..1 -- or BELOW ZERO for a quarry that has no
    // such move, which is how the bar knows not to draw the skinny meter under the hull. A type
    // test would have done it and is exactly what this interface exists to avoid.
    double SuperFill { get; }
}

public static class Missions
{
    // A BOSS, WHOLE: its id (a pilot's clears are recorded under it), its name, its base hull, the
    // art and shape of that hull, how it closes when no move holds it, and its MOVES -- a BossMove
    // row each. EnemyDef carries exactly this for a raider, down to the sprite and the length, and
    // for the same reason: BEHAVIOUR is code, geometry and art are data. There is no Make: every
    // boss is the one Boss class reading this row, where each was a subclass of its own with the
    // sprite, the length and the half-width written into it as abstract members.
    // Its ART -- Texture, Length, Tint and the Nozzles its art has (listed: a boss draws no flame)
    // -- is the HullArt it derives from (Sprites.cs). HalfWidth is how it is hit, not its art.
    public class BossType : HullArt
    {
        public string Id, Name;
        public double Hull;
        public float HalfWidth;
        // UNLOCKED, IT CLOSES TO about this far off its NOSE and no nearer (Boss.Approach adds the
        // half-length): measured from the hull, so a bigger hull holds the same gap. 470 is the old
        // 650 u from the centre of the 360 u Rusty Bucket.
        public float HoldOff = 470f;
        public float CloseSpeed = 30f;      // ...at this, ponderously
        public float TurnRate = 0.3f;       // its native turn (rad/s)
        public BossMove[] Moves;
    }
    // THE BOSSES ARE RED AND BLACK: the owner's swatch (mean RGB 172, 7, 2) multiplying the pack's
    // grey, so the art's highlights come out that red and its shadows black. Declared before
    // Bosses: a static field's initialiser runs in the order it is written.
    private static readonly Color BossRed = new(0.67f, 0.03f, 0.01f);
    // THE BOSSES ARE THIS MANY TIMES THE SIZE THEIR ART WAS MEASURED AT (the owner's "2 or 3x",
    // 2026-09-25, built at 2): the length, the half-width -- their HIT SIZE grows with the art --
    // and the bells. Everything a move places about the hull reads the row (Boss.cs), so 3x is
    // this one number.
    private const float BossSize = 2f;

    public static readonly BossType[] Bosses =
    {
        // ITS ID IS NOT ITS NAME. "silver_lancer" is a save key -- Character.BossCleared writes it
        // into [boss_cleared], and every file ever written carries it -- so the id is frozen and
        // the name above it is free. The code that holds its moves is Lancer.cs for the same
        // reason: it is named for the id on disk, not for the words on the screen.
        new() { Id = "silver_lancer", Name = "RUSTY BUCKET", Hull = 760,
                Texture = "res://boss_raider.png", Tint = BossRed,
                Nozzles = Nozzle.Scaled(BossSize, new(-30.30f, 178.81f, 39.21f), new(31.19f, 178.81f, 38.61f)),
                Length = 360f * BossSize, HalfWidth = 70f * BossSize, Moves = Lancer.Moves },
        new() { Id = "drake_bastion", Name = "DRAKE BASTION", Hull = 700,
                Texture = "res://boss_drake.png", Tint = BossRed,
                Nozzles = Nozzle.Scaled(BossSize, new(-53.82f, 209.38f, 20.78f), new(-28.54f, 208.76f, 20.47f), new(-3.57f, 191.39f, 15.82f),
                                                  new(19.85f, 208.45f, 19.85f), new(50.25f, 208.45f, 22.33f)),
                Length = 420f * BossSize, HalfWidth = 90f * BossSize, HoldOff = 440f,   // 650 u off the centre of its 420 u
                Moves = Drake.Moves },
    };
    // The boss of a level: every peer works it out from the replicated level alone.
    public static BossType ForLevel(int level) => Bosses[(Math.Max(1, level) - 1) % Bosses.Length];

    // ── A CATEGORY: a group in the TIO, and A LADDER OF ITS OWN ──────────────
    // The bounties and the raids climb SEPARATE ladders. Clearing a siege opens the next siege and
    // no boss level; clearing a boss opens no raid. ONE STORE carries both -- the cleared-levels
    // dictionary that is already on disk (Character.BossCleared -> section [boss_cleared], one key
    // per id, saved and loaded key by key whatever the keys are) -- and a category says which of
    // those keys count for it. There is no second field, and no second copy of the ladder code.
    //
    // WHAT AN OLD SAVE DOES, exactly. A BOUNTY still files under the BOSS that held the level
    // ("silver_lancer", "drake_bastion"), which is what every earlier build wrote, and those ids
    // are the BOSS category's own Keys -- so a file from any earlier build reads with its boss
    // ladder exactly intact: no migration, no conversion, no new key. A RAID files under its
    // kind's id ("siege"), which no earlier build ever wrote, so an existing pilot's raid ladder
    // is empty and its first siege is level 1. The other way round is safe too: the old filter
    // kept only boss ids, so an old build reading a new file simply ignores "siege".
    public sealed class MissionCategory
    {
        public string Id;          // what the TIO shows, and how a kind names its group
        public string[] Keys;      // file keys that count for it BESIDE its own kinds' ids
    }
    public static readonly MissionCategory[] Categories =
    {
        new() { Id = "BOSS",  Keys = Bosses.Select(b => b.Id).ToArray() },
        new() { Id = "RAIDS", Keys = Array.Empty<string>() },
    };

    // ── WHAT A MISSION IS: one row, and a third kind is a row too ────────────
    // A bounty was the only thing a mission could ever be: BossType was the whole vocabulary and
    // Hub.BuildArena wrote `new Boss()` out by hand, because there was nothing else to build. A
    // row says what a mission is CALLED, which CATEGORY it is filed under (and so which ladder it
    // climbs), what it BUILDS, what it is WON BY KILLING, what its clear is RECORDED under, what
    // it PAYS, whether its quarry DROPS, and its own garrison CLOCK. Nothing in Hub.cs names a kind.
    //
    // A ROW MUST FILL IN: Id, Name, Category, Build and Quarry. Everything else has a default that
    // means "as a bounty does it".
    public sealed class MissionKind
    {
        public string Id;                           // reached by id; a clear may file under it
        public string Name;                         // what the TIO calls it
        public string Category;                     // a row of Categories, by its Id
        public Func<int, string> Title;             // what the arena's line calls the thing at level L
        public Action<Hub> Build;                   // what it puts in the arena
        public Func<Hub, IQuarry> Quarry;           // what it is won by killing, and whose wreck drops
        public Action<Hub> Won;                     // a guest hearing the host won: what its copy must be told
        public Action<Hub, int> CatchUp;            // a peer arriving mid-mission: what Hub.Spawn's catch-up cannot carry
        public Func<int, string> Record;            // what a clear at level L files under (null: this row's Id)
        public double Pay = 1;                      // its share of BountyBase
        // ...and its share of the EXP a clear pays. TWO SIEGES OR FOUR BOSS KILLS is the owner's
        // rate for a pilot to come level with a level, so a siege is worth two bounties: it is a
        // base with four pylons and a garrison, against one hull.
        public double Exp = 1;
        public bool Drops = true;                   // its quarry's wreck leaves crates
        public double FirstWave, WaveEvery;         // its own garrison clock, in seconds (0: it brings none)
        public string RecordId(int level) => Record?.Invoke(level) ?? Id;
    }

    public const int Bounty = 0, Siege = 1;
    public static readonly MissionKind[] Kinds =
    {
        // A BOUNTY. Its clear files under the BOSS that held the level, not under this row, so
        // every character file written before this table still reads with its ladder intact.
        new() { Id = "bounty", Name = "BOUNTY", Category = "BOSS",
                Title = l => ForLevel(l).Name,
                Record = l => ForLevel(l).Id,
                Build = h => h.BuildBoss(),
                Quarry = h => GodotObject.IsInstanceValid(h.Boss) ? h.Boss : null,
                Won = h => { if (GodotObject.IsInstanceValid(h.Boss)) h.Boss.Downed(); },
                CatchUp = (h, who) => { if (GodotObject.IsInstanceValid(h.Boss) && h.Boss.Alive) h.Boss.CatchUp(who); } },

        // A SIEGE -- the first entry of RAIDS: a pirate base behind four shield pylons, its
        // garrison arriving wave by wave (Waves.All: "siege") on the clock below. What a pirate
        // base IS is not here: it is a row of Emplacements.All, and a SECOND raid is more rows
        // there plus one more row here. Its quarry is the base, so only the base ever drops.
        new() { Id = "siege", Name = "PIRATE BASE SIEGE", Category = "RAIDS",
                Title = _ => Emplacements.Of(Emplacements.RowOf(Emplacements.Base)).Label,
                Build = h => Emplacements.Raise(h, Emplacements.PirateBase),
                Quarry = h => h.Emplacements.FirstOrDefault(e => GodotObject.IsInstanceValid(e) && e.Def.Id == Emplacements.Base),
                Exp = 2, FirstWave = 12, WaveEvery = 25 },
    };
    public static MissionKind KindOf(int kind) => Kinds[kind >= 0 && kind < Kinds.Length ? kind : Bounty];
    public static MissionCategory CatOf(int kind)
    {
        var id = KindOf(kind).Category;
        foreach (var c in Categories) if (c.Id == id) return c;
        return Categories[0];
    }
    // THE SELECTED KIND (the host decides; replicated beside the level, on the same two packets --
    // Hub.NetSector and Hub.NetMission). It HAS to reach a guest: a guest builds its own arena
    // from this row, and the row is also which LADDER the level it was sent belongs to.
    // A property for the same reason Level is one: the bound is kept once, here.
    private static int _kind;
    public static int Kind { get => _kind; set => _kind = value >= 0 && value < Kinds.Length ? value : Bounty; }

    // A BOSS GETS QUICKER AND LONGER-ARMED WITH THE LEVEL, gently: one percent a level, compounding,
    // beside the 10% a level its hull and damage already take. A pilot's reach and rate of fire
    // climb with gear and points, and a boss whose wind-ups and ranges never moved would be fought
    // from further out and dodged more easily every level -- the fight would get EASIER as the
    // numbers got bigger. What it touches: how long it takes to wind up and how fast a fired body
    // flies (its animation), and how far its moves carry. What it does NOT touch is how long a beam
    // BURNS or how often that burn is judged, because those are the damage itself, which has a
    // scale of its own.
    public const double MoveStep = 1.01;
    public static double Quicken(int level) => Math.Pow(MoveStep, Math.Max(1, level) - 1);
    // WHAT A LEVEL ADDS TO A MISSION: its hull, its damage, its bounty and its crates.
    //
    // 1.025, not the 1.10 it was. Measured against the owner's own target -- a pilot on par after
    // FOUR boss kills at a level (or two sieges), carrying gear levelled to about 65% of the
    // salvage ladder -- a tenth a level was unwinnable: the boss grew forty-fold across forty
    // levels while the points those kills paid for bought +45%, so the fight at level 20 was a
    // 200-shot slog and level 40 was arithmetic, not combat. At 1.025 a level-40 fight is twice
    // the length of a level-1 fight: the boss pulls steadily ahead, which is what was asked for,
    // and the answer is a fourth clear or better gear rather than a wall.
    public const double LevelStep = 1.025;
    public static double S(int level) => Math.Pow(LevelStep, Math.Max(1, level) - 1);
    // THE SELECTED LEVEL -- ONE PER CATEGORY (the host decides; replicated). The bounty ladder and
    // the raid ladder are separate, so the level left selected for one is never moved by stepping
    // the other. `Level` is always the SELECTED category's: that is the one an arena is built from
    // and the only one on the wire -- the KIND rides beside it, and a category is the kind's, so a
    // guest always knows which ladder the level it was sent belongs to. It stays a property so the
    // floor is kept once, here, rather than by each arrival.
    private static readonly Dictionary<string, int> _levels = new();
    public static int Level
    {
        get => _levels.TryGetValue(KindOf(Kind).Category, out var l) ? Math.Max(1, l) : 1;
        set => _levels[KindOf(Kind).Category] = Math.Max(1, value);
    }
    // Session state, like the world a guest was in: a pilot that visited a host sitting on level
    // 12 carried that 12 into its own next session, and hosting broadcast it to every guest. EVERY
    // ladder goes back to this pilot's own newest, and the operation back to the first row.
    public static void EndSession()
    {
        Kind = Bounty;
        foreach (var c in Categories) _levels[c.Id] = HighestIn(c) + 1;
    }
    // ── THE LADDER, ONE PER CATEGORY ─────────────────────────────────────────
    // Which file keys count toward a category: the ones it names outright (the BOSS ladder's are
    // the boss ids, which is what a bounty has always filed under) plus every kind filed under it
    // (the RAIDS ladder's is "siege"). An id in the file this build does not know still counts for
    // nothing, as it always has. Every question about a ladder is asked ABOUT A KIND, so no caller
    // can ask the wrong one by forgetting to say which.
    private static HashSet<string> KeysOf(MissionCategory c) =>
        c.Keys.Concat(Kinds.Where(k => k.Category == c.Id).Select(k => k.Id)).ToHashSet();
    private static IEnumerable<int> ClearedIn(MissionCategory c)
    {
        var keys = KeysOf(c);
        return Character.BossCleared.Where(kv => keys.Contains(kv.Key)).SelectMany(kv => kv.Value);
    }
    private static int HighestIn(MissionCategory c) => ClearedIn(c).DefaultIfEmpty(0).Max();
    public static bool Cleared(int kind, int level) => ClearedIn(CatOf(kind)).Contains(level);
    public static int HighestBeaten(int kind) => HighestIn(CatOf(kind));
    public static int Unlocked(int kind) => HighestIn(CatOf(kind)) + 1;

    public static double HullMult(int level, int party) => S(level) * (1 + 0.6 * (Math.Max(1, party) - 1));
    public static double DamageMult(int level, int party) => S(level) * (1 + 0.2 * (Math.Max(1, party) - 1));
    private const double BountyBase = 2000;
    // WHAT A MISSION PAYS is its row's share of the base figure (MissionKind.Pay), never a second
    // formula: the level's scale and the party's split are the same for every kind.
    public static double BountyEach(int kind, int level, int party) =>
        BountyBase * KindOf(kind).Pay * S(level) * (1 + 0.5 * (Math.Max(1, party) - 1)) / Math.Max(1, party);

    public const int KillExp = 300, FirstClearExp = 250, CompletionExp = 100;
    // A BOSS UNDER HALF YOUR LEVEL TEACHES YOU NOTHING. The kill's EXP already fell away with the
    // gap -- 300 x its level over yours -- but it never reached zero, so farming a level-1 boss
    // for ever still paid, and the first-clear and completion bonuses paid in full whatever you
    // flew against. Below the mark the whole award is nothing; the clear is still RECORDED, so the
    // ladder and the levels you have beaten do not care what level you were when you beat them.
    public const double ExpFloorShare = 0.5;
    public static bool WorthExp(int bossLevel, int pilotLevel) => bossLevel >= pilotLevel * ExpFloorShare;
    public static int KillExpFor(int bossLevel, int pilotLevel) =>
        !WorthExp(bossLevel, pilotLevel) ? 0 : (int)Math.Round(KillExp * (double)bossLevel / Math.Max(1, pilotLevel));
}
