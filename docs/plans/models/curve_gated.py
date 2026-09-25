# THE CURVE WITH LEVEL WALLS. Read-only design model; see level_gates.md.
# Imports ../curve.py (Par v1: kit chips fitted, today's items) and ../items/tiers.py (Par v2: kit chips baked
# into Base, the dropped-chip bank, compound tiers, step 0.03) and changes ONE thing: the reference pilot and
# every class wear only what the pilot's level has opened. Every wall is a row; nothing below is an `if` on a class.
# usage: python curve_gated.py [default|slow|fast|a1late]
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); UP = os.path.dirname(HERE)
sys.path.insert(0, UP); sys.path.insert(0, os.path.join(UP, 'items'))
import curve as cv
import tiers as tv

# ---------------------------------------------------------------- THE WALLS (Unlocks.All): (pilot level, opens, nth)
WALLS = {
    'default': [(1, 'A', 1), (2, 'C', 1), (3, 'A', 2), (4, 'C', 2), (6, 'A', 3), (8, 'C', 3), (10, 'C', 4), (12, 'C', 5), (14, 'C', 6)],
    'slow':    [(1, 'A', 1), (3, 'C', 1), (5, 'A', 2), (7, 'C', 2), (9, 'A', 3), (11, 'C', 3), (13, 'C', 4), (15, 'C', 5), (17, 'C', 6)],
    'fast':    [(1, 'A', 1), (2, 'C', 1), (3, 'A', 2), (4, 'C', 2), (5, 'A', 3), (6, 'C', 3), (8, 'C', 4), (10, 'C', 5), (12, 'C', 6)],
    'a1late':  [(2, 'A', 1), (2, 'C', 1), (3, 'A', 2), (4, 'C', 2), (6, 'A', 3), (8, 'C', 3), (10, 'C', 4), (12, 'C', 5), (14, 'C', 6)],
    'none':    [(1, 'A', 1), (1, 'A', 2), (1, 'A', 3)] + [(1, 'C', k) for k in range(1, 7)],
}
def opened(walls, kind, PL): return sum(1 for (lv, k, n) in walls if k == kind and lv <= PL)

# ---------------------------------------------------------------- each class's three, IN THE ORDER A PILOT LEARNS THEM
# (name, key, sheet DPS vs a lone boss). The weapon (Space, and R where the weapon has one) is never behind a wall.
# Sheets from signoff_v2 §2-4 / kits2_*; owner changes with no numbers yet are 0 and marked (?).
LEARN = {
    'BATTLESHIP': [('Broadside', 'F', 27.3), ('Brace', 'Q', 0), ('CIWS', 'E', 0)],
    'CARRIER':    [('Bomber strike', 'F', 17.45), ('Warp gunships', 'E', 9.6), ('Supercarrier (?)', 'Q', 4.0)],
    'DESTROYER':  [('Long Lance', 'F', 16.7), ('Suppressing fire', 'Q', 0), ('Grapnel', 'E', 1.0)],
    'FREIGHTER':  [('Unmask', 'F', 10.0), ('Bubble', 'Q', 0), ('Redeploy', 'E', 0)],
    'TENDER':     [('Overdrive', 'F', 6.6), ('Repair field', 'Q', 0), ('Resupply', 'E', 1.8)],
    'BASTION':    [('Bunker buster', 'F', 30.0), ('Shockwave', 'Q', 0), ('Gravity well', 'E', 0)],
    'WARRIOR':    [('Lunge', 'E', 5.7), ('Whirlwind', 'Q', 0), ('Prism stance', 'F', 4.0)],
    'SNIPER':     [('Anchor', 'F', 21.0), ('Tether mine', 'Q', 0), ('new E (?)', 'E', 0)],
    'WARDEN':     [('Hunters', 'F', 19.3), ('Taunt', 'Q', 0), ('Flak curtain', 'E', 0)],
    'DART':       [('Rod from God', 'F', 25.1), ('Ramjet (?)', 'Q', 0), ('Slingshot', 'E', 0)],
    'ECHO':       [('Reverb', 'F', 11.4), ('Rewind', 'Q', 0), ('EMP', 'E', 0)],
    'WRAITH':     [('Veil', 'F', 5.5), ('Venom', 'Q', 6.25), ('Shadow step', 'E', 0)],
}
# The freighter's sentries are its weapon's R (never walled), so its weapon share is (spotter + sentries) / sheet.
WEAPON_SHARE = {'FREIGHTER': 55.0 / 65.0}
def shares(cls):
    """(weapon share, [share of ability 1, 2, 3]) of the class's realistic DPS; the three split (1 - f_p) by sheet."""
    fp = WEAPON_SHARE.get(cls, cv.CLASSES[cls][3]); tot = sum(s for _, _, s in LEARN[cls])
    return fp, [(1 - fp) * s / tot if tot else 0.0 for _, _, s in LEARN[cls]]
