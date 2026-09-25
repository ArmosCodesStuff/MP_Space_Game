using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// LOOT -- what a boss leaves each pilot, and only bosses leave any. The HOST rolls it (what drops
// is not something a pilot may decide), one set per pilot in the party, and sends each pilot only
// its own; each machine draws only its own crates. Nobody competes for a drop, and a guest's loot
// is its own whoever hosted.
//
//   crates   2 per pilot, 3 from boss level 6, 4 from level 11
//   tier     the level's base tier, 1 + floor((L-1)/4), rolled 20% one lower, 70% on it, 10% one
//            higher, held to T1-T10: tier t first drops at L 4t-7 (T2 at L1, T10 at L33) and is the
//            main drop over L 4t-3 to 4t (numbers_curve_raids_items.md §3.2; Par's rows assume it)
//   parts    70% from the pilot's own HULL CATEGORY (its lines and the chips), 30% any part that drops
//
// The drops are on the pilot's file from the moment of the kill (Character.Unclaimed), so a quit,
// a crash or a lost host costs nothing: the next world the pilot enters claims them (ClaimAll).
// Flying over a crate takes it at once -- the hold has it -- and the file follows 2.5 s after the
// LAST pickup (Character.SaveSoon), so a run of pickups is one write.
// ─────────────────────────────────────────────────────────────────────────────
public static class Loot
{
    public const float Ring = 240f;                  // the crates lie on a ring this far from where the boss died
    private static readonly Random Rng = new();

    public static int CratesFor(int bossLevel) => 2 + (bossLevel >= 6 ? 1 : 0) + (bossLevel >= 11 ? 1 : 0);

    // THE DROP ROW: a tier every TierEvery levels, the roll either side of it, the own-category share.
    public const int TierEvery = 4;
    public const double Lower = 0.20, Higher = 0.10, Own = 0.70;
    public static int BaseTier(int bossLevel) => Math.Clamp(1 + (Math.Max(1, bossLevel) - 1) / TierEvery, 1, Tiers.Count);
    public static int RollTier(int bossLevel, Random rng)
    {
        double r = rng.NextDouble();
        return Math.Clamp(BaseTier(bossLevel) + (r < Lower ? -1 : r >= 1 - Higher ? 1 : 0), 1, Tiers.Count);
    }

    // Host: every pilot's drops for a boss of `level`, by peer.
    public static Dictionary<int, string[]> RollParty(IEnumerable<(int peer, ShipClass cls)> pilots, int level, Random rng = null)
    {
        rng ??= Rng;
        var all = Equipment.Drops.ToList();
        var d = new Dictionary<int, string[]>();
        if (all.Count == 0) return d;          // nothing in the game drops: no crates, not an index into an empty list
        foreach (var (peer, cls) in pilots)
        {
            var drops = new string[CratesFor(level)];
            for (int i = 0; i < drops.Length; i++)
            {
                int tier = RollTier(level, rng);
                bool own = rng.NextDouble() < Own;
                // "the pilot's own category" is a PREFERENCE, not a filter. As a filter the narrowed
                // pool could come out EMPTY, and rng.Next(0) returns 0, so this would index an empty
                // list: on the HOST, at the instant a boss died. It falls back to everything of that
                // tier, and that to everything that drops at all.
                var cat = Hulls.Of(cls);
                var byTier = all.Where(it => it.Tier == tier).ToList();
                var mine = own ? byTier.Where(it => it.Cat == null || it.Cat == cat).ToList() : byTier;
                var pool = mine.Count > 0 ? mine : byTier.Count > 0 ? byTier : all;
                drops[i] = pool[rng.Next(pool.Count)].Id;
            }
            d[peer] = drops;
        }
        return d;
    }

    // What arrives off the wire: parts that can drop, and no more than a boss gives.
    public static string[] Sanitize(string[] ids, int max) =>
        (ids ?? Array.Empty<string>()).Where(id => Equipment.ById(id) is { Kit: false }).Take(Math.Max(0, max)).ToArray();

    // Every drop not yet flown over, into the hold -- entering any world.
    public static void ClaimAll()
    {
        if (Character.Unclaimed.Count == 0) return;
        foreach (var id in Character.Unclaimed) Character.Stow(id);
        Character.Unclaimed.Clear();
        Character.Save();
    }

    // A crate flown over.
    public static void Take(string item)
    {
        if (!Character.Unclaimed.Remove(item)) return;
        Character.Stow(item);
        Character.SaveSoon();
    }
}

// One of this pilot's crates. It exists on this machine only -- nobody else's drops are here to
// see -- so it needs no network: flying over it is enough. A pilot in stasis collects with its pod.
public partial class LootCrate : Node2D
{
    public Hub Hub;
    public string Item;
    private const float Size = 26f, PodReach = 24f, NameReach = 500f;
    private float _t;

    public override void _Ready() => ZIndex = 6;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
        var me = Hub?.MyShip;
        if (me == null) return;
        bool over = me.Alive ? me.Covers(Position, Size * 0.5f) : me.ViewPosition.DistanceTo(Position) <= PodReach;
        if (!over) return;
        Loot.Take(Item);
        Hub.CrateTaken(this);
        Hub.Hints.Meet("equipment");
        QueueFree();
    }

    public override void _Draw()
    {
        var it = Equipment.ById(Item);
        var col = Ui.TierColor(it?.Tier ?? 1);
        float pulse = 0.55f + 0.45f * Mathf.Sin(_t * 3f);
        DrawCircle(Vector2.Zero, Size * 1.3f, col with { A = 0.12f * pulse });
        var box = new Rect2(-Size / 2, -Size / 2, Size, Size);
        DrawRect(box, Ui.Deep);
        DrawRect(box, col, false, 2f);
        DrawLine(new Vector2(-Size / 2, 0), new Vector2(Size / 2, 0), col with { A = 0.7f }, 1.5f);
        var me = Hub?.MyShip;
        if (it != null && me != null && me.ViewPosition.DistanceTo(Position) < NameReach)
            Txt.Centre(this, ThemeDB.FallbackFont, new Vector2(0, -Size), it.Name, Txt.Size(13), col);
    }
}
