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

## J1 PRE -- tier opus
Intent: foundation + rows (see Jobs J1). HEAD 5305253. Hashes:
  scripts/Equipment.cs dcc2ef54d70a880fe689ce3e2a16290165dba2d2
  scripts/Ui.cs cad90afd6d0c9a98716d8cd2690d282ec339668c
  scripts/Economy.cs 994419b74ff4b1b7fbc90d08e0c3bb4011f254ea
  scripts/EquipmentWindow.cs 6fd9f8715e30c3d321d1dbaf24cab65c77c56970
  scripts/Radar.cs 0d2c845a77a13d6dc94458993a6677727ff0b501
  scripts/RecyclerPanel.cs d49f462fa515b8d2b5ec1a423323465bec184a3d
  scripts/Yard.cs a113a6af43ba0e9244388d287024b06641e45cc5
  scripts/Character.cs d127ed2ec2b06d48c6df59fff8c55f8d47721ae0
  scripts/Loot.cs 77b8e410e42325f092cce77f48d3e534fb77016e
  scripts/TioWindow.cs ad768024515d17771331cfc8526db77054bcf28c
  scripts/Game.cs 0041e7a9e744ccba90957f8afef62aa42afb1324
  tools/smoketest/SmokeTest.cs.txt 5e2cd742ace0e8bc1d754c2658dbf5e46756d3f7
  tools/screens/Shots.cs.txt 740a6bba9f9f8212030798d5ed42e737da5276cc
  scripts/Items.cs (new)
## J1 POST
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED (0 warnings, 0 findings, UNUSED 0). Own diff read.
Files: scripts/Items.cs (new: Hulls, Tiers, Price, ItemLine, Items.Roles/IdsOf/Expand/Lines/Build), Equipment.cs (Rarity,
Migrated, the 62 old lines and Line() deleted; ItemDef.Tier/Cat/Line; Fits by category; SheetOf/BaseOf; Sum and Describe
expand roles), Loot.cs (J2's code landed here: BaseTier, RollTier 20/70/10, 70% own category), Ui.TierColor, Economy
(ScrapValue deleted), Yard, RecyclerPanel, EquipmentWindow, Radar, TioWindow ("mostly T{n}"), Character.Load (no
Migrated), Game.Version 4, Ships.cs (EveryReachStat deleted: its one caller is gone), SmokeTest.cs.txt, Shots.cs.txt.
Checks written (engine-unproven: rungs owed in the final test phase):
- NEW methods: ItemsLawChecks, ItemsTableChecks (catalogue, (slot, lean) dups, what fits, Par lines, caps, chip budget,
  the stacked-share floor, every stat real on every hull it fits, kit fit, other category inert, Describe),
  ItemsLineChecks (48 lines at T1/T5/T10 from §3.3 literals; 8 reference hull lines on 3 hulls x 3 tiers; salvage lifts
  ups only, not the rider's +n), ItemsLootChecks (crates, base tier, 20/70/10 at L1/L20/L40, 70% own category).
- REWRITTEN onto the new ids: SaveCoverage (round trip incl. hot_barrel_t10; tampered file; version 4 stamp),
  ItemsNoMigrationChecks (the old "file before the destroyer" block: old ids read as nothing, no refund), RecyclerChecks
  (scrap by tier 100/125/244/745), GearLevelChecks (Heavy Battery / Director Suite), ChipChecks (hull literals
  1.08/1.08/1.16/1.24; Combat chip no longer costs hull), the refit fraction (Bulwark Belt T10/T1), Salvo Core on the
  DD, the carrier's hangar block (counts via Character.Bonuses; Magazine Core T1/T5/T9 on the carrier), bombers via
  Bonuses, the window's level purchase (Bulwark Belt +25% -> +25.75%), NeedsSaid words, K window (Magazine Core 4 +1 = 5,
  top -17% red), L1 boss crates T1-T2, the arena/Mp guest carrier (Magazine Core T9: 6 torpedoes on the host's copy),
  the guest's levelled Helm Drive (rudder x1.265 / x1.2725). Shots: 6b_k_stats_gear, 58_equipment_hold, 58b, 81*, 10b.
Test phase owes for J1: rung 3 (solo) x2 seeds on all of the above; rung 4 frames 6b, 58, 58b, 10b by eye; rung 5
(six) for the Mp carrier and Helm Drive checks. EXPECTED RED until the kits reconcile: ItemsTableChecks "every stat a
part names is on every hull it fits" (craft_damage & the other I6 rows until J4; warp_safe/warp_rate/surge_*/
strafe_thrust until lane B merges; flare_count; @area / @duration rows the kits' abilities do not yet join).
DESIGN FLAG for the owner (open): a capital can pay top speed in four slots + three Combat chips = -108% (the sheet's
x0.1 floor holds it at 10.4 u/s); the spec prices each line alone, never the stack.
Next: J2 is now only the TIO line (done in J1) -> fold into J7; next job J3's content landed in ItemsLineChecks, so the
next job is J4 (conditional doors), then J5 (guest-role check ItemsGuestChecks, ItemsParRowsChecks), J6 (frames beyond
58: a crate's tier colour, the recycler list by tier), J7 (docs + final POST).
