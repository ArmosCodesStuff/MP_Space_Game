# ITEM TIERS: the tier law, proved against curve.py's Par. Read-only design model; see math.md.
# usage: python tiers.py [all|law|loadout|drops|par|ttk|salvage|flats|budget|bound] [step]
#   step = salvage level step (recommended 0.03; today 0.05)
# Everything here is a ROW the generator reads; nothing below is meant to become an `if`.
import math, random, sys, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import curve as cv          # Par, points, EXP pace, ladder, class rows (B, D, N, f_p)
import player_model as pm   # fleet salvage income

# ---------------------------------------------------------------- the law
LAWS = {'compound': lambda t: 1.2 ** (t - 1), 'linear': lambda t: 1 + 0.2 * (t - 1)}
TIERS = range(1, 11)
B1 = 20.0          # a core slot's budget at T1, in the slot's reference units (+20% of its scope)
CAP1 = 25.0        # owner: the low-tier cap on one up (%)
DOWN_CAP = 50.0    # owner: the low-tier cap on one down (%): NEVER grows
BANK1 = 30.0       # the chip bank's budget at T1 (ship %), split over the class's N chips
LAMBDA = 0.5       # a down on a system the item does not lift buys back half its weighted size
STEP_REC, STEP_TODAY = 0.03, 0.05
MAXLEVEL = 40