def dmg_frac(cls, na):
    fp, a = shares(cls); return fp + sum(a[:na])

# ---------------------------------------------------------------- THE PACE, fight by fight (curve.py's EXP rules)
def fights(top=60, kills=4):
    """{L: [pilot level at the START of each of the level's fights]} at the reference pace."""
    pl, exp, out = 1, 0, {}
    for L in range(1, top + 1):
        out[L] = []
        for k in range(kills):
            out[L].append(pl)
            ok = L >= pl * 0.5
            ke = round(cv.KILL * L / pl) if ok else 0
            exp += (ke + (cv.FIRST if k == 0 else 0) + cv.DONE) if ok else 0
            while exp >= cv.EXPLV: exp -= cv.EXPLV; pl += 1
    return out
FIGHTS = fights()
def reached(PL):
    for L, pls in FIGHTS.items():
        for k, p in enumerate(pls):
            if p >= PL: return L, k + 1, sum(len(FIGHTS[x]) for x in range(1, L)) + k + 1
    return None

# ---------------------------------------------------------------- the gated pilot, two regimes
def pilot_K(cls, L, walls, PLs=None):
    """Par v1 (curve.py): kit chips FITTED (+5%/+5% each), today's items. Averaged over the level's fights."""
    B, D, N, fp = cv.CLASSES[cls]; H = Dp = 0.0; PLs = PLs or FIGHTS[L]
    for pl in PLs:
        n = min(N, opened(walls, 'C', pl)); f = dmg_frac(cls, opened(walls, 'A', pl))
        cv.CLASSES[cls] = (B, D * f, n, fp)
        h, d = cv.pilot(cls, L); H += h; Dp += d
        cv.CLASSES[cls] = (B, D, N, fp)
    return H / len(PLs), Dp / len(PLs)

def pilot_B(cls, L, walls, PLs=None, law='compound', step=tv.STEP_REC, share=0.65):
    """Par v2 (items/tiers.py): kit chips BAKED into Base; the dropped-chip bank worn only in opened slots
    (an upper bound on the wall's cost: early on a pilot owns fewer chips than it has slots)."""
    B, D, N, fp = cv.CLASSES[cls]; PLs = PLs or FIGHTS[L]
    g = share * min(tv.MAXLEVEL, L)
    w, h = cv.split_points(cv.PL[min(max(1, L), max(cv.PL))] - 1)
    K = 0.05 * N; pw = tv.par_worn(L - 1, law); Gg = tv.G(g, step)
    H = Dp = 0.0
    for pl in PLs:
        nf = min(N, opened(walls, 'C', pl)) / N; f = dmg_frac(cls, opened(walls, 'A', pl))
        dmg = (1 + K) * (1 + 0.03 * w + (tv.B1 / 100) * Gg * (fp * pw['W'] + (1 - fp) * pw['U']))
        hull = 1 + (tv.B1 / 100) * Gg * pw['Sh']
        dmg += (1 + K) * 0.5 * (tv.BANK1 / 100) * Gg * pw['C'] * nf; hull += 0.5 * (tv.BANK1 / 100) * Gg * pw['C'] * nf
        H += (B * (1 + K) + 5 * h) * hull; Dp += D * f * dmg
    return H / len(PLs), Dp / len(PLs)

PILOT = {'K': pilot_K, 'B': pilot_B}

def scales(reg, walls, L):
    H1, D1 = PILOT[reg](cv.MEDIAN, 1, walls); H, D = PILOT[reg](cv.MEDIAN, L, walls)
    return D / D1, H / H1, 60 * D1, 60 * D           # HullScale, DamageScale, anchor, absolute boss hull (Lancer)

def ttd(H, sheet): return cv.ttd(H, sheet)

