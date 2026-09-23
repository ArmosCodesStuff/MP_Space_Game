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
//   rarity   L1-5 Common; L6-10 Common 70 / Rare 30; L11+ Common 55 / Rare 30 / Epic 15
//   parts    70% one for the pilot's own class (or a general part), 30% any part that drops
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

    private static Rarity RollRarity(int level, Random rng)
    {
        double r = rng.NextDouble();
        if (level >= 11) return r < 0.15 ? Rarity.Epic : r < 0.45 ? Rarity.Rare : Rarity.Common;
        if (level >= 6) return r < 0.30 ? Rarity.Rare : Rarity.Common;
        return Rarity.Common;
    }

    // Host: every pilot's drops for a boss of `level`, by peer.
    public static Dictionary<int, string[]> RollParty(IEnumerable<(int peer, ShipClass cls)> pilots, int level, Random rng = null)
    {
        rng ??= Rng;
        var all = Equipment.Drops.ToList();
        var d = new Dictionary<int, string[]>();
        foreach (var (peer, cls) in pilots)
        {
            var drops = new string[CratesFor(level)];
            for (int i = 0; i < drops.Length; i++)
            {
                var rarity = RollRarity(level, rng);
                bool own = rng.NextDouble() < 0.7;
                var pool = all.Where(it => it.Rarity == rarity && (!own || Equipment.Fits(it, it.Slot, cls))).ToList();
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
        var col = Ui.RarityColor(it?.Rarity ?? Rarity.Common);
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
