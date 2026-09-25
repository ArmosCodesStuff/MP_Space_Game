# THE CURVE: boss scaling read off a reference ("par") pilot. Read-only model; see curve.md.
# Repo constants re-read from source (file in the comment). v2 kit numbers are PLACEHOLDERS.
# usage: python curve.py [today|capped] [LevelCost] [LevelGrowth]
import math, sys
import player_model as pm          # income only (recycler + fleet); its old-ladder spending is ignored

# ---------------------------------------------------------------- repo constants (verified in source)
KILL, FIRST, DONE, EXPLV = 300, 250, 100, 1000        # Missions.cs KillExp/FirstClearExp/CompletionExp; Progression ExpPerLevel
REGEN = 0.005                                          # PlayerShip.RegenInCombat
# boss DPS per unit of scale against ONE pilot (boss_model.md 1.3, from Lancer.cs / Drake.cs rows):
LANCER_SHEET, DRAKE_SHEET = 17.05, 13.23               # everything lands ("stops dodging")
LANCER_REAL, DRAKE_REAL = 6.70, 3.91                   # stated land rates, long band
MAXLEVEL, STEP = 40, 0.05                              # Equipment.MaxLevel, Equipment.LevelStep
LANCER_OLD, DRAKE_OLD = 760, 700                       # Missions.Bosses[].Hull

# ---------------------------------------------------------------- class rows (PLACEHOLDER: v2 drafts / signoff)
# name: (base hull B, realistic bare DPS D vs a boss, chips N, primary share f_p)
CLASSES = {
    'BATTLESHIP': (500, 46.0, 3, 0.48), 'CARRIER': (425, 50.0, 3, 0.45), 'DESTROYER': (395, 54.0, 3, 0.73),
    'FREIGHTER': (450, 53.5, 4, 0.45), 'TENDER': (380, 46.0, 4, 0.83), 'BASTION': (420, 47.0, 4, 0.45),
    'SNIPER': (240, 43.0, 5, 0.55), 'WARRIOR': (300, 50.0, 5, 0.80), 'WARDEN': (270, 49.0, 5, 0.64),
    'DART': (200, 61.0, 6, 0.64), 'ECHO': (180, 72.0, 6, 0.85), 'WRAITH': (220, 64.0, 6, 0.85),
}
MEDIAN = 'DESTROYER'   # nearest the fleet median on BOTH axes after kit chips (DPS 62.1 vs 61.7; hull 454 vs 415)

# ---------------------------------------------------------------- the PAR row (every assumption is data)
PAR = dict(kills=4, share=0.65, rarity=((5, 1.0), (25, 2.0)), parts_by=5, fight=60.0,
           hull_parts=1,          # the Shield slot's hull part; the frame is left to the pilot
           dmg_parts='all')       # 'all': a Weapon part lifts the primary AND a Utility part the abilities
ITEMS = {
    'today':  dict(sh=0.40, fr=0.30, rate=0.60, down=0.30),   # Bulwark, Braced, Rapid pattern (Equipment.cs)
    'capped': dict(sh=0.25, fr=0.25, rate=0.25, down=0.00),   # the item pass's +25% common cap
}

def ladder_cost(n, lc, lg): return round(lc * lg ** n)
def ladder_cum(g, lc, lg):
    return sum(ladder_cost(k, lc, lg) for k in range(int(g))) + (g - int(g)) * ladder_cost(int(g), lc, lg)
def cap(L, gated=True): return min(MAXLEVEL, L) if gated else MAXLEVEL
def affordable(salvage, parts, lc, lg, L, gated=True):
    g = 0.0; c = cap(L, gated)
    while g < c and parts * ladder_cum(min(c, g + 0.05), lc, lg) <= salvage: g = min(c, g + 0.05)
    return g

def pl_table(kills=4, xmul=1, top=120):
    pl, exp, out = 1, 0, {}
    for L in range(1, top + 1):
        for k in range(kills):
            ok = L >= pl * 0.5
            ke = round(KILL * L / pl) if ok else 0
            exp += round(xmul * (ke + (FIRST if k == 0 else 0) + DONE)) if ok else 0
            while exp >= EXPLV: exp -= EXPLV; pl += 1
        out[L] = pl
    return out
PL = pl_table()

