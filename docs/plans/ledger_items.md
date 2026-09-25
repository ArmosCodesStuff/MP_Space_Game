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

- A10 (J4) the primary's blows by weapon id, `Items.PrimaryShots` = shell, fighter: Spin-up Feed ramps only
  these; each new primary of the nine adds its Dealt weapon id there.
- A11 (J4) a CRAFT (Escort Hunter, Hunter Chip) is `Tag.Light | Tag.Fighter` and never `Tag.Boss`; Burst Feed's
  window opens when the class's DRIVE row's run ends (`Drive.Row`, lane B), so it waits on no kit.

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

## Step 0 (fresh agent, 2026-09-25): merged version-l 8dcc845 (lane B drives) into wt/items at 893b87b, clean;
typecheck 0 errors. The drive rows (warp_safe, warp_rate, surge_*, strafe_thrust) are now on the sheets.

## J4 PRE -- tier opus
Intent: the conditional doors (I6). Items.Conditions: a row per conditional stat (id, label, door, ceiling,
When), put on the sheet at x1 of every hull a conditional line fits; doors: Dealt (Dealt.Deal via
Credit.Outgoing: craft, execute <35%, redline <50%, spin-up on the primary), Taken (Guarded: small hits
<10% of hull, ceiling 40%), Tracking (PD turret onto craft), Kill (Dealt.Deal: every cooldown left),
Web (ApplyStatus + the pinned top speed, ceiling 60%), AfterDrive (Cadence of @primary_rate for 4 s after
the drive's run ends). IHittable.HullLeft (1 by default; IQuarry and Raider answer theirs).
HEAD 893b87b. Hashes:
  scripts/Items.cs f66dbe6678b5984823a7d9c3afc3a08f65e0fa41
  scripts/Stats.cs eb1e7d677ea45d149db1fd8096f8fdfbdad80c89
  scripts/Dealt.cs 4065547df26ef5773f2128be395cd7b42a275653
  scripts/PlayerShip.cs ec25cb8e083a2c377347f69acd810956ff412ee1
  scripts/Turrets.cs 2f876a91a3b1259af6b0fa4fe86110b74baad7b6
  scripts/ShipClasses.cs 5c455e82cf7a880ad2dfc55ecd8413db75f58eec
  scripts/Missions.cs c7b68c2ffca92855fed21c07ebcc3dbff30b9f1a
  scripts/Raider.cs 5fd3a21aa59a8a69f451cdb5c5d530cc4c0c1a82
  tools/smoketest/SmokeTest.cs.txt 80b5f7052899fcfe29c0bd06dfdeed8db66837a8
## J4 POST
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED. Own diff read.
Files: Items.cs (Door, Blow, Condition rows, RowsOf, ShareOf, Shares, SpinShare, PinnedSpeed, IsCraft, PrimaryShots),
Stats.cs (condition rows on the sheet, group "Conditions"), Dealt.cs (Credit.Outgoing before the blow, NoteKill after
a kill), PlayerShip.cs (Outgoing, NoteKill over every slot once, TrackingOn, HullLeft, WebCut in ApplyStatus and the
pinned top, the small-hit cut first in Guarded, Burst Feed's 4 s window in Cadence for @primary_rate only),
Turrets.cs (PD swing x Credit.TrackingOn(target)), IHittable.HullLeft (1 by default; IQuarry and Raider answer).
Craft tracking lifts POINT DEFENCE only: the main guns follow the cursor and have no target (default, in open).
Checks written (engine-unproven: rungs owed in the final test phase): NEW ItemsDoorChecks (rung 3): the rows'
placement; Escort Hunter T5 x3 runs (136.60 / 100, tracking x1.366 / x1); interaction Escort Hunter + Hunter Chip
add to +33%, chip alone +8%; Executioner T1 at 3 hull shares; Redline T10 at 3 own-hull shares; Ablative T1 small /
big, T10 at its 40% ceiling; Reset Core T1 x3 (no kill keeps, a kill x0.75); Web Breaker T1 / T10 / none, pinned speed
0.2 / 0.4 / 0.68 and the 60% ceiling; Burst Feed during / within 4 s / after; Spin-up ramp, cap 125, PD no ramp,
new target and silence reset. Helpers WearI / WornOffI / HeldI.
Next: J5 (ItemsGuestChecks in a guest role, ItemsParRowsChecks, chip budget), J6 (frames), J7 (docs, final POST).

## J5 PRE -- tier opus
Intent: the guest-role item check and the Par rows check (the save round trip at version 4 and the chip budget
landed in J1: SaveCoverage, ItemsTableChecks). HEAD ce495eb. Hashes:
  tools/smoketest/SmokeTest.cs.txt af0ba219d9974c1ce77f43c07e09ea667188ef06
## J5 POST
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED. Own diff read. Harness only.
Checks written (engine-unproven: rungs owed in the final test phase): NEW ItemsGuestChecks (rung 5, called on the
host's copy of the guest carrier and on the guest's own copy: Magazine Core T9 torpedoes x1.5359 of the class row,
craft rows x1, tracking onto the dummy x1); NEW ItemsParRowsChecks (rung 3: Tiers / Equipment salvage / Loot rows
equal numbers_v2.py's GROW, UP1, MULT1, CHIP1, STEP, LADDER, BAND, own 0.70, scrap; BaseTier at 7 levels).
Next: J6 frames, J7 docs + final POST.

## J6 PRE -- tier opus
Intent: the item UI frames beyond J1's (6b, 58, 58b, 81c recycler by tier, 62 crates in tier colour already
cover the list by tier and the crate colour): a K stats frame of the Conditions group a conditional line fills.
HEAD f72a96c. Hashes:
  tools/screens/Shots.cs.txt ef87838b08ae877fce2afa25e085d9510e555015
## J6 POST
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED. Own diff read. Shots.cs.txt only.
Frames written (engine-unproven: rung 4 owed, read by eye once): NEW 6d_k_stats_conditions (a Dart in Redline T10,
Reset Core T5, Ablative Skin T9: the K stats Conditions group). J1's 81c (recycler by tier), 58/58b (the hold) and
62 (crates in Ui.TierColor) are the rest of the item UI.
Next: J7 docs + final POST.

## J7 PRE -- tier opus
Intent: docs (CHANGES Handoff + Unreleased + Known broken, DESIGN's gear section onto the item law and the
save format 4), the final POST. HEAD 53828a0. Hashes:
  docs/CHANGES.md 13321414a9d735c49becdb3275e710560358186f
  docs/DESIGN.md 98cc510a1f0aa501c03ee2ecad22b382d6fc766e
## J7 POST -- the lane's final
Verdict: done. verify -Quick ALL CHECKS PASSED at the lane's HEAD (0 warnings, 0 findings, UNUSED 0). Own diff read.
Files: docs/CHANGES.md (Handoff line, Unreleased "Items by hull category, lane I", Known broken), docs/DESIGN.md (save
format 4 with no migrations; the line / tier / role / condition law replaces the rarity bullet; "right category").
THE TEST PHASE OWES (engine-unproven, nothing in this lane has run on the engine):
- rung 3 (solo) x2 seeds: ItemsLawChecks, ItemsTableChecks, ItemsLineChecks, ItemsLootChecks, ItemsParRowsChecks,
  ItemsDoorChecks, SaveCoverage, ItemsNoMigrationChecks, RecyclerChecks, GearLevelChecks, ChipChecks and the other
  J1 rewrites. EXPECTED RED: ItemsTableChecks "every stat a part names is on every hull it fits" until the kits
  reconcile (flare_count; @area / @duration rows of abilities not yet built).
- rung 4 (screens): 6b, 6d_k_stats_conditions, 58, 58b, 81c, 10b, 62 by eye once.
- rung 5 (six) x2: ItemsGuestChecks (both ends), the guest carrier's Magazine Core T9, the guest's levelled Helm Drive.
Open for the reconcile job: ITEM ASSUMPTIONS A1-A11 above; Items.Roles and Items.PrimaryShots are the two tables
it edits.

## J8 PRE -- tier opus -- items gate fix (the opus merge gate's three findings)
Intent: (1) Web Breaker acts on a real latch: the pin keeps its own hold clock (Items.WebRound 2 s rounds,
pinned (1 - cut) of each, free the rest; a web's ask is kept whole and its time is the pin's clock), so a
refreshed latch is held 75% of the time at T1; ItemsDoorChecks' single-apply check rewritten onto a real
Webifier latch at none / T1 / T10, timed from the first pinned frame. (2) The echo's blast is weighed once:
Items.Repeats (weapon ids that repeat blows already weighed at the door: the echo) pass Outgoing as they are;
new checks Echo + Redline (3 hull shares) and Echo + Hunter Chip (craft). (3) The boost's slide gets its own
lift row surge_strafe (x1.5; AbilityDef.StrafeStat, PlayerShip.StrafeMult); Convoy Rig lifts ~surge_strafe;
Burner Drive keeps ~surge_lift, which is now top speed and thrust only (§3.3 prints it so).
Files: scripts/PlayerShip.cs, scripts/Items.cs, scripts/Drives.cs, scripts/Abilities.cs,
tools/smoketest/SmokeTest.cs.txt, docs/CHANGES.md, docs/plans/ledger_items.md. HEAD 35d58a4. Hashes:
  scripts/PlayerShip.cs 4d7e78daca3cd2e9ce425b25a0fd8b440754175d
  scripts/Items.cs 10aed943726a400cad99cbccd6917573149dec01
  scripts/Drives.cs fba973520b87d7c6fe366df8d2ee2a232da5574b
  scripts/Abilities.cs 4f130c86b8b5160df23cfb8eb9358de085b58e9d
  tools/smoketest/SmokeTest.cs.txt 03a281a19331b3ec6341f2452e36ca2453217aaa
  docs/CHANGES.md 7290d2da3f29704780c4e9d91e726553686f74c7
## J8 POST
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED (0 warnings, 0 findings, UNUSED 0). Own diff read.
Files: scripts/PlayerShip.cs (web hold clock HoldWeb/_webAsked/_webPhase; Outgoing passes Items.Repeats; Lift enum
+ StrafeMult, Steer's slide and StrafeNow on it), scripts/Items.cs (Repeats, WebRound, WebHoldLeft; Convoy Rig
~surge_strafe), scripts/Drives.cs (surge_strafe row x1.5, labels Speed / Strafe; Boost.StrafeStat),
scripts/Abilities.cs (AbilityDef.StrafeStat), harness, docs/DESIGN.md (three law bullets), docs/CHANGES.md.
Checks written (engine-unproven: rungs owed in the final test phase, rung 3 x2 seeds): REWRITTEN ItemsDoorChecks
Web Breaker (a real Webifier latch on a Warrior at none / T1 / T10, timed from the first pinned frame over 3
rounds: first hold never / 1.50 / 0.82 s, pinned share 100 / 75 / 41%, free < 0.3 s after it dies) + the pure
WebHoldLeft rule; NEW ItemsEchoChecks (Echo + Redline T10 over 3 hull shares; Echo + Hunter Chip T1 onto a craft
and a gunship, 3 situations); NEW ItemsBoostLiftChecks (live: Convoy Rig T1 Hauler / T10 Bastion, Burner Drive
T1 Dart); ItemsLineChecks rows table onto ~surge_strafe + NEW sheet proof on all 3 freighters / 3 lights x T1/T5/T10.
Decisions (defaults, owner questions):
- D-J8a Items.WebRound = 2 s: a web holds in 2 s rounds, pinned (1 - cut) of each (T1 1.5 s held / 0.5 s free).
  The spec gives no round; an unworn ship is pinned exactly as before.
- D-J8b The echo stores the blow as it landed (weighed) and its blast is not weighed again (Items.Repeats), rather
  than storing the unweighed blow and weighing the blast per target.
- D-J8c Burner Drive's ~surge_lift is now top speed and thrust only (§3.3 prints "boost top speed"); before, the
  one row also lifted the slide.
- Behaviour to watch in the test phase: a heavy gunship reads the pin, so in a Web Breaker ship's free part of a
  round it turns for the map's edge and comes back on the next hold.
Next: none; the lane's merge gate re-runs.

## items-J-gate2 PRE
Job: items gate 2 record; tier opus; intent: fix stale surge_lift/slide record (invariant C); files docs/DESIGN.md 9acda2cf0ac09a38c978364e6b23e795c36f1136, docs/CHANGES.md 50391b1ff6fe4628894b21403772c83cdfa7ff58; HEAD fed46ad08b56df7d3343c25b29750bd9ff3e266d
## items-J-gate2 POST
Verdict: done; typecheck + quick green; docs only. Next: merge gate re-runs.

## MERGE M2 (version-l into wt/items, lane merge) PRE
HEAD 7afd1d4cc7a9c0f98f8e0ea81945b3acd74903b9; merging version-l 54db5c3. Conflicts: docs/CHANGES.md, scripts/TioWindow.cs, tools/smoketest/SmokeTest.cs.txt.
## MERGE M2 POST
- docs/CHANGES.md: Handoff and Unreleased, both sides kept (items first, then raids/net2/wings/kits).
- scripts/TioWindow.cs: bounty line keeps raids' AddsLine and items' tier crates line ("mostly T{BaseTier}").
- SmokeTest.cs.txt: boss drop check keeps items' tier literal (T1 or T2) and raids' adds-cleared check; the guest block keeps ItemsGuestChecks and net2's _streamGap = 0.
- Combination red fixed: wings deleted Dealt.Fighter (wing blows credit their row id); Items.PrimaryShots now names "fighter" (same value).
- typecheck 0 errors, verify -Quick ALL CHECKS PASSED. No engine run.

# LANE I RECONCILE (2026-09-25): the items against the built kits (slices 6a-6d and the items merged)

## Step 0 (items-0): wt/items was an ancestor of version-l; `git merge version-l` fast-forwarded to d524c33 (no commit).

## JOB 0: the audit (rung 0: a static read of Ships.cs / Stats.cs / Drives.cs rows against Items.cs, every line on
every hull of its category; the script lives in the scratchpad) and the job list
ASSUMPTIONS A1-A11 against the built kits:
- A1 HOLDS except the Sniper: its primary is the railgun (kits_v31 §2 "Space: Charge railgun"; §3.3 Heavy Barrel
  "blade, railgun, flak"), but `rail_damage` sits in @output, not @primary: the Gunner Chip lifts NOTHING on a Sniper.
- A2 FAILS for the Sniper: @primary_rate / @primary_range have no Sniper row (Rapid Action lifts nothing there; its
  price vanishes). §3.3: "rail charge rate (Overcharge reads it)". Nose-aimed primaries (railgun, blade) have no
  tracking row: Heavy Barrel is UNPRICED on the Sniper and the Warrior.
- A3 FAILS in part: @output misses Time on target (`tot_damage`), the Buster (`buster_damage`), the Repair field's
  strength (`repair_share`), the Carrier's gunships (`gunship_damage`, the model's 8.5 share); `overdrive_mult`
  (x1.5 a rate LIFT) is lifted whole (+25% -> x1.875: +75% of the field) where the model lifts the ability's share
  (+25% of its excess); the Sniper's ability output is the Anchor's lift (`anchor_rate` x2.5; model 16.7), not the rail.
