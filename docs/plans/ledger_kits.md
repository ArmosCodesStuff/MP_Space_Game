# Ledger: class-kits batch, lane A (combat core)

Writer: one agent in worktree `WarShips_wt_kits`, branch `wt/kits`, started at 03d47ba.
Spec: `kits_v31.md` §8 (Step 0 item 3, lane A slices 1-2), §6 foundations, v3 §5 (`kits_v3.md`),
v2 §5 (`kits_v2.md`). A PRE with no POST is an interrupted job: compare the files with the hashes,
revert or keep the half-made edits, then run the job again (CLAUDE.md §2b rule 3).

## Decisions taken where the spec is silent

(D-numbers; each is the plan's default or the smallest reading of it.)

- **D1** `raids_v2.md` is already in the repo, byte-identical, as `raids_squads_adds.md`. Not copied a
  second time (two copies is two truths); the README row and kits_v31's references name it instead.
- **D2** The copies are named `kits_v2.md` / `kits_v3.md` beside `kits_v31.md`; their text still says
  `signoff_v2.md` / `raids_v2.md` (unchanged copy), so each header maps the old names.
- **D3** Spec read for the code slices: kits_v2 §5 (foundations, OutGuards, build order) and the card
  sections a foundation names; kits_v3 §5 and §7; kits_v31 §6, §8, §10. The rest was diffed / skimmed
  by heading to keep this agent under its context cap.

## Jobs

### J1 · PRE · specs into the repo
- Intent: copy v2 and v3 sign-offs into `docs/plans/` unchanged (one-line origin header), list them in
  the README table, repoint references to scratchpad paths, and add a short `docs/DESIGN.md` section
  pointing at them (kits_v31 §8 Step 0 item 3).
- Files: docs/plans/kits_v2.md (new), docs/plans/kits_v3.md (new), docs/plans/README.md,
  docs/plans/kits_v31.md, docs/plans/numbers_curve_raids_items.md, docs/DESIGN.md, this ledger.
- Start: 03d47baa504be57aad842eccdfff0b49218623bc
- Hashes: README.md 677dd2a1 · kits_v31.md 0b3f77b7 · DESIGN.md 8d15c0e2 ·
  numbers_curve_raids_items.md 72c3f80d

### J1 · POST
- Verdict: done (docs only; no code, no rung needed).
- Scratch `kits3/signoff_v31.md` vs repo `kits_v31.md`: the repo copy carries the 2026-09-25
  reconciliation (L1 3091/2847 -> 3222/2968; the x0.64 cut -> Lancer x0.744 / Drake x0.787; §5 moved
  to `numbers_curve_raids_items.md`; decision 15 reversed: no boss-hull trim for adds; decisions 6, 8,
  9, 14 marked settled; check 11's literals 1.075/3091 -> 1.098/3222) and the Echo Rewind ruling
  (8 s, hull too, 0.5 s ring of 16). Nothing else differs.
- Files: kits_v2.md, kits_v3.md (new); README.md (3 rows, raids row); kits_v31.md (lines 15, 39, 501,
  513); numbers_curve_raids_items.md (line 44); DESIGN.md (one pointer section under Ship classes).
- Next: J2, lane A slice 1.