def split_points(P):
    """Weapons and Hull, the cheaper next point first (Weapons on a tie). Cooling/Gunnery left out (sign bug)."""
    w = h = 0
    while True:
        cw, ch = w + 1, h + 1
        if cw <= ch and cw <= P: P -= cw; w += 1
        elif ch <= P: P -= ch; h += 1
        else: return w, h

def rho(L):
    (l0, r0), (l1, r1) = PAR['rarity']
    return r0 if L <= l0 else r1 if L >= l1 else r0 + (r1 - r0) * (L - l0) / (l1 - l0)
def parts(L): return min(1.0, max(0.0, (L - 1) / (PAR['parts_by'] - 1)))

def pilot(cls, L, g=None, items='today', hullparts=None, dmg=None, rarity=None, geared=True):
    """(hull, realistic DPS vs a boss) of a class's pilot after the pace's clears at level L."""
    B, D, N, fp = CLASSES[cls]; it = ITEMS[items]
    hullparts = PAR['hull_parts'] if hullparts is None else hullparts
    dmg = PAR['dmg_parts'] if dmg is None else dmg
    if g is None: g = PAR['share'] * cap(L)
    r = rho(L) if rarity is None else rarity
    p = parts(L) if geared else 0.0; G = 1 + STEP * g
    w, h = split_points(PL[min(max(1, L), max(PL))] - 1)
    H = (B + 5 * h) * (1 + 0.05 * N + p * sum([it['sh'], it['fr']][:hullparts]) * r * G)
    dsh = 0.05 * N + 0.03 * w
    lifted = (1 + dsh - p * it['down']) * (1 + p * it['rate'] * r * G)
    plain = 1 + dsh
    Dp = D * (lifted if dmg == 'all' else fp * lifted + (1 - fp) * plain if dmg == 'primary' else plain)
    return H, Dp

def par_scales(L, items='today'):
    H1, D1 = pilot(MEDIAN, 1, items=items); H, Dd = pilot(MEDIAN, L, items=items)
    return Dd / D1, H / H1          # (HullScale = pilot DPS growth, DamageScale = pilot hull growth)

def anchor(items='today'): return PAR['fight'] * pilot(MEDIAN, 1, items=items)[1]
def ttd(H, sheet): return H / max(1e-9, sheet - REGEN * H)

