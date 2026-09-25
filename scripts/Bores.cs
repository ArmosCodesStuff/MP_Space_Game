using Godot;
using System;

// ─────────────────────────────────────────────────────────────────────────────
// BORES — a gun fired DOWN THE NOSE from fixed rails, never from a turret.
//
// Replaced: the Dart's single light turret (the Mk I Dart Cannon on the turret path, FireControl). A
// bore gun has no mount that swings: each round leaves a rail along the nose at the row's Speed PLUS
// the ship's own velocity, so a hull that slides throws its rounds with it; what it does in flight is
// its Shots row's (the Pepperbox's darts are Commanded onto the pilot's live cursor, ShotDef.Command).
//
// A row fills in: Shot (a Shots.All index), the stat ids Damage / Every / Speed / Range / Turn, Rails
// (the lateral offsets fired in turn), and optionally PriceTop / PriceCap -- the damage is priced from the
// firing ship's TopNow on the HOST at the launch: x clamp(TopNow / PriceTop, 1, PriceCap) (kits_v3 §3.6,
// numbers §8 R6: every top-speed lift prices its guns, the boost and gear included). Credit: the row's
// Shots Id through Dealt (Shot.Strike). A Hold weapon row (AbilityDef.Bore) fires while the trigger holds
// (Tick, host); a row's Parting spec fires one round as its run ends (PlayerShip.Part, Launch) -- the Rod from God,
// priced at the top the run gave, and its Recoil is the speed the hull keeps after.
// ─────────────────────────────────────────────────────────────────────────────
public class BoreSpec
{
    public int Shot;
    public string Damage, Every, Speed, Range, Turn;   // stat ids; Turn null = a straight round
    public string PriceTop, PriceCap;                   // stat ids; null = unpriced
    public string Recoil;                               // stat id: the share of speed kept after a parting round; null = none
    public float[] Rails = { 0f };                      // u to the side of the nose, fired in turn
}

public static class Bores
{
    // THE PRICE, pure: `damage` x clamp(top / priceTop, 1, cap). Provable on literals (LaneA6dPepperChecks).
    public static double Price(double damage, double top, double priceTop, double cap)
        => damage * Math.Clamp(top / Math.Max(1e-6, priceTop), 1.0, Math.Max(1.0, cap));

    // What one round of `row` deals fired from `s` now (host): its damage stat, priced if the row prices.
    public static double DamageOf(PlayerShip s, BoreSpec row) => DamageAt(s, row, s.TopNow);
    public static double DamageAt(PlayerShip s, BoreSpec row, double top)
        => row.PriceTop == null ? s.Stats[row.Damage]
                                : Price(s.Stats[row.Damage], top, s.Stats[row.PriceTop], s.Stats[row.PriceCap]);

    // WHERE A ROUND LEAVES AND HOW FAST, pure: from the nose, `rail` u to the side, at `speed` along the nose
    // plus the ship's velocity -- the launch vector, whose size is the round's speed and whose bearing its Dir.
    public static (Vector2 from, Vector2 v) Muzzle(Vector2 at, float rotation, float half, float rail, float speed, Vector2 shipV)
    {
        var nose = Vector2.Up.Rotated(rotation);
        var side = new Vector2(-nose.Y, nose.X);
        return (at + nose * half + side * rail, nose * speed + shipV);
    }

    // ONE ROUND of `row` off rail `rail` (host). `damage` is the round's, already priced.
    public static Shot Launch(PlayerShip s, BoreSpec row, int rail, double damage, string weapon)
    {
        var (from, v) = Muzzle(s.Position, s.Rotation, s.MyArt.Length * 0.5f, row.Rails[Math.Abs(rail) % row.Rails.Length],
                               (float)s.Stats[row.Speed], s.Velocity);
        bool steered = row.Turn != null && Shots.Of(row.Shot).Command > 0;
        return Combat.Fire(row.Shot, from, v, v.Length(), (float)s.Stats[row.Range], damage,
                           targetId: steered ? s.NetId : 0, turnRate: steered ? (float)s.Stats[row.Turn] : 0f,
                           source: s, hitSource: weapon);
    }

    // THE TRIGGER (host): while `def`'s trigger holds, one round every Cadence(Every) seconds, the rails in turn.
    // The clock is the slot's Own and CARRIES its remainder (the guns' rule); N is the next rail. Idle, it reloads.
    public static void Tick(PlayerShip s, AbilityDef def, bool firing, double delta)
    {
        if (def.Bore is not { } row) return;
        ref var sl = ref s.Sl(def.Id);
        if (!firing) { sl.Own = Math.Max(0, sl.Own - delta); return; }
        sl.Own -= delta;
        for (int n = 0; sl.Own <= 0 && n < 16; n++)
        {
            Launch(s, row, sl.N, DamageOf(s, row), def.Id);
            sl.N = (sl.N + 1) % row.Rails.Length;
            sl.Own += s.Cadence(row.Every);
        }
    }
}
