// THE BOUNTY BOSS OF THE ODD LEVELS (Missions.ForLevel), shown to a player as RUSTY BUCKET. This
// file and its id keep the Lancer name because the ID IS A SAVE KEY ("silver_lancer", written into
// [boss_cleared] by every build there has ever been); renaming either would orphan every pilot's
// ladder. The name a player reads is the one field of its row (Missions.Bosses) -- FIVE ROWS, and the
// class that held them is gone. It was a Boss subclass whose Tick was five open-coded clocks, each
// the same shape written out again; Boss now runs any table of moves and this is the Lancer's.
//
// One rhythm, 30 s long (all damage x the level's and party's scale). EVERY MOVE BUT THE BURN is cut
// to x0.744 of its old row (numbers_curve_raids_items.md §1.2): with no chips a par pilot who stops
// dodging lives 31 s at every level; the burn (50 a tick, 250 whole) is a super and is kept:
//   GUNS       always: 2.68 every 1.2 s (2.23 DPS) at the nearest ship within 900 u
//   DEATH BEAM every 30 s, in three steps, the boss holding position through all of them:
//              ESCORTS OUT: two escorts go for the nearest pilot to web it; the boss turns to
//                face that pilot -- the only turning the beam allows, and only now.
//              CHARGE: once the web has actually PINNED the pilot (this cycle's escorts under way;
//                or, the escorts all down, once their web would have landed; or when they are
//                overdue), a red line down the nose for 6 s. From here to the beam's end it is
//                HARD LOCKED: no turn, no move -- the line shown is the line fired, and the way
//                out is to leave it.
//              LIVE: 3 s along that line, checking every 0.25 s: 50 each time it lands on a ship
//                (a ship's 0.52 s invulnerability to one source means a hit about every 0.75 s)
//   RAM        15 s after each beam (never during one): a red line for 1.5 s, then a ram along it
//              at 1200 u/s: 29.8 to any ship in its path -- the one move that shifts the hull
//   TRIDENT    between them (every 15 s from 13.5 s): 3 guided missiles, 0 and +-25 degrees,
//              11.2 each, twice the size -- INTERCEPTABLE (PD shoots them). ALL THREE LAND: each
//              seeker is its own damage source (Shots.SourceKey), where one shared name meant a
//              hull felt only the first inside its 0.52 s gap.
//   SHOCKWAVE  every 17 s: a red ring for 1.8 s, held still, then 33.5 within 340 u
// Slow and heavy: between its moves it closes on the party to about 650 u and turns ponderously
// (its Missions.BossType row, which carries the hull's art and shape as well).
public static class Lancer
{
    public static readonly BossMove[] Moves =
    {
        // ALWAYS, through everything else it is doing -- as the Drake's main gun fires through its
        // own specials. It waits on nothing, so it needs no idle hull to fire from.
        new() { Id = "guns", Way = MoveWay.Bolt, Waits = MoveWait.Nothing,
                Every = 1.2, First = 2.0, Damage = 2.68, Find = 900f,
                Source = DamageSource.LancerGuns, Beam = Beam.Bolt },

        // THE TRIDENT, between the supers: three seekers 25 degrees apart, born 14 u ahead of the
        // nose, twice a missile's size, turning at 1.4 rad/s after the ship they were thrown at.
        new() { Id = "trident", Way = MoveWay.Shoot, Waits = MoveWait.Nothing, Shot = Shots.Seeker,
                Every = 15, First = 13.5, Count = 3, Spread = 25f,
                Damage = 11.2, Speed = 135f, Range = 1920f, Turn = 1.4f, Size = 2f, Muzzle = 14f,
                Cue = "boss_trident", Source = DamageSource.LancerMissiles },

        // THE DEATH BEAM. Its escorts are two Webifiers 45 degrees to port and starboard, launched
        // 130 u off the hull (was HalfWidth + 60), 3 hull each -- 3 s at one point-defence turret,
        // so a pilot can clear them inside the charge -- boosting for the charge's whole 6 s. The
        // charge starts on the PIN; with every escort dead, 1 s past their predicted web; at the
        // outside 1 s past that, and never later than 5 s (under the 6 s that would push the beam
        // into the ram's slot). Then 6 s of red line, 10000 u long and 70 u wide, and 3 s of burn
        // judged every 0.25 s for 50. Only ITSELF holds it off: a beam already running.
        new() { Id = "beam", Way = MoveWay.Beam, Waits = MoveWait.Itself, Busy = true, Super = true,
                Every = 30, First = 6.0, Windup = 6.0, Live = 3.0, Tick = 0.25,
                Damage = 50, Reach = 10000f, Width = 70f,
                Escorts = 2, EscortKind = Enemies.Webifier, EscortAngle = 45f, EscortOut = 130f,
                EscortHull = 3, WebGrace = 1.0, WebSlack = 1.0, ArmMax = 5.0,
                Cue = "boss_beam_charge", Strike = "boss_beam", Source = DamageSource.LancerBeam },

        // THE RAM, 15 s after each beam: 1.5 s of red line 900 u long and 140 u wide (the hull's
        // own beam, 2 x its 70 u half-width), then the hull down it at 1200 u/s for 29.8 on contact.
        // ITS OWN CADENCE. It read the beam's 30 s constant (`_charge = BeamEvery`): the two are
        // the same number BY ACCIDENT, not by design, and tuning the beam retuned the ram.
        new() { Id = "ram", Way = MoveWay.Dash, Waits = MoveWait.Everything, Busy = true, Super = true,
                Every = 30, First = 21.0, Windup = 1.5,
                Damage = 29.8, Reach = 900f, Width = 140f, Speed = 1200f,
                Strike = "boss_ram", Source = DamageSource.LancerCharge },

        // THE SHOCKWAVE, every 17 s. Both of these were bare literals in the method that fired it.
        // It waits only on the moves that SHIFT the hull: it is centred where the boss stands, and
        // a boss that rammed out of its own ring would leave it behind. Not a super: the bar under
        // the hull counts down to the beam and the ram, and always did.
        new() { Id = "wave", Way = MoveWay.Ring, Waits = MoveWait.Movers, Busy = true,
                Every = 17.0, First = 10.0, Windup = 1.8, Damage = 33.5, Reach = 340f,
                Strike = "boss_shockwave", Source = DamageSource.LancerWave },
    };
}
