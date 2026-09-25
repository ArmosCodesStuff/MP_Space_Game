# ECHO — spec card

## Identity
- Light, hull **180**, top speed **260 u/s** (`thrust` 190, `reverse_thrust` 90, `reverse_speed` 95, `turn_radius` 35, `turn_rate` 3.0). *Ships.cs:641-643.*
- Drive: **boost** (the nine's Surge, not warp). `strafe_speed` 130, `strafe_thrust` 520. Shift+A/D strafes; V boosts (+50% top/thrust/strafe, 3 s, 15 s cooldown; never breaks a web). *kits_v31.md §2, §3.4; `Drives.cs` row `Boost`.*
- No passive (kits_v31 §2 table: passive column empty for Echo).
- `Fit = Fit.Guns` — one main mount, cursor-aimed. *Ships.cs:636.*
- Role: "every shot and pulse repeats." *kits_v31.md §2.*

## Primary — Echo Repeater (Space, mouse aims)
`main_count` 1, `main_damage` **22** every `main_interval` **0.5 s** to `main_range` **500 u**, `shell_speed` **620 u/s** = 44 DPS. Each round fired echoes at `echo_share` **0.5x** (11 dmg) `echo_delay` **0.6 s** later, flying straight from the recorded muzzle (row `echo`: not guided, one stop, ghost look) = **+22 DPS, total 66 DPS**. *Ships.cs Nums/Rows; `Shots.Of("echo")`; kits_v2.md ECHO card; kits_v31.md §2 table.*

## Abilities, in LEARN order (`ClassDef.Abilities[2..4]`; Guns/FireMode are the shared gun-primary abilities, unwalled)

### 1. Reverb — key **F**, cooldown **18 s** (`reverb_cooldown`)
Press: **5 s** (`reverb_time`) at `reverb_rate` **x1.2** fire rate while the ability remembers what the guns deal (`OnDealt` accumulates into the slot, tracking the last landed position). On expiry, `reverb_share` **35%** of the stored total detonates in `reverb_radius` **220 u** at the last landing spot. *Ships.cs Reverb rows; `Ab.Reverb` (Abilities.cs:811); kits_v2.md ECHO card.*

### 2. Rewind — key **Q**, cooldown **30 s** (`rewind_cooldown`)
Back `rewind_back` **8 s**: position, heading, velocity **and hull** return to what they were 8 s ago. A snapshot every `rewind_every` **0.5 s** into a 16-entry ring; the Rewind takes the one closest to 8 s ago. Any web on the pilot lets go. The boost's time left is not rewound; the helm clamps a rewound speed to the current top. *Ships.cs Rewind rows; `Ab.Rewind` (Abilities.cs:827); README.md ruling; kits_v31.md §3.4 line 282.*

### 3. EMP — key **E**, cooldown **18 s** (`emp_cooldown`)
Press: every hostile Light/Heavy craft within `emp_range` **300 u** is JAMMED for `emp_jam` **4.0 s** — no shots, no launches, the turret stops tracking, a web on a friend lets go; standoff heavies head back to the edge. A second pulse `emp_echo` **0.6 s** later, from the press point, refreshes the jam (up to 4.6 s total). Bosses, structures, dummies and missiles in flight are immune. No damage. *Ships.cs EMP rows; `Ab.Emp` (Abilities.cs:839); kits_v2.md ECHO card.*

## Interactions (kits_v31.md §3.4, §7)
- **The capitals' warp vs the web-breakers** — the nine lose the warp's web break; the Echo's own answers are **Rewind and EMP**. *§7:467.*
- Rewind is exempted from the drive's snap-pricing rule the capitals' warp needs (no capital has Rewind or Shadow step, so there is no exemption list). *§3.4:210.*
- Boost's time-left survives a Rewind untouched; a rewound speed is clamped to the ship's *current* top (post-boost), not the top at the moment rewound to. *§3.4:282.*

## Rulings touching this class
- README.md: "Approved as written: Battleship, Bastion, Tender, Warrior, Echo, Wraith … with the changes below" — no owner edit fell on the Echo's numbers.
- README.md: "Echo Q = REWIND goes back 8 s: position, heading, velocity AND HULL return to what they were 8 s ago. A snapshot every 0.5 s; the Rewind takes the one closest to 8 s ago."
- README.md / numbers_curve_raids_items.md §8: "a par Echo solo still dies to the Rusty Bucket before the fight ends; the model does not price the Rewind's hull" — an open balance risk, not a built number.

## BUILT (scripts/Ships.cs, scripts/Abilities.cs)
- `ClassDef` row: `ShipClass.LightEcho`, `Ready = true`, `Fit = Fit.Guns`, `Drive = Drives.Boost`.
- `ClassDef.Abilities = { Ab.Guns, Ab.FireMode, Ab.Reverb, Ab.Rewind, Ab.Emp }` (Ships.cs:684) — ids `guns, firemode, reverb, rewind, emp`. `Unlocks.Walled` excludes `Guns`/`FireMode` (both `Weapon = true`), so the level-walled 1-2-3 are Reverb (L1), Rewind (L3), EMP (L6).
- **Check methods that exist** (`tools/smoketest/SmokeTest.cs.txt`, grep exact names):
  `LaneA6dRepeaterRowChecks`, `LaneA6dRepeaterChecks`, `LaneA6dReverbChecks`, `LaneA6dRewindRowChecks`, `LaneA6dRewindChecks`, `LaneA6dRewindHostWatch`/`LaneA6dRewindHostChecks`/`LaneA6dRewindGuestChecks` (`LaneA6dRewindSeen`), `LaneA6dEmpRowChecks`, `LaneA6dEmpChecks`, `LaneA6dEmpHostWatch`/`LaneA6dEmpHostChecks`/`LaneA6dEmpGuestChecks` (`LaneA6dEmpSeen`). None run in the engine yet.

## OPEN
- **`LaneA6dRepeaterRowChecks` (SmokeTest.cs.txt:11265,11270) asserts a stale learn order.** It reads `Classes.Of(ShipClass.LightEcho).Abilities.Select(a => a.Id).ToArray()` (the FULL array) and checks `.SequenceEqual(new[] { "guns", "firemode", "reverb" })` — 3 entries. The built `ClassDef.Abilities` for `LightEcho` has **5** entries (`guns, firemode, reverb, rewind, emp`, Ships.cs:684). `SequenceEqual` requires equal length, so this assertion is false as written and this Check will read FAIL once the engine runs it — even though `LaneA6dEmpRowChecks` (line 11678) correctly asserts the full 5-element sequence against the same array. Stated, not resolved. The same stale learn order is in `LaneA6dRewindRowChecks` (SmokeTest.cs.txt:11468): `kit.SequenceEqual(new[] { "guns", "firemode", "reverb", "rewind" })` against the 5-entry array also reads FAIL. Only `LaneA6dEmpRowChecks` (11679) is right.
- `Ab.FireMode` is built on G for this class; kits_v31 §2 lists no fire-mode action for it and names G unused (the same R/G gap as the Battleship card's OPEN). Not resolved here.
