# Plans: what is designed, what the owner ruled, and what is next

Everything here is DESIGN, not built code, unless a line says otherwise. It was written on the
owner's PC over 2026-09-24 and copied here so a session with no access to that PC (a cloud session)
has the whole picture. The owner's sign-off page (answers stored in its database) is
https://claude.ai/artifact/HGdMkrChYfYDmj5Q1TUJ5x.

Numbers in these files come from the Python models in `models/` (run them from that folder:
`python power_v31.py`, `python numbers_v2.py`, ...). File paths inside the documents that point at a
temporary "scratchpad" folder refer to the originals of these same files.

## The files

| file | what it is | status |
|---|---|---|
| `kits_v31.md` | the 12 class kits, every owner change folded in; power table; build order (9 lanes) | signed off with changes; §10 decisions 6, 8, 9, 14, 15 settled, the rest open (defaults stand) |
| `kits_v3.md` | the v3 sign-off (the scratchpad's `kits3/signoff_v3.md`, unchanged): the base `kits_v31.md` cites as "v3 §x"; foundations §5, build order §7 | signed off; superseded where `kits_v31.md` says so |
| `kits_v2.md` | the v2 sign-off (the scratchpad's `signoff_v2.md`, unchanged): the full class cards and the foundation table v3 §5 amends | signed off; superseded where v3 / v3.1 say so |
| `ledger_kits.md` | the class-kits batch, lane A: jobs, decisions, the engine rungs owed | in progress |
| `sniper_active_reload.md` | the Sniper's one-round chamber with an active reload on Space | designed; one question (below) |
| `level_walls.md` | abilities and chip slots unlocked by pilot level | approved (6 chip slots on every hull) |
| `numbers_curve_raids_items.md` | the progression curve with a chip-free reference pilot, raids/adds pacing, and the items by hull category at +10% a tier | designed and reconciled with the kits (§8); D6/D7 settled, the rest open (defaults stand) |
| `progression_curve.md` | the earlier curve design (its defaults were approved) | superseded in its numbers by the file above |
| `raids_squads_adds.md` | raids v2 (the scratchpad's `raids_v2.md`, byte-identical): raid squads in formation and boss-fight adds, EXP for adds | defaults approved; owner changes in the rulings below |
| `network_webrtc.md` | multiplayer over the official Godot WebRTC plugin, invite codes, Google/Cloudflare STUN only | designed (v2); 3 questions (below) |
| `network_webrtc_spike.md` | proof that webrtc-native 1.2.1 loads and connects in this project's .NET setup, editor and export | done |
| `network_audit_connection.md`, `network_audit_protocol.md` | why real two-machine play never worked, and the protocol bugs found | reference |
| `sprites.md` | mapping of the 34 new ship sprites (in `art_source/pack_2026-09-24/`) onto every entity | defaults approved |

**Reconciled 2026-09-25** (`numbers_curve_raids_items.md` §8): that file owns every curve, boss, raid,
chip and item number; `kits_v31.md` owns what each class does. L1 Lancer 3222 / Drake 2968 (the fleet's
walled L1 median); every non-super move Lancer ×0.744 / Drake ×0.787; no starting chips; the Dart at 72
(every speed lift prices it); the boss hull is NOT trimmed for adds (owner ruled 2026-09-24).
Still true: a par Echo solo still dies to the Rusty Bucket before the fight ends; the model does not
price the Rewind's hull, ruled after it ran, which may close that.

## The owner's rulings (binding; newest win)

Kits and classes
- 1 primary weapon + 3 abilities per class, each ability useful to a pilot flying alone. Lights
  keep the highest DPS; hulls: BB 500 / CV 425 / DD 395, heavies 240-300, lights 180-220.
- Approved as written: Battleship, Bastion, Tender, Warrior, Echo, Wraith; Freighter, Dart, Sniper
  with the changes below.
- Carrier Q = SUPERCARRIER: a second, automated wing patrolling the carrier, engaging anything
  within ~600 u (range moddable) -- missiles included.
- Destroyer Grapnel PULLS the destroyer to the target and tears a chunk off stations, bosses and
  pylons: a visual AND a hit of 1% of the target's total hull + 10.
- Warden Beacon -> TAUNT: the pull plus 33% damage reduction for 6 s, shown on screen.
- Dart keeps a speed button, RAMJET (+10% top a second at full throttle, up to +50%; turning bleeds
  it), instead of Jetwash.
- Sniper: the v1 Anchor unchanged; the new piece is the ACTIVE RELOAD (see its file): one round in
  the chamber; after each shot a 3 s reload; Space in the 0.6 s sweet spot enhances the loaded
  round (x1.5). UI: grey bar, white box for the spot, white flash on a hit, grey flash on a miss.
  Everything on Space. OPEN: should a perfect press also finish the reload at once? Default no.
- Freighter F = TIME ON TARGET, its shots HITSCAN rail lines like the Sniper's, all landing at once;
  sentries LAUNCH to the cursor (600 u, 0.8 s; R recalls); sentries PREFER painted targets, else
  anything hostile, bosses included.
- WARP (hold-to-warp with a range UI; overshoot = disabled 2 s per 300 u over the limit, max 900
  over) is CAPITAL-ONLY (BB, CV, DD). Capitals are slower overall and rely on it. The other nine
  have no warp; their V is a speed + strafe boost: +50% top speed and strafe for 3 s, 15 s cooldown.
- Rate buffs ADD (two x2 = x3). Point defence passive, 1 DPS per mount, 2 on every capital.
- Echo Q = REWIND goes back 8 s: position, heading, velocity AND HULL return to what they were 8 s
  ago. A snapshot every 0.5 s; the Rewind takes the one closest to 8 s ago (built with the class
  batch; kits_v31 §3).
- Supers unchanged: burn 250, rock 250. The Drake stays silent during its throw.
- Enemy heavies: twin laser, each barrel 1.25x the light mean = 2.58 DPS per heavy; heavies never
  own CC (their webifier lights do); heavy missile 35 in 10 s, only at a pinned target.

Chips, walls, balance
- EVERY hull has 6 chip slots, at most 3 combat and at most 3 utility.
- Level walls (every class): ability 1 at L1, chip slot 1 L2, ability 2 L3, chip 2 L4, ability 3
  L6, chips 3-6 at L8 / L10 / L12 / L14. Keys keep their layout; walls read the highest level ever
  reached; the Auto-sell and raid unlocks fold into the same table; kit chips are NOT baked in.
- BALANCE AROUND BASE CLASSES, NOT CHIPS: the curve's reference pilot carries no chips; every boss
  is killed in ~60 s and the pilot survives ~31 s (Lancer) / ~42 s (Drake) at every level. Bosses
  and raids must not fall behind.
- Player respawn 24 s (built: `PlayerShip.StasisTime`); the whole party down at once fails.

Raids and bosses
- Boss-fight adds from the raid manager: the Rusty Bucket's 2 beam escorts are squad wave 1 from
  level 1; from L6 one more enemy every 3 levels (H, L, L, L) to 3 heavies + 9 lights at L39; kinds
  fixed per slot. ANY add's web may start the Rusty's beam; the escape window is never less than
  0.6 s before the first hit; beam damage and rate unchanged. A wiped squad returns after 30 s with
  the same kinds; refills pay no EXP.
- The boss's hull is NOT trimmed for its adds: the scaling is the boss alone against the pilot, and
  a fight with adds may run past 60 s (2026-09-24).
- Curve: a reference-pilot table instead of x1.025 a level; salvage 500 x 1.10 a level, capped at
  the highest boss cleared + 1; salvage levels live on the SLOT, per pilot; siege guns cut (built as
  the cruise missile); level skipping up to +2.

Items
- Split by HULL CATEGORY (capital, freighter, heavy, light): 8-12 themed item LINES each, the same
  item across tiers with better stats; 10 tiers compounding +10% a tier; low-tier caps +25% / -50%
  and +/-1; an item may carry "+25% and +1" of something small; never a duplicate of a signature.
  The item pass lands AFTER the kits.
- EXISTING SAVES ARE DISREGARDED: no migrations, no refunds.

Network and infrastructure
- The official Godot webrtc-native plugin (MIT; the Windows x86_64 DLLs ship with the game).
- NO third-party infrastructure except public STUN lookups from Google and Cloudflare (stateless,
  rows, the game must fall back gracefully). No ntfy.sh, no hosted rendezvous, no TURN accounts.
  Friends connect with INVITE CODES pasted to each other. Today's build still asks api.ipify.org for
  the host's public address -- remove it with the switch (or sooner).
- OPEN (network file): read the reply code off the clipboard while an invite waits (default yes);
  hold a dropped friend's place 90 s (default keep); delete UPnP (default delete).
- The owner has a standing OK for new GitHub releases. Push only on a green bar, to BOTH version-l
  and main.
- Real two-machine play has never worked (the owner is behind double NAT). The harness is loopback
  only: never call multiplayer "working" until the owner and a friend have played.

Sprites
- 34 sprites in `art_source/pack_2026-09-24/`; mapping in `sprites.md`; hit sizes unchanged; BB
  mains on the 4 flanking twins; enemies, bosses, fleet and siege first, player ships with their kits.

Testing (owner, standing)
- Every change carries its check (CLAUDE.md §3). After the features: new Python scenario tools and
  smoke tests covering every mechanic for every class, every ability, and interactions between
  heavies, bosses and lights for every weapon and platform.

## What is built, and what is next

Built and committed (see docs/CHANGES.md): sky parallax, the Drake's warp-back throw, honest stats
(Cadence, additive lifts), stealth choosing-vs-hitting, sentries that fight, guest gear levels, the
practice range south, courier docks, beam sounds, the equipment base with the 10 s combat lock, the
siege cruise missile, 24 s respawn, one holder for loaded assets, and networking slice S1 (admitted
guests, a channel per stream, goodbyes that survive a lossy link).

Next, in order:
1. ~~Reconcile the kits with the numbers~~ (done 2026-09-25, numbers §8).
2. WebRTC: `network_webrtc.md` slices R0-R5 (R0 measures the invite reply window first); then a
   release and the owner's two-machine test (its §4 script).
3. The class batch from `kits_v31.md` (9 lanes), with the level walls, 6 chip slots, warp for
   capitals only, the V drive, strafe, and the Sniper's active reload.
4. The curve, raids/adds and siege numbers from the reconciled model.
5. Sprites (enemies, bosses, fleet, siege first; player ships with their kits).
6. Items by hull category.
7. The full testing program, then the bar, the record, a push to both branches and a release.

The engine ladder (rungs 3-6) runs only on a Windows PC with Godot 4.7.2 .NET (`tools\find-godot.ps1`).
