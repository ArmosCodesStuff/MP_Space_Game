# ledger_fields (lane D, wt/fields): F9 fields, zones, marks (kits_v31 §6 F9, §3.3; kits_v3 §5, §3.2, §3.5; kits_v2 §5)

BUILD PHASE: no engine run. Per job: typecheck + verify -Quick + a read of the diff. Checks written now, run in the final test phase.

## JOB 0 -- the lane's job list (foundations first)
- J1 FOUNDATION `Fields` (in Fx.cs): a slot-drawn field is a ROW {Id, Slot, Look, RadiusStat|HullShare, Tint, FadeStat, TagStat};
  `Fields.Up(ship)` (pure, what is up now) and `Fields.Draw(ship)` (one switch on Look). PlayerShip._Draw's hardcoded bubble
  block becomes row `bubble` (deleted there). Checks: pure contract + live bubble on 3 varied spots; guest sees the bubble field (rung 5).
- J2 ROWS on J1: `patrol` (slot super: dashed 600 u ring, patrol_range), `taunt` (slot taunt: hex shimmer + "-33%" tag from
  taunt_guard), `boost` (slot boost: the drive's plume), and Fx row `taunt_ring` (the 1000 u flash on the press).
  Checks: pure per-row literals; live wherever a class owns the slot (other lanes build the abilities); frames 81-83.
- J3 FOUNDATION + ROWS: FxShape Debris / Sparks / Puffs / Scar, FxDef.With (companions raised on every peer from ONE raise),
  FxDef.Cap (per anchor), a node that rides nothing but READS its anchor (Loose), `Fx.Rip(anchor, hook, toward)`, seeded tumble
  `Fx.Tumble`; rows rip, rip_sparks, rip_smoke, scar. Checks: rip on dummy / pylon / base / Lancer at VaryAngle, companions,
  scar cap 3, lifetimes, determinism; guest draws the chunk within 20 u of the host's (rung 5); frame 84.
- J4 RECORD: CHANGES Unreleased + Handoff, DESIGN trap note; final POST lists what the test phase owes.

Defaults taken (no ruling found in docs/plans/README.md):
- D1 slot ids the field rows key on: `super` (lane E's Supercarrier), `taunt` (Warden Taunt), `boost` (lane B's drive row).
  A lane that picks another id changes ONE row here. Checks print `NOTE unbound field` until a class owns the slot.
- D2 the rip's DAMAGE (1% MaxHp + 10 through Dealt.Deal, credited grapnel) belongs to the grapnel (lane A F8, not built);
  this lane gives it `Fx.Rip` for the look only. Debris sound `grapnel_rip`: row name reserved, null until a make_sounds row exists.
- D3 Unmask panels: never built in this tree; nothing to delete.
