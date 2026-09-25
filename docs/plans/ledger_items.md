# Ledger: lane I, items (by hull category, +10% a tier)

Writer: one agent (opus) in worktree `WarShips_wt_items`, branch `wt/items`, started at ccf7244 (version-l).
Spec: `numbers_curve_raids_items.md` §3 (the law, Loot, the 42 lines + 6 chips, salvage), §4 rows 10-13, §8;
`kits_v31.md` §2 (the class table), §3.6 (chips, walls, Game.Version 3 -> 4), §10 decision 6 (chip budget:
settled by numbers §8 R7 -- no salvage on chip slots, chip rows T1 +8% x1.10 a tier); README Items rulings.
BUILD PHASE: no engine run; per job typecheck + verify -Quick + a read of the own diff. A PRE with no POST
is an interrupted job: compare hashes, revert half-made edits, redo it first. Re-read this tail before a PRE.

## Decisions taken where the spec is silent (defaults; each also in the lane's return)

- **I1 Items.cs is the generator** (new file): `HullCat` + the `Hulls` rows (which class is which category),
  the `Tiers` law (P(t) = 1.10^(t-1), the T1 leans Power .25 / Multiplier .20 / Reach .18 / Top .22 /
  Chip .08 / Half .125, the +n rider rule, scrap 100 x 1.25^(t-1)), the `Roles` rows, and `Items.Lines`
  (48 rows: 42 category lines + 6 chips) that `Equipment.Build` yields at 10 tiers each: ids `{stem}_t{n}`.
- **I2 A ROLE is a stat key that names a set of rows** (`@damage` every weapon's damage = ClassDef.Damage keys;
  `@primary`, `@primary_rate`, `@primary_range`, `@tracking`, `@shot_speed`, `@output` every ability output,
  `@area` ability reach/area, `@duration` ability duration): a line lifts a role, and `Equipment.Sum` expands
  it onto the ids of that role the hull's sheet has. The role rows live in Items.cs (`Roles`), keyed by stat id,
  so the reconcile job edits ONE table when the kits rename a row. `~id` lifts the EXCESS of a x-multiplier row
  over x1 (the boost's `surge_lift` 1.5: "+25%" is +50% -> +62.5%).
- **I3 Rarity is deleted; `ItemDef.Tier` (1-10) replaces it**, `Ui.TierColor` replaces RarityColor (a ramp
  from the text colour to Epic), `Tiers.Scrap(t)` replaces `Economy.ScrapValue(Rarity)`.
- **I4 The fit rule**: slot matches AND (the line's category is the hull's, or it has none: chips) AND (it
  names no Needs, or the hull has one of them). A rider's Needs are its count rows; kit parts keep their
  signature Needs. `Equipment.Migrated` and the rack table are deleted (saves disregarded, no migrations).
- **I5 Salvage lifts the ups only** (ItemDef.Ups, unchanged rule); a rider's +n is not an up (a count never
  grows by 3%). Chips take no level (already so).
