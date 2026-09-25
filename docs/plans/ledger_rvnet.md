# Ledger rvnet — Fable review fixes, scope net (net-RV1..RV10)

## PRE rvnet-1 (tier opus, review fixes)
- intent: apply review_tasks.json net-RV1..RV10 one by one (checks + generic-path code fixes)
- HEAD 1cd733e (version-l)
- files: scripts/Hub.cs ccbc0d4c · scripts/Net.cs bffa495b · scripts/Dealt.cs 24aeb454 · scripts/Turrets.cs 7f6c4b52 ·
  scripts/PlayerShip.cs 097c434d · scripts/Deployed.cs e0461fbf · scripts/Session.cs bc8bfd3b ·
  tools/smoketest/SmokeTest.cs.txt 6d35985a

## POST
- net-RV1 applied: ArenaMp host line asserts agInfo.Token == Session.Tokens[cid] and 32 chars; wire: S2TokenWireThird/Host (Third announces LIVE Guesty's id with deadbeefx4: Guesty kept, _replacing empty) and S2TokenWireGuest + host held line (Guesty announces DROPPED Third's id, no token, during its hold: place kept for Third). Held variant moved to Guesty: Third leaves before Guesty's drop (premise), so Third's own hold is the one a third peer can see.
- net-RV2 applied (code as written: Beat watches Players, sends to _heard, NetWelcomed zeroes silence). Check: solo R2SwitchChecks, a let-in peer never welcomed, read off _silence (counted from join, let go past 8 s, not Full). The guest2 Pretend.Mute rung-5 leg NOT added: an 8 s leg before guest2's join shifts it against the host's fixed start and Guesty's choreography.
- net-RV3 applied: Dealt.Deal returns on a corpse; LaneADamageDoorChecks BB block, 3 runs: no DealtBy, no Landed.
- net-RV4 applied: TurretSpec.Weapon (PlayerShip pd -> Pd, Deployed -> Turret, Hauler -> Turret: kept as before, not null); Turrets uses spec.Weapon ?? Shots.Of(Kind).Id.
- net-RV5 applied: Session.Rejoins per host (Net.HostName = the join target), RejoinFor/Remember; S2TokenChecks 3 varied host pairs; R2cGuestReturns + arena guest read RejoinFor(HostName).
- net-RV6 applied: host sends NetClassKept on a refused class (not to the announcer's own ship); SendIdentity sends the host LAST so a relayed identity reaches a guest before the host's word. Check: GateFixRefitWireHost/Third/Guest in the Mp roles (no Mp role is in the arena: premise; the host holds Guesty's copy in combat, Third holds its copy's HasIdentity off so it takes any announcement).
- net-RV7 applied: Net.HangAll (Shutdown + _ExitTree), _ExitTree closes _answer. Check: R2SwitchChecks, the node leaves the tree with 2-3 invites pending: every gather closed, table empty, re-added.
- net-RV8 applied: N2 draws still/moving/boosting; boosting flees at 1.25 x the fastest burn (1.5 x top_speed never outruns a 273-700 u/s burn: premise) until the Leash re-forms it; asserts 4 members every frame, re-form, re-commit, pinned within 40 s of the stop.
- net-RV9 applied: R2FullKnockHost (after the courier's reply is taken: invites to Full, "full", Say line "guest2 Base knocked, ..." -- the knock carries guest2 Base, not Third: premise -- then hangs them, "free"); guest2's full knock asserts the guest literal. Host start wait now also waits for Third's identity and both Hears.
- net-RV10 applied: RaidsArenaAddsChecks L6 block after N15 (N13 keeps the L1 slot): 3 adds gunship+2 webifiers, one squad, 2600 +-5%, 9 s in formation, worth 18/6.
- CHANGES entry: Rejoin tokens kept per host; a blow on a corpse credits nothing; turret weapon ids from the spec; the host's watchdog drops a guest that never answers the welcome; a refused class change reaches every peer; Net leaving the tree closes pending invites.
- PROOF (HEAD 2440536): rvnet_d quick green; rvnet_c six (seed 11400714819323315540) 241 fails / 3 FAIL LANE / 4975 exc, 4/6 runs -- every red also in rvfr_d/rvhv_c/rvcap_b six at their heads (version-l baseline: FieldsRipYard/ItemsDoor/WingsGunship lanes, "test threw: Sequence contains no elements", NaN exception flood, fingerprint, pellets, webifier latch, hauler hint, session-admits-build). None traced to rvnet. rvnet_b's guest-join reds (753af94) fixed by 2440536. New checks PASS: RV1, RV2, RV3, RV5, RV6, RV7, RV8 (rvnet_b), RV9. RV10 (RaidsArenaAddsChecks L6) never reached: the solo run throws before the boss lanes (baseline).
