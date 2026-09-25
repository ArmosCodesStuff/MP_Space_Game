# SNIPER · ACTIVE RELOAD: one round in the chamber, a timed Space (design only, nothing built)

Design only. Nothing in the repo was edited, built or run. The numbers come from `sniper_reload.py`, in this folder. It uses the method of `power_v3.py` / `power_v31.py`: the v1 Anchor's duty is 0.394, and the v3.1 median is 51.5.

## Owner summary

1. **The chamber.** The railgun holds one round. After every shot it reloads by itself in **3.0 s**. Press Space while the white box (**1.2–1.8 s**) is under the fill and that round hits **50% harder: 180 instead of 120**. You get a white flash and a bright tick. Press outside the box and you get a grey flash and a dull click. Nothing else is lost.
2. **Space does one thing at a time.** While the bar is grey (reloading), a press is the timing press, and only the first one counts. Once the round is in, hold Space to charge (**0.8 s**, the bar turns blue) and let go to fire.
3. **A perfect press does not finish the reload early.** If it did, a good Sniper would reach 74–84 DPS, above every light. Enhancing only puts the Sniper at **51.7** at a 65% hit rate (the median is 51.5), and at **63.4** for a flawless pilot. The lowest light is 64.
4. **The Anchor's x2.5 runs the whole bar.** Anchored, the reload is 1.2 s, the box is 0.24 s, and the charge is 0.32 s. It stays a fifth of the bar in every state.
5. **Online.** The host judges each press on the pilot's own timing. It believes that timing only within the connection's measured jitter, and never by more than 0.1 s. There is one press per reload, so nobody can do better than a flawless player.
6. **Scope: a new table.** `ActiveReload.cs` is the chamber and the timed press, and any gun row can name one. The Sniper gets its own rows. It builds on F7 (the charge railgun on Space), in the class batch's heavy slice. It is proved at rungs 3, 4 and 5.

**The one question (default in bold).** Should a perfect press *also* finish the reload at once, as Risk of Rain's does?
- **Default: no.** It only enhances the round.
- Finishing early stays under the lights only with **no** damage bonus and the box moved to **2.2–2.8 s** (ceiling 62.6). With any bonus of x1.25 or more, the ceiling passes 64 wherever the box sits (§1.3 table).

---

## 0 · What is fixed

| from | fixed | here |
|---|---|---|
| ruling | one round in the chamber, an automatic 3 s reload, a 0.6 s sweet spot, Space presses it, a small bar: a grey bar filling, a white box over the grey, a white flash on a hit and a grey flash on a miss | §1, §4 |
| clarification | everything is on Space. A perfect press **enhances the round being loaded**, and the bonus is applied when that round is fired. The UI shows the round as enhanced until it fires. The charge and the timed press must never ask one key to do two things at once | §1.1, §1.4, §4 |
| v3 / v3.1 decision 9 | "the Sniper's new piece" (Overcharge by default; Fracture; Deadeye) | **Resolved by this ruling.** The active reload *is* the piece. Overcharge, Fracture and Deadeye are never built, and v3's F7 band rows `rail_over_time` / `rail_over_mult` are dropped from the plan. The class table's passive cell reads **Active reload** |
| unchanged | the v1 Anchor (F), Tether mine (Q), Flares (E), hull 240, the 2500 x 14 u line through everything, 3500 u anchored | |

**Today's code is not the kit.** Live, the Sniper has a light cannon on Space and the railgun on **F**:
- 150 damage, a 3 s locked charge, and a 1 s cooldown (`Ships.cs:346-375`, `Abilities.cs:258-269`, `PlayerShip.cs:594-620`).
- The signed kit moves the railgun to Space as F7's Charge primary and deletes the cannon, FireMode and the lock.

**This design lands on that kit.** It changes F7's numbers for the Sniper and adds one table. It does not reorder the batch.

---

## 1 · The mechanic

### 1.1 One key, one meaning per stroke

A Space stroke's meaning is **fixed at its press**, by the chamber's state at that moment:

| chamber when Space goes down | what the press is | what its release does |
|---|---|---|
| **reloading** (the bar is grey) | **the timing press**, judged at once. Only the first press of a reload is judged; later ones do nothing | **nothing**. Holding it across the end of the reload does not start a charge |
| **seated** (the round is in) | **the charge**: the bar refills blue over `rail_charge` | **fires** the round |

- This is why one key never does two things at the same moment.
- A timing stroke can never fire, even if it is still held when the round seats.
- A charge stroke can only begin on a seated round.
- The bar's colour tells the pilot which one a press will be: grey means timing, blue means charge.

**The rhythm:**

```
release: FIRE ─┬─ grey fill, 3.0 s ────────────────────────────────┬─ SEATED ── press+hold: blue, 0.8 s ── release: FIRE
               0 s          1.2 [ white box ] 1.8                 3.0
               a press here is the timing press (the first counts)     a press here is the charge
```

**The owner never raises `Trigger` for a timing stroke.** Today `Trigger` means "Space is held" (`PlayerShip.cs:1132`). For an active-reload gun it means **"a charge stroke is held"**, and the host's charge reads only that. The timing press travels as its own request (§3). So the host never has to guess which of the two a held key was.

### 1.2 The sweet spot: 1.2–1.8 s (40–60% of the bar)

1. **It sits in the middle, as a fifth of the bar.** That is the owner's "0.6 of 3". The white box is the centre fifth of the track, which is the easiest place to read at a glance.
2. **It is 1.2 s after the shot.**
   - A reflexive second press right after firing (a bounce, or a pilot mashing) always lands before the box. Because only the first press of a reload counts, **mashing can never beat pressing once**. That means a miss needs no extra penalty to discourage spam.
   - By 1.2 s the line has landed and the pilot has seen it.