def P(t, law='compound'): return LAWS[law](t)
def Pb(t, law='compound'): return math.sqrt(P(t, law))            # the bound law: sqrt of the power law
def G(g, step): return 1 + step * g                                  # slot level multiplier
def base_tier(L, per=4): return min(10, 1 + (max(1, L) - 1) // per)  # the tier a level-L boss drops most

# ---------------------------------------------------------------- 1. the law table
def law_table(step):
    print("\n== 1 · THE LAW: per tier (compound = recommended; linear shown for comparison)")
    print(" t | drops at L | P comp  P lin | core budget | max up (power) | max up (bound) | chip/N=3,4,5,6 | +n flat (n1=1) | down cap | T->T+1 item step comp/lin")
    for t in TIERS:
        lo = 4 * (t - 1) + 1; hi = lo + 3 if t < 10 else 40
        chips = '/'.join(f"{BANK1 * P(t) / n:4.1f}" for n in (3, 4, 5, 6))
        nf = flat_n(1, t)
        step_c = P(t + 1) / P(t) - 1 if t < 10 else float('nan'); step_l = P(t + 1, 'linear') / P(t, 'linear') - 1 if t < 10 else float('nan')
        print(f"{t:>2} | L{lo:>2}-{hi:<2}   | {P(t):5.2f} {P(t,'linear'):5.2f} | {B1*P(t):6.1f}%     | {CAP1*P(t):6.1f}%        | {CAP1*Pb(t):5.1f}%         | {chips} | +{nf}             | -{DOWN_CAP:.0f}%     | {100*step_c:4.1f}% / {100*step_l:4.1f}%")
    print(f"  tier and SLOT level make ONE factor F = P(t)(1 + {step} g); every item is re-solved at F (power ups ~F, bound ups ~sqrt F,"
          f" downs fixed); g <= min(40, highest cleared + 1).")
    print(f"  T10 at max level: power x{P(10)*G(40,step):.2f}, bound x{math.sqrt(P(10)*G(40,step)):.2f} of the T1 unlevelled value.")

# ---------------------------------------------------------------- 2. full loadouts, stacked additively
def loadout(law, t, g, step, N, chips='bank', frame=True, par=False):
    """(damage multiplier, hull multiplier) of a ship's GEAR + kit, all slots at tier t and level g.
    W+U lift the whole damage (each its own scope) by B(t)G; Sh (+Fr if a hull frame) lift hull; chips:
      'bank'  -- the bank is BANK1*P per ship, split half damage / half hull, whatever N is;
      'flat'  -- every chip is today's 8% Combat/Armour chip x P, at most 3 Combat (sign-off decision 1 default),
                 the rest Armour."""
    X = P(t, law) * G(g, step) / 100.0
    K = 0.05 * N                                          # kit chips, BAKED into the class's Base (stock unchanged)
    dmg = 1 + B1 * X
    hull = 1 + B1 * X + (B1 * X if frame else 0)
    if chips == 'bank':
        dmg += 0.5 * BANK1 * X; hull += 0.5 * BANK1 * X
    else:
        dmg += 8 * min(3, N) * X; hull += 8 * max(0, N - 3) * X
    return (1 + K) * dmg, (1 + K) * hull

def loadout_table(step):
    print("\n== 2 · FULL LOADOUT (W,U damage; Sh,Fr hull; chips), stacked additively: damage x / hull x / power (dmg*hull)")
    for chips in ('bank', 'flat'):
        print(f"  chips = {chips}" + ("  (bank/N: the same chip power on every class)" if chips == 'bank' else "  (8%/chip, <=3 Combat, rest Armour: decision 1 default)"))
        for law in ('compound', 'linear'):
            for (t, g, tag) in ((1, 0, 'T1 g0'), (5, 16, 'T5 g16'), (10, 0, 'T10 g0'), (10, 26, 'T10 g26'), (10, 40, 'T10 g40')):
                row = []
                for N in (3, 4, 5, 6):
                    d, h = loadout(law, t, g, step, N, chips)
                    row.append(f"N{N} {d:5.2f}/{h:5.2f}/{d*h:6.1f}")
                d3, h3 = loadout(law, t, g, step, 3, chips); d6, h6 = loadout(law, t, g, step, 6, chips)
                print(f"   {law:<8} {tag:<8} " + ' | '.join(row) + f" | light/capital x{d6*h6/(d3*h3):.2f} (stock x{(1.30/1.15)**2:.2f})")
    print("  Ship-level step when EVERY slot moves up one tier (compound vs linear), N=3, g=26:")
    s = []
    for t in range(1, 10):
        a = [loadout(l, t, 26, step, 3) for l in ('compound', 'linear')]; b = [loadout(l, t + 1, 26, step, 3) for l in ('compound', 'linear')]
        s.append(f"T{t}->{t+1} {100*(b[0][0]/a[0][0]-1):4.1f}%/{100*(b[1][0]/a[1][0]-1):4.1f}%")
    print("   damage: " + '  '.join(s))

# ---------------------------------------------------------------- 3. drops: when is tier t worn
def roll_tier(L, rng, band=(0.20, 0.70, 0.10), per=4, cap_level=None):
    b = base_tier(min(L, cap_level) if cap_level else L, per); r = rng.random()
    t = b - 1 if r < band[0] else b if r < band[0] + band[1] else b + 1
    return max(1, min(10, t))

def drop_sim(N=3, runs=600, seed=7, pref=0.5, per=4, band=(0.20, 0.70, 0.10), law='compound', top=40, start=1, stock_at=None):
    """Expected P(worn tier) per slot type after each level at the owner's pace (4 clears a level).
    A crate: 70% own class (+30%/12 of the any-class share); slot drawn by the class's 20-item mix
    (W4 U5 Sh3 Fr2 Eng2 + N chips); `pref` = share of a slot's shapes the pilot will wear."""
    rng = random.Random(seed)
    mix = [('W', 4), ('U', 5), ('Sh', 3), ('Fr', 2), ('Eng', 2), ('C', N)]
    tot = sum(w for _, w in mix)
    acc = {L: {'W': 0.0, 'U': 0.0, 'Sh': 0.0, 'C': 0.0} for L in range(start, top + 1)}
    tiers = {L: [] for L in range(start, top + 1)}
    for _ in range(runs):
        best = {'W': 0, 'U': 0, 'Sh': 0, 'Fr': 0, 'Eng': 0}; chip = [0] * N
        for L in range(start, top + 1):
            for k in range(4):
                for c in range(pm.crates(L)):
                    if rng.random() >= 0.7 + 0.3 / 12: continue
                    t = roll_tier(L, rng, band, per)
                    x = rng.random() * tot; s = None
                    for name, w in mix:
                        if x < w: s = name; break
                        x -= w
                    if rng.random() >= pref: continue
                    if s == 'C':
                        i = min(range(N), key=lambda j: chip[j])
                        if t > chip[i]: chip[i] = t
                    elif t > best[s]: best[s] = t
            for s in ('W', 'U', 'Sh'): acc[L][s] += P(best[s], law) if best[s] else 0.0
            acc[L]['C'] += sum(P(c, law) if c else 0.0 for c in chip) / N
            tiers[L].append(best['Sh'])
    for L in acc:
        for s in acc[L]: acc[L][s] /= runs
    return acc, tiers

def inv_P(p, law='compound'):
    if p <= 0: return 0.0
    return 1 + math.log(p) / math.log(1.2) if law == 'compound' else 1 + (p - 1) / 0.2

def fmtT(p): return f"{inv_P(p):4.1f}" if p >= 1 else ' -- '

def drops_table():
    print("\n== 3 · DROPS: base tier by boss level = 1 + floor((L-1)/4); roll 20% one below / 70% on / 10% one above")
    acc3, t3 = drop_sim(3); acc6, t6 = drop_sim(6)
    print(" L  | base | worn P (W / U / Sh / chip bank), N=3 | effective tier (Sh) | Sh tier p10/p50/p90 | N=6 chip bank P, eff. tier")
    for L in (1, 2, 3, 4, 5, 8, 10, 12, 16, 20, 24, 28, 30, 32, 36, 37, 40):
        a = acc3[L]; srt = sorted(t3[L]); q = lambda f: srt[int(f * (len(srt) - 1))]
        print(f" {L:<3}| T{base_tier(L):<3}| {a['W']:4.2f} / {a['U']:4.2f} / {a['Sh']:4.2f} / {a['C']:4.2f}          | {fmtT(a['Sh'])}               | "
              f"T{q(.1)}/T{q(.5)}/T{q(.9)}        | {acc6[L]['C']:4.2f}, {fmtT(acc6[L]['C'])}")
    # re-gearing from nothing (a class switch) at L37
    rng_runs = 400; need = []
    for seed in range(rng_runs):
        rng = random.Random(1000 + seed); best = {'W': 0, 'U': 0, 'Sh': 0}; clears = 0
        while min(best.values()) < 9 and clears < 60:
            clears += 1
            for c in range(4):
                if rng.random() >= 0.725: continue
                t = roll_tier(37, rng); x = rng.random() * 19; s = 'W' if x < 4 else 'U' if x < 9 else 'Sh' if x < 12 else None
                if s and rng.random() < 0.5 and t > best[s]: best[s] = t
        need.append(clears)
    need.sort()
    print(f"  a class switch at L37: clears until W, U and Sh are all T9+ (preferred shape): median {need[len(need)//2]}, p90 {need[int(.9*len(need))]}")
    r = [climb(seed, 26) for seed in range(200)]; r.sort()
    print(f"  a class switch by a L40 pilot whose slot levels are ACCOUNT-WIDE (g 26 kept): stock can fight L{r[0][1]}; "
          f"clears to fight L37 at TTK <= 75 s and TTD >= 20 s: median {r[len(r)//2][0]}, p90 {r[int(.9*len(r))][0]}")
    print(f"  ...with PER-CLASS slot levels (g 0) it never gets there on drops alone: T10 at g 0 is the 'no salvage' row of section 5,"
          f" and 4 slots x level 26 is {4*round(cv.ladder_cum(26, 500, 1.10)):,} salvage to re-buy")
    return acc3

def climb(seed, g, cls=None, step=None, target=37):
    """A pilot at L40 switches to a class it has no drops for: it fights the highest level it can (TTK <= 90 s,
    TTD >= 15 s), wears the best preferred drop per slot, and stops when it can fight `target` at par-ish."""
    cls = cls or cv.MEDIAN; step = step or STEP_REC; rng = random.Random(5000 + seed)
    B, D, N, fp = cv.CLASSES[cls]; w, h = cv.split_points(cv.PL[40] - 1); K = 0.05 * N
    best = {'W': 0, 'U': 0, 'Sh': 0}; chip = [0] * N; mix = [('W', 4), ('U', 5), ('Sh', 3), ('Fr', 2), ('Eng', 2), ('C', N)]; tot = sum(x for _, x in mix)
    Hp1 = pilot2(cv.MEDIAN, 1, 'compound', step)[0]; PAR = {l: pilot2(cv.MEDIAN, l, 'compound', step) for l in range(1, 41)}
    def me():
        pw = {k: (P(v) if v else 0.0) for k, v in best.items()}; pc = sum(P(c) if c else 0.0 for c in chip) / N; Gg = G(g, step)
        dmg = (1 + K) * (1 + 0.03 * w + B1 / 100 * Gg * (fp * pw['W'] + (1 - fp) * pw['U']) + 0.5 * BANK1 / 100 * Gg * pc)
        hull = 1 + B1 / 100 * Gg * pw['Sh'] + 0.5 * BANK1 / 100 * Gg * pc
        return (B * (1 + K) + 5 * h) * hull, D * dmg
    def fight(L):
        Hx, Dx = me(); HpL, DpL = PAR[L]
        return 60 * DpL / Dx, cv.ttd(Hx, cv.LANCER_SHEET * HpL / Hp1)
    clears = 0; start = None
    while clears < 200:
        k, d = fight(target)
        if k <= 75 and d >= 20: return clears, start
        L = max([l for l in range(1, 41) if fight(l)[0] <= 90 and fight(l)[1] >= 15] or [1])
        if start is None: start = L
        clears += 1
        for c in range(pm.crates(L)):
            if rng.random() >= 0.725: continue
            t = roll_tier(L, rng); x = rng.random() * tot; sl = None
            for name, wt in mix:
                if x < wt: sl = name; break
                x -= wt
            if rng.random() >= 0.5: continue
            if sl == 'C':
                i = min(range(N), key=lambda j: chip[j])
                if t > chip[i]: chip[i] = t
            elif sl in best and t > best[sl]: best[sl] = t
    return clears, start

# ---------------------------------------------------------------- 4. Par v2: the reference counts tiers
_PAR = {}
def par_worn(L, law='compound'):
    """The reference's worn item power per slot, P(worn), from the drop sim (N of the median class)."""
    key = law
    if key not in _PAR: _PAR[key] = drop_sim(cv.CLASSES[cv.MEDIAN][2], law=law, top=80)[0]
    if L < 1: return {'W': 0.0, 'U': 0.0, 'Sh': 0.0, 'C': 0.0}
    return _PAR[key][min(80, L)]

def pilot2(cls, L, law='compound', step=STEP_REC, g=None, t=None, frame=False, chips=True, geared=True, share=0.65):
    """(hull, realistic DPS) of `cls` at level L wearing W,U damage, Sh hull, [Fr hull], chip bank (half/half).
    t: force every slot to tier t (else the reference's worn power from the drop sim); g: slot level."""
    B, D, N, fp = cv.CLASSES[cls]
    if g is None: g = share * min(MAXLEVEL, L)
    w, h = cv.split_points(cv.PL[min(max(1, L), max(cv.PL))] - 1)
    K = 0.05 * N
    if not geared: pw = {'W': 0, 'U': 0, 'Sh': 0, 'C': 0}
    elif t is not None: pw = {s: P(t, law) for s in ('W', 'U', 'Sh', 'C')}
    else: pw = par_worn(L - 1, law)          # the boss of level L is fought with what level L-1 left you
    Gg = G(g, step)
    # W lifts the primary, U the abilities, each by its budget: together the whole damage.
    # the kit chips are BAKED into Base (x(1+K)); every share after that adds: (Base+Flat) x (1 + sum)
    dmg = (1 + K) * (1 + 0.03 * w + (B1 / 100) * Gg * (fp * pw['W'] + (1 - fp) * pw['U']))
    hull = 1 + (B1 / 100) * Gg * pw['Sh'] + ((B1 / 100) * Gg * pw['Sh'] if frame else 0)
    if chips:
        dmg += (1 + K) * 0.5 * (BANK1 / 100) * Gg * pw['C']; hull += 0.5 * (BANK1 / 100) * Gg * pw['C']
    return (B * (1 + K) + 5 * h) * hull, D * dmg

def par_scales2(L, law='compound', step=STEP_REC):
    H1, D1 = pilot2(cv.MEDIAN, 1, law, step); H, Dd = pilot2(cv.MEDIAN, L, law, step)
    return Dd / D1, H / H1

def par_table(step):
    print(f"\n== 4 · PAR v2 (counts tiers; step {step}): HullScale / DamageScale, against curve.md's Par")
    print(" L  | v2 compound H / D | v2 linear H / D | curve capped H / D | curve today H / D | 1.025^(L-1)")
    for L in (1, 2, 3, 5, 10, 15, 20, 25, 30, 35, 40, 50, 60, 80):
        c = par_scales2(L, 'compound', step); l = par_scales2(L, 'linear', step)
        cc = cv.par_scales(L, 'capped'); ct = cv.par_scales(L, 'today')
        print(f" {L:<3}| {c[0]:6.2f} / {c[1]:5.2f}    | {l[0]:5.2f} / {l[1]:5.2f}   | {cc[0]:5.2f} / {cc[1]:5.2f}      | {ct[0]:5.2f} / {ct[1]:5.2f}     | {1.025**(L-1):5.2f}")
    segs = [(1, 5), (5, 10), (10, 20), (20, 30), (30, 40), (40, 80)]
    print("  local growth per level (compound): " + '  '.join(
        f"L{a}-{b} {((par_scales2(b,'compound',step)[0]/par_scales2(a,'compound',step)[0])**(1/(b-a))-1)*100:4.1f}%/{((par_scales2(b,'compound',step)[1]/par_scales2(a,'compound',step)[1])**(1/(b-a))-1)*100:4.1f}%" for a, b in segs))
    A = 60 * pilot2(cv.MEDIAN, 1, 'compound', step)[1]
    print(f"  Lancer L1 anchor = 60 s x Par DPS(1) = {A:.0f} (curve.md: 3820); boss hull at L40 = {A*par_scales2(40,'compound',step)[0]:.0f}")

# ---------------------------------------------------------------- 5. TTK / TTD at L40
def ttk_ttd(x, L, law, step, par_law=None, par_step=None, par='v2'):
    """x = (hull, DPS) of a pilot; against the boss of level L scaled by Par ('v2' counts tiers, 'capped' = curve.md)."""
    if par == 'v2':
        Dp1 = pilot2(cv.MEDIAN, 1, par_law or law, par_step or step)[1]; Hp1 = pilot2(cv.MEDIAN, 1, par_law or law, par_step or step)[0]
        HpL, DpL = pilot2(cv.MEDIAN, L, par_law or law, par_step or step)
        hs, ds = DpL / Dp1, HpL / Hp1; A = 60 * Dp1
    else:
        hs, ds = cv.par_scales(L, par); A = cv.anchor(par)
    H, Dx = x
    return A * hs / Dx, cv.ttd(H, cv.LANCER_SHEET * ds)

def ttk_table(step):
    print(f"\n== 5 · TTK s / TTD s (Lancer sheet) at L40, median class unless named; step {step}")
    M = cv.MEDIAN; L = 40
    rows = [
        ('Par v2 (reference: W,U,Sh, chip bank at worn tier, 65% of gate)', pilot2(M, L)),
        ('Par v2 + a hull frame', pilot2(M, L, frame=True)),
        ('T10 full (W,U,Sh,Fr hull, bank), g 26', pilot2(M, L, t=10, frame=True)),
        ('T10 full, g 40 (the ceiling)', pilot2(M, L, t=10, g=40, frame=True)),
        ('T10 reference slots only, g 40', pilot2(M, L, t=10, g=40)),
        ('T8 reference slots (two tiers behind), g 26', pilot2(M, L, t=8)),
        ('Par, no salvage (g 0)', pilot2(M, L, g=0)),
        ('stock (kit only)', pilot2(M, L, geared=False)),
    ]
    for law in ('compound', 'linear'):
        print(f"  law {law}:")
        for tag, _ in rows:
            pass
        for tag, x in rows:
            # recompute under this law
            kw = {}
            if 'T10 full, g 40' in tag: x = pilot2(M, L, law, step, t=10, g=40, frame=True)
            elif 'T10 full' in tag: x = pilot2(M, L, law, step, t=10, frame=True)
            elif 'T10 reference' in tag: x = pilot2(M, L, law, step, t=10, g=40)
            elif 'T8' in tag: x = pilot2(M, L, law, step, t=8)
            elif 'no salvage' in tag: x = pilot2(M, L, law, step, g=0)
            elif 'stock' in tag: x = pilot2(M, L, law, step, geared=False)
            elif 'hull frame' in tag: x = pilot2(M, L, law, step, frame=True)
            else: x = pilot2(M, L, law, step)
            k2, d2 = ttk_ttd(x, L, law, step, par='v2'); k1, d1 = ttk_ttd(x, L, law, step, par='capped')
            print(f"    {tag:<62} hull {x[0]:6.0f} DPS {x[1]:6.1f} | vs Par v2: {k2:5.1f} / {d2:5.1f} | vs curve.md Par (no tiers): {k1:5.1f} / {d1:6.1f}")
    print("  every class, Par v2 compound, TTK/TTD at L1 / L20 / L40 (constant => the curve still holds per class):")
    for c in cv.CLASSES:
        r = []
        for L in (1, 20, 40):
            k, d = ttk_ttd(pilot2(c, L), L, 'compound', step); r.append(f"{k:4.0f}/{d:3.0f}")
        print(f"    {c:<11} " + '  '.join(r))

# ---------------------------------------------------------------- 6. salvage x tiers
def salvage_table(step):
    print(f"\n== 6 · SALVAGE x TIERS (step {step})")
    print("  (a) Is a drop one tier up an upgrade over the worn item at the reference's level g = 0.65 L?")
    print("   L  | worn T, g | new T+1 | part-keyed (new g=0) | line-keyed: same line p / else | slot-keyed")
    for L in (8, 12, 20, 28, 36):
        t = base_tier(L) - 1 if L > 4 else 1; g = 0.65 * L
        old = P(t) * G(g, step); new0 = P(t + 1) * G(0, step); newg = P(t + 1) * G(g, step)
        print(f"   {L:<3}| T{t}, {g:4.1f}  | T{t+1}     | x{new0/old:4.2f} ({'upgrade' if new0>old else 'DOWNGRADE'})  "
              f"      | 1/3 x{newg/old:4.2f}, 2/3 x{new0/old:4.2f}          | x{newg/old:4.2f}")
    print("  (b) shares of the reference's L40 hull (median class): base+kit / points / drop (tier) / salvage (levels)")
    B, D, N, fp = cv.CLASSES[cv.MEDIAN]
    w, h = cv.split_points(cv.PL[40] - 1); pw = par_worn(39)          # as pilot2: the L40 boss is fought with L39's gear
    for st in (0.05, 0.03, 0.025):
        g = 0.65 * 40; X = (B1 * pw['Sh'] + 0.5 * BANK1 * pw['C']) / 100
        base = B * (1 + 0.05 * N); pts = 5 * h; drop = (base + pts) * X; lev = (base + pts) * X * st * g
        tot = base + pts + drop + lev
        print(f"   step {st}: {100*base/tot:4.0f}% / {100*pts/tot:3.0f}% / {100*drop/tot:4.0f}% / {100*lev/tot:4.0f}%   (hull {tot:.0f});"
              f"  no-salvage TTD vs Par: x{(base+pts+drop)/tot:.2f}")
    print("  (c) affordability on the curve's ladder (500 x 1.10^n, gated), rewards only, scrap priced by tier:")
    inc = pm.run('bounty', hub=0)
    for sv in ('rarity (today)', 'tier 100x1.28^(t-1)'):
        row = []
        for slots in (3, 4, 6):
            cells = []
            for L in (10, 20, 30, 40):
                fleet = inc[L]['cum'] - inc[L]['cs']
                scrap = inc[L]['cs'] if sv.startswith('rarity') else sum(
                    4 * pm.crates(l) * sum(p * 100 * 1.28 ** (tt - 1) for p, tt in
                        ((0.2, max(1, base_tier(l) - 1)), (0.7, base_tier(l)), (0.1, min(10, base_tier(l) + 1)))) for l in range(1, L + 1))
                a = cv.affordable(fleet + scrap, slots, 500, 1.10, L)
                cells.append(f"{100*a/min(40,L):3.0f}%")
            row.append(f"{slots} slots " + '/'.join(cells))
        print(f"   {sv:<22} % of gate at L10/20/30/40: " + '  |  '.join(row))

# ---------------------------------------------------------------- 7. flats step at tiers
def flat_nF(n1, f, base=None):
    if n1 <= 0: return n1
    n = max(n1, int(math.floor(n1 * math.sqrt(f) + 0.5)))
    if base: n = min(n, int(math.floor(CAP1 / 100 * f * base + 1e-9)))
    return n if n >= n1 else 0

def flat_n(n1, t, law='compound', base=None):
    """+n at tier t: n1 grown by the bound law, rounded, and never more than the POWER cap allows on a count of
    `base` (n/base <= 25% P(t)). 0 = the shape is not offered at this tier. A -n (n1 < 0) never grows."""
    if n1 <= 0: return n1
    n = max(n1, int(math.floor(n1 * Pb(t, law) + 0.5)))
    if base: n = min(n, int(math.floor(CAP1 / 100 * P(t, law) * base + 1e-9)))
    return n if n >= n1 else 0

def flats_table():
    print("\n== 7 · FLATS: +n(t) = round(n1 * sqrt(P(t))), never below n1; a -1 never grows; the % part absorbs the rounding")
    print("  t:        " + ' '.join(f"T{t:<3}" for t in TIERS))
    print("  +n (n1=1) " + ' '.join(f"+{flat_n(1,t):<3}" for t in TIERS))
    print("  +n (n1=2) " + ' '.join(f"+{flat_n(2,t):<3}" for t in TIERS))
    print("  n1 = 1 on a count of `base` (n/base <= 25% x P(t); '--' = not offered at that tier):")
    for base in (2, 3, 4, 6, 8, 12):
        print(f"   base {base:>2}: " + ' '.join((f"+{flat_n(1, t, base=base)}  " if flat_n(1, t, base=base) else '--  ') + ' ' for t in TIERS))
    print("  example: 'Swarm cells' (+1 hunter of 4, the rest hunter damage), ability share 0.6, budget 20 P(t):")
    for t in (1, 3, 5, 6, 8, 10):
        n = flat_n(1, t, base=4); cnt = n / 4
        # value = 0.6 * ((1+cnt)(1+x) - 1) = B -> x
        x = ((B1 * P(t) / 100) / 0.6 + 1) / (1 + cnt) - 1
        print(f"   T{t:<2} +{n} hunter (+{100*cnt:.0f}%), hunter damage +{100*x:5.1f}%  -> value {0.6*((1+cnt)*(1+x)-1)*100:5.1f} = budget {B1*P(t):5.1f}")

# ---------------------------------------------------------------- 8. the budget: 20 shapes, equal power
# stat: (system, weight in the SLOT's reference units, mult?, bound?)
#   mult: multiplies its system's output (damage, rate, count, cooldown rate, uptime, hull) -> combined as a PRODUCT
#   lin : range, turn, speed, radius, guidance -> linear
#   bound: grows by sqrt(P) and has a loadout ceiling; power: grows by P, no ceiling (the boss curve absorbs it)
# DESTROYER placeholders from the v2 card (Director 45 DPS, Lance 16.7, Suppress, Grapnel). Ability shares:
# Lance 0.55, Suppress 0.30, Grapnel 0.15 of the ability scope.
STATS = {
    'W':   {'main_damage': ('pri', 1.0, 1, 0), 'main_rate': ('pri', 1.0, 1, 1), 'main_range': ('pri_g', 0.4, 0, 1),
            'main_turn': ('pri_g', 0.25, 0, 1), 'shell_speed': ('pri_g', 0.25, 0, 1)},
    'U':   {'lance_damage': ('lance', 0.55, 1, 0), 'lance_cooldown': ('lance', 0.55, 1, 1), 'lance_speed': ('lance_g', 0.15, 0, 1),
            'lance_range': ('lance_g', 0.10, 0, 1), 'suppress_time': ('supp', 0.30, 1, 1), 'suppress_cooldown': ('supp', 0.30, 1, 1),
            'suppress_mult': ('supp', 0.30, 1, 0), 'grapnel_cooldown': ('grap', 0.15, 1, 1), 'hurl_damage': ('grap', 0.15, 1, 0),
            'grapnel_range': ('grap_g', 0.08, 0, 1)},
    'Sh':  {'hull': ('hull', 1.0, 1, 0), 'pd_range': ('pd', 0.10, 0, 1), 'max_speed': ('helm', 0.4, 0, 1), 'thrust': ('helm', 0.2, 0, 1),
            'turn_rate': ('helm', 0.3, 0, 1), 'pd_damage': ('pd', 0.15, 1, 0)},
    'Eng': {'max_speed': ('helm', 1.0, 0, 1), 'thrust': ('helm', 0.6, 0, 1), 'turn_rate': ('helm', 0.8, 0, 1), 'turn_radius': ('helm', 0.6, 0, 1),
            'reverse_speed': ('helm', 0.3, 0, 1), 'hull': ('hull', 1.0, 1, 0)},
    'C':   {'all_damage': ('dmg', 1.0, 1, 0), 'hull': ('hull', 1.0, 1, 0), 'all_reach': ('reach', 0.4, 0, 1), 'cooldown_rate': ('cd', 0.5, 1, 1),
            'max_speed': ('helm', 0.4, 0, 1)},
}
STATS['Fr'] = STATS['Sh']
# shape rows: (slot, name, power ups {stat: ratio}, bound ups {stat: T1 %}, downs {stat: %}, flats {stat: (n1, base)})
SHAPES = [
    ('W', 'Heavy shells',     {'main_damage': 1}, {}, {'main_turn': -30}, {}),
    ('W', 'Quick feed',       {'main_damage': 1}, {'main_rate': 15}, {'main_range': -25}, {}),
    ('W', 'Long director',    {'main_damage': 1}, {'main_range': 20}, {'main_turn': -30}, {}),
    ('W', 'Tracking director',{'main_damage': 1}, {'main_turn': 25, 'shell_speed': 20}, {}, {}),
    ('U', 'Heavy warhead',    {'lance_damage': 1}, {'lance_cooldown': 15}, {'lance_speed': -30}, {}),
    ('U', 'Rapid reload',     {'lance_damage': 1}, {'lance_cooldown': 20}, {'lance_range': -20}, {}),
    ('U', 'Long-run tubes',   {'lance_damage': 1}, {'lance_speed': 25, 'lance_range': 20, 'lance_cooldown': 8}, {}, {}),
    ('U', 'Deck winch',       {'lance_damage': 1, 'hurl_damage': 1}, {'grapnel_cooldown': 25}, {}, {}),
    ('U', 'Wide suppression', {'suppress_mult': 1, 'lance_damage': 1}, {'suppress_time': 25}, {'grapnel_range': -30}, {}),
    ('U', 'Hot tubes',        {'lance_damage': 2, 'suppress_mult': 1}, {'suppress_cooldown': 20}, {'grapnel_range': -30}, {}),
    ('Sh', 'Bulwark',         {'hull': 1}, {}, {'max_speed': -15}, {}),
    ('Sh', 'Balanced',        {'hull': 1}, {'pd_range': 20}, {}, {}),
    ('Sh', 'Screen',          {'hull': 1}, {}, {'thrust': -10}, {}),
    ('Fr', 'Braced',          {'hull': 1}, {}, {'turn_rate': -20, 'max_speed': -10}, {}),
    ('Fr', 'Flak',            {'pd_damage': 1, 'hull': 1}, {'pd_range': 10}, {}, {}),
    ('Eng', 'Racing',         {}, {'max_speed': 12, 'thrust': 15}, {'turn_rate': -25}, {}),
    ('Eng', 'Helm',           {}, {'turn_rate': 15, 'turn_radius': 10}, {'max_speed': -10}, {}),
    ('C', 'Combat',           {'all_damage': 1}, {}, {'hull': -5}, {}),
    ('C', 'Armour',           {'hull': 1}, {}, {'all_damage': -5}, {}),
    ('C', 'Targeting',        {'all_damage': 1}, {'all_reach': 8}, {}, {}),
]

def value(slot, ups, downs, flats):
    """ups/downs: {stat: fraction}; flats: {stat: (n, base)}. Weighted, products within a mult system."""
    S = STATS[slot]; sysprod = {}; lin = 0.0; upsys = set()
    for s, x in ups.items():
        sy, w, m, _ = S[s]
        if m: sysprod.setdefault(sy, [w, 1.0]); sysprod[sy][1] *= (1 + x); upsys.add(sy)
        else: lin += w * x
    for s, (n, base) in flats.items():
        sy, w, m, _ = S[s]; sysprod.setdefault(sy, [w, 1.0]); sysprod[sy][1] *= (1 + n / base); upsys.add(sy)
    pen = 0.0
    for s, d in downs.items():
        sy, w, m, _ = S[s]
        if m and sy in upsys: sysprod[sy][1] *= max(0.1, 1 + d)     # a down on what the item lifts is paid in full
        else: pen += LAMBDA * w * abs(d)                             # a price paid elsewhere: half
    return sum(w * (p - 1) for w, p in sysprod.values()) + lin - pen

def F(t, g=0, law='compound', step=STEP_REC):
    """THE ONE POWER FACTOR: tier and slot level multiply into one number, and the item is solved at it."""
    return P(t, law) * G(g, step)

def slot_budget(slot, f, N=3):
    """What one item of this slot is worth at power factor f, in the slot's reference units (fraction)."""
    if slot == 'C': return BANK1 / N * f / 100                  # the chip bank, split over the class's N chips
    if slot == 'Eng': return B1 * math.sqrt(f) / 100            # an all-bound slot grows by the bound law
    return B1 * f / 100

def build(shape, t, law='compound', budget=None, N=3, g=0, step=STEP_REC):
    slot, name, pw, bd, dn, fl = shape
    f = F(t, g, law, step)
    ups = {s: v / 100 * math.sqrt(f) for s, v in bd.items()}
    downs = {s: v / 100 for s, v in dn.items()}
    flats = {s: (flat_nF(n1, f, base), base) for s, (n1, base) in fl.items()}
    if budget is None: budget = slot_budget(slot, f, N)
    if pw:
        lo, hi = 0.0, 20.0
        for _ in range(80):
            k = (lo + hi) / 2; u = dict(ups)
            for s, r in pw.items(): u[s] = u.get(s, 0) + k * r / max(pw.values())
            if value(slot, u, downs, flats) < budget: lo = k
            else: hi = k
        for s, r in pw.items(): ups[s] = ups.get(s, 0) + lo * r / max(pw.values())
    else:  # an all-bound shape: its row gives the RATIOS; solved at every tier to the slot's (bound-law) budget
        base_ups = {s: v / 100 for s, v in bd.items()}; lo, hi = 0.0, 20.0
        for _ in range(80):
            k = (lo + hi) / 2
            if value(slot, {s: v * k for s, v in base_ups.items()}, downs, flats) < budget: lo = k
            else: hi = k
        ups = {s: v * lo for s, v in base_ups.items()}
    return ups, downs, flats, value(slot, ups, downs, flats)

def caps_ok(shape, ups, flats, t, law='compound'):
    slot = shape[0]; bad = []
    for s, x in ups.items():
        bound = STATS[slot][s][3]
        cap = CAP1 / 100 * (Pb(t, law) if bound else P(t, law))
        if x > cap + 1e-9: bad.append(f"{s} {100*x:.0f}%>{100*cap:.0f}%")
    return bad

def budget_table():
    print("\n== 8 · THE BUDGET: 20 DESTROYER shapes, generated at T1 / T5 / T10 from their rows (core 20 P(t), engines 20 sqrt P(t), chip 30 P(t)/N); all 20 inside the caps")
    for t in (1, 5, 10):
        print(f"  T{t}: budget core {B1*P(t):.1f}, engines {B1*Pb(t):.1f}, chip {BANK1*P(t)/3:.1f}; up caps: power {CAP1*P(t):.0f}%, bound {CAP1*Pb(t):.0f}%; downs fixed")
        vals = []
        for sh in SHAPES:
            ups, downs, flats, v = build(sh, t); vals.append(v * 100)
            bad = caps_ok(sh, ups, flats, t)
            up = ', '.join(f"{s} +{100*x:.0f}%" for s, x in ups.items())
            dn = ', '.join(f"{s} {100*x:.0f}%" for s, x in downs.items())
            print(f"   {sh[0]:<3} {sh[1]:<18} {up:<58} | {dn:<32} | value {100*v:5.1f}" + (f"  OVER CAP: {'; '.join(bad)}" if bad else ''))
        for sl in ('W', 'U', 'Sh', 'Fr', 'Eng', 'C'):
            vv = [v for sh, v in zip(SHAPES, vals) if sh[0] == sl]
            assert max(vv) - min(vv) < 0.01, (sl, vv)
        print(f"   every slot's shapes are equal to 0.01 at T{t}")
    print("  levelled: the item is RE-SOLVED at F = P(t)(1 + 0.03 g), so the 20 stay equal at every slot level:")
    for t, g in ((1, 26), (5, 40), (10, 26), (10, 40)):
        spread = []
        for sl in ('W', 'U', 'Sh', 'Fr', 'Eng', 'C'):
            vv = [build(sh, t, g=g)[3] for sh in SHAPES if sh[0] == sl]; spread.append(max(vv) - min(vv))
        hs = build(SHAPES[0], t, g=g)[0]
        print(f"   T{t} g{g}: F {F(t, g):5.2f}, max spread within a slot {100*max(spread):.3f} points; Heavy shells main_damage +{100*hs['main_damage']:.0f}%")
    print("  ...whereas lifting the printed ups by (1 + 0.03 g) after the fact (today's Lifted) spreads them: W 26% at T1 g26, 11% at T10 g40")
    print("  RULE CHECKS: (1) why value is a PRODUCT within a system, at T10:")
    x = B1 * P(10) / 100; print(f"   linear-budgeted split +{50*x:.0f}% dmg & +{50*x:.0f}% rate = x{(1+x/2)**2:.2f} DPS vs single x{1+x:.2f};"
                                  f" product-budgeted split = +{100*(math.sqrt(1+x)-1):.0f}% each = x{1+x:.2f}")
    print("  (2) a down on a stat the item LIFTS is paid in full, so under a cap of 1.25 x budget it never fits:")
    for d in (0.1, 0.2, 0.3):
        for t in (1, 10):
            Bt = B1 * P(t) / 100; x = (1 + Bt) / (1 - d) - 1
            print(f"     T{t:<2} -{100*d:.0f}% rate on the gun it lifts: damage must be +{100*x:.0f}% (cap {CAP1*P(t):.0f}%) -> {'fits' if x <= CAP1*P(t)/100 else 'NEVER'}")
    print("  (3) a system of weight w reaches at most w((1+cap_power)(1+cap_bound)-1) with one power and one bound up:")
    for w in (1.0, 0.55, 0.3, 0.15):
        for t in (1, 10):
            m = w * ((1 + CAP1 * P(t) / 100) * (1 + CAP1 * Pb(t) / 100) - 1)
            print(f"     w {w:<4} T{t:<2}: {100*m:5.1f} of a {B1*P(t):5.1f} budget -> {'one system is enough' if m >= B1*P(t)/100 else 'needs a second system'}")

# ---------------------------------------------------------------- 9. bound stats: ceilings
CEIL = {'top speed / thrust': 75, 'turn / radius': 100, 'range / reach': 60, 'area radius': 60, 'projectile speed / guidance': 100,
        'rate of fire (spawns shots)': 150, 'ability cooldown rate': 100, 'buff duration': 100, 'CC duration': 60, 'counts (+n)': 3}
def bound_table(step):
    m = math.sqrt(P(10) * G(40, step))
    print(f"\n== 9 · BOUND STATS: sqrt law, ceiling on the LOADOUT's summed bonus. One T10 g40 item = x{m:.2f} its T1 value, so its T1 cap = ceiling/{m:.2f}")
    for k, c in CEIL.items():
        if 'counts' in k: print(f"   {k:<30} ceiling +{c} and <= the base count"); continue
        print(f"   {k:<30} ceiling +{c:>3}%  -> T1 cap for one item {c/m:4.1f}%  (vs the owner's 25%)")
    print("   uptime rule (generator assert): stock uptime x (1 + dur ceiling) x (1 + cd ceiling) <= 0.8 for every timed buff, else lower that ability's ceilings.")

def main():
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    step = float(sys.argv[2]) if len(sys.argv) > 2 else STEP_REC
    parts = {'law': lambda: law_table(step), 'loadout': lambda: loadout_table(step), 'drops': drops_table,
             'par': lambda: par_table(step), 'ttk': lambda: ttk_table(step), 'salvage': lambda: salvage_table(step),
             'flats': flats_table, 'budget': budget_table, 'bound': lambda: bound_table(step)}
    for k, f in parts.items():
        if what in ('all', k): f()

if __name__ == '__main__':
    main()
