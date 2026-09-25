# RAIDS v2: gank squads and boss-fight adds (for sign-off)

Merges `formations.md` and `boss_adds.md` into one spec. Where the two disagreed, this file picks one answer and says so (§0b). This is read-only work: nothing in the repo was edited, built or run.

**Corrected to the owner's ruling: a heavy never owns CC, at any level.** Its CC is the webifier lights in its squad. The first draft gave heavies a tractor from L20 (old D5); that is gone from every section below, and §3 and §6 were re-run without it (`raids_v2_model.fight(..., heavy_web=999)`). The pre-ruling draft is `raids_v2.pre_ruling.md`.

- Line numbers are from the working tree: `de65f5a` plus the uncommitted hunks in Boss/Drake/Ships/Sky/SmokeTest.
- The numbers come from `raids_v2_model.py`, which re-runs `adds_model.py` under the merged spec.

**Scope: a new table.**
- **The foundation** is `Squads.cs`: doctrine rows, a host-only runtime `Squad`, and one contract, `ISquadMember`.
- **Everything else is rows and fields** on tables that already exist: `Waves.All`, `Enemies.All`, `Missions.Kinds`, `WaveDef`, `WaveCrew`, `WaveBrief` and `Lead`.
- **On the wire:** one new reliable RPC, plus bits inside the `int flags` the raider packet already carries.

---

## 0 · FACTS THIS RESTS ON (each checked in the source)

### 0a · Verified

| # | fact | where |
|---|---|---|
| 1 | **An unpinned heavy never fights.** It flies to `EdgeSpot` = `BasePos + dir*4200` and waits there. Both its laser and its approach are gated on the pin. In the arena that puts it about 3500 u from the fight | Raider.cs:303-319, Hub.cs:43, 64, 1096 |
| 2 | **Its missile ignores the pin.** It fires within `MissileRange` whatever the pin state. The flight is the `Raider.MissileFlight = 12.0` const | Raider.cs:78, 331-342 |
| 3 | **Posts wrap after three.** A light's post is its rank among the lights on that target, taken `% 3`, so a 4th light stacks on the 1st | Raider.cs:98, 241-245 |
| 4 | **Warp snap.** A latched light closes on its post at `Max(top, d*12)`. A 1200 u warp therefore moves it 240 u in one frame | Raider.cs:256 |
| 5 | **The web is hard-coded** as `ApplyStatus(Status.Pinned, 0.25)` | Raider.cs:263 |
| 6 | **A squad exists only at the spawn.** `Send` places the crew at `at + c.At + c.Step*(i-(n-1)/2)` in world axes. After that the only link is `Patrol = serial` | Raids.cs:124-137 |
| 7 | **An idle patrol circles `Station`**, which defaults to `Hub.BasePos`. So the siege garrison circles (0,0), not the base it defends | Raider.cs:87, 216, 289-299; Raids.cs:136 |
| 8 | **A boss kill sweeps every live raider through the same path as a kill.** `MissionCleared` calls `RaiderDown` on each, with a burst and their remaining hull (> 0). A kill passes 0. `CallOff` passes `burst: false` | Hub.cs:931-935, 1186; Raids.cs:100-105; Raider.cs:168-173 |
| 9 | **Raider flags ride the unreliable channel.** `NetRaiders` is `UnreliableOrdered`, at 10 Hz, 24 raiders per packet | Hub.cs:1249, 1267-1271, 1281-1282 |
| 10 | **The bounty has no garrison clock.** Its `FirstWave` and `WaveEvery` are 0; the siege's are 12 and 25. The clock lives in `Hub.TickArena` | Missions.cs:121, 130-146; Hub.cs:195, 983-987 |
| 11 | **`Waves.For` does not know the mission.** With a clock on the bounty, it would draw the siege's rows | Waves.cs:274-279 |
| 12 | **The beam charges on any pin of its target**, not only its own escorts'. Only the RUSTY BUCKET has escorts | Boss.cs:380; Lancer.cs:58; Drake.cs (none) |
| 13 | **Escort growth is the owner's call** (CHANGES item 6): +1 every 10 levels, up to 5 | Boss.cs:521-524; CHANGES.md:339-341 |
| 14 | **Raiders pay no EXP.** EXP comes only from `AwardClear`. `WorthExp` pays nothing under half the pilot's level. `KillExp = 300` | Progression.cs:175-190; Missions.cs:229-238 |
| 15 | **The kinds are not random per fight.** `_roll` starts at 0 in every world, and every sector is a new Hub scene. `KindOf` uses the same roll for both ways, so squad 1 is always webifier+gunship, squad 2 talon+cross, squad 3 pod+lancerkin | Raids.cs:32, 122; Waves.cs:292-297; Hub.cs:898, 1098 |
| 16 | **`Lead` already detects jumps**: a move of 50 u or more in one frame. It throws the fact away | Missiles.cs:71, 115-125 |
| 17 | **Adding an RPC or a const changes `Net.Protocol`.** The fingerprint hashes every `[Rpc]` signature, every static literal and every readonly table | Net.cs:169-186 |

### 0b · Corrections to the inputs (and what this file does instead)