3. **It closes 1.2 s before the round seats.** A late timing press can never be taken for the charge-and-fire stroke, which needs a seated round. A white round is never thrown away on a stray tap.
4. **Humans time a predictable bar well.**
   - Timing a moving bar about 1.5 s ahead is anticipation, with a spread of roughly 5–10% of the interval. That is about 75–150 ms at 1.5 s.
   - The ±300 ms box is 2–4 of those spreads wide. A focused pilot should land it about 95% of the time, and 60–70% in a fight.
   - The spread grows with the interval, so a faster bar (anchored) is timed about as well **in proportion**. This is why the box scales with the bar (§1.5) rather than staying 0.6 s long.
5. **Because the reward is damage, not time (§1.3), where the box sits changes no DPS.** It is chosen only for reading and for keeping Space's two meanings apart.

### 1.3 A perfect press: what it grants, and what a miss costs

- **Perfect: the round being loaded is ENHANCED, x1.5 (`rail_perfect`).**
  - The multiplier is stored on the chamber and applied to whatever that round is fired as: a full charge gives 180, a tap gives 72.
  - The reload still runs its 3.0 s.
  - The round shows **white** (the chamber pip on the bar, and a glint at the muzzle) until it is fired.
  - A seated white round can be held as long as the pilot likes. Holding it costs DPS already, so there is no timer on it.
