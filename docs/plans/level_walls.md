# LEVEL WALLS: abilities and chip slots behind pilot levels

Read-only design. Nothing in the repo was edited, built or run. Numbers come from `kits3/curve_gated.py`
(`python curve_gated.py [default|slow|fast|a1late]`; outputs in `kits3/run_*.txt`), which imports `curve.py` and
`items/tiers.py` unchanged. Kit numbers are the v2 sign-off's; the owner's later changes with no numbers yet
(Supercarrier mode, Ramjet, the Sniper's new ability) are marked (?) and carry no damage in the model.

## Owner summary

1. **Scope: a new table.** `Unlocks.cs` is ONE list of walls that every class reads. It needs two foundations first: F15 (3/4/5/6 chip slots per class) and a saved highest level reached (`Peak`).
2. **Walls:** ability 1 comes with the ship. Chip slot 1 opens at L2, ability 2 at L3, chip slot 2 at L4, ability 3 at L6, chip slot 3 at L8. Slot 4 opens at L10, slot 5 at L12 and slot 6 at L14.
3. **Pace:** a pilot has all three abilities by its 14th boss fight. Capitals have every chip slot by the 20th kill (boss L5), lights by the 36th (boss L9).
4. **Unlock order:** damage first, then survival, then crowd control. Ten classes already unlock in F, Q, E order. Two differ: the Carrier learns F, E, Q (gunships before Supercarrier) and the Warrior E, Q, F (Prism last).
5. **Boss scale with walls:** the L1 Lancer is **3779** instead of 3838. Boss L2-L5 are 2-3% lighter than today's plan, and nothing changes from L6 on. On-par kills stay at **60 s** at every level.
6. **Two classes feel the walls:** the Carrier takes 84 s at L1, 72 s at L2 and 68 s at L3 (usually 65 s), and the Warrior takes 64-65 s until L3 (usually 60 s). Every other class stays within ±3 s of its usual time.
7. **Locked things:** a locked ability's key reads `LOCKED · L3` and refuses the press. The host refuses it too. A locked chip slot shows its level. A save with chips in locked slots moves them to the hold.
8. **Blocked on you:** 7 questions in §5; the defaults are the build.

## THE GATE TABLE (`Unlocks.All`, pilot-level rows)

| pilot level | opens | the reference reaches it at | completes |
|---|---|---|---|
| **1** | the weapon (Space, and its R/G action) + **ability 1** | boss L1, fight 1 | |
| **2** | chip slot 1 | boss L1, fight 3 (#3) | |
| **3** | **ability 2** | boss L2, fight 2 (#6) | |
| **4** | chip slot 2 | boss L3, fight 1 (#9) | |
| **6** | **ability 3** | boss L4, fight 2 (#14) | every class's abilities |
| **8** | chip slot 3 | boss L5, fight 4 (#20) | capitals (3) |
| **10** | chip slot 4 | boss L7, fight 1 (#25) | freighters (4) |
| **12** | chip slot 5 | boss L8, fight 2 (#30) | heavies (5) |
| **14** | chip slot 6 | boss L9, fight 4 (#36) | lights (6) |

"Reference" is curve.md's pace: 4 clears a level. It enters boss L1..L15 at pilot levels 1, 2, 4, 5, 7, 8, 10, 11, 12, 14, 15, 17, 18, 20 and 21.

A class shows only its own chip count of rows: a capital never sees slots 4-6. The owner's rule of at most 3
COMBAT and at most 3 UTILITY chips is a fit rule on the open slots (F15 / decision 1), not a wall of its own (§1.3).

---

## 1 · THE WALLS

### 1.1 What each class learns first (the row order is the unlock order)

The rule is the same for every class: **1 kills, 2 survives, 3 controls.**
- Ability 1 carries the class's damage, so a new pilot's fights are the right length.
- Ability 2 answers a boss super or a web.
- Ability 3 is the party, raid or advanced tool.

| class | 1st (L1) | 2nd (L3) | 3rd (L6) | why this order |
|---|---|---|---|---|
| BATTLESHIP | F Broadside | Q Brace | E CIWS | Broadside is 52% of its damage. Brace answers the beam. CIWS is for raids |
| CARRIER | F Bomber strike | **E** Warp gunships | **Q** Supercarrier (?) | It has the lowest weapon share (0.45). Gunships return 17% of its damage. A mode switch is a capstone |
| DESTROYER | F Long Lance | Q Suppressing fire | E Grapnel | The Grapnel (hook, swing, tow, rip) is its hardest tool |
| FREIGHTER | F Unmask | Q Bubble | E Redeploy | The sentries are its weapon's R, so they are open from L1. Redeploy manages them |
| TENDER | F Overdrive | Q Repair field | E Resupply | Resupply is the party tool |
| BASTION | F Bunker buster | Q Shockwave | E Gravity well | Buster is 55% of its damage |
| WARRIOR | **E** Lunge | Q Whirlwind | **F** Prism stance | Prism is the hardest mechanic in the game (angle bands, the L-sized F11). Lunge closes the melee gap |
| SNIPER | F Anchor | Q Tether mine | E new ability (?) | The new ability is slotted when its card exists |
| WARDEN | F Hunters | Q Taunt (33% DR, 6 s) | E Flak curtain | Taunt is its survival tool |
| DART | F Rod from God | Q Ramjet (?) | E Slingshot | Ramjet teaches "speed is damage" (Pepperbox pricing, Slipstream). Slingshot re-aims the rod |
| ECHO | F Reverb | Q Rewind | E EMP | |
| WRAITH | F Veil | Q Venom | E Shadow step | Shadow step needs a selected target and positioning |

- Ten classes already unlock in key order.
- The Carrier and the Warrior do not. By default their keys stay where you put them: their bars read F E Q and E Q F. The alternative is to re-key them (Q5).

### 1.2 Why ability 1 is not behind a wall (the numbers)

The weapon's share of a class's damage runs from 0.45 (Carrier, Bastion) to 0.85 (Echo, Wraith, Freighter).

Walling ability 1 until L2 (the `a1late` run) breaks the first boss:
- The Carrier takes **123 s** to kill it.
- The par pilot's kill time swings **70.5 s → 52.2 s** within boss L1, because the boss's hull is set per level and the wall falls mid-level.

With ability 1 open, the worst class is the Carrier at 84 s. Every other class is within ±4 s of its usual time.

### 1.3 Chip slots are walled by index, and they are not typed

Every class gets its 1st, 2nd and 3rd chip at L2, L4 and L8. Slots 4-6 exist only on freighters, heavies and lights.

Per-kind walls ("combat 1 at L2, utility 1 at L4 ...") were considered and rejected:
- They type the slots, so a capital could never hold 3 Combat chips.
- They double the rows a player must learn.
- For the curve they are identical: damage chips arrive at the same levels on every hull either way.

### 1.4 Spacing (the other two tables that were run)

| table | ability 2 / 3 at | last chip at | Carrier L1 / L2 / L3 | verdict |
|---|---|---|---|---|
| **default** | PL3 / PL6 (fights 6 / 14) | PL14 (boss L9) | 84 / 72 / 68 s | the default |
| slow (every other level) | PL5 / PL9 (11 / 22) | PL17 (boss L11) | 84 / 84 / 75 s | the Carrier's deficit lasts twice as long |
| fast (every level) | PL3 / PL5 (6 / 11) | PL12 (boss L8) | 84 / 72 / 66 s | 5 fights to learn ability 2 before ability 3 arrives |

---

## 2 · THE CURVE WITH WALLS

**What changed in the model.**
- The reference pilot (the DESTROYER, median) and every class wear only what their pilot level has opened.
- A class's ability shares are its v2 sheet split, in learn order.
- The pilot level is taken **fight by fight**, and the boss of level L is sized to the average of the four fights the reference flies at L.
- Everything else is curve.md / math.md unchanged.

### 2.1 The new boss scale, L1-L15: RECOMMENDED (kit chips baked into Base, Par v2)

This is the items plan's K3: `chip_basic` becomes part of each class's Base, so a wall never takes a kit chip. Only dropped chips are walled. It uses compound tiers and step 0.03.

| L | PL in | HullScale | DamageScale | Lancer hull, walled | Lancer hull, no walls | ratio |
|---|---|---|---|---|---|---|
| 1 | 1-2 | 1.000 | 1.000 | **3779** | 3838 | 0.985 |
| 2 | 2-3 | 1.109 | 1.104 | 4191 | 4321 | 0.970 |
| 3 | 4-5 | 1.218 | 1.180 | 4603 | 4737 | 0.972 |
| 4 | 5-6 | 1.280 | 1.248 | 4836 | 4948 | 0.977 |
| 5 | 7-8 | 1.326 | 1.298 | 5011 | 5101 | 0.983 |
| 6 | 8-9 | 1.433 | 1.377 | 5417 | 5417 | 1.000 |
| 7 | 10-11 | 1.486 | 1.435 | 5615 | 5615 | 1 |
| 8 | 11-12 | 1.524 | 1.476 | 5760 | 5760 | 1 |
| 9 | 12-14 | 1.551 | 1.523 | 5860 | 5860 | 1 |
| 10 | 14-15 | 1.608 | 1.578 | 6076 | 6076 | 1 |
| 11 | 15-17 | 1.678 | 1.621 | 6342 | 6342 | 1 |
| 12 | 17-18 | 1.715 | 1.661 | 6482 | 6482 | 1 |
| 13 | 18-20 | 1.743 | 1.693 | 6588 | 6588 | 1 |
| 14 | 20-21 | 1.818 | 1.789 | 6870 | 6870 | 1 |
| 15 | 21-22 | 1.865 | 1.839 | 7049 | 7049 | 1 |

- The Drake is x0.921 of the Lancer.
- The scales are re-normalised to the new anchor (3779). That is why HullScale from L6 reads above the unwalled 1.412... while the absolute hulls are identical. L40 is 16,196, as in math.md.
- The par pilot's time to die is **30.7 s at every level**, because kit chips baked into Base leave its L1 hull at 454.

### 2.2 The same, on curve.py as it stands (kit chips still fitted as `chip_basic`)

| L | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| HullScale | 1.000 | 1.105 | 1.273 | 1.352 | 1.426 | 1.586 | 1.638 | 1.691 | 1.747 | 1.804 | 1.922 | 1.985 | 2.049 | 2.116 | 2.184 |
| DamageScale | 1.000 | 1.143 | 1.304 | 1.439 | 1.578 | 1.652 | 1.691 | 1.730 | 1.793 | 1.835 | 1.879 | 1.925 | 1.971 | 2.044 | 2.094 |
| Lancer hull | **3366** | 3719 | 4283 | 4552 | 4799 | 5338 | 5513 | 5693 | 5880 | 6072 | 6471 | 6681 | 6898 | 7121 | 7350 |
| no walls | 3823 | 4152 | 4565 | 4814 | 5005 | 5338 | = | = | = | = | = | = | = | = | = |

**The cost of NOT baking the kit chips.**
- A wall now takes kit chips off, so a PL1 pilot's hull is 405, not 454.
- The par pilot's time to die falls to **26.9 s** (Lancer) and 36.1 s (Drake) at every level, from 30.7 s and 41.5 s.
- Holding 30.7 s needs a boss damage anchor of **x0.892** on the move rows.
- At PL1 the lights lose their +30% of kit chips and the capitals their +15%. The Echo runs 44-45 s through L6 against its usual 39 s.

This is why Q4 defaults to baking.

### 2.3 How the 60 s target holds (recommended regime)

- **At par:** exactly **60.0 s** at every level, by construction. When a wall falls mid-level, the level's four fights run **59.0-60.7 s**.
- **Every class**, kill time / time to die in seconds (Lancer sheet), against its own usual figure with no walls:

| class | L1 | L2 | L3 | L4 | L5 | L6 | L8 | L10 | no walls |
|---|---|---|---|---|---|---|---|---|---|
| CARRIER | **84**/33 | **72**/33 | **68**/33 | 66/33 | 65/33 | 65/33 | 65/33 | 65/33 | 65/33 |
| WARRIOR | 64/25 | 64/25 | 65/24 | 62/24 | 61/24 | 62/24 | 60/25 | 60/25 | 60/25 |
| WRAITH | 48/18 | 45/18 | 45/18 | 46/18 | 46/18 | 47/18 | 46/18 | 45/19 | 45/19 |
| SNIPER | 68/19 | 68/19 | 69/19 | 70/19 | 71/19 | 72/19 | 70/19 | 69/20 | 69/20 |
| ECHO | 39/15 | 39/15 | 40/15 | 40/15 | 41/15 | 42/14 | 41/15 | 40/15 | 40/15 |
| DESTROYER | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 | 60/31 |
| the other six | within ±2 s of their usual time at every level |

  - The Carrier is the one real cost. Its bombers are only 31% of its damage, and its other 24% is walled until L3 and L6.
  - The lights and the Sniper drift 2-3 s over L4-L8, because their chip slots 4-6 open last.
- **Behind the reference's walls:** a pilot who skipped ahead, or is carried by a party, meets the boss with fewer walls open than the reference.

  | levels behind | kill time |
  |---|---|
  | 2 | **+2 s** (61.9) |
  | 4 | **+2.5-4.5 s** (62.5-64.5) |
  | any, from L10 | nothing |

  On curve.py's regime these are +4 s and +6-8 s.
- **What the curve cannot see: survival abilities.**
  - Brace, Bubble, Taunt, Rewind and the Prism are not hull, so DamageScale is blind to them.
  - The unlock order puts every survival tool 2nd, at pilot level 3 (fight 6). Five fights are flown without it, against the gentlest boss.
  - If flying shows L1-L2 deaths, the lever is the order or the ability-2 row, not the curve.
- **Par must read the walls.** When `Par.cs` lands (curve batch):
  - it takes the reference's pilot level at each of the level's fights from the pace row;
  - it asks `Unlocks` what is open at that level;
  - so the boss scale above is computed, not copied.

---

## 3 · CODE PLAN

### 3.1 Where things live today

**Abilities.**
- `Abilities.cs`:
  - `AbilityDef` (:33-76), the `Ab` catalogue;
  - `Abilities.For` (:376), `Find` (:383), `ByKey` (:400);
  - 6 `Open` slots appended to every class (:370-373).
- The class's list is `ClassDef.Abilities` (Ships.cs:133). **Its order is the bar's order.**
- `PlayerShip.cs`:
  - `UseAbility` (:444) is the owner's courtesy (Refuse, Fail note);
  - `RequestAbility` (:459) is the guest's RPC, and the host checks the sender;
  - `DoAbility` (:475) **is the host gate**. Today it checks only dead/alive.
- Displays and the key path:
  - `AbilityBar.StateOf` (:30) and its open-slot drawing (:55-59);
  - the K window's list (StatsWindow.cs:124);
  - the hub's key dispatch (Hub.cs:1702-1705).

**Chips.**
- `Equipment.cs:68`: `CoreSlots = 5, ChipSlots = 5, Slots = 10`, all constants, the same for every class. F15 is not built.
- `Default` (:407) fills 5 `chip_basic`.
- `Sanitize` (:417) is the one sanitising path. Its callers are:
  - the host's copy of a claim (PlayerShip.cs:404);
  - `Character.Load` (:286);
  - `Equipment.Sum` (:475).
- EquipmentWindow:
  - chip rows (:82-89);
  - the free-slot search (:106, :180);
  - `FitChip` (:177).

**Pilot level.**
- `Character.Level`: field at :36, saved at :163, loaded at :266.
- `Progression`:
  - `AddExp` (:156-169) is the level-up hook, and it already meets hint "pilot";
  - `Refit` (:241) **takes a level off**. A refit is also the only way to change class (BasePanel);
  - `MaxSpendLevel` 100 and `Afford` (:95-113).
- The identity:
  - `Hub.SendIdentity` (:769-781) sends `Character.Level`;
  - `NetIdentity` (:784-816) stores it as a claim in `p.Level` (:813; Net.cs:66).
  - `ApplyIdentity` (:753-766) passes only `bought` to the ship (`SetProgress`, PlayerShip.cs:372). **A ship does not know its pilot's level today.**

**The tutorial.**
- `Hints.All` (Hints.cs:14): static (title, body) rows.
- `Tour` (:36).
- `Meet` (:81).
- `Start` (:103) shows a row and marks it seen per character.

**Gates of the same shape that already exist.**
- `Economy.Upgrade.NeedsBoss`:
  - defined at Economy.cs:82; Auto-sell = boss 3 (:39, :119);
  - checked in Yard.cs:263 and BasePanel.cs:120-129 ("LOCKED").
- `Lanes.NeedsBoss = 1` (Lanes.cs:103): raids start after boss 1 (Raids.cs:53).

**The harness flies level-1 pilots.**
- Role setup: SmokeTest.cs.txt:193 and :198 call `NewBlank`.
- Shots.cs.txt:69 and :71.
- The menu's demo battleship presses Broadside (MainMenu.cs:283).

### 3.2 The system: `scripts/Unlocks.cs` (NEW), named for the mechanism

**The header** says what it replaced:
- `Economy.NeedsBoss`;
- `Lanes.NeedsBoss`;
- the chip-slot count's constant.

It also says what a new row must fill in.

**The row and the contract:**
```
public enum Measure { Pilot, Boss }           // the pilot's highest level reached; a base owner's highest boss beaten
public enum Opens { Ability, ChipSlot, Economy, Raids }
public readonly struct Unlock { Measure By; int At; Opens What; int Nth; string Id; string Hint; }
public interface IGated { int Peak { get; } ShipClass Class { get; } }     // PlayerShip implements it
public static readonly Unlock[] All = { ... the 9 pilot rows above, + {Boss, 3, Economy, "hauler_autosell"}, {Boss, 1, Raids} };
```

**Queries** (the only way anything asks):
- `Count(Opens, int at)`;
- `At(Opens, int nth)` → the level that opens it;
- `LockedAt(IGated, AbilityDef)` → `int?`;
- `ChipSlots(IGated)` = `min(ClassDef.Chips, Count(ChipSlot, Peak))`;
- `Crossed(int from, int to)` → the rows passed on a level-up;
- `Top` = the last pilot row's level;
- `Fill(text, ship)` → the hint tokens.

**Reached by position, never by type.**
- `AbilityDef.Weapon` (a new bool) marks a weapon's own actions: Guns, FireMode, Attack, Recall, the sentry's R. They never wait behind a wall.
- A class's non-Weapon entries, **in `ClassDef.Abilities` order**, are its abilities 1, 2 and 3.
- The bar therefore fills left to right in unlock order.
- `Universal` (Reboard) and the `Open` hotkeys are never walled.
- A 13th class needs no new code: its list order is its unlock order.

### 3.3 Edits, file by file (foundations first)

1. **Foundations.**
   - **F15:** `ClassDef.Chips` (3/4/5/6). `Equipment.ChipSlots` is deleted, and `Slots` becomes `CoreSlots` + 6.
   - **Bake `chip_basic`** into each class's Base: `Default` fits no chips, and its +5%/+5% is in the sheet (Q4).
   - **`Character.Peak`:**
     - saved as `[progress] peak`;
     - on load it is `max(peak, level)`: a file without it reads its level, so this is carried forward and **not** a `Game.Version` bump (DESIGN.md: 997);
     - `AddExp` raises it and `Refit` never lowers it.
2. **Wire.**
   - `SendIdentity` adds `Character.Peak` after `Level`.
   - `NetIdentity` sets `p.Peak = Clamp(Max(peak, level), 1, MaxSpendLevel)`. It is the same cap as spending, through one `Progression.Claim(level)` that `Afford` also uses.
   - `ApplyIdentity` calls `s.SetProgress(bought, peak)`.
   - `PlayerShip` implements `IGated`. A change to `Peak` re-sanitises the loadout and restats.
3. **The ability gate.**
   - `DoAbility` returns when `Unlocks.LockedAt(this, def)` is set. This is the host's rule, and it holds a guest to its claimed level.
   - `UseAbility` fails the press with `LOCKED · L3` before `Refuse`, so the slot flashes the reason at once.
4. **The bar.**
   - `SlotState` gains `Locked`. `AbilityBar.StateOf` returns it first.
   - A locked slot draws in the quiet `_open` box, with its key and name dimmed and `LOCKED · L3` beneath.
   - **The K window** keeps it rebindable and adds "opens at level 3" to its row.
5. **Chips.**
   - `Equipment.Sanitize(c, ids, peak)` empties a chip in an unopened slot on the host's copy, exactly as it empties one that does not fit.
   - `EquipmentWindow` shows one row per `ClassDef.Chips`. A locked row reads `CHIP 4  ·  LOCKED · L10` with no button.
   - `FitChip` and EQUIP use the first **open** empty slot. With none, EQUIP greys out with the tooltip "next chip slot opens at level 10".
   - The ≤3 Combat / ≤3 Utility fit rule (F15) is checked in the same two places.
6. **Saves: never delete.**
   - `Character.Load` already moves a fitted part that no longer fits into the hold (the `displaced` list, :279-295).
   - A chip in a slot at or above `min(Chips, open at Peak)` joins that list, for every class's loadout.
   - With `Peak` only rising after load, nothing is ever re-locked in play.
7. **The tutorial.**
   - `Hints.All` gains one row per `Unlocks.All` pilot row (id `unlock_<n>`). Tokens (`{key}`, `{name}`, `{blurb}`) are filled by `Unlocks.Fill` in `Hints.Start`.
   - An example card: "LEVEL 3 · Q · SUPPRESSING FIRE" with the ability's blurb. A chip card reads "LEVEL 4 · CHIP SLOT 2: fit one from the hold (I)".
   - `AddExp` meets `Crossed(old, new)` in order. A 2600-EXP jump from level 1 queues the chip-slot-1 card, then the ability-2 card.
   - The "abilities" and "pilot" rows gain one sentence: "a locked key shows the level that opens it."
   - The **Pilot window (L)** gains a NEXT line from `Unlocks`: "Level 6: E · GRAPNEL".
8. **The absorbed gates.**
   - `Economy.NeedsBoss` and `Lanes.NeedsBoss` are deleted.
   - Yard.cs:263, BasePanel.cs:120-129 and Raids.cs:53 ask `Unlocks` (Measure.Boss, fed `Yard.OwnerBoss` / `Missions.HighestBeaten`). This is one path (CLAUDE §3.5), Q6.
9. **Harness.**
   - The role setup (SmokeTest.cs.txt:193, :198) and Shots.cs.txt:69/:71 set `Character.Level = Character.Peak = Unlocks.Top`, read from the table. Every existing check keeps its meaning.
   - The wall checks set lower levels themselves.
   - The menu ship gets `Peak = Unlocks.Top` too, so the menu never depends on the order.
   - Callers of the deleted `ChipSlots` must also move: SmokeTest :2948-3038, :3627, :3684, :3827.
10. **Record.**
    - DESIGN.md: the walls, the peak rule and the trap "a list's order is its unlock order".
    - CHANGES.md.

**Authority, stated once.**
- A guest's level is its own word, capped at 100 like its points.
- A guest that claims 100 opens everything, which is the same trust the points already have.
- What the host guarantees:
  - one claim governs points, walls and chips alike;
  - a press or a chip the claim has not opened never reaches combat.

---

## 4 · CHECKS

- Literals are this table's levels once you sign it. Nothing reads the code's own constants except the one table proof.
- Positions and bearings use `Vary` / `VaryAngle` / `VaryNear`; levels are never varied.

| # | check | literals | 3 varied situations | rung |
|---|---|---|---|---|
| 1 | Compile: `Sanitize(c, ids, peak)`, `SetProgress(bought, peak)`, `ChipSlots` deleted, and the harness callers | | | **1**, then **2** (`UNUSED 0`) |
| 2 | **The table, in one place:** `At(Ability, 1..3) = 1, 3, 6`; `At(ChipSlot, 1..6) = 2, 4, 8, 10, 12, 14`; `Top = 14`; Auto-sell at boss 3; raids at boss 1 | the table | | 3 |
| 3 | Every flyable class has exactly 3 walled abilities, and every `Weapon` row fires at level 1 | Warrior order lunge, whirlwind, prism; Carrier bombers, gunships, supercarrier | Space held on a dummy at VaryNear 300-600 u for {capital, freighter, light} | 3 |
| 4 | **Owner refusal:** at levels {2, 3} ability 2's key is refused with `LOCKED · L3` and accepted. No slot state or cooldown changes on a refusal | L3; `LOCKED · L3` | {Destroyer Suppress, Warden Taunt, Echo Rewind}, each at a Vary spot, with the target at VaryNear/VaryAngle where one is needed | 3 |
| 5 | **Host gate:** `RequestAbility` delivered straight to the host for a ship at level 5 pressing ability 3 does nothing. At level 6 it acts | L6 | 3 classes whose ability 3 differs in kind (CIWS, Grapnel, Prism) | 3 (host path in solo) |
| 6 | **Chip slots:** open slots at levels {1, 2, 4, 8, 14} | light 0, 1, 2, 3, 6; capital 0, 1, 2, 3, 3 | {Battleship, Warden, Echo} | 3 |
| 6b | EQUIP greys out with no open slot, and the ≤3 Combat rule holds | ≤3 | the same three classes | 3 |
| 7 | **Save, never delete:** a file at level 3 with 5 Combat chips fitted loads with 1 fitted and 4 in the hold. Owned chips are equal before and after | 1 fitted / 4 held | files at level {1, 3, 14} × {capital, heavy, light}, plus a file with no `peak` key | 3 (+ **mutant**: drop the `displaced` line and the check must fail) |
| 8 | **Refit never re-locks:** level 6 → refit → level 5. Ability 3 still open; `peak` 6 saved and reloaded | L6 / L5 | three classes | 3 |
| 9 | **Cards:** 2600 EXP from level 1 queues `unlock` chip-1 then ability-2, naming this class's ability 2 | "LEVEL 3 · Q · …" | {Destroyer, Warrior (E), Carrier (E)} | 3 |
| 10 | The stock sheet is unchanged by baking: level-1 hull = the class hull literal × (1 + 0.05N) | the class hull literals | 3 classes | 3 |
| 11 | *(when Par.cs exists)* `Par` reads the walls: `HullScale(2)` | 1.098; anchor 3222 (`numbers_curve_raids_items.md` §1.3, §8) | | 3 |
| 12 | **What is drawn:** a locked slot (`LOCKED · L3`), a locked chip row, the Pilot NEXT line, one unlock card | | a capital, a light, and the Warrior (bar order E Q F) | **4** (LINT 0) |
| 13 | **Guest, host decides:** guest at claimed level 1 sends `RequestAbility` for ability 2 bypassing its own refusal. The host's ship shows no state, and the other guest sees nothing | | guest is a {capital, light} | **5** (+ **mutant**: delete the `DoAbility` wall line and the check must fail) |
| 14 | Guest at level 3: ability 2 acts on the host, and the other guest sees the effect | | | 5 |
| 15 | Guest with 5 chips fitted and claimed level 3: the host's and the other guest's copies both apply 1 chip | hull = base × (1 + one chip's share) | | 5 |
| 16 | A guest levels up (AddExp over L3) and presses ability 2 in the same frame: the identity is ordered before the press, so it acts | | | 5 |
| 17 | Guest refits at level 6: the host still opens its ability 3 (`peak` on the wire) | L6 | | 5 |

**Order.**
- Rung 1 → 2 on the foundations, then rung 3 per slice.
- Rung 4 once for items 12.
- Rung 5 once, after 13-17 are written.
- Rung 6 once, at the end of the batch.

---

## 5 · QUESTIONS (say nothing and the default stands)

1. **Is ability 1 open from the first flight?** Default **yes**. Walled until L2, the first two kills run up to 123 s (Carrier) and the par swings 70 → 52 s within L1.
2. **Spacing?** Default **the table above**: all abilities by PL6, the last chip at PL14 (boss L9). The alternatives are every other level (to PL17 / boss L11) or every level (to PL12 / boss L8).
3. **Are chip slots walled by index, untyped, with ≤3 Combat / ≤3 Utility as the fit rule?** Default **yes**. Per-kind walls would stop a capital holding 3 Combat chips.
4. **Bake `chip_basic` into each class's Base in the same edit** (the items plan's K3, moved earlier)? Default **yes**. Otherwise the L1 boss is 3366, the par's time to die is 26.9 s, and boss damage needs x0.892.
5. **Keys against unlock order: keep your keys** (the Carrier's bar reads F E Q, the Warrior's E Q F)? Default **keep**. The alternative re-keys them so a new pilot always unlocks F, then Q, then E.
6. **Do walls read the highest level ever reached,** so a refit (which costs a level and is the only class change) never re-locks anything? Default **yes**: `peak` is saved and sent with the identity.
7. **Fold the two existing boss-beaten gates** (Auto-sell at boss 3, raids after boss 1) into the same table in this edit? Default **yes**, per the one-path rule. Refusing it leaves three gate mechanisms.
