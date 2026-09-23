// THE DRAKE BASTION -- the bounty boss of the even levels (Missions.ForLevel): THREE ROWS, and the
// class that held them is gone. It was written as a second copy of the Lancer's shape -- the same
// five-clock Tick, its own header recording that its numbers were forked from the Lancer's -- and
// that copy is what made a table overdue. Boss runs any table of moves; this is the Drake's.
//
// A heavy scrapper: no escorts, a lower, steadier pressure than the Lancer, and two specials on
// the Lancer's own clocks, all damage x the level's and party's scale:
//   MAIN GUN       always: a slow shell (220 u/s) every 2.5 s at the nearest ship within 1100 u,
//                  6 each (2.4 DPS) -- straight, and dodged by moving
//   SCRAP SHOTGUN  every 15 s (the trident's cadence), from 17 s: it WARPS to range -- a ring
//                  where it will land, 1 s -- 600 u from the nearest pilot, then a fan of seven red
//                  lines for 1.875 s, then seven pieces of scrap down them at once -- the same
//                  fixed fan every time, 10 degrees apart -- 18.75 each, each its own hit
//   ASTEROID THROW every 30 s (the death beam's slot, from 6 s): a big rock appears beside it and
//                  its tractor beam takes hold; a red lane to the target for 7.5 s; then the rock
//                  is hurled down it -- slow to start, then very fast -- striking every ship in
//                  the lane for 250, and breaking apart at the lane's end
// It holds still through its specials -- the warp is the shotgun's own opener -- and between them
// closes on the party like any boss. The two never run back to back: a throw holds it 9.1 s (7.5 s
// of warning, 1.6 s of flight), so the shotgun comes 11 s after each throw starts (and 4 s after
// it, in the cycle's other half), never on the trident's own 13.5 s, which fell inside the first
// throw.
//
// ITS FIGURES ARE ITS OWN. They were once written as 1.25 x a Lancer constant (a "threat
// multiple"), so tuning the Lancer silently retuned this boss as well -- and the two are not the
// same fight. Where a number came from is a comment; the numbers here are the Drake's alone.
public static class Drake
{
    public static readonly BossMove[] Moves =
    {
        // ALWAYS, through its own specials: a dodgeable round, never a point-defence target,
        // because a round that is MEANT to be dodged and that point defence could delete would
        // never need dodging (Shots.Slug carries its own report, "drake_gun").
        new() { Id = "gun", Way = MoveWay.Shoot, Waits = MoveWait.Nothing, Shot = Shots.Slug,
                Every = 2.5, First = 2.0, Find = 1100f,
                Damage = 6, Speed = 220f, Range = 1300f, Radius = 7f, Source = DamageSource.DrakeGun },

        // THE SCRAP SHOTGUN: the warp opener first -- a ring 117 u across (was HalfWidth x 1.3)
        // for 1 s, 600 u off the nearest pilot -- then seven red lines 1400 u long and 28 u wide
        // (was ScrapRadius x 2 + 10) for 1.875 s, then the seven pieces down them at once.
        new() { Id = "shotgun", Way = MoveWay.Shoot, Waits = MoveWait.Everything, Busy = true, Super = true,
                Shot = Shots.Scrap, Every = 15, First = 17.0,
                Warp = 1.0, Standoff = 600f, WarpRing = 117f, WarpSound = "drake_warp",
                Windup = 1.875, Count = 7, Spread = 10f, Width = 28f,
                Damage = 18.75, Speed = 480f, Range = 1400f, Radius = 9f,
                Strike = "drake_scrap", Source = DamageSource.DrakeScrap },

        // THE ASTEROID THROW: a 90 u body 220 u off the flank (was HalfWidth + RockRadius + 40),
        // a lane 1800 u long and 180 u wide (the body's own width) held 7.5 s, then 1.6 s of
        // flight. ThrownRock flies it and names the blow it deals.
        new() { Id = "throw", Way = MoveWay.Throw, Waits = MoveWait.Everything, Busy = true, Super = true,
                Every = 30, First = 6.0, Windup = 7.5, Flight = 1.6,
                Damage = 250, Reach = 1800f, Width = 180f, Radius = 90f, Offset = 220f,
                Cue = "drake_tractor", Strike = "drake_throw" },
    };
}