- A4 FAILS in part: @area misses the Tender's `field_radius` (Field Emitter's up is EMPTY on the Tender), the DD's
  Lance run, the Sniper's Anchor reach, the Dart's rod reach, the Wraith's step reach, the Echo's EMP reach; @duration
  misses `repair_time`. The Battleship has no ability reach at all: Salvo / Magazine Core are unpriced on it.
- A5 FAILS for the Flares: `flare_count` exists on no sheet (the count is Decoys.All's literal 6), so Swarm Rack does not
  fit the Sniper (§3.3, numbers_v2 'hvy_swarm' fits SNIPER + WARDEN). broadside_volleys 6, bomber_ammo 4, hunter_count 6 HOLD.
- A6 HOLDS (warp_safe 2400, warp_rate, surge_time 3, surge_cooldown 15, surge_lift 1.5, surge_strafe 1.5, strafe_thrust).
- A7 HOLDS (cooldown_share on every sheet). A8 HOLDS: no line grants cloak, warp off a capital, heal, pull, rewind,
  paint, speed-priced damage, a web-break button, a blink or a CIWS burst (Aegis lifts pd_damage, which the CIWS also
  multiplies: a lift, not the burst). A9 HOLDS: no light line lifts max_speed; the Dart prices its live top (TopNow).
- A10 HOLDS (Spin-up fits freighters: spotter, Dealt.Mortar, Dealt.Lance are their primaries' blows). A11 HOLDS.
- Hull categories: Hulls.All = §3.3's headings exactly. Chip budget (kits_v31 §10 d6 / numbers §8 R7) HOLDS: 6 slots
  (walls L2/4/8/10/12/14), 3 of a kind, LevelKey(Chip) == null, chip T1 8% (Gunner 10%, Engine 5/6%, Targeting 6%).
- Signatures (kits_v31 §2-3): no duplicate. Reset Core (a kill cuts cooldowns, lights) vs the Tender's Resupply
  (a party field, on demand): different trigger and category, as §3.3 designed.
- The harness's ItemsTableChecks "every stat a part names is on every hull it fits" would be RED today on 15 pairs.

Decisions (defaults; in the return's open):
- R-D1 A role row may name an EXCESS (`~id`): the role lifts that x-multiplier row's excess over x1, as a line's `~id`
  already does. Needed so "ability output" on a LIFT row (overdrive_mult, anchor_rate) and "area" on a reach lift
  (anchor_reach) move the ability's share, not the whole multiplier.
- R-D2 @output names the damage (or strength) row of each ability §3.3 names plus every ability the model gives a DPS
  share with a row of its own: + tot_damage, buster_damage, repair_share, gunship_damage, ~overdrive_mult (was
  overdrive_mult), ~anchor_rate (was rail_damage). Left out (no damage row, or a signature): the CIWS, Prism, Ramjet,
  Veil's primed volley, the Grapnel rip, Resupply, the Supercarrier (it flies the fighters: @primary already).
- R-D3 @area gains field_radius, lance_range, ~anchor_reach, rod_range, step_reach, emp_range (each the reach of an
  ability its category's output line lifts); @duration gains repair_time.
- R-D4 The Sniper's railgun joins the primary roles: @primary rail_damage; @primary_rate rail_charge AND rail_reload
  (the cycle is reload + charge: +20% rate is +20% DPS only if both shorten); @primary_range rail_range.
- R-D5 UNPRICED where the hull has no such system (no row invented): Salvo Core and Magazine Core on the Battleship
  (no ability reach), Heavy Barrel on the Sniper and the Warrior (no tracking: nose-aimed). ItemsTableChecks names
  exactly these four; an UP with no row stays an error. Owner question: price them on something else?
- R-D6 `flare_count` becomes the Sniper's row (base 6) and a salvo's count travels in its spawn seed (N = row + 64 x
  count, Decoys.Pack/Unpack; no new RPC), so Swarm Rack fits the Sniper: 6 -> 7 (T1-T5) -> 8 (T6+).

Jobs (foundations first):
- items-J1 the role rows (R-D1..R-D5): Items.cs (role members may be `~id`; Roles edits). Checks: NEW
  ItemsReconcileChecks (sheet literals on 3 hulls x T1/T5/T10), NEW live ItemsAnchorLiftChecks (Tactical Core on the
  Anchor's running lift, 3 situations), REWRITTEN ItemsTableChecks "every stat a part names" (the R-D5 literal list).
- items-J2 the flare count (R-D6): Ships.cs Sniper row, DecoyDef.CountStat, DecoySalvo.Count, Decoys.Pack/Unpack,
  Spawned Decoy seed, Hub.Flares(count), PlayerShip.Pop. Checks: NEW ItemsFlareRiderChecks (rung 3, none / T1 / T6
  through the E key), NEW ItemsFlareRiderGuestChecks (rung 5, the guest draws 7), REWRITTEN ItemsTableChecks'
  wears (Sniper 11) and Swarm Rack fit (Sniper yes, Warrior no).
- items-J3 the record: CHANGES.md Handoff + Unreleased, DESIGN.md (role law), final POST (what the test phase owes).

## items-J1 PRE -- tier opus
Intent: R-D1..R-D5 -- role members may be `~id` (an excess); Roles rows per the audit; ItemsTableChecks' "every stat"
check onto the R-D5 list; NEW ItemsReconcileChecks + ItemsAnchorLiftChecks. HEAD ad8d93183f206165fa9fe9f4b91d5da197eadd32. Hashes:
  scripts/Items.cs 409dcc91cc3d9f81521a7f5ac16e503e415d5a4a
  tools/smoketest/SmokeTest.cs.txt a94b5c6be0ea4eb7b61d6156df0e94b388b0f9dd
## items-J1 POST
Verdict: done. typecheck 0 errors; verify -Quick ALL CHECKS PASSED (0 warnings, UNUSED 0). Own diff read.
Files: scripts/Items.cs (Roles: @primary + rail_damage; @primary_rate + rail_charge, rail_reload; @primary_range +
rail_range; @output + gunship_damage, tot_damage, repair_share, buster_damage, ~overdrive_mult (was overdrive_mult),
~anchor_rate (rail_damage out); @area + field_radius, emp_range, lance_range, ~anchor_reach, rod_range, step_reach;
@duration + repair_time; Members() lets a role member be `~id`, IdsOf / Expand read it), SmokeTest.cs.txt.
Checks (engine-unproven: rungs owed in the final test phase): NEW ItemsReconcileChecks (rung 3; sheet literals on
sniper / warden / tender / hauler / bastion / DD / carrier / dart / wraith / echo at T1/T5/T10); NEW
ItemsAnchorLiftChecks (rung 3, live: none / Tactical Core T1 / T10 on a running Anchor at VaryNear spots, FireRate
x2.5 / x2.875 / x3.3842, the charge 0.8 / rate, reach x1.4 / x1.288, x1 after); REWRITTEN ItemsTableChecks "every stat
a part names" (an up must find a row; a price may miss only the R-D5 four, asserted exactly).
Next: items-J2 (the flare count).
