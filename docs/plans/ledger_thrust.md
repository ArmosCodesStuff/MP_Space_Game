# ledger_thrust — the stern plume at idle, and cosmetic side thrusters (owner, 2026-09-25 22:35)

## Decisions
1. Idle plume (no push ahead) = a steady half-disc glow on the nozzle, each layer as wide as its bell, bulging aft; the triangle point (flickering) only while the hull pushes ahead (owner's words, README Sprites).
2. "Pushes ahead" is read off the hull's MOTION on every peer alike (`Plume.cs` EngineWatch): push = dv/dt + water_drag x v (midpoint) along the keel > 0.25 x sheet thrust. Owner samples every frame, a remote at each report (span >= 0.03 s), idle after 0.5 s unheard. No new wire.
3. Side jets = `ClassArt.SideJets`, 4 spots per row (bow port, bow stbd, stern port, stern stbd), measured off each hull texture at 12% of the length in from bow and stern, on the row's outermost opaque pixel (alpha8 > 128). Port mirrors nothing: both sides measured.
4. Turn: |angular velocity| > 0.05 rad/s lights the pair that produces it (starboard turn: bow port + stern stbd; port turn: bow stbd + stern port). Slide: push across = d(across)/dt + keel x across beyond 0.25 x strafe_thrust fires the side OPPOSITE the slide (physics-true; "the strafe side" read as the pair that makes it: default, see open).
5. Jet: a Plume flame outboard, its outer layer Clamp(0.02 x Length, 3, 6) u long, accent colour.
6. Callers: EscapePod burns on a held key (owner) / on moving since the last report (remote); DrawPlumes' `active` is now `burn` (AI craft: moving = under power, args unchanged); CharacterCreator idle (half-disc); Fx boost field burning. `PlayerShip.Thrusting` deleted.
7. Guest check: the arena pair; the host flies a script (W 1.5, coast 1.5, A 1.5, D 1.5 s) beside LaneBHostDrives, the guest watches the host's ship through LaneBGuestDrives and asserts burn -> idle half-disc -> port pair -> starboard pair.

## Jobs
- J1 PRE/POST: plume + EngineWatch + SideJets rows + callers + solo/guest checks + frames + README ruling (files below)
- J2: prove (typecheck, quick, rungs thrust_p quick,solo,screens)

### Pointers for the next agent
- scripts/Plume.cs (Outline, Draw, Lit, DrawJets, EngineWatch); scripts/Ships.cs ClassArt.SideJets + 12 rows
- scripts/PlayerShip.cs: _engines (fields near _yawRate), _Process Tick/Watch, RemoteFollow Tick, ApplyState Watch, _Draw
- SmokeTest.cs.txt: ThrustChecks (solo, after LaneA6dStepChecks), ThrustHostFlight/ThrustGuestWatch/ThrustGuestChecks (ArenaMp)
- Shots.cs.txt: ThrustFrames after LaneA6dWraithFrames
- id ranges: none (no wire, no enum). COORDINATOR NOTE: none.

## Log
PRE J1: plume half-disc + side jets + checks; files Plume.cs PlayerShip.cs EscapePod.cs Fx.cs Sprites.cs CharacterCreator.cs Ships.cs SmokeTest.cs.txt Shots.cs.txt README.md; HEAD f7a8803; hashes 7647e97cf597 f1e3d305e240 c9ef879728e4 a07a6a56b138 23bea5a91633 6a8a2c7bc862 9ed943af3920 f7c8115cc077 8531d65236db ba4f5862d65c

## POST J1 (222a94f + frames fix; typecheck 0, quick green; thrust_p solo 21/21 thrust PASS, 121 fails all pre-existing; thrust_s2 screens: 3 frames made, red on the baseline NRE (53 frames on c1e8ad4))
- Checks: ThrustChecks (solo, 21 PASS seed 1783695343), ThrustHostFlight + ThrustGuestChecks (arena pair; six owed to the test phase), Shots ThrustFrames (thruster_idle, thruster_burn, thruster_turn_left: the pair 6 visible).
- 3 solo reds not in the c1e8ad4 union (volley of three, buster run 0, grapnel warped run 1) are seed reds seen in other lanes' runs; nothing of this lane's.
- CHANGES entry: the stern plume is a half-disc glow at idle and the point only while the hull pushes ahead (read off its motion on every peer); tiny side jets at bow and stern light the pair that turns or slides the hull. Known broken: a webbed hull at its pinned cap shows the idle half-disc under forced thrust.
- DESIGN: engines read off replicated motion (push = dv/dt + damping x v), never input; jets placed off the art (SideJets, checked against the texture).
- next: J3 (coordinator, 23:15): turrets draw at their host's layer.
PRE J3 (coordinator 23:15, turrets at their host's layer): Turret.Setup ZIndex 5 -> host's (0, relative); solo TurretLayerChecks; Shots turrets_under_boss; files Turrets.cs SmokeTest.cs.txt Shots.cs.txt; HEAD cb32fdc; hashes 2804789e6fdc 01147b76db0a e6419591d855
