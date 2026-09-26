# ledger_fixa2ga2nancascadea2ciwsdamage (round a2, kind code)

## PRE
- a2-nan-cascade: Math.Sign(NaN) throws in PlayerShip._Process (Steer 2299, Turrets Swing 345, Hauler 283, Prism 105); rung solo@11400714819323333590.
- a2-ciws-damage: CIWS x3 damage never reaches PD mounts (DamageOf 0 callers, Spec(pd) reads Stats[pd_damage]); rung solo@11400714819323392986.
- Repro tag fixa2ga2nancascadea2ciwsdamage_a; proof tag fixa2ga2nancascadea2ciwsdamage_p.

## POST a2-nan-cascade (effbfee)
- Root: DashCarry kept its row across a refit and flew the new sheet's 0/0: Position NaN, 994 Swing throws; FitClass ends the carry. Exceptions 4974 -> 2.
- Other roots of the named checks: dash fresh-press read against the last dash's clock (lunge at top speed, code); lunge lane wrecked the Warrior (whirlwind, check setup: hull refilled); gunship left the rail's line (check: held Disabled); dart slide trigger needed a placed frame (check).
- New check LaneA6cLungeChecks section 5 (refit mid-dash), red on the old code.
- Proved: solo@11400714819323333590 FAIL 135, solo@11400714819323392986 FAIL 143, solo SEED 2080866651 FAIL 137: every named check PASS. Guest 'host stream 0.4 a second' (six) not run.
- CHANGES entry: a refit mid-dash no longer throws the hull to NaN; a dash pressed again right after a cooldown cut flies.

## POST a2-ciws-damage (dc7d665)
- Spec reads DamageOf (the CIWS x3 on pd_damage); Turret clamps a running reload to one interval of the rate now (x8 reaches it at once).
- Proved: CIWS run x3 PASS (48.0/48.0/48.0-49.5) at both seeds and SEED 2080866651.
- CHANGES entry: CIWS now triples the point defence's damage and its rate lands at once.
