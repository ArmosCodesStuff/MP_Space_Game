# SNIPER: one round in the chamber, a 3.0 s auto-reload, a 0.6 s sweet spot (the owner's literals).
# Design model for sniper_active_reload.md. Method as power_v3.py / power_v31.py: "realistic" is what a
# pilot lands on a lone boss; the v1 Anchor (8 s up, 12 s cooldown from release, 0.3 s release) sets
# the anchored duty, and its x2.5 runs the reload, the spot and the charge alike (one rate door).
#
# SPACE, one meaning per stroke, fixed at its press:
#   chamber NOT seated (reloading): the press is the reload press (the first of a reload only); its
#                                   release does nothing.
#   chamber SEATED:                 the press starts the charge; the release fires it.
# A PERFECT press (inside the spot) ENHANCES the round being loaded: x PERFECT when it is fired.
# `completes` = the alternative where a perfect press also finishes the reload at once.
import itertools

RELOAD, SPOT = 3.0, 0.6                      # the owner's literals
ANC_RATE, ANC_UP, ANC_CD, ANC_REL = 2.5, 8.0, 12.0, 0.3
DUTY = ANC_UP / (ANC_UP + ANC_CD + ANC_REL)  # 0.394, v3's
MEDIAN, LOWEST_LIGHT = 51.5, 64.0            # v3.1 table (v3: 51.8)

class Pilot:
    # h: share of reloads with a perfect press; at: where in the spot it lands (0 = the opening edge)
    # retap: perfect press -> charge press (only when the perfect also seats the round)
    # react: the round seating -> the charge press;  release: full charge -> release;  land: lines that land
    def __init__(s, name, h, at, retap, react, release, land):
        s.name, s.h, s.at, s.retap, s.react, s.release, s.land = name, h, at, retap, react, release, land
BOT     = Pilot("bot (sheet)",       1.00, 0.00, 0.00, 0.00, 0.00, 1.00)
CEILING = Pilot("skilled ceiling",   1.00, 0.20, 0.12, 0.18, 0.08, 0.95)
SKILLED = Pilot("skilled (90%)",     0.90, 0.40, 0.15, 0.20, 0.10, 0.94)
REAL70  = Pilot("realistic (70%)",   0.70, 0.50, 0.20, 0.25, 0.15, 0.93)
REAL    = Pilot("realistic (65%)",   0.65, 0.50, 0.20, 0.25, 0.15, 0.93)
REAL60  = Pilot("realistic (60%)",   0.60, 0.50, 0.20, 0.25, 0.15, 0.93)
NEVER   = Pilot("never presses",     0.00, 0.50, 0.20, 0.30, 0.15, 0.93)
PILOTS = (BOT, CEILING, SKILLED, REAL70, REAL, REAL60, NEVER)

def dps(D, perfect, spot_at, charge, p, rate, completes=False):
    T, a, w, C = RELOAD / rate, spot_at / rate, SPOT / rate, charge / rate
    plain = T + p.react + C + p.release                               # shot -> shot
    good = (a + p.at * w + p.retap + C + p.release) if completes else plain
    return (p.h * D * perfect + (1 - p.h) * D) / (p.h * good + (1 - p.h) * plain) * p.land

def blended(D, perfect, spot_at, charge, p, completes=False):
    return ((1 - DUTY) * dps(D, perfect, spot_at, charge, p, 1.0, completes)
            + DUTY * dps(D, perfect, spot_at, charge, p, ANC_RATE, completes))

def solve_D(perfect, spot_at, charge, completes=False):
    return MEDIAN / blended(1.0, perfect, spot_at, charge, REAL, completes)

def table(title, rows, completes):
    print(f"\n{title}")
    print("| spot | perfect | charge | full D | enhanced | never | 60% | 65% | 70% | 90% | ceiling | bot | ceiling < 64? |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for spot_at, perfect, charge in rows:
        D = solve_D(perfect, spot_at, charge, completes)
        r = {p.name: blended(D, perfect, spot_at, charge, p, completes) for p in PILOTS}
        ok = "yes" if r[CEILING.name] < LOWEST_LIGHT else "**no**"
        print(f"| {spot_at:.1f}-{spot_at + SPOT:.1f} | x{perfect:.2f} | {charge:.1f} | {D:.0f} | {D * perfect:.0f} | "
              + " | ".join(f"{r[p.name]:.1f}" for p in (NEVER, REAL60, REAL, REAL70, SKILLED, CEILING, BOT)) + f" | {ok} |")

if __name__ == "__main__":
    print(f"anchor duty {DUTY:.3f}; realistic target {MEDIAN} at 65% perfect; the ceiling must stay under {LOWEST_LIGHT}")
    table("A PERFECT PRESS ENHANCES THE ROUND ONLY (the reload runs its 3.0 s) -- the spot's place moves no DPS",
          [(1.2, b, c) for c in (0.6, 0.8, 1.6) for b in (1.3, 1.4, 1.5, 1.6)], False)
    table("A PERFECT PRESS ALSO FINISHES THE RELOAD AT ONCE (Railgunner's way)",
          [(s, b, 0.8) for s in (1.2, 1.5, 1.8, 2.2) for b in (1.0, 1.25, 1.5)], True)

    # ---- THE PICK ----
    D, P, S, C = 120, 1.5, 1.2, 0.8
    print(f"\nTHE PICK: reload {RELOAD} s, spot {S:.1f}-{S + SPOT:.1f} s, x{P} on the round, charge {C} s, full {D} -> enhanced {D * P:.0f}")
    for p in PILOTS:
        un, an = dps(D, P, S, C, p, 1.0), dps(D, P, S, C, p, ANC_RATE)
        print(f"  {p.name:17s} unanchored {un:5.1f}  anchored {an:6.1f}  blended {blended(D, P, S, C, p):5.1f}")
    print(f"  shot to shot, plain: {RELOAD + 0.25 + C + 0.15:.2f} s free, {RELOAD / ANC_RATE + 0.25 + C / ANC_RATE + 0.15:.2f} s anchored")
    print(f"  anchored: reload {RELOAD / ANC_RATE:.2f} s, spot {S / ANC_RATE:.2f}-{(S + SPOT) / ANC_RATE:.2f} s "
          f"({SPOT / ANC_RATE * 1000:.0f} ms), charge {C / ANC_RATE:.2f} s")
    print(f"  anchored under a Tender's Overdrive (x3.0 additive): reload {RELOAD / 3:.2f} s, spot {SPOT / 3 * 1000:.0f} ms")
    print(f"  K window's railgun row (every reload perfect): {D * P:.0f} every {RELOAD + C:.1f} s = {D * P / (RELOAD + C):.1f} DPS"
          f" ({D / (RELOAD + C):.1f} with none)")
    # a released-early charge: 40% at 0 s .. 100% at full. Full is always right (DPS rises with the hold):
    for hold in (0.0, 0.4, 0.8):
        dmg = D * (0.4 + 0.6 * hold / C)
        print(f"  released at {hold:.1f} s: {dmg:.0f} ({dmg * P:.0f} enhanced), {dmg / (RELOAD + hold):.1f} DPS plain")
