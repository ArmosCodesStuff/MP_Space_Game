# ledger_fixa2ga2nancascadea2ciwsdamage (round a2, kind code)

## PRE
- a2-nan-cascade: Math.Sign(NaN) throws in PlayerShip._Process (Steer 2299, Turrets Swing 345, Hauler 283, Prism 105); rung solo@11400714819323333590.
- a2-ciws-damage: CIWS x3 damage never reaches PD mounts (DamageOf 0 callers, Spec(pd) reads Stats[pd_damage]); rung solo@11400714819323392986.
- Repro tag fixa2ga2nancascadea2ciwsdamage_a; proof tag fixa2ga2nancascadea2ciwsdamage_p.
