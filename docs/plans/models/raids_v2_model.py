# raids_v2: adds_model.py re-run on the MERGED spec.
# Changes from adds_model (FORM_FOR / SLOT_KEEPS_ROLL switch the first two back): (1) kinds are the builder's real roll (Raids._roll starts at 0 in every
# arena, and KindOf pairs pin/standoff by roll: 0 webifier+gunship, 1 talon+cross, 2 pod+lancerkin);
# (2) travel is per squad from the formation spec (form 2600 u from the pilot, anchor at the slowest
# cruise to CommitAt, then a time-on-target burn); (3) the missile fires only at a pinned pilot;
# the cross carries none. Everything else (boss, par pilot, schedule, EXP) is adds_model's.
import math
from adds_model import S, Q, interp, PAR, MH, BOSS_REAL, BOSS_SHEET, boss, adds, squads, heavies, lights, lights_in

ROW = {  # cruise, boostmult, boosttime, reach, hull, dps, missile  (Enemies.cs today; heavy dps = ruling)
    "webifier":  (100, 5.0, 3, 100, 25, 1.0, False),
    "talon":     (130, 5.0, 3, 90, 18, 1.4, False),
    "pod":       (78, 3.5, 3, 130, 60, 0.7, False),
    "gunship":   (100, 7.0, 3, 150, 100, 1.25, True),
    "cross":     (100, 6.0, 3, 140, 130, 1.25, False),
    "lancerkin": (100, 5.5, 3, 260, 150, 1.25, True),
}
PAIR = [("webifier", "gunship"), ("talon", "cross"), ("pod", "lancerkin")]
FORM, EXTENT, STERN = 2600.0, 20.0, 60.0
FORM_FOR = 10.0          # merged spec: every squad flies 10 s in formation (0: the fixed 2600 u of formations.md)
SLOT_KEEPS_ROLL = True   # merged spec: squad slot k is always pair k (a refill keeps its kinds)
def boost_at(k): c, b, t, r, *_ = ROW[k]; return c * b * t + r
def burn(k): return ROW[k][0] * ROW[k][1]
def hold(k): return ROW[k][3] * 0.9
def travel(roll, nl):
    lk, hk = PAIR[roll % 3]
    pace = min([ROW[hk][0]] + ([ROW[lk][0]] if nl else []))
    commit = boost_at(lk) if nl else boost_at(hk)
    t_form = FORM_FOR if FORM_FOR else max(0.0, FORM - commit) / pace   # doctrine FormFor: spawn at commit + pace x FormFor
    t_burn = (commit + STERN + hold(hk) + EXTENT) / burn(hk)
    if nl: t_burn = max(t_burn, (commit - hold(lk) - EXTENT) / burn(lk))
    return t_form + t_burn, t_form, commit, pace

RESPAWN, REACT, EFF, MISSILE, MISSILE_EVERY, LAND_PINNED = 30.0, 2.0, 0.7, 35.0, 12.0, 0.9
WEB_FROM_HEAVY = 20
EXP_L, EXP_H = 6, 18

def fight(L, T0=60.0, ignore=False, dt=0.05, respawn=RESPAWN, heavy_web=WEB_FROM_HEAVY):
    s = S(L); Dp = interp(PAR, L); B = B0 = Dp * T0; n = squads(L)
    roll = [0]
    slots = [dict(k=k, state="wait", due=RESPAWN * k, first=True, nl=lights_in(L, k)) for k in range(n)]
    t = dmg = pinned_t = add_dmg = boss_dmg = 0.0; exp = 0; missiles = []
    while B > 0 and t < 900:
        for sl in slots:
            if sl["state"] == "wait":
                hull_due = sl["first"] and sl["k"] > 0 and B / B0 <= 1 - sl["k"] / n
                if t >= sl["due"] or hull_due:
                    r = sl["k"] if SLOT_KEEPS_ROLL else roll[0]; roll[0] += 1
                    sl.update(state="travel", roll=r, lk=PAIR[r % 3][0], hk=PAIR[r % 3][1], t=travel(r, sl["nl"])[0])
                    sl["Lhp"] = sl["nl"] * ROW[sl["lk"]][4] * s; sl["Hhp"] = ROW[sl["hk"]][4] * s
            elif sl["state"] == "travel":
                sl["t"] -= dt
                if sl["t"] <= 0: sl.update(state="fight", eng=0.0, cd=0.0)
        eng = [sl for sl in slots if sl["state"] == "fight"]
        pinned = any(sl["Lhp"] > 0 for sl in eng) or (L >= heavy_web and any(sl["Hhp"] > 0 for sl in eng))
        if pinned: pinned_t += dt
        b = boss(L); bd = (BOSS_SHEET[b] if pinned else BOSS_REAL[b]) * s * dt
        dmg += bd; boss_dmg += bd
        for sl in eng:
            sl["eng"] += dt
            lh = ROW[sl["lk"]][4] * s
            alive = math.ceil(sl["Lhp"] / lh - 1e-9) if sl["Lhp"] > 0 else 0
            a = alive * ROW[sl["lk"]][5] * s * dt + (ROW[sl["hk"]][5] * s * dt if sl["Hhp"] > 0 else 0)
            dmg += a; add_dmg += a
            if sl["Hhp"] > 0 and ROW[sl["hk"]][6]:
                sl["cd"] -= dt
                if sl["cd"] <= 0 and pinned: sl["cd"] = MISSILE_EVERY; missiles.append(t + 10.0)
        for m in [m for m in missiles if m <= t]:
            missiles.remove(m); a = MISSILE * (LAND_PINNED if pinned else 0.15); dmg += a; add_dmg += a
        tgt = None
        if not ignore:
            for sl in eng:
                if sl["eng"] >= REACT and (sl["Lhp"] > 0 or sl["Hhp"] > 0): tgt = sl; break
        if tgt:
            hit = EFF * Dp * dt; lh = ROW[tgt["lk"]][4] * s
            if tgt["Lhp"] > 0:
                before = math.ceil(tgt["Lhp"] / lh - 1e-9); tgt["Lhp"] = max(0, tgt["Lhp"] - hit)
                after = math.ceil(tgt["Lhp"] / lh - 1e-9) if tgt["Lhp"] > 0 else 0
                if tgt["first"]: exp += EXP_L * (before - after)
            else:
                tgt["Hhp"] = max(0, tgt["Hhp"] - hit)
                if tgt["Hhp"] <= 0 and tgt["first"]: exp += EXP_H
            if tgt["Lhp"] <= 0 and tgt["Hhp"] <= 0:
                tgt.update(state="wait", due=t + respawn, first=False)
        else:
            B -= Dp * dt
        t += dt
    return dict(T=t, dps=dmg / t, add=add_dmg / t, boss=boss_dmg / t, pinned=pinned_t / t, exp=exp)