- **I6 Conditional lines are sheet rows at x1** (the Wraith's stealth_speed pattern), read at one door each:
  `craft_damage`, `execute_damage`, `redline_damage` in `Dealt.Deal` (outgoing), `small_hit_cut` in
  `PlayerShip.Guarded`, `kill_reset` on a kill through `Dealt.Deal`, `web_resist` where the pin is applied.
  Burst Feed (`afterboost_rate`) and Spin-up Feed (`spinup_damage`) need the drive's boost end and F1's
  Ramp on a primary: written as rows now, their door is owed to the reconcile job if the kit shape differs.
- **I7 Game.Version 3 -> 4** (kits_v31 §3.6: the items release). Nothing migrated, nothing refunded.
- **I8 Par** keeps lane F's rows (the model already ran on this law, §4 "Order"); `ItemsParRowsChecks`
  proves the Tiers and Loot rows equal the constants `models/numbers_v2.py` used, so a change to either
  is caught against Par.

## ITEM ASSUMPTIONS ABOUT THE KITS (the reconcile job checks each against the built kits)

Stat ids an item line lifts that the kits own (today's id where one exists; `?` = not in version-l yet):
- A1 primary damage (`@primary`): BB/DD director `main_damage`, CV wing `fighter_damage`; the nine's new
  primaries (spotter, lance, mortar, blade, railgun `rail_damage`, flak, pepperbox, repeater, scattergun) ?
- A2 primary rate `@primary_rate` (main_interval, fighter_interval), range `@primary_range` (main_range,
  fighter_range), tracking `@tracking` (main_turn, fighter_turn), shot speed `@shot_speed` (shell_speed,
  fighter_speed): each new primary's rows join these roles.
- A3 ability output `@output`: Broadside `broadside_mult`, Bomber strike `torpedo_damage`, Long Lance ?,
  TOT ?, Overdrive `overdrive_mult`, Buster ?, Hunters `hunter_damage`, Anchor/Lunge ?, Rod/Reverb/Venom ?.
- A4 ability area `@area` and duration `@duration`: Bubble `bubble_radius`/`bubble_time`, Repair field ?,
  Gravity well ?, Shockwave `wave_range`, Anchor 8 s ?, Taunt 6 s ?, Prism / Whirlwind 2 s ?.
- A5 rider counts: Broadside volleys `broadside_volleys` base **6** (code today 3), torpedoes a bomber
  `bomber_ammo` base 4, Hunters `hunter_count` 6, Flares `flare_count` ? 6. The Warrior and every light
  have no count of 4+ (the Wraith's 7 pellets are a spread, not a count a line lifts).
- A6 drive rows (lane B, wt_drives): `warp_safe` 2400, `warp_rate`, `surge_time` 3 s, `surge_cooldown` 15 s,
  `surge_lift` x1.5, `strafe_thrust`, `strafe_speed` (F24).
- A7 cooldowns: `cooldown_share` on every sheet (Stats.cs) is what "every ability cooldown" lifts.
- A8 signatures no line duplicates: BB CIWS burst (Aegis Array is passive PD only), cloak (Veil), warp off a
  capital, heal (Tender), pull (DD Grapnel, Bastion Gravity well), rewind (Echo), paint (Freighter),
  speed-priced damage (Dart), a web-break button (Web Breaker is passive), rate from a stance.
- A9 the Dart reads every top-speed lift (R6): no Light line or chip lifts `max_speed` except the Engine
  chip (+5%, the lever named in R6) and Burner Drive's boost excess.

## Jobs (foundations first)

- **J1** Foundation + rows: Items.cs (Hulls, Tiers, Roles, Lines, generator); ItemDef.Tier/Cat, Rarity
  deleted; Equipment.Build yields the generator, the old 62 rarity lines and Migrated deleted; Fits by
  category; role expansion in Equipment.Sum; Ui.TierColor; Tiers.Scrap; callers (EquipmentWindow, Radar,
  RecyclerPanel, Yard, Economy, Character.Load). Checks: `ItemsLawChecks` (P(t), leans, +n, scrap from
  literals), `ItemsGeneratorChecks` (48 lines, 480 ids, 11/10/11/10 + 6, no (slot, lean) duplicate in a
  category, every class wears >= 10 of its category's lines, DD no Magazine Core, Warrior no Swarm Rack),
  `ItemsCapsChecks` (T1: up <= 25% / 18% / 22%, price >= -50%, count +/-1, price constant over tiers).
  Harness: every old item id / Rarity / ScrapValue / Migrated check rewritten onto the new ids.
- **J2** Loot row: 70% own hull category + 30% any; base tier 1 + floor((L-1)/4), roll 20/70/10 (clamped
  T1-T10); TioWindow's crate line says tiers. Checks `ItemsLootChecks` (tier bands at 3 levels, category
  share, first-drop levels T2@L1 T10@L33).
- **J3** Per-line compounding: `ItemsLineChecks` -- each of the 48 lines at T1 / T5 / T10 from the spec's
  literals (T1 -> T10 column), on three hulls where it fits (one per varied hull of the category).
- **J4** Conditional doors (I6): craft / execute / redline in Dealt.Deal, small hits in Guarded, kill reset,
  web resist; the x1 rows on the sheet. Checks per door, 3 situations each (range, hull share, target kind).
- **J5** Save at version 4 round trip (`ItemsSaveChecks`), a guest sees an equipped item's effect (rung 5,
  `ItemsGuestChecks`), `ItemsParRowsChecks` (I8), the chip budget (6 slots, 3+3, no level).
- **J6** Frames: `Shots.cs.txt` named frames of the item UI (equipment window with tiered parts, the hold
  sorted by tier, a crate's tier colour, the recycler's scrap by tier).
- **J7** docs: CHANGES.md Unreleased + Handoff, DESIGN.md (the item law, roles), final POST (what the test
  phase owes).
