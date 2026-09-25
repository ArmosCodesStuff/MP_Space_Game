import math
LS = 1.025; MS = 1.01
def S(L): return LS ** (max(1, L) - 1)
def Q(L): return MS ** (max(1, L) - 1)

# ---- the schedule: one add every 3 levels from 6, cadence H L L L, cap 12 ----
FROM, EVERY, CAP, CAD = 6, 3, 12, 4
def adds(L): return 0 if L < FROM else min(CAP, (L - FROM) // EVERY + 1)
def squads(L): return (adds(L) + CAD - 1) // CAD
def heavies(L): return squads(L)
def lights(L): return adds(L) - heavies(L)
def lights_in(L, k): return max(0, min(CAD - 1, adds(L) - CAD * k - 1))

# ---- rows (today's Enemies.All; heavy laser per the owner's ruling) ----
LIGHT_DPS = (1.0 + 1.4 + 0.7) / 3          # webifier, talon, pod
LIGHT_HULL = (25 + 18 + 60) / 3
HEAVY_LASER = 1.25 * 1.0                     # twin laser, 1.25 x a webifier (ruling)
HEAVY_TODAY = (2.0 + 2.6 + 1.8) / 3
HEAVY_HULL = (100 + 130 + 150) / 3
MISSILE, MISSILE_EVERY, MISSILE_SHARE = 35.0, 12.0, 2 / 3   # 10 s flight; gunship + lancerkin carry it
WEB_FROM_HEAVY = 20
EXP_LIGHT, EXP_HEAVY = 6, 18

# ---- boss DPS per S (boss_model.py) ----
BOSS_REAL = {"lancer": 8.02, "drake": 3.91}
BOSS_SHEET = {"lancer": 17.05, "drake": 13.23}
def boss(L): return "lancer" if (L - 1) % 2 == 0 else "drake"

# ---- par pilot (fit.py, reading B 20k/h, today items) ----
PAR = {1: 56.5, 5: 65.9, 10: 82.4, 20: 116.1, 30: 134.8, 40: 139.9}
MH = {1: 1.0, 5: 1.136, 10: 1.406, 20: 1.947, 30: 2.254, 40: 2.316}
def interp(tab, L):
    ks = sorted(tab)
    if L <= ks[0]: return tab[ks[0]]
    for a, b in zip(ks, ks[1:]):
        if a <= L <= b: return tab[a] + (tab[b] - tab[a]) * (L - a) / (b - a)
    return tab[ks[-1]]

FIRST, RESPAWN, TRAVEL, REACT, EFF = 0.0, 30.0, 8.0, 2.0, 0.7

def fight(L, T0=60.0, ignore=False, dt=0.05, heavy_dps=HEAVY_LASER, land_free=0.15, land_pinned=0.9):
    s = S(L); Dp = interp(PAR, L); B = Dp * T0; B0 = B
    n = squads(L)
    # per slot: state
    slots = []
    for k in range(n):
        slots.append(dict(k=k, state="wait", due=FIRST + RESPAWN * k, t=0.0, L=lights_in(L, k), Lhp=0.0, Hhp=0.0,
                          first=True, eng=0.0, paid=0, miss_cd=0.0))
    t = 0.0; dmg = 0.0; pinned_t = 0.0; exp = 0; add_dmg = 0.0; boss_dmg = 0.0; missiles = []
    shot_adds = 0.0
    while B > 0 and t < 600:
        # arrivals (hull share or time, whichever first)
        for sl in slots:
            if sl["state"] == "wait":
                hull_due = sl["first"] and sl["k"] > 0 and B / B0 <= 1 - sl["k"] / n
                if t >= sl["due"] or hull_due:
                    sl["state"] = "travel"; sl["t"] = TRAVEL
                    sl["Lhp"] = sl["L"] * LIGHT_HULL * s; sl["Hhp"] = HEAVY_HULL * s; sl["nl"] = sl["L"]
            elif sl["state"] == "travel":
                sl["t"] -= dt
                if sl["t"] <= 0: sl["state"] = "fight"; sl["eng"] = 0.0; sl["miss_cd"] = 0.0
        engaged = [sl for sl in slots if sl["state"] == "fight"]
        # pinned?
        pinned = any(sl["Lhp"] > 0 for sl in engaged) or (L >= WEB_FROM_HEAVY and any(sl["Hhp"] > 0 for sl in engaged))
        if pinned: pinned_t += dt
        # boss damage to pilot
        b = boss(L)
        bd = (BOSS_SHEET[b] if pinned else BOSS_REAL[b]) * s * dt
        dmg += bd; boss_dmg += bd
        # adds' damage
        for sl in engaged:
            sl["eng"] += dt
            nl_alive = math.ceil(sl["Lhp"] / (LIGHT_HULL * s) - 1e-9) if sl["Lhp"] > 0 else 0
            a = nl_alive * LIGHT_DPS * s * dt + (heavy_dps * s * dt if sl["Hhp"] > 0 else 0)
            dmg += a; add_dmg += a
            if sl["Hhp"] > 0:
                sl["miss_cd"] -= dt
                if sl["miss_cd"] <= 0:
                    sl["miss_cd"] = MISSILE_EVERY
                    missiles.append(t + 10.0)
        # missiles landing
        for m in [m for m in missiles if m <= t]:
            missiles.remove(m)
            a = MISSILE * MISSILE_SHARE * (land_pinned if pinned else land_free)
            dmg += a; add_dmg += a
        # the pilot shoots: adds after REACT, lights first
        target = None
        if not ignore:
            for sl in engaged:
                if sl["eng"] >= REACT and (sl["Lhp"] > 0 or sl["Hhp"] > 0): target = sl; break
        if target:
            hit = EFF * Dp * dt; shot_adds += dt
            if target["Lhp"] > 0:
                before = math.ceil(target["Lhp"] / (LIGHT_HULL * s) - 1e-9)
                target["Lhp"] = max(0, target["Lhp"] - hit)
                after = math.ceil(target["Lhp"] / (LIGHT_HULL * s) - 1e-9) if target["Lhp"] > 0 else 0
                if target["first"]: exp += EXP_LIGHT * (before - after)
            else:
                target["Hhp"] = max(0, target["Hhp"] - hit)
                if target["Hhp"] <= 0 and target["first"]: exp += EXP_HEAVY
            if target["Lhp"] <= 0 and target["Hhp"] <= 0:
                target["state"] = "wait"; target["due"] = t + RESPAWN; target["first"] = False
        else:
            B -= Dp * dt
        t += dt
    return dict(T=t, dmg=dmg, dps=dmg / t, boss_dps=boss_dmg / t, add_dps=add_dmg / t, pinned=pinned_t / t, exp=exp, shot_adds=shot_adds)

if __name__ == "__main__":
    print("L  boss   n  sq  H  L  | T(s) +T%  | DPS/S taken  base  x   | adds/S  pinned%  | EXP(first fills, PL=L) | ignore: DPS/S x  pinned%")
    for L in [1, 5, 6, 8, 9, 12, 15, 18, 20, 21, 24, 27, 30, 33, 36, 39, 40]:
        s = S(L); b = boss(L)
        base = fight(L) if adds(L) == 0 else None
        r = fight(L); ig = fight(L, ignore=True)
        b0 = BOSS_REAL[b]
        print(f"{L:2d} {b:6s} {adds(L):2d} {squads(L):2d} {heavies(L):2d} {lights(L):2d} | {r['T']:5.1f} {100*(r['T']/60-1):+4.0f}% | "
              f"{r['dps']/s:6.2f} {b0:5.2f} x{r['dps']/s/b0:4.2f} | {r['add_dps']/s:5.2f} {100*r['pinned']:5.1f}% | {r['exp']:4d} | "
              f"{ig['dps']/s:6.2f} x{ig['dps']/s/b0:4.2f} {100*ig['pinned']:5.1f}%")