if __name__ == "__main__":
    print("TRAVEL (spawn -> engaged), first fills")
    for r, name in enumerate(["sq1 webifier+gunship", "sq2 talon+cross", "sq3 pod+lancerkin"]):
        for nl in (0, 3):
            tt, tf, c, p = travel(r, nl)
            print(f"  {name} nl={nl}: pace {p:.0f} u/s, commit {c:.0f} u, formation {tf:.1f} s, engaged {tt:.1f} s")
    print()
    print("| L | boss | H+L | DPS taken /S | x boss | pinned | fight +% | roster EXP |")
    for L in range(1, 41):
        s = S(L); r = fight(L); b0 = BOSS_REAL[boss(L)]
        print(f"| {L} | {'RUSTY' if boss(L)=='lancer' else 'DRAKE'} | {heavies(L)}+{lights(L)} | {r['dps']/s:.2f} | x{r['dps']/s/b0:.2f} | {100*r['pinned']:.0f}% | {100*(r['T']/60-1):+.0f}% | {r['exp']} |")
    print()
    print("ignore adds (worst case):")
    for L in (6, 9, 18, 20, 30, 39, 40):
        s = S(L); ig = fight(L, ignore=True); b0 = BOSS_REAL[boss(L)]
        print(" ", L, f"{ig['dps']/s:.2f}/S x{ig['dps']/s/b0:.2f} pinned {100*ig['pinned']:.0f}%")
    print("TTD boss alone -> with adds (hull 120 / 200 / 420, x1.64 x mH, 0.5%/s regen)")
    for L in (6, 9, 18, 20, 30, 39, 40):
        s = S(L); r = fight(L); bd = BOSS_REAL[boss(L)] * s; row = []
        for H in (120, 200, 420):
            hp = H * 1.64 * interp(MH, L)
            f = lambda d: "never" if d - 0.005 * hp <= 0 else f"{hp / (d - 0.005 * hp):.0f}"
            row.append(f"{f(bd)}->{f(r['dps'])}")
        print(" ", L, boss(L), f"{r['T']:.0f} s", " | ".join(row))
    print("no heavy CC ever: 20", f"{fight(20, heavy_web=999)['dps']/S(20):.2f}", " 39", f"{fight(39, heavy_web=999)['dps']/S(39):.2f}")
    print("respawn never / 20 s at 39:", f"{fight(39, respawn=1e9)['T']:.0f} s {fight(39, respawn=1e9)['dps']/S(39):.2f}/S", "|", f"{fight(39, respawn=20)['T']:.0f} s {fight(39, respawn=20)['dps']/S(39):.2f}/S")
    print("beam (RUSTY): windup vs strip time of squad 1 (3 webifiers) and + gunship")
    for L in (9, 15, 21, 31, 39):
        Dp = interp(PAR, L); s = S(L)
        lt = REACT + 3 * 25 * s / (EFF * Dp); ht = lt + 100 * s / (EFF * Dp)
        print(f"  L{L}: windup {6 / Q(L):.1f} s | strip 3 webifiers {lt:.1f} s | + gunship {ht:.1f} s | beam 4 ticks {200 * s:.0f}")