- **Miss: nothing is lost except the chance.**
  - The press is spent, the box greys out for the rest of that reload, and the round seats plain at 3.0 s.
  - Why nothing more:
    - Spam already punishes itself (§1.2).
    - A jam penalty (Gears of War's) would make pressing a bad bet below about a 40% hit rate. New pilots would then learn not to press, which is the opposite of teaching the mechanic.
    - The grey flash and the click are the whole cost.
- **Why the perfect does not also finish the reload at once** (Railgunner's way). The ruling sets one test for skill: a pilot hitting 60–70% sits at the median (about 51), and a flawless pilot stays under the lowest light (64). That allows a 1.24x gap between the two.
  - **Finishing early** saves up to 1.8 s of a 4.2 s cycle. That time saving alone gives a gap of 1.29x or more. Add any damage bonus and it passes the lights, wherever the box is.
  - **Enhancing only** makes the gain linear in the hit rate, and the gap at x1.5 is 1.23x.

  From `sniper_reload.py`. The damage is solved so the 65% pilot is at 51.5; a 0.8 s charge; 3.0 s reload and 0.6 s box:

  | the perfect press | box | bonus | full / enhanced | never presses | 60% | **65%** | 70% | 90% | **flawless** | bot (sheet) |
  |---|---|---|---|---|---|---|---|---|---|---|
  | **enhances only (this design)** | 1.2–1.8 | **x1.5** | **120 / 180** | 38.2 | 50.7 | **51.7** | 52.7 | 59.6 | **63.4** | 75.4 |
  | enhances only | 1.2–1.8 | x1.4 | 126 / 176 | 40.1 | 50.7 | 51.5 | 52.3 | 58.6 | 62.0 | 73.7 |
  | also finishes the reload | 1.2–1.8 | x1.0 | 122 | 39.0 | 50.4 | 51.5 | 52.7 | 63.6 | **74.0** | 97.3 |
  | also finishes the reload | 1.2–1.8 | x1.5 | 92 / 138 | 29.4 | 49.4 | 51.5 | 53.7 | 69.6 | **83.8** | 110.1 |
  | also finishes the reload | 1.8–2.4 | x1.25 | 117 / 146 | 37.2 | 50.3 | 51.5 | 52.7 | 62.7 | **71.3** | 89.4 |
  | also finishes the reload | 2.2–2.8 | x1.0 | 145 | 46.2 | 51.1 | 51.5 | 51.9 | 57.4 | 62.6 | 76.9 |

  "Flawless" means every press lands at 20% into the box, the pilot re-presses in 0.18 s and releases in 0.08 s, and 95% of lines land. It is an extreme human.
  "Bot" means no human delay and every line lands. That is the most any client, honest or not, can reach (§3.5).

- **The flawless margin is 0.6 DPS (63.4 against 64).** The probe (H) replaces this model. If it measures a flawless pilot over 64, the lever is one row: `rail_perfect` x1.4 gives 62.0.

### 1.4 The charge stays, and shortens

- **It stays.** Kit sign-off approved a charge railgun ("plants, charges, pierces"), and "charge x2.5" is what the Anchor does.
- **It shortens: full at 0.8 s (v1: 1.6 s).**
  - The reload is now the long clock between shots. The charge is only the aim-and-commit beat.
  - At 1.6 s, a shot would come every 5 s.
- **Releasing early fires a ramp, and the shot is always a full line.** Damage rises from **40%** at 0 s (`rail_tap`) to 100% at full.
  - The kit's "tap for 8, first body" is deleted, because one round is too precious to plink with.
  - The shot is still the 2500 x 14 u line through everything, however short the hold.
  - Full is always the better choice: 16.0 DPS released at once, 24.7 at 0.4 s, 31.6 at full.
- **F7 is simplified.** Its band table for the Sniper becomes two rows: {0 s, 0.40, line} and {full, 1.00, line}.

### 1.5 The Anchor, and every other rate

- **The Anchor's x2.5 is a RateStat, and a rate lift has one door: `PlayerShip.Cadence` (`PlayerShip.cs:165`).**
  - The reload's length is `Cadence("rail_reload")`, fixed when the shot leaves.
  - The charge is `Cadence("rail_charge")`.
  - The box is a share of the reload, so it scales with it.
- **The Anchor row is untouched (v1).** Its "charge x2.5" now also runs the reload. That is because the lift reaches every interval through Cadence, not because of a new rule.

| state | reload | box | charge | shot to shot (a 65% pilot) |
|---|---|---|---|---|
| free | 3.00 s | 1.20–1.80 s (600 ms) | 0.80 s | 4.20 s |
| anchored (x2.5) | 1.20 s | 0.48–0.72 s (240 ms) | 0.32 s | 1.92 s |
| anchored + a Tender's Overdrive (x3.0, lifts add) | 1.00 s | 0.40–0.60 s (200 ms) | 0.27 s | — |

- **A reload started before the Anchor keeps its length**, like every cooldown in the game. The next reload runs at the new rate. This keeps owner and host agreeing on the length from the shot onward.
- **If the box kept its 0.6 s under the Anchor,** it would cover half of a 1.2 s bar and every anchored press would land.

### 1.6 The power-table row

| class | tier | hull | sheet | realistic | flawless | never presses |
|---|---|---|---|---|---|---|
| SNIPER | heavy | 240 | **75.4** | **51.7** (v3: 51.0, Overcharge) | 63.4 | 38.2 |

- The fleet median moves from 51.5 to **51.9**.
- Lights stay on top: the lowest light is 64, and the highest non-light is still the DD at 56.6.
- The L1 hull curve (3222) is set from the probe's median, not from this table (v3.1 §9 mitigation), so nothing else moves.

---

## 2 · As rows

### 2.1 The Sniper's own rows (`ClassDef.Rows`, `Ships.cs`)

These replace the railgun rows at `Ships.cs:364-370` together with F7's Sniper numbers. Gear, pilot points and lifts move them like any other row.

| id | label | base | unit | Inverse | moved by |
|---|---|---|---|---|---|
| `rail_damage` | Damage, full charge | **120** | | | the pilot's Weapons points (`Damage["rail_damage"]` = **6.0**, 5% of 120; was 7.5 of 150); `rlg_slug` +50%; `rlg_quick` −25% |
| `rail_charge` | Charge to full | **0.8** | s | yes | Gunnery (`Cycle`); `rlg_slug`, `rlg_quick`; every rate lift |
| `rail_tap` | Released at once | **40** | % | | none today |
| `rail_reload` | Reload | **3.0** | s | yes | **NEW.** Gunnery (`Cycle["rail_reload"]` = 1); every rate lift; `rlg_wide` |
| `rail_spot_at` | Sweet spot opens | **40** | % of the reload | | none today (the box's place moves no DPS, §1.2) |
| `rail_spot` | Sweet spot | **20** | % of the reload | | a part may widen it later |
| `rail_perfect` | Perfect round | **1.5** | x | | a part may raise it later |
| `rail_range` | Reach | 2500 | u | | unchanged |
| `rail_width` | Beam width | 14 | u | | unchanged |

- **Deleted in the same edit:** `rail_cooldown`.
  - The kit's 0.2 s recovery is replaced by the reload.
  - Its one part term, `rlg_wide`'s `("rail_cooldown", -0.20)` at `Equipment.cs:292-293`, is re-pointed to `("rail_reload", -0.20)`. The blurb becomes "a far wider beam; less reach and a longer reload".
  - Nothing else reads `rail_cooldown` (grep: `Abilities.cs:268`, `PlayerShip.cs:619`, `Stats.cs:341-342`, all rewritten below).
- **Why the box is a share of the reload and not seconds:**
  - A share can never leave the bar. A part that cut the reload to 1.5 s would put a box written in seconds (1.2–1.8) past the reload's end.
  - A share also scales with every rate and every reload part, by one rule.
  - The K window prints "40% of the reload". The Hint and the Blurb say it in seconds at x1 ("0.6 s of 3 s").
- **`Dps.Railgun` (`Stats.cs:339-343`) is rewritten, sheet convention:**
  - Rate: `rail_damage * rail_perfect / (rail_reload + rail_charge)`, which is **47.4** at base.
  - Note: `"180 every 3.8 s, the reload and the charge, with a perfect reload every time (120 without)"`.

### 2.2 The contract: `scripts/ActiveReload.cs` (new, named for the mechanism)

```csharp
// ─────────────────────────────────────────────────────────────────────────────
// ACTIVE RELOAD -- a gun that holds its rounds in a CHAMBER, reloads by itself after every shot,
// and pays a press timed inside a SWEET SPOT of that reload: the round being loaded is ENHANCED.
// New: nothing did this before. The Sniper's railgun is its first row; nothing here knows that.
//
// A NEW ROW MUST FILL IN: the ability id whose Slot holds the chamber (PlayerShip.Slot: Left =
// seconds of reload left, Own = this reload's full length, N = the Chamber below), and the four
// stat ids it reads -- the reload (seconds, Inverse, so every rate lift runs it), where the spot
// opens and how wide it is (percent of the reload), and the multiplier a perfect round carries.
// Those are the class's own rows (ClassDef.Rows), so gear moves them. The gun's own code calls
// three doors and nothing else: Spent (it fired), Seat (its row's Elapsed), Take (the multiplier on
// the round it is firing). The owner's key comes in through Press; the host's verdict is Judge.
// A NEW ROW MUST NOT: add a field to the wire, or a case anywhere that names its gun.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReloadSpec { public string Id, Reload, SpotAt, Spot, Perfect; }

// Slot.N. Reloading: Empty (no press yet), Missed or Perfect (the one press is spent).
// Seated: Seated (plain) or Enhanced. Rides the host's 10 Hz report like every slot.
public enum Chamber { Empty = 0, Missed = 1, Perfect = 2, Seated = 3, Enhanced = 4 }
public enum Verdict { None, Perfect, Missed }

public static class ActiveReload
{
    public static readonly ReloadSpec Rail = new() { Id = "railgun", Reload = "rail_reload",
        SpotAt = "rail_spot_at", Spot = "rail_spot", Perfect = "rail_perfect" };
    public static readonly ReloadSpec[] All = { Rail };

    public static bool Reloading(in PlayerShip.Slot sl) => sl.N <= (int)Chamber.Perfect && sl.Left > 0;
    public static double Progress(in PlayerShip.Slot sl) => sl.Own > 0 ? 1 - sl.Left / sl.Own : 1;
    public static (double open, double close) Spot(PlayerShip s, ReloadSpec r)
        => (s.Stats[r.SpotAt] / 100, (s.Stats[r.SpotAt] + s.Stats[r.Spot]) / 100);

    // HOST: the shot left. The chamber is empty; the reload runs at the rate the ship has now.
    public static void Spent(PlayerShip s, ReloadSpec r)
    { ref var sl = ref s.Sl(r.Id); sl.N = (int)Chamber.Empty; sl.Own = sl.Left = s.Cadence(r.Reload); }

    // Its row's ELAPSED (every peer, the host included: a phase, not a spend -- Abilities.cs:54-56):
    // the round seats, enhanced if this reload's press was perfect. Idempotent, and a fitted ship
    // starts Seated (the fit sets N, as it fills the missile magazine at PlayerShip.cs:300).
    public static void Seat(PlayerShip s, ReloadSpec r)
    {
        ref var sl = ref s.Sl(r.Id);
        if (sl.N <= (int)Chamber.Perfect) sl.N = (int)(sl.N == (int)Chamber.Perfect ? Chamber.Enhanced : Chamber.Seated);
    }

    // HOST, at the shot: what the round is worth (x1 plain, rail_perfect enhanced). The owner's
    // view never reaches this: the multiplier is the host's slot.
    public static double Take(PlayerShip s, ReloadSpec r)
        => s.Sl(r.Id).N == (int)Chamber.Enhanced ? s.Stats[r.Perfect] : 1.0;

    // HOST: THE JUDGEMENT. The owner's claim (how far through the reload it was on ITS clock) is
    // believed only as far as the wire can honestly bend it from the host's own clock; one press a
    // reload; no reload, no press. Leeway is seconds (Net.Leeway), never more than a quarter of the spot.
    public static void Judge(PlayerShip s, ReloadSpec r, double claim, double leeway)
    {
        ref var sl = ref s.Sl(r.Id);
        if (!Net.Sim || !s.Alive || sl.N != (int)Chamber.Empty || sl.Left <= 0) return;
        var (open, close) = Spot(s, r);
        double at = Progress(sl), give = Math.Min(leeway / sl.Own, (close - open) / 4);
        double judged = double.IsFinite(claim) ? Math.Clamp(claim, at - give, at + give) : at;
        sl.N = (int)(judged >= open && judged <= close ? Chamber.Perfect : Chamber.Missed);
    }

    // OWNER: Space went down while reloading (PlayerShip's input, §1.1). The verdict is shown at
    // once from the owner's own clock (View), then the host's word follows on the slot.
    public static void Press(PlayerShip s, ReloadSpec r) { /* View.Said/Flash + Sfx; host: Judge(s, r, p, 0); guest: AskHost(RequestReloadPress) */ }

    // OWNER ONLY, never on the wire: the owner's own clock since its own shot, so the bar and the
    // claim are what the pilot saw, not the host's packet a round trip old (§3.3).
    public struct View { public double Since, Length, Flash, Grace; public Verdict Said; public bool Mine; }
}
```

**Where it is wired.** Every item below is reached through the row, not the type:
- **`AbilityDef.Reload`** is a new field beside `RateStat` / `SpeedStat` (`Abilities.cs:71`): "a gun that reloads actively names its row". The Sniper's Space row sets `Reload = ActiveReload.Rail`, `Elapsed = s => ActiveReload.Seat(s, ActiveReload.Rail)`, and a `Show` that reads the chamber (§4.3).
  - It sets **no `Expire`.** `TickAbilities` runs `Elapsed` on every peer, the host included, and then `Expire` on the host (`PlayerShip.cs:1049-1050`). Seating twice would be harmless, since `Seat` is idempotent, but it would be two ways to do one thing.
  - On a guest, the seat is shown at once; the host's next report confirms it.
- **The owner's input** (the `Trigger` read at `PlayerShip.cs:1132`, moved to the primary's key by F7): on Space going down, `if (def.Reload != null && ActiveReload.Reloading(...))`, the stroke is a timing stroke. It calls `ActiveReload.Press` and holds `Trigger` low until release. Otherwise the stroke is the charge, as F7 has it.
- **The host's fire** (F7's `FireRail` on `Lines.Strike`, F23):
  - damage = `rail_damage × ramp(hold) × ActiveReload.Take(...)`;
  - then `ActiveReload.Spent(...)`;
  - the line's Fx and Beam rows are `rail_enhanced` when the round was enhanced (§5).
  - The host's charge starts on the **later** of `Trigger` rising and the chamber seating (§3.4).
- **Slot fields.** The Sniper's slot uses `Left`, `Own` and `N`. `Cool` is unused, since a chambered gun has no cooldown. **Nothing is added to the wire:** the slot already rides `SendHostState` (`PlayerShip.cs:1259-1276`).

---

## 3 · Multiplayer authority

### 3.1 The clocks, and why the one-way delays cancel

- **The shot.** The owner releases Space at its own time t0. The release rides the owner's 20 Hz report (`SendInterval` 0.05 s, `PlayerShip.cs:262`). The host fires, and starts the reload, when that report arrives: one one-way delay plus a wait of q (0–50 ms) for the next report.
- **The press.** The owner presses at t0 + e on its own clock and sends the request at once (reliable). It reaches the host one one-way delay later.
- **The result.** On the host's clock the press lands at **e − q ± jitter**. The one-way delays cancel, because both legs travel owner to host. What is left is the report's step (at most 50 ms, or 100 ms if one report is lost) and the difference between two one-way delays.
- **So the tolerance comes from the connection's jitter, not its RTT.** ENet measures that jitter per peer as `RoundTripTimeVariance`.

**`Net.Leeway(int peer)`** (new, `Net.cs`, beside `RoundTrip` at `:716-720`) is how far a peer's own clock may honestly disagree with the host's:

```
Leeway(peer) = min(0.10 s, SendInterval + 1/60 s + 2 × RoundTripTimeVariance(peer))
             = 0 for the host's own ship (and single player)
```

- On the host, the peer's statistic is `(peer as ENetMultiplayerPeer).GetPeer(peer).GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTimeVariance)` in milliseconds. Any other transport answers the 0.10 s cap.
- `SendInterval` becomes `public` so `Net` can read it.
- This revives the smallest piece of v2's dropped F11 "per-peer RTT", and nothing more.
- On the harness's -Wan relay (90 ± 25 ms one way, 2% loss, `wan.py`), the leeway comes out at about 0.08–0.10 s.

### 3.2 The request, and the host's judgement

```csharp
// PlayerShip, beside RequestAbility (PlayerShip.cs:460-464)
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
private void RequestReloadPress(string id, float claim)
{
    if (Net.FromPlayer(this, out int who) && who == OwnerId && Abilities.Find(Class, id)?.Reload is { } r)
        ActiveReload.Judge(this, r, claim, Net.Leeway(who));
}
```

- **One request carries the press.** It holds the ability id (so a class could one day have two such guns) and the **claim**: how far through its reload the owner was, as a share (0..1) of the reload length.
- **Why a share and not seconds.** A share does not care whether the owner and host briefly disagree on the reload's length. The host converts the leeway to a share with its own length.
- **The host judges the claim, clamped to its own clock ± the leeway** (`ActiveReload.Judge`).
  - An honest owner is judged on exactly what it saw, unless its jitter spikes past the leeway. Even then only presses within that spike of a box edge can flip.
  - This is the fix for v3's Deadeye risk ("a guest's window is ±50 ms ragged"): the verdict matches the flash the owner already saw.
- **The host player and single player** call `Judge` directly with the exact share and a leeway of 0.

### 3.3 The owner's prediction, and how it gives way

A guest's own slot is overwritten by every host report (`ApplyHostState`, `PlayerShip.cs:1289-1290`). That report is at least a round trip old on the reload's clock. So the owner cannot draw a 0.6 s timing box from it. The owner runs **`ActiveReload.View`**, its own clock:
- **At its own release:** `Since = 0`, and `Length = Cadence("rail_reload")`. `Length` is replaced by the host's `Own` as soon as the host's report of that reload arrives, because the host's length is the truth (for example, the Anchor landed between the two). The bar's fill and the claim are `Since / Length`.
- **At the press:** `Said = Perfect` or `Missed` from its own share, and the flash and the sound play at once. The request is then sent.
- **Giving way.** When the host's slot disagrees with the prediction for longer than `Net.RoundTrip + 0.15 s` (`Grace`), the host wins:
  - The host fired no shot: its chamber is still seated. The view drops the reload and shows the round seated.
  - The host judged differently: `Said` flips, and a second flash plays in the host's colour.
  - The pip always follows the host's `N` once that grace has passed.
- On the host's own ship, `View` mirrors the slot with no grace.

### 3.4 The charge, when owner and host disagree on the seat

The owner raises `Trigger` only for a charge stroke (§1.1). The host's charge begins at the **later of** `Trigger` rising and its own chamber seating, and the host fires on `Trigger` falling only if a charge began.

| case | what happens |
|---|---|
| the owner seated 50 ms before the host | the charge starts 50 ms late, and the hold time (F7's, within one packet) absorbs it |
| the host never seated (the owner's view was wrong) | nothing fires, and the view gives way (§3.3) |
| the owner's view says reloading, the host's says seated | the owner treats the stroke as a timing press; the host ignores the request (the chamber is not `Empty`); the next stroke charges |

### 3.5 No trust holes

| a guest tries to... | the host |
|---|---|
| press for someone else's ship | ignores it: `FromPlayer && who == OwnerId` |
| press with no reload running, or with the round seated | ignores it: the chamber is not `Empty` |
| press twice in one reload (mash, or a script) | judges the first; the rest find the chamber `Missed` or `Perfect` and are ignored |
| claim the centre of the box when it pressed at 0.3 s | clamps the claim to its own clock ± at most 0.1 s (a quarter of the box, anchored), which is a miss |
| send NaN, infinity, a negative number or 7.0 | NaN and infinity are judged on the host's own clock. A finite number is clamped to the host's clock like any claim. The press is spent either way |
| press from a wreck | ignores it (`!s.Alive`) |
| make the enhanced round hit harder | cannot: the multiplier is the host's slot at the host's shot (`Take`) |
| score perfects faster than a human could | cannot. One press per reload, one reload per host-fired shot, one shot per seated round and a charge. The fastest possible is a flawless pilot, and a flawless pilot is **priced**: 63.4 realistic, 75.4 for a bot with perfect aim (§1.3) |

- **The most a lie can buy is the leeway on each side of the box:** up to 0.8 s of 3.0 s, or 0.36 s of 1.2 s anchored. A pilot who lies still cannot pass the flawless row.
- **A timing bot needs no lie.** The bar is predictable, as an aim-bot's target is. The defence is the pricing, not detection.

### 3.6 What other players see of someone else's Sniper

- **No bar, no flash, no tick.** The timing box is the pilot's own instrument. A tick from someone else's ship would be noise.
- **A white glint at the railgun's muzzle while an enhanced round is seated.** It is drawn in `PlayerShip._Draw` from the slot's `N == Enhanced`, which already rides the 10 Hz report. This is the one thing allies can act on: a 180 line is coming, so stay off it or line the boss up. The owner's own ship draws it too.
- **The enhanced shot** draws Fx row `rail_enhanced` (the rail bar in white, 1.5x as wide; the 14 u hitbox is unchanged) and plays Beam row `rail_enhanced` (§5). The host raises both on every peer through the existing `Fx.Line` / `Combat.Flash` paths.

---

## 4 · The UI: `ReloadBar`

### 4.1 Where it goes, and why

The pilot's eyes are on the ship and the cursor, not on the bottom of the screen. Timing a 0.6 s box by glancing at the ability bar at y 976 would cost hits. So the bar **follows the owner's ship on screen**, centred under the hull and always upright.
- It uses the `HaulerHud` pattern (`Hauler.cs:418-425`): a Control on the HUD layer, `Position = canvasTransform × ship.GlobalPosition + offset`.
- The camera centres on the ship, so at 1920×1080 the bar sits at about **x 872–1048, y 620–642**.
- The fixed bottom HUD starts at y 924:
  - the hull panel, x 732–1188, y 924–958 (`HubNodes.cs:70-96`);
  - the ability bar, y 968–1048 (`AbilityBar.cs:17-22`).
  - That leaves **about 280 px of clear space**.
- Nothing else is drawn under the own ship:
  - "+EXP" rises *above* the hull (`Popups.cs:43-44`).
  - Health bars are drawn only over *other* ships (`Hub.cs:1569`).
  - The hints card is at the bottom right, x 1550–1910.

### 4.2 Sizes (screen pixels at 1920×1080; the canvas stretches like the rest of the HUD)

| part | size and place (inside a 180 × 22 Control) | colour |
|---|---|---|
| track | 160 × 8, at (0, 7) | dark grey `#2E333A`, 85% alpha, with a 1 px `#15181C` outline |
| fill, reloading | from the left to the share of the reload done | **grey** `#9AA0A8` |
| **the sweet spot** | a **box 32 × 16** at x 64–96 (40–60% of 160), y 3–19: taller than the track, so it reads as a box *over* the grey | **white** `#FFFFFF`: a 2 px outline plus a 20% fill. After a miss it dims to `#6B7079` for the rest of the reload |
| the chamber pip | 10 × 16 capsule at x 170–180 | empty: an outline. Seated: `#9AA0A8`. **Enhanced: white with a 4 px soft glow, pulsing 0.9–1.0 at 2 Hz until fired** |
| fill, charging | after the seat, a charge stroke refills the same track, left to right, over `rail_charge`; a 2 px white tick at the right end when full | rail blue `#73B3FF` (the rail tint, `Beam.cs:67`) |
| **flash, perfect** | 0.25 s: a white overlay on the whole Control, alpha 0.9 to 0, while the Control scales 1.12 to 1.0 about its centre | `#FFFFFF` |
| **flash, miss** | 0.25 s: a grey overlay, alpha 0.8 to 0, no scale | `#8A9099` |

- **Offset.** The bar's top edge sits `hull half-length on screen + 20 px` below the ship's centre: `MyArt.Length × 0.5 × zoom + 20`, which is 80 px at zoom 1. So it never touches the hull at any zoom.
- **Shown:**
  - from the shot until 0.6 s after the round seats;
  - all through a charge;
  - for as long as an enhanced round waits. Then the track stays full grey, so the white pip has something to sit on.
- **Hidden:**
  - for any class whose Space row has no `Reload`;
  - on a wreck;
  - when the Control is not wholly on screen (the HaulerHud rule; this can happen under the free camera, Y);
  - under the BASE menu;
  - whenever its rect meets the hull panel's or the ability bar's rect. Under the free camera the ship can be pushed to the bottom, and the HUD wins.

### 4.3 The Space slot on the ability bar

The slot's `Show`, through `ActiveReload`, stays in step with the bar, in the slot's own words:

| state | slot text | lit, busy |
|---|---|---|
| reloading | `RELOAD 1.4s` | busy = the share still to go |
| reloading, after a perfect press | `PERFECT` | lit |
| reloading, after a miss | `RELOAD 1.4s` | busy |
| seated | `READY` | |
| seated, enhanced | `x1.5 ROUND` | lit |
| charging | `CHARGE 0.3s` | lit, busy = the charge to go |

- **The controls line (`ClassDef.Hint`):** `SNIPER · Space: hold to charge, release to fire · Space in the white box while it reloads: next round x1.5`.
- **A new `Hints` card row (`reload`), met at the first shot:** "Tap SPACE while the grey bar is inside the white box: the next round hits half again as hard."

### 4.4 The screenshot sweep

- **The linter must see the bar.** `ReloadBar` is added to the overlap list in the lint at `tools/screens/Shots.cs.txt:43`, beside `HullHud`, `AbilityBar`, `BasePanel` and `Hints`. It is a direct child of the HUD CanvasLayer (`Hub.cs:311-348`), `MouseFilter = Ignore`, with a real 180 × 22 rect, so the off-screen and overlap lints both see it.
- **Frame `73c_sniper_reload_spot`**, placed after `73b_sniper_rail_flash` (`Shots.cs.txt:626-636`), using the same Sniper and zoom 0.7:
  1. Fire one real shot.
  2. Step frames until the view's share is 0.5, with the fill in the middle of the white box.
  3. Make the press (Perfect).
  4. Snap 3 frames later.
- The one frame shows the grey track and fill, the white box, the white flash at about 80%, and the pip about to seat white. The frame is expected to give `LINT: 0`.
- Read the image once, for the new art.

---

## 5 · Sound

**Two new files, both made by `tools/make_sounds.py`.** Each is a new row in `SOUNDS`, with a function beside `rock()`. The file's header line changes from "THE BOSSES' SOUNDS" to "THE SYNTHESISED SOUNDS: the bosses' moves and the guns' cues", since it has stopped being true.

| row | recipe | length, peak | heard by |
|---|---|---|---|
| `('rail_perfect', perfect_tick, 0.5)` | **a bright latch.** A 3.2 kHz sine ping over a 1.6 kHz one, 4 ms attack and a 70 ms exponential decay. 30 ms later a small metal knock: 900 Hz with a 40 ms decay and a 4 ms high-passed noise tick | 0.16 s, 0.5 | the owner only, at the press |
| `('rail_miss', miss_click, 0.35)` | **a dull click.** A 25 ms burst of noise low-passed at 700 Hz, over a 160 Hz thump with a 50 ms decay | 0.10 s, 0.35 | the owner only, at the press |

- **`Sfx._gap`** (`Sfx.cs`) gets `["rail_perfect"] = 0.2` and `["rail_miss"] = 0.2`. They play at the owner's ship, which is the view's centre, so they sound at full level.
- **The door they play through is misnamed.** `Sfx.Special(name, at)` says "a boss's special move", but `Fx.cs:181,191`, `Shots.cs:161` and `ThrownRock.cs:71` already use it for other things. It is renamed **`Sfx.ByName`** in the same edit (CLAUDE.md §3, naming rule).
  - Callers: `Boss.cs:631,635`, `Fx.cs:181,191`, `Shots.cs:161`, `ThrownRock.cs:71`.
  - Comments: `Boss.cs:72`, `Fx.cs:48`, `Shots.cs:37`.
- **The enhanced shot's report is a Beam row, not a file.** Append `new() { Id = "rail_enhanced", Tint = new(0.92f, 0.96f, 1f), Report = "laser_boss", Pitch = 0.63f }` to `Beam.All`.
  - 0.63 is two semitones under the rail's 0.71 and more than a semitone from every other row on `laser_boss`, as the header asks (`Beam.cs:24-28`).
  - The index is the wire, so it is appended only. Add `RailEnhanced = 9`.
  - The Fx row `rail_enhanced` is appended the same way (`Fx.cs:73`).
  - Every peer hears it and sees it.

---

## 6 · Checks

The owner's literals are **3.0 s** and **0.6 s**. This design's literals, once approved, are 1.2 s, 1.8 s, x1.5, 120, 180 and 0.8 s.
- Positions use `Vary` / `VaryAngle` / `VaryNear` (ST:151-169).
- Press times are drawn **inside** a range that is never on an edge. The draw decides *when* the press is made, never what is asserted.
- Solo runs at 60 fixed fps, so a press is placed on an exact frame, as ST:6903-6911 does on `stShip.Clock`.

### Rung 3, solo (`run.ps1 -Solo`)

| # | check |
|---|---|
| S1 | **The table against the ruling, once.** `rail_reload` 3.0 s. Spot = `rail_spot`% × reload = **0.6 s**. It opens at **1.2 s** and closes at **1.8 s**. `rail_perfect` 1.5, `rail_damage` 120, `rail_charge` 0.8, `rail_tap` 40. Every check below reads the rows |
| S2 | **Three perfect presses, three seeded times inside the box.** On three reloads, press at `Vary(1.30, 1.70)` s after the shot (at least 0.1 s inside both edges). Each time the chamber reads `Perfect` on that frame, and `Enhanced` after it seats. The next full charge deals **180 ± 0.5** to each of 3 dummies on a `VaryAngle` line placed by `VaryNear` |
| S3 | **Just outside both edges, never on them.** A press at **1.15 s** (3 frames before 1.2) gives `Missed`, the round seats plain, and the shot deals **120**. A press at **1.85 s** (3 frames after 1.8) gives the same, 120 |
| S4 | **One press per reload.** A press at 0.9 s (a miss), then another at 1.5 s: still `Missed`, 120. A perfect at 1.5 s, then another press at 1.6 s: still `Perfect`, and `Sfx.Played["rail_perfect"]` rose by exactly 1 |
| S5 | **The reload is 3.0 s whatever the press.** After a perfect at 1.5 s the chamber is not seated at 2.95 s and is seated at 3.05 s |
| S6 | **Space never does two things.** A timing stroke pressed at 1.5 s and held to 3.4 s, then released: nothing fires (the dummies take 0), and the round stays `Enhanced`. The next stroke (press, hold 0.8 s, release) deals 180. On a fresh plain round, a stroke released after **0.4 s** (24 frames) deals **84** (the ramp's midpoint: 40% + 60% × 0.5 = 70%) |
| S7 | **The Anchor runs the bar.** Anchored, the round seats at 1.2 s (3.0 / 2.5). A press at **0.60 s** is `Perfect`. Presses at **0.44 s** and **0.76 s** (2–3 frames outside 0.48 and 0.72) are `Missed` |
| S8 | **The cues.** A perfect adds one `rail_perfect` and a miss adds one `rail_miss` to `Sfx.Played`. An enhanced shot adds one `rail_enhanced` |
| S9 | **The enhanced round is spent by its shot.** The round after it, with no press, deals 120. A press from a wreck changes nothing |

### Rung 4 (`tools\screens\run.ps1`)

- Frame `73c_sniper_reload_spot`, with `LINT: 0`.

### Rung 5, the wire (`run.ps1`, all roles; the host, guest and guest2 roles at ST:8259 / 8519 / 8415)

| # | role | check |
|---|---|---|
| G1 | guest (and host) | **An honest press on the guest's own clock.** The guest's Sniper fires at a host-placed dummy, then presses at `Vary(1.35, 1.60)` s on its own clock. On -Wan, the host's clock can trail the claim by up to 0.15 s (the report's step plus one lost report), and a resent request can land up to about 0.25 s late. After the 0.1 s leeway, the judged value moves at most 0.05 s earlier or 0.15 s later, so both edges stay out of reach and the check is not flaky. Its `View.Said == Perfect` on that frame, so the flash is instant. Within `RealTime(Wan ? 4 : 2)` the host's report shows `Enhanced` after the seat. The **host** role asserts that its dummy took **180** from the guest's next shot |
| G2 | guest to host | **Impossible claims are refused.** At 0.3 s into a reload, the guest calls `RpcId(1, "RequestReloadPress", "railgun", 0.5f)` directly. On the host's clock that is about 0.1, so it is `Missed`. At 1.5 s into the same reload it sends 0.5 again: still `Missed`. The round seats plain, and the host's dummy takes **120** |
| G3 | guest to host | On a fresh reload, a claim of `NaN` at 1.5 s is judged on the host's clock and is `Perfect`. A claim of `7.0` at 0.3 s is `Missed` |
| G4 | guest2 to host | **Another pilot's ship.** guest2 calls `RpcId(1, "RequestReloadPress", "railgun", 0.5f)` on guest's ship node during its reload. The guest's chamber is still `Empty` on the host |
| G5 | guest2 | **What another pilot sees.** Its copy of the guest's ship reads `N == Enhanced` (the glint). When that round fires, guest2's `Sfx.Played["rail_enhanced"]` rises and `Sfx.Played["rail_perfect"]` does not: the tick is the owner's alone |

- **One mutant, for authority.** It is used once, because G2's wiring is the thing in doubt. Delete the `Math.Clamp` in `Judge`: G2's first claim must then come back `Perfect`, and the check must fail. Restore it.

---

## 7 · Build

**Dependencies.**
- F7 (the Charge primary on Space; the railgun leaves F; the cannon and FireMode go) and F23 (`Lines`) must exist first.
- This lands in **lane A's slice 6c (the heavies)**, in the Sniper's pass, with the Anchor, Tether and Flares.
- It replaces F7's Sniper bands (Overcharge's rows are never written).

| step | what | rung |
|---|---|---|
| 1 | `Net.Leeway`; `PlayerShip.SendInterval` made public | 1 |
| 2 | `ActiveReload.cs` (the contract, the table, the doors, `View`); `AbilityDef.Reload`; `PlayerShip.RequestReloadPress`; the owner's stroke rule in the input; the host's charge latch | 1, then 2 |
| 3 | the Sniper's rows (§2.1); the Space row's `Reload`, `Elapsed` and `Show`; `FireRail`'s `Take` / `Spent` and the ramp; `rail_cooldown` deleted and `rlg_wide` re-pointed; `Dps.Railgun`; `Damage["rail_damage"]` 6.0; `Cycle["rail_reload"]`; the Hint and a `Hints` row; the muzzle glint | 2, then S1-S9 at 3 |
| 4 | the `rail_enhanced` Beam and Fx rows; the two `make_sounds.py` rows (run it once) and the `_gap` entries; `Sfx.Special` renamed to `Sfx.ByName` | 2, then S8 at 3 |
| 5 | `ReloadBar` on the HUD layer; the lint's overlap list; frame 73c | 4 |
| 6 | G1-G5 and the mutant | 5 |

- **Cost: M.** The contract is S. The owner's prediction and the host's latch are the M. The bar is S.
- **Record** in the same commit: CHANGES.md; DESIGN.md (the one-meaning-per-stroke rule, the leeway, why the perfect does not shorten the reload); README.md (sizes).

**Standing invariants.**
- **A (lifetime).** `ReloadBar` goes with the HUD layer. `View` is a field on the ship and subscribes to nothing.
- **B (authority).** The verdict and the multiplier are host-only (`Judge`, `Take`). The seat is a phase every peer shows at once and the host's report confirms (`Seat` through `Elapsed`). The guest asks by one RPC, and the host checks the sender and the chamber.
- **C (replacement).** `rail_cooldown` goes, the kit's tap band goes, and `Special` gives way to `ByName`.
- **D (correctness).** Every new row is read: `rail_spot_at` by `Spot`, and `rail_tap` by the ramp.

---

## 8 · Risks

1. **The flawless margin is thin (63.4 against 64).** The model's flawless pilot is extreme, but the probe decides. The lever is one row: `rail_perfect` 1.4 gives 62.0.
2. **The prediction can snap back.** Under heavy loss (a resent request, or a lost release report), the owner's prediction can flip after its flash. The grace (RTT + 0.15 s) bounds it, and G1 on -Wan measures how often it happens. If it is frequent, widen the grace, not the leeway.
3. **A pre-press near the seat is a miss.** A pilot who presses at 2.9 s "to start charging early" gets a miss, and must release and press again. The grey bar and the grey flash say so at once, and the hints card teaches it. The other design, where a held timing stroke becomes the charge, lets a quick perfect tap fire a 40% shot and throw away the white round. That is worse.
4. **Anchored with an Overdrive, the box is 200 ms.** That is still about 4 of the spreads of anticipatory timing, but it is the tightest state. The leeway's quarter-of-the-box cap keeps a lying client within 50 ms there.
5. **The bar is new art near the ship.** Rung 4's lint sees only the Control's rect. The flash's 1.12x scale draws up to 11 px outside it, and the frame is read once by eye.