def main():
    items = sys.argv[1] if len(sys.argv) > 1 else 'today'
    LC = float(sys.argv[2]) if len(sys.argv) > 2 else 500; LG = float(sys.argv[3]) if len(sys.argv) > 3 else 1.10
    A = anchor(items)
    print(f"== items {items}; ladder {LC:g} x {LG:g}^n, gated at min(40, L); Lancer L1 {A:.0f}, Drake {A*DRAKE_OLD/LANCER_OLD:.0f}")
    print("PL:", {L: PL[L] for L in (1, 5, 10, 20, 30, 40, 60, 80)}, " split at L40:", split_points(PL[40] - 1))
    print("\nL  | rho  g_ref | HullScale DamageScale | 1.025^(L-1) | local step H / D")
    prev = None
    for L in (1, 2, 3, 4, 5, 10, 15, 20, 25, 30, 35, 40, 50, 60, 80):
        sh, sd = par_scales(L, items)
        s = f"{L:<3}| {rho(L):.2f} {PAR['share']*cap(L):5.2f} | {sh:6.3f}   {sd:6.3f}     | {1.025**(L-1):6.3f}"
        if prev: s += f" | {(sh/prev[1])**(1/(L-prev[0])):.4f} / {(sd/prev[2])**(1/(L-prev[0])):.4f}"
        print(s); prev = (L, sh, sd)
    print("full table (L: HullScale, DamageScale):", ' '.join(f"{L}:{par_scales(L, items)[0]:.3f},{par_scales(L, items)[1]:.3f}" for L in range(1, 41)))
    inc0, inc60 = pm.run('bounty', hub=0), pm.run('bounty', hub=60)
    print("\nladder: level n costs", [ladder_cost(n, LC, LG) for n in (0, 1, 2, 10, 20, 25, 30, 39)],
          "| to 26:", round(ladder_cum(26, LC, LG)), "| whole:", sum(ladder_cost(k, LC, LG) for k in range(40)))
    print("\nL  | salvage rewards-only / +60 s hub | g affordable (3 ids, gated) | % of cap | g_ref")
    for L in (1, 5, 10, 15, 20, 25, 30, 35, 40):
        s0, s6 = inc0[L]['cum'], inc60[L]['cum']
        a0, a6 = affordable(s0, 3, LC, LG, L), affordable(s6, 3, LC, LG, L)
        print(f"{L:<3}| {s0:8.0f} / {s6:8.0f} | {a0:5.1f} / {a6:5.1f} | {100*a0/cap(L):4.0f}% / {100*a6/cap(L):4.0f}% | {0.65*cap(L):.1f}")
    print("\nTHE TABLE: hull, DPS | boss hull, boss DPS sheet/realistic | TTK | TTD sheet (Lancer; Drake) | realistic share of hull lost in the fight")
    for cls in (MEDIAN, 'ECHO', 'SNIPER'):
        print(" ", cls, CLASSES[cls])
        for L in (1, 5, 10, 20, 30, 40):
            sh, sd = par_scales(L, items); H, Dp = pilot(cls, L, items=items)
            bh = A * sh; sheet = LANCER_SHEET * sd; real = LANCER_REAL * sd
            print(f"   L{L:<3} {H:6.0f} {Dp:6.1f} | {bh:6.0f} {sheet:5.1f}/{real:4.1f} | {bh/Dp:5.1f} | {ttd(H, sheet):5.1f}; {ttd(H, DRAKE_SHEET*sd):5.1f}"
                  f" | {100*max(0,(real-REGEN*H))*bh/Dp/H:3.0f}%  (g aff. {affordable(inc0[L]['cum'],3,LC,LG,L):.1f} rewards / {affordable(inc60[L]['cum'],3,LC,LG,L):.1f} hub)")
    for dm in ('all', 'primary'):
        print(f"\nEVERY CLASS, damage parts lift '{dm}': TTK s / TTD s (Lancer sheet) at L1, L20, L40")
        for cls in CLASSES:
            row = []
            for L in (1, 20, 40):
                sh, sd = par_scales(L, items); H, Dp = pilot(cls, L, items=items, dmg=dm)
                row.append(f"L{L}: {A*sh/Dp:4.0f} / {ttd(H, LANCER_SHEET*sd):4.0f}")
            print(f"  {cls:<11}", '  '.join(row))
    print("\nBELOW / ABOVE the reference, median class: TTK s / TTD s (Lancer sheet)")
    for L in (5, 10, 20, 40):
        sh, sd = par_scales(L, items); bh = A * sh; sheet = LANCER_SHEET * sd
        def show(tag, **kw):
            H, Dp = pilot(MEDIAN, L, items=items, **kw); return f"{tag} {bh/Dp:3.0f}/{ttd(H, sheet):3.0f}"
        g6 = affordable(inc60[L]['cum'], 3, LC, LG, L); g0 = affordable(inc0[L]['cum'], 3, LC, LG, L)
        print(f"  L{L:<2}", ' | '.join([show('ref'), show('no salvage', g=0), show('no hull part', hullparts=0),
              show('2 hull parts', hullparts=2), show('rewards-only', g=g0), show('+60s hub', g=g6), show('100% cap', g=cap(L)),
              show('100% + 2 hull', g=cap(L), hullparts=2), show('stock', geared=False, rarity=1.0)]))
    print("\nSKIPPING: the L-reference against the boss k levels up: TTK s / TTD s")
    for L in (5, 10, 20, 30):
        H, Dp = pilot(MEDIAN, L, items=items)
        print(f"  L{L}:", ' | '.join(f"+{k}: {A*par_scales(L+k, items)[0]/Dp:3.0f}/{ttd(H, LANCER_SHEET*par_scales(L+k, items)[1]):3.0f}" for k in (1, 2, 3, 5, 10)))
    print("\nREWARDS: credits scale HullScale(L) vs 1.025^(L-1); the siege at base 1.0x + 4 pylons 0.125x")
    for L in (1, 5, 10, 20, 30, 40):
        sh, sd = par_scales(L, items)
        print(f"  L{L:<2} credits x{sh:.2f} (was x{1.025**(L-1):.2f}) = {2000*sh:6.0f} | base {A*sh:6.0f}, pylon {0.125*A*sh:5.0f}, quarry {1.5*A*sh:6.0f}"
              f" | crates {2 + (L>=6) + (L>=11)} bounty, x2 siege")

if __name__ == '__main__':
    main()