def main():
    name = sys.argv[1] if len(sys.argv) > 1 else 'default'
    W = WALLS[name]; NONE = WALLS['none']
    print(f"== WALLS '{name}':", ' '.join(f"PL{lv}:{k}{n}" for lv, k, n in W))
    print("\n-- when the reference pilot (4 clears a level) reaches each wall: boss level, fight of that level, fights so far")
    for lv, k, n in W:
        L, f, tot = reached(lv); print(f"   PL{lv:<3} {k}{n}: boss L{L} fight {f} (fight #{tot})")
    print("   pilot level entering boss L:", {L: FIGHTS[L][0] for L in range(1, 16)})

    print("\n-- class shares (weapon | ability 1, 2, 3 in learn order) and what each wall state leaves of full damage")
    for c in LEARN:
        fp, a = shares(c)
        print(f"   {c:<11} w {fp:.2f} | " + ' '.join(f"{n}({k}) {s:.3f}" for (n, k, _), s in zip(LEARN[c], a))
              + f" || A0 {dmg_frac(c,0):.2f}  A1 {dmg_frac(c,1):.2f}  A2 {dmg_frac(c,2):.2f}")

    for reg, title in (('K', "PAR v1 (curve.py): kit chips fitted, today's items"), ('B', "PAR v2 (items/tiers.py): kit chips baked, chip bank, compound, step 0.03")):
        print(f"\n== {title}")
        s0 = scales(reg, NONE, 1); s1 = scales(reg, W, 1)
        print(f"   anchor (Lancer L1 = 60 s x Par DPS(1)): ungated {s0[2]:.0f}, gated {s1[2]:.0f}  (x{s1[2]/s0[2]:.3f});"
              f"  Par hull(1): ungated {PILOT[reg](cv.MEDIAN,1,NONE)[0]:.0f}, gated {PILOT[reg](cv.MEDIAN,1,W)[0]:.0f}")
        print("   L  | PL in | gated HullScale DamageScale | boss hull (Lancer) gated / ungated | ungated H/D scale | par TTD Lancer (move rows x DamageScale) gated / ungated")
        for L in list(range(1, 16)) + [20, 30, 40]:
            g = scales(reg, W, L); u = scales(reg, NONE, L)
            Hg = PILOT[reg](cv.MEDIAN, L, W)[0]; Hu = PILOT[reg](cv.MEDIAN, L, NONE)[0]
            print(f"   {L:<3}| {FIGHTS[L][0]:>2}-{FIGHTS[L][-1]:<2}| {g[0]:6.3f}  {g[1]:6.3f}           | {g[3]:6.0f} / {u[3]:6.0f}  ({g[3]/u[3]:.3f})"
                  f"     | {u[0]:6.3f} {u[1]:6.3f}   | {ttd(Hg, cv.LANCER_SHEET*g[1]):5.1f} / {ttd(Hu, cv.LANCER_SHEET*u[1]):5.1f}")
        print("   TTK s / TTD s (Lancer sheet), every class against the GATED boss, gated pilots; the ungated L20 figure last")
        cols = (1, 2, 3, 4, 5, 6, 8, 10, 12, 15)
        print("   class       " + ' '.join(f"{'L'+str(L):>9}" for L in cols) + "   ungated")
        for c in cv.CLASSES:
            row = []
            for L in cols:
                hs, ds, A, _ = scales(reg, W, L); H, D = PILOT[reg](c, L, W)
                row.append(f"{A*hs/D:4.0f}/{ttd(H, cv.LANCER_SHEET*ds):<4.0f}")
            hs, ds, A, _ = scales(reg, NONE, 20); H, D = PILOT[reg](c, 20, NONE)
            print(f"   {c:<11} " + ' '.join(row) + f"   {A*hs/D:4.0f}/{ttd(H, cv.LANCER_SHEET*ds):.0f}")
        print("   within a level: the par's TTK at each of the level's fights (a wall crossed mid-level moves the later fights)")
        for L in range(1, 13):
            hs, ds, A, _ = scales(reg, W, L)
            per = [A * hs / PILOT[reg](cv.MEDIAN, L, W, PLs=[p])[1] for p in FIGHTS[L]]
            wc = [c for c in cv.CLASSES]
            worst = max(wc, key=lambda c: max(A*hs/PILOT[reg](c, L, W, PLs=[p])[1] for p in FIGHTS[L]) / (A*hs/PILOT[reg](c, L, NONE)[1]))
            spread = [A * hs / PILOT[reg](worst, L, W, PLs=[p])[1] for p in FIGHTS[L]]
            print(f"   L{L:<3} PL {FIGHTS[L]}  DESTROYER " + ' '.join(f"{x:4.1f}" for x in per)
                  + f"  | most walled vs its own ungated: {worst} " + ' '.join(f"{x:4.0f}" for x in spread))
        print("   a pilot BEHIND the reference's walls (skipped ahead, or carried): par class at boss L with the PL of boss L-k")
        for L in (4, 6, 8, 10, 12):
            hs, ds, A, _ = scales(reg, W, L); out = []
            for k in (0, 2, 4):
                src = max(1, L - k)
                H, D = PILOT[reg](cv.MEDIAN, L, W, PLs=FIGHTS[src]); out.append(f"-{k}: {A*hs/D:4.1f}")
            print(f"   L{L:<3}", ' | '.join(out))

if __name__ == '__main__':
    main()