| input said | actually | here |
|---|---|---|
| formations: an unsquadded spawn is a solo **gank** squad, "today's behaviour exactly" | Gank prefers player ships. SmokeTest:3817-3821 spawns a light 120 u from a miner while the pilot is up elsewhere, and asserts `r4.Target == mnr`, so that check would fail | A 4th doctrine row, **`lone`** (nearest, no preference), for unsquadded spawns |
| formations: add EXP reaches guests through a `FlagPays` bit | Flags travel unreliably (fact 9). An add killed before a guest's first packet would pay the host but not that guest | A reliable `NetKillExp` |
| both: adds form up at a fixed 2600 u | A pod squad cruises at 78 u/s and commits at 949 u, so it engages after **24 s** (the others take 9-13 s). The model then makes L33-35 easier than L30-32 (x1.38 against x1.44), and most of squad 3's EXP goes unpaid | **`WaveDef.FormFor` = 10 s**: every squad spawns 10 s of its own pace outside its commit range, and so engages about 13 s after it appears |
| both: kinds are "drawn from the rotation" (variance) | They are fixed per slot (fact 15). A refill would draw the next roll and change its squad's kinds | **A slot keeps its draw.** A refill brings the same kinds |
| formations Q1 against boss_adds R1: gate the beam, or keep it | Gating on the boss's own escorts delays the charge from about 1 s to the escorts' latch or `Overdue` (at most 5 s). That adds about 3-4 s before the windup | Default: **gate it** (D2). §6 R1 has the numbers |
| boss_adds: strip times (6.5-8 s) used the mean light hull, 34 | Squad 1 is webifiers, hull 25 | 3 webifiers take 3.5-4.0 s; with the gunship, 5.5-6.6 s |
| boss_adds: Waves.cs line numbers | The file is 298 lines long | 274-279 and 174 |
| formations `WaveBrief.Squad` / boss_adds `WaveDef.Roster` | Both cut a roster into squads | **`Roster` only**: the builder deals N out in crew order |
| formations `EnemyDef.Cc/CcFrom` / boss_adds `EnemyDef.WebFrom` | Both are the CC gate | **`Cc` only**, named for the mechanism. No level gate: heavies have no CC at any level (ruling), so a `CcFrom` would be 1 on every row that has a `Cc` |
| formations: adds form on a ring round the party; boss_adds: on a Fan round the boss | - | A **Fan off the party-centre → boss line**, 0.35 rad per slot, alternating sides |

---

## 1 · FORMATION SPEC

### 1a · The row: `SquadDoctrine` (Squads.All)

| field | default | meaning |
|---|---|---|
| `Id` | - | reached by id |
| `Detect` | 0 | 0 means take a target at once. Above 0, hold the ring until something comes this close to the **anchor** |
| `Pick` | Nearest | `Nearest` or `Loneliest`: fewest friendly hulls within 800 u, then nearest |
| `Prefer` | none | a `TargetFilter`, taken first whenever one is up |
| `Spread` | false | prefer a target no other squad of the wave holds, if it is within 1.5x the nearest |
| `CatchUp` | 1.5 | a member off its slot flies at up to this times its own cruise |
| `CommitAt` | 0 | 0 means auto: the smallest `BoostAtMine` among its pinners, or its own for a squad without one |
| `Leash` | 1.5 | a target further than this x `CommitAt` from the squad's centre means re-form |
| `Reboost` | 10 s | from one burn to the next. A re-commit inside it closes at cruise |
| `Front` | 0, ∓90, ∓45, ∓135, ∓22.5, ∓67.5, ∓112.5 | light posts, in degrees off the target's bow (13). The first three are today's |
| `Rear` | 180, ∓155, ∓130 | standoff posts (5). They keep ±25° astern clear of lights |
| `LaserNeedsPin` | false | a standoff member holds its gun until the target is pinned |
| `MissileNeedsPin` | true | the missile flies only at a pinned target (triage #24) |
| `Shiver` | 0 | seconds it is held at the launch point first (escorts) |

| row | differs from the defaults | flown by |
|---|---|---|
| `lone` | - | unsquadded `SpawnRaider` (tests, stray spawns) |
| `patrol` | Detect 2000 | raid, patrol (Called), siege, blockade |
| `gank` | Pick Loneliest, Prefer players, Spread | **bounty_adds**; hunts (their Quarry comes first) |
| `escort` | Shiver 1.0, CommitAt ∞, Front {-90, 90} | a boss move's escorts (fold-in of `IsEscort`/`Escort()`) |

- **Runtime:** a `Squad` holds Members, Slots, Anchor, Heading, Phase (Hold / Approach / Commit / Engaged), Target, Dark, Quarry, Station, Circuit and Lead.
- **Where it lives:** `Raids` holds the live list and the per-target post book. `Raids.Tick` already runs in `Hub._Process` (Hub.cs:1468), before the raiders' own `_Process`.
- **`Station` defaults to the brief's `Origin`, not `Hub.BasePos`.** Every home brief's Origin is already `BasePos`, and a blockade already sets `Hold`, so nothing changes at home. It fixes the arena garrison (fact 7).

### 1b · Approach, commit, engage

| phase | rule |
|---|---|
| **Shape** | Gank/Patrol crew in the squad frame (−Y is forward). Lights in a vee: point (0,-40), wings (±60,-10), from `Step (60,0)` and `Sweep 30`. The heavy at (0,60), close astern. Every slot is within 62 u of the spot, so the raid check "within 110 u of RaidEdge" (SmokeTest:6005) still holds |
| **Pace** | The anchor moves at the **slowest living member's** `Cruise x Agility`: 100 u/s for most squads, 78 with a pod |
| **Heading** | It turns at `(CatchUp-1) x pace / largest slot radius`, which is 0.5 rad/s for the gank shape. Members fly to their slots at `min(1.5 x cruise, d*6 + pace)` and never wait for a straggler |
| **Hold** | With `Detect > 0`, the anchor circles `Station` at `Circuit`, at 100 u/s (1.5x off the ring), and moves to Approach on a target within `Detect` of the anchor |
| **Form-up** | A row with `FormFor > 0` spawns each squad at `CommitAt + pace x FormFor` on its Form's bearing. At 10 s: webifier squad 2600 u · talon 3040 · pod 1729 · lone gunship 3250 · lone cross 2940 · lone lancerkin 2910 |
| **Commit** | Starts at `Gap(anchor, target) <= CommitAt`: webifier squad 1600 u · talon 2040 · pod 949 · lone gunship 2250 |
| **Posts** | Each member takes the lowest free post of its way from the book: lights from `Front`, heavies from `Rear`. A post is **sticky**: it is held until death or re-form, and nobody reshuffles. 9 lights and 3 heavies on one hull get 12 distinct posts |
| **Time on target** | `T = max over members of (distance to post / burn)`, where burn = `Cruise x BoostMult x Agility`: webifier 500 · talon 650 · pod 273 · gunship 700 · cross 600 · lancerkin 550. Each member flies at `min(burn, d / max(T_left, 1/60))`, so **all arrive together** and every boost flag rises in the same frame. For a solo member this reduces exactly to today's burn |
| **Station keeping** | **Capped at `burn`**, which replaces `Max(top, d*12)`. This kills the warp snap |
| **Lights, engaged** | Latched (within 12 u of the post and `Gap <= Reach`): apply the row's `Cc` (if it has one) for 0.25 s, then fire `Def.Dps` |
| **Heavies, engaged** | Rear post at `Def.Hold` (gunship 135 u, lancerkin 234). They **laser whether or not the target is pinned**, twin laser at 1.25x a light's DPS (ruling). The **missile flies only at a pinned target**, by anyone's web, with its row's range and cadence and a **10 s flight** (row). **A heavy never applies CC of its own** (ruling): what pins its target is the webifiers flying with it |

### 1c · Break conditions

| event | detected by | the squad |
|---|---|---|
| **leader dies** | the lead leaves `Members` | Command passes to the next Standoff member, else the lowest NetId. The anchor is virtual, so nothing moves and the empty slot stays empty. Pace is recomputed (kill the pod and the squad speeds up) |
| **target flees** | `Gap(centroid, target) > Leash x CommitAt` (2400 u for webifiers) | Re-forms at the survivors' centroid, releases its posts and returns to Approach. Inside `Reboost` it closes at cruise |
| **target jumps** (warp, Rewind, Shadow step) | `Lead.Jumped` (≥ 50 u in a frame) | Re-forms at once. The capped station keeping means no member moves more than `burn/60` in a frame |
| **target dies, hides or leaves** | `!Raider.Up(Target)` | Re-picks, keeping it as `Dark` (taken back the moment it is seen), and re-forms toward the new target |
| **losses** | members leave | It **fights on**: no merge, no rout. Heavies still laser without a pin |
| **boss dies** | `MissionCleared` downs every raider | Empty squads are pruned. They pay nothing (§2c) |

### 1d · CC gating: a row, not an `if`

- **Pin rows** (webifier, talon, pod) are `Cc = Pinned`. **Standoff rows** (gunship, cross, lancerkin) have **no `Cc`, at any level** (ruling). "Heavies get CC later" is the schedule, not a row: an L6-8 add is a lone heavy, and from L9 webifiers fly with it.
- Both ways run the one line at Raider.cs:263, now reading the row. There is no new status and no new bit, because guests already receive `Pinned`.
- `EnemyWay` now means only how a member closes and where it posts. Rewrite the header at Enemies.cs:12-15.
- **`Raider.Level` is host-only.** `Raids.Send` sets it from `b.Level`, as it sets `Agility`:

| wave | its Level |
|---|---|
| bounty adds | the boss level |
| raid | the failed level |
| siege | the siege level |
| blockade | the highest boss beaten |
| hunt | `(int)Threat`; today it takes `Missions.Level`, which a hunt row never reads |
| called patrol, lone spawn | 1 |

### 1e · Wire and readability

| bits of `NetFlags` | meaning | drawn as |
|---|---|---|
| 1, 2 | boost, shiver (unchanged; SmokeTest:5592-5597 asserts these literals) | plumes |
| 4 `FlagLead` | leads its squad | link lines, radar bracket |
| 8 `FlagLock` | tether is a lock line (committing) | dashed red lines converging on the victim for the burn (about 3 s) |
| - | whether it webs: **no bit**. The guest already has the kind by index (`NetRaiderSpawn`, Enemies.cs:70) and reads the row's `Cc` | a light's web: thin light-red tether. **A heavy draws no tether**, only its laser flash |
| bits 8-23 | squad id & 0xFFFF | grouping on guests |

- **Behaviour needs nothing on the wire.** Squads, slots, posts and the anchor are host bookkeeping, and packet size and RPC shapes are unchanged.
- **Readability:**
  - A vee with the heavy astern holds its shape for about 10 s.
  - Faint link lines to the leader, alpha 0.25, **vanish at commit**.
  - The victim's HUD shows a line such as `GANK: GUNSHIP + 3 WEBIFIER`.
  - Each row's `Name` shows under its hull for 3 s when the squad enters view and again at commit, with a web glyph if its row has a `Cc`.
- **Radar** (Radar.cs:96-103 draws every hostile as a 2.6 px dot today):
  - `Tag.Heavy` is drawn as a diamond.
  - A squad in formation gets a bracket.
  - An off-scope squad is one rim chevron, labelled `1+3`.
- **Lifetime (invariant A):** `Raider._ExitTree` calls `Squad.Leave`, which releases the member's post, and `Raids.Tick` prunes empty squads. Leaving the arena leaves 0 squads and an empty post book.

---

## 2 · BOSS-FIGHT SCHEDULE (levels 1-40)

### 2a · Rules

- **Count:** `N(L) = L < 6 ? 0 : min(12, (L-6)/3 + 1)`.
- **Order:** enemies are dealt **H, L, L, L, H, L, L, L, H, L, L, L** into squads of the crew (1 standoff + 3 pinners), so there are `ceil(N/4)` squads.
- **Cap:** 3+9 is reached at L39. L40 and above stay at 12 while `S(L)` keeps climbing.
- **Arrival:** slot 1 comes at t = 0 (`FirstWave = 0`). Slot k+1 comes when the boss's hull is at or below `1 - k/squads`, **or** at `k x 30 s`, whichever is first.
- **Refill:** a slot whose last member falls returns `WaveEvery = 30 s` later, with **the same kinds**. It never pays EXP.
- **Party:** the count is the level's alone. Each add takes the boss's multipliers: `Strength = DamageMult(L,P)` and `HullShare = HullMult/DamageMult`. That is hull x(1+0.6(P-1)) and damage x(1+0.2(P-1)).
- **Form-up:** a Fan off the party-centre → boss line, 0.35 rad, alternating sides by slot, at `FormFor = 10 s` (§1b). Engagement comes about 13 s after the squad spawns.
- **Target:** the gank doctrine: the loneliest pilot, spread across squads. Solo, that is the pilot.
- **Kinds by slot** (fact 15): slot 1 **G**unship + **W**ebifiers · slot 2 **X** (cross) + **T**alons · slot 3 **K** (lancerkin) + **P**ods.

| L | boss | adds H+L | squads | slot 2 arrives | slot 3 arrives | roster EXP, PL = L | roster EXP, PL = 1.5L |
|---|---|---|---|---|---|---|---|
| 1-5 | R/D | 0 | - | - | - | 0 | 0 |
| 6-8 | D R D | 1+0 | [G] | - | - | 18 | 12 |
| 9-11 | R D R | 1+1 | [G+W] | - | - | 24 | 16 |
| 12-14 | D R D | 1+2 | [G+2W] | - | - | 30 | 20 |
| 15-17 | R D R | 1+3 | [G+3W] | - | - | 36 | 24 |
| 18-20 | D R D | 2+3 | [G+3W] [X] | ≤50% or 30 s | - | 54 | 36 |
| 21-23 | R D R | 2+4 | [G+3W] [X+T] | ≤50% or 30 s | - | 60 | 40 |
| 24-26 | D R D | 2+5 | [G+3W] [X+2T] | ≤50% or 30 s | - | 66 | 44 |
| 27-29 | R D R | 2+6 | [G+3W] [X+3T] | ≤50% or 30 s | - | 72 | 48 |
| 30-32 | D R D | 3+6 | + [K] | ≤67% or 30 s | ≤33% or 60 s | 90 | 60 |
| 33-35 | R D R | 3+7 | + [K+P] | ≤67% or 30 s | ≤33% or 60 s | 96 | 64 |
| 36-38 | D R D | 3+8 | + [K+2P] | ≤67% or 30 s | ≤33% or 60 s | 102 | 68 |
| 39-40 | R D | **3+9** | [G+3W] [X+3T] [K+3P] | ≤67% or 30 s | ≤33% or 60 s | 108 | 72 |

The working reading checks out: a heavy at 6; lights at 9, 12, 15; a heavy at 18; lights at 21, 24, 27; a heavy at 30; lights at 33, 36, 39.

### 2b · EXP per add

- **Formula:** `Missions.ExpFor(worth, L, PL) = WorthExp(L, PL) ? round(worth x L / PL) : 0`, where `worth = EnemyDef.Exp x WaveDef.Exp`.
  - `EnemyDef.Exp` is **6 for every light and 18 for every heavy**, roughly their hull ratio.
  - `WaveDef.Exp` is 1 on `bounty_adds` and 0 on every other row, so raids, sieges and hunts still pay nothing.
  - `KillExpFor` becomes `ExpFor(KillExp, ...)`: one formula, unchanged results.
- **The half-level rule applies unchanged**, because an add is the boss's level:

| add | PL = L | PL 1.5L | PL 2L | PL 2L+1 |
|---|---|---|---|---|
| L6 heavy / light | 18 / 6 | 12 / 4 | 9 / 3 | **0 / 0** |
| L39 heavy / light | 18 / 6 | 12 / 4 | 9 / 3 | **0 / 0** |

- **Who is paid:** every pilot in the arena at the kill, each at its own level, with `Popups.Exp`. A disconnected (held) pilot is not owed adds.
- **Never paid:**
  - the boss-death sweep, which comes through `RaiderDown` with hull left
  - `CallOff`
  - beam escorts (no row)
  - refills
  - credits, salvage and crates
- **Why first fill only:** paying every kill would let L39 respawns be farmed for about 162 EXP a minute, level with clearing.

### 2c · The pay path

1. The host pays only from the kill branch of `Raider.TakeDamage`: `if (Hp <= 0 && Pays) Hub.PayKill(worth, Level)`, before `RaiderDown`.
2. `Hub.PayKill` awards the host locally and sends the reliable `NetKillExp(double worth, int level)` to the arena.
3. Each guest runs `Progression.AwardKill(worth, level)`, which computes its own figure with `ExpFor`.
- The RPC is named for the mechanism. Any future paid kill uses it.

### 2d · Existing escorts

- **Default (D1): replace the level growth.** Delete `EscortStep`, `EscortMax` and `EscortsAt`. `LaunchEscorts` then reads `m.Escorts`, so the beam launches **2 at every level**.
- From L9, the adds' lights take over the pinning that the extra escorts did. After this, "more enemies as the level climbs" has one home, the roster, and it applies to the DRAKE levels too (they have no escorts).
- **Escorts fold into doctrine `escort`.** Their behaviour is kept, and the `IsEscort` read-through stays for the harness's 9 uses.
- **The beam charges only on its own escorts' latch (D2):** `s.Escorts.Any(r => IsInstanceValid(r) && r.Latched)` replaces `s.Target.Pinned` at Boss.cs:380.

---

## 3 · WHAT IT ADDS TO A BOSS FIGHT (for curve.md)

**Setup:**
- solo, a par pilot, 60 s anchor fight
- boss alone per S(L): RUSTY 8.02, DRAKE 3.91
- **x boss** = all DPS taken by the pilot / boss alone
- **fight +%** = extra length, because pilot DPS goes to adds
- **EXP +%** = roster EXP / a repeat clear (KillExp 300 + completion 100 = 400)

| L | adds | x boss RUSTY | x boss DRAKE | pinned | fight +% | roster EXP (PL = L) | EXP +% |
|---|---|---|---|---|---|---|---|
| 1-5 | 0 | x1.00 | x1.00 | 0% | 0 | 0 | 0% |
| 6-8 | 1+0 | x1.01 | x1.03 | 0% | +4% | 18 | +4% |
| 9-11 | 1+1 | x1.13 | x1.26 | 8% | +10% | 24 | +6% |
| 12-14 | 1+2 | x1.15 | x1.32 | 9% | +11% | 30 | +8% |
| 15-17 | 1+3 | x1.18 | x1.38 | 10% | +12% | 36 | +9% |
| 18-20 | 2+3 | x1.18 | x1.38 | 10% | +16% | 54 | +14% |
| 21-23 | 2+4 | x1.23 | x1.48 | 13% | +17% | 60 | +15% |
| 24-26 | 2+5 | x1.24 | x1.51 | 14% | +18% | 66 | +16% |
| 27-29 | 2+6 | x1.26 | x1.53 | 14% | +19% | 72 | +18% |
| 30-32 | 3+6 | x1.27 | x1.57 | 14% | +25% | 90 | +22% |
| 33-35 | 3+7 | x1.34 | x1.71 | 19% | +29% | 96 | +24% |
| 36-38 | 3+8 | x1.37 | x1.79 | 21% | +33% | 102 | +26% |
| 39-40 | 3+9 | x1.44 | x1.92 | 23% | +38% | 108 | +27% |

**The web is the danger, not the guns.**
- The adds' own guns deal at most about 1.5/S (L39).
- **No step at L20.** Heavies bring no CC (ruling), so the pinned share grows only with the webifier count: 10% at L15-20, 23% at L39.
- The worst case, a pilot who ignores the adds, is x2.3 to x4.6, pinned 78% of the fight.

**Time to die**, boss alone → with adds. Hull = stock x 1.64 x mH(L), with 0.5%/s regen.

| L (fight) | hull 120 | hull 200 | hull 420 |
|---|---|---|---|
| 6 DRAKE (62 s) | 72 → 69 s | 158 → 151 s | 2519 → 1844 s |
| 9 RUSTY (66 s) | 32 → **27 s** | 59 → **50 s** | 182 → 146 s |
| 18 DRAKE (70 s) | 87 → **56 s** | 206 → 115 s | never → 665 s |
| 20 DRAKE (70 s) | 88 → **57 s** | 209 → 117 s | never → 688 s |
| 30 DRAKE (75 s) | 77 → **43 s** | 172 → 83 s | 6505 → 324 s |
| 39 RUSTY (83 s) | 25 → **17 s** | 45 → **29 s** | 127 → **74 s** |
| 40 DRAKE (83 s) | 57 → **26 s** | 118 → **48 s** | 704 → 136 s |

Bold means the pilot dies before the fight ends, if it never mitigates (applied to every cell; the first draft bolded some and not others). The light hulls on RUSTY levels are already short with no adds at all; that is the curve's problem.

**For curve.md:**
- To hold a 60 s fight *including* adds, scale the boss's hull by `1/(1 + fight%)`.
- To hold "four kills a level", trim `KillExp` by the EXP +% column, or absorb it as faster levelling late.
- **Against boss_adds.md:** L39 was x1.64, +50%, 42% pinned. It is lower here because squads engage at about 13 s rather than 8, the missile needs a pin, and heavies bring no CC.
- **Refill sensitivity at L39:** never gives 78 s and 10.50/S; 20 s gives 88 s and 11.57/S.

---

## 4 · WHAT CHANGES

### 4a · Fields and rows

| table | field / row | value |
|---|---|---|
| **new** `Squads.All` | `SquadDoctrine` rows `lone`, `patrol`, `gank`, `escort` | §1a |
| `WaveDef` | `Doctrine` | `Squads.Patrol`; hunts and bounty_adds use `Gank` |
| `WaveDef` | `Roster` (`Func<WaveBrief,int>`) | null means fire-and-forget (every row today). bounty_adds: `N(L)` |
| `WaveDef` | `Mission` (string) + `WaveBrief.Mission` | `For()` enforces it. The siege rows get "siege", bounty_adds "bounty" |
| `WaveDef` | `Exp` (double) | 0; bounty_adds 1 |
| `WaveDef` | `FormFor` (s) | 0 means today's placement; bounty_adds 10 |
| `WaveDef` | `HullShare`: `double` → `Func<WaveBrief,double>` | the 4 hunt rows become `_ => HunterHull` |
| `WaveCrew` | `Sweep` | 0; the Patrol crew gets the vee (§1b) |
| `EnemyDef` | `Cc` | pin rows `Pinned`; standoff rows none (ruling) |
| `EnemyDef` | `MissileFlight` | 10 (ruling); replaces the `Raider.MissileFlight` const |
| `EnemyDef` | `Exp` | 6 light / 18 heavy |
| `Lead` | `Jumped` | moved ≥ `Missiles.Step` in a frame |
| `Missions.Kinds[Bounty]` | `FirstWave 0`, `WaveEvery 30` | values, not new fields |
| `Waves.All` | **`bounty_adds`** | Garrison · Mission "bounty" · Gank · Roster N(L) · Fan Turn 0.35 Alternate · FormFor 10 · Strength/HullShare from the boss multipliers · Exp 1 · Crew `[Draw(Standoff) x1 at (0,60), Draw(Pin) x3 at (0,-40) step (60,0) sweep 30]` |
| `Raider` (host-only) | `Level`, `Pays`, `Squad` | set by `Raids.Send`, as `Agility` is |

### 4b · Code by file

| file | change |
|---|---|
| **Squads.cs** (new) | Doctrine rows, `Squad.Tick` (approach, heading, commit, time on target, break conditions), `ISquadMember`, and `Pace(kinds)` / `CommitAt(doctrine, kinds)` for placement. The header says what it replaced |
| Raider.cs | Flies its slot or post from its `Squad`. The CC line reads the row (heavies have none). Heavies laser unpinned and fire the missile only on a pin. Station keeping is capped. The kill branch pays. `Quarry` reads through to the squad. New flag bits |
| Raids.cs | Owns squads and the post book. `Send` builds a squad per spawn, sets Level/Pays, deals the Roster, and places by `FormFor`. `TickGarrison` takes over the arena clock from Hub, keeping per-slot due times and draws. `Hunt` passes `Level = (int)Threat` |
| Waves.cs | The new fields, `bounty_adds`, `Mission` on the siege rows, `For()` matching Mission, `SquadAt` taking a distance, and `Grown(L, from, every, cap)` |
| Enemies.cs | 4 fields on 6 rows; header 12-15 rewritten |
| Missions.cs | `ExpFor`, with `KillExpFor` routed through it |
| Progression.cs | `AwardKill(worth, level)`; header "and from their adds" |
| Hub.cs | Delete `_garrisonT`, `_garrisonWaves` and TickArena:983-987. `GarrisonWave` stays (harness :6071). `SpawnRaider(..., Squad)` (null means `lone`). `PayKill` + `NetKillExp`. Delete the 1092-1095 comment; `EdgeSpot` stays as hunt spawn geometry |
| Boss.cs | Delete `EscortStep`, `EscortMax` and `EscortsAt` (D1). Line 380 gates on its own escorts (D2). `LaunchEscorts` makes an escort squad |
| Missiles.cs | `Lead.Jumped` |
| Radar.cs | Heavy diamond, squad bracket, rim chevron (by tag) |
| UI text | PilotWindow.cs:44 EXP note; Hints "pilot"/"boss"; TIO EXP line |

**Deleted (invariant C).** From Raider:
- `Patrol`, `Station`, `Circuit`, `_orbit`, `Circle()`, `Choose()`
- `Posts[]` and the rank loop
- the `_boostUsed`/`BoostAtMine` trigger (the formula stays, for `CommitAt`)
- `HeavyBoostStop`, the `EdgeSpot` wait and both pin gates
- `Max(top, d*12)`
- `MissileFlight`
- the escort internals: `_escortPost`, `_shiver`, `_shiverHome`, `Escort()`

**Stale text to fix in the same edit:**
- Missions.cs:16 ("200 x"; the code has 300) and Missions.cs:163 ("10% a level")
- Raider.cs:15-29 (the edge wait, "7 s") and Raider.cs:48 ("1.1^")
- Waves.cs:121 and 174 ("10%", "1.1^")
- Enemies.cs:44-45 ("7 s flight")
- DESIGN.md:416-417 (`AwardPartyExp`, 1.5^)

**Record in DESIGN.md as traps:**
- a burst does not mean a kill
- a Garrison row must name its Mission
- `Raids.Tick` must stay ahead of the raiders' `_Process`

### 4c · Existing checks that must change (SmokeTest.cs.txt)

| lines | asserts today | action |
|---|---|---|
| 3837-3842 | unpinned heavy waits at the edge (4200 u), laser silent | **delete**; replaced by N8 |
| 3843-3882 | missile at an unpinned target 420 u off; ring `Time 12`; `Predict` 10 u/s → 120 u | restage with the target pinned. Literals **10 s / 100 u**. Add N7's unpinned half |
| 3883-3895 | pinned: 700% burn, then cruise inside 300 u | **replace** with time on target (N1). 700 survives only as the lone gunship's burn cap |
| 3833, 3899-3900 | heavy "2 DPS" | 1.25 (numbers pass, twin-laser ruling) |
| 4145 | `Raider.MissileFlight - 12` | `E(Gunship).MissileFlight == 10`, plus `Cc`/`Exp` literals in the 4114 table check (heavies' `Cc` asserted empty) |
| 3909, 4152-4157, 6003 | `r.Patrol` | `r.Squad.Id` (rung 1 forces it) |
| 1340 | `r.Station`, `r.Circuit` | `r.Squad.Station`, `r.Squad.Circuit` |
| 6005 | "(3200 u out)" | stale text; RaidEdge is 4200 |
| 5561-5565 | `EscortsAt` grows 2 → 5 | "L31 launches 2" (D1) |
| 5601-5607 | a charge on `ApplyStatus(Pinned)` from the test (:5604) | pin through an escort's latch; add "an add's web does not charge it" (D2) |
| 6068-6069 | `Bounty.WaveEvery == 0` | the bounty's clock is 0 / 30 |
| **must pass unchanged** | 3766-3825 (6a solo light, **needs the `lone` row**) · 3906-3920 (6c patrol) · 5546-5597 (escorts, flag bits 1 and 2) · 2137-2227 (hunts) · 1336-1341 (blockade, all within 450 u) · 6071-6075 (siege 4 raiders) | |

### 4d · New checks (written with the feature)

Rung 3 unless marked. **Varied** means where things are, never what is asserted. Each run prints `SEED n`.

| # | check | varied | asserts |
|---|---|---|---|
| N1 | squad of 4 closes as one (Called: gunship + 3 webifiers) | bearing `VaryAngle`, start `Vary(2600,3400)`, target heading | every member within 25 u of its slot after 2 s · spread ≤ span + 25 u until commit · centroid 100 ± 8 u/s · boost flags rise in the same frame ± 1 at 1600 ± 30 u · the 3 lights latch within 0.5 s of each other, the heavy within 0.5 s of the last |
| N2 | target still / moving / boosting | one of the three per seed; course `VaryAngle` | still or moving: pinned within 6 s of commit · boosting away: the squad never splits, and re-commits and pins once the target stops |
| N3 | squad of 1 / 4 / 12 | `VaryNear` | 1 = today's 6a numbers · 12 = 9 lights on 9 distinct front posts (≥ 30 u apart), 3 heavies on 3 rear posts, no overlapping hulls, DPS = 9 x 1 + 3 x 1.25 |
| N4 | leader killed first | kill at `Vary(0.3,0.8)` of the run-in | formation kept · exactly one FlagLead · still commits together · kill a light instead: the others' slots unchanged ± 2 u |
| N5 | target warps mid-engagement | warp heading | no raider moves > 700/60 + 1 u in any frame · pin gone within 0.5 s · re-formed within 4 s · no burn for 10 s |
| N6 | a heavy never pins | spawn spot, bearing astern | a lone latched heavy at **L1, L20 and L40**: no `Pinned` in 3 s · a L1 webifier pins within 0.5 s |
| N7 | missile only on a pin | unpinned soak `Vary(13,16)` s | unpinned: `BlastsPending == 0` · pinned: ring `Time == 10` at `Predict(pos, v, 10)` |
| N8 | a lone heavy lasers unpinned (the L6 add) | bearing, distance | no wait anywhere; posts astern at 135 u; 1.25 ± 0.3 DPS with `!Pinned` throughout |
| N9 | sticky posts | which light dies (seeded) | the other two bearings unchanged ± 10° |
| N10 | target hides and returns | hide `Vary(1,4)` s | re-targets and re-forms within span; the first target back inside 5 s is taken again |
| N11 | schedule by literal | - | L5 none · L6 [H] · L9 [H L] · L18 [H L L L][H] · L39 [H L L L] x3 · L40 = L39 · a 1..60 sweep against `"HLLLHLLLHLLL"` (never > 12, heavies = ceil(N/4)) |
| N12 | arrival | pilot `VaryNear(ArenaCentre,600)`, boss facing | L6: one heavy within 0.5 s of start, 3250 ± 5% u out, flies ≥ 9 s in formation · L39: hull 68% nothing / 66% slot 2 / 34% nothing / 32% slot 3 · hull held full: slot 2 at 30 s, slot 3 at 60 s (large tick delta) |
| N13 | refill | wipe time | L9 slot 1 wiped: nothing at +29.9 s, the same kinds at +30.1 s · live adds never > N(L) |
| N14 | routing | - | a siege brief gets siege rows only · a bounty brief never gets a siege row · L1-5 bounty sends nothing |
| N15 | EXP by literal | - | L6 heavy: PL 6 +18 (popup) · PL 12 +9 · PL 13 +0 (no popup) · L9 light at PL 9 +6 · a refill kill +0 · boss killed with adds alive: the sweep pays +0 · beam escort +0 · `CallOff` +0 |
| N16 | escorts and beam (D1, D2) | - | RUSTY L31 launches 2 · an add's web on the pilot does not start the charge; an escort's latch does |
| N17 | lifetime | - | leave the arena with squads alive: 0 squads, empty post book, orphan and object counts flat across re-entry |
| N18 | **rung 5**: guest | - | squad-id bits equal per squad · one FlagLead · FlagLock set during the burn and clear once latched · a guest pinned by an add squad · L18 P2: count 2+3, hull = row x 1.025^17 x 1.6, shot = Dps x 1.025^17 x 1.2 · an add kill pays the guest at its own level (a guest above 2L gets 0) · the boss-kill sweep pays none |
| N19 | **rung 4**: the look | - | one frame inbound (vee, link lines, names), one during the burn (lock lines, HUD line), `LINT: 0` |

N1-N10 geometry must pass three seeds at rung 3 before anything goes higher.

### 4e · Build order (foundations first; one writer)

| step | work | proved at |
|---|---|---|
| 1 | `EnemyDef.Cc/Exp/MissileFlight`, `Raider.Level`, `Lead.Jumped`; table literals in check 4114 | rung 1, then 3 |
| 2 | `Squads.cs`, the Raider rewrite, the post book, the `lone` squad, and the harness callers and 6b rewrite (4c) | rung 2, then 3 (N1-N10, N17) |
| 3 | `Doctrine`, `FormFor`, `Sweep`, `Station = Origin`, the escort fold-in | rung 3 (5546-5597) |
| 4 | `Roster`/`Mission`/`Exp`/`HullShare` func, `TickGarrison`, `bounty_adds`, the bounty clock, Boss D1/D2 | rung 3 (N11-N14, N16) |
| 5 | `ExpFor`, `AwardKill`, `PayKill`/`NetKillExp` | rung 3 (N15) |
| 6 | flag bits, guest drawing, radar, labels | rung 4 (N19), then **rung 5 once** for steps 5 and 6 (N18) |
| 7 | the bar | **rung 6 once** |

**Cost:** about L for steps 1-3 (most of it the Raider rewrite and 6b), plus M for steps 4-6.

---

## 5 · DECISIONS FOR THE OWNER (the default stands if unanswered)

| # | fork | default |
|---|---|---|
| D1 | Beam escorts' +1 per 10 levels (your CHANGES item 6): keep, or replace with the adds? | **Replace**: the beam keeps 2; level growth belongs to the adds |
| D2 | Beam charge: on any pin (an add's web can start it about 1 s after launch), or only on its own escorts' web? | **Own escorts only** |
| D3 | A wiped add squad: returns after 30 s, returns after 20 s, or never? | **30 s**, with the same kinds |
| D4 | EXP from refilled squads? | **No**: one roster per fight pays |
| D5 | *(settled by your ruling)* A heavy's CC | **None of its own, at any level.** The webifiers in its squad are its CC |
| D6 | Add kinds: fixed per slot (gunship+webifiers, cross+talons, lancerkin+pods), or gunship+webifiers in every slot? | **Fixed per slot** |
| D7 | Party: count fixed by level, each add scaled like the boss; or one more squad per extra pilot? | **Count fixed**, boss multipliers |

---

## 6 · RISKS (top three)

| # | risk | number | mitigation |
|---|---|---|---|
| R1 | **The beam on RUSTY levels while squad 1's webifiers hold the pilot** | Strip 3 webifiers: 3.7 s (L9), 3.5 (L21), 3.7 (L31), 4.0 (L39), against a windup of 5.5, 4.9, 4.5, 4.1 s. The margin shrinks to **0.1 s at L39**. The beam lands 4 hits = 244 (L9) to 511 (L39) x party damage | With no heavy CC the pin always ends with the last webifier. D2 moves the charge to the escorts' latch (at most 5 s), so launch to beam is about 7-9 s and the margin comes back. **If D2 is refused**, L39 needs a longer windup or fewer webifiers in slot 1 |
| R2 | **Home content changes with the formation rewrite.** Heavies no longer wait at the edge, so hauler escorts and raids bring the laser at once. Posts and pacing change for every raid type | Hunts are at half hull; odd hunt waves bring a heavy | This is what was asked ("instead of isolated from far out"). The numbers pass should watch hauler losses. The home checks in 4c must pass unchanged |
| R3 | **EXP and fight length drift the curve** | EXP per fight +4% (L6) to +27% (L39). Fight length +4% to +39%. Pinned share up to 35% | curve.md absorbs it or trims `KillExp` / boss hull (§3). Pay only from the kill branch (fact 8), or the boss-death sweep pays for every live add |
